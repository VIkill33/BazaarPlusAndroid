#!/usr/bin/env python3
"""Verify production history storage on Android, or inspect an existing target read-only.

Fault tests use only a new isolated UUID database and retain failed test data. The
explicit --inspect-game-root mode opens existing history read-only without initialization.
Successful cleanup is restricted to this invocation's isolated test directory.
"""
import argparse
import json
from pathlib import Path
import re
import select
import shutil
import subprocess
import tempfile
import time
import uuid

from inspect_game import find_adb, run, select_device


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--adb')
    parser.add_argument('--serial')
    parser.add_argument('--android-jar', type=Path, required=True)
    parser.add_argument('--d8', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--kill-transaction', action='store_true', help='SIGKILL an isolated uncommitted production-store write and verify recovery')
    parser.add_argument('--reader', action='store_true', help='Verify read-only pagination and scope boundaries in an isolated database')
    parser.add_argument('--inspect-game-root', help='Read-only inspection of an existing confirmed target; no isolated test or cleanup')
    args = parser.parse_args()
    if args.inspect_game_root and not re.fullmatch(r'/sdcard/BazaarPlusPlusAndroid/[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)+', args.inspect_game_root):
        parser.error('Invalid confirmed game root')
    if args.kill_transaction and args.inspect_game_root:
        parser.error('Transaction fault tests cannot use a game root')
    if args.reader and (args.kill_transaction or args.inspect_game_root):
        parser.error('Reader fixture tests must use an isolated directory')
    root = Path(__file__).resolve().parent
    javac = shutil.which('javac')
    if javac is None or not args.android_jar.is_file() or not args.d8.is_file():
        parser.error('JDK, Android SDK android.jar and d8 are required')
    adb = find_adb(args.adb)
    serial = select_device(run([adb, 'devices', '-l']), args.serial)
    command = [adb, '-s', serial]
    token = str(uuid.uuid4())
    remote = f'/data/local/tmp/bpp-history-store-{token}.dex'
    test_root = f'/sdcard/BppHistoryM1Check/{token}'
    runner = 'HistoryReaderDevice' if args.reader else 'HistoryReadDevice' if args.inspect_game_root else 'HistoryStoreDevice'
    if args.inspect_game_root:
        test_root = args.inspect_game_root
    production = root / 'launcher/fusionApp/src/main/java/dev/allofus/fusioncore/history'
    with tempfile.TemporaryDirectory() as directory:
        work = Path(directory)
        classes, dex = work / 'classes', work / 'dex'
        classes.mkdir()
        dex.mkdir()
        subprocess.run([javac, '--release', '8', '-cp', str(args.android_jar), '-d', str(classes),
                        str(production / 'HistoryAccessLock.java'), str(production / 'HistoryStore.java'),
                        str(production / 'HistoryReader.java'), str(production / 'HistoryPresentation.java'),
                        str(production / 'PurchaseSql.java'),
                        str(root / f'tests/device/{runner}.java')], check=True, timeout=30)
        subprocess.run([str(args.d8), '--min-api', '26', '--lib', str(args.android_jar), '--output', str(dex)] +
                       [str(path) for path in sorted(classes.rglob('*.class'))], check=True, timeout=30)
        subprocess.run(command + ['push', str(dex / 'classes.dex'), remote], check=True, timeout=15)
        try:
            invocation = f'CLASSPATH={remote} app_process /system/bin dev.allofus.fusioncore.history.{runner} {test_root}'
            if args.kill_transaction:
                held = subprocess.Popen(command + ['shell', invocation + ' hold'], text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
                assert held.stdout is not None
                pid = None
                deadline = time.monotonic() + 15
                while time.monotonic() < deadline:
                    readable, _, _ = select.select([held.stdout], [], [], max(0, deadline - time.monotonic()))
                    if not readable:
                        break
                    line = held.stdout.readline()
                    if not line:
                        break
                    if line.startswith('{'):
                        marker = json.loads(line)
                        if marker.get('holdingTransaction') is True:
                            pid = int(marker['pid'])
                            break
                if pid is None:
                    held.terminate()
                    raise RuntimeError('Isolated writer did not reach its uncommitted boundary')
                subprocess.run(command + ['shell', 'kill', '-9', str(pid)], check=True, timeout=10)
                held.communicate(timeout=10)
                invocation += ' recover'
            result = subprocess.run(command + ['shell', invocation], text=True, capture_output=True, timeout=60)
        finally:
            subprocess.run(command + ['shell', 'rm', '-f', remote], check=True, timeout=15)
    rows = [row for row in result.stdout.splitlines() if row.startswith('{')]
    summary = json.loads(rows[-1]) if rows else {}
    passed = result.returncode == 0 and summary.get('passed') is True
    evidence = {'serial': serial, 'timeMs': int(time.time() * 1000), 'testRoot': test_root,
                'exitCode': result.returncode, 'stdout': result.stdout, 'stderr': result.stderr,
                'summary': summary, 'passed': passed, 'killTransaction': args.kill_transaction}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + '\n')
    print(('Read-only history inspection passed' if passed else 'Read-only history inspection failed') if args.inspect_game_root else result.stdout)
    if passed and not args.inspect_game_root:
        subprocess.run(command + ['shell', 'rm', '-rf', test_root], check=True, timeout=15)
    elif not passed:
        print(result.stderr)
        raise SystemExit(f'History store check failed; isolated data retained at {test_root}')


if __name__ == '__main__':
    main()
