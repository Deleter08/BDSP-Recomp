#!/usr/bin/env python3
"""Prepare private mobile data from your dump and locally converted mobile bundles.

Raw Switch bundles cannot be copied into a mobile player. --built-data must be
the data directory produced by YOUR local build for the selected platform.
Preparation never contacts a device. install and push each require explicit opt-in.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE / 'stages'))
import check


def sha(p, algorithm='sha256'):
    h = hashlib.new(algorithm)
    with p.open('rb') as f:
        for block in iter(lambda: f.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def files(root):
    if root.is_symlink() or not root.is_dir():
        raise RuntimeError('Missing directory or symlink: ' + str(root))
    result = []
    for p in sorted(root.rglob('*')):
        if p.is_symlink():
            raise RuntimeError('Symlink in data input; refusing.')
        if p.is_file() and p.suffix != '.meta' and p.name != '.DS_Store':
            result.append(p)
    if not result:
        raise RuntimeError('Empty data input: ' + str(root))
    return result


def prepare(dump, built, target, output, accepted):
    if not accepted:
        raise RuntimeError('Add --accept-inputs for your legally obtained dump and your own converted mobile data.')
    if output.exists() or output.is_symlink():
        raise RuntimeError('Output must be a NEW directory; existing data is preserved.')
    for root in (dump, built):
        if root == output or root in output.parents:
            raise RuntimeError('Output must be outside both inputs.')
    for rel, expected, size in check.FINGERPRINTS:
        p = dump / rel
        if not p.is_file() or p.stat().st_size != size or sha(p, 'sha1') != expected:
            raise RuntimeError('Wrong/incomplete BD v1.3.0 dump: ' + rel)
    marker = built / 'BUILD.txt'
    if not marker.is_file() or not marker.read_text().startswith(target + ' player '):
        raise RuntimeError('Expected this platform\'s local player data/BUILD.txt. Run the clean launcher first.')
    bundles = built / 'StreamingAssets/AssetAssistant'
    if not (bundles / 'Dpr.bin').is_file():
        raise RuntimeError('Converted mobile Dpr.bin missing; a raw Switch dump is insufficient.')
    source = dump / 'romfs/Data/StreamingAssets'
    platform_name = {'android': 'Android', 'ios': 'iOS'}[target]
    mappings = [(bundles, 'AssetAssistant'), (source / 'Movie', 'Movie'),
                (source / 'Audio/GeneratedSoundBanks/Switch', 'Audio/GeneratedSoundBanks/' + platform_name)]
    if not (mappings[2][0] / 'Init.bnk').is_file():
        raise RuntimeError('Dump Init.bnk missing.')
    pending = []
    for root, dest in mappings:
        for p in files(root):
            rel = Path('StreamingAssets') / dest / p.relative_to(root)
            pending.append((p, rel))
    # Validate all inputs before writing; receipt is written only after every copy
    # is hashed. A BUILD marker is provenance context, not an authenticity proof.
    output.mkdir(parents=True)
    rows = {}
    for p, rel in pending:
        dst = output / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(p, dst)
        digest = sha(p)
        if sha(dst) != digest:
            raise RuntimeError('Data copy hash mismatch; incomplete output retained for inspection.')
        rows[rel.as_posix()] = {'sha256': digest, 'bytes': dst.stat().st_size}
    receipt = {'target': target, 'private_game_data': True, 'runtime_validated': False,
               'bundle_build': marker.read_text().splitlines()[0], 'files': rows}
    (output / 'mobile-data.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print('Prepared %d private data files; no device accessed: %s' % (len(rows), output))


def verify(data, target):
    receipt = json.loads((data / 'mobile-data.json').read_text())
    if receipt.get('target') != target or not receipt.get('files'):
        raise RuntimeError('Wrong/empty data receipt.')
    expected = receipt['files']
    actual = {p.relative_to(data).as_posix() for p in files(data / 'StreamingAssets')}
    if actual != set(expected):
        raise RuntimeError('Data files added or missing since preparation.')
    for rel, row in expected.items():
        if Path(rel).is_absolute() or '..' in Path(rel).parts or not rel.startswith('StreamingAssets/'):
            raise RuntimeError('Unsafe receipt path.')
        p = data / rel
        if p.stat().st_size != row['bytes'] or sha(p) != row['sha256']:
            raise RuntimeError('Data changed since preparation.')


def device_commands(a):
    if not a.allow_install or not a.device or a.device.startswith('-'):
        raise RuntimeError('Device actions require --allow-install and --device ID; no automatic device selection.')
    if not re.fullmatch(r'[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z0-9_]+)+', a.bundle_id):
        raise RuntimeError('Invalid bundle identifier.')
    if a.action == 'install':
        if not a.artifact:
            raise RuntimeError('Install requires --artifact (already signed by you).')
        sys.path.insert(0, str(HERE))
        import launcher
        return [launcher.install_command(a)]
    if not a.data:
        raise RuntimeError('Push requires --data prepared-directory.')
    data = Path(a.data).expanduser().resolve()
    verify(data, a.target)
    if a.target == 'ios':
        # Finder exposes Documents through the existing UIFileSharingEnabled flag.
        # No unverified devicectl appDataContainer access assumed for arbitrary IPAs.
        raise RuntimeError('iOS: copy StreamingAssets into Dpr Documents using Finder > device > Files > Dpr. No device command was run.')
    adb = shutil.which('adb') or str(Path.home() / 'Library/Android/sdk/platform-tools/adb')
    remote = '/sdcard/Android/data/' + a.bundle_id + '/files'
    return [[adb, '-s', a.device, 'shell', 'mkdir', '-p', remote],
            [adb, '-s', a.device, 'push', '--sync', str(data / 'StreamingAssets'), remote + '/'],
            [adb, '-s', a.device, 'shell', 'chmod', '-R', 'a+rX', remote + '/StreamingAssets']]


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('action', choices=['prepare', 'verify', 'install', 'push'])
    ap.add_argument('--target', choices=['android', 'ios'], required=True)
    ap.add_argument('--dump', type=Path)
    ap.add_argument('--built-data', type=Path)
    ap.add_argument('--output', type=Path)
    ap.add_argument('--accept-inputs', action='store_true')
    ap.add_argument('--data')
    ap.add_argument('--artifact')
    ap.add_argument('--device')
    ap.add_argument('--allow-install', action='store_true')
    ap.add_argument('--bundle-id', default='com.opendpr.bdsp')
    a = ap.parse_args()
    if a.action in ('prepare', 'verify') and (a.device or a.artifact or a.allow_install):
        ap.error('Device options cannot be used for local preparation/verification.')
    if a.action == 'prepare':
        if not all((a.dump, a.built_data, a.output)):
            ap.error('prepare requires --dump --built-data --output')
        prepare(a.dump.expanduser().resolve(), a.built_data.expanduser().resolve(),
                a.target, a.output.expanduser().resolve(), a.accept_inputs)
    elif a.action == 'verify':
        if not a.data:
            ap.error('verify requires --data')
        verify(Path(a.data).expanduser().resolve(), a.target)
        print('All prepared data hashes match. No runtime validation or device access.')
    else:
        for cmd in device_commands(a):
            subprocess.run(cmd, check=True, stdin=subprocess.DEVNULL)
        print('Requested device action completed. No signing or launch performed.')


if __name__ == '__main__':
    try:
        main()
    except (RuntimeError, OSError, ValueError, subprocess.CalledProcessError) as e:
        sys.exit('STOP: ' + str(e))
