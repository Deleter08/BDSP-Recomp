"""Phase 4 (HANDOFF §11.6 / §11.13) — rebuild spec for the 73 SpriteAtlas objects AssetRipper did not export.

AssetRipper exported every atlas-packed Sprite as a native .asset with `m_RD.texture: {fileID: 0}` (as shipped: the packed sprite
binds late to its atlas, which holds the render data) and every atlas page as `Assets/Texture2D/sactx-*.png`, but no SpriteAtlas:
60 bundle container paths (`*.spriteatlas`) are missing and 16 prefab / asset fields carry AssetRipper's placeholder
`{fileID: 687078895, guid: 0000000deadbeef15deadf00d0000000, type: 3}`.

This tool reads the shipped SpriteAtlas objects (UnityPy, read-only, run with -I) and writes a JSON spec that the editor script
`Assets/Editor/BdspAudit/BdspAtlas.cs` turns into real `.spriteatlas` assets through SerializedObject (Unity writes its own YAML):
  - target path (the bundle container path with the on-disk folder case; an atlas with no container path -> Assets/SpriteAtlas/<name>),
  - m_Name / m_Tag / m_IsVariant,
  - m_PackedSprites -> the project Sprite asset of each packed sprite, found by its unique m_RenderDataKey (GUID + fileID),
  - m_PackedSpriteNamesToIndex,
  - m_RenderDataMap -> key (GUID words + fileID, copied verbatim) and SpriteAtlasData with the page texture mapped to its PNG,
  - placeholder fixes: (file, line, atlas key) for every deadbeef reference, matched by owner GameObject name + field name.

usage: py -3.13 -I tools/spriteatlas_spec.py --aa <romfs>/Data/StreamingAssets/AssetAssistant --containers work/s32/bundle_containers.json
           --map work/s32/bundle_map.json --project project --out work/s32/spriteatlas_spec.json --report work/s32/spriteatlas_report.txt
"""
import argparse
import hashlib
import json
import os
import re
import sys

DEADBEEF = "{fileID: 687078895, guid: 0000000deadbeef15deadf00d0000000, type: 3}"


def guid_text(words):
    """Unity's GUID text: each uint32 as 8 hex digits, nibble order reversed (verified on btl_txt_30sec_01: 0xF2099D39 -> 93d9902f)."""
    return "".join(f"{w:08x}"[::-1] for w in words)


def read_meta_guid(path):
    try:
        with open(path + ".meta", "r", encoding="utf-8", errors="replace") as f:
            for line in f:
                if line.startswith("guid: "):
                    return line[6:].strip()
    except OSError:
        pass
    return None


def index_sprites(project):
    """(rdkey guid text, fileID) -> [project paths] for every native Sprite .asset, and m_Name -> [(path, texture guid, textureRect)]
    (AssetRipper re-keyed the atlas-packed sprites that had no container path and pointed them at the page: the second index finds them)."""
    idx, by_name, n = {}, {}, 0
    rx_key = re.compile(r"^\s+([0-9a-f]{32}): (-?\d+)\s*$")
    rx_tex = re.compile(r"^    texture: \{fileID: (-?\d+)(?:, guid: ([0-9a-f]{32}))?")
    for dirpath, _d, names in os.walk(os.path.join(project, "Assets")):
        for nm in names:
            if not nm.endswith(".asset"):
                continue
            full = os.path.join(dirpath, nm)
            try:
                with open(full, "r", encoding="utf-8", errors="replace") as f:
                    head = [next(f, "") for _ in range(4)]
                    if len(head) < 4 or not head[2].startswith("--- !u!213 "):
                        continue
                    lines = head + f.read().split("\n")
            except OSError:
                continue
            n += 1
            rel = os.path.relpath(full, project).replace("\\", "/")
            name, texguid, trect = None, None, None
            for i, line in enumerate(lines):
                if line.startswith("  m_Name: ") and name is None:
                    name = line[10:].strip()
                elif line.startswith("  m_RenderDataKey:"):
                    m = rx_key.match(lines[i + 1])
                    if m:
                        idx.setdefault((m.group(1), int(m.group(2))), []).append(rel)
                elif rx_tex.match(line) and texguid is None:
                    texguid = rx_tex.match(line).group(2)
                elif line.startswith("    textureRect:") and trect is None:
                    vals = {}
                    for j in range(i + 1, i + 6):
                        mm = re.match(r"^\s+(x|y|width|height): (\S+)", lines[j])
                        if mm:
                            vals[mm.group(1)] = float(mm.group(2))
                    trect = [vals.get(k) for k in ("x", "y", "width", "height")]
            if name is not None:
                by_name.setdefault(name, []).append((rel, texguid, trect))
    return idx, by_name, n


def match_by_page(by_name, sprite_name, page_path, rect, project):
    """the standalone export of a packed sprite: same name (AssetRipper may add _N), same page texture, same texture rect."""
    page_guid = read_meta_guid(os.path.join(project, page_path)) if page_path else None
    if page_guid is None:
        return []
    out = []
    for nm, ents in by_name.items():
        if nm.strip() != sprite_name.strip() and not re.fullmatch(re.escape(sprite_name.strip()) + r"_\d+", nm.strip()):
            continue
        for rel, tg, tr in ents:
            if tg == page_guid and tr and all(v is not None for v in tr) and all(abs(tr[k] - rect[k]) < 0.01 for k in range(4)):
                out.append(rel)
    return out


def index_pages(project):
    """atlas page texture name -> [(project path, sha1)] from Assets/Texture2D/sactx-*.png (AssetRipper adds _N to duplicates)."""
    idx = {}
    tdir = os.path.join(project, "Assets", "Texture2D")
    for nm in os.listdir(tdir):
        if not nm.startswith("sactx-") or not nm.endswith(".png"):
            continue
        base = nm[:-4]
        m = re.match(r"^(.*)_(\d+)$", base)
        key = m.group(1) if m and m.group(1).startswith("sactx-") and re.search(r"-[0-9a-f]{8}$", m.group(1)) else base
        full = os.path.join(tdir, nm)
        with open(full, "rb") as f:
            sha = hashlib.sha1(f.read()).hexdigest()
        idx.setdefault(key, []).append(("Assets/Texture2D/" + nm, sha))
    return idx


_page_cache = {}
_png_thumbs = {}


def _thumb(img):
    return img.convert("RGBA").resize((64, 64))


def match_page(tex_reader, cands, page_issues, atlas_name):
    key = (id(tex_reader.assets_file), tex_reader.path_id)
    if key in _page_cache:
        return _page_cache[key]
    from PIL import Image, ImageChops, ImageStat
    shipped = _thumb(tex_reader.read().image)
    scores = []
    for path, _sha in cands:
        if path not in _png_thumbs:
            _png_thumbs[path] = _thumb(Image.open(os.path.join(_PROJECT, path)))
        st = ImageStat.Stat(ImageChops.difference(shipped, _png_thumbs[path]))
        scores.append((sum(st.mean) / 4, path))
    scores.sort()
    best, second = scores[0], (scores[1] if len(scores) > 1 else (999, None))
    if best[0] > 2.0 or second[0] - best[0] < 2.0:
        page_issues.append((atlas_name, "texture", tex_reader.path_id, f"page match weak: best {best[0]:.2f} {best[1]}, next {second[0]:.2f}"))
    _page_cache[key] = best[1]
    return best[1]


_PROJECT = None


def main():
    global _PROJECT
    ap = argparse.ArgumentParser()
    ap.add_argument("--aa", required=True)
    ap.add_argument("--containers", required=True)
    ap.add_argument("--map", required=True)
    ap.add_argument("--project", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--report", required=True)
    a = ap.parse_args()
    _PROJECT = a.project

    import UnityPy

    scan = json.load(open(a.containers, encoding="utf-8"))
    bmap = json.load(open(a.map, encoding="utf-8"))["bundles"]
    # setup (W2): a packed sprite with several project copies (AssetRipper exports a shipped sprite once per shipped OBJECT, and the
    # free 1.3.14 export gives the copies the SAME render-data key) resolves BY OBJECT IDENTITY, never by name: when the sprite
    # object the shipped atlas references is a CONTAINER object, the project copy is the file written for that container (the
    # container maps to <dir>/<name>.png; its Sprite is the .asset beside it); otherwise one of the non-container copies.
    pid_to_container = {pid: p.lower() for rec in scan.values() for p, cls, _fid, pid in (rec.get("container") or [])
                        if cls == "Sprite"}
    dev_to_disk = {dev.lower(): disk for b in bmap.values() for dev, disk in b.get("paths", []) if dev and disk}
    container_stems = {os.path.splitext(disk)[0] for disk in dev_to_disk.values()}
    file_by_name = {b["file"]: n for n, b in bmap.items()}
    name_to_file = {n: b["file"] for n, b in bmap.items()}

    sprites, by_name, n_sprites = index_sprites(a.project)
    by_page = [0]
    pages = index_pages(a.project)
    print(f"sprite assets {n_sprites}, rdkeys {len(sprites)}, atlas page names {len(pages)}", flush=True)

    rep = []
    envs = {}

    def load(rel):
        if rel not in envs:
            envs[rel] = UnityPy.load(os.path.join(a.aa, rel))
        return envs[rel]

    cab_index = {}

    def cab_lookup(cab):
        """CAB name -> bundle file; the global index (every bundle's CAB names) is built once and cached on disk."""
        if not cab_index:
            cache = os.path.join(os.path.dirname(a.out), "cab_index.json")
            if os.path.exists(cache):
                cab_index.update(json.load(open(cache, encoding="utf-8")))
            else:
                print("building CAB index over every bundle ...", flush=True)
                for rel2, rec2 in scan.items():
                    if rec2.get("error"):
                        continue
                    e2 = UnityPy.load(os.path.join(a.aa, rel2))
                    for c in e2.cabs.keys():
                        cab_index[c.lower()] = rel2
                    del e2
                json.dump(cab_index, open(cache, "w", encoding="utf-8"))
        return cab_index.get(cab)

    def resolve(obj_reader, pptr, deps_files):
        """PPtr -> ObjectReader (local, or in another bundle by CAB name: the bundle's dependencies first, then the CAB index)."""
        if pptr["m_FileID"] == 0:
            return obj_reader.assets_file.objects.get(pptr["m_PathID"])
        ext = obj_reader.assets_file.externals[pptr["m_FileID"] - 1]
        cab = ext.path.split("/")[-1].lower()
        for rel in list(deps_files) + [None]:
            if rel is None:
                rel = cab_lookup(cab)
                if rel is None:
                    return None
            env = load(rel)
            sf = env.cabs.get(cab)
            if sf is not None and hasattr(sf, "objects"):
                return sf.objects.get(pptr["m_PathID"])
        return None

    atlases = []          # unique atlases
    sig_to_idx = {}
    atlas_at = {}         # (bundle file, atlas pathID) -> atlas index
    unresolved_sprites, dup_sprites, page_issues = [], [], []

    # container atlases first, so an embedded copy reuses the container asset
    order = []
    for rel, rec in sorted(scan.items()):
        if rec.get("error"):
            continue
        cont = {c[3]: c[0] for c in rec["container"] if c[1] == "SpriteAtlas"}
        order.append((0 if cont else 1, rel, cont))
    order.sort()

    for _pri, rel, cont in order:
        rec = scan[rel]
        bname = rec.get("name", "")
        dep_files = [name_to_file[d] for d in rec.get("dependencies", []) if d in name_to_file]
        env = load(rel)
        for o in env.objects:
            if o.type.name != "SpriteAtlas":
                continue
            t = o.read_typetree()
            names = list(t["m_PackedSpriteNamesToIndex"])
            rdmap = []
            for (kw, fid), v in t["m_RenderDataMap"]:
                tex = {}
                for slot in ("texture", "alphaTexture"):
                    pp = v[slot]
                    if pp["m_PathID"] == 0:
                        tex[slot] = None
                        continue
                    to = resolve(o, pp, dep_files)
                    tname = to.read_typetree()["m_Name"] if to is not None else None
                    cands = pages.get(tname, []) if tname else []
                    if not cands:
                        page_issues.append((t["m_Name"], slot, tname, "no PNG"))
                        tex[slot] = None
                    elif len({c[1] for c in cands}) == 1:
                        tex[slot] = sorted(cands)[0][0]
                    else:
                        # a multi-page atlas names every page alike (Unity: sactx-<size>-<format>-<atlas>-<atlas hash>), so the
                        # page is picked by pixels: the shipped texture decoded vs each same-named PNG (64x64 thumbnails, mean |diff|)
                        tex[slot] = match_page(to, cands, page_issues, t["m_Name"])
                rdmap.append({
                    "key": [kw["data[0]"], kw["data[1]"], kw["data[2]"], kw["data[3]"]],
                    "keyText": guid_text([kw["data[0]"], kw["data[1]"], kw["data[2]"], kw["data[3]"]]),
                    "fileID": int(fid),
                    "texture": tex["texture"], "alphaTexture": tex["alphaTexture"],
                    "textureRect": [v["textureRect"][k] for k in ("x", "y", "width", "height")],
                    "textureRectOffset": [v["textureRectOffset"]["x"], v["textureRectOffset"]["y"]],
                    "atlasRectOffset": [v["atlasRectOffset"]["x"], v["atlasRectOffset"]["y"]],
                    "uvTransform": [v["uvTransform"][k] for k in ("x", "y", "z", "w")],
                    "downscaleMultiplier": v["downscaleMultiplier"],
                    "settingsRaw": v["settingsRaw"],
                })
            rd_by_key = {(r["keyText"], r["fileID"]): r for r in rdmap}
            packed = []
            for i, pp in enumerate(t["m_PackedSprites"]):
                so = resolve(o, pp, dep_files)
                if so is None:
                    unresolved_sprites.append((rel, t["m_Name"], i, names[i] if i < len(names) else "?", "PPtr not resolved"))
                    packed.append(None)
                    continue
                st = so.read_typetree()
                (kw, fid) = st["m_RenderDataKey"]
                key = (guid_text([kw["data[0]"], kw["data[1]"], kw["data[2]"], kw["data[3]"]]), int(fid))
                cands = sprites.get(key, [])
                how = "rdkey"
                if not cands and key in rd_by_key:
                    r = rd_by_key[key]
                    cands = match_by_page(by_name, st["m_Name"], r["texture"], r["textureRect"], a.project)
                    how = "page+rect"
                if not cands:
                    unresolved_sprites.append((rel, t["m_Name"], i, st["m_Name"], f"no project sprite with rdkey {key} nor page+rect"))
                    packed.append(None)
                    continue
                if len(cands) > 1:
                    dup_sprites.append((t["m_Name"], st["m_Name"], how, cands))
                if how != "rdkey":
                    by_page[0] += 1
                cpath = pid_to_container.get(so.path_id)
                if cpath and cpath in dev_to_disk:
                    stem = os.path.splitext(dev_to_disk[cpath])[0]
                    pick = sorted(c for c in cands if os.path.splitext(c)[0] == stem)
                else:
                    pick = sorted(c for c in cands if os.path.splitext(c)[0] not in container_stems)
                packed.append(pick[0] if pick else sorted(cands)[0])
            sig = hashlib.sha1(json.dumps([t["m_Name"], t["m_Tag"], t["m_IsVariant"], names, packed, rdmap], sort_keys=True).encode()).hexdigest()
            if sig in sig_to_idx:
                idx = sig_to_idx[sig]
                atlases[idx]["sources"].append([rel, o.path_id])
            else:
                if o.path_id in cont:
                    cpath = cont[o.path_id]
                    # on-disk folder case from a sibling container path of the same bundle that mapped to a file
                    folder = os.path.dirname(cpath)
                    disk_folder = None
                    for dev, disk in bmap.get(bname, {}).get("paths", []):
                        if disk and os.path.dirname(dev).lower() == folder.lower():
                            disk_folder = os.path.dirname(disk)
                            break
                    # the file name carries the shipped m_Name casing: a native asset's main object is named after its file, and
                    # UIManager.FindSpriteAtlasIndex matches asset.name against the master data's atlas names
                    fname = os.path.basename(cpath)
                    if fname.lower() == t["m_Name"].lower() + ".spriteatlas":
                        fname = t["m_Name"] + ".spriteatlas"
                    target = (disk_folder or ("Assets" + folder[6:] if folder.lower().startswith("assets") else folder)) + "/" + fname
                    container = cpath
                else:
                    target = f"Assets/SpriteAtlas/{t['m_Name']}.spriteatlas"
                    container = None
                used = {x["target"].lower() for x in atlases}
                base, n = target[:-len(".spriteatlas")], 1
                while target.lower() in used:
                    target = f"{base}_{n}.spriteatlas"
                    n += 1
                idx = len(atlases)
                sig_to_idx[sig] = idx
                atlases.append({
                    "target": target, "container": container, "bundle": bname,
                    "name": t["m_Name"], "tag": t["m_Tag"], "isVariant": bool(t["m_IsVariant"]),
                    "packedNames": names, "packedSprites": packed, "renderData": rdmap,
                    "sources": [[rel, o.path_id]],
                })
            atlas_at[(rel, o.path_id)] = idx

    # placeholder references -> atlas, by owner GameObject name + field name
    fixes, fix_issues = [], []
    disk_to_bundles = {}
    for n, b in bmap.items():
        for dev, disk in b["paths"]:
            if disk:
                disk_to_bundles.setdefault(disk, []).append(b["file"])
    script_class = {}
    for dirpath, _d, names in os.walk(os.path.join(a.project, "Assets")):
        for nm in names:
            if nm.endswith(".cs.meta"):
                g = read_meta_guid(os.path.join(dirpath, nm[:-5]))
                if g:
                    script_class[g] = nm[:-8]
    files_with_ph = []
    for dirpath, _d, names in os.walk(os.path.join(a.project, "Assets")):
        for nm in names:
            if nm.endswith((".prefab", ".unity", ".asset", ".controller", ".mat")):
                full = os.path.join(dirpath, nm)
                try:
                    with open(full, "r", encoding="utf-8", errors="replace") as f:
                        txt = f.read()
                except OSError:
                    continue
                if "0000000deadbeef15deadf00d0000000" in txt:
                    files_with_ph.append(os.path.relpath(full, a.project).replace("\\", "/"))
    for rel_disk in files_with_ph:
        with open(os.path.join(a.project, rel_disk), "r", encoding="utf-8", errors="replace") as f:
            lines = f.read().split("\n")
        docs, cur = {}, None
        for i, line in enumerate(lines):
            m = re.match(r"^--- !u!(\d+) &(-?\d+)", line)
            if m:
                cur = m.group(2)
                docs[cur] = {"start": i, "cls": m.group(1)}
            elif cur is not None:
                if line.startswith("  m_Name: "):
                    docs[cur].setdefault("name", line[10:])
                elif line.startswith("  m_GameObject: {fileID: "):
                    docs[cur]["go"] = line.split("fileID: ")[1].split("}")[0]
                elif line.startswith("  m_Script: {fileID: ") and "guid: " in line:
                    docs[cur]["script"] = script_class.get(line.split("guid: ")[1][:32])
        # bundle-side owners of atlas references
        owners = []
        for brel in disk_to_bundles.get(rel_disk, []):
            env = load(brel)
            for o in env.objects:
                if o.type.name != "MonoBehaviour":
                    continue
                try:
                    t = o.read_typetree()
                except Exception:
                    continue
                hits = []

                def walk(v, path):
                    if isinstance(v, dict):
                        if set(v.keys()) == {"m_FileID", "m_PathID"}:
                            if v["m_PathID"] != 0:
                                target = None
                                if v["m_FileID"] == 0:
                                    target = atlas_at.get((brel, v["m_PathID"]))
                                else:
                                    to = resolve(o, v, [name_to_file[d] for d in scan[brel].get("dependencies", []) if d in name_to_file])
                                    if to is not None and to.type.name == "SpriteAtlas":
                                        for (r2, pid), ix in atlas_at.items():
                                            if pid == to.path_id:
                                                target = ix
                                                break
                                if target is not None:
                                    hits.append((path, target))
                            return
                        for k, x in v.items():
                            walk(x, k)
                    elif isinstance(v, list):
                        for x in v:
                            walk(x, path)
                walk(t, "")
                if hits:
                    go = o.read_typetree()["m_GameObject"]
                    goo = o.assets_file.objects.get(go["m_PathID"]) if go["m_FileID"] == 0 else None
                    goname = goo.read_typetree()["m_Name"] if goo is not None else None
                    msr = resolve(o, t["m_Script"], [name_to_file[d] for d in scan[brel].get("dependencies", []) if d in name_to_file])
                    cls = msr.read_typetree().get("m_ClassName") if msr is not None else None
                    for field, ix in hits:
                        owners.append((goname, field, ix, brel, cls))
        for i, line in enumerate(lines):
            if DEADBEEF not in line:
                continue
            m = re.match(r"^(\s*)(-\s*)?([A-Za-z_][A-Za-z0-9_]*)?:?", line)
            field = line.strip().split(":")[0] if not line.strip().startswith("-") else None
            if field is None:  # array element: the field is the nearest less-indented key above
                ind = len(line) - len(line.lstrip())
                for j in range(i - 1, -1, -1):
                    lj = lines[j]
                    if lj.strip() and (len(lj) - len(lj.lstrip())) <= ind and not lj.strip().startswith("-"):
                        field = lj.strip().split(":")[0]
                        break
            doc = None
            for did, d in docs.items():
                if d["start"] < i and (doc is None or d["start"] > docs[doc]["start"]):
                    doc = did
            goname = docs.get(docs[doc].get("go"), {}).get("name") if doc and "go" in docs[doc] else None
            scls = docs[doc].get("script") if doc else None
            cands = sorted({ix for (gn, f, ix, _b, c) in owners if f == field and gn == goname and c == scls})
            if len(cands) != 1:
                c2 = sorted({ix for (gn, f, ix, _b, c) in owners if f == field and gn == goname})
                c3 = sorted({ix for (gn, f, ix, _b, c) in owners if f == field})
                cands = c2 if len(c2) == 1 else (c3 if len(c3) == 1 else cands)
            if len(cands) == 1:
                fixes.append({"file": rel_disk, "line": i + 1, "field": field, "gameObject": goname, "script": scls, "atlas": cands[0],
                              "text": line})
            else:
                fix_issues.append((rel_disk, i + 1, field, goname, scls, [atlases[c]["target"] for c in cands],
                                   sorted({(gn, f, c, atlases[ix]["target"]) for (gn, f, ix, _b, c) in owners})[:6]))

    spec = {"atlases": atlases, "fixes": fixes}
    with open(a.out, "w", encoding="utf-8") as f:
        json.dump(spec, f, indent=1, ensure_ascii=False)

    with open(a.report, "w", encoding="utf-8") as r:
        n_obj = sum(len(x["sources"]) for x in atlases)
        r.write(f"SpriteAtlas objects {n_obj} -> unique atlases {len(atlases)} "
                f"(container {sum(1 for x in atlases if x['container'])}, embedded {sum(1 for x in atlases if not x['container'])})\n")
        r.write(f"packed sprites {sum(len(x['packedSprites']) for x in atlases)}  unresolved {len(unresolved_sprites)}  "
                f"duplicate rdkeys {len(dup_sprites)}\n")
        r.write(f"render data entries {sum(len(x['renderData']) for x in atlases)}  page issues {len(page_issues)}\n")
        r.write(f"placeholder refs: files {len(files_with_ph)}  fixed {len(fixes)}  UNMATCHED {len(fix_issues)}\n\n")
        for x in atlases:
            r.write(f"{x['target']}  name={x['name']} tag={x['tag']} variant={x['isVariant']} packed={len(x['packedSprites'])} "
                    f"rd={len(x['renderData'])} sources={len(x['sources'])}\n")
        r.write("\nunresolved packed sprites:\n")
        for u in unresolved_sprites:
            r.write(f"  {u}\n")
        r.write("\nduplicate rdkeys (resolved by object identity: the container copy for a container object, else a non-container copy):\n")
        for d in dup_sprites:
            r.write(f"  {d}\n")
        r.write("\npage issues:\n")
        for p in page_issues:
            r.write(f"  {p}\n")
        r.write("\nplaceholder fixes:\n")
        for fx in fixes:
            r.write(f"  {fx['file']}:{fx['line']}  {fx['field']} on '{fx['gameObject']}' -> {atlases[fx['atlas']]['target']}\n")
        r.write("\nUNMATCHED placeholders:\n")
        for u in fix_issues:
            r.write(f"  {u}\n")
    print(f"atlases {len(atlases)}, unresolved sprites {len(unresolved_sprites)}, page issues {len(page_issues)}, "
          f"fixes {len(fixes)}, unmatched {len(fix_issues)} -> {a.out}", flush=True)


if __name__ == "__main__":
    sys.exit(main())
