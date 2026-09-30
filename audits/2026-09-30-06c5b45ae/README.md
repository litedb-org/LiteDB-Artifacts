# PR #133 integrated safety review

Candidate: `06c5b45ae2de79960edd06812534ef870254a347`.
Baseline: stacked parent `49c327cf1926fa300f9eb7477eb4bcb404f75c43` and merged upstream dev `5dd942a7367c361fadd600be4ce10aace2768b27`.
Known-bad transaction-handle revision: `560529066aeda64c24d5cdd32d34a8aca12695ae`.
Production benchmark build: `f84b35d9890955029e406e9fe3267ec0e3940215`; subsequent candidate changes touch tests, documentation and evidence metadata only.

The PR and `docs/transaction-handles-safety-review.md` describe the finite gate, affected contracts and fixes. This archive preserves original failures and their disposition, intermediate checks, independent review and final candidate evidence. Earlier passes are not substituted for the final candidate. Source mappings, hashes and provenance limitations are in `manifest.json`.

- `original-review/`: supplied review and reproduction sources.
- `before-after/`: initial comparisons against actual known-bad source; current hosted proof reports supersede them for final qualification.
- `interim-integrated/`: broad local suites and original reflection-argument harness failures; corrected affected paths rerun without weakened assertions.
- `adversarial-review/`: deferred-fatal counterexample and independent reviewers. Mutation runs validate the guards; they are not historical regression proofs.
- `deferred-fatal-fix/`: actual failing-before tests, expanded strict proof and focused net8/net10 validation.
- `final-local/`: 194 handle/lifetime/scratch cases per runtime, followed by 22 disposal controls after six added cases. These overlap; do not add the counts as distinct tests.
- `hosted-interim-2179/`: superseded candidate platform harness failures. Caller-owned streams crossed native-admitted cold reopen; the final tests close them before verification.
- `performance/`: production identities, every fresh-process window and paired results. No local builds/tests/profiles overlapped timing. Host load is otherwise uncontrolled.

Fault models: exception/failed I/O, forced interleavings, process death, and inherited modeled persistence coverage. No actual power-cut/device campaign is claimed. Existing quarantines remain visible gaps. No new storage format or WAL publication protocol is introduced. Only holder worker/wrapper reuse is integrated; no core/cache or native writer ownership survives between transactions.

Final hosted and upstream performance results are added before publication. No completion claim is made by this staging text.

The `1eaeaf092` full suite found an upstream test expecting silent close-checkpoint
failure. Candidate `06c5b45ae` requires the original IOException and retains all WAL,
committed-model, index and cold-recovery assertions; eight checkpoint cases pass
on each runtime. This is an intentional exception-contract correction in the test,
not a product change. The independent review agrees. All production sources remain
identical to the measured build. Final hosted qualification is pending separately.
