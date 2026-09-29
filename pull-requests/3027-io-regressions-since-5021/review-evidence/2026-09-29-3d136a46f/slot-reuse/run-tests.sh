#!/bin/bash
# Usage: run-tests.sh <output-name> <filter> [--build]
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT
cd $REPO/.claude/worktrees/agent-ac6b0cf63f278326a || exit 1
OUT=$SCRATCH/slot/$1.txt
BUILD=--no-build
if [ "$3" == "--build" ]; then BUILD=""; fi
timeout 3000 dotnet test LiteDB.Tests -f net10.0 $BUILD --filter "$2" --logger "console;verbosity=normal" > "$OUT.full" 2>&1
grep -E "^\s+Failed |Passed!|Failed!|Total tests|error CS|Error Message" "$OUT.full" | head -80 > "$OUT"
tail -5 "$OUT.full" >> "$OUT"
cat "$OUT"
