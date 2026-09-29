#!/bin/bash
# Runs the promotion/journal related test classes outside the Regressions namespace on net10.0.
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
cd $REPO/.claude/worktrees/agent-af988567ece06d389 || exit 1
FILTER='FullyQualifiedName~CompactPromotion|FullyQualifiedName~ConversionIntentRecovery|FullyQualifiedName~IndexMigration|FullyQualifiedName~CheckpointDurability|FullyQualifiedName~Issue2242|FullyQualifiedName~MvccUnsyncableLog|FullyQualifiedName~Issue2881|FullyQualifiedName~PromotionPowerLoss|FullyQualifiedName~HeaderJournal|FullyQualifiedName~MvccRecovery'
dotnet test LiteDB.Tests/LiteDB.Tests.csproj -c Release -f net10.0 --no-build --filter "$FILTER"
