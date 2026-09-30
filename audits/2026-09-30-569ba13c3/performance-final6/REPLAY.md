# Replay the packaged benchmark

This folder contains runnable assemblies and all adjacent runtime/dependency files.
Install a matching .NET10 runtime; the recorded environment was10.0.11 on Ubuntu
24.04.3 x64/ext4. A different host is a new campaign, not reproduction of an exact
throughput number. Keep the complete `bench-*` directories together.

From this `performance-final6` directory, one final-candidate case is:

```sh
DOTNET_TieredCompilation=0 dotnet bench-final6/TransactionHandleBenchmarks.dll \
  569ba13c3b3867131c8687e7884bed65131edfbf steady shared handle read 1 5 5
```

For the full alternating campaign, copy `final6-reuse-benchmark-config.json` and
`final6-upstream-benchmark-config.json` to new filenames. Replace each version's
`runner` value with the absolute path to its packaged DLL in this directory:
`head` → `bench-head`, `dev` → `bench-dev`, `parent` → `bench-parent`, and
`final` → `bench-final6`. Retain the revisions, order, rounds and cases. Then run:

```sh
python3 benchmark-transaction-handle-steady.py --config replay-reuse.json --output replay-reuse-results
python3 benchmark-transaction-handle-steady.py --config replay-upstream.json --output replay-upstream-results
python3 summarize-transaction-handle-steady.py replay-reuse-results
python3 summarize-transaction-handle-steady.py replay-upstream-results
```

The configs' normalized `__WORKSPACE__` paths describe the original layout and are
not executable until replaced. Keep replay output separate from `reuse-raw` and
`upstream-raw`. `runner-source/README.md` retains repository build instructions;
its repository-relative paths are not this packaged layout. Exact recorded
binaries, raw output, hashes and the independent arithmetic audit remain available
without rebuilding.
