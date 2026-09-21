# LiteDB fuzz result: parser

- Status: **PASS**
- Seed: `1109458`
- Steps: `6480` / requested `100`
- Duration: `40.034s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `F35B28CC628287AEBBB0378BB00F49FAD83E6992F436C0618A9FA4378D87DBC6`
- Trace SHA-256: `3BF6DF343E490F05D8278D557ECA20BD38925B879AFBC2FA1218C02859B08D86`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110144-parser-s1109458-w0-r703be367/replay.json`

## Metrics

- sqlDifferentialChecks: `6480`
- distinctStatements: `4041`
