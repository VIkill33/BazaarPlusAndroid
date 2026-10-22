#!/usr/bin/env python3
"""Capture a long-press preview and reject game-state-changing command evidence.

Coordinates must come from a freshly inspected screenshot. This tool never taps,
selects a mode, launches a game, or advances an encounter. Visual content still
requires manual review; the assertions cover runtime logs, not image semantics.
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
    parser.add_argument("--x", required=True, type=int)
    parser.add_argument("--y", required=True, type=int)
    parser.add_argument("--name", required=True, help="Evidence filename prefix")
    args = parser.parse_args()
    if not args.name.replace("-", "").replace("_", "").isalnum():
        parser.error("name must contain only letters, numbers, '-' or '_'")
    output = Path(__file__).resolve().parent / "artifacts/phone"
    output.mkdir(parents=True, exist_ok=True)
    adb = ["adb", "-s", args.serial]
    log_path = "/sdcard/FusionCore/com.tempogames.thebazaar/BepInEx/LogOutput.log"

    def run(*command):
        return subprocess.check_output(adb + list(command), timeout=20)

    def screenshot(suffix):
        data = run("exec-out", "screencap", "-p")
        if data[:8] != b"\x89PNG\r\n\x1a\n":
            raise RuntimeError("Invalid device screenshot")
        (output / (args.name + "-" + suffix + ".png")).write_bytes(data)
        return struct.unpack(">II", data[16:24])

    if run("get-state").strip() != b"device":
        raise RuntimeError("Device is not authorized")
    width, height = screenshot("before")
    if not (0 < args.x < width and 0 < args.y < height):
        parser.error("Coordinates lie outside the current screen")
    before = run("shell", "cat", log_path)
    press = subprocess.Popen(adb + ["shell", "input", "swipe", str(args.x), str(args.y),
                                   str(args.x), str(args.y), "5000"])
    try:
        time.sleep(2.5)
        screenshot("holding")
    finally:
        press.wait(timeout=10)
    if press.returncode:
        raise RuntimeError("Long press failed")
    time.sleep(0.5)
    screenshot("released")
    after = run("shell", "cat", log_path)
    (output / (args.name + ".log")).write_bytes(after)
    stable_log = after.startswith(before)
    added = after[len(before):].decode("utf-8", errors="replace") if stable_log else ""
    assertions = {
        "same_log_session": stable_log,
        "event_preview_rendered": "event=android.event_preview.rendered" in added,
        "no_network_command": "Command completed:" not in added,
        "no_state_transition": "[AppState] State changed" not in added,
        "no_preview_error": "observe_failed" not in added and "Exception" not in added,
    }
    report = {"serial": args.serial, "coordinates": [args.x, args.y], "resolution": [width, height],
              "assertions": assertions, "visual_review": "required"}
    (output / (args.name + ".json")).write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2))
    if not all(assertions.values()):
        raise SystemExit("Preview verification failed; inspect evidence before any further input")


if __name__ == "__main__":
    main()
