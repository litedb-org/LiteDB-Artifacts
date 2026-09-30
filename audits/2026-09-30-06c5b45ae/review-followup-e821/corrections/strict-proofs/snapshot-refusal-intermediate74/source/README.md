# Refused late Shared snapshot close releases native protection

This black-box proof exercises review
[5365276527, finding 1](https://github.com/JKamsker/LiteDB/pull/133#pullrequestreview-5365276527).
Its package variant pins the actual PR revision
`e821ae7479dcb83570031180b0c0e3f4fd167793`, packed as
`0.0.0-knownbad.e821ae7479dc`; no released package contains this regression.

The proof creates an ordinary file at `<database>-readers`, making registry
registration fail through the supported filesystem fallback. A public read-only
`SharedEngine` with a `ReadTransform` returns a streaming reader. Only after
`Query` returns and the first pre-materialized row is consumed does the proof
arm its callback. The second `Read` calls `Dispose` from that callback, catches
the expected refusal, and probes writer admission from another caller thread
through a separate public Shared database and bounded transaction handle.

Before the callback, the fallback must exclude that writer. The known-bad
revision lets the same writer acquire during the callback after refusing the
close. The corrected source must still exclude it, leave the connection usable,
and support normal close once the read operation returns. The proof uses no
reflection, internal members, test hooks, or fabricated lock identities.

Plain and encrypted cases both run, alongside controls with actual reader-lease
files. A genuinely leased reader can survive connection close, admit a peer
writer, and finish its original snapshot after that writer commits. Every case
checks the exact snapshot and twice-cold records, values, value-index plan and
results, and an untouched sentinel collection. The fallback also checks that
the unrelated filesystem obstacle was preserved before removing it for the
post-close writer and cold reopen.

Exit `0` and `BUG_REPRODUCED` mean both fallback cases exhibit premature native
release. Exit `10` and `VERIFIED_FIXED` require both fallback cases to retain
ownership and pass all controls and cleanup checks. Unexpected errors or a
plain/encrypted disagreement exit `1`, never a verified fix. This is an
exception/ownership proof, not a power-loss or demonstrated-corruption claim.

Run through ReproRunner after packing the pinned commit into a local feed:

```sh
python .github/scripts/regression_proof.py pack-known-bad \
  --commit e821ae7479dcb83570031180b0c0e3f4fd167793 --feed __WORKSPACE__/temporary/pr133-knownbad-feed
RestoreAdditionalProjectSources=__WORKSPACE__/temporary/pr133-knownbad-feed \
  dotnet run --project LiteDB.ReproRunner/LiteDB.ReproRunner.Cli -- \
  run Issue_133_SharedSnapshotCloseRefusal
```
