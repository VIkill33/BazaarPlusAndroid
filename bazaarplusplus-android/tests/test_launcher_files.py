import pathlib
import shutil
import subprocess
import tempfile
import unittest


class LauncherFilesTests(unittest.TestCase):
    def test_file_transactions_and_untrusted_archive(self):
        root = pathlib.Path(__file__).resolve().parents[1]
        javac = shutil.which("javac")
        java = shutil.which("java")
        self.assertIsNotNone(javac, "JDK required for launcher file verification")
        with tempfile.TemporaryDirectory() as output:
            subprocess.run([javac, "--release", "17", "-d", output,
                            str(root / "launcher/fusionApp/src/main/java/dev/allofus/fusioncore/LauncherFiles.java"),
                            str(root / "launcher/fusionApp/src/main/java/dev/allofus/fusioncore/LauncherLog.java"),
                            str(root / "launcher/fusionApp/src/main/java/dev/allofus/fusioncore/LauncherSession.java"),
                            str(root / "tests/LauncherFilesTest.java"),
                            str(root / "tests/LauncherLogTest.java")], check=True)
            subprocess.run([java, "-cp", output, "LauncherFilesTest"], check=True)
            subprocess.run([java, "-cp", output, "LauncherLogTest"], check=True)
