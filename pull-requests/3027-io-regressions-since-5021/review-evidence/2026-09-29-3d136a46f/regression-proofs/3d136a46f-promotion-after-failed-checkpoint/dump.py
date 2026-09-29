import json,sys
d=json.load(open(sys.argv[1]))
for r in d['Repros']:
  for k in ('Package','Latest'):
    v=r.get(k) or {}
    print(k, 'exit', v.get('ExitCode'), 'met', v.get('Met'), v.get('FailureReason'))
    for o in v.get('Output',[]):
      t=o['Text']
      try:
        j=json.loads(t); print('  ',j.get('type'), (j.get('text') or '')[:3000])
      except Exception: print('  RAW', t[:300])
