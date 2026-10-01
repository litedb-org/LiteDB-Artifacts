# Independent final-head performance review

PASS: all 116 fresh processes and 580 measurement windows are complete and verified. No configuration, order, metadata, window, raw-rate or binary-hash mismatch was found.

All runs used .NET 10.0.11, Ubuntu 24.04.3 LTS, x64, tiered compilation disabled, five-second warmup and five one-second windows. Forty inventory files matched their hashes; all 24 reused baseline files matched the prior final7 inventory. Final and current-dev source checkouts were clean at their configured revisions, their Release/net10 DLLs matched the measured libraries, and retained build commands specify TestingEnabled=false. Runner and driver source is unchanged from e855 to 3abe.

Independent count/active-time process rates, count-weighted allocations and every round-paired ratio match all eight author paired tables. The JSON retains each process and ratio.

| Case | Final ops/s (median process) | Final B/op (median weighted process) | Final vs baseline (median paired throughput) |
| --- | ---: | ---: | --- |
| shared / handle / read / 0 | 1,890.02 | 220,811.77 | head +30.71%; reviewed +2.05% |
| shared / handle / read / 1 | 1,672.66 | 230,938.25 | head +28.92%; reviewed +0.75% |
| shared / handle / read / 10 | 1,397.43 | 295,244.31 | head +25.49%; reviewed -1.42% |
| direct / ordinary / read / 1 | 80,651.18 | 10,248.00 | dev -14.78%; parent -7.59%; reviewed -1.03% |
| direct / legacy / read / 1 | 72,073.14 | 10,128.00 | dev -22.34%; parent -15.82%; reviewed +1.47% |
| direct / ordinary / open / 1 | 11,089.42 | 29,920.00 | dev +56.96%; parent -33.85%; reviewed -1.76% |
| shared / ordinary / read / 1 | 46,267.93 | 10,616.00 | dev -11.45%; parent -9.60%; reviewed +0.40% |
| shared / legacy / read / 1 | 7,569.25 | 152,851.65 | dev -8.38%; parent -3.69%; reviewed -1.15% |

`head` = pre-reuse c8c0; `reviewed` = e855; `dev` = current upstream 023c; `parent` = 49c; `final` = 3abe.

The Shared handle improvement over pre-reuse remains clear in every paired round. Final versus reviewed throughput differences are small; these four rounds do not establish a meaningful additional improvement or regression. Shared legacy allocation increased about 128 B/op versus reviewed; its cause is not isolated. Existing ordinary/legacy slowdowns against upstream and parent remain visible and must accompany the handle gains.

These are warm read/open measurements, not new write, contention, durability or retained-memory evidence. Processes are independent repetitions; windows are not. Reported window percentiles are not pooled percentiles. Open is attach/count/dispose with a peer retained; read0 measures begin/get-collection/commit. Configured ordinary and transaction APIs have different semantics. No source was changed and this reviewer performed no timed benchmark run.
