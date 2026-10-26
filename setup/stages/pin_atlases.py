#!/usr/bin/env python3
"""Stage atlases, second half: give the 70 sprite atlases BdspAtlas.BuildAll just created this project's GUIDs.

Unity assigns a random GUID to every asset it creates; setup/data/atlas_guids.tsv holds the reference project's GUID per atlas path,
so every player's project stays identical to it (the placeholder references written next point at these). Only .meta guid lines
change; Unity re-imports the atlases on its next start. Fails if an atlas is missing or an unexpected one exists.

  python -I setup/stages/pin_atlases.py
"""
import os
import re
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402

GUID = re.compile(r"^guid: ([0-9a-f]{32})", re.M)


def main():
    rows = [l.rstrip("\n").split("\t") for l in open(os.path.join(paths.SETUP_DATA, "atlas_guids.tsv"), encoding="utf-8")][1:]
    want = {p: g for p, g in rows}
    found = []
    for dp, dn, fn in os.walk(paths.ASSETS):
        dn[:] = [d for d in dn if d not in ("StreamingAssets", "Scripts")]
        found += ["Assets/" + os.path.relpath(os.path.join(dp, f), paths.ASSETS).replace("\\", "/") for f in fn
                  if f.endswith(".spriteatlas")]
    extra, missing = sorted(set(found) - set(want)), sorted(set(want) - set(found))
    if extra or missing:
        for p in (missing + extra)[:20]:
            print("  %s %s" % ("missing" if p in missing else "unexpected", p))
        sys.exit("the built atlases differ from the reference set (%d missing, %d unexpected)" % (len(missing), len(extra)))
    changed = 0
    for p, g in want.items():
        meta = os.path.join(paths.PROJECT, *p.split("/")) + ".meta"
        text = open(meta, encoding="utf-8", newline="").read()
        m = GUID.search(text)
        if m.group(1) != g:
            with open(meta, "w", encoding="utf-8", newline="") as f:
                f.write(text[:m.start(1)] + g + text[m.end(1):])
            changed += 1
    print("atlases: %d, GUIDs pinned %d, already ours %d" % (len(want), changed, len(want) - changed))


if __name__ == "__main__":
    main()
