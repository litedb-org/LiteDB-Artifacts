# LiteDB fuzz result: shared-lifecycle

- Status: **PASS**
- Seed: `233098`
- Steps: `32` / requested `32`
- Duration: `61.449s`
- Git SHA: `f9487f81421a378cd50e8cae9b6112b5fd3be291`
- Working tree dirty: `True`
- Environment: `Microsoft Windows 10.0.26100`, `.NET 10.0.12`, `X64`
- Culture/timezone: `en-US` / `UTC`
- Failure ID: `n/a`
- Input SHA-256: `F948D93E03A9D6121DEAA470BEF11EED0F4BDDF14856A42A1535065949071E94`
- Trace SHA-256: `40C36799134529A0E6439C5FF26A8886E3C600853A6BA36E4305CD2A0081F642`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net10.0 -- --replay __CI_WORKSPACE__\artifacts\concurrency\20260930-195549-shared-lifecycle-s233098-w0-r9911b298\replay.json`

## Metrics

- semanticIndexKeys: `396`
- semanticDocuments: `214`
- semanticIndexes: `96`
- integrityPages: `8`
- integrityPhysicalPages: `8`
- novelStates: `26`
- observedBoundaries: `10`
- coldReopens: `64`
