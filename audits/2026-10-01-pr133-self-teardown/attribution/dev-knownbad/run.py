import subprocess,json,pathlib
p=pathlib.Path(__file__).resolve().parent
results=[]
for mode in ['none','pragma','dispose']:
 for encryption in ['plain','encrypted']:
  name=mode+'-'+encryption; cmd=['dotnet',str(p/'bin/Release/net8.0/Repro.dll'),mode,encryption]
  try:
   r=subprocess.run(cmd,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=15);code=r.returncode;output=r.stdout.decode()
  except subprocess.TimeoutExpired as e:code='TIMEOUT';output=e.stdout.decode() if e.stdout else ''
  (p/(name+'.log')).write_text(output); results.append({'case':name,'exit':code,'output':output});print(name,code,output,flush=True)
(p/'results.json').write_text(json.dumps(results,indent=2))
