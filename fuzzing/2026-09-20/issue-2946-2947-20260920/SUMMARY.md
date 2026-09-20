# LiteDB fuzzing validation — issues #2946 and #2947

Date: 2026-09-20 UTC  
Base commit: `ffc3edfc9728cc8cceccd8337f4a31a6817d2b37`  
Runner: the `LiteDB.Fuzz` implementation in the issue #2947 worktree

## Result

All 47 final validation runs passed: 1,006,683 generated cases/operations across
all 15 targets, with 1,161 seconds of aggregate target runtime. No LiteDB
correctness or durability failure remained after minimizing and correcting
harness-oracle assumptions discovered during development.

| Campaign | Runs | Generated steps | Result | Purpose |
| --- | ---: | ---: | --- | --- |
| `results/pr-smoke` | 15 | 450 | 15 passed | every target once, including real child-process death |
| `results/minute-correctness` | 18 | 1,004,861 | 18 passed | two independent one-minute shards for query/cache/state/WAL/page/index/BSON/parser/value |
| `results/persistence` | 12 | 1,002 | 12 passed | two shards for FileStorage, rebuild, vector, shared-process, sort, and mapper targets |
| `results/cache-parser` | 2 | 370 | 2 passed | widened LINQ shapes plus SQL/expression template rebinding and INCLUDE |

Each run directory contains the unchanged `run.json`, `summary.md`,
`trace.jsonl`, and `replay.json` emitted by the runner. Persistent targets also
retain database files and child-process ledgers where applicable. The run
metadata records OS, runtime, architecture, culture, timezone, seed, target,
base SHA, exception (when present), and replay command.

## Coverage exercised

- Independent Boolean query oracle against unindexed, indexed, and cached SQL
  execution across collations and BSON boundary values.
- Cached/direct/CLR LINQ comparisons, shared-mapper concurrency, DateTime,
  nullable, interface, DbRef, `Any`/`All`/`Select`, and `GetIndexExpression`
  shapes.
- CRUD and explicit transaction state model with rollback, reopen, checkpoint,
  collection/index DDL, duplicate failures, and small safepoint limits.
- Event-indexed read/write/partial-write/set-length/flush WAL failures, checking
  acknowledged transactions and rejection of torn logical transactions.
- Page allocator payload model plus byte accounting, footer, slot, overlap,
  defrag, and reload invariants.
- Scalar, unique, multikey, ordering, key-moving update, reopen, and index
  rebuild checks.
- Real multi-process shared-mode workers, transaction ledgers, checkpointing,
  timeouts, and deterministic owner-process death.
- BSON fragmented readers/writers, malformed mutations, parser/template/cache
  rebinding, mapper, FileStorage, rebuild/encryption/collation, vector churn,
  long-key sorting/Top-N, JSON/tokenizer/ObjectId/connection-string/value laws.

## Development findings

The longer pre-validation run found reproducible harness boundaries rather than
product bugs: MinValue/MaxValue cannot be stored as index keys; BSON field names
cannot contain NUL; SQL `NOT` uses the canonical Boolean expression form;
grouped aggregates require `SUM(*.Score)`; connection strings redact passwords
unless `ToStringWithPassword()` is used; CLR array equality is reference-based
while BSON equality is structural; duplicate-key failures can invalidate an
explicit transaction; reference comparisons must use the database collation;
and malformed BSON can be rejected by several documented exception classes.
The generators/oracles were corrected and the final campaigns above are clean.

The long 24–72 hour workflow was configured but intentionally not started.
