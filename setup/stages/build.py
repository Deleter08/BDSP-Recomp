#!/usr/bin/env python3
"""Optional stage build: a standalone Windows game (setup.cmd -Build) - Builds/win64_<time>/Dpr.exe.

Sub-steps, each recorded in <work>/build/steps.json (an interrupted run resumes at the one that did not finish):
   1 prereq    Unity's Windows IL2CPP module + Visual Studio's C++ tools (IL2CPP compiles the game to native code)
   2 manifest  setup/bin/DprManifestDump.exe: the dump's Dpr.bin (shipped AssetAssistant manifest) -> dpr_manifest_shipped.tsv
   3 plan      winbuild_plan.py: the 13,651-bundle Windows build plan, named exactly like the shipped bundles
   4 variants  shader_variants_shipped.py: the shader variants the shipped shaders bundle carries
   5 check     Unity BdspWinBuild.WinBuild.Check (plan vs project, no build)
   6 bundles   Unity BdspWinBuild.WinBuild.BuildBundlesFull (about 40 min, ~7 GB)
   7 player    Unity BdspWinBuild.WinBuild.BuildPlayerRelease (IL2CPP release player, bundles hard-linked)
Inputs and reports live in <work>/build; the output in <project>/Builds (BdspWinBuild reads BDSP_SETUP_WORK / BDSP_DUMP_DATA).
Every run after a finished build is a REBUILD (setup.cmd -Build after a git pull): all steps again, the bundles incrementally
(Unity rewrites only the bundles whose content changed), the player into a new Builds/win64_<time> folder. A player folder
hard-links the bundle files, so before the bundles step every bundle file an earlier player still shares gets its own copy - the
earlier player keeps exactly the files it was built with.

  python -I setup/stages/build.py [--from STEP]
"""
import argparse
import glob
import json
import os
import shutil
import subprocess
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402
import unity  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
STEPS = ["prereq", "manifest", "plan", "variants", "check", "bundles", "player"]
VSWHERE = os.path.join(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)"), "Microsoft Visual Studio", "Installer",
                       "vswhere.exe")


def run(script, *args):
    print(">> " + " ".join([script] + list(args)), flush=True)
    if subprocess.run([sys.executable, "-I", os.path.join(HERE, script)] + list(args), stdin=subprocess.DEVNULL).returncode:
        sys.exit("%s failed" % script)


def prereq():
    unity_exe = paths.need(paths.CFG.get("unity"), "unity")
    il2cpp = os.path.join(os.path.dirname(unity_exe), "Data", "PlaybackEngines", "windowsstandalonesupport", "Variations",
                          "win64_nondevelopment_il2cpp")
    bad = []
    if not os.path.isdir(il2cpp):
        bad.append("Unity's 'Windows Build Support (IL2CPP)' module is not installed (Unity Hub > Installs > 2019.4.27f1 > "
                   "Add modules)")
    vc = ""
    if os.path.isfile(VSWHERE):
        vc = subprocess.run([VSWHERE, "-products", "*", "-requires", "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
                             "-property", "installationPath"], capture_output=True, text=True).stdout.strip()
    if not vc:
        bad.append("Visual Studio 2022 (Build Tools or Community) with 'Desktop development with C++' is not installed")
    for b in bad:
        print("PROBLEM " + b)
    if bad:
        sys.exit(1)
    print("prerequisites OK (IL2CPP module; C++ tools: %s)" % vc.splitlines()[0], flush=True)


def unshare_bundles():
    """Give every bundle file that an earlier player folder hard-links its own copy (BdspWinBuild.LinkLayout links them; the
    incremental bundle build rewrites a changed bundle in place, which would change it inside the earlier player too)."""
    shared, size = [], 0
    for d in ("bundles_win64", "bundles_win64_side"):
        for dp, dn, fn in os.walk(os.path.join(paths.PROJECT, "Builds", d)):
            for f in fn:
                p = os.path.join(dp, f)
                st = os.stat(p)
                if st.st_nlink > 1:
                    shared.append(p)
                    size += st.st_size
    if not shared:
        print("bundles: no bundle file is shared with an earlier player", flush=True)
        return
    free = shutil.disk_usage(paths.PROJECT).free
    if free - size < 8 * 2 ** 30:
        sys.exit("an earlier player in Builds\\ shares %d bundle files (%.1f GB); keeping its own copies needs more free space than "
                 "the drive has - delete the Builds\\win64_* folders you no longer need, then run setup.cmd -Build again"
                 % (len(shared), size / 2 ** 30))
    for p in shared:
        tmp = p + ".unshare"
        shutil.copy2(p, tmp)
        os.replace(tmp, p)                   # the earlier player's link keeps the old file
    print("bundles: %d files (%.1f GB) shared with an earlier player now have their own copy" % (len(shared), size / 2 ** 30),
          flush=True)


def step(name):
    if name == "prereq":
        prereq()
    elif name == "manifest":
        dpr = os.path.join(paths.need(paths.AA, "dump"), "Dpr.bin")
        out = paths.work("build", "dpr_manifest_shipped.tsv")
        print(">> DprManifestDump.exe", flush=True)
        if subprocess.run([os.path.join(paths.SETUP, "bin", "DprManifestDump.exe"), dpr, out]).returncode:
            sys.exit("DprManifestDump failed (it needs the .NET Framework 4 that ships with Windows 10/11)")
    elif name == "plan":
        run("winbuild_plan.py")
    elif name == "variants":
        run("shader_variants_shipped.py")
    else:
        if name == "bundles":
            unshare_bundles()
        method, tag = {"check": ("Check", "check"), "bundles": ("BuildBundlesFull", "bundles_full"),
                       "player": ("BuildPlayerRelease", "player_release")}[name]
        if not unity.run("BdspWinBuild.WinBuild." + method, "BDSP_WINBUILD_DONE %s OK" % tag, min_disk=8.0):
            sys.exit("BdspWinBuild.%s failed - report in %s" % (method, paths.work("build", "reports", "")))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--from", dest="start", choices=STEPS)
    a = ap.parse_args()
    state_path = paths.work("build", "steps.json")
    done = json.load(open(state_path, encoding="utf-8")) if os.path.exists(state_path) else []
    if a.start:
        done = done[:STEPS.index(a.start)]
    elif "player" in done:
        print("build: the previous build finished - building again (bundles incrementally, the player into a new folder)",
              flush=True)
        done = []
    for name in STEPS:
        if name in done:
            print("build: %s already done" % name, flush=True)
            continue
        print("build: %s" % name, flush=True)
        step(name)
        done.append(name)
        with open(state_path, "w", encoding="utf-8") as f:
            json.dump(done, f)
    players = sorted(glob.glob(os.path.join(paths.PROJECT, "Builds", "win64_*", "Dpr.exe")), key=os.path.getmtime)
    print("build OK: %s" % (players[-1] if players else "(no Dpr.exe found under Builds)"))


if __name__ == "__main__":
    main()
