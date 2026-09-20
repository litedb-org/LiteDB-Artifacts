# LiteDB fuzz result: sort

- Status: **PASS**
- Seed: `2951000`
- Steps: `35433` / requested `100`
- Duration: `900.009s`
- Git SHA: `2f36856b46745e69bf39208299ad1ee4aaa3a979`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/fuzz-pr2951-review-20260920/20260920-205229-sort-s2951000-w0-r798248ea/replay.json`

## Metrics

- topNChecks: `70866`
- longKeyDocuments: `496062`
- documents: `1320`
- collation: `/Ordinal`
