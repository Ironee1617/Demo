using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Fire family: Fireball and Meteor.
    // Every fire body is drawn twice: an alpha-blended, saturated layer (deep red rims, orange flesh) that keeps its
    // silhouette on pale marble skies, and an additive layer on top that supplies the white-hot bloom on dark floors.
    public sealed partial class TowerVfx
    {
        // Billowing fire clouds that burn white, cool to orange and end as soot (with FireCoolRamp).
        private static Sprite[] FireBillowSprites;
        // A churning ball of fire (looped) and a comet: a white-hot head with a long rolling flame tail (pivot on the head).
        private static Sprite[] FireOrbSprites, FireCometSprites;
        private static Sprite FireStoneSprite, FireSealSprite;
        // A hemispherical shock front standing on the ground (pivot at the bottom centre) for ground blasts.
        private static Sprite FireDomeSprite;
        private static Gradient FireCoolRamp;

        // From a meteor's landing point back up to where it enters the sky (low enough that the sky seal stays on screen).
        private static readonly Vector2 FireMeteorPath = new Vector2(-2.6f, 6.9f);
        private static readonly Color FireHotTone = new Color(1, .95f, .78f), FireDeepTone = new Color(.5f, .06f, .01f), FireEmberTone = new Color(1, .48f, .12f),
            FireSootTone = new Color(.17f, .12f, .1f, .62f);

        private static void BuildFireTextures()
        {
            FireCoolRamp = new Gradient();
            // Burns white, stays orange for about a third of its life, then cools through deep red into thinning soot.
            FireCoolRamp.SetKeys(new[] {
                new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1, .9f, .72f), .16f), new GradientColorKey(new Color(1, .66f, .42f), .32f),
                new GradientColorKey(new Color(.64f, .28f, .17f), .48f), new GradientColorKey(new Color(.33f, .23f, .2f), .68f), new GradientColorKey(new Color(.22f, .18f, .17f), 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .45f), new GradientAlphaKey(.62f, 1) });

            FireDomeSprite = Build("Fire shock dome", 256, 128, (u, v) =>
            {
                float y = (v + 1) * .5f, r = Mathf.Sqrt(u * u + y * y); if (r > 1) return Color.clear;
                float n = Fbm(Mathf.Atan2(y, u) * 2.6f + 3, r * 5 + 1, 3);
                float front = .86f + (n - .5f) * .05f, inside = r < front ? 1 : 0;
                float a = Gauss(r - front, .035f) + Gauss(r - front, .12f) * (inside > 0 ? .4f : .25f) + inside * Mathf.Pow(r / front, 4) * .16f;
                a *= (.65f + .6f * n) * Smooth(0, .05f, y) * (1 - Smooth(.94f, 1, r));
                return White(Mathf.Clamp01(a));
            }, new Vector2(.5f, 0));

            FireBillowSprites = new Sprite[4];
            for (int i = 0; i < FireBillowSprites.Length; i++)
            {
                float seed = i * 13.7f + 5;
                FireBillowSprites[i] = Build("Fire billow " + i, 128, 128, (u, v) =>
                {
                    float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                    float n = Fbm(u * 2.1f + seed, v * 2.1f - seed * .6f, 4), d = Fbm(u * 5.3f - seed, v * 5.3f + seed * 1.3f, 3);
                    float edge = .64f + (n - .5f) * .5f + (d - .5f) * .14f;
                    float inside = 1 - Smooth(edge - .3f, edge + .05f, r);
                    if (inside <= 0) return Color.clear;
                    float depth = Mathf.Clamp01(1 - r / Mathf.Max(.2f, edge));
                    float heat = Mathf.Clamp01(depth * 1.05f + (d - .5f) * .8f + (n - .5f) * .3f + .02f);
                    float light = Mathf.Clamp01(.8f + v * .2f - u * .1f + (d - .5f) * .35f);
                    Color c = fireRamp.Evaluate(heat);
                    return new Color(c.r * light, c.g * light, c.b * light, inside * Mathf.Clamp01(.72f + d * .45f) * Window(r));
                });
            }

            FireOrbSprites = new Sprite[6];
            for (int f = 0; f < FireOrbSprites.Length; f++)
            {
                float phase = f / 6f * Mathf.PI * 2, ox = Mathf.Cos(phase) * .8f, oy = Mathf.Sin(phase) * .8f;
                FireOrbSprites[f] = Build("Fire orb " + f, 128, 128, (u, v) =>
                {
                    float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                    float n = Fbm(u * 2.8f + ox + 3, v * 2.8f + oy + 9, 4), th = Mathf.Atan2(v, u);
                    float licks = Fbm(Mathf.Cos(th) * 1.6f + ox * .7f + 11, Mathf.Sin(th) * 1.6f + oy * .7f + 4, 3);
                    float edge = .6f + (licks - .5f) * .6f + (n - .5f) * .12f;
                    float inside = 1 - Smooth(edge - .18f, edge + .04f, r);
                    if (inside <= 0) return Color.clear;
                    float heat = Mathf.Clamp01(1.2f - r / Mathf.Max(.25f, edge) * 1.05f + (n - .5f) * .7f);
                    Color c = fireRamp.Evaluate(heat); c.a = inside * Mathf.Clamp01(.55f + heat * .7f) * Window(r); return c;
                });
            }

            FireCometSprites = new Sprite[4];
            for (int f = 0; f < FireCometSprites.Length; f++)
            {
                float phase = f / 4f * Mathf.PI * 2, ox = Mathf.Cos(phase) * .6f, oy = Mathf.Sin(phase) * .6f;
                FireCometSprites[f] = Build("Fire comet " + f, 256, 96, (u, v) =>
                {
                    const float head = .64f, radius = .2f, aspect = 96f / 256f;
                    float x = u, y = v * aspect, behind = head - x, t = Mathf.Clamp01(behind / (head + 1));
                    float n = Fbm(x * 3.4f + ox + 7, y * 9 + oy + 2, 4), fine = Fbm(x * 8 + oy * 2 + 1, y * 18 + ox * 2 + 5, 2);
                    float d;
                    if (behind <= 0) d = Mathf.Sqrt(behind * behind + y * y) / radius;
                    else { float w = radius * (1 + .9f * Mathf.Sqrt(t)) * Mathf.Pow(1 - t, .85f); d = Mathf.Abs(y) / Mathf.Max(.004f, w); }
                    d += (n - .5f) * (.35f + 1.3f * t) + (fine - .5f) * .25f;
                    float inside = 1 - Smooth(.7f, 1.02f, d);
                    if (inside <= 0) return Color.clear;
                    float along = 1 - Smooth(.45f, 1, t);
                    float heat = Mathf.Clamp01((1.08f - t * 1.15f) * (1 - Mathf.Clamp01(d) * .5f) + (n - .5f) * .45f);
                    Color c = fireRamp.Evaluate(heat);
                    c.a = Mathf.Clamp01(inside * along * (.45f + .75f * heat)) * (1 - Smooth(.9f, 1, Mathf.Abs(u))) * (1 - Smooth(.85f, 1, Mathf.Abs(v)));
                    return c;
                }, new Vector2(.82f, .5f));
            }

            FireStoneSprite = Build("Fire meteor stone", 192, 192, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), th = Mathf.Atan2(v, u);
                float rr = .8f + .14f * (Fbm(Mathf.Cos(th) * 1.4f + 6, Mathf.Sin(th) * 1.4f + 2, 3) - .5f) * 2;
                float inside = 1 - Smooth(rr - .04f, rr + .01f, r); if (inside <= 0) return Color.clear;
                float n = Fbm(u * 4.5f + 3, v * 4.5f + 8, 4), crater = Smooth(.6f, .7f, Fbm(u * 3 + 11, v * 3 + 1, 3)) * .35f;
                float light = Mathf.Clamp01(.42f + .38f * (-u * .45f + v * .55f) / .8f + (n - .5f) * .55f - crater);
                Color stone = Color.Lerp(new Color(.13f, .08f, .07f), new Color(.46f, .32f, .25f), light);
                float vein = Gauss(Fbm(u * 3.2f + 21, v * 3.2f + 5, 3) - .5f, .035f) * Smooth(.12f, .45f, r / rr);
                float rim = Smooth(.7f, .98f, r / rr) * .8f, heat = Mathf.Clamp01(vein * .95f + rim);
                Color lava = Color.Lerp(new Color(.95f, .28f, .04f), new Color(1, .85f, .45f), Mathf.Clamp01(vein * 1.2f - .2f));
                Color c = Color.Lerp(stone, lava, heat); c.a = inside; return c;
            });

            // A fire seal: two rings, twelve flame glyphs pointing outward, a hexagram and a small sun at the centre.
            FireSealSprite = Build("Fire seal", 384, 384, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float deg = Mathf.Repeat(Mathf.Atan2(v, u) * Mathf.Rad2Deg, 360);
                float a = Gauss(r - .88f, .014f) + Gauss(r - .83f, .009f) * .8f + Gauss(r - .52f, .012f) * .9f + Gauss(r - .48f, .008f) * .6f;
                float tick = Mathf.Abs(Mathf.Repeat(deg + 15, 30) - 15) * Mathf.Deg2Rad * r;
                a += Gauss(tick, .01f) * Band(.83f, .9f, r) * .85f;
                float h = (r - .54f) / .26f;
                if (h > 0 && h < 1)
                {
                    float s = (Mathf.Repeat(deg, 30) - 15) * Mathf.Deg2Rad * r + Mathf.Sin(h * 5.5f) * .02f * h;
                    float half = .075f * Mathf.Pow(1 - h, .75f) * Mathf.Sqrt(Mathf.Min(1, h / .18f));
                    a += Gauss(Mathf.Abs(s) - half, .011f) * .95f + (Mathf.Abs(s) < half ? .3f : 0);
                }
                float seg = 1e9f;
                for (int i = 0; i < 6; i++)
                {
                    float a0 = (90 + i * 120) * Mathf.Deg2Rad, a1 = (90 + (i + 1) * 120) * Mathf.Deg2Rad, turn = i < 3 ? 0 : Mathf.PI / 3;
                    seg = Mathf.Min(seg, SegmentDistance(u, v, Mathf.Cos(a0 + turn) * .46f, Mathf.Sin(a0 + turn) * .46f, Mathf.Cos(a1 + turn) * .46f, Mathf.Sin(a1 + turn) * .46f));
                }
                a += Gauss(seg, .011f) * .85f;
                float ray = Mathf.Abs(Mathf.Repeat(deg + 22.5f, 45) - 22.5f) * Mathf.Deg2Rad * r;
                a += Gauss(r - .14f, .012f) * .9f + (1 - Smooth(.02f, .12f, r)) * .5f + Gauss(ray, .01f) * Band(.16f, .27f, r) * .8f;
                a += (1 - Smooth(.1f, .86f, r)) * .07f;
                return White(Mathf.Clamp01(a) * Window(r));
            });
            // The skill-level crown has no texture hook of its own: build it here, at load, so a first level-three cast
            // does not build it mid-fight.
            GradeEnsure();
        }

        // ---------------------------------------------------------------- fire builders

        /// <summary>A flame tongue. Alpha-blended tongues are deep orange-red and read on pale skies; additive ones glow.</summary>
        private Clip FireTongue(Vector2 pos, float width, Vector2 velocity, float life, int order, bool additive)
        {
            var clip = Spawn("Flame tongue", flame[random.Next(flame.Length)], pos, width, additive ? new Color(1, .86f, .6f, .95f) : new Color(1, .62f, .42f, 1), life, additive, order);
            clip.frames = flame; clip.fps = Range(18, 26); clip.loop = true;
            clip.velocity = velocity; clip.drag = 1.5f; clip.startScale = .7f; clip.endScale = 1.45f; clip.ease = 1; clip.angle = Range(-15, 15); clip.fadePower = 1.3f;
            return clip;
        }

        /// <summary>A billow of fire that cools to soot over its life.</summary>
        private Clip FireCloud(Vector2 pos, float width, Vector2 velocity, float life, int order)
        {
            var clip = Spawn("Fire billow", FireBillowSprites[random.Next(FireBillowSprites.Length)], pos, width, Color.white, life, false, order);
            clip.velocity = velocity; clip.drag = 3; clip.startScale = .45f; clip.endScale = 1.6f; clip.ease = 2.2f; clip.spin = Range(-50, 50); clip.angle = Range(-25, 25);
            clip.ramp = FireCoolRamp; clip.fadeIn = .03f; clip.hold = .25f; clip.fadePower = 1.4f;
            return clip;
        }

        /// <summary>Chunks of molten rock (glowing veins and rim) thrown out by a blast, each with a faint heat glow.</summary>
        private void FireChunks(Vector2 pos, int count, float spread, float speed, float size, float life)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = new Vector2(Range(-1, 1) * speed, Range(.6f, 1.3f) * speed);
                var chunk = Spawn("Molten chunk", FireStoneSprite, pos + Inside(spread), size * Range(.6f, 1.2f), new Color(1, .9f, .82f), life * Range(.75f, 1.1f), false, 128);
                chunk.velocity = velocity; chunk.gravity = 23; chunk.spin = Range(-600, 600); chunk.hold = .55f; chunk.fadePower = .8f;
                // Every chunk carries its own heat, so the debris reads as molten rock rather than dark dots.
                var heat = Spawn("Molten chunk glow", glow, chunk.position, chunk.scale.x * FireStoneSprite.bounds.size.x * 2.4f, Alpha(i % 2 == 0 ? FireLight : FireEmberTone, .75f), chunk.life, true, 129);
                heat.velocity = velocity; heat.gravity = 23; heat.hold = .45f; heat.fadePower = 1.2f; heat.pulse = .08f;
            }
        }

        /// <summary>A hemispherical shock front rising out of the ground at <paramref name="ground"/> and racing outwards.</summary>
        private Clip FireDome(Vector2 ground, float width, Color color, float life, bool additive, int order)
        {
            var dome = Spawn("Fire shock dome", FireDomeSprite, ground, width, color, life, additive, order);
            dome.startScale = .12f; dome.endScale = 1; dome.ease = 3.2f; dome.fadePower = 1.3f;
            return dome;
        }

        /// <summary>A dark, slightly red halo. Below 30 it sits behind the actors, so it darkens the sky, not the hero.</summary>
        private Clip FireShade(Vector2 pos, float width, float alpha, float life, int order)
        {
            var clip = Spawn("Fire shade", glow, pos, width, new Color(.1f, .025f, .01f, alpha), life, false, order); clip.fadeIn = .03f; clip.fadePower = 1.2f; return clip;
        }

        /// <summary>A straight streak between two points, additive or alpha-blended.</summary>
        private Clip FireStreak(Vector2 a, Vector2 b, float thickness, Color color, float life, bool additive, int order)
        {
            Vector2 delta = b - a; float length = Mathf.Max(.02f, delta.magnitude);
            var clip = Spawn("Fire streak", streak, (a + b) * .5f, length, color, life, additive, order);
            clip.scale = new Vector3(length / streak.bounds.size.x, thickness / streak.bounds.size.y, 1);
            clip.angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg; clip.ease = 1;
            clip.root.transform.localScale = clip.scale; clip.root.transform.localRotation = Quaternion.Euler(0, 0, clip.angle);
            return clip;
        }

        /// <summary>A seal lying on the ground: a glowing additive copy over a dark scorched copy. It never spins (the squash would wobble).</summary>
        private Clip FireGroundSeal(Vector2 ground, float width, Color color, float life, float darkAlpha)
        {
            var seal = Spawn("Fire ground seal", FireSealSprite, ground, width, color, life, true, 26); seal.scale.y *= .28f; seal.root.transform.localScale = seal.scale;
            seal.fadeIn = .08f; seal.startScale = .55f; seal.endScale = 1; seal.ease = 4; seal.hold = .65f; seal.fadePower = 1.6f; seal.pulse = .03f;
            if (darkAlpha > 0)
            {
                var burn = Spawn("Fire ground seal burn", FireSealSprite, ground, width * 1.02f, new Color(.32f, .04f, .01f, darkAlpha), life, false, 25); burn.scale.y *= .28f; burn.root.transform.localScale = burn.scale;
                burn.fadeIn = .08f; burn.startScale = .55f; burn.endScale = 1; burn.ease = 4; burn.hold = .65f; burn.fadePower = 1.6f;
            }
            return seal;
        }

        /// <summary>A camera-facing spinning seal with a dark alpha copy under it (anchored to the caster when there is one).</summary>
        private Clip FireStandingSeal(Transform anchor, Vector2 pos, float width, Color color, float life, float spin, int order, float darkAlpha)
        {
            var seal = Anchored(anchor, pos, "Fire seal", FireSealSprite, width, color, life, order);
            seal.spin = spin; seal.fadeIn = .05f; seal.startScale = .45f; seal.endScale = 1; seal.ease = 4; seal.hold = .7f; seal.fadePower = 1.6f;
            if (darkAlpha > 0)
            {
                var burn = Anchored(anchor, pos, "Fire seal burn", FireSealSprite, width * 1.03f, new Color(.36f, .04f, .01f, darkAlpha), life, order - 1, false);
                burn.spin = spin; burn.fadeIn = .05f; burn.startScale = .45f; burn.endScale = 1; burn.ease = 4; burn.hold = .7f; burn.fadePower = 1.6f;
            }
            return seal;
        }

        /// <summary>A charging ball of fire riding the caster's hand; it grows to full size at the release and snaps out.</summary>
        private void FireChargeOrb(CastInfo c, Vector2 hand, float size, float life)
        {
            var body = Anchored(c.anchor, hand, "Fire charge body", FireOrbSprites[0], size, Color.white, life, 124, false);
            body.frames = FireOrbSprites; body.fps = 16; body.loop = true; body.spin = -c.facing * 420; body.startScale = .2f; body.endScale = 1; body.ease = .9f; body.hold = .88f; body.fadePower = 2; body.fadeIn = .03f;
            var heart = Anchored(c.anchor, hand, "Fire charge heart", core, size * .48f, FireHotTone, life, 126); heart.startScale = .2f; heart.endScale = 1; heart.ease = .9f; heart.hold = .88f; heart.fadePower = 2; heart.pulse = .1f;
            var bloom = Anchored(c.anchor, hand, "Fire charge bloom", glow, size * 2.8f, Alpha(Color.Lerp(FireLight, c.tint, .15f), .8f), life + .06f, 122);
            bloom.startScale = .3f; bloom.endScale = 1; bloom.ease = 1; bloom.hold = .75f; bloom.fadePower = 1.5f; bloom.pulse = .1f; bloom.fadeIn = .04f;
            var vortex = Anchored(c.anchor, hand, "Fire charge vortex", swirl, size * 1.7f, Alpha(FireHotTone, .55f), life, 125);
            vortex.spin = -c.facing * 720; vortex.startScale = 1.4f; vortex.endScale = .55f; vortex.ease = 1; vortex.fadeIn = .06f; vortex.hold = .8f;
            // A deep red rim under the ball so the charge keeps its outline against pale skies.
            var rim = Anchored(c.anchor, hand, "Fire charge rim", glow, size * 1.9f, new Color(.62f, .08f, .02f, .5f), life, 121, false);
            rim.startScale = .25f; rim.endScale = 1; rim.ease = .9f; rim.hold = .85f; rim.fadePower = 2; rim.fadeIn = .03f;
        }

        /// <summary>Two blades of flame whirling around the hand and closing in on it as the charge peaks.</summary>
        private void FireChargeBlades(CastInfo c, Vector2 hand, float size, float life)
        {
            // Each blade is a saturated alpha-blended crescent (it stays orange on pale marble) under a thinner additive edge.
            for (int i = 0; i < 4; i++)
            {
                bool lit = i >= 2;
                var arc = Anchored(c.anchor, hand, lit ? "Fire charge blade" : "Fire charge blade body", crescent, size * (lit ? .92f : 1),
                    lit ? Alpha(i == 2 ? FireLight : FireEmberTone, .8f) : new Color(1, .38f, .05f, .8f), life, lit ? 127 : 126, lit);
                arc.angle = (i % 2) * 180; arc.spin = -c.facing * 760; arc.startScale = 1.25f; arc.endScale = .45f; arc.ease = 1.4f;
                arc.fadeIn = .1f; arc.hold = .82f; arc.fadePower = 2;
            }
        }

        /// <summary>Flame tongues spiralling into a point over <paramref name="life"/> seconds.</summary>
        private void FireInflow(Vector2 pos, int count, float radius, float life, int facing)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                var tongue = FireTongue(pos, Range(.42f, .64f), Vector2.zero, life * Range(.8f, 1), 124, i % 2 == 0);
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
                bool lit = i % 3 == 0;
                var tongue = FireTongue(feet + new Vector2(side * Range(.25f, .65f), .12f), Range(.5f, .8f), new Vector2(side * Range(-.15f, .3f), Range(1.6f, 2.6f)), Range(.3f, .42f), lit ? 127 : 121, lit);
                tongue.age = -Range(0, life * .7f);
                var ember = Spark(feet + new Vector2(Range(-.7f, .7f), Range(0, .6f)), new Vector2(Range(-.4f, .4f), Range(2, 4)), Chance(.3f) ? FireHotTone : FireEmberTone, Range(.14f, .24f), Range(.45f, .7f), -1, 1.2f);
                ember.flicker = .35f; ember.age = -Range(0, life * .8f);
            }
        }

        /// <summary>The release of a cast: a hot flash, an impact frame, a ring and a burst of flame towards the aim.</summary>
        private void FireRelease(Vector2 hand, Vector2 aim, float scale, float age)
        {
            Vector2 dir = aim.sqrMagnitude > .0001f ? aim.normalized : Vector2.right;
            var flash = Spawn("Fire release flash", glow, hand, 1.9f * scale, Alpha(FireHotTone, .85f), .16f, true, 131); flash.ramp = Ramp(Color.white, FireLight); flash.endScale = 1.5f; flash.ease = 1; flash.fadePower = 1.5f; flash.age = age;
            var lines = ImpactLines(hand, 3.2f * scale, Alpha(FireLight, .9f), .18f); lines.age = age;
            var star = Glint(hand, 2.2f * scale, Alpha(FireHotTone, .95f), .2f, 0); star.age = age;
            var wave = Spawn("Fire release ring", ring, hand + dir * .25f, 2.4f * scale, Alpha(FireLight, .95f), .24f, true, 130);
            wave.scale.x *= .42f; wave.angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; wave.startScale = .25f; wave.endScale = 1; wave.ease = 3; wave.fadePower = 1.3f; wave.velocity = dir * 3; wave.age = age;
            int tongues = Budget(5);
            for (int i = 0; i < tongues; i++)
            {
                Vector2 d = (Vector2)(Quaternion.Euler(0, 0, Range(-28, 28)) * dir);
                var tongue = FireTongue(hand, Range(.6f, .95f) * scale, d * Range(5, 8.5f), Range(.18f, .26f), i % 2 == 0 ? 127 : 122, i % 2 == 0);
                tongue.alignVelocity = true; tongue.angleOffset = -90; tongue.drag = 6; tongue.age = age;
            }
            int sparks = Budget(8);
            for (int i = 0; i < sparks; i++)
            {
                Vector2 d = (Vector2)(Quaternion.Euler(0, 0, Range(-40, 40)) * dir);
                Spark(hand, d * Range(5, 10) * scale, i % 3 == 0 ? Color.white : FireEmberTone, Range(.2f, .34f), Range(.22f, .38f), 4, 2.5f).age = age;
            }
            for (int i = 0; i < 2; i++) { var puff = Puff(hand + dir * .3f, Range(.6f, .9f) * scale, FireSootTone, dir * Range(1, 2) + Vector2.up * .6f, Range(.5f, .7f), 113); puff.age = age; puff.endScale = 2.2f; }
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

            var shade = FireShade(c.pos + new Vector2(f * .5f, .2f), 3.8f * gs, .32f, w + .3f, 22); shade.hold = .6f;
            // A fire mandala behind the caster, and a small seal at the palm.
            FireStandingSeal(c.anchor, c.pos + new Vector2(-f * .1f, .25f), 3.1f * gs, Alpha(FireLight, .8f), w + .22f, -f * 110, 23, .5f);
            FireGroundSeal(feet, 3.4f * gs, Alpha(FireLight, .75f), w + .35f, .45f);
            GroundGlow(feet, 3.2f * gs, Alpha(FireLight, .5f), w + .3f);
            FireAura(feet, Mathf.RoundToInt(7 * k), w + .1f);
            // An instant echo launches its fireball with the cast; the projectile's own launch burst is the release.
            if (quick) return;
            var palm = FireStandingSeal(c.anchor, hand + new Vector2(f * .2f, 0), 1.5f * gs, Alpha(FireHotTone, .9f), w + .1f, f * 260, 128, 0);
            palm.startScale = .2f;
            FireChargeOrb(c, hand, 1.6f * gs, w + .03f);
            FireChargeBlades(c, hand, 2f * gs, w + .02f);
            FireInflow(hand, Mathf.RoundToInt(10 * k), 2.2f * gs, w, f);
            Converge(hand, Mathf.RoundToInt(14 * k), 2.6f * gs, FireLight, w, .4f);
            if (g >= 2) FireLevelCharge(c, hand, feet, w, g);
            FireRelease(hand, aim, 1.3f, -(w - .02f));
        }

        /// <summary>
        /// What a higher-level fire cast adds: level two wraps the charge in a turning ring of flame and raises a flame crown
        /// round the caster; level three adds a second, white-hot seal turning the other way and a fire column behind the caster.
        /// </summary>
        private void FireLevelCharge(CastInfo c, Vector2 hand, Vector2 feet, float w, int g)
        {
            int f = c.facing;
            for (int i = 0; i < 2; i++)
            {
                bool lit = i == 1;
                var band = Anchored(c.anchor, hand, lit ? "Fire charge ring" : "Fire charge ring shade", ring, 2.6f + g * .2f, lit ? Alpha(FireLight, .9f) : new Color(.6f, .1f, .02f, .6f), w + .02f, lit ? 127 : 120, lit);
                band.startScale = 1.3f; band.endScale = .35f; band.ease = 1.2f; band.fadeIn = .08f; band.hold = .85f; band.fadePower = 2; band.pulse = .05f;
            }
            int crown = Budget(g == 3 ? 6 : 4);
            for (int i = 0; i < crown; i++)
            {
                float t = crown == 1 ? 0 : i / (float)(crown - 1) * 2 - 1; bool lit = i % 2 == 0;
                var jet = FireTongue(feet + new Vector2(t * 1.1f, .1f), Range(.7f, .95f), new Vector2(t * .3f, Range(2.4f, 3.2f)), Range(.32f, .42f), lit ? 127 : 121, lit);
                jet.drag = 3; jet.startScale = .5f; jet.endScale = 1.35f; jet.age = -Range(w * .35f, Mathf.Max(w * .4f, w - .15f));
            }
            if (g < 3) return;
            var inner = FireStandingSeal(c.anchor, c.pos + new Vector2(-f * .1f, .25f), 2.3f, Alpha(FireHotTone, .85f), w + .2f, f * 170, 24, .35f); inner.startScale = .3f;
            var column = Pillar(feet, 1.5f, 5.5f, Alpha(FireLight, .6f), w + .2f, 22); column.fadeIn = w * .5f; column.hold = .7f; column.fadePower = 1.8f;
            var columnShade = Spawn("Fire cast column shade", pillar, feet, 2.2f, new Color(.5f, .07f, .01f, .4f), w + .2f, false, 21);
            columnShade.scale = new Vector3(2.2f / pillar.bounds.size.x, 5.5f / pillar.bounds.size.y, 1); columnShade.root.transform.localScale = columnShade.scale;
            columnShade.axisStart = new Vector2(.35f, .12f); columnShade.axisEnd = Vector2.one; columnShade.ease = 4; columnShade.fadeIn = w * .5f; columnShade.hold = .7f; columnShade.fadePower = 1.8f;
        }

        private void ProjectileFireball(Transform root, Color tint, bool signature)
        {
            Vector2 dir = root.right; if (dir.sqrMagnitude < .0001f) dir = Vector2.right;
            if (!signature)
            {
                var boltHeart = Attach(root, "Fire bolt heart", core, .32f, FireHotTone, 125); boltHeart.pulse = .08f;
                var halo = Attach(root, "Fire bolt glow", glow, 1.1f, Alpha(FireLight, .8f), 123); halo.pulse = .12f;
                var trail = Attach(root, "Fire bolt tail", FireCometSprites[0], 1.5f, Color.white, 122, false); trail.frames = FireCometSprites; trail.fps = 16;
                Emitter(halo, 14, .08f, (owner, p) =>
                {
                    Vector2 back = owner.external ? -(Vector2)owner.external.right : Vector2.left;
                    Spark(p, back * Range(.8f, 2) + Inside(.8f), Chance(.3f) ? FireHotTone : FireEmberTone, Range(.1f, .18f), Range(.2f, .32f), -1, 2).flicker = .3f;
                });
                Bundle(root, boltHeart, halo, trail);
                return;
            }
            // From level two a cast throws two balls along nearly one path: they braid round each other (opposite phases) so
            // they read as a pair, they swell only a little (two bodies are the growth), and the second one carries no extra
            // light layers or launch burst, so the pair never adds up into one white sun.
            int g = Grade; float gs = 1 + (g - 1) * .07f, rate = 1 + (g - 1) * .15f;
            Vector2 muzzle = root.position;
            bool twin = FireShotFrame == Time.frameCount && (FireShotAt - muzzle).sqrMagnitude < 1;
            if (!twin) { FireShotFrame = Time.frameCount; FireShotAt = muzzle; }
            var heart = Attach(root, "Fireball heart", core, .78f * gs, FireHotTone, 127); heart.pulse = .1f;
            var body = Attach(root, "Fireball body", FireOrbSprites[0], 1.85f * gs, Color.white, 124, false); body.frames = FireOrbSprites; body.fps = 16; body.spin = -620;
            var rim = Attach(root, "Fireball crimson rim", glow, 2.6f * gs, new Color(.7f, .1f, .02f, .5f), 119, false);
            var tailShade = Attach(root, "Fireball tail shade", FireCometSprites[0], 5f * gs, new Color(.5f, .07f, .01f, .55f), 120, false); tailShade.frames = FireCometSprites; tailShade.fps = 14;
            var tail = Attach(root, "Fireball tail", FireCometSprites[0], 4.7f * gs, Color.white, 122, false); tail.frames = FireCometSprites; tail.fps = 14;
            var tailLight = Attach(root, "Fireball tail light", FireCometSprites[0], 5.2f * gs, Alpha(FireLight, .6f), 125); tailLight.frames = FireCometSprites; tailLight.fps = 14;
            // The tail unrolls behind the ball over the first quarter second instead of poking out behind the caster.
            foreach (var layer in new[] { tailShade, tail, tailLight }) { layer.life = 9; layer.ease = 100; layer.axisStart = new Vector2(.15f, .7f); layer.axisEnd = Vector2.one; }
            var halo2 = Attach(root, "Fireball heat", glow, 3.9f * gs, Alpha(Color.Lerp(FireLight, tint, .15f), g >= 2 ? .6f : .75f), 121); halo2.pulse = .12f;
            var shade = Attach(root, "Fireball shade", glow, 3.2f * gs, new Color(.1f, .025f, .01f, .3f), 113, false);
            var vortex = Attach(root, "Fireball vortex", swirl, 2.3f * gs, Alpha(FireHotTone, .5f), 126); vortex.spin = 900;
            var extra = new System.Collections.Generic.List<Clip>();
            if (g >= 2)
            {
                var shell = Attach(root, "Fireball outer shell", FireOrbSprites[3], 2.5f * gs, Alpha(FireLight, twin ? .3f : .5f), 123); shell.frames = FireOrbSprites; shell.fps = 18; shell.spin = 480;
                var shellShade = Attach(root, "Fireball outer shell shade", FireOrbSprites[1], 2.6f * gs, new Color(.6f, .12f, .02f, .6f), 118, false); shellShade.frames = FireOrbSprites; shellShade.fps = 14; shellShade.spin = 380;
                extra.Add(shell); extra.Add(shellShade);
            }
            if (g >= 3)
            {
                // Level three: a ring of fire (deep red under white-hot) and turning blades of flame round the ball.
                var coronaShade = Attach(root, "Fireball corona shade", ring, 1.9f * gs, new Color(.55f, .08f, .01f, .75f), 118, false); coronaShade.pulse = .14f;
                var blade = Attach(root, "Fireball blades", crescent, 2.2f * gs, Alpha(FireLight, .8f), 127); blade.spin = -760;
                extra.Add(coronaShade); extra.Add(blade);
                if (!twin) { var corona = Attach(root, "Fireball corona", ring, 1.8f * gs, Alpha(FireHotTone, .7f), 128); corona.pulse = .14f; extra.Add(corona); }
            }
            // The second ball of a pair drops the wide light layers (heat halo, tail light, vortex) to half.
            if (twin) { halo2.color.a *= .45f; tailLight.color.a *= .5f; vortex.color.a *= .5f; }
            Emitter(body, 24 * rate, .3f * gs, (owner, p) =>
            {
                Vector2 back = owner.external ? -(Vector2)owner.external.right : Vector2.left;
                float trailing = Mathf.Atan2(back.y, back.x) * Mathf.Rad2Deg;
                bool lit = Chance(.4f);
                var tongue = FireTongue(p + back * .25f, Range(.55f, .85f) * gs, back * Range(1, 2.4f) + Vector2.up * Range(.2f, .8f), Range(.22f, .32f), lit ? 123 : 120, lit);
                tongue.angle = trailing - 90 + Range(-20, 20);
            });
            Emitter(halo2, 28 * rate, .25f, (owner, p) =>
            {
                Vector2 back = owner.external ? -(Vector2)owner.external.right : Vector2.left;
                Spark(p, back * Range(1, 3) + Inside(1.6f) + Vector2.up * .5f, Chance(.3f) ? FireHotTone : FireEmberTone, Range(.14f, .26f), Range(.3f, .55f), -1, 1.5f).flicker = .35f;
            });
            Emitter(shade, 12, .3f, (owner, p) =>
            {
                Vector2 back = owner.external ? -(Vector2)owner.external.right : Vector2.left;
                var puff = Puff(p + back * .5f, Range(.5f, .8f), FireSootTone, back * .6f + Vector2.up * Range(.4f, .9f), Range(.5f, .8f), 112); puff.endScale = 2.4f;
            });
            extra.AddRange(new[] { body, rim, tailShade, tail, tailLight, halo2, shade, vortex });
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
            var flash = Spawn("Fireball launch", glow, at, 2.6f, Alpha(FireHotTone, .85f), .15f, true, 131); flash.ramp = Ramp(Color.white, FireLight); flash.endScale = 1.5f; flash.ease = 1; flash.fadePower = 1.5f;
            var boom = Spawn("Fireball launch ring", ring, at + dir * .3f, 2.8f, Alpha(FireLight, .9f), .24f, true, 130);
            boom.scale.x *= .4f; boom.angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; boom.startScale = .2f; boom.endScale = 1; boom.ease = 3; boom.fadePower = 1.3f; boom.velocity = dir * 2.5f;
            var boomShade = Spawn("Fireball launch ring shade", ring, at + dir * .3f, 2.9f, new Color(.55f, .08f, .02f, .5f), .26f, false, 118);
            boomShade.scale.x *= .4f; boomShade.angle = boom.angle; boomShade.startScale = .2f; boomShade.endScale = 1; boomShade.ease = 3; boomShade.fadePower = 1.3f; boomShade.velocity = dir * 2.5f;
            for (int i = 0; i < 5; i++)
            {
                Vector2 d = (Vector2)(Quaternion.Euler(0, 0, Range(-35, 35)) * dir);
                var tongue = FireTongue(at, Range(.6f, .9f), d * Range(4, 7f), Range(.16f, .26f), i % 2 == 0 ? 127 : 122, i % 2 == 0);
                tongue.alignVelocity = true; tongue.angleOffset = -90; tongue.drag = 6;
            }
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
            var flash = Spawn("Fire spark flash", glow, pos, .8f, Alpha(FireHotTone, .85f), .1f, true, 131); flash.endScale = 1.5f; flash.ease = 1; flash.fadePower = 1.4f;
            var heart = Spawn("Fire spark heart", core, pos, .36f, FireHotTone, .08f, true, 133); heart.endScale = .3f; heart.ease = 1;
            var wave = Spawn("Fire spark ring", ring, pos, .95f, Alpha(FireLight, .8f), .16f, true, 131); wave.startScale = .2f; wave.endScale = 1; wave.ease = 3; wave.fadePower = 1.3f;
            Glint(pos, .8f, Alpha(FireHotTone, .9f), .12f, Range(0, 45));
            for (int i = 0; i < 2; i++)
            {
                var tongue = FireTongue(pos + Inside(.1f), Range(.3f, .42f), Direction(Range(30, 150)) * Range(1, 2), Range(.16f, .22f), i == 0 ? 127 : 122, i == 0);
                tongue.drag = 4;
            }
            for (int i = 0; i < 4; i++) Spark(pos, (Direction(Range(0, 360)) + direction * .6f) * Range(2.5f, 4.5f), i % 2 == 0 ? FireHotTone : FireEmberTone, Range(.14f, .22f), Range(.16f, .26f), 6, 3);
        }

        private void ImpactFireball(Vector2 pos, float radius, Color tint, int facing)
        {
            float r = Mathf.Clamp(radius * 1.05f, .8f, 3.2f);
            FireExplosion(pos, r, new Vector2(facing, 0), 1.6f, tint);
            if (Grade >= 2) FireLevelBlast(pos, r, Grade);
        }

        /// <summary>The newest fireball blast's flash (its position is the blast), so fireballs landing together burst once.</summary>
        private Clip FireLastBlast;
        /// <summary>The frame and muzzle of the last fireball launched, so the second ball of a pair knows it is the twin.</summary>
        private int FireShotFrame = -1; private Vector2 FireShotAt;

        /// <summary>A second fireball landing on a blast that just went off: a hot flare, a ring and a few tongues.</summary>
        private void FireBlastEcho(Vector2 pos, float r, Vector2 push)
        {
            Vector2 at = pos + Inside(.4f);
            var flash = Spawn("Fire echo flash", glow, at, r * 1.6f, Alpha(FireHotTone, .9f), .14f, true, 131); flash.ramp = Ramp(Color.white, FireLight); flash.endScale = 1.4f; flash.ease = 1; flash.fadePower = 1.5f;
            Wave(at, r * 1.8f, Alpha(FireLight, .9f), .24f, 3);
            Glint(at, r * 1.2f, Alpha(FireHotTone, 1), .2f, Range(0, 45));
            for (int i = 0; i < 4; i++)
            {
                Vector2 d = Direction(Range(20, 160)); bool lit = i % 2 == 0;
                var tongue = FireTongue(at, r * Range(.35f, .5f), d * r * Range(2, 3) + push * r, Range(.25f, .35f), lit ? 127 : 121, lit);
                tongue.alignVelocity = true; tongue.angleOffset = -90; tongue.drag = 4;
            }
            for (int i = 0; i < 4; i++) Spark(at, Direction(Range(0, 360)) * r * Range(2, 4), i % 2 == 0 ? FireHotTone : FireEmberTone, Range(.18f, .3f), Range(.3f, .5f), 6, 1.5f).flicker = .3f;
        }

        /// <summary>A higher-level blast: flames burst up round the rim of the splash, taller and denser with the level. (The
        /// level-three column is the awakening's: the ground catching fire.)</summary>
        private void FireLevelBlast(Vector2 pos, float r, int g)
        {
            Vector2 ground = pos + Vector2.down * .8f;
            int rim = Budget(5);
            for (int i = 0; i < rim; i++)
            {
                float t = rim == 1 ? 0 : i / (float)(rim - 1) * 2 - 1; bool lit = i % 2 == 0;
                var tongue = FireTongue(ground + new Vector2(t * r * .9f, .1f), r * Range(.3f, .4f), new Vector2(t * .5f, Range(2.5f, 3.5f) * (g >= 3 ? 1.2f : 1)), Range(.35f, .5f), lit ? 127 : 121, lit);
                tongue.drag = 2.5f; tongue.startScale = .4f; tongue.endScale = 1.4f; tongue.age = -(.04f + Mathf.Abs(t) * .06f);
            }
        }

        /// <summary>A soft vertical shade (a stretched glow, no hard edges) behind a column of light, so it reads on pale skies.</summary>
        private Clip FireColumnShade(Vector2 ground, float width, float height, Color color, float life, int order)
        {
            var shade = Spawn("Fire column shade", glow, ground + Vector2.up * height * .45f, width, color, life, false, order);
            shade.scale = new Vector3(width / glow.bounds.size.x, height / glow.bounds.size.y, 1); shade.root.transform.localScale = shade.scale;
            shade.axisStart = new Vector2(.5f, .3f); shade.axisEnd = Vector2.one; shade.ease = 4; shade.hold = .2f; shade.fadePower = 1.4f; shade.fadeIn = .03f;
            return shade;
        }

        /// <summary>The old entry point, kept for callers that still want a plain fire blast.</summary>
        private void Explosion(Vector2 pos, float r, float intensity)
        {
            FireExplosion(pos, r, Vector2.zero, Mathf.Clamp(intensity, .5f, 2), FireLight);
        }

        /// <summary>
        /// A fire blast sized to <paramref name="r"/> (the splash radius): white-hot peak frame, a fireball of billows that
        /// cools to soot, flame tongues, double shock ring, embers with gravity, a smoke column and a scorched, glowing crack.
        /// <paramref name="detail"/> scales the optional particle counts (1 is about 70 clips).
        /// </summary>
        private void FireExplosion(Vector2 pos, float r, Vector2 push, float detail, Color tint)
        {
            Vector2 ground = pos + new Vector2(0, -.8f);
            push = push.sqrMagnitude > .0001f ? push.normalized : Vector2.zero;
            Color bloomTone = Color.Lerp(FireLight, tint, .15f);
            float v = r * 1.1f;

            // Peak frame.
            var shade = FireShade(pos, v * 3f, .42f, .6f, 112); shade.hold = .3f;
            var flash = Spawn("Fire blast flash", glow, pos, v * 2.4f, Color.white, .16f, true, 131); flash.ramp = Ramp(Color.white, FireHotTone, FireLight); flash.startScale = .6f; flash.endScale = 1.35f; flash.ease = 3; flash.fadePower = 1.6f;
            FireLastBlast = flash;
            var heart = Spawn("Fire blast heart", core, pos, v * .75f, FireHotTone, .14f, true, 133); heart.startScale = .35f; heart.endScale = 1.3f; heart.ease = 3; heart.fadePower = 2;
            ImpactLines(pos, v * 3.1f, Alpha(FireLight, .95f), .22f);
            Glint(pos, v * 2f, Alpha(FireHotTone, 1), .22f, Range(-10, 10));
            Glint(pos, v * 1.2f, Alpha(FireLight, 1), .26f, 45).age = -.03f;

            // The fireball: one heavy core billow wrapped in smaller ones; they burn white, then orange, then cool into soot.
            var mass = FireCloud(pos, v * 1.3f, push * v * .5f + Vector2.up * v * .5f, Range(.85f, 1f), 120); mass.startScale = .3f; mass.endScale = 1.45f; mass.drag = 2.5f;
            int billows = Budget(Mathf.RoundToInt(8 * detail));
            for (int i = 0; i < billows; i++)
            {
                Vector2 d = Direction(i * 360f / billows + Range(-25, 25));
                var billow = FireCloud(pos + d * v * Range(.08f, .25f), v * Range(.7f, .95f), d * v * Range(.9f, 1.9f) + push * v * .6f + Vector2.up * v * .55f, Range(.75f, 1.05f), 119);
                billow.age = -Range(0, .04f); billow.startScale = .35f;
            }
            var bloom = Spawn("Fire blast bloom", glow, pos, v * 2.3f, Alpha(bloomTone, .85f), .45f, true, 124); bloom.startScale = .5f; bloom.endScale = 1.2f; bloom.ease = 3; bloom.fadePower = 1.4f;
            // A vortex of flame turning inside the ball (with a deep red copy for pale skies), and an afterburn that keeps
            // lighting the cooling cloud from inside while it rises.
            float turn = push.x < 0 ? 520 : -520;
            var vortex = Swirl(pos, v * 1.9f, Alpha(FireHotTone, .65f), .4f, turn, 125); vortex.startScale = .4f; vortex.endScale = 1.25f; vortex.ease = 3;
            var vortexShade = Spawn("Fire blast vortex shade", swirl, pos, v * 2f, new Color(.5f, .07f, .01f, .5f), .42f, false, 118);
            vortexShade.angle = vortex.angle; vortexShade.spin = turn; vortexShade.startScale = .4f; vortexShade.endScale = 1.3f; vortexShade.ease = 3; vortexShade.fadePower = 1.3f;
            var afterburn = Spawn("Fire blast afterburn", glow, pos, v * 1.7f, Alpha(bloomTone, .6f), .85f, true, 121);
            afterburn.velocity = Vector2.up * v * .5f; afterburn.drag = 1.5f; afterburn.startScale = .6f; afterburn.endScale = 1.1f; afterburn.ease = 2;
            afterburn.fadeIn = .05f; afterburn.hold = .2f; afterburn.fadePower = 1.6f; afterburn.pulse = .08f;
            // A crown of flame jets thrown up out of the top of the ball.
            int jets = Budget(Mathf.RoundToInt(3 * detail));
            for (int i = 0; i < jets; i++)
            {
                float t = jets == 1 ? 0 : i / (float)(jets - 1) * 2 - 1;
                Vector2 d = Direction(90 - t * 34 + Range(-6, 6));
                var jet = FireTongue(pos + d * v * .25f, v * Range(.5f, .66f), d * v * Range(3.6f, 4.6f), Range(.3f, .4f), i % 2 == 0 ? 127 : 122, i % 2 == 0);
                jet.alignVelocity = true; jet.angleOffset = -90; jet.drag = 4.5f; jet.startScale = .5f; jet.endScale = 1.5f; jet.age = -Range(.02f, .06f);
            }

            int tongues = Budget(Mathf.RoundToInt(8 * detail));
            for (int i = 0; i < tongues; i++)
            {
                Vector2 d = Direction(i * 360f / tongues + Range(-18, 18));
                var tongue = FireTongue(pos + d * v * .2f, v * Range(.45f, .65f), d * v * Range(2.4f, 3.8f) + push * v + Vector2.up, Range(.28f, .42f), i % 2 == 0 ? 126 : 121, i % 2 == 0);
                tongue.alignVelocity = true; tongue.angleOffset = -90; tongue.drag = 3.5f; tongue.endScale = 1.3f;
            }

            Wave(pos, v * 2.1f, Alpha(FireHotTone, .95f), .24f, 3.2f);
            // The outer shock is a dome rising out of the ground, so it never pokes through the floor like a full ring.
            // The dome's front sits at 0.43 of its width and a ground ring's rim at 0.37: both end on the splash radius.
            FireDome(ground, v * 2.22f, Alpha(FireLight, .9f), .42f, true, 129).age = -.03f;
            FireDome(ground, v * 2.28f, new Color(.55f, .1f, .03f, .5f), .45f, false, 118).age = -.03f;
            GroundWave(ground, v * 2.5f, Alpha(FireLight, .85f), .42f);
            // Flame running out along the ground both ways.
            int run = Budget(Mathf.RoundToInt(4 * detail));
            for (int i = 0; i < run; i++)
            {
                float side = i % 2 == 0 ? -1 : 1; bool lit = i % 4 < 2;
                var tongue = FireTongue(ground + new Vector2(side * v * .15f, .15f), v * Range(.32f, .45f), new Vector2(side * v * Range(2.6f, 3.6f), Range(0, .5f)), Range(.38f, .52f), lit ? 126 : 121, lit);
                tongue.drag = 2.6f; tongue.angle = -side * Range(10, 25);
            }

            int embers = Budget(Mathf.RoundToInt(16 * detail));
            for (int i = 0; i < embers; i++)
            {
                var ember = Spark(pos + Inside(v * .25f), Direction(Range(15, 165)) * v * Range(1.8f, 4.6f) + push * v * 1.2f, i % 4 == 0 ? Color.white : i % 2 == 0 ? FireHotTone : FireEmberTone,
                    Range(.22f, .4f), Range(.5f, .95f), 7, 1.2f);
                ember.flicker = .3f;
            }
            FireChunks(pos, Mathf.RoundToInt(3 * detail), v * .2f, v * 2.8f, .22f, .85f);

            int smokes = Budget(Mathf.RoundToInt(4 * detail));
            for (int i = 0; i < smokes; i++)
            {
                var puff = Puff(pos + Inside(r * .3f) + Vector2.up * r * .2f, r * Range(.55f, .8f), FireSootTone, new Vector2(Range(-.6f, .6f), Range(1.2f, 2.2f)), Range(1f, 1.4f), 112);
                puff.drag = 1.4f; puff.gravity = -2f; puff.endScale = 2.3f; puff.age = -Range(.08f, .2f); puff.fadeIn = .12f;
            }

            Scorch(ground, r * 2.1f, 1.6f);
            Crack(ground, r * 1.4f, new Color(1, .5f, .15f, .9f), 1.3f);
            GroundGlow(ground, r * 2.4f, Alpha(bloomTone, .55f), 1f);
            int licks = Budget(Mathf.RoundToInt(3 * detail));
            for (int i = 0; i < licks; i++)
            {
                var tongue = FireTongue(ground + new Vector2(Range(-.7f, .7f) * r, .12f), r * Range(.25f, .38f), new Vector2(Range(-.2f, .2f), Range(.5f, 1.1f)), Range(.5f, .8f), i % 2 == 0 ? 126 : 121, i % 2 == 0);
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
            var shade = FireShade(c.pos + new Vector2(0, .9f), 4f, .35f, w + .4f, 22); shade.hold = .6f;
            FireStandingSeal(c.anchor, c.pos + new Vector2(0, .35f), 3.6f, Alpha(FireLight, .85f), w + .3f, -f * 90, 23, .55f);
            if (!quick)
            {
                FireChargeOrb(c, hand, 1.35f * gs, w + .03f);
                FireInflow(hand, Mathf.RoundToInt(8 * k), 1.8f * gs, w, f);
                Converge(hand, Mathf.RoundToInt(12 * k), 2.4f, FireLight, w, .45f);
            }
            float beamLife = w + (quick ? .3f : .2f);
            var beam = Pillar(hand, 1.3f, 10, Alpha(FireLight, .8f), beamLife, 122); beam.hold = .6f; beam.age = quick ? 0 : -.05f;
            var beamCore = Pillar(hand, .45f, 10, Alpha(FireHotTone, .9f), beamLife, 123); beamCore.hold = .55f; beamCore.age = beam.age;
            var beamShade = Spawn("Fire beam shade", pillar, hand, 1.9f, new Color(.55f, .08f, .02f, .45f), beamLife, false, 117);
            beamShade.scale = new Vector3(1.9f / pillar.bounds.size.x, 10 / pillar.bounds.size.y, 1); beamShade.axisStart = new Vector2(.35f, .12f); beamShade.axisEnd = Vector2.one; beamShade.ease = 4; beamShade.hold = .55f; beamShade.fadePower = 1.3f; beamShade.age = beam.age;
            FireGroundSeal(feet, 3.8f, Alpha(FireLight, .75f), w + .4f, .45f);
            GroundGlow(feet, 3.4f, Alpha(FireLight, .5f), w + .35f);
            FireAura(feet, Mathf.RoundToInt(6 * k), w);

            // The sky opens where the meteor will come from.
            var skyShade = Spawn("Meteor sky burn", glow, sky, 8f, new Color(.42f, .06f, .02f, .4f), impactAt, false, 111); skyShade.fadeIn = w * .7f + .05f; skyShade.hold = .7f; skyShade.fadePower = 1.5f;
            var skyGlow = Spawn("Meteor sky glow", glow, sky, 6.5f, Alpha(FireLight, .6f), impactAt, true, 120); skyGlow.fadeIn = w * .7f + .05f; skyGlow.hold = .75f; skyGlow.fadePower = 1.5f; skyGlow.pulse = .06f;
            var rift = Swirl(sky, 3.6f, Alpha(FireHotTone, .7f), impactAt, 300, 121); rift.fadeIn = w * .5f + .05f; rift.hold = .7f; rift.startScale = .4f; rift.endScale = 1.1f; rift.ease = 2;
            var riftShade = Spawn("Meteor rift shade", swirl, sky, 4f, new Color(.32f, .04f, .01f, .6f), impactAt, false, 118); riftShade.spin = 300; riftShade.angle = rift.angle; riftShade.fadeIn = rift.fadeIn; riftShade.hold = .7f; riftShade.startScale = .4f; riftShade.endScale = 1.1f; riftShade.ease = 2;
            var skySeal = Spawn("Meteor sky seal", FireSealSprite, sky, 4.4f * gs, Alpha(FireLight, .85f), impactAt - .1f, true, 122); skySeal.spin = -120; skySeal.fadeIn = .12f; skySeal.startScale = .4f; skySeal.endScale = 1; skySeal.ease = 4; skySeal.hold = .65f; skySeal.fadePower = 1.6f; skySeal.age = -w * .3f;
            if (g >= 2)
            {
                // A higher level opens a wider sky: a second seal turning the other way round the first, over a dark ring.
                var outer = Spawn("Meteor outer sky seal", FireSealSprite, sky, 6.2f * gs, Alpha(new Color(1, .5f, .16f), .7f), impactAt - .05f, true, 121);
                var outerShade = Spawn("Meteor outer sky seal shade", FireSealSprite, sky, 6.3f * gs, new Color(.35f, .04f, .01f, .5f), impactAt - .05f, false, 117);
                foreach (var s in new[] { outer, outerShade }) { s.spin = 70; s.fadeIn = .15f; s.startScale = .3f; s.endScale = 1; s.ease = 3; s.hold = .7f; s.fadePower = 1.6f; s.age = -w * .2f; }
            }
            if (g >= 3)
            {
                // Awakened: two smaller stones fall either side (gameplay: 1.7 units out, 60% radius, 0.2 s after the main one).
                // Each is told by one thin reticle closing on its own landing, inside the main mark: no second seal or glow.
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 at = targetGround + new Vector2(s * 1.7f, 0); float sr = radius * .6f, appear = w * .4f, lands = impactAt + .2f - appear;
                    var sideReticle = Spawn("Meteor side reticle", ring, at, sr * 2 / .74f, Alpha(FireLight, .55f), lands, true, 27); sideReticle.scale.y *= .3f; sideReticle.root.transform.localScale = sideReticle.scale;
                    var sideInk = Spawn("Meteor side reticle ink", ring, at, sr * 2 / .74f * 1.02f, new Color(.45f, .06f, .02f, .3f), lands, false, 25); sideInk.scale.y *= .3f; sideInk.root.transform.localScale = sideInk.scale;
                    foreach (var clip in new[] { sideReticle, sideInk }) { clip.startScale = 1; clip.endScale = .1f; clip.ease = 1.3f; clip.fadeIn = .15f; clip.hold = .85f; clip.fadePower = 1; clip.age = -appear; }
                }
            }

            // Telegraph on the ground: the damage area (the seal's rim sits at 0.88 of its half-width, on the true radius), and a
            // reticle that closes exactly when the meteor lands. The fill is the dark burn; the light keeps to the glyphs and
            // the rim, dimmer at higher levels whose larger mark would otherwise flood the floor.
            var mark = FireGroundSeal(targetGround, radius * 2 / .88f, Alpha(blaze, g >= 3 ? .3f : g == 2 ? .42f : .62f), impactAt + .15f, g >= 2 ? .78f : .7f); mark.hold = .8f; mark.fadePower = 2;
            var areaGlow = GroundGlow(targetGround, radius * 1.7f, Alpha(blaze, g >= 3 ? .1f : g == 2 ? .16f : .26f), impactAt + .1f); areaGlow.startScale = .5f; areaGlow.endScale = 1.1f; areaGlow.ease = 1; areaGlow.fadeIn = Mathf.Max(.1f, w);
            for (int i = 0; i < 2; i++)
            {
                float delay = i * .18f;
                var reticle = Spawn("Meteor reticle", ring, targetGround, radius * 2 / .74f, Alpha(FireLight, .8f), impactAt - delay, true, 27); reticle.scale.y *= .3f; reticle.root.transform.localScale = reticle.scale;
                reticle.startScale = 1; reticle.endScale = .1f; reticle.ease = 1.3f; reticle.fadeIn = .1f; reticle.hold = .85f; reticle.fadePower = 1; reticle.age = -delay;
                var reticleShade = Spawn("Meteor reticle shade", ring, targetGround, radius * 2 / .74f * 1.02f, new Color(.45f, .06f, .02f, .55f), impactAt - delay, false, 25); reticleShade.scale.y *= .3f; reticleShade.root.transform.localScale = reticleShade.scale;
                reticleShade.startScale = 1; reticleShade.endScale = .1f; reticleShade.ease = 1.3f; reticleShade.fadeIn = .1f; reticleShade.hold = .85f; reticleShade.fadePower = 1; reticleShade.age = -delay;
            }
            int heat = Budget(6);
            for (int i = 0; i < heat; i++)
            {
                var ember = Spark(targetGround + new Vector2(Range(-radius, radius) * .8f, .1f), new Vector2(Range(-.3f, .3f), Range(1.5f, 3)), FireEmberTone, Range(.14f, .24f), Range(.5f, .8f), -1, 1.2f);
                ember.flicker = .4f; ember.age = -Range(w * .3f, impactAt - .3f);
            }

            // Release: flash at the hand and a burst of the sky beam.
            float release = quick ? 0 : -(w - .02f);
            if (!quick) FireRelease(hand, Vector2.up, 1.1f, release);
            var burstBeam = Pillar(hand, 2.4f, 11, Alpha(FireLight, .9f), .35f, 124); burstBeam.age = release;
            var burstCore = Pillar(hand, .8f, 11, Alpha(FireHotTone, .95f), .25f, 125); burstCore.age = release;
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

            var shade = Spawn("Meteor shade", glow, start, 4.8f * m, new Color(.08f, .015f, .01f, .42f), life, false, 111); shade.velocity = velocity; shade.fadePower = 0; shade.fadeIn = .05f;
            var bloom = Spawn("Meteor heat", glow, start, 5.8f * m, Alpha(FireLight, .8f), life, true, 120); bloom.velocity = velocity; bloom.fadePower = 0; bloom.pulse = .08f;
            var tailShade = FireStreak(start - dir * 8.5f * m, start - dir * .4f * m, 3.2f * m, new Color(.42f, .06f, .02f, .5f), life, false, 116); tailShade.velocity = velocity; tailShade.fadePower = 0;
            var tailLight = FireStreak(start - dir * 9f * m, start - dir * .2f * m, 2.4f * m, Alpha(FireLight, .5f), life, true, 119); tailLight.velocity = velocity; tailLight.fadePower = 0;
            var tailCore = FireStreak(start - dir * 5.5f * m, start, .8f * m, Alpha(FireHotTone, .55f), life, true, 121); tailCore.velocity = velocity; tailCore.fadePower = 0;
            var mantleShade = Spawn("Meteor mantle shade", FireCometSprites[0], start - dir * .1f, 9.6f * m, new Color(.5f, .07f, .01f, .6f), life, false, 121);
            mantleShade.frames = FireCometSprites; mantleShade.fps = 16; mantleShade.loop = true; mantleShade.velocity = velocity; mantleShade.angle = angle; mantleShade.fadePower = 0;
            var mantle = Spawn("Meteor mantle", FireCometSprites[0], start, 9f * m, Color.white, life, false, 122);
            mantle.frames = FireCometSprites; mantle.fps = 16; mantle.loop = true; mantle.velocity = velocity; mantle.angle = angle; mantle.fadePower = 0;
            var mantleLight = Spawn("Meteor mantle light", FireCometSprites[0], start, 10f * m, Alpha(FireLight, .55f), life, true, 124);
            mantleLight.frames = FireCometSprites; mantleLight.fps = 16; mantleLight.loop = true; mantleLight.velocity = velocity; mantleLight.angle = angle; mantleLight.fadePower = 0;
            var stone = Spawn("Falling meteor", FireStoneSprite, start, 2.3f * m, Color.white, life, false, 125); stone.velocity = velocity; stone.spin = 160; stone.fadePower = 0;
            // The leading edge burns: a tight orange bow shock just ahead of the rock, not a white halo around it.
            var bow = Spawn("Meteor bow shock", crescent, start + dir * .18f * m, 2.25f * m, Alpha(FireLight, .75f), life, true, 127); bow.velocity = velocity; bow.angle = angle; bow.fadePower = 0; bow.pulse = .07f;
            var heart = Spawn("Meteor heart", glow, start + dir * .2f * m, 3f * m, Alpha(FireEmberTone, .55f), life, true, 123); heart.velocity = velocity; heart.fadePower = 0; heart.pulse = .1f;
            if (Grade >= 2 && m > 1)
            {
                // A higher-level stone burns inside a white-hot sheath with a second, wider bow shock.
                var sheath = Spawn("Meteor sheath", FireCometSprites[2], start, 7f * m, Alpha(FireHotTone, .5f), life, true, 126);
                sheath.frames = FireCometSprites; sheath.fps = 20; sheath.loop = true; sheath.velocity = velocity; sheath.angle = angle; sheath.fadePower = 0;
                var wideBow = Spawn("Meteor outer bow", crescent, start + dir * .3f * m, 3.1f * m, Alpha(FireHotTone, .55f), life, true, 127); wideBow.velocity = velocity; wideBow.angle = angle; wideBow.fadePower = 0; wideBow.pulse = .1f;
            }

            float trailing = angle + 180;
            Emitter(stone, 36 * Mathf.Min(1, m * m), .5f * m, (owner, p) =>
            {
                if (Chance(.5f))
                {
                    bool lit = Chance(.4f);
                    var tongue = FireTongue(p - dir * Range(.3f, 1) * m, Range(.8f, 1.3f) * m, -dir * Range(1, 3) + Inside(1), Range(.22f, .34f), lit ? 123 : 120, lit);
                    tongue.angle = trailing - 90 + Range(-15, 15);
                }
                else FireCloud(p - dir * Range(.4f, 1.2f) * m, Range(1f, 1.6f) * m, -dir * Range(1, 2.5f) + Vector2.up * .3f, Range(.45f, .7f), 119);
            });
            Emitter(bloom, 30 * Mathf.Min(1, m * m), .8f * m, (owner, p) =>
            {
                var ember = Spark(p, -dir * Range(1.5f, 4) + Inside(2.2f), Chance(.3f) ? FireHotTone : FireEmberTone, Range(.16f, .3f), Range(.35f, .6f), -1, 1.4f); ember.flicker = .35f;
            });
            Emitter(shade, 15 * Mathf.Min(1, m * m), .6f * m, (owner, p) =>
            {
                var puff = Puff(p - dir * Range(.8f, 1.6f) * m, Range(1.1f, 1.7f) * m, FireSootTone, -dir * Range(.5f, 1.2f) + Vector2.up * .3f, Range(.8f, 1.2f), 113); puff.endScale = 2.6f; puff.drag = 2;
            });

            // Burning fragments torn off on entry fall alongside it and burn out just before it lands.
            int fragments = m < 1 ? 1 : GradeSteady(3);
            for (int i = 0; i < fragments; i++)
            {
                Vector2 side = new Vector2(-dir.y, dir.x) * (i % 2 == 0 ? 1 : -1) * Range(.9f, 1.8f);
                Vector2 at = start + side - dir * Range(.8f, 2.4f);
                float burn = delay * Range(.82f, .95f), size = Range(.5f, .75f);
                var chunk = Spawn("Meteor fragment", FireStoneSprite, at, size, Color.white, burn, false, 125); chunk.velocity = velocity; chunk.spin = Range(-400, 400); chunk.hold = .85f; chunk.fadePower = 1.5f;
                var chunkTail = Spawn("Meteor fragment tail", FireCometSprites[0], at, size * 4.4f, Color.white, burn, false, 122);
                chunkTail.frames = FireCometSprites; chunkTail.fps = 16; chunkTail.loop = true; chunkTail.velocity = velocity; chunkTail.angle = angle; chunkTail.hold = .85f; chunkTail.fadePower = 1.5f;
                var chunkLight = Spawn("Meteor fragment light", FireCometSprites[0], at, size * 5f, Alpha(FireLight, .55f), burn, true, 123);
                chunkLight.frames = FireCometSprites; chunkLight.fps = 16; chunkLight.loop = true; chunkLight.velocity = velocity; chunkLight.angle = angle; chunkLight.hold = .85f; chunkLight.fadePower = 1.5f;
                var chunkGlow = Spawn("Meteor fragment glow", glow, at, size * 2.4f, Alpha(FireLight, .7f), burn, true, 124); chunkGlow.velocity = velocity; chunkGlow.hold = .85f; chunkGlow.fadePower = 1.5f; chunkGlow.pulse = .1f;
                Emitter(chunkGlow, 12, .15f, (owner, p) => Spark(p, -dir * Range(1, 2.5f) + Inside(.8f), FireEmberTone, Range(.1f, .2f), Range(.25f, .45f), -1, 1.5f));
            }

            // Where it breaks into the sky.
            Flash(start, 4.6f * m, Alpha(FireHotTone, .8f), .3f);
            Wave(start, 4.6f * m, Alpha(FireLight, .85f), .4f, 3);
            ImpactLines(start, 5.2f * m, Alpha(FireLight, .6f), .3f);

            // The side stones fall inside the main mark: they add no ground light of their own (it only flooded the floor).
            if (!sideStone)
            {
                // On the ground: its light grows and a shadow gathers under it.
                var shadow = Spawn("Meteor shadow", glow, ground, 4.4f * m, new Color(.04f, .01f, .01f, .55f), delay + .05f, false, 24);
                shadow.scale.y *= .3f; shadow.root.transform.localScale = shadow.scale; shadow.startScale = .25f; shadow.endScale = 1; shadow.ease = 1; shadow.fadeIn = delay * .4f; shadow.fadePower = 0;
                var light = GroundGlow(ground, 5.2f * m, Alpha(new Color(1, .45f, .12f), Grade >= 3 ? .3f : Grade == 2 ? .42f : .55f), delay + .08f); light.startScale = .3f; light.endScale = 1.1f; light.ease = 1; light.fadeIn = delay * .6f; light.hold = .9f; light.fadePower = .6f;
                int rising = Budget(6);
                for (int i = 0; i < rising; i++)
                {
                    var ember = Spark(ground + new Vector2(Range(-2, 2), .1f), new Vector2(Range(-.4f, .4f), Range(1.5f, 3.2f)), FireEmberTone, Range(.14f, .24f), Range(.4f, .6f), -1, 1.2f);
                    ember.flicker = .4f; ember.age = -Range(0, delay * .8f);
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
            var shade = FireStreak(from, to, 2.8f * t0, new Color(.4f, .06f, .02f, .55f), .7f, false, 114); shade.fadePower = 1.4f;
            var burn = FireStreak(from, to, 1.9f * t0, Alpha(FireLight, .75f), .45f, true, 127); burn.fadePower = 1.5f; burn.startScale = 1; burn.endScale = 1.15f;
            var white = FireStreak(from + dir * length * .3f, to, .6f * t0, Alpha(FireHotTone, .9f), .28f, true, 128); white.fadePower = 1.4f;
            if (sideStone) return;
            int puffs = count(10);
            for (int i = 0; i < puffs; i++)
            {
                float t = (i + Range(.1f, .9f)) / puffs;
                var puff = Puff(Vector2.Lerp(from, to, t) + Inside(.3f), Mathf.Lerp(1.8f, 1.1f, t), FireSootTone, new Vector2(Range(-.4f, .4f), Range(.1f, .5f)), Range(1f, 1.4f), 112);
                puff.drag = 1.5f; puff.endScale = 2.6f; puff.startScale = .8f; puff.fadeIn = .05f;
            }
            int billows = count(4);
            for (int i = 0; i < billows; i++)
            {
                float t = Range(.65f, 1);
                FireCloud(Vector2.Lerp(from, to, t) + Inside(.3f), Range(1.2f, 1.7f), -dir * Range(.5f, 1.5f) + Vector2.up * .4f, Range(.5f, .7f), 119);
            }
            int tongues = count(6);
            for (int i = 0; i < tongues; i++)
            {
                float t = Range(.4f, 1); bool lit = i % 2 == 0;
                var tongue = FireTongue(Vector2.Lerp(from, to, t) + Inside(.25f), Range(.7f, 1.1f), -dir * Range(.5f, 1.5f), Range(.25f, .4f), lit ? 128 : 121, lit);
                tongue.angle = Mathf.Atan2(-dir.y, -dir.x) * Mathf.Rad2Deg - 90 + Range(-15, 15);
            }
            int embers = count(12);
            for (int i = 0; i < embers; i++)
            {
                float t = Range(0, 1);
                Spark(Vector2.Lerp(from, to, t) + Inside(.4f), -dir * Range(.5f, 2) + Inside(1.5f), Chance(.3f) ? FireHotTone : FireEmberTone, Range(.14f, .26f), Range(.4f, .8f), 1.5f, 1.5f).flicker = .35f;
            }
        }

        private void ImpactMeteor(Vector2 pos, float radius, Color tint, int facing)
        {
            float r = Mathf.Clamp(radius, .9f, 6.5f);
            bool big = r >= 2.5f;
            Vector2 ground = pos + Vector2.down;
            Color bloomTone = Color.Lerp(FireLight, tint, .12f);
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

            // Peak frame: white-hot flash, an impact frame across the screen, glints and a fire pillar.
            if (big && !follow) ScreenFlash(pos, new Color(1, .8f, .55f, .22f), .12f);
            var shade = FireShade(pos + new Vector2(0, .4f), r * 2.7f, .5f, .9f, 111); shade.hold = .4f;
            if (!follow) { FireLastMeteor = shade; FireLastMeteorRadius = r; }
            if (g >= 2 && !sideStone) FireMeteorCrown(ground, r, g);
            // The white peak keeps one size past level one: the larger blast grows its domes, rings and spire, not its glare.
            var flash = Spawn("Meteor flash", glow, pos, Mathf.Min(r * 2f, 7), Color.white, .2f, true, 132); flash.ramp = Ramp(Color.white, FireHotTone, FireLight); flash.startScale = .5f; flash.endScale = 1.4f; flash.ease = 3; flash.fadePower = 1.8f;
            var heart = Spawn("Meteor heart", core, pos, Mathf.Min(r * .7f, 2.5f), FireHotTone, .18f, true, 133); heart.startScale = .3f; heart.endScale = 1.3f; heart.ease = 3; heart.fadePower = 2;
            // A deep red shock ring drawn over the white-hot ball gives the peak frame a hard outline, even on pale marble.
            var flashRim = Spawn("Meteor flash rim", ring, pos, r * 2.3f, new Color(.7f, .12f, .02f, .75f), .24f, false, 133);
            flashRim.startScale = .35f; flashRim.endScale = 1.1f; flashRim.ease = 3; flashRim.fadePower = 1.5f;
            ImpactLines(pos, Mathf.Min(r * 2.6f, 11), Alpha(FireHotTone, .95f), .24f);
            ImpactLines(pos, Mathf.Min(r * 3f, 13), Alpha(FireLight, .7f), .3f).age = -.03f;
            Glint(pos, Mathf.Min(r * 2.4f, 8), Warm, .26f, 0);
            Glint(pos, Mathf.Min(r * 1.5f, 5.5f), Alpha(FireLight, 1), .3f, 45).age = -.04f;
            Glint(pos + new Vector2(r * .45f, r * .35f), Mathf.Min(r * .8f, 3), Alpha(FireHotTone, 1), .22f, 20).age = -.08f;
            // The pillar of fire keeps a fixed girth (the level grows its spire, not its width), over a soft dark shade.
            float columnWidth = Mathf.Min(r * .9f, 3.4f);
            var column = Pillar(ground, columnWidth, 12, Alpha(FireLight, .9f), .7f, 128); column.hold = .3f;
            var columnCore = Pillar(ground, columnWidth * .38f, 12, Alpha(FireHotTone, .95f), .45f, 129); columnCore.hold = .25f;
            FireColumnShade(ground, Mathf.Min(columnWidth * 1.3f, 3.6f), 11, new Color(.5f, .08f, .02f, .45f), .5f, 117);

            // Shock: a tight ring at the strike, two fiery domes bursting out of the ground and two waves running along it, then
            // a dust ring. Every front ends on the true radius (a dome's front sits at 0.43 of its width, a ring's rim at 0.37).
            Wave(pos, r * 1.5f, Alpha(FireHotTone, .95f), .26f, 3.5f);
            FireDome(ground, r * 2.45f, Alpha(FireHotTone, .9f), .4f, true, 130);
            var outerDome = FireDome(ground, r * 2.55f, Alpha(FireLight, .7f), .55f, true, 129); outerDome.age = -.06f; outerDome.ease = 2.6f;
            var domeShade = FireDome(ground, r * 2.5f, new Color(.55f, .09f, .02f, .55f), .5f, false, 117); domeShade.age = -.02f;
            GroundWave(ground + new Vector2(0, .05f), r * 2.72f, Alpha(FireHotTone, .9f), .4f);
            GroundWave(ground + new Vector2(0, .05f), r * 2.8f, Alpha(FireLight, .75f), .55f).age = -.08f;
            var dustRing = Spawn("Meteor dust ring", ring, ground + new Vector2(0, .1f), r * 2.9f, new Color(.38f, .26f, .2f, .5f), .7f, false, 115);
            dustRing.scale.y *= .3f; dustRing.root.transform.localScale = dustRing.scale; dustRing.startScale = .15f; dustRing.endScale = 1; dustRing.ease = 3; dustRing.fadePower = 1.3f;
            // A dark red copy of the ground shock: the shape of the blast still reads on a white-hot or pale background.
            var groundShade = Spawn("Meteor ground shock shade", ring, ground + new Vector2(0, .05f), r * 2.76f, new Color(.5f, .08f, .02f, .55f), .55f, false, 115);
            groundShade.scale.y *= .3f; groundShade.root.transform.localScale = groundShade.scale; groundShade.startScale = .12f; groundShade.endScale = 1; groundShade.ease = 3; groundShade.fadePower = 1.3f; groundShade.age = -.06f;

            // The fire dome: billows that burn white and cool to soot.
            int billows = count(14);
            for (int i = 0; i < billows; i++)
            {
                float side = Range(-1, 1);
                var billow = FireCloud(ground + new Vector2(side * r * .6f, Range(.1f, .6f) * r * .6f), r * Range(.45f, .7f), new Vector2(side * r * Range(.6f, 1.6f), r * Range(.3f, 1.2f)), Range(1f, 1.45f), 119);
                billow.age = -Range(0, .05f); billow.startScale = .6f;
            }
            for (int i = 0; i < 3; i++)
            {
                var domeLight = Spawn("Meteor dome light", glow, ground + new Vector2(Range(-.3f, .3f) * r, r * .35f), r * 1.6f, Alpha(bloomTone, .75f), .55f, true, 124);
                domeLight.startScale = .5f; domeLight.endScale = 1.2f; domeLight.ease = 3; domeLight.fadePower = 1.4f; domeLight.age = -(i + 1) * .05f;
            }
            int tongues = count(10);
            for (int i = 0; i < tongues; i++)
            {
                Vector2 d = Direction(Range(15, 165));
                var tongue = FireTongue(pos + d * r * .15f, r * Range(.3f, .45f), d * r * Range(2f, 3.5f), Range(.4f, .6f), i % 2 == 0 ? 127 : 121, i % 2 == 0);
                tongue.alignVelocity = true; tongue.angleOffset = -90; tongue.drag = 3; tongue.endScale = 1.3f;
            }
            // A wave of fire racing out along the ground both ways.
            int wave = count(12);
            for (int i = 0; i < wave; i++)
            {
                float side = i % 2 == 0 ? -1 : 1; bool lit = i % 4 < 2;
                // Speeds chosen so the drag stops the run near the rim (about a third of the launch speed is travelled).
                var tongue = FireTongue(ground + new Vector2(side * r * .2f, .2f), r * Range(.28f, .4f), new Vector2(side * r * Range(1.8f, 2.6f), Range(0, .6f)), Range(.5f, .75f), lit ? 127 : 121, lit);
                tongue.drag = 2.2f; tongue.angle = -side * Range(10, 25); tongue.age = -Range(0, .06f);
            }
            int dust = count(6);
            for (int i = 0; i < dust; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                var puff = Puff(ground + new Vector2(side * r * .3f, .25f), r * Range(.35f, .5f), DustTone, new Vector2(side * r * Range(1.4f, 2.1f), Range(.2f, .8f)), Range(.8f, 1.1f), 118);
                puff.drag = 2.5f; puff.endScale = 2.4f;
            }
            // Fire geysers bursting from the cracks a moment later.
            for (int i = 0; i < 4; i++)
            {
                float x = (i < 2 ? -1 : 1) * r * Range(.35f, .85f);
                for (int j = 0; j < 2; j++)
                {
                    var jet = FireTongue(ground + new Vector2(x, .1f), r * Range(.26f, .34f), new Vector2(Range(-.3f, .3f), r * Range(2, 2.8f)), Range(.45f, .6f), j == 0 ? 127 : 121, j == 0);
                    jet.drag = 2.5f; jet.age = -Range(.1f, .3f); jet.startScale = .5f; jet.endScale = 1.5f;
                }
            }

            // Mushroom cloud: a rising column with a cap spreading sideways.
            int stem = count(5);
            for (int i = 0; i < stem; i++)
            {
                var puff = Puff(ground + new Vector2(Range(-.25f, .25f) * r, r * Range(.15f, .6f)), r * Range(.5f, .7f), FireSootTone, new Vector2(Range(-.3f, .3f), r * Range(.4f, .7f)), Range(1.4f, 1.9f), 111);
                puff.drag = 1.2f; puff.gravity = -1.5f; puff.endScale = 2.2f; puff.age = -Range(.08f, .2f); puff.fadeIn = .12f;
            }
            int cap = count(4);
            for (int i = 0; i < cap; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                var puff = Puff(ground + new Vector2(side * Range(.1f, .4f) * r, r * Range(.9f, 1.1f)), r * Range(.6f, .8f), FireSootTone, new Vector2(side * r * Range(.4f, .8f), r * Range(.25f, .45f)), Range(1.3f, 1.7f), 111);
                puff.drag = 1.5f; puff.endScale = 2.3f; puff.age = -Range(.2f, .3f); puff.fadeIn = .15f;
            }
            // A fireball rising up the stem into the cap, burning out to soot as it climbs.
            int rising = count(6);
            for (int i = 0; i < rising; i++)
            {
                var billow = FireCloud(ground + new Vector2(Range(-.2f, .2f) * r, r * Range(.25f, .5f)), r * Range(.45f, .6f), new Vector2(Range(-.4f, .4f) * r, r * Range(1.6f, 2.4f)), Range(1f, 1.3f), 119);
                billow.drag = 2.2f; billow.age = -Range(.02f, .1f); billow.endScale = 1.8f;
            }

            // Molten ground: scorch, glowing cracks, afterglow and flames that keep burning for about a second.
            Scorch(ground, r * 2.8f, 2.2f);
            Crack(ground, r * 2.5f, new Color(1, .45f, .1f, .95f), 2f);
            Crack(ground + new Vector2(r * .2f, 0), r * 1.5f, new Color(1, .6f, .2f, .9f), 1.6f);
            var afterglow = GroundGlow(ground, r * 3.2f, Alpha(bloomTone, .75f), 1.6f); afterglow.hold = .3f;
            var embersBed = Spawn("Meteor ground fire", glow, ground, r * 2.4f, Alpha(FireLight, .35f), 1.3f, true, 26);
            embersBed.scale.y *= .3f; embersBed.root.transform.localScale = embersBed.scale; embersBed.hold = .5f; embersBed.fadePower = 1.2f;
            float spread = r * .75f;
            Emitter(embersBed, 26 * detail, 0, (owner, p) =>
            {
                bool lit = Chance(.4f);
                var tongue = FireTongue(p + new Vector2(Range(-spread, spread), .1f), r * Range(.16f, .26f), new Vector2(Range(-.2f, .2f), Range(.6f, 1.4f)), Range(.35f, .55f), lit ? 126 : 121, lit);
                tongue.drag = 2;
            });

            // Rock fragments, lava blobs and an ember fountain.
            FireChunks(pos, Mathf.RoundToInt(8 * detail), .4f, r * 2.3f, r * .1f, 1.1f);
            int blobs = count(8);
            for (int i = 0; i < blobs; i++)
                Spark(pos + Inside(.4f), new Vector2(Range(-1, 1) * r * 2, Range(.8f, 1.6f) * r * 2), i % 2 == 0 ? FireEmberTone : FireHotTone, Range(.32f, .45f), Range(.8f, 1.1f), 10, .6f).flicker = .15f;
            int embers = count(26);
            for (int i = 0; i < embers; i++)
            {
                var ember = Spark(pos + Inside(r * .3f), new Vector2(Range(-1, 1) * r * 1.8f, r * Range(1.2f, 3f)), i % 4 == 0 ? Color.white : i % 2 == 0 ? FireHotTone : FireEmberTone,
                    Range(.18f, .32f), Range(.8f, 1.3f), 6, 1);
                ember.flicker = .3f; ember.age = -Range(0, .12f);
            }
        }
        /// <summary>The newest meteor's shade, so stones landing together wash the screen once.</summary>
        private Clip FireLastMeteor, FireLastZone; private float FireLastMeteorRadius;

        /// <summary>A level-three meteor's side stone: the same blast in brief (flash, red rim, column, domes, a ring on its true
        /// radius, billows, ground fire, soot, scorch and embers), about forty clips.</summary>
        private void FireMeteorSideImpact(Vector2 pos, Vector2 ground, float r)
        {
            var shade = FireShade(pos + new Vector2(0, .3f), r * 2.6f, .45f, .7f, 111); shade.hold = .3f;
            // Drawn over the main blast's debris (orders 132-134), so the side stones' landings read as their own.
            var flash = Spawn("Meteor side flash", glow, pos, r * 1.4f, Color.white, .16f, true, 133); flash.ramp = Ramp(Color.white, FireHotTone, FireLight); flash.startScale = .5f; flash.endScale = 1.3f; flash.ease = 3; flash.fadePower = 1.8f;
            var rim = Spawn("Meteor side rim", ring, pos, r * 1.8f, new Color(.7f, .12f, .02f, .8f), .26f, false, 134); rim.startScale = .35f; rim.endScale = 1.1f; rim.ease = 3; rim.fadePower = 1.5f;
            var star = Glint(pos, Mathf.Min(r * 1.3f, 4), Alpha(FireHotTone, 1), .22f, 0); star.renderer.sortingOrder = 134;
            ImpactLines(pos, r * 2.2f, Alpha(FireHotTone, .9f), .22f);
            var column = Pillar(ground, Mathf.Min(r * .6f, 2), 8, Alpha(FireLight, .85f), .5f, 128); column.hold = .3f;
            FireDome(ground, r * 2.45f, Alpha(FireHotTone, .85f), .4f, true, 130);
            FireDome(ground, r * 2.5f, new Color(.55f, .09f, .02f, .5f), .45f, false, 117).age = -.02f;
            GroundWave(ground + Vector2.up * .05f, r * 2 / .74f, Alpha(FireLight, .85f), .45f);
            for (int i = 0; i < 3; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                var billow = FireCloud(ground + new Vector2(side * Range(0, .5f) * r, Range(.1f, .5f) * r * .5f), r * Range(.4f, .6f), new Vector2(side * r * Range(.5f, 1.3f), r * Range(.3f, 1)), Range(.9f, 1.2f), 119);
                billow.startScale = .5f;
                bool lit = i % 4 < 2;
                var tongue = FireTongue(ground + new Vector2(side * r * .2f, .2f), r * Range(.28f, .38f), new Vector2(side * r * Range(2.2f, 3.4f), Range(0, .5f)), Range(.45f, .65f), lit ? 127 : 121, lit);
                tongue.drag = 2.2f; tongue.angle = -side * Range(10, 25);
                Spark(pos + Inside(r * .25f), new Vector2(Range(-1, 1) * r * 1.6f, r * Range(1.1f, 2.6f)), i % 3 == 0 ? Color.white : FireEmberTone, Range(.18f, .3f), Range(.7f, 1.1f), 6, 1).flicker = .3f;
            }
            for (int i = 0; i < 3; i++)
            {
                var puff = Puff(ground + new Vector2(Range(-.3f, .3f) * r, r * Range(.2f, .5f)), r * Range(.45f, .6f), FireSootTone, new Vector2(Range(-.3f, .3f), r * Range(.35f, .6f)), Range(1.2f, 1.6f), 111);
                puff.drag = 1.2f; puff.gravity = -1.5f; puff.endScale = 2.2f; puff.age = -Range(.08f, .2f); puff.fadeIn = .12f;
            }
            Scorch(ground, r * 2.4f, 1.8f);
            Crack(ground, r * 2, new Color(1, .45f, .1f, .95f), 1.6f);
            GroundGlow(ground, r * 2.6f, Alpha(FireLight, .65f), 1.3f).hold = .3f;
        }

        /// <summary>A higher-level meteor: fire geysers burst on the rim of the blast (level two); at level three a white-hot
        /// spire climbs out of the crater inside a second, wider shock dome.</summary>
        private void FireMeteorCrown(Vector2 ground, float r, int g)
        {
            int jets = g >= 3 ? 5 : 4;
            for (int i = 0; i < jets; i++)
            {
                float t = i / (float)(jets - 1) * 2 - 1, x = t * r * .92f, delay = Range(.1f, .22f);
                for (int j = 0; j < 2; j++)
                {
                    var jet = FireTongue(ground + new Vector2(x + Range(-.15f, .15f), .1f), r * Range(.2f, .27f), new Vector2(Range(-.3f, .3f), r * Range(1.7f, 2.3f)), Range(.45f, .6f), j == 0 ? 127 : 121, j == 0);
                    jet.drag = 2.5f; jet.startScale = .5f; jet.endScale = 1.5f; jet.age = -delay - j * .04f;
                }
                var vent = Spawn("Meteor rim vent", glow, ground + new Vector2(x, .2f), r * .55f, Alpha(FireLight, .7f), .5f, true, 125);
                vent.scale.y *= .5f; vent.root.transform.localScale = vent.scale; vent.age = -delay; vent.fadePower = 1.5f; vent.pulse = .1f;
            }
            if (g < 3) return;
            // Level three grows upward, not outward: a white-hot spire climbs far above the pillar, and a second pair of
            // geysers bursts a beat later just inside the rim.
            float spireWidth = Mathf.Min(r * .3f, 1.7f);
            var spire = Pillar(ground, spireWidth, 16, Alpha(FireHotTone, .85f), .8f, 129); spire.hold = .3f;
            FireColumnShade(ground, Mathf.Min(spireWidth * 1.4f, 2.4f), 15, new Color(.5f, .07f, .01f, .4f), .55f, 117);
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1 : 1) * r * Range(.7f, .8f);
                var jet = FireTongue(ground + new Vector2(x, .1f), r * Range(.24f, .3f), new Vector2(Range(-.2f, .2f), r * Range(2.4f, 3)), Range(.5f, .65f), 127, true);
                jet.drag = 2.5f; jet.startScale = .5f; jet.endScale = 1.6f; jet.age = -Range(.28f, .36f);
                var jetBody = FireTongue(ground + new Vector2(x, .1f), r * Range(.28f, .34f), new Vector2(Range(-.2f, .2f), r * Range(2.2f, 2.8f)), Range(.55f, .7f), 121, false);
                jetBody.drag = 2.5f; jetBody.startScale = .5f; jetBody.endScale = 1.6f; jetBody.age = jet.age + .02f;
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
        /// Level three: the burning ground a fireball leaves where it lands. A scorched fire seal lies across the burning area
        /// (its rim on the zone radius) with glowing cracks and a bed of heat; flames lick up all over it (deep orange alpha tongues
        /// under lit ones), embers and soot rise out of it, and the heat glows in the air above. It fades over its last half second.
        /// </summary>
        private void ZoneFireball(Transform root, float radius, float life)
        {
            float r = Mathf.Clamp(radius, .6f, 3), k = Mathf.Clamp(r / 1.3f, .7f, 1.6f);
            Vector2 floor = new Vector2(0, -.57f);
            // Charred ground first: a heavy scorch over the seal's dark burn, so on pale marble the field reads as burnt black and
            // red with fire in it, not as a gold disc; the light stays on the glyphs, the cracks and a modest bed of heat.
            var burn = FireFlatten(Attach(root, "Burning ground scorch", scorch, r * 2.6f, new Color(.07f, .04f, .03f, .8f), 25, false, floor), .3f);
            var sealBurn = FireFlatten(Attach(root, "Burning ground seal burn", FireSealSprite, r * 2 / .88f * 1.02f, new Color(.42f, .05f, .01f, .6f), 24, false, floor), .28f);
            var seal = FireFlatten(Attach(root, "Burning ground seal", FireSealSprite, r * 2 / .88f, Alpha(FireLight, .6f), 26, true, floor), .28f); seal.pulse = .04f;
            var cracks = FireFlatten(Attach(root, "Burning ground cracks", crack, r * 2.1f, new Color(1, .5f, .15f, .9f), 26, true, floor), .3f); cracks.flicker = .15f;
            var bed = FireFlatten(Attach(root, "Burning ground glow", glow, r * 2.9f, Alpha(FireLight, .3f), 26, true, floor), .35f); bed.pulse = .08f;
            var heat = FireFlatten(Attach(root, "Burning ground heat", glow, r * 2.5f, Alpha(FireEmberTone, .32f), 121, true, floor + Vector2.up * .9f), .7f); heat.pulse = .1f;
            var shade = FireFlatten(Attach(root, "Burning ground shade", glow, r * 3.1f, new Color(.1f, .025f, .01f, .3f), 112, false, floor + Vector2.up * .8f), .7f);
            var layers = new[] { sealBurn, seal, cracks, bed, heat, shade };
            FireZoneLayer(burn, life); foreach (var layer in layers) FireZoneLayer(layer, life);
            Bundle(root, burn, layers);
            // Fireballs of one cast often light the same spot: a second field there burns at about half the rate.
            bool overlap = FireLastZone != null && FireLastZone.age < .3f && clips.Contains(FireLastZone) && FireLastZone.external &&
                ((Vector2)FireLastZone.external.position - (Vector2)root.position).sqrMagnitude < 1.5f;
            if (!overlap) FireLastZone = bed;
            k *= overlap ? .45f : 1;

            // Flames lick up all over the area, tallest in the middle; embers and soot rise out of it.
            Emitter(bed, 24 * k, 0, (owner, p) =>
            {
                if (FireZoneEnding(owner)) return;
                Vector2 o = (Vector2)root.position + floor;
                // Six tongues in ten are the deep orange alpha body that holds on pale skies; the rest glow.
                float x = Range(-.92f, .92f) * r, tall = 1 - .45f * Mathf.Abs(x / r); bool lit = Chance(.4f);
                var tongue = FireTongue(o + new Vector2(x, .06f), Range(.7f, 1.05f) * tall * Mathf.Clamp(k, .8f, 1.25f), new Vector2(Range(-.15f, .15f), Range(1.5f, 2.6f) * tall), Range(.35f, .55f), lit ? 126 : 121, lit);
                tongue.drag = 2;
            });
            Emitter(heat, 7 * k, 0, (owner, p) =>
            {
                if (FireZoneEnding(owner)) return;
                Vector2 o = (Vector2)root.position + floor;
                var ember = Spark(o + new Vector2(Range(-.85f, .85f) * r, Range(.1f, .5f)), new Vector2(Range(-.4f, .4f), Range(1.6f, 3.2f)), Chance(.3f) ? FireHotTone : FireEmberTone, Range(.12f, .22f), Range(.5f, .8f), -1, 1.2f);
                ember.flicker = .4f;
            });
            Emitter(shade, 2.4f * k, 0, (owner, p) =>
            {
                if (FireZoneEnding(owner)) return;
                Vector2 o = (Vector2)root.position + floor;
                var puff = Puff(o + new Vector2(Range(-.6f, .6f) * r, Range(.7f, 1.1f)), Range(.7f, 1.1f) * k, FireSootTone, new Vector2(Range(-.2f, .2f), Range(.6f, 1.1f)), Range(1, 1.4f), 112);
                puff.drag = 1.2f; puff.endScale = 2.3f; puff.fadeIn = .15f;
            });

            // Ignition: the fire runs out from the centre to the rim in a moment.
            if (overlap) return;
            Vector2 ground = (Vector2)root.position + floor;
            var wave = GroundWave(ground + Vector2.up * .02f, r * 2 / .74f, Alpha(FireLight, .9f), .35f); wave.ease = 2;
            int run = Budget(6);
            for (int i = 0; i < run; i++)
            {
                float t = run == 1 ? 0 : i / (float)(run - 1) * 2 - 1; bool lit = i % 2 == 0;
                var tongue = FireTongue(ground + new Vector2(t * r * .85f, .08f), Range(.6f, .85f) * Mathf.Clamp(k, .8f, 1.25f), new Vector2(t * .3f, Range(1.6f, 2.4f)), Range(.35f, .5f), lit ? 126 : 121, lit);
                tongue.drag = 2.2f; tongue.age = -Mathf.Abs(t) * .12f;
            }
        }
    }
}
