This is the exact569 local strict-proof capture. The neighboring `SHA256SUMS` was
produced over the original capture before publication path normalization and is
retained as original evidence. Its binary hashes remain applicable, but normalized
text files have different public hashes. Validate this published copy with the
audit root's `SHA256SUMS`; `publication-normalization.json` maps changed text files.
`final-proof-source-index.json` also records raw source hashes and locations.

The runtime source/package exits and verified result are recorded in `proof.json`.
The capture watcher raced CLI cleanup after that report was written. The error and
inventory validation are preserved in `capture-cleanup-race.json` and
`binary-check.json`; no outer CLI exit code is asserted. The known-bad package does
not distribute a LiteDB PDB. The candidate PDB and complete runtime dependency
inventories are retained. This capture does not overwrite the earlier intermediate
proof or its failed setup attempts.
