#!/usr/bin/env python3
"""Normalize publication paths in a staged audit; keep raw backup copies outside it."""
import argparse,copy,datetime,gzip,hashlib,io,json,os,re,shutil,tarfile,tempfile
from pathlib import Path
import xml.etree.ElementTree as ET

BINARY={'.bin','.db','.dll','.exe','.pdb','.so','.a','.o','.nupkg','.zip','.png','.jpg','.jpeg','.webp','.pdf','.woff','.woff2'}
CREDENTIALS=[r'gh[pousr]_[A-Za-z0-9]{20,}',r'github_pat_[A-Za-z0-9_]{20,}',r'AKIA[0-9A-Z]{16}',r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',r'Authorization:\s*(?:Bearer|token)\s+(?!\*\*\*)\S+']

def digest(data):return hashlib.sha256(data).hexdigest()
def filehash(path):
 h=hashlib.sha256()
 with path.open('rb') as f:
  for block in iter(lambda:f.read(1024*1024),b''):h.update(block)
 return h.hexdigest()

def normalize(text,workspace):
 # Longest/specific prefixes precede generic home rules. Preserve path tails,
 # data values, line numbers, exception types, test identities and results.
 text=text.replace(str(workspace),'__WORKSPACE__')
 text=re.sub(r'/(?:home|Users)/runner/work/([^/\s"<>]+)/\1(?=[/\s"<>]|$)','__CI_WORKSPACE__',text,flags=re.I)
 text=re.sub(r'[A-Za-z]:[\\/]+a[\\/]+LiteDB[\\/]+LiteDB(?=[\\/\s"<>]|$)','__CI_WORKSPACE__',text,flags=re.I)
 text=re.sub(r'[A-Za-z]:[\\/]+a(?=[\\/\s"<>]|$)','__CI_HOME__/work',text,flags=re.I)
 text=re.sub(r'[A-Za-z]:[\\/]+Users[\\/]+(?:runneradmin|runner|VssAdministrator)(?=[\\/\s"<>]|$)','__CI_HOME__',text,flags=re.I)
 text=re.sub(r'[A-Za-z]:[\\/]+Users[\\/]+[^\\/\s"<>]+','__LOCAL_HOME__',text,flags=re.I)
 text=re.sub(r'/(?:home|Users)/runner(?=[/\s"<>]|$)','__CI_HOME__',text,flags=re.I)
 text=re.sub(r'/(?:home|Users)/[^/\s"<>]+','__LOCAL_HOME__',text)
 text=re.sub(r'/(?:private/)?var/folders/[^/\s"<>]+/[^/\s"<>]+','__CI_HOME__/temporary',text)
 text=re.sub(r'/(?:private/)?tmp(?=[/\s"<>]|$)','__WORKSPACE__/temporary',text)
 return text

def numeric_facts(value):
 if isinstance(value,dict):return [item for child in value.values() for item in numeric_facts(child)]
 if isinstance(value,list):return [item for child in value for item in numeric_facts(child)]
 return [(type(value).__name__,value)] if isinstance(value,(int,float,bool)) or value is None else []

def validate(name,before,after):
 suffix=Path(name).suffix.lower()
 if suffix=='.json':
  original=json.loads(before.decode('utf-8-sig'));published=json.loads(after.decode('utf-8-sig'))
  if numeric_facts(original)!=numeric_facts(published):raise ValueError('JSON numeric facts changed: '+name)
  return 'json'
 if suffix=='.jsonl':
  for data in (before,after):
   for line in data.decode('utf-8-sig').splitlines():
    if line.strip():json.loads(line)
  return 'jsonl'
 if suffix in {'.xml','.trx','.csproj','.props','.targets'}:
  ET.fromstring(before);ET.fromstring(after);return 'xml'
 return 'text'

def change_text(name,data,workspace,scan):
 if Path(name).suffix.lower() in BINARY or b'\x00' in data:return data,None
 try:text=data.decode('utf-8')
 except UnicodeError:return data,None
 if any(re.search(p,text) for p in CREDENTIALS):scan.add(name)
 changed=normalize(text,workspace).encode('utf-8')
 if changed==data:return data,None
 return changed,validate(name,data,changed)

def main():
 ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--audit',type=Path,required=True);ap.add_argument('--exclude-prefix',action='append',default=[]);ap.add_argument('--workspace',type=Path,default=Path(__file__).resolve().parent.parent);ap.add_argument('--backup',type=Path,default=Path(__file__).resolve().parent/'safety-publication-originals');args=ap.parse_args();audit=args.audit.resolve();args.backup.mkdir(parents=True,exist_ok=True)
 reportpath=audit/'publication-normalization.json'
 report=json.loads(reportpath.read_text()) if reportpath.exists() else {'schemaVersion':1,'description':'Public copies replace machine-specific absolute path prefixes with safe placeholders. Dataset measurements, assertions, outcomes and test identities are unchanged. Binary files and binary tar-member payloads are preserved byte-for-byte. Archive owner names/IDs and gzip filename/time metadata are removed for publication.','placeholders':['__WORKSPACE__','__LOCAL_HOME__','__CI_WORKSPACE__','__CI_HOME__'],'hashInterpretation':'manifest.json source SHA256 values refer to pre-normalization originals. This report maps each changed public file/member from its original SHA256 to published SHA256. Final SHA256SUMS is generated separately over the final public copies. Raw originals are retained privately outside this audit.','changes':[]}
 scan=set();count=0;validated={'json':0,'jsonl':0,'xml':0,'text':0};tarchecks=[]
 paths=sorted(p for p in audit.rglob('*') if p.is_file() and p.name not in {'publication-normalization.json','SHA256SUMS'} and not any(str(p.relative_to(audit)).startswith(prefix) for prefix in args.exclude_prefix))
 for path in paths:
  rel=str(path.relative_to(audit));entry={'file':rel};oldhash=filehash(path);changed=False
  if path.name.endswith(('.tar.gz','.tgz')):
   members=[];original_count=0;expected=[];metadata_count=0
   fd,tmp=tempfile.mkstemp(prefix='normalize-',suffix='.tar.gz',dir=args.backup);os.close(fd);temporary=Path(tmp)
   try:
    with tarfile.open(path,'r:gz') as source,temporary.open('wb') as output,gzip.GzipFile(filename='',mode='wb',fileobj=output,mtime=0,compresslevel=6) as gz,tarfile.open(fileobj=gz,mode='w|',format=tarfile.PAX_FORMAT) as target:
     for member in source:
      original_count+=1;info=copy.copy(member);info.pax_headers=dict(member.pax_headers)
      info.name=normalize(info.name,args.workspace);info.linkname=normalize(info.linkname,args.workspace)
      metachanged=bool(info.uname or info.gname or info.uid or info.gid or info.name!=member.name or info.linkname!=member.linkname)
      info.uname='';info.gname='';info.uid=0;info.gid=0
      for k in ('uname','gname','uid','gid'):
       if k in info.pax_headers:info.pax_headers.pop(k);metachanged=True
      for k,v in list(info.pax_headers.items()):
       clean=normalize(v,args.workspace)
       if clean!=v:metachanged=True;info.pax_headers[k]=clean
      before=source.extractfile(member).read() if member.isfile() else None
      after,kind=change_text(rel+'!'+member.name,before,args.workspace,scan) if before is not None else (None,None)
      if kind:validated[kind]+=1
      if metachanged:metadata_count+=1
      if before is not None:info.size=len(after)
      target.addfile(info,io.BytesIO(after) if after is not None else None)
      expected.append((info.name,None if after is None else digest(after)))
      if before!=after or metachanged:
       members.append({'entry':normalize(member.name,args.workspace),'publishedEntry':info.name,'originalSha256':None if before is None else digest(before),'publishedSha256':None if after is None else digest(after),'textValidation':kind,'metadataNormalized':metachanged})
       changed=True
    # Validate entry count AND every payload, including all preserved binary inputs.
    if changed:
     with tarfile.open(temporary,'r:gz') as result:
      actual=[(m.name,digest(result.extractfile(m).read()) if m.isfile() else None) for m in result]
     if actual!=expected:raise RuntimeError('Archive entries/payloads changed unexpectedly: '+rel)
     entry.update({'archiveEntriesBefore':original_count,'archiveEntriesAfter':len(actual),'metadataEntriesNormalized':metadata_count,'members':members,'validation':'All entry payload hashes and order verified; unchanged binary payloads preserved.'});tarchecks.append({'file':rel,'entries':original_count})
     backup=args.backup/(oldhash+'-'+path.name)
     if not backup.exists():shutil.copy2(path,backup)
     shutil.copyfile(temporary,path)
   finally:temporary.unlink(missing_ok=True)
  elif path.suffix=='.gz':
   before=gzip.decompress(path.read_bytes());after,kind=change_text(rel[:-3],before,args.workspace,scan)
   if kind:
    validated[kind]+=1;backup=args.backup/(oldhash+'-'+path.name)
    if not backup.exists():shutil.copy2(path,backup)
    path.write_bytes(gzip.compress(after,mtime=0));assert gzip.decompress(path.read_bytes())==after
    entry.update({'originalPayloadSha256':digest(before),'publishedPayloadSha256':digest(after),'textValidation':kind});changed=True
  else:
   before=path.read_bytes();after,kind=change_text(rel,before,args.workspace,scan)
   if kind:
    validated[kind]+=1;backup=args.backup/(oldhash+'-'+path.name)
    if not backup.exists():shutil.copy2(path,backup)
    path.write_bytes(after);entry['textValidation']=kind;changed=True
  if changed:
   entry.update({'originalSha256':oldhash,'publishedSha256':filehash(path)});report['changes'].append(entry);count+=1
 report['lastPassExcludedPrefixes']=args.exclude_prefix;report['lastPassUtc']=datetime.datetime.now(datetime.timezone.utc).isoformat();report['lastPassChangedFiles']=count;report['lastPassValidationCounts']=validated;report['lastPassArchiveChecks']=tarchecks;report['credentialPatternFiles']=sorted(scan)
 report['currentPublishedFiles']={name:filehash(audit/name) for name in sorted({entry.get('currentFile',entry['file']) for entry in report['changes']}) if (audit/name).is_file()}
 report['changeRecordMeaning']='changes preserves each normalization event at its original path; optional currentFile and relocations resolve a moved public copy without assigning historical hashes to a new candidate. currentPublishedFiles reflects the latest public copies, including later public metadata edits. Final SHA256SUMS remains authoritative.'
 reportpath.write_text(json.dumps(report,indent=2)+'\n');json.loads(reportpath.read_text())
 print(json.dumps({'changedFiles':count,'validation':validated,'tarChecks':tarchecks,'credentialPatternFiles':sorted(scan),'totalChangeRecords':len(report['changes'])},indent=2))
if __name__=='__main__':main()
