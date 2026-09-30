#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
case "$(uname -m)" in
    x86_64) architecture=x64 ;;
    aarch64) architecture=arm64 ;;
    *) printf 'Unsupported test architecture\n' >&2; exit 1 ;;
esac
image=litedb-admission-glibc231
docker build -t "$image" -f "$repo_root/.github/docker/native-admission-glibc231.Dockerfile" "$repo_root/.github/docker"
test "$(docker run --rm "$image" getconf GNU_LIBC_VERSION)" = 'glibc 2.31'
docker run --rm "$image" dotnet --info

results="$repo_root/LiteDB.Tests/TestResults"
mkdir -p "$results"
container_user="$(id -u):$(id -g)"
case "$(docker info --format '{{json .SecurityOptions}}')" in
    *rootless*) container_user=0:0 ;; # The invoking host user maps to container uid 0.
esac
# Use packaged outputs and the invoking host user's permissions; no network or
# restore is needed. Parent and children assert actual runtime/architecture.
test_filter='FullyQualifiedName~TransactionHandle|FullyQualifiedName~NativeAdmission|FullyQualifiedName~SharedMode|FullyQualifiedName~SharedAdmissionLifetime|FullyQualifiedName~DirectModeAdmission|FullyQualifiedName~Rebuild|FullyQualifiedName~TestHost_Tests'
container=(docker run --rm --network none --user "$container_user"
    -e LITEDB_EXPECTED_RUNTIME_MAJOR=8 -e LITEDB_EXPECTED_ARCHITECTURE="$architecture"
    -e LITEDB_MAPPED_TEST_DIRECTORY=/results -e LITEDB_RETAINED_FIXTURES=/results/retained-fixtures
    --mount "type=bind,src=$repo_root,dst=/repo,readonly"
    --mount "type=bind,src=$results,dst=/results"
    --workdir /repo/LiteDB.Tests/bin/Release/net8.0)
"${container[@]}" "$image" dotnet vstest LiteDB.Tests.dll \
    /ListFullyQualifiedTests /ListTestsTargetPath:/results/discovered-tests.txt
python3 "$repo_root/.github/scripts/record_test_leg.py" --root "$repo_root" --results "$results" \
    --discovery "$results/discovered-tests.txt" --filter "$test_filter" \
    --result "NativeAdmission-glibc231-$architecture.trx" --framework net8.0 --runtime 8 --architecture "$architecture" --partition
python3 - "$results/native-partitions.json" "$results/native-partitions.tsv" <<'PYPLAN'
import json, sys
with open(sys.argv[2], 'w') as output:
    for name, item in json.load(open(sys.argv[1])).items():
        output.write(name + '\t' + item['result'] + '\t' + item['filter'] + '\n')
PYPLAN
failed=0
while IFS=$'\t' read -r partition result partition_filter; do
    printf 'Running native selection partition: %s\n' "$partition"
    set +e
    "${container[@]}" "$image" sh -c '
        dotnet "$@"
        test_status=$?
        python3 /repo/scripts/collect-retained-fixtures.py "$LITEDB_RETAINED_FIXTURES"
        collect_status=$?
        if [ "$test_status" -ne 0 ]; then exit "$test_status"; fi
        exit "$collect_status"
    ' sh vstest LiteDB.Tests.dll \
        /Settings:/repo/tests.runsettings /ResultsDirectory:/results \
        "/Logger:trx;LogFileName=$result" \
        "/TestCaseFilter:($partition_filter)|FullyQualifiedName~TestHost_Tests"
    container_status=$?
    set -e
    if [ "$container_status" -ne 0 ]; then failed=1; fi
done < "$results/native-partitions.tsv"
if [ "$failed" -ne 0 ]; then exit 1; fi

python3 - "$results" <<'PY'
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
root = Path(sys.argv[1])
all_results = []
for item in json.loads((root / 'native-partitions.json').read_text()).values():
    results = ET.parse(root / item['result']).findall('.//t:UnitTestResult', ns)
    for guard in ('RequestedRuntimeAndArchitecture_AreActuallyRunning', 'LoadedLibrary_ContainsTheRequiredEngineTestHooks'):
        assert sum(r.get('testName', '').endswith(guard) and r.get('outcome') == 'Passed' for r in results) == 1, guard
    all_results.extend(results)
assert any('NativeAdmissionProcess_Tests' in r.get('testName', '') and r.get('outcome') == 'Passed' for r in all_results)
PY

bash "$repo_root/scripts/test-native-admission-bind-mount.sh" "$image"
