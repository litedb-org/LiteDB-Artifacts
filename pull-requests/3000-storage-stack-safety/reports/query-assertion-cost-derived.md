Derived copy of the retained measurement report. Original local archive references are contextual; compiled files and raw logs are not published. Exact source digest is in source-provenance.json.

# Randomized query assertion cost

Parent source dcd74f24e; isolated candidate 4302a0a2e7bf8681e87884a536eae0d644235d78.
This is a bounded test-harness investigation using Release, TestingEnabled=true assemblies.
It is not a production engine performance benchmark and does not explain the historical Windows
.NET 8 x86 148-second outlier. Local host is Linux x64, DOTNET_PROCESSOR_COUNT=2.
Each recorded case ran once in a fresh process, sequentially; no statistical confidence claim.
Exact referenced DLL hashes, copied/instrumented source and raw measurements are retained here.

| Runtime/case | Original total | Original ID comparison | Original comparison allocation | Guarded sorted total | Guarded sorted comparison | Guarded comparison allocation |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| .NET 8.0.30 union | 11.171 s | 10.629 s | 8,994,282,648 B | 0.676 s | 0.0277 s | 3,323,496 B |
| .NET 8.0.30 nested IgnoreCase | 10.019 s | 9.507 s | 8,204,721,880 B | 0.698 s | 0.0256 s | 2,434,824 B |
| .NET 10.0.11 union | 8.769 s | 8.212 s | 8,366,766,248 B | 0.687 s | 0.0256 s | 3,324,824 B |

Allocation figures are cumulative bytes allocated on the measured thread, not peak/live memory.
The unmodified union spent about 95% of elapsed time in BeEquivalentTo. Expression creation plus
scalar-oracle evaluation cost 0.241 s; plan/query execution cost 0.104 s. Merely projecting AsInt32
while retaining BeEquivalentTo still spent 9.620 s and 7.913 GB in union comparison. Therefore the
identified cost is the unordered assertion path; this is not evidence of deep BSON member traversal
or slow engine execution. BsonValue already implements numeric value equality.

Guarded candidate cases also passed for nested en-US/None on .NET 8 (0.606 s total), and nested
IgnoreCase/None on .NET 10 (0.663/0.690 s total). No original timing was collected for those three
additional combinations; do not derive an unmeasured before/after speedup.

The candidate materializes both ID sequences, requires BSON Int32 IDs, sorts each scalar sequence,
and compares sequences. This preserves unordered membership, cardinality and multiplicity for the
explicit Int32 fixture IDs. Only two assertion calls change. Seeds 410/430, 150 union rounds,
100 nested rounds for each culture, both query directions, all mixed BSON Value/collation inputs,
scalar predicate evaluation, query-plan checks and separate Value ordering assertions remain.

Semantic controls:
- Reordering and matching duplicates pass; missing/extra/wrong members fail both original/candidate.
- Actual [1,2,2] vs expected [1,1,2] fails both, despite equal length and equal distinct members.
  The isolated counterexample confirms HashSet plus Count would incorrectly pass.
- String "1" and fractional 1.4 convert to Int32 1: bare scalar comparison incorrectly passes,
  while original BSON comparison and guarded sorted comparison reject them.
- Boolean and Decimal coercion, wrong expected type, missing IDs and null IDs are rejected.
- Numeric-equivalent Int64 1 is accepted by original BSON numeric equality. The candidate explicitly
  tightens this case to the fixture's seeded Int32 type; a control records that intentional difference.
These controls are behavioral counterexamples, not claims that the original assertion was logically
wrong for the normal fixture. The original issue measured here is excessive assertion cost.

Validation:
- All four test frameworks compiled: net462, net481, net8.0, net10.0; zero errors.
- Final full CI query namespace partition: .NET 8.0.30 825 passed/1 existing skip, 35.428 s;
  .NET 10.0.11 825 passed/1 existing skip, 30.084 s. These include 14 new controls.
- Independent review found and closed the equal-set/unequal-multiplicity control gap.
- Diff and C# size checks passed. No production changes, randomized coverage reductions,
  deadline changes, root-worktree edits or pushes were made by this task.

Hosted exact-binary Windows diagnosis is owned by the separate reviewer. A large assertion allocation
rate can plausibly amplify runtime/GC/environment variation, but these local measurements cannot
attribute the isolated Windows session timeout or guarantee its absence after the candidate.
