p = '$REPO/.claude/worktrees/agent-ac6b0cf63f278326a/LiteDB.Tests/Regressions/SlotReuseWithoutProof_Tests.cs'
s = open(p).read()
start = s.index('        /// <summary>\n        /// A retiring checkpoint published its root durably')
end = s.index('        /// <summary>\n        /// Commit values 1 to 9 under a live reader')
new = open('$SCRATCH/slot/testb.txt').read()
s = s[:start] + new + s[end:]
old = 'CommitWithPowerLossImages(db, data, log, null, 9, 10, () =>'
assert old in s
s = s.replace(old, 'CommitWithPowerLossImages(db, data, log, null, 9, 10, committed: () =>')
open(p, 'w').write(s)
