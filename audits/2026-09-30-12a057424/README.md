# PR #133 integrated safety audit

Current pushed candidate: `12a057424843d54bf10a53318433c889a7db025d`. Review5366098967 inspected prior `f95f0b0cd59d35d285ac72183589975751e35229`.
Base: `dev` at `5dd942a7367c361fadd600be4ce10aace2768b27`.
New production tree: `baf11234b39c683d2bb5b03585f87d98cc5f7072`.

**Current-source CI qualification passed:** all39 main workflow jobs across29 test legs, the Safety aggregate, all18 actual known-bad comparisons, Fuzz, index compatibility and eight Shared comparisons. Review5366098967's two production findings are fixed and independently reviewed. Exact-source production benchmarks are independently verified. This is bounded evidence for12a, not a claim that older unexplained failures have been retrospectively diagnosed or that every possible fault is covered.

## Latest findings and source-specific evidence

The P1 Shared reader-retirement dependency can make an active callback join cleanup waiting for that same core. Corrected before-tests on actual f95 fail six forced cases while two controls pass. The first setup run also had two wrong-log-suffix setup failures; it is retained separately and is not six demonstrated product failures. Production 842 fixes the cycle. A net8 intermediate control asserted instantaneous native release and raced optional checkpoint cleanup; test-only57f observes actual release within a bounded interval. Final46 cases pass per runtime, including eight new cases and38 existing cases.

The P2 leased-reader cleanup gap marks a reader disposed before its independent snapshot refuses self-close, preventing a later retry from completing cleanup. Actual f95 fails all four self-disposal cases while four normal controls pass. Production e0cd keeps closure retryable. Final53 cases pass per runtime across both snapshot construction paths, plain/encrypted variants, strong object reachability, incompatible Direct writer admission, peer writes and indexed cold-state/sentinel checks.

Independent review retains actual f95 eight-failure/four-control results and passes54 cases per runtime on checkoutb2c9e2cce plus an archived untracked test overlay. Integrated ce82 production passes675 cases on each net8/net10, with zero failures/skips, and is built for all library TFMs. net462/net481 tests compile;101 policy tests and the18-proof registry checks pass. These overlapping counts must not be summed and remain source-specific; current hosted qualification is retained separately in hosted-final/.

Two public proofs pin actual f95. Three separate attempts retain exact source revisions, loaded configuration handshakes, package/source runner binaries, full logs and hashes. All meet strict reproduced-before/fixed-after outcomes. The P1 public forced-pin setup starts timing before the write and requires write plus BeginTrans below 50ms, with at most three separately logged setup attempts; slow setup or generic timeout cannot become a regression/fixed success. Permanent guards independently cover non-pin owner-exit semantics. No semantic failure is retried away or discarded. P2 keeps the supposedly disposed objects strongly reachable while checking incompatible admission. True plain uses null and encrypted variants use a nonempty password.

## Unexplained native failure and evidence retention

The f95 native job 109884239520 failed after 847ms when reading data-rebuild.db after an awaited child kill, with a sharing violation. The original fixture was not uploaded and is unavailable. The lock owner and original cause remain unknown; no product fix or Windows reproduction is claimed.

The diagnostic/retention correction preserves the original successful execution sequence and runs additional ownership diagnostics only on failure. Post-host collection retains database/WAL and recovery-marker state. Local Linux controls intentionally fail real synthetic engine scenarios:24 crash cases with 20 markers and six graph cases, with byte-identical retained copies after host exit. Independent review repeats24-case native checks,24 retained fixtures/20 markers, six collector tests and six graph controls. These synthetic controlled-failure databases are evidence-pipeline oracles, not the missing original Windows fixture. Original and follow-up controls remain separate; counts overlap.

The separate pin-yield failure in job109884239743 now has a locally validated harness correction in12a; its original phase remains unknown. pin-yield-harness-correction/ retains root30case runs with the net10edit limitation, laterexact12a6cases/runtime, fixer8cases/runtime and9collector tests. Independent owner-death/live-child/exception/diagnostic coupling findings are corrected. Full hosted qualification passes and is retained in hosted-final/.

## Evidence map

- review-followup-f95/: original review; P1/P2 before/fixed results; intermediate setup/oracle failures; independent test overlay; and two strict proofs with three separately retained attempts and exact binaries. Case-level trx-index.json and classification.json preserve scope.
- final-local/reader-retirement-43a14/: integrated675-per-runtime TRXs/logs, exactce82 production build, Framework compilation, the original98-policy check and18-proof checks; the later101-policy result is in pin-yield-harness-correction. candidate-provenance.json records local versus remote identity explicitly.
- native-failure-evidence-correction/: original hosted failure log, explicit missing-fixture provenance, implementation/independent diagnostic controls and synthetic retained database/WAL/marker fixtures. Binary payloads are preserved unchanged.
- final-source/: committed12a snapshots of registries, audit/API docs, CI and relevant tests; source-index.json identifies each git blob. They identify the current remote12a candidate, whose complete hosted qualification is retained in hosted-final/.
- hosted-interim-f95/, hosted-interim-19065/ and other hosted-interim-*/: failed, cancelled, partial or superseded attempts and their original cutoffs/completion appendices. hosted-final/ contains the complete12a qualification.
- performance-final4/: completed84-process/420-window campaign on19065, now historical/interim. Its95f947 production tree differs from current baf11234; superseded by exact12a measurements in performance-final5/. Prior performance folders remain historical too.
- Earlier review-followup-e821/, review-followup-eb01/, callback-budget-correction/, original-review/, before-after/, adversarial-review/, and related directories retain all prior bounded findings, fixes, controls and disclosed evidence limits. Metadata history preserves superseded narratives and source snapshots.

## Retained design and finite gate

Only Shared holder-worker and child-wrapper reuse is integrated as a performance option. Every transaction releases native writer ownership and closes/reopens its storage core; no coherent page cache survives external writers. Plain/encrypted guards prove actual reuse, external writer progress, refreshed indexed state and cold reopen. Batching, IPC writer services, retained native locks/cores and unrelated lifetime optimizations remain outside scope.

The five affected contracts are durable-ack, recovery-generation, reader-snapshot, historical-input and query-meaning. The finite gate includes ownership, disposal/cancellation/maintenance progress, isolation, durable outcomes, historical compatibility, fault and cross-process scenarios, complete test discovery/runtime/platform evidence, independent review and current before/after proofs. The registries now contain18 comparisons: eight against 560, three against eb01, three against e821, two against f95 and two published-package cases. All18 current-candidate comparisons pass; local comparisons remain pinned to their actual source revisions.

Shared parent final checkpoint remains documented best-effort; pin/child errors still propagate. Previous callback, allocation and timeout corrections retain their original evidence and qualifications. The original Windows pin timeout and ARM64 allocation6056 cause remain unknown; a later successful measurement does not explain them. The overwritten firstfacade proof setup-failure raw report remains unavailable, with only its historical disposition retained. Fault models cover exceptions, forced interleavings, process death and inherited modeled persistence faults, not physical-device power removal.

No new quarantine, merge, executed post-merge validation, repository-protection/queue change or completion of the entire #3034 roadmap is claimed. Current production benchmarks are complete; the hosted CI gate passes. Performance acceptance remains separate. Exact12a results retain ordinary/legacy read regressions against dev and parent, and observed unrelated host load limits timing interpretation.

## Publication provenance

Original pre-normalization source hashes are in manifest.json; publication-normalization.json records transformed public copies and historical relocations. Public copies are normalized with original/public hash mappings and final checksums. Binary/input payloads remain unchanged. SHA256SUMS is the final public-file checksum index. Raw originals remain retained locally; missing-original limitations are explicit. The immutable artifact commit is linked in the PR after publication; retained original-evidence limits remain explicit.

Final pin-harness evidence is in pin-yield-harness-correction/. The broad30case net10 run straddled a tiny source edit; subsequent exact12a six-case runs passed both runtimes. Current library tree remainsbaf112, so675/54 earlier production coverage remains relevant. Exact12a benchmarks are complete; full current hosted qualification passes.

## Current-source performance

performance-final5/ retains all84 processes and420 measured windows, exact
runner/library binaries and configs, summaries and independently checked paired
ratios. Zero/one/ten-read Shared handles measured median paired ratios
1.294/1.229/1.130 against pre-reuse c8c0cfab6. One-read median rates were
1258.95 to1546.23tx/s and median bytes/transaction246874.69 to240686.65.
No measured window was discarded. The experimental4.69x/155KB result was not
reproduced. Ten-read paired ratios span0.963 to1.489, including one regression;
this is not a stable guaranteed speedup. Unrelated compiler/test/database work
was observed on the host while all task-owned heavy work was paused. Allocation
varies even for unchanged baseline binaries; its cause is unproven. Remaining
dev/parent regressions and limits are explicit in the PR tables.

Final hosted verification: all 39 CI jobs and 29 test legs passed on exact
`12a057424`; 128,995 result rows contain zero failures. The hosted Safety
aggregate and independent evidence check report zero errors and zero warnings.
All 29 result-artifact digests and the Safety artifact digest match GitHub.
The original f95 failures remain failed historical records, with their limits
explicit; later passing controls do not reconstruct their causes.
