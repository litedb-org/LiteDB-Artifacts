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
