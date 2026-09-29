#!/bin/bash
# Prove every proof of the PR in turn; summary in prove-all.log.
P=$SCRATCH/proofs
: > "$P/prove-all.log"
for repro in "$@"; do
  echo "=== $repro" >> "$P/prove-all.log"
  bash "$P/prove.sh" "$repro" >> "$P/prove-all.log" 2>&1
  python3 - "$P/reports/$repro.json" >> "$P/prove-all.log" <<'EOF'
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
