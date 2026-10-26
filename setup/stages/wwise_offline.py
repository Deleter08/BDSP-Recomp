#!/usr/bin/env python3
"""Consume the user's own Audiokinetic Launcher offline package of the Wwise Unity Integration 2019.2.15.2215.

The offline package is the directory the Launcher writes for an "offline/package" download (bundle.json, install-entry.json and
the platform tarballs). Nothing is downloaded here. Unity.Windows.tar.xz carries the shared Common/Components/Editor code (there is
no separate Common archive in this bundle); Unity.Mac.tar.xz the Mac bindings + universal engines; UnityDocumentation.Windows/Mac
the help files under Wwise/Documentation; the two mobile archives the mobile engines and platform scripts.

  verify   bundle.json identity (id, version 2019.2.15 build 2215, SDK build 7667) and every needed archive's size + SHA-1, both
           against the local manifest AND the pins below (a locally edited manifest cannot widen what is accepted).
  extract  regular files and directories only; absolute paths, "..", links, devices and FIFOs are refused before anything is
           written. Output goes to a new private directory that is renamed into place only when complete, so a failed or
           interrupted extraction never looks finished.
  prepare  Windows + Mac (+ their documentation) archives -> <work>/wwise-offline/<key>/project/Assets/Wwise (a Launcher-project-shaped tree the existing
           validator/copier consumes). A receipt binds the tree to the archive hashes; a matching receipt is reused, anything else
           at the final path is refused rather than overwritten.

  python -I setup/stages/wwise_offline.py verify --package DIR [--target android|ios|both]
  python -I setup/stages/wwise_offline.py prepare --package DIR [--work DIR]
"""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import sys
import tarfile
import time

PACKAGE_ID = "unityintegration.2019_2_15_2215"
VERSION = {"year": 2019, "major": 2, "minor": 15, "build": 2215}
SDK_BUILD = 7667
# Sizes/SHA-1 from the Launcher bundle manifest, recomputed locally on 2026-10-26 (handoff/REPORT-wwise-local-reconciliation.md).
ARCHIVES = {
    "Unity.Windows.tar.xz": (59325088, "9ec6cc62a3bcda2e7a3a23a1ae40b58847db8326"),
    "Unity.Mac.tar.xz": (56527464, "7dd34c9591e6b269353acc8860b3ce5580c08ac4"),
    "Unity.Android.tar.xz": (43460992, "8b37f847b0ec4e26de59c540c2c5a0e8b3d6ebbe"),
    "Unity.iOS.tar.xz": (925653652, "83b5b85fb7ed5434e0f1963ef3636791ad03a2fb"),
    # Integration help files only; the project tracks their .meta files, so a complete install carries them.
    "UnityDocumentation.Windows.tar.xz": (9785084, "ee9bcfaddc818890cce5b18bc4d8761b6d5de02c"),
    "UnityDocumentation.Mac.tar.xz": (9811148, "dc44a43ca82ce782e3897e8cd53d590ab2f30c05"),
}
COMMON = ("Unity.Windows.tar.xz", "Unity.Mac.tar.xz", "UnityDocumentation.Windows.tar.xz", "UnityDocumentation.Mac.tar.xz")
MOBILE = {"android": "Unity.Android.tar.xz", "ios": "Unity.iOS.tar.xz"}
NEW_VERSION = {"sdk": "2019.2.15", "sdk_build": 7667, "bundle": "2019.2.15.2215", "integration": 19}
RECEIPT = "offline-source.json"


class PackageError(RuntimeError):
    pass


def digest(path, kind="sha1"):
    h = hashlib.new(kind)
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 22), b""):
            h.update(chunk)
    return h.hexdigest()


def needed(targets=()):
    return list(COMMON) + [MOBILE[t] for t in targets]


def load_manifest(package):
    """The local Launcher manifest, checked against the pinned package identity."""
    path = Path(package) / "bundle.json"
    try:
        manifest = json.loads(path.read_text())
    except (OSError, ValueError) as e:
        raise PackageError("%s: unreadable Launcher bundle manifest (%s); supply the offline package directory" % (path, e))
    if manifest.get("id") != PACKAGE_ID:
        raise PackageError("%s: package id %r, need %s" % (path, manifest.get("id"), PACKAGE_ID))
    version = manifest.get("version") or {}
    if any(version.get(k) != v for k, v in VERSION.items()):
        raise PackageError("%s: version %r, need 2019.2.15.2215; a different integration needs a reviewed pin update" % (path, version))
    sdk = (manifest.get("productDependentData") or {}).get("wwiseSdkBuild")
    if sdk != SDK_BUILD:
        raise PackageError("%s: SDK build %r, need %d" % (path, sdk, SDK_BUILD))
    files = {}
    for entry in manifest.get("files") or []:
        if isinstance(entry, dict) and "name" in entry:
            files[entry["name"]] = (entry.get("size"), str(entry.get("sha1", "")).lower())
    return manifest, files


def verify_package(package, targets=(), hashing=True):
    """name -> Path for every needed archive; all problems are reported together."""
    package = Path(package)
    _, files = load_manifest(package)
    problems, found = [], {}
    for name in needed(targets):
        pin = ARCHIVES[name]
        if files.get(name) != pin:
            problems.append("%s: manifest entry %r differs from the pinned %r" % (name, files.get(name), pin))
            continue
        p = package / name
        if p.is_symlink() or not p.is_file():
            problems.append("%s: missing from %s (select that platform in the Launcher's offline package)" % (name, package))
            continue
        if p.stat().st_size != pin[0]:
            problems.append("%s: %d bytes, expected %d (corrupt or partial download)" % (name, p.stat().st_size, pin[0]))
            continue
        if hashing and digest(p) != pin[1]:
            problems.append("%s: SHA-1 differs from the manifest (corrupt download)" % name)
            continue
        found[name] = p
    entry = package / "install-entry.json"
    if entry.is_file():
        # Optional Launcher receipt; the pinned bundle.json + archive hashes above are what is relied on.
        try:
            receipt = json.loads(entry.read_text())
        except ValueError:
            receipt = None
        if not isinstance(receipt, dict) or receipt.get("installType") != "package":
            problems.append("install-entry.json: present but not a Launcher offline-package receipt")
    if problems:
        raise PackageError("Wwise offline package rejected:\n  " + "\n  ".join(problems))
    return found


def parse_new_version(text):
    """Fields of the vendor NewVersion.txt / Version.txt record; unknown layouts are errors, not guesses."""
    sdk = re.search(r"Based on Wwise SDK: (\d{4}\.\d+\.\d+) Build (\d+)", text)
    bundle = re.search(r"Unity Integration Bundle: (\d{4}\.\d+\.\d+\.\d+)", text)
    integ = re.search(r"Unity Integration Version: (\d+)", text)
    if not (sdk and bundle and integ):
        raise PackageError("unrecognized Wwise version record")
    return {"sdk": sdk.group(1), "sdk_build": int(sdk.group(2)), "bundle": bundle.group(1), "integration": int(integ.group(1))}


def check_member(member):
    """Normalized relative name of a safe member, else PackageError."""
    name = member.name
    if not name or "\\" in name or "\x00" in name:
        raise PackageError("unsafe archive member name %r" % name)
    p = PurePosixPath(name)
    if p.is_absolute() or ".." in p.parts:
        raise PackageError("unsafe archive member path %r" % name)
    if not (member.isfile() or member.isdir()):
        raise PackageError("archive member %r is a link/device/FIFO; only regular files are accepted" % name)
    parts = [x for x in p.parts if x not in ("", ".")]
    if not parts:
        return None
    return "/".join(parts)


def safe_extract(archive, dest, select=lambda name: True):
    """Extract selected regular files of an xz tarball into the NEW directory dest; returns the extracted names."""
    dest = Path(dest)
    if dest.exists() or dest.is_symlink():
        raise PackageError("%s already exists; refusing to extract over it" % dest)
    names = []
    with tarfile.open(archive, "r:xz") as tar:
        members = []
        for m in tar:                      # validate everything before writing anything
            rel = check_member(m)
            if rel and select(rel):
                members.append((m, rel))
        dest.parent.mkdir(parents=True, exist_ok=True)
        partial = dest.parent / (".partial-%s-%d-%d" % (dest.name, os.getpid(), time.time_ns()))
        partial.mkdir()
        try:
            seen = set()
            for m, rel in members:
                out = partial / rel
                if m.isdir():
                    out.mkdir(parents=True, exist_ok=True)
                    continue
                if rel in seen:
                    raise PackageError("%s: duplicate member %s" % (archive, rel))
                seen.add(rel)
                out.parent.mkdir(parents=True, exist_ok=True)
                src = tar.extractfile(m)
                with open(out, "xb") as f:
                    shutil.copyfileobj(src, f, 1 << 22)
                os.chmod(out, 0o755 if m.mode & 0o111 else 0o644)
                names.append(rel)
        except BaseException:
            shutil.rmtree(partial, ignore_errors=True)    # our own scratch directory only
            raise
    os.rename(partial, dest)
    return names


def prepare_source(package, work):
    """Windows + Mac (+ documentation) archives -> a Launcher-project-shaped tree; returns the project root (holding Assets/Wwise)."""
    found = verify_package(package)
    key = hashlib.sha256("".join(ARCHIVES[n][1] for n in COMMON).encode()).hexdigest()[:16]
    root = Path(work) / "wwise-offline" / ("2019.2.15.2215-" + key)
    receipt_path = root / RECEIPT
    expected = {n: ARCHIVES[n][1] for n in COMMON}
    if receipt_path.is_file():
        receipt = json.loads(receipt_path.read_text())
        if receipt.get("archives_sha1") == expected and (root / "project/Assets/Wwise").is_dir():
            print("wwise_offline: reusing verified extraction %s" % root)
            return root / "project"
    if root.exists():
        raise PackageError("%s exists without a matching completion receipt; it was left by an interrupted run. "
                           "Move it aside (nothing is deleted automatically) and run again." % root)
    root.parent.mkdir(parents=True, exist_ok=True)
    partial = root.parent / (".partial-%s-%d" % (root.name, time.time_ns()))
    try:
        return _prepare(package, found, partial, root, expected)
    except BaseException:
        shutil.rmtree(partial, ignore_errors=True)        # our own scratch directory only
        raise


def _prepare(package, found, partial, root, expected):
    wwise = partial / "project" / "Assets" / "Wwise"
    owners = {}
    for name in COMMON:
        part = partial / ("x-" + name)
        print("wwise_offline: extracting %s ..." % name, flush=True)
        for rel in safe_extract(found[name], part, lambda r: r.startswith("Wwise/")):
            target = wwise / rel[len("Wwise/"):]
            source = part / rel
            if target.exists():
                if digest(target, "sha256") != digest(source, "sha256"):
                    raise PackageError("%s and %s disagree on %s" % (owners[rel], name, rel))
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            os.replace(source, target)
            owners[rel] = name
        shutil.rmtree(part)                # our own private scratch copy, never a user path
    version_file = wwise / "NewVersion.txt"
    if not version_file.is_file():
        raise PackageError("the package has no Wwise/NewVersion.txt; not a Unity integration offline package")
    info = parse_new_version(version_file.read_text(errors="replace"))
    if info != NEW_VERSION:
        raise PackageError("NewVersion.txt says %r, manifest says 2019.2.15.2215 / SDK 7667" % info)
    receipt = {"package": PACKAGE_ID, "version": "2019.2.15.2215", "sdk_build": SDK_BUILD, "archives_sha1": expected,
               "new_version": info, "new_version_sha256": digest(version_file, "sha256"), "files": len(owners),
               "source": str(Path(package).resolve()), "created": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())}
    (partial / RECEIPT).write_text(json.dumps(receipt, indent=2) + "\n")
    os.rename(partial, root)
    print("wwise_offline: %d files from %s -> %s" % (len(owners), " + ".join(COMMON), root / "project"))
    return root / "project"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("action", choices=["verify", "prepare"])
    ap.add_argument("--package", required=True)
    ap.add_argument("--target", choices=["android", "ios", "both"])
    ap.add_argument("--work")
    a = ap.parse_args()
    try:
        if a.action == "verify":
            targets = [] if not a.target else (["android", "ios"] if a.target == "both" else [a.target])
            for name, p in verify_package(a.package, targets).items():
                print("OK %s %s" % (name, ARCHIVES[name][1]))
        else:
            if not a.work:
                sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "lib"))
                import paths
                a.work = paths.WORK
            print(prepare_source(a.package, a.work))
    except PackageError as e:
        sys.exit(str(e))


if __name__ == "__main__":
    main()
