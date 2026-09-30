#!/usr/bin/env python3
"""Summarize active-window throughput and every paired ratio without dropping runs."""
import argparse
import json
from pathlib import Path
import statistics

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("directory", type=Path)
args = parser.parse_args()
manifest = json.loads((args.directory / "manifest.json").read_text())
packed = {}
if (args.directory / "raw.jsonl").exists():
    for line in (args.directory / "raw.jsonl").read_text().splitlines():
        row = json.loads(line)
        packed.setdefault(row.pop("run"), []).append(row)
summary = []
for group in manifest["configuration"]["groups"]:
    for scenario in group["cases"]:
        case = "-".join(map(str, scenario))
        versions, indexed = {}, {}
        for version in group["versions"]:
            runs = []
            all_windows = []
            for repeat in range(group.get("rounds", 4)):
                order = group["versions"] if repeat % 2 == 0 else list(reversed(group["versions"]))
                name = f'{group["name"]}-{case}-{repeat}-{order.index(version)}-{version}'
                path = args.directory / (name + ".jsonl")
                rows = packed[name] if packed else [json.loads(line) for line in path.read_text().splitlines()]
                if rows[-1]["phase"] != "verified":
                    raise ValueError("Unverified run: " + name)
                windows = [row for row in rows if row["phase"] == "window"]
                if len(windows) != group.get("windows", 10):
                    raise ValueError("Incomplete run: " + name)
                count = sum(row["count"] for row in windows)
                rate = count / sum(row["elapsed"] for row in windows)
                indexed[version, repeat] = rate
                runs.append({"name": name, "opsPerSecond": rate,
                    "bytesPerOp": sum(row["bytesPerOp"] * row["count"] for row in windows) / count,
                    "windowOpsMin": min(row["opsPerSecond"] for row in windows),
                    "windowOpsMax": max(row["opsPerSecond"] for row in windows),
                    "allLatenciesSampled": all(row["sampled"] == row["count"] for row in windows)})
                all_windows.extend(windows)
            if runs:
                rates = [run["opsPerSecond"] for run in runs]
                versions[version] = {"medianOps": statistics.median(rates), "minOps": min(rates), "maxOps": max(rates),
                    "medianBytesPerOp": statistics.median(run["bytesPerOp"] for run in runs),
                    "medianWindowP95us": statistics.median(row["p95us"] for row in all_windows),
                    "medianWindowP99us": statistics.median(row["p99us"] for row in all_windows), "runs": runs}
        ratios = []
        if len(group["versions"]) == 2:
            first, second = group["versions"]
            ratios = [indexed[second, i] / indexed[first, i] for i in range(group.get("rounds", 4))
                if (first, i) in indexed and (second, i) in indexed]
        summary.append({"group": group["name"], "case": case, "versions": versions,
            "pairedRatiosSecondOverFirst": ratios,
            "medianPairedRatio": statistics.median(ratios) if ratios else None})
print(json.dumps(summary, indent=2))
