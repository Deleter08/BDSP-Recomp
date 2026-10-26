#!/bin/sh
# Install only to the explicitly selected device: set ANDROID_SERIAL from adb devices.
#   sh setup/deploy_android.sh [APK]          install the APK (default: newest Builds/android_*/Dpr.apk), push its data/, launch
#   sh setup/deploy_android.sh install [APK]  install only            sh setup/deploy_android.sh push [APK]   push the data only
#   sh setup/deploy_android.sh launch         (re)start the game      sh setup/deploy_android.sh stop         force-stop it
#   sh setup/deploy_android.sh ini FILE       push FILE as the app's pc_settings.ini (ios_* render toggles, fps=, vsync= - one ini serves
#                                             Android and iOS; see Assets/Scripts/_Ours/PcPlatform/PcIosRender.cs); `ini -` removes it
#   sh setup/deploy_android.sh shot NAME      screenshot -> SetupWork/play/android/NAME.png (the phone's screen, nothing else)
#   sh setup/deploy_android.sh log            the game's logcat since launch -> SetupWork/play/android/logcat.txt + a summary
#   sh setup/deploy_android.sh tap X Y [MS]   hold a touch at device pixel X,Y for MS ms (default 250) - the layout's coordinates
#                                             are in the log: "hid: on-screen controller layout ... adb `input tap` x,y: A=(x,y) ..."
# The data (Android bundles + Dpr.bin + movies + the Wwise banks, ~5.7 GB) goes to the app's external files dir, where PcPaths looks for it:
#   /sdcard/Android/data/com.opendpr.bdsp/files/StreamingAssets/{AssetAssistant,Movie,Audio/GeneratedSoundBanks/Android}
# (the Wwise engine reads the banks from there: AkBasePathGetter -> PcPaths.StreamingAssets, patched by setup/stages/wwise_android.py)
# `adb push --sync` sends only files that changed, so a new APK with the same bundles pushes almost nothing.
# CAUTION: uninstalling the app (or a signature change forcing it) deletes that folder - the pushed data AND the saves.
set -e
here=$(cd "$(dirname "$0")" && pwd)
project=$(cd "$here/.." && pwd)
SERIAL=${ANDROID_SERIAL:?Set ANDROID_SERIAL to your device from adb devices}
PKG=com.opendpr.bdsp
REMOTE=/sdcard/Android/data/$PKG/files
OUT="$project/SetupWork/play/android"
ADB=${ADB:-adb}
command -v "$ADB" >/dev/null 2>&1 || ADB="$HOME/Library/Android/sdk/platform-tools/adb"
adb_() { "$ADB" -s "$SERIAL" "$@"; }

state=$("$ADB" -s "$SERIAL" get-state 2>/dev/null || true)
[ "$state" = "device" ] || { echo "device $SERIAL is not connected / authorized (adb devices: $("$ADB" devices | tr '\n' ' '))"; exit 1; }

apk_arg() {
    if [ -n "$1" ]; then echo "$1"; else ls -t "$project"/Builds/android_*/Dpr.apk 2>/dev/null | head -1; fi
}

do_install() {
    apk=$(apk_arg "$1")
    [ -f "$apk" ] || { echo "no APK (build one: sh setup/build_android.sh)"; exit 1; }
    echo "install: $apk ($(du -h "$apk" | cut -f1))"
    adb_ install -r "$apk"
}

do_push() {
    apk=$(apk_arg "$1")
    data="$(dirname "$apk")/data/StreamingAssets"
    [ -d "$data/AssetAssistant" ] || { echo "no data next to $apk ($data)"; exit 1; }
    adb_ shell mkdir -p "$REMOTE"
    echo "push: $data -> $REMOTE/StreamingAssets ($(du -sh "$data" | cut -f1); --sync: changed files only)"
    adb_ push --sync "$data" "$REMOTE/"
    # adb creates the directories as shell:ext_data_rw mode 2770, and the app is NOT in ext_data_rw (its groups: 3003 9997 2xxxx 5xxxx),
    # so it cannot enter them: Dpr.bin unreadable -> empty manifest -> NullReferenceException in AssetManager.LoadAssetBundle -> black
    # screen (observed 2026-10-21). World-readable fixes it; re-run after every push (new directories come back 2770).
    adb_ shell chmod -R a+rX "$REMOTE/StreamingAssets"
    adb_ shell ls "$REMOTE/StreamingAssets/AssetAssistant/Dpr.bin" >/dev/null && echo "push: Dpr.bin on the phone"
    adb_ shell ls "$REMOTE/StreamingAssets/Audio/GeneratedSoundBanks/Android/Init.bnk" >/dev/null 2>&1 \
        && echo "push: Wwise banks on the phone" || echo "push: WARNING no Audio/GeneratedSoundBanks/Android/Init.bnk on the phone - the game would be silent"
}

do_ini() {
    if [ "$1" = "-" ]; then
        adb_ shell rm -f "$REMOTE/pc_settings.ini" && echo "ini: removed"
    else
        [ -f "$1" ] || { echo "ini: no file $1"; exit 1; }
        adb_ shell mkdir -p "$REMOTE"
        adb_ push "$1" "$REMOTE/pc_settings.ini" && adb_ shell chmod a+r "$REMOTE/pc_settings.ini" && echo "ini: pushed $1 -> $REMOTE/pc_settings.ini (restart the game)"
    fi
}

do_launch() {
    adb_ shell am force-stop $PKG
    adb_ logcat -c
    adb_ shell input keyevent KEYCODE_WAKEUP
    adb_ shell am start -n $PKG/com.unity3d.player.UnityPlayerActivity
}

case "$1" in
    install) do_install "$2" ;;
    push) do_push "$2" ;;
    launch) do_launch ;;
    stop) adb_ shell am force-stop $PKG ;;
    ini) do_ini "${2:--}" ;;
    shot)
        mkdir -p "$OUT"
        f="$OUT/${2:-shot_$(date +%H%M%S)}.png"
        adb_ exec-out screencap -p > "$f"
        echo "shot: $f" ;;
    log)
        mkdir -p "$OUT"
        adb_ logcat -d -v time > "$OUT/logcat.txt"
        pid=$(adb_ shell pidof $PKG | tr -d '\r')
        echo "log: $OUT/logcat.txt (game pid ${pid:-not running})"
        grep -E " (Unity|CRASH|DEBUG|AndroidRuntime)" "$OUT/logcat.txt" > "$OUT/logcat_unity.txt" || true
        echo "  exceptions: $(grep -cE "Exception" "$OUT/logcat_unity.txt" || true)   errors (E Unity): $(grep -c " E/Unity" "$OUT/logcat_unity.txt" || true)   native crash lines: $(grep -cE "Fatal signal|backtrace:" "$OUT/logcat.txt" || true)"
        grep -E "\[PcPlatform\]|\[PC\]|hid: |swkbd|frame pacing|frame rate|PcIosRender|Wwise|AK_|PcBalloonCheck" "$OUT/logcat_unity.txt" | tail -25 || true ;;
    tap)
        adb_ shell input swipe "$2" "$3" "$2" "$3" "${4:-250}" ;;
    *)
        do_install "$1"
        do_push "$1"
        do_launch ;;
esac
