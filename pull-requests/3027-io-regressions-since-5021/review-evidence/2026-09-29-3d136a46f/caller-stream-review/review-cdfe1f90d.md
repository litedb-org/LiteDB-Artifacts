## Review of cdfe1f90d: 2 real defects, 1 minor cost issue, and some test gaps

I did not change the repository. I added the proof tests only in the scratch worktree `$SCRATCH/review-cs/wt` (detached at cdfe1f90d), in `LiteDB.Tests/Review/HeldWritesReview_Tests.cs` and `LiteDB.Tests/Review/HeldWritesReopen_Tests.cs`. Run them with `--filter FullyQualifiedName~LiteDB.Tests.Review`: 4 fail, 4 pass. Note that `anchorwt` moved on during the review (a docs-only commit e94e5a22b now sits on top).

### Finding 1 (Medium–High, breaks the main invariant): a write error that looks like "cannot sync" is dropped in `WriteOn`
- **Where:** `LiteDB/Engine/Disk/Streams/HeldWrites.cs:42-45`
- **What goes wrong:** `WriteOn` calls `stream.Flush()`. On a BufferedStream (or a FileStream with a large buffer) that call only writes the buffer on; it never syncs. So any exception there is a *write* error. `IsDurableFlushUnsupported` still matches some of them:
  - `UnauthorizedAccessException`, which .NET throws for EACCES/EPERM/EBADF on a write;
  - an `IOException` whose raw errno is EINVAL, EROFS or ENOTSUP.
- **Sequence:**
  1. A reader's write-on tears the held batch and throws one of these.
  2. `WriteOn` treats it as "handed on", sets `_writer = null` and records no hand-off.
  3. The reader continues with `_stream.Position = …`. BufferedStream retries the write it kept, at the device's already-advanced position, and the retry succeeds.
  4. Nobody hears about the failure. The batch is acknowledged, and the frame is torn on the device.
- **Proof:** `Reader_write_on_failure_classified_as_cannot_sync_is_not_lost`. It is SharedBufferedLogStream_Tests with the one-shot tear throwing `UnauthorizedAccessException` or `IOException(…, 22)`. Rows `wal-confirmation-after-write/unauthorized` and `/einval` fail with: reader no error, writer no error, acknowledged 32, recovered 1. So 31 acknowledged rows are lost.
- **Fix, tested:** delete the exemption. With it gone, my 4 rows plus the SharedBufferedLogStream, TornWalAppend and CallerStreamFlushCost tests all pass (30/30), and so does the whole `LiteDB.Tests.Regressions` namespace (318/318).
  - Nothing appears to need the exemption. A stream whose `Flush()` always answers "cannot sync" already fails the writer's own flush at the end of each batch.
- **How realistic:** it needs a transient failure and an inner stream that advances its position on a partial write. That is the same model the commit's own tests use.

### Finding 2 (Medium, the read-only continuation of decision 6): a reader's write-on I/O failure closes the engine for good
- **Where:** `HeldWrites.cs:46-54` rethrows the raw exception without naming a file, so `EngineState.Handle` (`EngineState.cs:110-129`) treats it as a failed read.
- **What goes wrong:** a device I/O failure during a reader's write-on is a write failure. Instead of the next call reopening read-only, the engine stays closed with "Engine closed after an I/O failure. Dispose and reopen…". Before this commit a reader never wrote on, so this failure only ever reached the writer, whose failure is recorded and gives the read-only reopen.
- **Proof:** `Reader_write_on_failure_leaves_a_read_only_continuation`. The `io` rows fail as described; the `other` rows (non-I/O failure, reaching the writer only through the hand-off) pass and read back 1 row as expected.
- **Fix, tested:** give `HeldWrites` its file (`StreamFactory` passes `_isLog ? Log : Data`) and call `WriteFailure.InFile(ex, origin)` before rethrowing. The reopen tests then pass, as do the existing 30.
- The same applies on the data side (a data reader during a partial checkpoint).

### Finding 3 (Low, cost): readers can still add flushes
Nothing is flushed per write any more. But every reader cache miss that lands after one of the writer's writes calls `Flush()` on the base stream, which is a sync for a stream whose `Flush()` syncs. Under concurrent reads that goes up to c4119caba's per-page cost, where 5.0.21 had none. No test covers this.

### Checked and found sound
1. **Log writes outside a batch.** Retirement records and clears, the header journal, `ShrinkLog`/`EmptyLog`, conversion, and promotion are each followed by `SyncLogBarrier` or a data sync while the writer lock is held. The batch always ends with a flush. So the log writer never holds anything at the start of a batch, and a hand-off cannot land in a batch's first-frame truncation.
2. **`uncertain` / `earlierHeld`.** For a buffering log, once any frame (the header frame included) is written, `uncertain` stays true, and truncation only clears it for the batch's first append with nothing held before it. The hand-off exception (`COR_E_IO`, no Data keys) does not match `IsDurableFlushUnsupported`, `IsUnsyncedStorage`, `IsQuietOverwriteRefusal` or `IsRefusedBeforeWrite`. The one place it is swallowed, as a failed truncation's cleanup exception, keeps `uncertain` true.
3. **Checkpoint data pages.** A torn held page is thrown at the next page write or at `SyncDataBarrier` (not caught by `FailedIn`). Both run inside the same `UseDataWriter`, before `KeepsWal`, `CompletePartialCheckpoint`, `ReclaimLogPages` and `EmptyLog`. So the checkpoint fails before the WAL shrinks.
4. **Locking and wrappers.** Every `HeldWrites` access is under `lock(baseStream)`. All three `ConcurrentStream` construction sites share the factory's `_held`. The extra lock in `Exists()` cannot deadlock (only the reentrant base monitor is taken).

### Test gaps
- **SharedBufferedLogStream `io` rows:** they cannot tell the hand-off apart from the reader's own stop, and they never check the read-only continuation (finding 2).
- **Cannot-sync classification:** no test covers errors that `WriteOn` treats as "cannot sync" (finding 1).
- **Data side:** no test with a tearing BufferedStream as `DataStream` and a data reader during a partial checkpoint (it should assert the checkpoint fails, the WAL is kept, and recovery is correct).
- **Encryption:** no `Password` variants of the SharedBufferedLogStream or TornWalAppend buffering rows. CallerStreamFlushCost only counts flushes.
- **Header frame:** no test tears a held header frame through a write-on (`StreamFactory.GetLength`/`Exists` during `header-frame-after-write` on an emptied WAL).
- **Reopen over the same stream:** no test reopens over the same caller stream after a tear (BufferedStream keeps the failed buffer, and the reopen writes it on).
- **Flush cost with readers:** CallerStreamFlushCost has no concurrent-reader case (finding 3).
