# Shared handle callback starting an ordinary pin write

Production reproduction of [PR 133 review finding 1](https://github.com/JKamsker/LiteDB/pull/133#pullrequestreview-5368851720).
The pinned package `0.0.0-knownbad.569ba13c3b38` is built from actual reviewed
commit `569ba13c3b3867131c8687e7884bed65131edfbf` with `TestingEnabled=false`.

An identity read transform establishes an independently leased anchor registered
on the callback thread, with no initial pin. A handle writes row 20, then its
lazy input attempts an ordinary write through the originating Shared connection.
The known-bad child must show parent native waiters, an occupied native turnstile,
native exclusion and a blocked callback; a process timeout alone fails the proof.
The fixed callback catches refusal, independently verifies native exclusion before
and after it, checks its active handle and earlier write, then commits or rolls back.

Both outcomes run plain and encrypted. Controls establish a normal pin start,
a pin start in another database with its own leased anchor, the no-anchor refusal,
and an ordinary caller whose idle handle owner commits on another thread.
Every case/control permits a real peer process to commit afterward and checks two
cold reopens against exact row, index-seek, rejected-write and sentinel models.
Known-bad background threads are ended by isolated process exit only after the
native boundary is proven. Each child has a ready/go handshake and hard deadline;
startup, reflection, timeout and model errors are unexpected failures (exit 2).

Package execution must print `BUG_REPRODUCED` and exit 0; corrected source must
print `VERIFIED_FIXED` and exit 10. Shared harness sources are in
[`../SharedPinCallbackProof`](../SharedPinCallbackProof).

```bash
dotnet run --project LiteDB.ReproRunner/LiteDB.ReproRunner.Cli -- run Issue_3067_SharedPinCallbackWrite
```
