import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


class HistoryArtworkTests(unittest.TestCase):
    def test_exporter_reader_contract(self):
        root = Path(__file__).resolve().parents[1]
        dotnet, javac, java = (shutil.which(name) for name in ("dotnet", "javac", "java"))
        self.assertTrue(dotnet and javac and java, ".NET SDK and JDK required for artwork contract verification")
        with tempfile.TemporaryDirectory() as output:
            fixture = Path(output) / "game"
            subprocess.run([dotnet, "run", "--project",
                            str(root / "tests/HistoryArtworkDirectory.Tests"), "--", str(fixture)], check=True)
            subprocess.run([javac, "--release", "8", "-d", output,
                            str(root / "launcher/fusionApp/src/main/java/dev/allofus/fusioncore/history/HistoryArtworkFiles.java"),
                            str(root / "tests/HistoryArtworkFilesTest.java")], check=True)
            subprocess.run([java, "-cp", output,
                            "dev.allofus.fusioncore.history.HistoryArtworkFilesTest", str(fixture)], check=True)
