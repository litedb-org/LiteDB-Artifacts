# Final pinned-writer progress and failure-retention harness

Candidate12a057424 has unchanged baf112 production. The correction separates child startup, native admission and committed progress, preserves a live pin owner for the handoff assertion, and retains failed fixtures only after the host and every child exit. Primary failures survive cleanup failures; child-stop error recording no longer depends on diagnostic publication. Independent reviews found and corrected the owner-death false-pass and retention/exception gaps. No original timeout cause or new production fix is claimed.

Root30-case net8/net10 runs use814 source; the final tiny diagnostic-key edit landed during the net10 run. Those runs are not relabeled exact12a evidence. Later exact12a six-case runs pass on both runtimes. Fixer logs show8cases/runtime including companions and9collector controls. Policy101, Frameworkcompile and exact12a all-TFM production build are retained. Counts overlap and must not be summed.

The initial retention command omitted --output and failed argument parsing; that output remains a setup failure. Original f95pin-timeout evidence stays in hosted-interim-f95. Current benchmark and full hosted gate remain pending; no publication is implied.
