# LiteDB fuzz result: sql-dml

- Status: **PASS**
- Seed: `900000`
- Steps: `123759` / requested `100`
- Duration: `40.025s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `CF1143A8B665BF5282A85275E1BF3C4196B0300508B36E63A9C26633E4CD7505`
- Trace SHA-256: `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110311-sql-dml-s900000-w0-r7699085e/replay.json`

## Metrics

- novelStates: `51`
- sqlDmlMutations: `43810`
