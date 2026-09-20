# LiteDB fuzz result: shared

- Status: **PASS**
- Seed: `526177210`
- Steps: `33` / requested `100`
- Duration: `60.850s`
- Git SHA: `434c01a6455daa21eb891deed14bbf1b09457f9a`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-5261772309/shared-process/20260920-214507-shared-s526177210-w0-r9d623ae0/replay.json`

## Metrics

- semanticIndexKeys: `2586`
- semanticDocuments: `2586`
- semanticIndexes: `33`
- integrityPages: `4`
- integrityPhysicalPages: `4`
- rounds: `33`
- processes: `99`
- acknowledgedRows: `2553`
- crashPositions: `26`
- internalCrashPoints: `14`
- integrityChecks: `33`
