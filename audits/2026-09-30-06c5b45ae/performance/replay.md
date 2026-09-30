# Replay

Use .NET 10.0.11 on the recorded platform for a comparable run. The supplied binaries are production Release with test hooks disabled. They are evidence artifacts, not packages for deployment.

Copy a configuration JSON and replace each `versions.*.runner` absolute path with that version's `binaries/<version>/TransactionHandleBenchmarks.dll`. Keep scenarios, revisions, warmup, windows, repetitions and alternating order unchanged. Run:

```sh
python3 benchmark-transaction-handle-steady.py --config replay-config.json --output replay-results
python3 summarize-transaction-handle-steady.py replay-results > replay-summary.json
```

The pre-handle dev and parent runners omit the conditional handle API branch; their ordinary/legacy workloads use the same runner source. The pre-reuse head and final reuse comparison use the identical runner DLL. Every process reports its actually loaded library hash. No samples are removed, including low windows on this shared host. Allocation is process-wide managed allocation, including holder workers, not live retained memory. Read-only transactions with durable commit configured do not measure write/fsync throughput.
