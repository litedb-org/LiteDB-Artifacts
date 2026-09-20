# LiteDB fuzz result: boundary

- Status: **PASS**
- Seed: `526191750`
- Steps: `1` / requested `14`
- Duration: `3.932s`
- Git SHA: `d9d10cc37f7ea113772640a7cd0cacd1544fbc5a`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 10.0.11`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net10.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-5261919744/new-targets-net10/20260920-224927-boundary-s526191750-w0-r12eb41e7/replay.json`

## Metrics

- semanticIndexKeys: `295`
- semanticDocuments: `39`
- semanticIndexes: `275`
- integrityPages: `4`
- integrityPhysicalPages: `4`
- novelStates: `1`
- explicitBoundaries: `7`
