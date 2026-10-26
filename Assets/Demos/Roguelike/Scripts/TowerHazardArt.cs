using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    /// <summary>
    /// Painted trap art at the effects' 128ppu: lit metal spikes on a riveted plate, a stone-framed vent grate, a carved
    /// totem with a gem socket (whole and broken), a rubble heap, a falling boulder and a spring launch pad. Every piece
    /// is shaded per texel (cone and cylinder normals, value-noise grain, grime and contact shadow) and tinted with the
    /// theme's metal, stone and rune colours, then cached per theme.
    /// </summary>
    internal static class TowerHazardArt
    {
        internal const float Ppu = 128;
        internal const float SpikeTile = .5f, SpikeHeight = .625f, GrateWidth = 1, GrateHeight = .375f;
        internal const float TotemWidth = .75f, TotemHeight = 1.625f, TotemGemY = 1.12f, RubbleWidth = 1.375f, RubbleHeight = .5f;
        internal const float BoulderSize = 1, PadWidth = 1.125f, PadHeight = .375f, ChevronWidth = .5f, ChevronHeight = .3125f;
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        // Texels painted ahead on worker threads; only the texture upload waits for the main thread.
        private static readonly Dictionary<string, Task<Color32[]>> pending = new Dictionary<string, Task<Color32[]>>();
        private static bool collecting;

        /// <summary>
        /// Starts painting a theme's whole trap set in the background (called while the floor before it is played),
        /// so a floor that changes theme only uploads finished pixels instead of shading them on the spot.
        /// </summary>
        internal static void Prefetch(TowerThemeId theme)
        {
            collecting = true;
            try { Spikes(theme); Grate(theme); Totem(theme, false); Totem(theme, true); Rubble(theme); Boulder(theme); Pad(theme); Chevron(); }
            finally { collecting = false; }
        }

        internal struct Palette { public Color metal, metalDark, stone, stoneDark, rune; }
        internal static Palette For(TowerThemeId theme)
        {
            switch (theme)
            {
                case TowerThemeId.Olympus: return P("c39a5c", "5a3c22", "d9d0bf", "8a8172", "f4dfa1");
                case TowerThemeId.Asgard: return P("a4b4c4", "3a4656", "5e6c7e", "2c3644", "9ee9ef");
                case TowerThemeId.Dangun: return P("837d70", "34302a", "6c6c64", "34342e", "e9c27a");
                case TowerThemeId.Eden: return P("b9aa7a", "4a4430", "7e8a64", "3e4630", "f2e39a");
                case TowerThemeId.Veda: return P("c8963c", "5a3a14", "8c6c52", "46342a", "ffb347");
                case TowerThemeId.Papyrus: return P("d2aa52", "5e4418", "bca072", "6a5434", "6ac8e8");
                case TowerThemeId.Hell: return P("5a4640", "1c1412", "3e3230", "1a1412", "ff7a2a");
                case TowerThemeId.Abyss: return P("d6c6ae", "4a3a30", "4c2c36", "22121a", "e0305a");
                case TowerThemeId.Village: return P("9c8c72", "3e342a", "6c5c4c", "342a22", "ffd27a");
                default: return P("8c8c94", "2c2c34", "7c4c42", "3a201c", "ff9a4a");
            }
        }
        private static Palette P(string metal, string metalDark, string stone, string stoneDark, string rune)
        { return new Palette { metal = Hex(metal), metalDark = Hex(metalDark), stone = Hex(stone), stoneDark = Hex(stoneDark), rune = Hex(rune) }; }
        private static Color Hex(string value) { Color c; ColorUtility.TryParseHtmlString("#" + value, out c); return c; }

        // ---------------------------------------------------------------- sprites

        /// <summary>Two cone spikes on a riveted base plate; tiles repeat across a strip. Pivot at the plate's bottom centre.</summary>
        internal static Sprite Spikes(TowerThemeId theme)
        {
            var pal = For(theme);
            return Paint("spikes " + theme, SpikeTile, SpikeHeight, new Vector2(.5f, 0), (x, y) =>
            {
                Color c = Color.clear;
                // Base plate with a bevelled top edge and one rivet.
                if (y < .075f)
                {
                    float grain = Fbm(x * 18, y * 18, 3, 11);
                    c = Color.Lerp(pal.metalDark, pal.metal, .25f + grain * .2f);
                    if (y > .06f) c = Color.Lerp(c, pal.metal, .55f);
                    else if (y < .012f) c *= .6f;
                    float rx = Mathf.Repeat(x, .25f) - .125f, ry = y - .037f;
                    if (rx * rx + ry * ry < .00045f) c = Color.Lerp(pal.metal, Color.white, ry > 0 ? .35f : -.2f);
                    c.a = 1;
                }
                foreach (float cx in new[] { .125f, .375f })
                {
                    float base0 = .05f, tip = .58f, t = (y - base0) / (tip - base0);
                    if (t < 0 || t > 1) continue;
                    float half = .1f * (1 - t) + .004f, d = x - cx;
                    float coverage = Mathf.Clamp01((half - Mathf.Abs(d)) * Ppu + .5f);
                    if (coverage <= 0) continue;
                    float n = Mathf.Clamp(d / half, -1, 1), nz = Mathf.Sqrt(Mathf.Max(0, 1 - n * n));
                    float diffuse = Mathf.Clamp01(-.55f * n + .7f * nz + .15f);
                    float spec = Mathf.Exp(-Sq((n + .38f) / .14f)) * (.55f + .45f * t);
                    Color metal = pal.metal * (.22f + .95f * diffuse) + Color.white * spec * .55f;
                    // Grime and rust gather toward the base; the tips are honed bright.
                    float grime = (1 - Smooth(0, .45f, t)) * (.45f + Fbm(x * 30, y * 30, 3, 5) * .5f);
                    metal = Color.Lerp(metal, pal.metalDark * .8f, grime * .7f);
                    float rust = Smooth(.58f, .72f, Fbm(x * 22 + cx * 9, y * 22, 3, 17)) * (1 - t);
                    metal = Color.Lerp(metal, new Color(.42f, .2f, .1f), rust * .45f);
                    if (t > .82f) metal = Color.Lerp(metal, Color.white, (t - .82f) * 1.6f * nz);
                    metal.a = 1;
                    c = c.a <= 0 ? new Color(metal.r, metal.g, metal.b, coverage) : Color.Lerp(c, metal, coverage);
                }
                return c;
            });
        }

        /// <summary>A stone block holding an iron grate. Pivot at the block's bottom centre; the top rides just above the ground.</summary>
        internal static Sprite Grate(TowerThemeId theme)
        {
            var pal = For(theme);
            return Paint("grate " + theme, GrateWidth, GrateHeight, new Vector2(.5f, 0), (x, y) =>
            {
                float edge = Mathf.Min(Mathf.Min(x, GrateWidth - x), Mathf.Min(y, GrateHeight - y));
                float grain = Fbm(x * 14, y * 14, 4, 23);
                Color c = Color.Lerp(pal.stoneDark, pal.stone, .45f + grain * .45f);
                if (edge < .03f) c *= .55f + edge * 10;
                if (y > GrateHeight - .035f) c = Color.Lerp(c, Color.white, .18f);
                bool inset = x > .1f && x < .9f && y > .12f && y < .33f;
                if (inset)
                {
                    float bar = Mathf.Repeat(x - .1f, .08f);
                    bool iron = bar < .028f || y < .14f || y > .31f;
                    if (iron)
                    {
                        float n = bar < .028f ? (bar - .014f) / .014f : 0;
                        c = pal.metalDark * (.7f + .6f * Mathf.Clamp01(-.6f * n + .5f)) + Color.white * Mathf.Exp(-Sq((n + .4f) / .2f)) * .18f;
                    }
                    // The vent's throat: soot black with a faint ember warmth at its depth.
                    else c = Color.Lerp(new Color(.05f, .03f, .025f), new Color(.32f, .1f, .03f), Smooth(.3f, .14f, y) * .7f);
                }
                c.a = 1; return c;
            });
        }

        /// <summary>A carved standing stone with a gem socket; broken, only its jagged stump remains. Pivot at the base.</summary>
        internal static Sprite Totem(TowerThemeId theme, bool broken)
        {
            var pal = For(theme);
            return Paint((broken ? "broken totem " : "totem ") + theme, TotemWidth, TotemHeight, new Vector2(.5f, 0), (x, y) =>
            {
                float cx = x - TotemWidth * .5f, half;
                if (y < .2f) half = .36f;
                else if (y < 1.36f) half = Mathf.Lerp(.28f, .23f, (y - .2f) / 1.16f);
                else half = .31f - Mathf.Max(0, y - 1.55f) * 1.6f;
                if (broken && y > .5f + Fbm(x * 9, 3, 2, 31) * .22f) return Color.clear;
                float coverage = Mathf.Clamp01((half - Mathf.Abs(cx)) * Ppu + .5f) * Mathf.Clamp01((TotemHeight - .02f - y) * Ppu);
                if (coverage <= 0) return Color.clear;
                float n = Mathf.Clamp(cx / half, -1, 1), nz = Mathf.Sqrt(Mathf.Max(0, 1 - n * n));
                float light = Mathf.Clamp01(-.5f * n + .72f * nz + .18f);
                float grain = Fbm(x * 12, y * 12, 4, 37), crack = Mathf.Abs(Fbm(x * 6 + 4, y * 3, 3, 41) - .5f);
                Color c = Color.Lerp(pal.stoneDark, Color.Lerp(pal.stone, new Color(.72f, .7f, .66f), .3f), grain * .5f + .4f) * (.45f + .8f * light);
                if (crack < .018f) c *= .55f;
                // Carved bands, the plinth's top edge and the capital.
                float band = Mathf.Repeat(y - .2f, .26f);
                if (y > .2f && y < 1.36f && band < .022f) c *= .62f;
                else if (y > .2f && y < 1.36f && band < .036f) c = Color.Lerp(c, Color.white, .12f);
                if (Mathf.Abs(y - .2f) < .018f || Mathf.Abs(y - 1.36f) < .018f) c = Color.Lerp(c, Color.white, .2f);
                // A column of carved runes down the shaft glows with the gem's colour.
                if (!broken && y > .3f && y < .98f && Mathf.Abs(cx) < .075f)
                {
                    int row = Mathf.FloorToInt((y - .3f) / .12f); float ry = Mathf.Repeat(y - .3f, .12f), rx = cx + .0375f;
                    float h = Hash(row, 3, 59); bool stroke = Mathf.Abs(cx) < .012f && ry > .015f && ry < .105f;
                    stroke |= ry > .015f && ry < .105f && Mathf.Abs((ry - .015f) * (h < .5f ? 1 : -1) * .8f - (rx - .0375f)) < .011f && Mathf.Abs(cx) < .07f;
                    stroke |= h > .7f && Mathf.Abs(ry - .06f) < .011f;
                    if (stroke) c = Color.Lerp(pal.rune, Color.white, .25f);
                }
                // The gem socket: a dark carved ring the glowing gem sits in.
                float gx = cx, gy = y - TotemGemY, r = Mathf.Sqrt(gx * gx + gy * gy);
                if (!broken && r < .105f) c = r < .075f ? new Color(.06f, .04f, .04f) : Color.Lerp(c * .5f, Color.white * .5f, Mathf.Clamp01(-gy * 8));
                if (broken && y > .42f) c = Color.Lerp(c, pal.stoneDark * .7f, .35f);
                c.a = coverage; return c;
            });
        }

        /// <summary>A heap of loose boulders marking a rockfall zone. Pivot at its bottom centre.</summary>
        internal static Sprite Rubble(TowerThemeId theme)
        {
            var pal = For(theme);
            float[][] stones = { new[] { .3f, .13f, .14f }, new[] { .62f, .17f, .19f }, new[] { .95f, .12f, .13f }, new[] { .47f, .3f, .12f }, new[] { .8f, .32f, .1f }, new[] { 1.13f, .08f, .08f } };
            return Paint("rubble " + theme, RubbleWidth, RubbleHeight, new Vector2(.5f, 0), (x, y) => Stones(pal, stones, x, y, 43));
        }

        /// <summary>One falling boulder. Pivot at its centre.</summary>
        internal static Sprite Boulder(TowerThemeId theme)
        {
            var pal = For(theme);
            float[][] stones = { new[] { BoulderSize * .5f, BoulderSize * .5f, BoulderSize * .43f } };
            return Paint("boulder " + theme, BoulderSize, BoulderSize, new Vector2(.5f, .5f), (x, y) => Stones(pal, stones, x, y, 47));
        }

        /// <summary>A sprung plate in a metal housing with a rune inlay. Pivot at the housing's bottom centre.</summary>
        internal static Sprite Pad(TowerThemeId theme)
        {
            var pal = For(theme);
            return Paint("pad " + theme, PadWidth, PadHeight, new Vector2(.5f, 0), (x, y) =>
            {
                float cx = x - PadWidth * .5f, grain = Fbm(x * 20, y * 20, 3, 53);
                Color c = Color.clear;
                if (y < .15f && Mathf.Abs(cx) < .52f)
                {
                    c = Color.Lerp(pal.metalDark, pal.metal, .3f + grain * .15f);
                    if (y > .13f) c = Color.Lerp(c, Color.white, .25f);
                    if (Mathf.Abs(cx) > .49f) c *= .6f;
                    c.a = 1;
                }
                else if (y < .27f && Mathf.Abs(cx) < .4f)
                {
                    // Coils: bright wire crests on dark gaps.
                    float wave = Mathf.Abs(Mathf.Repeat(y * 22 + cx * 1.5f, 2) - 1);
                    c = Color.Lerp(pal.metalDark * .5f, Color.Lerp(pal.metal, Color.white, .3f), Smooth(.35f, .85f, wave));
                    c.a = 1;
                }
                else if (y < .35f && Mathf.Abs(cx) < .5f)
                {
                    c = Color.Lerp(pal.metal, Color.white, .1f + grain * .15f) * (y > .33f ? 1.15f : .95f);
                    if (Mathf.Abs(cx) < .36f && Mathf.Abs(y - .305f) < .016f) c = Color.Lerp(pal.rune, Color.white, .3f);
                    if (Mathf.Abs(cx) > .47f) c *= .65f;
                    c.a = 1;
                }
                return c;
            });
        }

        /// <summary>A soft white upward chevron, tinted and floated above a launch pad. Pivot at its centre.</summary>
        internal static Sprite Chevron()
        {
            return Paint("pad chevron", ChevronWidth, ChevronHeight, new Vector2(.5f, .5f), (x, y) =>
            {
                float cx = Mathf.Abs(x - ChevronWidth * .5f), line = .2f - cx * .62f;
                float d = Mathf.Abs(y - .04f - line) * .85f;
                if (cx > .2f) d = Mathf.Max(d, cx - .2f);
                float a = Mathf.Clamp01(1 - d / .028f) * .95f + Mathf.Exp(-Sq(d / .05f)) * .4f;
                return new Color(1, 1, 1, Mathf.Clamp01(a));
            });
        }

        // ---------------------------------------------------------------- painting helpers

        private static Color Stones(Palette pal, float[][] stones, float x, float y, int seed)
        {
            Color c = Color.clear;
            // Loose rock reads as weathered grey-tan in every theme, tinted toward the theme's stone, with a cool rim light.
            Color lit = Color.Lerp(pal.stone, new Color(.7f, .66f, .6f), .55f), shade = Color.Lerp(pal.stoneDark, new Color(.3f, .28f, .26f), .45f);
            for (int i = 0; i < stones.Length; i++)
            {
                float sx = stones[i][0], sy = stones[i][1], r = stones[i][2];
                float dx = (x - sx) / r, dy = (y - sy) / (r * .86f);
                float wobble = (Fbm(Mathf.Atan2(dy, dx) * 1.3f + i * 7, i * 3.1f, 2, seed) - .5f) * .28f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) - wobble;
                float coverage = Mathf.Clamp01((1 - d) * r * Ppu + .5f);
                if (coverage <= 0) continue;
                float nx = Mathf.Clamp(dx, -1, 1), ny = Mathf.Clamp(dy, -1, 1), nz = Mathf.Sqrt(Mathf.Max(0, 1 - nx * nx - ny * ny));
                float light = Mathf.Clamp01(-.45f * nx + .55f * ny + .7f * nz);
                float grain = Fbm(x * 16 + i, y * 16, 4, seed + i);
                Color s = Color.Lerp(shade, lit, .3f + grain * .5f) * (.42f + .78f * light);
                s += new Color(.55f, .62f, .7f) * Smooth(.78f, .98f, d) * Mathf.Clamp01(-nx * .6f + ny) * .45f;
                if (Mathf.Abs(Fbm(x * 7, y * 7, 3, seed + 9) - .5f) < .02f) s *= .6f;
                s.a = 1;
                c = c.a <= 0 ? new Color(s.r, s.g, s.b, coverage) : Color.Lerp(c, s, coverage);
            }
            return c;
        }

        private static Sprite Paint(string key, float width, float height, Vector2 pivot, Func<float, float, Color> texel)
        {
            Sprite sprite;
            if (cache.TryGetValue(key, out sprite) && sprite != null) return sprite;
            int w = Mathf.RoundToInt(width * Ppu), h = Mathf.RoundToInt(height * Ppu);
            if (collecting)
            {
                if (!pending.ContainsKey(key)) pending[key] = Task.Run(() => Pixels(w, h, texel));
                return null;
            }
            Color32[] pixels = null; Task<Color32[]> ahead;
            if (pending.TryGetValue(key, out ahead))
            {
                pending.Remove(key);
                try { pixels = ahead.Result; } catch (AggregateException) { }
            }
            if (pixels == null) pixels = Pixels(w, h, texel);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = key };
            texture.SetPixels32(pixels); texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, w, h), pivot, Ppu, 0, SpriteMeshType.FullRect);
            sprite.name = key; cache[key] = sprite;
            return sprite;
        }

        /// <summary>The texels of one piece. Pure arithmetic on the palette, so it may run off the main thread.</summary>
        private static Color32[] Pixels(int w, int h, Func<float, float, Color> texel)
        {
            var pixels = new Color32[w * h];
            for (int py = 0; py < h; py++)
                for (int px = 0; px < w; px++)
                {
                    Color c = texel((px + .5f) / Ppu, (py + .5f) / Ppu);
                    c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b); c.a = Mathf.Clamp01(c.a);
                    pixels[py * w + px] = c;
                }
            return pixels;
        }

        private static float Sq(float v) { return v * v; }
        private static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
        private static float Hash(int x, int y, int seed)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
            h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
        private static float Noise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y); float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed), c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
        private static float Fbm(float x, float y, int octaves, int seed)
        {
            float sum = 0, amplitude = .5f, total = 0;
            for (int i = 0; i < octaves; i++) { sum += Noise(x, y, seed + i * 101) * amplitude; total += amplitude; x *= 2.03f; y *= 2.03f; amplitude *= .5f; }
            return sum / total;
        }
    }
}
