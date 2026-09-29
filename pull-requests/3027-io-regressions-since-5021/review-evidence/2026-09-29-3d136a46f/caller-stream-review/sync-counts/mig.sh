#!/bin/bash
# usage: mig.sh <worktree> <label> <encryption> <mode> <stage>
S=$SCRATCH
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1
cd $1
dotnet build tools/IndexMigrationRecovery/IndexMigrationRecovery.csproj -c Release -p:TestingEnabled=true > $S/mig-build-$2.log 2>&1 || { echo BUILD FAILED; exit 1; }
R=tools/IndexMigrationRecovery/bin/Release/net8.0/IndexMigrationRecovery.dll
D=$S/mig-$2; rm -rf $D; mkdir -p $D
dotnet $R create $D/orig.db $3 || exit 1
cp $D/orig.db $D/t.db
start=$(date +%s.%N)
timeout 300 dotnet $R fault $D/t.db $3 $4 $5 > $D/out.txt 2> $D/err.txt; code=$?
end=$(date +%s.%N)
echo "$2 $3 $4 $5 exit=$code secs=$(echo "$end - $start" | bc) $(cat $D/out.txt | tr '\n' ' ') last: $(tail -2 $D/err.txt | tr '\n' ' ')"
