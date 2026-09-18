# LiteDB #2905 — shared query IR and query optimization

Evidence for [litedb-org/LiteDB#2905](https://github.com/litedb-org/LiteDB/pull/2905). The pull request's summary tracks the current merge-confidence assessment; this folder holds what that assessment rests on.

## Provenance

Everything outside `reaudit-2026-09-18-windows/` was exported from the pull request at commit [`4599cb3a`](https://github.com/litedb-org/LiteDB/commit/4599cb3a9c4f72f2a70a9142de9cc43959b2a4b4), where it lived under `docs/`. The 357 data files are byte-identical to that commit. The four reports are verbatim except for two links that pointed into the source tree and now use permalinks.

To verify, from a LiteDB clone that has the pull request's commits:

```bash
git rev-parse 4599cb3a:docs/benchmarks/query-ir/after-1.json
git hash-object pull-requests/2905-shared-query-ir/benchmarks/query-ir/after-1.json   # same id
```

The harnesses that produced the data stay with the source, under [`tools/`](https://github.com/litedb-org/LiteDB/tree/4599cb3a9c4f72f2a70a9142de9cc43959b2a4b4/tools): `QueryIrBenchmarks`, `QueryOptimizationBenchmarks`, `QueryParameterLifetimeBenchmarks` and `QueryConfidenceChecks`. Each accepts `-p:LiteDBAssembly=<path to LiteDB.dll>` so that one build of the harness can be pointed at a "before" and an "after" assembly.

## Reports

| Report | Covers |
|---|---|
| [`query-ir-benchmarks.md`](query-ir-benchmarks.md) | The original direct-IR translation measurements |
| [`query-optimization-benchmarks.md`](query-optimization-benchmarks.md) | Every optimization step against the step before it: timings, allocations, plans, controls and the regressions that were found |
| [`query-optimization-overall.md`](query-optimization-overall.md) | Cumulative comparison against the original shared-IR implementation |
| [`query-merge-confidence.md`](query-merge-confidence.md) | First merge-confidence audit (Linux): questions asked, defects found and fixed, limits of the evidence |

## Data

| Folder | Files | Contents |
|---|---:|---|
| [`benchmarks/query-ir`](benchmarks/query-ir) | 6 | Raw batches for the direct-IR measurements |
| [`benchmarks/query-optimization`](benchmarks/query-optimization) | 332 | Raw batches per optimization step, including reruns and unfavourable samples |
| [`query-confidence-audit`](query-confidence-audit) | 19 | First audit: paired current-`dev` comparisons at 1 and 4 CPUs, tiering diagnostics, oracle seed summaries, parameter-lifetime probe, and [`results.md`](query-confidence-audit/results.md) |

Environment for all of the above: production Release assemblies, .NET 8.0.30, Ubuntu 24.04, AMD Ryzen 9 3900X, fixed CPU affinity, interleaved before/after processes. It is a shared host; the reports say where that limits precision.

## Independent re-audit

[`reaudit-2026-09-18-windows`](reaudit-2026-09-18-windows) is a second audit on a different OS and CPU (Windows 11 x64, .NET 8.0.30, 32 logical processors). It re-verified the first audit's claims rather than reusing them and added checks the first audit did not have. See its README for what each probe does and what it found.
