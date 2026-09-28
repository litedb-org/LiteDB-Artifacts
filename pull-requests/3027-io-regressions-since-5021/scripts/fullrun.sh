#!/bin/bash
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
cd "$1"
dotnet build LiteDB.Tests -c Release -f net10.0 -p:TestingEnabled=true 2>&1 | grep -E " error |Build succeeded" | sort -u
dotnet test LiteDB.Tests -c Release -f net10.0 -p:TestingEnabled=true --no-build --settings tests.runsettings 2>&1 | grep -E "^\s+Failed |Passed!|Failed!|Total tests|Skipped!" 
