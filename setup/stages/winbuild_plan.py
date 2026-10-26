"""tools/winbuild/winbuild_plan.py - the Windows AssetBundle build PLAN (s38). Read-only on every input; writes work/winbuild/.

The plan is what Assets/Editor/BdspWinBuild/BdspWinBuild.cs builds, one AssetBundleBuild per row:
  - one row per SHIPPED manifest record (romfs .../AssetAssistant/Dpr.bin, dumped by tools/winbuild/bin/DprManifestDump.exe)
    whose bundle file shipped (13,651 of 13,660 - the 9 others are debug / tool scenes that never shipped as files; the Windows
    Dpr.bin carries their shipped records unchanged, as the shipped one did);
  - bundle name / variant exactly as shipped (variant = the manifest's _assetBundleNamesWithVariant split at the last '.');
  - assetNames = the project files the proxy map (project/Assets/Editor/EditorAssetAssistant/bundle_map.tsv) gives for the
    bundle's shipped container paths; addressableNames = the SHIPPED container key of each (lower case, as the Switch bundle's
    m_Container had it), so AssetBundle.LoadAsset("assets/....png") keeps matching although AssetRipper renamed some files
    (.png -> .asset sprites, .mesh -> .asset, .fbx -> .prefab); a key that AssetRipper split into N files gives all N the same key
    (the shipped container had N entries with that key, one per sub-asset);
  - scene bundles: assetNames = the scene files, no addressable names (scenes register by path = SceneDatabase's _scenePath).
Bundles that are NOT in the shipped manifest (383 files + 18 sub-project manifest bundles) are unreachable through AssetManager
and are not built.

Outputs:
  work/winbuild/bundle_plan.tsv     #bdsp-winbuild-plan<TAB>1<TAB><rows>  then
                                    project \t bundle \t variant \t isScene \t n \t (projectPath \t addressableName) x n
  work/winbuild/bundle_plan_report.txt
  --subset a,b,c   additionally work/winbuild/bundle_plan_subset.tsv = those bundles + their shipped dependency closure
                   (a small dependency-closed build to measure time / RAM / disk before the full one)
usage: py -3.13 tools/winbuild/winbuild_plan.py [--subset name,name,...]
exit 0 = plan written with 0 errors (warnings listed in the report).
"""
import collections, json, os, sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402  (setup: dump / project / work folder from the setup config)
W = paths.work("build", "")
PROJ = paths.PROJECT
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from winbuild_survey import load_manifest, load_map, CONTAINERS  # noqa: E402

EXCLUDED_SEGMENTS = {"editor"}  # Unity never puts anything under an Editor folder into a build


def main():
    subset = None
    if "--subset" in sys.argv:
        subset = [s for s in sys.argv[sys.argv.index("--subset") + 1].split(",") if s]
    recs, variants = load_manifest()
    order = list(recs.keys())  # shipped record order (dict keeps insertion order)
    files = json.load(open(CONTAINERS, encoding="utf-8"))
    bmap = load_map()
    variant_set = set(variants)

    errors, warns, rows = [], [], []
    owner = {}
    n_assets = n_addr_changed = 0
    for name in order:
        r = recs[name]
        key = r["project"] + "/" + name
        f = files.get(key)
        if f is None:
            continue  # never shipped as a file (carried in Dpr.bin as shipped)
        row = bmap.get(name)
        if row is None:
            errors.append("no map row for %s" % key)
            continue
        # container keys are lower case for asset bundles, as authored (mixed case) for scene bundles: match case-insensitively,
        # keep the shipped spelling
        shipped_keys = [c[0] for c in (f.get("container") or [])]
        shipped_set = set(k.lower() for k in shipped_keys)
        by_dev = collections.defaultdict(list)
        for dev, pp in row["pairs"]:
            if not pp:
                continue  # a container path AssetRipper never exported (bundle_map_report "missing") - warned below
            by_dev[dev.lower()].append(pp)
        if name in variant_set:
            base, var = name.rsplit(".", 1)
        else:
            base, var = name, ""
        assets = []
        n_at_key = collections.Counter(shipped_keys)
        for k in dict.fromkeys(shipped_keys):  # unique, shipped order
            pps = by_dev.get(k.lower())
            if not pps:
                warns.append("shipped container key with no project file (not exported): %s %s" % (key, k))
                continue
            pps = list(dict.fromkeys(pps))
            # a key AssetRipper split into <stem>_0..N-1.asset (N sprites, no texture): the map names only _0 (the proxy's main
            # asset); the shipped container held all N under the key - list every sibling under it
            if len(pps) == 1 and pps[0].endswith("_0.asset") and n_at_key[k] > 1:
                stem = pps[0][:-len("_0.asset")]
                sib = [stem + "_%d.asset" % i for i in range(1, n_at_key[k])]
                have = [s for s in sib if os.path.isfile(os.path.join(PROJ, s))]
                if len(have) != len(sib):
                    warns.append("split key %s %s: %d of %d siblings on disk" % (key, k, len(have) + 1, n_at_key[k]))
                pps += have
            for pp in pps:
                assets.append((pp, k))
        for dev_l in by_dev:
            if dev_l not in shipped_set:
                errors.append("map dev path not in the shipped container of %s: %s" % (key, dev_l))
        if not assets:
            errors.append("bundle with no assets: %s" % key)
            continue
        is_scene = r["scene"]
        for pp, k in assets:
            full = os.path.join(PROJ, pp)
            if not os.path.isfile(full):
                errors.append("missing project file %s (%s)" % (pp, key))
            segs = [s.lower() for s in pp.replace("\\", "/").split("/")]
            if EXCLUDED_SEGMENTS & set(segs[:-1]):
                errors.append("asset under an Editor folder (never built): %s (%s)" % (pp, key))
            if pp.lower().endswith((".cs", ".js", ".dll")):
                errors.append("script / assembly in a bundle: %s (%s)" % (pp, key))
            if is_scene != pp.lower().endswith(".unity"):
                errors.append("scene / non-scene mix: %s in %s (scene bundle=%s)" % (pp, key, is_scene))
            prev = owner.get(pp.lower())
            if prev and prev != name:
                errors.append("project file in two bundles: %s -> %s, %s" % (pp, prev, name))
            owner[pp.lower()] = name
            n_assets += 1
            if k != ("assets/" + pp[len("Assets/"):]).lower():
                n_addr_changed += 1
        rows.append(dict(project=r["project"], name=name, base=base, variant=var, scene=is_scene, assets=assets))

    # variant groups: Unity swaps variants by asset NAME - every member must hold the same file names
    groups = collections.defaultdict(dict)
    for row in rows:
        if row["variant"]:
            groups[row["base"]][row["variant"]] = sorted(os.path.basename(a[0]).lower() for a in row["assets"])
    for base, members in groups.items():
        if len({tuple(v) for v in members.values()}) != 1:
            errors.append("variant group %s: members differ in asset names" % base)
    built_variants = sorted(r["name"] for r in rows if r["variant"])
    if built_variants != sorted(variants):
        errors.append("variant list differs from the shipped manifest: built %d, shipped %d" % (len(built_variants), len(variants)))

    # duplicate addressable keys inside one bundle (split multi-object paths)
    dup = []
    for row in rows:
        c = collections.Counter(k for _, k in row["assets"])
        for k, n in c.items():
            if n > 1:
                dup.append("%s: '%s' x%d" % (row["name"], k, n))

    def write_plan(path, plan_rows):
        with open(path, "w", encoding="utf-8", newline="\n") as fh:
            fh.write("#bdsp-winbuild-plan\t1\t%d\n" % len(plan_rows))
            for row in plan_rows:
                cells = [row["project"], row["base"], row["variant"], "1" if row["scene"] else "0", str(len(row["assets"]))]
                for pp, k in row["assets"]:
                    cells += [pp, "" if row["scene"] else k]
                fh.write("\t".join(cells) + "\n")

    out = os.path.join(W, "bundle_plan.tsv")
    write_plan(out, rows)

    rep = []
    rep.append("shipped manifest records %d; plan rows %d; assets %d (addressable name differs from the project path: %d)"
               % (len(recs), len(rows), n_assets, n_addr_changed))
    rep.append("scene bundles %d; variant bundles %d in %d groups" % (sum(r["scene"] for r in rows), len(built_variants), len(groups)))
    per = collections.Counter(r["project"] for r in rows)
    rep.append("rows per project: " + ", ".join("%s %d" % kv for kv in sorted(per.items())))
    rep.append("records carried without a build (no shipped file): %s" % [n for n in order if (recs[n]["project"] + "/" + n) not in files])
    rep.append("bundles holding one key several times (split multi-object paths): %d" % len(dup))
    rep += ["  " + d for d in dup]
    rep.append("ERRORS %d" % len(errors))
    rep += ["  " + e for e in errors[:200]]
    rep.append("WARNINGS %d" % len(warns))
    rep += ["  " + w for w in warns[:200]]

    if subset:
        want, stack = set(), list(subset)
        while stack:
            n = stack.pop()
            if n in want:
                continue
            if n not in recs:
                errors.append("subset name not in the manifest: %s" % n)
                rep.append("subset name not in the manifest: %s" % n)
                continue
            want.add(n)
            stack += recs[n]["deps"]
        sub_rows = [r for r in rows if r["name"] in want]
        write_plan(os.path.join(W, "bundle_plan_subset.tsv"), sub_rows)
        rep.append("subset: %s -> closure %d bundles, %d assets, shipped size %.1f MiB"
                   % (subset, len(sub_rows), sum(len(r["assets"]) for r in sub_rows),
                      sum(recs[r["name"]]["size"] for r in sub_rows) / 2**20))

    txt = "\n".join(rep) + "\n"
    open(os.path.join(W, "bundle_plan_report.txt"), "w", encoding="utf-8").write(txt)
    sys.stdout.write(txt)
    print("plan -> %s" % out)
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
