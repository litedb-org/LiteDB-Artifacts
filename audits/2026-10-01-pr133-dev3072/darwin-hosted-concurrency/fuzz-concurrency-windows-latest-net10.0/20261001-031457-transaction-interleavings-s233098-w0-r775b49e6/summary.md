# LiteDB fuzz result: transaction-interleavings

- Status: **PASS**
- Seed: `233098`
- Steps: `272` / requested `272`
- Duration: `71.832s`
- Git SHA: `3103575fc132209b9786050af337c3fc3c3cace1`
- Working tree dirty: `True`
- Environment: `Microsoft Windows 10.0.26100`, `.NET 10.0.12`, `X64`
- Culture/timezone: `en-US` / `UTC`
- Failure ID: `n/a`
- Input SHA-256: `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`
- Trace SHA-256: `D8C5A48216B6EF9F63E82537372C7C5CC5F134E327FA9C2872A8293FB0B6E3FA`
- Replay: `dotnet run --project LiteDB.Fuzz -c Release -f net10.0 -- --replay __CI_WORKSPACE__\artifacts\concurrency\20261001-031457-transaction-interleavings-s233098-w0-r775b49e6\replay.json`

## Metrics

- novelStates: `272`
- completedActorSchedules: `272`
