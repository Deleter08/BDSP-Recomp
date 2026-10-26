#!/usr/bin/env python3
"""Stage merge: MOVE the normalised export's asset folders into Assets/ (same drive - nothing is copied).

- every top-level entry of <work>/export/ExportedProject/Assets except Scripts/ (scripts come from git);
- an entry that already exists in Assets/ (StreamingAssets/ holds the tracked AK/ marker) is merged one level down;
- nothing in Assets/ is ever overwritten: any clash stops the stage before anything moves;
- then the sound banks: StreamingAssets/Audio/GeneratedSoundBanks/Switch -> .../Windows (the Switch-built banks play in the
  Windows Wwise runtime).

  python -I setup/stages/merge.py [--export <ExportedProject/Assets>]
"""
import argparse
import os
import shutil
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402

SKIP = {"Scripts"}
BANKS = os.path.join("StreamingAssets", "Audio", "GeneratedSoundBanks")


def plan(src_root, dst_root):
    moves, clashes = [], []
    for name in sorted(os.listdir(src_root)):
        if name in SKIP or name in ("Scripts.meta",):
            continue
        s, d = os.path.join(src_root, name), os.path.join(dst_root, name)
        if not os.path.exists(d):
            moves.append((s, d))
        elif os.path.isdir(s) and os.path.isdir(d):
            for sub in sorted(os.listdir(s)):
                ss, dd = os.path.join(s, sub), os.path.join(d, sub)
                if os.path.exists(dd):
                    clashes.append(dd)
                else:
                    moves.append((ss, dd))
        else:
            clashes.append(d)
    return moves, clashes


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--export", default=paths.EXPORT_ASSETS)
    a = ap.parse_args()
    src = a.export
    if not os.path.exists(os.path.join(os.path.dirname(os.path.dirname(src)), "NORMALISED")):
        sys.exit("the export is not normalised yet (stage normalise)")
    if not os.path.isdir(src):
        sys.exit("no export at " + src)
    moves, clashes = plan(src, paths.ASSETS)
    if clashes:
        for c in clashes[:20]:
            print("  already in Assets/: " + c)
        sys.exit("%d export entries would overwrite existing files in Assets/ - nothing moved" % len(clashes))
    if os.path.splitdrive(src)[0].lower() != os.path.splitdrive(paths.ASSETS)[0].lower():
        print("WARNING: the work folder is on another drive - the export is copied, which needs its size again in free space")
    for s, d in moves:
        shutil.move(s, d)
    print("moved %d entries into Assets/" % len(moves))
    sw, win = os.path.join(paths.ASSETS, BANKS, "Switch"), os.path.join(paths.ASSETS, BANKS, "Windows")
    if os.path.isdir(sw):
        if os.path.exists(win):
            sys.exit("both GeneratedSoundBanks/Switch and /Windows exist - resolve by hand")
        os.rename(sw, win)
        if os.path.exists(sw + ".meta"):
            os.rename(sw + ".meta", win + ".meta")
        print("sound banks: GeneratedSoundBanks/Switch -> Windows (%d files)" % len(os.listdir(win)))
    elif os.path.isdir(win):
        print("sound banks already in GeneratedSoundBanks/Windows")
    else:
        sys.exit("no GeneratedSoundBanks/Switch in the export")
    left = [n for n in os.listdir(src) if n not in SKIP and n != "Scripts.meta"]
    print("left in the export folder: %s" % (", ".join(left) or "nothing but Scripts/"))


if __name__ == "__main__":
    main()
