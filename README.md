# BDSP-Recomp

Mobile ports of BDSP.

A native Unity recompilation/port of **Pokémon Brilliant Diamond v1.3.0** for iOS and Android (Unity 6, Apple Silicon host), with restored C# game logic and a platform layer for macOS, iOS and Android. It is not an emulator. Shining Pearl and other game versions are not supported by the setup fingerprints. It is based on [uDPR](https://github.com/Manurocker95/uDPR) (via the [shatter9652/uDPR](https://github.com/shatter9652/uDPR) fork); see [Credits](#credits).

**You must own the game and dump your own copy with the v1.3.0 update applied.** Game data, Nintendo keys, Wwise integrations and mobile packages are not supplied. Builds contain game assets and must stay private. This repository is source only: no APK, IPA or other build is published.

This is experimental port work. See [clean setup and validation status](docs/CLEAN_SETUP.md) for requirements and remaining blockers. **Validation status:** on 2026-10-26 one clean source snapshot (a reviewed allowlisted copy, not a git clone) went through all 38 launcher stages with the offline Wwise 2019.2.15.2215 package and produced an **unsigned APK** and an **unsigned IPA**. That run was resumed with `run --from` after fixes for failures it found (those fixes are in this source); it was not a single uninterrupted pass and has not been repeated. Nothing was installed, signed or run on a device, so audio, graphics, gameplay and saves are unverified on the resulting builds. Historical handoff reports describe earlier runs, not guarantees for this checkout.

> Made with AI: the original project was made largely with Claude (Anthropic), directed, tested and reviewed by a human. This branch also includes Codex-assisted release preparation.

## Requirements

- An Apple Silicon Mac, an SSD and substantial free space. Common setup checks for 60 GB on a fresh project; keeping multiple mobile exports needs considerably more. 32 GB RAM is recommended (asset export can use about 21 GB).
- **Unity 6000.6.6f1**, with **iOS Build Support** and **Android Build Support** installed beside that editor. This branch is configured for Unity 6; the Windows bootstrap and historical macOS porting document describe an older Unity 2019 workflow.
- **Xcode**, its iOS SDK and command-line tools selected with `xcode-select`. Device signing uses your own Apple account/team.
- Android SDK platform-tools (`adb`), build-tools and a platform in API 34–37; **NDK 27.2.12479018**, **CMake 3.22.1**, and **JDK 17**. The scripts prefer `~/Library/Android/sdk` and Homebrew's `openjdk@17`. Overrides: `BDSP_ANDROID_SDK`, `BDSP_ANDROID_NDK`, `BDSP_ANDROID_JDK`, `BDSP_ANDROID_TARGET_SDK`.
- Your own **DOTween 1.2.335** directory (including `DOTween.dll` and `Editor/`). Set `BDSP_DOTWEEN_SOURCE` to it; setup verifies pinned file hashes. Vendor DLLs and artwork stay local and ignored.
- Python 3.12+, the exact packages in `setup/requirements.lock` (installed into a private venv by `sh setup/launch.sh deps`), and a macOS **AssetRipper 1.3.14** executable for common setup.
- Your own Audiokinetic Launcher **offline package** of **Wwise Unity Integration 2019.2.15.2215** (SDK build 7667), passed as `--wwise-package`. The directory holds `bundle.json`, `Unity.Windows.tar.xz` (shared Common/Editor code), `Unity.Mac.tar.xz`, `UnityDocumentation.Windows.tar.xz`, `UnityDocumentation.Mac.tar.xz`, and `Unity.Android.tar.xz` / `Unity.iOS.tar.xz` for the targets you build. Every archive is checked against pinned sizes and SHA-1 values (see [CLEAN_SETUP](docs/CLEAN_SETUP.md#wwise)). No Launcher-integrated Unity project is needed. No SDK downloads or credentials are bundled.
- Your full dump, outside the repository, laid out as:

```text
<dump>/exefs/main
<dump>/romfs/Data/globalgamemanagers
<dump>/romfs/Data/StreamingAssets/AssetAssistant/Dpr.bin
<dump>/romfs/Data/...                 # the complete data tree, not just these three files
```

`setup/stages/check.py` checks the three named files against BDSP v1.3.0 fingerprints. Asset bundles, movies, textures and sound banks come from the rest of your dump.

## All-in-one Apple Silicon setup

Start in the clean source download, with Python 3.12+ installed:

```sh
sh setup/launch.sh deps
sh setup/launch.sh preflight --target both --accept-inputs \
  --dump "/path/to/your/dump-v130" \
  --unity "/path/to/6000.6.6f1/Unity.app/Contents/MacOS/Unity" \
  --unity-mono "/path/to/2019.4.41f2/Unity.app/Contents/MacOS/Unity" \
  --wwise-package "/path/to/Wwise_Unity_Integration_2019.2.15.2215" \
  --dotween "/path/to/your/DOTween" \
  --assetripper "/path/to/AssetRipper.GUI.Free"
sh setup/launch.sh plan
sh setup/launch.sh run
```

The launcher saves paths locally, checks dependencies before preparation, resumes completed stages,
and exports Android **Gradle source, then an unsigned APK** (`android.package`) plus an iOS **Xcode project,
unsigned native build** (`ios.xcode`) **and unsigned IPA** (`ios.ipa`, `Builds/ios_package_*/Dpr-unsigned.ipa`).
It never signs; an unsigned APK/IPA is a signing input, not an installable package.
It uses real Unity script compilation without requiring a previous Bee cache.
`run --until common.merge` stops after a stage; `run --from android.compile` invalidates that stage
and all following receipts. A normal repeat resumes automatically. Close Unity first.

The full 38-stage run has completed once from a clean source snapshot with the offline 2019.2.15.2215 package
(see the validation status above). It was resumed after fixes rather than run uninterrupted, and it has not been repeated.
Device install, signing, audio and gameplay were not tested.
Read [the acquisition guide and exact limitations](docs/CLEAN_SETUP.md) before downloading large tools.
The launcher refuses to take over an already prepared project. Existing users can keep using the
lower-level build scripts below; those Android scripts produce locally signed development APKs.

Installation is a separate explicit action for an artifact you have already signed yourself:

```sh
sh setup/launch.sh install --target android --allow-install --device "YOUR_SERIAL" --artifact "/path/to/signed.apk"
sh setup/launch.sh install --target ios --allow-install --device "YOUR_DEVICE_ID" --artifact "/path/to/provisioned.app"
```

Neither command signs, chooses a device, pushes data, or launches the app. No installation was
performed while developing this launcher. For local dump preparation and separately opted-in data
transfer, see [the mobile data helper](docs/CLEAN_SETUP.md#local-data-helper).

## Lower-level common setup

`setup/setup.py` remains available for individual stages and the historical Windows pipeline.
The new launcher owns its own resume receipts in `SetupWork/launcher-state.json`; do not mix
orchestrators in one workspace.

Configuration is saved in ignored `SetupWork/config.json`; `BDSP_SETUP_CONFIG` selects another config. Logs and intermediate output live in `SetupWork/`. `--list` lists stages, `--only STAGE` runs one and `--from STAGE` resumes from a named stage. Do not use `--build` for mobile: that optional common stage builds Windows.

The pipeline exports assets, normalizes GUIDs, merges the export, installs and patches Wwise, restores shaders/textures/sprites, builds atlas metadata, and verifies the project. Some common-stage messages still name Unity 2019.4. The mobile steps also need the manifest, bundle plan and shader-variant tables; their `inputs` step creates missing ones through the Mac build helpers.

The repository stores Wwise metadata/GUIDs, not the vendor implementation. `setup/stages/wwise.py` applies idempotent project patches, and both mobile build drivers reapply them even on resumed runs. Check the installed integration without recopying it:

```sh
SetupWork/venv/bin/python -I setup/stages/wwise.py --check --unity6
SetupWork/venv/bin/python -I setup/stages/wwise_ios.py --check
SetupWork/venv/bin/python -I setup/stages/wwise_android.py --check
```

Install missing platform components by rerunning the relevant platform stage without `--check` and with your Launcher package in `--source`. A changed or stubbed integration fails clearly; do not substitute SDK files from another game. The offline 2019.2.15.2215 package route was validated once from a clean source snapshot (not from a git clone of this repository, and not on a device).

For background, [docs/PORTING_MACOS.md](docs/PORTING_MACOS.md) is retained as the **historical Unity 2019 porting guide**. Its claims about lack of Apple Silicon support do not describe this Unity 6 branch.

## iOS

Close Unity windows using this project, then:

```sh
sh setup/build_ios.sh
sh setup/build_ios.sh --from player   # resume/repeat from a particular step
sh setup/build_ios.sh --back-to-mac   # restore StandaloneOSX as the active target
```

Steps: `toolchain`, `inputs`, `offline`, `compile`, `switch`, `check`, `textures`, `bundles`, `player`, `xcode`. `--dev` selects a development player. `--from switch` is the recovery route when the project is on the wrong target. Native DXT/BC textures are converted to ASTC copies and swapped only for bundle/player steps; the originals are restored afterwards.

Outputs:

- `Builds/ios_<time>/Xcode/Unity-iPhone.xcodeproj`: IL2CPP **Xcode export**.
- `Builds/ios_<time>/data/StreamingAssets/`: separately staged game data, including `AssetAssistant/Dpr.bin`, bundles, movies and `Audio/GeneratedSoundBanks/iOS/`.
- The `xcode` step attempts an **unsigned** generic-device build into `SetupWork/build/ios_derived/Build/Products/Release-iphoneos/Dpr.app` (`Debug-iphoneos` with `--dev`). `build_ios.sh` does not archive or export an IPA, so `build_ios OK` must not be read as “an IPA exists.” (The launcher's separate `ios.ipa` stage does zip an unsigned IPA.)

### Sign and install your own build

Open the exported Xcode project, choose the `Unity-iPhone` target and your **Personal Team or paid developer team** under Signing & Capabilities, and use a unique bundle identifier. `BDSP_IOS_BUNDLE_ID` and `BDSP_IOS_TEAM_ID` can preselect those values for the export. Enable Developer Mode on the iPhone, select it in Xcode and Run. Do not put account credentials, signing certificates or provisioning profiles in Git.

Free Personal Team provisioning expires after **7 days**, requiring renewed signing and installation. See [Apple's account and Personal Team guidance](https://developer.apple.com/help/account/basics/about-your-developer-account).

**AltStore Classic / SideStore:** these consume an IPA you separately package from your own device build; `build_ios.sh` has no IPA-export step, and the launcher's `ios.ipa` output is unsigned. Import your own IPA through the sideloading app and maintain its signing refresh. See [AltStore's IPA support](https://faq.altstore.io/about-us/press-kit) and [SideStore's documentation](https://docs.sidestore.io/docs/faq). These install routes have not been tested in this release pass.

### Copy game data and keep saves

The build script stages data and prints Finder copy instructions; it **does not run `devicectl` or install anything**. Copy the entire `StreamingAssets` folder to the app's Documents using Finder > iPhone > Files > Dpr. For a development app accessible to Xcode, the equivalent explicit command is:

```sh
xcrun devicectl device copy to \
  --device "<your-device-identifier>" \
  --domain-type appDataContainer \
  --domain-identifier "<your-installed-bundle-id>" \
  --source "Builds/ios_<time>/data/StreamingAssets" \
  --destination "Documents/StreamingAssets"
```

Use the bundle identifier of the installed app. This command's options were checked with local `devicectl --help`; no device transfer was run for release preparation. For settings, use the same command with your local INI as `--source` and `Documents/pc_settings.ini` as destination.

`PcPaths` first looks for `Documents/StreamingAssets/AssetAssistant/Dpr.bin`, then falls back to data embedded in the app. `BDSP_IOS_DATA=app` embeds the data instead, making every installation much larger. The external data directory is marked to avoid backups; saves are separate:

```text
Documents/nn/save/<application-id>/<user-id>/SaveData.bin
Documents/nn/save/<application-id>/<user-id>/Backup.bin
Documents/pc_settings.ini
```

Back up saves before uninstalling or changing bundle identity. An in-place update with the same identity normally retains Documents.

## Android

```sh
sh setup/build_android.sh
sh setup/build_android.sh --from player
sh setup/build_android.sh --back-to-mac
```

Steps: `toolchain`, `inputs`, `offline`, `compile`, `switch`, `check`, `textures`, `bundles`, `player`. `--dev` makes a development APK. State is recorded in `SetupWork/build/steps_android6.json`. The player is IL2CPP **ARM64**, minimum API 26, with Vulkan first and **OpenGLES3 (ES 3.1 + AEP)** fallback. Texture conversion uses the same ASTC cache as iOS; `BDSP_ANDROID_TEXTURES=native` is an experimental opt-out.

Outputs are `Builds/android_<time>/Dpr.apk` and adjacent `data/StreamingAssets/` (roughly 6 GB). The large data tree is kept outside the APK. The build creates a local debug keystore under ignored `SetupWork/build/android/`; preserve it for compatible reinstall signatures. This is a development/sideload workflow, not a Play Store release pipeline.

Enable USB debugging and authorize your Mac. Select your device explicitly:

```sh
adb devices
export ANDROID_SERIAL="<your-device-serial>"
sh setup/deploy_android.sh "Builds/android_<time>/Dpr.apk"
```

That command **installs, pushes data and launches**. For separate actions:

```sh
sh setup/deploy_android.sh install "Builds/android_<time>/Dpr.apk"
sh setup/deploy_android.sh push "Builds/android_<time>/Dpr.apk"
sh setup/deploy_android.sh launch
sh setup/deploy_android.sh ini /path/to/pc_settings.ini
```

The package is `com.opendpr.bdsp`; the helper pins every adb command to `ANDROID_SERIAL`. `ADB` can select an alternate adb executable. Data goes to:

```text
/sdcard/Android/data/com.opendpr.bdsp/files/
  StreamingAssets/AssetAssistant/Dpr.bin
  StreamingAssets/AssetAssistant/...bundles...
  StreamingAssets/Movie/
  StreamingAssets/Audio/GeneratedSoundBanks/Android/Init.bnk
  nn/save/<application-id>/<user-id>/SaveData.bin
  nn/save/<application-id>/<user-id>/Backup.bin
  pc_settings.ini
```

Push uses `adb push --sync`, then makes the data readable by the app. Uninstalling the app deletes this external app directory, including saves. Without an APK argument the helper chooses the newest `Builds/android_*/Dpr.apk`; use an explicit path when keeping golden builds.

## Controls and frame rate

Both mobile players have an on-screen touch controller and native text-entry paths. Bluetooth gamepads are **untested in this release pass**; desktop controller results do not establish mobile compatibility. The default face-button mapping follows Switch positions.

`fps=0` means uncapped **within the mobile display pacing**: Android requests the display's highest supported refresh rate (at least 60), while iOS defaults to 60 and a positive cap can request a higher supported ProMotion rate. Neither means unlimited physical display refresh. `fps=30`, `fps=60` etc. request a cap. Game logic uses a 30 Hz reference independent of rendered frames. Mobile compositors pace presentation; the desktop V-Sync setting does not turn that off.

## Settings (`pc_settings.ini`)

Place this file next to the `nn/` directory above, then restart the app. Lines are `key=value`; `#` starts a comment. These are actual settings in `PcFrameClock`, `PcIosRender` and the PC input layer. The `ios_*` render keys apply to **both iOS and Android**.

| Key | Default / meaning |
|---|---|
| `fps` | `0`: mobile default described above; positive integer requests that cap |
| `vsync` | Desktop setting; mobile code uses platform pacing regardless |
| `ios_dof_skip` | `1`: bypass the incompatible depth-of-field chain; `0` reenables it |
| `ios_main_color` | Enabled unless `0`: redirects the world camera to its compositor color buffer; cameras with their own target texture retain it |
| `ios_depth` | `copy`; alternatives `bind` or `builtin` for opaque depth |
| `ios_cam_depth` | `1`; `0` disables the added camera-depth request |
| `ios_flip` | iOS/Vulkan default on, GLES follows `graphicsUVStartsAtTop`; `0` disables the correction (other values retain the API default) |
| `ios_shadow_cascades` | `2`; accepts `1`, `2`, `4`; `0` leaves the quality tier's cascades |
| `ios_blur_capture` | `1`; `0` disables the menu backdrop capture |
| `ios_keep_post` | `1`; `0` removes the Bloom/fog carry-over from the skipped DoF chain |
| `ios_fog_depth_copy` | `1`; `0` samples the live depth attachment |
| `ios_fog` | `1`; `0` disables fog keywords |
| `ios_fog_depth` | `1`; `2` overrides with camera depth; `0`/`1` leave the current global depth binding |
| `ios_bloom_copy` | Off; `1` substitutes a plain copy for Bloom Finalize for diagnosis |
| `key.*`, `pad.*` | Input bindings saved by the in-game keyboard/controller options |

There is **no generic `shadows` or `shadow_tier` INI setting**. The build scripts enable cascaded shadow maps on all mobile graphics tiers; `ios_shadow_cascades` is the runtime control.

## Known issues and troubleshooting

- Unsupported WebM movies (notably on iOS) are treated as finished when VideoPlayer reports a decode error, preventing a wait from hanging. Playback is attempted; this is not an unconditional Android movie skip.
- First-use shader compilation can hitch. End-to-end gameplay, thermal behavior and long sessions were not retested here.
- iOS Wwise uses its default SoloAmbient session; check the physical silent switch / silent mode if audio is absent.
- White screen: remove `ios_main_color=0` (or set `ios_main_color=1`) and restart. Keep `ios_dof_skip=1`. `ios_bloom_copy=1` is a diagnostic toggle, not a universal fix.
- No audio: verify `StreamingAssets/Audio/GeneratedSoundBanks/iOS/Init.bnk` or `.../Android/Init.bnk` under the selected data root, then run the platform Wwise `--check`. Android needs both the external base-path patch and `AddBasePath` initialization patch. A stale external manifest on iOS takes precedence over embedded data.
- Android CMake missing: install **CMake 3.22.1** in the configured SDK (`sdkmanager "cmake;3.22.1"`), then resume the failed step. Confirm the NDK and JDK overrides match the installed tools.
- Wrong active target: `sh setup/build_ios.sh --from switch` or the Android equivalent; `--back-to-mac` restores StandaloneOSX. Close an open Unity instance first.
- Missing optional icon: harmless warning; the existing/default icon is kept. An invalid supplied image can still fail import.
- Offline compile reports no recorded player response file: the legacy helper reuses Unity Bee artifacts. The clean launcher omits that optional check and calls the actual Unity compile stage after preparation. The launcher path passed real Unity compile stages in the one clean-snapshot run described above.

Logs: `SetupWork/logs/`; build reports: `SetupWork/build/reports/`; offline C# logs: `SetupWork/build/{ios,android}_compile_offline/`. Review logs for private signing/device details before sharing them.

## Optional app icon

Drop **your own 1024×1024 PNG** at `Assets/_Ours/Icon/boxart_1024.png`. The filename is retained for compatibility; it does not require box art. Use artwork you have permission to use. This folder, generated icon textures, `handoff/icon/` and the local reference image are ignored and must stay out of publication. Without the PNG, builds warn and keep the default icon.

## Contributing and repository contents

See [CONTRIBUTING.md](CONTRIBUTING.md). Runtime adaptation lives in `Assets/Scripts/_Ours/PcPlatform`, build tooling in `Assets/Editor/BdspWinBuild`, and setup stages in `setup/`. The restored game code and recovered shader source are separate from the game assets exported from your dump. Keep personal data, builds, signing material and vendor SDKs out of commits. Handoff notes are historical engineering context; staged code copies and local scratch directories are ignored.

## Credits

- [uDPR](https://github.com/Manurocker95/uDPR) (MIT, copyright pkdec) - the upstream project this port is built on (restored game C#, setup pipeline and PC platform layer); this repository was prepared from the [shatter9652/uDPR](https://github.com/shatter9652/uDPR) fork. Its MIT copyright notice is kept in [LICENSE](LICENSE). The upstream development history is not included here.
- [AssetRipper](https://github.com/AssetRipper/AssetRipper) - exports the game's assets from your dump (supplied by you for the macOS launcher).
- [UnityPy](https://github.com/K0lb3/UnityPy), [texture2ddecoder](https://github.com/K0lb3/texture2ddecoder), [Pillow](https://python-pillow.org/), [NumPy](https://numpy.org/) - read the shipped bundles and textures during the setup.
- [crunch](https://github.com/Unity-Technologies/crunch) (Unity Technologies' fork) - decodes the shipped crunched textures.
- [DOTween](http://dotween.demigiant.com/) by Demigiant - the tweening library the game ships with.
- [Audiokinetic Wwise](https://www.audiokinetic.com/) - the game's sound engine.
- [Il2CppDumper](https://github.com/Perfare/Il2CppDumper) and [Ghidra](https://ghidra-sre.org/) - used to read the game's metadata and executable while making this project (not included).
- [OpenDPR](https://github.com/TeamLumi/opendpr) by Team Luminescent - prior art for rebuilding the game as a Unity project from your own dump.

## Legal

uDPR is a fan project. It is not affiliated with, endorsed or sponsored by Nintendo, The Pokémon Company, GAME FREAK inc., Creatures
Inc. or ILCA. Pokémon and all related names, characters and game content are trademarks and copyrights of their respective owners.
Game asset files (textures, meshes, audio, movies, bundles) are excluded. The original development history is not included.
This repository **does** contain material recovered or derived from the game, which the MIT License does not cover and for which no
rights are claimed or cleared: C# restored from the game's executable (`Assets/Scripts`), shader source translated from the game's
compiled shader programs (`setup/data/shaders/`), and game-derived index tables (`setup/data/*.tsv*`, `Assets/Editor/EditorAssetAssistant/bundle_map.tsv`, `Assets/Scripts/Game/AK/EVENTS.cs`).
See [THIRD_PARTY.md](THIRD_PARTY.md).

## License

This project's own work is released under the [MIT License](LICENSE). Third-party components keep their own licenses - see
[THIRD_PARTY.md](THIRD_PARTY.md).
