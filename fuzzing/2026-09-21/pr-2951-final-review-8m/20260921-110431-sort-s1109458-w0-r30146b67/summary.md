# LiteDB fuzz result: sort

- Status: **PASS**
- Seed: `1109458`
- Steps: `3174` / requested `100`
- Duration: `40.032s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `655344F1A1D14879016F35E9AE68E4F7F9E5A9EA461DA4191CC584F21B7191C2`
- Trace SHA-256: `9542C64ED5A1FE657C2F6D1A8AB18864937F43EF9AF02465354EC614E0745796`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110431-sort-s1109458-w0-r30146b67/replay.json`

## Metrics

- earlySpilledCursorDisposals: `8`
- boundedSortTempBytes: `2457600`
- topNChecks: `6348`
- longKeyDocuments: `44436`
- documents: `960`
- collation: `/Ordinal`
- observedSortSpills: `28`
