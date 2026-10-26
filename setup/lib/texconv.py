#!/usr/bin/env python3
"""D7 texture conversion in pure Python - replaces tools/d7_texconv (a .NET tool on Ryujinx's texture code) for the setup (W3).

Same interface: a JSON-lines job file, one object per texture
    {"in": raw.bin, "out": out.bin, "w": W, "h": H, "mips": N, "format": UnityTextureFormat, "gobs": gobBlocksInY}
and one stdout line per job: "OK <out> <bytes>[ astc]" or "ERR <in> <reason>".
- BDSP's Unity 2019.4 Switch textures are stored LINEAR (tightly packed levels, gobs = 0): the data is copied unchanged, after the
  size check d7_texconv made. Block-linear input (gobs > 0) never occurs and is refused.
- ASTC is decoded to RGBA8, level by level, with texture2ddecoder - measured byte-identical to d7_texconv on all 10,663 ASTC
  textures of the game (tools/setup/w3_astc_test.py).

  python -I setup/lib/texconv.py <jobs.jsonl>
"""
import json
import sys

import numpy as np
import texture2ddecoder

FORMATS = {1: (1, 1, 1), 63: (1, 1, 1), 3: (1, 1, 3), 4: (1, 1, 4), 5: (1, 1, 4), 13: (1, 1, 2), 74: (1, 1, 8),
           10: (4, 4, 8), 26: (4, 4, 8), 12: (4, 4, 16), 25: (4, 4, 16), 27: (4, 4, 16)}
ASTC = {48: (4, 4), 49: (5, 5), 50: (6, 6), 51: (8, 8), 52: (10, 10), 53: (12, 12),
        54: (4, 4), 55: (5, 5), 56: (6, 6), 57: (8, 8), 58: (10, 10), 59: (12, 12)}


def convert(job):
    w, h, mips, fmt = job["w"], job["h"], max(1, job["mips"]), job["format"]
    if job["gobs"]:
        raise ValueError("block-linear data (gobs %d) is not supported" % job["gobs"])
    if fmt in ASTC:
        bw, bh = ASTC[fmt]
        bpp = 16
    elif fmt in FORMATS:
        bw, bh, bpp = FORMATS[fmt]
    else:
        raise ValueError("format %d" % fmt)
    raw = open(job["in"], "rb").read()
    levels, off, lw, lh = [], 0, w, h
    for _ in range(mips):
        n = ((lw + bw - 1) // bw) * ((lh + bh - 1) // bh) * bpp
        levels.append((off, n, lw, lh))
        off += n
        lw, lh = max(1, lw >> 1), max(1, lh >> 1)
    if len(raw) != off:
        raise ValueError("data %d != packed linear size %d" % (len(raw), off))
    if fmt not in ASTC:
        return raw, False
    out = []
    for o, n, lw, lh in levels:
        bgra = texture2ddecoder.decode_astc(raw[o:o + n], lw, lh, bw, bh)
        out.append(np.frombuffer(bgra, np.uint8).reshape(-1, 4)[:, [2, 1, 0, 3]].tobytes())
    return b"".join(out), True


def main():
    ok = err = 0
    for line in open(sys.argv[1], encoding="utf-8"):
        if not line.strip():
            continue
        job = json.loads(line)
        try:
            data, astc = convert(job)
            open(job["out"], "wb").write(data)
            print("OK\t%s\t%d%s" % (job["out"], len(data), " astc" if astc else ""), flush=True)
            ok += 1
        except Exception as e:      # noqa: BLE001 - reported per texture, like d7_texconv
            print("ERR\t%s\t%s: %s" % (job["in"], type(e).__name__, e), flush=True)
            err += 1
    print("texconv: %d ok, %d errors" % (ok, err), file=sys.stderr)
    return 0 if err == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
