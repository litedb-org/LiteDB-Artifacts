# Independent Shared admission review

Reviewed baseline: 569ba13c3b3867131c8687e7884bed65131edfbf, Release production net10.0 assembly from safety-production. No tracked code edits.

Source confirms both review5368851720 acquisition gaps. Executed eight additional bounded public-API admission cases: ordinary pinned input callback and transferred ForUpdate reader callback, each same/peer facade and plain/encrypted. All reached TimeoutException at 500-504 ms rather than rejecting the impossible synchronous dependency. All outer operations could complete after caught timeout; cold reopen retained original rows and outer ordinary writes. This is liveness evidence, not acknowledged commit loss or a measurement of infinite deadlock.

The pin cases assert actual local leased-reader registration, initially absent pin, then pin operations=1/holds=0 in callback. The transferred-reader cases execute two Reads on the foreign thread; the first consumes Query-prefetch and would be vacuous. The second invokes exactly one late ReadTransform callback.

A parent-only IsExecutingOwnedCoreOnCurrentThread guard does not fix peer-facade same-namespace callbacks. This was reproduced and communicated to the implementer before integration.

Reviewed F1 d6f0e7f8e source and 14 new cases: common WriteDatabase guard precedes existing-pin entry/start/retirement; no source defect found. Core/holder/native semantics are unchanged. Ordinary other-database pin, normal pin and independent idle-handle completion controls discriminate over-rejection.

Pending: inspect F2 candidate and combined final source/test evidence. Suggested controls: both mutex-backed SharedDataReader constructors, Query-prefetch before fallback snapshot publication, transferred second-Read callback count, truly leased independent callback, nested other-database composition, idle ownership releasable by another thread. Thread-static scopes must remove all engine references in finally and must not follow Task execution context.
