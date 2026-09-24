# PR #3003: shared-mode benchmark

Like-for-like shared-mode benchmark for [litedb-org/LiteDB#3003](https://github.com/litedb-org/LiteDB/pull/3003).

- **Source commits:**
  - pre-stack dev `0fd277aae`
  - dev with the merged storage stack `fc9cd5509`
  - PR head `f0228d301`
- **Environment:** AMD Ryzen 9 9955HX, NVMe, Windows 11, .NET 10.0.11, durable commits on (the default).
- **Method:** each build's `LiteDB.dll` (Release, net10.0) runs under the same harness. Each configuration runs 3 times, interleaved round-robin. Each run uses a fresh database, which is deleted afterwards.

| File | Content |
|---|---|
| `benchmark.md` | Results table (median, min–max), p99 latencies and observations |
| `benchmark.csv` | One row per scenario × build × mode × round |
| `raw.txt` | Raw harness output |
| `harness/` | Harness source (`Program.cs`, `fbench.csproj`), matrix runner (`run.sh`) and report generator (`report.py`) |

To reproduce:
1. Build LiteDB at each commit.
2. Build `harness/fbench.csproj` once per build against that build's `LiteDB.dll`, into `$WORK_DIR/fbench-bin/{pre,dev,imp}/`.
3. Run `WORK_DIR=<dir> harness/run.sh`, then `WORK_DIR=<dir> python harness/report.py`.
