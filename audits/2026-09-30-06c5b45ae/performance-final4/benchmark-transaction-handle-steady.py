#!/usr/bin/env python3
"""Run fresh-process transaction comparisons in alternating build order."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--config", required=True, type=Path)
parser.add_argument("--output", required=True, type=Path)
args = parser.parse_args()
config = json.loads(args.config.read_text())
args.output.mkdir(parents=True, exist_ok=True)
manifest = {"configuration": config, "driver_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(), "runs": []}
for group in config["groups"]:
    for scenario in group["cases"]:
        case = "-".join(map(str, scenario))
        for repeat in range(group.get("rounds", 4)):
            order = group["versions"] if repeat % 2 == 0 else list(reversed(group["versions"]))
            for position, version in enumerate(order):
                definition = config["versions"][version]
                warmup, windows = group.get("warmup", 10), group.get("windows", 10)
                name = f'{group["name"]}-{case}-{repeat}-{position}-{version}'
                invocation = list(scenario)
                if "mode" in definition:
                    invocation[1] = definition["mode"]
                command = ["dotnet", definition["runner"], definition["revision"], "steady", *map(str, invocation), str(warmup), str(windows)]
                environment = os.environ.copy()
                tiered = group.get("tiered", "0")
                if tiered is None:
                    environment.pop("DOTNET_TieredCompilation", None)
                else:
                    environment["DOTNET_TieredCompilation"] = tiered
                started = datetime.datetime.now(datetime.timezone.utc).isoformat()
                result = subprocess.run(command, env=environment, capture_output=True, text=True,
                    timeout=warmup + windows * 3 + 60)
                (args.output / f"{name}.jsonl").write_text(result.stdout)
                (args.output / f"{name}.stderr").write_text(result.stderr)
                manifest["runs"].append({"name": name, "started": started, "command": command,
                    "tiered": tiered, "exit": result.returncode})
                (args.output / "manifest.json").write_text(json.dumps(manifest, indent=2))
                if result.returncode:
                    raise SystemExit(f"{name} failed; see retained stderr")
                rows = [json.loads(line) for line in result.stdout.splitlines()]
                measured = [row for row in rows if row["phase"] == "window"]
                if len(measured) != windows or rows[-1]["phase"] != "verified":
                    raise SystemExit(f"{name}: incomplete measurement or cold verification")
                throughput = sum(row["count"] for row in measured) / sum(row["elapsed"] for row in measured)
                print(f"{name}: {throughput:.1f} ops/s, verified", flush=True)
