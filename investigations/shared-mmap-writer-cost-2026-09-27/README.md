# Shared mmap writer-cost experiments

Follow-up to [LiteDB#3013](https://github.com/litedb-org/LiteDB/pull/3013) and
[LiteDB#3014](https://github.com/litedb-org/LiteDB/pull/3014), 27 September 2026.
This folder preserves successful and rejected experiments, raw production timing,
fault/restart evidence, and revision provenance. The attached plan was treated as
a set of hypotheses to test independently.

The candidate keeps the archived mmap admission architecture. Its main gains come
from avoiding unnecessary cache invalidation and reducing cached-reader CPU demand
while a writer is active. Reducing atomic publication costs alone did not recover
the hosted writer regression. Ratio-ten pacing, mapped leases, early cache
retirement, shorter spinning, streaming yields, and checkpoint rescans were
measured independently; none justified inclusion in the selected implementation.

Qualification is still running at this evidence snapshot. In particular, the
initial exploratory goal of a uniform 5% writer throughput/p99 bound has **not**
been established. Saturated reader capacity and writer latency form a tradeoff;
an interval crossing zero is not proof of equivalence. There is no hard writer
latency guarantee.

## Source identity

| Role | LiteDB commit |
| --- | --- |
| Merged #3013/#3014 baseline | `c12b6a72149ad2166d62769d07efe961198b7a38` |
| Archived implementation | `130b339523cbe61efcef52b5d67252aee5a6cb53` |
| Byte-identical restoration on the merged stack | `16131953114971557b44e9deaa78bd8ce9d45ab1` |
| Ratio-seven candidate before hint completion | `e81c28bb864e09f48a2025fe8aad360d65baa27e` |
| Current library, including hint completion | `093c6a96fe3dae528e4919758b3e8759437fa8ad` |
| Current library plus core workflow selection | `eea594b79edf6b52342b32fb1840616e6cf948ae` |
| Phase-tail benchmark records, unchanged library | `9f4867c7818f47fceaa94838bb42f720ee8b1dc3` |
| Additional disposal tests, unchanged library | `c6053fdd9611fbe50592ae6ef65258096753f8c7` |

Each hosted directory retains `base.txt`, `head.txt`, `runtime.txt`, and JSONL
records with actual DLL SHA-256. Diagnostics that modified disposable source
checkouts retain their patch and are excluded from production performance claims.
The chronological investigation notes are in [investigation-log.md](investigation-log.md).
`source-identity.json` also verifies that the restored library tree matches the
archive and that `093c6a96f`, `eea594b79`, `9f4867c78` and `c6053fdd9` have the same
library source tree. Benchmark/test-only commits are not new library variants.

## Latest saturated comparison

Run [36318836474](https://github.com/litedb-org/LiteDB/actions/runs/36318836474)
compares the current library directly with the merged baseline: .NET 10, four
reader processes, one unthrottled writer, five serial alternating pairs. Each
process warms for 10 seconds and measures for 10 seconds. The writer updates all
200 records, each with a 4,000-character payload, per durable transaction. Reads
are points, 50-row indexed scans, or 200-row indexed scans. Checkpoint mode adds
an explicit checkpoint after every transaction; ordinary automatic checkpoint
behavior remains enabled in writer mode.

| Host/activity/read shape | Reader calls/s, medians | Writer txn/s, medians | Mean paired writer change | Mean paired writer p99 change |
| --- | ---: | ---: | ---: | ---: |
| Linux checkpoint, point | 583 → 7,486 | 68.43 → 65.77 | −3.76% | −0.07% |
| Linux checkpoint, medium | 342 → 1,586 | 58.50 → 56.93 | −2.75% | +4.54% |
| Linux checkpoint, large | 323 → 479 | 52.77 → 51.95 | −1.53% | +6.45% |
| Windows writer, point | 259 → 12,344 | 64.73 → 60.65 | −6.01% | +11.56% |
| Windows writer, medium | 228 → 2,105 | 55.84 → 58.37 | +7.98% | +1.36% |
| Windows writer, large | 222 → 606 | 58.87 → 59.68 | +4.61% | +9.38% |

These selected rows explain the tradeoff; the archive retains every host/scenario,
including adverse and inconclusive results. Windows checkpoint point p99 increases
9.18%. The Linux writer host has long, variable commit tails: point p99 increases
82.58%, with a wide [−5.88%, +171.03%] exploratory interval. This is retained as an
unresolved performance observation, not discarded or used to claim equivalence.
Phase data locate delays within updates, explicit checkpoints, and commits. Lower
aggregate CPU consumption under pacing supports resource contention as a hypothesis;
these measurements do not identify a hardware bottleneck or a single cause for
every regression.

Across this matrix, point allocations/read fall from roughly 179–181 KB to
52–84 KB. Medium/large reads also allocate less. Some large-scan reader p99s rise,
despite higher throughput. Sampled WAL peaks can rise from about 1.63 MiB to
6.48 MiB while reads are active. Every case validates all records, payloads,
generations and indexed results, then verifies that final close empties the WAL.
This demonstrates eventual cleanup in the tested workload, not a bound on WAL
growth under arbitrary retained cursors. RSS, retained managed bytes, allocations,
and WAL size are separate measurements in the raw records.

## Reader retention and ordinary workloads

The archived comparison [36316627061](https://github.com/litedb-org/LiteDB/actions/runs/36316627061)
uses `e81c28bb8`, before explicit hint completion. Idle point throughput improves
13–17% on Linux and 31–36% on Windows; idle large scans are approximately unchanged.
Under four saturated Linux readers, pacing loses 35–42% of archived point-reader
throughput while recovering about 12–15% writer throughput. Medium/large readers
can improve, and Windows scan/checkpoint cases trade reader throughput for writer
progress. The full matrix is retained; archived active-reader speed is not uniformly
preserved. This comparison is not represented as a measurement of the later hint
completion change.

Current core comparison [36317773622](https://github.com/litedb-org/LiteDB/actions/runs/36317773622)
on Linux/Windows .NET 8/10 reduces point mean latency by 83–86%, scan mean by
35–54%, and mixed-workload mean by 36–55%. Mixed p99 intervals remain inconclusive.
An earlier Windows mixed-workload regression caused by an expired-but-not-completed
scheduling hint was reproduced and fixed: completion now clears only its own
atomic request. Earlier failed performance results remain in the archive.

## Safety and limits

The protocol does not change the database or WAL format, require a rebuild, or use
the mmap control page as a durable commit record. Concurrent participants must use
the same exact LiteDB version, canonical database path and mutex naming policy.
Concurrent Direct/Shared, mixed versions, path aliases and cross-machine access
remain outside the contract. Unsupported mappings/volumes retain the existing
mutex protocol, with authoritative revocation before fallback writes.

Tests cover post-lease validation, reclaimed-address negative controls, every
retained generation, startup mutations and interrupted recovery, failed reuse
batches, timer/stream ownership, native process death, revocation, and disposal
before admission. The structural-scope mutation control fails 12 of 14 tests when
the guard is deliberately removed; the two clean-open controls continue to pass.
An earlier command used an incorrect mutant path and ran 14 unmodified controls;
both logs are retained and are not mislabeled as mutant detection.

The current hint-completion library passes 696 selected Shared/coordinator/WAL
cases on each of .NET 8 and .NET 10. Eight scheduling/stream/disposal cases pass on
each runtime after the extra disposal cases. The clean frozen `eea594b79` campaign
passes all 12 invocations and 18 reports: seeds 3012–3014, four targets, 32 steps,
six built-in replays, exact clean revision, matching before/after binary hashes.
The campaign manifests and all generated synthetic databases/reproduction inputs
are included. No database came from a real deployment.

Earlier full native CI [36316475858](https://github.com/litedb-org/LiteDB/actions/runs/36316475858)
passed all 47 jobs at `e81c28bb8`. Verified TRX includes actual Linux Arm64
.NET 10.0.12 (4,699 passed, seven pre-existing unrelated skips) and Windows X86
.NET 8.0.31 Shared selection (84 passed). The x86 timeout artifact is an intentional
dump-contract smoke; it captured and validated a full 32-bit dump successfully.
This earlier matrix does not qualify later source changes. Current native CI is
[36320688022](https://github.com/litedb-org/LiteDB/actions/runs/36320688022).

The SC model enumerates 210 bounded admission schedules without a counterexample;
its unsafe variants produce counterexamples. It is not a weak-memory proof.
Native ARM64 execution supplies additional evidence. Process termination is not
power loss: persistence tests model explicit torn/lost writes and failed flushes,
subject to documented filesystem/device durability guarantees. Finite tests cannot
establish arbitrary crash, filesystem or hardware behavior.

## Archive and reproduction

`evidence.tar.gz` contains raw measurements, logs, TRX, patches, finite campaign
outputs, and helper scripts. [manifest.json](manifest.json) names every file and
records original and published SHA-256 plus sizes. Published copies normalize
machine-specific paths/host text; original files remain unchanged locally. The
packager verifies that every numeric/boolean/null JSON value is unchanged and
validates JSON syntax after normalization. The archive excludes compiled binaries,
Git worktrees, downloaded external source, and PR comment copies.

The generated [reader-comparisons.csv](reader-comparisons.csv) covers every retained
hosted reader comparison. `tabulate-readers.py` reproduces it; its reader p99 fields
are medians of individual process p99s, not a percentile of pooled requests.

Extract into a new directory, then use `summarize.py <directory>/readers.jsonl` or
`summarize-standard.py <directory>/paired.jsonl`. Reader summaries report median
absolute values, mean paired percentage changes and a deterministic 10,000-resample
95% bootstrap interval. Five pairs and many comparisons make these exploratory,
not a multiple-comparison-adjusted equivalence or universal latency guarantee.

Build both exact source revisions in separate worktrees, Release with
`TestingEnabled=false`; compile the same benchmark source against each library.
Use `scripts/measure-shared-readers.py` with five rounds, one/four readers and all
three scenarios/activities. `--reader-interval-ms 50` supplies 20 reads/s per
process instead of saturation. Writers remain unthrottled. Each run records offered
and completed work plus API and intended-arrival latency, so missed demand cannot
be counted as completed throughput. Do not overlap timing with builds/tests on the
same host. Local runs used Linux x64, Ryzen 9 3900X and ext4 scratch; hosted runtime
and OS details are in each `runtime.txt`. No hardware performance counters were
available locally (`perf_event_paranoid=4`); no machine privileges were changed.

## Rejected final refinements

Ratio-ten pacing (`9935d3a0c`, run [36319814711](https://github.com/litedb-org/LiteDB/actions/runs/36319814711))
loses 13–38% reader throughput for roughly 1–5% writer throughput improvement;
the selected ratio remains seven. Unconditional checkpoint rescanning helps some
medium/large cases but slows point checkpoints. Restricting the rescan to accumulated
WAL (`fa8ec1c4`, run [36320186315](https://github.com/litedb-org/LiteDB/actions/runs/36320186315))
mostly produces inconclusive throughput changes and increases Windows large-writer
p99 by 8.64% [+2.31%, +16.29%]. Neither storage-path rescan is included. Their six
plaintext/encrypted safety cases and failed/corrected test-helper outputs remain
available as experiment evidence, not safety claims for unimplemented paths.

A final `Thread.Sleep(0)` handoff experiment (`471dcc98a`, run
[36320855823](https://github.com/litedb-org/LiteDB/actions/runs/36320855823)) tests
a scheduling alternative to the current-processor behavior documented for
[Thread.Yield](https://learn.microsoft.com/en-us/dotnet/api/system.threading.thread.yield?view=net-10.0).
Writer-only results are mostly unchanged, while Linux checkpoint point p99 rises
8.07% [+1.97%, +17.92%] and medium writer throughput falls 1.77% with p99 +4.68%.
It is rejected. The main implementation retains `Thread.Yield` for zero delay.

## Equal offered read demand

Run [36320616636](https://github.com/litedb-org/LiteDB/actions/runs/36320616636)
compares the current library at `9f4867c78` directly with the merged baseline on
Linux/Windows .NET 8/10, both writer activities, every read shape, one/four readers.
Every reader is offered 20 calls/s (20 or 80 calls/s total); writers are unthrottled.
All 480 records validate, and all 240,000 offered reads complete. Differences in
reported calls/s around 19.98/79.9 reflect the actual measurement duration.

Linux writer throughput changes range from −2.88% to +0.72% across the 24 cells.
The .NET 10 large/four checkpoint cell changes −0.53% [−0.95%, −0.09%] in throughput
and +3.68% [+2.04%, +5.29%] in p99. Tails are not uniformly equivalent: Linux .NET 8
point/one checkpoint p99 has +21.19% [+3.62%, +55.41%] paired mean, although median
absolute p99 is 27.36 → 28.37 ms; large/one is +10.64% with a wide interval.

Windows .NET 10 checkpoint throughput stays between −2.02% and +1.06%, with p99
changes between −1.09% and +0.92%. Windows .NET 8 large/four checkpoint throughput
changes −3.68% [−5.65%, −1.33%] and p99 +8.29% [+5.79%, +10.80%]. Windows .NET 10
medium/four writer throughput changes −5.71% [−9.92%, −1.51%], with p99 +2.97%
[−0.52%, +6.51%]. Other Windows cells include large, uncertain positive effects
from variable baseline timing; they are not advertised as reliable gains.

This separates throughput cost at matching demand from extra work at saturation.
It supports modest throughput overhead in most tested cells, but does not prove a
uniform 5% bound, eliminate the tail regressions, or identify a single root cause.
