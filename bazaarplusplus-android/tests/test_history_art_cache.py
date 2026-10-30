import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


class HistoryArtCacheTests(unittest.TestCase):
    def test_retention_and_cleanup(self):
        root = Path(__file__).resolve().parents[1]
        javac, java = (shutil.which(name) for name in ("javac", "java"))
        self.assertTrue(javac and java, "JDK required for history artwork cache verification")
        source = root / "launcher/fusionApp/src/main/java/dev/allofus/fusioncore"
        with tempfile.TemporaryDirectory() as output:
            subprocess.run([javac, "--release", "8", "-d", output,
                            str(source / "LauncherFiles.java"),
                            str(source / "history/HistoryArtCache.java"),
                            str(root / "tests/HistoryArtCacheTest.java")], check=True)
            subprocess.run([java, "-cp", output, "dev.allofus.fusioncore.history.HistoryArtCacheTest", output], check=True)
