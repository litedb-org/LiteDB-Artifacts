# Evidence for LiteDB#3027 at 3d136a46f (2026-09-29, 00:25 to 08:00 UTC)

The owner's merge gate on review 5883668296 and what answered it: the caller-stream flush and its
review, the gate fixes 659784920, ad3382f6a and 0ce00ee55 and their independent review, the
outstanding-journal promotion test ae3f68bee and the window it found (db394db6f), and fifteen
ReproRunner regression proofs pinned to the published LiteDB 6.0.0-prerelease.319 (8c3e2dca6,
ecb9eb4a5, 899b5acc5, 79e53c5cc, 5ffa46d13, 3d136a46f). Branch head when archived:
3d136a46f2e7f19bb32a93cf4b90a44e79360d2b; base dev 5dd942a73.

The provenance, a table of what is where, the PR's claims with their evidence, the test and fuzz
runs and the worktrees are in the section "Round of 2026-09-29, 00:25 to 08:00 UTC" of
[../../README.md](../../README.md). Test runs, fuzz logs, drafts and `test-head.sh` are in the
PR folder's `logs/`, `drafts/` and `scripts/`.

Files named `*.recovered-from-transcript.txt` were copied verbatim from the session transcript
because the run's own file was overwritten or never written. Paths are scrubbed to `$SCRATCH`,
`$SESSION`, `$REPO` and `$DOTNET_ROOT`.
