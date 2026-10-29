using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Archery and engineering family: MultiShot, PierceShot, RainArrows and Sentry.
    // Holy-light archery: a bow of light drawn while wind spirals into the hand, light arrows wrapped in spinning wind,
    // a lance that punches through accelerator rune rings, light spears raining from a sky sigil and bursting on the
    // ground, and a celestial crystal sentinel that scans and fires tracer bursts.
    // Shapes are three layers: a dark outline and a saturated body (alpha, so they read on pale skies) under a white-hot
    // additive core; bursts pair additive light with saturated alpha flare lines and rings for the same reason.
    public sealed partial class TowerVfx
    {
        // Arrow and lance sets: [0] dark outline, [1] body, [2] hot core (all the same size, drawn on top of each other).
        private static Sprite[] ArcheryArrowSet, ArcheryLanceSet, ArcheryBowFrames, ArcheryBowInkFrames, ArcheryHelixFrames;
        private static Sprite ArcheryScanSprite, ArcherySentinelSprite;

        private static readonly Color ArcheryGold = new Color(1, .8f, .34f), ArcheryHot = new Color(1, .97f, .86f),
            ArcheryBody = new Color(1, .68f, .14f, .95f), ArcheryFlare = new Color(1, .52f, .06f, .75f), ArcheryInk = new Color(.34f, .09f, 0, .8f),
            ArcheryWind = new Color(.9f, .97f, 1), ArcheryCloud = new Color(.42f, .33f, .28f, .4f), ArcheryFeather = new Color(.98f, .7f, .22f, .92f),
            ArcherySky = new Color(.4f, .82f, 1), ArcherySkyHot = new Color(.86f, .98f, 1), ArcherySkyBody = new Color(.16f, .55f, 1, .85f),
            ArcherySkyInk = new Color(.02f, .09f, .32f, .75f),
            // Level three: gold-white awakened light, and the frost of an awakened volley (its arrows slow what they hit).
            ArcheryAwake = new Color(1, .94f, .74f), ArcheryFrost = new Color(.6f, .9f, 1), ArcheryFrostInk = new Color(.04f, .2f, .46f, .72f);

        // A sentinel on the floor. back is how far it floats away from the hero (sign = side); mounts ride at its eye, floors at its feet.
        private sealed class ArcherySentryRig { public Transform root; public Clip body, eye, scan, heart; public Clip[] mounts, floors; public Vector3 baseScale; public Vector2 eyeAt; public float fireAt = -9, aim, back; public int side = 1, frame; public bool paired; }
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

        private static float ArcheryArrowShape(float u, float v, float grow)
        {
            float x = u, y = Mathf.Abs(v) * .25f;
            float shaft = (1 - Smooth(.02f + grow, .036f + grow, y)) * Smooth(-.9f - grow, -.84f - grow, x) * (1 - Smooth(.36f, .44f, x));
            float head = 0, t = (x - .3f + grow) / (.64f + grow * 1.6f);
            if (t > 0 && t < 1) { float hw = .145f * Mathf.Pow(t < .3f ? t / .3f : (1 - t) / .7f, .85f) + grow; head = 1 - Smooth(hw - .016f, hw + .002f, y); }
            float vane = 0, fu = (x + .94f + grow) / (.36f + grow * 2);
            if (fu > 0 && fu < 1) { float top = .03f + .12f * Mathf.Pow(Mathf.Sin(fu * Mathf.PI * .92f), .8f) * (1 - .25f * fu) + grow; vane = 1 - Smooth(top - .02f, top, y); }
            return Mathf.Max(Mathf.Max(shaft, head), vane) * (1 - Smooth(.95f, 1, Mathf.Abs(u))) * (1 - Smooth(.84f, 1, Mathf.Abs(v)));
        }

        private static float ArcheryLanceShape(float u, float v, float grow)
        {
            float x = u, y = Mathf.Abs(v) * .25f;
            float hb = .022f + .016f * Smooth(-.9f, .14f, x) + grow;
            float haft = (1 - Smooth(hb - .012f, hb, y)) * Smooth(-.86f - grow, -.82f - grow, x) * (1 - Smooth(.14f, .2f, x));
            float head = 0, t = (x - .12f + grow) / (.82f + grow * 1.5f);
            if (t > 0 && t < 1) { float hw = .125f * Mathf.Pow(t < .14f ? t / .14f : (1 - t) / .86f, .8f) + grow; head = 1 - Smooth(hw - .014f, hw + .002f, y); }
            float guard = (1 - Smooth(.02f + grow, .034f + grow, Mathf.Abs(x - .12f))) * (1 - Smooth(.14f + grow, .16f + grow, y));
            float pommel = 1 - Smooth(.045f + grow, .06f + grow, Mathf.Abs(x + .86f) + y * 1.1f);
            return Mathf.Max(Mathf.Max(haft, head), Mathf.Max(guard, pommel)) * (1 - Smooth(.95f, 1, Mathf.Abs(u))) * (1 - Smooth(.84f, 1, Mathf.Abs(v)));
        }

        private static float ArcheryBowShape(float u, float v, int f, float grow, float tipX, float tipY)
        {
            float x = u * .5f, y = v, ay = Mathf.Abs(y);
            float limbX = .25f - .34f * y * y + .1f * Smooth(.7f, .93f, ay);
            float d = Mathf.Abs(x - limbX) / Mathf.Sqrt(1 + Sq(.68f * y)), w = .03f * (1 - .45f * ay) + .012f + grow;
            float limb = (1 - Smooth(w - .01f, w, d)) * (1 - Smooth(.9f + grow, .95f + grow, ay));
            float nockX = tipX - .02f - f * .1f;
            float sd = Mathf.Min(SegmentDistance(x, y, tipX, tipY, nockX, 0), SegmentDistance(x, y, tipX, -tipY, nockX, 0));
            float bowString = (1 - Smooth(.004f + grow, .01f + grow, sd)) * .9f;
            float grip = 1 - Smooth(.05f + grow, .065f + grow, Mathf.Sqrt(Sq(x - .25f) + Sq(y / 2.2f)));
            float tips = 1 - Smooth(.03f + grow, .045f + grow, Mathf.Sqrt(Sq(x - tipX) + Sq(ay - tipY)));
            return Mathf.Max(Mathf.Max(limb, bowString), Mathf.Max(grip, tips)) * (1 - Smooth(.95f, 1, ay)) * (1 - Smooth(.9f, 1, Mathf.Abs(u)));
        }

        private static void BuildArcheryTextures()
        {
            ArcheryArrowSet = new[]
            {
                Build("Archery arrow outline", 256, 64, (u, v) => White(Mathf.Max(ArcheryArrowShape(u, v, .032f), ArcheryArrowShape(u, v, .07f) * .35f))),
                Build("Archery arrow body", 256, 64, (u, v) => White(ArcheryArrowShape(u, v, 0))),
                Build("Archery arrow core", 256, 64, (u, v) => White(ArcheryArrowShape(u, v, -.014f) + Gauss(SegmentDistance(u, Mathf.Abs(v) * .25f, -.8f, 0, .9f, 0), .02f) * .5f)),
            };
            ArcheryLanceSet = new[]
            {
                Build("Archery lance outline", 256, 64, (u, v) => White(Mathf.Max(ArcheryLanceShape(u, v, .03f), ArcheryLanceShape(u, v, .065f) * .35f))),
                Build("Archery lance body", 256, 64, (u, v) => White(ArcheryLanceShape(u, v, 0))),
                Build("Archery lance core", 256, 64, (u, v) => White(ArcheryLanceShape(u, v, -.013f) + Gauss(SegmentDistance(u, Mathf.Abs(v) * .25f, -.8f, 0, .92f, 0), .02f) * .55f)),
            };
            // A double wind helix trailing behind a shot (front at +u); the frames travel the twist backwards.
            ArcheryHelixFrames = Sheet("Archery wind helix", 6, 256, 64, (u, v, f) =>
            {
                float a = 0, back = (1 - u) * .5f;
                for (int s = 0; s < 2; s++)
                {
                    float arg = 2.6f * Mathf.PI * u + f / 6f * 2 * Mathf.PI + s * Mathf.PI;
                    float y = (.18f + .32f * back) * Mathf.Sin(arg), depth = .5f + .5f * Mathf.Cos(arg);
                    a = Mathf.Max(a, Gauss(v - y, .1f + .04f * back) * (.25f + .75f * depth) + Gauss(v - y, .28f) * .1f);
                }
                float envelope = Smooth(-1, -.2f, u) * (1 - Smooth(.65f, .95f, u));
                return White(Mathf.Clamp01(a * envelope) * (1 - Smooth(.82f, 1, Mathf.Abs(v))));
            });
            // A bow of light seen from the side (belly towards +u); the four frames draw the string back.
            float tipY = .9f, tipX = .25f - .34f * tipY * tipY + .1f * Smooth(.7f, .93f, tipY);
            ArcheryBowFrames = Sheet("Archery light bow", 4, 128, 256, (u, v, f) => White(ArcheryBowShape(u, v, f, 0, tipX, tipY)));
            ArcheryBowInkFrames = Sheet("Archery light bow outline", 4, 128, 256, (u, v, f) => White(Mathf.Max(ArcheryBowShape(u, v, f, .022f, tipX, tipY), ArcheryBowShape(u, v, f, .05f, tipX, tipY) * .35f)));
            // A scanning cone of light from the left edge (pivot), widening and fading to the right.
            ArcheryScanSprite = Build("Archery scan cone", 256, 128, (u, v) =>
            {
                float x = (u + 1) * .5f, spread = .06f + .55f * x;
                float cone = Gauss(v / spread, .85f) * Mathf.Pow(1 - x, 1.4f) * Smooth(0, .05f, x);
                float edge = Gauss(Mathf.Abs(v) - spread * .85f, .025f) * .35f * (1 - x) * Smooth(0, .1f, x);
                float beam = Gauss(v, .025f) * Mathf.Pow(1 - x, 2) * .7f;
                return White(Mathf.Clamp01(cone * .55f + edge + beam) * (1 - Smooth(.85f, 1, Mathf.Abs(v))) * (1 - Smooth(.92f, 1, u)));
            }, new Vector2(0, .5f));
            // The sentinel: a faceted sky-blue crystal with a white eye, girdled by a tilted golden orbit ring.
            ArcherySentinelSprite = Build("Archery sentinel", 128, 192, (u, v) =>
            {
                float x = u * .6667f, y = v;
                float cs = Mathf.Cos(.24f), sn = Mathf.Sin(.24f), rx = x * cs + (y + .02f) * sn, ry = -x * sn + (y + .02f) * cs;
                float er = Mathf.Sqrt(Sq(rx / .6f) + Sq(ry / .17f)), grad = Mathf.Sqrt(Sq(rx / .36f) + Sq(ry / .0289f)) / Mathf.Max(.001f, er);
                float ringA = 1 - Smooth(.022f, .036f, Mathf.Abs(er - 1) / Mathf.Max(.001f, grad));
                Color ringC = Lit(new Color(1, .76f, .3f), .8f + (ry < 0 ? .35f : 0) + .35f * Gauss(rx + .22f, .14f), ringA);
                Color c = Color.clear;
                if (ry >= 0) c = Over(c, Lit(ringC, .75f, ringA));
                float cy = y - .02f, d = Mathf.Abs(x) / .34f + Mathf.Abs(cy) / (cy > 0 ? .8f : .72f), inside = 1 - Smooth(.95f, 1, d);
                if (inside > 0)
                {
                    float face = (x < 0 ? (cy > .12f ? .95f : .6f) : (cy > .12f ? .72f : .44f)) + (1 - d) * .25f;
                    Color crystal = Color.Lerp(new Color(.04f, .15f, .5f), new Color(.55f, .9f, 1), Mathf.Clamp01(face));
                    float rim = Gauss(d - .96f, .03f), seam = Gauss(x, .012f) * .35f + Gauss(cy - .12f, .012f) * .3f, eye = Mathf.Sqrt(Sq(x) + Sq(cy - .1f));
                    crystal = Color.Lerp(crystal, Color.white, Mathf.Clamp01(rim * .8f + seam));
                    crystal = Color.Lerp(crystal, new Color(.85f, 1, 1), Gauss(eye, .1f));
                    crystal = Color.Lerp(crystal, Color.white, 1 - Smooth(.05f, .08f, eye));
                    crystal.a = inside; c = Over(c, crystal);
                }
                if (ry < 0) c = Over(c, ringC);
                c.a *= (1 - Smooth(.9f, 1, Mathf.Abs(u))) * (1 - Smooth(.92f, 1, Mathf.Abs(v)));
                return c;
            });
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
        private static Clip ArcheryOpaque(Clip clip) { clip.renderer.sharedMaterial = AlphaBlend; return clip; }
        /// <summary>Sets a clip's angle and applies it at once, so a freshly emitted clip is never drawn unrotated for a frame.</summary>
        private static Clip ArcheryTurn(Clip clip, float angle) { clip.angle = angle; clip.root.transform.localRotation = Quaternion.Euler(0, 0, angle + clip.angleOffset); return clip; }

        /// <summary>True while <paramref name="clip"/> is still alive, younger than <paramref name="window"/> and near <paramref name="pos"/>:
        /// a burst already marks this moment. Checks liveness, because an expired clip keeps the age it had when it died.</summary>
        private bool ArcheryRecent(Clip clip, float window, Vector2 pos, float distance)
        {
            return clip != null && clip.age < window && (clip.position - pos).sqrMagnitude < distance * distance && clips.Contains(clip);
        }

        /// <summary>Embers drifting up after a burst: half additive light, half saturated alpha so they read on pale skies.</summary>
        private void ArcheryEmbers(Vector2 pos, int count, float spread, float s, Color light, float delay)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                bool lit = i % 2 == 0;
                var ember = Spawn("Rising ember", spark, pos + Inside(spread), Range(.18f, .32f) * s, lit ? Alpha(Color.Lerp(light, Color.white, .3f), .95f) : Alpha(ArcheryBody, .9f), Range(.55f, 1f), lit, lit ? 127 : 121);
                ember.velocity = new Vector2(Range(-1.2f, 1.2f), Range(1.2f, 2.8f)) * s; ember.gravity = -1.5f; ember.drag = 1.8f; ember.alignVelocity = true;
                ember.flicker = .35f; ember.fadeIn = .05f; ember.hold = .3f; ember.endScale = .4f; ember.ease = 1; ember.age = -delay - Range(0, .08f);
            }
        }

        /// <summary>A segment of the burning scar a lance leaves along its path.</summary>
        private void ArcheryScar(Vector2 a, Vector2 b, Color light)
        {
            var lit = Line(a, b, .42f, Alpha(Color.Lerp(light, ArcheryHot, .4f), .7f), .42f, 122, 1.5f); lit.axisEnd = new Vector2(1, .2f); lit.ease = 1.6f; lit.fadePower = 1.3f;
            var tone = ArcheryOpaque(Line(a, b, .26f, Alpha(ArcheryFlare, .6f), .36f, 116, 1.5f)); tone.axisEnd = new Vector2(1, .15f); tone.ease = 1.6f; tone.fadePower = 1.4f;
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

        /// <summary>A three-layer light shape (outline, body, hot core) riding on <paramref name="anchor"/>, or free when there is none.</summary>
        private Clip[] ArcheryShape(Transform anchor, Sprite[] set, Vector2 pos, float angle, float width, float life, Color hot, int order)
        {
            var shape = new[]
            {
                Anchored(anchor, pos, "Light shape outline", set[0], width, ArcheryInk, life, order - 10, false),
                Anchored(anchor, pos, "Light shape body", set[1], width, ArcheryBody, life, order - 9, false),
                Anchored(anchor, pos, "Light shape core", set[2], width, hot, life, order, true),
            };
            foreach (var clip in shape) ArcheryTurn(clip, angle);
            return shape;
        }

        /// <summary>A sonic-boom ring standing across <paramref name="dir"/>, opening as it rides forward.</summary>
        private Clip ArcheryBoom(Vector2 pos, Vector2 dir, float width, Color color, float life, float speed, bool additive = true, int order = 130)
        {
            var clip = ArcheryTurn(ArcheryThin(Spawn("Sonic boom", ring, pos, width, color, life, additive, order), .3f), ArcheryAngle(dir));
            clip.startScale = .25f; clip.endScale = 1; clip.ease = 3; clip.fadePower = 1.3f;
            clip.velocity = dir * speed; clip.drag = 5; return clip;
        }

        /// <summary>Anime burst lines drawn in a saturated alpha colour, so a burst keeps its shape on pale backgrounds.</summary>
        private Clip ArcheryFlareLines(Vector2 pos, float width, Color color, float life, int order = 118)
        {
            var clip = Spawn("Flare lines", burst, pos, width, color, life, false, order);
            clip.angle = Range(0, 360); clip.startScale = .5f; clip.endScale = 1.15f; clip.ease = 3; clip.fadePower = 1.3f; return clip;
        }

        private Clip ArcheryFlareRing(Vector2 pos, float width, Color color, float life, int order = 117)
        {
            var clip = Spawn("Flare ring", ring, pos, width, color, life, false, order);
            clip.startScale = .15f; clip.endScale = 1; clip.ease = 3; clip.fadePower = 1.2f; return clip;
        }

        /// <summary>Wind streaks spiralling into <paramref name="pos"/> over <paramref name="life"/> seconds.</summary>
        private void ArcheryWindIn(Vector2 pos, int count, float radius, Color color, float life, float turns)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                float delay = Range(0, life * .35f), span = Mathf.Max(.06f, life - delay);
                var mote = Spawn("Wind streak", spark, pos, Range(.55f, .95f), Alpha(i % 3 == 0 ? Color.white : color, Range(.65f, .95f)), span, true, 130);
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

        /// <summary>The lance passing through a body: a white-hot flash, a gold rune ring standing across the path, light driven out
        /// of the back of the body, flare lines and a spray of sparks along the throw (about a dozen clips).</summary>
        private void ArcheryLanceThrough(Vector2 pos, Vector2 dir, Color light, float s, bool awake)
        {
            float a = ArcheryAngle(dir); Color hot = awake ? ArcheryAwake : ArcheryHot;
            var flash = Spawn("Lance pass flash", glow, pos, 1.7f * s, Alpha(hot, .9f), .1f, true, 132); flash.endScale = 1.5f; flash.ease = 1; flash.fadePower = 1.5f;
            Glint(pos, 1.9f * s, Alpha(hot, 1), .2f, a + 45);
            var portal = ArcheryTurn(ArcheryThin(Spawn("Lance pass ring", runeRing, pos, 2.2f * s, Alpha(light, .9f), .3f, true, 127), .32f), a);
            var portalTone = ArcheryTurn(ArcheryThin(Spawn("Lance pass ring tone", runeRing, pos, 2.26f * s, Alpha(ArcheryFlare, .6f), .28f, false, 117), .32f), a);
            foreach (var x in new[] { portal, portalTone }) { x.startScale = .4f; x.endScale = 1.15f; x.ease = 3; x.fadePower = 1.4f; x.velocity = dir * 2; x.drag = 4; }
            ArcheryFlareLines(pos, 2.6f * s, Alpha(ArcheryFlare, .75f), .18f, 119);
            Vector2 end = pos + dir * 2.6f * s;
            var through = Line(pos, end, .55f * s, Alpha(light, .6f), .2f, 127); through.axisStart = new Vector2(.2f, 1); through.axisEnd = new Vector2(1, .2f); through.ease = 3;
            var throughTone = ArcheryOpaque(Line(pos, end, .3f * s, Alpha(ArcheryFlare, .65f), .2f, 117)); throughTone.axisStart = new Vector2(.2f, 1); throughTone.axisEnd = new Vector2(1, .2f); throughTone.ease = 3;
            for (int i = 0; i < 4; i++) Spark(pos, Direction(a + Range(-35, 35)) * Range(5, 10), i % 2 == 0 ? Color.white : light, Range(.24f, .38f), Range(.18f, .3f), 4, 2.5f);
            var piece = Spawn("Lance pass shard", shard, pos, .3f * s, Alpha(hot, .9f), .35f, true, 129);
            piece.velocity = Direction(a + Range(-60, 60)) * 5 + Vector2.up; piece.gravity = 6; piece.drag = 1.2f; piece.spin = Range(-500, 500); piece.fadePower = 1.4f;
        }

        /// <summary>An awakened fan arrow passing through its first body: a glint, a ring across the path, a white line driven on
        /// and a few sparks (about eight clips).</summary>
        private void ArcheryArrowThrough(Vector2 pos, Vector2 dir, Color light)
        {
            float a = ArcheryAngle(dir);
            var flash = Spawn("Arrow pass flash", glow, pos, 1.1f, Alpha(ArcheryAwake, .85f), .09f, true, 132); flash.endScale = 1.4f; flash.ease = 1; flash.fadePower = 1.5f;
            Glint(pos, 1.4f, Alpha(ArcheryHot, .95f), .16f, a + 45);
            ArcheryBoom(pos, dir, 1.4f, Alpha(ArcheryAwake, .85f), .2f, 4);
            ArcheryBoom(pos, dir, 1.5f, Alpha(ArcheryFlare, .55f), .18f, 4, false, 117);
            var through = Line(pos, pos + dir * 1.8f, .1f, new Color(1, 1, 1, .85f), .14f, 131); through.startScale = .3f; through.endScale = 1; through.ease = 3; through.velocity = dir * 6;
            for (int i = 0; i < 3; i++) Spark(pos, Direction(a + Range(-40, 40)) * Range(4, 8), i == 0 ? Color.white : light, Range(.2f, .32f), Range(.16f, .26f), 5, 2.5f);
        }

        private void ArcheryFeathers(Vector2 pos, int count, float angle, float spread, float speed, Color light, float s)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                bool lit = i % 2 == 0;
                var feather = Spawn("Light feather", petal, pos + Inside(.25f * s), Range(.26f, .42f) * s, lit ? Alpha(Color.Lerp(light, Color.white, .35f), .9f) : ArcheryFeather, Range(.6f, 1f), lit, lit ? 127 : 120);
                feather.velocity = Direction(angle + Range(-spread, spread)) * Range(.5f, 1) * speed; feather.drag = 3; feather.gravity = 1.2f;
                feather.spin = Range(-360, 360); feather.angle = Range(0, 360); feather.fadePower = 1.3f; feather.hold = .3f;
            }
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
                var snap = Spawn("Quick release flash", glow, tip - dir * .3f, 1.7f, Alpha(ArcheryHot, .9f), .12f, true, 132); snap.age = -r; snap.startScale = .5f; snap.endScale = 1.3f; snap.ease = 2; snap.fadePower = 1.5f;
                Glint(tip - dir * .3f, 2f, Alpha(ArcheryHot, 1), .16f, a).age = -r;
                ArcheryBoom(tip, dir, 1.9f, Alpha(light, .85f), .22f, 9).age = -r;
                ArcheryBoom(tip, dir, 2f, Alpha(ArcheryFlare, .55f), .2f, 9, false, 117).age = -r;
                return;
            }

            // the bow of light, string drawn back over the wind-up
            Underlay(bowAt + dir * .2f, bowH * 1.2f, .22f, life).fadeIn = .1f;
            var bow = new[]
            {
                Anchored(c.anchor, bowAt, "Light bow outline", ArcheryBowInkFrames[0], bowH * .5f, ArcheryInk, life, 117, false),
                Anchored(c.anchor, bowAt, "Light bow", ArcheryBowFrames[0], bowH * .5f, ArcheryBody, life, 118, false),
                Anchored(c.anchor, bowAt, "Light bow shine", ArcheryBowFrames[0], bowH * .5f, Alpha(Color.Lerp(light, ArcheryHot, .5f), .65f), life, 127),
            };
            bow[0].frames = ArcheryBowInkFrames; bow[1].frames = ArcheryBowFrames; bow[2].frames = ArcheryBowFrames;
            foreach (var b in bow) { b.loop = false; b.angle = a; b.fadeIn = .06f; b.hold = .74f; b.fadePower = 1.5f; b.startScale = .5f; b.endScale = 1; b.ease = 6; }
            var aura = Anchored(c.anchor, bowAt + dir * .15f, "Light bow aura", glow, bowH, Alpha(light, .32f), life, 121);
            aura.scale.x *= .5f; aura.angle = a; aura.pulse = .06f; aura.hold = .7f; aura.fadeIn = .1f;
            Emitter(bow[2], Emit.Sparkles, 20, bowH * .35f, light);
            if (grade >= 3)
            {
                // Awakened: the bow burns gold-white along its limbs and a rune ring stands across the shot behind it.
                var gilt = Anchored(c.anchor, bowAt, "Light bow gilt", ArcheryBowInkFrames[0], bowH * .5f, Alpha(ArcheryAwake, .7f), life, 128);
                gilt.frames = ArcheryBowInkFrames; gilt.loop = false; gilt.angle = a; gilt.fadeIn = .06f; gilt.hold = .74f; gilt.fadePower = 1.5f; gilt.startScale = .5f; gilt.endScale = 1; gilt.ease = 6; gilt.flicker = .1f;
                for (int layer = 0; layer < 2; layer++)
                {
                    bool lit = layer == 1;
                    var halo = ArcheryThin(Anchored(c.anchor, bowAt - dir * .25f, lit ? "Awakened bow ring" : "Awakened bow ring tone", runeRing, bowH * (lit ? 1.05f : 1.08f),
                        lit ? Alpha(ArcheryAwake, .9f) : Alpha(ArcheryFlare, .55f), life, lit ? 126 : 116, lit), .34f);
                    halo.angle = a; halo.fadeIn = .1f; halo.startScale = .4f; halo.endScale = 1; halo.ease = 4; halo.hold = .74f; halo.fadePower = 1.5f; halo.pulse = .04f;
                }
            }

            // arrows of light forming on the string; the fan grows with the level (one more arrow each), 9 degrees apart like the shot
            int count = mode == 0 ? 3 + (grade - 1) : 1; var set = mode == 1 ? ArcheryLanceSet : ArcheryArrowSet;
            for (int i = 0; i < count; i++)
            {
                float ai = a + (count == 1 ? 0 : (i - (count - 1) * .5f) * 9);
                var shape = ArcheryShape(c.anchor, set, nockPt + Direction(ai) * arrowLen * .48f, ai, arrowLen, w + .01f, Alpha(ArcheryHot, .95f), 128);
                foreach (var x in shape) { x.fadeIn = w * .4f; x.fadePower = 0; x.startScale = .3f; x.endScale = 1; x.ease = 2.2f; }
                shape[2].flicker = .08f;
            }
            var charge = Anchored(c.anchor, tip, "Arrow charge", glow, mode == 1 ? 2.2f : 1.5f, Alpha(light, .8f), w + .04f, 126);
            charge.startScale = .12f; charge.endScale = 1; charge.ease = 1.2f; charge.pulse = .1f; charge.hold = .85f; charge.fadePower = 2;
            var star = Anchored(c.anchor, tip, "Arrow charge star", glint, mode == 1 ? 2.1f : 1.5f, Alpha(ArcheryHot, .95f), w + .04f, 129);
            star.startScale = .1f; star.endScale = 1; star.ease = 1.2f; star.spin = 160; star.hold = .85f; star.fadePower = 2;

            // wind spiralling into the drawing hand
            ArcheryWindIn(nockPt, mode == 2 ? 7 : mode == 1 ? 13 : 12, mode == 1 ? 2.9f : 2.4f, wind, w, .6f);
            var vortex = Swirl(nockPt, mode == 1 ? 3.4f : 2.8f, Alpha(wind, .45f), w + .04f, -380, 125); vortex.startScale = 1.25f; vortex.endScale = .35f; vortex.ease = 1.4f;
            var vortexTone = Spawn("Wind vortex tone", swirl, nockPt, mode == 1 ? 3.5f : 2.9f, Alpha(ArcheryFlare, .3f), w + .04f, false, 116);
            vortexTone.spin = -380; vortexTone.angle = vortex.angle; vortexTone.startScale = 1.25f; vortexTone.endScale = .35f; vortexTone.ease = 1.4f; vortexTone.fadeIn = .06f; vortexTone.fadePower = 1.3f;

            // footing: a rune ring on the ground and dust blown out by the gathering wind
            if (mode != 2)
            {
                var runeTone = ArcheryFlat(Spawn("Archer rune tone", runeRing, feet, 3.8f, Alpha(ArcheryFlare, .5f), w + .4f, false, 25), .3f);
                var rune = ArcheryFlat(Spawn("Archer rune", runeRing, feet, 3.7f, Alpha(light, .9f), w + .4f, true, 27), .3f);
                foreach (var x in new[] { runeTone, rune }) { x.fadeIn = .08f; x.startScale = .45f; x.endScale = 1.05f; x.ease = 4; x.hold = .6f; x.fadePower = 1.4f; }
            }
            GroundGlow(feet, 4.2f, Alpha(light, .42f), w + .35f);
            for (int i = 0, n = Budget(mode == 2 ? 2 : 4); i < n; i++)
            {
                var dust = Puff(feet + new Vector2(Range(-.5f, .5f), .1f), Range(.5f, .8f), DustTone, new Vector2((i % 2 == 0 ? 1 : -1) * Range(1, 2.2f), Range(.2f, .6f)), Range(.45f, .65f), 118);
                dust.gravity = -.3f;
            }

            // release
            float release = w - .02f;
            ArcheryRelease(tip - dir * .3f, dir, light, release, mode == 1 ? 1.2f : 1, mode != 1);
            for (int i = 0, n = Budget(3); i < n; i++)
            {
                var kick = Puff(feet + new Vector2(-f * Range(.1f, .5f), .1f), Range(.5f, .8f), DustTone, new Vector2(-f * Range(1.5f, 3), Range(.2f, .7f)), Range(.4f, .6f), 118);
                kick.age = -release;
            }
        }

        /// <summary>The bow's release at the arrow tip: a tight white-hot flash, burst lines, sonic-boom rings, gusts and a spark cone.</summary>
        private void ArcheryRelease(Vector2 pos, Vector2 dir, Color light, float delay, float s, bool booms)
        {
            float a = ArcheryAngle(dir); Vector2 side = new Vector2(-dir.y, dir.x);
            var flash = Spawn("Release flash", glow, pos, 1.8f * s, Alpha(ArcheryHot, .95f), .12f, true, 132); flash.age = -delay; flash.startScale = .5f; flash.endScale = 1.3f; flash.ease = 2; flash.fadePower = 1.6f;
            var bloom = Spawn("Release bloom", glow, pos + dir * .4f, 3.2f * s, Alpha(light, .35f), .24f, true, 123); bloom.age = -delay; bloom.startScale = .7f; bloom.endScale = 1.3f; bloom.ease = 2; bloom.fadePower = 1.5f;
            Glint(pos, 2.6f * s, Alpha(ArcheryHot, 1), .2f, a).age = -delay;
            ImpactLines(pos, 3.4f * s, Alpha(light, .7f), .2f).age = -delay;
            ArcheryFlareLines(pos, 3.8f * s, ArcheryFlare, .2f).age = -delay;
            if (booms)
            {
                ArcheryBoom(pos + dir * .3f * s, dir, 2.4f * s, Alpha(ArcheryHot, .9f), .28f, 8).age = -delay;
                ArcheryBoom(pos + dir * .9f * s, dir, 1.8f * s, Alpha(light, .9f), .28f, 13).age = -(delay + .04f);
                ArcheryBoom(pos + dir * .3f * s, dir, 2.6f * s, Alpha(ArcheryFlare, .6f), .26f, 8, false, 117).age = -delay;
            }
            for (int i = 0, n = ArcheryShare(6); i < n; i++)
            {
                Vector2 start = pos + side * Range(-.8f, .8f) * s - dir * Range(.2f, .8f);
                var gust = Line(start, start + dir * Range(1.4f, 2.6f) * s, Range(.05f, .1f), i % 3 == 0 ? new Color(1, 1, 1, .8f) : Alpha(light, .85f), Range(.16f, .24f), 129);
                if (i % 2 == 1) { ArcheryOpaque(gust); gust.color = Alpha(ArcheryFlare, .6f); }
                gust.age = -delay; gust.velocity = dir * Range(9, 15); gust.drag = 3; gust.startScale = .4f; gust.endScale = 1; gust.ease = 3; gust.fadePower = 1.5f;
            }
            for (int i = 0, n = ArcheryShare(10); i < n; i++)
                Spark(pos, Direction(a + Range(-35, 35)) * Range(5, 12) * s, i % 3 == 0 ? Color.white : light, Range(.25f, .45f), Range(.2f, .34f), 3, 2.5f).age = -delay;
        }

        private void CastMultiShot(CastInfo c)
        {
            ArcheryBow(c, 0);
            // the spread leaves the bow as a fan-shaped sweep of light in front of the arrows
            Color light = ArcheryLight(c.tint); float r0 = c.windup - .02f;
            Vector2 dir = ArcheryAim(c.target - c.pos, new Vector2(c.facing, 0)), at = c.pos + dir * 2.2f; float a = ArcheryAngle(dir), fanSize = ArcheryLevel(1.12f, 1.25f);
            var fan = ArcheryTurn(Crescent(at, 3.4f * fanSize, Alpha(Grade >= 3 ? Color.Lerp(light, ArcheryAwake, .5f) : light, .85f), .24f, a, 131), a);
            var fanTone = ArcheryTurn(Spawn("Fan sweep tone", crescent, at, 3.5f * fanSize, Alpha(ArcheryFlare, .6f), .22f, false, 117), a);
            fanTone.startScale = .75f; fanTone.endScale = 1.12f; fanTone.ease = 3; fanTone.fadePower = 1.3f;
            foreach (var x in new[] { fan, fanTone }) { x.age = -r0; x.velocity = dir * 7; x.drag = 4; x.scale.y *= 1.15f; }
        }

        private void CastPierceShot(CastInfo c)
        {
            ArcheryBow(c, 1);
            if (c.windup < .2f) return;
            float w = c.windup; Color light = ArcheryLight(c.tint); int grade = Grade; float gl = ArcheryLevel(1.08f, 1.16f);
            Vector2 dir = ArcheryAim(c.target - c.pos, new Vector2(c.facing, 0)); float a = ArcheryAngle(dir);
            Vector2 tip = c.pos + dir * (.12f + 3.3f * gl * .96f);
            // accelerator rune rings around the lance, opening one after another (one more ring per level)
            int rings = 2 + grade;
            for (int i = 0; i < rings; i++)
            {
                Vector2 at = c.pos + dir * (1.3f + i * .78f);
                float width = (2.9f - i * .45f) * gl, delay = i * w * .15f, span = Mathf.Max(.1f, w - delay + .06f);
                var ringTone = ArcheryThin(Spawn("Accelerator ring tone", runeRing, at, width * 1.02f, Alpha(ArcheryFlare, .6f), span, false, 117), .32f);
                var ringLit = ArcheryThin(Spawn("Accelerator ring", runeRing, at, width, Alpha(light, .95f), span, true, 127), .32f);
                foreach (var r in new[] { ringTone, ringLit }) { r.angle = a; r.age = -delay; r.fadeIn = .08f; r.startScale = .25f; r.endScale = 1; r.ease = 4; r.hold = .85f; r.fadePower = 2; }
                ringLit.flicker = .12f;
                var flare = ArcheryBoom(at, dir, width * 1.3f, Alpha(ArcheryHot, .9f), .26f, 10 + i * 3); flare.age = -(w - .02f + i * .025f); flare.startScale = .8f; flare.endScale = 1.5f;
                var flareTone = ArcheryBoom(at, dir, width * 1.35f, Alpha(ArcheryFlare, .55f), .24f, 10 + i * 3, false, 117); flareTone.age = flare.age; flareTone.startScale = .8f; flareTone.endScale = 1.5f;
            }
            Converge(tip, 8, 2.4f, light, w, .45f);
            // The line of the throw; awakened, it is a wide gold-white lane (the lance pierces everything along it).
            float lane = grade >= 3 ? 2.6f : 1;
            var sight = Line(tip, tip + dir * 6, .06f * lane, Alpha(grade >= 3 ? ArcheryAwake : light, .6f), w, 126); sight.fadeIn = w * .6f; sight.flicker = .35f; sight.hold = .85f; sight.fadePower = 2;
            var sightTone = ArcheryOpaque(Line(tip, tip + dir * 6, .09f * lane, Alpha(ArcheryFlare, .45f), w, 117)); sightTone.fadeIn = w * .6f; sightTone.hold = .85f; sightTone.fadePower = 2;
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
                for (int layer = 0; layer < 2; layer++)
                {
                    bool lit = layer == 1;
                    var quick = ArcheryFlat(Spawn(lit ? "Sky sigil quick" : "Sky sigil quick tone", runeRing, skyAt, (lit ? 5.4f : 5.6f) * area, lit ? Alpha(light, .95f) : Alpha(ArcheryFlare, .55f), .5f, lit, lit ? 127 : 117), .3f);
                    quick.startScale = .4f; quick.endScale = 1; quick.ease = 3; quick.hold = .3f; quick.fadePower = 1.4f;
                }
                var quickFlare = ArcheryFlat(Spawn("Sky sigil quick flare", glow, skyAt, 4.2f * area, Alpha(ArcheryHot, .6f), .26f, true, 131), .4f); quickFlare.startScale = .5f; quickFlare.endScale = 1.3f; quickFlare.ease = 2; quickFlare.fadePower = 1.5f;
                return;
            }
            // the sky sigil the volley will break through
            var skyTone = ArcheryFlat(Spawn("Sky sigil tone", runeRing, skyAt, 5.6f * area, Alpha(ArcheryFlare, .55f), life, false, 117), .3f);
            var skySeal = ArcheryFlat(Spawn("Sky sigil", runeRing, skyAt, 5.4f * area, Alpha(light, .95f), life, true, 127), .3f);
            var skyInner = ArcheryFlat(Spawn("Sky sigil core", sigil, skyAt, 3.2f * area, Alpha(ArcheryHot, .75f), life, true, 128), .3f);
            var skyInnerTone = ArcheryFlat(Spawn("Sky sigil core tone", sigil, skyAt, 3.3f * area, Alpha(ArcheryInk, .45f), life, false, 116), .3f);
            var skyLayers = new List<Clip> { skyTone, skySeal, skyInner, skyInnerTone };
            if (grade >= 3)
            {
                // Awakened, the volley carries frost: an ice-blue ring rims the sigil (deep blue under it for pale skies).
                skyLayers.Add(ArcheryFlat(Spawn("Sky sigil frost tone", ring, skyAt, 6.9f * area, ArcheryFrostInk, life, false, 116), .3f));
                skyLayers.Add(ArcheryFlat(Spawn("Sky sigil frost", ring, skyAt, 6.7f * area, Alpha(ArcheryFrost, .95f), life, true, 126), .3f));
            }
            foreach (var x in skyLayers) { x.fadeIn = .12f; x.startScale = .35f; x.endScale = 1; x.ease = 3; x.hold = .6f; x.fadePower = 1.4f; }
            skyInner.pulse = .05f;
            var skyGlow = ArcheryFlat(Spawn("Sky sigil glow", glow, skyAt, 6.5f, Alpha(light, .4f), life, true, 122), .45f); skyGlow.fadeIn = .15f; skyGlow.hold = .55f; skyGlow.pulse = .06f;
            for (int i = 0, n = Budget(3); i < n; i++)
            {
                var cloud = Puff(skyAt + new Vector2(Range(-2.4f, 2.4f), Range(.1f, .5f)), Range(1.4f, 2.2f), ArcheryCloud, new Vector2(Range(-.5f, .5f), Range(-.1f, .2f)), life, 112);
                cloud.fadeIn = .25f; cloud.drag = .5f; cloud.startScale = .7f; cloud.endScale = 1.3f;
            }
            // guide lines along every arrow's path, and the target ring on the ground
            for (int i = 0; i < arrows; i++)
            {
                Vector2 l = land + new Vector2((i - (arrows - 1) * .5f) * .55f, 0), s = l + new Vector2(-1, 6), d = (l - s).normalized;
                Vector2 end = s + d * ((groundY - s.y) / d.y);
                var guide = Line(s, end, .06f, Alpha(grade >= 3 && i % 2 == 0 ? ArcheryFrost : light, .6f), w + .12f, 126); guide.fadeIn = w * .7f; guide.flicker = .3f; guide.hold = .8f; guide.fadePower = 2;
                if (arrows > 5 && i % 2 == 1) continue;
                var guideTone = ArcheryOpaque(Line(s, end, .08f, Alpha(ArcheryFlare, .4f), w + .12f, 117)); guideTone.fadeIn = w * .7f; guideTone.hold = .8f; guideTone.fadePower = 2;
            }
            Vector2 groundAt = new Vector2(land.x, groundY);
            float zoneWidth = Mathf.Max(4.6f, (arrows - 1) * .55f + 2.2f) * area;
            var zoneTone = ArcheryFlat(Spawn("Rain zone tone", runeRing, groundAt, zoneWidth * 1.04f, Alpha(ArcheryFlare, .55f), w + .6f, false, 25), .3f);
            var zone = ArcheryFlat(Spawn("Rain zone", runeRing, groundAt, zoneWidth, Alpha(light, .85f), w + .6f, true, 27), .3f);
            var zoneLayers = new List<Clip> { zoneTone, zone };
            if (grade >= 3)
            {
                zoneLayers.Add(ArcheryFlat(Spawn("Rain zone frost tone", ring, groundAt, zoneWidth * 1.3f, ArcheryFrostInk, w + .6f, false, 24), .3f));
                zoneLayers.Add(ArcheryFlat(Spawn("Rain zone frost", ring, groundAt, zoneWidth * 1.27f, Alpha(ArcheryFrost, .9f), w + .6f, true, 26), .3f));
                for (int i = 0, n = Budget(2); i < n; i++)
                {
                    var mist = Puff(groundAt + new Vector2(Range(-.4f, .4f) * zoneWidth, .2f), Range(1, 1.4f), new Color(.75f, .9f, 1, .32f), new Vector2(Range(-.3f, .3f), Range(.1f, .3f)), w + .5f, 112);
                    mist.fadeIn = .2f; mist.drag = 1; mist.startScale = .7f; mist.endScale = 1.4f;
                }
            }
            foreach (var x in zoneLayers) { x.fadeIn = .15f; x.startScale = .5f; x.endScale = 1; x.ease = 3; x.hold = .6f; x.fadePower = 1.4f; }
            // release: a light arrow shot into the sky, the sigil flaring as the volley breaks through
            float r0 = w - .02f; Vector2 up = Direction(f > 0 ? 66 : 114);
            foreach (var x in ArcheryShape(null, ArcheryArrowSet, c.pos + up * 1.4f, ArcheryAngle(up), 2.6f, .28f, Alpha(ArcheryHot, 1), 128)) { x.velocity = up * 28; x.age = -r0; x.fadePower = .6f; }
            var shotWake = Spawn("Sky-bound wake", streak, c.pos + up * .1f, 4.5f, Alpha(light, .75f), .28f, true, 127); shotWake.angle = ArcheryAngle(up); shotWake.velocity = up * 28; shotWake.age = -r0; shotWake.scale.y *= .45f; shotWake.fadePower = .8f;
            var flare = ArcheryFlat(Spawn("Sky sigil flare", glow, skyAt, 4.2f, Alpha(ArcheryHot, .6f), .26f, true, 131), .4f); flare.age = -r0; flare.startScale = .5f; flare.endScale = 1.3f; flare.ease = 2; flare.fadePower = 1.5f;
            var shock = ArcheryFlat(Spawn("Sky sigil shock", ring, skyAt, 7.5f, Alpha(light, .9f), .36f, true, 130), .3f); shock.age = -r0; shock.startScale = .3f; shock.endScale = 1; shock.ease = 3; shock.fadePower = 1.3f;
            var shockTone = ArcheryFlat(ArcheryFlareRing(skyAt, 7.6f, Alpha(ArcheryFlare, .6f), .34f), .3f); shockTone.age = -r0;
            var lines = ArcheryFlat(ArcheryFlareLines(skyAt, 7, ArcheryFlare, .28f), .5f); lines.age = -r0; lines.angle = 0;
        }

        // ---------------------------------------------------------------- arrows in flight

        private void ProjectileMultiShot(Transform root, Color tint, bool signature)
        {
            if (!signature) { PlainArrow(root); return; }
            Color light = ArcheryLight(tint); Vector2 at = root.position, dir = ArcheryAim(root.right, Vector2.right);
            // Bigger arrows at higher levels; awakened arrows pierce (gold-white edge, a piercing filament and rings left behind).
            ArcheryLightArrow(root, tint, ArcheryLevel(1.1f, 1.2f), false, Grade >= 3, false);
            // Awakened arrows pass through their first body (the second stops them and strikes in full).
            if (Grade >= 3) ArcheryPassWatch(root, .16f, .08f, (p, d) => ArcheryArrowThrough(p, d, light));
            var muzzle = Spawn("Arrow launch flash", glow, at + dir * .5f, 1.2f, Alpha(ArcheryHot, .8f), .1f, true, 131); muzzle.endScale = 1.5f; muzzle.ease = 1; muzzle.fadePower = 1.5f;
            ArcheryBoom(at + dir * .7f, dir, 1.6f, Alpha(light, .85f), .22f, 9);
            ArcheryBoom(at + dir * .7f, dir, 1.7f, Alpha(ArcheryFlare, .5f), .2f, 9, false, 117);
            for (int i = 0; i < 2; i++) Spark(at + dir * .5f, Direction(ArcheryAngle(dir) + Range(-25, 25)) * Range(5, 9), Color.white, Range(.2f, .3f), Range(.15f, .25f), 2, 3);
        }

        private void ProjectileRainArrows(Transform root, Color tint, bool signature)
        {
            if (!signature) { PlainArrow(root); return; }
            Color light = ArcheryLight(tint); Vector2 at = root.position; int grade = Grade, spear = ArcheryRainSpears++;
            // Awakened volleys slow what they hit: their spears fall with frosted tips (a snowflake on every other one).
            // Each spear is drawn a little ahead or behind its gameplay point and a touch bigger or smaller, so the volley
            // falls as rain rather than as a rigid row (its landings and hits stay where gameplay puts them).
            ArcheryLightArrow(root, tint, 1.05f * ArcheryLevel(1.06f, 1.12f) * Range(.92f, 1.08f), true, false, grade >= 3, Range(-.35f, .2f), spear % 2 == 0);
            if (grade < 3) { var flash = Spawn("Sky arrow flash", glow, at, 1.6f, Alpha(ArcheryHot, .85f), .14f, true, 131); flash.endScale = 1.4f; flash.ease = 1; flash.fadePower = 1.5f; }
            if (grade < 2 || spear % 2 == 0)
            {
                var ripple = ArcheryFlat(Spawn("Sky arrow ripple", ring, at, 1.9f, Alpha(light, .85f), .28f, true, 130), .3f); ripple.startScale = .2f; ripple.endScale = 1; ripple.ease = 3; ripple.fadePower = 1.3f;
            }
            if (grade < 3) ArcheryFlat(ArcheryFlareRing(at, 2f, Alpha(ArcheryFlare, .55f), .26f), .3f);
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
                var drop = Line(start, start - fall * Range(1.6f, 2.4f), Range(.08f, .12f), i == 0 ? new Color(1, 1, 1, .85f) : ArcheryBody, flight, i == 0 ? 126 : 117);
                if (i != 0) ArcheryOpaque(drop);
                drop.velocity = fall * speed; drop.age = -delay; drop.fadeIn = .06f; drop.fadePower = .25f;
                var splash = ArcheryFlat(Spawn("Light rain splash", ring, land + Vector2.up * .04f, Range(.9f, 1.3f), Alpha(light, .85f), .24f, true, 128), .32f);
                splash.age = -(delay + flight); splash.startScale = .2f; splash.endScale = 1; splash.ease = 3; splash.fadePower = 1.3f;
                var spark = Spawn("Light rain flash", glow, land + Vector2.up * .15f, .7f, Alpha(ArcheryHot, .8f), .1f, true, 129);
                spark.age = -(delay + flight); spark.endScale = 1.4f; spark.ease = 1; spark.fadePower = 1.5f;
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

        // A signature light arrow: outline, golden body and white-hot core, a tip glint, a flare wake and (level shots) a wind helix.
        // A piercing arrow (awakened fan) adds a gold-white edge, a long piercing filament and small rings left along its path;
        // a frosted one (awakened volley) carries an ice-blue glow and a turning snowflake on its tip.
        private void ArcheryLightArrow(Transform root, Color tint, float size, bool falling, bool pierce, bool frost, float shift = 0, bool frostFlake = true)
        {
            // shift: the drawn arrow rides this far ahead of (or behind) the gameplay point along its flight, so a volley's spears
            // do not fall in one rigid row; landing and hits still come from the gameplay point.
            Color light = ArcheryLight(tint); float len = 2.1f * size;
            var hot = Attach(root, "Light arrow core", ArcheryArrowSet[2], len, Alpha(ArcheryHot, .95f), 125);
            var layers = new List<Clip>
            {
                Attach(root, "Light arrow outline", ArcheryArrowSet[0], len, ArcheryInk, 118, false),
                Attach(root, "Light arrow body", ArcheryArrowSet[1], len, ArcheryBody, 119, false),
            };
            var tipGlow = Attach(root, "Arrow tip glow", glow, .95f * size, Alpha(frost ? ArcheryFrost : light, .75f), 124, true, new Vector2(.47f * len, 0)); tipGlow.pulse = .12f; layers.Add(tipGlow);
            if (!frost) { var tipStar = Attach(root, "Arrow tip star", glint, .9f * size, Alpha(ArcheryHot, .9f), 126, true, new Vector2(.47f * len, 0)); tipStar.spin = 500; layers.Add(tipStar); }
            if (pierce) layers.Add(Attach(root, "Awakened arrow edge", ArcheryArrowSet[0], len * 1.04f, Alpha(ArcheryAwake, .5f), 126));
            if (frost && frostFlake)
            {
                var crystal = Attach(root, "Frost tip flake", flake, .5f * size, new Color(.92f, .98f, 1, .95f), 127, true, new Vector2(.47f * len, 0)); crystal.spin = 260; layers.Add(crystal);
                var crystalInk = Attach(root, "Frost tip flake tone", flake, .58f * size, ArcheryFrostInk, 117, false, new Vector2(.47f * len, 0)); crystalInk.spin = 260; layers.Add(crystalInk);
            }
            if (shift != 0) foreach (var clip in layers) { clip.offset.x += shift; clip.root.transform.localPosition = clip.offset; }
            if (shift != 0) { hot.offset.x += shift; hot.root.transform.localPosition = hot.offset; }
            var trails = new List<ArcheryTrailLayer>();
            ArcheryTrail(root, "Arrow wake tone", streak, (falling ? 4.8f : 3.4f) * size, .3f, Alpha(ArcheryFlare, .5f), 116, false, -.1f * size + shift, trails);
            ArcheryTrail(root, "Arrow wake", streak, (falling ? 4.4f : 3f) * size, .2f, Alpha(frost ? Color.Lerp(light, ArcheryFrost, .6f) : light, .7f), 121, true, -.15f * size + shift, trails);
            if (!falling)
            {
                var helix = ArcheryTrail(root, "Arrow wind helix", ArcheryHelixFrames[0], 2.6f * size, 1, Alpha(Color.Lerp(light, ArcheryWind, .5f), .6f), 122, true, .2f * size, trails).clip;
                helix.frames = ArcheryHelixFrames; helix.fps = 30;
            }
            if (pierce) ArcheryTrail(root, "Arrow pierce filament", streak, 4.8f * size, .07f, new Color(1, 1, 1, .85f), 123, true, .5f * len, trails);
            foreach (var t in trails) layers.Add(t.clip);
            Vector2 launch = root.position; ArcheryGrowTrails(trails, 0);
            // Falling spears come in big volleys at higher levels, so each one sheds fewer motes.
            int beat = 0, every = falling && Grade >= 2 ? (Grade >= 3 ? 6 : 4) : 2; bool buried = false;
            Emitter(tipGlow, 60, .1f, (owner, p) =>
            {
                if (!root || buried) return;
                if (!falling)
                {
                    // An arrow of a fan that dips to the floor (the low arrows of a wide fan) buries itself there and bursts
                    // instead of sliding on through the stone.
                    Vector2 heading = root.right, tip = (Vector2)root.position + heading * (.47f * len + shift);
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
                Vector2 d = owner.root.transform.right, here = owner.root.transform.position;
                if (pierce && step % 4 == 2)
                {
                    var mark = ArcheryBoom(here - d * .2f * len, d, .95f * size, Alpha(step % 8 == 2 ? ArcheryAwake : light, .85f), .22f, 0); mark.startScale = .4f; mark.endScale = 1.2f;
                }
                else if (frost && step % 4 == 1)
                {
                    var rime = Spawn("Frost mote", glow, p - d * Range(.2f, .9f), Range(.12f, .22f) * size, Alpha(ArcheryFrost, .9f), Range(.25f, .4f), true, 124);
                    rime.velocity = -d * Range(.5f, 1.5f) + new Vector2(Range(-.4f, .4f), 0); rime.drag = 2; rime.flicker = .5f; rime.endScale = .2f; rime.ease = 1;
                }
                else if (step % 3 == 0)
                {
                    var ghost = ArcheryTurn(Spawn("Arrow afterimage", ArcheryArrowSet[1], here - d * .47f * len, len, Alpha(ArcheryFlare, .32f), .12f, false, 115), ArcheryAngle(d));
                    ghost.endScale = 1.1f; ghost.ease = 1; ghost.fadePower = 1.4f;
                }
                else if (step % 7 == 1)
                {
                    bool lit = step % 2 != 0;
                    var feather = ArcheryTurn(Spawn("Light feather", petal, p - d * .9f, Range(.2f, .32f) * size, lit ? Alpha(Color.Lerp(light, Color.white, .3f), .85f) : ArcheryFeather, Range(.4f, .6f), lit, 120), Range(0, 360));
                    feather.velocity = -d * Range(.5f, 1.5f) + new Vector2(0, Range(-.6f, .3f)); feather.spin = Range(-300, 300); feather.drag = 2; feather.gravity = 1; feather.fadePower = 1.4f;
                }
                else
                {
                    var mote = Spawn("Arrow mote", glow, p - d * Range(.4f, 1.4f), Range(.12f, .24f) * size, Alpha(step % 4 == 0 ? Color.white : light, .9f), Range(.22f, .36f), true, 124);
                    mote.velocity = new Vector2(-d.y, d.x) * Range(-1.2f, 1.2f) - d * Range(.5f, 2f); mote.drag = 2.5f; mote.flicker = .5f; mote.endScale = .2f; mote.ease = 1;
                }
            });
            Bundle(root, hot, layers.ToArray());
        }

        // Companions' basic shots: a real arrow with a short warm wake.
        private void PlainArrow(Transform root)
        {
            var primary = Attach(root, "Arrow", arrow, 1.25f, Color.white, 123, false);
            var tail = Attach(root, "Arrow wake", streak, 1.9f, new Color(1, .9f, .6f, .55f), 121, true, new Vector2(-.85f, 0)); tail.scale.y *= .3f;
            var halo = Attach(root, "Arrow tip light", glow, .6f, Alpha(ArcheryGold, .45f), 122, true, new Vector2(.45f, 0));
            Emitter(tail, Emit.Trace, 12, .03f, new Color(1, .9f, .6f, .3f));
            Bundle(root, primary, tail, halo);
        }

        private void ProjectilePierceShot(Transform root, Color tint, bool signature)
        {
            Color light = ArcheryLight(tint); Vector2 at = root.position, dir = ArcheryAim(root.right, Vector2.right);
            // the lance rides a little ahead of the hit point so its haft never trails behind the thrower at launch;
            // higher levels throw a longer lance, and the awakened one (unlimited pierce, wider hit) is wider still and gold-white
            int grade = signature ? Grade : 1; float s = signature ? ArcheryLevel(1.12f, 1.28f) : 1, wide = grade >= 3 ? 1.3f : 1;
            float len = (signature ? 3.3f : 2.1f) * s, lead = (signature ? .5f : .3f) * s;
            var hot = Attach(root, "Light lance core", ArcheryLanceSet[2], len, Alpha(grade >= 3 ? ArcheryAwake : ArcheryHot, .95f), 126, true, new Vector2(lead, 0));
            var ink = Attach(root, "Light lance outline", ArcheryLanceSet[0], len, ArcheryInk, 118, false, new Vector2(lead, 0));
            var body = Attach(root, "Light lance body", ArcheryLanceSet[1], len, ArcheryBody, 119, false, new Vector2(lead, 0));
            foreach (var x in new[] { hot, ink, body }) { x.scale.y *= wide; x.root.transform.localScale = x.scale; }
            if (!signature)
            {
                var trail = Attach(root, "Lance wake", streak, 2.6f, Alpha(light, .6f), 120, true, new Vector2(-1.2f, 0)); trail.scale.y *= .3f;
                Bundle(root, hot, ink, body, trail); return;
            }
            var star = Attach(root, "Lance tip star", glint, 1.6f * s * wide, Alpha(ArcheryHot, .95f), 128, true, new Vector2(.47f * len + lead, 0)); star.spin = 420; star.pulse = .12f;
            var halo = Attach(root, "Lance tip glow", glow, 1.5f * s * wide, Alpha(light, .7f), 124, true, new Vector2(.4f * len + lead, 0)); halo.pulse = .1f;
            var trails = new List<ArcheryTrailLayer>();
            var helix = ArcheryTrail(root, "Lance wind helix", ArcheryHelixFrames[0], 4f * s, 1.3f * s * wide, Alpha(Color.Lerp(light, ArcheryWind, .4f), .8f), 122, true, .8f * s, trails).clip;
            helix.frames = ArcheryHelixFrames; helix.fps = 36;
            ArcheryTrail(root, "Lance wake tone", streak, 6f * s, .42f * s * wide, Alpha(ArcheryFlare, .5f), 116, false, 0, trails);
            ArcheryTrail(root, "Lance wake", streak, 5.6f * s, .28f * s * wide, Alpha(light, .6f), 121, true, 0, trails);
            ArcheryTrail(root, "Lance wake core", streak, 3.6f * s, .1f * s, new Color(1, 1, 1, .75f), 123, true, 0, trails);
            var bundle = new List<Clip> { ink, body, star, halo };
            if (grade >= 3)
            {
                // Awakened: a wide gold-white corona round the whole flight path and a gilt edge on the lance.
                ArcheryTrail(root, "Awakened lance corona", streak, 7f * s, .75f * wide, Alpha(ArcheryAwake, .3f), 120, true, .4f * s, trails);
                var gilt = Attach(root, "Awakened lance edge", ArcheryLanceSet[0], len * 1.03f, Alpha(ArcheryAwake, .55f), 127, true, new Vector2(lead, 0));
                gilt.scale.y *= wide; gilt.root.transform.localScale = gilt.scale; bundle.Add(gilt);
            }
            Vector2 launch = at, scarFrom = at + dir * .3f; ArcheryGrowTrails(trails, 0);
            int beat = 0;
            // about 60 clips a second: scar segments, path rings, afterimages and sparks
            Emitter(halo, 60, .15f, (owner, p) =>
            {
                if (!root) return;
                Vector2 now = root.position, d = owner.root.transform.right, here = owner.root.transform.position, n = new Vector2(-d.y, d.x);
                ArcheryGrowTrails(trails, (now - launch).magnitude);
                // a burning scar laid down behind the haft: it lingers and decays after the lance has gone by
                Vector2 tail = now + d * (lead - .45f * len);
                if (Vector2.Dot(tail - scarFrom, d) > 1.2f) { ArcheryScar(scarFrom, tail, light); scarFrom = tail; }
                int phase = ++beat % 4;
                if (phase == 0)
                {
                    var spiral = ArcheryBoom(here - d * 1.4f * s, d, 1.7f * s * wide, Alpha(beat % 8 == 0 ? (grade >= 3 ? ArcheryAwake : ArcheryHot) : light, .8f), .34f, 0); spiral.startScale = .45f; spiral.endScale = 1.3f;
                    if (beat % 8 == 0) { var spiralTone = ArcheryBoom(here - d * 1.4f * s, d, 1.8f * s * wide, Alpha(ArcheryFlare, .55f), .3f, 0, false, 117); spiralTone.startScale = .45f; spiralTone.endScale = 1.3f; }
                }
                else if (phase == 2 && beat % 8 == 2)
                {
                    var ghost = ArcheryTurn(Spawn("Lance afterimage", ArcheryLanceSet[1], here - d * (.4f * len + .2f), len, Alpha(ArcheryFlare, .3f), .1f, false, 115), ArcheryAngle(d)); ghost.endScale = 1.08f; ghost.fadePower = 1.4f;
                }
                else if (phase == 2) Spark(p - d * Range(.6f, 2f), n * Range(-3, 3) - d * Range(1, 3), Chance(.3f) ? Color.white : light, Range(.22f, .38f), Range(.2f, .32f), 0, 2.5f);
            });
            foreach (var t in trails) bundle.Add(t.clip);
            Bundle(root, hot, bundle.ToArray());
            // launch: a sonic boom and a spark spray along the throw
            ArcheryBoom(at + dir * .8f, dir, 2.4f * s * wide, Alpha(ArcheryHot, .9f), .28f, 12);
            ArcheryBoom(at + dir * .5f, dir, 2.6f * s * wide, Alpha(ArcheryFlare, .55f), .3f, 9, false, 117);
            for (int i = 0, count = Budget(5); i < count; i++) Spark(at + dir * .6f, Direction(ArcheryAngle(dir) + Range(-20, 20)) * Range(7, 13), i % 2 == 0 ? Color.white : light, Range(.3f, .45f), Range(.18f, .28f), 0, 3);
            // every body the lance passes through is struck in passing; when it reaches the end of its flight it breaks into light
            ArcheryPassWatch(root, grade >= 3 ? .34f : .16f, .08f, (p, d) => ArcheryLanceThrough(p, d, light, s * wide, grade >= 3));
            ArcheryWatch(root, 4.5f, last =>
            {
                if (Vector2.Dot(last - scarFrom, dir) > .3f) ArcheryScar(scarFrom, last, light);
                if (ArcheryRecent(ArcheryPierceHit, .12f, last, 1)) return;
                ArcheryFizzle(last, dir, light, s * wide, grade >= 3);
            });
        }

        // The lance reaching the end of its flight detonates into light: a white-hot flash, burst lines, shock rings,
        // the lance's ghost driven on, light shards and motes, so the throw always ends on a beat.
        private void ArcheryFizzle(Vector2 pos, Vector2 dir, Color light, float s, bool awake)
        {
            // s: the lance's size for its level; an awakened lance ends in a gold-white burst.
            Vector2 head = pos + dir * 1.6f * s; float a = ArcheryAngle(dir);
            Color hot = awake ? ArcheryAwake : ArcheryHot;
            Underlay(head, 2.8f * s, .2f, .2f);
            var flash = Spawn("Lance burst flash", glow, head, 2.4f * s, Alpha(hot, .95f), .14f, true, 132); flash.startScale = .5f; flash.endScale = 1.5f; flash.ease = 2; flash.fadePower = 1.5f;
            var bloom = Spawn("Lance burst bloom", glow, head, 3.8f * s, Alpha(light, .45f), .38f, true, 123); bloom.startScale = .6f; bloom.endScale = 1.3f; bloom.ease = 2; bloom.fadePower = 1.5f;
            ImpactLines(head, 3.6f * s, Alpha(light, .8f), .22f);
            ArcheryFlareLines(head, 4f * s, ArcheryFlare, .24f);
            Glint(head, 2.6f * s, Alpha(hot, 1), .24f, a);
            Wave(head, 3.4f * s, Alpha(light, .85f), .32f, 3);
            ArcheryFlareRing(head, 3.5f * s, Alpha(ArcheryFlare, .55f), .3f);
            ArcheryBoom(head, dir, 2.8f * s, Alpha(hot, .85f), .3f, 6);
            ArcheryBoom(head, dir, 2.9f * s, Alpha(ArcheryFlare, .55f), .28f, 6, false, 117);
            foreach (var x in ArcheryShape(null, ArcheryLanceSet, pos + dir * .5f, a, 3.3f * s, .22f, Alpha(hot, .7f), 124)) { x.velocity = dir * 7; x.drag = 4; x.endScale = 1.3f; x.fadePower = 1.3f; }
            Shards(head, 6, 6, .32f * s, Alpha(hot, .9f), .5f);
            for (int i = 0, n = Budget(8); i < n; i++)
            {
                var mote = Spawn("Lance motes", glow, pos - dir * Range(-1.5f, 1.5f) + Inside(.3f), Range(.12f, .26f), Alpha(i % 3 == 0 ? Color.white : light, .9f), Range(.45f, .8f), true, 130);
                mote.velocity = dir * Range(1, 4) + Inside(1.2f); mote.drag = 2; mote.flicker = .5f; mote.endScale = .2f; mote.ease = 1;
            }
            ArcheryEmbers(head, 6, .4f, 1, light, .04f);
        }

        private void TrailPierceShot(Vector2 from, Vector2 to, Color tint)
        {
            Color light = ArcheryLight(tint);
            Vector2 delta = to - from; float length = delta.magnitude; if (length < .05f) return;
            Vector2 dir = delta / length; float a = ArcheryAngle(dir);
            ArcheryTrailEnd = to; ArcheryTrailDir = dir;
            var ink = ArcheryOpaque(Line(from, to, .5f, ArcheryInk, .3f, 117)); ink.axisEnd = new Vector2(1, .15f); ink.ease = 2;
            var tone = ArcheryOpaque(Line(from, to, .36f, Alpha(ArcheryFlare, .85f), .3f, 118)); tone.axisEnd = new Vector2(1, .15f); tone.ease = 2;
            var beam = Line(from, to, 1f, Alpha(light, .6f), .3f, 127, 1.06f); beam.axisEnd = new Vector2(1, .1f); beam.ease = 2;
            var filament = Line(from, to, .22f, Color.white, .22f, 130, 1.03f); filament.axisEnd = new Vector2(1, .2f); filament.ease = 2;
            int segments = Mathf.Clamp(Mathf.CeilToInt(length / 2.2f), 1, 8);
            for (int i = 0; i < segments; i++)
            {
                var twist = Sheet("Beam wind helix", ArcheryHelixFrames, from + dir * (length * (i + .5f) / segments), length / segments * 1.2f, Alpha(Color.Lerp(light, ArcheryWind, .4f), .8f), .32f, 128, 30, true);
                twist.angle = a; twist.scale.y *= 1.5f; twist.fadePower = 1.5f; twist.axisEnd = new Vector2(1, .4f);
            }
            int rings = Mathf.Clamp(Mathf.RoundToInt(length / 1.1f), 2, 10);
            for (int i = 0; i < rings; i++)
            {
                float t = (i + .5f) / rings;
                var spiral = ArcheryBoom(from + delta * t, dir, 1.4f, Alpha(i % 2 == 0 ? ArcheryHot : light, .8f), .3f, 0); spiral.age = -t * .08f; spiral.startScale = .3f; spiral.endScale = 1.1f;
                if (i % 2 == 0) { var spiralTone = ArcheryBoom(from + delta * t, dir, 1.5f, Alpha(ArcheryFlare, .5f), .28f, 0, false, 117); spiralTone.age = spiral.age; spiralTone.startScale = .3f; spiralTone.endScale = 1.1f; }
            }
            var muzzle = Spawn("Beam muzzle", glow, from, 1.3f, Alpha(ArcheryHot, .85f), .12f, true, 131); muzzle.endScale = 1.4f; muzzle.ease = 1; muzzle.fadePower = 1.5f;
            Glint(to, 1.8f, Alpha(ArcheryHot, 1), .2f, a);
            Vector2 side = new Vector2(-dir.y, dir.x);
            for (int i = 0, n = Budget(8); i < n; i++)
                Spark(from + delta * Range(.1f, 1), side * Range(-3, 3) + dir * Range(1, 4), i % 3 == 0 ? Color.white : light, Range(.2f, .35f), Range(.2f, .32f), 0, 2.5f);
        }

        // ---------------------------------------------------------------- hits and bursts

        private void ArrowBurst(Vector2 pos, float radius, Color tint, SkillEffect effect)
        {
            ArcheryStrike(pos, ArcheryLight(tint), Vector2.right, Mathf.Clamp(.55f + radius * .35f, .6f, 1.8f));
        }

        // A companion's basic arrow landing: a crisp glint, a small ring and a few sparks along the shot.
        private void ArcheryPrick(Vector2 pos, Color light, Vector2 dir)
        {
            float a = ArcheryAngle(dir);
            var flash = Spawn("Arrow hit flash", glow, pos, .8f, Alpha(ArcheryHot, .85f), .09f, true, 132); flash.endScale = 1.4f; flash.ease = 1; flash.fadePower = 1.5f;
            Glint(pos, 1.1f, Alpha(ArcheryHot, .95f), .14f, a + 45);
            ArcheryFlareLines(pos, 1.15f, Alpha(ArcheryFlare, .7f), .13f, 119);
            var wave = Spawn("Arrow hit ring", ring, pos, .95f, Alpha(light, .8f), .16f, true, 131); wave.startScale = .2f; wave.endScale = 1; wave.ease = 3; wave.fadePower = 1.3f;
            var through = Line(pos, pos + dir * .55f, .07f, new Color(1, 1, 1, .85f), .1f, 131); through.startScale = .4f; through.endScale = 1; through.ease = 3;
            for (int i = 0; i < 4; i++) Spark(pos, Direction(a + Range(-38, 38)) * Range(3, 6.5f), i % 2 == 0 ? Color.white : light, Range(.18f, .3f), Range(.13f, .22f), 6, 3);
        }

        // A signature arrow striking: white-hot core, flare and light burst lines, a glint, shock rings, piercing rays, sparks and feathers,
        // then a follow-through of drifting smoke, rising embers and an afterglow. Returns the hot core (marks the strike's moment).
        private Clip ArcheryStrike(Vector2 pos, Color light, Vector2 dir, float s)
        {
            float a = ArcheryAngle(dir);
            Underlay(pos, 2.6f * s, .24f, .34f);
            ArcheryFlareLines(pos, 4.2f * s, ArcheryFlare, .26f);
            var hot = Spawn("Arrow strike core", glow, pos, 1.5f * s, Alpha(ArcheryHot, 1), .1f, true, 133); hot.endScale = 1.5f; hot.ease = 1; hot.fadePower = 1.6f;
            var afterglow = Spawn("Arrow strike afterglow", glow, pos, 2f * s, Alpha(light, .4f), .75f, true, 122); afterglow.hold = .15f; afterglow.fadePower = 1.6f; afterglow.endScale = 1.35f; afterglow.ease = 1;
            for (int i = 0, n = Budget(2); i < n; i++)
            {
                var smokeTone = Puff(pos + Inside(.25f * s), Range(.8f, 1.1f) * s, new Color(.5f, .32f, .16f, .34f), -dir * Range(.4f, 1.1f) + new Vector2(0, Range(.4f, .9f)), Range(.7f, .95f), 112);
                smokeTone.endScale = 2.2f; smokeTone.fadeIn = .06f; smokeTone.drag = 2;
            }
            ArcheryEmbers(pos, 6, .35f * s, s, light, .05f);
            var bloom = Spawn("Arrow strike bloom", glow, pos, 2.6f * s, Alpha(light, .55f), .24f, true, 124); bloom.startScale = .6f; bloom.endScale = 1.3f; bloom.ease = 2; bloom.fadePower = 1.5f;
            ImpactLines(pos, 3.3f * s, Alpha(light, .8f), .2f);
            Glint(pos, 2.4f * s, Alpha(ArcheryHot, 1), .22f, a);
            Wave(pos, 2.6f * s, Alpha(light, .9f), .3f, 3);
            ArcheryFlareRing(pos, 2.8f * s, Alpha(ArcheryFlare, .6f), .3f);
            ArcheryBoom(pos + dir * .3f, dir, 2.2f * s, Alpha(ArcheryHot, .85f), .26f, 6);
            ArcheryBoom(pos + dir * .3f, dir, 2.3f * s, Alpha(ArcheryFlare, .55f), .24f, 5, false, 117);
            for (int i = -1; i <= 1; i++)
            {
                Vector2 d = Direction(a + i * 11);
                var pierce = Line(pos, pos + d * Range(1.6f, 2.6f) * s, i == 0 ? .14f : .09f, i == 0 ? Color.white : Alpha(light, .9f), .16f, 131);
                if (i != 0) { ArcheryOpaque(pierce); pierce.color = Alpha(ArcheryFlare, .75f); }
                pierce.startScale = .25f; pierce.endScale = 1; pierce.ease = 4; pierce.fadePower = 1.4f; pierce.velocity = d * 4;
            }
            for (int i = 0, n = Budget(12); i < n; i++)
            {
                bool back = i % 4 == 3;
                Spark(pos, Direction(a + (back ? 180 : 0) + Range(-40, 40)) * Range(5, 11) * s, i % 3 == 0 ? Color.white : light, Range(.25f, .45f) * s, Range(.2f, .38f), 6, 2.5f);
            }
            ArcheryFeathers(pos, 5, a, 70, 3.5f * s, light, s);
            return hot;
        }

        // A second or third arrow of the same volley landing on the same spot: a sharp beat on top of the strike, not another white-out.
        private void ArcheryStrikeEcho(Vector2 pos, Color light, Vector2 dir)
        {
            float a = ArcheryAngle(dir);
            var hot = Spawn("Arrow echo core", glow, pos + Inside(.2f), .9f, Alpha(ArcheryHot, .9f), .08f, true, 133); hot.endScale = 1.5f; hot.ease = 1; hot.fadePower = 1.5f;
            Glint(pos, 1.7f, Alpha(ArcheryHot, .95f), .16f, a + Range(20, 70));
            ArcheryFlareLines(pos, 2.4f, Alpha(ArcheryFlare, .7f), .16f, 119);
            var pierce = Line(pos, pos + dir * Range(1.6f, 2.2f), .1f, Color.white, .14f, 131); pierce.startScale = .25f; pierce.endScale = 1; pierce.ease = 4; pierce.fadePower = 1.4f; pierce.velocity = dir * 5;
            var pierceTone = ArcheryOpaque(Line(pos, pos + dir * 1.9f, .16f, Alpha(ArcheryFlare, .75f), .14f, 118)); pierceTone.startScale = .25f; pierceTone.endScale = 1; pierceTone.ease = 4; pierceTone.velocity = dir * 5;
            for (int i = 0, n = Budget(5); i < n; i++) Spark(pos, Direction(a + Range(-45, 45)) * Range(5, 10), i % 2 == 0 ? Color.white : light, Range(.24f, .38f), Range(.18f, .3f), 6, 2.5f);
            ArcheryFeathers(pos, 1, a, 60, 3, light, 1);
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
                var through = Line(pos, end, .55f, Alpha(ArcheryAwake, .6f), .2f, 127); through.axisStart = new Vector2(.2f, 1); through.axisEnd = new Vector2(1, .2f); through.ease = 3;
                var throughTone = ArcheryOpaque(Line(pos, end, .3f, Alpha(ArcheryFlare, .6f), .2f, 117)); throughTone.axisStart = new Vector2(.2f, 1); throughTone.axisEnd = new Vector2(1, .2f); throughTone.ease = 3;
            }
            // the fan's arrows often land on one target within a few frames: the first strikes in full, the others echo it
            if (ArcheryRecent(ArcheryStrikeMark, .1f, pos, 1.1f)) { ArcheryStrikeEcho(pos, light, direction); return; }
            float s = ArcheryLevel(1.1f, 1.2f);
            ArcheryStrikeMark = ArcheryStrike(pos, light, direction, 1.2f * s);
            // a holy brand burns on the target and lingers after the flash: a standing rune ring over a saturated tone
            float spin = direction.x >= 0 ? -110 : 110;
            var brand = RuneCircle(pos, 2.3f * s, Alpha(Color.Lerp(light, Grade >= 3 ? ArcheryAwake : ArcheryHot, .3f), .9f), .8f, spin, false, 126); brand.startScale = .45f; brand.endScale = 1.05f; brand.hold = .45f;
            var brandTone = Spawn("Holy brand tone", runeRing, pos, 2.36f * s, Alpha(ArcheryFlare, .55f), .8f, false, 117);
            brandTone.spin = spin; brandTone.angle = brand.angle = Range(0, 360); brandTone.fadeIn = .08f; brandTone.startScale = .45f; brandTone.endScale = 1.05f; brandTone.ease = 4; brandTone.hold = .45f; brandTone.fadePower = 1.4f;
        }

        /// <summary>A light arrow left standing in the target for a moment after it strikes, then fading.</summary>
        private void ArcheryEmbed(Vector2 pos, Color light, Vector2 dir)
        {
            const float len = 1.7f;
            foreach (var x in ArcheryShape(null, ArcheryArrowSet, pos - dir * (len * .47f - .4f) + Inside(.12f), ArcheryAngle(dir) + Range(-6, 6), len, .55f, Alpha(ArcheryHot, .9f), 126))
            { x.startScale = 1.12f; x.endScale = 1; x.ease = 8; x.hold = .5f; x.fadePower = 1.4f; }
        }

        private void HitPierceShot(Vector2 pos, Color tint, bool signature, Vector2 direction)
        {
            Color light = ArcheryLight(tint);
            if (!signature) { ArcheryPrick(pos, light, direction); return; }
            float a = ArcheryAngle(direction);
            ArcheryStrike(pos, light, direction, 1.35f);
            ArcheryPierceHit = Spawn("Lance burst", glow, pos, 3.6f, Alpha(light, .45f), .34f, true, 123); ArcheryPierceHit.startScale = .6f; ArcheryPierceHit.endScale = 1.3f; ArcheryPierceHit.ease = 2; ArcheryPierceHit.fadePower = 1.5f;
            Glint(pos, 2.2f, Alpha(ArcheryHot, .9f), .3f, a + 45);
            var shock = Wave(pos, 4.4f, Alpha(light, .8f), .4f, 3); shock.age = -.05f;
            ArcheryFlareRing(pos, 4.6f, Alpha(ArcheryFlare, .5f), .38f).age = -.05f;
            // the lance's light keeps going straight through the target
            Vector2 end = pos + direction * 4.5f;
            var through = Line(pos, end, .8f, Alpha(light, .55f), .22f, 127); through.axisStart = new Vector2(.2f, 1); through.axisEnd = new Vector2(1, .2f); through.ease = 3;
            var throughCore = Line(pos, end, .18f, Color.white, .18f, 130); throughCore.axisStart = new Vector2(.2f, 1); throughCore.axisEnd = new Vector2(1, .3f); throughCore.ease = 3;
            var throughTone = ArcheryOpaque(Line(pos, end, .4f, Alpha(ArcheryFlare, .6f), .2f, 117)); throughTone.axisStart = new Vector2(.2f, 1); throughTone.axisEnd = new Vector2(1, .2f); throughTone.ease = 3;
            Shards(pos, 6, 7, .35f, Alpha(ArcheryHot, .9f), .5f);
            EmberBurst(pos, 8, 5, light, .8f, 3);
        }

        private void HitRainArrows(Vector2 pos, Color tint, bool signature, Vector2 direction)
        {
            Color light = ArcheryLight(tint);
            if (!signature) { ArcheryPrick(pos, light, direction); return; }
            float height = pos.y - ArcheryRainGround;
            float ground = !float.IsNaN(ArcheryRainGround) && height > -.2f && height < 3 ? ArcheryRainGround : pos.y - .9f;
            float a = ArcheryAngle(direction);
            // the volley's first spear to strike also shows the whole splash: a ground shock as wide as the area, a dust wall and a burn mark;
            // the spears landing with it (a level-three volley brings nine) burst smaller on top of it
            bool first = !ArcheryRecent(ArcheryVolleyMark, .14f, pos, 3.5f);
            if (first) ArcheryVolleyMark = ArcheryVolleyBlast(new Vector2(pos.x, ground), light, ArcheryLevel(1.12f, 1.25f));
            // bigger volleys: every spear lands lite and the volley blast alone carries the splash
            ArcheryGroundBurst(new Vector2(pos.x, ground), light, 1.1f, .02f, first && Grade < 2, a, Grade >= 2);
            if (Grade >= 3) ArcheryFrostBite(pos, ground, first);
            var hot = Spawn("Spear strike core", glow, pos, 1.3f, Alpha(ArcheryHot, 1), .1f, true, 133); hot.endScale = 1.5f; hot.ease = 1; hot.fadePower = 1.6f;
            if (first) { ArcheryFlareLines(pos, 3f, ArcheryFlare, .2f); ImpactLines(pos, 3f, Alpha(light, .8f), .2f); }
            Glint(pos, 2f, Alpha(ArcheryHot, 1), .2f, a);
            for (int i = 0, n = first ? Budget(5) : Grade >= 2 ? 2 : 3; i < n; i++) Spark(pos, Direction(a + Range(-60, 60) + (i % 2 == 0 ? 180 : 0)) * Range(4, 8), i % 2 == 0 ? Color.white : light, Range(.22f, .36f), Range(.18f, .3f), 6, 2.5f);
        }

        /// <summary>An awakened volley's spear biting with frost (it slows what it hits): an ice-blue flash and snowflake on the body,
        /// ice shards and a frost bloom spreading on the floor; the first spear also leaves a chill mist over the splash.</summary>
        private void ArcheryFrostBite(Vector2 pos, float ground, bool first)
        {
            var chill = Spawn("Frost bite flash", glow, pos, 1.6f, Alpha(ArcheryFrost, .8f), .3f, true, 131); chill.startScale = .5f; chill.endScale = 1.3f; chill.ease = 2; chill.fadePower = 1.4f;
            var star = Spawn("Frost bite flake", flake, pos, 1.1f, new Color(.92f, .98f, 1, .95f), .45f, true, 132); star.startScale = .3f; star.endScale = 1; star.ease = 4; star.spin = 120; star.hold = .3f; star.fadePower = 1.5f;
            var starInk = Spawn("Frost bite flake tone", flake, pos, 1.25f, ArcheryFrostInk, .45f, false, 118); starInk.startScale = .3f; starInk.endScale = 1; starInk.ease = 4; starInk.spin = 120; starInk.hold = .3f; starInk.fadePower = 1.5f;
            if (first) Shards(pos, 4, 4.5f, .26f, Alpha(ArcheryFrost, .95f), .45f);
            // Nine spears land within a tenth of a second: the frost bloom on the floor is laid by the first and every third.
            if (!first && ArcheryFrostBites++ % 3 != 0) return;
            Vector2 floor = new Vector2(pos.x, ground);
            var bloomInk = ArcheryFlat(Spawn("Frost bloom tone", sigil, floor + Vector2.up * .03f, 2f, ArcheryFrostInk, 1, false, 25), .3f);
            var bloom = ArcheryFlat(Spawn("Frost bloom", sigil, floor + Vector2.up * .03f, 1.9f, Alpha(ArcheryFrost, .9f), .9f, true, 27), .3f);
            foreach (var x in new[] { bloomInk, bloom }) { x.startScale = .3f; x.endScale = 1; x.ease = 4; x.hold = .45f; x.fadePower = 1.4f; }
            if (!first) return;
            for (int i = 0, n = ArcheryShare(3); i < n; i++)
            {
                var mist = Puff(floor + new Vector2(Range(-2.2f, 2.2f), .25f), Range(1, 1.4f), new Color(.78f, .92f, 1, .38f), new Vector2(Range(-.5f, .5f), Range(.2f, .5f)), Range(.9f, 1.2f), 112);
                mist.fadeIn = .1f; mist.drag = 1.2f; mist.endScale = 2;
            }
        }

        /// <summary>A light spear landing on the ground: flash, short pillar, ground rings, a cracked mark, the spear left standing, sparks and dust.</summary>
        private void ArcheryGroundBurst(Vector2 ground, Color light, float s, float delay, bool big, float angle, bool lite = false)
        {
            // lite: one of many spears of a level-two or level-three volley; it keeps the flash, column, ring, mark and the
            // standing spear and drops the tone twins of the small rings, the glow pool and most of the sparks and dust.
            Vector2 lift = ground + Vector2.up * .35f * s;
            if (lite && !big)
            {
                var quick = Spawn("Spear landing flash", glow, lift, 1.5f * s, Alpha(ArcheryHot, .9f), .12f, true, 132); quick.age = -delay; quick.endScale = 1.5f; quick.ease = 1; quick.fadePower = 1.5f;
                var post = Pillar(ground, .85f * s, 2.3f * s, Alpha(light, .85f), .26f); post.age = -delay;
                var ripple = GroundWave(ground + Vector2.up * .05f, 2.8f * s, Alpha(light, .9f), .3f); ripple.age = -delay;
                var scar = Decal(ground, crack, 1.3f * s, Alpha(ArcheryInk, .75f), .9f, false, 24); scar.age = -delay;
                Vector2 down = Direction(angle); if (down.y > -.2f) down = Direction(-80.5f);
                float shaft = 1.5f * s;
                foreach (var x in ArcheryShape(null, ArcheryArrowSet, ground - down * (shaft * .47f - .12f * s), ArcheryAngle(down), shaft, .75f, Alpha(ArcheryHot, .9f), 125)) { x.age = -delay; x.hold = .45f; x.fadePower = 1.4f; }
                Spark(lift, Direction(Range(25, 155)) * Range(4, 9) * s, Color.white, Range(.25f, .4f), Range(.25f, .4f), 9, 2).age = -delay;
                return;
            }
            var flash = Spawn("Spear landing flash", glow, lift, 1.6f * s, Alpha(ArcheryHot, .9f), .12f, true, 132); flash.age = -delay; flash.endScale = 1.5f; flash.ease = 1; flash.fadePower = 1.5f;
            var column = Pillar(ground, .9f * s, (big ? 3.8f : 2.4f) * s, Alpha(light, .85f), big ? .36f : .28f); column.age = -delay;
            var columnTone = ArcheryOpaque(Pillar(ground, 1.3f * s, (big ? 3.4f : 2.1f) * s, Alpha(ArcheryFlare, .5f), big ? .34f : .26f, 117)); columnTone.age = -delay;
            var wave = GroundWave(ground + Vector2.up * .05f, (big ? 5f : 3f) * s, Alpha(light, .9f), .34f); wave.age = -delay;
            var waveTone = ArcheryFlat(ArcheryFlareRing(ground, (big ? 5f : 2.9f) * s, Alpha(ArcheryFlare, .6f), .32f, 25), .34f); waveTone.age = -delay;
            var mark = Decal(ground, crack, (big ? 2.2f : 1.4f) * s, Alpha(ArcheryInk, .75f), big ? 1.4f : 1f, false, 24); mark.age = -delay;
            if (big) { var hotMark = Decal(ground, crack, 2f * s, Alpha(light, .8f), .6f, true, 26); hotMark.age = -delay; hotMark.fadePower = 1.8f; }
            Vector2 d = Direction(angle); if (d.y > -.2f) d = Direction(-80.5f);
            float len = (big ? 1.75f : 1.5f) * s;
            var glowPool = GroundGlow(ground, (big ? 2.6f : 1.8f) * s, Alpha(light, .6f), big ? 1.1f : .8f); glowPool.age = -delay;
            foreach (var x in ArcheryShape(null, ArcheryArrowSet, ground - d * (len * .47f - .12f * s), ArcheryAngle(d), len, big ? 1.1f : .8f, Alpha(ArcheryHot, .9f), 125)) { x.age = -delay; x.hold = .45f; x.fadePower = 1.4f; }
            for (int i = 0, n = ArcheryShare(big ? 8 : 3); i < n; i++)
                Spark(lift, Direction(Range(25, 155)) * Range(4, 9) * s, i % 3 == 0 ? Color.white : light, Range(.25f, .4f), Range(.25f, .45f), 9, 2).age = -delay;
            for (int i = 0, n = ArcheryShare(big ? 4 : 2); i < n; i++)
            {
                var puff = Puff(ground + new Vector2(Range(-.5f, .5f) * s, .1f), Range(.5f, .8f) * s, DustTone, new Vector2((i % 2 == 0 ? 1 : -1) * Range(1.2f, 2.6f) * s, Range(.3f, .9f)), Range(.5f, .75f), 118);
                puff.age = -delay; puff.gravity = -.3f;
            }
            if (big)
                for (int i = 0, n = ArcheryShare(4); i < n; i++)
                {
                    var chip = Spawn("Spear debris", rock[i % rock.Length], lift, Range(.1f, .2f) * s, Color.white, Range(.5f, .8f), false, 128);
                    chip.age = -delay; chip.velocity = new Vector2(Range(-3.5f, 3.5f), Range(3, 6.5f)) * s; chip.gravity = 18; chip.spin = Range(-600, 600); chip.fadePower = .6f; chip.hold = .5f;
                }
        }

        /// <summary>The splash of a rain volley: ground shocks as wide as the splash area (radius about 3.5), squashed burst lines,
        /// a dust wall rolling out both ways, a burn mark and rising light. Returns its flash, which marks the volley's moment.</summary>
        private Clip ArcheryVolleyBlast(Vector2 ground, Color light, float area)
        {
            // area: the splash's growth with the volley's level (1, 1.12, 1.25 of its 3.5 radius).
            Vector2 lift = ground + Vector2.up * .6f;
            ArcheryFlat(Underlay(ground + Vector2.up * .3f, 6.4f * area, .22f, .45f), .5f);
            var flash = Spawn("Volley blast flash", glow, lift, 3.6f * area, Alpha(ArcheryHot, .6f), .16f, true, 132); flash.startScale = .5f; flash.endScale = 1.4f; flash.ease = 2; flash.fadePower = 1.5f;
            // the outer shock lands exactly on the splash radius
            float reach = 3.5f * area * 2 / .74f;
            var shock = ArcheryFlat(Spawn("Volley shock", ring, ground + Vector2.up * .05f, reach, Alpha(light, .95f), .46f, true, 130), .3f); shock.startScale = .12f; shock.endScale = 1; shock.ease = 3; shock.fadePower = 1.3f;
            var shockTone = ArcheryFlat(ArcheryFlareRing(ground, reach * 1.02f, Alpha(ArcheryFlare, .7f), .46f, 25), .3f); shockTone.ease = 3;
            var inner = ArcheryFlat(Spawn("Volley inner shock", ring, ground + Vector2.up * .05f, 4.6f * area, Alpha(ArcheryHot, .9f), .32f, true, 131), .3f); inner.startScale = .1f; inner.endScale = 1; inner.ease = 2.5f; inner.fadePower = 1.4f; inner.age = -.05f;
            var lines = ArcheryFlat(ArcheryFlareLines(ground + Vector2.up * .3f, 7.2f * area, ArcheryFlare, .32f), .45f); lines.angle = 0;
            var lit = ArcheryFlat(ImpactLines(ground + Vector2.up * .3f, 6.8f * area, Alpha(light, .85f), .28f), .45f); lit.angle = 0;
            var burn = Decal(ground, scorch, 4.4f * area, new Color(.22f, .1f, .02f, .5f), 1.6f, false, 24); burn.angle = Range(-4, 4);
            var seam = Decal(ground, crack, 3.8f * area, Alpha(light, .85f), .9f, true, 26); seam.fadePower = 1.8f;
            GroundGlow(ground, 7.2f * area, Alpha(light, .5f), 1.1f);
            for (int i = 0, n = ArcheryShare(8); i < n; i++)
            {
                float side = i % 2 == 0 ? 1 : -1, t = (i / 2) / 3f;
                var dust = Puff(ground + new Vector2(side * Range(.2f, 1f), .15f), Range(.8f, 1.2f), DustTone, new Vector2(side * Mathf.Lerp(3, 7, t) * Range(.85f, 1.1f), Range(.3f, 1f)), Range(.7f, 1f), 118);
                dust.gravity = -.4f; dust.endScale = 2.6f; dust.drag = 2.4f;
            }
            ArcheryEmbers(lift, Mathf.RoundToInt(8 * ArcheryLevel(.85f, .7f)), 2.2f, 1.1f, light, .05f);
            return flash;
        }

        private void ImpactMultiShot(Vector2 pos, float radius, Color tint, int facing)
        {
            Color light = ArcheryLight(tint); float r = Mathf.Clamp(radius, .7f, 3.5f), s = Mathf.Lerp(.75f, 1.6f, (r - .7f) / 2.8f);
            ArcheryStrike(pos, light, new Vector2(facing == 0 ? 1 : facing, 0), s);
            // a fan of light arrows bursting out of the strike
            for (int i = 0, n = Budget(6); i < n; i++)
            {
                float angle = facing == 0 ? i * 360f / n + Range(-10, 10) : (facing > 0 ? 0 : 180) + (i - (n - 1) * .5f) * 14;
                Vector2 d = Direction(angle);
                foreach (var x in ArcheryShape(null, ArcheryArrowSet, pos + d * .5f * r, angle, 1.5f * s, .3f, Alpha(ArcheryHot, .95f), 128)) { x.velocity = d * 9 * s; x.drag = 3; x.fadePower = 1.6f; x.hold = .3f; }
            }
        }

        private void ImpactPierceShot(Vector2 pos, float radius, Color tint, int facing)
        {
            Color light = ArcheryLight(tint); float r = Mathf.Clamp(radius, .6f, 3.5f), s = .55f + r * .35f;
            Vector2 dir = facing != 0 ? new Vector2(facing, 0) : (ArcheryTrailEnd - pos).sqrMagnitude < .5f ? ArcheryTrailDir : Vector2.right;
            ArcheryStrike(pos, light, dir, s);
            var portal = ArcheryThin(Spawn("Pierce rune ring", runeRing, pos, 2.4f * s, Alpha(light, .9f), .32f, true, 127), .32f);
            var portalTone = ArcheryThin(Spawn("Pierce rune ring tone", runeRing, pos, 2.45f * s, Alpha(ArcheryFlare, .55f), .3f, false, 117), .32f);
            foreach (var x in new[] { portal, portalTone }) { x.angle = ArcheryAngle(dir); x.startScale = .4f; x.endScale = 1.15f; x.ease = 3; x.fadePower = 1.4f; }
            Shards(pos, 4, 6 * s, .3f * s, Alpha(ArcheryHot, .9f), .45f);
            EmberBurst(pos, 6, 4 * s, light, .7f, 3);
        }

        private void ImpactRainArrows(Vector2 pos, float radius, Color tint, int facing)
        {
            Color light = ArcheryLight(tint);
            float r = Mathf.Clamp(radius, .7f, 3.5f), groundY = pos.y - .8f;
            Vector2 center = new Vector2(pos.x, groundY);
            var markTone = ArcheryFlat(Spawn("Volley mark tone", runeRing, center, r * 2.7f, Alpha(ArcheryFlare, .55f), 1.1f, false, 25), .3f);
            var mark = ArcheryFlat(Spawn("Volley mark", runeRing, center, r * 2.6f, Alpha(light, .9f), 1.1f, true, 27), .3f);
            foreach (var x in new[] { markTone, mark }) { x.fadeIn = .05f; x.startScale = .5f; x.endScale = 1; x.ease = 4; x.hold = .5f; x.fadePower = 1.4f; }
            GroundGlow(center, r * 3f, Alpha(light, .45f), .9f);
            int n = Budget(Mathf.Clamp(Mathf.RoundToInt(3 + r * 1.2f), 4, 7));
            Vector2 vel = new Vector2(2.4f, -16); float fall = .2f, angle = ArcheryAngle(vel);
            for (int i = 0; i < n; i++)
            {
                float delay = i * .045f + Range(0, .025f), x = ((i * .618f) % 1 * 2 - 1) * r * .85f;
                Vector2 land = new Vector2(pos.x + x, groundY), start = land - vel * fall;
                foreach (var c in ArcheryShape(null, ArcheryArrowSet, start, angle, 1.9f, fall, Alpha(ArcheryHot, 1), 128)) { c.velocity = vel; c.age = -delay; c.fadePower = 0; }
                var wake = Spawn("Falling spear wake", streak, start - vel.normalized * 1.6f, 3.6f, Alpha(light, .7f), fall, true, 127); wake.scale.y *= .3f; wake.angle = angle; wake.velocity = vel; wake.age = -delay; wake.fadePower = 0;
                ArcheryGroundBurst(land, light, Mathf.Lerp(.75f, 1, r / 3.5f), delay + fall, false, angle);
            }
            var shock = GroundWave(center + Vector2.up * .05f, r * 3.4f, Alpha(light, .9f), .5f); shock.age = -.2f;
            var lines = ArcheryFlareLines(pos, r * 3f, ArcheryFlare, .3f); lines.age = -.2f;
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
                    var beam = Pillar(site + Vector2.up * 10, 1f, 10, Alpha(ArcherySkyHot, .9f), .16f, 130); beam.angle = 180;
                    var land = Spawn("Sentry quick landing", glow, site + Vector2.up * 1.4f, 1.8f, Alpha(ArcherySkyHot, .85f), .14f, true, 132); land.endScale = 1.4f; land.ease = 1; land.fadePower = 1.5f;
                }
                return;
            }
            // the summoning hand
            var handGlow = Anchored(c.anchor, hand, "Sentry hand glow", glow, 1.5f, Alpha(sky, .8f), w + .1f, 126);
            handGlow.startScale = .3f; handGlow.endScale = 1; handGlow.ease = 1.5f; handGlow.hold = .8f; handGlow.pulse = .1f; handGlow.fadeIn = .05f;
            var handStar = Anchored(c.anchor, hand, "Sentry hand star", glint, 1.2f, Alpha(ArcherySkyHot, .9f), w + .1f, 129);
            handStar.startScale = .2f; handStar.endScale = 1; handStar.ease = 1.5f; handStar.spin = 200; handStar.hold = .8f;
            Converge(hand, 10, 2f, sky, w, .4f);
            // The sentinel deploys behind the hero; higher levels draw a bigger circle and a taller column, and an awakened cast
            // deploys a second sentinel on the other side of the hero a beat later.
            float s = ArcheryLevel(1.1f, 1.2f);
            ArcherySentrySite(new Vector2(c.pos.x - f * 1.1f, feet.y), sky, w, s, 0);
            if (Grade >= 3) ArcherySentrySite(new Vector2(c.pos.x + f * 1.1f, feet.y), sky, w, s, .06f);
        }

        /// <summary>One sentinel's deployment: a circle drawn on the floor, a targeting laser and a summoning ring, then at the release
        /// a beam slamming down and a column of light where it lands. <paramref name="lag"/> delays the landing.</summary>
        private void ArcherySentrySite(Vector2 site, Color sky, float w, float s, float lag)
        {
            int grade = Grade;
            float circleLife = w + .9f;
            var circleTone = ArcheryFlat(Spawn("Sentry circle tone", runeRing, site, 3f * s, Alpha(ArcherySkyBody, .6f), circleLife, false, 25), .3f);
            var circle = ArcheryFlat(Spawn("Sentry circle", runeRing, site, 2.85f * s, Alpha(sky, .95f), circleLife, true, 27), .3f);
            var lattice = ArcheryFlat(Spawn("Sentry lattice", hex, site, 2.2f * s, Alpha(sky, .45f), circleLife, true, 26), .3f);
            foreach (var x in new[] { circleTone, circle, lattice }) { x.fadeIn = .06f; x.startScale = .3f; x.endScale = 1; x.ease = 4; x.hold = .6f; x.fadePower = 1.4f; }
            lattice.flicker = .3f;
            GroundGlow(site, 3.8f * s, Alpha(sky, .5f), circleLife);
            float spread = 1.6f * s;
            Emitter(circle, 24, 0, (owner, p) =>
            {
                var bit = Spawn("Sentry data mote", core, new Vector2(site.x + Range(-spread, spread), site.y + Range(-.15f, .15f)), Range(.05f, .11f), Alpha(Chance(.3f) ? ArcherySkyHot : sky, .95f), Range(.35f, .6f), true, 128);
                bit.velocity = new Vector2(0, Range(1.4f, 3)); bit.flicker = .5f; bit.fadeIn = .05f; bit.endScale = .3f; bit.ease = 1;
            });
            // a targeting laser from the sky and a summoning ring above the site
            var laser = Pillar(site, .35f, 9, Alpha(sky, .55f), w, 126); laser.flicker = .4f; laser.fadeIn = w * .4f; laser.hold = .8f; laser.fadePower = 2;
            var halo = Spawn("Sentry summoning ring", runeRing, site + Vector2.up * 2.4f, 1.9f * s, Alpha(sky, .85f), w + .2f, true, 125);
            var haloTone = Spawn("Sentry summoning ring tone", runeRing, site + Vector2.up * 2.4f, 1.95f * s, Alpha(ArcherySkyBody, .5f), w + .2f, false, 117);
            var halos = new List<Clip> { halo, haloTone };
            // Awakened: a gold-white ring turns the other way inside the summoning ring.
            if (grade >= 3) halos.Add(Spawn("Sentry summoning ring gilt", runeRing, site + Vector2.up * 2.4f, 1.35f, Alpha(ArcheryAwake, .9f), w + .2f, true, 126));
            foreach (var x in halos) { x.spin = x == halos[halos.Count - 1] && grade >= 3 ? -220 : 160; x.startScale = .2f; x.endScale = 1; x.ease = 3; x.hold = .7f; x.fadeIn = .06f; x.fadePower = 1.5f; }
            // release: a beam slams down from the sky and a column of light erupts where the sentinel lands
            float r0 = w - .02f + lag; Vector2 body = site + Vector2.up * 1.15f;
            var descent = Pillar(site + Vector2.up * 10, 1.1f * s, 10, Alpha(ArcherySkyHot, .9f), .16f, 130); descent.angle = 180; descent.age = -(r0 - .06f);
            var column = Pillar(site, 1.8f * s, 7 * s, Alpha(sky, .7f), .4f, 128); column.age = -r0;
            var columnCore = Pillar(site, .7f * s, 8 * s, Alpha(grade >= 3 ? ArcheryAwake : ArcherySkyHot, .9f), .26f, 129); columnCore.age = -r0;
            var columnTone = ArcheryOpaque(Pillar(site, 2.4f * s, 6 * s, Alpha(ArcherySkyBody, .45f), .4f, 117)); columnTone.age = -r0;
            var flash = Spawn("Sentry landing flash", glow, body, 1.6f * s, Alpha(ArcherySkyHot, .85f), .12f, true, 132); flash.age = -r0; flash.endScale = 1.4f; flash.ease = 1; flash.fadePower = 1.5f;
            ImpactLines(body, 3.4f * s, Alpha(sky, .75f), .22f).age = -r0;
            ArcheryFlareLines(body, 4f * s, Alpha(ArcherySkyBody, .7f), .24f, 117).age = -r0;
            Glint(body, 2.2f * s, Alpha(ArcherySkyHot, 1), .22f).age = -r0;
            var shield = Spawn("Sentry lattice flash", hex, body, 2.4f * s, Alpha(sky, .65f), .3f, true, 127); shield.age = -r0; shield.startScale = .4f; shield.endScale = 1.15f; shield.ease = 3; shield.fadePower = 1.4f;
            GroundWave(site + Vector2.up * .05f, 5.5f * s, Alpha(sky, .9f), .4f).age = -r0;
            ArcheryFlat(ArcheryFlareRing(site, 5.4f * s, Alpha(ArcherySkyBody, .55f), .38f, 25), .34f).age = -r0;
            for (int i = 0, n = ArcheryShare(10); i < n; i++)
                Spark(body, Direction(Range(15, 165)) * Range(4, 9), i % 3 == 0 ? Color.white : sky, Range(.25f, .42f), Range(.25f, .4f), 8, 2).age = -r0;
            for (int i = 0, n = ArcheryShare(4); i < n; i++)
            {
                var dust = Puff(site + new Vector2(Range(-.5f, .5f), .1f), Range(.55f, .85f), DustTone, new Vector2((i % 2 == 0 ? 1 : -1) * Range(1.4f, 2.8f), Range(.2f, .7f)), Range(.5f, .7f), 118);
                dust.age = -r0; dust.gravity = -.3f;
            }
        }

        private void ZoneSentry(Transform root, float radius, float life)
        {
            Color sky = ArcherySky; int grade = Grade;
            float hold = Mathf.Clamp01(1 - .45f / life), pop = ArcheryPop(life, .2f);
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
            // The wheel, aura and floor ring stay close round the crystal so a hero standing between two sentinels stays clear.
            float wheelSize = ArcheryLevel(1.12f, 1.22f);
            var body = Attach(root, "Sentinel crystal", ArcherySentinelSprite, 1.4f, Color.white, 28, false, hover);
            var eye = Attach(root, "Sentinel eye", glow, .8f, Alpha(ArcherySkyHot, .9f), 29, true, eyeAt); eye.pulse = .15f;
            var heart = Attach(root, "Sentinel aura", glow, 2.1f, Alpha(sky, .45f), 27, true, eyeAt); heart.pulse = .1f;
            var wheel = Attach(root, "Sentinel halo wheel", runeRing, 1.7f * wheelSize, Alpha(sky, .65f), 26, true, eyeAt); wheel.spin = 45;
            var wheelTone = Attach(root, "Sentinel halo wheel tone", runeRing, 1.74f * wheelSize, Alpha(ArcherySkyBody, .5f), 25, false, eyeAt); wheelTone.spin = 45;
            var floorTone = ArcheryFlat(Attach(root, "Sentinel floor ring tone", runeRing, 2.5f, Alpha(ArcherySkyBody, .55f), 25, false, floorAt), .3f);
            var floor = ArcheryFlat(Attach(root, "Sentinel floor ring", runeRing, 2.4f, Alpha(sky, .85f), 27, true, floorAt), .3f); floor.pulse = .04f;
            var lattice = ArcheryFlat(Attach(root, "Sentinel floor lattice", hex, 1.9f, Alpha(sky, .35f), 26, true, floorAt), .3f); lattice.flicker = .25f;
            var floorGlow = ArcheryFlat(Attach(root, "Sentinel floor glow", glow, 2.5f, Alpha(sky, .55f), 26, true, floorAt), .35f); floorGlow.pulse = .14f;
            var scan = Attach(root, "Sentinel scan cone", ArcheryScanSprite, 5.5f, Alpha(sky, .12f), 27, true, eyeAt);
            var mounts = new List<Clip> { wheel, wheelTone };
            if (grade >= 3)
            {
                var gilt = Attach(root, "Sentinel gilt ring", runeRing, 1.3f, Alpha(ArcheryAwake, .8f), 28, true, eyeAt); gilt.spin = -110;
                var giltGlow = Attach(root, "Sentinel gilt glow", glow, 1.5f, Alpha(ArcheryAwake, .3f), 27, true, eyeAt); giltGlow.pulse = .08f;
                mounts.Add(gilt); mounts.Add(giltGlow);
            }
            var layers = new List<Clip> { body, eye, heart, floorTone, floor, lattice, floorGlow, scan }; layers.AddRange(mounts);
            foreach (var l in layers) { l.life = life; l.hold = hold; l.fadePower = 1.2f; l.fadeIn = .12f; }
            body.startScale = .3f; body.endScale = 1; body.ease = pop;
            foreach (var l in new[] { floorTone, floor, lattice }) { l.startScale = .3f; l.endScale = 1; l.ease = pop * .6f; }
            foreach (var l in mounts) { l.startScale = .3f; l.endScale = 1; l.ease = pop * .6f; }
            var rig = new ArcherySentryRig { root = root, body = body, eye = eye, scan = scan, heart = heart, baseScale = body.scale, eyeAt = eyeAt, side = side,
                back = back, paired = partner != null, frame = Time.frameCount, mounts = mounts.ToArray(), floors = new[] { floorTone, floor, lattice, floorGlow } };
            scan.angle = rig.side > 0 ? 0 : 180;
            ArcheryRigs.Add(rig);
            Emitter(floorGlow, 60, 0, (owner, p) =>
            {
                if (!rig.root) return;
                float t = owner.age, since = t - rig.fireAt, kick = Mathf.Exp(-Mathf.Max(0, since) * 18);
                float bob = Mathf.Sin(t * 2.4f) * .05f;
                rig.eyeAt = new Vector2(rig.back, .42f);
                body.offset = new Vector2(rig.back, .3f + bob); eye.offset = heart.offset = scan.offset = rig.eyeAt + new Vector2(0, bob);
                foreach (var m in rig.mounts) m.offset = rig.eyeAt;
                foreach (var m in rig.floors) m.offset = new Vector2(rig.back, -.72f);
                body.scale = new Vector3(rig.baseScale.x * (1 + .14f * kick), rig.baseScale.y * (1 - .08f * kick), 1);
                // At rest a sentinel scans outward, away from the hero (so a pair looks out both ways); after a burst it follows
                // its target a moment, the cone flaring only for a quarter second after each shot.
                float rest = rig.paired ? (rig.back > 0 ? 0 : 180) : (rig.side > 0 ? 0 : 180);
                float idle = rest + Mathf.Sin(t * 1.5f) * 22;
                scan.angle = Mathf.LerpAngle(scan.angle, since < .7f ? rig.aim : idle, since < .7f ? .5f : .08f);
                scan.color = Alpha(sky, since < .25f ? .45f : .12f);
                eye.color = Alpha(ArcherySkyHot, since < .15f ? 1 : .8f);
                heart.color = Alpha(sky, since < .2f ? .75f : .4f);
                if (Chance(.16f))
                {
                    Vector2 at = rig.root.position;
                    var bit = Spawn("Sentinel data mote", core, at + new Vector2(rig.back + Range(-.9f, .9f), -.7f), Range(.05f, .1f), Alpha(Chance(.3f) ? ArcherySkyHot : sky, .9f), Range(.5f, .8f), true, 128);
                    bit.velocity = new Vector2(0, Range(1.2f, 2.4f)); bit.flicker = .5f; bit.fadeIn = .1f; bit.endScale = .3f; bit.ease = 1;
                }
            });
            layers.Remove(body);
            Bundle(root, body, layers.ToArray());
            // boot-up: the eye lights and a ring runs across the floor
            Vector2 origin = root.position;
            var boot = Glint(origin + eyeAt, 1.6f, Alpha(ArcherySkyHot, 1), .3f); boot.age = -.12f;
            GroundWave(origin + floorAt + Vector2.up * .05f, 3f, Alpha(sky, .8f), .4f);
        }

        private void ProjectileSentry(Transform root, Color tint, bool signature)
        {
            Color sky = Color.Lerp(ArcherySky, new Color(tint.r, tint.g, tint.b, 1), .12f);
            Vector2 at = root.position, dir = ArcheryAim(root.right, Vector2.right); float a = ArcheryAngle(dir);
            int k = ArcherySentryShots++ & 3;
            float lateral = signature ? (k - 1.5f) * .12f : 0, delay = signature ? k * .035f : 0;
            if (!signature)
            {
                var spark = Attach(root, "Sentry tracer core", core, .26f, ArcherySkyHot, 125, true, new Vector2(.1f, 0));
                var glowLayer = Attach(root, "Sentry tracer glow", glow, .6f, Alpha(sky, .7f), 122);
                var tail = Attach(root, "Sentry tracer streak", streak, 1.5f, Alpha(sky, .8f), 121, true, new Vector2(-.65f, 0)); tail.scale.y *= .26f;
                Emitter(glowLayer, Emit.Trace, 10, .02f, Alpha(sky, .4f)); Bundle(root, spark, glowLayer, tail); return;
            }
            // each tracer is a small sky-blue arrow of light: outline, body and white core, with a growing wake
            float bolt = ArcheryLevel(1.1f, 1.2f);
            var primary = Attach(root, "Sentry bolt core", ArcheryArrowSet[2], bolt, Alpha(ArcherySkyHot, .95f), 125, true, new Vector2(0, lateral));
            var halo = Attach(root, "Sentry bolt glow", glow, .8f, Alpha(sky, .7f), 122, true, new Vector2(.4f, lateral));
            var layers = new List<Clip>
            {
                halo,
                Attach(root, "Sentry bolt body", ArcheryArrowSet[1], bolt, ArcherySkyBody, 119, false, new Vector2(0, lateral)),
                Attach(root, "Sentry bolt outline", ArcheryArrowSet[0], bolt, ArcherySkyInk, 118, false, new Vector2(0, lateral)),
            };
            var trails = new List<ArcheryTrailLayer>();
            ArcheryTrail(root, "Sentry bolt wake", streak, 2.4f, .28f, Alpha(sky, .85f), 121, true, -.3f, trails);
            ArcheryTrail(root, "Sentry bolt filament", streak, 1.6f, .09f, new Color(1, 1, 1, .9f), 124, true, -.25f, trails);
            ArcheryTrail(root, "Sentry bolt wake tone", streak, 2.2f, .22f, Alpha(ArcherySkyBody, .75f), 117, false, -.3f, trails);
            foreach (var t in trails) { t.clip.offset.y = lateral; layers.Add(t.clip); }
            Vector2 launch = at; ArcheryGrowTrails(trails, 0);
            foreach (var l in layers) l.age = -delay;
            primary.age = -delay;
            int beat = 0;
            Emitter(halo, 60, .03f, (owner, p) =>
            {
                if (!root) return;
                ArcheryGrowTrails(trails, ((Vector2)root.position - launch).magnitude);
                if (++beat % 4 != 0) return;
                var trace = Spawn("Tracer trace", glow, p, Range(.22f, .34f), Alpha(sky, .45f), Range(.16f, .22f), true, 120); trace.endScale = .2f; trace.ease = 1; trace.fadePower = 1.2f;
            });
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
            // one muzzle flash per burst, at the sentinel's eye when it can be found
            Vector2 m = (rig != null ? (Vector2)rig.root.position + rig.eyeAt : at) + dir * .3f;
            if (ArcheryRecent(ArcherySentryMuzzle, .07f, m, .5f)) return;
            ArcherySentryMuzzle = Spawn("Sentry muzzle flash", glow, m, 1.4f, Alpha(ArcherySkyHot, .9f), .1f, true, 131); ArcherySentryMuzzle.endScale = 1.4f; ArcherySentryMuzzle.ease = 1; ArcherySentryMuzzle.fadePower = 1.5f;
            Glint(m, 1.8f, Alpha(ArcherySkyHot, 1), .16f, a);
            ArcheryBoom(m + dir * .2f, dir, 1.6f, Alpha(sky, .85f), .22f, 6);
            ArcheryBoom(m + dir * .2f, dir, 1.7f, Alpha(ArcherySkyBody, .6f), .2f, 6, false, 117);
            ArcheryFlareLines(m, 1.8f, Alpha(ArcherySkyBody, .6f), .12f, 117);
            for (int i = 0; i < 4; i++) Spark(m, Direction(a + Range(-25, 25)) * Range(5, 10), i % 2 == 0 ? Color.white : sky, Range(.2f, .32f), Range(.12f, .2f), 2, 3);
        }

        private void HitSentry(Vector2 pos, Color tint, bool signature, Vector2 direction)
        {
            Color sky = Color.Lerp(ArcherySky, new Color(tint.r, tint.g, tint.b, 1), .12f); float a = ArcheryAngle(direction);
            bool lead = !ArcheryRecent(ArcherySentryHit, .06f, pos, .6f);
            if (!lead || !signature)
            {
                // a follow-up tracer of the same burst: a crisp tick
                var tick = Spawn("Tracer tick", glow, pos + Inside(.2f), .7f, Alpha(ArcherySkyHot, .8f), .08f, true, 132); tick.endScale = 1.3f; tick.ease = 1;
                var ringTick = Spawn("Tracer tick ring", ring, pos, .8f, Alpha(sky, .75f), .14f, true, 131); ringTick.startScale = .2f; ringTick.endScale = 1; ringTick.ease = 3;
                for (int i = 0; i < 3; i++) Spark(pos, Direction(a + Range(-50, 50)) * Range(3, 7), i == 0 ? Color.white : sky, Range(.18f, .28f), Range(.12f, .2f), 5, 3);
                return;
            }
            ArcherySentryHit = Spawn("Sentry hit flash", glow, pos, 1.3f, Alpha(ArcherySkyHot, .95f), .1f, true, 132); ArcherySentryHit.endScale = 1.5f; ArcherySentryHit.ease = 1; ArcherySentryHit.fadePower = 1.5f;
            var bloom = Spawn("Sentry hit bloom", glow, pos, 2.4f, Alpha(sky, .8f), .24f, true, 124); bloom.endScale = 1.3f; bloom.fadePower = 1.4f;
            ImpactLines(pos, 2.8f, Alpha(sky, .9f), .2f);
            ArcheryFlareLines(pos, 3f, Alpha(ArcherySkyBody, .6f), .2f);
            Glint(pos, 2f, Alpha(ArcherySkyHot, 1), .2f, a);
            var shield = Spawn("Sentry hit lattice", hex, pos, 1.6f, Alpha(sky, .8f), .26f, true, 129); shield.startScale = .5f; shield.endScale = 1.2f; shield.ease = 3; shield.fadePower = 1.4f;
            Wave(pos, 2.2f, Alpha(sky, .9f), .24f, 3);
            ArcheryFlareRing(pos, 2.3f, Alpha(ArcherySkyBody, .45f), .24f);
            for (int i = 0; i < 3; i++) Spark(pos, Direction(a + Range(-45, 45)) * Range(4, 8), i % 2 == 0 ? Color.white : sky, Range(.22f, .34f), Range(.15f, .26f), 5, 3);
        }

        private void ImpactSentry(Vector2 pos, float radius, Color tint, int facing)
        {
            Color sky = Color.Lerp(ArcherySky, new Color(tint.r, tint.g, tint.b, 1), .12f);
            float r = Mathf.Clamp(radius, .7f, 3.5f), s = .6f + r * .3f;
            Vector2 ground = new Vector2(pos.x, pos.y - .8f);
            // an orbital strike: a beam drops from the sky and a column of light bursts up
            var beam = Pillar(ground + Vector2.up * 9, 1.1f * s, 9, Alpha(ArcherySkyHot, .95f), .16f, 130); beam.angle = 180;
            var column = Pillar(ground, 2.2f * s, 5.5f * s, Alpha(sky, .85f), .42f, 128); column.age = -.06f;
            var columnTone = ArcheryOpaque(Pillar(ground, 2.6f * s, 5f * s, Alpha(ArcherySkyBody, .4f), .42f, 117)); columnTone.age = -.06f;
            var flash = Spawn("Orbital flash", glow, pos, 2.2f * s, Alpha(ArcherySkyHot, .95f), .14f, true, 132); flash.age = -.06f; flash.endScale = 1.5f; flash.ease = 1; flash.fadePower = 1.5f;
            var bloom = Spawn("Orbital bloom", glow, pos, 3.6f * s, Alpha(sky, .5f), .3f, true, 124); bloom.age = -.06f; bloom.endScale = 1.3f; bloom.fadePower = 1.4f;
            ImpactLines(pos, 4.2f * s, Alpha(sky, .85f), .26f).age = -.06f;
            ArcheryFlareLines(pos, 4.6f * s, Alpha(ArcherySkyBody, .65f), .24f, 117).age = -.06f;
            Glint(pos, 3f * s, Alpha(ArcherySkyHot, 1), .26f).age = -.06f;
            var shield = Spawn("Orbital lattice", hex, pos, 2.6f * s, Alpha(sky, .65f), .35f, true, 127); shield.age = -.06f; shield.startScale = .4f; shield.endScale = 1.2f; shield.ease = 3; shield.fadePower = 1.4f;
            GroundWave(ground + Vector2.up * .05f, r * 3.2f, Alpha(sky, .9f), .42f).age = -.06f;
            var circleTone = ArcheryFlat(Spawn("Orbital circle tone", runeRing, ground, r * 2.5f, Alpha(ArcherySkyBody, .55f), 1f, false, 25), .3f);
            var circle = ArcheryFlat(Spawn("Orbital circle", runeRing, ground, r * 2.4f, Alpha(sky, .9f), 1f, true, 27), .3f);
            foreach (var x in new[] { circleTone, circle }) { x.fadeIn = .04f; x.startScale = .5f; x.endScale = 1; x.ease = 4; x.hold = .5f; x.fadePower = 1.4f; }
            for (int i = 0, n = Budget(12); i < n; i++)
                Spark(pos, Direction(Range(0, 360)) * Range(4, 9) * s, i % 3 == 0 ? Color.white : sky, Range(.25f, .42f), Range(.22f, .38f), 6, 2.2f).age = -.06f;
            for (int i = 0, n = Budget(8); i < n; i++)
            {
                var bit = Spawn("Orbital data mote", core, ground + new Vector2(Range(-r, r), .05f), Range(.05f, .11f), Alpha(i % 3 == 0 ? ArcherySkyHot : sky, .95f), Range(.5f, .8f), true, 128);
                bit.velocity = new Vector2(0, Range(1.5f, 3.5f)); bit.flicker = .5f; bit.endScale = .3f; bit.ease = 1; bit.age = -Range(.05f, .3f);
            }
        }
    }
}
