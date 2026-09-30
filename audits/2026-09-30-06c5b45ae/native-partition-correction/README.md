# Native partition harness correction

Harness commit9ac9ef3fd, integrated as010821392. Library/test source3b60d5aea unchanged. The local rehearsal ran on Linux x64/.NET10, using the macOS native-selection filter and an emulated CI job/matrix field to exercise evidence recording. The retained evidence-leg.json does not imply macOS execution. Actual macOS/Windows/glibc qualification is the hosted final run.

Local results: handles207passed/46s, admission202passed/71s, remainder236passed+1existing skip/48s. All298discovered methods produced results.646case rows include four additional repetitions of the two runtime/hook guards across the three sessions; the original642-case workload remains unchanged. No timeout was increased. Independent review and96Python policy tests passed. The case-insensitive discovery correction records21lowercase-rebuild methods (45cases) that previously executed but were omitted from accounting.
