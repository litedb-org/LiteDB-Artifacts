# LiteDB fuzz result: index

- Status: **PASS**
- Seed: `2905001`
- Steps: `200` / requested `200`
- Duration: `2.089s`
- Git SHA: `d9d10cc37f7ea113772640a7cd0cacd1544fbc5a`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-5261919744/persistence/20260920-224927-index-s2905001-w0-r6d8de17b/replay.json`

## Metrics

- novelStates: `46`
- semanticIndexKeys: `1023`
- semanticDocuments: `219`
- semanticIndexes: `28`
- integrityPages: `9`
- integrityPhysicalPages: `9`
- documents: `46`
- keyMovingUpdates: `38`
- integrityChecks: `7`
