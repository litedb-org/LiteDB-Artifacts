import subprocess, sys, os, pathlib
ROOT = pathlib.Path("$SCRATCH/mutwt")
env = dict(os.environ, PATH="/root/.dotnet:" + os.environ["PATH"], DOTNET_ROOT="/root/.dotnet")
FILTER = "|".join("FullyQualifiedName~" + c for c in ["HeaderFrame_Tests","AcknowledgedReopen_Tests","FreshEngineDurability_Tests","PageChecksum_Tests","CompactPromotionRejection_Tests","CompactPromotionFileRecovery_Tests","UnsyncableDataFile_Tests","UnsyncedBackfillPowerLoss_Tests","LiteDB.Internals.Disk_Tests","WalDurability_Tests"])
HF = "LiteDB/Engine/Disk/DiskService.HeaderFrame.cs"
M = {
 "BASE": [],
 "H1 no header frame": [(HF, "if (!ChecksumsEnabled || _volatileLog || _checksums.JournalBytes != 0 || Interlocked.Read(ref _logLength) != -PAGE_SIZE) return;", "return;")],
 "H2 reader does not skip header frame": [("LiteDB/Engine/Disk/WalRetirementReader.cs", "if (page.WalFrame.RetirementRecord || page.WalFrame.HeaderFrame) continue;", "if (page.WalFrame.RetirementRecord) continue;")],
 "H3 sector rule off": [("LiteDB/Engine/Disk/HeaderFrame.cs", "if (!zeros && !same) return false;", "")],
 "H4 fits rule off": [("LiteDB/Engine/Disk/HeaderFrame.cs", "(new BufferSlice(header, 0, PAGE_SIZE).ReadUInt32(HeaderPage.P_LAST_PAGE_ID) + 1L) * PAGE_SIZE <= System.Math.Max(dataLength, PAGE_SIZE);", "true;")],
 "H5 missing data file initialized over": [(HF, "if (_readOnly && !exists) return false;", "if (!exists) return false;")],
 "H6 read-only restore not applied": [(HF, "                _recoveredHeader = frame;\n", "")],
 "H7 empty data file not restored": [("LiteDB/Engine/Disk/DiskService.cs", "if (dataLength < PAGE_SIZE && this.RestoreDataFileFromLog(dataLength))", "if (false && this.RestoreDataFileFromLog(dataLength))")],
 "H8 header frame not truncated on failure": [(HF, "                    stream.SetLength(0);\n                    _logFactory.TrimCapacity(stream);\n                    uncertain = false;", "                    throw failure;")],
 "D13a bound off": [("LiteDB/Engine/Disk/DiskService.AcknowledgedLog.cs", "            _logLength = failure.AcknowledgedLogEnd - PAGE_SIZE;\n", "")],
 "D13b raw length check off": [("LiteDB/Engine/Disk/DiskService.AcknowledgedLog.cs", "if (reader.RawStream.Length != failure.FailedRawLogLength) return;", "")],
 "D13c shared snapshot not bounded": [("LiteDB/Engine/Disk/DiskService.cs", "settings.WriteFailure ?? settings.SharedDurability?.WriteFailure", "settings.WriteFailure")],
 "D14a barrier off": [("LiteDB/Engine/Disk/DiskService.CommitDurability.cs", "                this.DataBarrierBeforeFirstCommit();\n", "")],
 "D14b shared barrier every op": [("LiteDB/Engine/Disk/DiskService.CommitDurability.cs", "if (shared != null && (shared.DataBarrierDone || shared.FileSyncUnsupported)) return;", "if (shared != null && shared.FileSyncUnsupported) return;")],
 "P1 proof before MoveNext": [("LiteDB/Engine/Disk/DiskService.WalWrite.cs", "                var uncertain = false;\n                try\n                {\n                    using (var iterator", "                this.RequireDurableCommit(stream);\n                var uncertain = false;\n                try\n                {\n                    using (var iterator")],
 "P2 caller log stream remembered": [("LiteDB/Engine/Disk/DiskService.cs", "_logPath = ((ChecksummedWalFactory)_logFactory).IsFile ? LogDurablePath(settings) : null;", "_logPath = LogDurablePath(settings);")],
}
which = sys.argv[1:] or list(M)
for key in which:
    originals = {}
    try:
        for path, old, new in M[key]:
            p = ROOT / path
            if p not in originals: originals[p] = p.read_bytes()
            text = p.read_text()
            assert text.count(old) == 1, (key, path, text.count(old))
            p.write_text(text.replace(old, new))
        b = subprocess.run(["dotnet", "build", "LiteDB.Tests/LiteDB.Tests.csproj", "-f", "net10.0", "-c", "Debug"], cwd=ROOT, env=env, capture_output=True, text=True)
        if b.returncode: print(key, "BUILD FAILED", b.stdout[-800:], flush=True); continue
        try:
            t = subprocess.run(["dotnet", "test", "LiteDB.Tests/LiteDB.Tests.csproj", "-f", "net10.0", "-c", "Debug", "--no-build", "--filter", FILTER, "--logger", "console;verbosity=normal"],
                               cwd=ROOT, env=env, capture_output=True, text=True, timeout=900)
            out = t.stdout
        except subprocess.TimeoutExpired as e:
            out = (e.stdout or b"").decode() if isinstance(e.stdout, bytes) else (e.stdout or "")
            out += "\nTIMEOUT"
        fails = sorted({l.strip().split(' [')[0] for l in out.splitlines() if l.strip().startswith("Failed LiteDB")})
        summary = [l.strip() for l in out.splitlines() if "Passed!" in l or "Failed!" in l or "TIMEOUT" in l or "aborted" in l]
        print(f"== {key}: {summary}", flush=True)
        for f in fails[:8]: print("   ", f, flush=True)
    finally:
        for p, data in originals.items():
            p.write_bytes(data); os.utime(p, None)
print("restored", flush=True)
