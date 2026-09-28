#!/bin/bash
# build fixwt for net10 and net8, then run partitions for both
S=$SCRATCH
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
cd $S/fixwt
for fw in net10.0 net8.0; do
  dotnet build LiteDB.Tests/LiteDB.Tests.csproj -c Release -f $fw -p:TestingEnabled=true > $S/final-build-$fw.log 2>&1 || { echo "BUILD FAILED $fw" > $S/final-$fw.status; continue; }
  rm -rf $S/final-$fw; $S/run-partitions.sh $S/fixwt $fw $S/final-$fw
  echo done > $S/final-$fw.status
done
