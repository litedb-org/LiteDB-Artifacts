# PR #133 concurrency coverage matrix

Baseline: `98a10086c2a296040a345e62ea987d59fc2ab487`. Resources and source
acquisition sites are defined in the [dependency inventory](pr133-concurrency-dependencies.md).

New schedules use the existing callback, admission, lock and publication observers;
no new production hook or runtime behavior was introduced. The inventory is a
bounded source audit, not exhaustive enumeration of arbitrary application code.

| Boundary | Held resources/state | Competing operation | Required behavior | Existing/new deterministic evidence | Disposition |
| --- | --- | --- | --- | --- | --- |
| C01 H | executing handle callback | second bound read/commit/rollback | Immediate exact refusal; Active preserved | Explorer00–11; existing binding/handoff tests | Final full matrix qualified; C12 failures remain |
| C02 C[n] | executing handle owns collection | ordinary same-collection callback write | Immediate lock refusal; ordinary objects remain independent | TransactionHandleCallbackLock_Tests | 98a baseline CI |
| C03 C[n] | idle handle owns collection | ordinary write while peer completes handle | Legitimate wait then progress | Original_thread_may_wait_for_idle_handle_completed_by_another_thread | 98a baseline CI |
| C04 C[a]/C[b] | two Direct writers own opposite collections | each writes the other collection | Configured timeout breaks cycle; full losing transaction rolls back | Explorer32–33; actual BeforeWait markers on both collection locks | Included in full272 and representative68 |
| C05 E | active ordinary call | queued rebuild and late ordinary reader | Drain current work; fence fresh entry until exclusive retires | Explorer24–25 Direct; TransactionHandleMaintenanceProgress_Tests | Positive maintenance reservation/fence observations |
| C06 E | ordinary callback awaiting fresh actor | rebuild fence blocks awaited actor | Permitted configured maintenance timeout; eventual successful retry | TransactionHandleMaintenanceCycle_Tests | 4 focused C06/C22 rows pass final net8/net10 after retention correction |
| C07 E | callback awaiting fresh actor | raw close awaits callback | Fresh call refuses ENGINE_DISPOSED; drain completes | TransactionHandleRawCloseDependency_Tests | 98a baseline CI |
| C08 S | legacy owner and blocked peer | session close | Cancel admission then drain/rollback | TransactionHandleCloseCleanup_Tests; explorer Shared admission | Observed local/native wait and close publication |
| C09 G/N | executing explicit handle | same-file ordinary callback work | Reject self-dependent acquisition before pin/native wait | Explorer bound callback controls; TransactionHandleSharedPinCallback_Tests | Pinned historical production proofs plus baseline CI |
| C10 G/N | ordinary pin or mutex-backed reader callback | begin handle through same/peer facade | Reject before local/native admission | TransactionHandleOrdinaryCallbackBegin_Tests and Control_Tests | 98a production proofs and CI |
| C11 G/N | independent leased reader; independently completable idle owner | begin handle | Valid admission; do not blanket-reject callbacks | Independent_leased_reader_callback_can_begin_handle; idle controls | 98a baseline CI |
| C12 O/Q/N | ordinary native-owning callback | ordinary same-file peer write | Refuse impossible synchronous dependency; preserve outer operation | Explorer29 pinned and31 unanchored; independent production repro | CONFIRMED DEFECT on98a,parent49c,dev5dd; plain/encrypted |
| C13 P/J | reader callback retains draining core | last-reader disposal | No join on holder awaiting this callback | SharedReaderRetirement_Tests; SharedLateReaderClose_Tests | 98a baseline CI |
| C14 U/E | closingCore published | fresh ordinary operation | No premature replacement core; refuse self-reentry | Last_reader_close_fences_fresh_operation_until_core_retired | 98a baseline CI |
| C15 Q/N | live native owner and waiter | owner/waiter process death or cancellation | Correct ownership release; survivor completes | shared-lifecycle native-waiter and owner cuts; turnstile/pin tests | Observed admission and per-command completion, not startup timing alone |
| C16 G/J | returned child wrapper | external writer then next handle | Core/native ownership retired; wrapper reused and refreshed | shared-lifecycle after-refreshed-wrapper-reuse; child reuse tests | Actual wrapper reference and external data observed |
| C17 B/I/W | commit/checkpoint ownership | WAL or checkpoint process death | Acknowledged/flush-proven effects survive; unknown outcome complete | shared-lifecycle four WAL/checkpoint cuts; existing modeled-power-loss targets | Process death only in new target; established lock order from source |
| C18 K/R | live reader lease | external writer/checkpoint | Original admitted snapshot contents preserved | shared-lifecycle held reader and existing coordination publication tests | Snapshot lease witness plus full original documents |
| C19 A | native mode/file admission | alias opener or killed process | Compatible lexical aliases coordinate; incompatible mode refuses | shared-lifecycle mixed canonical/./ paths; NativeAdmission_Tests | Physical aliases and platform fallback remain inherited coverage/gaps |
| C20 D/E | Direct facade/handle/cursor refs | close plus fresh/continued work | No premature disposal; correct close refusal/drain | Explorer26–27; Direct pool/lifetime guards | Actual closing publication and callback handoff |
| C21 M | page first-loader owns Loading entry | peer load/close/failure | Publish success/failure; no premature pinned-frame reuse | Existing cache loading/lifecycle and disposal regressions | Source inventory +98a CI; arbitrary custom-stream callback graph outside gate |
| C22 cross-database | A ownsX, B ownsY | callbacks each begin other database | Cancellable admission escapes application ordering cycle; one-way valid | TransactionHandleCrossDatabaseCycle_Tests | Opposed observed gates, both cancellation outcomes, cold indexed models |

The confirmed C12 cycle is preserved as a failing assertion, not an allowed timeout.
C04, C06 and C22 are feasible bounded/application cycles with deterministic tests;
they are not described as unreachable. Other internal inverse-order candidates are
rejected by the specific order/publication arguments in the inventory, backed by
the linked existing tests. Source arguments do not establish safety of arbitrary
custom stream callbacks or application-created cross-thread waits.

New process cuts before reader/session disposal are API-boundary cuts, not evidence
of interruption inside internal cleanup. Cross-process persistence here is process
death with OS caches surviving. Existing modeled-power-loss campaigns remain
separate. Final exact source, local outcomes and CI links are recorded in the
[combined report](pr133-concurrency-audit.md).
