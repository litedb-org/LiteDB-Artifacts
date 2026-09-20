# LiteDB-Artifacts

Things that were *produced* while working on [LiteDB](https://github.com/litedb-org/LiteDB) and are worth keeping, but do not belong in its source tree: raw measurements, benchmark and audit reports, reproduction outputs, and similar evidence.

Keeping them here means a pull request can cite thousands of raw samples without adding thousands of files to its diff, and the evidence stays available after the branch is gone.

## What goes where

| Lives here | Stays in `litedb-org/LiteDB` |
|---|---|
| Raw benchmark samples, profiler and memory measurements | The code that produces them (`tools/`, `LiteDB.Benchmarks`, `LiteDB.Stress`) |
| Reports that interpret those results for one change | Documentation of how the product works |
| Audit and verification outputs, one-off probes used for a review | Tests, including anything CI must run |

The rule of thumb: if a file describes or configures the product, it belongs with the source. If it records what happened when someone ran something, it belongs here.

## Layout

```
pull-requests/<number>-<slug>/    evidence gathered for one LiteDB pull request
```

Add other top-level folders when a different origin needs one, for example `releases/<version>/` or `investigations/<issue>-<slug>/`. Group by where the artifact came from, not by file type, so that one folder tells one story.

## Conventions

- **Record provenance.** Every folder has a `README.md` naming the LiteDB commit the artifacts were produced from, the environment, and how to reproduce them.
- **Link by commit, not by branch.** Point at source with permalinks such as `https://github.com/litedb-org/LiteDB/blob/<sha>/...`. Commits that belong to a pull request stay reachable through that pull request even after its branch is deleted.
- **Do not edit raw data.** If a measurement is repeated, add the new run beside the old one. Unfavourable results are evidence too.
- **No secrets, no personal data, no database files from real deployments.** Scrub machine-specific paths from outputs before committing.

## Contents

| Folder | What it holds |
|---|---|
| [`pull-requests/2905-shared-query-ir`](pull-requests/2905-shared-query-ir) | Benchmarks, merge-confidence audit and an independent re-audit for [LiteDB#2905](https://github.com/litedb-org/LiteDB/pull/2905), *Build shared query IR and optimize LINQ and SQL execution* |
| [`pull-requests/2944-shared-query-ir`](pull-requests/2944-shared-query-ir) | Cache review report and the randomized cached-versus-direct translation check for [LiteDB#2944](https://github.com/litedb-org/LiteDB/pull/2944), the successor of #2905 |
