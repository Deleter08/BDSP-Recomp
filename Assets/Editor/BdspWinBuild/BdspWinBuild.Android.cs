// BDSP Android build (2026-10-24, Unity 6000.6): the same 13,651-bundle plan as the Windows / macOS / iOS builds, for BuildTarget.Android,
// an IL2CPP ARM64 APK. Editor only. NOT shipped code. NOTHING here runs by itself: menu BDSP/Android Build/..., or setup/build_android.sh,
// which runs `-executeMethod BdspWinBuild.WinBuild.Android<Step>` in order (the editor exits with 0 = OK, 1 = failed).
// (The 2019.4 version of this file built a Mono ARMv7 APK, GLES3 only, without audio. This is the Unity 6 / iOS-parity version.)
//
// What differs from the macOS build (BuildPlayerMac) / mirrors the iOS one (BdspWinBuild.iOS.cs):
//   - Needs Unity's Android Build Support (PlaybackEngines/AndroidPlayer next to Unity.app; il2cpp + mono Variations). That module ships
//     NO SDK / NDK / JDK (Unity 6 downloads them via Hub): ApplyAndroidTools points the editor (AndroidExternalToolsSettings) at the
//     installed ones - SDK ~/Library/Android/sdk, its NDK, a JDK 17 (homebrew openjdk@17). BDSP_ANDROID_SDK / _NDK / _JDK override.
//   - IL2CPP, ARM64 only (arm64-v8a), minSdk 26, targetSdk = the newest of 37..34 whose platforms/android-N is installed (BDSP_ANDROID_TARGET_SDK
//     overrides), Vulkan first with OpenGLES3 as the fallback, managed stripping Minimal + Assets/link.xml, engine stripping off,
//     landscape, fullscreen, Activity (not GameActivity: setup/deploy_android.sh launches com.unity3d.player.UnityPlayerActivity),
//     Optimized Frame Pacing on. Package id com.opendpr.bdsp (unchanged).
//   - BuildTargetGroup.Android's scripting defines PC_UNLOCKED_FPS;BDSP_UNITY6_GATE are SAVED in ProjectSettings.asset (key 7 / "Android"):
//     the switch and every editor start while Android is active compile with the saved ones, before any step code runs
//     (without BDSP_UNITY6_GATE MeshRendererDirect compiles the removed 2019.4 BatchRendererGroup API; without PC_UNLOCKED_FPS
//     PcFrameClock is compiled out and the game runs its 30 fps logic). SwitchToAndroid and build_android.py put them back.
//   - Shadows: every Android graphics tier gets cascadedShadowMaps = true (the shaders expect the screen-space shadow mask; mobile tiers
//     default to the raw shadow map: "transparent" shadows that move with the casters) - the same block as the iOS one.
//   - Audio: the Wwise 2019.2.15 Android engine (Assets/Wwise/Deployment/Plugins/Android/arm64-v8a/Release/libAkSoundEngine.so + the
//     Generated/Android bindings, DllImport "AkSoundEngine" - setup/stages/wwise_android.py installs both; build_android.py runs it).
//     Wwise's plug-in activator enables only the folder of its current config (none set = Profile): the player step sets Release in memory
//     for the build and puts the previous value back. The banks are the macOS ones (Switch-authored, Vorbis, platform-neutral): cut out
//     of the APK like the other StreamingAssets, then APFS-cloned into <data>/Audio/GeneratedSoundBanks/Android, where AkBasePathGetter
//     looks on Android (from PcPaths.StreamingAssets - patched by wwise_android.py).
//   - Textures: the ripped DXT/BC native textures: the iOS build converts them to ASTC copies (BdspWinBuild.iOS.Textures.cs,
//     setup/stages/ios_textures.py); the Android bundle + player steps run with the same ASTC copies swapped into Assets
//     (build_android.py), because Unity 6 loading DXT/BC on a GPU without them is not proven on the Pixel (the 2019.4 player
//     decompressed them at load time). BDSP_ANDROID_TEXTURES=native skips the swap.
//   - The data does NOT go into the APK: ~5 GB of bundles + 0.5 GB of movies would need an OBB, and the game reads its Dpr.bin and bundles
//     with File / LoadFromFile, which cannot open a path inside the APK. The player build writes
//     Builds/android_<time>/data/StreamingAssets/{AssetAssistant,Movie,Audio} (APFS clones, no extra disk); setup/deploy_android.sh pushes
//     it to the app's external files dir (Application.persistentDataPath), where PcPaths.StreamingAssets points on Android. The project's
//     StreamingAssets (Switch bundles, Wwise banks, movies) is cut out of the generated Gradle project before Gradle packs it
//     (AndroidApkStreamingAssetsStrip) - the project folder itself is never moved or touched.
//   - Input: the on-screen controller (PcVirtualPad) + Android's gamepad support; frame pacing: PcFrameClock's Android branch (the display's
//     highest refresh rate when uncapped, vSyncCount 0).
// Every Android player setting is applied in memory for the step and ProjectSettings.asset / GraphicsSettings.asset / StartupSettings and
// the Wwise + plug-in .meta files are written back byte for byte after it (as the macOS / iOS steps do). The active build target (Library
// state) does change: setup/build_mac.sh needs it back on StandaloneOSX (sh setup/build_android.sh --back-to-mac).
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace BdspWinBuild
{
    public static partial class WinBuild
    {
        const string AndroidPackage = "com.opendpr.bdsp";
        const string AndroidKeyAlias = "bdspdebug";
        const string AndroidKeyPass = "bdspdebug";   // a local debug key, nothing secret
        const string AndroidDefines = "PC_UNLOCKED_FPS;BDSP_UNITY6_GATE";
        const string AndroidWwiseLib = "Assets/Wwise/Deployment/Plugins/Android/arm64-v8a/Release/libAkSoundEngine.so";
        static string Home { get { return Environment.GetEnvironmentVariable("HOME") ?? "~"; } }
        static string AndroidSdkRoot { get { string v = Environment.GetEnvironmentVariable("BDSP_ANDROID_SDK"); return string.IsNullOrEmpty(v) ? Path.Combine(Home, "Library", "Android", "sdk") : v.Trim(); } }
        static string AndroidNdkRoot
        {
            get
            {
                string v = Environment.GetEnvironmentVariable("BDSP_ANDROID_NDK");
                if (!string.IsNullOrEmpty(v))
                    return v.Trim();
                string dir = Path.Combine(AndroidSdkRoot, "ndk");
                // NDK r27 (27.2.12479018) is the one the 2019 / Unity 6 IL2CPP toolchain here was set up with; otherwise the newest installed
                string pinned = Path.Combine(dir, "27.2.12479018");
                if (Directory.Exists(pinned))
                    return pinned;
                return Directory.Exists(dir) ? Directory.GetDirectories(dir).OrderBy(x => x, StringComparer.Ordinal).LastOrDefault() ?? pinned : pinned;
            }
        }
        static string AndroidJdkRoot
        {
            get
            {
                string v = Environment.GetEnvironmentVariable("BDSP_ANDROID_JDK");
                return string.IsNullOrEmpty(v) ? "/opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home" : v.Trim();
            }
        }
        // targetSdk: the newest platform the module enum knows (37) that is installed; BDSP_ANDROID_TARGET_SDK forces one
        static int AndroidTargetSdk
        {
            get
            {
                int forced;
                if (int.TryParse((Environment.GetEnvironmentVariable("BDSP_ANDROID_TARGET_SDK") ?? "").Trim(), out forced) && forced > 0)
                    return forced;
                // exact "android-N" folders first (what Unity's SDK probing names); the newer SDK layout's "android-N.0" only when none exists
                for (int n = 37; n >= 34; n--)
                    if (Directory.Exists(Path.Combine(AndroidSdkRoot, "platforms", "android-" + n)))
                        return n;
                for (int n = 37; n >= 34; n--)
                    if (Directory.Exists(Path.Combine(AndroidSdkRoot, "platforms", "android-" + n + ".0")))
                        return n;
                return 34;
            }
        }
        static bool AndroidAstcTextures { get { return (Environment.GetEnvironmentVariable("BDSP_ANDROID_TEXTURES") ?? "").Trim() != "native"; } }
        static string AndroidKeystore { get { return Path.Combine(Work, "android", "bdsp-debug.keystore"); } }
        static string AndroidPlayerEngine { get { return Path.Combine(EditorApplication.applicationContentsPath, "..", "..", "PlaybackEngines", "AndroidPlayer"); } }

        // set by BuildPlayerAndroid for its BuildPlayer call only: AndroidApkStreamingAssetsStrip cuts the data out of the Gradle project
        internal static bool StripApkStreamingAssets;

        // ---- entry points (menu + -executeMethod; setup/build_android.sh runs them in this order) ----------------------------
        [MenuItem("BDSP/Android Build/0  Switch the active build target to Android (re-imports)", false, 0)]
        public static void AndroidSwitch() { if (Confirm("Switch the active build target to Android? Assets re-import for Android (long the first time).")) RunAndroid("switch", r => SwitchToAndroid(r)); }

        [MenuItem("BDSP/Android Build/1  Check plan + project (builds nothing)", false, 1)]
        public static void AndroidCheck() { RunAndroid("check", r => CheckCore(r, false, false)); }

        [MenuItem("BDSP/Android Build/1a Compile the player scripts for Android (builds nothing)", false, 2)]
        public static void AndroidCompileScripts() { RunAndroid("compile", r => CompileAndroidScripts(r)); }

        // the ASTC copies are the iOS ones (same files, same index): converting is generic, only the report tag differs
        [MenuItem("BDSP/Android Build/1b Convert the DXT/BC textures to ASTC (into SetupWork; shared with the iOS build)", false, 3)]
        public static void AndroidConvertTextures() { RunAndroid("textures", r => ConvertIosTextures(r, IosTexRoot, null)); }

        [MenuItem("BDSP/Android Build/2  Build AssetBundles - FULL (13,651 bundles, ~6 GB)", false, 20)]
        public static void AndroidBuildBundlesFull() { if (Confirm("Build ALL 13,651 Android bundles into Builds/bundles_android? This takes a long time and ~6 GB.")) RunAndroid("bundles_full", r => AndroidRequireAstc(r) && BuildBundles(r, false)); }

        [MenuItem("BDSP/Android Build/3  Rewrite Dpr.bin from the last FULL bundle build", false, 22)]
        public static void AndroidRewriteManifestFull() { RunAndroid("manifest_full", r => RewriteManifest(r, false)); }

        [MenuItem("BDSP/Android Build/4  Build APK - RELEASE (IL2CPP ARM64)", false, 40)]
        public static void AndroidBuildPlayerRelease() { if (Confirm("Build the Android APK (release, IL2CPP ARM64) into Builds/android_<time>/Dpr.apk?")) RunAndroid("player_release", r => AndroidRequireAstc(r) && BuildPlayerAndroid(r, false)); }

        [MenuItem("BDSP/Android Build/5  Build APK - DEVELOPMENT (IL2CPP ARM64)", false, 41)]
        public static void AndroidBuildPlayerDev() { if (Confirm("Build the Android APK (development, IL2CPP ARM64) into Builds/android_<time>_dev/Dpr.apk?")) RunAndroid("player_dev", r => AndroidRequireAstc(r) && BuildPlayerAndroid(r, true)); }

        // setup/build_mac.sh's check refuses any other active target: this puts it back (sh setup/build_android.sh --back-to-mac)
        [MenuItem("BDSP/Android Build/9  Switch the active build target back to macOS", false, 90)]
        public static void AndroidSwitchBackToMac() { if (Confirm("Switch the active build target back to macOS (StandaloneOSX)?")) RunAndroid("switch_back_mac", r => SwitchBackToMac(r)); }

        // the bundle / player steps: no DXT/BC native texture may be left in Assets unless BDSP_ANDROID_TEXTURES=native
        static bool AndroidRequireAstc(Report r)
        {
            if (!AndroidAstcTextures)
            {
                r.Warn("BDSP_ANDROID_TEXTURES=native: the DXT/BC textures stay as ripped (the player must decompress them at load)");
                return true;
            }
            return IosRequireNoDesktopTextures(r);
        }

        // ---- the editor-wide Android tool locations (the module ships none: Unity 6 fetches them through Hub) -------------------------
        static void ApplyAndroidTools(Report r)
        {
            // UnityEditor.Android.AndroidExternalToolsSettings lives in the Android module's UnityEditor.Android.Extensions.dll, which is NOT a
            // reference of the editor script assembly while another target is active (a direct use would stop every Mac / iOS build compiling):
            // reflection, with the EditorPrefs keys the Preferences > External Tools page stores as the same thing.
            EditorPrefs.SetBool("SdkUseEmbedded", false);
            EditorPrefs.SetBool("NdkUseEmbedded", false);
            EditorPrefs.SetBool("JdkUseEmbedded", false);
            EditorPrefs.SetString("AndroidSdkRoot", AndroidSdkRoot);
            EditorPrefs.SetString("AndroidNdkRoot", AndroidNdkRoot);
            EditorPrefs.SetString("JdkPath", AndroidJdkRoot);
            Type t = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType("UnityEditor.Android.AndroidExternalToolsSettings");
                if (t != null)
                    break;
            }
            if (t == null)
            {
                r.Warn("AndroidExternalToolsSettings not loaded (Android module extension assembly): the EditorPrefs were set instead");
                return;
            }
            foreach (var kv in new[] { new[] { "sdkRootPath", AndroidSdkRoot }, new[] { "ndkRootPath", AndroidNdkRoot }, new[] { "jdkRootPath", AndroidJdkRoot } })
            {
                var prop = t.GetProperty(kv[0], System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop == null || !prop.CanWrite)
                    r.Warn("AndroidExternalToolsSettings." + kv[0] + " not found / not settable");
                else
                    prop.SetValue(null, kv[1], null);
            }
            Func<string, string> get = n => { var p = t.GetProperty(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static); return p == null ? "?" : (string)p.GetValue(null, null); };
            r.Line("Android tools: SDK " + get("sdkRootPath") + ", NDK " + get("ndkRootPath") + ", JDK " + get("jdkRootPath"));
        }

        // ---- 0. switch -----------------------------------------------------------------------------------------------------------
        static bool SwitchToAndroid(Report r)
        {
            var t0 = DateTime.Now;
            ApplyAndroidTools(r);
            // ASTC: the Pixel's Mali-G78 decodes it natively. Only textures Unity imports with automatic compression follow this; the
            // ripped Texture2D .asset files keep the format they were exported with (build_android.py swaps ASTC copies in for the bundles).
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                r.Line("active build target is already Android");
                return true;
            }
            // the switch recompiles the scripts with the Android group's SAVED defines (as does every later editor start while Android is
            // active): without BDSP_UNITY6_GATE MeshRendererDirect compiles the removed 2019.4 BRG API and nothing after it can run
            if (PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android) != AndroidDefines)
            {
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, AndroidDefines);
                AssetDatabase.SaveAssets();
                r.Line("Android scripting defines set + saved: " + AndroidDefines);
            }
            r.Line("active build target " + EditorUserBuildSettings.activeBuildTarget + " -> Android (texture subtarget ASTC)");
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            r.Line(string.Format("SwitchActiveBuildTarget {0} after {1:F1} min; active target now {2}", ok ? "OK" : "FAILED",
                (DateTime.Now - t0).TotalMinutes, EditorUserBuildSettings.activeBuildTarget));
            if (!ok || EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                r.Error("the active build target did not become Android");
            return r.Errors == 0;
        }

        // ---- 1. check (the Android part of CheckCore) ------------------------------------------------------------------------------
        static bool CheckAndroid(Report r)
        {
            foreach (var need in new[] { "UnityEditor.Android.Extensions.dll", "AndroidPlayerBuildProgram.exe", "Variations/il2cpp/Release/Libs/arm64-v8a/libunity.so",
                                         "Variations/il2cpp/Development/Libs/arm64-v8a/libunity.so" })
                if (!File.Exists(Path.Combine(AndroidPlayerEngine, need)))
                    r.Error("Android player support is missing " + Path.GetFullPath(Path.Combine(AndroidPlayerEngine, need)));
            if (!File.Exists(Path.Combine(AndroidSdkRoot, "platform-tools", "adb")))
                r.Error("no Android SDK at " + AndroidSdkRoot + " (platform-tools/adb) - BDSP_ANDROID_SDK overrides");
            if (!Directory.Exists(Path.Combine(AndroidSdkRoot, "build-tools")) || Directory.GetDirectories(Path.Combine(AndroidSdkRoot, "build-tools")).Length == 0)
                r.Error("the Android SDK has no build-tools");
            if (!Directory.Exists(Path.Combine(AndroidSdkRoot, "platforms", "android-" + AndroidTargetSdk)) && !Directory.Exists(Path.Combine(AndroidSdkRoot, "platforms", "android-" + AndroidTargetSdk + ".0")))
                r.Error("the Android SDK has no platform android-" + AndroidTargetSdk + " (targetSdk) - install it or set BDSP_ANDROID_TARGET_SDK");
            if (!File.Exists(Path.Combine(AndroidNdkRoot, "source.properties")))
                r.Error("no Android NDK at " + AndroidNdkRoot + " - IL2CPP needs it (BDSP_ANDROID_NDK overrides)");
            if (!File.Exists(Path.Combine(AndroidJdkRoot, "bin", "java")))
                r.Error("no JDK 17 at " + AndroidJdkRoot + " - brew install openjdk@17 (BDSP_ANDROID_JDK overrides)");
            if (!File.Exists(Path.Combine(Project, "Assets", "link.xml")))
                r.Error("Assets/link.xml is missing - IL2CPP stripping would remove code the game reaches by name / from bundles");
            if (!File.Exists(Path.Combine(Project, AndroidWwiseLib)))
                r.Error("no " + AndroidWwiseLib + " - run: python -I setup/stages/wwise_android.py (the Android bindings call it)");
            if (!AndroidExportOnly && !File.Exists(AndroidKeystore))
                r.Warn("no debug keystore yet at " + AndroidKeystore + " (setup/build_android.py creates it before the player step)");
            r.Line(string.Format("Android player settings now: package '{0}', backend {1}, architectures {2}, min SDK {3}, target SDK {4}, graphics {5} (default {6}), defines '{7}'",
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android), PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android),
                PlayerSettings.Android.targetArchitectures, PlayerSettings.Android.minSdkVersion, PlayerSettings.Android.targetSdkVersion,
                string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).Select(x => x.ToString()).ToArray()),
                PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android), PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android)));
            r.Line("each build step uses: package " + AndroidPackage + ", IL2CPP, ARM64, min SDK 26, target SDK " + AndroidTargetSdk + ", Vulkan then OpenGLES3, " +
                   "stripping Minimal + link.xml, engine stripping off, landscape, defines " + AndroidDefines + ", textures " + (AndroidAstcTextures ? "ASTC copies" : "native (BDSP_ANDROID_TEXTURES=native)") +
                   "; ProjectSettings.asset is written back afterwards");
            r.Line("texture subtarget: " + EditorUserBuildSettings.androidBuildSubtarget);
            if (File.Exists(StateFile) && File.ReadAllText(StateFile).Contains("ACTIVE"))
                r.Warn("STATE.txt says a build step did not finish - run Recover first:\n" + File.ReadAllText(StateFile));
            return r.Errors == 0;
        }

        // ---- 1a. compile the player scripts (UNITY_ANDROID, !UNITY_EDITOR) without building -----------------------------------
        static bool CompileAndroidScripts(Report r)
        {
            string projectSettingsPath = Path.Combine(Project, "ProjectSettings", "ProjectSettings.asset");
            byte[] projectSettingsBytes = File.ReadAllBytes(projectSettingsPath);
            string outDir = Path.Combine(Work, "android_compile");
            if (Directory.Exists(outDir))
                Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);
            var logged = new System.Collections.Generic.List<string>();
            Application.LogCallback grab = (msg, stack, type) =>
            {
                if ((type == LogType.Error || type == LogType.Exception) && logged.Count < 200)
                    logged.Add(msg);
            };
            Application.logMessageReceivedThreaded += grab;
            try
            {
                ApplyAndroidPlayerSettings(r, false);
                var settings = new UnityEditor.Build.Player.ScriptCompilationSettings
                {
                    group = BuildTargetGroup.Android,
                    target = BuildTarget.Android,
                    options = UnityEditor.Build.Player.ScriptCompilationOptions.None,
                };
                var result = UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(settings, outDir);
                var dlls = result.assemblies == null ? new string[0] : result.assemblies.ToArray();
                r.Line("compiled " + dlls.Length + " player assemblies for Android -> " + outDir + ": " + string.Join(", ", dlls));
                if (!dlls.Contains("Assembly-CSharp.dll"))
                    r.Error("Assembly-CSharp.dll was not produced - the player scripts do not compile for Android");
            }
            finally
            {
                Application.logMessageReceivedThreaded -= grab;
                AssetDatabase.SaveAssets();
                if (!File.ReadAllBytes(projectSettingsPath).SequenceEqual(projectSettingsBytes))
                    File.WriteAllBytes(projectSettingsPath, projectSettingsBytes);
            }
            foreach (var e in logged)
                r.Error("compile: " + e);
            return r.Errors == 0;
        }

        // ---- the Android player settings every step uses (in memory; the caller writes ProjectSettings.asset back) --------------
        static bool AndroidExportOnly { get { return Environment.GetEnvironmentVariable("BDSP_NO_SIGN") == "1"; } }

        static void ApplyAndroidPlayerSettings(Report r, bool development)
        {
            var android = NamedBuildTarget.Android;
            ApplyAndroidTools(r);
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = AndroidExportOnly;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            PlayerSettings.SetApplicationIdentifier(android, AndroidPackage);
            // Map/character shaders sample _ShadowMapTexture as a SCREEN-SPACE shadow mask (SHADOWS_SCREEN, desktop/NVN style).
            // Mobile tiers default cascadedShadowMaps=false -> Unity skips the screen-space collect and binds the raw shadow map:
            // "transparent" shadows that move with the casters. Force cascaded/screen-space shadows on every Android tier.
            foreach (GraphicsTier t in new[] { GraphicsTier.Tier1, GraphicsTier.Tier2, GraphicsTier.Tier3 })
            {
                var ts = UnityEditor.Rendering.EditorGraphicsSettings.GetTierSettings(BuildTargetGroup.Android, t);
                ts.cascadedShadowMaps = true;
                UnityEditor.Rendering.EditorGraphicsSettings.SetTierSettings(BuildTargetGroup.Android, t, ts);
            }
            PlayerSettings.bundleVersion = "1.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(android, development ? Il2CppCompilerConfiguration.Debug : Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Minimal);
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.SetScriptingDefineSymbols(android, AndroidDefines);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)AndroidTargetSdk;
            // Vulkan first, OpenGLES3 (ES 3.1 + AEP: every project shader is `#pragma target 5.0`) as the fallback. The bundles carry the
            // shader programs of both (they are built for this API list, so rebuild them after changing it).
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.openGLRequireES31 = true;
            PlayerSettings.openGLRequireES31AEP = true;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.Android.optimizedFramePacing = true;                       // Swappy: frames are spread over the display's refresh
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;   // not GameActivity: deploy_android.sh launches UnityPlayerActivity
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.androidIsGame = true;
            PlayerSettings.Android.useAPKExpansionFiles = false;
            if (AndroidExportOnly)
            {
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.Android.keystoreName = "";
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasName = "";
                PlayerSettings.Android.keyaliasPass = "";
            }
            else if (File.Exists(AndroidKeystore))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = AndroidKeystore;
                PlayerSettings.Android.keystorePass = AndroidKeyPass;
                PlayerSettings.Android.keyaliasName = AndroidKeyAlias;
                PlayerSettings.Android.keyaliasPass = AndroidKeyPass;
            }
            r.Line("Android player settings for this step: " + AndroidPackage + ", IL2CPP " + (development ? "Debug" : "Release") + ", ARM64, SDK 26.." + AndroidTargetSdk +
                   ", Vulkan + OpenGLES3 (ES 3.1 AEP), stripping Minimal, optimized frame pacing, landscape, defines " + AndroidDefines + ", keystore " +
                   (File.Exists(AndroidKeystore) ? AndroidKeystore : "(none yet)"));
        }

        const string AndroidBanksDst = "Audio/GeneratedSoundBanks/Android";

        // the Android Wwise banks = APFS clones of the macOS ones (Switch-authored banks + Vorbis .wem: nothing platform-specific in them; the
        // macOS build plays the same files). Wwise's own pre-build copy is off for these builds (CopySoundBanksAsPreBuildStep).
        static bool CloneAndroidBanks(Report r, string dst)
        {
            string src = Path.Combine(Project, "Assets", "StreamingAssets", "Audio", "GeneratedSoundBanks", "Mac");
            if (!File.Exists(Path.Combine(src, "Init.bnk")))
            {
                r.Error("no Wwise banks at " + src + " (Init.bnk) - the Android build would be silent");
                return false;
            }
            if (Directory.Exists(dst))
                Directory.Delete(dst, true);
            int banks = 0, media = 0, copies = 0;
            foreach (var f in Directory.GetFiles(src, "*", SearchOption.TopDirectoryOnly))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".bnk" && ext != ".wem" && ext != ".xml" && ext != ".h")
                    continue;
                Link(f, Path.Combine(dst, Path.GetFileName(f)), ref copies);
                if (ext == ".bnk") banks++;
                else if (ext == ".wem") media++;
            }
            r.Line(string.Format("Wwise banks cloned into {0}: {1} .bnk + {2} .wem ({3} plain copies)", dst, banks, media, copies));
            return banks > 0;
        }

        // ---- 4/5. the APK ----------------------------------------------------------------------------------------------------------
        static bool BuildPlayerAndroid(Report r, bool development)
        {
            if (!CheckCore(r, false, true))
                return false;
            if (!AndroidExportOnly && !File.Exists(AndroidKeystore))
            {
                r.Error("no debug keystore at " + AndroidKeystore + " - setup/build_android.py creates it (JDK 17 keytool)");
                return false;
            }
            if (!File.Exists(ManifestOut(false)))
            {
                r.Error("no Android Dpr.bin (" + ManifestOut(false) + ") - build the bundles first");
                return false;
            }
            string bundleManifest = Path.Combine(BundleOut(false), Path.GetFileName(BundleOut(false)) + ".manifest");
            if (!File.Exists(bundleManifest))
            {
                r.Error("no bundle build manifest " + bundleManifest + " - build the bundles first");
                return false;
            }
            var drive = BuildDrive;
            if (drive.AvailableFreeSpace < 12 * GiB)
            {
                r.Error(string.Format("refusing: {0:F1} GiB free on {1}, the APK build needs >= 12 GiB (Unity stages the project's StreamingAssets " +
                    "into the Gradle project before it is cut out)", drive.AvailableFreeSpace / (double)GiB, drive.Name));
                return false;
            }
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string outDir = Path.Combine(BuildRoot, "android" + Flavour + "_" + stamp + (development ? "_dev" : ""));
            if (Directory.Exists(outDir))
            {
                r.Error("output exists: " + outDir);
                return false;
            }
            string apk = Path.Combine(outDir, AndroidExportOnly ? "Gradle" : "Dpr.apk");
            string dataSa = Path.Combine(outDir, "data", "StreamingAssets");

            string bdir = Path.Combine(Backups, "android_player_" + stamp);
            Directory.CreateDirectory(bdir);
            byte[] startupBytes = File.ReadAllBytes(Path.Combine(Project, StartupAsset));
            File.WriteAllBytes(Path.Combine(bdir, "StartupSettings.asset"), startupBytes);
            string projectSettingsPath = Path.Combine(Project, "ProjectSettings", "ProjectSettings.asset");
            byte[] projectSettingsBytes = File.ReadAllBytes(projectSettingsPath);
            File.WriteAllBytes(Path.Combine(bdir, "ProjectSettings.asset"), projectSettingsBytes);
            string graphicsPath = Path.Combine(Project, "ProjectSettings", "GraphicsSettings.asset");
            byte[] graphicsBytes = File.ReadAllBytes(graphicsPath);
            bool prevWwiseCopy = AkWwiseEditorSettings.Instance.CopySoundBanksAsPreBuildStep;
            var wwiseData = AkWwiseProjectInfo.GetData();
            string prevWwiseConfig = wwiseData != null ? wwiseData.CurrentPluginConfig : null;
            var metas = SnapshotPluginMetas();
            r.Line("backed up: StartupSettings + ProjectSettings -> " + bdir + "; " + metas.Count + " .meta files under Assets/Wwise + Assets/Plugins (in memory)");
            WriteState("ACTIVE android player build " + stamp + "\nstartup_backup=" + Path.Combine(bdir, "StartupSettings.asset") + "\n");

            bool ok = false;
            try
            {
                ApplyAndroidPlayerSettings(r, development);
                if (!ApplyBoxArtIcons(r, NamedBuildTarget.Android, true))          // box-art app icon: adaptive + round + legacy (in memory)
                    return false;
                AkWwiseEditorSettings.Instance.CopySoundBanksAsPreBuildStep = false;   // in memory only (never saved)
                SetStartupFields(3, null);                                             // StreamingAssets, as shipped (PcPaths moves it on Android)
                if (!File.Exists(Path.Combine(Project, AndroidWwiseLib)))
                {
                    r.Error("no " + AndroidWwiseLib + " - run: python -I setup/stages/wwise_android.py (the Android bindings call it)");
                    return false;
                }
                if (wwiseData == null)
                {
                    r.Error("no AkWwiseProjectData (Wwise editor data) - cannot select the Release Wwise plug-in config");
                    return false;
                }
                wwiseData.CurrentPluginConfig = AkPluginActivator.CONFIG_RELEASE;     // in memory only: AkBuildPreprocessor enables Plugins/Android/arm64-v8a/Release
                r.Line("Wwise plug-in config for the build: " + AkPluginActivator.GetCurrentConfig() + " (was '" + prevWwiseConfig + "')");
                StripApkStreamingAssets = true;
                Directory.CreateDirectory(outDir);
                var opts = new BuildPlayerOptions
                {
                    scenes = new[] { BootScene },
                    locationPathName = apk,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                    assetBundleManifestPath = bundleManifest,
                };
                r.Line(string.Format("BuildPlayer -> {0} ({1}, IL2CPP ARM64, scenes [{2}], bundle manifest {3})", apk, development ? "development" : "release",
                    BootScene, bundleManifest));
                BuildReport rep = BuildPipeline.BuildPlayer(opts);
                var sum = rep.summary;
                r.Line(string.Format("result {0}, {1:F1} min, size {2:F2} GiB, errors {3}, warnings {4}", sum.result, sum.totalTime.TotalMinutes,
                    sum.totalSize / (double)GiB, sum.totalErrors, sum.totalWarnings));
                int shown = 0;
                foreach (var st in rep.steps)
                    foreach (var msg in st.messages)
                        if ((msg.type == LogType.Error || msg.type == LogType.Exception) && shown++ < 40)
                            r.Line("  build " + msg.type + ": " + msg.content);
                if (sum.result != BuildResult.Succeeded || (AndroidExportOnly ? !File.Exists(Path.Combine(apk, "settings.gradle")) : !File.Exists(apk)))
                {
                    r.Error("BuildPlayer did not succeed");
                    return false;
                }
                if (AndroidExportOnly)
                {
                    r.Line("Unsigned Gradle export: " + apk + "; no Gradle package/sign/install invoked");
                    ok = true; // BuildReport succeeded and settings.gradle exists; not an APK validation.
                }
                else
                {
                    r.Line(string.Format("APK {0} ({1:F1} MiB)", apk, new FileInfo(apk).Length / (1024.0 * 1024.0)));
                    ok = VerifyApkLibs(r, apk);
                }
                // the data the APK does not carry: the Android bundles + Dpr.bin, the Wwise banks and the movies (deploy_android.sh pushes data/)
                ok &= LinkLayout(r, Path.Combine(dataSa, "AssetAssistant"), false, null, null);
                ok &= CloneAndroidBanks(r, Path.Combine(dataSa, AndroidBanksDst));
                string movies = Path.Combine(Project, "Assets", "StreamingAssets", "Movie");
                int movieFiles = 0, copies = 0;
                if (Directory.Exists(movies))
                    foreach (var f in Directory.GetFiles(movies, "*", SearchOption.AllDirectories).Where(x => !x.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)))
                    {
                        Link(f, Path.Combine(dataSa, "Movie", f.Substring(movies.Length + 1)), ref copies);
                        movieFiles++;
                    }
                r.Line("movies cloned into " + Path.Combine(dataSa, "Movie") + ": " + movieFiles + " (" + copies + " plain copies)");
                if (movieFiles == 0)
                    r.Error("no movies at " + movies);
                File.WriteAllText(Path.Combine(outDir, "data", "BUILD.txt"), "android player " + stamp + " (" + (development ? "development" : "release") +
                    "); bundles from " + BundleOut(false) + "\n");
                r.Line("player: " + apk);
                r.Line("data:   " + Path.Combine(outDir, "data") + "  (push: sh setup/deploy_android.sh)");
                return ok && r.Errors == 0;
            }
            finally
            {
                StripApkStreamingAssets = false;
                File.WriteAllBytes(Path.Combine(Project, StartupAsset), startupBytes);
                AssetDatabase.ImportAsset(StartupAsset, ImportAssetOptions.ForceUpdate);
                AkWwiseEditorSettings.Instance.CopySoundBanksAsPreBuildStep = prevWwiseCopy;
                if (wwiseData != null)
                    wwiseData.CurrentPluginConfig = prevWwiseConfig;                   // before SaveAssets, should anything have dirtied it
                AssetDatabase.SaveAssets();          // BEFORE the metas: saving re-writes importers still dirty from the build
                RestorePluginMetas(r, metas);
                bool psSame = File.ReadAllBytes(projectSettingsPath).SequenceEqual(projectSettingsBytes);
                if (!psSame)
                    File.WriteAllBytes(projectSettingsPath, projectSettingsBytes);
                bool gsSame = File.ReadAllBytes(graphicsPath).SequenceEqual(graphicsBytes);
                if (!gsSame)
                    File.WriteAllBytes(graphicsPath, graphicsBytes);
                bool startupSame = File.ReadAllBytes(Path.Combine(Project, StartupAsset)).SequenceEqual(startupBytes);
                if (!startupSame)
                    r.Error("StartupSettings.asset is not byte-identical to its backup " + bdir);
                r.Line("restored: StartupSettings (byte-identical " + startupSame + "), ProjectSettings.asset (" + (psSame ? "unchanged" : "original bytes written back") +
                       "), GraphicsSettings.asset (" + (gsSame ? "unchanged" : "original bytes written back") + "), Wwise copy flag " + prevWwiseCopy);
                WriteState("clean (last: android player build " + stamp + ", " + (ok ? "OK" : "FAILED") + ")\n");
            }
        }

        // the APK must carry the arm64 player + the Wwise engine and nothing 32-bit; a missing libAkSoundEngine.so would be a silent build
        static bool VerifyApkLibs(Report r, string apk)
        {
            string[] libs;
            using (var zip = System.IO.Compression.ZipFile.OpenRead(apk))
                libs = zip.Entries.Select(e => e.FullName).Where(n => n.StartsWith("lib/", StringComparison.Ordinal) && n.EndsWith(".so", StringComparison.Ordinal)).ToArray();
            r.Line("APK native libs: " + string.Join(", ", libs));
            bool ok = true;
            foreach (var need in new[] { "lib/arm64-v8a/libunity.so", "lib/arm64-v8a/libAkSoundEngine.so" })
                if (!libs.Contains(need))
                {
                    r.Error("the APK has no " + need);
                    ok = false;
                }
            foreach (var bad in libs.Where(n => !n.StartsWith("lib/arm64-v8a/", StringComparison.Ordinal)))
            {
                r.Error("the APK carries a non-arm64 lib: " + bad);
                ok = false;
            }
            return ok;
        }
    }

    // The project's StreamingAssets (Switch bundles 3.9 GB, Wwise banks of other platforms, movies) is staged into the generated Gradle
    // project whole; for the BDSP Android build it is removed there before Gradle packs the APK (the data is pushed next to the app).
    // Only while WinBuild.StripApkStreamingAssets is set - any other Android build is left alone.
    sealed class AndroidApkStreamingAssetsStrip : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder { get { return 0; } }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if (!WinBuild.StripApkStreamingAssets)
                return;
            string assets = Path.Combine(path, "src", "main", "assets");
            foreach (var d in new[] { "AssetAssistant", "Audio", "Movie" })
            {
                string p = Path.Combine(assets, d);
                if (Directory.Exists(p))
                {
                    Directory.Delete(p, true);
                    Debug.Log("[WinBuild] APK: removed StreamingAssets/" + d + " from the Gradle project (" + p + ")");
                }
            }
            if (Directory.Exists(assets))
                Debug.Log("[WinBuild] APK assets left: " + string.Join(", ", Directory.GetFileSystemEntries(assets).Select(Path.GetFileName).ToArray()));
        }
    }
}
