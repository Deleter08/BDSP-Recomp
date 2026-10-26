"""D7 (author-approved 2026-10-11, HANDOFF §11.10 PM2 / §11.15): map every project .png texture to the shipped Texture2D it was
exported from, and measure what the native rebuild costs on disk.

Pairing: PNG stem == shipped m_Name (AssetRipper appends _N to duplicate names - stripped when the full stem has no match), same
width x height. Several shipped candidates (the same texture packed into several bundles) are fine when their raw data is identical
(sha1 of image data); otherwise the PNG decides (decoded shipped texels vs the PNG, smallest mean difference, recorded).
Route per texture:
  native  - the .png becomes a native Texture2D .asset with the shipped data (BC blocks as shipped, ASTC decoded to RGBA32 by
            Ryujinx's own decoder, every mip level), same GUID;
  png     - importer sprites (textureType 8: sprites live in the .meta) stay PNG: exact texels written into the PNG + uncompressed
            import - exact only when the shipped texture has 1 mip (reported otherwise).
Output: work/s34/d7_map.json, a summary with the disk estimate (YAML hex = 2 x data + Library artifact = 1 x data).
  py -3.13 -I tools/d7_map.py
"""
import collections
import hashlib
import json
import os
import re
import sys
import time

import numpy as np
import UnityPy
from PIL import Image

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402  (setup: dump / project / work folder from the setup config)
DATA = paths.data_path()
ASSETS = paths.ASSETS
INDEX = paths.work("d7", "texture_shipped_index.json")
OUT = paths.work("d7", "d7_map.json")

BC = {10: 0.5, 12: 1.0, 25: 1.0, 26: 0.5, 27: 1.0}            # bytes per texel (BC1/BC4 4 bpp, BC3/BC5/BC7 8 bpp)
ASTC = {48: (4, 4), 49: (5, 5), 50: (6, 6), 51: (8, 8), 52: (10, 10), 53: (12, 12)}
UNCOMPRESSED = {1: 1, 3: 3, 4: 4, 5: 4, 13: 2, 63: 1}


def mip_texels(w, h, mips):
    t = 0
    for _ in range(max(1, mips)):
        t += w * h
        w, h = max(1, w // 2), max(1, h // 2)
    return t


def native_bytes(r):
    f = r["format"]
    if f in BC:
        return int(mip_texels(r["width"], r["height"], r["mips"]) * BC[f])
    if f in ASTC:
        return mip_texels(r["width"], r["height"], r["mips"]) * 4
    if f in UNCOMPRESSED:
        return mip_texels(r["width"], r["height"], r["mips"]) * (4 if f == 3 else UNCOMPRESSED[f])  # RGB24 -> RGBA32 in Unity? keep 3
    return r.get("image_size") or 0


def main():
    index = json.load(open(INDEX, encoding="utf-8"))
    by_name = collections.defaultdict(list)
    for r in index:
        by_name[r["name"]].append(r)
    pngs = []
    for dp, _, fns in os.walk(ASSETS):
        for fn in fns:
            if fn.lower().endswith((".png", ".tga", ".jpg")) and os.path.exists(os.path.join(dp, fn + ".meta")):
                pngs.append(os.path.join(dp, fn))
    pngs.sort()
    stats = collections.Counter()
    fmt_count = collections.Counter()
    rows = []
    envs = {}
    hash_cache = {}

    def raw_hash(r):
        key = (r["file"], r["path_id"])
        if key not in hash_cache:
            env = envs.get(r["file"])
            if env is None:
                if len(envs) > 64:
                    envs.clear()
                env = envs[r["file"]] = UnityPy.load(os.path.join(DATA, r["file"]))
            obj = next(o for o in env.objects if o.path_id == r["path_id"])
            d = obj.read()
            hash_cache[key] = hashlib.sha1(bytes(d.image_data)).hexdigest()
        return hash_cache[key]

    t0 = time.time()
    for n, png in enumerate(pngs):
        rel = os.path.relpath(png, ASSETS).replace("\\", "/")
        stem = os.path.splitext(os.path.basename(png))[0]
        meta = open(png + ".meta", encoding="utf-8", errors="replace").read()
        guid = re.search(r"^guid: ([0-9a-f]{32})", meta, re.M).group(1)
        ttype = int(re.search(r"textureType: (-?\d+)", meta).group(1)) if re.search(r"textureType: (-?\d+)", meta) else 0
        with Image.open(png) as im:
            size = im.size
        cands = by_name.get(stem) or by_name.get(re.sub(r"_\d+$", "", stem)) or []
        cands = [c for c in cands if (c["width"], c["height"]) == size]
        row = {"png": rel, "guid": guid, "textureType": ttype, "size": list(size)}
        if not cands:
            stats["unmapped"] += 1
            row["route"] = "unmapped"
            rows.append(row)
            continue
        hashes = {raw_hash(c) for c in cands} if len(cands) > 1 else None
        if hashes is not None and len(hashes) > 1:
            # different textures share the name and size: let the PNG decide
            img = np.asarray(Image.open(png).convert("RGBA"), dtype=np.int16)
            best = None
            for c in cands:
                env = envs.get(c["file"]) or UnityPy.load(os.path.join(DATA, c["file"]))
                envs[c["file"]] = env
                d = next(o for o in env.objects if o.path_id == c["path_id"]).read()
                try:
                    dec = np.asarray(d.image.convert("RGBA"), dtype=np.int16)
                except Exception:
                    continue
                if dec.shape != img.shape:
                    continue
                score = float(np.abs(dec - img).mean())
                if best is None or score < best[0]:
                    best = (score, c)
            if best is None:
                stats["ambiguous"] += 1
                row["route"] = "ambiguous"
                rows.append(row)
                continue
            pick = best[1]
            row["pick_score"] = round(best[0], 3)
            stats["disambiguated"] += 1
        else:
            pick = cands[0]
        row.update({"file": pick["file"], "path_id": pick["path_id"], "format": pick["format"], "mips": pick["mips"],
                    "colorspace": pick["colorspace"], "filter": pick["filter"], "wrapU": pick["wrapU"], "wrapV": pick["wrapV"],
                    "aniso": pick["aniso"], "mipbias": pick["mipbias"], "native_bytes": native_bytes(pick)})
        fmt_count[pick["format"]] += 1
        if ttype == 8:
            row["route"] = "png" if pick["mips"] <= 1 else "png_mipped"
        else:
            row["route"] = "native"
        stats[row["route"]] += 1
        rows.append(row)
        if n % 2000 == 0:
            print(f"{n}/{len(pngs)} {dict(stats)} {time.time() - t0:.0f} s", flush=True)
    json.dump(rows, open(OUT, "w", encoding="utf-8"))
    native = [r for r in rows if r.get("route") == "native"]
    data = sum(r["native_bytes"] for r in native)
    print("routes:", dict(stats))
    print("shipped formats of mapped textures:", dict(sorted(fmt_count.items())))
    print(f"native rebuild: {len(native)} textures, data {data / 2**30:.2f} GiB -> YAML ~{2 * data / 2**30:.2f} GiB + Library ~{data / 2**30:.2f} GiB")
    print("->", OUT)
    return 0


if __name__ == "__main__":
    sys.exit(main())
