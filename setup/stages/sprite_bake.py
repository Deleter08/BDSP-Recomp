"""Phase 4 (HANDOFF §11.13) — bake the shipped atlas binding into the atlas-packed Sprite assets.

Shipped: a packed sprite carries no texture (`m_RD.texture` null, its m_RD = the rect in the developers' source texture) and is
bound at runtime to its SpriteAtlas, whose m_RenderDataMap holds the page texture + rect + uvTransform per sprite. The Unity 2019.4
editor cannot hold that map (`m_RenderDataMap` is not a serialized property there; the editor rebuilds it only by packing, and
packing an atlas with no packables EMPTIES it — measured s32, work/s32/atlas_probe_*.txt). So each packed sprite's m_RD is set to
what the runtime binding produces: texture / alphaTexture = the page PNG, textureRect / textureRectOffset / atlasRectOffset /
settingsRaw / uvTransform / downscaleMultiplier = the atlas entry, and the mesh UVs recomputed from the vertex positions
(texel = pos * uvTransform.xz + uvTransform.yw; uv = texel / page size; no shipped entry uses packing rotation — checked).
The sprite then draws the shipped texels on its own; the rebuilt .spriteatlas keeps m_PackedSprites for GetSprite.

Only sprites whose m_RD.texture is null are baked (the container sprites); the Sprite/ exports AssetRipper already pointed at a page
are left as they are. Text edit of the m_RD block only (m_AtlasRD untouched); originals copied to --backup first (oldest copy kept).

usage: py -3.13 -I tools/sprite_bake.py --spec work/s32/spriteatlas_spec.json --project project --backup work/s32/sprite_bake_backup
           --report work/s32/sprite_bake_report.txt [--apply]
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import struct
import sys

from PIL import Image


def meta_guid(path):
    with open(path + ".meta", "r", encoding="utf-8", errors="replace") as f:
        for line in f:
            if line.startswith("guid: "):
                return line[6:].strip()
    return None


def fmt(v):
    """Unity YAML float text (shortest round-trip, integers without .0)."""
    v = float(struct.unpack("<f", struct.pack("<f", v))[0])
    if v == int(v) and abs(v) < 1e15:
        return str(int(v))
    r = repr(v)
    for digits in range(6, 18):
        s = f"{v:.{digits}g}"
        if float(struct.unpack("<f", struct.pack("<f", float(s)))[0]) == v:
            r = s
            break
    return r.replace("e+", "e+").replace("e-0", "e-0")


def bake_text(text, rd, page_ref, alpha_ref, page_w, page_h):
    lines = text.split("\n")
    try:
        start = lines.index("  m_RD:")
    except ValueError:
        return None, "no m_RD block"
    end = next((i for i in range(start + 1, len(lines)) if lines[i].startswith("  ") and not lines[i].startswith("   ")), len(lines))
    blk = lines[start:end]

    def find(prefix):
        for i, l in enumerate(blk):
            if l.startswith(prefix):
                return i
        return None

    it = find("    texture: ")
    if it is None or blk[it].strip() != "texture: {fileID: 0}":
        return None, "texture already set"
    blk[it] = "    texture: " + page_ref
    ia = find("    alphaTexture: ")
    if alpha_ref and ia is not None:
        blk[ia] = "    alphaTexture: " + alpha_ref

    # vertex data: channel 0 = position (float3), channel 4 = uv0 (float2)
    iv = find("    m_VertexData:")
    ich = find("      m_Channels:")
    chans, j = [], ich + 1
    while j < len(blk) and blk[j].startswith("      - stream: "):
        st = int(blk[j].split(": ")[1]); off = int(blk[j + 1].split(": ")[1]); fm = int(blk[j + 2].split(": ")[1]); dm = int(blk[j + 3].split(": ")[1])
        chans.append((st, off, fm, dm)); j += 4
    ivc = find("      m_VertexCount: ")
    ids = find("      m_DataSize: ")
    itd = find("      _typelessdata: ")
    vcount = int(blk[ivc].split(": ")[1])
    data = bytearray(bytes.fromhex(blk[itd].split(": ", 1)[1].strip()))
    if len(chans) < 5 or chans[0][2] != 0 or chans[0][3] != 3 or chans[4][2] != 0 or chans[4][3] != 2:
        return None, f"unexpected channels {chans[:5]}"
    strides = {}
    for st, off, fm, dm in chans:
        if dm:
            strides[st] = max(strides.get(st, 0), off + dm * 4)
    starts, pos = {}, 0
    for st in sorted(strides):
        starts[st] = pos
        pos = ((pos + vcount * strides[st] + 15) // 16) * 16 if st != max(strides) else pos + vcount * strides[st]
    if pos != len(data) or len(data) != int(blk[ids].split(": ")[1]):
        return None, f"vertex layout mismatch: computed {pos}, data {len(data)}"
    ux, uy, vx, vy = rd["uvTransform"]
    for k in range(vcount):
        p0 = starts[chans[0][0]] + k * strides[chans[0][0]] + chans[0][1]
        x, y, _z = struct.unpack_from("<3f", data, p0)
        u0 = starts[chans[4][0]] + k * strides[chans[4][0]] + chans[4][1]
        struct.pack_into("<2f", data, u0, (x * ux + uy) / page_w, (y * vx + vy) / page_h)
    blk[itd] = "      _typelessdata: " + data.hex()

    itr = find("    textureRect:")
    for n, key in enumerate(("x", "y", "width", "height")):
        assert blk[itr + 2 + n].startswith(f"      {key}: ")
        blk[itr + 2 + n] = f"      {key}: {fmt(rd['textureRect'][n])}"
    # The atlas entry's textureRectOffset IS the shipped trim offset (== the shipped Sprite's own m_RD.textureRectOffset). It only
    # draws right with the SHIPPED m_Rect / m_Border / m_Pivot / m_Offset - AssetRipper exports trimmed sprites cropped (m_Rect =
    # the texel area). Run tools/sprite_restore_shipped.py after this bake (s33, HANDOFF §11.14). An s33 detour that zeroed this
    # offset instead (tools/sprite_trim_offset_fix.py) was wrong and is superseded.
    blk[find("    textureRectOffset: ")] = f"    textureRectOffset: {{x: {fmt(rd['textureRectOffset'][0])}, y: {fmt(rd['textureRectOffset'][1])}}}"
    blk[find("    atlasRectOffset: ")] = f"    atlasRectOffset: {{x: {fmt(rd['atlasRectOffset'][0])}, y: {fmt(rd['atlasRectOffset'][1])}}}"
    blk[find("    settingsRaw: ")] = f"    settingsRaw: {rd['settingsRaw']}"
    blk[find("    uvTransform: ")] = "    uvTransform: {x: %s, y: %s, z: %s, w: %s}" % tuple(fmt(v) for v in rd["uvTransform"])
    blk[find("    downscaleMultiplier: ")] = f"    downscaleMultiplier: {fmt(rd['downscaleMultiplier'])}"
    lines[start:end] = blk
    return "\n".join(lines), None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--spec", required=True)
    ap.add_argument("--project", required=True)
    ap.add_argument("--backup", required=True)
    ap.add_argument("--report", required=True)
    ap.add_argument("--apply", action="store_true")
    a = ap.parse_args()

    spec = json.load(open(a.spec, encoding="utf-8"))
    rx_key = re.compile(r"^\s+([0-9a-f]{32}): (-?\d+)\s*$")
    page_info = {}
    done, skipped, problems, manifest = 0, {}, [], []
    seen = set()
    for atlas in spec["atlases"]:
        rd_by_key = {(r["keyText"], r["fileID"]): r for r in atlas["renderData"]}
        for path in atlas["packedSprites"]:
            if not path or path in seen:
                continue
            seen.add(path)
            full = os.path.join(a.project, path)
            text = open(full, "r", encoding="utf-8", newline="").read()
            lines = text.split("\n")
            key = None
            for i, l in enumerate(lines):
                if l.startswith("  m_RenderDataKey:"):
                    m = rx_key.match(lines[i + 1])
                    key = (m.group(1), int(m.group(2))) if m else None
                    break
            rd = rd_by_key.get(key)
            if rd is None:
                reason = "texture already set" if "    texture: {fileID: 0}" not in text.split("  m_AtlasRD:")[0] else "rdkey not in this atlas"
                skipped[reason] = skipped.get(reason, 0) + 1
                continue
            refs = []
            for slot in ("texture", "alphaTexture"):
                tp = rd[slot]
                if not tp:
                    refs.append(None)
                    continue
                if tp not in page_info:
                    with Image.open(os.path.join(a.project, tp)) as im:
                        page_info[tp] = (meta_guid(os.path.join(a.project, tp)), im.size)
                refs.append("{fileID: 2800000, guid: %s, type: 3}" % page_info[tp][0])
            if refs[0] is None:
                problems.append((path, "no page texture"))
                continue
            w, h = page_info[rd["texture"]][1]
            new, err = bake_text(text, rd, refs[0], refs[1], w, h)
            if err:
                if err == "texture already set":
                    skipped[err] = skipped.get(err, 0) + 1
                else:
                    problems.append((path, err))
                continue
            manifest.append({"path": path, "atlas": atlas["target"], "page": rd["texture"],
                             "sha1_before": hashlib.sha1(text.encode("utf-8")).hexdigest(),
                             "sha1_after": hashlib.sha1(new.encode("utf-8")).hexdigest()})
            if a.apply:
                bk = os.path.join(a.backup, path)
                if not os.path.exists(bk):
                    os.makedirs(os.path.dirname(bk), exist_ok=True)
                    shutil.copy2(full, bk)
                with open(full, "w", encoding="utf-8", newline="") as f:
                    f.write(new)
            done += 1
    with open(a.report, "w", encoding="utf-8") as r:
        r.write(f"{'APPLIED' if a.apply else 'DRY RUN'}: baked {done}, skipped {skipped}, problems {len(problems)}\n")
        for p in problems:
            r.write(f"  PROBLEM {p}\n")
    with open(os.path.splitext(a.report)[0] + "_manifest.json", "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=0)
    print(f"{'APPLIED' if a.apply else 'DRY RUN'}: baked {done}, skipped {skipped}, problems {len(problems)}", flush=True)


if __name__ == "__main__":
    sys.exit(main())
