# Final-head PR #133 production measurements

Measured candidate **3abead6dc2eb6413daf5fa81d8faed06cdf4ade7** after upstream #3072/#3075 and the final cleanup-frame handle guard. The head is unchanged by this benchmark publication.

Production Release/net10.0, TestingEnabled=false, actual runtime .NET10.0.11, Ubuntu24.04.3 x64/ext4. Four rounds in alternating forward/reverse version order, fresh process per scenario/version/round, five seconds warmup and five one-second measurement windows, tiering disabled. Every result, indexed-field query and cold reopen/sentinel check passed. The indexed query checks its result, not an index-seek plan. No samples were discarded.

These read-only workloads measure lifecycle/read cost, not durable-write throughput. Process-wide allocations include helper threads. Each process is a repetition; five windows are not five independent repetitions. Throughput aggregates counts over active window time within each process. Displayed values are medians of process results; paired gains are medians of four round-matched ratios and need not equal the displayed quotient. Raw window percentiles are not pooled latency percentiles. No task builds/tests/profiles or artifact compression overlapped timing; metadata/text inspection continued. Shared-host load was uncontrolled, so changes are descriptive and are not isolated attribution to individual fixes.

## Shared handles

Baseline is pre-reuse PR head c8c0cfab623a22880b1d71eb966b09e89f9153b9. This is the final implementation versus that baseline, including intervening correctness fixes.

| Reads/handle | Before tx/s | Final tx/s | Paired gain | Before B/tx | Final B/tx |
| --- | ---: | ---: | ---: | ---: | ---: |
| 0 | 1,444.3 | 1,890.0 | 1.307× | 227,783 | 220,812 |
| 1 | 1,301.9 | 1,672.7 | 1.289× | 237,904 | 230,938 |
| 10 | 1,113.7 | 1,397.4 | 1.255× | 302,130 | 295,244 |

One-read handles improve 28.9% in median paired throughput and reduce median allocation by 2.9%. All four pairs improve in all three handle workloads. The experimental 4.69× / ~155KB claim is not reproduced. The implementation reuses the holder worker and child SharedEngine wrapper; each transaction still releases native writer ownership and closes/reopens the storage core. No coherent core/cache is retained across external writers.

## Ordinary and legacy APIs

Current upstream dev is 023c2b4ba8ffe637c955092ff05289d90eafdfb4, verified before building. Parent is 49c327cf1926fa300f9eb7477eb4bcb404f75c43. No handle API is emulated here: each row uses the same ordinary/legacy API on all versions. Attach means count/dispose with a peer retained, not cold startup.

| Workload | dev ops/s | Parent ops/s | Final ops/s | Paired vs dev | Paired vs parent |
| --- | ---: | ---: | ---: | ---: | ---: |
| Direct ordinary read | 95,279.0 | 88,121.6 | 80,651.2 | -14.8% | -7.6% |
| Direct legacy begin/read/commit | 94,161.7 | 86,457.3 | 72,073.1 | -22.3% | -15.8% |
| Direct attach/count/dispose | 7,080.1 | 16,794.9 | 11,089.4 | +57.0% | -33.8% |
| Shared ordinary read | 51,796.0 | 51,178.9 | 46,267.9 | -11.4% | -9.6% |
| Shared legacy begin/read/commit | 8,330.1 | 7,880.8 | 7,569.2 | -8.4% | -3.7% |

Read/legacy regressions remain. Direct ordinary allocation is 10,112/10,264/10,248 B/op (dev/parent/final), so allocation volume alone does not explain the throughput differences. The attach comparison includes different pooling/lifetime behavior; this benchmark does not isolate their costs.

## Increment since the previously measured implementation

Reviewed e8559b642b34449e0843c9e74860a3eb5817d0c7 is measured afresh in this campaign, not compared using old timing samples. Median paired throughput changes to final3a:

- `shared-handle-read-0`: +2.05%.
- `shared-handle-read-1`: +0.75%.
- `shared-handle-read-10`: -1.42%.
- `direct-ordinary-read-1`: -1.03%.
- `direct-legacy-read-1`: +1.47%.
- `direct-ordinary-open-1`: -1.76%.
- `shared-ordinary-read-1`: +0.40%.
- `shared-legacy-read-1`: -1.15%.

These small changes include host/process variation and are not proof that the merged guard has zero cost. Shared legacy allocation rises from about152,724 to152,852 B/op; the cause of the approximately128-byte difference was not isolated. Other median allocations are effectively unchanged against e855.

## Provenance and scope

The archive retains all116processes/580windows, exact40runner/library/support files, configurations, process order, build logs, actual loaded DLL hashes, source archives and independent raw-data validation. Preexisting baseline binaries also match the prior inventory. The source snapshots under `source/` are exact git archives and excluded from diagnostic path normalization. Public text normalizes local path prefixes; binaries and database payloads are unchanged. SHA256SUMS covers every published file except itself.

Final-head safety qualification remains [39 green CI jobs](https://github.com/JKamsker/LiteDB/actions/runs/36809499442) and [23 passing production proofs](https://github.com/JKamsker/LiteDB/actions/runs/36809499232). Full integration evidence and known limits remain in [the prior immutable archive](https://github.com/litedb-org/LiteDB-Artifacts/tree/3beed935c4cb54eb9d71e0303fedc827e0f364ff/audits/2026-10-01-pr133-dev3072). Benchmarks do not resolve separate upstream issue3073, existing quarantines or untested failure states. No production functionality changed in this follow-up.
