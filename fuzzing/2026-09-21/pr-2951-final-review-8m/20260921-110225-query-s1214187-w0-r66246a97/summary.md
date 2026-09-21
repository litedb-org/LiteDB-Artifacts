# LiteDB fuzz result: query

- Status: **PASS**
- Seed: `1214187`
- Steps: `4117` / requested `100`
- Duration: `37.255s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `63C9BA626BEE53A5C3A4756C80C5D6DE2AFAC8BF58A05529DE5D270C84DF090B`
- Trace SHA-256: `005C613A112448474B4CCFC3A8A64DBE945D128894A0181B6E56A0F5AB9D68B7`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110225-query-s1214187-w0-r66246a97/replay.json`

## Metrics

- novelStates: `2064`
- assertions: `53521`
- operationHits: `System.Collections.Generic.Dictionary`2[System.String,System.Int32]`
- collation: `en-US/IgnoreNonSpace`
