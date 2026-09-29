#!/bin/bash
# Build one repro against a given LiteDB package version and run it directly (exit code + result line).
# Usage: against.sh <repro> <version>
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
P=$SCRATCH/proofs
REPRO="$1"; VERSION="$2"
if [ -n "$3" ]; then export RestoreAdditionalProjectSources="$3"; fi
OUT="$P/against/$REPRO-$VERSION"
rm -rf "$OUT"; mkdir -p "$OUT"
cd $REPO/.claude/worktrees/agent-aaeaca9f570479c49/LiteDB.ReproRunner/Repros/$REPRO || exit 1
dotnet build "$REPRO.csproj" -c Release -p:UseProjectReference=false -p:LiteDBPackageVersion="$VERSION" \
  -p:OutputPath="$OUT/" > "$OUT/build.log" 2>&1 || { echo "$REPRO $VERSION: BUILD FAILED"; grep -m3 error "$OUT/build.log"; exit 1; }
dotnet "$OUT/$REPRO.dll" > "$OUT/run.log" 2>&1
code=$?
echo "$REPRO against LiteDB $VERSION: exit $code"
grep -o '"type":"result"[^}]*"text":"[^"]*"' "$OUT/run.log" | sed 's/.*"text":/    /'
