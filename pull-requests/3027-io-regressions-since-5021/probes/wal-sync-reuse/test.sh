#!/bin/bash
# reviewer agent-ab0ddec58a37c5425 only
cd $REPO/.claude/worktrees/agent-ab0ddec58a37c5425 || exit 1
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
if [ "$2" = "build" ]; then
  dotnet build LiteDB.Tests/LiteDB.Tests.csproj -c Release -f net10.0 -p:TestingEnabled=true 2>&1 | grep -E " error |Error\(s\)" | head -20
fi
timeout 280 dotnet test LiteDB.Tests/LiteDB.Tests.csproj -c Release -f net10.0 -p:TestingEnabled=true --no-build --settings tests.runsettings --filter "$1" --logger "console;verbosity=normal" 2>&1 | grep -vE "^\s*$|warning" | tail -${3:-60}
