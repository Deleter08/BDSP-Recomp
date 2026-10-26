// BDSP PC rebuild - Windows build, s39 (HANDOFF "s39"). Editor only, NOT shipped code. Experiment, nothing runs by itself:
// `-executeMethod BdspWinBuild.AtlasBuildProbe.Run` (the editor exits 0 / 1).
//
// Why: the s38 subset gate found every packed sprite of a Windows-built bundle WITHOUT its texture (shareduiassets/sharedui: 322/322
// m_RD.texture null) and the SpriteAtlas WITHOUT render data / pages (shipped: 322 + 2 pages). The build late-binds the sprites to an
// atlas the packer never packed (packer Disabled, s32 law 3). This builds ONLY shareduiassets/sharedui under three variants, each into
// ../work/winbuild/atlas_probe/<variant>/, to learn what makes the build keep the sprite's own (baked, s32) page texture:
//   P1_tagsCleared      the sprite assets' serialized m_AtlasTags emptied (AssetRipper wrote the runtime tags into the .asset files)
//   P2_includeInBuild   the atlas's "Include in Build" (m_EditorData.bindAsDefault) on, sprites as they are
//   P3_both             both
// Every touched file (the atlas + the bundle's sprite .asset files) is backed up first, written back byte for byte after each variant
// and re-imported; the report ends with the restore check. Report ../work/winbuild/atlas_probe/report_<stamp>.txt, marker
// BDSP_ATLASPROBE_DONE OK|FAILED.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace BdspWinBuild
{
    public static class AtlasBuildProbe
    {
        const string Bundle = "shareduiassets/sharedui";
        static readonly Regex Tags = new Regex(@"(?m)^  m_AtlasTags:\n(?:  - [^\n]*\n)+");

        static string Project { get { return Path.GetFullPath(Directory.GetCurrentDirectory()); } }
        static string Root { get { return Path.GetFullPath(Path.Combine(Project, "..")); } }

        public static void Run()
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string outBase = Path.Combine(Root, "work", "winbuild", "atlas_probe");
            Directory.CreateDirectory(outBase);
            var rep = new StringBuilder();
            Action<string> say = s => { rep.Append(s).Append('\n'); UnityEngine.Debug.Log("[AtlasProbe] " + s); };
            bool ok = false;
            var backup = new Dictionary<string, byte[]>();
            try
            {
                say("atlas build probe " + stamp + ", spritePackerMode " + EditorSettings.spritePackerMode);
                string[] paths = null, keys = null;
                foreach (var line in File.ReadAllLines(Path.Combine(Root, "work", "winbuild", "bundle_plan_subset.tsv"), Encoding.UTF8).Skip(1))
                {
                    var c = line.Split('\t');
                    if (c.Length < 5 || c[1] != Bundle) continue;
                    int n = int.Parse(c[4]);
                    paths = Enumerable.Range(0, n).Select(k => c[5 + 2 * k]).ToArray();
                    keys = Enumerable.Range(0, n).Select(k => c[6 + 2 * k]).ToArray();
                }
                if (paths == null) { say("ERROR " + Bundle + " not in bundle_plan_subset.tsv"); return; }
                string atlasPath = paths.Single(p => p.EndsWith(".spriteatlas", StringComparison.OrdinalIgnoreCase));
                var spritePaths = paths.Where(p => p.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)).Distinct().ToArray();
                say(string.Format("{0}: {1} assets, atlas {2}, sprite assets {3}", Bundle, paths.Length, atlasPath, spritePaths.Length));

                // backup (memory + disk)
                string bdir = Path.Combine(outBase, "backup_" + stamp);
                foreach (var p in new[] { atlasPath }.Concat(spritePaths))
                {
                    backup[p] = File.ReadAllBytes(Path.Combine(Project, p));
                    string dst = Path.Combine(bdir, p);
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    File.WriteAllBytes(dst, backup[p]);
                }
                say("backed up " + backup.Count + " files to " + bdir);

                foreach (var variant in new[] { "P1_tagsCleared", "P2_includeInBuild", "P3_both" })
                {
                    bool clearTags = variant != "P2_includeInBuild", include = variant != "P1_tagsCleared";
                    if (clearTags)
                    {
                        int edited = 0;
                        AssetDatabase.StartAssetEditing();
                        try
                        {
                            foreach (var p in spritePaths)
                            {
                                string text = Encoding.UTF8.GetString(backup[p]);
                                string now = Tags.Replace(text, "  m_AtlasTags: []\n", 1);
                                if (now == text) continue;
                                File.WriteAllBytes(Path.Combine(Project, p), new UTF8Encoding(false).GetBytes(now));
                                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                                edited++;
                            }
                        }
                        finally { AssetDatabase.StopAssetEditing(); }
                        say(variant + ": m_AtlasTags emptied in " + edited + " sprite files");
                    }
                    if (include)
                    {
                        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                        atlas.SetIncludeInBuild(true);
                        EditorUtility.SetDirty(atlas);
                        AssetDatabase.SaveAssets();
                        say(variant + ": Include in Build on");
                    }
                    var probe = AssetDatabase.LoadAssetAtPath<Sprite>(spritePaths[0]);
                    say(string.Format("  editor sprite {0}: texture {1}, packed {2}", spritePaths[0], probe == null || probe.texture == null ? "null" : probe.texture.name, probe != null && probe.packed));

                    string dir = Path.Combine(outBase, variant);
                    Directory.CreateDirectory(dir);
                    var t0 = DateTime.Now;
                    var m = BuildPipeline.BuildAssetBundles(dir, new[] { new AssetBundleBuild { assetBundleName = Bundle, assetNames = paths, addressableNames = keys } },
                        BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64);
                    var f = new FileInfo(Path.Combine(dir, Bundle));
                    say(string.Format("  built {0} in {1:F1} s: manifest {2}, file {3}", variant, (DateTime.Now - t0).TotalSeconds, m != null, f.Exists ? f.Length + " bytes" : "MISSING"));

                    Restore(backup, atlasPath, say);
                }
                ok = true;
            }
            catch (Exception e)
            {
                say("ERROR exception " + e);
            }
            finally
            {
                // final restore check: every file byte-identical to the backup
                int same = 0, differ = 0;
                foreach (var kv in backup)
                {
                    string p = Path.Combine(Project, kv.Key);
                    if (File.Exists(p) && File.ReadAllBytes(p).SequenceEqual(kv.Value)) same++;
                    else
                    {
                        differ++;
                        File.WriteAllBytes(p, kv.Value);
                        AssetDatabase.ImportAsset(kv.Key, ImportAssetOptions.ForceUpdate);
                    }
                }
                say(string.Format("restore check: {0} byte-identical, {1} rewritten now", same, differ));
                if (differ > 0) ok = false;
                string path = Path.Combine(Path.Combine(Root, "work", "winbuild", "atlas_probe"), "report_" + stamp + ".txt");
                File.WriteAllText(path, rep.ToString(), new UTF8Encoding(false));
                UnityEngine.Debug.Log("BDSP_ATLASPROBE_DONE " + (ok ? "OK" : "FAILED") + " report " + path);
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }

        // `-executeMethod BdspWinBuild.AtlasBuildProbe.RuntimeCheck`: loads Windows-built bundles the way the player does
        // (AssetBundle.LoadFromFile, ../build/bundles_win64_subset unless ../work/winbuild/atlas_probe/runtime_dir.txt names another),
        // takes every SpriteAtlas in them, and for each: GetSprites count / with texture, GetSprite on the first 3 packed names, and a
        // SpriteRenderer render of GetSprite(first) into a 128x128 RT (non-black pixels). Marker BDSP_ATLASRUNTIME_DONE OK|FAILED;
        // report ../work/winbuild/atlas_probe/runtime_<stamp>.txt + one PNG per atlas.
        public static void RuntimeCheck()
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string outBase = Path.Combine(Root, "work", "winbuild", "atlas_probe");
            Directory.CreateDirectory(outBase);
            var rep = new StringBuilder();
            Action<string> say = s => { rep.Append(s).Append('\n'); UnityEngine.Debug.Log("[AtlasRuntime] " + s); };
            bool ok = false;
            var loaded = new List<AssetBundle>();
            try
            {
                string dirFile = Path.Combine(outBase, "runtime_dir.txt");
                string dir = File.Exists(dirFile) ? File.ReadAllText(dirFile).Trim() : Path.Combine(Root, "build", "bundles_win64_subset");
                var names = new[] { "shareduiassets/sharedui", "shareduiassets/battle_lang.en" };
                say("runtime atlas check " + stamp + " in " + dir + ", spritePackerMode " + EditorSettings.spritePackerMode);
                int atlases = 0, bad = 0;
                foreach (var n in names)
                {
                    var ab = AssetBundle.LoadFromFile(Path.Combine(dir, n));
                    if (ab == null) { say("ERROR cannot load " + n); bad++; continue; }
                    loaded.Add(ab);
                    foreach (var atlas in ab.LoadAllAssets<SpriteAtlas>())
                    {
                        atlases++;
                        var all = new Sprite[Math.Max(1, atlas.spriteCount)];
                        int got = atlas.spriteCount > 0 ? atlas.GetSprites(all) : 0;
                        int tex = all.Count(s => s != null && s.texture != null);
                        say(string.Format("{0}: atlas '{1}' tag {2} spriteCount {3} GetSprites {4} with texture {5}", n, atlas.name, atlas.tag, atlas.spriteCount, got, tex));
                        if (got == 0 || tex != got) bad++;
                        Sprite first = null;
                        foreach (var s in all.Where(x => x != null).Take(3))
                        {
                            string key = s.name.EndsWith("(Clone)", StringComparison.Ordinal) ? s.name.Substring(0, s.name.Length - 7) : s.name;
                            var g = atlas.GetSprite(key);
                            if (first == null) first = g;
                            say(g == null ? "   GetSprite(" + key + ") = null" : string.Format("   GetSprite({0}) texture {1} {2}x{3} textureRect {4} uv0 {5}", key,
                                g.texture == null ? "null" : g.texture.name, g.texture == null ? 0 : g.texture.width, g.texture == null ? 0 : g.texture.height, g.textureRect,
                                g.uv.Length > 0 ? g.uv[0].ToString("F4") : "-"));
                            if (g == null || g.texture == null) bad++;
                        }
                        if (first != null && first.texture != null)
                        {
                            int cover = Render(first, Path.Combine(outBase, "runtime_" + stamp + "_" + atlas.name + ".png"));
                            say("   RENDER GetSprite(" + first.name + "): covered " + cover + "/16384");
                            if (cover == 0) bad++;
                        }
                    }
                }
                say(string.Format("atlases {0}, problems {1}", atlases, bad));
                ok = atlases > 0 && bad == 0;
            }
            catch (Exception e)
            {
                say("ERROR exception " + e);
            }
            finally
            {
                foreach (var ab in loaded) ab.Unload(true);
                string path = Path.Combine(outBase, "runtime_" + stamp + ".txt");
                File.WriteAllText(path, rep.ToString(), new UTF8Encoding(false));
                UnityEngine.Debug.Log("BDSP_ATLASRUNTIME_DONE " + (ok ? "OK" : "FAILED") + " report " + path);
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }

        static int Render(Sprite sprite, string png)
        {
            var camGo = new GameObject("AtlasProbeCam");
            var go = new GameObject("AtlasProbeSprite");
            var rt = RenderTexture.GetTemporary(128, 128, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(sprite.bounds.extents.x, sprite.bounds.extents.y) * 1.1f + 0.01f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
                cam.transform.position = new Vector3(0, 0, -10);
                go.AddComponent<SpriteRenderer>().sprite = sprite;
                go.transform.position = -sprite.bounds.center;
                cam.targetTexture = rt;
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                cam.targetTexture = null;
                File.WriteAllBytes(png, tex.EncodeToPNG());
                return tex.GetPixels32().Count(p => p.r + p.g + p.b > 0);   // the camera writes alpha 1 everywhere: count non-black
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        static void Restore(Dictionary<string, byte[]> backup, string atlasPath, Action<string> say)
        {
            int n = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var kv in backup)
                {
                    string p = Path.Combine(Project, kv.Key);
                    if (File.ReadAllBytes(p).SequenceEqual(kv.Value)) continue;
                    File.WriteAllBytes(p, kv.Value);
                    AssetDatabase.ImportAsset(kv.Key, ImportAssetOptions.ForceUpdate);
                    n++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            say("  restored " + n + " files");
        }
    }
}
