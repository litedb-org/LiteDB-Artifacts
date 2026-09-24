import collections, json, pathlib, statistics
root=pathlib.Path(__file__).resolve().parent
out=[]
for filename, expected in [('final-shared-startup.jsonl',36),('final-shared-steady.jsonl',54),('final-direct.jsonl',12)]:
 path=root/filename
 if not path.exists(): continue
 rows=[json.loads(x) for x in path.read_text().splitlines()]
 groups=collections.defaultdict(list)
 for row in rows: groups[(row['runtime'],row['mode'],row['scenario'],row['build'])].append(row)
 out += [f'## {filename}: {len(rows)}/{expected} runs', '', '| Runtime | Mode | Scenario | Build | Runs | Mean ms (median; range) | p99 ms | Allocated bytes/op | CPU ms/op | Close ms |', '| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |']
 for key, runs in sorted(groups.items()):
  median=lambda field:statistics.median(x[field] for x in runs)
  means=[x['meanMs'] for x in runs]
  out.append('| '+' | '.join(key)+f' | {len(runs)} | {median("meanMs"):.4f} ({min(means):.4f}–{max(means):.4f}) | {median("p99Ms"):.4f} | {median("bytesPerOperation"):,.0f} | {median("cpuMsPerOperation"):.4f} | {median("closeMs"):.4f} |')
 out += ['', 'All figures are medians of individual run statistics; p99 is not a pooled percentile.', '']
(root/'final-summary.md').write_text('\n'.join(out)+'\n')
print('\n'.join(out))
