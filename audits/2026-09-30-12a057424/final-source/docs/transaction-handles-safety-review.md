# PR133 integrated safety review

This audit covers transaction/session ownership and Shared wrapper reuse against
stacked parent `49c327cf1926fa300f9eb7477eb4bcb404f75c43`, previous head
`560529066aeda64c24d5cdd32d34a8aca12695ae`, and upstream `dev`
`5dd942a7367c361fadd600be4ce10aace2768b27`. The prior head's green CI and bounded
agent reviews did not detect the subsequently supplied counterexamples. They do
not substitute for the new regression proofs or integrated candidate run.

## Finite gate

Affected contracts: durable-ack, recovery-generation, reader-snapshot,
historical-input, query-meaning. Select ownership admission, maintenance fairness,
cleanup/error isolation, stale scratch cleanup, external-process reuse and settings
refresh. Retain the inherited format/recovery/fuzz/compatibility suites; this change
adds no new format, WAL publication or device-durability protocol.

Completion requires real known-bad/candidate proofs for confirmed review defects,
focused net8/net10 tests, CLR4 reuse coverage, the full hosted platform matrix,
registered test hooks, coverage dispositions, a passing Safety section, Safety
policy, Safety evidence and Regression proof run, and fresh independent review
without an unresolved in-scope counterexample. Performance evidence is separately
revision-specific and cannot replace correctness evidence.

The fault models are exceptions/failed I/O, forced interleavings, abrupt process
death and inherited modeled-power-loss suites. No physical device/VM-reset
campaign is claimed. The strongest counterexample class is a close or pending
maintenance operation that blocks the transaction/cursor continuation required to
drain itself, including fatal-host and concurrent-disposal paths. Dedicated tests
must force those dependencies, not merely wait for a timing threshold.

## Review findings

The supplied review states nine defects but enumerates eight. Each of the eight
is tracked; Rebuild starvation and raw-dispose starvation share an admission root
cause while raw-dispose collection wait is independently tested. No undocumented
ninth defect is assumed.

- Shared legacy owner + blocked ordinary peer during close: preserve native
  exclusion until admitted work unwinds; cancel queued work so drain can finish.
- Pending maintenance + ordinary readers: stop admitting new independent work
  while permitting completion of already admitted transaction/cursor dependencies.
- Raw engine disposal + collection wait: interrupt waiters before draining;
  never dispose services while their active operations still use them.
- Same-caller legacy/pinned/for-update Shared ownership + handle begin: refuse
  before scheduling a holder that needs the caller's native lock.
- Disposed bound reader/enumerator use: throw ObjectDisposedException before the
  transaction execution scope, preserving earlier writes and the active handle.
- Session close racing handle/child disposal: coordinate one cleanup owner without
  masking a user exception or waiting on the same executing callback.
- Peer fatal host failure: disposing an otherwise healthy handle must not rethrow
  another transaction's failure or claim a successful commit.
- Stale sort scratch: writable ownership removes stale scratch while a read-only
  peer cannot delete another reader's active spill.

## Coverage changes

Retain old assertions and workload concurrency. Replace early child-start timing
with observed native wait. Wait for pending begin admission before closing and
require OperationCanceledException. Enable actual worker/wrapper reuse and relevant
settings/failure tests on CLR4, add concurrent Direct handles and capability cases,
and document disposed-facade exception migration. Ledger entries account for
intentional test replacements and strengthened CI evidence; no new quarantine is
an acceptable replacement for these regressions.

The full CI aggregate includes native glibc, macOS Intel, Windows ARM64 and f2fs
qualification in addition to ordinary full test legs. Native legs retain filtered
discovery, candidate/build identity and result artifacts. Existing skips remain
visible under the inherited quarantine policy; scheduled-only fuzz jobs are not
claimed as executed campaigns.

## Additional findings during this pass

A fresh disposal review reproduced the peer-fatal error again while an unrelated
ordinary read delayed core teardown: fatal state was published before IsDisposed
became true. Cleanup now checks the published fatal state while retaining an operation, and
recognizes only the already-published peer exception if failure races rollback.
The deferred-teardown and check/dispatch cases have separate controls requiring
the handle's own IOException and INVALID_DATAFILE_STATE to propagate unchanged. The same review passed its other bounded controls.

The broader process suite exposed two harness failures after cancellable admission
added a parameter: reflection invocations still supplied the old argument count.
The harness now explicitly supplies CancellationToken.None; no original process
assertion or workload was removed. Both failing attempts are retained.

An independent evidence review found a false-fixed route in the Shared close
repro: a timed-out Dispose followed by peer completion could skip cold-state
verification. That outcome now fails the proof; only successful Dispose plus peer
cancellation and indexed cold state can report fixed. Dedicated before/after proof
jobs provide their known-bad package feed; ordinary regression suites retain the
permanent tests rather than trying to restore a local-only package from NuGet.

## Evidence locations and interpretation

The five contract families are mapped to executable methods in
[contracts.json](../.github/safety/contracts.json). The eight review scenarios
and their real known-bad revision `560529066` are registered in
[regression-proofs.json](../.github/safety/regression-proofs.json). Ordinary
suites retain each permanent guard; dedicated proof jobs build the pinned package.
The two inherited published-package proofs remain required when the shared
proof harness changes.

The [PR description](https://github.com/JKamsker/LiteDB/pull/133) links the accepted
candidate's hosted runs and immutable raw evidence, including every prior failed
attempt and its classification. Use those revision-specific results for the
completion decision. The original reuse report contains historical measurements;
new production measurements belong to the review evidence and PR description.

The new safety aggregate checks the actual candidate and binary identities,
expected jobs and legs, discovery versus completed results, runtime guards and
unscheduled tests. Its successful result is required alongside all ordinary,
fuzz and compatibility checks. Existing quarantines remain explicit limits;
this review adds none. Repository protection settings and unrelated global audit
roadmap work are outside this PR; no claim is made that a green advisory check
prevents a maintainer from bypassing it.

Checkpoint CI also exposed caller-owned streams retained by the new fatal-error
tests across a native-admitted cold reopen on macOS and Windows. The test now
closes those streams before reopening. This is a harness ownership correction:
assertions, fault injection, transaction outcomes and cold-state checks remain.

The full integrated suite exposed one upstream-test expectation that predates this
PR's error contract: a failed close checkpoint was expected to return normally.
The updated test now requires the exact injected IOException; all original WAL
retention, committed-state/index recovery and subsequent checkpoint assertions
remain. This is a documented semantic difference, not suppression of a safety
failure. The failing candidate `1eaeaf092` and corrected run are retained.

Windows qualification found a second test-harness portability issue: a raw native
lock probe bypasses engine path normalization, so a lexical `.` alias is not a
valid extended Windows filename. The raw probe now uses the normalized data path,
with both released-before and released-after controls. Public engine and child
admission still use the alias and must reject it while the owner is live; scratch
bytes and committed data remain checked. No production behavior changed.

A final source check found a child-disposal admission race: a call could capture
its inner reader, then enter after disposal and throw inside the transaction abort
handler. Four forced cases failed on actual candidate `3e88454e2`. The fix validates
child lifetime after admission but outside that handler, and publishes child
disposal before releasing admission. Direct/Shared and plain/encrypted reader and
enumerator cases require the transaction to stay active and its earlier writes to
commit and survive two indexed cold reopens. Separate controls still require real
reader and disposal failures to abort. This production change invalidates the
prior benchmark candidate; the PR's final comparison is rerun after this fix.

The next full candidate run (`36691368391`, production revision `3b60d5aea`)
exposed native-suite capacity exhaustion on macOS Intel/.NET 10. Its 300-second
session completed 637 passing cases and one existing platform skip, with results
continuing until 19 ms before the timeout; four discovered methods had not run.
The same candidate's .NET 8 leg completed all 642 cases. This is a CI harness
capacity failure, not passing evidence or an unexplained transaction hang.
The unchanged native selection now runs in three disjoint sessions (handles,
admission, and the remainder), each retaining the 300-second limit and runtime
and hook guards. Discovery checks exact coverage, nonempty workloads and distinct
result files before execution; aggregate evidence requires every result. The
recorder also now matches VSTest's case-insensitive FQN filtering, so lowercase
`rebuild` methods already executed by the old filter are included in truncation
accounting. This harness correction changes no library code or benchmark binary.

### Cross-process pin progress budget

The full `010821392` run timed out in the Windows x86/.NET 8 pin test while
waiting for the child process's final `done` line. That line followed startup and
twenty separately committed inserts. The retained failure does not identify
whether startup, native admission, any insert, or output delivery stopped; it is
not evidence of a particular product deadlock or established runner slowdown.

The test now starts its child before establishing the pin, waits for `ready`,
then releases a `go` barrier after a dedicated owner thread is pinned. That
owner remains alive throughout the proof, so progress cannot be explained by
abandonment of an async test-runner thread. The child reports its first actual
native-admission attempt through the existing `BeforeMainWait` hook and each of
the same twenty ordinary inserts only after it returns. The native marker proves
reaching admission, not by itself that the mutex was occupied at that instant.

This deliberately changes the test from a combined twenty-second startup-plus-
throughput budget to separate bounded startup and write-progress checks. Startup
remains bounded by twenty seconds; the first commit is due within twenty seconds
of `go`, and each later commit within twenty seconds of the preceding commit.
Diagnostic markers do not renew that deadline. A strict sixty-second overall
cap includes startup, writes and child exit; the test-session cap remains 300
seconds. No write, process, platform, assertion, or failure is skipped or retried.
The final parent update remains, and cold reopen additionally checks every ID,
payload, an index-seek result and an unrelated sentinel.

Plain and encrypted negative controls retain a real transaction hold on a live
pin owner. After observing native admission, the same progress checker must
reject the missing first commit. The child is killed before the owner is allowed
to exit, and cold reopen confirms no child writes were admitted. These controls
prevent the progress protocol from treating an indefinitely blocked writer as
success. This correction changes test discrimination and diagnostics only; it
makes no production-code change and does not resolve the original opaque
failure's phase retrospectively.

## Follow-up reviews of 010821392 and eb01f346e

[Review 5364329616](https://github.com/JKamsker/LiteDB/pull/133#pullrequestreview-5364329616)
and [comment 5908951510](https://github.com/JKamsker/LiteDB/pull/133#issuecomment-5908951510)
reopened the gate. Their static findings were tested separately:

- A surfaced pin-core close failure could skip independent Shared cleanup.
  Connection cleanup now preserves the primary error, attaches secondary failures
  and completes reader-registry, cached-child and admission retirement. A legitimate
  reader keeps its lease until it finishes; another writer can progress meanwhile.
  A further forced callback check caught premature native release in an
  intermediate cleanup correction. Raw callback disposal now rejects before the
  disposed transition; a core close that refuses admission stops dependent
  ownership cleanup. Six plain/encrypted paths keep native exclusion until the
  operation returns, then prove subsequent writes and disposal.
  Plain/encrypted guards check exact snapshot and indexed cold state. A black-box
  Linux RLIMIT_FSIZE proof reproduces the lease leak on eb01 using a real OS write
  failure. This is an exception test, not a power-loss test.
- The Shared parent's final checkpoint historically ignores returned close errors
  and expected I/O/access/database exceptions. The migration guide now states that
  precise best-effort contract instead of claiming universal propagation. Separate
  parent-only faults verify retained WAL bytes and committed indexed recovery;
  pin/child errors remain observable after independent cleanup.
- A raw close could fence fresh work awaited by an admitted callback. Close now
  refuses those fresh dependencies while allowing existing work to drain. Rebuild
  retains its waiting semantics. Real eb01 fails; the stacked pre-PR parent does
  not reproduce the public callback scenario.
- An ordinary callback write could wait on the handle executing that callback.
  Collection ownership now records the currently executing context thread solely
  for self-wait detection; idle handles and sequential handoff remain supported.
  Independent review caught admission released before scope restoration in the
  first fix. A real four-thread handoff test detects that intermediate bug; the
  corrected order restores all execution scopes before releasing admission.
- Contended Windows waits now use a single cancellable kernel wait. Its event and
  cancellation registration belong to that wait and are disposed in safe order;
  uncontended waits allocate neither. Unix rejects WaitAny containing a named
  mutex, as an actual runtime probe confirms, so it retains bounded kernel waits
  while holding an acquired turnstile. WaitOne(10) wakes on release; it is not a
  fixed ten-millisecond sleep. The review's performance attribution remains a
  hypothesis, not a measured explanation of the read regression. Tests observe
  entry into the contended path and verify cancellation, release and owner death.

The proposed stale ReadTransform policy is refuted for the supported API:
SharedEngine clones constructor settings, ordinary reads and reused handles retain
that same policy, and mutations of the original delegate target remain visible.
Four replacement/target-state controls agree on the original and current builds.
The hypothesized pre-persist Commit lock leak has no demonstrated reachable path;
open-reader preconditions reject before terminal completion, and explicit retry,
outcome and subsequent-writer controls pass without changing Commit semantics.

ThreadID zero is diagnostic; handle ownership uses context identity. The existing
per-path TransactionWriters metadata registry is not newly introduced by reuse;
its unpruned managed metadata is not claimed to be bounded, but does not retain
native writer ownership or the application graph. Scheduler duplication, unsupported
Drop/Rename API shape and existing encoding are unchanged within this scoped fix.
Unsupported operations retain documented rejection tests. These dispositions do
not turn unrelated cleanup proposals into completed work.

The three confirmed follow-up regressions have separate proofs against actual
`eb01f346eb7d8d51925a45c2f87ef40e1c3984ee`, in addition to the original eight and
two inherited proofs. Final integrated tests, platform evidence and new production
measurements must cover these corrections; eb01 and earlier measurements are
interim evidence. The PR description and pinned artifact report record the
accepted revision and results, rather than treating local review as final CI.

## Follow-up review of e821ae747

Review `5365276527` identified callbacks during later reader advances, after
`Query()` admission has ended. Independent plain/encrypted reproductions confirmed
both counterexamples on the real reviewed commit: refused close of a separate
unleased snapshot released its native mutex, and foreign connection close waited
for a callback while holding the connection lock that callback needed. The prior
single-row callback controls exercised eager first-row materialization and did
not establish safety for this later phase; they remain alongside the new cases.

The correction includes every mutex-dependent core in the callback-close preflight,
keeps those cores and pin ownership published until teardown completes, and drains
outside connection bookkeeping locks. A thrown pre-teardown refusal must preserve
native ownership; a returned cleanup-error list follows completed teardown and
keeps the existing error contract. Genuinely leased snapshots retain their separate
lifetime. Owner-exit and handed-off-reader variants exercise the same dependency
without a disposed facade, including callers that trigger owner retirement
rather than arriving after it starts. Valid own-pin callbacks and harmless raw
rollback after disposal remain controls.

Two public-API proofs pin actual `e821ae747`, with no internal hooks or reflection.
`Issue_133_SharedSnapshotCloseRefusal` uses a real reader-registry filesystem
obstacle, a different caller's bounded writer admission, leased controls, and
plain/encrypted indexed cold-state checks.
`Issue_3067_SharedLateReaderClose` requires the later-row callback and both sides
of the blocked close dependency, then requires the corrected run to finish,
permit an external writer process, exclude uncommitted rows, and preserve exact
records/indexes/sentinel. A setup error or generic process timeout cannot satisfy
either known-bad proof. Both dedicated-feed comparisons and matching permanent
suite guards are registered; their ordinary repro substitutions are checked.

The e821 hosted matrix also found an older open-failure test expecting callback
`Dispose()` to succeed. Its replacement explicitly requires the documented
refusal, retains incompatible-writer exclusion, preserves the exact injected
IOException, then disposes outside the callback and verifies all references retire.
Plain/encrypted indexed cold-state and unrelated-sentinel controls remain. This
is an intentional exception-contract update recorded in the coverage ledger;
failed hosted attempts are retained rather than reclassified as successful.

The later comment `5910144864` accepts the earlier pin-close, checkpoint-contract,
Windows cancellation-wait and callback-scope resolutions. Idle pin-core errors
may surface from the last ordinary reader's `Dispose()`; the migration guide now
states that path explicitly. Revision-specific evidence, subsequent review
findings and the current completion status remain in the PR and immutable audit.

The facade variant in comment `5910144864` was independently reproduced: a late
ordinary reader holds a core operation but no session-call lease, so an owning
facade previously started shutdown and timed out waiting for its own callback.
A facade preflight now refuses that self-dependent close before session state
changes. It does not add a read-path lease or change the close scheduler. Pooled
Direct, genuinely leased Shared and non-owning facade controls still permit
independent disposal; foreign-thread close still drains correctly.
`Issue_3067_LateCallbackClose` pins the actual reviewed commit and requires the
specific old timeout versus the corrected refusal and usable facade, followed by
peer progress and exact cold state.

Independent review of the first drain correction also caught a fresh-operation
race with off-thread last-reader disposal. The original head passed that forced
interleaving while the intermediate correction failed. Core retirement now fences
new admissions until the old core is retired without holding the callback-needed
lock during drain. The actual intermediate failure and passing controls remain
retained. Strict proof validation also caught an empty-password/plain mismatch;
empty password enables encryption. Corrected proofs use null for truly plain
databases on every local/peer open, with the earlier harness failures preserved.

### Coordination allocation qualification

The e821 Linux ARM64/.NET 10.0.12 run reported 6,056 allocated bytes in the
steady-publication test. That production path was unchanged by this PR. The
original class passed on local x64 .NET 10.0.11 and 10.0.12; these do not reproduce
or explain the ARM64 result. Its cause remains unknown.

The corrected measurement warms its complete counter boundary on a dedicated
worker without xUnit execution context, retains the original 100 warmup and
1,000 measured publications, and still requires exactly zero bytes. No measured
window is retried or discarded. Publication counters and unchanged epoch identity
are checked before the allocation assertion. An escaped allocation control is
required to register allocations; inserting it into the actual zero-budget test
failed at 24,000 bytes. Independent review confirmed those boundaries. The new
class passes on the local runtimes, but successor ARM64 CI remains required.
This is a measurement-hardening change, not a proven explanation for the old
6,056-byte observation or an allocation waiver.

## Late callback close test budget

The callback self-drain regression keeps its 100ms close probe for raw owning
Direct, unleased Shared, and Shared writer readers. Its required outcome remains
`InvalidOperationException` before session closing, followed by successful writes.
The fixture restores the prior timeout in `finally` before normal teardown.
Independently retained and non-owning reader controls perform legitimate close
and use the default deadline throughout. No production close limit changes.

Windows x64 net8 job 109871740658 failed at the outer `using` disposal on the
19065cc1e candidate, after the callback/refusal/write assertions passed. The case
took 325ms overall; the specific scheduling or drain duration is unknown. This
correction removes an accidental 100ms normal-cleanup budget, without asserting a
production timing cause. The revised test still rejects real e821 source in all
six plain/encrypted self-dependent callback cases.

## Ordinary reader cleanup dependencies

Review5366098967 identified two additional paths on f95f0b0cd. Last-leased-reader
retirement could join a forced pin whose core was draining that same callback;
the non-pin owner-exit path could similarly wait before an optional checkpoint.
Actual f95 reproductions cover both plain/encrypted paths, a nonempty committed
WAL, native exclusion before callback unwind, and indexed cold recovery without
uncommitted writes. Retirement now requests release as before but skips a dependent
join; the existing concurrent connection disposer still reports the exact pin-close
failure. Non-dependent last-reader disposal continues to join normally. No core
drain or native-ownership lifetime is shortened, and no new cleanup scheduler is
introduced. The optional checkpoint leaves the authoritative WAL for later recovery.

A leased reader's self-disposal could close its cursor and latch disposal before
snapshot-core close refused the executing callback, permanently preventing retry.
Both leased snapshot construction paths now provide the owned core to a disposal
preflight before mutation. Plain/encrypted tests retain the reader strongly, retry
after callback unwind, assert actual core closure and acquire fresh Direct admission.
Normal leased and existing unleased controls retain their behavior. Actual f95
fails all four leased leak cases; independent review also uses an alternate
QueryCore setup with a retained FOR UPDATE reader.

One draft non-dependent pin test probed native ownership with an immediate zero
wait after an optional checkpoint had posted its asynchronous release. Its net8
encrypted control failed that probe. The final guard keeps zero-wait exclusion
while the callback is blocked, then permits a bounded three-second native acquire
after completion. It still requires actual acquisition and cold-state checks;
there is no fixed sleep or assertion retry. The original local failure is retained.

The two further comparisons are registered against actual f95f0b0cd as
`Issue_3067_SharedReaderRetirement` and `Issue_3067_LeasedReaderSelfDispose`,
bringing the dedicated proof set to 18. The public forced-pin proof requires a
bounded setup window shorter than the pin's minimum idle lifetime and records its
measured setup; slow setup is refused, never called fixed. Permanent tests force
the core-drain boundary directly and also cover owner exit. Exact public proof
binaries, independent controls and repeated attempts remain revision-specific.

## Native crash fixture capture

Windows ARM64/net8 job109884239520 failed a strict raw read of the recovery marker
after child death at the before-marker-flush boundary, encrypted with fallback.
The initial `File.ReadAllBytes` snapshot failed after awaited child Kill/Dispose,
before any Direct/Shared recovery reopen or replacement copy. Its 847ms result
did not establish the sharing-lock owner. The original fixture
was missing from the uploaded artifact: the test used a diagnostic variable which
native CI did not set, and the configured collector accepted individual GUID
basenames rather than this test's GUID directory. That confirmed capture defect
is corrected with typed directory manifests and post-host copies, refusing live
hosts and children. Actual deliberately failing crash tests verify all 24 fixture
sets and 20 recovery markers byte-for-byte, alongside existing graph retention.

The original Kill/Dispose to strict recovery-verification sequence and raw byte
assertions remain. Native-authority observations run only after failure and cannot
replace the primary exception or suppress fixture publication. Interim probes on
the success path were removed because their extra I/O could alter reproduction
timing. No retry, delay, threshold relaxation or production fix is used here.
The old fixture cannot be reconstructed and the original lock owner remains
unknown; a later passing run must not be described as explaining that failure.

## Pinned writer progress observation

Windows x64/net8 job109884239743 timed out waiting for the Shared follow-up
writer's final output. The original test had no child-ready or native-wait
boundary, so the failing phase cannot be reconstructed. This is distinct from
the earlier SharedReaderPin fixture failure; no common cause is established.

The revised fixture initializes the writer before establishing the pin and
observes native admission followed by all twenty separately committed records
within the unchanged ten-second post-go budget. The owner retains its active
update loop and one-minute hold settings. Its process must remain alive before
intentional termination, so a crash cannot masquerade as successful yielding.
Plain/encrypted held-legacy-pin controls require a timeout before the first
commit, then verify no attempted write survived; successful cases verify exact
cold indexed records and an unrelated sentinel. Failure diagnostics preserve the
last observed phase, child output and original exception, with database files
retained on their original temporary volume for post-host collection.

Independent review found and corrected the owner-exit false-pass risk and a
failure-capture gap when stopping a child itself fails. Collection must refuse
any still-live recorded child as well as a live test host. The old timeout remains
unclassified; these changes improve its oracle and evidence, not production
behavior.
