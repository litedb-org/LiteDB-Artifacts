# LiteDB fuzz result: index

- Status: **PASS**
- Seed: `900000`
- Steps: `5621` / requested `100`
- Duration: `40.035s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `8E00FEB560AD6041052CE8D23915DDF79AA3D08CF223D64B1339E06C96A2C7B8`
- Trace SHA-256: `2810F6088FAA86E5F77F30D5C88401641384B98A60B9F8F2B6CEF0138D742C5C`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110022-index-s900000-w0-r09e9a302/replay.json`

## Metrics

- semanticIndexKeys: `55999`
- semanticDocuments: `12235`
- semanticIndexes: `780`
- integrityPages: `14`
- integrityPhysicalPages: `14`
- uniqueConflictScenarios: `6`
- novelStates: `79`
- documents: `71`
- keyMovingUpdates: `804`
- integrityChecks: `194`
