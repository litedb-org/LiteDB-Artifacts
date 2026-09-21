# LiteDB fuzz result: vector

- Status: **FAIL**
- Seed: `2975100`
- Steps: `101` / requested `101`
- Duration: `8.590s`
- Git SHA: `ecacf8d719ad578c53d6eb65b99134c5533df85b`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `VECTOR_DATABASEVECTORINTEGRITYVERIFIER_L43`
- Input SHA-256: `A35F6514895885F0981BCA33B3BDFCB738E6ADA51B5398E74C4AB0D4BE3431E4`
- Trace SHA-256: `9D3023B908333E5AA1342189BA7A407E0F0E705CE748C80E6B40C2CAB54C47B1`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /tmp/litedb-fuzz-rewrite/artifacts_temp/fuzz-vector-replay/20260921-151201-vector-s2975100-w0-r902cf525/replay.json`

## Metrics

- novelStates: `48`
- explicitVectorPairs: `18`

## Failure

```
LiteDB.Fuzz.FuzzFailureException: Collection 1 has orphaned vector nodes: 0004:02 -> 0003:02, 0004:08 -> 0003:08, 0004:35 -> 0003:34
   at LiteDB.Fuzz.FuzzContext.Check(Boolean condition, String message, String file, Int32 line) in /tmp/litedb-fuzz-rewrite/LiteDB.Fuzz/FuzzContext.cs:line 103
   at LiteDB.Fuzz.DatabaseVectorIntegrityVerifier.Verify(FuzzContext context, CollectionPage collection, Dictionary`2 pages, Dictionary`2 dataBlocks, HashSet`1 pkData) in /tmp/litedb-fuzz-rewrite/LiteDB.Fuzz/DatabaseVectorIntegrityVerifier.cs:line 43
   at LiteDB.Fuzz.DatabaseIntegrityVerifier.ValidateCollection(FuzzContext context, CollectionPage collection, Dictionary`2 pages, Collation collation) in /tmp/litedb-fuzz-rewrite/LiteDB.Fuzz/DatabaseIntegrityVerifier.cs:line 150
   at LiteDB.Fuzz.DatabaseIntegrityVerifier.Verify(FuzzContext context, String filename, String password) in /tmp/litedb-fuzz-rewrite/LiteDB.Fuzz/DatabaseIntegrityVerifier.cs:line 40
   at LiteDB.Fuzz.Targets.VectorFuzzer.Run(FuzzContext context) in /tmp/litedb-fuzz-rewrite/LiteDB.Fuzz/Targets/VectorFuzzer.cs:line 72
   at LiteDB.Fuzz.Targets.VectorFuzzer.RunAsync(FuzzContext context) in /tmp/litedb-fuzz-rewrite/LiteDB.Fuzz/Targets/VectorFuzzer.cs:line 16
   at LiteDB.Fuzz.Program.<>c__DisplayClass2_0.<RunAsync>b__0() in /tmp/litedb-fuzz-rewrite/LiteDB.Fuzz/Program.cs:line 119
   at System.Threading.Tasks.Task`1.InnerInvoke()
   at System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(Thread threadPoolThread, ExecutionContext executionContext, ContextCallback callback, Object state)
--- End of stack trace from previous location ---
   at System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(Thread threadPoolThread, ExecutionContext executionContext, ContextCallback callback, Object state)
   at System.Threading.Tasks.Task.ExecuteWithThreadLocal(Task& currentTaskSlot, Thread threadPoolThread)
--- End of stack trace from previous location ---
   at LiteDB.Fuzz.Program.RunAsync(IFuzzTarget target, FuzzOptions options, Int32 worker) in /tmp/litedb-fuzz-rewrite/LiteDB.Fuzz/Program.cs:line 119
```

- Automatically minimized failing prefix: `76` steps (`minimized-replay.json`).
