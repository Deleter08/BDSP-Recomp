#!/bin/sh
# Smoke-test a built macOS player: launches it WINDOWED (1280x720) for N seconds with its log in SetupWork/logs, quits it, and
# reports what the log says about startup, Wwise audio and exceptions. It does not press anything - it proves the .app boots,
# stays up, loads its bundles and initializes audio; playing it (and hearing it) is a human test.
#   sh setup/verify_mac_app.sh [<path to Dpr.app>] [seconds, default 90] [<input script>]
# An input script (PcInput syntax, e.g. setup/data/mac_app_drive.txt) drives the game instead of the keyboard / pad; its `shot`
# captures (the game's own frame only) land in SetupWork/play/mac_app/ (the script is copied there and run from there).
here=$(cd "$(dirname "$0")" && pwd)
project=$(cd "$here/.." && pwd)
app="$1"
[ -n "$app" ] || app=$(ls -dt "$project"/Builds/mac_*/Dpr.app 2>/dev/null | head -1)
secs="${2:-90}"
script="$3"
[ -x "$app/Contents/MacOS/Dpr" ] || { echo "no player at '$app'"; exit 2; }
mkdir -p "$project/SetupWork/logs"
log="$project/SetupWork/logs/mac_app_$(date +%Y%m%d_%H%M%S).log"
echo "app: $app"
echo "log: $log  (${secs}s)"
if [ -n "$script" ]; then
    mkdir -p "$project/SetupWork/play/mac_app"
    cp "$script" "$project/SetupWork/play/mac_app/input.txt"
    script="$project/SetupWork/play/mac_app/input.txt"
    echo "input script: $script"
    "$app/Contents/MacOS/Dpr" -logFile "$log" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -bdspInput "$script" &
else
    "$app/Contents/MacOS/Dpr" -logFile "$log" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 &
fi
pid=$!
i=0
while [ $i -lt "$secs" ] && kill -0 $pid 2>/dev/null; do sleep 1; i=$((i+1)); done
if kill -0 $pid 2>/dev/null; then
    echo "still running after ${secs}s - quitting it"; alive=1
    # a normal Quit (Apple Event) lets the player flush its log; signals are the fallback
    osascript -e 'tell application id "com.DefaultCompany.Dpr" to quit' >/dev/null 2>&1
    i=0; while [ $i -lt 15 ] && kill -0 $pid 2>/dev/null; do sleep 1; i=$((i+1)); done
    kill -0 $pid 2>/dev/null && { kill -TERM $pid; sleep 5; }
    kill -0 $pid 2>/dev/null && kill -KILL $pid
else
    wait $pid; echo "EXITED by itself after ${i}s (exit $?)"; alive=0
fi
c() { grep -c "$1" "$log" 2>/dev/null || true; }
echo "---- summary"
echo "Wwise SDK line:           $(grep -m1 'Wwise(R) SDK Version' "$log")"
echo "sound engine initialized: $(c 'Sound engine initialized successfully')"
echo "Bank Load Failed:         $(c 'Bank Load Failed')"
echo "Cannot open file:         $(c 'Cannot open file')"
echo "DllNotFoundException:     $(c 'DllNotFoundException')"
echo "EntryPointNotFound:       $(c 'EntryPointNotFoundException')"
echo "exceptions (any):         $(c 'Exception')"
echo "errors logged ([Error] / Error:): $(grep -cE '^\[?Error|Error:' "$log" 2>/dev/null || true)"
echo "PcGamepad / GameController: $(grep -m1 -i 'PcMacPad\|gamecontroller\|PcGamepad' "$log")"
echo "---- first 15 distinct error / exception lines"
grep -E 'Exception|Error|Failed|Cannot open' "$log" | sort | uniq -c | sort -rn | head -15
[ "$alive" = 1 ]
