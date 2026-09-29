#!/bin/bash
# verify.sh <prefix> <n> <version>: verify n ReproRunner reports with the proof script
O=$SCRATCH/promo-proof
cd $REPO/.claude/worktrees/agent-ae3466fb0272766da
for i in $(seq 1 $2); do
  python3 .github/scripts/regression_proof.py verify --report $O/$1-$i.json --repro Issue_3027_PromotionAfterFailedCheckpoint --expect-version $3 > $O/verify-$1-$i.txt 2>&1
  echo "verify $1-$i exit $? : $(head -1 $O/verify-$1-$i.txt)"
done
