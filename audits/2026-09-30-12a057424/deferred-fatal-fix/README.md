Production candidate: a3863ed2f (based on 1d4097394).

Before evidence runs actual production source from 1d4097394 with only new tests added from a3863ed2f. The four deferred/published-peer cases fail, while two controls for this rollback's own failures pass. before-tests.patch records those new tests. This is an intermediate PR-state regression check, separate from the black-box ReproRunner comparison against original package560529066aed.

Failure model: exception/concurrency. Cold reopen verifies committed rows, selected secondary index and untouched sentinel, excludes uncommitted writes. No power-loss claim.

49 focused tests passed each on net8/net10 against a3863ed2f. Expanded PeerFatalDispose ReproRunner report passes original560 package + fixeda386 production source with strict exit/marker expectations. Followup94fee53b5 only closes caller-owned streams before native cold reopen, correcting macOS harness locking; affected disposal suite reruns are logged as stream-reopen-net*.
