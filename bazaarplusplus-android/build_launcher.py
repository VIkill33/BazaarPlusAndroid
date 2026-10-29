#!/usr/bin/env python3
"""Prepare verified launcher inputs and build a separately signed APK (no device deployment)."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tarfile
import zipfile
import urllib.error
import urllib.request


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def valid_dependency(path, descriptor):
    return (path.is_file() and path.stat().st_size == descriptor["size"]
            and digest(path) == descriptor["sha256"])


def bundle_dependencies(manifest, assets, cache):
    """Fetch pinned inputs at build time; a verified cache permits offline builds."""
    cache.mkdir(parents=True, exist_ok=True)
    for dependency in manifest["dependencies"]:
        cached = cache / dependency["sha256"]
        if not valid_dependency(cached, dependency):
            last_error = None
            for url in [dependency["url"], *dependency.get("fallbackUrls", [])]:
                temp = cached.with_suffix(".part")
                try:
                    if not url.startswith("https://"):
                        raise ValueError("Dependency source must use HTTPS")
                    request = urllib.request.Request(url, headers={
                        "Accept": "application/octet-stream", "User-Agent": "BazaarPlusAndroid-build"})
                    with urllib.request.urlopen(request, timeout=60) as response, temp.open("wb") as output:
                        if response.status != 200 or not response.geturl().startswith("https://"):
                            raise ValueError("Dependency response must be HTTPS 200")
                        done = 0
                        while chunk := response.read(65536):
                            done += len(chunk)
                            if done > dependency["size"]:
                                raise ValueError("Dependency exceeds pinned size")
                            output.write(chunk)
                    if not valid_dependency(temp, dependency):
                        raise ValueError("Dependency size or SHA-256 mismatch")
                    temp.replace(cached)
                    break
                except (OSError, ValueError, urllib.error.URLError) as error:
                    last_error = error
                finally:
                    temp.unlink(missing_ok=True)
            else:
                raise ValueError(f"Cannot bundle dependency {dependency['name']}: {last_error}") from last_error
        asset = "launcher/dependencies/" + dependency["name"]
        target = assets / asset
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(cached, target)
        dependency["asset"] = asset


def prepare(root, upstream, plugin_dir, dependency_dir=None):
    launcher = root / "launcher"
    lock = json.loads((launcher / "upstream.json").read_text())
    commit = subprocess.check_output(["git", "-C", str(upstream), "rev-parse", "HEAD"], text=True).strip()
    if commit != lock["commit"]:
        raise ValueError("Upstream checkout does not match upstream.json")
    for relative, expected in lock["binaryInputs"].items():
        source = upstream / relative
        if digest(source) != expected:
            raise ValueError(f"Upstream binary checksum mismatch: {relative}")
        target = launcher / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
    plugin = plugin_dir / "BazaarPlusPlus.Android.dll"
    inputs = json.loads((plugin_dir / "build-inputs.json").read_text())
    toolchain = json.loads((root / "toolchain.json").read_text())
    if digest(plugin) != inputs["sha256"]:
        raise ValueError("Plugin does not match its build-inputs.json")
    if inputs["frameworkArchiveSha256"] != toolchain["loader"]["frameworkArchiveSha256"]:
        raise ValueError("Plugin framework differs from pinned toolchain")
    if not inputs.get("sourceInputs"):
        raise ValueError("Rebuild the plugin to record source provenance")
    for relative, expected in inputs["sourceInputs"].items():
        if digest(root.parent / relative) != expected:
            raise ValueError(f"Plugin source changed after build: {relative}")
    manifest = json.loads((launcher / "compatibility.json").read_text())
    if not re.fullmatch(r"[A-Za-z0-9_-]+", manifest["storageRoot"]) or manifest["storageRoot"] == "FusionCore":
        raise ValueError("Launcher storage must use its own external root")
    manifest["sourceCommit"] = subprocess.check_output(["git", "-C", str(root), "rev-parse", "HEAD"], text=True).strip()
    manifest["sourceDirty"] = bool(subprocess.check_output(["git", "-C", str(root), "status", "--porcelain"], text=True).strip())
    manifest["upstreamCommit"] = lock["commit"]
    manifest["pluginBuildCommit"] = inputs["sourceCommit"]
    gradle = (launcher / "fusionApp/build.gradle.kts").read_text()
    manifest["launcherVersion"] = re.search(r'versionName = "([^"]+)"', gradle).group(1)
    manifest["launcherVersionCode"] = int(re.search(r'versionCode = (\d+)', gradle).group(1))
    manifest["applicationId"] = re.search(r'applicationId = "([^"]+)"', gradle).group(1)
    # A dirty source build still has an exact content identity; no claim it is committed.
    sources = sorted(p for p in launcher.rglob("*") if p.is_file() and
                     not any(part in {"build", ".gradle", ".kotlin", ".cxx", "jniLibs"} for part in p.relative_to(launcher).parts)
                     and p.suffix not in {".zip", ".dll", ".jks", ".keystore"}
                     and "assets/launcher/" not in p.as_posix() and p.name != "local.properties")
    tree_hash = hashlib.sha256()
    for path in sources:
        tree_hash.update(path.relative_to(launcher).as_posix().encode())
        tree_hash.update(path.read_bytes())
    manifest["launcherSourceSha256"] = tree_hash.hexdigest()
    manifest["pluginBuildInputsSha256"] = digest(plugin_dir / "build-inputs.json")
    manifest["pluginInteropAggregateSha256"] = hashlib.sha256(
        json.dumps(inputs["interop"], sort_keys=True).encode()).hexdigest()
    asset_dir = launcher / "fusionApp/src/main/assets/launcher"
    asset_dir.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(plugin, asset_dir / plugin.name)
    version = inputs["pluginVersion"]

    def descriptor(path, asset):
        return {"asset": asset, "size": path.stat().st_size, "sha256": digest(path)}

    assets = launcher / "fusionApp/src/main/assets"
    bundle_dependencies(manifest, assets, dependency_dir or root / "artifacts/launcher-dependencies")
    manifest["plugin"] = {**descriptor(plugin, "launcher/" + plugin.name), "version": version}
    for key, name in [("framework", "BepInEx-arm64.zip"), ("runtime", "dotnet-arm64.zip")]:
        manifest[key] = descriptor(assets / name, name)
        manifest[key]["source"] = toolchain["loader"]["repository"] + "/tree/" + lock["commit"]
    manifest["framework"]["version"] = "FusionCore-" + toolchain["loader"]["tag"] + "-bundled"
    with zipfile.ZipFile(assets / "dotnet-arm64.zip") as archive:
        libraries = json.loads(archive.read("Microsoft.NETCore.App.deps.json"))["libraries"]
    manifest["runtime"]["version"] = next(iter(libraries)).split("/")[-1]
    manifest["releaseId"] = hashlib.sha256(json.dumps(manifest, sort_keys=True).encode()).hexdigest()
    (asset_dir / "release.json").write_text(json.dumps(manifest, indent=2) + "\n")
    (asset_dir / "NOTICE.txt").write_text(
        "BazaarPlusAndroid launcher\nBazaarPlusPlus: MIT (see BPP-LICENSE.txt).\n"
        "FusionCore: GPL-3.0, copyright its contributors (see FusionCore-LICENSE.txt).\n"
        "In-game dock icons made by Lorc (\"Anvil impact\", \"Tied scroll\", \"Magnifying glass\") "
        "and Skoll (\"Sight disabled\"), from https://game-icons.net, "
        "CC BY 3.0 (https://creativecommons.org/licenses/by/3.0/); "
        "recoloured and outlined, see bazaarplusplus-android/assets/dock-icons/SOURCE.md.\n"
        "Launcher home icons: Material Design Icons, copyright its contributors, Apache-2.0 "
        "(see MDI-LICENSE.txt); paths converted to Android VectorDrawable and tinted.\n"
        "Corresponding launcher source and build inputs: bazaarplusplus-android/launcher and build_launcher.py "
        "in https://github.com/VIkill33/BazaarPlusAndroid (supply the exact source archive with any test distribution).\n"
        "BepInEx/.NET/Dobby and bundled components retain their own licenses. "
        "Third-party redistribution review remains required before public release.\n"
        "This is a local test build, not a publicly approved distribution.\n")
    shutil.copyfile(root / "assets/launcher-icons/LICENSE", asset_dir / "MDI-LICENSE.txt")
    shutil.copyfile(launcher / "LICENSE", asset_dir / "FusionCore-LICENSE.txt")
    shutil.copyfile(root.parent / "LICENSE", asset_dir / "BPP-LICENSE.txt")
    print(f"Prepared launcher release {manifest['releaseId']}")
    return manifest


def verify_bundled_inputs(apk, manifest):
    with zipfile.ZipFile(apk) as archive:
        for descriptor in [manifest["framework"], manifest["runtime"], manifest["plugin"],
                           *manifest["dependencies"]]:
            payload = archive.read("assets/" + descriptor["asset"])
            if len(payload) != descriptor["size"] or hashlib.sha256(payload).hexdigest() != descriptor["sha256"]:
                raise ValueError(f"APK bundled input mismatch: {descriptor['asset']}")


def main():
    root = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--upstream", type=Path, default=root / "artifacts/tooling/FusionCore")
    parser.add_argument("--plugin-dir", type=Path, default=root / "artifacts/mod")
    parser.add_argument("--prepare-only", action="store_true")
    parser.add_argument("--dependency-dir", type=Path, help="Verified dependency cache (files named by SHA-256); missing inputs are downloaded during build")
    parser.add_argument("--keystore", type=Path, help="Existing release key; passwords and alias use KEYSTORE_PASSWORD/KEY_PASSWORD/KEY_ALIAS")
    args = parser.parse_args()
    manifest = prepare(root, args.upstream.resolve(), args.plugin_dir.resolve(), args.dependency_dir)
    if args.prepare_only:
        return
    if not args.keystore or not args.keystore.is_file():
        parser.error("A persistent signing key is required via --keystore; do not use the Android debug key")
    for name in ("KEYSTORE_PASSWORD", "KEY_PASSWORD", "KEY_ALIAS"):
        if not os.environ.get(name):
            parser.error(f"Missing signing environment variable {name}")
    env = {**os.environ, "KEYSTORE_PATH": str(args.keystore.resolve())}
    subprocess.run(["./gradlew", ":fusionApp:assembleRelease", "--console=plain"], cwd=root / "launcher", env=env, check=True)
    apk = root / "launcher/fusionApp/build/outputs/apk/release/fusionApp-release.apk"
    verify_bundled_inputs(apk, manifest)
    output = root / "artifacts/launcher"
    output.mkdir(parents=True, exist_ok=True)
    target = output / "BazaarPlusAndroid-launcher.apk"
    shutil.copyfile(apk, target)
    (output / "BazaarPlusAndroid-launcher.apk.sha256").write_text(f"{digest(target)}  {target.name}\n")
    (output / "release.json").write_text(json.dumps(manifest, indent=2) + "\n")
    sdk = Path(env["ANDROID_HOME"])
    signers = sorted((sdk / "build-tools").glob("*/apksigner"))
    if not signers:
        raise ValueError("Android SDK apksigner is required to verify the signed output")
    verification = subprocess.check_output([str(signers[-1]), "verify", "--verbose", "--print-certs", str(target)], env=env, text=True)
    (output / "signing-report.txt").write_text(verification)
    # Exact local sources, including uncommitted implementation; excludes secrets and generated binaries.
    source_paths = subprocess.check_output(["git", "-C", str(root.parent), "ls-files", "--cached", "--others", "--exclude-standard",
                                           "bazaarplusplus-android/launcher"], text=True).splitlines()
    inputs = json.loads((args.plugin_dir / "build-inputs.json").read_text())
    source_paths += list(inputs["sourceInputs"])
    source_paths += ["LICENSE", "bazaarplusplus-android/build_launcher.py", "bazaarplusplus-android/build_probe.py",
                     "bazaarplusplus-android/assets/launcher-icons/LICENSE",
                     "bazaarplusplus-android/assets/launcher-icons/SOURCE.md",
                     "bazaarplusplus-android/toolchain.json", "bazaarplusplus-android/android-mod/BazaarPlusPlus.Android.csproj",
                     "bazaarplusplus-android/README.md", "bazaarplusplus-android/data/README.md",
                     "bazaarplusplus-android/docs/adr/0001-dedicated-launcher.md",
                     "bazaarplusplus-android/verify_launcher.py", "bazaarplusplus-android/inspect_game.py",
                     "bazaarplusplus-android/tests/test_launcher_dependencies.py",
                     "bazaarplusplus-android/tests/LauncherFilesTest.java", "bazaarplusplus-android/tests/LauncherLogTest.java",
                     "bazaarplusplus-android/tests/test_launcher_files.py",
                     "bazaarplusplus-android/tests/LauncherContextTest.java",
                     "bazaarplusplus-android/tests/test_launcher_context.py",
                     "bazaarplusplus-android/tests/LauncherUnityLibrariesTest.java",
                     "bazaarplusplus-android/tests/test_launcher_unity_libraries.py",
                     "bazaarplusplus-android/tests/UnityLibraries.Tests/Program.cs",
                     "bazaarplusplus-android/tests/UnityLibraries.Tests/UnityLibraries.Tests.csproj"]
    with tarfile.open(output / "launcher-source.tar.gz", "w:gz") as archive:
        for relative in sorted(set(source_paths)):
            path = root.parent / relative
            if path.is_file():
                archive.add(path, arcname=relative, recursive=False)
    print(target)


if __name__ == "__main__":
    main()
