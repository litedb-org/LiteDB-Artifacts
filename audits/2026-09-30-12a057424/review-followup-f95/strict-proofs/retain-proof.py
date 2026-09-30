import os,sys,subprocess,pathlib,time,shutil,hashlib,json,datetime
root=pathlib.Path('__WORKSPACE__/temporary/pr133-reader-retirement-proofs'); name=sys.argv[1]
base=pathlib.Path('__WORKSPACE__/temporary/pr133-reader-proof-evidence')/name
out=base/(datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S')+'-'+sys.argv[2]);out.mkdir(parents=True)
log=out/'runner.log';env=dict(os.environ,RestoreAdditionalProjectSources='__WORKSPACE__/temporary/pr133-f95-knownbad-feed')
cmd=['dotnet','run','--project','LiteDB.ReproRunner/LiteDB.ReproRunner.Cli','--','run',name,'--report',str(out/'report.json')]
shutil.copytree(root/'LiteDB.ReproRunner/Repros'/name,out/'source',ignore=shutil.ignore_patterns('bin','obj'))
(out/'revision.txt').write_text(subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True))
with log.open('w') as handle:
 p=subprocess.Popen(cmd,cwd=root,env=env,stdout=handle,stderr=subprocess.STDOUT);copied=False
 while p.poll() is None:
  if not copied and 'starting execution' in log.read_text():
   hits=list((root/'LiteDB.ReproRunner/LiteDB.ReproRunner.Cli/bin').glob('**/runs/'+name))
   if len(hits)!=1:raise RuntimeError(str(hits))
   for build in hits[0].glob('*/build'):shutil.copytree(build,out/build.parent.name/'build',dirs_exist_ok=True)
   copied=True
  time.sleep(.05)
 (out/'result.txt').write_text('Runner exit '+str(p.returncode)+'; binaries captured '+str(copied)+'\n')
files={str(x.relative_to(out)):hashlib.sha256(x.read_bytes()).hexdigest() for x in out.rglob('*') if x.is_file()};(out/'sha256.json').write_text(json.dumps(files,indent=2))
print(out);sys.exit(p.returncode)
