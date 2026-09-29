The regression proof for the "fsyncgate" fix (e4b9553fd) is done and committed as **853bde83f**, on top of 899b5acc5 in my worktree (detached HEAD, not pushed). The known bad fails as required and the candidate passes.

**Repro:** `LiteDB.ReproRunner/Repros/Issue_3027_FailedHeaderSync/` (csproj pinned to 6.0.0-prerelease.319, Program.cs, README.md, repro.json). It follows the existing proof conventions: exit 0 only on the defect, 1 only after the fix is checked in full, 2 otherwise, and repro.json pins both exit codes and log texts. The PREDEV pragma is acknowledged like the other proofs.

**How it simulates the bug, black-box:**
- **Device model:** the database runs on caller streams (`EngineSettings.DataStream`/`LogStream`). They are a `FileStream` subclass inside the repro that models the device. A write lands in the file at once (the OS cache) and on the device image only at a successful `Flush(true)`.
- **Same sync path on both versions:** both 5dd942a73 and the candidate route a caller `FileStream` subclass that overrides `Flush(bool)` to `stream.Flush(true)` (`NativeFileSync.OverridesFlush`). Both classify a plain `IOException` with HResult 5 (EIO) as a real failure (`IsDurableFlushUnsupported` is identical).
- **Trigger without the internal `CheckpointStage` hook:** during `db.Checkpoint()`, the first data sync whose only pending write is one 8192-byte write at offset 0 fails with EIO and forgets its pending write. That sync is the salt rotation's header write (`RotateWalSalt`). The repro requires that exactly one sync failed and that the cached header differs from the device header afterwards.
- **Scenario:** it follows the guard (`SyncFailureRecovery_Tests`). Rows 1-10 are committed, then the failing checkpoint runs. On both versions the IOException stops the engine, with no checkpoint on close, so no separate process kill was needed. The database is reopened on the same streams and rows 11-15 are committed with `durableLogFlush=true`. A power loss is modelled by opening the device images from plain FileStreams.

**Known bad (6.0.0-prerelease.319), exit 0:**
`REPRODUCED: commits acknowledged durable after the recovery of a failed header sync were lost at a power loss (acknowledged [1-15], recovered [1-10]): recovery retired the header journal behind a sync that wrote nothing, and the device kept the header from before the failed sync`

**Candidate (source at 853bde83f), exit 1:**
`FIXED: after a failed header sync, recovery wrote the header back before the sync that retired its journal: the device holds the header the engine reads, the power loss kept every acknowledged row [1-15], and the recovered database took and kept a new commit`
It checks rows 1-15 with their payloads, that the device header equals the cached one, and that a new row 16 survives a reopen.

**Verification (everything passed; nothing failed to reproduce):**
- **Selection:** `regression_proof.py select --base 5dd942a73 --labels '["bug"]'` lists `Issue_3027_FailedHeaderSync`, and `validate` passes with 15 proofs.
- **ReproRunner:** `run Issue_3027_FailedHeaderSync --ci --report` with both a Debug and a Release CLI, then `verify --expect-version 6.0.0-prerelease.319`, passes.
- **Dev commit 5dd942a73:** it also reproduces there, packed as `0.0.0-knownbad.5dd942a7367c` and restored from an isolated package cache: exit 0 with the same REPRODUCED text. The worktree isolation hook blocked `pack-known-bad`, because it runs `git worktree add`. I did its steps by hand instead: `git archive 5dd942a73`, then `dotnet pack -c Release -p:TestingEnabled=false`, then the script's own `_reversion`.
- **Targets this fix:** with only the fix's condition put back (`var rewrite = _recoveredHeader != null;`), the candidate reproduced too (acknowledged 1-15, recovered 1-10). I restored the file before committing.
- **Guard:** `SyncFailureRecovery_Tests` at HEAD on net10.0 Debug: 1 passed.
- **Scripts:** `python3 -m unittest discover -q -p 'test_*.py'` in `.github/scripts`: 85 tests OK.
- **Checks:** `check_coverage_regression`, `check_fault_points` and `check_contracts` with `--base 5dd942a73 --head HEAD` all pass. The only warnings are older ones in `LiteDB/`, none from this commit. `scripts/check-csharp-size.py --base 5dd942a73` exits 0, and Program.cs is 279 lines.

**Shared files (append-only, minimal):**
- `.github/safety/regression-proofs.json`: a new entry with guard `LiteDB.Tests/Regressions/SyncFailureRecovery_Tests.cs#Journal_retired_after_a_failed_header_sync_leaves_the_header_on_the_device`.
- `.github/repro-ci.json`: a retirement entry naming the same test.
- `docs/reprorunner.md`: one table row.
- `.github/scripts/test_compose_repro_matrix.py`: skipped 14→15, retired entries 32→34, comment "twelve"→"thirteen". Other agents' proofs will shift these counts again when merged.

Files are in $REPO/.claude/worktrees/agent-a3b31b7327f17254c:
- LiteDB.ReproRunner/Repros/Issue_3027_FailedHeaderSync/Program.cs
- LiteDB.ReproRunner/Repros/Issue_3027_FailedHeaderSync/README.md
- LiteDB.ReproRunner/Repros/Issue_3027_FailedHeaderSync/repro.json
- LiteDB.ReproRunner/Repros/Issue_3027_FailedHeaderSync/Issue_3027_FailedHeaderSync.csproj