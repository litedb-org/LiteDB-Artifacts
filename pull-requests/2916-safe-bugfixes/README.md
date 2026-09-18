# PR #2916 — [1/4] Safe bugfixes from #2908

Evidence gathered while reviewing https://github.com/litedb-org/LiteDB/pull/2916.

## review-2026-09-18-windows

Independent merge-confidence review. Probes compare production `LiteDB.dll` builds (net8.0, `TestingEnabled=false`)
of the PR base and head, loaded by the same console program.

| | commit |
| --- | --- |
| base (`dev`) | `7d2a16c4313fc24f7904cd558af6528d79128a40` |
| head (`codex/pr2908-safe`) | `e0d0d67ee3166f5caf681c7d00c738a94cc91d8b` |

Environment: Windows 11 x64, .NET SDK 10, probes run on the .NET 10 runtime, machine culture de-AT, time zone
W. Europe Standard Time (UTC+1/+2), NVMe SSD.

### How to reproduce

```sh
# one production DLL per commit, outside the source tree (worktree builds need the explicit version)
dotnet build LiteDB/LiteDB.csproj -c Release -f net8.0 -p:TestingEnabled=false -p:GitVersionEnabled=false \
  -p:AssemblyVersion=6.0.0.0 -p:FileVersion=6.0.0.0 -p:Version=6.0.0 --artifacts-path <scratch>/prod-<variant>

# copy probe.csproj next to a Program.cs, then one probe build per variant.
# The -o directory MUST be outside the probe project directory: a LiteDB.dll below the project directory is picked up
# as a reference candidate and silently wins over the HintPath, so every "variant" would run the first DLL built.
dotnet build -c Release -p:LiteDBAssembly=<scratch>/prod-<variant>/bin/LiteDB/release_net8.0/LiteDB.dll -o <scratch>/out-<variant>
```

Check the size of the `LiteDB.dll` beside each probe exe before trusting a comparison (here: dev 625664, head 628224 bytes).

### Full suite on Windows (non-UTC time zone, comma-decimal culture)

`dotnet test LiteDB.Tests -c Release -p:TestingEnabled=true --settings tests.runsettings`:
net10.0 and net8.0 each **1,189 passed, 7 skipped, 0 failed**.

### Cross-version database files

`../../2918-storage-transaction-bugfixes/review-2026-09-18-windows/file-compat-and-commit-latency` writes a database
(20k documents, two secondary indexes, deletes, a 3 MB and an empty file-storage entry) with one version and verifies
and then writes to it with another. All 12 ordered pairs of {dev, #2916, #2917, #2918}, plain and AES-encrypted: OK.

### Findings reproduced (all three have a drafted fix)

Fix branch: https://github.com/JKamsker/LiteDB/tree/review/pr2916-confidence-fixes (three commits on top of the head
above; full suite on that branch: 1,199 passed, 7 skipped, 0 failed on net10.0).

**1. JSON text of ordinary doubles changes (`2bb440a8`, `JsonWriter` uses `G17`)** — `json-text/Program.cs`

```
value        dev                          head
0.1          0.1                          0.10000000000000001
0.3          0.3                          0.29999999999999999
19.99        19.99                        19.989999999999998
2.675        2.675                        2.6749999999999998
1.0 / 1.5    1.0 / 1.5                    1.0 / 1.5
1e-7         0.0000001                    9.9999999999999995E-08
1e21         1000000000000000000000.0     1E+21
1.0/3        0.333333333  (lossy)         0.33333333333333331
```

Affects `BsonValue.ToString()`, `JsonSerializer.Serialize`, shell/Studio output and JSON export. The commit's goal
(bit-exact round trip; dev truncates to 9 decimals) is right, the chosen format is not the shortest one. Fix `377c0567`:
write `"R"`, re-parse, fall back to `G17` only when the bits differ; adds a test that asserts the text.

**2. Read-only `EnsureIndex` on an index that already exists now throws (`2e5aa746`)** —
`readonly-ensureindex-and-projection/Program.cs`

```
                                              dev                                    head
no -log file, EnsureIndex(existing)           FileNotFoundException, engine dead     NotSupportedException, reads keep working
-log file present, EnsureIndex(existing)      returns False                          NotSupportedException
```

The first row is the improvement the commit is after. The second row is a regression for "EnsureIndex at startup,
open read-only" (a reader next to a live writer, or after an unclean shutdown). Fix `1c457376`: look the index up in a
read snapshot, return `false` when it exists with the same expression, throw only when creation would be needed.

**3. Internal projection marker changes the public `BsonMapper.ToObject` (`48430631`)** — same probe

```
var doc = col.Query().Select("$.Name").ToList()[0];     // {"Name":"n1"}
BsonMapper.Global.ToObject<NameDto>(doc)
  dev : NameDto { Name = "n1" }
  head: InvalidCastException: Unable to cast object of type 'System.String' to type 'NameDto'
```

Fix `6982099f`: keep the author's design (typed materialization still goes through the virtual `ToObject` hook) but
clear the marker on documents handed to the caller as raw `BsonDocument`.

### Reproduced by a review agent only (not re-run by the reviewer)

- `481e6c2d`: a custom deserializer registered for `Derived` that delegates to `mapper.Deserialize(typeof(Base), doc)`
  now recurses without bound (the `_type`-resolved deserializer is honoured on polymorphic reads). On dev that
  delegation was the only route to default materialization.
- `cc003466`: with `EnumAsInteger`, `uint`/`long`/`ulong` enums are stored as Int64 (were Int32); an Int64 that does not
  fit an `int` enum now truncates silently instead of throwing `OverflowException`.
- `bfbc1975`: an oversized integer literal in an expression becomes a String (`$.x > 99999999999999999999` is `false`
  instead of an error); the same token in JSON/SQL document literals still throws.

### Not covered

netstandard2.0 on .NET Framework; Linux lock-contention retry (`a7c697ca`); the four `LiteDB.Shell` commits were read,
not executed, in this review.
