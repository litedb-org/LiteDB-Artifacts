from pathlib import Path
import json,hashlib,statistics,math,subprocess,datetime
root=Path.cwd();base=root/'artifacts_temp';problems=[];checks=[];cases=[];totals={'processes':0,'windows':0};platforms=set();hashes={}
def test(ok,label):
 if not ok:problems.append(label)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def eq(x,y,label):test(math.isclose(x,y,rel_tol=1e-12,abs_tol=1e-10),label)
def git(*args):return subprocess.check_output(['git','-C',str(base/'integration'),*args],text=True).strip()
for campaign in ['attribution','reuse']:
 before=json.loads((base/f'final9-{campaign}-binaries-before.json').read_text());after=json.loads((base/f'final9-{campaign}-binaries-after.json').read_text());test(before==after,'before/after inventory '+campaign)
 for name,h in before.items():
  actual=sha(base/name);test(actual==h,'binary hash '+name);hashes[name]=actual
 p=base/f'final9-{campaign}-results';m=json.loads((p/'manifest.json').read_text());conf=json.loads((base/f'final9-{campaign}-benchmark-config.json').read_text());test(m['configuration']==conf,'configuration '+campaign)
 test(m['driver_sha256']==sha(base/'integration/scripts/benchmark-transaction-handle-steady.py'),'driver hash '+campaign)
 author=json.loads((base/f'final9-{campaign}-summary.json').read_text());names=[];starts=[];recidx=0
 for g in conf['groups']:
  for scenario in g['cases']:
   key='-'.join(map(str,scenario));versionstats={};rates={};rawbyversion={}
   for n in range(g['rounds']):
    order=g['versions'] if n%2==0 else list(reversed(g['versions']))
    for pos,version in enumerate(order):
     definition=conf['versions'][version];name=f'{g["name"]}-{key}-{n}-{pos}-{version}';names.append(name);rec=m['runs'][recidx];recidx+=1;starts.append(datetime.datetime.fromisoformat(rec['started']))
     invocation=list(scenario);invocation[1]=definition.get('mode',invocation[1]);cmd=['dotnet',definition['runner'],definition['revision'],'steady',*map(str,invocation),str(g['warmup']),str(g['windows'])]
     test(rec['name']==name and rec['command']==cmd and rec['exit']==0 and rec['tiered']=='0','manifest '+name)
     rows=[json.loads(line) for line in (p/(name+'.jsonl')).read_text().splitlines()];test([x['phase'] for x in rows]==['metadata']+['window']*g['windows']+['verified'],'phase sequence '+name);meta=rows[0];w=rows[1:-1]
     test(meta['dll']==str(Path(definition['runner']).parent/'LiteDB.dll'),'loaded DLL path '+name);test(meta['sha256']==hashes[str(Path(meta['dll']).relative_to(base))],'loaded DLL hash '+name)
     test(meta['warmupSeconds']==g['warmup'] and meta['windows']==g['windows'] and meta['tiered']=='0','measurement dimensions '+name);platforms.add((meta['runtime'],meta['os'],meta['architecture']))
     for row in rows:test(row['revision']==definition['revision'],'revision '+name)
     for row in rows[:-1]:test([row['shared'],row['mode'],row['operation'],row['reads']]==[scenario[0]=='shared',invocation[1],scenario[2],scenario[3]],'scenario '+name)
     test([x['window'] for x in w]==list(range(g['windows'])),'window ids '+name);test(not(p/(name+'.stderr')).read_text(),'stderr '+name)
     for row in w:
      test(row['count']>0 and row['elapsed']>=1 and row['sampled']==min(row['count'],250000),'window shape '+name);eq(row['opsPerSecond'],row['count']/row['elapsed'],'raw rate '+name);eq(row['readsPerSecond'],row['count']*scenario[3]/row['elapsed'],'raw read rate '+name);test(0<=row['p50us']<=row['p95us']<=row['p99us'] and row['bytesPerOp']>=0,'raw metrics '+name)
     count=sum(x['count'] for x in w);rate=count/sum(x['elapsed'] for x in w);alloc=sum(x['count']*x['bytesPerOp'] for x in w)/count;rates[version,n]=rate
     item={'name':name,'opsPerSecond':rate,'bytesPerOp':alloc,'windowOpsMin':min(x['opsPerSecond'] for x in w),'windowOpsMax':max(x['opsPerSecond'] for x in w),'allLatenciesSampled':all(x['count']==x['sampled'] for x in w)}
     versionstats.setdefault(version,[]).append(item);rawbyversion.setdefault(version,[]).extend(w);totals['processes']+=1;totals['windows']+=len(w)
   summaries={}
   for v,items in versionstats.items():
    r=[x['opsPerSecond'] for x in items];w=rawbyversion[v]
    summaries[v]={'medianOps':statistics.median(r),'minOps':min(r),'maxOps':max(r),'medianBytesPerOp':statistics.median(x['bytesPerOp'] for x in items),'medianWindowP95us':statistics.median(x['p95us'] for x in w),'medianWindowP99us':statistics.median(x['p99us'] for x in w),'runs':items}
   ratios={v:[rates['final',n]/rates[v,n] for n in range(g['rounds'])] for v in g['versions'] if v!='final'}
   result={'campaign':campaign,'group':g['name'],'case':key,'versions':summaries,'pairedFinalOverBaseline':ratios,'medianPairedRatios':{v:statistics.median(x) for v,x in ratios.items()}}
   a=next(x for x in author if x['case']==key);test(a['group']==g['name'],'summary group '+key)
   for v,s in summaries.items():
    for metric,value in s.items():
     if metric=='runs':
      test(len(a['versions'][v]['runs'])==len(value),'summary run count '+key)
      for computed,given in zip(value,a['versions'][v]['runs']):
       for name,c in computed.items():
        if isinstance(c,(str,bool)):test(c==given[name],'summary per-run '+key+name)
        else:eq(c,given[name],'summary per-run '+key+name)
     else:eq(value,a['versions'][v][metric],'summary '+key+metric)
   if len(g['versions'])==2:
    test(len(a['pairedRatiosSecondOverFirst'])==len(ratios['head']),'summary ratio length '+key)
    for x,y in zip(a['pairedRatiosSecondOverFirst'],ratios['head']):eq(x,y,'summary paired ratio '+key)
    eq(a['medianPairedRatio'],statistics.median(ratios['head']),'summary median ratio '+key)
   else:test(a['pairedRatiosSecondOverFirst']==[] and a['medianPairedRatio'] is None,'summary multiversion ratios '+key)
   cases.append(result)
 test(names==[x['name'] for x in m['runs']],'process order/completeness '+campaign);test(len(list(p.glob('*.jsonl')))==len(names),'unexpected raw runs '+campaign);test(len(list(p.glob('*.stderr')))==len(names),'stderr count '+campaign);test(all(b>a for a,b in zip(starts,starts[1:])),'nonmonotonic starts '+campaign);test(len(author)==len([x for x in cases if x['campaign']==campaign]),'summary case count '+campaign)
 checks.append({'campaign':campaign,'processes':len(names),'windows':len(names)*5,'beforeAfterInventoryEntries':len(before)})
ratios=json.loads((base/'final9-attribution-ratios.json').read_text())
for a in ratios:
 c=next(x for x in cases if x['case']==a['case'])
 for v,values in a['ratios'].items():
  test(len(values)==4,'paired rounds '+a['case'])
  for x,y in zip(values,c['pairedFinalOverBaseline'][v]):eq(x,y,'external paired ratio '+a['case'])
  eq(a['median'][v],c['medianPairedRatios'][v],'external median ratio '+a['case'])
test(totals=={'processes':84,'windows':420},'total count')
identity=json.loads((base/'bench-corrected-parent/identity.json').read_text());test(git('rev-parse',identity['sourceCommit']+'^{tree}')==identity['sourceTree'],'parent source tree');test(git('rev-parse',identity['sourceCommit']+':LiteDB')==identity['libraryTree'],'parent library tree')
for x in identity['files']:test(sha(Path(x['path']))==x['sha256'],'parent identity file '+x['path'])
sources=[]
for name,revision,runner in [('final9','52dd7f5797eb093d895029b2cba3e01b99f181dd','bench-final9'),('fixeddev','fad082daf2283f6844fcef2af379a3e0d5c02d59','bench-fixeddev'),('corrected-parent','8a630b26be3fde62030c80c7e6f5680c1fb3341c','bench-corrected-parent/bin')]:
 source=base/f'bench-{name}-source';actual=subprocess.check_output(['git','-C',str(source),'rev-parse','HEAD'],text=True).strip();status=subprocess.check_output(['git','-C',str(source),'status','--short'],text=True);test(actual==revision and not status,'source identity '+name);test(sha(source/'LiteDB/bin/Release/net10.0/LiteDB.dll')==sha(base/runner/'LiteDB.dll'),'source built DLL '+name);sources.append({'revision':actual,'worktree':str(source),'clean':not status,'measuredLibraryMatchesBuild':True})
final='52dd7f5797eb093d895029b2cba3e01b99f181dd';pushed=git('rev-parse','dace941d1');samediff=git('diff',final,pushed,'--','LiteDB','tools/TransactionHandleBenchmarks','scripts/benchmark-transaction-handle-steady.py');test(not samediff,'measured/pushed library or harness source difference')
old={x['path']:x['sha256'] for x in json.loads((base/'final8-benchmark-binaries.json').read_text())}
for name,h in hashes.items():
 if name.startswith('bench-head/'):test(old['artifacts_temp/'+name]==h,'pre-reuse baseline continuity '+name)
report={'status':'pass' if not problems else 'fail','findings':problems,'totals':totals,'campaigns':checks,'platforms':[{'runtime':x[0],'os':x[1],'architecture':x[2]} for x in sorted(platforms)],'verifiedUniqueBinaryFiles':len(hashes),'binaryHashes':hashes,'sourceChecks':sources,'measuredRevision':final,'pushedRevision':pushed,'measuredPushedProductionAndHarnessIdentical':not samediff,'correctedParentIdentityVerified':identity,'summaryComparisons':'All scalar metrics, process metrics, window percentile medians, supplied paired ratios and medians independently recomputed and matched.','cases':cases,'limitations':['Corrected stacked parent 8a630b is a local manually merged benchmark baseline 49c + fad, not original parent or CI-qualified release.','Corrected dev fad is a safety-branch revision; do not label as an unchanged upstream release.','Parent focused production net8 teardown proof is retained evidence; this review verified source/proof-binary identity, did not rerun proof, and it does not establish full safety/net10 qualification.','Read/open steady state only. No new write/contention/durability or retained-memory claims.','Four fresh processes per version/case are replicates; five windows per process are not independent replicates.','Three-version forward/reverse order balances endpoints; parent remains middle.','Differences include all source/merge changes, so neither baseline comparison isolates individual handle/guard cost.','Retained build configuration declares production Release TestingEnabled=false; no build or test execution by independent reviewer.']}
(base/'final9-independent-audit.json').write_text(json.dumps(report,indent=2)+'\n')
lines=['# Independent corrected-baseline benchmark audit','',f'**{report["status"].upper()}**: 84 processes and 420 windows, all complete with exit zero and final cold verification. No raw-data, configuration, process-order, summary, stderr or binary-identity mismatch found.','',f'All {len(hashes)} distinct binary/runtime files match before/after manifests and current hashes. Isolated final, corrected-dev and corrected-parent source/build identities match the measured libraries. The reused pre-reuse baseline matches its preceding inventory. Library and harness source at measured 52dd7f579 is identical to pushed dace941d1.','', 'Runtime/platform: .NET 10.0.11, Ubuntu 24.04.3 LTS, x64. Tiering disabled; 5-second warmup, five one-second windows and four fresh processes per case/version.','', '| Case | Final median ops/s | Final median weighted B/op | Median paired final/baseline gain |','| --- | ---: | ---: | --- |']
for c in cases:
 f=c['versions']['final'];comparison='; '.join(v+f' {(n-1)*100:+.2f}%' for v,n in c['medianPairedRatios'].items());lines.append(f'| {c["case"]} | {f["medianOps"]:,.2f} | {f["medianBytesPerOp"]:,.2f} | {comparison} |')
lines+=['','`dev` is corrected safety branch fad082daf; `parent` is local corrected stacked parent 8a630b26b; `head` is pre-reuse c8c0cfab. The JSON retains every independently recomputed per-process metric and every paired ratio.','','## Qualification limits','']+['- '+x for x in report['limitations']]
(base/'final9-independent-audit.md').write_text('\n'.join(lines)+'\n')
print(json.dumps({k:report[k] for k in ['status','findings','totals','platforms','verifiedUniqueBinaryFiles']},indent=2))
for c in cases:print(c['case'],{v:round((x-1)*100,3) for v,x in c['medianPairedRatios'].items()})
