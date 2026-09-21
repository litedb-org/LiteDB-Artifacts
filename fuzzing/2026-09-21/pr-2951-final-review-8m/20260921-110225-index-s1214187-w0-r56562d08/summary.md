# LiteDB fuzz result: index

- Status: **PASS**
- Seed: `1214187`
- Steps: `5388` / requested `100`
- Duration: `37.276s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `36BD95FAD9DCCA78448280903C585E01A6603E43BE3A28019F52C6F99EA717AC`
- Trace SHA-256: `360F087C2A8F48DC064BD8A7B1C86D31DBDA7F82EA4337429071D41238FBAF01`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110225-index-s1214187-w0-r56562d08/replay.json`

## Metrics

- semanticIndexKeys: `53847`
- semanticDocuments: `11798`
- semanticIndexes: `748`
- integrityPages: `13`
- integrityPhysicalPages: `13`
- uniqueConflictScenarios: `6`
- novelStates: `73`
- documents: `69`
- keyMovingUpdates: `749`
- integrityChecks: `186`
