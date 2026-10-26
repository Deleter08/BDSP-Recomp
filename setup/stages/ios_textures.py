#!/usr/bin/env python3
"""iOS textures: the ASTC copies of the DXT/BC native textures into Assets/ for ONE iOS Unity step, the originals back after it.

The ripped textures are NativeFormatImporter .asset files with the shipped DXT1/DXT5/BC4/BC5/BC7 (Texture2D) and BC6H (Cubemap) data;
Unity does not re-encode those per platform and BuildPipeline refuses them for iOS. build_ios.py's `textures` step
(BdspWinBuild.WinBuild.IosConvertTextures) writes an ASTC copy of each into <work>/build/ios_textures/files/<Assets/... path> (same
GUID, fileID, name, size, mips, sampler) and index.tsv (path, source size, source write time in .NET ticks, formats, PSNR).
The bundles and player steps run between swap_in() and restore():
  swap_in   every source must still be the file that was converted (size + write time = index) - else stop (re-run `textures`).
            swap.json is written FIRST (each original's inode, size, mtime). Per file: APFS clone of the copy -> <path>.ios_astc_tmp,
            the original RENAMED to ios_textures/swap_orig/<path> (same volume: no bytes copied or rewritten), the clone renamed
            to <path>. The clone gets the current time, so the editor's start sees a changed file.
  restore   every original renamed back, then checked: same inode + size + mtime as recorded = the very same file, byte-identical
            by construction. swap.json is removed only when all match. Safe to run at any point of an interrupted swap_in, and
            run again: build_ios.py calls it at every start (and --back-to-mac), so a killed run leaves nothing behind.
The editor is never running during either (build_ios.py runs Unity between them; a running editor on this project = refuse).
  python -I setup/stages/ios_textures.py [--status | --swap-in | --restore]
"""
import argparse
import ctypes
import ctypes.util
import json
import os
import shutil
import subprocess
import sys
import time

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402

ROOT = paths.work("build", "ios_textures")
FILES = os.path.join(ROOT, "files")
INDEX = os.path.join(ROOT, "index.tsv")
JOURNAL = os.path.join(ROOT, "swap.json")
ORIG = os.path.join(ROOT, "swap_orig")
TMP = ".ios_astc_tmp"
EPOCH_TICKS = 621355968000000000      # .NET DateTime ticks of 1970-01-01 (the index's write times are C# LastWriteTimeUtc.Ticks)

_libc = ctypes.CDLL(ctypes.util.find_library("c"), use_errno=True)


def clone(src, dst):
    """APFS clone (copy-on-write, no data written); a plain copy where the volume cannot clone."""
    if _libc.clonefile(os.fsencode(src), os.fsencode(dst), 0) != 0:
        shutil.copyfile(src, dst)


def ticks(st):
    return st.st_mtime_ns // 100 + EPOCH_TICKS


def read_index():
    out = {}
    if os.path.exists(INDEX):
        for line in open(INDEX, encoding="utf-8"):
            c = line.rstrip("\n").split("\t")
            if not line.startswith("#") and len(c) >= 10:
                out[c[0]] = (int(c[1]), int(c[2]), c[3], c[4])
    return out


def editor_on_project():
    ps = subprocess.run(["ps", "-axo", "pid=,command="], capture_output=True, text=True).stdout
    return [l.strip() for l in ps.splitlines() if "Unity.app/Contents/MacOS/Unity" in l and paths.PROJECT in l]


def write_journal(j):
    tmp = JOURNAL + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(j, f)
        f.flush()
        os.fsync(f.fileno())
    os.replace(tmp, JOURNAL)


def swap_in():
    restore()                                                           # a leftover swap first
    running = editor_on_project()
    if running:
        sys.exit("ios_textures: an editor has this project open - quit it first:\n  " + "\n  ".join(running))
    idx = read_index()
    if not idx:
        sys.exit("ios_textures: no ASTC copies (%s) - run the textures step: sh setup/build_ios.sh --from textures" % INDEX)
    entries, stale = [], []
    for rel, (size, tk, _, _) in sorted(idx.items()):
        src = os.path.join(paths.PROJECT, rel)
        if not os.path.exists(src):
            continue                                                    # gone from the project: nothing to replace
        st = os.stat(src)
        if st.st_size != size or ticks(st) != tk or not os.path.exists(os.path.join(FILES, rel)):
            stale.append(rel)
            continue
        entries.append({"rel": rel, "ino": st.st_ino, "size": st.st_size, "mtime_ns": st.st_mtime_ns})
    if stale:
        sys.exit("ios_textures: %d sources changed since they were converted (first: %s) - run: sh setup/build_ios.sh --from textures"
                 % (len(stale), ", ".join(stale[:3])))
    t0 = time.time()
    write_journal({"state": "active", "time": time.strftime("%Y-%m-%d %H:%M:%S"), "entries": entries})
    now = time.time()
    for e in entries:
        src = os.path.join(paths.PROJECT, e["rel"])
        bak = os.path.join(ORIG, e["rel"])
        tmp = src + TMP
        if os.path.exists(tmp):
            os.remove(tmp)
        clone(os.path.join(FILES, e["rel"]), tmp)
        os.utime(tmp, (now, now))
        os.makedirs(os.path.dirname(bak), exist_ok=True)
        os.rename(src, bak)
        os.rename(tmp, src)
    print("ios_textures: %d ASTC copies in Assets (originals in %s) in %.1f s" % (len(entries), ORIG, time.time() - t0), flush=True)


def restore():
    """Idempotent: puts every original back and proves it; no journal = nothing to do."""
    if not os.path.exists(JOURNAL):
        return True
    j = json.load(open(JOURNAL, encoding="utf-8"))
    entries = j["entries"]
    t0 = time.time()
    moved, bad = 0, []
    for e in entries:
        src = os.path.join(paths.PROJECT, e["rel"])
        bak = os.path.join(ORIG, e["rel"])
        if os.path.exists(src + TMP):
            os.remove(src + TMP)
        if os.path.exists(bak):
            os.replace(bak, src)                                         # the ASTC clone at src goes; its source stays in FILES
            moved += 1
        st = os.stat(src) if os.path.exists(src) else None
        if st is None or (st.st_ino, st.st_size, st.st_mtime_ns) != (e["ino"], e["size"], e["mtime_ns"]):
            bad.append(e["rel"])
    if bad:
        sys.exit("ios_textures: RESTORE INCOMPLETE - %d of %d originals are not back as recorded (first: %s). The journal %s and %s are "
                 "kept; nothing was deleted." % (len(bad), len(entries), ", ".join(bad[:3]), JOURNAL, ORIG))
    shutil.rmtree(ORIG, ignore_errors=True)
    os.remove(JOURNAL)
    print("ios_textures: originals back: %d renamed back, all %d verified (same inode, size, mtime) in %.1f s (swap of %s)"
          % (moved, len(entries), time.time() - t0, j.get("time", "?")), flush=True)
    return True


def status():
    idx = read_index()
    stale = 0
    for rel, (size, tk, _, _) in idx.items():
        src = os.path.join(paths.PROJECT, rel)
        if os.path.exists(src) and not os.path.exists(JOURNAL):
            st = os.stat(src)
            stale += st.st_size != size or ticks(st) != tk
    by = {}
    for v in idx.values():
        by[v[2] + "->" + v[3]] = by.get(v[2] + "->" + v[3], 0) + 1
    print("ios_textures: %d ASTC copies (%s); %s; swap %s" % (
        len(idx), ", ".join("%s %d" % kv for kv in sorted(by.items())),
        "sources changed since conversion: %d" % stale if not os.path.exists(JOURNAL) else "staleness not checked while swapped",
        "ACTIVE (ASTC copies are in Assets - run --restore)" if os.path.exists(JOURNAL) else "none (Assets has the originals)"))


def main():
    ap = argparse.ArgumentParser()
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--swap-in", action="store_true")
    g.add_argument("--restore", action="store_true")
    g.add_argument("--status", action="store_true")
    a = ap.parse_args()
    if a.swap_in:
        swap_in()
    elif a.restore:
        restore()
    status()


if __name__ == "__main__":
    main()
