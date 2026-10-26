"""Phase 6 (HANDOFF §11.15, D7 groundwork): index every shipped Texture2D's format + sampler settings from the Switch bundles and the
player's own serialized files (UnityPy, read-only, header fields only - no texel decoding).

Why: the project's textures are AssetRipper PNGs whose importer settings re-compress them (textureCompression 2, automatic format).
Textures the game SHIPPED UNCOMPRESSED (Alpha8, R8, RGB24, RGBA32, ARGB32, RGBA4444, ...) can be restored to their exact shipped
texels by importer settings alone, once the PNG equals the shipped decode (tools/font_atlas_import_fix.py did this for the 12 TMP
atlases; the colour-grading LUTs are the next class, play-test log D-006). This index says which textures are which.

Writes work/s34/texture_shipped_index.json: [{file, path_id, name, format, width, height, mips, colorspace, filter, wrapU, wrapV,
aniso, mipbias, is_readable, image_size}], plus a by-format summary on stdout.
  py -3.13 -I tools/texture_shipped_index.py
"""
import collections
import json
import os
import sys
import time

import UnityPy

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402  (setup: dump / project / work folder from the setup config)
DATA = paths.data_path()
OUT = paths.work("d7", "texture_shipped_index.json")


def main():
    files = []
    for dp, _, fns in os.walk(os.path.join(DATA, "StreamingAssets", "AssetAssistant")):
        for fn in fns:
            if not fn.endswith((".manifest", ".bin", ".json")):
                files.append(os.path.join(dp, fn))
    files.sort()
    files += [os.path.join(DATA, f) for f in ("globalgamemanagers.assets", "resources.assets", "sharedassets0.assets", "level0")
              if os.path.exists(os.path.join(DATA, f))]
    out, errors, t0 = [], 0, time.time()
    for n, path in enumerate(files):
        try:
            env = UnityPy.load(path)
            for o in env.objects:
                if o.type.name != "Texture2D":
                    continue
                d = o.read()
                ts = d.m_TextureSettings
                out.append({"file": os.path.relpath(path, DATA).replace("\\", "/"), "path_id": o.path_id, "name": d.m_Name,
                            "format": int(d.m_TextureFormat), "width": d.m_Width, "height": d.m_Height, "mips": d.m_MipCount,
                            "colorspace": getattr(d, "m_ColorSpace", None), "filter": ts.m_FilterMode, "wrapU": ts.m_WrapU,
                            "wrapV": ts.m_WrapV, "aniso": ts.m_Aniso, "mipbias": ts.m_MipBias,
                            "is_readable": getattr(d, "m_IsReadable", None), "image_size": getattr(d, "m_CompleteImageSize", None)})
        except Exception as e:  # a few non-Unity files live beside the bundles
            errors += 1
            if errors <= 10:
                print("skip", os.path.relpath(path, DATA), type(e).__name__, str(e)[:120])
        if n % 1000 == 0:
            print(f"{n}/{len(files)} files, {len(out)} textures, {time.time() - t0:.0f} s", flush=True)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    json.dump(out, open(OUT, "w", encoding="utf-8"))
    fmt = collections.Counter(r["format"] for r in out)
    print(f"{len(out)} Texture2D in {len(files)} files ({errors} unreadable) -> {OUT}")
    print("by format:", dict(sorted(fmt.items())))
    return 0


if __name__ == "__main__":
    sys.exit(main())
