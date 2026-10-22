#!/usr/bin/env python3
"""Verify a cold/warm collection load in a running game; review screenshots separately."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--serial", required=True)
    parser.add_argument("--expect", choices=("cold", "warm"), required=True)
    for name in ("entry", "slot", "close"):
        parser.add_argument(f"--{name}", nargs=2, type=int, required=True,
                            help="Coordinates confirmed from a current screenshot")
    parser.add_argument("--already-open", action="store_true", help="Observe this launch from its existing log")
    parser.add_argument("--timeout", type=float, default=180)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    base = ["adb", "-s", args.serial]

    def adb(*parts):
        return subprocess.check_output([*base, *map(str, parts)], timeout=20)

    def log():
        return adb("shell", "cat", "/sdcard/FusionCore/com.tempogames.thebazaar/BepInEx/LogOutput.log").decode(errors="replace")

    baseline = "" if args.already_open else log()

    def delta():
        current = log()
        assert current.startswith(baseline), "Game session changed during validation"
        return current[len(baseline):]

    def wait(event):
        deadline = time.monotonic() + args.timeout
        while time.monotonic() < deadline:
            text = delta()
            assert "android.collection.catalog_failed" not in text, text
            if event in text:
                return text
            time.sleep(.5)
        raise RuntimeError(f"Timed out waiting for {event}")

    def capture(name):
        (args.output / f"{name}.png").write_bytes(adb("exec-out", "screencap", "-p"))

    if not args.already_open:
        adb("shell", "input", "tap", *args.entry)
    event = "android.collection.cache_hit" if args.expect == "warm" else "android.collection.catalog_ready"
    text = wait(event)
    if args.expect == "cold":
        text = wait("android.collection.cache_written")
    else:
        assert "android.collection.catalog_ready" not in text, "Warm load rebuilt the catalog"
    match = re.search(re.escape(event) + r"[^\n]*projected_count=(\d+)[^\n]*elapsed_ms=(\d+)", text)
    assert match and int(match[1]) > 0, "Missing catalog count/timing"
    time.sleep(2)
    capture("loaded")
    adb("shell", "input", "tap", *args.slot)
    wait("android.collection.preview_ready")
    capture("preview")
    adb("shell", "input", "tap", *args.close)
    wait("android.collection.visibility open=False")
    capture("closed")
    current = log()
    (args.output / "session.log").write_text(current)
    relevant = delta()
    assert not re.search(r"android\.collection\.[^\s]*failed", relevant), relevant
    result = {"load": args.expect, "cards": int(match[1]), "elapsed_ms": int(match[2]),
              "preview_ready": True, "closed": True, "visual_assertions": "manual review required"}
    (args.output / "validation.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result))


if __name__ == "__main__":
    main()
