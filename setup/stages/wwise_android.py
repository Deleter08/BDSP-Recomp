#!/usr/bin/env python3
"""Optional stage wwise_android: the Wwise 2019.2.15 Android sound engine for the Android build (Audiokinetic's files are not shipped
with this repository - only our .meta files and this script are).

Source = Unity.Android.tar.xz of the Wwise Unity Integration 2019.2.15.7667.2215 (Audiokinetic Launcher bundle
unityintegration.2019_2_15_2215, sha1 8b37f847b0ec4e26de59c540c2c5a0e8b3d6ebbe), or the folder it was extracted into (the one holding
Wwise/Deployment/...). It is the same engine version as the macOS AkSoundEngine.bundle and the iOS static lib.

  1 lib       Wwise/Deployment/Plugins/Android/arm64-v8a/Release/libAkSoundEngine.so (the Release build: no profiler comms on the phone).
              Wwise's AkPluginActivator enables only the folder of the current plug-in config (none set = Profile): BdspWinBuild.Android.cs
              switches it to Release for the Android player build and restores it. No DSP/*.so is installed: every plug-in the banks
              use (PluginInfo.xml: ParametricEQ, RoomVerb, Flanger, Meter, DefaultSink) is built into libAkSoundEngine.so (the Debug
              build still carries CAkParametricEQFX / CAkRoomVerbFX / CAkMeterFX / the flanger params; the Release build is stripped),
              the Vorbis decoder too. The lib needs only libOpenSLES / libandroid / libz / libc / libm / libdl (llvm-readelf -d).
  2 bindings  Wwise/Deployment/API/Generated/Android/*_Android.cs = the integration's own Generated/Android (SWIG 4.0.2), NOT the macOS
              ones: Android's AkOSChar is char (UnmanagedType.LPStr) where the Mac bindings marshal wchar_t (LPWStr), and 8 overloads
              are numbered differently. The only hand edit: AkSoundEnginePINVOKE gets a CSharp_new_AkRoomParams() forwarding to
              CSharp_new_AkRoomParams__SWIG_0 (Generated/Common/AkRoomParams.cs, SWIG 3.0.12, calls it by that name). Every EntryPoint
              is checked against the lib's exported symbols (llvm-nm -D from the NDK), and every AkSoundEnginePINVOKE.CSharp_* call
              of Generated/Common + Handwritten + Components must exist in the binding. The integration's Handwritten/Android files and
              Editor activators are not installed (Common already has StringFromIntPtrOSString for Android; no per-platform settings
              class, as on iOS: the global initialization settings apply).
  3 bank path AkBasePathGetter.GetPlatformBasePath on Android starts from PcPlatform.PcPaths.StreamingAssets (persistentDataPath/
              StreamingAssets, where setup/deploy_android.sh pushes the data) instead of leaving the path relative to the APK. The banks
              (StreamingAssets/Audio/GeneratedSoundBanks/Android) are APFS clones of the Mac ones, made by the Android player step.
  4 legacy    the old no-audio stand-in (Assets/Scripts/_Ours/PcPlatform/WwiseAndroid) defines the same types: it must be gone
              (it was moved to handoff/android-legacy-wwise-stub). This stage stops while it is still under Assets.

  python -I setup/stages/wwise_android.py [--source <Unity.Android.tar.xz | extracted folder>] [--check]
--check: report what is installed / stale, change nothing (exit 1 if anything is missing).

The description above is the 2019.2.4 common integration (the existing working tree; that path is unchanged). When the installed
common code is 2019.2.15 (Assets/Wwise/Version.txt, e.g. from the offline package) the vendor files are coherent as shipped:
  - Generated/Android is installed WITHOUT the AkRoomParams forwarder (2019.2.15 Common already calls CSharp_new_AkRoomParams__SWIG_0);
  - 2019.2.15 moved per-platform code out of Common, so Handwritten/Android/AkSoundEngine.cs (StringFromIntPtrOSString),
    AkAndroidBasePathGetter.cs (DefaultPlatformName) and Editor/EditorPlatformHelpers/Android/AkAndroidPluginActivator.cs
    (BuildTarget.Android -> "Android" for AkPluginActivator / AkBuildPreprocessor) are installed with deterministic .meta files;
  - AkAndroidSettings.cs and the Android Wwise menu are NOT installed: the working build runs on the global initialization settings
    (no Android platform settings asset), and registering the class would let the editor add one. A reference to an excluded class
    from an installed file stops the stage instead of compiling into an error.
  - every installed platform script must be guarded by an #if that names UNITY_EDITOR (player-only or editor-only), because the
    Mac editor compiles its own Mac partials while the Android target is active.
Entry points are still checked against the engine's exports and every shared AkSoundEnginePINVOKE call must resolve.
"""
import argparse
import hashlib
import os
import re
import shutil
import subprocess
import sys
import tempfile

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import wwise  # noqa: E402

TAR_SHA1 = "8b37f847b0ec4e26de59c540c2c5a0e8b3d6ebbe"
ARCH = "arm64-v8a"
TAR_LIB = "Wwise/Deployment/Plugins/Android/%s/Release/libAkSoundEngine.so" % ARCH
TAR_GEN = "Wwise/Deployment/API/Generated/Android"
DOWNLOADS = os.path.join(os.path.dirname(paths.PROJECT), "downloads", "wwise-android-2019.2.15")
DEFAULT_SOURCE = os.path.join(DOWNLOADS, "x")
DEFAULT_TARBALL = os.path.join(DOWNLOADS, "Unity.Android.tar.xz")
WWISE = os.path.join(paths.ASSETS, "Wwise")
LIB = os.path.join(WWISE, "Deployment", "Plugins", "Android", ARCH, "Release", "libAkSoundEngine.so")
GEN_ANDROID = os.path.join(WWISE, "Deployment", "API", "Generated", "Android")
LEGACY = os.path.join(paths.ASSETS, "Scripts", "_Ours", "PcPlatform", "WwiseAndroid")
NDK = os.path.expanduser(os.environ.get("BDSP_ANDROID_NDK") or os.path.join(
    os.environ.get("BDSP_ANDROID_SDK") or "~/Library/Android/sdk", "ndk", "27.2.12479018"))

BASEPATH = os.path.join(WWISE, "Deployment", "API", "Handwritten", "Common", "AkBasePathGetter.cs")
BASEPATH_ORIG = ("#if UNITY_EDITOR || !UNITY_ANDROID\n"
                 "\t\tfullBasePath = System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, fullBasePath);\n")
BASEPATH_IOS = ("#if UNITY_IOS && !UNITY_EDITOR\n"
                "\t\t// [PORT] the iOS data (bundles, movies, banks) is copied into the app's Documents: PcPaths picks it, else the app's own\n"
                "\t\tfullBasePath = System.IO.Path.Combine(PcPlatform.PcPaths.StreamingAssets, fullBasePath);\n"
                "#elif UNITY_EDITOR || !UNITY_ANDROID\n"
                "\t\tfullBasePath = System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, fullBasePath);\n")
BASEPATH_NEW = ("#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR\n"
                "\t\t// [PORT] the iOS / Android data (bundles, movies, banks) is pushed into the app's Documents / persistentDataPath:\n"
                "\t\t// PcPaths picks it (iOS falls back to the app's own StreamingAssets)\n"
                "\t\tfullBasePath = System.IO.Path.Combine(PcPlatform.PcPaths.StreamingAssets, fullBasePath);\n"
                "#elif UNITY_EDITOR || !UNITY_ANDROID\n"
                "\t\tfullBasePath = System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, fullBasePath);\n")

PINVOKE_ANCHOR = "static AkSoundEnginePINVOKE() {\r\n  }\r\n"
PINVOKE_SHIM = ("\r\n  // [PORT] wwise_android.py: Generated/Common/AkRoomParams.cs (SWIG 3.0.12) calls the name the SWIG 3 bindings gave this constructor\r\n"
                "  public static global::System.IntPtr CSharp_new_AkRoomParams() { return CSharp_new_AkRoomParams__SWIG_0(); }\r\n")
HEADER = "// [PORT] installed by setup/stages/wwise_android.py from the Wwise 2019.2.15 Unity.Android integration (Generated/Android)%s.\r\n"

# our .meta files (committed, like every Wwise .meta): fixed GUIDs, so a re-install never re-keys anything
FOLDER_META = "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
SCRIPT_META = ("fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n"
               "  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
LIB_META = """fileFormatVersion: 2
guid: %s
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Android: Android
    second:
      enabled: 1
      settings:
        CPU: ARM64
  - first:
      Any:
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  - first:
      Standalone: OSXUniversal
    second:
      enabled: 0
      settings: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def guid(rel):
    return hashlib.md5(("bdsp-wwise-android:" + rel).encode()).hexdigest()


def rel(p):
    return os.path.relpath(p, paths.ASSETS).replace(os.sep, "/")


def write_if_changed(path, data, check, report):
    data = data.encode("utf-8") if isinstance(data, str) else data
    old = open(path, "rb").read() if os.path.exists(path) else None
    if old == data:
        return True
    report.append(("missing " if old is None else "stale ") + rel(path))
    if not check:
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb") as f:
            f.write(data)
    return False


def sha1(path):
    h = hashlib.sha1()
    with open(path, "rb") as f:
        for b in iter(lambda: f.read(1 << 22), b""):
            h.update(b)
    return h.hexdigest()


def run(cmd):
    p = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    if p.returncode:
        sys.exit("failed (%d): %s\n%s" % (p.returncode, " ".join(cmd), (p.stdout + p.stderr)[-2000:]))
    return p.stdout


def llvm_nm():
    hits = [os.path.join(NDK, "toolchains", "llvm", "prebuilt", d, "bin", "llvm-nm")
            for d in (os.listdir(os.path.join(NDK, "toolchains", "llvm", "prebuilt")) if os.path.isdir(os.path.join(NDK, "toolchains", "llvm", "prebuilt")) else [])]
    hits = [h for h in hits if os.path.isfile(h)]
    return hits[0] if hits else None


def source_tree(source, tmp):
    """The folder holding Wwise/Deployment/...: the given folder, or the tarball extracted (lib + Generated/Android only) into tmp."""
    if os.path.isdir(source):
        for base in (source, os.path.dirname(source)):
            if os.path.isfile(os.path.join(base, TAR_LIB)):
                return base
        sys.exit("no %s under %s" % (TAR_LIB, source))
    if not os.path.isfile(source):
        sys.exit("no Wwise Android integration at %s (Unity.Android.tar.xz of Wwise Unity Integration 2019.2.15 - Audiokinetic Launcher)" % source)
    got = sha1(source)
    if got != TAR_SHA1:
        sys.exit("%s: sha1 %s, expected %s (Unity_2019.2.15.7667.2215 Unity.Android.tar.xz)" % (source, got, TAR_SHA1))
    print("extracting the Android lib + bindings from %s ..." % source)
    run(["tar", "-xJf", source, "-C", tmp, TAR_LIB, TAR_GEN])
    return tmp


def install_lib(tree, check, report):
    ok = all([write_if_changed(os.path.join(WWISE, "Deployment", "Plugins", "Android.meta"), FOLDER_META % guid("Plugins/Android"), check, report),
              write_if_changed(os.path.join(WWISE, "Deployment", "Plugins", "Android", ARCH + ".meta"), FOLDER_META % guid("Plugins/Android/" + ARCH), check, report),
              write_if_changed(os.path.join(WWISE, "Deployment", "Plugins", "Android", ARCH, "Release.meta"),
                               FOLDER_META % guid("Plugins/Android/%s/Release" % ARCH), check, report),
              write_if_changed(LIB + ".meta", LIB_META % guid("Plugins/Android/%s/Release/libAkSoundEngine.so" % ARCH), check, report)])
    if check or tree is None:
        if not os.path.isfile(LIB):
            report.append("missing " + rel(LIB))
            return False
        return ok
    src = os.path.join(tree, TAR_LIB)
    if os.path.isfile(LIB) and sha1(LIB) == sha1(src):
        print("lib up to date: %s" % rel(LIB))
        return ok
    os.makedirs(os.path.dirname(LIB), exist_ok=True)
    shutil.copyfile(src, LIB)
    report.append("installed " + rel(LIB))
    print("lib: %s (%.1f MB)" % (rel(LIB), os.path.getsize(LIB) / 1e6))
    return ok


def exported_symbols():
    nm = llvm_nm()
    if not nm or not os.path.isfile(LIB):
        return None
    p = subprocess.run([nm, "-D", "--defined-only", LIB], stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
    if p.returncode:
        sys.exit("llvm-nm -D %s failed (%d)" % (LIB, p.returncode))
    return set(m.group(1) for m in re.finditer(r" T (CSharp_\w+)$", p.stdout, re.M))


def install_bindings(tree, check, report):
    ok = write_if_changed(GEN_ANDROID + ".meta", FOLDER_META % guid("API/Generated/Android"), check, report)
    exports = exported_symbols()
    if exports is None:
        print("bindings: (no llvm-nm under %s or no lib yet: entry points not checked)" % NDK)
    names = None
    if tree is not None:
        names = sorted(f for f in os.listdir(os.path.join(tree, TAR_GEN)) if f.endswith("_Android.cs"))
    else:
        names = sorted(f for f in os.listdir(GEN_ANDROID) if f.endswith("_Android.cs")) if os.path.isdir(GEN_ANDROID) else []
    if not names:
        report.append("missing bindings under " + rel(GEN_ANDROID))
        return False
    for name in names:
        if tree is not None:
            data = open(os.path.join(tree, TAR_GEN, name), "rb").read()
        else:
            data = open(os.path.join(GEN_ANDROID, name), "rb").read()
        text = data.decode("utf-8-sig")
        if not text.startswith("#if UNITY_ANDROID"):
            sys.exit("%s: expected the Android #if guard on the first line" % name)
        if name == "AkSoundEnginePINVOKE_Android.cs":
            if tree is not None and text.count(PINVOKE_ANCHOR) != 1:
                sys.exit("%s: expected the static constructor anchor once (CRLF)" % name)
            if PINVOKE_SHIM not in text:
                text = text.replace(PINVOKE_ANCHOR, PINVOKE_ANCHOR + PINVOKE_SHIM)
            imports = set(re.findall(r'EntryPoint="(CSharp_\w+)"', text))
            if exports is not None:
                missing = sorted(imports - exports)
                if missing:
                    sys.exit("the Android libAkSoundEngine.so does not export %d entry points the binding imports: %s" % (len(missing), ", ".join(missing[:20])))
                print("bindings: %d entry points, all exported by the Android lib (%d CSharp_* exports)" % (len(imports), len(exports)))
            # every call the shared (Common / Handwritten / Components) code makes must resolve
            defined = set(re.findall(r"static (?:extern )?[\w.:<>]+ (CSharp_\w+)\(", text))
            called = set()
            for root in (os.path.join(WWISE, "Deployment", "API", "Generated", "Common"), os.path.join(WWISE, "Deployment", "API", "Handwritten"),
                         os.path.join(WWISE, "Deployment", "Components")):
                for d, _, files in os.walk(root):
                    for f in files:
                        if f.endswith(".cs"):
                            called |= set(re.findall(r"AkSoundEnginePINVOKE\.(CSharp_\w+)", open(os.path.join(d, f), encoding="utf-8-sig").read()))
            unresolved = sorted(called - defined)
            if unresolved:
                sys.exit("shared Wwise code calls %d AkSoundEnginePINVOKE entries the Android binding lacks: %s" % (len(unresolved), ", ".join(unresolved[:20])))
            print("bindings: all %d AkSoundEnginePINVOKE calls of the shared Wwise code resolve" % len(called))
        elif name != "AkAudioAPI_Android.cs":
            pass
        lines = text.split("\r\n", 1)
        if not lines[1].startswith("// [PORT]"):
            text = lines[0] + "\r\n" + HEADER % "" + lines[1]
        dst = os.path.join(GEN_ANDROID, name)
        ok &= write_if_changed(dst, text, check, report)
        ok &= write_if_changed(dst + ".meta", SCRIPT_META % guid("API/Generated/Android/" + name), check, report)
    return ok


PLATFORM_2019_2_15 = ["Deployment/API/Handwritten/Android/AkSoundEngine.cs", "Deployment/API/Handwritten/Android/AkAndroidBasePathGetter.cs",
                      "Editor/EditorPlatformHelpers/Android/AkAndroidPluginActivator.cs"]
EXCLUDED_2019_2_15 = {"Deployment/API/Handwritten/Android/AkAndroidSettings.cs": "AkAndroidSettings",
                      "Editor/WwiseMenu/Android/AkWwiseMenu_Android.cs": "AkWwiseMenu_Android"}


def source_tree_2019_2_15(source, tmp):
    """Folder holding Wwise/...: an extracted folder, or the verified tarball's needed members safely extracted into tmp."""
    if os.path.isdir(source):
        return source_tree(source, tmp)
    if not os.path.isfile(source):
        sys.exit("no Wwise Android integration at %s (Unity.Android.tar.xz of Wwise Unity Integration 2019.2.15 - Audiokinetic Launcher)" % source)
    got = sha1(source)
    if got != TAR_SHA1:
        sys.exit("%s: sha1 %s, expected %s (Unity_2019.2.15.7667.2215 Unity.Android.tar.xz)" % (source, got, TAR_SHA1))
    import wwise_offline
    out = os.path.join(tmp, "x")
    print("extracting the Android lib, bindings and platform scripts from %s ..." % source, flush=True)
    try:
        wwise_offline.safe_extract(source, out, lambda r: r == TAR_LIB or r.startswith((TAR_GEN + "/",) + tuple(
            "Wwise/" + p.rsplit("/", 1)[0] + "/" for p in PLATFORM_2019_2_15)))
    except wwise_offline.PackageError as e:
        sys.exit(str(e))
    return out


def guarded(text):
    """True when the first preprocessor #if names UNITY_EDITOR (player-only !UNITY_EDITOR or editor-only UNITY_EDITOR)."""
    m = re.search(r"^\s*#if\b(.*)$", text, re.M)
    return bool(m and "UNITY_EDITOR" in m.group(1))


def install_2019_2_15(tree, check, report):
    """Vendor-coherent 2019.2.15 Android: Generated/Android as shipped + the required platform scripts."""
    ok = write_if_changed(GEN_ANDROID + ".meta", FOLDER_META % guid("API/Generated/Android"), check, report)
    exports = exported_symbols()
    if exports is None:
        print("bindings: (no llvm-nm under %s or no lib yet: entry points not checked)" % NDK)
    gen_dir = os.path.join(tree, TAR_GEN) if tree is not None else GEN_ANDROID
    names = sorted(f for f in os.listdir(gen_dir) if f.endswith("_Android.cs")) if os.path.isdir(gen_dir) else []
    if not names:
        report.append("missing bindings under " + rel(GEN_ANDROID))
        return False
    pinvoke = None
    for name in names:
        text = open(os.path.join(gen_dir, name), "rb").read().decode("utf-8-sig")
        if not text.startswith("#if UNITY_ANDROID"):
            sys.exit("%s: expected the Android #if guard on the first line" % name)
        if PINVOKE_SHIM in text:
            sys.exit("%s: the 2019.2.4 AkRoomParams forwarder is present; 2019.2.15 Common does not use it" % name)
        lines = text.split("\r\n", 1) if "\r\n" in text else text.split("\n", 1)
        if not lines[1].startswith("// [PORT]"):
            nl = "\r\n" if "\r\n" in text else "\n"
            text = lines[0] + nl + (HEADER % ", as shipped").rstrip("\r\n") + nl + lines[1]
        if name == "AkSoundEnginePINVOKE_Android.cs":
            pinvoke = text
        dst = os.path.join(GEN_ANDROID, name)
        ok &= write_if_changed(dst, text, check, report)
        ok &= write_if_changed(dst + ".meta", SCRIPT_META % guid("API/Generated/Android/" + name), check, report)
    if pinvoke is None:
        sys.exit("no AkSoundEnginePINVOKE_Android.cs in the Android bindings")
    imports = set(re.findall(r'EntryPoint\s*=\s*"(CSharp_\w+)"', pinvoke))
    if len(imports) < 1000:
        sys.exit("AkSoundEnginePINVOKE_Android.cs: only %d native entry points; not a genuine generated binding" % len(imports))
    if exports is not None:
        missing = sorted(imports - exports)
        if missing:
            sys.exit("the Android libAkSoundEngine.so does not export %d entry points the binding imports: %s" % (len(missing), ", ".join(missing[:20])))
        print("bindings: %d entry points, all exported by the Android lib (%d CSharp_* exports)" % (len(imports), len(exports)))
    for rel_path in PLATFORM_2019_2_15:
        src = os.path.join(tree, "Wwise", rel_path) if tree is not None else os.path.join(WWISE, rel_path)
        if not os.path.isfile(src):
            report.append("missing " + rel_path)
            ok = False
            continue
        data = open(src, "rb").read()
        text = data.decode("utf-8-sig")
        if not guarded(text):
            sys.exit("%s: platform script without a UNITY_EDITOR-aware #if guard; it would collide with the Mac editor partials" % rel_path)
        for excluded, cls in EXCLUDED_2019_2_15.items():
            if re.search(r"\b%s\b" % cls, text):
                sys.exit("%s references %s, which this stage deliberately does not install (%s); review before changing the exclusion"
                         % (rel_path, cls, excluded))
        dst = os.path.join(WWISE, rel_path)
        ok &= write_if_changed(dst, data, check, report)
        parts = rel_path.split("/")
        for i in range(2, len(parts)):       # new folders only; an existing tracked folder .meta is never rewritten
            folder = "/".join(parts[:i])
            if not os.path.exists(os.path.join(WWISE, folder + ".meta")):
                ok &= write_if_changed(os.path.join(WWISE, folder + ".meta"), FOLDER_META % guid(folder + "/"), check, report)
        ok &= write_if_changed(dst + ".meta", SCRIPT_META % guid(rel_path), check, report)
    # every call the shared code (Generated/Common, Handwritten incl. Android, Components) makes must resolve in the binding
    defined = set(re.findall(r"static (?:extern )?[\w.:<>]+ (CSharp_\w+)\(", pinvoke))
    called = set()
    for root in (os.path.join(WWISE, "Deployment", "API", "Generated", "Common"), os.path.join(WWISE, "Deployment", "API", "Handwritten"),
                 os.path.join(WWISE, "Deployment", "Components")):
        for d, _, files in os.walk(root):
            if os.sep + "Handwritten" + os.sep in d + os.sep and os.path.basename(d) in ("iOS", "Mac", "Windows"):
                continue
            for f in files:
                if f.endswith(".cs"):
                    called |= set(re.findall(r"AkSoundEnginePINVOKE\.(CSharp_\w+)", open(os.path.join(d, f), encoding="utf-8-sig").read()))
    unresolved = sorted(called - defined)
    if unresolved:
        sys.exit("shared Wwise code calls %d AkSoundEnginePINVOKE entries the Android binding lacks: %s" % (len(unresolved), ", ".join(unresolved[:20])))
    print("bindings: all %d AkSoundEnginePINVOKE calls of the shared Wwise code resolve" % len(called))
    return ok


def patch_basepath(check, report):
    text = open(BASEPATH, encoding="utf-8-sig").read()
    if BASEPATH_NEW in text:
        return True
    if text.count(BASEPATH_IOS) == 1:
        old = BASEPATH_IOS
    elif text.count(BASEPATH_ORIG) == 1:
        old = BASEPATH_ORIG
    else:
        sys.exit("cannot patch %s: neither the original nor the iOS-patched streamingAssetsPath block was found once" % rel(BASEPATH))
    report.append(("unpatched " if check else "patched ") + rel(BASEPATH) + " (Android banks via PcPaths.StreamingAssets)")
    if not check:
        raw = open(BASEPATH, "rb").read()
        bom = b"\xef\xbb\xbf" if raw.startswith(b"\xef\xbb\xbf") else b""
        with open(BASEPATH, "wb") as f:
            f.write(bom + text.replace(old, BASEPATH_NEW).encode("utf-8"))
    return False


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--source", default=os.environ.get("BDSP_WWISE_ANDROID") or (DEFAULT_SOURCE if os.path.isdir(DEFAULT_SOURCE) else DEFAULT_TARBALL))
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    if not os.path.isdir(os.path.join(WWISE, "Deployment", "API", "Generated", "Common")):
        sys.exit("no Assets/Wwise: install the Wwise integration first (setup/stages/wwise.py)")
    if os.path.isdir(LEGACY):
        sys.exit("%s still exists: the old no-audio Android stand-in defines the same types as the real bindings - move it out of Assets "
                 "(it lives in handoff/android-legacy-wwise-stub)" % os.path.relpath(LEGACY, paths.PROJECT))
    family = wwise.version_info(WWISE)[0]
    common_ok = wwise.apply_patches(WWISE, check=a.check, unity6=True, family=family)
    report = []
    with tempfile.TemporaryDirectory(prefix="wwise_android_") as tmp:
        if family == "2019.2.15":
            tree = None if a.check else source_tree_2019_2_15(a.source, tmp)
            ok = install_lib(tree, a.check, report)
            ok &= install_2019_2_15(tree, a.check, report)
        else:
            tree = None if a.check else source_tree(a.source, tmp)
            ok = install_lib(tree, a.check, report)
            ok &= install_bindings(tree, a.check, report)
    ok &= patch_basepath(a.check, report)
    ok &= common_ok
    for line in report:
        print("  " + line)
    if a.check:
        print("wwise_android: " + ("installed, up to date" if ok else "NOT installed / stale - run: python -I setup/stages/wwise_android.py"))
        return 0 if ok else 1
    print("wwise_android: done (%d change(s))" % len(report))
    return 0


if __name__ == "__main__":
    sys.exit(main())
