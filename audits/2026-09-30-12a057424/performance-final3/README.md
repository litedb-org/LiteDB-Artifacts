# Interim production comparisons (measured 7e13/e821)

Status: review 5365276527 reproduced additional late-reader ownership/deadlock defects. A successor production fix and fresh measurements are required; these completed results are retained as interim evidence.

Measured source: `7e13e58e5f4e40642492ce768b49018e4a958c8b`. Current head `e821ae7479dcb83570031180b0c0e3f4fd167793` changes only proof-routing configuration; both LiteDB subtrees are `4519699ceab868a455fb448d34701a4d974f9d6f`. Production Release/net10.0, TestingEnabled=false, .NET 10.0.11, Ubuntu 24.04.3 x64/ext4, tiering disabled.

All 84 fresh processes and 420 measured windows completed with verified result/index/sentinel and cold-reopen checks. Four alternating rounds per case, five seconds warmup plus five one-second windows. No .NET builds/tests/profiles or archive compression overlapped timing. Eleven Python matrix-policy checks ran for 0.084 s; ordinary editing/Git/metadata inspection continued. Other host load was uncontrolled. Rates are medians of process rates; ratios are medians of paired process rates. Managed allocation is process-wide and includes holder workers. This read-only transaction workload measures lifecycle overhead, not fsync/write throughput.

`reuse-raw/` compares pre-reuse PR head `c8c0cfab623a22880b1d71eb966b09e89f9153b9` with measured source for zero/one/ten reads. `upstream-raw/` compares dev `5dd942a7367c361fadd600be4ce10aace2768b27`, stacked parent `49c327cf1926fa300f9eb7477eb4bcb404f75c43` and measured source. All raw windows, stderr and process ordering are retained. Binary hashes match every run's metadata; exact runners/libraries are archived under bench-*. `source-index.json` records pre-publication source hashes and verification. Public text paths are normalized separately; raw numbers and binary/input bytes are unchanged.

| Reads/Shared handle | Before tx/s | Current tx/s | Paired gain | Before bytes/tx | Current bytes/tx |
| --- | ---: | ---: | ---: | ---: | ---: |
| 0 | 1377.7 | 1723.6 | 1.258x | 232272 | 225441 |
| 1 | 1249.5 | 1571.8 | 1.249x | 242386 | 235567 |
| 10 | 1086.8 | 1335.7 | 1.231x | 306629 | 299875 |

The historical 4.69× / ~155 KB proof result was not reproduced. Required mode-admission, coordination and core cleanup, plus intervening correctness fixes, are included. This is the final implementation versus the pre-reuse head; it does not isolate the contribution of each change. Every transaction releases native writer ownership and reopens its core; only holder/wrapper infrastructure is reused.

| Workload | dev ops/s | Parent ops/s | Current ops/s | Paired vs dev | Paired vs parent |
| --- | ---: | ---: | ---: | ---: | ---: |
| Direct ordinary read | 91326.2 | 86346.2 | 78648.3 | -12.9% | -9.0% |
| Direct legacy begin/read/commit | 94283.0 | 86988.9 | 73071.5 | -22.8% | -16.9% |
| Direct attach/count/dispose | 7131.3 | 16634.7 | 11080.4 | +54.2% | -33.4% |
| Shared ordinary read | 52033.0 | 52550.8 | 47199.4 | -8.1% | -10.2% |
| Shared legacy begin/read/commit | 8347.5 | 8002.9 | 7645.9 | -8.5% | -4.0% |

These remaining regressions are explicit. Ordinary Direct allocation is 10,112 / 10,264 / 10,248 bytes per operation (dev/parent/current), so allocation volume alone does not explain the regression. Attach benefits from the stacked pooled host against dev while still paying the handle PR's lifetime machinery against parent. Earlier profiling and isolated options are historical evidence; no broader optimization is folded into this implementation.

Replay: install .NET 10, copy a configuration, replace each versions.*.runner with its archived absolute TransactionHandleBenchmarks.dll path, then run `python3 benchmark-transaction-handle-steady.py --config CONFIG --output NEW_DIRECTORY`. Summarize with `python3 summarize-transaction-handle-steady.py NEW_DIRECTORY`. The upstream paired file contains all four current/baseline ratios for each comparison, calculated by matching round indices. Preserve original raw files; do not overwrite them. Exact runner source is included for inspection; binaries are authoritative measured inputs.
