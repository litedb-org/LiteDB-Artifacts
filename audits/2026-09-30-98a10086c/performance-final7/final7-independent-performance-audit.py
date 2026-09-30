import json,hashlib,statistics,math,datetime,subprocess
from pathlib import Path
root=Path('__WORKSPACE__'); a=root/'artifacts_temp'; errors=[]; results={};run_count=window_count=0

def check(ok,msg):
 if not ok:errors.append(msg)
def close(x,y,label):check(math.isclose(x,y,rel_tol=1e-12,abs_tol=1e-8),f'{label}: {x} != {y}')
inventory=json.loads((a/'final7-benchmark-binaries.json').read_text());check(len(inventory)==40,'inventory count')
for item in inventory:
 p=root/item['path'];check(p.stat().st_size==item['bytes'],f'size {p}');check(hashlib.sha256(p.read_bytes()).hexdigest()==item['sha256'],f'hash {p}')
for campaign in ['reuse','upstream']:
 cfg=json.loads((a/f'final7-{campaign}-benchmark-config.json').read_text());folder=a/f'final7-{campaign}-benchmark';manifest=json.loads((folder/'manifest.json').read_text());check(manifest['configuration']==cfg,'embedded config')
 expected=[];bycase={};runs={};starts=[]
 for group in cfg['groups']:
  for scenario in group['cases']:
   case='-'.join(map(str,scenario));bycase[case]={}
   for round in range(group['rounds']):
    for position,version in enumerate(group['versions'][::1 if round%2==0 else -1]):
     name=f'{group["name"]}-{case}-{round}-{position}-{version}';expected.append(name)
     rows=[json.loads(line) for line in (folder/(name+'.jsonl')).read_text().splitlines()];check([r['phase'] for r in rows]==['metadata']+['window']*5+['verified'],f'phases {name}')
     check((folder/(name+'.stderr')).read_text()=='',f'stderr {name}')
     meta=rows[0];windows=rows[1:-1];run_count+=1;window_count+=len(windows)
     check(meta['runtime']=='10.0.11' and meta['os']=='Ubuntu 24.04.3 LTS' and meta['architecture']=='X64' and meta['tiered']=='0',f'env {name}')
     check(meta['warmupSeconds']==5 and meta['windows']==5,f'dim {name}')
     check(meta['revision']==cfg['versions'][version]['revision'] and rows[-1]['revision']==meta['revision'],f'revision {name}')
     dll=Path(meta['dll']);check(dll==Path(cfg['versions'][version]['runner']).parent/'LiteDB.dll',f'dllpath {name}');check(hashlib.sha256(dll.read_bytes()).hexdigest()==meta['sha256'],f'loaded hash {name}')
     for ix,w in enumerate(windows):
      check(w['window']==ix and w['count']>0 and w['elapsed']>=1 and w['sampled']==w['count'],f'windowcount {name} {ix}')
      check(w['shared']==(scenario[0]=='shared') and w['mode']==scenario[1] and w['operation']==scenario[2] and w['reads']==scenario[3],f'shape {name}')
      close(w['count']/w['elapsed'],w['opsPerSecond'],f'windowrate {name}')
     ops=sum(w['count'] for w in windows)/sum(w['elapsed'] for w in windows);alloc=sum(w['count']*w['bytesPerOp'] for w in windows)/sum(w['count'] for w in windows)
     runs[name]={'ops':ops,'bytes':alloc};bycase[case].setdefault(version,{})[round]=runs[name]
 check([r['name'] for r in manifest['runs']]==expected,f'order {campaign}')
 check(set(p.stem for p in folder.glob('*.jsonl'))==set(expected),f'extraneous/missing raw {campaign}')
 for r in manifest['runs']:
  check(r['exit']==0 and r['tiered']=='0',f'runresult {r["name"]}');starts.append(datetime.datetime.fromisoformat(r['started']))
 check(starts==sorted(starts) and len(set(starts))==len(starts),f'timestamps {campaign}')
 summary=json.loads((a/f'final7-{campaign}-summary.json').read_text());paired=json.loads((a/f'final7-{campaign}-paired.json').read_text());pair_lookup={x['case']:x for x in paired};out=[]
 for s in summary:
  case=s['case'];vals=bycase[case];entry={'case':case,'versions':{},'paired':{}}
  for v,rr in vals.items():
   mops=statistics.median(x['ops'] for x in rr.values());mbytes=statistics.median(x['bytes'] for x in rr.values());entry['versions'][v]={'ops':mops,'bytes':mbytes}
   close(mops,s['versions'][v]['medianOps'],f'median rate {case}/{v}');close(mbytes,s['versions'][v]['medianBytesPerOp'],f'median bytes {case}/{v}')
   close(mops,pair_lookup[case]['versions'][v]['ops'],f'paired displayed rate {case}/{v}');close(mbytes,pair_lookup[case]['versions'][v]['bytes'],f'paired displayed bytes {case}/{v}')
   for run in s['versions'][v]['runs']:
    close(runs[run['name']]['ops'],run['opsPerSecond'],f'processrate {run["name"]}');close(runs[run['name']]['bytes'],run['bytesPerOp'],f'processbytes {run["name"]}')
  for v in vals:
   if v=='final':continue
   rat=[vals['final'][i]['ops']/vals[v][i]['ops'] for i in range(4)];ar=[vals['final'][i]['bytes']/vals[v][i]['bytes'] for i in range(4)];m=statistics.median(rat);p=pair_lookup[case]['paired'][v]
   for x,y in zip(rat,p['ratios']):close(x,y,f'pairedratio {case}/{v}')
   close(m,p['median'],f'pairedmedian {case}/{v}');close(100*(m-1),p['percent'],f'pairedpct {case}/{v}')
   close(100*(1-entry['versions']['final']['bytes']/entry['versions'][v]['bytes']),p['allocationMedianReductionPercent'],f'allocmedianratio {case}/{v}')
   for x,y in zip(ar,p['pairedAllocationRatios']):close(x,y,f'pairedalloc {case}/{v}')
   close(100*(1-statistics.median(ar)),p['pairedAllocationReductionPercent'],f'pairedallocmedian {case}/{v}')
   entry['paired'][v]={'median':m,'percent':100*(m-1),'min':min(rat),'max':max(rat),'ratios':rat}
  out.append(entry)
 results[campaign]=out
check(run_count==116 and window_count==580,'total')
prod=subprocess.check_output(['git','rev-parse','e8559b642b34449e0843c9e74860a3eb5817d0c7:LiteDB'],cwd=root,text=True).strip();check(prod=='4c82e64ec435862987ccb0d1061dc6eadc34510f','prod tree')
report={'independent':True,'measuredSource':'e8559b642b34449e0843c9e74860a3eb5817d0c7','productionTree':prod,'processes':run_count,'windows':window_count,'actualFilesHashed':len(inventory),'allArithmeticChecksFromRaw':not errors,'errors':errors,'recomputed':results}
(a/'final7-independent-performance-review.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({'processes':run_count,'windows':window_count,'files':len(inventory),'errors':errors},indent=2));assert not errors
