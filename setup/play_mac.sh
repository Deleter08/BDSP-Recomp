#!/bin/sh
# Play BDSP in the Unity editor on macOS with the keyboard and a game pad (DualSense, DualShock 4, Xbox ...): opens the project,
# enters Play mode from level0 and stays there - no input script, no time limit. Stop with the editor's Play button; quit Unity
# when done. Close any Unity window already showing this project first (Unity opens a project once).
#   sh setup/play_mac.sh             play
#   sh setup/play_mac.sh --paddiag   play + log every pad input to SetupWork/play/pad_diag.txt (paste that file back)
# Editor log: SetupWork/logs/unity_play_interactive.log
here=$(cd "$(dirname "$0")" && pwd)
project=$(cd "$here/.." && pwd)
work="$project/SetupWork"
unity=$(/usr/bin/python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1])).get("unity",""))' "$work/config.json" 2>/dev/null)
[ -x "$unity" ] || unity="${BDSP_UNITY:-}"
if [ ! -x "$unity" ]; then
	echo "play_mac.sh: no Unity editor found. Set \"unity\" in $work/config.json (written by setup) or export BDSP_UNITY" >&2
	echo "  to the editor executable, e.g. /Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity" >&2
	exit 1
fi
method=BdspPlay.Play
[ "$1" = "--paddiag" ] && method=BdspPlay.PlayPadDiag
mkdir -p "$work/logs" "$work/play"
echo "Unity: $unity"
echo "method: $method   log: $work/logs/unity_play_interactive.log"
BDSP_SETUP_WORK="$work" exec "$unity" -projectPath "$project" -logFile "$work/logs/unity_play_interactive.log" -executeMethod "$method"
