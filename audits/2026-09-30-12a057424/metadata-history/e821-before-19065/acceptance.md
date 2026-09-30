# PR133 acceptance matrix

Candidate `e821ae7479dcb83570031180b0c0e3f4fd167793`; production source `7e13e58e5f4e40642492ce768b49018e4a958c8b`. Two new P1 Shared late-reader callback findings in review5365276527 reopen this candidate; independent reproduction/fix and successor qualification remain pending. Local test/benchmark completion does not by itself meet the finite gate.

| Applicable #3034 obligation | Evidence | Limit |
| --- | --- | --- |
| Five Safety prompts and explicit contracts | PR declaration; pinned final-source/.github/safety/contracts.json | Five affected contracts; whole roadmap/settings not claimed |
| Real failing-before comparisons | Thirteen registered proofs below; source-specific local before/after outputs retained | Current hosted13 proof results still required; mutants separate |
| Preserve semantic coverage | Pinned ledger; exact native partition/discovery accounting; runtime/hook guards; policy checks | Existing quarantines remain gaps; changed test budgets explicitly recorded |
| Lifecycle and fault outcomes | Plain/encrypted ownership, callback, error, cross-process, indexed cold-state and process-death guards | No physical-power-loss campaign |
| Candidate integration | candidate-provenance.json; exact 7e13/e821 library-tree equality; required final CI/Safety/Fuzz/compatibility workflows | No earlier successful aggregate substitutes for current candidate |
| Independent audit and failure retention | Original reviews, local-fixes, case-level inventories and retained failed/intermediate hosted attempts | Bounded clean review is not universal correctness |
| Repeatable regression lifecycle | Permanent guards in normal suites; pinned-feed dedicated proofs; scheduled/post-dev validation configured | PR unmerged, post-merge execution not claimed |

The new review is retained in review-followup-e821/. Existing guards and local evidence below do not discharge these new obligations. Current hosted and benchmark records are interim.

## Registered actual known-bad states and permanent guards

| Proof | Known bad | Permanent guards |
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
| Issue_133_RawCloseCallbackDependency | `eb01f346eb7d8d51925a45c2f87ef40e1c3984ee` | `LiteDB.Tests/Engine/TransactionHandleRawCloseDependency_Tests.cs#Raw_close_rejects_fresh_callback_dependency_before_draining_callback`<br>`LiteDB.Tests/Engine/TransactionHandleRawCloseDependency_Tests.cs#Close_wakes_fresh_work_already_waiting_behind_rebuild` |
| Issue_3067_SharedPinCleanup | `eb01f346eb7d8d51925a45c2f87ef40e1c3984ee` | `LiteDB.Tests/Engine/SharedDisposalFailure_Tests.cs#Pin_close_failure_finishes_connection_cleanup_and_preserves_leased_reader`<br>`LiteDB.Tests/Engine/SharedCallbackClose_Tests.cs#Refused_raw_callback_close_keeps_native_ownership_until_operation_returns` |
| Issue_3067_HandleCallbackSelfWait | `eb01f346eb7d8d51925a45c2f87ef40e1c3984ee` | `LiteDB.Tests/Engine/TransactionHandleCallbackLock_Tests.cs#Same_collection_callback_refuses_self_wait_without_enlisting_independent_work`<br>`LiteDB.Tests/Engine/TransactionHandleCallbackLock_Tests.cs#Original_thread_may_wait_for_idle_handle_completed_by_another_thread`<br>`LiteDB.Tests/Engine/TransactionHandleCallbackLock_Tests.cs#Repeated_public_handoff_restores_binding_before_admitting_next_callback`<br>`LiteDB.Tests/Engine/TransactionHandleCallbackHandoff_Tests.cs#Public_admission_handoff_preserves_current_callback_marker` |

The eight originating cases use 560529066, three follow-ups use eb01f346e and two inherited cases use published packages. Strict outcomes distinguish reproduced regression from harness/setup failure. Exact registry metadata is authoritative. Source-specific follow-up proofs do not erase the obligation to rerun all required proofs on the accepted candidate.

## Current local and performance evidence

final-local/followup-7e13 retains268 passing cases per runtime with no skips/failures, independent18 case net10 review and production/Framework compilation. Earlier63/51/34/28/25/16/9 case selections overlap; trx-index inventories enumerate cases without summing them. Full96 policy checks pass after timing;11 matrix checks cover the proof-scheduling-only successor.

performance-final3 contains the completed84 process/420 window campaign on 7e13. Complete library-tree equality transfers its source identity to e821, not hosted success. Current Shared one-read gain is24.94% paired, with2.81% lower managed allocation; the4.69× historical proof result is unreproduced. Ordinary/legacy read regressions versus dev remain disclosed and require separate performance acceptance.

## Review dispositions and retained limits

Original review defects and deferred-fatal/disposed-child variants have guarded fixes. Additional reviews produced exception-safe Shared retirement, raw-close callback progress, callback self-wait rejection, scoped Windows cancellation and corrected intermediate ownership/handoff defects. Shared parent final checkpoint retains documented best-effort behavior with WAL/cold recovery controls; pin/child errors remain surfaced. ReadTransform replacement allegation is refuted by original/current public behavior; hypothetical Commit precondition leak remains unconfirmed with retained passing controls. Structural nits stay outside scope.

The original Windows pin timeout remains unknown in origin. The successor test's changed progress budget is explicit, with real held-pin negative controls. Case-sensitive discovery omissions, old close-error expectations, stream/reflection/path setup failures and final5 pinned-feed scheduling omissions are classified and retained. No new quarantine is introduced. Final hostedcompletion, final artifact normalization/checksums/publication and maintainer performance acceptance remain outstanding; repository protections/merge queue and unrelated3034 roadmap work are not claimed complete.
