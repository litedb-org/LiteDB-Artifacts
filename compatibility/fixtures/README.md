# Regression fixtures for litedb-org/LiteDB

Binary fixtures consumed by `LiteDB.Tests` and `LiteDB.ReproRunner` through `LiteDB.Tests/Resources/artifacts.json` (`ArtifactFixtures.Path`). Extracted for the #3051 split of PRs #3025/#3027; bytes are identical to `litedb-org/LiteDB` commit 3d136a46f `LiteDB.Tests/Resources/<file>`. Generators live in the LiteDB repo (`tools/RegressionFixtures5021`, `tools/PrereleaseVectorFixture`).

## SHA-256

```
d47ec8e861374618066f9467bcc5979ad38ff9ea782ee8d81279f3e95c4cd131  ConcurrentWalCrash_5_0_21.zip
939cbf90a733d4b9b8ab268aa579d61e3a68463500a3f45d7f5c36303b60a999  DamagedDocument_5_0_21.zip
ea5cf8ae44699ad3f153066f9210fc260126d060a20337afa56a54fd51b4b029  DropIndex_5_0_21.zip
bdc7ce05c9098a98bc6b1e7b2fcf001bfa5da8d2961f2ec71170871a851bae92  EncryptedWalCrash_5_0_21.zip
5fccf47b7fba0b1ab3d1682fc698de1657f72f77ec6d08240c7442232876cdfc  ForeignWal_5_0_21.zip
419ac1e867e8af98354f826137fa4dd7ee31df81ff5017b71f15600db745ffa1  Vectors_6_0_0_prerelease_114.zip
3ecc257b66fd94112f39f3b275d7e182c18ecb2e5ebeaa3361fafa682243679a  WalCrash_5_0_21.zip
```

## Provenance recorded by slice S05

### S05 staged artifacts: legacy vector/index metadata compatibility

Upload target: `litedb-org/LiteDB-Artifacts`, path `compatibility/fixtures/<file>`.
Consumer manifest: `LiteDB.Tests/Resources/artifacts.json` on branch `split/05-vector-compatibility`
(the `revision` still pins `88b857f6fde39742cac2ad5399c9372bf54a9b3c`; bump it together with the upload).

The bytes are identical to the blobs committed at `split/src-3027` (3d136a46f):

| File | git blob at split/src-3027 | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| `DropIndex_5_0_21.zip` | `12d7cd10130877b6fffc1fdb196d6c4e800acfcc` | 19,142 | `ea5cf8ae44699ad3f153066f9210fc260126d060a20337afa56a54fd51b4b029` |
| `Vectors_6_0_0_prerelease_114.zip` | `cc005c19adef8c9e963fad9b2f3b3ebdd8309161` | 20,491 | `419ac1e867e8af98354f826137fa4dd7ee31df81ff5017b71f15600db745ffa1` |

Entry hashes, from `LiteDB.Tests/Resources/RegressionFixtures.md` at split/src-3027:

| Archive | Entry | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| DropIndex_5_0_21.zip | `customers.db` | 106,496 | `f35d8ecad0572e4173140b544d1b49177ed4779dbdf8fd0adff37314b835e34c` |
| | `items-a.db` | 57,344 | `07119a463046f1a2894e00a280cbef85d2e23abb8d25c414a7b8989410bd51f7` |
| | `items-b.db` | 81,920 | `272d71c7b08c7e442c23b258d53a1aea35672d7bd9a620a0f1b68d50cb7b287f` |
| Vectors_6_0_0_prerelease_114.zip | `vectors.db` | 90,112 | `538abeb144036eece8068af2543b0ee22140fcd17af53fc29d633d09afce4f5c` |
| | `vectors-encrypted.db` | 98,304 | `605ec25fd7c257af74d2c9bd9de19e8b5d9441cf0a7fccbe7154608c94e6bcf2` |
| | `vector-salvage.db` | 40,960 | `9dd0174b37712814fff89468b31c58cd64c61275b0581983c8bfba20da55d5e2` |

#### Producers

| Package | SHA-256 of the `.nupkg` | Assembly loaded |
| --- | --- | --- |
| LiteDB 5.0.21 | `938a4f5f9d6a28383de8ccfcbdb8897e78432399215232cf38dbf147e03ec167` | `lib/netstandard2.0/LiteDB.dll` 5.0.21.0, SHA-256 `ae31ac6a93549217b9e8b81497d1ee831658ade6df9e1ee20f2838f22de5e218` |
| LiteDB 6.0.0-prerelease.114 | `0d23347a50f1ab9e9bac4e0fb27d33b9187be017a4dd90469843480103eff54e` | `lib/net8.0/LiteDB.dll` 6.0.0.0, SHA-256 `b4055de8b53452c0df6bd2e462ec78f0271a4574ed991a9cf7b27d3924685af6` |

Environment: written 2026-09-27/28 (UTC) on Linux x64 (Ubuntu 24.04.4 LTS, kernel 6.18.44, ext4),
.NET SDK 10.0.401. `LANG` was unset, so the invariant culture was current (collation LCID 127,
IgnoreCase). DropIndex ran on .NET 10.0.12 (Release); Vectors ran on .NET 8.0.31 (vectors.db and
vectors-encrypted.db from a Debug build).

#### DropIndex_5_0_21.zip

- Introduced at #3027 `e8785c4d6`; `items-a.db`/`items-b.db` added in `ac2a81f48`.
- Generator: `tools/RegressionFixtures5021/DropIndex.cs` (on this branch). Its host
  (`tools/RegressionFixtures5021/Program.cs` + `RegressionFixtures5021.csproj`, with the
  `["DropIndex"] = DropIndex.Generate` registry row and the `drop-` child dispatch) lands with S06.
  Command once the host exists:
  `DOTNET_ROLL_FORWARD=LatestMajor dotnet run --project tools/RegressionFixtures5021 -- DropIndex <empty-dir>`.
- Recipe: each file is written by its own process with `new LiteDatabase(path)` and closed
  normally; nothing is patched.
  - `customers.db`: collection `customers`, indexes Name, Age, CustomerId, 200 documents
    `{_id: i, Name: "n"+i, Age: i%90, CustomerId: "C"+i}`, then `DropIndex("Age")`.
  - `items-a.db`: collection `items`, 50 documents `{_id: i, <field>: <field>+i}`, indexes
    CreatedAt, Phone, Status; drop CreatedAt, Phone.
  - `items-b.db`: same shape, indexes LastLogin, Score, CustomerId, Country, Status; drop
    CustomerId, LastLogin, Country, Score. Two of 40 seeded random layouts, the two that failed.
- Archive made with Python `zipfile` (ZIP_DEFLATED), entries in the order above.
- A regeneration differs only in the header creation time (bytes 68..75) and ZIP metadata.
- Expected behavior: 5.0.21 itself keeps inserting, updating, deleting and indexing in all three.
  The current engine's writable open migrates each file and the collection stays writable (exact
  counts and index lookups after reopen); every interrupted-migration image recovers writable; a
  vector index added after migration keeps its metadata. Known bad `6.0.0-prerelease.319` fails
  the first insert with `LiteException` 999 "request page must be less or equals lastest page in
  data file".
- Consumers: `LiteDB.Tests/Regressions/LegacyDroppedIndex_Tests.cs` (S05), ReproRunner
  `Issue_3027_LegacyDroppedIndex` (S05, via linked `ArtifactFixtures`); later
  `LegacyReadOnlyStream_Tests`, `ReadOnlyStorageEngine_Tests` and the
  `Issue_3027_LegacyReadOnlyStream` repro (S07); `scripts/test-5021-regression-compatibility.py`
  `ARCHIVES` (S06 scaffolding).

#### Vectors_6_0_0_prerelease_114.zip

- Introduced at #3027 `cf92a407d`; `vector-salvage.db` added in `e94c95b1a` (first two entries unchanged).
- Generator: `tools/PrereleaseVectorFixture` (on this branch), package LiteDB 6.0.0-prerelease.114:
  `dotnet run --project tools/PrereleaseVectorFixture -- <empty-dir>`.
- Recipe:
  - `vectors.db` / `vectors-encrypted.db` (no password / `Password=vector-secret`): collection
    `docs` `{_id: 1..40, name: "d"+i, Embedding: [i, 1]}` with index `name` and vector index
    `embedding` on `$.Embedding` (2 dims); collection `computed` `{_id: 1..20, Embedding: [1, i]}`,
    every fifth Embedding null, vector index `coalesced` on `COALESCE($.Embedding, [0, 0])`.
    Not patched.
  - `vector-salvage.db` (separate process): collection `vectors` `{_id: 1..3, a: "keep-i",
    b: "u-i", v: [i, 1]}` with vector index `vv` on
    `IIF(SUBSTRING(COALESCE($.b, 'x'), 1, 2) = '-0', $.v, $.v)`, checkpointed and closed;
    post-processing overwrote the BSON length of `b` in document 2 with `0x7FFFFFF0`.
- A regeneration differs in the creation time, the vector pages (unseeded level randomizer), one
  byte of vector metadata per collection page, the encryption salt, and ZIP metadata; logical
  content is identical.
- Expected behavior: a writable open migrates `vectors.db`/`vectors-encrypted.db` and their vector
  indexes plus the ordinary `name` index stay usable; Rebuild and Auto-Rebuild keep them with no
  `_rebuild_errors`; in-test copies with a renamed section entry, a 5.0.21-style rewritten index
  list, or a section naming other indexes open read-only, fail a writable open with
  `INVALID_DATAFILE_STATE` "vector index 'embedding' has no vector metadata", and Auto-Rebuild
  drops only that index and lists it in `_rebuild_errors`. For `vector-salvage.db`,
  `Auto-Rebuild=true` keeps documents 1 and 3 and index `vv`, and reports document 2 because its
  vector value cannot be computed (needs S04 partial-document salvage; not asserted on S05 alone).
- Consumer: `LiteDB.Tests/Regressions/PrereleaseVectorFile_Tests.cs` (S05; the `vector-salvage.db`
  test is deferred to the integrated S04+S05 tree).

## Provenance recorded by slice S06

### S06 staged artifacts (legacy WAL validation and conversion drain)

Destination in litedb-org/LiteDB-Artifacts: `compatibility/fixtures/<file>`. Each file was extracted
byte-for-byte from #3027's head (`split/src-3027` = 3d136a46f,
`git show split/src-3027:LiteDB.Tests/Resources/<file>`); nothing was regenerated. The full
provenance record is `LiteDB.Tests/Resources/RegressionFixtures.md` on branch `split/06-legacy-wal`.

Common to all four:
- Producer: NuGet package LiteDB 5.0.21 (`.nupkg` SHA-256
  `938a4f5f9d6a28383de8ccfcbdb8897e78432399215232cf38dbf147e03ec167`; `lib/netstandard2.0/LiteDB.dll`
  SHA-256 `ae31ac6a93549217b9e8b81497d1ee831658ade6df9e1ee20f2838f22de5e218`).
- Host: Linux x64, Ubuntu 24.04.4 LTS, kernel 6.18.44, ext4, .NET SDK 10.0.401, invariant culture
  (collation LCID 127, IgnoreCase), written 2026-09-27/28 UTC.
- Generator: `dotnet run --project tools/RegressionFixtures5021 -- <Fixture> <empty-dir>` (then zip the
  entries in the listed order). Regenerated bytes differ (creation time, crash timing, ZIP metadata,
  encryption salt); the logical content and consumer outcome must match.

| File | SHA-256 | Bytes | Runtime | Entries (bytes, SHA-256) | Generator subcommand |
| --- | --- | ---: | --- | --- | --- |
| `WalCrash_5_0_21.zip` | `3ecc257b66fd94112f39f3b275d7e182c18ecb2e5ebeaa3361fafa682243679a` | 13,956 | .NET 10.0.12 | `crash.db` (40,960, `a02cbef3…2bbce7`), `crash-log.db` (516,096, `2ae0e11a…726259`) | `WalCrash` |
| `EncryptedWalCrash_5_0_21.zip` | `bdc7ce05c9098a98bc6b1e7b2fcf001bfa5da8d2961f2ec71170871a851bae92` | 18,821 | .NET 10.0.12 | `crash.db` (49,152, `80d9897f…7c417c4`), `crash-log.db` (57,344, `61d08c16…fcec1f`) | `EncryptedWalCrash` |
| `ConcurrentWalCrash_5_0_21.zip` | `d47ec8e861374618066f9467bcc5979ad38ff9ea782ee8d81279f3e95c4cd131` | 2,556 | .NET 8.0.31 | `c.db` (57,344, `e28b1668…fee87b692`), `c-log.db` (106,496, `c6c960a5…c89883`) | `ConcurrentWalCrash` (zipped with Info-ZIP) |
| `ForeignWal_5_0_21.zip` | `5fccf47b7fba0b1ab3d1682fc698de1657f72f77ec6d08240c7442232876cdfc` | 451 | .NET 8.0.31 | `foreign-log.db` (32,768, `0ef3c0ef…d5a519a`) | `ForeignWal` |

Consumers and expected behavior:
- `WalCrash_5_0_21.zip`: process-crash image, 100 docs checkpointed + 21 WAL-only commits. Current engine
  converts on a writable open keeping 101 docs / 21 with value 7; read-only legacy scan reads the same
  without writing; a live shared-reader lease or unreadable reader registry refuses the conversion with
  LOCK_TIMEOUT, both files byte-identical, then converts after unblocking; an appended committed page
  beyond both files or not a page fails with INVALID_DATABASE, files unchanged. Consumers:
  `LegacyWalSharedMigration_Tests`, `LegacyWalTornTail_Tests`, `LegacyWalPageBound_Tests`,
  `scripts/test-5021-regression-compatibility.py`, repros `Issue_3027_LegacyWalConversion` and
  `Issue_3027_ForeignLegacyWal`. Later consumers (S09): `UnsyncableDataFile_Tests`,
  `DataFileStopsSyncing_Tests`, `UnsyncedBackfillPowerLoss_Tests`, `KeptWalStop*`.
- `EncryptedWalCrash_5_0_21.zip`: encrypted crash image (password `wal-secret`), 65 docs / 15 with value 7;
  converts with a torn tail of 16 or 100 bytes. Consumer: `LegacyWalTornTail_Tests`.
- `ConcurrentWalCrash_5_0_21.zip`: 5.0.21 concurrent-writer crash; committed pages 58/59 above the data
  file's LastPageID 6 are legitimate; writable and read-only opens read a=10, b=13 (3 with _id >= 100).
  Consumers: `LegacyWalPageBound_Tests`, `scripts/test-5021-regression-compatibility.py`.
- `ForeignWal_5_0_21.zip`: WAL of another 5.0.21 database, paired by the tests with WalCrash's `crash.db`;
  every open (default, read-only legacy scan, Auto-Rebuild) fails with INVALID_DATABASE "commits the header
  of another database", both files unchanged. Consumers: `LegacyWalPageBound_Tests`,
  `scripts/test-5021-regression-compatibility.py`, repro `Issue_3027_ForeignLegacyWal`
  (which also pins the entry digests).

`LiteDB.Tests/Resources/artifacts.json` on `split/06-legacy-wal` lists these four with
`"path": "compatibility/fixtures/<file>"`; `revision` is unchanged (`88b857f6…`) and must be bumped
when they are uploaded.

## Provenance recorded by slice S07

### S07 staged fixtures (consumed, not produced, by S07)

S07 generates no new binary. Both files are byte-exact copies of split/src-3027
(`LiteDB.Tests/Resources/<file>`), staged so S07's tests and repro can resolve them via
`ArtifactFixtures` with the same pins as their owners. Identical bytes to the owners' staging.

| File | Path in LiteDB-Artifacts | sha256 | Owner (generator) | Producer | S07 consumers |
|---|---|---|---|---|---|
| DropIndex_5_0_21.zip | compatibility/fixtures/DropIndex_5_0_21.zip | ea5cf8ae44699ad3f153066f9210fc260126d060a20337afa56a54fd51b4b029 | S05 (`tools/RegressionFixtures5021/DropIndex.cs`) | LiteDB 5.0.21 package | LegacyReadOnlyStream_Tests, ReadOnlyStorageEngine_Tests.Checkpoint_and_rebuild_of_read_only_streams_do_nothing_as_in_5_0_21, repro Issue_3027_LegacyReadOnlyStream |
| DamagedDocument_5_0_21.zip | compatibility/fixtures/DamagedDocument_5_0_21.zip | 939cbf90a733d4b9b8ab268aa579d61e3a68463500a3f45d7f5c36303b60a999 | S04 | LiteDB 5.0.21 package | ReadOnlyStorageEngine_Tests.Damaged_5_0_21_file_over_a_read_only_stream_reports_the_damage_and_changes_nothing |

Expected consumer behavior: opened over a non-writable stream, each file opens read-only
(`$database.readOnly = true`), reads its documents, and no byte of data or log changes.
Generator commands, runtime and recipes: see the owning slices' MANIFEST.md.

