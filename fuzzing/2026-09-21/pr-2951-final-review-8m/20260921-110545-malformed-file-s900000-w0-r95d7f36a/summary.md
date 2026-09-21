# LiteDB fuzz result: malformed-file

- Status: **PASS**
- Seed: `900000`
- Steps: `2404` / requested `100`
- Duration: `40.020s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `F959BC951B4BC82E89817661779EE84A2DA62A1EBE8D5C42A2B4176398C76883`
- Trace SHA-256: `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110545-malformed-file-s900000-w0-r95d7f36a/replay.json`

## Metrics

- novelStates: `12`
- semanticIndexKeys: `224160`
- semanticDocuments: `112080`
- semanticIndexes: `2802`
- integrityPages: `19`
- integrityPhysicalPages: `19`
- malformedFileRejections: `1003`
