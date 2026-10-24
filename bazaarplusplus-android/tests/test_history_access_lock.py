import pathlib
import shutil
import subprocess
import tempfile
import unittest


class HistoryAccessLockTests(unittest.TestCase):
    def test_process_exclusion_and_death_recovery(self):
        root = pathlib.Path(__file__).resolve().parents[1]
        javac = shutil.which("javac")
        java = shutil.which("java")
        self.assertIsNotNone(javac, "JDK required for history lock verification")
        self.assertIsNotNone(java, "JDK required for history lock verification")
        with tempfile.TemporaryDirectory() as output:
            subprocess.run([
                javac, "--release", "17", "-d", output,
                str(root / "launcher/fusionApp/src/main/java/dev/allofus/fusioncore/history/HistoryAccessLock.java"),
                str(root / "tests/HistoryAccessLockTest.java"),
            ], check=True, timeout=30)
            subprocess.run([java, "-cp", output, "HistoryAccessLockTest"], check=True, timeout=30)
