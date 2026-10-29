import base64
import pathlib
import shutil
import sqlite3
import subprocess
import tempfile
import unittest


class PurchaseSqlTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        root = pathlib.Path(__file__).resolve().parents[1]
        with tempfile.TemporaryDirectory() as output:
            subprocess.run([shutil.which('javac'), '--release', '8', '-d', output,
                            str(root / 'launcher/fusionApp/src/main/java/dev/allofus/fusioncore/history/PurchaseSql.java'),
                            str(root / 'tests/PurchaseSqlHarness.java')], check=True, timeout=30)
            lines = subprocess.check_output([shutil.which('java'), '-cp', output, 'PurchaseSqlHarness'], text=True, timeout=30)
        cls.sql = {key: base64.b64decode(value).decode() for key, value in (line.split(':', 1) for line in lines.splitlines())}

    def setUp(self):
        self.db = sqlite3.connect(':memory:')
        self.addCleanup(self.db.close)
        self.db.row_factory = sqlite3.Row
        self.db.executescript('''PRAGMA foreign_keys=ON;
            CREATE TABLE history_scopes(id TEXT PRIMARY KEY,account_key TEXT);
            CREATE TABLE runs(id TEXT PRIMARY KEY,scope_id TEXT REFERENCES history_scopes(id),hero TEXT);
            INSERT INTO history_scopes VALUES('a','account-a'),('b','account-b');
            INSERT INTO runs VALUES('r1','a','Vanessa'),('r2','a','Dooley'),('r3','b','Vanessa');
            PRAGMA user_version=1;''')
        self.db.execute(self.sql['create'])
        self.db.execute(self.sql['index'])

    def purchase(self, id_, run='r1', template='template-1', price=3, at=100):
        self.db.execute(self.sql['insert'], (id_, run, 'instance-' + id_, template, 'Saved name', 'Item', 'Gold', price, at, 2, 3))

    def rows(self, name, args=()):
        return self.db.execute(self.sql[name], args).fetchall()

    def test_empty_summary_is_zero(self):
        row = self.rows('ranking')[0]
        self.assertEqual((row['totalPurchases'], row['totalGold'], row['uniqueCards'], row['purchaseRuns']), (0, 0, 0, 0))
        self.assertIsNone(row['templateId'])

    def test_ranking_uses_template_and_full_uint_price(self):
        self.purchase('1', price=4294967295)
        self.purchase('2', run='r2', price=4294967295)
        self.purchase('3', template='template-2', price=0)
        rows = self.rows('ranking')
        self.assertEqual(len(rows), 2)
        self.assertEqual((rows[0]['purchaseCount'], rows[0]['gold'], rows[0]['runCount']), (2, 8589934590, 2))
        self.assertEqual((rows[0]['totalPurchases'], rows[0]['totalGold'], rows[0]['uniqueCards']), (3, 8589934590, 2))

    def test_same_instance_retry_cannot_add_a_purchase(self):
        self.purchase('1')
        with self.assertRaises(sqlite3.IntegrityError):
            self.db.execute(self.sql['insert'], ('retry', 'r1', 'instance-1', 'template-1', 'Changed', 'Item', 'Gold', 3, 100, 2, 3))
        self.assertEqual(self.rows('ranking')[0]['totalPurchases'], 1)
        self.purchase('1-other-run', run='r2')
        self.assertEqual(self.rows('ranking')[0]['totalPurchases'], 2)

    def test_account_hero_and_cutoff_apply_to_summary_and_page(self):
        self.purchase('1', at=100)
        self.purchase('2', run='r2', at=200)
        self.purchase('3', run='r3', at=300)
        account = self.rows('account', ('account-a', 'account-a'))[0]
        hero = self.rows('hero', ('Vanessa', 'Vanessa'))[0]
        cutoff = self.rows('cutoff', (150, 150))
        self.assertEqual((account['totalPurchases'], account['purchaseCount']), (2, 2))
        self.assertEqual((hero['totalPurchases'], hero['purchaseCount']), (2, 2))
        self.assertEqual((len(cutoff), cutoff[0]['totalPurchases']), (1, 1))

    def test_time_window_intersects_hero_and_includes_both_boundaries(self):
        for id_, run, at in [('old', 'r1', 99), ('start', 'r1', 100), ('end', 'r1', 200),
                             ('future', 'r1', 201), ('otherHero', 'r2', 150)]:
            self.purchase(id_, run=run, at=at)
        rows = self.rows('window', ('Vanessa', 100, 200) * 2)
        self.assertEqual({r['purchaseId'] for r in rows}, {'start', 'end'})
        self.assertEqual(rows[0]['totalPurchases'], 2)

    def test_size_intersects_hero_in_both_projections(self):
        small = '00000000-0000-0000-0000-000000000001'
        self.purchase('small', template=small)
        self.purchase('otherHero', run='r2', template=small)
        self.purchase('medium', template='00000000-0000-0000-0000-000000000002')
        self.purchase('skill', template=small)
        self.db.execute("UPDATE card_purchases SET card_type='Skill' WHERE purchase_id='skill'")
        rows = self.rows('size', ('Vanessa',) * 2)
        self.assertEqual((len(rows), rows[0]['totalPurchases'], rows[0]['purchaseCount']), (1, 1, 1))
        self.assertEqual(self.rows('emptySize')[0]['totalPurchases'], 0)

    def test_tied_times_have_stable_paging(self):
        for index in range(45):
            self.purchase(f'{index:02d}')
        first, second = self.rows('recent'), self.rows('page2')
        self.assertEqual(len(first), 21)  # one look-ahead row
        self.assertEqual(first[20]['purchaseId'], second[0]['purchaseId'])
        self.assertFalse({r['purchaseId'] for r in first[:20]} & {r['purchaseId'] for r in second[:20]})
        self.assertEqual(second[0]['totalPurchases'], 45)

    def test_invalid_prices_and_missing_parent_rejected(self):
        for price in (-1, 4294967296):
            with self.assertRaises(sqlite3.IntegrityError):
                self.purchase(str(price), price=price)
        with self.assertRaises(sqlite3.IntegrityError):
            self.purchase('orphan', run='missing')

    def test_migration_rollback_preserves_old_rows_and_version(self):
        self.db.commit()
        self.db.execute('DROP TABLE card_purchases')
        self.db.commit()
        before = list(map(tuple, self.db.execute('SELECT * FROM runs')))
        self.db.execute('BEGIN')
        self.db.execute(self.sql['create'])
        self.db.execute(self.sql['index'])
        self.db.execute('PRAGMA user_version=2')
        self.db.rollback()
        self.assertEqual(self.db.execute('PRAGMA user_version').fetchone()[0], 1)
        self.assertIsNone(self.db.execute("SELECT name FROM sqlite_master WHERE name='card_purchases'").fetchone())
        self.assertEqual(list(map(tuple, self.db.execute('SELECT * FROM runs'))), before)
        self.db.execute('BEGIN')
        self.db.execute(self.sql['create'])
        self.db.execute(self.sql['index'])
        self.db.execute('PRAGMA user_version=2')
        self.db.commit()
        self.assertEqual(self.db.execute('PRAGMA user_version').fetchone()[0], 2)
        self.assertEqual(list(map(tuple, self.db.execute('SELECT * FROM runs'))), before)


if __name__ == '__main__':
    unittest.main()
