# PR #133 integrated safety audit

Candidate: `f95f0b0cd59d35d285ac72183589975751e35229`.
Base: `dev` at `5dd942a7367c361fadd600be4ce10aace2768b27`.
Production tree: `95f947dfbda13d002c2a2f6b0d83a1e2777b4807`.

**Status: local validation and the benchmark campaign on production-identical190 are complete. Full hosted qualification on test-only successor f95 remains pending; this staging copy does not claim acceptance.** Earlier candidates, including green eb01 and failed e821, remain historical evidence.

## Candidate identity and finite gate

candidate-provenance.json records f95 head tree `03605ef756b50c7393607a5aa37280e8d6238d55`. Its production tree equals measured190; only a callback-test timeout scope, its coverage disposition and documentation changed. The190 hosted gate failed on Windows outer cleanup and remains interim. New final8 merge identity and every required workflow must qualify f95; earlier successful legs or production equivalence do not substitute for that gate.

The affected contracts are durable-ack, recovery-generation, reader-snapshot, historical-input and query-meaning. The finite gate covers explicit handles, session/reader/native ownership, Shared holder/wrapper reuse, admission/maintenance/close progress, cleanup and errors, scratch ownership, external writers, abandonment, settings and callback interactions, plus inherited recovery/fuzz/compatibility coverage. acceptance.md and the pinned safety-review document state the scenario/fault/platform obligations. No merge, executed post-merge validation or repository-protection change is claimed.

## Implementation and latest review dispositions

Only Shared holder-worker and child-wrapper reuse is integrated as a performance option. Every transaction releases native writer ownership and closes/reopens its storage core; no coherent cache survives external writers. Plain/encrypted guards prove repeated holder/wrapper identity, distinct cores, independent writer progress, refreshed indexed reads and cold reopen. Excluded batching/IPC/core-retention/lifetime experiments remain separate.

Original review defects and deferred-fatal/disposed-child variants have guarded fixes. The e821 reviews additionally confirmed late-reader close failures after Query admission ends: refused unleased-snapshot close could release native protection, foreign-thread close could hold a callback-needed connection lock, and an owning facade could wait on its own late callback. The corrected implementation preflights dependent cores before changing close state, drains without holding the callback-needed lock, preserves protection on refusal, and fences fresh opens until a draining core retires. Independent review caught the intermediate fresh-open race on 74c; its real failure and the original e821 passing control are retained. Leased/non-owning/pooled controls preserve independent lifetimes.

Shared parent final checkpointing remains explicitly historical best-effort, with retained WAL/indexed recovery checks; pin/child close errors still propagate. The guide identifies the last ordinary reader's Dispose as an idle pin-close error path. ReadTransform replacement was refuted against original/current public behavior; the hypothetical pre-persist Commit leak remains unconfirmed with explicit controls. Structural nits stay outside the correction. Full source-specific dispositions are in final-source/docs/transaction-handles-safety-review.md and review-followup-e821/.

## Local qualification and source limits

The broad selection passes **658 cases on each net8/net10**, with zero failures/skips, on source c20deb015. Its exact library tree equals 19065. The allocation harness passes **13 cases per runtime** on feaff source, also with that library tree; independent x64 .NET 10.0.12 qualification is retained. These builds are not relabeled as binaries built from 19065 commit metadata. Exact 19065 production builds cover all library TFMs; net462/net481 tests compile. All 96 policy tests and contract/fault/coverage/proof checks pass.

Independent combined review passes **58 cases per runtime** on checkout14a6df044 plus the archived ReviewLateSharedReader test overlay, with the same production tree. Fixer 53/52-case runs and a 235-case handle selection overlap; do not sum them. Case-level inventories retain methods and outcomes. Hosted CLR 4, native and ARM64 execution remains required.

Three new strict proofs pin actual e821, alongside eight originating-PR cases, three eb01 follow-ups and two package proofs: 16 registered comparisons. Local proof bundles retain exact sources and runners. SnapshotCloseRefusal uses intermediate74c production; SharedLateReaderClose uses99cd, and LateCallbackClose uses089. They are historical source-specific validation, not final190 binaries. Final8 must run all16 proofs on f95 current source.

## Evidence map

- callback-budget-correction/: test-only f95 correction, actual190 Windows failure logs, corrected18/runtime and real e8216fail6safe/runtime, plus exact test/source provenance.
- final-local/19065/: broad658 and allocation 13 TRXs/logs, exact 190 production/Framework compilation, policy output and precise source attribution. Earlier final-local subdirectories belong to earlier source states.
- final-source/: committed f95 registry, documentation, CI and relevant guard snapshots, with git blob/SHA256 identities in source-index.json. Historical supplementary patches are identified separately; full authoritative source is the candidate commit.
- hosted-final/: CI-agent-owned current f95 results, discovery/aggregate, proof/fuzz/compatibility records. Completion is still required.
- performance-final4/: root-owned completed campaign on exact 190, 84 fresh processes and420 verified measured windows, exact binaries, configurations, distributions and replay instructions. performance-final3, performance-final2 and performance/ remain superseded historical measurements.
- review-followup-e821/corrections/: real e821 and intermediate74 failures, shared/facade fixes, independent test overlay, three strict proof bundles, allocation oracles and case-level source/outcome inventories. The original review bodies remain alongside it.
- review-followup-eb01/, adversarial-review/, before-after/, original-review/, deferred-fatal-fix/, bound-child-disposal-race/: prior review/failure/proof/control evidence. Synthetic mutants are oracle controls, not historical known-bad proof.
- hosted-interim-e821/, hosted-interim-eb01/ and other hosted-interim-*/: completed or superseded hosted attempts with original failures/classifications, including original cutoffs preserved separately from later completion. Failed aggregates are not green evidence.
- pin-progress-correction/, native-partition-correction/, metadata-history/: explicit harness-budget/discovery changes and superseded narratives/source snapshots.

## Current performance

These measurements were built and executed on19065. They remain applicable to f95 through exact library-tree equivalence; no benchmark binary is relabeled as a f95 build.

Read-only Shared handles versus pre-reuse c8c0cfab6:

| Reads | Before tx/s | Current tx/s | Median paired gain | Before bytes/tx | Current bytes/tx |
| --- | ---: | ---: | ---: | ---: | ---: |
| 0 | 1,449.7 | 1,869.8 | 1.292× | 227,784 | 220,946 |
| 1 | 1,302.2 | 1,703.9 | 1.308× | 237,908 | 231,078 |
| 10 | 1,130.4 | 1,417.7 | 1.245× | 302,141 | 295,381 |

One-read throughput improves about 30.8% paired with about 2.9% less managed allocation. The historical 4.69×/~155KB proof result was not reproduced. This compares the full current implementation, including correctness fixes and required cleanup, with the pre-reuse head; it does not isolate every effect of reuse or measure durable-write/fsync throughput.

Regressions remain explicit. Paired changes versus dev/stacked-parent respectively: Direct ordinary reads−15.16%/−6.40%; Direct legacy−21.00%/−15.15%; Shared ordinary−8.03%/−8.90%; Shared legacy−8.52%/−3.41%. Attach/count/dispose improves 55.83% versus dev but remains 33.38% below parent. Performance acceptance is separate from safety acceptance.

Production Release/net10 on .NET10.0.11, TestingEnabled=false, tiering disabled; four alternating fresh-process rounds, five-second warmup plus five measured one-second windows. All 84 processes exit successfully and every result/index/sentinel/cold-reopen check passes. No measured windows are discarded. No heavy local build/test/profile/archive work overlapped this campaign; other host load is uncontrolled. Paired medians need not equal displayed median-rate quotients. Exact raw records and binary identities are authoritative.

## Failure classifications and evidence limits

The190 Windowslatest/x64/net8 plain and Windows2022/x64/net10 encrypted callback cases failed during outer cleanup because its100ms probe override remained scoped to the whole test. The fixture now limits that deadline to unsafe callback attempts and restores it before outer cleanup; safe controls retain normal deadlines. Original scheduling/drain duration remains unknown. callback-budget-correction/ retains the failed hosted output, revised real-e8216fail6safe controls and corrected18pass/runtime. This is a test-only correction, not a productionfix claim.

The original Windows pin-progress timeout's phase remains unknown. The replacement deliberately separates bounded startup and completed-write progress, preserves 20 inserts and a live owner, and adds held-pin negative controls and exact cold-state checks. Later success does not establish the original cause.

The e821 ARM64/net10 allocation case reported 6,056 bytes; its cause remains unknown. Local x64 original-class passes do not reproduce it. Measurement now runs on a dedicated worker, warms the entire boundary, preserves 100 warmup/1,000 measured publications and the exact zero-byte requirement, verifies publication/epoch state, and rejects an escaping-allocation mutation at 24,000 bytes. No retry/discard or threshold relaxation is used. This is test discrimination, not a proven explanation or ARM64 waiver. The prior190 CI collector verified its ARM64/net10 leg: strict zero and the allocation-positive control passed, along with runtime/architecture guards in all15partitions (artifact test-linux-arm64-2). The original6056-byte cause remains unknown; full successorf95 hosted qualification remains pending.

Earlier stream/reflection/path setup errors, old callback/close-error expectations, native-session budget exhaustion, missing case-insensitive discovery entries and pinned-feed scheduling omissions remain classified with original outputs. The e821 aggregate correctly rejected all callback-expectation failures plus the ARM64 allocation observation. The original raw strict-proof report for the first facade external-peer empty-password mismatch was overwritten; only its historical README/tool-output disposition remains. This archive does **not** claim to contain that unavailable raw report. Corrected proof reports and exact binaries use null for genuinely plain databases and nonempty passwords for encrypted cases. Earlier mislabeled empty-password cases prove encryption, not plain operation.

Fault models cover exceptions/failed I/O, forced interleavings, abrupt process death and inherited modeled persistence faults. No physical-device/VM-reset campaign is claimed. Existing quarantines remain gaps; no new quarantine is introduced. Completing this finite PR gate does not complete the global #3034 roadmap.

## Publication provenance

manifest.json source hashes identify original pre-normalization inputs. publication-normalization.json records altered public copies and historical relocation paths; binary/input payloads remain unchanged. This interim checkpoint contains normalized historical/local/performance evidence and matching SHA256SUMS. Current f95 hosted results are absent and remain required; final qualification and the accepted-candidate archive will be published separately. Exact local originals remain retained. Publishing this checkpoint does not claim final safety acceptance.
