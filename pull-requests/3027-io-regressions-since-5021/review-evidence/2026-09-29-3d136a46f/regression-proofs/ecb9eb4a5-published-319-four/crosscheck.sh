#!/bin/bash
# Cross-check the four repros against the dev commit 5dd942a7 packed as 0.0.0-knownbad.5dd942a7367c.
P=$SCRATCH/proofs3027b
FEED=$SCRATCH/proofs/feed
for r in Issue_3027_DumpInTransaction Issue_3027_StreamDatabaseDispose Issue_3027_LegacyReadOnlyStream Issue_3027_ForeignLegacyWal; do
  bash "$P/against.sh" "$r" 0.0.0-knownbad.5dd942a7367c "$FEED" | grep -v '  log '
done
