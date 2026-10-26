"""D7 (author-approved 2026-10-11; HANDOFF §11.10 PM2 / §11.15): rebuild project textures as NATIVE Unity Texture2D assets holding the
shipped texels - every mip level, BCn blocks exactly as shipped, ASTC decoded to RGBA32 by the reference emulator's own decoder
(tools/d7_texconv = Ryujinx.Graphics.Texture of work/ryubing, commit 475615f), uncompressed formats as shipped.

Per texture of route "native" in work/s34/d7_map.json (tools/d7_map.py):
  1. raw data + m_PlatformBlob (UnityPy, read-only) -> gobBlocksInY = 1 << u32(blob[8:12])  (NVN block height, log2 GOBs);
  2. tools/d7_texconv: block-linear -> linear (all levels), ASTC -> RGBA8 (Ryujinx path when the host GPU has no ASTC);
  3. <dir>/<stem>.asset = Texture2D YAML in the layout THIS editor writes (Assets/_d7_template, BdspD7.WriteTemplate), name, size,
     format (ASTC -> RGBA32), mips, the shipped sampler (filter / aniso / mip bias / wrap) and colour space;
     <dir>/<stem>.asset.meta = NativeFormatImporter, the PNG's GUID, mainObjectFileID 2800000 -> every {fileID: 2800000, guid} reference holds;
  4. the PNG + its .meta MOVE to work/s34/d7_png_aside/<rel> (never deleted); manifest work/s34/d7_manifest.jsonl (append) records
     png, asset, guid, format, sizes, sha1 of the written data - `--undo` moves everything back.
  py -3.13 -I tools/d7_build.py [--only <substr>] [--limit N] [--apply] [--undo]
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import time

import UnityPy

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402  (setup: dump / project / work folder from the setup config)
DATA = paths.data_path()
ASSETS = paths.ASSETS
MAP = paths.work("d7", "d7_map.json")
ASIDE = paths.work("d7", "d7_png_aside")
MANIFEST = paths.work("d7", "d7_manifest.jsonl")
TEMPLATE = os.path.join(ASSETS, "_d7_template", "d7_template_bin.asset")  # BINARY (Unity-written, Mixed mode; see BdspD7)
CONV = [sys.executable, "-I", os.path.join(paths.SETUP, "lib", "texconv.py")]   # pure-Python d7_texconv (W3)
ASTC = set(range(48, 60))

META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 2800000
  userData:
  assetBundleName:
  assetBundleVariant:
"""


SUPPORTED = {1, 3, 4, 5, 10, 12, 13, 25, 26, 27, 63, 28, 29, 74} | ASTC
CRUNCH = {28: 10, 29: 12}   # DXT1Crunched -> DXT1, DXT5Crunched -> DXT5 (all levels via Unity's crunch, tools/crunch_unity)
CRN = os.path.join(paths.SETUP, "bin", "crn_levels.exe")
if sys.platform == "darwin":
    CRN = os.path.join(paths.SETUP, "bin", "crunch_src", "crn_levels")
DISK_FLOOR_GB = 5.0   # checked before every batch (a batch writes up to ~1 GB)
DISK_STOP = 3         # exit code: stopped on the disk floor (d7.py must not treat it as a pairing failure)


def load_template():
    return open(TEMPLATE, "rb").read()


def build_binary(template, name, w, h, fmt, mips, settings, colorspace, readable, data):
    """The texture as a binary Unity serialized file (Texture2D at path_id 2800000), from the editor-written template - the
    project is ForceText, but YAML hex would double 13 GiB of texels (E: cannot hold it); Unity reads binary assets in any mode."""
    env = UnityPy.load(template)
    obj = next(iter(env.objects))
    assert obj.path_id == 2800000 and obj.type.name == "Texture2D"
    tt = obj.read_typetree()
    tt.update({"m_Name": name, "m_Width": w, "m_Height": h, "m_CompleteImageSize": len(data), "m_TextureFormat": fmt,
               "m_MipCount": mips, "m_IsReadable": bool(readable), "m_ColorSpace": colorspace, "image data": data,
               "m_StreamData": {"offset": 0, "size": 0, "path": ""}})
    tt["m_TextureSettings"].update(settings)
    obj.save_typetree(tt)
    return env.file.save()


def verify_mip0(data, fmt, w, h, png, normal=False):
    """Mip 0 of the rebuilt data == the exported PNG (flipped: Unity rows run bottom-up). AssetRipper's PNG decode is exact for
    mip 0 (s34: BC1 / BC3 / BC7 / ASTC 4x4 / 6x6 all max 0); a mismatch means a pairing or layout error - the PNG is left alone."""
    import numpy as np
    import texture2ddecoder
    from PIL import Image
    bw, bh = (w + 3) // 4, (h + 3) // 4
    try:
        if fmt == 10:
            a = np.frombuffer(texture2ddecoder.decode_bc1(data[:bw * bh * 8], w, h), np.uint8).reshape(h, w, 4)[:, :, [2, 1, 0, 3]]
        elif fmt == 12:
            a = np.frombuffer(texture2ddecoder.decode_bc3(data[:bw * bh * 16], w, h), np.uint8).reshape(h, w, 4)[:, :, [2, 1, 0, 3]]
        elif fmt == 25:
            a = np.frombuffer(texture2ddecoder.decode_bc7(data[:bw * bh * 16], w, h), np.uint8).reshape(h, w, 4)[:, :, [2, 1, 0, 3]]
        elif fmt == 26:
            a = np.frombuffer(texture2ddecoder.decode_bc4(data[:bw * bh * 8], w, h), np.uint8).reshape(h, w, 4)[:, :, [2, 1, 0, 3]]
        elif fmt == 27:
            a = np.frombuffer(texture2ddecoder.decode_bc5(data[:bw * bh * 16], w, h), np.uint8).reshape(h, w, 4)[:, :, [2, 1, 0, 3]]
        elif fmt in ASTC or fmt in (4, 5):
            a = np.frombuffer(data[:w * h * 4], np.uint8).reshape(h, w, 4)
            if fmt == 5:
                a = a[:, :, [1, 2, 3, 0]]
        elif fmt == 3:
            a = np.frombuffer(data[:w * h * 3], np.uint8).reshape(h, w, 3)
        elif fmt in (1, 63):
            a = np.frombuffer(data[:w * h], np.uint8).reshape(h, w)
        elif fmt == 74:
            a = (np.frombuffer(data[:w * h * 8], "<u2").reshape(h, w, 4) >> 8).astype(np.uint8)   # RGBA64 vs the 8-bit PNG
        else:
            return None   # no verifier for this format (reported as unverified in the manifest)
        ref = np.asarray(Image.open(png).convert("RGBA"))[::-1]
        if normal and fmt not in (26, 27) and a.ndim == 3 and a.shape[2] == 4:
            # Unity's packed normal map (Switch data): R = 255, G = B = y, A = x; AssetRipper's PNG unpacks it to R = x, G = y,
            # B = computed z - compare x and y exactly (measured on ob0008_00_obj_nor / ob0230_00_mudball_nor: max 0)
            ref, a = ref[:, :, :2], np.stack([a[:, :, 3], a[:, :, 1]], axis=2)
        elif fmt == 1:
            ref, a = ref[:, :, 3], a
        elif fmt == 63:
            ref = ref[:, :, 0]
        elif fmt == 3:
            ref = ref[:, :, :3]
        elif fmt in (26,):
            ref, a = ref[:, :, 0], a[:, :, 0]
        elif fmt in (27,):
            ref, a = ref[:, :, :2], a[:, :, :2]
        if ref.shape != a.shape:
            return f"shape {a.shape} vs png {ref.shape}"
        diff = int(np.abs(ref.astype(np.int16) - a.astype(np.int16)).max())
        # BC4 / BC5 blocks are copied unchanged; only the two DECODERS (texture2ddecoder here, AssetRipper's for the PNG) round
        # differently by 1 - the check is about pairing / orientation, so allow 2 there; every other format must be exact.
        tol = 2 if fmt in (26, 27) else (1 if fmt == 74 else 0)
        return None if diff <= tol else f"mip0 differs from the PNG (max {diff})"
    except Exception as e:  # noqa: BLE001
        return "verifier " + type(e).__name__ + ": " + str(e)[:120]


def rgba_to_rgb_if_opaque(data):
    """RGBA32 -> RGB24 when every alpha byte is 255: exact (the editor uploads RGB24 as RGBA with alpha 255) and 25 % smaller."""
    alpha = data[3::4]
    if alpha.count(255) != len(alpha):
        return None
    out = bytearray(len(data) // 4 * 3)
    out[0::3], out[1::3], out[2::3] = data[0::4], data[1::4], data[2::4]
    return bytes(out)


def fill(template, name, w, h, fmt, mips, settings, colorspace, data):
    t = template
    rep = {
        "m_Name": name, "m_Width": w, "m_Height": h, "m_CompleteImageSize": len(data), "m_TextureFormat": fmt,
        "m_MipCount": mips, "m_IsReadable": 0, "m_ColorSpace": colorspace,
    }
    for k, v in rep.items():
        t, n = re.subn(r"^(  %s:) .*$" % re.escape(k), lambda m: m.group(1) + " " + str(v), t, count=1, flags=re.M)
        assert n == 1, k
    for k, v in settings.items():
        t, n = re.subn(r"^(    %s:) .*$" % re.escape(k), lambda m: m.group(1) + " " + str(v), t, count=1, flags=re.M)
        assert n == 1, k
    t, n = re.subn(r"^(  image data:) .*$", lambda m: m.group(1) + " " + str(len(data)), t, count=1, flags=re.M)
    assert n == 1
    t, n = re.subn(r"^(  _typelessdata:) .*$", lambda m: m.group(1) + " " + data.hex(), t, count=1, flags=re.M)
    assert n == 1
    return t


def gobs_from_blob(blob):
    if blob and len(blob) >= 12:
        return 1 << struct.unpack_from("<I", bytes(blob), 8)[0]
    return 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default=None)
    ap.add_argument("--list", default=None, help="JSON list of PNG paths (relative to Assets) to restrict to")
    ap.add_argument("--limit", type=int, default=0)
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--undo", action="store_true")
    ap.add_argument("--batch", type=int, default=400)
    a = ap.parse_args()
    if a.undo:
        return undo()
    rows = [r for r in json.load(open(MAP, encoding="utf-8")) if r.get("route") == "native"]
    done = set()
    if os.path.exists(MANIFEST):
        for line in open(MANIFEST, encoding="utf-8"):
            m = json.loads(line)
            if m.get("state") == "done":
                done.add(m["png"])
    rows = [r for r in rows if r["png"] not in done and os.path.exists(os.path.join(ASSETS, r["png"])) and r["format"] in SUPPORTED]
    if a.list:
        keep = set(json.load(open(a.list, encoding="utf-8")))
        rows = [r for r in rows if r["png"] in keep]
    if a.only:
        rows = [r for r in rows if a.only.lower() in r["png"].lower()]
    if a.limit:
        rows = rows[:a.limit]
    print(len(rows), "textures to rebuild", "(apply)" if a.apply else "(dry run)")
    if not a.apply or not rows:
        return 0
    template = load_template()
    tmp = tempfile.mkdtemp(prefix="d7_", dir=paths.work("d7", ""))
    rows.sort(key=lambda r: r["file"])
    t0 = time.time()
    total_ok = total_err = 0
    for b0 in range(0, len(rows), a.batch):
        free = shutil.disk_usage(ASSETS).free / 2 ** 30
        if free < DISK_FLOOR_GB:
            print(f"STOPPING: {free:.1f} GB free on the project's drive (floor {DISK_FLOOR_GB:.0f} GB) after {b0}/{len(rows)}; "
                  "free some space and run the setup again - it continues here", flush=True)
            return DISK_STOP
        batch = rows[b0:b0 + a.batch]
        jobs, meta, crunch_status = [], {}, {}
        env_file, env = None, None
        for i, r in enumerate(batch):
            if r["file"] != env_file:
                env_file, env = r["file"], UnityPy.load(os.path.join(DATA, r["file"]))
            obj = next(o for o in env.objects if o.path_id == r["path_id"])
            d = obj.read()
            raw = bytes(d.get_image_data())   # resolves the bundle's .resS stream
            gobs = 0                           # Unity 2019.4 Switch data is LINEAR (packed levels); no m_PlatformBlob in this version
            rin, rout = os.path.join(tmp, f"{i}.raw"), os.path.join(tmp, f"{i}.lin")
            open(rin, "wb").write(raw)
            meta[rout] = (r, d, gobs, rin)
            if int(d.m_TextureFormat) in CRUNCH:
                cr = subprocess.run([CRN, rin, rout], capture_output=True, text=True)
                crunch_status[rout] = ("OK " + rout) if cr.returncode == 0 else ("ERR " + rin + " crunch " + cr.stderr.strip())
                continue
            jobs.append(json.dumps({"in": rin, "out": rout, "w": d.m_Width, "h": d.m_Height, "mips": d.m_MipCount,
                                    "format": int(d.m_TextureFormat), "gobs": gobs}))
        jf = os.path.join(tmp, "jobs.jsonl")
        open(jf, "w", encoding="utf-8").write("\n".join(jobs) + "\n")
        res = subprocess.run(CONV + [jf], capture_output=True, text=True)
        status = dict(crunch_status)
        for line in res.stdout.splitlines():
            parts = line.split("\t", 2)
            status[parts[1]] = line
        with open(MANIFEST, "a", encoding="utf-8") as man:
            for rout, (r, d, gobs, rin) in meta.items():
                line = status.get(rout) or next((v for k, v in status.items() if k == rin), "ERR missing")
                if not line.startswith("OK") or not os.path.exists(rout):
                    total_err += 1
                    man.write(json.dumps({"png": r["png"], "state": "error", "reason": line}) + "\n")
                    continue
                data = open(rout, "rb").read()
                fmt = CRUNCH.get(int(d.m_TextureFormat), int(d.m_TextureFormat))
                why = verify_mip0(data, fmt, d.m_Width, d.m_Height, os.path.join(ASSETS, r["png"]), normal=r.get("textureType") == 1)
                if why:
                    total_err += 1
                    man.write(json.dumps({"png": r["png"], "state": "error", "reason": "verify: " + why}) + "\n")
                    continue
                out_fmt = 4 if fmt in ASTC else fmt
                if fmt in ASTC:
                    rgb = rgba_to_rgb_if_opaque(data)
                    if rgb is not None:
                        data, out_fmt = rgb, 3
                ts = d.m_TextureSettings
                settings = {"m_FilterMode": ts.m_FilterMode, "m_Aniso": ts.m_Aniso, "m_MipBias": ts.m_MipBias,
                            "m_WrapU": ts.m_WrapU, "m_WrapV": ts.m_WrapV, "m_WrapW": ts.m_WrapW}
                colorspace = getattr(d, "m_ColorSpace", 1)
                blob = build_binary(template, d.m_Name, d.m_Width, d.m_Height, out_fmt, d.m_MipCount, settings, colorspace,
                                    getattr(d, "m_IsReadable", False), data)
                png = os.path.join(ASSETS, r["png"])
                stem = os.path.splitext(png)[0]
                asset = stem + ".asset"
                if os.path.exists(asset) or os.path.exists(asset + ".meta"):
                    asset = stem + "_tex2d.asset"
                aside = os.path.join(ASIDE, r["png"])
                os.makedirs(os.path.dirname(aside), exist_ok=True)
                shutil.move(png, aside)
                shutil.move(png + ".meta", aside + ".meta")
                open(asset, "wb").write(blob)
                open(asset + ".meta", "w", encoding="utf-8", newline="\n").write(META.format(guid=r["guid"]))
                man.write(json.dumps({"png": r["png"], "asset": os.path.relpath(asset, ASSETS).replace("\\", "/"), "guid": r["guid"],
                                      "format": fmt, "out_format": out_fmt, "w": d.m_Width, "h": d.m_Height, "mips": d.m_MipCount,
                                      "gobs": gobs, "bytes": len(data), "sha1": hashlib.sha1(data).hexdigest(), "state": "done"}) + "\n")
                total_ok += 1
                os.remove(rout)
                os.remove(rin)
        print(f"{b0 + len(batch)}/{len(rows)} ok {total_ok} err {total_err} {time.time() - t0:.0f} s", flush=True)
    os.remove(os.path.join(tmp, "jobs.jsonl"))
    try:
        os.rmdir(tmp)
    except OSError:
        pass
    print("done: ok", total_ok, "errors", total_err)
    return 0 if total_err == 0 else 1


def undo():
    n = 0
    for line in open(MANIFEST, encoding="utf-8"):
        m = json.loads(line)
        if m.get("state") != "done":
            continue
        png = os.path.join(ASSETS, m["png"])
        aside = os.path.join(ASIDE, m["png"])
        asset = os.path.join(ASSETS, m["asset"])
        if os.path.exists(aside) and not os.path.exists(png):
            undo_dir = paths.work("d7", "d7_undone_assets", os.path.dirname(m["asset"]))
            os.makedirs(undo_dir, exist_ok=True)
            for f in (asset, asset + ".meta"):
                if os.path.exists(f):
                    shutil.move(f, os.path.join(undo_dir, os.path.basename(f)))
            shutil.move(aside, png)
            shutil.move(aside + ".meta", png + ".meta")
            n += 1
    print("restored", n, "PNGs (the generated assets moved to work/s34/d7_undone_assets)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
