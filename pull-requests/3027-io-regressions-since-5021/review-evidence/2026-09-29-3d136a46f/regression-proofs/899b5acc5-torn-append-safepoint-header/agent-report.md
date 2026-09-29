All three proofs are done and pass verification: the known-bad 6.0.0-prerelease.319 fails each one for the intended reason, and the candidate (source at 0ce00ee55, Release) passes each one. They are in one commit, **7f03eb293f87b3783e49396cdc7879a3668dc178**, on top of 0ce00ee55 in my worktree (`$REPO/.claude/worktrees/agent-ad1a00c61296566d3`), not pushed. The commit message ends with the two required lines.

**Verification results**
- **`select`:** `regression_proof.py select --base 5dd942a73 --labels '["bug"]'` lists the existing five plus my three. `validate` passes on 10 proofs, and so does `validate --provenance --only` for each of mine.
- **ReproRunner and `verify`:** I ran each proof through the CLI as the workflow does (`run <id> --ci --report`). `verify --expect-version 6.0.0-prerelease.319` passes for all three.
- **Cross-check against the dev commit:** all three also exit 0 against 0.0.0-knownbad.5dd942a7367c (dev commit 5dd942a73), with the same text.
- **Guard tests (net10.0 Debug, those classes only):** all pass.
  - TornWalAppend_Tests: 18
  - TornSlotRewrite_Tests: 2
  - FailedSafepointWrite_Tests: 5
  - HeaderFrame_Tests: 31
  - HeaderFrameCrash_Tests: 4
- **Scripts:** `python3 -m unittest discover` in `.github/scripts` passes 85 tests. `check_coverage_regression.py`, `check_fault_points.py` and `check_contracts.py --base 5dd942a73 --head HEAD` all pass. The fault-point warnings are about LiteDB engine source files, which this commit doesn't touch.

**1. Issue_3027_TornWalAppend** (guards: TornWalAppend_Tests ×3, TornSlotRewrite_Tests ×2)
Two scenarios, both on caller streams with TransactionPageLimit=1 and a non-IOException failure:
- (a) the log stores half of the insert's 3rd frame and throws, then SetLength fails too;
- (b) a 64 KiB BufferedStream over a log that tears the first frame it receives.
- **Known bad (exit 0):** "REPRODUCED: commits acknowledged behind a torn WAL frame were lost at recovery (torn append whose truncation failed: acknowledged [1-20, 200, 201], recovered [1-20]; frame a BufferedStream log tore later: acknowledged [1-20, 200, 201], recovered [1-20])"
- **Candidate (exit 1):** the inserts of 200 and 201 and an update are refused with "Cannot modify this database: an earlier write failed…". The engine reads rows 1-20 and does not change either stream. The recovered image holds exactly 1-20, then takes and keeps new commits.
- **Did not reproduce, so not used:** on the known-bad, a torn 1st or 2nd frame loses nothing because the next commit writes over it; I used the 3rd frame. An IOException tear doesn't reproduce either: that engine closes ("Engine closed after an I/O failure"). A complete frame with a failed truncation also recovers everything.

**2. Issue_3027_FailedSafepointWrite** (guards: FailedSafepointWrite_Tests, both tests)
TransactionPageLimit=6 and an explicit transaction inserting rows 2-4. A query over 100 large documents then forces a safepoint, and the caller log fails that frame write.
- **Known bad (exit 0):** "REPRODUCED: Commit published the transaction whose safepoint write failed; the database it left cannot be read, also after reopening: LiteException: get only index below highest index"
- **Candidate (exit 1):** a read, then Commit, both throw "Writing this transaction's pages failed, so it can only be rolled back". In a second run, an insert throws the same and Commit returns false. In both runs the data is intact (row 1, its index, 100 large documents), and a new commit survives a reopen.
- **Did not reproduce:** FindById returning a document of another collection. In this scenario the known-bad FindById(1) returned the correct row; the defect proven is Commit publishing a database that can no longer be read.

**3. Issue_3027_LostHeaderBesideWal** (guards: 6 HeaderFrame_Tests plus HeaderFrameCrash_Tests)
File-backed with CHECKPOINT=0 and Durable Commits=false: 50 rows and an index, then close. The repro checks that the data file is only its header page, and that an undamaged copy holds 50 rows. It then damages only the data file and never touches the WAL.
- **Known bad (exit 0):** "REPRODUCED: the open of an empty data file beside a WAL of committed frames created a new, empty database and discarded every frame: 0 of 50 rows, the WAL cut from 81920 to 0 bytes"
- **Candidate (exit 1):** for an emptied file, a zeroed header page and a zeroed first sector, the open restores all 50 rows and the index, and a new commit survives a reopen.
- **Did not reproduce as data loss:** the zeroed-header-page case on the known-bad. It refuses the open with "File is not a valid LiteDB database format or contains a invalid password." and keeps the WAL. The known-bad path therefore stops at the empty-file case.

**Shared files (append-only)**
- `.github/safety/regression-proofs.json` and `.github/repro-ci.json`: three entries appended at the end.
- `docs/reprorunner.md`: one row added after the existing `Issue_3027_*` row.
- `.github/scripts/test_compose_repro_matrix.py`: skipped 7→10, entries 18→24, comment "five"→"eight". These lines will conflict with the other agent's four proofs. With all seven new proofs the counts become skipped 14 and entries 32 ("twelve" Linux-only proofs), assuming all of theirs are also Linux-only.