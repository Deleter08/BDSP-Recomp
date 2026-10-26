#!/bin/sh
# Build the iOS game: Builds/ios_<time>/Xcode (IL2CPP Xcode project, compiled unsigned by xcodebuild) + data/ (bundles + movies).
# Needs Unity's iOS Build Support in the editor (the toolchain step prints the install command) and Xcode.
# Close any Unity window that has this project open first (Unity opens a project once). Steps + resume: setup/stages/build_ios.py
#   sh setup/build_ios.sh                  all steps (an interrupted run resumes at the step that did not finish)
#   sh setup/build_ios.sh --from player    redo from one step (toolchain | inputs | offline | compile | switch | check | bundles | player | xcode)
#   sh setup/build_ios.sh --dev            a development build
#   sh setup/build_ios.sh --back-to-mac    active build target back to StandaloneOSX (setup/build_mac.sh refuses any other)
# Then (yours): open the Xcode project, pick your Team, Run on the iPhone; copy data/StreamingAssets into the app's Documents.
# Reports: SetupWork/build/reports/ios_*.txt   Unity logs: SetupWork/logs/unity_BdspWinBuild_WinBuild_Ios*.log
here=$(cd "$(dirname "$0")" && pwd)
project=$(cd "$here/.." && pwd)
py="$project/SetupWork/venv/bin/python"
[ -x "$py" ] || py=python3
exec "$py" -I "$here/stages/build_ios.py" "$@"
