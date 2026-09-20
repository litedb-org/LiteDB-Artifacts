# LiteDB fuzz result: vector

- Status: **PASS**
- Seed: `526191752`
- Steps: `30` / requested `30`
- Duration: `0.640s`
- Git SHA: `d9d10cc37f7ea113772640a7cd0cacd1544fbc5a`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-5261919744/persistence/20260920-224927-vector-s526191752-w0-r4d48a284/replay.json`

## Metrics

- novelStates: `10`
- semanticIndexKeys: `13`
- semanticDocuments: `13`
- semanticIndexes: `1`
- integrityPages: `5`
- integrityPhysicalPages: `5`
- documents: `13`
- dimensions: `8`
- metric: `DotProduct`
- searches: `30`
- integrityChecks: `1`
