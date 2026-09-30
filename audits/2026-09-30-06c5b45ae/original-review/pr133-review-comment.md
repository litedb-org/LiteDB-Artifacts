## Review of PR head `5605290`

Five parallel reviewers covered the handle API, session lifetime, Shared holder reuse, engine internals, and tests/CI. Seven defects were confirmed by running a repro, four of them also compared against the base `49c327cf1`. One was confirmed by tracing the code. One hang was confirmed by a repro but only occurs in an edge case.

The two regressions in the first section should block merging.

### Regressions

**1. Disposing a Shared database while one thread has an open legacy transaction can hang another thread forever.**
Where: [`SessionLifetime.cs:154`](https://github.com/JKamsker/LiteDB/blob/560529066aeda64c24d5cdd32d34a8aca12695ae/LiteDB/Client/Transactions/SessionLifetime.cs#L154) together with [`LiteDatabase.cs:425`](https://github.com/JKamsker/LiteDB/blob/560529066aeda64c24d5cdd32d34a8aca12695ae/LiteDB/Client/Database/LiteDatabase.cs#L425).

Close only disposes the engine once `_active == 0`. In Shared mode, though, a peer waiting in `SharedMutexOwner.Enter` is only freed by that engine disposal (`_owner.ReleaseAll()`).

Steps:
1. Thread T calls `BeginTrans()` and inserts.
2. Thread U inserts and blocks.
3. T calls `db.Dispose()`.

At head: `Dispose` throws `TimeoutException`, T can no longer Commit or Rollback (`ObjectDisposedException`), U stays blocked, and the cross-process writer mutex stays held until T's thread exits.
At base: Dispose succeeds, and U gets a `LiteException` after about 1 s.

**2. `db.Rebuild()` is starved by ordinary concurrent reads.**
Where: [`OperationLifetime.cs:69-83`](https://github.com/JKamsker/LiteDB/blob/560529066aeda64c24d5cdd32d34a8aca12695ae/LiteDB/Engine/Services/OperationLifetime.cs#L69-L83), used by `Rebuild.cs:26`.

`Exclusive()` checks `_active == 0` every 10 ms, but `Enter()` only blocks once `_exclusive` is actually set. New readers are never held back while rebuild waits. The old `EnterExclusive()` stopped new readers as soon as it started waiting.

With 4 reader threads and `TIMEOUT=5s`:

| | Result |
| --- | --- |
| Base | Rebuild succeeds in about 450 ms |
| Head | `LockTimeout("operation/maintenance")` after 5 s |

**3. `LiteEngine.Dispose()` can wait forever.**
Where: [`LiteEngine.cs:235`](https://github.com/JKamsker/LiteDB/blob/560529066aeda64c24d5cdd32d34a8aca12695ae/LiteDB/Engine/LiteEngine.cs#L235), which calls `Exclusive(() => true)` with no timeout. The root cause is the same as #2.

- A raw engine with 16 reader threads was still blocked in Dispose after 20 s.
- A peer waiting on a collection lock stalls Dispose for the full lock timeout. On base, Dispose returned in 100 ms.

`LiteDatabase` mostly avoids this because its session close drains work first. Raw `LiteEngine` users hit it directly.

### Other defects

**4. `BeginTransaction()` hangs when the calling thread already holds the Shared lock through another path.**
Where: [`SharedEngine.Transactions.cs:62`](https://github.com/JKamsker/LiteDB/blob/560529066aeda64c24d5cdd32d34a8aca12695ae/LiteDB/Client/Shared/SharedEngine.Transactions.cs#L62).

The guard only checks `_owner.IsOwnedByCurrentThread`. It misses two cases:
- A legacy transaction that took over a pin from a streaming reader, i.e. `Execute("SELECT $ …")`, then a write, then `BeginTrans()`.
- An open `SELECT … FOR UPDATE` reader.

In both cases the holder worker waits for a lock that only the blocked caller can release. With the default timeout this never ends; the intended behavior is an immediate `InvalidOperationException`. With a 3 s timeout it fails with `TimeoutException` instead.

**5. Using a bound reader or enumerator after disposing it rolls back the whole transaction.**
Where: [`TransactionEnumerable.cs:21-23`](https://github.com/JKamsker/LiteDB/blob/560529066aeda64c24d5cdd32d34a8aca12695ae/LiteDB/Client/Transactions/TransactionEnumerable.cs#L21-L23) and [`:40-44`](https://github.com/JKamsker/LiteDB/blob/560529066aeda64c24d5cdd32d34a8aca12695ae/LiteDB/Client/Transactions/TransactionEnumerable.cs#L40-L44).

`Dispose` sets `_inner = null`. A later `Read()`, `MoveNext()` or `Current` then throws `NullReferenceException` inside `Run`, which aborts the handle: the state becomes `Failed` and every earlier write is silently lost.

Ordinary readers throw `ObjectDisposedException` here and leave the transaction alone. Even an `ObjectDisposedException` would still abort the handle through `Run`, so both parts need fixing.

**6. `ILiteTransaction.Dispose()` throws if the database is closing at the same time.**
Where: [`LiteTransaction.cs:216-227`](https://github.com/JKamsker/LiteDB/blob/560529066aeda64c24d5cdd32d34a8aca12695ae/LiteDB/Client/Transactions/LiteTransaction.cs#L216-L227).

The state check and `Enter()` happen under separate locks. Depending on timing:
- Close has started but hasn't reached this handle yet: `ObjectDisposedException`.
- The close worker is rolling this handle back right now: `InvalidOperationException("Overlapping transaction disposal…")`.

Either exception masks the one coming out of the `using` body. The bound enumerator and reader `Dispose` behave the same way. The handle still ends up RolledBack, so nothing leaks.

**7. Disposing a healthy handle rethrows another transaction's fatal error.**
Where: [`LiteTransaction.cs:249`](https://github.com/JKamsker/LiteDB/blob/560529066aeda64c24d5cdd32d34a8aca12695ae/LiteDB/Client/Transactions/LiteTransaction.cs#L249), leading to `:155-163`.

`Abort` skips the rollback when the engine is already disposed (`onlyIfActive: true`), but `Dispose`/`RollbackCore` does not.

Example: tx2's commit hits an injected disk-write failure, which stops the Direct host. `tx1.Dispose()` then throws tx2's `IOException`.

**8. A stale `<db>-tmp` sort file is no longer deleted (low severity).**
Where: [`SortDisk.cs:111`](https://github.com/JKamsker/LiteDB/blob/560529066aeda64c24d5cdd32d34a8aca12695ae/LiteDB/Engine/Sort/SortDisk.cs#L111).

The file is now only deleted if the current engine instance spilled to it. A `-tmp` file left behind by a crashed process stays until some later process spills.

### Test and CI gaps

**Vacuous test:** [`TransactionHandleProcess_Tests.cs:110-111`](https://github.com/JKamsker/LiteDB/blob/560529066aeda64c24d5cdd32d34a8aca12695ae/LiteDB.Tests/Internals/TransactionHandleProcess_Tests.cs#L110-L111). The test asserts the child is blocked only 100 ms after it prints `attempting`. An unblocked child takes about 165 ms to finish anyway, so the test passes even if cross-process waiting is broken.
Fix: print a marker from `TransactionAdmission.Observe("native-wait")` and wait for it before asserting.

**Weak (plausible):** `TransactionHandleLifetime_Tests.cs:100` and `:127` accept `ObjectDisposedException`. On a slow runner they can pass without exercising the "cancel pending begin" path.
Fix: gate on `Observe` counts and require `OperationCanceledException`.

**No .NET Framework coverage of Shared wrapper reuse:** all of `TransactionHandleChild*_Tests` is `#if !NETFRAMEWORK`. ChildLifetime and ChildSettings are excluded only because their helpers live in the ChildReuse file, and they compile for net462 once the helpers are moved. The netstandard2.0 build also skips the `NET8_0_OR_GREATER` coordination reset.

**Untested behavior:**
- Several handles running concurrently in Direct mode; tests and fuzzing are sequential only.
- Drop or rename on a writable handle.
- `$cols` / `$indexes` queries through a handle.
- Rejection of custom `ILiteEngine`s; only the manual tool checks it.
- The same-thread legacy-transaction guard.

**Undocumented public break:** using a disposed `LiteDatabase` now throws `ObjectDisposedException` instead of `LiteException(ENGINE_DISPOSED)`. The migration section of `docs/transaction-handles.md` doesn't mention this.

### Checked and found sound

- **Binding:** no collection, query, Include, vector or SQL path runs outside the handle.
- **Handle concurrency:** overlapping use and callback reentry are detected correctly.
- **Shared holder:** no page cache survives between handles, the mutex is released on the thread that acquired it, no job is lost when a pool worker retires, and settings invalidation is complete.
- **Test edits:** the changes to existing tests are justified.
- **Test runs:** the scoped test suite passed 218/218, including repeated runs under full CPU load.
