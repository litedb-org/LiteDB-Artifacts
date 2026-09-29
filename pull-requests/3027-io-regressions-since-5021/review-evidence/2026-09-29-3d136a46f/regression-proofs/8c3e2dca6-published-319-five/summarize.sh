#!/bin/bash
# The result job's "Summarize tests added and proofs proven" step, on the local reports.
P=$SCRATCH/proofs
cd $REPO/.claude/worktrees/agent-aaeaca9f570479c49 || exit 1
python3 .github/scripts/pr_evidence.py summarize --base 5dd942a7367c361fadd600be4ce10aace2768b27 \
  --head-sha "$1" --pr 3027 --matrix "$(cat "$P/matrix.json")" --reports "$P/artifacts" \
  --labels '["bug"]' --output "$P/pr-evidence.json"
cat "$P/pr-evidence.json"; echo
