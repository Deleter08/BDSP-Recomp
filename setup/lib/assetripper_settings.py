#!/usr/bin/env python3
"""Read an AssetRipper GUI settings page (GET /Settings/Edit, saved as html) into name=value lines, or compare two of them.

  py -3.13 -I tools/assetripper_settings.py <page.html>                  -> every setting
  py -3.13 -I tools/assetripper_settings.py <a.html> <b.html>            -> only the settings that differ (or exist on one side)

Selects give the selected option's value (+ the options offered), checkboxes checked/off, text/number inputs their value.
"""
import re
import sys


def parse(path):
    s = open(path, encoding="utf-8", errors="replace").read()
    out = {}
    for m in re.finditer(r'<select[^>]*name="([^"]+)"[^>]*>(.*?)</select>', s, re.S):
        opts = re.findall(r'<option([^>]*)>([^<]*)<', m.group(2))
        vals = []
        sel = None
        for attrs, label in opts:
            v = re.search(r'value="([^"]*)"', attrs)
            v = v.group(1) if v else label.strip()
            vals.append(v)
            if re.search(r'\sselected(=""|\b)', attrs):
                sel = v
        out[m.group(1)] = "%s   [options: %s]" % (sel if sel is not None else "(none selected)", ", ".join(vals))
    for m in re.finditer(r'<input[^>]*>', s):
        t = m.group(0)
        name = re.search(r'name="([^"]+)"', t)
        typ = re.search(r'type="([^"]+)"', t)
        if not name or not typ or typ.group(1) in ("hidden", "submit"):
            continue
        if typ.group(1) == "checkbox":
            out[name.group(1)] = ("checked" if re.search(r'\schecked(=""|\b)', t) else "off") + \
                (" LOCKED" if re.search(r'\sdisabled(=""|\b)', t) else "")      # Premium-only in the free edition
        else:
            val = re.search(r'value="([^"]*)"', t)
            out[name.group(1)] = val.group(1) if val else ""
    return out


def main():
    a = sys.argv[1:]
    if len(a) == 1:
        for k, v in parse(a[0]).items():
            print("%-34s %s" % (k, v))
        return
    x, y = parse(a[0]), parse(a[1])
    diff = 0
    for k in sorted(set(x) | set(y)):
        if x.get(k) != y.get(k):
            diff += 1
            print("%-34s A: %s\n%-34s B: %s" % (k, x.get(k, "(missing)"), "", y.get(k, "(missing)")))
    print("%d settings differ (A = %s, B = %s)" % (diff, a[0], a[1]))


if __name__ == "__main__":
    main()
