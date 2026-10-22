#!/usr/bin/env python3
"""Background an already running game and reopen its launcher; assert activity/process reuse."""

import argparse
import json
from pathlib import Path
import re
import subprocess
import time

from inspect_game import find_adb, run, select_device

PACKAGE = "com.bazaarplusplus.android.launcher"
COMPONENT = PACKAGE + "/dev.allofus.fusioncore.LauncherActivity"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adb")
    parser.add_argument("--serial")
    parser.add_argument("--cycles", type=int, default=3)
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parent / "artifacts/launcher/resume")
    args = parser.parse_args()
    if args.cycles < 1:
        parser.error("--cycles must be positive")
    adb = find_adb(args.adb)
    serial = select_device(run([adb, "devices", "-l"]), args.serial)
    command = [adb, "-s", serial]
    args.output.mkdir(parents=True, exist_ok=True)

    def snapshot(name):
        state = run(command + ["shell", "dumpsys", "activity", "activities"])
        (args.output / (name + ".txt")).write_text(state)
        records = re.findall(r"\* Hist\s+#\d+: (ActivityRecord\{[^\n]+)", state)
        records = [record for record in records if PACKAGE + "/" in record]
        resumed = re.search(r"topResumedActivity=(ActivityRecord\{[^\n]+)", state)
        return {"records": records, "resumed": resumed.group(1) if resumed else "",
                "pid": run(command + ["shell", "pidof", PACKAGE])}

    initial = snapshot("initial")
    if "/dev.allofus.fusioncore.StubActivity " not in initial["resumed"]:
        raise SystemExit("Open the running game before verification; no game is launched by this script")
    checks = {}
    for cycle in range(1, args.cycles + 1):
        run(command + ["shell", "input", "keyevent", "KEYCODE_HOME"])
        time.sleep(1)
        # Match the MAIN/LAUNCHER flags observed from MIUI's home-screen icon.
        run(command + ["shell", "am", "start", "-W", "-a", "android.intent.action.MAIN",
                       "-c", "android.intent.category.LAUNCHER", "-f", "0x10600000", "-n", COMPONENT])
        deadline = time.monotonic() + 10
        while True:
            current = snapshot(f"cycle-{cycle}")
            if (current["resumed"] == initial["resumed"] and current["records"] == initial["records"]) or time.monotonic() >= deadline:
                break
            time.sleep(0.5)
        checks[f"cycle{cycle}SameGameActivity"] = current["resumed"] == initial["resumed"]
        checks[f"cycle{cycle}SameStack"] = current["records"] == initial["records"]
        checks[f"cycle{cycle}SameProcess"] = current["pid"] == initial["pid"]
        with (args.output / f"cycle-{cycle}.png").open("wb") as out:
            subprocess.run(command + ["exec-out", "screencap", "-p"], stdout=out, check=True)
        if not all(checks.values()):
            break
    (args.output / "verification.json").write_text(json.dumps({"serial": serial, "initial": initial, "checks": checks}, indent=2) + "\n")
    print(json.dumps(checks, indent=2))
    if not all(checks.values()):
        raise SystemExit("Launcher resume verification failed")


if __name__ == "__main__":
    main()
