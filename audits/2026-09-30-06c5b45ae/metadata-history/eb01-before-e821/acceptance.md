# PR133 acceptance matrix

Interim candidate `eb01f346eb7d8d51925a45c2f87ef40e1c3984ee`; successor production fixes pending. Actual hosted completion status is in README.md and hosted-final/, not inferred from this matrix.

Additional reviews and pending obligations are in review-followup-eb01/. No row below constitutes final acceptance while successor production changes, benchmarks and hosted qualification remain outstanding.

## Applicable #3034 obligations

| Obligation | Evidence | Limit |
| --- | --- | --- |
| Five-prompt declaration and explicit contracts | Current PR description; final-source/.github/safety/contracts.json | Applies to this ownership/concurrency change, including inherited stack interactions |
| Real failing-before / fixed-candidate comparisons | Ten proof obligations, listed below; prior 010821392 results retained in hosted-interim-010821; eb01f346e hosted results pending | Synthetic mutants are separate oracle controls |
| Preserve semantic coverage | Coverage ledger, unchanged native selections, discovery/result reconciliation, runtime and hook guards | Existing quarantines remain gaps |
| Fault and lifecycle evidence | Registered boundaries, forced interleavings, exact indexed cold-state models, process-death and inherited persistence suites | No physical device power-loss campaign |
| Candidate integration | candidate-provenance.json; final hosted Safety evidence aggregate, Fuzz and compatibility workflows pending | Local agent1391 and candidate eb01 full trees match; measured3b60 LiteDB tree is unchanged. The old retarget equivalence is historical and does not replace final eb01 CI; no repository protection/queue setting changed |
| Independent audit and retained failures | This archive, original-review/, adversarial-review/, before-after/ and all hosted-interim-* | A clean bounded review is not universal correctness |
| Reusable regression lifecycle | Permanent guards in ordinary tests; regression-proof workflow repeats after dev integration | PR remains unmerged, so post-merge validation is configured but not claimed executed |

## Historical regression comparisons

| Proof | Actual known-bad state | Permanent guards |
| --- | --- | --- |
| Issue_2586_RollbackTransaction | `5.0.20` | `LiteDB.Tests/Engine/Transactions_Tests.cs#Transaction_Rollback_Should_Skip_ReadOnly_Buffers_From_Safepoint`<br>`LiteDB.Tests/Engine/Transactions_Tests.cs#Transaction_Rollback_Should_Discard_Writable_Dirty_Pages`<br>`LiteDB.Tests/Issues/Issue2586_RollbackSafety_Tests.cs#RollbackAfterSafepoint_PreservesCommittedDataOnReopen` |
| Issue_2614_DiskServiceDispose | `5.0.21` | `LiteDB.Tests/Issues/Issue2614_InitializationCleanup_Tests.cs#FailedInitialization_ReleasesFileHandle_AndAllowsRetry`<br>`LiteDB.Tests/Internals/StreamOwnership_Tests.cs#DiskServiceCtor_Failure_DisposesWrappers_ButNotCallerStreams` |
| Issue_133_MaintenanceProgress | `560529066aeda64c24d5cdd32d34a8aca12695ae` | `LiteDB.Tests/Engine/TransactionHandleMaintenanceProgress_Tests.cs#Maintenance_progresses_under_sustained_ordinary_reads` |
| Issue_133_CloseCollectionWait | `560529066aeda64c24d5cdd32d34a8aca12695ae` | `LiteDB.Tests/Engine/TransactionHandleMaintenanceProgress_Tests.cs#Raw_close_interrupts_collection_waiter_and_cold_reopen_keeps_only_committed_rows` |
| Issue_3034_Pr133StaleSortCleanup | `560529066aeda64c24d5cdd32d34a8aca12695ae` | `LiteDB.Tests/Engine/SortDiskCleanup_Tests.cs#Writable_open_reclaims_stale_sort_file_without_a_new_spill` |
| Issue_3067_SharedCloseWait | `560529066aeda64c24d5cdd32d34a8aca12695ae` | `LiteDB.Tests/Engine/TransactionHandleSharedAdmission_Tests.cs#Closing_cancels_waiting_shared_call_before_draining_legacy_owner` |
| Issue_3067_SharedNestedBegin | `560529066aeda64c24d5cdd32d34a8aca12695ae` | `LiteDB.Tests/Engine/TransactionHandleSharedNested_Tests.cs#Handle_begin_rejects_callers_locking_reader_or_pinned_legacy_transaction` |
| Issue_3067_DisposedBoundObject | `560529066aeda64c24d5cdd32d34a8aca12695ae` | `LiteDB.Tests/Engine/TransactionHandleDisposeSafety_Tests.cs#Disposed_bound_objects_refuse_use_without_aborting_prior_writes`<br>`LiteDB.Tests/Engine/TransactionHandleBoundDisposeRace_Tests.cs#Disposal_between_child_capture_and_admission_does_not_abort_writes`<br>`LiteDB.Tests/Engine/TransactionHandleBoundDisposeRace_Tests.cs#Actual_reader_operation_and_disposal_failures_still_abort` |
| Issue_3067_DisposeDuringClose | `560529066aeda64c24d5cdd32d34a8aca12695ae` | `LiteDB.Tests/Engine/TransactionHandleDisposeSafety_Tests.cs#Disposal_after_close_admission_is_revoked_does_not_mask_user_exception`<br>`LiteDB.Tests/Engine/TransactionHandleDisposeSafety_Tests.cs#Disposal_while_close_worker_rolls_back_is_idempotent` |
| Issue_3067_PeerFatalDispose | `560529066aeda64c24d5cdd32d34a8aca12695ae` | `LiteDB.Tests/Engine/TransactionHandleDisposeSafety_Tests.cs#Disposing_healthy_handle_after_peer_fatal_failure_does_not_repeat_peer_error`<br>`LiteDB.Tests/Engine/TransactionHandleDisposeSafety_Tests.cs#Deferred_fatal_teardown_does_not_repeat_peer_error_on_healthy_disposal`<br>`LiteDB.Tests/Engine/TransactionHandleFatalCleanupRace_Tests.cs#Cleanup_discriminates_published_peer_failure_from_its_own_failure` |

The final child-disposal race additionally has actual failing-before evidence on 3e88454e2 and twelve new race/error controls, plus eight independently added close-race cases. See bound-child-disposal-race/ and adversarial-review/child-admission/.

## Retained ownership semantics

Shared holder and wrapper identity recur, while core identity changes. Native writer ownership and mode-admission/coordination participation are released between handles; independent processes write between transactions and the next handle observes their indexed data. Plain and encrypted cases verify repeated cold reopen. Abandonment tests check application graph collection and native release; normal close drains admitted operations and cancels waiting begins. Maintenance tests force continued transaction/cursor dependencies while new independent work is fenced. Actual I/O failures retain their original error and rollback/indeterminate contract.

## Pin-progress harness qualification

The 010821392 Windows x86/net8 final-marker timeout remains failed with unknown cause. pin-progress-correction/ retains 15 local passing cases per runtime, including plain/encrypted held-pin negative controls; the earlier 13-case run overlaps. The correction retains all twenty inserts, checks cold indexed state and bounds startup/progress/overall completion. Diagnostic output cannot renew the progress budget. This is a documented test-budget/discrimination change, not proof that the original timeout was harmless. Final hosted eb01 qualification is required.

## Remaining scope

Performance acceptance remains separate: the safe Shared reuse gain is measured, but ordinary/legacy read regressions versus dev/parent remain reported. Whole-engine roadmap work, complete historical fixture provenance, registered-hook event completeness and physical-device campaigns are not solved by this PR. The normative rules retain maintainer ownership and scheduled audit cadence. The next investigation is the explicitly reported ordinary-read overhead if performance acceptance requires it; no excluded optimization is silently integrated.
