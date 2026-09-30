# PR #133 integrated safety audit

Local integrated snapshot: `43a14a8cb467b5fe4027df6c31270881c4c56d79` (not pushed at capture).
Reviewed remote head: `f95f0b0cd59d35d285ac72183589975751e35229`.
Base: `dev` at `5dd942a7367c361fadd600be4ce10aace2768b27`.
New production tree: `baf11234b39c683d2bb5b03585f87d98cc5f7072`.

**The finite gate is reopened. Review 5366098967 confirmed two further production defects. Local corrections and evidence are staged; a remaining pin-yield harness correction, a new benchmark campaign and qualification of the eventual pushed candidate are pending. No current or earlier candidate is accepted by this archive.**

## Latest findings and source-specific evidence

The P1 Shared reader-retirement dependency can make an active callback join cleanup waiting for that same core. Corrected before-tests on actual f95 fail six forced cases while two controls pass. The first setup run also had two wrong-log-suffix setup failures; it is retained separately and is not six demonstrated product failures. Production 842 fixes the cycle. A net8 intermediate control asserted instantaneous native release and raced optional checkpoint cleanup; test-only57f observes actual release within a bounded interval. Final46 cases pass per runtime, including eight new cases and38 existing cases.

The P2 leased-reader cleanup gap marks a reader disposed before its independent snapshot refuses self-close, preventing a later retry from completing cleanup. Actual f95 fails all four self-disposal cases while four normal controls pass. Production e0cd keeps closure retryable. Final53 cases pass per runtime across both snapshot construction paths, plain/encrypted variants, strong object reachability, incompatible Direct writer admission, peer writes and indexed cold-state/sentinel checks.

Independent review retains actual f95 eight-failure/four-control results and passes54 cases per runtime on checkoutb2c9e2cce plus an archived untracked test overlay. Integrated ce82 production passes675 cases on each net8/net10, with zero failures/skips, and is built for all library TFMs. net462/net481 tests compile;98 policy tests and the18-proof registry checks pass. These overlapping counts must not be summed and do not qualify a future hosted candidate.

Two public proofs pin actual f95. Three separate attempts retain exact source revisions, loaded configuration handshakes, package/source runner binaries, full logs and hashes. All meet strict reproduced-before/fixed-after outcomes. The P1 public forced-pin setup starts timing before the write and requires write plus BeginTrans below 50ms, with at most three separately logged setup attempts; slow setup or generic timeout cannot become a regression/fixed success. Permanent guards independently cover non-pin owner-exit semantics. No semantic failure is retried away or discarded. P2 keeps the supposedly disposed objects strongly reachable while checking incompatible admission. True plain uses null and encrypted variants use a nonempty password.

## Unexplained native failure and evidence retention

The f95 native job 109884239520 failed after 847ms when reading data-rebuild.db after an awaited child kill, with a sharing violation. The original fixture was not uploaded and is unavailable. The lock owner and original cause remain unknown; no product fix or Windows reproduction is claimed.

The diagnostic/retention correction preserves the original successful execution sequence and runs additional ownership diagnostics only on failure. Post-host collection retains database/WAL and recovery-marker state. Local Linux controls intentionally fail real synthetic engine scenarios:24 crash cases with 20 markers and six graph cases, with byte-identical retained copies after host exit. Independent review repeats24-case native checks,24 retained fixtures/20 markers, six collector tests and six graph controls. These synthetic controlled-failure databases are evidence-pipeline oracles, not the missing original Windows fixture. Original and follow-up controls remain separate; counts overlap.

The separate pin-yield failure in job109884239743 has a correction in progress. This staging task does not invent its outcome or include an unvalidated correction. All hosted evidence paths remain owned by the CI collector.

## Evidence map

- review-followup-f95/: original review; P1/P2 before/fixed results; intermediate setup/oracle failures; independent test overlay; and two strict proofs with three separately retained attempts and exact binaries. Case-level trx-index.json and classification.json preserve scope.
- final-local/reader-retirement-43a14/: integrated675-per-runtime TRXs/logs, exactce82 production build, Framework compilation, policy 98 and18-proof checks. candidate-provenance.json records local versus remote identity explicitly.
- native-failure-evidence-correction/: original hosted failure log, explicit missing-fixture provenance, implementation/independent diagnostic controls and synthetic retained database/WAL/marker fixtures. Binary payloads are preserved unchanged.
- final-source/: committed local43 snapshots of registries, audit/API docs, CI and relevant tests; source-index.json identifies each git blob. These are not labeled as the remote f95 or eventual accepted source.
- hosted-interim-f95/, hosted-interim-19065/ and other hosted-interim-*/: failed, cancelled, partial or superseded attempts and their original cutoffs/completion appendices. hosted-final remains reserved for the eventual qualified candidate.
- performance-final4/: completed84-process/420-window campaign on19065, now historical/interim. Its95f947 production tree differs from current baf11234; new measurements are required. Prior performance folders remain historical too.
- Earlier review-followup-e821/, review-followup-eb01/, callback-budget-correction/, original-review/, before-after/, adversarial-review/, and related directories retain all prior bounded findings, fixes, controls and disclosed evidence limits. Metadata history preserves superseded narratives and source snapshots.

## Retained design and finite gate

Only Shared holder-worker and child-wrapper reuse is integrated as a performance option. Every transaction releases native writer ownership and closes/reopens its storage core; no coherent page cache survives external writers. Plain/encrypted guards prove actual reuse, external writer progress, refreshed indexed state and cold reopen. Batching, IPC writer services, retained native locks/cores and unrelated lifetime optimizations remain outside scope.

The five affected contracts are durable-ack, recovery-generation, reader-snapshot, historical-input and query-meaning. The finite gate includes ownership, disposal/cancellation/maintenance progress, isolation, durable outcomes, historical compatibility, fault and cross-process scenarios, complete test discovery/runtime/platform evidence, independent review and current before/after proofs. The registries now contain18 comparisons: eight against 560, three against eb01, three against e821, two against f95 and two published-package cases. Current candidate execution is still required; local comparisons are pinned to their actual source revisions.

Shared parent final checkpoint remains documented best-effort; pin/child errors still propagate. Previous callback, allocation and timeout corrections retain their original evidence and qualifications. The original Windows pin timeout and ARM64 allocation6056 cause remain unknown; a later successful measurement does not explain them. The overwritten firstfacade proof setup-failure raw report remains unavailable, with only its historical disposition retained. Fault models cover exceptions, forced interleavings, process death and inherited modeled persistence faults, not physical-device power removal.

No new quarantine, merge, executed post-merge validation, repository-protection/queue change or completion of the entire #3034 roadmap is claimed. Current production lacks a completed benchmark and hosted gate. Performance acceptance remains separate; the old 190 ordinary/legacy read regressions remain historical observations, not current-source measurements.

## Publication provenance

Original pre-normalization source hashes are in manifest.json; publication-normalization.json records transformed public copies and historical relocations. Newly appended sources and synthetic fixtures still await coordinated normalization and final checksums. Binary/input payloads remain unchanged. SHA256SUMS is stale. Raw originals remain retained locally; missing-original limitations are explicit. No publication or final acceptance is claimed.
