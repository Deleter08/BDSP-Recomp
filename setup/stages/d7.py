#!/usr/bin/env python3
"""Stage d7: every texture holds the SHIPPED texels (author-approved D7; .tga / .jpg sources included - author's W4 call).

The reference order (HANDOFF §11.15 / work/setup/ASSET_PIPELINE_INVENTORY.md S21), each sub-step recorded in <work>/d7/steps.json so
an interrupted run resumes at the sub-step that did not finish:
   1 index      texture_shipped_index.py - every shipped Texture2D of the dump
   2 templates  Unity: BdspD7.WriteAllTemplates (Assets/_d7_template); ProjectSettings/EditorSettings.asset is saved before and
                written back byte for byte after (the template switches the serialization mode for one binary asset)
   3 map        d7_map.py - pair every exported texture with its shipped Texture2D
   4 build      d7_build.py --apply - native Texture2D .asset files (first pass: pairing failures are expected here)
   5 repair     d7_repair_pairs.py, then d7_build.py --apply again - must end with no errors
   6 cubemaps   cubemap_shipped_index.py + d7_cubemaps.py --apply
   7 png_route  d7_png_route.py --apply - sprite-owning PNGs keep PNG with the shipped sampler
   8 refs       d7_refs.py --apply - references to the converted textures (+ bundle_map.tsv paths)
   9 verify     d7_verify_lists.py, Unity BdspD7.Verify, d7_check_refs.py

  python -I setup/stages/d7.py [--from STEP]
"""
import argparse
import json
import os
import subprocess
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402
import unity  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
STEPS = ["index", "templates", "map", "build", "repair", "cubemaps", "png_route", "refs", "verify"]
EDITOR_SETTINGS = os.path.join(paths.PROJECT, "ProjectSettings", "EditorSettings.asset")
TEMPLATES = ["d7_template.asset", "d7_template_rgba.asset", "d7_template_bin.asset", "d7_template_cube.asset"]


def run(script, *args, allow_fail=False):
    cmd = [sys.executable, "-I", os.path.join(HERE, script)] + list(args)
    print(">> " + " ".join([script] + list(args)), flush=True)
    rc = subprocess.run(cmd, stdin=subprocess.DEVNULL).returncode
    if rc and not allow_fail:
        sys.exit("%s failed (exit %d)" % (script, rc))
    return rc


def templates():
    saved = open(EDITOR_SETTINGS, "rb").read()
    try:
        ok = unity.run("BdspD7.WriteAllTemplates", "BDSP_D7_TEMPLATES_DONE")
    finally:
        with open(EDITOR_SETTINGS, "wb") as f:
            f.write(saved)
        print("ProjectSettings/EditorSettings.asset written back", flush=True)
    missing = [t for t in TEMPLATES if not os.path.exists(os.path.join(paths.ASSETS, "_d7_template", t))]
    if not ok or missing:
        sys.exit("D7 templates failed (missing: %s)" % ", ".join(missing))


def step(name):
    if name == "index":
        run("texture_shipped_index.py")
    elif name == "templates":
        templates()
    elif name == "map":
        run("d7_map.py")
    elif name == "build":
        rc = run("d7_build.py", "--apply", allow_fail=True)
        if rc == 3:                                  # d7_build.DISK_STOP
            sys.exit("d7_build stopped on the disk floor - free some space on the project's drive and run the setup again")
        print("first build pass exit %d (pairing failures are repaired next)" % rc, flush=True)
    elif name == "repair":
        run("d7_repair_pairs.py", allow_fail=True)
        run("d7_build.py", "--apply")
    elif name == "cubemaps":
        run("cubemap_shipped_index.py")
        run("d7_cubemaps.py", "--apply")
    elif name == "png_route":
        run("d7_png_route.py", "--apply")
    elif name == "refs":
        run("d7_refs.py", "--apply")
    elif name == "verify":
        run("d7_verify_lists.py")
        if not unity.run("BdspD7.Verify", "BDSP_D7_VERIFY_DONE"):
            sys.exit("BdspD7.Verify failed")
        run("d7_check_refs.py")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--from", dest="start", choices=STEPS)
    a = ap.parse_args()
    state_path = paths.work("d7", "steps.json")
    done = json.load(open(state_path, encoding="utf-8")) if os.path.exists(state_path) else []
    if a.start:
        done = done[:STEPS.index(a.start)]
    for name in STEPS:
        if name in done:
            print("d7: %s already done" % name, flush=True)
            continue
        print("d7: %s" % name, flush=True)
        step(name)
        done.append(name)
        with open(state_path, "w", encoding="utf-8") as f:
            json.dump(done, f)
    print("d7 OK")


if __name__ == "__main__":
    main()
