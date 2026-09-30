#!/usr/bin/env python3
"""Add only explicitly selected, quiescent evidence; never commit or publish."""
import argparse, hashlib, importlib.util, json, re, shutil, subprocess, tarfile
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent
AUDIT=ROOT/'artifacts_temp/published-evidence/audits/2026-09-30-pr133-concurrency'
NORMALIZER=ROOT/'artifacts_temp/normalize-pr133-publication.py'
BACKUP=ROOT/'artifacts_temp/pr133-concurrency-publication-originals'
GROUPS={
 'author-explorer':'audit-explorer/artifacts_temp/explorer-evidence',
 'author-lifecycle/runtime':'audit-chaos/artifacts_temp/lifecycle-flush-oracle-runtime',
 'author-lifecycle/net8.0':'audit-chaos/artifacts_temp/lifecycle-flush-oracle-net8.0',
 'author-lifecycle/net10.0':'audit-chaos/artifacts_temp/lifecycle-flush-oracle-net10.0',
 'independent-oracles/peer-callback-repro':'audit-oracles/artifacts_temp/peer-callback-repro',
 'independent-oracles/explorer-mutations':'audit-oracles/artifacts_temp/explorer-mutations',
 'independent-oracles/final-oracle-controls':'audit-oracles/artifacts_temp/final-oracle-controls',
}
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def git(*args):return subprocess.check_output(['git','-C',str(ROOT),*args])
def dump(p,d):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(d,indent=2)+'\n')
def copy_group(src,dest,records):
 if dest.exists():raise RuntimeError('Refusing to overwrite group '+str(dest))
 files=sorted(p for p in src.rglob('*') if p.is_file())
 for p in files:
  if p.is_symlink():raise RuntimeError('Unexpected symlink '+str(p))
  target=dest/p.relative_to(src);target.parent.mkdir(parents=True,exist_ok=True)
  before=sha(p);shutil.copy2(p,target)
  if before!=sha(p) or before!=sha(target):raise RuntimeError('Source changed during capture '+str(p))
  records.append({'source':str(p.relative_to(ROOT)) if p.is_relative_to(ROOT) else str(p),'file':str(target.relative_to(AUDIT)),'sourceSha256':before,'sourceBytes':p.stat().st_size})
def refresh():
 subprocess.run(['python3',str(NORMALIZER),'--audit',str(AUDIT),'--workspace',str(ROOT),'--backup',str(BACKUP)],check=True,stdout=subprocess.DEVNULL)
 manifest=json.loads((AUDIT/'manifest.json').read_text())
 binary_count=0
 for row in manifest['files']:
  p=AUDIT/row['file'];row['publishedSha256']=sha(p);row['publishedBytes']=p.stat().st_size
  data=p.read_bytes()
  if b'\0' in data or p.suffix.lower() in {'.db','.dll','.pdb','.exe','.so','.bin'}:
   binary_count+=1
   if row['sourceSha256']!=row['publishedSha256']:raise RuntimeError('Binary changed '+row['file'])
 dump(AUDIT/'manifest.json',manifest)
 spec=importlib.util.spec_from_file_location('normalizer',NORMALIZER);mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
 credential=[];paths=[];text_count=0;binary_paths=0
 def scan(name,data):
  nonlocal text_count,binary_paths
  decoded=data.decode('utf-8',errors='replace')
  if any(re.search(pattern,decoded) for pattern in mod.CREDENTIALS):credential.append(name)
  private=bool(re.search(r'/(?:home|Users)/[^/\s"<>]+|[A-Za-z]:[\\/]+Users[\\/]+',decoded))
  if b'\0' in data or Path(name).suffix.lower() in mod.BINARY:
   if private:binary_paths+=1
   return
  try:data.decode('utf-8')
  except UnicodeError:return
  text_count+=1
  if private:paths.append(name)
 for p in sorted(AUDIT.rglob('*')):
  if not p.is_file() or p.name in {'SHA256SUMS','scan-report.json'}:continue
  if p.name.endswith('.tar.gz'):
   with tarfile.open(p,'r:gz') as tar:
    for m in tar:
     if m.isfile():scan(str(p.relative_to(AUDIT))+'!'+m.name,tar.extractfile(m).read())
  else:scan(str(p.relative_to(AUDIT)),p.read_bytes())
 report={'credentialPatternFiles':credential,'privatePathTextFiles':paths,'textPayloadsScanned':text_count,'binaryPayloadsContainingOriginalBuildPaths':binary_paths,'binaryFilesVerifiedByteIdenticalToSources':binary_count,'scope':'All staged file payloads and source archive members; binary build paths intentionally retained to preserve exact binary identities. Pattern scan is not a universal secret detector.'}
 dump(AUDIT/'scan-report.json',report)
 if credential or paths:raise RuntimeError('Publication scan failed; inspect scan-report.json')
 files=sorted(p for p in AUDIT.rglob('*') if p.is_file() and p.name!='SHA256SUMS')
 (AUDIT/'SHA256SUMS').write_text(''.join(sha(p)+'  '+str(p.relative_to(AUDIT))+'\n' for p in files))
 print(json.dumps({'files':len(files)+1,'bytes':sum(p.stat().st_size for p in AUDIT.rglob('*') if p.is_file()),'scan':report},indent=2))
def main():
 ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--initial',action='store_true');ap.add_argument('--add-tree',nargs=2,metavar=('SOURCE','DESTINATION'));ap.add_argument('--quiescent',action='store_true');a=ap.parse_args()
 if a.initial:
  if AUDIT.exists():raise RuntimeError('Audit directory already exists')
  AUDIT.mkdir(parents=True);records=[]
  for dest,source in GROUPS.items():copy_group(ROOT/'artifacts_temp'/source,AUDIT/dest,records)
  dump(AUDIT/'manifest.json',{'schemaVersion':1,'description':'Original source hashes and normalized public hashes are distinct. Groups captured only after author processes exited. Earlier overwritten binaries are not recreated.','files':records})
  source=AUDIT/'source';source.mkdir()
  base=git('rev-parse','f9487f814').decode().strip()
  with (source/'integration-source.tar.gz').open('wb') as out:subprocess.run(['git','-C',str(ROOT),'archive','--format=tar.gz',base],stdout=out,check=True)
  revisions=['98a10086c','c645c51b0','dc305db77','e79635a06','4867ceb90','0494bd320','cdf4a6d89','c543133fe','49c327cf1926fa300f9eb7477eb4bcb404f75c43','5dd942a7367c361fadd600be4ce10aace2768b27']
  entries=[]
  for revision in revisions:
   commit=git('rev-parse',revision).decode().strip();p=source/(commit+'.patch');p.write_bytes(git('diff','--binary',base,commit))
   entries.append({'commit':commit,'tree':git('rev-parse',commit+'^{tree}').decode().strip(),'patchFromIntegration':p.name,'patchOriginalSha256':sha(p)})
  dump(source/'manifest.json',{'integrationCommit':base,'integrationTree':git('rev-parse',base+'^{tree}').decode().strip(),'archiveOriginalSha256':sha(source/'integration-source.tar.gz'),'reconstruction':'Extract integration-source.tar.gz into a clean directory; apply the selected patch with git apply --binary. Retained intermediate mutation and standalone control sources are in their evidence directories. Normalization maps any changed source payloads separately.','revisions':entries})
  (AUDIT/'scripts').mkdir();shutil.copy2(NORMALIZER,AUDIT/'scripts/normalize-publication.py');shutil.copy2(__file__,AUDIT/'scripts/stage-evidence.py')
  for name in ['pr133-concurrency-explorer.md','pr133-multiprocess-campaign.md','pr133-concurrency-oracle-review.md']:
   p=ROOT/'artifacts_temp/concurrency-audit/docs/audits'/name
   if p.exists():shutil.copy2(p,AUDIT/name)
  refresh()
 elif a.add_tree:
  if not a.quiescent:raise RuntimeError('--quiescent requires confirmation that all owning workers exited')
  src=Path(a.add_tree[0]).resolve();dest=AUDIT/a.add_tree[1]
  if not dest.resolve().is_relative_to(AUDIT):raise RuntimeError('Destination must be inside new audit')
  manifest=json.loads((AUDIT/'manifest.json').read_text());copy_group(src,dest,manifest['files']);dump(AUDIT/'manifest.json',manifest);refresh()
 else:refresh()
if __name__=='__main__':main()
