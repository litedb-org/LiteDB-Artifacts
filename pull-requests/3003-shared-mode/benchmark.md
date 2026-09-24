# Shared-mode benchmark: pre-stack dev vs dev now vs improvements

All numbers come from one harness (`harness/Program.cs`), compiled against each build's `LiteDB.dll`. Assembly hashes were verified next to each harness binary.

- **Builds** (LiteDB Release net10.0, production build without TESTING hooks):
  - pre-stack dev: `0fd277aae`
  - dev now (storage stack merged): `fc9cd5509`
  - improved: `f0228d301` (`wip/shared-salvage`: #3003 salvage, #3007, #3005, #3006, lazy close checkpoint, pending-release fix, open handles, native fsync, review fixes, coordinator + zero-IPC reads)
- **Machine:** AMD Ryzen 9 9955HX (16 cores), Samsung MZVLC1T0 NVMe (C:), Windows 11, .NET 10.0.11. Default durable commits.
- **Method:**
  - A fresh 2,000-row database (about 200-byte documents) per run, deleted right after.
  - 3 rounds. Within each round and scenario, the builds and modes run round-robin, so machine noise spreads across all variants.
  - Cells show the **median** (min–max).
  - The coordinator runs as a separate host process, and the measured process is a client.
  - "Held reader": a second connection keeps a `FindAll` cursor open while the writer updates. In pre-stack shared mode the writer never finished within 10 s (reported as blocked).

| Scenario | Shared, pre-stack dev | Shared, dev now | Shared, improved | Coordinator, improved | Direct, pre-stack | Direct, dev now | Direct, improved |
|---|---|---|---|---|---|---|---|
| Update loop (2000), ms/op | **3.95** (3.86–4.35) | **8.90** (8.82–9.05) | **2.62** (2.61–2.98) | **1.86** (1.65–1.89) | **1.11** (1.04–1.13) | **1.22** (1.16–1.26) | **1.21** (1.21–1.22) |
| Insert loop (2000), ms/op | **4.83** (4.57–5.39) | **9.22** (9.17–9.27) | **3.31** (3.20–3.57) | **2.00** (1.97–2.02) | **1.16** (1.16–1.18) | **1.32** (1.31–1.89) | **1.32** (1.31–1.39) |
| Point read FindById (4000), ms/op | **0.495** (0.436–0.501) | **0.687** (0.680–0.745) | **0.667** (0.644–0.701) | **0.042** (0.040–0.044) | **0.036** (0.036–0.038) | **0.038** (0.038–0.045) | **0.038** (0.038–0.041) |
| 1 update : 9 reads (2000), ms/op | **0.806** (0.774–0.816) | **1.38** (1.31–1.40) | **1.35** (1.34–1.74) | **0.325** (0.317–0.356) | **0.156** (0.145–0.164) | **0.172** (0.167–0.177) | **0.170** (0.158–0.174) |
| Full scan 2000 rows (200), ms/op | **5.92** (5.90–6.09) | **9.19** (8.94–9.42) | **9.43** (9.21–9.56) | **4.29** (4.11–4.29) | **3.96** (3.94–4.05) | **4.11** (4.03–4.14) | **4.32** (4.15–4.42) |
| Update while another connection holds a reader (500), ms/op | blocked (writer waits for the reader) | **5.69** (5.29–5.69) | **5.05** (4.99–5.13) | **1.83** (1.72–2.02) | not possible | not possible | not possible |
| 2000 updates while iterating a cursor, total s | **2.24** (2.21–2.25) | **3.16** (3.11–3.22) | **3.22** (3.16–3.87) | **4.43** (4.39–4.46) | **2.28** (2.18–2.37) | **2.80** (2.71–2.81) | **2.72** (2.71–2.83) |

| Scenario (p99 ms, median of 3 runs) | Shared, pre-stack dev | Shared, dev now | Shared, improved | Coordinator, improved | Direct, pre-stack | Direct, dev now | Direct, improved |
|---|---|---|---|---|---|---|---|
| Update loop (2000) | 11.28 | 33.28 | 10.82 | 5.10 | 2.04 | 2.31 | 4.41 |
| Insert loop (2000) | 15.35 | 34.21 | 12.16 | 6.64 | 4.60 | 5.84 | 4.33 |
| Point read FindById (4000) | 1.14 | 1.44 | 1.40 | 0.08 | 0.07 | 0.07 | 0.08 |
| 1 update : 9 reads (2000) | 3.95 | 7.74 | 3.58 | 2.09 | 1.22 | 1.33 | 1.37 |
| Full scan 2000 rows (200) | 24.48 | 29.54 | 37.81 | 22.41 | 18.64 | 10.64 | 21.34 |
| Update while another connection holds a reader (500) | blocked | 27.15 | 23.50 | 5.69 | n/a | n/a | n/a |
| 2000 updates while iterating a cursor | 2.22 | 3.47 | 5.42 | 7.42 | 2.04 | 5.08 | 3.32 |
