import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


class HistoryBattleLayoutTests(unittest.TestCase):
    def test_cards_fit_portrait_landscape_and_dense_groups(self):
        root = Path(__file__).resolve().parents[1]
        javac, java = shutil.which('javac'), shutil.which('java')
        self.assertIsNotNone(javac)
        self.assertIsNotNone(java)
        with tempfile.TemporaryDirectory() as output:
            subprocess.run([javac, '--release', '8', '-d', output,
                str(root / 'launcher/fusionApp/src/main/java/dev/allofus/fusioncore/history/HistoryBattleLayout.java'),
                str(root / 'tests/HistoryBattleLayoutTest.java')], check=True, capture_output=True, timeout=30)
            result = subprocess.run([java, '-cp', output, 'HistoryBattleLayoutTest'],
                check=True, capture_output=True, text=True, timeout=30)
            self.assertIn('PASS 3060 viewport/card-density layouts', result.stdout)
