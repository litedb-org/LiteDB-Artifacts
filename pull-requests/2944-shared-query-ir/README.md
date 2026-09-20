# LiteDB #2944 — shared query IR (successor of #2905)

Evidence for [litedb-org/LiteDB#2944](https://github.com/litedb-org/LiteDB/pull/2944). That pull request carries the audited commits of #2905 minus residual-contradiction pruning (tracked in [#2945](https://github.com/litedb-org/LiteDB/issues/2945)) and the packed index links (now [#2942](https://github.com/litedb-org/LiteDB/pull/2942)). Benchmarks and both merge-confidence audits stay in [`../2905-shared-query-ir`](../2905-shared-query-ir); they were not repeated.

## Cache review

[`cache-review.md`](cache-review.md) is the focused review of the LINQ shape cache, the mapping guards and the SQL/text cache boundaries. It was written in the pull request at commit [`d5b16641e`](https://github.com/litedb-org/LiteDB/commit/d5b16641e67fd3e7953b99b2a7e35e3d145ded43) as `docs/shared-query-ir-cache-review.md` and moved here by [`2ee26f4f2`](https://github.com/litedb-org/LiteDB/commit/2ee26f4f2). The file is byte-identical:

```bash
git rev-parse d5b16641e:docs/shared-query-ir-cache-review.md                 # 31a233cf...
git hash-object pull-requests/2944-shared-query-ir/cache-review.md           # same id
```

Its fixes are in [`b1e027307`](https://github.com/litedb-org/LiteDB/commit/b1e0273073ba4a53cbd259a6e00224e934373e58). Independent check of that commit on Windows 11 (W. Europe Standard Time, .NET 10.0 and 8.0): with the shape-key and `BsonValue` pass-through fixes reverted, 9 of the 23 new regression tests fail (the eight array/initializer collision cases and the BSON parity case); with them in place all pass.

One caveat on the report's "Final local verification": it names `TZ=Europe/Berlin`. That variable changes the zone for .NET on Linux only. The runs below were made on a machine whose system zone is not UTC.

## Randomized differential test

Three manual reviews each found a cache-reuse bug (`0d60eb953`, `4a3bf9724`, `b1e027307`), so [`7b5df926c`](https://github.com/litedb-org/LiteDB/commit/7b5df926c) adds a seeded differential test instead of a fourth reading: `LiteDB.Tests/Mapper/LinqCacheFuzz_Tests.cs` with its generator `LinqCacheFuzzGenerator.cs`.

- One `BsonMapper` translates 400 generated lambdas by default (`LITEDB_FUZZ_SHAPES` and `LITEDB_FUZZ_SEED` select other ranges), each with three sets of captured values, so unrelated shapes share buckets, evict each other and are hit again with new values.
- The shape seed alone decides the tree; the value seed only changes captures (integers, strings, enum values, `StringComparison` modes, dictionary keys, list contents, and the runtime type behind an `object` capture).
- The grammar covers predicates, integer scalars, nested `object[]` projections and nested member initializers, including `Math.Min`/`Math.Max`, closed helper calls over nested arrays, `string.Equals` in both forms, enum `==` and `Equals`, and dictionary indexers.
- Each translation must equal an uncached translation of the same tree (`DisableCompilationCache`, fresh mapper) in `Source`, expression metadata, parameters and the scalar result on one document. If one side throws, the other must throw the same exception type.
- It runs in both seed orders and both `EnumAsInteger` settings, and fails if fewer than half the generated lambdas translate.

The first version (`7b5df926c`) ran 1,500 seeds in 50 s locally and timed out two Windows .NET 10 CI jobs, whose test step is limited to five minutes. [`7c4d5e396`](https://github.com/litedb-org/LiteDB/commit/7c4d5e396) defaults to 400 seeds (12 s) and biases array elements toward nested containers and plain captures, because the smaller range otherwise missed the shape-key mutation.

Mutation checks at `7c4d5e396`, net10.0, default range:

| Product change | Result |
|---|---|
| none | 4 of 4 pass, 12 s |
| child counts removed from the shape key (reverts part of `b1e027307`) | all 4 configurations fail, e.g. shape seed 278: `[[],@p0]` and `[[@p0]]` reuse each other's template |
| `string.Equals` comparison-mode marker removed (reverts `0d60eb953`) | all 4 configurations fail: `x.Name.Equals("ready", Ordinal)` gets the non-ordinal template and loses the `$.Name=@p0` term |

The last row matters because the hand-written guard test for that case could not be made to fail by mutation.

Exploratory run at `7c4d5e396`: `LITEDB_FUZZ_SHAPES=20000`, `LITEDB_FUZZ_SEED=10000`, net10.0. 240,000 translations across the four configurations, 11 m 38 s, no mismatch.

Full local suites at `56fdfc274`, Release, system zone W. Europe Standard Time:

| Target | Passed | Skipped | Failed |
|---|---:|---:|---:|
| net10.0 | 3,000 | 7 | 0 |
| net8.0 | 3,000 | 7 | 0 |

CI at `7c4d5e396`: all 48 checks passed.

net481 and net462 test projects compile; their suites were not run locally.

To reproduce:

```bash
dotnet test LiteDB.Tests -c Release -f net10.0 -p:GitVersionEnabled=false --filter "FullyQualifiedName~LinqCacheFuzz_Tests"
```

## Rebinding bug found by a second review

A source-level review of `b1e027307` (nothing executed by that reviewer) reported that `BsonExpression.BindCore` dropped `GroupKeyAliases`. Reproduced before fixing: `Query.And("@key = 1", "COUNT(*) > 0")` as a `Having` filter returns group 1, the same expression after `Bind` returned none. Fixed in [`56fdfc274`](https://github.com/litedb-org/LiteDB/commit/56fdfc274); five cases fail before and pass after. The randomized test could not have found this: it covers LINQ translation, not rebinding. `SelectAliases`, `SelectContext` and `_selectAliasCompiled` are also not copied by `BindCore`, on purpose, because they are only read while SQL is parsed.

The remaining gaps are tracked in [LiteDB#2946](https://github.com/litedb-org/LiteDB/issues/2946).

## Limits

The generator only produces trees the grammar describes. It has no `DateTime` members, DbRefs, quantifiers (`Any`/`All`), `Select`/`Where` over row arrays, interface-typed rows or custom resolvers, and it uses one fixed document. A clean run says the cache agrees with the direct translator for these shapes; it does not say the direct translator is right.

## Post-merge wide campaign

The [wide fuzz campaign](wide-fuzz/README.md) closes the listed generator gaps,
adds a compiled-CLR reference, null and missing documents, index-expression
translation, and concurrent callers. Across 2.4 million generated cases it
found no cache difference. It did expose a closed `DateTime.Date` translator
bug, records the focused fix, and classifies the remaining pre-existing
row-dependent time-zone differences.
