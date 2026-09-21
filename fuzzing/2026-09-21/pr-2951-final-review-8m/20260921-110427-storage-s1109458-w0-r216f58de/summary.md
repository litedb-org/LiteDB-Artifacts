# LiteDB fuzz result: storage

- Status: **PASS**
- Seed: `1109458`
- Steps: `1120` / requested `100`
- Duration: `40.227s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `A11B0D2F8FDA351AB956394340ED1EB898B6B0E49943109C75468177CCC124AC`
- Trace SHA-256: `D27ECCD3893BF6D5A8D946D6518473A5CCE624A04F6B1CC96F6BCE564E52C8D4`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110427-storage-s1109458-w0-r216f58de/replay.json`

## Metrics

- semanticIndexKeys: `1186`
- semanticDocuments: `1186`
- semanticIndexes: `70`
- integrityPages: `443`
- integrityPhysicalPages: `443`
- files: `10`
- bytes: `2308074`
- reopens: `128`
- interruptedTransactions: `128`
- atomicUploadProbes: `65`
- injectedUploadFailures: `65`
- integrityChecks: `35`
