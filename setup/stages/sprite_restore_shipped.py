"""s33 (HANDOFF §11.14): restore the SHIPPED sprite geometry that AssetRipper's export cropped away.

AssetRipper exports a trimmed sprite (84 % of the 8,341 shipped Sprite objects - work/s33/sprite_shipped_index.json) cropped to its
texel area: m_Rect = the trimmed rect, m_Border = shipped border minus the trim, m_Pivot / m_Offset re-centred, and the trim offset
(m_RD.textureRectOffset) zeroed - every Image quad then draws without the shipped padding (too big: the message window's speaker tab
321x57 instead of the shipped 306x43, measured against a retail screenshot). Vertex positions survive the crop (the re-centred pivot
keeps the origin), so the restore rewrites only the sprite-space fields:
    m_Rect, m_Offset, m_Border, m_Pivot  <- the shipped Sprite
    m_RD.textureRectOffset              <- the shipped trim offset (== the SpriteAtlas entry's for atlas-packed sprites)
and leaves texture / textureRect / uvTransform / vertex data (the s32 atlas bake or AssetRipper's page binding) as they are, after
checking that the project's vertex POSITIONS equal the shipped ones (any mismatch is refused, not patched).

Shipped sprite -> project file:  atlas-packed: the s32 atlas spec (render-data key -> packedSprites path);  container path: the s32
bundle map;  else Assets/Sprite/<name>[_N].asset whose m_RD.textureRect equals the shipped / atlas one.
usage: py -3.13 -I tools/sprite_restore_shipped.py [--apply] (dry run: census + refusals -> work/s33/sprite_restore_report.txt)
Backups: work/s33/sprite_restore_backup/<path> (oldest kept); manifest work/s33/sprite_restore_manifest.json (sha1 before/after).
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import struct
import sys
from collections import Counter, defaultdict

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402  (setup: the project + work folder come from the setup config)

PROJ = paths.PROJECT
NUM =r"(-?[0-9.eE+-]+|nan|-?inf)"


def f32(v):
    return struct.unpack("<f", struct.pack("<f", float(v)))[0]


def fmt(v):
    v = f32(v)
    if v == int(v) and abs(v) < 1e9:
        return str(int(v))
    for p in range(6, 12):
        s = ("%." + str(p) + "g") % v
        if f32(float(s)) == v:
            return s
    return repr(v)


def key_ints(text):
    return [int(x) for x in re.findall(r"'data\[\d\]': (\d+)", text)]


def parse_sprite(text):
    """The fields we compare / rewrite, from a Sprite YAML (AssetRipper / Unity layout)."""
    if "\n--- !u!213 " not in "\n" + text and not text.startswith("--- !u!213"):
        return None
    out = {}
    m = re.search(r"\n  m_Name: (.*)\n", text)
    out["name"] = m.group(1) if m else None
    m = re.search(r"\n  m_Rect:\n    serializedVersion: 2\n    x: (\S+)\n    y: (\S+)\n    width: (\S+)\n    height: (\S+)\n", text)
    out["rect"] = [float(v) for v in m.groups()]
    i0, i1 = text.index("\n  m_RD:\n"), text.index("\n  m_AtlasRD:\n")
    rd = text[i0:i1]
    m = re.search(r"\n    textureRect:\n      serializedVersion: 2\n      x: (\S+)\n      y: (\S+)\n      width: (\S+)\n      height: (\S+)\n", rd)
    out["texRect"] = [float(v) for v in m.groups()]
    m = re.search(r"\n    texture: \{fileID: \d+, guid: (\w+)", rd)
    out["texGuid"] = m.group(1) if m else None
    m = re.search(r"\n      m_VertexCount: (\d+)\n", rd)
    out["vcount"] = int(m.group(1)) if m else 0
    m = re.search(r"\n      _typelessdata: ([0-9a-f]*)\n", rd)
    out["vdata"] = m.group(1) if m else ""
    chans = re.findall(r"\n      - stream: (\d+)\n        offset: (\d+)\n        format: (\d+)\n        dimension: (\d+)", rd)
    out["channels"] = [tuple(int(x) for x in c) for c in chans]
    return out


def positions(vcount, channels, data_hex):
    """Vertex positions (channel 0, float3) from Unity's vertex buffer layout (streams 16-aligned)."""
    if not vcount or not channels:
        return []
    data = bytes.fromhex(data_hex)
    strides = {}
    for st, off, fm, dm in channels:
        if dm:
            strides[st] = max(strides.get(st, 0), off + dm * (4 if fm == 0 else 2 if fm == 1 else 1))
    starts, pos = {}, 0
    for st in sorted(strides):
        starts[st] = pos
        pos = ((pos + vcount * strides[st] + 15) // 16) * 16
    st, off, fm, dm = channels[0]
    return [struct.unpack_from("<3f", data, starts[st] + k * strides[st] + off) for k in range(vcount)]


def shipped_channels(s):
    return [(c["stream"], c["offset"], c["format"], c["dimension"]) for c in (s["rd"]["channels"] or [])]


def geom(s):
    return json.dumps([s["m_Rect"], s["m_Offset"], s["m_Border"], s["m_Pivot"], s["rd"]["textureRectOffset"]], sort_keys=True)


def rewrite(text, s):
    r, o, b, p = s["m_Rect"], s["m_Offset"], s["m_Border"], s["m_Pivot"]
    t = s["rd"]["textureRectOffset"]
    new = re.sub(r"(\n  m_Rect:\n    serializedVersion: 2\n)    x: \S+\n    y: \S+\n    width: \S+\n    height: \S+\n",
                 lambda m: m.group(1) + "    x: %s\n    y: %s\n    width: %s\n    height: %s\n" % (fmt(r["x"]), fmt(r["y"]), fmt(r["width"]), fmt(r["height"])),
                 text, count=1)
    new = re.sub(r"\n  m_Offset: \{x: [^,]+, y: [^}]+\}", "\n  m_Offset: {x: %s, y: %s}" % (fmt(o["x"]), fmt(o["y"])), new, count=1)
    new = re.sub(r"\n  m_Border: \{x: [^,]+, y: [^,]+, z: [^,]+, w: [^}]+\}",
                 "\n  m_Border: {x: %s, y: %s, z: %s, w: %s}" % (fmt(b["x"]), fmt(b["y"]), fmt(b["z"]), fmt(b["w"])), new, count=1)
    new = re.sub(r"\n  m_Pivot: \{x: [^,]+, y: [^}]+\}", "\n  m_Pivot: {x: %s, y: %s}" % (fmt(p["x"]), fmt(p["y"])), new, count=1)
    i0, i1 = new.index("\n  m_RD:\n"), new.index("\n  m_AtlasRD:\n")
    rd = re.sub(r"\n    textureRectOffset: \{x: [^,]+, y: [^}]+\}", "\n    textureRectOffset: {x: %s, y: %s}" % (fmt(t["x"]), fmt(t["y"])), new[i0:i1], count=1)
    return new[:i0] + rd + new[i1:]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--backup", default=paths.work("sprites", "sprite_restore_backup"))
    ap.add_argument("--manifest", default=paths.work("sprites", "sprite_restore_manifest.json"))
    ap.add_argument("--report", default=paths.work("sprites", "sprite_restore_report.txt"))
    ap.add_argument("--diff", help="write a unified diff of the plan here (s44)")
    a = ap.parse_args()

    S = json.load(open(paths.work("sprites", "sprite_shipped_index.json"), encoding="utf-8"))
    bmap = json.load(open(paths.work("atlas", "bundle_map.json"), encoding="utf-8"))
    spec = json.load(open(paths.work("atlas", "spriteatlas_spec.json"), encoding="utf-8"))

    container_to_file = {}
    for bundle in bmap["bundles"].values():
        for dev, proj in bundle["paths"]:
            container_to_file[dev.lower()] = proj
    # atlas render-data key -> atlas entry (page texture path + page rect); a packed name may sit in several atlases (Field / Field_1)
    key_to_packed = {}
    entry_by_key = {}
    for atl in spec["atlases"]:
        names = atl["packedNames"]
        for e in atl["renderData"]:
            entry_by_key.setdefault(tuple(e["key"]), e)
        for i, n in enumerate(names):
            key_to_packed.setdefault(n, []).append((atl["packedSprites"][i] if i < len(atl["packedSprites"]) else None, atl))
    page_guid = {}

    def guid_of(path):
        if path not in page_guid:
            g = None
            try:
                for line in open(os.path.join(PROJ, *path.split("/")) + ".meta", encoding="utf-8"):
                    if line.startswith("guid: "):
                        g = line[6:].strip()
                        break
            except OSError:
                pass
            page_guid[path] = g
        return page_guid[path]
    sprite_dir = os.path.join(PROJ, "Assets", "Sprite")
    by_stem = defaultdict(list)
    for fn in os.listdir(sprite_dir):
        if fn.endswith(".asset"):
            stem = re.sub(r"_\d+$", "", fn[:-6])
            by_stem[stem].append("Assets/Sprite/" + fn)
            by_stem[fn[:-6]].append("Assets/Sprite/" + fn)

    cache = {}

    def load(path):
        if path not in cache:
            full = os.path.join(PROJ, *path.split("/"))
            if not os.path.exists(full) or not path.endswith(".asset"):
                cache[path] = None
            else:
                txt = open(full, encoding="utf-8", errors="replace").read()
                cache[path] = (txt, parse_sprite(txt))
        return cache[path]

    stats, refused, plan, claimed = Counter(), [], [], {}
    # s44: page-backed copies of packed sprites beyond the first match, restored in a second pass (see below)
    page_copies = defaultdict(list)

    def restore_one(path, how, s, trimmed):
        if path in claimed:
            stats[("duplicate", "same geometry", how.split(":")[0])] += 1
            return
        claimed[path] = geom(s)
        txt, p = load(path)
        # vertex positions must agree (the crop keeps the origin)
        # UnityPy returns the vertex bytes under m_DataSize (the index stores them hex-encoded there)
        sdata = s["rd"]["data"] if isinstance(s["rd"]["data"], str) and s["rd"]["data"] not in ("None", "") else s["rd"]["dataSize"]
        sp = positions(s["rd"]["vertexCount"], shipped_channels(s), sdata if isinstance(sdata, str) else "")
        pp = positions(p["vcount"], p["channels"], p["vdata"])
        if len(sp) != len(pp) or any(abs(x - y) > 1e-4 for u, v in zip(sp, pp) for x, y in zip(u, v)):
            stats[("refused", "positions differ", how.split(":")[0])] += 1
            refused.append((s["bundle"], s["name"], "vertex positions differ from the shipped sprite", path))
            return
        new = rewrite(txt, s)
        if new == txt:
            stats[("same", "trim" if trimmed else "full", how.split(":")[0])] += 1
            return
        stats[("restore", "trim" if trimmed else "full", how.split(":")[0])] += 1
        plan.append((path, txt, new))

    for s in S:
        rd = s["rd"]
        ds = rd["downscaleMultiplier"] or 1.0
        trimmed = abs(rd["textureRect"]["width"] * ds - s["m_Rect"]["width"]) > 0.01 or abs(rd["textureRect"]["height"] * ds - s["m_Rect"]["height"]) > 0.01
        cands = []
        if s["container"]:
            f = container_to_file.get(s["container"].lower())
            if f:
                cands.append((f, "container"))
        if rd["textureNull"]:
            for path, atl in key_to_packed.get(s["name"], []):
                if path:
                    cands.append((path, "atlas:" + atl["name"]))
        for path in by_stem.get(s["name"], []):
            cands.append((path, "Sprite/"))
        hit = None
        entry = entry_by_key.get(tuple(key_ints(s["m_RenderDataKey"]))) if rd["textureNull"] else None
        for path, how in cands:
            got = load(path)
            # AssetRipper suffixes _N to the m_Name of same-named copies (language variants of one sheet)
            if not got or not got[1] or not got[1]["name"] or not re.fullmatch(re.escape(s["name"]) + r"(_\d+)?", got[1]["name"]):
                continue
            p = got[1]
            # same texel area: the project's m_Rect / textureRect size == the shipped texel size
            tw, th = rd["textureRect"]["width"], rd["textureRect"]["height"]
            if abs(p["texRect"][2] - tw) > 0.02 or abs(p["texRect"][3] - th) > 0.02:
                continue
            if how == "Sprite/" and not rd["textureNull"] and (abs(p["texRect"][0] - rd["textureRect"]["x"]) > 0.02 or abs(p["texRect"][1] - rd["textureRect"]["y"]) > 0.02):
                continue
            # a packed sprite's page-backed copy: same page texture and the atlas entry's page rect
            if how == "Sprite/" and rd["textureNull"]:
                if not entry or p.get("texGuid") != guid_of(entry["texture"]) or abs(p["texRect"][0] - entry["textureRect"][0]) > 0.02 or abs(p["texRect"][1] - entry["textureRect"][1]) > 0.02:
                    continue
            # the same sprite ships in several bundles (a prefab bundle + its atlas bundle): identical geometry may share a file
            if path in claimed and claimed[path] != geom(s):
                continue
            if hit:
                # s44: AssetRipper exports a packed sprite twice - the late-binding copy the atlas spec lists AND a page-backed
                # <name>_N cut from the atlas page, which is the copy the prefabs reference (Dance.prefab's contest score gauge
                # drew the cropped 287x24 strip stretched over its 80-unit rect). Remember page-backed copies for pass 2.
                if rd["textureNull"] and how == "Sprite/" and path != hit[0]:
                    page_copies[path].append((s, how, trimmed))
                continue
            hit = (path, how)
        if not hit and s["container"] and not rd["textureNull"]:
            # a single sprite generated by the TextureImporter from the container .png: the importer rebuilds the tight texel
            # rect itself (same Unity, same alpha, same extrude); AssetRipper's meta carries the cropped pivot / border instead
            # of the shipped ones -> write those into the .png.meta (spriteMode 1 only; a sheet would need its rects too)
            png = container_to_file.get(s["container"].lower())
            if png and png.endswith(".png") and os.path.exists(os.path.join(PROJ, *png.split("/")) + ".meta"):
                mpath = png + ".meta"
                mtxt = open(os.path.join(PROJ, *mpath.split("/")), encoding="utf-8").read()
                mode = re.search(r"\n  spriteMode: (\d+)", mtxt)
                if mode and mode.group(1) == "1" and int(re.search(r"\n  spriteExtrude: (\d+)", mtxt).group(1)) == s["m_Extrude"]:
                    pv, bd = s["m_Pivot"], s["m_Border"]
                    new = re.sub(r"\n  spritePivot: \{x: [^,]+, y: [^}]+\}", "\n  spritePivot: {x: %s, y: %s}" % (fmt(pv["x"]), fmt(pv["y"])), mtxt, count=1)
                    new = re.sub(r"\n  spriteBorder: \{x: [^,]+, y: [^,]+, z: [^,]+, w: [^}]+\}",
                                 "\n  spriteBorder: {x: %s, y: %s, z: %s, w: %s}" % (fmt(bd["x"]), fmt(bd["y"]), fmt(bd["z"]), fmt(bd["w"])), new, count=1)
                    new = re.sub(r"\n  spritePixelsToUnits: \S+", "\n  spritePixelsToUnits: %s" % fmt(s["m_PixelsToUnits"]), new, count=1)
                    if mpath in claimed and claimed[mpath] != geom(s):
                        stats[("refused", "importer: two shipped sprites differ", "png")] += 1
                        refused.append((s["bundle"], s["name"], "two shipped sprites with different geometry on one .png", mpath))
                        continue
                    if mpath in claimed:
                        stats[("duplicate", "same geometry", "png")] += 1
                        continue
                    claimed[mpath] = geom(s)
                    if new == mtxt:
                        stats[("same", "trim" if trimmed else "full", "png.meta")] += 1
                    else:
                        stats[("restore", "trim" if trimmed else "full", "png.meta")] += 1
                        plan.append((mpath, mtxt, new))
                    continue
        if not hit:
            stats[("unmapped", "trim" if trimmed else "full", "packed" if rd["textureNull"] else "tex")] += 1
            refused.append((s["bundle"], s["name"], "no project sprite with the same name + texel size", [c[0] for c in cands][:4]))
            continue
        restore_one(hit[0], hit[1], s, trimmed)

    # s44 pass 2: a page-backed copy nobody claimed in pass 1 gets the shipped geometry when every shipped sprite that maps to it
    # agrees (two shipped sprites can share one atlas entry with different borders: GMS_ScrollBar_01 / _02 - pass 1 pairs those)
    for path, lst in sorted(page_copies.items()):
        if path in claimed:
            stats[("pass2", "already claimed", "Sprite/")] += 1
            continue
        if len({geom(s) for s, _, _ in lst}) != 1:
            stats[("refused", "page copy: shipped geometries differ", "Sprite/")] += 1
            refused.append((lst[0][0]["bundle"], lst[0][0]["name"], "page-backed copy: shipped geometries differ", path))
            continue
        s, how, trimmed = lst[0]
        restore_one(path, "page-copy", s, trimmed)

    rep = ["sprite_restore_shipped: %d shipped sprites" % len(S)]
    for k, v in sorted(stats.items()):
        rep.append("  %6d %s" % (v, " / ".join(k)))
    rep.append("refused / unmapped (first 200):")
    for r in refused[:200]:
        rep.append("  " + json.dumps(r))
    open(a.report, "w", encoding="utf-8").write("\n".join(rep) + "\n")
    json.dump(refused, open(a.report.replace(".txt", "_refused.json"), "w", encoding="utf-8"), indent=0)
    print("\n".join(rep[: len(stats) + 1]))
    print("plan:", len(plan), "files;", "refused/unmapped:", len(refused), "->", a.report)
    if a.diff:
        import difflib
        with open(a.diff, "w", encoding="utf-8") as fh:
            for path, txt, new in plan:
                fh.writelines(difflib.unified_diff(txt.splitlines(True), new.splitlines(True), path, path, n=0))
        print("diff ->", a.diff)

    if a.apply:
        man = []
        for path, txt, new in plan:
            full = os.path.join(PROJ, *path.split("/"))
            bk = os.path.join(a.backup, *path.split("/"))
            os.makedirs(os.path.dirname(bk), exist_ok=True)
            if not os.path.exists(bk):
                shutil.copy2(full, bk)
            raw = new.encode("utf-8")
            man.append({"path": path, "sha1_before": hashlib.sha1(txt.encode("utf-8")).hexdigest(), "sha1_after": hashlib.sha1(raw).hexdigest()})
            with open(full, "wb") as fh:
                fh.write(raw)
        json.dump(man, open(a.manifest, "w", encoding="utf-8"), indent=1)
        print("applied", len(man), "-> manifest", a.manifest)


if __name__ == "__main__":
    sys.exit(main())
