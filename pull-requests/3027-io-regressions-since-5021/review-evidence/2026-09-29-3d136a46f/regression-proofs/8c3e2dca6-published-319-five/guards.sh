#!/bin/bash
# Run the permanent guard classes of the five proofs in the normal LiteDB.Tests suite (already built).
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
cd $REPO/.claude/worktrees/agent-aaeaca9f570479c49 || exit 1
FILTER="FullyQualifiedName~LegacyWalSharedMigration_Tests|FullyQualifiedName~LegacyDroppedIndex_Tests|FullyQualifiedName~LegacyDamagedDocument_Tests|FullyQualifiedName~UnsyncableDataFile_Tests|FullyQualifiedName~SharedMutexNameLength_Tests"
dotnet test LiteDB.Tests/LiteDB.Tests.csproj -c Release -f net10.0 -p:TestingEnabled=true --no-build --filter "$FILTER" 2>&1 | tail -8
