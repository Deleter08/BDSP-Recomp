"""tools/winbuild/winbuild_survey.py - read-only cross-check for the Windows bundle build (s38).

Inputs (all read-only):
  work/winbuild/dpr_manifest_shipped.tsv        DprManifestDump.exe of romfs .../AssetAssistant/Dpr.bin
  work/s32/bundle_containers.json               every Switch bundle file: name, dependencies, isScene, container, size
  project/Assets/Editor/EditorAssetAssistant/bundle_map.tsv   the proxy map (bundle -> dev path / project path pairs)
Output: work/winbuild/survey.txt (+ survey.json) - manifest vs files vs map, dependency edges, variant groups, sizes.

usage: py -3.13 tools/winbuild/winbuild_survey.py
"""
import json, os, sys, collections

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402  (setup: dump / project / work folder from the setup config)
W = paths.work("build", "")
MANIFEST = os.path.join(W, "dpr_manifest_shipped.tsv")
CONTAINERS = paths.work("atlas", "bundle_containers.json")
MAP_TSV = paths.TSV
PROJ = paths.PROJECT


def load_manifest():
    recs, variants = {}, []
    for line in open(MANIFEST, encoding="utf-8"):
        line = line.rstrip("\n")
        if line.startswith("#variants\t"):
            variants = [v for v in line.split("\t", 1)[1].split(";") if v]
            continue
        if line.startswith("#"):
            continue
        f = line.split("\t")
        deps = [] if f[6] in ("", "<null>") else f[6].split(";")
        paths = [] if f[7] in ("", "<null>") else f[7].split(";")
        recs[f[1]] = dict(project=f[0], name=f[1], scene=f[2] == "1", size=int(f[3]), hash=f[4], deps=deps, paths=paths)
    return recs, variants


def load_map():
    rows = {}
    with open(MAP_TSV, encoding="utf-8") as fh:
        fh.readline()
        for line in fh:
            f = line.rstrip("\n").split("\t")
            pairs = list(zip(f[2::2], f[3::2]))
            rows[f[0]] = dict(flag=f[1], pairs=pairs)
    return rows


def main():
    recs, variants = load_manifest()
    files = json.load(open(CONTAINERS, encoding="utf-8"))
    bmap = load_map()
    out = []
    P = out.append

    # files keyed by bundle name (first project wins on a clash, as bundle_containers.py did)
    by_name = {}
    clashes = []
    for key, v in files.items():
        name = v.get("assetBundleName") or v.get("name")
        proj = key.split("/", 1)[0]
        if name in by_name:
            clashes.append((name, by_name[name]["key"], key))
            continue
        by_name[name] = dict(v, key=key, project=proj)

    P("manifest records %d, variant names %d" % (len(recs), len(variants)))
    P("bundle files %d (unique names %d), map rows %d" % (len(files), len(by_name), len(bmap)))

    # 1. manifest record -> file at <project>/<name>
    no_file, proj_mismatch = [], []
    for n, r in recs.items():
        key = r["project"] + "/" + n
        if key not in files:
            if n in by_name:
                proj_mismatch.append((n, r["project"], by_name[n]["key"]))
            else:
                no_file.append((r["project"], n))
    P("records without a file at <project>/<name>: %d (of which file under another project: %d)" % (len(no_file) + len(proj_mismatch), len(proj_mismatch)))
    for x in no_file[:40]:
        P("  nofile %s/%s" % x)
    for x in proj_mismatch[:20]:
        P("  elsewhere %s (manifest %s, file %s)" % x)

    # 2. files not in the manifest
    manifest_keys = {r["project"] + "/" + n for n, r in recs.items()}
    orphan = sorted(k for k in files if k not in manifest_keys)
    orphan_kind = collections.Counter()
    for k in orphan:
        v = files[k]
        nm = v.get("assetBundleName") or v.get("name") or ""
        proj = k.split("/", 1)[0]
        orphan_kind["subproject-manifest" if nm == proj or k.split("/")[-1] == proj else "bundle"] += 1
    P("files not in the manifest: %d %s" % (len(orphan), dict(orphan_kind)))
    for k in orphan[:60]:
        P("  orphan %s" % k)

    # 3. orphan reachable as a dependency of a manifest bundle?
    reach = set()
    for n, r in recs.items():
        for d in r["deps"]:
            if d not in recs:
                reach.add(d)
    P("manifest deps naming a non-record: %d %s" % (len(reach), sorted(reach)[:20]))

    # 4. dependency edges: manifest vs the file's own AssetBundle m_Dependencies; cross-project edges
    dep_diff = 0
    cross = collections.Counter()
    for n, r in recs.items():
        f = files.get(r["project"] + "/" + n)
        if f is not None:
            fd = sorted(f.get("dependencies") or [])
            if fd != sorted(r["deps"]):
                dep_diff += 1
        for d in r["deps"]:
            dr = recs.get(d)
            if dr and dr["project"] != r["project"]:
                cross[(r["project"], dr["project"])] += 1
    P("records whose manifest deps != the file's m_Dependencies: %d" % dep_diff)
    P("cross-project dependency edges: %d" % sum(cross.values()))
    for (a, b), c in cross.most_common(30):
        P("  %s -> %s : %d" % (a, b, c))

    # 5. manifest assetPaths vs file container paths vs map
    path_diff = 0
    for n, r in recs.items():
        f = files.get(r["project"] + "/" + n)
        if f is None:
            continue
        fc = sorted(set(c[0].lower() for c in (f.get("container") or [])))
        if fc != sorted(set(p.lower() for p in r["paths"])):
            path_diff += 1
    P("records whose manifest assetPaths != the file's container paths: %d" % path_diff)
    not_in_map = sorted(n for n in recs if n not in bmap)
    P("manifest records with no bundle_map row: %d %s" % (len(not_in_map), not_in_map[:20]))
    map_not_manifest = sorted(n for n in bmap if n not in recs)
    P("bundle_map rows with no manifest record: %d %s" % (len(map_not_manifest), map_not_manifest[:30]))

    # 6. variant groups: the manifest's variant names vs map rows; same asset FILE NAMES across a group
    groups = collections.defaultdict(list)
    for v in variants:
        base, ext = v.rsplit(".", 1)
        groups[base].append(ext)
    P("variant groups in the manifest: %d (sizes %s)" % (len(groups), dict(collections.Counter(len(x) for x in groups.values()))))
    bad_groups = []
    for base, exts in sorted(groups.items()):
        sets = {}
        for e in exts:
            row = bmap.get(base + "." + e)
            if row is None:
                sets[e] = None
                continue
            sets[e] = sorted(os.path.basename(p[1]).lower() for p in row["pairs"])
        vals = [tuple(s) if s is not None else None for s in sets.values()]
        if len(set(vals)) != 1:
            bad_groups.append((base, {e: (len(s) if s else s) for e, s in sets.items()}))
    P("variant groups whose members differ in asset file names: %d" % len(bad_groups))
    for b in bad_groups[:30]:
        P("  %s %s" % b)
    map_variant_rows = sorted(n for n in bmap if "." in n.split("/")[-1])
    P("map rows with a variant suffix: %d (manifest variants %d)" % (len(map_variant_rows), len(variants)))
    extra_var = sorted(set(map_variant_rows) - set(variants))
    P("  map variant rows not in the manifest variant list: %d %s" % (len(extra_var), extra_var[:20]))

    # 7. scene bundles
    scenes = sorted(n for n, r in recs.items() if r["scene"])
    P("scene records: %d" % len(scenes))
    for n in scenes:
        row = bmap.get(n)
        P("  %s file=%s map=%s paths=%s" % (n, (recs[n]["project"] + "/" + n) in files, None if row is None else [p[1] for p in row["pairs"]][:3], recs[n]["paths"][:3]))

    # 8. sizes (shipped, Switch) per project
    sz = collections.Counter()
    for n, r in recs.items():
        sz[r["project"]] += r["size"]
    P("shipped manifest size total %.2f GiB" % (sum(sz.values()) / 2**30))
    for p, s in sz.most_common():
        P("  %-20s %8.1f MiB" % (p, s / 2**20))

    # 9. map project paths that do not exist on disk
    missing = []
    for n, row in bmap.items():
        if n not in recs:
            continue
        for dev, pp in row["pairs"]:
            if not os.path.exists(os.path.join(PROJ, pp)):
                missing.append((n, pp))
    P("map project paths (manifest bundles) missing on disk: %d" % len(missing))
    for m in missing[:20]:
        P("  %s %s" % m)

    # 10. a project file assigned to two bundles
    owner = collections.defaultdict(set)
    for n, row in bmap.items():
        if n not in recs:
            continue
        for dev, pp in row["pairs"]:
            owner[pp.lower()].add(n)
    multi = {p: sorted(b) for p, b in owner.items() if len(b) > 1}
    P("project files listed in more than one manifest bundle: %d" % len(multi))
    for p, b in list(multi.items())[:20]:
        P("  %s %s" % (p, b))

    txt = "\n".join(out) + "\n"
    open(os.path.join(W, "survey.txt"), "w", encoding="utf-8").write(txt)
    json.dump(dict(no_file=no_file, proj_mismatch=proj_mismatch, orphan=orphan, bad_groups=bad_groups,
                   scenes=scenes, missing=missing, multi=multi, cross={"%s>%s" % k: v for k, v in cross.items()},
                   map_not_manifest=map_not_manifest, not_in_map=not_in_map),
              open(os.path.join(W, "survey.json"), "w", encoding="utf-8"), indent=1)
    sys.stdout.write(txt)


if __name__ == "__main__":
    main()
