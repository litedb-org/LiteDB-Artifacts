# LiteDB fuzz result: transaction

- Status: **PASS**
- Seed: `2951000`
- Steps: `157786` / requested `100`
- Duration: `900.013s`
- Git SHA: `2f36856b46745e69bf39208299ad1ee4aaa3a979`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/fuzz-pr2951-review-20260920/20260920-205229-transaction-s2951000-w0-r57e6eea0/replay.json`

## Metrics

- integrityPages: `12`
- documents: `49`
- collections: `2`
- integrityChecks: `3798`
- commits: `2457`
- rollbacks: `2491`
- reopens: `7307`
- schemaChanges: `14694`
