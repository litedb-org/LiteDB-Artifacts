# LiteDB fuzz result: sort

- Status: **PASS**
- Seed: `1004729`
- Steps: `3014` / requested `100`
- Duration: `40.033s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `B18FC4BC144D37EF20DFF318416502AD14F6383DAF047C2A050C8A9043AE2425`
- Trace SHA-256: `6CA437F5419AD6EF1882A2C172CD925383F273DB84B3EB782721D4447F164B75`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110350-sort-s1004729-w0-rcdc01b3e/replay.json`

## Metrics

- earlySpilledCursorDisposals: `8`
- boundedSortTempBytes: `2457600`
- topNChecks: `6028`
- longKeyDocuments: `42196`
- documents: `560`
- collation: `en-US/IgnoreCase`
- observedSortSpills: `28`
