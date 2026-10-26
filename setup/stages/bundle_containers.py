"""Phase 4 (HANDOFF §11.6) — the Switch bundles' container tables, for the editor load path.

Two steps, both read-only on their inputs:

  scan  py -3.13 -I tools/bundle_containers.py scan <romfs>/Data/StreamingAssets/AssetAssistant --out work/s32/bundle_containers.json
        UnityPy reads every bundle's AssetBundle object (no texture / mesh decoding): file path, m_Name,
        m_AssetBundleName, m_Dependencies, m_IsStreamedSceneAssetBundle, m_Container (path -> local object class + pathID).
        Checkpoints to <out>.partial every 500 bundles; a re-run resumes from it.

  map   py -3.13 -I tools/bundle_containers.py map --containers work/s32/bundle_containers.json --project project
              --out work/s32/bundle_map.json --report work/s32/bundle_map_report.txt
        Maps every container path to the project file AssetRipper wrote for it (case as on disk) and writes the table the
        editor proxy reads (Assets/Editor/EditorAssetAssistant/... loads it): bundle name -> [dev path, project path] pairs,
        where the dev path is the container path with the on-disk case of its folders and file stem (the extension stays the
        container's), so the game's own lower-casing / SceneDatabase look-ups see the developers' paths.

The romfs tree is untrusted extracted data: run with -I, outputs go under work/.
"""
import argparse
import json
import os
import sys
import time


def scan(root, out):
    import UnityPy  # only the scan step needs it

    partial = out + ".partial"
    done = {}
    if os.path.exists(partial):
        with open(partial, "r", encoding="utf-8") as f:
            done = json.load(f)
        print(f"resume: {len(done)} bundles already scanned", flush=True)

    files = []
    for dirpath, _dirs, names in os.walk(root):
        for n in names:
            full = os.path.join(dirpath, n)
            rel = os.path.relpath(full, root).replace("\\", "/")
            if rel.endswith(".bin") and "/" not in rel:
                continue  # the BinaryFormatter manifest (Dpr.bin), not a bundle
            files.append(rel)
    files.sort()
    print(f"{len(files)} files under {root}", flush=True)

    t0 = time.time()
    n_new = 0
    for i, rel in enumerate(files):
        if rel in done:
            continue
        rec = {"error": None}
        try:
            env = UnityPy.load(os.path.join(root, rel))
            abs_ = [o for o in env.objects if o.type.name == "AssetBundle"]
            if len(abs_) != 1:
                rec["error"] = f"{len(abs_)} AssetBundle objects"
            else:
                o = abs_[0]
                t = o.read_typetree()
                local = o.assets_file.objects
                container = []
                for path, info in t["m_Container"]:
                    pp = info["asset"]
                    cls = None
                    if pp["m_FileID"] == 0 and pp["m_PathID"] in local:
                        cls = local[pp["m_PathID"]].type.name
                    container.append([path, cls, pp["m_FileID"], pp["m_PathID"]])
                rec.update({
                    "name": t.get("m_Name", ""),
                    "assetBundleName": t.get("m_AssetBundleName", ""),
                    "dependencies": list(t.get("m_Dependencies", [])),
                    "isScene": bool(t.get("m_IsStreamedSceneAssetBundle", False)),
                    "container": container,
                    "size": os.path.getsize(os.path.join(root, rel)),
                })
            del env
        except Exception as e:  # recorded, never fatal
            rec["error"] = f"{type(e).__name__}: {e}"
        done[rel] = rec
        n_new += 1
        if n_new % 500 == 0:
            with open(partial, "w", encoding="utf-8") as f:
                json.dump(done, f)
            el = time.time() - t0
            print(f"  {len(done)}/{len(files)}  {el:.0f}s  ({n_new / el:.1f}/s)", flush=True)

    with open(out, "w", encoding="utf-8") as f:
        json.dump(done, f, indent=0)
    errs = sum(1 for r in done.values() if r["error"])
    print(f"scan done: {len(done)} bundles, {errs} errors, {time.time() - t0:.0f}s -> {out}", flush=True)


def build_disk_index(project):
    """lower-case project-relative path -> on-disk path; lower-case path without extension -> [on-disk paths]."""
    exact, stem = {}, {}
    assets = os.path.join(project, "Assets")
    for dirpath, _dirs, names in os.walk(assets):
        for n in names:
            if n.endswith(".meta"):
                continue
            full = os.path.join(dirpath, n)
            rel = os.path.relpath(full, project).replace("\\", "/")
            low = rel.lower()
            exact[low] = rel
            base, _ext = os.path.splitext(low)
            stem.setdefault(base, []).append(rel)
    return exact, stem


def dev_path(container_path, disk_path):
    """the container path with the on-disk case of its folders + stem; the container's own extension kept."""
    c_base, c_ext = os.path.splitext(container_path)
    d_base, _d_ext = os.path.splitext(disk_path)
    if c_base.lower() != d_base.lower():
        return container_path
    return d_base + c_ext


MANIFEST_PROJECT = {"bg/arenas/sky/s037": "Environments"}  # the only clash (bundle_map_report.txt); see build_map


def build_map(containers, project, out, report, tsv=None):
    with open(containers, "r", encoding="utf-8") as f:
        scan_ = json.load(f)
    exact, stem = build_disk_index(project)
    print(f"disk index: {len(exact)} files", flush=True)

    bundles = {}          # bundle name -> record
    name_clash = []
    misses = []           # (bundle, container path, class)
    ext_changed = {}      # (container ext, disk ext) -> count
    multi_stem = []
    split_paths = []
    errors = []
    for rel, rec in sorted(scan_.items()):
        if rec["error"]:
            errors.append((rel, rec["error"]))
            continue
        name = rec["name"]
        if not name:
            continue  # the sub-project AssetBundleManifest bundles (Battle/Battle, Dpr/Dpr, ...)
        # s38: a bundle name shipped in two sub-projects resolves to the file the shipped manifest names (Dpr.bin record.projectName,
        # work/winbuild/dpr_manifest_shipped.tsv) - the game only ever loads <projectName>/<name>. Sorted order had kept
        # Effects/bg/arenas/sky/s037 (an effect material prefab) for the Environments battle sky.
        pref = MANIFEST_PROJECT.get(name)
        if pref is not None and rel.split("/", 1)[0] != pref:
            name_clash.append((name, pref + "/" + name, rel))
            continue
        if name in bundles:
            name_clash.append((name, bundles[name]["file"], rel))
            continue
        paths = {}
        n_at_path = {}
        for path, _cls, _fid, _pid in rec["container"]:
            n_at_path[path] = n_at_path.get(path, 0) + 1
        for path, cls, _fid, _pid in rec["container"]:
            low = path.lower()
            disk = exact.get(low)
            base = os.path.splitext(low)[0]
            if disk is None:
                cands = stem.get(base, []) or stem.get(base.rstrip(" "), [])  # Windows drops a trailing space
                if len(cands) == 1:
                    disk = cands[0]
                elif len(cands) > 1:
                    multi_stem.append((name, path, cands))
                    disk = sorted(cands)[0]
            if disk is None and n_at_path[path] > 1:
                # several objects (sprites) at one container path with no texture: AssetRipper wrote <stem>_0 .. <stem>_<n-1>;
                # the main asset of the path is the first (the proxy's LoadMainAssetAtPath contract is one object per path)
                cands = [stem[b][0] for b in (base + "_" + str(k) for k in range(n_at_path[path])) if b in stem]
                if cands:
                    if not any(sp[0] == name and sp[1] == path for sp in split_paths):
                        split_paths.append((name, path, cands))
                    disk = cands[0]
            if disk is None:
                misses.append((name, path, cls))
                ent = paths.setdefault(path, {"dev": path, "disk": None, "classes": []})
            else:
                ce, de = os.path.splitext(low)[1], os.path.splitext(disk.lower())[1]
                if ce != de:
                    ext_changed[(ce, de)] = ext_changed.get((ce, de), 0) + 1
                ent = paths.setdefault(path, {"dev": dev_path(path, disk), "disk": disk, "classes": []})
            ent["classes"].append(cls)
        bundles[name] = {
            "file": rel,
            "isScene": rec["isScene"],
            "dependencies": rec["dependencies"],
            "paths": [[e["dev"], e["disk"]] for e in paths.values()],
            "classes": [e["classes"] for e in paths.values()],
        }

    variants = sorted(n for n in bundles if "." in n.rsplit("/", 1)[-1])
    table = {
        "source": os.path.abspath(containers),
        "bundles": bundles,
        "variants": variants,
    }
    with open(out, "w", encoding="utf-8") as f:
        json.dump(table, f, indent=0, ensure_ascii=False)
    if tsv:
        # the editor proxy's input: one line per bundle = name, isScene, then (dev path, project path) pairs; no tabs in any path
        os.makedirs(os.path.dirname(tsv), exist_ok=True)
        with open(tsv, "w", encoding="utf-8", newline="\n") as f:
            f.write(f"#bdsp-bundle-map\t1\t{len(bundles)}\n")
            for n in sorted(bundles):
                b = bundles[n]
                cols = [n, "1" if b["isScene"] else "0"]
                for dev, disk in b["paths"]:
                    cols += [dev, disk or ""]
                assert not any("\t" in c or "\n" in c for c in cols), n
                f.write("\t".join(cols) + "\n")

    n_paths = sum(len(b["paths"]) for b in bundles.values())
    with open(report, "w", encoding="utf-8") as r:
        r.write(f"bundles scanned {len(scan_)}  errors {len(errors)}  named bundles {len(bundles)}  name clashes {len(name_clash)}\n")
        r.write(f"scene bundles {sum(1 for b in bundles.values() if b['isScene'])}  variant bundles {len(variants)}\n")
        r.write(f"container paths {n_paths}  mapped {n_paths - len(misses)}  MISSING {len(misses)}  multi-stem {len(multi_stem)}  split {len(split_paths)}\n")
        r.write("extension changes (container -> disk):\n")
        for (ce, de), c in sorted(ext_changed.items(), key=lambda x: -x[1]):
            r.write(f"  {ce or '(none)'} -> {de or '(none)'}  {c}\n")
        r.write("\nerrors:\n")
        for e in errors:
            r.write(f"  {e[0]}: {e[1]}\n")
        r.write("\nname clashes (name, kept file, skipped file):\n")
        for c in name_clash:
            r.write(f"  {c}\n")
        miss_cls = {}
        for _b, _p, cls in misses:
            miss_cls[cls] = miss_cls.get(cls, 0) + 1
        r.write("\nmissing by class:\n")
        for cls, c in sorted(miss_cls.items(), key=lambda x: -x[1]):
            r.write(f"  {cls}: {c}\n")
        r.write("\nmissing (bundle, container path, class):\n")
        for m in misses:
            r.write(f"  {m[0]}\t{m[1]}\t{m[2]}\n")
        r.write("\nmulti-stem (bundle, container path, candidates; first sorted taken):\n")
        for m in multi_stem:
            r.write(f"  {m[0]}\t{m[1]}\t{m[2]}\n")
        r.write("\nsplit multi-object paths (bundle, container path, files; the first is the path's main asset):\n")
        for m in split_paths:
            r.write(f"  {m[0]}\t{m[1]}\t{m[2]}\n")
        r.write("\nvariant bundles:\n")
        for v in variants:
            r.write(f"  {v}\n")
    print(f"map: {len(bundles)} bundles, {n_paths} paths, {len(misses)} missing, {len(multi_stem)} multi-stem -> {out}", flush=True)


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("scan")
    s.add_argument("root")
    s.add_argument("--out", required=True)
    m = sub.add_parser("map")
    m.add_argument("--containers", required=True)
    m.add_argument("--project", required=True)
    m.add_argument("--out", required=True)
    m.add_argument("--report", required=True)
    m.add_argument("--tsv", default=None, help="the editor proxy's table (project/Assets/Editor/EditorAssetAssistant/bundle_map.tsv)")
    a = ap.parse_args()
    if a.cmd == "scan":
        scan(a.root, a.out)
    else:
        build_map(a.containers, a.project, a.out, a.report, a.tsv)


if __name__ == "__main__":
    sys.exit(main())
