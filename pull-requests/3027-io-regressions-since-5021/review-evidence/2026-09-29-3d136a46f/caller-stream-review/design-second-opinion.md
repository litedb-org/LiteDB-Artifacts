## 1. Verdict

**L (with the fixes below) is the most correct; S is a defensible fallback that re-opens a documented hole.** Confidence ~75% that L is right in principle; lower (~55%) that it lands cleanly in this PR, because it touches the WAL failure window again.

Why S is not "most correct" under the owner's rule: the *engine* is what shares one caller stream between the WAL writer and N reader threads (StreamFactory.cs:46,50; StreamPool.Rent) and it is the engine's reader thread that triggers the caller stream's flush. A stream that buffers until `Flush()` is inside the `Stream` contract, so a held frame torn on a reader's thread is ours. S closes it only for three types.

Why c4119caba as-is is wrong: it pays on the writer's thread for every caller stream whether or not a reader exists (harness: 4x fsyncs, single-threaded, `tools/IndexMigrationRecovery/Program.cs` has no threads).

## 2. Flaws in L as described

**a. The writer's own Length re-creates the per-write flush.** `WriteLogPage` reads `stream.Length` before every frame (DiskService.WalWrite.cs:224 → ChecksummedWalStream.cs:16,28 → ConcurrentStream.Length). If "any non-write access flushes when Dirty" includes the dirtying wrapper, L is c4119caba moved one line. Dirty must record *which wrapper* wrote; only a *different* wrapper's access flushes. (BufferedStream/FileStream.Length flush internally anyway, on the writer's thread; covered by d.)

**b. Poisoning reads breaks decisions 2/6.** After the stop, `EnsureOpen` reopens read-only over the *same* caller streams (LiteEngine.WriteFailure.cs:47-49, 63-66; `Clone()` is memberwise, EngineSettings.cs:79 → CreateLogFactory → new StreamFactory over the same base). A ConditionalWeakTable keyed on the base carries the poison into the reopen: every read throws, and `AcknowledgedLogAt` / `BoundLogToAcknowledged` (DiskService.AcknowledgedLog.cs:44-45, 74-77) fall back to "files win". So: never poison reads; throw poison only from Write/Flush/FlushToDisk/SetLength; keep the state in the `StreamFactory` instance (the single creation point, StreamFactory.cs:46,50,79) instead of a CWT, so a reopen starts clean. Do **not** let SetLength clear it: the writer's truncation of frame N+1 (WalWrite.cs:293) says nothing about a reader-torn frame N.

**c. Direct base accesses.** `StreamFactory.GetLength` (StreamFactory.cs:61-93: Length, Position, ReadByte) is called at run time by `ReadLogFrom` (DiskService.cs:255-256), HeaderJournal.cs:58, Validation.cs:69. `Exists()` (StreamFactory.cs:99) takes **no lock** and runs at run time (Dispose.cs:16, DiskService.cs:255): with a BufferedStream base, `Length` flushes its write buffer concurrently with the writer — a pre-existing race on dev, independent of failures. Both must go through the shared state and the lock. `TrimCapacity` (119-136) is owned-streams only, fine.

**d. The batch-wide window must return essentially as 76a28bdd4, not simpler.** Three sites: WalWrite.cs:267 (`uncertain = false` after the write), :296 (truncation clears only when `count == 0`), and the final `stream.Flush()` at :196, which today sits **outside any catch**: under L a poison or a BufferedStream's own flush failure there escapes the writer lock with a torn frame in the log and no stop — exactly the 76a28bdd4 bug. One boolean (`_logMayBuffer`) is enough; the failure always surfaces on the writer's thread (internal flush at the next Length/Write, or the poison at the next base access).

**e. Write-then-acknowledge without a Flush: none found.** WalWrite ends in FlushConfirmedLog (:179) or Flush (:196); data writes sit inside `UseDataWriter` and end in `SyncDataBarrier` (HeaderJournal.cs:113-125, Retirement.cs:85-97, HeaderFrame.cs:130); retirement records → SyncLogBarrier (Retirement.cs:64-70); preambles → FlushToDisk (AesStream.cs:164, EncryptedLogPreamble.cs:64-75). `Pad()` writes inside Flush/FlushToDisk (ChecksummedWalStream.cs:100-107, WalPadding.cs:22-35) but through the wrapper before the base flush, so Dirty clears in the same call.

**f. Lock ordering: no new edge.** ConcurrentStream holds only `lock(_stream)` and calls the base; the cache invokes the read factory *after* leaving `_sync` (MemoryCache.cs:71-113); the writer nests writer-lock → base-lock, never the reverse; readers take base-lock only. A Flush inside Read under base-lock adds nothing. Only risk is a caller Flush that blocks on the caller's own lock — out of scope.

**g. Cost.** One Flush per reader access that finds another wrapper's Dirty (an fsync for FaultStream), bounded by log-page cache misses during a batch; zero when single-threaded.

## 3. Questions for the owner (two knobs)

1. **"When someone gives us a buffering stream and reads while we write, do we guarantee acknowledged commits survive a failure on the reader's thread?"** Yes → L (shared dirty/poison state per factory, batch-wide window back; ~100 lines in a subtle path). No → S (type list; residual documented as out of scope).
2. **"Is one extra `Flush()` per commit, only when a reader runs concurrently, acceptable for a custom caller stream?"** That is L's whole cost. If even that is not → S.