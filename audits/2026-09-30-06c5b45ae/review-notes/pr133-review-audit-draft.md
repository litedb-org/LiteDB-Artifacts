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
became true. This reopens that regression until the deferred-teardown and
check/dispatch race are covered. The same review passed its other bounded controls.

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
