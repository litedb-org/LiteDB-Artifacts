# Issue 3022 validation

Source: [cc238af66](https://github.com/JKamsker/LiteDB/tree/cc238af66a196b18f210a2296b2198809d614a19), [PR 3025](https://github.com/litedb-org/LiteDB/pull/3025). Tested on Linux x64, SDK 10.0.400, Release, `TestingEnabled=true`, .NET 8 and .NET 10 targets, on 2026-09-27. Both targeted suites passed 222 tests with one pre-existing skipped test (`Rebuild_Change_Culture_Error`).

The corpus is pinned separately at [7896d47](https://github.com/litedb-org/LiteDB-Artifacts/tree/7896d47c1261f19260ace114b4aed772a1f9c783/compatibility/released). All 56 fixtures passed current-reader verification. Their own released writers also verified complete scans and every expected payload after reopen before publication.

| Invariant / risk | Discriminating evidence | Result |
|---|---|---|
| Damage must not be reported as a runtime collation difference | `Issue3022LegacyDamage_Tests`: bad adjacent keys, unique duplicates, Guid inversions, known signed ObjectId positive control | Pass |
| Real collation mismatch must not trigger salvage | `Issue2812CollationStamp_Tests`, including AutoRebuild=true | Pass; code 141, bytes unchanged |
| Rejected opening inspection must preserve data and WAL | `Issue3022LegacyDamage_Tests`, `Issue3022WalRecovery_Tests`, ordinary and read-only opens | Pass |
| New recovery entry point must retain backup evidence, original readable data and committed writes | First-open salvage, unrelated collection payloads, exact data/WAL backups, two reopens | Pass |
| Salvage must report unreadable records rather than conceal loss | Malformed BSON fixture and original public #1603 attachment | Pass; original attachment yields 6,824 directory records and six diagnostics |
| Failed replacement must remain retryable without losing the original pair | Two repeated injected failures at each of seven installation phases, followed by successful salvage | Pass |
| Interrupted promotion must not reinterpret early LIMIT_SIZE bytes as a fingerprint | Pending v10/v11 headers with old bytes, read-only inspection, writable retry and repeated reopen | Pass |
| Shared admission must still prevent replacement with active leases | Denied `AutoRebuildAllowed` callback | Pass; source unchanged |
| Healthy released files must retain documents and usable indexes after new writes | Every stable v5 and v4 package, plain/encrypted, complete payload oracle, seeks versus scans, sorted traversal, rollback and reopen | All 56 pass |
| Existing format, index, encryption and recovery boundaries must remain intact | `test-index-compatibility.py`, `test-vector-compatibility.py`, `test-compact-compatibility.py` | Pass |
| Persistent transitions must survive process death and partial I/O | `test-index-migration-recovery.py`, promotion/WAL/commit/checkpoint, plain/encrypted | All 16 cases pass |

Targeted suite command (repeat with net10.0):

```sh
dotnet test LiteDB.Tests -c Release -f net8.0 -p:TestingEnabled=true --settings tests.runsettings --filter 'FullyQualifiedName~Issue3022|FullyQualifiedName~Issue2812|FullyQualifiedName~Issue2417|FullyQualifiedName~IndexMigration|FullyQualifiedName~Rebuild'
```

The untouched baseline `11e9ffacc` was compiled in an isolated checkout and tested against the original attachment. Direct/read-only opens, each with AutoRebuild false/true, all rejected with code 0. The fixed runner checks the complete known salvage result and unchanged backup, then verifies a new record after reopen. The deployment attachment remains at its original public issue URL and is not stored here.

Limits: local results establish Linux behavior under the injected failures, separate-process interruption, and existing simulated power-loss models. They do not prove that arbitrary hardware honors durable flushes. Linux/ICU and Windows/NLS hosted corpus jobs are configured on PR 3025 but were still queued when this report was written; these local results are not hosted-CI evidence. Ambiguous culture/container ordering is deliberately not sufficient to authorize salvage.
