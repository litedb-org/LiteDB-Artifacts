# LiteDB fuzz result: page

- Status: **PASS**
- Seed: `2951000`
- Steps: `9254052` / requested `100`
- Duration: `900.002s`
- Git SHA: `2f36856b46745e69bf39208299ad1ee4aaa3a979`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/fuzz-pr2951-review-20260920/20260920-205229-page-s2951000-w0-r6486dd51/replay.json`

## Metrics

- operations: `{ inserts = 2300393, updates = 2302212, deletes = 2300367, defrags = 1200158 }`
- survivingSlots: `26`
- usedBytes: `7658`
- freeBytes: `362`
