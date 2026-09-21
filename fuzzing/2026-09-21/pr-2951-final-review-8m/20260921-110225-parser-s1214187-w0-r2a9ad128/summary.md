# LiteDB fuzz result: parser

- Status: **PASS**
- Seed: `1214187`
- Steps: `6149` / requested `100`
- Duration: `37.244s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `CE351091706312C825263A8942B62D031E90560C0744B63856F997DDB33D3BDD`
- Trace SHA-256: `36DD68BE37B5EF66DE56D24F944B7CB8C30663499B6C5C35C25BCC0883A49E9F`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110225-parser-s1214187-w0-r2a9ad128/replay.json`

## Metrics

- sqlDifferentialChecks: `6149`
- distinctStatements: `3885`
