import os, subprocess, pathlib, time, shutil, hashlib, json
root=pathlib.Path('__WORKSPACE__/temporary/pr133-facade-late-proof')
out=pathlib.Path('__WORKSPACE__/temporary/pr133-facade-late-retained');out.mkdir(exist_ok=True)
log=out/'runner.log'
env=dict(os.environ,RestoreAdditionalProjectSources='__WORKSPACE__/temporary/pr133-e821-knownbad-feed')
cmd=['dotnet','run','--project','LiteDB.ReproRunner/LiteDB.ReproRunner.Cli','--','run','Issue_3067_LateCallbackClose','--report',str(out/'report.json')]
with log.open('w') as handle:
 p=subprocess.Popen(cmd,cwd=root,env=env,stdout=handle,stderr=subprocess.STDOUT)
 copied=False
 while p.poll() is None:
  if not copied and 'starting execution' in log.read_text():
   runs=root/'LiteDB.ReproRunner/LiteDB.ReproRunner.Cli/bin/Debug/net10.0/runs/Issue_3067_LateCallbackClose'
   if not runs.exists():
    hits=list((root/'LiteDB.ReproRunner/LiteDB.ReproRunner.Cli/bin').glob('**/runs/Issue_3067_LateCallbackClose'))
    if len(hits)!=1: raise RuntimeError(str(hits))
    runs=hits[0]
   for build in runs.glob('*/build'):
    shutil.copytree(build,out/build.parent.name/'build',dirs_exist_ok=True)
   copied=True
  time.sleep(.05)
 if p.returncode!=0: raise RuntimeError('proof failed '+str(p.returncode))
 if not copied: raise RuntimeError('No exact binaries retained')
files={str(p.relative_to(out)):hashlib.sha256(p.read_bytes()).hexdigest() for p in out.rglob('*') if p.is_file()}
(out/'sha256.json').write_text(json.dumps(files,indent=2))
print(out)
