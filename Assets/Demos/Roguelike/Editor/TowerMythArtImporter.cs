using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Roguelike.Editor
{
    // Atlas cells are sliced at runtime, so importing never rewrites the generated source images.
    public sealed class TowerMythArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Demos/Roguelike/Resources/Tower/Myths/")||!assetPath.EndsWith(".png"))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Default;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
            // Eden measures transparent frame margins once to keep every sole on
            // the actor root. The remaining mythology atlases use their mesh rig.
            importer.isReadable=assetPath.Contains("/EdenCute/");
        }
    }
}
