using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R66: allocation-free HUD text. OnGUI runs every frame (twice before R66: layout and repaint), and every
    // number turned into a string, every "a" + b and every new GUIContent was fresh garbage, so the garbage
    // collector had to step in every few seconds mid-round. These caches hand back the same string object
    // for the same value, so after the first few seconds of a round the HUD allocates nothing.
    public sealed partial class AquariumGame
    {
        private readonly GUIContent measureContent = new GUIContent();
        private static readonly Dictionary<int, string> intText = new Dictionary<int, string>();
        private static readonly Dictionary<int, string> tenthText = new Dictionary<int, string>();
        private static readonly Dictionary<int, string> clockText = new Dictionary<int, string>();
        private static readonly Dictionary<int, string> minSecText = new Dictionary<int, string>();
        private static readonly Dictionary<(string, string), string> joined = new Dictionary<(string, string), string>();

        // style.CalcSize(new GUIContent(text)) without the new GUIContent.
        private Vector2 Measure(GUIStyle style, string text) { measureContent.text = text; return style.CalcSize(measureContent); }

        // value.ToString()
        private static string Int(int value)
        {
            if (intText.TryGetValue(value, out var s)) return s;
            s = value.ToString(); if (intText.Count < 20000) intText[value] = s; return s;
        }
        // value.ToString("0.0")
        private static string Tenths(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || Mathf.Abs(value) > 100000) return value.ToString("0.0");
            int key = (int)System.Math.Round((double)value * 10, System.MidpointRounding.AwayFromZero);
            if (tenthText.TryGetValue(key, out var s)) return s;
            s = value.ToString("0.0"); if (tenthText.Count < 20000) tenthText[key] = s; return s;
        }
        // string.Format("{0:00}:{1:00}", seconds / 60, seconds % 60)
        private static string Clock(int seconds)
        {
            if (clockText.TryGetValue(seconds, out var s)) return s;
            s = string.Format("{0:00}:{1:00}", seconds / 60, seconds % 60); if (clockText.Count < 20000) clockText[seconds] = s; return s;
        }
        // (s / 60) + ":" + (s % 60).ToString("00")
        private static string MinSecText(int s)
        {
            if (minSecText.TryGetValue(s, out var text)) return text;
            text = (s / 60) + ":" + (s % 60).ToString("00"); if (minSecText.Count < 20000) minSecText[s] = text; return text;
        }
        // a + b, the same string object every time for the same pair.
        private static string Cat(string a, string b)
        {
            a = a ?? ""; b = b ?? "";
            if (joined.TryGetValue((a, b), out var s)) return s;
            s = a + b; if (joined.Count < 50000) joined[(a, b)] = s; return s;
        }
        private static string Cat(string a, string b, string c) { return Cat(Cat(a, b), c); }
        private static string Cat(string a, string b, string c, string d) { return Cat(Cat(Cat(a, b), c), d); }

        // R66: compile the shader variants a round uses (recorded in play: menu, lobby, both tanks, diver and alien
        // views; Resources/Aquarium/R66ShaderWarmup) while the game starts, instead of the first time each one
        // comes into view mid-round (the hitch when the diver first sees the tank).
        private static bool shadersWarmed;
        private static void WarmShaders()
        {
            if (shadersWarmed) return;
            shadersWarmed = true;
            var variants = Resources.Load<ShaderVariantCollection>("Aquarium/R66ShaderWarmup");
            if (variants != null && !variants.isWarmedUp) variants.WarmUp();
        }

        // R66: the pause menu's picture option. "빠르게" draws with 2x instead of 4x anti-aliasing and two shadow
        // cascades instead of four (measured at 4K on an RTX 4070 Ti: 1.5 to 3 ms less GPU time per frame, about
        // +25% fps in the heaviest views). Off by default, so the picture only changes if the player picks it; kept.
        private bool fastGraphics;
        private int fullCascades = 4;
        private void LoadGraphicsChoice() { fullCascades = QualitySettings.shadowCascades; fastGraphics = PlayerPrefs.GetInt("Aquarium.FastGraphics", 0) == 1; }
        private void SetFastGraphics(bool fast)
        {
            fastGraphics = fast; PlayerPrefs.SetInt("Aquarium.FastGraphics", fast ? 1 : 0); PlayerPrefs.Save();
            if (!fast) { QualitySettings.antiAliasing = 4; QualitySettings.shadowCascades = fullCascades; }   // the tank's own settings
            ApplyGraphicsChoice();
        }
        // Every frame (two property reads): building a tank sets 4x anti-aliasing again.
        private void ApplyGraphicsChoice()
        {
            AquariumWaterLife.Fast = fastGraphics;   // R85: fewer specks, cheaper sway
            if (!fastGraphics) return;
            if (QualitySettings.antiAliasing > 2) QualitySettings.antiAliasing = 2;
            if (QualitySettings.shadowCascades > 2) QualitySettings.shadowCascades = 2;
        }

        // Array.Find(players, p => p.id == id) without the closure.
        private static Swimmer PlayerById(Swimmer[] players, int id)
        {
            if (players == null) return null;
            for (int i = 0; i < players.Length; i++) if (players[i].id == id) return players[i];
            return null;
        }
        private static Swimmer FirstAliveSeeker(Swimmer[] players)
        {
            if (players == null) return null;
            for (int i = 0; i < players.Length; i++) if (players[i].alive && players[i].team == Team.Seeker) return players[i];
            return null;
        }
        private static Swimmer FirstAlive(Swimmer[] players)
        {
            if (players == null) return null;
            for (int i = 0; i < players.Length; i++) if (players[i].alive) return players[i];
            return null;
        }
    }
}
