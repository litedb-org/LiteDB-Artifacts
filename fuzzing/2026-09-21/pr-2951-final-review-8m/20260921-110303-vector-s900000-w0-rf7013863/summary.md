# LiteDB fuzz result: vector

- Status: **FAIL**
- Seed: `900000`
- Steps: `101` / requested `100`
- Duration: `2.725s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `VECTOR_DATABASEVECTORINTEGRITYVERIFIER_L43`
- Input SHA-256: `F2250316ED3066AEAAEC6281E3BF851C8B9794F3EDF22A0A7A09780DA06528D7`
- Trace SHA-256: `2E66C28919A75EC0FB098BCBA1EFBE171B13FD71362045DCEB585231106D5C39`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110303-vector-s900000-w0-rf7013863/replay.json`

## Metrics

- novelStates: `41`
- explicitVectorPairs: `18`

## Failure

```
LiteDB.Fuzz.FuzzFailureException: Collection 1 has orphaned vector nodes: 0004:24 -> 0003:24
   at LiteDB.Fuzz.FuzzContext.Check(Boolean condition, String message, String file, Int32 line) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/FuzzContext.cs:line 103
   at LiteDB.Fuzz.DatabaseVectorIntegrityVerifier.Verify(FuzzContext context, CollectionPage collection, Dictionary`2 pages, Dictionary`2 dataBlocks, HashSet`1 pkData) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/DatabaseVectorIntegrityVerifier.cs:line 43
   at LiteDB.Fuzz.DatabaseIntegrityVerifier.ValidateCollection(FuzzContext context, CollectionPage collection, Dictionary`2 pages, Collation collation) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/DatabaseIntegrityVerifier.cs:line 150
   at LiteDB.Fuzz.DatabaseIntegrityVerifier.Verify(FuzzContext context, String filename, String password) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/DatabaseIntegrityVerifier.cs:line 40
   at LiteDB.Fuzz.Targets.VectorFuzzer.Run(FuzzContext context) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Targets/VectorFuzzer.cs:line 72
   at LiteDB.Fuzz.Targets.VectorFuzzer.RunAsync(FuzzContext context) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Targets/VectorFuzzer.cs:line 16
   at LiteDB.Fuzz.Program.<>c__DisplayClass2_0.<RunAsync>b__0() in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Program.cs:line 119
   at System.Threading.Tasks.Task`1.InnerInvoke()
   at System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(Thread threadPoolThread, ExecutionContext executionContext, ContextCallback callback, Object state)
--- End of stack trace from previous location ---
   at System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(Thread threadPoolThread, ExecutionContext executionContext, ContextCallback callback, Object state)
   at System.Threading.Tasks.Task.ExecuteWithThreadLocal(Task& currentTaskSlot, Thread threadPoolThread)
--- End of stack trace from previous location ---
   at LiteDB.Fuzz.Program.RunAsync(IFuzzTarget target, FuzzOptions options, Int32 worker) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Program.cs:line 119
```
