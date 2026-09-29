#!/bin/bash
# Build the repro against a LiteDB package version (or the in-repo code with "head") and run it directly.
# Usage: against.sh <version|head> [feed]
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
P=$SCRATCH/ob3027
REPRO=Issue_3027_OverwriteBehindUnsyncedLog; VERSION="$1"
if [ -n "$2" ]; then export RestoreAdditionalProjectSources="$2"; fi
OUT="$P/against/$VERSION"
rm -rf "$OUT"; mkdir -p "$OUT"
cd $REPO/.claude/worktrees/agent-ae670113747d5f100/LiteDB.ReproRunner/Repros/$REPRO || exit 1
if [ "$VERSION" = head ]; then PROPS="-p:UseProjectReference=true"; else PROPS="-p:UseProjectReference=false -p:LiteDBPackageVersion=$VERSION"; fi
dotnet build "$REPRO.csproj" -c Release $PROPS -p:OutputPath="$OUT/" > "$OUT/build.log" 2>&1 || { echo "$VERSION: BUILD FAILED"; grep -m5 'error' "$OUT/build.log"; exit 1; }
(cd "$OUT" && timeout 300 dotnet "$OUT/$REPRO.dll" > "$OUT/run.log" 2>&1)
code=$?
echo "$REPRO against LiteDB $VERSION: exit $code"
grep -o '"type":"log"[^}]*"text":"[^"]*"' "$OUT/run.log" | sed 's/.*"text":/  log /' | cut -c1-400
grep -o '"type":"result"[^}]*"text":"[^"]*"' "$OUT/run.log" | sed 's/.*"text":/  RESULT /'
