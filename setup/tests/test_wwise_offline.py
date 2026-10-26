"""Offline Wwise package handling, safe extraction and version-specific patches.

Fixtures are synthetic tarballs; no Audiokinetic file is needed. Set BDSP_WWISE_VENDOR_TREE to an extracted 2019.2.15 Assets/Wwise
(Windows + Mac archives) to also run the patch table against the real vendor sources (copied into a temporary tree first).
"""
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import sys
import tarfile
import tempfile
import unittest
from unittest.mock import patch

HERE = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE / 'stages'))
import launcher  # noqa: E402
import wwise  # noqa: E402
import wwise_offline  # noqa: E402
import wwise_android  # noqa: E402
import wwise_ios  # noqa: E402

GENUINE = ("Wwise Unity Integration Version Info:\n\nBased on Wwise SDK: 2019.2.15 Build 7667\n\n"
           "Unity Integration Bundle: 2019.2.15.2215\n\nUnity Integration Version: 19\n")
LEGACY = GENUINE.replace("2019.2.15 Build 7667", "2019.2.4 Build 7329").replace("2019.2.15.2215", "2019.2.4.1806")


def make_tar(path, files, extra=()):
    """files: name -> bytes; extra: prebuilt TarInfo objects (links etc.)."""
    with tarfile.open(path, "w:xz") as tar:
        for name, data in files.items():
            info = tarfile.TarInfo(name)
            info.size = len(data)
            tar.addfile(info, io.BytesIO(data))
        for info in extra:
            tar.addfile(info)


def sha1(p):
    return hashlib.sha1(Path(p).read_bytes()).hexdigest()


class Package:
    """A synthetic offline package whose pins are patched into wwise_offline for the test."""

    def __init__(self, root, windows=None, mac=None):
        self.root = Path(root)
        self.root.mkdir(parents=True, exist_ok=True)
        windows = windows if windows is not None else {"Wwise/NewVersion.txt": GENUINE.encode(), "Wwise/Deployment/a.cs": b"common"}
        mac = mac if mac is not None else {"Wwise/Deployment/b.cs": b"mac", "Wwise/Deployment/a.cs": b"common"}
        make_tar(self.root / "Unity.Windows.tar.xz", windows)
        make_tar(self.root / "Unity.Mac.tar.xz", mac)
        make_tar(self.root / "UnityDocumentation.Windows.tar.xz", {"Wwise/Documentation/Windows/en/h.chm": b"win help"})
        make_tar(self.root / "UnityDocumentation.Mac.tar.xz", {"Wwise/Documentation/AppleCommon/en/h.chm": b"mac help"})
        self.pins = {n: (os.path.getsize(self.root / n), sha1(self.root / n)) for n in wwise_offline.COMMON}
        self.write_manifest()

    def write_manifest(self, **overrides):
        manifest = {"id": wwise_offline.PACKAGE_ID, "version": dict(wwise_offline.VERSION),
                    "productDependentData": {"wwiseSdkBuild": wwise_offline.SDK_BUILD},
                    "files": [{"name": n, "size": s, "sha1": h} for n, (s, h) in self.pins.items()]}
        manifest.update(overrides)
        (self.root / "bundle.json").write_text(json.dumps(manifest))

    def patched(self):
        return patch.dict(wwise_offline.ARCHIVES, self.pins)


class ManifestTests(unittest.TestCase):
    def test_genuine_identity_passes_and_wrong_identity_fails(self):
        with tempfile.TemporaryDirectory() as d:
            pkg = Package(d)
            with pkg.patched():
                self.assertEqual(set(wwise_offline.verify_package(d)), set(wwise_offline.COMMON))
                for bad in ({"id": "unityintegration.2019_2_4_1806"},
                            {"version": dict(wwise_offline.VERSION, minor=4)},
                            {"productDependentData": {"wwiseSdkBuild": 7329}}):
                    pkg.write_manifest(**bad)
                    with self.assertRaises(wwise_offline.PackageError):
                        wwise_offline.verify_package(d)

    def test_missing_corrupt_and_unreadable_inputs(self):
        with tempfile.TemporaryDirectory() as d:
            pkg = Package(d)
            with pkg.patched():
                mac = Path(d) / "Unity.Mac.tar.xz"
                data = mac.read_bytes()
                mac.unlink()
                with self.assertRaisesRegex(wwise_offline.PackageError, "missing"):
                    wwise_offline.verify_package(d)
                mac.write_bytes(data[:-1])
                with self.assertRaisesRegex(wwise_offline.PackageError, "bytes"):
                    wwise_offline.verify_package(d)
                mac.write_bytes(bytes([data[0] ^ 1]) + data[1:])
                with self.assertRaisesRegex(wwise_offline.PackageError, "SHA-1"):
                    wwise_offline.verify_package(d)
                mac.write_bytes(data)
                (Path(d) / "bundle.json").write_text("{not json")
                with self.assertRaisesRegex(wwise_offline.PackageError, "unreadable"):
                    wwise_offline.verify_package(d)
            with self.assertRaisesRegex(wwise_offline.PackageError, "unreadable"):
                wwise_offline.verify_package(Path(d) / "absent")

    def test_local_manifest_cannot_widen_pins(self):
        with tempfile.TemporaryDirectory() as d:
            pkg = Package(d)
            # real pins (not the fixture's): a manifest describing the fixture files is still rejected
            with self.assertRaisesRegex(wwise_offline.PackageError, "differs from the pinned"):
                wwise_offline.verify_package(d)
            self.assertEqual(wwise_offline.ARCHIVES["Unity.Windows.tar.xz"][1], "9ec6cc62a3bcda2e7a3a23a1ae40b58847db8326")

    def test_mobile_archive_required_only_for_selected_target(self):
        with tempfile.TemporaryDirectory() as d:
            pkg = Package(d)
            with pkg.patched():
                wwise_offline.verify_package(d)
                with self.assertRaisesRegex(wwise_offline.PackageError, "Unity.Android.tar.xz"):
                    wwise_offline.verify_package(d, ("android",))

    def test_install_entry_must_be_package_receipt(self):
        with tempfile.TemporaryDirectory() as d:
            pkg = Package(d)
            with pkg.patched():
                (Path(d) / "install-entry.json").write_text(json.dumps({"installType": "package"}))
                wwise_offline.verify_package(d)
                (Path(d) / "install-entry.json").write_text(json.dumps({"installType": "online"}))
                with self.assertRaises(wwise_offline.PackageError):
                    wwise_offline.verify_package(d)

    def test_version_record(self):
        self.assertEqual(wwise_offline.parse_new_version(GENUINE), wwise_offline.NEW_VERSION)
        with self.assertRaises(wwise_offline.PackageError):
            wwise_offline.parse_new_version("Wwise 2019.2.15")


class ExtractionTests(unittest.TestCase):
    def unsafe(self, d, info, data=b""):
        archive = Path(d) / "bad.tar.xz"
        with tarfile.open(archive, "w:xz") as tar:
            ok = tarfile.TarInfo("Wwise/ok.cs")
            ok.size = 2
            tar.addfile(ok, io.BytesIO(b"ok"))
            tar.addfile(info, io.BytesIO(data) if data else None)
        dest = Path(d) / "out"
        with self.assertRaises(wwise_offline.PackageError):
            wwise_offline.safe_extract(archive, dest)
        self.assertFalse(dest.exists())
        self.assertFalse((Path(d) / "evil").exists())
        self.assertEqual([p.name for p in Path(d).iterdir() if not p.name.startswith((".partial", "bad"))], [])

    def test_rejects_traversal_absolute_links_and_devices(self):
        for make in (lambda: self._file("../evil"), lambda: self._file("/tmp/evil-abs"), lambda: self._file("Wwise/../../evil"),
                     lambda: self._link("Wwise/l", tarfile.SYMTYPE, "../../evil"), lambda: self._link("Wwise/h", tarfile.LNKTYPE, "Wwise/ok.cs"),
                     lambda: self._link("Wwise/dev", tarfile.CHRTYPE, ""), lambda: self._link("Wwise/fifo", tarfile.FIFOTYPE, "")):
            with tempfile.TemporaryDirectory() as d:
                info, data = make()
                self.unsafe(d, info, data)

    def _file(self, name):
        info = tarfile.TarInfo(name)
        info.size = 4
        return info, b"evil"

    def _link(self, name, kind, target):
        info = tarfile.TarInfo(name)
        info.type = kind
        info.linkname = target
        return info, b""

    def test_extracts_selected_regular_files_into_new_directory_only(self):
        with tempfile.TemporaryDirectory() as d:
            archive = Path(d) / "a.tar.xz"
            make_tar(archive, {"Wwise/x/a.cs": b"a", "Wwise/y.so": b"\x7fELF", "WwiseUnityIntegration_Src.zip": b"zip"})
            names = wwise_offline.safe_extract(archive, Path(d) / "out", lambda r: r.startswith("Wwise/"))
            self.assertEqual(sorted(names), ["Wwise/x/a.cs", "Wwise/y.so"])
            self.assertEqual((Path(d) / "out/Wwise/x/a.cs").read_bytes(), b"a")
            self.assertFalse((Path(d) / "out/WwiseUnityIntegration_Src.zip").exists())
            with self.assertRaisesRegex(wwise_offline.PackageError, "already exists"):
                wwise_offline.safe_extract(archive, Path(d) / "out")

    def test_corrupt_archive_leaves_no_destination(self):
        with tempfile.TemporaryDirectory() as d:
            archive = Path(d) / "a.tar.xz"
            make_tar(archive, {"Wwise/a.cs": os.urandom(50000)})    # incompressible, so truncation really cuts the member
            archive.write_bytes(archive.read_bytes()[:25000])
            with self.assertRaises((tarfile.TarError, EOFError, OSError, wwise_offline.PackageError)):
                wwise_offline.safe_extract(archive, Path(d) / "out")
            self.assertFalse((Path(d) / "out").exists())


class PrepareTests(unittest.TestCase):
    def test_prepare_is_idempotent_and_receipted(self):
        with tempfile.TemporaryDirectory() as d:
            pkg = Package(Path(d) / "pkg")
            with pkg.patched():
                project = wwise_offline.prepare_source(pkg.root, Path(d) / "work")
                wwise_dir = project / "Assets/Wwise"
                self.assertEqual((wwise_dir / "Deployment/a.cs").read_bytes(), b"common")
                self.assertEqual((wwise_dir / "Deployment/b.cs").read_bytes(), b"mac")
                receipt = project.parent / wwise_offline.RECEIPT
                before = receipt.read_bytes(), receipt.stat().st_mtime_ns
                self.assertEqual(wwise_offline.prepare_source(pkg.root, Path(d) / "work"), project)
                self.assertEqual((receipt.read_bytes(), receipt.stat().st_mtime_ns), before)
                self.assertEqual(json.loads(before[0])["new_version"], wwise_offline.NEW_VERSION)

    def test_conflicting_archives_and_bad_version_fail_without_receipt(self):
        for windows, mac, message in (
                ({"Wwise/NewVersion.txt": GENUINE.encode(), "Wwise/a.cs": b"1"}, {"Wwise/a.cs": b"2"}, "disagree"),
                ({"Wwise/NewVersion.txt": LEGACY.encode()}, {"Wwise/b.cs": b"m"}, "NewVersion"),
                ({"Wwise/a.cs": b"1"}, {"Wwise/b.cs": b"m"}, "NewVersion.txt")):
            with tempfile.TemporaryDirectory() as d:
                pkg = Package(Path(d) / "pkg", windows, mac)
                with pkg.patched():
                    with self.assertRaisesRegex(wwise_offline.PackageError, message):
                        wwise_offline.prepare_source(pkg.root, Path(d) / "work")
                self.assertFalse(list((Path(d) / "work/wwise-offline").glob("2019*")))

    def test_leftover_without_receipt_is_refused_not_deleted(self):
        with tempfile.TemporaryDirectory() as d:
            pkg = Package(Path(d) / "pkg")
            with pkg.patched():
                key = hashlib.sha256("".join(pkg.pins[n][1] for n in wwise_offline.COMMON).encode()).hexdigest()[:16]
                leftover = Path(d) / "work/wwise-offline" / ("2019.2.15.2215-" + key)
                leftover.mkdir(parents=True)
                (leftover / "keep.txt").write_text("user data")
                with self.assertRaisesRegex(wwise_offline.PackageError, "interrupted"):
                    wwise_offline.prepare_source(pkg.root, Path(d) / "work")
                self.assertEqual((leftover / "keep.txt").read_text(), "user data")


class PatchTests(unittest.TestCase):
    def test_patch_text_count_checked_and_idempotent(self):
        text = "a X b X"
        self.assertEqual(wwise.patch_text(text, "X", "Y", 2), ("a Y b Y", True))
        self.assertEqual(wwise.patch_text("a Y b Y", "X", "Y", 2), ("a Y b Y", False))
        self.assertIsNone(wwise.patch_text(text, "X", "Y", 1))
        self.assertIsNone(wwise.patch_text("a Y b X", "X", "Y", 2))       # half-applied is an error, not "done"
        anchor, block = "\nvar d;", "#if A\nB\n#endif\n"
        once = wwise.patch_text("x" + anchor, anchor, block + anchor, 1)[0]
        self.assertEqual(wwise.patch_text(once, anchor, block + anchor, 1), (once, False))

    def test_prior_2019_2_4_adapters_preserved(self):
        rules = wwise.family_patches("2019.2.4", True)
        self.assertIn((wwise.INIT_SETTINGS, "\n\t\tvar decodedBankFullPath = AkBasePathGetter.DecodedBankFullPath;",
                       wwise.BANK_BLOCK + "\n\t\tvar decodedBankFullPath = AkBasePathGetter.DecodedBankFullPath;", 1), rules)
        self.assertEqual([r[:3] for r in rules[3:]], wwise.UNITY6_PATCHES)
        self.assertTrue(any("Lumin" in r[1] for r in rules))
        self.assertEqual(wwise.family_patches("2019.2.5", True), rules)

    def test_2019_2_15_rules(self):
        rules = wwise.family_patches("2019.2.15", True)
        self.assertIn("akBasePathGetterInstance.DecodedBankFullPath", rules[2][1])
        self.assertFalse(any("Lumin" in r[1] for r in rules))
        self.assertEqual(len({(r[0], r[1]) for r in rules}), len(rules))
        for f, old, new, count in rules:
            self.assertNotIn(old, new.replace(old, "", 1) if f == wwise.INIT_SETTINGS else new)
            self.assertGreaterEqual(count, 1)

    def test_version_family_detection(self):
        with tempfile.TemporaryDirectory() as d:
            self.assertRaises(SystemExit, wwise.version_info, d)
            (Path(d) / "NewVersion.txt").write_text(GENUINE)
            self.assertEqual(wwise.version_info(d)[:2], ("2019.2.15", "NewVersion.txt"))
            (Path(d) / "Version.txt").write_text(LEGACY)
            self.assertEqual(wwise.version_info(d)[:2], ("2019.2.4", "Version.txt"))
            (Path(d) / "Version.txt").write_text("Based on Wwise SDK: 2021.1.0 Build 7575")
            self.assertRaises(SystemExit, wwise.version_info, d)

    def test_deterministic_metas_never_overwrite(self):
        with tempfile.TemporaryDirectory() as d:
            (Path(d) / "Editor.meta").write_text("tracked")
            created = wwise.ensure_metas(d, ["Editor/EditorPlatformHelpers/Mac/AkMacPluginActivator.cs"])
            self.assertEqual(created, ["Editor/EditorPlatformHelpers.meta", "Editor/EditorPlatformHelpers/Mac.meta",
                                       "Editor/EditorPlatformHelpers/Mac/AkMacPluginActivator.cs.meta"])
            self.assertEqual((Path(d) / "Editor.meta").read_text(), "tracked")
            first = (Path(d) / created[-1]).read_text()
            self.assertEqual(wwise.ensure_metas(d, ["Editor/EditorPlatformHelpers/Mac/AkMacPluginActivator.cs"]), [])
            guids = {l for p in Path(d).rglob("*.meta") for l in p.read_text().splitlines() if l.startswith("guid:")}
            self.assertEqual(len(guids), 3)
            shutil.rmtree(Path(d) / "Editor")
            wwise.ensure_metas(d, ["Editor/EditorPlatformHelpers/Mac/AkMacPluginActivator.cs"])
            self.assertEqual((Path(d) / created[-1]).read_text(), first)
            guids = {l for p in Path(d).rglob("*.meta") for l in p.read_text().splitlines() if l.startswith("guid:")}
            self.assertEqual(len(guids), 3)

    def test_counterpart_requirements_scoped(self):
        with tempfile.TemporaryDirectory() as d:
            src, dst = Path(d) / "src", Path(d) / "dst"
            for rel in ["Deployment/Plugins/Windows/x86_64/DSP/AkFlanger.dll", "Deployment/Plugins/Windows/x86/DSP/AkReflect.dll",
                        "Deployment/API/Generated/Mac/AkSoundEnginePINVOKE_Mac.cs.stub.bak", "Version.txt",
                        "Deployment/Plugins/Windows/x86_64/Release/AkSoundEngine.dll", "Deployment/API/Generated/iOS/X_iOS.cs",
                        "Deployment/Real.cs"]:
                (dst / rel).parent.mkdir(parents=True, exist_ok=True)
                (dst / (rel + ".meta")).write_text("guid: x\n")
            src.mkdir()
            (src / "NewVersion.txt").write_text(GENUINE)
            self.assertEqual(sorted(wwise.required_counterparts(src, dst, "2019.2.15")),
                             ["Deployment/Plugins/Windows/x86_64/Release/AkSoundEngine.dll", "Deployment/Real.cs"])
            self.assertIn("Version.txt", wwise.required_counterparts(src, dst, "2019.2.4"))

    @unittest.skipUnless(os.environ.get("BDSP_WWISE_VENDOR_TREE"), "set BDSP_WWISE_VENDOR_TREE to an extracted 2019.2.15 Assets/Wwise")
    def test_real_vendor_sources(self):
        vendor = Path(os.environ["BDSP_WWISE_VENDOR_TREE"])
        rels = {r[0] for r in wwise.family_patches("2019.2.15", True)} | {
            "Deployment/API/Handwritten/Common/AkBasePathGetter.cs", "Deployment/Components/AkAudioListener.cs",
            "Deployment/API/Generated/Mac/AkSoundEnginePINVOKE_Mac.cs", "NewVersion.txt"}
        with tempfile.TemporaryDirectory() as d:
            for rel in rels:
                (Path(d) / rel).parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(vendor / rel, Path(d) / rel)
            self.assertEqual(wwise.version_info(d)[0], "2019.2.15")
            self.assertFalse(wwise.apply_patches(d, check=True, unity6=True))
            self.assertTrue(wwise.apply_patches(d, unity6=True))
            once = {rel: (Path(d) / rel).read_bytes() for rel in rels}
            self.assertTrue(wwise.apply_patches(d, check=True, unity6=True))
            wwise.apply_patches(d, unity6=True)
            self.assertEqual({rel: (Path(d) / rel).read_bytes() for rel in rels}, once)
            for rel in rels - {"NewVersion.txt"}:
                self.assertEqual(b"\r\n" in once[rel], b"\r\n" in (vendor / rel).read_bytes(), rel)
            text = (Path(d) / wwise.INIT_SETTINGS).read_text()
            self.assertEqual(text.count("AkSoundEngine.AddBasePath(soundBankBasePath)"), 1)
            self.assertNotIn("GetInstanceID", "".join((Path(d) / r).read_text() for r in rels if r.endswith(".cs")))


class PlatformScriptTests(unittest.TestCase):
    def test_guard_must_name_unity_editor(self):
        for mod in (wwise_android, wwise_ios):
            self.assertTrue(mod.guarded("public partial class A {\n#if UNITY_ANDROID && !UNITY_EDITOR\n#endif\n}"))
            self.assertTrue(mod.guarded("#if UNITY_EDITOR\nclass B {}\n#endif"))
            self.assertFalse(mod.guarded("#if UNITY_ANDROID\nclass C {}\n#endif"))
            self.assertFalse(mod.guarded("class D {}"))

    def test_settings_classes_excluded_and_activators_included(self):
        self.assertIn("Editor/EditorPlatformHelpers/Android/AkAndroidPluginActivator.cs", wwise_android.PLATFORM_2019_2_15)
        self.assertIn("Editor/EditorPlatformHelpers/iOS/AkiOSPluginActivator.cs", wwise_ios.PLATFORM_2019_2_15)
        self.assertFalse(set(wwise_android.PLATFORM_2019_2_15) & set(wwise_android.EXCLUDED_2019_2_15))
        self.assertFalse(set(wwise_ios.PLATFORM_2019_2_15) & set(wwise_ios.EXCLUDED_2019_2_15))


class LauncherPackageTests(unittest.TestCase):
    def test_mobile_archives_come_from_package_unless_overridden(self):
        cfg = {"target": "both", "wwise_package": "/pkg"}
        self.assertEqual(launcher.mobile_archive(cfg, "ios"), "/pkg/Unity.iOS.tar.xz")
        env = launcher.environment(cfg)
        self.assertEqual(env["BDSP_WWISE_ANDROID"], "/pkg/Unity.Android.tar.xz")
        cfg["wwise_ios"] = "/explicit/Unity.iOS.tar.xz"
        self.assertEqual(launcher.environment(cfg)["BDSP_WWISE_IOS"], "/explicit/Unity.iOS.tar.xz")
        self.assertEqual(launcher.stage_config(cfg)["wwise_package"], "/pkg")

    def test_ios_plan_ends_with_unsigned_ipa(self):
        names = [n for n, _ in launcher.plan({"target": "ios"}, Path("/python"))]
        self.assertEqual(names[-2:], ["ios.xcode", "ios.ipa"])
        names = [n for n, _ in launcher.plan({"target": "android"}, Path("/python"))]
        self.assertEqual(names[-1], "android.package")


if __name__ == "__main__":
    unittest.main()
