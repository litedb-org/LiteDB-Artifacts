#!/bin/bash
# usage: tm.sh <filter> [tail] [framework] -- test main tree (no build)
cd $REPO || exit 1
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
dotnet test LiteDB.Tests/LiteDB.Tests.csproj -c Release -f ${3:-net10.0} -p:TestingEnabled=true --no-build --settings tests.runsettings --filter "$1" 2>&1 | grep -E "^\s*Failed |Passed!|Failed!| error |Error Message|at LiteDB" | tail -${2:-40}
