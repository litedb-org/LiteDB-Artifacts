Baseline: e821ae7479dcb83570031180b0c0e3f4fd167793 plus new test file only.
Baseline filter Late_callback_close_refuses: net8 6 failures / 6 successes. All failures exact TimeoutException instead of InvalidOperationException for raw owning Direct / unleased Shared / Shared FOR UPDATE late ReadTransform callbacks, plain and encrypted.
Candidate: 0891c35bc (contains dependencies cherry-picked from 74c04d0c2 and 99cd5b287).
Focused after suite: 52/52 net8 and 52/52 net10, Release TestingEnabled=true tests.runsettings.
Filter: FullyQualifiedName~TransactionHandleLateCallbackClose_Tests|FullyQualifiedName~SharedLateReaderClose_Tests|FullyQualifiedName~SharedCallbackClose_Tests|FullyQualifiedName~TransactionHandleLifetime_Tests|FullyQualifiedName~TransactionHandleCloseCleanup_Tests
New tests: 12 same-thread callback cases plus6 cross-thread drain controls, both plain/encrypted. Cold reopen checks all expected indexed rows and unrelated sentinel. Safe control modes are non-owning raw facade, pooled Direct facade, independent Shared lease. No changed test hooks or narrowed assertions.
Standalone public proof delegated to fix_maintenance; do not confuse the shortened unit CloseWaitOverride with production ten-second callback timeout behavior.
