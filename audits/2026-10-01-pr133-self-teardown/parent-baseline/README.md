# Corrected stacked-parent performance baseline

Local-only benchmark baseline: #132 parent 49c327cf1926fa300f9eb7477eb4bcb404f75c43 merged with corrected dev safety branch fad082daf2283f6844fcef2af379a3e0d5c02d59. No transaction-handle API. Final source commit 8a630b26b.

Production merge adaptation retained #132 native SharedAdmission disposal in its relocated Disposal partial, applying corrected-dev teardown refusal before publishing disposed state, ownership frame and CloseRetainedCore. Upstream guarded snapshot close remains in Query partial; duplicate old relocated method removed. Query Call-before-getter merged automatically. CI workflow conflict combined both retained-fixture and upstream results upload steps; this local benchmark branch is not a production PR/CI-qualified baseline.

Release TestingEnabled=false library net10 and Handles=false benchmark runner built successfully. Existing SharedSelfTeardownRelease production source proof net8 passed exact exit2/FIXED_VERIFIED, both plain/encrypted controls and guarded disposal cases, later foreign writer and two cold reopens. Its runtime and original fixture copy are retained. This focused proof validates the manual guard merge, not the full safety suite. No timing measurements were performed by this baseline preparation task.

Initial build failure from duplicate relocated CloseMutexSnapshotsLocked and initial proof invocation rejection of unsupported --root option are retained separately; final build/proof logs supersede these setup errors. Original fixture remains at the path in identity.json. Binary hashes identify the actual production assemblies; no TESTING build was performed.
