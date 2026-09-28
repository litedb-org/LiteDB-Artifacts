#!/bin/bash
# usage: t.sh <filter> [build|nobuild] [tail] [verbosity]
cd $REPO/.claude/worktrees/agent-a90774404ee23c2db || exit 1
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
if [ "$2" = "build" ]; then
  dotnet build LiteDB.Tests/LiteDB.Tests.csproj -c Release -f net10.0 -p:TestingEnabled=true 2>&1 | grep -E " error |Error\(s\)" | head -20
fi
dotnet test LiteDB.Tests/LiteDB.Tests.csproj -c Release -f net10.0 -p:TestingEnabled=true --no-build --settings tests.runsettings --filter "FullyQualifiedName~$1" --logger "console;verbosity=${4:-normal}" 2>&1 | grep -vE "^\s*$|warning" | tail -${3:-60}
