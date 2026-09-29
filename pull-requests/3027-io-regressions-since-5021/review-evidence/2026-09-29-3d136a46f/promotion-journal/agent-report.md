## The refused-promotion state can't be reached, so I added a test that proves it can't

**Commit:** `ac261ad2ce310282dadc5970daa9dc67fc4b093a`. It's on local branch `review4-refused-promotion`, created from 4662fe49f because the worktree started on an old commit. Nothing is pushed.

**The path.** In `DiskService.WriteFileVersion`, the catch filter `!IsRefusedBeforeWrite(ex)` only matters when a header journal is already outstanding (`JournalBytes != 0`) when a promotion starts. In that case `BeginHeaderJournal` reuses the earlier journal, and the refusal comes from `UnsyncedPromotion` or `RequireLogSynced(wroteNothing: true)`.

**Why it can't be reached (in an engine that hasn't already failed):**
- `RecoverHeaderJournal` retires every checksummed or published journal at open, or throws `UnsyncedHeaderRecovery`, so the engine does not open writable.
- The one journal an open keeps is a legacy conversion journal whose converted header never synced. That header is below v11, so its index-order version is 0 and the index migration must run. The migration's drain reuses the kept journal, backfills the legacy redo, and the log is emptied (drain, then `_walIndex.Clear()`) before `EnableChecksums` writes its own journal. `PromoteFileFormat` also refuses to run on a file without checksums.
- A conversion whose own header doesn't sync throws `UnsyncedDataConversion` before the migration's promotion.
- In a running engine, every journal writer either retires its journal or begins the stop inside the WAL writer, and WAL appends are refused while a journal is outstanding.
- I checked this empirically: with temporary instrumentation over the full net10.0 suite (5,217 passed), no promotion ever started with a journal outstanding. There were 283 reuses of an outstanding journal, and every one was the legacy migration drain.

**One caveat (reasoned, not tested).** A promotion blocked on the WAL writer behind a checkpoint that fails with its journal kept will enter after the stop has begun. The failure is already recorded by then, so the filter makes no observable difference. However, `WriteFileVersion` doesn't re-check `RequireNoWriteFailure`, so a promotion that isn't refused in that window could write and sync the header after the failure. That's worth a follow-up.

**Also seen.** An encrypted file with a kept journal, opened while the data file can't sync, throws `FileSyncException` (errno 22) when the encryption header page is synced, instead of falling back to read-only. The plain file falls back to read-only. `AesStream` documents this, and the files stay unchanged.

**Tests added:** `LiteDB.Tests/Regressions/KeptJournalPromotion_Tests.cs` (IoSafety, NativeFileSync collection). Each of the three tests runs plain and encrypted, so six cases:
1. **Conversion whose header doesn't sync:** the 5.0.21 fixture with the data file failing from the converted-header sync. No promotion starts, the conversion journal is kept, and the power-loss image is legacy data plus that journal.
2. **Reopening that image (the PR's stated precondition):**
   - While the data file can't sync: no promotion, the log and power-loss image are unchanged, and the plain header is rewritten with the journal's copy.
   - Once it syncs: the drain writes no journal of its own, empties the log, and the conversion writes a fresh journal.
   - The migration's promotion is then refused at its start: it saw no outstanding journal, both files are byte-identical, no failure is recorded, it falls back to read-only, and the power-loss image has exactly the expected rows.
   - With syncing storage it migrates. The later compact and index promotions see no journal, and the cold reopen and power-loss image have exact rows.
3. **Torn promotion header repaired from its journal:** the journal is retired before the next promotion.

To observe this I added one test-only hook, `EngineState.ObservePromotion`, behind `#if DEBUG || TESTING`, and registered it in `fault-points.json`. I also added a paragraph to `docs/header-publication.md`.

**Mutation checks (all restored):**

| Mutant | Result |
|---|---|
| M1: conversion keeps its journal and continues | 4 of 6 fail (the migration's promotion is refused behind the kept journal) |
| M2: open keeps an unrepaired checksummed journal | 2 of 6 fail (promotion observed as `(12, True)`) |
| M4: emptying a legacy log keeps `JournalBytes` | 2 of 6 fail (conversion reuses the stale journal) |
| M3: remove `!IsRefusedBeforeWrite` | No test changes (all 209 IoSafety pass) |
| M1 + M3 | Same as M1 |

M3 changes nothing because the filter only differs when a journal is outstanding at the start, which never happens. M1 + M3 matches M1 because the read-only fallback discards the failed open's state.

**Runs:** the `LiteDB.Tests.Regressions` namespace passed 328/328 on net10.0. The related classes outside it (CompactPromotion*, ConversionIntentRecovery, IndexMigration*, CheckpointDurability, Issue2242, MvccUnsyncableLog, Issue2881, PromotionPowerLoss, HeaderJournal*, MvccRecovery) passed 508/508. net462 and net481 build. `check_fault_points` and `check_coverage_regression` (base 4662fe49f) pass.
