# LiteDB fuzz result: integrity

- Status: **PASS**
- Seed: `526177209`
- Steps: `1` / requested `1`
- Duration: `0.875s`
- Git SHA: `434c01a6455daa21eb891deed14bbf1b09457f9a`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-5261772309/integrity-net8/20260920-214453-integrity-s526177209-w0-r69a96aac/replay.json`

## Metrics

- semanticIndexKeys: `81`
- semanticDocuments: `35`
- semanticIndexes: `9`
- integrityPages: `4`
- integrityPhysicalPages: `4`
- mutantsDetected: `10`
- preallocatedLayouts: `1`
- encryptedLayouts: `1`
- legacyFiles: `1`
