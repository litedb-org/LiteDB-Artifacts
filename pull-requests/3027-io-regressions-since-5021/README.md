# LiteDB #3027: review scratch code for the IO regressions since 5.0.21

Temporary code and small evidence from the Claude Code session behind [litedb-org/LiteDB#3027](https://github.com/litedb-org/LiteDB/pull/3027), *Fix IO regressions since 5.0.21 (with proof tests)*, branch `claude/compassionate-knuth-m52zj6`. It was copied out of the session's scratch directory before the container was deleted.

> **This is temporary review code. It is not maintained.** The probes were xUnit classes dropped into `LiteDB.Tests` for a few minutes to prove or refute a review finding, then removed. Nothing here is built by CI. Most probes use internal test hooks (`TestingEnabled=true`) and helper classes of the commit they were written against, so they may not compile against later commits. What the pull request kept is in its own tests. The fixture generators were rewritten as maintained tools in [`c70147bf6`](https://github.com/litedb-org/LiteDB/commit/c70147bf6b18f121a110e5d635c7c1650555c5c2) ([`tools/RegressionFixtures5021`](https://github.com/litedb-org/LiteDB/blob/d6d7f16cfb52e76b2e6d5420e09489aa95689cc9/tools/RegressionFixtures5021), [`tools/PrereleaseVectorFixture`](https://github.com/litedb-org/LiteDB/blob/d6d7f16cfb52e76b2e6d5420e09489aa95689cc9/tools/PrereleaseVectorFixture), provenance in [`RegressionFixtures.md`](https://github.com/litedb-org/LiteDB/blob/d6d7f16cfb52e76b2e6d5420e09489aa95689cc9/LiteDB.Tests/Resources/RegressionFixtures.md)); the originals below are kept for the record.

## Provenance

| | |
|---|---|
| Pull request | [#3027](https://github.com/litedb-org/LiteDB/pull/3027), base `dev` |
| Branch head when archived | [`d6d7f16cf`](https://github.com/litedb-org/LiteDB/commit/d6d7f16cfb52e76b2e6d5420e09489aa95689cc9) (`d6d7f16cfb52e76b2e6d5420e09489aa95689cc9`) |
| `dev` at session start | [`11e9ffacc`](https://github.com/litedb-org/LiteDB/commit/11e9ffaccf0ea0ada216cb60f1ac6432b947f181), the parent of the first PR commit [`e8785c4d6`](https://github.com/litedb-org/LiteDB/commit/e8785c4d60054fc7473e9fbbd26eb6720a55f7b8) (which adds the failing proof tests) |
| PR commits | `git log --oneline origin/dev..d6d7f16cf`; commit ids below link to them |
| Session | started 2026-09-27 23:04 UTC, still running when this folder was assembled (2026-09-28 about 15:10 UTC): one main session and 23 reviewer subagents |
| Released packages used | LiteDB 5.0.21 and 6.0.0-prerelease.114 from nuget.org |

Environment: Linux x64 container (Ubuntu 24.04.4 LTS, kernel 6.18.44, 4 vCPUs). The .NET SDK was installed at `/root/.dotnet`; `PATH=/root/.dotnet:$PATH dotnet --list-sdks` printed

```
10.0.401 [/root/.dotnet/sdk]
```

with the runtimes Microsoft.NETCore.App 8.0.31 and 10.0.12. Tests ran in Release with `-p:TestingEnabled=true --settings tests.runsettings`. Full suites were split into 11 filters by [`scripts/run-partitions.sh`](scripts/run-partitions.sh), because the runsettings' 5-minute `TestSessionTimeout` cuts a single run short.

**Scrubbed paths.** In every copied text file these strings were replaced; code logic is unchanged, binary files (`.zip`, `.tar.xz`) are unchanged:

| Placeholder | Stood for |
|---|---|
| `$SCRATCH` | the session's scratch directory (the source of this folder) |
| `$REPO` | the LiteDB checkout the session worked in |
| `$TMPDIR` | the container's temp directory that held the reviewers' worktrees (`$TMPDIR/review2-a`, ...) |

They are literal text: shell and Python helpers need `SCRATCH`/`REPO` substituted or exported, and `.csproj` files and C# string literals must be edited by hand. `/root/.dotnet` was left as is.

**Source trees not copied.** `headsrc/` was a `git worktree` of [`11e9ffacc`](https://github.com/litedb-org/LiteDB/commit/11e9ffaccf0ea0ada216cb60f1ac6432b947f181) (removed during the session); `fixsrc/` was `git archive db41487e1 LiteDB Directory.Build.props` (all 490 files verified identical to [`db41487e1`](https://github.com/litedb-org/LiteDB/commit/db41487e1f30ad7686026c73c2f4ac5e12afe900)). `crash-harness/head`, `heads` reference the first, `headfix` the second.

## What is where

Reviewer folders are named after the reviewer's topic; `r2`..`r4` are review rounds. The finding column names the fix commits (their messages say "Found by review").

| Path | What it is | Finding / commits |
|---|---|---|
| [`probes/sync-retirement-r2/`](probes/sync-retirement-r2) | `ReviewProbe_Tests.cs` (P1-P9), reviewer "sync, retirement, WAL" at [`db41487e1`](https://github.com/litedb-org/LiteDB/commit/db41487e1f30ad7686026c73c2f4ac5e12afe900); `baseline-mvcc.txt` its MVCC baseline run | The checkpoint that first finds storage that cannot sync still retired and cleared WAL frames: [`43b76d9d9`](https://github.com/litedb-org/LiteDB/commit/43b76d9d98ac784accf86d4a055c96f98c041f5f) |
| [`probes/streams-r2/`](probes/streams-r2) | `ZzReviewScratch_Tests.cs`, reviewer "streams and whole branch" at [`db41487e1`](https://github.com/litedb-org/LiteDB/commit/db41487e1f30ad7686026c73c2f4ac5e12afe900): replays the 59 crash images of a 5.0.21 migration over non-writable streams | Two writable repairs ran before the read-only fallback of [`003a84961`](https://github.com/litedb-org/LiteDB/commit/003a84961b5d9103f92dee198fd0daefea56399a): [`9d141f4f8`](https://github.com/litedb-org/LiteDB/commit/9d141f4f8a0c43819a7591e9406d54b10062d82a). Console probe: `generators/p21-pbr`, images: `images/stream-opens-r2.zip` |
| [`probes/legacy-rebuild-r2/`](probes/legacy-rebuild-r2) | `ReviewProbe.bak/` (expression, layout, retry, vector flake probes) and `ReviewProbe.final/` (disk full, drain, encrypted tail, `TailCrash_Probe`), reviewer "legacy format & rebuild" at [`db41487e1`](https://github.com/litedb-org/LiteDB/commit/db41487e1f30ad7686026c73c2f4ac5e12afe900); `vector-compat.log` | Blocker: [`776b0a1d7`](https://github.com/litedb-org/LiteDB/commit/776b0a1d79164d1b83c85e401cd7427a7deb8ce6) ran the legacy drain before the trim, so the journal footer completed a torn WAL page: [`5873fde3c`](https://github.com/litedb-org/LiteDB/commit/5873fde3c9a11820a4489f193cd5be896e9d629a). Images: `images/badimages.zip`, `images/crash5021.zip`; `generators/probe5021` |
| [`probes/drain-rebuild-retry-r3/`](probes/drain-rebuild-retry-r3) | `TornTailSweep_Probe.cs` (crash sweep with repeated interruption), `Misc_Probe.cs`, reviewer round 3 at [`f6e5df63d`](https://github.com/litedb-org/LiteDB/commit/f6e5df63d75a38e68e1cbb6ccec3dd0cee5d949d) (approve) | Salvaged document whose vector key throws: [`e94c95b1a`](https://github.com/litedb-org/LiteDB/commit/e94c95b1ae058041b374e802b42ce91d50949cab); test gaps: [`d5a30871c`](https://github.com/litedb-org/LiteDB/commit/d5a30871c4d5d688e51a6a8222abff927bc6dd59), [`42d7dfd8a`](https://github.com/litedb-org/LiteDB/commit/42d7dfd8a62ebfbf8cd9d5d0f83bf1084a0bd364). Console probes: `generators/r3a` |
| [`probes/sync-proof-r3/`](probes/sync-proof-r3) | `Review3b_Probe_Tests.cs` (R1-R4), `opted-out-log-test.patch`, mutation helpers, reviewer "sync proof & retirement" at [`f6e5df63d`](https://github.com/litedb-org/LiteDB/commit/f6e5df63d75a38e68e1cbb6ccec3dd0cee5d949d) | The proof's log sync had no discriminating test: [`28a5dcf20`](https://github.com/litedb-org/LiteDB/commit/28a5dcf20f14e7701616a6961b0f253b4bb7304d) |
| [`probes/non-writable-streams-r3/`](probes/non-writable-streams-r3) | `r3c-ZzProbe3_Tests.cs` (crash-image replay over non-writable stream combinations) and its logs, including a mutation run | `EngineSettings` stream callers never got the read-only fallback: [`b0f04034a`](https://github.com/litedb-org/LiteDB/commit/b0f04034a78471ecb17999a8431849276b1e8af4). Generator: `generators/r3c21` |
| [`probes/streams-retirement-r4/`](probes/streams-retirement-r4) | `R4a*_Tests.cs`, `mut.sh`, reviewer round 4 at [`b0f04034a`](https://github.com/litedb-org/LiteDB/commit/b0f04034a78471ecb17999a8431849276b1e8af4) (approve) | Minors: [`57ef1dc3c`](https://github.com/litedb-org/LiteDB/commit/57ef1dc3cb9a1f13d6587f3c460c1cf8d1d034e1). Console probe: `generators/r4a/p21` |
| [`probes/wal-sync-reuse/`](probes/wal-sync-reuse) | `ScratchReview_Tests.cs`, reviewer of [`1f24a2507`](https://github.com/litedb-org/LiteDB/commit/1f24a2507398a7b311f1e556537a360693fc93e7) and [`ad5a064e5`](https://github.com/litedb-org/LiteDB/commit/ad5a064e5596528125ac1be1de2f7c49c0042502) at [`ada572454`](https://github.com/litedb-org/LiteDB/commit/ada572454da8eaa9e25e95b7756ec08ebaf7f228) | A fresh engine acknowledged commits resting on unsynced data (external review P1): [`37ddef286`](https://github.com/litedb-org/LiteDB/commit/37ddef286534356cb95de814863d373ed44c89a5), [`49416e578`](https://github.com/litedb-org/LiteDB/commit/49416e578f670963687b26262fd093ca39c2ebd5), [`1d9aae40f`](https://github.com/litedb-org/LiteDB/commit/1d9aae40fa9fce7e3a60f1ab3c6e3231805b630d), [`f71148eda`](https://github.com/litedb-org/LiteDB/commit/f71148eda4de31a847d2adc290364ef34fa28289), [`79d648eb6`](https://github.com/litedb-org/LiteDB/commit/79d648eb61cac02f1e0490e5861748b4efcde80f) |
| [`probes/torn-append/`](probes/torn-append) | `ScratchTornWal_Tests.cs`, reviewer of the external torn-append finding at [`ada572454`](https://github.com/litedb-org/LiteDB/commit/ada572454da8eaa9e25e95b7756ec08ebaf7f228) | The reported finding did not reproduce; two other write-path defects did: [`ca7e36a03`](https://github.com/litedb-org/LiteDB/commit/ca7e36a039fdb1bdba9e041ecf82e56e34836bd0), [`319613158`](https://github.com/litedb-org/LiteDB/commit/31961315828dfd13b0a7d875523acd4b5a17b3d0), [`ab535861d`](https://github.com/litedb-org/LiteDB/commit/ab535861d79a2b4d7b4d322dc00d6f90efc01db1) |
| [`probes/sync-durability/`](probes/sync-durability) | `ReviewFindings_Tests.cs` (F1-F6, at [`dbac6940a`](https://github.com/litedb-org/LiteDB/commit/dbac6940a6df6fcc516f15a23c2a39e6a909e93d)), `ReReview_Tests.cs` (R1, at [`3e789555f`](https://github.com/litedb-org/LiteDB/commit/3e789555fd912b32483020826ebe2931a9b51143)), `ReReview3_Tests.cs` (T1 and `Legacy_5_0_21_crash_images_open`, at [`274a39336`](https://github.com/litedb-org/LiteDB/commit/274a39336ae7c3b950a2a191089adf671b67a3bb)), test snippets `*.cs.txt` | F1-F6: [`7887c96f9`](https://github.com/litedb-org/LiteDB/commit/7887c96f9c9f15fc133d91fb9bb3cd837f494495), [`2b2347bb4`](https://github.com/litedb-org/LiteDB/commit/2b2347bb4b2739dec3817c2bb864b608d6aab84f), [`5fca247fd`](https://github.com/litedb-org/LiteDB/commit/5fca247fde8b82dea05615aa8f8d1c1499c615fd), [`7d1ec4367`](https://github.com/litedb-org/LiteDB/commit/7d1ec4367f64013de99f54e6f8f2672cd8610a46); R1: [`8c8900bc6`](https://github.com/litedb-org/LiteDB/commit/8c8900bc619091fc6a9af1d271dd4a2024cebdf4); T1: [`125497376`](https://github.com/litedb-org/LiteDB/commit/125497376d8058ef786583e4da3e025ab41e41e0). Reads `review-evidence/img5021b.tar.xz` |
| [`probes/failure-path-wal/`](probes/failure-path-wal) | Nine `Review*_Tests.cs` of the reviewer "failure-path WAL fixes" ([`dbac6940a`](https://github.com/litedb-org/LiteDB/commit/dbac6940a6df6fcc516f15a23c2a39e6a909e93d) to [`bc7c3ca82`](https://github.com/litedb-org/LiteDB/commit/bc7c3ca829e86033debb5ef319236576bd154cc9)) | Reader tears a buffered frame: [`c4119caba`](https://github.com/litedb-org/LiteDB/commit/c4119caba870f2c51f4f1fab7a3603e1bf8b34ec) (supersedes [`76a28bdd4`](https://github.com/litedb-org/LiteDB/commit/76a28bdd480b2e8531153fc3c59eb3a44c203f06)); encrypted log stream sync: [`5a748e6a9`](https://github.com/litedb-org/LiteDB/commit/5a748e6a9cf58bc6dfbb2f3a6fea75bb91395f4c); kept WAL across engines: [`bc7c3ca82`](https://github.com/litedb-org/LiteDB/commit/bc7c3ca829e86033debb5ef319236576bd154cc9); checkpoint failure window: [`dd1f86c90`](https://github.com/litedb-org/LiteDB/commit/dd1f86c906be43ab526216f870283ea415c15a30), [`94dc01500`](https://github.com/litedb-org/LiteDB/commit/94dc01500687b7aef7adcdc9f5c343dcecb88abe); `ReviewBufferedRetry` and `ReviewWriteThroughCost` back the residual and cost notes of [`11fa3a67f`](https://github.com/litedb-org/LiteDB/commit/11fa3a67f0a6c8855f8d630a2b269e7e5f22bd30) |
| [`review-evidence/legacy-format/`](review-evidence/legacy-format) | Hand-off folder of the reviewer "legacy-format fixes" ([`274a39336`](https://github.com/litedb-org/LiteDB/commit/274a39336ae7c3b950a2a191089adf671b67a3bb) to [`68931ef4f`](https://github.com/litedb-org/LiteDB/commit/68931ef4fe15c2fe602ece4254a943ba6f993487)): probes, `candidate-fix.diff`, `identity-fix.diff`, generator copies, `images/` | B1, a real 5.0.21 concurrent-writer WAL refused by [`95d9f7ff7`](https://github.com/litedb-org/LiteDB/commit/95d9f7ff7c277551f0e9e174c736bd396cdb6036): [`3c502e92f`](https://github.com/litedb-org/LiteDB/commit/3c502e92fa713f8f906e5064d07b78d5fbf44a65) (fixture `ConcurrentWalCrash_5_0_21.zip`); B2, a foreign 5.0.21 WAL: [`3b8744cdf`](https://github.com/litedb-org/LiteDB/commit/3b8744cdf490428299af54f5da2f497321afdd14) (`images/big1.zip`); N2: [`a6af38881`](https://github.com/litedb-org/LiteDB/commit/a6af38881419a8cfbac7434438f1217cd0f0c5b3) (`images/pre2-pre3.zip`); N5: [`86a433c61`](https://github.com/litedb-org/LiteDB/commit/86a433c619c7b95ae7679d5910b98cc93d4ef2d6); salvage: [`68931ef4f`](https://github.com/litedb-org/LiteDB/commit/68931ef4fe15c2fe602ece4254a943ba6f993487), [`8aff82d83`](https://github.com/litedb-org/LiteDB/commit/8aff82d832fe1fefdc07765338e9a664f329e825); `images/img5021c.zip` backs the creation-time identity check of [`3b8744cdf`](https://github.com/litedb-org/LiteDB/commit/3b8744cdf490428299af54f5da2f497321afdd14) |
| [`review-evidence/img5021b.tar.xz`](review-evidence/img5021b.tar.xz) | Ten crash images written by the LiteDB 5.0.21 package, each beside the live file it was copied from (list below) | Legacy page bound ([`95d9f7ff7`](https://github.com/litedb-org/LiteDB/commit/95d9f7ff7c277551f0e9e174c736bd396cdb6036), [`3c502e92f`](https://github.com/litedb-org/LiteDB/commit/3c502e92fa713f8f906e5064d07b78d5fbf44a65)): the PR description's "10 crash images" |
| [`review-evidence/candidate-diffs/`](review-evidence/candidate-diffs) | Reviewers' proposed patches | `candidate-fix-recoverheader.diff`: [`8c8900bc6`](https://github.com/litedb-org/LiteDB/commit/8c8900bc619091fc6a9af1d271dd4a2024cebdf4); `candidate-fix-aes-recovery.diff`: [`125497376`](https://github.com/litedb-org/LiteDB/commit/125497376d8058ef786583e4da3e025ab41e41e0); `probe-fix-and-truncation-probe.diff`: [`9be2a37f8`](https://github.com/litedb-org/LiteDB/commit/9be2a37f8300c6d0ee0946ec0775db5f5101e702) |
| [`review-evidence/snapshots/`](review-evidence/snapshots) | Source states found in no commit, plus [`identical-to-commits.tsv`](review-evidence/snapshots/identical-to-commits.tsv) listing 99 scratch snapshots and fixture copies that were not copied because they are byte-identical to a committed blob | `DurableFlush.experimentX.cs`: an unconditional data sync before a fresh engine's first durable ack, measured (shared mode, 300 single inserts: 2.6-3.3 ms per insert against 1.8-2.2 ms without) and replaced by the per-header proof of [`49416e578`](https://github.com/litedb-org/LiteDB/commit/49416e578f670963687b26262fd093ca39c2ebd5), [`5fca247fd`](https://github.com/litedb-org/LiteDB/commit/5fca247fde8b82dea05615aa8f8d1c1499c615fd); `WalIndexService.identity.cs`: the reviewer's identity check, became [`3b8744cdf`](https://github.com/litedb-org/LiteDB/commit/3b8744cdf490428299af54f5da2f497321afdd14); `r7final-IndexMigration.cs`: the conversion refusal naming the read-only connection string, before [`71373767e`](https://github.com/litedb-org/LiteDB/commit/71373767e56d7f10c5a754e30628f26f493355f7) committed it in another form; `pvf.full`: `PrereleaseVectorFile_Tests.cs` while [`86a433c61`](https://github.com/litedb-org/LiteDB/commit/86a433c619c7b95ae7679d5910b98cc93d4ef2d6) and [`a6af38881`](https://github.com/litedb-org/LiteDB/commit/a6af38881419a8cfbac7434438f1217cd0f0c5b3) were committed; `dfss.aes`: the encrypted-reader block of `DataFileStopsSyncing_Tests.cs`, cut out to commit [`5a748e6a9`](https://github.com/litedb-org/LiteDB/commit/5a748e6a9cf58bc6dfbb2f3a6fea75bb91395f4c), [`506678daf`](https://github.com/litedb-org/LiteDB/commit/506678dafffa750891aea8bc919c86f27671d48f) and [`d4d88a117`](https://github.com/litedb-org/LiteDB/commit/d4d88a117426994d42e0fc82d4041e07e116770d) separately |
| [`review-evidence/mutation/failure-path/`](review-evidence/mutation/failure-path) | Mutation harness (`mutate.py`, `mutations.py`, `mut2.py`, `mut3.py`, `run*.sh`) and results of the failure-path reviewer: `results*.txt` (M01-M24), `n_runs.txt`, `p_runs.txt`, `q_runs.txt`, per-mutation `*.test.log` | Mutation checks of [`dbac6940a`](https://github.com/litedb-org/LiteDB/commit/dbac6940a6df6fcc516f15a23c2a39e6a909e93d) to [`bc7c3ca82`](https://github.com/litedb-org/LiteDB/commit/bc7c3ca829e86033debb5ef319236576bd154cc9) (sync proofs, kept WAL, checkpoint failure window, buffered caller streams, encrypted log sync) |
| [`review-evidence/mutation/kept-wal/`](review-evidence/mutation/kept-wal) | The main session's mutation run (`run.py`, M1-M7, each caught; `results.txt`) on the working tree that became [`933b0ca2e`](https://github.com/litedb-org/LiteDB/commit/933b0ca2e013a982b06d6b5f744ef17abd5acb39) | [`933b0ca2e`](https://github.com/litedb-org/LiteDB/commit/933b0ca2e013a982b06d6b5f744ef17abd5acb39) |
| [`crash-harness/`](crash-harness) | Black-box process-crash harness (`crash/*.cs`: `Crash`, `Scen`, `DropIdx`, `Salvage`, `Migrate`, `Bank`, `DiskFull`, `TornSafepoint`, `SharedStress`, `SmallCrash`, `StreamDispose`, `SlowReader`, `Small`, `Trace`); `v5`/`v5s` build it against LiteDB 5.0.21, `head`/`heads` against `headsrc` ([`11e9ffacc`](https://github.com/litedb-org/LiteDB/commit/11e9ffaccf0ea0ada216cb60f1ac6432b947f181)), `headfix` against `fixsrc` | The first regression hunt, pinned by [`e8785c4d6`](https://github.com/litedb-org/LiteDB/commit/e8785c4d60054fc7473e9fbbd26eb6720a55f7b8): stale DropIndex bytes ([`ac2a81f48`](https://github.com/litedb-org/LiteDB/commit/ac2a81f489daeb0aa5286d1287117af4586412a4), [`cf92a407d`](https://github.com/litedb-org/LiteDB/commit/cf92a407d0602ebf59fcc0f7a5175eb259e1440b)), legacy WAL lost in conversion ([`31da4d5d7`](https://github.com/litedb-org/LiteDB/commit/31da4d5d7ef0179841594b4c22dbcd292167f2f0)), damaged documents ([`55ea28faf`](https://github.com/litedb-org/LiteDB/commit/55ea28faf594712b8a36d1dcafb30117a18d0fcc)), stream dispose ([`2a5d043b6`](https://github.com/litedb-org/LiteDB/commit/2a5d043b6b79c3f93a06264b6769f8594f76206e)), torn slot rewrite ([`ed9596da4`](https://github.com/litedb-org/LiteDB/commit/ed9596da426a55c13592bd72fe88487ea02a3b8f)). It wrote the sources of the `WalCrash`, `DropIndex` and `DamagedDocument` 5.0.21 fixtures |
| [`generators/`](generators) | `Program.cs` and `.csproj` of each throwaway generator or console probe (table below) | |
| [`scripts/`](scripts) | Helpers: `run-partitions.sh` (partitioned full suite), `full-run.sh`, `fullrun.sh`, `chain.sh`, `chain6.sh` (suite + compatibility scripts + fuzz shards), `run-fuzz.sh` (the CI fuzz shards), `bm.sh`/`tm.sh` (build/test main tree), `validate11.sh` (suites plus the checksum fuzz smoke), `mutate*.sh`, `mutate.py`, `runmut.sh`, `sim.py` (DropIndex layout simulation of the first review), `walinfo*.py` (WAL page dump) | |
| [`logs/test-runs/`](logs/test-runs) | `summary.txt` and per-partition logs of every full partitioned run (table below) | |
| [`logs/fuzz/`](logs/fuzz) | `summary.txt` and `checksums.log` of each local run of the CI fuzz shards; `checksum-repin/` logs; [`fuzz-results-index.tsv`](logs/fuzz/fuzz-results-index.tsv) (one row per fuzz case of every run, generated from the `summary.md` files, which were not kept) | Corpus re-pins [`8ec5373b2`](https://github.com/litedb-org/LiteDB/commit/8ec5373b2187623ddf69d20d8affbf08ce4f2d1e) and [`ad2625283`](https://github.com/litedb-org/LiteDB/commit/ad26252831b8d2816669748b82e33c9cc7f5e776): the two checksum-crash seeds 3163459 and 4058733 |
| [`logs/compat-initial/`](logs/compat-initial), [`logs/harness-runs/`](logs/harness-runs), [`logs/misc/`](logs/misc) | First compatibility-script run; crash-harness outputs (`*.out`) and the DropIndex layout cases; filtered test and build outputs | |
| [`drafts/`](drafts) | `pr-body.md` (PR description), `reply.md` (reply to the owner's review), `msg.txt` (commit message of [`5a748e6a9`](https://github.com/litedb-org/LiteDB/commit/5a748e6a9cf58bc6dfbb2f3a6fea75bb91395f4c)), `safety-section.md` (a #3034 safety section for the description, with `HEAD_SHA`/`DEV_SHA` still unfilled) | |
| [`images/`](images) | Database images, zipped (table below), [`SHA256SUMS`](images/SHA256SUMS) of every file inside, and [`not-archived.sha256.tsv`](images/not-archived.sha256.tsv) | |

### Generators

| Folder | Package | Wrote | Committed fixture |
|---|---|---|---|
| `gen5u` | 5.0.21 | unique-index documents with a damaged field | `DamagedUniqueDocuments_5_0_21.zip` ([`62d8f602f`](https://github.com/litedb-org/LiteDB/commit/62d8f602f91bb5131846d19c182d0ed5e3c56e17)) |
| `gen5enc` | 5.0.21 | encrypted WAL crash image | `EncryptedWalCrash_5_0_21.zip` ([`5873fde3c`](https://github.com/litedb-org/LiteDB/commit/5873fde3c9a11820a4489f193cd5be896e9d629a)) |
| `gen5keys` | 5.0.21 | damaged index keys | `DamagedIndexKeys_5_0_21.zip` ([`c8d02b00a`](https://github.com/litedb-org/LiteDB/commit/c8d02b00a9589b21c139be4a2bd0eb2c7831d9de)) |
| `gen5021` | 5.0.21 | `create`, `crash`, `bigcrash`, `dropindex`, `verify`: the concurrent-writer crash (`images/fp1.zip`), the 1,730-page foreign WAL (`review-evidence/legacy-format/images/big1.zip`), `pre3` | `ConcurrentWalCrash_5_0_21.zip` ([`3c502e92f`](https://github.com/litedb-org/LiteDB/commit/3c502e92fa713f8f906e5064d07b78d5fbf44a65)), `ForeignWal_5_0_21.zip` ([`3b8744cdf`](https://github.com/litedb-org/LiteDB/commit/3b8744cdf490428299af54f5da2f497321afdd14)) |
| `gen5021b` | 5.0.21 | `img5021c`: files after pragma, rebuild, collation rebuild, encrypted rebuild, checkpoint and upgrade (same text as `review-evidence/legacy-format/gen5021b-Program.cs` and `Program.cs`) | none |
| `gen5021b-original` | 5.0.21 | `img5021b`, the ten crash images. **Reconstructed**: the reviewer "sync-durability fixes" wrote it at 09:48 UTC and it was overwritten at 10:55 by the program above; this copy replays its three recorded edits from the session transcript | none |
| `gen5021s` | 5.0.21 | salvage duplicate | `DamagedSalvageDuplicate_5_0_21.zip` ([`68931ef4f`](https://github.com/litedb-org/LiteDB/commit/68931ef4fe15c2fe602ece4254a943ba6f993487)) |
| `pre114` | 6.0.0-prerelease.114 | `vectors.db`, `vectors-encrypted.db` | `Vectors_6_0_0_prerelease_114.zip` ([`cf92a407d`](https://github.com/litedb-org/LiteDB/commit/cf92a407d0602ebf59fcc0f7a5175eb259e1440b)) |
| `genpre-original` | 6.0.0-prerelease.114 | `vector-salvage.db`. **Recovered** verbatim from the session transcript; overwritten in scratch by `genpre` | added to `Vectors_6_0_0_prerelease_114.zip` in [`e94c95b1a`](https://github.com/litedb-org/LiteDB/commit/e94c95b1ae058041b374e802b42ce91d50949cab) |
| `genpre` | 6.0.0-prerelease.114 | `multi.db` with two vector indexes (`pre2`, `pre3`); same text as `review-evidence/legacy-format/genpre-Program.cs` | none ([`a6af38881`](https://github.com/litedb-org/LiteDB/commit/a6af38881419a8cfbac7434438f1217cd0f0c5b3) evidence) |
| `probe5021` | 5.0.21 | opens `badimages` with 5.0.21 | none |
| `r3c21` | 5.0.21 | fixtures and read-only probe of the non-writable streams reviewer | none |
| `r3a/p21`, `r3a/p21b`, `r3a/genvec` | 5.0.21 | console probes and the vector fixture of the round-3 reviewer | none |
| `r4a/p21` | 5.0.21 | console probe of the round-4 reviewer | none |
| `p21-pbr` | 5.0.21 / branch build | one `Program.cs` compiled against 5.0.21 (`p21`) and a branch `LiteDB.dll` (`pbr`, `BRANCH`); non-writable stream opens | none |
| `mx`, `mxlim` | runtime only | named-mutex length limits on Linux | none ([`01831c79f`](https://github.com/litedb-org/LiteDB/commit/01831c79fbd0ee41bb7f07a2dc73100ede183c98)) |

To run one: `dotnet run --project generators/<name> -c Release -- <arguments>` (see the top of `Program.cs`); `p21-pbr/pbr` needs its `HintPath` pointed at a branch build.

### Full test runs

Partitioned full suites (`logs/test-runs/<run>/summary.txt`). Counts are summed over the 11 partitions. Runs in the main working tree (`p*`) overlapped later commits.

| Run | Commit | Framework | Passed / failed / skipped | Note |
|---|---|---|---|---|
| `results-baseline` | [`e8785c4d6`](https://github.com/litedb-org/LiteDB/commit/e8785c4d60054fc7473e9fbbd26eb6720a55f7b8) | net10.0 | 4,817 / 16 / 7 | The 16 failures are the proof tests, as intended before the fixes |
| `results-r1-net10`, `results-r1-net8` | [`44909eeb9`](https://github.com/litedb-org/LiteDB/commit/44909eeb94d0703adefb021c1ecf36515936a512) | both | 4,856 / 0 / 7 | Round-1 fixes |
| `r2/net8` | [`db41487e1`](https://github.com/litedb-org/LiteDB/commit/db41487e1f30ad7686026c73c2f4ac5e12afe900) | net8.0 | 4,866 / 0 / 7 | with `compat-summary.txt` (6 compatibility scripts pass) |
| `r3/net10`, `r3b/net8` | [`f6e5df63d`](https://github.com/litedb-org/LiteDB/commit/f6e5df63d75a38e68e1cbb6ccec3dd0cee5d949d) | both | 4,885 / 0 / 7 | |
| `r4/net10`, `r4b/net8` | [`b0f04034a`](https://github.com/litedb-org/LiteDB/commit/b0f04034a78471ecb17999a8431849276b1e8af4) | both | 4,900 / 0 / 7 | |
| `r5/net10`, `r5b/net8` | [`57ef1dc3c`](https://github.com/litedb-org/LiteDB/commit/57ef1dc3cb9a1f13d6587f3c460c1cf8d1d034e1) | both | 4,902 / 0 / 7 | |
| `r6/net10.0`, `r6/net8.0` | [`ada572454`](https://github.com/litedb-org/LiteDB/commit/ada572454da8eaa9e25e95b7756ec08ebaf7f228) | both | 4,920 / 0 / 7 | `chain.txt` and compatibility-script logs |
| `p10`, `p8` | [`dbac6940a`](https://github.com/litedb-org/LiteDB/commit/dbac6940a6df6fcc516f15a23c2a39e6a909e93d) | net10.0, net8.0 | 4,940 / 0 / 7; 4,939 / 1 / 7 | net8.0: `FuzzingContract_Tests.Rebuild_waits_for_an_active_writer...` `ObjectDisposedException`, the pre-existing rebuild race listed as a residual (fixed on `dev` by [`d1189a56f`](https://github.com/litedb-org/LiteDB/commit/d1189a56fc7c3de60d7a08dcf69a21e0f33b2e1b), merged in [`55b87ec17`](https://github.com/litedb-org/LiteDB/commit/55b87ec17b4d6ad9bc8b027dfa5f345d6f8c7622)) |
| `p10b`, `p8b` | [`5e85a8b50`](https://github.com/litedb-org/LiteDB/commit/5e85a8b50e6e0f1ac947c9b64880bd99ca7673b9) | both | 4,943 / 0 / 7 | |
| `p10c`, `p8c` | [`274a39336`](https://github.com/litedb-org/LiteDB/commit/274a39336ae7c3b950a2a191089adf671b67a3bb) | both | 4,973 / 1 / 7; 4,974 / 0 / 7 | net10.0: `Issue2965_Tests`, the same race |
| `final-a6af-*` | [`a6af38881`](https://github.com/litedb-org/LiteDB/commit/a6af38881419a8cfbac7434438f1217cd0f0c5b3) | both | 4,991 / 1 / 7 | `Issue2818_FlushFallback_Tests` counted the per-write flush: [`2b3ba4b90`](https://github.com/litedb-org/LiteDB/commit/2b3ba4b902909edd8e08524de3e9fc2d8be636e4) |
| `final-f84-*` | [`f84c10f33`](https://github.com/litedb-org/LiteDB/commit/f84c10f33146afd93f80d23d1ceef4d17d19a9da) | both | 4,998 / 0 / 7 | |
| `final-d4d-*` | [`d4d88a117`](https://github.com/litedb-org/LiteDB/commit/d4d88a117426994d42e0fc82d4041e07e116770d) | both | 5,005 / 0 / 7 | |
| `final-net10.0`, `final-net8.0` | [`ceb610c80`](https://github.com/litedb-org/LiteDB/commit/ceb610c807e53e25b03a5351b240a43709d35fa5) | both | 5,008 / 0 / 7 | The run the PR cites. Only `summary.recovered-from-transcript.txt`: the scratch copy was deleted when the next run started, so its summary was taken verbatim from the session transcript and its partition logs are lost |

Local runs of the CI fuzz shards (`logs/fuzz/<run>/summary.txt`, `scripts/run-fuzz.sh`): `fuzzres` [`44909eeb9`](https://github.com/litedb-org/LiteDB/commit/44909eeb94d0703adefb021c1ecf36515936a512), `r2/fuzz` [`db41487e1`](https://github.com/litedb-org/LiteDB/commit/db41487e1f30ad7686026c73c2f4ac5e12afe900), `r3b/fuzz` [`f6e5df63d`](https://github.com/litedb-org/LiteDB/commit/f6e5df63d75a38e68e1cbb6ccec3dd0cee5d949d), `r4b/fuzz` [`b0f04034a`](https://github.com/litedb-org/LiteDB/commit/b0f04034a78471ecb17999a8431849276b1e8af4), `r5b/fuzz` [`57ef1dc3c`](https://github.com/litedb-org/LiteDB/commit/57ef1dc3cb9a1f13d6587f3c460c1cf8d1d034e1), `fz7` [`319613158`](https://github.com/litedb-org/LiteDB/commit/31961315828dfd13b0a7d875523acd4b5a17b3d0) (dirty tree), `fz8` [`3e789555f`](https://github.com/litedb-org/LiteDB/commit/3e789555fd912b32483020826ebe2931a9b51143), `fz9` [`274a39336`](https://github.com/litedb-org/LiteDB/commit/274a39336ae7c3b950a2a191089adf671b67a3bb): all 11 shards pass. `r6/fuzz` [`ada572454`](https://github.com/litedb-org/LiteDB/commit/ada572454da8eaa9e25e95b7756ec08ebaf7f228): the checksums shard fails on the two conversion-crash seeds 3163459 and 4058733, whose pinned trace hashes drifted; re-pinned in [`8ec5373b2`](https://github.com/litedb-org/LiteDB/commit/8ec5373b2187623ddf69d20d8affbf08ce4f2d1e) (`checksum-repin/` and the `fz1`, `fz2`, `fzr*`, `fzd*`, `fzm`, `fzl`, `fzci*` rows of the index). The same happened at [`ceb610c80`](https://github.com/litedb-org/LiteDB/commit/ceb610c807e53e25b03a5351b240a43709d35fa5): `fz-head` fails both seeds, `fz-pin-net8.0` and `fz-pin-net10.0` pass on both runtimes with the hashes [`ad2625283`](https://github.com/litedb-org/LiteDB/commit/ad26252831b8d2816669748b82e33c9cc7f5e776) pins.

## Database images

All images are synthetic: written by the LiteDB 5.0.21 or 6.0.0-prerelease.114 packages, the branch, or the harness in this container. None comes from a real deployment. Each zip holds its files under their scratch-relative paths, unchanged.

| Archive | Files (raw size) | What | Finding / commits |
|---|---|---|---|
| [`review-evidence/img5021b.tar.xz`](review-evidence/img5021b.tar.xz) (1.8 MB) | 40 (536.7 MB) | ten 5.0.21 crash images and the live files they were copied from (list below) | [`95d9f7ff7`](https://github.com/litedb-org/LiteDB/commit/95d9f7ff7c277551f0e9e174c736bd396cdb6036), [`3c502e92f`](https://github.com/litedb-org/LiteDB/commit/3c502e92fa713f8f906e5064d07b78d5fbf44a65) |
| [`review-evidence/legacy-format/images/img5021c.zip`](review-evidence/legacy-format/images/img5021c.zip) (146 KB) | 16 (4.8 MB) | 5.0.21 files after pragma, rebuild, collation, encrypted rebuild, checkpoint, upgrade (list below) | [`3b8744cdf`](https://github.com/litedb-org/LiteDB/commit/3b8744cdf490428299af54f5da2f497321afdd14) |
| [`review-evidence/legacy-format/images/big1.zip`](review-evidence/legacy-format/images/big1.zip) (248 KB) | 2 (14.2 MB) | 5.0.21 database with a 1,730-page WAL (`gen5021 bigcrash`), the foreign WAL that `ReviewForeignWal_Tests.cs` reads as `$SCRATCH/big1` | [`3b8744cdf`](https://github.com/litedb-org/LiteDB/commit/3b8744cdf490428299af54f5da2f497321afdd14) |
| [`review-evidence/legacy-format/images/pre2-pre3.zip`](review-evidence/legacy-format/images/pre2-pre3.zip) (10 KB) | 3 (197 KB) | 6.0.0-prerelease.114 file with two vector indexes (`genpre`), and the same after 5.0.21 `DropIndex` (`gen5021 dropindex`), read by `ReviewVectorSection_Tests.cs` as `$SCRATCH/pre2`, `pre3` | [`a6af38881`](https://github.com/litedb-org/LiteDB/commit/a6af38881419a8cfbac7434438f1217cd0f0c5b3) |
| [`images/badimages.zip`](images/badimages.zip) (246 KB) | 54 (10.3 MB) | 5.0.21 WAL images with torn or garbage tails (`d<data tail>-l<log tail>-<n>`, with `.txt` notes), made by the round-2 legacy reviewer | [`776b0a1d7`](https://github.com/litedb-org/LiteDB/commit/776b0a1d79164d1b83c85e401cd7427a7deb8ce6) blocker, [`5873fde3c`](https://github.com/litedb-org/LiteDB/commit/5873fde3c9a11820a4489f193cd5be896e9d629a) |
| [`images/crash5021.zip`](images/crash5021.zip) (3.8 MB) | 24 (14.4 MB) | 5.0.21 crash images, plain and encrypted, and their states after an interrupted drain (`after-<...>`), read by `probes/legacy-rebuild-r2/ReviewProbe.final/TailCrash_Probe.cs` | [`5873fde3c`](https://github.com/litedb-org/LiteDB/commit/5873fde3c9a11820a4489f193cd5be896e9d629a) |
| [`images/salv.zip`](images/salv.zip) (665 bytes) | 1 (41 KB) | `damaged.db` of `DamagedSalvageDuplicate_5_0_21.zip` (written by `generators/gen5021s`), where later readable parts of damaged documents repeat an `_id` | [`68931ef4f`](https://github.com/litedb-org/LiteDB/commit/68931ef4fe15c2fe602ece4254a943ba6f993487) |
| [`images/drop.zip`](images/drop.zip) (89 KB) | 9 (860 KB) | 5.0.21 files after `DropIndex` (`crash-harness` `dropmake`/`dropuse`; layouts in `logs/harness-runs/drop-cases*.txt`) | [`ac2a81f48`](https://github.com/litedb-org/LiteDB/commit/ac2a81f489daeb0aa5286d1287117af4586412a4), [`cf92a407d`](https://github.com/litedb-org/LiteDB/commit/cf92a407d0602ebf59fcc0f7a5175eb259e1440b) |
| [`images/fx.zip`](images/fx.zip) (19 KB) | 3 (246 KB) | sources of the `DropIndex_5_0_21.zip` fixture | [`e8785c4d6`](https://github.com/litedb-org/LiteDB/commit/e8785c4d60054fc7473e9fbbd26eb6720a55f7b8) |
| [`images/sc2.zip`](images/sc2.zip) (44 KB) | 7 (1.7 MB) | sources of the `WalCrash_5_0_21.zip` fixture (`crash-harness` `smallcrash`) | [`e8785c4d6`](https://github.com/litedb-org/LiteDB/commit/e8785c4d60054fc7473e9fbbd26eb6720a55f7b8) |
| [`images/fz.zip`](images/fz.zip) (452 bytes) | 1 (33 KB) | `foreign-log.db`, source of `ForeignWal_5_0_21.zip` (copy of `big1/c-log.db`) | [`3b8744cdf`](https://github.com/litedb-org/LiteDB/commit/3b8744cdf490428299af54f5da2f497321afdd14) |
| [`images/fp1.zip`](images/fp1.zip) (6 KB) | 5 (819 KB) | `fp1/orig`: source of `ConcurrentWalCrash_5_0_21.zip`; `fp1v`: the same after `gen5021 verify` | [`3c502e92f`](https://github.com/litedb-org/LiteDB/commit/3c502e92fa713f8f906e5064d07b78d5fbf44a65) |
| [`images/imgv.zip`](images/imgv.zip) (91 KB) | 8 (3.8 MB) | the `img5021c` files after the branch opened them (`gen5021b verify`) | [`3b8744cdf`](https://github.com/litedb-org/LiteDB/commit/3b8744cdf490428299af54f5da2f497321afdd14) |
| [`images/stream-opens-r2.zip`](images/stream-opens-r2.zip) (74 KB) | 18 (4.1 MB) | `a`, `b`, `c`, `f21`, `pr`, `pr2`: legacy files and caller data/log stream pairs (some with partial tails) of the round-2 streams reviewer (`generators/p21-pbr`) | [`9d141f4f8`](https://github.com/litedb-org/LiteDB/commit/9d141f4f8a0c43819a7591e9406d54b10062d82a) |
| [`images/stream-dispose-and-torn-create.zip`](images/stream-dispose-and-torn-create.zip) (3 KB) | 9 (242 KB) | `sd-*`: `LiteDatabase(Stream)` dispose scenario, 5.0.21 (`v5s`) and branch (`heads`); `small-*`: a torn 4 KB creation | [`2a5d043b6`](https://github.com/litedb-org/LiteDB/commit/2a5d043b6b79c3f93a06264b6769f8594f76206e) |

<details>
<summary>SHA-256 and size of each file in <code>review-evidence/img5021b.tar.xz</code> (40 files)</summary>

Produced at 09:48 UTC by the program in [`generators/gen5021b-original`](generators/gen5021b-original) (reconstructed; see the generator table) against the LiteDB 5.0.21 package: for each of `initial` (`Initial Size=4MB`), `bigwal` (400 inserts, WAL far larger than the data file), `churn` (drop, delete and reinsert of 200 KB and 100 KB documents), `rollback` (a rolled-back 3,000-insert transaction after safepoints) and `grown` (checkpointed, then grown in the WAL), plain and with a password, `X.db`/`X-log.db` is the database left open and `X-crash.db`/`X-crash-log.db` the copy taken while the writer held it (the crash image). Consumed by [`probes/sync-durability/ReReview3_Tests.cs`](probes/sync-durability/ReReview3_Tests.cs) (`Legacy_5_0_21_crash_images_open`) and by a throwaway `ZzImages_Tests` of the main session (not kept). Note: `review-evidence/legacy-format/gen5021b-Program.cs` is **not** this program; it is the later one that wrote `img5021c`.

| File | Bytes | SHA-256 |
|---|---:|---|
| `img5021b/bigwal-crash-log.db` | 23,838,720 | `030ddb5d6e6e173dffed0c6a25b691ce1a5281ce2b7504625120b99049db93bd` |
| `img5021b/bigwal-crash.db` | 8,192 | `8e5c9b8321dffa0faf7f357ffcf857673606f172c3bb3350fa158a3623b8242b` |
| `img5021b/bigwal-log.db` | 23,838,720 | `030ddb5d6e6e173dffed0c6a25b691ce1a5281ce2b7504625120b99049db93bd` |
| `img5021b/bigwal.db` | 8,192 | `8e5c9b8321dffa0faf7f357ffcf857673606f172c3bb3350fa158a3623b8242b` |
| `img5021b/churn-crash-log.db` | 58,335,232 | `c239a9bb57c7b462d12d609e9f3396fdabe3e4cd8d6bb81c09f2827d85f3d7bf` |
| `img5021b/churn-crash.db` | 8,192 | `c3b599959726abb79e14da91c5f81b1c9830175054e9f811dfb8e5c04591ef5f` |
| `img5021b/churn-log.db` | 58,335,232 | `c239a9bb57c7b462d12d609e9f3396fdabe3e4cd8d6bb81c09f2827d85f3d7bf` |
| `img5021b/churn.db` | 8,192 | `c3b599959726abb79e14da91c5f81b1c9830175054e9f811dfb8e5c04591ef5f` |
| `img5021b/enc-bigwal-crash-log.db` | 23,863,296 | `1eb81e54b2069a33ef6f032f7e77dd77c47f25bde8d866bdf1e8c0593ade4ef5` |
| `img5021b/enc-bigwal-crash.db` | 16,384 | `dbf9e32f2ce49127c6e8cfe7a89bb1a351b066d7132fe5b802ba00739d20bc4e` |
| `img5021b/enc-bigwal-log.db` | 23,863,296 | `1eb81e54b2069a33ef6f032f7e77dd77c47f25bde8d866bdf1e8c0593ade4ef5` |
| `img5021b/enc-bigwal.db` | 16,384 | `dbf9e32f2ce49127c6e8cfe7a89bb1a351b066d7132fe5b802ba00739d20bc4e` |
| `img5021b/enc-churn-crash-log.db` | 58,343,424 | `169673df8a72e833c9cac21d063529687b21df2f063a058d021513414df5995c` |
| `img5021b/enc-churn-crash.db` | 16,384 | `90f41eaeb26838c11830191d4eb0dc61915be6fc6631f19736ff24e07f4e24ce` |
| `img5021b/enc-churn-log.db` | 58,343,424 | `169673df8a72e833c9cac21d063529687b21df2f063a058d021513414df5995c` |
| `img5021b/enc-churn.db` | 16,384 | `90f41eaeb26838c11830191d4eb0dc61915be6fc6631f19736ff24e07f4e24ce` |
| `img5021b/enc-grown-crash-log.db` | 16,769,024 | `1a9a0b2b964cf6f9e2c7c606fb82c507ada28dc90ba6397dd68dd770ca7eb680` |
| `img5021b/enc-grown-crash.db` | 851,968 | `67c1dfcbf1ec3f73c14668f89ba18b281cd705697a0c1b400bd14af699b2dddf` |
| `img5021b/enc-grown-log.db` | 16,769,024 | `1a9a0b2b964cf6f9e2c7c606fb82c507ada28dc90ba6397dd68dd770ca7eb680` |
| `img5021b/enc-grown.db` | 851,968 | `67c1dfcbf1ec3f73c14668f89ba18b281cd705697a0c1b400bd14af699b2dddf` |
| `img5021b/enc-initial-crash-log.db` | 7,004,160 | `ef0796c62c352c649d961efc871e566a577ebe15b7febd0aed417a39186c17a6` |
| `img5021b/enc-initial-crash.db` | 16,384 | `b40e160cf8874d99c3afaead4dc360c8d488829bb88c46c50718dee5c263c3f5` |
| `img5021b/enc-initial-log.db` | 7,004,160 | `ef0796c62c352c649d961efc871e566a577ebe15b7febd0aed417a39186c17a6` |
| `img5021b/enc-initial.db` | 16,384 | `b40e160cf8874d99c3afaead4dc360c8d488829bb88c46c50718dee5c263c3f5` |
| `img5021b/enc-rollback-crash-log.db` | 25,116,672 | `7a228e21667654e67974d899f71090007b798e23d6d8b360802672fa1fde6609` |
| `img5021b/enc-rollback-crash.db` | 16,384 | `5d755b1852460421ed7724794e03b84624fd246b91609eb1a1a7d1a5f4c54561` |
| `img5021b/enc-rollback-log.db` | 25,116,672 | `7a228e21667654e67974d899f71090007b798e23d6d8b360802672fa1fde6609` |
| `img5021b/enc-rollback.db` | 16,384 | `5d755b1852460421ed7724794e03b84624fd246b91609eb1a1a7d1a5f4c54561` |
| `img5021b/grown-crash-log.db` | 16,834,560 | `1905edf05672c25ebf0f57242583ededcfc74bb94596e1b51e91cdd2f20c86e5` |
| `img5021b/grown-crash.db` | 1,048,576 | `e9dc989de1d93fcf68556dce31d046793cb19b5b3de929731946fb1791081b52` |
| `img5021b/grown-log.db` | 16,834,560 | `1905edf05672c25ebf0f57242583ededcfc74bb94596e1b51e91cdd2f20c86e5` |
| `img5021b/grown.db` | 1,048,576 | `e9dc989de1d93fcf68556dce31d046793cb19b5b3de929731946fb1791081b52` |
| `img5021b/initial-crash-log.db` | 6,963,200 | `d62f30a66e0a1a2f471a037bb5f31f7aedaaa0de36279a1cc269559bb6afb541` |
| `img5021b/initial-crash.db` | 4,194,304 | `26c1c06dd19b65937b4b170db85645d7c7568367aef9471d1fa1aa0cfec58236` |
| `img5021b/initial-log.db` | 6,963,200 | `d62f30a66e0a1a2f471a037bb5f31f7aedaaa0de36279a1cc269559bb6afb541` |
| `img5021b/initial.db` | 4,194,304 | `26c1c06dd19b65937b4b170db85645d7c7568367aef9471d1fa1aa0cfec58236` |
| `img5021b/rollback-crash-log.db` | 25,108,480 | `bd51575723827615f2a3656a75429a2d26a305b8c791c19d935f93061d140af9` |
| `img5021b/rollback-crash.db` | 8,192 | `f6bfd9a0a6c54b1dbb551205f86c322ef302b1327e5a79e0f4dca0df77a82e83` |
| `img5021b/rollback-log.db` | 25,108,480 | `bd51575723827615f2a3656a75429a2d26a305b8c791c19d935f93061d140af9` |
| `img5021b/rollback.db` | 8,192 | `f6bfd9a0a6c54b1dbb551205f86c322ef302b1327e5a79e0f4dca0df77a82e83` |

</details>

<details>
<summary>SHA-256 and size of each file in <code>review-evidence/legacy-format/images/img5021c.zip</code> (16 files)</summary>

Written by [`generators/gen5021b`](generators/gen5021b) (the reviewer "legacy-format fixes", 10:55 UTC) with LiteDB 5.0.21: the file after `pragma`, `rebuild`, `rebuild-collation`, `encrypted-rebuild`, `checkpoint0` and `upgrade`, to show that no legitimate 5.0.21 WAL commits a header with another creation time ([`3b8744cdf`](https://github.com/litedb-org/LiteDB/commit/3b8744cdf490428299af54f5da2f497321afdd14)). The scratch folder `img/` was an identical copy; `review-evidence/legacy-format/ReviewImages_Tests.cs` reads it as `$SCRATCH/img`.

| File | Bytes | SHA-256 |
|---|---:|---|
| `img5021c/checkpoint0/d-log.db` | 2,244,608 | `4ef0b50ad6c802576930cfbf3e6aa6d8e1c3eaea7fcaa54d19cdc9a55558ef55` |
| `img5021c/checkpoint0/d.db` | 262,144 | `59f00d0358e3195cb981829e1c6016761f41d48494e2c854fca1b884b22d174e` |
| `img5021c/encrypted-rebuild/d-backup.db` | 270,336 | `3ba99df484eea1ab41ecc0b6f435f39c4c4be96e8c04e16fd6ce6c474a4060cd` |
| `img5021c/encrypted-rebuild/d-log.db` | 65,536 | `8c1473b1be620a2334d590321d7f3329cd3995786876f0484b6589dbea002342` |
| `img5021c/encrypted-rebuild/d.db` | 270,336 | `4a4876d7ed0f085698700828d3729b44e2adb601eff72a806c33b754728f7346` |
| `img5021c/pragma/d-log.db` | 90,112 | `3c387ef21425e31ffac0cc15832c107dc3b665f11ee4937192194813de67e6ea` |
| `img5021c/pragma/d.db` | 262,144 | `caaa68588015da414ca665d8290640ca57713c59aca251b3161a711f42a9ff41` |
| `img5021c/rebuild-collation/d-backup.db` | 262,144 | `8b6fc797f1a2f3d6ae2aa53d77539b6d1407de4694a734bae2002038ae173bf9` |
| `img5021c/rebuild-collation/d-log.db` | 57,344 | `ed620533b08f151b43ff9e1df171eb9646bb47016fd2405e5231a085204ca472` |
| `img5021c/rebuild-collation/d.db` | 262,144 | `642fd8fd2b7795b6203bdbc0b347a8086fe5fe570413583c438fb458d540ce9d` |
| `img5021c/rebuild/d-backup.db` | 262,144 | `f1ad522e13cb7df4d317b97aae03ec8e433884fbbdeb6c034abb126ca9b1f9f7` |
| `img5021c/rebuild/d-log.db` | 49,152 | `b79671f268b6355bf20d750c873684aaab186d563505c6d131c83a5aa920b968` |
| `img5021c/rebuild/d.db` | 139,264 | `00496321e5420d3a1903e4cc98f0a0713b8d49581797c0d69876fd2ca89967a8` |
| `img5021c/upgrade/d-backup.db` | 73,728 | `af6864cf7ba5a3f836b34ea736934e5d7a38e78071eeba30ef58c0c61f9b7348` |
| `img5021c/upgrade/d-log.db` | 65,536 | `c8fcb76c9da8956b8573e83dce902554632ebfe2fce475ba81f75045efa13217` |
| `img5021c/upgrade/d.db` | 139,264 | `277e77faa3a27a60206e19a92f8167911f42fe7e538a249529cff9413775cba4` |

</details>

Not archived (per-file SHA-256 and size in [`images/not-archived.sha256.tsv`](images/not-archived.sha256.tsv)):

| Scratch folder | Files | Bytes | Why | Source |
|---|---:|---:|---|---|
| `r3a/images` | 670 | 34,865,766 | too large (35 MB); derived by the probe from the 5.0.21 crash fixtures | `probes/drain-rebuild-retry-r3/TornTailSweep_Probe.cs` |
| `shmig` | 9 | 19,251,200 | too large (19 MB, 3.9 MB compressed) | `crash-harness` `scen crashimage`, shared-mode migration with an unreadable reader registry ([`31da4d5d7`](https://github.com/litedb-org/LiteDB/commit/31da4d5d7ef0179841594b4c22dbcd292167f2f0)) |
| `sc` | 6 | 8,691,712 | too large (8.7 MB, 4.8 MB compressed) | `crash-harness` `small`: legacy `Cache Size` key, read-only `FileStream`, torn creation |
| `mig` | 3 | 165,126,144 | too large (165 MB) | `crash-harness` `migrate`: 40 interrupted-migration trials, output in `logs/harness-runs/mig1.out` |
| `runs` | 7 | 257,966,080 | too large (258 MB) | `crash-harness` parent/child crash runs, outputs in `logs/harness-runs/*.out` |
| `bank` | 1 | 868,352 | stress output, no finding | `crash-harness` `bank` |
| `perf` | 1 | 1,908,736 | benchmark output | `crash-harness` `perf` (single-insert fsync cost, 5.0.21 against the branch) |
| `ss5` | 1 | 1,540,096 | stress output, no finding | `crash-harness` `sparent` on 5.0.21 |
| `ssh` | 1 | 1,540,096 | stress output, no finding | `crash-harness` `sparent` on the branch |
| `mx/heads` | 1 | 40,960 | probe output | shared-mode open in a long non-ASCII directory ([`01831c79f`](https://github.com/litedb-org/LiteDB/commit/01831c79fbd0ee41bb7f07a2dc73100ede183c98)) |
| `mx/v5s` | 2 | 81,920 | probe output | same with 5.0.21 |
| `gen5enc/out` | 6 | 319,488 | generator output; the fixture is committed | `generators/gen5enc` |
| `gen5u` | 5 | 180,224 | generator output; the fixture is committed | `generators/gen5u` |
| `gen5keys` | 1 | 139,264 | generator output; the fixture is committed | `generators/gen5keys` |
| `gen5021s/out` | 1 | 40,960 | generator output; the fixture is committed | `generators/gen5021s` |
| `genpre` | 1 | 40,960 | generator output; the fixture is committed | `generators/genpre-original` |
| `pre114/out` | 3 | 229,376 | generator output; the fixture is committed | `generators/pre114` |
| `r3c21` | 5 | 376,832 | generator output | `generators/r3c21` |
| `r3a/p21b` | 1 | 32,768 | generator output | `generators/r3a/p21b` |
| `r3a/vecthrow.db` | 1 | 40,960 | generator output | `generators/r3a/genvec` |
| `r4a/damaged.db` | 1 | 32,768 | generator output | `generators/r4a/p21` |
| `img` | 16 | 4,775,936 | identical to `review-evidence/img5021c` |  |
| `big1` | 2 | 14,204,928 | identical to `review-evidence/big1` |  |
| `pre2` | 1 | 65,536 | identical to `review-evidence/pre2` |  |
| `pre3` | 2 | 131,072 | identical to `review-evidence/pre3` |  |

## Skipped

- `bin/` and `obj/` of every project (152 MB), the `.trx` files of the test runs (267 files, 178 MB), build logs (51 files) and the per-mutation `*.build.log` files.
- `fixsrc/` and `headsrc/` (source trees, commits above), `pkgs/` (the nuget.org packages LiteDB 6.0.0-prerelease.47 to .53, 16 MB, used by the round-2 legacy reviewer for layout checks), `dotnet-install.sh` (Microsoft's installer script).
- The fuzz artifact folders (`fuzzres`, `fz*`, `r*/fuzz`, about 2.1 GB of working databases and corpora); their results are in `logs/fuzz/fuzz-results-index.tsv`.
- Snapshots that are byte-identical to a committed file (`*.bak`, `*.orig`, `o*.cs`, `r6final/`, `r7final/`, `r8final/`, `r9/`, `stage/`, `mut/*.orig`) and fixture zips identical to `LiteDB.Tests/Resources`: listed with their commit in `review-evidence/snapshots/identical-to-commits.tsv`. Top-level copies of `candidate-fix.diff`, `identity-fix.diff` and `ConcurrentWalCrash_5_0_21.zip` duplicate files in `review-evidence/legacy-format/`.
- `prov/`: the working folder of the session that wrote [`c70147bf6`](https://github.com/litedb-org/LiteDB/commit/c70147bf6b18f121a110e5d635c7c1650555c5c2) (still in use when this folder was assembled; it includes a 15 MB extraction of the session transcript).
- Empty files (`chain*.log`, `chain6.out`, `fz7.out`, `size.txt`).
- Work the session started while this folder was assembled (after 14:55 UTC): the full-suite run for the new head (`final-net10.0/` was recreated for it, see the test-run table), git worktrees (`devwt`, `fixwt`, `fuzzwt`, `prodwt`, `revwt11`), hosted-CI downloads (`citrx/` test results, `ck-fail.log`), `fz-pin2-*` fuzz runs and `tmproot/`.

## Reproducing

- **A probe:** check out the commit named in its row, copy the `.cs` file into `LiteDB.Tests/Regressions/`, unpack any images it reads and replace `$SCRATCH` in its paths, then
  `dotnet test LiteDB.Tests -c Release -f net10.0 -p:TestingEnabled=true --settings tests.runsettings --filter "FullyQualifiedName~<ClassName>"`.
- **The crash harness:** `dotnet run --project crash-harness/v5s -c Release -- <command> ...` (commands in `crash/Crash.cs`, `Main`); for the branch, point the `ProjectReference` of `crash-harness/heads/heads.csproj` at a LiteDB checkout.
- **A fixture:** prefer the maintained generators of [`c70147bf6`](https://github.com/litedb-org/LiteDB/commit/c70147bf6b18f121a110e5d635c7c1650555c5c2); the committed fixtures differ from regenerated ones only in creation times, AES salts, prerelease vector pages and ZIP metadata ([`RegressionFixtures.md`](https://github.com/litedb-org/LiteDB/blob/d6d7f16cfb52e76b2e6d5420e09489aa95689cc9/LiteDB.Tests/Resources/RegressionFixtures.md)).
- **Images:** `unzip X.zip` (or `tar -xJf review-evidence/img5021b.tar.xz`) and check with `grep -v '^#' images/SHA256SUMS | sha256sum -c` or the tables above.
