# LiteDB #3014 Shared-mode performance evidence

Source PR: https://github.com/litedb-org/LiteDB/pull/3014, stacked on #3013.
The measured parent is `d2fb099acebb58dbbb07acbeea49c05bb025defd`.
Selected library/test source: `2f7a2350c8476e6807e5c2e3e5f09ea5b46d542b`.
All concurrent Shared participants in a database use exactly the same LiteDB version.

This directory preserves successful and unsuccessful investigations. Inclusion in
an archive does **not** mean a candidate or campaign qualified. The source PR and
its results note distinguish final evidence from superseded, incomplete, negative
control, instrumented, and excluded-experiment results.

## Investigation archive

`investigation-evidence.tar.gz` contains preceding production comparisons,
instrumented cost diagnostics, discriminating negative controls, failure/repair
TRX reports, source patches, commands and campaign classifications. The explicit
`preserved-images/` directory contains only synthetic test databases and control
files copied/preserved around intentionally broken or failed implementations.
These images have not been opened for recovery while preparing this publication.
All 32 original image hashes were rechecked and matched their preservation manifests.

`investigation-files.json` records original and published SHA-256 for each archive
entry. Text copies replace the local checkout prefix with `${WORKTREE}` and the
remaining local home prefix with `${HOME}`; binary database images are unchanged.
Original local artifacts remain intact. Substitute those markers when reproducing
commands. The archive is evidence, not a source checkout; use its recorded LiteDB
revisions and patches in separate disposable checkouts.

Key groups:

- `hosted-reviewed-core`, `hosted-reviewed-traffic`, `hosted-reviewed-readers`:
  production `9655832f2` versus parent; five alternating pairs on Windows/Linux,
  .NET 8/10. Timed reader workers do not sample WAL files; the controller records
  sampling CPU separately.
- `hosted-mixed-long`: ten pairs of 20,000 mixed calls, production `9655832f2`.
- `hosted-writer-refined`: excluded incremental-reopen experiment, including
  failing native campaigns. Fix `c5d379607` preserves allocator witnesses for
  intermediate safepoints; it remains excluded on performance grounds.
- `hosted-fixed-yield`: rejected fixed 10 ms pin yielding and thread lifecycle cost.
- `hosted-read-demand*`: excluded read-demand-only authority experiment.
- `cost-diagnostics-complete`: temporary instrumentation of parent and `9655832f2`;
  overlapping phase scopes explain work and must not be summed or used as headline
  production speedups. The preceding `cost-diagnostics` group missed ordinary
  writable close and remains preserved as incomplete instrumentation.
- `mmap-negative-*`, `writer-reuse-*`, `writer-resume-safepoint-*`:
  mutants, fixed-after comparisons, original files and manifests.
- `architecture-gate`: an initially insufficient negative control passed because
  a later checkpoint independently revoked the page. `architecture-gate-v2`
  checks visibility immediately after acknowledgement and fails without revocation.
- `publication-allocation`: old publication allocates 320,000 bytes across 1,000
  cycles; selected publication allocates zero, followed by 99 passing focused tests.
- `fuzz-admission-final`: incomplete older campaign; a shared corpus root exceeded
  the 290-second invocation budget while repeating earlier seeds. Primary seed
  3016 had passed. `incomplete-classification.json` preserves the original failure.
- `fuzz-reviewed-final`, `fuzz-0a0002b-final`: superseded partial campaigns, stopped
  between native invocations after source changes. They are not full campaign passes.
- `ci-965-timeouts`: intentional timeout-dump contract smoke, not a database hang.

## Reproduction and interpretation

Production benchmarks use Release with `TestingEnabled=false`; test/fuzz builds
use `TestingEnabled=true` in separate checkouts. The campaign script partitions
native targets/seeds below the 300-second session bound and preserves discovered
inputs for individual `--replay <replay.json>` execution. Built-in regressions still
run. Local campaigns use private ext4 TMPDIR, one heavy local job at a time,
256 MiB runner/512 MiB external campaign caps and a 10 GB total task disk budget.

Raw outputs identify actual revisions, runtime/OS, commands, inputs and hashes.
Production comparison scripts validate data and record final close/WAL state.
Paired intervals are exploratory per-metric intervals, not simultaneous confidence
bounds or equivalence tests. Saturation workloads can perform different amounts
of read work; writer results and aggregate CPU must be considered alongside read
throughput. Working set/managed retention in the timing runners includes their
latency samples and is not isolated library cache memory. No arbitrary power-loss
or device-fault guarantee follows from finite process-death testing.

Final-source evidence will be added separately after the active qualification runs
finish. No completion claim is made by this initial archive.
