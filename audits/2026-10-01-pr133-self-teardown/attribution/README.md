# Same-connection Shared teardown attribution

Linux x64, .NET 8 production builds (`Release`, `TestingEnabled=false`).
Tests used public caller-owned FileStream callbacks. Reflection only obtains the
existing native Mutex for independent foreign-thread `WaitOne(0)` probes.
No peer writes were attempted inside the dangerous callback.

| Revision | Getter callback, plain/encrypted | Raw Dispose callback, plain/encrypted |
| --- | --- | --- |
| Upstream dev `023c2b4ba8ffe637c955092ff05289d90eafdfb4` | Returns; later peer write detects data checksum mismatch at offset 8192 | Returns; native mutex becomes available inside callback; later peer write detects same checksum mismatch |
| PR stacked parent `49c327cf1` | Same observed outcome as dev | Same observed outcome as dev |
| PR head `3abead6dc2eb6413daf5fa81d8faed06cdf4ade7` | Callback entered with native mutex held, never returned; process killed after 15 seconds | Returns; native mutex becomes available inside callback; later cold indexed state remains correct |
| Upstream safety correction, production tree from `f4349ef92` | Immediate InvalidOperationException; native mutex stays unavailable inside callback | Same |

The corrected source refuses before touching ownership. Exact rows, indexed
lookups and unrelated sentinel survive two cold reopenings, and a subsequent
independent Shared connection writes successfully. Original unmodified dev and
parent no-reentry controls pass in both plain/encrypted configurations. Thus the
upstream checksum failures are discriminating, not a generally broken stream
fixture. These observations do not establish an introduction commit and do not
claim a concurrent external writer was used to corrupt the database.

The two killed PR-head getter processes recovered all 61 acknowledged rows,
their indexed lookups and sentinel in two fresh direct opens. Original pre-recovery
fixture copies were retained before those opens; recovery logs and binaries are in
`head/recovery`. This models process termination, not power loss.

## Reproduce

Copy the selected variant's `Program.cs` and `Repro.csproj` into
`artifacts_temp/teardown-repro/` in a checkout at the listed revision, then run:

```
dotnet build artifacts_temp/teardown-repro/Repro.csproj -c Release -p:TestingEnabled=false
python3 artifacts_temp/teardown-repro/run.py
```

For a single case: `dotnet .../bin/Release/net8.0/Repro.dll dispose encrypted`.
`none` executes the same checkpoint/callback path without reentry. Each case prints
its original `__WORKSPACE__/temporary/litedb-teardown-attribution-*` directory and retains it. The
runner launches a fresh process per case and kills a child at 15 seconds; it never
tries to dispose a blocked live graph. Timeout is classified as failure evidence,
not successful deadlock prevention.

`dev-knownbad` is a fresh untouched-production dev checkout preserving the original
bad behavior separately from `dev` (the corrected source). Earlier dev results
were superseded by this explicit known-bad rerun, not relabeled as fixed evidence.
`head` ran the initial two-mode source, archived as `Program.initial-executed.cs.txt`;
its current Program.cs only adds a no-reentry mode. Parent's initial runtime is
also retained; the later runtime adds that mode for positive controls.

## Scope and limits

The upstream correction targets retained-core teardown callbacks and does not
introduce transaction handles or alter `OperationLifetime.Exclusive`. It merits
an independent upstream safety PR: the native early-release defect and checksum
failure exist without PR #133. PR #133 adds the getter self-wait manifestation.

The regression suite covers five teardown routes, plain/encrypted getter/disposal,
native exclusion on both sides of the callback, no replacement core publication,
other-database and legitimate ordinary same-instance recursion controls, later
writer progress, and exact repeated cold indexed state. The final 24 new cases plus
30 existing peer cases passed 54/54 on each of net8/net10 at `8853dedf1`, including
the no-core-open assertion. net462 compiled with zero errors; it was not executed.
No Windows/macOS execution or power-loss campaign was performed here. Constructor
failure cleanup before a newly constructed LiteEngine is published is outside the
explicit retained-core scope; do not describe this as every possible failed-open
callback path.
