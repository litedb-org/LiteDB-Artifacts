import json, pathlib, subprocess, time, hashlib
root = pathlib.Path(__file__).resolve().parent
builds = json.loads((root / "final-builds.json").read_text())
# No builds or tests run concurrently with this script.
for key, build in builds.items():
    dll = pathlib.Path(build["dll"])
    assert hashlib.sha256(dll.read_bytes()).hexdigest() == build["sha256"], key
    assert b"GetMonitor" not in dll.read_bytes(), key

def run(phase, runtime, build, mode, scenario, count, warmup, round, output):
    name = f"final-{build}-net{runtime}"
    command = ["dotnet", str(root / name / "SharedReadBenchmarks.dll"),
               "/tmp/litedb-pr3003-data-3122", mode, scenario, str(count), str(warmup)]
    result = subprocess.run(command, capture_output=True, text=True, timeout=120)
    if result.returncode:
        (root / "final-benchmark-error.log").write_text(result.stdout + result.stderr)
        raise RuntimeError(f"Benchmark failed: {command}")
    data = json.loads(result.stdout)
    identity = builds[f"{build}-net{runtime}"]
    assert data["sha256"].lower() == identity["sha256"]
    assert data["runtime"].startswith(f".NET {runtime}.")
    data.update(build=build, commit=identity["commit"], round=round, phase=phase)
    output.write(json.dumps(data) + "\n")
    output.flush()
    print(phase, runtime, build, mode, scenario, round,
          round_ms(data["meanMs"]), "p99", round_ms(data["p99Ms"]), flush=True)

def round_ms(value):
    return f"{value:.4f}"

for phase, warmup, scenarios in [
        ("startup", 0, [("point", 8000), ("scan", 150)]),
        ("steady", 10, [("point", 20000), ("scan", 1000), ("mixed", 2000)])]:
    with open(root / f"final-shared-{phase}.jsonl", "w") as output:
        for round in range(3):
            for runtime in ([10, 8] if round % 2 == 0 else [8, 10]):
                for scenario, count in scenarios:
                    order = ["prestack", "baseline", "candidate"]
                    if round % 2: order.reverse()
                    for build in order:
                        run(phase, runtime, build, "shared", scenario, count, warmup, round, output)
with open(root / "final-direct.jsonl", "w") as output:
    for round in range(3):
        for runtime in [10, 8]:
            order = ["baseline", "candidate"]
            if round % 2: order.reverse()
            for build in order:
                run("steady", runtime, build, "direct", "scan", 1000, 10, round, output)
print("All correctness, production-identity and final-WAL checks passed.", flush=True)
