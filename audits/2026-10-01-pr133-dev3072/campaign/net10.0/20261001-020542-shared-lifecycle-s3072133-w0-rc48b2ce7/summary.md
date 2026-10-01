# LiteDB fuzz result: shared-lifecycle

- Status: **PASS**
- Seed: `3072133`
- Steps: `20` / requested `20`
- Duration: `26.030s`
- Git SHA: `7f2b2bf166f7f8236f02ca1b5826fca1de656be7`
- Working tree dirty: `False`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 10.0.11`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `ECDF452B70697F90A4CDF38522EE17017BCBC9933E233F8D5087283F8EA40065`
- Trace SHA-256: `F5454489D5D161AD9FCB169C22CD303210DE1F5F0B33B4D374AC26192A3047B6`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net10.0 -- --replay __WORKSPACE__/artifacts_temp/dev3072-campaign/artifacts_temp/campaign/net10.0/20261001-020542-shared-lifecycle-s3072133-w0-rc48b2ce7/replay.json`

## Metrics

- semanticIndexKeys: `248`
- semanticDocuments: `134`
- semanticIndexes: `60`
- integrityPages: `8`
- integrityPhysicalPages: `8`
- novelStates: `20`
- observedBoundaries: `10`
- coldReopens: `40`
