#!/usr/bin/env python3
"""Package a Unity Gradle export in a NEW directory, without signing or devices."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import zipfile


def sha(p):
    h = hashlib.sha256()
    with Path(p).open('rb') as f:
        for b in iter(lambda: f.read(1024 * 1024), b''):
            h.update(b)
    return h.hexdigest()


def unsigned_gradle(text):
    # Unity-generated Groovy only. Do not evaluate signing values or print them.
    while True:
        m = re.search(r'(?m)^\s*signingConfigs\s*\{', text)
        if not m:
            break
        end, depth = m.end(), 1
        while end < len(text) and depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        if depth:
            raise RuntimeError('Unrecognized signing block; refusing to package.')
        text = text[:m.start()] + '\n' + text[end:]
    text = re.sub(r'(?m)^\s*signingConfig\s+signingConfigs\.\w+\s*$', '\n', text)
    if re.search(r'(?i)storePassword|keyPassword|storeFile|keyAlias|signingConfig', text):
        raise RuntimeError('Unrecognized signing settings; refusing to copy credentials.')
    return text


def prepare(source, output, sdk):
    source, output = source.resolve(), output.resolve()
    if output.exists() or source == output or source in output.parents:
        raise RuntimeError('Output must be a new directory outside the input export.')
    if not (source / 'settings.gradle').is_file():
        raise RuntimeError('Input is not a Unity Gradle export.')
    files = []
    skipped = {'build', '.gradle', '.cxx', '.git', '.DS_Store'}
    for directory, dirs, names in os.walk(source):
        # 'build' is Gradle output only next to a build script; a fresh Unity 6 export ships the IL2CPP compiler itself under
        # unityLibrary/src/main/Il2CppOutputProject/IL2CPP/build/deploy, which must be copied.
        gradle_module = (Path(directory) / 'build.gradle').is_file() or (Path(directory) / 'build.gradle.kts').is_file()
        dirs[:] = [d for d in dirs if d not in skipped or (d == 'build' and not gradle_module)]
        if any((Path(directory) / d).is_symlink() for d in dirs):
            raise RuntimeError('Symlink in Gradle export; refusing.')
        for name in names:
            p = Path(directory) / name
            if name in skipped or name == 'local.properties':
                continue
            if p.is_symlink() or p.suffix.lower() in {'.jks', '.keystore', '.p12', '.pfx', '.mobileprovision'}:
                raise RuntimeError('Symlink or signing material in Gradle export; refusing.')
            data = None
            if p.suffix == '.gradle':
                data = unsigned_gradle(p.read_text())
            elif p.name == 'gradle.properties':
                data = p.read_text()
                if re.search(r'(?i)password|token|secret|signing', data):
                    raise RuntimeError('Sensitive Gradle properties; refusing.')
                data = re.sub(r'(?m)^unity(?:\.|)ProjectPath=.*$', 'unityProjectPath=' + str(output / 'Gradle'), data)
                data = re.sub(r'(?m)^unity\.projectPath=.*$', 'unity.projectPath=' + str(output / 'Gradle'), data)
            files.append((p, p.relative_to(source), data))
    output.mkdir(parents=True)
    root = output / 'Gradle'
    for p, rel, data in files:
        dest = root / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        if data is None:
            shutil.copy2(p, dest)
        else:
            dest.write_text(data)
    (root / 'local.properties').write_text('sdk.dir=' + str(sdk.resolve()) + '\n')
    # Defense in depth: disable signing on every application variant, and refuse
    # any actual signing or device task in the task graph (validateSigning is OK).
    init = output / 'unsigned.init.gradle'
    init.write_text('''gradle.projectsEvaluated {
    allprojects { p ->
        if (p.plugins.hasPlugin('com.android.application')) {
            p.android.buildTypes.all { signingConfig = null }
        }
    }
}
gradle.taskGraph.whenReady { graph ->
    graph.allTasks.each { t ->
        if (t.name ==~ /(?i)(sign|install|uninstall|publish).*/) {
            throw new GradleException('Signing/device/publishing task forbidden')
        }
    }
}
''')
    return root, init


def verify(apk, sdk, jdk):
    with zipfile.ZipFile(apk) as z:
        if z.testzip():
            raise RuntimeError('APK CRC failure.')
        names = z.namelist()
        for name in ['AndroidManifest.xml', 'classes.dex', 'lib/arm64-v8a/libAkSoundEngine.so',
                     'lib/arm64-v8a/libil2cpp.so', 'lib/arm64-v8a/libunity.so']:
            if name not in names:
                raise RuntimeError('APK missing ' + name)
        if any(n.startswith('lib/') and not n.startswith('lib/arm64-v8a/') for n in names):
            raise RuntimeError('Unexpected APK architecture.')
        if any(n.startswith(('assets/AssetAssistant/', 'assets/Audio/', 'assets/Movie/')) for n in names):
            raise RuntimeError('APK includes external game data.')
    env = {**os.environ, 'JAVA_HOME': str(jdk)}
    # apksigner verify never signs. Exit 1 with the explicit unsigned diagnostic
    # distinguishes unsigned output from a signed APK or an unreadable container.
    check = subprocess.run([str(sdk / 'build-tools/34.0.0/apksigner'), 'verify', str(apk)],
                           capture_output=True, text=True, env=env)
    if check.returncode == 0 or 'Missing META-INF/MANIFEST.MF' not in check.stderr + check.stdout:
        raise RuntimeError('Expected unsigned APK; signature verifier gave an unexpected result.')
    check = subprocess.run([str(sdk / 'build-tools/34.0.0/aapt2'), 'dump', 'badging', str(apk)],
                           capture_output=True, text=True)
    if check.returncode or "name='com.opendpr.bdsp'" not in check.stdout:
        raise RuntimeError('APK manifest validation failed.')
    return {'sha256': sha(apk), 'bytes': apk.stat().st_size, 'signed': False,
            'runtime_validated': False, 'manifest': check.stdout.splitlines()[0]}


def package(source, output, sdk, jdk, gradle, cache=None, offline=False):
    root, init = prepare(source, output, sdk)
    home = output / 'gradle-home'
    home.mkdir()
    if cache:
        # Copy caches only, never ~/.gradle/init.d, gradle.properties or credentials.
        shutil.copytree(cache / 'caches', home / 'caches')
    jars = list((gradle / 'lib').glob('gradle-launcher-*.jar'))
    if len(jars) != 1:
        raise RuntimeError('Expected one launcher jar in Unity Gradle lib directory.')
    env = dict(os.environ)
    for key in list(env):
        if key.startswith(('ORG_GRADLE_PROJECT_', 'BDSP_')) or key in ('GRADLE_OPTS', 'JAVA_OPTS', 'JAVA_TOOL_OPTIONS', '_JAVA_OPTIONS'):
            del env[key]
    env.update(JAVA_HOME=str(jdk), GRADLE_USER_HOME=str(home), ANDROID_HOME=str(sdk))
    cmd = [str(jdk / 'bin/java'), '-classpath', str(jars[0]), 'org.gradle.launcher.GradleMain',
           '--no-daemon', '--console=plain', '--max-workers=4', '-g', str(home), '-I', str(init)]
    if offline:
        cmd.append('--offline')
    cmd += [':launcher:assembleRelease']
    with (output / 'gradle.log').open('w') as log:
        log_path = output / 'gradle.log'
        log_path.chmod(0o600)
        rc = subprocess.run(cmd, cwd=root, env=env, stdout=log, stderr=subprocess.STDOUT).returncode
    if rc:
        raise RuntimeError('Gradle failed (exit %d); inspect the private gradle.log.' % rc)
    apk = root / 'launcher/build/outputs/apk/release/launcher-release-unsigned.apk'
    receipt = verify(apk, sdk, jdk)
    dest = output / 'Dpr-unsigned.apk'
    shutil.copy2(apk, dest)
    receipt['apk'] = str(dest)
    (output / 'package.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print('Unsigned APK packaged and structurally checked: ' + str(dest))
    return dest


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--export', type=Path, required=True)
    ap.add_argument('--output', type=Path, required=True)
    ap.add_argument('--sdk', type=Path, required=True)
    ap.add_argument('--jdk', type=Path, required=True)
    ap.add_argument('--gradle', type=Path, required=True)
    ap.add_argument('--cache', type=Path, help='optional local Gradle home; copy caches only')
    ap.add_argument('--offline', action='store_true')
    a = ap.parse_args()
    package(a.export.resolve(), a.output.resolve(), a.sdk.resolve(), a.jdk.resolve(),
            a.gradle.resolve(), a.cache.resolve() if a.cache else None, a.offline)


if __name__ == '__main__':
    try:
        main()
    except (RuntimeError, OSError, zipfile.BadZipFile) as e:
        sys.exit('STOP: ' + str(e))
