#!/usr/bin/env python3
"""Build the isolated IL2CPP probe against the pinned loader's framework."""

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import zipfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--framework-zip", required=True, type=Path)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--interop", type=Path, help="Also build the Unity/game runtime probe using Android-generated assemblies")
    parser.add_argument("--mod", action="store_true", help="Also build the Android collection plugin (requires --interop)")
    args = parser.parse_args()
    if args.mod and not args.interop:
        parser.error("--mod requires --interop")
    root = Path(__file__).resolve().parent
    toolchain = json.loads((root / "toolchain.json").read_text())
    actual = hashlib.sha256(args.framework_zip.read_bytes()).hexdigest()
    if actual != toolchain["loader"]["frameworkArchiveSha256"]:
        parser.error("Framework archive SHA-256 differs from toolchain.json")
    framework = root / "artifacts/framework"
    framework.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(args.framework_zip) as archive:
        for name in archive.namelist():
            parts = name.split("/")
            if len(parts) == 2 and parts[0] == "core" and parts[1].endswith(".dll"):
                (framework / parts[1]).write_bytes(archive.read(name))
    subprocess.run([args.dotnet, "build", str(root / "probe/BazaarPlusPlus.Android.Probe.csproj"),
                    "-c", "Release", f"-p:FrameworkPath={framework}"], check=True)
    output = root / "artifacts/probe"
    output.mkdir(parents=True, exist_ok=True)
    source = root / "probe/bin/Release/net10.0/BazaarPlusPlus.Android.Probe.dll"
    shutil.copy2(source, output / source.name)
    print(f"Probe: {output / source.name}")
    if args.interop:
        interop = args.interop.resolve()
        subprocess.run([args.dotnet, "build", str(root / "runtime-probe/BazaarPlusPlus.Android.RuntimeProbe.csproj"),
                        "-c", "Release", f"-p:FrameworkPath={framework}", f"-p:InteropPath={interop}"], check=True)
        runtime = root / "runtime-probe/bin/Release/net10.0/BazaarPlusPlus.Android.RuntimeProbe.dll"
        shutil.copy2(runtime, output / runtime.name)
        print(f"Runtime probe: {output / runtime.name}")
        if args.mod:
            if not (interop / "Il2Cppmscorlib.dll").is_file():
                parser.error("Android IL2CPP proxies are required; desktop Managed assemblies are not valid inputs")
            subprocess.run([args.dotnet, "build", str(root / "android-mod/BazaarPlusPlus.Android.csproj"),
                            "-c", "Release", f"-p:FrameworkPath={framework}", f"-p:InteropPath={interop}"], check=True)
            mod = root / "android-mod/bin/Release/net10.0/BazaarPlusPlus.Android.dll"
            mod_output = root / "artifacts/mod"
            mod_output.mkdir(parents=True, exist_ok=True)
            shutil.copy2(mod, mod_output / mod.name)
            manifest = {"plugin": mod.name, "sha256": hashlib.sha256(mod.read_bytes()).hexdigest(),
                        "frameworkArchiveSha256": actual,
                        "interop": {path.name: hashlib.sha256(path.read_bytes()).hexdigest()
                                    for path in sorted(interop.glob("*.dll"))}}
            (mod_output / "build-inputs.json").write_text(json.dumps(manifest, indent=2) + "\n")
            print(f"Android plugin: {mod_output / mod.name}")


if __name__ == "__main__":
    main()
