# LiteDB fuzz result: transaction

- Status: **PASS**
- Seed: `700001`
- Steps: `26` / requested `26`
- Duration: `0.330s`
- Git SHA: `2f36856b46745e69bf39208299ad1ee4aaa3a979`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/fuzz-pr2951-review-20260920/20260920-205229-transaction-s700001-w0-r744d0e8b/replay.json`

## Metrics

- integrityPages: `8`
- documents: `4`
- collections: `2`
- integrityChecks: `1`
- commits: `0`
- rollbacks: `0`
- reopens: `2`
- schemaChanges: `4`
