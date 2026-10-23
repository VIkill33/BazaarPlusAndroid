#!/usr/bin/env python3
"""Exercise an enabled, collapsed BPP dock on a connected device (requires Pillow).

Start on the game lobby or an existing run, close settings and enable auto-hide first.
Only operates the plugin dock; does not start runs or interact with game choices.
Screenshot assertions identify the dock's flat button fill and rectangle sizes;
they do not assert text, card content or arbitrary game UI semantics.
"""

import argparse
from io import BytesIO
import json
from pathlib import Path
import subprocess
import time

from PIL import Image

from inspect_game import find_adb, run, select_device


def rectangles(image):
    image = image.convert("RGB")
    width, height = image.size
    pixels = {i for i, color in enumerate(image.get_flattened_data() if hasattr(image, "get_flattened_data") else image.getdata()) if color in ((38, 51, 61), (37, 49, 59))}
    found = []
    while pixels:
        seed = pixels.pop()
        stack = [seed]
        xs, ys = [], []
        while stack:
            index = stack.pop()
            x, y = index % width, index // width
            xs.append(x)
            ys.append(y)
            for neighbour in (index - 1 if x else -1, index + 1 if x + 1 < width else -1,
                              index - width, index + width):
                if neighbour in pixels:
                    pixels.remove(neighbour)
                    stack.append(neighbour)
        box = (min(xs), min(ys), max(xs) + 1, max(ys) + 1)
        scale = height / 1080
        w, h = (box[2] - box[0]) / scale, (box[3] - box[1]) / scale
        if any(abs(w - ew) < 3 and abs(h - eh) < 3 for ew, eh in ((60, 132), (270, 90), (270, 70))):
            found.append(box)
    return sorted(found, key=lambda box: box[1])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adb")
    parser.add_argument("--serial")
    parser.add_argument("--safe-left", type=int, default=0, help="Current left safe-area inset in pixels")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    adb = find_adb(args.adb)
    serial = select_device(run([adb, "devices", "-l"]), args.serial)
    command = [adb, "-s", serial]
    args.output.mkdir(parents=True, exist_ok=True)
    checks = {}

    def call(*parts):
        return subprocess.check_output(command + list(parts), timeout=20)

    def capture(name, count):
        path = args.output / (name + ".png")
        # Android CLI is the preferred capture path; ADB supports installations without it.
        try:
            subprocess.run(["android", "screen", "capture", "--device", serial, "--output", str(path)],
                           check=True, capture_output=True, timeout=20)
            image = Image.open(path)
        except (FileNotFoundError, subprocess.CalledProcessError):
            data = call("exec-out", "screencap", "-p")
            path.write_bytes(data)
            image = Image.open(BytesIO(data))
        boxes = rectangles(image)
        checks[name] = {"rectangles": boxes, "expectedCount": count, "passed": len(boxes) == count}
        (args.output / "checks.json").write_text(json.dumps(checks, indent=2) + "\n")
        assert len(boxes) == count, (name, boxes)
        return image.size, boxes

    def tap(box):
        call("shell", "input", "tap", str((box[0] + box[2]) // 2), str((box[1] + box[3]) // 2))

    log_path = "/sdcard/BazaarPlusPlusAndroid/com.tempogames.thebazaar/BepInEx/LogOutput.log"
    before = call("shell", "cat", log_path)
    (width, height), boxes = capture("initial-collapsed", 1)
    # Drag the collapsed handle to each edge, asserting a single handle survives.
    targets = (("left", args.safe_left + 70, height // 2), ("top", width // 2, 40),
               ("right", width - 60, height // 2), ("bottom", width // 2, height - 50))
    for edge, x, y in targets:
        box = boxes[0]
        call("shell", "input", "swipe", str((box[0] + box[2]) // 2), str((box[1] + box[3]) // 2),
             str(x), str(y), "650")
        _, boxes = capture("collapsed-" + edge, 1)
        box = boxes[0]
        distance = {"left": abs(box[0] - args.safe_left), "top": box[1],
                    "right": width - box[2], "bottom": height - box[3]}[edge]
        checks["collapsed-" + edge]["edgeDistance"] = distance
        checks["collapsed-" + edge]["passed"] = distance <= 3
        (args.output / "checks.json").write_text(json.dumps(checks, indent=2) + "\n")
        assert distance <= 3, (edge, box)
    tap(boxes[0])
    _, boxes = capture("tap-expanded", 4)
    # Drag each action button independently; none should execute its click action.
    for slot in range(3):
        box = boxes[slot]
        call("shell", "input", "swipe", str((box[0] + box[2]) // 2), str((box[1] + box[3]) // 2),
             str(width - 120), str(height // 2), "650")
        _, boxes = capture("drag-button-" + str(slot), 4)
        assert all(abs(box[2] - width) <= 3 for box in boxes)
    tap(boxes[-1])
    _, boxes = capture("manual-collapse", 1)
    tap(boxes[0])
    capture("reexpanded", 4)
    time.sleep(5.5)
    capture("idle-collapse", 1)
    after = call("shell", "cat", log_path)
    assert after.startswith(before), "Game restarted during validation"
    delta = after[len(before):].decode(errors="replace")
    (args.output / "events.log").write_text(delta)
    assert delta.count("event=android.dock.snapped") == 7, delta
    assert not any(word in delta for word in ("Exception", "_failed", "Command completed:", "[AppState] State changed", "event=android.tooltip.default_mode")), delta
    checks["logs"] = {"snaps": 7, "noExceptionOrGameCommandOrTransition": True}
    (args.output / "checks.json").write_text(json.dumps(checks, indent=2) + "\n")
    print("Dock: four edges, three button drags, expand, manual/idle collapse and log assertions passed.")


if __name__ == "__main__":
    main()
