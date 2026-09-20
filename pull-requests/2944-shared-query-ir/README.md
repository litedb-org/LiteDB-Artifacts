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

- One `BsonMapper` translates 1,500 generated lambdas, each with three sets of captured values, so unrelated shapes share buckets, evict each other and are hit again with new values.
- The shape seed alone decides the tree; the value seed only changes captures (integers, strings, enum values, `StringComparison` modes, dictionary keys, list contents, and the runtime type behind an `object` capture).
- The grammar covers predicates, integer scalars, nested `object[]` projections and nested member initializers, including `Math.Min`/`Math.Max`, closed helper calls over nested arrays, `string.Equals` in both forms, enum `==` and `Equals`, and dictionary indexers.
- Each translation must equal an uncached translation of the same tree (`DisableCompilationCache`, fresh mapper) in `Source`, expression metadata, parameters and the scalar result on one document. If one side throws, the other must throw the same exception type.
- It runs in both seed orders and both `EnumAsInteger` settings, and fails if fewer than half the generated lambdas translate.

Mutation checks, net10.0:

| Product change | Result |
|---|---|
| none (`7b5df926c`) | 4 of 4 pass, 50 s |
| child counts removed from the shape key (reverts part of `b1e027307`) | fails; first at shape seeds 98 and 838: `[[],@p0]` and `[[@p0]]` reuse each other's template |
| `string.Equals` comparison-mode marker removed (reverts `0d60eb953`) | fails at shape seed 1496, value seed 1: `x.Name.Equals("ready", Ordinal)` gets the non-ordinal template and loses the `$.Name=@p0` term |

The second row matters because the hand-written guard test for that case could not be made to fail by mutation.

Full local suites at `7b5df926c`, Release, system zone W. Europe Standard Time:

| Target | Passed | Skipped | Failed |
|---|---:|---:|---:|
| net10.0 | 2,994 | 7 | 0 |
| net8.0 | 2,994 | 7 | 0 |

net481 and net462 test projects compile; their suites were not run locally.

To reproduce:

```bash
dotnet test LiteDB.Tests -c Release -f net10.0 -p:GitVersionEnabled=false --filter "FullyQualifiedName~LinqCacheFuzz_Tests"
```

## Limits

The generator only produces trees the grammar describes. It has no `DateTime` members, DbRefs, quantifiers (`Any`/`All`), `Select`/`Where` over row arrays, interface-typed rows or custom resolvers, and it uses one fixed document. A clean run says the cache agrees with the direct translator for these shapes; it does not say the direct translator is right.
