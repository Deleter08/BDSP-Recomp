"""D7 (HANDOFF §11.15): after tools/d7_build.py turned PNG textures into native Texture2D assets (same GUID, main fileID 2800000),
re-point the project at them:
  1. every YAML file under Assets: `{fileID: 2800000, guid: G, type: 3}` -> `type: 2` for converted GUIDs G (type 3 = an
     importer-produced object; a native asset's object is type 2 - measured: type-3 references to the native asset load as null,
     type 2 resolves);
  2. Assets/Editor/EditorAssetAssistant/bundle_map.tsv (the Phase 4 editor proxy's container -> project path map): converted
     .png project paths -> the new .asset paths.
Each modified file is backed up once to work/s34/d7_ref_backup/<rel> (first write wins); report work/s34/d7_refs_report.txt.
  py -3.13 tools/d7_refs.py [--apply]
"""
import argparse
import json
import os
import re
import shutil
import sys
import time

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402  (setup: dump / project / work folder from the setup config)
ASSETS = paths.ASSETS
MANIFEST = paths.work("d7", "d7_manifest.jsonl")
BACKUP = paths.work("d7", "d7_ref_backup")
REPORT = paths.work("d7", "d7_refs_report.txt")
TSV = os.path.join(ASSETS, "Editor", "EditorAssetAssistant", "bundle_map.tsv")
REF = re.compile(rb"\{fileID: (2800000|8900000), guid: ([0-9a-f]{32}), type: 3\}")
CUBE_MANIFEST = paths.work("d7", "d7_cubemap_manifest.jsonl")


def backup(path):
    rel = os.path.relpath(path, paths.PROJECT)
    dst = os.path.join(BACKUP, rel)
    if not os.path.exists(dst):
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copy2(path, dst)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    a = ap.parse_args()
    done = [json.loads(l) for l in open(MANIFEST, encoding="utf-8")]
    done = [m for m in done if m.get("state") == "done"]
    want = {m["guid"].encode(): b"2800000" for m in done}   # 2D textures: main object Texture2D 2800000
    if os.path.exists(CUBE_MANIFEST):
        cubes = [json.loads(l) for l in open(CUBE_MANIFEST, encoding="utf-8")]
        cubes = [m for m in cubes if m.get("state") == "done"]
        want.update({m["guid"].encode(): b"8900000" for m in cubes})   # cubemaps: main object Cubemap 8900000
        done = done + cubes
    guids = want
    png_to_asset = {"Assets/" + m["png"]: "Assets/" + m["asset"] for m in done}
    print(len(guids), "converted textures")
    t0 = time.time()
    files = changed = refs = bytes_changed = 0
    lines = []
    for dp, dns, fns in os.walk(ASSETS):
        for fn in fns:
            if fn.endswith((".meta", ".png", ".jpg", ".tga", ".psd", ".exr", ".dll", ".cs", ".shader", ".cginc", ".hlsl", ".wem",
                            ".bnk", ".webm", ".mp4", ".ttf", ".otf", ".bytes", ".txt", ".json", ".tsv", ".fbx", ".FBX")):
                continue
            path = os.path.join(dp, fn)
            try:
                with open(path, "rb") as f:
                    head = f.read(5)
                    if head != b"%YAML":
                        continue
                    data = head + f.read()
            except OSError:
                continue
            files += 1
            n = [0]

            def sub(m):
                if guids.get(m.group(2)) == m.group(1):
                    n[0] += 1
                    return b"{fileID: " + m.group(1) + b", guid: " + m.group(2) + b", type: 2}"
                return m.group(0)

            if b"type: 3}" not in data:
                continue
            new = REF.sub(sub, data)
            if n[0]:
                changed += 1
                refs += n[0]
                bytes_changed += len(data)
                lines.append(f"{n[0]:5d} {os.path.relpath(path, ASSETS)}")
                if a.apply:
                    backup(path)
                    with open(path, "wb") as f:
                        f.write(new)
            if files % 20000 == 0:
                print(f"{files} yaml files scanned, {changed} to change, {time.time() - t0:.0f} s", flush=True)
    # the editor proxy's path map
    tsv_changes = 0
    text = open(TSV, encoding="utf-8").read()
    out_lines = []
    for line in text.split("\n"):
        cols = line.split("\t")
        if len(cols) > 2 and not line.startswith("#"):
            for i in range(3, len(cols), 2):
                if cols[i] in png_to_asset:
                    cols[i] = png_to_asset[cols[i]]
                    tsv_changes += 1
        out_lines.append("\t".join(cols))
    if a.apply and tsv_changes:
        backup(TSV)
        open(TSV, "w", encoding="utf-8", newline="").write("\n".join(out_lines))
    summary = (f"yaml files {files}, changed {changed} ({bytes_changed / 2**20:.1f} MiB to back up), references {refs}; "
               f"bundle_map.tsv paths {tsv_changes}; {time.time() - t0:.0f} s; {'APPLIED' if a.apply else 'dry run'}")
    open(REPORT, "w", encoding="utf-8").write(summary + "\n" + "\n".join(sorted(lines, reverse=True)) + "\n")
    print(summary)
    return 0


if __name__ == "__main__":
    sys.exit(main())
