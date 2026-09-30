# Callback close probe budget correction

Candidatef95 is test/doc/ledger-only and has the exact full tree of local tested303c153a9. No library code changed: production tree95f947 matches measured190. Benchmark binaries remain identified as190 builds; full f95 hosted qualification is still required.

The original Windowslatest/x64/net8 failure on190 occurred during outer using cleanup after callback refusal and post-refusal write checks had succeeded. The fixture's100ms CloseWaitOverride unintentionally remained active for that unrelated cleanup. The precise original scheduling/drain duration remains unknown; this archive does not claim a new product fix or a proven infrastructure cause.

The override now surrounds only unsafe callback modes0/4/5 and restores the previous value in finally. Safe modes1/2/3 and outer cleanup use the normal deadline. The exact revised oracle still fails against real e821:6 unsafe failures and6 safe successes per runtime, with the expected callback exception mismatch. Corrected303c/f95 passes18cases on each runtime. All original hosted output, before/fixed results and corrected source are retained. Counts overlap broader suites and are not summed.

Current policy96 passes; deliberate negative-control error messages in its log are expected tests. This record is source-specific local evidence, not final CI success.

A second historical190 failure is retained: Windows2022/x64/net10, mode0/password secret, same outerusingline58,515ms overall. It shares the fixture-scope disposition; no new production mechanism is inferred. The old workflow ended with twofailed and twocancelled jobs after successor push, so its incomplete results cannot qualify f95.
