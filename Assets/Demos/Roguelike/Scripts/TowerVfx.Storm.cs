using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Storm and arcane family: ChainLightning and ArcaneVolley.
    // Lightning is drawn from pre-baked forked bolts: one stretched clip shows a whole jagged, branching bolt
    // (white core, electric-blue glow, indigo haze) and cycles through the baked variants so it crackles, instead
    // of building every segment from separate lines. Bolts and arcs are alpha-blended "ink" so they keep a coloured
    // silhouette on pale marble skies, with additive bloom underneath for light on dark floors.
    public sealed partial class TowerVfx
    {
        private sealed class StormPath { public Vector2[] points; public int[] column; public float core, strength; public Vector2 min, max; }

        private const int StormBoltWidth = 384, StormBoltHeight = 144, StormBoltVariants = 5;
        private static Sprite[] StormBolts, StormArcRings;
        private static Sprite[][] StormBoltCycles;
        private static Sprite StormTwin, StormShard, StormSeal;
        private static int StormFlashFrame = -1, StormBurstFrame = -100, StormBurstStack, StormMuzzleFrame = -1, StormVolleyCount, StormGradeFrame = -100;
        private static Vector2 StormBurstAt;
        private static readonly Color StormElectric = new Color(.38f, .6f, 1), StormIndigo = new Color(.22f, .18f, .82f), StormHot = new Color(.94f, .97f, 1),
            StormCloud = new Color(.13f, .13f, .25f, .62f), StormInk = new Color(.07f, .08f, .3f, .62f), StormViolet = new Color(.76f, .5f, 1),
            StormArcaneInk = new Color(.3f, .07f, .56f, .82f), StormPink = new Color(1, .62f, .96f);

        // ---------------------------------------------------------------- textures

        private static void BuildStormTextures()
        {
            StormBolts = new Sprite[StormBoltVariants];
            for (int f = 0; f < StormBoltVariants; f++)
            {
                StormPath[] paths = StormForkedPaths(new System.Random(4100 + f * 37));
                StormBolts[f] = Build("Storm bolt " + f, StormBoltWidth, StormBoltHeight, (u, v) => StormBoltShade(paths, u, v));
            }
            // Each bolt starts on its own variant and crackles through all of them.
            StormBoltCycles = new Sprite[StormBoltVariants][];
            for (int f = 0; f < StormBoltVariants; f++)
            {
                StormBoltCycles[f] = new Sprite[StormBoltVariants];
                for (int k = 0; k < StormBoltVariants; k++) StormBoltCycles[f][k] = StormBolts[(f + k) % StormBoltVariants];
            }

            // A jagged ring of lightning with gaps: discharge shells and the crackle around a charging hand.
            StormArcRings = new Sprite[3];
            for (int f = 0; f < StormArcRings.Length; f++)
            {
                var rng = new System.Random(7300 + f * 11);
                float[] coarse = StormLoop(rng, 18, .07f), fine = StormLoop(rng, 60, .025f), other = StormLoop(rng, 23, .085f), gaps = StormLoop(rng, 6, 1);
                StormArcRings[f] = Build("Storm arc ring " + f, 256, 256, (u, v) =>
                {
                    float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                    float t = Mathf.Atan2(v, u) / (2 * Mathf.PI), core = 0, glow = 0, haze = 0;
                    for (int strand = 0; strand < 2; strand++)
                    {
                        float[] big = strand == 0 ? coarse : other; float offset = strand * .37f, baseRadius = strand == 0 ? .6f : .57f;
                        float radius = baseRadius + StormLoopAt(big, t) + StormLoopAt(fine, t + offset);
                        float slope = (StormLoopAt(big, t + .003f) + StormLoopAt(fine, t + offset + .003f) - StormLoopAt(big, t - .003f) - StormLoopAt(fine, t + offset - .003f)) / (.006f * 2 * Mathf.PI);
                        float d = Mathf.Abs(r - radius) * 128 / Mathf.Sqrt(1 + Sq(slope / Mathf.Max(.2f, r)));
                        float s = (strand == 0 ? 1 : .6f) * Smooth(-.35f, .15f, StormLoopAt(gaps, t + strand * .5f));
                        float width = strand == 0 ? 2.4f : 1.6f;
                        core = Mathf.Max(core, Gauss(d, width) * s); glow = Mathf.Max(glow, Gauss(d, width * 3.2f + 2) * s); haze = Mathf.Max(haze, Gauss(d, 14) * s);
                    }
                    return StormTone(core, glow, haze, Window(r));
                });
            }

            // Two motes chasing each other round a circle with comet tails; spinning it orbits a missile.
            StormTwin = Build("Storm twin motes", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), degrees = Mathf.Atan2(v, u) * Mathf.Rad2Deg, a = Gauss(r - .6f, .018f) * .14f;
                for (int m = 0; m < 2; m++)
                {
                    float centre = m * 180, behind = Mathf.Repeat(centre - degrees, 360);
                    float mx = Mathf.Cos(centre * Mathf.Deg2Rad) * .6f, my = Mathf.Sin(centre * Mathf.Deg2Rad) * .6f, md = Mathf.Sqrt(Sq(u - mx) + Sq(v - my));
                    float mote = Gauss(md, .1f) + Gauss(md, .22f) * .3f;
                    float k = behind / 150f, tail = k < 1 ? Gauss(r - .6f, .03f + k * .035f) * Mathf.Pow(1 - k, 1.7f) : 0;
                    a = Mathf.Max(a, Mathf.Max(mote, tail * .8f));
                }
                return White(Mathf.Clamp01(a) * Window(r));
            });

            // A faceted light shard, long axis vertical.
            StormShard = Build("Storm shard", 64, 128, (u, v) =>
            {
                float d = Mathf.Abs(u) / .7f + Mathf.Abs(v) / .96f, inside = 1 - Smooth(.9f, 1, d);
                float l = Mathf.Clamp01((u < 0 ? .78f : 1) + Gauss(u, .08f) * .25f - d * .15f);
                return new Color(l, l, l, inside * (.7f + .3f * (1 - d)));
            });

            // A bold seal (thick rings, a glyph band and an eight-point star) that still reads at game-camera scale.
            StormSeal = Build("Storm seal", 384, 384, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float deg = Mathf.Repeat(Mathf.Atan2(v, u) * Mathf.Rad2Deg, 360);
                float a = Gauss(r - .92f, .024f) + Gauss(r - .85f, .011f) * .8f + Gauss(r - .64f, .02f) + Gauss(r - .3f, .016f) * .85f;
                if (r > .66f && r < .83f)
                {
                    float cell = deg / 18f; int slot = (int)cell; float lx = cell - slot, ly = (r - .67f) / .15f, g = 0, w = .13f;
                    uint h = Hash(slot * 13 + 5);
                    if ((h & 1u) != 0) g = Mathf.Max(g, Gauss(lx - .5f, w) * Band(.1f, .9f, ly));
                    if ((h & 2u) != 0) g = Mathf.Max(g, Gauss(ly - .5f, w) * Band(.15f, .85f, lx));
                    if ((h & 4u) != 0) g = Mathf.Max(g, Gauss(ly - .85f, w) * Band(.15f, .85f, lx));
                    if ((h & 8u) != 0) g = Mathf.Max(g, Gauss(Mathf.Sqrt(Sq(lx - .5f) + Sq(ly - .45f)) - .22f, w * .8f));
                    if ((h & 16u) != 0) g = Mathf.Max(g, Gauss(lx - .2f - ly * .6f, w) * Band(.1f, .9f, ly));
                    if (g == 0) g = Gauss(ly - .5f, w) * Band(.3f, .7f, lx);
                    a += g * .95f * Band(.06f, .94f, lx);
                }
                float seg = 1e9f;
                for (int i = 0; i < 8; i++)
                {
                    float a0 = (i * 45 + 22.5f) * Mathf.Deg2Rad, a1 = ((i + 3) * 45 + 22.5f) * Mathf.Deg2Rad;
                    seg = Mathf.Min(seg, SegmentDistance(u, v, Mathf.Cos(a0) * .64f, Mathf.Sin(a0) * .64f, Mathf.Cos(a1) * .64f, Mathf.Sin(a1) * .64f));
                }
                a += Gauss(seg, .017f) * .9f;
                float tick = Mathf.Abs(Mathf.Repeat(deg + 22.5f, 45) - 22.5f) * Mathf.Deg2Rad * r;
                a += Gauss(Mathf.Sqrt(Sq(tick) + Sq(r - .92f)), .038f) * .9f + Gauss(Mathf.Sqrt(Sq(tick) + Sq(r - .64f)), .03f) * .8f;
                a += (1 - Smooth(.05f, .9f, r)) * .1f;
                return White(Mathf.Clamp01(a) * Window(r));
            });
        }

        private static Color StormTone(float core, float glow, float haze, float edge)
        {
            Color c = Color.Lerp(StormIndigo, StormElectric, Mathf.Clamp01(glow * 1.6f));
            c = Color.Lerp(c, Color.white, Mathf.Clamp01(core * 1.3f));
            c.a = Mathf.Clamp01(core * 1.15f + glow * .6f + haze * .4f) * edge;
            return c;
        }

        private static Color StormBoltShade(StormPath[] paths, float u, float v)
        {
            float x = u * StormBoltWidth * .5f, y = v * StormBoltHeight * .5f, core = 0, glow = 0, haze = 0;
            const float reach = 38;
            for (int k = 0; k < paths.Length; k++)
            {
                var path = paths[k];
                if (x < path.min.x - reach || x > path.max.x + reach || y < path.min.y - reach || y > path.max.y + reach) continue;
                int from = 0, to = path.points.Length - 1;
                if (path.column != null)
                {
                    int segment = path.column[Mathf.Clamp((int)(x + StormBoltWidth * .5f), 0, StormBoltWidth - 1)];
                    from = Mathf.Max(0, segment - 2); to = Mathf.Min(path.points.Length - 1, segment + 3);
                }
                float along, d = StormSegmentsDistance(path.points, from, to, x, y, out along);
                if (d > reach) continue;
                float s = path.strength * (path.column != null ? .84f + .16f * Mathf.Sin(along * 47 + k) : Mathf.Pow(Mathf.Max(0, 1 - along), .6f));
                core = Mathf.Max(core, Gauss(d, path.core) * s);
                glow = Mathf.Max(glow, Gauss(d, path.core * 2.6f + 3) * s);
                haze = Mathf.Max(haze, Gauss(d, 17) * s);
            }
            return StormTone(core, glow, haze, (1 - Smooth(.8f, .985f, Mathf.Abs(v))) * (1 - Smooth(.96f, 1, Mathf.Abs(u))));
        }

        // A trunk from end to end of the texture with two to four forks (some forking again), in pixel space.
        private static StormPath[] StormForkedPaths(System.Random rng)
        {
            float reach = StormBoltWidth * .5f * .955f;
            var paths = new List<StormPath>();
            var trunk = StormDisplace(rng, new Vector2(-reach, 0), new Vector2(reach, 0), 5, 22, .56f, true, 30);
            paths.Add(StormMakePath(trunk, 4.2f, 1, true));
            int limbs = 2 + rng.Next(3);
            for (int b = 0; b < limbs; b++)
            {
                Vector2 start = trunk[5 + rng.Next(trunk.Count - 14)];
                float angle = (rng.NextDouble() < .5 ? -1 : 1) * (22 + (float)rng.NextDouble() * 32), length = 45 + (float)rng.NextDouble() * 70;
                Vector2 end = start + Direction(angle) * length; end.x = Mathf.Min(end.x, reach - 6); end.y = Mathf.Clamp(end.y, -46, 46);
                var limb = StormDisplace(rng, start, end, 3, length * .14f, .55f, false, 48);
                paths.Add(StormMakePath(limb, 2.7f, .8f, false));
                if (rng.NextDouble() < .65)
                {
                    Vector2 fork = limb[2 + rng.Next(limb.Count - 4)];
                    float turn = angle + (rng.NextDouble() < .5 ? -1 : 1) * (18 + (float)rng.NextDouble() * 26), twig = 20 + (float)rng.NextDouble() * 28;
                    Vector2 tip = fork + Direction(turn) * twig; tip.x = Mathf.Min(tip.x, reach - 6); tip.y = Mathf.Clamp(tip.y, -50, 50);
                    paths.Add(StormMakePath(StormDisplace(rng, fork, tip, 2, twig * .15f, .55f, false, 52), 1.9f, .6f, false));
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

        private static StormPath StormMakePath(List<Vector2> points, float core, float strength, bool monotonic)
        {
            var path = new StormPath { points = points.ToArray(), core = core, strength = strength, min = points[0], max = points[0] };
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

        /// <summary>A deep, saturated version of a tint for alpha-blended contrast layers.</summary>
        private static Color StormDeepen(Color color, float alpha)
        {
            float h, s, v; Color.RGBToHSV(color, out h, out s, out v);
            Color deep = s < .08f ? new Color(.18f, .18f, .28f) : Color.HSVToRGB(h, Mathf.Clamp01(s * 1.2f + .25f), .42f);
            deep.a = alpha; return deep;
        }

        /// <summary>The caster's feet: the anchor is the actor root standing on the floor; without one, a body-height guess.</summary>
        private static Vector2 StormFeet(CastInfo c)
        {
            if (c.anchor) { Vector2 root = c.anchor.position; if (Mathf.Abs(root.y - (c.pos.y - 1)) < .6f) return root; }
            return c.pos - new Vector2(0, 1);
        }

        // ---------------------------------------------------------------- lightning builders

        /// <summary>One forked lightning bolt stretched between two points; it crackles through the baked variants.</summary>
        private Clip StormStroke(Vector2 a, Vector2 b, float thickness, Color color, float life, bool additive, int order, int variant, bool flip, bool crackle = true)
        {
            Vector2 delta = b - a; float length = Mathf.Max(.05f, delta.magnitude) * 1.045f;
            Sprite[] cycle = StormBoltCycles[Mathf.Abs(variant) % StormBoltCycles.Length];
            var clip = Spawn("Lightning bolt", cycle[0], (a + b) * .5f, length, color, life, additive, order);
            Vector2 size = cycle[0].bounds.size;
            clip.scale = new Vector3(length / size.x, thickness / size.y * (flip ? -1 : 1), 1);
            clip.angle = delta.sqrMagnitude > 1e-6f ? Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg : 90; clip.ease = 1;
            clip.root.transform.localScale = clip.scale; clip.root.transform.localRotation = Quaternion.Euler(0, 0, clip.angle);
            if (crackle) { clip.frames = cycle; clip.fps = 20; clip.loop = true; }
            return clip;
        }

        /// <summary>A soft straight band of light or shade between two points (Line with a choice of blending).</summary>
        private Clip StormBand(Vector2 a, Vector2 b, float thickness, Color color, float life, bool additive, int order)
        {
            Vector2 delta = b - a; float length = Mathf.Max(.05f, delta.magnitude) * 1.15f;
            var clip = Spawn("Storm band", streak, (a + b) * .5f, length, color, life, additive, order);
            clip.scale = new Vector3(length / streak.bounds.size.x, thickness / streak.bounds.size.y, 1);
            clip.angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg; clip.ease = 1;
            clip.root.transform.localScale = clip.scale; clip.root.transform.localRotation = Quaternion.Euler(0, 0, clip.angle);
            return clip;
        }

        /// <summary>A small crackling arc (single ink layer).</summary>
        private Clip StormArc(Vector2 a, Vector2 b, float thickness, float life, float delay, float alpha = 1)
        {
            var arc = StormStroke(a, b, thickness, new Color(1, 1, 1, alpha), life, false, 127, random.Next(StormBoltVariants), Chance(.5f));
            arc.age = -delay; arc.flicker = .3f; arc.hold = .2f; arc.fadePower = 1.4f;
            return arc;
        }

        /// <summary>A full bolt: dark contrast band, soft bloom, crackling bolt, a restrike and an ionised afterimage.</summary>
        private void StormMainBolt(Vector2 a, Vector2 b, float thickness, Color light, float life, float delay, bool afterimage, Color? boltTint = null)
        {
            // The baked bolt is white-cored blue; a tint multiplies it (gold turns the core gold and the glow a dark bronze).
            Color tint = boltTint ?? Color.white;
            int variant = random.Next(StormBoltVariants); bool flip = Chance(.5f);
            var band = StormBand(a, b, thickness * .7f, boltTint.HasValue ? new Color(.3f, .17f, .04f, .62f) : StormInk, life, false, 112); band.age = -delay; band.fadePower = 1.6f;
            var bloom = StormBand(a, b, thickness * .85f, Alpha(light, .6f), life, true, 126); bloom.age = -delay; bloom.flicker = .35f; bloom.fadePower = 1.6f;
            var bolt = StormStroke(a, b, thickness, tint, life, false, 128, variant, flip); bolt.age = -delay; bolt.flicker = .22f; bolt.hold = .35f; bolt.fadePower = 1.5f;
            var flare = StormStroke(a, b, thickness, Alpha(Color.Lerp(light, Color.white, .5f), .5f), life * .7f, true, 130, variant, flip); flare.age = -delay; flare.flicker = .4f; flare.fadePower = 1.8f;
            var again = StormStroke(a, b, thickness * .85f, Alpha(tint, .9f), life * .55f, false, 129, variant + 2, !flip); again.age = -(delay + life * .4f); again.flicker = .3f;
            if (afterimage)
            {
                // A short ionised afterimage: long enough to sell the flash on the eye, short enough not to smear.
                var ghost = StormStroke(a, b, thickness, new Color(.6f, .5f, 1, .4f), .26f, false, 127, variant, flip, false);
                ghost.age = -(delay + life * .55f); ghost.fadeIn = .03f; ghost.fadePower = 2;
            }
        }

        /// <summary>A thunderbolt from a thunderhead down onto the ground: forked bolt, light column, scorch and cracks.</summary>
        private void StormSkyStrike(Vector2 ground, Vector2 top, Color light, float delay, float scale, bool lean = false, Color? boltTint = null)
        {
            StormMainBolt(top, ground, 2.6f * scale, light, .3f, delay, true, boltTint);
            Vector2 split = Vector2.Lerp(top, ground, Range(.4f, .6f));
            var fork = StormStroke(split, ground + new Vector2((Chance(.5f) ? 1 : -1) * Range(.9f, 1.7f) * scale, 0), 1.6f * scale, Alpha(boltTint ?? Color.white, .85f), .2f, false, 127, random.Next(StormBoltVariants), Chance(.5f));
            fork.age = -(delay + .02f); fork.flicker = .4f; fork.fadePower = 1.5f;
            for (int i = 0; i < 3; i++)
            {
                var cloud = Puff(top + new Vector2((i - 1) * 1.2f * scale, .45f + Range(-.15f, .2f)), Range(2.4f, 3.2f) * scale, StormCloud, new Vector2((i - 1) * .9f, 0), .7f, 112);
                cloud.scale.y *= .75f; cloud.age = -Mathf.Max(0, delay - .04f); cloud.startScale = .75f; cloud.endScale = 1.25f; cloud.spin = Range(-15, 15); cloud.hold = .25f; cloud.fadePower = 1.5f;
            }
            var glare = Spawn("Thunderhead flash", glow, top, 2.8f * scale, Alpha(light, .8f), .22f, true, 125); glare.age = -delay; glare.flicker = .6f; glare.fadePower = 1.5f;
            var floor = Spawn("Strike floor flash", glow, ground + Vector2.up * .15f, 4.2f * scale, Alpha(StormHot, .85f), .15f, true, 126);
            floor.scale.y *= .42f; floor.age = -delay; floor.endScale = 1.3f; floor.ease = 1; floor.fadePower = 1.8f;
            var column = Pillar(ground, 1.3f * scale, (top.y - ground.y) * .85f, Alpha(light, .7f), .24f, 125); column.age = -delay;
            // The ground shock: a bright electric ring over a deep indigo one, so it reads on pale marble too.
            var shockInk = Spawn("Strike shock ink", ring, ground + Vector2.up * .05f, 5 * scale, new Color(.08f, .1f, .42f, .6f), .34f, false, 115);
            shockInk.scale.y *= .34f; shockInk.root.transform.localScale = shockInk.scale; shockInk.age = -delay;
            shockInk.startScale = .12f; shockInk.endScale = 1; shockInk.ease = 3; shockInk.fadePower = 1.3f;
            var shock = GroundWave(ground + Vector2.up * .05f, 4.6f * scale, Alpha(light, .9f), .3f); shock.age = -delay;
            var burn = Scorch(ground, 3.2f * scale, 1.6f); burn.age = -delay; burn.color = new Color(.05f, .04f, .08f, .75f);
            var crackDark = Decal(ground, crack, 3.6f * scale, new Color(.05f, .05f, .12f, .9f), 1.5f, false, 25); crackDark.angle = Range(-6, 6); crackDark.age = -delay;
            var crackHot = Decal(ground, crack, 3.6f * scale, Alpha(StormElectric, 1), 1.2f, true, 26); crackHot.angle = crackDark.angle; crackHot.fadePower = 1.6f; crackHot.age = -delay; crackHot.flicker = .25f;
            // Residue: static still crawling over the burn, smoke curling up and charged motes drifting off (skipped on lean
            // strikes: the links of a higher-level chain, which brings more of them).
            if (!lean)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2 start = ground + new Vector2(Range(-1.1f, 1.1f) * scale, .08f);
                    StormArc(start, start + new Vector2(Range(-.9f, .9f), Range(.05f, .5f)) * scale, .5f * scale, .09f, delay + Range(.2f, .9f), .9f);
                }
                for (int i = 0; i < 2; i++)
                {
                    var smoke = Puff(ground + new Vector2(Range(-.6f, .6f) * scale, .3f), Range(1, 1.4f) * scale, new Color(.14f, .14f, .2f, .5f), new Vector2(Range(-.3f, .3f), Range(.9f, 1.5f)), Range(1, 1.3f), 112);
                    smoke.age = -(delay + .12f); smoke.drag = 1.2f; smoke.fadeIn = .12f; smoke.endScale = 2.2f;
                }
                for (int i = 0; i < 4; i++)
                {
                    var mote = Spawn("Static mote", glow, ground + new Vector2(Range(-1, 1) * scale, Range(.1f, .5f)), Range(.14f, .24f), Alpha(i % 2 == 0 ? Color.white : StormElectric, .95f), Range(.8f, 1.2f), true, 131);
                    mote.age = -(delay + Range(.1f, .3f)); mote.velocity = new Vector2(Range(-.4f, .4f), Range(.7f, 1.4f)); mote.drag = .6f; mote.flicker = .6f; mote.fadeIn = .08f; mote.endScale = .3f; mote.ease = 1;
                }
            }
            int sparks = GradeSteady(lean ? 6 : 9);
            for (int i = 0; i < sparks; i++)
            {
                var s = Spark(ground + Vector2.up * .1f, new Vector2(Range(-6, 6), Range(3, 9)) * scale, i % 3 == 0 ? Color.white : light, Range(.22f, .4f), Range(.35f, .6f), 16, 1.2f);
                s.age = -delay;
            }
            for (int i = 0; i < 3; i++)
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
            Color light = Color.Lerp(c.light, StormLight, .6f);
            // Higher levels swell the orb and throw more arcs; level three wraps the caster in a crackling shell and the
            // thunderhead answers a third time.
            int g = Grade; float gs = 1 + (g - 1) * .12f;
            float orbSize = (echo ? .9f : 1.35f) * gs;

            // A crackling orb in the hand that grows to the release.
            var shell = Anchored(c.anchor, hand, "Storm orb shell", orb, orbSize, new Color(.12f, .14f, .42f, .7f), w + .06f, 121, false);
            shell.startScale = .3f; shell.endScale = 1; shell.ease = 1.6f; shell.fadeIn = .04f; shell.hold = .8f; shell.fadePower = 2;
            // Electric blue light over the indigo shell (not white) so the orb keeps its colour and rim on pale skies.
            var ball = Anchored(c.anchor, hand, "Storm orb", orb, orbSize, Alpha(Color.Lerp(light, StormElectric, .45f), .72f), w + .08f, 123);
            ball.startScale = .3f; ball.endScale = 1; ball.ease = 1.6f; ball.fadeIn = .03f; ball.hold = .8f; ball.fadePower = 2; ball.pulse = .08f; ball.flicker = .12f;
            var heart = Anchored(c.anchor, hand, "Storm orb core", core, echo ? .34f : .44f, StormHot, w + .06f, 124);
            heart.startScale = .4f; heart.endScale = 1; heart.hold = .8f; heart.fadePower = 2; heart.pulse = .1f;
            var crackle = Anchored(c.anchor, hand, "Storm orb arcs", StormArcRings[0], (echo ? 1.3f : 2.3f) * gs, Color.white, w + .06f, 122, false);
            crackle.frames = StormArcRings; crackle.fps = 22; crackle.loop = true; crackle.spin = 240 * f;
            crackle.startScale = .35f; crackle.endScale = 1; crackle.ease = 1.5f; crackle.fadeIn = .05f; crackle.hold = .8f; crackle.fadePower = 2;
            var bloom = Anchored(c.anchor, hand, "Storm orb bloom", glow, echo ? 1.7f : 3, Alpha(light, .45f), w + .1f, 120);
            bloom.fadeIn = w * .5f; bloom.hold = .75f; bloom.fadePower = 2; bloom.flicker = .3f;
            Emitter(ball, echo ? 10 : 16, .1f, (owner, p) =>
            {
                Vector2 at = owner.root.transform.position;
                StormArc(at, at + Direction(Range(0, 360)) * Range(.5f, 1.1f), .42f, Range(.06f, .1f), 0);
            });

            if (!echo)
            {
                Underlay(hand, 2.2f, .3f, w + .1f).fadeIn = w * .4f;
                // Storm rune under the feet: deep ink under bright glyphs.
                var runeInk = StormRune(feet + Vector2.up * .03f, 3.8f, new Color(.1f, .12f, .38f, .55f), w + .35f, 0, true, false, 26);
                var rune = StormRune(feet + Vector2.up * .03f, 3.8f, Alpha(light, .9f), w + .35f, 0, true, true, 27);
                runeInk.pulse = rune.pulse = .04f;
                GroundGlow(feet, 4, Alpha(light, .45f), w + .3f);

                // A thunderhead gathers overhead and answers twice: a feeler bolt, then the full bolt into the hand.
                Vector2 sky = c.pos + new Vector2(f * .5f, 3.4f);
                for (int i = 0; i < 5; i++)
                {
                    var cloud = Puff(sky + new Vector2(Range(-1.5f, 1.5f), Range(-.25f, .35f)), Range(1.8f, 2.6f), StormCloud, new Vector2(Range(-.3f, .3f), 0), w + .7f, 112);
                    cloud.fadeIn = w * .55f; cloud.startScale = .45f; cloud.endScale = 1.15f; cloud.hold = .45f; cloud.fadePower = 1.4f; cloud.spin = Range(-20, 20); cloud.drag = 1;
                }
                var thunder = Spawn("Cloud lightning", glow, sky, 3.6f, Alpha(light, .75f), w + .3f, true, 113);
                thunder.fadeIn = w * .5f; thunder.flicker = .6f; thunder.hold = .5f; thunder.fadePower = 1.5f;
                for (int i = 0; i < 3; i++)
                {
                    Vector2 start = sky + new Vector2(Range(-1.3f, 1.3f), Range(-.2f, .3f));
                    var sheet = StormArc(start, start + new Vector2(Range(.8f, 1.5f) * (Chance(.5f) ? 1 : -1), Range(-.3f, .3f)), .6f, .08f, Range(.1f, Mathf.Max(.12f, w - .05f)));
                    sheet.renderer.sortingOrder = 114;
                }
                StormMainBolt(sky + new Vector2(Range(-.5f, .5f), 0), hand, 1, light, .12f, w * .5f, false);
                StormMainBolt(sky, hand, 1.5f * gs, light, .22f, release, false);
                if (g >= 3)
                {
                    StormMainBolt(sky + new Vector2(Range(-1, 1), .2f), hand, 1.2f, light, .12f, w * .75f, false);
                    var cage = Anchored(c.anchor, c.pos + Vector2.up * .1f, "Storm body shell", StormArcRings[1], 3.6f, new Color(1, 1, 1, .8f), w + .08f, 121, false);
                    cage.frames = StormArcRings; cage.fps = 18; cage.loop = true; cage.spin = -120 * f;
                    cage.startScale = .5f; cage.endScale = 1; cage.ease = 2; cage.fadeIn = w * .4f; cage.hold = .8f; cage.fadePower = 2; cage.flicker = .25f;
                }

                // Arcs leaping from the orb down to the floor.
                int legs = 4 + 2 * (g - 1);
                for (int i = 0; i < legs; i++) StormArc(hand, new Vector2(c.pos.x + f * Range(-1.2f, 1.8f), feet.y + .05f), .75f, .1f, Range(.04f, Mathf.Max(.05f, w - .06f)));
                Converge(hand, 10, 2, light, w, .45f);
            }

            // Release: the blinding peak at the hand.
            var blast = Spawn("Storm release", glow, hand, echo ? 2.2f : 3.6f, Alpha(StormHot, .95f), .16f, true, 131);
            blast.age = -release; blast.startScale = .5f; blast.endScale = 1.3f; blast.ease = 1; blast.fadePower = 1.6f;
            var lines = ImpactLines(hand, echo ? 2.6f : 4, new Color(1, 1, 1, .85f), .18f); lines.age = -release;
            var star = Glint(hand, echo ? 2 : 3.2f, StormHot, .2f, Range(0, 45)); star.age = -release;
            var wave = Wave(hand, echo ? 2 : 3.2f, Alpha(light, .9f), .26f, 3); wave.age = -release;
            int sparks = echo ? 4 : 8;
            for (int i = 0; i < sparks; i++)
            {
                var s = Spark(hand, Direction(Range(0, 360)) * Range(4, 9), i % 2 == 0 ? Color.white : light, Range(.2f, .35f), Range(.2f, .35f), 6, 3);
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
            Color light = Color.Lerp(tint, StormLight, .55f);
            Vector2 delta = to - from; float length = delta.magnitude;
            int g = Grade; float gs = 1 + (g - 1) * .15f;
            bool fromSky = delta.y < -2.5f && Mathf.Abs(delta.x) < -delta.y * .4f;
            if (fromSky)
            {
                // A strike called down from the sky (passives, and the level-three chain's closing bolt): one huge bolt through
                // the target into the ground. At level three it is the only sky strike of the chain and falls as a twin bolt
                // lit gold-white (its column, bloom and ground shock), so the finale reads apart from the blue links.
                Color strike = g >= 3 ? Color.Lerp(light, GradeGold, .7f) : light;
                Color? gold = g >= 3 ? new Color(1, .86f, .5f) : (Color?)null;
                StormSkyStrike(new Vector2(to.x, to.y - 1), from, strike, 0, g >= 3 ? 1.4f : 1.1f, g >= 3, gold);
                if (g >= 3)
                {
                    StormMainBolt(from + new Vector2(Range(.6f, 1) * (Chance(.5f) ? 1 : -1), .3f), new Vector2(to.x, to.y - 1), 1.7f, strike, .26f, .04f, true, gold);
                    if (Time.frameCount != StormFlashFrame) { StormFlashFrame = Time.frameCount; ScreenFlash(to, new Color(.75f, .82f, 1, .18f), .12f); }
                }
                var hit = Spawn("Strike body flash", glow, to, 2.4f * gs, Alpha(g >= 3 ? GradeHot : StormHot, g >= 3 ? .55f : .9f), .14f, true, 131); hit.endScale = 1.4f; hit.ease = 1; hit.fadePower = 1.5f;
                return;
            }
            if (length < .3f) { Discharge(to, 1, light); return; }

            // Higher levels thicken every link and braid a second strand into it.
            float thickness = Mathf.Clamp(1.9f + length * .15f, 2, 2.9f) * gs;
            StormMainBolt(from, to, thickness, light, .32f, 0, true);
            var strand = StormStroke(from, to, thickness * .7f, new Color(1, 1, 1, .75f), .22f, false, 127, random.Next(StormBoltVariants), Chance(.5f));
            strand.flicker = .45f; strand.fadePower = 1.4f;
            if (g >= 2)
            {
                var braid = StormStroke(from, to, thickness * .55f, Alpha(Color.Lerp(light, Color.white, .4f), .8f), .2f, false, 127, random.Next(StormBoltVariants), Chance(.5f));
                braid.flicker = .4f; braid.fadePower = 1.4f; braid.age = -.04f;
            }
            // A wide electric glow along the whole link so the chain itself, not only its ends, owns the peak frame.
            var sheen = StormBand(from, to, thickness * 1.5f, Alpha(light, .42f), .16f, true, 125); sheen.fadePower = 1.8f;
            var spit = Spawn("Arc jump flash", glow, from, 1.9f, Alpha(StormHot, .85f), .14f, true, 130); spit.endScale = 1.4f; spit.ease = 1; spit.fadePower = 1.5f;
            Glint(from, 1.8f, Alpha(StormHot, .9f), .14f, Range(0, 45));
            Discharge(to, 1.2f, light, g >= 2);
            // Storm-coloured chains call thunder down onto every link; other tints (oath links) stay plain arcs. At level three
            // the sky is kept for the closing bolt on the last foe, so the links stay sky-less arcs.
            bool storm = tint.b >= tint.r && tint.b >= tint.g * .95f;
            if (storm && g < 3)
            {
                StormSkyStrike(new Vector2(to.x, to.y - 1), to + new Vector2(Range(-.7f, .7f), 7.2f), light, .05f, .85f * (1 + (g - 1) * .1f), g >= 2);
                if (Time.frameCount != StormFlashFrame) { StormFlashFrame = Time.frameCount; ScreenFlash(to, new Color(.7f, .78f, 1, .14f), .1f); }
            }
        }

        /// <summary>An electric discharge burst: white-hot core, crackling shell, radiating arcs, sparks and smoke.</summary>
        private void Discharge(Vector2 pos, float r, Color light, bool lean = false, float bloomAlpha = .9f)
        {
            Underlay(pos, r * 2.8f, .34f, .3f);
            var hot = Spawn("Discharge core", core, pos, r * .95f, StormHot, .12f, true, 133); hot.endScale = .3f; hot.ease = 1; hot.fadePower = 1.4f;
            var bloom = Spawn("Discharge bloom", glow, pos, r * 2.9f, Alpha(light, bloomAlpha), .26f, true, 130);
            bloom.ramp = Ramp(Color.white, light); bloom.startScale = .55f; bloom.endScale = 1.15f; bloom.ease = 2; bloom.fadePower = 1.6f; bloom.flicker = .25f;
            ImpactLines(pos, r * 3.6f, new Color(1, 1, 1, .9f), .2f);
            Glint(pos, r * 3, StormHot, .22f, Range(0, 45));
            var shell = Spawn("Discharge arcs", StormArcRings[random.Next(StormArcRings.Length)], pos, r * 2.6f, Color.white, .3f, false, 128);
            shell.frames = StormArcRings; shell.fps = 24; shell.loop = true; shell.spin = Range(-200, 200);
            shell.startScale = .3f; shell.endScale = 1.05f; shell.ease = 3; shell.fadePower = 1.5f; shell.flicker = .2f;
            Wave(pos, r * 3.2f, Alpha(light, .85f), .3f, 3);
            int arcs = Budget(5);
            for (int i = 0; i < arcs; i++)
            {
                float angle = i * 360f / arcs + Range(-25, 25);
                StormArc(pos, pos + Direction(angle) * r * Range(1.1f, 1.9f), r * .6f, Range(.1f, .18f), Range(0, .06f));
            }
            int sparks = GradeSteady(lean ? 8 : 12);
            for (int i = 0; i < sparks; i++) Spark(pos, Direction(Range(0, 360)) * r * Range(5, 10), i % 3 == 0 ? Color.white : light, Range(.22f, .4f), Range(.25f, .45f), 9, 2.4f);
            // Residual crackle crawling over the target after the hit (one arc on the lean links of a higher-level chain).
            for (int i = 0; i < (lean ? 1 : 3); i++)
            {
                Vector2 start = pos + Inside(.3f * r);
                StormArc(start, start + Direction(Range(0, 360)) * r * Range(.5f, .9f), r * .4f, .07f, Range(.12f, .5f), .85f);
            }
            for (int i = 0; i < 2; i++)
            {
                var smoke = Puff(pos + Inside(.3f * r), r * Range(.8f, 1.2f), new Color(.16f, .16f, .26f, .45f), new Vector2(Range(-.4f, .4f), Range(.8f, 1.5f)), Range(.7f, 1), 112);
                smoke.age = -.08f;
            }
        }

        // ---------------------------------------------------------------- arcane volley

        /// <summary>A rune circle, vertical or lying on the ground (ground circles do not spin: a squashed ellipse would wobble).</summary>
        private Clip StormRune(Vector2 pos, float width, Color color, float life, float spin, bool onGround, bool additive, int order)
        {
            var clip = Spawn("Storm rune circle", StormSeal, pos, width, color, life, additive, order);
            if (onGround) { clip.scale.y *= .34f; clip.root.transform.localScale = clip.scale; }
            clip.spin = onGround ? 0 : spin; clip.fadeIn = .08f; clip.startScale = .6f; clip.endScale = 1; clip.ease = 4; clip.fadePower = 1.4f; clip.hold = .4f;
            return clip;
        }

        /// <summary>An arcane orb (ink shell, light, core and a streak tail) spiralling in to <paramref name="center"/>.</summary>
        private void StormOrbiter(Vector2 center, float angle, float startRadius, float endRadius, float life, float turns, Color light)
        {
            for (int layer = 0; layer < 4; layer++)
            {
                Clip clip;
                if (layer == 0) clip = Spawn("Arcane orb shell", orb, center, .8f, StormArcaneInk, life, false, 124);
                else if (layer == 1) clip = Spawn("Arcane orb", orb, center, .8f, Alpha(light, .95f), life, true, 126);
                else if (layer == 2) clip = Spawn("Arcane orb core", core, center, .32f, Color.white, life, true, 127);
                else { clip = Spawn("Arcane orb tail", spark, center, 1.3f, Alpha(light, .85f), life, true, 125); clip.alignVelocity = true; clip.scale.y *= .7f; }
                clip.orbitCenter = center; clip.orbitAngle = angle; clip.orbit = turns * 360 / Mathf.Max(.05f, life);
                clip.orbitRadius = startRadius; clip.orbitRadiusEnd = endRadius; clip.orbitPower = 1.3f;
                clip.fadeIn = .06f; clip.hold = .85f; clip.fadePower = 2;
            }
        }

        private void CastArcaneVolley(CastInfo c)
        {
            float w = c.windup, release = Mathf.Max(0, w - .02f); int f = c.facing; bool echo = w < .2f;
            Vector2 gate = c.pos + new Vector2(f * .8f, 0);
            Color light = Color.Lerp(c.light, StormViolet, .5f);
            // One orb gathers per missile the level fires (3, 4, then 6) and the gate and mandala widen with it.
            int g = Grade; float gs = 1 + (g - 1) * .1f; int orbs = g >= 3 ? 6 : g == 2 ? 4 : 3;

            var focus = Anchored(c.anchor, gate, "Arcane focus", glow, (echo ? 1.6f : 2.4f) * gs, Alpha(light, .7f), w + .08f, 122);
            focus.fadeIn = .05f; focus.startScale = .5f; focus.endScale = 1; focus.hold = .8f; focus.fadePower = 2; focus.pulse = .06f;
            var focusStar = Anchored(c.anchor, gate, "Arcane focus star", glint, echo ? 1 : 1.5f, StormHot, w + .06f, 125);
            focusStar.spin = 200 * f; focusStar.startScale = .3f; focusStar.endScale = 1; focusStar.hold = .8f; focusStar.fadePower = 2;
            Emitter(focus, Emit.Sparkles, echo ? 10 : 26, .6f, light);

            if (!echo)
            {
                // A mandala behind the caster: deep ink under bright glyphs, an inner seal turning the other way.
                Vector2 halo = c.pos + new Vector2(-f * .15f, .2f);
                StormRune(halo, 4.2f * gs, new Color(.26f, .04f, .5f, .85f), w + .3f, 60 * f, false, false, 28);
                StormRune(halo, 4.2f * gs, Alpha(light, .6f), w + .3f, 60 * f, false, true, 29);
                if (g >= 3)
                {
                    // Awakened: an outer ring of the mandala turning the other way.
                    var outerInk = Spawn("Arcane outer seal ink", runeRing, halo, 5.8f, new Color(.26f, .04f, .5f, .6f), w + .3f, false, 27);
                    var outer = Spawn("Arcane outer seal", runeRing, halo, 5.7f, Alpha(StormPink, .6f), w + .3f, true, 28);
                    foreach (var s in new[] { outerInk, outer }) { s.spin = -35 * f; s.fadeIn = .1f; s.startScale = .5f; s.endScale = 1; s.ease = 3; s.hold = .6f; s.fadePower = 1.6f; }
                }
                var inner = Spawn("Arcane inner seal", sigil, halo, 2.4f * gs, Alpha(StormPink, .8f), w + .25f, true, 29);
                inner.spin = -110 * f; inner.fadeIn = .1f; inner.startScale = .5f; inner.endScale = 1; inner.ease = 3; inner.hold = .6f; inner.fadePower = 1.6f;
                Vector2 floor = StormFeet(c) + Vector2.up * .03f;
                StormRune(floor, 3.6f, new Color(.26f, .04f, .5f, .6f), w + .35f, 0, true, false, 26);
                StormRune(floor, 3.6f, Alpha(light, .85f), w + .35f, 0, true, true, 27);

                // A launch gate in front of the hand, seen edge-on toward the target.
                for (int i = 0; i < 2; i++)
                {
                    bool ink = i == 0;
                    var door = Spawn(ink ? "Arcane gate ink" : "Arcane gate", StormSeal, gate, 3, ink ? new Color(.26f, .04f, .5f, .8f) : Alpha(light, .95f), w + .15f, !ink, ink ? 121 : 123);
                    door.scale.x *= .36f; door.root.transform.localScale = door.scale;
                    door.startScale = .3f; door.endScale = 1; door.ease = 3; door.fadeIn = .08f; door.hold = .7f; door.fadePower = 2;
                }

                // Three orbs, one per missile, spiral in to the gate.
                for (int i = 0; i < orbs; i++) StormOrbiter(gate, i * 360f / orbs + Range(-10, 10), 1.8f * gs, .12f, w, f * 1.1f, light);
                Converge(gate, 10, 2.2f, light, w, .5f);
                GroundGlow(floor, 3.6f, Alpha(light, .4f), w + .3f);
                Underlay(gate, 2.6f, .3f, w + .1f).fadeIn = w * .4f;
            }

            // Release: the gate bursts.
            // Crisp shapes (star, lines, rings) carry the release; the soft glow stays small so the gate does not blur into a blob.
            var blast = Spawn("Arcane release", glow, gate, echo ? 1.4f : 2.1f, new Color(1, .94f, 1, .6f), .14f, true, 131);
            blast.age = -release; blast.startScale = .5f; blast.endScale = 1.3f; blast.ease = 1; blast.fadePower = 1.6f;
            var gateInk = Spawn("Arcane release ink", ring, gate, echo ? 2.4f : 3.6f, new Color(.26f, .04f, .5f, .75f), .26f, false, 122);
            gateInk.scale.x *= .42f; gateInk.root.transform.localScale = gateInk.scale; gateInk.age = -release;
            gateInk.startScale = .2f; gateInk.endScale = 1; gateInk.ease = 3; gateInk.fadePower = 1.3f;
            var star = Glint(gate, echo ? 2.2f : 3.4f, new Color(1, .95f, 1, .95f), .22f, 0); star.age = -release;
            var lines = ImpactLines(gate, echo ? 2.6f : 3.6f, Alpha(StormPink, .85f), .18f); lines.age = -release;
            var ringOut = Wave(gate, echo ? 2.2f : 3.2f, Alpha(light, .95f), .26f, 3); ringOut.scale.x *= .42f; ringOut.age = -release;
            int sparks = echo ? 4 : 8;
            for (int i = 0; i < sparks; i++)
            {
                var s = Spark(gate, Direction(Range(-50, 50)) * Range(4, 9), i % 2 == 0 ? Color.white : light, Range(.2f, .35f), Range(.2f, .35f), 3, 3);
                s.velocity = new Vector2(s.velocity.x * f, s.velocity.y); s.age = -release;
            }
        }

        private void ImpactArcaneVolley(Vector2 pos, float radius, Color tint, int facing)
        {
            StormArcaneBurst(pos, Mathf.Clamp(radius / 1.2f, .6f, 2.2f), tint, facing != 0 ? Vector2.right * facing : Vector2.zero);
        }

        /// <summary>A double-helix ribbon trail: two thin bands twisting round the missile's path.</summary>
        private Action<Clip, Vector2> StormHelix(Transform root, Color color)
        {
            Vector2 last = root.position, lastA = last, lastB = last, heading = root.right;
            return (owner, p) =>
            {
                Vector2 now = owner.root.transform.position, step = now - last;
                if (step.sqrMagnitude < .0004f) return;
                heading = step.normalized; last = now;
                Vector2 side = new Vector2(-heading.y, heading.x) * (Mathf.Sin(owner.age * 30) * .3f);
                Vector2 a = now + side, b = now - side;
                var ribbonA = Line(lastA, a, .22f, Alpha(color, .95f), .32f, 122, 1.2f); ribbonA.axisEnd = new Vector2(1, .2f);
                var ribbonB = StormBand(lastB, b, .2f, new Color(.42f, .1f, .74f, .85f), .32f, false, 123); ribbonB.axisEnd = new Vector2(1, .2f);
                lastA = a; lastB = b;
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
                // Companions' basic bolt: a spinning star in a deep-tinted shell with a thin ribbon.
                Color tone = Color.Lerp(tint, ArcaneLight, .3f);
                var shell = Attach(root, "Bolt shell", orb, .5f, StormDeepen(tint, .85f), 120, false);
                var star = Attach(root, "Bolt star", glint, .8f, Color.Lerp(tone, Color.white, .55f), 123); star.spin = 360;
                var halo = Attach(root, "Bolt halo", glow, 1.05f, Alpha(tone, .8f), 121); halo.pulse = .1f;
                Emitter(halo, 16, 0, StormRibbon(root, .2f, Alpha(tone, .7f), .14f));
                Bundle(root, star, shell, halo);
                return;
            }
            Color light = Color.Lerp(tint, StormViolet, .5f);
            // Higher levels fire more missiles (3, 4, 6): each head is drawn a little smaller so the volley reads as a swarm of
            // separate missiles, not one ball; from level two a second pair of motes circles the other way.
            int g = Grade; float gs = g >= 3 ? .68f : g == 2 ? .84f : 1;
            var body = Attach(root, "Arcane missile shell", orb, 1.45f * gs, StormArcaneInk, 120, false); body.pulse = .06f;
            var aura = Attach(root, "Arcane missile aura", glow, 2.5f * gs, Alpha(light, .48f), 121); aura.pulse = .12f;
            // Comet tails: a deep violet ink wake under a bright one, so the missile keeps a silhouette on pale skies.
            var inkTail = Attach(root, "Arcane missile ink tail", streak, 3.4f * gs, new Color(.3f, .06f, .55f, .62f), 119, false, new Vector2(-1.3f * gs, 0)); inkTail.scale.y *= .42f;
            var tail = Attach(root, "Arcane missile tail", streak, 4.2f * gs, Alpha(light, .72f), 121, true, new Vector2(-1.7f * gs, 0)); tail.scale.y *= .36f;
            var twin = Attach(root, "Arcane twin motes", StormTwin, 2.1f * gs, Alpha(StormPink, .95f), 123); twin.spin = 900;
            var star2 = Attach(root, "Arcane missile star", glint, 2.4f * gs, new Color(1, .96f, 1), 124); star2.spin = -240; star2.pulse = .14f;
            var heart = Attach(root, "Arcane missile core", core, .6f * gs, Color.white, 125);
            Emitter(aura, 30 / (1 + (g - 1) * .5f), 0, StormHelix(root, light));
            var partList = new List<Clip> { body, aura, inkTail, tail, twin, star2, heart };
            if (g >= 2)
            {
                var twin2 = Attach(root, "Arcane counter motes", StormTwin, 1.6f * gs, Alpha(Color.Lerp(light, Color.white, .3f), .9f), 123); twin2.spin = -700; twin2.angle = 90;
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
            // A volley leaves the gate together: the light layers (not the violet ink) start dimmed, by more the more missiles the
            // level fires (3, 4, 6), and come up as the missiles fan apart, so the gate never sums into one white blob.
            float muzzleCap = g >= 3 ? .4f : g == 2 ? .55f : .7f;
            Action<float, float> shape = (grow, bright) =>
            {
                float head = .2f + .8f * grow, light = Mathf.Lerp(muzzleCap, 1, bright);
                for (int i = 0; i < parts.Length; i++)
                {
                    bool wake = baseX[i] < 0;
                    parts[i].scale = wake ? new Vector3(baseScale[i].x * Mathf.Max(.05f, grow), baseScale[i].y, 1) : baseScale[i] * head;
                    if (wake) parts[i].offset.x = baseX[i] * grow;
                    if (parts[i] != body && parts[i] != inkTail) parts[i].color.a = baseAlpha[i] * light;
                }
            };
            shape(0, 0);
            for (int i = 0; i < parts.Length; i++) { parts[i].root.transform.localScale = parts[i].scale; parts[i].root.transform.localPosition = parts[i].offset; }
            // Six phases round the circle with the sway sign alternating, so missiles of one volley split into a fan at the gate.
            int slot = StormVolleyCount++;
            float phase = (slot % 6) * Mathf.PI / 3 + .4f, sway = (slot % 2 == 0 ? 1 : -1) * (g >= 3 ? 1.05f : g == 2 ? .9f : .75f);
            Emitter(heart, 100, 0, (owner, p) =>
            {
                float t = owner.age, y = Mathf.Sin(t * 20 + phase) * sway * Mathf.Clamp01(t * 9) * Mathf.Clamp01(1 - t / .8f);
                shape(1 - Sq(1 - Mathf.Clamp01(t / .16f)), Mathf.Clamp01((t - .05f) / .25f));
                // Every layer weaves, the level crown added after this one included.
                owner.offset.y = y;
                if (owner.layers != null) for (int i = 0; i < owner.layers.Count; i++) owner.layers[i].offset.y = y;
            });

            Vector2 at = root.position;
            var ringOut = Wave(at, 1.6f, Alpha(light, .9f), .2f, 3); ringOut.scale.x *= .45f; ringOut.angle = root.eulerAngles.z;
            if (Time.frameCount == StormMuzzleFrame) return;
            StormMuzzleFrame = Time.frameCount;
            Glint(at, 2.2f, StormHot, .16f, Range(0, 45));
            var puff = Spawn("Arcane launch flash", glow, at, 1.5f, Alpha(light, .7f), .12f, true, 130); puff.endScale = 1.5f; puff.ease = 1; puff.fadePower = 1.5f;
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
            // Basic and hostile bolts: a crisp pop, ten clips, gone in a fifth of a second.
            Color light = Color.Lerp(tint, Color.white, .3f);
            var flash = Spawn("Bolt hit flash", glow, pos, .8f, new Color(1, 1, 1, .9f), .1f, true, 132); flash.endScale = 1.45f; flash.ease = 1; flash.fadePower = 1.4f;
            var bloom = Spawn("Bolt hit bloom", glow, pos, 1.15f, Alpha(tint, .7f), .18f, true, 130); bloom.startScale = .5f; bloom.ease = 2; bloom.fadePower = 1.5f;
            var ink = Spawn("Bolt hit ink", ring, pos, .9f, StormDeepen(tint, .75f), .16f, false, 126); ink.startScale = .25f; ink.endScale = 1; ink.ease = 3; ink.fadePower = 1.4f;
            Glint(pos, 1.05f, Alpha(light, .95f), .15f, Range(0, 45));
            Wave(pos, 1.15f, Alpha(light, .8f), .2f, 3);
            float back = Mathf.Atan2(-direction.y, -direction.x) * Mathf.Rad2Deg;
            for (int i = 0; i < 5; i++) Spark(pos, Direction(back + Range(-70, 70)) * Range(3, 6), i % 2 == 0 ? Color.white : light, Range(.18f, .28f), Range(.14f, .24f), 4, 3);
        }

        /// <summary>The arcane burst: a white-hot star, a collapsing ring, then a spinning seal, prism shards and a spiral of motes.</summary>
        private void StormArcaneBurst(Vector2 pos, float s, Color tint, Vector2 direction)
        {
            Color light = Color.Lerp(tint, StormViolet, .55f);
            // More missiles land on each foe at higher levels: the first burst holds its soft white back (and its star
            // stays one size) so the following missiles' pops on its rim read as separate hits.
            float soft = Grade >= 3 ? .55f : Grade == 2 ? .8f : 1;
            Underlay(pos, 3.2f * s, .42f, .4f);
            var hot = Spawn("Arcane burst core", core, pos, s, Color.white, .14f, true, 133); hot.endScale = .25f; hot.ease = 1; hot.fadePower = 1.3f;
            // A deep violet shock front racing out under the light: the burst's silhouette on pale backgrounds.
            var shockInk = Spawn("Arcane shock ink", ring, pos, 4.6f * s, new Color(.24f, .04f, .48f, .72f), .32f, false, 126);
            shockInk.startScale = .15f; shockInk.endScale = 1; shockInk.ease = 3; shockInk.fadePower = 1.3f;
            var bloom = Spawn("Arcane burst bloom", glow, pos, 3.4f * s, Alpha(light, .72f * soft), .3f, true, 130);
            bloom.ramp = Ramp(Color.white, light); bloom.startScale = .5f; bloom.endScale = 1.2f; bloom.ease = 2; bloom.fadePower = 1.6f;
            Glint(pos, 3.4f * Mathf.Min(s, 1.55f), new Color(1, .95f, 1, .95f * Mathf.Lerp(1, soft, .5f)), .26f, 0);
            ImpactLines(pos, 3.8f * s, new Color(1, .92f, 1, .85f), .22f);
            var implode = Spawn("Arcane implosion", ring, pos, 3 * s, Alpha(light, .95f), .08f, true, 131); implode.startScale = 1; implode.endScale = .12f; implode.ease = 1; implode.fadePower = .6f;

            const float pop = .06f;
            var sealInk = Spawn("Arcane seal ink", StormSeal, pos, 3.2f * s, StormArcaneInk, .55f, false, 127);
            var seal = Spawn("Arcane seal", StormSeal, pos, 3.2f * s, Alpha(light, .95f), .5f, true, 129);
            foreach (var layer in new[] { sealInk, seal }) { layer.age = -pop; layer.spin = 240; layer.startScale = .3f; layer.endScale = 1.1f; layer.ease = 4; layer.hold = .35f; layer.fadePower = 1.4f; }
            var wave = Wave(pos, 4 * s, Alpha(light, .9f), .36f, 3); wave.age = -pop;
            var wave2 = Wave(pos, 2.6f * s, new Color(1, 1, 1, .75f), .22f, 2.5f); wave2.age = -pop;
            var star = Glint(pos, 2.4f * s, Alpha(StormPink, .95f), .3f, 45); star.age = -pop;

            int shards = GradeSteady(12);
            for (int i = 0; i < shards; i++)
            {
                Vector2 dir = Direction(i * 360f / shards + Range(-12, 12));
                bool ink = i % 3 == 0;
                var piece = Spawn("Arcane shard", StormShard, pos + dir * .2f * s, Range(.3f, .52f) * s, ink ? StormArcaneInk : (i % 2 == 0 ? Color.white : light), Range(.35f, .55f), !ink, ink ? 127 : 131);
                piece.age = -pop; piece.velocity = (dir * Range(4.5f, 8.5f) + direction * 2) * s; piece.drag = 3.2f;
                piece.alignVelocity = true; piece.angleOffset = -90; piece.endScale = .4f; piece.ease = 1; piece.fadePower = 1.4f;
            }
            int sparks = GradeSteady(12);
            for (int i = 0; i < sparks; i++)
            {
                var spk = Spark(pos, (Direction(Range(0, 360)) * Range(4, 9) + direction * 3) * s, i % 3 == 0 ? Color.white : light, Range(.2f, .36f), Range(.25f, .45f), 4, 2.5f);
                spk.age = -pop;
            }
            int motes = GradeSteady(8);
            for (int i = 0; i < motes; i++)
            {
                var mote = Spawn("Arcane spiral mote", glow, pos, Range(.2f, .32f) * s, i % 2 == 0 ? Alpha(StormPink, .95f) : Alpha(light, .95f), Range(.45f, .6f), true, 131);
                mote.age = -pop; mote.orbitCenter = pos; mote.orbitAngle = i * 360f / motes; mote.orbit = 520;
                mote.orbitRadius = .2f * s; mote.orbitRadiusEnd = Range(1.5f, 2) * s; mote.orbitPower = .6f; mote.endScale = .2f; mote.ease = 1; mote.fadePower = 1.3f;
            }
            var runeInk = StormRune(pos, 2.4f * s, Alpha(StormArcaneInk, .45f), 1.05f, -120, false, false, 126); runeInk.age = -.1f;
            var rune = StormRune(pos, 2.4f * s, Alpha(light, .55f), 1, -120, false, true, 128); rune.age = -.1f;
            Sparkles(pos, Mathf.RoundToInt(8 / (1 + (Grade - 1) * .34f)), .9f * s, light);
            int rising = GradeSteady(6);
            for (int i = 0; i < rising; i++)
            {
                var mote = Spawn("Arcane ember", wisp, pos + Inside(.8f * s), Range(.16f, .28f) * s, Alpha(i % 2 == 0 ? StormPink : light, .95f), Range(.8f, 1.2f), true, 131);
                mote.age = -Range(.1f, .3f); mote.velocity = new Vector2(Range(-.5f, .5f), Range(.9f, 1.8f)); mote.drag = .7f; mote.fadeIn = .1f; mote.flicker = .35f; mote.endScale = .3f; mote.ease = 1;
            }
        }

        /// <summary>A follow-up missile landing on a burst that just went off: a quick offset pop, staggered by its place in the volley.</summary>
        private void StormArcanePop(Vector2 pos, Color tint, Vector2 direction, int stack)
        {
            Color light = Color.Lerp(tint, StormViolet, .55f);
            float delay = Mathf.Min(4, stack) * .07f;
            // Each follower pops on the first burst's rim, a sixth of a turn round from the last, clear of its white core.
            Vector2 at = pos + Direction(stack * 60 + 30) * 1.15f;
            var ink = Spawn("Arcane pop ink", ring, at, 1.9f, StormArcaneInk, .2f, false, 126); ink.age = -delay; ink.startScale = .3f; ink.endScale = 1; ink.ease = 3; ink.fadePower = 1.4f;
            // A small violet seal turns where it landed, so the extra missile leaves a mark of its own.
            var mark = Spawn("Arcane pop seal", StormSeal, at, 1.4f, Alpha(light, .9f), .4f, true, 129); mark.age = -delay; mark.spin = 260; mark.startScale = .3f; mark.endScale = 1; mark.ease = 4; mark.hold = .3f; mark.fadePower = 1.4f;
            var markInk = Spawn("Arcane pop seal ink", StormSeal, at, 1.45f, StormArcaneInk, .42f, false, 127); markInk.age = -delay; markInk.spin = 260; markInk.startScale = .3f; markInk.endScale = 1; markInk.ease = 4; markInk.hold = .3f; markInk.fadePower = 1.4f;
            var flash = Spawn("Arcane pop flash", glow, at, 2.1f, new Color(1, .92f, 1, .9f), .14f, true, 131); flash.age = -delay; flash.endScale = 1.4f; flash.ease = 1; flash.fadePower = 1.5f;
            var star = Glint(at, 2.9f, Alpha(stack % 2 == 0 ? Color.white : StormPink, .95f), .2f, stack % 2 == 0 ? 0 : 45); star.age = -delay;
            var wave = Wave(at, 2.8f, Alpha(light, .85f), .24f, 3); wave.age = -delay;
            int shards = GradeSteady(5);
            for (int i = 0; i < shards; i++)
            {
                Vector2 dir = Direction(Range(0, 360));
                var piece = Spawn("Arcane shard", StormShard, at, Range(.22f, .36f), i == 0 ? StormArcaneInk : (i % 2 == 0 ? Color.white : light), Range(.3f, .45f), i != 0, i == 0 ? 127 : 131);
                piece.age = -delay; piece.velocity = dir * Range(4, 7.5f) + direction * 2; piece.drag = 3.2f;
                piece.alignVelocity = true; piece.angleOffset = -90; piece.endScale = .4f; piece.ease = 1; piece.fadePower = 1.4f;
            }
            int sparks = GradeSteady(5);
            for (int i = 0; i < sparks; i++)
            {
                var spk = Spark(at, Direction(Range(0, 360)) * Range(4, 8) + direction * 3, i % 2 == 0 ? Color.white : light, Range(.2f, .32f), Range(.2f, .35f), 4, 2.5f);
                spk.age = -delay;
            }
        }

        /// <summary>The volley's closing chord on one target: a column of arcane light out of a sealing ring on the floor.</summary>
        private void StormArcaneResonance(Vector2 pos, Color tint)
        {
            Color light = Color.Lerp(tint, StormViolet, .55f);
            const float delay = .12f;
            // Missiles aim at the body, about a unit above the feet.
            Vector2 ground = pos + Vector2.down;
            var ink = Spawn("Arcane column ink", pillar, ground, 2.9f, new Color(.2f, .03f, .42f, .55f), .75f, false, 117);
            ink.scale = new Vector3(2.9f / pillar.bounds.size.x, 8 / pillar.bounds.size.y, 1); ink.axisStart = new Vector2(.4f, .15f); ink.axisEnd = Vector2.one; ink.ease = 4; ink.hold = .3f; ink.fadePower = 1.3f;
            var column = Pillar(ground, 2.2f, 8, Alpha(light, .95f), .65f, 128); column.hold = .35f;
            var beam = Pillar(ground, .7f, 8.5f, new Color(1, .95f, 1, .95f), .5f, 129); beam.hold = .3f;
            var seal = RuneCircle(ground, 4.4f, Alpha(light, .95f), .9f, 90, true);
            var floor = GroundGlow(ground, 5, Alpha(light, .5f), .8f);
            var wave = GroundWave(ground, 6.5f, Alpha(light, .9f), .45f);
            var flash = Flash(pos, 4.5f, Alpha(StormPink, .8f), .25f);
            var star = Glint(pos, 4, new Color(1, .95f, 1, .95f), .3f, 0);
            foreach (var clip in new[] { ink, column, beam, seal, floor, wave, flash, star }) clip.age = -delay;
            int motes = Budget(8);
            for (int i = 0; i < motes; i++)
            {
                var mote = Spawn("Arcane column mote", spark, ground + new Vector2(Range(-.55f, .55f), Range(0, 1.5f)), Range(.3f, .5f), Alpha(i % 2 == 0 ? StormPink : light, .95f), Range(.35f, .55f), true, 131);
                mote.age = -delay - Range(0, .1f); mote.velocity = new Vector2(Range(-.4f, .4f), Range(9, 15)); mote.drag = 1.5f; mote.alignVelocity = true; mote.endScale = .3f; mote.ease = 1;
            }
        }
    }
}
