"""D7 (HANDOFF §11.15): index every shipped Cubemap object (UnityPy, read-only): name, size, format, mips, sampler, data size.
The project's cubemaps are AssetRipper PNG strips (textureShape 2) - the native rebuild needs the shipped face data.
  py -3.13 -I tools/cubemap_shipped_index.py  ->  work/s34/cubemap_shipped_index.json
"""
import json
import os
import sys

import UnityPy

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402  (setup: dump / project / work folder from the setup config)
DATA = paths.data_path()


def main():
    out = []
    files = []
    for dp, _, fns in os.walk(os.path.join(DATA, "StreamingAssets", "AssetAssistant")):
        files += [os.path.join(dp, f) for f in fns if not f.endswith((".manifest", ".bin", ".json"))]
    files += [os.path.join(DATA, f) for f in ("resources.assets", "sharedassets0.assets", "level0")]
    for path in sorted(files):
        try:
            env = UnityPy.load(path)
        except Exception:
            continue
        for o in env.objects:
            if o.type.name != "Cubemap":
                continue
            d = o.read()
            ts = d.m_TextureSettings
            out.append({"file": os.path.relpath(path, DATA).replace(os.sep, "/"), "path_id": o.path_id, "name": d.m_Name,
                        "width": d.m_Width, "height": d.m_Height, "format": int(d.m_TextureFormat), "mips": d.m_MipCount,
                        "image_count": getattr(d, "m_ImageCount", None), "dimension": getattr(d, "m_TextureDimension", None),
                        "filter": ts.m_FilterMode, "wrapU": ts.m_WrapU, "aniso": ts.m_Aniso, "mipbias": ts.m_MipBias,
                        "colorspace": getattr(d, "m_ColorSpace", None), "size": getattr(d, "m_CompleteImageSize", None)})
    json.dump(out, open(paths.work("d7", "cubemap_shipped_index.json"), "w", encoding="utf-8"), indent=1)
    print(len(out), "cubemaps:", sorted({(r["name"], r["width"], r["format"], r["mips"]) for r in out}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
