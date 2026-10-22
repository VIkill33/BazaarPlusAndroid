import hashlib
import importlib.util
import io
import pathlib
import tempfile
import unittest
import zipfile
from unittest.mock import patch


ROOT = pathlib.Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("build_launcher", ROOT / "build_launcher.py")
build = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(build)


class Response(io.BytesIO):
    status = 200

    def geturl(self):
        return "https://example.com/dependency"


class LauncherDependencyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = pathlib.Path(self.temp.name)
        self.assets = self.root / "assets"
        self.cache = self.root / "cache"
        self.payload = b"pinned Unity dependency"
        self.descriptor = {"name": "libunity.so", "size": len(self.payload),
                           "sha256": hashlib.sha256(self.payload).hexdigest(),
                           "url": "https://example.com/primary",
                           "fallbackUrls": ["https://example.com/fallback"]}
        self.manifest = {"dependencies": [self.descriptor]}

    def bundle(self):
        build.bundle_dependencies(self.manifest, self.assets, self.cache)

    def test_verified_cache_bundles_without_network(self):
        self.cache.mkdir()
        (self.cache / self.descriptor["sha256"]).write_bytes(self.payload)
        with patch.object(build.urllib.request, "urlopen", side_effect=AssertionError("network used")):
            self.bundle()
        self.assertEqual((self.assets / self.descriptor["asset"]).read_bytes(), self.payload)
        self.assertEqual(self.descriptor["url"], "https://example.com/primary")

    def test_bad_cache_and_bad_primary_recover_via_pinned_fallback(self):
        self.cache.mkdir()
        (self.cache / self.descriptor["sha256"]).write_bytes(b"broken cache")
        with patch.object(build.urllib.request, "urlopen", side_effect=[
                Response(b"x" * len(self.payload)), Response(self.payload)]) as fetch:
            self.bundle()
        self.assertEqual(fetch.call_count, 2)
        self.assertEqual(fetch.call_args.args[0].full_url, self.descriptor["fallbackUrls"][0])
        self.assertEqual((self.assets / self.descriptor["asset"]).read_bytes(), self.payload)
        self.assertFalse(list(self.cache.glob("*.part")))

    def test_wrong_size_or_digest_fails_build_and_never_bundles(self):
        for payload in (self.payload[:-1], self.payload + b"x", b"x" * len(self.payload)):
            with self.subTest(payload=payload), patch.object(
                    build.urllib.request, "urlopen", side_effect=lambda *a, **kw: Response(payload)):
                with self.assertRaisesRegex(ValueError, "Cannot bundle dependency"):
                    self.bundle()
                self.assertFalse((self.assets / "launcher/dependencies/libunity.so").exists())
                self.assertFalse(list(self.cache.glob("*.part")))

    def test_apk_verification_rejects_missing_or_corrupt_bundled_dependency(self):
        descriptor = {**self.descriptor, "asset": "launcher/dependencies/libunity.so"}
        manifest = {"framework": descriptor, "runtime": descriptor, "plugin": descriptor,
                    "dependencies": [descriptor]}
        apk = self.root / "test.apk"
        for payload in (None, b"x" * len(self.payload), self.payload):
            with self.subTest(payload=payload):
                with zipfile.ZipFile(apk, "w") as archive:
                    if payload is not None:
                        archive.writestr("assets/" + descriptor["asset"], payload)
                if payload == self.payload:
                    build.verify_bundled_inputs(apk, manifest)
                else:
                    with self.assertRaises((KeyError, ValueError)):
                        build.verify_bundled_inputs(apk, manifest)
