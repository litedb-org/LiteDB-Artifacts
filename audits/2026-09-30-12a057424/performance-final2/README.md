# Interim production comparisons (measured 3b60)

Status: historical/interim evidence. New review follow-up requires successor production changes and a fresh benchmark campaign; these results are not final acceptance numbers. Raw measurements and binaries are retained unchanged.

Measured source: `3b60d5aea5c1da0de0512571d79551f2b0576b55`. Production Release/net10.0, TestingEnabled=false. Compare the eventual accepted head's `LiteDB/` tree before carrying these numbers forward. CI-only changes do not change this library.

All 84 fresh processes and 420 measured windows completed with verified result/index/sentinel and cold reopen. Four alternating rounds, five seconds warmup plus five one-second windows, DOTNET_TieredCompilation=0. No local tests, builds or profiles overlapped timing. Host load otherwise uncontrolled. Rates are median process rates; ratios are medians of paired process rates. Managed allocation is process-wide and includes holder workers. This read-only transaction workload measures lifecycle overhead, not fsync/write throughput.

`reuse-raw/` compares pre-reuse PR head c8c0cfab6 with the final production source for zero/one/ten reads. `upstream-raw/` compares current origin/dev 5dd942a73, stacked parent49c327cf1 and final source for ordinary/legacy reads and attach/count/dispose. All windows and stderr retained. `binaries.json` hashes every exact runner/library file; its original source paths identify where they were collected. The same files are copied here under bench-*.

Replay: install .NET10, copy a configuration, replace each versions.*.runner with the corresponding archived absolute TransactionHandleBenchmarks.dll path, then run `python3 benchmark-transaction-handle-steady.py --config CONFIG --output NEW_DIRECTORY`. Summarize with `python3 summarize-transaction-handle-steady.py NEW_DIRECTORY`. Preserve original raw files; do not overwrite them.

The original 4.69x child-wrapper proof result was not reproduced after required correctness cleanup. The measured3b60 one-read paired gain is1.323x; ordinary/legacy read regressions versusdev remain. Earlier f84 data in ../performance/ is historical, not final-source evidence.
