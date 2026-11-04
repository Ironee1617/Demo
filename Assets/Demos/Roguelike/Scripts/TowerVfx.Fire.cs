using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Fire family: Fireball and Meteor, painted fire.
    // Every fire shape is a light level with its hue baked in (red at the thin edge, orange body, yellow toward the core),
    // which the painted material runs from crimson through orange and pale yellow to a white-hot core inside a crisp edge,
    // with a soft bloom glowing under the bright parts. Explosions billow and tear apart frame by frame (the shared blast
    // flipbook, often a second lobe bursting off the first a beat later), hits flare as drawn sparks, smoke is warm grey
    // billows, and stones and debris are painted rock lit from the upper left. Nothing is outlined: where fire needs contrast
    // on a pale sky, a deep red body no wider than its light sits under it, or a soft dark shade behind the actors.
    public sealed partial class TowerVfx
    {
        // A ball of fire with licking tongues and a flame tongue standing on its root (looped sheets), a comet (pivot on the
        // head, looped), painted rocks (two plain, two molten), a painted molten meteor stone with its heat as light, and a
        // fire seal of crisp painted strokes.
        private static Sprite[] FireBallSprites, FireTongueSprites, FireCometSprites, FireRockSprites;
        private static Sprite FireStoneSprite, FireStoneGlowSprite, FireSealSprite;
        // A hemispherical shock front standing on the ground (pivot at the bottom centre) for ground blasts.
        private static Sprite FireDomeSprite;

        // From a meteor's landing point back up to where it enters the sky (low enough that the sky seal stays on screen).
        private static readonly Vector2 FireMeteorPath = new Vector2(-2.6f, 6.9f);
        private static readonly Color FireHotTone = new Color(1, .95f, .78f), FireEmberTone = new Color(1, .48f, .12f),
            // A deep saturated red for the few contrast bodies (never wider than their light), and the warm grey smoke.
            FireInkTone = new Color(.6f, .1f, .03f, 1), FireCharTone = new Color(.4f, .32f, .28f, .8f);

        private static void BuildFireTextures()
        {
            FireBallSprites = new Sprite[6];
            for (int f = 0; f < FireBallSprites.Length; f++)
            {
                float phase = f / 6f * Mathf.PI * 2;
                FireBallSprites[f] = Build("Fire ball " + f, 128, 128, (u, v) =>
                {
                    // A round body of fire with seven tongues licking out of it; they sway and breathe over the loop.
                    float r = Mathf.Sqrt(u * u + v * v); if (r > .99f) return Color.clear;
                    float th = Mathf.Atan2(v, u), reach = .5f + .025f * Mathf.Sin(th * 3 + phase);
                    for (int k = 0; k < 7; k++)
                    {
                        uint h = Hash(k * 37 + 11);
                        float angle = k * .8976f + ((h & 255) / 255f - .5f) * .6f + .22f * Mathf.Sin(phase + k * 2.1f);
                        float length = .16f + (h >> 8 & 255) / 255f * .22f + .07f * Mathf.Sin(phase + k * 1.7f), width = .3f + (h >> 16 & 255) / 255f * .22f;
                        float d = Mathf.Abs(Mathf.Repeat(th - angle + Mathf.PI, 2 * Mathf.PI) - Mathf.PI);
                        reach = Mathf.Max(reach, .5f + length * Mathf.Pow(Mathf.Max(0, 1 - d / width), 1.7f));
                    }
                    float inside = 1 - Smooth(reach - .014f, reach + .004f, r); if (inside <= 0) return Color.clear;
                    float x = Mathf.Clamp01(r / reach), n = Fbm(Mathf.Cos(th) * 4.5f + r * 1.3f + phase * .16f, Mathf.Sin(th) * 4.5f - r * 1.1f + 7, 2);
                    float level = (.13f + 1.1f * Mathf.Pow(1 - x, .7f)) * (.87f + .32f * (n - .5f)) * (1 - .3f * Smooth(.48f, .9f, r));
                    return FireHue(level * inside);
                });
            }

            FireTongueSprites = new Sprite[6];
            for (int f = 0; f < FireTongueSprites.Length; f++)
            {
                float phase = f / 6f * Mathf.PI * 2;
                FireTongueSprites[f] = Build("Fire tongue " + f, 64, 160, (u, v) =>
                {
                    // A tongue of flame on a round root, swaying more toward its pointed tip, with licking notches along its
                    // sides and streaks running up it: white-hot at the root, red at the tip.
                    float h = (v + 1) * .5f;
                    float sway = (.2f * Mathf.Sin(phase + h * 3) + .08f * Mathf.Sin(phase * 2 + h * 7 + 1)) * h * h;
                    float w = h < .18f ? .72f * Mathf.Sqrt(Mathf.Max(0, 1 - Sq((h - .18f) / .17f))) : .72f * Mathf.Pow(Mathf.Max(0, 1 - (h - .18f) / .8f), 1.3f);
                    w *= 1 + .14f * Mathf.Sin(h * 15 - phase * 2) * Smooth(.3f, .6f, h);
                    if (w <= .005f) return Color.clear;
                    float d = Mathf.Abs(u - sway) / w, inside = 1 - Smooth(.94f, 1.02f, d); if (inside <= 0) return Color.clear;
                    float n = Fbm(u * 8 + phase * .25f + 3, h * 2.4f - phase * .3f + 5, 2);
                    float level = (.13f + 1.05f * Mathf.Pow(Mathf.Max(0, 1 - d), .7f)) * (1.08f - .75f * h) * (.87f + .32f * (n - .5f));
                    return FireHue(level * inside);
                }, new Vector2(.5f, .08f));
            }

            FireCometSprites = new Sprite[4];
            for (int f = 0; f < FireCometSprites.Length; f++)
            {
                float phase = f / 4f * Mathf.PI * 2;
                FireCometSprites[f] = Build("Fire comet " + f, 256, 96, (u, v) =>
                {
                    // A banded head and a tapered tail with licking edges; streaks run along the tail and split it into
                    // strands toward its end.
                    const float head = .64f, radius = .21f, aspect = 96f / 256f;
                    float y = v * aspect, behind = head - u, t = Mathf.Clamp01(behind / (head + 1)), d, w = radius;
                    if (behind <= 0) d = Mathf.Sqrt(behind * behind + y * y) / radius;
                    else
                    {
                        w = radius * (1 + .45f * Mathf.Sqrt(t)) * Mathf.Pow(1 - t, 1.1f);
                        w *= 1 + .2f * Mathf.Sin(t * 24 - phase) * Smooth(.06f, .25f, t);
                        float wob = Mathf.Sin(t * 6.5f + phase) * .3f * w * Smooth(0, .4f, t);
                        d = Mathf.Abs(y - wob) / Mathf.Max(.003f, w);
                    }
                    float inside = 1 - Smooth(.95f, 1.02f, d); if (inside <= 0) return Color.clear;
                    float n = Fbm(t * 2.6f + f * .37f + 3, y / Mathf.Max(.02f, w) * 2.2f + 9, 2), gap = Smooth(.2f, .75f, t) * Smooth(.52f, .62f, n);
                    float level = (.13f + 1.08f * Mathf.Pow(Mathf.Max(0, 1 - d), .72f)) * (1.06f - .85f * t) * (.86f + .34f * (n - .5f)) * (1 - gap);
                    return FireHue(level * inside * (1 - Smooth(.9f, 1, Mathf.Abs(u))));
                }, new Vector2(.82f, .5f));
            }

            FireDomeSprite = Build("Fire shock dome", 256, 128, (u, v) =>
            {
                // A painted shock front standing on the ground: a hard bright front with a streaky tail fraying inward.
                float y = (v + 1) * .5f, r = Mathf.Sqrt(u * u + y * y); if (r > .92f) return Color.clear;
                float th = Mathf.Atan2(y, u), edge = .86f;
                float outside = 1 - Smooth(edge - .014f, edge + .004f, r), depth = Mathf.Max(0, edge - r);
                float streaks = RingNoise(th, r, 1.6f, 14), reach = .1f + .2f * RingNoise(th, 0, 3.2f, 0);
                return White(outside * Mathf.Exp(-depth / reach) * (.8f + .5f * (streaks - .5f)) * Smooth(0, .04f, y));
            }, new Vector2(.5f, 0));

            // The meteor is a painted molten stone, with a light copy of its heat (veins and the leading face) that glows over it.
            float unused;
            FireStoneSprite = Build("Fire meteor stone", 192, 192, (u, v) => FireRockSample(u, v, 101, true, out unused));
            FireStoneGlowSprite = Build("Fire meteor stone heat", 192, 192, (u, v) => { float heat; var c = FireRockSample(u, v, 101, true, out heat); return White(c.a * heat); });
            FireRockSprites = new Sprite[4];
            for (int i = 0; i < FireRockSprites.Length; i++)
            {
                int seed = 211 + i * 17; bool molten = i >= 2;
                FireRockSprites[i] = Build("Fire rock " + i, 64, 64, (u, v) => FireRockSample(u, v, seed, molten, out unused));
            }

            // A fire seal of crisp strokes: two rings, twelve flame glyphs pointing outward, a hexagram and a small sun.
            FireSealSprite = Build("Fire seal", 384, 384, (u, v) => White(FireSealLevel(u, v)));
            // The skill-level crown has no texture hook of its own: build it here, at load, so a first level-three cast
            // does not build it mid-fight.
            GradeEnsure();
        }

        /// <summary>The fire hue for a light level, blended: red at the thin edge, orange body, yellow toward the core.</summary>
        private static Color FireHue(float level)
        {
            level = Mathf.Clamp01(level);
            Color c = Color.Lerp(new Color(1, .28f, .06f), new Color(1, .56f, .12f), Smooth(.12f, .45f, level));
            c = Color.Lerp(c, new Color(1, .86f, .4f), Smooth(.5f, .85f, level));
            c.a = level; return c;
        }

        /// <summary>A painted stone: a lumpy round silhouette, soft light from the upper left, pitted craters and a shadowed
        /// rim; a molten one has glowing lava veins and a white-hot face on its lower right, which leads its fall.
        /// <paramref name="heat"/> is how hot the point burns (0 to 1), which the glow copy draws as light.</summary>
        private static Color FireRockSample(float u, float v, int seed, bool molten, out float heat)
        {
            heat = 0;
            float r = Mathf.Sqrt(u * u + v * v), th = Mathf.Atan2(v, u);
            float rr = .78f + .26f * (RingNoise(th, seed * .37f, 1.5f, 0) - .5f) + .05f * (RingNoise(th, seed, 4.5f, 0) - .5f);
            float cover = 1 - Smooth(rr - .025f, rr + .005f, r); if (cover <= 0) return Color.clear;
            float x = u / rr, y = v / rr, z = Mathf.Sqrt(Mathf.Max(0, 1 - x * x - y * y));
            float n = Fbm(u * 3.2f + seed, v * 3.2f - seed, 4), pits = Fbm(u * 6.5f + seed * 2, v * 6.5f + seed, 3);
            float diffuse = Mathf.Clamp01(-x * .55f + y * .62f + z * .58f);
            float light = (.2f + .8f * diffuse) * (.78f + .45f * (n - .5f));
            float crater = Smooth(.58f, .66f, pits);
            light = light * (1 - crater * .38f) + Gauss(pits - .57f, .018f) * .14f * diffuse;
            light *= 1 - .32f * Smooth(rr - .24f, rr, r) * (1 - diffuse * .6f);
            Color c = Color.Lerp(new Color(.13f, .08f, .065f), new Color(.66f, .5f, .4f), Mathf.Clamp01(light));
            if (molten)
            {
                float vein = Gauss(Fbm(u * 2.6f + seed * 3, v * 2.6f + 11, 3) - .5f, .032f) * Smooth(.12f, .45f, r / rr);
                float face = Mathf.Clamp01(x * .6f - y * .8f + .15f);
                heat = Mathf.Clamp01(vein * (.45f + .8f * face) + Smooth(.62f, .98f, r / rr) * face * .95f + crater * face * .35f);
                c = Color.Lerp(c, Color.Lerp(new Color(.82f, .18f, .03f), new Color(1, .84f, .45f), Mathf.Clamp01(heat * 1.4f - .3f)), Mathf.Clamp01(heat * 1.2f));
            }
            c.a = cover; return c;
        }

        /// <summary>One crisp painted stroke: a hard edge, a thin deep rim, then rising to <paramref name="peak"/> on its spine.</summary>
        private static float FireStroke(float distance, float half, float peak)
        {
            if (distance >= half + .003f) return 0;
            float s = Mathf.Clamp01(1 - distance / half);
            return (.13f + (peak - .13f) * Mathf.Pow(s, .55f)) * (1 - Smooth(half - .005f, half + .003f, distance));
        }

        private static float FireSealLevel(float u, float v)
        {
            const float grow = 0;
            float r = Mathf.Sqrt(u * u + v * v); if (r > .97f) return 0;
            float deg = Mathf.Repeat(Mathf.Atan2(v, u) * Mathf.Rad2Deg, 360), level = 0;
            level = Mathf.Max(level, FireStroke(Mathf.Abs(r - .885f), .017f + grow, .95f));
            level = Mathf.Max(level, FireStroke(Mathf.Abs(r - .825f), .008f + grow, .72f));
            level = Mathf.Max(level, FireStroke(Mathf.Abs(r - .52f), .013f + grow, .9f));
            level = Mathf.Max(level, FireStroke(Mathf.Abs(r - .475f), .007f + grow, .7f));
            if (r > .82f && r < .9f) level = Mathf.Max(level, FireStroke(Mathf.Abs(Mathf.Repeat(deg + 15, 30) - 15) * Mathf.Deg2Rad * r, .008f + grow, .8f));
            // Twelve flame glyphs: a hard outline with a small hot tongue inside (left open, so the seal never reads as a wheel).
            float hh = (r - .545f) / .25f;
            if (hh > -.05f && hh < 1.05f)
            {
                float s = (Mathf.Repeat(deg, 30) - 15) * Mathf.Deg2Rad * r + Mathf.Sin(hh * 5.5f) * .018f * hh;
                float half = .072f * Mathf.Pow(Mathf.Clamp01(1 - hh), .75f) * Mathf.Sqrt(Mathf.Clamp01(hh / .18f)), a = Mathf.Abs(s);
                level = Mathf.Max(level, FireStroke(Mathf.Abs(a - half), .007f + grow, .8f));
                if (grow <= 0 && hh < .7f) level = Mathf.Max(level, FireStroke(a, half * .32f, .75f));
            }
            float seg = 1e9f;
            for (int i = 0; i < 6; i++)
            {
                float a0 = (90 + i * 120) * Mathf.Deg2Rad, a1 = (90 + (i + 1) * 120) * Mathf.Deg2Rad, turn = i < 3 ? 0 : Mathf.PI / 3;
                seg = Mathf.Min(seg, SegmentDistance(u, v, Mathf.Cos(a0 + turn) * .46f, Mathf.Sin(a0 + turn) * .46f, Mathf.Cos(a1 + turn) * .46f, Mathf.Sin(a1 + turn) * .46f));
            }
            level = Mathf.Max(level, FireStroke(seg, .008f + grow, .8f));
            level = Mathf.Max(level, FireStroke(Mathf.Abs(r - .14f), .011f + grow, .9f));
            level = Mathf.Max(level, FireStroke(r, .062f + grow, 1));
            float ray = Mathf.Abs(Mathf.Repeat(deg + 22.5f, 45) - 22.5f) * Mathf.Deg2Rad * r;
            if (r > .165f && r < .28f) level = Mathf.Max(level, FireStroke(ray, (.013f + grow) * (1 - (r - .165f) / .12f) + .002f, .85f));
            return level;
        }

        // ---------------------------------------------------------------- fire builders

        /// <summary>A flame tongue standing on its root and pointing along its +y. Lit tongues are painted fire light; unlit ones are a
        /// deep orange-red painted body (the same flame, never an outline).</summary>
        private Clip FireTongue(Vector2 pos, float width, Vector2 velocity, float life, int order, bool additive)
        {
            var clip = Spawn("Flame tongue", FireTongueSprites[0], pos, width * .62f, additive ? Color.white : new Color(1, .5f, .32f, 1), life, additive, order);
            clip.frames = FireTongueSprites; clip.fps = Range(10, 15); clip.loop = true;
            if (Chance(.5f)) clip.scale.x = -clip.scale.x;
            clip.velocity = velocity; clip.drag = 1.5f; clip.startScale = .7f; clip.endScale = 1.25f; clip.ease = 5; clip.angle = Range(-12, 12); clip.hold = .2f; clip.fadePower = 1.2f;
            return clip;
        }

        /// <summary>A painted ball of fire (looped sheet) that pops open, holds and erodes.</summary>
        private Clip FireBallClip(Vector2 pos, float width, Color color, float life, int order)
        {
            var clip = Spawn("Fire ball", FireBallSprites[0], pos, width, color, life, true, order);
            clip.frames = FireBallSprites; clip.fps = Range(12, 16); clip.loop = true; clip.angle = Range(0, 360);
            clip.startScale = .55f; clip.endScale = 1.15f; clip.ease = 5; clip.hold = .25f; clip.fadePower = 1.1f;
            return clip;
        }

        /// <summary>Holds every clip spawned since index <paramref name="first"/> back by <paramref name="delay"/> seconds, so a
        /// built-up burst (a blast with its bloom and spark, a flash) starts late as one piece.</summary>
        private void FireHoldFrom(int first, float delay)
        {
            if (delay <= 0) return;
            for (int i = first; i < clips.Count; i++) { var clip = clips[i]; clip.age -= delay; if (clip.renderer) clip.renderer.enabled = false; }
        }

        /// <summary>The blast flipbook without the white hit spark the shared Blast lays on its heart (a big blast would carry a
        /// spark most of its own width): a bloom, a deeper body swelling behind and the billowing fire, started
        /// <paramref name="delay"/> late. The caller sizes its own flash.</summary>
        private Clip FireBurst(Vector2 pos, float width, Color color, float life, int order, float delay = 0)
        {
            int first = clips.Count;
            var bloom = Spawn("Fire burst bloom", glow, pos, width * 1.3f, Alpha(color, .7f), life * .6f, true, order - 2); bloom.endScale = 1.3f; bloom.ease = 1; bloom.fadePower = 1.5f;
            var back = Spawn("Fire burst body", blastFrames[0], pos + Vector2.down * width * .04f, width * 1.12f, Alpha(Color.Lerp(color, FireInkTone, .3f), .85f), life * 1.1f, true, order - 1);
            var blast = Spawn("Fire burst", blastFrames[0], pos, width, Alpha(color, 1), life, true, order);
            foreach (var clip in new[] { back, blast }) { clip.frames = blastFrames; clip.loop = false; clip.angle = Range(-12, 12); clip.startScale = .9f; clip.endScale = 1.08f; clip.ease = 2; clip.hold = .6f; clip.fadePower = .9f; }
            back.angle = blast.angle + Range(-25, 25);
            FireHoldFrom(first, delay);
            return blast;
        }

        /// <summary>Keeps a squashed ground ring level: the shared ground wave turns its shock frames to a random angle after
        /// squashing them, which tilts the ellipse; half turns keep it flat and still vary the breakup.</summary>
        private Clip FireLevel(Clip ring) { ring.angle = Chance(.5f) ? 0 : 180; return ring; }

        /// <summary>A spark flaring at the caster's hand: the drawn spark over the actors and its bloom behind them (28), so a
        /// release lights the hero up instead of washing them out.</summary>
        private void FireHandFlash(Vector2 pos, float width, float life)
        {
            var bloom = Spawn("Fire hand bloom", glow, pos, width * 1.5f, Alpha(FireLight, .6f), life * 1.4f, true, 28); bloom.endScale = 1.4f; bloom.ease = 1; bloom.fadePower = 1.4f;
            SparkFlip(pos, width, Alpha(FireHotTone, .95f), life, 131);
        }

        /// <summary>A hit flash (bloom and the drawn spark) in white-hot fire, started <paramref name="delay"/> late.</summary>
        private Clip FireFlash(Vector2 pos, float width, float life, float delay = 0)
        {
            int first = clips.Count;
            var flash = Flash(pos, width, Alpha(FireHotTone, 1), life);
            FireHoldFrom(first, delay);
            return flash;
        }

        /// <summary>A few slow embers that hang in the air after a blast, rising and flickering out.</summary>
        private void FireDriftEmbers(Vector2 pos, int count, float spread, float delay)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                var ember = Spark(pos + Inside(spread), new Vector2(Range(-.7f, .7f), Range(.8f, 2)), Chance(.35f) ? FireHotTone : FireEmberTone, Range(.14f, .24f), Range(.7f, 1.1f), -.6f, 1);
                ember.flicker = .45f; ember.age = -delay - Range(0, .25f);
            }
        }

        /// <summary>Molten chunks (painted stone with glowing lava veins) thrown out by a blast.</summary>
        private void FireChunks(Vector2 pos, int count, float spread, float speed, float size, float life)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                var chunk = Spawn("Molten chunk", FireRockSprites[2 + i % 2], pos + Inside(spread), size * Range(.7f, 1.3f), Color.white, life * Range(.75f, 1.1f), false, 128);
                chunk.velocity = new Vector2(Range(-1, 1) * speed, Range(.6f, 1.3f) * speed); chunk.gravity = 23; chunk.spin = Range(-600, 600); chunk.hold = .6f; chunk.fadePower = .8f; chunk.angle = Range(0, 360);
            }
        }

        /// <summary>Painted rock debris kicked up by a landing, flung outward and falling.</summary>
        private void FireRubble(Vector2 pos, int count, float spread, float speed, float size, float life)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                float side = Range(-1, 1);
                var chip = Spawn("Rock debris", FireRockSprites[i % FireRockSprites.Length], pos + new Vector2(side * spread, Range(0, .3f)), size * Range(.5f, 1.25f), Color.white, life * Range(.7f, 1.1f), false, 128);
                chip.velocity = new Vector2(side * speed, Range(.55f, 1.25f) * speed * (1 - .3f * Mathf.Abs(side))); chip.gravity = 20; chip.spin = Range(-700, 700); chip.hold = .6f; chip.fadePower = .7f; chip.angle = Range(0, 360);
            }
        }

        /// <summary>Warm grey smoke billowing up out of a blast as its fire burns down.</summary>
        private void FireSmokeColumn(Vector2 pos, int count, float size, float rise, float delay, float life, int order)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                var puff = Puff(pos + new Vector2(Range(-.4f, .4f) * size, Range(0, .5f) * size), size * Range(.7f, 1.05f), FireCharTone, new Vector2(Range(-.4f, .4f), rise * Range(.7f, 1.2f)), life * Range(.8f, 1.1f), order);
                puff.drag = 1.6f; puff.gravity = -1.2f; puff.startScale = .5f; puff.endScale = 1.6f; puff.fadeIn = 0; puff.hold = .35f; puff.fadePower = 1.2f; puff.age = -delay - Range(0, .08f);
            }
        }

        /// <summary>A hemispherical shock front rising out of the ground at <paramref name="ground"/> and racing outwards.</summary>
        private Clip FireDome(Vector2 ground, float width, Color color, float life, bool additive, int order)
        {
            var dome = Spawn("Fire shock dome", FireDomeSprite, ground, width, color, life, additive, order);
            dome.startScale = .12f; dome.endScale = 1; dome.ease = 4; dome.fadePower = 1.3f;
            return dome;
        }

        /// <summary>A dark, slightly red soft halo. Below 30 it sits behind the actors, so it darkens the sky, not the hero.</summary>
        private Clip FireShade(Vector2 pos, float width, float alpha, float life, int order)
        {
            var clip = Spawn("Fire shade", glow, pos, width, new Color(.1f, .025f, .01f, alpha), life, false, order); clip.fadeIn = .03f; clip.fadePower = 1.2f; return clip;
        }

        /// <summary>A straight painted streak between two points, painted light or a deep red body.</summary>
        private Clip FireStreak(Vector2 a, Vector2 b, float thickness, Color color, float life, bool additive, int order)
        {
            Vector2 delta = b - a; float length = Mathf.Max(.02f, delta.magnitude);
            var clip = Spawn("Fire streak", streak, (a + b) * .5f, length, color, life, additive, order);
            clip.scale = new Vector3(length / streak.bounds.size.x, thickness / streak.bounds.size.y, 1);
            clip.angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg; clip.ease = 1;
            clip.root.transform.localScale = clip.scale; clip.root.transform.localRotation = Quaternion.Euler(0, 0, clip.angle);
            return clip;
        }

        /// <summary>A deep red body (the pillar shape painted, a little narrower than its light) standing behind a column of
        /// fire, so the column keeps its colour on a pale sky without an outline.</summary>
        private Clip FireColumnInk(Vector2 ground, float width, float height, Color color, float life, int order)
        {
            var shade = Spawn("Fire column body", pillar, ground, width, color, life, false, order);
            shade.scale = new Vector3(width / pillar.bounds.size.x, height / pillar.bounds.size.y, 1); shade.root.transform.localScale = shade.scale;
            shade.axisStart = new Vector2(.35f, .12f); shade.axisEnd = Vector2.one; shade.ease = 4; shade.hold = .2f; shade.fadePower = 1.4f;
            return shade;
        }

        /// <summary>A seal lying on the ground: crisp light strokes over a soft bed of their own glow (<paramref name="glowAlpha"/>).
        /// It never spins (the squash would wobble).</summary>
        private Clip FireGroundSeal(Vector2 ground, float width, Color color, float life, float glowAlpha)
        {
            var seal = Spawn("Fire ground seal", FireSealSprite, ground, width, color, life, true, 26); seal.scale.y *= .28f; seal.root.transform.localScale = seal.scale;
            seal.fadeIn = .06f; seal.startScale = .55f; seal.endScale = 1; seal.ease = 6; seal.hold = .65f; seal.fadePower = 1.4f;
            if (glowAlpha > 0)
            {
                var bed = Spawn("Fire ground seal glow", glow, ground, width * 1.05f, Alpha(color, glowAlpha), life, true, 25); bed.scale.y *= .3f; bed.root.transform.localScale = bed.scale;
                bed.fadeIn = .06f; bed.startScale = .55f; bed.endScale = 1; bed.ease = 6; bed.hold = .65f; bed.fadePower = 1.4f;
            }
            return seal;
        }

        /// <summary>A camera-facing spinning seal over a soft bloom of its glow (anchored to the caster when there is one).</summary>
        private Clip FireStandingSeal(Transform anchor, Vector2 pos, float width, Color color, float life, float spin, int order, float glowAlpha)
        {
            var seal = Anchored(anchor, pos, "Fire seal", FireSealSprite, width, color, life, order);
            seal.spin = spin; seal.fadeIn = .04f; seal.startScale = .45f; seal.endScale = 1; seal.ease = 6; seal.hold = .7f; seal.fadePower = 1.4f;
            if (glowAlpha > 0)
            {
                var bloom = Anchored(anchor, pos, "Fire seal glow", glow, width * .95f, Alpha(color, glowAlpha), life, order - 1);
                bloom.fadeIn = .04f; bloom.startScale = .45f; bloom.endScale = 1; bloom.ease = 6; bloom.hold = .7f; bloom.fadePower = 1.4f;
            }
            return seal;
        }

        /// <summary>A charging ball of fire riding the caster's hand round a swirling white-hot plasma heart; it grows to full
        /// size at the release and snaps out.</summary>
        private void FireChargeOrb(CastInfo c, Vector2 hand, float size, float life)
        {
            var body = Anchored(c.anchor, hand, "Fire charge body", FireBallSprites[0], size, Color.white, life, 124);
            body.frames = FireBallSprites; body.fps = 14; body.spin = -c.facing * 260;
            var plasma = Anchored(c.anchor, hand, "Fire charge plasma", plasmaFrames[0], size * .72f, Alpha(FireHotTone, 1), life, 125);
            plasma.frames = plasmaFrames; plasma.fps = 18;
            foreach (var layer in new[] { body, plasma })
            {
                layer.loop = true;
                layer.startScale = .2f; layer.endScale = 1; layer.ease = .9f; layer.hold = .88f; layer.fadePower = 2; layer.fadeIn = .03f;
            }
            var heart = Anchored(c.anchor, hand, "Fire charge heart", core, size * .34f, Color.white, life, 126); heart.startScale = .2f; heart.endScale = 1; heart.ease = .9f; heart.hold = .88f; heart.fadePower = 2;
            var bloom = Anchored(c.anchor, hand, "Fire charge bloom", glow, size * 2.2f, Alpha(Color.Lerp(FireLight, c.tint, .15f), .45f), life + .04f, 122);
            bloom.startScale = .3f; bloom.endScale = 1; bloom.ease = 1; bloom.hold = .75f; bloom.fadePower = 1.5f; bloom.fadeIn = .04f;
        }

        /// <summary>Two painted blades of flame whirling round the hand and closing in on it as the charge peaks.</summary>
        private void FireChargeBlades(CastInfo c, Vector2 hand, float size, float life)
        {
            for (int i = 0; i < 2; i++)
            {
                var arc = Anchored(c.anchor, hand, "Fire charge blade", crescent, size * (i == 0 ? 1 : .86f), Alpha(i == 0 ? FireLight : FireEmberTone, 1), life, 127);
                arc.angle = i * 180; arc.spin = -c.facing * 760; arc.startScale = 1.25f; arc.endScale = .45f; arc.ease = 1.4f;
                arc.fadeIn = .08f; arc.hold = .82f; arc.fadePower = 2;
            }
        }

        /// <summary>Flame tongues spiralling into a point over <paramref name="life"/> seconds.</summary>
        private void FireInflow(Vector2 pos, int count, float radius, float life, int facing)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                var tongue = FireTongue(pos, Range(.45f, .65f), Vector2.zero, life * Range(.8f, 1), 124, i % 3 != 2);
                tongue.orbitCenter = pos; tongue.orbitAngle = i * 360f / Mathf.Max(1, count) + Range(-15, 15); tongue.orbit = (facing > 0 ? 1 : -1) * .5f * 360 / Mathf.Max(.1f, life);
                tongue.orbitRadius = radius * Range(.7f, 1.05f); tongue.orbitRadiusEnd = .06f; tongue.orbitPower = 1.3f; tongue.alignVelocity = true; tongue.angleOffset = 90;
                tongue.drag = 0; tongue.startScale = 1; tongue.endScale = .45f; tongue.ease = 1; tongue.fadeIn = life * .3f; tongue.hold = .72f; tongue.fadePower = 1.5f;
            }
        }

        /// <summary>Fire licking up around a caster's feet, with rising embers.</summary>
        private void FireAura(Vector2 feet, int count, float life)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                bool lit = i % 3 != 2;
                var tongue = FireTongue(feet + new Vector2(side * Range(.25f, .65f), .02f), Range(.6f, .9f), new Vector2(side * Range(-.1f, .25f), Range(.8f, 1.4f)), Range(.3f, .42f), lit ? 127 : 121, lit);
                tongue.angle = side * Range(0, 14); tongue.age = -Range(0, life * .7f);
                var ember = Spark(feet + new Vector2(Range(-.7f, .7f), Range(0, .6f)), new Vector2(Range(-.4f, .4f), Range(2, 4)), Chance(.3f) ? FireHotTone : FireEmberTone, Range(.18f, .3f), Range(.4f, .6f), -1, 1.2f);
                ember.age = -Range(0, life * .8f);
            }
        }

        /// <summary>The release of a cast: a hit spark flaring over a small blast, impact wedges, a shock front thrown along the
        /// aim, and flame, needles and warm smoke toward it. <paramref name="age"/> (zero or negative) delays the whole burst.</summary>
        private void FireRelease(Vector2 hand, Vector2 aim, float scale, float age)
        {
            Vector2 dir = aim.sqrMagnitude > .0001f ? aim.normalized : Vector2.right;
            float heading = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            int first = clips.Count;
            FireHandFlash(hand, 1.5f * scale, .14f);
            FireBurst(hand + dir * .45f * scale, 1.7f * scale, FireLight, .32f, 126);
            ImpactLines(hand, 3f * scale, Alpha(FireLight, .9f), .18f);
            var wave = ShockFlip(hand + dir * .25f, 2.6f * scale, Alpha(FireLight, 1), .24f, 130);
            wave.scale.x *= .42f; wave.angle = heading; wave.startScale = .3f; wave.endScale = 1; wave.ease = 5; wave.velocity = dir * 3;
            int tongues = Budget(4);
            for (int i = 0; i < tongues; i++)
            {
                Vector2 d = (Vector2)(Quaternion.Euler(0, 0, Range(-30, 30)) * dir);
                var tongue = FireTongue(hand, Range(.6f, .9f) * scale, d * Range(5, 8), Range(.16f, .24f), i % 3 != 2 ? 127 : 122, i % 3 != 2);
                tongue.alignVelocity = true; tongue.angleOffset = -90; tongue.drag = 7;
            }
            int sparks = Budget(7);
            for (int i = 0; i < sparks; i++)
            {
                Vector2 d = (Vector2)(Quaternion.Euler(0, 0, Range(-40, 40)) * dir);
                Spark(hand, d * Range(6, 11) * scale, i % 3 == 0 ? Color.white : FireEmberTone, Range(.28f, .42f), Range(.18f, .3f), 4, 3);
            }
            for (int i = 0; i < 2; i++)
            {
                var puff = Puff(hand - dir * .1f + Vector2.up * .1f, Range(.5f, .75f) * scale, FireCharTone, -dir * Range(.6f, 1.4f) + Vector2.up * .7f, Range(.4f, .55f), 118);
                puff.endScale = 1.6f; puff.fadeIn = 0;
            }
            FireHoldFrom(first, -age);
        }

        // ---------------------------------------------------------------- Fireball

        /// <summary>How much a fire skill's drawn body swells at the current level (its gameplay reach grows by LevelArea).</summary>
        private float FireSwell { get { return 1 + (Grade - 1) * .14f; } }
        /// <summary>The level's area factor (TowerSkillBuilds.LevelArea), for blasts whose size the call does not pass.</summary>
        private float FireReach { get { return Grade >= 3 ? 1.25f : Grade == 2 ? 1.12f : 1; } }

        private void CastFireball(CastInfo c)
        {
            float w = c.windup; int f = c.facing; bool quick = w < .2f; float k = Mathf.Clamp(w / .46f, .35f, 1.2f), gs = FireSwell; int g = Grade;
            Vector2 hand = c.pos + new Vector2(f * .78f, .12f), feet = c.pos + Vector2.down, aim = c.target - hand;

            var shade = FireShade(c.pos + new Vector2(f * .5f, .2f), 3.6f * gs, .26f, w + .3f, 22); shade.hold = .6f;
            // A fire mandala behind the caster, a seal on the floor and a small one at the palm.
            FireStandingSeal(c.anchor, c.pos + new Vector2(-f * .1f, .25f), 3.1f * gs, Alpha(FireLight, .45f), w + .22f, -f * 110, 23, .3f);
            FireGroundSeal(feet, 3.4f * gs, Alpha(FireLight, .45f), w + .35f, .3f);
            FireAura(feet, Mathf.RoundToInt(7 * k), w + .1f);
            // An instant echo launches its fireball with the cast; the projectile's own launch burst is the release.
            if (quick) return;
            var palm = FireStandingSeal(c.anchor, hand + new Vector2(f * .2f, 0), 1.5f * gs, Alpha(FireHotTone, .95f), w + .1f, f * 260, 128, 0);
            palm.startScale = .2f;
            FireChargeOrb(c, hand, 1.5f * gs, w + .03f);
            FireChargeBlades(c, hand, 2f * gs, w + .02f);
            FireInflow(hand, Mathf.RoundToInt(9 * k), 2.2f * gs, w, f);
            Converge(hand, Mathf.RoundToInt(10 * k), 2.6f * gs, FireLight, w, .4f);
            if (g >= 2) FireLevelCharge(c, hand, feet, w, g);
            FireRelease(hand, aim, 1.3f, -(w - .02f));
        }

        /// <summary>
        /// What a higher-level fire cast adds: level two wraps the charge in a closing painted ring of flame and raises a flame
        /// crown round the caster; level three adds a second, white-hot seal turning the other way and a fire column behind.
        /// </summary>
        private void FireLevelCharge(CastInfo c, Vector2 hand, Vector2 feet, float w, int g)
        {
            int f = c.facing;
            var band = Anchored(c.anchor, hand, "Fire charge ring", ring, 2.6f + g * .2f, Alpha(FireLight, .5f), w + .02f, 127);
            band.startScale = 1.3f; band.endScale = .35f; band.ease = 1.2f; band.fadeIn = .08f; band.hold = .85f; band.fadePower = 2;
            int crown = Budget(g == 3 ? 6 : 4);
            for (int i = 0; i < crown; i++)
            {
                float t = crown == 1 ? 0 : i / (float)(crown - 1) * 2 - 1; bool lit = i % 3 != 2;
                var jet = FireTongue(feet + new Vector2(t * 1.1f, .02f), Range(.85f, 1.1f), new Vector2(t * .3f, Range(1.2f, 1.8f)), Range(.32f, .42f), lit ? 127 : 121, lit);
                jet.drag = 3; jet.startScale = .5f; jet.endScale = 1.3f; jet.angle = t * 12; jet.age = -Range(w * .35f, Mathf.Max(w * .4f, w - .15f));
            }
            if (g < 3) return;
            var inner = FireStandingSeal(c.anchor, c.pos + new Vector2(-f * .1f, .25f), 2.3f, Alpha(FireHotTone, .9f), w + .2f, f * 170, 24, 0); inner.startScale = .3f;
            var column = Pillar(feet, 1.5f, 5.5f, Alpha(FireLight, .75f), w + .2f, 22); column.fadeIn = w * .5f; column.hold = .7f; column.fadePower = 1.8f;
            var columnInk = FireColumnInk(feet, 1.35f, 5.5f, Alpha(FireInkTone, .42f), w + .2f, 21); columnInk.fadeIn = w * .5f; columnInk.hold = .7f; columnInk.fadePower = 1.8f;
            var columnGlow = Spawn("Fire column glow", glow, feet + Vector2.up * 2.2f, 3.2f, Alpha(FireLight, .3f), w + .2f, true, 20);
            columnGlow.scale.y *= 1.6f; columnGlow.fadeIn = w * .5f; columnGlow.hold = .7f; columnGlow.fadePower = 1.8f;
        }

        private void ProjectileFireball(Transform root, Color tint, bool signature)
        {
            Vector2 dir = root.right; if (dir.sqrMagnitude < .0001f) dir = Vector2.right;
            if (!signature)
            {
                var boltBody = Attach(root, "Fire bolt body", FireBallSprites[0], .75f, Color.white, 125); boltBody.frames = FireBallSprites; boltBody.fps = 16; boltBody.spin = -500;
                var trail = Attach(root, "Fire bolt tail", FireCometSprites[0], 1.7f, Color.white, 122); trail.frames = FireCometSprites; trail.fps = 14;
                var boltGlow = Attach(root, "Fire bolt glow", glow, 1.5f, Alpha(FireLight, .4f), 121); boltGlow.pulse = .1f;
                Emitter(boltBody, 14, .08f, (owner, p) =>
                {
                    Vector2 back = owner.external ? -(Vector2)owner.external.right : Vector2.left;
                    Spark(p, back * Range(.8f, 2) + Inside(.8f), Chance(.3f) ? FireHotTone : FireEmberTone, Range(.14f, .22f), Range(.18f, .28f), -1, 2);
                });
                Bundle(root, boltBody, trail, boltGlow);
                return;
            }
            // From level two a cast throws two balls along nearly one path: they braid round each other (opposite phases) so
            // they read as a pair, they swell only a little (two bodies are the growth), and the second one carries no extra
            // light layers or launch burst, so the pair never adds up into one white sun.
            int g = Grade; float gs = 1 + (g - 1) * .07f, rate = 1 + (g - 1) * .15f;
            Vector2 muzzle = root.position;
            bool twin = FireShotFrame == Time.frameCount && (FireShotAt - muzzle).sqrMagnitude < 1;
            if (!twin) { FireShotFrame = Time.frameCount; FireShotAt = muzzle; }
            // A painted ball of fire licking round a swirling white-hot plasma heart, a comet tail of fire with a hotter core
            // streak unrolling behind it, and two blooms (a wide heat glow and a white-hot one). Tongues of flame, small balls
            // of fire and embers shed off it, and warm grey smoke drifts from its wake.
            var heart = Attach(root, "Fireball heart", core, .5f * gs, Color.white, 127);
            var plasma = PlasmaOrb(root, "Fireball plasma", 1.25f * gs, Alpha(FireHotTone, 1), 126); plasma.fps = 18;
            var body = Attach(root, "Fireball body", FireBallSprites[0], 2.15f * gs, Color.white, 124); body.frames = FireBallSprites; body.fps = 16; body.spin = -420;
            var tail = Attach(root, "Fireball tail", FireCometSprites[0], 5.3f * gs, Color.white, 123); tail.frames = FireCometSprites; tail.fps = 12;
            var tailCore = Attach(root, "Fireball tail core", FireCometSprites[2], 3.3f * gs, Alpha(FireHotTone, .85f), 125); tailCore.frames = FireCometSprites; tailCore.fps = 15;
            // The tail unrolls behind the ball over the first quarter second instead of poking out behind the caster.
            foreach (var layer in new[] { tail, tailCore }) { layer.life = 9; layer.ease = 100; layer.axisStart = new Vector2(.15f, .7f); layer.axisEnd = Vector2.one; }
            var halo = Attach(root, "Fireball heat", glow, 3.8f * gs, Alpha(Color.Lerp(FireLight, tint, .15f), twin ? .24f : .48f), 121); halo.pulse = .1f;
            var hot = Attach(root, "Fireball white heat", glow, 1.7f * gs, Alpha(FireHotTone, twin ? .25f : .45f), 122); hot.pulse = .12f;
            var extra = new System.Collections.Generic.List<Clip>();
            if (g >= 2)
            {
                // Level two: a painted blade of flame whirling round the ball.
                var blade = Attach(root, "Fireball blade", crescent, 2.4f * gs, Alpha(FireLight, twin ? .6f : .95f), 126); blade.spin = -760;
                extra.Add(blade);
            }
            if (g >= 3)
            {
                // Level three: a second blade on the far side and a painted ring of fire round the ball.
                var blade2 = Attach(root, "Fireball far blade", crescent, 2.1f * gs, Alpha(FireEmberTone, twin ? .6f : .95f), 126); blade2.spin = -760; blade2.angle = 180;
                extra.Add(blade2);
                if (!twin) { var corona = Attach(root, "Fireball corona", ring, 2.4f * gs, Alpha(FireLight, .45f), 128); corona.pulse = .08f; extra.Add(corona); }
            }
            Emitter(body, 22 * rate, .3f * gs, (owner, p) =>
            {
                Vector2 back = owner.external ? -(Vector2)owner.external.right : Vector2.left;
                float trailing = Mathf.Atan2(back.y, back.x) * Mathf.Rad2Deg;
                if (Chance(.7f))
                {
                    bool lit = Chance(.7f);
                    var tongue = FireTongue(p + back * .2f, Range(.55f, .85f) * gs, back * Range(1.2f, 2.6f) + Vector2.up * Range(.2f, .7f), Range(.18f, .26f), lit ? 123 : 120, lit);
                    tongue.angle = trailing - 90 + Range(-20, 20);
                }
                else
                {
                    // A small ball of fire torn off the head, burning out as it falls behind.
                    var ball = FireBallClip(p + back * Range(.35f, .7f), Range(.6f, .85f) * gs, Color.white, Range(.24f, .34f), 122);
                    ball.velocity = back * Range(1, 2.2f) + Vector2.up * .35f; ball.drag = 3; ball.ramp = Ramp(Color.white, new Color(1, .55f, .38f));
                    ball.startScale = 1; ball.endScale = .4f; ball.ease = 1; ball.hold = .2f;
                }
            });
            Emitter(halo, 22 * rate, .25f, (owner, p) =>
            {
                Vector2 back = owner.external ? -(Vector2)owner.external.right : Vector2.left;
                var ember = Spark(p, back * Range(1.5f, 3.5f) + Inside(1.4f) + Vector2.up * .5f, Chance(.3f) ? FireHotTone : FireEmberTone, Range(.2f, .32f), Range(.25f, .5f), -1, 1.5f);
                ember.flicker = .3f;
            });
            Emitter(tail, 8, .3f, (owner, p) =>
            {
                Vector2 back = owner.external ? -(Vector2)owner.external.right : Vector2.left;
                var puff = Puff(p + back * .9f, Range(.5f, .75f), FireCharTone, back * .7f + Vector2.up * Range(.4f, .9f), Range(.45f, .65f), 112); puff.endScale = 1.7f; puff.fadeIn = 0;
            });
            extra.AddRange(new[] { plasma, body, tail, tailCore, halo, hot });
            Bundle(root, heart, extra.ToArray());
            if (g >= 2)
            {
                // The pair braids: each ball weaves across the path on the opposite phase, easing in out of the muzzle.
                // A driver emitter (no particles) moves every layer, the level crown added later included; the head is at
                // most 0.4 off the true path.
                float phase = twin ? Mathf.PI : 0;
                Emitter(heart, 100, 0, (owner, p) =>
                {
                    float t = owner.age, y = Mathf.Sin(t * 13 + phase) * .4f * Mathf.Clamp01(t * 7);
                    owner.offset.y = y;
                    if (owner.layers != null) for (int i = 0; i < owner.layers.Count; i++) owner.layers[i].offset.y = y;
                });
            }
            // Launch burst at the muzzle, once per cast (it does not swell with the level).
            if (twin) return;
            Vector2 at = root.position;
            float heading = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            // A muzzle blast billows out round the ball as it leaves, under a flaring hit spark and a shock front thrown forward.
            FireHandFlash(at, 1.9f, .14f);
            FireBurst(at + dir * .5f, 1.9f, FireLight, .34f, 126);
            var boom = ShockFlip(at + dir * .3f, 2.9f, Alpha(FireLight, 1), .24f, 130);
            boom.scale.x *= .4f; boom.angle = heading; boom.startScale = .25f; boom.endScale = 1; boom.ease = 5; boom.velocity = dir * 2.5f;
            for (int i = 0; i < 4; i++)
            {
                Vector2 d = (Vector2)(Quaternion.Euler(0, 0, Range(-35, 35)) * dir);
                var tongue = FireTongue(at, Range(.6f, .9f), d * Range(4, 7f), Range(.16f, .24f), i % 3 != 2 ? 127 : 122, i % 3 != 2);
                tongue.alignVelocity = true; tongue.angleOffset = -90; tongue.drag = 7;
            }
            for (int i = 0; i < 2; i++) { var puff = Puff(at - dir * .2f, Range(.5f, .7f), FireCharTone, -dir * Range(.5f, 1.2f) + Vector2.up * .6f, Range(.35f, .5f), 118); puff.fadeIn = 0; puff.endScale = 1.6f; }
        }

        private void HitFireball(Vector2 pos, Color tint, bool signature, Vector2 direction)
        {
            if (signature)
            {
                // The splash grows with the level's area; density (Budget), a flame rim and (at three) a fire column come on top.
                // Fireballs of one cast striking the same foe together burst once; the others add a quick flare.
                int g = Grade; float r = 1.9f * FireReach;
                bool echo = FireLastBlast != null && FireLastBlast.age < .15f && clips.Contains(FireLastBlast) && (FireLastBlast.position - pos).sqrMagnitude < 1.5f;
                if (echo) { FireBlastEcho(pos, r, direction); return; }
                // (Level three trims the blast's own particles: its burning ground and awakening bring their own fire.)
                FireExplosion(pos, r, direction, (g >= 3 ? 1.05f : 1.25f) / (1 + (g - 1) * .34f), tint);
                if (g >= 2) { FireLevelBlast(pos, r, g); GradeImpact(SkillEffect.Fireball, pos, r, tint, 0); }
                return;
            }
            FireFlash(pos, 1.1f, .12f);
            var wave = ShockFlip(pos, 1.15f, Alpha(FireLight, 1), .18f, 131); wave.startScale = .35f; wave.endScale = 1; wave.ease = 5;
            for (int i = 0; i < 2; i++)
            {
                var tongue = FireTongue(pos + Inside(.1f), Range(.35f, .48f), Direction(Range(40, 140)) * Range(1, 2), Range(.16f, .22f), 127, true);
                tongue.alignVelocity = true; tongue.angleOffset = -90; tongue.drag = 5;
            }
            for (int i = 0; i < 4; i++) Spark(pos, (Direction(Range(0, 360)) + direction * .6f) * Range(2.5f, 4.5f), i % 2 == 0 ? FireHotTone : FireEmberTone, Range(.18f, .28f), Range(.16f, .26f), 6, 3);
        }

        private void ImpactFireball(Vector2 pos, float radius, Color tint, int facing)
        {
            float r = Mathf.Clamp(radius * 1.05f, .8f, 3.2f);
            FireExplosion(pos, r, new Vector2(facing, 0), 1.6f, tint);
            if (Grade >= 2) FireLevelBlast(pos, r, Grade);
        }

        /// <summary>The newest fireball blast (its position is the blast), so fireballs landing together burst once.</summary>
        private Clip FireLastBlast;
        /// <summary>The frame and muzzle of the last fireball launched, so the second ball of a pair knows it is the twin.</summary>
        private int FireShotFrame = -1; private Vector2 FireShotAt;

        /// <summary>A second fireball landing on a blast that just went off: a smaller blast lobe and a flare, a ring, a few
        /// tongues and warm smoke.</summary>
        private void FireBlastEcho(Vector2 pos, float r, Vector2 push)
        {
            Vector2 at = pos + Inside(.4f);
            Blast(at + Vector2.up * r * .15f, r * 1.4f, FireLight, .45f, 126);
            FireFlash(at, r * .9f, .1f);
            Wave(at, r * 1.8f, Alpha(FireLight, 1), .22f, 5);
            for (int i = 0; i < 4; i++)
            {
                Vector2 d = Direction(Range(20, 160)); bool lit = i % 3 != 2;
                var tongue = FireTongue(at, r * Range(.4f, .55f), d * r * Range(2, 3) + push * r, Range(.22f, .32f), lit ? 127 : 121, lit);
                tongue.alignVelocity = true; tongue.angleOffset = -90; tongue.drag = 5;
            }
            for (int i = 0; i < 4; i++) Spark(at, Direction(Range(0, 360)) * r * Range(2, 4), i % 2 == 0 ? FireHotTone : FireEmberTone, Range(.24f, .36f), Range(.3f, .45f), 6, 1.5f);
            InkBurst(at, r * .4f, FireCharTone, 2, .4f, 119);
        }

        /// <summary>A higher-level blast: flames burst up round the rim of the splash, taller and denser with the level. (The
        /// level-three column is the awakening's: the ground catching fire.)</summary>
        private void FireLevelBlast(Vector2 pos, float r, int g)
        {
            Vector2 ground = pos + Vector2.down * .8f;
            int rim = Budget(5);
            for (int i = 0; i < rim; i++)
            {
                float t = rim == 1 ? 0 : i / (float)(rim - 1) * 2 - 1; bool lit = i % 3 != 2;
                var tongue = FireTongue(ground + new Vector2(t * r * .9f, .02f), r * Range(.42f, .55f) * (g >= 3 ? 1.15f : 1), new Vector2(t * .5f, Range(1.4f, 2.2f)), Range(.35f, .5f), lit ? 127 : 121, lit);
                tongue.drag = 2.5f; tongue.startScale = .4f; tongue.endScale = 1.3f; tongue.angle = -t * 14; tongue.age = -(.04f + Mathf.Abs(t) * .06f);
            }
        }

        /// <summary>The old entry point, kept for callers that still want a plain fire blast.</summary>
        private void Explosion(Vector2 pos, float r, float intensity)
        {
            FireExplosion(pos, r, Vector2.zero, Mathf.Clamp(intensity, .5f, 2), FireLight);
        }

        /// <summary>
        /// A fire blast sized to <paramref name="r"/> (the splash radius): the blast flipbook billowing out of a white-hot heart
        /// with a second lobe bursting off one side a beat later, a big drawn hit spark, a painted fireball that rises and cools,
        /// petals of flame flung out, a painted shock front and dome, needle embers, hanging embers and molten chunks, warm grey
        /// smoke, and scorched ground with hot cracks and a fading glow.
        /// <paramref name="detail"/> scales the optional particle counts (1 is about 60 clips).
        /// </summary>
        private void FireExplosion(Vector2 pos, float r, Vector2 push, float detail, Color tint)
        {
            Vector2 ground = pos + new Vector2(0, -.8f);
            push = push.sqrMagnitude > .0001f ? push.normalized : Vector2.zero;
            float v = r * 1.1f;
            Color blaze = Color.Lerp(FireLight, tint, .1f);

            // Peak: the blast billows out of a white-hot heart; a smaller lobe bursts off one side (along the push) a beat later,
            // a big hit spark flares at the heart and impact wedges race out.
            var shade = FireShade(pos, v * 2.6f, .26f, .5f, 29); shade.hold = .25f;
            var blast = FireBurst(pos + Vector2.up * v * .18f + push * v * .12f, v * 2.7f, blaze, .78f, 127); FireLastBlast = blast;
            float lobe = Mathf.Abs(push.x) > .1f ? Mathf.Sign(push.x) : (Chance(.5f) ? 1 : -1);
            FireBurst(pos + new Vector2(lobe * v * .5f, v * .4f), v * 1.5f, FireEmberTone, .6f, 125, .06f);
            FireFlash(pos, v * 1.2f, .12f);
            ImpactLines(pos, v * 3f, Alpha(FireLight, .9f), .2f);

            // The fireball: a painted ball that swells and rises, cooling from white-hot to deep red as it erodes, with smaller
            // balls thrown up and out of it.
            var cooling = Ramp(Color.white, new Color(1, .78f, .55f), new Color(1, .45f, .32f));
            var mass = FireBallClip(pos, v * 1.5f, Color.white, .75f, 125);
            mass.ramp = cooling; mass.velocity = push * v * .5f + Vector2.up * v * .8f; mass.drag = 3; mass.startScale = .5f; mass.endScale = 1.25f; mass.hold = .35f;
            int balls = Budget(Mathf.RoundToInt(3 * detail));
            for (int i = 0; i < balls; i++)
            {
                Vector2 d = Direction(90 + (i - (balls - 1) * .5f) * 70 + Range(-20, 20));
                var ball = FireBallClip(pos + d * v * .3f, v * Range(.6f, .85f), Color.white, Range(.5f, .7f), 123);
                ball.ramp = cooling; ball.velocity = d * v * Range(1.4f, 2.2f) + push * v * .6f; ball.drag = 4; ball.age = -Range(0, .03f);
            }
            // Painted petals of flame flung out of the ball, longest upward and along the push.
            int petals = Budget(Mathf.RoundToInt(8 * detail));
            for (int i = 0; i < petals; i++)
            {
                Vector2 d = Direction(i * 360f / petals + Range(-16, 16) + 90);
                float lift = Mathf.Clamp01(.55f + d.y * .45f + Vector2.Dot(d, push) * .25f);
                bool lit = i % 4 != 3;
                var petal = FireTongue(pos + d * v * .1f, v * Range(.5f, .65f) * (.7f + .55f * lift), d * v * Range(1.8f, 2.8f) * (.6f + .6f * lift), Range(.32f, .45f), lit ? 127 : 121, lit);
                petal.alignVelocity = true; petal.angleOffset = -90; petal.drag = 6; petal.startScale = .5f; petal.endScale = 1.2f; petal.ease = 6;
            }

            // A painted shock front and a dome rising out of the ground, both ending on the splash radius (the ring's rim sits
            // at 0.39 of its width, the dome's front at 0.43).
            Wave(pos, v * 2.1f, Alpha(FireHotTone, 1), .22f, 5);
            FireDome(ground, v * 2.22f, Alpha(FireLight, .95f), .36f, true, 129).age = -.02f;
            FireLevel(GroundWave(ground, v * 2.5f, Alpha(FireLight, .9f), .38f));
            // Flame running out along the ground both ways.
            int run = Budget(Mathf.RoundToInt(4 * detail));
            for (int i = 0; i < run; i++)
            {
                float side = i % 2 == 0 ? -1 : 1; bool lit = i % 4 < 3;
                var tongue = FireTongue(ground + new Vector2(side * v * .15f, .02f), v * Range(.38f, .5f), new Vector2(side * v * Range(2.6f, 3.6f), Range(0, .4f)), Range(.36f, .5f), lit ? 126 : 121, lit);
                tongue.drag = 2.6f; tongue.angle = -side * Range(15, 30);
            }

            // Needle embers flung out, most of them upward and along the push, and molten chunks.
            Needles(pos, Mathf.RoundToInt(7 * detail), v * 5f, FireEmberTone, .3f, .42f);
            int embers = Budget(Mathf.RoundToInt(8 * detail));
            for (int i = 0; i < embers; i++)
                Spark(pos + Inside(v * .2f), Direction(Range(20, 160)) * v * Range(1.8f, 4.4f) + push * v * 1.2f, i % 3 == 0 ? Color.white : i % 2 == 0 ? FireHotTone : FireEmberTone, Range(.26f, .4f), Range(.45f, .8f), 7, 1.2f);
            FireChunks(pos, Mathf.RoundToInt(3 * detail), v * .2f, v * 2.8f, .26f, .85f);
            FireDriftEmbers(pos + Vector2.up * v * .4f, Mathf.RoundToInt(4 * detail), v * .7f, .15f);

            // Warm grey smoke: a burst kicked out under the fire, then a column rising as the fire burns down.
            InkBurst(pos + Vector2.up * v * .1f, v * .72f, FireCharTone, Mathf.RoundToInt(4 * detail), .7f, 119);
            FireSmokeColumn(pos + Vector2.up * v * .25f, Mathf.RoundToInt(3 * detail), v * .75f, v * 1.1f, .12f, 1.2f, 118);

            // Scorched ground with hot cracks and a fading glow, and flames licking up from it a moment later.
            Scorch(ground, r * 2.1f, 1.5f);
            Crack(ground, r * 1.4f, new Color(1, .5f, .15f, .9f), 1.2f);
            var embersGlow = GroundGlow(ground, r * 2.4f, Alpha(FireLight, .3f), 1); embersGlow.hold = .3f;
            int licks = Budget(Mathf.RoundToInt(3 * detail));
            for (int i = 0; i < licks; i++)
            {
                var tongue = FireTongue(ground + new Vector2(Range(-.7f, .7f) * r, .02f), r * Range(.3f, .42f), new Vector2(Range(-.2f, .2f), Range(.3f, .7f)), Range(.45f, .7f), i % 3 != 2 ? 126 : 121, i % 3 != 2);
                tongue.age = -Range(.06f, .25f); tongue.drag = 2;
            }
        }

        // ---------------------------------------------------------------- Meteor

        private void CastMeteor(CastInfo c)
        {
            float w = c.windup; int f = c.facing; bool quick = w < .2f; float k = Mathf.Clamp(w / .46f, .35f, 1.2f), gs = FireSwell; int g = Grade;
            // Casts receive min(1.3, radius * 0.3): below the cap that is the exact radius; past it, rebuild the blast radius
            // from the card (3.5) and the level (LevelArea with the 1.2 single-blow widening: 1.344 at two, 1.5 at three) so the
            // telegraph keeps matching the real area.
            float radius = c.radius >= 1.29f ? Mathf.Max(4.33f, 3.5f * (g >= 3 ? 1.5f : g == 2 ? 1.344f : 1)) : Mathf.Clamp(c.radius / .3f, 2.5f, 5f);
            radius = Mathf.Min(radius, 6.5f);
            float impactAt = w + .7f;
            Vector2 feet = c.pos + Vector2.down, hand = c.pos + new Vector2(f * .3f, .95f);
            Vector2 landing = new Vector2(c.target.x, c.target.y - .1f), targetGround = new Vector2(c.target.x, c.target.y - .7f), sky = landing + FireMeteorPath;
            Color blaze = new Color(1, .42f, .1f);

            // The caster: a mandala behind, a ball of fire in the raised hand, a beam into the sky.
            var shade = FireShade(c.pos + new Vector2(0, .9f), 4f, .3f, w + .4f, 22); shade.hold = .6f;
            FireStandingSeal(c.anchor, c.pos + new Vector2(0, .35f), 3.6f, Alpha(FireLight, .45f), w + .3f, -f * 90, 23, .3f);
            if (!quick)
            {
                FireChargeOrb(c, hand, 1.3f * gs, w + .03f);
                FireInflow(hand, Mathf.RoundToInt(7 * k), 1.8f * gs, w, f);
                Converge(hand, Mathf.RoundToInt(10 * k), 2.4f, FireLight, w, .45f);
            }
            float beamLife = w + (quick ? .3f : .2f);
            var beam = Pillar(hand, 1.3f, 10, Alpha(FireLight, .9f), beamLife, 122); beam.hold = .6f; beam.age = quick ? 0 : -.05f;
            var beamCore = Pillar(hand, .45f, 10, Alpha(FireHotTone, .95f), beamLife, 123); beamCore.hold = .55f; beamCore.age = beam.age;
            var beamInk = FireColumnInk(hand, 1.15f, 10, Alpha(FireInkTone, .4f), beamLife, 117); beamInk.hold = .55f; beamInk.fadePower = 1.3f; beamInk.age = beam.age;
            FireGroundSeal(feet, 3.8f, Alpha(FireLight, .42f), w + .4f, .26f);
            FireAura(feet, Mathf.RoundToInt(6 * k), w);

            // The sky tears open where the meteor will come from: a dim red burn, a soft bloom, two painted blades of fire
            // wheeling round it and a crisp seal of light strokes.
            var skyShade = Spawn("Meteor sky burn", glow, sky, 7f, new Color(.42f, .06f, .02f, .28f), impactAt, false, 111); skyShade.fadeIn = w * .7f + .05f; skyShade.hold = .7f; skyShade.fadePower = 1.5f;
            var skyGlow = Spawn("Meteor sky glow", glow, sky, 5.2f * gs, Alpha(FireLight, .32f), impactAt, true, 118); skyGlow.fadeIn = w * .5f + .05f; skyGlow.hold = .7f; skyGlow.fadePower = 1.5f; skyGlow.pulse = .06f;
            for (int i = 0; i < 2; i++)
            {
                var rift = Spawn("Meteor sky rift", crescent, sky, 3.9f - i * .5f, Alpha(i == 0 ? FireLight : FireEmberTone, 1), impactAt - .05f, true, 121);
                rift.angle = i * 180 + Range(-20, 20); rift.spin = 300; rift.fadeIn = w * .5f + .05f; rift.hold = .7f; rift.startScale = .4f; rift.endScale = 1.1f; rift.ease = 3; rift.fadePower = 1.4f;
            }
            var skySeal = Spawn("Meteor sky seal", FireSealSprite, sky, 4.4f * gs, Alpha(FireLight, .78f), impactAt - .1f, true, 122);
            skySeal.spin = -120; skySeal.fadeIn = .1f; skySeal.startScale = .4f; skySeal.endScale = 1; skySeal.ease = 6; skySeal.hold = .65f; skySeal.fadePower = 1.4f; skySeal.age = -w * .3f;
            if (g >= 2)
            {
                // A higher level opens a wider sky: a second seal turning the other way round the first.
                var outer = Spawn("Meteor outer sky seal", FireSealSprite, sky, 6.2f * gs, Alpha(new Color(1, .5f, .16f), .62f), impactAt - .05f, true, 121);
                outer.spin = 70; outer.fadeIn = .12f; outer.startScale = .3f; outer.endScale = 1; outer.ease = 5; outer.hold = .7f; outer.fadePower = 1.4f; outer.age = -w * .2f;
            }
            if (g >= 3)
            {
                // Awakened: two smaller stones fall either side (gameplay: 1.7 units out, 60% radius, 0.2 s after the main one).
                // Each is told by one thin reticle closing on its own landing, inside the main mark: no second seal or glow.
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 at = targetGround + new Vector2(s * 1.7f, 0); float sr = radius * .6f, appear = w * .4f, lands = impactAt + .2f - appear;
                    var sideReticle = Spawn("Meteor side reticle", ring, at, sr * 2 / .74f, Alpha(FireLight, .35f), lands, true, 27); sideReticle.scale.y *= .3f; sideReticle.root.transform.localScale = sideReticle.scale;
                    sideReticle.startScale = 1; sideReticle.endScale = .1f; sideReticle.ease = 1.3f; sideReticle.fadeIn = .15f; sideReticle.hold = .85f; sideReticle.fadePower = 1; sideReticle.age = -appear;
                }
            }

            // Telegraph on the ground: the damage area (the seal's rim sits at 0.885 of its half-width, on the true radius), and a
            // reticle that closes exactly when the meteor lands. The light keeps to the strokes over a faint bed of their glow,
            // dimmer at higher levels whose larger mark would otherwise flood the floor.
            var mark = FireGroundSeal(targetGround, radius * 2 / .885f, Alpha(blaze, g >= 3 ? .3f : g == 2 ? .35f : .42f), impactAt + .15f, 0); mark.hold = .8f; mark.fadePower = 2;
            var areaGlow = GroundGlow(targetGround, radius * 1.9f, Alpha(blaze, g >= 3 ? .1f : g == 2 ? .13f : .17f), impactAt + .1f); areaGlow.startScale = .5f; areaGlow.endScale = 1.1f; areaGlow.ease = 1; areaGlow.fadeIn = Mathf.Max(.1f, w);
            for (int i = 0; i < 2; i++)
            {
                float delay = i * .18f;
                var reticle = Spawn("Meteor reticle", ring, targetGround, radius * 2 / .74f, Alpha(FireLight, i == 0 ? .38f : .26f), impactAt - delay, true, 27); reticle.scale.y *= .3f; reticle.root.transform.localScale = reticle.scale;
                reticle.startScale = 1; reticle.endScale = .1f; reticle.ease = 1.3f; reticle.fadeIn = .1f; reticle.hold = .85f; reticle.fadePower = 1; reticle.age = -delay;
            }
            int heat = Budget(6);
            for (int i = 0; i < heat; i++)
            {
                var ember = Spark(targetGround + new Vector2(Range(-radius, radius) * .8f, .1f), new Vector2(Range(-.3f, .3f), Range(1.5f, 3)), FireEmberTone, Range(.18f, .3f), Range(.45f, .7f), -1, 1.2f);
                ember.age = -Range(w * .3f, impactAt - .3f);
            }

            // Release: a fire star at the hand and a burst of the sky beam.
            float release = quick ? 0 : -(w - .02f);
            if (!quick) FireRelease(hand, Vector2.up, 1.1f, release);
            var burstBeam = Pillar(hand, 2.4f, 11, Alpha(FireLight, 1), .32f, 124); burstBeam.age = release;
            var burstCore = Pillar(hand, .8f, 11, Alpha(FireHotTone, 1), .24f, 125); burstCore.age = release;
        }

        /// <summary>A meteor falling onto <paramref name="pos"/>; it lands after <paramref name="delay"/> seconds, when the game calls Impact.</summary>
        public void DescendMeteor(Vector2 pos, float delay)
        {
            EnsureTextures();
            delay = Mathf.Max(.1f, delay);
            // The level swells the stone and its mantle; a level-three meteor's two side stones (they land 0.9 s after the cast,
            // the main one 0.7 s) are drawn at about half size. They break into the sky a moment after the main one and fall
            // steeply, splaying outward on their own paths, so each reads as its own stone instead of debris in the main mantle.
            bool sideStone = Grade >= 3 && delay > .8f;
            float m = sideStone ? .58f : FireSwell, late = 0;
            Vector2 path = FireMeteorPath;
            if (!sideStone) { FireDescendFrame = Time.frameCount; FireDescendX = pos.x; }
            else
            {
                float side = FireDescendFrame == Time.frameCount ? Mathf.Sign(pos.x - FireDescendX) : (FireSideNext % 2 == 0 ? -1 : 1);
                if (side == 0) side = 1;
                path = new Vector2(side * 1.5f, 7.3f); late = Mathf.Min(.12f, delay * .2f);
                FireSidePaths[FireSideNext++ % FireSidePaths.Length] = new Vector4(pos.x, pos.y, path.x, Time.time);
            }
            int firstClip = clips.Count;
            delay -= late;
            Vector2 start = pos + path, velocity = (pos - start) / delay, dir = velocity.normalized, ground = pos + new Vector2(0, -.6f);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, life = delay + .02f;

            // A painted comet: banded head and streaky tail over a slimmer deep red body, a molten stone in a ball of fire, a bow
            // shock on its leading edge, painted streaks behind and one dim bloom.
            var shade = Spawn("Meteor shade", glow, start, 4.4f * m, new Color(.08f, .015f, .01f, .28f), life, false, 111); shade.velocity = velocity; shade.fadePower = 0; shade.fadeIn = .05f;
            var bloom = Spawn("Meteor heat", glow, start, 5.4f * m, Alpha(FireLight, .5f), life, true, 120); bloom.velocity = velocity; bloom.fadePower = 0;
            var tailShade = FireStreak(start - dir * 8.2f * m, start - dir * .6f * m, 1.9f * m, Alpha(FireInkTone, .5f), life, false, 116); tailShade.velocity = velocity; tailShade.fadePower = 0;
            var tailLight = FireStreak(start - dir * 9f * m, start - dir * .2f * m, 2.3f * m, Alpha(FireLight, .85f), life, true, 119); tailLight.velocity = velocity; tailLight.fadePower = 0;
            var tailCore = FireStreak(start - dir * 5.5f * m, start, .8f * m, Alpha(FireHotTone, .9f), life, true, 121); tailCore.velocity = velocity; tailCore.fadePower = 0;
            var mantleInk = Spawn("Meteor mantle body", FireCometSprites[0], start - dir * .25f, 8.6f * m, Alpha(FireInkTone, .55f), life, false, 121);
            var mantle = Spawn("Meteor mantle", FireCometSprites[0], start, 9.2f * m, Color.white, life, true, 123);
            foreach (var layer in new[] { mantleInk, mantle }) { layer.frames = FireCometSprites; layer.fps = 14; layer.loop = true; layer.velocity = velocity; layer.angle = angle; layer.fadePower = 0; }
            // The stone burns inside a churning ball of fire, its veins and leading face glowing white-hot.
            var head = FireBallClip(start + dir * .1f * m, 3.4f * m, Color.white, life, 124); head.velocity = velocity; head.fadePower = 0; head.fps = 18; head.startScale = head.endScale = 1;
            var stone = Spawn("Falling meteor", FireStoneSprite, start, 2.3f * m, Color.white, life, false, 125); stone.velocity = velocity; stone.spin = 160; stone.fadePower = 0;
            var stoneHeat = Spawn("Falling meteor heat", FireStoneGlowSprite, start, 2.3f * m, Alpha(FireLight, 1), life, true, 126); stoneHeat.velocity = velocity; stoneHeat.spin = 160; stoneHeat.fadePower = 0; stoneHeat.flicker = .15f;
            var bow = Spawn("Meteor bow shock", crescent, start + dir * .18f * m, 2.3f * m, Alpha(FireLight, 1), life, true, 127); bow.velocity = velocity; bow.angle = angle; bow.fadePower = 0; bow.pulse = .05f;
            if (Grade >= 2 && m > 1)
            {
                // A higher-level stone burns inside a white-hot sheath with a second, wider bow shock.
                var sheath = Spawn("Meteor sheath", FireCometSprites[2], start, 7f * m, Alpha(FireHotTone, .7f), life, true, 124);
                sheath.frames = FireCometSprites; sheath.fps = 18; sheath.loop = true; sheath.velocity = velocity; sheath.angle = angle; sheath.fadePower = 0;
                var wideBow = Spawn("Meteor outer bow", crescent, start + dir * .3f * m, 3f * m, Alpha(FireLight, .55f), life, true, 124); wideBow.velocity = velocity; wideBow.angle = angle; wideBow.fadePower = 0; wideBow.pulse = .08f;
            }

            float trailing = angle + 180;
            Emitter(stone, 30 * Mathf.Min(1, m * m), .5f * m, (owner, p) =>
            {
                if (Chance(.6f))
                {
                    bool lit = Chance(.7f);
                    var tongue = FireTongue(p - dir * Range(.3f, 1) * m, Range(.9f, 1.4f) * m, -dir * Range(1, 3) + Inside(1), Range(.2f, .3f), lit ? 123 : 120, lit);
                    tongue.angle = trailing - 90 + Range(-15, 15);
                }
                else
                {
                    var ball = FireBallClip(p - dir * Range(.4f, 1.2f) * m, Range(.8f, 1.2f) * m, Color.white, Range(.3f, .45f), 119);
                    ball.velocity = -dir * Range(1, 2.5f) + Vector2.up * .3f; ball.drag = 3; ball.ramp = Ramp(Color.white, new Color(1, .5f, .35f));
                }
            });
            Emitter(bloom, 24 * Mathf.Min(1, m * m), .8f * m, (owner, p) =>
            {
                Spark(p, -dir * Range(1.5f, 4) + Inside(2.2f), Chance(.3f) ? FireHotTone : FireEmberTone, Range(.2f, .34f), Range(.3f, .5f), -1, 1.4f);
            });
            Emitter(shade, 12 * Mathf.Min(1, m * m), .6f * m, (owner, p) =>
            {
                var puff = Puff(p - dir * Range(.9f, 1.7f) * m, Range(.9f, 1.4f) * m, FireCharTone, -dir * Range(.5f, 1.2f) + Vector2.up * .3f, Range(.6f, .9f), 113); puff.endScale = 1.9f; puff.drag = 2; puff.fadeIn = 0;
            });

            // Burning fragments torn off on entry fall alongside it and burn out just before it lands.
            int fragments = m < 1 ? 1 : GradeSteady(3);
            for (int i = 0; i < fragments; i++)
            {
                Vector2 side = new Vector2(-dir.y, dir.x) * (i % 2 == 0 ? 1 : -1) * Range(.9f, 1.8f);
                Vector2 at = start + side - dir * Range(.8f, 2.4f);
                float burn = delay * Range(.82f, .95f), size = Range(.5f, .75f);
                var chunk = Spawn("Meteor fragment", FireRockSprites[2 + i % 2], at, size, Color.white, burn, false, 125); chunk.velocity = velocity; chunk.spin = Range(-400, 400); chunk.hold = .85f; chunk.fadePower = 1.5f;
                var chunkInk = Spawn("Meteor fragment body", FireCometSprites[0], at - dir * .1f, size * 4.1f, Alpha(FireInkTone, .5f), burn, false, 121);
                var chunkTail = Spawn("Meteor fragment tail", FireCometSprites[0], at, size * 4.4f, Color.white, burn, true, 123);
                foreach (var layer in new[] { chunkInk, chunkTail }) { layer.frames = FireCometSprites; layer.fps = 14; layer.loop = true; layer.velocity = velocity; layer.angle = angle; layer.hold = .85f; layer.fadePower = 1.5f; }
                Emitter(chunkTail, 12, .15f, (owner, p) => Spark(p, -dir * Range(1, 2.5f) + Inside(.8f), FireEmberTone, Range(.14f, .24f), Range(.22f, .4f), -1, 1.5f));
            }

            // Where it breaks into the sky: a burst of fire billows out of the seal under a flaring spark.
            Blast(start, 3.6f * m, FireLight, .42f, 122);
            FireFlash(start, 3.2f * m, .14f);
            Wave(start, 4.6f * m, Alpha(FireLight, .95f), .36f, 5);
            ImpactLines(start, 5.2f * m, Alpha(FireLight, .7f), .26f);

            // The side stones fall inside the main mark: they add no ground light of their own (it only flooded the floor).
            if (!sideStone)
            {
                // On the ground: its light grows and a shadow gathers under it.
                var shadow = Spawn("Meteor shadow", glow, ground, 4.4f * m, new Color(.04f, .01f, .01f, .55f), delay + .05f, false, 24);
                shadow.scale.y *= .3f; shadow.root.transform.localScale = shadow.scale; shadow.startScale = .25f; shadow.endScale = 1; shadow.ease = 1; shadow.fadeIn = delay * .4f; shadow.fadePower = 0;
                var light = GroundGlow(ground, 5.2f * m, Alpha(new Color(1, .45f, .12f), Grade >= 3 ? .2f : Grade == 2 ? .28f : .36f), delay + .08f); light.startScale = .3f; light.endScale = 1.1f; light.ease = 1; light.fadeIn = delay * .6f; light.hold = .9f; light.fadePower = .6f;
                int rising = Budget(6);
                for (int i = 0; i < rising; i++)
                {
                    var ember = Spark(ground + new Vector2(Range(-2, 2), .1f), new Vector2(Range(-.4f, .4f), Range(1.5f, 3.2f)), FireEmberTone, Range(.18f, .3f), Range(.4f, .6f), -1, 1.2f);
                    ember.age = -Range(0, delay * .8f);
                }
            }
            // A side stone enters late: hold everything it spawned until then.
            if (late > 0)
                for (int i = firstClip; i < clips.Count; i++) { var clip = clips[i]; clip.age -= late; if (clip.renderer) clip.renderer.enabled = false; }
        }

        // The main stone's landing (frame and x), so its side stones know which way to splay, and the side stones' own paths
        // (landing x, landing y, path x, time) for their trails.
        private int FireDescendFrame = -1, FireSideNext; private float FireDescendX;
        private readonly Vector4[] FireSidePaths = new Vector4[4];

        private void TrailMeteor(Vector2 from, Vector2 to, Color tint)
        {
            Vector2 delta = from - to;
            // The game draws the trail straight down; the meteor falls along FireMeteorPath (a side stone along its own), so
            // follow that instead.
            if (Mathf.Abs(delta.x) < .05f && delta.y > 0)
            {
                Vector2 path = FireMeteorPath;
                foreach (var side in FireSidePaths)
                    if (side.w > 0 && Time.time - side.w < 2 && Mathf.Abs(side.x - to.x) < .2f && Mathf.Abs(side.y - to.y) < 1) { path = new Vector2(side.z, 7.3f); break; }
                from = to + path.normalized * delta.magnitude * 1.25f;
            }
            delta = to - from; float length = delta.magnitude; if (length < .05f) return;
            Vector2 dir = delta / length;
            // A level-three meteor's side stones trail in 0.2 s after the main one: thinner wakes with a third of the smoke.
            bool sideStone = FireLastMeteor != null && FireLastMeteor.age < .4f && clips.Contains(FireLastMeteor);
            float t0 = sideStone ? .6f : 1, part = sideStone ? .35f : 1;
            System.Func<int, int> count = n => GradeSteady(Mathf.Max(1, Mathf.RoundToInt(n * part)));
            var shade = FireStreak(from, to, 1.75f * t0, Alpha(FireInkTone, .5f), .6f, false, 114); shade.fadePower = 1.2f; shade.hold = .2f;
            var burn = FireStreak(from, to, 1.9f * t0, Alpha(FireLight, .95f), .42f, true, 127); burn.fadePower = 1.2f; burn.startScale = 1; burn.endScale = 1.12f; burn.hold = .2f;
            var white = FireStreak(from + dir * length * .3f, to, .6f * t0, Alpha(FireHotTone, 1), .26f, true, 128); white.fadePower = 1.3f;
            if (sideStone) return;
            int puffs = count(8);
            for (int i = 0; i < puffs; i++)
            {
                float t = (i + Range(.1f, .9f)) / puffs;
                var puff = Puff(Vector2.Lerp(from, to, t) + Inside(.3f), Mathf.Lerp(1.6f, 1f, t), FireCharTone, new Vector2(Range(-.4f, .4f), Range(.1f, .5f)), Range(.8f, 1.1f), 112);
                puff.drag = 1.5f; puff.endScale = 1.9f; puff.startScale = .7f; puff.fadeIn = 0; puff.hold = .3f;
            }
            int balls = count(3);
            for (int i = 0; i < balls; i++)
            {
                var ball = FireBallClip(Vector2.Lerp(from, to, Range(.65f, 1)) + Inside(.3f), Range(1f, 1.4f), Color.white, Range(.35f, .5f), 120);
                ball.velocity = -dir * Range(.5f, 1.5f) + Vector2.up * .4f; ball.drag = 2; ball.ramp = Ramp(Color.white, new Color(1, .5f, .35f));
            }
            int tongues = count(6);
            for (int i = 0; i < tongues; i++)
            {
                float t = Range(.4f, 1); bool lit = i % 3 != 2;
                var tongue = FireTongue(Vector2.Lerp(from, to, t) + Inside(.25f), Range(.8f, 1.2f), -dir * Range(.5f, 1.5f), Range(.22f, .36f), lit ? 128 : 121, lit);
                tongue.angle = Mathf.Atan2(-dir.y, -dir.x) * Mathf.Rad2Deg - 90 + Range(-15, 15);
            }
            int embers = count(10);
            for (int i = 0; i < embers; i++)
                Spark(Vector2.Lerp(from, to, Range(0, 1)) + Inside(.4f), -dir * Range(.5f, 2) + Inside(1.5f), Chance(.3f) ? FireHotTone : FireEmberTone, Range(.18f, .3f), Range(.35f, .7f), 1.5f, 1.5f);
        }

        private void ImpactMeteor(Vector2 pos, float radius, Color tint, int facing)
        {
            float r = Mathf.Clamp(radius, .9f, 6.5f);
            bool big = r >= 2.5f;
            Vector2 ground = pos + Vector2.down;
            int g = Grade;
            // A level-three meteor's side stones land 0.2 s after the main one with a smaller radius: they wash the screen
            // no second time and throw about half the particles.
            bool follow = FireLastMeteor != null && FireLastMeteor.age < .4f && clips.Contains(FireLastMeteor);
            bool sideStone = follow && r < FireLastMeteorRadius * .8f;
            // A higher-level meteor grows in size, a rim of geysers and a spire rather than in raw particles: it already
            // brings two more stones at level three.
            if (sideStone) { FireMeteorSideImpact(pos, ground, r); return; }
            float detail = g >= 3 ? .5f : 1;
            System.Func<int, int> count = n => GradeSteady(Mathf.Max(1, Mathf.RoundToInt(n * detail)));

            // Peak: a white-hot flash and drawn spark, the fireball billowing up frame by frame from a white-hot heart, then two
            // more lobes rolling up off either side a beat apart, and impact wedges.
            if (big && !follow) ScreenFlash(pos, new Color(1, .8f, .55f, .2f), .12f);
            var shade = FireShade(pos + new Vector2(0, .4f), r * 2.8f, .32f, .85f, 29); shade.hold = .4f;
            if (!follow) { FireLastMeteor = shade; FireLastMeteorRadius = r; }
            if (g >= 2 && !sideStone) FireMeteorCrown(ground, r, g);
            // The peak keeps one size past level one: the larger blast grows its domes, rings and spire, not its glare.
            float peak = Mathf.Min(r * 2.7f, 10.5f), lean = Chance(.5f) ? 1 : -1;
            FireBurst(pos + Vector2.up * r * .32f, peak, FireLight, .95f, 127);
            FireBurst(pos + new Vector2(lean * peak * .2f, peak * .3f), peak * .6f, FireEmberTone, .8f, 125, .08f);
            FireBurst(pos + new Vector2(-lean * peak * .17f, peak * .2f), peak * .5f, FireLight, .7f, 124, .15f);
            FireFlash(pos, Mathf.Min(r * 1.15f, 4.4f), .14f);
            ImpactLines(pos, Mathf.Min(r * 2.6f, 11), Alpha(FireHotTone, .95f), .22f);
            ImpactLines(pos, Mathf.Min(r * 3f, 13), Alpha(FireLight, .75f), .28f).age = -.03f;
            // The pillar of fire keeps a fixed girth (the level grows its spire, not its width), over a slimmer deep red body.
            float columnWidth = Mathf.Min(r * .9f, 3.4f);
            var column = Pillar(ground, columnWidth, 12, Alpha(FireLight, 1), .62f, 128); column.hold = .3f;
            var columnCore = Pillar(ground, columnWidth * .38f, 12, Alpha(FireHotTone, 1), .42f, 129); columnCore.hold = .25f;
            FireColumnInk(ground, columnWidth * .85f, 11, Alpha(FireInkTone, .42f), .5f, 117);

            // Shock: a painted front at the strike, a dome bursting out of the ground and two waves running along it, then a
            // dust ring. Every front ends on the true radius (a dome's front sits at 0.43 of its width, a ring's rim at 0.39).
            Wave(pos, r * 1.5f, Alpha(FireHotTone, 1), .24f, 5);
            FireDome(ground, r * 2.45f, Alpha(FireHotTone, .95f), .38f, true, 130);
            var outerDome = FireDome(ground, r * 2.55f, Alpha(FireLight, .85f), .5f, true, 129); outerDome.age = -.06f; outerDome.ease = 3;
            FireLevel(GroundWave(ground + new Vector2(0, .05f), r * 2.72f, Alpha(FireHotTone, .95f), .38f));
            FireLevel(GroundWave(ground + new Vector2(0, .05f), r * 2.8f, Alpha(FireLight, .8f), .5f)).age = -.08f;
            var dustRing = Spawn("Meteor dust ring", ring, ground + new Vector2(0, .1f), r * 2.9f, new Color(.4f, .28f, .21f, .85f), .6f, false, 115);
            dustRing.scale.y *= .3f; dustRing.root.transform.localScale = dustRing.scale; dustRing.startScale = .15f; dustRing.endScale = 1; dustRing.ease = 4; dustRing.fadePower = 1.2f;

            // Painted fire: balls of fire that rise and cool, petals of flame flung up out of the strike, and a wave of fire racing
            // out along the ground both ways.
            var cooling = Ramp(Color.white, new Color(1, .78f, .55f), new Color(1, .45f, .32f));
            int balls = count(6);
            for (int i = 0; i < balls; i++)
            {
                float side = balls == 1 ? 0 : i / (float)(balls - 1) * 2 - 1;
                var ball = FireBallClip(ground + new Vector2(side * r * .45f, Range(.2f, .6f) * r * .6f), r * Range(.55f, .8f), Color.white, Range(.55f, .8f), i % 2 == 0 ? 124 : 122);
                ball.ramp = cooling; ball.velocity = new Vector2(side * r * Range(.6f, 1.2f), r * Range(.5f, 1.3f)); ball.drag = 2.4f; ball.age = -Range(0, .05f);
            }
            int petals = count(10);
            for (int i = 0; i < petals; i++)
            {
                Vector2 d = Direction(15 + (i + Range(.1f, .9f)) / petals * 150);
                bool lit = i % 4 != 3;
                var tongue = FireTongue(pos + d * r * .1f, r * Range(.42f, .58f) * (.7f + .45f * d.y), d * r * Range(2f, 3.2f), Range(.36f, .5f), lit ? 127 : 121, lit);
                tongue.alignVelocity = true; tongue.angleOffset = -90; tongue.drag = 4; tongue.startScale = .5f; tongue.endScale = 1.25f; tongue.ease = 6;
            }
            int wave = count(10);
            for (int i = 0; i < wave; i++)
            {
                float side = i % 2 == 0 ? -1 : 1; bool lit = i % 4 < 3;
                // Speeds chosen so the drag stops the run near the rim (about a third of the launch speed is travelled).
                var tongue = FireTongue(ground + new Vector2(side * r * .2f, .03f), r * Range(.36f, .5f), new Vector2(side * r * Range(1.8f, 2.6f), Range(0, .4f)), Range(.5f, .7f), lit ? 127 : 121, lit);
                tongue.drag = 2.2f; tongue.angle = -side * Range(15, 30); tongue.age = -Range(0, .06f);
            }
            // Fire geysers bursting from the cracks a moment later.
            for (int i = 0; i < 4; i++)
            {
                float x = (i < 2 ? -1 : 1) * r * Range(.35f, .85f);
                var jet = FireTongue(ground + new Vector2(x, .03f), r * Range(.34f, .44f), new Vector2(Range(-.3f, .3f), r * Range(1.1f, 1.5f)), Range(.45f, .6f), 127, true);
                jet.drag = 2.5f; jet.age = -Range(.1f, .3f); jet.startScale = .5f; jet.endScale = 1.4f;
            }
            int dust = count(4);
            for (int i = 0; i < dust; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                var puff = Puff(ground + new Vector2(side * r * .3f, .25f), r * Range(.35f, .5f), new Color(.8f, .7f, .58f, .85f), new Vector2(side * r * Range(1.4f, 2.1f), Range(.2f, .8f)), Range(.6f, .85f), 118);
                puff.drag = 2.5f; puff.endScale = 1.9f; puff.fadeIn = 0;
            }

            // Warm grey smoke: a burst kicked out under the fire, then a mushroom of it rising as the fire burns down.
            InkBurst(pos + Vector2.up * .3f, r * .45f, FireCharTone, count(5), .6f, 118);
            FireSmokeColumn(ground + Vector2.up * r * .3f, count(4), r * .5f, r * .7f, .14f, 1.3f, 111);
            int cap = count(3);
            for (int i = 0; i < cap; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                var puff = Puff(ground + new Vector2(side * Range(.1f, .4f) * r, r * Range(.9f, 1.1f)), r * Range(.55f, .75f), FireCharTone, new Vector2(side * r * Range(.4f, .8f), r * Range(.25f, .45f)), Range(1.1f, 1.4f), 111);
                puff.drag = 1.5f; puff.startScale = .5f; puff.endScale = 1.7f; puff.age = -Range(.2f, .3f); puff.fadeIn = 0; puff.hold = .35f;
            }

            // Rock debris, molten chunks and needle embers.
            FireRubble(ground + Vector2.up * .2f, count(8), r * .35f, r * 2.2f, r * .14f, 1);
            FireChunks(pos, Mathf.RoundToInt(5 * detail), .4f, r * 2.3f, r * .12f, 1.1f);
            Needles(pos, count(8), r * 4.5f, FireEmberTone, .4f, .55f);
            int embers = count(14);
            for (int i = 0; i < embers; i++)
                Spark(pos + Inside(r * .3f), new Vector2(Range(-1, 1) * r * 1.8f, r * Range(1.2f, 3f)), i % 4 == 0 ? Color.white : i % 2 == 0 ? FireHotTone : FireEmberTone,
                    Range(.26f, .4f), Range(.7f, 1.1f), 6, 1).age = -Range(0, .12f);
            FireDriftEmbers(pos + Vector2.up * r * .6f, count(6), r * .8f, .3f);

            // Molten ground: scorch, glowing cracks, a dim afterglow and flames that keep burning for about a second.
            Scorch(ground, r * 2.8f, 2.2f);
            Crack(ground, r * 2.5f, new Color(1, .45f, .1f, .95f), 2f);
            Crack(ground + new Vector2(r * .2f, 0), r * 1.5f, new Color(1, .6f, .2f, .9f), 1.6f);
            var afterglow = GroundGlow(ground, r * 3f, Alpha(FireLight, .35f), 1.4f); afterglow.hold = .3f;
            float spread = r * .75f;
            Emitter(afterglow, 18 * detail, 0, (owner, p) =>
            {
                if (owner.age > owner.life * .8f) return;
                bool lit = Chance(.7f);
                var tongue = FireTongue(p + new Vector2(Range(-spread, spread), .02f), r * Range(.2f, .3f), new Vector2(Range(-.2f, .2f), Range(.3f, .8f)), Range(.35f, .55f), lit ? 126 : 121, lit);
                tongue.drag = 2;
            });
        }
        /// <summary>The newest meteor's shade, so stones landing together wash the screen once.</summary>
        private Clip FireLastMeteor, FireLastZone; private float FireLastMeteorRadius;

        /// <summary>A level-three meteor's side stone: the same blast in brief (a blast lobe and spark, column, dome, a ring on
        /// its true radius, painted fire, warm smoke, rubble, scorch and embers), about forty clips.</summary>
        private void FireMeteorSideImpact(Vector2 pos, Vector2 ground, float r)
        {
            var shade = FireShade(pos + new Vector2(0, .3f), r * 2.4f, .3f, .6f, 29); shade.hold = .3f;
            // Drawn over the main blast's debris (orders 129-134), so the side stones' landings read as their own.
            FireBurst(pos + Vector2.up * r * .25f, r * 2.1f, FireLight, .6f, 131);
            FireFlash(pos, r * 1.2f, .12f);
            ImpactLines(pos, r * 2.2f, Alpha(FireHotTone, .95f), .2f);
            var column = Pillar(ground, Mathf.Min(r * .6f, 2), 8, Alpha(FireLight, 1), .45f, 128); column.hold = .3f;
            FireDome(ground, r * 2.45f, Alpha(FireHotTone, .95f), .38f, true, 130);
            FireLevel(GroundWave(ground + Vector2.up * .05f, r * 2 / .78f, Alpha(FireLight, .9f), .42f));
            for (int i = 0; i < 3; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                var ball = FireBallClip(ground + new Vector2(side * Range(0, .5f) * r, Range(.2f, .5f) * r * .5f), r * Range(.5f, .7f), Color.white, Range(.5f, .7f), 123);
                ball.velocity = new Vector2(side * r * Range(.5f, 1.2f), r * Range(.4f, 1)); ball.drag = 2.4f; ball.ramp = Ramp(Color.white, new Color(1, .5f, .35f));
                bool lit = i != 2;
                var tongue = FireTongue(ground + new Vector2(side * r * .2f, .03f), r * Range(.36f, .48f), new Vector2(side * r * Range(2.2f, 3.4f), Range(0, .4f)), Range(.45f, .6f), lit ? 127 : 121, lit);
                tongue.drag = 2.2f; tongue.angle = -side * Range(15, 30);
                Spark(pos + Inside(r * .25f), new Vector2(Range(-1, 1) * r * 1.6f, r * Range(1.1f, 2.6f)), i % 3 == 0 ? Color.white : FireEmberTone, Range(.26f, .38f), Range(.6f, 1), 6, 1);
            }
            FireSmokeColumn(ground + Vector2.up * r * .3f, 3, r * .5f, r * .6f, .12f, 1.1f, 111);
            FireRubble(ground + Vector2.up * .2f, 4, r * .3f, r * 2.2f, r * .16f, .9f);
            Scorch(ground, r * 2.4f, 1.8f);
            Crack(ground, r * 2, new Color(1, .45f, .1f, .95f), 1.6f);
        }

        /// <summary>A higher-level meteor: fire geysers burst on the rim of the blast (level two); at level three a white-hot
        /// spire climbs out of the crater over a slim deep red body.</summary>
        private void FireMeteorCrown(Vector2 ground, float r, int g)
        {
            int jets = g >= 3 ? 5 : 4;
            for (int i = 0; i < jets; i++)
            {
                float t = i / (float)(jets - 1) * 2 - 1, x = t * r * .92f, delay = Range(.1f, .22f);
                for (int j = 0; j < 2; j++)
                {
                    var jet = FireTongue(ground + new Vector2(x + Range(-.15f, .15f), .03f), r * Range(.28f, .36f), new Vector2(Range(-.3f, .3f), r * Range(.9f, 1.3f)), Range(.45f, .6f), j == 0 ? 127 : 121, j == 0);
                    jet.drag = 2.5f; jet.startScale = .5f; jet.endScale = 1.4f; jet.angle = -t * 10; jet.age = -delay - j * .04f;
                }
            }
            if (g < 3) return;
            // Level three grows upward, not outward: a white-hot spire climbs far above the pillar, and a second pair of
            // geysers bursts a beat later just inside the rim.
            float spireWidth = Mathf.Min(r * .3f, 1.7f);
            var spire = Pillar(ground, spireWidth, 16, Alpha(FireHotTone, .95f), .7f, 129); spire.hold = .3f;
            FireColumnInk(ground, spireWidth * .85f, 15, Alpha(FireInkTone, .42f), .55f, 117);
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1 : 1) * r * Range(.7f, .8f);
                var jet = FireTongue(ground + new Vector2(x, .03f), r * Range(.34f, .42f), new Vector2(Range(-.2f, .2f), r * Range(1.3f, 1.7f)), Range(.5f, .65f), 127, true);
                jet.drag = 2.5f; jet.startScale = .5f; jet.endScale = 1.5f; jet.age = -Range(.28f, .36f);
                var jetBody = FireTongue(ground + new Vector2(x, .03f), r * Range(.38f, .46f), new Vector2(Range(-.2f, .2f), r * Range(1.2f, 1.5f)), Range(.55f, .7f), 121, false);
                jetBody.drag = 2.5f; jetBody.startScale = .5f; jetBody.endScale = 1.5f; jetBody.age = jet.age + .02f;
            }
        }

        // ---------------------------------------------------------------- burning ground (level-three fireball)

        private static Clip FireFlatten(Clip clip, float squash) { clip.scale.y *= squash; if (clip.root) clip.root.transform.localScale = clip.scale; return clip; }

        /// <summary>A zone layer that lives as long as the zone: it spreads out in about 0.25 s and fades over the last half second.</summary>
        private static void FireZoneLayer(Clip clip, float life)
        {
            clip.life = life; clip.loop = clip.frames != null;
            clip.startScale = .3f; clip.endScale = 1; clip.ease = Mathf.Log(.05f) / Mathf.Log(Mathf.Clamp(1 - .25f / life, .01f, .99f));
            clip.fadeIn = .1f; clip.hold = Mathf.Clamp01(1 - .5f / life); clip.fadePower = 1.4f;
        }

        private bool FireZoneEnding(Clip owner)
        {
            float start = owner.life - .8f;
            return owner.age > start && Chance(Mathf.InverseLerp(start, owner.life - .15f, owner.age));
        }

        /// <summary>
        /// Level three: the burning ground a fireball leaves where it lands. A fire seal lies across the burning area (its rim on
        /// the zone radius) over charred ground and glowing cracks; flames lick up all over it (deep red tongues among lit
        /// ones), embers and warm smoke rise out of it. It fades over its last half second.
        /// </summary>
        private void ZoneFireball(Transform root, float radius, float life)
        {
            float r = Mathf.Clamp(radius, .6f, 3), k = Mathf.Clamp(r / 1.3f, .7f, 1.6f);
            Vector2 floor = new Vector2(0, -.57f);
            // Charred ground first, so on pale marble the field reads as burnt ground with fire in it, not as a gold disc; the
            // light stays on the strokes, the cracks and a dim bed of heat.
            var burn = FireFlatten(Attach(root, "Burning ground scorch", scorch, r * 2.6f, new Color(.16f, .07f, .04f, .72f), 24, false, floor), .3f);
            var seal = FireFlatten(Attach(root, "Burning ground seal", FireSealSprite, r * 2 / .885f, Alpha(FireLight, .7f), 26, true, floor), .28f);
            var cracks = FireFlatten(Attach(root, "Burning ground cracks", crack, r * 2.1f, new Color(1, .5f, .15f, .9f), 26, true, floor), .3f); cracks.flicker = .15f;
            var bed = FireFlatten(Attach(root, "Burning ground glow", glow, r * 2.9f, Alpha(FireLight, .32f), 25, true, floor), .35f); bed.pulse = .08f;
            var shade = FireFlatten(Attach(root, "Burning ground shade", glow, r * 3.1f, new Color(.1f, .025f, .01f, .25f), 29, false, floor + Vector2.up * .8f), .7f);
            var layers = new[] { seal, cracks, bed, shade };
            FireZoneLayer(burn, life); foreach (var layer in layers) FireZoneLayer(layer, life);
            Bundle(root, burn, layers);
            // Fireballs of one cast often light the same spot: a second field there burns at about half the rate.
            bool overlap = FireLastZone != null && FireLastZone.age < .3f && clips.Contains(FireLastZone) && FireLastZone.external &&
                ((Vector2)FireLastZone.external.position - (Vector2)root.position).sqrMagnitude < 1.5f;
            if (!overlap) FireLastZone = bed;
            k *= overlap ? .45f : 1;

            // Flames lick up all over the area, tallest in the middle; embers and warm smoke rise out of it.
            Emitter(bed, 20 * k, 0, (owner, p) =>
            {
                if (FireZoneEnding(owner)) return;
                Vector2 o = (Vector2)root.position + floor;
                float x = Range(-.92f, .92f) * r, tall = 1 - .45f * Mathf.Abs(x / r); bool lit = Chance(.65f);
                var tongue = FireTongue(o + new Vector2(x, .02f), Range(.8f, 1.15f) * tall * Mathf.Clamp(k, .8f, 1.25f), new Vector2(Range(-.15f, .15f), Range(.6f, 1.2f) * tall), Range(.35f, .55f), lit ? 126 : 121, lit);
                tongue.drag = 2;
            });
            Emitter(cracks, 7 * k, 0, (owner, p) =>
            {
                if (FireZoneEnding(owner)) return;
                Vector2 o = (Vector2)root.position + floor;
                Spark(o + new Vector2(Range(-.85f, .85f) * r, Range(.1f, .5f)), new Vector2(Range(-.4f, .4f), Range(1.6f, 3.2f)), Chance(.3f) ? FireHotTone : FireEmberTone, Range(.18f, .28f), Range(.45f, .7f), -1, 1.2f);
            });
            Emitter(shade, 2.4f * k, 0, (owner, p) =>
            {
                if (FireZoneEnding(owner)) return;
                Vector2 o = (Vector2)root.position + floor;
                var puff = Puff(o + new Vector2(Range(-.6f, .6f) * r, Range(.7f, 1.1f)), Range(.6f, .9f) * k, FireCharTone, new Vector2(Range(-.2f, .2f), Range(.6f, 1.1f)), Range(.8f, 1.1f), 112);
                puff.drag = 1.2f; puff.endScale = 1.8f; puff.fadeIn = 0; puff.hold = .3f;
            });

            // Ignition: the fire runs out from the centre to the rim in a moment.
            if (overlap) return;
            Vector2 ground = (Vector2)root.position + floor;
            var wave = FireLevel(GroundWave(ground + Vector2.up * .02f, r * 2 / .78f, Alpha(FireLight, .95f), .35f)); wave.ease = 3;
            int run = Budget(6);
            for (int i = 0; i < run; i++)
            {
                float t = run == 1 ? 0 : i / (float)(run - 1) * 2 - 1; bool lit = i % 3 != 2;
                var tongue = FireTongue(ground + new Vector2(t * r * .85f, .02f), Range(.7f, .95f) * Mathf.Clamp(k, .8f, 1.25f), new Vector2(t * .3f, Range(.8f, 1.4f)), Range(.35f, .5f), lit ? 126 : 121, lit);
                tongue.drag = 2.2f; tongue.angle = -t * 12; tongue.age = -Mathf.Abs(t) * .12f;
            }
        }
    }
}
