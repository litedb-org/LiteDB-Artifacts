# LiteDB fuzz result: shared

- Status: **PASS**
- Seed: `526177211`
- Steps: `31` / requested `100`
- Duration: `55.532s`
- Git SHA: `434c01a6455daa21eb891deed14bbf1b09457f9a`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-5261772309/shared-refactored/20260920-214757-shared-s526177211-w0-r68c77ef2/replay.json`

## Metrics

- semanticIndexKeys: `2376`
- semanticDocuments: `2376`
- semanticIndexes: `31`
- integrityPages: `4`
- integrityPhysicalPages: `4`
- rounds: `31`
- processes: `92`
- acknowledgedRows: `2345`
- crashPositions: `26`
- internalCrashPoints: `14`
- integrityChecks: `31`
