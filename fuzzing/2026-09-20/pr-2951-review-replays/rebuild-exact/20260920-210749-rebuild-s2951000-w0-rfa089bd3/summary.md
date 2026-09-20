# LiteDB fuzz result: rebuild

- Status: **FAIL**
- Seed: `2951000`
- Steps: `63` / requested `63`
- Duration: `53.073s`
- Git SHA: `2f36856b46745e69bf39208299ad1ee4aaa3a979`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/replay-rebuild-2951000/20260920-210749-rebuild-s2951000-w0-rfa089bd3/replay.json`

## Metrics

- integrityPages: `12`

## Failure

```
System.IndexOutOfRangeException: Index was outside the bounds of the array.
   at LiteDB.Fuzz.Targets.RebuildFuzzer.Verify(FuzzContext context, LiteDatabase db, SortedDictionary`2 expected) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Targets/RebuildFuzzer.cs:line 118
   at LiteDB.Fuzz.Targets.RebuildFuzzer.ProbeCorruption(FuzzContext context, String file, String password, SortedDictionary`2 expected) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Targets/RebuildFuzzer.cs:line 147
   at LiteDB.Fuzz.Targets.RebuildFuzzer.RunAsync(FuzzContext context) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Targets/RebuildFuzzer.cs:line 48
   at LiteDB.Fuzz.Program.<>c__DisplayClass2_0.<RunAsync>b__0() in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Program.cs:line 66
   at System.Threading.Tasks.Task`1.InnerInvoke()
   at System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(Thread threadPoolThread, ExecutionContext executionContext, ContextCallback callback, Object state)
--- End of stack trace from previous location ---
   at System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(Thread threadPoolThread, ExecutionContext executionContext, ContextCallback callback, Object state)
   at System.Threading.Tasks.Task.ExecuteWithThreadLocal(Task& currentTaskSlot, Thread threadPoolThread)
--- End of stack trace from previous location ---
   at LiteDB.Fuzz.Program.RunAsync(IFuzzTarget target, FuzzOptions options, Int32 worker) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Program.cs:line 66
```

- Automatically minimized failing prefix: `63` steps (`minimized-replay.json`).
