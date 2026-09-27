# Investigation chronology (historical snapshots)

The entries below were recorded during successive experiments. Statements that a
run is pending or a candidate is unaccepted describe that point in the investigation;
use README.md and the run index for the selected implementation and latest evidence.
Unfavourable results and unsuccessful prototypes remain part of this record.

# Shared mmap writer-cost investigation

This follow-up restores the archived mapped admission implementation from
`130b339523cbe61efcef52b5d67252aee5a6cb53` on the merged #3013/#3014 Shared stack
(`c12b6a72149ad2166d62769d07efe961198b7a38`). The restored library is byte-for-byte
the archived source before the separately recorded optimizations. `dev` did not
contain that stack when this work started.

Qualification is in progress. Neither historical safety results nor an individual
benchmark constitutes acceptance of the revised implementation. The initial
performance target is no repeatable writer throughput/p99 regression greater than
5% relative to the merged implementation, while retaining the archived reader gains.

## Protocol changes under evaluation

1. Aligned acquire status loads on x64/ARM64; x86 retains compare-exchange reads.
   Seqlock validation and post-lease admission retain full memory barriers.
2. End a protected writable open and establish local trust in one publication.
3. Publish a monotonic append commit through one atomic version update. Equal
   versions need no write. Resets and interrupted sequences retain full sequencing.
4. Publish WAL reuse before the first overwrite of each write batch. Only an
   `IBatchedCoordinationSignals` participant can coalesce: its protocol excludes
   cold snapshot installation for the whole batch. The separate coordinator keeps
   per-frame notification. Each safepoint or new batch announces independently,
   including failure cleanup; no durability barrier or allocation rule changes.
5. Update the last-use timestamp on reader completion. The existing timer checks
   activity and rearms itself; streaming readers retain leases, idle caches do not.

6. Treat the first status lookup as a hint, omitting its two revocation probes.
   The final authoritative lookup after lease publication still performs both
   filesystem probes and the full fence; no query runs on the hint alone.
7. Experimentally fence actual startup mutations instead of every writable open.
   Initialization, header repair, checksum conversion, tail truncation and v7
   upgrade now have explicit structural scopes. Automatic rebuild fences before
   scanning external leases. Existing checkpoint/format-promotion/reuse scopes
   remain. An interrupted mapped publisher still requires a broad protected open.
   Additional recovery qualification is in progress; this is not accepted yet.

## Evidence location and reproducibility

Local raw results, build logs, patches, and TRX reports are under
`artifacts_temp/mmap-writer/`. Production variants use isolated worktrees and
Release / `TestingEnabled=false`; tests use the main worktree with hooks enabled.
No timing run overlaps builds or tests on the same host. Other host activity is outside
this task's control. The host is Linux x64, Ryzen 9 3900X, with database scratch on
the original `/tmp` ext4 volume. Every runner validates full payloads, generations,
secondary-index results and final WAL cleanup; measured throughput does not by
itself establish correctness or acceptance.

The reader driver also supports a finite `--reader-interval-ms` to separate equal
offered load from saturation. Writers remain unthrottled. API and intended-arrival
latencies, offered/completed work, allocations, CPU and lifecycle costs are retained.

## Safety coverage

The restored tests cover native admission/checkpoint interleavings, killed readers
and writers, partial leases, fallback revocation, unknown files, encrypted reads,
resource disposal and idle lease release. New tests distinguish append publication
from resets/interrupted publishers, per-batch notification from per-frame protocol
requirements, failed overwrite cleanup, and timer visits during streaming reads.
The broader Shared/MVCC/recovery suites and finite campaigns remain required.
Process death does not simulate loss of the OS cache; persistence-fault tests cover
their own explicit lost/torn-write and failed-flush models. Cross-platform results
must name their actual tested runtime and architecture.

## Initial Linux .NET 10 results

Five alternating pairs, 10-second warmup and 10-second measured interval,
4 reader processes and one unthrottled writer (200-document durable transaction).
Values are medians; paired changes use an exploratory deterministic bootstrap.

| Comparison / readers | Writer transactions/s | Writer p99 ms | Reader calls/s |
| --- | --- | --- | --- |
| Archived → acquire loads, point | 23.48 → 23.72 | 50.89 → 50.23 | 829 → 940 |
| Merged → combined, point | 23.20 → 23.49 | 52.76 → 50.41 | 217 → 929 |
| Merged → combined, large scan | 22.58 → 22.59 | 53.11 → 53.69 | 90.20 → 94.10 |

Combined is library `ad7de069d`: loads, duplicate removal, monotonic publication,
reuse batching and timer changes. In the large-scan comparison writer throughput
changes −0.24% [−0.68, +0.08], while writer p99 changes +3.52% [−2.17, +8.28].
The latter is inconclusive, not proof of a 5% equivalence bound. Point readers use
15.7% more aggregate process CPU while completing 4.15 times as many reads.

These local runs do not reproduce the archived hosted regression magnitude.
The original Linux .NET 10 hosted point results have 68.44 → 56.76 writer
transactions/s and 379 → 12,181 reader calls/s; Windows large scans have
44.02 → 33.92 writer transactions/s and 163 → 616 reader calls/s. Their raw
reports were inspected. The local host's lower reader concurrency and different
I/O timings make hosted qualification necessary before claiming the writer problem
is solved. Hardware performance counters are unavailable (`perf_event_paranoid=4`);
no system permissions were changed to collect them.

## Isolated coordination measurements

A temporary reflection-bound helper runner exercises production methods directly;
delegates are bound before timing. Five alternating rounds per variant, a two-second
warmup and one million operations per process validate the final status/version.
These are helper costs, not database transaction latency.

| Cumulative variant | Commit publication ns | Writable-open publication ns | Status read ns |
| --- | ---: | ---: | ---: |
| Archived | 31.82 | 104.26 | 2146.55 |
| Acquire loads | 23.27 | 76.59 | 2112.96 |
| Remove duplicate open publication | 22.59 | 63.21 | 2126.31 |
| Atomic monotonic commit | 14.46 | 63.49 | 2139.22 |
| Batch reuse | 14.53 | 63.66 | 2130.28 |
| Timer change | 14.69 | 64.21 | 2110.50 |

The final two changes are not exercised by this helper; their unchanged values
are controls. Batching's event counts/failure boundaries are tested separately.
The full status call retains its filesystem revocation checks. Nanosecond savings
in publication cannot alone explain a double-digit end-to-end writer regression.
Raw inputs/source and per-run library hashes are in `coordination-costs.jsonl`
and the sibling `coordination-probe` directory.

Local Shared/coordinator/WAL selection on .NET 10 passed 461 cases, including the
new publication/reset, batch/failure and timer tests. Hosted CI and reader
comparisons are running on `b2010e8bd`; their completion remains outstanding.

Local .NET 8 also passed the same 461-case selection. Optimized x64 disassembly
shows eight `lock cmpxchg` instructions per archived status scan and none in the
acquire-load scan; its explicit full fence remains. Both disassemblies and the
runtime memory-model source consulted are retained with the local evidence.

| Changed invariant | Discriminating tests |
| --- | --- |
| Append publication preserves old storage while exposing the new version | `SharedCoordinationPublication_Tests.Append_commit_changes_only_the_visible_version`; existing native concurrent document/index oracles |
| Reset or interrupted publication invalidates old cached state | `Version_reset_still_invalidates_old_snapshots`, `Interrupted_sequence_requires_recovery_even_for_an_unchanged_version`; mapped process-death tests |
| Each destructive batch announces before mutation, including failed writes | `SharedWalBatchPublication_Tests` (batched/unbatched and success/failure); `SharedWalReusePublication_Tests`; `WalSlotReuse_Tests` partial-write rollback/recovery |
| Idle expiry never drops an active lease or strands cleanup | `SharedCachedIdleTimer_Tests`; restored idle, spill, finalization and native reader-death tests |
| Admission still protects every accepted generation | existing forced final-recheck negative control; Shared generation/reclamation tests; bounded snapshot/shared/MVCC-retirement campaign |

The local selections used Release with `TestingEnabled=true`; production source
is `ad7de069d` and new tests are in `e417f5884`. Later commits through `b2010e8bd`
change benchmark support and documentation, not library behavior. The campaign and
hosted matrices have not yet been recorded as completed qualification.

The bounded local campaign passed 12 invocations (seeds 3012–3014, 32 steps each
for snapshot/shared/MVCC-retirement/index): 384 primary steps plus six built-in
replays. It recorded 288 held-generation validations, 672 between-generation
checkpoints, 288 Shared child processes, 3,282 acknowledged rows, 78 crash-position
observations (42 internal), and 96 Shared integrity checks. Counts are summed
observations, not distinct faults. Each Shared seed reached all 26 configured
crash positions, including 14 internal boundaries. All independent payload/index
and integrity checks passed. Maximum invocation was 54.3 seconds, with about
6.2 MB externally accounted artifacts. The campaign ran against unchanged library
source; concurrent working-tree edits were documentation-only and their patch plus
binary/source-tree hashes are retained. This is a three-seed follow-up, not a claim
to have rerun the archived 16-seed campaign.

## Hosted rejection of the first combined variant

Run `36308282410` measured production `b2010e8bd` against the merged baseline.
Linux .NET 8, four saturated point readers: writer throughput changed −14.18%
[−15.37, −13.19] and p99 +11.26% [+8.01, +14.46], despite reader throughput
increasing about 20 times. Windows .NET 10 checkpoint workloads also regressed:
point throughput −28.19%, large −16.91%, with substantially worse writer p99.
These results reject the first combined variant against the provisional target.
The publication helper savings do not solve the end-to-end problem.

Idle Linux .NET 10 comparison against the archived mapped implementation retained
reader performance: point +1.90% [+0.86, +2.78], large +0.13% [−1.29, +1.89].
Equal offered load locally completes approximately 80 large reads/s with writer
throughput −0.93% [−1.62, −0.23], but that does not excuse saturated regressions.

The initial full CI exposed a Windows sharing-mode error in the new publication
test helper; `f69ba57f4` fixes the helper using an explicitly shared stream.
The old run and a duplicate queued run were stopped while implementation continues;
their partial results are not final qualification. The five corrected tests pass
locally. Windows confirmation remains required.

## Rejected mapped reader-lease experiment

Archived revision `b02672b75` (library `f8b8f9d18`) added a fixed 4 KB table containing 511 atomic version/complement slots.
Only cached coordinated reads use it; scoped fallback keeps its file-backed table.
One connection may therefore own one table of each kind. The new `mapped-` lease
prefix makes older parsers fail closed. Scanners and publishers both map the table;
the implementation does not assume coherence between file reads and mapped writes.
An exclusive OS lease handle still proves process liveness. No table is resized
while published, and exhaustion falls back through ordinary query admission.

The table is created under database ownership, and hot admission can only reuse
an established table. Idle caches release their slots immediately. Scanner views
are currently opened for each scan; their added open/map costs are part of the
experiment. It was reverted from the working candidate after measurement; the
branch `codex/shared-mmap-leases-experiment` preserves it locally.

New tests exercise full tables, malformed contents and lengths, version zero,
mixed lease formats, deferred disposal and prohibition of unowned table creation.
Existing generation tests now explicitly exercise the bounded two-table case;
file-write injection tests explicitly select the fallback protocol. A partial
file-backed publication is still tested with live mapped peers. Native mapped
death tests cover atomic publication before final admission. Production phase
timing records writer begin/update/commit costs separately to identify where any
end-to-end regression occurs. Hosted run `36310590505` found Linux point readers
+11.89% [+9.40, +14.33], but writers −1.05% [−1.61, −0.44] against the already
regressed combined variant. Windows writer comparisons were noisy and did not
establish recovery of the lost throughput. Extra table/mapping complexity was not
accepted without solving the writer problem. A local broad selection with the mapped
prototype and subsequent cache-retirement change passed 474 cases; that is not a
qualification of either standalone variant.

## Ownership delay and cache retirement

Production phase timing in run `36310099049` compares the merged baseline with
the first combined variant. Linux .NET 10 point workloads have median writer
begin time 1.40 → 3.53 ms/transaction, while updates remain 4.99 → 5.09 ms and
commit 6.75 → 6.75 ms. Windows point begins grow 1.10 → 3.19 ms; large begins
3.10 → 4.99 ms, with large commits 8.43 → 8.41 ms. Begin includes ownership
waiting and writable-engine opening; it does not by itself separate those costs.

The candidate at `46963eb9d` retired an invalid cached snapshot before waiting for database
ownership. Previously its accumulated pages were released while installing the
replacement under that mutex. Active streaming readers still retain their engine
and lease until their own disposal. A targeted interleaving checks retirement
while another connection holds a write transaction, then verifies both the new
query's committed value and every older streaming value. Its 463-case local
Shared/coordinator/WAL selection passed on .NET 10, but hosted run `36311367955`
did not recover writer performance: Linux point writer throughput fell another
2.02% [−3.14, −1.23] against the first combined variant. It was removed from the
working candidate and retained on `codex/shared-cache-retirement-experiment`.

An explicitly instrumented diagnostic run (`36311407545`) separates engine
opening from ownership waiting. Linux writer open was 0.088 → 0.119 ms/call,
but ownership wait 1.18 → 3.90 ms; Windows open 0.196 → 0.277 ms and wait
1.29 → 5.20 ms. Reader cache retirement averaged about 0.01 ms/call. These
instrumented builds are not production performance evidence. Their exact source
patch is retained in the workflow artifacts, and normal runners report no
ownership profile.

The expanded diagnostic (`36311862394`) measured native waiting separately.
Linux writer wait grew 1.03 → 3.64 ms/call: native wait 0.99 → 2.61 ms and
holder handoff 0.043 → 1.06 ms. Windows native wait grew 1.25 → 2.45 ms and
handoff 0.049 → 0.52 ms. Cold reader open/query work remained sub-millisecond.
The production spin-count experiment (`36311998025`, variant `9d85a8702`)
found no consistent writer recovery. Linux had variable durable-flush times;
Windows point writer change was −0.96% [−4.08, +2.36]. It was not adopted.

## Reduced hint probes and rejected continuous writer pressure

Revision `640aaa1c4` removes only the provisional hint's two filesystem probes.
The strengthened fallback tests prove a lease can still be published after
revocation, while the final authoritative check rejects it and reads the new
committed data through ownership. The broad 461-case .NET 10 selection passed.
Hosted run `36312799557` compared it with the first combined variant: median idle
point throughput rose 226,570 → 273,527 calls/s on Linux and 51,806 → 66,053 on
Windows. Active-writer point reads rose 12,538 → 13,425 and 3,548 → 4,181;
writer medians were approximately unchanged. This alone does not close the
writer gap against the merged implementation.

Revision `cfca6d460` added an expiring 100 ms scheduling hint before requesting
writer ownership. Accepted cached queries yielded once while the hint was live.
It changed no leases, durability or admission authority. Hosted run `36313054364`
compared it directly with `640aaa1c4` using five alternating validated pairs:

| Host / workload, four readers | Paired writer throughput change | Reader throughput change |
| --- | ---: | ---: |
| Linux point / writer | +17.13% [+16.15, +17.97] | −82.34% |
| Linux point / checkpoint | +16.80% [+15.30, +18.28] | −77.11% |
| Windows point / writer | +7.30% [+1.68, +14.08] | −55.74% |
| Windows large / writer | +4.54% [−1.55, +11.79] | −39.96% |

That reader cost is too high with broad startup invalidation. The standalone
hint was reverted; the local branch
`codex/shared-pressure-experiment` preserves the tested revision. All four jobs
validated their data. Locally the same point experiment mainly reduced readers
(−9.56%) while writers changed +0.24%; slow local durable flushes again limit
how representative those measurements are.

## Narrowing startup invalidation

Revision `323b6c31e` moves structural fencing to actual startup mutations.
Four deterministic plaintext/encrypted cases prove that a cached point reader
finishes while a real writable transaction holds database ownership, and verify
commit/rollback payloads, indexed results and cold reopen. Two publication cases
cover interrupted seqlock/structural publishers. Native tests now distinguish
an untouched writer open from a child killed inside actual tail repair.
File-backed mutation guards cover clean open, initialization, legacy conversion,
data/WAL tails, and repeated torn header repair with preserved redo. Rebuild
coverage checks that its fence precedes the external lease scan.

The first hosted dispatch (`36314066290`) failed before building because checkout
requires a full SHA rather than the abbreviated baseline provided. It contains
no performance results. Corrected run `36314228934` compares the same candidate
with the full `640aaa1c43df36a95a83aefbd853561c99b79bec` baseline.

Run `36314228934` completed all four jobs and validated their data. Narrowing
alone is rejected on writer performance: Linux point readers improved +332.63%
[+320.94, +344.28], but writers regressed −40.25% [−41.05, −39.45]; large readers
improved +309.94%, with writers −36.03%. Windows point reads improved +613.51%,
with writers −25.09%; large reads improved +191.38%, with writers −27.32%.
Measured process CPU rose substantially. The extra usable reader time under a
writer creates additional saturated work; narrower fences are not sufficient.

Revision `c5c54c9cb` combines those narrower scopes with the earlier expiring
writer-pressure yield. Run `36314784490` compares that combination directly with
the merged `c12b6a72149ad2166d62769d07efe961198b7a38` baseline. The hypothesis is
that pacing some of the additional reader work can recover writer capacity while
still retaining the archived reader throughput. The completed result follows below.

Safety evidence for narrowing: the .NET 10 broad selection passed 482 cases;
a subsequent .NET 8 selection including encrypted native repair death passed
483. On the combined revision another 317 targeted recovery, migration, rebuild,
pressure and native-process cases passed. In an isolated checkout, deliberately
removing `StructuralScope` publication fails all twelve startup-mutation/rebuild
tests while both clean-open controls pass; the unmodified control passes all
fourteen. The exact mutant patch and TRX reports are retained. This establishes
that these tests detect missing guards; it does not replace final platform and
campaign qualification.

The combined single-yield run `36314784490` completed successfully but still
missed the writer target. Versus the merged stack, Linux writer point throughput
changed −26.58% [−27.87, −25.30] and large −27.78%; Windows point −21.57% and
large −27.59%. Checkpoint writer regressions ranged from −18.70% to −41.01%.
Readers and their CPU work remained far above the merged baseline.

Revision `50849afa2` moves the scheduling yield before lease publication and
outside the snapshot gate. All admission state is freshly read afterwards.
Four forced interleavings distinguish a paused unleased query (which sees the
new committed generation after a checkpoint) from an already accepted reader
(which retains its old generation). Both plaintext and encrypted cases pass.
Run `36315269597` also completed and validated all four production jobs. Linux
writer point/large regressions remained −26.50%/−29.84%; Windows −14.79%/−18.42%.
Checkpoint runs had unusually variable durable-flush times and do not establish
recovery of point writer performance. Moving the yield alone is insufficient.

## Bounded pacing experiment

Library revision `b0716ded2` charges a local scheduling budget with five times
observed cached-query execution time while a writer's hint is active. It pays
that budget in requested pauses of at most 10 ms before publishing any lease;
actual OS scheduling delays can be longer. Total debt
and oversleep credit are bounded to 50 ms; actual elapsed pause time is credited
to accommodate coarse OS timers. Inactive pressure clears the budget. Streaming
work is timed inside engine Read/Dispose calls, excluding caller time between
rows. The wrapper is created only under pressure. This is an experimental ratio,
not a demonstrated performance guarantee.

Run `36315568607` compares four readers against the merged stack, and
`36315762447` compares one reader. All eight jobs completed and validated their
results. The latter source adds only tests and a production-excluded path counter
(`0c89561eb`). The 493-case Shared/coordinator/WAL selection passes on both .NET
10 and .NET 8, including pacing bounds and measured plaintext/encrypted streaming
cursors disposed from another thread.

Five paired rounds, mean paired changes with exploratory 95% bootstrap intervals:

| Five-times pacing vs merged, four readers | Writer throughput | Writer p99 | Reader throughput |
| --- | ---: | ---: | ---: |
| Linux point / writer | +0.97% [−5.07, +9.31] | +2.07% [−38.58, +45.75] | 335 → 16,405/s |
| Linux large / writer | +19.15% [+15.81, +23.69] | −23.36% [−42.10, −6.87] | 274 → 711/s |
| Linux point / checkpoint | −7.09% [−7.91, −6.07] | +6.59% [+4.77, +8.72] | 543 → 10,398/s |
| Linux large / checkpoint | −5.08% [−5.75, −4.17] | +11.22% [+8.65, +13.40] | 321 → 544/s |
| Windows point / writer | −5.20% [−6.49, −3.93] | +4.98% [+4.32, +5.70] | 224 → 5,935/s |
| Windows large / writer | −3.02% [−6.84, −0.35] | +23.50% [+6.43, +52.35] | 182 → 438/s |
| Windows point / checkpoint | −1.90% [−2.27, −1.47] | +0.45% [−3.07, +3.04] | 425 → 5,385/s |
| Windows large / checkpoint | −4.47% [−10.15, +1.20] | +8.25% [+0.14, +16.91] | 210 → 451/s |

The Linux writer-only host had variable durable-flush times; its apparent large
writer gain should not be generalized. Several stable checkpoint/Windows cells
still exceed the provisional writer budget. Phase measurements show that begin
latency is largely recovered: Linux checkpoint point begin fell 0.446 → 0.189 ms,
but update rose 4.813 → 5.518 ms and commit 8.176 → 8.886 ms. Reader CPU rose
975 → 6,091 ms per measured interval. Remaining execution/resource contention is
a better explanation than the original ownership queue alone.

With one reader, Windows checkpoint large still regressed −4.39% [−6.54, −2.38]
in writer throughput and +9.92% [+5.28, +13.71] in writer p99. Linux writer point
was −3.64% [−5.34, −2.18], with p99 +3.30% [−3.31, +9.92]. The one-reader Linux
checkpoint comparisons were too variable to establish equivalence. Raw results
retain all cells, including this inconclusive evidence.

The earlier combined yield implementation (`c5c54c9cb`) passed another bounded
12-invocation campaign with seeds 3012–3014 and 32 steps per primary case. Its
pre/post runner and library hashes match the saved manifest. Runtime-generated
Git metadata sampled the main checkout during later edits, so those printed
SHAs are not the executed binary identity: the manifest's build revision and
hashes identify the tested code. Final qualification will run from a frozen
checkout to avoid that metadata ambiguity. This campaign does not qualify the
later pacing wrapper.


## Seven-times pacing qualification

Revision `e81c28bb8` changes only the work-to-delay ratio from five to seven.
Direct run `36316381961` compares it with `0c89561eb`, not the merged baseline.
All four .NET 10 Linux/Windows jobs pass their data validation. Point reader
throughput decreases 28.9–34.8% and large reader throughput 10.8–24.5%. Writer
throughput improves 1.25–7.42%, but several intervals include no change; writer
p99 does not improve consistently. This is a measured tradeoff, not by itself
acceptance against the merged writer target.

Direct merged-baseline qualification is run `36317194132` (.NET 8/10, both hosts,
idle/writer/checkpoint, point/medium/large, one/four readers). Archived mmap
reader retention is measured separately by `36316627061`, with identical .NET 10
workloads and archived restoration `16131953114971557b44e9deaa78bd8ce9d45ab1`.
Their results are pending; cross-host ratios will not be multiplied to manufacture
an acceptance result.

In `e81c28bb8`, the advisory hint expires 100 ms after a writer requests ownership. It is not
cleared immediately at completion, and long operations do not continuously
refresh it. It neither proves a writer is alive nor authorizes storage access.
The pacing ratio is a scheduling policy, not a hard CPU quota or latency bound.
The hint can briefly pace reads after a write ends. In standard run `36316411546`,
Linux .NET 10 mixed-loop median mean latency improves 0.274 → 0.154 ms while p99
rises 0.835 → 1.112 ms (+30.1% paired [+17.4, +42.0]). Point/scan means improve
83.8%/41.1% and p99 improves 93.2%/49.5%. Linux .NET 8 mixed mean improves 44.2%,
with an inconclusive +6.1% p99 interval [−4.2, +21.8]. These reader-tail costs are
retained alongside the throughput gains.

A fresh bounded safety campaign ran from a clean frozen checkout at `e81c28bb8`.
All twelve invocations passed: seeds 3012–3014, 32 steps each for snapshot, shared,
MVCC retirement, and index. Eighteen reports include six built-in replays. Every
report names the same clean revision; the runner/library SHA-256 values match
before and after. Maximum invocation was 54.72 seconds; external artifact size
was 6,223,166 bytes. Fault scope remains the explicit process-death and injected
persistence models, not arbitrary hardware failure. Full CI `36316475858` and
remaining standard production comparisons are still pending.


## Complete pressure requests when writers finish

The .NET 8/10 Windows core results in `36316411546` reject retaining the hint
until expiry: mixed-loop mean latency rises 220–233% and p99 rises 942–989%
(roughly 1.4 → 14.1 ms). Coarse timer oversleep becomes especially costly when
each completed write resets the read cache and its scheduling history. Pure
reads and ordinary write-heavy workloads do not exhibit this mixed-loop problem;
the raw traffic rows retain those controls.

Library revision `093c6a96f` gives each hint an atomic deadline/request-sequence/
active-bit word. Completion conditionally clears only its own request, retaining
the sequence so repeated requests within one millisecond are distinguishable.
Queued writers publish before waiting but install their local completion token
only after obtaining ownership. Closing the writable engine, commit, rollback and
opening failures complete the request. A crashed requester still expires without
cleanup. This remains an advisory policy: it is not a count of every queued writer,
a liveness proof, a fairness guarantee, or part of storage admission authority.

Tests cover same-tick newer requests surviving older completion, deadline wrap,
plaintext/encrypted transaction nesting and completion, and an injected failed
open followed by unchanged committed data and cold reopen. The initial focused
selection passed 24 cases. A broader Shared/coordinator/WAL selection passed 696
cases on each of .NET 10 and .NET 8. New hosted comparisons remain pending. `36317773622` compares core workloads directly against the merged stack;
`36317775743` compares concurrent readers directly against `e81c28bb8` to measure
whether cleanup changes saturated writer/reader behavior. Workflow-only revision
`eea594b79` enables the focused core dispatch. Earlier runs do not qualify this
later library revision.


The direct archived comparison `36316627061` has completed Linux .NET 10 results.
Idle point throughput improves 17.37% [+14.94, +19.96] for one reader and 13.49%
[+12.71, +14.26] for four. Idle medium/large throughput is retained (four-reader
medium +2.45%, large +0.13%). Under writer pressure with four readers, point
throughput decreases 42.24% [−43.35, −41.01] while writer throughput improves
11.91% [+10.52, +13.09] and writer p99 improves 11.89%. With checkpointing, point
reads decrease 34.60% while writers improve 14.91%. Medium/large readers remain
12–53% faster than the archive with writer activity; however, large checkpoint
writer p99 regresses 11.90% [+8.93, +15.93]. These data motivate a separate,
unaccepted streaming-yield experiment (`478ecfc1e`, run `36318133551`) based on
the hint-completion revision. It cooperates every sixteen streaming rows while
under pressure; the normal branch has not adopted it.

The Windows writer-only archived comparison had substantially variable flush
latencies: four-reader large writer throughput ranges enough to give a paired
+100% mean with an interval [+11, +262], despite medians 21.7 → 29.0/s. It does
not establish a stable magnitude. Its raw samples and one-reader tail regressions
remain part of the evidence; they are not combined with another host's rates.


All six archived-comparison jobs completed with validated results. Windows idle
point readers improve 31.08% [+24.62, +37.62] for one reader and 36.35%
[+21.59, +54.52] for four; idle large-reader differences are inconclusive around
no change. In the Windows checkpoint run, four-reader large throughput decreases
37.84% [−51.36, −21.65] while writer throughput improves 51.60% [+22.03, +94.64]
and p99 improves 45.33%. One large reader instead increases writer p99 32.83%
[+4.12, +65.44] on a variable-flush host. These are unresolved tradeoffs, not
uniform preservation of archived reader speed or a writer-equivalence result.

The `e81c28bb8` standard run `36316411546` completed all eight jobs. Ordinary
write/transaction/checkpoint mean changes are generally small (about −2% to +5%);
Windows .NET 8 write p99 still has a +19.78% paired mean with a wide [+2.21,
+46.03] interval. Two/four-writer throughput changes range −2.07% to +2.34% across
the four host/runtime combinations. Offered-writer completion remains comparable;
the benchmark intentionally saturates one writer, so unmet offered demand and
long arrival tails are retained rather than reported as successful throughput.
All lifecycle checks and each production state campaign passed, including four
killed uncommitted writers, four killed readers and twelve validated generations
per candidate host/runtime. The hint-cleanup revision still needs its own results.

Full CI evidence on the earlier `e81c28bb8` includes native Linux ARM64 .NET
10.0.12: 4,699 passed and seven pre-existing unrelated skips across all partitions.
TRX output confirms the actual Arm64 runtime; all 42 affected mapped/opening/
pressure cases in the Shared/engine partitions passed. Windows x86 .NET 8.0.31
Shared concurrency/repeated-write/dump-contract selection passed 84 cases with
actual X86 runtime output. Its timeout artifact is the deliberate dump-contract
smoke, which captured and validated a full 32-bit process dump on the first attempt;
it is not an unreported failed database test.


## Further tail attribution and fenced rescan experiment

The later direct merged-baseline matrix `36317194132` does not establish the
writer target. Its Linux .NET 10 writer-only large/four case has writer p99
+122.21% [+15.89, +228.54], despite inconclusive throughput (−0.03% [−7.72,
+7.65]). Per-pair begin times are lower, update time slightly higher, and commit
means vary. Sampled WAL peaks rise from 1.712 to 6.791 MB. Other baseline and
candidate cells also have very variable tails; these adverse candidate results
remain unresolved, rather than being discarded as host noise. Windows point/four
writer throughput regresses 14.09% with p99 +39.45%; medium/four throughput
regresses 15.69% with p99 +21.81%. Current hint-completion comparisons are separate.

Benchmark revision `9f4867c78` retains per-phase p99 and the ten slowest complete
transaction breakdowns, using the already measured phase timestamps. It passed
an A/A harness smoke check with full payload/index validation; the smoke is not
performance evidence. Run `36318836474` directly compares the hint-completion
library to the merged baseline using those records.

The initial streaming-yield results do not support adoption. Windows large
checkpoint p99 regresses 53.49% [+4.86, +138.39], while Linux improves only 3.99%
and throughput is effectively unchanged. The experiment remains off the main
branch pending its final job.

A separate unaccepted branch (`f180da96f`, run `36319100538`) yields once and
rescans external leases only after a Shared checkpoint sees live readers, with
its existing structural fence still active. New admission remains excluded;
actual surviving leases or an unknown scan still prevent unsafe reclamation.
The hypothesis is that readers which finish during the first scan can allow a
full checkpoint, reducing WAL growth and later replay/commit cost. No fixed sleep
or unbounded drain loop is introduced. Safety tests and performance are pending;
the normal branch has not adopted this storage-path experiment.

The hint-completion library passed its clean frozen campaign at `eea594b79`:
12 invocations and 18 reports, the same three seeds/four targets, maximum 55.16
seconds, 6,223,232 artifact bytes, matching pre/post binaries and exact clean
revision in every report. This qualifies neither the later rescan experiment nor
streaming yields.


All four streaming-yield jobs completed with validated data. Linux writer large
throughput is unchanged (−0.04% [−0.58, +0.54]); medium improves 1.57% but readers
lose 3.41%. Windows writer large throughput regresses 1.62% and readers lose
5.87%. Together with the adverse Windows checkpoint tail, this experiment is
rejected and remains only on its separate branch.

Hint-completion run `36317775743` completed all four jobs. Linux point/large writer
throughput changes are within about ±0.5% versus the expiry-only version; reader
changes are also small. Windows writer point throughput changes −3.97% [−5.06,
−2.92] while p99 improves 2.22%; other Windows throughput intervals include zero.
The direct core run `36317773622` passed all four host/runtime jobs. Windows mixed
mean latency now improves 53.44% (.NET 8) and 53.72% (.NET 10) against the merged
baseline, with no statistically established p99 regression. This fixes the earlier
14 ms timer tail; it does not by itself qualify all concurrent-writer workloads.
Linux mixed mean improves 35.87–55.34%, again with inconclusive p99 differences.

Full CI `36316475858` completed successfully: all 47 jobs passed at `e81c28bb8`.
This covers the restored protocol and narrowed scopes before hint completion;
it is not a claim of hosted CI for the latest library changes.

The fenced-rescan tests now pass all six plaintext/encrypted cases: a departing
reader permits full reclamation, an actual held snapshot preserves every old row
and indexed result, and an unknown second scan leaves data and WAL bytes unchanged.
An initial encrypted empty-WAL assertion incorrectly expected physical length zero;
the encryption header remains 8 KiB. The corrected assertion compares with the
known-empty length captured before the update. The failed and corrected reports
are retained. Broader rescan safety and performance qualification remain pending.


The completed phase run `36318836474` gives more specific attribution. In the
stable Linux checkpoint comparison, writer throughput changes −3.76% for point,
−2.75% for medium and −1.53% for large; total writer p99 changes −0.07%, +4.54%,
and +6.45%. Large explicit-checkpoint p99 rises 6.51 → 13.77 ms, while its commit
mean falls 8.43 → 7.34 ms. On Windows writer-only point, throughput regresses
6.01% and p99 rises 11.56%; mean update time rises 4.45 → 6.22 ms, with begin and
commit means lower. Its update p99 rises 17.34 → 21.12 ms. The Linux writer-only
host's very long tails occur in commit (roughly 92 → 201 ms point p99), while
begin/update tails remain around 3/10 ms. The raw phase distributions distinguish
these phenomena; they are not collapsed into one explanation.

The unconditional rescan (`f180da96f`) passes 702 selected Shared/coordinator/WAL
cases on .NET 10 after its six new cases. Its Linux checkpoint medium case improves
reader throughput 7.59%, writer throughput 1.77%, and writer p99 5.27%, but point
writer throughput regresses 2.09%. Windows checkpoint large writer p99 improves
8.62%, with inconclusive throughput and reader differences. Point checkpoint p99
instead rises 1.13%. This does not justify an unconditional additional scan.
A subsequent experiment restricts it to an already accumulated WAL (more than
one confirmed transaction); existing tests exercise the retained old generation
across two commits. The original rescan remains unaccepted.

A separate scalar experiment (`9935d3a0c`, run `36319814711`) compares a pacing
ratio of ten with the current seven, preserving every bound and admission rule.
This quantifies the remaining reader/writer tradeoff. It has not been adopted.
