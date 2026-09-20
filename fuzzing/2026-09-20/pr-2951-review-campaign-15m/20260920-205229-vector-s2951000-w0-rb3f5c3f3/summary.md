# LiteDB fuzz result: vector

- Status: **FAIL**
- Seed: `2951000`
- Steps: `404` / requested `100`
- Duration: `51.146s`
- Git SHA: `2f36856b46745e69bf39208299ad1ee4aaa3a979`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/fuzz-pr2951-review-20260920/20260920-205229-vector-s2951000-w0-rb3f5c3f3/replay.json`

## Metrics

- integrityPages: `7`

## Failure

```
LiteDB.Fuzz.FuzzFailureException: Collection 1 has orphaned vector nodes.
   at LiteDB.Fuzz.FuzzContext.Check(Boolean condition, String message) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/FuzzContext.cs:line 63
   at LiteDB.Fuzz.DatabaseVectorIntegrityVerifier.Verify(FuzzContext context, CollectionPage collection, Dictionary`2 pages, Dictionary`2 dataBlocks, HashSet`1 pkData) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/DatabaseVectorIntegrityVerifier.cs:line 42
   at LiteDB.Fuzz.DatabaseIntegrityVerifier.ValidateCollection(FuzzContext context, CollectionPage collection, Dictionary`2 pages, Collation collation) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/DatabaseIntegrityVerifier.cs:line 133
   at LiteDB.Fuzz.DatabaseIntegrityVerifier.Verify(FuzzContext context, String filename) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/DatabaseIntegrityVerifier.cs:line 47
   at LiteDB.Fuzz.Targets.VectorFuzzer.Run(FuzzContext context) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Targets/VectorFuzzer.cs:line 69
   at LiteDB.Fuzz.Targets.VectorFuzzer.RunAsync(FuzzContext context) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Targets/VectorFuzzer.cs:line 15
   at LiteDB.Fuzz.Program.<>c__DisplayClass2_0.<RunAsync>b__0() in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Program.cs:line 66
   at System.Threading.Tasks.Task`1.InnerInvoke()
   at System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(Thread threadPoolThread, ExecutionContext executionContext, ContextCallback callback, Object state)
--- End of stack trace from previous location ---
   at System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(Thread threadPoolThread, ExecutionContext executionContext, ContextCallback callback, Object state)
   at System.Threading.Tasks.Task.ExecuteWithThreadLocal(Task& currentTaskSlot, Thread threadPoolThread)
--- End of stack trace from previous location ---
   at LiteDB.Fuzz.Program.RunAsync(IFuzzTarget target, FuzzOptions options, Int32 worker) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Program.cs:line 66
```

- Automatically minimized failing prefix: `371` steps (`minimized-replay.json`).
