#!/bin/bash
# run.sh <name> : run the repro via ReproRunner (both variants) and dump the report
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
cd $REPO/.claude/worktrees/agent-ae3466fb0272766da
D=$SCRATCH/promo-proof
timeout 900 dotnet run --project LiteDB.ReproRunner/LiteDB.ReproRunner.Cli -c Release -- run Issue_3027_PromotionAfterFailedCheckpoint --ci --report $D/$1.json > $D/$1.log 2>&1
echo "cli exit $?"
python3 $D/dump.py $D/$1.json
