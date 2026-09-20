# LiteDB fuzz result: index

- Status: **PASS**
- Seed: `526191752`
- Steps: `30` / requested `30`
- Duration: `0.532s`
- Git SHA: `d9d10cc37f7ea113772640a7cd0cacd1544fbc5a`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-5261919744/persistence/20260920-224927-index-s526191752-w0-r81c233e0/replay.json`

## Metrics

- novelStates: `12`
- semanticIndexKeys: `20`
- semanticDocuments: `4`
- semanticIndexes: `8`
- integrityPages: `7`
- integrityPhysicalPages: `7`
- documents: `2`
- keyMovingUpdates: `5`
- integrityChecks: `2`
