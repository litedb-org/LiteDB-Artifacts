#!/bin/bash
# usage: mutate.sh <file> <python-old> <python-new> <filter>
set -u
cd $TMPDIR/review2-a
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1
FILE="$1"; OLD="$2"; NEW="$3"; FILTER="$4"
python3 - "$FILE" "$OLD" "$NEW" <<'PY'
import sys
p,old,new=sys.argv[1],sys.argv[2],sys.argv[3]
s=open(p,encoding='utf-8-sig').read()
raw=open(p,'rb').read()
bom=raw.startswith(b'\xef\xbb\xbf')
if s.count(old)!=1:
    print("MUTATION TARGET COUNT", s.count(old)); sys.exit(1)
s=s.replace(old,new)
open(p,'w',encoding='utf-8-sig' if bom else 'utf-8').write(s)
print("mutated", p)
PY
[ $? -ne 0 ] && exit 1
dotnet build LiteDB.Tests -c Release -f net10.0 -p:TestingEnabled=true 2>&1 | grep -E " error |Error\(s\)" | head -5
timeout 800 dotnet test LiteDB.Tests -c Release -f net10.0 -p:TestingEnabled=true --no-build --settings tests.runsettings --filter "$FILTER" --logger "console;verbosity=detailed" 2>&1 | grep -E "^\s+Failed |Passed!|Failed!|control|dataTail=|logTail=" | cut -c1-300
git checkout -- "$FILE"
