# LiteDB fuzz result: index

- Status: **PASS**
- Seed: `2951000`
- Steps: `76502` / requested `100`
- Duration: `900.007s`
- Git SHA: `2f36856b46745e69bf39208299ad1ee4aaa3a979`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/fuzz-pr2951-review-20260920/20260920-205229-index-s2951000-w0-rcde70cc1/replay.json`

## Metrics

- integrityPages: `14`
- documents: `65`
- keyMovingUpdates: `10897`
- integrityChecks: `2639`
