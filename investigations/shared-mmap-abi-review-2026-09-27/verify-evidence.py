#!/usr/bin/env python3
"""Verify the published bundle against its per-file and per-archive hashes."""
import argparse,hashlib,json,pathlib,re,tarfile
p=argparse.ArgumentParser(description=__doc__);p.add_argument('folder',type=pathlib.Path);args=p.parse_args()
m=json.loads((args.folder/'manifest.json').read_text());expected={r['path']:r for r in m['files']};seen=set();total=0
assert m['format']==2 and len(expected)==len(m['files'])
secret=re.compile(rb'(?:ghp_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{50,})')
for part in m['archives']:
 path=args.folder/part['name'];raw=path.read_bytes();assert len(raw)==part['bytes'] and len(raw)<100*1024**2
 assert hashlib.sha256(raw).hexdigest()==part['sha256']
 with tarfile.open(path) as tar:
  for item in tar:
   assert item.isfile() and item.name in expected and item.name not in seen,item.name
   assert not pathlib.PurePosixPath(item.name).is_absolute() and '..' not in pathlib.PurePosixPath(item.name).parts
   seen.add(item.name);data=tar.extractfile(item).read();r=expected[item.name]
   assert r['archive']==part['name'] and len(data)==r['publishedBytes'] and hashlib.sha256(data).hexdigest()==r['publishedSha256'],item.name
   assert not secret.search(data),item.name
   total+=len(data)
assert seen==set(expected)
print(json.dumps({'archives':len(m['archives']),'files':len(seen),'publishedBytes':total,'archiveBytes':sum(p['bytes'] for p in m['archives']),'allHashesMatch':True}))
