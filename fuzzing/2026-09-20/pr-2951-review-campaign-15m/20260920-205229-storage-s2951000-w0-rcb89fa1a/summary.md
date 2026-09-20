# LiteDB fuzz result: storage

- Status: **PASS**
- Seed: `2951000`
- Steps: `13658` / requested `100`
- Duration: `900.023s`
- Git SHA: `2f36856b46745e69bf39208299ad1ee4aaa3a979`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/fuzz-pr2951-review-20260920/20260920-205229-storage-s2951000-w0-rcb89fa1a/replay.json`

## Metrics

- integrityPages: `514`
- files: `11`
- bytes: `2748319`
- reopens: `1537`
- interruptedTransactions: `1468`
- atomicUploadProbes: `803`
- injectedUploadFailures: `803`
- integrityChecks: `418`
