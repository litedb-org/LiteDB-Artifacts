# Direct rebuild ownership evidence

Evidence for [LiteDB PR #3025](https://github.com/litedb-org/LiteDB/pull/3025), addressing the ownership gap raised in [review comment 5865449210](https://github.com/litedb-org/LiteDB/pull/3025#issuecomment-5865449210) and [issue #3035](https://github.com/litedb-org/LiteDB/issues/3035).

## Revisions and environment

Collected 2026-09-28 on Ubuntu 24.04.3 LTS, x64, ICU, Release with `TestingEnabled=true`; .NET 8.0.30 and .NET 10.0.11. Before: `19288367f` (the PR before ownership changes). Production fix: `bd95a2ae17cc3c07f605fffd9c9baffb551bca85`. Final source: `ce6d67f98aab31b8a91bafd55e142641f90636a3`; the last commit only adds an opening-admission test hook and two real-opener handoff cases.

Raw logs are retained with only machine-specific checkout/runtime paths replaced by `<checkout>` / `<dotnet>`. No database from a real deployment is included.

## Discriminating reproduction

`Program.cs` pauses rebuild at `before-recovery-marker` and opens a second Direct connection on the same thread, inserts record 99, then lets rebuild install and checks the record after reopening. This is a deterministic two-connection reproduction, not a separate-process reproduction. On the before revision the insert is acknowledged but disappears:

```
REACHED=True ACKNOWLEDGED=True PRESENT_AFTER_REBUILD=False
```

On the fixed revision the competing open receives an IOException before any insert is acknowledged. The production fix was tested before the final test-only commit. This probe exits 2 for acknowledged data loss, 3 if it misses the hook, and 0 otherwise. Permanent tests separately cover real processes, successful writes after ownership handoff, and preservation of the original independent record model.

To reproduce, build each source revision in its own checkout with `dotnet build LiteDB/LiteDB.csproj -c Release -f net8.0 -p:TestingEnabled=true`. Run `dotnet run --project Probe.csproj -c Release -p:Library=/absolute/path/to/LiteDB/bin/Release/net8.0/LiteDB.dll -- /absolute/path/to/a/new/database.db`, rebuilding the probe for each library. Each invocation needs a fresh nonexistent database path.

## Safety invariants and results

| Invariant | Discriminating coverage | Result |
| --- | --- | --- |
| Another opener cannot acknowledge a write omitted from the replacement | Before/after probe; exclusive source/WAL/candidate handles; independent-process probes at source claim, before marker, installation and handoff | Before loses record 99; fixed denies the competing open; later writer persists |
| A real opener cannot slip into the handle-release/reopen handoff | Child reaches actual Open admission while owner is held at `after-data-close`; then resumes and verifies complete records | Both plaintext/encrypted cases pass |
| Failed admission does not alter original data/WAL | Existing data/WAL handles, repeated denial, exact byte comparisons, successful retry | Pass |
| Process death releases transient ownership without bypassing incomplete-installation protection | Kill owner twice before marker; kill after candidate publication; repeat reopen and verify original backups | Pass |
| Cleanup failure retains correct live-state credentials and original errors | Inject data/WAL/candidate cleanup failures and installation failure; reopen using installed password; aggregate checks | Pass |
| Read-only sharing and caller-owned streams remain safe | Two read-only opens through normalized alias; filename-plus-stream rebuild rejects without closing/changing streams | Pass |
| Unsupported Unix locking fails before records are read | Separate child with locking disabled, exact source/WAL bytes retained | Pass |
| Shared/Coordinated admission and cached handles remain compatible | SharedFileHandles, SharedReaderPin, SharedOrdinaryOpen, SharedOpeningMutation, CoordinatorSafety, CoordinatorSnapshot | 55 pass on .NET 8 |
| Released clean files remain readable/upgradable and writable | All 56 pinned released-writer fixtures, full payload/index model, direct writes, rollback and repeated reopen | 56 pass |
| Existing durable migration recovery remains repeatable | Plain/encrypted separate-process crash and partial-I/O at promotion/WAL/commit/checkpoint | All 16 pass |

Broader issue/migration/rebuild/opening/stream-pool regressions: 319 pass, one existing skip on .NET 8 at `bd95a2ae`; 321 pass, one existing skip on .NET 10 with the final source. The final .NET 8 ownership rerun passes all 35 ownership cases (including the two added handoff cases). These are overlapping suites, not additive test counts. Corpus and migration-process runs used the final source. The optional original #3022 attachment check recovered 6,824 documents and recorded six diagnostics.

Reproduce permanent ownership tests with:

```sh
dotnet test LiteDB.Tests -c Release -f net8.0 -p:TestingEnabled=true --settings tests.runsettings --filter FullyQualifiedName~RebuildOwnership
python3 scripts/test-release-compatibility.py --help
python3 scripts/test-index-migration-recovery.py
```

Use the compatibility script's `--artifacts` path to `compatibility/released` in a checkout of `LiteDB-Artifacts`, pinned corpus commit `7896d47c1261f19260ace114b4aed772a1f9c783`; `--check-issue-attachment` enables the optional damaged-file reproduction. The fixture folder contains writer/package/file provenance.

## Limits

These are local Linux results; hosted Windows, Linux and macOS checks are separately reported on the PR. The fault model includes process termination, injected I/O and cleanup failures on supported local filesystems. It does not establish behavior on devices that ignore flushes. All connections must use a consistent data/WAL path; normalized absolute/relative paths are covered, but symlink/hard-link aliases and old binaries do not share the path-based admission protocol. Physical claims do not prove equivalence of sidecar identities. The durable marker still requires verified manual recovery after an interrupted installation; abandoning the mutex never grants permission to bypass it.
