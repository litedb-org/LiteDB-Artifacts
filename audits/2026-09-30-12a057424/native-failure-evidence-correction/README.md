# Native crash failure diagnostics and artifact retention

Original f95 job 109884239520 failed reading a marker/state file after child kill with a sharing violation at 847ms. The original Windows fixture was not uploaded and is missing. Its lock owner/root cause remains unknown. original-hosted-failure.log and source provenance retain that limit.

The correction keeps the prior success sequence unchanged and runs ownership diagnostics only on failure, then retains files after host exit. implementation/ and independent/ contain real executions of intentionally failing synthetic database scenarios. Native controls cover 24cases/20recovery markers; graph controls cover sixcases. They prove byte-identical capture of synthetic test fixtures, not reproduction or reconstruction of the missing original fixture. Initial/follow-up/reviewer runs overlap and must not be summed.

Database/WAL binaries remain byte-for-byte copies. Text path normalization and final checksums are deferred to publication. No product cause, Windows reproduction or current final gate is claimed.
