# LiteDB fuzz result: shared-lifecycle

- Status: **PASS**
- Seed: `133098`
- Steps: `32` / requested `32`
- Duration: `40.878s`
- Git SHA: `e9a3b72063d8b717c55391829aad996a412cdf7b`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.5 LTS`, `.NET 8.0.31`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `C4B767656AFE130CFE8E5278D658A6E461AD9E74615D93685423B4C481B7B3E8`
- Trace SHA-256: `02F3FC541C2D1F500F7F7BE7147406474D3F6BEBC77E68ED4F114D06BBF151AB`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay __CI_WORKSPACE__/artifacts/concurrency/20261001-021128-shared-lifecycle-s133098-w0-rf3c1609e/replay.json`

## Metrics

- semanticIndexKeys: `396`
- semanticDocuments: `214`
- semanticIndexes: `96`
- integrityPages: `8`
- integrityPhysicalPages: `8`
- novelStates: `23`
- observedBoundaries: `10`
- coldReopens: `64`
