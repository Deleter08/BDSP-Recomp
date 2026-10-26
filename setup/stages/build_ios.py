#!/usr/bin/env python3
"""Optional stage build_ios: an iOS Xcode project of the game (sh setup/build_ios.sh) - Builds/ios_<time>/Xcode + data/.

Sub-steps, each recorded in <work>/build/steps_ios.json (an interrupted run resumes at the one that did not finish):
   1 toolchain the editor's iOS Build Support (<editor>/PlaybackEngines/iOSSupport, next to Unity.app) and Xcode's iphoneos SDK.
               Missing iOS support = the exact package + install command are printed and the run stops (nothing is downloaded here).
               Then wwise_ios.py: the Wwise 2019.2.15 iOS engine (libAkSoundEngine.a) + its bindings, installed when missing/stale
               from the Launcher's Unity.iOS.tar.xz (BDSP_WWISE_IOS / its --source; the iOS scripts do not compile without them).
   2 inputs    the platform-neutral inputs the macOS build made (dpr_manifest_shipped.tsv, bundle_plan.tsv,
               shipped_shader_variants.tsv): made again only when one is missing (build_mac.py's manifest / plan / variants)
   3 offline   ios_compile_check.py: the player scripts compiled with the iOS defines by the editor's Roslyn, no editor run (seconds)
   4 compile   Unity BdspWinBuild.WinBuild.IosCompileScripts: the player scripts compiled by Unity for iOS (UNITY_IOS, no editor) -
               it does not need the active target, so a code error stops the run before the long switch
   5 switch    Unity BdspWinBuild.WinBuild.IosSwitch: active build target -> iOS. Assets re-import for iOS the first time; the macOS
               import results stay in Library (asset pipeline v2). setup/build_mac.sh needs the target back: --back-to-mac.
   6 check     Unity BdspWinBuild.WinBuild.IosCheck
   7 textures  Unity BdspWinBuild.WinBuild.IosConvertTextures: an ASTC copy of every DXT/BC native texture .asset (iOS refuses those;
               Unity does not re-encode NativeFormat textures) -> <work>/build/ios_textures (Assets/ is only read). Incremental.
   8 bundles   Unity BdspWinBuild.WinBuild.IosBuildBundlesFull (iOS bundles -> Builds/bundles_ios) + Dpr.bin
   9 player    Unity BdspWinBuild.WinBuild.IosBuildPlayerRelease (IL2CPP Xcode project Builds/ios_<time>/Xcode; data/ = APFS clones
               of the bundles + movies, copied once into the app's Documents; BDSP_IOS_DATA=app puts them in the app instead)
               bundles + player run with the ASTC copies swapped into Assets and the originals put back (and proven the same files)
               after the editor exits - ios_textures.py; any start of this script (and --back-to-mac) finishes an interrupted swap.
  10 xcode     xcodebuild of that project for a generic iOS device, UNSIGNED (CODE_SIGNING_ALLOWED=NO): proves the IL2CPP C++, the
               plug-ins and the link. No Apple ID is involved; signing + installing is the user's step in Xcode (see the end of the run).
     ipa       (clean launcher only) the app of that exact build zipped as Payload/<app> into Builds/ios_package_<time>/Dpr-unsigned.ipa,
               after checking it is arm64, carries no signature/provisioning and links the Wwise engine. An unsigned IPA is a signing
               input, not an installable package. With --source-workflow, player/xcode/ipa follow receipts of the exact export/app
               they made (never the newest folder under Builds).
Every Unity step is guarded like the macOS ones: the Wwise / plug-in .meta files, ProjectSettings/*.asset and StartupSettings are
written back to their pre-step bytes after the editor exits (build_mac.snapshot / restore). Before the snapshot, ensure_ios_defines
makes sure ProjectSettings.asset saves the iOS group's defines (scriptingDefineSymbols 4:), so they survive every step.

  python -I setup/stages/build_ios.py [--from STEP] [--dev]
  python -I setup/stages/build_ios.py --back-to-mac      active build target back to StandaloneOSX (for setup/build_mac.sh)
Environment (optional): BDSP_IOS_BUNDLE_ID (default com.opendpr.bdsp - a free Personal Team needs one nobody registered),
BDSP_IOS_TEAM_ID (pre-selects the team in Xcode), BDSP_IOS_DATA=app (data inside the .app).
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
import build_mac  # noqa: E402  (snapshot / restore / step - nothing runs on import)
import ios_textures  # noqa: E402

STEPS = ["toolchain", "inputs", "offline", "compile", "switch", "check", "textures", "bundles", "player", "xcode"]
SOURCE_STEPS = STEPS + ["ipa"]
IOS_DEFINE_KEY = "4"                  # BuildTargetGroup.iOS in ProjectSettings.asset scriptingDefineSymbols (2019.4 layout)
IOS_DEFINES = "PC_UNLOCKED_FPS;BDSP_UNITY6_GATE"   # = BdspWinBuild.iOS.cs IosDefines
CHANGESET = "d20c9ac777da"            # Unity 6000.6.6f1 (the editor's modules.json lists the same download URLs)


def editor_root():
    exe = paths.need(paths.CFG.get("unity"), "unity")                    # <root>/Unity.app/Contents/MacOS/Unity
    return os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(exe))))


def editor_version():
    m = re.search(r"/(\d+\.\d+\.\d+[abfp]\d+)/", paths.CFG.get("unity", "") + "/")
    return m.group(1) if m else "6000.6.6f1"


def toolchain():
    engines = os.path.join(editor_root(), "PlaybackEngines")
    ios = os.path.join(engines, "iOSSupport")
    if not os.path.exists(os.path.join(ios, "iOSPlayerBuildProgram.exe")):
        v = editor_version()
        pkg = "UnitySetup-iOS-Support-for-Editor-%s.pkg" % v
        url = "https://download.unity3d.com/download_unity/%s/MacEditorTargetInstaller/%s" % (CHANGESET, pkg)
        # measured 2026-10-24 (HTTP range reads of the pkg): 872,709,892 bytes; PackageInfo install-location
        # /Applications/Unity/Unity-<v>/PlaybackEngines/ (so `installer` would miss this editor); Payload = gzip cpio rooted at
        # ./iOSSupport/ (6,131 files, 2.76 GB), in the component TargetSupport.pkg.tmp. Extracting the payload into <editor>/PlaybackEngines is what Unity Hub does.
        sys.exit("toolchain: no iOS Build Support at %s\n"
                 "  The editor loads its modules from <editor>/PlaybackEngines (next to Unity.app; MacStandaloneSupport is there).\n"
                 "  Install (~830 MB download, 2.8 GB on disk; quit Unity first):\n"
                 "    mkdir -p /tmp/ios_pkg && cd /tmp/ios_pkg && curl -fL -o %s '%s'\n"
                 "    pkgutil --expand-full %s x\n"
                 "    ditto \"$(find x -type d -path '*/Payload/iOSSupport' -prune | head -1)\" '%s'\n"
                 "  then run this again." % (ios, pkg, url, pkg, ios))
    print("toolchain: iOS Build Support %s" % ios, flush=True)
    r = subprocess.run(["xcodebuild", "-version"], capture_output=True, text=True)
    if r.returncode:
        sys.exit("toolchain: xcodebuild failed (is Xcode selected? sudo xcode-select -s /Applications/Xcode.app):\n" + r.stdout + r.stderr)
    sdk = subprocess.run(["xcrun", "--sdk", "iphoneos", "--show-sdk-version"], capture_output=True, text=True)
    if sdk.returncode:
        sys.exit("toolchain: Xcode has no iphoneos SDK (Xcode > Settings > Components):\n" + sdk.stderr)
    print("toolchain: %s, iphoneos SDK %s" % (" ".join(r.stdout.split()[:2]), sdk.stdout.strip()), flush=True)
    wwise = [sys.executable, "-I", os.path.join(HERE, "wwise_ios.py")]
    if subprocess.run(wwise + ["--check"], stdin=subprocess.DEVNULL).returncode:
        build_mac.run("wwise_ios.py")


def inputs():
    need = {"manifest": paths.work("build", "dpr_manifest_shipped.tsv"), "plan": paths.work("build", "bundle_plan.tsv"),
            "variants": paths.work("build", "shipped_shader_variants.tsv")}
    for name, f in need.items():
        if os.path.exists(f):
            print("inputs: %s present (%s)" % (name, f), flush=True)
        else:
            print("inputs: %s missing - making it" % name, flush=True)
            build_mac.step(name)


def latest_xcode():
    projs = sorted(glob.glob(os.path.join(paths.PROJECT, "Builds", "ios_*", "Xcode", "Unity-iPhone.xcodeproj")), key=os.path.getmtime)
    return os.path.dirname(projs[-1]) if projs else None


def source_workflow():
    return os.environ.get("BDSP_NO_SIGN") == "1"


def write_receipt(name, value):
    path = paths.work("build", name)
    tmp = path + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(value, f, indent=2)
    os.replace(tmp, path)


def sha256(path):
    import hashlib
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for b in iter(lambda: f.read(1 << 22), b""):
            h.update(b)
    return h.hexdigest()


def receipted_xcode():
    """The Xcode export the source-workflow player step made (pbxproj unchanged since)."""
    path = paths.work("build", "ios-export.json")
    if not os.path.isfile(path):
        sys.exit("xcode: no clean-source iOS export receipt; complete ios.player first")
    data = json.load(open(path, encoding="utf-8"))
    pbx = os.path.join(data["export"], "Unity-iPhone.xcodeproj", "project.pbxproj")
    if not os.path.isfile(pbx) or sha256(pbx) != data["pbxproj_sha256"]:
        sys.exit("xcode: the receipted export changed since ios.player; rerun the player stage")
    return data["export"]


def xcode(dev):
    proj = receipted_xcode() if source_workflow() else latest_xcode()
    if not proj:
        sys.exit("xcode: no Builds/ios_*/Xcode/Unity-iPhone.xcodeproj - build the player first")
    derived = paths.work("build", "ios_derived", "x")
    derived = os.path.dirname(derived)
    log = paths.work("logs", "xcodebuild_ios.log")
    cmd = ["xcodebuild", "-project", os.path.join(proj, "Unity-iPhone.xcodeproj"), "-scheme", "Unity-iPhone",
           "-configuration", "Debug" if dev else "Release", "-sdk", "iphoneos", "-destination", "generic/platform=iOS",
           "-derivedDataPath", derived, "CODE_SIGNING_ALLOWED=NO", "CODE_SIGNING_REQUIRED=NO", "CODE_SIGN_IDENTITY=", "build"]
    print(">> " + " ".join(cmd) + "\n   (log %s; the IL2CPP C++ takes a while)" % log, flush=True)
    with open(log, "w", encoding="utf-8") as f:
        rc = subprocess.run(cmd, stdout=f, stderr=subprocess.STDOUT, stdin=subprocess.DEVNULL).returncode
    text = open(log, encoding="utf-8", errors="replace").read()
    errors = sorted({l.strip() for l in text.splitlines() if re.search(r"(^|\s)error:", l)})
    apps = glob.glob(os.path.join(derived, "Build", "Products", "*-iphoneos", "*.app"))
    print("xcode: exit %d, %d distinct error lines, BUILD %s; app %s" % (
        rc, len(errors), "SUCCEEDED" if "** BUILD SUCCEEDED **" in text else "FAILED", apps[0] if apps else "-"), flush=True)
    for e in errors[:40]:
        print("  " + e, flush=True)
    if rc or "** BUILD SUCCEEDED **" not in text:
        sys.exit("xcodebuild failed - see " + log)
    if source_workflow():
        if len(apps) != 1:
            sys.exit("xcode: expected exactly one built .app under %s, found %d" % (derived, len(apps)))
        write_receipt("ios-app.json", {"project": proj, "app": apps[0], "log": log, "configuration": "Debug" if dev else "Release"})


def package_ipa():
    """Zip the receipted unsigned app as Payload/<name>.app; verify architecture, absence of signing and the Wwise link."""
    import plistlib
    import time
    import zipfile
    path = paths.work("build", "ios-app.json")
    if not os.path.isfile(path):
        sys.exit("ipa: no clean-source app receipt; complete ios.xcode first")
    data = json.load(open(path, encoding="utf-8"))
    app = data["app"]
    info = plistlib.load(open(os.path.join(app, "Info.plist"), "rb"))
    exe = os.path.join(app, info["CFBundleExecutable"])
    framework = os.path.join(app, "Frameworks", "UnityFramework.framework", "UnityFramework")
    checks = {}
    for label, binary in (("executable", exe), ("UnityFramework", framework)):
        archs = subprocess.run(["lipo", "-archs", binary], capture_output=True, text=True)
        if archs.returncode or archs.stdout.split() != ["arm64"]:
            sys.exit("ipa: %s is not arm64-only: %s" % (binary, (archs.stdout + archs.stderr).strip()))
        sig = subprocess.run(["codesign", "-dv", binary], capture_output=True, text=True)   # read-only inspection
        if sig.returncode == 0:
            sys.exit("ipa: %s carries a code signature; this stage only packages unsigned builds" % binary)
        checks[label] = {"archs": "arm64", "codesign": (sig.stderr or sig.stdout).strip()}
    if os.path.exists(os.path.join(app, "embedded.mobileprovision")) or os.path.isdir(os.path.join(app, "_CodeSignature")):
        sys.exit("ipa: provisioning/signature material inside %s" % app)
    pbx = open(os.path.join(data["project"], "Unity-iPhone.xcodeproj", "project.pbxproj"), encoding="utf-8", errors="replace").read()
    if "libAkSoundEngine.a" not in pbx:
        sys.exit("ipa: the Xcode project does not link libAkSoundEngine.a (Wwise iOS engine)")
    nm = subprocess.run(["nm", "-gU", framework], stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
    wwise_symbols = len(re.findall(r" T _CSharp_\w+$", nm.stdout, re.M))
    log_link = "libAkSoundEngine" in open(data["log"], encoding="utf-8", errors="replace").read()
    if not wwise_symbols and not log_link:
        sys.exit("ipa: no evidence that the Wwise engine was linked (no CSharp_* exports, no libAkSoundEngine in the build log)")
    checks["wwise"] = {"pbxproj_links_libAkSoundEngine": True, "framework_CSharp_exports": wwise_symbols, "build_log_mentions_lib": log_link}
    out = os.path.join(paths.PROJECT, "Builds", "ios_package_%d" % time.time_ns())
    os.makedirs(out)                                   # new directory: never reuses or overwrites a previous package
    payload = os.path.join(out, "Payload")
    os.makedirs(payload)
    name = os.path.basename(app)
    for cmd in (["ditto", app, os.path.join(payload, name)],
                ["ditto", "-c", "-k", "--sequesterRsrc", "--keepParent", payload, os.path.join(out, "Dpr-unsigned.ipa")]):
        if subprocess.run(cmd, stdin=subprocess.DEVNULL).returncode:
            sys.exit("ipa: %s failed" % " ".join(cmd))
    ipa = os.path.join(out, "Dpr-unsigned.ipa")
    with zipfile.ZipFile(ipa) as z:
        bad = z.testzip()
        names = set(z.namelist())
    if bad or "Payload/%s/Info.plist" % name not in names or "Payload/%s/%s" % (name, info["CFBundleExecutable"]) not in names:
        sys.exit("ipa: archive verification failed (%s)" % (bad or "missing Payload entries"))
    receipt = {"ipa": ipa, "bytes": os.path.getsize(ipa), "sha256": sha256(ipa), "entries": len(names), "app": app,
               "bundle_id": info.get("CFBundleIdentifier"), "minimum_os": info.get("MinimumOSVersion"), "checks": checks,
               "signed": False, "note": "unsigned signing input; not installable until the owner signs/provisions it"}
    with open(os.path.join(out, "package.json"), "w", encoding="utf-8") as f:
        json.dump(receipt, f, indent=2)
    print("ipa: %s (%d bytes, sha256 %s, %d entries); %d CSharp_* exports in UnityFramework" % (
        ipa, receipt["bytes"], receipt["sha256"], len(names), wwise_symbols), flush=True)


def ensure_ios_defines():
    """BuildTargetGroup.iOS (key 4) defines saved in ProjectSettings.asset, as Standalone's (1) and Android's (7): the editor compiles
    with the SAVED defines of the active group when it starts and when it switches - before any step code runs - so with iOS active
    and no BDSP_UNITY6_GATE, MeshRendererDirect compiles the removed 2019.4 BRG API and every step dies with "Scripts have compiler
    errors". Written before the step's snapshot, so the guard keeps it. Only the key-4 line is touched."""
    path = os.path.join(paths.PROJECT, "ProjectSettings", "ProjectSettings.asset")
    text = open(path, encoding="utf-8", newline="").read()
    m = re.search(r"^  scriptingDefineSymbols:(?: \{\})?\n((?:    \S+: .*\n)*)", text, re.M)
    if not m:
        sys.exit("ensure_ios_defines: no scriptingDefineSymbols block in " + path)
    entries = dict(l.strip().split(": ", 1) for l in m.group(1).splitlines())
    key = "iPhone" if any(not k.isdigit() for k in entries) else IOS_DEFINE_KEY   # Unity 6 re-saves it with group names
    if entries.get(key) == IOS_DEFINES:
        return
    entries[key] = IOS_DEFINES
    keys = sorted(entries, key=lambda k: (not k.isdigit(), int(k) if k.isdigit() else 0, k))
    block = "  scriptingDefineSymbols:\n" + "".join("    %s: %s\n" % (k, entries[k]) for k in keys)
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text[:m.start()] + block + text[m.end():])
    print("ensure_ios_defines: ProjectSettings.asset scriptingDefineSymbols %s: %s (saved)" % (key, IOS_DEFINES), flush=True)


def step(name, dev):
    if name == "toolchain":
        return toolchain()
    if name == "inputs":
        return inputs()
    if name == "offline":
        return build_mac.run("ios_compile_check.py")
    if name == "xcode":
        return xcode(dev)
    method, tag = {"switch": ("IosSwitch", "ios_switch"), "compile": ("IosCompileScripts", "ios_compile"),
                   "check": ("IosCheck", "ios_check"), "textures": ("IosConvertTextures", "ios_textures"),
                   "bundles": ("IosBuildBundlesFull", "ios_bundles_full"),
                   "player": ("IosBuildPlayerDev", "ios_player_dev") if dev else ("IosBuildPlayerRelease", "ios_player_release"),
                   "back": ("IosSwitchBackToMac", "ios_switch_back_mac")}[name]
    ensure_ios_defines()
    exports_before = set(glob.glob(os.path.join(paths.PROJECT, "Builds", "ios_*", "Xcode", "Unity-iPhone.xcodeproj")))
    swap = name in ("bundles", "player")         # the iOS data is written from the ASTC copies; the Mac's DXT/BC files go back after
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
    if name == "player" and source_workflow():
        exports = set(glob.glob(os.path.join(paths.PROJECT, "Builds", "ios_*", "Xcode", "Unity-iPhone.xcodeproj"))) - exports_before
        if len(exports) != 1:
            sys.exit("Expected one new successful Xcode export; refusing ambiguous selection")
        export = os.path.dirname(exports.pop())
        write_receipt("ios-export.json", {"export": export, "pbxproj_sha256": sha256(
            os.path.join(export, "Unity-iPhone.xcodeproj", "project.pbxproj"))})


def main():
    if sys.platform != "darwin":
        sys.exit("build_ios.py needs macOS + Xcode")
    ap = argparse.ArgumentParser()
    ap.add_argument("--from", dest="start", choices=STEPS)
    ap.add_argument("--dev", action="store_true", help="a development build (Builds/ios_<time>_dev, IL2CPP Debug, Xcode Debug)")
    ap.add_argument("--back-to-mac", action="store_true", help="only switch the active build target back to StandaloneOSX")
    ap.add_argument("--only", choices=SOURCE_STEPS, help="run exactly one real stage (launcher owns resume receipts)")
    ap.add_argument("--source-workflow", action="store_true", help="clean launcher: no Bee precheck, no signing, receipted export/app/IPA")
    a = ap.parse_args()
    if a.source_workflow and not a.only:
        sys.exit("--source-workflow requires --only; use setup/launch.sh run for resumable orchestration")
    if a.source_workflow:
        os.environ["BDSP_NO_SIGN"] = "1"
        if a.only == "offline":
            sys.exit("source workflow uses the real Unity compile stage; offline requires a previous Bee cache")
    if a.only == "ipa":
        if not a.source_workflow:
            sys.exit("ipa requires --source-workflow; it packages only the receipted unsigned build")
        package_ipa()
        return
    ios_textures.restore()                       # an interrupted swap (killed run): the originals back before anything else
    if a.back_to_mac:
        step("back", a.dev)
        print("build_ios: active build target is StandaloneOSX again (setup/build_mac.sh can run)")
        return
    build_mac.run("dotween.py", "--check")
    build_mac.run("wwise.py", "--patch-only", "--unity6")
    if a.only:
        step(a.only, a.dev)
        print("ios stage " + a.only + " completed")
        return
    state_path = paths.work("build", "steps_ios.json")
    done = json.load(open(state_path, encoding="utf-8")) if os.path.exists(state_path) else []
    if a.start:
        done = done[:STEPS.index(a.start)]
    elif "xcode" in done:
        print("build_ios: the previous build finished - building again (bundles incrementally, the player into a new folder)", flush=True)
        done = done[:STEPS.index("check")]
    for name in STEPS:
        if name in done:
            print("build_ios: %s already done" % name, flush=True)
            continue
        print("build_ios: %s" % name, flush=True)
        step(name, a.dev)
        done.append(name)
        with open(state_path, "w", encoding="utf-8") as f:
            json.dump(done, f)
    proj = latest_xcode()
    print("build_ios OK: %s\n"
          "  Sign + install (yours): open %s/Unity-iPhone.xcodeproj; target Unity-iPhone > Signing & Capabilities > Team = your\n"
          "  (Personal) Team, a unique Bundle Identifier; iPhone: Settings > Privacy & Security > Developer Mode on; Product > Run.\n"
          "  Data: Finder > iPhone > Files > Dpr <- drag %s/StreamingAssets (once; re-installs keep it).\n"
          "  Back to the Mac build: sh setup/build_ios.sh --back-to-mac" % (
              proj or "(no Xcode project under Builds)", proj, os.path.join(os.path.dirname(proj), "data") if proj else "Builds/ios_<time>/data"))


if __name__ == "__main__":
    main()
