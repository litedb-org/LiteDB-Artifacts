Derived report: machine-specific directories and the original machine-bound command block were omitted. Timing values and limitations are retained. Source digest is in reports/source-provenance.json. Portable harness instructions are in ../upgrade-harness/README.md.

# Bounded upgrade-cost evidence

Source: `7fca9be9558043f7ceb270c6efddc2f80222e718`, isolated `<RAM_WORKSPACE>`; production Release `TestingEnabled=false`. Runtime .NET 8.0.30, SDK 10.0.400; Linux 6.8.0-139-generic x86_64.

Released writer: actual NuGet LiteDB 5.0.21 netstandard2.0 binary (not patched). Four cases: 10,000 / 50,000 documents, plain/encrypted, binary collation, checkpoint pragma 0. Each row has _id, ObjectId crossing signed timestamp boundary, scalar score, decimal 0.1, and 128-character payload + ID. Indexes: _id, oid, score, and `IIF($.number = DOUBLE($.number), 1, 0)`. Released writer verified computed key 1; current reader verified key 0 and selected computed index plan. A separate cold collection was preserved.

| Rows | Mode | DB bytes | Upgrade open ms | Explicit checkpoint ms | Fresh-process reopened ms | Warm repeat range ms | Total data write bytes | Total WAL write bytes | Peak WAL bytes |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 10000 | plain | 5292032 | 841.46 | 21.09 | 50.06 | 0.28–3.05 | 2146304 | 2211840 | 2162688 |
| 10000 | encrypted | 5300224 | 926.82 | 23.60 | 55.79 | 5.03–7.43 | 2146304 | 2211890 | 2170880 |
| 50000 | plain | 26214400 | 2168.85 | 24.33 | 47.83 | 0.27–2.70 | 10461184 | 18286912 | 10534912 |
| 50000 | encrypted | 26222592 | 2409.95 | 32.37 | 53.99 | 4.32–7.54 | 10461184 | 18286962 | 10543104 |

All cases passed full serialized-payload equality, all score-index result sets, independently ordered ObjectId index results, computed-index results and plan, unrelated collection preservation, and three same-process plus one fresh-process repeated opens. Every repeated open wrote zero bytes. Encrypted repeated opens requested two data-stream flushes but wrote no data.

Files migrated v8 → v11; page counts and LastPageID stayed unchanged. 10k: 646 pages (257 index / 386 data / 3 header+collection). 50k: 3200 pages (1272 index / 1925 data / 3 header+collection). SHA-256 of every original document page at its original page ID matched after checkpoint. This establishes index/metadata work without document rewriting for these fixtures. Default Auto did not repromote the reopened BSON documents to v12.

First-open data writes were exactly 16,384 bytes (two header publications); indexes remained in WAL until explicit checkpoint. Total checkpoint-inclusive writes are shown above. The 50k WAL write count exceeds peak length because safepoints reuse slots. No replacement database was created by the stream-based probe.

Limits: single first-upgrade observation per case, three warm repetitions, one new-process reopen; not a statistical or hardware benchmark. RAM-backed <RAM_FILESYSTEM> removes real storage latency/durability/SSD wear. Counts are bytes submitted to underlying FileStream.Write, not filesystem/device write amplification. Instrumented caller-owned FileStreams exercise real physical files and encryption but do not measure filename factory or directory-sync costs. Full payload/index verification ran outside open/checkpoint timing. Before timing, page inspection warms OS caches; new-process reopen has cold engine JIT but warm filesystem caches. No large-index, constrained-capacity, collation-transition, crash, or concurrent-reader performance claim is made. Retained database/WAL files occupy 63,045,632 bytes in <RAM_FILESYSTEM>.
