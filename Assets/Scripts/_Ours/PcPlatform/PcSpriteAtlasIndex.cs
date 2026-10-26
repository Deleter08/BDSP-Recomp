#if !SWITCH
// PC platform layer: the sprite list of one of the game's sprite atlases, written next to it into its AssetBundle by the Unity 6
// bundle build (BdspWinBuild.AtlasIndex.cs) and read by PcSpriteAtlas. NOT shipped code. Own file: Unity needs a ScriptableObject
// class in a file of the same name.
namespace PcPlatform
{
    public sealed class PcSpriteAtlasIndex : global::UnityEngine.ScriptableObject
    {
        public global::UnityEngine.U2D.SpriteAtlas atlas;
        public global::UnityEngine.Sprite[] sprites;
    }
}
#endif
