"""s33 (HANDOFF §11.14): index every shipped Sprite object's raw geometry from the Switch bundles (UnityPy, read-only).

Why: AssetRipper exported each TRIMMED sprite cropped to its texel area - m_Rect = the trimmed rect, m_Border shrunk by the trim,
pivot re-centred, m_RD.textureRectOffset 0. The shipped Sprite keeps the full rect (e.g. cmn_wd_message_speaker_01_01: m_Rect
0,0,66,52, border 29,12,29,28, trim offset 8.05,8.08), and Unity's UI insets every Image quad by that trim (DataUtility.GetPadding).
The cropped export draws trimmed sprites too big (the message window's speaker tab: 321x57 instead of the shipped 306x43).

Writes work/s33/sprite_shipped_index.json: one record per Sprite object - bundle, path_id, name, container path (if any), m_Rect,
m_Offset, m_Border, m_Pivot, m_PixelsToUnits, m_Extrude, m_IsPolygon, m_AtlasTags, m_RenderDataKey, and m_RD (textureRect,
textureRectOffset, atlasRectOffset, settingsRaw, uvTransform, downscaleMultiplier, m_SubMeshes, m_IndexBuffer hex, m_VertexData
channels + data hex, m_Bindpose count). Run with -I from the workspace: py -3.13 -I tools/sprite_shipped_index.py
"""
import json
import os
import sys
import time

import UnityPy

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402  (setup: the dump + work folder come from the setup config)

AA = paths.need(paths.AA, "dump")
OUT = paths.work("sprites", "sprite_shipped_index.json")


def hexval(v):
    if isinstance(v, (bytes, bytearray)):
        return bytes(v).hex()
    if isinstance(v, list):
        return bytes(v).hex()
    return v


def main():
    files = []
    for dp, _, fns in os.walk(AA):
        for fn in fns:
            if not fn.endswith((".manifest", ".bin", ".json")):
                files.append(os.path.join(dp, fn))
    files.sort()
    out, errors, t0 = [], 0, time.time()
    for n, f in enumerate(files):
        rel = os.path.relpath(f, AA).replace("\\", "/")
        try:
            env = UnityPy.load(f)
        except Exception:
            errors += 1
            continue
        containers = {}
        sprites = []
        for obj in env.objects:
            tn = obj.type.name
            if tn == "AssetBundle":
                try:
                    tree = obj.read_typetree()
                    for path, info in tree.get("m_Container", []):
                        containers[info["asset"]["m_PathID"]] = path
                except Exception:
                    pass
            elif tn == "Sprite":
                sprites.append(obj)
        for obj in sprites:
            try:
                t = obj.read_typetree()
            except Exception:
                errors += 1
                continue
            rd = t["m_RD"]
            vd = rd.get("m_VertexData", {})
            out.append({
                "bundle": rel, "path_id": obj.path_id, "name": t.get("m_Name"), "container": containers.get(obj.path_id),
                "m_Rect": t["m_Rect"], "m_Offset": t["m_Offset"], "m_Border": t["m_Border"], "m_Pivot": t["m_Pivot"],
                "m_PixelsToUnits": t["m_PixelsToUnits"], "m_Extrude": t.get("m_Extrude"), "m_IsPolygon": t.get("m_IsPolygon"),
                "m_AtlasTags": t.get("m_AtlasTags"), "m_RenderDataKey": str(t.get("m_RenderDataKey")),
                "rd": {
                    "textureRect": rd["textureRect"], "textureRectOffset": rd["textureRectOffset"],
                    "atlasRectOffset": rd.get("atlasRectOffset"), "settingsRaw": rd.get("settingsRaw"),
                    "uvTransform": rd.get("uvTransform"), "downscaleMultiplier": rd.get("downscaleMultiplier"),
                    "textureNull": rd["texture"]["m_PathID"] == 0,
                    "subMeshes": rd.get("m_SubMeshes"), "indexBuffer": hexval(rd.get("m_IndexBuffer")),
                    "vertexCount": vd.get("m_VertexCount"), "channels": vd.get("m_Channels"), "dataSize": vd.get("m_DataSize"),
                    "data": hexval(vd.get("m_DataSize") and vd.get("m_Data") or vd.get("_typelessdata")),
                    "bindpose": len(rd.get("m_Bindpose") or []),
                },
            })
        if n % 1000 == 0:
            print(f"{n}/{len(files)} files, {len(out)} sprites, {errors} errors, {time.time() - t0:.0f}s", flush=True)
    json.dump(out, open(OUT, "w", encoding="utf-8"), default=lambda o: bytes(o).hex() if isinstance(o, (bytes, bytearray)) else str(o))
    trimmed = sum(1 for s in out if abs(s["rd"]["textureRect"]["width"] * (s["rd"]["downscaleMultiplier"] or 1) - s["m_Rect"]["width"]) > 0.01
                  or abs(s["rd"]["textureRect"]["height"] * (s["rd"]["downscaleMultiplier"] or 1) - s["m_Rect"]["height"]) > 0.01)
    print(f"DONE files {len(files)} sprites {len(out)} trimmed {trimmed} errors {errors} -> {OUT}")


if __name__ == "__main__":
    sys.exit(main())
