using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Roguelike.Editor
{
    // Pixel god sheets are sliced at runtime, so importing never rewrites the baked images.
    public sealed class TowerMythArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Demos/Roguelike/Resources/Tower/Myths/")||!assetPath.EndsWith(".png"))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Default;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
            importer.isReadable=false;
        }
    }
}
