using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Storm and arcane family: ChainLightning and ArcaneVolley, drawn as painted light.
    // Lightning comes from pre-baked forked bolts. Each bolt is baked twice from one set of paths: the bolt itself as a light
    // level (a white-hot core easing out through pale cyan and electric blue to a thin saturated blue edge, with brush grain
    // along the stroke) and its bloom, a soft glow hugging every branch. A painted bolt lays the light over its bloom with the
    // same variant, so it glows along its whole jagged length; one stretched clip shows a whole branching bolt and cycles
    // through the baked variants so it crackles. The hand charge is a crackling plasma orb, thunderheads are soft slate
    // clouds lit from the upper left with lightning flickering inside, and strikes land in hit sparks, shock fronts and
    // small electric blasts. Arcane missiles are violet plasma orbs with smooth comet trails, bursting in arcane blasts,
    // hit sparks, painted prisms and seals of crisp glowing lines.
    public sealed partial class TowerVfx
    {
        private sealed class StormPath { public Vector2[] points; public int[] column; public float core; public Vector2 min, max; }
        // A painted shape's two bakes at one texel: x is the light level, y the bloom around it.
        private delegate Vector2 StormField(float u, float v);

        private const int StormBoltWidth = 384, StormBoltHeight = 144, StormBoltVariants = 5;
        // StormBoltLights are the painted bolts and StormBolts their soft blooms. StormArcLights are crackling arc rings,
        // StormArcGlows their blooms and StormArcRings a deep body a little inside the light (Grade lays it under its gold arcs).
        private static Sprite[] StormBolts, StormBoltLights, StormArcRings, StormArcLights, StormArcGlows;
        private static Sprite[][] StormBoltCycles, StormBoltLightCycles;
        // The shared billows held whole for most of a thunderhead's life, tearing apart only at its end.
        private static Sprite[][] StormCloudFrames;
        private static Sprite StormTwin, StormShard, StormSeal, StormSealGlow, StormSealBody;
        private static int StormFlashFrame = -1, StormBurstFrame = -100, StormBurstStack, StormMuzzleFrame = -1, StormVolleyCount, StormGradeFrame = -100;
        private static Vector2 StormBurstAt;
        private static readonly Color StormElectric = new Color(.38f, .6f, 1), StormHot = new Color(.94f, .97f, 1), StormViolet = new Color(.76f, .5f, 1),
            StormPink = new Color(1, .62f, .96f),
            // The bolt's tint (white core, pale cyan, electric blue, a saturated blue edge) and the plasma of the hand charge.
            StormBlue = new Color(.22f, .66f, 1), StormPlasma = new Color(.3f, .62f, 1), StormDeepBlue = new Color(.16f, .38f, 1),
            // A thunderhead: slate underside and paler lit tops (both soft painted billows), and the pale ionised smoke of a strike.
            StormCloudDark = new Color(.42f, .45f, .66f, .8f), StormCloudLit = new Color(.82f, .85f, .97f, .74f), StormIon = new Color(.6f, .66f, .86f, .5f),
            // The missile's tint (violet ramp round a white core), its plasma and the lavender smoke of its burst.
            StormArcane = new Color(.68f, .4f, 1), StormArcanePlasma = new Color(.74f, .38f, 1), StormArcaneDeep = new Color(.56f, .2f, 1),
            StormArcaneSmoke = new Color(.62f, .48f, .82f, .5f);

        // ---------------------------------------------------------------- textures

        private static void BuildStormTextures()
        {
            StormBolts = new Sprite[StormBoltVariants]; StormBoltLights = new Sprite[StormBoltVariants];
            for (int f = 0; f < StormBoltVariants; f++)
            {
                StormPath[] paths = StormForkedPaths(new System.Random(4100 + f * 37));
                int variant = f; Sprite unused;
                StormBake("Storm bolt " + f, StormBoltWidth, StormBoltHeight, (u, v) => StormBoltField(paths, variant, u, v), false, out StormBoltLights[f], out StormBolts[f], out unused);
            }
            // Each bolt starts on its own variant and crackles through all of them; light and bloom cycle in step.
            StormBoltCycles = new Sprite[StormBoltVariants][]; StormBoltLightCycles = new Sprite[StormBoltVariants][];
            for (int f = 0; f < StormBoltVariants; f++)
            {
                StormBoltCycles[f] = new Sprite[StormBoltVariants]; StormBoltLightCycles[f] = new Sprite[StormBoltVariants];
                for (int k = 0; k < StormBoltVariants; k++) { StormBoltCycles[f][k] = StormBolts[(f + k) % StormBoltVariants]; StormBoltLightCycles[f][k] = StormBoltLights[(f + k) % StormBoltVariants]; }
            }

            // A jagged ring of lightning with gaps (each strand tapers to points at them): discharge shells and the crackle round a hand.
            StormArcRings = new Sprite[3]; StormArcLights = new Sprite[3]; StormArcGlows = new Sprite[3];
            for (int f = 0; f < StormArcRings.Length; f++)
            {
                var rng = new System.Random(7300 + f * 11);
                float[] coarse = StormLoop(rng, 18, .07f), fine = StormLoop(rng, 60, .025f), other = StormLoop(rng, 23, .085f), gaps = StormLoop(rng, 6, 1);
                StormBake("Storm arc ring " + f, 256, 256, (u, v) =>
                {
                    float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Vector2.zero;
                    float t = Mathf.Atan2(v, u) / (2 * Mathf.PI), level = 0, bloom = 0;
                    for (int strand = 0; strand < 2; strand++)
                    {
                        float[] big = strand == 0 ? coarse : other; float offset = strand * .37f, baseRadius = strand == 0 ? .6f : .57f;
                        float s = Smooth(-.35f, .2f, StormLoopAt(gaps, t + strand * .5f)); if (s < .05f) continue;
                        float radius = baseRadius + StormLoopAt(big, t) + StormLoopAt(fine, t + offset);
                        float slope = (StormLoopAt(big, t + .003f) + StormLoopAt(fine, t + offset + .003f) - StormLoopAt(big, t - .003f) - StormLoopAt(fine, t + offset - .003f)) / (.006f * 2 * Mathf.PI);
                        float d = Mathf.Abs(r - radius) * 128 / Mathf.Sqrt(1 + Sq(slope / Mathf.Max(.2f, r)));
                        float w = (strand == 0 ? 1.45f : 1.05f) * s * (.82f + .18f * Mathf.Sin(t * 53 + strand * 2));
                        level = Mathf.Max(level, StormLevel(d / Mathf.Max(.2f, w))); bloom = Mathf.Max(bloom, StormBloom(d, Mathf.Max(.2f, w)) * s);
                    }
                    float edge = Window(r);
                    return new Vector2(level * edge, bloom * edge);
                }, true, out StormArcLights[f], out StormArcGlows[f], out StormArcRings[f]);
            }

            // Two motes chasing each other round a circle with tapering comet tails; spinning it orbits a missile.
            StormTwin = Build("Storm twin motes", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), degrees = Mathf.Atan2(v, u) * Mathf.Rad2Deg, a = 0;
                for (int m = 0; m < 2; m++)
                {
                    float centre = m * 180, behind = Mathf.Repeat(centre - degrees, 360);
                    float mx = Mathf.Cos(centre * Mathf.Deg2Rad) * .6f, my = Mathf.Sin(centre * Mathf.Deg2Rad) * .6f, md = Mathf.Sqrt(Sq(u - mx) + Sq(v - my));
                    float mote = Mathf.Clamp01(1.15f - md / .15f);
                    float k = behind / 140f, tail = k < 1 ? Mathf.Clamp01(1 - Mathf.Abs(r - .6f) / (.075f * (1 - k) + .008f)) * (.95f - k * .55f) : 0;
                    a = Mathf.Max(a, Mathf.Max(mote, tail));
                }
                return White(Mathf.Clamp01(a) * Window(r));
            });

            // A prism of light, long axis vertical: brightest along its spine and its upper-left edge, easing smoothly toward the
            // far edge, with a streak of refraction inside (the painted ramp turns it white-hot at the spine, coloured at the rim).
            StormShard = Build("Storm shard", 64, 128, (u, v) =>
            {
                float d = Mathf.Abs(u) / .7f + Mathf.Abs(v) / .96f; if (d > 1) return Color.clear;
                float inside = 1 - Smooth(.9f, 1, d), half = .7f * (1 - Mathf.Abs(v) / .96f) + .001f;
                float spine = Mathf.Clamp01(1 - Mathf.Abs(u + .06f) / half);
                float streak = Gauss(u - .18f + v * .25f, .07f) * .25f, grain = Fbm(u * 4 + 2, v * 6 + 1, 2);
                float lit = .3f + .58f * Mathf.Pow(spine, .8f) + .14f * Mathf.Clamp01(-u * 1.3f + v * .5f) + streak + .12f * (grain - .5f);
                return White(inside * Mathf.Clamp01(lit));
            });

            // A bold seal (thick rings, a glyph band, an eight-point star and studs) painted as crisp lines with white spines,
            // and its bloom: a soft glow hugging every line.
            StormBake("Storm seal", 384, 384, StormSealField, true, out StormSeal, out StormSealGlow, out StormSealBody);

            StormCloudFrames = new Sprite[billowFrames.Length][];
            for (int k = 0; k < billowFrames.Length; k++) { var b = billowFrames[k]; StormCloudFrames[k] = new[] { b[0], b[0], b[1], b[1], b[1], b[2], b[2], b[3], b[4], b[5] }; }
        }

        /// <summary>The light level of a painted stroke <paramref name="x"/> core widths from its spine: a smooth ramp, white-hot
        /// through the core, easing through a pale band and a wide electric band to the crisp deep edge near 4.15.</summary>
        private static float StormLevel(float x) { return Mathf.Clamp01(1 - Mathf.Pow(Mathf.Max(0, x) / 4.15f, 1.15f)); }

        /// <summary>The bloom of a stroke <paramref name="d"/> pixels from its spine, whose core is <paramref name="w"/> pixels:
        /// a glow a few times wider than the light, with a long faint skirt.</summary>
        private static float StormBloom(float d, float w) { return Mathf.Clamp01(.8f * Gauss(d, 2.4f * w + 3) + .32f * Gauss(d, 5 * w + 7)); }

        /// <summary>Bakes a painted shape: the light (white, alpha = level), its bloom (soft, added to the smooth set so it glows
        /// under the stroke) and, when <paramref name="withBody"/>, a deep body: the light's silhouette pulled in from its edge,
        /// shaded a little toward the core, for alpha layers that give the stroke weight without drawing a rim round it.</summary>
        private static void StormBake(string name, int width, int height, StormField field, bool withBody, out Sprite light, out Sprite bloom, out Sprite body)
        {
            var lit = new Color32[width * height]; var halo = new Color32[width * height]; var deep = withBody ? new Color32[width * height] : null;
            for (int y = 0; y < height; y++)
            {
                float v = (y + .5f) / height * 2 - 1;
                for (int x = 0; x < width; x++)
                {
                    Vector2 f = field((x + .5f) / width * 2 - 1, v);
                    float level = Mathf.Clamp01(f.x), shine = Mathf.Clamp01(Mathf.Max(f.y, level * .6f));
                    int i = y * width + x;
                    lit[i] = new Color32(255, 255, 255, (byte)(level * 255));
                    halo[i] = new Color32(255, 255, 255, (byte)(shine * 255));
                    if (deep != null)
                    {
                        byte g = (byte)(Mathf.Lerp(.78f, 1, Smooth(.5f, .9f, level)) * 255);
                        deep[i] = new Color32(g, g, g, (byte)(Smooth(.34f, .5f, level) * 255));
                    }
                }
            }
            light = StormSprite(name + " light", width, height, lit);
            bloom = StormSprite(name + " bloom", width, height, halo); soft.Add(bloom);
            body = deep != null ? StormSprite(name, width, height, deep) : null;
        }

        private static Sprite StormSprite(string name, int width, int height, Color32[] colors)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "Tower effect " + name, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 2 };
            texture.SetPixels32(colors); texture.Apply(true, false);
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        /// <summary>A painted line <paramref name="x"/> from its spine, <paramref name="w"/> wide (the light's reach): a tent of
        /// light with a soft glow round it.</summary>
        private static void StormLine(ref float level, ref float bloom, float x, float w, float peak = 1)
        {
            float t = Mathf.Abs(x) / w;
            level = Mathf.Max(level, peak * Mathf.Clamp01(1.06f * (1 - t)));
            bloom = Mathf.Max(bloom, peak * (.7f * Gauss(t, 2.4f) + .28f * Gauss(t, 5.5f)));
        }

        private static Vector2 StormSealField(float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v); if (r > .99f) return Vector2.zero;
            float th = Mathf.Atan2(v, u), deg = Mathf.Repeat(th * Mathf.Rad2Deg, 360), level = 0, bloom = 0;
            StormLine(ref level, ref bloom, r - .915f, .038f);
            StormLine(ref level, ref bloom, r - .845f, .016f, .85f);
            StormLine(ref level, ref bloom, r - .64f, .03f);
            StormLine(ref level, ref bloom, r - .3f, .024f, .9f);
            if (r > .655f && r < .835f)
            {
                // The glyph band: one glyph per eighteen degrees, strokes measured in true distance across the cell.
                float cell = deg / 18f; int slot = (int)cell; float lx = cell - slot, ly = (r - .67f) / .15f;
                float arc = 18 * Mathf.Deg2Rad * r, X = lx * arc, Y = ly * .15f, best = 1e9f;
                uint h = Hash(slot * 13 + 5);
                if ((h & 1u) != 0) best = Mathf.Min(best, SegmentDistance(X, Y, .5f * arc, .02f, .5f * arc, .13f));
                if ((h & 2u) != 0) best = Mathf.Min(best, SegmentDistance(X, Y, .2f * arc, .075f, .8f * arc, .075f));
                if ((h & 4u) != 0) best = Mathf.Min(best, SegmentDistance(X, Y, .2f * arc, .125f, .8f * arc, .125f));
                if ((h & 8u) != 0) best = Mathf.Min(best, Mathf.Abs(Mathf.Sqrt(Sq(X - .5f * arc) + Sq(Y - .068f)) - .034f));
                if ((h & 16u) != 0) best = Mathf.Min(best, SegmentDistance(X, Y, .22f * arc, .02f, .74f * arc, .13f));
                if (best > 1e8f) best = SegmentDistance(X, Y, .3f * arc, .075f, .7f * arc, .075f);
                StormLine(ref level, ref bloom, best, .016f, .95f);
            }
            float seg = 1e9f;
            for (int i = 0; i < 8; i++)
            {
                float a0 = (i * 45 + 22.5f) * Mathf.Deg2Rad, a1 = ((i + 3) * 45 + 22.5f) * Mathf.Deg2Rad;
                seg = Mathf.Min(seg, SegmentDistance(u, v, Mathf.Cos(a0) * .64f, Mathf.Sin(a0) * .64f, Mathf.Cos(a1) * .64f, Mathf.Sin(a1) * .64f));
            }
            StormLine(ref level, ref bloom, seg, .026f, .92f);
            float tick = Mathf.Abs(Mathf.Repeat(deg + 22.5f, 45) - 22.5f) * Mathf.Deg2Rad * r;
            StormLine(ref level, ref bloom, Mathf.Sqrt(Sq(tick) + Sq(r - .915f)), .056f);
            StormLine(ref level, ref bloom, Mathf.Sqrt(Sq(tick) + Sq(r - .64f)), .042f, .9f);
            // Brush streaks running round the seal.
            level *= .9f + .2f * (RingNoise(th, r, 2.4f, 11) - .5f);
            float edge = Window(r);
            return new Vector2(level, bloom * edge);
        }

        private static Vector2 StormBoltField(StormPath[] paths, int variant, float u, float v)
        {
            float x = u * StormBoltWidth * .5f, y = v * StormBoltHeight * .5f, level = 0, bloom = 0;
            const float reach = 44;
            for (int k = 0; k < paths.Length; k++)
            {
                var path = paths[k];
                if (x < path.min.x - reach || x > path.max.x + reach || y < path.min.y - reach || y > path.max.y + reach) continue;
                int from = 0, to = path.points.Length - 1;
                if (path.column != null)
                {
                    int segment = path.column[Mathf.Clamp((int)(x + StormBoltWidth * .5f), 0, StormBoltWidth - 1)];
                    from = Mathf.Max(0, segment - 4); to = Mathf.Min(path.points.Length - 1, segment + 5);
                }
                float along, d = StormSegmentsDistance(path.points, from, to, x, y, out along);
                if (d > reach) continue;
                // The trunk swells and pinches along its length and tapers to points at both ends; limbs thin out to their tips.
                float w = path.core;
                if (path.column != null) w *= (.8f + .2f * Mathf.Sin(along * 47 + k + variant)) * (.3f + .7f * Smooth(0, .07f, Mathf.Min(along, 1 - along)));
                else w *= .15f + .85f * Mathf.Pow(Mathf.Max(0, 1 - along), .8f);
                w = Mathf.Max(.25f, w);
                // Brush grain: the bands wander slowly along the stroke and fine streaks run inside it.
                float grain = Fbm(along * 9 + k * 3.7f + variant * 1.3f, d / w * .35f + k, 2);
                float streaks = Fbm(along * 34 + k * 1.9f, d * .3f + variant * 2.1f, 2);
                float t = d / w * (1 + .3f * (grain - .5f));
                level = Mathf.Max(level, StormLevel(t) * (.86f + .2f * (streaks - .5f)));
                bloom = Mathf.Max(bloom, StormBloom(d, w) * (path.column != null ? 1 : .8f));
            }
            float edge = (1 - Smooth(.9f, 1, Mathf.Abs(v))) * (1 - Smooth(.975f, 1, Mathf.Abs(u)));
            return new Vector2(Mathf.Clamp01(level) * edge, bloom * edge);
        }

        // A trunk from end to end of the texture with two to four forks (some forking again), in pixel space.
        private static StormPath[] StormForkedPaths(System.Random rng)
        {
            float reach = StormBoltWidth * .5f * .955f;
            var paths = new List<StormPath>();
            var trunk = StormDisplace(rng, new Vector2(-reach, 0), new Vector2(reach, 0), 5, 22, .56f, true, 30);
            paths.Add(StormMakePath(trunk, 2.4f, true));
            int limbs = 2 + rng.Next(3);
            for (int b = 0; b < limbs; b++)
            {
                Vector2 start = trunk[5 + rng.Next(trunk.Count - 14)];
                float angle = (rng.NextDouble() < .5 ? -1 : 1) * (22 + (float)rng.NextDouble() * 32), length = 45 + (float)rng.NextDouble() * 70;
                Vector2 end = start + Direction(angle) * length; end.x = Mathf.Min(end.x, reach - 6); end.y = Mathf.Clamp(end.y, -46, 46);
                var limb = StormDisplace(rng, start, end, 3, length * .14f, .55f, false, 48);
                paths.Add(StormMakePath(limb, 1.55f, false));
                if (rng.NextDouble() < .65)
                {
                    Vector2 fork = limb[2 + rng.Next(limb.Count - 4)];
                    float turn = angle + (rng.NextDouble() < .5 ? -1 : 1) * (18 + (float)rng.NextDouble() * 26), twig = 20 + (float)rng.NextDouble() * 28;
                    Vector2 tip = fork + Direction(turn) * twig; tip.x = Mathf.Min(tip.x, reach - 6); tip.y = Mathf.Clamp(tip.y, -50, 50);
                    paths.Add(StormMakePath(StormDisplace(rng, fork, tip, 2, twig * .15f, .55f, false, 52), 1.1f, false));
                }
            }
            return paths.ToArray();
        }

        private static List<Vector2> StormDisplace(System.Random rng, Vector2 a, Vector2 b, int levels, float amplitude, float decay, bool verticalOnly, float clampY)
        {
            var points = new List<Vector2> { a, b };
            for (int level = 0; level < levels; level++, amplitude *= decay)
            {
                var next = new List<Vector2>(points.Count * 2);
                for (int i = 0; i < points.Count - 1; i++)
                {
                    Vector2 p = points[i], q = points[i + 1], mid = (p + q) * .5f, d = q - p;
                    float offset = ((float)rng.NextDouble() * 2 - 1) * amplitude;
                    mid += verticalOnly ? new Vector2(0, offset) : new Vector2(-d.y, d.x).normalized * offset;
                    mid.y = Mathf.Clamp(mid.y, -clampY, clampY);
                    next.Add(p); next.Add(mid);
                }
                next.Add(points[points.Count - 1]); points = next;
            }
            return points;
        }

        private static StormPath StormMakePath(List<Vector2> points, float core, bool monotonic)
        {
            var path = new StormPath { points = points.ToArray(), core = core, min = points[0], max = points[0] };
            foreach (var p in points) { path.min = Vector2.Min(path.min, p); path.max = Vector2.Max(path.max, p); }
            if (monotonic)
            {
                path.column = new int[StormBoltWidth];
                int segment = 0;
                for (int x = 0; x < StormBoltWidth; x++)
                {
                    float px = x + .5f - StormBoltWidth * .5f;
                    while (segment < path.points.Length - 2 && path.points[segment + 1].x < px) segment++;
                    path.column[x] = segment;
                }
            }
            return path;
        }

        private static float StormSegmentsDistance(Vector2[] points, int from, int to, float x, float y, out float along)
        {
            float best = float.MaxValue; along = 0;
            for (int i = from; i < to; i++)
            {
                Vector2 a = points[i], b = points[i + 1]; float dx = b.x - a.x, dy = b.y - a.y, length = dx * dx + dy * dy;
                float t = length > 0 ? Mathf.Clamp01(((x - a.x) * dx + (y - a.y) * dy) / length) : 0;
                float ex = x - a.x - dx * t, ey = y - a.y - dy * t, d = ex * ex + ey * ey;
                if (d < best) { best = d; along = (i + t) / (points.Length - 1); }
            }
            return Mathf.Sqrt(best);
        }

        private static float[] StormLoop(System.Random rng, int count, float amplitude)
        {
            var values = new float[count];
            for (int i = 0; i < count; i++) values[i] = ((float)rng.NextDouble() * 2 - 1) * amplitude;
            return values;
        }

        private static float StormLoopAt(float[] values, float t)
        {
            float x = Mathf.Repeat(t, 1) * values.Length; int i = (int)x;
            return Mathf.Lerp(values[i % values.Length], values[(i + 1) % values.Length], x - i);
        }

        /// <summary>A deep, saturated version of a hue (never near-black) for alpha bodies laid inside a light shape.</summary>
        private static Color StormDeep(Color color, float alpha)
        {
            float h, s, v; Color.RGBToHSV(color, out h, out s, out v);
            Color deep = Color.HSVToRGB(h, Mathf.Clamp01(s * 1.25f + .2f), .72f); deep.a = alpha; return deep;
        }

        /// <summary>The tint of a bolt: storm tints become electric blue; other tints (oath links) keep most of their own hue.</summary>
        private static Color StormBoltTone(Color light)
        {
            bool storm = light.b >= light.r && light.b >= light.g * .95f;
            Color tone = Color.Lerp(light, StormBlue, storm ? .78f : .3f); tone.a = 1; return tone;
        }

        /// <summary>The tint of an arcane missile and its burst: violet, a little of the caster's tint kept.</summary>
        private static Color StormArcaneTone(Color tint) { Color tone = Color.Lerp(tint, StormArcane, .7f); tone.a = 1; return tone; }

        /// <summary>The caster's feet: the anchor is the actor root standing on the floor; without one, a body-height guess.</summary>
        private static Vector2 StormFeet(CastInfo c)
        {
            if (c.anchor) { Vector2 root = c.anchor.position; if (Mathf.Abs(root.y - (c.pos.y - 1)) < .6f) return root; }
            return c.pos - new Vector2(0, 1);
        }

        /// <summary>The blast flipbook (it billows from a white-hot heart, licks flame tongues and tears into wisps) over its
        /// deeper body copy, popping on after <paramref name="delay"/>. Unlike the shared Blast it brings no bloom or spark of
        /// its own: the strike's hit spark and its bloom sit with it, so the two never stack into a white disc. A
        /// <paramref name="bodyAlpha"/> above zero glazes the blast's white-hot heart with a saturated deep body of its hue (an
        /// alpha copy a little inside it, whose rising cut covers only the heart), so a pale hue keeps its colour on the pale sky.</summary>
        private Clip StormBurst(Vector2 pos, float width, Color color, float life, int order, float delay = 0, float bodyAlpha = 0)
        {
            var back = Spawn("Storm blast body", blastFrames[0], pos + Vector2.down * width * .04f, width * 1.12f, Alpha(Color.Lerp(color, Color.black, .25f), .85f), life * 1.1f, true, order - 1);
            var blast = Spawn("Storm blast", blastFrames[0], pos, width, Alpha(color, 1), life, true, order);
            var layers = new List<Clip> { back, blast };
            if (bodyAlpha > 0) layers.Add(Spawn("Storm blast glaze", blastFrames[0], pos, width * .9f, StormDeep(color, bodyAlpha), life, false, order + 1));
            foreach (var clip in layers)
            {
                clip.frames = blastFrames; clip.loop = false; clip.angle = Range(-12, 12); clip.startScale = .9f; clip.endScale = 1.08f; clip.ease = 2; clip.hold = .6f; clip.fadePower = .9f;
                clip.age = -delay;
            }
            back.angle = blast.angle + Range(-25, 25);
            return blast;
        }

        /// <summary>A shock front lying on the floor: it spins in the ground plane instead of tilting as a squashed ring would.</summary>
        private Clip StormFloorWave(Vector2 pos, float width, Color color, float life, float delay = 0)
        {
            var wave = Wave(pos, width, color, life, 4); LayFlat(wave, .34f); wave.renderer.sortingOrder = 116; wave.age = -delay;
            return wave;
        }

        /// <summary>A shock front seen edge-on (a launch gate), squashed across without being turned off its axis.</summary>
        private Clip StormEdgeWave(Vector2 pos, float width, Color color, float life, float squash, float delay = 0)
        {
            var wave = Wave(pos, width, color, life, 4); wave.angle = Chance(.5f) ? 0 : 180; wave.scale.x *= squash; wave.age = -delay;
            return wave;
        }

        // ---------------------------------------------------------------- lightning builders

        /// <summary>One forked lightning bolt stretched between two points: its painted light, or its soft bloom when
        /// <paramref name="bloom"/> is set; it crackles through the baked variants.</summary>
        private Clip StormStroke(Vector2 a, Vector2 b, float thickness, Color color, float life, bool bloom, int order, int variant, bool flip, bool crackle = true)
        {
            Vector2 delta = b - a; float length = Mathf.Max(.05f, delta.magnitude) * 1.045f;
            Sprite[] cycle = (bloom ? StormBoltCycles : StormBoltLightCycles)[Mathf.Abs(variant) % StormBoltVariants];
            var clip = Spawn(bloom ? "Lightning bloom" : "Lightning bolt", cycle[0], (a + b) * .5f, length, color, life, true, order);
            Vector2 size = cycle[0].bounds.size;
            clip.scale = new Vector3(length / size.x, thickness / size.y * (flip ? -1 : 1), 1);
            clip.angle = delta.sqrMagnitude > 1e-6f ? Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg : 90; clip.ease = 1;
            clip.root.transform.localScale = clip.scale; clip.root.transform.localRotation = Quaternion.Euler(0, 0, clip.angle);
            if (crackle) { clip.frames = cycle; clip.fps = 20; clip.loop = true; }
            return clip;
        }

        /// <summary>A painted bolt popping on after <paramref name="delay"/>: its light over its bloom (same variant, so both
        /// crackle in step). The bloom glows in the light's own hue; <paramref name="bloom"/>'s alpha says how strongly, and no
        /// alpha draws the light alone. Returns the light.</summary>
        private Clip StormPainted(Vector2 a, Vector2 b, float thickness, Color light, Color bloom, float life, float delay, int order, int variant, bool flip, bool crackle = true)
        {
            var over = StormStroke(a, b, thickness, light, life, false, order, variant, flip, crackle);
            over.age = -delay; over.hold = .3f; over.fadePower = 1.3f; over.flicker = .16f;
            if (bloom.a > 0)
            {
                Color hue = Color.Lerp(light, StormHot, .12f);
                var halo = StormStroke(a, b, thickness, Alpha(hue, Mathf.Min(1, bloom.a) * .72f * Mathf.Max(.4f, light.a)), life * 1.15f, true, order - 1, variant, flip, crackle);
                halo.age = -delay; halo.hold = .25f; halo.fadePower = 1.6f; halo.flicker = .22f;
            }
            return over;
        }

        /// <summary>A small crackling arc of painted light, in its glow when <paramref name="glowing"/>.</summary>
        private Clip StormArc(Vector2 a, Vector2 b, float thickness, float life, float delay, float alpha = 1, bool glowing = true)
        {
            var arc = StormPainted(a, b, thickness, Alpha(StormBlue, alpha), glowing ? Alpha(StormBlue, .75f * alpha) : Color.clear, life, delay, 127, random.Next(StormBoltVariants), Chance(.5f));
            arc.flicker = .3f; arc.hold = .2f; arc.fadePower = 1.4f;
            return arc;
        }

        /// <summary>A strike's hit spark: a bloom, the spark flipbook in <paramref name="tone"/> and a smaller white one turned
        /// apart (both flare, split into shards and scatter), popping on after <paramref name="delay"/>. Returns the white spark.</summary>
        private Clip StormSpark(Vector2 pos, float width, Color tone, float life, float delay = 0, float bloomAlpha = .55f)
        {
            if (bloomAlpha > 0)
            {
                var bloom = Spawn("Storm spark bloom", glow, pos, width * 1.3f, Alpha(tone, bloomAlpha), Mathf.Max(.2f, life * 1.8f), true, 130);
                bloom.age = -delay; bloom.startScale = .6f; bloom.endScale = 1.2f; bloom.ease = 2; bloom.fadePower = 1.5f;
            }
            var colour = SparkFlip(pos, width, Alpha(tone, 1), life, 132); colour.age = -delay;
            var hot = SparkFlip(pos, width * .62f, Alpha(StormHot, 1), life * .8f, 133); hot.age = -delay; hot.angle = colour.angle + Range(20, 40);
            return hot;
        }

        /// <summary>A thunderhead: soft slate billows underneath, paler lit billows heaped on their upper left, and lightning
        /// flickering inside it (drawn between the two, so it lights the dark underside).</summary>
        private void StormThunderhead(Vector2 at, float width, float life, float fadeIn, float delay, Color light, int puffs, float glowAlpha = .45f)
        {
            // A soft slate haze joins the billows into one mass.
            var mass = Spawn("Thunderhead body", glow, at + Vector2.down * width * .02f, width * 1.2f, Alpha(StormCloudDark, .6f), life, false, 111);
            mass.scale.y *= .42f; mass.root.transform.localScale = mass.scale; mass.age = -delay;
            mass.fadeIn = Mathf.Max(.04f, fadeIn); mass.startScale = .7f; mass.endScale = 1.1f; mass.hold = .45f; mass.fadePower = 1.4f;
            for (int i = 0; i < puffs; i++)
            {
                float t = puffs == 1 ? 0 : i / (float)(puffs - 1) * 2 - 1;
                var under = Puff(at + new Vector2(t * width * .3f + Range(-.1f, .1f), Range(-.12f, .04f)), width * Range(.56f, .7f), StormCloudDark, new Vector2(t * .3f, 0), life, 112);
                StormCloudPose(under, fadeIn, delay); under.scale.y *= .7f;
                if (i % 2 == 0 || puffs <= 3)
                {
                    var top = Puff(at + new Vector2(t * width * .26f - width * .06f, width * .09f + Range(-.03f, .06f)), width * Range(.42f, .52f), StormCloudLit, new Vector2(t * .25f, .05f), life * .95f, 114);
                    StormCloudPose(top, fadeIn, delay); top.scale.y *= .66f;
                }
            }
            if (glowAlpha <= 0) return;
            var inner = Spawn("Thunderhead light", glow, at + Vector2.down * width * .03f, width * .75f, Alpha(light, glowAlpha), life, true, 113);
            inner.scale.y *= .55f; inner.root.transform.localScale = inner.scale; inner.age = -delay; inner.fadeIn = fadeIn; inner.flicker = .7f; inner.hold = .5f; inner.fadePower = 1.5f;
        }

        // Billows held level so their baked light stays on the upper left; they swell slowly, hold and tear apart at the end.
        private void StormCloudPose(Clip puff, float fadeIn, float delay)
        {
            puff.frames = ReferenceEquals(puff.frames, billowFrames[0]) ? StormCloudFrames[0] : StormCloudFrames[StormCloudFrames.Length - 1];
            puff.age = -delay; puff.angle = Range(-12, 12); puff.spin = Range(-8, 8); puff.drag = 1.2f;
            puff.fadeIn = Mathf.Max(.04f, fadeIn); puff.startScale = .7f; puff.endScale = 1.15f; puff.hold = .45f; puff.fadePower = 1.4f;
        }

        /// <summary>A full bolt: the painted bolt in its bloom, a restrike on another variant as it frays and a short ionised
        /// afterimage that thins to its core.</summary>
        private void StormMainBolt(Vector2 a, Vector2 b, float thickness, Color light, float life, float delay, bool afterimage, Color? boltTint = null)
        {
            Color tone = boltTint.HasValue ? new Color(boltTint.Value.r, boltTint.Value.g, boltTint.Value.b, 1) : StormBoltTone(light);
            int variant = random.Next(StormBoltVariants); bool flip = Chance(.5f);
            var bolt = StormPainted(a, b, thickness, tone, Alpha(tone, 1), life, delay, 129, variant, flip); bolt.hold = .35f; bolt.fadePower = 1.4f;
            var again = StormPainted(a, b, thickness * .8f, Alpha(Color.Lerp(tone, Color.white, .25f), .95f), Color.clear, life * .5f, delay + life * .4f, 131, variant + 2, !flip);
            again.flicker = .3f;
            if (afterimage)
            {
                // Long enough to sell the flash on the eye, short enough not to smear: the bands erode to a thin core line.
                var ghost = StormStroke(a, b, thickness, Alpha(Color.Lerp(tone, new Color(.62f, .55f, 1), .5f), .4f), .26f, false, 127, variant, flip, false);
                ghost.age = -(delay + life * .55f); ghost.fadePower = 1.5f;
            }
        }

        /// <summary>A thunderbolt from a thunderhead down onto the ground: forked painted bolt, a hit spark and a small electric
        /// blast at the ground, a light column, shock fronts along the floor, scorch and glowing cracks.</summary>
        private void StormSkyStrike(Vector2 ground, Vector2 top, Color light, float delay, float scale, bool lean = false, Color? boltTint = null)
        {
            Color tone = boltTint.HasValue ? new Color(boltTint.Value.r, boltTint.Value.g, boltTint.Value.b, 1) : StormBoltTone(light);
            StormMainBolt(top, ground, 2.6f * scale, light, .3f, delay, true, boltTint);
            Vector2 split = Vector2.Lerp(top, ground, Range(.4f, .6f));
            var fork = StormPainted(split, ground + new Vector2((Chance(.5f) ? 1 : -1) * Range(.9f, 1.7f) * scale, 0), 1.6f * scale, Alpha(tone, .95f), lean ? Color.clear : Alpha(tone, .6f), .2f, delay + .02f, 127, random.Next(StormBoltVariants), Chance(.5f));
            fork.flicker = .4f; fork.fadePower = 1.5f;
            StormThunderhead(top + Vector2.up * .45f, 4.8f * scale, .7f, .04f, Mathf.Max(0, delay - .04f), light, 3, .3f);
            var glare = Spawn("Thunderhead flash", glow, top, 2.6f * scale, Alpha(light, .55f), .2f, true, 115); glare.age = -delay; glare.flicker = .6f; glare.fadePower = 1.5f;
            // The strike lands in a hit spark and a small electric blast, its shards thrown along the floor.
            Vector2 hit = ground + Vector2.up * .2f;
            StormSpark(hit, 1.9f * scale, tone, .15f, delay, .6f);
            float blast = (lean ? 1.2f : 1.5f) * scale;
            StormBurst(hit + Vector2.up * .12f * scale, blast, Color.Lerp(tone, StormDeepBlue, .3f), .36f, 127, delay);
            var skid = SparkFlip(hit, 3.8f * scale, Alpha(tone, .9f), .2f, 131); skid.angle = 0; skid.scale.y *= .45f; skid.age = -delay;
            var column = Pillar(ground, 1.1f * scale, (top.y - ground.y) * .8f, Alpha(tone, .75f), .2f, 125); column.age = -delay;
            StormFloorWave(ground + Vector2.up * .05f, 4.6f * scale, Alpha(tone, .9f), .3f, delay);
            if (!lean) StormFloorWave(ground + Vector2.up * .05f, 3 * scale, new Color(1, 1, 1, .75f), .22f, delay);
            var burn = Scorch(ground, 3.2f * scale, 1.6f); burn.age = -delay; burn.color = new Color(.12f, .12f, .22f, .55f);
            var crackDark = Decal(ground, crack, 3.6f * scale, new Color(.14f, .14f, .26f, .7f), 1.5f, false, 25); crackDark.angle = Range(-6, 6); crackDark.age = -delay;
            var crackHot = Decal(ground, crack, 3.6f * scale, Alpha(tone, 1), 1.2f, true, 26); crackHot.angle = crackDark.angle; crackHot.fadePower = 1.6f; crackHot.age = -delay; crackHot.flicker = .25f;
            // Residue: static still crawling over the burn, pale ionised smoke curling up and charged flecks drifting off
            // (skipped on lean strikes: the links of a higher-level chain, which brings more of them).
            if (!lean)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2 start = ground + new Vector2(Range(-1.1f, 1.1f) * scale, .08f);
                    StormArc(start, start + new Vector2(Range(-.9f, .9f), Range(.05f, .5f)) * scale, .5f * scale, .09f, delay + Range(.2f, .9f), .9f, false);
                }
                for (int i = 0; i < 2; i++)
                {
                    var smoke = Puff(ground + new Vector2(Range(-.6f, .6f) * scale, .3f), Range(1, 1.4f) * scale, StormIon, new Vector2(Range(-.3f, .3f), Range(.9f, 1.5f)), Range(1, 1.3f), 112);
                    smoke.age = -(delay + .12f); smoke.drag = 1.2f; smoke.fadeIn = .12f; smoke.endScale = 2.2f;
                }
                for (int i = 0; i < 4; i++)
                {
                    var mote = Spawn("Static fleck", fleck, ground + new Vector2(Range(-1, 1) * scale, Range(.1f, .5f)), Range(.2f, .32f), i % 2 == 0 ? StormHot : tone, Range(.6f, .9f), true, 131);
                    mote.age = -(delay + Range(.1f, .3f)); mote.velocity = new Vector2(Range(-.4f, .4f), Range(.7f, 1.4f)); mote.drag = .6f; mote.flicker = .6f; mote.endScale = .3f; mote.ease = 1; mote.angle = Range(-20, 20);
                }
            }
            int sparks = GradeSteady(lean ? 6 : 9);
            for (int i = 0; i < sparks; i++)
            {
                var s = Spark(ground + Vector2.up * .1f, new Vector2(Range(-6, 6), Range(3, 9)) * scale, i % 3 == 0 ? Color.white : tone, Range(.3f, .5f), Range(.3f, .5f), 16, 1.2f);
                s.age = -delay;
            }
            for (int i = 0; i < 2; i++)
            {
                var dust = Puff(ground + new Vector2(Range(-.6f, .6f), .2f), Range(.8f, 1.2f) * scale, DustTone, new Vector2(Range(-2.4f, 2.4f), Range(.4f, 1.2f)), Range(.6f, .9f), 118);
                dust.age = -delay;
            }
        }

        // ---------------------------------------------------------------- chain lightning

        private void CastChainLightning(CastInfo c)
        {
            float w = c.windup, release = Mathf.Max(0, w - .02f); int f = c.facing; bool echo = w < .2f;
            // The orb is held out in front of the chest so the caster's face stays readable through the charge.
            Vector2 hand = c.pos + new Vector2(f * .8f, .2f), feet = StormFeet(c);
            Color light = Color.Lerp(c.light, StormLight, .6f), tone = StormBoltTone(light);
            // Higher levels swell the orb and throw more arcs; level three wraps the caster in a crackling shell and the
            // thunderhead answers a third time.
            int g = Grade; float gs = 1 + (g - 1) * .12f;
            float orbSize = (echo ? .95f : 1.4f) * gs;

            // A crackling plasma ball in the hand that grows to the release: a wide electric bloom, the plasma orb (filaments
            // swirling inside, a lit rim, flares licking off it), a white-hot heart and a ring of arcs crackling round it in
            // their own glow.
            var bloom = Anchored(c.anchor, hand, "Storm orb bloom", glow, orbSize * 2.1f, Alpha(tone, .55f), w + .1f, 120);
            bloom.startScale = .4f; bloom.endScale = 1; bloom.ease = 1.6f; bloom.fadeIn = w * .3f; bloom.hold = .8f; bloom.fadePower = 2; bloom.flicker = .25f;
            var ball = Anchored(c.anchor, hand, "Storm orb", plasmaFrames[0], orbSize * 1.45f, Alpha(StormPlasma, 1), w + .08f, 123);
            ball.frames = plasmaFrames; ball.fps = 20; ball.loop = true;
            ball.startScale = .3f; ball.endScale = 1; ball.ease = 1.6f; ball.fadeIn = .03f; ball.hold = .8f; ball.fadePower = 2; ball.pulse = .06f;
            var heart = Anchored(c.anchor, hand + new Vector2(-.06f, .07f) * orbSize, "Storm orb core", core, orbSize * .36f, StormHot, w + .06f, 124);
            heart.startScale = .4f; heart.endScale = 1; heart.hold = .8f; heart.fadePower = 2; heart.pulse = .12f;
            int ringVariant = random.Next(StormArcRings.Length);
            for (int layer = 0; layer < 2; layer++)
            {
                bool lit = layer == 1;
                var crackle = Anchored(c.anchor, hand, lit ? "Storm orb arcs" : "Storm orb arcs glow", (lit ? StormArcLights : StormArcGlows)[ringVariant], (echo ? 1.4f : 2.4f) * gs,
                    lit ? tone : Alpha(tone, .6f), w + .06f, lit ? 122 : 121);
                crackle.frames = lit ? StormArcLights : StormArcGlows; crackle.fps = 22; crackle.loop = true; crackle.spin = 240 * f;
                crackle.startScale = .35f; crackle.endScale = 1; crackle.ease = 1.5f; crackle.fadeIn = .05f; crackle.hold = .8f; crackle.fadePower = 2; crackle.flicker = lit ? .12f : .3f;
            }
            Emitter(ball, echo ? 10 : 16, .1f, (owner, p) =>
            {
                Vector2 at = owner.root.transform.position;
                StormArc(at, at + Direction(Range(0, 360)) * Range(.55f, 1.15f) * gs, .42f, Range(.06f, .1f), 0, 1, false);
            });

            if (!echo)
            {
                Underlay(hand, 2.4f, .26f, w + .1f).fadeIn = w * .4f;
                // A storm seal of glowing lines under the feet.
                var rune = StormRune(feet + Vector2.up * .03f, 3.3f, Alpha(tone, .75f), w + .35f, 0, true, 27); rune.pulse = .04f;
                GroundGlow(feet, 3.4f, Alpha(light, .35f), w + .3f);

                // A thunderhead gathers overhead and answers twice: a feeler bolt, then the full bolt into the hand.
                Vector2 sky = c.pos + new Vector2(f * .5f, 3.4f);
                StormThunderhead(sky, 4.8f, w + .7f, w * .55f, 0, light, 5, .5f);
                for (int i = 0; i < 3; i++)
                {
                    Vector2 start = sky + new Vector2(Range(-1.3f, 1.3f), Range(-.2f, .2f));
                    var sheet = StormArc(start, start + new Vector2(Range(.8f, 1.5f) * (Chance(.5f) ? 1 : -1), Range(-.3f, .3f)), .6f, .08f, Range(.1f, Mathf.Max(.12f, w - .05f)), 1, false);
                    sheet.renderer.sortingOrder = 115;
                }
                StormMainBolt(sky + new Vector2(Range(-.5f, .5f), 0), hand, 1, light, .12f, w * .5f, false);
                StormMainBolt(sky, hand, 1.5f * gs, light, .22f, release, false);
                if (g >= 3)
                {
                    StormMainBolt(sky + new Vector2(Range(-1, 1), .2f), hand, 1.2f, light, .12f, w * .75f, false);
                    for (int layer = 0; layer < 2; layer++)
                    {
                        bool lit = layer == 1;
                        var cage = Anchored(c.anchor, c.pos + Vector2.up * .1f, lit ? "Storm body shell" : "Storm body shell glow", (lit ? StormArcLights : StormArcGlows)[1], 3.6f,
                            Alpha(tone, lit ? .85f : .45f), w + .08f, lit ? 121 : 120);
                        cage.frames = lit ? StormArcLights : StormArcGlows; cage.fps = 18; cage.loop = true; cage.spin = -120 * f;
                        cage.startScale = .5f; cage.endScale = 1; cage.ease = 2; cage.fadeIn = w * .4f; cage.hold = .8f; cage.fadePower = 2; cage.flicker = .25f;
                    }
                }

                // Arcs leaping from the orb down to the floor.
                int legs = 4 + 2 * (g - 1);
                for (int i = 0; i < legs; i++) StormArc(hand, new Vector2(c.pos.x + f * Range(-1.2f, 1.8f), feet.y + .05f), .75f, .1f, Range(.04f, Mathf.Max(.05f, w - .06f)), 1, i % 2 == 0);
                Converge(hand, 10, 2, tone, w, .45f);
            }

            // Release: a hit spark flares out of the hand in its bloom, a small electric blast pops just ahead of it, a shock
            // front races out and needle sparks are thrown forward.
            Vector2 muzzle = hand + new Vector2(f * .3f, 0);
            StormSpark(muzzle, echo ? 1.6f : 2.3f, tone, .16f, release, .45f);
            if (!echo) StormBurst(hand + new Vector2(f * .55f, 0), 1.2f * gs, Color.Lerp(tone, StormDeepBlue, .35f), .3f, 127, release);
            var wave = Wave(muzzle, echo ? 2 : 3.2f, Alpha(tone, .9f), .24f, 4); wave.age = -release;
            int sparks = echo ? 4 : 8;
            for (int i = 0; i < sparks; i++)
            {
                var s = Spark(hand, Direction(f > 0 ? Range(-60, 60) : Range(120, 240)) * Range(4, 9), i % 2 == 0 ? Color.white : tone, Range(.3f, .5f), Range(.2f, .35f), 6, 3);
                s.age = -release;
            }
        }

        private void ImpactChainLightning(Vector2 pos, float radius, Color tint, int facing)
        {
            // At level three this is the closing sky bolt's burst: its white bloom is held back so the awakening's gold shell
            // and crown read through it.
            bool finale = Grade >= 3;
            Discharge(pos, Mathf.Clamp(radius * 1.1f, .7f, 3.2f), Color.Lerp(tint, StormLight, .55f), finale, finale ? .45f : .9f);
        }

        private void TrailChainLightning(Vector2 from, Vector2 to, Color tint)
        {
            Color light = Color.Lerp(tint, StormLight, .55f), tone = StormBoltTone(light);
            Vector2 delta = to - from; float length = delta.magnitude;
            int g = Grade; float gs = 1 + (g - 1) * .15f;
            bool fromSky = delta.y < -2.5f && Mathf.Abs(delta.x) < -delta.y * .4f;
            if (fromSky)
            {
                // A strike called down from the sky (passives, and the level-three chain's closing bolt): one huge bolt through
                // the target into the ground. At level three it is the only sky strike of the chain and falls as a twin bolt
                // painted gold-white (its column, spark and ground shock), so the finale reads apart from the blue links.
                Color strike = g >= 3 ? Color.Lerp(light, GradeGold, .7f) : light;
                Color? gold = g >= 3 ? new Color(1, .86f, .5f) : (Color?)null;
                StormSkyStrike(new Vector2(to.x, to.y - 1), from, strike, 0, g >= 3 ? 1.4f : 1.1f, g >= 3, gold);
                if (g >= 3)
                {
                    StormMainBolt(from + new Vector2(Range(.6f, 1) * (Chance(.5f) ? 1 : -1), .3f), new Vector2(to.x, to.y - 1), 1.7f, strike, .26f, .04f, true, gold);
                    if (Time.frameCount != StormFlashFrame) { StormFlashFrame = Time.frameCount; ScreenFlash(to, new Color(.75f, .82f, 1, .18f), .12f); }
                }
                StormSpark(to, 1.6f * gs, gold ?? tone, .13f, 0, .4f);
                return;
            }
            if (length < .3f) { Discharge(to, 1, light); return; }

            // Higher levels thicken every link and braid a second strand into it.
            float thickness = Mathf.Clamp(1.9f + length * .15f, 2, 2.9f) * gs;
            StormMainBolt(from, to, thickness, light, .32f, 0, true);
            var strand = StormPainted(from, to, thickness * .65f, Alpha(Color.Lerp(tone, Color.white, .2f), .85f), Color.clear, .22f, 0, 128, random.Next(StormBoltVariants), Chance(.5f));
            strand.flicker = .45f; strand.fadePower = 1.4f;
            if (g >= 2)
            {
                var braid = StormPainted(from, to, thickness * .55f, Alpha(Color.Lerp(tone, Color.white, .35f), .8f), Color.clear, .2f, .04f, 128, random.Next(StormBoltVariants), Chance(.5f));
                braid.flicker = .4f; braid.fadePower = 1.4f;
            }
            // The arc jumps off the last foe in a small hit spark with needles flung along the link.
            StormSpark(from, 1.5f, tone, .12f, 0, .4f);
            Vector2 heading = delta / length;
            for (int i = 0; i < 3; i++) Spark(from, (heading * Range(4, 7) + Inside(2.5f)), i == 0 ? Color.white : tone, Range(.3f, .45f), Range(.16f, .26f), 3, 3);
            Discharge(to, 1.2f, light, g >= 2);
            // Storm-coloured chains call thunder down onto every link; other tints (oath links) stay plain arcs. At level three
            // the sky is kept for the closing bolt on the last foe, so the links stay sky-less arcs.
            bool storm = tint.b >= tint.r && tint.b >= tint.g * .95f;
            if (storm && g < 3)
            {
                StormSkyStrike(new Vector2(to.x, to.y - 1), to + new Vector2(Range(-.7f, .7f), 7.2f), light, .05f, .85f * (1 + (g - 1) * .1f), g >= 2);
                if (Time.frameCount != StormFlashFrame) { StormFlashFrame = Time.frameCount; ScreenFlash(to, new Color(.7f, .78f, 1, .12f), .1f); }
            }
        }

        /// <summary>An electric discharge burst: a hit spark in its bloom, a small electric blast billowing and tearing apart,
        /// a shock front, a crackling arc shell in its glow, radiating arcs, needle sparks and a wisp of pale ionised smoke.</summary>
        private void Discharge(Vector2 pos, float r, Color light, bool lean = false, float bloomAlpha = .9f)
        {
            Color tone = StormBoltTone(light);
            Underlay(pos, r * 2.4f, .26f, .26f);
            StormSpark(pos, r * 1.75f, tone, .16f, 0, bloomAlpha * .35f);
            StormBurst(pos, r * (bloomAlpha < .6f ? 1.2f : 1.55f), Color.Lerp(tone, StormDeepBlue, .4f), .34f, 127);
            Wave(pos, r * 3.1f, Alpha(tone, .85f), .26f, 4);
            int variant = random.Next(StormArcRings.Length); float spin = Range(-200, 200);
            for (int layer = 0; layer < 2; layer++)
            {
                bool lit = layer == 1;
                var shell = Spawn(lit ? "Discharge arcs" : "Discharge arcs glow", (lit ? StormArcLights : StormArcGlows)[variant], pos, r * 2.3f, lit ? tone : Alpha(tone, .5f), .3f, true, lit ? 128 : 126);
                shell.frames = lit ? StormArcLights : StormArcGlows; shell.fps = 24; shell.loop = true; shell.spin = spin;
                shell.startScale = .3f; shell.endScale = 1.05f; shell.ease = 4; shell.hold = .25f; shell.fadePower = 1.4f; shell.flicker = .2f;
            }
            int arcs = Budget(4);
            for (int i = 0; i < arcs; i++)
            {
                float angle = i * 360f / arcs + Range(-25, 25);
                StormArc(pos, pos + Direction(angle) * r * Range(1.1f, 1.9f), r * .6f, Range(.1f, .18f), Range(0, .06f));
            }
            Needles(pos, GradeSteady(lean ? 7 : 10), r * 9, tone, .4f, .5f);
            // Residual crackle crawling over the target after the hit (one arc on the lean links of a higher-level chain).
            for (int i = 0; i < (lean ? 1 : 2); i++)
            {
                Vector2 start = pos + Inside(.3f * r);
                StormArc(start, start + Direction(Range(0, 360)) * r * Range(.5f, .9f), r * .4f, .07f, Range(.12f, .5f), .85f, false);
            }
            int smoke = lean ? 1 : 2;
            for (int i = 0; i < smoke; i++)
            {
                var puff = Puff(pos + Inside(.3f * r) + Vector2.up * .1f, r * Range(.7f, .95f), StormIon, new Vector2(Range(-.5f, .5f), Range(.6f, 1.2f)) * r, Range(.5f, .7f), 126);
                puff.age = -.06f; puff.endScale = 1.8f; puff.fadeIn = .06f;
            }
        }

        // ---------------------------------------------------------------- arcane volley

        /// <summary>A seal of crisp glowing lines over its own soft bloom, upright or lying on the ground (ground seals do not
        /// spin: a squashed ellipse would wobble). Both layers pop on after <paramref name="delay"/>, grow from
        /// <paramref name="from"/> to <paramref name="to"/> and hold for <paramref name="hold"/> of their life. A
        /// <paramref name="bodyAlpha"/> above zero lays a saturated deep body inside the lines (alpha, thinner than the light), so
        /// the seal keeps its colour on the pale sky. Returns the lines.</summary>
        private Clip StormRune(Vector2 pos, float width, Color color, float life, float spin, bool onGround, int order, float delay = 0, float from = .6f, float to = 1, float hold = .4f, float glowAlpha = .55f, float bodyAlpha = 0)
        {
            var layers = new List<Clip>();
            if (glowAlpha > 0) layers.Add(Spawn("Storm rune glow", StormSealGlow, pos, width, Alpha(color, color.a * glowAlpha), life * 1.05f, true, order - 2));
            if (bodyAlpha > 0) layers.Add(Spawn("Storm rune body", StormSealBody, pos, width, StormDeep(color, bodyAlpha), life, false, order - 1));
            var lines = Spawn("Storm rune circle", StormSeal, pos, width, color, life, true, order);
            layers.Add(lines);
            foreach (var clip in layers)
            {
                if (onGround) { clip.scale.y *= .34f; clip.root.transform.localScale = clip.scale; }
                clip.spin = onGround ? 0 : spin; clip.fadeIn = delay > 0 ? 0 : .06f; clip.startScale = from; clip.endScale = to; clip.ease = 5; clip.fadePower = 1.4f; clip.hold = hold;
                clip.age = -delay;
            }
            return lines;
        }

        /// <summary>A small violet plasma orb (its glow, the orb and a spindle tail) spiralling in to <paramref name="center"/>.</summary>
        private void StormOrbiter(Vector2 center, float angle, float startRadius, float endRadius, float life, float turns, Color light, float size = 1)
        {
            for (int layer = 0; layer < 3; layer++)
            {
                Clip clip;
                if (layer == 0) clip = Spawn("Arcane orb glow", glow, center, 1.35f * size, Alpha(light, .45f), life, true, 124);
                else if (layer == 1) { clip = Spawn("Arcane orb", plasmaFrames[0], center, 1.1f * size, Alpha(StormArcanePlasma, 1), life, true, 126); clip.frames = plasmaFrames; clip.fps = 18; clip.loop = true; }
                else { clip = Spawn("Arcane orb tail", spark, center, 1.3f * size, Alpha(StormPink, .9f), life, true, 125); clip.alignVelocity = true; clip.scale.y *= .7f; }
                clip.orbitCenter = center; clip.orbitAngle = angle; clip.orbit = turns * 360 / Mathf.Max(.05f, life);
                clip.orbitRadius = startRadius; clip.orbitRadiusEnd = endRadius; clip.orbitPower = 1.3f;
                clip.fadeIn = .06f; clip.hold = .85f; clip.fadePower = 2;
            }
        }

        private void CastArcaneVolley(CastInfo c)
        {
            float w = c.windup, release = Mathf.Max(0, w - .02f); int f = c.facing; bool echo = w < .2f;
            Vector2 gate = c.pos + new Vector2(f * .8f, 0);
            Color light = Color.Lerp(c.light, StormViolet, .5f), tone = StormArcaneTone(c.light);
            // One orb gathers per missile the level fires (3, 4, then 6) and the gate and mandala widen with it.
            int g = Grade; float gs = 1 + (g - 1) * .1f; int orbs = g >= 3 ? 6 : g == 2 ? 4 : 3;

            var focus = Anchored(c.anchor, gate, "Arcane focus", glow, (echo ? 1.4f : 2.1f) * gs, Alpha(light, .5f), w + .08f, 122);
            focus.fadeIn = .05f; focus.startScale = .5f; focus.endScale = 1; focus.hold = .8f; focus.fadePower = 2; focus.pulse = .06f;
            var focusStar = Anchored(c.anchor, gate, "Arcane focus star", fleck, echo ? .9f : 1.3f, StormHot, w + .06f, 125);
            focusStar.spin = 200 * f; focusStar.startScale = .3f; focusStar.endScale = 1; focusStar.hold = .8f; focusStar.fadePower = 2;
            Emitter(focus, Emit.Sparkles, echo ? 10 : 22, .6f, light);

            if (!echo)
            {
                // A mandala of glowing lines behind the caster, an inner seal turning the other way.
                Vector2 halo = c.pos + new Vector2(-f * .15f, .2f);
                StormRune(halo, 4 * gs, Alpha(tone, .7f), w + .3f, 60 * f, false, 29, 0, .6f, 1, .4f, .45f, .4f);
                if (g >= 3)
                {
                    // Awakened: an outer ring of the mandala turning the other way, in its own glow.
                    var outerGlow = Spawn("Arcane outer seal glow", glow, halo, 6.4f, Alpha(StormPink, .22f), w + .3f, true, 27);
                    var outer = Spawn("Arcane outer seal", runeRing, halo, 5.7f, Alpha(StormPink, .65f), w + .3f, true, 28);
                    foreach (var s in new[] { outerGlow, outer }) { s.spin = -35 * f; s.fadeIn = .1f; s.startScale = .5f; s.endScale = 1; s.ease = 4; s.hold = .6f; s.fadePower = 1.6f; }
                }
                var inner = StormRune(halo, 2.2f * gs, Alpha(StormPink, .85f), w + .25f, -110 * f, false, 29, 0, .5f, 1, .6f, .4f);
                inner.fadeIn = .1f; inner.fadePower = 1.6f;
                Vector2 floor = StormFeet(c) + Vector2.up * .03f;
                StormRune(floor, 3.4f, Alpha(tone, .75f), w + .35f, 0, true, 27, 0, .6f, 1, .4f, .55f, .45f);

                // A launch gate in front of the hand, seen edge-on toward the target.
                var doorGlow = Spawn("Arcane gate glow", StormSealGlow, gate, 3, Alpha(StormPink, .45f), w + .15f, true, 122);
                var door = Spawn("Arcane gate", StormSeal, gate, 3, Alpha(Color.Lerp(tone, StormPink, .3f), .75f), w + .15f, true, 123);
                foreach (var d in new[] { doorGlow, door })
                {
                    d.scale.x *= .42f; d.root.transform.localScale = d.scale;
                    d.startScale = .3f; d.endScale = 1; d.ease = 4; d.fadeIn = .06f; d.hold = .7f; d.fadePower = 2;
                }

                // One plasma orb per missile spirals in to the gate (smaller the more there are, so six do not bury the caster).
                float bead = orbs >= 6 ? .72f : orbs == 4 ? .86f : 1;
                for (int i = 0; i < orbs; i++) StormOrbiter(gate, i * 360f / orbs + Range(-10, 10), 1.8f * gs, .12f, w, f * 1.1f, tone, bead);
                Converge(gate, 10, 2.2f, light, w, .5f);
                GroundGlow(floor, 3.2f, Alpha(light, .3f), w + .3f);
                Underlay(gate, 2.6f, .28f, w + .1f).fadeIn = w * .4f;
            }

            // Release: the gate flares in a pink hit spark and its bloom just ahead of the hand (clear of the caster), a shock
            // front races out edge-on and sparks fly forward.
            Vector2 muzzle = gate + new Vector2(f * .35f, 0);
            StormSpark(muzzle, echo ? 1.4f : 1.9f, StormPink, .14f, release, .35f);
            StormEdgeWave(muzzle, echo ? 2.4f : 3.4f, Alpha(StormPink, .85f), .26f, .42f, release);
            int sparks = echo ? 4 : 8;
            for (int i = 0; i < sparks; i++)
            {
                var s = Spark(gate, Direction(Range(-50, 50)) * Range(4, 9), i % 2 == 0 ? Color.white : tone, Range(.3f, .48f), Range(.2f, .35f), 3, 3);
                s.velocity = new Vector2(s.velocity.x * f, s.velocity.y); s.age = -release;
            }
        }

        private void ImpactArcaneVolley(Vector2 pos, float radius, Color tint, int facing)
        {
            StormArcaneBurst(pos, Mathf.Clamp(radius / 1.2f, .6f, 2.2f), tint, facing != 0 ? Vector2.right * facing : Vector2.zero);
        }

        /// <summary>The missile's painted comet trail: a smooth ribbon of light tapering away behind it over a soft glow, with
        /// a crescent wake and a sparkle shed now and then.</summary>
        private Action<Clip, Vector2> StormComet(Transform root, Color color)
        {
            Vector2 last = root.position; int steps = 0;
            return (owner, p) =>
            {
                Vector2 now = owner.root.transform.position, step = now - last;
                if (step.sqrMagnitude < .0016f) return;
                Vector2 heading = step.normalized;
                var ribbon = Line(last, now, .32f, Alpha(color, .92f), .28f, 121, 1.3f); ribbon.axisEnd = new Vector2(1, .12f);
                if (++steps % 2 == 0)
                {
                    var haze = Spawn("Arcane trail glow", glow, now - heading * .15f, .95f, Alpha(Color.Lerp(color, StormPink, .4f), .32f), .32f, true, 119);
                    haze.endScale = .35f; haze.ease = 1; haze.fadePower = 1.2f;
                }
                if (steps % 5 == 0 && owner.age > .08f)
                {
                    var wake = Spawn("Arcane wake", crescent, now - heading * .45f, .85f, Alpha(StormPink, .75f), .16f, true, 120);
                    wake.angle = Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg; wake.scale.y *= .9f;
                    wake.startScale = .7f; wake.endScale = 1.15f; wake.ease = 5; wake.hold = .15f; wake.fadePower = 1.3f;
                }
                if (steps % 3 == 0)
                {
                    var mote = Spawn("Arcane trail sparkle", fleck, now + Inside(.2f), Range(.16f, .26f), Alpha(Color.Lerp(StormPink, Color.white, .4f), .95f), Range(.25f, .4f), true, 122);
                    mote.velocity = -heading * Range(.3f, .9f) + Inside(.6f); mote.drag = 2; mote.flicker = .45f; mote.endScale = .2f; mote.ease = 1;
                }
                last = now;
            };
        }

        /// <summary>A single ribbon along the path of a basic bolt.</summary>
        private Action<Clip, Vector2> StormRibbon(Transform root, float thickness, Color color, float life)
        {
            Vector2 last = root.position;
            return (owner, p) =>
            {
                Vector2 now = owner.root.transform.position;
                if ((now - last).sqrMagnitude < .0025f) return;
                var band = Line(last, now, thickness, color, life, 120, 1.3f); band.axisEnd = new Vector2(1, .15f);
                last = now;
            };
        }

        private void ProjectileArcaneVolley(Transform root, Color tint, bool signature)
        {
            if (!signature)
            {
                // Companions' basic bolt: a small plasma orb in its glow, a white heart, a turning sparkle and a thin ribbon.
                Color tone = Color.Lerp(tint, ArcaneLight, .3f);
                var halo = Attach(root, "Bolt halo", glow, 1.1f, Alpha(tone, .5f), 119); halo.pulse = .1f;
                var bead = PlasmaOrb(root, "Bolt orb", .85f, Alpha(tone, 1), 121);
                var pip = Attach(root, "Bolt heart", core, .24f, Color.white, 122);
                var star = Attach(root, "Bolt star", fleck, .7f, Color.Lerp(tone, Color.white, .6f), 123); star.spin = 360;
                Emitter(halo, 16, 0, StormRibbon(root, .2f, Alpha(tone, .8f), .14f));
                Bundle(root, star, bead, pip, halo);
                return;
            }
            Color light = Color.Lerp(tint, StormViolet, .5f), toned = StormArcaneTone(tint);
            // Higher levels fire more missiles (3, 4, 6): each head is drawn a little smaller so the volley reads as a swarm of
            // separate missiles, not one ball; from level two a second pair of motes circles the other way.
            int g = Grade; float gs = g >= 3 ? .7f : g == 2 ? .85f : 1;
            int slot = StormVolleyCount++;
            var aura = Attach(root, "Arcane missile aura", glow, 2.2f * gs, Alpha(light, .34f), 119); aura.pulse = .12f;
            // Comet tails: a soft glow under a painted spindle of light.
            var tailGlow = Attach(root, "Arcane missile tail glow", glow, 4.6f * gs, Alpha(Color.Lerp(toned, StormPink, .4f), .4f), 119, true, new Vector2(-1.7f * gs, 0)); tailGlow.scale.y *= .32f;
            var tail = Attach(root, "Arcane missile tail", streak, 4.2f * gs, Alpha(Color.Lerp(toned, StormPink, .35f), .95f), 121, true, new Vector2(-1.7f * gs, 0)); tail.scale.y *= .3f;
            var head = PlasmaOrb(root, "Arcane missile head", 1.6f * gs, Alpha(StormArcanePlasma, .9f), 122); head.fps = 16 + slot % 3 * 3; head.pulse = .05f;
            var twin = Attach(root, "Arcane twin motes", StormTwin, 1.7f * gs, Alpha(StormPink, .75f), 123); twin.spin = 900;
            var star2 = Attach(root, "Arcane missile star", fleck, .7f * gs, Alpha(Color.Lerp(StormPink, Color.white, .35f), .55f), 124); star2.spin = -240; star2.pulse = .14f;
            var heart = Attach(root, "Arcane missile core", core, .32f * gs, new Color(1, .94f, 1, .9f), 125);
            Emitter(aura, 30 / (1 + (g - 1) * .5f), 0, StormComet(root, toned));
            var partList = new List<Clip> { head, aura, tailGlow, tail, twin, star2, heart };
            if (g >= 2)
            {
                var twin2 = Attach(root, "Arcane counter motes", StormTwin, 1.35f * gs, Alpha(Color.Lerp(toned, Color.white, .3f), .8f), 123); twin2.spin = -700; twin2.angle = 90;
                partList.Add(twin2);
            }
            Bundle(root, heart, partList.FindAll(part => part != heart).ToArray());

            // Missiles of one volley share a path, so each weaves on its own phase: they braid apart and
            // converge on the target like arcane missiles. A driver emitter (no particles) moves the layers
            // and grows the comet tails out of the muzzle, so a fresh missile never trails back through its caster.
            // The head also swells out of the gate, so a volley leaving together does not stack into one white blob.
            var parts = partList.ToArray();
            var baseX = new float[parts.Length]; var baseScale = new Vector3[parts.Length]; var baseAlpha = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++) { baseX[i] = parts[i].offset.x; baseScale[i] = parts[i].scale; baseAlpha[i] = parts[i].color.a; }
            // A volley leaves the gate together: the light starts dimmed, by more the more missiles the level fires (3, 4, 6),
            // and comes up as the missiles fan apart, so the gate never sums into one white blob.
            float muzzleCap = g >= 3 ? .4f : g == 2 ? .55f : .7f;
            Action<float, float> shape = (grow, bright) =>
            {
                float size = .2f + .8f * grow, lit = Mathf.Lerp(muzzleCap, 1, bright);
                for (int i = 0; i < parts.Length; i++)
                {
                    bool wake = baseX[i] < 0;
                    parts[i].scale = wake ? new Vector3(baseScale[i].x * Mathf.Max(.05f, grow), baseScale[i].y, 1) : baseScale[i] * size;
                    if (wake) parts[i].offset.x = baseX[i] * grow;
                    parts[i].color.a = baseAlpha[i] * lit;
                }
            };
            shape(0, 0);
            for (int i = 0; i < parts.Length; i++) { parts[i].root.transform.localScale = parts[i].scale; parts[i].root.transform.localPosition = parts[i].offset; }
            // Six phases round the circle with the sway sign alternating, so missiles of one volley split into a fan at the gate.
            float phase = (slot % 6) * Mathf.PI / 3 + .4f, sway = (slot % 2 == 0 ? 1 : -1) * (g >= 3 ? 1.15f : g == 2 ? 1.05f : .95f);
            Emitter(heart, 100, 0, (owner, p) =>
            {
                float t = owner.age, y = Mathf.Sin(t * 20 + phase) * sway * Mathf.Clamp01(t * 9) * Mathf.Clamp01(1 - t / .8f);
                shape(1 - Sq(1 - Mathf.Clamp01(t / .16f)), Mathf.Clamp01((t - .05f) / .25f));
                // Every layer weaves, the level crown added after this one included.
                owner.offset.y = y;
                if (owner.layers != null) for (int i = 0; i < owner.layers.Count; i++) owner.layers[i].offset.y = y;
            });

            // One muzzle flourish per volley frame: missiles leaving together share it instead of stacking rings into a bar.
            Vector2 at = root.position;
            if (Time.frameCount == StormMuzzleFrame) return;
            StormMuzzleFrame = Time.frameCount;
            var ringOut = Wave(at, 1.6f, Alpha(toned, .75f), .18f, 4); ringOut.scale.x *= .45f; ringOut.angle = root.eulerAngles.z;
            var pop = SparkFlip(at, 1.4f, Alpha(StormPink, 1), .12f, 130); pop.startScale = .7f;
            var glitter = Spawn("Arcane launch sparkle", fleck, at, 1.6f, StormHot, .14f, true, 131); glitter.startScale = .3f; glitter.endScale = 1; glitter.ease = 5; glitter.spin = 120;
        }

        private void HitArcaneVolley(Vector2 pos, Color tint, bool signature, Vector2 direction)
        {
            if (signature)
            {
                // Volleys often land several missiles on the same enemy in the same instant: the first one bursts,
                // the rest add quick staggered pops instead of stacking into a white-out.
                if (Time.frameCount - StormBurstFrame <= 3 && (pos - StormBurstAt).sqrMagnitude < 2.2f)
                {
                    StormArcanePop(pos, tint, direction, ++StormBurstStack);
                    // A third missile on the same target closes the volley with a resonance column.
                    if (StormBurstStack == 2) StormArcaneResonance(StormBurstAt, tint);
                }
                else
                {
                    // The first missile on a target bursts; the burst grows a step with the level and carries the level accent.
                    StormBurstFrame = Time.frameCount; StormBurstAt = pos; StormBurstStack = 0;
                    StormArcaneBurst(pos, 1.55f * (1 + (Grade - 1) * .1f), tint, direction);
                    // One level accent per volley (about half a second), not one per target; a missile has no area, so its
                    // accent is the small halo, not a floor ring.
                    if (Grade >= 2 && Time.frameCount - StormGradeFrame > 30) { StormGradeFrame = Time.frameCount; GradeImpact(SkillEffect.ArcaneVolley, pos, .6f, tint, 0); }
                }
                return;
            }
            // Basic and hostile bolts: a crisp pop, about ten clips, gone in a fifth of a second.
            Color light = Color.Lerp(tint, Color.white, .3f), tone = new Color(tint.r, tint.g, tint.b, 1);
            StormSpark(pos, 1.15f, tone, .1f, 0, .45f);
            Wave(pos, 1.15f, Alpha(light, .85f), .18f, 4);
            float back = Mathf.Atan2(-direction.y, -direction.x) * Mathf.Rad2Deg;
            for (int i = 0; i < 5; i++) Spark(pos, Direction(back + Range(-70, 70)) * Range(3, 6), i % 2 == 0 ? Color.white : light, Range(.24f, .36f), Range(.14f, .24f), 4, 3);
        }

        /// <summary>The arcane burst: an arcane blast billowing from a white-hot heart, a pink hit spark, a collapsing ring, then
        /// a spinning seal of glowing lines, shock fronts, painted prisms, needles, a spiral of flecks and lavender smoke.</summary>
        private void StormArcaneBurst(Vector2 pos, float s, Color tint, Vector2 direction)
        {
            Color light = Color.Lerp(tint, StormViolet, .55f), tone = StormArcaneTone(tint);
            // More missiles land on each foe at higher levels: the first burst holds its white back (and its blast stays one
            // size) so the following missiles' pops on its rim read as separate hits.
            float held = Grade >= 3 ? .55f : Grade == 2 ? .8f : 1;
            Underlay(pos, 3 * s, .34f, .36f);
            var bloom = Spawn("Arcane burst bloom", glow, pos, 2.8f * s, Alpha(light, .45f * held), .26f, true, 126);
            bloom.startScale = .5f; bloom.endScale = 1.15f; bloom.ease = 2; bloom.fadePower = 1.6f;
            StormBurst(pos, 1.7f * Mathf.Min(s, 1.55f) * Mathf.Lerp(1, held, .5f), Color.Lerp(tone, StormArcaneDeep, .45f), .42f, 128, 0, .32f);
            Wave(pos, 3.2f * s, Alpha(StormArcaneDeep, .9f), .26f, 4);
            StormSpark(pos, 1.5f * Mathf.Min(s, 1.55f), Color.Lerp(StormPink, StormArcaneDeep, .3f), .2f, 0, 0);
            var implode = Spawn("Arcane implosion", ring, pos, 3 * s, Alpha(tone, .95f), .08f, true, 131); implode.startScale = 1; implode.endScale = .12f; implode.ease = 1; implode.fadePower = .6f;

            const float pop = .06f;
            StormRune(pos, 3.2f * s, Alpha(tone, .95f), .5f, 240, false, 129, pop, .3f, 1.1f, .35f, .55f, .55f);
            var wave = Wave(pos, 4 * s, Alpha(tone, .9f), .34f, 4); wave.age = -pop;
            var wave2 = Wave(pos, 2.6f * s, new Color(1, 1, 1, .75f), .2f, 3); wave2.age = -pop;
            // A second, smaller pink spark flares a beat later off centre: the burst's afterbeat.
            var after = SparkFlip(pos + Inside(.25f * s), 1.5f * s, Alpha(StormPink, .95f), .16f, 132); after.age = -pop * 1.5f;

            int shards = GradeSteady(12);
            for (int i = 0; i < shards; i++)
            {
                Vector2 dir = Direction(i * 360f / shards + Range(-12, 12));
                var piece = Spawn("Arcane shard", StormShard, pos + dir * .2f * s, Range(.34f, .58f) * s, i % 3 == 0 ? Color.white : i % 3 == 1 ? tone : StormPink, Range(.35f, .55f), true, 131);
                piece.age = -pop; piece.velocity = (dir * Range(4.5f, 8.5f) + direction * 2) * s; piece.drag = 3.2f;
                piece.alignVelocity = true; piece.angleOffset = -90; piece.endScale = .4f; piece.ease = 1; piece.hold = .3f; piece.fadePower = 1.4f;
            }
            int sparks = GradeSteady(10);
            for (int i = 0; i < sparks; i++)
            {
                var spk = Spark(pos, (Direction(Range(0, 360)) * Range(4, 9) + direction * 3) * s, i % 3 == 0 ? Color.white : tone, Range(.3f, .48f), Range(.25f, .42f), 4, 2.5f);
                spk.age = -pop;
            }
            int motes = GradeSteady(7);
            for (int i = 0; i < motes; i++)
            {
                var mote = Spawn("Arcane spiral fleck", fleck, pos, Range(.26f, .4f) * s, i % 2 == 0 ? Alpha(StormPink, .95f) : tone, Range(.45f, .6f), true, 131);
                mote.age = -pop; mote.orbitCenter = pos; mote.orbitAngle = i * 360f / motes; mote.orbit = 520;
                mote.orbitRadius = .2f * s; mote.orbitRadiusEnd = Range(1.5f, 2) * s; mote.orbitPower = .6f; mote.endScale = .25f; mote.ease = 1; mote.fadePower = 1.3f;
            }
            StormRune(pos, 2.4f * s, Alpha(tone, .6f), 1, -120, false, 128, .1f, .6f, 1, .4f, .4f);
            for (int i = 0; i < 2; i++)
            {
                var puff = Puff(pos + Inside(.3f * s), Range(.7f, .95f) * s, StormArcaneSmoke, Direction(Range(30, 150)) * Range(.8f, 1.6f) * s, Range(.6f, .8f), 125);
                puff.age = -.08f; puff.endScale = 1.8f; puff.fadeIn = .06f;
            }
            int rising = GradeSteady(4);
            for (int i = 0; i < rising; i++)
            {
                var mote = Spawn("Arcane ember", fleck, pos + Inside(.8f * s), Range(.2f, .32f) * s, i % 2 == 0 ? Alpha(StormPink, .95f) : tone, Range(.7f, 1), true, 131);
                mote.age = -Range(.1f, .3f); mote.velocity = new Vector2(Range(-.5f, .5f), Range(.9f, 1.8f)); mote.drag = .7f; mote.flicker = .35f; mote.endScale = .3f; mote.ease = 1; mote.angle = Range(-20, 20);
            }
        }

        /// <summary>A follow-up missile landing on a burst that just went off: a quick offset pop, staggered by its place in the volley.</summary>
        private void StormArcanePop(Vector2 pos, Color tint, Vector2 direction, int stack)
        {
            Color tone = StormArcaneTone(tint);
            float delay = Mathf.Min(4, stack) * .07f;
            // Each follower pops on the first burst's rim, a sixth of a turn round from the last, clear of its white core.
            Vector2 at = pos + Direction(stack * 60 + 30) * 1.15f;
            // A small violet seal turns where it landed, so the extra missile leaves a mark of its own.
            StormRune(at, 1.45f, Alpha(tone, .9f), .4f, 260, false, 129, delay, .3f, 1, .3f, .45f, .5f);
            StormSpark(at, 2.1f, stack % 2 == 0 ? tone : StormPink, .16f, delay, .4f);
            var wave = Wave(at, 2.6f, Alpha(tone, .85f), .22f, 4); wave.age = -delay;
            int shards = GradeSteady(5);
            for (int i = 0; i < shards; i++)
            {
                Vector2 dir = Direction(Range(0, 360));
                var piece = Spawn("Arcane shard", StormShard, at, Range(.26f, .4f), i % 2 == 0 ? Color.white : tone, Range(.3f, .45f), true, 131);
                piece.age = -delay; piece.velocity = dir * Range(4, 7.5f) + direction * 2; piece.drag = 3.2f;
                piece.alignVelocity = true; piece.angleOffset = -90; piece.endScale = .4f; piece.ease = 1; piece.fadePower = 1.4f;
            }
            int sparks = GradeSteady(5);
            for (int i = 0; i < sparks; i++)
            {
                var spk = Spark(at, Direction(Range(0, 360)) * Range(4, 8) + direction * 3, i % 2 == 0 ? Color.white : tone, Range(.28f, .42f), Range(.2f, .35f), 4, 2.5f);
                spk.age = -delay;
            }
        }

        /// <summary>The volley's closing chord on one target: a column of arcane light in its glow out of a sealing ring on the
        /// floor, with a blast at its foot.</summary>
        private void StormArcaneResonance(Vector2 pos, Color tint)
        {
            Color light = Color.Lerp(tint, StormViolet, .55f), tone = StormArcaneTone(tint);
            const float delay = .12f;
            // Missiles aim at the body, about a unit above the feet.
            Vector2 ground = pos + Vector2.down;
            var haze = Spawn("Arcane column glow", glow, ground + Vector2.up * 3.2f, 3.4f, Alpha(light, .42f), .75f, true, 117);
            haze.scale.y *= 2.4f; haze.root.transform.localScale = haze.scale; haze.startScale = .5f; haze.endScale = 1; haze.ease = 4; haze.hold = .3f; haze.fadePower = 1.3f;
            var column = Pillar(ground, 2, 8, Alpha(tone, .95f), .6f, 128); column.hold = .35f;
            var beam = Pillar(ground, .6f, 8.5f, new Color(1, .95f, 1, .95f), .45f, 129); beam.hold = .3f;
            StormRune(ground + Vector2.up * .03f, 4.4f, Alpha(tone, .95f), .9f, 0, true, 27, delay, .6f, 1, .4f, .6f, .5f);
            var floor = GroundGlow(ground, 4, Alpha(light, .35f), .8f);
            var wave = StormFloorWave(ground, 6.5f, Alpha(tone, .9f), .45f);
            StormSpark(pos, 3.4f, StormPink, .22f, delay, .35f);
            StormBurst(ground + Vector2.up * .5f, 2.2f, Color.Lerp(tone, StormArcaneDeep, .3f), .45f, 127, delay, .3f);
            var glitter = Spawn("Arcane column sparkle", fleck, pos, 2.6f, StormHot, .26f, true, 133); glitter.startScale = .3f; glitter.endScale = 1; glitter.ease = 5; glitter.fadePower = 1.4f;
            foreach (var clip in new[] { haze, column, beam, floor, wave, glitter }) clip.age = -delay;
            int motes = Budget(8);
            for (int i = 0; i < motes; i++)
            {
                var mote = Spawn("Arcane column mote", spark, ground + new Vector2(Range(-.55f, .55f), Range(0, 1.5f)), Range(.36f, .56f), Alpha(i % 2 == 0 ? StormPink : tone, .95f), Range(.35f, .55f), true, 131);
                mote.age = -delay - Range(0, .1f); mote.velocity = new Vector2(Range(-.4f, .4f), Range(9, 15)); mote.drag = 1.5f; mote.alignVelocity = true; mote.endScale = .3f; mote.ease = 1;
            }
        }
    }
}
