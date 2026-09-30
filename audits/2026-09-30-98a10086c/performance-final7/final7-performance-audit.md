# Final7 production comparison and arithmetic audit

Measured source: `e8559b642b34449e0843c9e74860a3eb5817d0c7`; production tree: `4c82e64ec435862987ccb0d1061dc6eadc34510f`. Reviewed baseline: `569ba13c3b3867131c8687e7884bed65131edfbf`.

All **116 processes / 580 windows** completed with exit zero and final result/index/sentinel/cold-reopen verification. No retry or measured-window discard occurred. Reuse ran 17:27:24–17:34:18 UTC; upstream ran 17:34:18–17:48:56 UTC on 2026-09-30. Four alternating fresh-process rounds per case, five-second warmup and five measured windows; .NET 10.0.11, Ubuntu 24.04.3 X64, Release, tiering disabled.

Process rates are total operations / total elapsed time. Process bytes/operation use count-weighted windows. Displayed rates and bytes are medians of process values; gains are medians of same-round final/baseline ratios. Allocation reduction is a ratio of median allocation values, not a median paired reduction.

| Shared handle reads | Pre-reuse tx/s | Reviewed 569 tx/s | Final e855 tx/s | Paired vs pre-reuse | Paired vs reviewed | Pre-reuse bytes/tx | Reviewed bytes/tx | Final bytes/tx |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 0 | 1,442.8 | 1,892.3 | 1,870.2 | +29.55% | -0.58% | 227,794 | 220,825 | 220,817 |
| 1 | 1,302.5 | 1,692.2 | 1,687.0 | +29.61% | -0.70% | 237,916 | 230,940 | 230,933 |
| 10 | 1,110.6 | 1,424.7 | 1,397.1 | +25.41% | -2.60% | 302,144 | 295,248 | 295,243 |

| Workload | Dev ops/s | Parent ops/s | Reviewed 569 ops/s | Final e855 ops/s | Paired vs dev | Paired vs parent | Paired vs reviewed |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| direct-ordinary-read-1 | 94,836.5 | 86,218.6 | 79,693.5 | 81,372.3 | -14.10% | -7.57% | +2.58% |
| direct-legacy-read-1 | 95,556.6 | 87,513.4 | 72,458.0 | 70,825.6 | -25.91% | -19.70% | -2.01% |
| direct-ordinary-open-1 | 7,114.6 | 16,863.8 | 11,113.3 | 11,085.9 | +55.56% | -34.63% | -0.39% |
| shared-ordinary-read-1 | 51,247.7 | 49,620.8 | 46,414.0 | 45,901.5 | -9.89% | -7.49% | -0.57% |
| shared-legacy-read-1 | 8,275.7 | 7,823.1 | 7,773.1 | 7,640.8 | -7.66% | -2.51% | -1.58% |

The one-read handle comparison gains 29.61% paired throughput versus pre-reuse with 2.9348% lower median allocation; it loses 0.70% paired throughput versus reviewed 569. Every final/pre-reuse handle pair improves. Against reviewed 569, all four pairs regress for ten-read handles, Direct legacy and Shared legacy; other workloads have mixed pairs. These are descriptive local measurements, not established isolated costs of individual new guards.

| Workload | Dev bytes/op | Parent bytes/op | Reviewed bytes/op | Final bytes/op |
| --- | ---: | ---: | ---: | ---: |
| direct-ordinary-read-1 | 10,112.000 | 10,264.000 | 10,248.000 | 10,248.000 |
| direct-legacy-read-1 | 9,992.000 | 10,192.000 | 10,128.000 | 10,128.000 |
| direct-ordinary-open-1 | 168,124.807 | 29,208.000 | 29,920.000 | 29,920.000 |
| shared-ordinary-read-1 | 10,464.000 | 10,544.048 | 10,616.000 | 10,616.000 |
| shared-legacy-read-1 | 151,083.539 | 152,131.636 | 152,715.638 | 152,723.641 |

The roughly eight extra bytes/operation for Shared legacy are an observed result. The source diff adds a `_mutexOwner` reference field to `SharedDataReader`, consistent with a pointer-sized increase on x64; no allocation trace or isolated object-size control was run to establish attribution. `CallbackScope` is a readonly struct and its thread-static list is reused. Broader allocation variation across campaigns remains unexplained.

All 40 binary inventory sizes/SHA256 hashes match after timing; all 32 prior files are unchanged. Each raw loaded-library hash matches its configured inventory. The measured DLL matches the clean e855 production checkout and has SHA256 `5fcd5d25206e21b40eb0632aa784651e5818b74da906907e877a7b0e36960d77`. PDB compiler options confirm Release optimization without TESTING or DEBUG. Final and reviewed runner binaries differ, but tracked runner source and correctness controls are unchanged. Independent recomputation from every raw window matches both driver summaries. Supplemental paired files cover final/head/reviewed and final/dev/parent/reviewed because the standard summarizer only computes ratios for two versions.

Host observations record PostgreSQL/compiler and other shared-host activity; the host was not controlled or claimed idle. Task-owned heavy work was paused by explicit operator agreement; raw windows alone cannot prove this isolation or establish host load as the cause of any change. All original results remain retained.

This agent executed the campaigns and separately recomputed their arithmetic; this is not a claim of a separate independent reviewer. No PR edit, artifact publication or benchmark rerun was performed. Candidate successors must retain e855 as the measured source and demonstrate production-tree equivalence rather than relabel binaries.
