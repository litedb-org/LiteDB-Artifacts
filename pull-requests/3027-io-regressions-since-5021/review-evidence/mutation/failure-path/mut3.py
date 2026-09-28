J='LiteDB/Engine/Disk/DiskService.HeaderJournal.cs'
V='LiteDB/Engine/Disk/DiskService.Validation.cs'
W='LiteDB/Engine/Disk/WalChecksum.cs'
X='LiteDB/Engine/Services/WalIndexService.cs'
K='LiteDB/Engine/Services/WalIndexService.Checkpoint.cs'
R='LiteDB/Engine/Disk/DiskService.WalWrite.cs'
S='LiteDB/Engine/Disk/DiskService.cs'
M = {
 'P01_recovery_journal_sync_proves_data': (J, "SyncLogBarrierUnproven(((ChecksummedWalStream)_writer.Value).RawStream);", "SyncLogBarrier(((ChecksummedWalStream)_writer.Value).RawStream);"),
 'P02_no_frame0_reject': (V, "                if (WalChecksum.IsFrame(frame, 0))", "                if (false)"),
 'P03_isframe_no_crc': (W, "            return expected == actual;\n        }\n\n        private static ulong Contribution", "            return true;\n        }\n\n        private static ulong Contribution"),
 'P04_isframe_no_position': (W, "metadata.ReadUInt32(0) != Magic || metadata.ReadInt64(24) != position) return false;", "metadata.ReadUInt32(0) != Magic) return false;"),
 'P05_no_page_bound': (X, "if (entry.PageID > legacyLimit) throw LegacyPageOutOfRange(entry.PageID, legacyLimit);", "{ }"),
 'P06_bound_without_log_pages': (X, "Math.Max(header.LastPageID, dataPages - 1) + logPages", "Math.Max(header.LastPageID, dataPages - 1)"),
 'P07_bound_header_only': (X, "Math.Max(header.LastPageID, dataPages - 1) + logPages", "header.LastPageID + logPages"),
 'P08_bound_data_only': (X, "Math.Max(header.LastPageID, dataPages - 1) + logPages", "(dataPages - 1) + logPages"),
 'P09_no_stop_inside_writer': (K, "                stopBegun = _disk.BeginCheckpointStop(error, out stopOwned);\n", ""),
 'P10_log_never_buffers': (S, "_logMayBuffer = settings.LogStream != null && !(settings.LogStream is MemoryStream);", "_logMayBuffer = false;"),
 'P11_flush_failure_not_stopped': (R, "catch (Exception ex) when (count > 0 && _logMayBuffer)", "catch (Exception ex) when (false)"),
 'P12_truncation_ignores_buffered_batch': (R, "if (!overwrite && (count == 0 || !_logMayBuffer)) uncertain = false;", "if (!overwrite) uncertain = false;"),
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
