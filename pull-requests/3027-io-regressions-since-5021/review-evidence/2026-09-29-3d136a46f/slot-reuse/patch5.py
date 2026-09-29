p = '$REPO/.claude/worktrees/agent-ac6b0cf63f278326a/docs/storage-stack-safety.md'
s = open(p).read()


def rep(old, new):
    global s
    assert s.count(old) == 1, old
    s = s.replace(old, new)


rep('To find such storage before a witness or a reused slot depends on it, every retiring checkpoint first '
    'syncs the data file and the log (the log\'s directory once per engine), and an engine syncs the raw log once '
    'before it first reuses a slot; an engine syncs the data file once before its first commit',
    'To find such storage before a witness depends on it, every retiring checkpoint first syncs the data file and '
    'the log (the log\'s directory once per engine). Reusing a slot syncs nothing, in any engine ([decisions]'
    '(decisions/durability-policy.md) note 15): a slot is free only while a root on the device witnesses it, as a '
    'root whose sync failed keeps its header journal and the next open syncs it or opens read-only, and a clear that '
    'never reached the device leaves a witnessed old frame, which recovery skips. An engine syncs the data file once '
    'before its first commit')

rep('[RetiredSlotPowerLoss_Tests](../LiteDB.Tests/Regressions/RetiredSlotPowerLoss_Tests.cs) and '
    '[UnsyncedBackfillPowerLoss_Tests](../LiteDB.Tests/Regressions/UnsyncedBackfillPowerLoss_Tests.cs) recover each '
    'file as of its last successful sync after the data file stops syncing, with a second independent connection, '
    'also after neither file synced a full checkpoint and the WAL alone syncs again (documents and index compared);',
    '[RetiredSlotPowerLoss_Tests](../LiteDB.Tests/Regressions/RetiredSlotPowerLoss_Tests.cs) and '
    '[UnsyncedBackfillPowerLoss_Tests](../LiteDB.Tests/Regressions/UnsyncedBackfillPowerLoss_Tests.cs) recover each '
    'file as of its last successful sync after the data file stops syncing, with a second independent connection, '
    'also after neither file synced a full checkpoint and the WAL alone syncs again (documents and index compared); '
    '[SlotReuseWithoutProof_Tests](../LiteDB.Tests/Regressions/SlotReuseWithoutProof_Tests.cs) checks every WAL frame '
    'overwrite against the root on the device after a root sync that answered "cannot sync" or failed with an EIO that '
    'forgot it (new direct and shared connections, and a shared one whose data barrier already ran), and recovers '
    'exactly every power-loss image of a reusing commit (its pending writes lost, written back, torn, or only its own) '
    'with clears that synced, were forgotten or are still dirty, plain and encrypted, documents and an index compared;')

rep('an open that recovers a header from its journal writes it back (the same bytes) before the sync that retires '
    'the journal, so a header the failed sync left in the cache only reaches the device.',
    'an open that recovers a header from its journal writes it back (the same bytes) before the sync that retires '
    'the journal, so a header the failed sync left in the cache only reaches the device (a retirement root among '
    'them: its slots become reusable only then).')

rep('[SyncFailureRecovery_Tests](../LiteDB.Tests/Regressions/SyncFailureRecovery_Tests.cs) (a header journal retired '
    'after the header\'s sync failed, with pages the failed sync marked clean),',
    '[SyncFailureRecovery_Tests](../LiteDB.Tests/Regressions/SyncFailureRecovery_Tests.cs) (a header journal retired '
    'after the header\'s sync failed, with pages the failed sync marked clean), '
    '[SlotReuseWithoutProof_Tests](../LiteDB.Tests/Regressions/SlotReuseWithoutProof_Tests.cs) (a witness root whose '
    'sync failed that way; clears it forgot),')
open(p, 'w').write(s)
