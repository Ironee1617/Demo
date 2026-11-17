using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R77: the bouncy ending title. The big title (ESCAPED!, WIPED OUT!, ...) pops in one letter after another,
    // each letter dropping in stretched tall, landing in a squash and wobbling to rest (volume kept: it gets wider
    // as it gets shorter), with a soft bubble plip per letter. Once every letter has settled the whole word is
    // drawn in one piece again and only breathes (a slow, small jelly squash about its baseline). The subtitle pill
    // and the results table follow with a lighter version of the same pop.
    // All clocks are local (Time.unscaledTime), started from the server age, so the pop runs smooth at any snapshot rate.
    public sealed partial class AquariumGame
    {
        // Timing (seconds).
        private const float R77Stagger = .055f;      // between letters ("파바박")
        private const float R77BangDelay = .07f;     // a beat before the closing '!'
        private const float R77LetterTime = .62f;    // one letter's drop, squash and wobble
        private const float R77Drop = .12f;          // the drop lands (squash peak) here
        private const float R77BaseLine = 30f;       // the letters' foot below the title centre (84 px font)

        private sealed class R77Word { public string[] glyphs; public float[] centre, start; public float settled; }
        private readonly Dictionary<string, R77Word> r77Words = new Dictionary<string, R77Word>();
        private readonly GUIContent r77Content = new GUIContent();

        // One clock for the title shown now (a new key, or nothing shown for half a second, starts it again).
        private int r77Key = int.MinValue, r77Popped = -1, r77FinalKey = int.MinValue;
        private float r77Start, r77Seen = -10, r77FinalStart;
        private AudioSource r77Plip;
        // Editor reviews pin the clocks to exact ages (negative = live).
        public static float QaTitleAge = -1, QaFinalAge = -1;

        private float R77Clock(int key, float serverAge)
        {
            float now = Time.unscaledTime;
            if (key != r77Key || now - r77Seen > .5f)
            {
                r77Key = key; r77Start = now - Mathf.Clamp(serverAge, 0, 600); r77Popped = -2;
                r77FinalKey = int.MinValue;
            }
            r77Seen = now;
            if (QaTitleAge >= 0) return QaTitleAge;
            return now - r77Start;
        }
        // The results table pops again when the free look ends and it moves to the middle.
        private float R77FinalAge()
        {
            if (r77FinalKey != r77Key) { r77FinalKey = r77Key; r77FinalStart = Time.unscaledTime; }
            if (QaFinalAge >= 0) return QaFinalAge;
            return Time.unscaledTime - r77FinalStart;
        }
        private static int R77KeyOf(float at, int salt) { return Mathf.RoundToInt(at * 100f) * 8 + salt; }

        private R77Word R77Layout(string text)
        {
            R77Word w;
            if (r77Words.TryGetValue(text, out w)) return w;
            int n = text.Length;
            w = new R77Word { glyphs = new string[n], centre = new float[n], start = new float[n] };
            float pad = titleArt.padding.horizontal;
            r77Content.text = text; float total = titleArt.CalcSize(r77Content).x - pad;
            float before = 0;
            for (int i = 0; i < n; i++)
            {
                r77Content.text = text.Substring(0, i + 1);
                float upto = titleArt.CalcSize(r77Content).x - pad;
                w.glyphs[i] = text[i] == ' ' ? null : text[i].ToString();
                w.centre[i] = (before + Mathf.Max(upto, before)) * .5f - total * .5f;
                before = Mathf.Max(upto, before);
                w.start[i] = i * R77Stagger + (i == n - 1 && text[i] == '!' ? R77BangDelay : 0);
            }
            w.settled = (n > 0 ? w.start[n - 1] : 0) + R77LetterTime;
            r77Content.text = null;
            r77Words[text] = w;
            return w;
        }
        // When the title has fully landed (the subtitle starts a little before that).
        private float R77Settled(string text) { TitleStyles(); return R77Layout(text).settled; }

        private static float R77BackOut(float u)
        {
            const float c1 = 1.70158f, c3 = c1 + 1;
            float v = u - 1; return 1 + c3 * v * v * v + c1 * v * v;
        }

        // Chunky display text: a stacked drop shadow, a dark outline, a two-tone fill.
        private void R77TitleArt(float cx, float cy, string text, Color main, float age, float alpha)
        {
            TitleStyles();
            var word = R77Layout(text);
            if (age >= word.settled)
            {
                // Settled: one piece, breathing (wider as it gets shorter) about its baseline.
                float b = .022f * Mathf.Sin((age - word.settled) * 3.4f) * Mathf.Clamp01((age - word.settled) / .4f);
                var keep = GUI.matrix;
                var pivot = new Vector3(cx, cy + R77BaseLine, 0);
                GUI.matrix = keep * Matrix4x4.TRS(pivot, Quaternion.identity, new Vector3(1 + b, 1 - b, 1)) * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
                R77Layers(new Rect(cx - 500, cy - 60, 1000, 120), text, main, alpha);
                GUI.matrix = keep;
                return;
            }
            if (Event.current.type != EventType.Repaint) return;
            // A late join (or a reopened view) skips the plips of letters already down.
            if (r77Popped == -2) { r77Popped = -1; for (int i = 0; i < word.start.Length; i++) if (age > word.start[i] + .1f) r77Popped = i; }
            var matrix = GUI.matrix;
            int count = word.glyphs.Length;
            for (int i = 0; i < count; i++)
            {
                string glyph = word.glyphs[i];
                float t = age - word.start[i];
                if (glyph == null || t < 0) continue;
                if (i > r77Popped) { r77Popped = i; R77Plip(i, count); }
                bool bang = text[i] == '!' && i == count - 1;
                // Grows in with an overshoot while it drops, stretched tall (anticipation of the landing) ...
                float grow = Mathf.LerpUnclamped(.2f, 1, R77BackOut(Mathf.Clamp01(t / .16f)));
                // ... squashes flat as it lands at R77Drop and wobbles out (cos 26 t: -1 at the landing).
                float wobble = (bang ? .5f : .42f) * Mathf.Exp(-7.5f * t) * Mathf.Cos(26f * t);
                float q = 1 + wobble;
                float sx = grow / Mathf.Sqrt(q), sy = grow * Mathf.Sqrt(q);
                float drop = t < R77Drop ? -34f * (1 - t / R77Drop) * (1 - t / R77Drop)
                    : -9f * Mathf.Exp(-9f * (t - R77Drop)) * Mathf.Abs(Mathf.Sin((t - R77Drop) * 14f));
                // Overlapping action: the tilt runs behind the squash, alternating from letter to letter.
                float tilt = ((i & 1) == 0 ? 1 : -1) * (bang ? 14f : 9f) * Mathf.Exp(-6f * t) * Mathf.Sin(20f * t + .4f);
                float a = alpha * Mathf.Clamp01(t / .03f);
                float x = cx + word.centre[i];
                var pivot = new Vector3(x, cy + R77BaseLine + drop, 0);
                GUI.matrix = matrix * Matrix4x4.TRS(pivot, Quaternion.Euler(0, 0, tilt), new Vector3(sx, sy, 1))
                    * Matrix4x4.TRS(new Vector3(-x, -(cy + R77BaseLine), 0), Quaternion.identity, Vector3.one);
                R77Layers(new Rect(x - 80, cy - 60, 160, 120), glyph, main, a);
            }
            GUI.matrix = matrix;
        }

        private void R77Layers(Rect rect, string text, Color main, float alpha)
        {
            var old = GUI.color;
            var deep = new Color(main.r * .22f, main.g * .22f, main.b * .3f, alpha);
            GUI.color = deep;
            for (int i = 9; i >= 1; i--) GUI.Label(new Rect(rect.x + i * .7f, rect.y + i, rect.width, rect.height), text, titleArt);
            GUI.color = new Color(0.02f, 0.06f, 0.1f, alpha);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6;
                GUI.Label(new Rect(rect.x + Mathf.Cos(a) * 4.5f, rect.y + Mathf.Sin(a) * 4.5f, rect.width, rect.height), text, titleArt);
            }
            GUI.color = new Color(main.r, main.g, main.b, alpha);
            GUI.Label(rect, text, titleArt);
            // the lighter upper half of the letters
            GUI.BeginGroup(new Rect(rect.x, rect.y, rect.width, rect.height * .5f));
            GUI.color = new Color(Mathf.Lerp(main.r, 1, .55f), Mathf.Lerp(main.g, 1, .55f), Mathf.Lerp(main.b, 1, .55f), alpha);
            GUI.Label(new Rect(0, 0, rect.width, rect.height), text, titleArt);
            GUI.EndGroup();
            GUI.color = old;
        }

        // A soft bubble plip per letter, climbing in pitch along the word (the R75 UI click, quieter).
        private void R77Plip(int i, int count)
        {
            EnsureUiSounds();
            if (r77Plip == null) { r77Plip = gameObject.AddComponent<AudioSource>(); r77Plip.playOnAwake = false; r77Plip.spatialBlend = 0; r77Plip.volume = .6f; }
            r77Plip.pitch = .85f + .55f * i / Mathf.Max(1, count - 1);
            r77Plip.PlayOneShot(uiClick, .34f);
        }

        // The lighter pop for the subtitle pill and the results table: grows in from `from` with an overshoot,
        // then a few volume-keeping wobbles (wide = the width leads). Returns the matrix to restore.
        private Matrix4x4 R77PushJelly(Vector2 pivot, float t, float from, float wobble, bool wide, float rise = 0)
        {
            var keep = GUI.matrix;
            float u = Mathf.Clamp01(t / .24f);
            float grow = Mathf.LerpUnclamped(from, 1, R77BackOut(u));
            float q = 1 + wobble * Mathf.Exp(-8f * t) * Mathf.Sin(24f * t);
            float sx = wide ? grow * q : grow / Mathf.Sqrt(q), sy = wide ? grow / Mathf.Sqrt(q) : grow * q;
            var p = new Vector3(pivot.x, pivot.y + rise * (1 - R77BackOut(u)), 0);
            GUI.matrix = keep * Matrix4x4.TRS(p, Quaternion.identity, new Vector3(sx, sy, 1)) * Matrix4x4.TRS(new Vector3(-pivot.x, -pivot.y, 0), Quaternion.identity, Vector3.one);
            return keep;
        }
    }
}
