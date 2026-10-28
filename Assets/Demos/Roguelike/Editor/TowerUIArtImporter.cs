using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Roguelike.Editor
{
    /// <summary>
    /// The UI kit baked by Tools/TowerArt/UIPainter is drawn at half its pixel size and smaller on low screens, so it
    /// keeps full colour with trilinear mipmaps; the menu backdrop is large and opaque and is block-compressed.
    /// </summary>
    public sealed class TowerUIArtImporter : AssetPostprocessor
    {
        // Version 2: the menu backdrop drops its mipmaps so it can be block-compressed (non-power-of-two size).
        public override uint GetVersion(){return 2;}
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Demos/Roguelike/Resources/Tower/UI/")||!assetPath.EndsWith(".png"))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;
            importer.sRGBTexture=true;importer.alphaIsTransparency=true;importer.isReadable=false;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=0;
            importer.mipmapEnabled=true;importer.mipmapFilter=TextureImporterMipFilter.BoxFilter;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
            if(assetPath.EndsWith("/menu-backdrop.png")){importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;}
            else importer.textureCompression=TextureImporterCompression.Uncompressed;
        }
    }
}
