#!/usr/bin/env python3
"""Stage wwise: install the Wwise Unity Integration from the user's own Audiokinetic files (author D-S2: Audiokinetic's files are
not shipped with this repository).

Two source forms, both validated before anything is copied:
  --package DIR  the Launcher's offline package of Wwise Unity Integration 2019.2.15.2215 (bundle.json + Unity.*.tar.xz;
                 config key `wwise_package`). wwise_offline.py verifies and safely extracts Windows (shared Common/Components/
                 Editor) + Mac into <work>/wwise-offline/, a Launcher-project-shaped tree that is then consumed like:
  --source DIR   a Unity project the Launcher integrated Wwise 2019.2.4/5/15 into (config key `wwise`).

Copied into Assets/Wwise: every file this repository has a .meta for (the .meta files are OURS and are never replaced: the game's
assets reference the Wwise components by these GUIDs, while the Launcher's install gets random new ones). Kept as ours:
ScriptableObjects/*.asset (the project's Wwise settings), LauncherInfo.json. Skipped: files we have no .meta for (plugin .pdb
files, the optional Extensions folder), the editor cache Editor/ProjectData. 2019.2.15 moved per-platform code out of Common into
platform scripts (Handwritten/Mac|Windows, EditorPlatformHelpers); those six are installed with deterministic GUID .meta files
(NEW_2019_2_15). The offline package's genuine NewVersion.txt is installed as Version.txt (the integrated-project name; bytes
unchanged, so the version label stays the vendor's). Then the shipped game's two public methods: AkInitializer.LateUpdate and
AkGameObj.SetPosition (2019.2.5 made them private; shipped game code calls them).

  python -I setup/stages/wwise.py [--source <Unity project with Assets/Wwise> | --package <offline package dir>]
  python -I setup/stages/wwise.py --patch-only --unity6
  python -I setup/stages/wwise.py --check --unity6

Patches are per source family (FAMILY_PATCHES), count-checked and idempotent: a 2019.2.15 anchor never satisfies a 2019.2.4 rule
and vice versa. Shared mobile bank paths, native-engine guards and Unity 6000.6 compatibility are reapplied by both mobile drivers,
including resumed runs. Unknown/stubbed integrations fail without replacing working files.
"""
import argparse
import hashlib
import os
from pathlib import Path
import re
import shutil
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402

OURS = ("ScriptableObjects/", "LauncherInfo.json", "InstallConfig.json", "Editor/ProjectData/")
MOBILE_OWNED = ("Deployment/API/Generated/iOS/", "Deployment/API/Generated/Android/", "Deployment/Plugins/iOS/",
                "Deployment/Plugins/Android/", "Deployment/API/Handwritten/iOS/", "Deployment/API/Handwritten/Android/",
                "Editor/EditorPlatformHelpers/iOS/", "Editor/EditorPlatformHelpers/Android/")
# Tracked .meta counterparts that are not inputs of a mobile/Mac-editor setup: a backup of an old local stub, and the Windows DSP
# plug-in DLLs that come from a Windows SDK install, not from the Unity integration archives. Windows player builds need the latter.
NOT_REQUIRED = re.compile(r"^(Deployment/API/Generated/Mac/AkSoundEnginePINVOKE_Mac\.cs\.stub\.bak"
                          r"|Deployment/Plugins/Windows/(x86|x86_64)/DSP/[^/]+\.dll)$")
# 2019.2.15 per-platform scripts with no counterpart in the 2019.2.4 tree (reconciliation evidence vendor-new-managed-files.json).
NEW_2019_2_15 = ["Deployment/API/Handwritten/Mac/AkSoundEngine.cs", "Deployment/API/Handwritten/Mac/AkMacBasePathGetter.cs",
                 "Deployment/API/Handwritten/Windows/AkWindowsBasePathGetter.cs", "Deployment/API/Handwritten/Windows/AkSoundEngine.cs",
                 "Editor/EditorPlatformHelpers/Mac/AkMacPluginActivator.cs",
                 "Editor/EditorPlatformHelpers/Windows/AkWindowsPluginActivator.cs"]

# The low-level Android IO device treats SetBasePath as an APK path. Register the
# external bank directory too. This patch must survive a fresh Launcher install.
BANK_ANCHORS = {"2019.2.4": "\n\t\tvar decodedBankFullPath = AkBasePathGetter.DecodedBankFullPath;",
                "2019.2.15": "\n\t\tvar decodedBankFullPath = akBasePathGetterInstance.DecodedBankFullPath;"}
BANK_ANCHORS["2019.2.5"] = BANK_ANCHORS["2019.2.4"]
BANK_ANCHOR = BANK_ANCHORS["2019.2.4"]
BANK_BLOCK = ("#if UNITY_ANDROID && !UNITY_EDITOR\n"
              "\t\t// [PORT] Android low-level IO resolves SetBasePath inside the APK (AAssetManager); the banks are pushed to external\n"
              "\t\t// storage (PcPaths.StreamingAssets), so add that folder as a file-system base path too (Init.bnk was AK_FileNotFound).\n"
              "\t\tUnityEngine.Debug.Log(\"WwiseUnity: [PORT] AddBasePath \" + soundBankBasePath + \" -> \" + AkSoundEngine.AddBasePath(soundBankBasePath));\n"
              "#endif\n")
INIT_SETTINGS = "Deployment/API/Handwritten/Common/AkWwiseInitializationSettings.cs"
PATCHES = [("Deployment/Components/AkInitializer.cs", "private void LateUpdate()", "public void LateUpdate()"),
           ("Deployment/Components/AkGameObj.cs", "private void SetPosition()", "public void SetPosition()"),
           (INIT_SETTINGS, BANK_ANCHOR, BANK_BLOCK + BANK_ANCHOR)]

# Shared by both mobile players, including resumes after common setup recopies Wwise.
BASEPATH_ORIGINAL = '#if UNITY_EDITOR || !UNITY_ANDROID\n\t\tfullBasePath = System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, fullBasePath);\n'
BASEPATH_IOS = "#if UNITY_IOS && !UNITY_EDITOR\n\t\t// [PORT] the iOS data (bundles, movies, banks) is copied into the app's Documents: PcPaths picks it, else the app's own\n\t\tfullBasePath = System.IO.Path.Combine(PcPlatform.PcPaths.StreamingAssets, fullBasePath);\n#elif UNITY_EDITOR || !UNITY_ANDROID\n\t\tfullBasePath = System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, fullBasePath);\n"
BASEPATH_MOBILE = "#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR\n\t\t// [PORT] the iOS / Android data (bundles, movies, banks) is pushed into the app's Documents / persistentDataPath:\n\t\t// PcPaths picks it (iOS falls back to the app's own StreamingAssets)\n\t\tfullBasePath = System.IO.Path.Combine(PcPlatform.PcPaths.StreamingAssets, fullBasePath);\n#elif UNITY_EDITOR || !UNITY_ANDROID\n\t\tfullBasePath = System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, fullBasePath);\n"

# 2019.2.4/5 (the existing working integration): unchanged.
UNITY6_PATCHES = [
    ("Deployment/Components/AkSurfaceReflector.cs", "return (ulong)meshFilter.GetInstanceID();",
     "return UnityEngine.EntityId.ToULong(meshFilter.GetEntityId());"),
    ("Editor/WwiseComponents/AkEventPlayableInspector.cs", "new System.Collections.Generic.List<int>();",
     "new System.Collections.Generic.List<UnityEngine.EntityId>();"),
    ("Editor/WwiseComponents/AkEventPlayableInspector.cs", "var id = obj.GetInstanceID();", "var id = obj.GetEntityId();"),
    ("Editor/WwiseComponents/AkEventPlayableInspector.cs", "obj.GetInstanceID() == id", "obj.GetEntityId() == id"),
]
for _file in ("AkPluginActivator.cs", "AkBuildPreprocessor.cs"):
    UNITY6_PATCHES.append(("Editor/WwiseSetupWizard/" + _file, "case UnityEditor.BuildTarget.Lumin:",
                          "case (UnityEditor.BuildTarget)39: // UnityEditor.BuildTarget.Lumin"))

# 2019.2.15: the 6000.6 EntityId replacements the working tree carries, as explicit (file, old, new, count) rules. Every count
# was measured in the vendor 2019.2.15 files. 2019.2.15 has no BuildTarget.Lumin case, so no Lumin rule.
_LIST = ("new System.Collections.Generic.List<int>();", "new System.Collections.Generic.List<UnityEngine.EntityId>();")
_ID = ("var id = obj.GetInstanceID();", "var id = obj.GetEntityId();")
_EQ = ("obj.GetInstanceID() == id", "obj.GetEntityId() == id")
UNITY6_PATCHES_2019_2_15 = [
    ("Deployment/Components/AkSurfaceReflector.cs", "return (ulong)GetInstanceID();",
     "return UnityEngine.EntityId.ToULong(GetEntityId());", 1),
    ("Deployment/Components/AkSurfaceReflector.cs", "return (ulong)meshFilter.GetInstanceID();",
     "return UnityEngine.EntityId.ToULong(meshFilter.GetEntityId());", 1),
    ("Deployment/Components/AkRoomPortal.cs", "return (ulong)GetInstanceID();", "return UnityEngine.EntityId.ToULong(GetEntityId());", 1),
    ("Deployment/API/Handwritten/Common/AkSoundEngine.cs", "(ulong) gameObject.GetInstanceID()",
     "UnityEngine.EntityId.ToULong(gameObject.GetEntityId())", 1),
    ("Deployment/API/Handwritten/Common/AkCallbackManager.cs",
     "UnityEditor.EditorUtility.InstanceIDToObject((int) AkMonitoringCallbackInfo.gameObjID)",
     "UnityEditor.EditorUtility.EntityIdToObject(UnityEngine.EntityId.FromULong(AkMonitoringCallbackInfo.gameObjID))", 1),
    ("Deployment/API/Handwritten/Common/Timeline/AkTimelineEventPlayable.cs",) + _LIST + (1,),
    ("Deployment/API/Handwritten/Common/Timeline/AkTimelineEventPlayable.cs",) + _ID + (1,),
    ("Deployment/API/Handwritten/Common/Timeline/AkTimelineEventPlayable.cs",) + _EQ + (1,),
    ("Editor/WwiseSetupWizard/AkWwiseSetupWizard.cs",) + _LIST + (2,),
    ("Editor/WwiseSetupWizard/AkWwiseSetupWizard.cs",) + _ID + (2,),
    ("Editor/WwiseSetupWizard/AkWwiseSetupWizard.cs",) + _EQ + (2,),
    ("Editor/WwiseComponents/AkEventPlayableInspector.cs",) + _LIST + (1,),
    ("Editor/WwiseComponents/AkEventPlayableInspector.cs",) + _ID + (1,),
    ("Editor/WwiseComponents/AkEventPlayableInspector.cs",) + _EQ + (1,),
    ("Editor/WwiseComponents/AkPortalManager.cs", "Dictionary<int, System.Collections.Generic.List<AkEnvironment>>",
     "Dictionary<UnityEngine.EntityId, System.Collections.Generic.List<AkEnvironment>>", 3),
    ("Editor/WwiseComponents/AkPortalManager.cs", "in_portal.GetInstanceID()", "in_portal.GetEntityId()", 2),
    ("Editor/WwiseComponents/AkPortalManager.cs", "PortalList[i].GetInstanceID()", "PortalList[i].GetEntityId()", 2),
]


def family_patches(family, unity6):
    """(file, old, new, count) rules for one source family."""
    rules = [(f, o, n, 1) for f, o, n in PATCHES[:2]]
    rules.append((INIT_SETTINGS, BANK_ANCHORS[family], BANK_BLOCK + BANK_ANCHORS[family], 1))
    if unity6:
        rules += UNITY6_PATCHES_2019_2_15 if family == "2019.2.15" else [(f, o, n, 1) for f, o, n in UNITY6_PATCHES]
    return rules


def version_info(wwise_dir):
    """(family, record file, text) from Version.txt (integrated project) or NewVersion.txt (offline package layout)."""
    for name in ("Version.txt", "NewVersion.txt"):
        p = Path(wwise_dir) / name
        if p.is_file():
            text = p.read_text(errors="replace")
            m = re.search(r"Based on Wwise SDK: 2019\.2\.(4|5|15) Build \d+", text) or re.search(r"\b2019\.2\.(4|5|15)\b", text)
            if m:
                return "2019.2." + m.group(1), name, text
            break
    sys.exit("Wwise common: require a licensed Launcher 2019.2.4/5/15 integration or the 2019.2.15.2215 offline package; "
             "unknown version. See docs/CLEAN_SETUP.md.")


def patch_text(text, old, new, count):
    """(new text, changed) for one count-checked idempotent rule, or None when the anchors do not match."""
    if text.count(new) == count and old not in text.replace(new, ""):
        return text, False
    if text.count(new) == 0 and text.count(old) == count:
        return text.replace(old, new), True
    return None


def apply_patches(dst, check=False, unity6=False, family=None):
    """Count-checked, idempotent replacements; validate all inputs before writing.

    Preserve each file's BOM and line endings. Unknown vendor revisions stop rather
    than silently claiming the ignored dependency is reproducible. --check is read-only.
    """
    originals, texts = {}, {}
    family = family or version_info(dst)[0]

    def read(rel):
        if rel not in texts:
            path = Path(dst) / rel
            if not path.is_file():
                sys.exit("missing Wwise dependency: %s; run setup/stages/wwise.py --source/--package first" % path)
            originals[rel] = path.read_bytes()
            texts[rel] = originals[rel].decode("utf-8-sig").replace("\r\n", "\n")
        return texts[rel]

    for rel, old, new, count in family_patches(family, unity6):
        result = patch_text(read(rel), old, new, count)
        if result is None:
            sys.exit("cannot patch %s for Wwise %s: expected %d original anchor(s) or %d applied patch(es); check the integration version"
                     % (rel, family, count, count))
        texts[rel] = result[0]

    # A previous iOS-only installation is also a valid predecessor.
    rel = "Deployment/API/Handwritten/Common/AkBasePathGetter.cs"
    text = read(rel)
    if text.count(BASEPATH_MOBILE) != 1:
        old = BASEPATH_IOS if text.count(BASEPATH_IOS) == 1 else BASEPATH_ORIGINAL
        if text.count(BASEPATH_MOBILE) or text.count(old) != 1:
            sys.exit("cannot patch %s: expected one original, iOS-only or shared mobile base-path block" % rel)
        texts[rel] = text.replace(old, BASEPATH_MOBILE)

    if unity6:
        # Older local integrations disabled every player. Clean Launcher installs
        # need the same capability checks; existing fail-soft bodies are preserved.
        methods = {
            "AkInitializer.cs": ["OnEnable", "OnApplicationPause", "OnApplicationFocus", "OnApplicationQuit", "LateUpdate"],
            "AkGameObj.cs": ["Register", "SetPosition", "Awake", "OnDestroy", "Update"],
            "AkAudioListener.cs": ["Awake"],
        }
        for file, names in methods.items():
            rel = "Deployment/Components/" + file
            for name in names:
                text = read(rel)
                rx = r"(?:public|private) (?:void|AKRESULT) " + name + r"\([^)]*\)\s*\{"
                matches = list(re.finditer(rx, text))
                if len(matches) != 1:
                    sys.exit("cannot patch %s: expected one %s method" % (rel, name))
                begin = matches[0].end()
                end, depth = begin, 1
                while end < len(text) and depth:
                    depth += (text[end] == "{") - (text[end] == "}")
                    end += 1
                body = text[begin:end - 1]
                if "if (!AkPortGuard.NativeEngineUsable)" in body:
                    continue
                if body.count("if (!UnityEngine.Application.isEditor)") == 1:
                    body = body.replace("if (!UnityEngine.Application.isEditor)", "if (!AkPortGuard.NativeEngineUsable)")
                else:
                    action = "return;" if name != "Register" else "{ isRegistered = true; return AKRESULT.AK_Success; }"
                    body = "\n\t\t// [PORT] Avoid native calls when this platform has no usable Wwise engine.\n\t\tif (!AkPortGuard.NativeEngineUsable)\n\t\t\t" + action + "\n" + body
                texts[rel] = text[:begin] + body + text[end - 1:]
        rel = "Deployment/Components/AkAudioListener.cs"
        text = read(rel)
        old = "if (ret && AkSoundEngine.IsInitialized())"
        legacy = "if (ret && UnityEngine.Application.isEditor && AkSoundEngine.IsInitialized())"
        new = "if (ret && AkPortGuard.NativeEngineUsable && AkSoundEngine.IsInitialized())"
        if text.count(new) != 2:
            if text.count(old) + text.count(legacy) + text.count(new) != 2:
                sys.exit("cannot patch %s: expected two listener registration guards" % rel)
            texts[rel] = text.replace(old, new).replace(legacy, new)
        rel = "Deployment/Components/AkInitializer.cs"
        text = read(rel)
        if "s_soundEngineUnavailable" not in text:
            old = "\tpublic void LateUpdate()\n\t{"
            if text.count(old) != 1:
                sys.exit("cannot patch %s: LateUpdate signature changed" % rel)
            text = text.replace(old, "\tprivate static bool s_soundEngineUnavailable;\n\n" + old)
            start = text.index(old)
            tail = text[start:]
            call = "AkSoundEngineController.Instance.LateUpdate();"
            if tail.count(call) != 1:
                sys.exit("cannot patch %s: LateUpdate call changed" % rel)
            tail = tail.replace(call, "if (s_soundEngineUnavailable) return;\n\t\ttry { " + call + " }\n\t\tcatch (System.Exception e)\n\t\t{\n\t\t\ts_soundEngineUnavailable = true;\n\t\t\tUnityEngine.Debug.LogWarning(\"[PORT] Wwise unavailable; disabling audio updates. \" + e.GetType().Name + \": \" + e.Message);\n\t\t}")
            texts[rel] = text[:start] + tail
        # The Mac bindings are the editor's real host binding (and, for 2019.2.4, the iOS generator's input). A local no-op stub
        # is not a valid installation source, even if its filenames and version record look right.
        rel = "Deployment/API/Generated/Mac/AkSoundEnginePINVOKE_Mac.cs"
        text = read(rel)
        if text.count('DllImport("AkSoundEngine"') < 1000 or re.search(r"public static .*\{ return default; \}", text):
            sys.exit("%s: native Mac bindings missing/stubbed; supply an unmodified Wwise 2019.2.x Launcher integration with real DllImports" % rel)

    changed = []
    for rel, text in texts.items():
        raw = originals[rel]
        newline = "\r\n" if b"\r\n" in raw else "\n"
        data = (b"\xef\xbb\xbf" if raw.startswith(b"\xef\xbb\xbf") else b"") + text.replace("\n", newline).encode("utf-8")
        if data != raw:
            changed.append((rel, data))
    for rel, data in changed:
        if not check:
            (Path(dst) / rel).write_bytes(data)
        print(("patch required: " if check else "patched: ") + rel)
    if check and changed:
        print("run: python -I setup/stages/wwise.py --patch-only" + (" --unity6" if unity6 else ""))
    else:
        print("wwise patches (%s): up to date (%d files checked)" % (family, len(texts)))
    return not changed if check else True


def script_meta(rel, prefix="bdsp-wwise-2019.2.15:"):
    """Deterministic MonoImporter .meta for a vendor script that has no tracked counterpart."""
    guid = hashlib.md5((prefix + rel).encode()).hexdigest()
    return ("fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n"
            "  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid)


FOLDER_META = "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def ensure_metas(dst, rels, prefix="bdsp-wwise-2019.2.15:", check=False):
    """Create missing .meta files (folders included) for newly installed vendor scripts; never rewrites an existing .meta."""
    created = []
    for rel in rels:
        parts = rel.split("/")
        for i in range(1, len(parts)):
            folder = "/".join(parts[:i])
            meta = Path(dst) / (folder + ".meta")
            if not meta.exists():
                created.append(folder + ".meta")
                if not check:
                    meta.parent.mkdir(parents=True, exist_ok=True)
                    meta.write_text(FOLDER_META % hashlib.md5((prefix + folder + "/").encode()).hexdigest())
        meta = Path(dst) / (rel + ".meta")
        if not meta.exists():
            created.append(rel + ".meta")
            if not check:
                meta.parent.mkdir(parents=True, exist_ok=True)
                meta.write_text(script_meta(rel, prefix))
    return created


def required_counterparts(source, target, family):
    """Tracked .meta files whose source file is absent, after scoping out non-inputs."""
    missing = []
    for meta in target.rglob("*.meta"):
        rel = meta.relative_to(target).as_posix()[:-5]
        if rel.startswith(OURS + MOBILE_OWNED) or NOT_REQUIRED.match(rel):
            continue
        if rel == "Version.txt" and family == "2019.2.15" and (source / "NewVersion.txt").is_file():
            continue                         # installed from the offline package's NewVersion.txt
        if "folderAsset: yes" not in meta.read_text(errors="replace") and not (source / rel).exists():
            missing.append(rel)
    return missing


def validate_source(src, dst, unity6=True):
    """Read-only candidate validation; never mistake patch-needed for invalid source. Returns the source family."""
    import subprocess
    source, target = Path(src), Path(dst)
    if source.is_symlink() or any(p.is_symlink() for p in source.rglob("*")):
        sys.exit("Wwise source contains symlinks; provide a regular-file Launcher integration before installation.")
    family = version_info(source)[0]
    apply_patches(str(source), check=True, unity6=unity6, family=family)
    missing = required_counterparts(source, target, family)
    if family == "2019.2.15":
        missing += [rel for rel in NEW_2019_2_15 if not (source / rel).is_file()]
    if missing:
        sys.exit("Wwise common source incomplete: " + ", ".join(missing[:12]) +
                 "; install the complete licensed common + Mac + Windows integration through Audiokinetic Launcher.")
    if unity6 and sys.platform == "darwin":
        for mode in ("Debug", "Profile", "Release"):
            binary = source / ("Deployment/Plugins/Mac/" + mode + "/AkSoundEngine.bundle")
            if binary.is_dir():
                binary = binary / "Contents/MacOS/AkSoundEngine"
            result = subprocess.run(["lipo", "-archs", str(binary)], capture_output=True, text=True)
            if result.returncode or "arm64" not in result.stdout.split():
                sys.exit("Wwise common: " + mode + " Mac AkSoundEngine needs a licensed arm64 slice matching the real Mac bindings. "
                         "The available old Intel integration is insufficient; no ABI or license workaround is provided.")
    print("Wwise %s source candidate validated; clean Unity import is still required." % family)
    return family


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--source", default=None)
    ap.add_argument("--package", default=None, help="Launcher offline package directory of 2019.2.15.2215")
    ap.add_argument("--patch-only", action="store_true", help="apply project patches without recopying vendor files")
    ap.add_argument("--check", action="store_true", help="check project patches without writing anything")
    ap.add_argument("--unity6", action="store_true", help="also apply Unity 6 compatibility and native-engine guards")
    ap.add_argument("--validate-source", action="store_true", help="validate the supplied source without copying or patching it")
    a = ap.parse_args()
    dst = os.path.join(paths.ASSETS, "Wwise")
    unity6 = a.unity6 or "6000." in open(os.path.join(paths.PROJECT, "ProjectSettings", "ProjectVersion.txt"), encoding="utf-8").read()
    if a.patch_only or a.check:
        sys.exit(0 if apply_patches(dst, a.check, unity6) else 1)
    package = a.package or (None if a.source else paths.CFG.get("wwise_package"))
    if package:
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        import wwise_offline
        try:
            project = str(wwise_offline.prepare_source(package, paths.WORK))
        except wwise_offline.PackageError as e:
            sys.exit(str(e))
    else:
        project = a.source or paths.CFG.get("wwise")
    if not project:
        sys.exit("no Wwise source configured (--package <Launcher offline package> or --source <Launcher-integrated project>)")
    src = os.path.join(project, "Assets", "Wwise")
    # Validate the source before overwriting any working installation.
    family = validate_source(src, dst, unity6)
    if a.validate_source:
        return
    new_files = NEW_2019_2_15 if family == "2019.2.15" else []
    for rel in ensure_metas(dst, new_files):
        print("registered (deterministic GUID): " + rel)
    # Offline layout: the vendor's NewVersion.txt becomes Version.txt, byte for byte (no relabelling).
    offline_version = family == "2019.2.15" and not os.path.isfile(os.path.join(src, "Version.txt"))
    copied = same = skipped = 0
    skipped_list = []
    for dp, dn, fn in os.walk(src):
        # Mac universal plugins are bundle directories; only their root has a
        # tracked Unity meta. Copy their contents as a unit, retaining that meta.
        for name in list(dn):
            s = os.path.join(dp, name)
            rel = os.path.relpath(s, src)
            if name.endswith(".bundle") and os.path.isfile(os.path.join(dst, rel + ".meta")):
                if any(os.path.islink(os.path.join(d, f)) for d, dirs, files in os.walk(s) for f in dirs + files):
                    sys.exit("Wwise bundle contains a symlink; supply an unpacked regular-file integration: " + rel)
                shutil.copytree(s, os.path.join(dst, rel), dirs_exist_ok=True)
                dn.remove(name)
                copied += 1
        for f in fn:
            if f.endswith(".meta"):
                continue
            rel = os.path.relpath(os.path.join(dp, f), src).replace("\\", "/")
            if rel.startswith(OURS):
                continue
            s = os.path.join(dp, f)
            d = os.path.join(dst, "Version.txt" if rel == "NewVersion.txt" and offline_version else rel)
            if not os.path.exists(d + ".meta"):
                skipped += 1
                skipped_list.append(rel)
                continue
            if os.path.exists(d) and open(s, "rb").read() == open(d, "rb").read():
                same += 1
                continue
            os.makedirs(os.path.dirname(d), exist_ok=True)
            shutil.copyfile(s, d)
            copied += 1
    print("copied %d, already identical %d, skipped (no .meta of ours) %d: %s" % (
        copied, same, skipped, ", ".join(sorted(set(os.path.splitext(x)[1] or x for x in skipped_list)))))
    # A vendor C# file nobody registered would silently drop out of the build; refuse instead.
    unregistered = sorted(x for x in skipped_list if x.endswith(".cs") and not x.startswith(MOBILE_OWNED + ("Extensions/",)))
    if unregistered:
        sys.exit("vendor scripts without a tracked/registered .meta (review and add to NEW_2019_2_15): " + ", ".join(unregistered[:20]))
    # every Wwise file / folder we have a .meta for must now exist. Folders the integration fills only at edit time
    # (Editor/ProjectData) are created empty; InstallConfig.json is the Launcher's own record, empty in this project.
    missing = []
    optional = []
    for dp, dn, fn in os.walk(dst):
        for f in fn:
            if f.endswith(".meta"):
                target = os.path.join(dp, f[:-5])
                if os.path.exists(target):
                    continue
                rel = os.path.relpath(target, dst).replace("\\", "/")
                if "folderAsset: yes" in open(os.path.join(dp, f), encoding="utf-8", errors="replace").read():
                    os.makedirs(target, exist_ok=True)
                elif rel.startswith(MOBILE_OWNED):
                    continue  # installed and checked by wwise_ios.py / wwise_android.py
                elif rel == "InstallConfig.json":
                    open(target, "wb").close()
                elif NOT_REQUIRED.match(rel):
                    optional.append(rel)
                else:
                    missing.append(rel)
    if optional:
        print("not installed (not inputs of the Mac editor / mobile builds; a Windows player needs the SDK's DSP DLLs): %d files" % len(optional))
    if missing:
        for m in missing[:20]:
            print("  missing: " + m)
        sys.exit("%d Wwise files are missing after the copy - is the Launcher integration complete (Windows + macOS)?"
                 % len(missing))
    apply_patches(dst, unity6=unity6, family=family)
    print("wwise OK (%s)" % family)


if __name__ == "__main__":
    main()
