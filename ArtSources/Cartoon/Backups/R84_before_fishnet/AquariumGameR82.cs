using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R82: no first-use stalls at the dive and the first catch (sounds and HUD characters).
    // The sound clips are imported without "preload audio data", so each one was read and decoded on the main thread
    // on the frame it was first played (4 to 14 ms each in the editor): the splash, the swim, breath and bubble loops
    // and the hunt music all start within the dive's 2.6 seconds, the shot, the hit and the reel on the first catch.
    // They are all loaded while the game starts instead (about 0.15 s, once).
    public sealed partial class AquariumGame
    {
        private void PreloadSounds()
        {
            foreach (var clip in sounds.Values) Preload(clip);
            foreach (var clip in real.Values) Preload(clip);
            Preload(pingSound); Preload(growlSound);
            if (screwSource != null) Preload(screwSource.clip);
        }

        // The HUD font is a dynamic one: a character is rasterised, and the font's kerning table rebuilt, on the frame
        // it is first drawn (13 to 30 ms per batch of new characters, measured at the hunt start and on the first catch).
        // Every character the game's own text uses (AquariumGlyphsR82) is laid out once, in both weights, on the first
        // menu frame, so no HUD line that appears later adds any.
        private bool glyphsWarmed;
        private void WarmGlyphs()
        {
            if (glyphsWarmed || font == null || Event.current == null || Event.current.type != EventType.Repaint) return;
            glyphsWarmed = true;
            var content = new GUIContent(AquariumGlyphsR82.Text);
            var style = new GUIStyle { font = font, fontSize = 16, wordWrap = false, richText = false };
            style.CalcSize(content);
            style.fontStyle = FontStyle.Bold; style.CalcSize(content);
        }

        private static void Preload(AudioClip clip)
        {
            if (clip != null && clip.loadType != AudioClipLoadType.Streaming && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        }
    }
}
