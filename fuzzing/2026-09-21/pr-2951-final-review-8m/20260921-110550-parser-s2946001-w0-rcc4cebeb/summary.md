# LiteDB fuzz result: parser

- Status: **PASS**
- Seed: `2946001`
- Steps: `100` / requested `100`
- Duration: `1.274s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `176E95080BA300FAD181976C3DA421B325B3E30B9B1681C5E28C8F2D43B0ABA8`
- Trace SHA-256: `3A15F2F77EEC5B3DFD783134BE187009EAC61377C07EE6403DC4CDDA8B46FB41`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110550-parser-s2946001-w0-rcc4cebeb/replay.json`

## Metrics

- sqlDifferentialChecks: `100`
- distinctStatements: `74`
