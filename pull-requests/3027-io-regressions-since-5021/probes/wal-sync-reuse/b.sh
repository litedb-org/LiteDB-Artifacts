#!/bin/bash
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
cd $REPO/.claude/worktrees/agent-ab0ddec58a37c5425
dotnet build LiteDB.Tests/LiteDB.Tests.csproj -c Release -f net10.0 -p:TestingEnabled=true 2>&1 | grep -E "error|Error\(s\)" | head -20
