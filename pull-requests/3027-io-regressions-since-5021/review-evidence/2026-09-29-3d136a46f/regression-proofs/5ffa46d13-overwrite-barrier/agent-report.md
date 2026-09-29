The proof for PR #3027's overwrite barrier is committed as **`6858313b5`** ("Prove the overwrite barrier on a log that cannot sync"), in worktree `$REPO/.claude/worktrees/agent-ae670113747d5f100`, not pushed. The published 6.0.0-prerelease.319 fails as required, and the candidate passes. It also reproduced first time, so no fallback variant was needed.

**Repro:** `Issue_3027_OverwriteBehindUnsyncedLog`
- The database runs on two caller streams (`EngineSettings.DataStream`/`LogStream`). Each is a `FileStream` subclass (`DeviceFile.cs`) that keeps a device image, updated only by a successful `Flush(true)`.
- Both 319 and the candidate send a caller `FileStream` subclass's device sync to `Flush(true)` (`NativeFileSync.OverridesFlush`). Both treat `IOException(HResult 22)` as "cannot sync" (`IsDurableFlushUnsupported` is the same in both). So the data file syncs and the log answers EINVAL.
- The scenario: rows 1-10 are committed and synced. Then, with `durable commits=false` and the log refusing syncs, rows 11-30 are inserted and a checkpoint runs.
- If the checkpoint writes to the data file, the repro captures the images at its data sync. The data file gets each prefix of the pending writes, the last one whole or torn in half. The log is left as of its last successful sync.
- Everything else follows the existing proofs:
  - same csproj pin and PREDEV pragma call;
  - exit 0/1/2 rules, with both outcomes and log texts pinned in `repro.json`;
  - an entry in `regression-proofs.json` and a retirement in `repro-ci.json`, both append-only;
  - a row in `docs/reprorunner.md`;
  - matrix counts moved from 14 to 15 skipped and 32 to 34 entries ("thirteen" Linux-only proofs).

If other agents' commits also bump those counts, the numbers will need merging.

**Known-bad failure text** (319 and dev-commit 5dd942a7 give the same text):
`REPRODUCED: with durable commits=false on a log that cannot sync (EINVAL), a checkpoint (11 pages) overwrote the data file in place behind a header journal and WAL only in the OS cache: 6 of its 11 writes overwrote bytes the device held, and 19 of the 22 images a power loss mid-overwrite leaves do not hold rows 1-10 intact (first: after write 2 of 11 torn (8192 bytes at 16384): PageChecksumException: Checksum mismatch in Data file at position 16384.)`
- The log refused 2 syncs, and 116 log writes (journal and WAL) were pending when the overwrite happened.
- The other damaged images fail with "get only index below highest index" or `EndOfStreamException`.
- Three images reopen intact: write 1 (the header page, whole or torn) and write 11 whole.

**Candidate result** (exit 1):
`FIXED: with durable commits=false on a log that cannot sync (EINVAL), the checkpoint wrote nothing to the data file and kept the WAL: a power loss keeps rows 1-10 of the data file intact (rows 11-30 of the unsynced WAL are lost, a process crash keeps them), and once the log syncs a checkpoint drains the WAL`
- The checkpoint returned 0 after 1 refused log sync, with no data-file write at all, including at close.
- The data file is byte for byte unchanged, both on the device and in the cache, and the WAL is unchanged.
- The live engine reads rows 1-30 through a full scan and the index; `$database.writeFailure` is null and `readOnly` is false.

**Verification**
- `select --base 5dd942a73 --labels '["bug"]'` lists the proof; `select --base 899b5acc5` lists only it.
- `reprorunner run Issue_3027_OverwriteBehindUnsyncedLog --ci --report` met both outcomes, then `verify --expect-version 6.0.0-prerelease.319` passed.
- Against dev-commit 5dd942a73, the repro exits 0 with the identical text. I reused the existing `pack-known-bad` feed at `scratchpad/proofs/feed` (`0.0.0-knownbad.5dd942a7367c`, nuspec commit 5dd942a7) instead of re-packing.
- As an extra check, I disabled only the fix's refusal in the candidate (`RequireLogSynced` returning early when `!_durableCommits`). The repro then reproduced with the same text. I restored the source before committing.
- `OverwriteBarrier_Tests` and `Issue2242_UnsyncableLog_Tests` at HEAD (net10.0 Debug): 12 passed, 0 failed.
- `.github/scripts` unittest discover: 85 tests OK.
- `check_coverage_regression`, `check_fault_points` and `check_contracts --base 5dd942a73 --head HEAD` all passed. The fault-point warnings are the same 15 as at 899b5acc5, none from this commit.
- `check-csharp-size --base 5dd942a73` passed. I split `DeviceFile.cs` out so both files stay under 300 lines (Program.cs 272, DeviceFile.cs 127).

**Permanent guards named:** the four `OverwriteBarrier_Tests` methods, plus `Issue2242_UnsyncableLog_Tests#Compact_promotion_on_a_log_that_never_syncs_falls_back_to_bson_without_durable_commits`.

**Not covered by the repro:** it proves the checkpoint only. The format-promotion and conversion forms of the same bug are left to the guard tests.

Scratch files, including the proof report `proof.json`, are in `$SCRATCH/ob3027/`.