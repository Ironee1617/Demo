using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Blade family: Shockwave (a golden, earth-splitting slam and a towering sword wave), BladeStorm (a whirlwind of
    // orbiting swords with crossing slashes) and DashStrike (a blazing dash that ends in an iaido flash cut).
    // Every bright additive layer rides on a saturated alpha "ink" twin, so the silhouettes hold on pale themes too.
    public sealed partial class TowerVfx
    {
        private static Sprite BladeSlashSprite, BladeCutSprite, BladeWhirlSprite, BladeSwordSprite, BladeRingSprite, BladeChunkSprite, BladeShadeSprite;

        private static readonly Color BladeAmberInk = new Color(.66f, .32f, .04f, .78f), BladeBronzeInk = new Color(.42f, .22f, .05f, .72f),
            BladeNavyInk = new Color(.03f, .13f, .42f, .78f), BladeHotGold = new Color(1, .82f, .38f), BladeCyan = new Color(.42f, .9f, 1),
            BladeRockTone = new Color(.46f, .37f, .29f, 1), BladeStoneTone = new Color(.82f, .7f, .55f, 1), BladeDust = new Color(.62f, .52f, .4f, .6f),
            BladeAwakeGold = new Color(1, .9f, .6f);
        // A slam's wind-up, noted where and which way the hero swung: gameplay draws the slam's burst without a facing, and the
        // sword wave only runs forward, so the burst's rock, fronts and reach ring run forward too. first is the slam an
        // awakened second wave (a quarter second later, from the same caster) echoes.
        private sealed class BladeSlamNote { public Clip clock; public int facing; public Clip first; }
        private readonly List<BladeSlamNote> BladeSlamNotes = new List<BladeSlamNote>();
        // The last sword wave striking a body (so a wave consumed by a hit does not also break apart at the end of its flight).
        private Clip BladeWaveHit;

        private static void BuildBladeTextures()
        {
            // A long razor cut that bows upward: a hard bright edge on top and a soft motion smear below it.
            BladeSlashSprite = Build("Blade slash", 512, 128, (u, v) =>
            {
                float taper = Mathf.Max(0, 1 - u * u), center = .22f - .5f * u * u;
                float half = .3f * Mathf.Pow(taper, .8f) + .015f, edge = half * .35f, d = v - center;
                float body = Mathf.Pow(Mathf.Clamp01((d + half) / (edge + half)), 1.6f) * (1 - Smooth(edge - .03f, edge + .02f, d));
                float razor = Gauss(d - edge + .015f, .045f) * Mathf.Pow(taper, .5f);
                float halo = Gauss(d, half * 1.5f + .05f) * .2f * taper;
                float streaks = .72f + .28f * Mathf.PerlinNoise(u * 16 + 3, d * 4 + 7);
                float a = (body * streaks * Mathf.Pow(taper, .6f) + razor + halo) * (1 - Smooth(.86f, .99f, Mathf.Abs(u))) * (1 - Smooth(.8f, .99f, Mathf.Abs(v)));
                float l = Mathf.Lerp(.84f, 1, Mathf.Clamp01(razor * 1.4f));
                return new Color(l, l, l, Mathf.Clamp01(a));
            });
            // A thin needle cut, thickest toward its head (+x), with a soft glow around a hard core.
            BladeCutSprite = Build("Blade cut", 256, 32, (u, v) =>
            {
                float t = Mathf.Pow((u + 1) * .5f, 1.4f), profile = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 1.2f);
                float a = Gauss(v, .1f + .5f * profile) * profile * .75f + Gauss(v, .09f + .1f * profile) * profile;
                return White(Mathf.Clamp01(a) * (1 - Smooth(.92f, 1, Mathf.Abs(u))) * (1 - Smooth(.8f, 1, Mathf.Abs(v))));
            });
            // A sweeping ring arc: bright head at 0 degrees, a razor rim and a wind-streaked tail trailing clockwise.
            BladeWhirlSprite = Build("Blade whirl", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float rad = Mathf.Atan2(v, u), behind = Mathf.Repeat(-rad * Mathf.Rad2Deg + 12, 360) - 12;
                float front = Smooth(-10, 0, behind), cut = 1 - Smooth(160, 240, behind);
                float trail = Mathf.Exp(-Mathf.Max(0, behind) / 70f) * cut;
                float w = .03f + .05f * trail, d = r - .8f;
                float body = Gauss(d, w) * front * trail;
                float razor = Gauss(d - w * .75f, .013f) * front * Mathf.Exp(-Mathf.Max(0, behind) / 130f) * cut;
                float wind = Gauss(d + .11f, .08f) * front * trail * .5f * (.4f + Fbm(Mathf.Cos(rad) * 2.5f + 3, Mathf.Sin(rad) * 2.5f + r * 7, 2));
                float l = Mathf.Lerp(.8f, 1, Mathf.Clamp01(razor + body * .3f));
                return new Color(l, l, l, Mathf.Clamp01(body + razor + wind) * Window(r));
            });
            // A spectral sword pointing along +x (light-coded: bright blade, darker grip).
            BladeSwordSprite = Build("Blade spectral sword", 256, 64, (u, v) => BladeSwordShade(u, v * .25f));
            // Three swords chasing each other round a circle, each with a short motion arc: spin it and the blades orbit.
            BladeRingSprite = Build("Blade ring", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float deg = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
                Color c = Color.clear;
                for (int k = 0; k < 3; k++)
                {
                    float a0 = k * 120 * Mathf.Deg2Rad, ca = Mathf.Cos(a0), sa = Mathf.Sin(a0);
                    float px = u - ca * .7f, py = v - sa * .7f;
                    float along = -px * sa + py * ca, across = px * ca + py * sa;
                    c = Over(c, BladeSwordShade(along / .42f, across / .8f));
                    float behind = Mathf.Repeat(k * 120 - 26 - deg, 360);
                    float trail = Gauss(r - .7f, .022f + behind * .0006f) * Mathf.Exp(-behind / 28f) * (1 - Smooth(90, 120, behind)) * .7f;
                    c = Over(c, new Color(1, 1, 1, trail));
                }
                c.a *= Window(r); return c;
            });
            // A broad soft disc (flat centre, long feathered edge) for shadows laid behind the actors.
            BladeShadeSprite = Build("Blade shade", 128, 128, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                return White((1 - Smooth(.35f, .98f, r)) * (.88f + .12f * Fbm(u * 3 + 5, v * 3 + 1, 2)));
            });
            // A pale, faceted stone chunk (light-coded so a tint gives sandstone or basalt with lit facets).
            BladeChunkSprite = Build("Blade rock chunk", 64, 64, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), th = Mathf.Atan2(v, u);
                float rr = .72f + .14f * Mathf.Cos(th * 3 + 1.3f) + .08f * Mathf.Cos(th * 5 + .4f);
                float inside = 1 - Smooth(rr - .07f, rr, r);
                float facet = Mathf.Repeat(th / (Mathf.PI * 2) * 5 + .1f, 1) < .5f ? 1 : .8f;
                float light = Mathf.Clamp01(.66f + .34f * (-u * .5f + v * .7f) / Mathf.Max(.3f, rr)) * facet;
                float l = Mathf.Clamp01(light + Gauss(r - rr + .1f, .05f) * .18f);
                return new Color(l, l * .97f, l * .93f, inside);
            });
        }

        private static Color BladeSwordShade(float x, float y)
        {
            float ay = Mathf.Abs(y);
            float half = x < .55f ? .075f : .075f * Mathf.Clamp01((.96f - x) / .41f);
            float blade = Smooth(-.45f, -.42f, x) * (1 - Smooth(.94f, .97f, x)) * (1 - Smooth(half - .012f, half + .004f, ay));
            float fuller = Gauss(y, .018f) * Smooth(-.42f, -.3f, x) * (1 - Smooth(.45f, .75f, x));
            float rim = Gauss(ay - half + .012f, .012f) * blade;
            float guard = Smooth(-.535f, -.52f, x) * (1 - Smooth(-.44f, -.425f, x)) * (1 - Smooth(.17f, .19f, ay));
            float grip = Smooth(-.85f, -.83f, x) * (1 - Smooth(-.53f, -.51f, x)) * (1 - Smooth(.03f, .04f, ay));
            float pommel = 1 - Smooth(.04f, .055f, Mathf.Sqrt(Sq(x + .88f) + y * y));
            float halo = Gauss(ay, .09f) * Smooth(-.5f, -.3f, x) * (1 - Smooth(.8f, 1, x)) * .35f;
            float a = Mathf.Max(Mathf.Max(blade, guard), Mathf.Max(grip, pommel));
            float l = blade > .5f ? Mathf.Clamp01(.86f + fuller * .3f + rim * .3f) : guard > .5f ? .82f : pommel > .5f ? .85f : .6f;
            return new Color(l, l, l, Mathf.Clamp01(a + halo * (1 - a)));
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

        /// <summary>True while <paramref name="clip"/> is alive, younger than <paramref name="window"/> and near <paramref name="pos"/>.</summary>
        private bool BladeRecent(Clip clip, float window, Vector2 pos, float distance)
        {
            return clip != null && clip.age < window && (clip.position - pos).sqrMagnitude < distance * distance && clips.Contains(clip);
        }

        /// <summary>Notes a slam wound up at <paramref name="pos"/> facing <paramref name="facing"/> for <paramref name="life"/> seconds.</summary>
        private void BladeNoteSlam(Vector2 pos, int facing, float life)
        {
            BladeSlamNotes.RemoveAll(n => !clips.Contains(n.clock));
            var clock = Spawn("Blade slam note", glow, pos, .05f, new Color(0, 0, 0, 0), life, true, 20);
            BladeSlamNotes.Add(new BladeSlamNote { clock = clock, facing = facing < 0 ? -1 : 1 });
        }

        /// <summary>The newest live slam note within 2.5 units of <paramref name="pos"/>, or null (a passive or a boss slam).</summary>
        private BladeSlamNote BladeFindSlam(Vector2 pos)
        {
            BladeSlamNote best = null;
            foreach (var note in BladeSlamNotes)
                if (clips.Contains(note.clock) && (note.clock.position - pos).sqrMagnitude < 6.25f && (best == null || note.clock.age < best.clock.age)) best = note;
            return best;
        }

        private static void BladeDelay(Clip clip, float delay)
        {
            if (delay <= 0) return;
            clip.age = -delay; if (clip.renderer) clip.renderer.enabled = false;
        }

        /// <summary>Draws a clip alpha-blended (a saturated or dark "ink" layer under the light).</summary>
        private static Clip BladeInk(Clip clip) { if (clip.renderer) clip.renderer.sharedMaterial = AlphaBlend; return clip; }

        /// <summary>Snaps a slash open along its length in a couple of frames.</summary>
        private static void BladeSnap(Clip clip, float delay)
        {
            clip.axisStart = new Vector2(.25f, 1); clip.axisEnd = Vector2.one; clip.ease = 9; clip.startScale = .96f; clip.endScale = 1.05f; clip.fadePower = 1.5f;
            BladeDelay(clip, delay);
        }

        private static void BladeZoneFade(Clip clip, float life) { clip.life = life; clip.fadeIn = .12f; clip.hold = .72f; clip.fadePower = 1.4f; }

        private Clip BladeCutLine(Vector2 a, Vector2 b, float thickness, Color color, float life, int order, bool additive = true)
        {
            Vector2 delta = b - a; float length = Mathf.Max(.05f, delta.magnitude);
            var clip = Spawn("Blade cut", BladeCutSprite, (a + b) * .5f, length, color, life, additive, order);
            clip.scale = new Vector3(length / BladeCutSprite.bounds.size.x, thickness / BladeCutSprite.bounds.size.y, 1);
            clip.angle = delta.sqrMagnitude > 1e-6f ? Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg : 0; clip.ease = 1;
            clip.root.transform.localScale = clip.scale; clip.root.transform.localRotation = Quaternion.Euler(0, 0, clip.angle);
            return clip;
        }

        private Clip BladeSlashAt(Vector2 pos, float width, float height, float angle, Color color, float life, bool additive, int order)
        {
            var clip = Spawn("Blade slash", BladeSlashSprite, pos, width, color, life, additive, order);
            clip.scale = new Vector3(width / BladeSlashSprite.bounds.size.x, height / BladeSlashSprite.bounds.size.y, 1);
            clip.angle = angle; clip.root.transform.localScale = clip.scale; clip.root.transform.localRotation = Quaternion.Euler(0, 0, angle);
            return clip;
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
                float angle = i * 360f / count + Range(-10, 10);
                var ink = Attach(plane, "Storm sweep shadow", BladeWhirlSprite, width * 1.05f, BladeBronzeInk, order - 7, false);
                var sweep = Attach(plane, "Storm sweep", BladeWhirlSprite, width, Alpha(light, alpha), order);
                ink.angle = angle - 12 * Mathf.Sign(spin); sweep.angle = angle;
                foreach (var clip in new[] { ink, sweep })
                {
                    if (spin < 0) clip.scale.x *= -1;
                    clip.spin = spin; BladeZoneFade(clip, life);
                    layers.Add(clip);
                }
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

        /// <summary>A dark halo drawn behind the actors (above the backdrop) so bodies and the light over them stand out.</summary>
        private Clip BladeBackShade(Vector2 pos, float width, Color color, float life, float squash = 1)
        {
            var clip = Spawn("Blade back shade", BladeShadeSprite, pos, width, color, life, false, 28);
            clip.scale.y *= squash; clip.root.transform.localScale = clip.scale; clip.fadeIn = .04f; clip.fadePower = 1.3f;
            return clip;
        }

        /// <summary>Stone chunks flung from a blast: pale faceted rock tinted to <paramref name="tone"/>, tumbling under gravity.</summary>
        private void BladeRubble(Vector2 pos, int count, float speed, float size, float life, Color tone)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                var chunk = Spawn("Blade rubble", BladeChunkSprite, pos + Inside(.3f), size * Range(.5f, 1.15f), tone, life * Range(.7f, 1.1f), false, 128);
                chunk.velocity = new Vector2(Range(-1, 1) * speed, Range(.55f, 1.2f) * speed); chunk.gravity = 18; chunk.spin = Range(-500, 500); chunk.hold = .55f; chunk.fadePower = .8f;
            }
        }

        /// <summary>Light streaks rushing in toward a point (the pull of a charge), spread over a wind-up window.</summary>
        private void BladeInward(Vector2 target, int count, float reach, Color color, float window, float thickness)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                float speed = Range(8, 12), dist = reach * Range(.75f, 1.1f), life = Mathf.Max(.08f, (dist - .5f) / speed);
                Vector2 dir = Direction(Range(0, 360)), from = target + dir * dist;
                var ray = BladeCutLine(from + dir * Range(.8f, 1.3f), from, thickness * Range(.7f, 1.2f), i % 3 == 0 ? Color.white : Alpha(color, .95f), life, 129);
                ray.velocity = -dir * speed; ray.fadeIn = .03f; ray.hold = .6f; ray.fadePower = 1.5f;
                BladeDelay(ray, Range(0, Mathf.Max(0, window - life)));
            }
        }

        /// <summary>A light column standing on <paramref name="ground"/> that rides on the caster (or stays put without one).</summary>
        private Clip BladeColumn(Transform anchor, Vector2 ground, float width, float height, Color color, float life, int order, bool additive = true)
        {
            var clip = Anchored(anchor, ground, "Blade light column", pillar, width, color, life, order, additive);
            float lossyY = anchor ? Mathf.Max(.0001f, Mathf.Abs(anchor.lossyScale.y)) : 1;
            clip.scale = new Vector3(clip.scale.x, height / pillar.bounds.size.y / lossyY, 1);
            clip.root.transform.localScale = clip.scale;
            return clip;
        }

        /// <summary>A short crackle of jagged light.</summary>
        private void BladeZap(Vector2 at, float size, Color color, float delay)
        {
            Vector2 p = at; float heading = Range(0, 360);
            for (int i = 0; i < 3; i++)
            {
                Vector2 next = p + Direction(heading + Range(-70, 70)) * size * Range(.25f, .45f);
                var arc = Line(p, next, .09f, i == 1 ? Color.white : Alpha(color, 1), .1f, 132, 1.15f); arc.flicker = .35f; arc.fadePower = 1.2f;
                BladeDelay(arc, delay); p = next;
            }
        }

        // ---------------------------------------------------------------- Shockwave: golden slam and sword wave

        private void CastShockwave(CastInfo c)
        {
            int f = c.facing; float w = c.windup, release = Mathf.Max(0, w - .03f);
            Vector2 pos = c.pos, feet = new Vector2(pos.x, BladeGroundY(pos, 1)), gather = pos + new Vector2(f * .35f, .9f);
            Color light = Color.Lerp(BladeHotGold, c.light, .3f);
            // The slam's burst (and an awakened second wave a quarter second after it) reads this to run forward only.
            BladeNoteSlam(pos, f, w + .9f);
            if (w < .2f)
            {
                // An instant echo (a recast with no wind-up): only the swing coming down; the slam follows at once.
                var quickInk = BladeSlashAt(pos + new Vector2(f * .85f, .25f), 3.8f, 1.9f, -90 * f, BladeAmberInk, .22f, false, 118); BladeSnap(quickInk, release);
                var quick = BladeSlashAt(pos + new Vector2(f * .9f, .25f), 3.6f, 1.7f, -90 * f, new Color(1, .95f, .8f, .9f), .18f, true, 130); BladeSnap(quick, release);
                var quickStar = Glint(gather, 2.4f, Color.white, .18f); BladeDelay(quickStar, release);
                return;
            }
            // The raised weapon drinks in light: an amber shadow, a golden seal turning behind it, a swelling heart and a star.
            var shade = Anchored(c.anchor, gather, "Wave charge shadow", glow, 3.2f, new Color(.42f, .2f, .02f, .55f), w + .08f, 110, false);
            shade.fadeIn = .05f; shade.startScale = .45f; shade.endScale = 1.1f; shade.ease = 1; shade.hold = .8f; shade.fadePower = 2;
            var sealInk = Anchored(c.anchor, gather, "Wave seal shadow", runeRing, 3.4f, BladeAmberInk, w + .06f, 111, false);
            var seal = Anchored(c.anchor, gather, "Wave seal", runeRing, 3.3f, Alpha(light, .95f), w + .06f, 125);
            foreach (var clip in new[] { sealInk, seal }) { clip.fadeIn = .06f; clip.startScale = .3f; clip.endScale = 1; clip.ease = 2.5f; clip.spin = 150 * f; clip.hold = .88f; clip.fadePower = 2.2f; }
            var heart = Anchored(c.anchor, gather, "Wave charge", glow, 2.6f, Alpha(light, .95f), w + .06f, 126);
            heart.fadeIn = .04f; heart.startScale = .25f; heart.endScale = 1.4f; heart.ease = 1; heart.pulse = .08f; heart.hold = .9f; heart.fadePower = 2.5f;
            var hot = Anchored(c.anchor, gather, "Wave charge core", core, .6f, Warm, w + .04f, 127);
            hot.startScale = .3f; hot.endScale = 1.5f; hot.ease = 1; hot.pulse = .15f; hot.hold = .9f; hot.fadePower = 2;
            var star = Anchored(c.anchor, gather, "Wave charge star", glint, 3f, Alpha(Warm, .95f), w + .04f, 128);
            star.startScale = .12f; star.endScale = 1; star.ease = 1.2f; star.spin = 120; star.hold = .88f; star.fadePower = 2;
            // A golden column rises behind the hero (behind the actors, so the body stands out against it) and swells to the release.
            var columnInk = BladeColumn(c.anchor, feet, 2.4f, 4.6f, new Color(.4f, .19f, .02f, .5f), w + .05f, 28, false);
            var column = BladeColumn(c.anchor, feet, 1.9f, 5.2f, Alpha(light, .85f), w + .05f, 29);
            foreach (var clip in new[] { columnInk, column }) { clip.axisStart = new Vector2(.35f, .2f); clip.axisEnd = Vector2.one; clip.ease = 1.3f; clip.fadeIn = .05f; clip.hold = .9f; clip.fadePower = 3; }
            column.pulse = .05f;
            // Right before the slam the gathered light collapses inward: a ring snapping shut on the weapon.
            var implode = Spawn("Wave implode", ring, gather, 4.2f, Alpha(Warm, .95f), .15f, true, 129);
            implode.startScale = 1; implode.endScale = .12f; implode.ease = 1.6f; implode.fadeIn = .03f; implode.fadePower = .5f; BladeDelay(implode, Mathf.Max(0, w - .16f));
            var implodeInk = BladeInk(Spawn("Wave implode shadow", ring, gather, 4.4f, BladeAmberInk, .15f, false, 112));
            implodeInk.startScale = 1; implodeInk.endScale = .12f; implodeInk.ease = 1.6f; implodeInk.fadeIn = .03f; implodeInk.fadePower = .5f; BladeDelay(implodeInk, Mathf.Max(0, w - .16f));
            Converge(gather, 18, 2.8f, light, w, .45f);
            BladeInward(gather, 8, 3.2f, light, w, .16f);
            // The ground answers: an amber-shadowed rune ring, a golden glow, sparks rising, pebbles lifting and dust drawn in.
            Spawn("Wave charge ground shadow", glow, feet + Vector2.up * .05f, 5f, new Color(.36f, .17f, .03f, .45f), w + .2f, false, 24).scale.y *= .28f;
            BladeBackShade(pos + Vector2.up * .4f, 4.2f, new Color(.3f, .14f, .02f, .4f), w + .12f);
            var floor = BladeFlatPlane(c.anchor, feet + Vector2.up * .06f, .27f, w + .35f);
            var runeInk = BladeOnPlane(floor, "Wave rune shadow", runeRing, 4.7f, Alpha(BladeAmberInk, .6f), w + .35f, 25, false, 90 * f);
            var rune = BladeOnPlane(floor, "Wave rune", runeRing, 4.5f, Alpha(light, .95f), w + .35f, 27, true, 90 * f);
            foreach (var clip in new[] { runeInk, rune }) { clip.startScale = .55f; clip.endScale = 1; clip.ease = 4; clip.fadeIn = .08f; }
            GroundGlow(feet + Vector2.up * .05f, 4.8f, Alpha(light, .6f), w + .2f);
            int rising = BladeShare(10);
            for (int i = 0; i < rising; i++)
            {
                var mote = Spark(feet + new Vector2(Range(-2.1f, 2.1f), .1f), new Vector2(Range(-.2f, .2f), Range(2.5f, 5)), i % 3 == 0 ? Color.white : light, Range(.22f, .36f), Range(.25f, .4f), -1, 1);
                mote.flicker = .3f; BladeDelay(mote, Range(0, w * .8f));
            }
            int pebbles = BladeShare(8);
            for (int i = 0; i < pebbles; i++)
            {
                float side = i % 2 == 0 ? 1 : -1, delay = Range(0, w * .35f);
                var stone = Spawn("Rising pebble", BladeChunkSprite, feet + new Vector2(side * Range(.5f, 2.3f), .08f), Range(.16f, .32f), BladeStoneTone, Mathf.Max(.1f, w + .1f - delay), false, 119);
                stone.velocity = new Vector2(-side * Range(.1f, .4f), Range(1, 2.4f)); stone.drag = 2.2f; stone.spin = Range(-160, 160); stone.fadeIn = .05f; stone.hold = .85f; stone.fadePower = 2;
                BladeDelay(stone, delay);
            }
            int dusts = BladeShare(8);
            for (int i = 0; i < dusts; i++)
            {
                float side = i % 2 == 0 ? 1 : -1, x = Range(1.4f, 2.6f);
                var puff = Puff(feet + new Vector2(side * x, .18f), Range(.6f, .9f), BladeDust, new Vector2(-side * x * 2.2f, Range(.1f, .5f)), w * Range(.75f, 1.05f), 118);
                puff.drag = 2.5f; puff.startScale = .8f; puff.endScale = 1.3f; puff.fadeIn = .08f;
            }
            // Release: the weapon comes down in a bright arc and the gathered star bursts.
            var swingInk = BladeSlashAt(pos + new Vector2(f * .85f, .25f), 3.8f, 1.9f, -90 * f, BladeAmberInk, .22f, false, 118); BladeSnap(swingInk, release);
            var swing = BladeSlashAt(pos + new Vector2(f * .9f, .25f), 3.6f, 1.7f, -90 * f, new Color(1, .95f, .8f, .9f), .18f, true, 130); BladeSnap(swing, release);
            var burst = Glint(gather, 3.2f, Color.white, .2f); BladeDelay(burst, release);

            // Level two: a white-gold spire climbs the column and a counter-turning inner seal tightens round the weapon.
            int grade = Grade;
            if (grade < 2 || w < .2f) return;
            var spire = BladeColumn(c.anchor, feet, .85f, grade >= 3 ? 7.6f : 6.6f, Alpha(Warm, .8f), w + .05f, 29);
            spire.axisStart = new Vector2(.3f, .15f); spire.axisEnd = Vector2.one; spire.ease = 1.3f; spire.fadeIn = .05f; spire.hold = .9f; spire.fadePower = 3;
            var innerInk = Anchored(c.anchor, gather, "Wave inner seal shadow", runeRing, 2.3f, BladeAmberInk, w + .06f, 112, false);
            var inner = Anchored(c.anchor, gather, "Wave inner seal", runeRing, 2.2f, Alpha(Warm, .95f), w + .06f, 127);
            foreach (var clip in new[] { innerInk, inner }) { clip.fadeIn = .08f; clip.startScale = .3f; clip.endScale = 1; clip.ease = 2.5f; clip.spin = -240 * f; clip.hold = .88f; clip.fadePower = 2.2f; }
            if (grade < 3) return;
            // Level three, awakened: a spectral greatsword gathers point-down above and behind the hero (behind the actors);
            // at the release it plunges into the floor (the slam draws it planted).
            Vector2 hilt = new Vector2(pos.x - f * .45f, feet.y + 4.2f);
            for (int layer = 0; layer < 3; layer++)
            {
                bool ink = layer == 0;
                var blade = Anchored(c.anchor, hilt, ink ? "Awakened greatsword shadow" : layer == 1 ? "Awakened greatsword" : "Awakened greatsword edge", BladeSwordSprite,
                    ink ? 4.1f : 3.9f, ink ? BladeAmberInk : layer == 1 ? Alpha(BladeHotGold, .95f) : Alpha(Warm, .85f), w + .03f, 26 + layer, !ink);
                blade.angle = -90; blade.scale.y *= ink ? 2.7f : layer == 1 ? 2.2f : 1.1f; blade.root.transform.localScale = blade.scale;
                blade.fadeIn = w * .55f; blade.startScale = .75f; blade.endScale = 1; blade.ease = 2; blade.hold = .9f; blade.fadePower = 3;
            }
            var aura = Anchored(c.anchor, hilt, "Awakened greatsword aura", glow, 3.2f, Alpha(BladeAwakeGold, .4f), w + .03f, 25);
            aura.scale.x *= .45f; aura.fadeIn = w * .5f; aura.pulse = .08f; aura.hold = .9f; aura.fadePower = 3;
            var hiltStar = Anchored(c.anchor, hilt + Vector2.up * 1.55f, "Awakened greatsword star", glint, 1.6f, Alpha(Warm, .95f), w + .03f, 130);
            hiltStar.startScale = .2f; hiltStar.endScale = 1; hiltStar.ease = 2; hiltStar.spin = 90; hiltStar.hold = .85f; hiltStar.fadePower = 2.5f;
        }

        /// <summary>The greatsword of an awakened slam driving into the floor behind the hero and standing there a moment,
        /// over a gold seal whose outer ring sits on the slam's reach.</summary>
        private void BladePlantedSword(Vector2 g, float R)
        {
            Vector2 planted = g + new Vector2(0, 1.85f);
            for (int layer = 0; layer < 3; layer++)
            {
                bool ink = layer == 0;
                var drop = Spawn(ink ? "Plunging greatsword shadow" : "Plunging greatsword", BladeSwordSprite, planted + Vector2.up * 2.2f, ink ? 4.1f : 3.9f,
                    ink ? BladeAmberInk : layer == 1 ? Alpha(BladeHotGold, 1) : Alpha(Warm, .9f), .9f, !ink, 26 + layer);
                drop.angle = -90; drop.scale.y *= ink ? 2.7f : layer == 1 ? 2.2f : 1.1f; drop.root.transform.localScale = drop.scale;
                drop.root.transform.localRotation = Quaternion.Euler(0, 0, -90);
                // Down 2.2 units in 0.06 s, then it stands while the dust settles.
                drop.velocity = Vector2.down * 2.2f * 22; drop.drag = 22; drop.hold = .45f; drop.fadePower = 1.6f;
            }
            var hit = Flash(g + Vector2.up * .2f, 2.2f, Alpha(Warm, .95f), .14f); hit.ramp = Ramp(Color.white, BladeAwakeGold);
            var standGlow = Spawn("Planted greatsword glow", glow, planted, 3.4f, Alpha(BladeAwakeGold, .4f), .9f, true, 25);
            standGlow.scale.x *= .5f; standGlow.root.transform.localScale = standGlow.scale; standGlow.hold = .4f; standGlow.fadePower = 1.5f; standGlow.pulse = .06f;
            // Where it bites: a glowing crack radiating from the blade's point (the level overlay lays the seal on the reach).
            Crack(g + Vector2.up * .02f, 1.8f, Alpha(BladeAwakeGold, 1), 1.1f);
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
            Underlay(hit + Vector2.up * .3f * k, R * 1.1f, .24f, .26f);
            var bloom = Flash(hit, R * .4f, Alpha(Warm, .7f), .12f); bloom.ramp = Ramp(Color.white, light);
            var blastInk = Spawn("Echo floor blast shadow", streak, g + new Vector2(f * R * .5f, .3f), R * (f != 0 ? 2f : 2.6f), new Color(.55f, .26f, .04f, .65f), .28f, false, 117);
            var blast = Spawn("Echo floor blast", streak, g + new Vector2(f * R * .5f, .32f), R * (f != 0 ? 2f : 2.6f), Alpha(Warm, 1), .22f, true, 132);
            blastInk.scale.y *= .55f; blast.scale.y *= .26f;
            foreach (var clip in new[] { blastInk, blast }) { clip.root.transform.localScale = clip.scale; clip.startScale = .3f; clip.endScale = 1.12f; clip.ease = 3.5f; clip.fadePower = 1.5f; }
            ImpactLines(hit, R * 1.8f, Alpha(light, .7f), .2f);
            // The reach of the wave: a gold ring on the floor whose rim lands on the gameplay reach.
            var reachInk = BladeInk(Wave(reachAt + Vector2.up * .04f, reach * 2 / .74f * 1.02f, new Color(.5f, .25f, .05f, .6f), .46f, 3)); reachInk.scale.y *= .3f; reachInk.renderer.sortingOrder = 115;
            GroundWave(reachAt + Vector2.up * .05f, reach * 2 / .74f, Alpha(light, .95f), .42f);
            for (int s = -1; s <= 1; s += 2)
            {
                if (f != 0 && s != f)
                {
                    // Behind the hero: only a puff of dust kicked back by the swing.
                    var kick = Puff(g + new Vector2(s * .5f, .2f), Range(.6f, .85f), BladeDust, new Vector2(s * Range(.6f, 1.2f), Range(.6f, 1.2f)), Range(.45f, .6f), 118);
                    kick.endScale = 2; continue;
                }
                // A seam of light races out along the floor to the reach and dust is thrown up behind it.
                var seamInk = BladeCutLine(g + new Vector2(s * .3f, .04f), g + new Vector2(s * R * .95f, .04f), .34f, new Color(.4f, .2f, .03f, .6f), .7f, 24, false);
                var seam = BladeCutLine(g + new Vector2(s * .3f, .05f), g + new Vector2(s * R * .95f, .05f), .16f, Alpha(light, 1), .6f, 26);
                foreach (var clip in new[] { seamInk, seam }) { clip.axisStart = new Vector2(.08f, 1); clip.axisEnd = Vector2.one; clip.ease = 5; clip.hold = .3f; clip.fadePower = 1.5f; }
                int dusts = Budget(2);
                for (int i = 0; i < dusts; i++)
                {
                    float t = (i + 1f) / (dusts + 1);
                    var dust = Puff(g + new Vector2(s * R * t, .2f), Range(.6f, .9f), BladeDust, new Vector2(s * Range(.5f, 1.5f), Range(.8f, 1.8f)), Range(.5f, .7f), 118);
                    dust.endScale = 2.2f; BladeDelay(dust, t * .12f);
                    var spark = Spark(g + new Vector2(s * R * t, .3f), new Vector2(s * Range(2, 5), Range(2, 5)), i % 2 == 0 ? Color.white : light, Range(.22f, .34f), Range(.25f, .4f), 6, 2);
                    BladeDelay(spark, t * .12f);
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
            // Impact frame: a dark halo, a white-hot heart, bloom, a star, anime lines and a geyser of light over an amber shadow.
            BladeBackShade(hit + new Vector2(f * R * .25f, .6f * k), R * (f != 0 ? 1.9f : 2.3f), new Color(.16f, .08f, .02f, .38f), .42f, .7f);
            Underlay(hit + Vector2.up * .3f * k, R * 1.2f, .3f, .3f);
            var heart = Spawn("Slam core", core, hit, R * .3f, Color.white, .07f, true, 133); heart.startScale = 1.3f; heart.endScale = .3f; heart.ease = 1;
            var bloom = Flash(hit, R * .5f, Alpha(light, .7f), .1f); bloom.ramp = Ramp(Color.white, light);
            // The blast skims the floor ahead (both ways when the swing is unknown): a flat white-gold glare over an amber band,
            // so the slam reads as striking the ground.
            float blastWidth = f != 0 ? 2.3f : 3f;
            var blastInk = Spawn("Slam floor blast shadow", streak, g + new Vector2(f * R * .55f, .3f), R * blastWidth, new Color(.55f, .26f, .04f, .75f), .32f, false, 117);
            blastInk.scale.y *= .7f; blastInk.ramp = null;
            var blast = Spawn("Slam floor blast", streak, g + new Vector2(f * R * .55f, .32f), R * (blastWidth - .1f), Alpha(Warm, 1), .24f, true, 132);
            blast.scale.y *= .32f; blast.ramp = Ramp(Color.white, light);
            foreach (var clip in new[] { blastInk, blast }) { clip.root.transform.localScale = clip.scale; clip.startScale = .3f; clip.endScale = 1.12f; clip.ease = 3.5f; clip.fadePower = 1.5f; }
            ImpactLines(hit + Vector2.up * .2f, R * 2.8f, Alpha(Warm, .95f), .24f);
            Glint(hit, R * 1.1f, Color.white, .26f);
            Pillar(g, R * .4f * lift, (R * 1.5f + .8f) * lift, Alpha(light, .95f), .32f, 129);
            BladeInk(Pillar(g, R * .8f * lift, (R * 1.4f + .7f) * lift, BladeAmberInk, .42f, 117));
            if (grade >= 2) Pillar(g, R * .16f, (R * 1.8f + 1) * lift, Alpha(Warm, .95f), .26f, 130);
            // Shock rings on the ground (an amber one under the light so it reads on pale floors), their rims on the struck
            // stretch, and one in the air.
            float rimScale = f != 0 ? 2 / .74f : 2.15f;
            var groundInk = BladeInk(Wave(reachAt + Vector2.up * .04f, reach * rimScale * 1.04f, new Color(.5f, .25f, .05f, .75f), .5f, 3)); groundInk.scale.y *= .3f; groundInk.renderer.sortingOrder = 115;
            GroundWave(reachAt + Vector2.up * .05f, reach * rimScale, Alpha(light, 1), .45f);
            var second = GroundWave(reachAt + Vector2.up * .05f, reach * rimScale * .7f, new Color(1, 1, 1, .9f), .32f); BladeDelay(second, .06f);
            Wave(hit + Vector2.up * .3f, R * 1.3f, Alpha(Warm, .75f), .28f, 3);
            // Razor-edged shock fronts race out along the floor tearing up dust and grit, with rock spikes erupting behind them:
            // forward only for a noted slam (behind the hero just a short puff of a front and dust), both ways otherwise.
            for (int s = -1; s <= 1; s += 2)
            {
                float side = s, gy = g.y; int emitted = 0;
                if (f != 0 && s != f)
                {
                    Vector2 backAt = g + new Vector2(side * .35f * k, .6f * k);
                    var stubInk = Spawn("Slam back front shadow", crescent, backAt, 1.1f * k, Alpha(BladeAmberInk, .55f), .26f, false, 118);
                    var stub = Spawn("Slam back front", crescent, backAt, 1f * k, Alpha(light, .7f), .22f, true, 126);
                    foreach (var clip in new[] { stubInk, stub })
                    { clip.scale.x *= side; clip.scale.y *= 1.3f; clip.velocity = new Vector2(side * 3.2f, 0); clip.drag = 4; clip.startScale = .5f; clip.endScale = .9f; clip.ease = 2; clip.fadePower = 1.6f; }
                    for (int i = 0; i < 2; i++)
                    {
                        var kick = Puff(g + new Vector2(side * Range(.3f, .8f), .2f), Range(.55f, .85f) * (.6f + .4f * k), BladeDust, new Vector2(side * Range(.5f, 1.4f), Range(.5f, 1.3f)), Range(.45f, .65f), 118);
                        kick.endScale = 2;
                    }
                    continue;
                }
                Vector2 start = g + new Vector2(side * .35f * k, .8f * k);
                var frontInk = Spawn("Slam front shadow", crescent, start - new Vector2(side * .15f * k, 0), 1.55f * k, BladeAmberInk, .46f, false, 118);
                var front = Spawn("Slam front", crescent, start, 1.4f * k, Alpha(light, .95f), .42f, true, 126);
                var edge = BladeSlashAt(start + new Vector2(side * .1f * k, 0), 2.5f * k, 1.4f * k, side > 0 ? -90 : 90, new Color(1, 1, 1, .95f), .3f, true, 127);
                var fronts = new List<Clip> { frontInk, front, edge };
                // Level two and up: a taller front with a gold-white gilt crest riding just ahead of it.
                if (grade >= 2)
                {
                    var gilt = Spawn("Slam front gilt", crescent, start + new Vector2(side * .2f * k, 0), 1.25f * k, Alpha(grade >= 3 ? Warm : BladeAwakeGold, .9f), .36f, true, 128);
                    fronts.Add(gilt); gilt.scale.x *= side; gilt.scale.y *= 1.8f * lift;
                }
                foreach (var clip in new[] { frontInk, front }) { clip.scale.x *= side; clip.scale.y *= 1.8f * Mathf.Sqrt(lift); }
                foreach (var clip in fronts)
                { clip.velocity = new Vector2(side * R * 3.4f, 0); clip.drag = 2.6f; clip.startScale = .5f; clip.endScale = 1.1f; clip.ease = 2; clip.fadePower = 1.5f; }
                Emitter(front, 22 + 18 * k, .15f, (owner, p) =>
                {
                    emitted++;
                    Vector2 at = new Vector2(p.x, gy);
                    if (emitted % 3 == 0)
                    {
                        var chip = Spawn("Slam chip", BladeChunkSprite, at + Vector2.up * .1f, Range(.16f, .32f), BladeStoneTone, Range(.45f, .7f), false, 128);
                        chip.velocity = new Vector2(side * Range(-.5f, 2.5f), Range(3, 6.5f)); chip.gravity = 18; chip.spin = Range(-600, 600); chip.fadePower = .6f;
                    }
                    else
                    {
                        var dust = Puff(at + new Vector2(Range(-.2f, .2f), .2f), Range(.6f, 1.05f) * (.6f + .4f * k), BladeDust, new Vector2(side * Range(.2f, 1.2f), Range(.8f, 2.2f)), Range(.5f, .8f), 118);
                        dust.endScale = 2.3f;
                    }
                });
                int spikes = Mathf.Max(2, Mathf.RoundToInt(2 + 3 * k));
                for (int i = 0; i < spikes; i++)
                {
                    float t = (i + .6f) / spikes, x = side * R * .92f * t, delay = (Mathf.Exp(.7f * t) - 1) / 2.6f;
                    float h = (2.1f - .9f * t) * (.45f + .55f * k) * Range(.85f, 1.1f) * BladeLevel(1.15f, 1.3f), tiltAngle = -side * Range(6, 24);
                    Vector2 at = g + new Vector2(x, -.06f);
                    var stone = Spawn("Erupting rock", spike, at, h / 3, BladeRockTone, .85f, false, 119);
                    var rim = Spawn("Erupting rock light", spike, at, h / 3 * .82f, Alpha(light, .8f), .5f, true, 120);
                    foreach (var clip in new[] { stone, rim })
                    {
                        clip.scale.x *= 1.5f;
                        clip.angle = tiltAngle; clip.axisStart = new Vector2(.6f, 0); clip.axisEnd = Vector2.one; clip.ease = 7; clip.hold = .62f; clip.fadePower = 2;
                        BladeDelay(clip, delay);
                    }
                    var kick = Puff(at + Vector2.up * .2f, Range(.5f, .8f) * (.6f + .4f * k), BladeDust, new Vector2(side * Range(.3f, 1), Range(.6f, 1.4f)), Range(.45f, .65f), 118);
                    BladeDelay(kick, delay);
                    // The spike crumbles as it fades: chunks tumble off its flank and a puff of grit slumps to the floor.
                    float crumble = delay + .58f;
                    Vector2 flank = at + Direction(90 + tiltAngle) * h * .55f;
                    for (int j = 0; j < 1; j++)
                    {
                        var bit = Spawn("Crumbling rock", BladeChunkSprite, flank + Inside(.2f), h * Range(.08f, .13f), BladeStoneTone, Range(.45f, .6f), false, 128);
                        bit.velocity = new Vector2(side * Range(.3f, 1.6f), Range(.5f, 2)); bit.gravity = 16; bit.spin = Range(-400, 400); bit.hold = .5f; bit.fadePower = .8f;
                        BladeDelay(bit, crumble);
                    }
                    var slump = Puff(at + Vector2.up * h * .3f, Range(.6f, .9f) * (.6f + .4f * k), BladeDust, new Vector2(side * Range(.2f, .6f), Range(-.3f, .4f)), Range(.6f, .8f), 118);
                    slump.endScale = 2.1f; BladeDelay(slump, crumble);
                }
            }
            // The scar: glowing cracks over a dark char along the struck stretch, and an afterglow that lingers on the floor.
            Crack(reachAt + Vector2.up * .02f, reach * 2.4f, Alpha(light, 1), 1.6f);
            Crack(g + new Vector2(f * R * .2f, .03f), R * (f != 0 ? 1f : 1.25f), Alpha(Warm, 1), 1.5f);
            Decal(g + new Vector2(f * R * .3f, .02f), scorch, R * (f != 0 ? 1.2f : 1.4f), new Color(.16f, .1f, .05f, .45f), 1.4f, false, 24);
            GroundGlow(reachAt + Vector2.up * .06f, reach * 2f, Alpha(light, .65f), 1.1f);
            var seam = GroundGlow(g + new Vector2(f * R * .25f, .05f), R * (f != 0 ? .9f : 1.1f), Alpha(Warm, .8f), 1.5f); seam.hold = .4f; seam.fadePower = 1.6f;
            if (grade >= 3) { if (note != null) note.first = seam; BladePlantedSword(g, R); }
            // Follow-through: rock chunks, billowing dust, a spray of hot sparks and embers drifting up from the scar
            // (thrown forward with the wave when the swing is known).
            BladeRubble(hit + new Vector2(f * .4f, 0), Mathf.RoundToInt((6 + 5 * k) * BladeLevel(.85f, .7f)), 5 + 3 * k, .42f, 1.1f, BladeStoneTone);
            int clouds = BladeShare(Mathf.RoundToInt(4 + 4 * k));
            for (int i = 0; i < clouds; i++)
            {
                float side = f != 0 ? (i % 3 == 2 ? -f : f) : i % 2 == 0 ? 1 : -1, throwBack = f != 0 && side != f ? .25f : 1;
                var cloud = Puff(g + new Vector2(side * Range(0, .6f) * R * throwBack, .3f), R * Range(.25f, .4f), BladeDust, new Vector2(side * Range(1, 3.5f) * k * throwBack, Range(.6f, 1.8f)), Range(.8f, 1.3f), 118);
                cloud.endScale = 2.4f; cloud.drag = 2; cloud.fadeIn = .05f;
            }
            EmberBurst(hit, Mathf.RoundToInt((8 + 4 * k) * BladeLevel(.85f, .7f)), 7 + 4 * k, light, .6f, 7);
            int embers = BladeShare(Mathf.RoundToInt(4 + 8 * k));
            for (int i = 0; i < embers; i++)
            {
                float x = f != 0 ? f * Range(-.15f, 1) * R * .85f : Range(-1, 1) * R * .7f;
                var ember = Spark(g + new Vector2(x, .2f), new Vector2(Range(-.4f, .4f), Range(1, 2.6f)), i % 3 == 0 ? Warm : light, Range(.14f, .26f), Range(.7f, 1.1f), -1, 1);
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
                var edge = Attach(root, "Sword wave edge", crescent, .8f, Alpha(light, .95f), 123); edge.scale.y *= 1.5f; edge.pulse = .05f;
                var ink = Attach(root, "Sword wave shadow", crescent, .9f, BladeAmberInk, 119, false, new Vector2(-.08f, 0)); ink.scale.y *= 1.5f;
                var halo = Attach(root, "Sword wave light", glow, 1.1f, Alpha(light, .45f), 121, true, new Vector2(-.2f, 0)); halo.scale.y *= 1.4f;
                Emitter(halo, Emit.Trace, 14, .1f, Alpha(light, .45f));
                Bundle(root, edge, ink, halo);
                return;
            }
            // A towering crescent sword wave with a razor front and an echo behind; a level one stands on the floor.
            // Higher levels raise a taller wave (its foot stays on the floor) with more echoes; level three gilds its crest.
            int grade = Grade; float lift = BladeLevel(1.13f, 1.27f);
            float h = (level ? 3.3f : 2.6f) * lift; Vector2 mid = new Vector2(0, level ? (h * .5f - 1.1f) * up : 0);
            var shadow = Attach(root, "Sword wave shadow", crescent, 2.6f, BladeAmberInk, 118, false, mid + new Vector2(-.2f, 0)); shadow.scale.y *= h * 1.04f / 2.6f;
            var body = Attach(root, "Sword wave", crescent, 2.4f, Alpha(light, 1), 123, true, mid); body.scale.y *= h / 2.4f; body.pulse = .04f;
            var echo = Attach(root, "Sword wave echo", crescent, 2.1f, Alpha(light, .45f), 122, true, mid + new Vector2(-.8f, 0)); echo.scale.y *= h * .88f / 2.1f;
            var extra = new List<Clip>();
            if (grade >= 2)
            {
                var echo2 = Attach(root, "Sword wave second echo", crescent, 1.9f, Alpha(light, .3f), 121, true, mid + new Vector2(-1.55f, 0)); echo2.scale.y *= h * .78f / 1.9f;
                extra.Add(echo2);
            }
            if (grade >= 3)
            {
                var gilt = Attach(root, "Sword wave gilt crest", crescent, 2.2f, Alpha(Warm, .95f), 124, true, mid + new Vector2(.28f, 0)); gilt.scale.y *= h * 1.02f / 2.2f; gilt.pulse = .06f;
                var giltInk = Attach(root, "Sword wave gilt shadow", crescent, 2.5f, Alpha(BladeAmberInk, .55f), 117, false, mid + new Vector2(.1f, 0)); giltInk.scale.y *= h * 1.06f / 2.5f;
                extra.Add(gilt); extra.Add(giltInk);
            }
            var razor = Attach(root, "Sword wave edge", BladeSlashSprite, h, Color.white, 125, true, mid + new Vector2(.62f, 0));
            razor.scale = new Vector3(h * 1.02f / BladeSlashSprite.bounds.size.x, 2.2f / BladeSlashSprite.bounds.size.y, 1); razor.angle = -90;
            var halo2 = Attach(root, "Sword wave light", glow, 2.4f, Alpha(light, .5f), 121, true, mid + new Vector2(-.2f, 0)); halo2.scale.y *= h * .9f / 2.4f; halo2.pulse = .08f;
            var wake = Attach(root, "Sword wave wake", streak, 3f, Alpha(light, .45f), 120, true, mid + new Vector2(-1.5f, 0)); wake.scale.y *= 2.4f;
            var wakeInk = Attach(root, "Sword wave wake shadow", streak, 2.4f, Alpha(BladeAmberInk, .4f), 117, false, mid + new Vector2(-1.2f, 0)); wakeInk.scale.y *= 2.2f;
            float groundY = BladeGroundY(start, .95f); int emitted = 0;
            Emitter(body, 52, .1f, (owner, p) =>
            {
                emitted++;
                if (level)
                {
                    // Tearing the floor under the wave: dust thrown up and back, grit, sparks off the crest and a scored furrow.
                    Vector2 g = new Vector2(p.x + dir.x * Range(-.1f, .6f), groundY);
                    switch (emitted % 4)
                    {
                        case 0:
                            var dust = Puff(g + new Vector2(Range(-.2f, .2f), .22f), Range(.7f, 1.15f), BladeDust, new Vector2(-dir.x * Range(.5f, 2), Range(1.4f, 3)), Range(.45f, .7f), 118);
                            dust.endScale = 2.3f; break;
                        case 1:
                            var chip = Spawn("Wave grit", BladeChunkSprite, g + Vector2.up * .1f, Range(.16f, .3f), BladeStoneTone, Range(.5f, .7f), false, 128);
                            chip.velocity = new Vector2(dir.x * Range(-1.5f, 2.5f), Range(3.5f, 7)); chip.gravity = 18; chip.spin = Range(-600, 600); chip.fadePower = .6f; break;
                        case 2:
                            Spark(new Vector2(p.x, groundY + Range(.3f, 3)), new Vector2(-dir.x * Range(3, 7), Range(-1, 2)), emitted % 8 == 2 ? Color.white : light, Range(.22f, .36f), Range(.22f, .34f), 3, 2); break;
                        default:
                            Decal(g + Vector2.up * .02f, scorch, Range(.7f, 1f), new Color(.14f, .09f, .05f, .5f), .8f, false, 24);
                            Line(g + new Vector2(-dir.x * .4f, .04f), g + new Vector2(dir.x * .4f, .04f), .14f, Alpha(light, .85f), .4f, 26); break;
                    }
                }
                else if (emitted % 3 == 0) Glint(p + Inside(.6f), Range(.4f, .7f), Color.white, .14f);
                else Spark(p + Inside(.9f), -dir * Range(3, 7) + Inside(1.5f), emitted % 2 == 0 ? Color.white : light, Range(.2f, .34f), Range(.2f, .32f), 2, 2);
            });
            extra.AddRange(new[] { shadow, echo, razor, halo2, wake, wakeInk });
            Bundle(root, body, extra.ToArray());
            // Launch: a flash where the wave leaves and a puff of dust at the feet.
            Flash(start + dir * .5f, 1.5f, Alpha(Warm, .75f), .14f);
            Glint(start + dir * .5f, 1.8f, Color.white, .18f);
            if (level) for (int i = 0; i < Budget(4); i++)
                    Puff(new Vector2(start.x + Range(-.3f, .3f), groundY + .15f), Range(.5f, .8f), BladeDust, new Vector2(-dir.x * Range(1, 3), Range(.4f, 1.2f)), Range(.4f, .6f), 118);
            // The wave pierces: every body it passes through is cut (a crescent slash through it), and at the end of its reach it
            // breaks apart into gold light and dust instead of vanishing (unless a last body stopped it).
            float rise = level ? h * .5f - 1.1f : 0;
            ArcheryPassWatch(root, .16f, .08f, (at, d) => BladeWaveThrough(at, d, light, grade));
            ArcheryWatch(root, 4.5f, last =>
            {
                if (BladeRecent(BladeWaveHit, .15f, last, 1.6f)) return;
                BladeWaveBreak(last + Vector2.up * rise, dir, light, h, level ? groundY : float.NaN, grade);
            });
        }

        /// <summary>The sword wave passing through a body it pierces: a gold crescent cut across it over an amber shadow, a flash,
        /// a glint and sparks thrown on along the flight. Light (about ten clips): a wave crosses several bodies.</summary>
        private void BladeWaveThrough(Vector2 at, Vector2 d, Color light, int grade)
        {
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, s = BladeLevel(1.1f, 1.2f);
            var ink = Spawn("Wave pass shadow", crescent, at + d * .2f, 2f * s, Alpha(BladeAmberInk, .7f), .24f, false, 118);
            var cut = Crescent(at + d * .3f, 1.85f * s, Alpha(light, 1), .2f, angle, 131);
            ink.angle = angle; ink.startScale = .75f; ink.endScale = 1.12f; ink.ease = 3; ink.fadePower = 1.3f;
            foreach (var clip in new[] { ink, cut }) { clip.scale.y *= 1.35f; clip.velocity = d * 3; clip.drag = 5; }
            var flash = Flash(at, 1.3f, Alpha(Warm, .8f), .1f); flash.ramp = Ramp(Color.white, light);
            Glint(at, 1.4f, grade >= 3 ? Alpha(BladeAwakeGold, 1) : Color.white, .16f, angle + 45);
            var through = Line(at, at + d * 1.7f, .13f, new Color(1, 1, 1, .85f), .14f, 131); through.velocity = d * 6; through.startScale = .3f; through.endScale = 1; through.ease = 3;
            for (int i = 0; i < 4; i++)
                Spark(at, Direction(angle + Range(-50, 50)) * Range(4, 8), i % 2 == 0 ? Color.white : light, Range(.2f, .32f), Range(.18f, .3f), 5, 2.5f);
        }

        /// <summary>A sword wave reaching the end of its flight: the crescent bursts forward and frays into gold light, sparks and
        /// (for a wave running along the floor) a last kick of dust and grit. <paramref name="ground"/> is NaN for a wave in the air.</summary>
        private void BladeWaveBreak(Vector2 center, Vector2 d, Color light, float h, float ground, int grade)
        {
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            for (int layer = 0; layer < 2; layer++)
            {
                bool ink = layer == 0;
                var ghost = Spawn(ink ? "Wave break shadow" : "Wave break", crescent, center + d * (ink ? .1f : .25f), ink ? 2.6f : 2.4f,
                    ink ? Alpha(BladeAmberInk, .6f) : Alpha(grade >= 3 ? Warm : light, .85f), ink ? .34f : .3f, !ink, ink ? 118 : 127);
                ghost.angle = angle; ghost.scale.y *= h * (ink ? 1.02f : .96f) / (ink ? 2.6f : 2.4f); ghost.root.transform.localScale = ghost.scale;
                ghost.velocity = d * 5; ghost.drag = 3; ghost.startScale = 1; ghost.endScale = 1.3f; ghost.axisEnd = new Vector2(1.5f, .55f); ghost.ease = 2; ghost.fadePower = 1.4f;
            }
            var flash = Flash(center + d * .5f, 1.6f, Alpha(Warm, .75f), .14f); flash.ramp = Ramp(Color.white, light);
            Glint(center + d * .6f, 1.5f, Color.white, .18f, angle);
            int sparks = Budget(6);
            for (int i = 0; i < sparks; i++)
                Spark(center + new Vector2(0, Range(-.45f, .45f) * h), Direction(angle + Range(-40, 40)) * Range(4, 8), i % 3 == 0 ? Color.white : light, Range(.2f, .34f), Range(.25f, .4f), 4, 2.2f);
            int motes = Budget(4);
            for (int i = 0; i < motes; i++)
            {
                var mote = Spawn("Wave break mote", glow, center + new Vector2(Range(-.2f, .4f), Range(-.4f, .4f) * h), Range(.16f, .28f), Alpha(i % 2 == 0 ? Warm : light, .9f), Range(.45f, .7f), true, 130);
                mote.velocity = d * Range(.8f, 2) + Vector2.up * Range(.3f, 1.1f); mote.drag = 1.6f; mote.flicker = .4f; mote.endScale = .25f; mote.ease = 1;
            }
            if (float.IsNaN(ground)) return;
            for (int i = 0, n = Budget(3); i < n; i++)
            {
                var dust = Puff(new Vector2(center.x + Range(-.3f, .5f), ground + .2f), Range(.65f, 1f), BladeDust, d * Range(.8f, 2.2f) + Vector2.up * Range(.6f, 1.4f), Range(.55f, .8f), 118);
                dust.endScale = 2.3f;
            }
            BladeRubble(new Vector2(center.x, ground + .25f), 3, 4, .3f, .7f, BladeStoneTone);
        }

        private void HitShockwave(Vector2 pos, Color tint, bool signature, Vector2 direction)
        {
            Color light = Color.Lerp(BladeHotGold, tint, .3f);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (!signature)
            {
                Flash(pos, .9f, Alpha(Warm, .85f), .1f);
                Crescent(pos + direction * .15f, 1.1f, Alpha(light, .95f), .16f, angle, 131);
                var ink = Spawn("Wave hit shadow", crescent, pos + direction * .1f, 1.15f, BladeAmberInk, .18f, false, 118);
                ink.angle = angle; ink.startScale = .75f; ink.endScale = 1.1f; ink.ease = 3; ink.fadePower = 1.3f;
                for (int i = 0; i < 6; i++) Spark(pos, Direction(angle + Range(-60, 60)) * Range(3, 6), i % 2 == 0 ? Color.white : light, Range(.18f, .3f), Range(.15f, .25f), 4, 3);
                return;
            }
            Underlay(pos, 2.6f, .32f, .3f);
            var bloom = Flash(pos, 2.4f, Alpha(light, .95f), .18f); bloom.ramp = Ramp(Color.white, light); BladeWaveHit = bloom;
            ImpactLines(pos, 3.4f, Alpha(Warm, .85f), .2f);
            Glint(pos, 1.8f, Color.white, .22f, angle);
            var arcInk = Spawn("Wave hit shadow", crescent, pos + direction * .15f, 2.6f, BladeAmberInk, .28f, false, 118);
            arcInk.angle = angle; arcInk.scale.y *= 1.4f; arcInk.startScale = .75f; arcInk.endScale = 1.12f; arcInk.ease = 3; arcInk.fadePower = 1.3f;
            var arc = Crescent(pos + direction * .25f, 2.4f, Alpha(light, 1), .24f, angle, 131); arc.scale.y *= 1.4f;
            Wave(pos, 2.2f, Alpha(light, .8f), .26f, 3);
            int sparks = Budget(14);
            for (int i = 0; i < sparks; i++)
                Spark(pos + Inside(.2f), Direction(angle + Range(-70, 70)) * Range(4, 10), i % 3 == 0 ? Color.white : light, Range(.2f, .4f), Range(.25f, .45f), 6, 2.2f);
            BladeRubble(pos, 5, 5, .34f, .8f, BladeStoneTone);
            int dusts = Budget(5);
            for (int i = 0; i < dusts; i++) Puff(pos + Inside(.4f), Range(.5f, .8f), BladeDust, direction * Range(.5f, 2) + Vector2.up * Range(.4f, 1.2f), Range(.5f, .8f), 118);
        }

        private void TrailShockwave(Vector2 from, Vector2 to, Color tint)
        {
            Vector2 delta = to - from; float length = delta.magnitude; if (length < .05f) return;
            Vector2 d = delta / length; float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Color light = Color.Lerp(BladeHotGold, tint, .35f);
            const float sweep = .16f;
            // A crescent wave races down the line, scoring a golden cut behind it.
            var waveInk = Spawn("Sweep wave shadow", crescent, from - d * .12f, 1.6f, BladeAmberInk, sweep + .1f, false, 118);
            var wave = Spawn("Sweep wave", crescent, from, 1.45f, Alpha(light, 1), sweep + .08f, true, 128);
            foreach (var clip in new[] { waveInk, wave }) { clip.angle = angle; clip.scale.y *= 1.5f; clip.velocity = d * length / sweep; clip.hold = .6f; clip.fadePower = 2; }
            int segments = Mathf.Clamp(Mathf.RoundToInt(length * 1.2f), 2, 6);
            for (int i = 0; i < segments; i++)
            {
                Vector2 a = Vector2.Lerp(from, to, i / (float)segments), b = Vector2.Lerp(from, to, (i + 1f) / segments) + d * .2f;
                float delay = i / (float)segments * sweep;
                var ink = BladeCutLine(a, b, .8f, Alpha(BladeAmberInk, .6f), .32f, 117, false); ink.fadePower = 1.4f; BladeDelay(ink, delay);
                var cut = Line(a, b, .5f, Alpha(light, .85f), .28f, 127, 1.05f); cut.fadePower = 1.4f; BladeDelay(cut, delay);
                var core2 = BladeCutLine(a, b, .16f, Color.white, .2f, 129); BladeDelay(core2, delay);
            }
            // Dust and grit kicked up along the floor, sparks thrown forward as it passes.
            float gy = BladeGroundY(from, .8f);
            int count = Budget(Mathf.Clamp(Mathf.RoundToInt(length * 1.6f), 3, 10));
            for (int i = 0; i < count; i++)
            {
                float t = (i + .5f) / count;
                var dust = Puff(new Vector2(Mathf.Lerp(from.x, to.x, t), gy + .15f), Range(.5f, .8f), BladeDust, new Vector2(d.x * Range(.6f, 1.6f), Range(.8f, 1.8f)), Range(.45f, .7f), 118);
                BladeDelay(dust, t * sweep);
                var spark = Spark(Vector2.Lerp(from, to, t) + Vector2.up * Range(-.5f, .6f), d * Range(3, 7) + Vector2.up * Range(-1, 2), i % 2 == 0 ? Color.white : light, Range(.18f, .32f), Range(.2f, .35f), 4, 2.5f);
                BladeDelay(spark, t * sweep);
            }
            var end = Flash(to, 1.6f, Alpha(Warm, .85f), .14f); BladeDelay(end, sweep);
            var star = Glint(to, 1.4f, Color.white, .18f); BladeDelay(star, sweep);
        }

        // ---------------------------------------------------------------- BladeStorm: a whirlwind of swords

        private void CastBladeStorm(CastInfo c)
        {
            float w = c.windup, release = Mathf.Max(0, w - .03f);
            Vector2 pos = c.pos, feet = new Vector2(pos.x, BladeGroundY(pos, 1)), hub = pos + Vector2.up * .35f;
            Color light = Color.Lerp(BladeHotGold, c.light, .3f), pale = Color.Lerp(light, Color.white, .45f);
            if (w < .2f)
            {
                // An instant echo: one quick whirl round the body; the storm follows at once.
                var quickInk = Spawn("Storm quick whirl shadow", BladeWhirlSprite, hub, 3.3f, Alpha(BladeBronzeInk, .6f), w + .14f, false, 116);
                var quick = Spawn("Storm quick whirl", BladeWhirlSprite, hub, 3.1f, Alpha(light, .8f), w + .12f, true, 124);
                foreach (var clip in new[] { quickInk, quick }) { clip.spin = 1100; clip.startScale = .5f; clip.endScale = 1.05f; clip.ease = 2; clip.fadeIn = .03f; clip.fadePower = 1.5f; }
                var quickStar = Glint(hub + Vector2.up * .5f, 2f, Color.white, .18f); BladeDelay(quickStar, release);
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
                for (int layer = 0; layer < 2; layer++)
                {
                    bool ink = layer == 0;
                    var sword = Spawn(ink ? "Phantom sword shadow" : "Phantom sword", BladeSwordSprite, hub, ink ? 2.4f : 2.2f, ink ? BladeBronzeInk : Alpha(pale, 1), life, !ink, ink ? 117 : 127);
                    sword.orbitCenter = hub; sword.orbitAngle = start; sword.orbit = orbitSpeed; sword.orbitRadius = ringStart; sword.orbitRadiusEnd = ringEnd; sword.orbitPower = .7f;
                    sword.spin = orbitSpeed; sword.fadeIn = .05f; sword.hold = .9f; sword.fadePower = 2;
                    sword.position = hub + Direction(start) * ringStart; sword.root.transform.position = sword.position;
                    sword.angle = start; sword.root.transform.localRotation = Quaternion.Euler(0, 0, start);
                    sword.scale.y *= ink ? 2.1f : 1.8f; sword.startScale = .55f; sword.endScale = 1; sword.ease = 3; sword.root.transform.localScale = sword.scale;
                    BladeDelay(sword, delay);
                }
                var spark = Glint(hub + Direction(start) * ringStart, 1.2f, Color.white, .16f); BladeDelay(spark, delay);
                // Where this sword sits when the wheel bursts: it shoots straight out from there.
                float at = start + (release - delay) * orbitSpeed;
                Vector2 from = hub + Direction(at) * ringEnd;
                for (int layer = 0; layer < 2; layer++)
                {
                    bool ink = layer == 0;
                    var flung = Spawn(ink ? "Flung sword shadow" : "Flung sword", BladeSwordSprite, from, ink ? 2.5f : 2.3f, ink ? BladeBronzeInk : new Color(1, .97f, .85f, 1), .24f, !ink, ink ? 117 : 128);
                    flung.angle = at; flung.velocity = Direction(at) * 17; flung.drag = 4; flung.scale.y *= ink ? 2.1f : 1.8f; flung.root.transform.localScale = flung.scale;
                    flung.root.transform.localRotation = Quaternion.Euler(0, 0, at); flung.stretch = .03f; flung.fadePower = 1.6f;
                    BladeDelay(flung, release);
                }
            }
            // The storm forming: a whirl of light spinning up round the body over a golden glow and rune ring at the feet.
            var whirlInk = Spawn("Storm forming shadow", BladeWhirlSprite, hub, 3.3f, Alpha(BladeBronzeInk, .5f), w + .04f, false, 116);
            var whirl = Spawn("Storm forming", BladeWhirlSprite, hub, 3.1f, Alpha(light, .55f), w + .04f, true, 124);
            foreach (var clip in new[] { whirlInk, whirl }) { clip.spin = 900; clip.startScale = .45f; clip.endScale = 1; clip.ease = 2; clip.fadeIn = .1f; clip.hold = .8f; clip.fadePower = 2; }
            whirlInk.angle = -14;
            // A golden column behind the body (behind the actors) swells to the release.
            float lift = BladeLevel(1.15f, 1.3f);
            var columnInk = BladeColumn(c.anchor, feet, 2.6f, 4f * lift, new Color(.36f, .18f, .03f, .45f), w + .05f, 28, false);
            var column = BladeColumn(c.anchor, feet, 2f, 4.6f * lift, Alpha(light, .75f), w + .05f, 29);
            foreach (var clip in new[] { columnInk, column }) { clip.axisStart = new Vector2(.4f, .25f); clip.axisEnd = Vector2.one; clip.ease = 1.3f; clip.fadeIn = .05f; clip.hold = .9f; clip.fadePower = 3; }
            BladeBackShade(hub, 4.2f, new Color(.24f, .12f, .02f, .35f), w + .1f);
            if (grade >= 3 && w >= .2f)
            {
                // Awakened: the wind is already being dragged in from far out, and a gold-white crown turns over the head.
                BladeInward(hub + Vector2.down * .3f, 7, 4.8f, Warm, w, .12f);
                var crownPlane = BladeFlatPlane(c.anchor, hub + Vector2.up * 2.1f, .3f, w + .1f);
                var crownInk = BladeOnPlane(crownPlane, "Storm crown shadow", runeRing, 2.7f, Alpha(BladeBronzeInk, .6f), w + .1f, 116, false, 300);
                var crown = BladeOnPlane(crownPlane, "Storm crown", runeRing, 2.6f, Alpha(BladeAwakeGold, .95f), w + .1f, 128, true, 300);
                foreach (var clip in new[] { crownInk, crown }) { clip.startScale = .3f; clip.endScale = 1; clip.ease = 3; }
            }
            GroundGlow(feet + Vector2.up * .05f, 4.2f, Alpha(light, .55f), w + .2f);
            var floor = BladeFlatPlane(c.anchor, feet + Vector2.up * .06f, .26f, w + .3f);
            var runeInk = BladeOnPlane(floor, "Storm rune shadow", runeRing, 4f, Alpha(BladeBronzeInk, .55f), w + .3f, 25, false, 220);
            var rune = BladeOnPlane(floor, "Storm rune", runeRing, 3.9f, Alpha(light, .9f), w + .3f, 27, true, 220);
            var vortex = BladeOnPlane(floor, "Storm cast vortex", swirl, 4.4f, Alpha(light, .6f), w + .1f, 26, true, 600);
            foreach (var clip in new[] { runeInk, rune, vortex }) { clip.startScale = .5f; clip.endScale = 1; clip.ease = 3; }
            Converge(hub, 12, 2.2f, light, w, .6f);
            int dusts = Budget(6);
            for (int i = 0; i < dusts; i++)
            {
                float side = i % 2 == 0 ? 1 : -1, x = Range(1.2f, 2.2f);
                var puff = Puff(feet + new Vector2(side * x, .18f), Range(.55f, .85f), BladeDust, new Vector2(-side * x * 2, Range(.4f, 1)), w * Range(.75f, 1.05f), 118);
                puff.drag = 2.5f; puff.spin = 260 * side;
            }
            // Release: a ring and a star as the storm breaks out.
            var ring2 = Wave(hub, 4.8f, Alpha(light, .85f), .26f, 3); BladeDelay(ring2, release);
            var flare = Glint(hub + Vector2.up * .5f, 2.4f, Color.white, .2f); BladeDelay(flare, release);
        }

        private void ImpactBladeStorm(Vector2 pos, float radius, Color tint, int facing)
        {
            float R = Mathf.Clamp(radius, .9f, 4.5f), k = Mathf.Clamp(R / 3, .35f, 1.4f);
            Color light = Color.Lerp(BladeHotGold, tint, .3f);
            Vector2 center = pos + Vector2.up * .35f;
            // A full sword circle whips round the body. Higher levels' zones already stack more light round the hero, so each
            // turn's own additive light steps down a little to keep the body readable on pale skies.
            bool clockwise = Chance(.5f); float start = Range(0, 360), calm = BladeLevel(.85f, .75f);
            var swingInk = Spawn("Storm swing shadow", BladeWhirlSprite, center, R * 1.4f, BladeBronzeInk, .2f, false, 117);
            var swing = Spawn("Storm swing", BladeWhirlSprite, center, R * 1.32f, Alpha(light, calm), .18f, true, 128);
            foreach (var clip in new[] { swingInk, swing })
            {
                if (clockwise) clip.scale.x *= -1;
                clip.angle = start; clip.spin = clockwise ? -1500 : 1500; clip.startScale = .75f; clip.endScale = 1.06f; clip.ease = 3; clip.fadePower = 1.4f;
            }
            swingInk.angle = start + (clockwise ? 14 : -14);
            // Curved cuts snap open off-centre in an X, then a flat sweep crosses the whole area; each is tipped with a glint.
            float crossSide = facing != 0 ? facing : (Chance(.5f) ? 1 : -1);
            Vector2 cross = center + new Vector2(crossSide * Range(.3f, .5f) * R, Range(-.1f, .3f));
            float tilt = Range(22, 40);
            for (int i = 0; i < 3; i++)
            {
                float angle = (i == 0 ? tilt : i == 1 ? -tilt : Range(-8, 8)) + (Chance(.5f) ? 180 : 0);
                float delay = .02f + i * .05f, width = 2 * R * (i == 2 ? 1.12f : .95f), height = width * .5f;
                Vector2 at = (i == 2 ? center + Vector2.down * .5f : cross) + Inside(.12f * R);
                var ink = BladeSlashAt(at, width * 1.04f, height * 1.05f, angle, BladeBronzeInk, .26f, false, 117);
                var body = BladeSlashAt(at, width, height, angle, Alpha(light, 1), .22f, true, 129);
                var edge = BladeSlashAt(at, width * .97f, height, angle, new Color(1, 1, 1, .8f), .16f, true, 130);
                BladeSnap(ink, delay); BladeSnap(body, delay); BladeSnap(edge, delay);
                var tip = Glint(at + Direction(angle) * width * .38f * (Chance(.5f) ? 1 : -1), .9f + .3f * k, Color.white, .16f, Range(0, 45)); BladeDelay(tip, delay + .03f);
            }
            Flash(cross, .9f * k + .3f, Alpha(Warm, .4f * calm), .1f);
            ImpactLines(cross, R * 1.7f, Alpha(light, .45f * calm), .18f);
            // A flurry of thin cuts flickers through the storm.
            int cuts = Budget(4);
            for (int i = 0; i < cuts; i++)
            {
                Vector2 mid = center + Inside(R * .7f); float a = Range(0, 180), len = R * Range(.5f, .9f);
                Vector2 half = Direction(a) * len * .5f; float delay = .03f + i * .045f;
                var cutInk = BladeCutLine(mid - half, mid + half, .4f, BladeBronzeInk, .16f, 118, false); cutInk.fadePower = 1.5f; BladeDelay(cutInk, delay);
                var cut = BladeCutLine(mid - half, mid + half, .18f, Color.white, .14f, 130); cut.fadePower = 1.5f; BladeDelay(cut, delay);
            }
            // Sparks sprayed along the slashes.
            int sparks = BladeShare(Mathf.RoundToInt(8 + 6 * k));
            for (int i = 0; i < sparks; i++)
            {
                float a = (i % 2 == 0 ? tilt : -tilt) + (Chance(.5f) ? 180 : 0) + Range(-12, 12);
                Spark(cross + Inside(.3f), Direction(a) * Range(5, 11) * (.6f + .4f * k), i % 3 == 0 ? Color.white : light, Range(.2f, .38f), Range(.2f, .36f), 5, 2.4f);
            }
            Wave(center, R * 1.5f, Alpha(light, .4f), .22f, 3);
            // The rim of the storm bites on both sides: short curved cuts with a hit flash where enemies stand, and a ring on
            // the floor that marks the reach of every turn.
            float floorY = BladeGroundY(pos, 1.1f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 bite = new Vector2(center.x + s * R * Range(.72f, .92f), floorY + Range(.6f, 1.3f));
                float angle = Range(-35, 35) + (s < 0 ? 180 : 0), delay = Range(.03f, .1f);
                var biteInk = BladeSlashAt(bite, 2f * (.6f + .4f * k), 1f * (.6f + .4f * k), angle, BladeBronzeInk, .22f, false, 117);
                var biteLight = BladeSlashAt(bite, 1.9f * (.6f + .4f * k), .95f * (.6f + .4f * k), angle, Alpha(Warm, 1), .18f, true, 130);
                BladeSnap(biteInk, delay); BladeSnap(biteLight, delay);
                var pop = Flash(bite, 1.1f * (.6f + .4f * k), Alpha(light, .8f), .1f); BladeDelay(pop, delay + .02f);
                var star = Glint(bite, 1.2f * (.6f + .4f * k), Color.white, .14f, Range(0, 45)); BladeDelay(star, delay + .02f);
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
            Color light = BladeHotGold, pale = Color.Lerp(light, Color.white, .45f);
            var layers = new List<Clip>();
            var heart = Attach(root, "Blade storm heart", glow, R * .9f, Alpha(light, .1f), 121, true, new Vector2(0, .35f));
            BladeZoneFade(heart, life); heart.pulse = .12f;
            // A bronze shadow behind the actors gives the whirl a dark eye to spin against on pale floors.
            var shade = Attach(root, "Blade storm shadow", BladeShadeSprite, R * 2.3f, new Color(.22f, .11f, .02f, .3f), 28, false, new Vector2(0, .3f));
            shade.scale.y *= .55f; BladeZoneFade(shade, life); layers.Add(shade);
            // Outer orbit: two long sweeps racing round a flat ellipse at the hips; a tilted counter-rotating one inside it.
            // Higher levels add layers (a counter ring of swords, a taller cyclone, a crown), so these sweeps dim to make room:
            // the new layers replace light rather than pile more of it on the hero.
            int grade = Grade; float calm = BladeLevel(.7f, .6f);
            BladeOrbit(layers, BladePlane(root, new Vector2(0, .32f), .3f, 0), R * 2, 2, 820, light, .95f * calm, life, 124);
            BladeOrbit(layers, BladePlane(root, new Vector2(0, .5f), .48f, 15), R * 1.45f, 2, -1000, light, .75f * calm, life, 125);
            // The whirlwind climbs: narrower, fainter sweeps stacked above the head give the storm a cyclone's height.
            BladeOrbit(layers, BladePlane(root, new Vector2(0, 1.75f), .32f, -6), R * 1.2f, 1, 1250, pale, .6f * calm, life, 123);
            BladeOrbit(layers, BladePlane(root, new Vector2(0, 2.75f), .36f, 8), R * .8f, 1, -1500, pale, .45f * calm, life, 122);
            // Inner orbit: a ring of spectral swords circling the body.
            var swordPlane = BladePlane(root, new Vector2(0, .42f), .62f, -10);
            var swordInk = Attach(swordPlane, "Storm swords shadow", BladeRingSprite, R * 1.56f, BladeBronzeInk, 118, false); swordInk.spin = 600; swordInk.angle = -8;
            var swords = Attach(swordPlane, "Storm swords", BladeRingSprite, R * 1.5f, Alpha(pale, 1), 126); swords.spin = 600;
            // Wind: fast pale streaks, and a golden vortex over a dust ring skimming the floor.
            var wind = Attach(BladePlane(root, new Vector2(0, .7f), .4f, 7), "Storm wind", BladeWhirlSprite, R * 2.3f, new Color(1, 1, 1, .3f), 123); wind.spin = 1300;
            var floor = BladePlane(root, new Vector2(0, -.5f), .26f, 0);
            var vortex = Attach(floor, "Storm vortex", swirl, R * 2.4f, Alpha(light, .5f), 116); vortex.spin = 520;
            var dust = Attach(floor, "Storm dust ring", swirl, R * 2.5f, new Color(.5f, .38f, .24f, .5f), 113, false); dust.spin = 520; dust.angle = 40;
            foreach (var clip in new[] { swordInk, swords, wind, vortex, dust }) { BladeZoneFade(clip, life); layers.Add(clip); }
            // Sparks flung off the rim, glints along it and dust skidding round the feet.
            int tick = 0;
            Emitter(heart, 46, 0, (owner, p) =>
            {
                tick++;
                float phi = Range(0, 360) * Mathf.Deg2Rad, cos = Mathf.Cos(phi), sin = Mathf.Sin(phi);
                Vector2 on = p + new Vector2(cos * R, sin * R * .3f - .03f), tangent = new Vector2(-sin, cos * .3f).normalized;
                if (tick % 6 == 0)
                    Puff(new Vector2(on.x, p.y - .82f), Range(.5f, .85f), BladeDust, tangent * Range(1.5f, 3) + Vector2.up * Range(.3f, .9f), Range(.45f, .7f), 118);
                else if (tick % 5 == 0)
                {
                    // A gust spiralling up the cyclone wall.
                    Vector2 wall = p + new Vector2(cos * R * .55f, -.5f + sin * .15f), rise = (tangent * 3.5f + Vector2.up * 6).normalized;
                    var gust = BladeCutLine(wall, wall + rise * Range(.9f, 1.5f), Range(.07f, .13f), tick % 2 == 0 ? new Color(1, 1, 1, .75f) : Alpha(light, .85f), Range(.24f, .34f), 124);
                    gust.velocity = rise * Range(5, 8) + tangent * 2; gust.fadeIn = .03f; gust.fadePower = 1.3f;
                }
                else if (tick % 4 == 0) Glint(on, Range(.5f, .9f), Color.white, .14f, Range(0, 90));
                else Spark(on, tangent * Range(6, 11) + (on - p).normalized * 1.5f, tick % 3 == 0 ? Color.white : light, Range(.22f, .38f), Range(.18f, .3f), 2, 2.4f);
            });
            if (grade >= 2)
            {
                // Level two: a second ring of swords turns the other way higher on the body and the cyclone climbs taller.
                var plane2 = BladePlane(root, new Vector2(0, 1f), .5f, 12);
                var ring2Ink = Attach(plane2, "Storm counter swords shadow", BladeRingSprite, R * 1.2f, BladeBronzeInk, 118, false);
                var ring2 = Attach(plane2, "Storm counter swords", BladeRingSprite, R * 1.15f, Alpha(Color.Lerp(pale, Warm, .5f), .7f), 126);
                foreach (var clip in new[] { ring2Ink, ring2 }) { clip.scale.x *= -1; clip.spin = -760; clip.angle = 50; BladeZoneFade(clip, life); layers.Add(clip); }
                BladeOrbit(layers, BladePlane(root, new Vector2(0, 3.65f), .4f, -10), R * .6f, 1, 1800, pale, .38f, life, 121);
            }
            if (grade >= 3)
            {
                // Level three, awakened: a gold-white crown turns over the cyclone, and wind streaks and dust rush in along the
                // floor from as far as the storm drags enemies (1.6 times its reach) toward the caster.
                var crownPlane = BladePlane(root, new Vector2(0, 3.2f), .3f, 0);
                var crownInk = Attach(crownPlane, "Storm crown shadow", runeRing, 2.75f, Alpha(BladeBronzeInk, .6f), 116, false);
                var crown = Attach(crownPlane, "Storm crown", runeRing, 2.6f, Alpha(BladeAwakeGold, .95f), 127);
                foreach (var clip in new[] { crownInk, crown }) { clip.spin = 260; BladeZoneFade(clip, life); layers.Add(clip); }
                var pull = Attach(root, "Storm suction", glow, .1f, new Color(0, 0, 0, 0), 110);
                BladeZoneFade(pull, life); layers.Add(pull);
                float outer = Mathf.Max(radius * 1.6f, R + .8f), inner = R * .3f; int beat = 0;
                Emitter(pull, 36, 0, (owner, p) =>
                {
                    if (owner.age > owner.life - .3f) return;
                    beat++;
                    Vector2 o = owner.root.transform.position;
                    float side = beat % 2 == 0 ? 1 : -1, floorY = o.y - .62f, x = Range(R * .75f, outer);
                    float speed = Range(8, 11), travel = Mathf.Clamp((x - inner) / speed, .12f, .45f);
                    if (beat % 3 == 0)
                    {
                        // Dust skidding in along the floor: a dark scuff streak with a puff kicked up behind it, as if dragged.
                        Vector2 foot = new Vector2(o.x + side * x, floorY + .1f), pull = new Vector2(-side, 0);
                        var skid = BladeCutLine(foot - pull * Range(1.4f, 2.2f), foot, Range(.3f, .4f), new Color(.4f, .3f, .2f, .6f), travel, 117, false);
                        skid.velocity = pull * speed; skid.fadeIn = .04f; skid.hold = .5f; skid.fadePower = 1.4f;
                        var drift = Puff(foot + new Vector2(0, .12f), Range(.5f, .75f), BladeDust, pull * Range(4, 6) + Vector2.up * Range(.1f, .4f), Range(.4f, .55f), 118);
                        drift.drag = 1.2f; drift.endScale = 1.6f; return;
                    }
                    // A wind streak with a dark core rushing in toward the caster at body height.
                    float y = floorY + Range(.35f, 1.8f), len = Range(1.4f, 2.3f);
                    Vector2 head = new Vector2(o.x + side * x, y), dir = new Vector2(-side, Range(-.05f, .05f));
                    var ink = BladeCutLine(head - dir * len * 1.05f, head, .38f, new Color(.32f, .16f, .02f, .62f), travel, 116, false);
                    var lit = BladeCutLine(head - dir * len, head, Range(.2f, .3f), beat % 4 == 1 ? new Color(1, 1, 1, .9f) : Alpha(BladeAwakeGold, .95f), travel, 123);
                    foreach (var clip in new[] { ink, lit }) { clip.velocity = dir * speed; clip.fadeIn = .04f; clip.hold = .55f; clip.fadePower = 1.5f; }
                });
            }
            Bundle(root, heart, layers.ToArray());
        }

        // ---------------------------------------------------------------- DashStrike: blazing dash and iaido flash cut

        private void CastDashStrike(CastInfo c)
        {
            int f = c.facing; float w = c.windup, release = Mathf.Max(0, w - .02f);
            Vector2 pos = c.pos, feet = new Vector2(pos.x, BladeGroundY(pos, 1)), hip = pos + new Vector2(-f * .25f, -.25f), eye = pos + new Vector2(f * .16f, .52f);
            Color cyan = Color.Lerp(BladeCyan, c.light, .3f), aura = new Color(.04f, .16f, .52f, .62f);
            if (w < .2f)
            {
                // An instant echo: the blade's gleam at the hip, a glare and a sonic ring as the body launches.
                var quickGleam = Anchored(c.anchor, hip + new Vector2(f * .4f, .08f), "Dash quick gleam", BladeCutSprite, 2.4f, new Color(1, 1, 1, .95f), w + .05f, 128);
                quickGleam.angle = f > 0 ? 18 : 162; quickGleam.startScale = .3f; quickGleam.endScale = 1.2f; quickGleam.ease = 1.5f; quickGleam.fadePower = 1.5f;
                var quickGlare = Spawn("Dash quick glare", streak, pos + new Vector2(f * .6f, 0), 5f, new Color(1, 1, 1, .9f), .12f, true, 131);
                quickGlare.scale.y *= .45f; quickGlare.startScale = .4f; quickGlare.endScale = 1.2f; quickGlare.ease = 3; quickGlare.fadePower = 1.5f; BladeDelay(quickGlare, release);
                var quickRing = Spawn("Dash quick ring", ring, pos + new Vector2(f * .5f, 0), 2.6f, Alpha(cyan, .9f), .2f, true, 130);
                quickRing.scale.x *= .38f; quickRing.startScale = .3f; quickRing.endScale = 1.1f; quickRing.ease = 3; quickRing.fadePower = 1.3f; BladeDelay(quickRing, release);
                return;
            }
            // A dark battle aura: a deep-blue halo behind the crouched body and blue smoke boiling tight round it.
            BladeBackShade(pos + Vector2.up * .2f, 4.2f, new Color(.02f, .07f, .3f, .55f), w + .08f, 1.15f);
            int smokes = BladeShare(6);
            for (int i = 0; i < smokes; i++)
            {
                float side = i % 2 == 0 ? 1 : -1, delay = Range(0, w * .5f);
                var smoke = Puff(pos + new Vector2(side * Range(.35f, .7f), Range(-.8f, .35f)), Range(1.1f, 1.6f), aura, new Vector2(side * Range(.1f, .35f), Range(.9f, 1.8f)), Mathf.Clamp(w - delay + .05f, .15f, .5f), 29);
                smoke.drag = 1; smoke.startScale = .6f; smoke.endScale = 1.4f; BladeDelay(smoke, delay);
            }
            // A cyan column of battle aura rises behind the body (behind the actors) and swells until the dash.
            int grade = Grade; float lift = BladeLevel(1.15f, 1.3f);
            var columnInk = BladeColumn(c.anchor, feet, 2.9f, 4.4f * lift, new Color(.02f, .1f, .38f, .55f), w + .04f, 28, false);
            var column = BladeColumn(c.anchor, feet, 2.3f, 5f * lift, Alpha(cyan, .9f), w + .04f, 29);
            foreach (var clip in new[] { columnInk, column }) { clip.axisStart = new Vector2(.4f, .2f); clip.axisEnd = Vector2.one; clip.ease = 1.2f; clip.fadeIn = .04f; clip.hold = .92f; clip.fadePower = 3; }
            column.pulse = .07f;
            var auraLight = Anchored(c.anchor, pos + Vector2.up * .1f, "Dash charge aura", glow, 2.2f, Alpha(cyan, .3f), w + .04f, 120);
            auraLight.fadeIn = .05f; auraLight.startScale = .5f; auraLight.endScale = 1.2f; auraLight.ease = 1; auraLight.pulse = .1f; auraLight.hold = .9f; auraLight.fadePower = 2.5f;
            Emitter(auraLight, 56, .1f, (owner, p) =>
            {
                // Aura flame licks: needles of light shooting up the body's outline, thicker and longer as the dash nears.
                float charged = Mathf.Clamp01(owner.age / Mathf.Max(.05f, w));
                if (!Chance(.35f + .65f * charged)) return;
                float side = Chance(.5f) ? 1 : -1;
                Vector2 at = p + new Vector2(side * Range(.25f, .7f), Range(-1f, .2f)), rise = new Vector2(-side * Range(0, .25f), 1).normalized;
                var lick = BladeCutLine(at, at + rise * Range(.6f, 1.2f) * (.7f + .5f * charged), Range(.12f, .22f), Chance(.3f) ? Color.white : Alpha(cyan, 1), Range(.14f, .22f), 121);
                lick.velocity = rise * Range(3, 5.5f); lick.fadeIn = .03f; lick.fadePower = 1.3f;
            });
            // The sheathed blade at the hip swells with light right up to the draw; the eyes flash just before the dash.
            var charge = Anchored(c.anchor, hip, "Dash charge", glow, 1.8f, Alpha(cyan, .95f), w + .04f, 126);
            charge.fadeIn = .03f; charge.startScale = .3f; charge.endScale = 1.6f; charge.ease = 1; charge.pulse = .12f; charge.hold = .92f; charge.fadePower = 3;
            var spark = Anchored(c.anchor, hip, "Dash charge core", core, .45f, Color.white, w + .03f, 127);
            spark.startScale = .4f; spark.endScale = 1.4f; spark.ease = 1; spark.pulse = .2f; spark.hold = .92f; spark.fadePower = 2;
            var gleam = Anchored(c.anchor, hip + new Vector2(f * .4f, .08f), "Dash blade gleam", BladeCutSprite, 2.4f, new Color(1, 1, 1, .95f), w + .03f, 128);
            gleam.angle = f > 0 ? 18 : 162; gleam.startScale = .25f; gleam.endScale = 1.2f; gleam.ease = 1.2f; gleam.hold = .9f; gleam.fadePower = 2;
            if (grade >= 2)
            {
                // Higher levels: the drawn edge burns longer and, awakened, gold-white along its length.
                var gild = Anchored(c.anchor, hip + new Vector2(f * .5f, .1f), "Dash blade gilt", BladeCutSprite, 2.4f * lift + .4f, Alpha(grade >= 3 ? BladeAwakeGold : Warm, .9f), w + .03f, 129);
                gild.angle = f > 0 ? 18 : 162; gild.scale.y *= 1.4f; gild.startScale = .25f; gild.endScale = 1.2f; gild.ease = 1.2f; gild.hold = .9f; gild.fadePower = 2; gild.pulse = .08f;
            }
            var eyeGlint = Anchored(c.anchor, eye, "Dash eye glint", glint, 1.6f, new Color(.85f, 1, 1, 1), .2f, 133);
            eyeGlint.startScale = .2f; eyeGlint.endScale = 1; eyeGlint.ease = 4; eyeGlint.spin = 160; eyeGlint.fadePower = 1.6f; BladeDelay(eyeGlint, Mathf.Max(.04f, w - .15f));
            int zaps = BladeShare(4);
            for (int i = 0; i < zaps; i++) BladeZap(pos + Inside(.9f), 1.5f, cyan, Range(.04f, w * .9f));
            // A thin line of intent runs from the hip to the target and brightens until the dash: the path the cut will take.
            if ((c.target - hip).sqrMagnitude > .25f)
            {
                var aimInk = BladeCutLine(hip, c.target, .24f, new Color(.03f, .1f, .38f, .55f), w + .02f, 117, false);
                var aimLine = BladeCutLine(hip, c.target, .08f, Alpha(cyan, 1), w + .02f, 128);
                foreach (var clip in new[] { aimInk, aimLine }) { clip.fadeIn = w * .75f; clip.hold = .92f; clip.fadePower = 2; }
                aimLine.flicker = .3f;
            }
            // Ground charge: a navy ring, a cyan glow, a fast vortex and a ring of wind drawn in under the planted feet.
            Spawn("Dash charge ground shadow", glow, feet + Vector2.up * .05f, 3.6f, new Color(.02f, .08f, .25f, .5f), w + .1f, false, 24).scale.y *= .3f;
            GroundGlow(feet + Vector2.up * .05f, 3.2f, Alpha(cyan, .75f), w + .1f);
            var floor = BladeFlatPlane(c.anchor, feet + Vector2.up * .08f, .28f, w + .05f);
            var vortexInk = BladeOnPlane(floor, "Dash vortex shadow", swirl, 3.5f, BladeNavyInk, w + .05f, 25, false, -760 * f);
            var vortex = BladeOnPlane(floor, "Dash vortex", swirl, 3.3f, Alpha(cyan, .9f), w + .05f, 27, true, -760 * f);
            foreach (var clip in new[] { vortexInk, vortex }) { clip.startScale = .5f; clip.endScale = 1.1f; clip.ease = 2; clip.hold = .85f; }
            var pull = GroundWave(feet + Vector2.up * .05f, 4.4f, Alpha(cyan, .8f), w * .9f); pull.startScale = 1; pull.endScale = .15f; pull.ease = 2; pull.fadeIn = .08f;
            // Speed lines rush in from both sides and slip past the body, crowding in as the dash nears.
            int lines = BladeShare(12);
            for (int i = 0; i < lines; i++)
            {
                float side = i % 3 == 0 ? -f : f, y = pos.y + Range(-.9f, 1f), x = pos.x + side * Range(2.2f, 4f), len = Range(1, 2.4f);
                var line = BladeCutLine(new Vector2(x + side * len, y), new Vector2(x, y), Range(.08f, .17f), i % 3 == 1 ? Color.white : Alpha(cyan, .95f), Range(.16f, .24f), 128);
                line.velocity = new Vector2(-side * Range(9, 14), 0); line.fadeIn = .03f; line.fadePower = 1.4f;
                BladeDelay(line, w * .75f * Mathf.Sqrt(Range(0, 1)));
            }
            Converge(hip, Mathf.RoundToInt(12 * BladeLevel(.85f, .7f)), 2.1f, cyan, w, .5f);
            int dusts = BladeShare(5);
            for (int i = 0; i < dusts; i++)
            {
                var puff = Puff(feet + new Vector2(-f * Range(.1f, .5f), .12f), Range(.4f, .65f), BladeDust, new Vector2(-f * Range(1.5f, 3), Range(.3f, .9f)), Range(.3f, .45f), 118);
                BladeDelay(puff, Range(0, w * .5f));
            }
            // Release: a horizontal glare and a sonic ring as the body launches; the floor cracks under the push-off and dust
            // blasts out behind it.
            var glare = Spawn("Dash launch glare", streak, pos + new Vector2(f * .6f, 0), 6f, new Color(1, 1, 1, .95f), .14f, true, 131);
            glare.scale.y *= .5f; glare.startScale = .4f; glare.endScale = 1.2f; glare.ease = 3; glare.fadePower = 1.5f; BladeDelay(glare, release);
            var boomInk = Spawn("Dash sonic ring shadow", ring, pos + new Vector2(f * .45f, 0), 3f, BladeNavyInk, .24f, false, 118);
            var boom = Spawn("Dash sonic ring", ring, pos + new Vector2(f * .5f, 0), 2.9f, Alpha(cyan, .95f), .22f, true, 130);
            foreach (var clip in new[] { boomInk, boom }) { clip.scale.x *= .38f; clip.startScale = .3f; clip.endScale = 1.1f; clip.ease = 3; clip.fadePower = 1.3f; BladeDelay(clip, release); }
            var kickInk = Decal(feet + Vector2.up * .02f, crack, 2.8f, new Color(.05f, .07f, .16f, .85f), .9f, false, 25); BladeDelay(kickInk, release);
            var kickGlow = Decal(feet + Vector2.up * .03f, crack, 2.8f, Alpha(cyan, 1), .55f, true, 26); kickGlow.fadePower = 1.8f; BladeDelay(kickGlow, release);
            var kickRing = GroundWave(feet + Vector2.up * .05f, 3.4f, Alpha(cyan, .9f), .3f); BladeDelay(kickRing, release);
            int kicks = BladeShare(4);
            for (int i = 0; i < kicks; i++)
            {
                var puff = Puff(feet + new Vector2(-f * Range(0, .5f), .2f), Range(.6f, .9f), BladeDust, new Vector2(-f * Range(3, 6), Range(.6f, 1.6f)), Range(.4f, .6f), 118);
                puff.endScale = 2.2f; BladeDelay(puff, release);
            }
        }

        private void ImpactDashStrike(Vector2 pos, float radius, Color tint, int facing)
        {
            // Without a facing this is the awakened crossing cut that lands where the dash ended.
            if (facing == 0) { BladeCrossCut(pos, radius, tint); return; }
            int f = facing < 0 ? -1 : 1, grade = Grade;
            float R = Mathf.Clamp(radius, 1, 7f), k = Mathf.Clamp(R / 4.5f, .3f, 1.1f), lift = BladeLevel(1.15f, 1.3f);
            Color cyan = Color.Lerp(BladeCyan, tint, .25f), navy = new Color(.03f, .12f, .4f, .85f);
            float gy = BladeGroundY(pos, 1), span = Mathf.Min(2 * R, 8.6f * BladeLevel(1.12f, 1.25f)), tilt = Range(-4, 4), height = span * .34f;
            Vector2 center = pos + new Vector2(f * span * .22f, -.1f), along = Direction(tilt) * f;
            Vector2 a0 = center - along * span * .5f, a1 = center + along * span * .5f;
            // Iaido: the flash cut lands now; the sheath clicks home at burstAt and every cut left hanging in the air bursts at once.
            // An awakened dash's crossing cut lands 0.2 s after the dash, so its sheath clicks early (0.13 s): three clean beats,
            // the dash cut, the sheath burst, then the X.
            float burstAt = grade >= 3 ? .13f : .46f, spacing = grade >= 3 ? .02f : .03f;
            // Impact frame: for a breath the whole cut darkens (over the actors), so one white line splits a navy world:
            // a navy band, a cyan glow band and a white razor across the whole reach.
            var frame = BladeBackShade(center + Vector2.up * .2f, span * 1.2f, new Color(.03f, .08f, .32f, .8f), .2f, .36f);
            frame.renderer.sortingOrder = 112; frame.fadeIn = 0; frame.fadePower = 2.5f;
            var band = BladeInk(Line(a0 - along * .3f, a1 + along * .3f, 2.3f * lift, new Color(.03f, .12f, .4f, .9f), .36f, 117));
            var glowBand = Line(a0, a1, .95f * lift, Alpha(cyan, 1), .3f, 129);
            var razor = BladeCutLine(a0 - along * .4f, a1 + along * .6f, .32f * lift, Color.white, .22f, 131);
            var cutLayers = new List<Clip> { band, glowBand, razor };
            if (grade >= 3)
            {
                // Awakened: a gold-white edge runs the whole cut, over a second, wider glow band.
                cutLayers.Add(BladeCutLine(a0 - along * .5f, a1 + along * .7f, .2f, Alpha(BladeAwakeGold, 1), .3f, 132));
                cutLayers.Add(Line(a0, a1, 1.7f, Alpha(cyan, .45f), .34f, 128));
            }
            foreach (var clip in cutLayers) { clip.axisStart = new Vector2(.15f, 1); clip.axisEnd = Vector2.one; clip.ease = 9; clip.fadePower = 1.5f; }
            var thread = BladeCutLine(a0, a1 + along * .3f, .1f, Alpha(cyan, .9f), .75f, 128); thread.hold = .35f; thread.fadePower = 1.4f;
            // A wide crescent swoosh rides just above the line.
            var ink = BladeSlashAt(center, span * .96f, height * 1.1f, tilt, new Color(.03f, .12f, .4f, .8f), .38f, false, 116);
            var body = BladeSlashAt(center, span * .92f, height, tilt, Alpha(cyan, .95f), .3f, true, 128);
            var edge = BladeSlashAt(center, span * .9f, height, tilt, new Color(1, 1, 1, .75f), .18f, true, 130);
            BladeSnap(ink, 0); BladeSnap(body, 0); BladeSnap(edge, 0);
            var glare = Spawn("Flash cut glare", streak, center, span * 1.15f, new Color(1, 1, 1, .9f), .16f, true, 132);
            glare.scale.y *= .35f; glare.startScale = .5f; glare.endScale = 1.1f; glare.ease = 3; glare.fadePower = 1.6f;
            Flash(pos + new Vector2(f * .7f, .1f), 2.6f * k + .6f, new Color(.9f, 1, 1, .9f), .16f);
            ImpactLines(center + Vector2.up * .3f, R * 2.2f, new Color(.85f, 1, 1, .8f), .22f);
            Glint(pos + new Vector2(f * .8f, .15f), 2.4f * k + .6f, Color.white, .24f);
            Wave(center + Vector2.up * .3f, R * 1.3f, Alpha(cyan, .8f), .3f, 3);
            // The strike's reach on the floor: a ring centred where the dash ends, its rim on the gameplay radius.
            GroundWave(new Vector2(pos.x, gy + .05f), R * 2 / .74f, Alpha(cyan, .85f), .4f);
            if (R >= 3) ScreenFlash(center, new Color(.8f, .95f, 1, .12f), .08f);
            int sparks = BladeShare(Mathf.RoundToInt(8 + 6 * k));
            for (int i = 0; i < sparks; i++)
            {
                Vector2 at = center + new Vector2(Range(-.5f, .5f) * span, Range(0, .5f));
                Spark(at, new Vector2(f * Range(4, 11), Range(-1.5f, 2.5f)), i % 3 == 0 ? Color.white : cyan, Range(.22f, .42f), Range(.25f, .45f), 4, 2.2f);
            }
            // Hanging cuts: thin lines of light snap open one after another across the area and hang there, trembling, until the
            // sheath clicks; then each splits open into a band of cyan light with a flash, a star, sparks and drifting motes.
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
                var shadow = BladeCutLine(mid - half * 1.08f, mid + half * 1.08f, 1f, navy, hang + .05f, 118, false);
                var glowCut = BladeCutLine(mid - half * 1.04f, mid + half * 1.04f, .5f, Alpha(cyan, 1), hang + .03f, 129);
                var cut = BladeCutLine(mid - half, mid + half, .13f, Color.white, hang + .02f, 130);
                foreach (var clip in new[] { shadow, glowCut, cut }) { clip.axisStart = new Vector2(.08f, 1); clip.axisEnd = Vector2.one; clip.ease = 14; clip.hold = .9f; clip.fadePower = 2; BladeDelay(clip, appear); }
                glowCut.flicker = .2f;
                var tip = Glint(mid + half * .9f, .9f, Color.white, .12f, a); BladeDelay(tip, appear);
                var blastInk = BladeCutLine(mid - half * 1.2f, mid + half * 1.2f, .7f, navy, .26f, 117, false);
                var blast = BladeCutLine(mid - half * 1.15f, mid + half * 1.15f, .55f, Alpha(cyan, 1), .22f, 131);
                var blastCore = BladeCutLine(mid - half * 1.1f, mid + half * 1.1f, .2f, Color.white, .15f, 132);
                blastInk.axisEnd = new Vector2(1.1f, 3); blast.axisEnd = new Vector2(1.08f, 4); blastCore.axisEnd = new Vector2(1.05f, 2.5f);
                foreach (var clip in new[] { blastInk, blast, blastCore }) { clip.axisStart = Vector2.one; clip.ease = 3; clip.fadePower = 1.6f; BladeDelay(clip, pop); }
                if (grade < 2) { var flash = Flash(mid, .9f * (.6f + .4f * k), Alpha(cyan, .6f), .12f); BladeDelay(flash, pop); }
                var star = Glint(mid + half * Range(-.4f, .4f), 1.7f * (.6f + .4f * k), Color.white, .22f, a); BladeDelay(star, pop);
                // The scar of the cut lingers a moment after it bursts.
                var after = BladeCutLine(mid - half, mid + half, .1f, Alpha(cyan, .85f), .55f, 129); after.hold = .25f; after.fadePower = 1.3f; BladeDelay(after, pop + .08f);
                for (int j = 0; j < (grade >= 2 ? 2 : 3); j++)
                {
                    var bit = Spark(mid + half * Range(-.8f, .8f), Direction(a + 90 + Range(-25, 25) + (j % 2 == 0 ? 0 : 180)) * Range(4, 9), j == 0 ? Color.white : cyan, Range(.2f, .36f), Range(.22f, .36f), 4, 2.5f);
                    BladeDelay(bit, pop);
                }
                for (int j = 0; j < (grade >= 2 ? 1 : 2); j++)
                {
                    var mote = Spawn("Cut mote", glow, mid + half * Range(-.8f, .8f), Range(.22f, .38f), Alpha(cyan, .95f), Range(.7f, 1.05f), true, 131);
                    mote.velocity = new Vector2(Range(-.3f, .3f), Range(.5f, 1.3f)); mote.drag = .6f; mote.flicker = .45f; mote.endScale = .3f; mote.ease = 1; mote.hold = .3f;
                    BladeDelay(mote, pop + .05f);
                }
            }
            // The sheath click: a glint and a ring at the hip, then a wide ring and a last burst of impact lines over the cut area.
            Vector2 sheath = pos + new Vector2(-f * .2f, -.25f);
            var click = Glint(sheath, 1.5f, Color.white, .18f); BladeDelay(click, burstAt - .03f);
            var clickRing = Wave(sheath, 1.6f, Alpha(cyan, .9f), .2f, 3); BladeDelay(clickRing, burstAt - .03f);
            // (Awakened, the crossing cut brings its own burst a beat later, so this one stays a ring without the white lines.)
            var finale = Wave(center + Vector2.up * .4f, R * 2f, Alpha(cyan, grade >= 3 ? .5f : .75f), .34f, 3); BladeDelay(finale, burstAt);
            if (grade < 3) { var finaleLines = ImpactLines(center + Vector2.up * .4f, R * 2.4f, new Color(.85f, 1, 1, .75f), .24f); BladeDelay(finaleLines, burstAt); }
            // Follow-through: a scar of light on the floor under the cut, wind and dust swept forward and a fading crescent.
            var scarInk = BladeCutLine(new Vector2(a0.x, gy + .04f), new Vector2(a1.x, gy + .04f), .3f, new Color(.03f, .1f, .3f, .45f), 1.3f, 24, false);
            var scar = BladeCutLine(new Vector2(a0.x, gy + .05f), new Vector2(a1.x, gy + .05f), .14f, Alpha(cyan, .95f), 1.1f, 26);
            foreach (var clip in new[] { scarInk, scar }) { clip.axisStart = new Vector2(.2f, 1); clip.axisEnd = Vector2.one; clip.ease = 9; clip.hold = .5f; clip.fadePower = 1.5f; }
            int dusts = BladeShare(6);
            for (int i = 0; i < dusts; i++)
            {
                var puff = Puff(new Vector2(pos.x + f * Range(-.3f, 1.5f) * R * .5f, gy + .15f), Range(.55f, .95f), BladeDust, new Vector2(f * Range(1.5f, 4), Range(.3f, 1)), Range(.5f, .8f), 118);
                puff.endScale = 2.2f;
            }
            var linger = BladeSlashAt(center, span * .92f, height, tilt, Alpha(cyan, .45f), .8f, true, 127); linger.fadePower = 1.6f; BladeDelay(linger, .12f);
        }

        /// <summary>The awakened crossing cut where a dash ended (facing 0), drawn at the moment it strikes: two long diagonal cuts
        /// snap open one after the other into a tall X standing on the floor, its feet on the burst's reach and its crossing above
        /// the hero's head; a cyan star flares where they cross, each cut splits open into a band of cyan light within a tenth of
        /// a second (with the damage), and an X scar is left on the floor.</summary>
        private void BladeCrossCut(Vector2 pos, float radius, Color tint)
        {
            float r = Mathf.Clamp(radius, 1, 6), k = Mathf.Clamp(r / 4, .4f, 1.2f);
            Color cyan = Color.Lerp(BladeCyan, tint, .25f), navy = new Color(.03f, .12f, .4f, .85f);
            float gy = BladeGroundY(pos, .8f), slope = 30, reach = Mathf.Min(r * .92f, 5.2f), half = reach / Mathf.Cos(slope * Mathf.Deg2Rad);
            float rise = half * Mathf.Sin(slope * Mathf.Deg2Rad);
            // The X stands on the floor: its lower tips touch the ground at the reach on both sides.
            Vector2 center = new Vector2(pos.x, gy + Mathf.Max(1.05f, rise - .05f));
            BladeBackShade(center, r * 2.1f, new Color(.02f, .07f, .3f, .5f), .4f, .55f);
            for (int i = 0; i < 2; i++)
            {
                float angle = i == 0 ? slope : -slope, appear = i * .05f, pop = .1f + i * .04f;
                Vector2 d = Direction(angle) * half, a = center - d, b = center + d;
                var ink = BladeCutLine(a - d * .05f, b + d * .05f, 1.6f * k + .35f, navy, pop - appear + .04f, 117, false);
                var glowCut = BladeCutLine(a, b, .85f * k + .2f, Alpha(cyan, 1), pop - appear + .03f, 129);
                var razor = BladeCutLine(a + d * .03f, b - d * .03f, .24f, Color.white, pop - appear + .02f, 131);
                foreach (var clip in new[] { ink, glowCut, razor }) { clip.axisStart = new Vector2(.06f, 1); clip.axisEnd = Vector2.one; clip.ease = 12; clip.hold = .85f; clip.fadePower = 2; BladeDelay(clip, appear); }
                // A curved swoosh rides each cut so it has body, not just a line.
                var arcInk = BladeSlashAt(center, half * 1.95f, r * .5f, angle, new Color(.03f, .12f, .4f, .7f), .3f, false, 116);
                var arc = BladeSlashAt(center, half * 1.9f, r * .45f, angle, Alpha(cyan, .85f), .26f, true, 128);
                if (i == 1) { arcInk.scale.y *= -1; arc.scale.y *= -1; }
                BladeSnap(arcInk, appear); BladeSnap(arc, appear);
                var tip = Glint(b, 1.2f * k + .4f, Color.white, .16f, angle); BladeDelay(tip, appear + .02f);
                int sparks = BladeShare(4);
                for (int j = 0; j < sparks; j++)
                {
                    var bit = Spark(center + d * Range(-.9f, .9f), Direction(angle + (j % 2 == 0 ? 90 : -90) + Range(-25, 25)) * Range(4, 9) * k, j % 3 == 0 ? Color.white : cyan, Range(.22f, .36f), Range(.22f, .36f), 5, 2.5f);
                    BladeDelay(bit, appear + .02f);
                }
                // The cut splits open: a band of light swelling across its width, then gone.
                var blastInk = BladeCutLine(a - d * .1f, b + d * .1f, .7f, navy, .26f, 117, false);
                var blast = BladeCutLine(a - d * .08f, b + d * .08f, .55f, Alpha(cyan, 1), .22f, 130);
                var blastCore = BladeCutLine(a - d * .05f, b + d * .05f, .2f, Color.white, .15f, 132);
                blastInk.axisEnd = new Vector2(1.08f, 3.2f); blast.axisEnd = new Vector2(1.06f, 4); blastCore.axisEnd = new Vector2(1.04f, 2.5f);
                foreach (var clip in new[] { blastInk, blast, blastCore }) { clip.axisStart = Vector2.one; clip.ease = 3; clip.fadePower = 1.6f; BladeDelay(clip, pop); }
            }
            // Where the cuts cross: a cyan star and a ring (the awakening overlay brings the white flash and crown here).
            var star = Glint(center, 2.2f * k + .5f, Alpha(cyan, 1), .26f, 0); star.spin = 0; BladeDelay(star, .05f);
            var air = Wave(center, r * 1.4f, Alpha(cyan, .75f), .3f, 3); BladeDelay(air, .1f);
            // The reach on the floor and an X scar left in it (two crossing seams in perspective).
            var reachInk = BladeInk(Wave(new Vector2(pos.x, gy + .04f), r * 2 / .74f * 1.02f, navy, .5f, 3)); reachInk.scale.y *= .3f; reachInk.renderer.sortingOrder = 115;
            GroundWave(new Vector2(pos.x, gy + .05f), r * 2 / .74f, Alpha(cyan, .9f), .45f);
            for (int i = 0; i < 2; i++)
            {
                float lean = i == 0 ? .14f : -.14f;
                Vector2 s0 = new Vector2(pos.x - r * .8f, gy + .03f - lean), s1 = new Vector2(pos.x + r * .8f, gy + .03f + lean);
                var scarInk = BladeCutLine(s0, s1, .32f, new Color(.03f, .1f, .3f, .5f), 1.2f, 24, false);
                var scar = BladeCutLine(s0, s1, .13f, Alpha(cyan, .95f), 1f, 26);
                foreach (var clip in new[] { scarInk, scar }) { clip.axisStart = new Vector2(.1f, 1); clip.axisEnd = Vector2.one; clip.ease = 9; clip.hold = .45f; clip.fadePower = 1.5f; BladeDelay(clip, i * .07f); }
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
            Color cyan = Color.Lerp(BladeCyan, tint, .25f);
            int grade = Grade; float lift = BladeLevel(1.15f, 1.3f);
            // The blazing ribbon: a navy shadow band under a cyan light band, a white-hot cut and a thin scar that lingers.
            // Higher levels widen it; awakened, a gold-white edge runs its length.
            var ink = BladeCutLine(from - d * .3f, to + d * .5f, 2.1f * lift, BladeNavyInk, .34f, 117, false); ink.fadePower = 1.3f;
            var ribbon = Line(from - d * .2f, to + d * .2f, 1.05f * lift, Alpha(cyan, .85f), .3f, 128); ribbon.fadePower = 1.3f;
            var hot = BladeCutLine(from, to + d * .5f, .34f * lift, Color.white, .2f, 130); hot.fadePower = 1.5f;
            var linger = BladeCutLine(from, to, .1f, Alpha(cyan, .9f), .7f, 129); linger.hold = .35f; linger.fadePower = 1.4f;
            if (grade >= 3) { var gilt = BladeCutLine(from + n * .22f, to + d * .4f + n * .22f, .12f, Alpha(BladeAwakeGold, 1), .26f, 131); gilt.fadePower = 1.4f; }
            // Wind-cut lines slip along the ribbon and past it.
            int cuts = BladeShare(Grade >= 3 ? 9 : 12);
            for (int i = 0; i < cuts; i++)
            {
                Vector2 a = Vector2.Lerp(from, to, Range(-.1f, .7f)) + n * Range(-1, 1) * Range(.3f, 1.1f);
                var cut = BladeCutLine(a, a + d * Range(1, 2.4f), Range(.04f, .11f), i % 3 == 0 ? Color.white : Alpha(cyan, .9f), Range(.14f, .26f), 129);
                cut.velocity = d * Range(5, 10); cut.fadePower = 1.4f; BladeDelay(cut, Range(0, .06f));
            }
            // Sonic rings strung along the path.
            for (int i = 0; i < 2; i++)
            {
                Vector2 at = Vector2.Lerp(from, to, .15f + i * .35f);
                var shadow = Spawn("Dash sonic ring shadow", ring, at - d * .08f, 2.3f - i * .3f, BladeNavyInk, .26f, false, 118);
                var boom = Spawn("Dash sonic ring", ring, at, 2.2f - i * .3f, Alpha(cyan, .9f), .24f, true, 130);
                foreach (var clip in new[] { shadow, boom }) { clip.scale.x *= .34f; clip.angle = angle; clip.startScale = .35f; clip.endScale = 1.15f; clip.ease = 3; clip.fadePower = 1.3f; BladeDelay(clip, i * .04f); }
            }
            // Ghost glows of the body along the path, fading from the start.
            for (int i = 0; i < 4; i++)
            {
                var ghost = Spawn("Dash ghost light", glow, Vector2.Lerp(from, to, .15f + i * .22f), 1.1f, Alpha(cyan, .5f), .18f + i * .04f, true, 127);
                ghost.scale.y *= 1.6f; ghost.fadePower = 1.4f;
            }
            // Sparks thrown off the ribbon and dust kicked from the floor beneath it.
            int sparks = BladeShare(12);
            for (int i = 0; i < sparks; i++)
            {
                Vector2 at = Vector2.Lerp(from, to, Range(0, 1)) + n * Range(-.35f, .35f);
                Spark(at, -d * Range(1, 4) + n * Range(-4, 4) + Vector2.up * Range(0, 2), i % 3 == 0 ? Color.white : cyan, Range(.16f, .3f), Range(.2f, .4f), 6, 2);
            }
            float gy = BladeGroundY(from, .95f);
            int dusts = BladeShare(Mathf.Clamp(Mathf.RoundToInt(length * 1.3f), 2, 8));
            for (int i = 0; i < dusts; i++)
            {
                float t = (i + .5f) / dusts;
                var dust = Puff(new Vector2(Mathf.Lerp(from.x, to.x, t), gy + .12f), Range(.45f, .75f), BladeDust, new Vector2(d.x * Range(.8f, 2.2f), Range(.4f, 1.2f)), Range(.4f, .6f), 118);
                BladeDelay(dust, t * .05f);
            }
            Flash(from, 2, new Color(.85f, 1, 1, .75f), .12f);
        }
    }
}
