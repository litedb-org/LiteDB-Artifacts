# LiteDB fuzz result: transaction-interleavings

- Status: **FAIL**
- Seed: `233098`
- Steps: `240` / requested `272`
- Duration: `100.758s`
- Git SHA: `f9487f81421a378cd50e8cae9b6112b5fd3be291`
- Working tree dirty: `True`
- Environment: `Microsoft Windows 10.0.26100`, `.NET 10.0.12`, `X64`
- Culture/timezone: `en-US` / `UTC`
- Failure ID: `System.InvalidOperationException`
- Input SHA-256: `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`
- Trace SHA-256: `86A3EA369DD0E8BF868171166E088CBCE6B4672A1A9ED4141888C189777F1EAE`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net10.0 -- --replay __CI_WORKSPACE__\artifacts\concurrency\20260930-195406-transaction-interleavings-s233098-w0-rbf098316\replay.json`

## Metrics

- novelStates: `239`
- completedActorSchedules: `239`

## Failure

```
System.InvalidOperationException: Oracle: ordinary same-file callback entered native self-wait (candidate merge blocker)
   at LiteDB.ConcurrencyTesting.ExplorerDatabase.Require(Boolean condition, String message) in __CI_WORKSPACE__\LiteDB.Tests\Engine\ConcurrencyExplorer\ExplorerDatabase.cs:line 40
   at LiteDB.ConcurrencyTesting.ExplorerOrdinaryCallbacks.Run(ExplorerSchedule schedule, ExplorerDatabase model, ExplorerDatabase other, List`1 resources, Actor actor, Actor cleanup, Int32 variant) in __CI_WORKSPACE__\LiteDB.Tests\Engine\ConcurrencyExplorer\ExplorerOrdinaryCallbacks.cs:line 78
   at LiteDB.ConcurrencyTesting.TransactionInterleavingExplorer.Run(String file, Boolean shared, Boolean encrypted, Int32 schedule, Int32 seed) in __CI_WORKSPACE__\LiteDB.Tests\Engine\ConcurrencyExplorer\TransactionInterleavingExplorer.cs:line 51
--- End of stack trace from previous location ---
   at LiteDB.ConcurrencyTesting.TransactionInterleavingExplorer.Run(String file, Boolean shared, Boolean encrypted, Int32 schedule, Int32 seed) in __CI_WORKSPACE__\LiteDB.Tests\Engine\ConcurrencyExplorer\TransactionInterleavingExplorer.cs:line 99
   at LiteDB.Fuzz.Targets.TransactionInterleavingFuzzer.RunAsync(FuzzContext context) in __CI_WORKSPACE__\LiteDB.Fuzz\Targets\TransactionInterleavingFuzzer.cs:line 24
   at LiteDB.Fuzz.Program.<>c__DisplayClass2_0.<RunAsync>b__0() in __CI_WORKSPACE__\LiteDB.Fuzz\Program.cs:line 124
   at System.Threading.Tasks.Task`1.InnerInvoke()
   at System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(Thread threadPoolThread, ExecutionContext executionContext, ContextCallback callback, Object state)
--- End of stack trace from previous location ---
   at System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(Thread threadPoolThread, ExecutionContext executionContext, ContextCallback callback, Object state)
   at System.Threading.Tasks.Task.ExecuteWithThreadLocal(Task& currentTaskSlot, Thread threadPoolThread)
--- End of stack trace from previous location ---
   at LiteDB.Fuzz.Program.RunAsync(IFuzzTarget target, FuzzOptions options, Int32 worker) in __CI_WORKSPACE__\LiteDB.Fuzz\Program.cs:line 124
```
