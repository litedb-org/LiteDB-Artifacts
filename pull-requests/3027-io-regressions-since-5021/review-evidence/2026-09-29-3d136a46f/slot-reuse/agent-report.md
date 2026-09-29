Implementation note 15 is done on branch `slot-reuse`, based on 172f667ee, in worktree $REPO/.claude/worktrees/agent-ac6b0cf63f278326a. Nothing was pushed. The only test failure in the full suite (`FrameFailure_Tests.LogPublication_Failure_DiscardsUnpublishedFrame`) was already failing at 172f667ee.

**Commits**
- `055387cf1` Pin slot reuse behind a durable witness root with power-loss images (tests only)
- `fdf9ba0f4` Reuse retired WAL slots without a sync of their own (engine change plus test updates)
- `f9b0158cb` Document slot reuse without a sync of its own (docs)

Each commit message ends with the two required trailer lines.

**Step 1 (verifying the argument): no counterexample found, so I did not stop.**
- **Where free slots come from:** at open, `RestoreIndex` registers only slots named by the root in the header the engine read, and not currently in use. That happens after `RecoverHeaderJournal`. At runtime, `ReclaimLogPages` publishes slots only after the root was synced in `CompletePartialCheckpoint` (which throws `DataStoppedSyncing` otherwise) and after its own clear sync.
- **Can an open see a root that is not on the device, with no journal pending?** No. The journal goes only through `ShrinkLog`, which first requires a data sync covering every data write.
- **Pending journal:** the next open writes the header back and syncs it, or throws `UnsyncedHeaderRecovery` and opens read-only. Read-only engines return before writing anything.
- **The header frame** always carries root 0, because a WAL starts only after a salt reset.
- **Recovery:** in a witnessed slot, a torn or cleared frame, or the old frame matching its witness, is skipped rather than ending recovery. So a lost clear or a torn reused frame cannot lose an earlier commit.
- **Failed commits:** the old proof's refused sync gave `NotCommitted`. Without it, a log that stops syncing fails at the commit's own flush with "Unknown", which note 14 allows. No path shows a failed commit as more than "outcome unknown".
- **Reused frames on unsyncable storage:** a fresh engine that never learned the storage cannot sync may now reuse slots. That is safe because the root was durable when the slot was retired.

**Step 2: pinning tests** (`LiteDB.Tests/Regressions/SlotReuseWithoutProof_Tests.cs`, Category IoSafety, 12 cases)
- `ForgetfulFile` moved out of `SyncFailureRecovery_Tests` into a shared helper, `LiteDB.Tests/Regressions/ForgetfulFile.cs`. It now supports three failures: an EIO that forgets pending writes, "cannot sync" (writes stay pending), and a crash just before a sync.
- It builds power-loss images from the pending writes: all lost, all written back, written back with the last write torn, or only the commit's own writes (older pending writes lost).
- **(a) Root sync fails**, as "cannot sync" or as a forgetful EIO, for three kinds of second engine: a new direct connection, a new shared connection, and a shared connection opened earlier whose data barrier already ran. Every overwrite of a WAL frame is checked against the root on the device. While the data file cannot sync, the open is read-only and both files stay unchanged. Every power-loss image of the reusing commit recovers exactly (documents plus an index query).
- **(b) Clears synced, forgotten by EIO, or still dirty after a crash**, plain and encrypted. A fresh engine reuses the slots; for the dirty case it is an engine that opted out of durable commits, so no sync precedes its frames. Every image at each WAL write and around the sync recovers exactly, and so does the image after a later durable commit.
- All 12 pass on the old engine and on the new one. The one post-change assertion (no sync before the first reuse, plain log only) was added in commit 2.

**Mutation results** (all reverted)
- **M1, header not written back in `RecoverHeaderJournal`:** on both old and new code, all three EIO cases of (a) fail with "frame at 8256 overwritten while the device's root is 0". The power-loss image also loses commits 6 to 9 (recovers value 5). `SyncFailureRecovery_Tests` fails too. The old proof did not catch this, because `DurableHeaders` had recorded the unwritten header as durable.
- **M2, `RecoverHeaderJournal`'s data sync replaced by a fake success:** on old code, only the "cannot sync" read-only expectations fail; the proof hid the rest. On new code, EIO with the pre-opened shared connection also fails on the device-root check. So that recovery sync is now what makes the root durable before reuse.

**Step 3: engine change**
- `ProveSlotReuse` and `_slotReuseProven` are removed.
- `AllocateLogPosition` keeps `!FlushDegraded` and also `!LogSyncUnverified`. The second guard is required by `RuntimeSyncDurability_Tests`, which expects no slot reuse when syncs cannot report failure; I added that to note 15 as well.
- Doc comments in `WalSlots`, `ProveDataFile`, `LogSyncUnverified` and `FlushDegraded` updated. `ProveDataFile` stays for the data barrier before the first commit.

**Step 4: changed test expectations (old → new, each with a note 15 comment)**
- `MvccUnsyncableLog_Tests`, renamed `Fresh_engine_reuses_witnessed_blank_slots_without_a_sync_of_its_own`, case `syncable:false`:
  - reused slots: 0 → more than 0
  - refused syncs: 0 asserted
  - added power-loss images: the log as last synced, and with the reused frames written back but no confirmations; both recover cold=0, docs=20.
- `SharedUnsyncableLog_Tests`, renamed `Fresh_shared_engines_never_reuse_slots_on_a_log_their_connection_knows_cannot_sync_without_durable_commits`, case (clear, second connection):
  - changed frames: 0 → more than 0
  - refused syncs: 1 → 0
  - `SyncFile` now records its durable image, and the "clear" cases add the same two power-loss images.
- `FreshEngineDurability_Tests`, renamed `Slot_reuse_adds_no_sync_per_shared_operation`: new assertion that 5 shared operations make exactly 5 log syncs. The old engine made 10. `FilePowerLossModel` gained a `LogSyncs` counter for this.
- No durability oracle was weakened; each changed test gained power-loss checks.

**Step 5: test counts (net10.0)**

| Test set | Before | After |
|---|---|---|
| Focused set (requested classes plus `RuntimeSyncDurability_Tests`) | 475/475 | 487/487 (+12 new) |
| Category=IoSafety | 173/173 | 185/185 |
| LiteDB.Internals | 986 passed, 1 failed, 1 skipped (988) | same |
| Full suite (after only) | — | 5164 total: 5156 passed, 1 failed, 7 skipped |

The tests also compile for net462 and net481.

**Step 6: docs updated:** `docs/storage-stack-safety.md` (rows 42 and 43, plus evidence), `docs/shared-mode-safety.md` (the paragraph and the table row), `docs/release-notes.md`, `docs/mvcc-checkpoint.md`, `docs/rules/compatibility.md`, and one clause in note 15. The decisions doc says implementation status is tracked in #3027, so I did not add an "implemented" marker there.

**Things that look suspicious**
- **`FrameFailure_Tests.LogPublication_Failure_DiscardsUnpublishedFrame` fails deterministically at 172f667ee.** It most likely assumes the first WAL page lands at position 0, which the header frame has taken since 2b4f1ac0f. I queued a separate task for it and did not change it here.
- **Engines over caller-supplied log streams sync the log before every engine's first commit.** Their log has no path, so `DurableLogs` never remembers it. An encrypted log also syncs when its writer is created.
- **A fresh engine that opted out of durable commits now reuses witnessed slots on a log that cannot sync.** It never learns the log cannot sync. This is safe by the argument above, and power-loss images cover it, but it is a behaviour change.
- **Not caused by this change, and I did not investigate it:** a commit's WAL frames that an earlier engine's failed EIO sync left in the cache but not on the device are built upon by the next engine. Nothing writes them again.