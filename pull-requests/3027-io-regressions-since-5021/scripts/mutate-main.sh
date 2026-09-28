#!/bin/bash
# usage: mutate-main.sh <file> <python-old> <python-new> <filter>   (restores the file from a backup, not git)
set -u
cd $REPO
export PATH=/root/.dotnet:$PATH DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
FILE="$1"; OLD="$2"; NEW="$3"; FILTER="$4"
cp "$FILE" "$FILE.mutbak"
python3 - "$FILE" "$OLD" "$NEW" <<'PY'
import sys
p,old,new=sys.argv[1],sys.argv[2],sys.argv[3]
raw=open(p,'rb').read()
bom=raw.startswith(b'\xef\xbb\xbf')
s=raw.decode('utf-8-sig')
if s.count(old)!=1:
    print("MUTATION TARGET COUNT", s.count(old)); sys.exit(1)
s=s.replace(old,new)
open(p,'w',encoding='utf-8-sig' if bom else 'utf-8').write(s)
print("mutated", p)
PY
if [ $? -eq 0 ]; then
  dotnet build LiteDB.Tests/LiteDB.Tests.csproj -c Release -f net10.0 -p:TestingEnabled=true 2>&1 | grep -E " error |Error\(s\)" | head -5
  timeout 800 dotnet test LiteDB.Tests/LiteDB.Tests.csproj -c Release -f net10.0 -p:TestingEnabled=true --no-build --settings tests.runsettings --filter "$FILTER" 2>&1 | grep -E "^\s+Failed |Passed!|Failed!" | cut -c1-250
fi
mv "$FILE.mutbak" "$FILE"; touch "$FILE"
