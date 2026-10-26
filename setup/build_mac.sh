#!/bin/sh
# Build the standalone macOS game: Builds/mac_<time>/Dpr.app (Mono player, Wwise audio, DualSense via GameController).
# Close any Unity window that has this project open first (Unity opens a project once). Steps + resume: setup/stages/build_mac.py
#   sh setup/build_mac.sh                  all steps (an interrupted run resumes at the step that did not finish)
#   sh setup/build_mac.sh --from player    redo from one step (manifest | plan | variants | check | bundles | player | plugins)
# Reports: SetupWork/build/reports/mac_*.txt   Unity logs: SetupWork/logs/unity_BdspWinBuild_WinBuild_Mac*.log
here=$(cd "$(dirname "$0")" && pwd)
project=$(cd "$here/.." && pwd)
py="$project/SetupWork/venv/bin/python"
[ -x "$py" ] || py=python3
exec "$py" -I "$here/stages/build_mac.py" "$@"
