# Independent bounded review

Reviewer: /root/fix_coverage. Source/log review only; no execution or edits.

No blocking finding in the current production guard or twenty-case test.
Caller-stream settings deliberately bypass filename normalization, so canonicalizing
the existing fixture directory aligns native namespaces without moving the data.
Eight be60 macOS cases observed admission exceptions during callback; two failed
setup admission before the callback. Neither proves callback-guard failure.

RetainedByOther(namespace, null) is appropriate for handles acquiring through a
separate child. Executing, dynamically retaining same-namespace frames matter;
other files, idle owners and ordinary same-facade recursion retain their behavior.

The tests require the callback, reject before admission, permit other-file
transactions, verify later progress, and perform two cold indexed/sentinel checks.
The small last-reader update and completed native release avoid the upstream
fixture race; pending WAL and the cold updated payload are asserted.

Limit: this is not macOS runtime verification or actual-knownbad proof.
