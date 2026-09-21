# LiteDB fuzz result: query

- Status: **PASS**
- Seed: `1109458`
- Steps: `4405` / requested `100`
- Duration: `40.033s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `056A40486DED2839827A43054E2C68C668D6104FB72C6BC2EB0567CCC508A9C4`
- Trace SHA-256: `3EDED9C4FC0393249F3C81CEF14995E7EA0AC4E07B1D6EFB4134A62FB183653D`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110144-query-s1109458-w0-r0b741d0c/replay.json`

## Metrics

- novelStates: `2193`
- assertions: `57265`
- operationHits: `System.Collections.Generic.Dictionary`2[System.String,System.Int32]`
- collation: `tr-TR/IgnoreCase`
