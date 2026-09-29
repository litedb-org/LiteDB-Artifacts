#!/bin/bash
# checks.sh: the workflow's selection and the safety checks against the PR base
cd $REPO/.claude/worktrees/agent-ae3466fb0272766da
echo "== select"; python3 .github/scripts/regression_proof.py select --base 5dd942a73 --labels '["bug"]'; echo "exit $?"
echo "== validate"; python3 .github/scripts/regression_proof.py validate --head HEAD; echo "exit $?"
for c in check_coverage_regression check_fault_points check_contracts; do
  echo "== $c"; python3 .github/scripts/$c.py --base 5dd942a73 --head HEAD 2>&1 | tail -8; echo "exit ${PIPESTATUS[0]}"
done
echo "== size"; python3 scripts/check-csharp-size.py --base 5dd942a73 2>&1 | grep -i "PromotionAfterFailedCheckpoint\|ERROR"; python3 scripts/check-csharp-size.py --base 5dd942a73 > /dev/null 2>&1; echo "exit $?"
