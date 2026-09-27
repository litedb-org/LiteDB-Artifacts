# Shared mmap ABI and review follow-up

This report accompanies [LiteDB PR #3016](https://github.com/litedb-org/LiteDB/pull/3016)
and [issue #3018](https://github.com/litedb-org/LiteDB/issues/3018). It records the
versioned coordination ABI, the two review follow-ups, final-source validation,
and the performance cost of those changes. The previous
[writer-cost investigation](../shared-mmap-writer-cost-2026-09-27/README.md)
remains unchanged and retains the earlier prototypes and rejected alternatives.

## Source and comparisons

| Role | Source |
| --- | --- |
| Final candidate | [`4c84f44ed4adb1df57cc6d797d443b8978747f13`](https://github.com/litedb-org/LiteDB/commit/4c84f44ed4adb1df57cc6d797d443b8978747f13) |
| CI checkout | `44f113be3e6e8e60637c5df8703bde23f368daab`; tree equals the final head |
| PR merge candidate | `76604f01fcc1993ab2841dbcdb8878ee1cf56ee3`; tree equals the final head |
| Final tree | `7f6f5b865881c5f846c14daa8e7b230c2d18c544` |
| Merged #3013/#3014 stack baseline | `94bf30948a865edee75482265195cca7af18a954` |
| Archived mmap prototype | `16131953114971557b44e9deaa78bd8ce9d45ab1` |
| Optimization before the ABI changes | `186cdeec6bbc76d949b77c726c54a492dbc6d3cf` |
| Reviewed ABI source | `e06fc9d2d061e412fa6782fb8c993f3b75ce35ec` |
| Intermediate review fixes | `8c038860c8dcd1b7f7293eb684849e9d4fad96a3` |

The final change rejects either Unix disabled-file-locking configuration knob.
This matters because .NET 9/10 give the environment value precedence, unlike the
checked .NET 8 source. The intermediate guard had incorrectly given an explicit
false AppContext value precedence on all runtimes. Two native child-process cases
fail on that guard and pass on the final source. A process without file locks
must never delete an authority while another process still uses its mapped page.

[Review resolution](review-resolution.md) maps each finding to the code change,
discriminating test and deliberate remaining limit. Product guidance lives with
[source](https://github.com/litedb-org/LiteDB/blob/4c84f44ed4adb1df57cc6d797d443b8978747f13/docs/shared-mapped-operations.md).
The [protocol](https://github.com/litedb-org/LiteDB/blob/4c84f44ed4adb1df57cc6d797d443b8978747f13/docs/shared-coordination-prototype.md)
defines the ephemeral control-file ABI; database and WAL formats are unchanged.

## Validation

The final head passes all selected validation jobs, without a CI/fuzz/compatibility rerun:

| Validation | Result | Hosted run |
| --- | --- | --- |
| CI | 47/47 jobs pass | [36342371373](https://github.com/litedb-org/LiteDB/actions/runs/36342371373) |
| Fuzz | 12 selected jobs pass; 135 passing reports retained | [36342371157](https://github.com/litedb-org/LiteDB/actions/runs/36342371157) |
| Index compatibility | 4/4 jobs pass | [36342371209](https://github.com/litedb-org/LiteDB/actions/runs/36342371209) |

All 21 modern full-suite artifacts contain 92 passing ABI/review cases each,
including 65 protocol/upgrade cases and the cleanup, policy, temporary-file,
write-intent and abandoned-owner regressions. No mapped test was skipped on these
qualified hosts. The 33 retained full-suite/cross-process artifact sets contain
101,922 passing results, zero failures and 147 skips of seven pre-existing test
names. Runtime assertions identify the actual .NET 8/9/10 x64, ARM64 and Windows
x86 test hosts; the artifact audit retains those assertions. Framework-targeted
jobs and both Windows cross-process matrices also pass.

Platform-specific cases execute on their applicable OS: native sharing-error retries on Windows, disabled-file-locking rejection on Unix, and case aliases on case-insensitive volumes. Passing a guarded case on another OS does not qualify that OS-specific behavior.

The affected safety invariants are covered by native process termination at every
changed authority retirement/publication boundary, repeated interrupted recovery,
plaintext/encrypted cold document and index oracles, malformed/newer/copied/stale
headers, fallback writes while readers survive, retained cursor generations,
forced owner-cleanup interleavings, and cleanup exceptions. Sidecar cleanup tests
preserve unknown and unrelated files and cannot remove a live publisher.

This is evidence under the documented local-filesystem sharing-lock, coherent
mmap and rename assumptions. It does not qualify excluded filesystems, processes
that bypass the protocol, physical path aliases, live cloud synchronization or
hardware that violates durability guarantees. Existing fault-injection and fuzz
campaigns cover the specified short/failed I/O, recovery and power-loss models;
finite runs are not a universal safety proof.

The intermediate source additionally passed 1,181 local .NET 8 Shared/WAL/MVCC
cases, 194 focused .NET 10 cases and all production targets. Fifteen selected
mapped cases passed with tmpfs as the default temporary directory, using a
qualified fallback. Explicitly forcing tmpfs skipped ten test methods with the
volume reason. Twelve same-binary churn variants validate runner behavior only.
The final source passed all nine policy cases on .NET 8 and .NET 10 locally and
all three production library builds.

Earlier `e06fc9d2d` CI was red: a real abandoned-owner cleanup race and a Windows
x86 query session timeout. Both original failures and their fixes are retained.
The owner regression fails before the fix for both Commit and Rollback. The slow
NotEqualIndex class now has a separate, disjoint CI session; exact-coverage checks
and the 300-second limit are unchanged. The superseded intermediate CI was
cancelled after 31 passing jobs when the runtime-precedence problem was found;
its completed fuzz, compatibility and performance runs remain in the bundle.
Cancelled comparisons that never executed are not performance observations.

## Production measurement method

All comparisons use isolated Release libraries with `TestingEnabled=false`, the
same candidate benchmark runner on both libraries, fresh processes, ten-second
warmup, five serial alternating pairs and independent result/cold-state checks.
Each raw record includes the library hash and runtime, and each hosted artifact
contains both source SHAs and `dotnet --info`. Reader cases use ten measured
seconds after warmup, point/medium/large reads, one/four reader processes, and
idle/writer/checkpoint activity. Saturation uses an offered interval of zero;
ordinary traffic and scheduled writer contention are separate workloads.

Connection churn constructs and disposes a Shared connection for every writer
transaction. Its latency includes construction, 200 updates, commit and final
close/checkpoint. Phase counters cover BeginTrans/update/Commit only. New
connections can announce pressure only after safely attaching under the mutex;
BeginTrans has no declared write intent, so its first actual write announces.

Tables report the median baseline and candidate separately from the mean of five
paired percentage changes. These are different statistics and can diverge under
outliers. Deterministic bootstrap intervals (10,000 resamples, seed 9316) are
exploratory, not multiple-comparison-adjusted. Crossing zero is not equivalence.
Hosts and runtimes are never pooled into one speedup. All adverse cells remain
in the complete CSVs and raw records.

Compared with the merged stack, ordinary point-read mean latency fell 82.31–85.33%
and scan latency fell 40.18–48.73% across the four OS/runtime cells. Mixed mean
latency fell 16.78–53.03%; the Linux .NET 8 interval crossed zero
(−46.25%, +12.69%). Pure-write mean changes ranged from −2.36% to +2.05%, all
with intervals crossing zero. These observations do not establish equivalence.

Adverse ordinary tails remain: Windows .NET 10 mixed p99 rose 18.84%
[0.85%, 36.83%], and open/close p99 rose 41.64% [0.98%, 120.78%]
(medians 6.015 → 6.177 ms). Windows .NET 8 checkpoint p99 was +28.65%
[−7.77%, 73.57%]. The median-versus-paired discrepancy is retained explicitly.

Two/four-writer contention also has adverse cells: Windows .NET 8 with two writers
lost 5.02% [1.48%, 9.08%] aggregate throughput. Windows .NET 10 with four writers
lost 10.12% [2.21%, 22.81%] scheduled completion, despite its noisy aggregate
throughput estimate. Offered work exceeds capacity in this workload; arrival
latency and completed-work fractions remain in the raw results and summaries.

Against the merged stack, every final saturation reader cell improved: the lowest
paired throughput increase was 74.02% [71.12%, 76.30%] for four large readers with
checkpoint activity on Linux .NET 10. With four point readers and a writer on
Linux .NET 10, reader throughput rose from a median 394 to 20,905 calls/s, while
writer throughput fell 21.29% [19.43%, 22.97%].

The largest paired writer-throughput loss was Windows .NET 10 with four medium
readers: −26.46% [−37.50%, −11.02%], medians 50.85 → 36.77 transactions/s.
The largest writer p99 increase was Windows .NET 8 with four large readers:
+192.19% [45.77%, 422.69%], medians 43.21 → 65.16 ms. Windows checkpoint/large
cells also have substantial adverse tails. These results establish neither a
uniform 5% writer-cost bound nor a uniform tail-latency bound. Users with an
unacceptable workload tradeoff can disable mapped reads at process startup.

Archived comparisons do not establish uniform preservation of prototype speeds.
The largest reader loss was four point readers with checkpoint activity on Linux
.NET 10: −22.73% [−25.47%, −19.46%], medians 11,510 → 8,880 calls/s. The largest
writer-throughput loss was four large readers with checkpoint activity on Linux
.NET 10: −5.46% [−6.26%, −4.85%]. Windows .NET 10 writer p99 with four medium
readers rose 57.35% [23.27%, 92.64%], medians 73.02 → 110.46 ms.

Connection churn versus reviewed `e06fc9d2d` improved paired writer throughput in
all 24 cells, but reader pacing has a substantial cost. With one point reader on
Windows .NET 8, writer throughput improved 3.70% [3.06%, 4.31%], while reader
throughput fell 91.34% [90.46%, 91.99%], medians 12,713 → 1,076 calls/s. With four
large readers on Linux .NET 10, median writer throughput rose from 3.80 to 33.95
transactions/s (paired +767.73% [291.35%, 1262.64%]). Some churn writer tails still
regress: Linux .NET 8 with one medium reader has paired p99 +81.12%
[−12.53%, 187.75%], medians 34.40 → 44.89 ms. This is a workload-dependent tradeoff,
not a claim that fresh connections become uniformly faster for every participant.

The final-versus-pre-ABI first batch has point-read mean changes of +0.33% to
+3.65%, scan mean changes of +0.69% to +101.01%, and mixed mean changes of +0.73%
to +2.18%. The extreme scan estimate comes from one Windows .NET 10 candidate
sample: 8.918 ms mean and 135.659 ms p99, with the first five chronological windows
much slower than the last five. The other four candidate samples were 1.483–1.622 ms
mean. The full five-pair estimates remain +101.01% [−3.32%, 306.57%] mean and
+1182.03% [−1.66%, 3541.03%] p99; neither is replaced by its median.

One unchanged repeat of only that successful Windows .NET 10 job did not reproduce
the severe scan sample: scan mean −0.24% [−1.54%, 1.07%], p99 +6.52%
[−2.11%, 15.74%]. Its mixed CPU cost was +14.40% [4.65%, 25.52%], mixed mean
+14.01% [−0.45%, 40.80%], and mixed p99 +27.61% [−21.57%, 103.42%]. The first batch
also had mixed CPU +6.09% [1.24%, 10.46%]. The repeat does not establish neutral
cost, identify the outlier's cause, or erase any adverse first-batch result.

All five requested production matrices pass: ordinary 8 jobs, saturation 12,
archived 6, incremental 4, and churn 4. The additional unchanged performance repeat
passes too. The production audit checks all 35 artifact sets, 62 JSONL files and
2,408 records for source identity, complete pairs, binary hashes and correctness
outcomes. Detailed [comparison tables](performance-tables.md),
[ordinary estimates](standard-summary.csv), [reader estimates](readers-summary.csv),
and [run index](runs.md) retain every cell.

## Isolated ABI cost and rejected pacing experiment

The comparison that isolates the ABI work is reviewed `e06fc9d2d` versus pre-ABI
`186cdeec6`, not the final review head. First-batch mean latency changes were
−1.63% to −0.09% for point reads, −0.33% to +1.39% for scans and −0.02% to +2.20%
for mixed work. Windows .NET 10 mixed p99 was +20.56% [2.69%, 38.71%]. Repeating
that unchanged job on another hosted machine gave +8.57% [−8.28%, 26.42%], with
scan p99 +9.29% [0.97%, 17.63%]. Both batches are retained separately. The repeat
does not erase the first adverse result or establish a tail-latency bound.
Final-head comparisons against `186cdeec6` include all review changes as well.

Local Linux .NET 8 connection-churn experiments used five alternating pairs and
four saturated point readers. Review fixes versus `e06fc9d2d` reduced paired
writer mean latency 17.88% [8.26%, 27.51%] while reducing reader throughput
84.97% [83.12%, 87.26%]. Against the merged stack, review fixes increased paired
writer mean latency 20.84% [7.17%, 36.30%], while reader throughput rose about
22-fold. These local runs are on intermediate `8c038860c`, not final-head evidence.

A rejected trial changed only the pacing delay/work ratio from seven to three
(source `5d8825e49`, patch and source identity retained). Relative to ratio seven,
reader throughput rose 91.91% [76.84%, 104.18%], while writer mean latency changed
+2.93% [−13.87%, 19.43%]. Individual pairs ranged from a 30% writer regression to
a 22% gain. That evidence was too variable to replace the existing ratio seven.
The rejected trial and both local baselines are retained, not pooled with hosted
results or presented as a selected improvement.

## Retained evidence and reproduction

`manifest.json` records every archived file, original and published hashes, and
archive hashes. Only machine paths/host identifiers are normalized; numeric JSON
values are checked unchanged. Archives exclude source worktrees, compiled
binaries, external runtime source copies, and bulk fuzz database/dump files;
fuzz replay inputs, trace reports, source identities and failure logs remain.
All database data was generated by tests/benchmarks, not taken from deployments.

Extract the archives into one directory, then run the retained analysis scripts:

```sh
for archive in evidence-*.tar.gz; do tar -xzf "$archive"; done
python3 review-final/audit-safety.py
python3 review-final/tabulate-standard.py review-final standard-summary.csv
python3 review-e06/tabulate-readers.py review-final readers-summary.csv
python3 review-final/audit-production.py
```

The source workflow is
[shared-slot-performance.yml](https://github.com/litedb-org/LiteDB/blob/4c84f44ed4adb1df57cc6d797d443b8978747f13/.github/workflows/shared-slot-performance.yml).
Dispatch at the final SHA with `suite=standard` and the stack baseline for ordinary
work; `suite=readers`, interval zero, the desired baseline and activities for
reader saturation/churn; `suite=core` and pre-ABI baseline for incremental cost.
Archived comparisons use .NET 10 on both OSes; the other production matrices use
.NET 8 and .NET 10 on Linux and Windows. Run manifests identify the comparison baselines and hosted jobs; per-row commands
retain the executed workload parameters. No timing results use diagnostic ownership
instrumentation or safety-hook assemblies.
