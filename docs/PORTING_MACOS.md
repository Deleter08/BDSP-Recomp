# Porting uDPR to macOS

**Status: untested.** Nobody has built or run uDPR on a Mac yet. This guide is worked out from the project's code and Unity
2019.4's documented behaviour, so expect to find problems it does not mention - please report them, or open a pull request.

## The short version

1. Run the normal setup **on Windows** (`setup.cmd`): it produces the Unity project.
2. Add Unity's **Mac Build Support (Mono)** module. Unity 2019.4 can build a Mac game from Windows with the Mono backend.
3. Copy the game's sound banks to a `Mac` folder.
4. Build the asset bundles for `StandaloneOSX` instead of `StandaloneWindows64`.
5. Build the macOS player, with the bundles laid out next to it the same way the Windows build does it.
6. Replace the few Windows-only parts of the PC layer (gamepad, name entry, restart) - until then they are simply missing on
   macOS.

Each step is described below.

## What you need

- A finished setup on Windows (see the [README](../README.md#setup)). The setup itself only runs on Windows: it uses PowerShell,
  Windows builds of its tools (AssetRipper, Python, `crn_levels.exe`) and Windows APIs.
- Unity Hub > Installs > 2019.4.27f1 > Add modules > **Mac Build Support (Mono)**.
  - The **IL2CPP** backend for macOS can only be built on a Mac with Xcode. Mono is the simpler route and works from Windows.
- A Mac to test on.

### Apple Silicon

Unity 2019.4 builds **Intel (x86_64) Mac games only**. On Apple Silicon (M1 and later) the game runs through Rosetta 2. A native
Apple Silicon build needs a newer Unity version, which this project does not support.

## Step 1 - sound banks

The Mac Wwise plugin is already in the project: the setup integrates Wwise for Windows **and macOS** (the Audiokinetic Launcher
requires macOS), so `Assets/Wwise/Deployment/Plugins/Mac/` exists.

Wwise looks for the sound banks in a folder named after the platform (`Assets/Wwise/Deployment/API/Handwritten/Common/
AkBasePathGetter.cs`: `"Mac"` on macOS). The setup puts the game's banks in:

```
Assets/StreamingAssets/Audio/GeneratedSoundBanks/Windows
```

Copy that folder to `.../GeneratedSoundBanks/Mac`. Unity copies `StreamingAssets` into the player whole, so for a Mac build move
the `Windows` folder out of `Assets/` for the duration of the build (or the game ships both).

The banks are the Switch originals. They play in Wwise's Windows runtime; whether the Mac runtime plays them is **untested**.

## Step 2 - asset bundles

The game loads nearly all of its assets from asset bundles at runtime, and bundles are built per platform.
`Assets/Editor/BdspWinBuild/BdspWinBuild.cs` (menu **BDSP > Windows Build**) builds them for Windows only:

| Where | What to change for macOS |
|---|---|
| `BuildBundles` (`BuildPipeline.BuildAssetBundles(..., BuildTarget.StandaloneWindows64)`, main and side builds) | `BuildTarget.StandaloneOSX` |
| `BundleOut` / `ManifestOut` (`build/bundles_win64...`, `build/manifest_win64...`) | a separate output folder, e.g. `bundles_osx` |
| the active-build-target check in `CheckCore` (`active != BuildTarget.StandaloneWindows64`) | accept `StandaloneOSX` |
| `RewriteManifest` | unchanged: it rewrites the shipped manifest (`Dpr.bin`) for the bundles you just built - run it on the Mac bundles |

The bundle plan (`setup/stages/winbuild_plan.py`, written by `setup.cmd -Build`) names every bundle exactly as the shipped game does
and is not platform-specific; reuse it. The full bundle build takes about 40 minutes and 7 GB.

A clean way to do this is to give `BdspWinBuild` a build target parameter instead of copying it.

## Step 3 - the player

`BuildPlayer` in `BdspWinBuild.cs` shows everything the Windows player build does; the Mac version needs the same steps with these
differences:

| Windows build | macOS build |
|---|---|
| `target = BuildTarget.StandaloneWindows64`, `locationPathName = .../Dpr.exe` | `BuildTarget.StandaloneOSX`, `.../Dpr.app` |
| scripting backend IL2CPP (`PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, IL2CPP)`) | **Mono** when building from Windows |
| graphics API forced to Direct3D 11 | Metal (Unity's default on macOS) |
| the Windows bundles hard-linked into `Dpr_Data/StreamingAssets/AssetAssistant/` + the rewritten `Dpr.bin` | the same layout inside `Dpr.app/Contents/Resources/Data/StreamingAssets/AssetAssistant/` |

Keep the rest as the Windows build does it: the Switch bundles in `Assets/StreamingAssets/AssetAssistant` are moved aside during the
build (Unity would copy them into the player), `StartupSettings` targets StreamingAssets for the build, Wwise's pre-build bank copy
is off, and every setting is restored afterwards. The scripting define `PC_UNLOCKED_FPS` lives in the Standalone group, so the
Mac build gets it too.

The hard links (`CreateHardLink`, kernel32) are a disk-space trick for building on Windows; plain copies work too.

## Step 4 - the Windows-only parts of the PC layer

The PC layer (`Assets/Scripts/_Ours/PcPlatform`) compiles on every platform - the Windows-only parts are fenced off with
`UNITY_STANDALONE_WIN` - but on macOS they are missing:

| Part | Where | On macOS today | What a port needs |
|---|---|---|---|
| Gamepad | `PcXInput.cs` (XInput), read in `PcHid.cs` | no gamepad; the keyboard works | a gamepad path through Unity's input |
| Name entry (the software keyboard) | `PcSwkbdNative.cs` (a Win32 window), called from `PcSwkbd.cs` | no text window: the game silently accepts the default text | a text-entry dialog |
| Restart (the console's "restart the software") | `PcProcessLaunch.cs`, called from `PcProgram.cs` | the game quits instead of relaunching | a relaunch (e.g. `open -n` on the app bundle) |
| Timer resolution | `PcFrameClock.cs` (`timeBeginPeriod`, winmm) | skipped | nothing - macOS timers are fine |

Also:

- **Display modes** (`PcDisplay.cs`): the "Full Screen" option uses `FullScreenMode.ExclusiveFullScreen`, which Unity supports on
  Windows only; on macOS Unity falls back to a full-screen window. "Windowed" and "Borderless Window" map to modes macOS has.
- **Shaders**: the recovered shaders have only been tested with Direct3D 11. Unity compiles them for Metal too; anything that
  renders wrong on a Mac is most likely there.

## Step 5 - running it on the Mac

- Copy `Dpr.app` to the Mac. A Mac app built on Windows can lose the executable permission on its binary when it is copied or
  zipped; if it does not start, run `chmod +x Dpr.app/Contents/MacOS/*` in Terminal.
- The app is not signed or notarised: the first launch needs right-click > **Open** (or
  `xattr -dr com.apple.quarantine Dpr.app`).
- Saves, `pc_settings.ini` and `Player.log` live in `~/Library/Application Support/DefaultCompany/Dpr/` (Unity's
  `persistentDataPath` on macOS); the log is `~/Library/Logs/DefaultCompany/Dpr/Player.log`.

**Your build contains the game's assets - do not share it.**

## Opening the project in the Unity editor on a Mac

The project folder the setup produced can be copied to a Mac and opened with Unity 2019.4.27f1 there. Leave `Library/` behind:
Unity rebuilds it on the Mac (the first import takes a long time). Building IL2CPP Mac players needs this route plus Xcode.

## Contributing a port

A port that works deserves to be in this repository. Please keep the Windows build unchanged, put platform code behind
`UNITY_STANDALONE_OSX` the same way the Windows parts use `UNITY_STANDALONE_WIN`, and describe on which Mac (Intel / Apple Silicon,
macOS version) you tested. See [CONTRIBUTING.md](../CONTRIBUTING.md).
