using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Roguelike.Editor
{
    /// <summary>Keep the shared backdrop and creature art crisp, with uncompressed transparency.</summary>
    public sealed class TowerThemeArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Demos/Roguelike/Resources/Tower/Themes/")||!assetPath.EndsWith(".png"))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Default;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
            importer.mipmapEnabled=false;importer.isReadable=true;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
        }
    }
}
