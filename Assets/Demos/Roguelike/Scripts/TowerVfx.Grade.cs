using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Skill levels: the extra light a level-two or level-three skill adds to any effect, and the awakening a level-three skill shows.
    // Level two adds one clear tier in the skill's own colour: a ring at the caster's feet with bands of light climbing the body,
    // a crisp star and a fine ring on the true edge of an area, a sparkling wake behind shots and a lit edge under lasting zones.
    // Level three adds the awakened crown in gold and white: a turning rune seal on the floor, a saint's halo, a short column of
    // light and a few rising glints. Every shape is an ink (alpha) layer under a light (additive) one, so it holds on pale skies.
    // Awaken draws the signature burst of each skill's awakened behaviour on top of the skill's own impact.
    public sealed partial class TowerVfx
    {
        private static Sprite GradeHaloSprite;
        private static readonly Color GradeGold = new Color(1, .82f, .42f), GradeHot = new Color(1, .97f, .86f), GradeUmber = new Color(.3f, .16f, .02f, .62f);
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
            if (GradeHaloSprite != null) return;
            // A saint's crown: a fine double ring with twelve rays (long and short in turn) and a faint inner ring.
            GradeHaloSprite = Build("Grade halo", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float deg = Mathf.Repeat(Mathf.Atan2(v, u) * Mathf.Rad2Deg, 360);
                float a = Gauss(r - .58f, .016f) + Gauss(r - .5f, .009f) * .75f + Gauss(r - .58f, .07f) * .22f + Gauss(r - .36f, .008f) * .45f;
                float cell = Mathf.Repeat(deg + 15, 30) - 15;
                float reach = ((int)((deg + 15) / 30)) % 2 == 0 ? .84f : .73f;
                float t = Mathf.Clamp01((r - .6f) / (reach - .6f)), arc = Mathf.Abs(cell) * Mathf.Deg2Rad * r;
                a += Gauss(arc, .022f * (1 - t) + .004f) * Band(.6f, reach, r) * .95f;
                a += (1 - Smooth(.1f, .56f, r)) * .05f;
                return White(Mathf.Clamp01(a) * (1 - Smooth(.86f, .98f, r)));
            });
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

        /// <summary>A deep, saturated version of a colour for the ink layer under its light.</summary>
        private static Color GradeDeep(Color color, float alpha)
        {
            float h, s, v; Color.RGBToHSV(color, out h, out s, out v);
            Color deep = s < .1f ? new Color(.2f, .18f, .26f) : Color.HSVToRGB(h, Mathf.Clamp01(s * 1.15f + .3f), .4f);
            deep.a = alpha; return deep;
        }

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

        // A single foe's brand (curse, mark): its tier stays on the foe, without floor seals, edges or columns.
        private static bool GradeSingleTarget(SkillEffect effect) { return effect == SkillEffect.Curse || effect == SkillEffect.HuntMark; }

        // Families that already raise their own level-three column of light out of the burst: no second gold one on top.
        private static bool GradeOwnColumn(SkillEffect effect)
        {
            return effect == SkillEffect.Fireball || effect == SkillEffect.Meteor || effect == SkillEffect.IceNova;
        }

        /// <summary>Gold glints and motes rising off the floor around <paramref name="from"/>.</summary>
        private void GradeRise(Vector2 from, float spread, int count, Color color, float delayMax)
        {
            for (int i = 0; i < count; i++)
            {
                bool star = i % 3 == 0;
                var mote = Spawn(star ? "Grade rising glint" : "Grade rising mote", star ? glint : glow, from + new Vector2(Range(-spread, spread), Range(0, .35f)),
                    star ? Range(.4f, .6f) : Range(.12f, .22f), Alpha(color, .95f), Range(.6f, .95f), true, 131);
                mote.velocity = new Vector2(Range(-.25f, .25f), Range(1.4f, 2.6f)); mote.drag = .5f; mote.fadeIn = .08f; mote.endScale = .3f; mote.ease = 1;
                if (star) mote.spin = Range(-120, 120); else mote.flicker = .35f;
                GradeDelay(mote, Range(0, delayMax));
            }
        }

        /// <summary>A ring lying on the floor whose rim sits exactly on <paramref name="radius"/>: ink under light.</summary>
        private Clip GradeEdge(Vector2 ground, float radius, Color light, Color deep, float life, float hold)
        {
            float width = radius * 2 / .74f;
            var ink = GradeFlat(Spawn("Grade edge ink", ring, ground + Vector2.up * .04f, width * 1.01f, deep, life, false, 25), .3f);
            var lit = GradeFlat(Spawn("Grade edge", ring, ground + Vector2.up * .04f, width, Alpha(light, .9f), life * .9f, true, 27), .3f);
            foreach (var clip in new[] { ink, lit }) { clip.startScale = .25f; clip.endScale = 1; clip.ease = 3.5f; clip.hold = hold; clip.fadePower = 1.4f; }
            return ink;
        }

        /// <summary>The awakened rune seal turning on the floor, its outer ring on <paramref name="radius"/>.</summary>
        private Clip GradeSeal(Vector2 ground, float radius, float life, float spin)
        {
            float width = Mathf.Clamp(radius * 2 / .955f, 1.6f, 13);
            var ink = Spawn("Grade seal ink", runeRing, ground + Vector2.up * .03f, width * 1.01f, GradeUmber, life, false, 25); LayFlat(ink, .34f);
            ink.spin = spin; ink.fadeIn = .08f; ink.startScale = .6f; ink.endScale = 1; ink.ease = 4; ink.hold = .4f; ink.fadePower = 1.4f;
            return RuneCircle(ground + Vector2.up * .03f, width, Alpha(GradeGold, .92f), life, spin, true);
        }

        /// <summary>A short column of gold light on the floor, over a dark umber copy that keeps it on pale skies.</summary>
        private void GradeColumn(Vector2 ground, float width, float height, float life, float delay, bool behind = false)
        {
            var ink = Spawn("Grade column ink", pillar, ground, width * 1.5f, new Color(.32f, .18f, .03f, .34f), life * 1.1f, false, behind ? 25 : 117);
            ink.scale = new Vector3(width * 1.5f / pillar.bounds.size.x, height / pillar.bounds.size.y, 1); ink.root.transform.localScale = ink.scale;
            ink.axisStart = new Vector2(.35f, .12f); ink.axisEnd = Vector2.one; ink.ease = 4; ink.hold = .3f; ink.fadePower = 1.3f;
            var column = Pillar(ground, width, height, Alpha(GradeGold, .75f), life, behind ? 27 : 128); column.hold = .3f;
            var heart = Pillar(ground, width * .32f, height * 1.05f, Alpha(GradeHot, .9f), life * .75f, behind ? 28 : 129); heart.hold = .25f;
            GradeDelay(ink, delay); GradeDelay(column, delay); GradeDelay(heart, delay);
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
            Color light = GradeTone(effect, c.tint), deep = GradeDeep(light, .6f);
            float w = c.windup, release = Mathf.Max(0, w - .02f);
            Vector2 feet = c.pos + Vector2.down, head = c.pos + new Vector2(-c.facing * .06f, .8f);
            if (w < .2f)
            {
                // An instant echo: one bright tier star at the chest; the skill's own burst follows at once.
                var pip = Glint(c.pos + Vector2.up * .1f, g == 3 ? 1.6f : 1.1f, Alpha(g == 3 ? GradeHot : light, .9f), .2f, 0); pip.spin = 0;
                return;
            }
            float life = w + .2f, hold = w / life;

            // Level two: a ring of the skill's light at the feet and bands of it climbing the body toward the release.
            float ringWidth = g == 3 ? 3.1f : 2.6f;
            var floorInk = GradeFlat(Spawn("Grade cast ring ink", ring, feet + Vector2.up * .03f, ringWidth * 1.02f, deep, life, false, 25), .3f);
            var floorRing = GradeFlat(Spawn("Grade cast ring", ring, feet + Vector2.up * .03f, ringWidth, Alpha(light, .9f), life, true, 27), .3f);
            foreach (var clip in new[] { floorInk, floorRing }) { clip.fadeIn = .08f; clip.startScale = .5f; clip.endScale = 1; clip.ease = 3; clip.hold = hold; clip.fadePower = 1.6f; }
            int bands = g == 3 ? 3 : 2;
            for (int i = 0; i < bands; i++)
            {
                float delay = i * w / (bands + .5f), span = Mathf.Max(.15f, w - delay);
                bool gold = g == 3 && i == bands - 1;
                var band = GradeFlat(Spawn("Grade cast band", ring, feet + Vector2.up * .1f, 1.75f, Alpha(gold ? GradeHot : light, .85f), span, true, 131), .28f);
                band.velocity = Vector2.up * 1.9f / span; band.startScale = 1.15f; band.endScale = .7f; band.ease = 1; band.fadeIn = span * .2f; band.fadePower = 1.4f;
                GradeDelay(band, delay);
            }
            GradeRise(feet, .7f, Budget(3), light, w * .7f);
            if (g < 3) return;

            // Level three, the awakened crown: a gold seal turning under the feet, a halo behind the head (behind the actors),
            // a column of light behind the caster and gold glints rising past it, all peaking at the release.
            var sealInk = Spawn("Grade crown seal ink", runeRing, feet + Vector2.up * .02f, 3.5f, GradeUmber, life, false, 25); LayFlat(sealInk, .32f);
            var seal = RuneCircle(feet + Vector2.up * .02f, 3.4f, Alpha(GradeGold, .9f), life, 50, true);
            foreach (var clip in new[] { sealInk, seal }) { clip.spin = 50; clip.fadeIn = .08f; clip.startScale = .6f; clip.endScale = 1; clip.ease = 4; clip.hold = hold; clip.fadePower = 1.5f; }
            var haloInk = Anchored(c.anchor, head, "Grade crown halo ink", GradeHaloSprite, 1.42f, new Color(.32f, .18f, .03f, .72f), life, 27, false);
            var halo = Anchored(c.anchor, head, "Grade crown halo", GradeHaloSprite, 1.36f, Alpha(GradeHot, .95f), life, 28);
            foreach (var clip in new[] { haloInk, halo }) { clip.spin = 40 * c.facing; clip.fadeIn = w * .3f; clip.startScale = .4f; clip.endScale = 1; clip.ease = 3; clip.hold = hold; clip.fadePower = 1.6f; }
            var columnInk = Spawn("Grade crown column ink", pillar, feet, 1.6f, new Color(.32f, .18f, .03f, .3f), life, false, 26);
            columnInk.scale = new Vector3(1.6f / pillar.bounds.size.x, 4.2f / pillar.bounds.size.y, 1); columnInk.root.transform.localScale = columnInk.scale;
            var column = Pillar(feet, 1.15f, 4.2f, Alpha(GradeGold, .55f), life, 28);
            foreach (var clip in new[] { columnInk, column }) { clip.axisStart = new Vector2(.3f, .25f); clip.axisEnd = Vector2.one; clip.ease = 3; clip.fadeIn = w * .5f; clip.hold = hold; clip.fadePower = 1.8f; }
            for (int i = 0; i < 3; i++)
            {
                var spark = Glint(feet + new Vector2(Range(-.8f, .8f), Range(.3f, 1.7f)), Range(.45f, .7f), Alpha(GradeHot, .95f), .4f, Range(0, 45));
                spark.velocity = Vector2.up * Range(.8f, 1.4f); GradeDelay(spark, Range(.05f, Mathf.Max(.06f, w - .1f)));
            }
            // At the release a gold star flares at the casting hand.
            var flare = Glint(c.pos + new Vector2(c.facing * .7f, .15f), 2.4f, Alpha(GradeGold, .95f), .26f, 0); flare.spin = 0; GradeDelay(flare, release);
        }

        private void GradeImpact(SkillEffect effect, Vector2 pos, float radius, Color tint, int facing)
        {
            int g = Grade; if (g < 2) return;
            bool repeat = GradeRepeats(effect, radius);
            if (repeat && GradeThrottled(effect, .35f)) return;
            GradeEnsure();
            Color light = GradeTone(effect, tint), deep = GradeDeep(light, .7f);
            float R = Mathf.Clamp(radius, .3f, 7), drop = GradeDrop(effect, radius);
            bool grounded = drop >= 0;
            Vector2 ground = pos + Vector2.down * Mathf.Max(0, drop);
            float scale = repeat ? .65f : 1;
            // A burst round the caster's own body keeps its tier light behind the actors and its star smaller.
            bool self = GradeSelfCentred(effect);
            GradeImpactFrame = Time.frameCount; GradeImpactEffect = effect; GradeImpactAt = pos;

            // Level two: a crisp star of the skill's light and a fine ring on the area's true edge; repeating hits keep the star.
            var mark = Glint(pos, Mathf.Clamp(.7f + R * .35f, .9f, 2.6f) * scale * (self ? .7f : 1), Alpha(Color.Lerp(light, Color.white, .45f), .95f), .24f, 45); mark.spin = 0;
            if (self) mark.renderer.sortingOrder = 28;
            GradeAccentTime[effect] = Time.time;
            // Bursts landing together (a meteor's side stones, marks on several foes) share the first one's rings and seal:
            // the followers keep only their star and crown.
            float first;
            bool follow = !repeat && GradeSealTime.TryGetValue(effect, out first) && Time.time - first < .45f;
            if (follow)
            {
                if (g >= 3)
                {
                    var echo = Spawn("Grade crown", GradeHaloSprite, pos, Mathf.Clamp(1 + R * .45f, 1.3f, 3.2f) * .8f, Alpha(GradeHot, .9f), .36f, true, self ? 28 : 132);
                    echo.startScale = .35f; echo.endScale = 1.15f; echo.ease = 3; echo.spin = 90; echo.fadePower = 1.5f;
                }
                return;
            }
            bool single = GradeSingleTarget(effect);
            if (!repeat && !single)
            {
                if (grounded && R >= .9f) { GradeEdge(ground, R, light, deep, .5f, .2f); GradeSealTime[effect] = Time.time; }
                else
                {
                    var halo = Spawn("Grade halo ring", ring, pos, Mathf.Clamp(R * 2.2f, 1.2f, 4), Alpha(light, .85f), .3f, true, 131);
                    halo.startScale = .3f; halo.endScale = 1; halo.ease = 3; halo.fadePower = 1.4f;
                }
            }
            int motes = Budget(repeat ? 1 : 3);
            for (int i = 0; i < motes; i++)
            {
                var mote = Spawn("Grade mote", glow, pos + Inside(Mathf.Min(1.4f, R * .5f)), Range(.12f, .22f), Alpha(i % 2 == 0 ? light : Color.white, .95f), Range(.4f, .65f), true, 131);
                mote.velocity = new Vector2(Range(-.4f, .4f), Range(1.2f, 2.4f)); mote.drag = .8f; mote.flicker = .45f; mote.endScale = .25f; mote.ease = 1; mote.fadeIn = .04f;
            }
            if (g < 3) return;

            // Level three, the awakened crown: a gold halo snapping open over the burst, a turning rune seal on the floor at
            // the area's edge, a short column of light and gold glints rising.
            float crownWidth = Mathf.Clamp(1 + R * .45f, 1.3f, 3.2f) * scale;
            var crownInk = Spawn("Grade crown ink", GradeHaloSprite, pos, crownWidth * 1.04f, GradeUmber, .42f, false, self ? 27 : 126);
            var crown = Spawn("Grade crown", GradeHaloSprite, pos, crownWidth, Alpha(GradeHot, .95f), .4f, true, self ? 28 : 132);
            foreach (var clip in new[] { crownInk, crown }) { clip.startScale = .35f; clip.endScale = 1.15f; clip.ease = 3; clip.spin = 90; clip.hold = .15f; clip.fadePower = 1.5f; }
            if (repeat || single) { GradeRise(pos + Vector2.down * .3f, .3f, 1, GradeHot, .05f); return; }
            if (grounded)
            {
                GradeSeal(ground, R, .9f, 60); GradeSealTime[effect] = Time.time;
                if (!GradeOwnColumn(effect)) GradeColumn(ground, Mathf.Clamp(.5f + R * .18f, .7f, 1.4f), Mathf.Clamp(2.2f + R * .35f, 2.4f, 4.5f), .55f, .02f, self);
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
            // A fireball already burns white-hot at its heart: it takes no extra aura, and of the twin balls one cast throws
            // along one path only the first wears the crown (the second keeps a thinner wake).
            bool fire = effect == SkillEffect.Fireball;
            Vector2 at = root.position;
            bool twin = fire && GradeShotFrame == Time.frameCount && GradeShotEffect == effect && (GradeShotAt - at).sqrMagnitude < 1;
            if (!twin) { GradeShotFrame = Time.frameCount; GradeShotEffect = effect; GradeShotAt = at; }
            // Level two: a soft aura of the skill's light around the head and a wake of sparkles.
            bool slug = effect == SkillEffect.Ricochet;
            var aura = Attach(root, "Grade shot aura", glow, size * (fire ? 1 : slug ? .8f : 1.5f), Alpha(light, fire ? .12f : slug ? .18f : .5f), slug ? 116 : 121); aura.pulse = .12f;
            layers.Add(aura);
            Color wake = g == 3 ? GradeHot : Color.Lerp(light, Color.white, .35f);
            Emitter(aura, (g == 3 ? 16 : 10) * (twin ? .5f : 1), size * .2f, (owner, p) =>
            {
                Vector2 back = owner.external ? -(Vector2)owner.external.right : Vector2.left;
                var mote = Spawn("Grade shot sparkle", glow, p, Range(.1f, .2f) * Mathf.Max(.8f, size), Alpha(Chance(.5f) ? wake : light, .95f), Range(.25f, .4f), true, 131);
                mote.velocity = back * Range(.6f, 1.6f) + Inside(.6f) + Vector2.up * .3f; mote.drag = 2; mote.flicker = .5f; mote.endScale = .15f; mote.ease = 1;
            });
            if (g == 3 && !twin && !slug)
            {
                // Level three: a turning gold crown with a white star at its heart rides the head of the shot.
                float crownSize = size * (fire ? 1 : 1.5f), starSize = size * (fire ? .7f : 1.3f);
                var crownInk = Attach(root, "Grade shot crown ink", GradeHaloSprite, crownSize * 1.04f, GradeUmber, 120, false); crownInk.spin = -200;
                var crown = Attach(root, "Grade shot crown", GradeHaloSprite, crownSize, Alpha(GradeGold, .9f), 126); crown.spin = -200;
                var star = Attach(root, "Grade shot star", glint, starSize, Alpha(GradeHot, .9f), 127); star.spin = 280; star.pulse = .12f;
                layers.Add(crownInk); layers.Add(crown); layers.Add(star);
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
            Color light = Color.Lerp(EffectColor(effect), Color.white, .15f), deep = GradeDeep(light, .5f);
            float R = Mathf.Clamp(radius, .4f, 7);
            // A poison cloud turns its own plague seal at level three: the tier keeps only the lit edge, in its own colour.
            bool plague = effect == SkillEffect.PoisonCloud;
            var layers = new List<Clip>();
            if (effect == SkillEffect.Orbit)
            {
                // Orbs: the path they really travel (OrbitOffset), a fine tilted ring around the hub.
                float width = R * 2 / .74f;
                var pathInk = Attach(root, "Grade orbit path ink", ring, width * 1.01f, Alpha(deep, .35f), 112, false);
                var path = Attach(root, "Grade orbit path", ring, width, Alpha(light, .4f), 119);
                foreach (var clip in new[] { pathInk, path }) { clip.scale.y *= OrbitTilt; clip.root.transform.localScale = clip.scale; layers.Add(clip); }
                if (g == 3)
                {
                    var hub = Attach(root, "Grade orbit crown", GradeHaloSprite, 1.1f, Alpha(GradeGold, .8f), 126); hub.spin = 120;
                    layers.Add(hub);
                }
            }
            else
            {
                // A squashed child on the floor so seals spin in the ground plane; it dies with the zone root.
                var plane = new GameObject("Grade ground plane").transform; plane.SetParent(root, false);
                plane.localPosition = new Vector3(0, -.57f, 0); plane.localScale = new Vector3(1, .3f, 1);
                // A turret's radius is its firing range: its tier sits at its base instead.
                bool compact = effect == SkillEffect.Sentry || effect == SkillEffect.Mine;
                float edge = compact ? 1.8f : R * 2 / .74f;
                var ink = Attach(plane, "Grade zone edge ink", ring, edge * 1.01f, deep, 25, false);
                var lit = Attach(plane, "Grade zone edge", ring, edge, Alpha(light, .75f), 27); lit.pulse = .03f;
                layers.Add(ink); layers.Add(lit);
                if (g == 3 && !plague)
                {
                    float seal = compact ? 2.4f : Mathf.Clamp(R * 2 / .955f, 1.6f, 13);
                    var runeInk = Attach(plane, "Grade zone seal ink", runeRing, seal * 1.01f, GradeUmber, 25, false); runeInk.spin = 22;
                    var rune = Attach(plane, "Grade zone seal", runeRing, seal, Alpha(GradeGold, .8f), 27); rune.spin = 22;
                    layers.Add(runeInk); layers.Add(rune);
                    float spread = compact ? .8f : Mathf.Min(R * .85f, 3.5f), end = life;
                    Emitter(rune, Mathf.Clamp(R * .8f, 1.2f, 3.5f), 0, (owner, p) =>
                    {
                        if (owner.age > end - .5f) return;
                        Vector2 at = (Vector2)root.position + new Vector2(Range(-spread, spread), -.55f);
                        var mote = Spawn(Chance(.3f) ? "Grade zone glint" : "Grade zone mote", glow, at, Range(.12f, .22f), Alpha(Chance(.4f) ? GradeHot : GradeGold, .95f), Range(.7f, 1), true, 131);
                        mote.velocity = new Vector2(Range(-.2f, .2f), Range(1, 1.9f)); mote.drag = .4f; mote.fadeIn = .1f; mote.flicker = .35f; mote.endScale = .3f; mote.ease = 1;
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
            Color light = GradeTone(effect, tint), deep = GradeDeep(light, .75f);
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
                // The common chord of every awakening: a dark ground for the light, a gold-white flash, the crown halo snapping
                // open and a white star; on the floor a gold shock ring runs out to the true radius. Round the caster's own
                // body the light stands behind the actors.
                bool self = GradeSelfCentred(effect), own = GradeOwnAwakening(effect);
                Underlay(pos, Mathf.Clamp(R * 1.6f, 1.6f, 6), .3f, .45f);
                var flash = Spawn("Awaken flash", glow, pos, Mathf.Min(own ? 1.6f : 5, Mathf.Clamp(1.4f + R * .6f, 1.6f, 5) * (self ? .7f : 1)), Alpha(GradeHot, .9f), .18f, true, self ? 28 : 132);
                flash.ramp = Ramp(Color.white, GradeGold); flash.startScale = .5f; flash.endScale = 1.3f; flash.ease = 3; flash.fadePower = 1.6f;
                float crownWidth = Mathf.Clamp(1.6f + R * .5f, 1.4f, 4.2f);
                var crownInk = Spawn("Awaken crown ink", GradeHaloSprite, pos, crownWidth * 1.04f, GradeUmber, .52f, false, self ? 27 : 127);
                var crown = Spawn("Awaken crown", GradeHaloSprite, pos, crownWidth, Alpha(GradeHot, .95f), .5f, true, self ? 28 : 133);
                foreach (var clip in new[] { crownInk, crown }) { clip.startScale = .3f; clip.endScale = 1.2f; clip.ease = 4; clip.spin = 120; clip.hold = .2f; clip.fadePower = 1.6f; }
                var star = Glint(pos, Mathf.Min(own ? 2 : 4.5f, Mathf.Clamp(2 + R * .5f, 1.8f, 4.5f) * (self ? .6f : 1)), Color.white, .28f, 0); star.spin = 0;
                if (self) star.renderer.sortingOrder = 28;
                if (grounded && !small && !own)
                {
                    GradeEdge(ground, R, GradeGold, GradeUmber, .55f, .15f);
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
                case SkillEffect.Boomerang: GradeAwakenTurn(pos, light, deep); break;
                case SkillEffect.Hook: GradeAwakenSnap(pos, ground, R, light, deep); break;
                case SkillEffect.Mine: GradeAwakenStill(pos, ground, R, light, deep); break;
                case SkillEffect.Vortex: GradeAwakenSpiral(pos, ground, R, light, deep); break;
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
            // The lock: a ring at the edge closes inward on the frozen.
            for (int i = 0; i < 2; i++)
            {
                bool ink = i == 0;
                var shut = GradeFlat(Spawn(ink ? "Awaken frost lock ink" : "Awaken frost lock", ring, ground + Vector2.up * .06f, R * 2 / .74f * (ink ? 1.02f : 1),
                    ink ? new Color(.04f, .24f, .62f, .85f) : new Color(.85f, .97f, 1, .95f), .42f, !ink, ink ? 26 : 28), .3f);
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
                var glintStar = Glint(at, Range(.8f, 1.2f), Color.white, .3f, Range(0, 45)); GradeDelay(glintStar, .08f + Mathf.Abs(t) * .12f);
            }
        }

        // Chain lightning: the sky answers the last link. A crackling gold crown of arcs and radial bolts bursts around it.
        private void GradeAwakenThunder(Vector2 pos, Vector2 ground, float R)
        {
            var shell = Spawn("Awaken thunder shell", StormArcRings[random.Next(StormArcRings.Length)], pos, R * 2.8f, new Color(1, .93f, .7f, 1), .4f, false, 128);
            shell.frames = StormArcRings; shell.fps = 24; shell.loop = true; shell.spin = 160; shell.startScale = .3f; shell.endScale = 1.05f; shell.ease = 3; shell.fadePower = 1.4f; shell.flicker = .2f;
            // A gold-umber ink under the shell so the gold crackle keeps its shape inside the white strike on a pale sky.
            var shellInk = Spawn("Awaken thunder ink", ring, pos, R * 2.5f, new Color(.34f, .2f, .04f, .55f), .36f, false, 127);
            shellInk.startScale = .3f; shellInk.endScale = 1.05f; shellInk.ease = 3; shellInk.fadePower = 1.4f;
            var shellLight = Spawn("Awaken thunder light", glow, pos, R * 2.4f, Alpha(GradeGold, .38f), .35f, true, 125); shellLight.flicker = .4f; shellLight.fadePower = 1.5f;
            for (int i = 0; i < 5; i++)
            {
                Vector2 dir = Direction(i * 72 + 18 + Range(-15, 15)); if (dir.y < -.3f) dir.y = -.3f;
                var bolt = StormStroke(pos, pos + dir.normalized * R * Range(1, 1.35f), .7f, new Color(1, .95f, .78f, 1), .2f, false, 129, random.Next(StormBoltVariants), Chance(.5f));
                bolt.flicker = .35f; bolt.fadePower = 1.4f; bolt.age = -Range(0, .08f);
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

        // Boomerang: the disc turns home, a gold whirl flaring around it.
        private void GradeAwakenTurn(Vector2 pos, Color light, Color deep)
        {
            for (int i = 0; i < 3; i++)
            {
                bool ink = i == 0;
                var whirl = Spawn(ink ? "Awaken turn ink" : "Awaken turn", crescent, pos, ink ? 2 : 1.8f - (i - 1) * .4f, ink ? Alpha(deep, .7f) : Alpha(i == 1 ? GradeGold : GradeHot, .95f), .35f, !ink, ink ? 126 : 130 + i);
                whirl.angle = i * 120; whirl.spin = 900; whirl.startScale = .5f; whirl.endScale = 1.2f; whirl.ease = 2; whirl.fadePower = 1.4f;
            }
            Wave(pos, 2.2f, Alpha(GradeGold, .9f), .26f, 3);
            for (int i = 0; i < 8; i++) Spark(pos, Direction(i * 45 + Range(-10, 10)) * Range(3, 5), i % 2 == 0 ? GradeHot : GradeGold, Range(.18f, .28f), Range(.25f, .4f), 4, 2.5f);
        }

        // Hook: the reeled-in foes are slammed down; a steel-gold shock with cutting arcs on both sides.
        private void GradeAwakenSnap(Vector2 pos, Vector2 ground, float R, Color light, Color deep)
        {
            ImpactLines(pos, R * 2.8f, Alpha(GradeHot, .9f), .24f);
            for (int s = -1; s <= 1; s += 2)
            {
                var arcInk = Crescent(pos + new Vector2(s * .3f, 0), R * 1.5f, Alpha(deep, .7f), .32f, s > 0 ? 0 : 180, 126); arcInk.renderer.sharedMaterial = AlphaBlend;
                var arc = Crescent(pos + new Vector2(s * .3f, 0), R * 1.4f, Alpha(GradeGold, .95f), .3f, s > 0 ? 0 : 180, 130);
                arcInk.velocity = arc.velocity = new Vector2(s * 4, 0); arcInk.drag = arc.drag = 4;
            }
            Debris(ground + Vector2.up * .2f, 6, 5, .2f, .8f, Color.white);
            Crack(ground, R * 1.6f, Alpha(GradeGold, .85f), 1.1f);
        }

        // Mine: the blast's slow. A violet time seal closes over the area and motes sink like sand in a glass.
        private void GradeAwakenStill(Vector2 pos, Vector2 ground, float R, Color light, Color deep)
        {
            for (int i = 0; i < 2; i++)
            {
                bool ink = i == 0;
                var seal = Spawn(ink ? "Awaken still ink" : "Awaken still", runeRing, ground + Vector2.up * .04f, R * 2 / .955f * (ink ? 1.02f : 1), ink ? Alpha(deep, .7f) : Alpha(Color.Lerp(light, GradeHot, .3f), .95f), 1, !ink, ink ? 25 : 27);
                LayFlat(seal, .32f); seal.spin = -40; seal.startScale = 1.25f; seal.endScale = .85f; seal.ease = 2; seal.hold = .45f; seal.fadePower = 1.4f; seal.fadeIn = .04f;
            }
            var dome = Spawn("Awaken still dome", glow, ground + Vector2.up * .6f, R * 2.4f, Alpha(light, .35f), .9f, true, 122); dome.scale.y *= .6f; dome.root.transform.localScale = dome.scale; dome.hold = .4f; dome.fadePower = 1.5f;
            for (int i = 0; i < 10; i++)
            {
                var mote = Spawn("Awaken still mote", glow, ground + new Vector2(Range(-R, R) * .85f, Range(1.2f, 2.2f)), Range(.12f, .22f), Alpha(i % 2 == 0 ? GradeHot : light, .95f), Range(.8f, 1.1f), true, 131);
                mote.velocity = Vector2.down * Range(.7f, 1.3f); mote.fadeIn = .15f; mote.flicker = .3f; mote.endScale = .4f; mote.ease = 1;
            }
        }

        // Vortex: the gathered storm bursts outward in a widening gold spiral.
        private void GradeAwakenSpiral(Vector2 pos, Vector2 ground, float R, Color light, Color deep)
        {
            var swirlInk = Swirl(pos, R * 1.4f, Alpha(deep, .7f), .5f, -520, 126); swirlInk.renderer.sharedMaterial = AlphaBlend;
            var swirlLight = Swirl(pos, R * 1.3f, Alpha(Color.Lerp(light, GradeGold, .45f), .9f), .45f, -520, 130);
            foreach (var clip in new[] { swirlInk, swirlLight }) { clip.startScale = .3f; clip.endScale = 1.35f; clip.ease = 3; clip.fadePower = 1.4f; }
            int motes = 12;
            for (int i = 0; i < motes; i++)
            {
                var mote = Spawn("Awaken spiral mote", i % 3 == 0 ? spark : glow, pos, i % 3 == 0 ? .5f : Range(.16f, .26f), Alpha(i % 2 == 0 ? GradeHot : light, .95f), Range(.45f, .6f), true, 131);
                mote.orbitCenter = pos; mote.orbitAngle = i * 30; mote.orbit = -480; mote.orbitRadius = .2f; mote.orbitRadiusEnd = R * Range(.9f, 1.15f); mote.orbitPower = .7f;
                mote.alignVelocity = i % 3 == 0; mote.endScale = .3f; mote.ease = 1; mote.fadePower = 1.3f;
            }
            Wave(pos, R * 2 / .74f, Alpha(light, .85f), .36f, 3);
        }
    }
}
