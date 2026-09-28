D='LiteDB/Engine/Disk/DiskService.DurableFlush.cs'
V='LiteDB/Engine/Disk/DiskService.Validation.cs'
W='LiteDB/Engine/Disk/WalChecksum.cs'
H='LiteDB/Engine/Disk/DurableHeaders.cs'
C='LiteDB/Engine/Disk/DiskService.Checksums.cs'
M = {
 'N01_proof_ignores_readonly': (D, "if (!_dataSyncProven && _dataIsFile && !_readOnly && !_dataFlushDegraded)", "if (!_dataSyncProven && _dataIsFile && !_dataFlushDegraded)"),
 'N02_proof_ignores_degraded': (D, "if (!_dataSyncProven && _dataIsFile && !_readOnly && !_dataFlushDegraded)", "if (!_dataSyncProven && _dataIsFile && !_readOnly)"),
 'N03_no_proof_in_SyncRawLog': (D, "            this.ProveDataBeforeLog();\n            var stream = _writer.Value;", "            var stream = _writer.Value;"),
 'N04_record_before_sync': (D, "                data.FlushToDisk();\n                _dataBarrierSynced = _dataSyncProven = true;\n                if (_dataPath != null && data.Length >= PAGE_SIZE) DurableHeaders.Record(_dataPath, ReadDataHeader(data));", "                if (_dataPath != null && data.Length >= PAGE_SIZE) DurableHeaders.Record(_dataPath, ReadDataHeader(data));\n                data.FlushToDisk();\n                _dataBarrierSynced = _dataSyncProven = true;"),
 'N05_matches_any_path': (H, "if (!_hashes.TryGetValue(path, out hash)) return false;", "hash = _hashes.Values.FirstOrDefault(); if (hash == null) return false;"),
 'N06_isframe_no_crc': (W, "            return expected == actual;\n        }\n\n        private static ulong Contribution", "            return true;\n        }\n\n        private static ulong Contribution"),
 'N07_reject_frame0_only': (V, "                    if (!WalChecksum.IsFrame(frame, position)) continue;", "                    if (!WalChecksum.IsFrame(frame, position)) { if (offset == 0) break; continue; }"),
 'N08_no_reject': (V, "                if (!ChecksumsEnabled) this.RejectConvertedWal();", ""),
 'N09_no_conversion_refusal': (C, "            if (this.DataUnsyncedWhileLogSyncs) throw UnsyncableConversion();", ""),
 'N10_path_null': (D, "            _dataPath_placeholder", "x"),
}
import sys
name=sys.argv[1]
if name=='list':
    print("\n".join(k for k in M if k!='N10_path_null')); sys.exit()
f,old,new=M[name]
s=open(f).read()
n=s.count(old)
if n!=1: print("MATCH",n,name); sys.exit(2)
open(f,'w').write(s.replace(old,new))
print(f)
