# LiteDB fuzz result: rebuild

- Status: **FAIL**
- Seed: `900000`
- Steps: `14` / requested `100`
- Duration: `3.061s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `System.NotImplementedException`
- Input SHA-256: `B6AB2C168BC6DF8B9B1FA012F2B88DEAAAA67EB6A06A1D60B7FF59A3978FEF57`
- Trace SHA-256: `FA33A4497A89C91CB38BBA733DC1CD24AC169EC527CA7B00F5C8515006B960D0`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110303-rebuild-s900000-w0-r5f097fdc/replay.json`

## Metrics

- semanticIndexKeys: `566`
- semanticDocuments: `283`
- semanticIndexes: `30`
- integrityPages: `7`
- integrityPhysicalPages: `7`

## Failure

```
System.NotImplementedException: The method or operation is not implemented.
   at LiteDB.BufferSliceExtensions.ReadIndexKey(BufferSlice buffer, Int32 offset) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB/Utils/Extensions/BufferSliceExtensions.cs:line 221
   at LiteDB.Engine.IndexNode..ctor(IndexPage page, Byte index, BufferSlice segment) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB/Engine/Structures/IndexNode.cs:line 126
   at LiteDB.Engine.IndexPage.GetIndexNode(Byte index) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB/Engine/Pages/IndexPage.cs:line 42
   at LiteDB.Engine.IndexService.GetNode(PageAddress address) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB/Engine/Services/IndexService.cs:line 200
   at LiteDB.Engine.IndexService.FindAll(CollectionIndex index, Int32 order)+MoveNext() in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB/Engine/Services/IndexService.cs:line 373
   at LiteDB.Engine.QueryPipe.SkipNodes(IEnumerable`1 nodes, Int32 offset)+MoveNext() in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB/Engine/Query/Pipeline/QueryPipe.cs:line 93
   at LiteDB.Engine.BasePipe.LoadDocument(IEnumerable`1 nodes)+MoveNext() in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB/Engine/Query/Pipeline/BasePipe.cs:line 36
   at LiteDB.Engine.QueryPipe.Select(IEnumerable`1 source, BsonExpression select)+MoveNext() in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB/Engine/Query/Pipeline/QueryPipe.cs:line 116
   at LiteDB.Engine.QueryExecutor.<>c__DisplayClass12_0.<<ExecuteQuery>g__RunQuery|2>d.MoveNext() in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB/Engine/Query/QueryExecutor.cs:line 154
   at LiteDB.Utils.Extensions.EnumerableExtensions.OnDispose[T](IEnumerable`1 source, Action onDispose)+MoveNext() in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB/Utils/Extensions/EnumerableExtensions.cs:line 13
   at LiteDB.Utils.Extensions.EnumerableExtensions.OnDispose[T](IEnumerable`1 source, Action onDispose)+MoveNext() in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB/Utils/Extensions/EnumerableExtensions.cs:line 13
   at LiteDB.BsonDataReader.Read() in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB/Document/DataReader/BsonDataReader.cs:line 111
   at LiteDB.LiteQueryable`1.ReadDocuments()+MoveNext() in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB/Client/Database/LiteQueryable.cs:line 309
   at LiteDB.LiteQueryable`1.ToDocuments()+MoveNext() in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB/Client/Database/LiteQueryable.cs:line 297
   at System.Linq.Enumerable.SelectEnumerableIterator`2.ToArray()
   at LiteDB.LiteQueryable`1.ToArray() in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB/Client/Database/LiteQueryable.cs:line 353
   at LiteDB.Fuzz.Targets.RebuildFuzzer.Verify(FuzzContext context, LiteDatabase db, SortedDictionary`2 expected) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Targets/RebuildFuzzer.cs:line 115
   at LiteDB.Fuzz.Targets.RebuildFuzzer.ProbeCorruption(FuzzContext context, String file, String password, SortedDictionary`2 expected) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Targets/RebuildFuzzer.cs:line 144
   at LiteDB.Fuzz.Targets.RebuildFuzzer.RunAsync(FuzzContext context) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Targets/RebuildFuzzer.cs:line 45
   at LiteDB.Fuzz.Program.<>c__DisplayClass2_0.<RunAsync>b__0() in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Program.cs:line 119
   at System.Threading.Tasks.Task`1.InnerInvoke()
   at System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(Thread threadPoolThread, ExecutionContext executionContext, ContextCallback callback, Object state)
--- End of stack trace from previous location ---
   at System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(Thread threadPoolThread, ExecutionContext executionContext, ContextCallback callback, Object state)
   at System.Threading.Tasks.Task.ExecuteWithThreadLocal(Task& currentTaskSlot, Thread threadPoolThread)
--- End of stack trace from previous location ---
   at LiteDB.Fuzz.Program.RunAsync(IFuzzTarget target, FuzzOptions options, Int32 worker) in /home/jonas/.codex/worktrees/3c13/LiteDB/LiteDB.Fuzz/Program.cs:line 119
```
