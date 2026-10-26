#!/bin/sh
# Builds Assets/Plugins/macOS/PcGamepad.bundle (x86_64 + arm64) from PcGamepad.m, and padprobe (a terminal pad tester, no Unity).
#   sh setup/native/pcgamepad/build.sh      (quit Unity first: overwriting a loaded plugin can crash the editor)
set -e
here=$(cd "$(dirname "$0")" && pwd)
project=$(cd "$here/../../.." && pwd)
out="$project/Assets/Plugins/macOS/PcGamepad.bundle"
flags="-arch x86_64 -arch arm64 -mmacosx-version-min=10.13 -fobjc-arc -O2 -Wall"
mkdir -p "$out/Contents/MacOS"
clang $flags -bundle -framework Foundation -framework GameController "$here/PcGamepad.m" -o "$out/Contents/MacOS/PcGamepad"
cat > "$out/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleExecutable</key><string>PcGamepad</string>
	<key>CFBundleIdentifier</key><string>bdsp.pc.gamepad</string>
	<key>CFBundleName</key><string>PcGamepad</string>
	<key>CFBundlePackageType</key><string>BNDL</string>
	<key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
	<key>CFBundleShortVersionString</key><string>1.0</string>
</dict>
</plist>
PLIST
codesign -s - -f "$out" >/dev/null 2>&1 || true
clang $flags -framework Foundation -framework GameController "$here/padprobe.m" "$here/PcGamepad.m" -o "$here/padprobe"
codesign -s - -f "$here/padprobe" >/dev/null 2>&1 || true
echo "built $out and $here/padprobe"
