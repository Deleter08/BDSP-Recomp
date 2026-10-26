// BDSP PC rebuild - sprite atlas indexes for the Unity 6 bundle build (2026-10-22). Editor only, NOT shipped code.
//
// Unity 6 (2023.2+) writes a SpriteAtlas into a player bundle as m_RenderDataMap only - the m_PackedSprites /
// m_PackedSpriteNamesToIndex lists of the game's ripped, never-packed atlases are dropped, so the built atlases are empty and
// SpriteAtlas.GetSprite returns null (Unity 2019.4 keeps both lists; see Scripts/_Ours/PcPlatform/PcSpriteAtlas.cs). For every
// .spriteatlas in the plan this writes a PcPlatform.PcSpriteAtlasIndex (the atlas + its m_PackedSprites) and adds it to the atlas's
// bundle under PcSpriteAtlas.IndexKey - one extra container key per atlas, which the game never asks for (as VariantsKey). Its
// sprite references pull the sprites into the bundle the way 2019.4's m_PackedSprites did (measured: textures/trainercard shipped
// its 18 sprites that way; the Unity 6 build had none of them). Derived assets in the git-ignored SharedAssets tree.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

namespace BdspWinBuild
{
    public static partial class WinBuild
    {
        const string AtlasIndexRoot = "Assets/SharedAssets/pcplatform_spriteatlasindex";

        // adds an index asset + key to every plan row that carries a .spriteatlas; false on any error
        static bool AddAtlasIndexes(Report r, List<PlanRow> rows)
        {
#if UNITY_2023_2_OR_NEWER
            int atlases = 0, sprites = 0;
            foreach (var row in rows)
            {
                var atlasPaths = row.Paths.Where(p => p.EndsWith(".spriteatlas", System.StringComparison.OrdinalIgnoreCase)).ToArray();
                if (atlasPaths.Length == 0)
                    continue;
                var paths = new List<string>(row.Paths);
                var keys = new List<string>(row.Keys);
                foreach (var atlasPath in atlasPaths)
                {
                    var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                    if (atlas == null)
                    {
                        r.Error("atlas index: no SpriteAtlas at " + atlasPath + " (" + row.FullName + ")");
                        return false;
                    }
                    int packedCount;
                    var list = PackedSprites(atlasPath, out packedCount);
                    if (packedCount == 0)
                        r.Warn("atlas index: no m_PackedSprites in " + atlasPath + " (" + row.FullName + ")");
                    else if (list.Count != packedCount)
                        r.Warn(string.Format("atlas index: {0} ({1}): {2} of {3} packed sprites resolve", atlasPath, row.FullName, list.Count, packedCount));
                    string key = PcPlatform.PcSpriteAtlas.IndexKey(atlas.name);
                    if (keys.Contains(key))
                    {
                        r.Error("atlas index: two atlases named " + atlas.name + " in " + row.FullName);
                        return false;
                    }
                    string indexPath = AtlasIndexRoot + "/" + row.FullName.Replace('/', '_') + "/" + atlas.name + ".asset";
                    WriteAtlasIndex(indexPath, atlas, list.ToArray());
                    paths.Add(indexPath);
                    keys.Add(key);
                    atlases++;
                    sprites += list.Count;
                }
                row.Paths = paths.ToArray();
                row.Keys = keys.ToArray();
            }
            // atlases a row only references (BalloonIconAtlas: serialized in the field canvas prefab, in no row's Paths) are built
            // into that bundle implicitly and got no index - the "!" balloons drew as white quads (iPhone, 2026-10-24). Index every
            // such atlas in each row that pulls it in; the runtime matches index.atlas to the instance, so per-bundle copies are fine.
            var explicitAtlases = new HashSet<string>(rows.SelectMany(rw => rw.Paths)
                .Where(p => p.EndsWith(".spriteatlas", System.StringComparison.OrdinalIgnoreCase)), System.StringComparer.OrdinalIgnoreCase);
            int implicitAtlases = 0;
            foreach (var row in rows)
            {
                var deps = AssetDatabase.GetDependencies(row.Paths, true)
                    .Where(p => p.EndsWith(".spriteatlas", System.StringComparison.OrdinalIgnoreCase) && !explicitAtlases.Contains(p))
                    .Distinct().ToArray();
                if (deps.Length == 0)
                    continue;
                var paths = new List<string>(row.Paths);
                var keys = new List<string>(row.Keys);
                foreach (var atlasPath in deps)
                {
                    var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                    if (atlas == null)
                        continue;
                    string key = PcPlatform.PcSpriteAtlas.IndexKey(atlas.name);
                    if (keys.Contains(key))
                        continue;
                    int packedCount;
                    var list = PackedSprites(atlasPath, out packedCount);
                    if (list.Count == 0)
                        continue;
                    string indexPath = AtlasIndexRoot + "/" + row.FullName.Replace('/', '_') + "/" + atlas.name + ".asset";
                    WriteAtlasIndex(indexPath, atlas, list.ToArray());
                    paths.Add(indexPath);
                    keys.Add(key);
                    implicitAtlases++;
                    sprites += list.Count;
                    r.Line("atlas index (referenced): " + atlasPath + " -> " + row.FullName + " (" + list.Count + " sprites)");
                }
                row.Paths = paths.ToArray();
                row.Keys = keys.ToArray();
            }
            r.Line(string.Format("atlas indexes: {0} atlases + {1} referenced, {2} sprites ({3})", atlases, implicitAtlases, sprites, AtlasIndexRoot));
#endif
            return true;
        }

#if UNITY_2023_2_OR_NEWER
        static readonly Regex PackedRef = new Regex(@"^  - \{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: \d+\}$");

        // the atlas's m_PackedSprites, read from its YAML (the editor's SerializedObject does not expose the list in Unity 6)
        static List<Sprite> PackedSprites(string atlasPath, out int count)
        {
            var list = new List<Sprite>();
            count = 0;
            bool inList = false;
            foreach (var line in File.ReadAllLines(atlasPath))
            {
                if (line == "  m_PackedSprites:") { inList = true; continue; }
                if (!inList)
                    continue;
                var m = PackedRef.Match(line);
                if (!m.Success)
                    break;
                count++;
                long fileId = long.Parse(m.Groups[1].Value);
                string path = AssetDatabase.GUIDToAssetPath(m.Groups[2].Value);
                if (string.IsNullOrEmpty(path))
                    continue;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    string guid; long local;
                    var s = o as Sprite;
                    if (s != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out guid, out local) && local == fileId)
                    {
                        list.Add(s);
                        break;
                    }
                }
            }
            return list;
        }

        static void WriteAtlasIndex(string path, SpriteAtlas atlas, Sprite[] sprites)
        {
            var index = AssetDatabase.LoadAssetAtPath<PcPlatform.PcSpriteAtlasIndex>(path);
            if (index != null && index.atlas == atlas && index.sprites != null && index.sprites.SequenceEqual(sprites))
                return;   // unchanged: the incremental bundle build sees no change
            if (index == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                index = ScriptableObject.CreateInstance<PcPlatform.PcSpriteAtlasIndex>();
                index.atlas = atlas;
                index.sprites = sprites;
                AssetDatabase.CreateAsset(index, path);
            }
            else
            {
                index.atlas = atlas;
                index.sprites = sprites;
                EditorUtility.SetDirty(index);
                AssetDatabase.SaveAssetIfDirty(index);
            }
        }
#endif
    }
}
