from pathlib import Path
import re,json,hashlib,subprocess
root=Path(__file__).resolve().parent
checkout=root.parent/'safety-production'
head=subprocess.check_output(['git','-C',str(checkout),'rev-parse','HEAD'],text=True).strip()
tree=subprocess.check_output(['git','-C',str(checkout),'rev-parse','HEAD:LiteDB'],text=True).strip()
assert head=='e8559b642b34449e0843c9e74860a3eb5817d0c7'
assert tree=='4c82e64ec435862987ccb0d1061dc6eadc34510f'
results=[]
for tfm,runtime in [('net8.0','.NET 8.0.30'),('net10.0','.NET 10.0.11')]:
 d=root/('final-production-'+tfm)
 s=(d/'results.log').read_text()
 outcomes=re.findall(r'result=(\w+) elapsedMs=(\d+)',s)
 assert len(outcomes)==10
 assert all(kind=='InvalidOperationException' for kind,_ in outcomes)
 assert 'unexpected-' not in s
 assert s.count('localReaders=1 noPin=True')==4
 assert s.count('callback operations=1 holds=0')==4
 assert s.count('reader-callback-count=1')==4
 assert s.count('cold ordinary=1 rows=2')==4
 assert s.count('reader-cold rows=2')==4
 assert s.count('prefetch read=True leased=True')==2
 assert 'assembly=0.0.0-detached+'+head in s
 assert 'runtime='+runtime in s
 assert 'testing-hook-absent=True' in s
 binary=d/'bin'/'Release'/tfm/'LiteDB.dll'
 digest=hashlib.sha256(binary.read_bytes()).hexdigest()
 assert digest==hashlib.sha256((checkout/'LiteDB'/'bin'/'Release'/tfm/'LiteDB.dll').read_bytes()).hexdigest()
 results.append({'target':tfm,'runtime':runtime,'cases':10,'elapsed_ms':[int(ms)for _,ms in outcomes],'checks_passed':True,'dll_sha256':digest,'testing_hook_absent':True,'log':str(d.relative_to(root)/'results.log')})
report={'head':head,'production_tree':tree,'configuration':'Release TestingEnabled=false','oracle':'Exact outcome and branch/cold-state marker parsing; console exit0 alone does not establish success.','source_change':'Final probe adds only assembly/runtime/test-hook identity checks to frozen baseline ten-case source.','results':results}
(root/'final-production-validation.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
