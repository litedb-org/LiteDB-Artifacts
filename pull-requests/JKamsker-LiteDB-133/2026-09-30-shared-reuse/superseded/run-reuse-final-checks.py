import pathlib,subprocess,json
root=pathlib.Path.cwd(); work=root/'artifacts_temp/integration'; results=[]
for framework in ['net8.0','net10.0']:
 name='reuse-child-final-'+framework
 cmd=['dotnet','test','LiteDB.Tests','-c','Release','-f',framework,'-p:TestingEnabled=true','--settings','tests.runsettings','--filter','FullyQualifiedName~TransactionHandleChild','--logger',f'trx;LogFileName={name}.trx']
 with (root/'artifacts_temp'/f'{name}.log').open('w') as log:
  p=subprocess.run(cmd,cwd=work,stdout=log,stderr=subprocess.STDOUT)
 results.append({'name':name,'command':cmd,'exit':p.returncode}); print(name,p.returncode,flush=True)
 if p.returncode: raise SystemExit(p.returncode)
for framework in ['net462','net481']:
 name='reuse-build-'+framework
 cmd=['dotnet','build','LiteDB.Tests','-c','Release','-f',framework,'-p:TestingEnabled=true']
 with (root/'artifacts_temp'/f'{name}.log').open('w') as log:
  p=subprocess.run(cmd,cwd=work,stdout=log,stderr=subprocess.STDOUT)
 results.append({'name':name,'command':cmd,'exit':p.returncode}); print(name,p.returncode,flush=True)
 if p.returncode: raise SystemExit(p.returncode)
(root/'artifacts_temp/reuse-final-checks.json').write_text(json.dumps(results,indent=2))
