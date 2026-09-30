# Explicit transaction handles (v6)

`LiteDatabase.BeginTransaction()` creates one independently owned synchronous
transaction. `ILiteDatabase.BeginTransaction()` is an extension using the optional
`ILiteTransactionProvider` capability; existing interface implementers need no new
members. Providers without the capability fail before starting a transaction.

```csharp
using var tx = db.BeginTransaction();
var users = tx.GetCollection<User>("users");
users.Insert(user);
tx.Commit();
```

“Bound” means that each collection, query and reader obtained through `tx` always
executes within exactly that transaction and rejects use after completion. Only
objects obtained through `tx` enlist. Ordinary database collections, including
ones used in a mapper/input callback, remain ordinary operations (or use their
applicable legacy transaction). Two handles are separate transactions, even on
the same thread; they are not nested transactions or savepoints. Existing isolation
and durability settings are preserved. The API adds no snapshot-isolation promise.

Sequential handoff to another thread is supported, including after the creating
thread exits. Overlapping public calls and public reentry on the same handle fail
before executing and do not abort the legitimate operation. This includes mapping,
query execution, enumeration, reader access, commit, rollback and disposal. The
guard detects overlapping calls, not accidental sequential sharing by an application.
Raw `LiteEngine` reentry from a bound mapper/input/read callback is rejected before
side effects. Ordinary `LiteDatabase` objects keep callback work independent, but
cannot wait for ownership held by the executing handle. In Direct mode, an ordinary
callback write that needs the handle's collection lock fails immediately with the
existing lock-timeout error; unrelated collections remain independent.

In Shared mode, an ordinary callback operation requiring the same database's native
writer ownership throws `InvalidOperationException` before waiting. This includes
`db.UserVersion` and writes to unrelated collections, even through another facade
using the same mutex namespace. Defer that work until the handle completes, or use
the appropriate supported transaction-bound API explicitly. Ordinary coordinated
reads that need no native writer ownership remain independent. Catching a refusal
inside the callback leaves the handle Active and its previous writes intact. Work
on another database remains independent, and an idle handle can still be completed
on another thread to release a legitimate waiter. The dependency check lasts only
for the synchronous handle operation; it neither enlists ordinary calls nor changes
the general Shared admission timeout contract.
Internal engine composition carries a one-use dispatch authorization, consumed
before entering user callbacks. If a callback lets that refusal escape a statement
whose engine transaction was rolled back, the handle becomes Failed.

## Supported surface

| Backend or API | Handle support |
| --- | --- |
| Built-in file-backed Direct | Yes; retains the existing pooled host and originating session |
| Direct memory, temporary and caller-stream engines | Yes; existing caller ownership remains applicable |
| Built-in filename-backed Shared | Yes; uses existing native admission/mutex/recovery protocol |
| Read-only Direct/Shared | Queries and completion; mutation remains forbidden |
| Shared memory/temporary storage or caller-supplied data/log/temp streams | Rejected before handle admission |
| Shared handle begin under Windows thread impersonation | Rejected before queuing/acquisition; holder context must not silently use a different OS identity |
| Coordinated and custom/decorated engines inside `LiteDatabase` | Rejected; legacy/ordinary support unchanged |
| Typed/BSON collections, bulk input, queries, Include, vector queries | Yes |
| Index creation/removal and collection metadata | Yes |
| Collection drop/rename | Explicit `NotSupportedException` before mutation |
| SQL, FileStorage, nested begin, checkpoint, rebuild, pragma mutation | Not exposed through the handle |
| External/system query I/O | Rejected; `$cols` and `$indexes` metadata are supported |

Shared caller streams can hold application callbacks that capture their session.
A native holder retaining those streams would prevent abandoned sessions from
being collected. This initial capability boundary avoids that ownership cycle;
it does not change ordinary Shared or Direct caller-stream ownership.

## Outcome and cleanup

`State` is `Active`, `Committed`, `RolledBack`, `Failed` or `Indeterminate`.
`Commit()` and `Rollback()` return void. Repeated completion is a usage error;
repeated disposal is safe. A disposed bound reader or enumerator throws
`ObjectDisposedException` before transaction execution, leaving earlier writes
intact. This takes precedence over transaction-completed errors for that disposed
object. Commit with an open bound reader fails before mutation
and leaves the transaction active: dispose the reader and retry commit.

An executing statement failure aborts the handle; there is no statement savepoint.
Read-only/capability refusals before mutation leave it active. Disposing a healthy
active handle rolls it back. Disposal racing session close leaves rollback to the
close owner and does not throw merely because admission closed. Normal overlapping
user operations and callback reentry remain invalid. If another transaction has
already fatally stopped the host, disposing a healthy peer releases its resources
without rethrowing the other transaction's fatal error. Terminal disposal never commits or claims rollback of
an indeterminate commit. A known committed result stays committed if later cleanup
fails. A failed flush does not prove recovery will find the write absent.

Completion releases transaction locks, Shared writer ownership and unused host
references, and unregisters the handle as soon as cleanup finishes. Keeping a
completed handle alive does not retain those dependencies. Bound collections,
queryables and readers reject further work after completion; they never revert to
automatic transactions.

Original errors remain primary. Additional cleanup errors are attached in
`Exception.Data` under `LiteDB.*Cleanup*`, `LiteDB.StatementRollback`, or
`LiteDB.TransactionRollback` keys. A fatal storage error stops every session using
that Direct host; recovery requires releasing its owners and opening a new host.

## Session close and resource topology

Disposal moves a session from Open through Closing to Closed. It rejects new work,
cancels pending handle admission and ordinary Shared owner/pin acquisition,
settles idle owned handles, and drains executing
work before releasing its engine lease. Each disposal call waits up to 10 seconds,
including cleanup time. A timeout leaves the session Closing with needed resources
retained; cleanup continues automatically when outstanding work finishes. A retry
joins cleanup and reports any deferred cleanup failure once. Reentrant disposal
from an executing operation or its internal native holder is rejected before
changing the session state.

Initial cleanup runs independently of the application thread pool, so callers
disposing sessions cannot exhaust the pool needed to clean them up. If the runtime
cannot start that cleanup worker, disposal reports the startup error, retains the
Closing session and its ownership, and a later disposal retries scheduling.
At most two empty cleanup workers are reused for up to one second. Busy workers
do not limit other sessions, and idle workers retain no session, callback or caller
execution context. This cache never retains database admission or storage ownership.

Already-open ordinary readers retain their existing independent lifetime; bound
readers belong to their handle/session. Peer Direct sessions remain usable unless
the shared storage host has suffered a fatal error. `disposeOnClose=false` never
grants ownership of the externally supplied engine to the facade.

Direct keeps the parent PR's one compatible storage host per canonical database
identity and loaded LiteDB assembly, with independent EngineContexts. Explicit
transactions have logical identities; only the deprecated legacy adapter uses
thread-local lookup. A synchronous dispatch scope carries identity through existing
internal call composition; it does not own the transaction or flow across threads.

Writable Direct holds exclusive native admission for the host's entire dependency
lifetime, including idle periods between transactions. It excludes every other
process, including read-only Direct opens. Compatible writable facades in the same
process share that host and may read/write through their independent contexts;
transaction/collection locks still govern overlapping work. Standalone read-only
Direct processes can coexist only when no writable Direct or Shared owner is
admitted. A conflicting Direct open fails; it does not queue for its turn. The last
real dependent lease releases admission after cleanup, allowing another process to
open the file. Opt-in waiting for Direct opening remains [#3068](https://github.com/litedb-org/LiteDB/issues/3068).

Shared keeps separate engines. At most one new handle per database/mutex namespace
in the process passes the handle admission gate. That handle has a native mutex
holder worker and a child `SharedEngine` wrapper. Sequential handles reuse the
wrapper on that Shared connection and an available holder worker. Pending begins
wait on their calling threads before checking out a wrapper or scheduling a holder. Ordinary and legacy Shared callers still use
the native mutex; they cannot recurse into a handle's writer ownership. As with
existing Shared native admission, parameterless begin can wait until the owner
releases it; closing its session cancels pending admission. Cached admission gates
are inert metadata.

These mechanisms have separate jobs:

| Mechanism | Ownership and scope |
| --- | --- |
| Shared lifetime admission | Allows compatible Shared participants and excludes Direct hosts for the dependent lifetime; it does not mean that the process owns the writer mutex throughout |
| Native writer mutex | Serializes writer ownership across processes and ordinary/legacy/handle callers |
| Local handle gate | Queues only the new transaction handles for that database/mutex namespace, before checking out their holder/child wrapper |
| Ordinary Shared snapshot reads | Use the existing snapshot/version and reader-lease protocol where supported, without enlisting in an explicit handle; fallback/native coordination remains backend-dependent |

Each handle releases native ownership before the next handle acquires it. The local
queue does not remove this release/acquire pair or consolidate ordinary and legacy
writers into one process participant. Broader writer scheduling and remote fairness
remain [#3069](https://github.com/litedb-org/LiteDB/issues/3069).
Participation/mapping/reader-registry consolidation is [#3017](https://github.com/litedb-org/LiteDB/issues/3017);
retained coherent engine/cache state is [#3004](https://github.com/litedb-org/LiteDB/issues/3004).

A reused wrapper closes its underlying `LiteEngine` after every handle and opens a
fresh core after reacquiring native ownership. Its page/cache/WAL view is never
retained across external writers. Open or cleanup failures and changed effective password/collation discard the wrapper;
session disposal also disposes an idle wrapper. The holder has only a weak link to
the owning Shared connection, so abandoned handles can still release storage through
finalization without retaining the application graph. Completed handles release their
session and resource references as before.

The separate holder pool retains at most two idle background threads for one second;
busy holders do not prevent another database from obtaining a worker. An idle worker
retains neither its last callback nor its execution context. The live Shared connection
retains at most one child wrapper and its ownership infrastructure. Idle data/log
handles, coordination participation and the child's mode-admission lease are released
along with native writer ownership and the storage core. A handle-only facade therefore still permits a
Direct process between handles; an ordinary operation on the parent retains that
parent's independent Shared lifetime admission as before. This trades bounded
idle infrastructure for lower setup cost; it introduces no batching or durability change.
See [Shared holder reuse validation](transaction-handles-shared-reuse.md) for evidence.
The child closes its operation engine using normal WAL/checkpoint thresholds;
the parent session retains the final checkpoint policy. Completing each handle
does not force an extra final checkpoint. Acknowledged WAL commits remain durable
if the process dies before that session closes.

Operation leases cover the full storage call and completion tail. Maintenance and
close cannot replace/dispose the core until those calls finish. Pending maintenance
fences new independent calls while permitting existing transaction and cursor
continuations to drain. Raw engine close rejects new independent calls instead of
queueing them behind the drain, including calls started by another thread from an
active callback. It also interrupts collection-lock waiters before waiting for
active operations; it still releases transaction/page resources only after that
drain. A callback that never returns for an unrelated application reason can still
hold raw engine close open; the session's 10-second disposal deadline does not apply
to direct `LiteEngine.Dispose()` calls. Cursor/snapshot
leases separately protect idle readers. Existing native lock/MMF/sidecar protocols,
filesystem support, file formats, WAL publication and configured durability remain
those of [native admission](native-database-admission.md).

## Opt-in Shared admission deadline and cancellation

```csharp
using var tx = db.BeginTransaction(TimeSpan.FromSeconds(2), cancellationToken);
tx.GetCollection<User>("users").Insert(user);
tx.Commit();
```

The `sharedAdmissionTimeout` overload uses one monotonic budget for the combined
local handle-gate and native writer-mutex waits. `TimeSpan.Zero` attempts immediate
admission at both stages; `Timeout.InfiniteTimeSpan` keeps the unbounded default.
Starting a handle from a callback that is executing under the same Shared writer
ownership is refused with `InvalidOperationException` before the local gate wait.
This includes an explicit handle callback, an ordinary pinned write, and a
mutex-backed reader transferred to another thread, including calls through another
connection to the same mutex namespace. A leased reader whose snapshot is independent
of writer ownership may still start a handle. An idle handle still permits a new
caller to wait while another thread completes it.
Other values must be nonnegative and at most `Int32.MaxValue` milliseconds.
Expiration throws `TimeoutException`; caller cancellation throws
`OperationCanceledException` carrying the supplied token. Partial ownership and
registrations are cleaned before the failed begin returns; cleanup failures remain
secondary diagnostics on the original error. Failed admission never completes or
releases the current owner's transaction.

Cancellation applies only while beginning a transaction. Once a handle is returned,
later token cancellation does not cancel its operations or an executing commit,
and cannot determine its commit outcome. Admission tokens and their callbacks are
detached before return. Internal holder threads do not retain caller execution
contexts. Session close still cancels pending begins independently.
Holder settings are detached from application subclasses, and explicit collation
is copied through its existing serialized policy. Application fields on settings
or collation cannot keep an abandoned session and its writer ownership alive.
Default collation initialization still observes the caller's culture, and an unset
collation still accepts the existing file's persisted value. On Windows, beginning
a Shared handle while impersonating is explicitly unsupported: it fails before
queuing rather than opening storage under a different identity.

The timeout bounds the two Shared ownership waits, not the total method duration:
engine opening/recovery I/O, cleanup and existing Direct/collection lock waits keep
their existing contracts. Direct observes begin cancellation at acquisition
boundaries; this overload does not add a deadline to Direct file opening.
Collection-lock `TIMEOUT` is a different setting. Do not use parameterless begin for
a second Shared handle when the blocked caller is the only code able to complete
the first; use a bounded begin, or arrange independent completion/cancellation.

`ILiteDatabase` exposes the overload through the optional
`ILiteTransactionAdmissionProvider` extension capability. Neither `ILiteDatabase`
nor the existing `ILiteTransactionProvider` gains required members. An older/custom
provider without this capability rejects the overload before beginning work; it
does not silently ignore the timeout/token.

## Migrating legacy callers

`BeginTrans`, database-level `Commit` and `Rollback` remain binary compatible and
thread-bound. They now emit **CS0618** (`Obsolete`, warning only). Replace the trio
with a handle and obtain the participating collections from that handle. Valid
existing synchronous legacy use continues to work; crossing `await` remains unsafe
for the legacy API.

Projects using `TreatWarningsAsErrors` may migrate incrementally with a targeted
`<WarningsNotAsErrors>$(WarningsNotAsErrors);CS0618</WarningsNotAsErrors>` setting,
or a narrow `#pragma warning disable CS0618` around intentional legacy calls.
Do not disable unrelated warnings.

Close/checkpoint cleanup failures now propagate from `LiteEngine.Dispose`, Direct
session disposal, and a Shared handle child's close. If session disposal waits
for a Shared pin, its core-close failure propagates after the connection finishes
independent resource cleanup. If the last ordinary reader releases an idle pin,
a pin-core close error can instead surface from that reader's `Dispose()`.
If that disposal runs inside another reader callback which forced pin close is
already draining, it does not join its own drain: the concurrent Shared connection
disposer remains responsible for reporting the pin-close error. An optional
last-reader checkpoint also skips a dependent owner-retirement wait; the committed
WAL remains authoritative for the next open.
The engine preserves the WAL needed to recover acknowledged commits.
A leased Shared reader refuses disposal from inside its own executing callback
before changing its cursor or ownership. Catching that refusal and retrying after
the callback returns completes snapshot and admission cleanup. Disposing a different,
independent leased reader from a callback remains supported.

Raw Shared disposal from an executing callback is refused before changing the
connection state or writer ownership. The current call may finish, and disposal
may be retried afterward. This also covers reader callbacks whose original Query
call has already returned. An owning facade also refuses a late-reader callback
close before publishing session shutdown when its storage teardown would wait for
that callback. Independently retained pooled Direct and leased Shared snapshots
keep their supported facade-close behavior; disposal from another thread still
drains the active operation.

The Shared parent's final checkpoint retains its historical best-effort policy:
returned core-close errors, and expected I/O/access/database errors opening or
checkpointing that final core, do not make session disposal throw. This applies
both to an already-open parent core and a fresh core opened solely for final
checkpointing; it does not suppress handle-child or pin-core disposal errors.
Successful session disposal therefore does not establish that a Shared WAL was
fully checkpointed. A retained WAL remains authoritative, and a later open
recovers its acknowledged commits. The plain/encrypted parent-only failure and
cold-recovery test is
`SharedFinalCheckpointContract_Tests.Parent_final_checkpoint_remains_best_effort_and_failed_attempt_keeps_committed_wal`.

An injected close-checkpoint data-write error is asserted by identity, followed by
plain/encrypted recovery of every committed row and index; see
`CheckpointDataWriteFailure_Tests.FailedCloseCheckpoint_LeavesTheWalToRecoverEveryCommit`.

Using a disposed `LiteDatabase` now throws `ObjectDisposedException` naming
`LiteDatabase`, instead of `LiteException` with `ENGINE_DISPOSED`. Callers that
catch the old error code for facade use-after-dispose must catch
`ObjectDisposedException` instead. This is an intentional exception-contract
change; it does not make concurrent use of a closing facade valid.

## Safety evidence and limits

The transaction-handle test classes cover handoff/creator retirement, exact binding,
overlap, mapper callbacks, cursor completion, close deadlines and eventual cleanup,
terminal resource release, read-only behavior, WAL write/flush failures, cleanup
failures and process termination with repeated cold reopen/index/sentinel checks.
The broader legacy, admission, Shared and maintenance suites remain required.
Hosted CI and performance evidence must identify the final tested revision.

Tests also distinguish abandonment of a whole dirty cache from leaking a page
owned by a surviving cache. The test-only finalizer diagnostic uses a short weak
cache reference; explicit cache disposal remains strict, and finalizers perform
no new storage I/O or rollback. The original assertion failure was reproduced on
parent `49c327cf1926fa300f9eb7477eb4bcb404f75c43` using only legacy `BeginTrans`.
Abandonment is recovery, not a substitute for deterministic disposal.
