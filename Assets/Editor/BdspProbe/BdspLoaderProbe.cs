// BDSP play-test campaign, Phase 4 (HANDOFF §11.13): the editor load path checked WITHOUT the boot (editor-only PC tooling, not game code).
// The boot stops at GameManager.Initialize -> UserAccount.Open (a Switch extern, Phase 5), before AssetManager.Initialize ever runs. This
// probe enters Play mode on level0 (the game's boot runs and fails as it does), then starts AssetManager itself (its own
// [AssetAssistantInitializeMethod] Initialize, by reflection), waits for isReady and requests real bundles / scenes through the game's
// own AssetManager -> Sequencer.editorProxy (UnityEditorProxy + Assets/Editor/EditorAssetAssistant/bundle_map.tsv):
//   ../work/s32/loader_requests.txt (optional; lines "bundle <name> [variant,...]" / "scene <Assets/... .unity>"), else the default list.
// Report -> ../work/s32/loader_<time>.txt: per request the end status, Activated assets (count, types, nulls), the bundle cache's loaded
// assets, renderers / materials whose shader is missing or unsupported, missing scripts; Game-view screenshots of the loaded scenes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SmartPoint.AssetAssistant;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BdspLoaderProbe
{
    const string Phase = "bdsp_loader_phase", Stamp = "bdsp_loader_stamp";
    static readonly string[] DefaultRequests =
    {
        "bundle scriptableobjects/gamesettings", "bundle shaders", "bundle common_resources", "bundle shareduiassets/sharedui",
        "bundle shareduiassets/battle_lang en", "bundle textures/common", "bundle fieldui/main", "bundle persons/field/fc0001_00",
        "bundle pokemons/field/pm0001_00_00", "bundle fields/area001", "scene Assets/SharedAssets/Games/Scenes/Title.unity",
    };

    class Req { public string kind, name; public string[] variants; public AssetRequestOperation op; public List<string> events = new List<string>(); public double t0; }
    static List<Req> reqs;
    static int frames, step;
    static double stepStart;

    static string Work() { var d = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "work", "s32")); Directory.CreateDirectory(d); return d; }
    static string Report() => Path.Combine(Work(), "loader_" + SessionState.GetString(Stamp, "x") + ".txt");
    static void Log(string s) { File.AppendAllText(Report(), s + "\n"); UnityEngine.Debug.Log("[BdspLoaderProbe] " + s); }

    static BdspLoaderProbe() { EditorApplication.update += Tick; }

    [MenuItem("BDSP/Play/Loader probe (AssetDatabase mode)")]
    public static void Run()
    {
        SessionState.SetString(Stamp, DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture));
        Log("BDSP loader probe, unity " + Application.unityVersion + ", StartupSettings target " + StartupSettings.assetBundleTarget);
        EditorSceneManager.OpenScene("Assets/Scenes/level0.unity", OpenSceneMode.Single);
        SessionState.SetString(Phase, "play");
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        var phase = SessionState.GetString(Phase, "");
        if (phase == "play" && EditorApplication.isPlaying)
        {
            try { PlayStep(); }
            catch (Exception e) { Log("PROBE EXCEPTION at step " + step + ": " + e); Finish(); }
        }
        else if (phase == "exit" && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetString(Phase, "");
            Log("BDSP_LOADER_PROBE_DONE");
            EditorApplication.Exit(0);
        }
    }

    static void Finish() { SessionState.SetString(Phase, "exit"); EditorApplication.ExitPlaymode(); }

    static void PlayStep()
    {
        frames++;
        double now = EditorApplication.timeSinceStartup;
        if (step == 0)
        {
            if (frames < 10) return;
            Log(string.Format("frame {0}: AssetManager.isReady={1} editorProxy={2}", Time.frameCount, AssetManager.isReady, Sequencer.editorProxy == null ? "null" : Sequencer.editorProxy.GetType().FullName));
            if (!AssetManager.isReady)
            {
                var init = typeof(AssetManager).GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Static);
                Log("boot did not reach AssetManager.Initialize -> invoking it here (" + (init == null ? "NOT FOUND" : init.ToString()) + ")");
                init.Invoke(null, null);
            }
            step = 1; stepStart = now;
        }
        else if (step == 1)
        {
            if (!AssetManager.isReady) { if (now - stepStart > 60) { Log("AssetManager never became ready"); Finish(); } return; }
            Log(string.Format("AssetManager ready after {0:F1} s; bundle names via proxy: {1}", now - stepStart, AssetManager.GetAllAssetBundleNames().Length));
            var f = Path.Combine(Work(), "loader_requests.txt");
            var lines = File.Exists(f) ? File.ReadAllLines(f).Where(l => l.Trim().Length > 0 && !l.StartsWith("#")).ToArray() : DefaultRequests;
            reqs = new List<Req>();
            foreach (var l in lines)
            {
                var p = l.Trim().Split(' ');
                var r = new Req { kind = p[0], name = p[1], variants = p.Length > 2 ? p[2].Split(',') : null, t0 = now };
                RequestEventCallback cb = (type, name, asset) => r.events.Add(string.Format("{0} '{1}' {2}", type, name, asset == null ? "NULL" : asset.GetType().Name));
                if (r.kind == "bundle")
                    r.op = AssetManager.RequestAssetBundle(r.name, true, cb, r.variants);
                else
                {
                    AssetManager.AppendAssetRequest(r.name, cb);
                    r.op = AssetManager.DispatchRequests(cb);
                }
                reqs.Add(r);
            }
            step = 2; stepStart = now;
        }
        else if (step == 2)
        {
            bool waiting = reqs.Any(r => r.op != null && r.op.keepWaiting);
            if (waiting && now - stepStart < 120) return;
            foreach (var r in reqs) Describe(r);
            step = 3; stepStart = now;
            ScreenCapture.CaptureScreenshot(Path.Combine(Work(), "loader_" + SessionState.GetString(Stamp, "x") + "_shot0.png"));
        }
        else if (step == 3)
        {
            if (now - stepStart < 3) return;
            ScreenCapture.CaptureScreenshot(Path.Combine(Work(), "loader_" + SessionState.GetString(Stamp, "x") + "_shot1.png"));
            step = 4; stepStart = now;
        }
        else if (step == 4)
        {
            if (now - stepStart < 2) return;
            var sb = new StringBuilder("scenes at end:");
            for (int i = 0; i < SceneManager.sceneCount; i++) { var s = SceneManager.GetSceneAt(i); sb.Append(string.Format(" [{0} loaded={1} roots={2}]", s.path, s.isLoaded, s.isLoaded ? s.rootCount : 0)); }
            Log(sb.ToString());
            Finish();
        }
    }

    static void Describe(Req r)
    {
        var item = r.op == null ? null : r.op.request;
        Log(string.Format("\n== {0} {1}{2}: status={3} error={4} events={5}", r.kind, r.name, r.variants == null ? "" : " [" + string.Join(",", r.variants) + "]",
            item == null ? "no op" : item.status.ToString(), item == null ? "" : item.error, r.events.Count));
        foreach (var g in r.events.GroupBy(e => e.Split(' ')[0] + " " + e.Substring(e.LastIndexOf(' ') + 1)).OrderBy(g => g.Key)) Log("   " + g.Key + " x" + g.Count());
        foreach (var e in r.events.Where(e => e.EndsWith("NULL")).Take(10)) Log("   NULL: " + e);
        if (r.kind == "bundle")
        {
            var bundleName = item == null ? r.name : item.uri;
            var cache = AssetBundleCache.Get(bundleName, true);
            if (cache == null) { Log("   cache: none for " + bundleName); return; }
            var assets = cache.loadedAssets ?? new UnityEngine.Object[0];
            Log(string.Format("   cache {0}: record paths {1}, loadedAssets {2} (null {3}), isLoaded {4}", bundleName, cache.record == null || cache.record.assetPaths == null ? -1 : cache.record.assetPaths.Length,
                assets.Length, assets.Count(a => a == null), cache.isLoaded));
            foreach (var go in assets.OfType<GameObject>().Take(20)) CheckObjects(go.name, go.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject));
        }
        else
        {
            var scene = SceneManager.GetSceneByPath(r.name);
            Log(string.Format("   scene {0}: valid={1} loaded={2} roots={3}", r.name, scene.IsValid(), scene.isLoaded, scene.isLoaded ? scene.rootCount : 0));
            if (scene.isLoaded) CheckObjects(r.name, scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject));
        }
    }

    static void CheckObjects(string label, IEnumerable<GameObject> gos)
    {
        int objs = 0, missing = 0, renderers = 0, badMat = 0; var bad = new HashSet<string>();
        foreach (var go in gos)
        {
            objs++;
            missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            foreach (var rd in go.GetComponents<Renderer>())
            {
                renderers++;
                foreach (var m in rd.sharedMaterials)
                {
                    if (m == null || m.shader == null || !m.shader.isSupported || m.shader.name == "Hidden/InternalErrorShader")
                    { badMat++; if (bad.Count < 8) bad.Add(m == null ? "null material on " + go.name : m.name + " (" + (m.shader == null ? "no shader" : m.shader.name) + ")"); }
                }
            }
        }
        Log(string.Format("   objects in {0}: gameObjects {1}, missing scripts {2}, renderers {3}, bad materials {4} {5}", label, objs, missing, renderers, badMat, bad.Count > 0 ? string.Join("; ", bad) : ""));
    }
}
