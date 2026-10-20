#!/usr/bin/env python3
"""Capture installed APKs and identify Unity runtime evidence, without modifying a device."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import zipfile


class InspectionError(Exception):
    pass


def find_adb(explicit=None):
    candidates = [explicit, shutil.which("adb")]
    for root in (os.environ.get("ANDROID_SDK_ROOT"), os.environ.get("ANDROID_HOME"),
                 str(Path.home() / "Library/Android/sdk"), str(Path.home() / "Android/Sdk")):
        if root:
            candidates.append(str(Path(root) / "platform-tools/adb"))
    for candidate in candidates:
        if candidate and Path(candidate).is_file():
            return str(Path(candidate).resolve())
    raise InspectionError("找不到 adb；请通过 --adb 指定 Android SDK platform-tools/adb。")


def run(command, timeout=30):
    result = subprocess.run(command, capture_output=True, text=True, timeout=timeout)
    if result.returncode:
        raise InspectionError(result.stderr.strip() or result.stdout.strip() or str(command))
    return result.stdout.strip()


def select_device(output, serial=None):
    devices = {}
    for line in output.splitlines():
        parts = line.split()
        if len(parts) >= 2 and parts[1] in ("device", "offline", "unauthorized", "recovery", "sideload"):
            devices[parts[0]] = parts[1]
    if serial:
        if devices.get(serial) != "device":
            raise InspectionError(f"设备 {serial} 尚不可用：{devices.get(serial, '未连接')}。")
        return serial
    if len(devices) == 1:
        return select_device(output, next(iter(devices)))
    if not devices:
        raise InspectionError("未发现 ADB 设备。请开启 USB 调试、解锁手机并接受 RSA 授权。")
    raise InspectionError("存在多个设备，请用 --serial 选择：" + ", ".join(devices))


def select_package(output, requested=None):
    packages = [line.removeprefix("package:") for line in output.splitlines()
                if line.startswith("package:")]
    if requested:
        if requested not in packages:
            raise InspectionError(f"设备上未安装 {requested}。")
        return requested
    matches = [name for name in packages if "bazaar" in name.lower()]
    if len(matches) == 1:
        return matches[0]
    raise InspectionError("无法唯一识别大巴扎包名，请用 --package 指定。候选：" + ", ".join(matches))


def inspect_apks(paths):
    archives = []
    libraries = set()
    managed = set()
    metadata = []
    unity_versions = set()
    for path in paths:
        digest = hashlib.sha256()
        with path.open("rb") as source:
            for chunk in iter(lambda: source.read(1024 * 1024), b""):
                digest.update(chunk)
        with zipfile.ZipFile(path) as archive:
            for entry in archive.infolist():
                name = entry.filename
                if re.fullmatch(r"lib/[^/]+/[^/]+\.so", name):
                    libraries.add(name)
                    if name.endswith("/libunity.so"):
                        unity_versions.update(match.decode("ascii") for match in re.findall(
                            rb"(?:6000|20\d{2})\.\d+\.\d+[abfp]\d+", archive.read(entry)))
                if name.lower().endswith(".dll") and "/managed/" in name.lower():
                    managed.add(name)
                if name.endswith("/global-metadata.dat"):
                    with archive.open(entry) as source:
                        header = source.read(8)
                    magic, version = struct.unpack("<II", header) if len(header) == 8 else (None, None)
                    metadata.append({"apk": path.name, "entry": name, "size": entry.file_size,
                                     "version": version if magic == 0xFAB11BAF else None})
        archives.append({"file": str(path.resolve()), "size": path.stat().st_size,
                         "sha256": digest.hexdigest()})
    il2cpp = any(name.endswith("/libil2cpp.so") for name in libraries)
    mono = any(name.rsplit("/", 1)[-1] in ("libmono.so", "libmonobdwgc-2.0.so", "libmonosgen-2.0.so")
               for name in libraries)
    if il2cpp and mono:
        runtime = "ambiguous"
    elif il2cpp:
        runtime = "il2cpp"
    elif mono:
        runtime = "mono"
    else:
        runtime = "unknown"
    return {"apks": archives, "runtime": runtime,
            "abis": sorted({name.split("/")[1] for name in libraries}),
            "native_libraries": sorted(libraries), "managed_assemblies": sorted(managed),
            "unity_version_candidates": sorted(unity_versions),
            "il2cpp_metadata": metadata,
            "note": "静态 APK 证据；不代表加载器或模组已在游戏中运行。"}


def capture(args):
    adb = find_adb(args.adb)
    serial = select_device(run([adb, "devices", "-l"]), args.serial)

    def shell(*command):
        return run([adb, "-s", serial, "shell", *command])

    package = select_package(shell("pm", "list", "packages"), args.package)
    if not re.fullmatch(r"[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+", package):
        raise InspectionError("无效包名。")
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    apk_paths = [line[len("package:"):] for line in shell("pm", "path", package).splitlines()
                 if line.startswith("package:")]
    if not apk_paths:
        raise InspectionError("pm path 没有返回 APK；请检查包名和用户配置。")
    local_paths = []
    for index, remote in enumerate(apk_paths):
        if not remote.startswith("/") or not remote.endswith(".apk"):
            raise InspectionError("pm path 返回了异常路径。")
        local = output / f"{index:02d}-{Path(remote).name}"
        print(f"正在导出 {index + 1}/{len(apk_paths)}: {Path(remote).name}", flush=True)
        descriptor, temporary = tempfile.mkstemp(prefix=local.name + ".", suffix=".partial", dir=output)
        os.close(descriptor)
        try:
            run([adb, "-s", serial, "pull", remote, temporary], timeout=args.pull_timeout)
            if not zipfile.is_zipfile(temporary):
                raise InspectionError("导出的文件不是完整 APK，请重新读取。")
            Path(temporary).replace(local)
        finally:
            Path(temporary).unlink(missing_ok=True)
        local_paths.append(local)
    report = inspect_apks(local_paths)
    details = shell("dumpsys", "package", package)
    versions = [line.strip() for line in details.splitlines()
                if re.match(r"\s*(versionCode|versionName|minSdk|targetSdk|primaryCpuAbi|secondaryCpuAbi)=", line)]
    report["device"] = {"serial": serial, "model": shell("getprop", "ro.product.model"),
                        "android": shell("getprop", "ro.build.version.release"),
                        "sdk": shell("getprop", "ro.build.version.sdk"),
                        "abis": shell("getprop", "ro.product.cpu.abilist")}
    report["package"] = {"name": package, "version_fields": versions, "installed_apks": apk_paths}
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adb", help="Path to platform-tools/adb")
    parser.add_argument("--serial", help="Choose one connected ADB device")
    parser.add_argument("--package", help="Installed game package; otherwise match bazaar")
    parser.add_argument("--apk", action="append", type=Path, help="Inspect local APK; repeat for all splits")
    parser.add_argument("--pull-timeout", type=int, default=1800, help="Timeout in seconds for each APK transfer")
    parser.add_argument("--output", default=str(Path(__file__).parent / "artifacts/device"),
                        help="Local capture directory (ignored by Git)")
    args = parser.parse_args()
    try:
        report = inspect_apks(args.apk) if args.apk else capture(args)
        destination = Path(args.output) / "inspection.json"
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"Runtime: {report['runtime']}; APK ABIs: {', '.join(report['abis']) or 'unknown'}")
        print(f"Report: {destination.resolve()}")
        return 0
    except (InspectionError, OSError, subprocess.TimeoutExpired, zipfile.BadZipFile) as error:
        print(f"检查失败：{error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
