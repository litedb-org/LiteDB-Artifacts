D='LiteDB/Engine/Disk/DiskService.DurableFlush.cs'
W='LiteDB/Engine/Disk/DiskService.WalSlots.cs'
C='LiteDB/Engine/Services/WalIndexService.Checkpoint.cs'
B='LiteDB/Engine/Services/CheckpointBackoff.cs'
R='LiteDB/Engine/Services/RebuildService.cs'
I='LiteDB/Engine/Engine/IndexMigration.cs'
E='LiteDB/Engine/EngineSettings.cs'
M = {
 'M01_no_ProveDataFile_on_commit': (D, "            if (!_dataSyncProven && _dataIsFile) this.ProveDataFile();\n", ""),
 'M02_shortcut_ignores_header': (D, "if (durable != null && ReadDataHeader(data).SequenceEqual(durable))", "if (durable != null)"),
 'M03_never_shortcut': (D, "if (durable != null && ReadDataHeader(data).SequenceEqual(durable))", "if (false)"),
 'M04_slotreuse_no_data_sync': (D, "            if (!_dataSyncProven) this.SyncDataFile();\n", ""),
 'M05_slotreuse_no_log_sync': (D, "            if (!this.FlushDegraded) this.SyncRawLog();\n            return _slotReuseProven", "            return _slotReuseProven"),
 'M06_slotreuse_ignores_failure': (D, "return _slotReuseProven = !this.FlushDegraded;", "return _slotReuseProven = true;"),
 'M07_no_DurableHeader_update': (D, "if (_sharedDurability != null && _dataIsFile) _sharedDurability.DurableHeader = ReadDataHeader(data);", ""),
 'M08_dataBarrierSynced_not_cleared': (D, "                _dataBarrierSynced = false;\n", ""),
 'M09_logBarrierSynced_never_set': (D, "                log.FlushToDisk();\n                _logBarrierSynced = true;", "                log.FlushToDisk();"),
 'M10_volatile_ignored': (D, "!_volatileLog && !_dataBarrierSynced", "!_dataBarrierSynced"),
 'M11_reclaim_no_degraded_guard': (W, "                // Keep the retired frames: while the root names them, reads skip them.\n                if (this.FlushDegraded) return;\n", ""),
 'M12_no_kept_wal': (C, "if (reclaim && _disk.DataUnsyncedWhileLogSyncs)", "if (false)"),
 'M13_no_root_guard': (C, "if (retirement != null && _disk.FlushDegraded)", "if (false)"),
 'M14_no_rationing': (C, "if (automatic && _backoff.DefersKeptWal(_disk.GetFileLength(FileOrigin.Log))) return 0;", ""),
 'M15_WalEmptied_noop': (B, "lock (_sync) _keptWalLength = 0;", ""),
 'M16_close_not_automatic': (C, "automatic: _rationClose);", "automatic: false);"),
 'M17_no_rebuild_refusal': (R, "if (File.Exists(tempLog) && new FileInfo(tempLog).Length > 0)", "if (false)"),
 'M18_no_conversion_message': (I, "if (_disk.DataUnsyncedWhileLogSyncs)", "if (false)"),
 'M19_volatile_always_true': (E, "internal bool VolatileLog => this.LogStream == null &&", "internal bool VolatileLog => true || this.LogStream == null &&"),
 'M20_shortcut_prefix_only': (D, "ReadDataHeader(data).SequenceEqual(durable)", "ReadDataHeader(data).Take(64).SequenceEqual(durable.Take(64))"),
 'M21_rawlog_not_tracked': (D, "                raw.FlushToDisk();\n                _logBarrierSynced = true;", "                raw.FlushToDisk();"),
 'M22_no_KeptWal_record': (C, "                    _backoff.KeptWal(_disk.GetFileLength(FileOrigin.Log));\n", ""),
 'M23_kept_wal_reclaim_ignored': (C, "                    reclaim = false;\n                    _backoff.KeptWal", "                    _backoff.KeptWal"),
 'M24_slotreuse_skip_when_degraded_removed': (D, "if (this.FlushDegraded || this.LogSyncUnverified) return false;", "if (this.LogSyncUnverified) return false;"),
}
import sys
name=sys.argv[1]
if name=='list':
    print("\n".join(M)); sys.exit()
f,old,new=M[name]
s=open(f).read()
n=s.count(old)
if n!=1: print("MATCH",n,name); sys.exit(2)
open(f,'w').write(s.replace(old,new))
print(f)
