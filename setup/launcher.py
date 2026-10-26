#!/usr/bin/env python3
"""Resumable Apple Silicon setup, unsigned Android APK and unsigned iOS build.

Default action is preflight. Use run to prepare/build, or install with an explicit
artifact, device and --allow-install. No game/SDK acquisition or signing is automated.
"""
import argparse
import contextlib
import fcntl
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path
import platform
import plistlib
import re
import shutil
import subprocess
import sys
import time
import tempfile

ROOT = Path(__file__).resolve().parent.parent
WORK = ROOT / 'SetupWork'
UNITY = '6000.6.6f1'
# Observed macOS Free 1.3.14 executable; version corroborated by its startup log.
# Not a vendor signature. Unknown executables require a reviewed pin update.
ASSETRIPPER_SHA256 = '8e0148f64a183c0e5d85bd1b42caed9136fb70db7e56819acff41258c5b13869'
ARCHIVES = {'ios': '83b5b85fb7ed5434e0f1963ef3636791ad03a2fb',
            'android': '8b37f847b0ec4e26de59c540c2c5a0e8b3d6ebbe'}
COMMON = ['export', 'fix_arrays', 'normalise', 'merge', 'dotween', 'wwise', 'startup',
          'shaders', 'containers', 'atlas_spec', 'sprite_bake', 'atlases', 'placeholders',
          'sprite_restore', 'd7', 'untag', 'verify']
MOBILE = ['toolchain', 'inputs', 'compile', 'switch', 'check', 'textures', 'bundles', 'player']
PATH_KEYS = ['dump', 'unity', 'unity_mono', 'wwise', 'wwise_package', 'assetripper', 'dotween', 'wwise_ios', 'wwise_android',
             'android_sdk', 'android_ndk', 'android_jdk']
PACKAGE_ARCHIVES = {'ios': 'Unity.iOS.tar.xz', 'android': 'Unity.Android.tar.xz'}


def mobile_archive(cfg, target):
    """Explicit --wwise-<target> archive, else the one inside the offline package."""
    if cfg.get('wwise_' + target):
        return cfg['wwise_' + target]
    if cfg.get('wwise_package'):
        return str(Path(cfg['wwise_package']) / PACKAGE_ARCHIVES[target])
    return None


def digest(path, kind='sha256'):
    h = hashlib.new(kind)
    with open(path, 'rb') as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()


def atomic(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + '.tmp')
    fd = os.open(tmp, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
    with os.fdopen(fd, 'w') as f:
        f.write(json.dumps(value, indent=2) + '\n')
        f.flush()
        os.fsync(f.fileno())
    os.replace(tmp, path)


def read_json(path):
    return json.loads(path.read_text()) if path.exists() else {}


def checked(cmd, **kwargs):
    p = subprocess.run([str(x) for x in cmd], stdin=subprocess.DEVNULL, **kwargs)
    if p.returncode:
        raise RuntimeError('Command failed (exit %d); see the stage log.' % p.returncode)
    return p


@contextlib.contextmanager
def lock(work):
    work.mkdir(parents=True, exist_ok=True)
    with (work / 'launcher.lock').open('a') as f:
        try:
            fcntl.flock(f, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise RuntimeError('Another launcher owns this workspace. Wait for it to exit.')
        yield


def idle():
    p = subprocess.run(['ps', '-axo', 'comm='], capture_output=True, text=True)
    if p.returncode:
        raise RuntimeError('Cannot inspect running processes; preflight cannot guarantee exclusive Unity access.')
    if any(line.rstrip().endswith('/Unity.app/Contents/MacOS/Unity') or line.strip() == 'Unity'
           for line in p.stdout.splitlines()):
        raise RuntimeError('Close all Unity editors/builds before running this launcher (one Unity job at a time).')


def targets(cfg):
    return ['android', 'ios'] if cfg['target'] == 'both' else [cfg['target']]


def environment(cfg):
    env = dict(os.environ)
    # Do not inherit alternate workspaces or signing/team settings.
    for key in list(env):
        if key.startswith('BDSP_'):
            del env[key]
    env.update(BDSP_SETUP_CONFIG=str(WORK / 'launcher-stage-config.json'),
               BDSP_DOTWEEN_SOURCE=cfg.get('dotween', ''), BDSP_NO_SIGN='1')
    for key in ('android_sdk', 'android_ndk', 'android_jdk'):
        if cfg.get(key):
            env['BDSP_' + key.upper()] = cfg[key]
    for target in ('ios', 'android'):
        if mobile_archive(cfg, target):
            env['BDSP_WWISE_' + target.upper()] = mobile_archive(cfg, target)
    env['BDSP_ANDROID_TARGET_SDK'] = '34'
    return env


def stage_config(cfg):
    return {**{k: cfg[k] for k in ('dump', 'unity', 'unity_mono', 'wwise', 'wwise_package', 'assetripper') if cfg.get(k)},
            'work': str(WORK), 'unity_batchmode': True}


def pins(python):
    code = ('import importlib.metadata as m,json; '
            'print(json.dumps({d.metadata["Name"].lower().replace("_","-"):d.version for d in m.distributions()}))')
    p = subprocess.run([str(python), '-I', '-c', code], capture_output=True, text=True)
    if p.returncode:
        return ['Python environment cannot run; use deps with Python 3.12+.']
    installed = json.loads(p.stdout)
    return [line for line in (ROOT / 'setup/requirements.lock').read_text().splitlines()
            if '==' in line and installed.get(line.split('==')[0].lower().replace('_', '-')) != line.split('==')[1]]


def preflight(cfg, python):
    """Aggregate actionable failures before export, copying dependencies, or Unity."""
    bad = []
    def probe(label, fn):
        try:
            fn()
            print('OK ' + label, flush=True)
        except (OSError, ValueError, RuntimeError, KeyError, plistlib.InvalidFileException) as e:
            bad.append(label + ': ' + str(e))
    def require(ok, message):
        if not ok:
            raise RuntimeError(message)
    probe('host', lambda: require(sys.platform == 'darwin' and platform.machine() == 'arm64',
                                 'Apple Silicon macOS with native arm64 Python required.'))
    probe('exclusive Unity access', idle)
    probe('disk', lambda: require(shutil.disk_usage(ROOT).free >= 60 * 2**30,
                                 'At least 60 GiB free required; full mobile outputs need more.'))
    probe('Python pins', lambda: require(not pins(python),
          'Missing/mismatched setup/requirements.lock packages; run sh setup/launch.sh deps.'))
    def dump():
        sys.path.insert(0, str(ROOT / 'setup/stages'))
        import check
        base = Path(cfg.get('dump', '/missing'))
        for rel, sha, size in check.FINGERPRINTS:
            p = base / rel
            require(p.is_file() and p.stat().st_size == size and digest(p, 'sha1') == sha,
                    'Supply your own complete BD v1.3.0 dump via --dump; wrong/missing ' + rel)
        require((base / 'romfs/Data/StreamingAssets/AssetAssistant').is_dir(), 'Complete romfs/Data required.')
    probe('game dump fingerprints', dump)
    def unity():
        p = Path(cfg.get('unity', '/missing'))
        require(p.is_file(), 'Set --unity to the licensed 6000.6.6f1 editor executable.')
        info = plistlib.loads((p.parents[1] / 'Info.plist').read_bytes())
        require(info.get('CFBundleVersion') == UNITY or info.get('CFBundleShortVersionString') == UNITY,
                'Editor Info.plist does not identify ' + UNITY)
        engines = p.parents[3] / 'PlaybackEngines'
        for t in targets(cfg):
            rel = ('iOSSupport/iOSPlayerBuildProgram.exe' if t == 'ios'
                   else 'AndroidPlayer/AndroidPlayerBuildProgram.exe')
            require((engines / rel).is_file(), 'Add matching ' + t + ' Build Support in Unity Hub.')
    probe('Unity and modules', unity)
    def mono():
        p = Path(cfg.get('unity_mono', '/missing'))
        require(p.is_file(), '--unity-mono must name a licensed Unity 2019.4.41f2 editor (manifest helper).')
        info = plistlib.loads((p.parents[1] / 'Info.plist').read_bytes())
        require(info.get('CFBundleVersion') == '2019.4.41f2', 'Manifest runtime requires Unity 2019.4.41f2.')
        base = p.parents[1] / 'MonoBleedingEdge'
        require((base / 'lib/mono/4.5/mcs.exe').is_file(), 'Unity 2019 Mono compiler missing.')
        checked(['arch', '-x86_64', base / 'bin/mono', '--version'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    probe('manifest Mono / Rosetta runtime', mono)
    probe('AssetRipper 1.3.14 pin', lambda: require(
        Path(cfg.get('assetripper', '/missing')).is_file() and digest(cfg['assetripper']) == ASSETRIPPER_SHA256,
        'Supply the pinned macOS Free 1.3.14 executable via --assetripper; see docs/CLEAN_SETUP.md.'))
    def dotween():
        sys.path.insert(0, str(ROOT / 'setup/stages'))
        import dotween as vendor
        for rel, sha in vendor.FILES.items():
            p = Path(cfg.get('dotween', '/missing')) / rel
            require(p.is_file() and digest(p) == sha, 'Supply DOTween 1.2.335 via --dotween; missing/different ' + rel)
    probe('DOTween pins', dotween)
    def wwise():
        require(bool(cfg.get('wwise') or cfg.get('wwise_package')),
                '--wwise-package needs your Launcher offline package of 2019.2.15.2215 (or --wwise, a Launcher-integrated project).')
        if cfg.get('wwise_package'):
            # Verifies the pinned manifest/archives, then extracts Windows + Mac once into SetupWork/wwise-offline (receipted,
            # reused on later runs). Writes no Assets/ProjectSettings files.
            source = ['--package', cfg['wwise_package']]
        else:
            source = ['--source', cfg['wwise']]
        p = subprocess.run([str(python), '-I', str(ROOT / 'setup/stages/wwise.py'), '--validate-source', '--unity6'] + source,
                           capture_output=True, text=True)
        if p.returncode:
            raise RuntimeError((p.stderr or p.stdout).strip() + ' See docs/CLEAN_SETUP.md#wwise.')
        print(p.stdout.strip())
    probe('Wwise common source', wwise)
    for target in targets(cfg):
        def archive(t=target):
            p = Path(mobile_archive(cfg, t) or '/missing')
            require(p.is_file() and digest(p, 'sha1') == ARCHIVES[t],
                    'Supply Launcher Unity.' + ('iOS' if t == 'ios' else 'Android') +
                    '.tar.xz for 2019.2.15.7667.2215 in --wwise-package or via --wwise-' + t + ' (pinned archive required).')
        probe('Wwise ' + target + ' archive', archive)
    def xcode():
        checked(['xcrun', '--find', 'clang++'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        if 'ios' in targets(cfg):
            checked(['xcrun', '--sdk', 'iphoneos', '--show-sdk-version'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    probe('Xcode toolchain', xcode)
    if 'android' in targets(cfg):
        def android():
            sdk = Path(cfg.get('android_sdk', str(Path.home() / 'Library/Android/sdk')))
            ndk = Path(cfg.get('android_ndk', str(sdk / 'ndk/27.2.12479018')))
            require('Pkg.Revision = 27.2.12479018' in (ndk / 'source.properties').read_text(), 'Install NDK 27.2.12479018.')
            for rel in ['platform-tools/adb', 'platforms/android-34/android.jar', 'build-tools/34.0.0/aapt2', 'cmake/3.22.1/bin/cmake']:
                require((sdk / rel).is_file(), 'Android SDK missing ' + rel + '; see official acquisition guide.')
            jdk = Path(cfg.get('android_jdk', '/opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home'))
            r = checked([jdk / 'bin/java', '-version'], capture_output=True, text=True)
            require('"17.' in r.stderr + r.stdout, 'JDK 17 required via --android-jdk.')
        probe('Android pins', android)
    for problem in bad:
        print('BLOCKED ' + problem, flush=True)
    if bad:
        print('No preparation/build was started. Acquisition guide: docs/CLEAN_SETUP.md')
    return not bad


def input_identity(cfg):
    """Bind completed work to source dependency bytes, including unpinned common files."""
    values = {}
    if cfg.get('assetripper'):
        values['assetripper'] = digest(cfg['assetripper'])
    for target in targets(cfg):
        if mobile_archive(cfg, target):
            values['wwise_' + target] = digest(mobile_archive(cfg, target))
    if cfg.get('wwise_package'):
        base = Path(cfg['wwise_package'])
        values['wwise_package'] = {name: digest(base / name) for name in ['bundle.json', 'Unity.Windows.tar.xz', 'Unity.Mac.tar.xz',
                                                                   'UnityDocumentation.Windows.tar.xz', 'UnityDocumentation.Mac.tar.xz']}
    for key, suffix in [('wwise', 'Assets/Wwise'), ('dotween', '')]:
        if not cfg.get(key):
            continue
        root = Path(cfg[key]) / suffix
        h = hashlib.sha256()
        for p in sorted(root.rglob('*')):
            if p.is_file():
                h.update(p.relative_to(root).as_posix().encode())
                h.update(bytes.fromhex(digest(p)))
        values[key] = h.hexdigest()
    return values


def plan(cfg, python):
    py = [str(python), '-I']
    result = [('native', py + [str(ROOT / 'setup/stages/native_tools.py')])]
    for name in COMMON:
        result.append(('common.' + name, py + [str(ROOT / 'setup/setup.py'), '--only', name]))
        if name == 'merge':
            result.append(('banks', py + [str(ROOT / 'setup/stages/mobile_banks.py')]))
    for target in targets(cfg):
        for step in MOBILE + (['xcode', 'ipa'] if target == 'ios' else ['package']):
            result.append((target + '.' + step, py + [str(ROOT / ('setup/stages/build_' + target + '.py')),
                                                    '--only', step, '--source-workflow']))
    return result


def run_steps(steps, state_path, env, start=None, until=None, runner=None):
    """Only a completed command gets a durable receipt; failures remain retryable."""
    state = read_json(state_path)
    names = [name for name, _ in steps]
    if until and until not in names:
        raise RuntimeError('Unknown --until stage: ' + until)
    if start and until and start in names and names.index(until) < names.index(start):
        raise RuntimeError('--until must not precede --from.')
    if start:
        if start not in names:
            raise RuntimeError('Unknown --from stage: ' + start)
        for name in names[names.index(start):]:
            state.pop(name, None)
        atomic(state_path, state)
    for name, cmd in steps:
        if state.get(name, {}).get('done'):
            print('RESUME completed ' + name, flush=True)
        else:
            print('RUN ' + name, flush=True)
            if runner:
                runner(name, cmd, env)
            else:
                idle()
                logs = state_path.parent / 'launcher-logs'
                logs.mkdir(exist_ok=True)
                log = logs / (name + '-' + str(time.time_ns()) + '.log')
                print('Log: ' + str(log), flush=True)
                with log.open('w') as f:
                    log.chmod(0o600)
                    checked(cmd, cwd=ROOT, env=env, stdout=f, stderr=subprocess.STDOUT)
            state[name] = {'done': time.strftime('%Y-%m-%dT%H:%M:%S')}
            atomic(state_path, state)
        if name == until:
            break


def install_command(a):
    if not a.allow_install or not a.device or not a.artifact or a.target not in ('ios', 'android'):
        raise RuntimeError('Install requires --allow-install --target android|ios --device ID --artifact PATH. No automatic selection.')
    p = Path(a.artifact).expanduser().resolve()
    if a.target == 'android' and p.is_file() and p.suffix == '.apk':
        adb = shutil.which('adb') or str(Path.home() / 'Library/Android/sdk/platform-tools/adb')
        return [adb, '-s', a.device, 'install', '-r', str(p)]
    if a.target == 'ios' and p.is_dir() and p.suffix == '.app' and (p / 'embedded.mobileprovision').is_file():
        return ['xcrun', 'devicectl', 'device', 'install', 'app', '--device', a.device, str(p)]
    raise RuntimeError('Supply your separately signed APK or provisioned iOS .app; exported projects cannot be installed. No signing is performed.')


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('action', nargs='?', choices=['preflight', 'plan', 'deps', 'run', 'install'], default='preflight')
    ap.add_argument('--target', choices=['android', 'ios', 'both'])
    for key in PATH_KEYS:
        ap.add_argument('--' + key.replace('_', '-'))
    ap.add_argument('--accept-inputs', action='store_true', help='confirm your dump is legally obtained and your Unity/Wwise inputs licensed')
    ap.add_argument('--from', dest='start')
    ap.add_argument('--until')
    ap.add_argument('--allow-install', action='store_true')
    ap.add_argument('--artifact')
    ap.add_argument('--device')
    a = ap.parse_args()
    if a.action == 'install':
        cmd = install_command(a)
        checked(cmd)
        print('Install completed. No data transfer or launch performed; see README for data placement.')
        return 0
    if a.allow_install or a.artifact or a.device:
        ap.error('Device options are only accepted with the separate install action.')
    config_path = WORK / 'launcher.json'
    previous = read_json(config_path)
    cfg = dict(previous)
    for key in PATH_KEYS:
        value = getattr(a, key)
        if value:
            cfg[key] = str(Path(value).expanduser().resolve())
    cfg['target'] = a.target or cfg.get('target', 'both')
    if a.accept_inputs:
        cfg['accept_inputs'] = True
    if any(previous.get(k) != cfg.get(k) for k in PATH_KEYS) and not a.accept_inputs:
        cfg['accept_inputs'] = False
    python = WORK / 'launcher-venv/bin/python'
    if a.action == 'plan':
        for name, _ in plan(cfg, python):
            print(name)
        print('Preflight always runs; Android exports Gradle then packages an unsigned APK; iOS builds unsigned and zips an unsigned IPA. No install/sign step.')
        return 0
    with lock(Path(tempfile.gettempdir()) / ('bdsp-launcher-' + str(os.getuid()))), lock(WORK):
        if a.action == 'deps':
            if sys.version_info < (3, 12):
                raise RuntimeError('Use PYTHON=/path/to/python3.12 (or newer) sh setup/launch.sh deps.')
            if not python.exists():
                checked([sys.executable, '-m', 'venv', str(python.parent.parent)])
            if pins(python):
                checked([python, '-m', 'pip', '--isolated', 'install', '--index-url', 'https://pypi.org/simple',
                         '-r', ROOT / 'setup/requirements.lock'])
            if pins(python):
                raise RuntimeError('Dependency verification failed; no stages can run.')
            print('Pinned Python dependencies ready in SetupWork/launcher-venv.')
            return 0
        # Bind receipts to user inputs; never silently reuse a prepared dump with changed paths.
        receipts = read_json(WORK / 'launcher-state.json')
        if receipts and cfg != previous:
            raise RuntimeError('Inputs/target changed after preparation. Use a new clean source workspace; existing work is preserved.')
        atomic(config_path, cfg)
        if not cfg.get('accept_inputs'):
            print('BLOCKED: add --accept-inputs after supplying your own legally obtained dump and licensed Unity/Wwise inputs.')
            return 1
        ok = preflight(cfg, python if python.exists() else sys.executable)
        if not ok or a.action == 'preflight':
            return 0 if ok else 1
        if not python.exists() or pins(python):
            raise RuntimeError('Run sh setup/launch.sh deps before preparation.')
        # Refuse accidental use of pre-existing common/build state from another workflow.
        identity = input_identity(cfg)
        identity_path = WORK / 'launcher-inputs.json'
        if receipts and read_json(identity_path) != identity:
            raise RuntimeError('Dependency bytes changed after preparation. Use a new clean source workspace; no old outputs were reused.')
        atomic(identity_path, identity)
        if not receipts and ((WORK / 'state.json').exists() or (ROOT / 'Assets/Texture2D').exists()):
            raise RuntimeError('Existing prepared project detected. Run this launcher in a clean snapshot to preserve existing assets/builds.')
        atomic(WORK / 'launcher-stage-config.json', stage_config(cfg))
        run_steps(plan(cfg, python), WORK / 'launcher-state.json', environment(cfg), a.start, a.until)
        print('Requested stages completed. See Builds for exports; installation is a separate explicit action.')
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (RuntimeError, OSError, ValueError) as exc:
        print('STOP: ' + str(exc), file=sys.stderr)
        sys.exit(1)
