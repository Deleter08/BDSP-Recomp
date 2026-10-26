#!/usr/bin/env python3
"""Stage verify: the finished project, checked in the Unity editor.

1. BdspAudit.ImportReport: scripts compile; every project shader loads, none with errors (85 recovered + package shaders).
2. BdspAudit.FullAudit: missing scripts / pink materials must be EXACTLY setup/data/audit_baseline.txt - the shipped game's own
   leftovers (5+2 underground prefabs, MonitorCanvas, two _Old demo scenes, 4 test ScriptableObjects, ScoreGaugeMat assigned at
   runtime). Anything else means a setup step went wrong.
3. BdspPlay.Run: level0 in Play mode for 60 s; the summary's error / exception counts are reported (not yet a gate).

  python -I setup/stages/verify.py [--no-play]
"""
import argparse
import glob
import os
import re
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
import paths  # noqa: E402
import unity  # noqa: E402


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--no-play", action="store_true")
    a = ap.parse_args()
    audit_dir = paths.work("audit", "")
    if not unity.run("BdspAudit.ImportReport", "BDSP_IMPORT_REPORT_DONE", quit_after=True):
        sys.exit("ImportReport failed")
    rep = open(os.path.join(audit_dir, "import_report.txt"), encoding="utf-8").read()
    m = re.search(r"^SHADERS (\d+) loaded, (\d+) with errors", rep, re.M)
    if "scriptCompilationFailed False" not in rep or not m or int(m.group(2)) or int(m.group(1)) < 85:
        sys.exit("import report: compilation or shaders not OK (%s)" % (m.group(0) if m else "no SHADERS line"))
    print("import report OK: %s" % m.group(0), flush=True)
    before = set(glob.glob(os.path.join(audit_dir, "audit_*.txt")))
    if not unity.run("BdspAudit.FullAudit", "BDSP_FULL_AUDIT_DONE", quit_after=True):
        sys.exit("FullAudit failed")
    new = sorted(set(glob.glob(os.path.join(audit_dir, "audit_*.txt"))) - before) or sorted(
        glob.glob(os.path.join(audit_dir, "audit_*.txt")))
    got = [l for l in open(new[-1], encoding="utf-8").read().splitlines()
           if not l.startswith("BDSP full audit") and l != "BDSP_FULL_AUDIT_DONE" and l.strip()]
    want = [l for l in open(os.path.join(paths.SETUP_DATA, "audit_baseline.txt"), encoding="utf-8").read().splitlines() if l.strip()]
    if sorted(got) != sorted(want):
        for l in sorted(set(got) - set(want))[:30]:
            print("  NEW   " + l)
        for l in sorted(set(want) - set(got))[:30]:
            print("  GONE  " + l)
        sys.exit("full audit differs from the reference baseline")
    print("full audit = reference baseline (%d lines)" % len(want), flush=True)
    if not a.no_play:
        play_dir = paths.work("play", "")
        with open(os.path.join(play_dir, "play_seconds.txt"), "w", encoding="utf-8") as f:
            f.write("60")
        if not unity.run("BdspPlay.Run", "BDSP_PLAY_DONE"):
            sys.exit("play smoke did not finish")
        summ = sorted(glob.glob(os.path.join(play_dir, "play_*.txt")), key=os.path.getmtime)
        summ = [s for s in summ if not s.endswith("_log.txt")]
        if summ:
            line = next((l for l in open(summ[-1], encoding="utf-8") if l.startswith("messages:")), "messages: ?")
            print("play smoke: " + line.strip(), flush=True)
    print("verify OK")


if __name__ == "__main__":
    main()
