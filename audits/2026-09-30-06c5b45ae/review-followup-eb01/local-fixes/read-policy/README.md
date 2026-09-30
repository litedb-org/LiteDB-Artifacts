# ReadTransform policy comparison

The bounded stale-delegate allegation was not reproduced: constructor-selected settings are snapshotted for both ordinary operations and handles, on both c8c0cfab and eb01. The retained delegate’s mutable target remains observable through wrapper reuse. The public probe covers four initial/replacement combinations and target mutation.

16 tests pass on each runtime, including four new guards and existing lifetime/settings tests. Production behavior was unchanged; source and instrumentation qualifiers are explicit in provenance.json. Counts overlap broader suites and must not be summed. Exact compared library bytes are retained; this is behavioral evidence, not a benchmark.
