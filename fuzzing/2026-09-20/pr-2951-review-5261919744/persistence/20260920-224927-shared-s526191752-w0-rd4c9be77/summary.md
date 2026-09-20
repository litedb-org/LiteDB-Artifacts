# LiteDB fuzz result: shared

- Status: **PASS**
- Seed: `526191752`
- Steps: `1` / requested `30`
- Duration: `1.525s`
- Git SHA: `d9d10cc37f7ea113772640a7cd0cacd1544fbc5a`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-5261919744/persistence/20260920-224927-shared-s526191752-w0-rd4c9be77/replay.json`

## Metrics

- novelStates: `1`
- semanticIndexKeys: `15`
- semanticDocuments: `15`
- semanticIndexes: `1`
- integrityPages: `4`
- integrityPhysicalPages: `4`
- rounds: `1`
- processes: `2`
- acknowledgedRows: `14`
- crashPositions: `1`
- internalCrashPoints: `0`
- integrityChecks: `1`
