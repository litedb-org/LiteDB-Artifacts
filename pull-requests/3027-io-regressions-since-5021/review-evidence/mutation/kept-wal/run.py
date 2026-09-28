import subprocess, shutil, os, sys
R='$REPO'
S='$SCRATCH/mut11'
env=dict(os.environ, PATH='/root/.dotnet:'+os.environ['PATH'], DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1')
F="FullyQualifiedName~DataFileStopsSyncing|FullyQualifiedName~UnsyncedBackfillPowerLoss|FullyQualifiedName~FreshEngineDurability|FullyQualifiedName~UnsyncableDataFile|FullyQualifiedName~ConvertedWalBesideLegacyHeader|FullyQualifiedName~UnsyncableRetirement|FullyQualifiedName~RetiredSlotPowerLoss"
M=[
 ('M1 checkpoint empties WAL after failed data sync','LiteDB/Engine/Services/WalIndexService.Checkpoint.cs','                if (reclaim && _disk.KeepsWal) reclaim = false;\n',''),
 ('M2 checkpoints never defer','LiteDB/Engine/Services/WalIndexService.Checkpoint.cs','            if (_disk.DefersCheckpoint()) return 0;\n',''),
 ('M3 no conversion check before the drain','LiteDB/Engine/Engine/IndexMigration.cs','            if (!_disk.ChecksumsEnabled && !_disk.DataFileSyncs()) throw DiskService.UnsyncedDataConversion();\n',''),
 ('M4 no conversion guard in EnableChecksums','LiteDB/Engine/Disk/DiskService.Checksums.cs','            if (!_dataBarrierSynced) throw UnsyncedDataConversion();\n',''),
 ('M5 no rebuild refusal','LiteDB/Engine/Services/RebuildService.cs','            if (File.Exists(tempLog) && new FileInfo(tempLog).Length > 0)\n','            if (false)\n'),
 ('M6 volatile WAL kept too','LiteDB/Engine/Disk/DiskService.DurableFlush.cs','        internal bool KeepsWal => !_volatileLog && !_dataBarrierSynced;','        internal bool KeepsWal => !_dataBarrierSynced;'),
 ('M7 walKept never reported','LiteDB/Engine/SystemCollections/SysDatabase.cs','                ["walKept"] = _disk.KeepsWal,','                ["walKept"] = false,'),
]
out=open(S+'/results.txt','w')
for name,path,old,new in M:
    p=os.path.join(R,path); orig=open(p).read()
    if orig.count(old)!=1: out.write(f'{name}: PATTERN NOT FOUND\n'); out.flush(); continue
    open(p,'w').write(orig.replace(old,new))
    try:
        b=subprocess.run(['dotnet','build','LiteDB.Tests/LiteDB.Tests.csproj','-c','Release','-f','net10.0','-p:TestingEnabled=true'],cwd=R,env=env,capture_output=True,text=True)
        if b.returncode!=0 or 'Build succeeded' not in b.stdout:
            out.write(f'{name}: BUILD FAILED\n'); out.flush(); continue
        t=subprocess.run(['dotnet','test','LiteDB.Tests/LiteDB.Tests.csproj','-c','Release','-f','net10.0','--no-build','--filter',F],cwd=R,env=env,capture_output=True,text=True,timeout=900)
        fails=[l.strip() for l in t.stdout.splitlines() if l.strip().startswith('Failed LiteDB')]
        summ=[l.strip() for l in t.stdout.splitlines() if 'Failed!' in l or 'Passed!' in l]
        out.write(f'{name}: {len(fails)} failed; {summ}\n'+''.join('   '+f+'\n' for f in fails)); out.flush()
    finally:
        open(p,'w').write(orig)
b=subprocess.run(['dotnet','build','LiteDB.Tests/LiteDB.Tests.csproj','-c','Release','-f','net10.0','-p:TestingEnabled=true'],cwd=R,env=env,capture_output=True,text=True)
out.write('restored build: '+('ok' if b.returncode==0 else 'FAILED')+'\nDONE\n'); out.close()
