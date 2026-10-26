#!/bin/sh
# Build the Android game (Unity 6000.6): Builds/android_<time>/Dpr.apk (IL2CPP ARM64, Vulkan + OpenGLES3, Wwise audio, every iOS render /
# shadow / frame-rate fix, on-screen touch controller) + data/ (bundles + movies + Wwise banks).
# Needs Unity's Android Build Support in the editor, an Android SDK + NDK and a JDK 17 (the toolchain step checks and says what is missing).
# Close any Unity window that has this project open first (Unity opens a project once). Steps + resume: setup/stages/build_android.py
#   sh setup/build_android.sh                  all steps (an interrupted run resumes at the step that did not finish)
#   sh setup/build_android.sh --from player    redo from one step (toolchain | inputs | offline | compile | switch | check | textures | bundles | player)
#   sh setup/build_android.sh --dev            a development APK
#   sh setup/build_android.sh --back-to-mac    active build target back to StandaloneOSX (setup/build_mac.sh refuses any other)
# Then: sh setup/deploy_android.sh   (install + push the data + launch on the Pixel 6 Pro)
# Reports: SetupWork/build/reports/android_*.txt   Unity logs: SetupWork/logs/unity_BdspWinBuild_WinBuild_Android*.log
here=$(cd "$(dirname "$0")" && pwd)
project=$(cd "$here/.." && pwd)
py="$project/SetupWork/venv/bin/python"
[ -x "$py" ] || py=python3
exec "$py" -I "$here/stages/build_android.py" "$@"
