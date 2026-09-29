#!/bin/bash
# Build testwt for net10.0 and net8.0, then run all partitions of both.
S=$SCRATCH
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
cd $S/testwt
for fw in net10.0 net8.0; do
  dotnet build LiteDB.Tests/LiteDB.Tests.csproj -c Release -f $fw -p:TestingEnabled=true > $S/head-build-$fw.log 2>&1 || { echo "BUILD FAILED $fw" > $S/head-$fw.status; continue; }
  rm -rf $S/head-$fw; $S/run-partitions.sh $S/testwt $fw $S/head-$fw
  echo done > $S/head-$fw.status
done
