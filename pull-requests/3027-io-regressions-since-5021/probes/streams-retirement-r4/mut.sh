#!/bin/bash
# usage: mut.sh <name> <filter> ; expects the mutation already applied in worktree
cd $TMPDIR/review4-a
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
echo "== $1"; git diff --stat | tail -1
dotnet build LiteDB.Tests -c Release -f net10.0 -p:TestingEnabled=true 2>&1 | grep -E " error |Error\(s\)" | head -5
timeout 900 dotnet test LiteDB.Tests -c Release -f net10.0 -p:TestingEnabled=true --no-build --settings tests.runsettings --filter "$2" 2>&1 | grep -E "^\s+Failed |Passed!|Failed!|Total tests" | head -30
git checkout -- LiteDB LiteDB.Tests
