# PR133 finite qualification record — CI passed

Candidate12a057424 is pushed; f95 remains failed interim evidence. Production tree baf11234 includes confirmed P1 reader-retirement and P2 leased-reader retry fixes. Exact current-source measurement and independent arithmetic audit are complete; the complete hosted qualification passes. No older green run or production-inequivalent benchmark substitutes.

| Obligation | Current evidence | Outstanding limit |
| --- | --- | --- |
| Five Safety prompts and contracts | Five-contract registry and source-pinned audit/docs | Final PR and this record identify pushed12a; no merge is authorized |
| Real known-bad comparisons |18 registered cases below; actual f95 strict proof attempts retained separately with exact binaries | All18 comparisons passed on12a; bounded50ms public pin setup assumption explicit |
| Preserve coverage | Ledger, complete discovery/partition/runtime guards, policy 101 | Final pin-harness correction staged with exact12a6case checks; existing quarantines remain gaps |
| Fault/ownership outcomes | Actual P1/P2 failures, plain/encrypted safeguards, independent 54 and integrated675 perruntime | Overlapping source-specific scopes, separate from the complete final29-leg platform qualification |
| Candidate integration/performance | Current local production baf11234 identified | Exact12a84/420campaign independently verified; full hosted gate passes;190 measurements historical |
| Independent audit and failure retention | Review body, actual/intermediate failures, exact proof binaries, native retention controls | Original847ms Windows fixture missing/cause unknown; synthetic Linux controls are not that fixture |
| Lifecycle repeatability | Permanent normal-suite guards and dedicated pinned-feed proofs | PR unmerged, no post-merge execution claim |

## Registered comparisons

| Proof | Actual known bad | Permanent guards |
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
| Issue_133_SharedSnapshotCloseRefusal | `e821ae7479dcb83570031180b0c0e3f4fd167793` | `LiteDB.Tests/Engine/SharedLateReaderClose_Tests.cs#Late_reader_self_close_preserves_unleased_protection_or_leased_independence` |
| Issue_3067_SharedLateReaderClose | `e821ae7479dcb83570031180b0c0e3f4fd167793` | `LiteDB.Tests/Engine/SharedLateReaderClose_Tests.cs#Late_reader_callback_can_reenter_after_foreign_close_starts_draining` |
| Issue_3067_LateCallbackClose | `e821ae7479dcb83570031180b0c0e3f4fd167793` | `LiteDB.Tests/Engine/TransactionHandleLateCallbackClose_Tests.cs#Late_callback_close_refuses_only_a_self_dependent_drain`<br>`LiteDB.Tests/Engine/TransactionHandleLateCallbackClose_Tests.cs#Cross_thread_close_drains_late_callback_without_refusing_it` |
| Issue_3067_SharedReaderRetirement | `f95f0b0cd59d35d285ac72183589975751e35229` | `LiteDB.Tests/Engine/SharedReaderRetirement_Tests.cs#Last_other_leased_reader_cleanup_does_not_join_forced_pin_draining_this_callback`<br>`LiteDB.Tests/Engine/SharedReaderRetirement_Tests.cs#Last_other_leased_reader_cleanup_does_not_join_exited_owner_draining_this_callback`<br>`LiteDB.Tests/Engine/SharedReaderRetirement_Tests.cs#Independent_last_leased_reader_disposal_still_joins_pin_cleanup` |
| Issue_3067_LeasedReaderSelfDispose | `f95f0b0cd59d35d285ac72183589975751e35229` | `LiteDB.Tests/Engine/SharedLeasedReaderDispose_Tests.cs#Leased_snapshot_disposal_refuses_callback_before_mutation_and_can_retry` |

Local P1 strict proof qualifies the forced-pin path only; permanent tests establish non-pin owner-exit. Write-plus-BeginTrans must satisfy the documented 50ms setup bound, with at most three separately logged setup attempts. Slow setup and generic timeout cannot be treated as successful reproduced/fixed outcomes. Three complete proof attempts remain retained; no semantic failure is discarded. P2 strong-reachability and incompatible Direct admission controls prevent finalizer-induced false passes.

Integrated 675-per-runtime results use ce82 production; independent 54-per-runtime uses b2c9e2cce plus the archived test overlay; P1 fixer 46 and P2 fixer 53 scopes overlap. Initial P1 wrong-log-suffix setup failures and the immediate native-release assertion failure remain separate from the six genuine corrected-before deadlocks. Framework compilation is not execution; all29 current candidate test legs passed with runtime/platform evidence checks.

The native847ms sharing violation has unknown original lock ownership and a missing original fixture. The correction only improves failure diagnostics/retention while preserving the success path. Actual synthetic Linux failure fixtures prove byte-identical preservation and marker capture, not resolution of that original failure. The separate pin-yield failure109884239743 remains unknown in origin; its strengthened progress oracle and retention corrections are integrated at12a and locally pass. No unexplained observation is retrospectively turned green by this narrative.

Old 190 benchmarks and previous f95/190/e821 hosted results remain interim. Final hosted aggregate/proofs/fuzz/compatibility pass. Normalization/checksums are finalized for publication; maintainer performance acceptance remains separate. No physical-power-loss guarantee, new quarantine, merge or whole #3034-roadmap completion is claimed.

Final pin-harness evidence is in pin-yield-harness-correction/. The broad30case net10 run straddled a tiny source edit; subsequent exact12a six-case runs passed both runtimes. Current library tree remainsbaf112, so675/54 earlier production coverage remains relevant. Exact12a benchmark and independent arithmetic audit are complete; full current hosted qualification passes.

Final hosted verification: all 39 CI jobs and 29 test legs passed on exact
`12a057424`; 128,995 result rows contain zero failures. The hosted Safety
aggregate and independent evidence check report zero errors and zero warnings.
All 29 result-artifact digests and the Safety artifact digest match GitHub.
The original f95 failures remain failed historical records, with their limits
explicit; later passing controls do not reconstruct their causes.
