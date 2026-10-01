# PR #133: upstream dev integration and final callback guard

Candidate: **`3abead6dc2eb6413daf5fa81d8faed06cdf4ade7`** in
[JKamsker/LiteDB PR #133](https://github.com/JKamsker/LiteDB/pull/133).
Upstream dev `023c2b4ba8ffe637c955092ff05289d90eafdfb4` (#3072 and #3075),
and audit `d3b374f2ba6b974cbbb647f53e6e9569cb24d15f`, are ancestors.
Neither upstream fix was duplicated by cherry-pick.

## Result and remaining limits

Upstream's executing-callback frames resolve the original audit's C12 failures.
A subsequent integration check found that handle begin missed cleanup frames.
The final correction consults those existing frames before handle admission.
A handle's separate child cannot use ordinary same-facade mutex recursion.
No storage core/cache, native writer ownership, or commit batching is retained.

The real pushed `be60d53a3` blocks in a cleanup callback's native acquisition.
The exact final twenty-case test on that unmodified production revision gives
**ten same-file timeouts and ten other-file successes**. The final implementation
passes all twenty. Five cleanup routes run plain/encrypted with observed callback,
zero admission observations on refusal, later progress, and two cold indexed
state/sentinel checks. The production proof independently witnesses native
ownership and waiter state with default-infinite admission; cancellation is used
only to drain known-bad cases, never accepted as fixed behavior.

Upstream #3073 (idle same-thread legacy ownership) remains separate. Existing
quarantine and unmodeled device/failure boundaries remain; this is finite evidence,
not a claim that every possible interleaving or hardware failure was explored.
Performance was not remeasured after this integration.

## Final Darwin fixture follow-up

Production code is byte-identical to 0d7ea1ee6 (tree
`b4f0e002314b0bb11a8cd01ab37a679881730008`). Its macOS Intel run revealed a
second fixture conflict after canonicalization: ordinary FileStream(path) adds a
Darwin whole-file flock. Eight same-file guards passed, then later writes failed;
four last-reader cases failed setup admission. The final fixtures and production
proof use the same raw-descriptor opening as existing AdmittedFileStream only on
Darwin. Every assertion and timeout remains intact; other OS constructors are
unchanged. Earlier diagnosis of namespace normalization as sufficient was wrong.

The 0d7 hosted directories remained on the runner but were not uploaded. Those
file contents are unavailable; logs remain. The final fixture publishes typed
manifests to the existing post-host collector. Eleven collector tests pass,
including live-host and invalid-path refusals. Running the final tests on actual
be60 produced ten failing cases and ten controls; all ten C# failure manifests
were collected after host exit, preserving twenty raw database files. See
`darwin-fixture/retained-manifests/`.

The sixty affected tests pass on both runtimes after the last fixture change;
net462 compiles. Exact executed runtimes and reports are in `darwin-fixture/`.
The new production proof is green locally. Actual final-head macOS Intel native-admission jobs 110201425352 (.NET8) and
110201425339 (.NET10) both pass; their TRX reports confirm all twenty new cases
on each. All three full macOS runtime legs also pass their ten upstream and twenty handle-close cases. All 23 regression proofs, Fuzz, index migration and eight Shared production comparisons pass. The wider matrix is recorded in qualification.json.

## Local evidence

| Check | Result | Evidence |
| --- | --- | --- |
| Full audit selection, .NET 8 | 316 passed | `close-frame-qualified/full-audit-net8.0.trx` |
| Ownership selection, .NET 8 | 869 passed | `close-frame-qualified/ownership-net8.0.trx` |
| Full audit selection, .NET 10 | 316 passed | `close-frame-qualified/full-audit-net10.0.trx` |
| Ownership selection, .NET 10 | 869 passed | `close-frame-final/ownership-net10.0.trx` |
| Actual final tests on be60 | 10 same-file failures / 10 controls pass | `close-frame-before-final/` |
| Temp-directory alias close tests | 30 passed | `local/close-frame-alias-net8.trx` |
| net462 | Compilation passed; not executed locally | `local/close-frame-net462-build.log` |
| Production before/after proof | Package reproduces; candidate fixed | `close-frame-proof/runner-report.json` |

The 316-case audit selection contains all 272 actor schedules plus 40 upstream
peer-callback cases and 4 focused cycles. Ownership selections are disjoint.
`close-frame-final/full-audit-*` are earlier 112-case default-subset runs, not the
full matrix; `close-frame-qualified/commands.json` records the full-matrix env.
The unchanged 300-second session limit is used throughout. Executed test runtimes
are in `close-frame-runtime/`; their source tree matches committed 0d7ea1ee6,
although some assemblies were built before commit creation.

`close-frame-proof/` retains exact production binaries, actual original-volume
fixtures copied after workers/processes exited, build/run metadata, and the
independent review's original discovery. The normal ReproRunner retires its build
layouts, so separately executed package/source layouts are also preserved.
No test-hook production assemblies were used for that proof.

## Earlier states and failed runs

The initial merged 7f2b2bf16/be60 tree passed 1,165 local tests per runtime but missed
the later-discovered close-frame gap. Its artifacts remain historical: folders
`final-runtime/` and `final-hosted-concurrency/` refer to that earlier candidate,
not 0d7ea1ee6. `campaign/` contains its 80 local process scenarios/replays and 36
oracle tests; final hosted campaigns are separately identified in qualification.
`mutation-capture/` is an intentional missing-ordinary-guard oracle mutation,
not a substitute for the actual known-bad proofs.

Initial four upstream close-fixture failures were caused by holding a FOR UPDATE
cursor on the same collection as an independent ordinary transaction. The cursor
now retains native ownership on the sentinel collection. Two early other-file
controls wrote the wrong database and skipped the intended checkpoint; originals
are retained in `close-frame-fixture-failures/`. Upstream #3075 supplies the
observed-release/small-update setup for the final last-reader case.

The be60 macOS jobs failed admission because custom streams kept `/var` while
filename-backed peers used `/private/var`. The final fixture canonicalizes the
same physical directory without relocating it or relaxing assertions. See logs
and the independent bounded review in `local/`. The earliest ten-case discovery
predates added failure retention, so its database fixtures are unavailable;
the exact final before-test run retains all ten failed fixtures instead.

## Hosted status and preservation

`qualification.json` records the final candidate and exact hosted run IDs/status.
Final full CI completed successfully: all 39 jobs, with 29 safety evidence legs and no evidence errors or warnings. Independent parsing of 338 TRX files finds 124,282 passed rows and 69 NotExecuted rows for the three existing quarantines. The separate Framework XML records 5,182 passed and five skipped (the same three quarantines plus two target-specific exclusions); counts are not summed across formats. The retained Windows x86 timeout artifact is the successful intentional diagnostic-capture smoke test, not a product timeout. See `final-full-ci/` and `final-safety/`.

The scoped integration findings are resolved with final-head regression evidence. No additional production correction is indicated by this audit for these paths. This does not resolve the separately tracked #3073, the existing quarantines, historical unknown failures, or untested states described above. Exact unnormalized git source archives are in `source/` (see `source/identity.json`); prior snapshots
and already-published audit archives remain unchanged.

Public diagnostic text copies normalize machine-specific path prefixes only; git source archives are excluded to preserve code and test literals. This does not
move the exercised databases. `publication-normalization.json` maps original and
published hashes; binaries and database payloads remain byte-for-byte unchanged.
`original-capture.json` and `close-frame-original-binary-hashes.json` preserve
original binary/fixture digests. Root `SHA256SUMS` covers all published files,
including nested historical manifests, except itself.
