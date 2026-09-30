# Shared close versus a later reader callback

Reproduces [review finding 2 on fork PR #133](https://github.com/JKamsker/LiteDB/pull/133#pullrequestreview-5365276527), pinned to the actual reviewed revision `e821ae7479dcb83570031180b0c0e3f4fd167793`.

The public raw `SharedEngine` owns a legacy transaction and a multi-row reader.
Only after `Query` returns and the first pre-materialized row is consumed does
the program arm `ReadTransform`. The second-row callback pauses; a dedicated
thread calls only `Dispose`, and its `WaitSleepJoin` state establishes that it
has reached a wait while that callback is live. The callback then calls the raw
Shared `Pragma` method. No product hooks, private reflection, or test assemblies
are used. Both variants place databases in a short path on the system temporary
volume and print that path; long runner artifact paths can exceed the legacy
native-mutex name limit and are not used as database paths.

The known-bad result requires both callback and disposer to remain alive and
waiting after the bounded joins, with no callback/read/close error. A setup
failure or unrelated timeout fails the proof. The reproduction process exits
with those background threads still blocked, so the known-bad case cannot hang
the runner indefinitely. Plain and encrypted cases must agree.

The fixed result requires the callback's independent call to be refused, the
reader and disposer to finish, a separate process to commit a Shared write, and
a cold Direct reopen to match the committed records and secondary index while
excluding the interrupted legacy transaction's write and preserving an unrelated
sentinel. It also checks that the value-index query plan selects the index.

Outcomes are strict: known bad exits `0` with `BUG_REPRODUCED`; fixed source exits
`10` with `VERIFIED_FIXED`; unexpected failures exit `1` and do not satisfy either
expectation. This is bounded Linux/process/thread/exception evidence, not a
power-loss test or a claim that all callback patterns are safe.

```bash
RestoreAdditionalProjectSources=/path/to/pinned-feed \
  dotnet run --project LiteDB.ReproRunner/LiteDB.ReproRunner.Cli -- \
  run Issue_3067_SharedLateReaderClose --report artifacts_temp/late-reader-close.json
```
