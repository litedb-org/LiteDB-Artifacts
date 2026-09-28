#!/bin/bash
# full partitioned suites, then the CI checksum fuzz smoke (which replays the corpus seeds), in fixwt
S=$SCRATCH
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
$S/full-run.sh
cd $S/fixwt
for fw in net10.0 net8.0; do
  dotnet build LiteDB.Fuzz/LiteDB.Fuzz.csproj -c Release -f $fw -p:TestingEnabled=true > $S/fuzz-build-$fw.log 2>&1 || { echo "FUZZ BUILD FAILED $fw" >> $S/fuzz11.txt; continue; }
  timeout 1500 dotnet run --project LiteDB.Fuzz -c Release -f $fw --no-build -- --target checksum-page,checksum-wal,checksum-migration,checksum-crash,compact-crash,mvcc-retirement,mvcc-checkpoint --seed 2954 --count 28 --determinism-check --artifact-dir $S/fuzz11-art-$fw > $S/fuzz11-$fw.log 2>&1
  echo "fuzz $fw exit=$? $(grep 'FUZZ SUMMARY' $S/fuzz11-$fw.log)" >> $S/fuzz11.txt
done
echo DONE >> $S/fuzz11.txt
