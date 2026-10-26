// BDSP iOS build (2026-10-24): the same 13,651-bundle plan as the Windows / macOS / Android builds, for BuildTarget.iOS, and an IL2CPP
// Xcode project. Editor only. NOT shipped code. NOTHING here runs by itself: menu BDSP/iOS Build/..., or setup/build_ios.sh, which runs
// `-executeMethod BdspWinBuild.WinBuild.Ios<Step>` in order (the editor exits with 0 = OK, 1 = failed), then xcodebuild.
//
// What differs from the macOS build (BuildPlayerMac):
//   - Needs Unity's iOS Build Support (PlaybackEngines/iOSSupport next to Unity.app). CheckIos says so when it is missing.
//   - IL2CPP: iOS has no Mono player. Managed stripping Minimal (the lowest IL2CPP allows) + Assets/link.xml, engine stripping off.
//     Unity writes an Xcode project (Builds/ios_<time>/Xcode); Xcode compiles the IL2CPP C++, links and signs it (build_ios.py's
//     `xcode` step compiles it unsigned, CODE_SIGNING_ALLOWED=NO; signing is the user's, in Xcode, with their own Apple ID).
//   - BuildTargetGroup.iOS's scripting defines PC_UNLOCKED_FPS;BDSP_UNITY6_GATE are SAVED in ProjectSettings.asset (key 4, as Standalone's
//     1 and Android's 7): the switch and every editor start while iOS is active compile with the saved ones, before any step code runs
//     (without BDSP_UNITY6_GATE MeshRendererDirect compiles the removed 2019.4 BatchRendererGroup API). SwitchToIos and build_ios.py
//     put them back if they are missing; the Standalone group's defines are not touched.
//   - Audio: the Wwise 2019.2.15 iOS engine is linked statically (Assets/Wwise/Deployment/Plugins/iOS/Release/libAkSoundEngine.a + the
//     Generated/iOS bindings, DllImport "__Internal" - setup/stages/wwise_ios.py installs both; build_ios.py runs it). Wwise's plug-in
//     activator enables only the folder of its current config (none set = Profile, what macOS ships): the player step sets Release in
//     memory for the build and puts the previous value back. The banks are the macOS ones (Switch-authored, Vorbis, platform-neutral):
//     cut out of the Xcode project like the other StreamingAssets, then APFS-cloned into <data>/Audio/GeneratedSoundBanks/iOS, where
//     AkBasePathGetter looks on iOS (from PcPaths.StreamingAssets - patched by wwise_ios.py).
//   - Textures: the ripped DXT/BC native textures are refused for iOS; the bundle + player steps run with ASTC copies swapped into
//     Assets by build_ios.py (BdspWinBuild.iOS.Textures.cs, setup/stages/ios_textures.py) and refuse to start while any is left.
//   - The data: by default NOT in the app (as Android). ~6 GB inside the .app would be re-copied by every install / re-sign (a free
//     Apple ID signs for 7 days). The player build writes Builds/ios_<time>/data/StreamingAssets/{AssetAssistant,Movie} (APFS clones);
//     the user copies that StreamingAssets folder once into the app's Documents (Finder > iPhone > Files > Dpr - Info.plist
//     UIFileSharingEnabled is set below), where PcPaths.StreamingAssets looks first on iOS. BDSP_IOS_DATA=app puts the iOS bundles +
//     movies into the Xcode project's Data/Raw instead (one self-contained .ipa).
//   - Input: GameController (DualSense / DualShock 4 / Xbox / MFi) through PcMacPad + Assets/Plugins/iOS/PcGamepad.m (__Internal), the
//     on-screen controller (PcVirtualPad) for touch. Frame pacing: PcFrameRate's iOS branch (display link, 60 or the cap).
// Every iOS player setting is applied in memory for the step and ProjectSettings.asset / GraphicsSettings.asset / StartupSettings and
// the Wwise + plug-in .meta files are written back byte for byte after it (as the macOS / Android steps do), so the files the Mac
// build uses never change. The active build target (Library state) does: setup/build_mac.sh needs it back on StandaloneOSX.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace BdspWinBuild
{
    public static partial class WinBuild
    {
        // a free Apple ID's Personal Team needs a bundle id no other team has registered: BDSP_IOS_BUNDLE_ID overrides it (or change it in
        // Xcode > Signing & Capabilities). BDSP_IOS_TEAM_ID = the user's 10-character team id, only so Xcode opens with it selected.
        const string IosDefaultBundleId = "com.opendpr.bdsp";
        const string IosMinOs = "15.0";
        const string IosDefines = "PC_UNLOCKED_FPS;BDSP_UNITY6_GATE";
        static string IosBundleId { get { string v = Environment.GetEnvironmentVariable("BDSP_IOS_BUNDLE_ID"); return string.IsNullOrEmpty(v) ? IosDefaultBundleId : v.Trim(); } }
        static string IosTeamId { get { return (Environment.GetEnvironmentVariable("BDSP_IOS_TEAM_ID") ?? "").Trim(); } }
        static bool IosDataInApp { get { return (Environment.GetEnvironmentVariable("BDSP_IOS_DATA") ?? "").Trim() == "app"; } }
        static string IosPcGamepadPlugin { get { return Path.Combine(Project, "Assets", "Plugins", "iOS", "PcGamepad.m"); } }
        static string IosPcGamepadSource { get { return Path.Combine(Project, "setup", "native", "pcgamepad", "PcGamepad.m"); } }

        // ---- entry points (menu + -executeMethod; setup/build_ios.sh runs them in this order) -------------------------------------
        [MenuItem("BDSP/iOS Build/0  Switch the active build target to iOS (re-imports)", false, 0)]
        public static void IosSwitch() { if (Confirm("Switch the active build target to iOS? Assets re-import for iOS (long the first time).")) RunIos("switch", r => SwitchToIos(r)); }

        [MenuItem("BDSP/iOS Build/1  Check plan + project (builds nothing)", false, 1)]
        public static void IosCheck() { RunIos("check", r => CheckCore(r, false, false)); }

        [MenuItem("BDSP/iOS Build/1a Compile the player scripts for iOS (builds nothing)", false, 2)]
        public static void IosCompileScripts() { RunIos("compile", r => CompileIosScripts(r)); }

        [MenuItem("BDSP/iOS Build/2  Build AssetBundles - FULL (13,651 bundles, ~6 GB)", false, 20)]
        public static void IosBuildBundlesFull() { if (Confirm("Build ALL 13,651 iOS bundles into Builds/bundles_ios? This takes a long time and ~6 GB.")) RunIos("bundles_full", r => IosRequireNoDesktopTextures(r) && BuildBundles(r, false)); }

        [MenuItem("BDSP/iOS Build/3  Rewrite Dpr.bin from the last FULL bundle build", false, 22)]
        public static void IosRewriteManifestFull() { RunIos("manifest_full", r => RewriteManifest(r, false)); }

        [MenuItem("BDSP/iOS Build/4  Build Xcode project - RELEASE (IL2CPP)", false, 40)]
        public static void IosBuildPlayerRelease() { if (Confirm("Build the iOS Xcode project (release, IL2CPP) into Builds/ios_<time>/Xcode?")) RunIos("player_release", r => IosRequireNoDesktopTextures(r) && BuildPlayerIos(r, false)); }

        [MenuItem("BDSP/iOS Build/5  Build Xcode project - DEVELOPMENT (IL2CPP)", false, 41)]
        public static void IosBuildPlayerDev() { if (Confirm("Build the iOS Xcode project (development, IL2CPP) into Builds/ios_<time>_dev/Xcode?")) RunIos("player_dev", r => IosRequireNoDesktopTextures(r) && BuildPlayerIos(r, true)); }

        // setup/build_mac.sh's check refuses any other active target: this puts it back (sh setup/build_ios.sh --back-to-mac)
        [MenuItem("BDSP/iOS Build/9  Switch the active build target back to macOS", false, 90)]
        public static void IosSwitchBackToMac() { if (Confirm("Switch the active build target back to macOS (StandaloneOSX)?")) RunIos("switch_back_mac", r => SwitchBackToMac(r)); }

        static bool SwitchBackToMac(Report r)
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneOSX)
            {
                r.Line("active build target is already StandaloneOSX");
                return true;
            }
            var t0 = DateTime.Now;
            r.Line("active build target " + EditorUserBuildSettings.activeBuildTarget + " -> StandaloneOSX");
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
            r.Line(string.Format("SwitchActiveBuildTarget {0} after {1:F1} min; active target now {2}", ok ? "OK" : "FAILED",
                (DateTime.Now - t0).TotalMinutes, EditorUserBuildSettings.activeBuildTarget));
            if (!ok || EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
                r.Error("the active build target did not become StandaloneOSX");
            return r.Errors == 0;
        }

        // ---- 0. switch -----------------------------------------------------------------------------------------------------------
        static bool SwitchToIos(Report r)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                r.Error("no iOS build support in this editor (" + EditorApplication.applicationPath + ") - install the iOS module first (see setup/build_ios.py)");
                return false;
            }
            var t0 = DateTime.Now;
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS)
            {
                r.Line("active build target is already iOS");
                return true;
            }
            // the switch recompiles the scripts with the iOS group's SAVED defines (as does every later editor start while iOS is
            // active): without BDSP_UNITY6_GATE MeshRendererDirect compiles the removed 2019.4 BRG API and nothing after it can run
            if (PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS) != IosDefines)
            {
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.iOS, IosDefines);
                AssetDatabase.SaveAssets();
                r.Line("iOS scripting defines set + saved: " + IosDefines);
            }
            r.Line("active build target " + EditorUserBuildSettings.activeBuildTarget + " -> iOS");
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            r.Line(string.Format("SwitchActiveBuildTarget {0} after {1:F1} min; active target now {2}", ok ? "OK" : "FAILED",
                (DateTime.Now - t0).TotalMinutes, EditorUserBuildSettings.activeBuildTarget));
            if (!ok || EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                r.Error("the active build target did not become iOS");
            return r.Errors == 0;
        }

        // ---- 1. check (the iOS part of CheckCore) ----------------------------------------------------------------------------------
        static bool CheckIos(Report r)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
                r.Error("no iOS player support (BuildPipeline.IsBuildTargetSupported(iOS, iOS) is false) - install UnitySetup-iOS-Support-for-Editor-" +
                        Application.unityVersion + ".pkg into " + Path.Combine(Path.GetDirectoryName(EditorApplication.applicationPath), "PlaybackEngines", "iOSSupport"));
            if (!File.Exists(Path.Combine(Project, "Assets", "link.xml")))
                r.Error("Assets/link.xml is missing - IL2CPP stripping would remove code the game reaches by name / from bundles");
            if (!File.Exists(IosPcGamepadPlugin))
                r.Error("missing " + IosPcGamepadPlugin + " (the iOS copy of " + IosPcGamepadSource + ")");
            else if (File.Exists(IosPcGamepadSource) && !File.ReadAllBytes(IosPcGamepadPlugin).SequenceEqual(File.ReadAllBytes(IosPcGamepadSource)))
                r.Error("Assets/Plugins/iOS/PcGamepad.m differs from setup/native/pcgamepad/PcGamepad.m - copy the source over it");
            r.Line(string.Format("iOS player settings now: bundle id '{0}', backend {1}, stripping {2}, min iOS {3}, graphics {4} (default {5}), defines '{6}'",
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS), PlayerSettings.GetScriptingBackend(NamedBuildTarget.iOS),
                PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.iOS), PlayerSettings.iOS.targetOSVersionString,
                string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.iOS).Select(x => x.ToString()).ToArray()),
                PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.iOS), PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS)));
            r.Line("each build step uses: bundle id " + IosBundleId + ", IL2CPP, stripping Minimal + link.xml, engine stripping off, iOS " + IosMinOs +
                   "+, Metal, landscape, defines " + IosDefines + ", data " + (IosDataInApp ? "IN the app (BDSP_IOS_DATA=app)" : "in Documents (copied once)") +
                   "; ProjectSettings.asset is written back afterwards");
            if (File.Exists(StateFile) && File.ReadAllText(StateFile).Contains("ACTIVE"))
                r.Warn("STATE.txt says a build step did not finish - run Recover first:\n" + File.ReadAllText(StateFile));
            return r.Errors == 0;
        }

        // ---- 1a. compile the player scripts (UNITY_IOS, !UNITY_EDITOR) without building --------------------------------------
        static bool CompileIosScripts(Report r)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                r.Error("no iOS build support in this editor - install the iOS module first (setup/stages/ios_compile_check.py runs without it)");
                return false;
            }
            string projectSettingsPath = Path.Combine(Project, "ProjectSettings", "ProjectSettings.asset");
            byte[] projectSettingsBytes = File.ReadAllBytes(projectSettingsPath);
            string outDir = Path.Combine(Work, "ios_compile");
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
                ApplyIosPlayerSettings(r, false);
                var settings = new UnityEditor.Build.Player.ScriptCompilationSettings
                {
                    group = BuildTargetGroup.iOS,
                    target = BuildTarget.iOS,
                    options = UnityEditor.Build.Player.ScriptCompilationOptions.None,
                };
                var result = UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(settings, outDir);
                var dlls = result.assemblies == null ? new string[0] : result.assemblies.ToArray();
                r.Line("compiled " + dlls.Length + " player assemblies for iOS -> " + outDir + ": " + string.Join(", ", dlls));
                if (!dlls.Contains("Assembly-CSharp.dll"))
                    r.Error("Assembly-CSharp.dll was not produced - the player scripts do not compile for iOS");
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

        // ---- the iOS player settings every step uses (in memory; the caller writes ProjectSettings.asset back) --------------------
        static void ApplyIosPlayerSettings(Report r, bool development)
        {
            var ios = NamedBuildTarget.iOS;
            PlayerSettings.SetApplicationIdentifier(ios, IosBundleId);
            // Map/character shaders sample _ShadowMapTexture as a SCREEN-SPACE shadow mask (SHADOWS_SCREEN, desktop/NVN style).
            // Mobile tiers default cascadedShadowMaps=false -> Unity skips the screen-space collect and binds the raw shadow map:
            // "transparent" shadows that move with the casters. Force cascaded/screen-space shadows on every iOS tier.
            foreach (GraphicsTier t in new[] { GraphicsTier.Tier1, GraphicsTier.Tier2, GraphicsTier.Tier3 })
            {
                var ts = UnityEditor.Rendering.EditorGraphicsSettings.GetTierSettings(BuildTargetGroup.iOS, t);
                ts.cascadedShadowMaps = true;
                UnityEditor.Rendering.EditorGraphicsSettings.SetTierSettings(BuildTargetGroup.iOS, t, ts);
            }
            PlayerSettings.bundleVersion = "1.0";
            PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.SetScriptingBackend(ios, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(ios, development ? Il2CppCompilerConfiguration.Debug : Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetManagedStrippingLevel(ios, ManagedStrippingLevel.Minimal);
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.SetScriptingDefineSymbols(ios, IosDefines);
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.targetOSVersionString = IosMinOs;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.appleEnableAutomaticSigning = Environment.GetEnvironmentVariable("BDSP_NO_SIGN") != "1";
            PlayerSettings.iOS.appleDeveloperTeamID = IosTeamId;
            // Metal only: the bundles carry one set of shader programs (as the Mac's)
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorUserBuildSettings.iOSXcodeBuildConfig = development ? XcodeBuildConfig.Debug : XcodeBuildConfig.Release;
            EditorUserBuildSettings.symlinkSources = false;
            r.Line("iOS player settings for this step: " + IosBundleId + ", IL2CPP " + (development ? "Debug" : "Release") + ", stripping Minimal, iOS " + IosMinOs +
                   "+, Metal, landscape, defines " + IosDefines + ", team '" + (IosTeamId.Length > 0 ? "[configured; redacted]" : "(none - pick it in Xcode)") + "'");
        }

        // ---- 4/5. the Xcode project --------------------------------------------------------------------------------------------------
        static bool BuildPlayerIos(Report r, bool development)
        {
            if (!CheckCore(r, false, true))
                return false;
            if (!File.Exists(ManifestOut(false)))
            {
                r.Error("no iOS Dpr.bin (" + ManifestOut(false) + ") - build the bundles first");
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
                r.Error(string.Format("refusing: {0:F1} GiB free on {1}, the Xcode project build needs >= 12 GiB (Unity copies the project's StreamingAssets " +
                    "into it before it is cut out)", drive.AvailableFreeSpace / (double)GiB, drive.Name));
                return false;
            }
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string outDir = Path.Combine(BuildRoot, "ios" + Flavour + "_" + stamp + (development ? "_dev" : ""));
            if (Directory.Exists(outDir))
            {
                r.Error("output exists: " + outDir);
                return false;
            }
            string xcode = Path.Combine(outDir, "Xcode");
            string raw = Path.Combine(xcode, "Data", "Raw");                         // the iOS player's Application.streamingAssetsPath
            string dataSa = IosDataInApp ? raw : Path.Combine(outDir, "data", "StreamingAssets");

            string bdir = Path.Combine(Backups, "ios_player_" + stamp);
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
            var prevXcodeConfig = EditorUserBuildSettings.iOSXcodeBuildConfig;
            bool prevSymlink = EditorUserBuildSettings.symlinkSources;
            var metas = SnapshotPluginMetas();
            r.Line("backed up: StartupSettings + ProjectSettings -> " + bdir + "; " + metas.Count + " .meta files under Assets/Wwise + Assets/Plugins (in memory)");
            WriteState("ACTIVE ios player build " + stamp + "\nstartup_backup=" + Path.Combine(bdir, "StartupSettings.asset") + "\n");

            bool ok = false;
            try
            {
                ApplyIosPlayerSettings(r, development);
                if (!ApplyBoxArtIcons(r, NamedBuildTarget.iOS, false))             // box-art app icon (in memory; ProjectSettings is restored below)
                    return false;
                AkWwiseEditorSettings.Instance.CopySoundBanksAsPreBuildStep = false;   // in memory only (never saved)
                SetStartupFields(3, null);                                             // StreamingAssets, as shipped (PcPaths picks Documents on iOS)
                if (!File.Exists(Path.Combine(Project, IosWwiseLib)))
                {
                    r.Error("no " + IosWwiseLib + " - run: python -I setup/stages/wwise_ios.py (the iOS bindings link against it)");
                    return false;
                }
                if (wwiseData == null)
                {
                    r.Error("no AkWwiseProjectData (Wwise editor data) - cannot select the Release Wwise plug-in config");
                    return false;
                }
                wwiseData.CurrentPluginConfig = AkPluginActivator.CONFIG_RELEASE;     // in memory only: AkBuildPreprocessor enables Plugins/iOS/Release
                r.Line("Wwise plug-in config for the build: " + AkPluginActivator.GetCurrentConfig() + " (was '" + prevWwiseConfig + "')");
                Directory.CreateDirectory(outDir);
                var opts = new BuildPlayerOptions
                {
                    scenes = new[] { BootScene },
                    locationPathName = xcode,
                    target = BuildTarget.iOS,
                    targetGroup = BuildTargetGroup.iOS,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                    assetBundleManifestPath = bundleManifest,
                };
                r.Line(string.Format("BuildPlayer -> {0} ({1}, IL2CPP, scenes [{2}], bundle manifest {3})", xcode, development ? "development" : "release",
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
                string pbx = Path.Combine(xcode, "Unity-iPhone.xcodeproj", "project.pbxproj");
                if (sum.result != BuildResult.Succeeded || !File.Exists(pbx))
                {
                    r.Error("BuildPlayer did not succeed (or wrote no " + pbx + ")");
                    return false;
                }
                if (!File.ReadAllText(pbx).Contains("libAkSoundEngine.a"))
                {
                    r.Error("the Xcode project does not link libAkSoundEngine.a (Wwise's activator left " + IosWwiseLib + " off for iOS?)");
                    return false;
                }
                r.Line("Xcode project links libAkSoundEngine.a (Wwise " + AkPluginActivator.CONFIG_RELEASE + ")");
                // the copied project StreamingAssets: Switch bundles + every platform's Wwise banks out (the iOS banks are cloned below);
                // the movies out too unless the data goes into the app
                foreach (var d in IosDataInApp ? new[] { "AssetAssistant", "Audio" } : new[] { "AssetAssistant", "Audio", "Movie" })
                {
                    string gone = Path.Combine(raw, d);
                    if (Directory.Exists(gone))
                    {
                        Directory.Delete(gone, true);
                        r.Line("removed from the Xcode project: Data/Raw/" + d);
                    }
                }
                ok = LinkLayout(r, Path.Combine(dataSa, "AssetAssistant"), false, null, null);
                ok &= CloneIosBanks(r, Path.Combine(dataSa, "Audio", "GeneratedSoundBanks", "iOS"));
                if (!IosDataInApp)
                {
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
                    File.WriteAllText(Path.Combine(outDir, "data", "BUILD.txt"), "ios player " + stamp + " (" + (development ? "development" : "release") +
                        "); bundles from " + BundleOut(false) + "\ncopy data/StreamingAssets into the app's Documents: Finder > iPhone > Files > Dpr\n");
                }
                else if (!Directory.Exists(Path.Combine(raw, "Movie")))
                    r.Error("BDSP_IOS_DATA=app but the Xcode project has no Data/Raw/Movie");
                PatchXcodeProject(r, xcode);
                foreach (var need in new[] { "Info.plist", "Data", "Libraries/Plugins/iOS/PcGamepad.m" })
                    if (!File.Exists(Path.Combine(xcode, need)) && !Directory.Exists(Path.Combine(xcode, need)))
                        r.Warn("the Xcode project has no " + need + " (expected from Unity's iOS layout - check the project)");
                r.Line("Xcode project: " + xcode + "  (compile: sh setup/build_ios.sh --from xcode; sign + install: open it in Xcode)");
                if (!IosDataInApp)
                    r.Line("data:          " + Path.Combine(outDir, "data", "StreamingAssets") + "  (copy into the app's Documents once)");
                return ok && r.Errors == 0;
            }
            finally
            {
                File.WriteAllBytes(Path.Combine(Project, StartupAsset), startupBytes);
                AssetDatabase.ImportAsset(StartupAsset, ImportAssetOptions.ForceUpdate);
                AkWwiseEditorSettings.Instance.CopySoundBanksAsPreBuildStep = prevWwiseCopy;
                if (wwiseData != null)
                    wwiseData.CurrentPluginConfig = prevWwiseConfig;                   // before SaveAssets, should anything have dirtied it
                EditorUserBuildSettings.iOSXcodeBuildConfig = prevXcodeConfig;
                EditorUserBuildSettings.symlinkSources = prevSymlink;
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
                WriteState("clean (last: ios player build " + stamp + ", " + (ok ? "OK" : "FAILED") + ")\n");
            }
        }

        const string IosWwiseLib = "Assets/Wwise/Deployment/Plugins/iOS/Release/libAkSoundEngine.a";

        // the iOS Wwise banks = APFS clones of the macOS ones (Switch-authored banks + Vorbis .wem: nothing platform-specific in them; the
        // macOS build plays the same files). Wwise's own pre-build copy is off for these builds (CopySoundBanksAsPreBuildStep).
        static bool CloneIosBanks(Report r, string dst)
        {
            string src = Path.Combine(Project, "Assets", "StreamingAssets", "Audio", "GeneratedSoundBanks", "Mac");
            if (!File.Exists(Path.Combine(src, "Init.bnk")))
            {
                r.Error("no Wwise banks at " + src + " (Init.bnk) - the iOS build would be silent");
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

        // Info.plist + the Xcode project after Unity wrote them (UnityEditor.iOS.Xcode: MacStandaloneSupport ships it too, so this file
        // compiles in an editor without the iOS module).
        static void PatchXcodeProject(Report r, string xcode)
        {
            string plistPath = Path.Combine(xcode, "Info.plist");
            var plist = new UnityEditor.iOS.Xcode.PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            root.SetBoolean("UIFileSharingEnabled", true);                 // Finder > iPhone > Files > Dpr: copy the data into Documents
            root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);    // ... and the Files app shows it
            root.SetBoolean("CADisableMinimumFrameDurationOnPhone", true); // ProMotion: a cap above 60 may use 120 Hz (PcFrameRate's iOS branch)
            root.SetBoolean("GCSupportsControllerUserInteraction", true);  // the pad's Home / menu buttons go to the game, not the system
            root.SetBoolean("GCSupportsGameMode", true);
            var controllers = root.CreateArray("GCSupportedGameControllers");
            controllers.AddDict().SetString("ProfileName", "ExtendedGamepad");
            plist.WriteToFile(plistPath);
            r.Line("Info.plist: UIFileSharingEnabled, LSSupportsOpeningDocumentsInPlace, CADisableMinimumFrameDurationOnPhone, GCSupportsControllerUserInteraction, " +
                   "GCSupportsGameMode = true; GCSupportedGameControllers = [ExtendedGamepad]");

            string pbxPath = UnityEditor.iOS.Xcode.PBXProject.GetPBXProjectPath(xcode);
            var pbx = new UnityEditor.iOS.Xcode.PBXProject();
            pbx.ReadFromFile(pbxPath);
            string framework = pbx.GetUnityFrameworkTargetGuid();
            pbx.AddFrameworkToProject(framework, "GameController.framework", false);   // PcGamepad.m (also in its importer settings)
            pbx.WriteToFile(pbxPath);
            r.Line("Xcode project: GameController.framework linked into UnityFramework");
        }
    }
}
