# Evidence for LiteDB#3027 at f04c62d34 (2026-09-29)

- `corpus-hash-bisect.txt`: the three pinned fuzz crash seeds replayed (.NET 10) at each implementation
  commit since the pushed head a2e966a8c; input/trace hash prefixes per run. compact-crash 2937 changes
  only at 7f53e4539; the checksum-crash seeds at 7f53e4539, 2b4f1ac0f, e4b9553fd and 042db3fa8.
- `full-suite-*`: partitioned full-suite summaries (Linux x64). b92a0ad05: 5,143 passed, 1 failed
  (FrameFailure_Tests, fixed by 57b0db2e6), 7 skipped on .NET 10 and .NET 8. f04c62d34 (merged with
  dev 5dd942a73): 5,197 passed, 0 failed, 3 skipped on .NET 10.
- `SyncCost_Probe_Tests.cs`: the fsync-count probe behind the PR's cost table. dev 5dd942a73: direct
  open+insert+dispose 2+3, CHECKPOINT=0 0+1, shared per operation 0+1, full checkpoint 2+2, retiring
  partial 3+8 then 2+5, first commit after retiring 0+1. This branch: 3+3, 0+1, 0+1, 3+2, 5+9 then 3+6, 0+2.
- `ReviewA_Tests.cs`, `ReviewB_Tests.cs`: the independent reviewers' repro tests (reviews of
  a2e966a8c..172f667ee). Confirmed findings became HeaderFrameRebuildReader_Tests, the HeaderFrame_Tests
  torn-sector case, FreshEngineLogBarrier_Tests and RecordedFailure_Tests (64f4c6d4c, 5ec242f49,
  a5b00d4fa). Not adopted: the sort-spill repro (pre-existing on dev, listed as a residual), the
  encrypted partial-sector restore (needs a drive defect), the shared slot-reuse sync (implementation
  note 15, in progress).
