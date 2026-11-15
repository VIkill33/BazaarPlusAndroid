import shutil
import subprocess
import unittest
from pathlib import Path


class CustomBuildTests(unittest.TestCase):
    def test_draft_and_persistent_collection(self):
        dotnet = shutil.which("dotnet")
        self.assertTrue(dotnet, ".NET SDK required for custom build verification")
        project = Path(__file__).resolve().parent / "CustomBuilds.Tests"
        subprocess.run([dotnet, "run", "--project", str(project)], check=True)
