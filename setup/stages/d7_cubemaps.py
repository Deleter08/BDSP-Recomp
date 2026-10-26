"""D7 (HANDOFF §11.15): the 10 reflection cubemaps (CubeSample, T_C_001_Reflection_03 + AssetRipper duplicates _0.._3) become NATIVE
Cubemap assets holding the shipped BC6H data (6 faces x 10 mips, linear as stored - Unity 2019.4 Switch bundles are not swizzled;
BC6H is sampled natively by both D3D11 and Ryubing's GL backend). The PNG strips (512x3072, textureShape 2) move to
work/s34/d7_png_aside like the 2D textures; the .asset keeps the PNG's GUID with main fileID 8900000 (a Cube-shaped PNG's main
object is a Cubemap); references `{fileID: 8900000, guid: G, type: 3}` -> type 2 (tools/d7_refs.py --cubemaps).
Pairing: name (minus _N) + the PNG strip vs the shipped faces decoded by texture2ddecoder (BC6H -> 8 bit), smallest mean difference
over the six faces (the PNG is AssetRipper's 8-bit rendering of HDR data, so the check is loose - reported).
Template: Assets/_d7_template/d7_template_cube.asset (BdspD7.WriteCubemapTemplateAndVerify, binary).
  py -3.13 -I tools/d7_cubemaps.py [--apply]
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import sys

import numpy as np
import texture2ddecoder
import UnityPy
from PIL import Image

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402  (setup: dump / project / work folder from the setup config)
DATA = paths.data_path()
ASSETS = paths.ASSETS
INDEX = paths.work("d7", "cubemap_shipped_index.json")
TEMPLATE = os.path.join(ASSETS, "_d7_template", "d7_template_cube.asset")
ASIDE = paths.work("d7", "d7_png_aside")
MANIFEST = paths.work("d7", "d7_cubemap_manifest.jsonl")

META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 8900000
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def face_images(d):
    raw = bytes(d.get_image_data())
    per = d.m_CompleteImageSize
    w = d.m_Width
    l0 = ((w + 3) // 4) ** 2 * 16
    faces = []
    for f in range(6):
        dec = texture2ddecoder.decode_bc6(raw[f * per:f * per + l0], w, w)
        faces.append(np.frombuffer(dec, np.uint8).reshape(w, w, 4)[:, :, [2, 1, 0]].astype(np.int16))
    return raw, faces


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    a = ap.parse_args()
    index = json.load(open(INDEX, encoding="utf-8"))
    pngs = []
    for dp, _, fns in os.walk(os.path.join(ASSETS, "Cubemap")):
        pngs += [os.path.join(dp, f) for f in fns if f.lower().endswith(".png")]
    template = open(TEMPLATE, "rb").read() if a.apply else None
    for png in sorted(pngs):
        rel = os.path.relpath(png, ASSETS).replace(os.sep, "/")
        stem = os.path.splitext(os.path.basename(png))[0]
        base = re.sub(r"_\d+$", "", stem)
        guid = re.search(r"^guid: ([0-9a-f]{32})", open(png + ".meta", encoding="utf-8").read(), re.M).group(1)
        strip = np.asarray(Image.open(png).convert("RGB")).astype(np.int16)
        best = None
        for c in [c for c in index if c["name"] in (stem, base)]:
            d = next(o for o in UnityPy.load(os.path.join(DATA, c["file"])).objects if o.path_id == c["path_id"]).read()
            raw, faces = face_images(d)
            w = d.m_Width
            score = None
            for flip in (False, True):
                s = np.mean([np.abs((faces[f][::-1] if flip else faces[f]) - strip[f * w:(f + 1) * w]).mean() for f in range(6)])
                score = s if score is None else min(score, s)
            if best is None or score < best[0]:
                best = (score, c, d, raw)
        if best is None:
            print("NO SHIPPED CUBEMAP", rel)
            continue
        score, c, d, raw = best
        print(f"{rel}: {c['file']} mean|d| vs PNG strip {score:.2f}")
        if not a.apply:
            continue
        env = UnityPy.load(template)
        obj = next(iter(env.objects))
        assert obj.type.name == "Cubemap" and obj.path_id == 8900000, (obj.type.name, obj.path_id)
        tt = obj.read_typetree()
        ts = d.m_TextureSettings
        tt.update({"m_Name": d.m_Name, "m_Width": d.m_Width, "m_Height": d.m_Height, "m_CompleteImageSize": d.m_CompleteImageSize,
                   "m_TextureFormat": int(d.m_TextureFormat), "m_MipCount": d.m_MipCount, "m_IsReadable": False,
                   "m_ImageCount": 6, "m_ColorSpace": getattr(d, "m_ColorSpace", 0), "image data": raw,
                   "m_StreamData": {"offset": 0, "size": 0, "path": ""}})
        tt["m_TextureSettings"].update({"m_FilterMode": ts.m_FilterMode, "m_Aniso": ts.m_Aniso, "m_MipBias": ts.m_MipBias,
                                        "m_WrapU": ts.m_WrapU, "m_WrapV": ts.m_WrapV, "m_WrapW": ts.m_WrapW})
        obj.save_typetree(tt)
        asset = os.path.splitext(png)[0] + ".asset"
        aside = os.path.join(ASIDE, rel)
        os.makedirs(os.path.dirname(aside), exist_ok=True)
        shutil.move(png, aside)
        shutil.move(png + ".meta", aside + ".meta")
        open(asset, "wb").write(env.file.save())
        open(asset + ".meta", "w", encoding="utf-8", newline="\n").write(META.format(guid=guid))
        with open(MANIFEST, "a", encoding="utf-8") as m:
            m.write(json.dumps({"png": rel, "asset": os.path.relpath(asset, ASSETS).replace(os.sep, "/"), "guid": guid,
                                "file": c["file"], "path_id": c["path_id"], "score": round(float(score), 3),
                                "sha1": hashlib.sha1(raw).hexdigest(), "state": "done"}) + "\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
