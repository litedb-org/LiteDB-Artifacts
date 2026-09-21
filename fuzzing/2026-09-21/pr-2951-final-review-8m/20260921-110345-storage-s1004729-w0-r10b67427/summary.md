# LiteDB fuzz result: storage

- Status: **PASS**
- Seed: `1004729`
- Steps: `1075` / requested `100`
- Duration: `40.057s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `8EC66EFE10A3F45E28D8E8D713238712A3CACAF33DF5B4B783A4CFEBF436BDDD`
- Trace SHA-256: `052481EB35124133B8D599F34A97DD8439FB3D1CDC3311B5D9F8A401BD6D6E01`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110345-storage-s1004729-w0-r10b67427/replay.json`

## Metrics

- semanticIndexKeys: `1347`
- semanticDocuments: `1347`
- semanticIndexes: `66`
- integrityPages: `480`
- integrityPhysicalPages: `480`
- files: `11`
- bytes: `2532948`
- reopens: `109`
- interruptedTransactions: `126`
- atomicUploadProbes: `63`
- injectedUploadFailures: `63`
- integrityChecks: `33`
