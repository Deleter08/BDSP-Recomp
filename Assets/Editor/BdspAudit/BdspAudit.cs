// BDSP play-test campaign, Phase 3 (HANDOFF §11.5): editor-only audit, PC tooling - not game code.
// Its own asmdef with NO references, so it compiles even when Assembly-CSharp does not (the report must still run).
//   BdspAudit.ImportReport  (-executeMethod, run right after the first import): script compilation state, asset count, every .shader
//                           under Assets with ShaderHasError + its messages -> ../work/s31/import_report.txt. Light: loads no
//                           prefab / scene / material and forces no variant compile (harness law 10).
//   BdspAudit.FullAudit     (later, its own session): missing-script components over every prefab and scene, ScriptableObjects with
//                           a missing script, materials whose shader is null or Hidden/InternalErrorShader -> ../work/s31/audit_<date>.txt
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BdspAudit
{
    static string WorkDir()
    {
        // the automated setup passes its work folder in BDSP_SETUP_WORK; without it the workspace layout ../work/s31
        var setup = Environment.GetEnvironmentVariable("BDSP_SETUP_WORK");
        var d = string.IsNullOrEmpty(setup) ? Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "work", "s31"))
                                            : Path.Combine(setup, "audit");
        Directory.CreateDirectory(d); return d;
    }

    [MenuItem("BDSP/Audit/Import report")]
    public static void ImportReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("BDSP import report " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " unity " + Application.unityVersion);
        sb.AppendLine("scriptCompilationFailed " + EditorUtility.scriptCompilationFailed);
        sb.AppendLine("loaded assemblies with Assembly-CSharp: " + AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name.StartsWith("Assembly-CSharp")));
        sb.AppendLine("asset paths " + AssetDatabase.GetAllAssetPaths().Length);
        int n = 0, bad = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid); var sh = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (sh == null) { sb.AppendLine("SHADER LOAD FAILED " + path); bad++; continue; }
            n++; bool err = ShaderUtil.ShaderHasError(sh); var msgs = ShaderUtil.GetShaderMessages(sh);
            if (err || msgs.Length > 0) bad += err ? 1 : 0;
            sb.AppendLine(string.Format("SHADER {0} | {1} | error={2} supported={3} passes={4} messages={5}", sh.name, path, err, sh.isSupported, sh.passCount, msgs.Length));
            foreach (var m in msgs.Take(5)) sb.AppendLine(string.Format("   [{0}] line {1} platform {2}: {3}", m.severity, m.line, m.platform, m.message));
        }
        sb.AppendLine(string.Format("SHADERS {0} loaded, {1} with errors", n, bad));
        sb.AppendLine("BDSP_IMPORT_REPORT_DONE");
        File.WriteAllText(Path.Combine(WorkDir(), "import_report.txt"), sb.ToString());
        Debug.Log(sb.ToString());
    }

    [MenuItem("BDSP/Audit/Full audit (prefabs, scenes, materials)")]
    public static void FullAudit()
    {
        var sb = new StringBuilder(); int prefabs = 0, prefabMissing = 0, scenes = 0, sceneMissing = 0, soMissing = 0, pink = 0, matCount = 0;
        sb.AppendLine("BDSP full audit " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid); var root = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (root == null) continue;
            prefabs++; int miss = root.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            if (miss > 0) { prefabMissing++; if (prefabMissing <= 200) sb.AppendLine("PREFAB MISSING " + miss + " " + path); }
            if (prefabs % 200 == 0) { EditorUtility.UnloadUnusedAssetsImmediate(); GC.Collect(); }
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid); var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single); scenes++;
            int miss = sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            if (miss > 0) { sceneMissing++; sb.AppendLine("SCENE MISSING " + miss + " " + path); }
        }
        // a ScriptableObject whose script is missing reports the bare base type (or none); checked by TYPE only - no load
        foreach (var path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && p.EndsWith(".asset")))
        {
            var t = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (t == null || t == typeof(MonoBehaviour) || t == typeof(ScriptableObject)) { soMissing++; if (soMissing <= 200) sb.AppendLine("SO MISSING SCRIPT " + path + " (type " + (t == null ? "null" : t.Name) + ")"); }
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid); var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader") { pink++; if (pink <= 200) sb.AppendLine("PINK " + path); }
            if (++matCount % 2000 == 0) { EditorUtility.UnloadUnusedAssetsImmediate(); GC.Collect(); }
        }
        sb.AppendLine(string.Format("TOTAL prefabs {0} (with missing scripts {1}), scenes {2} ({3}), ScriptableObjects missing script {4}, pink materials {5}", prefabs, prefabMissing, scenes, sceneMissing, soMissing, pink));
        sb.AppendLine("BDSP_FULL_AUDIT_DONE");
        File.WriteAllText(Path.Combine(WorkDir(), "audit_" + DateTime.Now.ToString("yyyyMMdd_HHmm", System.Globalization.CultureInfo.InvariantCulture) + ".txt"), sb.ToString());
        Debug.Log(sb.ToString());
    }
}
