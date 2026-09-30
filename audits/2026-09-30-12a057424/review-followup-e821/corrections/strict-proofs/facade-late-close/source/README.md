# Late ordinary reader callback closes its facade

A callback reached by the second ordinary reader `Read()` calls its owning
`LiteDatabase.Dispose()`. Raw Direct and Shared `FOR UPDATE` storage must drain
that same executing reader, so the facade must reject the callback close before
publishing session closing. Independently leased Shared snapshots are the positive
control: their retained storage permits facade close and reader completion.

This public-API proof runs plain and encrypted files. The known-bad PR commit
`e821ae7479dcb83570031180b0c0e3f4fd167793` returns the specific session
`TimeoutException` after its ten-second close deadline. The fixed source must
return `InvalidOperationException` within two seconds and remain writable. Both
paths finish the reader, retry close, prove native writer release with a separate
Shared writer process, and verify indexed data and a sentinel on
two cold reopens. Unexpected errors, a generic watchdog expiry, or ambiguous
latency fail with exit 2; only the exact defect gives exit 0 and
`REGRESSION_REPRODUCED`. Verified fixed behavior gives exit 10 and
`FIXED_STATE_VERIFIED`. Four known-bad callbacks take roughly forty seconds.

Permanent tests additionally exercise mutex-backed unleased snapshots, pooled
Direct readers, non-owning facades, and legitimate cross-thread close/drain.
Both variants use a printed short system-temporary-volume path; runner artifact
paths can exceed the legacy native mutex-name limit.

This is lifecycle/exception evidence, not power-loss evidence.
