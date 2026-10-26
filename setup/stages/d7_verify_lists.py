"""D7 (HANDOFF §11.15): write the editor verification inputs for the rebuilt textures - work/s34/d7_verify.txt (every asset of the
manifest, or the last N) and work/s34/d7_verify_materials.txt (materials that reference their GUIDs, up to --mats).
  py -3.13 tools/d7_verify_lists.py [--last N] [--mats 40]
"""
import argparse
import glob
import json
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402  (setup: dump / project / work folder from the setup config)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--last", type=int, default=0)
    ap.add_argument("--mats", type=int, default=40)
    a = ap.parse_args()
    done = [json.loads(l) for l in open(paths.work("d7", "d7_manifest.jsonl"), encoding="utf-8")]
    done = [m for m in done if m.get("state") == "done"]
    if a.last:
        done = done[-a.last:]
    with open(paths.work("d7", "d7_verify.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join("Assets/" + m["asset"] for m in done) + "\n")
    guids = {m["guid"] for m in done}
    mats = []
    for path in glob.glob(os.path.join(paths.ASSETS, "**", "*.mat"), recursive=True):
        text = open(path, encoding="utf-8", errors="replace").read()
        if any(g in text for g in guids):
            mats.append(os.path.relpath(path, paths.PROJECT).replace(os.sep, "/"))
            if len(mats) >= a.mats:
                break
    with open(paths.work("d7", "d7_verify_materials.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(mats) + "\n")
    print(len(done), "assets,", len(mats), "materials")


if __name__ == "__main__":
    main()
