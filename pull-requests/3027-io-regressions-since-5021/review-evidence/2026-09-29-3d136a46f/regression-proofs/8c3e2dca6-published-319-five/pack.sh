#!/bin/bash
# Pack the known-bad dev commit into a local feed, as the Regression proof workflow does.
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
export TMPDIR=$SCRATCH/proofs/tmp
P=$SCRATCH/proofs
cd $REPO/.claude/worktrees/agent-aaeaca9f570479c49 || exit 1
python3 .github/scripts/regression_proof.py pack-known-bad --commit "$1" --feed "$P/feed" > "$P/pack.log" 2>&1
echo "exit=$?" >> "$P/pack.log"
