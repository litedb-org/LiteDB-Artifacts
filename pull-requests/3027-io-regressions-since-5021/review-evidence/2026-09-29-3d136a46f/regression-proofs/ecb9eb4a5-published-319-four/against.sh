#!/bin/bash
# Build one repro against a LiteDB package version (or the in-repo code with "head") and run it directly.
# Usage: against.sh <repro> <version|head> [feed]
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
P=$SCRATCH/proofs3027b
REPRO="$1"; VERSION="$2"
if [ -n "$3" ]; then export RestoreAdditionalProjectSources="$3"; fi
OUT="$P/against/$REPRO-$VERSION"
rm -rf "$OUT"; mkdir -p "$OUT"
cd $REPO/.claude/worktrees/agent-af1c17944ad1df782/LiteDB.ReproRunner/Repros/$REPRO || exit 1
if [ "$VERSION" = head ]; then PROPS="-p:UseProjectReference=true"; else PROPS="-p:UseProjectReference=false -p:LiteDBPackageVersion=$VERSION"; fi
dotnet build "$REPRO.csproj" -c Release $PROPS -p:OutputPath="$OUT/" > "$OUT/build.log" 2>&1 || { echo "$REPRO $VERSION: BUILD FAILED"; grep -m5 'error' "$OUT/build.log"; exit 1; }
(cd "$OUT" && timeout 300 dotnet "$OUT/$REPRO.dll" > "$OUT/run.log" 2>&1)
code=$?
echo "$REPRO against LiteDB $VERSION: exit $code"
grep -o '"type":"log"[^}]*"text":"[^"]*"' "$OUT/run.log" | sed 's/.*"text":/  log /' | cut -c1-700
grep -o '"type":"result"[^}]*"text":"[^"]*"' "$OUT/run.log" | sed 's/.*"text":/  RESULT /'
