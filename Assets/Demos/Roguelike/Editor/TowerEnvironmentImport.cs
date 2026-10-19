#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace PrototypeLab.Roguelike.Editor
{
    public sealed class TowerEnvironmentImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if(!assetPath.Contains("/Resources/Tower/Environment/"))return;
            var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Default;t.mipmapEnabled=false;t.filterMode=FilterMode.Point;
            t.textureCompression=TextureImporterCompression.Uncompressed;t.npotScale=TextureImporterNPOTScale.None;t.alphaIsTransparency=true;t.maxTextureSize=2048;t.wrapMode=TextureWrapMode.Clamp;
        }
    }
}
#endif
