# LiteDB fuzz result: storage

- Status: **PASS**
- Seed: `700001`
- Steps: `12` / requested `12`
- Duration: `1.175s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `3492AE7C572BA50EB13CFAE14F417927ED37B2C6070126F87738C2C1B95554AF`
- Trace SHA-256: `7ADD62E2D345D94161FEF38310D1C16F00DF8DCCBA0BF2AD6EF38DC9D9BA57ED`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110552-storage-s700001-w0-r23f94ae5/replay.json`

## Metrics

- semanticIndexKeys: `43`
- semanticDocuments: `43`
- semanticIndexes: `2`
- integrityPages: `116`
- integrityPhysicalPages: `116`
- files: `3`
- bytes: `862848`
- reopens: `0`
- interruptedTransactions: `1`
- atomicUploadProbes: `0`
- injectedUploadFailures: `0`
- integrityChecks: `1`
