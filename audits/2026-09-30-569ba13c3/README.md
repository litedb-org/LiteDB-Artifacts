# PR #133 review 5367523337 — candidate 569ba13c3

[Source commit](https://github.com/JKamsker/LiteDB/commit/569ba13c3b3867131c8687e7884bed65131edfbf).

Qualification completed: **39 CI jobs, all 29 test legs, 129,861 result rows with
zero failures, and all 19 regression proofs**. Fuzz, index compatibility and Shared
comparisons passed. The Safety aggregate and independent evidence check have zero
errors/warnings. `acceptance.json` and `hosted-completion.json` record the exact head,
base and merge-tree identity. Actual CLR 4 and native-platform runs are included.
Three pre-existing quarantined tests remain explicit gaps. Individual reports are
immutable snapshots of their stated revision and stage; the earlier pending captures
do not replace this final qualification.

The correction preserves an executing Shared handle's non-enlisting ownership
dependency across ordinary facade calls. Same-namespace native acquisition and
nested handle admission refuse before waiting. Coordinated reads, independent
work, idle handoff, cancellation, completion and rollback retain their contracts.

- `candidate-source/`: twenty committed changed files versus reviewed `12a057424`; exact
  Git path/content identities are in `candidate-source-index.json`.
- `review-5367523337/`: actual-before failures, fixes, permanent tests, independent
  probes, process-isolated strict proof, exact binaries and cold fixtures. Failed
  setup attempts and corrected source attribution remain explicit. Counts overlap.
- `performance-final6/`: final `569ba13c3` production benchmarks, raw output, exact binaries
  and arithmetic; 84 processes and 420 windows, all verified, none discarded.
- `hosted-final/`: complete exact-head hosted evidence, including verified artifact digests.
- `metadata-history/`: earlier metadata captures, kept at their original stage.

The [previous immutable `12a057424` audit](https://github.com/litedb-org/LiteDB-Artifacts/tree/ac04c2aab4f0443feeefa34aa00e3e4999a6b179/audits/2026-09-30-12a057424)
contains the earlier implementation, reviews, failed interim CI and historical
experiments. It remains unchanged. Its unknown failure causes and missing-original
limits are not explained by this correction or by a later passing test.

Local broad 705 tests per runtime were built from `99bc2c3dd`, whose production/test
and proof-source trees equal `569ba13c3`. Focused 76 and independent 63 per runtime cover
production tree `f6f315315692f9992d4a1a08e776f69662354284`. Local net462 evidence is compilation only.
The earlier strict local proof uses the first fixed production tree; a separate
`review-5367523337/strict-proof/final-569` capture now qualifies the exact final source and retains
its runtime binaries. Its verified result survives a later capture-helper cleanup
race, which is preserved separately. The outer CLI exit is not claimed, and the
known-bad package does not ship a LiteDB PDB. See per-run provenance and attempt
dispositions rather than summing overlapping counts.

## Affected invariants and discriminating evidence

| Invariant | Evidence |
| --- | --- |
| A callback cannot wait on the enclosing handle's Shared ownership | Actual reviewed `12a057424` reaches native contention; fixed input/late-reader cases refuse before waiting, in plain/encrypted files. |
| Ordinary work stays independent | A coordinated-read counter confirms the allowed path; it sees committed data and cannot see the handle's earlier uncommitted row. Other-database and Direct callbacks remain usable. |
| Refusal preserves the owner's transaction | Caught refusals retain Active state and earlier writes; commit and rollback have independent cold record/index/sentinel models. Uncaught refusals roll back and permit later ordinary work. |
| Dependencies restore before handoff and after exceptions | Nested Shared/Direct callbacks, ordinary binding suppression, late-reader handoff, and independent nested-exception probes; original/next threads can continue valid work. |
| Idle admission and cancellation retain their contracts | Tests observe the real local/native wait; cross-thread completion releases an idle handle, while cancellation of a different waiting session leaves its owner Active. |
| Writer ownership becomes available after completion | The strict proof requires separate processes to commit, then two cold indexed/sentinel reopens. Existing wrapper/core-refresh and abandonment guards remain in the broader suites. |
| Failed setup cannot masquerade as reproduction | Process startup handshake precedes the native-boundary oracle; ambiguous waits fail. A forced child stall preserves output and returns unexpected-failure exit2. |

Public text copies normalize machine-specific path prefixes, preserving outcomes,
case identities and numeric measurements. Binary payloads remain exact. Original
source hashes refer to raw inputs; `publication-normalization.json` maps modified
public copies, and final root `SHA256SUMS` covers every published file except itself, including nested
original hash inventories. Original
fixtures remained on their temporary volume; no missing device identity or lost
historical fixture is reconstructed.

These are bounded exception/concurrency, process-death, compatibility and existing
modeled-persistence tests. No physical-device/power-cut campaign, repository setting
change, merge, or completion of the whole upstream #3034 roadmap is claimed.
