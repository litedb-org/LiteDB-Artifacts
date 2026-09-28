#!/bin/bash
# usage: runmut.sh NAME FILTER FILE OLD NEW
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
S=$SCRATCH
R=${R:-$S/rv-legacy}
name=$1; filter=$2; file=$3; old=$4; new=$5
cd $R
python3 $S/mutate.py "$R/$file" "$old" "$new" || exit 2
dotnet build LiteDB.Tests/LiteDB.Tests.csproj -c Release -f net10.0 -p:TestingEnabled=true 2>&1 | grep -E " error |rror\(s\)" | head -5
timeout 295 dotnet test LiteDB.Tests/LiteDB.Tests.csproj -c Release -f net10.0 -p:TestingEnabled=true --no-build --settings tests.runsettings --filter "$filter" 2>&1 | grep -E "Passed!|Failed!|\[FAIL\]" | head -20
git checkout -- "$file"; touch "$file"
echo "== done $name"
