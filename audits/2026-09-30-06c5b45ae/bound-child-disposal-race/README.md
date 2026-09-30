Candidate03d4e610a based on3e88454e2.

Before: production3e88454e2 plus four new plaintext forced-interleaving cases (Direct/Shared x reader/enumerator); all four failed. The retainedTRX names the exact four cases. Final candidate expands the same scenario to encrypted variants and adds controls proving genuine inner Read(ObjectDisposedException) and Dispose(IOException) still abort. No new hooks: holding the existing private handle admission gate and observing a dedicated thread blocked there forces the capture/disposal/admission sequence.

Final scoped56/56 passes on both net8 andnet10. Failure model ordinary exception/concurrency; twice cold record/index/sentinel state verification.
