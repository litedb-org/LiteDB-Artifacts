import json
import sys

r = json.load(open(sys.argv[1]))
for rp in r['Repros']:
    print(rp['Id'], 'Failed', rp['Failed'])
    for v in ('Package', 'Latest'):
        x = rp[v]
        print(' ', v, {k: x[k] for k in ('Met', 'ExitCode', 'UseProjectReference', 'FailureReason', 'DurationSeconds')})
        for o in x['Output']:
            t = o['Text']
            if '"result"' in t or 'loaded from' in t:
                print('   ', json.loads(t).get('text'))
