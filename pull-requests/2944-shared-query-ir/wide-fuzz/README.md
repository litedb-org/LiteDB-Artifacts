# Wide LINQ cache fuzz campaign

This directory records the expanded differential campaign run after
[LiteDB #2944](https://github.com/litedb-org/LiteDB/pull/2944) merged. The source
and the narrowly scoped fix found by the campaign are in LiteDB commit
[`395a84319`](https://github.com/litedb-org/LiteDB/commit/395a84319).

## Method

The opt-in test generates expression trees at depth 4 and translates three
capture-value sets for every shape through a shared `BsonMapper`. It compares
the cached translation with a fresh uncached translator, then evaluates the
translation on three mapped rows plus null and missing-field documents. Where
the expression can be compiled, it also compares LiteDB's result with the CLR
lambda on the three real rows.

The expanded grammar adds:

- `DateTime.Date`, `DateTime.Year`, and `DateTime.AddDays`;
- nullable coalescing, `HasValue`, and `Value`;
- `Any`, `All`, `Where`, and `Select` over arrays;
- captured `[BsonRef]` values and projections;
- interface-typed roots;
- regular and index expression entry points; and
- eight concurrent callers sharing one mapper.

All runs used .NET 10 Release on Linux with `TZ=Europe/Berlin`, seed 0,
200,000 shape seeds, three value sets, and eight worker threads. The four
configurations cover the Cartesian product of `EnumAsInteger` false/true and
`GetExpression`/`GetIndexExpression`.

```bash
TZ=Europe/Berlin \
LITEDB_FUZZ_SEED=0 \
LITEDB_FUZZ_SHAPES=200000 \
LITEDB_FUZZ_CONFIG=0 \
LITEDB_FUZZ_THREADS=8 \
LITEDB_FUZZ_MAX_TRANSLATOR_DIFFS=2909 \
LITEDB_FUZZ_OUTPUT=config-0.jsonl \
dotnet test LiteDB.Tests/LiteDB.Tests.csproj -c Release -f net10.0 \
  -p:GitVersionEnabled=false --no-build \
  --filter 'FullyQualifiedName~LinqCacheWideFuzz_Tests'
```

## Results

Each configuration generated 600,000 cases. Exactly 556,743 translated and
43,257 were rejected in the same way by the cached and direct paths.

| Config | Enum as integer | Entry point | Cache differences | Translator differences | Generator failures | SHA-256 |
|---:|:---:|---|---:|---:|---:|---|
| 0 | no | expression | 0 | 2,909 | 0 | `a74ffc4b085ef56b3989ea6904490e79dca4d6c3f1a0a8fb0d6bc12c1284d10c` |
| 1 | yes | expression | 0 | 2,909 | 0 | `eef022ac71416cf3c72fed73dfc45883e919d3fb602e834f17e20bede47ba74c` |
| 2 | no | index expression | 0 | 2,909 | 0 | `506c73b1886eb9722bd3c35792bd6ed73cd627bacf5417d10f64eabcfb16f45e` |
| 3 | yes | index expression | 0 | 2,909 | 0 | `235625875bebd5aec4aea7523deff64b302637f62140e990f21c3b1b47085210` |

The aggregate is 2.4 million generated cases and 2,226,972 successful
translations, with zero cache differences and zero generator failures. The
raw JSONL files include the summary and every retained failure, identified by
seed, value seed, expression, row, and both results.

## Finding and classification

The first rehearsal exposed a real translator issue rather than a cache issue:
a fully closed captured local `DateTime.Date` was lowered to LiteDB's server-side
`DATETIME(YEAR(...), ...)` expression. That reconstructs a UTC-kind value and can
shift the calendar date when serialization applies the local offset. The source
follow-up evaluates a fully closed `.Date` chain before BSON serialization and
adds a fixed regression for `.Date` and `AddDays(...).Date`.

For comparison, `dev` at the #2944 merge commit `ffc3edfc9` was run with the
same harness and configuration 0. It reported 48,860 translator differences,
zero cache differences, and zero generator failures. The first 10,000 details
are retained in `dev-baseline.jsonl` (the campaign's sample cap); its SHA-256 is
`b7c589cddc2925f8abc3fb0a00530b82440099cd6e0e13fa189cd1b0b1f10a3c`.
The focused fix reduced that class by 94% to 2,909 differences.

The remaining 2,909 records cover 757 shape seeds and one semantic class:
`.Date` is applied to a row-dependent `DateTime` expression, often a conditional
that mixes stored and captured dates. LiteDB has no time-zone/kind metadata at
that point, so its component-wise server expression and the CLR's local-kind
operation differ around local midnight. This result is identical in all four
cache configurations. It predates the follow-up, does not vary with cache reuse,
and is therefore classified as an existing translator limitation rather than a
cache-soundness failure.

On the requested stop criterion, the shared LINQ shape cache completed the
wider campaign cleanly: no generated shape produced a difference between a
cached translation and a fresh translation.

The final source commit also builds the entire solution in Release with test
hooks enabled, including net462 and net481. Its local Release suites in the
Europe/Berlin zone passed on both net10.0 and net8.0: 3,002 passed, 7 skipped,
and 0 failed on each target.
