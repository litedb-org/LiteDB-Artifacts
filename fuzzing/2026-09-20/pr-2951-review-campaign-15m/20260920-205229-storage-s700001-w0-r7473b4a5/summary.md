# LiteDB fuzz result: storage

- Status: **PASS**
- Seed: `700001`
- Steps: `12` / requested `12`
- Duration: `0.330s`
- Git SHA: `2f36856b46745e69bf39208299ad1ee4aaa3a979`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/fuzz-pr2951-review-20260920/20260920-205229-storage-s700001-w0-r7473b4a5/replay.json`

## Metrics

- integrityPages: `62`
- files: `1`
- bytes: `31522`
- reopens: `0`
- interruptedTransactions: `1`
- atomicUploadProbes: `0`
- injectedUploadFailures: `0`
- integrityChecks: `1`
