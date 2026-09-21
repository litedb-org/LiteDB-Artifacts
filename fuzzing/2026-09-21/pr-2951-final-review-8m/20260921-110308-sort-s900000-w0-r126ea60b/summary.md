# LiteDB fuzz result: sort

- Status: **PASS**
- Seed: `900000`
- Steps: `3098` / requested `100`
- Duration: `40.034s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `E25C0E4F9FC338D4EC0A46D144401045439D90C8E8AB73A3060B1F264C0E735C`
- Trace SHA-256: `378EB32E3CD9BA3928ABC0AC8E5BB331CD5D2918D32CCF1EEABA1039BEA98636`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110308-sort-s900000-w0-r126ea60b/replay.json`

## Metrics

- earlySpilledCursorDisposals: `8`
- boundedSortTempBytes: `2457600`
- topNChecks: `6196`
- longKeyDocuments: `43372`
- documents: `1920`
- collation: `/Ordinal`
- observedSortSpills: `28`
