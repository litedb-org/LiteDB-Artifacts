# LiteDB fuzz result: malformed-file

- Status: **PASS**
- Seed: `1214187`
- Steps: `2184` / requested `100`
- Duration: `36.117s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `9892D7B1B0BE90DCBB485E1749EF6500C23C31481996662EDB7E6320C58D866B`
- Trace SHA-256: `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110749-malformed-file-s1214187-w0-re90706ca/replay.json`

## Metrics

- novelStates: `12`
- semanticIndexKeys: `203840`
- semanticDocuments: `101920`
- semanticIndexes: `2548`
- integrityPages: `19`
- integrityPhysicalPages: `19`
- malformedFileRejections: `910`
