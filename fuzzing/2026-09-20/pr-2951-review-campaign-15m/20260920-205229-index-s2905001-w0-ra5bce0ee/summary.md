# LiteDB fuzz result: index

- Status: **PASS**
- Seed: `2905001`
- Steps: `200` / requested `200`
- Duration: `2.423s`
- Git SHA: `2f36856b46745e69bf39208299ad1ee4aaa3a979`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/fuzz-pr2951-review-20260920/20260920-205229-index-s2905001-w0-ra5bce0ee/replay.json`

## Metrics

- integrityPages: `10`
- documents: `43`
- keyMovingUpdates: `29`
- integrityChecks: `7`
