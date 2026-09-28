#!/bin/bash
S=$SCRATCH
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
out=$S/r5b; rm -rf $out; mkdir -p $out
# net8 full suite (partitioned)
cd $S/fullrun && dotnet build LiteDB.Tests -c Release -f net8.0 -p:TestingEnabled=true > $out/build-net8.log 2>&1
$S/run-partitions.sh $S/fullrun net8.0 $out/net8
# compatibility scripts
cd $S/compatwt
for s in "test-index-compatibility.py" "test-vector-compatibility.py" "test-v9-compatibility.py" "test-mvcc-compatibility.py --parent-ref a2ea9a65a469f6449bb8838592f5266b078758b3" "test-parent-format-compatibility.py --max-version 12 --v12-ref a2ea9a65a469f6449bb8838592f5266b078758b3" "test-index-migration-recovery.py"; do
  name=$(echo $s | cut -d' ' -f1); start=$(date +%s)
  python3 scripts/$s > $out/$name.log 2>&1
  echo "$name exit=$? $(( $(date +%s)-start ))s" >> $out/compat-summary.txt
done
# fuzz shards
$S/run-fuzz.sh $S/fuzzwt $out/fuzz
echo CHAIN-DONE >> $out/compat-summary.txt
