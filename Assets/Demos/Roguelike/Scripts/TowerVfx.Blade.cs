using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Blade family: Shockwave (a golden slam and a towering painted sword wave), BladeStorm (painted crescent sweeps whirling
    // round the hero with spectral swords and crossing cuts) and DashStrike (a dash drawn as long slash beams with white
    // afterimages, ending in a giant painted crescent iaido cut). Everything is painted light: strokes are smooth level
    // profiles (deep saturated edge, colour, a pale band, a white-hot core) with brush streaks running along each stroke,
    // a soft bloom glowing under every bright stroke, hits that flare and scatter as hand-drawn sparks, and painted stone
    // lit from the upper left. No dark outlines or ink twins: where light needs contrast it gets a deeper hue of its own
    // colour or a bloom. Shapes pop open in a frame or two, hold, then erode from the thin edge.
    public sealed partial class TowerVfx
    {
        private static Sprite BladeSlashSprite, BladeCutSprite, BladeBeamSprite, BladeWhirlSprite, BladeSwordSprite, BladeRingSprite;
        private static Sprite[] BladeSweepSheet, BladeSweepFray, BladeRockSprites, BladeSpireSprites;

        private static readonly Color BladeHotGold = new Color(1, .8f, .34f), BladeStormGold = new Color(1, .7f, .24f), BladeDashBlue = new Color(.26f, .72f, 1),
            BladeDeepGold = new Color(1, .5f, .1f), BladeCobalt = new Color(.12f, .4f, 1), BladeDust = new Color(.66f, .56f, .44f, .55f),
            BladeSoot = new Color(.42f, .35f, .3f, .55f), BladeWind = new Color(.82f, .93f, 1, .42f), BladeAwakeGold = new Color(1, .9f, .6f);
        // A slam's wind-up, noted where and which way the hero swung: gameplay draws the slam's burst without a facing, and the
        // sword wave only runs forward, so the burst's rock, fronts and reach ring run forward too. first is the slam an
        // awakened second wave (a quarter second later, from the same caster) echoes.
        private sealed class BladeSlamNote { public Clip clock; public int facing; public Clip first; }
        private readonly List<BladeSlamNote> BladeSlamNotes = new List<BladeSlamNote>();
        // The last sword wave striking a body (so a wave consumed by a hit does not also break apart at the end of its flight).
        private Clip BladeWaveHit;

        private static void BuildBladeTextures()
        {
            // Every light shape below is a level profile for the painted-light ramp: a crisp edge, an even fall-off across the stroke
            // (so the bands come out evenly) and brush streaks running along it, slow along the stroke and fast across.

            // A long razor cut bowing upward: a hard white edge along the top, colour falling off below it, tapering to points
            // whose ends break up into dry brush streaks.
            BladeSlashSprite = Build("Blade slash", 512, 128, (u, v) =>
            {
                float au = Mathf.Abs(u), taper = Mathf.Max(0, 1 - u * u), center = .22f - .5f * u * u;
                float half = .34f * Mathf.Pow(taper, .75f) + .008f, top = half * .36f, d = v - center;
                if (d > top + .03f || d < -half - .02f) return Color.clear;
                float across = Mathf.Clamp01((d + half) / (top + half)), edge = 1 - Smooth(top - .01f, top + .006f, d);
                float streaks = Fbm(u * 1.7f + 3, d * 16 / Mathf.Max(.3f, taper) + 7, 2);
                float level = edge * Mathf.Pow(across, 1.1f) * (.95f + .5f * (streaks - .5f)) * Mathf.Pow(taper, .3f);
                level -= Smooth(.55f, .95f, au) * (1 - across * .6f) * (.15f + .9f * Mathf.Max(0, .55f - streaks));
                return White(Mathf.Clamp01(level) * (1 - Smooth(.92f, .99f, au)));
            });
            // A spindle cut, thickest toward its head (+x) with a long thin tail.
            BladeCutSprite = Build("Blade cut", 256, 48, (u, v) =>
            {
                float t = (u + 1) * .5f, profile = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(t, 1.35f)), .9f);
                float half = .86f * profile + .002f, across = Mathf.Clamp01(1 - Mathf.Abs(v) / half);
                float grain = Fbm(u * 2.4f + 11, v * 5 + 3, 2);
                return White(Mathf.Pow(across, .8f) * Mathf.Pow(profile, .35f) * (1.02f + .3f * (grain - .5f)));
            });
            // A long slash beam: a needle head at +x, the body thickest just behind it and a long tail that splits into
            // separate dry-brush streaks toward -x.
            BladeBeamSprite = Build("Blade beam", 512, 64, (u, v) =>
            {
                float t = (u + 1) * .5f;
                float body = t < .8f ? Mathf.Pow(t / .8f, .5f) : Mathf.Pow(Mathf.Max(0, 1 - (t - .8f) / .2f), .75f);
                float half = .84f * body + .002f, across = Mathf.Clamp01(1 - Mathf.Abs(v) / half);
                float streak = Fbm(u * 1.5f + 5, v * 8 / Mathf.Max(.3f, body) + 2, 2);
                float level = Mathf.Pow(across, .75f) * (1 + .45f * (streak - .5f)) * Mathf.Pow(body, .25f);
                level -= (1 - Smooth(.05f, .62f, t)) * (.2f + 1.1f * Mathf.Max(0, .56f - streak));
                return White(Mathf.Clamp01(level) * (1 - Smooth(.95f, 1, Mathf.Abs(u))));
            });
            // A sweeping arc round a circle: a pointed head at 0 degrees, a razor outer rim (white near the head) and a body
            // streaked along the sweep that thins and breaks into dry brush as it trails off clockwise.
            BladeWhirlSprite = Build("Blade whirl", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .97f || r < .4f) return Color.clear;
                float rad = Mathf.Atan2(v, u), behind = Mathf.Repeat(-rad * Mathf.Rad2Deg + 12, 360) - 12;
                if (behind < -8 || behind > 240) return Color.clear;
                float head = Smooth(-8, 26, behind), trail = Mathf.Exp(-Mathf.Max(0, behind) / 90f);
                float outer = .91f, thick = .28f * head * (.3f + .7f * trail) + .004f, depth = (outer - r) / thick;
                if (depth > 1) return Color.clear;
                float rim = 1 - Smooth(outer - .006f, outer + .008f, r), n = RingNoise(rad, r, 1.5f, 17);
                float level = rim * Mathf.Pow(Mathf.Clamp01(1 - depth), 1.15f) * (.8f + .5f * (n - .5f)) * (.5f + .5f * trail);
                level -= Smooth(60, 200, behind) * (.1f + .9f * Mathf.Max(0, .55f - n));
                return White(Mathf.Clamp01(level));
            });
            // A giant painted crescent (bulge toward +x), six drawn frames: the stroke is laid upward from the bottom tip in the
            // first two, held, then frayed from its hollow side out to the razor rim. Dry-brush gaps run along it and a second,
            // thinner stroke line sits inside the hollow edge, as a brush leaves it.
            BladeSweepSheet = Sheet("Blade sweep", 6, 256, 256, (u, v, f) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .97f || r < .3f) return Color.clear;
                float theta = Mathf.Atan2(v, u) * Mathf.Rad2Deg; if (Mathf.Abs(theta) > 91) return Color.clear;
                float s = theta / 90f, lead = f == 0 ? -10 : f == 1 ? 62 : 120, drawn = 1 - Smooth(lead - 30, lead, theta);
                if (drawn <= 0) return Color.clear;
                float thick = .4f * Mathf.Max(0, 1 - Mathf.Pow(Mathf.Abs(s), 2.4f)) * (1 + .16f * s) * Mathf.Lerp(.4f, 1, drawn) + .004f;
                float outer = .92f - .025f * Mathf.Abs(s), depth = (outer - r) / thick;
                if (depth > 1) return Color.clear;
                float rim = 1 - Smooth(outer - .006f, outer + .008f, r), n = RingNoise(theta * Mathf.Deg2Rad, r, 1.3f, 19);
                float level = rim * Mathf.Pow(Mathf.Clamp01(1 - depth), .85f) * (.97f + .6f * (n - .5f));
                level -= Mathf.Max(0, .42f - n) * 1.4f * depth;
                level += Gauss(depth - .78f, .045f) * .26f * Mathf.Clamp01(1 - Mathf.Abs(s) * 1.2f);
                float fray = f < 3 ? 0 : (f - 2) / 3f;
                level -= fray * (.2f + .9f * (1 - n)) * Smooth(.1f, .85f, depth);
                return White(Mathf.Clamp01(level * drawn));
            });
            BladeSweepFray = new[] { BladeSweepSheet[3], BladeSweepSheet[4], BladeSweepSheet[5] };
            // A spectral sword pointing along +x, painted as light (rgb = light level, alpha = shape): no outline.
            BladeSwordSprite = Build("Blade spectral sword", 256, 64, (u, v) => BladeSwordShade(u, v * .25f));
            // Three swords chasing each other round a circle, each trailing a tapered, streaked motion arc: spin it and they orbit.
            BladeRingSprite = Build("Blade ring", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .99f) return Color.clear;
                float deg = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
                Color c = Color.clear;
                for (int k = 0; k < 3; k++)
                {
                    float behind = Mathf.Repeat(k * 120 - 18 - deg, 360);
                    if (behind < 110)
                    {
                        float fall = 1 - behind / 110f, w = .075f * Mathf.Pow(fall, .8f) + .004f;
                        float across = Mathf.Clamp01(1 - Mathf.Abs(r - .7f) / w), streak = RingNoise(deg * Mathf.Deg2Rad, r, 1.8f, 30);
                        float level = across * Mathf.Pow(fall, .7f) * (.85f + .5f * (streak - .5f)) * Smooth(0, 8, behind);
                        c = Over(c, new Color(1, 1, 1, Mathf.Clamp01(level)));
                    }
                    float a0 = k * 120 * Mathf.Deg2Rad, ca = Mathf.Cos(a0), sa = Mathf.Sin(a0);
                    float px = u - ca * .7f, py = v - sa * .7f;
                    c = Over(c, BladeSwordShade((-px * sa + py * ca) / .42f, (px * ca + py * sa) / .8f));
                }
                return c;
            });
            // Painted stones (lumpy, softly lit from the upper left, pitted, a lit rim and the slam's gold light catching the
            // lower right) and a tall stone spire erupting from the floor, standing on its base.
            BladeRockSprites = new Sprite[3];
            for (int i = 0; i < BladeRockSprites.Length; i++) { int seed = 307 + i * 23; BladeRockSprites[i] = Build("Blade rock " + i, 64, 64, (u, v) => BladeRockSample(u, v, seed)); }
            BladeSpireSprites = new Sprite[3];
            for (int i = 0; i < BladeSpireSprites.Length; i++) { int seed = i; BladeSpireSprites[i] = Build("Blade rock spire " + i, 96, 192, (u, v) => BladeSpireSample(u, v, seed), new Vector2(.5f, 0)); }
        }

        /// <summary>Painted stone colour for a light level: warm umber in shadow, ochre in the middle, pale sand in the light.</summary>
        private static Color BladeStoneHue(float light)
        {
            light = Mathf.Clamp01(light);
            Color c = Color.Lerp(new Color(.25f, .16f, .1f), new Color(.6f, .45f, .31f), Smooth(0, .5f, light));
            return Color.Lerp(c, new Color(.95f, .86f, .7f), Smooth(.45f, 1, light));
        }

        private static Color BladeSwordShade(float x, float y)
        {
            // Painted as light for the ramp, with no outline: the blade's upper face catches more light than the lower one and
            // the two blend across a white-hot fuller; both cutting edges glow, the point flares, and faint streaks run along
            // the steel. The guard is a rounded bar lit from above with a jewel at its heart, the grip a wrapped band and the
            // pommel a small lit ball. The lowest level anywhere stays in the colour body, so no deep rim rings the shape.
            float ay = Mathf.Abs(y);
            float half = x < .55f ? .075f : .075f * Mathf.Clamp01((.96f - x) / .41f);
            float blade = Smooth(-.45f, -.43f, x) * (1 - Smooth(.95f, .965f, x)) * (1 - Smooth(half - .006f, half + .002f, ay));
            float guard = Smooth(-.535f, -.525f, x) * (1 - Smooth(-.445f, -.435f, x)) * (1 - Smooth(.17f, .18f, ay));
            float grip = Smooth(-.85f, -.84f, x) * (1 - Smooth(-.53f, -.52f, x)) * (1 - Smooth(.034f, .04f, ay));
            float pommel = 1 - Smooth(.045f, .055f, Mathf.Sqrt(Sq(x + .88f) + y * y));
            float a = Mathf.Max(Mathf.Max(blade, guard), Mathf.Max(grip, pommel)), l;
            if (x > -.44f && blade > .01f)
            {
                float across = Mathf.Clamp01(ay / Mathf.Max(.004f, half));
                float face = Mathf.Lerp(.42f, .6f, Smooth(-.014f, .014f, y));
                float edge = Smooth(.5f, 1, across) * .26f, fuller = Gauss(y, .011f) * .45f * Smooth(-.43f, -.3f, x) * (1 - Smooth(.72f, .92f, x));
                float point = Smooth(.58f, .94f, x) * .3f, root = (1 - Smooth(-.44f, -.22f, x)) * .08f;
                float streak = (Fbm(x * 6 + 3, y * 70 + 1, 2) - .5f) * .16f;
                l = face + edge + fuller + point - root + streak;
            }
            else if (guard > .01f && x > -.54f && x < -.43f)
            {
                float gy = y / .17f, gx = (x + .485f) / .05f;
                l = .4f + .2f * Smooth(-.8f, .9f, gy) + .12f * Gauss(gy - .45f, .25f) - .06f * Smooth(.6f, 1, Mathf.Abs(gx));
                l += Gauss(Mathf.Sqrt(Sq(gx * 1.4f) + Sq(gy * 3.2f)), .55f) * .45f;
            }
            else if (pommel > .01f && x < -.82f)
            {
                float px = (x + .88f) / .05f, py = y / .05f;
                l = .44f + .4f * Mathf.Clamp01(.55f - px * .45f + py * .55f) + Gauss(Mathf.Sqrt(Sq(px + .3f) + Sq(py - .35f)), .3f) * .2f;
            }
            else l = .32f + .1f * Smooth(-1, 1, y / .04f) + .06f * Mathf.Sin(x * 150 + y * 60);
            return new Color(Mathf.Clamp01(l), Mathf.Clamp01(l), Mathf.Clamp01(l), a);
        }

        /// <summary>A painted stone (alpha, baked colour): a lumpy round silhouette, soft light from the upper left, pits and faint
        /// strata, a shaded (never outlined) rim, a lit rim on the upper left and the slam's gold light on the lower right.</summary>
        private static Color BladeRockSample(float u, float v, int seed)
        {
            float r = Mathf.Sqrt(u * u + v * v), th = Mathf.Atan2(v, u);
            // A broken stone: a lumpy outline with a few chipped flats where it split.
            float rr = .74f + .3f * (RingNoise(th, seed * .37f, 1.3f, 0) - .5f) + .1f * (RingNoise(th, seed, 4.6f, 0) - .5f);
            rr -= Mathf.Max(0, RingNoise(th, seed * 1.7f, 2.4f, 0) - .58f) * .9f;
            float cover = 1 - Smooth(rr - .03f, rr + .005f, r); if (cover <= 0) return Color.clear;
            float x = u / rr, y = v / rr, z = Mathf.Sqrt(Mathf.Max(0, 1 - x * x - y * y));
            float n = Fbm(u * 3.4f + seed, v * 3.4f - seed, 4), pits = Fbm(u * 7 + seed * 2, v * 7 + seed, 3);
            float diffuse = Mathf.Clamp01(-x * .6f + y * .65f + z * .5f);
            float light = (.12f + .88f * diffuse) * (.78f + .5f * (n - .5f));
            float crater = Smooth(.6f, .68f, pits), crack = Gauss(Fbm(u * 2.6f + seed, v * 2.6f + 3, 3) - .5f, .022f);
            light = light * (1 - crater * .35f) + Gauss(pits - .59f, .02f) * .15f * diffuse - crack * .3f;
            light *= 1 - .25f * Smooth(rr - .24f, rr, r) * (1 - diffuse * .6f);
            Color c = BladeStoneHue(light);
            float edgeBand = Smooth(rr - .2f, rr - .03f, r);
            c = Color.Lerp(c, new Color(1, .95f, .82f), edgeBand * Mathf.Clamp01(-x * .7f + y * .7f) * .55f);
            c = Color.Lerp(c, new Color(1, .7f, .28f), edgeBand * Mathf.Clamp01(x * .6f - y * .8f) * .6f);
            c.a = cover; return c;
        }

        /// <summary>A stone spire (alpha, baked colour) standing on its base, one of three variants: a broken shard that leans
        /// and splits off at a slanted top, its outline chipped; three softly blended planes (the lit left face, the front and the
        /// shaded right face) with a highlight along the ridge, grain, strata and cracks in the stone, a lit rim on the left
        /// edge and the slam's gold glow at its foot.</summary>
        private static Color BladeSpireSample(float u, float v, int seed)
        {
            float h = (v + 1) * .5f, side = seed == 1 ? -1 : 1;
            float x = u - side * (.06f + .05f * seed) * h * h;
            float wobble = (Fbm(h * 3 + seed * 5, x > 0 ? 2.3f : 8.1f, 2) - .5f) * .45f;
            float notch = Mathf.Max(0, Fbm(h * 8 + seed * 3, x > 0 ? 5.5f : 1.5f, 2) - .56f) * 1.6f;
            float width = seed == 2 ? .62f : .8f;
            float half = width * Mathf.Pow(Mathf.Max(0, 1 - h * .96f), .7f) * (1 + wobble - notch) + .01f;
            float top = .9f + .07f * side * Mathf.Clamp(x / .25f, -1, 1) - .06f * seed;
            float cover = (1 - Smooth(half - .03f, half + .005f, Mathf.Abs(x))) * Smooth(0, .02f, h) * (1 - Smooth(top - .015f, top + .005f, h));
            if (cover <= 0) return Color.clear;
            float s = Mathf.Clamp(x / Mathf.Max(.02f, half), -1, 1);
            float planes = Smooth(-.6f, -.3f, s) + Smooth(.15f, .45f, s);
            float light = planes < 1 ? Mathf.Lerp(.95f, .62f, planes) : Mathf.Lerp(.62f, .2f, planes - 1);
            light += Gauss(s + .45f, .08f) * .14f + Gauss(h - top + .03f, .03f) * .25f;
            float n = Fbm(u * 5 + seed * 4, v * 10 + 2, 4), grain = Fbm(u * 14 + seed, v * 6, 2);
            float strata = Gauss(Mathf.Repeat(h * 5.5f + Fbm(u * 1.6f + seed, h * 2, 2) * 1.6f, 1) - .5f, .06f);
            float crack = Gauss(Fbm(u * 2.4f + 7 + seed, v * 4.4f + 1, 3) - .5f, .014f) * Smooth(.05f, .3f, h);
            light = light * (.74f + .5f * (n - .5f)) + (grain - .5f) * .1f - strata * .14f - crack * .35f;
            Color c = BladeStoneHue(light);
            c = Color.Lerp(c, new Color(1, .95f, .82f), Smooth(.78f, .98f, -s) * .5f);
            c = Color.Lerp(c, new Color(1, .7f, .28f), (1 - Smooth(.02f, .24f, h)) * .6f);
            c.a = cover; return c;
        }

        // ---------------------------------------------------------------- blade helpers

        /// <summary>The floor under an effect: snaps to the main ground line when the point sits just above it.</summary>
        private static float BladeGroundY(Vector2 pos, float drop)
        {
            float above = pos.y - TowerWorld.Ground;
            return above > -.4f && above < 1.6f ? TowerWorld.Ground : pos.y - drop;
        }

        /// <summary>A blade skill's growth with its level: 1 at level one, <paramref name="two"/> and <paramref name="three"/> above.
        /// Used for height, thickness, glow and density, never for the reach of an area.</summary>
        private float BladeLevel(float two, float three) { return Grade >= 3 ? three : Grade == 2 ? two : 1; }

        /// <summary>A count of secondary particles (sparks, dust, rubble) that Budget then grows with the level: this keeps
        /// their growth gentle (about a sixth more at level three) so the level's new layers, not more debris, carry the step.</summary>
        private int BladeShare(int count) { return Budget(Mathf.Max(1, Mathf.RoundToInt(count * BladeLevel(.85f, .7f)))); }

        private static Color BladeDashHue(Color tint) { return Color.Lerp(BladeDashBlue, tint, .2f); }

        /// <summary>True while <paramref name="clip"/> is alive, younger than <paramref name="window"/> and near <paramref name="pos"/>.</summary>
        private bool BladeRecent(Clip clip, float window, Vector2 pos, float distance)
        {
            return clip != null && clip.age < window && (clip.position - pos).sqrMagnitude < distance * distance && clips.Contains(clip);
        }

        /// <summary>Notes a slam wound up at <paramref name="pos"/> facing <paramref name="facing"/> for <paramref name="life"/> seconds.</summary>
        private void BladeNoteSlam(Vector2 pos, int facing, float life)
        {
            BladeSlamNotes.RemoveAll(n => !clips.Contains(n.clock));
            BladeSlamNotes.Add(new BladeSlamNote { clock = BladeMarker(pos, life), facing = facing < 0 ? -1 : 1 });
        }

        /// <summary>The newest live slam note within 2.5 units of <paramref name="pos"/>, or null (a passive or a boss slam).</summary>
        private BladeSlamNote BladeFindSlam(Vector2 pos)
        {
            BladeSlamNote best = null;
            foreach (var note in BladeSlamNotes)
                if (clips.Contains(note.clock) && (note.clock.position - pos).sqrMagnitude < 6.25f && (best == null || note.clock.age < best.clock.age)) best = note;
            return best;
        }

        /// <summary>An invisible clip that only keeps time and place.</summary>
        private Clip BladeMarker(Vector2 pos, float life) { return Spawn("Blade marker", glow, pos, .05f, new Color(0, 0, 0, 0), life, true, 20); }

        private static void BladeDelay(Clip clip, float delay)
        {
            if (delay <= 0) return;
            clip.age = -delay; if (clip.renderer) clip.renderer.enabled = false;
        }

        /// <summary>Snaps a slash open along its length in a couple of frames.</summary>
        private static void BladeSnap(Clip clip, float delay)
        {
            clip.axisStart = new Vector2(.25f, 1); clip.axisEnd = Vector2.one; clip.ease = 9; clip.startScale = .96f; clip.endScale = 1.05f; clip.hold = .25f; clip.fadePower = 1.3f;
            BladeDelay(clip, delay);
        }

        private static void BladeZoneFade(Clip clip, float life) { clip.life = life; clip.fadeIn = .12f; clip.hold = .72f; clip.fadePower = 1.4f; }

        private Clip BladeStretch(string name, Sprite sprite, Vector2 a, Vector2 b, float thickness, Color color, float life, int order, bool additive)
        {
            Vector2 delta = b - a; float length = Mathf.Max(.05f, delta.magnitude);
            var clip = Spawn(name, sprite, (a + b) * .5f, length, color, life, additive, order);
            clip.scale = new Vector3(length / sprite.bounds.size.x, thickness / sprite.bounds.size.y, 1);
            clip.angle = delta.sqrMagnitude > 1e-6f ? Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg : 0; clip.ease = 1;
            clip.root.transform.localScale = clip.scale; clip.root.transform.localRotation = Quaternion.Euler(0, 0, clip.angle);
            return clip;
        }

        /// <summary>A spindle cut from <paramref name="a"/> to its head at <paramref name="b"/>.</summary>
        private Clip BladeCutLine(Vector2 a, Vector2 b, float thickness, Color color, float life, int order, bool additive = true)
        { return BladeStretch("Blade cut", BladeCutSprite, a, b, thickness, color, life, order, additive); }

        /// <summary>A slash beam from its frayed tail at <paramref name="a"/> to its needle head at <paramref name="b"/>.</summary>
        private Clip BladeBeamLine(Vector2 a, Vector2 b, float thickness, Color color, float life, int order, bool additive = true)
        { return BladeStretch("Blade beam", BladeBeamSprite, a, b, thickness, color, life, order, additive); }

        private Clip BladeSlashAt(Vector2 pos, float width, float height, float angle, Color color, float life, bool additive, int order)
        {
            var clip = Spawn("Blade slash", BladeSlashSprite, pos, width, color, life, additive, order);
            clip.scale = new Vector3(width / BladeSlashSprite.bounds.size.x, height / BladeSlashSprite.bounds.size.y, 1);
            clip.angle = angle; clip.root.transform.localScale = clip.scale; clip.root.transform.localRotation = Quaternion.Euler(0, 0, angle);
            return clip;
        }

        /// <summary>A giant painted crescent sweep bulging toward <paramref name="angle"/>: drawn in its first frames, held, then
        /// frayed from the hollow side out to the razor rim, glowing over a soft bloom of <paramref name="bloom"/> alpha (0: none)
        /// and, when <paramref name="deep"/> has alpha, a slightly smaller stroke of that deeper hue under it for body on pale
        /// skies. <paramref name="pop"/> adds a white silhouette frame the moment it is fully drawn; <paramref name="mirror"/>
        /// flips it top to bottom.</summary>
        private Clip BladeSweep(Vector2 pos, float width, float height, float angle, Color color, float bloom, Color deep, float life, int order, bool mirror, bool pop, float delay = 0)
        {
            Vector2 ahead = Direction(angle);
            var layers = new List<Clip>();
            if (deep.a > 0) layers.Add(Sheet("Blade sweep depth", BladeSweepSheet, pos - ahead * width * .03f, width * .96f, deep, life * 1.05f, order - 1, 0, false));
            var body = Sheet("Blade sweep", BladeSweepSheet, pos, width, color, life, order, 0, false); layers.Add(body);
            foreach (var clip in layers) { clip.startScale = .86f; clip.endScale = 1.05f; clip.ease = 6; clip.hold = .3f; clip.fadePower = 1.1f; BladeDelay(clip, delay); }
            if (pop)
            {
                var flash = Spawn("Blade sweep flash", crescent, pos + ahead * width * .02f, width * .98f, new Color(1, 1, 1, Mathf.Min(1, color.a)), .09f, true, order + 2);
                flash.startScale = .97f; flash.endScale = 1.04f; flash.ease = 4; flash.fadePower = 1.8f; layers.Add(flash);
                BladeDelay(flash, delay + life * .32f);
            }
            foreach (var clip in layers)
            {
                clip.angle = angle; clip.scale.y *= height / width * (mirror ? -1 : 1);
                clip.root.transform.localScale = clip.scale; clip.root.transform.localRotation = Quaternion.Euler(0, 0, angle);
            }
            if (bloom > 0)
            {
                // The bloom sits over the arc itself (the outer third of the crescent), stretched along its height.
                var halo = Spawn("Blade sweep bloom", glow, pos + ahead * width * .3f, Mathf.Abs(height) * 1.05f, Alpha(color, bloom), life * 1.3f, true, order - 3);
                halo.angle = angle + 90; halo.scale.y *= .5f; halo.root.transform.localScale = halo.scale; halo.root.transform.localRotation = Quaternion.Euler(0, 0, angle + 90);
                halo.startScale = .8f; halo.endScale = 1.1f; halo.ease = 4; halo.hold = .2f; halo.fadePower = 1.3f; BladeDelay(halo, delay);
            }
            return body;
        }

        /// <summary>A hit: the hand-drawn spark flaring, splitting into shards and scattering (plays at least a fifth of a
        /// second) over a soft bloom of <paramref name="halo"/> (none when its alpha is 0). Returns the spark.</summary>
        private Clip BladeStar(Vector2 pos, float width, Color color, Color halo, float life, int order = 132, float delay = 0)
        {
            if (halo.a > 0)
            {
                var bloom = Spawn("Blade hit bloom", glow, pos, width * 1.35f, halo, Mathf.Max(.2f, life * 1.8f), true, order - 2);
                bloom.startScale = .6f; bloom.endScale = 1.25f; bloom.ease = 3; bloom.fadePower = 1.5f; BladeDelay(bloom, delay);
            }
            var spark = SparkFlip(pos, width * 1.1f, color, life, order); BladeDelay(spark, delay);
            return spark;
        }

        /// <summary>A soft bloom stretched along a stroke from <paramref name="a"/> to <paramref name="b"/>: the glow under a
        /// beam that makes the painted stroke read as light.</summary>
        private Clip BladeBloomLine(Vector2 a, Vector2 b, float thickness, Color color, float life, int order)
        {
            Vector2 mid = (a + b) * .5f, half = (b - a) * .5f * 1.25f;
            return BladeStretch("Blade bloom", glow, mid - half, mid + half, thickness, color, life, order, true);
        }

        /// <summary>A soft contrast underlay stretched along a stroke (the core Underlay's soft dark halo, behind the actors at
        /// 29), so a white-hot beam still reads on a pale sky without any outline.</summary>
        private Clip BladeShade(Vector2 a, Vector2 b, float thickness, float alpha, float life)
        {
            Vector2 mid = (a + b) * .5f, half = (b - a) * .5f * 1.2f;
            var clip = BladeStretch("Contrast underlay", glow, mid - half, mid + half, thickness, new Color(.03f, .04f, .1f, alpha), life, 29, false);
            clip.fadeIn = .03f; clip.fadePower = 1.2f; return clip;
        }

        /// <summary>Ground cracks where a blade bit: warm brown fissures (never black), a glowing copy of <paramref name="hot"/>
        /// fading first, and a soft glow on the floor round them.</summary>
        private void BladeCrack(Vector2 ground, float width, Color hot, float life)
        {
            var dark = Decal(ground, crack, width, new Color(.34f, .2f, .09f, .5f), life, false, 25); dark.angle = Range(-10, 10);
            var lit = Decal(ground, crack, width, hot, life * .75f, true, 26); lit.angle = dark.angle; lit.fadePower = 1.8f;
            GroundGlow(ground + Vector2.up * .05f, width * 1.1f, Alpha(hot, .4f), life * .6f);
        }

        /// <summary>A crisp glint: a long and a short spindle crossed over a four-point fleck.</summary>
        private void BladeTwinkle(Vector2 pos, float size, Color color, float life, float angle, float delay)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2 half = Direction(angle + i * 90) * size * (i == 0 ? .5f : .28f);
                var ray = Line(pos - half, pos + half, size * (i == 0 ? .11f : .09f), color, life, 133);
                ray.startScale = .25f; ray.endScale = 1; ray.ease = 5; ray.fadePower = 1.4f; BladeDelay(ray, delay);
            }
            var heart = Spawn("Blade twinkle", fleck, pos, size * .32f, Color.white, life * .8f, true, 133);
            heart.startScale = .3f; heart.endScale = 1; heart.ease = 4; heart.fadePower = 1.4f; BladeDelay(heart, delay);
        }

        /// <summary>Needle sparks flung from <paramref name="pos"/> around <paramref name="angle"/> (within <paramref name="spread"/>
        /// degrees), white and coloured, a third of them straighter and faster.</summary>
        private void BladeNeedles(Vector2 pos, float angle, float spread, int count, float speed, Color color, float life, float size = .42f, float delay = 0)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                bool straight = i % 3 == 0;
                var needle = Spark(pos, Direction(angle + Range(-spread, spread) * (straight ? .45f : 1)) * speed * (straight ? Range(.9f, 1.2f) : Range(.45f, 1)),
                    straight ? Color.white : color, size * Range(.6f, 1.25f), life * Range(.6f, 1), 4, 3);
                BladeDelay(needle, delay);
            }
        }

        /// <summary>Painted smoke (warm grey, dust or pale wind) kicked from <paramref name="pos"/> toward <paramref name="dir"/>.</summary>
        private void BladeKick(Vector2 pos, Vector2 dir, float size, Color color, int count, float life, int order = 126, float delay = 0)
        {
            Vector2 main = dir.sqrMagnitude > .0001f ? dir.normalized : Vector2.up;
            for (int i = 0; i < count; i++)
            {
                Vector2 d = (main + Inside(.75f)).normalized;
                var puff = Puff(pos + d * size * Range(.05f, .35f), size * Range(.55f, 1), color, d * size * Range(1.4f, 3) + Vector2.up * size * .3f, life * Range(.75f, 1.1f), order);
                puff.startScale = .5f; puff.endScale = 1.5f; puff.fadeIn = 0; BladeDelay(puff, delay);
            }
        }

        /// <summary>White silhouettes of the dashing body strung along its path: copies of the afterimage gameplay took at the start
        /// of the dash (this frame), each a step further along, the ones behind fading first.</summary>
        private void BladeGhosts(Vector2 from, Vector2 to, int count)
        {
            Clip source = null;
            for (int i = clips.Count - 1; i >= 0 && i >= clips.Count - 48; i--)
            {
                var c = clips[i];
                if (c.age <= 0 && (c.copies != null || c.meshCopies != null) && c.root && c.root.name == "Dash afterimage") { source = c; break; }
            }
            if (source == null) return;
            Vector2 delta = to - from; float length = delta.magnitude, reach = Mathf.Clamp01((length - .9f) / Mathf.Max(.01f, length));
            Clip last = null;
            for (int i = 0; i < count; i++)
            {
                Vector2 at = source.position + delta * reach * (i + 1f) / (count + 1);
                var copy = Instantiate(source.root, source.root.transform.parent);
                copy.name = "Dash ghost"; copy.transform.position = new Vector3(at.x, at.y, 0);
                var block = new MaterialPropertyBlock(); block.SetColor("_Color", Color.white);
                var meshes = copy.GetComponentsInChildren<MeshRenderer>();
                var sprites = copy.GetComponentsInChildren<SpriteRenderer>();
                // The ghosts are drawn among the slash beams (over the blue beam, under its white needle), not behind them
                // with the actors, so the silhouettes read through the dash.
                foreach (var mesh in meshes) { mesh.SetPropertyBlock(block); mesh.sortingLayerID = 0; mesh.sortingOrder = 129; }
                foreach (var sprite in sprites) { sprite.sortingLayerID = 0; sprite.sortingOrder = 129; }
                last = new Clip { root = copy, copies = sprites, meshCopies = meshes, meshTint = block, position = at, scale = Vector3.one,
                    color = new Color(1, 1, 1, .5f), ramp = Ramp(new Color(.6f, .88f, 1), new Color(.36f, .7f, 1), new Color(.16f, .38f, 1)), hold = .2f, fadePower = 1.4f,
                    life = .16f + i * .05f, seed = sequence++ };
                clips.Add(last);
            }
            // The copy that fades last keeps the frozen meshes alive, so the first afterimage (the oldest) may fade first.
            if (last != null && source.ownedMeshes != null) { last.ownedMeshes = source.ownedMeshes; source.ownedMeshes = null; }
            source.life = Mathf.Min(source.life, .11f);
        }

        /// <summary>A squashed, tilted child plane of a zone root: anything spinning on it runs round an ellipse in perspective.</summary>
        private static Transform BladePlane(Transform root, Vector2 offset, float squash, float tilt)
        {
            var go = new GameObject("Blade storm plane"); go.transform.SetParent(root, false);
            go.transform.localPosition = offset; go.transform.localRotation = Quaternion.Euler(0, 0, tilt); go.transform.localScale = new Vector3(1, squash, 1);
            return go.transform;
        }

        private void BladeOrbit(List<Clip> layers, Transform plane, float width, int count, float spin, Color light, float alpha, float life, int order)
        {
            for (int i = 0; i < count; i++)
            {
                var sweep = Attach(plane, "Storm sweep", BladeWhirlSprite, width, Alpha(light, alpha), order);
                sweep.angle = i * 360f / count + Range(-25, 25);
                if (spin < 0) sweep.scale.x *= -1;
                sweep.spin = spin * Range(.9f, 1.1f); BladeZoneFade(sweep, life);
                layers.Add(sweep);
            }
        }

        /// <summary>A flat, squashed plane riding on <paramref name="anchor"/> (or free when there is none). Clips attached to it and
        /// spun run round an ellipse on the floor instead of tipping up. The plane is an invisible clip itself, so it is
        /// cleaned up like any other; keep its children's lives within <paramref name="life"/>.</summary>
        private Transform BladeFlatPlane(Transform anchor, Vector2 worldPos, float squash, float life)
        {
            // Without a caster the plane rides on the effects root instead, so it is never a pooled object that could
            // carry children into its next use.
            if (!anchor) anchor = transform;
            var plane = Anchored(anchor, worldPos, "Blade floor plane", glow, 1, new Color(1, 1, 1, 0), life + .05f, 20);
            Vector3 lossy = anchor.lossyScale;
            plane.scale = new Vector3(1 / Mathf.Max(.0001f, Mathf.Abs(lossy.x)), squash / Mathf.Max(.0001f, Mathf.Abs(lossy.y)), 1);
            plane.root.transform.localScale = plane.scale;
            return plane.root.transform;
        }

        private Clip BladeOnPlane(Transform plane, string name, Sprite sprite, float width, Color color, float life, int order, bool additive, float spin)
        {
            var clip = Attach(plane, name, sprite, width, color, order, additive);
            clip.life = life; clip.fadeIn = .06f; clip.hold = .75f; clip.fadePower = 1.6f; clip.spin = spin; clip.loop = false;
            return clip;
        }

        /// <summary>A painted stone chip (alpha, its own colours) of about <paramref name="size"/> units.</summary>
        private Clip BladeStone(string name, Vector2 pos, float size, float life, int order = 128)
        {
            var chip = Spawn(name, BladeRockSprites[random.Next(BladeRockSprites.Length)], pos, size, Color.white, life, false, order);
            chip.angle = Range(0, 360); return chip;
        }

        /// <summary>Painted stones flung from a blast, tumbling under gravity.</summary>
        private void BladeRubble(Vector2 pos, int count, float speed, float size, float life)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                var chunk = BladeStone("Blade rubble", pos + Inside(.3f), size * Range(.5f, 1.15f), life * Range(.7f, 1.1f));
                chunk.velocity = new Vector2(Range(-1, 1) * speed, Range(.55f, 1.2f) * speed); chunk.gravity = 18; chunk.spin = Range(-500, 500);
                chunk.hold = .55f; chunk.fadePower = .8f;
            }
        }

        /// <summary>Spindle strokes rushing in toward a point (the pull of a charge), spread over a wind-up window.</summary>
        private void BladeInward(Vector2 target, int count, float reach, Color color, float window, float thickness)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                float speed = Range(8, 12), dist = reach * Range(.75f, 1.1f), life = Mathf.Max(.08f, (dist - .5f) / speed);
                Vector2 dir = Direction(Range(0, 360)), from = target + dir * dist;
                var ray = BladeCutLine(from + dir * Range(.8f, 1.3f), from, thickness * Range(.7f, 1.2f), i % 3 == 0 ? Color.white : Alpha(color, 1), life, 129);
                ray.velocity = -dir * speed; ray.fadeIn = .03f; ray.hold = .6f; ray.fadePower = 1.5f;
                BladeDelay(ray, Range(0, Mathf.Max(0, window - life)));
            }
        }

        /// <summary>Painted crescents whirling in round a raised weapon over a soft bloom, closing as the release nears.</summary>
        private void BladeGatherArcs(Transform anchor, Vector2 at, float width, float spin, Color color, float w, float turn)
        {
            for (int i = 0; i < 2; i++)
            {
                // Two unequal arcs, a third of a turn apart and off-centre, so they whirl rather than close into a ring.
                float angle = turn + i * 135 + Range(-20, 20);
                Vector2 off = at + Direction(angle + 90) * width * (i == 0 ? .08f : -.12f);
                var arc = Anchored(anchor, off, "Gather arc", crescent, width * (i == 0 ? 1 : .66f), Alpha(color, 1), w + .04f, 126);
                arc.angle = angle; arc.spin = spin; arc.startScale = 1.2f; arc.endScale = .72f; arc.ease = 1.4f; arc.fadeIn = .05f; arc.hold = .7f; arc.fadePower = 1.6f;
            }
            var bloom = Anchored(anchor, at, "Gather bloom", glow, width * 1.25f, Alpha(color, .5f), w + .04f, 124);
            bloom.startScale = 1.15f; bloom.endScale = .8f; bloom.ease = 1.4f; bloom.fadeIn = .08f; bloom.hold = .75f; bloom.fadePower = 1.6f; bloom.pulse = .06f;
        }

        /// <summary>Flame licks of light rising round a body from the floor (behind the actors) over a soft glow, thicker and
        /// longer as the release nears; a third of them in <paramref name="deep"/>, a deeper hue of the same colour, so the
        /// aura keeps its body on a pale sky.</summary>
        private void BladeAuraLicks(Transform anchor, Vector2 pos, float floorY, float w, Color color, Color deep, float rate, float length, float spread)
        {
            var host = Anchored(anchor, pos, "Aura glow", glow, spread * 3.4f, Alpha(color, .32f), w + .02f, 28);
            host.scale.y *= 1.25f; host.root.transform.localScale = host.scale; host.fadeIn = w * .5f; host.hold = .8f; host.fadePower = 2; host.pulse = .05f;
            Emitter(host, rate, 0, (owner, p) =>
            {
                float charged = Mathf.Clamp01(owner.age / Mathf.Max(.05f, w));
                if (!Chance(.35f + .65f * charged)) return;
                float side = Chance(.5f) ? 1 : -1, life = Range(.15f, .23f);
                Vector2 at = new Vector2(p.x + side * Range(.15f, spread), floorY + Range(.05f, .9f)), rise = new Vector2(-side * Range(0, .2f), 1).normalized;
                float len = Range(.8f, 1.6f) * (.55f + .6f * charged) * length;
                float roll = Range(0, 1);
                var lick = BladeCutLine(at, at + rise * len, Range(.15f, .25f), roll < .25f ? Color.white : roll < .55f ? Alpha(deep, 1) : Alpha(color, 1), life, 29);
                lick.velocity = rise * Range(3, 5.5f); lick.fadeIn = .03f; lick.fadePower = 1.3f;
            });
        }

        /// <summary>A short crackle of jagged light.</summary>
        private void BladeZap(Vector2 at, float size, Color color, float delay)
        {
            Vector2 p = at; float heading = Range(0, 360);
            for (int i = 0; i < 3; i++)
            {
                Vector2 next = p + Direction(heading + Range(-70, 70)) * size * Range(.25f, .45f);
                var arc = Line(p, next, .1f, i == 1 ? Color.white : Alpha(color, 1), .1f, 132, 1.15f); arc.flicker = .35f; arc.fadePower = 1.2f;
                BladeDelay(arc, delay); p = next;
            }
        }

        // ---------------------------------------------------------------- Shockwave: golden slam and sword wave

        /// <summary>The weapon coming down: a razor slash bowing forward over a deep-gold stroke and a bloom, and a spark
        /// flaring where it was raised.</summary>
        private void BladeShockSwing(Vector2 pos, int f, Color light, float delay)
        {
            var depth = BladeSlashAt(pos + new Vector2(f * 1.12f, .25f), 3.6f, 1.75f, -90 * f, Alpha(BladeDeepGold, .8f), .22f, true, 129); BladeSnap(depth, delay);
            var swing = BladeSlashAt(pos + new Vector2(f * 1.16f, .25f), 3.8f, 1.9f, -90 * f, Alpha(light, 1), .2f, true, 130); BladeSnap(swing, delay);
            var bloom = BladeBloomLine(pos + new Vector2(f * 1.35f, 1.6f), pos + new Vector2(f * 1.35f, -1.1f), 1.3f, Alpha(light, .32f), .26f, 127); BladeDelay(bloom, delay);
            bloom.startScale = .8f; bloom.endScale = 1.05f; bloom.ease = 4; bloom.fadePower = 1.4f;
            BladeStar(pos + new Vector2(f * .45f, .85f), 2.1f, Alpha(Warm, 1), Alpha(light, .5f), .14f, 132, delay);
        }

        private void CastShockwave(CastInfo c)
        {
            int f = c.facing; float w = c.windup, release = Mathf.Max(0, w - .03f);
            Vector2 pos = c.pos, feet = new Vector2(pos.x, BladeGroundY(pos, 1)), gather = pos + new Vector2(f * .35f, .9f);
            Color light = Color.Lerp(BladeHotGold, c.light, .3f);
            // The slam's burst (and an awakened second wave a quarter second after it) reads this to run forward only.
            BladeNoteSlam(pos, f, w + .9f);
            // An instant echo (a recast with no wind-up): only the swing coming down; the slam follows at once.
            if (w < .2f) { BladeShockSwing(pos, f, light, release); return; }
            int grade = Grade; float lift = BladeLevel(1.2f, 1.45f);
            // The raised weapon drinks in the wind: painted crescents whirl in round it over a gold bloom, spindle strokes rush
            // in and a white-hot glint swells on the blade.
            BladeGatherArcs(c.anchor, gather, 2.4f, 520 * f, light, w, Range(0, 360));
            var star = Anchored(c.anchor, gather, "Wave charge glint", glint, 1.5f, Color.white, w + .03f, 128);
            star.startScale = .2f; star.endScale = 1.1f; star.ease = 1.3f; star.spin = 160; star.pulse = .08f; star.hold = .85f; star.fadePower = 2;
            Converge(gather, 10, 2.6f, light, w, .45f);
            BladeInward(gather, 8, 3.2f, light, w, .18f);
            // Power climbs the body: gold and deep-gold flame licks rise round the hero over a gold glow (behind the actors),
            // longer with the level.
            BladeAuraLicks(c.anchor, pos, feet.y, w, light, BladeDeepGold, 24 * lift, .9f * lift, .8f);
            // The ground answers: a gold rune ring over a glow on the floor, sparks rising, stones lifting and dust drawn in.
            var floor = BladeFlatPlane(c.anchor, feet + Vector2.up * .06f, .27f, w + .35f);
            var runeGlow = BladeOnPlane(floor, "Wave rune glow", glow, 5.2f, Alpha(light, .42f), w + .35f, 26, true, 0);
            var rune = BladeOnPlane(floor, "Wave rune", runeRing, 4.5f, Alpha(light, .9f), w + .35f, 27, true, 90 * f);
            foreach (var clip in new[] { runeGlow, rune }) { clip.startScale = .55f; clip.endScale = 1; clip.ease = 4; clip.fadeIn = .08f; }
            int rising = BladeShare(8);
            for (int i = 0; i < rising; i++)
            {
                var mote = Spark(feet + new Vector2(Range(-2.1f, 2.1f), .1f), new Vector2(Range(-.2f, .2f), Range(2.5f, 5)), i % 3 == 0 ? Color.white : light, Range(.24f, .38f), Range(.25f, .4f), -1, 1);
                mote.flicker = .3f; BladeDelay(mote, Range(0, w * .8f));
            }
            int pebbles = BladeShare(7);
            for (int i = 0; i < pebbles; i++)
            {
                float side = i % 2 == 0 ? 1 : -1, delay = Range(0, w * .35f);
                var stone = BladeStone("Rising pebble", feet + new Vector2(side * Range(.5f, 2.3f), .08f), Range(.2f, .34f), Mathf.Max(.1f, w + .1f - delay), 119);
                stone.velocity = new Vector2(-side * Range(.1f, .4f), Range(1, 2.4f)); stone.drag = 2.2f; stone.spin = Range(-160, 160);
                stone.fadeIn = .05f; stone.hold = .85f; stone.fadePower = 2;
                BladeDelay(stone, delay);
            }
            int dusts = BladeShare(6);
            for (int i = 0; i < dusts; i++)
            {
                float side = i % 2 == 0 ? 1 : -1, x = Range(1.4f, 2.6f);
                var puff = Puff(feet + new Vector2(side * x, .18f), Range(.6f, .9f), BladeDust, new Vector2(-side * x * 2.2f, Range(.1f, .5f)), w * Range(.75f, 1.05f), 118);
                puff.drag = 2.5f; puff.startScale = .8f; puff.endScale = 1.3f; puff.fadeIn = .08f;
            }
            // Release: the weapon comes down in a razor arc and the gathered star bursts.
            BladeShockSwing(pos, f, light, release);

            // Level two: a white-gold spire climbs behind the hero and a second, counter-turning pair of crescents tightens round
            // the weapon.
            if (grade < 2) return;
            float spireHeight = grade >= 3 ? 7.4f : 6.4f;
            var spireGlow = BladeBloomLine(feet, feet + Vector2.up * spireHeight, 2.1f, Alpha(light, .45f), w + .05f, 27);
            var spireDepth = BladeCutLine(feet, feet + Vector2.up * spireHeight * .97f, .95f, Alpha(BladeDeepGold, .75f), w + .05f, 28);
            var spire = BladeCutLine(feet, feet + Vector2.up * spireHeight, .62f, Alpha(Warm, 1), w + .05f, 29);
            foreach (var clip in new[] { spireGlow, spireDepth, spire }) { clip.axisStart = new Vector2(.2f, .3f); clip.axisEnd = Vector2.one; clip.ease = 1.3f; clip.fadeIn = .05f; clip.hold = .9f; clip.fadePower = 3; }
            BladeGatherArcs(c.anchor, gather, 1.9f, -760 * f, Warm, w, Range(0, 360));
            if (grade < 3) return;
            // Level three, awakened: a painted spectral greatsword gathers point-down above and behind the hero over a gold glow
            // (behind the actors); at the release it plunges into the floor (the slam draws it planted).
            Vector2 hilt = new Vector2(pos.x - f * .45f, feet.y + 4.2f);
            var aura = Anchored(c.anchor, hilt + Vector2.down * .3f, "Awakened greatsword glow", glow, 2.6f, Alpha(BladeHotGold, .5f), w + .03f, 25);
            aura.scale.y *= 2; aura.root.transform.localScale = aura.scale; aura.fadeIn = w * .55f; aura.hold = .9f; aura.fadePower = 3;
            var blade = Anchored(c.anchor, hilt, "Awakened greatsword", BladeSwordSprite, 3.9f, Alpha(BladeHotGold, 1), w + .03f, 27);
            blade.angle = -90; blade.scale.y *= 2.2f; blade.root.transform.localScale = blade.scale;
            blade.fadeIn = w * .55f; blade.startScale = .75f; blade.endScale = 1; blade.ease = 2; blade.hold = .9f; blade.fadePower = 3;
            BladeTwinkle(hilt + Vector2.up * 1.55f, 1.6f, Alpha(Warm, 1), .3f, 0, w * .4f);
        }

        /// <summary>The greatsword of an awakened slam driving into the floor behind the hero and standing there a moment.</summary>
        private void BladePlantedSword(Vector2 g, float R)
        {
            Vector2 planted = g + new Vector2(0, 1.85f);
            var aura = Spawn("Plunging greatsword glow", glow, planted + Vector2.up * 2.2f, 2.6f, Alpha(BladeHotGold, .5f), .9f, true, 25);
            var drop = Spawn("Plunging greatsword", BladeSwordSprite, planted + Vector2.up * 2.2f, 3.9f, Alpha(BladeHotGold, 1), .9f, true, 27);
            drop.angle = -90; drop.scale.y *= 2.2f; drop.root.transform.localScale = drop.scale;
            drop.root.transform.localRotation = Quaternion.Euler(0, 0, -90);
            aura.scale.y *= 2; aura.root.transform.localScale = aura.scale;
            // Down 2.2 units in 0.06 s, then it stands while the dust settles.
            foreach (var clip in new[] { aura, drop }) { clip.velocity = Vector2.down * 2.2f * 22; clip.drag = 22; clip.hold = .45f; clip.fadePower = 1.6f; }
            BladeStar(g + Vector2.up * .25f, 2.2f, Alpha(BladeAwakeGold, 1), Alpha(BladeHotGold, .5f), .16f, 29);
            BladeNeedles(g + Vector2.up * .2f, 90, 70, 7, 7, BladeAwakeGold, .4f);
            BladeKick(g + Vector2.up * .2f, Vector2.up, .55f, BladeDust, 3, .45f, 28);
            // Where it bites: a glowing crack radiating from the blade's point (the level overlay lays the seal on the reach).
            BladeCrack(g + Vector2.up * .02f, 1.8f, Alpha(BladeAwakeGold, 1), 1.1f);
        }

        /// <summary>The second wave of an awakened slam, a quarter second after the first and from the same spot: a gold-white
        /// sweep along the floor through the same reach, without a second eruption of rock. <paramref name="f"/> is the way the
        /// wave runs (0: unknown, both ways).</summary>
        private void BladeEchoWave(Vector2 pos, float R, float k, Color tint, int f)
        {
            Vector2 g = new Vector2(pos.x, BladeGroundY(pos, .6f)), hit = g + Vector2.up * (.25f + .3f * k);
            Color light = Color.Lerp(BladeAwakeGold, tint, .2f);
            // A forward wave marks only the stretch in front of the hero, [0, R]; an unknown one the whole circle.
            Vector2 reachAt = g + new Vector2(f * R * .5f, 0); float reach = f != 0 ? R * .5f : R;
            BladeStar(hit + new Vector2(f * .9f, 0), R * .6f, Alpha(Warm, 1), Alpha(light, .5f), .14f, 132);
            var echoFront = ShockFlip(hit + new Vector2(f * 1.1f, .2f), R * .9f, Alpha(light, .8f), .26f, 130); echoFront.angle = 0; echoFront.scale.x *= .45f;
            // The reach of the wave: a gold ring on the floor whose rim lands on the gameplay reach.
            GroundWave(reachAt + Vector2.up * .05f, reach * 2 / .74f, Alpha(light, 1), .42f);
            for (int s = -1; s <= 1; s += 2)
            {
                if (f != 0 && s != f)
                {
                    // Behind the hero: only a puff of dust kicked back by the swing.
                    var kick = Puff(g + new Vector2(s * .5f, .2f), Range(.6f, .85f), BladeDust, new Vector2(s * Range(.6f, 1.2f), Range(.6f, 1.2f)), Range(.45f, .6f), 118);
                    kick.endScale = 2; continue;
                }
                // A slash beam skims the floor to the reach over a gold bloom, and a seam of light is left behind it.
                var blastGlow = BladeBloomLine(g + new Vector2(s * .3f, .3f), g + new Vector2(s * R * 1.1f, .3f), 1.4f, Alpha(light, .38f), .32f, 127);
                var blast = BladeBeamLine(g + new Vector2(s * .45f, .32f), g + new Vector2(s * R, .32f), .6f, Alpha(Warm, 1), .24f, 132);
                foreach (var clip in new[] { blastGlow, blast }) { clip.axisStart = new Vector2(.3f, .5f); clip.axisEnd = Vector2.one; clip.ease = 6; clip.hold = .25f; clip.fadePower = 1.3f; }
                var seam = BladeCutLine(g + new Vector2(s * .3f, .05f), g + new Vector2(s * R * .95f, .05f), .2f, Alpha(light, 1), .6f, 26);
                seam.axisStart = new Vector2(.08f, 1); seam.axisEnd = Vector2.one; seam.ease = 5; seam.hold = .3f; seam.fadePower = 1.5f;
                int dusts = Budget(2);
                for (int i = 0; i < dusts; i++)
                {
                    float t = (i + 1f) / (dusts + 1);
                    var dust = Puff(g + new Vector2(s * R * t, .2f), Range(.6f, .9f), BladeDust, new Vector2(s * Range(.5f, 1.5f), Range(.8f, 1.8f)), Range(.5f, .7f), 118);
                    dust.endScale = 2.2f; BladeDelay(dust, t * .12f);
                    var needle = Spark(g + new Vector2(s * R * t, .3f), new Vector2(s * Range(2, 5), Range(2, 5)), i % 2 == 0 ? Color.white : light, Range(.24f, .36f), Range(.25f, .4f), 6, 2);
                    BladeDelay(needle, t * .12f);
                }
            }
        }

        private void ImpactShockwave(Vector2 pos, float radius, Color tint, int facing)
        {
            float R = Mathf.Clamp(radius, .8f, 5f), k = Mathf.Clamp(R / 3.5f, .3f, 1.2f);
            int grade = Grade;
            // Gameplay draws the slam without a facing; its wind-up noted which way the hero swung. The sword wave only runs
            // forward, so the slam's rock, fronts and reach ring do too. Slams with no note (passives, bosses) stay round.
            var note = BladeFindSlam(pos);
            int f = facing != 0 ? (facing < 0 ? -1 : 1) : note != null ? note.facing : 0;
            // An awakened slam's second wave follows a quarter second later from the same caster: a lighter gold-white echo.
            if (grade >= 3 && note != null && note.first != null && BladeRecent(note.first, .45f, pos, 2.5f)) { BladeEchoWave(pos, R, k, tint, f); return; }
            // Higher levels stand taller and brighter; the reach (spikes, fronts, rings) stays the gameplay radius.
            float lift = BladeLevel(1.2f, 1.4f);
            Vector2 g = new Vector2(pos.x, BladeGroundY(pos, .6f)), hit = g + Vector2.up * (.25f + .3f * k);
            Color light = Color.Lerp(BladeHotGold, tint, .3f);
            // The struck stretch of floor: in front of the hero, [0, R], for a noted slam; the whole circle otherwise.
            Vector2 reachAt = g + new Vector2(f * R * .5f, 0); float reach = f != 0 ? R * .5f : R;
            // Impact frame: a golden blast billowing up behind the hero where the weapon struck (behind the actors, so the hero
            // stays in view), a white-hot spark flaring over the strike, wedge lines bursting out and warm smoke thrown up.
            Vector2 strike = hit + new Vector2(f * .55f, .05f);
            Blast(strike + new Vector2(f * .35f, .25f), (1.5f + .55f * R) * Mathf.Sqrt(lift), Alpha(Color.Lerp(light, BladeDeepGold, .4f), 1), .5f, 26);
            BladeStar(strike + new Vector2(f * .6f, .1f), R * .4f + .45f, Alpha(Warm, 1), Color.clear, .14f, 133);
            var strikeFront = ShockFlip(strike + new Vector2(f * .9f, .2f), R * .85f + .8f, Alpha(light, .9f), .28f, 130); strikeFront.angle = 0; strikeFront.scale.x *= .45f;
            BladeKick(hit + Vector2.up * .2f, new Vector2(f * .5f, 1), .5f + .35f * k, BladeSoot, BladeShare(4), .5f, 27);
            // The blast skims the floor ahead (both ways when the swing is unknown): a slash beam of white-gold light over a
            // deep-gold stroke and a gold bloom, so the slam reads as striking the ground.
            for (int s = -1; s <= 1; s += 2)
            {
                if (f != 0 && s != f) continue;
                var blastGlow = BladeBloomLine(g + new Vector2(s * .3f, .32f), g + new Vector2(s * R * 1.25f, .32f), 1.7f * Mathf.Sqrt(lift), Alpha(light, .38f), .36f, 127);
                var blastDepth = BladeBeamLine(g + new Vector2(s * .5f, .3f), g + new Vector2(s * R * 1.2f, .3f), .95f * Mathf.Sqrt(lift), Alpha(BladeDeepGold, .8f), .3f, 131);
                var blast = BladeBeamLine(g + new Vector2(s * .55f, .32f), g + new Vector2(s * R * 1.15f, .32f), .68f * Mathf.Sqrt(lift), Alpha(light, 1), .26f, 132);
                foreach (var clip in new[] { blastGlow, blastDepth, blast }) { clip.axisStart = new Vector2(.35f, .5f); clip.axisEnd = Vector2.one; clip.ease = 6; clip.hold = .25f; clip.fadePower = 1.3f; }
            }
            // A geyser of light out of the strike, standing behind the hero over a tall gold bloom; level two adds a white-hot spine.
            float geyser = (R * 1.5f + .8f) * lift;
            var column = BladeBloomLine(g, g + Vector2.up * geyser, R * .9f * lift, Alpha(light, .5f), .42f, 27); column.fadePower = 1.4f;
            Pillar(g, R * .36f * lift, geyser, Alpha(light, 1), .32f, 28);
            if (grade >= 2) Pillar(g, R * .14f, (R * 1.8f + 1) * lift, Alpha(Warm, 1), .26f, 29);
            // Shock fronts on the floor, their rims on the struck stretch.
            float rimScale = f != 0 ? 2 / .74f : 2.15f;
            GroundWave(reachAt + Vector2.up * .05f, reach * rimScale, Alpha(light, 1), .45f);
            var second = GroundWave(reachAt + Vector2.up * .05f, reach * rimScale * .7f, new Color(1, 1, 1, .9f), .32f); BladeDelay(second, .06f);
            // Towering painted fronts race out along the floor tearing up dust and grit, with rock spikes erupting behind them:
            // forward only for a noted slam (behind the hero just a short stub of a front and dust), both ways otherwise.
            for (int s = -1; s <= 1; s += 2)
            {
                float side = s, gy = g.y; int emitted = 0;
                if (f != 0 && s != f)
                {
                    Vector2 backAt = g + new Vector2(side * .35f * k, .6f * k);
                    var stub = Spawn("Slam back front", crescent, backAt, 1f * k, Alpha(light, .8f), .22f, true, 126);
                    stub.scale.x *= side; stub.scale.y *= 1.3f; stub.velocity = new Vector2(side * 3.2f, 0); stub.drag = 4; stub.startScale = .5f; stub.endScale = .9f; stub.ease = 2; stub.fadePower = 1.6f;
                    for (int i = 0; i < 2; i++)
                    {
                        var kick = Puff(g + new Vector2(side * Range(.3f, .8f), .2f), Range(.55f, .85f) * (.6f + .4f * k), BladeDust, new Vector2(side * Range(.5f, 1.4f), Range(.5f, 1.3f)), Range(.45f, .65f), 118);
                        kick.endScale = 2;
                    }
                    continue;
                }
                // The front is drawn upward as it leaves and frays as it dies, over a deep-gold stroke and a gold bloom riding
                // with it; level two adds a white-gold crest riding just ahead.
                float fw = 1.55f * k, fh = 2.3f * k * Mathf.Sqrt(lift);
                Vector2 start = g + new Vector2(side * (.55f + .35f * k), fh * .42f);
                var frontDepth = Sheet("Slam front depth", BladeSweepSheet, start - new Vector2(side * .06f * k, 0), fw * .95f, Alpha(BladeDeepGold, .75f), .48f, 125, 0, false);
                var front = Sheet("Slam front", BladeSweepSheet, start, fw, Alpha(light, 1), .46f, 126, 0, false);
                var fronts = new List<Clip> { frontDepth, front };
                if (grade >= 2) fronts.Add(Sheet("Slam front gilt", BladeSweepSheet, start + new Vector2(side * .24f * k, 0), fw * .8f, Alpha(grade >= 3 ? Warm : BladeAwakeGold, 1), .38f, 128, 0, false));
                foreach (var clip in fronts)
                {
                    clip.scale.x *= side; clip.scale.y *= fh / fw; clip.root.transform.localScale = clip.scale;
                    clip.velocity = new Vector2(side * R * 3.4f, 0); clip.drag = 2.6f; clip.startScale = .55f; clip.endScale = 1.1f; clip.ease = 3; clip.hold = .2f; clip.fadePower = 1.4f;
                }
                var frontGlow = Spawn("Slam front bloom", glow, start + new Vector2(side * fw * .35f, 0), fh * 1.1f, Alpha(light, .38f), .5f, true, 123);
                frontGlow.scale.x *= .55f; frontGlow.root.transform.localScale = frontGlow.scale;
                frontGlow.velocity = new Vector2(side * R * 3.4f, 0); frontGlow.drag = 2.6f; frontGlow.startScale = .55f; frontGlow.endScale = 1.1f; frontGlow.ease = 3; frontGlow.hold = .2f; frontGlow.fadePower = 1.4f;
                Emitter(front, 20 + 16 * k, .15f, (owner, p) =>
                {
                    emitted++;
                    Vector2 at = new Vector2(p.x, gy);
                    if (emitted % 3 == 0)
                    {
                        var chip = BladeStone("Slam chip", at + Vector2.up * .1f, Range(.2f, .36f), Range(.45f, .7f));
                        chip.velocity = new Vector2(side * Range(-.5f, 2.5f), Range(3, 6.5f)); chip.gravity = 18; chip.spin = Range(-600, 600); chip.fadePower = .6f;
                    }
                    else
                    {
                        var dust = Puff(at + new Vector2(Range(-.2f, .2f), .2f), Range(.6f, 1.05f) * (.6f + .4f * k), emitted % 4 == 1 ? BladeSoot : BladeDust, new Vector2(side * Range(.2f, 1.2f), Range(.8f, 2.2f)), Range(.5f, .8f), 118);
                        dust.endScale = 2.3f;
                    }
                });
                int spikes = Mathf.Max(2, Mathf.RoundToInt(2 + 3 * k));
                for (int i = 0; i < spikes; i++)
                {
                    float t = (i + .6f) / spikes, x = side * R * .92f * t, delay = (Mathf.Exp(.7f * t) - 1) / 2.6f;
                    float h = (2.1f - .9f * t) * (.45f + .55f * k) * Range(.85f, 1.1f) * BladeLevel(1.15f, 1.3f), tiltAngle = -side * Range(6, 24);
                    Vector2 at = g + new Vector2(x, -.06f);
                    // A painted stone spire bursts up out of the floor, a gold glow flashing at its foot.
                    var stone = Spawn("Erupting rock", BladeSpireSprites[(i + random.Next(3)) % BladeSpireSprites.Length], at, h * Range(.5f, .62f), Color.white, .85f, false, 119);
                    stone.angle = tiltAngle; stone.axisStart = new Vector2(.6f, 0); stone.axisEnd = Vector2.one; stone.ease = 7; stone.hold = .62f; stone.fadePower = 2;
                    BladeDelay(stone, delay);
                    var foot = Spawn("Erupting rock glow", glow, at + Vector2.up * .2f, h * .75f, Alpha(light, .5f), .32f, true, 118);
                    foot.scale.y *= .5f; foot.root.transform.localScale = foot.scale; foot.startScale = .6f; foot.endScale = 1.1f; foot.ease = 3; foot.fadePower = 1.5f;
                    BladeDelay(foot, delay);
                    var kick = Puff(at + Vector2.up * .2f, Range(.5f, .8f) * (.6f + .4f * k), BladeDust, new Vector2(side * Range(.3f, 1), Range(.6f, 1.4f)), Range(.45f, .65f), 118);
                    BladeDelay(kick, delay);
                    // The spike crumbles as it fades: a chunk tumbles off its flank and a puff of grit slumps to the floor.
                    float crumble = delay + .58f;
                    Vector2 flank = at + Direction(90 + tiltAngle) * h * .55f;
                    var bit = BladeStone("Crumbling rock", flank + Inside(.2f), h * Range(.1f, .15f), Range(.45f, .6f));
                    bit.velocity = new Vector2(side * Range(.3f, 1.6f), Range(.5f, 2)); bit.gravity = 16; bit.spin = Range(-400, 400); bit.hold = .5f; bit.fadePower = .8f;
                    BladeDelay(bit, crumble);
                    var slump = Puff(at + Vector2.up * h * .3f, Range(.6f, .9f) * (.6f + .4f * k), BladeDust, new Vector2(side * Range(.2f, .6f), Range(-.3f, .4f)), Range(.6f, .8f), 118);
                    slump.endScale = 2.1f; BladeDelay(slump, crumble);
                }
            }
            // The scar: glowing cracks in warm brown along the struck stretch over a faint char.
            BladeCrack(reachAt + Vector2.up * .02f, reach * 2.4f, Alpha(light, 1), 1.6f);
            BladeCrack(g + new Vector2(f * R * .2f, .03f), R * (f != 0 ? 1f : 1.25f), Alpha(Warm, 1), 1.5f);
            Decal(g + new Vector2(f * R * .3f, .02f), scorch, R * (f != 0 ? 1.2f : 1.4f), new Color(.32f, .2f, .1f, .38f), 1.4f, false, 24);
            if (grade >= 3) { if (note != null) note.first = BladeMarker(g, 1.5f); BladePlantedSword(g, R); }
            // Follow-through: painted stones, billowing dust, a spray of needle sparks thrown forward and embers drifting up from
            // the scar (thrown forward with the wave when the swing is known).
            BladeRubble(hit + new Vector2(f * .8f, 0), Mathf.RoundToInt((5 + 4 * k) * BladeLevel(.85f, .7f)), 5 + 3 * k, .46f, 1.1f);
            int clouds = BladeShare(Mathf.RoundToInt(4 + 4 * k));
            for (int i = 0; i < clouds; i++)
            {
                float side = f != 0 ? (i % 3 == 2 ? -f : f) : i % 2 == 0 ? 1 : -1, throwBack = f != 0 && side != f ? .25f : 1;
                // Clouds rising on the hero billow behind the actors, so the dust never veils the body; farther ones roll in front.
                float off = side * Range(0, .6f) * R * throwBack;
                var cloud = Puff(g + new Vector2(off, .3f), R * Range(.25f, .4f), i % 3 == 1 ? BladeSoot : BladeDust,
                    new Vector2(side * Range(1, 3.5f) * k * throwBack, Range(.6f, 1.8f)), Range(.8f, 1.3f), Mathf.Abs(off) < 1.4f ? 27 : 118);
                cloud.endScale = 2.4f; cloud.drag = 2; cloud.fadeIn = .05f;
            }
            BladeNeedles(hit, f > 0 ? 55 : f < 0 ? 125 : 90, f != 0 ? 45 : 75, Mathf.RoundToInt((8 + 4 * k) * BladeLevel(.85f, .7f)), 9 + 4 * k, light, .5f, .46f);
            int embers = BladeShare(Mathf.RoundToInt(4 + 6 * k));
            for (int i = 0; i < embers; i++)
            {
                float x = f != 0 ? f * Range(-.15f, 1) * R * .85f : Range(-1, 1) * R * .7f;
                var ember = Spark(g + new Vector2(x, .2f), new Vector2(Range(-.4f, .4f), Range(1, 2.6f)), i % 3 == 0 ? Warm : light, Range(.16f, .28f), Range(.7f, 1.1f), -1, 1);
                ember.flicker = .4f; BladeDelay(ember, Range(.1f, .85f));
            }
        }

        private void ProjectileShockwave(Transform root, Color tint, bool signature)
        {
            Vector2 start = root.position, dir = root.right;
            if (dir.sqrMagnitude < .01f) dir = Vector2.right;
            dir.Normalize();
            // The root is turned to its flight, so local +y is world-down when it flies left.
            float up = dir.x < 0 ? -1 : 1;
            bool level = Mathf.Abs(dir.y) < .55f;
            Color light = Color.Lerp(BladeHotGold, tint, .3f);
            if (!signature)
            {
                var edge = Attach(root, "Sword wave edge", BladeSweepSheet[2], .85f, Alpha(light, 1), 123); edge.scale.y *= 1.5f;
                var tail = Attach(root, "Sword wave tail", BladeBeamSprite, 1.4f, Alpha(light, .8f), 121, true, new Vector2(-.6f, 0));
                tail.scale = new Vector3(1.4f / BladeBeamSprite.bounds.size.x, .22f / BladeBeamSprite.bounds.size.y, 1);
                var halo = Attach(root, "Sword wave glow", glow, 1.5f, Alpha(light, .45f), 120, true, new Vector2(.1f, 0)); halo.scale.x *= .6f;
                Bundle(root, edge, tail, halo);
                return;
            }
            // A towering painted crescent wave (gold body, white razor rim, dry-brush streaks) glowing over a gold bloom with a
            // deep-gold stroke inside it for body, trailed by frayed echoes of itself and slash beams streaking behind; a level
            // one stands on the floor. Higher levels raise a taller wave (its foot stays on the floor) with more echoes; level
            // three gilds its crest.
            int grade = Grade; float lift = BladeLevel(1.13f, 1.27f);
            float h = (level ? 3.3f : 2.6f) * lift; Vector2 mid = new Vector2(0, level ? (h * .5f - 1.1f) * up : 0);
            const float bw = 3f;
            var bloom = Attach(root, "Sword wave bloom", glow, h * 1.2f, Alpha(light, .5f), 119, true, mid + new Vector2(.35f, 0)); bloom.scale.x *= .6f; bloom.pulse = .05f;
            var depth = Attach(root, "Sword wave depth", BladeSweepSheet[2], bw * .95f, Alpha(BladeDeepGold, .7f), 122, true, mid + new Vector2(-.12f, 0)); depth.scale.y *= h * .95f / (bw * .95f);
            var body = Attach(root, "Sword wave", BladeSweepSheet[2], bw, Alpha(light, 1), 123, true, mid); body.scale.y *= h / bw; body.pulse = .03f;
            var echo = Attach(root, "Sword wave echo", BladeSweepSheet[3], bw * .9f, Alpha(light, .6f), 121, true, mid + new Vector2(-1f, 0)); echo.scale.y *= h * .9f / (bw * .9f);
            var extra = new List<Clip> { bloom, depth, echo };
            if (grade >= 2)
            {
                var echo2 = Attach(root, "Sword wave second echo", BladeSweepSheet[4], bw * .8f, Alpha(light, .45f), 121, true, mid + new Vector2(-1.85f, 0)); echo2.scale.y *= h * .8f / (bw * .8f);
                extra.Add(echo2);
            }
            if (grade >= 3)
            {
                var gilt = Attach(root, "Sword wave gilt crest", BladeSweepSheet[2], bw * .78f, Alpha(Warm, 1), 124, true, mid + new Vector2(.42f, 0)); gilt.scale.y *= h * .98f / (bw * .78f); gilt.pulse = .05f;
                extra.Add(gilt);
            }
            // The razor: a thin white arc bowing forward just ahead of the crest.
            var razor = Attach(root, "Sword wave edge", BladeSlashSprite, h, Color.white, 125, true, mid + new Vector2(.9f, 0));
            razor.scale = new Vector3(h * 1.02f / BladeSlashSprite.bounds.size.x, 1.5f / BladeSlashSprite.bounds.size.y, 1); razor.angle = -90;
            extra.Add(razor);
            // Speed streaks: slash beams of different lengths and heights trailing the wave.
            for (int i = 0; i < 3; i++)
            {
                float y = (i - 1) * h * .3f + Range(-.12f, .12f), length = Range(2.2f, 3.4f) * (i == 1 ? 1.2f : 1);
                var wake = Attach(root, "Sword wave wake", BladeBeamSprite, length, i == 1 ? Color.white : Alpha(light, .9f), 120, true, mid + new Vector2(-.4f - length * .5f, y));
                wake.scale = new Vector3(length / BladeBeamSprite.bounds.size.x, (i == 1 ? .26f : .38f) / BladeBeamSprite.bounds.size.y, 1);
                extra.Add(wake);
            }
            float groundY = BladeGroundY(start, .95f); int emitted = 0;
            Emitter(body, 46, .1f, (owner, p) =>
            {
                emitted++;
                if (level)
                {
                    // Tearing the floor under the wave: dust and warm grit thrown up and back, painted stone chips, needle sparks off the
                    // crest and a scored furrow.
                    Vector2 g = new Vector2(p.x + dir.x * Range(-.1f, .6f), groundY);
                    switch (emitted % 4)
                    {
                        case 0:
                            var dust = Puff(g + new Vector2(Range(-.2f, .2f), .22f), Range(.7f, 1.15f), emitted % 8 == 0 ? BladeSoot : BladeDust, new Vector2(-dir.x * Range(.5f, 2), Range(1.4f, 3)), Range(.45f, .7f), 118);
                            dust.endScale = 2.3f; break;
                        case 1:
                            var chip = BladeStone("Wave grit", g + Vector2.up * .1f, Range(.2f, .34f), Range(.5f, .7f));
                            chip.velocity = new Vector2(dir.x * Range(-1.5f, 2.5f), Range(3.5f, 7)); chip.gravity = 18; chip.spin = Range(-600, 600); chip.fadePower = .6f; break;
                        case 2:
                            Spark(new Vector2(p.x, groundY + Range(.3f, 3)), new Vector2(-dir.x * Range(3, 7), Range(-1, 2)), emitted % 8 == 2 ? Color.white : light, Range(.26f, .4f), Range(.22f, .34f), 3, 2); break;
                        default:
                            Decal(g + Vector2.up * .02f, scorch, Range(.7f, 1f), new Color(.32f, .2f, .1f, .36f), .8f, false, 24);
                            BladeCutLine(g + new Vector2(-dir.x * .5f, .04f), g + new Vector2(dir.x * .4f, .04f), .16f, Alpha(light, 1), .4f, 26); break;
                    }
                }
                else Spark(p + Inside(.9f), -dir * Range(3, 7) + Inside(1.5f), emitted % 2 == 0 ? Color.white : light, Range(.24f, .38f), Range(.2f, .32f), 2, 2);
            });
            Bundle(root, body, extra.ToArray());
            // Launch: a spark flaring where the wave leaves, needle sparks and a puff of dust at the feet.
            BladeStar(start + dir * .5f, 1.6f, Alpha(Warm, 1), Alpha(light, .5f), .14f);
            BladeNeedles(start + dir * .5f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, 35, 5, 7, light, .3f);
            if (level) for (int i = 0; i < Budget(3); i++)
                    Puff(new Vector2(start.x + Range(-.3f, .3f), groundY + .15f), Range(.5f, .8f), BladeDust, new Vector2(-dir.x * Range(1, 3), Range(.4f, 1.2f)), Range(.4f, .6f), 118);
            // The wave pierces: every body it passes through is cut (a crescent slash through it), and at the end of its reach it
            // breaks apart into gold strokes and dust instead of vanishing (unless a last body stopped it).
            float rise = level ? h * .5f - 1.1f : 0;
            ArcheryPassWatch(root, .16f, .08f, (at, d) => BladeWaveThrough(at, d, light, grade));
            ArcheryWatch(root, 4.5f, last =>
            {
                if (BladeRecent(BladeWaveHit, .15f, last, 1.6f)) return;
                BladeWaveBreak(last + Vector2.up * rise, dir, light, h, level ? groundY : float.NaN, grade);
            });
        }

        /// <summary>The sword wave passing through a body it pierces: a gold crescent cut across it, a flaring spark and needle
        /// sparks thrown on along the flight. Light (about ten clips): a wave crosses several bodies.</summary>
        private void BladeWaveThrough(Vector2 at, Vector2 d, Color light, int grade)
        {
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, s = BladeLevel(1.1f, 1.2f);
            var cut = Crescent(at + d * .3f, 1.9f * s, Alpha(light, 1), .2f, angle, 131);
            cut.scale.y *= 1.35f; cut.velocity = d * 3; cut.drag = 5; cut.startScale = .8f; cut.ease = 6; cut.hold = .25f;
            BladeStar(at, 1.3f, grade >= 3 ? Alpha(BladeAwakeGold, 1) : Color.white, Alpha(light, .45f), .11f);
            var through = BladeCutLine(at - d * .2f, at + d * 1.7f, .16f, Color.white, .14f, 131); through.velocity = d * 6; through.startScale = .3f; through.endScale = 1; through.ease = 4;
            BladeNeedles(at, angle, 45, 4, 8, light, .28f, .4f);
        }

        /// <summary>A sword wave reaching the end of its flight: the crescent bursts forward and frays apart into gold strokes,
        /// needle sparks and (for a wave running along the floor) a last kick of dust and grit. <paramref name="ground"/> is NaN for a
        /// wave in the air.</summary>
        private void BladeWaveBreak(Vector2 center, Vector2 d, Color light, float h, float ground, int grade)
        {
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var ghost = Sheet("Wave break", BladeSweepFray, center + d * .25f, 2.3f, Alpha(grade >= 3 ? Warm : light, 1), .3f, 127, 0, false);
            ghost.angle = angle; ghost.scale.y *= h * .95f / 2.3f; ghost.root.transform.localScale = ghost.scale;
            ghost.velocity = d * 5; ghost.drag = 3; ghost.startScale = 1; ghost.endScale = 1.3f; ghost.axisEnd = new Vector2(1.5f, .6f); ghost.ease = 2; ghost.fadePower = 1.2f;
            var bloom = Spawn("Wave break bloom", glow, center + d * .4f, h * 1.1f, Alpha(light, .45f), .34f, true, 125); bloom.scale.x *= .6f; bloom.angle = angle;
            bloom.root.transform.localScale = bloom.scale; bloom.velocity = d * 5; bloom.drag = 3; bloom.endScale = 1.3f; bloom.ease = 2; bloom.fadePower = 1.3f;
            var burst = ShockFlip(center + d * .5f, 1.9f, Alpha(light, .9f), .26f, 129); burst.scale.x *= .45f; burst.angle = angle;
            BladeStar(center + d * .5f, 1.5f, Alpha(Warm, 1), Color.clear, .14f);
            int sparks = Budget(6);
            for (int i = 0; i < sparks; i++)
                Spark(center + new Vector2(0, Range(-.45f, .45f) * h), Direction(angle + Range(-40, 40)) * Range(4, 8), i % 3 == 0 ? Color.white : light, Range(.24f, .38f), Range(.25f, .4f), 4, 2.2f);
            for (int i = 0; i < 3; i++)
            {
                Vector2 from = center + new Vector2(0, (i - 1) * h * .3f);
                var streakOut = BladeCutLine(from, from + d * Range(1.2f, 2), .2f, i == 1 ? Color.white : Alpha(light, 1), .22f, 128);
                streakOut.velocity = d * 7; streakOut.fadePower = 1.4f;
            }
            if (float.IsNaN(ground)) return;
            for (int i = 0, n = Budget(3); i < n; i++)
            {
                var dust = Puff(new Vector2(center.x + Range(-.3f, .5f), ground + .2f), Range(.65f, 1f), i == 0 ? BladeSoot : BladeDust, d * Range(.8f, 2.2f) + Vector2.up * Range(.6f, 1.4f), Range(.55f, .8f), 118);
                dust.endScale = 2.3f;
            }
            BladeRubble(new Vector2(center.x, ground + .25f), 3, 4, .34f, .7f);
        }

        private void HitShockwave(Vector2 pos, Color tint, bool signature, Vector2 direction)
        {
            Color light = Color.Lerp(BladeHotGold, tint, .3f);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (!signature)
            {
                BladeStar(pos, .95f, Alpha(Warm, 1), Alpha(light, .4f), .1f);
                var arc = Crescent(pos + direction * .15f, 1.1f, Alpha(light, 1), .16f, angle, 131); arc.ease = 6; arc.hold = .25f;
                BladeNeedles(pos, angle, 55, 5, 5, light, .2f, .34f);
                return;
            }
            // A gold spark flaring over the body with a white-hot one at its heart and a shock ring, wedge lines, a crescent cut
            // through it, needle sparks thrown on, painted stones and a kick of warm dust behind.
            var star = BladeStar(pos, 2.2f, Alpha(light, 1), Alpha(light, .55f), .18f); BladeWaveHit = star;
            BladeStar(pos + direction * .1f, 1.2f, Color.white, Color.clear, .1f, 133);
            ShockFlip(pos, 2.4f, Alpha(light, .85f), .26f, 130);
            ImpactLines(pos, 3.3f, Alpha(Warm, .85f), .2f);
            var cut = Crescent(pos + direction * .25f, 2.4f, Alpha(light, 1), .24f, angle, 131); cut.scale.y *= 1.4f; cut.ease = 6; cut.hold = .25f;
            BladeNeedles(pos, angle, 65, 12, 10, light, .4f, .44f);
            BladeRubble(pos, 5, 5, .4f, .8f);
            BladeKick(pos, direction + Vector2.up * .5f, .5f, BladeDust, 2, .5f);
            int dusts = Budget(3);
            for (int i = 0; i < dusts; i++) Puff(pos + Inside(.4f), Range(.5f, .8f), BladeDust, direction * Range(.5f, 2) + Vector2.up * Range(.4f, 1.2f), Range(.5f, .8f), 118);
        }

        private void TrailShockwave(Vector2 from, Vector2 to, Color tint)
        {
            Vector2 delta = to - from; float length = delta.magnitude; if (length < .05f) return;
            Vector2 d = delta / length; float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Color light = Color.Lerp(BladeHotGold, tint, .35f);
            const float sweep = .16f;
            // A painted crescent wave races down the line, scoring a slash beam behind it.
            var wave = Spawn("Sweep wave", BladeSweepSheet[2], from, 1.5f, Alpha(light, 1), sweep + .08f, true, 128);
            wave.angle = angle; wave.scale.y *= 1.5f; wave.velocity = d * length / sweep; wave.hold = .6f; wave.fadePower = 2;
            var bloom = BladeBloomLine(from - d * .2f, to + d * .3f, 1.3f, Alpha(light, .45f), .34f, 125);
            var cut = BladeBeamLine(from, to + d * .2f, .45f, Alpha(light, 1), .28f, 127);
            foreach (var clip in new[] { bloom, cut }) { clip.axisStart = new Vector2(.2f, .6f); clip.axisEnd = Vector2.one; clip.ease = 4; clip.hold = .3f; clip.fadePower = 1.4f; }
            // Dust and grit kicked up along the floor, needle sparks thrown forward as it passes.
            float gy = BladeGroundY(from, .8f);
            int count = Budget(Mathf.Clamp(Mathf.RoundToInt(length * 1.4f), 3, 9));
            for (int i = 0; i < count; i++)
            {
                float t = (i + .5f) / count;
                var dust = Puff(new Vector2(Mathf.Lerp(from.x, to.x, t), gy + .15f), Range(.5f, .8f), BladeDust, new Vector2(d.x * Range(.6f, 1.6f), Range(.8f, 1.8f)), Range(.45f, .7f), 118);
                BladeDelay(dust, t * sweep);
                var needle = Spark(Vector2.Lerp(from, to, t) + Vector2.up * Range(-.5f, .6f), d * Range(3, 7) + Vector2.up * Range(-1, 2), i % 2 == 0 ? Color.white : light, Range(.22f, .36f), Range(.2f, .35f), 4, 2.5f);
                BladeDelay(needle, t * sweep);
            }
            BladeStar(to, 1.5f, Alpha(Warm, 1), Alpha(light, .45f), .14f, 132, sweep);
        }

        // ---------------------------------------------------------------- BladeStorm: a whirlwind of swords

        private void CastBladeStorm(CastInfo c)
        {
            float w = c.windup, release = Mathf.Max(0, w - .03f);
            Vector2 pos = c.pos, feet = new Vector2(pos.x, BladeGroundY(pos, 1)), hub = pos + Vector2.up * .35f;
            Color light = Color.Lerp(BladeStormGold, c.light, .25f);
            if (w < .2f)
            {
                // An instant echo: one quick painted whirl round the body; the storm follows at once.
                var quick = Spawn("Storm quick whirl", BladeWhirlSprite, hub, 3.2f, Alpha(light, 1), w + .12f, true, 124);
                quick.spin = 1100; quick.startScale = .5f; quick.endScale = 1.05f; quick.ease = 2; quick.fadeIn = .03f; quick.fadePower = 1.5f;
                var quickGlow = Spawn("Storm quick glow", glow, hub, 3.4f, Alpha(light, .4f), w + .14f, true, 28); quickGlow.startScale = .5f; quickGlow.ease = 2; quickGlow.fadePower = 1.5f;
                BladeStar(hub + Vector2.up * .5f, 2.2f, Alpha(Warm, 1), Alpha(light, .5f), .14f, 29, release);
                return;
            }
            // Spectral swords flash into being one after another, tips outward like the spokes of a wheel, and circle the caster,
            // closing in toward the body; at the release the wheel bursts and every sword flies outward.
            // Higher levels summon more swords into the wheel (8, 10, 12).
            const float orbitSpeed = 400, ringStart = 2.4f, ringEnd = 1.35f;
            int grade = Grade, swords = grade >= 3 ? 12 : grade == 2 ? 10 : 8;
            for (int i = 0; i < swords; i++)
            {
                float delay = Mathf.Min(i * .24f / swords, w * .4f), life = Mathf.Max(.08f, release - delay + .02f), start = i * 360f / swords + 15;
                var sword = Spawn("Phantom sword", BladeSwordSprite, hub, 2.2f, Alpha(light, 1), life, true, 127);
                sword.orbitCenter = hub; sword.orbitAngle = start; sword.orbit = orbitSpeed; sword.orbitRadius = ringStart; sword.orbitRadiusEnd = ringEnd; sword.orbitPower = .7f;
                sword.spin = orbitSpeed; sword.fadeIn = .05f; sword.hold = .9f; sword.fadePower = 2;
                sword.position = hub + Direction(start) * ringStart; sword.root.transform.position = sword.position;
                sword.angle = start; sword.root.transform.localRotation = Quaternion.Euler(0, 0, start);
                sword.scale.y *= 1.8f; sword.startScale = .55f; sword.endScale = 1; sword.ease = 3; sword.root.transform.localScale = sword.scale;
                BladeDelay(sword, delay);
                var spark = Spawn("Sword spark", fleck, hub + Direction(start) * ringStart, .8f, Color.white, .14f, true, 131);
                spark.startScale = .3f; spark.endScale = 1; spark.ease = 4; spark.fadePower = 1.4f; BladeDelay(spark, delay);
                // Where this sword sits when the wheel bursts: it shoots straight out from there.
                float at = start + (release - delay) * orbitSpeed;
                Vector2 from = hub + Direction(at) * ringEnd;
                var flung = Spawn("Flung sword", BladeSwordSprite, from, 2.3f, Alpha(light, 1), .24f, true, 128);
                flung.angle = at; flung.velocity = Direction(at) * 17; flung.drag = 4; flung.scale.y *= 1.8f; flung.root.transform.localScale = flung.scale;
                flung.root.transform.localRotation = Quaternion.Euler(0, 0, at); flung.stretch = .03f; flung.fadePower = 1.6f;
                BladeDelay(flung, release);
            }
            // The storm forming: a painted whirl spinning up round the body over a deep-gold one turning just inside it, and a
            // gold glow swelling behind the hero.
            var whirlDepth = Spawn("Storm forming depth", BladeWhirlSprite, hub, 2.9f, Alpha(BladeDeepGold, .7f), w + .04f, true, 123);
            var whirl = Spawn("Storm forming", BladeWhirlSprite, hub, 3.1f, Alpha(light, .8f), w + .04f, true, 124);
            foreach (var clip in new[] { whirlDepth, whirl }) { clip.spin = 900; clip.startScale = .45f; clip.endScale = 1; clip.ease = 2; clip.fadeIn = .1f; clip.hold = .8f; clip.fadePower = 2; }
            whirlDepth.angle = 150;
            // Gold flame licks climb round the body over a glow behind the actors, taller with the level.
            float lift = BladeLevel(1.15f, 1.3f);
            BladeAuraLicks(c.anchor, pos, feet.y, w, light, BladeDeepGold, 16 * lift, .8f * lift, .9f);
            if (grade >= 3)
            {
                // Awakened: the wind is already being dragged in from far out, and a gold-white crown turns over the head.
                BladeInward(hub + Vector2.down * .3f, 7, 4.8f, Warm, w, .14f);
                var crownPlane = BladeFlatPlane(c.anchor, hub + Vector2.up * 2.1f, .3f, w + .1f);
                var crownGlow = BladeOnPlane(crownPlane, "Storm crown glow", glow, 3.1f, Alpha(BladeAwakeGold, .45f), w + .1f, 27, true, 0);
                var crown = BladeOnPlane(crownPlane, "Storm crown", runeRing, 2.6f, Alpha(BladeAwakeGold, .95f), w + .1f, 128, true, 300);
                foreach (var clip in new[] { crownGlow, crown }) { clip.startScale = .3f; clip.endScale = 1; clip.ease = 3; }
            }
            var floor = BladeFlatPlane(c.anchor, feet + Vector2.up * .06f, .26f, w + .3f);
            var runeGlow = BladeOnPlane(floor, "Storm rune glow", glow, 4.6f, Alpha(light, .42f), w + .3f, 25, true, 0);
            var rune = BladeOnPlane(floor, "Storm rune", runeRing, 3.9f, Alpha(light, .85f), w + .3f, 27, true, 220);
            var floorWhirl = BladeOnPlane(floor, "Storm cast whirl", BladeWhirlSprite, 4.4f, Alpha(light, .85f), w + .1f, 26, true, 600);
            foreach (var clip in new[] { runeGlow, rune, floorWhirl }) { clip.startScale = .5f; clip.endScale = 1; clip.ease = 3; }
            Converge(hub, 10, 2.2f, light, w, .6f);
            int dusts = Budget(5);
            for (int i = 0; i < dusts; i++)
            {
                float side = i % 2 == 0 ? 1 : -1, x = Range(1.2f, 2.2f);
                var puff = Puff(feet + new Vector2(side * x, .18f), Range(.55f, .85f), BladeDust, new Vector2(-side * x * 2, Range(.4f, 1)), w * Range(.75f, 1.05f), 118);
                puff.drag = 2.5f; puff.spin = 260 * side;
            }
            // Release: a shock ring and a spark flaring as the storm breaks out.
            var burstRing = Wave(hub, 4.8f, Alpha(light, .85f), .26f, 3); BladeDelay(burstRing, release);
            BladeStar(hub + Vector2.up * .5f, 2.6f, Alpha(Warm, 1), Alpha(light, .5f), .15f, 29, release);
        }

        private void ImpactBladeStorm(Vector2 pos, float radius, Color tint, int facing)
        {
            float R = Mathf.Clamp(radius, .9f, 4.5f), k = Mathf.Clamp(R / 3, .35f, 1.4f);
            Color light = Color.Lerp(BladeStormGold, tint, .25f);
            Vector2 center = pos + Vector2.up * .35f;
            // Each turn of the storm: a painted crescent sweeps round the body on the floor plane (so it reads as circling the
            // hero), and a big painted sweep arcs up on one side. Higher levels' zones already stack more light round the hero,
            // so each turn's own light steps down a little to keep the body readable.
            bool clockwise = Chance(.5f); float start = Range(0, 360), calm = BladeLevel(.85f, .75f);
            var swing = Spawn("Storm swing", BladeWhirlSprite, center + Vector2.down * .2f, R * 2.2f, Alpha(light, .85f * calm), .2f, true, 128);
            LayFlat(swing, .42f);
            if (clockwise) swing.scale.x *= -1;
            swing.angle = start; swing.spin = clockwise ? -1300 : 1300; swing.startScale = .8f; swing.endScale = 1.05f; swing.ease = 4; swing.hold = .2f; swing.fadePower = 1.3f;
            float crossSide = facing != 0 ? facing : (Chance(.5f) ? 1 : -1);
            float sweepAngle = (crossSide > 0 ? 0 : 180) + Range(-25, 25);
            BladeSweep(center + new Vector2(crossSide * R * .18f, .1f), R * 1.15f, R * 1.25f, sweepAngle, Alpha(light, .9f * calm), .3f * calm, Alpha(BladeDeepGold, .6f * calm), .22f, 129, Chance(.5f), false);
            // Curved cuts snap open off-centre in an X, then a flat sweep crosses the whole area; each is tipped with a fleck.
            Vector2 cross = center + new Vector2(crossSide * Range(.3f, .5f) * R, Range(-.1f, .3f));
            float tilt = Range(22, 40);
            for (int i = 0; i < 3; i++)
            {
                float angle = (i == 0 ? tilt : i == 1 ? -tilt : Range(-8, 8)) + (Chance(.5f) ? 180 : 0);
                float delay = .02f + i * .05f, width = 2 * R * (i == 2 ? 1.12f : .95f), height = width * .5f;
                Vector2 at = (i == 2 ? center + Vector2.down * .5f : cross) + Inside(.12f * R);
                var cut = BladeSlashAt(at, width, height, angle, Alpha(light, i == 2 ? .85f : 1), .22f, true, 130);
                BladeSnap(cut, delay);
                var tip = Spawn("Storm cut tip", fleck, at + Direction(angle) * width * .4f * (Chance(.5f) ? 1 : -1), .6f + .25f * k, Color.white, .14f, true, 131);
                tip.startScale = .3f; tip.ease = 4; tip.fadePower = 1.4f; BladeDelay(tip, delay + .03f);
            }
            ShockFlip(cross, R * 1.1f, Alpha(light, .6f * calm), .22f, 129);
            // A flurry of thin cuts flickers through the storm.
            int cuts = Budget(3);
            for (int i = 0; i < cuts; i++)
            {
                Vector2 mid = center + Inside(R * .7f); float a = Range(0, 180), len = R * Range(.5f, .9f);
                Vector2 half = Direction(a) * len * .5f;
                var thin = BladeCutLine(mid - half, mid + half, .2f, Color.white, .14f, 130); thin.fadePower = 1.5f; BladeDelay(thin, .03f + i * .045f);
            }
            // Needle sparks sprayed along the slashes.
            int sparks = BladeShare(Mathf.RoundToInt(7 + 5 * k));
            for (int i = 0; i < sparks; i++)
            {
                float a = (i % 2 == 0 ? tilt : -tilt) + (Chance(.5f) ? 180 : 0) + Range(-12, 12);
                Spark(cross + Inside(.3f), Direction(a) * Range(5, 11) * (.6f + .4f * k), i % 3 == 0 ? Color.white : light, Range(.24f, .4f), Range(.2f, .36f), 5, 2.4f);
            }
            // The rim of the storm bites on both sides: a short razor slash and a flaring spark where enemies stand, and a ring on
            // the floor that marks the reach of every turn.
            float floorY = BladeGroundY(pos, 1.1f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 bite = new Vector2(center.x + s * R * Range(.72f, .92f), floorY + Range(.6f, 1.3f));
                float angle = Range(-35, 35) + (s < 0 ? 180 : 0), delay = Range(.03f, .1f), size = .6f + .4f * k;
                var biteLight = BladeSlashAt(bite, 1.9f * size, 1f * size, angle, Alpha(Color.Lerp(light, Color.white, .3f), 1), .18f, true, 130); BladeSnap(biteLight, delay);
                BladeStar(bite, 1.15f * size, Alpha(Warm, 1), Alpha(light, .45f), .12f, 132, delay + .02f);
                BladeNeedles(bite, angle, 35, 3, 7 * size, light, .25f, .38f, delay + .02f);
            }
            // The ring's rim lands exactly on the storm's reach.
            var reach = GroundWave(new Vector2(center.x, floorY + .05f), R * 2 / .74f, Alpha(light, .6f), .3f); reach.startScale = .55f;
            // (The zone's own counter-turning sword ring carries level two between the turns, so a turn adds no second sweep.)
            if (Grade < 3) return;
            // Level three: the reach flashes gold-white on every turn.
            var rim = GroundWave(new Vector2(center.x, floorY + .05f), R * 2 / .74f, Alpha(Warm, .55f), .22f); rim.startScale = .85f; rim.ease = 2; BladeDelay(rim, .05f);
        }

        private void ZoneBladeStorm(Transform root, float radius, float life)
        {
            float R = Mathf.Clamp(radius, 1.2f, 4.5f);
            Color light = BladeStormGold, pale = Color.Lerp(light, Color.white, .45f);
            var layers = new List<Clip>();
            var heart = Attach(root, "Blade storm heart", glow, .1f, new Color(0, 0, 0, 0), 121, true, new Vector2(0, .35f));
            BladeZoneFade(heart, life);
            // Outer orbit: painted sweeps racing round a flat ellipse at the hips; a tilted counter-rotating pair inside it.
            // Higher levels add layers (a counter ring of swords, a taller cyclone, a crown), so these sweeps dim to make room:
            // the new layers replace light rather than pile more of it on the hero.
            int grade = Grade; float calm = BladeLevel(.75f, .65f);
            BladeOrbit(layers, BladePlane(root, new Vector2(0, .32f), .3f, 0), R * 2, 2, 820, light, .95f * calm, life, 124);
            // A deep-gold pair turning just inside the outer sweeps gives the whirl body and depth on a pale sky.
            BladeOrbit(layers, BladePlane(root, new Vector2(0, .26f), .3f, -4), R * 1.8f, 2, 900, BladeDeepGold, .6f * calm, life, 123);
            BladeOrbit(layers, BladePlane(root, new Vector2(0, .55f), .46f, 15), R * 1.45f, 2, -1000, light, .85f * calm, life, 125);
            // The whirlwind climbs: narrower sweeps stacked above the head give the storm a cyclone's height.
            BladeOrbit(layers, BladePlane(root, new Vector2(0, 1.75f), .32f, -6), R * 1.2f, 1, 1250, light, .8f * calm, life, 123);
            BladeOrbit(layers, BladePlane(root, new Vector2(0, 2.75f), .36f, 8), R * .8f, 1, -1500, light, .65f * calm, life, 122);
            // Inner orbit: a ring of painted spectral swords circling the body.
            var swordPlane = BladePlane(root, new Vector2(0, .42f), .62f, -10);
            var swords = Attach(swordPlane, "Storm swords", BladeRingSprite, R * 1.5f, Alpha(pale, 1), 126); swords.spin = 600;
            // The storm's light: a gold glow behind the hero (behind the actors) and a soft glow lying on the floor, a vortex of
            // painted wind turning on the floor, pale sand skidding round the feet and pale wind streaks higher up.
            var aura = Attach(root, "Storm glow", glow, R * 2.6f, Alpha(light, .28f), 28, true, new Vector2(0, .5f)); aura.scale.y *= .8f; aura.pulse = .05f;
            var floor = BladePlane(root, new Vector2(0, -.5f), .26f, 0);
            var floorGlow = Attach(floor, "Storm floor glow", glow, R * 2.8f, Alpha(light, .4f), 26); floorGlow.pulse = .06f;
            var vortex = Attach(floor, "Storm vortex", swirl, R * 2.3f, Alpha(light, .75f), 27); vortex.spin = 700;
            var dust = Attach(floor, "Storm dust ring", BladeWhirlSprite, R * 2.5f, new Color(.9f, .8f, .64f, .5f), 113, false); dust.spin = 560; dust.angle = 40;
            var dust2 = Attach(floor, "Storm dust ring", BladeWhirlSprite, R * 2.1f, new Color(.9f, .8f, .64f, .4f), 113, false); dust2.spin = 640; dust2.angle = 220;
            var wind = Attach(BladePlane(root, new Vector2(0, .75f), .4f, 7), "Storm wind", BladeWhirlSprite, R * 2.3f, Alpha(pale, .3f), 123); wind.spin = 1300;
            foreach (var clip in new[] { swords, aura, floorGlow, vortex, dust, dust2, wind }) { BladeZoneFade(clip, life); layers.Add(clip); }
            // Needle sparks flung off the rim, flecks along it, gusts spiralling up and dust skidding round the feet.
            int tick = 0;
            Emitter(heart, 42, 0, (owner, p) =>
            {
                tick++;
                float phi = Range(0, 360) * Mathf.Deg2Rad, cos = Mathf.Cos(phi), sin = Mathf.Sin(phi);
                Vector2 on = p + new Vector2(cos * R, sin * R * .3f - .03f), tangent = new Vector2(-sin, cos * .3f).normalized;
                if (tick % 6 == 0)
                    Puff(new Vector2(on.x, p.y - .82f), Range(.5f, .85f), tick % 12 == 0 ? BladeSoot : BladeDust, tangent * Range(1.5f, 3) + Vector2.up * Range(.3f, .9f), Range(.45f, .7f), 118);
                else if (tick % 5 == 0)
                {
                    // A gust spiralling up the cyclone wall.
                    Vector2 wall = p + new Vector2(cos * R * .55f, -.5f + sin * .15f), rise = (tangent * 3.5f + Vector2.up * 6).normalized;
                    var gust = BladeCutLine(wall, wall + rise * Range(.9f, 1.5f), Range(.1f, .16f), tick % 2 == 0 ? Color.white : Alpha(light, 1), Range(.22f, .32f), 124);
                    gust.velocity = rise * Range(5, 8) + tangent * 2; gust.fadeIn = .03f; gust.fadePower = 1.3f;
                }
                else if (tick % 4 == 0)
                {
                    var mote = Spawn("Storm fleck", fleck, on, Range(.4f, .65f), Color.white, .14f, true, 131);
                    mote.angle = Range(-20, 20); mote.startScale = .3f; mote.endScale = 1; mote.ease = 4; mote.fadePower = 1.4f;
                }
                else Spark(on, tangent * Range(6, 11) + (on - p).normalized * 1.5f, tick % 3 == 0 ? Color.white : light, Range(.26f, .4f), Range(.18f, .3f), 2, 2.4f);
            });
            if (grade >= 2)
            {
                // Level two: a second ring of swords turns the other way higher on the body and the cyclone climbs taller.
                var plane2 = BladePlane(root, new Vector2(0, 1f), .5f, 12);
                var ring2 = Attach(plane2, "Storm counter swords", BladeRingSprite, R * 1.15f, Alpha(Color.Lerp(pale, Warm, .5f), .85f), 126);
                ring2.scale.x *= -1; ring2.spin = -760; ring2.angle = 50; BladeZoneFade(ring2, life); layers.Add(ring2);
                BladeOrbit(layers, BladePlane(root, new Vector2(0, 3.65f), .4f, -10), R * .6f, 1, 1800, pale, .45f, life, 121);
            }
            if (grade >= 3)
            {
                // Level three, awakened: a gold-white crown turns over the cyclone, and wind streaks and dust rush in along the
                // floor from as far as the storm drags enemies (1.6 times its reach) toward the caster.
                var crownPlane = BladePlane(root, new Vector2(0, 3.2f), .3f, 0);
                var crownGlow = Attach(crownPlane, "Storm crown glow", glow, 3.2f, Alpha(BladeAwakeGold, .45f), 121);
                var crown = Attach(crownPlane, "Storm crown", runeRing, 2.6f, Alpha(BladeAwakeGold, .95f), 127);
                foreach (var clip in new[] { crownGlow, crown }) { clip.spin = 260; BladeZoneFade(clip, life); layers.Add(clip); }
                var pull = Attach(root, "Storm suction", glow, .1f, new Color(0, 0, 0, 0), 110);
                BladeZoneFade(pull, life); layers.Add(pull);
                float outer = Mathf.Max(radius * 1.6f, R + .8f), inner = R * .3f; int beat = 0;
                Emitter(pull, 32, 0, (owner, p) =>
                {
                    if (owner.age > owner.life - .3f) return;
                    beat++;
                    Vector2 o = owner.root.transform.position;
                    float side = beat % 2 == 0 ? 1 : -1, floorY = o.y - .62f, x = Range(R * .75f, outer);
                    float speed = Range(8, 11), travel = Mathf.Clamp((x - inner) / speed, .12f, .45f);
                    if (beat % 3 == 0)
                    {
                        // Dust skidding in along the floor: a pale scuff streak with a puff kicked up behind it, as if dragged.
                        Vector2 foot = new Vector2(o.x + side * x, floorY + .1f), toward = new Vector2(-side, 0);
                        var skid = BladeBeamLine(foot - toward * Range(1.4f, 2.2f), foot, Range(.3f, .4f), new Color(.82f, .7f, .54f, .55f), travel, 117, false);
                        skid.velocity = toward * speed; skid.fadeIn = .04f; skid.hold = .5f; skid.fadePower = 1.4f;
                        var drift = Puff(foot + new Vector2(0, .12f), Range(.5f, .75f), BladeDust, toward * Range(4, 6) + Vector2.up * Range(.1f, .4f), Range(.4f, .55f), 118);
                        drift.drag = 1.2f; drift.endScale = 1.6f; return;
                    }
                    // A wind streak rushing in toward the caster at body height.
                    float y = floorY + Range(.35f, 1.8f), len = Range(1.4f, 2.3f);
                    Vector2 head = new Vector2(o.x + side * x, y), dir = new Vector2(-side, Range(-.05f, .05f));
                    var lit = BladeBeamLine(head - dir * len, head, Range(.24f, .34f), beat % 4 == 1 ? Color.white : Alpha(BladeAwakeGold, 1), travel, 123);
                    lit.velocity = dir * speed; lit.fadeIn = .04f; lit.hold = .55f; lit.fadePower = 1.5f;
                });
            }
            Bundle(root, heart, layers.ToArray());
        }

        // ---------------------------------------------------------------- DashStrike: blazing dash and iaido flash cut

        /// <summary>The push-off of a dash: a forward needle beam of glare over a bloom, a sonic ring, a spark flaring at the hip,
        /// the floor cracking and pale wind and dust blasted out behind.</summary>
        private void BladeDashLaunch(Vector2 pos, Vector2 feet, int f, Color cyan, float delay, bool full)
        {
            var glare = BladeBeamLine(pos + new Vector2(-f * 1.2f, -.05f), pos + new Vector2(f * 3.2f, .05f), .5f, Alpha(Color.Lerp(cyan, Color.white, .25f), 1), .14f, 131);
            glare.startScale = .5f; glare.endScale = 1.1f; glare.ease = 5; glare.fadePower = 1.4f; BladeDelay(glare, delay);
            var boom = ShockFlip(pos + new Vector2(f * .5f, 0), 2.9f, Alpha(cyan, 1), .24f, 130);
            boom.angle = 0; boom.scale.x *= .38f; boom.startScale = .3f; boom.endScale = 1.1f; boom.ease = 3; BladeDelay(boom, delay);
            BladeStar(pos + new Vector2(f * .3f, -.1f), 1.7f, Color.white, Alpha(cyan, .5f), .1f, 132, delay);
            if (!full) return;
            var kickScar = Decal(feet + Vector2.up * .02f, crack, 2.8f, new Color(.16f, .3f, .62f, .45f), .9f, false, 25); BladeDelay(kickScar, delay);
            var kickGlow = Decal(feet + Vector2.up * .03f, crack, 2.8f, Alpha(cyan, 1), .55f, true, 26); kickGlow.fadePower = 1.8f; BladeDelay(kickGlow, delay);
            var kickRing = GroundWave(feet + Vector2.up * .05f, 3.4f, Alpha(cyan, .9f), .3f); BladeDelay(kickRing, delay);
            BladeKick(feet + new Vector2(-f * .2f, .4f), new Vector2(-f, .35f), .6f, BladeWind, BladeShare(3), .45f, 126, delay);
            int kicks = BladeShare(3);
            for (int i = 0; i < kicks; i++)
            {
                var puff = Puff(feet + new Vector2(-f * Range(0, .5f), .2f), Range(.6f, .9f), BladeDust, new Vector2(-f * Range(3, 6), Range(.6f, 1.6f)), Range(.4f, .6f), 118);
                puff.endScale = 2.2f; BladeDelay(puff, delay);
            }
        }

        private void CastDashStrike(CastInfo c)
        {
            int f = c.facing; float w = c.windup, release = Mathf.Max(0, w - .02f);
            Vector2 pos = c.pos, feet = new Vector2(pos.x, BladeGroundY(pos, 1)), hip = pos + new Vector2(-f * .25f, -.25f), eye = pos + new Vector2(f * .16f, .52f);
            Color cyan = BladeDashHue(c.light);
            if (w < .2f)
            {
                // An instant echo: the blade's gleam at the hip, then the launch glare and a sonic ring as the body breaks away.
                var quickGleam = Anchored(c.anchor, hip + new Vector2(f * .4f, .08f), "Dash quick gleam", BladeCutSprite, 2.4f, Alpha(cyan, 1), w + .05f, 128);
                quickGleam.angle = f > 0 ? 18 : 162; quickGleam.scale.y *= .7f; quickGleam.startScale = .3f; quickGleam.endScale = 1.2f; quickGleam.ease = 1.5f; quickGleam.fadePower = 1.5f;
                BladeDashLaunch(pos, feet, f, cyan, release, false);
                return;
            }
            // A battle aura: deep cobalt smoke boiling tight round the crouched body (behind the actors) over a blue glow, with
            // flame licks of blue and cobalt light climbing through it, longer and denser with the level.
            int grade = Grade; float lift = BladeLevel(1.15f, 1.3f);
            int smokes = BladeShare(6);
            for (int i = 0; i < smokes; i++)
            {
                float side = i % 2 == 0 ? 1 : -1, delay = Range(0, w * .5f);
                var smoke = Puff(pos + new Vector2(side * Range(.35f, .7f), Range(-.8f, .35f)), Range(1, 1.5f), new Color(.24f, .46f, 1, .42f), new Vector2(side * Range(.1f, .35f), Range(.9f, 1.8f)), Mathf.Clamp(w - delay + .05f, .15f, .5f), 27);
                smoke.drag = 1; smoke.startScale = .6f; smoke.endScale = 1.4f; BladeDelay(smoke, delay);
            }
            BladeAuraLicks(c.anchor, pos, feet.y, w, cyan, BladeCobalt, 30 * lift, 1.05f * lift, .75f);
            // The sheathed blade at the hip: a white star swelling on it and the drawn edge gleaming longer up to the draw; the
            // eyes flash just before the dash.
            var spark = Anchored(c.anchor, hip, "Dash charge star", fleck, 1.1f, Color.white, w + .03f, 127);
            spark.startScale = .25f; spark.endScale = 1.25f; spark.ease = 1; spark.pulse = .15f; spark.spin = 220; spark.hold = .9f; spark.fadePower = 2;
            var gleam = Anchored(c.anchor, hip + new Vector2(f * .4f, .08f), "Dash blade gleam", BladeCutSprite, 2.4f, Alpha(cyan, 1), w + .03f, 128);
            gleam.angle = f > 0 ? 18 : 162; gleam.scale.y *= .75f; gleam.startScale = .25f; gleam.endScale = 1.2f; gleam.ease = 1.2f; gleam.hold = .9f; gleam.fadePower = 2;
            if (grade >= 2)
            {
                // Higher levels: the drawn edge burns longer and, awakened, gold-white along its length.
                var gild = Anchored(c.anchor, hip + new Vector2(f * .55f, .12f), "Dash blade gilt", BladeCutSprite, 2.4f * lift + .5f, Alpha(grade >= 3 ? BladeAwakeGold : Warm, 1), w + .03f, 129);
                gild.angle = f > 0 ? 18 : 162; gild.scale.y *= .55f; gild.startScale = .25f; gild.endScale = 1.2f; gild.ease = 1.2f; gild.hold = .9f; gild.fadePower = 2;
            }
            BladeTwinkle(eye, 1.4f, Alpha(Color.Lerp(cyan, Color.white, .5f), 1), .2f, 0, Mathf.Max(.04f, w - .15f));
            int zaps = BladeShare(4);
            for (int i = 0; i < zaps; i++) BladeZap(pos + Inside(.9f), 1.5f, cyan, Range(.04f, w * .9f));
            // A thin line of intent runs from the hip to the target and brightens until the dash: the path the cut will take.
            if ((c.target - hip).sqrMagnitude > .25f)
            {
                var aimGlow = BladeBloomLine(hip, c.target, .5f, Alpha(cyan, .4f), w + .02f, 127);
                var aimLine = BladeCutLine(hip, c.target, .1f, Alpha(cyan, 1), w + .02f, 128);
                foreach (var clip in new[] { aimGlow, aimLine }) { clip.fadeIn = w * .75f; clip.hold = .92f; clip.fadePower = 2; }
                aimLine.flicker = .3f;
            }
            // The floor: a painted shock ring drawn in under the planted feet.
            var pull = GroundWave(feet + Vector2.up * .05f, 4.4f, Alpha(cyan, .9f), w * .9f); pull.startScale = 1; pull.endScale = .15f; pull.ease = 2; pull.fadeIn = .08f;
            // Speed lines rush in from both sides and slip past the body, crowding in as the dash nears.
            int lines = BladeShare(11);
            for (int i = 0; i < lines; i++)
            {
                float side = i % 3 == 0 ? -f : f, y = pos.y + Range(-.9f, 1f), x = pos.x + side * Range(2.2f, 4f), len = Range(1, 2.4f);
                var line = BladeCutLine(new Vector2(x + side * len, y), new Vector2(x, y), Range(.1f, .2f), i % 3 == 1 ? Color.white : Alpha(cyan, 1), Range(.16f, .24f), 128);
                line.velocity = new Vector2(-side * Range(9, 14), 0); line.fadeIn = .03f; line.fadePower = 1.4f;
                BladeDelay(line, w * .75f * Mathf.Sqrt(Range(0, 1)));
            }
            Converge(hip, Mathf.RoundToInt(9 * BladeLevel(.85f, .7f)), 2.1f, cyan, w, .5f);
            int dusts = BladeShare(4);
            for (int i = 0; i < dusts; i++)
            {
                var puff = Puff(feet + new Vector2(-f * Range(.1f, .5f), .12f), Range(.4f, .65f), BladeDust, new Vector2(-f * Range(1.5f, 3), Range(.3f, .9f)), Range(.3f, .45f), 118);
                BladeDelay(puff, Range(0, w * .5f));
            }
            // Release: the body breaks away.
            BladeDashLaunch(pos, feet, f, cyan, release, true);
        }

        private void ImpactDashStrike(Vector2 pos, float radius, Color tint, int facing)
        {
            // Without a facing this is the awakened crossing cut that lands where the dash ended.
            if (facing == 0) { BladeCrossCut(pos, radius, tint); return; }
            int f = facing < 0 ? -1 : 1, grade = Grade;
            float R = Mathf.Clamp(radius, 1, 7f), k = Mathf.Clamp(R / 4.5f, .3f, 1.1f), lift = BladeLevel(1.15f, 1.3f);
            Color cyan = BladeDashHue(tint), deep = Alpha(BladeCobalt, .8f);
            float gy = BladeGroundY(pos, 1), span = Mathf.Min(2 * R, 8.6f * BladeLevel(1.12f, 1.25f)), tilt = Range(-4, 4);
            Vector2 center = pos + new Vector2(f * span * .22f, -.1f), along = Direction(tilt) * f, across = new Vector2(-along.y, along.x);
            Vector2 a0 = center - along * span * .5f, a1 = center + along * span * .5f;
            // Iaido: the flash cut lands now; the sheath clicks home at burstAt and every cut left hanging in the air bursts at once.
            // An awakened dash's crossing cut lands 0.2 s after the dash, so its sheath clicks early (0.13 s): three clean beats,
            // the dash cut, the sheath burst, then the X.
            float burstAt = grade >= 3 ? .13f : .46f, spacing = grade >= 3 ? .02f : .03f;
            // The flash cut: one long slash beam across the whole reach (a blue beam with a white core and a white needle over a
            // slightly thinner cobalt stroke and a wide blue bloom), with a thinner beam glancing off it at a slight angle.
            BladeShade(a0 - along * .2f, a1 + along * .3f, 3f * lift, .34f, .5f);
            var band = BladeBloomLine(a0 - along * .5f, a1 + along * .6f, 2.6f * lift, Alpha(cyan, .32f), .36f, 126);
            var depthBeam = BladeBeamLine(a0 - along * .2f, a1 + along * .4f, 1.1f * lift, deep, .32f, 128);
            var beam = BladeBeamLine(a0 - along * .3f, a1 + along * .45f, 1.25f * lift, Alpha(cyan, 1), .3f, 129);
            var needle = BladeBeamLine(a0 + along * span * .15f, a1 + along * .9f, .42f * lift, Color.white, .2f, 131);
            var glance = BladeBeamLine(a0 + along * span * .3f - across * .55f, a1 + along * .2f + across * .75f, .5f * lift, Alpha(cyan, 1), .24f, 130);
            var cutLayers = new List<Clip> { band, depthBeam, beam, needle, glance };
            // Awakened: a gold-white edge runs the whole cut.
            if (grade >= 3) cutLayers.Add(BladeCutLine(a0 - along * .3f + across * .2f, a1 + along * .7f + across * .2f, .24f, Alpha(BladeAwakeGold, 1), .3f, 132));
            foreach (var clip in cutLayers) { clip.axisStart = new Vector2(.3f, .45f); clip.axisEnd = Vector2.one; clip.ease = 9; clip.hold = .3f; clip.fadePower = 1.3f; }
            var thread = BladeCutLine(a0, a1 + along * .3f, .12f, Alpha(cyan, 1), .75f, 128); thread.hold = .35f; thread.fadePower = 1.4f;
            // The finisher: a giant painted crescent cut sweeping up through the reach in front of the hero, bulging forward, glowing
            // over a blue bloom with a cobalt stroke inside it, and a white silhouette frame the moment it lands.
            float sweepW = span * .56f, sweepH = span * .62f;
            BladeSweep(new Vector2(pos.x + f * span * .18f, gy + sweepH * .4f), sweepW, sweepH, f > 0 ? 14 : 166, Alpha(cyan, 1), .45f, Alpha(BladeCobalt, .7f), .42f, 128, f < 0, true);
            // Hits along the cut: sparks flaring one after the other over blue blooms with needle sparks thrown forward, a shock
            // ring and wedge lines at the first, pale wind kicked out of the far end.
            for (int i = 0; i < 2; i++)
            {
                Vector2 at = Vector2.Lerp(a0, a1, .45f + i * .28f) + across * Range(-.2f, .5f);
                BladeStar(at, (1.25f + .4f * k) * (i == 0 ? 1.25f : 1), i == 1 ? Alpha(cyan, 1) : Color.white, Alpha(cyan, .5f), .13f, 133, i * .04f);
                BladeNeedles(at, f > 0 ? 8 : 172, 40, 5, 10, cyan, .32f, .46f, i * .04f);
            }
            var hitRing = ShockFlip(Vector2.Lerp(a0, a1, .45f), 2.6f + k, Alpha(cyan, .9f), .26f, 130); hitRing.scale.x *= .55f; hitRing.angle = 0;
            ImpactLines(Vector2.Lerp(a0, a1, .55f) + Vector2.up * .3f, R * 1.3f, new Color(.7f, .9f, 1, .6f), .18f);
            BladeKick(a1 - along * .8f, along + Vector2.up * .3f, .6f, BladeWind, BladeShare(3), .45f, 126);
            // The strike's reach on the floor: a ring centred where the dash ends, its rim on the gameplay radius.
            GroundWave(new Vector2(pos.x, gy + .05f), R * 2 / .74f, Alpha(cyan, .9f), .4f);
            if (R >= 3) ScreenFlash(center, new Color(.8f, .95f, 1, .1f), .08f);
            // Hanging cuts: thin cuts snap open one after another across the area and hang there, trembling, until the sheath
            // clicks; then each splits open into a band of blue light with a flaring spark and needle sparks.
            // An awakened dash follows with its crossing cut, so it leaves three fewer cuts hanging here.
            int cuts = Mathf.RoundToInt(3 + 4 * k) - (grade >= 3 ? 3 : 0);
            for (int i = 0; i < cuts; i++)
            {
                float appear = spacing + i * spacing, hang = Mathf.Max(.02f, burstAt - appear), pop = burstAt + i * .012f;
                // Spread evenly across the reach (with jitter) so the cuts fill the area instead of bunching up.
                float slot = cuts == 1 ? .5f : i / (float)(cuts - 1);
                Vector2 mid = new Vector2(pos.x + f * Mathf.Lerp(-.2f, 1.05f, Mathf.Repeat(slot * 3.7f, 1)) * R * .9f + Range(-.25f, .25f), gy + Range(.4f, 2.2f));
                float a = i % 3 == 2 ? Range(-12, 12) : (i % 2 == 0 ? Range(25, 60) : Range(-60, -25)), len = Range(3.4f, 4.8f) * (.5f + .5f * k);
                Vector2 half = Direction(a) * len * .5f;
                var cut = BladeCutLine(mid - half, mid + half, .27f, Alpha(cyan, 1), hang + .03f, 130);
                cut.axisStart = new Vector2(.08f, 1); cut.axisEnd = Vector2.one; cut.ease = 14; cut.hold = .9f; cut.fadePower = 2; BladeDelay(cut, appear);
                cut.flicker = .15f;
                var tip = Spawn("Cut tip", fleck, mid + half * .95f, .7f, Color.white, .12f, true, 131); tip.startScale = .3f; tip.ease = 4; tip.fadePower = 1.4f; BladeDelay(tip, appear);
                var blastGlow = BladeBloomLine(mid - half, mid + half, 1.1f, Alpha(cyan, .45f), .28f, 128);
                var blast = BladeBeamLine(mid - half * 1.15f, mid + half * 1.15f, .45f, Alpha(cyan, 1), .22f, 131);
                blastGlow.axisEnd = new Vector2(1.08f, 2.2f); blast.axisEnd = new Vector2(1.08f, 3f);
                foreach (var clip in new[] { blastGlow, blast }) { clip.axisStart = Vector2.one; clip.ease = 3; clip.hold = .15f; clip.fadePower = 1.4f; BladeDelay(clip, pop); }
                BladeStar(mid + half * Range(-.4f, .4f), 1.5f * (.6f + .4f * k), Color.white, Color.clear, .16f, 132, pop);
                // The scar of the cut lingers a moment after it bursts.
                var after = BladeCutLine(mid - half, mid + half, .12f, Alpha(cyan, 1), .5f, 129); after.hold = .25f; after.fadePower = 1.3f; BladeDelay(after, pop + .08f);
                for (int j = 0; j < (grade >= 2 ? 2 : 3); j++)
                {
                    var bit = Spark(mid + half * Range(-.8f, .8f), Direction(a + 90 + Range(-25, 25) + (j % 2 == 0 ? 0 : 180)) * Range(4, 9), j == 0 ? Color.white : cyan, Range(.26f, .42f), Range(.22f, .36f), 4, 2.5f);
                    BladeDelay(bit, pop);
                }
            }
            // The sheath click: a glint and a ring at the hip, then a wide ring and a last burst of wedge lines over the cut area.
            Vector2 sheath = pos + new Vector2(-f * .2f, -.25f);
            BladeTwinkle(sheath, 1.4f, Color.white, .18f, 20, burstAt - .03f);
            var clickRing = Wave(sheath, 1.6f, Alpha(Color.Lerp(cyan, Color.white, .6f), 1), .2f, 3); BladeDelay(clickRing, burstAt - .03f);
            // (Awakened, the crossing cut brings its own burst a beat later, so this one stays a ring without the wedge lines.)
            var finale = Wave(center + Vector2.up * .4f, R * 2f, Alpha(cyan, grade >= 3 ? .5f : .8f), .34f, 3); BladeDelay(finale, burstAt);
            if (grade < 3) { var finaleLines = ImpactLines(center + Vector2.up * .4f, R * 1.6f, new Color(.7f, .9f, 1, .6f), .22f); BladeDelay(finaleLines, burstAt); }
            // Follow-through: a scar of light on the floor under the cut over a soft glow, and dust swept forward.
            var scarGlow = BladeBloomLine(new Vector2(a0.x, gy + .04f), new Vector2(a1.x, gy + .04f), .55f, Alpha(cyan, .4f), 1.1f, 25);
            var scar = BladeCutLine(new Vector2(a0.x, gy + .05f), new Vector2(a1.x, gy + .05f), .16f, Alpha(cyan, 1), 1.1f, 26);
            foreach (var clip in new[] { scarGlow, scar }) { clip.axisStart = new Vector2(.2f, 1); clip.axisEnd = Vector2.one; clip.ease = 9; clip.hold = .5f; clip.fadePower = 1.5f; }
            int dusts = BladeShare(5);
            for (int i = 0; i < dusts; i++)
            {
                var puff = Puff(new Vector2(pos.x + f * Range(-.3f, 1.5f) * R * .5f, gy + .15f), Range(.55f, .95f), BladeDust, new Vector2(f * Range(1.5f, 4), Range(.3f, 1)), Range(.5f, .8f), 118);
                puff.endScale = 2.2f;
            }
        }

        /// <summary>The awakened crossing cut where a dash ended (facing 0), drawn at the moment it strikes: two long diagonal slash
        /// beams snap open one after the other into a tall X standing on the floor, its feet on the burst's reach and its crossing
        /// above the hero's head; each rides a curved razor slash, a blast of blue light flares where they cross, each cut splits open into
        /// a band of blue light within a tenth of a second (with the damage), and an X scar is left on the floor.</summary>
        private void BladeCrossCut(Vector2 pos, float radius, Color tint)
        {
            float r = Mathf.Clamp(radius, 1, 6), k = Mathf.Clamp(r / 4, .4f, 1.2f);
            Color cyan = BladeDashHue(tint);
            float gy = BladeGroundY(pos, .8f), slope = 30, reach = Mathf.Min(r * .92f, 5.2f), half = reach / Mathf.Cos(slope * Mathf.Deg2Rad);
            float rise = half * Mathf.Sin(slope * Mathf.Deg2Rad);
            // The X stands on the floor: its lower tips touch the ground at the reach on both sides.
            Vector2 center = new Vector2(pos.x, gy + Mathf.Max(1.05f, rise - .05f));
            for (int i = 0; i < 2; i++)
            {
                float angle = i == 0 ? slope : 180 - slope, appear = i * .05f, pop = .1f + i * .04f;
                Vector2 d = Direction(angle) * half, a = center - d, b = center + d;
                var bloom = BladeBloomLine(a, b, 2.1f * k + .5f, Alpha(cyan, .5f), pop - appear + .1f, 127);
                var depthBeam = BladeBeamLine(a, b, .72f * k + .16f, Alpha(BladeCobalt, .8f), pop - appear + .05f, 128);
                var beam = BladeBeamLine(a - d * .04f, b + d * .04f, .85f * k + .2f, Alpha(cyan, 1), pop - appear + .05f, 129);
                var razor = BladeBeamLine(a + d * .2f, b + d * .1f, .3f, Color.white, pop - appear + .04f, 131);
                foreach (var clip in new[] { bloom, depthBeam, beam, razor }) { clip.axisStart = new Vector2(.1f, .6f); clip.axisEnd = Vector2.one; clip.ease = 12; clip.hold = .85f; clip.fadePower = 2; BladeDelay(clip, appear); }
                // A curved razor slash rides each cut so it has body, not just a line.
                var arc = BladeSlashAt(center, half * 1.9f, r * .45f, angle, Alpha(cyan, 1), .26f, true, 128);
                if (i == 1) arc.scale.y *= -1;
                BladeSnap(arc, appear);
                BladeStar(b, 1f * k + .4f, Color.white, Alpha(cyan, .45f), .12f, 132, appear + .02f);
                int sparks = BladeShare(4);
                for (int j = 0; j < sparks; j++)
                {
                    var bit = Spark(center + d * Range(-.9f, .9f), Direction(angle + (j % 2 == 0 ? 90 : -90) + Range(-25, 25)) * Range(4, 9) * k, j % 3 == 0 ? Color.white : cyan, Range(.26f, .4f), Range(.22f, .36f), 5, 2.5f);
                    BladeDelay(bit, appear + .02f);
                }
                // The cut splits open: a band of light swelling across its width, then gone.
                var blast = BladeBeamLine(a - d * .08f, b + d * .08f, .6f, Alpha(cyan, 1), .22f, 130);
                blast.axisEnd = new Vector2(1.06f, 3.6f); blast.axisStart = Vector2.one; blast.ease = 3; blast.hold = .15f; blast.fadePower = 1.4f; BladeDelay(blast, pop);
            }
            // Where the cuts cross: a blue blast of light flaring (the awakening overlay brings the crown here), a ring racing out
            // and pale wind thrown up behind.
            Blast(center, 1.8f * k + .9f, Alpha(cyan, 1), .34f, 131);
            BladeStar(center, 1.6f * k + .5f, Color.white, Color.clear, .16f, 134, .05f);
            var air = Wave(center, r * 1.4f, Alpha(cyan, .8f), .3f, 3); BladeDelay(air, .1f);
            BladeKick(center, Vector2.up, .55f, BladeWind, BladeShare(3), .45f, 116, .05f);
            // The reach on the floor and an X scar left in it (two crossing seams in perspective).
            GroundWave(new Vector2(pos.x, gy + .05f), r * 2 / .74f, Alpha(cyan, .9f), .45f);
            for (int i = 0; i < 2; i++)
            {
                float lean = i == 0 ? .14f : -.14f;
                Vector2 s0 = new Vector2(pos.x - r * .8f, gy + .03f - lean), s1 = new Vector2(pos.x + r * .8f, gy + .03f + lean);
                var scarGlow = BladeBloomLine(s0, s1, .5f, Alpha(cyan, .4f), 1f, 25);
                var scar = BladeCutLine(s0, s1, .15f, Alpha(cyan, 1), 1f, 26);
                foreach (var clip in new[] { scarGlow, scar }) { clip.axisStart = new Vector2(.1f, 1); clip.axisEnd = Vector2.one; clip.ease = 9; clip.hold = .45f; clip.fadePower = 1.5f; BladeDelay(clip, i * .07f); }
            }
            int dusts = BladeShare(3);
            for (int i = 0; i < dusts; i++)
            {
                float side = i % 2 == 0 ? 1 : -1;
                var puff = Puff(new Vector2(pos.x + side * Range(.3f, .8f) * r, gy + .15f), Range(.55f, .9f), BladeDust, new Vector2(side * Range(1.5f, 3.5f), Range(.3f, 1)), Range(.5f, .75f), 118);
                puff.endScale = 2.1f; BladeDelay(puff, .1f);
            }
        }

        private void TrailDashStrike(Vector2 from, Vector2 to, Color tint)
        {
            Vector2 delta = to - from; float length = delta.magnitude; if (length < .05f) return;
            Vector2 d = delta / length, n = new Vector2(-d.y, d.x); float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Color cyan = BladeDashHue(tint);
            int grade = Grade; float lift = BladeLevel(1.15f, 1.3f);
            // The dash itself: long slash beams along the path, a blue beam with a white core and a white needle over a slightly
            // thinner cobalt stroke and a wide blue bloom, with two thinner beams streaking alongside at different heights and
            // lengths (wider with the level).
            BladeShade(from - d * .3f, to + d * .4f, 2.6f * lift, .36f, .4f);
            var bloom = BladeBloomLine(from - d * .5f, to + d * .6f, 2.4f * lift, Alpha(cyan, .3f), .36f, 126);
            var depthBeam = BladeBeamLine(from - d * .25f, to + d * .45f, .95f * lift, Alpha(BladeCobalt, .85f), .31f, 127);
            var beam = BladeBeamLine(from - d * .35f, to + d * .5f, 1.1f * lift, Alpha(cyan, 1), .3f, 128);
            var needle = BladeBeamLine(from + d * length * .1f, to + d * .85f, .3f * lift, Color.white, .22f, 130);
            var upper = BladeBeamLine(from + d * length * .3f + n * .6f, to + n * .5f + d * .3f, .42f * lift, Alpha(cyan, 1), .24f, 129);
            var lower = BladeBeamLine(from + d * length * .12f - n * .5f, to - n * .42f - d * .5f, .3f * lift, Alpha(BladeDashBlue, 1), .2f, 129);
            var beams = new List<Clip> { bloom, depthBeam, beam, needle, upper, lower };
            // Awakened, a gold-white edge runs its length.
            if (grade >= 3) beams.Add(BladeCutLine(from + n * .24f, to + d * .4f + n * .24f, .16f, Alpha(BladeAwakeGold, 1), .26f, 131));
            foreach (var clip in beams) { clip.axisStart = new Vector2(.45f, .6f); clip.axisEnd = Vector2.one; clip.ease = 8; clip.hold = .25f; clip.fadePower = 1.3f; }
            var linger = BladeCutLine(from, to, .12f, Alpha(cyan, 1), .7f, 129); linger.hold = .35f; linger.fadePower = 1.4f;
            // White silhouettes of the body strung along the dash, the ones behind fading first.
            BladeGhosts(from, to, grade >= 3 ? 3 : 2);
            // Wind-cut lines slip along the beams and past them.
            int cuts = BladeShare(grade >= 3 ? 8 : 10);
            for (int i = 0; i < cuts; i++)
            {
                Vector2 a = Vector2.Lerp(from, to, Range(-.1f, .7f)) + n * Range(-1, 1) * Range(.3f, 1.1f);
                var cut = BladeCutLine(a, a + d * Range(1, 2.4f), Range(.07f, .15f), i % 3 == 0 ? Color.white : Alpha(cyan, 1), Range(.14f, .26f), 129);
                cut.velocity = d * Range(5, 10); cut.fadePower = 1.4f; BladeDelay(cut, Range(0, .06f));
            }
            // A sonic ring where the body broke away and another midway.
            for (int i = 0; i < 2; i++)
            {
                var boom = ShockFlip(Vector2.Lerp(from, to, .12f + i * .38f), 2.2f - i * .3f, Alpha(cyan, 1), .26f, 130);
                boom.scale.x *= .34f; boom.angle = angle; boom.startScale = .35f; boom.endScale = 1.15f; boom.ease = 3; BladeDelay(boom, i * .04f);
            }
            // Kick-off (the cast's launch brings the spark): pale wind torn off behind and needle sparks flung back off the beam.
            BladeKick(from - d * .3f, -d + Vector2.up * .4f, .55f, BladeWind, BladeShare(3), .45f, 126);
            int sparks = BladeShare(10);
            for (int i = 0; i < sparks; i++)
            {
                Vector2 at = Vector2.Lerp(from, to, Range(0, 1)) + n * Range(-.35f, .35f);
                Spark(at, -d * Range(1, 4) + n * Range(-4, 4) + Vector2.up * Range(0, 2), i % 3 == 0 ? Color.white : cyan, Range(.22f, .36f), Range(.2f, .4f), 6, 2);
            }
            float gy = BladeGroundY(from, .95f);
            int dusts = BladeShare(Mathf.Clamp(Mathf.RoundToInt(length * 1.1f), 2, 7));
            for (int i = 0; i < dusts; i++)
            {
                float t = (i + .5f) / dusts;
                var dust = Puff(new Vector2(Mathf.Lerp(from.x, to.x, t), gy + .12f), Range(.45f, .75f), BladeDust, new Vector2(d.x * Range(.8f, 2.2f), Range(.4f, 1.2f)), Range(.4f, .6f), 118);
                BladeDelay(dust, t * .05f);
            }
        }
    }
}
