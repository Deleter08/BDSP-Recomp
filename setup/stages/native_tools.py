#!/usr/bin/env python3
"""Compile source-backed local helpers without calling codesign or downloading binaries."""
from pathlib import Path
import os
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]

def main():
    crunch = ROOT / 'setup/bin/crunch_src'
    commands = [['xcrun', 'clang++', '-O2', str(crunch / 'crn_levels.cpp'), '-o', str(crunch / 'crn_levels')]]
    for folder, name, frameworks in [('pcgamepad', 'PcGamepad', ['Foundation', 'GameController']),
                                     ('pcmacdialog', 'PcMacDialog', ['Cocoa'])]:
        out = ROOT / ('Assets/Plugins/macOS/' + name + '.bundle/Contents/MacOS/' + name)
        out.parent.mkdir(parents=True, exist_ok=True)
        cmd = ['xcrun', 'clang', '-arch', 'arm64', '-arch', 'x86_64', '-mmacosx-version-min=10.13',
               '-fobjc-arc', '-O2', '-Wall', '-bundle']
        for framework in frameworks:
            cmd += ['-framework', framework]
        cmd += [str(ROOT / ('setup/native/' + folder + '/' + name + '.m')), '-o', str(out)]
        commands.append(cmd)
    for cmd in commands:
        subprocess.run(cmd, check=True, stdin=subprocess.DEVNULL)
    # Small execution smoke check: expected usage failure, never decode game content here.
    p = subprocess.run([str(crunch / 'crn_levels')], capture_output=True, text=True)
    if p.returncode != 2 or 'usage:' not in p.stderr:
        sys.exit('crn_levels smoke check failed')
    print('Source helpers compiled; crn_levels usage smoke passed. No codesign invoked.')

if __name__ == '__main__':
    main()
