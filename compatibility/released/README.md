# Released LiteDB database corpus

56 synthetic healthy databases: plain and encrypted files written by each of the 22 stable NuGet v5 packages (5.0.0–5.0.21) and six stable v4 packages (4.0.0, 4.1.0–4.1.4). Prerelease packages are excluded. No deployment databases are stored here.

Generator source: [LiteDB commit 95d195988](https://github.com/JKamsker/LiteDB/tree/95d195988515c57752061548f0f0b778808a9421/tools/ReleaseCompatibility), with [generation script](https://github.com/JKamsker/LiteDB/blob/95d195988515c57752061548f0f0b778808a9421/scripts/generate-release-corpus.py).

Generated on Linux x64 using SDK 10.0.400 to compile the writer and the pinned Microsoft .NET Core 3.1 runtime image recorded in `manifest.json` to execute it. Original released NuGet assemblies are used unchanged. Runtime choice matters: early v5 writers generate invalid scan links on .NET 8. Every fixture passed complete scans and all 1,024 primary-key payload checks with its own writer after close/reopen. The writer drains queries fully to avoid the released 5.0.18 early-disposal transaction leak.

Each database contains four collections with integer, Guid, ObjectId and string primary keys; 256 documents each; secondary unique and nonunique indexes; Unicode; numeric extrema and decimal/double fractions; nulls and booleans; timestamps; nested and empty containers; binary values up to 20 KB. V5 files additionally contain computed and multikey indexes. Plain v5 uses Ordinal collation; encrypted v5 uses en-US/IgnoreCase. V4 uses its original collation behavior. The test password is `release-corpus`.

The manifest records SHA-256 hashes of each ZIP, uncompressed database, original NuGet package and writer LiteDB assembly, plus generator/corpus hashes and the exact runtime image digest. ZIP members contain a single checkpointed database, with no required sidecars. Consumers must verify these hashes and test disposable copies.

Regenerate on Linux with Docker:

```sh
python3 scripts/generate-release-corpus.py /path/to/LiteDB-Artifacts/compatibility/released
```

LiteDB CI pins this repository by commit. Its compatibility runner verifies read-only access (v5 scan fallback), byte-preserving v4 rejection, explicit v4 upgrade, normal direct writes, payloads and index results, rollback, and persistence after two reopens. The source issue is [#3022](https://github.com/litedb-org/LiteDB/issues/3022). The original damaged attachment is consumed at its public issue URL, not republished here.
