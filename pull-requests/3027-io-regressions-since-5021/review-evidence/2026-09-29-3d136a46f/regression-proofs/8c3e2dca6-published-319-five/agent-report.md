## Result

I registered five proofs. Run locally the way the workflow does it, all five fail on the known-bad state for the intended reason and pass at the PR head. Commit: **79cb0a604**, one local commit on top of 4662fe49f, not pushed.

**How proofs are registered and selected.** A proof is an entry in `.github/safety/regression-proofs.json` with the repro, the known-bad state and the permanent guard. The repro is a black-box program under `LiteDB.ReproRunner/Repros/<id>/`. ReproRunner builds it twice: once against the known-bad LiteDB package pinned in its `.csproj`, once against the candidate source. The known-bad state is either a published package or a dev commit that `pack-known-bad` packs as `0.0.0-knownbad.<sha12>`.

No test from the known-bad tree ever runs, so it doesn't matter that the tests don't exist on dev. Nothing gets copied into the old tree; the repro only has to use API that both builds have. `select` picks the entries that were added or changed against the PR base, or all of them if the proving harness changed. The old head selected nothing because the PR added no entry.

**Known-bad state:** a published package, `6.0.0-prerelease.319`, not a dev commit. Its nuspec shows it was built from exactly `5dd942a7` (the PR base), and `validate --provenance` passes. The rule is to use a released package when one contains the regression, and this one does.

As a cross-check, all five also reproduce against dev packed with `pack-known-bad` (`0.0.0-knownbad.5dd942a7367c`). Against 5.0.21 none of them reproduce.

**Proofs** (known bad → candidate). Each repro exits 0 only on its defect and 1 only after checking the fixed behavior in full. `repro.json` pins both exit codes and log texts, so a build error or an unrelated failure can't count as a pass.

| Proof (permanent guard) | Known bad, 319 | Candidate |
|---|---|---|
| `Issue_3027_LegacyWalConversion` (`LegacyWalSharedMigration_Tests`, 5.0.21 compat script) | A shared open while a reader holds a lease truncates the legacy WAL from 516096 to 32768 bytes; 100 documents remain, 0 of the 21 WAL commits | LOCK_TIMEOUT with both files unchanged, then 101/21 |
| `Issue_3027_LegacyDroppedIndex` (`LegacyDroppedIndex_Tests`) | `LiteException 999: request page must be less or equals lastest page in data file` | Writes succeed and reopen with 200 documents |
| `Issue_3027_LegacyDamagedDocument` (`LegacyDamagedDocument_Tests`) | First Auto-Rebuild open: `LiteException 999: string length exceeds the document limit`; a second open drops document 2 | First open salvages documents 1, 2 (readable part) and 3 |
| `Issue_2242_UnsyncableStorage` (`UnsyncableDataFile_Tests`) | A seccomp filter makes fsync and fdatasync return EINVAL; creating a database with `Durable Commits=false` throws `FileSyncException … failed (errno 22)` | Created, indexed, checkpointed and reopened |
| `Issue_3027_SharedMutexNameLength` (`SharedMutexNameLength_Tests`) | Shared open in a Cyrillic-named directory: `ArgumentException … The length of the name exceeds the maximum limit` from `Mutex` | Opens, writes, reopens |

- **Fifth proof:** I picked SharedMutexNameLength instead of TornWalAppend. It hit the WAL repro by accident on a long path, and it's a clean black-box case on real files.
- **Fixtures:** the 5.0.21 fixtures are embedded from `LiteDB.Tests/Resources`.
- **Linux only:** all five are marked Linux-only because that is the only platform I ran them on.

**Local pipeline run:**
- `select --base 5dd942a73 --labels '["bug"]'` now emits `count=5`.
- `verify` passes for each proof.
- `pr_evidence summarize` gives `Evidence: +231 tests (−18) · 5/5 proven to fail before`.
- The 41 tests in the five guard classes pass at HEAD, none skipped or quarantined.
- `check_coverage_regression`, `check_fault_points` and `check_contracts` all pass (the fault-point warnings were already there).
- The 85 script unit tests pass.
- Reports: `$SCRATCH/proofs/reports/` and `artifacts/`.

**Other changes in the commit:**
- The five repros are retired in `.github/repro-ci.json`, like 2586 and 2614, with a row added to `docs/reprorunner.md`.
- `test_compose_repro_matrix.py` needed its counts changed (7 skipped, 18 entries).

**What the PR needs:**
- **Selection:** nothing. Pushing the commit selects all five.
- **`bug` label:** add it. That enforces the "must add a proof" rule, re-runs the workflow, and lets the labeler set `regression: proven`.
- **Safety section:** suggested line: "Regression proofs (known bad LiteDB 6.0.0-prerelease.319, published from dev 5dd942a7): Issue_3027_LegacyWalConversion, Issue_3027_LegacyDroppedIndex, Issue_3027_LegacyDamagedDocument, Issue_2242_UnsyncableStorage, Issue_3027_SharedMutexNameLength; guards in regression-proofs.json; run: <Regression proof run link>".

**Problems in the machinery that affect all PRs (#3034; I did not fix them):**
1. `test_compose_repro_matrix.py` hard-codes the real repro inventory, so every PR that adds a repro fails the scripts' unit tests until it edits those numbers.
2. The full-tier Repro Runner runs every repro that isn't retired. A repro pinned to a dev or PR commit can't restore there, and neither the scaffold nor the docs say to retire it.
3. The candidate passes on any non-zero exit, so a crashing candidate counts as a pass unless `expectedOutcomes.latest` is pinned, and the scaffold doesn't pin it. On the push to dev, GitVersion's `prerelease` label probably makes the candidate a PREDEV build. That build refuses to open files until the risk pragma is acknowledged, so repros that don't call it (2586, 2614, the scaffold template) would "pass" without testing anything. I couldn't check this outside CI. My repros acknowledge the pragma and pin both outcomes.