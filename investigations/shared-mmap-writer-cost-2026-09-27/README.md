# Shared mmap writer-cost experiments

Follow-up to [LiteDB#3013](https://github.com/litedb-org/LiteDB/pull/3013) and
[LiteDB#3014](https://github.com/litedb-org/LiteDB/pull/3014), 27 September 2026.
This folder preserves successful and rejected experiments, raw production timing,
fault/restart evidence, and revision provenance. The attached plan was treated as
a set of hypotheses to test independently.

The candidate keeps the archived mmap admission architecture. Eligible .NET 8+
Shared connections gain cached reads automatically, without a new public API. Its main gains come
from avoiding unnecessary cache invalidation and reducing cached-reader CPU demand
while a writer is active. Reducing atomic publication costs alone did not recover
the hosted writer regression. Ratio-ten pacing, mapped leases, early cache
retirement, shorter spinning, streaming yields, and checkpoint rescans were
measured independently; none justified inclusion in the selected implementation.

The selected revision is `186cdeec6bbc76d949b77c726c54a492dbc6d3cf`; all reported
final qualification and comparison runs have completed. The
initial exploratory goal of a uniform 5% writer throughput/p99 bound has **not**
been established. Saturated reader capacity and writer latency form a tradeoff;
an interval crossing zero is not proof of equivalence. There is no hard writer
latency guarantee.

## Exact-head archived comparison

[All 36 cells](final-archived-comparison.md) compare final `186cdeec6` directly with
the byte-identical archived implementation, on Linux/Windows .NET 10 with one/four
readers and five alternating pairs. Idle point throughput improves 21–26% on Linux
and 30–33% on Windows. Idle medium scans improve 1.7–2.5% on Linux and 5.8–5.9% on
Windows; large scans are approximately unchanged, with wider Windows intervals.

With four Linux point readers and explicit checkpoints, reader medians change
11,582→7,390 calls/s (paired −36.40% [−40.45%, −33.34%]), writer throughput improves
15.29% [+13.52%, +17.14%], and writer p99 falls 14.59% [−16.25%, −12.93%]. Medium
checkpoint readers and writers both improve, while large/four writer throughput
falls 3.53% [−4.12%, −2.91%] and writer p99 rises 6.97% [+4.24%, +9.70%]. This
preserves or improves idle capacity but does not preserve every active-reader rate.

The Linux writer-only host has long, variable commit tails in both variants.
Readers improve in all shapes, consistent with the narrower writable-open scopes
allowing cached reads to continue. This is not an isolated attribution of this matrix’s gains. Writer changes are mostly inconclusive; the
point/four mean paired improvement is +29.89% [+2.54%, +58.63%], while its median
absolute rates are only 20.25→21.08 transactions/s. The paired mean must not be
substituted for the median-rate ratio or advertised as a uniform improvement.

Windows four-reader checkpoint cases recover writer throughput at a larger reader
cost. Point readers change 10,049→4,139 calls/s (−58.82% [−65.70%, −53.76%]), writer
throughput improves 26.91% [+13.52%, +46.84%], and writer p99 falls 25.35%. Medium
readers change 2,342→1,107 calls/s (−53.06% [−55.77%, −50.34%]), writer throughput
improves 48.08% [+37.25%, +60.11%], and writer p99 falls 43.12%. Large/four writer
throughput has paired +68.78% [+29.41%, +113.89%] with absolute medians 27.45→35.98;
reader paired change −12.14% has a wide interval [−30.06%, +16.99%]. Single-reader
writer throughput changes remain small/inconclusive, although point writer p99
increases 7.31% [+2.76%, +14.39%]. The full table preserves these limits.

Windows writer-only results also show the reader/writer tradeoff. With four
readers, point/medium/large writer throughput improves 22.95% / 58.54% / 23.22%,
while reader throughput changes −59.07% / −58.53% / −33.37%. Corresponding writer
p99 changes are −37.56% / −51.08% / −17.21%. With one medium reader, however,
writer throughput falls 6.98% [−13.84%, −0.11%] and p99 rises 35.88%
[+4.11%, +93.54%], while readers improve about sevenfold. The final candidate
improves several useful operating points; it does not dominate the archive in
every metric or workload.

## Exact-head ordinary workloads

The final production comparison at `186cdeec6`
([36326327445](https://github.com/litedb-org/LiteDB/actions/runs/36326327445)) uses
the merged `c12b6a721` baseline. All eight jobs complete successfully. Its core jobs report these mean paired
latency changes over five pairs:

| Host/runtime | Point mean | Scan mean | Mixed mean | Mixed p99 |
| --- | ---: | ---: | ---: | ---: |
| Linux .NET 8 | −83.27% | −38.68% | −51.97% | +3.31% [−0.25%, +6.76%] |
| Linux .NET 10 | −82.41% | −44.35% | −48.87% | +5.52% [−10.57%, +21.61%] |
| Windows .NET 8 | −85.12% | −38.72% | −53.62% | +2.69% [−5.62%, +11.24%] |
| Windows .NET 10 | −87.61% | −45.27% | −55.08% | +21.69% [+8.84%, +34.53%] |

Windows .NET 10 mixed p99 has median 0.7874→1.0389 ms. The mean improvement does
not remove this tail cost. The mixed scenario's operation percentile combines
reads and writes; the writer-only measurements are reported separately.

All 80 two/four-writer contention records validate and end with an empty WAL.
The final matrix's aggregate throughput paired changes range −10.26% to +10.20%,
with wide intervals for both extremes. Linux .NET 8 two-writer throughput changes
−1.65% [−2.87%, −0.43%]; the remaining six intervals include zero. Scheduled
workers complete only approximately 5–9% of offered work in both versions under
an unthrottled competing writer, with maximum-worker arrival p99 near 9–10 seconds.
These are overloaded workloads, not evidence of fairness or bounded waits.
All eight same-version interop records (baseline/candidate on four hosts/runtimes)
pass process-death, rollback, logical/index integrity, and final cleanup checks.

Windows .NET 10 pure-write mean changes −0.52% [−1.37%, +0.24%] and p99 −1.11%
[−3.19%, +0.82%]. Windows .NET 8 pure-write mean changes +2.78% [−4.72%, +12.34%]
and p99 +8.68% [−9.67%, +39.90%]. Windows .NET 10 checkpoint p99 increases 16.66%
[+2.90%, +30.08%], despite mean +2.26% [−0.71%, +5.11%]. Windows .NET 8 churn mean
increases 3.25% [+1.31%, +5.48%]. These are operation timings, not concurrent
reader/writer throughput measurements.

Linux .NET 10 traffic contains large write tails in both variants and an adverse
candidate effect: pure-write mean paired change +114.38% [−9.86%, +315.84%], p99
+205.82% [+6.52%, +513.65%]. Median pair means are 1.891→2.643 ms; median pair p99s
39.01→87.88 ms. Individual candidate means range 1.091–6.118 ms and baseline
1.009–2.365 ms. Per-operation p50 stays approximately 0.48–0.54 ms in both variants;
CPU change +4.46% has interval [−14.06%, +24.78%]. Close delays appear in both
variants. This suggests waiting contributes to the tail, but does not establish
host noise or identify its cause. The result is retained and prevents a uniform
ordinary-writer cost claim. No sample is removed.

Linux .NET 8 traffic also has variable write timing: pure-write mean change −3.34%
[−51.32%, +44.65%], but churn mean +46.12% [+5.56%, +86.68%] and p99 +35.97%
[+7.73%, +64.21%]. Its median churn means are 11.02→12.03 ms. The difference between
median absolute values and mean paired percentages is intentional; the CSV retains
both instead of substituting the less adverse statistic.

## Source identity

| Role | LiteDB commit |
| --- | --- |
| Merged #3013/#3014 baseline | `c12b6a72149ad2166d62769d07efe961198b7a38` |
| Archived implementation | `130b339523cbe61efcef52b5d67252aee5a6cb53` |
| Byte-identical restoration on the merged stack | `16131953114971557b44e9deaa78bd8ce9d45ab1` |
| Ratio-seven candidate before hint completion | `e81c28bb864e09f48a2025fe8aad360d65baa27e` |
| Hint-completion stage | `093c6a96fe3dae528e4919758b3e8759437fa8ad` |
| Hint-completion stage plus core workflow selection | `eea594b79edf6b52342b32fb1840616e6cf948ae` |
| Phase-tail benchmark records, unchanged library | `9f4867c7818f47fceaa94838bb42f720ee8b1dc3` |
| Additional disposal tests, unchanged library | `c6053fdd9611fbe50592ae6ef65258096753f8c7` |
| Benchmark input/deadline fixes, unchanged library | `3520438d8` |
| Atomic live/status publication | `31bbd70f40afbab905087bccf08c30ac4d436633` |
| Atomic revocation publication on all runtimes | `3e7b8d9b5ce787a828b2b3ca1b760cc6e22b82f2` |
| Current candidate, including Windows path budget | `186cdeec6bbc76d949b77c726c54a492dbc6d3cf` |

Each hosted directory retains `base.txt`, `head.txt`, `runtime.txt`, and JSONL
records with actual DLL SHA-256. Diagnostics that modified disposable source
checkouts retain their patch and are excluded from production performance claims.
The chronological investigation notes are in [investigation-log.md](investigation-log.md).
`source-identity.json` also verifies that the restored library tree matches the
archive and that `093c6a96f`, `eea594b79`, `9f4867c78` and `c6053fdd9` have the same
library source tree. Benchmark/test-only commits are not new library variants.

## Pre-publication-fix saturated comparison

Run [36318836474](https://github.com/litedb-org/LiteDB/actions/runs/36318836474)
compares the pre-publication-fix library at `9f4867c78` directly with the merged baseline: .NET 10, four
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

Earlier core comparison [36317773622](https://github.com/litedb-org/LiteDB/actions/runs/36317773622)
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

The earlier hint-completion library passes 696 selected Shared/coordinator/WAL
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
This earlier matrix does not qualify later source changes. The intervening hint-completion run
[36320688022](https://github.com/litedb-org/LiteDB/actions/runs/36320688022) is also historical; exact-head qualification is recorded below.

The SC model enumerates 210 bounded admission schedules without a counterexample;
its unsafe variants produce counterexamples. It is not a weak-memory proof.
Native ARM64 execution supplies additional evidence. Process termination is not
power loss: persistence tests model explicit torn/lost writes and failed flushes,
subject to documented filesystem/device durability guarantees. Finite tests cannot
establish arbitrary crash, filesystem or hardware behavior.

## Archive and reproduction

`evidence-001.tar.gz`, `evidence-002.tar.gz`, and the remaining numbered parts
contain raw measurements, logs, TRX, patches, finite campaign outputs, and helper
scripts. Extract every part into the same fresh directory. Each part is below
GitHub’s 100 MiB blob limit; the format-2 manifest records its hash and size. [manifest.json](manifest.json) names every file and
records original and published SHA-256 plus sizes. Published copies normalize
machine-specific paths/host text; original files remain unchanged locally. The
packager verifies that every numeric/boolean/null JSON value is unchanged and
validates JSON syntax after normalization. The archive excludes compiled binaries,
Git worktrees, downloaded external source, and PR comment copies. Hosted fuzz
bulk database/dump files are omitted; all reports and binary replay inputs remain.
Locally generated finite-campaign synthetic databases remain included.

The generated [standard-comparisons.csv](standard-comparisons.csv) covers complete
ordinary-workload pairs and labels instrumented diagnostics.
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
compares the pre-publication-fix library at `9f4867c78` directly with the merged baseline on
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

## Exact-head safety evidence

`ci-final-head/safety-audit.json`, reproduced by `audit-safety.py`, groups actual
TRX outcomes by affected invariant. All 21 modern full-suite artifacts
contain these passing cases; skipped tests are counted separately:

| Affected invariant | Passing cases per full-suite artifact | Main test families |
| --- | ---: | --- |
| Complete control-file publication | 30 | `SharedCoordinationFile`, native creation death boundaries |
| Admission and storage mutation fences | 46 | Coordination page/publication, opening mutation, native admission/recovery |
| Announce WAL reuse before overwrite | 5 | `SharedWalReusePublication`, `SharedWalBatchPublication` |
| Snapshot lifetime and resource ownership | 17 | Coordinated reads/resources/lifecycle, idle timer |
| Writer scheduling and disposal | 20 | Read pacer, writer pressure/completion, ordinary open |
| Unsupported participant fallback | 2 | Coordination architecture fallback |

The native CI checks out merge commit `181f88711516145d3bfa5d727985624be1f08592`;
its whole source tree equals head `186cdeec6` (`pr3016-source-identity.json`). The
retained build log independently records that checkout. Actual .NET Framework
4.6.2 and 4.8.1 jobs each complete 4,515 cases, with zero failures and nine skipped
cases. The mapped fast path is intentionally unavailable on those frameworks;
the atomic file helper and safe revocation path still compile and run there.

## Review fixes and subsequent qualification

The selected library now publishes complete participation, status and revocation
control files with a flushed temporary file and non-replacing same-directory
publication. Unknown destination files remain untouched. Process death can leave
an ignored temporary file; control files remain advisory and are not durable
commit records. Filesystem-dependent directory-entry persistence remains outside
the process-kill fault model.

`3520438d8` fixes benchmark workflow input interpolation and bounds offered-arrival
waits by their phase deadline. PowerShell testing passes an activity containing
semicolon/quotes as one literal argument. A ten-minute offered interval completes
a 200 ms measurement on time with exactly one read, alongside zero/50 ms controls.
`31bbd70f4` adds atomic live/status creation, `96cbf7874` corrects an ambiguous BSON
assertion, and `3e7b8d9b5` extends atomic creation to revocation on every runtime.
The original compile failures and their correction are retained. `186cdeec6` keeps the temporary basename at 21 characters, so a one-character
name at the existing 239-character Windows database-path limit never creates a
path of 260 characters. The six helper tests also compile on legacy targets.
The focused 24 process-kill/six helper cases pass on both .NET 8/10, and the full
.NET Framework 4.6.2 test project builds. The preceding revocation implementation
passes 727 selected Shared/coordinator/WAL tests on .NET 10. Final native CI [36326327744](https://github.com/litedb-org/LiteDB/actions/runs/36326327744) passes all 47 jobs at the source-equivalent PR merge.

The pre-publication-fix PR matrix at `a9483fd3a` passed all 47 CI jobs
([36322551709](https://github.com/litedb-org/LiteDB/actions/runs/36322551709)),
all 12 selected fuzz jobs and all four index migration jobs. Its 135 hosted fuzz
reports pass at merge commit `a121c6d0a`, whose whole source tree equals the head.
Those reports retain `workingTreeDirty=true`; generated CI output makes them
different evidence from the explicitly clean frozen local campaigns.

The first atomic-creation campaign at frozen `31bbd70f4` passed all 18 reports,
but five reports inherited dirty metadata from the active working directory.
The source checkout and binary hashes stayed unchanged. The driver was corrected
to execute in the frozen source checkout; the independent rerun passed 12
invocations/18 reports with every dirty flag false and matching before/after DLL
hashes. Both runs remain available; the first is not relabeled clean.

## Additional performance observations

The completed full matrix [36321687725](https://github.com/litedb-org/LiteDB/actions/runs/36321687725)
uses `c6053fdd9`, the same library source as `a9483fd3a`. Linux .NET 10 four-reader
checkpoint throughput changes are −4.24% (point), −3.58% (medium) and −0.07% (large),
with reader-call medians 553→7,802, 358→1,700 and 345→508 respectively. Writer p99
changes are +3.70%, +7.02% and +6.28%; the tail cost is visible even where writer
throughput is nearly unchanged. Windows point/four writer throughput changes
−6.93% on .NET 10 and −6.73% on .NET 8.

A Windows .NET 10 large/four checkpoint pair has candidate p99 2,097 ms versus
43.63 ms; all other candidate pairs in that cell are near 47–53 ms. The slowest
transaction spends 1,920 ms in its explicit checkpoint, and other slow transactions
stall in different phases. The targeted repeat
[36324132720](https://github.com/litedb-org/LiteDB/actions/runs/36324132720) does not
repeat the two-second event: Windows writer throughput improves 12.66% and p99
falls 11.18%; Linux throughput changes −1.23% and p99 +7.40%. This does not explain
or erase the original event.

Writer-only repeat [36323868272](https://github.com/litedb-org/LiteDB/actions/runs/36323868272)
retains further adverse Linux results: point/one throughput −21.95%
[−37.18%, −6.99%] and medium/one −18.83% [−31.81%, −5.85%], with long variable
commit tails. Windows point/four changes −6.11% [−8.33%, −3.43%] and p99 +8.98%
[+6.54%, +11.43%]. Other Windows writer throughput cells range −2.56% to +3.13%.
These results prevent claiming a uniform small writer overhead.

The repeated ordinary-workload matrix
[36322551372](https://github.com/litedb-org/LiteDB/actions/runs/36322551372) retains
a .NET 8 Windows churn candidate at 41.50 ms/operation versus 2.58 ms baseline;
its ten chronological windows remain slow, while CPU is only 2.20 ms/operation.
Other candidate churn pairs are around 2.5–3.0 ms. Earlier Windows ordinary
workload results contain both candidate and baseline stalls. The instrumented
follow-up [36325118477](https://github.com/litedb-org/LiteDB/actions/runs/36325118477)
separates mutex handoff, engine opening and closing, but cannot be used as
production acceptance timing.

Two/four-writer contention in the repeated matrix validates every result and
final WAL cleanup. Aggregate paired throughput ranges −3.08% to +1.01% across
the eight host/runtime/count cells. Linux .NET 10 two-writer maximum-worker API
p99 increases 23.12% [+8.81%, +36.59%]. Scheduled workers complete only roughly
5–10% of their offered work in both versions under the unthrottled competing
writer; retained completion and arrival-latency fields expose this overload.
This is not a fairness or maximum-wait guarantee. Same-version process-death
interop campaigns pass for both variants on all four hosts/runtimes.

The bounded first-transaction checkpoint drain (`1abb51e9d`, run
[36323206436](https://github.com/litedb-org/LiteDB/actions/runs/36323206436)) is
rejected. Linux medium/checkpoint writer throughput improves 5.44% and p99 falls
7.20%, but Windows large/checkpoint throughput falls 5.42% and p99 rises 8.64%;
Linux large writer-only throughput falls 4.46%. Eight focused safety cases and
706 broader cases pass for the corrected experiment. Its initial four failures
incorrectly treated physical confirmation offsets as transaction counters; the
failed and corrected outputs are retained. The experiment remains off the main
branch. The longer advisory-hint experiment is also rejected, as described below.

The final candidate's [fuzz](https://github.com/litedb-org/LiteDB/actions/runs/36326327463)
and [migration](https://github.com/litedb-org/LiteDB/actions/runs/36326327457)
workflows pass all 12 and four selected jobs respectively. All 135 fuzz reports
pass at merge commit `181f88711516145d3bfa5d727985624be1f08592`; its whole source
tree equals `186cdeec6`. Their dirty metadata flags remain true and are retained.
The frozen `3e7b8d9b5` local campaign passes all 12 invocations/18 reports with
clean flags and unchanged binaries. It predates only the temporary-name boundary
fix; it is not mislabeled as an exact-head campaign.

The new full production matrix at `31bbd70f4`
([36324968780](https://github.com/litedb-org/LiteDB/actions/runs/36324968780)) also
retains adverse Windows .NET 10 rows: medium/four writer throughput −15.65%
[−18.12%, −12.01%], p99 +35.04% [+21.87%, +54.15%], while reader medians rise
187.6→1,269.1 calls/s. Point/four writer throughput changes −9.20% and large/four
−2.93%. Linux .NET 10 writer timings on this host are much steadier than the
previous slow-I/O hosts: point/four changes −6.26% with readers 356.8→7,328.9;
medium/four changes −2.54% with readers 247.4→1,673.6; large/four improves 6.12%
with readers 257.9→469.2. Writer p99 changes in those Linux cells are +4.59%,
+13.07%, and +8.54%. Host variability and persistent saturated-load costs both
remain visible; this matrix must not replace the earlier unfavorable runs.


## Final experiment decisions

The 500 ms writer-hint experiment (`7c383b5b4`,
[36325857226](https://github.com/litedb-org/LiteDB/actions/runs/36325857226)) compares
only hint lifetime against `3e7b8d9b5` on Linux/Windows .NET 10. It does not show a
consistent writer benefit, so the selected lifetime remains 100 ms. Windows
medium/four writer throughput falls 4.91% [−7.32%, −2.45%]; Linux point/one falls
3.26% [−8.12%, −0.28%]. A Windows large/four baseline outlier produces a large,
inconclusive positive paired mean and is retained. The Linux host does not
reproduce the long commit stalls that motivated this hypothesis: its writer p99s
are below 100 ms. The experiment therefore does not disprove a benefit during
those stalls; it does not establish enough benefit to extend abandoned hints.

Extended instrumented diagnostics (`45364c9f1`,
[36326725129](https://github.com/litedb-org/LiteDB/actions/runs/36326725129)) run
15 churn pairs each on Windows .NET 8/10, separating ownership waits, engine
open/close, notification and WAL write phases. All results validate. The original
41 ms/operation event does not recur: candidate pair means are 2.440–2.509 ms on
.NET 8 and 1.395–1.742 ms on .NET 10. Paired mean changes are +0.12%
[−1.03%, +1.28%] and +0.87% [−1.66%, +3.41%]. These instrumented runs cannot be
used as production acceptance timing, and absence of the stall does not explain
or refute it. No diagnostic instrumentation is in the selected library.


The final Linux traffic tail triggered one additional bounded diagnostic,
`b554ad5c5` ([36328609589](https://github.com/litedb-org/LiteDB/actions/runs/36328609589)),
with 15 pure-write pairs and a durable-flush timer. All 30 results validate with
empty final WAL. Mean medians are 0.6078→0.6102 ms; mean paired change is 0.00%
[−2.99%, +2.12%], p99 −2.23% [−5.83%, +0.49%]. The slow production regime does
not recur. Durable flush averages approximately 0.30 ms/operation, opening 0.17 ms,
closing 0.06 ms and ownership wait 0.002 ms. The public `WriteLogDisk` wrapper
counter records no calls in this workload; its zero is not a claim of zero WAL
I/O. Flush timing records the path actually used. Instrumentation and a different
host prevent this run from qualifying or explaining the earlier production tails.
No further source change was justified by these diagnostics.
