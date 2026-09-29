The regression proof for the db394db6f fix is committed as **e996fe8dd9df1f3b618161b61c8e77e3a290bf1b** (detached HEAD in my worktree on top of 899b5acc5, not pushed). The known bad fails and the candidate passes on every run, including under load.

**Repro:** `LiteDB.ReproRunner/Repros/Issue_3027_PromotionAfterFailedCheckpoint/` (csproj pinned to 6.0.0-prerelease.319, `Program.cs` at 298 lines, README, `repro.json` pinning both exit codes and log texts). I also appended:
- an entry in `.github/safety/regression-proofs.json`, guard `LiteDB.Tests/Regressions/CheckpointFailureWindow_Tests.cs#Promotion_waiting_on_a_failed_checkpoint_syncs_and_writes_nothing`;
- a retirement in `.github/repro-ci.json`;
- a row in `docs/reprorunner.md`;
- new counts in `test_compose_repro_matrix.py` (skipped 14→15, include-retired 32→34).

**How the window is forced, using only public API:**
- A v11 database (CompactStorage=Legacy, CHECKPOINT=0) on caller MemoryStream subclasses keeps exactly one commit in its WAL. With one commit, the checkpoint retires no frame, so it cannot promote the file to v13 itself.
- It is reopened with CompactStorage=Compact. One insert on its own thread stores a BSON document, then a second document of the same shape compactly. The first document of a shape is never compact, and a single-document insert never reached the promotion on either version.
- The second document is a `BsonDocument` subclass whose overridden indexer holds the thread when the engine reads `_id` for compact encoding. That read is after the per-document `_state.Validate()` and before `lock(_header)`, with no page reads in between.
- The checkpoint's first data sync (the caller stream's `Flush()`, which both versions call for a non-FileStream) releases the insert. It then waits until the insert is blocked on the header lock the checkpoint holds, and throws `IOException(HResult 5)`.

**Why it is deterministic:** the promotion can only start after the checkpoint releases the header lock, which happens after the failed sync. On the known bad, the promotion's own writes don't depend on the engine's concurrent teardown (the caller-stream wrappers skip no write after dispose).

**Known-bad failure text:** `REPRODUCED: a compact insert that waited on the checkpoint whose data sync failed promoted the file after that failure: it wrote a v12 header over the v11 one and synced the data file again on the handle whose sync had just failed, which reported success`. After the failed sync the journal shows the insert thread doing: data read, log sync, data write (the header at offset 0), data sync, log truncate, log sync. The header goes from v11 to v12.

**Candidate result:** `FIXED: ... refused with that failure: nothing was written to or synced on either file after the failed sync, the data header stayed v11, and a reopen has every committed row`. The refusal is thrown by `EngineState.ThrowIfStopped` inside `WriteFileVersion`, with the injected EIO as the cause. A copy of the bytes reopens with rows 1-21 and only the seed, then takes compact inserts (promoted to v12) and keeps them after another reopen.

**Determinism evidence:**
- **ReproRunner CLI (`run --ci --report`):** 11 runs against 6.0.0-prerelease.319, all passing; `regression_proof.py verify --expect-version 6.0.0-prerelease.319` passed on all 11.
- **Built binaries, 8 in parallel on 4 cores:**
  - known bad: 60 + 50 runs, 110/110 REPRODUCED;
  - candidate: 60 + 50 runs, 110/110 FIXED with no stream operation after the failure.
- **Dev commit 5dd942a73:** 5 + 5 CLI runs, all passing `verify --expect-version 0.0.0-knownbad.5dd942a7367c`, plus 30 + 40 parallel runs, all REPRODUCED. I did not run `pack-known-bad` again because it does `git worktree add` in the shared repo. I used the pack it produced earlier in this session (`scratchpad/proofs/feed`; the nuspec names commit 5dd942a7367c). The csproj pin was edited only temporarily for these runs and restored.
- **Fix removed:** with the fix's two checks taken out of `DiskService.FileVersion.cs` (restored afterwards), the candidate reproduces the defect and the proof fails.

**Other checks:**
- `regression_proof.py select --base 5dd942a73 --labels '["bug"]'` lists the new proof, and `validate` reports 15 proofs.
- `CheckpointFailureWindow_Tests` passes 13/13 at HEAD on net10.0 Debug.
- The script unit tests pass (85, `python3 -m unittest discover -q -p 'test_*.py'`).
- `check_coverage_regression`, `check_fault_points` and `check_contracts` (`--base 5dd942a73 --head HEAD`) and `check-csharp-size.py --base 5dd942a73` all exit 0.

**What did not reproduce as the fix commit describes it:**
- **No header journal of its own:** on the known bad the promotion writes no header journal of its own in this scenario. The known-bad checkpoint writes its own header journal before its first data sync, so that journal is still outstanding. The promotion therefore only syncs the log, then writes the header, syncs the data file, truncates and syncs the log. The pinned text and README say exactly this. A known-bad checkpoint has no data sync before its journal, so the guard's variant (a sync failing before any journal) cannot be reached black-box on the known bad.
- **After the promotion:** what the insert does next races the engine's teardown, so it is logged but not pinned. Its error varies between MemoryCache disposed, "page must be writable" and "Engine closed". In about 4% of runs it also appended five log frames, then truncated and synced the log.