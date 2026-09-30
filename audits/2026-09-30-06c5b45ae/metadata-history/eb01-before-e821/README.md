# PR #133 integrated safety audit

Interim candidate: `eb01f346eb7d8d51925a45c2f87ef40e1c3984ee`; successor production fixes pending.
Current PR base: `dev` at `5dd942a7367c361fadd600be4ce10aace2768b27`.
Original stacked baseline / performance control: `49c327cf1926fa300f9eb7477eb4bcb404f75c43`.
Measured production source: `3b60d5aea5c1da0de0512571d79551f2b0576b55`; its complete `LiteDB/` diff to the candidate is empty.

Status: additional review findings require successor production fixes. eb01 hosted evidence is interim; no final gate or completion is claimed.

## Candidate identity and finite gate

The five affected contracts are durable-ack, recovery-generation, reader-snapshot, historical-input and query-meaning. The finite gate covers transaction/session ownership, Shared holder/wrapper reuse, admission/close/maintenance progress, cleanup/error isolation, scratch ownership, external writers, abandonment and settings refresh, plus inherited full regression/fuzz/compatibility coverage. See acceptance.md and final-source/docs/transaction-handles-safety-review.md.

Historical candidate 010821392: the PR was retargeted from the stacked branch to dev at 09:10:30 UTC while its CI ran. Actual validated merge `dafa93b84a2ef4b009c7531c6aa11d7d4728f17e`, current dev merge `c75baa2d90bc149d8385fd4375ad573fb1e900b7` and the 010821392 head all have tree `4e45c4ffdca2158d539a4e8ab321f8208dd46af0`; the full merge-to-merge diff is empty. Both bases are ancestors of the head. hosted-interim-010821/candidate-tree.json records that historical equivalence. It does not qualify eb01f346e; candidate-provenance.json records the new source identity, with final hosted CI still required. Local policy was recalculated against dev and passed; it remains distinct from the hosted policy using the original event base. The same ten proofs and full-platform selection apply.

## Production changes and retained semantics

All eight enumerated review findings are fixed; the review's nine-defect summary did not enumerate a ninth. Close cancels queued Shared admission before draining actual work. Pending maintenance fences fresh operations while admitting existing dependencies. Raw close interrupts collection waiters safely. Same-caller native ownership rejects nested handle begin. Disposed children reject misuse outside abort handling, including the independently found capture/admission race. Cleanup cooperates with session close and suppresses only a peer's already-published fatal error, while real cleanup failures propagate. Writable admission reclaims stale sort scratch without exposing another reader's active spill.

Only Shared holder-worker and child-wrapper reuse is integrated as a performance optimization. Every transaction releases native writer ownership and closes/reopens its storage core; no coherent cache survives external writers. Plain/encrypted tests observe repeated identities, changing core identities, real external writer progress, refreshed indexed state and cold reopen. No group commit, IPC writer, retained native lock, persistent core, atomic SessionLifetime, single-bound-scope or inline-close experiment is included.

## Evidence map

- final-local/: 220 targeted handle/lifetime/abandonment/scratch cases on each net8/net10, production build and policy evidence. Earlier focused counts overlap and must not be summed.
- hosted-final/: empty reservation for the eventual successor candidate; final aggregate results are required.
- hosted-interim-eb01/: completed eb01 workflow replays and explicitly bounded full-CI cutoff snapshot.
- review-followup-eb01/: new independent review bodies, pending finding classifications and actual named-mutex WaitAny runtime probe.
- pin-progress-correction/: 15 local passing cases per runtime, including held-pin negative controls, exact source/candidate equivalence and the retained interim 13-case run.
- hosted-interim-010821/: failed Windows x86/net8 pin-progress qualification, all completed legs/proofs and the historical base-retarget equivalence.
- performance-final2/: interim measured3b60 production, pending rerun after successor fixes. All 84 fresh processes and 420 measured windows, exact runner/library binaries, hashes, configurations and replay instructions. One-read Shared handle: 1,301 → 1,720 tx/s, paired 1.323×; 237,904 → 231,054 bytes/tx. Direct ordinary reads remain 11.8% below dev. No samples discarded. Earlier performance/ contains superseded f84 data.
- original-review/, before-after/, interim-integrated/:supplied repros, actual known-bad comparisons and broader intermediate tests, including retained original failures.
- adversarial-review/, deferred-fatal-fix/, bound-child-disposal-race/:independent reviews, actual failing-before variants, controls that still require real errors to abort, and mutation checks. Mutants are oracle controls, not historical proof.
- hosted-interim-*/ and native-partition-correction/:superseded hosted runs and harness corrections; local Linux partition rehearsal is clearly distinguished from actual macOS execution.
- final-source/:pinned evidence registries, audit/migration documentation and relevant harness sources. Authoritative product source remains the candidate commit.

## Failure classifications

The new fatal-disposal tests initially retained caller-owned streams across native-admitted cold reopen on macOS/Windows; closing their owned streams fixed the harness without dropping assertions. Two process-harness reflection calls required the newly added cancellation argument. The upstream close-checkpoint test expected silent failure; it now requires the exact injected IOException while preserving all WAL/model/index/recovery assertions, matching the documented public error contract. Windows raw-lock probing required a normalized path; real alias rejection and released-before/after controls remain.

The macOS Intel/net10 run on `3b60d5aea` exhausted 300 seconds after 637 passes and one existing skip, with progress 19 ms before the deadline. The same head completed 642 cases on net8. The corrected native harness divides the unchanged selection into three disjoint sessions, each still limited to 300 seconds with runtime/hook guards. It also fixes case-sensitive discovery accounting: 21 lowercase-rebuild methods / 45 executed cases were absent from the old listing. The final local rehearsal completed all 642 original cases plus four repeated guards, and all 298 discovered methods reported results. The original timeout remains a failed run, not green evidence or an unchanged retry.

The 010821392 Windows x86/net8 pin test timed out waiting for a child final marker after twenty inserts. The original cause remains unknown; its log cannot distinguish startup, native admission, insert work or output delivery. The eb01f346e harness now reports completed-write progress, keeps the pin owner alive, retains all inserts and adds exact indexed cold-state assertions and plain/encrypted held-pin negative controls. It deliberately replaces the combined startup/throughput deadline with 20-second startup/per-commit limits and a strict 60-second overall cap; diagnostic markers cannot renew the progress deadline. See pin-progress-correction/. This test-only correction does not establish the original failure's cause.

## Reproduction and limits

To reproduce this interim state, checkout `eb01f346e` from JKamsker/LiteDB. Follow its docs/rules/development.md and validation.md with TestingEnabled=true for tests; use isolated Release/TestingEnabled=false builds for measurement. Run the transaction-handle/completion/abandonment/scratch filter shown in final-local logs on net8/net10. Framework/native qualification requires the actual hosted platforms. Reproduce known-bad comparisons with the candidate's docs/rules/safety-evidence.md pack-known-bad/ReproRunner commands and the pinned source/package in acceptance.md. Benchmark replay is specified in performance-final2/README.md; remap archived placeholder paths to the extracted files. Prior candidate fuzz recorded inputs/configurations and bounded campaign metadata are in hosted-interim-010821/fuzz-replay/; final candidate evidence must be added under hosted-final/.

Fault models are exceptions/failed I/O, forced interleavings, abrupt process death and inherited modeled persistence faults. No physical-device or VM-reset campaign is claimed. Existing quarantines remain gaps; no new quarantine is introduced. Performance acceptance is separate from safety acceptance. The global #3034 roadmap and repository protection/merge-queue settings are not claimed complete. No PR merge has been performed.

## Public-copy provenance

Textual machine paths are normalized to documented placeholders; measured values, outcomes, timestamps and binary/input payloads are unchanged. Original raw sources remain retained locally. manifest.json records original pre-normalization source hashes; publication-normalization.json records original/public hashes for changed files and archive members, plus historical-to-current locations for moved hosted evidence. SHA256SUMS is the authoritative checksum list for final published bytes. Native Linux rehearsal metadata emulates a CI matrix for harness testing and is not represented as macOS evidence. Generated successful fuzz *.db end-state files are omitted with exact identities/hashes recorded; replay inputs and traces are retained. Historical failure databases and compatibility fixtures are synthetic test data, not real deployments.
