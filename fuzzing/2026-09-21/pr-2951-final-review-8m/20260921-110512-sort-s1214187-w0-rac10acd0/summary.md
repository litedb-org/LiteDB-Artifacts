# LiteDB fuzz result: sort

- Status: **PASS**
- Seed: `1214187`
- Steps: `2778` / requested `100`
- Duration: `36.709s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `54CB7D33D0165D3F335F6B5379982DCB018D7DDF0825B9C9C73252BE8BAEE7F5`
- Trace SHA-256: `53FFEC200FD7451CCB31B40DD478D547BF29CA7C07BD3C22FD10BE0AAEAD6B83`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110512-sort-s1214187-w0-rac10acd0/replay.json`

## Metrics

- earlySpilledCursorDisposals: `8`
- boundedSortTempBytes: `2457600`
- topNChecks: `5556`
- longKeyDocuments: `38892`
- documents: `1120`
- collation: `en-US/IgnoreCase`
- observedSortSpills: `28`
