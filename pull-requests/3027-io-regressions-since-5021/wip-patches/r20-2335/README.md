# anchor-wip at 9771c6218 (LiteDB#3027), not yet pushed to the PR branch

A git bundle of every commit on top of the PR branch's pushed head a2e966a8c,
including the merge of dev 5dd942a73 (#3045 safety-evidence gates). Restore with:

    git fetch <path>/anchor-wip.bundle anchor-wip:anchor-wip

Held back from the PR branch until the fuzz corpus is re-pinned, the independent reviews are
addressed and the .NET 8/10 suites pass (a push now would turn the Fuzz check red).
Supersedes r17 to r19.
