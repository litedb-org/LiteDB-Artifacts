# LiteDB fuzz result: query

- Status: **PASS**
- Seed: `900000`
- Steps: `4442` / requested `100`
- Duration: `40.027s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `68B8A81777243A89CF58F9C92DD92956951D44017F3E69FDCEF8B4D25BEB7182`
- Trace SHA-256: `663B273098A75696463AFBD9EF872B4C5275E218B0CD8F6C08FB0E0F07D388CC`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110022-query-s900000-w0-r4e880fc3/replay.json`

## Metrics

- novelStates: `2119`
- assertions: `57746`
- operationHits: `System.Collections.Generic.Dictionary`2[System.String,System.Int32]`
- collation: `en-US/None`
