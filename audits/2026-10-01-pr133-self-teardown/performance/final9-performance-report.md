## Final production performance

Measured source `52dd7f579`; identical library, runner and driver to final pushed `dace941d1`. Production Release/net10, TestingEnabled=false, .NET 10.0.11, Ubuntu x64/ext4, tiered compilation disabled. Four alternating fresh-process rounds, 5s warmup + five 1s measurement windows per case. All 84 processes/420 windows passed result and cold-reopen verification; no samples discarded. No task builds/tests/profiles/compression overlapped timing. Host load was uncontrolled. Rates and allocations are medians of process results; percentage differences are median round-paired ratios, not ratios of table medians. Windows are not independent repetitions. Read-only handles measure lifecycle overhead, not durable-write throughput.

| Workload | Corrected dev fad082daf ops/s | Corrected stacked parent 8a630b26b ops/s | Final ops/s | Paired vs dev | Paired vs parent |
| --- | ---: | ---: | ---: | ---: | ---: |
| direct-ordinary-read-1 | 95,216.0 | 88,854.5 | 80,331.6 | -15.61% | -8.68% |
| direct-legacy-read-1 | 94,932.3 | 85,754.0 | 72,324.3 | -23.74% | -15.59% |
| direct-ordinary-open-1 | 7,180.2 | 16,991.5 | 11,166.7 | +55.74% | -33.50% |
| shared-ordinary-read-1 | 52,002.5 | 50,226.7 | 47,115.4 | -7.86% | -6.44% |
| shared-legacy-read-1 | 8,346.2 | 7,762.9 | 7,608.3 | -9.02% | -1.79% |

| Reads per Shared handle | Pre-reuse c8c0cfab6 tx/s | Final tx/s | Paired gain | Before bytes/tx | Final bytes/tx |
| --- | ---: | ---: | ---: | ---: | ---: |
| 0 | 1,449.0 | 1,907.7 | 1.317× | 227,783 | 220,809 |
| 1 | 1,307.5 | 1,711.4 | 1.304× | 237,908 | 230,941 |
| 10 | 1,116.9 | 1,407.6 | 1.257× | 302,129 | 295,240 |

One-read Shared handles improve **30.4%** with about **2.9%** lower allocation in this campaign. The original experimental 4.69×/~155KB result is not reproduced. The comparison includes intervening correctness fixes, so it does not isolate wrapper reuse alone. Only the holder worker and child SharedEngine wrapper are reused: **storage cores close/reopen and native writer ownership is released between handles**.

## Performance follow-up and attribution limits

Investigate the confirmed ordinary/legacy regression as a focused follow-up; do not add another optimization experiment to the current correctness correction. Keep the pre-existing Shared teardown fix independently reviewable in upstream #3077 and integrated into #133 by ancestry. PR-induced safety corrections remain intrinsic feature cost.

The corrected stacked-parent benchmark baseline is a local manual merge (`8a630b26b`) of original parent `49c327cf1` and safety-corrected dev `fad082daf`. Production builds and the focused net8 Shared teardown proof pass; it has not received the full safety suite or hosted CI. It is an attribution aid, not a merge-ready release. Final measured source `52dd7f579` and pushed head `dace941d1` have identical library, runner and driver sources. Three-version reversal alternates endpoints but leaves the parent in the middle; it is not fully randomized position balancing. The shared host is uncontrolled.

The measured incremental regressions warrant profiling session admission/exit, ordinary-reader lifetime/context bookkeeping and close scheduling separately. Current code performs per-call monitor/dictionary accounting plus binding/cancellation scopes. That identifies work to profile, not measured exclusive attribution to one lock. The earlier atomic-session experiment was small/mixed; the earlier inline-close speedup failed bounded-close semantics. Neither should be promoted based on throughput alone.

Acceptance for a separate optimization: production same-host before/after runs across ordinary, legacy and handle operations, attach/dispose and representative larger transactions; allocations and tail latency; unchanged deterministic safety/abandonment/cancellation/callback/cross-process tests; independent oracle review. Cold storage reopen and native writer release remain mandatory between Shared handles. Safety qualification does not by itself approve the measured performance tradeoff.
