#!/usr/bin/env python3
"""Inject one two-finger preview gesture at freshly inspected screen coordinates.

Requires JDK javac, an Android SDK android.jar and d8. Does not launch the game,
change input mode or advance encounters. Screenshots require human review.
"""

import argparse
import json
from pathlib import Path
import struct
import subprocess
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--serial", required=True)
    parser.add_argument("--android-jar", type=Path, required=True)
    parser.add_argument("--d8", type=Path, required=True)
    parser.add_argument("--hold", type=int, nargs=2, required=True, metavar=("X", "Y"))
    parser.add_argument("--target", type=int, nargs=2, required=True, metavar=("X", "Y"))
    parser.add_argument("--holder-first", action="store_true", help="Release the mode button while the inspecting finger remains down")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    adb = ["adb", "-s", args.serial]

    def run(*command):
        return subprocess.check_output(adb + list(command), timeout=20)

    def screenshot(name):
        png = run("exec-out", "screencap", "-p")
        if png[:8] != b"\x89PNG\r\n\x1a\n":
            raise RuntimeError("Invalid screenshot")
        (output / name).write_bytes(png)
        return struct.unpack(">II", png[16:24])

    width, height = screenshot("before.png")
    for x, y in (args.hold, args.target):
        if not (0 <= x < width and 0 <= y < height):
            parser.error("Coordinates lie outside the current screen")
    release = json.loads((root / "artifacts/launcher/release.json").read_text())
    log_path = f"/sdcard/{release['storageRoot']}/{release['game']['package']}/BepInEx/LogOutput.log"
    before = run("shell", "cat", log_path)
    (output / "before.log").write_bytes(before)
    subprocess.run(["javac", "--release", "8", "-Xlint:-options", "-classpath", str(args.android_jar.resolve()),
                    "-d", str(output), str(root / "tests/device/HoldGesture.java")], check=True)
    subprocess.run([str(args.d8.resolve()), "--output", str(output), str(output / "HoldGesture.class")], check=True)
    remote = "/data/local/tmp/bpp-hold-gesture.dex"
    run("shell", "rm", "-f", remote)
    run("push", str(output / "classes.dex"), remote)
    command = f"CLASSPATH={remote} app_process /data/local/tmp HoldGesture " + " ".join(
        str(n) for n in (*args.hold, *args.target))
    if args.holder_first:
        command += " holder-first"
    process = subprocess.Popen(adb + ["shell", command])
    try:
        time.sleep(3)
        screenshot("during.png")
    finally:
        process.wait(timeout=20)
    time.sleep(.5)
    screenshot("released.png")
    after = run("shell", "cat", log_path)
    (output / "after.log").write_bytes(after)
    same_session = after.startswith(before)
    added = after[len(before):].decode(errors="replace") if same_session else ""
    assertions = {
        "injection_succeeded": process.returncode == 0,
        "same_log_session": same_session,
        "preview_started": "event=android.hold_preview.started" in added,
        "preview_shown": "event=android.hold_preview.shown" in added,
        "preview_stopped": "event=android.hold_preview.stopped" in added,
        "no_preview_after_stop": "event=android.tooltip.observed" not in added.rsplit("event=android.hold_preview.stopped", 1)[-1],
        "no_mutation_report": "source_unchanged=False" not in added,
        "no_game_command": "Command completed:" not in added,
        "no_state_transition": "State changed from" not in added,
        "no_errors": "[Error" not in added and "Exception" not in added,
    }
    (output / "validation.json").write_text(json.dumps(assertions, indent=2) + "\n")
    print(json.dumps(assertions, indent=2))
    if not all(assertions.values()):
        raise SystemExit(1)


if __name__ == "__main__":
    main()
