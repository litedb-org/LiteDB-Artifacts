# Independent Shared admission review

Reviewed baseline: 569ba13c3b3867131c8687e7884bed65131edfbf, Release production net10.0 assembly from safety-production. No tracked code edits.

Source confirms both review5368851720 acquisition gaps. Executed eight additional bounded public-API admission cases: ordinary pinned input callback and transferred ForUpdate reader callback, each same/peer facade and plain/encrypted. All reached TimeoutException at 500-504 ms rather than rejecting the impossible synchronous dependency. All outer operations could complete after caught timeout; cold reopen retained original rows and outer ordinary writes. This is liveness evidence, not acknowledged commit loss or a measurement of infinite deadlock.

The pin cases assert actual local leased-reader registration, initially absent pin, then pin operations=1/holds=0 in callback. The transferred-reader cases execute two Reads on the foreign thread; the first consumes Query-prefetch and would be vacuous. The second invokes exactly one late ReadTransform callback.

A parent-only IsExecutingOwnedCoreOnCurrentThread guard does not fix peer-facade same-namespace callbacks. This was reproduced and communicated to the implementer before integration.

Reviewed F1 d6f0e7f8e source and 14 new cases: common WriteDatabase guard precedes existing-pin entry/start/retirement; no source defect found. Core/holder/native semantics are unchanged. Ordinary other-database pin, normal pin and independent idle-handle completion controls discriminate over-rejection.

Pending: inspect F2 candidate and combined final source/test evidence. Suggested controls: both mutex-backed SharedDataReader constructors, Query-prefetch before fallback snapshot publication, transferred second-Read callback count, truly leased independent callback, nested other-database composition, idle ownership releasable by another thread. Thread-static scopes must remove all engine references in finally and must not follow Task execution context.

## Candidate checks

Reviewed final F2 working production diff and 22 focused cases, including nested other-database failure restoration, throwing transferred reader callback, Query-prefetch before publication, no admission before refusal, native exclusion, true leased-reader and idle-pin controls. No further in-scope source defect found. F2 was subsequently identified by implementer commit below; baseline and candidate source patches retained separately.

Independent console probes rerun against F2 hook-enabled candidate net8 and net10 assemblies: all ten formerly timing-out cases throw InvalidOperationException in 0-1 ms, outer ordinary writes/cold original rows preserved, transferred callback count exactly one, initial Query callbacks refuse before returning a truly leased snapshot. These probes are observational programs; probe-validation.json records explicit parsing of all ten outcome types and all branch/cold-state markers, rather than interpreting process exit0 as a regression oracle. Fixed production final integration not yet executed by this reviewer.

Candidate logs/binaries/production patch/base revision retained in candidate-net8 and candidate-net10. Exact569 production baseline logs/binaries/source are frozen under baseline-frozen. Earlier four/eight-case logs are historical subsets; their exact intermediate binaries were not retained. Do not identify them as independent final binaries. The authoritative ten-case baseline was rerun with the final source and its full executable layout was retained.

Scope limits: this review is specific to handle admission and the reviewed synchronous native-dependency branches. Bounded 500-ms admission observations do not independently establish an infinite hang, process-death behavior, full CI or physical power-loss durability. Existing writer/reader lifetime semantics were traced, not redesigned. The parent task owns strict process-isolated proof, broader suites and final exact-head qualification.
