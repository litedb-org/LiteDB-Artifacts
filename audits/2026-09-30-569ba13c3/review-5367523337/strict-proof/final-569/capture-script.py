import os,subprocess,time,shutil,json,hashlib
from pathlib import Path
root=Path('__WORKSPACE__/artifacts_temp/proof-shared-callback-native')
evidence=root/'artifacts_temp/native-callback-proof-569-final';evidence.mkdir()
env=os.environ.copy();env['RestoreAdditionalProjectSources']='__WORKSPACE__/temporary/pr133-12a-knownbad-feed';env['TestingEnabled']='false'
cmd=['dotnet','run','--project','LiteDB.ReproRunner/LiteDB.ReproRunner.Cli','-c','Release','--','run','Issue_3067_SharedCallbackNativeWait','--ci','--report',str(evidence/'proof.json')]
with (evidence/'run.log').open('w') as log:
 p=subprocess.Popen(cmd,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT)
 while p.poll() is None:
  for source in (root/'LiteDB.ReproRunner/LiteDB.ReproRunner.Cli/bin').glob('**/runs/Issue_3067_SharedCallbackNativeWait/*/build'):
   if (source/'Issue_3067_SharedCallbackNativeWait.deps.json').exists():
    dest=evidence/'binaries'/source.parent.name
    try: shutil.copytree(source,dest,dirs_exist_ok=True)
    except FileNotFoundError: pass
  time.sleep(.1)
 code=p.wait()
(evidence/'exit-code.txt').write_text(str(code)+'\n')
if (evidence/'proof.json').exists():
 report=json.loads((evidence/'proof.json').read_text())
 for label in ['Package','Latest']:
  for line in report['Repros'][0][label]['Output']:
   if line['Text'].startswith('DATABASE_ROOT '):
    path=Path(line['Text'].split(' ',1)[1]);shutil.copytree(path,evidence/'databases'/label)
shutil.copytree(root/'LiteDB.ReproRunner/Repros/Issue_3067_SharedCallbackNativeWait', evidence/'source',ignore=shutil.ignore_patterns('bin','obj'))
shutil.copy2('__WORKSPACE__/temporary/pr133-12a-knownbad-feed/LiteDB.0.0.0-knownbad.12a057424843.nupkg',evidence)
(evidence/'provenance.json').write_text(json.dumps({'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'productionTree':subprocess.check_output(['git','rev-parse','HEAD:LiteDB'],cwd=root,text=True).strip(),'command':cmd,'TestingEnabled':False,'knownBad':'12a057424843d54bf10a53318433c889a7db025d'},indent=2)+'\n')
files=sorted(x for x in evidence.rglob('*') if x.is_file())
(evidence/'SHA256SUMS').write_text(''.join(hashlib.sha256(x.read_bytes()).hexdigest()+'  '+str(x.relative_to(evidence))+'\n' for x in files))
print(evidence,code)
raise SystemExit(code)
