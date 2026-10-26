using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Roguelike.Editor
{
    /// <summary>
    /// Story campaign paintings (Tools/TowerArt/StoryPainter): the final boss is drawn well below its source size and the
    /// village props, breakable caches and loot near it, so they keep bilinear sampling and full colour; the runtime
    /// builds sprites (and the caches' shards) straight from them.
    /// </summary>
    public sealed class TowerStoryArtImporter : AssetPostprocessor
    {
        // Version 2 sets the 2D shape explicitly: a bare meta file otherwise imports these PNGs as cubemaps.
        public override uint GetVersion(){return 2;}
        void OnPreprocessTexture()
        {
            bool story=assetPath.StartsWith("Assets/Demos/Roguelike/Resources/Tower/Story/")||assetPath.StartsWith("Assets/Demos/Roguelike/Resources/Tower/Breakables/");
            if(!story||!assetPath.EndsWith(".png"))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;
            importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
            importer.mipmapEnabled=false;importer.isReadable=false;importer.alphaIsTransparency=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=1024;
        }
    }
}
