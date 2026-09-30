Test-only candidate303c153a9, based on19065cc1e; LiteDB production tree unchanged.
Hosted original failure: Windowslatestx64net8 job109871740658, candidate19065 (merge2a15beace59345d1daaa8cdbc887cfe67b5d89ff), mode0 plain, outerDispose line58, 325ms overall. Callback/refusal/post-refusal write assertions already passed. Specific scheduling/drain duration remains unknown.
Fix:100ms override applies only within unsafe callback modes0/4/5 and previous override is restored in finally. Legitimately closing safe controls modes1/2/3 use default timeout throughout; outer cleanup uses default.
Final exact test source:18/18 net8 and18/18 net10.
Real negative control:e821ae7479dcb83570031180b0c0e3f4fd167793 plus exact revised test source.6 failures/6 passes each runtime; all unsafe cases fail expected InvalidOperationException versus actual TimeoutException at callback assertion line62. Safe controls pass. No mutant or test hook replacement.
Commands:dotnet test LiteDB.Tests/LiteDB.Tests.csproj -f net8.0|net10.0 -c Release -p:TestingEnabled=true --settings tests.runsettings --filter FullyQualifiedName~TransactionHandleLateCallbackClose_Tests (candidate); --filter FullyQualifiedName~Late_callback_close_refuses (real negative).
Coverage gate passes; it warns body-only fixture edits don't match its classification. Explicit intentional-change ledger retained anyway.
