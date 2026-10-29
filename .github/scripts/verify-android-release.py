#!/usr/bin/env python3
"""Verify the locally signed Android artifacts attached to a GitHub Release."""

import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import tarfile
import zipfile


def verify(directory, tag):
    if not re.fullmatch(r"v\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?", tag):
        raise ValueError("Expected an Android version tag such as v0.1.0-test.13")
    name = f"BazaarPlusAndroid-{tag}"
    # Releases published before the rename retain their original asset names.
    if not (directory / f"{name}.apk").is_file():
        name = f"BazaarPlusPlus-Android-{tag}"
    apk = directory / f"{name}.apk"
    checksum = (directory / f"{name}.apk.sha256").read_text().split()
    if len(checksum) != 2 or checksum[1] != apk.name:
        raise ValueError("Checksum file must name the release APK")
    if hashlib.sha256(apk.read_bytes()).hexdigest() != checksum[0]:
        raise ValueError("APK SHA-256 mismatch")

    manifest = json.loads((directory / "release.json").read_text())
    with zipfile.ZipFile(apk) as archive:
        if json.loads(archive.read("assets/launcher/release.json")) != manifest:
            raise ValueError("Published input manifest differs from APK manifest")

    root = Path(__file__).resolve().parents[2]
    spec = importlib.util.spec_from_file_location(
        "build_launcher", root / "bazaarplusplus-android/build_launcher.py")
    builder = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(builder)
    builder.verify_bundled_inputs(apk, manifest)

    with tarfile.open(directory / f"{name}-source.tar.gz") as archive:
        source = archive.extractfile(
            "bazaarplusplus-android/launcher/fusionApp/build.gradle.kts").read().decode()
    version = re.search(r'versionName\s*=\s*"([^"]+)"', source)
    if version is None or version.group(1) != tag[1:]:
        raise ValueError("Source archive version differs from release tag")
    print(f"Verified {tag}: APK SHA-256, bundled inputs, manifest, and source version")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tag", required=True)
    parser.add_argument("--directory", type=Path, required=True)
    args = parser.parse_args()
    verify(args.directory, args.tag)
