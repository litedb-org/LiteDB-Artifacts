import pathlib, subprocess, json
root=pathlib.Path.cwd(); work=root/'artifacts_temp/integration'
groups={
 'handles':'FullyQualifiedName~TransactionHandle|FullyQualifiedName~TransactionCompletion|FullyQualifiedName~PageBufferAbandonment',
 'shared-engine':'FullyQualifiedName~LiteDB.Tests.Engine.Shared|FullyQualifiedName~CrossProcess_Shared',
 'shared-native':'(FullyQualifiedName~Shared&FullyQualifiedName!~LiteDB.Tests.Engine.Shared&FullyQualifiedName!~CrossProcess_Shared&FullyQualifiedName!~TransactionHandle)|FullyQualifiedName~NativeAdmission'
}
results=[]
for framework in ['net8.0','net10.0']:
 for group, filt in groups.items():
  name=f'reuse-{group}-{framework}'
  cmd=['dotnet','test','LiteDB.Tests','-c','Release','-f',framework,'-p:TestingEnabled=true','--settings','tests.runsettings','--filter',filt,'--logger',f'trx;LogFileName={name}.trx']
  with (root/'artifacts_temp'/f'{name}.log').open('w') as log:
   p=subprocess.run(cmd,cwd=work,stdout=log,stderr=subprocess.STDOUT)
  results.append({'name':name,'command':cmd,'exit':p.returncode})
  (root/'artifacts_temp/reuse-validation.json').write_text(json.dumps(results,indent=2))
  print(name,p.returncode,flush=True)
  if p.returncode: raise SystemExit(p.returncode)
