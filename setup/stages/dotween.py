#!/usr/bin/env python3
"""Install DOTween 1.2.335 from the user's own copy; never distribute vendor binaries/art.

Set BDSP_DOTWEEN_SOURCE to the DOTween directory containing DOTween.dll and Editor/.
Existing matching files are kept. --check only verifies hashes and never writes.
"""
import argparse
import hashlib
import os
from pathlib import Path
import sys

PROJECT = Path(__file__).resolve().parents[2]
DEST = PROJECT / "Assets/Plugins/DOTween"
# Hashes pin the previously used release, without distributing its proprietary bytes.
FILES = {
    'DOTween.dll': '9fb74a2df55c3b32054f6b661305d47b5c6c213f2b30a90461019d967347d9a7',
    'Editor/DOTweenEditor.dll': '2a9196bc7453bb6e7f31b1fd7c35fc3134bf03f8a891c0cd38821eb1f4a3512a',
    'Editor/Imgs/DOTweenIcon.png': '6e6ca81af7b3fdbecc7069b8644047713af97c8e311cd3da97e75eff0b973faa',
    'Editor/Imgs/Footer.png': 'b1bccfb25ef58988d123ade538e28637da988dfaf33272067b641f34582e7b89',
    'Editor/Imgs/Footer_dark.png': '2bc04d9700e60b53aa4741c4580d61e490b1192d3ea4f98f4437a2e776057a5a',
    'Editor/Imgs/Header.jpg': '80652902491d99fe580056ed204d204d9ecaccf810e7ec546bd949ccff8d67fd',
}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--source", default=os.environ.get("BDSP_DOTWEEN_SOURCE"))
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    missing = [rel for rel, sha in FILES.items() if digest(DEST / rel) != sha]
    if not missing:
        print("dotween: pinned 1.2.335 files present")
        return 0
    if args.check or not args.source:
        for rel in missing:
            print("missing or different: Assets/Plugins/DOTween/" + rel)
        print("Supply your own DOTween 1.2.335: python -I setup/stages/dotween.py --source <DOTween directory>")
        return 1
    source = Path(args.source).expanduser()
    # Preflight the entire source set before changing any target. Preserve metadata/GUIDs.
    for rel in missing:
        if digest(source / rel) != FILES[rel]:
            sys.exit("DOTween source is missing or differs from pinned 1.2.335: " + str(source / rel))
        if (DEST / rel).exists():
            sys.exit("Refusing to overwrite a different local vendor file: " + str(DEST / rel))
    for rel in missing:
        target = DEST / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes((source / rel).read_bytes())
        print("installed: Assets/Plugins/DOTween/" + rel)
    return 0


if __name__ == "__main__":
    sys.exit(main())
