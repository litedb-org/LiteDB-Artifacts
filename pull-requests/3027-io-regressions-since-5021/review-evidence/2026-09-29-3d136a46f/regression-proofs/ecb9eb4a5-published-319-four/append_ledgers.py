"""Append the four #3027 proofs to regression-proofs.json and retire them in repro-ci.json (append-only)."""
import json
import os

ROOT = "$REPO/.claude/worktrees/agent-af1c17944ad1df782"
os.chdir(ROOT)

R = "LiteDB.Tests/Regressions/"
proofs = [
    ("Issue_3027_DumpInTransaction", [
        R + "DumpPinnedWalSlot_Tests.cs#System_collection_inside_an_explicit_transaction_does_not_break_the_next_commit",
        R + "DumpPinnedWalSlot_Tests.cs#Pinned_slots_of_a_file_database_survive_commit_retirement_and_recovery"]),
    ("Issue_3027_StreamDatabaseDispose", [
        R + "StreamDatabaseDispose_Tests.cs#Second_dispose_does_not_throw",
        R + "StreamDatabaseDispose_Tests.cs#Dispose_with_an_open_transaction_rolls_back_and_releases_the_file",
        R + "StreamDatabaseDispose_Tests.cs#Opening_through_a_stream_does_not_persistently_change_the_checkpoint_pragma"]),
    ("Issue_3027_LegacyReadOnlyStream", [
        R + "LegacyReadOnlyStream_Tests.cs#Read_only_stream_of_a_5_0_21_database_can_be_queried",
        R + "LegacyReadOnlyStream_Tests.cs#Read_only_stream_of_a_5_0_21_database_accepts_transactions_and_rejects_writes",
        R + "LegacyReadOnlyStream_Tests.cs#Crash_images_over_non_writable_streams_open_without_any_change",
        R + "LegacyReadOnlyStream_Tests.cs#Read_only_stream_of_a_current_database_keeps_transactions_that_change_nothing"]),
    ("Issue_3027_ForeignLegacyWal", [
        R + "LegacyWalPageBound_Tests.cs#Log_of_another_database_fails_the_open",
        R + "LegacyWalPageBound_Tests.cs#Header_of_another_database_does_not_raise_the_bound",
        R + "LegacyWalPageBound_Tests.cs#Committed_legacy_page_beyond_both_files_fails_the_open",
        R + "LegacyWalPageBound_Tests.cs#Committed_legacy_page_that_is_not_a_page_fails_the_open",
        R + "ConvertedWalBesideLegacyHeader_Tests.cs#Legacy_header_beside_a_converted_wal_is_refused"]),
]
scripts = {"Issue_3027_ForeignLegacyWal": ["scripts/test-5021-regression-compatibility.py"]}

ledger = ".github/safety/regression-proofs.json"
s = open(ledger, encoding="utf-8").read()
end = "    }\n  ]\n}\n"
assert s.endswith(end)
blocks = []
for repro, tests in proofs:
    guard = ",\n".join(f'        "{t}"' for t in tests + scripts.get(repro, []))
    blocks.append(f'    {{\n      "repro": "{repro}",\n'
                  f'      "knownBad": {{"kind": "package", "version": "6.0.0-prerelease.319"}},\n'
                  f'      "permanentGuard": [\n{guard}\n      ]\n    }}')
s = s[:-len(end)] + "    },\n" + ",\n".join(blocks) + "\n  ]\n}\n"
open(ledger, "w", encoding="utf-8").write(s)
json.loads(s)

reasons = {
    "Issue_3027_DumpInTransaction": "$dump and $page_list inside an explicit transaction that safepointed its pages are covered by the normal test suite.",
    "Issue_3027_StreamDatabaseDispose": "the stored CHECKPOINT pragma and Dispose of stream-based databases are covered by the normal test suite.",
    "Issue_3027_LegacyReadOnlyStream": "5.0.21 files on non-writable streams are covered by the normal test suite.",
    "Issue_3027_ForeignLegacyWal": "legacy WAL pages that are not this database's pages are covered by the normal test suite.",
}
ci = ".github/repro-ci.json"
s = open(ci, encoding="utf-8").read()
end = "    ]\n  }\n}\n"
assert s.endswith(end)
entries = []
for repro, tests in proofs:
    listed = ",\n".join(f'      "{t}"' for t in tests)
    entries.append(f'  "{repro}": {{\n'
                   f'    "reason": "Regression proof of PR #3027 (Regression proof workflow); {reasons[repro]}",\n'
                   f'    "tests": [\n{listed}\n    ]\n  }}')
s = s[:-len(end)] + "    ]\n  },\n" + ",\n".join(entries) + "\n}\n"
open(ci, "w", encoding="utf-8").write(s)
json.loads(s)
print("ok")
