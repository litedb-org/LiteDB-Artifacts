"""Record candidate/build provenance and discovery for a filtered native CI leg."""
import argparse
import json
import os
import re
import subprocess
from pathlib import Path


def selected(name, expression):
    """Evaluate the deliberately small FQN filter language used by native legs."""
    if not expression:
        return True
    for alternative in expression.split("|"):
        clauses = alternative.split("&")
        matches = []
        for clause in clauses:
            match = re.fullmatch(r"FullyQualifiedName(!?~)(.+)", clause.strip())
            if not match:
                raise ValueError(f"Unsupported native evidence filter clause: {clause}")
            operator, value = match.groups()
            matches.append((value.casefold() in name.casefold()) == (operator == "~"))
        if all(matches):
            return True
    return False


def partition_plan(names, expression, result):
    """Intersect the original selection with disjoint categories; never sample it."""
    selectors = {
        "handles": "FullyQualifiedName~TransactionHandle",
        "admission": "FullyQualifiedName!~TransactionHandle&FullyQualifiedName~NativeAdmission",
        "remaining": "FullyQualifiedName!~TransactionHandle&FullyQualifiedName!~NativeAdmission",
    }
    stem = Path(result).stem
    plan = {key: {"filter": "|".join(f"{term}&{selector}" for term in expression.split("|")),
                  "result": f"{stem}-{key}.trx"}
            for key, selector in selectors.items()}
    validate_partition_plan(names, plan)
    return plan


def validate_partition_plan(names, plan):
    if len({item["result"] for item in plan.values()}) != len(plan):
        raise ValueError("Native partitions must have distinct result files")
    counts = dict.fromkeys(plan, 0)
    for name in names:
        if "TestHost_Tests" in name:
            continue  # Both guards intentionally execute in every partition.
        matches = [key for key, item in plan.items() if selected(name, item["filter"])]
        if len(matches) != 1:
            raise ValueError(f"Native partition coverage: {name} matched {matches}, expected exactly one")
        counts[matches[0]] += 1
    empty = [key for key, count in counts.items() if not count]
    if empty:
        raise ValueError(f"Native partitions contain no workload tests: {empty}")


def record(root, results, discovery, expression, result, framework, runtime, architecture, partition=False):
    names = discovery.read_text(encoding="utf-8-sig").splitlines()
    names = sorted({name.strip() for name in names if name.strip()
                    and (selected(name.strip(), expression) or "TestHost_Tests" in name)})
    if not names:
        raise ValueError("Native test discovery produced no selected tests")
    plan = partition_plan(names, expression, result) if partition else None
    # This file travels with the binaries downloaded from the corresponding build
    # job. Never substitute HEAD when the binary provenance is missing.
    build = json.loads((root / "LiteDB.Tests/bin/Release/build-evidence.json").read_text(encoding="utf-8-sig"))
    revision = lambda ref: subprocess.check_output(["git", "-C", str(root), "rev-parse", ref], text=True).strip()
    leg = {
        "schemaVersion": 1, "job": os.environ.get("GITHUB_JOB"),
        "matrix": json.loads(os.environ.get("LITEDB_EVIDENCE_MATRIX", "null")),
        "sha": revision("HEAD"), "tree": revision("HEAD^{tree}"), "buildSha": build["sha"],
        "runtimeMajor": runtime, "framework": framework, "architecture": architecture,
        "format": "trx", "discovery": "discovered-tests.txt",
        "partitions": {key: item["result"] for key, item in plan.items()} if plan else {"native": result},
    }
    results.mkdir(parents=True, exist_ok=True)
    (results / "discovered-tests.txt").write_text("\n".join(names) + "\n", encoding="utf-8")
    if plan:
        (results / "native-partitions.json").write_text(json.dumps(plan, indent=2) + "\n", encoding="utf-8")
    (results / "evidence-leg.json").write_text(json.dumps(leg, indent=2) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--results", type=Path, required=True)
    parser.add_argument("--discovery", type=Path, required=True)
    parser.add_argument("--filter", default="")
    parser.add_argument("--result", required=True)
    parser.add_argument("--framework", required=True)
    parser.add_argument("--runtime", type=int, required=True)
    parser.add_argument("--architecture", required=True)
    parser.add_argument("--partition", action="store_true")
    args = parser.parse_args()
    record(args.root, args.results, args.discovery, args.filter, args.result,
           args.framework, args.runtime, args.architecture, args.partition)


if __name__ == "__main__":
    main()
