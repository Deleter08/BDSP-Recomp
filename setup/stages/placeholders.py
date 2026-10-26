#!/usr/bin/env python3
"""Stage placeholders: point AssetRipper's SpriteAtlas placeholders at the rebuilt atlases (was tools/phase4_settings.py
--placeholders).

AssetRipper exported no SpriteAtlas; the 16 references to them in 7 prefabs come out as
{fileID: 687078895, guid: 0000000deadbeef15deadf00d0000000, type: 3}. spriteatlas_spec.json ("fixes") lists each one (file, line,
field, atlas); it becomes {fileID: <the atlas's main object id>, guid: <the atlas .meta guid>, type: 2}. Runs after the atlases exist
and carry their pinned GUIDs. Fails if a listed line holds no placeholder and is not already pointing at its atlas.

  python -I setup/stages/placeholders.py [--spec <spriteatlas_spec.json>]
"""
import argparse
import json
import os
import re
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402

DEADBEEF = "{fileID: 687078895, guid: 0000000deadbeef15deadf00d0000000, type: 3}"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--spec", default=paths.work("atlas", "spriteatlas_spec.json"))
    a = ap.parse_args()
    spec = json.load(open(a.spec, encoding="utf-8"))
    by_file = {}
    for fx in spec["fixes"]:
        by_file.setdefault(fx["file"], []).append(fx)
    fixed = already = 0
    for rel, fixes in sorted(by_file.items()):
        full = os.path.join(paths.PROJECT, rel)
        text = open(full, encoding="utf-8", newline="").read()
        lines = text.split("\n")
        for fx in fixes:
            tgt = spec["atlases"][fx["atlas"]]["target"]
            tfull = os.path.join(paths.PROJECT, tgt)
            if not os.path.exists(tfull):
                sys.exit("%s: atlas %s not built" % (rel, tgt))
            guid = next(l[6:].strip() for l in open(tfull + ".meta", encoding="utf-8") if l.startswith("guid: "))
            anchor = next(re.match(r"^--- !u!687078895 &(-?\d+)", l).group(1)
                          for l in open(tfull, encoding="utf-8") if l.startswith("--- !u!687078895 &"))
            want = "{fileID: %s, guid: %s, type: 2}" % (anchor, guid)
            i = fx["line"] - 1
            if want in lines[i]:
                already += 1
                continue
            if DEADBEEF not in lines[i]:
                sys.exit("%s line %d: no SpriteAtlas placeholder (%s)" % (rel, fx["line"], lines[i].strip()[:120]))
            lines[i] = lines[i].replace(DEADBEEF, want)
            fixed += 1
        new = "\n".join(lines)
        if new != text:
            with open(full, "w", encoding="utf-8", newline="") as f:
                f.write(new)
    print("SpriteAtlas placeholders: %d fixed, %d already pointing at their atlas, in %d files" % (fixed, already, len(by_file)))


if __name__ == "__main__":
    main()
