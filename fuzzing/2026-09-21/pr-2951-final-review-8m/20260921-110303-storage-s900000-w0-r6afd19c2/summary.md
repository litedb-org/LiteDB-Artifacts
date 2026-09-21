# LiteDB fuzz result: storage

- Status: **PASS**
- Seed: `900000`
- Steps: `1225` / requested `100`
- Duration: `40.137s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `6844A3E04919A206C31776985600158E2B189F8C1457E5B41E18E40C13F4D4CC`
- Trace SHA-256: `B8EA2D272CB7F9D48DAF1DDC67F3312FD936E8D8E2852C1C59B496493AC2835B`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110303-storage-s900000-w0-r6afd19c2/replay.json`

## Metrics

- semanticIndexKeys: `1400`
- semanticDocuments: `1400`
- semanticIndexes: `76`
- integrityPages: `510`
- integrityPhysicalPages: `510`
- files: `10`
- bytes: `1519963`
- reopens: `130`
- interruptedTransactions: `143`
- atomicUploadProbes: `72`
- injectedUploadFailures: `72`
- integrityChecks: `38`
