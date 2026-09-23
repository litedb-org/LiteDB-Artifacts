# Bounded upgrade probe harness

Program.cs is byte-unchanged from the retained synthetic probe; its source SHA-256 is listed in source-provenance.json. The fixed cost-fixture password is public synthetic test data, not a credential. Reader.csproj and Writer.csproj are explicitly derived: only original machine-specific assembly locations are replaced by MSBuild properties. No executables, real databases, or original JSONL output paths are included.

Provenance: production source [7fca9be9558043f7ceb270c6efddc2f80222e718](https://github.com/litedb-org/LiteDB/commit/7fca9be9558043f7ceb270c6efddc2f80222e718), Release TestingEnabled=false; Linux x64/.NET 8.0.30, SDK 10.0.400. Released writer is the unmodified LiteDB 5.0.21 NuGet assembly. Measurements used RAM-backed files and warm filesystem caches.

Reproduce by building LiteDB at that commit with Release/net8.0/TestingEnabled=false. Restore the LiteDB 5.0.21 package separately. Build Writer.csproj with -p:LegacyLiteDBAssembly=<absolute NuGet DLL path>, and Reader.csproj with -p:CurrentLiteDBAssembly=<absolute production DLL path>; use separate output/intermediate directories. Run Writer.dll <RAM-backed database filename> <10000 or 50000> <plain or encrypted>, then Reader.dll with the same arguments. Run Reader.dll in a new process once more for cold-engine reopening. Each executable prints its engine hash; inspect those before comparing measurements.

Exact assembly hashes from the original measurement:
- Released DLL: AE31AC6A93549217B9E8B81497D1EE831658ADE6DF9E1EE20F2838F22DE5E218
- Production DLL: C87F8C5A3A92ED90367040E195066A75962525E4932E2AB3D82701414A2CD434

The portable project variants were not rerun for this artifact preparation. This bundle records the original observation and source; it does not claim a new benchmark result.
