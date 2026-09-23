# Storage stack safety evidence for LiteDB #3000

Retained audit evidence for the final storage-stack candidates, published on a dedicated evidence branch in LiteDB-Artifacts. This folder contains clearly labeled derived reports, byte-unchanged synthetic random tapes and replay metadata, plus the original synthetic upgrade-probe source with portable derived project files. Original local/hosted evidence remains separate and unmodified.

## Status snapshot and scope

Final hosted observation: **2026-09-23T00:18:01.254569+00:00**. All four final heads passed all 47 platform jobs and 12 applicable fuzz jobs each; the index, compact and MVCC layers also passed all four compatibility jobs each. Two scheduled-only fuzz jobs per PR are intentionally skipped. Exact revisions, job results and links are in [reports/hosted-final.json](reports/hosted-final.json). This records successful validation, not a PR merge; #3000 remains draft.

Historical frozen hosted observation: 2026-09-23T00:09:07.592307+00:00. Checksum #2998 and index #2924 are fully green. Compact #2999 and MVCC #3000 still have pending platform validation in this snapshot; their Fuzz/compatibility checks are green. No claim that all final checks passed, no PR merge, and no change to #3000 draft state is implied.

| Layer | Exact candidate commit |
|---|---|
| Checksum #2998 | [81165d83527a15773ec3c9bb1e50355c1606abfb](https://github.com/litedb-org/LiteDB/commit/81165d83527a15773ec3c9bb1e50355c1606abfb) |
| Index #2924 | [95ba846e9a5fe7abae1cbe0b003bc78f0c8fca57](https://github.com/litedb-org/LiteDB/commit/95ba846e9a5fe7abae1cbe0b003bc78f0c8fca57) |
| Compact #2999 | [6a10ec48bb6c28fbef0262f966a1547f65108d66](https://github.com/litedb-org/LiteDB/commit/6a10ec48bb6c28fbef0262f966a1547f65108d66) |
| MVCC #3000 | [3ead160ccf0a529a58989324cb98d25cb87f9c64](https://github.com/litedb-org/LiteDB/commit/3ead160ccf0a529a58989324cb98d25cb87f9c64) |

The integrated production source was validated at b8c4a336e823154e7ed7a4f1dce0de591cf5f8c4; subsequent changes discussed here are tests/docs. Full Linux x64 suites at that production/test source passed 4,121 cases per .NET 8.0.30/.NET 10.0.11, with seven existing skips; those counts predate later test additions. Current code and permanent acceptance tests: [storage-stack-safety.md](https://github.com/litedb-org/LiteDB/blob/3ead160ccf0a529a58989324cb98d25cb87f9c64/docs/storage-stack-safety.md). Reproduce those source-level tests and compatibility scripts at the linked commits using Release with TestingEnabled=true; local and hosted results retain their respective exact revision and configuration scopes.

## Contents and provenance

- reports/fuzz-summary-derived.json: 168 successful child runs / 269,046 modeled steps, including the 20-minute-budget storage campaign; four anticipated initial corpus-drift failures remain disclosed. Linux x64 only, hooks enabled, finite models, database work in RAM.
- raw/fuzz: 89 recorded duration epochs and two pinned checksum-crash records. Input.bin is a generated random-word tape, not an executable or database. All 91 tapes were verified against seeded xorshift32 and original input/trace metadata; published bytes are unchanged. Full manifest and replay instructions are included.
- reports/upgrade-cost-derived.md and upgrade-harness: actual unmodified 5.0.21 writer to production 7fca9be95, plain/encrypted 10k/50k rows, full document/index checks and unchanged original document pages, repeated opens with zero writes. RAM-backed timing and FileStream API bytes are not device latency, wear or physical write-amplification measurements.
- reports/query-assertion-cost-derived.md and diagnostics-derived.md: measured harness-cost correction, captured recovery/liveness evidence and unfavorable historical results with limits.
- reports/hosted-status-derived.json: exact run links and statuses at the frozen observation; reports/source-provenance.json: hashes of source reports/archive and precise assembly metadata without publishing binaries.

The original fuzz archive is 32,716,216 bytes, SHA-256 8850cdf8c52b40b57f3248d0ddb59b89c2cdc40d04bb7fa81b535846812f2471. This public subset is intentionally smaller and is not a replacement for the complete original archive. Reproduction commands are in raw/fuzz/README.md and upgrade-harness/README.md; exact linked source commits remain the authority.

## Exclusions and interpretation

Omitted: every process dump, DLL/executable/PDB, environment dump, raw CI/local log, raw TRX, database/WAL snapshot, full source archive and intermediate/minimization artifact. Original machine-specific paths were removed only from labeled derived reports/projects; no raw file was redacted or silently changed. Raw files that matched a machine-path/token marker would be omitted as a whole. None of the selected replay records required omission. Redundant input-offset indexes and traces were omitted to keep the public subset bounded; expected trace hashes remain in the original replay descriptors. Not every successful child run is included as raw data.

The one-off upgrade harness uses the fixed public synthetic cost-fixture password and entirely generated rows. No real deployment database, personal data or credential is included. Screening combined explicit source allowlisting, text/path/token checks, and full seeded-word verification; this is a preparation audit, not a claim that regex scanning can certify arbitrary files.

Durability remains subject to the documented sync/power-safe-overwrite model. Lazy checksums do not retroactively protect untouched legacy pages. Index migration may repeat uncommitted scans/writes after interruption; committed migration becomes a no-op. Backup requires excluding writers/checkpoints through preparation and capture. The permanent source documentation explains these contracts. Finite tests are not absolute proof.
