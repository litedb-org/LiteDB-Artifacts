# Cross-process pin progress evidence

Candidate `eb01f346eb7d8d51925a45c2f87ef40e1c3984ee` has the exact full tree of local test source `1391b23d0c42fb0a2d638b0ff452c0f598665d9f`. The local net8/net10 final runs each pass 15 cases, including plain and encrypted negative controls with a live owner holding a real transaction. The earlier 13-case net8 review is retained as intermediate evidence; these overlapping counts must not be summed. Local Linux tests do not replace final hosted Windows qualification.

The original Windows x86/net8 timeout on `010821392` remains failed evidence in `../hosted-interim-010821/`. Its cause is unknown: the old final-marker timeout cannot distinguish child startup, native admission, insert progress, or output delivery. No particular product deadlock or runner slowdown is established.

The corrected harness separates bounded startup from actual completed-write progress, keeps a dedicated pin-owner thread alive, observes native admission, retains all twenty child inserts and the final parent update, and checks exact cold IDs, payloads, indexed results and a sentinel. Diagnostic messages cannot renew the progress deadline. Startup and per-commit bounds are 20 seconds; the overall cap is 60 seconds and the session cap remains 300 seconds. This deliberately changes the old combined startup/throughput budget; it is not an unchanged retry. Held-pin negative controls must reject missing commit progress before their owner exits and verify no child inserts on cold reopen.

No production code changed. The complete `LiteDB/` diff from measured `3b60d5aea` is empty, so `../performance-final2/` remains applicable. Candidate patch, harness sources, raw-source hash mappings and parsed TRX counts are retained here. Hosted completion remains pending.
