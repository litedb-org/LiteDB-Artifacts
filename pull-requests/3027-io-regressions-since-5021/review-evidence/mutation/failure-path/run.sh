#!/bin/bash
S=$SCRATCH
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
cd $S/rv-sync
FILTER='FullyQualifiedName~FreshEngineDurability|FullyQualifiedName~RetiredSlotPowerLoss|FullyQualifiedName~UnsyncableRetirement|FullyQualifiedName~UnsyncedBackfillPowerLoss|FullyQualifiedName~SharedUnsyncableLog|FullyQualifiedName~MvccUnsyncableLog'
for m in "$@"; do
  f=$(python3 $S/mut/mutations.py $m) || { echo "$m: APPLY FAILED $f"; continue; }
  touch $f
  if ! dotnet build LiteDB.Tests/LiteDB.Tests.csproj -c Release -f net10.0 -p:TestingEnabled=true > $S/mut/$m.build.log 2>&1; then
    echo "$m: BUILD FAILED"; git checkout -- $f; touch $f; continue
  fi
  timeout 900 dotnet test LiteDB.Tests/LiteDB.Tests.csproj -c Release -f net10.0 -p:TestingEnabled=true --no-build --settings tests.runsettings --filter "$FILTER" > $S/mut/$m.test.log 2>&1
  summary=$(grep -E "Passed!|Failed!" $S/mut/$m.test.log | sed 's/ - LiteDB.*//')
  failed=$(grep -E "^\s+Failed " $S/mut/$m.test.log | sed -E 's/^\s+Failed LiteDB\.(Tests\.)?//; s/ \[.*//' | tr '\n' ';')
  echo "$m: $summary :: $failed"
  git checkout -- $f; touch $f
done
