#!/usr/bin/env python3
"""Collect failed fixtures after their test host exits, preserving source files."""
import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys


def running(pid):
    if os.name == 'nt':
        kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        kernel.OpenProcess.restype = ctypes.c_void_p
        kernel.CloseHandle.argtypes = [ctypes.c_void_p]
        handle = kernel.OpenProcess(0x1000, False, pid)
        if not handle:
            if ctypes.get_last_error() == 87:  # ERROR_INVALID_PARAMETER: exited
                return False
            raise OSError(ctypes.get_last_error(), 'Cannot inspect fixture host')
        try:
            code = ctypes.c_ulong()
            kernel.GetExitCodeProcess.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_ulong)]
            if not kernel.GetExitCodeProcess(handle, ctypes.byref(code)):
                raise OSError(ctypes.get_last_error(), 'Cannot inspect fixture host')
            return code.value == 259
        finally:
            kernel.CloseHandle(handle)
    try:
        os.kill(pid, 0)
        if sys.platform.startswith('linux'):
            # An exited orphan can remain a zombie under a container PID 1. Its
            # descriptors are already closed even before its parent reaps it.
            try:
                state = Path(f'/proc/{pid}/stat').read_text().rsplit(')', 1)[1].split()[0]
                if state in ('Z', 'X'):
                    return False
            except FileNotFoundError:
                return False
        return True
    except ProcessLookupError:
        return False


def collect(directory):
    failures = []
    for manifest in sorted(directory.glob('*.json')):
        target = directory / manifest.stem
        target.mkdir(exist_ok=True)
        report = {'manifest': str(manifest), 'files': [], 'errors': []}
        try:
            source = json.loads(manifest.read_text(encoding='utf-8-sig'))
            report['primaryFailure'] = source['failure']
            if running(int(source['processId'])):
                raise RuntimeError('Fixture host is still running; refusing to copy')
            directory_prefixes = {'native-crash-directory': 'litedb-native-crash-',
                                  'shared-followup-directory': 'litedb-followup-'}
            if source.get('fixtureKind') in directory_prefixes:
                fixture = Path(source['directory'])
                prefix = directory_prefixes[source['fixtureKind']]
                if not fixture.is_absolute() or not fixture.name.startswith(prefix) or len(fixture.name) != len(prefix) + 32:
                    raise ValueError(f'Expected an absolute GUID {source["fixtureKind"]} directory')
                int(fixture.name[len(prefix):], 16)
                if fixture.is_symlink():
                    raise ValueError('Refusing to follow a fixture directory symlink')
                child_pids = list(source.get('childPids', []))
                if source.get('childPid', 0):
                    child_pids.append(source['childPid'])
                for child_pid in child_pids:
                    if running(int(child_pid)):
                        raise RuntimeError(f'Fixture child {child_pid} is still running; refusing to copy')
                items = sorted(fixture.iterdir())
            else:
                database = Path(source['database'])
                # TempFile owns a GUID basename. Never capture a neighboring GUID.
                stem = database.stem
                if not database.is_absolute() or not stem.startswith('litedb-') or len(stem) != 39:
                    raise ValueError('Expected an absolute GUID TempFile database path')
                int(stem[7:], 16)
                items = [item for item in sorted(database.parent.iterdir())
                         if item.name == database.name or item.name.startswith(stem + '-') or item.name.startswith(stem + '.')]
            for item in items:
                try:
                    if item.is_symlink():
                        raise ValueError('Refusing to follow a fixture symlink')
                    destination = target / item.name
                    if item.is_dir():
                        # Copy symlinks themselves, never outside the fixture tree.
                        shutil.copytree(item, destination, symlinks=True, dirs_exist_ok=True)
                    else:
                        shutil.copy2(item, destination)
                    for copied in ([destination] if destination.is_file() else destination.rglob('*')):
                        if copied.is_file() and not copied.is_symlink():
                            report['files'].append({'source': str(item),
                                'copy': str(copied.relative_to(directory)), 'size': copied.stat().st_size,
                                'sha256': hashlib.sha256(copied.read_bytes()).hexdigest()})
                except Exception as error:
                    report['errors'].append(f'{item}: {error}')
            if not report['files']:
                raise RuntimeError('No retained fixture files were collected')
        except Exception as error:
            report['errors'].append(str(error))
        (target / 'collection-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        failures.extend(report['errors'])
    for failure in failures:
        print('Fixture collection failure (primary test failure preserved): ' + failure, file=sys.stderr)
    return 1 if failures else 0


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    sys.exit(collect(parser.parse_args().directory))
