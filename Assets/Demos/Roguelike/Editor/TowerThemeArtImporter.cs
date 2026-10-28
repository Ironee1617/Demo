using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Roguelike.Editor
{
    /// <summary>Keep the shared backdrop and creature art crisp, with uncompressed transparency.</summary>
    public sealed class TowerThemeArtImporter : AssetPostprocessor
    {
        // Version 3: backdrops are baked at twice the screen density of a 1080p camera (see the strip settings below).
        public override uint GetVersion(){return 3;}
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Demos/Roguelike/Resources/Tower/Themes/")||!assetPath.EndsWith(".png"))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Default;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
            importer.mipmapEnabled=false;importer.isReadable=true;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
            // Parallax strips are large and only drawn: block-compress them (dimensions are multiples
            // of four) and keep no CPU copy. They stay without mipmaps: Unity leaves mipmapped
            // non-power-of-two textures uncompressed (four times the memory). Baked at twice a 1080p
            // camera's density, the mid and near strips shrink by exactly half there, where bilinear
            // sampling averages each 2×2 block; the runtime picks point or bilinear per screen scale.
            if(assetPath.Contains("/Backdrops/"))
            {
                importer.isReadable=false;importer.textureCompression=TextureImporterCompression.CompressedHQ;
                importer.filterMode=FilterMode.Bilinear;importer.maxTextureSize=8192;
                importer.ClearPlatformTextureSettings("Standalone");
            }
            // Lit terrain tiles are sampled through repeating UVs on sloped meshes: filtered, mipmapped and tiling.
            else if(assetPath.Contains("/Terrain/"))
            {
                importer.isReadable=false;importer.textureCompression=TextureImporterCompression.CompressedHQ;
                importer.filterMode=FilterMode.Bilinear;importer.mipmapEnabled=true;importer.mipmapFilter=TextureImporterMipFilter.KaiserFilter;
                importer.wrapMode=TextureWrapMode.Repeat;importer.maxTextureSize=4096;
            }
        }
    }
}
