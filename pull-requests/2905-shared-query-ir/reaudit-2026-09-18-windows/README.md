# Independent re-audit of LiteDB #2905 (2026-09-18, Windows)

A second merge-confidence audit, run on a different OS and CPU from the first. Its conclusions are tracked in the [pull request summary](https://github.com/litedb-org/LiteDB/pull/2905); this folder holds the probe sources and the raw outputs behind them.

**Environment:** Windows 11 x64, .NET 8.0.30, 32 logical processors, production Release assemblies (`TestingEnabled=false`).

**Builds compared**

| Name in outputs | Commit |
|---|---|
| `dev`, "before" | `litedb-org/LiteDB` `dev` at `7d2a16c4` |
| `pr`, "after" | pull request head at `21a04896` |
| `merged` | `21a04896` merged into `7d2a16c4` |

`merged` matters. The branch was nine commits behind `dev`, and one apparent regression (see *Extreme bounds* below) existed only because of that gap. Judge a pull request by what would be merged, not by its head alone.

Machine-specific paths in the outputs were replaced with `<scratch>` and `<home>`. Nothing else was edited.

## How the probe works

`probe/` is one console project compiled once per LiteDB build, so both sides run identical code:

```bash
dotnet build LiteDB/LiteDB.csproj -c Release -p:TestingEnabled=false -f net8.0 --artifacts-path <out>/prod-X
dotnet build probe/Probe.csproj -c Release -p:LiteDBAssembly=<out>/prod-X/bin/LiteDB/release_net8.0/LiteDB.dll --artifacts-path <out>/probe-X
dotnet <out>/probe-X/bin/Probe/release/Probe.dll <command> [args]
```

A build made from a `git worktree` gets assembly version `0.0.0.0`; add `-p:GitVersionEnabled=false -p:AssemblyVersion=6.0.0.0` when a binary compiled against one build must load another.

## Probes and findings

| Command | Source | What it checks | Result |
|---|---|---|---|
| `corpus cold\|warm <set>` | `Corpus.cs` | 165 LINQ lambdas × 3 sets of captured values. Prints `Source`, parameters, type, flags, fields and the execution result. `warm` first translates the corpus four times with set 0 on one mapper, so the shape cache is hit with values it was not admitted with. | `dev` vs `pr`: 151 lambdas byte-identical, 14 intentional differences (`results/dev-cold-*.txt`, `results/pr-cold-*.txt`). `pr` warm vs cold: identical (`results/pr-warm-*.txt`). |
| `stress <threads> <rounds> <iterations>` | `Stress.cs` | Threads share one mapper and one database while using different captured values and SQL parameters; every answer is compared with the single-threaded one. | 12 threads: 118,800 LINQ and 48,000 SQL checks, 0 mismatches, on both builds. |
| `rt-create`, `rt-mutate`, `rt-digest <file> [password]` | `RoundTrip.cs` | Cross-version file round trip. One build creates 6,000 documents with five secondary indexes (unique, duplicate-heavy, nested, multikey, keys over 255 bytes); the other deletes 2,000, moves the keys of 2,000, inserts 3,000 and rolls back a transaction. Both then walk every index in both directions through the verified plan and seek every distinct key. | Both directions, plain and AES: digests identical between builds, 10,101 seeks, 0 problems (`results/roundtrip-digest-example.txt`). |
| `like <seed> <n>` | `Like.cs` | Fuzzes `LIKE` against a regex reference under two collations. | `pr`: 400,000 cases, 0 disagreements. `dev` does not finish, see next row. |
| `likehang <maxValue> <maxPattern>`, `linqhang` | `LikeHang.cs`, `LinqHang.cs` | Enumerates small value/pattern pairs by size and reports the first that does not finish. | `dev`: `'ab' LIKE '%%a'` never terminates, reachable from `col.Find(x => x.Name.EndsWith("%a"))`. `pr`: none in 10,571 pairs. |
| `api` | `Api.cs` | Dumps the public and protected surface for a text diff. | No removals. The only addition from the pull request is `BsonExpression.Bind(BsonDocument)` (`results/api-*.txt`; the `BsonVector` lines come from a newer `dev` commit, not from the pull request). |
| `extreme` | `ExtremeBounds.cs` | `Score > @p AND Score < 100` with `NaN`, `+Infinity` and `double.MaxValue`, indexed or not, empty or populated. | `pr` alone throws `OverflowException` in 9 of 16 cases; `merged` equals `dev` in 16 of 16, because `dev` had since fixed `BsonValue.CompareTo`. Guarded on the branch as well in `4599cb3a` (`results/extreme-*.txt`). |
| `throwing` | `ThrowingBounds.cs` | 14 predicates whose bound expression throws (`1 % 0`, `SUBSTRING('abc', 10)`) × indexed/unindexed × empty/populated. | `merged` equals `dev` in 56 of 56 cases (`results/throwing-*.txt`). |
| `groupby`, `issues` | `GroupByLeak.cs`, `IssueRepros.cs` | Reproductions of two defects that exist on `dev` and are not caused by the pull request. | Filed as [LiteDB#2921](https://github.com/litedb-org/LiteDB/issues/2921) (LINQ drops unary minus) and [LiteDB#2922](https://github.com/litedb-org/LiteDB/issues/2922) (`GROUP BY` overwrites `key` in the caller's parameters). |
| `misc` | `Misc.cs` | Index creation and error messages across builds. | Auto-generated index names and persisted index expressions are identical. |

The randomized oracle itself is [`tools/QueryConfidenceChecks`](https://github.com/litedb-org/LiteDB/blob/4599cb3a9c4f72f2a70a9142de9cc43959b2a4b4/tools/QueryConfidenceChecks/Program.cs) in the source tree. It passed six seeds the first audit had not used (71001 to 71006, 300 shapes each): 28,800 cases and 115,200 assertions.

## Benchmarks

`results/bench/` holds 52 runs of the source tree's `QueryOptimizationBenchmarks` `overall` suite: the harness built once, with only `LiteDB.dll` swapped between `dev` ("before") and the pull request head ("after"). Runs are interleaved before, after, after, before. Affinity was set at process creation with `start /affinity`. Checksums match in all 16 workloads.

| Series | Runs | Conditions |
|---|---:|---|
| `4cpu` | 12 | four CPUs, default tiering |
| `1cpu` | 12 | one CPU, default tiering |
| `1cpu-notier` | 12 | one CPU, `DOTNET_TieredCompilation=0` |
| `iso-scan` | 8 | one CPU, default tiering, **only** the scan-control workload |
| `nopgo-full` | 8 | one CPU, `DOTNET_TieredPGO=0`, full suite |

Scan control on one CPU, median:

| Condition | `dev` | pull request | Change |
|---|---:|---:|---:|
| full suite, scan runs last | 25.1 ms | 52.0 ms | +107% |
| scan in isolation | 55.0 ms | 49.1 ms | −11% |
| full suite, tiering off | 27.5 ms | 25.8 ms | −6% |

The +107% reproduces the first audit's Linux result, but it is an artifact of benchmark order. In isolation both builds sit near 50 ms, which is code that has not reached tier 1 yet. On `dev` the nine workloads that run before the scan are themselves full scans and warm that path; on the pull request they are microsecond index seeks, so the scan path is still cold when it is measured, and with one CPU the background JIT cannot catch up within the run. The first audit's *Exists* and top-N regressions did not reproduce on this machine (−11% and −21%).
