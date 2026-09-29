#!/bin/bash
# loop.sh <prefix> <n> : n CLI runs, summarize each
D=$SCRATCH/promo-proof
cd $REPO/.claude/worktrees/agent-ae3466fb0272766da
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
for i in $(seq 1 $2); do
  start=$(date +%s)
  timeout 900 dotnet run --project LiteDB.ReproRunner/LiteDB.ReproRunner.Cli -c Release -- run Issue_3027_PromotionAfterFailedCheckpoint --ci --report $D/$1-$i.json > $D/$1-$i.log 2>&1
  code=$?
  echo "== run $i cli exit $code ($(( $(date +%s) - start ))s)"
  python3 - $D/$1-$i.json <<'PY'
import json,sys
d=json.load(open(sys.argv[1]))
for r in d['Repros']:
  for k in ('Package','Latest'):
    v=r.get(k) or {}
    res=[json.loads(o['Text']) for o in v.get('Output',[]) if o['Text'].startswith('{')]
    ver=[x['text'].split(' loaded')[0] for x in res if x.get('type')=='log' and x['text'].startswith('LiteDB ')]
    after=[x['text'] for x in res if x.get('type')=='log' and x['text'].startswith('After the failed')]
    result=[x['text'][:60] for x in res if x.get('type')=='result']
    print(f"  {k}: exit={v.get('ExitCode')} met={v.get('Met')} {ver[0][:60] if ver else ''} | {after[0] if after else ''} | {result[0] if result else ''}")
PY
done
