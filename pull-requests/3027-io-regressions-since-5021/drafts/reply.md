Thanks for the review. Every finding is addressed below; the head is now `ad2625283`. Each fix has tests that fail when it is reverted (mutation-checked), and independent reviewers re-reviewed each fix until they had no findings left. The PR description is rewritten to match the branch.

### Open findings

| # | Finding | Outcome |
|---|---|---|
| 1 | Buffering caller log stream loses an acknowledged commit | Fixed. `76a28bdd4` kept the failure window open for the whole batch, as you suggested. Re-review then found a case it missed: a **reader** thread's seek wrote the held frame on, tore it on its own thread, and the writer never saw the failure (31 of 32 acknowledged rows lost). `c4119caba` replaces the window: a caller stream other than a `MemoryStream` is flushed after each write, inside the lock readers take, and `Length`/`Flush` take that lock too. So nothing is held once a write returns. Tests: the `TornWalAppend_Tests` buffering rows (`BufferedStream`, and a stream that holds its latest write), and `SharedBufferedLogStream_Tests` (a reader in the window after a frame write). Cost: one `Flush()` per page write on such streams. |
| 2 | Caller streams where only the data stream cannot sync | Fixed for caller `FileStream`s (`7887c96f9`): they are proven like files the engine opens. From `2b2347bb4`, the proof comes before an engine's first log sync of any kind, not just its first commit; that also covers the torn-tail repair and the checkpoint journal. `5fca247fd` keeps the healthy-storage fsync count at `dev`'s level for opens, commits and shared operations (the table in the description). Other caller streams (memory streams, custom devices) stay the caller's to share; that is listed as a residual. |
| 3 | Neither file syncs during a full checkpoint, then only the WAL syncs | Fixed (`5136e938e`), after a second external review argued it should block rather than be documented. After a data sync answers "cannot sync", every log sync first retries the data sync, and while the data file still cannot sync the log is only flushed to the OS cache; a new WAL's directory entry waits too. So the WAL that last synced stays the durable one. Test: `UnsyncedBackfillPowerLoss_Tests.Commits_durable_before_neither_file_synced_survive_when_only_the_wal_syncs_again`, run with a long-lived engine, an independent connection and a reopen: commits 1..5 are durable, and after the power loss every document and the value index match. Before the fix, every row reverted to the setup value. |
| 4 | WAL grows without bound when only the data file cannot sync | Fixed by the same work (`5136e938e`, `506678daf`). A WAL is kept only while it holds frames this process synced and the data file has not synced since (`bc7c3ca82` remembers that by the WAL's path, so the next engine or shared operation keeps it too); automatic checkpoints then wait for a data sync that succeeds instead of rescanning the WAL at every commit. A WAL no log sync reached, such as on storage that never synced, is emptied, so it stays bounded (`Wal_stays_bounded_where_only_the_data_file_cannot_sync`). A 5.x conversion whose drain keeps such a WAL is refused with an `IOException` naming the storage, and a rebuild runs as where neither file syncs. |
| 5 | `FindById` after a failed safepoint write | Fixed (`e1aeaa947`): every operation in such a transaction fails at its snapshot with "can only be rolled back", point reads included. `FailedSafepointWrite_Tests("read")`. |
| 6 | Salvaged partial document looks complete | Fixed (`4205a5102`): `_rebuild_errors` names the kept document's `_id` and says its fields after the damage are missing. The release notes cover this and the null unique key. |
| 7 | Files 5.0.21 changed after a prerelease wrote vector indexes | Fixed (`0c0df7b85`): a section that does not decode, or does not name exactly the page's IndexType-1 indexes, is ignored. The read-only open reads every document, a writable open reports the vector index as damage, and the rebuild drops only that index. A test for a section that decodes but names other indexes is in `a6af38881`. |
| 8 | Behaviour changes the description should list | Listed in the description and the release notes (`a2d0853cc`): the fsync costs, a data EIO during the proof, the engine stops, rollback-only transactions, and every refusal and its code. `LOCK_TIMEOUT` is in `docs/`, and `dd96bd002` refuses a blocked conversion before validating any document. `f70646ca0` keeps the damage in `Data["LiteDB.RebuildCause"]` when the repeated open fails with another exception type. The misleading sentence is gone. |

**The unbounded legacy page IDs** (your suggestion under "Still open") are fixed by `95d9f7ff7`, `3c502e92f`, `11463c02a` and `3b8744cdf`. A committed legacy page must be a page of its type (page 0 is the header, and only page 0). A committed header must carry the data file's creation time. A committed page ID must be at most max(LastPageID, data pages − 1) + WAL pages, where a committed header raises LastPageID. Each case fails the open with `INVALID_DATABASE` and changes neither file. The bound was checked against real 5.0.21 WALs:
- 10 crash images: `InitialSize`, a 2,910-page WAL, churn, a rolled-back bulk insert and a grown file, plain and encrypted.
- A concurrent-writer crash, where commits take IDs after pages an open transaction was handed but never wrote (`ConcurrentWalCrash_5_0_21.zip`). The first bound refused this one.
- A WAL of another database, which the raised bound would otherwise replay into the file (`ForeignWal_5_0_21.zip`).

### Test evidence

- `LegacyWalTornTail` moved to a new `IoSafety` category, for defects that are not regressions since 5.0.21. `TornWalAppend`, `FailedSafepointWrite`, `FailedPromotionJournal` and the other new I/O classes have it too (`5cfbf98a2`).
- `TornWalAppend.Failed_truncation(ioFailure: false)` is described as a policy change (the engine stops), not a data-loss fix on `dev`.
- `TornSlotRewrite` uses a hook in place of the 300 ms sleep: the stop mutant is caught 5 of 5 times.
- `SharedMutexNameLength` is reported as skipped on Windows (`UnixFact`).
- The untested `JournalBytes == 0` guard (`ab535861d`) is now isolated. Its deferred-stop rows fail without it: `CheckpointFailureWindow_Tests.Append_after_a_torn_checkpoint_header_keeps_the_journal` reaches it through a hook that restores the old stop timing. In `FailedPromotionJournal` the engine stop is what protects the data, as you found; that test now carries the `IoSafety` trait.
- `UnsyncableRetirement` checks the row count.
- `LegacyReadOnlyStream` compares every document with a writable open of private copies.
- Power-loss model limits (one state per crash, no reordered or torn writes) are listed as residuals.

### Found in later review rounds, fixed here

- `8c8900bc6` and `125497376`: a torn header's journal becomes durable before any data sync; for encrypted files, the `AesStream` constructor synced the data file first.
- `dd1f86c90`: a failed checkpoint stops the engine before it releases the WAL writer, so a waiting commit can no longer append behind a torn retirement record. `94dc01500` tests that the engine is then closed.
- `843dd045d`: only the first WAL frame is checked beside a legacy header.
- `5a748e6a9`: an encrypted stream synced its file whenever it was created, including every reader a pool adds for a concurrent read. That sync bypassed the engine's barriers, so with a password the external review's scenario still lost commits; now only a writer syncs.
- `506678daf`: the OS can write an emptied WAL back ahead of the data file's backfill, so a full checkpoint whose backfill did not sync keeps a WAL whose frames the engine synced (tested with the WAL written back and the data file as of its last sync).
- `bc7c3ca82`: the record of a WAL with synced frames was per engine, so the next engine over the same files (a reopen, the next shared operation) failed its data proof on the header the backfill rewrote and emptied the WAL; the process now keeps the record by WAL path (`Wal_kept_by_one_engine_is_kept_by_the_next`).
- `68931ef4f`: a readable part the rebuild rejected (a unique conflict) no longer reserves its `_id`, so a later part with that `_id` that fits is kept (second external review; fixture written by 5.0.21).
- `f84c10f33`: the durable-header cache's contract is stated in the release notes (whoever replaces a database file at its path must sync it; a copy with a byte-identical header is taken as the synced file) and pinned by a warm-cache replacement test. Binding the cache to one open-file lifetime would instead add a data fsync per direct-mode open and per shared operation; say if you prefer that.
- `ad2625283`: the checksum-fuzz corpus pins the byte trace of two encrypted conversion-crash seeds; `5a748e6a9` removed the reader syncs from that trace, so both hashes were re-pinned (the old ones kept under `previousHashes`).
- `2b3ba4b90`: `Issue2818_FlushFallback_Tests` counted the per-write flush of `c4119caba` as a fallback flush (red on the full suite); it now counts only plain flushes after the durable flush failed.

### Validation on `ad2625283`

- **Local, Linux x64:** the full suite in partitions on `ceb610c80` (this branch merged with `dev`; `ad2625283` after it changes only the fuzz corpus), 5,008 passed, 0 failed, 7 skipped on .NET 10 and on .NET 8.
- **Hosted CI on `ad2625283`:** green: Linux .NET 8 and .NET 10, Windows .NET 10 and net481, Fuzz (both checksum shards, determinism, v8 differential, smoke) and Index migration compatibility.
- **Not covered:** macOS (its jobs are skipped by the workflow), a real power cut.

---
_Generated by [Claude Code](https://claude.ai/code)_
