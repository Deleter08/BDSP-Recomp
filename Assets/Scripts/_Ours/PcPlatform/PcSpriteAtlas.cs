#if !SWITCH
// PC platform layer: SpriteAtlas.GetSprite for the game's sprite atlases in Unity 6 AssetBundles. NOT shipped code.
//
// The game's .spriteatlas assets are the shipped runtime atlases (AssetRipper): never packed in this project (sprite packer
// Disabled), they carry the sprites as m_PackedSprites + m_PackedSpriteNamesToIndex, and each sprite keeps its own baked page
// texture. Unity 2019.4 writes those two lists into the bundle and GetSprite(name) finds the sprite through them. Unity 6
// (2023.2+) dropped both from the player's SpriteAtlas (it keeps m_RenderDataMap, which only packing fills): all 59 atlases are
// built empty and every GetSprite returns null - an Image given that null sprite draws a flat quad in its color (measured
// 2026-10-22: the message window frames, the battle Bag icon, the move buttons' effectiveness bar).
// The Unity 6 bundle build therefore puts a PcSpriteAtlasIndex (the atlas + its m_PackedSprites) into each atlas's bundle under
// IndexKey(atlas name); GetSprite asks the atlas first and, only when that returns null, finds the sprite by name in the index.
// Unity 2019.4 (and any atlas Unity 6 did pack) never reaches the index.

// global namespace: the game's call sites (`atlas.GetSpritePc(name)` in their `#if !SWITCH` arm) need no extra using
public static class PcSpriteAtlasExtensions
{
    public static global::UnityEngine.Sprite GetSpritePc(this global::UnityEngine.U2D.SpriteAtlas atlas, string name)
    {
        return global::PcPlatform.PcSpriteAtlas.GetSprite(atlas, name);
    }
}

namespace PcPlatform
{
    public static class PcSpriteAtlas
    {
        const string IndexKeyPrefix = "assets/pcplatform/spriteatlasindex/";

        // container key of an atlas's index in its bundle (BdspWinBuild.AtlasIndex.cs writes the same key)
        public static string IndexKey(string atlasName) { return IndexKeyPrefix + atlasName.ToLowerInvariant() + ".asset"; }

        // atlas -> sprite name -> sprite (empty map = the atlas's bundle has no index)
        static readonly global::System.Collections.Generic.Dictionary<global::UnityEngine.U2D.SpriteAtlas, global::System.Collections.Generic.Dictionary<string, global::UnityEngine.Sprite>> maps =
            new global::System.Collections.Generic.Dictionary<global::UnityEngine.U2D.SpriteAtlas, global::System.Collections.Generic.Dictionary<string, global::UnityEngine.Sprite>>();

        static readonly global::System.Collections.Generic.HashSet<string> missLogged = new global::System.Collections.Generic.HashSet<string>();

        public static global::UnityEngine.Sprite GetSprite(global::UnityEngine.U2D.SpriteAtlas atlas, string name)
        {
            var sprite = atlas.GetSprite(name);
#if UNITY_2023_2_OR_NEWER
            if (sprite == null && !string.IsNullOrEmpty(name))
            {
                global::System.Collections.Generic.Dictionary<string, global::UnityEngine.Sprite> map;
                if (!maps.TryGetValue(atlas, out map))
                {
                    map = ReadIndex(atlas);
                    if (map.Count > 0)   // an empty map is retried: the index's bundle may load after the first lookup
                        maps[atlas] = map;
                }
                map.TryGetValue(name, out sprite);
                if (sprite == null && missLogged.Add(atlas.name + "/" + name))
                    global::UnityEngine.Debug.LogWarning("PcSpriteAtlas MISS atlas='" + atlas.name + "' sprite='" + name + "' indexSprites=" + map.Count);
            }
#endif
            return sprite;
        }

        // The bundle cache unloads a bundle (Unload(false)) as soon as its asset requests finish, so an atlas that outlives its
        // bundle (BalloonIconAtlas in FieldCanvas) found no index at lookup time: the white "!" balloons. AssetBundleCache calls this
        // just before Unload; the indexed sprites are already loaded objects and survive Unload(false).
        public static void HarvestIndexes(global::UnityEngine.AssetBundle bundle)
        {
            if (bundle == null || bundle.isStreamedSceneAssetBundle)
                return;
            string[] names;
            try { names = bundle.GetAllAssetNames(); } catch { return; }
            foreach (var n in names)
            {
                if (!n.StartsWith(IndexKeyPrefix, global::System.StringComparison.Ordinal))
                    continue;
                var index = bundle.LoadAsset<PcSpriteAtlasIndex>(n);
                if (index == null || index.atlas == null || index.sprites == null)
                    continue;
                global::System.Collections.Generic.Dictionary<string, global::UnityEngine.Sprite> map;
                if (maps.TryGetValue(index.atlas, out map) && map.Count > 0)
                    continue;
                map = new global::System.Collections.Generic.Dictionary<string, global::UnityEngine.Sprite>();
                foreach (var sp in index.sprites)
                    if (sp != null && !map.ContainsKey(sp.name))
                        map.Add(sp.name, sp);
                if (map.Count > 0)
                    maps[index.atlas] = map;
            }
        }

        static global::System.Collections.Generic.Dictionary<string, global::UnityEngine.Sprite> ReadIndex(global::UnityEngine.U2D.SpriteAtlas atlas)
        {
            var map = new global::System.Collections.Generic.Dictionary<string, global::UnityEngine.Sprite>(global::System.StringComparer.Ordinal);
            string key = IndexKey(atlas.name);
            PcSpriteAtlasIndex byName = null;
            // the language variants share the key: the index whose atlas IS this atlas is the one
            foreach (var bundle in global::UnityEngine.AssetBundle.GetAllLoadedAssetBundles())
            {
                if (bundle == null || bundle.isStreamedSceneAssetBundle || !bundle.Contains(key))
                    continue;
                var index = bundle.LoadAsset<PcSpriteAtlasIndex>(key);
                if (index == null || index.sprites == null)
                {
                    global::UnityEngine.Debug.LogWarning("PcSpriteAtlas index unreadable: bundle='" + bundle.name + "' key='" + key + "' index=" + (index == null ? "null" : "no sprites"));
                    continue;
                }
                if (index.atlas != atlas)
                {
                    // Same-named atlas from another copy (an atlas built implicitly into several bundles is one instance per
                    // bundle). Language variants share a key too, so only accept it when no exact match turns up.
                    if (byName == null && index.atlas != null && index.atlas.name == atlas.name)
                        byName = index;
                    global::UnityEngine.Debug.LogWarning("PcSpriteAtlas index atlas mismatch: bundle='" + bundle.name + "' index.atlas=" +
                        (index.atlas == null ? "null" : index.atlas.name + "#" + index.atlas.GetEntityId()) + " asked=" + atlas.name + "#" + atlas.GetEntityId());
                    continue;
                }
                foreach (var s in index.sprites)
                    if (s != null && !map.ContainsKey(s.name))
                        map.Add(s.name, s);
                return map;
            }
            if (byName != null)
                foreach (var s in byName.sprites)
                    if (s != null && !map.ContainsKey(s.name))
                        map.Add(s.name, s);
            if (map.Count == 0)
            {
                int n = 0;
                foreach (var b in global::UnityEngine.AssetBundle.GetAllLoadedAssetBundles()) if (b != null && !b.isStreamedSceneAssetBundle && b.Contains(key)) n++;
                global::UnityEngine.Debug.LogWarning("PcSpriteAtlas no index for '" + atlas.name + "': loaded bundles with key=" + n);
            }
            return map;
        }
    }
}
#endif
