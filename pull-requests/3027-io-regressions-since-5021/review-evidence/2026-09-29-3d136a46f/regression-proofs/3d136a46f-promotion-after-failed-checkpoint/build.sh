#!/bin/bash
# build.sh <variant: pkg|latest|knownbad> -> builds into out-<variant>
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
cd $REPO/.claude/worktrees/agent-ae3466fb0272766da
P=LiteDB.ReproRunner/Repros/Issue_3027_PromotionAfterFailedCheckpoint/Issue_3027_PromotionAfterFailedCheckpoint.csproj
O=$SCRATCH/promo-proof/out-$1
case $1 in
  pkg) dotnet build $P -c Release -o $O -p:BaseIntermediateOutputPath=$O/obj/ -nologo -v q ;;
  latest) dotnet build $P -c Release -o $O -p:UseProjectReference=true -nologo -v q ;;
esac
