# LiteDB fuzz result: malformed-file

- Status: **PASS**
- Seed: `1004729`
- Steps: `2420` / requested `100`
- Duration: `40.024s`
- Git SHA: `0f244643f2fcf1ca8b318b0137ee64527cd18722`
- Working tree dirty: `True`
- Environment: `Ubuntu 24.04.3 LTS`, `.NET 8.0.30`, `X64`
- Culture/timezone: `` / `Etc/UTC`
- Failure ID: `n/a`
- Input SHA-256: `4F089C2132A884EE7C54D03EA3F2D1A7FCF7CEE7D879FA5874A83BDBC6F844E5`
- Trace SHA-256: `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net8.0 -- --replay /home/jonas/.codex/worktrees/3c13/LiteDB/artifacts_temp/review-final-campaign/20260921-110626-malformed-file-s1004729-w0-r73b2349e/replay.json`

## Metrics

- novelStates: `12`
- semanticIndexKeys: `225760`
- semanticDocuments: `112880`
- semanticIndexes: `2822`
- integrityPages: `19`
- integrityPhysicalPages: `19`
- malformedFileRejections: `1009`
