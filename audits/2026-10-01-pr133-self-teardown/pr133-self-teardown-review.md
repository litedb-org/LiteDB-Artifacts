# Shared same-connection teardown reentry

Review [5375063144](https://github.com/JKamsker/LiteDB/pull/133#pullrequestreview-5375063144)
identified two previously uncovered public caller-stream callback paths at
`3abead6dc2eb6413daf5fa81d8faed06cdf4ade7`: a getter can wait for its own close,
and raw disposal can release the writer mutex before the original checkpoint ends.

## Attribution and scope

Production reproductions distinguish the affected revisions:

| Revision | Same-connection getter during retaining-reader close | Raw disposal during that callback |
| --- | --- | --- |
| Upstream dev `023c2b4ba` | Returns, but subsequent access detects a data checksum error | Native writer exclusion is lost inside callback; subsequent checksum error |
| Stacked parent `49c327cf1` | Same observed result | Same observed result |
| PR head `3abead6dc` | Self-dependent wait, independently witnessed and process-isolated | Native writer exclusion is lost; the retained completed case passes cold-state checks |

The upstream no-reentry controls pass plain and encrypted. The corrected upstream
branch passes refusal, native exclusion, later writer progress, and cold indexed
payload/sentinel checks. Published package `6.0.0-prerelease.319` independently
reproduces premature native release. The getter's exact closing-core self-wait
is specific to the PR lifecycle implementation; this does not identify its earliest
introducing commit. No concurrent writer is deliberately run during exposed native
ownership, and no power-loss claim follows from killing a test process.

The pre-existing correction is developed independently on upstream dev and merged
into #133, preserving shared ancestry rather than duplicating a cherry-pick. Most
previous review fixes are different: twenty added proof entries are pinned to
PR-specific commits because their regressions were introduced by this feature's
lifetime changes. Those fixes remain part of the feature and its performance cost.

## Correction

An executing retained-core teardown has an explicit `SharedCallFrames` marker.
Ordinary public calls and raw disposal refuse same-connection callback reentry
before native acquisition, replacement-core creation or ownership mutation.
The marker remains effective when the closing core was already detached from
connection state. Valid ordinary recursion and internal maintenance-exclusive
recursion remain unchanged; an already-disposed raw connection still permits
idempotent disposal.

Covered retained closes include ordinary core retirement, pin-holder retirement,
last-reader/final checkpoint, owned snapshots, abandoned-core retirement and
cleanup of a core whose opening completed but publication failed. This is not a
claim to cover a constructor's internal cleanup before it returns an unpublished
engine. Independent leased/coordinated snapshots are not classified as native
writer-retaining teardown.

## Regression evidence

`SharedSelfCloseCallback_Tests` adds 24 cases: five close routes × two callback
operations × plain/encrypted, plus same-connection ordinary recursion and
other-database controls. It probes the real mutex on a foreign thread both before
and after attempted reentry, inside the original callback. It checks that no new
core was opened, that a subsequent independent writer progresses, and that exact
indexed documents and the sentinel survive repeated cold reopens. Failed original
fixtures are retained and published through the existing post-host collector.

`SharedSelfTeardownRelease` proves the native invariant against the published
package. Its isolated known-bad child exits inside the callback immediately after
the premature acquisition witness; it does not continue the unprotected checkpoint.
`SharedSelfTeardownWait` proves the PR-specific cycle against the actual `3abead6dc`
package: active callback, closing-core exclusive owner equal to the blocked worker,
and independently excluded native acquisition, observed twice. A watchdog timeout
is always a failure, never successful reproduction. Both proofs require positive
controls and exact cold indexed state on the corrected implementation.

The unchanged candidate production tree passed the 54-case close selection on
net8/net10 and both production before/after proofs. Wider final-head CI and exact
artifact identities are recorded in the PR description; local runs are not hosted
qualification. Existing quarantines and previously documented untested states remain.

## Performance disposition

The `3abead6dc` measurements precede this correction and must remain labeled with
that revision. They show ordinary/legacy regressions; green safety checks do not
resolve those costs. Separate proven pre-existing fixes first, then compare
corrected dev, the corrected stacked parent, and the feature with identical
production settings. Removing protections needed by handles would hide feature
cost, not isolate it. Broader lifetime/guard optimization remains a separate,
measured task; this correction introduces no batching, retained core/cache or
native ownership between transactions.
