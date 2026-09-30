import subprocess,pathlib,json,hashlib
r=pathlib.Path(__file__).resolve().parent
results=[]
for encrypted in ['plain','encrypted']:
 for mode in ['peer','same','other','cancel']:
  f=r/(encrypted+'-'+mode+'.db')
  p=subprocess.run(['dotnet',str(r/'bin/Release/net8.0/repro.dll'),str(f),encrypted,mode],capture_output=True,text=True,timeout=25)
  (r/(encrypted+'-'+mode+'.log')).write_text(p.stdout+p.stderr)
  results.append(dict(encryption=encrypted,mode=mode,exit=p.returncode,stdout=p.stdout,stderr=p.stderr))
  if mode in ['peer','cancel']:
   v=subprocess.run(['dotnet',str(r/'bin/Release/net8.0/repro.dll'),str(f),encrypted,'verify'],capture_output=True,text=True,timeout=10)
   results.append(dict(encryption=encrypted,mode=mode+'-cold',exit=v.returncode,stdout=v.stdout,stderr=v.stderr))
(r/'results.json').write_text(json.dumps(results,indent=2))
print(json.dumps(results,indent=2))
