# Clean Apple Silicon setup

Entry point: `sh setup/launch.sh`. With no action it runs preflight; `--help` lists options.
Use a fresh source directory. Do not publish your prepared project: setup adds private game data
and vendor dependencies. The source snapshot contains no git history, game art (textures, meshes,
audio, movies, bundles), SDK binaries, signing material, build products or internal handoff files.
It does contain recovered game C#, shader source translated from the game's compiled shaders and
game-derived index tables; see [THIRD_PARTY.md](../THIRD_PARTY.md). Credits and source licences remain.

## Acquisition and exact checks

The launcher downloads only Python packages, through the explicit `deps` action. It does not
fetch game content, Unity, Wwise, Android SDKs, Xcode or DOTween. Sign in and accept licences in
the vendors' tools yourself. `--accept-inputs` confirms your dump is legally obtained and your
Unity/Wwise inputs are licensed; it does not activate a licence or store credentials.

| Input | Check and official acquisition route |
|---|---|
| Apple Silicon macOS | Native arm64 Python; running Unity editors cause a refusal. Python 3.12+ from [python.org](https://www.python.org/downloads/macos/). |
| Python packages | Exact versions in `setup/requirements.lock`; `sh setup/launch.sh deps` creates a private venv and installs through [PyPI](https://pypi.org/). These are version pins, not wheel hash/signature verification. |
| Unity | Info.plist must report **6000.6.6f1**. Install the selected mobile Build Support modules in Hub using the [Unity archive](https://unity.com/releases/editor/archive). Activate your licence in Hub. Licence usability is ultimately checked by a real editor launch, not guessed from a licence filename. |
| Manifest helper runtime | **Unity 2019.4.41f2** via `--unity-mono`; only its bundled Mono compiler/runtime is used, under Rosetta. Obtain from the same Unity archive. See [Apple's Rosetta guidance](https://support.apple.com/en-us/102527). No second editor is launched. |
| Xcode | `xcrun` must locate clang++; iOS also requires the selected iphoneos SDK. Install via [Apple](https://developer.apple.com/xcode/), complete first-launch/licence setup yourself. Version is detected by the existing iOS stage; Xcode is not pinned to one version by this project. |
| Android tools | SDK **API 34**, build-tools **34.0.0**, NDK **27.2.12479018**, CMake **3.22.1**, JDK **17**. Install in [Android Studio SDK Manager](https://developer.android.com/studio). Set `--android-sdk`, `--android-ndk`, `--android-jdk` if needed. Default SDK is `~/Library/Android/sdk`; default JDK is Homebrew openjdk@17's macOS bundle. |
| AssetRipper | User-supplied macOS Free **1.3.14** executable from its [official release](https://github.com/AssetRipper/AssetRipper/releases/tag/1.3.14). SHA-256 `8e0148f64a183c0e5d85bd1b42caed9136fb70db7e56819acff41258c5b13869`. This is the observed local executable, corroborated by its 1.3.14.0 startup log, not a vendor-published signature. Different platform/repacked executables stop and need a reviewed pin update. |
| DOTween | **1.2.335**, own directory via `--dotween`; six exact SHA-256 pins in `setup/stages/dotween.py`. Use [Demigiant's downloads](https://dotween.demigiant.com/download.php) and respect its licence. No fallback to a newer version. |
| Wwise | `--wwise-package`: the Audiokinetic Launcher **offline package** directory of Wwise Unity Integration **2019.2.15.2215** (SDK build 7667): `bundle.json` plus `Unity.Windows.tar.xz` (shared Common/Components/Editor code), `Unity.Mac.tar.xz`, `UnityDocumentation.Windows.tar.xz` / `UnityDocumentation.Mac.tar.xz` (help files whose metas the project tracks), and `Unity.Android.tar.xz` / `Unity.iOS.tar.xz` for the selected targets. Pinned SHA-1: Windows `9ec6cc62…8326`, Mac `7dd34c95…c4`, Android `8b37f847…ebbe`, iOS `83b5b85f…2fb` (full values in `setup/stages/wwise_offline.py`). Obtain it with your own Audiokinetic account from the [Launcher](https://www.audiokinetic.com/en/downloads/). Archive fingerprints identify inputs; they are not credentials. `--wwise-ios` / `--wwise-android` still override the mobile archives; `--wwise` still accepts a Launcher-integrated project. |
| Game | Your complete Brilliant Diamond **v1.3.0** dump, external to the source tree. Three file sizes/SHA-1 fingerprints in `setup/stages/check.py` gate setup. No acquisition, decryption or key bypass is provided. |

Use the example in [README](../README.md). Paths are recorded in ignored, permission-restricted
`SetupWork/launcher.json`. No account passwords, tokens, licence files or signing identities are
accepted as CLI options. Dependency logs may contain local paths; inspect before sharing.

## Wwise

The offline package is consumed directly; no Launcher project integration step is needed:

```sh
python3 -I setup/stages/wwise_offline.py verify --package "/your/Wwise_Unity_Integration_2019.2.15.2215" --target both
python3 -I setup/stages/wwise.py --validate-source --unity6 --package "/your/Wwise_Unity_Integration_2019.2.15.2215"
```

- `bundle.json` must name `unityintegration.2019_2_15_2215`, version 2019.2.15 build 2215, SDK 7667; each needed archive's
  size and SHA-1 must match both that manifest and the pins in `wwise_offline.py` (an edited manifest cannot widen them).
  Missing, truncated or altered archives are reported together before anything is extracted.
- Extraction accepts regular files and directories only (no absolute paths, `..`, links, devices) and writes into a new
  private directory under `SetupWork/wwise-offline/`, renamed into place with a receipt only when complete. A matching
  receipt is reused; an unreceipted leftover is refused, not deleted.
- The vendor `NewVersion.txt` is installed as `Assets/Wwise/Version.txt` byte for byte; the version label is the vendor's.
- Patches are per source family: 2019.2.15 gets its own count-checked anchors (Android external-bank `AddBasePath` on
  `akBasePathGetterInstance`, the Unity 6000.6 `EntityId` replacements, no Lumin rule). The 2019.2.4 rules used by existing
  installations are unchanged.
- 2019.2.15 moved per-platform code out of Common. The six Mac/Windows platform scripts it adds are installed with
  deterministic `.meta` GUIDs; existing tracked GUIDs are never rewritten. Mobile stages install each archive's own
  `Generated/<platform>` bindings plus `Handwritten/<platform>` `AkSoundEngine.cs` / base-path getter and the editor
  plug-in activator. `Ak<platform>Settings.cs` is deliberately not installed, so builds keep using the project's global
  initialization settings. Every binding entry point is checked against the engine's exports and every shared managed call
  must resolve.
- Windows DSP plug-in DLLs (from a Windows SDK install, not the Unity archives) and an old local stub backup are no longer
  required inputs; a Windows player build would need the former.
- All three Mac engines must contain arm64 (the 2019.2.15 Mac archive's do). Do not substitute stubs or relabel versions.

Status: unit-tested (`setup/tests/test_wwise_offline.py`) and exercised once end to end (2026-10-26): a clean source snapshot
plus this offline package went through every launcher stage on Unity 6000.6.6f1 to an unsigned APK and an unsigned IPA. The run
was resumed with `run --from` after fixing the failures it found (the fixes are in this source); it was not one uninterrupted
pass and has not been repeated. That is a build result only: audio playback was not tested on a device.

## Execution, interruption and output

`plan` is read-only. `deps` is repeatable and installs only into `SetupWork/launcher-venv`.
`preflight` saves supplied paths and reports all detected blockers without exporting game data.
`run` repeats preflight, then:

1. Compiles source-backed crunch and Mac input helpers locally; no `codesign` command.
2. Uses the existing common setup stages for export, repair, GUID mapping, merge, vendor install,
   shader/texture/sprite work, atlas import and project audits. A conflict-checked copy makes the
   Mac bank source consumed by both mobile builders, preserving Windows originals.
3. Runs each selected mobile driver one stage at a time. Actual Unity script compilation replaces
   the optional historical Bee-based offline check. Android runs before iOS when `--target both`.
4. Android exports an IL2CPP **Gradle project** under `Builds/android_*/Gradle`, with adjacent game
   data. `android.package` copies that exact receipted export into a new `Builds/android_package_*`
   directory, removes signing configuration, disables signing/device tasks, and runs
   `:launcher:assembleRelease`. Success requires an unsigned ARM64 APK containing Wwise,
   IL2CPP and Unity, with valid ZIP and manifest. Its private Gradle home may fetch public
   Gradle/Maven dependencies; no game content is downloaded. iOS exports Xcode and uses the existing
   `CODE_SIGNING_ALLOWED=NO` / `CODE_SIGNING_REQUIRED=NO` native build of that receipted export; `ios.ipa` then zips the
   receipted app as `Payload/` into `Builds/ios_package_*/Dpr-unsigned.ipa` after checking arm64, absence of any
   signature/provisioning and the Wwise link. An unsigned IPA is a signing input, not an installable package.

Receipts are atomic in `SetupWork/launcher-state.json`; failures have no success receipt. Repeat
`run` to retry the first unfinished stage. Logs are in `SetupWork/launcher-logs/` and existing stage
log directories. `--until STAGE` stops after a named stage. `--from STAGE` clears that and later
launcher receipts; use it for rebuilds or source edits. Never rewind destructive export/merge
operations on populated assets; use a new source workspace to change dump/dependency paths.
Target and dependency path changes after preparation are refused to prevent stale reuse.
Dependency bytes are rechecked during preflight; common Wwise and DOTween source digests also bind the resume receipts. Build-tools 34.0.0 availability is checked; Unity may select another installed build-tools version in the exported Gradle configuration, which still needs clean-build verification. Changed source code requires an explicit
`--from` at the earliest affected stage; this launcher does not infer asset dependency graphs.

A per-user global lock and a workspace lock prevent simultaneous launchers. Process inspection refuses any running Unity
editor before preparation and each stage; existing texture-swap recovery also checks for Unity.
Do not start Unity manually during a run. An interrupted child may keep running; the next run
will refuse while Unity is alive. Common-stage internal recovery remains owned by those stages.

Installation requires a separate `install --allow-install --target ... --device ... --artifact ...`
command. It accepts a separately signed APK or provisioned `.app`; it never signs, chooses the
newest build, copies data or launches the game. OS tooling validates the supplied artifact. Keep
your device identifier private. External game-data placement is described in README.

### Local data helper

Raw Switch bundles need conversion by Unity. This helper is not a replacement for common
preparation and mobile bundle building. After your own selected mobile player build:

```sh
python3 -I setup/mobile_data.py prepare --target android --accept-inputs \
  --dump /your/BD-v1.3.0-dump --built-data /your/android-build/data \
  --output /your/NEW-private-data
python3 -I setup/mobile_data.py verify --target android --data /your/NEW-private-data
```

It validates the dump fingerprints, takes converted AssetAssistant data from your local build,
copies movies and Switch banks from your dump into the existing mobile layout, and hashes every
copy. Existing output directories and symlinks are refused. The platform BUILD marker is local
provenance context, not proof of authenticity or runtime compatibility. Bank copying matches
the existing builders; device audio still requires testing.

Only when you choose to access a device, separately:

```sh
python3 -I setup/mobile_data.py install --target android --allow-install \
  --device YOUR_ID --artifact /your/signed.apk
python3 -I setup/mobile_data.py push --target android --allow-install \
  --device YOUR_ID --data /your/NEW-private-data
```

Push can replace files inside StreamingAssets only; it does not delete data, touch saves, sign,
launch, or choose a device. Keep the same Android signing key for updates. For iOS, prepare
with `--target ios` and your iOS build's data; copy `StreamingAssets` into Dpr's Documents through
Finder > device > Files > Dpr. The helper can install an already provisioned `.app` with explicit
opt-in; it does not claim arbitrary IPA installation.

## Validation status

The implementation tests exercise launcher failure/retry/receipt invalidation, locking, install
opt-in rejection, environment isolation and bank conflicts. Fixture passes are not game builds.
An isolated validation copy compiled the source helper binaries, installed and verified the pinned Python dependencies, compiled/executed the manifest reader (13,660 records), and generated the shipped shader-variant table. These checks used no `Library`, Bee responses or original SetupWork state. The populated original checkout also passed an editor Roslyn check; that separate cached check does not establish a clean Unity import. On 2026-10-26 one
clean snapshot ran all 38 launcher stages with the 2019.2.15 offline package (common preparation, Android unsigned APK, iOS
xcodebuild and unsigned IPA), resumed after fixes rather than uninterrupted; no device, signing or runtime success is claimed. Later testing packaged a real unsigned APK from an
isolated copy of an existing Gradle tree (121 tasks). This is existing-export packaging, not
clean-source Unity validation. The data helper prepared and hashed 17,609 real local files
without device access. Exact local evidence is recorded in the development
handoff report, deliberately excluded from the downloadable source.

## Source snapshot

`setup/release-allowlist.txt` is an explicit reviewed path list, not an automatic git export.
`python3 -I setup/release_snapshot.py NEW_DIRECTORY` creates a source-only tree with SHA-256
receipts. It refuses existing destinations, symlinks, binary payloads, excluded directories and
common secret patterns. `--verify` checks every listed file and rejects extras. Copy the pristine
snapshot to a separate validation workspace before running setup, because setup adds private data.
Pattern checks cannot establish the absence of every possible secret encoding. Review changes
to the allowlist and source before publication. No git init, commit, remote change or push occurs.
