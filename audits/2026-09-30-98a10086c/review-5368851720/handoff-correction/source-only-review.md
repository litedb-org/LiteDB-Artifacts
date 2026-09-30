# Independent handoff test correction review

Reviewed the uncommitted three-file correction in integration, based on e8559b642: TransactionHandleCallbackHandoff_Tests.cs, TransactionHandleHandoffProgress_Tests.cs, TransactionHandoffProgress.cs. Source inspection only during benchmark freeze; no build, test or mutation execution performed for this review.

No blocking source defect found in this scope.

The forced gate probe establishes that the callback has entered before taking the handle gate and permitting its return. While the test owns that gate, public Exit cannot release admission. The volatile callback marker must therefore restore before Exit; moving restoration after Exit would fail this probe. Its finally releases the barrier and gate and attempts a bounded join.

The race phase retains four simultaneously started contenders and requires 1,000 successfully returned Run calls per worker. It accepts only the exact overlap refusal before callback entry. Other exceptions and any callback marker disagreement fail the test. Attempts and overlap refusals never renew the worker's last-success deadline. The external monitor can diagnose a blocked Run; peer success cannot conceal a stalled worker. The 60-second elapsed cap is followed by a bounded five-second worker drain, rather than being a promise of test-host termination at exactly 60 seconds.

Cleanup never closes/disposes the storage graph while a known worker is still alive. A static retained graph prevents TempFile finalization and transaction/session collection during a blocked worker. The alive-to-dead race at cleanup is safe because all worker starts are complete before this decision; an exited worker cannot restart. A successfully drained failure still reports errors and uses normal disposal. Start barriers are disposed only after all workers exit.

Independent negative validation to run after freeze: reverse context-restoration/public-Exit ordering and require the forced probe to fail; block one worker inside Run and require its own-success watchdog to fail while storage remains retained; throw an unexpected InvalidOperationException after callback entry and require a reported worker error rather than an overlap retry. The helper's existing deterministic tests cover own retry/peer success not renewing progress, blocked operation stage, total cap despite progress, and an unscheduled peer.

The original macOS Intel/net10 four-deadline failure remains unexplained. This source review supports the new oracle and diagnostics; it does not establish that the historical failure was scheduler contention or that a product bug was absent. It also does not replace the needed final exact-head hosted run.
