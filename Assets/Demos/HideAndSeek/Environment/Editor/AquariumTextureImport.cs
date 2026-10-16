using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    public sealed class AquariumTextureImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if(!assetPath.Contains("HideAndSeek/Environment/Resources/Aquarium/Textures/"))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Default; // The triplanar shader consumes linear RGB normals.
            importer.sRGBTexture=assetPath.Contains("Albedo")||assetPath.Contains("Atlas");
            importer.maxTextureSize=4096;importer.mipmapEnabled=true;importer.streamingMipmaps=false;
            importer.anisoLevel=16;importer.filterMode=FilterMode.Trilinear;importer.wrapMode=assetPath.Contains("Atlas")?TextureWrapMode.Clamp:TextureWrapMode.Repeat;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.compressionQuality=100;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Standalone",overridden=true,maxTextureSize=4096,format=TextureImporterFormat.BC7,compressionQuality=100});
        }
    }
}
