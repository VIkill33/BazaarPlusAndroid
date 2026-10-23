#!/usr/bin/env python3
"""Build production ELF padding code and test RW/R/RX tails on an authorized arm64 device."""
import argparse
from pathlib import Path
import re
import struct
import subprocess


def run(args):
    subprocess.run([str(arg) for arg in args], check=True)


def add_tail(data, flags):
    """Relocate the program headers into a final segment, matching repackaged client layouts."""
    data = bytearray(data)
    phoff = struct.unpack_from("<Q", data, 32)[0]
    entsize, count = struct.unpack_from("<HH", data, 54)
    assert data[:5] == b"\x7fELF\x02" and entsize == 56
    headers = [list(struct.unpack_from("<IIQQQQQQ", data, phoff + i * entsize)) for i in range(count)]
    align = lambda n: (n + 16383) & ~16383
    offset = align(len(data))
    address = align(max(h[3] + h[6] for h in headers if h[0] == 1))
    size = (count + 1) * entsize
    for h in headers:
        if h[0] == 6:  # PT_PHDR must describe the relocated table.
            h[2:7] = [offset, address, address, size, size]
    headers.append([1, flags, offset, address, address, size, size, 16384])
    data.extend(bytes(offset - len(data)))
    data.extend(b"".join(struct.pack("<IIQQQQQQ", *h) for h in headers))
    struct.pack_into("<Q", data, 32, offset)
    struct.pack_into("<H", data, 56, count + 1)
    return data


def main():
    root = Path(__file__).resolve().parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ndk", type=Path, required=True)
    parser.add_argument("--serial", required=True)
    parser.add_argument("--adb", default="adb")
    parser.add_argument("--output", type=Path, default=root / "artifacts/native-padding")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    jni = root / "launcher/fusionApp/src/main/jni/fusion"
    sdk = re.search(r"minSdk = (\d+)", (root / "launcher/fusionApp/build.gradle.kts").read_text()).group(1)
    compilers = list(args.ndk.glob(f"toolchains/llvm/prebuilt/*/bin/aarch64-linux-android{sdk}-clang++"))
    if len(compilers) != 1:
        parser.error("Expected exactly one NDK host compiler")
    compiler = compilers[0]
    test = args.output / "padding-test"
    fixture = args.output / "fixture.so"
    run([compiler, "-std=c++20", "-static-libstdc++", "-I", jni / "include",
         root / "tests/native/LauncherPaddingTest.cpp", jni / "src/utilities/library.cpp",
         jni / "src/hooking/allocator.cpp", "-ldl", "-o", test])
    run([compiler.with_name(compiler.name[:-2]), "-shared", "-fPIC",
         root / "tests/native/padding_fixture.c", "-o", fixture])
    adb = [args.adb, "-s", args.serial]
    device = "/data/local/tmp/bpp-launcher-padding"
    run(adb + ["shell", "mkdir", "-p", device])
    run(adb + ["push", test, device + "/padding-test"])
    run(adb + ["shell", "chmod", "755", device + "/padding-test"])
    for name, flags in [("rw", 6), ("r", 4), ("rx", 5)]:
        variant = args.output / (name + ".so")
        variant.write_bytes(add_tail(fixture.read_bytes(), flags))
        run(adb + ["push", variant, device + "/" + name + ".so"])
        run(adb + ["shell", device + "/padding-test", device + "/" + name + ".so",
                   device + "/padded-" + name + ".so"])
    print("All RW/R/RX tail cases passed on device")


if __name__ == "__main__":
    main()
