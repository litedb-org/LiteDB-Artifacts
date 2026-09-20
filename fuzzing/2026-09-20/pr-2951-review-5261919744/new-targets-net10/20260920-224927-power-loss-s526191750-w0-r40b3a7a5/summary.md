# LiteDB fuzz result: power-loss

- Status: **PASS**
- Seed: `526191750`
- Steps: `14` / requested `14`
- Duration: `0.315s`
- Git SHA: `d9d10cc37f7ea113772640a7cd0cacd1544fbc5a`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 10.0.11`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net10.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-5261919744/new-targets-net10/20260920-224927-power-loss-s526191750-w0-r40b3a7a5/replay.json`

## Metrics

- semanticIndexKeys: `64`
- semanticDocuments: `32`
- semanticIndexes: `28`
- integrityPages: `8`
- integrityPhysicalPages: `8`
- novelStates: `14`
- powerCuts: `14`
- durablePhases: `14`
