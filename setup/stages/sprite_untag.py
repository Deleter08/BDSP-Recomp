"""tools/winbuild/sprite_untag.py - s39: empty the runtime atlas tags (m_AtlasTags) that AssetRipper wrote into the Sprite .asset files.

Why (measured s39, work/winbuild/atlas_probe/report_*.txt): a Sprite whose asset carries m_AtlasTags is treated by BuildAssetBundles as
LATE-BOUND to a SpriteAtlas, and the build writes it with m_RD.texture = null. Shipped, the atlas then supplies the render data + page
textures; ours can't (the editor never packs them - packer Disabled, s32 law 3 - and m_RenderDataMap cannot be written in the 2019.4
editor), so every packed sprite of a Windows bundle came out untextured (shareduiassets/sharedui 322/322) and the atlas had no pages.
With the tags emptied the build keeps the sprite's own (s32-baked) page texture and packs the page Texture2D into the bundle - the
data model the EDITOR has drawn with since s32 (packer Disabled: no binding, GetSprite returns clones of the textured sprites). The
editor is unaffected (with the packer Disabled it never binds by tag). The rebuilt .spriteatlas files keep m_Tag / m_PackedSprites.

Only the m_AtlasTags block is edited (`  m_AtlasTags:\\n  - X\\n...` -> `  m_AtlasTags: []\\n`); everything else byte-identical.
Originals copied to --backup first (an existing backup copy is never overwritten = the oldest original is kept) + a TSV manifest of
sha1 before / after; --restore writes the backups back.

usage: py -3.13 -I tools/winbuild/sprite_untag.py [--apply | --restore] [--project project] [--backup work/winbuild/backup/sprite_untag]
       report -> work/winbuild/sprite_untag_report.txt
Rebuild order (HANDOFF s32 + s39): ... sprite_bake.py --apply -> BdspAtlas.BuildAll -> ... -> sprite_untag.py --apply.
"""
import collections, hashlib, os, re, shutil, sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402  (setup: --project / --backup are passed as absolute paths; the report goes to the work folder)

ROOT = paths.PROJECT
TAGS =re.compile(rb"(?m)^  m_AtlasTags:\n(?:  - [^\n]*\n)+")
TEX = re.compile(rb"(?m)^  m_RD:\n    serializedVersion: \d+\n    texture: \{fileID: (\d+)")


def arg(name, default):
    return sys.argv[sys.argv.index(name) + 1] if name in sys.argv else default


def sha1(b):
    return hashlib.sha1(b).hexdigest()


def main():
    project = os.path.join(ROOT, arg("--project", "project"))
    backup = os.path.join(ROOT, arg("--backup", os.path.join("work", "winbuild", "backup", "sprite_untag")))
    apply, restore = "--apply" in sys.argv, "--restore" in sys.argv
    manifest = os.path.join(backup, "manifest.tsv")
    rep = []

    if restore:
        n = 0
        for line in open(manifest, encoding="utf-8"):
            if line.startswith("#"):
                continue
            rel, before, _after = line.rstrip("\n").split("\t")
            src = os.path.join(backup, rel)
            data = open(src, "rb").read()
            if sha1(data) != before:
                sys.exit("backup sha1 mismatch: " + rel)
            open(os.path.join(project, rel), "wb").write(data)
            n += 1
        print("restored %d files from %s" % (n, backup))
        return 0

    tagged, texnull, tags = [], 0, collections.Counter()
    scanned = 0
    for d, _dirs, files in os.walk(os.path.join(project, "Assets")):
        for f in files:
            if not f.endswith(".asset"):
                continue
            p = os.path.join(d, f)
            with open(p, "rb") as fh:
                head = fh.read(200)
                if b"!u!213 " not in head:
                    continue
                data = head + fh.read()
            scanned += 1
            m = TAGS.search(data)
            if not m:
                continue
            rel = os.path.relpath(p, project)
            tagged.append((rel, data, m))
            for t in m.group(0).split(b"\n")[1:]:
                if t.strip():
                    tags[t.strip()[2:].decode("utf-8", "replace")] += 1
            mt = TEX.search(data)
            if mt is None or mt.group(1) == b"0":
                texnull += 1
    rep.append("sprite .asset files scanned %d, with a non-empty m_AtlasTags %d (of those m_RD.texture null: %d)" % (scanned, len(tagged), texnull))
    rep.append("tags: %d distinct; top: %s" % (len(tags), ", ".join("%s %d" % kv for kv in tags.most_common(15))))

    if apply:
        os.makedirs(backup, exist_ok=True)
        rows = []
        if os.path.isfile(manifest):
            rows = [l.rstrip("\n") for l in open(manifest, encoding="utf-8") if not l.startswith("#")]
        known = {r.split("\t")[0] for r in rows}
        n = 0
        for rel, data, m in tagged:
            dst = os.path.join(backup, rel)
            if not os.path.exists(dst):
                os.makedirs(os.path.dirname(dst), exist_ok=True)
                shutil.copy2(os.path.join(project, rel), dst)
            new = data[:m.start()] + b"  m_AtlasTags: []\n" + data[m.end():]
            open(os.path.join(project, rel), "wb").write(new)
            if rel not in known:
                rows.append("%s\t%s\t%s" % (rel, sha1(data), sha1(new)))
            n += 1
        with open(manifest, "w", encoding="utf-8", newline="\n") as fh:
            fh.write("#sprite_untag\trel path\tsha1 original\tsha1 untagged\n" + "\n".join(rows) + "\n")
        rep.append("APPLIED: %d files untagged; originals in %s (manifest.tsv, %d rows)" % (n, backup, len(rows)))
    else:
        rep.append("dry run (no --apply)")
    out = paths.work("build", "sprite_untag_report.txt")
    open(out, "w", encoding="utf-8").write("\n".join(rep) + "\n")
    print("\n".join(rep))
    return 0


if __name__ == "__main__":
    sys.exit(main())
