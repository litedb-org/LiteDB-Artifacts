#!/bin/bash
# Run each proof as the "prove" job of .github/workflows/regression-proof.yml does, then verify its report.
# Usage: prove-all.sh <repro>...; summary in prove-all.log.
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
P=$SCRATCH/proofs3027b
W=$REPO/.claude/worktrees/agent-af1c17944ad1df782
cd "$W" || exit 1
mkdir -p "$P/reports"
: > "$P/prove-all.log"
for REPRO in "$@"; do
  echo "=== $REPRO" >> "$P/prove-all.log"
  dotnet run --project LiteDB.ReproRunner/LiteDB.ReproRunner.Cli -c Release -- \
    run "$REPRO" --ci --report "$P/reports/$REPRO.json" > "$P/reports/$REPRO.log" 2>&1
  echo "reprorunner exit=$?" >> "$P/prove-all.log"
  VERSION=$(python3 -c "import json,sys; sys.path.insert(0,'.github/scripts'); import regression_proof as r; print(r.known_bad_version(next(e for e in json.load(open('.github/safety/regression-proofs.json'))['proofs'] if e['repro']=='$REPRO')['knownBad']))")
  python3 .github/scripts/regression_proof.py verify --report "$P/reports/$REPRO.json" --repro "$REPRO" --expect-version "$VERSION" >> "$P/prove-all.log" 2>&1
  echo "verify exit=$?" >> "$P/prove-all.log"
  python3 - "$P/reports/$REPRO.json" >> "$P/prove-all.log" <<'EOF'
import json, sys
data = json.load(open(sys.argv[1], encoding="utf-8-sig"))
for entry in data["Repros"]:
    for variant in ("Package", "Latest"):
        item = entry[variant]
        print(f"  {variant}: exit {item['ExitCode']}, met {item['Met']}, failure {item['FailureReason']}")
        for line in item["Output"]:
            try:
                event = json.loads(line["Text"])
            except ValueError:
                continue
            if event.get("type") in ("result", "configuration"):
                print("    ", event.get("type"), event.get("text") or event.get("payload"))
EOF
done
echo DONE >> "$P/prove-all.log"
