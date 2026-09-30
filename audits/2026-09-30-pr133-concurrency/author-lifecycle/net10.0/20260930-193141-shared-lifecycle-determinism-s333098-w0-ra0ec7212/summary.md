# LiteDB fuzz result: shared-lifecycle

- Status: **PASS**
- Seed: `333098`
- Steps: `20` / requested `20`
- Duration: `27.504s`
- Git SHA: `cdf4a6d8989d11096f4e6869b0db54ba95d6ec66`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 10.0.11`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `378AB2CABD31176C8910783D5A296BF4A5EFFB07B73BCD890EECA2D3EE2C43D6`
- Trace SHA-256: `C6BA2B7F5E405C6BEE0494D34CF721C96A8EAEDA304035B66DAFAA6FB9DFBC8A`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net10.0 -- --replay __WORKSPACE__/artifacts_temp/audit-chaos/artifacts_temp/lifecycle-flush-oracle-net10.0/20260930-193141-shared-lifecycle-determinism-s333098-w0-ra0ec7212/replay.json`

## Metrics

- semanticIndexKeys: `248`
- semanticDocuments: `134`
- semanticIndexes: `60`
- integrityPages: `8`
- integrityPhysicalPages: `8`
- novelStates: `20`
- observedBoundaries: `10`
- coldReopens: `40`
