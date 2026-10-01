# PR #3077 isolated performance cost

Comparison: upstream dev immediately before this fix (`023c2b4ba8ffe637c955092ff05289d90eafdfb4`) against `fad082daf2283f6844fcef2af379a3e0d5c02d59`, whose production tree is identical to PR head `9e6111802619d30f5aa38c3fedd039912306aebf`. This isolates #3077, unlike earlier comparisons between PR #133 and corrected baselines.

Production Release/net10, TestingEnabled=false; .NET 10.0.11, Ubuntu 24.04.3 x64/ext4, tiering disabled. Four alternating fresh-process pairs per workload; 5s warmup then five 1s windows. All 32 processes/160 windows passed result/cold-reopen verification, stderr was empty and loaded library identities matched. Both complete retained binary layouts matched their previously audited inventories before running and all 16 files remained unchanged after running. No builds/tests/profiles/archive compression overlapped timing. Shared-host load is uncontrolled; four pairs do not establish statistical equivalence or a universal bound.

| Workload | Before ops/s | After ops/s | Median paired throughput change | Paired range | Before bytes/op | After bytes/op |
| --- | ---: | ---: | ---: | --- | ---: | ---: |
| shared-ordinary-read-1 | 51,011.9 | 52,014.6 | +1.97% | -0.10% to +3.77% | 10,528.00 | 10,528.00 |
| shared-legacy-read-1 | 8,314.7 | 8,293.0 | -0.26% | -4.15% to +4.32% | 151,275.54 | 151,275.54 |
| shared-ordinary-open-1 | 1,401.2 | 1,409.7 | +0.61% | -8.44% to +4.50% | 174,434.55 | 174,440.24 |
| direct-ordinary-read-1 | 95,769.2 | 94,326.3 | -1.23% | -2.62% to +1.50% | 10,112.00 | 10,112.00 |

No substantial throughput penalty was observed in these workloads. The positive medians are not evidence of a proven speedup: paired ranges straddle zero and the unchanged Direct execution path also varied. Shared point-read and read-only legacy-transaction allocation medians are unchanged; attach/count/dispose varied by about 6 bytes/op (under 0.004%). These are single-thread read-only transactions and attach/count/dispose with a retained peer, not cold startup, durable-write throughput, contention, or callback-heavy retained-core teardown. A broader zero-cost claim is not established. The substantial Direct-mode PR #133 regression cannot be attributed to this Shared-only source change.

Rates/allocations are medians of process results. Throughput percentages are medians of four round-matched ratios; they need not equal the quotient of displayed median rates. Windows are not independent repetitions. No samples discarded.

`pr3077-incremental-results` retains every raw window and invocation. `source/runner` and the driver scripts allow replay after changing config runner paths to the retained `binaries` directories. Exact upstream source is pinned by Git commit above; the corrected source archive and complete safety evidence are in [the preceding immutable audit](https://github.com/litedb-org/LiteDB-Artifacts/tree/a3d56e9bca2c02a888469f1b0dd48fff198071d4/audits/2026-10-01-pr133-self-teardown).

Publication normalizes local diagnostic paths; binaries/support files and runner source remain byte-for-byte intact. Root SHA256SUMS covers the final public copies, and the normalization report maps changed diagnostic hashes. No production code or tests changed for this measurement.
