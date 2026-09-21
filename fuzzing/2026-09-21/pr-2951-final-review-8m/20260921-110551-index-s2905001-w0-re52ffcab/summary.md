# LiteDB fuzz result: index

- Status: **PASS**
- Seed: `2905001`
- Steps: `200` / requested `200`
- Duration: `2.014s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `3EC527C8C7EA771CE24858A19379275304454A143EB4D3A3F4A6E3314EEDFF3B`
- Trace SHA-256: `4CF71F5085BE740FD514CE8DC09D764CBFFEE2082C097F27649442111229F867`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110551-index-s2905001-w0-re52ffcab/replay.json`

## Metrics

- semanticIndexKeys: `1029`
- semanticDocuments: `222`
- semanticIndexes: `32`
- integrityPages: `9`
- integrityPhysicalPages: `9`
- uniqueConflictScenarios: `6`
- novelStates: `46`
- documents: `46`
- keyMovingUpdates: `38`
- integrityChecks: `7`
