#!/usr/bin/env python3
"""Read-only launcher evidence checks. Never installs, launches, logs in or changes game data."""

import argparse
import hashlib
import json
import re
from pathlib import Path
import subprocess
import zipfile

from inspect_game import find_adb, run, select_device


def main():
    root = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adb")
    parser.add_argument("--serial")
    parser.add_argument("--apk", type=Path, default=root / "artifacts/launcher/BazaarPlusPlus-launcher.apk")
    parser.add_argument("--output", type=Path, default=root / "artifacts/launcher/verification")
    parser.add_argument("--expect-loaded", action="store_true")
    parser.add_argument("--not-before-ms", type=int, help="Record epoch milliseconds before initiating the tested launch")
    args = parser.parse_args()
    if args.expect_loaded and args.not_before_ms is None:
        parser.error("--expect-loaded requires --not-before-ms to reject a previous launch")
    adb = find_adb(args.adb)
    serial = select_device(run([adb, "devices", "-l"]), args.serial)
    command = [adb, "-s", serial]
    with zipfile.ZipFile(args.apk) as archive:
        release = json.loads(archive.read("assets/launcher/release.json"))
        bundled = archive.read("assets/launcher/BazaarPlusPlus.Android.dll")
    if not re.fullmatch(r"[A-Za-z0-9_-]+", release["storageRoot"]) or not re.fullmatch(r"[A-Za-z0-9_.]+", release["game"]["package"]):
        parser.error("APK manifest contains invalid device path components")
    target = f"/sdcard/{release['storageRoot']}/{release['game']['package']}/BepInEx"
    checks = {"bundledPluginMatchesManifest": hashlib.sha256(bundled).hexdigest() == release["plugin"]["sha256"]}
    package = run(command + ["shell", "dumpsys", "package", release["applicationId"]])
    checks["installedLauncherVersionMatches"] = bool(re.search(r"versionName=" + re.escape(release["launcherVersion"]) + r"\s*$", package, re.MULTILINE))
    evidence = {"releaseId": release["releaseId"], "launcherVersion": release["launcherVersion"],
                "android": run(command + ["shell", "getprop", "ro.build.version.release"]),
                "deviceModel": run(command + ["shell", "getprop", "ro.product.model"]), "checks": checks}
    if args.expect_loaded:
        request = json.loads(run(command + ["shell", "cat", target + "/bpp-launch-session.json"]))
        loaded = json.loads(run(command + ["shell", "cat", target + "/bpp-launch-loaded.json"]))
        checksum = run(command + ["shell", "sha256sum", target + "/plugins/BazaarPlusPlus.Android.dll"]).split()[0]
        log = run(command + ["shell", "cat", target + "/LogOutput.log"])
        checks.update({
            "newSession": request["startedAt"] >= args.not_before_ms,
            "sessionMatches": request["session"] == loaded["session"],
            "pluginVersionMatches": request["pluginVersion"] == loaded["pluginVersion"] == release["plugin"]["version"],
            "allPluginDigestsMatch": request["sha256"] == loaded["sha256"] == checksum == release["plugin"]["sha256"],
            "ackAfterStart": loaded["loadedAt"] >= request["startedAt"],
            "loaded": loaded["loaded"] is True,
            "currentSessionEvent": "event=android.launcher.loaded session=" + request["session"] in log,
            "pluginEntryEvent": "event=android.mod.loaded" in log,
            "generatedGameProxyPresent": run(command + ["shell", "test", "-f", target + "/interop/TheBazaarRuntime.dll", "&&", "echo", "present"]) == "present",
        })
        evidence.update({"session": request["session"], "startedAt": request["startedAt"], "loadedAt": loaded["loadedAt"],
                         "pluginSha256": checksum})
        events = [line for line in log.splitlines() if any(event in line for event in (
            "event=android.mod.loaded", "event=android.launcher.loaded", "event=android.collection.ui_ready"))]
        args.output.mkdir(parents=True, exist_ok=True)
        (args.output / "launcher-events.log").write_text("\n".join(events) + "\n")
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / "verification.json").write_text(json.dumps(evidence, indent=2, ensure_ascii=False) + "\n")
    with (args.output / "screen.png").open("wb") as out:
        subprocess.run(command + ["exec-out", "screencap", "-p"], stdout=out, check=True)
    print(json.dumps(checks, indent=2))
    if not all(checks.values()):
        raise SystemExit("Launcher evidence checks failed")


if __name__ == "__main__":
    main()
