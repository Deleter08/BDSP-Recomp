#!/usr/bin/env python3
"""Stage fix_arrays: repair the negative values the free AssetRipper 1.3.14 export damages in YAML hex arrays.

Unity writes arrays of primitives as one hex string (`TrainerID: 0a000000ffffffff...`). The free AssetRipper export writes the
high nibble of a negative value's top (last, little-endian) byte as '0' + (nibble - 16), i.e. one of ( ) * + , - . / instead of
8 9 a b c d e f: -1 = "ffffffff" comes out as "ffffff/f". Hit in the dry run: 4,553 files / 379,024 values - message attributes
(format_msbt), encounter tables (HoneyTree, Fishing, Before/After Morning/Daytime/Night), EventSymbol, TrainerID,
m_BoneNameHashes, ... Repair = ( ) * + , - . / -> 8 9 a b c d e f at exactly those positions; checked against this project's own
(Premium-exported) assets: every repaired value equal (tools/setup/hexarray_scan.py).

A value is touched only when every odd character sits where the bug puts it (the high nibble of an element's top byte, element
size 4, 2 or 8 bytes, followed by a hex digit). A damaged value can also read as a decimal number ("058989.6", "722275.5" - six
EventSymbol values in the dry run), while real decimals of the same shape exist ("m_InnerSpotAngle: 21.80208"): such a value is
repaired only when its field name holds plain hex arrays elsewhere in the export (a field never mixes hex arrays and decimals).
Idempotent: a second run finds nothing. Runs on the export before `normalise`; on an already merged project it runs on Assets/.

  python -I setup/stages/fix_arrays.py [--root <folder>] [--dry]
"""
import argparse
import collections
import json
import os
import re
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402

SKIP_TOP = {"Scripts", "Editor", "Plugins", "Wwise"}     # code (git-tracked or unused decompiled scripts)
BAD = "()*+,-./"
REPAIR = str.maketrans(BAD, "89abcdef")
QUICK = re.compile(rb": [0-9a-f]*[()*+,\-./][0-9a-f]")
LINE = re.compile(rb"^(\s*(?:- )?[A-Za-z_][A-Za-z0-9_]*: )([0-9a-f()*+,\-./]+)(\r?)$")
NUMBER = re.compile(r"^-?[0-9]*\.?[0-9]+(?:e[-+]?[0-9]+)?$")
# a plain hex array: only hex digits, at least one letter, whole 4-byte elements - marks its field name as a hex-array field
HEX_FIELD = re.compile(rb"^\s*(?:- )?([A-Za-z_][A-Za-z0-9_]*): ((?:[0-9a-f]{8})+)\r?$", re.M)
# a value that reads as a decimal number but could be a damaged hex array
DECIMAL_CANDIDATE = re.compile(rb"^\s*(?:- )?([A-Za-z_][A-Za-z0-9_]*): ([0-9]+\.[0-9]+)\r?$", re.M)


def damage_width(val, allow_number=False):
    if not allow_number and NUMBER.match(val):
        return 0
    bad = [i for i, c in enumerate(val) if c in BAD]
    if not bad:
        return 0
    for e in (4, 2, 8):
        step = 2 * e
        if len(val) % step == 0 and all(i % step == step - 2 and val[i + 1] in "0123456789abcdef" for i in bad):
            return e
    return 0


def repair(data, by_field, hex_fields=None):
    """Repair the unambiguous values; with hex_fields, also the decimal-looking values of those fields."""
    lines = data.split(b"\n")
    n = 0
    for i, line in enumerate(lines):
        m = LINE.match(line)
        if not m:
            continue
        val = m.group(2).decode("ascii")
        field = m.group(1).decode("ascii").strip().lstrip("- ").rstrip(":")
        if not damage_width(val):
            if not (hex_fields and field in hex_fields and NUMBER.match(val) and damage_width(val, allow_number=True)):
                continue
        lines[i] = m.group(1) + val.translate(REPAIR).encode("ascii") + m.group(3)
        by_field[field] += 1
        n += 1
    return (b"\n".join(lines), n) if n else (data, 0)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root")
    ap.add_argument("--dry", action="store_true")
    a = ap.parse_args()
    root = a.root
    if not root:
        # before `merge` the assets are in the export; after it (an older setup resumed) they are in Assets/ - the export folder
        # still exists then (the merge leaves the export's Scripts behind), so decide by the setup state, not by the folder
        state_path = paths.work("state.json")
        state = json.load(open(state_path, encoding="utf-8")) if os.path.exists(state_path) else {}
        root = paths.ASSETS if state.get("merge", {}).get("done") else paths.EXPORT_ASSETS
    if not os.path.isdir(root):
        sys.exit("no exported assets at %s" % root)
    print("fix_arrays: %s%s" % (root, " (dry run)" if a.dry else ""), flush=True)
    by_field = collections.Counter()
    by_top = collections.Counter()
    scanned = files = values = 0
    hex_fields = set()
    ambiguous = []                       # files with decimal-looking candidates, decided after every hex field is known
    for dp, dn, fn in os.walk(root):
        rel_dp = os.path.relpath(dp, root).replace("\\", "/")
        if rel_dp.split("/")[0] in SKIP_TOP:
            dn[:] = []
            continue
        for f in fn:
            if f.endswith(".meta"):
                continue
            p = os.path.join(dp, f)
            with open(p, "rb") as fh:
                head = fh.read(5)
                if head != b"%YAML":
                    continue
                data = head + fh.read()
            scanned += 1
            if scanned % 20000 == 0:
                print("  %d YAML files, %d repaired files, %d values" % (scanned, files, values), flush=True)
            for m in HEX_FIELD.finditer(data):
                if re.search(rb"[a-f]", m.group(2)):
                    hex_fields.add(m.group(1).decode("ascii"))
            cands = {m.group(1).decode("ascii") for m in DECIMAL_CANDIDATE.finditer(data)
                     if damage_width(m.group(2).decode("ascii"), allow_number=True)}
            if cands:
                ambiguous.append((p, rel_dp, cands))
            if not QUICK.search(data):
                continue
            new, n = repair(data, by_field)
            if not n:
                continue
            files += 1
            values += n
            by_top[rel_dp.split("/")[0]] += 1
            if not a.dry:
                with open(p, "wb") as fh:
                    fh.write(new)
    # second, small pass: decimal-looking values of hex-array fields
    for p, rel_dp, cands in ambiguous:
        if not cands & hex_fields:
            continue
        data = open(p, "rb").read()
        new, n = repair(data, by_field, hex_fields=cands & hex_fields)
        if not n:
            continue
        files += 1
        values += n
        by_top[rel_dp.split("/")[0]] += 1
        if not a.dry:
            with open(p, "wb") as fh:
                fh.write(new)
    print("fix_arrays: %d YAML files scanned; %s %d files / %d values (%d hex-array fields seen)" % (
        scanned, "would repair" if a.dry else "repaired", files, values, len(hex_fields)), flush=True)
    print("  by field: " + ", ".join("%s %d" % kv for kv in by_field.most_common(20)), flush=True)
    print("  by folder: " + ", ".join("%s %d" % kv for kv in by_top.most_common(20)), flush=True)


if __name__ == "__main__":
    main()
