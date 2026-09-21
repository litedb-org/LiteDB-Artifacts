# LiteDB fuzz result: storage

- Status: **PASS**
- Seed: `1214187`
- Steps: `1023` / requested `100`
- Duration: `33.660s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `D3C178C53A22595C65A9B42F6C2EFBADE39F0003FD0D5DB6AA6E97C030FEA276`
- Trace SHA-256: `D294AAE69EBBF8848700789D0425AEDDA887FECD2240A84BE60264DC62560B5F`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110509-storage-s1214187-w0-r2bb06171/replay.json`

## Metrics

- semanticIndexKeys: `1188`
- semanticDocuments: `1188`
- semanticIndexes: `64`
- integrityPages: `475`
- integrityPhysicalPages: `475`
- files: `12`
- bytes: `3214359`
- reopens: `124`
- interruptedTransactions: `108`
- atomicUploadProbes: `60`
- injectedUploadFailures: `60`
- integrityChecks: `32`
