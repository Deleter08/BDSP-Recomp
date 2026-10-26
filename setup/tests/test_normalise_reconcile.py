"""normalise_guids.reconcile: Finder metadata removal and the AssetRipper same-name numbering-order case (synthetic trees)."""
import os
from pathlib import Path
import sys
import tempfile
import unittest

HERE = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(HERE / 'stages'))
import normalise_guids  # noqa: E402

COPY, REAL = b"copy bytes", b"the real NAME_1"


def tree(root, files):
    for rel, data in files.items():
        p = Path(root) / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_bytes(data)
        Path(str(p) + ".meta").write_text("guid: %s\n" % ("%032x" % abs(hash(rel)))[:32])


class ReconcileTests(unittest.TestCase):
    def table(self):
        # reference layout: real object took NAME_1, three copies are NAME, NAME_0, NAME_2
        return {t: "x" for t in ["T/n.png", "T/n_0.png", "T/n_1.png", "T/n_2.png", "S/a.bytes"]}

    def test_collision_order_renamed_to_table_layout(self):
        with tempfile.TemporaryDirectory() as d:
            # this run: a copy took NAME_1, so the real one became NAME_1_0 and the copies end at NAME_1
            tree(d, {"T/n.png": COPY, "T/n_0.png": COPY, "T/n_1.png": COPY, "T/n_1_0.png": REAL, "S/a.bytes": b"a",
                     "S/.DS_Store": b"finder"})
            assets = ["T/n.png", "T/n_0.png", "T/n_1.png", "T/n_1_0.png", "S/a.bytes", "S/.DS_Store"]
            got = normalise_guids.reconcile(d, assets, self.table())
            self.assertEqual(sorted(got), sorted(self.table()))
            self.assertEqual((Path(d) / "T/n_1.png").read_bytes(), REAL)
            self.assertEqual((Path(d) / "T/n_2.png").read_bytes(), COPY)
            self.assertTrue((Path(d) / "T/n_2.png.meta").is_file())
            self.assertFalse((Path(d) / "T/n_1_0.png").exists())
            self.assertFalse((Path(d) / "S/.DS_Store").exists())

    def test_differing_copies_refuse(self):
        with tempfile.TemporaryDirectory() as d:
            tree(d, {"T/n.png": COPY, "T/n_0.png": b"other", "T/n_1.png": COPY, "T/n_1_0.png": REAL, "S/a.bytes": b"a"})
            with self.assertRaises(SystemExit):
                normalise_guids.reconcile(d, ["T/n.png", "T/n_0.png", "T/n_1.png", "T/n_1_0.png", "S/a.bytes"], self.table())
            self.assertEqual((Path(d) / "T/n_1_0.png").read_bytes(), REAL)     # nothing moved

    def test_unrelated_mismatch_left_for_the_strict_check(self):
        with tempfile.TemporaryDirectory() as d:
            tree(d, {"T/n.png": COPY, "T/n_0.png": COPY, "T/n_1.png": COPY, "T/other.png": b"?", "S/a.bytes": b"a"})
            got = normalise_guids.reconcile(d, ["T/n.png", "T/n_0.png", "T/n_1.png", "T/other.png", "S/a.bytes"], self.table())
            self.assertIn("T/other.png", got)
            self.assertNotIn("T/n_2.png", got)


if __name__ == '__main__':
    unittest.main()
