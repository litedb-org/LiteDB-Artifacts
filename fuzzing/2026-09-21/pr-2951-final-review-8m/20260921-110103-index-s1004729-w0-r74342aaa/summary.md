# LiteDB fuzz result: index

- Status: **PASS**
- Seed: `1004729`
- Steps: `5510` / requested `100`
- Duration: `40.029s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `7526421E18E72C6DA4DC017C781318AC866BFDF37B2CAC8505BBDD234BAF5D2E`
- Trace SHA-256: `161B099276DA21B9A4A2E67DCEB9381CE6F59A1F4A2AA47084CC617551DDBF8A`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110103-index-s1004729-w0-r74342aaa/replay.json`

## Metrics

- semanticIndexKeys: `57879`
- semanticDocuments: `12535`
- semanticIndexes: `768`
- integrityPages: `14`
- integrityPhysicalPages: `14`
- uniqueConflictScenarios: `6`
- novelStates: `73`
- documents: `61`
- keyMovingUpdates: `779`
- integrityChecks: `191`
