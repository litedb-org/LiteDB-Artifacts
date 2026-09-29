#!/bin/bash
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
cd $REPO/.claude/worktrees/agent-ae3466fb0272766da
P=LiteDB.ReproRunner/Repros/Issue_3027_PromotionAfterFailedCheckpoint/Issue_3027_PromotionAfterFailedCheckpoint.csproj
O=$SCRATCH/promo-proof
dotnet build $P -c Release -o $O/bin-pkg -v q -nologo 2>&1 | tail -3
dotnet build $P -c Release -o $O/bin-latest -p:UseProjectReference=true -v q -nologo 2>&1 | tail -3
ls -la $O/bin-pkg/LiteDB.dll $O/bin-latest/LiteDB.dll
