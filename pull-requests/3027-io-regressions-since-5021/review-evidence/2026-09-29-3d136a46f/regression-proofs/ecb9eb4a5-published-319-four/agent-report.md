All four proofs are committed as **271b1837f** in $REPO/.claude/worktrees/agent-af1c17944ad1df782 (detached, on top of 0ce00ee55, not pushed). The known bad fails and the candidate passes for each one, and every bug reproduced on `6.0.0-prerelease.319`.

**Verification**
- `regression_proof.py select --base 5dd942a73 --labels '["bug"]'` at HEAD lists 9 proofs: the existing five plus my four. `validate` passes (11 proofs in the ledger).
- For each of my four I ran `reprorunner run <id> --ci --report` and then `verify --expect-version 6.0.0-prerelease.319`. All four passed. The known-bad run reported package `6.0.0-prerelease.319` and the candidate run reported the project reference.
- Cross-check against dev-commit 5dd942a73, using the already packed `0.0.0-knownbad.5dd942a7367c`: all four reproduce with identical text.
- Guard classes at HEAD (net10.0 Debug): 30/30 pass across DumpPinnedWalSlot, StreamDatabaseDispose, LegacyReadOnlyStream, LegacyWalPageBound and ConvertedWalBesideLegacyHeader.
- `python3 -m unittest discover -q -p 'test_*.py'` in .github/scripts: 85 tests OK.
- `check_coverage_regression.py`, `check_fault_points.py` and `check_contracts.py --base 5dd942a73 --head HEAD` all exit 0.
- `scripts/check-csharp-size.py --base 5dd942a73` fails on `LiteDB.Tests/Regressions/HeaderFrame_Tests.cs` (578 lines, over the 500 limit). That file is already in the PR and I did not touch it; my Program.cs files are 147 to 191 lines.

**Per proof** (all pinned to `6.0.0-prerelease.319`, `supports: ["linux"]`)

1. **Issue_3027_DumpInTransaction** (guard: DumpPinnedWalSlot_Tests, both methods)
   - Setup: a file database with `Transaction Pages=50`, 1000 inserts in an explicit transaction, then `$dump(pageID)` (and, in a second database, `$page_list(pageID)`) for every page, an update of the 1000 rows, and the commit.
   - Known bad fails: "REPRODUCED: after $dump or $page_list read pages that an explicit transaction had safepointed into the WAL, its next safepoint failed an ENSURE (LiteException 999: only idle readable pages can be evicted), which stopped the engine and marked the data file invalid and lost the transaction". It fails at `$dump(41)` and `$page_list(39)`; the header's invalid flag is set and a reopen finds 1 document.
   - Candidate passes: all safepoints and the commit succeed, the flag is not set, and a reopen finds 1001 documents with 1000 updated.
   - **Deviation from your description:** on the known bad it is not the commit that fails. It is the transaction's next safepoint, which runs during the page reads themselves. Same ENSURE; the stack goes through WriteLogPage → MemoryCache.Invalidate.
   - **Not used:** `$page_list` with no argument hits a different error on the known bad (InvalidCastException BasePage→DataPage, from reading through the collection's write snapshot). That looks like a separate, probably older defect, so the repro only uses `$page_list(pageID)`, as the guard does.

2. **Issue_3027_StreamDatabaseDispose** (guard: StreamDatabaseDispose_Tests, all 3 methods)
   - Known bad fails: "REPRODUCED: new LiteDatabase(stream) left CHECKPOINT=1 in the database header, a second Dispose threw and Dispose with an open transaction threw". The second Dispose throws "LiteException: This engine instance already disposed."; Dispose with an open transaction throws "The current thread already contains an open transaction...".
   - Candidate passes: CHECKPOINT stays 1000 with both commits kept, the second Dispose does not throw, and Dispose rolls the open transaction back (only _id 1 remains).
   - The repro exits 0 only if all three defects occur.

3. **Issue_3027_LegacyReadOnlyStream** (guard: LegacyReadOnlyStream_Tests, 4 methods; embeds DropIndex_5_0_21.zip, `customers.db`)
   - Known bad fails: "REPRODUCED: new LiteDatabase(stream) over a read-only stream of a 5.0.21 file failed with NotSupportedException: Stream does not support writing. (with a writable log stream too, after writing 32768 bytes into it)". The first case is a FileStream opened with FileAccess.Read; the second is a read-only MemoryStream beside an empty writable log.
   - Candidate passes: both open read-only and read all 200 documents, including the CustomerId, Name and Age queries. An insert is rejected ("IOException: Cannot modify a read-only database."), the file is unchanged and the log gets 0 bytes. A writable reopen afterwards keeps all 200.

4. **Issue_3027_ForeignLegacyWal** (guards: LegacyWalPageBound_Tests, 4 methods incl. the two using ForeignWal_5_0_21.zip; ConvertedWalBesideLegacyHeader_Tests; scripts/test-5021-regression-compatibility.py)
   - Setup: `foreign-log.db` placed beside WalCrash's `crash.db`, then a default open.
   - Known bad fails: "REPRODUCED: the open replayed the 5.0.21 log of another database into this data file (40960 to 14196736 bytes, log deleted): collections [big,fresh], 0 documents".
   - Candidate passes: INVALID_DATABASE 103 "...commits the header of another database...", and neither file changes. As a control, beside its own WAL the same data file opens with 101 documents, 21 with value 7.
   - **Growth guard:** I chose this variant because its damage is bounded; the foreign log's highest page is 1732, about 14 MB. I did not use the converted-WAL variant. The repro also:
     - checks the SHA-256 of both fixtures;
     - requires the highest page to fit under the cap;
     - sets RLIMIT_FSIZE to 64 MiB with SIGXFSZ ignored, so a far write fails with EFBIG (exit 2).

**Shared files** (append-only, except the counts)
- `.github/safety/regression-proofs.json`: 4 entries appended.
- `.github/repro-ci.json`: 4 retirements appended.
- `docs/reprorunner.md`: one row inserted after the existing `Issue_3027_*` row. That row's wildcard already covers these ids.
- `.github/scripts/test_compose_repro_matrix.py`: the counts will conflict with the other agent's edit to the same lines. With both sets merged, add 3 to skipped and 6 to entries if their repros are also Linux-only.
  - skipped 7 → 11
  - entries 18 → 26
  - comment "five" → "nine"

Scratch scripts and reports are in $SCRATCH/proofs3027b; the proof run logs are `prove-all.first.log` and `prove-all.log` there.