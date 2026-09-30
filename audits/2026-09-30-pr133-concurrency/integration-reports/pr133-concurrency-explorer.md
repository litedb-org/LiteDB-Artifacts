# PR #133 bounded concurrency explorer

The explorer runs real dedicated actors inside the existing xUnit suite and the
isolated `LiteDB.Fuzz` runner. It does not replace the process runner, timeout,
retention or replay infrastructure. Production code and production hooks are
unchanged. The source baseline is `98a10086c`; execution results belong in the
integrated audit report, including failures. A green old PR build does not qualify
these newly covered schedules.

## Finite gate

`TransactionHandleInterleaving_Tests` defaults to **68 representative cases**: all
34 vectors × Direct/Shared, with encryption and completion parity chosen from the
vector bits. These configuration choices are correlated, not a full Cartesian
claim. Both pinned/plain and unanchored/encrypted known-defect paths still fail.
Set `LITEDB_EXPLORER_FULL=1` to enumerate all 34 vectors × Direct/Shared ×
plain/encrypted × commit/rollback parity: **272 independent cases per runtime**. Cases continue after a different theory case fails. Fuzz
`transaction-interleavings --count 272` enumerates the same configuration space,
rotating its starting index by the recorded seed; like other fuzz targets it stops
at the first failure. That prefix is not a completed 272-case campaign.

The vector chooses an explicit sequence of enabled controller actions. Actors
execute database calls concurrently between observed boundaries; this is bounded
schedule exploration, not exhaustive enumeration of every instruction or every
application program. The permutations of overlap refusals are deliberately named
as contender-order permutations, not independent internal engine schedules.

| Vectors | Forced boundary and competing work | Oracle |
| --- | --- | --- |
| 0–11 | A executing bound bulk-input callback; B attempts read/commit/rollback in all six orders; same/peer facade; callback performs ordinary same-file/other-file work | Overlap refuses without aborting A; Shared ownership dependency refuses; Direct unrelated ordinary write and other-file write commit independently; transferred reader sees its transaction; open reader prevents commit; disposed reader refuses; commit/rollback cold state |
| 12–23 Direct | Two handles hold different collections and are simultaneously inside input callbacks; C reads committed state; reverse completion order and either/both commit | No dirty updates/inserts; update/delete/insert compose atomically; independent rollback; exact acknowledged state |
| 12–23 Shared | Handle/legacy owner; second begin reaches actual local/native admission hook; controller cancels, completes owner or closes waiting/owning session | Global same-file handle semaphore is respected across facades; cancellation token identity; legitimate waiter progresses after release; close rolls back owner; no timeout accepted as successful cancellation |
| 24–25 Direct | Ordinary write paused under operation lease; rebuild has registered exclusive wait; C positively hits maintenance fence | Existing work drains, maintenance completes, new read sees complete commit; optional subsequent checkpoint |
| 24–27 Shared; 26–27 Direct | Bound callback active; session close paused after Closing publication before dispatch; overlapping commit; both gate-release orders | Admission refusal, active call drains, session close rolls back, cold state remains unchanged |
| 28–31 Shared | Ordinary input callback under pin/native ownership; same-facade, same-file peer, other-file controls | Existing same-facade recursion/other-file operation remain usable; same-file peer must not enter an impossible native wait. Actual wait counter is a failure, never a passing timeout |
| 32–33 Direct | A holds rows, B holds other; both positively hit opposite `CollectionLock.BeforeWait`; invocation order reversed | At least one configured collection timeout aborts its whole handle. Any survivor commits both effects; no loser effect survives |
| Remaining Direct/Shared counterparts | Active-close vector as above | Same close invariant, additional configuration repetitions |

The independent model stores acknowledged document ids and values and constructs
expected payloads itself. Each successful run stops all actors, disposes resources,
and cold opens twice. It compares every document's serialized contents, exact
sentinel contents, both collections' index catalogs, and indexed queries against
the model. It does not infer correctness from engine snapshots or acknowledge an
operation merely because it was invoked.

The oracle follows `docs/transaction-handles.md`: separate handles, no implicit
ordinary-call enlistment, overlap refusal, bound-object lifetime, whole-transaction
rollback and committed state. It asserts no unrestricted linearizability or new
snapshot-isolation contract. The opposite-lock cycle permits either/both timeout
victims; only complete outcomes corresponding to actual successful commits are
accepted. No injected I/O failure occurs here; an unexpected indeterminate result
fails this target rather than being silently classified as committed/aborted.

## Progress, failure retention and replay

Each actor's current operation has its own immutable start timestamp and completion
timestamp. Every controller wait checks all actors, and only that operation's
completion ends its 15-second deadline. Peer activity and stage logging cannot
refresh it. Hook arrival alone never completes an operation. The diagnostic log
flushes actor invocation, completion, exception, acquisition and release events
with monotonic sequence/timestamp into `<database>.history`.

On failure the runner releases only its explicit test barriers, then joins every
actor. It does not dispose/reopen/copy a database with a live worker. Original
fixtures and history remain. Cleanup errors are attached without replacing the
first failure; when all workers stopped, cleanup errors do not skip the cold
oracle. A native self-wait discovered in the ordinary-callback diagnostic is
released by closing only its waiting facade, then still reported as a **failure**.
This bounded diagnostic does not claim the application would recover unaided.

Examples (build with `TestingEnabled=true` consistently):

```bash
LITEDB_EXPLORER_FULL=1 dotnet test LiteDB.Tests -c Release -f net8.0 -p:TestingEnabled=true \
  --settings tests.runsettings --filter FullyQualifiedName~TransactionHandleInterleaving

dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- \
  --target transaction-interleavings --seed 133098 --count 272 \
  --artifact-dir artifacts_temp/concurrency-explorer

dotnet run --project LiteDB.Fuzz -c Release -f net8.0 --no-build -- \
  --replay <retained-run>/replay.json --artifact-dir artifacts_temp/concurrency-replay
```

xUnit failure fixtures remain at their original temporary directory; failure
exception data names `ExplorerFixture`, `ExplorerSchedule` and `ExplorerSeed`.
Fuzz uses its recorded seed, deterministic vector and input/replay metadata;
actual histories are supplementary diagnostics and not forced byte-identical
thread timestamps. The bounded vector reproduces its synchronization edges, not
unconstrained OS scheduling of completion after those edges.

## Explicit limits

No device/power-loss claim, scheduler fairness proof, arbitrary lock-graph model
checker, GC/abandonment campaign, arbitrary callback graph or platform alias claim
is made. This target does not inject indeterminate commit failures; the process
and existing modeled-power-loss targets own those protocols. Native cross-process
exclusion is covered by the multiprocess campaign, not inferred from this
same-process wait counter. The initial 272-case matrix contains repeated close
controls by design. Sustained starvation under unbounded arrivals and arbitrary
higher-actor combinations remain outside this finite gate.
