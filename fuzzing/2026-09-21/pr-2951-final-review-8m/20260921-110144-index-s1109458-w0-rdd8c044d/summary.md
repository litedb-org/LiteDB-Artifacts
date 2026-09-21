# LiteDB fuzz result: index

- Status: **PASS**
- Seed: `1109458`
- Steps: `5715` / requested `100`
- Duration: `40.037s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `0764F26A4E6FC4895A3F24C3345EFC4200AF5C0BD69E2D3A3F369564B1805D64`
- Trace SHA-256: `2B4AC2BFED340201F9A98BD83A903D90811C54BB2CDC2E6A8C343C5D687D46D5`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110144-index-s1109458-w0-rdd8c044d/replay.json`

## Metrics

- semanticIndexKeys: `57724`
- semanticDocuments: `12658`
- semanticIndexes: `796`
- integrityPages: `14`
- integrityPhysicalPages: `14`
- uniqueConflictScenarios: `6`
- novelStates: `71`
- documents: `71`
- keyMovingUpdates: `803`
- integrityChecks: `198`
