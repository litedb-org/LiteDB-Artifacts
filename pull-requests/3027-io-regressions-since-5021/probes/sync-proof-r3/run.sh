#!/bin/bash
# usage: run.sh <name> <file> <old> <new> <filter>
set -u
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
cd $TMPDIR/review3-b
S=$SCRATCH/r3b
name=$1; file=$2; old=$3; new=$4; filter=$5
echo "=== $name"
python3 $S/mutate.py "$file" "$old" "$new" || exit 1
if dotnet build LiteDB.Tests -c Release -f net10.0 -p:TestingEnabled=true > $S/build_$name.log 2>&1; then
  dotnet test LiteDB.Tests -c Release -f net10.0 -p:TestingEnabled=true --no-build --settings tests.runsettings --filter "$filter" --logger "console;verbosity=normal" 2>&1 | grep -E "^\s+(Passed|Failed) |Total tests|Passed!|Failed!" | sed 's/LiteDB\.\(Tests\.\)\?//'
else
  echo BUILD FAILED; tail -5 $S/build_$name.log
fi
git checkout -- "$file"
