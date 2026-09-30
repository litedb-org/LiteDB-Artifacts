# Independent final7 performance audit

Bounded clean. Audited measured source `e8559b642b34449e0843c9e74860a3eb5817d0c7`, production tree `4c82e64ec435862987ccb0d1061dc6eadc34510f` independently of the campaign operator. No measurement was rerun or discarded during this audit.

Recomputed all 116 process rates and count-weighted allocations directly from all 580 raw windows, then recomputed version medians, same-round paired ratios, paired medians, and both allocation reduction conventions. Both summary files and both supplemental paired files agree. Configurations, alternating forward/reverse order, all exit codes, runtime/architecture/tiering/revision metadata, window numbering, complete latency sampling, empty stderr, and final correctness markers match all expected runs. No extra raw run files are present.

All 40 actual measured binary/support files match inventory size/SHA256. Every raw loaded-library hash matches its actual configured DLL. All 32 prior inventory entries remain unchanged. The final DLL matches the clean e855 production checkout. Portable-PDB options record Release optimization and no TESTING or DEBUG define. Reviewed-to-measured tracked runner source has no changes. All 294 indexed archive files match both original sources and staged copies.

The PR production tables, rounded paired comparisons, allocation values, and statements about the direction of all four pairs agree with independently recomputed results. One-read Shared handle throughput is 1,302.5 to 1,687.0 tx/s: median paired improvement 29.6083%, ratio range 1.25846–1.31756. Median allocation falls from 237,915.784 to 230,933.340 bytes/tx, a 2.93484% reduction. The 4.69× experimental result remains explicitly unreproduced. Regressions against dev/parent and reviewed 569 are disclosed.

Limits: host load was uncontrolled; operator freeze is not independently provable from result windows. These read-only transactions measure lifecycle work, not durable-write throughput or isolated guard costs. The runner verifies returned point-read values, cold query results on an indexed field, and a sentinel; it does not assert an index-seek plan. Successor source must demonstrate production-tree equivalence and retain e855 as the measured revision.

Recomputation script: `final7-independent-performance-audit.py`. Detailed results: `final7-independent-performance-review.json`.
