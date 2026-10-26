#!/usr/bin/env python3
"""Stage shaders: install the recovered shaders over AssetRipper's placeholder .shader files (author D-S3: shipped with the repo).

The game ships its shaders only as compiled Switch (NVN / Maxwell) programs; AssetRipper exports placeholders. setup/data/shaders/
holds the shader source this project recovered from those programs (one file per shader name, 75 names) and shaders.tsv the 85
asset paths they belong at. Each placeholder's BODY is replaced; its .meta is never touched (same GUID, materials keep their links).
Fails if a listed path is missing, if a target is neither a placeholder nor already installed, or if any placeholder remains.
With BDSP_STAGE_FRESH=1 (setup.py re-runs the stage after its version was bumped) an earlier recovered version is replaced too.

  python -I setup/stages/shaders.py
"""
import hashlib
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402

SRC = os.path.join(paths.SETUP_DATA, "shaders")
PLACEHOLDER = b"DummyShaderTextExporter"


def sha1(b):
    return hashlib.sha1(b).hexdigest()


def main():
    installed = already = updated = 0
    fresh = os.environ.get("BDSP_STAGE_FRESH") == "1"
    rows = [l.rstrip("\r\n").split("\t") for l in open(os.path.join(SRC, "shaders.tsv"), encoding="utf-8")][1:]
    for rel, name, fname, want in rows:
        dst = os.path.join(paths.ASSETS, *rel.split("/"))
        if not os.path.isfile(dst):
            sys.exit("missing exported shader %s (%s)" % (rel, name))
        body = open(dst, "rb").read()
        if sha1(body) == want:
            already += 1
            continue
        if PLACEHOLDER not in body:
            if not fresh:
                sys.exit("%s is not an AssetRipper placeholder - not overwritten" % rel)
            updated += 1                     # an earlier recovered version (stage version bump)
        new = open(os.path.join(SRC, fname), "rb").read()
        if sha1(new) != want:
            sys.exit("setup/data/shaders/%s does not match shaders.tsv" % fname)
        meta = dst + ".meta"
        meta_before = open(meta, "rb").read() if os.path.exists(meta) else None
        with open(dst, "wb") as f:
            f.write(new)
        if (open(meta, "rb").read() if os.path.exists(meta) else None) != meta_before:
            sys.exit("the .meta of %s changed" % rel)
        installed += 1
    left = []
    for dp, dn, fn in os.walk(paths.ASSETS):
        dn[:] = [d for d in dn if d not in ("StreamingAssets",)]
        for f in fn:
            if f.endswith(".shader") and PLACEHOLDER in open(os.path.join(dp, f), "rb").read():
                left.append(os.path.relpath(os.path.join(dp, f), paths.ASSETS))
    print("shaders: %d installed (%d of them replacing an earlier version), %d already installed, %d placeholders left" % (
        installed, updated, already, len(left)))
    if left:
        for p in left[:20]:
            print("  placeholder without a recovered shader: " + p)
        sys.exit(1)


if __name__ == "__main__":
    main()
