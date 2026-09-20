# LiteDB fuzz result: shared

- Status: **PASS**
- Seed: `2951000`
- Steps: `462` / requested `100`
- Duration: `901.378s`
- Git SHA: `2f36856b46745e69bf39208299ad1ee4aaa3a979`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/fuzz-pr2951-review-20260920/20260920-205229-shared-s2951000-w0-r9e588450/replay.json`

## Metrics

- integrityPages: `4`
- rounds: `462`
- processes: `1386`
- acknowledgedRows: `35400`
- crashPositions: `12`
- integrityChecks: `462`
