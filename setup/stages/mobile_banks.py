#!/usr/bin/env python3
"""Reproduce the Mac bank source used by both mobile builders; retain Windows originals."""
import hashlib
import os
from pathlib import Path
import shutil
import sys

ROOT = Path(__file__).resolve().parents[2]

def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(1024*1024), b''):
            h.update(block)
    return h.digest()

def prepare(base):
    source, dest = base / 'Windows', base / 'Mac'
    if not (source / 'Init.bnk').is_file():
        raise RuntimeError('No Windows/Init.bnk after merge; supply the complete game dump and complete common.merge.')
    pending = []
    for src in sorted(source.rglob('*')):
        if src.is_symlink():
            raise RuntimeError('Refusing bank symlink: ' + str(src))
        if not src.is_file() or src.suffix == '.meta':
            continue
        dst = dest / src.relative_to(source)
        if dst.exists():
            if not dst.is_file() or sha(src) != sha(dst):
                raise RuntimeError('Conflicting bank; preserve and investigate: ' + str(dst))
        else:
            pending.append((src, dst))
    for src, dst in pending:
        dst.parent.mkdir(parents=True, exist_ok=True)
        tmp = dst.with_name(dst.name + '.setup-partial')
        shutil.copyfile(src, tmp)
        if sha(src) != sha(tmp):
            raise RuntimeError('Bank copy verification failed: ' + str(dst))
        os.replace(tmp, dst)
    print('mobile bank source ready: %d copied, existing files verified' % len(pending))

if __name__ == '__main__':
    prepare(ROOT / 'Assets/StreamingAssets/Audio/GeneratedSoundBanks')
