# LiteDB fuzz result: parser

- Status: **PASS**
- Seed: `900000`
- Steps: `6709` / requested `100`
- Duration: `40.030s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `E5BBE40B4F274B4B89E4717B9118F64A7382745ABD902A30519C8FF3B85B3D76`
- Trace SHA-256: `FC0D379A1888B5B863AD319E14EC0E4FFF4D1E34D0F9006E458BF5054F24AC90`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110022-parser-s900000-w0-r51c0d2d3/replay.json`

## Metrics

- sqlDifferentialChecks: `6709`
- distinctStatements: `4196`
