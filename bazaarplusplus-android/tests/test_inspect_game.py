import importlib.util
from pathlib import Path
import tempfile
import struct
import unittest
import zipfile

spec = importlib.util.spec_from_file_location("inspect_game", Path(__file__).parents[1] / "inspect_game.py")
inspection = importlib.util.module_from_spec(spec)
spec.loader.exec_module(inspection)


class InspectionTests(unittest.TestCase):
    def test_split_apks_are_combined_for_runtime_detection(self):
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory) / "base.apk"
            split = Path(directory) / "split.apk"
            with zipfile.ZipFile(base, "w") as archive:
                archive.writestr("assets/bin/Data/Managed/Metadata/global-metadata.dat", struct.pack("<II", 0xFAB11BAF, 39))
            with zipfile.ZipFile(split, "w") as archive:
                archive.writestr("lib/arm64-v8a/libil2cpp.so", b"native")
                archive.writestr("lib/arm64-v8a/libunity.so", b"\x006000.3.11f1\x00")
            report = inspection.inspect_apks([base, split])
            self.assertEqual(report["runtime"], "il2cpp")
            self.assertEqual(report["abis"], ["arm64-v8a"])
            self.assertEqual(len(report["apks"]), 2)
            self.assertEqual(report["il2cpp_metadata"][0]["size"], 8)
            self.assertEqual(report["il2cpp_metadata"][0]["version"], 39)
            self.assertEqual(report["unity_version_candidates"], ["6000.3.11f1"])
            self.assertEqual(len(report["apks"][0]["sha256"]), 64)

    def test_runtime_evidence_is_not_inferred_from_dll_names(self):
        cases = [(["assets/bin/Data/Managed/Assembly-CSharp.dll"], "unknown"),
                 (["lib/armeabi-v7a/libmonobdwgc-2.0.so"], "mono"),
                 (["lib/arm64-v8a/libil2cpp.so", "lib/arm64-v8a/libmono.so"], "ambiguous")]
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "base.apk"
            for names, expected in cases:
                with self.subTest(expected=expected):
                    with zipfile.ZipFile(path, "w") as archive:
                        for name in names:
                            archive.writestr(name, b"fixture")
                    self.assertEqual(inspection.inspect_apks([path])["runtime"], expected)

    def test_unauthorized_and_multiple_devices_require_user_action(self):
        for output in ("List of devices attached\n", "phone unauthorized\n",
                       "phone device\nprojector device\n"):
            with self.subTest(output=output), self.assertRaises(inspection.InspectionError):
                inspection.select_device(output)
        self.assertEqual(inspection.select_device("phone device\nprojector device", "phone"), "phone")

    def test_package_selection_does_not_choose_an_unrelated_device_app(self):
        with self.assertRaises(inspection.InspectionError):
            inspection.select_package("package:com.projector.launcher")
        self.assertEqual(inspection.select_package("package:com.tempo.bazaar"), "com.tempo.bazaar")
        with self.assertRaises(inspection.InspectionError):
            inspection.select_package("package:com.tempo.bazaar", "com.missing.game")


if __name__ == "__main__":
    unittest.main()
