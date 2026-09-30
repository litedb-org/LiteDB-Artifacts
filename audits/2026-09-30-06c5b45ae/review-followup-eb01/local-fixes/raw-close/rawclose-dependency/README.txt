Candidate e584847ed80b3f030a21ccafcb07733c240f9cfc, based on eb01f346eb7d8d51925a45c2f87ef40e1c3984ee.

Regression guard fails on real eb01f346e (test file overlaid only), net8 and net10:
"Close must reject fresh work before waiting for an active callback that needs it."
See before-net8.trx and before-net10.trx. The candidate contains the same callback
regression plus a second pending-work wakeup guard.

Final boundary suites: 25 passed, 1 existing Rebuild_Change_Culture_Error skip each
on net8/net10, Release TestingEnabled=true. Filter:
FullyQualifiedName~TransactionHandleRawCloseDependency_Tests|FullyQualifiedName~TransactionHandleMaintenanceProgress_Tests|FullyQualifiedName~TransactionHandleReaderOperation_Tests|FullyQualifiedName~TransactionHandleRawEngine_Tests|FullyQualifiedName~TransactionCompletion_Tests|FullyQualifiedName~Rebuild_Tests

Black-box ReproRunner package is real eb01, packed by root in
__WORKSPACE__/temporary/pr133-eb01-knownbad-feed. Package runner exits0 BUG_REPRODUCED; latest runner
exits10 VERIFIED_FIXED. Latest source build used TestingEnabled=true to match this
worktree's test assemblies; no hooks/reflection/internal engine APIs in the repro.
Both exact runner layouts retained here, with LiteDB assembly hashes.

Same public scenario against pre-PR base49c327cf1 production net10 assembly exits10
VERIFIED_FIXED; explicit baseline runner layout is ../rawclose-base/bin/Release/net10.0.
It has truthful baseline metadata (not reported as eb01 package).

Raw-close limitation remains: arbitrary application callbacks that never return
can hold Dispose open. Ordinary LiteDatabase session Dispose has its separate10s
bound. Fix rejects new independent engine calls rather than queueing behind a
close waiting for an active callback; Rebuild still queues independent work.
