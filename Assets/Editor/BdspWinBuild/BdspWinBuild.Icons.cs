// Optional user-supplied app icon for iOS and Android (Assets/_Ours/Icon/boxart_1024.png).
// Applied in memory before BuildPipeline.BuildPlayer (the caller restores ProjectSettings.asset afterwards,
// so nothing sticks). The macOS build never calls this.
//   iOS:     every icon slot of every kind (incl. the App Store 1024) gets the art, flattened on black (iOS icons must have no alpha).
//   Android: Adaptive = black background layer + art scaled to the 66% safe zone as the foreground layer; Round = art in a circle;
//            Legacy = the art, square. All densities (each slot gets a texture of its own size).
// Derived textures are written to Assets/_Ours/Icon/Generated (git-ignored) because PlayerSettings icons must be project assets.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace BdspWinBuild
{
    public static partial class WinBuild
    {
        const string IconSource = "Assets/_Ours/Icon/boxart_1024.png";
        const string IconGenDir = "Assets/_Ours/Icon/Generated";

        static Texture2D EnsureIconSource(Report r)
        {
            if (!File.Exists(Path.Combine(Project, IconSource)))
            {
                r.Warn("no optional " + IconSource + " - keeping the default icon; supply your own 1024x1024 PNG to override it");
                return null;
            }
            AssetDatabase.ImportAsset(IconSource, ImportAssetOptions.ForceSynchronousImport);
            var imp = AssetImporter.GetAtPath(IconSource) as TextureImporter;
            if (imp == null) { r.Error(IconSource + " is not imported as a texture"); return null; }
            if (imp.textureType != TextureImporterType.Default || imp.textureCompression != TextureImporterCompression.Uncompressed || !imp.isReadable ||
                imp.mipmapEnabled || imp.npotScale != TextureImporterNPOTScale.None || imp.maxTextureSize < 1024 || imp.alphaSource != TextureImporterAlphaSource.None)
            {
                imp.textureType = TextureImporterType.Default;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.isReadable = true;
                imp.mipmapEnabled = false;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.maxTextureSize = 2048;
                imp.alphaSource = TextureImporterAlphaSource.None;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(IconSource);
        }

        // size x size RGBA, bilinear-resampled from the source. scale = fraction of the canvas the art covers (centred); circle = alpha-mask to a disc.
        // background: opaque fill behind the art (alpha 0 -> transparent canvas).
        static Texture2D MakeIconTexture(Texture2D src, int size, float scale, Color background, bool circle)
        {
            var px = new Color32[size * size];
            Color32 bg = background;
            float half = size * 0.5f, art = size * scale, lo = half - art * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Color32 c = bg;
                    float u = (x + 0.5f - lo) / art, v = (y + 0.5f - lo) / art;
                    if (u >= 0f && u <= 1f && v >= 0f && v <= 1f)
                    {
                        Color s = src.GetPixelBilinear(u, v);
                        c = Color.Lerp(background, new Color(s.r, s.g, s.b, 1f), s.a);
                        c.a = (byte)Mathf.Max(bg.a, 255 * s.a);
                    }
                    if (circle)
                    {
                        float dx = x + 0.5f - half, dy = y + 0.5f - half, d = Mathf.Sqrt(dx * dx + dy * dy);
                        c.a = (byte)(c.a * Mathf.Clamp01(half - d));      // 1px anti-aliased edge
                    }
                    px[y * size + x] = c;
                }
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        // the PNG as a project asset (icons must be assets); unchanged content is not re-imported
        static Texture2D SaveIconAsset(Texture2D t, string name)
        {
            Directory.CreateDirectory(Path.Combine(Project, IconGenDir));
            string asset = IconGenDir + "/" + name + ".png", disk = Path.Combine(Project, asset);
            byte[] png = t.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(t);
            if (!File.Exists(disk) || !System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(disk), png))
                File.WriteAllBytes(disk, png);
            AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport);
            var imp = AssetImporter.GetAtPath(asset) as TextureImporter;
            if (imp != null && (imp.textureCompression != TextureImporterCompression.Uncompressed || imp.mipmapEnabled || !imp.isReadable || imp.npotScale != TextureImporterNPOTScale.None))
            {
                imp.textureType = TextureImporterType.Default;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.isReadable = true;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.alphaIsTransparency = false;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(asset);
        }

        // the static name ("Adaptive", "Round", "Legacy", ...) of a PlatformIconKind, by finding which static member of its own type it is
        static string IconKindName(object kind)
        {
            foreach (var p in kind.GetType().GetProperties(BindingFlags.Public | BindingFlags.Static))
                if (p.PropertyType.IsAssignableFrom(kind.GetType()) && Equals(p.GetValue(null, null), kind)) return p.Name;
            foreach (var f in kind.GetType().GetFields(BindingFlags.Public | BindingFlags.Static))
                if (f.FieldType.IsAssignableFrom(kind.GetType()) && Equals(f.GetValue(null), kind)) return f.Name;
            return kind.ToString();
        }

        // Missing optional art succeeds with a warning. Invalid supplied art remains an error.
        static bool ApplyBoxArtIcons(Report r, NamedBuildTarget target, bool android)
        {
            if (!File.Exists(Path.Combine(Project, IconSource)))
            {
                EnsureIconSource(r); // warning only; do not change any PlayerSettings icon slots
                return true;
            }
            Texture2D src = EnsureIconSource(r);
            if (src == null) return false;
            var cache = new Dictionary<string, Texture2D>();
            Func<string, int, Func<Texture2D>, Texture2D> get = (key, size, make) =>
            {
                string k = key + size;
                Texture2D t;
                if (!cache.TryGetValue(k, out t)) cache[k] = t = SaveIconAsset(make(), (android ? "android_" : "ios_") + key + "_" + size);
                return t;
            };
            int slots = 0, textures = 0;
            var summary = new List<string>();
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(target))
            {
                string kn = IconKindName(kind);
                var icons = PlayerSettings.GetPlatformIcons(target, kind);
                foreach (var icon in icons)
                {
                    int size = Mathf.Max(icon.width, icon.height), layers = Mathf.Max(1, icon.maxLayerCount);
                    var tex = new Texture2D[layers];
                    if (!android)
                    {
                        for (int i = 0; i < layers; i++)
                            tex[i] = i == 0 ? get("flat", size, () => MakeIconTexture(src, size, 1f, Color.black, false)) : null;
                    }
                    else if (kn == "Adaptive" && layers >= 2)
                    {
                        tex[0] = get("adaptive_bg", size, () => MakeIconTexture(src, size, 0f, Color.black, false));
                        tex[1] = get("adaptive_fg", size, () => MakeIconTexture(src, size, 0.66f, new Color(0, 0, 0, 0), false));
                    }
                    else if (kn == "Round")
                        tex[0] = get("round", size, () => MakeIconTexture(src, size, 1f, Color.black, true));
                    else
                        tex[0] = get("legacy", size, () => MakeIconTexture(src, size, 1f, Color.black, false));
                    icon.SetTextures(tex);
                    slots++;
                    textures += layers;
                }
                PlayerSettings.SetPlatformIcons(target, kind, icons);
                summary.Add(kn + " x" + icons.Length);
            }
            if (slots == 0) { r.Error("no icon slots found for " + target.TargetName + " - box art not applied"); return false; }
            AssetDatabase.SaveAssets();
            r.Line("app icon (box art): " + slots + " slots set for " + target.TargetName + " (" + string.Join(", ", summary.ToArray()) + ")");
            return true;
        }
    }
}
