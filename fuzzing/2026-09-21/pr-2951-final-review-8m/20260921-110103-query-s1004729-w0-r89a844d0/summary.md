# LiteDB fuzz result: query

- Status: **PASS**
- Seed: `1004729`
- Steps: `4531` / requested `100`
- Duration: `40.030s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `02E862C8B8F604D5A4053F9D4A0713F1459F9D271F17760F381DB432AEFA2404`
- Trace SHA-256: `672B0F4A13C6E0AC3DFC7DF60AF38043984A313AE0129B9A53C8F1324B6C6172`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110103-query-s1004729-w0-r89a844d0/replay.json`

## Metrics

- novelStates: `2135`
- assertions: `58903`
- operationHits: `System.Collections.Generic.Dictionary`2[System.String,System.Int32]`
- collation: `en-US/IgnoreCase`
