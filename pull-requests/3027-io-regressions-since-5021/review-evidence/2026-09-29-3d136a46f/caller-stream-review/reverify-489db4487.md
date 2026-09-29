## Re-verification at 489db4487: both findings are fixed, and I found no hole opened by Heard()

I made a fresh worktree at `$SCRATCH/review-cs/wt3` and copied my two proof tests from `LiteDB.Tests/Review` into it. I changed nothing in the repository.

**(a) Proof tests**
- **Finding 1 is fixed.** `Reader_write_on_failure_classified_as_cannot_sync_is_not_lost` passes in all 4 cases. Before, the two `wal-confirmation-after-write` cases (unauthorized, einval) lost 31 acknowledged rows.
- **Finding 2 is fixed.** `Reader_write_on_failure_leaves_a_read_only_continuation` passes in all 4 cases, including the two `io` cases that used to report "Engine closed after an I/O failure".
- I ran them with the SharedBufferedLogStream, TornWalAppend, CallerStreamFlushCost and Issue2818 test classes: 81/81 pass.

**(b) Can Heard() drop an earlier held write's failure?** Heard() clears the marker when the writer's own call fails. After that, a reader's seek can still make the stream write on bytes it kept, with no record and no file named. That only matters if the engine carries on after the writer's own failure while earlier bytes are still held. I found no such path:
- **WAL batch, buffering log:** `uncertain` stays true from the first write of the batch (the header frame included) until the final flush succeeds. So any failure the writer hears after a write stops the engine. The only outcomes where the engine carries on ("not committed") come before the batch's first write, or at that first write once its truncation succeeded. At that point nothing earlier is held: the previous batch ended with a flush, and every log write outside a batch is followed by a log sync under the writer lock. The TornWalAppend "frame" cases cover the writer tearing its own earlier frame, and they pass.
- **Data writer:** a failed page write or sync during a checkpoint propagates, and the checkpoint stops before the WAL shrinks. When a sync answers "cannot sync", the writer's own `Flush()` retries the held bytes, and a failure there propagates. The failure paths of promotion, header repair and conversion either stop the engine or propagate.

**Remaining minor points (not proven harmful)**
- **The marker survives two failures.** When a reader's write-on fails, the marker stays set, so every later reader access retries the write-on. When `ThrowIfTorn` hands a failure to the writer, it throws from `BeforeWrite`, outside the `try` that calls `Heard()`, so the marker is not cleared there either. In both cases readers keep flushing the file after a failure the writer has already heard, which is what Heard() was added to stop. The engine stops in both cases, so no commit is lost.
- **The writer's own barriers still misclassify.** `SyncLogBarrier`, `SyncDataBarrier`, `SyncRawLog` and `RequireDurableCommit` treat a failure of the write-on inside their FlushToDisk call (EINVAL/EROFS/ENOTSUP or `UnauthorizedAccessException`) as "cannot sync". Each is followed by a retry, a `LogCannotSync` error, or the checkpoint keeping the WAL, and I found no loss through them.
