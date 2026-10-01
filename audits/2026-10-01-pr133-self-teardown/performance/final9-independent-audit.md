# Independent corrected-baseline benchmark audit

**PASS**: 84 processes and 420 windows, all complete with exit zero and final cold verification. No raw-data, configuration, process-order, summary, stderr or binary-identity mismatch found.

All 32 distinct binary/runtime files match before/after manifests and current hashes. Isolated final, corrected-dev and corrected-parent source/build identities match the measured libraries. The reused pre-reuse baseline matches its preceding inventory. Library and harness source at measured 52dd7f579 is identical to pushed dace941d1.

Runtime/platform: .NET 10.0.11, Ubuntu 24.04.3 LTS, x64. Tiering disabled; 5-second warmup, five one-second windows and four fresh processes per case/version.

| Case | Final median ops/s | Final median weighted B/op | Median paired final/baseline gain |
| --- | ---: | ---: | --- |
| direct-ordinary-read-1 | 80,331.55 | 10,248.00 | dev -15.61%; parent -8.68% |
| direct-legacy-read-1 | 72,324.34 | 10,128.00 | dev -23.74%; parent -15.59% |
| direct-ordinary-open-1 | 11,166.71 | 29,920.00 | dev +55.74%; parent -33.50% |
| shared-ordinary-read-1 | 47,115.43 | 10,616.00 | dev -7.86%; parent -6.44% |
| shared-legacy-read-1 | 7,608.32 | 152,851.64 | dev -9.02%; parent -1.79% |
| shared-handle-read-0 | 1,907.67 | 220,808.66 | head +31.71% |
| shared-handle-read-1 | 1,711.39 | 230,940.63 | head +30.38% |
| shared-handle-read-10 | 1,407.57 | 295,240.15 | head +25.67% |

`dev` is corrected safety branch fad082daf; `parent` is local corrected stacked parent 8a630b26b; `head` is pre-reuse c8c0cfab. The JSON retains every independently recomputed per-process metric and every paired ratio.

## Qualification limits

- Corrected stacked parent 8a630b is a local manually merged benchmark baseline 49c + fad, not original parent or CI-qualified release.
- Corrected dev fad is a safety-branch revision; do not label as an unchanged upstream release.
- Parent focused production net8 teardown proof is retained evidence; this review verified source/proof-binary identity, did not rerun proof, and it does not establish full safety/net10 qualification.
- Read/open steady state only. No new write/contention/durability or retained-memory claims.
- Four fresh processes per version/case are replicates; five windows per process are not independent replicates.
- Three-version forward/reverse order balances endpoints; parent remains middle.
- Differences include all source/merge changes, so neither baseline comparison isolates individual handle/guard cost.
- Retained build configuration declares production Release TestingEnabled=false; no build or test execution by independent reviewer.
