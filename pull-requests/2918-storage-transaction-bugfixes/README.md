# PR #2918 — [3/4] Independent storage and transaction bugfixes from #2908

Evidence gathered while reviewing https://github.com/litedb-org/LiteDB/pull/2918.

## review-2026-09-18-windows

| | commit |
| --- | --- |
| base (`codex/pr2908-medium`, #2917) | `2508490cc6066908907aeb643e40e42916e29945` |
| head (`codex/pr2908-danger`) | `8c7999b6d1bcf769cce7f14ef8cdc4fcf2d07234` |

Environment and build recipe: see `../2916-safe-bugfixes/README.md` (same machine, same method). `LiteDB.dll` sizes used
to verify each variant: dev 625664, base 707584, head 717312 bytes. Storage: NVMe SSD, NTFS; a raw
`FileStream.Flush(true)` after two 8 KB page writes costs ~0.97 ms here (200 iterations: 193 ms vs 3 ms with `Flush()`).

### Full suite on Windows

net10.0 and net8.0 each: **1,877 passed, 7 skipped, 1 failed** — the failure is the time-zone-dependent test inherited
from #2917 (see `../2917-medium-bugfixes/README.md`), not a #2918 change.

### Cross-version database files — `file-compat-and-commit-latency` (`write`, `verify`)

20k documents, two secondary indexes, `DeleteMany`, a 3 MB and an empty file-storage entry; written by one version, then
verified (every document, both indexes, file bytes) and written to by another. All 12 ordered pairs of
{dev, #2916, #2917, #2918}, plain and AES-encrypted: **OK**. No on-disk format change. Review agents additionally
verified: 25 file states (clean, killed with data only in the WAL, partial tails, zero-byte, tiny, ...) x read-only /
read-write produced by dev and base all open on head with every committed document; `_files`/`_chunks` byte-identical
across versions for 12 file sizes; encrypted 100k-document databases in both directions.

### Durable commit cost on the isolated branch (`5e1e7a7e`) — same probe (`perf`)

```
                          base (#2917)          head (#2918)
200 single inserts        ~10 ms  (0.05 ms)     ~220 ms (1.10 ms per commit)
200 single updates        ~5 ms                 ~210 ms
200 inserts in one tx     ~1.2 ms               ~2.5 ms
InsertBulk 100k           ~320 ms               ~360 ms
```

The extra ~1.05 ms per commit is exactly this disk's fsync cost. One flush per confirmed transaction; batched work is
unaffected. There is no opt-out setting.

### Findings reproduced by the reviewer

**1. Readers on any collection stall behind every commit's fsync** — `reader-stall-and-slow-cursor` (`nohold`)

One writer updating collection `w`, two threads doing `FindById` on a *different* collection `r`, file database.

```
            writes/s      reads/s
base        ~25,000       ~52,000
head        ~600 → 190    ~1,500 → 450
```

Writes dropping is the fsync. Reads dropping 35x is not inherent: `Commit()` holds `lock(_header)` around
`PersistDirtyPages`, which now contains `Flush(true)` (`5e1e7a7e`), and every snapshot constructor, reads included,
takes `lock(_header)` (`03050f5a`). On `:memory:` head matches base (agent result).

**2. One slow cursor throttles every writer** — same probe (`hold`): as above plus one thread enumerating `FindAll()`
with 5 ms per document.

```
            writes/s      reads/s      -log file after 8 s
base        ~31,000       ~62,000      1,532 MB (unbounded: this is #2814)
head        ~55           ~250         8 MB
```

`8c7999b6` makes each commit over the checkpoint threshold wait (10 ms timeout, ~15.6 ms on the Windows timer tick) for
readers to drain, on every commit, with no back-off. The WAL is bounded, at a ~500x throughput cost while any
long-lived reader exists.

**3. `Rollback()` in the usual catch block masks the original exception (`f21d2e2e`)** —
`../2917-medium-bugfixes/review-2026-09-18-windows/updatemany-revisit-and-index-names` (`rb`)

Thread A holds an unrelated explicit transaction. Thread B runs
`try { BeginTrans; Insert(duplicate); Commit } catch { db.Rollback(); throw; }`.

```
base: LiteException: Cannot insert duplicate key in unique index '_id'. The duplicate value is '1'.
head: LiteException: No transaction belongs to this thread, but an explicit transaction is open on another thread. ...
```

The failed insert already rolled B's transaction back, so B legitimately has none; base returned `false`.

### Reproduced by a review agent only

- `be4b01a1`: `LiteStorage.Upload` holds the `_files`/`_chunks` write locks for the whole source-stream copy (a slow
  source makes a concurrent 1 KB upload hit the lock timeout), and a failed upload inside the caller's `BeginTrans` rolls
  the caller's transaction back (`Commit` returns `False`, an earlier insert is lost). Atomicity itself verified: a failed
  overwrite leaves the old file intact (base destroys it), no orphan chunks.
- `f21d2e2e`: async `BeginTrans(); await; Insert(); Commit();` — the guard fired in 7 of 1,600 thread-pool iterations;
  when the continuation lands on a thread that already owns a transaction, `Commit()` completes that one. Transactions
  leak on base and head alike. Do not describe this as "async misuse is now detected".
- `9bebc4d7`: an encrypted file of 1–8191 bytes is now rejected instead of silently re-initialized (intended).
- `c4aebf6c`: a truncated data file now raises `EndOfStreamException` and closes the engine (base: `LiteException`,
  engine stays open).
- Verified good by agents: snapshot-isolation oracle (4 writers, 6 readers, checkpoint size 20, create/drop, 60 s) —
  base dies within seconds (#2792), head 0 violations; cache frame reuse under a 256–512 KB cache with checksummed
  documents, 0 torn/foreign pages, memory bounded; cursor disposed on another thread — base
  `LockRecursionException` in 288/800 iterations, head 800/800; `Rebuild` on an encrypted database — base writes a
  plaintext file and dies, head keeps encryption, data, indexes and files; 16 public-API failure scenarios do not stop
  the engine (`b54b00dd`); generated-id sequences identical to base incl. 8 threads, no throughput cost.

### Not covered

HDD / network / cloud-disk fsync cost; power loss (process kill is not power loss); multi-process `Shared` mode under
the new checkpoint wait; real I/O faults during `Commit`/`Rollback` on a file; v4/v7 upgrade paths; AES databases in
the concurrency stress probes; netstandard2.0 on .NET Framework.

## fixes-2026-09-18 (same machine, same probes)

All three findings above were fixed in the PR itself: head `eca59a9ce0a2d8ac8bd51fb3819dc07288427891` (history rewritten:
14 commits re-stacked on the fixed #2917 + 3 fix commits; previous head `8c7999b6`). Full suite net10.0 / net8.0:
1,996 passed, 7 skipped, 0 failed.

Same probes against a production build of the fixed head (`LiteDB.dll` 728576 bytes):

```
reader-stall (nohold), s6-s8:   writes/s ~940-990    reads/s ~130,000-146,000    log 3-4 MB     (was ~190 / ~450)
slow cursor  (hold),   s6-s8:   writes/s ~915-945    reads/s ~130,000-141,000    log 41-55 MB   (was ~55 / ~250, log 8 MB)
rollback masking:               caller sees: LiteException: Cannot insert duplicate key in unique index '_id'. ...
commit latency:                 200 single inserts ~200 ms (1.0 ms/commit) | 200 in one tx 2-4 ms | InsertBulk 100k ~310 ms
cross-version files:            24/24 ordered pairs of {dev, fixed #2916, fixed #2917, fixed #2918}, plain + AES: OK
```

With the cursor held the log grows at the write rate (~7 MB/s here): a checkpoint needs zero open transactions, so this
is inherent; the earlier 8 MB was a side effect of the 55 writes/s throttle, not a bound. Writes are fsync-bound by
design (#2818); there is no opt-out setting.
