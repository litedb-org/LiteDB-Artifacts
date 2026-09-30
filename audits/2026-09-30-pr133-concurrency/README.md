# PR #133 concurrency audit: additive evidence

This archive adds local and hosted evidence for the concurrency audit at tested source
`f9487f81421a378cd50e8cae9b6112b5fd3be291`. It is separate from, and does not modify,
`../2026-09-30-98a10086c`. The production baseline remains `98a10086c`.
The known ordinary Shared cross-facade callback self-wait is an unresolved product
failure, also reproduced on parent `49c327cf` and upstream dev `5dd942a7`.
No failing schedule is reclassified as passing because emergency cleanup worked.

**Decision: do not merge PR #133 yet.** The bounded audit is complete, but C12
requires a separate production correction and failing-before/passing-after evidence.
The authoritative final report is
[integration-reports/pr133-concurrency-audit.md](integration-reports/pr133-concurrency-audit.md).
Final reports are from `84b2a6e2e2fcc847daefb3ce09aeabeccd672959`; only three
report files differ from the tested `f9487f814` source. Production remains unchanged.

## Final qualification

| Qualification | Result |
| --- | --- |
| Final Linux net8.0, full actor matrix plus C06/C22 | 276 rows: 268 passed, 8 C12 failures, 0 skipped |
| Final Linux net10.0, same selection | 276 rows: 268 passed, 8 C12 failures, 0 skipped |
| Final Fuzz.Tests, net8.0 | 36 passed |
| Final net462 compilation | 0 errors; no new CLR4 execution claim |
| Size, coverage, fault-registry and contract policies | Passed; existing quarantine gaps remain |
| [Hosted run 36768732883](https://github.com/JKamsker/LiteDB/actions/runs/36768732883), exact `f9487f814` | Failed: 12 successful jobs, 2 C12 failures, 2 intentional skips |
| Hosted Ubuntu .NET 8.0.31, seed 133098 | Actor prefix: 143 completed, C12 at step 144; independent process campaign 32/32 passed |
| Hosted Windows .NET 10.0.12, seed 233098 | Actor prefix: 239 completed, C12 at step 240; independent process campaign 32/32 passed |

All 16 original failing fixture directories were copied only after their test hosts
exited; original paths remain retained. Both complete test runtime layouts and the
Fuzz.Tests layout are preserved. Local runtimes were .NET 8.0.30 and 10.0.11.
Hosted artifacts did not upload exact runnable binaries; local binaries do not
substitute for them. Hosted `workingTreeDirty: true` is retained, and no post-run
tracked-diff evidence establishes a clean hosted tree. The actor prefixes are not
completed 272-case campaigns. The configured 150-minute nightly shard was not run.

## Evidence groups and source identity

| Directory | Attribution and source | Outcome and limits |
| --- | --- | --- |
| `integration-final` | Integrator; tested source `f9487f814` | Final local TRX, exact commands, binaries, 16 failed fixtures, policy output, hosted run/job metadata and hosted artifacts. All source workers exited before capture. |
| `integration-reports` | Final reports at `84b2a6e2e` | Six final audit reports. The three reports at archive root remain historical drafts. |
| `author-explorer` | Explorer author; final author head `c645c51b0`; binary-specific identity in `summary.json` | Final strict subsets: 66 passed, 2 product failures each on net8/net10. Earlier full net8: 264 passed, 8 product failures. Earlier full net10: 263 passed, 8 product failures and 1 corrected harness false positive. Earlier harness errors remain preserved. |
| `author-lifecycle/runtime` | Lifecycle author; `cdf4a6d89` plus retained `source.patch`, committed as `0494bd320` | Exact net8/net10 runnable captures; 36/36 Fuzz unit tests, including 12 new controls. |
| `author-lifecycle/net8.0`, `net10.0` | Same strict post-flush source and binary capture | 20 boundaries/configurations plus 20 recorded-input determinism cases per runtime: 80 total process-death scenarios. Run metadata correctly retains then-dirty commit identity. |
| `independent-oracles/peer-callback-repro` | Original independent reviewer; production library at frozen baseline, parent and dev | 6 deadlock detections across three revisions; 10 other frozen-head control/recovery outcomes. Linux net8, production `TestingEnabled=false`. |
| `independent-oracles/explorer-mutations` | Original independent reviewer; retained intermediate copied source in each runner | Wrong value, missing boundary and worker starvation fail; unchanged positive control passes. This is oracle discrimination, not a production historical regression proof. |
| `independent-oracles/final-oracle-controls` | Replacement independent reviewer; final helper copies; review commit `c543133fe` | Three final helper controls pass. Exact commands and outputs are in `results.json`. |

The author's earlier full explorer and earlier lifecycle binaries were overwritten
before capture; they are unavailable. Current captured binaries are never attributed
to those historical executions. The net462 explorer capture establishes compilation,
not CLR4 execution. These author/reviewer runs are local Linux evidence. Final integrated and hosted
qualification is separately attributed under `integration-final`; it does not
retroactively replace historical author/reviewer executions.

`source/integration-source.tar.gz` is a source snapshot. `source/manifest.json`
records its commit/tree and patches reconstructing specific author and historical
revisions from that snapshot. Standalone mutation/control source copies are retained
with their exact binaries. Original artifact manifests retain their original hashes;
`manifest.json` maps every copied evidence file from source hash to public hash.
`publication-normalization.json` maps changed text/archive payloads. Root `SHA256SUMS`
is authoritative for the final published layout. Nested historical SHA256SUMS files
still describe original captures and can differ for normalized text.

## Commands and replay

Exact recorded commands are preserved where they existed: final-control `results.json`
and peer reproducer `run.py`. Runner `run.json`/`replay.json` preserve execution
parameters; original logs and build logs preserve their recorded output. Final
integrator execution commands are in `integration-final/commands.json` and
`integration-final/policy-commands.json`.
The author reports provide execution examples. Some historical shell invocations
were not retained verbatim; examples and reconstructed replay commands must not be
mistaken for an original execution transcript. Source/binary identities and outcomes
remain independently recorded. Do not run the peer `run.py` against this immutable
archive: it creates fixtures next to itself. Copy its full directory to scratch first.

Examples from a scratch copy of this complete archive (these are replay instructions,
not newly executed checks):

```bash
# Strict author test binaries; expected known-defect failures remain failures.
dotnet vstest author-explorer/binaries/tests-net8.0/LiteDB.Tests.dll \
  --TestCaseFilter:FullyQualifiedName~TransactionHandleInterleaving
# A fresh bounded process-death campaign using retained strict-flush binaries.
dotnet author-lifecycle/runtime/net8.0/LiteDB.Fuzz.dll \
  --target shared-lifecycle --seed 333098 --count 20 --determinism-check \
  --artifact-dir scratch-lifecycle --max-artifact-mb 0
# Independent final helper; supply a scratch output history path.
dotnet independent-oracles/final-oracle-controls/bin/Release/net8.0/controls.dll \
  strict-refusal scratch-refusal.history
```

Recorded Fuzz input/replay files retain seeds, hashes and original path tails.
Public text substitutes `__WORKSPACE__`, `__LOCAL_HOME__`, `__CI_WORKSPACE__` and
`__CI_HOME__`; remap these placeholders when replaying on another machine. They are
not literal runnable paths. Never rewrite retained fixtures while adjusting replay
configuration: copy the required run and binaries into a separate scratch directory.

## Publication and evidence capture

This package was prepared without committing or pushing. All selected source trees
were quiescent. Original failed fixtures remain at their original locations; archive
copies are supplemental. The integrator confirmed the final qualification directory
frozen after policy checks and committed final reports before this additive capture.
The workspace helper performed these operations (already completed):

```bash
python3 artifacts_temp/stage-pr133-concurrency-evidence.py \
  --add-tree artifacts_temp/concurrency-audit/artifacts_temp/final-qualification \
  integration-final --quiescent
python3 artifacts_temp/stage-pr133-concurrency-evidence.py \
  --add-tree artifacts_temp/concurrency-audit/docs/audits \
  integration-reports --quiescent
# After adding/editing metadata, refresh normalization, scans and all checksums:
python3 artifacts_temp/stage-pr133-concurrency-evidence.py
```

The helper refuses to overwrite existing groups. Its copy in `scripts/` documents
the staging process; the workspace copy is the entrypoint because it resolves the
workspace relative to its location. Final integration/hosted identities and outcomes
are recorded above; no earlier green CI substitutes for these failing audit results.
Verify `sha256sum --check SHA256SUMS` from this archive and check the Git diff is
additive under this directory only. The baseline archive must remain unchanged.

The scan covers staged text and source-archive members for private home paths and
common credential patterns. Exact binaries retain embedded build/debug paths; these
are counted in `scan-report.json`, not rewritten. A pattern scan is not a guarantee
that every possible secret format is detected. All fixture, DLL, PDB and other
binary payloads must match their source hashes.

The finite campaigns do not establish unrestricted linearizability, arbitrary
scheduler fairness, all callback graphs, physical aliases, interruption inside every
cleanup instruction, or device power-loss safety. Process death leaves OS caches
alive. The six final reports under `integration-reports` explain the detailed boundaries.
