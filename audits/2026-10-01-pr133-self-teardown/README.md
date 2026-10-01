# PR #133: Shared teardown review, separated upstream fix, and corrected-baseline performance

Final PR head: `dace941d125111f89a4e18ed2b008a251f7f0ccb`.
Independent upstream draft PR #3077: `9e6111802619d30f5aa38c3fedd039912306aebf`.
Production tree for PR #133: `c1ae61d3d0c59e1d7671d843d7d3f0ce6188fda0` (unchanged from tested `56c8680ab` and benchmark source `52dd7f579`).

The review's two paths were executed, not merely source-traced. Same-connection getter teardown self-wait is independently witnessed on PR revision `3abead6dc`; premature native writer release also reproduces on upstream dev `023c2b4ba`, stacked parent `49c327cf1` and published package `6.0.0-prerelease.319`. The correction marks executing retained-core teardown, refusing unsafe same-connection public reentry before waiting, reopening or changing ownership. Ordinary recursion and other-database callbacks remain supported. The independent upstream fix is merged into #133 through shared ancestry. No group commit, IPC service, retained storage cache or writer-lock retention is added.

## Contents and interpretation

- `pr133-self-teardown-review.md`: scoped correction, attribution, invariants and remaining constructor-internal cleanup boundary.
- `validation/`: local net8/net10 close selection (54 cases, overlapping), disjoint audit and ownership selections (1,209 cases per runtime), net462 build, production before/after proof reports and diagnostics. Earlier unsuccessful setup/build/CI logs remain labeled by their original names; final reports supersede them. `upstream-linux-build.log` records the stale hardcoded matrix-count assertion fixed by asserting the exact expected job set; no production behavior was changed for that failure.
- `attribution/`: independent bad-baseline execution, exact runtime files and retained fixtures. Upstream no-reentry controls pass; unguarded getter/dispose cases can later report checksum failure. Do not interpret process kills as modeled power loss. No peer writes were deliberately raced through the exposed ownership gap.
- `upstream-proof/`: production published-package/source proof, source/fixture copies, exact runtimes and raw report. The known-bad child exits inside the callback at the witnessed unsafe release, avoiding an unprotected checkpoint tail.
- `binaries/proofs/`: complete manually executed production layouts for both release and wait proofs. Source expectations are exit 2/FIXED_VERIFIED; known-bad expectations are exit 0/TEARDOWN_DEFECT_VERIFIED. A watchdog timeout is a failure, never proof of the defect.
- `source/`: exact Git source archives, full commits and production-tree identities, and per-member Git-blob verification counts.
- `parent-baseline/`: local manually merged corrected parent `8a630b26b`, build diagnostics and focused net8 proof. It is not fully safety/CI-qualified and is not proposed as a production release.
- `performance/`: final report/disposition, 84 fresh-process runs and 420 windows, driver/configs, before/after hashes and independent audit. Read-only handles measure lifecycle overhead, not durable-write throughput. Ordinary-call regressions remain explicit. Shared-host variability and baseline adaptation prevent attribution to individual guards.
- `binaries/benchmarks/`: exact production benchmark layouts, including the unchanged pre-reuse baseline. Only holder+wrapper reuse is implemented; core reopen and native release remain mandatory.

The original experimental 4.69× / ~155 KB Shared-handle claim is not reproduced. Final one-read handles measure 1,307.5→1,711.4 tx/s (median paired +30.38%) and 237,908→230,941 bytes/transaction. Corrected-parent ordinary/legacy costs remain, so safety success is not automatic performance acceptance.

## Replay

Run the retained production proof layouts with .NET 8:

```
dotnet binaries/proofs/SharedSelfTeardownRelease-source-final/SharedSelfTeardownRelease.dll --release-only
dotnet binaries/proofs/SharedSelfTeardownWait-source-final/SharedSelfTeardownWait.dll --wait-only
```

Their expected fixed exit is 2. Replace `source-final` with `package-final` for known-bad execution (expected 0). These proof controllers launch isolated child processes and validate both encryption modes and controls.

For fresh performance runs, rewrite each config's `runner` path to the matching retained `binaries/benchmarks` directory, then run `performance/benchmark-transaction-handle-steady.py --config CONFIG --output NEW_DIRECTORY`. Use production .NET 10.0.11, no competing local work and preserve all outputs. Do not combine replay timings from another host with this campaign's ratios.

## Publication integrity

`publication-normalization.json` maps normalized diagnostic copies to their original hashes; machine-specific path prefixes are placeholders. Source archives and all runtime/support directories listed in `publication-exclusions.json` are preserved byte-for-byte. Nested historical SHA256SUMS files describe their original packages and may precede diagnostic normalization; the root SHA256SUMS describes the final public archive. Exact source content is independently checked against Git blobs. No prior published evidence archive was modified.

Three existing quarantines remain unexecuted; a passing safety report is bounded by those existing exclusions and the documented untested states. Hosted results must be read at their exact recorded head/merge identity, separately from local results.

## Final hosted qualification

PR #133 [CI36821386840](https://github.com/JKamsker/LiteDB/actions/runs/36821386840) passed all 39 jobs at final head. The downloaded safety-evidence ZIP matches GitHub’s SHA-256 digest; its 29 test legs report no partition failures, errors or warnings. Tested merge `5433b1c89c0870dc5d953bf1027e14b076b0a081` has the same tree as `dace941d1`. All 25 regression proofs, Fuzz, index migration, Shared production comparisons and PR safety checks passed. Independent macOS artifact inspection confirms all 54 relevant cases on net8/net10.

Upstream #3077 [CI36821345272](https://github.com/litedb-org/LiteDB/actions/runs/36821345272) and its other four workflows pass at `9e6111802`; tested merge `d4f25e6b` has no file differences. Its safety report covers 4 legs and the same 3 existing quarantines. Independent Linux artifact inspection confirms24 self-close + 20 ordinary peer + 10 peer-close cases on both .NET 8.0.31 and 10.0.12. The independent reports do not claim every hosted binary ZIP was downloaded or digest-verified.
