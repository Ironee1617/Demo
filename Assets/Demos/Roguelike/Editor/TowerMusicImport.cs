using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Roguelike.Editor
{
    public sealed class TowerMusicImport : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Demos/Roguelike/Resources/Tower/Music/") || !assetPath.EndsWith(".ogg")) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false; importer.loadInBackground = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .85f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
        }
    }
}
