# PR #133 performance options — 2026-09-30

Evidence for [JKamsker/LiteDB#133](https://github.com/JKamsker/LiteDB/pull/133).
The library PR itself is unchanged; all implementations are on separate proof branches.

- `experiment-identities.json`: pinned library revisions/SHA-256, OS, CPU, runtime and filesystem.
- `upstream/`: 60 fresh-process runs comparing dev, stacked parent and head.
- `options/`: independently paired ownership variants; inline close is a rejected upper bound.
- `writes/`: separate-process IPC and in-process durable-write strategies, with correctness checks.
- `safety-summary.json`: exact test names, pass/fail counts and tested revisions.
- `controller-safety.log`: 17 plain/encrypted, failure, recovery, concurrency and IPC scenarios.
- `rejected-inline-close.log`: failing bounded-close regression; this implementation is not accepted.
- `production-runners.zip`: exact measured libraries and runners; execute the DLL with `dotnet`.
- `test-results/`: raw TRX results for the summarized library test runs.
- `profiles/`: separate diagnostic EventPipe traces, commands, hashes and event summaries.

Production Release/net10.0 with TestingEnabled=false. Test suites use TestingEnabled=true
in separate checkouts. No local builds/tests/profiles ran alongside uninstrumented timing.
Host load and filesystem latency were uncontrolled. Timing percentiles are per-process or
per-window as stated in the source report; their medians are not pooled percentiles.

`raw.jsonl` files pack every run without dropping samples. Read/attach outputs include
metadata, warm active windows, and verified cold state. Write outputs include two cold
model/index/payload verifications, client and server CPU, sampled peak WAL, latency,
queue commit counters and drain time. Allocation counters exclude a separate IPC server.

Replay/summarization tools and the full contract are in the proof source branch:
[WriteControllerExperiments](https://github.com/JKamsker/LiteDB/tree/t3code/benchmark-group-commit-experiments/tools/WriteControllerExperiments)
and [report](https://github.com/JKamsker/LiteDB/blob/t3code/benchmark-group-commit-experiments/docs/transaction-performance-options.md).
The manifests retain exact commands; replace machine-specific absolute paths when replaying.

The controller batches commands into a single atomic transaction. It does not group the
WAL confirmations of already-open independent transactions. Child reuse caches a SharedEngine
wrapper but reopens its storage core after each native ownership handoff. The evidence does
not establish Windows/macOS behavior, coherent core reuse, automatic failover or exactly-once
IPC retries. Process-kill and last-synced-image power-loss tests use different fault models.
