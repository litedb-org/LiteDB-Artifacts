#!/usr/bin/env python3
"""Run only after the root agent explicitly releases the benchmark freeze.

Creates an additive proof bundle; does not edit tracked files or push.
"""
import argparse
import hashlib
import json
import os
import re
from pathlib import Path
import shlex
import shutil
import signal
import subprocess
import time
import zipfile


def digest(path):
    h = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def manifest(directory):
    return {str(p.relative_to(directory)): digest(p)
            for p in sorted(directory.rglob("*")) if p.is_file()}


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n")


def group_exists(group):
    try:
        os.killpg(group, 0)
        return True
    except ProcessLookupError:
        return False


def stop_group(process):
    # Every launched command owns its new session/process group, including the
    # proof's peer and scenario subprocesses. Never signal the agent's group.
    for sig in (signal.SIGTERM, signal.SIGKILL):
        try:
            os.killpg(process.pid, sig)
        except ProcessLookupError:
            pass
        try:
            process.wait(timeout=2)
        except subprocess.TimeoutExpired:
            pass
        deadline = time.monotonic() + 2
        while group_exists(process.pid) and time.monotonic() < deadline:
            time.sleep(0.02)
    process.wait(timeout=5)


def command(argv, cwd, destination, expected=0):
    destination.mkdir(parents=True, exist_ok=False)
    write_json(destination / "command.json", {"argv": list(map(str, argv)), "cwd": str(cwd)})
    (destination / "command.txt").write_text(shlex.join(list(map(str, argv))) + "\n")
    started = time.time()
    with (destination / "stdout.log").open("wb") as stdout, (destination / "stderr.log").open("wb") as stderr:
        process = subprocess.Popen(list(map(str, argv)), cwd=cwd, stdout=stdout, stderr=stderr,
                                   start_new_session=True)
        # Capture the actual runtime mapping when Linux exposes it. Runtimeconfig
        # and the installed-runtime listing are retained on every platform.
        maps = Path(f"/proc/{process.pid}/maps")
        deadline = time.monotonic() + 5
        while process.poll() is None and time.monotonic() < deadline:
            try:
                content = maps.read_text()
                core = next((line for line in content.splitlines() if "libcoreclr.so" in line), None)
                if core:
                    (destination / "runtime-process-maps.txt").write_text(content)
                    runtime_file = Path(core.split()[-1])
                    write_json(destination / "actual-runtime.json", {"pid": process.pid,
                        "path": str(runtime_file), "sha256": digest(runtime_file)})
                    break
            except (OSError, StopIteration):
                pass
            time.sleep(0.01)
        timed_out = False
        try:
            code = process.wait(timeout=900)
        except subprocess.TimeoutExpired:
            timed_out = True
            stop_group(process)
            code = process.returncode
        deadline = time.monotonic() + 2
        while group_exists(process.pid) and time.monotonic() < deadline:
            time.sleep(0.02)
        descendants_remained = group_exists(process.pid)
        if descendants_remained:
            stop_group(process)
        group_gone = not group_exists(process.pid)
    write_json(destination / "exit.json", {"exitCode": code, "expectedExitCode": expected,
        "startedUnix": started, "finishedUnix": time.time(), "processGroup": process.pid,
        "harnessTimeout": timed_out, "unexpectedLiveDescendants": descendants_remained,
        "processGroupGone": group_gone})
    # A child-lifetime failure is not an expected proof outcome even if the
    # parent happened to return the expected exit code and print its marker.
    return code if not timed_out and not descendants_remained and group_gone else 125


parser = argparse.ArgumentParser()
parser.add_argument("--freeze-released", action="store_true", required=True)
parser.add_argument("--repo", type=Path, default=Path("__WORKSPACE__"))
parser.add_argument("--revision", default="e8559b642")
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()
repo = args.repo.resolve()
root = args.output.resolve()
root.mkdir(parents=True, exist_ok=False)
revision = subprocess.check_output(["git", "rev-parse", args.revision], cwd=repo, text=True).strip()
tree = subprocess.check_output(["git", "rev-parse", revision + ":LiteDB"], cwd=repo, text=True).strip()
write_json(root / "identity.json", {"sourceRevision": revision, "productionTree": tree,
    "knownBadRevision": "569ba13c3b3867131c8687e7884bed65131edfbf",
    "knownBadPackage": "0.0.0-knownbad.569ba13c3b38", "testingEnabled": False,
    "configuration": "Release", "framework": "net8.0"})
# MSBuild and compiler servers must not survive in an owned build process group.
os.environ["MSBUILDDISABLENODEREUSE"] = "1"
os.environ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0"
(root / "capture-limits.txt").write_text(
    "Earlier author package runs passed but their runner binaries were overwritten by source builds.\n"
    "This additive rerun captures distinct exact binaries and fixtures for both variants.\n"
    "Earlier first-use bounded F2 logs are interim evidence, not proof of child native admission.\n"
    "These are local standalone runs; hosted formal evaluator results remain separate evidence.\n")
shutil.copy2(Path(__file__), root / "capture-script.py")
feed = root / "feed"
feed.mkdir()
package = repo / "artifacts_temp/review536885-knownbad-feed/LiteDB.0.0.0-knownbad.569ba13c3b38.nupkg"
shutil.copy2(package, feed / package.name)
write_json(root / "package-before.json", manifest(feed))
source_paths = ["LiteDB", "Directory.Build.props", "Directory.Build.targets", "global.json",
    "NuGet.Config", "GitVersion.yml", "LiteDB.ReproRunner/LiteDB.ReproRunner.Shared",
    "LiteDB.ReproRunner/Repros/Issue_3067_SharedPinCallbackWrite",
    "LiteDB.ReproRunner/Repros/Issue_3067_SharedOrdinaryCallbackBegin",
    "LiteDB.ReproRunner/Repros/SharedPinCallbackProof"]
available_paths = [path for path in source_paths if subprocess.run(
    ["git", "cat-file", "-e", revision + ":" + path], cwd=repo,
    stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0]
write_json(root / "selected-source-paths.json", available_paths)
(root / "source-commit-link.txt").write_text("https://github.com/JKamsker/LiteDB/commit/" + revision + "\n")
with (root / "selected-committed-source.tar").open("wb") as target:
    subprocess.run(["git", "archive", revision, "--", *available_paths], cwd=repo, stdout=target, check=True)
worktree = root / "checkout"
if command(["git", "worktree", "add", "--detach", worktree, revision], repo, root / "worktree-add") != 0:
    raise SystemExit("Worktree setup failed; evidence retained")
for suffix in ["--info", "--list-sdks", "--list-runtimes"]:
    if command(["dotnet", suffix], worktree, root / ("dotnet" + suffix)) != 0:
        raise SystemExit("Runtime inventory failed; evidence retained")
source_before = subprocess.check_output(["git", "status", "--porcelain"], cwd=worktree, text=True)
if source_before:
    raise SystemExit("New detached checkout is unexpectedly dirty")
projects = ["Issue_3067_SharedPinCallbackWrite", "Issue_3067_SharedOrdinaryCallbackBegin"]
results = []
for project in projects:
    for variant in ["package", "source"]:
        destination = root / project / variant
        destination.mkdir(parents=True)
        runtime = destination / "runtime"
        build = ["dotnet", "build", worktree / "LiteDB.ReproRunner/Repros" / project,
            "-c", "Release", "-f", "net8.0", "--output", runtime,
            "--disable-build-servers", "-p:UseSharedCompilation=false", "-nodeReuse:false",
            "-p:TestingEnabled=false", "-p:UseProjectReference=" + str(variant == "source").lower(),
            "-p:LiteDBPackageVersion=0.0.0-knownbad.569ba13c3b38",
            "-p:RestorePackagesPath=" + str(root / "nuget-packages"),
            "-p:RestoreAdditionalProjectSources=" + str(feed)]
        if command(build, worktree, destination / "build") != 0:
            raise SystemExit(f"Build failed: {project}/{variant}; evidence retained")
        before = manifest(runtime)
        write_json(destination / "runtime-before.json", before)
        if variant == "package":
            with zipfile.ZipFile(feed / package.name) as archive:
                packaged_hash = hashlib.sha256(archive.read("lib/net8.0/LiteDB.dll")).hexdigest()
            loaded_hash = digest(runtime / "LiteDB.dll")
            write_json(destination / "package-assembly-verification.json", {
                "packageEntry": "lib/net8.0/LiteDB.dll", "packageAssemblySha256": packaged_hash,
                "runtimeAssemblySha256": loaded_hash, "matches": packaged_hash == loaded_hash})
            if packaged_hash != loaded_hash:
                raise SystemExit("Baseline runtime assembly does not match retained actual569 package")
        else:
            # Generated AssemblyInfo is retained as the readable build metadata;
            # require its exact informational version (including commit identity)
            # to occur in the actual runtime DLL's serialized attribute blob.
            matches = []
            dll = (runtime / "LiteDB.dll").read_bytes()
            metadata_dir = destination / "generated-assembly-metadata"
            metadata_dir.mkdir()
            for generated in (worktree / "LiteDB/obj").rglob("*AssemblyInfo.cs"):
                content = generated.read_text()
                for version in re.findall(r'AssemblyInformationalVersionAttribute\("([^"\n]+)"\)', content):
                    if (revision in version or revision[:9] in version) and version.encode() in dll:
                        matches.append({"path": str(generated), "informationalVersion": version})
                        target = metadata_dir / generated.relative_to(worktree / "LiteDB/obj")
                        target.parent.mkdir(parents=True, exist_ok=True)
                        shutil.copy2(generated, target)
            built_tree = subprocess.check_output(["git", "rev-parse", "HEAD:LiteDB"], cwd=worktree, text=True).strip()
            built_commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=worktree, text=True).strip()
            write_json(destination / "source-assembly-verification.json", {
                "runtimeAssemblySha256": digest(runtime / "LiteDB.dll"), "metadataMatches": matches,
                "sourceRevision": built_commit, "productionTree": built_tree})
            if not matches or built_commit != revision or built_tree != tree:
                raise SystemExit("Source runtime assembly lacks the expected build commit metadata")
        expected = 0 if variant == "package" else 10
        actual = command(["dotnet", runtime / (project + ".dll")], worktree, destination / "run", expected)
        after = manifest(runtime)
        write_json(destination / "runtime-after.json", after)
        output = (destination / "run/stdout.log").read_text()
        marker = "BUG_REPRODUCED" if variant == "package" else "VERIFIED_FIXED"
        roots = [line.removeprefix("DATABASE_ROOT ") for line in output.splitlines()
                 if line.startswith("DATABASE_ROOT ")]
        write_json(destination / "fixture-roots.json", roots)
        exit_details = json.loads((destination / "run/exit.json").read_text())
        safe_to_capture = (exit_details["processGroupGone"] and
            not exit_details["unexpectedLiveDescendants"] and not exit_details["harnessTimeout"])
        valid = (actual == expected and marker in output and before == after and len(roots) == 1
                 and safe_to_capture)
        results.append({"project": project, "variant": variant, "expectedExitCode": expected,
            "actualExitCode": exit_details["exitCode"], "harnessOutcomeCode": actual,
            "expectedMarker": marker, "runtimeUnchanged": before == after,
            "safeToCaptureFixtures": safe_to_capture,
            "valid": valid})
        write_json(root / "results.json", results)
        if not valid:
            raise SystemExit(f"Proof failed: {project}/{variant}; original fixture paths retained, no fixture copy")
        # Copy only successful, validated fixtures after the entire owned
        # process group is gone. Do not turn a killed/live child into safety evidence.
        fixtures = destination / "fixtures"
        fixtures.mkdir()
        for original in roots:
            shutil.copytree(original, fixtures / Path(original).name)
        write_json(destination / "fixture-sha256.json", manifest(fixtures))
write_json(root / "package-after.json", manifest(feed))
status = subprocess.check_output(["git", "status", "--porcelain"], cwd=worktree, text=True)
(root / "source-status-after.txt").write_text(status)
if status or manifest(feed) != json.loads((root / "package-before.json").read_text()):
    raise SystemExit("Source/package changed during capture")
# Leave the detached checkout registered for inspection; archive publication is
# coordinated separately after the agent reports these completed local results.
print(root)
