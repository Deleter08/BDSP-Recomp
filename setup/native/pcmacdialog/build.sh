#!/bin/sh
# Builds Assets/Plugins/macOS/PcMacDialog.bundle (x86_64 + arm64) from PcMacDialog.m: the software-keyboard dialogs of the macOS
# player (PcSwkbdMac.cs). The editor never loads it (its .meta enables Standalone OSX only).
#   sh setup/native/pcmacdialog/build.sh [<output .bundle>]
set -e
here=$(cd "$(dirname "$0")" && pwd)
project=$(cd "$here/../../.." && pwd)
out="${1:-$project/Assets/Plugins/macOS/PcMacDialog.bundle}"
flags="-arch x86_64 -arch arm64 -mmacosx-version-min=10.13 -fobjc-arc -O2 -Wall"
mkdir -p "$out/Contents/MacOS"
clang $flags -bundle -framework Cocoa "$here/PcMacDialog.m" -o "$out/Contents/MacOS/PcMacDialog"
cat > "$out/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleExecutable</key><string>PcMacDialog</string>
	<key>CFBundleIdentifier</key><string>bdsp.pc.macdialog</string>
	<key>CFBundleName</key><string>PcMacDialog</string>
	<key>CFBundlePackageType</key><string>BNDL</string>
	<key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
	<key>CFBundleShortVersionString</key><string>1.0</string>
</dict>
</plist>
PLIST
codesign -s - -f "$out" >/dev/null 2>&1 || true
echo "built $out"
