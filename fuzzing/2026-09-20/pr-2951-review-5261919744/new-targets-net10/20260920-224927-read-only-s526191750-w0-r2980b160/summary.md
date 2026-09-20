# LiteDB fuzz result: read-only

- Status: **PASS**
- Seed: `526191750`
- Steps: `14` / requested `14`
- Duration: `1.241s`
- Git SHA: `d9d10cc37f7ea113772640a7cd0cacd1544fbc5a`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 10.0.11`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net10.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-5261919744/new-targets-net10/20260920-224927-read-only-s526191750-w0-r2980b160/replay.json`

## Metrics

- novelStates: `10`
- semanticIndexKeys: `160`
- semanticDocuments: `80`
- semanticIndexes: `2`
- integrityPages: `5`
- integrityPhysicalPages: `5`
- byteStableReadWorkloads: `14`
