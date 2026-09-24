# PR #3009 shared-read performance evidence

Follow-up to [LiteDB #3003](https://github.com/litedb-org/LiteDB/pull/3003), published
for [LiteDB #3009](https://github.com/litedb-org/LiteDB/pull/3009).

Final library sources:

| Build | LiteDB commit |
| --- | --- |
| prestack (`bef9aa^`) | `0fd277aaed127b9dec99524277fb4173a2351167` |
| baseline (PR #3003) | `137a1d934bd4b11339a397612394fa95a19e6f35` |
| candidate | `459e525d13ea6b0a5dc88d6dcb0cbf581561ad02` |

Runner source: `81e28693cb3943ec554b02de640108c61ae7a24d`. Later report commits do not
change the measured library. `final-builds.json` identifies all six production
assemblies. Local paths in the metadata are provenance, not portable download
links; rebuild the listed commits to reproduce.

Ubuntu 24.04.3, Ryzen 9 3900X, x64, ext4/LVM; SDK 10.0.400 and runtimes 8.0.30 and
10.0.11. Other database/fuzz workloads were active. No builds, tests or profiling
from this task ran alongside the final benchmark. These are Linux measurements;
Windows performance remains unmeasured.

## Final results and reproduction

- `final-shared-startup.jsonl`: 36 runs, count-only warmup (1,000 point calls/20
  scans), 8,000 measured point calls or 150 scans.
- `final-shared-steady.jsonl`: 54 runs, at least ten seconds warmup, 20,000 point
  calls, 1,000 scans or 2,000 mixed calls (one update per nine reads).
- `final-direct.jsonl`: 12 direct-mode scan controls, ten seconds warmup and 1,000
  measured scans, baseline/candidate only.
- `final-summary.md`: medians of run means, observed ranges, p99, allocation, CPU
  and close time. P99 values are medians of per-run percentiles, not pooled.
- `final-comparison.py`: exact orchestration; edit local build/scratch paths to
  reproduce. Build the runner separately against each already-built production
  DLL following `runner/README.md`. The runner validates full document contents,
  actual runtime/binary identity and final WAL cleanup. It rejects test hooks.
- `summarize-final.py` regenerates `final-summary.md` from these JSONL files.

All measurements retain actual warmup count/time, ten consecutive timing windows,
first operation (after seeding, not cold process startup), CPU, allocation and
separate connection-close time. WAL sizes are final observations, not peak size.

## Profiles and incremental experiments

`profiles.tar.gz` contains managed EventPipe traces/Speedscope conversions, profile
summaries, and two Linux CPU profiles with captured managed method maps. The
managed sampled-thread-time profile includes waits and safe-point bias; it is not
CPU utilization. Inclusive percentages overlap. Some native runtime frames remain
unresolved in CPU profiles.

`startup-point.perf` diagnoses startup CRC/JIT costs. `reader-scan2.perf` attaches
after warmup to diagnose document decoding costs. An unsuccessful coordinator
launch trace and the scan capture without usable managed maps are excluded. The
successful coordinator trace is `coordinator-attached.nettrace`; client protocol
waiting includes server execution and durable commit time.

The non-`final-` JSONL files are incremental experiments with their own embedded
commit/runtime/binary identity and different warmups. They establish attribution,
not a substitute final benchmark. `comparison.jsonl` compares the original PR head
`f0228d301` with the fingerprint-only fix. Header, marker, CRC, reader and name-cache
files isolate subsequent changes. Their percentages cannot be added together.

## Validation

`validation.tar.gz` retains TRX and console logs; `final-validation.json` summarizes
the final full-suite result. Candidate source `459e525d1`, Release,
`TestingEnabled=true`: 4,499 passed, zero failed, seven existing skips on **each**
of .NET 8 and .NET 10, across nine partitions. Raw partition totals include 16
repeated runtime/test-hook guards and therefore show 4,515 passed. All 2,121 test
methods are assigned to exactly one partition.

Also retained: 68 focused passes on each runtime, 26 checksum passes with hardware
intrinsics disabled, all-framework build output, and successful actual-5.0.21
create/migrate/refuse/verify compatibility output. Legacy target frameworks compile
locally; their runtimes were not executed locally.

Two explicitly non-final result groups are retained for audit:

- `counterfactual/`: four intentional failures after removing cursor cleanup,
  restoring duplicate header reads and restoring exception-based marker probes.
  They demonstrate that the regression tests detect those changes. Source was
  restored and the final focused suites passed afterwards.
- `initial-fixture-errors/`: early cursor tests with incorrect fixture schema size,
  a page-cache-bypassed fault injector and snapshot-owned references in the GC
  fixture. These fixtures were corrected before the final passing suites.

`hooks-guard.log` is an expected rejection of a test-hook DLL. The compatibility
helper rebuilds a test-hook assembly; production assemblies were rebuilt and all
six hashes reverified before final measurements. No test-hook timing is included.
