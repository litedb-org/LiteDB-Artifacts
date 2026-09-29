#!/bin/bash
# Run the permanent guard classes of the four proofs (net10.0 Debug) in this worktree.
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
P=$SCRATCH/proofs3027b
cd $REPO/.claude/worktrees/agent-af1c17944ad1df782 || exit 1
FILTER="FullyQualifiedName~LiteDB.Tests.Regressions.DumpPinnedWalSlot_Tests|FullyQualifiedName~LiteDB.Tests.Regressions.StreamDatabaseDispose_Tests|FullyQualifiedName~LiteDB.Tests.Regressions.LegacyReadOnlyStream_Tests|FullyQualifiedName~LiteDB.Tests.Regressions.LegacyWalPageBound_Tests|FullyQualifiedName~LiteDB.Tests.Regressions.ConvertedWalBesideLegacyHeader_Tests"
dotnet test LiteDB.Tests/LiteDB.Tests.csproj -c Debug -f net10.0 --filter "$FILTER" --logger "console;verbosity=normal" > "$P/guards.log" 2>&1
echo "exit=$?" >> "$P/guards.log"
