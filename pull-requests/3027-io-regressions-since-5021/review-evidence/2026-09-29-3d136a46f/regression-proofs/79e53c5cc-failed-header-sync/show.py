import json, sys
d = json.load(open(sys.argv[1], encoding='utf-8-sig'))
for r in d['Repros']:
    for k in ('Package', 'Latest'):
        v = r[k]
        print(k, 'exit', v['ExitCode'], 'met', v['Met'])
        for l in v['Output']:
            e = json.loads(l['Text'])
            if e.get('type') in ('result', 'log'):
                print('   ', e.get('text', '')[:420])
