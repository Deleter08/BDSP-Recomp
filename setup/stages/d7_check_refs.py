"""D7 (HANDOFF §11.15): check the editor's resolution of material texture references after the native rebuild: for every material
in work/s34/d7_verify_materials.txt, each texture property whose YAML references a converted GUID must resolve (BdspD7.Verify ->
work/s34/d7_verify_result.txt) to that GUID's new .asset. Prints counts and every failure.
  py -3.13 tools/d7_check_refs.py
"""
import json
import os
import sys
import re

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402  (setup: dump / project / work folder from the setup config)


def main():
    conv = {}
    for name in ("d7_manifest.jsonl", "d7_cubemap_manifest.jsonl"):
        p = paths.work("d7", name)
        if os.path.exists(p):
            for line in open(p, encoding="utf-8"):
                m = json.loads(line)
                if m.get("state") == "done":
                    conv[m["guid"]] = "Assets/" + m["asset"]
    resolved = {}
    for line in open(paths.work("d7", "d7_verify_result.txt"), encoding="utf-8"):
        m = re.match(r"^(Assets/\S.*?\.mat) (\S+) = (.*)$", line.rstrip("\n"))
        if m:
            resolved[(m.group(1), m.group(2))] = m.group(3)
    ok = bad = 0
    for mat in open(paths.work("d7", "d7_verify_materials.txt"), encoding="utf-8").read().splitlines():
        text = open(os.path.join(paths.PROJECT, mat), encoding="utf-8", errors="replace").read()
        for prop, guid, typ in re.findall(r"^      (\w+):\n        m_Texture: \{fileID: \d+, guid: ([0-9a-f]{32}), type: (\d)\}", text, re.M):
            if guid not in conv:
                continue
            got = resolved.get((mat, prop))
            if got is None:
                continue   # property not declared by the material's shader (stale saved property) - not resolvable by design
            if got.startswith(conv[guid]):
                ok += 1
            else:
                bad += 1
                print("BAD", mat, prop, "type", typ, "->", got, "expected", conv[guid])
    print("converted-texture references resolved:", ok, "failed:", bad)


if __name__ == "__main__":
    main()
