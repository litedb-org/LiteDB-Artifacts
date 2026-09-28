#!/bin/bash
# usage: run-partitions.sh <repo> <framework> <outdir>
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
repo=$1; fw=$2; out=$3; mkdir -p $out
declare -A groups=(
 [issues]='FullyQualifiedName~LiteDB.Tests.Issues.'
 [rebuild]='FullyQualifiedName~LiteDB.Tests.Engine.Rebuild'
 [engine-compact]='FullyQualifiedName~LiteDB.Tests.Engine.Compact'
 [engine-index]='FullyQualifiedName~LiteDB.Tests.Engine.Index'
 [engine]='FullyQualifiedName~LiteDB.Tests.Engine.&FullyQualifiedName!~LiteDB.Tests.Engine.Rebuild&FullyQualifiedName!~LiteDB.Tests.Engine.Compact&FullyQualifiedName!~LiteDB.Tests.Engine.Index'
 [query-not-equal]='FullyQualifiedName~LiteDB.Tests.QueryTest.NotEqualIndex_Tests'
 [query]='FullyQualifiedName~LiteDB.Tests.QueryTest.&FullyQualifiedName!~LiteDB.Tests.QueryTest.NotEqualIndex_Tests'
 [shared]='FullyQualifiedName~LiteDB.Internals.Shared'
 [mvcc]='FullyQualifiedName~LiteDB.Internals.Mvcc'
 [internals]='FullyQualifiedName~LiteDB.Internals.&FullyQualifiedName!~LiteDB.Internals.Shared&FullyQualifiedName!~LiteDB.Internals.Mvcc'
 [remaining]='FullyQualifiedName!~LiteDB.Tests.Issues.&FullyQualifiedName!~LiteDB.Tests.Engine.&FullyQualifiedName!~LiteDB.Tests.QueryTest.&FullyQualifiedName!~LiteDB.Internals.'
)
cd $repo
for g in "${!groups[@]}"; do
  start=$(date +%s)
  dotnet test LiteDB.Tests/LiteDB.Tests.csproj -c Release -f $fw -p:TestingEnabled=true --no-build --settings tests.runsettings \
    --filter "${groups[$g]}" --logger "trx;LogFileName=$out/$g.trx" > $out/$g.log 2>&1
  code=$?
  echo "$g exit=$code $(( $(date +%s) - start ))s $(grep -E 'Passed!|Failed!|Total tests' $out/$g.log | tail -1)" >> $out/summary.txt
done
echo DONE >> $out/summary.txt
