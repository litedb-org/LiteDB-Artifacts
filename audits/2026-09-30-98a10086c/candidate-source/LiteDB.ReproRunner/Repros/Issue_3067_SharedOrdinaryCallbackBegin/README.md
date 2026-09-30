# Handle begin inside an ordinary Shared callback

Production reproduction of [PR 133 review finding 2](https://github.com/JKamsker/LiteDB/pull/133#pullrequestreview-5368851720).
The pinned package `0.0.0-knownbad.569ba13c3b38` is built from actual reviewed
commit `569ba13c3b3867131c8687e7884bed65131edfbf` with `TestingEnabled=false`.

An ordinary write's lazy input runs with a pin operation and zero pin holds, or
a mutex-backed reader's later read-transform callback runs on a transferred thread.
Each callback attempts parameterless handle begin on the same facade or a peer
facade in the same database namespace. All four run plain and encrypted.

Before enclosing ownership is established, an empty handle is opened and rolled
back to cache its inert child wrapper. Read-only reflection retains that exact
child so the known-bad case must observe its native waiter count above zero,
child checkout, occupied local gate, native exclusion and a blocked caller. These
conditions must persist for another second before the isolated process exits.
The fixed callback must refuse without checking out that child or changing its
idle gate/waiter state, register no handle, and pass independent-thread native
exclusion probes before and after refusal. The legitimate outer operation finishes.
Missing reflection metadata is an error, distinct from a null field value.

Controls allow a genuinely leased transferred-reader callback to begin and commit
on either facade, and a non-callback begin to enter the session and wait behind an
idle handle that another thread commits. The latter then commits its own indexed
row. Every case/control requires a real peer-process commit and two cold reopens
checking exact row, index-seek, rejected-write and sentinel models.

An earlier exploratory first-use bounded run observed the exact admission timeout,
local gate acquisition and native exclusion. Those logs are retained as qualified
interim evidence: they cannot distinguish native waiting from holder startup.
This final proof uses actual cached-child native waiters. Permanent unit tests
retain first-handle and bounded-begin coverage; prewarming here only exposes the
specific native boundary without production hooks.

A timeout or startup/reflection/model failure exits 2. The known-bad package must
print `BUG_REPRODUCED` and exit 0; corrected source must print `VERIFIED_FIXED`
and exit 10. Shared harness sources are in
[`../SharedPinCallbackProof`](../SharedPinCallbackProof).

```bash
dotnet run --project LiteDB.ReproRunner/LiteDB.ReproRunner.Cli -- run Issue_3067_SharedOrdinaryCallbackBegin
```
