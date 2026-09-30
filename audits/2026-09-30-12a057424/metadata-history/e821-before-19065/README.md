# PR #133 integrated safety audit

Interim candidate: `e821ae7479dcb83570031180b0c0e3f4fd167793`.
Base: `dev` at `5dd942a7367c361fadd600be4ce10aace2768b27`.
Measured/locally tested production source: `7e13e58e5f4e40642492ce768b49018e4a958c8b`.
Both library trees: `4519699ceab868a455fb448d34701a4d974f9d6f`.

**Status: additional review5365276527 raises two P1 Shared late-reader callback findings, reopening the finite gate. Independent reproduction/fix work is pending. Local tests and the84process benchmark on7e13/e821 remain interim evidence; no acceptance is claimed.** Candidate e821 changes only proof scheduling after 7e13. Source-tree equivalence carries the production measurement forward, but does not replace candidate CI.

## Identity and finite gate

candidate-provenance.json records candidate tree `ba07414b12d0cbdab078c0e59bc72646c613c8f7` and hosted merge `3f614aa69b15771ed1a63b8b8796172fae8b6648`, which has the same tree. CI run36705044676 remains evidence for this interim candidate only. Successor fixes, relevant validation and the full accepted-candidate gate are required. The historical010 base-retarget equivalence and eb01 successful qualification remain historical, not current gate evidence.

The affected contracts are durable-ack, recovery-generation, reader-snapshot, historical-input and query-meaning. The finite gate covers transaction/session ownership, Shared holder/wrapper reuse, admission/maintenance/close progress, cleanup/error isolation, scratch ownership, external writers, abandonment, settings and callback behavior, plus inherited regression/fuzz/compatibility coverage. acceptance.md and final-source/docs/transaction-handles-safety-review.md define its evidence and limits. No merge, post-merge validation execution or repository-protection change is claimed.

## Current implementation and review evidence

Only Shared holder-worker and child-wrapper reuse is integrated as a performance option. Every transaction releases native writer ownership and closes/reopens its storage core; no coherent cache survives external writers. Plain/encrypted guards prove reuse, distinct cores, independent writer progress, refreshed indexed reads and cold reopen. Excluded experiments remain separate.

The original eight enumerated review findings and subsequent deferred-fatal/disposed-child races have guarded fixes. Further reviews required exception-safe Shared cleanup, raw-close callback dependency handling, callback collection self-wait rejection, scoped Windows cancellation waits and two independently found intermediate-fix corrections. Shared parent final checkpointing is explicitly historical best-effort; pin/child cleanup errors still propagate. The ReadTransform allegation was refuted against original/current public behavior; the speculative pre-persist Commit leak was not established and its controls retain existing semantics. Full per-finding dispositions and original failures are in review-followup-eb01/ and the pinned audit document.

Exact integrated 7e13 passes268 relevant cases on each net8/net10, with zero failures/skips; all production targets build and net462/net481 test compilation passes. Independent integrated Shared review passes18 net10 cases on a clean7e13 checkout. Callback review passes51 cases/runtime on source identical to424; those cases and fixer suites overlap and must not be summed. Hosted Framework/platform execution remains required.

## Evidence map

- final-local/followup-7e13/: exact integrated268/runtime TRXs/logs, independent18 case review, production/Framework builds and policy output. Original validation-snapshot pending fields are retained as historical; current-provenance.json supersedes their review/benchmark status. Earlier final-local files concern earlier sources.
- final-source/: committed e821 registries, docs and relevant CI sources; source-index.json pins every blob. Historical supplementary patches are identified separately. Authoritative full product source remains the candidate commit.
- hosted-final/: reserved for a future accepted candidate; no current qualification claim.
- hosted-interim-e821/: CI-agent-owned e821 evidence, explicitly reopened by review5365276527.
- review-followup-e821/: original new review and pending per-finding classification.
- performance-final3/: root-owned completed interim production campaign:84 processes/420 windows, exact binaries/configurations and replay records. performance-final2/3b60 and performance/f84 remain historical and superseded.
- review-followup-eb01/local-fixes/: source-specific original eb01/d1/d2 failures, corrected e584/424/eaa sources, genuine-failure controls, strict proofs and independent review. source-index.json and case-level trx-index.json preserve attribution and overlapping scopes.
- hosted-interim-eb01/: earlier successful five-workflow qualification, with the original12 leg cutoff retained separately from later completion. It was reopened by concrete review findings. Other hosted-interim-* retain prior failures and harness corrections.
- original-review/, before-after/, interim-integrated/, adversarial-review/, deferred-fatal-fix/, bound-child-disposal-race/: original review material, actual failing-before evidence, intermediate suites and mutation controls. Synthetic mutants are oracle controls, not historical proof.
- pin-progress-correction/, native-partition-correction/, metadata-history/: explicit test-budget/discovery corrections, local harness rehearsal and earlier narratives/source snapshots.

## Measured performance and protocol

Current one-read Shared handle:1249.5→1571.8tx/s, paired1.249432×;242386→235567bytes/tx. Zero/ten-read paired gains are1.258277×/1.231320×. The historical4.69×/155KB result was not reproduced. The workload measures read-only lifecycle cost, not durable-write/fsync throughput.

Fresh current-source regressions remain: Direct ordinary reads−12.93% versus dev (−8.97% versus parent), Direct legacy−22.77%(−16.91%), Shared ordinary−8.11%(−10.18%) and Shared legacy−8.52%(−4.03%). Attach/count/dispose improves54.25% versus dev but remains33.38% below parent. See performance-final3 for full raw rates, allocations, paired distributions and retained windows. Performance acceptance is separate from safety acceptance.

Production Release/net10, TestingEnabled=false, tiering disabled; four alternating fresh-process rounds, five-second warmup plus five measured one-second windows. Every result/index/sentinel/cold-reopen verification passed. No .NET builds/tests/profiles or archive compression overlapped timing. Eleven Python matrix-policy checks ran for 0.084 seconds during timing; the full96 policy suite ran after completion in 3.018 seconds. Other host load is uncontrolled. Ratios are medians of paired process rates, not necessarily displayed median-rate quotients.

## Retained failure classifications and limits

The 010821 Windows pin-test timeout's original phase remains unknown. Its replacement deliberately separates startup and committed-write progress, keeps the pin owner alive, preserves all 20 inserts, uses held-pin negative controls and exact cold-state checks. Passing later qualification does not retroactively establish why the original timed out.

Earlier failures include caller-owned streams retained across cold reopen, obsolete process-harness reflection arguments, a raw Windows alias-probe path, the upstream test's old silent-close-error expectation, native-session budget exhaustion and incomplete case-sensitive discovery accounting. The audit retains original outputs and corrected discriminating assertions. The7e13 CI run additionally exposed three missing pinned-feed scheduling substitutions; e821 fixes their routing while retaining all 13 dedicated before/after proofs and permanent guards. That is a harness-only correction, not a product-performance change.

Raw Shared callback-close review found real premature native release on d1; corrected eaa34 case/runtime results and strict proof are retained. Intermediate failures caused by calling Commit before closing the test reader are preserved separately. Earlier d2 callback scope-handoff failure is retained alongside corrected424and independent51case results. Some wait tests ran uncommitted source later committed as47edadeff/e22281057; the initial failed draft's exact source hash is unavailable. Those provenance limits remain explicit; integrated 7e13runs provide the later committed-source check.

Fault models cover exceptions/failed I/O, forced interleavings, abrupt process death and inherited modeled persistence faults. No physical device or VM-reset power-loss campaign is claimed. Existing quarantines remain gaps; no new quarantine is introduced. The finite PR gate does not complete the global3034 roadmap.

## Publication provenance

Source manifest SHA256 values describe pre-normalization originals; publication-normalization.json records changed public copies and relocated historical paths. Original raw evidence remains retained locally. Final SHA256SUMS must be regenerated only after all contributors finish; its current contents are stale. Newly appended data still awaits the final normalization pass. Binary/input payloads must remain unchanged. Placeholder paths are replay-location aliases, not machine-specific requirements. No artifact publication or final checksum claim is made by this staging update.
