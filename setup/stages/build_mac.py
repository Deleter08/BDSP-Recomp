#!/usr/bin/env python3
"""Optional stage build_mac: a standalone macOS game (sh setup/build_mac.sh) - Builds/mac_<time>/Dpr.app (Mono player).

Sub-steps, each recorded in <work>/build/steps_mac.json (an interrupted run resumes at the one that did not finish):
   1 manifest  setup/bin/manifest_src/DprManifestDump.cs compiled with the Unity editor's own Mono (mcs) and run with it (the
               Windows DprManifestDump.exe needs .NET Framework): the dump's Dpr.bin -> dpr_manifest_shipped.tsv
   2 plan      winbuild_plan.py: the 13,651-bundle build plan, named exactly like the shipped bundles (platform-neutral)
   3 variants  shader_variants_shipped.py: the shader variants the shipped shaders bundle carries
   4 check     Unity BdspWinBuild.WinBuild.MacCheck (plan vs project, active target StandaloneOSX, no build)
   5 bundles   Unity BdspWinBuild.WinBuild.MacBuildBundlesFull (StandaloneOSX bundles -> Builds/bundles_mac, ~7 GB) + Dpr.bin
   6 player    Unity BdspWinBuild.WinBuild.MacBuildPlayerRelease (Mono player; bundles APFS-cloned into the .app)
   7 plugins   the Wwise DSP plug-ins the banks use (Mac PluginInfo.xml) copied into <app>/Contents/Plugins, where the player's
               Wwise looks for them: Wwise's own activator selects none (its platform list comes from the Wwise project, which
               this repository does not have), so the build alone ships without them; then the .app is ad-hoc signed again
No IL2CPP / Xcode / Visual Studio is involved. The player step restores every Wwise / plug-in .meta, StartupSettings and
ProjectSettings it touches (see BdspWinBuild.BuildPlayerMac). A run after a finished build is a REBUILD: bundles incrementally,
the player into a new Builds/mac_<time> folder (earlier players keep their own cloned bundle files).

  python -I setup/stages/build_mac.py [--from STEP]
"""
import argparse
import glob
import json
import os
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402
import unity  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
STEPS = ["manifest", "plan", "variants", "check", "bundles", "player", "plugins"]


def run(script, *args):
    print(">> " + " ".join([script] + list(args)), flush=True)
    if subprocess.run([sys.executable, "-I", os.path.join(HERE, script)] + list(args), stdin=subprocess.DEVNULL).returncode:
        sys.exit("%s failed" % script)


def unity_mono():
    """(mono, mcs.exe) of the Unity 2019.4 editor (config "unity_mono"): Unity.app/Contents/MonoBleedingEdge. Unity 6.6 (config
    "unity") has no Contents/MonoBleedingEdge; DprManifestDump is an offline tool (not shipped), run x86_64 under Rosetta 2."""
    exe = paths.need(paths.CFG.get("unity_mono") or paths.CFG.get("unity"), "unity_mono")   # .../Unity.app/Contents/MacOS/Unity
    mbe = os.path.join(os.path.dirname(os.path.dirname(exe)), "MonoBleedingEdge")
    mono, mcs = os.path.join(mbe, "bin", "mono"), os.path.join(mbe, "lib", "mono", "4.5", "mcs.exe")
    for p in (mono, mcs):
        if not os.path.exists(p):
            sys.exit("the Unity editor's Mono is missing: %s" % p)
    return mono, mcs


def manifest():
    mono, mcs = unity_mono()
    arch = ["arch", "-x86_64"]                                           # the 2019.4 editor's Mono is x86_64 (Rosetta 2)
    exe = paths.work("build", "tools", "DprManifestDump.exe")
    os.makedirs(os.path.dirname(exe), exist_ok=True)
    src = os.path.join(paths.SETUP, "bin", "manifest_src", "DprManifestDump.cs")
    print(">> mcs DprManifestDump.cs", flush=True)
    if subprocess.run(arch + [mono, mcs, "-nologo", "-out:" + exe, src]).returncode:
        sys.exit("compiling DprManifestDump.cs with the Unity editor's Mono failed")
    dpr = os.path.join(paths.need(paths.AA, "dump"), "Dpr.bin")
    out = paths.work("build", "dpr_manifest_shipped.tsv")
    print(">> DprManifestDump.exe (Unity Mono)", flush=True)
    if subprocess.run(arch + [mono, exe, dpr, out]).returncode:
        sys.exit("DprManifestDump failed on " + dpr)


def latest_app():
    apps = sorted(glob.glob(os.path.join(paths.PROJECT, "Builds", "mac_*", "Dpr.app")), key=os.path.getmtime)
    return apps[-1] if apps else None


def dsp_plugins():
    app = latest_app()
    if not app:
        sys.exit("plugins: no Builds/mac_*/Dpr.app - build the player first")
    info = os.path.join(paths.ASSETS, "StreamingAssets", "Audio", "GeneratedSoundBanks", "Mac", "PluginInfo.xml")
    dsp = os.path.join(paths.ASSETS, "Wwise", "Deployment", "Plugins", "Mac", "DSP")
    dst = os.path.join(app, "Contents", "Plugins")
    if not os.path.isdir(dst):
        sys.exit("plugins: %s has no Contents/Plugins" % app)
    dlls = sorted({p.get("DLL") for p in ET.parse(info).getroot().iter("Plugin") if p.get("DLL")})
    copied, builtin = [], []
    for dll in dlls:
        src = os.path.join(dsp, "lib%s.bundle" % dll)
        if not os.path.isfile(src):
            builtin.append(dll)              # in AkSoundEngine itself (Parametric EQ, Meter, the sinks)
            continue
        shutil.copy2(src, os.path.join(dst, os.path.basename(src)))
        copied.append(os.path.basename(src))
    print("plugins: banks use %s; copied into %s: %s; built into the sound engine: %s"
          % (", ".join(dlls), dst, ", ".join(copied) or "-", ", ".join(builtin) or "-"), flush=True)
    r = subprocess.run(["codesign", "--force", "--deep", "--sign", "-", app], capture_output=True, text=True)
    print("plugins: ad-hoc re-sign %s" % ("OK" if r.returncode == 0 else "FAILED (%s) - the app still runs unsigned under Rosetta"
                                         % r.stderr.strip()), flush=True)


def guarded_files():
    """The files a Unity build step must leave exactly as they were: every Wwise / plug-in importer .meta (the editor's working
    audio depends on them), ProjectSettings/*.asset and StartupSettings. BdspWinBuild restores them inside the editor; this is
    the second line, applied after the editor has EXITED (nothing can save over it then)."""
    out = []
    for d in (os.path.join(paths.ASSETS, "Wwise"), os.path.join(paths.ASSETS, "Plugins")):
        for dp, dn, fn in os.walk(d):
            out += [os.path.join(dp, f) for f in fn if f.endswith(".meta")]
    ps = os.path.join(paths.PROJECT, "ProjectSettings")
    out += [os.path.join(ps, f) for f in os.listdir(ps) if f.endswith(".asset")]
    out.append(os.path.join(paths.ASSETS, "Resources", "StartupSettings.asset"))
    return out


def snapshot():
    return {p: open(p, "rb").read() for p in guarded_files()}


def restore(snap, name):
    changed = [p for p, b in snap.items() if not os.path.exists(p) or open(p, "rb").read() != b]
    for p in changed:
        with open(p, "wb") as f:
            f.write(snap[p])
    added = sorted(set(guarded_files()) - set(snap))
    print("guard after %s: %d of %d files written back to their pre-step bytes%s%s" % (
        name, len(changed), len(snap), "".join("\n  restored " + os.path.relpath(p, paths.PROJECT) for p in changed),
        "".join("\n  NEW (left in place) " + os.path.relpath(p, paths.PROJECT) for p in added)), flush=True)


def step(name):
    if name == "plugins":
        dsp_plugins()
    elif name == "manifest":
        manifest()
    elif name == "plan":
        run("winbuild_plan.py")
    elif name == "variants":
        run("shader_variants_shipped.py")
    else:
        method, tag = {"check": ("MacCheck", "mac_check"), "bundles": ("MacBuildBundlesFull", "mac_bundles_full"),
                       "player": ("MacBuildPlayerRelease", "mac_player_release")}[name]
        snap = snapshot()
        # macOS reports free + inactive RAM only (no page-file counter) - the Windows 3 GB floor would stop a healthy build
        ok = unity.run("BdspWinBuild.WinBuild." + method, "BDSP_WINBUILD_DONE %s OK" % tag, min_disk=8.0, min_commit=1.0)
        restore(snap, name)
        if not ok:
            sys.exit("BdspWinBuild.%s failed - report in %s" % (method, paths.work("build", "reports", "")))


def main():
    if sys.platform != "darwin":
        sys.exit("build_mac.py builds the macOS player - on Windows use setup.cmd -Build")
    ap = argparse.ArgumentParser()
    ap.add_argument("--from", dest="start", choices=STEPS)
    a = ap.parse_args()
    state_path = paths.work("build", "steps_mac.json")
    done = json.load(open(state_path, encoding="utf-8")) if os.path.exists(state_path) else []
    if a.start:
        done = done[:STEPS.index(a.start)]
    elif "player" in done:
        print("build_mac: the previous build finished - building again (bundles incrementally, the player into a new folder)",
              flush=True)
        done = []
    for name in STEPS:
        if name in done:
            print("build_mac: %s already done" % name, flush=True)
            continue
        print("build_mac: %s" % name, flush=True)
        step(name)
        done.append(name)
        with open(state_path, "w", encoding="utf-8") as f:
            json.dump(done, f)
    print("build_mac OK: %s" % (latest_app() or "(no Dpr.app found under Builds)"))


if __name__ == "__main__":
    main()
