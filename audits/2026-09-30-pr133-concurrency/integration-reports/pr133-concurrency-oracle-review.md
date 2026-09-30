# PR #133 independent concurrency oracle review

## Decision and scope

The audit found a real, previously uncovered Shared callback deadlock at the
frozen head `98a10086c2a296040a345e62ea987d59fc2ab487`. The identical production
reproducer also fails on stacked parent
`49c327cf1926fa300f9eb7477eb4bcb404f75c43` and upstream dev
`5dd942a7367c361fadd600be4ce10aace2768b27`. This is a pre-existing defect newly
exposed by the audit, not a regression attributed to Shared wrapper reuse.
The affected concurrency merge decision remains blocked pending a separate
correction and failing-before/passing-after evidence. No production code was
changed by this reviewer.

This independent review inspected the dependency inventory, deterministic actor
explorer and multiprocess lifecycle campaign. A replacement reviewer completed
the final source review; the retained earlier executions below were inspected
as evidence and were not rerun or attributed to the replacement reviewer. It challenged hook reachability,
actual overlap, transaction outcomes, individual progress, cleanup and fixture
retention. It does not prove absence of every possible application wait cycle,
platform-specific native defect or physical power-loss failure.

## Confirmed product finding: ordinary callback enters a peer facade

1. Open Shared engine A with an identity `ReadTransform`.
2. Retain an ordinary streaming, independently leased reader on A.
3. Insert an enumerable through A. Yield one document, then synchronously call
   ordinary `Insert` through Shared facade B for the same file.
4. A's input callback is inside an active pin (`_operations > 0`, `_holds == 0`).
   B reaches `_mutexWaiters > 0` and waits for native writer ownership. A's
   holder cannot release that ownership until the callback returns.

The resulting cycle is callback -> B native admission -> A pin operation ->
callback. No application-created cross-thread join is needed. `CallbackScope`
checks same-file ownership for `BeginTransaction`, while ordinary peer write
entry checks only explicit-handle `TransactionContext` dependencies. B's local
foreign-reader check cannot see A's executing core.

The public APIs retain their default unbounded wait in the reproduction. An
isolated child checks the live pin and peer wait state, proves continued lack of
completion, then exits without graceful disposal. A timeout alone is not the
reproduction oracle. Plain and encrypted runs reproduced on all three revisions
(six production runs). Separate controls on the frozen head establish:

| Control, each plain and encrypted | Observed result |
| --- | --- |
| Same-facade nested ordinary write | Completes |
| Different-file nested ordinary write | Completes |
| Close the waiting peer session after observing its native wait | Cancels with `OperationCanceledException`; outer worker returns |
| Cold reopen after isolated deadlock child termination | Original row and sentinel survive; uncommitted rows absent |
| Cold reopen after cancellation control | Original row and sentinel survive; uncommitted rows absent |

The frozen-head bundle contains twelve outcomes: two deadlock detections, four
valid nested-write controls, two cancellation controls and four cold checks.
Historical runs are four additional deadlock detections. These checks executed
on Linux x64, .NET 8, Release `TestingEnabled=false`; the exact source, build
logs, runner/runtime binaries, fixture paths and results are retained in the
audit artifact bundle. These are local results, not hosted-CI results.

Permanent failing explorer schedules preserve the broader ordinary callback
class, including the separately discovered unanchored peer route. Replay the
selected `transaction-interleavings` schedule using its retained input/history;
see [the audit coverage matrix](pr133-concurrency-matrix.md). The minimal public
API source is retained as `peer-callback-repro/Program.cs` in the evidence bundle,
with `run.py`, `results.json`, `historical-results.json` and separate binaries for
each revision. Do not report the known-defect schedules as passing by accepting
their emergency cancellation, timeouts or lack of progress.

A separate correction should centralize same-namespace synchronous ownership
checks before every ordinary blocking acquisition route, including pin creation,
native entry and late reader callbacks. It must preserve valid same-facade
recursion, other-file operations and independent leased-reader callbacks. An
idle owner must not be confused with an executing callback dependency. The
correction requires its own real known-bad regression proof under #3034; this
audit does not implement or pre-approve that change.

## False negatives and test defects challenged

| Reviewed weakness | Disposition |
| --- | --- |
| Existing `TransactionHandleModel_Tests` handoffs join before the next operation | Kept as sequential model coverage; new actor explorer establishes overlapping calls |
| Initial explorer expected same-file peer handles to reach native admission | Corrected to the actual process-wide local writer gate; legacy ownership still uses native admission |
| Initial explorer cold oracle compared only IDs/values and one index | Strengthened to full payloads, both index catalogs and complete sentinel state |
| Initial process snapshot used three small rows without transform | Could buffer completely; replaced by forced streaming plus concrete snapshot/lease publication and native-release checks |
| Initial process reader was disposed without checking its old contents | Retirement now enumerates and compares the complete original snapshot after external writes |
| Initial process index query could silently become a table scan | Index catalog and actual scan/seek plans now asserted |
| Initial process timeout began at `Expect`, not invocation | Monotonic invocation deadline retained across native-wait and later completion; timestamped output pump records actual observation |
| Initial process cleanup could skip cold verification on worker error | Independent cold oracle runs after successful quiescence, preserving the original error alongside any oracle error |
| Initial process fixture copy could run after failed child termination | No copy/reopen without confirmed child termination; original path retained |
| Pre-dispose pauses described as internal cleanup boundaries | Renamed as pre-API cuts and documented as a remaining internal-boundary gap |
| WAL post-flush unknown outcome accepted old state | Strengthened: after confirmation flush returns, process death must recover complete new state; pre-flush remains complete old/new |
| Focused C06/C22 tests deleted failed fixtures after workers stopped | Final review reconfirmed the gap; author supplied success-only deletion, suppression of deleting `TempFile` finalizers on failure, and independently attempted owner cleanup. The replacement reviewer inspected that correction; integrated qualification remains with the audit report |
| Explorer refusal helper accepted arbitrary `InvalidOperationException` | Final source requires the exact exception type/message and checks that each refused overlap leaves the legitimate transaction Active |
| Explorer watchdog aged already completed work while other actors ran | Final source freezes each completion timestamp and measures invocation-to-completion duration; peer activity cannot advance another actor's deadline |
| Explorer cold query only selected the index name | Final source also requires the actual `INDEX SEEK` plan mode |

The final inspected multiprocess source includes the strengthened snapshot,
index, deadline, quiescence and original-error handling. No further blocking
false negative was identified in that bounded source review. The final inspected process source is `0494bd320`; after the observed WAL
confirmation flush it requires the new state, while pre-flush uncertainty permits
only complete old/new outcomes. Author-executed final campaigns and twelve
controls remain attributed to the multiprocess campaign report. Execution
evidence must name the final version rather than substitute an earlier passing
campaign.

## Independent deliberate-fault evidence

The reviewer copied an intermediate explorer source snapshot into isolated
runners; production library code was unchanged. Source snapshots, SHA-256
identities, exact binaries, histories, commands and output are retained under
`explorer-mutations`. These validate oracles, not historical production bugs.

| Deliberate test-only change | Result |
| --- | --- |
| Unchanged schedule 0, Direct, plain, seed 3034134 | Pass; cold state verified |
| Change inserted value without changing acknowledged reference model | Fails `Oracle: cold exact state rows` |
| Suppress semantic boundary notification | Fails the boundary/individual-worker deadline; cannot silently skip the interleaving |
| Permanently stall actor A while actor B repeatedly completes work | Fails `Worker stalled: A/stalled` at approximately 15 seconds |

Each adverse runner exits 2; the unchanged runner exits 0. The mutation campaign
used the intermediate reusable explorer source, not the later expanded schedule
set; unchanged/shared components can be compared through the retained source
hashes. It is not a claim that every final schedule was mutation-tested.

The process campaign also supplies focused oracle/progress tests for partial
transactions, lost acknowledged effects, changed payloads/indexes/sentinels,
complete unknown outcomes, missing native markers and individually stalled
actors. Those are author-executed tests, separately reported in
[the multiprocess campaign report](pr133-multiprocess-campaign.md); they must not
be relabeled as independently executed reviewer evidence.

## Final reviewer source and control qualification

The replacement reviewer inspected explorer `c645c51b0` (including the strict
refusal correction from `dc305`), process campaign `0494bd320`, and the C06/C22
cleanup correction in the author's worktree. The explorer has 68 default theory
rows and 272 rows with `LITEDB_EXPLORER_FULL=1`; the final integrated runs are the
integrator's evidence, not executions by this reviewer. No further blocking
false negative was identified within these reviewed boundaries. The confirmed
ordinary Shared callback self-wait remains an unresolved product failure.

The replacement reviewer executed three small standalone controls on Linux,
.NET 8, using unchanged copies of the final `ExplorerSchedule.cs` and
`ExplorerDatabase.cs`. The build references a copied test-hook library; it does
not edit or rebuild production source. Sources, copied library, exact runnable
binaries, command lines, SHA-256 identities, histories and results are retained
under `final-oracle-controls` in this reviewer's evidence bundle.

| Final helper control | Observed result |
| --- | --- |
| Exact overlap refusal plus unrelated and derived exceptions | Exact expected refusal accepted; unrelated `InvalidOperationException` and derived `ObjectDisposedException` propagated |
| Actor completes, then remains idle for 16 seconds while a peer starts later | Pass: completed work does not acquire a false liveness failure |
| Actor A stalls while B repeatedly completes operations | A-specific 15-second deadline detects the stall; B cannot mask it |

These controls exercise the changed helper contracts, not every explorer
schedule. The earlier wrong-value and missing-boundary mutations remain earlier
reviewer evidence, with their original source snapshots and limits. All three
new runners exit zero because they assert the intended oracle behavior; the
stalled-actor runner succeeds only after observing the exact expected timeout.
The final cold index-plan assertion was reviewed in source and is exercised by
the integrator's complete schedule matrix; no independent index-plan mutation
is claimed here.

The C06/C22 cleanup correction does not manufacture a cold-check pass after a
scenario failure. It preserves the original fixture and diagnostic path when
quiescence or ownership is uncertain, attaches cleanup errors to the original
failure, and avoids deleting retained files through `TempFile` finalization.
This is failure-evidence preservation, not a product fix. The earlier proposed
same-namespace callback admission correction remains a separate production
change requiring its own failing-before/passing-after proof.

## Remaining boundaries and limits

- The original `ConcurrentFuzzer` still accepts any `LiteException` as a unique
  contender's loss and uses unbounded `Task.WhenAll` in places. Its green result
  alone is not evidence for the stricter new exception/progress contracts.
- Existing artifact-budget enforcement may delete database files after failing
  a run that exceeds its budget. Bounded campaigns avoid that path; arbitrary
  budget-exhaustion retention is not established by these tests.
- The process campaign models abrupt process death with OS caches surviving.
  Modeled power loss remains the existing separate target; no device power loss
  or OS-reset durability claim is made here.
- `./` filename aliases do not cover symlink/hardlink, case-folding or every
  Windows/Darwin native admission behavior. Existing platform tests remain
  necessary; this local review adds no cross-platform execution claim.
- Before-reader-dispose and before-session-dispose cuts are not interruption
  inside every cleanup instruction. Killing a reader verifies surviving process
  progress and persisted state, not the killed reader's later enumeration.
- The explorer checks bounded explicit schedules and permitted transaction
  histories, not unrestricted linearizability or general snapshot isolation.
- Final integrated source SHA, exact discovery/results and hosted-CI outcomes
  belong to [the audit report](pr133-concurrency-audit.md). Baseline green CI
  does not qualify the new tests, and known-defect failures remain failures.
