// BDSP PC rebuild - the real Windows build (HANDOFF "s38"). Editor only. NOT shipped code. NOTHING here runs by itself:
// menu BDSP/Windows Build/..., or `-executeMethod BdspWinBuild.WinBuild.<Method>` (the editor then exits with 0 = OK, 1 = failed).
//
// The editor play path loads every asset through UnityEditorProxy + AssetDatabase (StartupSettings._assetBundleTarget = 0); a player
// has no AssetDatabase. What the player needs, as the shipped game had it:
//   1. Windows x64 AssetBundles for the 13,651 shipped bundles (work/winbuild/bundle_plan.tsv from tools/winbuild/winbuild_plan.py):
//      shipped names, shipped language variants, the SHIPPED container keys as addressable names (AssetBundle.LoadAsset(
//      "assets/...png") keeps matching although AssetRipper renamed files). ONE BuildAssetBundles call - 7,327 shipped dependency
//      edges cross the sub-projects. Output build/bundles_win64/ (outside the project; the legacy pipeline rebuilds incrementally).
//   2. The Windows Dpr.bin from the game's own AssetBundleDownloadManifest.Load(targetPath, AssetBundleManifest, callback) (the
//      developers' tool path); each record keeps the SHIPPED projectName / assetPaths / scene flag, the shipped record order and
//      variant list; the 9 shipped records whose bundles never shipped (debug / tool scenes) are carried unchanged.
//      -> build/manifest_win64/Dpr.bin, re-read with the game's own loader and compared with the shipped one.
//   3. The player: level0 only (the shipped build's one scene), IL2CPP (author 2026-10-12; the shipped game is IL2CPP too) Release
//      C++, managed stripping Low + Assets/link.xml, engine code stripping off, the bundle build's manifest as
//      assetBundleManifestPath, IL2CPP / cl.exe temp on E:, Direct3D11, StartupSettings target StreamingAssets (3) for the build only, Wwise's pre-build bank copy OFF for the build (its post-build
//      step DELETES the destination folder = our shipped banks), the Switch bundles in Assets/StreamingAssets/AssetAssistant moved
//      aside for the build (Unity copies StreamingAssets whole), then the Windows bundles HARD-LINKED (no extra disk) into
//      <player>/Dpr_Data/StreamingAssets/AssetAssistant/<projectName>/<bundle> + Dpr.bin. Every change is restored in `finally`.
// Optional: "Editor play: from Windows bundles" = StartupSettings target RuntimeLocation (2) + URI on a hard-link layout, the
// developers' own editor-against-bundles mode; "back to AssetDatabase" restores the exact StartupSettings bytes.
// Reports: work/winbuild/reports/<step>_<stamp>.txt; the last log line is "BDSP_WINBUILD_DONE <step> OK|FAILED".
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using SmartPoint.AssetAssistant;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace BdspWinBuild
{
    public static partial class WinBuild
    {
        // ---- layout ------------------------------------------------------------------------------------------------------------
        static string Project { get { return Path.GetFullPath(Directory.GetCurrentDirectory()); } }
        static string Root { get { return Path.GetFullPath(Path.Combine(Project, "..")); } }
        // the automated setup (setup/setup.py) passes BDSP_SETUP_WORK (its work folder) and BDSP_DUMP_DATA (the dump's romfs/Data):
        // inputs + reports then live in <work>/build, output in <project>/Builds; without them the workspace layout (Root = ..)
        static string SetupWork { get { return Environment.GetEnvironmentVariable("BDSP_SETUP_WORK"); } }
        static string Work { get { return string.IsNullOrEmpty(SetupWork) ? Path.Combine(Root, "work", "winbuild") : Path.Combine(SetupWork, "build"); } }
        static string Reports { get { return Path.Combine(Work, "reports"); } }
        static string Backups { get { return Path.Combine(Work, "backup"); } }
        static string StateFile { get { return Path.Combine(Work, "STATE.txt"); } }
        static string Plan(bool subset) { return Path.Combine(Work, subset ? "bundle_plan_subset.tsv" : "bundle_plan.tsv"); }
        static string ShippedManifest
        {
            get
            {
                var data = Environment.GetEnvironmentVariable("BDSP_DUMP_DATA");
                return Path.Combine(string.IsNullOrEmpty(data) ? Path.Combine(Root, "romfs", "Data") : data, "StreamingAssets", "AssetAssistant", "Dpr.bin");
            }
        }
        static string BuildRoot { get { return string.IsNullOrEmpty(SetupWork) ? Path.Combine(Root, "build") : Path.Combine(Project, "Builds"); } }
        // s46 C5: BDSP_BUILD_FLAVOUR=<name> builds into build/bundles_win64_<name>[_side], build/manifest_win64_<name> and
        // build/win64_<name>_<stamp>, so a rebuild never rewrites the bundle files an existing player folder hard-links.
        // Unset = the original folders.
        static string Flavour
        {
            get
            {
                string f = Environment.GetEnvironmentVariable("BDSP_BUILD_FLAVOUR");
                return string.IsNullOrEmpty(f) ? "" : "_" + f.Trim();
            }
        }
        // macOS (2026-10-19): the BDSP/Mac Build entry points set Mac and build the same plan for StandaloneOSX - bundles_mac/,
        // manifest_mac/, a Mono arm64 player Builds/mac_arm64_<time>/Dpr.app (BuildPlayerMac). Every other entry point clears it.
        static bool Mac;
        // Android (2026-10-20): the BDSP/Android Build entry points (BdspWinBuild.Android.cs) set Android and build the same plan for
        // BuildTarget.Android - bundles_android/, manifest_android/, a Mono APK Builds/android_<time>/Dpr.apk. Every other entry point clears it.
        static bool Android;
        // iOS (2026-10-24): the BDSP/iOS Build entry points (BdspWinBuild.iOS.cs) set Ios and build the same plan for BuildTarget.iOS -
        // bundles_ios/, manifest_ios/, an IL2CPP Xcode project Builds/ios_<time>/Xcode (+ data/). Every other entry point clears it.
        static bool Ios;
        static BuildTarget Target { get { return Ios ? BuildTarget.iOS : Android ? BuildTarget.Android : Mac ? BuildTarget.StandaloneOSX : BuildTarget.StandaloneWindows64; } }
        static string Plat { get { return Ios ? "ios" : Android ? "android" : Mac ? "mac" : "win64"; } }
        static string BundleOut(bool subset) { return Path.Combine(BuildRoot, "bundles_" + Plat + (subset ? "_subset" : Flavour)); }
        static string ManifestOut(bool subset) { return Path.Combine(BuildRoot, "manifest_" + Plat + (subset ? "_subset" : Flavour), "Dpr.bin"); }
        // s39: a bundle whose name is a FOLDER of another bundle's name ("masterdatas" (Dpr) vs "masterdatas/uimasterdatas" (UIs),
        // "masterdatas/effectdatabase" (Effects)) cannot share one output directory (measured: "Cannot create folder .../masterdatas").
        // Shipped, every sub-project had its own folder. Those bundles are built by a SECOND BuildAssetBundles call into
        // <BundleOut>_side - exact only for bundles without dependencies and dependents, which Check verifies on the shipped graph.
        static string SideOut(bool subset) { return BundleOut(subset) + "_side"; }
        static string BuiltFile(bool subset, string bundle)
        {
            string main = Path.Combine(BundleOut(subset), bundle);
            if (File.Exists(main))
                return main;
            string side = Path.Combine(SideOut(subset), bundle);
            return File.Exists(side) ? side : main;
        }
        static string SwitchAside { get { return Path.Combine(Project, "_winbuild_aside", "AssetAssistant"); } } // outside Assets/

        const string StartupAsset = "Assets/Resources/StartupSettings.asset";
        const string SwitchBundles = "Assets/StreamingAssets/AssetAssistant";
        const string BanksRoot = "Assets/StreamingAssets/Audio/GeneratedSoundBanks";
        static string BankPlatform { get { return Mac ? "Mac" : "Windows"; } }
        static string BanksDir { get { return BanksRoot + "/" + BankPlatform; } }
        const string BootScene = "Assets/Scenes/level0.unity";            // the shipped build's only scene (globalgamemanagers)
        const string ManifestFileName = "Dpr.bin";                         // Application.productName + ".bin"
        const int ShippedRecordCount = 13660;
        const long GiB = 1L << 30;
        // s39: the shipped variant list of the `shaders` bundle as a ShaderVariantCollection packed INTO that bundle - BuildAssetBundles
        // keeps a shader_feature variant only when a material / collection in the shader's own bundle asks for it (measured: Map/Standard
        // 6 sub-programs vs 5,140 shipped; fxparticle, which carries the developers' own collections, = shipped exactly). Derived asset
        // in the git-ignored SharedAssets tree; input work/winbuild/shipped_shader_variants.tsv (tools/winbuild/shader_variants_shipped.py)
        const string ShadersBundle = "shaders";
        const string VariantsAsset = "Assets/SharedAssets/shaders/WinBuildShippedVariants.shadervariants";
        const string VariantsKey = "assets/winbuild/shippedshadervariants.shadervariants";   // one extra container key (game ignores it: `is Shader`)
        static string VariantsTsv { get { return Path.Combine(Work, "shipped_shader_variants.tsv"); } }

        // ---- entry points (menu + -executeMethod) ------------------------------------------------------------------------------
        [MenuItem("BDSP/Windows Build/1  Check plan + project (builds nothing)", false, 1)]
        public static void Check() { Run("check", r => CheckCore(r, false, false)); }

        [MenuItem("BDSP/Windows Build/2  Build AssetBundles - SUBSET (bundle_plan_subset.tsv)", false, 20)]
        public static void BuildBundlesSubset() { if (Confirm("Build the SUBSET bundles (work/winbuild/bundle_plan_subset.tsv) into build/bundles_win64_subset?")) Run("bundles_subset", r => BuildBundles(r, true)); }

        [MenuItem("BDSP/Windows Build/3  Build AssetBundles - FULL (13,651 bundles, hours, ~7 GB)", false, 21)]
        public static void BuildBundlesFull() { if (Confirm("Build ALL 13,651 Windows bundles into build/bundles_win64? This takes hours and ~7 GB on E:.")) Run("bundles_full", r => BuildBundles(r, false)); }

        [MenuItem("BDSP/Windows Build/4  Rewrite Dpr.bin from the last FULL bundle build", false, 22)]
        public static void RewriteManifestFull() { Run("manifest_full", r => RewriteManifest(r, false)); }

        [MenuItem("BDSP/Windows Build/4a Rewrite Dpr.bin from the last SUBSET bundle build", false, 23)]
        public static void RewriteManifestSubset() { Run("manifest_subset", r => RewriteManifest(r, true)); }

        [MenuItem("BDSP/Windows Build/5  Build player - DEVELOPMENT (full bundles)", false, 40)]
        public static void BuildPlayerDev() { if (Confirm("Build the Windows player (development) into build/win64_<time>?")) Run("player_dev", r => BuildPlayer(r, true, false)); }

        [MenuItem("BDSP/Windows Build/6  Build player - RELEASE (full bundles)", false, 41)]
        public static void BuildPlayerRelease() { if (Confirm("Build the Windows player (release) into build/win64_<time>?")) Run("player_release", r => BuildPlayer(r, false, false)); }

        [MenuItem("BDSP/Windows Build/5a Build player - DEVELOPMENT (SUBSET bundles)", false, 42)]
        public static void BuildPlayerSubsetDev() { if (Confirm("Build a development player with the SUBSET bundles into build/win64_<time>_subset?")) Run("player_subset_dev", r => BuildPlayer(r, true, true)); }

        [MenuItem("BDSP/Windows Build/Editor play: from Windows bundles (FULL)", false, 60)]
        public static void EditorPlayFromBundlesFull() { Run("editor_bundles_full", r => EditorPlayFromBundles(r, false)); }

        [MenuItem("BDSP/Windows Build/Editor play: from Windows bundles (SUBSET)", false, 61)]
        public static void EditorPlayFromBundlesSubset() { Run("editor_bundles_subset", r => EditorPlayFromBundles(r, true)); }

        [MenuItem("BDSP/Windows Build/Editor play: back to AssetDatabase (default)", false, 62)]
        public static void EditorPlayFromAssetDatabase() { Run("editor_assetdatabase", r => EditorPlayBack(r)); }

        [MenuItem("BDSP/Windows Build/Recover after a crash (Switch bundles + StartupSettings)", false, 80)]
        public static void Recover() { Run("recover", r => RecoverCore(r)); }

        // ---- macOS: the same plan for StandaloneOSX, a Mono player (setup/build_mac.sh runs these in order) ----------------------
        [MenuItem("BDSP/Mac Build/1  Check plan + project (builds nothing)", false, 1)]
        public static void MacCheck() { RunMac("check", r => CheckCore(r, false, false)); }

        [MenuItem("BDSP/Mac Build/2  Build AssetBundles - FULL (13,651 bundles, ~7 GB)", false, 20)]
        public static void MacBuildBundlesFull() { if (Confirm("Build ALL 13,651 macOS bundles into Builds/bundles_mac? This takes a long time and ~7 GB.")) RunMac("bundles_full", r => BuildBundles(r, false)); }

        [MenuItem("BDSP/Mac Build/3  Rewrite Dpr.bin from the last FULL bundle build", false, 22)]
        public static void MacRewriteManifestFull() { RunMac("manifest_full", r => RewriteManifest(r, false)); }

        [MenuItem("BDSP/Mac Build/4  Build player .app - RELEASE (Mono)", false, 40)]
        public static void MacBuildPlayerRelease() { if (Confirm("Build the macOS player (release, Mono) into Builds/mac_arm64_<time>/Dpr.app?")) RunMac("player_release", r => BuildPlayerMac(r, false)); }

        [MenuItem("BDSP/Mac Build/5  Build player .app - DEVELOPMENT (Mono)", false, 41)]
        public static void MacBuildPlayerDev() { if (Confirm("Build the macOS player (development, Mono) into Builds/mac_arm64_<time>_dev/Dpr.app?")) RunMac("player_dev", r => BuildPlayerMac(r, true)); }

        // ---- run / report ------------------------------------------------------------------------------------------------------
        sealed class Report
        {
            readonly StringBuilder sb = new StringBuilder();
            public int Errors, Warnings;
            public void Line(string s) { sb.Append(s).Append('\n'); UnityEngine.Debug.Log("[WinBuild] " + s); }
            public void Error(string s) { Errors++; Line("ERROR " + s); }
            public void Warn(string s) { Warnings++; Line("WARN  " + s); }
            public string Text { get { return sb.ToString(); } }
        }

        static bool FromCommandLine { get { return Environment.GetCommandLineArgs().Any(a => a == "-executeMethod"); } }

        static bool Confirm(string question)
        {
            return FromCommandLine || EditorUtility.DisplayDialog("BDSP Windows build", question, "Go", "Cancel");
        }

        static void Run(string step, Func<Report, bool> body) { Mac = false; Android = false; Ios = false; RunCore(step, body); }
        static void RunMac(string step, Func<Report, bool> body) { Mac = true; Android = false; Ios = false; RunCore("mac_" + step, body); }
        static void RunAndroid(string step, Func<Report, bool> body) { Mac = false; Android = true; Ios = false; RunCore("android_" + step, body); }
        static void RunIos(string step, Func<Report, bool> body) { Mac = false; Android = false; Ios = true; RunCore("ios_" + step, body); }

        static void RunCore(string step, Func<Report, bool> body)
        {
            var r = new Report();
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            r.Line(step + " " + stamp + " (Unity " + Application.unityVersion + ", project " + Project + ")");
            bool ok;
            try
            {
                ok = body(r) && r.Errors == 0;
            }
            catch (Exception e)
            {
                r.Error("exception: " + e);
                ok = false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            r.Line(string.Format("errors {0}, warnings {1}", r.Errors, r.Warnings));
            Directory.CreateDirectory(Reports);
            string path = Path.Combine(Reports, step + "_" + stamp + ".txt");
            File.WriteAllText(path, r.Text, new UTF8Encoding(false));
            UnityEngine.Debug.Log("BDSP_WINBUILD_DONE " + step + " " + (ok ? "OK" : "FAILED") + " report " + path);
            if (FromCommandLine)
                EditorApplication.Exit(ok ? 0 : 1);
            else if (!ok)
                EditorUtility.DisplayDialog("BDSP Windows build", step + " FAILED - see " + path, "OK");
        }

        // ---- the plan ----------------------------------------------------------------------------------------------------------
        sealed class PlanRow
        {
            public string Project, Bundle, Variant;
            public bool Scene;
            public string[] Paths, Keys;
            public string FullName { get { return Variant.Length == 0 ? Bundle : Bundle + "." + Variant; } }
        }

        static List<PlanRow> ReadPlan(Report r, bool subset)
        {
            string path = Plan(subset);
            var rows = new List<PlanRow>();
            if (!File.Exists(path))
            {
                r.Error("no plan " + path + " - run: py -3.13 tools/winbuild/winbuild_plan.py" + (subset ? " --subset <names>" : ""));
                return rows;
            }
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            var head = lines.Length > 0 ? lines[0].Split('\t') : new string[0];
            if (head.Length < 3 || head[0] != "#bdsp-winbuild-plan" || head[1] != "1")
            {
                r.Error("not a plan file: " + path);
                return rows;
            }
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                    continue;
                var c = lines[i].Split('\t');
                int n = int.Parse(c[4]);
                if (c.Length != 5 + 2 * n)
                {
                    r.Error("plan line " + (i + 1) + ": " + c.Length + " cells for " + n + " assets");
                    continue;
                }
                var row = new PlanRow { Project = c[0], Bundle = c[1], Variant = c[2], Scene = c[3] == "1", Paths = new string[n], Keys = new string[n] };
                for (int k = 0; k < n; k++)
                {
                    row.Paths[k] = c[5 + 2 * k];
                    row.Keys[k] = c[6 + 2 * k];
                }
                rows.Add(row);
            }
            if (rows.Count != int.Parse(head[2]))
                r.Error("plan says " + head[2] + " rows, read " + rows.Count);
            r.Line(string.Format("plan {0}: {1} bundles, {2} assets", Path.GetFileName(path), rows.Count, rows.Sum(x => x.Paths.Length)));
            return rows;
        }

        // the bundles of the second build call: some '/'-prefix of the name is itself a bundle name (s39, see SideOut)
        static HashSet<string> SideNames(IEnumerable<PlanRow> rows)
        {
            var names = new HashSet<string>(rows.Select(x => x.FullName), StringComparer.OrdinalIgnoreCase);
            var side = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in names)
            {
                var parts = n.Split('/');
                for (int i = 1; i < parts.Length; i++)
                    if (names.Contains(string.Join("/", parts, 0, i)))
                    {
                        side.Add(n);
                        break;
                    }
            }
            return side;
        }

        static AssetBundleDownloadManifest LoadShipped(Report r)
        {
            // the game's own loader (BinaryFormatter + IgnoreVersionBinder) on the shipped file - a live test of the player path
            var m = AssetBundleDownloadManifest.Load(ShippedManifest);
            if (m == null || m.records == null || m.recordCount != ShippedRecordCount)
            {
                r.Error("the shipped manifest did not load through AssetBundleDownloadManifest.Load: " + ShippedManifest +
                        " (records " + (m == null || m.records == null ? -1 : m.recordCount) + ", expected " + ShippedRecordCount + ")");
                return null;
            }
            r.Line(string.Format("shipped Dpr.bin: {0} records, {1} variant names, project '{2}'", m.recordCount,
                m.GetAssetBundleNamesWithVariant().Length, m.projectName));
            return m;
        }

        // ---- 1. check ----------------------------------------------------------------------------------------------------------
        static bool CheckCore(Report r, bool subset, bool forPlayer)
        {
            var shipped = LoadShipped(r);
            var rows = ReadPlan(r, subset);
            if (shipped == null || rows.Count == 0)
                return false;

            // plan vs the shipped manifest
            foreach (var row in rows)
            {
                var rec = shipped.GetAssetBundleRecord(row.FullName);
                if (rec == null)
                    r.Error("plan bundle not in the shipped manifest: " + row.FullName);
                else if (rec.projectName != row.Project)
                    r.Error("plan project " + row.Project + " != shipped " + rec.projectName + " for " + row.FullName);
                else if (rec.isStreamingSceneAssetBundle != row.Scene)
                    r.Error("scene flag differs for " + row.FullName);
            }

            // the second build call (s39): only bundles with no dependencies and no dependents in the shipped graph
            var sideNames = SideNames(rows);
            foreach (var n in sideNames.OrderBy(x => x, StringComparer.Ordinal))
            {
                var rec = shipped.GetAssetBundleRecord(n);
                var deps = rec == null || rec.allDependencies == null ? new string[0] : rec.allDependencies;
                var users = shipped.records.Where(x => x.allDependencies != null && x.allDependencies.Contains(n, StringComparer.OrdinalIgnoreCase))
                    .Select(x => x.assetBundleName).ToArray();
                if (deps.Length > 0 || users.Length > 0)
                    r.Error("bundle " + n + " must be built by a second call (its name is under another bundle's name) but has dependencies [" +
                            string.Join(",", deps) + "] / dependents [" + string.Join(",", users) + "]");
            }
            if (sideNames.Count > 0)
                r.Line("second build call into " + SideOut(subset) + " (name under another bundle's name; no dependencies either way): " +
                       string.Join(", ", sideNames.OrderBy(x => x, StringComparer.Ordinal).ToArray()));

            // every asset is a project asset of the right kind, in one bundle only
            var owner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int i = 0, total = rows.Sum(x => x.Paths.Length);
            foreach (var row in rows)
            {
                if (!FromCommandLine && (i % 500 == 0))
                    EditorUtility.DisplayProgressBar("BDSP Windows build", "checking " + row.FullName, (float)i / total);
                foreach (var p in row.Paths)
                {
                    i++;
                    if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(p)))
                    {
                        r.Error("not an imported asset: " + p + " (" + row.FullName + ")");
                        continue;
                    }
                    var t = AssetDatabase.GetMainAssetTypeAtPath(p);
                    if (t == null)
                        r.Error("asset without a main object: " + p);
                    else if ((t == typeof(SceneAsset)) != row.Scene)
                        r.Error("scene / asset mix: " + p + " (" + t.Name + ") in " + row.FullName);
                    string prev;
                    if (owner.TryGetValue(p, out prev) && prev != row.FullName)
                        r.Error("asset in two bundles: " + p + " -> " + prev + ", " + row.FullName);
                    owner[p] = row.FullName;
                }
            }
            EditorUtility.ClearProgressBar();

            // variant groups: Unity swaps members by asset name
            foreach (var g in rows.Where(x => x.Variant.Length > 0).GroupBy(x => x.Bundle))
            {
                var sets = g.Select(x => string.Join("|", x.Paths.Select(p => Path.GetFileName(p).ToLowerInvariant()).OrderBy(s => s, StringComparer.Ordinal))).Distinct().Count();
                if (sets != 1)
                    r.Error("variant group " + g.Key + ": members differ in asset names");
            }
            r.Line(string.Format("assets checked {0}, variant groups {1}, scene bundles {2}", i,
                rows.Where(x => x.Variant.Length > 0).Select(x => x.Bundle).Distinct().Count(), rows.Count(x => x.Scene)));

            // the build environment
            var active = EditorUserBuildSettings.activeBuildTarget;
            if (active != Target)
                r.Error("active build target is " + active + ", not " + Target + " - switching would re-import every asset; switch it deliberately first");
            else
                r.Line("active build target " + Target);
            var drive = BuildDrive;
            r.Line(string.Format("free on {0}: {1:F1} GiB (full bundles ~7 GiB + Library growth; player ~1.2 GiB)", drive.Name, drive.AvailableFreeSpace / (double)GiB));
            r.Line("StartupSettings _assetBundleTarget: " + ReadStartupField("_assetBundleTarget") + "  URI: '" + ReadStartupField("_assetBundleTargetURI") + "'");
            r.Line("Wwise CopySoundBanksAsPreBuildStep: " + AkWwiseEditorSettings.Instance.CopySoundBanksAsPreBuildStep + " (the build turns it off for its own run)");
            r.Line("sound banks in " + BanksDir + ": " + CountFiles(Path.Combine(Project, BanksDir)));
            r.Line("Switch bundles in " + SwitchBundles + ": " + (Directory.Exists(Path.Combine(Project, SwitchBundles)) ? CountFiles(Path.Combine(Project, SwitchBundles)).ToString() : "MISSING") +
                   (Directory.Exists(SwitchAside) ? "  - AND an aside copy exists at " + SwitchAside + " (run Recover)" : ""));
            if (!File.Exists(Path.Combine(Project, BootScene)))
                r.Error("boot scene missing: " + BootScene);
            r.Line(string.Format("player settings now: backend {0}, C++ {1}, stripping {2}, engine stripping {3}, {4} graphics APIs {5} (default {6}); " +
                (Mac ? "the player build uses Mono + managed stripping Disabled + engine stripping off"
                     : "the player build uses IL2CPP Release + stripping Low (link.xml) + engine stripping off + Direct3D11"),
                PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone), PlayerSettings.GetIl2CppCompilerConfiguration(BuildTargetGroup.Standalone),
                PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.Standalone), PlayerSettings.stripEngineCode, Target,
                string.Join(",", PlayerSettings.GetGraphicsAPIs(Target).Select(x => x.ToString()).ToArray()),
                PlayerSettings.GetUseDefaultGraphicsAPIs(Target)));
            r.Line("scripting defines (Standalone): " + PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone));
            if (Android)
                return CheckAndroid(r);
            if (Ios)
                return CheckIos(r);
            if (Mac)
            {
                // Unity 6.6 installs PlaybackEngines next to Unity.app, not inside it: ask the editor instead of probing the path
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
                    r.Error("no macOS player support (BuildPipeline.IsBuildTargetSupported(Standalone, StandaloneOSX) is false)");
                r.Line("Wwise plugin config for the build: " + AkPluginActivator.GetCurrentConfig() + "; Mac PluginInfo.xml " +
                       (File.Exists(Path.Combine(Project, BanksDir, "PluginInfo.xml")) ? "present" : "MISSING (DSP plug-ins would not be activated)"));
                if (File.Exists(StateFile) && File.ReadAllText(StateFile).Contains("ACTIVE"))
                    r.Warn("STATE.txt says a build step did not finish - run Recover first:\n" + File.ReadAllText(StateFile));
                return r.Errors == 0;
            }
            foreach (var variation in new[] { "win64_nondevelopment_il2cpp", "win64_development_il2cpp" })
            {
                string v = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "windowsstandalonesupport", "Variations", variation);
                if (!Directory.Exists(v))
                    r.Error("no IL2CPP Windows player variation at " + v);
            }
            if (!File.Exists(Path.Combine(Project, "Assets", "link.xml")))
                r.Error("Assets/link.xml is missing - IL2CPP stripping would remove code the game reaches by name / from bundles");
            r.Line("C++ toolchain IL2CPP will pick: " + PickedMsvc() + " (the highest on the machine; il2cpp_smoke passed with it)");
            string cl = Environment.GetEnvironmentVariable("_CL_");
            if (cl != null && cl.Contains(":/"))
                r.Line("_CL_ in this editor is path-mangled ('" + cl + "', started through Git Bash) - the player build sets the registry value '" + RegistryClOptions() + "'");
            if (File.Exists(StateFile) && File.ReadAllText(StateFile).Contains("ACTIVE"))
                r.Warn("work/winbuild/STATE.txt says a build step did not finish - run Recover first:\n" + File.ReadAllText(StateFile));
            return r.Errors == 0;
        }

        // ---- 2/3. bundles ------------------------------------------------------------------------------------------------------
        static bool BuildBundles(Report r, bool subset)
        {
            if (!CheckCore(r, subset, false))
                return false;
            var rows = ReadPlan(r, subset);
            long need = subset ? 1 * GiB : 12 * GiB;
            var drive = BuildDrive;
            if (drive.AvailableFreeSpace < need)
            {
                r.Error(string.Format("refusing: {0:F1} GiB free on {1}, the build needs >= {2} GiB", drive.AvailableFreeSpace / (double)GiB, drive.Name, need / GiB));
                return false;
            }
            string outDir = BundleOut(subset), sideDir = SideOut(subset);
            Directory.CreateDirectory(outDir);
            var sideNames = SideNames(rows);
            var shadersRow = rows.FirstOrDefault(x => x.FullName == ShadersBundle);
            if (shadersRow != null && !WriteShippedVariants(r, shadersRow))
                return false;
            // the legacy incremental build does not see a GraphicsSettings change (instancing below) or a regenerated collection with
            // the same content - the shaders bundle (~1.6 MB) is always rebuilt: its previous output is moved aside first
            if (shadersRow != null)
            {
                string stale = Path.Combine(BuildRoot, "stale", Path.GetFileName(outDir) + "_" + ShadersBundle + "_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture));
                foreach (var f in new[] { ShadersBundle, ShadersBundle + ".manifest" })
                    if (File.Exists(Path.Combine(outDir, f)))
                    {
                        Directory.CreateDirectory(stale);
                        File.Move(Path.Combine(outDir, f), Path.Combine(stale, f));
                    }
                if (Directory.Exists(stale))
                    r.Line("previous shaders bundle moved aside to " + stale + " (forces its rebuild)");
            }
            // Unity 6: every sprite atlas's bundle also gets the atlas's sprite list (BdspWinBuild.AtlasIndex.cs)
            if (!AddAtlasIndexes(r, rows))
                return false;
            Func<IEnumerable<PlanRow>, AssetBundleBuild[]> toBuilds = list => list.Select(x => new AssetBundleBuild
            {
                assetBundleName = x.Bundle,
                assetBundleVariant = x.Variant.Length == 0 ? null : x.Variant,
                assetNames = x == shadersRow ? x.Paths.Concat(new[] { VariantsAsset }).ToArray() : x.Paths,
                addressableNames = x.Scene ? null : x == shadersRow ? x.Keys.Concat(new[] { VariantsKey }).ToArray() : x.Keys,  // scenes register by path (SceneDatabase._scenePath)
            }).ToArray();
            var builds = toBuilds(rows.Where(x => !sideNames.Contains(x.FullName)));
            var sideBuilds = toBuilds(rows.Where(x => sideNames.Contains(x.FullName)));
            // ChunkBasedCompression = LZ4 chunks as shipped (UnityFS flags 0x43). The SUBSET runs StrictMode (fail fast); the FULL build
            // does not - one bad variant must not throw hours away - and instead every error logged during the build is collected
            // and reported (the step fails if there is any).
            var options = BuildAssetBundleOptions.ChunkBasedCompression | (subset ? BuildAssetBundleOptions.StrictMode : BuildAssetBundleOptions.None);
            r.Line(string.Format("BuildAssetBundles -> {0} ({1} bundles, {2}, {3})", outDir, builds.Length, options, Target));
            var logged = new List<string>();
            int loggedErrors = 0;
            Application.LogCallback grab = (msg, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    lock (logged) { loggedErrors++; if (logged.Count < 200) logged.Add(type + ": " + msg); }
            };
            var t0 = DateTime.Now;
            // each call's result is tested RIGHT AFTER it: the second call unloads unused assets, which destroys the first call's
            // AssetBundleManifest object (it then compares == null - measured s39). The Dpr.bin is written from the manifests re-read
            // from disk (RewriteManifest).
            bool mainOk, sideOk = true;
            // s39: GraphicsSettings "Instancing Variants" = Keep All FOR THE BUILD ONLY. Strip Unused keeps INSTANCING_ON only when a
            // material in the shader's own bundle enables instancing - none in `shaders` -> every instancing variant was stripped
            // (measured: Map/Standard 1,292 forward variants vs 2,562 shipped, the missing 1,280 = all INSTANCING_ON). Shipped has them.
            string graphicsPath = Path.Combine(Project, "ProjectSettings", "GraphicsSettings.asset");
            byte[] graphicsBytes = File.ReadAllBytes(graphicsPath);   // written back after the build: Unity re-serializes the file
            var graphics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
            var graphicsSo = graphics == null ? null : new SerializedObject(graphics);
            var instancing = graphicsSo == null ? null : graphicsSo.FindProperty("m_InstancingStripping");
            int prevInstancing = instancing == null ? -1 : instancing.intValue;
            if (instancing == null)
                r.Warn("GraphicsSettings m_InstancingStripping not found - instancing variants follow the project setting");
            else if (prevInstancing != 2)
            {
                instancing.intValue = 2;   // InstancingKeepAll
                graphicsSo.ApplyModifiedPropertiesWithoutUndo();
                r.Line("GraphicsSettings instancing stripping " + prevInstancing + " -> 2 (Keep All) for the build");
            }
            string projectSettingsPath = Path.Combine(Project, "ProjectSettings", "ProjectSettings.asset");
            byte[] projectSettingsBytes = File.ReadAllBytes(projectSettingsPath);
            var prevBackend = Mac ? UseMono(r) : PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
            var metas = Mac || Android || Ios ? SnapshotPluginMetas() : null;
            if (Android)
                ApplyAndroidPlayerSettings(r, false);
            if (Ios)
                ApplyIosPlayerSettings(r, false);
            Application.logMessageReceivedThreaded += grab;
            try
            {
                if (Mac)
                    ActivateWwiseForMac(r);
                mainOk = BuildPipeline.BuildAssetBundles(outDir, builds, options, Target) != null;
                if (sideBuilds.Length > 0)
                {
                    Directory.CreateDirectory(sideDir);
                    r.Line(string.Format("second BuildAssetBundles -> {0} ({1} bundles: {2})", sideDir, sideBuilds.Length,
                        string.Join(", ", sideBuilds.Select(x => x.assetBundleName).ToArray())));
                    var sideManifest = BuildPipeline.BuildAssetBundles(sideDir, sideBuilds, options, Target);
                    sideOk = sideManifest != null;
                    if (sideOk)
                        foreach (var n in sideManifest.GetAllAssetBundles())
                            if (sideManifest.GetAllDependencies(n).Length > 0)
                                r.Error("second-call bundle " + n + " came out WITH dependencies: " + string.Join(",", sideManifest.GetAllDependencies(n)));
                }
            }
            finally
            {
                Application.logMessageReceivedThreaded -= grab;
                if (Mac || Android || Ios)
                {
                    RestoreBackend(r, prevBackend, projectSettingsPath, projectSettingsBytes);   // saves assets: BEFORE the metas
                    RestorePluginMetas(r, metas);
                }
                if (instancing != null && prevInstancing != 2)
                {
                    instancing.intValue = prevInstancing;
                    graphicsSo.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                    // same values, but Unity's re-serialization reorders / re-wraps lines: write the original bytes back (nothing is
                    // dirty any more, so the editor does not save it again)
                    bool same = File.ReadAllBytes(graphicsPath).SequenceEqual(graphicsBytes);
                    if (!same)
                        File.WriteAllBytes(graphicsPath, graphicsBytes);
                    r.Line("GraphicsSettings instancing stripping restored -> " + prevInstancing + (same ? "" : " (file bytes written back)"));
                }
            }
            r.Line(string.Format("BuildAssetBundles took {0:F1} min; errors logged during the build: {1}", (DateTime.Now - t0).TotalMinutes, loggedErrors));
            foreach (var e in logged)
                r.Error("build log " + e);
            if (!mainOk || !sideOk)
            {
                r.Error("BuildAssetBundles returned null (" + (mainOk ? "second call" : "main call") + "; see the Console / Editor.log for the failing asset)");
                return false;
            }
            long bytes = 0;
            int missing = 0;
            foreach (var x in rows)
            {
                var f = new FileInfo(sideNames.Contains(x.FullName) ? Path.Combine(sideDir, x.FullName) : Path.Combine(outDir, x.FullName));
                if (f.Exists) bytes += f.Length; else { missing++; if (missing <= 20) r.Error("bundle not written: " + x.FullName); }
            }
            r.Line(string.Format("bundles written {0}/{1}, {2:F2} GiB", rows.Count - missing, rows.Count, bytes / (double)GiB));
            return missing == 0 && r.Errors == 0 && RewriteManifest(r, subset);
        }

        static PassType PassTypeOf(string lightMode)
        {
            switch (lightMode)
            {
                case "": case "ALWAYS": return PassType.Normal;
                case "VERTEX": return PassType.Vertex;
                case "VERTEXLM": return PassType.VertexLM;
                case "FORWARDBASE": return PassType.ForwardBase;
                case "FORWARDADD": return PassType.ForwardAdd;
                case "PREPASSBASE": return PassType.LightPrePassBase;
                case "PREPASSFINAL": return PassType.LightPrePassFinal;
                case "SHADOWCASTER": return PassType.ShadowCaster;
                case "DEFERRED": return PassType.Deferred;
                case "META": return PassType.Meta;
                case "MOTIONVECTORS": return PassType.MotionVectors;
                case "SRPDEFAULTUNLIT": return PassType.ScriptableRenderPipelineDefaultUnlit;
                default: return PassType.ScriptableRenderPipeline;
            }
        }

        // the shipped (shader, LightMode, keywords) list -> VariantsAsset; a row the project shader cannot express is reported
        static bool WriteShippedVariants(Report r, PlanRow shadersRow)
        {
            if (!File.Exists(VariantsTsv))
            {
                r.Error("no " + VariantsTsv + " - run: py -3.13 -I tools/winbuild/shader_variants_shipped.py");
                return false;
            }
            var byName = new Dictionary<string, Shader>();
            foreach (var p in shadersRow.Paths)
            {
                var s = AssetDatabase.LoadAssetAtPath<Shader>(p);
                if (s != null)
                    byName[s.name] = s;
            }
            var svc = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(VariantsAsset);
            bool create = svc == null;
            if (create)
                svc = new ShaderVariantCollection();
            else
                svc.Clear();
            int rows = 0, added = 0, invalid = 0, noShader = 0;
            foreach (var line in File.ReadAllLines(VariantsTsv, Encoding.UTF8).Skip(1))
            {
                var c = line.Split('\t');
                if (c.Length < 3)
                    continue;
                rows++;
                Shader shader;
                if (!byName.TryGetValue(c[0], out shader))
                {
                    if (++noShader <= 10)
                        r.Warn("shipped variant of a shader the shaders bundle does not hold: " + c[0]);
                    continue;
                }
                try
                {
                    if (svc.Add(new ShaderVariantCollection.ShaderVariant(shader, PassTypeOf(c[1]), c[2].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))))
                        added++;
                }
                catch (Exception e)
                {
                    if (++invalid <= 20)
                        r.Warn("shipped variant the project shader cannot express: " + c[0] + " / " + (c[1].Length == 0 ? "-" : c[1]) + " / [" + c[2] + "]: " + e.Message);
                }
            }
            if (create)
                AssetDatabase.CreateAsset(svc, VariantsAsset);
            else
                EditorUtility.SetDirty(svc);
            AssetDatabase.SaveAssets();
            r.Line(string.Format("shipped shader variants -> {0}: {1} rows, {2} added ({3} shaders, {4} variants in the collection), not expressible {5}, shader missing {6}",
                VariantsAsset, rows, added, svc.shaderCount, svc.variantCount, invalid, noShader));
            if (added == 0)
                r.Error("the shipped variant collection is empty");
            return added > 0;
        }

        static bool RewriteManifest(Report r, bool subset)
        {
            string outDir = BundleOut(subset);
            string mb = Path.Combine(outDir, Path.GetFileName(outDir));
            if (!File.Exists(mb))
            {
                r.Error("no bundle build at " + outDir);
                return false;
            }
            var ab = AssetBundle.LoadFromFile(mb);
            if (ab == null)
            {
                r.Error("cannot load " + mb);
                return false;
            }
            string sideMb = Path.Combine(SideOut(subset), Path.GetFileName(SideOut(subset)));
            var sideAb = File.Exists(sideMb) ? AssetBundle.LoadFromFile(sideMb) : null;
            try
            {
                var m = ab.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                var sm = sideAb == null ? null : sideAb.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                return m != null && WriteManifest(r, subset, m, sm);
            }
            finally
            {
                ab.Unload(false);
                if (sideAb != null)
                    sideAb.Unload(false);
            }
        }

        static readonly FieldInfo F_records = typeof(AssetBundleDownloadManifest).GetField("_records", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo F_variants = typeof(AssetBundleDownloadManifest).GetField("_assetBundleNamesWithVariant", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly MethodInfo M_lookup = typeof(AssetBundleDownloadManifest).GetMethod("BuildLookupTables", BindingFlags.NonPublic | BindingFlags.Instance);

        static bool WriteManifest(Report r, bool subset, AssetBundleManifest unityManifest, AssetBundleManifest sideManifest)
        {
            if (F_records == null || F_variants == null || M_lookup == null)
            {
                r.Error("AssetBundleDownloadManifest members changed (_records / _assetBundleNamesWithVariant / BuildLookupTables)");
                return false;
            }
            var shipped = LoadShipped(r);
            if (shipped == null)
                return false;
            string outDir = BundleOut(subset);
            var unknown = new List<string>();
            // the developers' tool path: records from Unity's manifest (hash, size, lastWriteTime, allDependencies of the WINDOWS build)
            AssetBundleDownloadManifest.OnRecordCreated fill = rec =>
            {
                var s = shipped.GetAssetBundleRecord(rec.assetBundleName);
                if (s == null)
                {
                    unknown.Add(rec.assetBundleName);
                    return;
                }
                rec.projectName = s.projectName;
                rec.assetPaths = s.assetPaths;
                rec.isStreamingSceneAssetBundle = s.isStreamingSceneAssetBundle;
            };
            var built = AssetBundleDownloadManifest.Load(outDir, unityManifest, fill);
            var allBuilt = built.records.ToList();
            if (sideManifest != null)   // the second build call (s39): its records from its own manifest + folder
                allBuilt.AddRange(AssetBundleDownloadManifest.Load(SideOut(subset), sideManifest, fill).records);
            foreach (var u in unknown)
                r.Error("built bundle not in the shipped manifest: " + u);

            // shipped order; shipped records without a build are carried unchanged (copy ctor: no editor proxy involved)
            var byName = allBuilt.ToDictionary(x => x.assetBundleName);
            var ordered = new List<AssetBundleRecord>(shipped.recordCount);
            var carried = new List<string>();
            foreach (var s in shipped.records)
            {
                AssetBundleRecord b;
                if (byName.TryGetValue(s.assetBundleName, out b))
                {
                    ordered.Add(b);
                    byName.Remove(s.assetBundleName);
                }
                else
                {
                    ordered.Add(new AssetBundleRecord(s));
                    carried.Add(s.assetBundleName);
                }
            }
            F_records.SetValue(built, ordered.ToArray());
            var builtVariants = unityManifest.GetAllAssetBundlesWithVariant();
            var shippedVariants = shipped.GetAssetBundleNamesWithVariant();
            if (!subset && (builtVariants.Length != shippedVariants.Length || builtVariants.Except(shippedVariants).Any()))
                r.Error(string.Format("variant bundles built {0} != shipped {1}", builtVariants.Length, shippedVariants.Length));
            F_variants.SetValue(built, shippedVariants.ToArray());
            built.projectName = shipped.projectName;
            M_lookup.Invoke(built, null);
            r.Line(string.Format("records: {0} from this build, {1} carried from the shipped manifest{2}", ordered.Count - carried.Count, carried.Count,
                carried.Count <= 12 ? " (" + string.Join(", ", carried.ToArray()) + ")" : ""));

            string path = ManifestOut(subset);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path))
            {
                string aside = path + "." + File.GetLastWriteTime(path).ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
                File.Move(path, aside);
                r.Line("previous Dpr.bin moved to " + aside);
            }
            built.Save(path, true);
            return VerifyManifest(r, path, shipped, outDir, subset);
        }

        static bool VerifyManifest(Report r, string path, AssetBundleDownloadManifest shipped, string outDir, bool subset)
        {
            var back = AssetBundleDownloadManifest.Load(path);   // the game's own loader, as the player will read it
            if (back == null || back.records == null || back.recordCount != shipped.recordCount)
            {
                r.Error("the written Dpr.bin did not read back with " + shipped.recordCount + " records");
                return false;
            }
            int depDiff = 0, sizeBad = 0, unresolved = 0;
            var examples = new List<string>();
            for (int i = 0; i < back.recordCount; i++)
            {
                var b = back.records[i];
                var s = shipped.records[i];
                if (b.assetBundleName != s.assetBundleName || b.projectName != s.projectName || b.isStreamingSceneAssetBundle != s.isStreamingSceneAssetBundle
                    || !(b.assetPaths ?? new string[0]).SequenceEqual(s.assetPaths ?? new string[0]))
                    r.Error("record " + i + " differs from the shipped one in name / project / scene / assetPaths: " + b.assetBundleName);
                foreach (var d in b.allDependencies ?? new string[0])
                    if (back.GetAssetBundleRecord(d) == null) { unresolved++; if (unresolved <= 10) r.Error("unresolved dependency " + b.assetBundleName + " -> " + d); }
                var f = new FileInfo(BuiltFile(subset, b.assetBundleName));
                if (f.Exists && f.Length != b.size) sizeBad++;
                var bd = (b.allDependencies ?? new string[0]).OrderBy(x => x, StringComparer.Ordinal);
                var sd = (s.allDependencies ?? new string[0]).OrderBy(x => x, StringComparer.Ordinal);
                if (f.Exists && !bd.SequenceEqual(sd))
                {
                    depDiff++;
                    if (examples.Count < 25)
                        examples.Add(b.assetBundleName + ": " + Plat + " [" + string.Join(",", bd.ToArray()) + "] shipped [" + string.Join(",", sd.ToArray()) + "]");
                }
            }
            if (sizeBad > 0)
                r.Error(sizeBad + " records whose size != the file");
            if (!back.GetAssetBundleNamesWithVariant().SequenceEqual(shipped.GetAssetBundleNamesWithVariant()))
                r.Error("variant list differs from the shipped one");
            // fidelity signal, not an error: the Windows dependency graph vs the shipped (Switch) one
            r.Line(string.Format("Dpr.bin verified: {0} records, variants as shipped, dependency lists differing from the shipped graph: {1}{2}",
                back.recordCount, depDiff, subset ? " (subset: carried records keep shipped deps)" : ""));
            foreach (var e in examples)
                r.Line("  dep " + e);
            r.Line("Dpr.bin -> " + path + " (" + new FileInfo(path).Length + " bytes)");
            return r.Errors == 0;
        }

        // ---- hard-link layout --------------------------------------------------------------------------------------------------
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        // <dir>/<projectName>/<bundle> for every record whose bundle was built, + <dir>/Dpr.bin. The directory must be NEW: nothing in
        // it is ever replaced (a stale link would keep an old bundle).
        static bool LinkLayout(Report r, string dir, bool subset, string[] editorBundles, string editorDir)
        {
            string manifest = ManifestOut(subset);
            if (!File.Exists(manifest))
            {
                r.Error("no Dpr.bin at " + manifest + " - build the bundles first");
                return false;
            }
            var m = AssetBundleDownloadManifest.Load(manifest);
            if (m == null || m.records == null || m.recordCount == 0)
            {
                r.Error("Dpr.bin unreadable: " + manifest);
                return false;
            }
            if (Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any())
            {
                r.Error("layout target is not empty: " + dir);
                return false;
            }
            int links = 0, copies = 0, absent = 0;
            foreach (var rec in m.records)
            {
                string src = BuiltFile(subset, rec.assetBundleName);   // main or second-call folder (s39)
                if (!File.Exists(src)) { absent++; continue; }
                links += Link(src, Path.Combine(dir, rec.projectName, rec.assetBundleName), ref copies);
                if (editorBundles != null && editorBundles.Contains(rec.assetBundleName))
                    links += Link(src, Path.Combine(editorDir, rec.assetBundleName), ref copies);
            }
            File.Copy(manifest, Path.Combine(dir, ManifestFileName));
            r.Line(string.Format("layout {0}: {1} hard links, {2} copies (link failed), {3} records without a built file (carried / subset), + {4}",
                dir, links, copies, absent, ManifestFileName));
            return true;
        }

        // macOS: an APFS clone instead of a hard link - no extra disk either, but the player keeps its own file when a later
        // incremental bundle build rewrites bundles_mac/ (no unshare step needed)
        [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "clonefile", SetLastError = true)]
        static extern int clonefile(string src, string dst, uint flags);

        static int Link(string src, string dst, ref int copies)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            if (Mac || Android || Ios ? clonefile(src, dst, 0) == 0 : CreateHardLink(dst, src, IntPtr.Zero))
                return 1;
            File.Copy(src, dst);
            copies++;
            return 0;
        }

        // ---- 5/6. player -------------------------------------------------------------------------------------------------------
        static bool BuildPlayer(Report r, bool development, bool subset)
        {
            if (!CheckCore(r, subset, true))
                return false;
            if (!File.Exists(ManifestOut(subset)))
            {
                r.Error("no Windows Dpr.bin (" + ManifestOut(subset) + ") - build the bundles first");
                return false;
            }
            // the legacy pipeline's text manifest of the whole output folder (every bundle's ClassTypes): with it the player build's
            // stripping keeps the engine / managed classes that only the bundles use (BuildPlayerOptions.assetBundleManifestPath)
            string bundleManifest = Path.Combine(BundleOut(subset), Path.GetFileName(BundleOut(subset)) + ".manifest");
            if (!File.Exists(bundleManifest))
            {
                r.Error("no bundle build manifest " + bundleManifest + " - build the bundles first");
                return false;
            }
            var drive = BuildDrive;
            if (drive.AvailableFreeSpace < 10 * GiB)
            {
                r.Error(string.Format("refusing: {0:F1} GiB free on {1}, an IL2CPP player build needs >= 10 GiB (generated C++, object cache, player)",
                    drive.AvailableFreeSpace / (double)GiB, drive.Name));
                return false;
            }
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string outDir = Path.Combine(BuildRoot, "win64" + Flavour + "_" + stamp + (subset ? "_subset" : ""));
            if (Directory.Exists(outDir))
            {
                r.Error("output exists: " + outDir);
                return false;
            }
            string exe = Path.Combine(outDir, "Dpr.exe");
            string dataDir = Path.Combine(outDir, "Dpr_Data");
            string il2cppTemp = Path.Combine(Work, "il2cpp_tmp");
            Directory.CreateDirectory(il2cppTemp);
            int banksBefore = CountFiles(Path.Combine(Project, BanksDir));

            // remember everything this step changes
            string bdir = Path.Combine(Backups, "player_" + stamp);
            Directory.CreateDirectory(bdir);
            byte[] startupBytes = File.ReadAllBytes(Path.Combine(Project, StartupAsset));
            File.WriteAllBytes(Path.Combine(bdir, "StartupSettings.asset"), startupBytes);
            File.Copy(Path.Combine(Project, "ProjectSettings", "ProjectSettings.asset"), Path.Combine(bdir, "ProjectSettings.asset"));
            var prevBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
            var prevCppConfig = PlayerSettings.GetIl2CppCompilerConfiguration(BuildTargetGroup.Standalone);
            var prevStripLevel = PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.Standalone);
            bool prevStripEngine = PlayerSettings.stripEngineCode;
            bool prevDefaultApis = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64);
            var prevApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            bool prevWwiseCopy = AkWwiseEditorSettings.Instance.CopySoundBanksAsPreBuildStep;
            var envNames = new[] { "IL2CPP_CUSTOM_TEMP_DIR", "TMP", "TEMP", "_CL_" };
            var prevEnv = envNames.ToDictionary(n => n, n => Environment.GetEnvironmentVariable(n));
            // Wwise's AkPluginActivator re-flags its plugin importers for every build and does not put all of them back (measured s39:
            // 24 plugin .meta files left changed) - each one is written back byte for byte afterwards
            string pluginDir = Path.Combine(Project, "Assets", "Wwise", "Deployment", "Plugins");
            var pluginMetas = Directory.Exists(pluginDir)
                ? Directory.GetFiles(pluginDir, "*.meta", SearchOption.AllDirectories).ToDictionary(p => p, p => File.ReadAllBytes(p))
                : new Dictionary<string, byte[]>();
            // set only what differs: setting a value equal to the default still writes an explicit entry into ProjectSettings.asset
            bool cppChanged = prevCppConfig != Il2CppCompilerConfiguration.Release, stripChanged = prevStripLevel != ManagedStrippingLevel.Low;
            bool apisChanged = false, movedAside = false;
            WriteState("ACTIVE player build " + stamp + "\nstartup_backup=" + Path.Combine(bdir, "StartupSettings.asset") + "\n");

            bool ok = false;
            try
            {
                // IL2CPP as the project is set (author 2026-10-12: "build it using IL2CPP, not Mono"); Release C++; managed stripping
                // Low (the IL2CPP minimum) with Assets/link.xml keeping the game + package assemblies whole; engine code stripping
                // OFF for this run - a class only a bundle uses must never be missing at runtime ("Could not produce class")
                if (prevBackend != ScriptingImplementation.IL2CPP)
                    PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
                if (cppChanged)
                    PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Standalone, Il2CppCompilerConfiguration.Release);
                if (stripChanged)
                    PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Low);
                PlayerSettings.stripEngineCode = false;
                if (!(prevApis.Length == 1 && prevApis[0] == GraphicsDeviceType.Direct3D11))
                {
                    PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
                    PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
                    apisChanged = true;
                }
                // il2cpp.exe and cl.exe inherit this process's environment: their temp files on E: (C: is nearly full), and _CL_ as the
                // registry holds it - an editor started through Git Bash inherits it path-mangled ("/D..." -> "C:/Program Files/Git/D...")
                Environment.SetEnvironmentVariable("IL2CPP_CUSTOM_TEMP_DIR", il2cppTemp);
                Environment.SetEnvironmentVariable("TMP", il2cppTemp);
                Environment.SetEnvironmentVariable("TEMP", il2cppTemp);
                Environment.SetEnvironmentVariable("_CL_", RegistryClOptions());
                r.Line("C++ toolchain IL2CPP will pick: " + PickedMsvc() + "; _CL_ for the build '" + RegistryClOptions() + "'");
                AkWwiseEditorSettings.Instance.CopySoundBanksAsPreBuildStep = false;   // in memory only (never saved)
                SetStartupFields(3, null);                                             // StreamingAssets, as shipped
                movedAside = MoveSwitchBundlesAside(r);
                if (!movedAside)
                    return false;

                var opts = new BuildPlayerOptions
                {
                    scenes = new[] { BootScene },
                    locationPathName = exe,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                    assetBundleManifestPath = bundleManifest,
                };
                r.Line(string.Format("BuildPlayer -> {0} ({1}, IL2CPP Release, stripping Low + link.xml, engine stripping off, Direct3D11, scenes [{2}], bundle manifest {3})",
                    exe, development ? "development" : "release", BootScene, bundleManifest));
                BuildReport rep = BuildPipeline.BuildPlayer(opts);
                var sum = rep.summary;
                r.Line(string.Format("result {0}, {1:F1} min, size {2:F2} GiB, errors {3}, warnings {4}", sum.result, sum.totalTime.TotalMinutes,
                    sum.totalSize / (double)GiB, sum.totalErrors, sum.totalWarnings));
                int shown = 0;
                foreach (var st in rep.steps)
                    foreach (var msg in st.messages)
                        if ((msg.type == LogType.Error || msg.type == LogType.Exception) && shown++ < 40)
                            r.Line("  build " + msg.type + ": " + msg.content);
                if (sum.result != BuildResult.Succeeded)
                {
                    r.Error("BuildPlayer did not succeed");
                    return false;
                }
                ok = LinkLayout(r, Path.Combine(dataDir, "StreamingAssets", "AssetAssistant"), subset, null, null);
                int playerBanks = CountFiles(Path.Combine(dataDir, "StreamingAssets", "Audio", "GeneratedSoundBanks", "Windows"));
                if (playerBanks != banksBefore)
                    r.Error("player sound banks " + playerBanks + " != project " + banksBefore);
                if (!File.Exists(Path.Combine(outDir, "GameAssembly.dll")))
                    r.Error("player is missing GameAssembly.dll (the IL2CPP code)");
                foreach (var need in new[] { "il2cpp_data/Metadata/global-metadata.dat", "level0", "resources.assets", "StreamingAssets/Movie" })
                    if (!File.Exists(Path.Combine(dataDir, need)) && !Directory.Exists(Path.Combine(dataDir, need)))
                        r.Error("player is missing " + need);
                r.Line("player: " + exe);
                return ok && r.Errors == 0;
            }
            finally
            {
                if (movedAside)
                    RestoreSwitchBundles(r);
                File.WriteAllBytes(Path.Combine(Project, StartupAsset), startupBytes);
                AssetDatabase.ImportAsset(StartupAsset, ImportAssetOptions.ForceUpdate);
                AkWwiseEditorSettings.Instance.CopySoundBanksAsPreBuildStep = prevWwiseCopy;
                foreach (var kv in prevEnv)
                    Environment.SetEnvironmentVariable(kv.Key, kv.Value);
                if (apisChanged)
                {
                    PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, prevApis);
                    PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, prevDefaultApis);
                }
                PlayerSettings.stripEngineCode = prevStripEngine;
                if (stripChanged)
                    PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, prevStripLevel);
                if (cppChanged)
                    PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Standalone, prevCppConfig);
                int metasBack = 0;
                foreach (var kv in pluginMetas)
                {
                    if (File.Exists(kv.Key) && File.ReadAllBytes(kv.Key).SequenceEqual(kv.Value))
                        continue;
                    File.WriteAllBytes(kv.Key, kv.Value);
                    string asset = kv.Key.Substring(Project.Length + 1).Replace('\\', '/');
                    AssetDatabase.ImportAsset(asset.Substring(0, asset.Length - ".meta".Length), ImportAssetOptions.ForceUpdate);
                    metasBack++;
                }
                r.Line("Wwise plugin .meta files written back: " + metasBack + " of " + pluginMetas.Count);
                if (prevBackend != ScriptingImplementation.IL2CPP)
                    PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, prevBackend);
                int banksAfter = CountFiles(Path.Combine(Project, BanksDir));
                if (banksAfter != banksBefore)
                    r.Error("PROJECT sound banks changed during the build: " + banksBefore + " -> " + banksAfter + " (restore from romfs/Data/StreamingAssets/Audio/GeneratedSoundBanks/Switch)");
                bool startupSame = File.ReadAllBytes(Path.Combine(Project, StartupAsset)).SequenceEqual(startupBytes);
                if (!startupSame)
                    r.Error("StartupSettings.asset is not byte-identical to its backup " + bdir);
                r.Line("restored: StartupSettings (byte-identical " + startupSame + "), Wwise copy flag " + prevWwiseCopy + ", backend " + prevBackend +
                       ", C++ config " + prevCppConfig + ", stripping " + prevStripLevel + ", engine stripping " + prevStripEngine + ", environment" +
                       (apisChanged ? ", graphics APIs" : "") + (movedAside ? ", Switch bundles back in " + SwitchBundles : ""));
                WriteState("clean (last: player build " + stamp + ", " + (ok ? "OK" : "FAILED") + ")\n");
            }
        }

        // ---- macOS player (Mono) ------------------------------------------------------------------------------------------------
        // Builds/mac_arm64_<stamp>[_dev]/Dpr.app: level0, Mono, native ARM64 (the project's Standalone backend is IL2CPP - switched for this run only),
        // managed + engine stripping off, the default graphics API (Metal), StartupSettings target StreamingAssets (3) for the build only,
        // Wwise's pre-build bank copy OFF (its post-build step deletes the destination = our Mac banks). Unlike the Windows build the
        // project's StreamingAssets is NOT touched: Unity copies it whole, then the copied Switch bundles and Windows banks are deleted
        // from the .app and the Mac bundles are APFS-cloned into <app>/Contents/Resources/Data/StreamingAssets/AssetAssistant.
        // Wwise's AkPluginActivator re-flags its plugin importers before the build and turns them all OFF after it: every plugin .meta
        // under Assets/Wwise and Assets/Plugins is written back byte for byte in `finally` (the editor's working audio depends on them).
        static bool BuildPlayerMac(Report r, bool development)
        {
            if (!CheckCore(r, false, true))
                return false;
            if (!File.Exists(ManifestOut(false)))
            {
                r.Error("no macOS Dpr.bin (" + ManifestOut(false) + ") - build the bundles first");
                return false;
            }
            string bundleManifest = Path.Combine(BundleOut(false), Path.GetFileName(BundleOut(false)) + ".manifest");
            if (!File.Exists(bundleManifest))
            {
                r.Error("no bundle build manifest " + bundleManifest + " - build the bundles first");
                return false;
            }
            var drive = BuildDrive;
            if (drive.AvailableFreeSpace < 8 * GiB)
            {
                r.Error(string.Format("refusing: {0:F1} GiB free on {1}, the player build needs >= 8 GiB (StreamingAssets is copied whole first)",
                    drive.AvailableFreeSpace / (double)GiB, drive.Name));
                return false;
            }
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string outDir = Path.Combine(BuildRoot, "mac_arm64" + Flavour + "_" + stamp + (development ? "_dev" : ""));
            if (Directory.Exists(outDir))
            {
                r.Error("output exists: " + outDir);
                return false;
            }
            string app = Path.Combine(outDir, "Dpr.app");
            string dataDir = Path.Combine(app, "Contents", "Resources", "Data");
            int banksBefore = CountFiles(Path.Combine(Project, BanksDir));

            string bdir = Path.Combine(Backups, "mac_player_" + stamp);
            Directory.CreateDirectory(bdir);
            byte[] startupBytes = File.ReadAllBytes(Path.Combine(Project, StartupAsset));
            File.WriteAllBytes(Path.Combine(bdir, "StartupSettings.asset"), startupBytes);
            string projectSettingsPath = Path.Combine(Project, "ProjectSettings", "ProjectSettings.asset");
            byte[] projectSettingsBytes = File.ReadAllBytes(projectSettingsPath);
            File.WriteAllBytes(Path.Combine(bdir, "ProjectSettings.asset"), projectSettingsBytes);
            string graphicsPath = Path.Combine(Project, "ProjectSettings", "GraphicsSettings.asset");
            byte[] graphicsBytes = File.ReadAllBytes(graphicsPath);
            var prevBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
            var prevStripLevel = PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.Standalone);
            bool prevStripEngine = PlayerSettings.stripEngineCode;
            bool prevWwiseCopy = AkWwiseEditorSettings.Instance.CopySoundBanksAsPreBuildStep;
            var prevMacArch = UnityEditor.OSXStandalone.UserBuildSettings.architecture;
            var metas = SnapshotPluginMetas();
            r.Line("backed up: StartupSettings + ProjectSettings -> " + bdir + "; " + metas.Count + " .meta files under Assets/Wwise + Assets/Plugins (in memory)");
            bool stripChanged = prevStripLevel != ManagedStrippingLevel.Disabled;
            WriteState("ACTIVE mac player build " + stamp + "\nstartup_backup=" + Path.Combine(bdir, "StartupSettings.asset") + "\n");

            bool ok = false;
            try
            {
                if (prevBackend != ScriptingImplementation.Mono2x)
                    PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
                if (stripChanged)
                    PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Disabled);
                PlayerSettings.stripEngineCode = false;
                AkWwiseEditorSettings.Instance.CopySoundBanksAsPreBuildStep = false;   // in memory only (never saved)
                SetStartupFields(3, null);                                             // StreamingAssets, as shipped
                UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.ARM64;   // native Apple silicon player
                ActivateWwiseForMac(r);                                                // AkBuildPreprocessor repeats it

                var opts = new BuildPlayerOptions
                {
                    scenes = new[] { BootScene },
                    locationPathName = app,
                    target = BuildTarget.StandaloneOSX,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                    assetBundleManifestPath = bundleManifest,
                };
                r.Line(string.Format("BuildPlayer -> {0} ({1}, Mono, " + UnityEditor.OSXStandalone.UserBuildSettings.architecture + ", stripping off, graphics {2}, scenes [{3}], bundle manifest {4})", app,
                    development ? "development" : "release", string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneOSX).Select(x => x.ToString()).ToArray()),
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
                if (sum.result != BuildResult.Succeeded)
                {
                    r.Error("BuildPlayer did not succeed");
                    return false;
                }
                // the copied project StreamingAssets: Switch bundles + the other platform's banks out, the Mac bundles in
                string sa = Path.Combine(dataDir, "StreamingAssets");
                foreach (var gone in new[] { Path.Combine(sa, "AssetAssistant"), Path.Combine(sa, "Audio", "GeneratedSoundBanks", "Windows") })
                    if (Directory.Exists(gone))
                    {
                        Directory.Delete(gone, true);
                        r.Line("removed from the player: " + gone.Substring(app.Length + 1));
                    }
                ok = LinkLayout(r, Path.Combine(sa, "AssetAssistant"), false, null, null);
                int playerBanks = CountFiles(Path.Combine(sa, "Audio", "GeneratedSoundBanks", "Mac"));
                if (playerBanks != banksBefore)
                    r.Error("player sound banks " + playerBanks + " != project " + banksBefore);
                else
                    r.Line("player sound banks (Mac): " + playerBanks + " files, as in the project");
                foreach (var need in new[] { "Contents/MacOS/Dpr", "Contents/Resources/Data/Managed/Assembly-CSharp.dll", "Contents/Resources/Data/level0",
                                             "Contents/Resources/Data/resources.assets", "Contents/Resources/Data/StreamingAssets/Movie",
                                             "Contents/Resources/Data/StreamingAssets/AssetAssistant/Dpr.bin",
                                             "Contents/Plugins/AkSoundEngine.bundle", "Contents/Plugins/PcGamepad.bundle" })
                    if (!File.Exists(Path.Combine(app, need)) && !Directory.Exists(Path.Combine(app, need)))
                        r.Error("player is missing " + need);
                string plugins = Path.Combine(app, "Contents", "Plugins");
                if (Directory.Exists(plugins))
                    r.Line("player plug-ins: " + string.Join(", ", Directory.GetDirectories(plugins).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToArray()));
                r.Line("player: " + app);
                return ok && r.Errors == 0;
            }
            finally
            {
                File.WriteAllBytes(Path.Combine(Project, StartupAsset), startupBytes);
                AssetDatabase.ImportAsset(StartupAsset, ImportAssetOptions.ForceUpdate);
                UnityEditor.OSXStandalone.UserBuildSettings.architecture = prevMacArch;
                AkWwiseEditorSettings.Instance.CopySoundBanksAsPreBuildStep = prevWwiseCopy;
                PlayerSettings.stripEngineCode = prevStripEngine;
                if (stripChanged)
                    PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, prevStripLevel);
                if (prevBackend != ScriptingImplementation.Mono2x)
                    PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, prevBackend);
                AssetDatabase.SaveAssets();          // BEFORE the metas: saving re-writes importers still dirty from the build
                RestorePluginMetas(r, metas);
                // same values, but a re-serialized ProjectSettings.asset may reorder lines: the original bytes go back; BuildPlayer
                // also adds a built-in shader to GraphicsSettings' always-included list (measured 2026-10-20) - original bytes too
                bool psSame = File.ReadAllBytes(projectSettingsPath).SequenceEqual(projectSettingsBytes);
                if (!psSame)
                    File.WriteAllBytes(projectSettingsPath, projectSettingsBytes);
                bool gsSame = File.ReadAllBytes(graphicsPath).SequenceEqual(graphicsBytes);
                if (!gsSame)
                    File.WriteAllBytes(graphicsPath, graphicsBytes);
                r.Line("GraphicsSettings.asset " + (gsSame ? "unchanged" : "original bytes written back"));
                int banksAfter = CountFiles(Path.Combine(Project, BanksDir));
                if (banksAfter != banksBefore)
                    r.Error("PROJECT sound banks changed during the build: " + banksBefore + " -> " + banksAfter + " (restore from ../backup-pre-app-build/SoundBanks-Mac)");
                bool startupSame = File.ReadAllBytes(Path.Combine(Project, StartupAsset)).SequenceEqual(startupBytes);
                if (!startupSame)
                    r.Error("StartupSettings.asset is not byte-identical to its backup " + bdir);
                r.Line("restored: StartupSettings (byte-identical " + startupSame + "), ProjectSettings.asset (" + (psSame ? "unchanged" : "original bytes written back") +
                       "), Wwise copy flag " + prevWwiseCopy + ", backend " + prevBackend + ", stripping " + prevStripLevel + ", engine stripping " + prevStripEngine);
                WriteState("clean (last: mac player build " + stamp + ", " + (ok ? "OK" : "FAILED") + ")\n");
            }
        }

        // _CL_ (extra cl.exe options) as Windows builds a fresh process environment: the user value wins over the machine value
        static string RegistryClOptions()
        {
            return Environment.GetEnvironmentVariable("_CL_", EnvironmentVariableTarget.User)
                ?? Environment.GetEnvironmentVariable("_CL_", EnvironmentVariableTarget.Machine);
        }

        // IL2CPP 2019.4.27f1 (Unity.IL2CPP.Building Msvc15SystemInstallation) takes the HIGHEST "VC Tools x64" over every Visual
        // Studio instance on the machine - there is no option to choose. This mirrors it for the report.
        static string PickedMsvc()
        {
            try
            {
                string vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
                var psi = new System.Diagnostics.ProcessStartInfo(vswhere, "-all -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };
                string best = null;
                Version bestV = null;
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    foreach (var line in p.StandardOutput.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string f = Path.Combine(line.Trim(), "VC", "Auxiliary", "Build", "Microsoft.VCToolsVersion.default.txt");
                        Version v;
                        if (File.Exists(f) && Version.TryParse(File.ReadAllText(f).Trim(), out v) && (bestV == null || v > bestV))
                        {
                            bestV = v;
                            best = line.Trim();
                        }
                    }
                    p.WaitForExit();
                }
                return best == null ? "NONE FOUND (IL2CPP cannot compile)" : "MSVC " + bestV + " in " + best;
            }
            catch (Exception e)
            {
                return "unknown (" + e.Message + ")";
            }
        }

        // ---- the Switch bundles out of the player's StreamingAssets ------------------------------------------------------------
        static bool MoveSwitchBundlesAside(Report r)
        {
            string src = Path.Combine(Project, SwitchBundles);
            if (Directory.Exists(SwitchAside))
            {
                r.Error("an aside copy already exists at " + SwitchAside + " - run Recover first");
                return false;
            }
            if (!Directory.Exists(src))
            {
                r.Warn("no Switch bundles at " + SwitchBundles + " (already out of the project)");
                return true;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(SwitchAside));
            AppendState("switch_bundles_aside=" + SwitchAside + "\n");
            Directory.Move(src, SwitchAside);
            if (File.Exists(src + ".meta"))
                File.Move(src + ".meta", SwitchAside + ".meta");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            r.Line("Switch bundles moved aside: " + SwitchBundles + " -> " + SwitchAside);
            return true;
        }

        static void RestoreSwitchBundles(Report r)
        {
            string dst = Path.Combine(Project, SwitchBundles);
            if (!Directory.Exists(SwitchAside))
                return;
            if (Directory.Exists(dst))
            {
                r.Error("cannot restore the Switch bundles: " + dst + " exists again (the aside copy stays at " + SwitchAside + ")");
                return;
            }
            Directory.Move(SwitchAside, dst);
            if (File.Exists(SwitchAside + ".meta") && !File.Exists(dst + ".meta"))
                File.Move(SwitchAside + ".meta", dst + ".meta");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            r.Line("Switch bundles restored to " + SwitchBundles);
        }

        // ---- editor play against the Windows bundles (developer mode, optional) ------------------------------------------------
        static bool EditorPlayFromBundles(Report r, bool subset)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                r.Error("leave Play mode first");
                return false;
            }
            if (ReadStartupField("_assetBundleTarget") != "0")
            {
                r.Error("StartupSettings target is " + ReadStartupField("_assetBundleTarget") + ", not 0 - run 'back to AssetDatabase' / Recover first");
                return false;
            }
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string root = Path.Combine(BuildRoot, "editor_bundles_" + stamp + (subset ? "_subset" : ""));
            string platform = ReadStartupField("_platformName");
            // RuntimeLocation (2): cachePath = <URI>/AssetBundles/<platformName>; the _assetBundlesForEditor names load from
            // <URI>/AssetBundles/Editor in the editor (AssetManager.editorPath)
            string cache = Path.Combine(root, "AssetBundles", platform);
            string editorDir = Path.Combine(root, "AssetBundles", "Editor");
            var forEditor = StartupSettings.assetBundlesForEditor ?? new string[0];
            if (!LinkLayout(r, cache, subset, forEditor, editorDir))
                return false;
            Directory.CreateDirectory(Backups);
            string backup = Path.Combine(Backups, "StartupSettings.asset.editor_bundles_" + stamp);
            File.Copy(Path.Combine(Project, StartupAsset), backup);
            File.WriteAllText(Path.Combine(Backups, "StartupSettings.editor_bundles.latest.txt"), backup);
            WriteState("ACTIVE editor bundle mode " + stamp + "\nstartup_backup=" + backup + "\n");
            SetStartupFields(2, root.Replace('\\', '/'));
            r.Line("StartupSettings: target RuntimeLocation (2), URI " + root.Replace('\\', '/') + " - press Play; 'back to AssetDatabase' restores " + backup);
            return true;
        }

        static bool EditorPlayBack(Report r)
        {
            string latest = Path.Combine(Backups, "StartupSettings.editor_bundles.latest.txt");
            if (!File.Exists(latest))
            {
                r.Error("no editor-bundle backup recorded (" + latest + ")");
                return false;
            }
            string backup = File.ReadAllText(latest).Trim();
            File.WriteAllBytes(Path.Combine(Project, StartupAsset), File.ReadAllBytes(backup));
            AssetDatabase.ImportAsset(StartupAsset, ImportAssetOptions.ForceUpdate);
            WriteState("clean (editor back on AssetDatabase)\n");
            r.Line("StartupSettings restored byte for byte from " + backup + " (target " + ReadStartupField("_assetBundleTarget") + ")");
            return true;
        }

        static bool RecoverCore(Report r)
        {
            RestoreSwitchBundles(r);
            if (File.Exists(StateFile))
            {
                var m = Regex.Match(File.ReadAllText(StateFile), @"startup_backup=(.+)");
                if (File.ReadAllText(StateFile).Contains("ACTIVE") && m.Success && File.Exists(m.Groups[1].Value.Trim()))
                {
                    File.WriteAllBytes(Path.Combine(Project, StartupAsset), File.ReadAllBytes(m.Groups[1].Value.Trim()));
                    AssetDatabase.ImportAsset(StartupAsset, ImportAssetOptions.ForceUpdate);
                    r.Line("StartupSettings restored from " + m.Groups[1].Value.Trim());
                }
            }
            r.Line("StartupSettings target now " + ReadStartupField("_assetBundleTarget") + "; Switch bundles " +
                   (Directory.Exists(Path.Combine(Project, SwitchBundles)) ? "in place" : "MISSING") + "; aside copy " + (Directory.Exists(SwitchAside) ? "PRESENT" : "none"));
            WriteState("clean (recovered)\n");
            return true;
        }

        // ---- StartupSettings as TEXT (one line each; byte-exact restore from the backup) ---------------------------------------
        static string ReadStartupField(string field)
        {
            var m = Regex.Match(File.ReadAllText(Path.Combine(Project, StartupAsset)), "(?m)^  " + Regex.Escape(field) + ":[ ]?([^\r\n]*)");
            return m.Success ? m.Groups[1].Value.Trim() : "<missing>";
        }

        static void SetStartupFields(int target, string uri)
        {
            string path = Path.Combine(Project, StartupAsset);
            string text = File.ReadAllText(path);
            text = Regex.Replace(text, @"(?m)^(  _assetBundleTarget:) \d+", "${1} " + target);
            if (uri != null)
                text = Regex.Replace(text, @"(?m)^(  _assetBundleTargetURI:)[^\r\n]*", "${1} " + uri);
            File.WriteAllText(path, text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(StartupAsset, ImportAssetOptions.ForceUpdate);
        }

        // ---- small helpers -----------------------------------------------------------------------------------------------------
        // the drive the build writes to: Path.GetPathRoot is "/" on macOS for every path, so an external /Volumes/<name> is its own
        static DriveInfo BuildDrive
        {
            get
            {
                var m = Regex.Match(BuildRoot.Replace('\\', '/'), "^/Volumes/[^/]+");
                return new DriveInfo(m.Success ? m.Value : Path.GetPathRoot(BuildRoot));
            }
        }

        // a Mono player / bundle build for macOS: BuildPipeline refuses to start while the selected Standalone backend (the project's
        // IL2CPP) has no macOS module installed - set Mono2x for the run; the caller restores it and the ProjectSettings.asset bytes
        static ScriptingImplementation UseMono(Report r)
        {
            var prev = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
            if (prev != ScriptingImplementation.Mono2x)
            {
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
                r.Line("Standalone scripting backend " + prev + " -> Mono2x for this run");
            }
            return prev;
        }

        // every plug-in importer .meta under Assets/Wwise + Assets/Plugins, byte for byte (the editor's working audio depends on them)
        static Dictionary<string, byte[]> SnapshotPluginMetas()
        {
            var metas = new Dictionary<string, byte[]>();
            foreach (var dir in new[] { Path.Combine(Project, "Assets", "Wwise"), Path.Combine(Project, "Assets", "Plugins") })
                if (Directory.Exists(dir))
                    foreach (var p in Directory.GetFiles(dir, "*.meta", SearchOption.AllDirectories))
                        metas[p] = File.ReadAllBytes(p);
            return metas;
        }

        static void RestorePluginMetas(Report r, Dictionary<string, byte[]> metas)
        {
            int back = 0;
            foreach (var kv in metas)
            {
                if (File.Exists(kv.Key) && File.ReadAllBytes(kv.Key).SequenceEqual(kv.Value))
                    continue;
                File.WriteAllBytes(kv.Key, kv.Value);
                string asset = kv.Key.Substring(Project.Length + 1).Replace('\\', '/');
                AssetDatabase.ImportAsset(asset.Substring(0, asset.Length - ".meta".Length), ImportAssetOptions.ForceUpdate);
                back++;
            }
            r.Line("plug-in .meta files written back: " + back + " of " + metas.Count);
        }

        // the project enables the Debug, Profile AND Release AkSoundEngine.bundle for Standalone OSX; Wwise narrows that to one
        // config only in its BuildPlayer pre-build hook (AkBuildPreprocessor) - BuildAssetBundles stops first with "Plugins colliding
        // with each other" (measured 2026-10-19). Wwise's own activation for the target, before either build; undone by RestorePluginMetas.
        static void ActivateWwiseForMac(Report r)
        {
            AkPluginActivator.Update(true);      // as AkBuildPreprocessor: reads PluginInfo.xml (which DSP plug-ins the banks use)
            AkPluginActivator.ActivatePluginsForDeployment(BuildTarget.StandaloneOSX, true);
            var on = PluginImporter.GetAllImporters()
                .Where(p => p.assetPath.Contains("/Wwise/Deployment/Plugins/Mac/") && p.GetCompatibleWithPlatform(BuildTarget.StandaloneOSX))
                .Select(p => p.assetPath.Substring(p.assetPath.IndexOf("/Mac/", StringComparison.Ordinal) + 5)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            r.Line("Wwise plug-ins activated for StandaloneOSX (" + AkPluginActivator.GetCurrentConfig() + "): " + string.Join(", ", on));
        }

        static void RestoreBackend(Report r, ScriptingImplementation prev, string projectSettingsPath, byte[] projectSettingsBytes)
        {
            if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone) != prev)
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, prev);
            AssetDatabase.SaveAssets();
            bool same = File.ReadAllBytes(projectSettingsPath).SequenceEqual(projectSettingsBytes);
            if (!same)
                File.WriteAllBytes(projectSettingsPath, projectSettingsBytes);
            r.Line("Standalone scripting backend restored -> " + prev + "; ProjectSettings.asset " + (same ? "unchanged" : "original bytes written back"));
        }
        static int CountFiles(string dir)
        {
            return Directory.Exists(dir) ? Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Count(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) : -1;
        }

        static void WriteState(string s)
        {
            Directory.CreateDirectory(Work);
            File.WriteAllText(StateFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " " + s);
        }

        static void AppendState(string s)
        {
            Directory.CreateDirectory(Work);
            File.AppendAllText(StateFile, s);
        }
    }
}
