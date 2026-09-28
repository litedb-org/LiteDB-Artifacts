## Safety / regression evidence

Following #3034 §1. Evidence below was produced on `HEAD_SHA` (this branch merged with `dev` at `DEV_SHA`); the tests are the evidence. The agent reviews listed under Validation proposed counterexamples; they are not evidence or approval.

**Contracts and risk.**
- *Stays supported:* 5.0.21 files and WALs open and convert (read directly, migrate automatically, or refuse unchanged, per the table below); commits acknowledged durable survive a power loss within the fault model below; caller streams, shared mode and encrypted files keep their 5.0.21 semantics except where listed under "Behaviour changes worth reviewing".
- *Intentionally changes:* the engine stops after a WAL write that may have torn a frame; rollback-only transactions after a failed safepoint write; conversion and rebuild refused where the data file cannot sync; the WAL kept (and growing) until the data file syncs; `AutoRebuild` in the same open; non-writable streams open read-only; one flush per page write on caller streams other than `MemoryStream`.
- *Why:* each change replaces a path that lost acknowledged data or misread a file (see Fixes).

**Prior states and interactions.** Real 5.0.21 files (clean, dirty WAL, encrypted, crash images, a concurrent-writer crash, damaged documents, stale `DropIndex` bytes), a 6.0.0-prerelease.114 vector file, a WAL of another database, files converted by an earlier engine whose header never synced, storage that answers "cannot sync" (#2242) for either file, caller `FileStream`/`BufferedStream`/`MemoryStream`/custom streams, shared mode with other connections and leases, and a restart between engines.

**Failure outcomes.**

| Boundary | Allowed outcome |
|---|---|
| WAL write fails part-way | The failed commit is not acknowledged; the engine stops before any later commit appends; recovery keeps every acknowledged commit |
| Safepoint write fails | The transaction can only roll back; nothing of it is published |
| Data file answers "cannot sync" | Commits reported non-durable; the WAL is kept until a data sync succeeds; checkpoints write nothing meanwhile; conversion and rebuild refused unchanged |
| Legacy WAL cannot be drained (leases, unreadable registry) | Conversion refused (`LOCK_TIMEOUT`), both files byte-identical |
| Legacy WAL page that is not this database's | Open refused (`INVALID_DATABASE`), both files byte-identical |
| Damaged 5.0.21 document | Error names the collection; `AutoRebuild` salvages the readable part and reports it in `_rebuild_errors` |
| Power loss (each file as of its last successful sync, or the WAL written back as it is) | Every commit acknowledged durable is present after reopen; unacknowledged ones may or may not be |

**Evidence** (one row per risk; all on Linux x64, .NET 10 and .NET 8, `HEAD_SHA`; hosted CI adds Windows .NET 10 and net481):

EVIDENCE_ROWS

**Coverage delta** (changes to tests, corpora and CI inputs that existed on `dev`, since the owner's gate 8 ledger of `5e85a8b50`):

| Item | Old invariant | Now | Disposition |
|---|---|---|---|
| `LiteDB.Fuzz/Corpus/regressions.json` (`ad2625283`) | Checksum-crash seeds 4058733 and 3163459 replay with pinned input/trace hashes | Re-pinned: `5a748e6a9` removed reader syncs from the encrypted conversion's I/O trace; with that change reverted the old hashes match again; the new hashes match on Linux and Windows; old ones kept under `previousHashes` | Same invariant, new trace |
| `Issue2818_FlushFallback_Tests` disk-full row (`2b3ba4b90`) | No plain flush papers over a failed durable flush (counted as "no plain flush after the failure started") | Counts plain flushes after the durable flush failed: `c4119caba` flushes each frame write before the durable flush | Same invariant, measured precisely |
| PR-internal tests replaced in this round | "WAL stays bounded where only the data file cannot sync"; "neither file syncs empties the WAL"; "conversion/rebuild proceed where the data file cannot sync"; "a new WAL's directory entry waits for the data file" | WAL kept and reported; conversion and rebuild refused unchanged; the directory scenario cannot arise (no WAL is emptied without a data sync) and became the restart test | Intended behaviour change (strict WAL rule) |

No test was skipped, disabled or given a longer timeout; one new conditional skip (`SharedMutexNameLength` on Windows, `UnixFact`) runs on the Linux legs (UNIX_EVIDENCE).

**Strongest remaining counterexample.** STRONGEST
