#!/bin/bash
# mutate.sh: remove the fix's two checks, run the proof once, restore the file
O=$SCRATCH/promo-proof
cd $REPO/.claude/worktrees/agent-ae3466fb0272766da
F=LiteDB/Engine/Disk/DiskService.FileVersion.cs
cp $F $O/FileVersion.orig
python3 - <<'PY'
p='LiteDB/Engine/Disk/DiskService.FileVersion.cs'
s=open(p).read()
old='''                _state.ThrowIfStopped();
                _state.RequireNoWriteFailure();
'''
assert old in s
open(p,'w').write(s.replace(old,''))
PY
$O/run.sh mutfinal | grep -E "exit|After the failed|result"
cp $O/FileVersion.orig $F
git status --short -- $F
echo restored
