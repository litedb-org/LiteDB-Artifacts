# PR #133: semantics-preserving Shared holder/wrapper reuse

Baseline: `c8c0cfab623a22880b1d71eb966b09e89f9153b9`.
Final production source: `8fb87fa123a88ef143e3fdfa1bae564032f9a941`.
Final validation: 966 distinct passing cases on each of net8.0 and net10.0.

The final implementation reuses the holder worker and child SharedEngine wrapper,
but closes the storage core, idle data/log handles, coordination participation,
and the child's native admission between handles. Changed effective password or
serialized collation invalidates the cached wrapper. No other experiment is included.

## Contents

- `benchmark/`: final 24-process manifest, per-window raw results, stderr and summary.
- `production-runners.zip`: exact production baseline/candidate runners (`head/`,
  `reuse/`), with matching benchmark executables and runtime configuration.
- `production-identities.json`: measured LiteDB DLL hashes; test hooks are absent.
- `benchmark-config.json`, benchmark driver and summarizer: exact alternating-order
  configuration and analysis code. Paths record the original host; update runner
  paths after extracting the archive to replay it.
- `validation.zip`: TRX files, logs and final test binaries with child-process harnesses.
  **`reuse-release-*` identifies final passing validation**, with the four partitions
  listed in `test-summary.json` and `reuse-release-validation-*.json`.
- `run-reuse-release-validation.py`: final partition driver, invoked with a checkout
  directory and `net8.0` or `net10.0` from the repository root.
- `admission-comparison.zip`: standalone public-API baseline/pre-fix/final comparison.
  Run `dotnet bin-baseline/Admission.dll`, `dotnet bin-prior/Admission.dll`, and
  `dotnet bin-fixed/Admission.dll`. The baseline/final exit 0 and allow a Direct
  open between completed handles; the prior retained-admission build exits 1.
  Each prints the actual loaded DLL hash. The permanent tests additionally cover
  encrypted files, three external Direct writes, wrapper reuse and refreshed reads.
- `environment.json`: SDK/runtime, CPU, OS and filesystem details.

The test archives intentionally retain development evidence. `reuse-settings-before`
has five expected failures demonstrating stale password/collation behavior; its
`after` run passes. `reuse-qualified-handles-*` has two settings-abandonment failures
that exposed releasing native admission before coordination participation ended.
Those unchanged tests pass in the final `reuse-release-*` runs. Earlier `reuse-final-*`
runs predate complete admission cleanup; they are not final safety evidence.

## Measurement

Production Release/net10.0 with TestingEnabled=false, .NET 10.0.11, Ubuntu 24.04 x64,
ext4. The runner keeps a facade alive and uses 0/1/10 point reads per handle; zero
still performs begin/GetCollection/commit. Four fresh processes per build/case,
alternating order, five seconds warmup and five one-second measured windows,
DOTNET_TieredCompilation=0. No local builds/tests/profiles overlap the final timing.
Every run checks values/index/sentinel and cold reopen. Allocations are managed
bytes per transaction across all threads. Host load is otherwise uncontrolled.

Run the archived driver with the adjusted configuration, then:

```bash
python3 summarize-transaction-handle-steady.py benchmark
```

## Superseded experiment data

`superseded/` retains the initial partial timing of `40d6a1874`, its production
binaries and earlier validation. Timing was interrupted after finding settings and
admission differences; it is not combined with final measurements. The historical
approximately 4.69x child-reuse result also retained extra admission/coordination
state and does not describe the final semantics-preserving implementation.

## Final measured results

| Reads/handle | Head tx/s | Reuse tx/s | Paired speedup | Head bytes/tx | Reuse bytes/tx |
| --- | ---: | ---: | ---: | ---: | ---: |
| 0 | 1,449 | 1,897 | 1.311× | 227,782 | 220,778 |
| 1 | 1,298 | 1,679 | 1.303× | 237,902 | 230,905 |
| 10 | 1,103 | 1,411 | 1.279× | 302,146 | 295,222 |

All 24 final processes completed and verified. The original approximately 4.69× / 155 KB one-read result was not reproduced on the semantics-preserving implementation.
