from pathlib import Path
import json,hashlib,statistics,math,subprocess,datetime
root=Path.cwd(); issues=[]; counts={'runs':0,'windows':0}; results=[];runtime=set();oses=set();arch=set();filechecks=[]
def check(ok,message):
 if not ok: issues.append(message)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
inventory=json.loads((root/'artifacts_temp/final8-benchmark-binaries.json').read_text()); inv={x['path']:x for x in inventory}
for x in inventory:
 p=root/x['path'];actual=sha(p);check(actual==x['sha256'] and p.stat().st_size==x['bytes'],'inventory mismatch '+x['path']);filechecks.append({'path':x['path'],'sha256':actual,'matches':actual==x['sha256']})
old={x['path']:x for x in json.loads((root/'artifacts_temp/final7-benchmark-binaries.json').read_text())}
reused=[]
for x in inventory:
 if any('/'+v+'/' in x['path'] for v in ['bench-head','bench-parent','bench-final7']):
  check(x['path'] in old and x==old[x['path']],'reused binary differs from final7 '+x['path']);reused.append(x['path'])
driver=root/'artifacts_temp/integration/scripts/benchmark-transaction-handle-steady.py'
for campaign in ['reuse','upstream']:
 p=root/f'artifacts_temp/final8-{campaign}-benchmark';m=json.loads((p/'manifest.json').read_text());config=json.loads((root/f'artifacts_temp/final8-{campaign}-benchmark-config.json').read_text());check(m['configuration']==config,'config mismatch '+campaign);check(m['driver_sha256']==sha(driver),'driver mismatch '+campaign)
 expected=[];allstarts=[]
 for g in config['groups']:
  for case in g['cases']:
   key='-'.join(map(str,case));perversion={};byround={}
   for repeat in range(g['rounds']):
    order=g['versions'] if repeat%2==0 else list(reversed(g['versions']))
    for pos,v in enumerate(order):
     name=f'{g["name"]}-{key}-{repeat}-{pos}-{v}';expected.append(name);definition=config['versions'][v];scenario=list(case);scenario[1]=definition.get('mode',scenario[1]);command=['dotnet',definition['runner'],definition['revision'],'steady',*map(str,scenario),str(g['warmup']),str(g['windows'])]
     rec=m['runs'][len(expected)-1];check(rec['name']==name and rec['command']==command and rec['exit']==0 and rec['tiered']=='0','manifest mismatch '+name);allstarts.append(datetime.datetime.fromisoformat(rec['started']))
     path=p/(name+'.jsonl');rows=[json.loads(line) for line in path.read_text().splitlines()];check([x['phase'] for x in rows]==['metadata']+['window']*g['windows']+['verified'],'phase sequence '+name);meta=rows[0];w=rows[1:-1]
     check(meta['revision']==definition['revision'] and meta['dll']==str(Path(definition['runner']).parent/'LiteDB.dll'),'revision/library path '+name)
     check(meta['sha256']==inv[str(Path(meta['dll']).relative_to(root))]['sha256'],'loaded library hash '+name)
     check(meta['warmupSeconds']==g['warmup'] and meta['windows']==g['windows'] and meta['tiered']=='0','dimensions '+name)
     for row in rows[:-1]:
      check((row['shared'],row['mode'],row['operation'],row['reads'])==(case[0]=='shared',scenario[1],case[2],case[3]),'scenario metadata '+name)
      check(row['revision']==definition['revision'],'window revision '+name)
     check(rows[-1]['revision']==definition['revision'],'verified revision '+name)
     runtime.add(meta['runtime']);oses.add(meta['os']);arch.add(meta['architecture'])
     check([x['window'] for x in w]==list(range(g['windows'])),'window ids '+name)
     check(not(p/(name+'.stderr')).read_text(),'stderr nonempty '+name)
     for row in w:
      check(row['count']>0 and row['elapsed']>=1 and row['sampled']==min(row['count'],250000),'window size '+name)
      check(math.isclose(row['opsPerSecond'],row['count']/row['elapsed'],rel_tol=1e-12),'window throughput '+name)
      check(math.isclose(row['readsPerSecond'],row['count']*case[3]/row['elapsed'],rel_tol=1e-12),'window read rate '+name)
      check(0<=row['p50us']<=row['p95us']<=row['p99us'] and row['bytesPerOp']>=0,'window metrics '+name)
     count=sum(x['count'] for x in w);elapsed=sum(x['elapsed'] for x in w);rate=count/elapsed;allocation=sum(x['count']*x['bytesPerOp'] for x in w)/count
     item={'name':name,'round':repeat,'opsPerSecond':rate,'weightedBytesPerOp':allocation,'windowCount':len(w),'allLatenciesSampled':all(x['sampled']==x['count'] for x in w),'medianWindowP95us':statistics.median(x['p95us'] for x in w),'medianWindowP99us':statistics.median(x['p99us'] for x in w)}
     perversion.setdefault(v,[]).append(item);byround[v,repeat]=item;counts['runs']+=1;counts['windows']+=len(w)
   summaries={v:{'medianOpsPerSecond':statistics.median(x['opsPerSecond'] for x in rs),'minOpsPerSecond':min(x['opsPerSecond'] for x in rs),'maxOpsPerSecond':max(x['opsPerSecond'] for x in rs),'medianWeightedBytesPerOp':statistics.median(x['weightedBytesPerOp'] for x in rs),'processes':rs} for v,rs in perversion.items()}
   ratios={}
   for v in g['versions']:
    if v=='final':continue
    pairs=[byround['final',n]['opsPerSecond']/byround[v,n]['opsPerSecond'] for n in range(g['rounds'])]
    ratios[v]={'pairedFinalOverBaseline':pairs,'medianPairedRatio':statistics.median(pairs),'medianRateRatio':summaries['final']['medianOpsPerSecond']/summaries[v]['medianOpsPerSecond'],'allocationDifferenceFinalMinusBaseline':summaries['final']['medianWeightedBytesPerOp']-summaries[v]['medianWeightedBytesPerOp']}
   results.append({'group':g['name'],'case':case,'versions':summaries,'comparisons':ratios})
 check([x['name'] for x in m['runs']]==expected,'missing/unexpected/order runs '+campaign)
 check(len(list(p.glob('*.jsonl')))==len(expected),'unexpected raw files '+campaign)
 check(all(b>a for a,b in zip(allstarts,allstarts[1:])),'nonmonotonic process starts '+campaign)
check(counts=={'runs':116,'windows':580},'total mismatch')
provenance=[]
for name,revision in [('final8','3abead6dc2eb6413daf5fa81d8faed06cdf4ade7'),('dev3075','023c2b4ba8ffe637c955092ff05289d90eafdfb4')]:
 source=root/f'artifacts_temp/bench-{name}-source';head=subprocess.check_output(['git','-C',str(source),'rev-parse','HEAD'],text=True).strip();status=subprocess.check_output(['git','-C',str(source),'status','--short'],text=True);check(head==revision and not status,'source worktree identity '+name)
 lib=source/'LiteDB/bin/Release/net10.0/LiteDB.dll';runnerlib=root/f'artifacts_temp/bench-{name}/LiteDB.dll';check(sha(lib)==sha(runnerlib),'isolated source library mismatch '+name)
 logs=[]
 for kind in ['library','runner']:
  path=root/f'artifacts_temp/{name}-{kind}-build.log';txt=path.read_text();check('Build succeeded.' in txt,'build log failure '+str(path));logs.append({'path':str(path.relative_to(root)),'sha256':sha(path)})
 provenance.append({'version':name,'revision':head,'cleanWorktree':not status,'isolatedReleaseNet10LibraryMatches':True,'buildLogs':logs,'testingEnabled':False,'testingEnabledEvidence':'Build owner supplied explicit TestingEnabled=false; logs record successful Release/net10 build but do not echo compiler properties.'})
source_diff=subprocess.check_output(['git','-C',str(root/'artifacts_temp/integration'),'diff','e8559b642b34449e0843c9e74860a3eb5817d0c7','3abead6dc2eb6413daf5fa81d8faed06cdf4ade7','--','tools/TransactionHandleBenchmarks','scripts/benchmark-transaction-handle-steady.py','scripts/summarize-transaction-handle-steady.py'],text=True);check(not source_diff,'runner source changed')
report={'status':'pass' if not issues else 'fail','issues':issues,'counts':counts,'runtime':sorted(runtime),'os':sorted(oses),'architecture':sorted(arch),'tieredCompilation':'0','warmupSeconds':5,'windowsPerProcess':5,'rounds':4,'inventoryFilesVerified':len(filechecks),'reusedFinal7FilesVerified':len(reused),'binaryChecks':filechecks,'provenance':provenance,'runnerSourceUnchangedSinceE855':not source_diff,'cases':results,'limitations':['Read/open steady state only; no new write/contention/crash-durability or retained-memory claim.','Each process is a replicate; its five windows are not independent replicates.','Read0 measures begin/get-collection/commit. Ordinary and legacy/handle have different transaction semantics.','Latency samples are first250000 operations/window; median window percentile is not a pooled percentile.','Version groups alternate forward/reverse; every baseline occupies balanced early/late position, but paired rounds are not simultaneous measurements.','Revision is caller-supplied metadata, cross-checked against inventory/source build; explicit compiler property provenance comes from build owner.']}
commands_path=root/'artifacts_temp/final8-build-commands.json'
commands=json.loads(commands_path.read_text())
for command in commands:
 check('-p:TestingEnabled=false' in command['libraryCommand'] and '-p:TestingEnabled=false' in command['runnerCommand'],'production command property missing')
 check('net10.0' in command['libraryCommand'] and 'Release' in command['libraryCommand'],'library build target mismatch')
report['buildCommands']={'path':str(commands_path.relative_to(root)),'sha256':sha(commands_path),'productionFlagsPresent':True}
for item in report['provenance']:
 item['testingEnabledEvidence']='Recorded exact build commands specify TestingEnabled=false; successful logs, clean revision-pinned worktree and matching source/output library hashes corroborate the production build.'
report['limitations'][-1]='Revision is caller-supplied metadata, cross-checked against inventory/source builds and retained exact production build commands.'
author_checks=0
for campaign in ['reuse','upstream']:
 author=json.loads((root/f'artifacts_temp/final8-{campaign}-paired.json').read_text())
 for a in author:
  own=next(x for x in results if '-'.join(map(str,x['case']))==a['case'])
  for version,v in a['versions'].items():
   check(math.isclose(v['ops'],own['versions'][version]['medianOpsPerSecond'],rel_tol=1e-12),'author median rate mismatch')
   check(math.isclose(v['bytes'],own['versions'][version]['medianWeightedBytesPerOp'],rel_tol=1e-12),'author median allocation mismatch')
  for version,v in a['paired'].items():
   check(all(math.isclose(x,y,rel_tol=1e-12) for x,y in zip(v['ratios'],own['comparisons'][version]['pairedFinalOverBaseline'])),'author paired ratios mismatch')
   check(math.isclose(v['median'],own['comparisons'][version]['medianPairedRatio'],rel_tol=1e-12),'author median paired ratio mismatch')
  author_checks+=1
report['authorPairedTablesMatched']=author_checks
report['status']='pass' if not issues else 'fail'
out=root/'artifacts_temp/final8-independent-performance-review.json';out.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({k:report[k] for k in ['status','issues','counts','runtime','os','architecture','inventoryFilesVerified','reusedFinal7FilesVerified']},indent=2))
for x in results:
 print(x['case']);print(' rates/B:',{v:[round(y['medianOpsPerSecond'],2),round(y['medianWeightedBytesPerOp'],2)] for v,y in x['versions'].items()});print(' final paired gains:',{v:round((y['medianPairedRatio']-1)*100,3) for v,y in x['comparisons'].items()})
