#!/usr/bin/env python3
"""Offline player-script compile check for iOS - needs NO Unity editor run and NO iOS build support installed.

Unity's own iOS compile (BdspWinBuild.WinBuild.IosCompileScripts, step "compile" of build_ios.py) needs the iOS module. This check
runs earlier and cheaper: it takes the exact csc command line Unity's Bee recorded for the last macOS PLAYER script compile
(Library/Bee/artifacts/<id>P.dag/Assembly-CSharp[-firstpass].rsp), swaps the macOS platform defines for the iOS ones, and runs the
editor's own Roslyn on it (Assembly-CSharp-firstpass first, then Assembly-CSharp against it). The engine references stay the
macOS player's: Unity's UnityEngine.*Module reference assemblies are the same API for every player; the package assemblies
(TextMeshPro, Timeline, ...) are taken as compiled for macOS - none of their public API is platform-conditional.

  python -I setup/stages/ios_compile_check.py                 iOS defines (UNITY_IOS, IL2CPP, PC_UNLOCKED_FPS, BDSP_UNITY6_GATE)
  python -I setup/stages/ios_compile_check.py --platform mac  the recorded macOS defines unchanged - must give 0 errors (proves
                                                              the harness itself before an iOS result is believed)
  python -I setup/stages/ios_compile_check.py --editor       Assembly-CSharp-Editor as the editor last compiled it (editor defines),
                                                              with today's sources - BdspWinBuild.iOS.cs etc. compile in THIS editor
Output: <work>/build/ios_compile_offline/<platform|editor>/{*.rsp, *.dll, csc_*.txt}. Exit 0 = 0 `error CS`.
New .cs files in folders the recorded compile already had are added; deleted ones are dropped (both reported).
"""
import argparse
import glob
import os
import re
import subprocess
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402

# the macOS-player-only defines of the recorded compile, and what an iOS IL2CPP player defines instead
MAC_ONLY = {"PLATFORM_STANDALONE_OSX", "PLATFORM_STANDALONE", "UNITY_STANDALONE_OSX", "UNITY_STANDALONE", "ENABLE_MONO",
            "PLATFORM_SUPPORTS_MONO", "ENABLE_CLUSTER_SYNC", "ENABLE_CLUSTERINPUT", "RENDER_SOFTWARE_CURSOR",
            "PLATFORM_REQUIRES_PATH_NORMALIZATION", "ENABLE_SPATIALTRACKING",
            "UNITY_ANDROID", "PLATFORM_ANDROID", "UNITY_ANDROID_API"}
IOS_ADD = ["UNITY_IOS", "UNITY_IPHONE", "PLATFORM_IOS", "UNITY_IPHONE_API", "ENABLE_IL2CPP", "ENABLE_IOS_ON_DEMAND_RESOURCES",
           "ENABLE_IOS_APP_SLICING", "PLAYERCONNECTION_LISTENS_FIXED_PORT", "DEBUGGER_LISTENS_FIXED_PORT",
           "PLATFORM_SUPPORTS_ADS_ID", "SUPPORT_MULTIPLE_DISPLAYS"]
# BdspWinBuild.IosScriptingDefines: what the iOS build sets for BuildTargetGroup.iOS (the project has none for it)
IOS_PROJECT = ["PC_UNLOCKED_FPS", "BDSP_UNITY6_GATE"]


def latest_dag(kind="P", rsp="Assembly-CSharp.rsp", platform=None):
    """kind P = the last player script compile, E = the last editor script compile."""
    dags = [d for d in glob.glob(os.path.join(paths.PROJECT, "Library", "Bee", "artifacts", "*%s.dag" % kind))
            if os.path.exists(os.path.join(d, rsp))]
    if platform == "mac":
        dags = [d for d in dags if "-define:UNITY_STANDALONE_OSX" in
                open(os.path.join(d, rsp), encoding="utf-8").read().splitlines()]
    if not dags:
        sys.exit("no recorded compile (Library/Bee/artifacts/*%s.dag/%s) - %s once first" % (
            kind, rsp, "build the macOS player" if kind == "P" else "open the project in the editor"))
    return max(dags, key=lambda d: os.path.getmtime(os.path.join(d, rsp)))


def csc_command():
    contents = os.path.dirname(os.path.dirname(paths.need(paths.CFG.get("unity"), "unity")))
    dotnet = os.path.join(contents, "Resources", "Scripting", "DotNetSdk", "dotnet")
    cscs = sorted(glob.glob(os.path.join(contents, "Resources", "Scripting", "DotNetSdk", "sdk", "*", "Roslyn", "bincore", "csc.dll")))
    if not os.path.exists(dotnet) or not cscs:
        sys.exit("the editor's Roslyn is missing under " + contents)
    return [dotnet, "exec", cscs[-1], "/nostdlib", "/noconfig"]


def rewrite(rsp_path, out_dir, name, platform, firstpass_ref, editor=False):
    lines = open(rsp_path, encoding="utf-8").read().splitlines()
    sources = [l for l in lines if l.startswith('"Assets') or l.startswith('"Library/PackageCache')]
    rest = [l for l in lines if l not in sources]
    out, defines = [], []
    for l in rest:
        if l.startswith("-define:"):
            defines.append(l[len("-define:"):])
        elif l.startswith("-out:") or l.startswith("-refout:"):
            continue
        elif firstpass_ref and l.startswith("-r:") and "Assembly-CSharp-firstpass.ref.dll" in l:
            out.append('-r:"%s"' % firstpass_ref)
        else:
            out.append(l)
    if platform == "ios":
        defines = [d for d in defines if d not in MAC_ONLY] + IOS_ADD
        for d in IOS_PROJECT:
            if d not in defines:
                defines.append(d)
    out.append('-out:"%s"' % os.path.join(out_dir, name + ".dll"))
    out.append('-refout:"%s"' % os.path.join(out_dir, name + ".ref.dll"))
    out += ["-define:" + d for d in defines]

    # the recorded source list, minus deleted files, plus new .cs files in the folders it already compiled (not Editor/ ones) and in
    # new sibling folders of those (e.g. Wwise's Generated/iOS next to Generated/Mac - Unity compiles every such folder too)
    have = [s.strip('"') for s in sources]
    kept = [s for s in have if os.path.exists(os.path.join(paths.PROJECT, s))]
    dirs = {os.path.dirname(s) for s in kept if s.startswith("Assets/")}
    for parent in sorted({os.path.dirname(d) for d in dirs if d.count("/") >= 2}):
        for sub in sorted(glob.glob(os.path.join(paths.PROJECT, parent, "*", ""))):
            d = os.path.relpath(sub, paths.PROJECT)
            if d not in dirs and glob.glob(os.path.join(sub, "*.cs")) and not glob.glob(os.path.join(sub, "*.asmdef")) \
                    and (("/Editor/" in "/" + d + "/") == editor):
                dirs.add(d)
    known = set(kept)
    added = []
    for d in sorted(dirs):
        for f in sorted(glob.glob(os.path.join(paths.PROJECT, d, "*.cs"))):
            rel = os.path.relpath(f, paths.PROJECT)
            if rel not in known and (editor or "/Editor/" not in "/" + rel):
                added.append(rel)
                known.add(rel)
    out += ['"%s"' % s for s in kept + added]
    new_rsp = os.path.join(out_dir, name + ".rsp")
    with open(new_rsp, "w", encoding="utf-8") as f:
        f.write("\n".join(out) + "\n")
    return new_rsp, len(have) - len(kept), added, defines


def compile_one(rsp, out_dir, name):
    r = subprocess.run(csc_command() + ["@" + rsp], cwd=paths.PROJECT, capture_output=True, text=True)
    log = os.path.join(out_dir, "csc_%s.txt" % name)
    with open(log, "w", encoding="utf-8") as f:
        f.write(r.stdout + r.stderr)
    errors = sorted({l.strip() for l in (r.stdout + r.stderr).splitlines() if re.search(r"error CS\d+", l)})
    warnings = len([l for l in (r.stdout + r.stderr).splitlines() if re.search(r"warning CS\d+", l)])
    return r.returncode, errors, warnings, log


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--platform", choices=["ios", "mac"], default="ios")
    ap.add_argument("--editor", action="store_true", help="compile Assembly-CSharp-Editor (editor defines) instead of the player scripts")
    a = ap.parse_args()
    if a.editor:
        dag = latest_dag("E", "Assembly-CSharp-Editor.rsp")
        out_dir = os.path.dirname(paths.work("build", "ios_compile_offline", "editor", "x"))
        print("ios_compile_check: editor scripts, recorded compile %s" % os.path.relpath(dag, paths.PROJECT), flush=True)
        rsp, dropped, added, defines = rewrite(os.path.join(dag, "Assembly-CSharp-Editor.rsp"), out_dir, "Assembly-CSharp-Editor", "mac", None, True)
        rc, errors, warnings, log = compile_one(rsp, out_dir, "Assembly-CSharp-Editor")
        print("  Assembly-CSharp-Editor: exit %d, %d error CS, %d warnings (%s); sources -%d deleted +%d new%s" % (
            rc, len(errors), warnings, os.path.relpath(log, paths.PROJECT), dropped, len(added), "".join("\n      + " + x for x in added)), flush=True)
        for e in errors[:80]:
            print("    " + e.replace(paths.PROJECT + "/", ""), flush=True)
        ok = rc == 0 and not errors
        print("ios_compile_check editor: %s" % ("OK (0 error CS)" if ok else "FAILED"), flush=True)
        sys.exit(0 if ok else 1)
    dag = latest_dag(platform=a.platform)
    out_dir = paths.work("build", "ios_compile_offline", a.platform, "x")
    out_dir = os.path.dirname(out_dir)
    print("ios_compile_check: %s defines, recorded compile %s" % (a.platform, os.path.relpath(dag, paths.PROJECT)), flush=True)
    total = 0
    firstpass_ref = None
    for name in ("Assembly-CSharp-firstpass", "Assembly-CSharp"):
        rsp, dropped, added, defines = rewrite(os.path.join(dag, name + ".rsp"), out_dir, name, a.platform, firstpass_ref)
        rc, errors, warnings, log = compile_one(rsp, out_dir, name)
        plat = [d for d in defines if d.startswith(("UNITY_IOS", "UNITY_STANDALONE", "UNITY_ANDROID", "ENABLE_IL2CPP", "ENABLE_MONO"))]
        print("  %s: exit %d, %d error CS, %d warnings (%s); sources -%d deleted +%d new%s; platform defines %s" % (
            name, rc, len(errors), warnings, os.path.relpath(log, paths.PROJECT), dropped, len(added),
            "".join("\n      + " + x for x in added), ",".join(plat)), flush=True)
        for e in errors[:80]:
            print("    " + e.replace(paths.PROJECT + "/", ""), flush=True)
        if len(errors) > 80:
            print("    ... %d more in %s" % (len(errors) - 80, log), flush=True)
        total += len(errors) + (1 if rc and not errors else 0)
        firstpass_ref = os.path.join(out_dir, "Assembly-CSharp-firstpass.ref.dll")
        if rc:
            break
    print("ios_compile_check %s: %s" % (a.platform, "OK (0 error CS)" if total == 0 else "FAILED"), flush=True)
    sys.exit(0 if total == 0 else 1)


if __name__ == "__main__":
    main()
