// CustomBDSP editor tooling (s32 logic, rewritten for the clean project in s46 C4). NOT shipped code: the developers' own
// EditorAssetAssistant.dll never shipped with the game.
//
// The shipped Sequencer.editorProxy getter (editor only) loads "Library/ScriptAssemblies/Assembly-CSharp-Editor.dll" when
// AssetAssistant is compiled into Assembly-CSharp, then calls the static SmartPoint.EditorAssetAssistant.UnityEditorProxy.GetInstance().
// This class is that entry point. It serves the editor simulation path (StartupSettings._assetBundleTarget = AssetDatabase): our loose
// assets came out of AssetRipper without bundle labels, so the bundle -> asset table is read from the shipped bundles' own container
// tables - tools/bundle_containers.py -> Assets/Editor/EditorAssetAssistant/bundle_map.tsv, one line per bundle:
//   <bundle name> TAB <isScene> TAB (<dev path> TAB <project path>)*
// A dev path is the container path as the game asks for it (AssetBundleRecord lower-cases it, SceneDatabase keeps scene paths); the
// project path is where that asset lives in this project (empty = same as the dev path).
using System;
using System.Collections.Generic;
using System.IO;
using SmartPoint.AssetAssistant;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmartPoint.EditorAssetAssistant
{
    public sealed class UnityEditorProxy : IUnityEditorProxy
    {
        public const string MapPath = "Assets/Editor/EditorAssetAssistant/bundle_map.tsv";

        static UnityEditorProxy instance;

        // Called by reflection from Sequencer.editorProxy.
        public static IUnityEditorProxy GetInstance()
        {
            if (instance == null)
            {
                instance = new UnityEditorProxy();
                LoadSingletonSettings();
            }
            return instance;
        }

        // In a player, SingletonScriptableObject<T>.instance loads Resources/<T.Name>. In the editor the shipped getter only checks that
        // this proxy exists (logging "Create Editor Proxy") and returns whatever instance the asset's OnEnable registered - so the proxy
        // brings those settings assets in when it is created: every SingletonScriptableObject type, by the name the player loads.
        static void LoadSingletonSettings()
        {
            Type open = typeof(SingletonScriptableObject<>);
            var loaded = new List<string>();
            foreach (Type t in open.Assembly.GetTypes())
            {
                if (t.IsAbstract || t.ContainsGenericParameters)
                    continue;
                for (Type b = t.BaseType; b != null; b = b.BaseType)
                {
                    if (b.IsGenericType && b.GetGenericTypeDefinition() == open)
                    {
                        try
                        {
                            if (Resources.Load(t.Name) != null)
                                loaded.Add(t.Name);
                            else
                                UnityEngine.Debug.LogWarning("[UnityEditorProxy] no Resources/" + t.Name + " for " + t.FullName);
                        }
                        catch (Exception e)
                        {
                            // one failing load must not leave the other settings unloaded
                            UnityEngine.Debug.LogWarning("[UnityEditorProxy] Resources/" + t.Name + ": " + e.GetType().Name + ": " + e.Message);
                        }
                        break;
                    }
                }
            }
            UnityEngine.Debug.Log("[UnityEditorProxy] settings assets loaded: " + string.Join(", ", loaded.ToArray()));
        }

        sealed class BundleMap
        {
            public readonly Dictionary<string, string[]> devPathsOfBundle = new Dictionary<string, string[]>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> projectPathOf = new Dictionary<string, string>(StringComparer.Ordinal);   // key: lower-case dev path
            public readonly Dictionary<string, string> bundleOf = new Dictionary<string, string>(StringComparer.Ordinal);        // key: lower-case dev path
            public string[] sortedNames = new string[0];
            public string[] variantNames = new string[0];
        }

        BundleMap map;

        BundleMap Map
        {
            get
            {
                if (map == null)
                    map = Read();
                return map;
            }
        }

        static BundleMap Read()
        {
            var m = new BundleMap();
            if (!File.Exists(MapPath))
            {
                UnityEngine.Debug.LogError("[UnityEditorProxy] missing " + MapPath + " - regenerate it with tools/bundle_containers.py map --tsv");
                return m;
            }
            var variants = new List<string>();
            foreach (string raw in File.ReadAllLines(MapPath))
            {
                if (raw.Length == 0 || raw[0] == '#')
                    continue;
                string[] col = raw.Split('\t');
                string bundle = col[0];
                int pairs = (col.Length - 2) / 2;
                var dev = new string[pairs];
                for (int p = 0; p < pairs; p++)
                {
                    dev[p] = col[2 + 2 * p];
                    string key = dev[p].ToLowerInvariant();
                    string project = col[3 + 2 * p];
                    if (project.Length > 0 && !m.projectPathOf.ContainsKey(key))
                        m.projectPathOf[key] = project;
                    if (!m.bundleOf.ContainsKey(key))
                        m.bundleOf[key] = bundle;
                }
                m.devPathsOfBundle[bundle] = dev;
                // a variant bundle is "<name>.<variant>": a dot after the last slash
                if (bundle.LastIndexOf('.') > bundle.LastIndexOf('/'))
                    variants.Add(bundle);
            }
            var names = new List<string>(m.devPathsOfBundle.Keys);
            names.Sort(StringComparer.Ordinal);
            m.sortedNames = names.ToArray();
            m.variantNames = variants.ToArray();
            UnityEngine.Debug.Log("[UnityEditorProxy] " + MapPath + ": " + m.sortedNames.Length + " bundles, " + m.bundleOf.Count + " asset paths, "
                + m.variantNames.Length + " variant bundles");
            return m;
        }

        string ProjectPath(string devPath)
        {
            string p;
            return devPath != null && Map.projectPathOf.TryGetValue(devPath.ToLowerInvariant(), out p) ? p : devPath;
        }

        // Only a material whose shader cannot draw here (a Switch shader object from a bundle) is pointed at the project shader of the
        // same name. Project materials are left alone: in this mode they ARE the assets on disk, and a change would be saved.
        static void FixMaterial(Material material)
        {
            if (material == null || EditorUtility.IsPersistent(material))
                return;
            Shader current = material.shader;
            if (current == null || current.isSupported)
                return;
            Shader local = Shader.Find(current.name);
            if (local != null && local != current)
                material.shader = local;
        }

        sealed class Finished : CustomYieldInstruction
        {
            public override bool keepWaiting { get { return false; } }
        }

        public string[] GetAllAssetBundleNames()
        {
            return (string[])Map.sortedNames.Clone();
        }

        public string GetAssetBundleNameAtPath(string path)
        {
            string bundle;
            if (path != null && Map.bundleOf.TryGetValue(path.ToLowerInvariant(), out bundle))
                return bundle;
            return string.Empty;
        }

        public bool HasAssetBundleNameAtPath(string path)
        {
            return GetAssetBundleNameAtPath(path).Length != 0;
        }

        // Same rule as the download manifest's own lookup: drop any variant suffix, then take the first requested variant that exists.
        public string FindMatchAssetBundleNameWithVariants(string assetBundleName, string[] variants)
        {
            string baseName = assetBundleName;
            int dot = baseName.IndexOf('.');
            if (dot >= 0)
                baseName = baseName.Substring(0, dot);
            if (variants != null)
            {
                for (int i = 0; i < variants.Length; i++)
                {
                    string candidate = baseName + "." + variants[i];
                    if (Map.devPathsOfBundle.ContainsKey(candidate))
                        return candidate;
                }
            }
            return baseName;
        }

        public string[] GetAssetBundleNamesWithVariant()
        {
            return (string[])Map.variantNames.Clone();
        }

        // A new array on every call - AssetBundleRecord lower-cases the array it gets in place.
        public string[] GetAssetPathsFromAssetBundle(string assetBundleName)
        {
            string[] dev;
            if (assetBundleName != null && Map.devPathsOfBundle.TryGetValue(assetBundleName, out dev))
                return (string[])dev.Clone();
            UnityEngine.Debug.LogWarning("[UnityEditorProxy] unknown asset bundle: " + assetBundleName);
            return new string[0];
        }

        public AsyncOperation LoadLevelAdditiveAsyncInPlayMode(string path)
        {
            return EditorSceneManager.LoadSceneAsyncInPlayMode(ProjectPath(path), new LoadSceneParameters(LoadSceneMode.Additive));
        }

        public UnityEngine.Object LoadMainAssetAtPath(string path)
        {
            string project = ProjectPath(path);
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(project);
            if (asset == null)
                UnityEngine.Debug.LogWarning("[UnityEditorProxy] nothing at " + path + (project != path ? " -> " + project : ""));
            return asset;
        }

        public void ReloadEditorSkyboxShader()
        {
            FixMaterial(RenderSettings.skybox);
        }

        public void ReloadEditorShadersInScene(Scene scene)
        {
            if (!scene.IsValid())
                return;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                ReloadEditorShaders(roots[i]);
        }

        public void ReloadEditorShaders(UnityEngine.Object asset)
        {
            var material = asset as Material;
            if (material != null)
            {
                FixMaterial(material);
                return;
            }
            var go = asset as GameObject;
            if (go == null)
                return;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                    FixMaterial(shared[i]);
            }
        }

        // Every variant bundle has its own container paths, so the project asset found for a variant path already is that variant.
        public void ReloadVariantAssets(UnityEngine.Object asset, string[] variants)
        {
        }

        public bool IsSceneAssetAtPath(string path)
        {
            return path != null && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);   // no shipped caller
        }

        public CustomYieldInstruction ReloadEditorShadersOperation(UnityEngine.Object[] assets)
        {
            if (assets != null)
            {
                for (int i = 0; i < assets.Length; i++)
                    ReloadEditorShaders(assets[i]);
            }
            return new Finished();
        }
    }
}
