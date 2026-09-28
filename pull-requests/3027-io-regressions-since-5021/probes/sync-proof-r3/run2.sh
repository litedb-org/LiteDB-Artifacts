#!/bin/bash
# like run.sh but prints probe output
set -u
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
cd $TMPDIR/review3-b
S=$SCRATCH/r3b
name=$1; file=$2; old=$3; new=$4; filter=$5
echo "=== $name"
if [ "$file" != "-" ]; then python3 $S/mutate.py "$file" "$old" "$new" || exit 1; fi
if dotnet build LiteDB.Tests -c Release -f net10.0 -p:TestingEnabled=true > $S/build_$name.log 2>&1; then
  dotnet test LiteDB.Tests -c Release -f net10.0 -p:TestingEnabled=true --no-build --settings tests.runsettings --filter "$filter" --logger "console;verbosity=detailed" 2>&1 | grep -E "^\s+(Passed|Failed) |^ R[0-9]|^ [a-z-]+ round|Expected|Total tests" | sed 's/LiteDB\.\(Tests\.\)\?//'
else
  echo BUILD FAILED; grep -E " error " $S/build_$name.log | head -3
fi
if [ "$file" != "-" ]; then git checkout -- "$file"; fi
