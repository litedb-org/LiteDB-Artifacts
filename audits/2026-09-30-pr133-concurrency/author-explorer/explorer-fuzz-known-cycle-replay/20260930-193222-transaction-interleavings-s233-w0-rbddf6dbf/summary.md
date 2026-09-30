# LiteDB fuzz result: transaction-interleavings

- Status: **FAIL**
- Seed: `233`
- Steps: `1` / requested `1`
- Duration: `2.640s`
- Git SHA: `e79635a069df6ea0739ae54efb986806ef9b1bbb`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 10.0.11`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `System.InvalidOperationException`
- Input SHA-256: `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`
- Trace SHA-256: `9E16C44D6FA796BB9AB34A0B69682E227BD002BFCFED637C8864CE21E347E595`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net10.0 -- --replay __WORKSPACE__/artifacts_temp/audit-explorer/artifacts_temp/explorer-fuzz-known-cycle-replay/20260930-193222-transaction-interleavings-s233-w0-rbddf6dbf/replay.json`

## Failure

```
System.InvalidOperationException: Oracle: ordinary same-file callback entered native self-wait (candidate merge blocker)
   at LiteDB.ConcurrencyTesting.ExplorerDatabase.Require(Boolean condition, String message) in __WORKSPACE__/artifacts_temp/audit-explorer/LiteDB.Tests/Engine/ConcurrencyExplorer/ExplorerDatabase.cs:line 40
   at LiteDB.ConcurrencyTesting.ExplorerOrdinaryCallbacks.Run(ExplorerSchedule schedule, ExplorerDatabase model, ExplorerDatabase other, List`1 resources, Actor actor, Actor cleanup, Int32 variant) in __WORKSPACE__/artifacts_temp/audit-explorer/LiteDB.Tests/Engine/ConcurrencyExplorer/ExplorerOrdinaryCallbacks.cs:line 75
   at LiteDB.ConcurrencyTesting.TransactionInterleavingExplorer.Run(String file, Boolean shared, Boolean encrypted, Int32 schedule, Int32 seed) in __WORKSPACE__/artifacts_temp/audit-explorer/LiteDB.Tests/Engine/ConcurrencyExplorer/TransactionInterleavingExplorer.cs:line 51
--- End of stack trace from previous location ---
   at LiteDB.ConcurrencyTesting.TransactionInterleavingExplorer.Run(String file, Boolean shared, Boolean encrypted, Int32 schedule, Int32 seed) in __WORKSPACE__/artifacts_temp/audit-explorer/LiteDB.Tests/Engine/ConcurrencyExplorer/TransactionInterleavingExplorer.cs:line 95
   at LiteDB.Fuzz.Targets.TransactionInterleavingFuzzer.RunAsync(FuzzContext context) in __WORKSPACE__/artifacts_temp/audit-explorer/LiteDB.Fuzz/Targets/TransactionInterleavingFuzzer.cs:line 24
   at LiteDB.Fuzz.Program.<>c__DisplayClass2_0.<RunAsync>b__0() in __WORKSPACE__/artifacts_temp/audit-explorer/LiteDB.Fuzz/Program.cs:line 123
   at System.Threading.Tasks.Task`1.InnerInvoke()
   at System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(Thread threadPoolThread, ExecutionContext executionContext, ContextCallback callback, Object state)
--- End of stack trace from previous location ---
   at System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(Thread threadPoolThread, ExecutionContext executionContext, ContextCallback callback, Object state)
   at System.Threading.Tasks.Task.ExecuteWithThreadLocal(Task& currentTaskSlot, Thread threadPoolThread)
--- End of stack trace from previous location ---
   at LiteDB.Fuzz.Program.RunAsync(IFuzzTarget target, FuzzOptions options, Int32 worker) in __WORKSPACE__/artifacts_temp/audit-explorer/LiteDB.Fuzz/Program.cs:line 123
```
