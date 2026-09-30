# PR #133 multiprocess concurrency campaign

`shared-lifecycle` extends `LiteDB.Fuzz`, retaining its input recording, replay,
epoch supervision and failure artifacts. It does not change the existing `shared`
corpus. Production baseline: `98a10086c`. No production behavior or hook was added.

## Finite boundary matrix

Every ten-case block executes the following cuts; alternating blocks switch plain
and encrypted files. Twenty cases therefore cover every cut in both modes.
Recorded input chooses lexical `./` aliases and one of six invocation orders for
three competing processes.

| Observed cut | Killed actor | Required state |
| --- | --- | --- |
| `native-waiter` | Handle writer after actual admission observer and exclusion window | Owner commits and replacement writer progresses |
| `uncommitted` | Reused-handle owner after update/delete/inserts, before commit request | Owner group absent |
| `wal-before-durable-flush` | Reused-handle owner inside existing crash hook | Complete old or new outcome |
| `wal-after-durable-flush` | Same, after sync but before public acknowledgement | Complete new outcome: confirmation and preceding pages were flushed |
| `after-commit-acknowledgement` | Idle owner after parent receives commit acknowledgement | Owner group survives |
| `after-refreshed-wrapper-reuse` | Owner after reused handle observes another process's commit | Owner and peer survive, same wrapper reference |
| `before-reader-dispose` | Process with observed live independent snapshot | Surviving processes progress, acknowledged state survives |
| `before-session-dispose` | Idle session after completed handle cleanup | Surviving processes progress, acknowledged state survives |
| `checkpoint-before-data-flush` | Legacy owner inside checkpoint hook | Acknowledged transaction survives |
| `checkpoint-after-data-flush` | Same, after data sync | Acknowledged transaction survives |

Checkpoint cases use the existing `SharedCheckpointCrashScope` so peers cannot
empty the WAL before the hook. These cases exercise legacy ownership. Reader and
session disposal cuts are **API boundaries before cleanup**, not interruption
inside an internal release implementation.

Non-checkpoint cases keep an independent process's real leased snapshot across
writes. Identity `ReadTransform` prevents buffering tiny results. Before publishing
`snapshot-held`, the child requires an owned snapshot, one registered local lease,
and released native writer ownership. Surviving snapshots are drained after the
writers finish and compared with the complete original documents. The killed
reader cannot subsequently enumerate; that case checks reclamation and recovery.

A writer reaches native handle admission while the owner is live. A 150 ms
exclusion window starts only after the observer marker. Opener/disposer, ordinary
reader and maintenance actors receive commands while the native owner is alive.
Each command has a 20-second monotonic invocation deadline. Intermediate wait
markers and peer progress cannot reset it. A background output pump timestamps
completion, so consuming an already-completed response later is not a timeout.

## Independent state and progress checks

The supervisor models an owner group containing an update, delete and two inserts,
plus a peer group containing two inserts. It records received acknowledgements in
a durable ledger outside the killed process. Requested but unacknowledged commits
allow only complete before/after groups until a flush-completed hook proves the
confirmation recoverable; that observed cut requires the complete new state.
Known uncommitted groups must be absent.
Acknowledged groups must survive even if subsequent ledger I/O fails.

Two cold reopens compare exact BSON documents, sentinel, index catalog, actual
secondary `INDEX SCAN` and per-value `INDEX SEEK` results. The existing independent
raw-file integrity verifier then checks structural and semantic index integrity.

Every child is stopped and joined before reading/copying the original fixture.
Cold verification still runs after an actor failure once quiescence is established;
its error is preserved alongside the original failure. If process termination
fails, the original path is retained and cold verification/registered-file copying
are blocked. Successful survivors also exercise graceful close. Pipe drains are
bounded. Actor JSONL contains invocation/completion timestamps and PIDs; durable
boundary markers, parent acknowledgements, stderr, schedule, seed/input and replay
metadata remain with the run.

The generic runner's artifact-budget failure path can delete databases. Use
`--max-artifact-mb 0` for investigations requiring every failure fixture. The new concurrency CI shard passes `--max-artifact-mb 0` explicitly; successful
nightly epochs retain existing compaction. Other shards keep their prior budget.

`SharedLifecycleOracle_Tests` rejects seven controlled bad states: acknowledged
loss, partial insert/update/delete/peer groups, missing sentinel and missing index.
Two positive controls accept complete unknown outcomes. `SharedLifecycleProgress_Tests`
checks one stalled worker despite peer progress, delayed waiting without resetting
the invocation deadline, and a missing native marker. Mutants validate the oracle;
they are not historical regression proofs.

## Replay and limits

```bash
dotnet build LiteDB.Fuzz/LiteDB.Fuzz.csproj -c Release -p:TestingEnabled=true
dotnet LiteDB.Fuzz/bin/Release/net8.0/LiteDB.Fuzz.dll \
  --target shared-lifecycle --seed 133098 --count 40 --determinism-check \
  --artifact-dir artifacts_temp/shared-lifecycle --max-artifact-mb 0
dotnet test LiteDB.Fuzz.Tests/LiteDB.Fuzz.Tests.csproj -c Release \
  -p:TestingEnabled=true --filter FullyQualifiedName~SharedLifecycle
# Extended campaign uses existing epoch/replay machinery:
dotnet LiteDB.Fuzz/bin/Release/net10.0/LiteDB.Fuzz.dll \
  --target shared-lifecycle --seed 233098 --duration 30m --workers 2 \
  --artifact-dir artifacts_temp/shared-lifecycle-nightly --max-artifact-mb 0
```

These are process deaths with surviving OS caches, **not power loss**. Existing
`power-loss`, checksum and MVCC targets keep their modeled durable/volatile tests.
Physical hardlink/symlink aliases, Direct pooling, platform admission fallback,
internal cleanup publication windows and device failure are outside this target.
It does not claim unrestricted linearizability, arbitrary snapshot isolation,
scheduler exhaustiveness or starvation freedom. A missed progress deadline needs
triage rather than automatically proving a product deadlock. Linux execution does
not establish Windows/macOS behavior. Exact final commit and hosted results belong
in the combined audit report.
