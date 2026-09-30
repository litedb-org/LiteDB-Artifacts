# PR #133 concurrency audit

## Frozen baseline and finite gate

The starting PR head is `98a10086c2a296040a345e62ea987d59fc2ab487`,
stacked on PR #132, with base `5dd942a7367c361fadd600be4ce10aace2768b27`.
The baseline production tree is `4c82e64ec435862987ccb0d1061dc6eadc34510f`.
Its full CI and Safety aggregate passed before this audit. Those results do not
establish coverage of the new schedules or qualify later audit commits.

This audit is bounded to transaction/session/reader ownership, Shared native
admission and retirement, callbacks, maintenance and process death. It does not
attempt arbitrary application-level wait graphs or physical device power loss.
The existing modeled persistence-fault targets remain separate evidence.

The gate consists of:

1. An acquisition-site inventory and resource dependency graph for the affected
   lifecycle, with each identified feasible cycle tied to a deterministic case
   or a concrete justification that the cycle cannot occur.
2. A reusable deterministic actor explorer in the existing test/fuzz infrastructure,
   covering selected pairwise and three-actor interleavings across Direct/Shared,
   plain/encrypted and same/peer facades. Recorded schedules must establish actual
   overlap and support replay, with an independent committed-state oracle.
3. A multiprocess campaign in the existing fuzz runner, with observed ownership
   and persistence boundaries, parent-recorded acknowledgements, independent
   progress watchdogs, and complete permitted cold-recovery outcomes.
4. An adversarial oracle review with deliberate faults and relevant pinned bad
   revisions where available. Mutants validate oracles; they do not establish
   historical production regressions.
5. A small deterministic PR selection, a larger recorded local campaign and an
   extended scheduled configuration. Counts, seeds, exact source/runtime identity
   and outcomes are recorded after execution, not inferred from configuration.

No production functionality or performance optimization is part of this audit.
A confirmed production defect retains a minimal failing reproducer and a separate
correction proposal. Such a defect blocks the affected merge decision until fixed
and revalidated; it must not be converted into passing coverage by accepting an
unexpected timeout, exception or partial outcome.

## Reports

- [Acquisition inventory and dependency graph](pr133-concurrency-dependencies.md)
- [Independent oracle review](pr133-concurrency-oracle-review.md)
- [Coverage matrix](pr133-concurrency-matrix.md)
- [Actor explorer and replay](pr133-concurrency-explorer.md)
- [Multiprocess campaign and replay](pr133-multiprocess-campaign.md)

## Decision: blocked pending a separate production correction

One newly exposed **pre-existing production defect** remains: an ordinary Shared
callback can synchronously write through another facade for the same file and wait
forever for native ownership retained by its own outer operation. Both pinned and
unanchored ordinary entry paths fail. The minimal production reproduction also
fails on parent `49c327cf1926fa300f9eb7477eb4bcb404f75c43` and upstream dev
`5dd942a7367c361fadd600be4ce10aace2768b27`, plain and encrypted. It is not a
regression attributed to the Shared child-wrapper optimization.

The cycle is **outer callback → peer native admission → outer pin/native owner →
outer callback**. The reproduction observes the actual held pin and native wait,
not merely elapsed time. Same-facade recursion and different-file nested writes
complete; explicitly closing the waiting peer cancels admission and permits cold
verification. The explorer performs that emergency cancellation only to contain
the diagnostic and still fails its assertion. Applications do not recover unaided.

The permanent failing schedules are Shared vectors 29 (pinned) and 31 (unanchored).
The minimal public-API reproduction, production binaries, original fixtures and
historical results are in the evidence bundle under
`independent-oracles/peer-callback-repro`. Seed 233 selects vector 29, Shared,
plain, first completion parity directly:

```bash
dotnet build LiteDB.Fuzz/LiteDB.Fuzz.csproj -c Release -f net8.0 -p:TestingEnabled=true
dotnet LiteDB.Fuzz/bin/Release/net8.0/LiteDB.Fuzz.dll \
  --target transaction-interleavings --seed 233 --count 1 \
  --artifact-dir artifacts_temp/c12 --max-artifact-mb 0
# Expected on the audited revision: nonzero exit, observed native self-wait.
```

A **separate correction** should check same-namespace synchronous ownership
before all ordinary blocking acquisition paths, including pin/native entry and
late-reader callbacks. It must distinguish an executing dependency from an idle
owner and preserve same-facade recursion, independent leased readers and other-file
operations. The inventory identifies the sites; the independent report explains
the counterexamples. The correction needs its own strongest available known-bad
#3034 proof and passing retained regressions. This audit neither implements nor
pre-approves that production change. Maintainer disposition is required before
merging PR #133; the recommendation is **do not merge yet**.

## Exact final qualification

Audit test/workflow source: **`f9487f81421a378cd50e8cae9b6112b5fd3be291`**,
on branch `audit/pr133-concurrency-98a`. Later report-only commits do not change
these tested sources. Production tree remains
`4c82e64ec435862987ccb0d1061dc6eadc34510f`; test tree is
`37107cc31ec5c34bd0e90a0f2ac04885722d3938`. PR #133 remains at `98a10086c`.
No production functionality, speculative optimization or existing test weakening
is included. The failing regression is deliberately not allowlisted.

| Evidence | Actual result |
| --- | --- |
| Final Linux/net8.0 full actor matrix + C06/C22 focused cycles | 268 passed / 8 failed / 0 skipped, 276 rows |
| Final Linux/net10.0 same selection | 268 passed / 8 failed / 0 skipped, 276 rows |
| Attribution of each eight failures | Shared vectors 29/31 × plain/encrypted × two completion parities; all the same observed native self-wait |
| Final Fuzz.Tests on net8.0 | 36/36 passed, including 12 new logical-oracle/progress controls |
| Final net462 test compilation | 0 errors; compile only, not CLR 4 execution of new tests |
| C# size, coverage ledger, fault registry, contract reference checks | Passed; three existing quarantines remain gaps |
| Author final process campaign, net8/net10 | 20 cases + 20 recorded-input replay per runtime: 80 scenarios passed |
| Independent final helper controls | Precise refusal, completed-worker idle time and stalled-worker isolation all detected their intended outcomes |
| Independent prior deliberate mutations | Wrong value, missing boundary and individually stalled actor detected; unchanged control passed. Exact intermediate source and limits retained |

Local final runtime installations were .NET 8.0.30 and .NET 10.0.11. Each local
host ran all 272 actor cases plus four focused cycles under the unchanged
300-second session limit. The 16 failed-fixture directories, histories and both
exact test runtime layouts were copied only after their test hosts exited;
original locations remain retained. No failed or slow case was discarded.

[Hosted Fuzz run 36768732883](https://github.com/JKamsker/LiteDB/actions/runs/36768732883)
executes the exact audit commit above. It finishes **failed**: 12 jobs succeed,
two concurrency jobs fail, and two non-smoke jobs are intentionally skipped.
Both concurrency jobs fail on C12 and still execute their independent process
steps successfully:

| Hosted platform | Actor exploration prefix | Process campaign |
| --- | --- | --- |
| Ubuntu x64, .NET 8.0.31, seed 133098 | 143 complete cases, C12 failure at step 144 of requested 272 | 32/32 passed; all ten cuts, both encryption modes; 64 cold reopens |
| Windows x64, .NET 10.0.12, seed 233098 | 239 complete cases, C12 failure at step 240 of requested 272 | 32/32 passed; all ten cuts, both encryption modes; 64 cold reopens |

A fuzz prefix is not a completed Cartesian campaign. Full local xUnit discovery
continues after failures and supplies that matrix. Hosted run metadata reports
`workingTreeDirty: true`; the workflow creates unignored `artifacts/` output, and
no post-run tracked-diff capture was included. Preserve that limitation rather
than representing the flag as a verified clean working tree. Hosted artifacts
retain inputs, histories, child diagnostics and fixtures; exact hosted runnable
binaries were not uploaded. Local runtime layouts and source identities are
retained separately and are not substituted for hosted binaries.

The prior baseline's green full matrix, 21 registered regression proofs and
Safety aggregate remain [separate evidence](https://github.com/litedb-org/LiteDB-Artifacts/tree/9fc63da3837a2be77755c8f0f03062bb94d974a5/audits/2026-09-30-98a10086c).
They do not turn this audit green or qualify the new tests on every old matrix leg.

## Coverage and explicit limits

The [matrix](pr133-concurrency-matrix.md) maps 22 identified dependency classes
to observed boundaries, held resources, competitors and expected outcomes. C04,
C06 and C22 are reachable collection/maintenance/application-order cycles with
explicit bounded/cancellable behavior, not impossible states. C12 is the retained
product failure. Other inverse-order candidates have source-order/publication
justifications and linked existing regressions. This is a bounded source audit,
not an enumeration of arbitrary application-created wait graphs.

The reusable actor explorer forces pairwise/three-actor overlap, exact refusal
contracts, admission/close/maintenance publication and independent transaction
outcomes. Each worker has its own invocation/completion deadline. Cold state checks
use complete BSON, indexes, actual index plans and unrelated sentinels. It assumes
only the documented transaction guarantees, not unrestricted linearizability or
snapshot isolation. The default PR selection has 68 representative cases; the
full opt-in selection has 272. Encryption/outcome choices in the default subset
are correlated and must not be described as full Cartesian coverage.

The process campaign uses six roles, ten observed cuts and recorded invocation
permutations, with acknowledgements recorded outside killed processes. After the
observed WAL confirmation flush, complete new state is mandatory even before the
public acknowledgement; before flush, only protocol-permitted whole outcomes are
allowed. The new 150-minute nightly shard is configured, **not claimed executed**.
Other existing fuzz budgets and coverage remain unchanged.

Uncovered or inherited limits remain: arbitrary higher-actor schedules, sustained
starvation under unbounded arrivals, custom-stream/application callback graphs,
new GC/abandonment campaigns, physical hardlink/symlink aliases and every platform
admission fallback. Reader/session kill cuts are before the cleanup API, not every
internal retirement instruction. Process death retains OS caches and is **not
power loss**; existing modeled persistence targets remain separate. Actual device
reset/power-loss validation is not claimed. All historical harness mistakes,
overwritten-binary gaps and earlier unknown failures remain identified in the
individual reports and evidence manifests.

The finite audit is complete. A production correction and its bounded revalidation
are the next task; the audit does not justify an indefinitely expanding PR or a
claim that the entire #3034 roadmap is complete.
