# Review resolution for PR #3016 / issue #3018

Final candidate: `4c84f44ed4adb1df57cc6d797d443b8978747f13`.
Intermediate review fixes: `8c038860c8dcd1b7f7293eb684849e9d4fad96a3`.
Reviewed source: `e06fc9d2d061e412fa6782fb8c993f3b75ce35ec`.
Merged stack baseline: `94bf30948a865edee75482265195cca7af18a954`.

## Behavioral changes

| Concern | Resolution and distinguishing evidence |
| --- | --- |
| Timer cleanup can terminate the process | Catch exceptions at the actual timer entry point; detach expired state and stop its timer even when close fails. `SharedCleanupFailure_Tests` injects a throwing lease, checks no reusable snapshot/timer remains, and verifies live/cold data. |
| Page finalization can throw | Finalizer contains errors; nested cleanup still attempts map and participation disposal. Test invokes the actual finalizer with a throwing FileStream and proves participation can be opened exclusively. Existing GC/native-death tests cover lifetime release. |
| Open succeeds but publication throws | Keep the provisional engine local until `Opened` succeeds; close it without checkpoint on failure. Failed structural cleanup cannot replace the original exception. Test changes the mapped header after a real engine open, checks empty connection state, and verifies cold committed records and another write. |
| No operational opt-out | AppContext switch `LiteDB.DisableSharedMappedReads`, or startup environment `LITEDB_DISABLE_SHARED_MAPPED_READS=1`. Native plaintext/encrypted opt-out writers revoke live peers before mutation; accepted streams keep their original data. Default remains automatic, preserving the requested optimization; default-off was an optional alternative to an opt-out. |
| Silent fallback / transient Windows sharing errors | Production `SharedEngine.CoordinationFallbackReason`, documented semantics and limitations. Retry only native sharing/lock violation codes 32/33, four delays of 10 ms per operation. Tests inject transient and permanent publication failures; Windows attachment test proves recovery before sticky fallback. No retry of unknown ABI/identity, generic I/O or access denial. |
| Tests require qualified `/tmp` | Shared fixture selects a qualified writable directory, with `LITEDB_MAPPED_TEST_DIRECTORY` override. Local tmpfs-default run passes all 15 selected cases; forcing tmpfs explicitly skips 10 test methods with the volume reason. Fallback/format tests are not globally moved. |
| Pure reads act like writers | Propagate actual write intent. Pragma getters and BeginTrans without writes keep the cache and leave the writer hint clear; actual writes retire it and announce. Tests inspect the real mapped hint during opening and verify transaction/read behavior. |
| New connection never hints | After safe attachment under the mutex, announce before a directly writable engine open; announce the first write inside an existing transaction/pin. The first mutex acquisition of a new connection still cannot safely publish. Churn benchmark includes creation and final close per transaction, including the caught sharing violation from exclusive-open liveness checks when peers remain. That liveness proof is retained. BeginTrans has no declared write intent, so its first actual write starts pressure. |
| Unix file-locking disabled | Reject Shared/Coordinated reader-registry creation and low-level authority attachment/retirement when the startup switch/environment disables .NET file locking. Native disabled-locking child cannot retire a live parent authority. Runtime sources for .NET 8/9/10 confirm switch names and sharing-lock behavior; the guard rejects either enabled disabling knob because runtime precedence differs. Native tests include environment=true with AppContext=false. All participants must obey the locking contract; later runtime reconfiguration and foreign processes bypassing locks remain unsupported. |
| Orphan control temporaries | Compact attributed names preserve the Windows 239-character database-path budget. Under the mutex, remove only empty or recognized matching temporaries after exclusive opening proves no live publisher. Publishers hold their file through rename. Unknown/corrupt/wrong-database/old untagged temporaries remain untouched. Native kill/recovery tests now assert recognized leftovers disappear. |
| macOS case identity | Existing mutex names already fold case; ABI binding and temporary attribution now fold on macOS too. Header and live authority alias tests cover this on case-insensitive filesystems. Linux binding retains case for distinct files. No symlink/hard-link identity claim. |
| Pacing and memory scope unclear | Operations guide documents synchronous Thread.Sleep, per-connection budgets, hint lifetime while the writer owns the engine, expiration, component memory limits, and multiplication across connections. Pooling/process sharing remains #3017. |
| Free WAL slot allocation lacks a lease scan | Document the existing invariant at the free-slot set: only blank/restored retired positions and structurally fenced checkpoint retirement produce entries. Allocation invalidates cached epochs; any new producer must retain that structural exclusion. Existing forced interleaving, reuse negative controls, native generation tests and state fuzz remain relevant. No redundant scan was added. |
| Filesystem exclusions / Docker / deployment | Release notes and operations guide enumerate supported volumes and excluded overlayfs/tmpfs/ZFS/drvfs/network storage, qualified Docker bind mounts, sidecars, antivirus, consistent data/WAL backup, and unsupported live cloud sync. |
| Offset alias and misleading probe name | Replace the reserved-header-word alias with named byte offsets; rename the low-level native probe to `ExistsOrUnknown`. |
| History / benchmark split | Keep forward history intact, with independent commits for the CI race, CI partitioning and churn benchmark. Existing stack benchmark dependencies remain in this PR. No merge/rebase/squash was authorized; squash can be selected when merging. PR description begins with the three user-visible changes. |

## CI defects discovered on the reviewed source

Windows .NET 9 exposed a real race: the holder cleared the exited owner's identity
before cleanup finished, and `TryEnter` reported a live foreign owner. The release
event now covers that cleanup window. Both forced Commit/Rollback regressions fail
with the old `SharedMutexOwner` and pass with the fix, preserving the committed row
and discarding the abandoned transaction. Ordinary foreign-owner tests still pass.

Windows x86 .NET 10 exceeded the existing 300-second query-session limit after 656
passes, while running the slow `NotEqualIndex_Tests`. CI now runs that class in its
own disjoint session. The partition verifier continues to enforce exact coverage;
no test or concurrency was removed, and the session limit remains 300 seconds.

## Evidence identities and limits

Local validation of the intermediate source: 1,181 Shared/WAL/coordinator/MVCC cases in four complete
.NET 8 partitions, 194 focused .NET 10 cases, all-target production builds, and 12
same-binary production churn smoke variants. The latter establish runner correctness,
not a speed comparison. Local environment qualification runs are retained separately.

The final guard correction additionally passes nine policy cases on each of .NET 8
and .NET 10. Its two native conflicting-settings regressions fail on the old guard.
Both .NET 9 and .NET 10 prefer the environment value, unlike the checked .NET 8 source;
the final guard conservatively rejects either enabled disabling knob.

The 63 ABI cases added for #3018 passed in all 21 modern full-suite artifacts on
`e06fc9d2d`, including macOS, ARM64 and Windows x86. That source's CI was nevertheless
red because of the two defects above; it is not presented as green validation of
the candidate. Its fuzz matrix and compatibility matrix passed. Final candidate
run IDs and exact comparison baselines are in `manifest.json`.

The isolated ABI comparison is `e06fc9d2d` versus `186cdeec6`: five alternating pairs
per Linux/Windows .NET 8/10 core workload, with two/four writer contention. The first
Windows .NET 10 mixed p99 batch was +20.56% [2.69%, 38.71%]; an unchanged rerun of that
job was +8.57% [-8.28%, 26.42%], with scan p99 +9.29% [0.97%, 17.63%]. Both batches are
retained. They are not pooled across hosted machines, and the repeat does not erase
the first adverse observation. Final `4c84f44ed` versus `186cdeec6` additionally
includes all review changes and cannot isolate header cost by itself.

The prior prototype experiments, rejected alternatives and adverse performance
cells remain in the immutable earlier artifact report. All final-head ordinary, saturation, archived, incremental and churn comparisons
completed successfully. An additional unchanged Windows .NET 10 incremental job
was repeated once to investigate a severe scan sample; both batches are retained
separately. Final CI passes 47 jobs, fuzz 12 selected jobs, and compatibility four
jobs. All 21 modern suites pass the 92 ABI/review cases with no mapped-test skips. Bootstrap intervals are exploratory; crossing zero does
not prove equivalence. Saturation and equal offered demand are different workloads.
