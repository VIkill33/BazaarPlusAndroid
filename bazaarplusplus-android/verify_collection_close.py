#!/usr/bin/env python3
"""Exercise collection preview close on an already running game; screenshots need review."""

import argparse
import json
from pathlib import Path
import subprocess
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adb", default="adb")
    parser.add_argument("--serial", required=True)
    parser.add_argument("--mode", required=True, choices=("standard", "advanced"),
                        help="Mode already selected in game settings; this script does not change it")
    for name in ("entry", "slot", "preview", "close"):
        parser.add_argument(f"--{name}", required=True, nargs=2, type=int,
                            metavar=("X", "Y"), help="Confirm coordinates from a current device screenshot")
    parser.add_argument("--repeat", type=int, default=3)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    base = [args.adb, "-s", args.serial]
    log_path = "/sdcard/FusionCore/com.tempogames.thebazaar/BepInEx/LogOutput.log"

    def adb(*parts):
        return subprocess.check_output([*base, *map(str, parts)], timeout=20)

    def log():
        return adb("shell", "cat", log_path).decode(errors="replace")

    def tap(point):
        adb("shell", "input", "tap", *point)

    def capture(name):
        (args.output / f"{name}.png").write_bytes(adb("exec-out", "screencap", "-p"))

    def wait_event(start, event):
        deadline = time.monotonic() + 30
        while time.monotonic() < deadline:
            if event in log()[start:]:
                return
            time.sleep(.5)
        raise RuntimeError(f"Timed out waiting for {event}")

    baseline = log()
    results = []
    for index in range(args.repeat):
        start = len(log())
        tap(args.entry)
        wait_event(start, "android.collection.visibility open=True")
        time.sleep(1)
        tap(args.slot)
        wait_event(start, "android.collection.preview_ready")
        capture(f"{index}-selected")
        tap(args.preview)
        adb("shell", "input", "swipe", *args.preview, *args.preview, 900)
        capture(f"{index}-pressed")
        tap(args.close)
        wait_event(start, "android.collection.visibility open=False")
        time.sleep(1)
        capture(f"{index}-closed")
        delta = log()[start:]
        assert "android.tooltip.observed" not in delta, "Native item tooltip was observed during collection inspection"
        assert "_failed" not in delta, "Failure logged during collection inspection"
        results.append({"iteration": index, "preview_ready": True, "closed": True,
                        "no_observed_native_tooltip_or_failure": True})
    current = log()
    assert current.startswith(baseline), "Game log session changed during validation"
    (args.output / "session.log").write_text(current)
    (args.output / "validation.json").write_text(json.dumps(
        {"mode": args.mode, "results": results, "visual_assertions": "manual review required"}, indent=2) + "\n")
    print(f"Passed {args.repeat} preview/close log checks; review screenshots in {args.output}")


if __name__ == "__main__":
    main()
