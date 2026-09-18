# PR #2917 — [2/4] Eyeball, then yolo bugfixes from #2908

Evidence gathered while reviewing https://github.com/litedb-org/LiteDB/pull/2917.

## review-2026-09-18-windows

| | commit |
| --- | --- |
| base (`codex/pr2908-safe`, #2916) | `e0d0d67ee3166f5caf681c7d00c738a94cc91d8b` |
| head (`codex/pr2908-medium`) | `2508490cc6066908907aeb643e40e42916e29945` |

Environment and build recipe: see `../2916-safe-bugfixes/README.md` (same machine, same method). `LiteDB.dll` sizes used
to verify each variant: base 628224, head 707584 bytes. Default collation on this machine: `de-DE/IgnoreCase`.

### Full suite on Windows

net10.0 and net8.0 each: **1,664 passed, 7 skipped, 1 failed**. The failure is a test bug, not a product bug:
`Issue2798_Tests.Interface_DateTime_id_range_uses_the_concrete_id_mapping_for_query_and_delete` compares UTC
expectations with `BsonValue.AsDateTime`, which LiteDB returns as local time, so it only passes where local time is UTC
(CI). Fix: https://github.com/JKamsker/LiteDB/tree/review/pr2917-tz-test (`ad042ce2`).

### Findings reproduced by the reviewer

**1. `UpdateMany` revisits documents or never returns (`55ca9aba`)** — `updatemany-revisit-and-index-names` (`hw`)

5,000 documents, index on `k`, `k = _id % 100`.

```
                                                              base            head
UpdateMany("{k:$.k+1, n:$.n+1}", "$.k BETWEEN 10 AND 20")     returns 550     returns 3300
  documents updated more than once                            0               500
  SUM(k) afterwards (correct: 248050)                         248050          250800
UpdateMany("{k:$.k+1000}", "$.k >= 0")                        returns 5000    still running after 20 s
```

The commit removes the per-document de-duplication from scalar index scans; an update that moves the indexed key forward
meets the same document again. Silent wrong data. No test in the suite updates the key it is scanning.

**2. `StartsWith` / `LIKE 'x%'` loses the index seek under culture collations, i.e. the default (`e298acbe`)** —
`startswith-seek-and-stringcomparison`, 200k documents, index on `Name`

```
                                   base                              head
plan                               INDEX SEEK (+RANGE SCAN)          FULL INDEX SCAN
x.Name.StartsWith("name00001")     0.09 ms/query                     58.12 ms/query
SQL  Name LIKE 'name00001%'        0.42 ms/query                     67.65 ms/query
```

Results are correct on head (base could return wrong rows under culture collations), but every prefix search on a
default database becomes a full index scan.

**3. `StringComparison` overloads throw on null fields and stop using the index (`0dfa5f3c`, `2508490c`)** — same probe

```
col.Count(x => x.Opt.Equals("x1", StringComparison.OrdinalIgnoreCase))     // a third of the rows have Opt == null
  base: 1
  head: NullReferenceException: String.Equals requires a non-null receiver.
x.Name.Equals(v, OrdinalIgnoreCase) plan:  base INDEX SEEK(Name = ...)   head FULL INDEX SCAN(_id)
```

Empty strings are stored as null by default (`EmptyStringToNull`), so they trigger it too. Base ignored the mode.

**4. LINQ on the `Id` of an abstract base class throws (`6b45f722`)** — `mapper-abstract-id-dst-ctor`

```
abstract class Vehicle { public int Id { get; set; } }   class Car : Vehicle { }
db.GetCollection<Vehicle>("v").Insert(new Car { Id = 1 });
v.Find(x => x.Id == 1)      base: 1 row
                            head: NotSupportedException: Known implementations disagree on the _id mapping for Vehicle.Id.
```

Also `DeleteMany`. Happens once any concrete subtype has been mapped.

**5. Writes throw for local times in the DST gap, and take the caller's transaction with them (`6de66d9c`)** — same probe

```
Insert(new Ev { When = new DateTime(2026, 3, 29, 2, 30, 0) })     // Kind=Unspecified, W. Europe Standard Time
  base: stored as 2026-03-29 01:30:00Z
  head: ArgumentException: Invalid local time cannot be stored as a UTC DateTime.

BeginTrans; Insert #200; try { Insert #201 (gap date) } catch {}; Insert #202; Commit
  base: Commit = True,  rows 200/201/202 = present/present/present
  head: Commit = False, rows 200/201/202 = missing/missing/present
```

Depends on the machine time zone (never on a UTC server). A review agent additionally showed date-only values being
rejected in zones that switch at midnight (Chile, Egypt) by swapping the cached local zone.

**6. Constructor-bound members: stored value no longer applied after the constructor (`0737b187`)** — same probe

```
class Person { public Person(int id, string name) { Id = id; }  public string Name { get; set; } }   // ctor ignores name
stored {_id:1, Name:"alice"}     base: Name = "alice"     head: Name = null
```

**7. Auto index names change, so `EnsureIndex` duplicates indexes after upgrade (`a2bedc0d`)** —
`updatemany-revisit-and-index-names` (`idx-create` with base, then `idx-ensure` with head, same file)

```
created by base:   _id, Gre=$.Größe, nave=$.naïve
after head:        _id, Gre=$.Größe, nave=$.naïve, Größe=$.Größe, naïve=$.naïve      (both EnsureIndex calls returned True)
```

Any field name with a non-ASCII letter; under tr-TR also ASCII names with a capital `I` (agent result).

### Reproduced by a review agent only

- `241e5441`: exceptions from member getters / custom serializers are now wrapped in `LiteException` (code 221), so
  `catch (MyException)` around `Insert` no longer matches; `ToDocument(string|int|List<int>)` throws instead of
  returning null.
- `bb473b4b`: a base class declaring both `BaseTwoId` and `ChildTwoId` now maps `_id` to `ChildTwoId` for `ChildTwo`
  (was `BaseTwoId`); existing data is silently mis-mapped. Narrow.
- `e0e4b04b`: implicit `ulong` → `BsonValue` stores Int64 bits (was Double); queries built from raw `BsonValue` against
  old Double data above `long.MaxValue` change result.
- `0737b187`: `ToObject` allocations on a plain POCO with five children 1144 → 2416 B/op.
- Verified good by agents: LIKE matcher vs. an independent reference over ~950k (pattern, value) pairs in five
  collations (one mismatch, also on base; base has hundreds and hangs on `%%X`); 123 ordinary LINQ predicates produce
  identical expression text and plans; deferred-compilation cache under 16 threads; pagination/aggregate oracle inside
  and outside transactions; 150k-row disk-spilling sort; 300k-insert transaction; header-lock stress (no deadlock);
  ObjectId uniqueness across 8 threads and unchanged cost; offset scans 8–11x faster.

### Not covered

`0a7278c7` (parameterized helpers), `b2b8507f` (ordering aliases), `dbdfb4b7` (`CtorOnly`) were not probed;
`67f5c34d`, `c2d20520`, `bf8e5117`, `ff48dc20`, `53808ded`, `36ea6c74` were read only. netstandard2.0 on .NET Framework.

## fixes-2026-09-18 (same machine, same probes)

All findings above were fixed in the PR itself: head `f81ec16923751bb1a708f97ea4898f9261bc7427` (history rewritten: the 47
commits re-stacked on the fixed #2916, #2357 replaced by an opt-in version, 7 fix commits; previous head `2508490c`).
Full suite net10.0 / net8.0: 1,775 passed, 7 skipped, 0 failed.

Same probes against a production build of the fixed head (`LiteDB.dll` 716800 bytes, default collation de-DE/IgnoreCase):

```
StartsWith plan: INDEX SEEK (+RANGE SCAN)(Name LIKE "name00001%")     0.09 ms/query   (was FULL INDEX SCAN, 58 ms)
SQL LIKE 'name00001%':                                                 0.34 ms/query   (was 68 ms)
Opt.Equals(v, OrdinalIgnoreCase) => 1            Opt.StartsWith(v, Ordinal) => 74076   (were NullReferenceException)
abstract base: Find(x => x.Id == 1).Count => 1   DeleteMany(x => x.Id == 99) => 0      (were NotSupportedException)
insert DST-gap Unspecified 2026-03-29 02:30 => 2026-03-29 01:30:00Z ; commit => True ; rows 200/201/202 present   (switch off = base)
ctor(id,name) not assigning name: Name => 'alice'                                       (was null)
UpdateMany k+1 WHERE k BETWEEN 10 AND 20: returned 550, docs updated more than once: 0, SUM(k)=248050
UpdateMany k+1000 WHERE k>=0: returned 5000                                             (was a hang)
index names, created by the ORIGINAL #2916 build then ensured by the fixed build:
  EnsureIndex($.Größe) => False, EnsureIndex($.naïve) => False ; indexes: _id, Gre, nave    (were duplicated)
```

Still by design: `Name.Equals(v, OrdinalIgnoreCase)` plans as FULL INDEX SCAN (results correct; only `Ordinal` equality
can be narrowed soundly without knowing the collation).
