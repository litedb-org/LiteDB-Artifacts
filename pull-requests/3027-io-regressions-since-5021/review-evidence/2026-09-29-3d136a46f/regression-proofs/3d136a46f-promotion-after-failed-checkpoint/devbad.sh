#!/bin/bash
# devbad.sh: cross-check against dev-commit 5dd942a73 packed by pack-known-bad (feed from the earlier proofs session)
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
O=$SCRATCH/promo-proof
FEED=$SCRATCH/proofs/feed
cd $REPO/.claude/worktrees/agent-ae3466fb0272766da
P=LiteDB.ReproRunner/Repros/Issue_3027_PromotionAfterFailedCheckpoint/Issue_3027_PromotionAfterFailedCheckpoint.csproj
cp $P $O/csproj.orig
sed -i 's/>6.0.0-prerelease.319</>0.0.0-knownbad.5dd942a7367c</' $P
grep -n knownbad $P
export RestoreAdditionalProjectSources=$FEED
for i in 1 2 3 4 5; do
  timeout 900 dotnet run --project LiteDB.ReproRunner/LiteDB.ReproRunner.Cli -c Release -- run Issue_3027_PromotionAfterFailedCheckpoint --ci --report $O/devbad-$i.json > $O/devbad-$i.log 2>&1
  echo "== devbad run $i cli exit $?"
  python3 .github/scripts/regression_proof.py verify --report $O/devbad-$i.json --repro Issue_3027_PromotionAfterFailedCheckpoint --expect-version 0.0.0-knownbad.5dd942a7367c; echo "verify exit $?"
done
dotnet build $P -c Release -o $O/bin-devbad -v q -nologo 2>&1 | tail -2
cp $O/csproj.orig $P
git diff --stat -- $P
