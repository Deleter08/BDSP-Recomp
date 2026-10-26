#!/usr/bin/env python3
"""Offline player-script compile check for Android - needs NO Unity editor run and NO Android build support installed.

Same approach as ios_compile_check.py (which this reuses: the csc command line Unity's Bee recorded for the last macOS PLAYER script
compile, run again with the editor's own Roslyn, Assembly-CSharp-firstpass first, then Assembly-CSharp against it), with the macOS
platform defines swapped for what an Android IL2CPP player defines (UNITY_ANDROID, PLATFORM_ANDROID, ENABLE_IL2CPP ...) and the
project's Android group defines (BdspWinBuild.AndroidDefines: PC_UNLOCKED_FPS, BDSP_UNITY6_GATE). The engine references stay the macOS
player's: Unity's UnityEngine.*Module reference assemblies are the same API for every player; the package assemblies are taken as
compiled for macOS. Android-only code (`#if UNITY_ANDROID`, the Generated/Android Wwise bindings, PcSwkbdAndroid) is therefore
compiled against the macOS UnityEngine surface: AndroidJNI etc. live in UnityEngine.AndroidJNIModule, which the recorded compile
already references.

  python -I setup/stages/android_compile_check.py            Android defines; exit 0 = 0 `error CS`
  python -I setup/stages/android_compile_check.py --platform mac   the recorded macOS defines unchanged - must give 0 errors (proves
                                                                   the harness before an Android result is believed)
Output: <work>/build/android_compile_offline/<platform>/{*.rsp, *.dll, csc_*.txt}.
"""
import argparse
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "lib"))
sys.path.insert(0, HERE)
import paths  # noqa: E402
import ios_compile_check as icc  # noqa: E402  (nothing runs on import)

# what an Android IL2CPP player defines instead of the macOS player's (Unity 6000.6 PlayerSettings > Android)
ANDROID_ADD = ["UNITY_ANDROID", "PLATFORM_ANDROID", "UNITY_ANDROID_API", "ENABLE_IL2CPP", "PLATFORM_SUPPORTS_ADS_ID",
               "SUPPORT_MULTIPLE_DISPLAYS", "PLAYERCONNECTION_LISTENS_FIXED_PORT", "DEBUGGER_LISTENS_FIXED_PORT"]
ANDROID_PROJECT = ["PC_UNLOCKED_FPS", "BDSP_UNITY6_GATE"]      # = BdspWinBuild.Android.cs AndroidDefines
# the recorded compile may be the last iOS player's (Library/Bee keeps the newest P.dag whatever the target): its iOS defines go too
IOS_ONLY = {"UNITY_IOS", "UNITY_IPHONE", "PLATFORM_IOS", "PLATFORM_IPHONE", "UNITY_IPHONE_API", "ENABLE_IOS_ON_DEMAND_RESOURCES",
            "ENABLE_IOS_APP_SLICING", "PC_UNLOCKED_FPS", "BDSP_UNITY6_GATE"}


def recorded_dag(platform):
    """The newest recorded player compile; for --platform mac one whose defines really are the macOS player's."""
    import glob
    dags = [d for d in glob.glob(os.path.join(paths.PROJECT, "Library", "Bee", "artifacts", "*P.dag"))
            if os.path.exists(os.path.join(d, "Assembly-CSharp.rsp"))]
    if platform == "mac":
        dags = [d for d in dags if "-define:UNITY_STANDALONE_OSX" in open(os.path.join(d, "Assembly-CSharp.rsp"), encoding="utf-8").read()]
        if not dags:
            sys.exit("no recorded macOS player compile (Library/Bee/artifacts/*P.dag with UNITY_STANDALONE_OSX): build the Mac player once")
    if not dags:
        sys.exit("no recorded player compile under Library/Bee/artifacts")
    return max(dags, key=lambda d: os.path.getmtime(os.path.join(d, "Assembly-CSharp.rsp")))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--platform", choices=["android", "mac"], default="android")
    a = ap.parse_args()
    # ios_compile_check.rewrite() swaps defines for platform == "ios" from these two module-level lists: point them at Android's
    icc.IOS_ADD = ANDROID_ADD
    icc.IOS_PROJECT = ANDROID_PROJECT
    icc.MAC_ONLY = set(icc.MAC_ONLY) | IOS_ONLY            # rewrite() drops these from the recorded defines before adding Android's
    plat = "ios" if a.platform == "android" else "mac"
    dag = recorded_dag(a.platform)
    out_dir = os.path.dirname(paths.work("build", "android_compile_offline", a.platform, "x"))
    print("android_compile_check: %s defines, recorded compile %s" % (a.platform, os.path.relpath(dag, paths.PROJECT)), flush=True)
    total = 0
    firstpass_ref = None
    for name in ("Assembly-CSharp-firstpass", "Assembly-CSharp"):
        rsp, dropped, added, defines = icc.rewrite(os.path.join(dag, name + ".rsp"), out_dir, name, plat, firstpass_ref)
        rc, errors, warnings, log = icc.compile_one(rsp, out_dir, name)
        shown = [d for d in defines if d.startswith(("UNITY_IOS", "UNITY_STANDALONE", "UNITY_ANDROID", "ENABLE_IL2CPP", "ENABLE_MONO"))]
        print("  %s: exit %d, %d error CS, %d warnings (%s); sources -%d deleted +%d new%s; platform defines %s" % (
            name, rc, len(errors), warnings, os.path.relpath(log, paths.PROJECT), dropped, len(added),
            "".join("\n      + " + x for x in added), ",".join(shown)), flush=True)
        for e in errors[:80]:
            print("    " + e.replace(paths.PROJECT + "/", ""), flush=True)
        if len(errors) > 80:
            print("    ... %d more in %s" % (len(errors) - 80, log), flush=True)
        total += len(errors) + (1 if rc and not errors else 0)
        firstpass_ref = os.path.join(out_dir, "Assembly-CSharp-firstpass.ref.dll")
        if rc:
            break
    print("android_compile_check %s: %s" % (a.platform, "OK (0 error CS)" if total == 0 else "FAILED"), flush=True)
    sys.exit(0 if total == 0 else 1)


if __name__ == "__main__":
    main()
