import json
import pathlib
import re
import shutil
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET


class LauncherLocalizationTests(unittest.TestCase):
    def test_bilingual_resources_and_error_resolution(self):
        main = pathlib.Path(__file__).resolve().parents[1] / "launcher/fusionApp/src/main"

        def strings(locale):
            return {entry.attrib["name"]: (entry.text or "").strip('"')
                    .replace("\\'", "'").replace('\\"', '"').replace("\\n", "\n")
                    for entry in ET.parse(main / f"res/{locale}/bpp_strings.xml").getroot()}

        english, chinese = strings("values"), strings("values-zh")
        self.assertEqual(english.keys(), chinese.keys())
        for key, text in english.items():
            self.assertTrue(text, key)
            self.assertIsNone(re.search(r"[\u4e00-\u9fff]", text), key)
            self.assertEqual(sorted(re.findall(r"%\d+\$[ds]", text)),
                             sorted(re.findall(r"%\d+\$[ds]", chinese[key])), key)

        # Run the production message adapter against resource-backed Context doubles.
        # Resource selection/layout on a real Android device is a separate acceptance check.
        with tempfile.TemporaryDirectory() as directory:
            out = pathlib.Path(directory)
            fields = "\n".join(f"public static final int {key} = {index};"
                               for index, key in enumerate(english))
            (out / "R.java").write_text(
                "package dev.allofus.fusioncore; public final class R {"
                "public static final class string {" + fields + "}}")
            arrays = [", ".join(json.dumps(value, ensure_ascii=False) for value in (values[key] for key in english))
                      for values in (english, chinese)]
            (out / "Context.java").write_text(
                "package android.content; public final class Context {"
                "private final boolean chinese; public Context(boolean chinese) {this.chinese=chinese;}"
                "public String getString(int id) {String[] values = chinese ? new String[]{"
                + arrays[1] + "} : new String[]{" + arrays[0] + "}; return values[id];}}")
            (out / "MessagesTest.java").write_text('''
package dev.allofus.fusioncore;
import android.content.Context;
public final class MessagesTest {
    public static void main(String[] args) {
        Context en = new Context(false), zh = new Context(true);
        require(LauncherMessages.error(en, "文件完整性校验失败，请重试")
                .equals("File integrity check failed. Please retry."));
        require(LauncherMessages.error(zh, "文件完整性校验失败，请重试")
                .equals("文件完整性校验失败，请重试"));
        require(LauncherMessages.error(en, "已取消准备，可重新初始化；已校验的缓存会保留")
                .equals("Preparation canceled. You can retry; verified cached files are kept."));
        require(LauncherMessages.error(en, "拒绝越界压缩文件")
                .equals("Archive entry escapes its destination"));
        require(LauncherMessages.error(en, "Platform diagnostic").equals("Platform diagnostic"));
        require(LauncherMessages.error(en, null).equals("Runtime preparation failed. You can retry."));
        require(LauncherMessages.processExit(en, 5).equals("Native crash"));
        require(LauncherMessages.processExit(zh, 5).equals("原生崩溃"));
        require(LauncherMessages.processExit(en, 999).equals("Process ended"));
    }
    private static void require(boolean value) {
        if (!value) throw new AssertionError("Localized message mismatch");
    }
}''')
            subprocess.run([shutil.which("javac"), "--release", "8", "-encoding", "UTF-8",
                            "-d", directory, str(out / "R.java"), str(out / "Context.java"),
                            str(out / "MessagesTest.java"),
                            str(main / "java/dev/allofus/fusioncore/LauncherMessages.java")], check=True)
            subprocess.run([shutil.which("java"), "-cp", directory,
                            "dev.allofus.fusioncore.MessagesTest"], check=True)
