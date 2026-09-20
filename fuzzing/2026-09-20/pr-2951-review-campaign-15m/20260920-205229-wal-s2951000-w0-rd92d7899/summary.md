# LiteDB fuzz result: wal

- Status: **PASS**
- Seed: `2951000`
- Steps: `88664` / requested `100`
- Duration: `900.002s`
- Git SHA: `2f36856b46745e69bf39208299ad1ee4aaa3a979`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/fuzz-pr2951-review-20260920/20260920-205229-wal-s2951000-w0-rd92d7899/replay.json`

## Metrics

- integrityPages: `14`
- injectedFailures: `49119`
- acknowledgedCommits: `236943`
- failureEvents: `System.Collections.Generic.Dictionary`2[System.String,System.Int32]`
- integrityChecks: `2860`
