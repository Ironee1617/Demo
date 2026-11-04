using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Archery and engineering family: MultiShot, PierceShot, RainArrows and Sentry.
    // Holy-light archery painted as light: a bow drawn as one glowing stroke, arrows and lances as luminous bolts (a crisp
    // silhouette, a smooth golden gradient rising to a white-hot ridge, a soft bloom under them and a painted comet wake
    // behind), a lance trailing a painted wind helix down a glowing spindle beam, spears raining from a sky sigil ringed by a
    // turning bank of luminous cloud, and a celestial crystal sentinel that scans and fires plasma-headed tracers.
    // Hits are the hand-animated spark flaring and scattering over a bloom, shock rings racing out across the shot, needle
    // sparks thrown forward and a little warm light haze; landings billow small blasts and dust.
    public sealed partial class TowerVfx
    {
        private static Sprite ArcheryArrowSprite, ArcheryLanceSprite, ArcheryWakeSprite, ArcheryBloomSprite, ArcheryBowGlowSprite;
        private static Sprite[] ArcheryBowFrames, ArcheryHelixFrames, ArcheryCloudFrames;
        private static Sprite ArcheryScanSprite, ArcherySentinelSprite;

        private static readonly Color ArcheryGold = new Color(1, .7f, .2f), ArcheryHot = new Color(1, .97f, .86f), ArcheryPale = new Color(1, .84f, .5f),
            // The sunlit holy cloud round the volley's sigil: cream gold with warm tan shadows.
            ArcheryCloudLight = new Color(1, .8f, .45f),
            // Warm light haze kicked out of a hit (a deep saturated amber, never dark) and sandy billowing dust.
            ArcheryHaze = new Color(.98f, .62f, .24f, .5f), ArcheryDust = new Color(.8f, .68f, .52f, .7f),
            ArcheryWind = new Color(.9f, .97f, 1),
            ArcherySky = new Color(.4f, .82f, 1), ArcherySkyHot = new Color(.86f, .98f, 1), ArcherySkyPlasma = new Color(.35f, .74f, 1),
            // Level three: gold-white awakened light, and the frost of an awakened volley (its arrows slow what they hit).
            ArcheryAwake = new Color(1, .94f, .74f), ArcheryFrost = new Color(.6f, .9f, 1), ArcheryFrostDeep = new Color(.3f, .68f, 1);

        // A sentinel on the floor. back is how far it floats away from the hero (sign = side); mounts ride at its eye, floors at its feet.
        private sealed class ArcherySentryRig { public Transform root, floorPlane; public Clip body, eye, scan, heart; public Clip[] mounts, floors; public Vector3 baseScale; public Vector2 eyeAt; public float fireAt = -9, aim, back; public int side = 1, frame; public bool paired; }
        // A wake behind a shot whose front end stays put while it grows with the distance flown, so it never pokes out behind the shooter.
        private sealed class ArcheryTrailLayer { public Clip clip; public float baseX, front, length; }
        private readonly List<ArcherySentryRig> ArcheryRigs = new List<ArcherySentryRig>();
        private Clip ArcherySentryMuzzle, ArcherySentryHit, ArcheryPierceHit, ArcheryStrikeMark, ArcheryVolleyMark;
        private int ArcherySentryShots, ArcherySentryFacing = 1;
        private float ArcheryRainGround = float.NaN;
        private int ArcheryRainSpears, ArcheryFrostBites;
        private Vector2 ArcheryTrailEnd = new Vector2(1e6f, 1e6f), ArcheryTrailDir = Vector2.right;
        // The game these effects belong to (read only: where the enemies stand, for bursts on bodies a piercing shot passes through,
        // and where the casters stand, so a sentinel floats away from the one who summoned it).
        private TowerGame ArcheryGameCache; private bool ArcheryGameLooked;

        // ---------------------------------------------------------------- textures

        // Bolts are unions of parts, each with a half width h at this point along the bolt: edge = h - y (positive inside) and
        // depth = edge / h (1 on the bolt's axis, 0 at its outline), so the painted ramp runs parallel to every edge and the
        // white-hot ridge follows the axis right to the tip. tone dims a part (fins and guards glow gold, not white).
        private static void ArcheryPart(float h, float y, float tone, ref float edge, ref float depth, ref float shade)
        {
            if (h <= .0005f) return;
            float e = h - y; if (e > edge) edge = e;
            float d = e / h; if (d > depth) { depth = d; shade = tone; }
        }

        // A light arrow pointing along +u (y is in the same units as u): a leaf head, a light shaft thinning to the tail and
        // two swept flare fins.
        private static void ArcheryArrowField(float u, float v, out float edge, out float depth, out float tone)
        {
            float x = u, y = Mathf.Abs(v) * .25f; edge = -1; depth = -1; tone = 0;
            float t = (x - .3f) / .68f;
            if (t > 0 && t < 1) ArcheryPart(.14f * (t < .24f ? Mathf.Pow(t / .24f, .55f) : Mathf.Pow((1 - t) / .76f, .9f)), y, 1, ref edge, ref depth, ref tone);
            if (x > -.92f && x < .35f) ArcheryPart((.012f + .03f * Mathf.Pow(Mathf.Clamp01((x + .92f) / 1.27f), 1.3f)) * Mathf.Clamp01((x + .92f) / .05f), y, .92f, ref edge, ref depth, ref tone);
            float f = (x + .9f) / .5f;
            if (f > 0 && f < 1) ArcheryPart((.02f + .1f * Mathf.Pow(1 - f, .8f)) * Mathf.Clamp01(f / .15f), y, .6f, ref edge, ref depth, ref tone);
        }

        // A light lance pointing along +u: a long leaf blade, a guard of two swept flares, a slim haft and a small pommel.
        private static void ArcheryLanceField(float u, float v, out float edge, out float depth, out float tone)
        {
            float x = u, y = Mathf.Abs(v) * .25f; edge = -1; depth = -1; tone = 0;
            float t = (x - .12f) / .86f;
            if (t > 0 && t < 1) ArcheryPart(.12f * (t < .18f ? Mathf.Pow(t / .18f, .55f) : Mathf.Pow((1 - t) / .82f, .85f)), y, 1, ref edge, ref depth, ref tone);
            float g = (.14f - x) / .3f;
            if (g > 0 && g < 1) ArcheryPart((.05f + .12f * Mathf.Pow(1 - g, 1.5f)) * Mathf.Clamp01(g / .12f) * Mathf.Clamp01((1 - g) / .05f), y, .75f, ref edge, ref depth, ref tone);
            if (x > -.95f && x < .14f) ArcheryPart(.016f + .022f * Mathf.Pow(Mathf.Clamp01((x + .95f) / 1.07f), 1.2f), y, .9f, ref edge, ref depth, ref tone);
            float p = 1 - Mathf.Abs(x + .86f) / .07f;
            if (p > 0) ArcheryPart(.05f * p, y, .7f, ref edge, ref depth, ref tone);
        }

        /// <summary>A bolt's light level: a crisp silhouette at a low (deep gold) level, rising smoothly to a narrow white-hot ridge
        /// on the axis, brightest round the head, with brush streaks <paramref name="n"/> running along the body.</summary>
        private static Color ArcheryBoltLevel(float edge, float depth, float tone, float x, float n)
        {
            if (depth <= 0) return Color.clear;
            float core = Mathf.Pow(Mathf.Clamp01(depth), 2.2f), along = .7f + .3f * Smooth(-.95f, .45f, x), hot = Gauss(x - .4f, .12f) * core * .3f;
            float level = .13f + .87f * Mathf.Clamp01(core * along * tone * (1 + .4f * (n - .5f) * (1 - core)) + hot);
            return White(level * Smooth(0, .006f, edge));
        }

        // The bow seen from the side (belly towards +u): a painted limb stroke thick at the grip, tapering to sharp recurved tips,
        // and a string drawn back over the four frames. offset is how far the point lies in front of (+) or behind the limb.
        private static void ArcheryBowField(float u, float v, int f, out float edge, out float depth, out float offset, out float stringDistance)
        {
            float x = u * .5f, y = v, ay = Mathf.Abs(y);
            float limbX = .25f - .34f * y * y + .1f * Smooth(.7f, .93f, ay);
            offset = x - limbX;
            float d = Mathf.Abs(offset) / Mathf.Sqrt(1 + Sq(.68f * y));
            float w = .05f * Mathf.Pow(Mathf.Max(0, 1 - Sq(ay / .96f)), .5f) + .018f * Gauss(y, .16f);
            edge = w - d; depth = w > .002f ? edge / w : -1;
            const float tipY = .9f;
            float tipX = .25f - .34f * tipY * tipY + .1f * Smooth(.7f, .93f, tipY), nockX = tipX - .02f - f * .1f;
            stringDistance = Mathf.Min(SegmentDistance(x, y, tipX, tipY, nockX, 0), SegmentDistance(x, y, tipX, -tipY, nockX, 0));
        }

        private static void BuildArcheryTextures()
        {
            ArcheryArrowSprite = Build("Archery light arrow", 256, 64, (u, v) =>
            {
                float e, d, t; ArcheryArrowField(u, v, out e, out d, out t);
                return ArcheryBoltLevel(e, d, t, u, Fbm(u * 1.3f + 3, v * 14 + 1, 2));
            });
            ArcheryLanceSprite = Build("Archery light lance", 384, 96, (u, v) =>
            {
                float e, d, t; ArcheryLanceField(u, v, out e, out d, out t);
                return ArcheryBoltLevel(e, d, t, u, Fbm(u * 1.2f + 7, v * 14 + 4, 2));
            });
            // A painted comet wake (front at +u): fat and hot behind the bolt, thinning and cooling to a point, its brush hairs
            // following the taper and fraying apart toward the tail.
            ArcheryWakeSprite = Build("Archery comet wake", 256, 64, (u, v) =>
            {
                float t = (u + 1) * .5f, hw = .9f * (1 - Smooth(.8f, 1, u)) * Mathf.Pow(t, .55f) + .001f;
                float across = 1 - Mathf.Abs(v) / hw; if (across <= 0) return Color.clear;
                float hair = Fbm(u * 1.4f + 3, v / hw * 3.2f + 7, 3);
                float level = (.12f + .88f * across * across) * (.25f + .75f * Mathf.Pow(t, 1.5f)) * (1 + .6f * (hair - .5f));
                level -= Sq(1 - t) * .55f * Fbm(u * 3 + 11, v / hw * 1.5f + 2, 2);
                return White(Mathf.Clamp01(level) * Smooth(0, .06f, across));
            });
            // The soft bloom under a bolt or a wake (front at +u): smooth light, widest and brightest toward the head.
            ArcheryBloomSprite = Build("Archery bolt bloom", 256, 64, (u, v) =>
            {
                float t = (u + 1) * .5f, w = .22f + .3f * Mathf.Pow(t, .6f);
                return White(Gauss(v, w) * Mathf.Pow(t, .8f) * (1 - Smooth(.7f, 1, u)) * Smooth(-1, -.7f, u));
            });
            // A double wind helix trailing behind a shot (front at +u), painted as two strands: the strand passing in front is
            // thick with a white ridge, the one behind thin and only coloured; both taper to points at the ends.
            ArcheryHelixFrames = Sheet("Archery wind helix", 6, 256, 64, (u, v, f) =>
            {
                float env = Smooth(-1, -.25f, u) * (1 - Smooth(.55f, .97f, u)); if (env <= 0) return Color.clear;
                float back = (1 - u) * .5f, level = 0;
                for (int s = 0; s < 2; s++)
                {
                    float arg = 2.6f * Mathf.PI * u + f / 6f * 2 * Mathf.PI + s * Mathf.PI;
                    float y = (.2f + .34f * back) * Mathf.Sin(arg), front = .5f + .5f * Mathf.Cos(arg);
                    float w = (.07f + .1f * front) * (.45f + .55f * env), depth = 1 - Mathf.Abs(v - y) / w;
                    if (depth <= 0) continue;
                    float n = Fbm(u * 3.2f + f * .8f + s * 5, (v - y) / w * 2.2f + 9, 2);
                    level = Mathf.Max(level, (.1f + .9f * Mathf.Pow(depth, 1.3f)) * (.32f + .68f * front) * (.84f + .34f * (n - .5f)) * Mathf.Min(1, env * 1.6f) * Smooth(0, .08f, depth));
                }
                return White(level);
            });
            // The bow: one glowing painted stroke (a deep gold edge rising to a white ridge along the limb), the string a thin
            // gold line, and a soft bloom of the limb drawn under it.
            ArcheryBowFrames = Sheet("Archery light bow", 4, 128, 256, (u, v, f) =>
            {
                float e, d, o, sd; ArcheryBowField(u, v, f, out e, out d, out o, out sd);
                float limb = 0;
                if (d > 0)
                {
                    float n = Fbm(v * 3 + 2, o * 40 + 5, 2);
                    limb = (.14f + .86f * Mathf.Clamp01(Mathf.Pow(d, 1.6f) * (.78f + .22f * Gauss(v, .3f)) * (1 + .35f * (n - .5f)))) * Smooth(0, .005f, e);
                }
                float bowString = .3f * (1 - Smooth(.004f, .009f, sd));
                return White(Mathf.Max(limb, bowString) * (1 - Smooth(.97f, 1, Mathf.Abs(v))));
            });
            ArcheryBowGlowSprite = Build("Archery light bow glow", 128, 256, (u, v) =>
            {
                float e, d, o, sd; ArcheryBowField(u, v, 0, out e, out d, out o, out sd);
                return White(Gauss(Mathf.Max(0, -e), .07f) * (1 - Smooth(.75f, 1, Mathf.Abs(v))) * Window(Mathf.Abs(u)));
            });
            // A bank of luminous cloud ringing the volley's sky sigil, drawn in perspective (looped): two rings of billowing lobes
            // on an ellipse, the near ones lower and bigger, lit from the sigil at its heart so each lobe glows pale on the side
            // facing it and deepens to amber away from it, creased where lobes meet and torn into wisps at the outer edge. The
            // lobes travel round the ellipse; after the last frame each has reached the next one's place, so the loop is seamless.
            ArcheryCloudFrames = Sheet("Archery sky cloud bank", 8, 384, 192, (u, v, f) =>
            {
                float x = u * 1.2f, y = v * .6f, er = Mathf.Sqrt(x * x + Sq(y * 3)) + .0001f, ox = x / er * .06f, oy = y * 3 / er * .02f;
                float first = -1, second = -1, lit = -1;
                for (int ring = 0; ring < 2; ring++)
                    for (int k = 0; k < 12; k++)
                    {
                        float phi = (k + .5f * ring) / 12 * 2 * Mathf.PI + f / 8f * 2 * Mathf.PI / 12, s = Mathf.Sin(phi);
                        float variety = .5f + .5f * Mathf.Sin(3 * phi + 1.3f) * Mathf.Cos(2 * phi);
                        float r = (ring == 0 ? .2f : .13f) * (1 - .3f * s) * (.8f + .4f * variety);
                        float dx = x - (ring == 0 ? .86f : .98f) * Mathf.Cos(phi), dy = y - ((ring == 0 ? .26f : .31f) * s + ring * .04f);
                        if (Mathf.Abs(dx) > r * 1.4f || Mathf.Abs(dy) > r * 1.4f) continue;
                        // a cauliflower rim: the lobe's edge wobbles with the angle round it
                        float around = Mathf.Atan2(dy, dx) + phi * 3 + ring * 2;
                        float d = 1 - Mathf.Sqrt(dx * dx + dy * dy) / (r * (1 + .045f * Mathf.Sin(around * 3) + .03f * Mathf.Sin(around * 7 + 1)));
                        if (d > first) { second = first; first = d; } else if (d > second) second = d;
                        lit = Mathf.Max(lit, 1 - Mathf.Sqrt(Sq(dx + ox) + Sq(dy + oy)) / r);
                    }
                if (first <= -.5f) return Color.clear;
                float th = Mathf.Atan2(y * 3, x), ph = f / 8f * 2 * Mathf.PI;
                float n = Fbm(Mathf.Cos(th) * 2.4f + er * 2 + 5 + .2f * Mathf.Cos(ph), Mathf.Sin(th) * 2.4f + 13 + .2f * Mathf.Sin(ph), 3);
                float field = first - .28f * (n - .45f) * (1 - Smooth(0, .3f, first));
                if (field <= 0) return Color.clear;
                float crease = Gauss(first - second, .09f) * Smooth(.02f, .15f, second) * .1f;
                float level = .12f + .18f * Mathf.Clamp01(field * 1.5f) + .36f * Smooth(0, .6f, lit) + .08f * Smooth(-.2f, .25f, y) * Mathf.Clamp01(field * 2) + .08f * (n - .5f) - crease;
                return White(Mathf.Clamp01(level) * Smooth(0, .03f, field));
            });
            // A scanning cone from the left edge (pivot): a white beam down its middle and bright edge lines near the eye, a
            // coloured fill falling off toward the rim.
            ArcheryScanSprite = Build("Archery scan cone", 256, 128, (u, v) =>
            {
                float x = (u + 1) * .5f, spread = .05f + .62f * x, av = Mathf.Abs(v);
                if (av > spread + .03f || x > .97f) return Color.clear;
                float n = Fbm(x * 2.5f + 3, v / Mathf.Max(.05f, spread) * 3 + 7, 2);
                float fill = Smooth(spread + .004f, spread - .01f, av) * Mathf.Max(0, .36f - .42f * x) * (.8f + .5f * (n - .5f));
                float edgeD = 1 - Mathf.Abs(av - spread) / .024f, edgeL = edgeD > 0 ? Mathf.Pow(edgeD, .7f) * Mathf.Max(0, .74f - .78f * x) : 0;
                float beamD = 1 - av / (.008f + .03f * (1 - x)), beamL = beamD > 0 ? Mathf.Pow(beamD, .7f) * Mathf.Max(0, 1 - 1.15f * x) : 0;
                return White(Mathf.Max(fill, Mathf.Max(edgeL, beamL)) * Smooth(0, .03f, x));
            }, new Vector2(0, .5f));
            // The sentinel, painted: a tall translucent crystal lit from within (deep blue edges deepening to a bright cyan heart
            // where its energy core floats), soft facet shading lit from the upper left, refraction streaks and a bright rim on
            // the lit edges, girdled by a tilted golden ring with smooth metal shading.
            ArcherySentinelSprite = Build("Archery sentinel", 128, 192, (u, v) =>
            {
                float x = u * .6667f, y = v;
                float cs = Mathf.Cos(.24f), sn = Mathf.Sin(.24f), rx = x * cs + (y + .02f) * sn, ry = -x * sn + (y + .02f) * cs;
                float er = Mathf.Sqrt(Sq(rx / .6f) + Sq(ry / .17f)), grad = Mathf.Sqrt(Sq(rx / .36f) + Sq(ry / .0289f)) / Mathf.Max(.001f, er);
                float band = Mathf.Abs(er - 1) / Mathf.Max(.001f, grad);
                float ringCover = 1 - Smooth(.03f, .042f, band), shine = Mathf.Clamp01(1 - band / .03f);
                Color ringC = Color.Lerp(new Color(.5f, .3f, .08f), new Color(1, .86f, .45f), Smooth(-.02f, .02f, -ry) * .6f + shine * .4f);
                ringC = Color.Lerp(ringC, new Color(1, .97f, .85f), Gauss(band - .008f, .006f) * Smooth(-.01f, .01f, -ry) * .8f); ringC.a = ringCover;
                Color c = Color.clear;
                if (ry >= 0) c = Over(c, ringC);
                float cy = y - .02f, d = Mathf.Abs(x) / .34f + Mathf.Abs(cy) / (cy > 0 ? .8f : .72f), cover = 1 - Smooth(.975f, 1.0f, d);
                if (cover > 0)
                {
                    // Soft facets: the upper-left face brightest, the lower-right deepest, blended across the ridges.
                    float lit = Mathf.Clamp01(.55f - x * 1.1f + cy * .5f), ridge = Gauss(x, .012f) * .18f + Gauss(cy - .1f, .015f) * .1f;
                    float inner = Gauss(Mathf.Sqrt(Sq(x) + Sq(cy - .1f)), .26f);
                    float streaks = Gauss(Mathf.Repeat((x * 1.6f + cy) * 3.2f, 1) - .5f, .05f) * (1 - Smooth(.5f, .85f, d)) * .25f;
                    float rim = Gauss(d - .9f, .045f) * Mathf.Clamp01(lit * 1.4f);
                    float level = Mathf.Clamp01(.18f + lit * .42f + inner * .5f + ridge + streaks + rim * .55f);
                    Color crystal = Color.Lerp(new Color(.04f, .15f, .46f), new Color(.24f, .6f, 1), Smooth(.1f, .5f, level));
                    crystal = Color.Lerp(crystal, new Color(.8f, .97f, 1), Smooth(.55f, .95f, level));
                    crystal = Color.Lerp(crystal, new Color(.06f, .2f, .55f), Smooth(.92f, .99f, d) * (1 - rim));
                    crystal.a = cover * Mathf.Lerp(.82f, 1, Smooth(.4f, .9f, d)); c = Over(c, crystal);
                }
                if (ry < 0) c = Over(c, ringC);
                c.a *= (1 - Smooth(.92f, 1, Mathf.Abs(u))) * (1 - Smooth(.94f, 1, Mathf.Abs(v)));
                return c;
            });
            // The blooms stay smooth additive light under the painted strokes.
            soft.Add(ArcheryBloomSprite); soft.Add(ArcheryBowGlowSprite);
        }

        // ---------------------------------------------------------------- small helpers

        private static Color ArcheryLight(Color tint) { return Color.Lerp(ArcheryGold, new Color(tint.r, tint.g, tint.b, 1), .3f); }
        /// <summary>An archery skill's growth with its level: 1 at level one, <paramref name="two"/> and <paramref name="three"/> above.</summary>
        private float ArcheryLevel(float two, float three) { return Grade >= 3 ? three : Grade == 2 ? two : 1; }
        /// <summary>A count of secondary particles (sparks, dust, debris) that Budget then grows with the level, kept gentle
        /// (about a sixth more at level three) so the level's new layers, not more debris, carry the step.</summary>
        private int ArcheryShare(int count) { return Budget(Mathf.Max(1, Mathf.RoundToInt(count * ArcheryLevel(.85f, .7f)))); }
        private static float ArcheryAngle(Vector2 d) { return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; }
        private static Vector2 ArcheryAim(Vector2 d, Vector2 fallback) { return d.sqrMagnitude > .0001f ? d.normalized : fallback; }
        /// <summary>The ease exponent that completes a start-to-end scale change in about <paramref name="seconds"/> of a long life.</summary>
        private static float ArcheryPop(float life, float seconds) { return Mathf.Log(.08f) / Mathf.Log(1 - Mathf.Clamp(seconds / Mathf.Max(.01f, life), .001f, .9f)); }
        /// <summary>Flattens a clip vertically (a ring lying on the ground or in the sky). Never give these spin.</summary>
        private static Clip ArcheryFlat(Clip clip, float y) { clip.scale.y *= y; clip.root.transform.localScale = clip.scale; return clip; }
        /// <summary>Narrows a clip horizontally (a ring standing across a flight path, before it is turned to the path).</summary>
        private static Clip ArcheryThin(Clip clip, float x) { clip.scale.x *= x; clip.root.transform.localScale = clip.scale; return clip; }
        /// <summary>Sets a clip's angle and applies it at once, so a freshly emitted clip is never drawn unrotated for a frame.</summary>
        private static Clip ArcheryTurn(Clip clip, float angle) { clip.angle = angle; clip.root.transform.localRotation = Quaternion.Euler(0, 0, angle + clip.angleOffset); return clip; }

        /// <summary>True while <paramref name="clip"/> is still alive, younger than <paramref name="window"/> and near <paramref name="pos"/>:
        /// a burst already marks this moment. Checks liveness, because an expired clip keeps the age it had when it died.</summary>
        private bool ArcheryRecent(Clip clip, float window, Vector2 pos, float distance)
        {
            return clip != null && clip.age < window && (clip.position - pos).sqrMagnitude < distance * distance && clips.Contains(clip);
        }

        /// <summary>Runs <paramref name="act"/> once, <paramref name="delay"/> seconds from now: the flipbook bursts (Blast) spawn
        /// several layers at once, so a wind-up cannot hold them back by age the way it holds a single clip.</summary>
        private void ArcheryLater(float delay, Action act)
        {
            if (delay <= 0) { act(); return; }
            bool done = false;
            var timer = Spawn("Archery timer", glow, Vector2.zero, .05f, new Color(0, 0, 0, 0), delay + .3f, true, 20);
            Emitter(timer, 120, 0, (owner, p) => { if (done || owner.age < delay) return; done = true; owner.life = owner.age; act(); });
        }

        /// <summary>A hit flash: the hand-animated spark flaring, splitting and scattering over a bloom, starting
        /// <paramref name="delay"/> seconds from now; <paramref name="squash"/> flattens it (a splash on the ground). Returns the spark.</summary>
        private Clip ArcheryFlash(Vector2 pos, float width, Color color, float life, int order = 132, float delay = 0, float squash = 1)
        {
            var bloom = Spawn("Flash bloom", glow, pos, width * 1.25f, Alpha(color, .5f), life * 1.4f + .05f, true, order - 3);
            bloom.startScale = .6f; bloom.endScale = 1.3f; bloom.ease = 3; bloom.fadePower = 1.4f; bloom.age = -delay; bloom.scale.y *= squash;
            var spark = SparkFlip(pos, width, color, life, order); spark.age = -delay;
            if (squash < 1) LayFlat(spark, squash);
            return spark;
        }

        /// <summary>Wedge impact lines leaning along <paramref name="dir"/> (squashed across it), so a hit bursts with the shot.</summary>
        private Clip ArcheryWedges(Vector2 pos, Vector2 dir, float width, Color color, float life, float squash = .55f, float delay = 0)
        {
            var lines = ImpactLines(pos, width, color, life); lines.angle = ArcheryAngle(dir); lines.scale.y *= squash; lines.age = -delay;
            lines.startScale = .6f; lines.ease = 6; lines.hold = .2f; lines.fadePower = 1.1f;
            return lines;
        }

        /// <summary>Needle sparks thrown mostly along <paramref name="dir"/> within <paramref name="cone"/> degrees (every fourth one back).</summary>
        private void ArcheryNeedles(Vector2 pos, Vector2 dir, int count, float speed, Color light, float life, float size, float cone, float delay = 0, float gravity = 4)
        {
            float a = ArcheryAngle(dir); count = ArcheryShare(count);
            for (int i = 0; i < count; i++)
            {
                bool back = i % 4 == 3;
                var needle = Spark(pos, Direction(a + (back ? 180 : 0) + Range(-cone, cone)) * speed * Range(.55f, 1.1f), i % 3 == 0 ? Color.white : light, size * Range(.7f, 1.2f), life * Range(.6f, 1), gravity, 2.6f);
                needle.hold = .25f; needle.age = -delay;
            }
        }

        /// <summary>A warm light haze billowing out of a hit, mostly back against the shot and up, that holds a beat and then tears
        /// away: a deep saturated tone behind the bright strokes, so they read on a pale sky.</summary>
        private void ArcheryHazePuffs(Vector2 pos, Vector2 dir, float size, Color color, int count, float life, int order = 121)
        {
            count = Budget(count); float back = ArcheryAngle(-dir);
            for (int i = 0; i < count; i++)
            {
                Vector2 d = Direction(back + Range(-75, 75)); d.y = Mathf.Abs(d.y) * .8f + .2f;
                var haze = Puff(pos + d * size * Range(.15f, .4f), size * Range(.75f, 1.1f), color, d * size * Range(1.4f, 2.4f), life * Range(.85f, 1.1f), order);
                haze.startScale = .55f; haze.endScale = 1.35f; haze.ease = 4; haze.fadeIn = 0; haze.hold = .25f; haze.fadePower = 1;
            }
        }

        /// <summary>A sonic-boom ring standing across <paramref name="dir"/>: the hand-animated shock front narrowed and turned to the
        /// path, racing out, thinning and breaking into arcs as it rides forward.</summary>
        private Clip ArcheryBoom(Vector2 pos, Vector2 dir, float width, Color color, float life, float speed, int order = 130)
        {
            var clip = ArcheryTurn(ArcheryThin(ShockFlip(pos, width, color, life, order), .4f), ArcheryAngle(dir));
            if (Chance(.5f)) { clip.scale.y = -clip.scale.y; clip.root.transform.localScale = clip.scale; }
            clip.startScale = .7f; clip.endScale = 1.05f; clip.ease = 4; clip.hold = .3f; clip.fadePower = .9f;
            clip.velocity = dir * speed; clip.drag = 5; return clip;
        }

        /// <summary>A shock front racing out across the floor, laid in the ground plane (so its drawn breaks turn there too).</summary>
        private Clip ArcheryGroundShock(Vector2 pos, float width, Color color, float life)
        {
            var wave = Wave(pos, width, color, life, 3); wave.renderer.sortingOrder = 116; return LayFlat(wave, .34f);
        }

        /// <summary>A big painted crescent sweep bulging along <paramref name="angle"/>: a coloured stroke with a white leading edge.</summary>
        private void ArcheryArc(Vector2 pos, float width, float angle, Color color, float life, float delay, Vector2 velocity, float squash = 1)
        {
            var arc = Spawn("Painted arc", crescent, pos, width, color, life, true, 130);
            var edge = Spawn("Painted arc edge", crescent, pos + Direction(angle) * width * .025f, width * .97f, Alpha(ArcheryHot, Mathf.Min(.6f, color.a)), life * .6f, true, 131);
            var bloom = Spawn("Painted arc glow", glow, pos + Direction(angle) * width * .25f, width * .9f, Alpha(color, .4f), life, true, 127);
            foreach (var x in new[] { arc, edge, bloom })
            {
                ArcheryTurn(x, angle); x.scale.x *= squash;
                x.startScale = .8f; x.endScale = 1.06f; x.ease = 7; x.hold = .3f; x.fadePower = 1.1f; x.age = -delay; x.velocity = velocity; x.drag = 4;
            }
            edge.scale.y *= .94f; edge.hold = .15f; edge.fadePower = 1.4f;
            bloom.scale.x *= .45f; bloom.fadePower = 1.5f;
        }

        /// <summary>A soft bloom stretched along a segment: the glow under a beam or a scar.</summary>
        private Clip ArcheryGlowLine(Vector2 a, Vector2 b, float thickness, Color color, float life, int order)
        {
            Vector2 delta = b - a; float length = Mathf.Max(.05f, delta.magnitude);
            var clip = Spawn("Beam glow", glow, (a + b) * .5f, length, color, life, true, order);
            clip.scale = new Vector3(length * 1.15f / glow.bounds.size.x, thickness / glow.bounds.size.y, 1); clip.root.transform.localScale = clip.scale;
            ArcheryTurn(clip, ArcheryAngle(delta)); clip.ease = 1;
            return clip;
        }

        /// <summary>A segment of the burning scar a lance leaves along its path: a spindle of light over a soft glow.</summary>
        private void ArcheryScar(Vector2 a, Vector2 b, Color light)
        {
            var lit = Line(a, b, .34f, Alpha(light, .9f), .42f, 122, 1.5f); lit.axisEnd = new Vector2(1, .35f); lit.ease = 2; lit.hold = .25f; lit.fadePower = 1.1f;
            var haze = ArcheryGlowLine(a, b, 1f, Alpha(light, .35f), .4f, 119); haze.axisEnd = new Vector2(1, .4f); haze.ease = 2; haze.fadePower = 1.3f;
        }

        private ArcheryTrailLayer ArcheryTrail(Transform root, string name, Sprite sprite, float length, float thickness, Color color, int order, bool additive, float front, List<ArcheryTrailLayer> into)
        {
            var clip = Attach(root, name, sprite, length, color, order, additive, new Vector2(front - length * .5f, 0));
            clip.scale.y *= thickness;
            var layer = new ArcheryTrailLayer { clip = clip, baseX = clip.scale.x, front = front, length = length };
            into.Add(layer); return layer;
        }

        private static void ArcheryGrowTrails(List<ArcheryTrailLayer> trails, float flown)
        {
            foreach (var t in trails)
            {
                float visible = Mathf.Clamp(flown + .35f, .25f, t.length);
                t.clip.scale.x = t.baseX * visible / t.length; t.clip.offset = new Vector2(t.front - visible * .5f, t.clip.offset.y);
                if (t.clip.root) { t.clip.root.transform.localScale = t.clip.scale; t.clip.root.transform.localPosition = t.clip.offset; }
            }
        }

        /// <summary>A luminous bolt riding on <paramref name="anchor"/>, or free when there is none: [0] its soft bloom, [1] the
        /// painted light.</summary>
        private Clip[] ArcheryShape(Transform anchor, Sprite sprite, Vector2 pos, float angle, float width, float life, Color light, int order, float bloom = .5f)
        {
            var shape = new[]
            {
                Anchored(anchor, pos, "Light bolt glow", ArcheryBloomSprite, width * 1.2f, Alpha(light, bloom), life, order - 4),
                Anchored(anchor, pos, "Light bolt", sprite, width, light, life, order),
            };
            shape[0].scale.y *= 1.4f; shape[0].root.transform.localScale = shape[0].scale;
            foreach (var clip in shape) ArcheryTurn(clip, angle);
            return shape;
        }

        /// <summary>Wind streaks spiralling into <paramref name="pos"/> over <paramref name="life"/> seconds.</summary>
        private void ArcheryWindIn(Vector2 pos, int count, float radius, Color color, float life, float turns)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                float delay = Range(0, life * .35f), span = Mathf.Max(.06f, life - delay);
                var mote = Spawn("Wind streak", spark, pos, Range(.6f, 1f), Alpha(i % 3 == 0 ? ArcheryHot : color, 1), span, true, 130);
                mote.age = -delay; mote.orbitCenter = pos; mote.orbitAngle = Range(0, 360); mote.orbit = -turns * 360 / span;
                mote.orbitRadius = radius * Range(.6f, 1.1f); mote.orbitRadiusEnd = .1f; mote.orbitPower = 1.3f;
                mote.alignVelocity = true; mote.fadeIn = .05f; mote.hold = .7f; mote.endScale = .5f; mote.ease = 1;
            }
        }

        /// <summary>A free invisible clip that watches a gameplay root and calls <paramref name="lost"/> with its last position once it is gone.</summary>
        private void ArcheryWatch(Transform root, float maxLife, Action<Vector2> lost)
        {
            Vector2 last = root.position; bool fired = false;
            var watch = Spawn("Shot watch", glow, last, .1f, new Color(0, 0, 0, 0), maxLife, true, 110);
            Emitter(watch, 60, 0, (owner, p) =>
            {
                if (fired) return;
                if (root) { last = root.position; return; }
                fired = true; owner.life = owner.age; lost(last);
            });
        }

        private TowerGame ArcheryGame()
        {
            if (!ArcheryGameLooked) { ArcheryGameLooked = true; ArcheryGameCache = GetComponentInParent<TowerGame>(); }
            return ArcheryGameCache;
        }

        /// <summary>
        /// Gameplay only reports a piercing shot's last hit, so the bodies it passes through are found here: every frame the shot's
        /// step is tested against each enemy's contact circle (the same circle gameplay hits, centred 0.7 up a body, 1.2 up a boss),
        /// and <paramref name="pass"/> draws a light pass-through burst at the entry point of each body it crosses for the first
        /// time, at most once every <paramref name="gap"/> seconds. A shot consumed by a body is destroyed before effects tick, so
        /// that last body gets the shot's full hit instead.
        /// </summary>
        private void ArcheryPassWatch(Transform root, float hitRadius, float gap, Action<Vector2, Vector2> pass)
        {
            var game = ArcheryGame(); if (game == null || !root) return;
            var crossed = new HashSet<TowerGame.Fighter>(); Vector2 previous = root.position; Clip last = null;
            var watch = Spawn("Pierce watch", glow, previous, .05f, new Color(0, 0, 0, 0), 5, true, 20);
            Emitter(watch, 120, 0, (owner, p) =>
            {
                if (!root) { owner.life = owner.age; return; }
                Vector2 now = root.position, step = now - previous; if (step.sqrMagnitude < 1e-6f) return;
                foreach (var enemy in game.enemies)
                {
                    if (enemy == null || !enemy.isEnemy || enemy.hp <= 0 || crossed.Contains(enemy)) continue;
                    Vector2 body = enemy.pos + Vector2.up * (enemy.isBoss ? 1.2f : .7f);
                    float reach = (enemy.isBoss ? 1.05f : .5f) + hitRadius, along = Mathf.Clamp01(Vector2.Dot(body - previous, step) / step.sqrMagnitude);
                    Vector2 closest = previous + step * along;
                    if ((closest - body).sqrMagnitude > reach * reach) continue;
                    crossed.Add(enemy);
                    if (last != null && last.age < gap && clips.Contains(last)) continue;
                    // Enter where the shot meets the body's near side.
                    Vector2 d = step.normalized; float back = Mathf.Sqrt(Mathf.Max(0, reach * reach - (closest - body).sqrMagnitude));
                    Vector2 entry = closest - d * Mathf.Min(back, (closest - previous).magnitude) * .6f;
                    last = Spawn("Pierce pass mark", glow, entry, .05f, new Color(0, 0, 0, 0), gap + .05f, true, 20);
                    pass(entry, d);
                }
                previous = now;
            });
        }

        /// <summary>The lance passing through a body: a little warm haze kicked back, the spark flaring over a bloom, a shock ring
        /// punched out across the throw, wedge lines, a spindle of light driven on and needles (about thirteen clips).</summary>
        private void ArcheryLanceThrough(Vector2 pos, Vector2 dir, Color light, float s, bool awake)
        {
            Color hot = awake ? Color.Lerp(ArcheryGold, ArcheryAwake, .4f) : ArcheryGold;
            ArcheryHazePuffs(pos, dir, .6f * s, ArcheryHaze, 2, .45f);
            ArcheryFlash(pos, 2.4f * s, Alpha(hot, 1), .2f, 133);
            ArcheryBoom(pos + dir * .35f, dir, 2.6f * s, Alpha(ArcheryPale, .9f), .26f, 4);
            ArcheryWedges(pos + dir * .3f, dir, 3.4f * s, Alpha(light, .8f), .18f, .6f);
            Vector2 end = pos + dir * 2.6f * s;
            var through = Line(pos, end, .42f * s, Alpha(light, 1), .2f, 127); through.axisStart = new Vector2(.3f, 1); through.axisEnd = new Vector2(1, .3f); through.ease = 5; through.hold = .2f; through.fadePower = 1.1f;
            ArcheryNeedles(pos, dir, 5, 10, light, .26f, .42f * s, 32);
        }

        /// <summary>An awakened fan arrow passing through its first body: a spark over a bloom, a shock ring and a white line driven on.</summary>
        private void ArcheryArrowThrough(Vector2 pos, Vector2 dir, Color light)
        {
            ArcheryFlash(pos, 1.4f, Alpha(ArcheryAwake, 1), .14f, 133);
            ArcheryBoom(pos + dir * .2f, dir, 1.6f, Alpha(ArcheryAwake, 1), .2f, 4);
            var through = Line(pos, pos + dir * 1.8f, .12f, Alpha(ArcheryHot, 1), .14f, 131); through.startScale = .3f; through.endScale = 1; through.ease = 6; through.velocity = dir * 6;
            ArcheryNeedles(pos, dir, 3, 8, light, .22f, .34f, 38);
        }

        // ---------------------------------------------------------------- the bow (MultiShot, PierceShot and RainArrows wind-ups)

        private void CastBow(CastInfo c) { ArcheryBow(c, 0); }

        // mode 0: a fan of three arrows, 1: a charged lance, 2: one arrow drawn towards the sky.
        private void ArcheryBow(CastInfo c, int mode)
        {
            float w = c.windup; int f = c.facing, grade = Grade;
            Color light = ArcheryLight(c.tint), wind = Color.Lerp(light, ArcheryWind, .5f);
            Vector2 dir = mode == 2 ? Direction(f > 0 ? 66 : 114) : ArcheryAim(c.target - c.pos, new Vector2(f, 0));
            // Higher levels draw a bigger bow and longer shafts.
            float gl = ArcheryLevel(1.08f, 1.16f);
            float a = ArcheryAngle(dir), bowH = (mode == 1 ? 3f : 2.7f) * gl, life = w + .14f, arrowLen = (mode == 1 ? 3.3f : 2.2f) * gl;
            Vector2 nockPt = c.pos + dir * .12f, bowAt = nockPt + dir * (.1262f * bowH), feet = c.pos + Vector2.down;
            Vector2 tip = nockPt + dir * arrowLen * .96f;
            if (w < .2f)
            {
                // An instant echo (a recast with no wind-up): only the snap of the release at the tip; the shot follows at once.
                float r = Mathf.Max(0, w - .02f);
                ArcheryFlash(tip - dir * .3f, 1.6f, Alpha(ArcheryGold, 1), .14f, 132, r);
                ArcheryBoom(tip, dir, 2.1f, Alpha(light, 1), .22f, 9).age = -r;
                return;
            }

            // The bow: one glowing painted stroke over its bloom that snaps open in two drawn frames and holds while the string
            // comes back.
            Underlay(bowAt + dir * .2f, bowH * 1.1f, .2f, life).fadeIn = .1f;
            var bow = new[]
            {
                Anchored(c.anchor, bowAt, "Light bow glow", ArcheryBowGlowSprite, bowH * .5f, Alpha(light, .6f), life, 118),
                Anchored(c.anchor, bowAt, "Light bow", ArcheryBowFrames[0], bowH * .5f, Alpha(light, 1), life, 127),
            };
            bow[1].frames = ArcheryBowFrames;
            float snap = ArcheryPop(life, .07f);
            foreach (var b in bow) { b.loop = false; b.angle = a; b.fadeIn = 0; b.hold = .8f; b.fadePower = 1.2f; b.startScale = .55f; b.endScale = 1; b.ease = snap; }
            if (grade >= 3)
            {
                // Awakened: the bow burns gold-white round its limbs and a rune ring stands across the shot behind it.
                var gilt = Anchored(c.anchor, bowAt, "Light bow gilt", ArcheryBowGlowSprite, bowH * .5f * 1.12f, Alpha(ArcheryAwake, .75f), life, 117);
                gilt.angle = a; gilt.hold = .8f; gilt.fadePower = 1.2f; gilt.startScale = .55f; gilt.endScale = 1; gilt.ease = snap; gilt.flicker = .1f;
                var halo = ArcheryThin(Anchored(c.anchor, bowAt - dir * .25f, "Awakened bow ring", runeRing, bowH * 1.05f, Alpha(ArcheryAwake, .6f), life, 126), .34f);
                halo.angle = a; halo.startScale = .4f; halo.endScale = 1; halo.ease = 6; halo.hold = .78f; halo.fadePower = 1.2f;
            }

            // arrows of light forming on the string (they grow out of their white ridge); the fan grows with the level (one more
            // arrow each), 9 degrees apart like the shot
            int count = mode == 0 ? 3 + (grade - 1) : 1; var sprite = mode == 1 ? ArcheryLanceSprite : ArcheryArrowSprite;
            for (int i = 0; i < count; i++)
            {
                float ai = a + (count == 1 ? 0 : (i - (count - 1) * .5f) * 9);
                var shape = ArcheryShape(c.anchor, sprite, nockPt + Direction(ai) * arrowLen * .48f, ai, arrowLen, w + .01f, Alpha(light, 1), 128, count > 1 ? .35f : .5f);
                foreach (var x in shape) { x.fadeIn = w * .45f; x.fadePower = 0; x.startScale = .35f; x.endScale = 1; x.ease = 3; }
            }
            var star = Anchored(c.anchor, tip, "Arrow charge star", glint, mode == 1 ? 2.1f : 1.6f, Alpha(ArcheryHot, 1), w + .04f, 129);
            star.startScale = .1f; star.endScale = 1; star.ease = 1.4f; star.spin = 160; star.hold = .85f; star.fadePower = 2;
            var charge = Anchored(c.anchor, tip, "Arrow charge", glow, mode == 1 ? 2.2f : 1.5f, Alpha(light, .7f), w + .04f, 126);
            charge.startScale = .12f; charge.endScale = 1; charge.ease = 1.2f; charge.pulse = .1f; charge.hold = .85f; charge.fadePower = 2;
            if (mode == 1)
            {
                // the charged lance gathers a churning orb of light at its point
                var orb = Anchored(c.anchor, tip, "Lance charge orb", plasmaFrames[0], 1.15f * gl, Alpha(light, 1), w + .04f, 130);
                orb.frames = plasmaFrames; orb.fps = 16; orb.loop = true; orb.startScale = .15f; orb.endScale = 1; orb.ease = 1.3f; orb.hold = .85f; orb.fadePower = 2;
            }

            // wind spiralling into the drawing hand: needle streaks and a painted vortex
            ArcheryWindIn(nockPt, mode == 2 ? 6 : mode == 1 ? 10 : 9, mode == 1 ? 2.9f : 2.4f, wind, w, .6f);
            var vortex = Swirl(nockPt, mode == 1 ? 3.2f : 2.6f, Alpha(wind, .55f), w + .04f, -380, 125); vortex.startScale = 1.25f; vortex.endScale = .35f; vortex.ease = 1.4f;

            // footing: a rune ring turning on the ground over its glow, and dust blown out by the gathering wind
            if (mode != 2)
            {
                var rune = LayFlat(Spawn("Archer rune", runeRing, feet, 3.7f, Alpha(light, .85f), w + .4f, true, 27), .3f);
                rune.startScale = .45f; rune.endScale = 1.05f; rune.ease = 6; rune.hold = .6f; rune.fadePower = 1.2f; rune.spin = -f * 70;
                GroundGlow(feet, 4.2f, Alpha(light, .4f), w + .35f);
            }
            for (int i = 0, n = Budget(mode == 2 ? 2 : 3); i < n; i++)
            {
                var dust = Puff(feet + new Vector2(Range(-.5f, .5f), .1f), Range(.5f, .8f), ArcheryDust, new Vector2((i % 2 == 0 ? 1 : -1) * Range(1, 2.2f), Range(.2f, .6f)), Range(.4f, .6f), 118);
                dust.gravity = -.3f;
            }

            // release
            float release = w - .02f;
            ArcheryRelease(tip - dir * .3f, dir, light, release, mode == 1 ? 1.2f : 1, mode != 1);
            for (int i = 0, n = Budget(2); i < n; i++)
            {
                var kick = Puff(feet + new Vector2(-f * Range(.1f, .5f), .1f), Range(.5f, .8f), ArcheryDust, new Vector2(-f * Range(1.5f, 3), Range(.2f, .7f)), Range(.35f, .55f), 118);
                kick.age = -release;
            }
        }

        /// <summary>The bow's release at the arrow tip: the spark flaring over a wide bloom, wedge lines leaning along the shot, shock
        /// rings racing out across it, gust lines streaming forward and a needle cone.</summary>
        private void ArcheryRelease(Vector2 pos, Vector2 dir, Color light, float delay, float s, bool booms)
        {
            Vector2 side = new Vector2(-dir.y, dir.x);
            ArcheryFlash(pos, 2.1f * s, Alpha(ArcheryGold, 1), .18f, 132, delay);
            var bloom = Spawn("Release bloom", glow, pos + dir * .5f, 3f * s, Alpha(light, .5f), .2f, true, 123); bloom.age = -delay; bloom.startScale = .7f; bloom.endScale = 1.2f; bloom.ease = 3; bloom.fadePower = 1.5f; bloom.scale.y *= .7f; ArcheryTurn(bloom, ArcheryAngle(dir));
            ArcheryWedges(pos + dir * .4f * s, dir, 3.4f * s, Alpha(light, .85f), .18f, .5f, delay);
            if (booms)
            {
                ArcheryBoom(pos + dir * .3f * s, dir, 2.4f * s, Alpha(light, 1), .26f, 8).age = -delay;
                ArcheryBoom(pos + dir * 1f * s, dir, 1.8f * s, Alpha(ArcheryPale, .9f), .22f, 13).age = -(delay + .04f);
            }
            else ArcheryBoom(pos + dir * .5f * s, dir, 2.8f * s, Alpha(ArcheryPale, .9f), .26f, 10).age = -delay;
            for (int i = 0, n = ArcheryShare(5); i < n; i++)
            {
                Vector2 start = pos + side * Range(-.7f, .7f) * s - dir * Range(.3f, 1f);
                var gust = Line(start, start + dir * Range(1.6f, 2.8f) * s, Range(.06f, .1f), i % 2 == 0 ? Alpha(ArcheryHot, 1) : Alpha(light, 1), Range(.14f, .2f), 129);
                gust.age = -delay; gust.velocity = dir * Range(10, 16); gust.drag = 3; gust.startScale = .5f; gust.endScale = 1; gust.ease = 5; gust.hold = .2f; gust.fadePower = 1.1f;
            }
            ArcheryNeedles(pos, dir, 7, 12 * s, light, .26f, .42f, 28, delay, 3);
        }

        private void CastMultiShot(CastInfo c)
        {
            ArcheryBow(c, 0);
            // the spread leaves the bow as a fan-shaped painted sweep in front of the arrows
            Color light = ArcheryLight(c.tint); float r0 = c.windup - .02f;
            Vector2 dir = ArcheryAim(c.target - c.pos, new Vector2(c.facing, 0)), at = c.pos + dir * 2.2f; float a = ArcheryAngle(dir), fanSize = ArcheryLevel(1.12f, 1.25f);
            ArcheryArc(at, 3.1f * fanSize, a, Alpha(Grade >= 3 ? Color.Lerp(light, ArcheryAwake, .5f) : light, 1), .24f, r0, dir * 7, .87f);
        }

        private void CastPierceShot(CastInfo c)
        {
            ArcheryBow(c, 1);
            if (c.windup < .2f) return;
            float w = c.windup; Color light = ArcheryLight(c.tint); int grade = Grade; float gl = ArcheryLevel(1.08f, 1.16f);
            Vector2 dir = ArcheryAim(c.target - c.pos, new Vector2(c.facing, 0)); float a = ArcheryAngle(dir);
            Vector2 tip = c.pos + dir * (.12f + 3.3f * gl * .96f);
            // accelerator rune rings around the lance, opening one after another (one more ring per level); each fires a shock
            // ring at the release
            int rings = 2 + grade;
            for (int i = 0; i < rings; i++)
            {
                Vector2 at = c.pos + dir * (1.3f + i * .78f);
                float width = (2.9f - i * .45f) * gl, delay = i * w * .15f, span = Mathf.Max(.1f, w - delay + .06f);
                var ringLit = ArcheryThin(Spawn("Accelerator ring", runeRing, at, width, Alpha(light, .95f), span, true, 127), .32f);
                ringLit.angle = a; ringLit.age = -delay; ringLit.startScale = .3f; ringLit.endScale = 1; ringLit.ease = 7; ringLit.hold = .85f; ringLit.fadePower = 1.4f; ringLit.flicker = .1f;
                if (i % 2 != 0) continue;
                var flare = ArcheryBoom(at, dir, width * 1.05f, Alpha(i == 0 ? ArcheryPale : light, 1), .24f, 10 + i * 3); flare.age = -(w - .02f + i * .025f);
            }
            Converge(tip, 6, 2.4f, light, w, .45f);
            // The line of the throw; awakened, it is a wide gold-white lane (the lance pierces everything along it).
            float lane = grade >= 3 ? 2.4f : 1;
            var sight = Line(tip, tip + dir * 6, .07f * lane, Alpha(grade >= 3 ? ArcheryAwake : light, .8f), w, 126); sight.fadeIn = w * .6f; sight.flicker = .3f; sight.hold = .85f; sight.fadePower = 2;
        }

        private void CastRainArrows(CastInfo c)
        {
            ArcheryBow(c, 2);
            float w = c.windup; int f = c.facing, grade = Grade; Color light = ArcheryLight(c.tint);
            Vector2 land = c.target, skyAt = land + new Vector2(-1, 6); float groundY = land.y - .7f, life = w + .75f;
            // The volley widens with its level (more arrows, a wider splash), so the sigil and the ground ring grow with it.
            float area = ArcheryLevel(1.12f, 1.25f); int arrows = 5 + 2 * (grade - 1);
            if (w < .2f)
            {
                // An instant echo: the sky sigil snaps open as the volley breaks through it; the spears follow at once.
                var quick = ArcheryFlat(Spawn("Sky sigil quick", runeRing, skyAt, 5.4f * area, Alpha(light, .95f), .45f, true, 127), .3f);
                quick.startScale = .4f; quick.endScale = 1; quick.ease = 7; quick.hold = .3f; quick.fadePower = 1.2f;
                ArcheryFlash(skyAt, 3f * area, Alpha(ArcheryGold, 1), .22f, 131, 0, .5f);
                return;
            }
            // the sky sigil the volley will break through, ringed by a turning bank of luminous cloud lit from the sigil
            ArcheryFlat(Underlay(skyAt + Vector2.up * .1f, 8.5f * area, .16f, life), .5f).fadeIn = .15f;
            var bank = Spawn("Sky cloud bank", ArcheryCloudFrames[0], skyAt + Vector2.up * .05f, 7.6f * area, ArcheryCloudLight, life, true, 125);
            bank.frames = ArcheryCloudFrames; bank.fps = 9; bank.loop = true; if (f < 0) { bank.scale.x = -bank.scale.x; bank.root.transform.localScale = bank.scale; }
            var skyGlow = ArcheryFlat(Spawn("Sky sigil glow", glow, skyAt, 7f * area, Alpha(light, .5f), life, true, 124), .36f);
            var skySeal = ArcheryFlat(Spawn("Sky sigil", runeRing, skyAt, 5.4f * area, Alpha(light, .95f), life, true, 127), .3f);
            var skyInner = ArcheryFlat(Spawn("Sky sigil core", sigil, skyAt, 3.2f * area, Alpha(ArcheryHot, .8f), life, true, 128), .3f);
            var skyLayers = new List<Clip> { bank, skyGlow, skySeal, skyInner };
            // Awakened, the volley carries frost: an ice-blue ring rims the sigil.
            if (grade >= 3) skyLayers.Add(ArcheryFlat(Spawn("Sky sigil frost", runeRing, skyAt, 6.3f * area, Alpha(ArcheryFrost, .85f), life, true, 126), .3f));
            foreach (var x in skyLayers) { x.fadeIn = .08f; x.startScale = .35f; x.endScale = 1; x.ease = 6; x.hold = .6f; x.fadePower = 1.2f; }
            bank.startScale = .55f; bank.fadeIn = .14f; bank.ease = 3; skyInner.pulse = .04f; skyGlow.pulse = .06f;
            // guide lines along every arrow's path, and the target ring on the ground
            for (int i = 0; i < arrows; i++)
            {
                Vector2 l = land + new Vector2((i - (arrows - 1) * .5f) * .55f, 0), s = l + new Vector2(-1, 6), d = (l - s).normalized;
                Vector2 end = s + d * ((groundY - s.y) / d.y);
                var guide = Line(s, end, .055f, Alpha(grade >= 3 && i % 2 == 0 ? ArcheryFrost : light, .7f), w + .12f, 126); guide.fadeIn = w * .7f; guide.flicker = .3f; guide.hold = .8f; guide.fadePower = 2;
            }
            ArcheryRainTarget(new Vector2(land.x, groundY), Mathf.Max(4.6f, (arrows - 1) * .55f + 2.2f) * area, light, w + .6f, grade >= 3);
            // release: a light arrow shot into the sky, the sigil bursting as the volley breaks through
            float r0 = w - .02f; Vector2 up = Direction(f > 0 ? 66 : 114);
            foreach (var x in ArcheryShape(null, ArcheryArrowSprite, c.pos + up * 1.4f, ArcheryAngle(up), 2.6f, .28f, Alpha(light, 1), 128)) { x.velocity = up * 28; x.age = -r0; x.fadePower = .6f; }
            var shotWake = ArcheryTurn(Spawn("Sky-bound wake", ArcheryWakeSprite, c.pos + up * .1f, 4.5f, Alpha(light, 1), .28f, true, 127), ArcheryAngle(up));
            shotWake.velocity = up * 28; shotWake.age = -r0; shotWake.scale.y *= .45f; shotWake.fadePower = .8f;
            ArcheryFlash(skyAt, 4f * area, Alpha(ArcheryGold, 1), .24f, 131, r0, .5f);
            var shock = LayFlat(ShockFlip(skyAt, 8f * area, Alpha(light, 1), .36f, 130), .3f); shock.age = -r0; shock.startScale = .3f; shock.endScale = 1; shock.ease = 4; shock.hold = .3f;
            var lines = ArcheryFlat(ImpactLines(skyAt, 7 * area, Alpha(light, .85f), .26f), .45f); lines.age = -r0; lines.angle = 0;
            ArcheryLater(r0, () => Blast(skyAt, 2.8f * area, Alpha(ArcheryGold, 1), .42f, 129));
        }

        /// <summary>The target marking of a rain volley: a rune ring turning on the floor over a soft glow, four wedge ticks closing
        /// in on it and (awakened) an ice-blue rim and chill mist.</summary>
        private void ArcheryRainTarget(Vector2 groundAt, float zoneWidth, Color light, float life, bool frost)
        {
            var zone = LayFlat(Spawn("Rain zone", runeRing, groundAt, zoneWidth, Alpha(light, .9f), life, true, 27), .3f); zone.spin = 40;
            var zoneGlow = ArcheryFlat(Spawn("Rain zone glow", glow, groundAt, zoneWidth * 1.15f, Alpha(light, .42f), life, true, 26), .32f);
            var zoneLayers = new List<Clip> { zone, zoneGlow };
            if (frost)
            {
                zoneLayers.Add(ArcheryFlat(Spawn("Rain zone frost", runeRing, groundAt, zoneWidth * 1.18f, Alpha(ArcheryFrost, .8f), life, true, 26), .3f));
                for (int i = 0, n = Budget(2); i < n; i++)
                {
                    var mist = Puff(groundAt + new Vector2(Range(-.4f, .4f) * zoneWidth, .2f), Range(1, 1.4f), new Color(.72f, .88f, 1, .75f), new Vector2(Range(-.3f, .3f), Range(.1f, .3f)), life - .1f, 112);
                    mist.fadeIn = .2f; mist.drag = 1; mist.startScale = .7f; mist.endScale = 1.4f;
                }
            }
            foreach (var x in zoneLayers) { x.fadeIn = .05f; x.startScale = .5f; x.endScale = 1; x.ease = 7; x.hold = .6f; x.fadePower = 1.2f; }
            // four ticks pointing in at the ring from left, right, front and back (squashed for the floor), snapping inward
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90 + 45; Vector2 d = Direction(angle); d.y *= .3f;
                var tick = Line(groundAt + d * zoneWidth * .62f, groundAt + d * zoneWidth * .44f, .12f, Alpha(i % 2 == 0 ? ArcheryHot : light, 1), life * .8f, 28);
                tick.velocity = -d * 1.2f; tick.drag = 5; tick.startScale = .5f; tick.endScale = 1; tick.ease = 6; tick.hold = .6f; tick.fadePower = 1.2f;
            }
        }

        // ---------------------------------------------------------------- arrows in flight

        private void ProjectileMultiShot(Transform root, Color tint, bool signature)
        {
            if (!signature) { PlainArrow(root); return; }
            Color light = ArcheryLight(tint); Vector2 at = root.position, dir = ArcheryAim(root.right, Vector2.right);
            // Bigger arrows at higher levels; awakened arrows pierce (a gold-white bloom and shock rings left behind).
            ArcheryLightArrow(root, tint, ArcheryLevel(1.1f, 1.2f), false, Grade >= 3, false);
            // Awakened arrows pass through their first body (the second stops them and strikes in full).
            if (Grade >= 3) ArcheryPassWatch(root, .16f, .08f, (p, d) => ArcheryArrowThrough(p, d, light));
            ArcheryBoom(at + dir * .7f, dir, 1.6f, Alpha(light, 1), .2f, 9);
        }

        private void ProjectileRainArrows(Transform root, Color tint, bool signature)
        {
            if (!signature) { PlainArrow(root); return; }
            Color light = ArcheryLight(tint); Vector2 at = root.position; int grade = Grade, spear = ArcheryRainSpears++;
            // Awakened volleys slow what they hit: their spears fall frosted (a snowflake on every other one).
            // Each spear is drawn a little ahead or behind its gameplay point and a touch bigger or smaller, so the volley
            // falls as rain rather than as a rigid row (its landings and hits stay where gameplay puts them).
            ArcheryLightArrow(root, tint, 1.05f * ArcheryLevel(1.06f, 1.12f) * Range(.92f, 1.08f), true, false, grade >= 3, Range(-.35f, .2f), spear % 2 == 0);
            if (grade < 2 || spear % 2 == 0)
            {
                // breaking through the sigil: a spark and a shock ring rippling across the sky plane
                var pop = SparkFlip(at, 1.3f, Alpha(grade >= 3 ? ArcheryFrost : ArcheryGold, 1), .14f, 131); LayFlat(pop, .5f);
                var ripple = LayFlat(ShockFlip(at, 2f, Alpha(light, 1), .26f, 130), .3f); ripple.startScale = .35f; ripple.endScale = 1; ripple.ease = 4;
            }
            // Rain arrows start six units above a landing point at enemy body height: misses bury themselves in the ground.
            float ground = at.y - 6.7f; ArcheryRainGround = ground;
            // streaks of light rain fall beside every spear, so the volley reads as a downpour (decoration only);
            // bigger volleys already bring more spears, so each spear brings fewer streaks
            Vector2 fall = ArcheryAim(root.right, Vector2.down); if (fall.y > -.3f) fall = Vector2.down;
            for (int i = 0, n = grade >= 3 ? 0 : grade == 2 ? 1 : Budget(2); i < n; i++)
            {
                Vector2 start = at + new Vector2(Range(-.9f, .9f) + (i == 0 ? -.35f : .35f), Range(-.7f, 0));
                float speed = 12 * Range(1, 1.2f), delay = Range(0, .16f), flight = Mathf.Max(.1f, (start.y - ground) / (-fall.y * speed));
                Vector2 land = start + fall * speed * flight;
                var drop = Line(start, start - fall * Range(1.8f, 2.6f), Range(.07f, .1f), Alpha(i == 0 ? ArcheryHot : light, 1), flight, 126);
                drop.velocity = fall * speed; drop.age = -delay; drop.fadeIn = .06f; drop.fadePower = .25f;
                var splash = SparkFlip(land + Vector2.up * .1f, .9f, Alpha(ArcheryGold, 1), .12f, 129); splash.age = -(delay + flight); LayFlat(splash, .6f);
            }
            Transform shotRoot = root; bool landed = false, lite = grade >= 2;
            var watch = Spawn("Rain arrow watch", glow, at, .1f, new Color(0, 0, 0, 0), 4.5f, true, 110);
            Emitter(watch, 60, 0, (owner, p) =>
            {
                if (landed) return;
                if (!shotRoot) { landed = true; owner.life = owner.age; return; }
                Vector2 now = shotRoot.position; if (now.y > ground) return;
                landed = true; owner.life = owner.age;
                ArcheryGroundBurst(new Vector2(now.x, ground), light, .8f, 0, false, ArcheryAngle(ArcheryAim(shotRoot.right, Vector2.down)), lite);
                Clip arrowClip; if (attached.TryGetValue(shotRoot, out arrowClip)) arrowClip.life = arrowClip.age;
            });
        }

        // A signature light arrow: a luminous bolt over its bloom, a tip glint, a painted comet wake over a soft glow wake, and
        // speed lines and afterimages shed while it flies. A piercing arrow (awakened fan) burns in a wider gold-white bloom and
        // leaves shock rings along its path; a frosted one (awakened volley) is ice-blue with a snowflake tip.
        private void ArcheryLightArrow(Transform root, Color tint, float size, bool falling, bool pierce, bool frost, float shift = 0, bool frostFlake = true)
        {
            // shift: the drawn arrow rides this far ahead of (or behind) the gameplay point along its flight, so a volley's spears
            // do not fall in one rigid row; landing and hits still come from the gameplay point.
            Color light = ArcheryLight(tint), tone = frost ? Color.Lerp(light, ArcheryFrostDeep, .75f) : light; float len = 2.2f * size;
            var hot = Attach(root, "Light arrow", ArcheryArrowSprite, len, Alpha(tone, 1), 125, true, new Vector2(shift, 0));
            var bloom = Attach(root, "Light arrow glow", ArcheryBloomSprite, len * 1.25f, Alpha(pierce ? Color.Lerp(tone, ArcheryAwake, .6f) : tone, pierce ? .7f : .55f), 120, true, new Vector2(shift + .05f * len, 0));
            bloom.scale.y *= pierce ? 1.9f : 1.4f;
            var layers = new List<Clip> { bloom };
            var tipStar = Attach(root, "Arrow tip star", glint, .8f * size, Alpha(ArcheryHot, 1), 126, true, new Vector2(.47f * len + shift, 0)); tipStar.spin = 500; layers.Add(tipStar);
            if (frost && frostFlake)
            {
                var crystal = Attach(root, "Frost tip flake", flake, .55f * size, Alpha(ArcheryFrost, 1), 127, true, new Vector2(.47f * len + shift, 0)); crystal.spin = 260; layers.Add(crystal);
            }
            var trails = new List<ArcheryTrailLayer>();
            ArcheryTrail(root, "Arrow wake glow", ArcheryBloomSprite, (falling ? 4.8f : 3.4f) * size, falling ? 1f : 1.3f, Alpha(tone, falling ? .26f : .4f), 116, true, .1f * size + shift, trails);
            ArcheryTrail(root, "Arrow wake", ArcheryWakeSprite, (falling ? 4.4f : 3f) * size, falling ? .3f : .36f, Alpha(tone, 1), 121, true, -.15f * size + shift, trails);
            foreach (var t in trails) layers.Add(t.clip);
            Vector2 launch = root.position; ArcheryGrowTrails(trails, 0);
            // Falling spears come in big volleys at higher levels, so each one sheds less.
            int beat = 0, every = falling ? (Grade >= 3 ? 7 : Grade == 2 ? 5 : 4) : 3; bool buried = false;
            Emitter(tipStar, 60, 0, (owner, p) =>
            {
                if (!root || buried) return;
                Vector2 heading = root.right;
                if (!falling)
                {
                    // An arrow of a fan that dips to the floor (the low arrows of a wide fan) buries itself there and bursts
                    // instead of sliding on through the stone.
                    Vector2 tip = (Vector2)root.position + heading * (.47f * len + shift);
                    if (heading.y < -.02f && tip.y < TowerWorld.Ground + .05f)
                    {
                        buried = true;
                        Clip arrowClip; if (attached.TryGetValue(root, out arrowClip)) arrowClip.life = arrowClip.age;
                        Vector2 entry = new Vector2(tip.x + (TowerWorld.Ground - tip.y) * heading.x / heading.y, TowerWorld.Ground);
                        ArcheryGroundBurst(entry, light, .75f * size, 0, false, ArcheryAngle(heading), true);
                        return;
                    }
                }
                ArcheryGrowTrails(trails, ((Vector2)root.position - launch).magnitude);
                if (++beat % every != 0) return;
                int step = beat / every;
                Vector2 d = heading, here = (Vector2)root.position + d * shift, n = new Vector2(-d.y, d.x);
                if (pierce && step % 4 == 2)
                {
                    var mark = ArcheryBoom(here - d * .2f * len, d, 1.1f * size, Alpha(step % 8 == 2 ? ArcheryAwake : light, 1), .22f, 0); mark.startScale = .5f; mark.endScale = 1.15f;
                }
                else if (frost && step % 3 == 1)
                {
                    var rime = Spawn("Frost fleck", fleck, here - d * Range(.3f, 1f) + n * Range(-.2f, .2f), Range(.18f, .3f) * size, Alpha(ArcheryFrost, 1), Range(.2f, .3f), true, 124);
                    rime.velocity = -d * Range(.5f, 1.5f); rime.drag = 2; rime.endScale = .3f; rime.ease = 1; rime.angle = Range(-20, 20);
                }
                else if (step % 3 == 0)
                {
                    // a drawn afterimage of the bolt a beat behind it
                    var ghost = ArcheryTurn(Spawn("Arrow afterimage", ArcheryArrowSprite, here - d * .35f * len, len, Alpha(tone, .45f), .1f, true, 115), ArcheryAngle(d));
                    ghost.endScale = 1.04f; ghost.ease = 1; ghost.fadePower = 1.2f;
                }
                else
                {
                    // a speed line peeling off the wake
                    Vector2 start = here - d * (len * .3f + Range(0, .4f)) + n * Range(-.28f, .28f) * size;
                    var line = Line(start, start - d * Range(1f, 1.7f) * size, Range(.04f, .06f), Alpha(step % 2 == 0 ? ArcheryHot : light, 1), Range(.12f, .17f), 123);
                    line.velocity = -d * Range(1, 3); line.hold = .2f; line.fadePower = 1.1f;
                }
            });
            Bundle(root, hot, layers.ToArray());
        }

        // Companions' basic shots: a real arrow with a short streak wake.
        private void PlainArrow(Transform root)
        {
            var primary = Attach(root, "Arrow", arrow, 1.25f, Color.white, 123, false);
            var tail = Attach(root, "Arrow wake", streak, 1.9f, new Color(1, .86f, .5f, .7f), 121, true, new Vector2(-.85f, 0)); tail.scale.y *= .26f;
            Bundle(root, primary, tail);
        }

        private void ProjectilePierceShot(Transform root, Color tint, bool signature)
        {
            Color light = ArcheryLight(tint); Vector2 at = root.position, dir = ArcheryAim(root.right, Vector2.right);
            // the lance rides a little ahead of the hit point so its haft never trails behind the thrower at launch;
            // higher levels throw a longer lance, and the awakened one (unlimited pierce, wider hit) is wider still and gold-white
            int grade = signature ? Grade : 1; float s = signature ? ArcheryLevel(1.12f, 1.28f) : 1, wide = grade >= 3 ? 1.3f : 1;
            float len = (signature ? 3.4f : 2.1f) * s, lead = (signature ? .5f : .3f) * s;
            var hot = Attach(root, "Light lance", ArcheryLanceSprite, len, Alpha(grade >= 3 ? Color.Lerp(light, ArcheryAwake, .5f) : light, 1), 126, true, new Vector2(lead, 0));
            hot.scale.y *= wide; hot.root.transform.localScale = hot.scale;
            var bloom = Attach(root, "Light lance glow", ArcheryBloomSprite, len * 1.2f, Alpha(grade >= 3 ? ArcheryAwake : light, grade >= 3 ? .7f : .55f), 119, true, new Vector2(lead + .05f * len, 0));
            bloom.scale.y *= 1.5f * wide; bloom.root.transform.localScale = bloom.scale;
            if (!signature)
            {
                var trail = Attach(root, "Lance wake", ArcheryWakeSprite, 2.6f, Alpha(light, .9f), 120, true, new Vector2(-1.2f, 0)); trail.scale.y *= .45f;
                Bundle(root, hot, bloom, trail); return;
            }
            var star = Attach(root, "Lance tip star", glint, 1.5f * s * wide, Alpha(ArcheryHot, 1), 128, true, new Vector2(.47f * len + lead, 0)); star.spin = 420; star.pulse = .12f;
            var halo = Attach(root, "Lance tip glow", glow, 1.4f * s * wide, Alpha(light, .55f), 124, true, new Vector2(.4f * len + lead, 0)); halo.pulse = .1f;
            var trails = new List<ArcheryTrailLayer>();
            // the long spindle beam behind the lance (painted light over a soft glow) wrapped in a painted wind helix
            var helix = ArcheryTrail(root, "Lance wind helix", ArcheryHelixFrames[0], 4.2f * s, 1.4f * s * wide, Alpha(Color.Lerp(light, ArcheryWind, .15f), 1), 122, true, .8f * s, trails).clip;
            helix.frames = ArcheryHelixFrames; helix.fps = 30;
            ArcheryTrail(root, "Lance beam glow", ArcheryBloomSprite, 6.8f * s, 1.5f * s * wide, Alpha(light, .42f), 116, true, .3f * s, trails);
            ArcheryTrail(root, "Lance beam", ArcheryWakeSprite, 6f * s, .42f * s * wide, Alpha(light, 1), 121, true, 0, trails);
            ArcheryTrail(root, "Lance beam core", streak, 4.4f * s, .09f * s, Alpha(ArcheryHot, 1), 123, true, .1f * s, trails);
            var bundle = new List<Clip> { bloom, star, halo };
            if (grade >= 3)
            {
                // Awakened: a wide gold-white corona round the whole flight path.
                ArcheryTrail(root, "Awakened lance corona", ArcheryBloomSprite, 7.6f * s, 2.6f * wide, Alpha(ArcheryAwake, .4f), 115, true, .5f * s, trails);
            }
            Vector2 launch = at, scarFrom = at + dir * .3f; ArcheryGrowTrails(trails, 0);
            int beat = 0;
            // about 40 clips a second: scar segments, shock rings, afterimages and needles
            Emitter(halo, 60, .15f, (owner, p) =>
            {
                if (!root) return;
                Vector2 now = root.position, d = owner.root.transform.right, here = owner.root.transform.position, n = new Vector2(-d.y, d.x);
                ArcheryGrowTrails(trails, (now - launch).magnitude);
                // a burning scar laid down behind the haft: it lingers and erodes after the lance has gone by
                Vector2 tail = now + d * (lead - .45f * len);
                if (Vector2.Dot(tail - scarFrom, d) > 1.2f) { ArcheryScar(scarFrom, tail, light); scarFrom = tail; }
                int phase = ++beat % 8;
                if (phase == 0)
                {
                    var spiral = ArcheryBoom(here - d * 1.4f * s, d, 2f * s * wide, Alpha(beat % 16 == 0 ? (grade >= 3 ? ArcheryAwake : ArcheryPale) : light, 1), .28f, 0); spiral.startScale = .55f; spiral.endScale = 1.2f;
                }
                else if (phase == 4)
                {
                    var ghost = ArcheryTurn(Spawn("Lance afterimage", ArcheryLanceSprite, here - d * (.4f * len + .2f), len, Alpha(light, .4f), .1f, true, 115), ArcheryAngle(d));
                    ghost.endScale = 1.05f; ghost.fadePower = 1.2f;
                }
                else if (phase % 2 == 0)
                {
                    var needle = Spark(p - d * Range(.6f, 2f), n * Range(-3, 3) - d * Range(1, 3), Chance(.3f) ? Color.white : light, Range(.3f, .45f), Range(.18f, .28f), 0, 2.5f); needle.hold = .25f;
                }
            });
            foreach (var t in trails) bundle.Add(t.clip);
            Bundle(root, hot, bundle.ToArray());
            // launch: a spark, a shock ring and a needle spray along the throw
            ArcheryFlash(at + dir * .6f, 1.8f * s, Alpha(ArcheryGold, 1), .16f, 131);
            ArcheryBoom(at + dir * .8f, dir, 2.4f * s, Alpha(ArcheryPale, .9f), .24f, 12);
            ArcheryNeedles(at + dir * .6f, dir, 5, 12, light, .24f, .45f, 18, 0, 0);
            // every body the lance passes through is struck in passing; when it reaches the end of its flight it breaks into light
            ArcheryPassWatch(root, grade >= 3 ? .34f : .16f, .08f, (p, d) => ArcheryLanceThrough(p, d, light, s * wide, grade >= 3));
            ArcheryWatch(root, 4.5f, last =>
            {
                if (Vector2.Dot(last - scarFrom, dir) > .3f) ArcheryScar(scarFrom, last, light);
                if (ArcheryRecent(ArcheryPierceHit, .12f, last, 1)) return;
                ArcheryFizzle(last, dir, light, s * wide, grade >= 3);
            });
        }

        // The lance reaching the end of its flight bursts into light: a warm haze kicked out, a blast billowing from a white-hot
        // heart, a shock ring, wedge lines, the lance's ghost driven on and needles, so the throw always ends on a beat.
        private void ArcheryFizzle(Vector2 pos, Vector2 dir, Color light, float s, bool awake)
        {
            // s: the lance's size for its level; an awakened lance ends in a gold-white burst.
            Vector2 head = pos + dir * 1.6f * s; float a = ArcheryAngle(dir);
            Color hot = awake ? Color.Lerp(ArcheryGold, ArcheryAwake, .4f) : ArcheryGold;
            Underlay(head, 2.6f * s, .2f, .25f);
            ArcheryHazePuffs(head, dir, .7f * s, ArcheryHaze, 2, .5f);
            Blast(head, 1.9f * s, Alpha(hot, 1), .34f, 130);
            ArcheryBoom(head, dir, 2.9f * s, Alpha(ArcheryPale, .9f), .28f, 6);
            ArcheryWedges(head + dir * .3f, dir, 3.4f * s, Alpha(light, .8f), .2f, .6f);
            foreach (var x in ArcheryShape(null, ArcheryLanceSprite, pos + dir * .5f, a, 3.4f * s, .2f, Alpha(light, .8f), 124)) { x.velocity = dir * 7; x.drag = 4; x.endScale = 1.25f; x.hold = .3f; x.fadePower = 1.1f; }
            ArcheryNeedles(head, dir, 8, 10 * s, light, .32f, .45f * s, 45);
        }

        private void TrailPierceShot(Vector2 from, Vector2 to, Color tint)
        {
            Color light = ArcheryLight(tint);
            Vector2 delta = to - from; float length = delta.magnitude; if (length < .05f) return;
            Vector2 dir = delta / length; float a = ArcheryAngle(dir);
            ArcheryTrailEnd = to; ArcheryTrailDir = dir;
            // an instant spindle beam: a soft glow, the painted beam with its white core, a painted wind helix along it
            var haze = ArcheryGlowLine(from, to, 1.6f, Alpha(light, .45f), .32f, 117); haze.axisEnd = new Vector2(1, .3f); haze.ease = 2; haze.fadePower = 1.3f;
            var beam = Line(from, to, .9f, Alpha(light, 1), .3f, 127, 1.06f); beam.axisEnd = new Vector2(1, .25f); beam.ease = 2; beam.hold = .25f; beam.fadePower = 1.1f;
            int segments = Mathf.Clamp(Mathf.CeilToInt(length / 2.2f), 1, 8);
            for (int i = 0; i < segments; i++)
            {
                var twist = Sheet("Beam wind helix", ArcheryHelixFrames, from + dir * (length * (i + .5f) / segments), length / segments * 1.2f, Alpha(Color.Lerp(light, ArcheryWind, .15f), 1), .3f, 128, 30, true);
                twist.angle = a; twist.scale.y *= 1.5f; twist.hold = .3f; twist.fadePower = 1.2f; twist.axisEnd = new Vector2(1, .5f);
            }
            int rings = Mathf.Clamp(Mathf.RoundToInt(length / 1.6f), 1, 6);
            for (int i = 0; i < rings; i++)
            {
                float t = (i + .5f) / rings;
                var spiral = ArcheryBoom(from + delta * t, dir, 1.7f, Alpha(i % 2 == 0 ? ArcheryPale : light, 1), .28f, 0); spiral.age = -t * .08f; spiral.startScale = .4f; spiral.endScale = 1.1f;
            }
            ArcheryFlash(from, 1.4f, Alpha(ArcheryGold, 1), .14f, 131);
            ArcheryFlash(to, 1.8f, Alpha(ArcheryGold, 1), .18f, 133);
            Vector2 side = new Vector2(-dir.y, dir.x);
            for (int i = 0, n = Budget(6); i < n; i++)
            {
                var needle = Spark(from + delta * Range(.1f, 1), side * Range(-3, 3) + dir * Range(1, 4), i % 3 == 0 ? Color.white : light, Range(.3f, .45f), Range(.2f, .32f), 0, 2.5f); needle.hold = .25f;
            }
        }

        // ---------------------------------------------------------------- hits and bursts

        private void ArrowBurst(Vector2 pos, float radius, Color tint, SkillEffect effect)
        {
            ArcheryStrike(pos, ArcheryLight(tint), Vector2.right, Mathf.Clamp(.55f + radius * .35f, .6f, 1.8f));
        }

        // A companion's basic arrow landing: a small spark, a shock ring and a few needles along the shot.
        private void ArcheryPrick(Vector2 pos, Color light, Vector2 dir)
        {
            SparkFlip(pos, 1f, Alpha(ArcheryGold, 1), .12f, 132);
            ArcheryBoom(pos + dir * .15f, dir, 1f, Alpha(ArcheryPale, .9f), .15f, 3);
            ArcheryNeedles(pos, dir, 3, 6.5f, light, .2f, .3f, 38, 0, 6);
        }

        // A signature arrow striking: a little warm haze kicked back, the spark flaring over a bloom, a shock ring punched out
        // across the shot, wedge lines leaning along it, a spindle of light driven on and needles thrown forward. Returns the
        // spark (marks the strike's moment).
        private Clip ArcheryStrike(Vector2 pos, Color light, Vector2 dir, float s)
        {
            Underlay(pos, 2.2f * s, .18f, .3f);
            ArcheryHazePuffs(pos, dir, .6f * s, ArcheryHaze, 2, .5f);
            var hot = ArcheryFlash(pos, 2.2f * s, Alpha(ArcheryGold, 1), .2f, 133);
            ArcheryBoom(pos + dir * .35f * s, dir, 2.3f * s, Alpha(ArcheryPale, .9f), .24f, 5);
            ArcheryWedges(pos + dir * .25f * s, dir, 3.6f * s, Alpha(light, .8f), .18f, .7f);
            var pierce = Line(pos, pos + dir * 2.4f * s, .2f * s, Alpha(light, 1), .16f, 131); pierce.startScale = .3f; pierce.endScale = 1; pierce.ease = 6; pierce.hold = .2f; pierce.fadePower = 1.1f; pierce.velocity = dir * 5;
            ArcheryNeedles(pos, dir, 9, 11 * s, light, .3f, .48f * s, 38, 0, 6);
            // an afterglow: embers drifting up out of the strike after the flash has gone
            for (int i = 0, n = ArcheryShare(4); i < n; i++)
            {
                var ember = Spark(pos + Inside(.35f * s), new Vector2(Range(-1.2f, 1.2f) + dir.x * 1.5f, Range(.8f, 2.4f)), i % 2 == 0 ? ArcheryPale : light, Range(.16f, .26f), Range(.5f, .8f), -1.5f, 1.4f);
                ember.flicker = .35f; ember.age = -Range(.05f, .15f);
            }
            return hot;
        }

        // A second or third arrow of the same volley landing on the same spot: a sharp beat on top of the strike, not another burst.
        private void ArcheryStrikeEcho(Vector2 pos, Color light, Vector2 dir)
        {
            SparkFlip(pos + Inside(.2f), 1.2f, Alpha(ArcheryGold, 1), .12f, 133);
            var pierce = Line(pos, pos + dir * Range(1.6f, 2.2f), .12f, Alpha(ArcheryHot, 1), .13f, 131); pierce.startScale = .25f; pierce.endScale = 1; pierce.ease = 6; pierce.fadePower = 1.2f; pierce.velocity = dir * 5;
            ArcheryNeedles(pos, dir, 4, 9, light, .24f, .38f, 45, 0, 6);
        }

        private void HitMultiShot(Vector2 pos, Color tint, bool signature, Vector2 direction)
        {
            Color light = ArcheryLight(tint);
            if (!signature) { ArcheryPrick(pos, light, direction); return; }
            ArcheryEmbed(pos, light, direction);
            if (Grade >= 3)
            {
                // Awakened arrows have already pierced one body: their light carries on straight through this one too.
                Vector2 end = pos + direction * 3.2f;
                var through = Line(pos, end, .45f, Alpha(ArcheryAwake, .9f), .2f, 127); through.axisStart = new Vector2(.3f, 1); through.axisEnd = new Vector2(1, .3f); through.ease = 5; through.hold = .2f; through.fadePower = 1.1f;
            }
            // the fan's arrows often land on one target within a few frames: the first strikes in full, the others echo it
            if (ArcheryRecent(ArcheryStrikeMark, .1f, pos, 1.1f)) { ArcheryStrikeEcho(pos, light, direction); return; }
            float s = ArcheryLevel(1.1f, 1.2f);
            ArcheryStrikeMark = ArcheryStrike(pos, light, direction, 1.15f * s);
        }

        /// <summary>A light arrow left standing in the target for a moment after it strikes, then eroding.</summary>
        private void ArcheryEmbed(Vector2 pos, Color light, Vector2 dir)
        {
            const float len = 1.7f;
            foreach (var x in ArcheryShape(null, ArcheryArrowSprite, pos - dir * (len * .47f - .4f) + Inside(.12f), ArcheryAngle(dir) + Range(-6, 6), len, .5f, Alpha(light, 1), 126, .4f))
            { x.startScale = 1.12f; x.endScale = 1; x.ease = 8; x.hold = .5f; x.fadePower = 1.2f; }
        }

        private void HitPierceShot(Vector2 pos, Color tint, bool signature, Vector2 direction)
        {
            Color light = ArcheryLight(tint);
            if (!signature) { ArcheryPrick(pos, light, direction); return; }
            ArcheryStrike(pos, light, direction, 1.35f);
            var shock = ShockFlip(pos + direction * .4f, 3.4f, Alpha(ArcheryHot, .9f), .3f, 132); shock.startScale = .45f; shock.endScale = 1.05f; shock.ease = 3;
            // the lance's light keeps going straight through the target as a spindle over a glow
            Vector2 end = pos + direction * 4.5f;
            var through = Line(pos, end, .7f, Alpha(light, 1), .22f, 127); through.axisStart = new Vector2(.3f, 1); through.axisEnd = new Vector2(1, .3f); through.ease = 5; through.hold = .25f; through.fadePower = 1.1f;
            var throughGlow = ArcheryGlowLine(pos, end, 1.5f, Alpha(light, .45f), .24f, 117); throughGlow.axisStart = new Vector2(.4f, 1); throughGlow.axisEnd = new Vector2(1, .35f); throughGlow.ease = 5; throughGlow.fadePower = 1.3f;
            ArcheryPierceHit = through;
            ArcheryNeedles(pos, direction, 5, 9, light, .4f, .45f, 70, 0, 6);
        }

        private void HitRainArrows(Vector2 pos, Color tint, bool signature, Vector2 direction)
        {
            Color light = ArcheryLight(tint);
            if (!signature) { ArcheryPrick(pos, light, direction); return; }
            float height = pos.y - ArcheryRainGround;
            float ground = !float.IsNaN(ArcheryRainGround) && height > -.2f && height < 3 ? ArcheryRainGround : pos.y - .9f;
            // the volley's first spear to strike also shows the whole splash: a blast, a ground shock as wide as the area, a dust
            // wall and a burn mark; the spears landing with it (a level-three volley brings nine) burst smaller on top of it
            bool first = !ArcheryRecent(ArcheryVolleyMark, .14f, pos, 3.5f);
            if (first) ArcheryVolleyMark = ArcheryVolleyBlast(new Vector2(pos.x, ground), light, ArcheryLevel(1.12f, 1.25f));
            // bigger volleys: every spear lands lite and the volley blast alone carries the splash
            ArcheryGroundBurst(new Vector2(pos.x, ground), light, 1.1f, .02f, first && Grade < 2, ArcheryAngle(direction), Grade >= 2);
            if (Grade >= 3) ArcheryFrostBite(pos, ground, first);
            if (first) ArcheryFlash(pos, 1.7f, Alpha(Grade >= 3 ? ArcheryFrost : ArcheryGold, 1), .14f, 133);
            else SparkFlip(pos, 1.2f, Alpha(Grade >= 3 ? ArcheryFrost : ArcheryGold, 1), .12f, 133);
            if (first) ArcheryWedges(pos, direction, 2.8f, Alpha(light, .85f), .2f, .6f);
            ArcheryNeedles(pos, -direction, first ? 5 : 2, 7, light, .26f, .38f, 70, 0, 6);
        }

        /// <summary>An awakened volley's spear biting with frost (it slows what it hits): an ice-blue snowflake on the body,
        /// ice shards and a frost bloom spreading on the floor; the first spear also leaves a chill mist over the splash.</summary>
        private void ArcheryFrostBite(Vector2 pos, float ground, bool first)
        {
            var star = Spawn("Frost bite flake", flake, pos, 1.1f, Alpha(ArcheryFrost, 1), .4f, true, 132); star.startScale = .4f; star.endScale = 1; star.ease = 7; star.spin = 120; star.hold = .35f; star.fadePower = 1.2f;
            if (first) Shards(pos, 4, 4.5f, .26f, Alpha(ArcheryFrost, 1), .45f);
            // Nine spears land within a tenth of a second: the frost bloom on the floor is laid by the first and every third.
            if (!first && ArcheryFrostBites++ % 3 != 0) return;
            Vector2 floor = new Vector2(pos.x, ground);
            var bloom = ArcheryFlat(Spawn("Frost bloom", sigil, floor + Vector2.up * .03f, 1.9f, Alpha(ArcheryFrost, .95f), .9f, true, 27), .3f);
            bloom.startScale = .3f; bloom.endScale = 1; bloom.ease = 7; bloom.hold = .45f; bloom.fadePower = 1.2f;
            if (!first) return;
            for (int i = 0, n = ArcheryShare(3); i < n; i++)
            {
                var mist = Puff(floor + new Vector2(Range(-2.2f, 2.2f), .25f), Range(1, 1.4f), new Color(.72f, .88f, 1, .75f), new Vector2(Range(-.5f, .5f), Range(.2f, .5f)), Range(.8f, 1.1f), 112);
                mist.fadeIn = .08f; mist.drag = 1.2f; mist.endScale = 2;
            }
        }

        /// <summary>A light spear landing on the ground: a glowing crack, the spear left standing, billowing dust and a squashed spark;
        /// the full version billows a small blast instead and adds a ground ring, sparks and (big) debris.</summary>
        private void ArcheryGroundBurst(Vector2 ground, Color light, float s, float delay, bool big, float angle, bool lite = false)
        {
            // lite: one of many spears of a level-two or level-three volley; it keeps the spark, the standing spear, the crack,
            // one dust puff and a needle, and drops the blast, ring, debris and most of the dust.
            Vector2 lift = ground + Vector2.up * .3f * s;
            Vector2 down = Direction(angle); if (down.y > -.2f) down = Direction(-80.5f);
            var seam = Decal(ground, crack, (big ? 2.2f : 1.3f) * s, Alpha(ArcheryGold, .85f), big ? 1f : .75f, true, 26); seam.age = -delay; seam.hold = .3f; seam.fadePower = 1.3f;
            float shaft = (big ? 1.75f : 1.5f) * s;
            foreach (var x in ArcheryShape(null, ArcheryArrowSprite, ground - down * (shaft * .47f - .12f * s), ArcheryAngle(down), shaft, big ? 1f : .7f, Alpha(light, 1), 125, .4f)) { x.age = -delay; x.hold = .5f; x.fadePower = 1.2f; }
            int dust = lite && !big ? 1 : ArcheryShare(big ? 4 : 2);
            for (int i = 0; i < dust; i++)
            {
                var puff = Puff(ground + new Vector2(Range(-.4f, .4f) * s, .1f), Range(.5f, .8f) * s, ArcheryDust, new Vector2((i % 2 == 0 ? 1 : -1) * Range(1.2f, 2.6f) * s, Range(.3f, .9f)), Range(.45f, .7f), 118);
                puff.age = -delay; puff.gravity = -.3f;
            }
            if (lite && !big)
            {
                var pop = SparkFlip(lift, 1.5f * s, Alpha(ArcheryGold, 1), .14f, 132); pop.age = -delay; LayFlat(pop, .62f);
                Spark(lift, Direction(Range(40, 140)) * Range(4, 8) * s, Color.white, Range(.3f, .45f), Range(.22f, .34f), 9, 2).age = -delay;
                return;
            }
            float width = (big ? 2.4f : 1.6f) * s;
            ArcheryLater(delay, () => Blast(ground + Vector2.up * width * .28f, width, Alpha(ArcheryGold, 1), big ? .42f : .34f, 128));
            var wave = ArcheryGroundShock(ground + Vector2.up * .05f, (big ? 4.6f : 2.8f) * s, Alpha(light, 1), .3f); wave.age = -delay; wave.ease = 5;
            for (int i = 0, n = ArcheryShare(big ? 7 : 3); i < n; i++)
                Spark(lift, Direction(Range(30, 150)) * Range(4, 9) * s, i % 3 == 0 ? Color.white : light, Range(.3f, .45f), Range(.22f, .4f), 9, 2).age = -delay;
            if (big)
                for (int i = 0, n = ArcheryShare(4); i < n; i++)
                {
                    var chip = Spawn("Spear debris", rock[i % rock.Length], lift, Range(.1f, .2f) * s, Color.white, Range(.5f, .8f), false, 128);
                    chip.age = -delay; chip.velocity = new Vector2(Range(-3.5f, 3.5f), Range(3, 6.5f)) * s; chip.gravity = 18; chip.spin = Range(-600, 600); chip.fadePower = .6f; chip.hold = .5f;
                }
        }

        /// <summary>The splash of a rain volley: a golden blast billowing up from the ground, a flattened spark and wedge lines as wide
        /// as the splash area (radius about 3.5), a shock front on its rim, a dust wall rolling out both ways, a burn mark with a
        /// glowing seam and needles thrown up. Returns its spark, which marks the volley's moment.</summary>
        private Clip ArcheryVolleyBlast(Vector2 ground, Color light, float area)
        {
            // area: the splash's growth with the volley's level (1, 1.12, 1.25 of its 3.5 radius).
            Vector2 lift = ground + Vector2.up * .5f;
            ArcheryFlat(Underlay(ground + Vector2.up * .3f, 6f * area, .22f, .4f), .5f);
            var star = ArcheryFlash(lift, 3.4f * area, Alpha(ArcheryGold, 1), .22f, 132, 0, .55f);
            Blast(ground + Vector2.up * .85f * area, 3f * area, Alpha(ArcheryGold, 1), .46f, 129);
            // the outer shock lands on the splash radius
            float reach = 3.5f * area * 2 / .86f;
            var shock = LayFlat(ShockFlip(ground + Vector2.up * .05f, reach, Alpha(light, 1), .42f, 130), .3f); shock.startScale = .2f; shock.endScale = 1; shock.ease = 4; shock.hold = .3f;
            var lines = ArcheryFlat(ImpactLines(ground + Vector2.up * .3f, 6.8f * area, Alpha(light, .8f), .26f), .45f); lines.angle = 0; lines.startScale = .6f; lines.ease = 6; lines.hold = .2f; lines.fadePower = 1.1f;
            var burn = Decal(ground, scorch, 4.4f * area, new Color(.42f, .22f, .08f, .45f), 1.4f, false, 24); burn.angle = Range(-4, 4);
            var seam = Decal(ground, crack, 3.8f * area, Alpha(light, .9f), .7f, true, 26); seam.hold = .3f; seam.fadePower = 1.2f;
            for (int i = 0, n = ArcheryShare(6); i < n; i++)
            {
                float side = i % 2 == 0 ? 1 : -1, t = (i / 2) / 2f;
                var dust = Puff(ground + new Vector2(side * Range(.2f, 1f), .15f), Range(.8f, 1.2f), ArcheryDust, new Vector2(side * Mathf.Lerp(3, 7, t) * Range(.85f, 1.1f), Range(.3f, 1f)), Range(.6f, .9f), 118);
                dust.gravity = -.4f; dust.endScale = 2.4f; dust.drag = 2.4f;
            }
            ArcheryNeedles(lift, Vector2.up, 6, 8, light, .5f, .45f, 60, 0, 7);
            return star;
        }

        private void ImpactMultiShot(Vector2 pos, float radius, Color tint, int facing)
        {
            Color light = ArcheryLight(tint); float r = Mathf.Clamp(radius, .7f, 3.5f), s = Mathf.Lerp(.75f, 1.6f, (r - .7f) / 2.8f);
            ArcheryStrike(pos, light, new Vector2(facing == 0 ? 1 : facing, 0), s);
            // a fan of light arrows bursting out of the strike
            for (int i = 0, n = Budget(5); i < n; i++)
            {
                float angle = facing == 0 ? i * 360f / n + Range(-10, 10) : (facing > 0 ? 0 : 180) + (i - (n - 1) * .5f) * 14;
                Vector2 d = Direction(angle);
                foreach (var x in ArcheryShape(null, ArcheryArrowSprite, pos + d * .5f * r, angle, 1.5f * s, .3f, Alpha(light, 1), 128, .35f)) { x.velocity = d * 9 * s; x.drag = 3; x.fadePower = 1.2f; x.hold = .35f; }
            }
        }

        private void ImpactPierceShot(Vector2 pos, float radius, Color tint, int facing)
        {
            Color light = ArcheryLight(tint); float r = Mathf.Clamp(radius, .6f, 3.5f), s = .55f + r * .35f;
            Vector2 dir = facing != 0 ? new Vector2(facing, 0) : (ArcheryTrailEnd - pos).sqrMagnitude < .5f ? ArcheryTrailDir : Vector2.right;
            ArcheryStrike(pos, light, dir, s);
            ArcheryArc(pos + dir * .3f * s, 2.2f * s, ArcheryAngle(dir), Alpha(light, 1), .22f, 0, dir * 3, .6f);
        }

        private void ImpactRainArrows(Vector2 pos, float radius, Color tint, int facing)
        {
            Color light = ArcheryLight(tint);
            float r = Mathf.Clamp(radius, .7f, 3.5f), groundY = pos.y - .8f;
            Vector2 center = new Vector2(pos.x, groundY);
            ArcheryRainTarget(center, r * 2.6f, light, 1.1f, false);
            int n = Budget(Mathf.Clamp(Mathf.RoundToInt(3 + r * 1.2f), 4, 7));
            Vector2 vel = new Vector2(2.4f, -16); float fall = .2f, angle = ArcheryAngle(vel);
            for (int i = 0; i < n; i++)
            {
                float delay = i * .045f + Range(0, .025f), x = ((i * .618f) % 1 * 2 - 1) * r * .85f;
                Vector2 land = new Vector2(pos.x + x, groundY), start = land - vel * fall;
                foreach (var c in ArcheryShape(null, ArcheryArrowSprite, start, angle, 1.9f, fall, Alpha(light, 1), 128)) { c.velocity = vel; c.age = -delay; c.fadePower = 0; }
                var wake = ArcheryTurn(Spawn("Falling spear wake", ArcheryWakeSprite, start - vel.normalized * 1.6f, 3.6f, Alpha(light, 1), fall, true, 127), angle);
                wake.scale.y *= .4f; wake.velocity = vel; wake.age = -delay; wake.fadePower = 0;
                ArcheryGroundBurst(land, light, Mathf.Lerp(.75f, 1, r / 3.5f), delay + fall, false, angle, true);
            }
            var shock = ArcheryGroundShock(center + Vector2.up * .05f, r * 3.4f, Alpha(light, 1), .45f); shock.age = -.2f; shock.ease = 5;
        }

        // ---------------------------------------------------------------- the celestial sentinel

        private void CastSentry(CastInfo c)
        {
            float w = c.windup; int f = c.facing; Color sky = Color.Lerp(ArcherySky, new Color(c.tint.r, c.tint.g, c.tint.b, 1), .12f);
            Vector2 feet = c.pos + Vector2.down, hand = c.pos + new Vector2(f * .45f, .15f);
            ArcherySentryFacing = f;
            if (w < .2f)
            {
                // An instant echo: a beam drops where each sentinel lands; the sentinels boot up at once.
                for (int i = 0; i < (Grade >= 3 ? 2 : 1); i++)
                {
                    Vector2 site = new Vector2(c.pos.x + (i == 0 ? -f : f) * 1.1f, feet.y);
                    var beam = Pillar(site + Vector2.up * 10, 1f, 10, Alpha(ArcherySkyHot, 1), .16f, 130); beam.angle = 180;
                    ArcheryFlash(site + Vector2.up * 1.3f, 1.8f, Alpha(sky, 1), .16f, 132);
                }
                return;
            }
            // the summoning hand
            var handStar = Anchored(c.anchor, hand, "Sentry hand star", glint, 1.3f, Alpha(ArcherySkyHot, 1), w + .1f, 129);
            handStar.startScale = .2f; handStar.endScale = 1; handStar.ease = 1.5f; handStar.spin = 200; handStar.hold = .8f;
            var handGlow = Anchored(c.anchor, hand, "Sentry hand glow", glow, 1.4f, Alpha(sky, .65f), w + .1f, 126);
            handGlow.startScale = .3f; handGlow.endScale = 1; handGlow.ease = 1.5f; handGlow.hold = .8f; handGlow.pulse = .1f; handGlow.fadeIn = .05f;
            Converge(hand, 8, 2f, sky, w, .4f);
            // The sentinel deploys behind the hero; higher levels draw a bigger circle and a taller column, and an awakened cast
            // deploys a second sentinel on the other side of the hero a beat later.
            float s = ArcheryLevel(1.1f, 1.2f);
            ArcherySentrySite(new Vector2(c.pos.x - f * 1.1f, feet.y), sky, w, s, 0);
            if (Grade >= 3) ArcherySentrySite(new Vector2(c.pos.x + f * 1.1f, feet.y), sky, w, s, .06f);
        }

        /// <summary>One sentinel's deployment: a circle turning on the floor over its glow, a targeting laser and a summoning ring,
        /// then at the release a beam slamming down and a column of light (a glow, the painted column and a white spine) erupting
        /// where it lands, with a small blast at its foot. <paramref name="lag"/> delays the landing.</summary>
        private void ArcherySentrySite(Vector2 site, Color sky, float w, float s, float lag)
        {
            int grade = Grade;
            float circleLife = w + .9f;
            var circle = LayFlat(Spawn("Sentry circle", runeRing, site, 2.85f * s, Alpha(sky, .95f), circleLife, true, 27), .3f); circle.spin = 55;
            var circleGlow = ArcheryFlat(Spawn("Sentry circle glow", glow, site, 3.4f * s, Alpha(sky, .5f), circleLife, true, 26), .32f);
            var lattice = ArcheryFlat(Spawn("Sentry lattice", hex, site, 2.2f * s, Alpha(sky, .5f), circleLife, true, 26), .3f);
            foreach (var x in new[] { circle, circleGlow, lattice }) { x.fadeIn = .04f; x.startScale = .3f; x.endScale = 1; x.ease = 7; x.hold = .6f; x.fadePower = 1.2f; }
            float spread = 1.6f * s;
            Emitter(circle, 12, 0, (owner, p) =>
            {
                var bit = Spawn("Sentry data mote", fleck, new Vector2(site.x + Range(-spread, spread), site.y + Range(-.15f, .15f)), Range(.12f, .22f), Alpha(Chance(.3f) ? ArcherySkyHot : sky, 1), Range(.35f, .6f), true, 128);
                bit.velocity = new Vector2(0, Range(1.4f, 3)); bit.fadeIn = .05f; bit.hold = .4f; bit.endScale = .4f; bit.ease = 1; bit.flicker = .3f;
            });
            // a targeting laser from the sky and a summoning ring above the site
            var laser = Pillar(site, .3f, 9, Alpha(sky, .75f), w, 126); laser.flicker = .3f; laser.fadeIn = w * .4f; laser.hold = .8f; laser.fadePower = 2;
            var halo = Spawn("Sentry summoning ring", runeRing, site + Vector2.up * 2.4f, 1.9f * s, Alpha(sky, .9f), w + .2f, true, 125);
            var halos = new List<Clip> { halo };
            // Awakened: a gold-white ring turns the other way inside the summoning ring.
            if (grade >= 3) halos.Add(Spawn("Sentry summoning ring gilt", runeRing, site + Vector2.up * 2.4f, 1.35f, Alpha(ArcheryAwake, .9f), w + .2f, true, 126));
            foreach (var x in halos) { x.spin = x == halos[halos.Count - 1] && grade >= 3 ? -220 : 160; x.startScale = .2f; x.endScale = 1; x.ease = 5; x.hold = .7f; x.fadeIn = .04f; x.fadePower = 1.3f; }
            var haloGlow = Spawn("Sentry summoning glow", glow, site + Vector2.up * 2.4f, 2.4f * s, Alpha(sky, .45f), w + .2f, true, 124); haloGlow.startScale = .3f; haloGlow.endScale = 1; haloGlow.ease = 3; haloGlow.hold = .7f; haloGlow.fadePower = 1.3f;
            // release: a beam slams down from the sky and a column of light erupts where the sentinel lands
            float r0 = w - .02f + lag; Vector2 body = site + Vector2.up * 1.15f;
            var descent = Pillar(site + Vector2.up * 10, 1.1f * s, 10, Alpha(ArcherySkyHot, 1), .16f, 130); descent.angle = 180; descent.age = -(r0 - .06f);
            var column = Pillar(site, 1.5f * s, 7 * s, Alpha(grade >= 3 ? Color.Lerp(sky, ArcheryAwake, .5f) : sky, 1), .36f, 128); column.age = -r0; column.hold = .25f; column.fadePower = 1.1f;
            var spine = Pillar(site, .5f * s, 8 * s, Alpha(ArcherySkyHot, 1), .26f, 129); spine.age = -r0; spine.hold = .2f; spine.fadePower = 1.2f;
            // the column's wide glow stays behind the actors so the hero between two sentinels is never washed out
            var columnGlow = Spawn("Sentry column glow", glow, site + Vector2.up * 2.8f * s, 2.6f * s, Alpha(sky, .45f), .45f, true, 28);
            columnGlow.scale.y *= 2.6f; columnGlow.age = -r0; columnGlow.startScale = .5f; columnGlow.endScale = 1.1f; columnGlow.ease = 3; columnGlow.fadePower = 1.4f;
            ArcheryFlash(body, 2.3f * s, Alpha(sky, 1), .2f, 132, r0);
            ArcheryLater(r0, () => Blast(site + Vector2.up * .55f * s, 2f * s, Alpha(sky, 1), .34f, 127));
            var wave = ArcheryGroundShock(site + Vector2.up * .05f, 5.2f * s, Alpha(sky, 1), .36f); wave.age = -r0; wave.ease = 5;
            ArcheryNeedles(body, Vector2.up, 8, 9, sky, .38f, .45f, 75, r0, 8);
            for (int i = 0, n = ArcheryShare(3); i < n; i++)
            {
                var dust = Puff(site + new Vector2(Range(-.5f, .5f), .1f), Range(.55f, .85f), ArcheryDust, new Vector2((i % 2 == 0 ? 1 : -1) * Range(1.4f, 2.8f), Range(.2f, .7f)), Range(.45f, .65f), 118);
                dust.age = -r0; dust.gravity = -.3f;
            }
        }

        private void ZoneSentry(Transform root, float radius, float life)
        {
            Color sky = ArcherySky; int grade = Grade;
            float hold = Mathf.Clamp01(1 - .45f / life), pop = ArcheryPop(life, .12f);
            // The sentinel stands beside the caster for the whole zone, so it floats a little further out and is drawn behind the
            // actors (orders 25-29, above ground marks and pickups): the caster stays readable. Gameplay fires its tracers from
            // root + 0.4 (the area's centre plus one), so the crystal floats low enough that its eye sits at that height.
            // An awakened cast deploys two in the same frame, one either side of the hero: each floats away from the hero.
            int side = ArcherySentryFacing; float back = -side * .25f;
            var game = ArcheryGame();
            if (game != null)
            {
                // The caster is the party member nearest the sentinel: it floats away from them and first looks where they face.
                TowerGame.Fighter caster = null; float best = 3;
                var party = new List<TowerGame.Fighter>(game.allies); if (game.hero != null) party.Add(game.hero);
                foreach (var member in party)
                {
                    if (member == null || !member.root) continue;
                    float d = Mathf.Abs(member.pos.x - root.position.x);
                    if (d < best) { best = d; caster = member; }
                }
                if (caster != null) { side = caster.facing < 0 ? -1 : 1; back = (root.position.x >= caster.pos.x ? 1 : -1) * .25f; }
            }
            ArcheryRigs.RemoveAll(x => !x.root || !clips.Contains(x.body));
            ArcherySentryRig partner = null;
            foreach (var other in ArcheryRigs) if (other.frame == Time.frameCount && Mathf.Abs(other.root.position.x - root.position.x) < 4) partner = other;
            if (partner != null)
            {
                float mid = (partner.root.position.x + root.position.x) * .5f;
                back = (root.position.x >= mid ? 1 : -1) * .25f;
                partner.back = (partner.root.position.x >= mid ? 1 : -1) * .25f;
                partner.paired = true;
            }
            Vector2 floorAt = new Vector2(back, -.72f), hover = new Vector2(back, .3f), eyeAt = new Vector2(back, .42f);
            // Higher levels: a wider halo wheel round the crystal; awakened, a gold-white ring turning the other way inside it.
            // The wheel and floor ring stay close round the crystal so a hero standing between two sentinels stays clear.
            float wheelSize = ArcheryLevel(1.12f, 1.22f);
            var body = Attach(root, "Sentinel crystal", ArcherySentinelSprite, 1.7f, Color.white, 28, false, hover);
            // Its eye is an energy core: a small plasma orb churning inside the crystal, in a wide glow.
            var eye = PlasmaOrb(root, "Sentinel core", .78f, Alpha(ArcherySkyPlasma, .9f), 29, eyeAt);
            var heart = Attach(root, "Sentinel aura", glow, 2.9f, Alpha(sky, .5f), 27, true, eyeAt); heart.pulse = .1f;
            var wheel = Attach(root, "Sentinel halo wheel", runeRing, 1.7f * wheelSize, Alpha(sky, .75f), 26, true, eyeAt); wheel.spin = 45;
            // The floor seal lies on a squashed plane of its own, so its rune ring and lattice turn in the ground plane.
            var floorPlane = new GameObject("Sentinel floor plane").transform; floorPlane.SetParent(root, false);
            floorPlane.localPosition = floorAt; floorPlane.localScale = new Vector3(1, .3f, 1);
            var floor = Attach(floorPlane, "Sentinel floor ring", runeRing, 2.4f, Alpha(sky, .85f), 27); floor.spin = 32;
            var lattice = Attach(floorPlane, "Sentinel floor lattice", hex, 1.9f, Alpha(sky, .4f), 26); lattice.flicker = .2f; lattice.spin = -18;
            var floorGlow = ArcheryFlat(Attach(root, "Sentinel floor glow", glow, 3.1f, Alpha(sky, .45f), 26, true, floorAt), .32f); floorGlow.pulse = .06f;
            var scan = Attach(root, "Sentinel scan cone", ArcheryScanSprite, 5.5f, Alpha(sky, .15f), 27, true, eyeAt);
            var mounts = new List<Clip> { wheel };
            if (grade >= 3)
            {
                var gilt = Attach(root, "Sentinel gilt ring", runeRing, 1.45f, Alpha(ArcheryAwake, .8f), 27, true, eyeAt); gilt.spin = -110;
                mounts.Add(gilt);
            }
            // Three crystal shards circle the core on a tilted ring.
            var satellitePlane = new GameObject("Sentinel satellite plane").transform; satellitePlane.SetParent(root, false);
            satellitePlane.localPosition = eyeAt; satellitePlane.localScale = new Vector3(1, .34f, 1);
            var satellites = Attach(satellitePlane, "Sentinel satellites", core, core.bounds.size.x, Color.clear, 0); satellites.spin = 140;
            var layers = new List<Clip> { body, eye, heart, floor, lattice, floorGlow, scan, satellites }; layers.AddRange(mounts);
            for (int i = 0; i < 3; i++)
            {
                var satellite = Attach(satellites.root.transform, "Sentinel satellite", shard, .34f, Alpha(sky, 1), 29, true, Direction(i * 120) * .95f);
                satellite.angle = i * 120 + 90; satellite.pulse = .06f; layers.Add(satellite);
            }
            foreach (var l in layers) { l.life = life; l.hold = hold; l.fadePower = 1.1f; l.fadeIn = .06f; }
            body.startScale = .3f; body.endScale = 1; body.ease = pop;
            foreach (var l in new[] { floor, lattice, floorGlow }) { l.startScale = .3f; l.endScale = 1; l.ease = pop * .6f; }
            foreach (var l in mounts) { l.startScale = .3f; l.endScale = 1; l.ease = pop * .6f; }
            var rig = new ArcherySentryRig { root = root, floorPlane = floorPlane, body = body, eye = eye, scan = scan, heart = heart, baseScale = body.scale, eyeAt = eyeAt, side = side,
                back = back, paired = partner != null, frame = Time.frameCount, mounts = mounts.ToArray(), floors = new[] { floorGlow } };
            scan.angle = rig.side > 0 ? 0 : 180;
            ArcheryRigs.Add(rig);
            Emitter(floorGlow, 60, 0, (owner, p) =>
            {
                if (!rig.root) return;
                float t = owner.age, since = t - rig.fireAt, kick = Mathf.Exp(-Mathf.Max(0, since) * 18);
                float bob = Mathf.Sin(t * 2.4f) * .05f;
                rig.eyeAt = new Vector2(rig.back, .42f);
                body.offset = new Vector2(rig.back, .3f + bob); eye.offset = heart.offset = scan.offset = rig.eyeAt + new Vector2(0, bob);
                if (satellitePlane) satellitePlane.localPosition = rig.eyeAt + new Vector2(0, bob);
                if (rig.floorPlane) rig.floorPlane.localPosition = new Vector2(rig.back, -.72f);
                foreach (var m in rig.mounts) m.offset = rig.eyeAt;
                foreach (var m in rig.floors) m.offset = new Vector2(rig.back, -.72f);
                body.scale = new Vector3(rig.baseScale.x * (1 + .14f * kick), rig.baseScale.y * (1 - .08f * kick), 1);
                // At rest a sentinel scans outward, away from the hero (so a pair looks out both ways); after a burst it follows
                // its target a moment, the cone flaring only for a quarter second after each shot.
                float rest = rig.paired ? (rig.back > 0 ? 0 : 180) : (rig.side > 0 ? 0 : 180);
                float idle = rest + Mathf.Sin(t * 1.5f) * 22;
                scan.angle = Mathf.LerpAngle(scan.angle, since < .7f ? rig.aim : idle, since < .7f ? .5f : .08f);
                scan.color = Alpha(sky, since < .25f ? .5f : .15f);
                eye.color = Alpha(ArcherySkyPlasma, since < .15f ? 1 : .85f);
                heart.color = Alpha(sky, since < .2f ? .8f : .5f);
                if (Chance(.1f))
                {
                    Vector2 at = rig.root.position;
                    var bit = Spawn("Sentinel data mote", fleck, at + new Vector2(rig.back + Range(-.9f, .9f), -.7f), Range(.12f, .2f), Alpha(Chance(.3f) ? ArcherySkyHot : sky, 1), Range(.5f, .8f), true, 128);
                    bit.velocity = new Vector2(0, Range(1.2f, 2.4f)); bit.fadeIn = .1f; bit.hold = .4f; bit.endScale = .4f; bit.ease = 1; bit.flicker = .3f;
                }
            });
            layers.Remove(body);
            Bundle(root, body, layers.ToArray());
            // boot-up: the eye snaps on and a shock ring runs across the floor
            Vector2 origin = root.position;
            var boot = Glint(origin + eyeAt, 1.6f, Alpha(ArcherySkyHot, 1), .3f); boot.age = -.1f;
            var wave = ArcheryGroundShock(origin + floorAt + Vector2.up * .05f, 3f, Alpha(sky, 1), .36f); wave.ease = 5;
        }

        private void ProjectileSentry(Transform root, Color tint, bool signature)
        {
            Color sky = Color.Lerp(ArcherySky, new Color(tint.r, tint.g, tint.b, 1), .12f);
            Vector2 at = root.position, dir = ArcheryAim(root.right, Vector2.right); float a = ArcheryAngle(dir);
            int k = ArcherySentryShots++ & 3;
            float lateral = signature ? (k - 1.5f) * .12f : 0, delay = signature ? k * .035f : 0;
            if (!signature)
            {
                var spark = Attach(root, "Sentry tracer core", core, .26f, Alpha(ArcherySkyHot, 1), 125, true, new Vector2(.1f, 0));
                var tail = Attach(root, "Sentry tracer streak", ArcheryWakeSprite, 1.5f, Alpha(sky, 1), 121, true, new Vector2(-.65f, 0)); tail.scale.y *= .3f;
                Bundle(root, spark, tail); return;
            }
            // each tracer is a churning plasma head in a glow at the front of a painted comet wake over a soft glow wake
            float bolt = ArcheryLevel(1.1f, 1.2f);
            var primary = PlasmaOrb(root, "Sentry tracer head", .4f * bolt, Alpha(sky, 1), 125, new Vector2(.05f, lateral));
            var layers = new List<Clip>();
            var halo = Attach(root, "Sentry tracer glow", glow, 1f * bolt, Alpha(sky, .6f), 122, true, new Vector2(0, lateral)); layers.Add(halo);
            var trails = new List<ArcheryTrailLayer>();
            ArcheryTrail(root, "Sentry tracer", ArcheryWakeSprite, 2.4f * bolt, .42f, Alpha(sky, 1), 121, true, .08f, trails);
            ArcheryTrail(root, "Sentry tracer glow trail", ArcheryBloomSprite, 2.8f * bolt, 1.2f, Alpha(sky, .4f), 117, true, .15f, trails);
            foreach (var t in trails) { t.clip.offset.y = lateral; layers.Add(t.clip); }
            Vector2 launch = at; ArcheryGrowTrails(trails, 0);
            foreach (var l in layers) l.age = -delay;
            primary.age = -delay;
            Emitter(primary, 60, 0, (owner, p) => { if (root) ArcheryGrowTrails(trails, ((Vector2)root.position - launch).magnitude); });
            Bundle(root, primary, layers.ToArray());
            // the sentinel that fired turns its gaze to the target and kicks
            ArcheryRigs.RemoveAll(x => !x.root || !clips.Contains(x.body));
            ArcherySentryRig rig = null; float best = 1.2f;
            foreach (var candidate in ArcheryRigs)
            {
                float distance = Vector2.Distance((Vector2)candidate.root.position + new Vector2(0, .4f), at);
                if (distance < best) { best = distance; rig = candidate; }
            }
            if (rig != null) { rig.fireAt = rig.body.age; rig.aim = a; rig.side = dir.x >= 0 ? 1 : -1; }
            // one muzzle burst per volley, at the sentinel's eye when it can be found
            Vector2 m = (rig != null ? (Vector2)rig.root.position + rig.eyeAt : at) + dir * .3f;
            if (ArcheryRecent(ArcherySentryMuzzle, .07f, m, .5f)) return;
            ArcherySentryMuzzle = ArcheryFlash(m, 1.5f, Alpha(sky, 1), .14f, 131);
            ArcheryBoom(m + dir * .25f, dir, 1.7f, Alpha(sky, .95f), .2f, 6);
            ArcheryNeedles(m, dir, 3, 9, sky, .18f, .34f, 22, 0, 2);
        }

        private void HitSentry(Vector2 pos, Color tint, bool signature, Vector2 direction)
        {
            Color sky = Color.Lerp(ArcherySky, new Color(tint.r, tint.g, tint.b, 1), .12f);
            bool lead = !ArcheryRecent(ArcherySentryHit, .06f, pos, .6f);
            if (!lead || !signature)
            {
                // a follow-up tracer of the same burst: a crisp spark
                SparkFlip(pos + Inside(.2f), .9f, Alpha(sky, 1), .1f, 132);
                ArcheryNeedles(pos, direction, 2, 6, sky, .18f, .3f, 50, 0, 5);
                return;
            }
            ArcherySentryHit = ArcheryFlash(pos, 1.6f, Alpha(sky, 1), .15f, 132);
            ArcheryBoom(pos + direction * .2f, direction, 1.8f, Alpha(sky, .95f), .2f, 3);
            ArcheryWedges(pos + direction * .2f, direction, 2.4f, Alpha(sky, .8f), .16f, .6f);
            ArcheryNeedles(pos, direction, 4, 8, sky, .24f, .36f, 45, 0, 5);
        }

        private void ImpactSentry(Vector2 pos, float radius, Color tint, int facing)
        {
            Color sky = Color.Lerp(ArcherySky, new Color(tint.r, tint.g, tint.b, 1), .12f);
            float r = Mathf.Clamp(radius, .7f, 3.5f), s = .6f + r * .3f;
            Vector2 ground = new Vector2(pos.x, pos.y - .8f);
            // an orbital strike: a beam drops from the sky, a column of light bursts up and a blast billows at its foot
            var beam = Pillar(ground + Vector2.up * 9, 1.1f * s, 9, Alpha(ArcherySkyHot, 1), .16f, 130); beam.angle = 180;
            var column = Pillar(ground, 2f * s, 5.5f * s, Alpha(sky, 1), .36f, 128); column.age = -.06f; column.hold = .25f; column.fadePower = 1.1f;
            var columnGlow = Spawn("Orbital column glow", glow, ground + Vector2.up * 2.2f * s, 3f * s, Alpha(sky, .5f), .45f, true, 126); columnGlow.scale.y *= 2.2f; columnGlow.age = -.06f; columnGlow.fadePower = 1.4f;
            ArcheryLater(.06f, () => Blast(ground + Vector2.up * .7f * s, 2.4f * s, Alpha(sky, 1), .36f, 127));
            ArcheryFlash(pos, 2.4f * s, Alpha(sky, 1), .18f, 132, .06f);
            ArcheryWedges(pos, Vector2.up, 4.2f * s, Alpha(sky, .8f), .24f, .75f, .06f);
            var wave = ArcheryGroundShock(ground + Vector2.up * .05f, r * 3.2f, Alpha(sky, 1), .4f); wave.age = -.06f; wave.ease = 5;
            var circle = LayFlat(Spawn("Orbital circle", runeRing, ground, r * 2.4f, Alpha(sky, .9f), 1f, true, 27), .3f); circle.spin = 60;
            circle.fadeIn = .04f; circle.startScale = .5f; circle.endScale = 1; circle.ease = 7; circle.hold = .5f; circle.fadePower = 1.2f;
            ArcheryNeedles(pos, Vector2.up, 10, 9 * s, sky, .38f, .45f, 110, .06f, 6);
        }
    }
}
