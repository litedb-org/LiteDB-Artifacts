# Transaction handle comparison

Build parent and candidate **production** Release libraries in separate checkouts
with `TestingEnabled=false`. Pin the parent to PR132 commit
`49c327cf1926fa300f9eb7477eb4bcb404f75c43`. Pass each absolute DLL path to this
same benchmark source; use separate intermediate/output directories. Set
`Handles=false` for the parent and `Handles=true` for the candidate.

```sh
dotnet build tools/TransactionHandleBenchmarks/TransactionHandleBenchmarks.csproj \
  -c Release -p:LibraryPath=/absolute/path/LiteDB.dll -p:Handles=true \
  -p:BaseIntermediateOutputPath=/tmp/handle-bench-obj/ -o /tmp/handle-bench
DOTNET_TieredCompilation=0 dotnet /tmp/handle-bench/TransactionHandleBenchmarks.dll COMMIT > measurements.jsonl
DOTNET_TieredCompilation=0 dotnet /tmp/handle-bench/TransactionHandleBenchmarks.dll COMMIT contention > contention.jsonl
```

Single-call tests use five measured fresh-database repetitions after a discarded
warmup repetition, with 50 warmup operations per measured database. Direct reads
measure 10,000 operations; other cases measure 200. `open` measures attach, count,
and disposal while one session remains open. Durable commits are enabled; writes
update an existing BSON row. Every database is cold-reopened and checked.

Contention measures three repetitions of four threads, each holding its own
database session and updating its own row 40 times in the same collection. Timing
includes draining/disposal; p50/p95/p99 cover individual operations. Process-wide
allocation and sampled native thread/handle counts include harness overhead.
Direct and Shared have the same workload, durability, storage directory and data.

New handles are compared with legacy begin/operation/commit as a semantic adapter,
not with a nonexistent old handle API. Report ordinary, legacy and new-handle
results separately, with runtime/platform, binary hashes, distributions and costs.
Avoid running other builds/tests concurrently with measurement. These are local
latency/resource measurements, not a universal throughput guarantee.

Candidate-only resource sampling also exercises twelve pending begins, with and
without a read callback:

```sh
DOTNET_TieredCompilation=0 dotnet /tmp/handle-bench/TransactionHandleBenchmarks.dll COMMIT resources > resources.jsonl
```

It samples idle, active, waiting, drained and closed/collected process state; the
waiting sample includes twelve application threads. These are coarse process
samples, not an assertion about exact per-object retained memory. Deterministic
pending-admission and holder-retirement assertions remain in the test suite.

## Longer alternating comparisons

`steady` runs one scenario in a fresh process, defaults to ten seconds of warmup
followed by ten one-second measurement windows, and verifies cold-reopened records,
an indexed lookup and an unrelated sentinel. Arguments after `steady` are backend,
API, operation, reads per transaction, warmup seconds and window count:

```sh
DOTNET_TieredCompilation=0 dotnet /tmp/handle-bench/TransactionHandleBenchmarks.dll \
  FULL_COMMIT_SHA steady direct ordinary read 1 10 10
```

APIs are `ordinary`, `legacy`, and (candidate only) `handle`; operations are `read`
and `open`. `open` means attach/count/dispose with a peer retained. Zero reads
measures begin/get-collection/commit without query execution. Ten or one hundred reads per transaction
show how fixed transaction costs amortize. Throughput is transactions/operations
per second; `readsPerSecond` reports the corresponding read count separately.
Point reads use a seeded BSON row plus an index. Latencies include the full public
operation, and process-wide allocation counters include helper threads. Harness
buffers, JSON serialization and process-resource samples are outside the timed
allocation interval. Latency sampling is bounded to the first 250,000 operations
per window; raw output reports the actual count and sample count.
Windows are separated by reporting and process inspection; throughput uses active
window time. This is a warm steady-state comparison, not a cold-start measurement.

[`benchmark-transaction-handle-steady.py`](../../scripts/benchmark-transaction-handle-steady.py)
alternates version order AB/BA/AB/BA. Its JSON configuration supplies `versions`
(each with `runner`, full `revision`, optionally an API `mode` override) and `groups`
(each with `name`, `versions`, `cases`, optionally `rounds`, `warmup`, `windows`,
and `tiered`). A case is `["direct", "ordinary", "read", 1]`. Setting `tiered` to
null removes the environment override for a default-tiering confirmation.

```sh
python3 scripts/benchmark-transaction-handle-steady.py \
  --config /tmp/comparisons.json --output artifacts_temp/transaction-steady
```

The driver retains the exact configuration, commands, exits and raw windows. Each
process records its runtime, architecture and actual loaded-library SHA-256.
Build all runners before measurement; do not run local builds/tests concurrently.
Profiles are separate experiments and must not be substituted for uninstrumented
timings. Attribute grouped guard changes separately from cleanup-worker reuse;
do not claim an individual guard's cost from an end-to-end throughput difference.

Summarize complete runs with `python3 scripts/summarize-transaction-handle-steady.py
DIRECTORY`. Published evidence can pack the per-process files into `raw.jsonl`
with an added `run` field on each record; the same summarizer accepts that format.
It aggregates counts over active-window time and reports every paired ratio,
process range, allocation total and median window p95/p99. Windows from one
process are not independent repetitions, and medians of window percentiles are
not pooled latency percentiles.
