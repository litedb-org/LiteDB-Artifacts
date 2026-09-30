import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[2] / 'scripts/collect-retained-fixtures.py'
spec = importlib.util.spec_from_file_location('retained', SCRIPT)
retained = importlib.util.module_from_spec(spec)
spec.loader.exec_module(retained)


class RetainedFixtureTests(unittest.TestCase):
    def test_actual_child_must_exit_before_database_wal_and_coordination_are_copied(self):
        with tempfile.TemporaryDirectory() as root:
            root = Path(root)
            sources, manifests = root / 'original-volume', root / 'artifacts'
            sources.mkdir()
            manifests.mkdir()
            stem = 'litedb-' + 'a' * 32
            child = subprocess.Popen([sys.executable, '-u', '-c', '''
import json, os, pathlib, sys
sources, manifests, stem = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]), sys.argv[3]
for suffix in ('.db', '-log.db', '-shared-live', '-shared-state'):
    (sources / (stem + suffix)).write_bytes(suffix.encode())
(sources / (stem + '-readers')).mkdir()
(sources / (stem + '-readers/lease')).write_bytes(b'lease')
(sources / ('litedb-' + 'b' * 32 + '.db')).write_bytes(b'unrelated')
held = (sources / (stem + '.db')).open('rb')
(manifests / 'failed.json').write_text(json.dumps({'database': str(sources / (stem + '.db')), 'processId': os.getpid(), 'failure': 'original assertion'}))
print('ready', flush=True)
sys.stdin.readline()
''', str(sources), str(manifests), stem], stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)
            try:
                self.assertEqual(child.stdout.readline().strip(), 'ready')
                self.assertEqual(retained.collect(manifests), 1)
                self.assertFalse((manifests / 'failed' / (stem + '.db')).exists())
                child.communicate('\n', timeout=10)
                self.assertEqual(child.returncode, 0)
                self.assertEqual(retained.collect(manifests), 0)
                report = json.loads((manifests / 'failed/collection-report.json').read_text())
                self.assertEqual(report['primaryFailure'], 'original assertion')
                self.assertEqual(len(report['files']), 5)
                for source in sources.rglob('*'):
                    if source.is_file() and source.name != 'litedb-' + 'b' * 32 + '.db':
                        copied = manifests / 'failed' / source.relative_to(sources)
                        self.assertEqual(copied.read_bytes(), source.read_bytes())
                self.assertFalse((manifests / 'failed' / ('litedb-' + 'b' * 32 + '.db')).exists())
            finally:
                if child.poll() is None:
                    child.kill()
                    child.communicate()

    def test_native_crash_directory_waits_for_child_and_retains_exact_recovery_set(self):
        with tempfile.TemporaryDirectory() as root:
            root = Path(root)
            fixture = root / ('litedb-native-crash-' + 'c' * 32)
            fixture.mkdir()
            artifacts = root / 'artifacts'
            artifacts.mkdir()
            for name in ('data.db', 'data-log.db', 'data-temp.db', 'data-rebuild.db'):
                (fixture / name).write_bytes(name.encode())
            unrelated = root / 'neighbor.db'
            unrelated.write_bytes(b'untouched')
            child = subprocess.Popen([sys.executable, '-u', '-c',
                'import sys; held = open(sys.argv[1], "rb"); print("ready", flush=True); sys.stdin.readline()',
                str(fixture / 'data-rebuild.db')], stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)
            try:
                self.assertEqual(child.stdout.readline().strip(), 'ready')
                manifest = {'fixtureKind': 'native-crash-directory', 'directory': str(fixture),
                            'processId': 1, 'childPid': child.pid, 'failure': 'primary marker sharing failure'}
                (artifacts / 'failed.json').write_text(json.dumps(manifest))
                actual_running = retained.running
                with patch.object(retained, 'running', side_effect=lambda pid: False if pid == 1 else actual_running(pid)):
                    self.assertEqual(retained.collect(artifacts), 1)
                    self.assertFalse((artifacts / 'failed/data.db').exists())
                    child.communicate('\n', timeout=10)
                    self.assertEqual(retained.collect(artifacts), 0)
                report = json.loads((artifacts / 'failed/collection-report.json').read_text())
                self.assertEqual(report['primaryFailure'], manifest['failure'])
                self.assertEqual(len(report['files']), 4)
                for source in fixture.iterdir():
                    self.assertEqual((artifacts / 'failed' / source.name).read_bytes(), source.read_bytes())
                self.assertFalse((artifacts / 'failed/neighbor.db').exists())
                self.assertEqual(unrelated.read_bytes(), b'untouched')
            finally:
                if child.poll() is None:
                    child.kill()
                    child.communicate()

    def test_native_crash_manifest_cannot_capture_an_arbitrary_directory(self):
        with tempfile.TemporaryDirectory() as root:
            root = Path(root)
            (root / 'unrelated.db').write_bytes(b'untouched')
            (root / 'failed.json').write_text(json.dumps({'fixtureKind': 'native-crash-directory',
                'directory': str(root), 'processId': 1, 'failure': 'primary'}))
            with patch.object(retained, 'running', return_value=False):
                self.assertEqual(retained.collect(root), 1)
            self.assertFalse((root / 'failed/unrelated.db').exists())

    def test_shared_followup_waits_for_host_and_keeps_raw_files_and_diagnostics(self):
        with tempfile.TemporaryDirectory() as root:
            root = Path(root)
            fixture = root / ('litedb-followup-' + 'd' * 32)
            fixture.mkdir()
            artifacts = root / 'artifacts'
            artifacts.mkdir()
            files = {'data.db': b'database sentinel', 'data-log.db': b'wal sentinel',
                     'failure-diagnostics.txt': b'child exited; native-wait; last commit 19'}
            for name, data in files.items():
                (fixture / name).write_bytes(data)
            (root / 'neighbor.db').write_bytes(b'untouched')
            host = subprocess.Popen([sys.executable, '-u', '-c',
                'import sys; held = open(sys.argv[1], "rb"); print("ready", flush=True); sys.stdin.readline()',
                str(fixture / 'data.db')], stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)
            try:
                self.assertEqual(host.stdout.readline().strip(), 'ready')
                manifest = {'fixtureKind': 'shared-followup-directory', 'directory': str(fixture),
                            'phase': 'writer progress', 'processId': host.pid, 'failure': 'original progress failure'}
                (artifacts / 'failed.json').write_text(json.dumps(manifest))
                self.assertEqual(retained.collect(artifacts), 1)
                self.assertFalse((artifacts / 'failed/data.db').exists())
                host.communicate('\n', timeout=10)
                self.assertEqual(retained.collect(artifacts), 0)
                report = json.loads((artifacts / 'failed/collection-report.json').read_text())
                self.assertEqual(report['primaryFailure'], manifest['failure'])
                self.assertEqual(len(report['files']), len(files))
                for name, data in files.items():
                    self.assertEqual((fixture / name).read_bytes(), data)
                    self.assertEqual((artifacts / 'failed' / name).read_bytes(), data)
                self.assertFalse((artifacts / 'failed/neighbor.db').exists())
                self.assertEqual((root / 'neighbor.db').read_bytes(), b'untouched')
            finally:
                if host.poll() is None:
                    host.kill()
                    host.communicate()

    def test_shared_followup_waits_for_every_child_after_host_exit(self):
        with tempfile.TemporaryDirectory() as root:
            root = Path(root)
            fixture = root / ('litedb-followup-' + 'f' * 32)
            fixture.mkdir()
            database = fixture / 'data.db'
            database.write_bytes(b'raw database sentinel')
            artifacts = root / 'artifacts'
            artifacts.mkdir()
            children = []
            try:
                for _ in range(2):
                    child = subprocess.Popen([sys.executable, '-u', '-c',
                        'import sys; held = open(sys.argv[1], "rb"); print("ready", flush=True); sys.stdin.readline()',
                        str(database)], stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)
                    children.append(child)
                    self.assertEqual(child.stdout.readline().strip(), 'ready')
                manifest = {'fixtureKind': 'shared-followup-directory', 'directory': str(fixture),
                            'processId': 1, 'childPids': [child.pid for child in children], 'failure': 'primary cleanup failure'}
                (artifacts / 'failed.json').write_text(json.dumps(manifest))
                actual_running = retained.running
                with patch.object(retained, 'running', side_effect=lambda pid: False if pid == 1 else actual_running(pid)):
                    for child in children:
                        self.assertEqual(retained.collect(artifacts), 1)
                        self.assertFalse((artifacts / 'failed/data.db').exists())
                        report = json.loads((artifacts / 'failed/collection-report.json').read_text())
                        self.assertEqual(report['primaryFailure'], manifest['failure'])
                        self.assertIn(str(child.pid), report['errors'][0])
                        child.communicate('\n', timeout=10)
                    self.assertEqual(retained.collect(artifacts), 0)
                self.assertEqual(database.read_bytes(), b'raw database sentinel')
                self.assertEqual((artifacts / 'failed/data.db').read_bytes(), database.read_bytes())
            finally:
                for child in children:
                    if child.poll() is None:
                        child.kill()
                        child.communicate()

    def test_shared_followup_cannot_capture_other_fixture_kinds_or_arbitrary_directories(self):
        for name in ('arbitrary', 'litedb-native-crash-' + 'e' * 32, 'litedb-followup-not-a-guid'):
            with self.subTest(name=name), tempfile.TemporaryDirectory() as root:
                root = Path(root)
                fixture = root / name
                fixture.mkdir()
                (fixture / 'data.db').write_bytes(b'untouched')
                (root / 'failed.json').write_text(json.dumps({'fixtureKind': 'shared-followup-directory',
                    'directory': str(fixture), 'processId': 1, 'failure': 'primary'}))
                with patch.object(retained, 'running', return_value=False):
                    self.assertEqual(retained.collect(root), 1)
                self.assertFalse((root / 'failed/data.db').exists())
                self.assertEqual((fixture / 'data.db').read_bytes(), b'untouched')

    def test_copy_failure_is_separate_and_remaining_files_are_collected(self):
        with tempfile.TemporaryDirectory() as root:
            root = Path(root)
            stem = 'litedb-' + 'a' * 32
            db = root / (stem + '.db')
            db.write_bytes(b'database')
            (root / (stem + '-log.db')).write_bytes(b'wal')
            (root / 'failed.json').write_text(json.dumps({'database': str(db), 'processId': 1, 'failure': 'primary'}))
            original = retained.shutil.copy2
            def copy(source, destination):
                if source == db:
                    raise OSError('copy failure sentinel')
                return original(source, destination)
            with patch.object(retained, 'running', return_value=False), patch.object(retained.shutil, 'copy2', side_effect=copy):
                self.assertEqual(retained.collect(root), 1)
            report = json.loads((root / 'failed/collection-report.json').read_text())
            self.assertEqual(report['primaryFailure'], 'primary')
            self.assertIn('copy failure sentinel', report['errors'][0])
            self.assertEqual(len(report['files']), 1)
            self.assertEqual(db.read_bytes(), b'database')

    @unittest.skipUnless(sys.platform.startswith('linux'), 'Linux zombie process state')
    def test_exited_unreaped_child_has_no_live_handles(self):
        child = subprocess.Popen([sys.executable, '-c', 'pass'])
        try:
            os.waitid(os.P_PID, child.pid, os.WEXITED | os.WNOWAIT)
            self.assertFalse(retained.running(child.pid))
        finally:
            child.wait(timeout=10)

    def test_no_failures_is_noop(self):
        with tempfile.TemporaryDirectory() as root:
            self.assertEqual(retained.collect(Path(root)), 0)
