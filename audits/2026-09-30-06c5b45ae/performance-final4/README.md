# Final19065 production measurements

Measured library source:19065cc1e6b14ee697f7512fa9e65dbe00a8b882; LiteDB tree:95f947dfbda13d002c2a2f6b0d83a1e2777b4807.

Production Release/net10, TestingEnabled=false, .NET10.0.11, Ubuntu24.04.3x64/ext4, tiering disabled. Four alternating fresh-process rounds per case; five seconds warmup plus five one-second measured windows. No local build/test/profile or artifact collection/compression overlapped. Text editing and metadata inspection continued; other host load was uncontrolled.

All84 processes exited zero and passed independent expected records/index/sentinel checks and cold reopen; all420 windows retained. source-index.json records validation of measured DLL hashes and every archived binary. Configuration and raw metadata identify runner, library, revision and process ordering. bench-head is the pre-reuse c8c0cfab6 baseline; bench-dev is current dev5dd942a73; bench-parent is stacked parent49c327cf1. bench-final4 holds the exact final library with the same runner. The baseline executables do not imply support for handle APIs on dev/parent; those versions run ordinary/legacy comparisons only.

Summaries report medians of process rates and process-wide bytes/operation, including holder-thread allocation. Paired gains are medians of final/baseline rates matched by repeat; they need not equal the quotient of displayed medians. No samples were dropped. The archived Python runner and summarizer replay/validate these files; replace normalized workspace paths in configs with local binary paths. The runner source is included, and exact binaries are authoritative for this campaign.

One-read Shared handles:1302.2→1703.9tx/s, paired1.308490×;237908→231078bytes/tx. Zero reads:1.292054×; ten reads:1.244560×. The historical4.69×/~155KB result was not reproduced. This is read-only transaction lifecycle throughput, not durable-write/fsync throughput. Current versus pre-reuse results include required storage-core/mode-admission/coordination cleanup and intervening correctness changes; they do not isolate every cost to wrapper reuse.

Ordinary/legacy regressions remain explicit in final4-upstream-paired.json. These exact-source measurements supersede performance-final3 (7e13/e821), performance-final2 (3b60) and performance (f84) for final19065 claims. Earlier campaigns remain separate, unfavorable results included. No cross-campaign speedup attribution is made.
