# PR133 review536885 evidence, baseline98a10086c

This additive archive records the two Shared callback admission corrections and
the strengthened handoff test on exact `98a10086c`. Production is identical to
measured `e8559b642`; no source or binary from an earlier attempt is relabeled.

- `candidate-source/`:32 exact changed source files from reviewed569 to98a.
- `review-5368851720/`: author and independent tests/probes, original failures,
  completed partitions, explicit missing-original limits, and exact-final proofs.
- `performance-final7/`:116 processes/580 windows,40 binaries and independent audit.
- `hosted-final/`: exact98a full CI/Safety and all dedicated workflow evidence.
- `hosted-prior-e855/`: failed predecessor evidence, never successor qualification.
- `metadata-history/`: original pending snapshots retained as history.

The full matrix is green. The later systematic concurrency-audit mission opens
an additional finite merge gate; this archive is its baseline, not its outcome.
See acceptance.json and the raw reports for the tested failure models and limits.
Original e855 handoff failure cause remains unknown. No physical device power-loss
or unrestricted deadlock-freedom claim follows from these bounded runs.
