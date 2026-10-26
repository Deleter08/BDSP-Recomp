#!/usr/bin/env python3
"""Copy only the reviewed explicit release allowlist; never read or copy git history.

The destination must not exist. Symlinks, binary payloads, unsafe paths and common
credential patterns stop the entire operation before any destination is created.
"""
import argparse
import gzip
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import shutil

ROOT = Path(__file__).resolve().parent.parent
DENIED = {'.git', '.utmp', 'handoff', 'Library', 'SetupWork', 'Build', 'Builds', 'Temp',
          '_CodeSignature', '__pycache__', '.DS_Store'}
BINARY = {'.dll', '.exe', '.so', '.a', '.dylib', '.png', '.jpg', '.jpeg', '.bnk', '.wem',
          '.apk', '.ipa', '.aab', '.nsp', '.xci', '.nca', '.p12', '.pfx', '.keystore', '.jks', '.ulf', '.mobileprovision'}
SECRETS = [rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----',
           rb'gh[pousr]_[A-Za-z0-9]{30,}', rb'github_pat_[A-Za-z0-9_]{30,}',
           rb'AKIA[0-9A-Z]{16}', rb'sk-(?:proj-)?[A-Za-z0-9_-]{40,}',
           rb'https?://[^\s/:]+:[^\s/@]+@',
           rb'(?:appleDeveloperTeamID|iOSManualProvisioningProfileID):[^\S\r\n]+[^\s\r\n][^\r\n]*']


def inspect(root, paths):
    result = {}
    for rel in paths:
        p = PurePosixPath(rel)
        if p.is_absolute() or '..' in p.parts or set(p.parts) & DENIED or p.suffix.lower() in BINARY:
            raise ValueError('Excluded release path: ' + rel)
        src = root / rel
        if not src.is_file() or any(q.is_symlink() for q in [src, *src.parents]):
            raise ValueError('Missing/nonregular/symlink input: ' + rel)
        data = src.read_bytes()
        scan = gzip.decompress(data) if rel == 'setup/data/guid_table.tsv.gz' else data
        if b'\x00' in scan:
            raise ValueError('Unreviewed binary payload: ' + rel)
        if any(re.search(pattern, scan) for pattern in SECRETS):
            raise ValueError('Potential secret (value withheld): ' + rel)
        result[rel] = hashlib.sha256(data).hexdigest()
    for required in ['LICENSE', 'THIRD_PARTY.md', 'README.md', 'Assets/Plugins/DOTween/readme.txt',
                     'setup/bin/crunch_src/crn_decomp.h', 'setup/launch.sh']:
        if required not in result:
            raise ValueError('Required credit/license/entry missing: ' + required)
    return result


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('destination', type=Path)
    ap.add_argument('--verify', action='store_true', help='verify a source snapshot against its receipt; no copying')
    a = ap.parse_args()
    dest = a.destination.resolve()
    if a.verify:
        receipt = json.loads((dest / 'SOURCE_SNAPSHOT.json').read_text())
        got = inspect(dest, receipt['files'])
        if got != receipt['files']:
            raise ValueError('Source snapshot digest mismatch')
        actual = {p.relative_to(dest).as_posix() for p in dest.rglob('*') if p.is_file()}
        if actual != set(got) | {'SOURCE_SNAPSHOT.json'}:
            raise ValueError('Snapshot has extra files; only pristine source can be verified for publication.')
        print('Verified %d explicit source files; no extra files/history or secret-pattern hits.' % len(got))
        return
    if dest.exists():
        raise ValueError('Destination exists; choose a new directory. No files were deleted or overwritten.')
    paths = [l for l in (ROOT / 'setup/release-allowlist.txt').read_text().splitlines() if l and not l.startswith('#')]
    if len(paths) != len(set(paths)):
        raise ValueError('Duplicate allowlist path')
    receipt = inspect(ROOT, paths)
    dest.mkdir(parents=True)
    for rel, sha in receipt.items():
        out = dest / rel
        out.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / rel, out)
        if hashlib.sha256(out.read_bytes()).hexdigest() != sha:
            raise ValueError('Source changed during snapshot: ' + rel)
    (dest / 'SOURCE_SNAPSHOT.json').write_text(json.dumps({'files': receipt}, indent=2) + '\n')
    print('Created %d source files at %s; no git repository initialized.' % (len(receipt), dest))

if __name__ == '__main__':
    main()
