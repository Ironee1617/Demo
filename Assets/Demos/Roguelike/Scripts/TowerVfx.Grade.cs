using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Skill levels: the extra strokes a level-two or level-three skill adds to any effect, and the awakening a level-three skill
    // shows, painted as light like the skills themselves (smooth ramps, white-hot spines, a soft bloom under every bright
    // accent, no dark outlines or ink twins).
    // Level two adds one clear tier in the skill's own colour: a painted loop at the caster's feet over a soft glow with loops
    // climbing the body, a four-point star flaring over a bloom and a painted loop on the true edge of an area, two crescents
    // circling the head of a shot with a fleck wake, and a lit edge under lasting zones.
    // Level three turns the tier gold: the loops and stars go gold-white, a painted spiked crown with white-hot spines and jewels
    // opens over a gold bloom, a gold rune seal turns on a glow on the floor, a column of light rises and gold stars drift up.
    // Awaken draws the signature burst of each skill's awakened behaviour on top of the skill's own impact.
    public sealed partial class TowerVfx
    {
        private static Sprite GradeHaloSprite, GradeHaloGlowSprite, GradeStarSprite, GradeLoopSprite;
        // Where the stroke of the loop sprite sits (its rim, as a fraction of half its width): edges on a gameplay radius use it.
        private const float GradeLoopRim = .92f;
        private static readonly Color GradeGold = new Color(1, .82f, .42f), GradeHot = new Color(1, .97f, .86f), GradeDeepGold = new Color(1, .52f, .12f);
        // When the last accent was drawn for each repeating effect (quake segments, blade storm ticks, orb touches, phantom swings).
        private readonly Dictionary<SkillEffect, float> GradeAccentTime = new Dictionary<SkillEffect, float>();
        // When the last floor ring and seal were drawn for each effect, so bursts landing together share one.
        private readonly Dictionary<SkillEffect, float> GradeSealTime = new Dictionary<SkillEffect, float>();
        // Where (x, y) and when (z) the last awakening of each effect flourished.
        private readonly Dictionary<SkillEffect, Vector3> GradeAwakenMark = new Dictionary<SkillEffect, Vector3>();
        // The last impact accent: an awakening drawn on that same burst in the same frame keeps only its signature.
        private int GradeImpactFrame = -1; private SkillEffect GradeImpactEffect; private Vector2 GradeImpactAt;
        // The frame and place of the last level-three shot crown, so twin shots of one cast wear a single crown.
        private int GradeShotFrame = -1; private SkillEffect GradeShotEffect; private Vector2 GradeShotAt;

        private static void GradeEnsure()
        {
            GradeLoopEnsure();
            if (GradeHaloSprite != null) return;
            // A saint's crown painted as light: a slender ring band rounded across (gold flanks rising to a white-hot centre line,
            // brush streaks running round it), a fine inner ring, twelve slender tapering rays (long and short in turn) with
            // white-hot spines, and a diamond of light at the foot of each long ray. Every stroke keeps its flanks in the gold
            // body, so no deep rim outlines it; its soft ring bloom (below) is the glow.
            GradeHaloSprite = Build("Grade crown", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .98f) return Color.clear;
                float rad = Mathf.Atan2(v, u), deg = Mathf.Repeat(rad * Mathf.Rad2Deg, 360);
                float band = GradeStroke(Mathf.Abs(r - .56f), .034f, .36f, 1) * (.92f + .22f * (RingNoise(rad, r, 2, 14) - .5f));
                float inner = GradeStroke(Mathf.Abs(r - .47f), .011f, .32f, .7f);
                int slot = (int)((deg + 15) / 30) % 12; float cell = Mathf.Repeat(deg + 15, 30) - 15;
                bool tall = slot % 2 == 0;
                float reach = tall ? .97f : .8f, t = Mathf.Clamp01((r - .54f) / (reach - .54f));
                float halfWidth = (tall ? 5.5f : 4.2f) * Mathf.Pow(1 - t, 1.1f) + .25f;
                float spike = r > .54f && r < reach ? GradeStroke(Mathf.Abs(cell) * Mathf.Deg2Rad * r, halfWidth * Mathf.Deg2Rad * r, .36f, 1 - .2f * t) : 0;
                float jx = cell * Mathf.Deg2Rad * .63f, jy = r - .63f;
                float jewel = tall ? GradeStroke(Mathf.Abs(jx) * 1.3f + Mathf.Abs(jy), .05f, .55f, 1) : 0;
                return White(Mathf.Max(Mathf.Max(band, inner), Mathf.Max(spike, jewel)));
            });
            // The crown's glow: a soft ring of light on its band with a faint fill, so the inside of the crown never reads as a
            // filled disc.
            GradeHaloGlowSprite = Build("Grade crown glow", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                return White((Gauss(r - .56f, .13f) * .85f + Gauss(r - .7f, .28f) * .25f) * Window(r));
            });
            soft.Add(GradeHaloGlowSprite);
            // A crisp four-point star with long tapering rays and four short diagonal ones, white at the heart and along each spine.
            GradeStarSprite = Build("Grade star", 128, 128, (u, v) =>
            {
                float au = Mathf.Abs(u), av = Mathf.Abs(v);
                float alongU = Mathf.Max(0, 1 - au / .97f), alongV = Mathf.Max(0, 1 - av / .97f);
                float ray = Mathf.Max(Mathf.Clamp01(1 - av / (.15f * Mathf.Pow(alongU, 1.3f) + .001f)) * Mathf.Pow(alongU, .3f),
                    Mathf.Clamp01(1 - au / (.15f * Mathf.Pow(alongV, 1.3f) + .001f)) * Mathf.Pow(alongV, .3f));
                float du = Mathf.Abs(u + v) * .7071f, dv = Mathf.Abs(u - v) * .7071f, reachU = Mathf.Max(0, 1 - du / .5f), reachV = Mathf.Max(0, 1 - dv / .5f);
                float diagonal = Mathf.Max(Mathf.Clamp01(1 - dv / (.08f * reachU + .001f)) * reachU, Mathf.Clamp01(1 - du / (.08f * reachV + .001f)) * reachV) * .8f;
                return White(Mathf.Max(ray, diagonal));
            });
        }

        /// <summary>A thin painted loop (a brush line round a circle, streaked, with a fainter second line just inside it):
        /// the level's rings and edges, so they mark a reach without laying a filled band over the actors.</summary>
        private static void GradeLoopEnsure()
        {
            if (GradeLoopSprite != null) return;
            GradeLoopSprite = Build("Grade loop", 512, 512, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .97f || r < .75f) return Color.clear;
                float rad = Mathf.Atan2(v, u), n = RingNoise(rad, r, 2.2f, 30), weight = .75f + .5f * Fbm(Mathf.Cos(rad) * 1.4f + 9, Mathf.Sin(rad) * 1.4f + 4, 2);
                float line = Mathf.Clamp01(1 - Mathf.Abs(r - .9f) / (.03f * weight)) * (.95f + .4f * (n - .5f));
                float echo = Mathf.Clamp01(1 - Mathf.Abs(r - .855f) / .009f) * .42f * Smooth(.35f, .6f, n);
                return White(Mathf.Max(line, echo));
            });
        }

        /// <summary>One painted stroke across <paramref name="distance"/> from its spine: a crisp edge at <paramref name="half"/>,
        /// the flank at <paramref name="floor"/> (inside the colour body, never the deep rim) rising to <paramref name="peak"/>.</summary>
        private static float GradeStroke(float distance, float half, float floor, float peak)
        {
            if (distance >= half + .004f) return 0;
            float s = Mathf.Clamp01(1 - distance / half);
            return (floor + (peak - floor) * Mathf.Pow(s, .6f)) * (1 - Smooth(half - .006f, half + .004f, distance));
        }

        /// <summary>A crisp four-point star snapping open; with <paramref name="bloom"/> above 0 a soft glow of its colour swells
        /// under it.</summary>
        private Clip GradeStar(Vector2 pos, float width, Color color, float life, float angle = 0, int order = 133, float bloom = 0, float delay = 0)
        {
            var star = Spawn("Grade star", GradeStarSprite, pos, width, color, life, true, order);
            star.angle = angle; star.startScale = .25f; star.endScale = 1; star.ease = 5; star.hold = .2f; star.fadePower = 1.4f;
            GradeDelay(star, delay);
            if (bloom > 0)
            {
                var halo = Spawn("Grade star bloom", glow, pos, width * 1.1f, Alpha(color, bloom), life * 1.2f, true, order - 2);
                halo.startScale = .5f; halo.endScale = 1.1f; halo.ease = 3; halo.hold = .2f; halo.fadePower = 1.4f;
                GradeDelay(halo, delay);
            }
            return star;
        }

        /// <summary>A soft gold ring of light under a crown (in place of a dark ink copy), opening with it.</summary>
        private Clip GradeCrownBloom(Vector2 pos, float width, float life, int order, float alpha = .5f)
        {
            var halo = Spawn("Grade crown bloom", GradeHaloGlowSprite, pos, width * 1.08f, Alpha(GradeGold, alpha), life, true, order);
            halo.startScale = .5f; halo.endScale = 1.1f; halo.ease = 6; halo.hold = .25f; halo.fadePower = 1.3f;
            return halo;
        }

        // ---------------------------------------------------------------- helpers

        private static void GradeDelay(Clip clip, float delay)
        {
            if (delay <= 0) return;
            clip.age = -delay; if (clip.renderer) clip.renderer.enabled = false;
        }

        private static Clip GradeFlat(Clip clip, float squash)
        {
            clip.scale.y *= squash; if (clip.root) clip.root.transform.localScale = clip.scale; return clip;
        }

        private static Color GradeTone(SkillEffect effect, Color tint) { return Color.Lerp(Color.Lerp(tint, EffectColor(effect), .5f), Color.white, .12f); }

        /// <summary>How far above the floor gameplay places each skill's burst (negative when it happens in the air).</summary>
        private static float GradeDrop(SkillEffect effect, float radius)
        {
            switch (effect)
            {
                case SkillEffect.Quake: return radius < 1.2f ? 0 : .8f;
                case SkillEffect.Mine: return .3f;
                case SkillEffect.WarBanner: return .4f;
                case SkillEffect.BladeStorm: return .5f;
                case SkillEffect.Vortex: return .7f;
                case SkillEffect.Hook: return .75f;
                case SkillEffect.Meteor: case SkillEffect.DashStrike: case SkillEffect.ShieldPulse: return .95f;
                case SkillEffect.Orbit: return -1;
                default: return .85f;
            }
        }

        // Effects whose impact repeats many times a second; their accent is thinned and kept small.
        private static bool GradeRepeats(SkillEffect effect, float radius)
        {
            return effect == SkillEffect.BladeStorm || effect == SkillEffect.Orbit || effect == SkillEffect.Decoy || effect == SkillEffect.WarBanner ||
                (effect == SkillEffect.Quake && radius < 1.2f);
        }

        /// <summary>
        /// Budget without the level's density boost: for the heaviest parts of effects that already multiply with the level
        /// (more projectiles, links or stones), so a level-three volley grows in size and layers instead of in raw particles.
        /// </summary>
        private int GradeSteady(int count)
        {
            if (count <= 1) return Budget(count);
            return Mathf.Max(1, Mathf.RoundToInt(Budget(count) / (1 + (grade - 1) * .34f)));
        }

        private bool GradeThrottled(SkillEffect effect, float gap)
        {
            float last;
            return GradeAccentTime.TryGetValue(effect, out last) && Time.time - last < gap;
        }

        // Bursts centred on the caster's own body: their tier light stands behind the actors so the hero stays in view.
        private static bool GradeSelfCentred(SkillEffect effect)
        {
            return effect == SkillEffect.IceNova || effect == SkillEffect.HealPulse || effect == SkillEffect.ShieldPulse || effect == SkillEffect.Shockwave ||
                effect == SkillEffect.DashStrike || effect == SkillEffect.BladeStorm || effect == SkillEffect.WarBanner || effect == SkillEffect.Hook;
        }

        // Families that draw their own level-three awakening (blade, grace, earth): the awakening keeps only a small shared chord.
        private static bool GradeOwnAwakening(SkillEffect effect)
        {
            return effect == SkillEffect.Shockwave || effect == SkillEffect.DashStrike || effect == SkillEffect.ShieldPulse || effect == SkillEffect.HealPulse ||
                effect == SkillEffect.Quake;
        }

        // Small placed objects (a sentinel, a mine) drawn just behind the actors: their tier stays small and behind them too.
        private static bool GradeCompact(SkillEffect effect) { return effect == SkillEffect.Sentry || effect == SkillEffect.Mine; }

        // A single foe's brand (curse, mark): its tier stays on the foe, without floor seals, edges or columns.
        private static bool GradeSingleTarget(SkillEffect effect) { return effect == SkillEffect.Curse || effect == SkillEffect.HuntMark; }

        // Families that already raise their own level-three column of light out of the burst: no second gold one on top.
        private static bool GradeOwnColumn(SkillEffect effect)
        {
            return effect == SkillEffect.Fireball || effect == SkillEffect.Meteor || effect == SkillEffect.IceNova || effect == SkillEffect.Quake;
        }

        /// <summary>Crisp stars and flecks rising off the floor around <paramref name="from"/>.</summary>
        private void GradeRise(Vector2 from, float spread, int count, Color color, float delayMax)
        {
            for (int i = 0; i < count; i++)
            {
                bool star = i % 3 == 0;
                var mote = Spawn(star ? "Grade rising star" : "Grade rising fleck", star ? GradeStarSprite : fleck, from + new Vector2(Range(-spread, spread), Range(0, .35f)),
                    star ? Range(.5f, .75f) : Range(.24f, .38f), Alpha(color, 1), Range(.55f, .85f), true, 131);
                mote.velocity = new Vector2(Range(-.25f, .25f), Range(1.4f, 2.6f)); mote.drag = .5f; mote.startScale = .4f; mote.endScale = .5f; mote.ease = 6; mote.hold = .35f; mote.fadePower = 1.3f;
                mote.angle = Range(-15, 15); if (!star) mote.flicker = .3f;
                GradeDelay(mote, Range(0, delayMax));
            }
        }

        /// <summary>A painted loop lying on the floor whose rim sits exactly on <paramref name="radius"/>, over a faint glow of
        /// its colour.</summary>
        private Clip GradeEdge(Vector2 ground, float radius, Color light, float life, float hold)
        {
            var lit = GradeFlat(Spawn("Grade edge", GradeLoopSprite, ground + Vector2.up * .04f, radius * 2 / GradeLoopRim, Alpha(light, .9f), life * .9f, true, 27), .3f);
            lit.startScale = .3f; lit.endScale = 1; lit.ease = 4; lit.hold = hold; lit.fadePower = .8f;
            var halo = GradeFlat(Spawn("Grade edge glow", glow, ground + Vector2.up * .04f, radius * 2.3f, Alpha(light, .3f), life * .8f, true, 26), .3f);
            halo.startScale = .4f; halo.endScale = 1; halo.ease = 4; halo.hold = hold; halo.fadePower = 1.4f;
            return lit;
        }

        /// <summary>The awakened rune seal turning on the floor over a gold glow, its outer ring on <paramref name="radius"/>.</summary>
        private Clip GradeSeal(Vector2 ground, float radius, float life, float spin)
        {
            float width = Mathf.Clamp(radius * 2 / .955f, 1.6f, 13);
            var halo = GradeFlat(Spawn("Grade seal glow", glow, ground + Vector2.up * .03f, width * 1.05f, Alpha(GradeGold, .38f), life, true, 26), .34f);
            halo.fadeIn = .08f; halo.startScale = .6f; halo.endScale = 1; halo.ease = 4; halo.hold = .4f; halo.fadePower = 1.4f;
            return RuneCircle(ground + Vector2.up * .03f, width, Alpha(GradeGold, .8f), life, spin, true);
        }

        /// <summary>A short column of gold light shooting up off the floor, with a white-hot spine, over a tall gold bloom.</summary>
        private void GradeColumn(Vector2 ground, float width, float height, float life, float delay, bool behind = false)
        {
            var bloom = Spawn("Grade column bloom", glow, ground + Vector2.up * height * .45f, height * 1.05f, Alpha(GradeGold, .4f), life, true, behind ? 26 : 127);
            bloom.scale.x *= Mathf.Clamp(width * 2.2f / height, .25f, .8f); bloom.root.transform.localScale = bloom.scale;
            bloom.startScale = .6f; bloom.endScale = 1; bloom.ease = 4; bloom.hold = .3f; bloom.fadePower = 1.4f;
            var column = Pillar(ground, width * .7f, height, Alpha(GradeGold, .75f), life, behind ? 27 : 128); column.hold = .3f;
            var heart = Pillar(ground, width * .22f, height * 1.05f, Alpha(GradeHot, .9f), life * .75f, behind ? 28 : 129); heart.hold = .25f;
            GradeDelay(bloom, delay); GradeDelay(column, delay); GradeDelay(heart, delay);
        }

        private static float GradeShotSize(SkillEffect effect)
        {
            switch (effect)
            {
                case SkillEffect.Fireball: case SkillEffect.Shockwave: return 1.3f;
                case SkillEffect.Boomerang: return 1;
                // A volley flies four to six missiles together: each wears a small tier.
                case SkillEffect.ArcaneVolley: return .65f;
                case SkillEffect.Sentry: return .6f;
                case SkillEffect.Ricochet: return .75f;
                default: return .7f;
            }
        }

        // ---------------------------------------------------------------- overlays

        private void GradeCast(SkillEffect effect, CastInfo c)
        {
            int g = Grade; if (g < 2) return;
            GradeEnsure();
            // Level two paints in the skill's colour; level three turns the same tier gold-white.
            Color light = GradeTone(effect, c.tint), tier = g == 3 ? Color.Lerp(light, GradeGold, .7f) : light;
            float w = c.windup, release = Mathf.Max(0, w - .02f);
            Vector2 feet = c.pos + Vector2.down, head = c.pos + new Vector2(-c.facing * .06f, .8f);
            if (w < .2f)
            {
                // An instant echo: one bright tier star at the chest; the skill's own burst follows at once.
                GradeStar(c.pos + Vector2.up * .1f, g == 3 ? 1.6f : 1.1f, Alpha(tier, 1), .2f, 0, 133, .35f);
                return;
            }
            float life = w + .2f, hold = w / life;

            // Level two: a thin painted loop at the feet over a soft glow and thin loops climbing the body toward the release,
            // all behind the actors so the caster stays clear, and a few flecks rising beside it.
            float ringWidth = g == 3 ? 3.1f : 2.6f;
            var floorGlow = GradeFlat(Spawn("Grade cast glow", glow, feet + Vector2.up * .03f, ringWidth * 1.15f, Alpha(tier, .35f), life, true, 26), .3f);
            var floorRing = GradeFlat(Spawn("Grade cast ring", GradeLoopSprite, feet + Vector2.up * .03f, ringWidth, Alpha(tier, .85f), life, true, 27), .3f);
            foreach (var clip in new[] { floorGlow, floorRing }) { clip.fadeIn = .08f; clip.startScale = .5f; clip.endScale = 1; clip.ease = 3; clip.hold = hold; clip.fadePower = 1.6f; }
            floorRing.fadePower = .8f;
            int bands = g == 3 ? 3 : 2;
            for (int i = 0; i < bands; i++)
            {
                float delay = i * w / (bands + .5f), span = Mathf.Max(.15f, w - delay);
                bool gold = g == 3 && i == bands - 1;
                var band = GradeFlat(Spawn("Grade cast band", GradeLoopSprite, feet + Vector2.up * .1f, 1.9f, Alpha(gold ? GradeHot : tier, .75f), span, true, 29), .28f);
                band.velocity = Vector2.up * 1.9f / span; band.startScale = 1.15f; band.endScale = .75f; band.ease = 1; band.fadeIn = span * .2f; band.fadePower = 1.4f;
                GradeDelay(band, delay);
            }
            GradeRise(feet + new Vector2(0, .1f), .9f, Budget(2), tier, w * .7f);
            if (g < 3) return;

            // Level three, the awakened crown: a gold seal turning on a glow under the feet and a painted spiked crown over a gold
            // bloom behind the head (all behind the actors), two gold stars rising beside the body and, at the release, a gold
            // star flaring at the casting hand.
            var sealGlow = GradeFlat(Spawn("Grade crown seal glow", glow, feet + Vector2.up * .02f, 3.7f, Alpha(GradeGold, .32f), life, true, 25), .32f);
            var seal = RuneCircle(feet + Vector2.up * .02f, 3.4f, Alpha(GradeGold, .8f), life, 50, true);
            foreach (var clip in new[] { sealGlow, seal }) { clip.fadeIn = .08f; clip.startScale = .6f; clip.endScale = 1; clip.ease = 4; clip.hold = hold; clip.fadePower = 1.5f; }
            seal.spin = 50;
            Vector2 crownAt = head + Vector2.up * .3f;
            var crownGlow = Anchored(c.anchor, crownAt, "Grade crown halo bloom", GradeHaloGlowSprite, 1.75f, Alpha(GradeGold, .55f), w + .1f, 27);
            var halo = Anchored(c.anchor, crownAt, "Grade crown halo", GradeHaloSprite, 1.65f, Alpha(GradeGold, 1), w + .1f, 28);
            foreach (var clip in new[] { crownGlow, halo }) { clip.fadeIn = w * .3f; clip.startScale = .4f; clip.endScale = 1; clip.ease = 3; clip.hold = w / (w + .1f); clip.fadePower = .8f; }
            halo.spin = 40 * c.facing; halo.fadePower = .6f; crownGlow.pulse = .06f;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1 : 1;
                var spark = GradeStar(feet + new Vector2(side * Range(.75f, 1.1f), Range(.4f, 1.5f)), Range(.5f, .7f), Alpha(GradeHot, .95f), .4f, Range(-15, 15), 131);
                spark.velocity = Vector2.up * Range(.8f, 1.4f); GradeDelay(spark, Range(.05f, Mathf.Max(.06f, w - .1f)));
            }
            GradeStar(c.pos + new Vector2(c.facing * .8f, .15f), 1.9f, Alpha(GradeGold, .95f), .24f, 0, 133, .4f, release);
        }

        private void GradeImpact(SkillEffect effect, Vector2 pos, float radius, Color tint, int facing)
        {
            int g = Grade; if (g < 2) return;
            bool repeat = GradeRepeats(effect, radius);
            if (repeat && GradeThrottled(effect, .35f)) return;
            GradeEnsure();
            // Level two paints in the skill's colour; level three turns the same tier gold-white.
            Color light = GradeTone(effect, tint), tier = g == 3 ? Color.Lerp(light, GradeGold, .7f) : light;
            float R = Mathf.Clamp(radius, .3f, 7), drop = GradeDrop(effect, radius);
            bool grounded = drop >= 0;
            Vector2 ground = pos + Vector2.down * Mathf.Max(0, drop);
            float scale = repeat ? .65f : 1;
            // A burst round the caster's own body, or a small placed object, keeps its tier behind the actors and its star smaller.
            bool self = GradeSelfCentred(effect) || GradeCompact(effect);
            int behind = GradeCompact(effect) ? 26 : 28;
            GradeImpactFrame = Time.frameCount; GradeImpactEffect = effect; GradeImpactAt = pos;

            // Level two: a star of the tier's colour flaring over a bloom and a painted loop on the area's true edge; repeating
            // hits keep the star.
            var mark = GradeStar(pos, Mathf.Clamp(.8f + R * .35f, 1, 2.6f) * scale * (self ? .7f : 1), Alpha(tier, 1), .24f, 45, self ? behind : 133, repeat ? 0 : .3f);
            GradeAccentTime[effect] = Time.time;
            // Bursts landing together (a meteor's side stones, marks on several foes) share the first one's rings and seal:
            // the followers keep only their star and crown.
            float first;
            bool follow = !repeat && GradeSealTime.TryGetValue(effect, out first) && Time.time - first < .45f;
            if (follow)
            {
                if (g >= 3)
                {
                    var echo = Spawn("Grade crown", GradeHaloSprite, pos, Mathf.Clamp(1 + R * .45f, 1.3f, 3.2f) * .8f, Alpha(GradeGold, .95f), .36f, true, self ? behind : 132);
                    echo.startScale = .5f; echo.endScale = 1.1f; echo.ease = 6; echo.spin = 90; echo.hold = .4f; echo.fadePower = .6f;
                }
                return;
            }
            bool single = GradeSingleTarget(effect);
            if (!repeat && !single)
            {
                if (grounded && R >= .9f) { GradeEdge(ground, R, tier, .5f, .2f); GradeSealTime[effect] = Time.time; }
                else
                {
                    var halo = Spawn("Grade halo ring", GradeLoopSprite, pos, Mathf.Clamp(R * 2.2f, 1.2f, 4), Alpha(tier, .9f), .3f, true, 131);
                    halo.startScale = .3f; halo.endScale = 1; halo.ease = 4; halo.fadePower = 1.3f;
                }
            }
            int motes = Budget(repeat ? 1 : 3);
            for (int i = 0; i < motes; i++)
            {
                var mote = Spawn("Grade fleck", fleck, pos + Inside(Mathf.Min(1.4f, R * .5f)), Range(.24f, .38f), Alpha(i % 2 == 0 ? tier : Color.white, 1), Range(.35f, .55f), true, 131);
                mote.velocity = new Vector2(Range(-.4f, .4f), Range(1.2f, 2.4f)); mote.drag = .8f; mote.flicker = .3f; mote.startScale = .4f; mote.endScale = .5f; mote.ease = 6; mote.hold = .3f;
                mote.angle = Range(-15, 15);
            }
            if (g < 3) return;

            // Level three, the awakened crown: a painted spiked gold crown snapping open over the burst on a gold bloom, a turning
            // rune seal on a glow on the floor at the area's edge, a column of light and gold stars rising.
            float crownWidth = Mathf.Clamp(1 + R * .45f, 1.3f, 3.2f) * scale;
            GradeCrownBloom(pos, crownWidth, .42f, self ? behind - 1 : 126, repeat ? .3f : .42f).spin = 0;
            var crown = Spawn("Grade crown", GradeHaloSprite, pos, crownWidth, Alpha(GradeGold, .95f), .4f, true, self ? behind : 132);
            crown.startScale = .5f; crown.endScale = 1.1f; crown.ease = 6; crown.spin = 90; crown.hold = .4f; crown.fadePower = .6f;
            if (repeat || single) { GradeRise(pos + Vector2.down * .3f, .3f, 1, GradeHot, .05f); return; }
            if (grounded)
            {
                GradeSeal(ground, R, .9f, 60); GradeSealTime[effect] = Time.time;
                if (!GradeOwnColumn(effect) && !GradeCompact(effect)) GradeColumn(ground, Mathf.Clamp(.5f + R * .18f, .7f, 1.4f), Mathf.Clamp(2.2f + R * .35f, 2.4f, 4.5f), .55f, .02f, self);
                GradeRise(ground, Mathf.Min(R * .7f, 2.5f), 4, GradeHot, .3f);
            }
            else GradeRise(pos, .4f, 3, GradeHot, .15f);
        }

        private void GradeProjectile(Transform root, SkillEffect effect, Color tint)
        {
            int g = Grade; if (g < 2 || !root) return;
            GradeEnsure();
            Color light = GradeTone(effect, tint);
            float size = GradeShotSize(effect);
            var layers = new List<Clip>();
            // Shots fired together in one frame (a volley, a fan of arrows, twin fireballs, a turret's burst) share one accent:
            // only the first wears the level's head ornament, the rest keep a thinner wake, so a volley never becomes a row of rings.
            Vector2 at = root.position;
            bool follower = GradeShotFrame == Time.frameCount && GradeShotEffect == effect && (GradeShotAt - at).sqrMagnitude < 2.25f;
            if (!follower) { GradeShotFrame = Time.frameCount; GradeShotEffect = effect; GradeShotAt = at; }
            // Small missiles (arrows, spears, bolts, turret rounds, slugs) only ever take the fleck wake and, at level three, one
            // small white star at the head of the first; the big single shots (a fireball, a sword wave, a disc) take the rest.
            bool big = size >= 1, fire = effect == SkillEffect.Fireball;
            var host = Attach(root, "Grade shot wake", glow, .1f, new Color(0, 0, 0, 0), 121);
            layers.Add(host);
            // Level two: a wake of flecks; on a big shot two thin painted crescents of the skill's colour also circle its head
            // (a fireball already burns white-hot at its heart, so it keeps the wake only).
            if (big && !fire && !follower)
                for (int i = 0; i < 2; i++)
                {
                    var arc = Attach(root, "Grade shot arc", crescent, size * (1.2f - i * .2f), Alpha(i == 0 ? light : Color.Lerp(light, Color.white, .4f), .8f), 122);
                    arc.angle = i * 180; arc.spin = 520; arc.scale.x *= .7f; arc.root.transform.localScale = arc.scale; layers.Add(arc);
                }
            Color wake = g == 3 ? GradeHot : Color.Lerp(light, Color.white, .35f);
            Emitter(host, (g == 3 ? 10 : 7) * (follower ? .35f : big ? 1 : .6f), size * .2f, (owner, p) =>
            {
                Vector2 back = owner.external ? -(Vector2)owner.external.right : Vector2.left;
                var mote = Spawn("Grade shot fleck", fleck, p, Range(.2f, .3f) * Mathf.Max(.8f, size), Alpha(Chance(.5f) ? wake : light, .9f), Range(.2f, .32f), true, 131);
                mote.velocity = back * Range(.6f, 1.6f) + Inside(.6f) + Vector2.up * .3f; mote.drag = 2; mote.flicker = .3f; mote.angle = Range(-15, 15);
                mote.startScale = .5f; mote.endScale = .4f; mote.ease = 6; mote.hold = .3f;
            });
            if (g == 3 && !follower)
            {
                if (big)
                {
                    // Level three on a big shot: a turning painted gold crown with a white star at its heart rides its head over a
                    // faint gold glow behind the shot.
                    float crownSize = size * (fire ? .95f : 1.3f), starSize = size * (fire ? .7f : 1);
                    var halo = Attach(root, "Grade shot glow", GradeHaloGlowSprite, crownSize * 1.1f, Alpha(GradeGold, .45f), 119); halo.pulse = .08f;
                    var crown = Attach(root, "Grade shot crown", GradeHaloSprite, crownSize, Alpha(GradeGold, .9f), 126); crown.spin = -200;
                    var star = Attach(root, "Grade shot star", GradeStarSprite, starSize, Alpha(GradeHot, .9f), 127); star.spin = 160; star.pulse = .1f;
                    layers.Add(halo); layers.Add(crown); layers.Add(star);
                }
                else if (effect != SkillEffect.Sentry && effect != SkillEffect.Ricochet)
                {
                    // A turret's tracers and the tiny ricochet slugs fire many times a cast: they keep the wake only.
                    var star = Attach(root, "Grade shot star", GradeStarSprite, .7f, Alpha(GradeHot, .9f), 127); star.spin = 160;
                    layers.Add(star);
                }
            }
            Clip primary;
            if (attached.TryGetValue(root, out primary) && primary.layers != null) primary.layers.AddRange(layers);
            else Bundle(root, layers[0], layers.GetRange(1, layers.Count - 1).ToArray());
        }

        private void GradeZone(Transform root, SkillEffect effect, float radius, float life)
        {
            int g = Grade; if (g < 2 || !root) return;
            // A fireball's burning ground is itself the level-three behaviour, and its awakening already brings the gold.
            if (effect == SkillEffect.Fireball) return;
            GradeEnsure();
            Color light = Color.Lerp(EffectColor(effect), Color.white, .15f);
            float R = Mathf.Clamp(radius, .4f, 7);
            // A poison cloud turns its own plague seal at level three: the tier keeps only the lit edge, in its own colour.
            bool plague = effect == SkillEffect.PoisonCloud;
            var layers = new List<Clip>();
            if (effect == SkillEffect.Orbit)
            {
                // Orbs: the path they really travel (OrbitOffset), one thin tilted brush line round the hub (gold when
                // awakened), never a band over the hero in the middle.
                var path = Attach(root, "Grade orbit path", GradeLoopSprite, R * 2 / .9f, Alpha(g == 3 ? GradeGold : light, .7f), 119);
                path.scale.y *= OrbitTilt; path.root.transform.localScale = path.scale; layers.Add(path);
            }
            else
            {
                // A squashed child on the floor so seals spin in the ground plane; it dies with the zone root.
                var plane = new GameObject("Grade ground plane").transform; plane.SetParent(root, false);
                plane.localPosition = new Vector3(0, -.57f, 0); plane.localScale = new Vector3(1, .3f, 1);
                // A turret's radius is its firing range: its tier sits at its base instead.
                bool compact = GradeCompact(effect);
                // A falling stone's warning is a brief marked spot: the tier keeps it to one thin loop (the landing brings the seal).
                bool warning = effect == SkillEffect.Meteor;
                float edge = compact ? 1.8f : R * 2 / GradeLoopRim;
                var lit = Attach(plane, "Grade zone edge", GradeLoopSprite, edge, Alpha(light, warning ? .55f : .7f), 27); lit.pulse = .03f;
                layers.Add(lit);
                if (g == 3 && !plague && !warning)
                {
                    float seal = compact ? 2.4f : Mathf.Clamp(R * 2 / .955f, 1.6f, 13);
                    var runeGlow = Attach(plane, "Grade zone seal glow", glow, seal * 1.05f, Alpha(GradeGold, .3f), 25); runeGlow.pulse = .05f;
                    var rune = Attach(plane, "Grade zone seal", runeRing, seal, Alpha(GradeGold, .7f), 27); rune.spin = 22;
                    layers.Add(runeGlow); layers.Add(rune);
                    float spread = compact ? .8f : Mathf.Min(R * .85f, 3.5f), end = life;
                    Emitter(rune, Mathf.Clamp(R * .8f, 1.2f, 3.5f), 0, (owner, p) =>
                    {
                        if (owner.age > end - .5f) return;
                        Vector2 at = (Vector2)root.position + new Vector2(Range(-spread, spread), -.55f);
                        bool star = Chance(.3f);
                        var mote = Spawn(star ? "Grade zone star" : "Grade zone fleck", star ? GradeStarSprite : fleck, at, star ? Range(.45f, .65f) : Range(.24f, .36f), Alpha(Chance(.4f) ? GradeHot : GradeGold, 1), Range(.6f, .9f), true, compact ? 27 : 131);
                        mote.velocity = new Vector2(Range(-.2f, .2f), Range(1, 1.9f)); mote.drag = .4f; mote.flicker = .3f; mote.angle = Range(-15, 15);
                        mote.startScale = .4f; mote.endScale = .5f; mote.ease = 6; mote.hold = .35f; mote.fadePower = 1.3f;
                    });
                }
            }
            float spreadIn = Mathf.Clamp(.3f / life, .02f, .5f);
            foreach (var clip in layers)
            {
                clip.life = life; clip.loop = false; clip.fadeIn = .15f; clip.startScale = .3f; clip.endScale = 1;
                clip.ease = Mathf.Log(.05f) / Mathf.Log(1 - spreadIn); clip.hold = Mathf.Clamp01(1 - .6f / life); clip.fadePower = 1.4f;
            }
            Clip primary;
            if (attached.TryGetValue(root, out primary) && primary.layers != null) primary.layers.AddRange(layers);
            else Bundle(root, layers[0], layers.GetRange(1, layers.Count - 1).ToArray());
        }

        // ---------------------------------------------------------------- awakenings

        /// <summary>The flourish of a level-three skill's awakened behaviour, shown where it happens.</summary>
        public void Awaken(SkillEffect effect, Vector2 pos, float radius, Color tint, int facing)
        {
            EnsureTextures(); GradeEnsure();
            // Several shots of one cast awakening on the same spot (two level-three fireballs on one foe) flourish once.
            Vector3 last;
            if (GradeAwakenMark.TryGetValue(effect, out last) && Time.time - last.z < .3f && ((Vector2)last - pos).sqrMagnitude < 2.25f) return;
            GradeAwakenMark[effect] = new Vector3(pos.x, pos.y, Time.time);
            Color light = GradeTone(effect, tint);
            float R = Mathf.Clamp(radius, .5f, 6);
            int side = facing < 0 ? -1 : 1;
            float drop;
            switch (effect)
            {
                case SkillEffect.IceNova: drop = .5f; break;
                case SkillEffect.HealPulse: drop = .4f; break;
                case SkillEffect.Mine: drop = .3f; break;
                case SkillEffect.Shockwave: drop = .95f; break;
                case SkillEffect.Fireball: drop = .7f; break;
                case SkillEffect.Boomerang: drop = -1; break;
                default: drop = .8f; break;
            }
            bool grounded = drop >= 0;
            Vector2 ground = pos + Vector2.down * Mathf.Max(0, drop);
            bool small = R < 1;

            // An awakening on the very burst whose level accent was just drawn (same frame, skill and place) keeps only its
            // signature: that accent already brought the crown, the gold seal, the edge ring and the rising glints.
            bool onImpact = GradeImpactFrame == Time.frameCount && GradeImpactEffect == effect && (GradeImpactAt - pos).sqrMagnitude < 2.25f;
            if (!onImpact)
            {
                // The common chord of every awakening: a gold-white spark flaring and scattering over a gold bloom, the painted
                // crown opening on its own bloom and a white star; on the floor a gold shock ring runs out to the true radius.
                // Round the caster's own body the light stands behind the actors.
                bool self = GradeSelfCentred(effect), own = GradeOwnAwakening(effect);
                float flashWidth = Mathf.Min(own ? 1.6f : 4, Mathf.Clamp(1.2f + R * .5f, 1.4f, 4) * (self ? .7f : 1));
                var flashBloom = Spawn("Awaken bloom", glow, pos, flashWidth * 1.4f, Alpha(GradeGold, .5f), .3f, true, self ? 26 : 130);
                flashBloom.startScale = .6f; flashBloom.endScale = 1.3f; flashBloom.ease = 3; flashBloom.fadePower = 1.5f;
                SparkFlip(pos, flashWidth * 1.1f, Alpha(GradeHot, 1), .18f, self ? 28 : 132);
                float crownWidth = Mathf.Clamp(1.6f + R * .5f, 1.4f, 4.2f);
                GradeCrownBloom(pos, crownWidth, .5f, self ? 27 : 127, .45f);
                var crown = Spawn("Awaken crown", GradeHaloSprite, pos, crownWidth, Alpha(GradeGold, 1), .5f, true, self ? 28 : 133);
                crown.startScale = .45f; crown.endScale = 1.15f; crown.ease = 6; crown.spin = 120; crown.hold = .45f; crown.fadePower = .6f;
                GradeStar(pos, Mathf.Min(own ? 2 : 4, Mathf.Clamp(1.8f + R * .45f, 1.6f, 4) * (self ? .6f : 1)), Color.white, .26f, 0, self ? 28 : 133);
                if (grounded && !small && !own)
                {
                    GradeEdge(ground, R, GradeGold, .55f, .15f);
                    var wave = GroundWave(ground + Vector2.up * .05f, R * 2 / .74f, Alpha(GradeHot, .85f), .4f); wave.age = -.02f;
                }
                GradeRise(grounded ? ground : pos, Mathf.Min(R * .6f, 2), small ? 2 : 4, GradeHot, .25f);
            }

            switch (effect)
            {
                case SkillEffect.IceNova: GradeAwakenFreeze(pos, ground, R); break;
                case SkillEffect.ChainLightning: GradeAwakenThunder(pos, ground, R); break;
                // Shockwave, DashStrike, HealPulse, ShieldPulse and Quake draw their own awakening in their families.
                case SkillEffect.Shockwave: case SkillEffect.DashStrike: case SkillEffect.HealPulse: case SkillEffect.ShieldPulse: case SkillEffect.Quake: break;
                case SkillEffect.Fireball: GradeAwakenIgnite(pos, ground, R); break;
                case SkillEffect.Boomerang: GradeAwakenTurn(pos); break;
                case SkillEffect.Hook: GradeAwakenSnap(pos, ground, R); break;
                case SkillEffect.Mine: GradeAwakenStill(pos, ground, R, light); break;
                case SkillEffect.Vortex: GradeAwakenSpiral(pos, R, light); break;
                default: if (grounded && !onImpact) GradeColumn(ground, Mathf.Clamp(.6f + R * .2f, .8f, 1.6f), 4, .6f, 0); break;
            }
        }

        // Ice nova: time locks in frost. The nova's own frost seal flashes gold for a moment, a ring snaps shut on the frozen
        // from the true edge, the air over them chills deep blue and snow stars burst over them (never over the caster).
        private void GradeAwakenFreeze(Vector2 pos, Vector2 ground, float R)
        {
            ScreenFlash(pos, new Color(.6f, .84f, 1, .09f), .2f);
            // The nova already lays its frost seal on this radius: the awakening only gilds it briefly.
            var gold = GradeFlat(Spawn("Awaken frost seal gold", FrostSpriteSigil, ground + Vector2.up * .03f, R * 2.05f, Alpha(GradeGold, .55f), .45f, true, 27), .24f);
            gold.fadeIn = .04f; gold.hold = .2f; gold.fadePower = 2;
            // The lock: a ring at the edge closes inward on the frozen, a deep ice-blue stroke riding just inside the pale one.
            for (int i = 0; i < 2; i++)
            {
                bool deepRing = i == 0;
                var shut = GradeFlat(Spawn(deepRing ? "Awaken frost lock depth" : "Awaken frost lock", ring, ground + Vector2.up * .06f, R * 2 / .74f * (deepRing ? .97f : 1),
                    deepRing ? new Color(.2f, .5f, 1, .8f) : new Color(.85f, .97f, 1, .95f), .42f, true, deepRing ? 27 : 28), .3f);
                shut.startScale = 1; shut.endScale = .35f; shut.ease = 2.5f; shut.fadePower = 1.2f; shut.age = -.08f;
            }
            // A deep blue chill (alpha) over the frozen on either side, drawn over the nova's light as its flash dies, so the
            // frozen area reads on a bright sky while the caster in the middle stays clear.
            for (int side = -1; side <= 1; side += 2)
            {
                var chill = Spawn("Awaken frost chill", glow, ground + new Vector2(side * R * .58f, 1), R * 1.35f, new Color(.06f, .26f, .68f, .36f), .85f, false, 120);
                chill.scale.y *= .75f; chill.root.transform.localScale = chill.scale; chill.fadeIn = .08f; chill.hold = .35f; chill.fadePower = 1.6f; chill.age = -.05f;
            }
            int stars = 6;
            for (int i = 0; i < stars; i++)
            {
                float t = (i < stars / 2 ? -1 : 1) * Mathf.Lerp(.3f, 1, (i % (stars / 2)) / (stars / 2 - 1f));
                Vector2 at = ground + new Vector2(t * R * .85f, Range(.6f, 1.6f));
                var flakeStar = Spawn("Awaken frost star", flake, at, Range(.55f, .85f), i % 2 == 0 ? Color.white : new Color(.2f, .55f, .95f, 1), .7f, i % 2 == 0, 130);
                flakeStar.startScale = .2f; flakeStar.endScale = 1; flakeStar.ease = 4; flakeStar.spin = Range(-90, 90); flakeStar.hold = .45f; flakeStar.fadePower = 1.5f;
                GradeDelay(flakeStar, .06f + Mathf.Abs(t) * .12f);
                var glintStar = GradeStar(at, Range(.8f, 1.15f), Color.white, .3f, Range(0, 45)); GradeDelay(glintStar, .08f + Mathf.Abs(t) * .12f);
            }
        }

        // Chain lightning: the sky answers the last link. A crackling gold crown of arcs and radial bolts bursts around it.
        private void GradeAwakenThunder(Vector2 pos, Vector2 ground, float R)
        {
            // The gold crackle is the storm family's painted arcs as light, each over its own soft glow (no dark copy under it).
            int arcs = random.Next(StormArcLights.Length);
            for (int layer = 0; layer < 2; layer++)
            {
                bool lit = layer == 1;
                var shell = Spawn(lit ? "Awaken thunder shell" : "Awaken thunder glow", (lit ? StormArcLights : StormArcGlows)[arcs], pos, R * 2.8f, lit ? new Color(1, .9f, .55f, 1) : Alpha(GradeGold, .6f), .4f, true, lit ? 128 : 126);
                shell.frames = lit ? StormArcLights : StormArcGlows; shell.fps = 24; shell.loop = true; shell.spin = 160; shell.startScale = .3f; shell.endScale = 1.05f; shell.ease = 3; shell.fadePower = 1.4f;
                shell.flicker = .2f;
            }
            var shellLight = Spawn("Awaken thunder light", glow, pos, R * 2.6f, Alpha(GradeGold, .4f), .38f, true, 125); shellLight.flicker = .4f; shellLight.fadePower = 1.5f;
            for (int i = 0; i < 5; i++)
            {
                Vector2 dir = Direction(i * 72 + 18 + Range(-15, 15)); if (dir.y < -.3f) dir.y = -.3f;
                var bolt = StormPainted(pos, pos + dir.normalized * R * Range(1, 1.35f), .7f, new Color(1, .9f, .55f, 1), Alpha(GradeGold, .75f), .2f, Range(0, .08f), 130, random.Next(StormBoltVariants), Chance(.5f));
                bolt.flicker = .35f;
            }
            var sky = Spawn("Awaken thunder sky", glow, ground + Vector2.up * 6.5f, 4.5f, Alpha(GradeHot, .7f), .3f, true, 113); sky.flicker = .5f; sky.fadePower = 1.5f;
            // One wash of the view per frame, shared with the strike itself.
            if (Time.frameCount != StormFlashFrame) { StormFlashFrame = Time.frameCount; ScreenFlash(pos, new Color(1, .93f, .75f, .12f), .12f); }
        }

        // Fireball: the ground catches. A row of flame tongues licks up across the burning area (whose own seal the burning
        // ground lays), one column of fire rises out of it and embers fly.
        private void GradeAwakenIgnite(Vector2 pos, Vector2 ground, float R)
        {
            int tongues = 8;
            for (int i = 0; i < tongues; i++)
            {
                float t = i / (float)(tongues - 1) * 2 - 1; bool lit = i % 2 == 0;
                var tongue = FireTongue(ground + new Vector2(t * R * .9f, .1f), Range(.7f, 1) * (1.1f - .3f * Mathf.Abs(t)), new Vector2(t * .4f, Range(2.2f, 3.4f)), Range(.4f, .55f), lit ? 127 : 121, lit);
                tongue.drag = 2.4f; tongue.startScale = .4f; tongue.endScale = 1.4f; tongue.age = -Mathf.Abs(t) * .1f;
            }
            var pillarLight = Pillar(ground, 1.2f, 3.6f, Alpha(FireLight, .8f), .45f, 126); pillarLight.hold = .2f;
            EmberBurst(pos, 10, 5, FireEmberTone, .7f, 6);
        }

        // Boomerang: the disc turns home, a gold whirl flaring around it over a bloom, a deeper-hued crescent turning inside.
        private void GradeAwakenTurn(Vector2 pos)
        {
            var bloom = Spawn("Awaken turn bloom", glow, pos, 2.6f, Alpha(GradeGold, .45f), .38f, true, 127); bloom.startScale = .5f; bloom.endScale = 1.2f; bloom.ease = 2; bloom.fadePower = 1.4f;
            for (int i = 0; i < 3; i++)
            {
                bool inner = i == 0;
                var whirl = Spawn(inner ? "Awaken turn depth" : "Awaken turn", crescent, pos, inner ? 1.5f : 1.8f - (i - 1) * .4f, inner ? Alpha(GradeDeepGold, .8f) : Alpha(i == 1 ? GradeGold : GradeHot, .95f), .35f, true, 129 + i);
                whirl.angle = i * 120; whirl.spin = 900; whirl.startScale = .5f; whirl.endScale = 1.2f; whirl.ease = 2; whirl.fadePower = 1.4f;
            }
            Wave(pos, 2.2f, Alpha(GradeGold, .9f), .26f, 3);
            for (int i = 0; i < 8; i++) Spark(pos, Direction(i * 45 + Range(-10, 10)) * Range(3, 5), i % 2 == 0 ? GradeHot : GradeGold, Range(.18f, .28f), Range(.25f, .4f), 4, 2.5f);
        }

        // Hook: the reeled-in foes are slammed down; a steel-gold shock with cutting arcs on both sides over gold blooms.
        private void GradeAwakenSnap(Vector2 pos, Vector2 ground, float R)
        {
            ImpactLines(pos, R * 2.8f, Alpha(GradeHot, .9f), .24f);
            for (int s = -1; s <= 1; s += 2)
            {
                var bloom = Spawn("Awaken snap bloom", glow, pos + new Vector2(s * R * .45f, 0), R * 1.3f, Alpha(GradeGold, .42f), .34f, true, 127);
                bloom.scale.x *= .6f; bloom.root.transform.localScale = bloom.scale; bloom.fadePower = 1.4f;
                var arc = Crescent(pos + new Vector2(s * .3f, 0), R * 1.4f, Alpha(GradeGold, .95f), .3f, s > 0 ? 0 : 180, 130);
                bloom.velocity = arc.velocity = new Vector2(s * 4, 0); bloom.drag = arc.drag = 4;
            }
            SparkFlip(pos, R * 1.2f, Alpha(GradeHot, 1), .16f, 132);
            Debris(ground + Vector2.up * .2f, 6, 5, .2f, .8f, Color.white);
            BladeCrack(ground, R * 1.6f, Alpha(GradeGold, .85f), 1.1f);
        }

        // Mine: the blast's slow. A violet time seal closes over the area on a soft glow and motes sink like sand in a glass.
        private void GradeAwakenStill(Vector2 pos, Vector2 ground, float R, Color light)
        {
            var halo = Spawn("Awaken still glow", glow, ground + Vector2.up * .04f, R * 2.3f, Alpha(light, .35f), 1, true, 25); LayFlat(halo, .32f);
            halo.startScale = 1.25f; halo.endScale = .85f; halo.ease = 2; halo.hold = .45f; halo.fadePower = 1.4f; halo.fadeIn = .04f;
            var seal = Spawn("Awaken still", runeRing, ground + Vector2.up * .04f, R * 2 / .955f, Alpha(Color.Lerp(light, GradeHot, .3f), .95f), 1, true, 27);
            LayFlat(seal, .32f); seal.spin = -40; seal.startScale = 1.25f; seal.endScale = .85f; seal.ease = 2; seal.hold = .45f; seal.fadePower = 1.4f; seal.fadeIn = .04f;
            var dome = Spawn("Awaken still dome", glow, ground + Vector2.up * .6f, R * 2.4f, Alpha(light, .35f), .9f, true, 122); dome.scale.y *= .6f; dome.root.transform.localScale = dome.scale; dome.hold = .4f; dome.fadePower = 1.5f;
            for (int i = 0; i < 10; i++)
            {
                var mote = Spawn("Awaken still fleck", fleck, ground + new Vector2(Range(-R, R) * .85f, Range(1.2f, 2.2f)), Range(.24f, .36f), Alpha(i % 2 == 0 ? GradeHot : light, 1), Range(.8f, 1.1f), true, 131);
                mote.velocity = Vector2.down * Range(.7f, 1.3f); mote.fadeIn = .1f; mote.flicker = .25f; mote.angle = Range(-15, 15); mote.endScale = .6f; mote.ease = 1; mote.hold = .4f;
            }
        }

        // Vortex: the gathered storm bursts outward, three painted crescents flung wide as they spin over a glow, with a
        // deeper-hued one turning just inside them.
        private void GradeAwakenSpiral(Vector2 pos, float R, Color light)
        {
            Color gilded = Color.Lerp(light, GradeGold, .45f);
            var bloom = Spawn("Awaken spiral bloom", glow, pos, R * 2.2f, Alpha(gilded, .4f), .45f, true, 126); bloom.startScale = .3f; bloom.endScale = 1.25f; bloom.ease = 3; bloom.fadePower = 1.4f;
            float hh, ss, vv; Color.RGBToHSV(light, out hh, out ss, out vv);
            Color inner = Color.HSVToRGB(hh, Mathf.Clamp01(ss * 1.2f + .25f), 1);
            for (int i = 0; i < 4; i++)
            {
                bool depth = i == 0;
                var arc = Spawn(depth ? "Awaken spiral depth" : "Awaken spiral", crescent, pos, R * (depth ? 1.2f : 1.35f - (i - 1) * .18f), depth ? Alpha(inner, .8f) : Alpha(i == 2 ? GradeHot : gilded, 1), .45f, true, 128 + i);
                arc.angle = i * 120 + 30; arc.spin = -620; arc.startScale = .3f; arc.endScale = 1.3f; arc.ease = 3; arc.hold = .2f; arc.fadePower = 1.3f;
            }
            int motes = 12;
            for (int i = 0; i < motes; i++)
            {
                bool needle = i % 3 == 0;
                var mote = Spawn("Awaken spiral mote", needle ? spark : fleck, pos, needle ? .55f : Range(.26f, .38f), Alpha(i % 2 == 0 ? GradeHot : light, 1), Range(.45f, .6f), true, 131);
                mote.orbitCenter = pos; mote.orbitAngle = i * 30; mote.orbit = -480; mote.orbitRadius = .2f; mote.orbitRadiusEnd = R * Range(.9f, 1.15f); mote.orbitPower = .7f;
                mote.alignVelocity = needle; mote.endScale = .4f; mote.ease = 1; mote.fadePower = 1.3f;
            }
            Wave(pos, R * 2 / .74f, Alpha(light, .9f), .36f, 3);
        }
    }
}
