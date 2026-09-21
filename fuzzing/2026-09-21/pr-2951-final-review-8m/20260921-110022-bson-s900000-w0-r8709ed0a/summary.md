# LiteDB fuzz result: bson

- Status: **FAIL**
- Seed: `900000`
- Steps: `5` / requested `100`
- Duration: `0.424s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `BSON_INTERNAL_MALFORMED_FAILURE`
- Input SHA-256: `C3C1D5B75068BFA8E069CDA6C51E9060AB27358897699A27981257173C07FC1D`
- Trace SHA-256: `337D30B2D8C59758A079844AEA2BAB1ADEF98E40EBBE6A629983A7D633742ACA`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110022-bson-s900000-w0-r8709ed0a/replay.json`

## Failure

```
LiteDB.Fuzz.FuzzFailureException: Malformed BSON escaped through internal exception System.OverflowException: System.OverflowException: Arithmetic operation resulted in an overflow.
   at LiteDB.Result`1.GetValue() in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB/Utils/Result.cs:line 24
   at LiteDB.Fuzz.Targets.BsonFuzzer.TryReadMutation(FuzzContext context, Byte[] bytes) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Targets/BsonFuzzer.cs:line 137
   at LiteDB.Fuzz.Targets.BsonFuzzer.TryReadMutation(FuzzContext context, Byte[] bytes) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Targets/BsonFuzzer.cs:line 148
   at LiteDB.Fuzz.Targets.BsonFuzzer.RunAsync(FuzzContext context) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Targets/BsonFuzzer.cs:line 46
   at LiteDB.Fuzz.Program.<>c__DisplayClass2_0.<RunAsync>b__0() in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Program.cs:line 119
   at System.Threading.Tasks.Task`1.InnerInvoke()
   at System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(Thread threadPoolThread, ExecutionContext executionContext, ContextCallback callback, Object state)
--- End of stack trace from previous location ---
   at System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(Thread threadPoolThread, ExecutionContext executionContext, ContextCallback callback, Object state)
   at System.Threading.Tasks.Task.ExecuteWithThreadLocal(Task& currentTaskSlot, Thread threadPoolThread)
--- End of stack trace from previous location ---
   at LiteDB.Fuzz.Program.RunAsync(IFuzzTarget target, FuzzOptions options, Int32 worker) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Program.cs:line 119
```
