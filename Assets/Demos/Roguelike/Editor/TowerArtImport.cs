#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Roguelike.Editor
{
    // Original pixel PNG bytes remain unchanged. ArtData supplies their source weighted meshes and clips.
    // Independent runtime copies also build without the original RPGAsset folder.
    public sealed class TowerArtImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Tower/Actors/") && !assetPath.Contains("/Resources/Tower/Monsters/") &&
                !assetPath.Contains("/Resources/Tower/Portraits/")) return;
            var importer = (TextureImporter)assetImporter;
            bool portrait = assetPath.Contains("/Portraits/");
            importer.textureType = portrait ? TextureImporterType.Default : TextureImporterType.Sprite;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = portrait ? FilterMode.Bilinear : FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = portrait ? 512 : 256;
            if (!portrait)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 1;
                // Alpha-centered custom pivots are stored with the copied image meta files.
            }
        }

        [MenuItem("Prototype Lab/Roguelike/Reimport character art")]
        public static void Reimport()
        {
            const string folder = "Assets/Demos/Roguelike/Resources/Tower";
            if (!Directory.Exists(folder)) return;
            foreach (string file in Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories))
                AssetDatabase.ImportAsset(file.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
        }
    }
}
#endif
