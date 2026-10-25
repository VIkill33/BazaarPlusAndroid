import pathlib
import shutil
import subprocess
import tempfile
import unittest


class LauncherUnityLibrariesTests(unittest.TestCase):
    def test_pinned_local_source_and_corrupt_legacy_cache(self):
        root = pathlib.Path(__file__).resolve().parents[1]
        javac, java = shutil.which("javac"), shutil.which("java")
        self.assertIsNotNone(javac, "JDK required for launcher Unity library verification")
        self.assertIsNotNone(java, "JDK required for launcher Unity library verification")
        with tempfile.TemporaryDirectory() as output:
            source = root / "launcher/fusionApp/src/main/java/dev/allofus/fusioncore"
            subprocess.run([javac, "--release", "8", "-d", output,
                            str(source / "LauncherFiles.java"),
                            str(source / "LauncherUnityLibraries.java"),
                            str(root / "tests/LauncherUnityLibrariesTest.java")], check=True)
            subprocess.run([java, "-cp", output, "LauncherUnityLibrariesTest"], check=True)
