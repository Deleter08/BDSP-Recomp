"""D7 (HANDOFF §11.15): re-pair textures whose rebuilt mip 0 did not verify (same name + size, different shipped content): try EVERY
shipped Texture2D with the same base name and size through tools/d7_texconv + the d7_build verifier, and write the candidate that
verifies exactly into work/s34/d7_map.json (old pick kept as "pick_was"). Then rerun tools/d7_build.py --apply.
  py -3.13 -I tools/d7_repair_pairs.py
"""
import json
import os
import re
import subprocess
import sys
import tempfile

import UnityPy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from d7_build import ASSETS, CONV, DATA, MANIFEST, MAP, verify_mip0  # noqa: E402

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402  (setup: dump / project / work folder from the setup config)
INDEX = paths.work("d7", "texture_shipped_index.json")


def main():
    last = {}
    for line in open(MANIFEST, encoding="utf-8"):
        m = json.loads(line)
        last[m["png"]] = m
    bad = [p for p, m in last.items() if m["state"] == "error"]
    rows = json.load(open(MAP, encoding="utf-8"))
    by_png = {r["png"]: r for r in rows}
    index = json.load(open(INDEX, encoding="utf-8"))
    tmp = tempfile.mkdtemp(prefix="d7rp_", dir=paths.work("d7", ""))
    fixed = 0
    for png in bad:
        r = by_png[png]
        stem = os.path.splitext(os.path.basename(png))[0]
        base = re.sub(r"_\d+$", "", stem)
        cands = [c for c in index if c["name"] in (stem, base) and [c["width"], c["height"]] == r["size"]]
        hit = None
        for c in cands:
            d = next(o for o in UnityPy.load(os.path.join(DATA, c["file"])).objects if o.path_id == c["path_id"]).read()
            rin, rout = os.path.join(tmp, "in.bin"), os.path.join(tmp, "out.bin")
            open(rin, "wb").write(bytes(d.get_image_data()))
            jf = os.path.join(tmp, "j.jsonl")
            open(jf, "w").write(json.dumps({"in": rin, "out": rout, "w": d.m_Width, "h": d.m_Height, "mips": d.m_MipCount,
                                            "format": int(d.m_TextureFormat), "gobs": 0}) + "\n")
            if os.path.exists(rout):
                os.remove(rout)
            subprocess.run(CONV + [jf], capture_output=True, text=True)
            if not os.path.exists(rout):
                continue
            why = verify_mip0(open(rout, "rb").read(), int(d.m_TextureFormat), d.m_Width, d.m_Height, os.path.join(ASSETS, png),
                              normal=r.get("textureType") == 1)
            if why is None:
                hit = c
                break
        if hit is None:
            print("NO EXACT CANDIDATE", png, len(cands), "candidates")
            continue
        r["pick_was"] = [r["file"], r["path_id"]]
        r.update({"file": hit["file"], "path_id": hit["path_id"], "format": hit["format"], "mips": hit["mips"],
                  "filter": hit["filter"], "wrapU": hit["wrapU"], "wrapV": hit["wrapV"], "aniso": hit["aniso"], "mipbias": hit["mipbias"]})
        fixed += 1
        print("re-paired", png, "->", hit["file"])
    json.dump(rows, open(MAP, "w", encoding="utf-8"))
    print("fixed", fixed, "of", len(bad))
    return 0


if __name__ == "__main__":
    sys.exit(main())
