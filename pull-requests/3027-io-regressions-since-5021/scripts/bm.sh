#!/bin/bash
# build main tree (Release, TestingEnabled) for net10.0
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
cd $REPO
dotnet build LiteDB.Tests/LiteDB.Tests.csproj -c Release -f net10.0 -p:TestingEnabled=true 2>&1 | grep -E " error |warn.*CS|Error\(s\)|Warning\(s\)" | sort -u | head -20
