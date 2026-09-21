# LiteDB fuzz result: parser

- Status: **PASS**
- Seed: `1004729`
- Steps: `6835` / requested `100`
- Duration: `40.039s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `4E805FB5AB4E9947655043AD87E485D4B755505C75EAF5AB52913337D6A937E4`
- Trace SHA-256: `BA69BE53C1559D190A0C02513276FBD2AEDAE2789A716CDDAA68656BD24B767E`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110103-parser-s1004729-w0-rb67ceb36/replay.json`

## Metrics

- sqlDifferentialChecks: `6835`
- distinctStatements: `4284`
