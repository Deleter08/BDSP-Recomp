"""D7 (HANDOFF §11.15), route "png": textures whose .meta defines SPRITES (textureType 8) stay PNG files - converting them to native
assets would drop the importer-made Sprite sub-objects every prefab references. All 2,630 are single-mip on the Switch, so an
UNCOMPRESSED import of a PNG that equals the shipped texels is exact:
  shipped RGBA32 / RGB24 -> the raw shipped data must equal the PNG (exact);
  shipped ASTC           -> Ryujinx's decode (tools/d7_texconv) must equal the PNG (exact; the emulator decodes ASTC on the CPU);
  shipped DXT1 / DXT5    -> texture2ddecoder's decode must equal the PNG; the emulator lets the GPU decode BC, which may round
                            differently by 1 -> recorded as "near-exact (BC decoded on CPU)".
Importer change (backup work/s34/d7_png_backup/<rel>.meta): Standalone override RGBA32 (RGB24 when the PNG is opaque) uncompressed,
DefaultTexturePlatform textureCompression 0, enableMipMap 0, filter / wrap / aniso / mip bias from the shipped Texture2D.
  py -3.13 -I tools/d7_png_route.py [--apply]
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

import numpy as np
import texture2ddecoder
import UnityPy
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from d7_build import ASTC, CONV  # noqa: E402

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402  (setup: dump / project / work folder from the setup config)
DATA = paths.data_path()
ASSETS = paths.ASSETS
MAP = paths.work("d7", "d7_map.json")
BACKUP = paths.work("d7", "d7_png_backup")
REPORT = paths.work("d7", "d7_png_route_report.txt")

STANDALONE = """  - serializedVersion: 3
    buildTarget: Standalone
    maxTextureSize: {maxsize}
    resizeAlgorithm: 0
    textureFormat: {fmt}
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 1
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
"""


def set_key(text, key, value, indent="    "):
    return re.sub(r"^(%s%s:) .*$" % (re.escape(indent), re.escape(key)), lambda m: m.group(1) + " " + str(value), text, count=1, flags=re.M)


def shipped_rgba(d, tmp):
    """The texels the reference samples, as an RGBA array in Unity row order (bottom-up)."""
    w, h, fmt = d.m_Width, d.m_Height, int(d.m_TextureFormat)
    raw = bytes(d.get_image_data())
    if fmt == 4:
        return np.frombuffer(raw[:w * h * 4], np.uint8).reshape(h, w, 4), "exact"
    if fmt == 3:
        a = np.frombuffer(raw[:w * h * 3], np.uint8).reshape(h, w, 3)
        return np.concatenate([a, np.full((h, w, 1), 255, np.uint8)], axis=2), "exact"
    if fmt in ASTC:
        rin, rout = os.path.join(tmp, "in.bin"), os.path.join(tmp, "out.bin")
        open(rin, "wb").write(raw)
        jf = os.path.join(tmp, "j.jsonl")
        open(jf, "w").write(json.dumps({"in": rin, "out": rout, "w": w, "h": h, "mips": 1, "format": fmt, "gobs": 0}) + "\n")
        subprocess.run(CONV + [jf], capture_output=True, text=True)
        return np.frombuffer(open(rout, "rb").read()[:w * h * 4], np.uint8).reshape(h, w, 4), "exact"
    bw, bh = (w + 3) // 4, (h + 3) // 4
    if fmt == 10:
        dec = texture2ddecoder.decode_bc1(raw[:bw * bh * 8], w, h)
    elif fmt == 12:
        dec = texture2ddecoder.decode_bc3(raw[:bw * bh * 16], w, h)
    else:
        return None, "unsupported format %d" % fmt
    return np.frombuffer(dec, np.uint8).reshape(h, w, 4)[:, :, [2, 1, 0, 3]], "near-exact (BC decoded on CPU)"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    a = ap.parse_args()
    rows = [r for r in json.load(open(MAP, encoding="utf-8")) if r.get("route") == "png"]
    index = json.load(open(paths.work("d7", "texture_shipped_index.json"), encoding="utf-8"))
    by_name = {}
    for c in index:
        by_name.setdefault(c["name"], []).append(c)
    rows.sort(key=lambda r: r["file"])
    tmp = tempfile.mkdtemp(prefix="d7png_", dir=paths.work("d7", ""))
    env_file, env = None, None
    lines, stats = [], {}
    for r in rows:
        png = os.path.join(ASSETS, r["png"])
        if not os.path.exists(png):
            continue
        if r["file"] != env_file:
            env_file, env = r["file"], UnityPy.load(os.path.join(DATA, r["file"]))
        d = next(o for o in env.objects if o.path_id == r["path_id"]).read()
        ship, cls = shipped_rgba(d, tmp)
        if ship is None:
            stats["skip"] = stats.get("skip", 0) + 1
            lines.append(f"SKIP   {r['png']} {cls}")
            continue
        ref = np.asarray(Image.open(png).convert("RGBA"))[::-1]
        if ref.shape != ship.shape or not np.array_equal(ref, ship):
            # same name + size exist for several textures (language variants): try every candidate for an exact match
            stem = os.path.splitext(os.path.basename(png))[0]
            for c in by_name.get(stem, []) + by_name.get(re.sub(r"_\d+$", "", stem), []):
                if [c["width"], c["height"]] != r["size"] or (c["file"], c["path_id"]) == (r["file"], r["path_id"]):
                    continue
                d2 = next(o for o in UnityPy.load(os.path.join(DATA, c["file"])).objects if o.path_id == c["path_id"]).read()
                s2, cls2 = shipped_rgba(d2, tmp)
                if s2 is not None and s2.shape == ref.shape and np.array_equal(s2, ref):
                    ship, cls = s2, cls2
                    r.update({k: c[k] for k in ("file", "path_id", "format", "mips", "filter", "wrapU", "wrapV", "aniso", "mipbias")})
                    break
        if ref.shape != ship.shape or not np.array_equal(ref, ship):
            diff = int(np.abs(ref.astype(np.int16) - ship.astype(np.int16)).max()) if ref.shape == ship.shape else -1
            stats["mismatch"] = stats.get("mismatch", 0) + 1
            lines.append(f"DIFF   {r['png']} max {diff}")
            continue
        opaque = bool((ref[:, :, 3] == 255).all())
        fmt = 3 if opaque else 4
        meta = png + ".meta"
        text = open(meta, encoding="utf-8").read()
        new = text
        for key, val in (("enableMipMap", 0), ("filterMode", r["filter"]), ("aniso", r["aniso"]), ("mipBias", r["mipbias"]),
                         ("wrapU", r["wrapU"]), ("wrapV", r["wrapV"])):
            new = set_key(new, key, int(val) if float(val).is_integer() else val)
        new = re.sub(r"(buildTarget: DefaultTexturePlatform\n(?:    .*\n)*?    textureCompression:) \d+", r"\1 0", new, count=1)
        w, h = r["size"]
        maxsize = max(2048, 1 << (max(w, h) - 1).bit_length())
        block = STANDALONE.format(fmt=fmt, maxsize=maxsize)
        if "buildTarget: Standalone" in new:
            new = re.sub(r"  - serializedVersion: 3\n    buildTarget: Standalone\n(?:    .*\n)+?(?=  - serializedVersion|  spriteSheet:)", block, new, count=1)
        else:
            new = re.sub(r"(  platformSettings:\n(?:  - serializedVersion: 3\n(?:    .*\n)+?)+?)(  spriteSheet:)",
                         lambda m: m.group(1) + block + m.group(2), new, count=1)
        new = set_key(new, "maxTextureSize", maxsize, "  ")
        key = "set_" + cls.split(" ")[0]
        stats[key] = stats.get(key, 0) + 1
        lines.append(f"{cls[:10]:10s} {r['png']} shipped {r['format']} -> {fmt}")
        if a.apply and new != text:
            dst = os.path.join(BACKUP, r["png"] + ".meta")
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            if not os.path.exists(dst):
                shutil.copy2(meta, dst)
            open(meta, "w", encoding="utf-8", newline="").write(new)
    open(REPORT, "w", encoding="utf-8").write("\n".join(lines) + "\n" + json.dumps(stats) + "\n")
    print(stats, "->", REPORT, "(applied)" if a.apply else "(dry run)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
