#!/bin/bash
S=$SCRATCH
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
W=$S/r6wt; out=$S/r6; rm -rf $out; mkdir -p $out
echo "head $(git -C $W rev-parse HEAD)" > $out/chain.txt
cd $W
for fw in net10.0 net8.0; do
  dotnet build LiteDB.Tests/LiteDB.Tests.csproj -c Release -f $fw -p:TestingEnabled=true > $out/build-$fw.log 2>&1; echo "build $fw exit=$?" >> $out/chain.txt
  $S/run-partitions.sh $W $fw $out/$fw
done
for s in "test-mvcc-compatibility.py --parent-ref a2ea9a65a469f6449bb8838592f5266b078758b3" "test-index-migration-recovery.py" "test-index-compatibility.py" "test-v9-compatibility.py" "test-parent-format-compatibility.py --max-version 12 --v12-ref a2ea9a65a469f6449bb8838592f5266b078758b3" "test-vector-compatibility.py"; do
  name=$(echo $s | cut -d' ' -f1); start=$(date +%s)
  python3 scripts/$s > $out/$name.log 2>&1
  echo "$name exit=$? $(( $(date +%s)-start ))s" >> $out/chain.txt
done
$S/run-fuzz.sh $W $out/fuzz
echo CHAIN-DONE >> $out/chain.txt
