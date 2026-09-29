#!/bin/bash
# Run one regression proof as the "prove" job of .github/workflows/regression-proof.yml does:
# ReproRunner builds the known-bad package variant and the candidate source variant, runs both,
# writes the report, then regression_proof.py verifies it. Usage: prove.sh <repro> [feed]
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
P=$SCRATCH/proofs
REPRO="$1"
if [ -n "$2" ]; then export RestoreAdditionalProjectSources="$2"; fi
cd $REPO/.claude/worktrees/agent-aaeaca9f570479c49 || exit 1
mkdir -p "$P/reports"
dotnet run --project LiteDB.ReproRunner/LiteDB.ReproRunner.Cli -c Release -- \
  run "$REPRO" --ci --report "$P/reports/$REPRO.json" > "$P/reports/$REPRO.log" 2>&1
echo "reprorunner exit=$?"
VERSION=$(python3 -c "import json,sys; sys.path.insert(0,'.github/scripts'); import regression_proof as r; print(r.known_bad_version(next(e for e in json.load(open('.github/safety/regression-proofs.json'))['proofs'] if e['repro']=='$REPRO')['knownBad']))")
python3 .github/scripts/regression_proof.py verify --report "$P/reports/$REPRO.json" --repro "$REPRO" --expect-version "$VERSION"
echo "verify exit=$?"
