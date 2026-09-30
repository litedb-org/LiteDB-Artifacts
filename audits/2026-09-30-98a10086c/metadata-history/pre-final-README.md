# PR #133 review 5368851720 — candidate e8559b642

[Source commit](https://github.com/JKamsker/LiteDB/commit/e8559b642b34449e0843c9e74860a3eb5817d0c7).

**Qualification remains incomplete.** Local affected tests pass all 1,375 selected cases on each of net8 and net10. Current CI has an unresolved macOS Intel net10 failure in the callback handoff test: four workers reported their 15-second workload deadline. Its cause is under investigation. The remaining hosted checks, 21-proof matrix, fresh benchmark analysis and complete proof capture are pending. This audit does not declare the candidate green or accepted.

A test-only handoff harness correction is being prepared and may produce a successor candidate. It preserves the four-worker, 1,000-call workload and is intended to distinguish lack of progress from total execution time. Its validation is pending, and it does not explain the original failure. The benchmark campaign measures exact `e855` production; a future matching production tree must not be used to relabel those binaries or their commit metadata.

The correction closes two Shared callback admission gaps. Ordinary writes preflight the executing handle's ownership before selecting a pin. Ordinary pinned operations and transferred mutex readers track their synchronous ownership scope so dependent handle admission refuses before waiting. Same-file peer facades and initial Query callbacks are covered. Independent leased reads, other databases, idle handoff, cancellation and exception unwinding retain their contracts. No group commit, IPC service, retained native writer ownership or persistent storage-core cache is added.

The original optimization continues to reuse only the Shared holder worker and child wrapper. Each transaction releases native ownership and closes its storage core; subsequent handles reopen it to observe external writes. This audit's new corrections do not replace the earlier wrapper/core-refresh evidence, which remains explicitly historical in the linked archive.

- `candidate-source/`: all 28 changed files versus reviewed `569ba13c3`, with Git blob and raw content identities in `source-index.json`.
- `candidate-provenance.json`: exact head/base/merge identities and the limited source-tree equivalence supporting local runs.
- `review-5368851720/`: original failures, per-run source/binary records, permanent guards, independent probes, proof-oracle corrections and all four incomplete local attempts.
- `review-dispositions.json`: finding-specific outcomes, unresolved hosted failure and missing-original limitations.
- `acceptance.json` and `acceptance.md`: current gates and explicit pending work. Planned benchmark counts are not results.

The [previous immutable `569ba13c3` audit](https://github.com/litedb-org/LiteDB-Artifacts/tree/f66fd4238d4e40c4759bdf3e5b1dbc2314d33e3a/audits/2026-09-30-569ba13c3) retains earlier qualification, measurements, unknown causes and missing-evidence limits. It remains unchanged and links the still earlier archive. Its passes are not current-candidate passes, and its measurements are not current-candidate measurements.

Local qualification used ten disjoint completed sessions per runtime. Raw full discovery, each partition's discovery and every actual TRX case match exactly: no missing, extra, duplicate, failed or skipped cases. Four earlier broad/Shared-only attempts reached the unchanged 300-second host deadline and remain failed, incomplete runs. An initial discovery parser omitted older namespaces; comparison with raw full discovery caught the gap, and the omitted Shared cases were executed before completion was claimed. See the retained correction and validation records.

Independent production probes on exact `e855` check ten admission branches per runtime with explicit result parsing and cold collection counts. Permanent tests and strict proofs add exact indexed-row and sentinel models. Framework evidence collected locally is compilation only. Earlier local fixer, combined review and retained-root selections overlap and must not be summed.

The strengthened local proof observes actual child native admission, rejects ambiguous setup timeouts and checks native exclusion before and after fixed refusals. A synthetic false-native-predicate control must fail without either success marker. Original author baseline executables were overwritten; that gap remains recorded. A new execution with four separately retained runtimes is scheduled after the benchmark window and must remain distinct from the original runs.

Raw copies intentionally retain source paths and test literals. Publication normalization and final checksums have not been performed. No artifact commit, merge, post-merge execution, physical power-cut certification or completion of the entire upstream #3034 roadmap is claimed.
