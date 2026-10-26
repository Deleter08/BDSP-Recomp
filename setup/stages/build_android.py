#!/usr/bin/env python3
"""Optional stage build_android: an Android APK of the game (sh setup/build_android.sh) - Builds/android_<time>/Dpr.apk + data/.
Unity 6000.6, IL2CPP ARM64, Vulkan + OpenGLES3, Wwise 2019.2.15 audio, every render / shadow / frame-rate fix of the iOS build.

Sub-steps, each recorded in <work>/build/steps_android6.json (an interrupted run resumes at the one that did not finish; the Mono-ARMv7
era's steps_android.json is not read):
   1 toolchain the editor's Android Build Support (<editor>/PlaybackEngines/AndroidPlayer, next to Unity.app; il2cpp ARM64 libs),
               the Android SDK (platform-tools, build-tools, the target platform), NDK and a JDK 17 - the module ships none of them, so
               BdspWinBuild.Android.cs points the editor at ~/Library/Android/sdk + its NDK + homebrew openjdk@17 (BDSP_ANDROID_SDK /
               _NDK / _JDK override) - and the JKS debug keystore <work>/build/android/ (JDK 17 keytool). Nothing in the SDK changes.
               Then wwise_android.py: the Wwise 2019.2.15 Android engine (arm64-v8a libAkSoundEngine.so) + its bindings, installed when
               missing/stale from the Launcher's Unity.Android.tar.xz (BDSP_WWISE_ANDROID / its --source).
   2 inputs    the platform-neutral inputs the macOS build made (dpr_manifest_shipped.tsv, bundle_plan.tsv,
               shipped_shader_variants.tsv): made again only when one is missing (build_mac.py's manifest / plan / variants)
   3 offline   android_compile_check.py: the player scripts compiled with the Android defines by the editor's Roslyn, no editor run (seconds)
   4 compile   Unity BdspWinBuild.WinBuild.AndroidCompileScripts: the player scripts compiled by Unity for Android (UNITY_ANDROID, no
               editor) - it does not need the active target, so a code error stops the run before the long switch
   5 switch    Unity BdspWinBuild.WinBuild.AndroidSwitch: active build target -> Android (texture subtarget ASTC). Assets re-import for
               Android the first time; the macOS import results stay in Library (asset pipeline v2). setup/build_mac.sh needs the target
               back on StandaloneOSX: --back-to-mac.
   6 check     Unity BdspWinBuild.WinBuild.AndroidCheck
   7 textures  Unity BdspWinBuild.WinBuild.AndroidConvertTextures: the ASTC copy of every DXT/BC native texture .asset (the iOS build's:
               same copies, <work>/build/ios_textures; incremental, instant when the iOS build already made them). BDSP_ANDROID_TEXTURES=native
               skips this step and the swap.
   8 bundles   Unity BdspWinBuild.WinBuild.AndroidBuildBundlesFull (Android bundles -> Builds/bundles_android) + Dpr.bin
   9 player    Unity BdspWinBuild.WinBuild.AndroidBuildPlayerRelease (IL2CPP ARM64 APK without the data; data/ = APFS clones of the
               bundles + movies + the Wwise banks for setup/deploy_android.sh). bundles + player run with the ASTC copies swapped into
               Assets and the originals put back (and proven the same files) after the editor exits - ios_textures.py; any start of this
               script (and --back-to-mac) finishes an interrupted swap.
Every Unity step is guarded like the macOS ones: the Wwise / plug-in .meta files, ProjectSettings/*.asset and StartupSettings are
written back to their pre-step bytes after the editor exits (build_mac.snapshot / restore). Before the snapshot, ensure_android_defines
makes sure ProjectSettings.asset saves the Android group's defines (scriptingDefineSymbols 7), so they survive every step.

  python -I setup/stages/build_android.py [--from STEP] [--dev]
  python -I setup/stages/build_android.py --back-to-mac      active build target back to StandaloneOSX (for setup/build_mac.sh)
"""
import argparse
import glob
import json
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "lib"))
sys.path.insert(0, HERE)
import paths  # noqa: E402
import unity  # noqa: E402
import build_mac  # noqa: E402  (snapshot / restore / step / run - nothing runs on import)
import ios_textures  # noqa: E402

STEPS = ["toolchain", "inputs", "offline", "compile", "switch", "check", "textures", "bundles", "player"]
SOURCE_STEPS = STEPS + ["package"]
ANDROID_DEFINE_KEY = "7"                 # BuildTargetGroup.Android in ProjectSettings.asset scriptingDefineSymbols (2019.4 layout)
ANDROID_DEFINES = "PC_UNLOCKED_FPS;BDSP_UNITY6_GATE"   # = BdspWinBuild.Android.cs AndroidDefines
SDK = os.path.expanduser(os.environ.get("BDSP_ANDROID_SDK") or "~/Library/Android/sdk")
JDK = os.environ.get("BDSP_ANDROID_JDK") or "/opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home"
KEYSTORE = paths.work("build", "android", "bdsp-debug.keystore")
KEY = "bdspdebug"
TEXTURES_NATIVE = os.environ.get("BDSP_ANDROID_TEXTURES", "").strip() == "native"


def editor_root():
    exe = paths.need(paths.CFG.get("unity"), "unity")                    # <root>/Unity.app/Contents/MacOS/Unity
    return os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(exe))))


def ndk():
    d = os.path.expanduser(os.environ.get("BDSP_ANDROID_NDK") or "")
    if d:
        return d
    root = os.path.join(SDK, "ndk")
    pinned = os.path.join(root, "27.2.12479018")
    if os.path.isdir(pinned):
        return pinned
    found = sorted(glob.glob(os.path.join(root, "*")))
    return found[-1] if found else pinned


def target_sdk():
    forced = os.environ.get("BDSP_ANDROID_TARGET_SDK", "").strip()
    if forced:
        return int(forced)
    for suffix in ("", ".0"):      # exact "android-N" folders first; the newer SDK layout's "android-N.0" only when none exists
        for n in range(37, 33, -1):
            if os.path.isdir(os.path.join(SDK, "platforms", "android-%d%s" % (n, suffix))):
                return n
    return 34


def toolchain():
    engine = os.path.join(editor_root(), "PlaybackEngines", "AndroidPlayer")
    for need in ("UnityEditor.Android.Extensions.dll", "AndroidPlayerBuildProgram.exe", "Variations/il2cpp/Release/Libs/arm64-v8a/libunity.so",
                 "Variations/il2cpp/Development/Libs/arm64-v8a/libunity.so"):
        if not os.path.exists(os.path.join(engine, need)):
            sys.exit("toolchain: no Android Build Support at %s (missing %s)\n"
                     "  The editor loads its modules from <editor>/PlaybackEngines (next to Unity.app; iOSSupport / MacStandaloneSupport are there).\n"
                     "  Install the Android module for the editor (Unity Hub > Installs > Add modules, or its UnitySetup-Android-Support-for-Editor-<ver>.pkg\n"
                     "  extracted like the iOS one - see build_ios.py's toolchain) and run this again." % (engine, need))
    print("toolchain: Android Build Support %s (il2cpp ARM64)" % engine, flush=True)
    for need in ("platform-tools/adb", "build-tools", "platforms"):
        if not os.path.exists(os.path.join(SDK, need)):
            sys.exit("toolchain: the Android SDK %s is missing %s (BDSP_ANDROID_SDK overrides the location)" % (SDK, need))
    sdk_t = target_sdk()
    if not any(os.path.isdir(os.path.join(SDK, "platforms", "android-%s" % v)) for v in (sdk_t, "%d.0" % sdk_t)):
        sys.exit("toolchain: the Android SDK has no platform android-%d (targetSdk; BDSP_ANDROID_TARGET_SDK overrides)" % sdk_t)
    print("toolchain: SDK %s, build-tools %s, targetSdk %d" % (SDK, ", ".join(sorted(os.listdir(os.path.join(SDK, "build-tools")))), sdk_t), flush=True)
    n = ndk()
    if not os.path.exists(os.path.join(n, "source.properties")):
        sys.exit("toolchain: no Android NDK at %s (IL2CPP needs it; BDSP_ANDROID_NDK overrides)" % n)
    print("toolchain: NDK %s" % n, flush=True)
    java = os.path.join(JDK, "bin", "java")
    if not os.path.exists(java):
        sys.exit("toolchain: no JDK 17 at %s (brew install openjdk@17; BDSP_ANDROID_JDK overrides)" % JDK)
    ver = subprocess.run([java, "-version"], capture_output=True, text=True)
    first = (ver.stderr or ver.stdout).splitlines()[0] if (ver.stderr or ver.stdout) else "?"
    if '"17.' not in first:
        sys.exit("toolchain: %s is not JDK 17 (%s) - Unity 6 Android needs 17" % (JDK, first))
    print("toolchain: JDK %s (%s)" % (JDK, first), flush=True)
    if os.environ.get("BDSP_NO_SIGN") != "1" and not os.path.exists(KEYSTORE):
        r = subprocess.run([os.path.join(JDK, "bin", "keytool"), "-genkeypair", "-keystore", KEYSTORE, "-storetype", "JKS", "-storepass", KEY,
                            "-keypass", KEY, "-alias", KEY, "-keyalg", "RSA", "-keysize", "2048", "-validity", "10000",
                            "-dname", "CN=BDSP Android Debug,O=OpenDPR,C=US"], capture_output=True, text=True)
        if r.returncode or not os.path.exists(KEYSTORE):
            sys.exit("toolchain: keytool failed:\n" + r.stdout + r.stderr)
    if os.environ.get("BDSP_NO_SIGN") != "1":
        print("toolchain: local debug keystore ready", flush=True)
    wwise = [sys.executable, "-I", os.path.join(HERE, "wwise_android.py")]
    if subprocess.run(wwise + ["--check"], stdin=subprocess.DEVNULL).returncode:
        build_mac.run("wwise_android.py")


def inputs():
    need = {"manifest": paths.work("build", "dpr_manifest_shipped.tsv"), "plan": paths.work("build", "bundle_plan.tsv"),
            "variants": paths.work("build", "shipped_shader_variants.tsv")}
    for name, f in need.items():
        if os.path.exists(f):
            print("inputs: %s present (%s)" % (name, f), flush=True)
        else:
            print("inputs: %s missing - making it" % name, flush=True)
            build_mac.step(name)


def latest_apk():
    apks = sorted(glob.glob(os.path.join(paths.PROJECT, "Builds", "android_*", "Dpr.apk")), key=os.path.getmtime)
    return apks[-1] if apks else None


def ensure_android_defines():
    """BuildTargetGroup.Android (key 7) defines saved in ProjectSettings.asset: the editor compiles with the SAVED defines of the active
    group when it starts and when it switches - before any step code runs. Android's had only BDSP_UNITY6_GATE: PC_UNLOCKED_FPS is added,
    or PcFrameClock (the unlimited-frame-rate layer) compiles out of the Android player. Written before the step's snapshot, so the guard
    keeps it. Only the Android line is touched."""
    path = os.path.join(paths.PROJECT, "ProjectSettings", "ProjectSettings.asset")
    text = open(path, encoding="utf-8", newline="").read()
    m = re.search(r"^  scriptingDefineSymbols:(?: \{\})?\n((?:    \S+: .*\n)*)", text, re.M)
    if not m:
        sys.exit("ensure_android_defines: no scriptingDefineSymbols block in " + path)
    entries = dict(l.strip().split(": ", 1) for l in m.group(1).splitlines())
    key = "Android" if any(not k.isdigit() for k in entries) else ANDROID_DEFINE_KEY   # Unity 6 re-saves it with group names
    if entries.get(key) == ANDROID_DEFINES:
        return
    entries[key] = ANDROID_DEFINES
    keys = sorted(entries, key=lambda k: (not k.isdigit(), int(k) if k.isdigit() else 0, k))
    block = "  scriptingDefineSymbols:\n" + "".join("    %s: %s\n" % (k, entries[k]) for k in keys)
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text[:m.start()] + block + text[m.end():])
    print("ensure_android_defines: ProjectSettings.asset scriptingDefineSymbols %s: %s (saved)" % (key, ANDROID_DEFINES), flush=True)


def step(name, dev):
    if name == "toolchain":
        return toolchain()
    if name == "inputs":
        return inputs()
    if name == "offline":
        return build_mac.run("android_compile_check.py")
    if name == "textures" and TEXTURES_NATIVE:
        return print("build_android: BDSP_ANDROID_TEXTURES=native - no ASTC copies", flush=True)
    method, tag = {"switch": ("AndroidSwitch", "android_switch"), "compile": ("AndroidCompileScripts", "android_compile"),
                   "check": ("AndroidCheck", "android_check"), "textures": ("AndroidConvertTextures", "android_textures"),
                   "bundles": ("AndroidBuildBundlesFull", "android_bundles_full"),
                   "player": ("AndroidBuildPlayerDev", "android_player_dev") if dev else
                             ("AndroidBuildPlayerRelease", "android_player_release"),
                   "back": ("AndroidSwitchBackToMac", "android_switch_back_mac")}[name]
    exports_before = set(glob.glob(os.path.join(paths.PROJECT, "Builds", "android_*", "Gradle", "settings.gradle")))
    ensure_android_defines()
    swap = name in ("bundles", "player") and not TEXTURES_NATIVE   # the Android data is written from the ASTC copies; the Mac's DXT/BC files go back after
    if swap:
        ios_textures.swap_in()
    try:
        snap = build_mac.snapshot()
        ok = unity.run("BdspWinBuild.WinBuild." + method, "BDSP_WINBUILD_DONE %s OK" % tag, min_disk=8.0, min_commit=1.0)
        build_mac.restore(snap, name)
    finally:
        if swap:
            ios_textures.restore()
    if not ok:
        sys.exit("BdspWinBuild.%s failed - report in %s" % (method, paths.work("build", "reports", "")))
    if name == "player" and os.environ.get("BDSP_NO_SIGN") == "1":
        exports = set(glob.glob(os.path.join(paths.PROJECT, "Builds", "android_*", "Gradle", "settings.gradle"))) - exports_before
        if len(exports) != 1:
            sys.exit("Expected one new successful Gradle export; refusing ambiguous package selection")
        from pathlib import Path
        import hashlib
        manifest = Path(exports.pop())
        receipt = {"export": str(manifest.parent), "settings_sha256": hashlib.sha256(manifest.read_bytes()).hexdigest()}
        receipt_path = Path(paths.work("build", "android-export.json"))
        tmp = receipt_path.with_suffix('.tmp')
        tmp.write_text(json.dumps(receipt, indent=2) + '\n')
        os.replace(tmp, receipt_path)


def package_export():
    """Use the exact successful export receipt, never the newest golden build."""
    from pathlib import Path
    import hashlib
    import time
    import android_package
    receipt = Path(paths.work("build", "android-export.json"))
    if not receipt.is_file():
        sys.exit("No clean-source Android export receipt; complete android.player first")
    data = json.loads(receipt.read_text())
    source = Path(data['export'])
    if hashlib.sha256((source / 'settings.gradle').read_bytes()).hexdigest() != data['settings_sha256']:
        sys.exit("Export settings changed since player completion; rerun android.player")
    output = Path(paths.PROJECT) / 'Builds' / ('android_package_' + str(time.time_ns()))
    android_package.package(source, output, Path(SDK), Path(JDK),
                            Path(editor_root()) / 'PlaybackEngines/AndroidPlayer/Tools/gradle')


def main():
    if sys.platform != "darwin":
        sys.exit("build_android.py is written for the macOS build machine")
    ap = argparse.ArgumentParser()
    ap.add_argument("--from", dest="start", choices=STEPS)
    ap.add_argument("--dev", action="store_true", help="a development APK (Builds/android_<time>_dev, IL2CPP Debug)")
    ap.add_argument("--back-to-mac", action="store_true", help="only switch the active build target back to StandaloneOSX")
    ap.add_argument("--only", choices=SOURCE_STEPS, help="run exactly one real stage (launcher owns resume receipts)")
    ap.add_argument("--source-workflow", action="store_true", help="clean launcher: no Bee precheck, no signing, Android Gradle export")
    a = ap.parse_args()
    if a.source_workflow and not a.only:
        sys.exit("--source-workflow requires --only; use setup/launch.sh run for resumable orchestration")
    if a.source_workflow:
        os.environ["BDSP_NO_SIGN"] = "1"
        if a.only == "offline":
            sys.exit("source workflow uses the real Unity compile stage; offline requires a previous Bee cache")
    if a.only == 'package':
        if not a.source_workflow:
            sys.exit('package requires --source-workflow; never signs')
        package_export()
        return
    ios_textures.restore()                       # an interrupted swap (killed run): the originals back before anything else
    if a.back_to_mac:
        step("back", a.dev)
        print("build_android: active build target is StandaloneOSX again (setup/build_mac.sh can run)")
        return
    build_mac.run("dotween.py", "--check")
    build_mac.run("wwise.py", "--patch-only", "--unity6")
    if a.only:
        step(a.only, a.dev)
        print("android stage " + a.only + " completed")
        return
    state_path = paths.work("build", "steps_android6.json")
    done = json.load(open(state_path, encoding="utf-8")) if os.path.exists(state_path) else []
    if a.start:
        done = done[:STEPS.index(a.start)]
    elif "player" in done:
        print("build_android: the previous build finished - building again (bundles incrementally, the APK into a new folder)", flush=True)
        done = done[:STEPS.index("check")]
    for name in STEPS:
        if name in done:
            print("build_android: %s already done" % name, flush=True)
            continue
        print("build_android: %s" % name, flush=True)
        step(name, a.dev)
        done.append(name)
        with open(state_path, "w", encoding="utf-8") as f:
            json.dump(done, f)
    print("build_android OK: %s\n"
          "  Install + push the data + launch on the Pixel: sh setup/deploy_android.sh\n"
          "  Back to the Mac build: sh setup/build_android.sh --back-to-mac" % (latest_apk() or "(no Dpr.apk found under Builds)"))


if __name__ == "__main__":
    main()
