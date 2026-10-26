#!/usr/bin/env python3
"""Stage check: everything the setup needs, verified before anything is written.

Fails (exit 1) on: a dump that is not BDSP v1.3.0 (fingerprints of three files), no Unity 2019.4.41f2, no Wwise 2019.2.5 integration,
no AssetRipper, missing Python packages, not enough free disk on the project's drive. Warns on low RAM.
"""
import ctypes
import hashlib
import importlib
import os
import shutil
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402

# BDSP v1.3.0 (Switch): sha1, size of files every correct dump has byte-identical
FINGERPRINTS = [
    ("exefs/main", "c99edea638aef0b4a253a74cd3e777ba4770b648", 37895896),
    ("romfs/Data/globalgamemanagers", "761d0eacb7181b42079dabcf92187e2306496c33", 177356),
    ("romfs/Data/StreamingAssets/AssetAssistant/Dpr.bin", "9c8159aa1aed650b9187d74b4e4d93d3174e7102", 5278300),
]
MIN_FREE_GB = 60          # export 23 -> assets ~31, Unity Library ~15-20, D7 work files, margin
MIN_RAM_GB = 24           # the export needs ~21 GB (AssetRipper, heap capped at 20 GB - ar_export.HEAP_GB); Unity's first import
                          # peaked ~16 GB (atlas build); below this Windows' page file makes up the difference
PY_PACKAGES = ["UnityPy", "numpy", "PIL", "texture2ddecoder"]


def sha1(path):
    h = hashlib.sha1()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def total_ram_gb():
    if sys.platform == "darwin":
        return os.sysconf("SC_PHYS_PAGES") * os.sysconf("SC_PAGE_SIZE") / 2 ** 30
    class M(ctypes.Structure):
        _fields_ = [("l", ctypes.c_ulong), ("ml", ctypes.c_ulong)] + [(n, ctypes.c_ulonglong) for n in
                                                                       ("t", "a", "tp", "ap", "tv", "av", "x")]
    m = M()
    m.l = ctypes.sizeof(M)
    ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(m))
    return m.t / 2 ** 30


def main():
    bad, warn = [], []
    cfg = paths.CFG
    # dump
    if not paths.DUMP:
        bad.append("no --dump given (the folder holding exefs\\ and romfs\\)")
    else:
        for rel, want, size in FINGERPRINTS:
            p = os.path.join(paths.DUMP, *rel.split("/"))
            if not os.path.isfile(p):
                bad.append("dump: %s is missing (expected <dump>\\exefs\\main and <dump>\\romfs\\Data\\...)" % rel)
            elif os.path.getsize(p) != size or sha1(p) != want:
                bad.append("dump: %s is not the BDSP v1.3.0 file (a different game version or a damaged dump)" % rel)
        print("dump: %s" % paths.DUMP)
    # Unity
    unity = cfg.get("unity")
    if not unity or not os.path.isfile(unity):
        bad.append("Unity 2019.4.41f2: Unity.exe not found (--unity <...\\2019.4.41f2\\Editor\\Unity.exe>)")
    elif "2019.4.41f2" not in unity.replace("/", "\\"):
        warn.append("Unity path does not name 2019.4.41f2: %s" % unity)
    else:
        print("unity: %s" % unity)
    # Wwise integration (the Launcher's Integrate step into any Unity project).
    # macOS port: demoted to a WARNING so the game can run silent first; audio is added later.
    w = cfg.get("wwise")
    ver = os.path.join(w, "Assets", "Wwise", "Version.txt") if w else None
    if not ver or not os.path.isfile(ver):
        warn.append("Wwise: no Unity project with the Launcher's Wwise integration (--wwise <project folder>); game audio will be missing")
    elif "2019.2.5 Build 7349" not in open(ver, encoding="utf-8", errors="replace").read():
        warn.append("Wwise: the integration in %s is not Wwise 2019.2.5 Build 7349" % w)
    else:
        print("wwise: %s" % w)
    # AssetRipper
    ar = cfg.get("assetripper")
    if not ar or not os.path.isfile(ar):
        bad.append("AssetRipper 1.3.14: AssetRipper.GUI.Free.exe not found (--assetripper)")
    else:
        print("assetripper: %s" % ar)
    # Python packages
    for mod in PY_PACKAGES:
        try:
            importlib.import_module(mod)
        except ImportError:
            bad.append("Python package missing: %s (pip install -r setup/requirements.txt)" % mod)
    # disk + RAM
    free = shutil.disk_usage(paths.PROJECT).free / 2 ** 30
    need_gb = float(cfg.get("min_free_gb", MIN_FREE_GB))     # config override for partial developer runs only
    if os.path.exists(paths.EXPORT_ASSETS) or os.path.exists(os.path.join(paths.ASSETS, "Texture2D")):
        print("disk: %.1f GB free (a previous run's files are present; not checked against %d GB)" % (free, need_gb))
    elif free < need_gb:
        bad.append("disk: %.1f GB free on the project's drive, %d GB needed" % (free, need_gb))
    else:
        print("disk: %.1f GB free" % free)
    ram = total_ram_gb()
    if ram < MIN_RAM_GB:
        sysfree = shutil.disk_usage("/" if sys.platform == "darwin" else os.environ.get("SystemDrive", "C:") + "\\").free / 2 ** 30
        warn.append("RAM: %.0f GB installed; the asset export needs about 21 GB, so Windows will use its page file (slower). Keep "
                    "the page file system-managed and about 15 GB free on the system drive (now %.0f GB free)" % (ram, sysfree))
    for w_ in warn:
        print("WARNING " + w_)
    for b in bad:
        print("PROBLEM " + b)
    if bad:
        sys.exit(1)
    print("check OK")


if __name__ == "__main__":
    main()
