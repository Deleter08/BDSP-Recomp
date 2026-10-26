// BDSP play-test campaign, Phase 4 (HANDOFF §11.13): SpriteAtlas rebuild + probe, editor-only PC tooling - not game code.
// AssetRipper exported no SpriteAtlas (73 shipped objects); tools/spriteatlas_spec.py reads them from the Switch bundles and writes
// ../work/s32/spriteatlas_spec.json. This script lets UNITY write each .spriteatlas (SerializedObject on m_PackedSprites,
// m_PackedSpriteNamesToIndex, m_RenderDataMap, m_Tag, m_IsVariant), so the YAML is the editor's own.
//   BdspAtlas.BuildAndProbe (-executeMethod): build the atlases named in ../work/s32/atlas_filter.txt (one substring of the target
//       path per line; empty / missing file = all), probe them in edit mode, enter Play mode on an empty scene, probe again
//       (GetSprite, the packed sprite's own texture = late binding, a SpriteRenderer render), leave Play mode, re-hash the files
//       (did the editor's packer rewrite them?), write ../work/s32/atlas_probe_<time>.txt and quit.
//   ../work/s32/atlas_packer_mode.txt (optional, one int): EditorSettings.spritePackerMode for the run (restored at the end).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

[InitializeOnLoad]
public static class BdspAtlas
{
    [Serializable] public class Spec { public AtlasSpec[] atlases; }
    [Serializable] public class AtlasSpec
    {
        public string target, container, bundle, name, tag; public bool isVariant;
        public string[] packedNames, packedSprites; public RD[] renderData;
    }
    [Serializable] public class RD
    {
        public long[] key; public long fileID; public string texture, alphaTexture;
        public float[] textureRect, textureRectOffset, atlasRectOffset, uvTransform; public float downscaleMultiplier; public long settingsRaw;
    }

    const string Phase = "bdsp_atlas_phase", Targets = "bdsp_atlas_targets", OldMode = "bdsp_atlas_oldmode";
    static int frames;

    // the automated setup (setup/setup.py) passes its work folder in BDSP_SETUP_WORK; without it the workspace layout ../work/s32
    static string Work()
    {
        var setup = Environment.GetEnvironmentVariable("BDSP_SETUP_WORK");
        var d = string.IsNullOrEmpty(setup) ? Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "work", "s32"))
                                            : Path.Combine(setup, "atlas");
        Directory.CreateDirectory(d); return d;
    }
    static string ReportPath() => Path.Combine(Work(), "atlas_probe_" + SessionState.GetString("bdsp_atlas_stamp", "x") + ".txt");
    static void Log(string s) { File.AppendAllText(ReportPath(), s + "\n"); Debug.Log("[BdspAtlas] " + s); }
    static string Sha1(string path) { using (var h = SHA1.Create()) return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
    static string Inv(float f) => f.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    static BdspAtlas() { EditorApplication.update += Tick; SpriteAtlasManager.atlasRequested += OnAtlasRequested; }

    static int atlasRequests;
    static void OnAtlasRequested(string tag, Action<SpriteAtlas> action) { atlasRequests++; if (atlasRequests <= 20) Debug.Log("[BdspAtlas] atlasRequested " + tag); }

    // the sprite's UV bounds in texels of its texture (compare with textureRect: equal for a FullRect mesh)
    static string UvTexels(Sprite s)
    {
        if (s.texture == null || s.uv.Length == 0) return "-";
        float x0 = s.uv.Min(u => u.x) * s.texture.width, x1 = s.uv.Max(u => u.x) * s.texture.width, y0 = s.uv.Min(u => u.y) * s.texture.height, y1 = s.uv.Max(u => u.y) * s.texture.height;
        return string.Format("[{0:F2},{1:F2}]x[{2:F2},{3:F2}] tex {4}x{5}", x0, x1, y0, y1, s.texture.width, s.texture.height);
    }

    // "Include in Build" (SpriteAtlasExtensions has only the setter in 2019.4)
    static string BindAsDefault(SerializedObject so) { var p = so.FindProperty("m_EditorData.bindAsDefault"); return p == null ? "n/a" : p.boolValue.ToString(); }

    public static Spec LoadSpec() => JsonUtility.FromJson<Spec>(File.ReadAllText(Path.Combine(Work(), "spriteatlas_spec.json")));

    // Unity 6 (SpriteAtlas serializedVersion 3) no longer exposes m_PackedSprites / m_PackedSpriteNamesToIndex to SerializedObject
    // and does not write them. It still loads and upgrades the 2019 layout, which is what this project's atlases are. There the
    // .spriteatlas is written as that 2019 text (the editor's own 2019.4 output, identical in all 70 project atlases apart from
    // name, tag and the two lists) and imported.
    static bool LegacyText()
    {
        var probe = new SpriteAtlas();
        try { return new SerializedObject(probe).FindProperty("m_PackedSprites") == null; }
        finally { UnityEngine.Object.DestroyImmediate(probe); }
    }

    // Unity's plain-scalar rule as far as sprite names need it: quote with '' when a plain scalar would be misread.
    static string YamlScalar(string s)
    {
        bool plain = s.Length > 0 && s.Trim() == s && "-?:,[]{}#&*!|>'\"%@`".IndexOf(s[0]) < 0 && !s.Contains(": ") && !s.Contains(" #")
                     && !s.EndsWith(":") && s.IndexOfAny(new[] { '\n', '\r', '\t' }) < 0;
        return plain ? s : "'" + s.Replace("'", "''") + "'";
    }

    static string Ref(UnityEngine.Object o)
    {
        if (o == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string guid, out long id)) return "{fileID: 0}";
        return "{fileID: " + id + ", guid: " + guid + ", type: " + (AssetDatabase.IsNativeAsset(o) ? 2 : 3) + "}";
    }

    static int WriteLegacy(AtlasSpec a)
    {
        int missing = 0;
        // The 2019 editor named the asset after its file and saved m_Tag = that name (Field_1.spriteatlas: spec name/tag "Field").
        var stem = Path.GetFileNameWithoutExtension(a.target);
        var sb = new StringBuilder();
        sb.Append("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!687078895 &4343727234628468602\nSpriteAtlas:\n");
        sb.Append("  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n");
        sb.Append("  m_Name: ").Append(YamlScalar(stem)).Append("\n");
        sb.Append("  m_EditorData:\n    serializedVersion: 2\n    textureSettings:\n      serializedVersion: 2\n      anisoLevel: 1\n      compressionQuality: 50\n");
        sb.Append("      maxTextureSize: 2048\n      textureCompression: 0\n      filterMode: 1\n      generateMipMaps: 0\n      readable: 0\n      crunchedCompression: 0\n");
        sb.Append("      sRGB: 1\n    platformSettings: []\n    packingSettings:\n      serializedVersion: 2\n      padding: 4\n      blockOffset: 1\n");
        sb.Append("      allowAlphaSplitting: 0\n      enableRotation: 1\n      enableTightPacking: 1\n    variantMultiplier: 1\n    packables: []\n");
        sb.Append("    bindAsDefault: 0\n  m_MasterAtlas: {fileID: 0}\n");
        sb.Append(a.packedSprites.Length == 0 ? "  m_PackedSprites: []\n" : "  m_PackedSprites:\n");
        foreach (var path in a.packedSprites)
        {
            var sp = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null) missing++;
            sb.Append("  - ").Append(Ref(sp)).Append("\n");
        }
        sb.Append(a.packedNames.Length == 0 ? "  m_PackedSpriteNamesToIndex: []\n" : "  m_PackedSpriteNamesToIndex:\n");
        foreach (var n in a.packedNames) sb.Append("  - ").Append(YamlScalar(n)).Append("\n");
        sb.Append("  m_Tag: ").Append(YamlScalar(stem)).Append("\n  m_IsVariant: ").Append(a.isVariant ? 1 : 0).Append("\n");
        File.WriteAllText(a.target, sb.ToString(), new UTF8Encoding(false));   // an existing .meta (GUID) is kept
        AssetDatabase.ImportAsset(a.target, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        return missing;
    }

    // Builds (or rebuilds in place, keeping the GUID) every atlas whose target matches the filter. Returns the built target paths.
    public static List<string> Build(Spec spec, string[] filter)
    {
        var built = new List<string>();
        bool legacy = LegacyText();
        if (legacy) Log("editor does not serialize m_PackedSprites (SpriteAtlas v3); writing the 2019 .spriteatlas text and importing it");
        foreach (var a in spec.atlases)
        {
            if (filter.Length > 0 && !filter.Any(f => a.target.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
            var dir = Path.GetDirectoryName(a.target).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir)) { Directory.CreateDirectory(dir); AssetDatabase.ImportAsset(dir); }
            if (legacy)
            {
                int miss = WriteLegacy(a);
                built.Add(a.target);
                Log(string.Format("BUILD {0} name={1} tag={2} packed={3} (missing {4}) rd={5} (2019 text; render data is late-bound)", a.target, a.name, a.tag, a.packedSprites.Length, miss, a.renderData.Length));
                continue;
            }
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(a.target);
            if (atlas == null) { atlas = new SpriteAtlas(); AssetDatabase.CreateAsset(atlas, a.target); atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(a.target); }
            atlas.SetIncludeInBuild(false); // shipped: late binding (packed sprites carry no texture; UIManager.OnAtlasRequested binds)
            var so = new SerializedObject(atlas);
            so.FindProperty("m_Tag").stringValue = a.tag;
            so.FindProperty("m_IsVariant").boolValue = a.isVariant;
            var ps = so.FindProperty("m_PackedSprites"); ps.arraySize = a.packedSprites.Length;
            int missing = 0;
            for (int i = 0; i < a.packedSprites.Length; i++)
            {
                var sp = string.IsNullOrEmpty(a.packedSprites[i]) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(a.packedSprites[i]);
                if (sp == null) missing++;
                ps.GetArrayElementAtIndex(i).objectReferenceValue = sp;
            }
            var pn = so.FindProperty("m_PackedSpriteNamesToIndex"); pn.arraySize = a.packedNames.Length;
            for (int i = 0; i < a.packedNames.Length; i++) pn.GetArrayElementAtIndex(i).stringValue = a.packedNames[i];
            var rd = so.FindProperty("m_RenderDataMap");
            if (rd == null) Log("BUILD " + a.target + ": m_RenderDataMap NOT a serialized property in the editor");
            else
            {
                rd.arraySize = a.renderData.Length;
                for (int i = 0; i < a.renderData.Length; i++)
                {
                    var e = rd.GetArrayElementAtIndex(i); var r = a.renderData[i];
                    var g = e.FindPropertyRelative("first.first");
                    for (int w = 0; w < 4; w++) g.FindPropertyRelative("data[" + w + "]").longValue = r.key[w];
                    e.FindPropertyRelative("first.second").longValue = r.fileID;
                    var v = e.FindPropertyRelative("second");
                    v.FindPropertyRelative("texture").objectReferenceValue = string.IsNullOrEmpty(r.texture) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(r.texture);
                    v.FindPropertyRelative("alphaTexture").objectReferenceValue = string.IsNullOrEmpty(r.alphaTexture) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(r.alphaTexture);
                    v.FindPropertyRelative("textureRect").rectValue = new Rect(r.textureRect[0], r.textureRect[1], r.textureRect[2], r.textureRect[3]);
                    v.FindPropertyRelative("textureRectOffset").vector2Value = new Vector2(r.textureRectOffset[0], r.textureRectOffset[1]);
                    v.FindPropertyRelative("atlasRectOffset").vector2Value = new Vector2(r.atlasRectOffset[0], r.atlasRectOffset[1]);
                    v.FindPropertyRelative("uvTransform").vector4Value = new Vector4(r.uvTransform[0], r.uvTransform[1], r.uvTransform[2], r.uvTransform[3]);
                    v.FindPropertyRelative("downscaleMultiplier").floatValue = r.downscaleMultiplier;
                    v.FindPropertyRelative("settingsRaw").longValue = r.settingsRaw;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(atlas);
            built.Add(a.target);
            Log(string.Format("BUILD {0} name={1} tag={2} packed={3} (missing {4}) rd={5}", a.target, a.name, a.tag, a.packedSprites.Length, missing, a.renderData.Length));
        }
        AssetDatabase.SaveAssets();
        return built;
    }

    static void Probe(string when, Spec spec, IEnumerable<string> targets)
    {
        foreach (var t in targets)
        {
            var a = spec.atlases.First(x => x.target == t);
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(t);
            if (atlas == null) { Log(when + " " + t + ": LOAD FAILED"); continue; }
            var so = new SerializedObject(atlas); var rd = so.FindProperty("m_RenderDataMap");
            Log(string.Format("{0} {1}: spriteCount={2} tag={3} includeInBuild={4} packedSprites={5} names={6} renderDataMap={7}", when, t, atlas.spriteCount, atlas.tag,
                BindAsDefault(so), so.FindProperty("m_PackedSprites").arraySize, so.FindProperty("m_PackedSpriteNamesToIndex").arraySize, rd == null ? "n/a" : rd.arraySize.ToString()));
            var all = new Sprite[Math.Max(1, atlas.spriteCount)]; int got = atlas.spriteCount > 0 ? atlas.GetSprites(all) : 0;
            Log(string.Format("   GetSprites -> {0} (non-null {1}, with texture {2})", got, all.Count(s => s != null), all.Count(s => s != null && s.texture != null)));
            for (int i = 0; i < Math.Min(3, a.packedNames.Length); i++)
            {
                var s = atlas.GetSprite(a.packedNames[i]);
                Log(s == null ? "   GetSprite(" + a.packedNames[i] + ") = null" : string.Format("   GetSprite({0}) = '{1}' texture={2} packed={3} textureRect={4} rect={5} uv0={6} uvTexels={7}",
                    a.packedNames[i], s.name, s.texture == null ? "null" : s.texture.name, s.packed, s.textureRect, s.rect, s.uv.Length > 0 ? s.uv[0].ToString("F4") : "-", UvTexels(s)));
                var asset = string.IsNullOrEmpty(a.packedSprites[i]) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(a.packedSprites[i]);
                if (asset != null) Log(string.Format("   asset {0}: texture={1} packed={2} uv0={3}", a.packedSprites[i], asset.texture == null ? "null" : asset.texture.name, asset.packed, asset.uv.Length > 0 ? asset.uv[0].ToString("F4") : "-"));
            }
            if (EditorApplication.isPlaying) RenderTest(atlas, a);
        }
    }

    // Draws GetSprite(first name) and the packed sprite asset with a SpriteRenderer into a 256x256 RT; reports covered pixels + mean.
    static void RenderTest(SpriteAtlas atlas, AtlasSpec a)
    {
        var sprites = new List<KeyValuePair<string, Sprite>>();
        sprites.Add(new KeyValuePair<string, Sprite>("GetSprite", atlas.GetSprite(a.packedNames[0])));
        sprites.Add(new KeyValuePair<string, Sprite>("asset", string.IsNullOrEmpty(a.packedSprites[0]) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(a.packedSprites[0])));
        foreach (var kv in sprites)
        {
            if (kv.Value == null) { Log("   RENDER " + kv.Key + ": sprite null"); continue; }
            var camGo = new GameObject("BdspAtlasCam"); var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = Mathf.Max(kv.Value.bounds.extents.x, kv.Value.bounds.extents.y) * 1.1f + 0.01f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0); cam.transform.position = new Vector3(0, 0, -10);
            var go = new GameObject("BdspAtlasSprite"); var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = kv.Value;
            go.transform.position = -kv.Value.bounds.center;
            var rt = RenderTexture.GetTemporary(256, 256, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt; cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(256, 256, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); tex.Apply(); RenderTexture.active = prev;
            var px = tex.GetPixels32(); int cover = px.Count(p => p.a > 0); double r = 0, g = 0, b = 0; foreach (var p in px) if (p.a > 0) { r += p.r; g += p.g; b += p.b; }
            Log(string.Format("   RENDER {0}: sprite '{1}' texture={2} covered={3}/65536 mean=({4:F1},{5:F1},{6:F1})", kv.Key, kv.Value.name, kv.Value.texture == null ? "null" : kv.Value.texture.name, cover,
                cover > 0 ? r / cover : 0, cover > 0 ? g / cover : 0, cover > 0 ? b / cover : 0));
            File.WriteAllBytes(Path.Combine(Work(), "atlas_render_" + a.name + "_" + kv.Key + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(camGo); UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    // Builds every atlas of the spec (no filter), checks each in edit mode (GetSprites: count, with texture) and quits. Needs
    // EditorSettings.spritePackerMode = Disabled (tools/phase4_settings.py --packer), or Play mode empties the atlases again.
    [MenuItem("BDSP/Atlas/Build all")]
    public static void BuildAll()
    {
        SessionState.SetString("bdsp_atlas_stamp", "buildall_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture));
        Log("BDSP atlas build-all, unity " + Application.unityVersion + ", spritePackerMode " + EditorSettings.spritePackerMode);
        var spec = LoadSpec();
        var built = Build(spec, new string[0]);
        int ok = 0;
        foreach (var t in built)
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(t);
            var all = new Sprite[Math.Max(1, atlas == null ? 0 : atlas.spriteCount)];
            int got = atlas == null || atlas.spriteCount == 0 ? 0 : atlas.GetSprites(all);
            int tex = all.Count(s => s != null && s.texture != null);
            if (atlas != null && got == atlas.spriteCount && tex == got) ok++;
            Log(string.Format("CHECK {0}: spriteCount={1} GetSprites={2} withTexture={3} sha1={4}", t, atlas == null ? -1 : atlas.spriteCount, got, tex, Sha1(t)));
        }
        Log(string.Format("BUILT {0} atlases, {1} fully textured", built.Count, ok));
        Log("BDSP_ATLAS_BUILD_DONE");
        EditorApplication.Exit(0);
    }

    [MenuItem("BDSP/Atlas/Build + probe")]
    public static void BuildAndProbe()
    {
        SessionState.SetString("bdsp_atlas_stamp", DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture));
        Log("BDSP atlas probe, unity " + Application.unityVersion + ", spritePackerMode " + EditorSettings.spritePackerMode);
        var modeFile = Path.Combine(Work(), "atlas_packer_mode.txt");
        if (File.Exists(modeFile))
        {
            SessionState.SetInt(OldMode, (int)EditorSettings.spritePackerMode);
            EditorSettings.spritePackerMode = (SpritePackerMode)int.Parse(File.ReadAllText(modeFile).Trim());
            Log("spritePackerMode for this run -> " + EditorSettings.spritePackerMode);
        }
        var filterFile = Path.Combine(Work(), "atlas_filter.txt");
        var filter = File.Exists(filterFile) ? File.ReadAllLines(filterFile).Select(l => l.Trim()).Where(l => l.Length > 0).ToArray() : new string[0];
        var spec = LoadSpec();
        var built = Build(spec, filter);
        foreach (var t in built) { Log("HASH after build " + Sha1(t) + " " + t); }
        if (built.Count > 0) File.WriteAllText(Path.Combine(Work(), "atlas_yaml_sample.txt"), File.ReadAllText(built[0]));
        Probe("EDIT", spec, built);
        SessionState.SetString(Targets, string.Join("\n", built));
        SessionState.SetString(Phase, "play");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        var phase = SessionState.GetString(Phase, "");
        if (phase == "") return;
        if (phase == "play" && EditorApplication.isPlaying)
        {
            if (++frames < 30) return;
            var spec = LoadSpec(); var targets = SessionState.GetString(Targets, "").Split('\n').Where(x => x.Length > 0).ToList();
            try { Probe("PLAY", spec, targets); } catch (Exception e) { Log("PLAY PROBE EXCEPTION " + e); }
            Log("atlasRequested callbacks during play: " + atlasRequests);
            SessionState.SetString(Phase, "exit");
            EditorApplication.ExitPlaymode();
        }
        else if (phase == "exit" && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetString(Phase, "");
            var spec = LoadSpec(); var targets = SessionState.GetString(Targets, "").Split('\n').Where(x => x.Length > 0).ToList();
            foreach (var t in targets) { AssetDatabase.ImportAsset(t, ImportAssetOptions.ForceUpdate); Log("HASH after play " + Sha1(t) + " " + t); }
            try { Probe("AFTER", spec, targets); } catch (Exception e) { Log("AFTER PROBE EXCEPTION " + e); }
            if (SessionState.GetInt(OldMode, -1) >= 0) { EditorSettings.spritePackerMode = (SpritePackerMode)SessionState.GetInt(OldMode, 4); Log("spritePackerMode restored -> " + EditorSettings.spritePackerMode); }
            Log("BDSP_ATLAS_PROBE_DONE");
            EditorApplication.Exit(0);
        }
    }
}
