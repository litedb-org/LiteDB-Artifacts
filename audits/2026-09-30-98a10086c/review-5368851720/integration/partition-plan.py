import sys, pathlib, subprocess, json, datetime, xml.etree.ElementTree as E
root=pathlib.Path('__WORKSPACE__')
tfm=sys.argv[1]; wd=root/'artifacts_temp'/('integration' if tfm=='net10.0' else 'safety-net8')
out=root/'artifacts_temp'/('review536885-partitions-'+tfm);out.mkdir(exist_ok=True)
filters={'handles':'FullyQualifiedName~TransactionHandle','shared':'FullyQualifiedName~Shared&FullyQualifiedName!~TransactionHandle','other':'(FullyQualifiedName~Lifetime|FullyQualifiedName~Abandon|FullyQualifiedName~Admission|FullyQualifiedName~ProcessDeath|FullyQualifiedName~Failure)&FullyQualifiedName!~TransactionHandle&FullyQualifiedName!~Shared'}
base=['dotnet','test','LiteDB.Tests/LiteDB.Tests.csproj','-c','Release','-f',tfm,'-p:TestingEnabled=true','--settings','tests.runsettings']
manifest={'source':subprocess.check_output(['git','rev-parse','HEAD'],cwd=wd,text=True).strip(),'filters':filters,'runs':[]}
for i,(name,flt) in enumerate(filters.items()):
 command=base+(['--no-build'] if i else [])+['--filter',flt,'--logger',f'trx;LogFileName=review536885-{name}-{tfm}.trx']
 with (out/(name+'.log')).open('w') as f:
  start=datetime.datetime.now(datetime.timezone.utc).isoformat();run=subprocess.run(command,cwd=wd,stdout=f,stderr=subprocess.STDOUT)
 manifest['runs'].append({'partition':name,'command':command,'started':start,'exit':run.returncode});(out/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
 print(name,run.returncode,flush=True)
 if run.returncode: sys.exit(run.returncode)
 subprocess.run(base+['--no-build','--list-tests','--filter',flt],cwd=wd,stdout=(out/(name+'-discovery.log')).open('w'),stderr=subprocess.STDOUT,check=True)
print('PARTITIONS_COMPLETE',flush=True)
