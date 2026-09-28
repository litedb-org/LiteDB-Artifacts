#!/bin/bash
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
cd $1; out=$2; mkdir -p $out
dotnet restore LiteDB.Fuzz/LiteDB.Fuzz.csproj > $out/restore.log 2>&1
for fw in net8.0 net10.0; do dotnet build LiteDB.Fuzz/LiteDB.Fuzz.csproj -c Release -f $fw --no-restore -p:TestingEnabled=true > $out/build-$fw.log 2>&1 || echo "BUILD FAIL $fw" >> $out/summary.txt; done
run() { name=$1; fw=$2; shift 2; start=$(date +%s); env "$@" > $out/$name.log 2>&1; echo "$name exit=$? $(( $(date +%s)-start ))s" >> $out/summary.txt; }
F="dotnet run --project LiteDB.Fuzz -c Release --no-build"
run linux-core-utc net8.0 TZ=UTC LANG=en_US.UTF-8 $F -f net8.0 -- --target query,transaction,wal,page,shared,bson,snapshot,threaded-snapshot,concurrent,transaction-gate,cursor-handoff,conflict,power-loss,recovery,chaos,boundary,read-only --seed 2947 --count 30 --artifact-dir $out/a1
run linux-cache-vienna net10.0 TZ=Europe/Vienna LANG=tr_TR.UTF-8 $F -f net10.0 -- --target linq-cache,parser,sql-dml,value,api-boundary,oracle-selftest,transaction-gate,cursor-handoff,compact-codec --seed 102947 --count 30 --artifact-dir $out/a2
run index-sort net8.0 TZ=UTC $F -f net8.0 -- --target index,mapper,sort,pressure --seed 202947 --count 30 --artifact-dir $out/a3
run persistence net8.0 TZ=UTC $F -f net8.0 -- --target wal,shared,power-loss,recovery,read-only,storage-failure,rebuild-transition --seed 252947 --count 10 --artifact-dir $out/a4
run storage-vector net10.0 TZ=America/New_York $F -f net10.0 -- --target storage,storage-failure,rebuild,rebuild-transition,vector,integrity,compatibility,malformed-file,compact-storage,compact-power-loss --seed 302947 --count 30 --artifact-dir $out/a5
run arm64-core net8.0 TZ=UTC $F -f net8.0 -- --target query,bson,value,integrity --seed 402947 --count 30 --artifact-dir $out/a6
run checksums net8.0 TZ=UTC $F -f net8.0 -- --target checksum-page,checksum-wal,checksum-migration,checksum-crash,compact-crash,mvcc-retirement,mvcc-checkpoint --seed 2954 --count 28 --determinism-check --artifact-dir $out/a7
run determinism net8.0 TZ=UTC $F -f net8.0 -- --target query,index,bson,parser,mapper,value --seed 42947 --count 40 --determinism-check --artifact-dir $out/a8
dotnet restore LiteDB.Fuzz.Tests/LiteDB.Fuzz.Tests.csproj > $out/ftrestore.log 2>&1
run fuzz-tests net8.0 TZ=UTC dotnet test LiteDB.Fuzz.Tests/LiteDB.Fuzz.Tests.csproj -c Release --no-restore -p:TestingEnabled=true
run triage net8.0 TZ=UTC python3 scripts/test-fuzz-finding-triage.py
run v8-differential net8.0 TZ=UTC python3 scripts/test-v8-differential.py --seeds 3 --operations 80
echo DONE >> $out/summary.txt
