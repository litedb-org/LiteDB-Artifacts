# LiteDB fuzz result: snapshot

- Status: **PASS**
- Seed: `526191750`
- Steps: `14` / requested `14`
- Duration: `6.920s`
- Git SHA: `d9d10cc37f7ea113772640a7cd0cacd1544fbc5a`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 10.0.11`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net10.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-5261919744/new-targets-net10/20260920-224927-snapshot-s526191750-w0-rf2e0b506/replay.json`

## Metrics

- novelStates: `8`
- semanticIndexKeys: `52`
- semanticDocuments: `26`
- semanticIndexes: `2`
- integrityPages: `37`
- integrityPhysicalPages: `37`
- snapshotValidations: `28`
- checkpointsBetweenReaderGenerations: `14`
