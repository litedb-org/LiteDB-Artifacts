# Reader retirement and leased self-disposal historical proofs

Known bad: actual reviewed f95f0b0cd59d35d285ac72183589975751e35229, packed as 0.0.0-knownbad.f95f0b0cd59d. Production candidates: P1 842b61df7 and P2 e0cd19dba combined; repeat includes P1 test-only followup57f55278c. Repro source commits a2afa687c and documentation0afc4ce41.

Each attempt has its own directory and was retained without overwriting earlier results. Each contains loaded-configuration handshakes and strict dispositions in report.json, full runner.log, exact revision, captured source, exact loaded package/source build outputs and hashes. Builds use Release net8.0 production defaults (no TestingEnabled=true). Platform: Linux. No private reflection or product test hooks in either proof.

All recorded runs passed strict before/after expectations. P1's public pin setup stopwatch starts before the write and requires write+BeginTrans below50ms, with at most3 separately logged attempts; the source README states this bounded public timing assumption. No slow setup or generic timeout is a known-bad or fixed outcome. Actual semantic pin/core barriers and the non-pin owner-exit variant are independently covered by permanent tests; the public historical P1 proof claims the forced-pin case only. P2 keeps the allegedly disposed reader/parent/holder strongly reachable through Direct admission, defeating accidental finalizer cleanup.

True plain uses null password everywhere, including normalization of the empty CLI transport string; encrypted cases use a nonempty password. Both proofs include non-dependent/normal controls, actual peer process writes after successful cleanup and two cold indexed-model/sentinel checks. No corruption, process-power-loss or device-reset guarantee is inferred.
