from pathlib import Path
import json,hashlib,xml.etree.ElementTree as E,collections,subprocess
root=Path('__WORKSPACE__');art=root/'artifacts_temp';report={'candidate':'e8559b642b34449e0843c9e74860a3eb5817d0c7','builtSource':'dded8c934','productionTree':'4c82e64ec435862987ccb0d1061dc6eadc34510f','testTree':'dd85c62c9e11116b8660fc3903afc995e7e71f85','runtimes':{}}
parts=['handles']+[f'shared-{i}' for i in range(4)]+[f'shared-extra-{i}' for i in range(4)]+['other']
for tfm,folder in [('net8.0','safety-net8'),('net10.0','integration')]:
 d=art/('review536885-partitions-'+tfm);expected=[x.strip() for x in (d/'full-discovery.log').read_text().splitlines() if x.strip().startswith('LiteDB.')];found=[];discovered=[];runs=[]
 for part in parts:
  discovery=[x.strip() for x in (d/(part+'-discovery.log')).read_text().splitlines() if x.strip().startswith('LiteDB.')];discovered+=discovery
  p=art/folder/'LiteDB.Tests/TestResults'/f'review536885-{part}-{tfm}.trx';r=E.parse(p).getroot();ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
  rows=r.findall('.//t:UnitTestResult',ns);outcomes=collections.Counter(x.attrib['outcome'] for x in rows);names=[x.attrib['testName'] for x in rows];found+=names
  assert r.find('t:ResultSummary',ns).attrib['outcome']=='Completed',(tfm,part,'incomplete')
  assert outcomes=={'Passed':len(rows)} and rows,(tfm,part,outcomes)
  assert collections.Counter(names)==collections.Counter(discovery),(tfm,part,'discovery mismatch',set(names)-set(discovery),set(discovery)-set(names))
  runs.append({'partition':part,'count':len(rows),'outcomes':dict(outcomes),'trx':str(p.relative_to(root)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
 assert collections.Counter(discovered)==collections.Counter(expected),(tfm,'union mismatch')
 assert len(discovered)==len(set(discovered)),(tfm,'duplicates')
 assert collections.Counter(found)==collections.Counter(expected),(tfm,'results mismatch')
 bins=[]
 for name in ['LiteDB.dll','LiteDB.pdb','LiteDB.Tests.dll','LiteDB.Tests.pdb','LiteDB.Tests.runtimeconfig.json']:
  p=art/folder/'LiteDB.Tests/bin/Release'/tfm/name;bins.append({'path':str(p.relative_to(root)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':p.stat().st_size})
 report['runtimes'][tfm]={'expected':len(expected),'passed':len(found),'failures':0,'skips':0,'duplicateCases':0,'missingCases':0,'extraCases':0,'runs':runs,'binaries':bins}
print(json.dumps({k:{x:y for x,y in v.items() if x not in ['runs','binaries']} for k,v in report['runtimes'].items()},indent=2));(art/'review536885-partition-validation.json').write_text(json.dumps(report,indent=2)+'\n')
