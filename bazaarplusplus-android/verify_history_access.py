#!/usr/bin/env python3
"""Check the history lease from a separate Android process without starting or stopping the game.

Compiles the production lock and the small device runner, uploads only the test DEX to
/data/local/tmp, and records its actual exit code. The game root must be confirmed first.
This does not validate SQLite, snapshots, archive operations or game/UI state.
"""

import argparse
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import time

from inspect_game import find_adb, run, select_device


def main():
    root = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adb")
    parser.add_argument("--serial")
    parser.add_argument("--game-root", required=True, help="Confirmed absolute target game root from launcher configuration")
    parser.add_argument("--expect", choices=("busy", "available"), required=True)
    parser.add_argument("--d8", required=True, type=Path, help="Android SDK build-tools d8")
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    # ADB shell interprets its command string; allow only the configured root/package path alphabet.
    if not re.fullmatch(r"/(?:sdcard|storage/emulated/0)/[A-Za-z0-9_-]+/[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)+", args.game_root):
        parser.error("--game-root must be the confirmed external-storage root and game package")
    javac = shutil.which("javac")
    if javac is None:
        parser.error("JDK required")
    adb = find_adb(args.adb)
    serial = select_device(run([adb, "devices", "-l"]), args.serial)
    command = [adb, "-s", serial]
    remote = "/data/local/tmp/bpp-history-access-check.dex"
    with tempfile.TemporaryDirectory() as directory:
        work = Path(directory)
        classes = work / "classes"
        dex = work / "dex"
        classes.mkdir()
        dex.mkdir()
        subprocess.run([javac, "--release", "17", "-d", str(classes),
                        str(root / "launcher/fusionApp/src/main/java/dev/allofus/fusioncore/history/HistoryAccessLock.java"),
                        str(root / "tests/device/HistoryLockDevice.java")], check=True, timeout=30)
        subprocess.run([str(args.d8), "--min-api", "26", "--output", str(dex)] +
                       [str(path) for path in sorted(classes.rglob("*.class"))], check=True, timeout=30)
        subprocess.run(command + ["push", str(dex / "classes.dex"), remote], check=True, timeout=15)
        try:
            result = subprocess.run(command + ["shell", f"CLASSPATH={remote} app_process /system/bin HistoryLockDevice {args.game_root}"],
                                    text=True, capture_output=True, timeout=15)
        finally:
            subprocess.run(command + ["shell", "rm", "-f", remote], check=True, timeout=15)
    expected = 3 if args.expect == "busy" else 0
    evidence = {"serial": serial, "timeMs": int(time.time() * 1000), "gameRoot": args.game_root,
                "expectedExit": expected, "exitCode": result.returncode,
                "stdout": result.stdout, "stderr": result.stderr, "passed": result.returncode == expected}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n")
    print("History access lease:", "passed" if evidence["passed"] else "failed")
    if not evidence["passed"]:
        raise SystemExit("Device process did not observe the expected lock state")


if __name__ == "__main__":
    main()
