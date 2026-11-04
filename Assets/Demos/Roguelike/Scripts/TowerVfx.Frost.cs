using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Frost and venom family, painted.
    // IceNova: a frost seal and spiralling snowflakes charge a luminous ice gem round a plasma heart in the hand; the release
    // bursts behind the caster as an ice-blue blast under a drawn spark, a shock front races out, the ground freezes across
    // the whole radius and a double row of painted ice crystals (smooth light from the upper left across soft facets,
    // refraction streaks, cold light glowing up their spines and frosty rims, drawn again as light over the paint) erupts
    // outward and shatters, in pale billowing frost mist.
    // PoisonCloud: a toxic orb of painted smoke round an acid plasma heart gathers in the hand and bursts like a vial; the
    // cloud lands as an acid blast with a violet lobe and a splash crown, and churns as billowing toxic smoke (violet shadow,
    // green body, acid highlights, lit from within) over a glossy acid pool, with glowing bubbles, toxic sparks, green arcs and
    // painted skulls with burning eyes. Nothing is outlined: contrast comes from deep saturated hues and soft dark shades
    // behind the actors.
    public sealed partial class TowerVfx
    {
        // Painted ice (a crystal cluster, a fan of great crystals, flying fragments and chips) with light copies of their inner
        // glow and rims, a luminous gem, snowflakes, a frost seal and frozen ground; painted venom (a drop, a splash crown, an
        // acid pool, a skull and its burning eyes) and a glossy light bubble.
        private static Sprite FrostSpriteCrystal, FrostSpriteCrystalGlow, FrostSpriteFan, FrostSpriteFanGlow, FrostSpriteShatter, FrostSpriteShard;
        private static Sprite FrostSpriteGem, FrostSpriteFlake, FrostSpriteSigil, FrostSpriteIce;
        private static Sprite FrostSpriteDrop, FrostSpriteBubble, FrostSpriteCrown, FrostSpritePool, FrostSpriteSkull, FrostSpriteSkullGlow;
        // Still toxic cloud masses for the lasting cloud, and billow flipbooks (toxic smoke and pale frost mist) that swell,
        // open holes and tear apart as they drift.
        private static Sprite[] FrostSpriteMiasma;
        private static Sprite[][] FrostSpriteFumes, FrostSpriteMists;
        // Crystal fan blades, back to front: angle from vertical (degrees), length and base half-width (texture units, height 2).
        private static readonly float[] FrostFanBlades = { -66, 1.1f, .18f, 64, 1.15f, .18f, -47, 1.45f, .23f, 45, 1.5f, .23f, -27, 1.72f, .26f, 28, 1.75f, .26f, -11, 1.88f, .28f, 13, 1.84f, .27f, 2, 1.96f, .32f };

        private static readonly Color FrostPale = new Color(.84f, .97f, 1), FrostBlue = new Color(.55f, .86f, 1), FrostDeep = new Color(.1f, .3f, .78f),
            FrostAcid = new Color(.6f, 1, .28f), FrostAcidHot = new Color(.9f, 1, .66f), FrostViolet = new Color(.72f, .38f, 1);

        // ---------------------------------------------------------------- ice nova

        private void CastIceNova(CastInfo c)
        {
            float w = c.windup, R = FrostAreaRadius(c.radius, 2.2f, 4.5f);
            Vector2 pos = c.pos, ground = new Vector2(pos.x, pos.y - 1), hand = pos + new Vector2(c.facing * .62f, .22f);
            Color light = Color.Lerp(IceLight, c.tint, .2f);
            if (w < .2f) { FrostQuickCast(c, hand, light, FrostPale, true); return; }
            float life = w + .35f, hold = w / life;
            // Higher levels swell the charge and the circle and grow a fuller crown of crystals; level three turns a second,
            // white-gold circle the other way behind the first and raises a column of cold light.
            int g = Grade; float gs = 1 + (g - 1) * .12f;
            if (g >= 3)
            {
                var crownGlow = Spawn("Frost crown glow", glow, pos + Vector2.up * .1f, 4.6f, Alpha(new Color(1, .95f, .8f), .3f), w + .2f, true, 26);
                var crownCircle = Spawn("Frost crown circle", FrostSpriteSigil, pos + Vector2.up * .1f, 4.5f, new Color(1, .95f, .8f, .7f), w + .2f, true, 27);
                crownCircle.spin = -45 * c.facing;
                foreach (var s in new[] { crownGlow, crownCircle }) { s.fadeIn = .08f; s.startScale = .3f; s.endScale = 1.1f; s.ease = 4; s.hold = w / (w + .2f); s.fadePower = 1.4f; }
                var column = Pillar(ground, 1.8f, 6.5f, Alpha(light, .7f), w + .25f, 27); column.fadeIn = w * .5f; column.hold = .75f; column.fadePower = 1.8f;
                var columnBody = FrostColumnBody(ground, 1.55f, 6.5f, .4f, w + .25f);
                columnBody.fadeIn = w * .5f; columnBody.hold = .75f; columnBody.fadePower = 1.8f;
            }

            // A soft deep blue shade behind the caster (behind the actors) so the cold light reads on pale skies.
            var shade = Spawn("Frost cast shade", glow, pos + Vector2.up * .15f, 3.6f, new Color(.01f, .06f, .2f, .32f), life, false, 28);
            shade.fadeIn = w * .4f; shade.hold = hold; shade.fadePower = 1.6f; shade.startScale = .6f; shade.endScale = 1.1f;

            // A frost seal of crisp strokes spreads across the floor toward the nova radius over a bed of cold light.
            float sealWidth = R * 1.75f;
            var seal = Spawn("Frost seal", FrostSpriteSigil, ground + Vector2.up * .03f, sealWidth, Alpha(FrostPale, .75f), life, true, 27);
            FrostSquash(seal, .24f); seal.fadeIn = .05f; seal.startScale = .2f; seal.endScale = 1; seal.ease = 4; seal.hold = hold; seal.fadePower = 1.4f;
            var floor = GroundGlow(ground, sealWidth * 1.1f, Alpha(light, .35f), life); floor.hold = hold; floor.fadeIn = .06f; floor.startScale = .3f; floor.endScale = 1; floor.ease = 3;
            Emitter(floor, 20, 0, (owner, p) =>
            {
                float t = Mathf.Clamp01(owner.age / Mathf.Max(.01f, owner.life * .6f));
                Vector2 at = new Vector2(ground.x + Range(-1f, 1f) * R * (.25f + .6f * t), ground.y + .05f);
                if (Chance(.35f))
                {
                    var breath = FrostMist(at, Range(.35f, .55f), Chance(.4f) ? FrostBlue : Color.white, .8f, new Vector2(Range(-.2f, .2f), Range(.9f, 1.6f)), Range(.35f, .5f), 114);
                    breath.drag = 1.5f; breath.endScale = 1.6f; breath.fadeIn = 0;
                }
                else
                {
                    var mote = Spawn("Rising frost", fleck, at + Vector2.up * Range(0, .4f), Range(.16f, .28f), Alpha(FrostPale, 1), Range(.3f, .5f), true, 131);
                    mote.velocity = new Vector2(Range(-.3f, .3f), Range(1.4f, 2.8f)); mote.endScale = .3f; mote.ease = 1; mote.angle = Range(-20, 20);
                }
            });

            // An upright frost circle turns behind the caster (drawn under the actors) and flares at the release.
            var circle = Spawn("Frost circle", FrostSpriteSigil, pos + Vector2.up * .1f, 3.4f * gs, Alpha(FrostPale, .7f), w + .18f, true, 29);
            circle.spin = 75 * c.facing; circle.fadeIn = .06f; circle.startScale = .35f; circle.endScale = 1.2f; circle.ease = 3; circle.hold = w / (w + .18f); circle.fadePower = 1.4f;

            // The charge: a luminous ice gem grows in the hand round a swirling plasma heart, over one soft bloom.
            var halo = Anchored(c.anchor, hand, "Frost charge halo", glow, 2.2f * gs, Alpha(FrostBlue, .42f), w + .03f, 125);
            var plasma = Anchored(c.anchor, hand, "Frost charge plasma", plasmaFrames[0], 1.1f * gs, Alpha(FrostBlue, .9f), w + .03f, 129);
            plasma.frames = plasmaFrames; plasma.fps = 16;
            var gem = Anchored(c.anchor, hand, "Frost charge gem", FrostSpriteGem, .95f * gs, Alpha(light, 1), w + .03f, 131); gem.angle = -c.facing * 12;
            foreach (var layer in new[] { halo, plasma, gem }) { layer.startScale = .15f; layer.endScale = 1; layer.ease = 1.5f; layer.hold = .9f; layer.fadeIn = .04f; }
            plasma.loop = true;

            // Light and snowflakes spiral into the hand, with a few pale mist billows.
            Converge(hand, 10, 2.9f, light, w, .45f);
            int flakes = Budget(10);
            for (int i = 0; i < flakes; i++)
            {
                var f = Spawn("Gathering snowflake", FrostSpriteFlake, hand, Range(.34f, .56f), Alpha(i % 3 == 0 ? FrostBlue : FrostPale, 1), w * Range(.8f, 1), true, 130);
                f.orbitCenter = hand; f.orbitAngle = Range(0, 360); f.orbit = (i % 2 == 0 ? 1 : -1) * Range(200, 320); f.orbitRadius = Range(1.8f, 3.2f); f.orbitRadiusEnd = .15f; f.orbitPower = 1.3f;
                f.spin = Range(-300, 300); f.fadeIn = .06f; f.hold = .7f; f.endScale = .5f; f.ease = 1;
            }
            for (int i = 0; i < 4; i++)
            {
                var breath = FrostMist(pos, Range(1f, 1.4f), i % 2 == 0 ? Color.white : FrostBlue, .8f, Vector2.zero, w * Range(.85f, 1.05f), 114);
                breath.orbitCenter = pos; breath.orbitAngle = i * 90 + Range(-20, 20); breath.orbit = c.facing * 160; breath.orbitRadius = Range(2.2f, 2.8f); breath.orbitRadiusEnd = .5f; breath.orbitPower = 1.2f;
                breath.fadeIn = w * .3f; breath.startScale = 1.1f; breath.endScale = .45f; breath.ease = 1; breath.fadePower = 1.2f;
            }

            // A crown of painted ice crystals forms around the caster, pointing outward.
            int crystals = 6 + 2 * (g - 1);
            for (int i = 0; i < crystals; i++)
            {
                float a0 = i * 360f / crystals + 30, delay = i * w * .3f / crystals;
                var crystal = Spawn("Forming crystal", FrostSpriteShard, pos, 1.05f, Color.white, w + .02f - delay, false, 129);
                crystal.orbitCenter = pos + Vector2.up * .1f; crystal.orbitAngle = a0; crystal.orbit = 70 * c.facing; crystal.orbitRadius = 1.75f; crystal.orbitRadiusEnd = 1.15f;
                crystal.angle = a0; crystal.spin = 70 * c.facing;
                crystal.startScale = .05f; crystal.endScale = 1; crystal.ease = 3; crystal.hold = .85f; crystal.fadePower = 2;
                FrostDelay(crystal, delay);
            }

            // Frost teeth creep along the floor from the caster to the edge of the nova, telegraphing its reach;
            // the last tooth on each side is the largest and stands on the rim.
            int teeth = Budget(5);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < teeth; i++)
                {
                    float t = (i + 1f) / teeth, x = Mathf.Lerp(.75f, R * .96f, t), appear = w * Mathf.Lerp(.12f, .78f, t);
                    bool rim = i == teeth - 1;
                    var tooth = Spawn("Frost tooth", FrostSpriteCrystal, new Vector2(ground.x + side * x, ground.y - .02f), (rim ? .6f : Range(.34f, .45f)) * Mathf.Clamp(R / 3.5f, .6f, 1.2f),
                        Color.white, w + .07f - appear, false, 28);
                    tooth.angle = -side * Range(8, 20); tooth.axisStart = new Vector2(.5f, 0); tooth.axisEnd = Vector2.one; tooth.ease = 6; tooth.hold = .82f; tooth.fadePower = 2;
                    FrostDelay(tooth, appear);
                }

            // Release: the gem flares into a drawn spark with a glint.
            float at0 = Mathf.Max(0, w - .02f);
            int first = clips.Count;
            FrostHandFlash(hand, 2.2f, light, .16f);
            Glint(hand, 2.2f, Color.white, .2f, 20);
            FrostDelayFrom(first, at0);
        }

        private void ImpactIceNova(Vector2 pos, float radius, Color tint, int facing)
        {
            float R = Mathf.Clamp(radius, .8f, 6f), k = Mathf.Clamp(R / 3.5f, .35f, 1.3f), kk = Mathf.Clamp(k, .55f, 1.15f);
            Vector2 ground = new Vector2(pos.x, pos.y - .92f);
            Color light = Color.Lerp(IceLight, tint, .2f);
            // The area itself grows with the level (R arrives at its true size); on top, crystals stand taller, the centre
            // fan widens, a second frost ring follows (level two) and level three flanks the fan with two more and raises a
            // column of cold light behind the caster.
            int g = Grade; float gh = 1 + (g - 1) * .15f;
            // (At level three the awakening's closing lock ring takes this second ring's place.)
            if (g == 2) FrostDelay(FrostLevel(GroundWave(ground + Vector2.up * .06f, R * 2 / .78f, new Color(.85f, .97f, 1, .9f), .45f)), .16f);
            if (g >= 3)
            {
                for (int side = -1; side <= 1; side += 2) FrostCrystalFan(ground + new Vector2(side * R * .5f, 0), Mathf.Clamp(R * .7f, 1.6f, 3.4f), light);
                var column = Pillar(ground, 2, 8, Alpha(light, .85f), .8f, 27); column.hold = .3f;
                var columnCore = Pillar(ground, .6f, 8.4f, new Color(.92f, .99f, 1, 1), .55f, 28); columnCore.hold = .25f;
                var columnBody = FrostColumnBody(ground, 1.7f, 8, .42f, .9f); columnBody.hold = .3f; columnBody.fadePower = 1.3f;
            }

            // Peak: an ice-blue blast billows out of a white heart behind the caster (under the actors, so the hero stays clear),
            // over a soft deep blue shade; a drawn spark, a luminous gem heart, a shock front, a glint and pale impact wedges.
            var shade = Spawn("Nova shade", glow, pos, R * 1.8f + 1, new Color(.01f, .06f, .2f, .38f), .5f, false, 25);
            shade.startScale = .5f; shade.endScale = 1.15f; shade.ease = 4; shade.fadePower = 1.6f;
            // Everything centred on the caster below sits behind the actors (29), so the hero stays readable through the peak.
            // The peak keeps one size at every level (a bigger area grows the rings, ice and frost, not the glare).
            float starWidth = Mathf.Min(R * 1.1f + 1.2f, 4.6f);
            FrostBurst(pos + Vector2.up * .2f, starWidth * 1.3f, Color.Lerp(FrostBlue, light, .3f), FrostDeep, .55f, 29);
            FrostHandFlash(pos + Vector2.up * .2f, starWidth * .4f, light, .12f);
            var heart = Spawn("Nova heart", FrostSpriteGem, pos + Vector2.up * .15f, .6f + .3f * kk, Alpha(light, 1), .16f, true, 29); heart.startScale = 1.15f; heart.endScale = .6f; heart.ease = 4; heart.angle = Range(-15, 15);
            var frame = ShockFlip(pos, 3.4f * kk + 1.5f, Alpha(light, .9f), .26f, 29); frame.startScale = .45f; frame.endScale = 1.25f; frame.ease = 6;
            Glint(pos, Mathf.Min(R * 1.4f + .6f, 5.6f), new Color(.9f, .98f, 1, 1), .26f, Range(0, 45));
            // Higher levels' wider wedges are drawn a little thinner, so the peak stays ice, not white.
            ImpactLines(pos, R * 2.9f, new Color(.85f, .97f, 1, .85f - (g - 1) * .1f), .24f);

            // Painted frost rings: the shock texture's rim sits at 78% of its half-width, so 2.56R puts it on the gameplay radius.
            float ringWidth = R * 2.56f;
            var nova = Wave(pos, ringWidth, Alpha(light, .95f), .34f, 5); FrostSquash(nova, .62f); FrostLevel(nova); nova.renderer.sortingOrder = 29;
            FrostLevel(GroundWave(ground + Vector2.up * .06f, ringWidth * 1.02f, Alpha(light, 1), .45f));
            FrostDelay(FrostLevel(GroundWave(ground + Vector2.up * .06f, ringWidth * 1.1f, new Color(1, 1, 1, .8f), .4f)), .09f);

            // Frozen ground across the whole radius, glowing faintly, with a crisp frost seal that lingers.
            var sheet = Spawn("Frozen ground", FrostSpriteIce, ground + Vector2.up * .02f, R * 2.45f, Color.white, 2f, false, 24);
            FrostSquash(sheet, .2f); sheet.startScale = .2f; sheet.endScale = 1; sheet.ease = 7; sheet.hold = .5f; sheet.fadePower = 1.3f; sheet.fadeIn = .02f;
            var iceGlow = GroundGlow(ground, R * 2.4f, Alpha(light, .28f), 1.5f); iceGlow.hold = .4f; iceGlow.startScale = .3f; iceGlow.endScale = 1; iceGlow.ease = 6;
            var seal = Spawn("Frost seal", FrostSpriteSigil, ground + Vector2.up * .03f, R * 2.05f, Alpha(FrostPale, .8f), 1.2f, true, 27);
            FrostSquash(seal, .22f); seal.startScale = .3f; seal.endScale = 1; seal.ease = 6; seal.hold = .4f; seal.fadePower = 1.4f;

            // Ice crystals erupt outward from the caster in a front and a back row, then shatter.
            int front = Mathf.Clamp(Mathf.RoundToInt(R * 1.9f), 3, 7), back = Mathf.Clamp(Mathf.RoundToInt(R * 1.3f), 2, 6);
            const float travel = .2f;
            // The centrepiece: a fan of great crystals bursts up behind the caster at the peak, lit from within, then shatters.
            FrostCrystalFan(ground, Mathf.Clamp(R * 1.25f, 2.2f, 6) * (1 + (g - 1) * .1f), light);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < front; i++)
                {
                    // The front row (drawn over the actors) starts clear of the caster and stays low near them; the level
                    // raises only the outer ice, so the hero is never buried in the wall.
                    float t = (i + .4f + Range(-.15f, .15f)) / front, x = Mathf.Lerp(1.1f, R * .97f, t);
                    float h = Mathf.Lerp(2.3f, 1.35f, t) * Range(.85f, 1.15f) * kk * (t > .55f ? gh : 1);
                    if (x < 1.8f) h = Mathf.Min(h, 1.3f);
                    // The rim crystal is raised so the edge of the nova reads.
                    if (i == front - 1) { x = R * .97f; h = 1.75f * kk * gh; }
                    FrostSpike(new Vector2(ground.x + side * x, ground.y - .1f), h, side * Range(6, 22), .02f + travel * x / R, Range(1, 1.25f), true, light);
                }
                for (int i = 0; i < back; i++)
                {
                    float t = (i + .7f) / (back + .4f), x = Mathf.Lerp(.6f, R * .9f, t);
                    float h = Mathf.Lerp(2, 1.15f, t) * Range(.85f, 1.1f) * kk * gh;
                    FrostSpike(new Vector2(ground.x + side * x, ground.y + .12f), h, side * Range(-8, 14), .04f + travel * x / R, Range(1, 1.2f), false, light);
                }
                // A wall of cold light races out along the floor with the ice, from beside the caster and behind the actors.
                var wall = Pillar(ground + new Vector2(side * .55f, -.05f), 1.1f * k + .3f, 2.4f * k + .4f, Alpha(light, .9f), .28f, 29);
                wall.velocity = new Vector2(side * R * 5.5f, 0); wall.drag = 5;
            }

            // Painted ice chips and needles fly, snowflakes drift; pale frost mist billows roll away along the floor.
            int shards = Budget(Mathf.RoundToInt(9 + 3 * Mathf.Min(1, k)));
            for (int i = 0; i < shards; i++)
            {
                Vector2 direction = Direction(Range(8, 172));
                var chip = Spawn("Ice shard", FrostSpriteShard, pos + direction * .3f, Range(.3f, .6f) * kk, Color.white, Range(.6f, .9f), false, 129);
                chip.velocity = direction * (R * Range(1.8f, 3.6f) + 1.5f); chip.gravity = 12; chip.drag = .8f; chip.alignVelocity = true; chip.fadePower = 1.1f; chip.hold = .45f;
            }
            Needles(pos, 9, R * 3.2f + 1, FrostPale, .4f, .5f);
            int snow = Budget(Mathf.RoundToInt(8 * Mathf.Min(1, kk)));
            for (int i = 0; i < snow; i++)
            {
                var f = Spawn("Snowflake", FrostSpriteFlake, pos + Inside(R * .35f), Range(.26f, .46f), Alpha(i % 3 == 0 ? FrostBlue : FrostPale, 1), Range(1, 1.5f), true, 130);
                f.velocity = new Vector2(Range(-1, 1) * R * 1.4f, Range(.5f, 2.5f)); f.drag = 1.8f; f.gravity = .6f; f.spin = Range(-220, 220);
                f.hold = .4f; f.fadePower = 1.2f; f.angle = Range(0, 60);
            }
            // Mist starts at the ice front (not over the caster) so the peak frame stays crisp, then rolls outward.
            int mist = Budget(Mathf.RoundToInt(7 * Mathf.Min(1, kk)));
            for (int i = 0; i < mist; i++)
            {
                int side = i % 2 == 0 ? 1 : -1;
                var m = FrostMist(ground + new Vector2(side * Range(.35f, .65f) * R, Range(.12f, .45f)), Range(1, 1.45f) * kk, i % 3 != 2 ? FrostBlue : Color.white, .85f,
                    new Vector2(side * Range(2.5f, 5) * k, Range(.1f, .4f)), Range(.7f, 1f), 113);
                m.drag = 2.4f; m.startScale = .45f; m.endScale = 1.6f; m.fadeIn = 0; m.hold = .3f;
                FrostDelay(m, Range(.04f, .1f));
            }
            int fallCount = Budget(Mathf.RoundToInt(6 * Mathf.Min(1, kk)));
            for (int i = 0; i < fallCount; i++)
            {
                var fall = Spawn("Falling snow", FrostSpriteFlake, ground + new Vector2(Range(-.95f, .95f) * R, Range(1.4f, 3.4f) * kk), Range(.24f, .4f), Alpha(i % 3 == 0 ? FrostBlue : FrostPale, 1), Range(1.2f, 1.7f), true, 130);
                fall.velocity = new Vector2(Range(-.4f, .4f), -Range(.4f, .9f)); fall.spin = Range(-160, 160); fall.angle = Range(0, 60); fall.fadeIn = .15f; fall.hold = .5f; fall.fadePower = 1.3f;
                FrostDelay(fall, Range(.12f, .6f));
            }
            Sparkles(pos, 4, R * .8f, FrostPale);
            for (int i = 0; i < 2; i++)
                FrostDelay(Glint(ground + new Vector2(Range(-.85f, .85f) * R, Range(.4f, 1.5f) * kk), Range(.5f, .9f), Color.white, .26f, Range(0, 45)), Range(.25f, .9f));
        }

        /// <summary>One painted ice crystal bursting from the floor (bottom pivot) with its inner light drawn over it, then
        /// breaking into tumbling fragments under a small drawn spark.</summary>
        private void FrostSpike(Vector2 at, float height, float lean, float delay, float life, bool front, Color light)
        {
            var body = Spawn("Ice spike", FrostSpriteCrystal, at, height * .5f, front ? Color.white : new Color(.8f, .88f, 1, 1), life, false, front ? 127 : 28);
            var shine = Spawn("Ice spike light", FrostSpriteCrystalGlow, at, height * .5f, Alpha(light, front ? .85f : .55f), life, true, front ? 128 : 29);
            foreach (var clip in new[] { body, shine })
            {
                clip.angle = -lean; clip.axisStart = new Vector2(.45f, 0); clip.axisEnd = Vector2.one; clip.ease = 8; clip.hold = .72f; clip.fadePower = 1.6f;
                FrostDelay(clip, delay);
            }
            if (!front) return;
            Vector2 up = Direction(90 - lean);
            float breakAt = delay + life * .74f;
            // The crystal bursts into a cluster of painted fragments that fly apart and tumble down (one clip), with a small
            // drawn spark where it breaks.
            var chips = Spawn("Ice spike shatter", FrostSpriteShatter, at + up * height * Range(.4f, .6f), Mathf.Clamp(height * .62f, .5f, 1.5f), Color.white, Range(.5f, .62f), false, 129);
            chips.startScale = .35f; chips.endScale = 1.5f; chips.ease = 3; chips.velocity = new Vector2(Range(-.7f, .7f), Range(.3f, 1.1f)); chips.gravity = 7;
            chips.spin = Range(-220, 220); chips.angle = Range(0, 360); chips.hold = .35f; chips.fadePower = 1.3f;
            FrostDelay(chips, breakAt);
            FrostDelay(SparkFlip(at + up * height * .45f, height * .65f, Alpha(FrostPale, .95f), .14f, 130), breakAt);
        }

        /// <summary>A fan of great ice crystals bursting up from one point behind the caster (bottom pivot): lit from within as
        /// it erupts over a soft bloom, standing through the nova, then shattering into fragments.</summary>
        private void FrostCrystalFan(Vector2 ground, float width, Color light)
        {
            const float life = 1.15f;
            float height = width * .625f, breakAt = life * .7f;
            Vector2 foot = ground + Vector2.up * .04f;
            var fan = Spawn("Ice crown", FrostSpriteFan, foot, width, Color.white, life, false, 28);
            var lit = Spawn("Ice crown light", FrostSpriteFanGlow, foot, width, Alpha(light, .9f), life, true, 29);
            foreach (var clip in new[] { fan, lit }) { clip.axisStart = new Vector2(.55f, .15f); clip.axisEnd = Vector2.one; clip.ease = 12; clip.hold = .66f; clip.fadePower = 1.6f; }
            var bloom = Spawn("Ice crown bloom", glow, ground + Vector2.up * height * .45f, width * 1.15f, Alpha(light, .45f), .55f, true, 27);
            bloom.startScale = .5f; bloom.endScale = 1.1f; bloom.ease = 3; bloom.fadePower = 1.5f;
            for (int i = 0; i < 3; i++)
            {
                Vector2 at = ground + new Vector2((i - 1) * width * .27f, height * (i == 1 ? .6f : .38f));
                var chips = Spawn("Ice crown shatter", FrostSpriteShatter, at, height * (i == 1 ? .7f : .55f), Color.white, .6f, false, 129);
                chips.startScale = .35f; chips.endScale = 1.55f; chips.ease = 3; chips.velocity = new Vector2((i - 1) * 1.3f + Range(-.3f, .3f), Range(.4f, 1.2f));
                chips.gravity = 8; chips.spin = Range(-160, 160); chips.angle = Range(0, 360); chips.hold = .35f; chips.fadePower = 1.3f;
                FrostDelay(chips, breakAt + i * .03f);
            }
            FrostDelay(SparkFlip(ground + Vector2.up * height * .5f, width * .6f, Alpha(FrostPale, 1), .18f, 130), breakAt);
            for (int side = -1; side <= 1; side += 2)
                FrostDelay(Glint(ground + new Vector2(side * width * Range(.12f, .22f), height * Range(.6f, .85f)), Range(.9f, 1.2f), Color.white, .24f, Range(0, 45)), Range(.2f, .45f));
        }

        /// <summary>The blast flipbook without the white hit spark the shared Blast lays on its heart, so a cold or toxic burst
        /// keeps its hue: a bloom, a body in <paramref name="deep"/> swelling behind, and the billowing burst in the colour's
        /// ramp. The caller adds a smaller spark of its own.</summary>
        private Clip FrostBurst(Vector2 pos, float width, Color color, Color deep, float life, int order)
        {
            var bloom = Spawn("Frost burst bloom", glow, pos, width * 1.25f, Alpha(color, .55f), life * .6f, true, order - 2); bloom.endScale = 1.3f; bloom.ease = 1; bloom.fadePower = 1.5f;
            var back = Spawn("Frost burst body", blastFrames[0], pos + Vector2.down * width * .04f, width * 1.12f, Alpha(deep, .85f), life * 1.1f, true, order - 1);
            var blast = Spawn("Frost burst", blastFrames[0], pos, width, Alpha(color, 1), life, true, order);
            foreach (var clip in new[] { back, blast }) { clip.frames = blastFrames; clip.loop = false; clip.angle = Range(-12, 12); clip.startScale = .9f; clip.endScale = 1.08f; clip.ease = 2; clip.hold = .6f; clip.fadePower = .9f; }
            back.angle = blast.angle + Range(-25, 25);
            return blast;
        }

        /// <summary>A spark flaring at (or over) the caster: the drawn spark over the actors in pale light and its bloom in
        /// <paramref name="color"/> behind them (28), so the release lights the hero up instead of washing them out.</summary>
        private void FrostHandFlash(Vector2 pos, float width, Color color, float life)
        {
            var bloom = Spawn("Frost hand bloom", glow, pos, width * 1.5f, Alpha(color, .6f), life * 1.4f, true, 28); bloom.endScale = 1.4f; bloom.ease = 1; bloom.fadePower = 1.4f;
            SparkFlip(pos, width, Alpha(Color.Lerp(color, Color.white, .6f), .95f), life, 131);
        }

        /// <summary>Keeps a squashed ring level: the shared shock frames turn to a random angle, which would tilt an ellipse
        /// squashed before it is turned; half turns keep it flat and still vary the breakup.</summary>
        private Clip FrostLevel(Clip ring) { ring.angle = Chance(.5f) ? 0 : 180; return ring; }

        /// <summary>A deep blue body (the pillar shape painted, narrower than the light column it stands behind).</summary>
        private Clip FrostColumnBody(Vector2 ground, float width, float height, float alpha, float life)
        {
            var body = Spawn("Frost column body", pillar, ground, width, Alpha(FrostDeep, alpha), life, false, 26);
            body.scale = new Vector3(width / pillar.bounds.size.x, height / pillar.bounds.size.y, 1); body.root.transform.localScale = body.scale;
            body.axisStart = new Vector2(.35f, .12f); body.axisEnd = Vector2.one; body.ease = 4;
            return body;
        }

        /// <summary>A billow of pale frost mist (the flipbook swells, opens and tears apart) in <paramref name="tint"/>.</summary>
        private Clip FrostMist(Vector2 pos, float width, Color tint, float alpha, Vector2 velocity, float life, int order)
        {
            var frames = FrostSpriteMists[random.Next(FrostSpriteMists.Length)];
            var clip = Spawn("Frost mist", frames[0], pos, width, Alpha(tint, alpha), life, false, order);
            clip.frames = frames; clip.loop = false;
            clip.velocity = velocity; clip.drag = 3; clip.startScale = .6f; clip.endScale = 1.45f; clip.ease = 1; clip.spin = Range(-30, 30); clip.angle = Range(-20, 20); clip.fadeIn = .04f; clip.fadePower = 1.1f;
            return clip;
        }

        // ---------------------------------------------------------------- poison cloud

        /// <summary>A billow of painted toxic smoke (violet shadow, green body, acid highlights, lit from the upper left) that
        /// swells, opens holes and tears apart as it drifts. <paramref name="shade"/> darkens it for the layers further back.</summary>
        private Clip FrostFume(Vector2 pos, float width, float shade, float alpha, Vector2 velocity, float life, int order)
        {
            var frames = FrostSpriteFumes[random.Next(FrostSpriteFumes.Length)];
            var clip = Spawn("Toxic puff", frames[0], pos, width, new Color(shade, shade, shade, alpha), life, false, order);
            clip.frames = frames; clip.loop = false;
            clip.velocity = velocity; clip.drag = 3; clip.startScale = .6f; clip.endScale = 1.4f; clip.ease = 3; clip.spin = Range(-25, 25); clip.angle = Range(-20, 20); clip.hold = .3f; clip.fadePower = 1.2f;
            return clip;
        }

        private void CastPoisonCloud(CastInfo c)
        {
            float w = c.windup, R = FrostAreaRadius(c.radius, 1.5f, 4);
            int f = c.facing;
            Vector2 pos = c.pos, hand = pos + new Vector2(f * .42f, .12f);
            if (w < .2f) { FrostQuickCast(c, hand, FrostAcid, FrostAcidHot, false); return; }
            // The orb charges until it is thrown; the flask then arcs onto the mark and lands at the release.
            float flight = Mathf.Clamp(w * .45f, .1f, .2f), throwAt = w - flight, life = throwAt + .06f;

            var shade = Spawn("Venom shade", glow, hand, 3f, new Color(.04f, .08f, .01f, .35f), throwAt + .2f, false, 28);
            shade.fadeIn = throwAt * .4f; shade.hold = throwAt / (throwAt + .2f); shade.fadePower = 1.5f; shade.startScale = .5f; shade.endScale = 1.1f;

            // A toxic orb churns in the hand: painted smoke masses turning round an acid plasma heart under one painted spiral of
            // acid. Higher levels swell it; level three adds a plague rune turning behind it.
            int g = Grade; float gs = 1 + (g - 1) * .12f;
            var murk = Anchored(c.anchor, hand, "Venom orb", FrostSpriteMiasma[0], 2.1f * gs, Color.white, life, 28, false); murk.spin = -220 * f;
            var plague = Anchored(c.anchor, hand, "Venom orb shadow", FrostSpriteMiasma[1], 1.7f * gs, new Color(.85f, .85f, .85f, 1), life, 29, false); plague.spin = 300 * f;
            var acid = Anchored(c.anchor, hand, "Venom vortex", swirl, 2.4f * gs, Alpha(FrostAcid, .8f), life, 126); acid.spin = -560 * f;
            var halo = Anchored(c.anchor, hand, "Venom halo", glow, 2.8f * gs, Alpha(FrostAcid, .45f), life, 125);
            var plasma = Anchored(c.anchor, hand, "Venom plasma", plasmaFrames[0], 1.35f * gs, Alpha(FrostAcid, 1), life, 127); plasma.frames = plasmaFrames; plasma.fps = 16;
            var heart = Anchored(c.anchor, hand, "Venom core", core, .5f * gs, new Color(.92f, 1, .75f, 1), life, 131);
            if (g >= 3)
            {
                var rune = Anchored(c.anchor, hand, "Venom plague rune", runeRing, 4.1f, Alpha(FrostViolet, .8f), life, 28); rune.spin = 90 * f;
                var runeGlow = Anchored(c.anchor, hand, "Venom plague rune glow", glow, 4.3f, Alpha(FrostViolet, .25f), life, 27);
                foreach (var layer in new[] { rune, runeGlow }) { layer.startScale = .3f; layer.endScale = 1; layer.ease = 3; layer.hold = .8f; layer.fadeIn = .06f; }
            }
            // Toxic fumes rise from the caster's feet while the orb charges.
            Vector2 feet = pos + Vector2.down * .9f;
            Emitter(shade, 14, 0, (owner, p) =>
            {
                var fume = FrostFume(feet + new Vector2(Range(-.7f, .7f), Range(0, .35f)), Range(.7f, 1f), Range(.8f, 1), .9f, new Vector2(Range(-.3f, .3f), Range(.8f, 1.4f)), Range(.45f, .6f), 29);
                fume.drag = 1.2f;
            });
            foreach (var layer in new[] { murk, plague, acid, halo, plasma, heart }) { layer.startScale = .25f; layer.endScale = 1; layer.ease = 2.5f; layer.hold = .82f; layer.fadeIn = .05f; }
            plasma.loop = true;
            Converge(hand, 12, 2.8f, FrostAcid, throwAt, .5f);
            for (int i = 0; i < 4; i++)
            {
                var fume = FrostFume(hand, Range(1f, 1.4f), .92f, .9f, Vector2.zero, throwAt * Range(.85f, 1.05f), 29);
                fume.orbitCenter = hand; fume.orbitAngle = i * 90 + Range(-15, 15); fume.orbit = -f * 220; fume.orbitRadius = Range(1.7f, 2.3f); fume.orbitRadiusEnd = .25f; fume.orbitPower = 1.2f;
                fume.fadeIn = w * .3f; fume.startScale = 1.1f; fume.endScale = .4f; fume.ease = 1; fume.hold = 0; fume.fadePower = 1.2f;
            }
            Emitter(halo, 12, .35f, (owner, p) =>
            {
                var bubble = Spawn("Venom bubble", FrostSpriteBubble, p, Range(.16f, .26f), Alpha(FrostAcid, .95f), Range(.35f, .5f), true, 128);
                bubble.velocity = new Vector2(Range(-.5f, .5f), Range(.8f, 1.7f)); bubble.startScale = .4f; bubble.endScale = 1.2f; bubble.ease = 3; bubble.hold = .7f; bubble.fadePower = 2;
            });

            // The landing spot is marked on the floor while the orb charges: a dim pool of shade and a plague rune of light.
            Vector2 mark = new Vector2(c.target.x, c.target.y - .7f + .03f);
            float markLife = w + .3f, markHold = w / markLife;
            var markFill = Spawn("Venom mark fill", glow, mark, R * 2.4f, new Color(.12f, .3f, .03f, .36f), markLife, false, 24);
            var markGlow = Spawn("Venom mark glow", glow, mark, R * 2.2f, Alpha(FrostAcid, .16f), markLife, true, 26);
            var markLight = Spawn("Venom mark", runeRing, mark, R * 2, Alpha(FrostViolet, .8f), markLife, true, 27);
            foreach (var m in new[] { markFill, markGlow, markLight }) { FrostSquash(m, .24f); m.fadeIn = w * .5f; m.startScale = .5f; m.endScale = 1; m.ease = 4; m.hold = markHold; m.fadePower = 1.4f; }

            // Throw: the orb bursts like a vial at the hand (a drawn spark over a small acid blast), glass and venom spray out.
            Vector2 aim = c.target - hand; float heading = aim.sqrMagnitude > .0001f ? Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg : (f > 0 ? 0 : 180);
            int first = clips.Count;
            FrostHandFlash(hand, 1.8f, FrostAcid, .14f);
            FrostBurst(hand + Vector2.right * f * .35f, 1.6f, FrostAcid, new Color(.3f, .62f, .1f), .3f, 126);
            var popFume = FrostFume(hand, 1.6f, .85f, 1, Vector2.zero, .4f, 117); popFume.spin = 90;
            for (int i = 0; i < Budget(6); i++)
            {
                var glass = Spawn("Vial glass", FrostSpriteShard, hand, Range(.22f, .36f), new Color(.82f, 1, .72f, 1), Range(.4f, .55f), false, 129);
                glass.velocity = Direction(heading + Range(-80, 80)) * Range(3, 6) + Vector2.up * 1.5f; glass.gravity = 12; glass.spin = Range(-700, 700); glass.angle = Range(0, 360); glass.hold = .4f;
            }
            for (int i = 0; i < Budget(5); i++)
            {
                var drop = Spawn("Venom spray", FrostSpriteDrop, hand, Range(.18f, .28f), Color.white, Range(.3f, .45f), false, 128);
                drop.velocity = Direction(heading + Range(-35, 35)) * Range(4, 7) + Vector2.up * Range(.5f, 2.5f); drop.gravity = 15; drop.stretch = .05f; drop.alignVelocity = true; drop.hold = .5f;
            }

            // The flask of venom arcs onto the mark, trailing drips and toxic puffs, and lands exactly at the release.
            Vector2 landing = mark + Vector2.up * .3f;
            const float gravity = 34;
            Vector2 launch = (landing - hand) / flight + Vector2.up * (.5f * gravity * flight);
            var flaskBody = Spawn("Venom flask body", FrostSpriteDrop, hand, .8f, new Color(.8f, .8f, .8f, 1), flight, false, 129);
            var flask = Spawn("Venom flask", plasmaFrames[0], hand, 1f, Alpha(FrostAcid, 1), flight, true, 130); flask.frames = plasmaFrames; flask.fps = 18; flask.loop = true;
            var flaskGlow = Spawn("Venom flask glow", glow, hand, 2.2f, Alpha(FrostAcid, .5f), flight, true, 128);
            var flaskCore = Spawn("Venom flask core", core, hand, .34f, FrostAcidHot, flight, true, 131);
            foreach (var clip in new[] { flaskBody, flask, flaskGlow, flaskCore }) { clip.velocity = launch; clip.gravity = gravity; clip.fadePower = 0; }
            Emitter(flaskGlow, 40, .12f, (owner, p) =>
            {
                if (Chance(.55f))
                {
                    var drip = Spawn("Venom drip", FrostSpriteDrop, p, Range(.14f, .22f), Color.white, Range(.3f, .45f), false, 126);
                    drip.velocity = owner.velocity * .15f + new Vector2(Range(-.5f, .5f), Range(-.5f, .5f)); drip.gravity = 14; drip.fadePower = 1.3f;
                }
                else FrostFume(p, Range(.4f, .6f), Range(.85f, 1), .9f, Vector2.zero, Range(.25f, .35f), 117);
            });
            FrostDelayFrom(first, throwAt);
        }

        private void ImpactPoisonCloud(Vector2 pos, float radius, Color tint, int facing)
        {
            float r = Mathf.Clamp(radius, .7f, 4), k = Mathf.Clamp(r / 2, .45f, 1.4f);
            Vector2 ground = new Vector2(pos.x, pos.y - .72f);
            FrostVenomBurst(ground, r, k);
            var pool = Spawn("Venom puddle", FrostSpritePool, ground + Vector2.up * .02f, r * 2.5f, Color.white, 1.6f, false, 24);
            FrostSquash(pool, .24f); pool.startScale = .2f; pool.endScale = 1; pool.ease = 7; pool.hold = .45f; pool.fadePower = 1.3f; pool.fadeIn = .03f;
            GroundGlow(ground, r * 2.5f, Alpha(FrostAcid, .3f), 1.1f);
            FrostSkullWisp(pos + new Vector2(Range(-.2f, .2f), .25f), 1.15f + .4f * k, .12f);
            for (int i = 0; i < Budget(5); i++)
            {
                var linger = FrostFume(ground + new Vector2(Range(-.8f, .8f) * r, Range(.3f, 1.1f) * k), Range(1, 1.4f) * k, i % 2 == 0 ? .82f : 1, .95f, new Vector2(Range(-.4f, .4f), Range(.2f, .5f)), Range(1, 1.4f), i % 3 == 0 ? 113 : 29);
                linger.drag = 1; linger.endScale = 1.7f; FrostDelay(linger, Range(0, .15f));
            }
            FrostArc(pos + new Vector2(-r * .5f, Range(-.2f, .4f)), pos + new Vector2(r * .4f, Range(-.2f, .5f)), FrostAcid, .07f, .14f);
        }

        private void ZonePoisonCloud(Transform root, float radius, float life)
        {
            float r = Mathf.Clamp(radius, .8f, 5), k = Mathf.Clamp(r / 2.8f, .4f, 1.6f);
            Vector2 center = root.position;
            const float lift = .6f;
            Vector2 floor = new Vector2(0, -lift + .03f);

            // Glossy acid pool across the radius, a dim glow on the floor and a plague sigil.
            var pool = Attach(root, "Venom pool", FrostSpritePool, r * 2.55f, Color.white, 24, false, floor); FrostSquash(pool, .22f);
            var poolGlow = Attach(root, "Venom pool glow", glow, r * 2.6f, Alpha(FrostAcid, .3f), 26, true, floor); FrostSquash(poolGlow, .3f); poolGlow.pulse = .07f;
            var sigilMark = Attach(root, "Venom sigil", runeRing, r * 2.1f, Alpha(FrostViolet, .55f), 27, true, floor + Vector2.up * .01f); FrostSquash(sigilMark, .22f);
            // The churning body: a soft dark shade behind the actors, then clusters of painted toxic smoke lit from within by a
            // soft acid glow, big ones behind the actors and smaller ones riding the top of the cloud in front, so the cloud has
            // depth and the enemies inside stay readable. Higher levels boil harder (more clusters, denser fumes, more skulls
            // and arcs) inside the same true radius; level three also turns a plague seal on the floor. Its root creeps after
            // the nearest enemy, and every layer and emitter follows the root.
            int g = Grade;
            float kk = Mathf.Clamp(k, .6f, 1.3f), rate = Mathf.Min(k, 1.25f) * (1 + (g - 1) * .25f);
            var body = Attach(root, "Miasma shadow", glow, r * 2.4f, new Color(.05f, .13f, .02f, .35f), 27, false, new Vector2(0, .75f)); FrostSquash(body, .62f); body.pulse = .05f;
            var haze = Attach(root, "Miasma spark host", glow, r * 2f, Alpha(FrostAcid, .1f), 116, true, new Vector2(0, .7f)); FrostSquash(haze, .6f);
            var layers = new System.Collections.Generic.List<Clip> { poolGlow, sigilMark, body, haze };
            float[] spots = g >= 3 ? new[] { -.62f, -.31f, 0, .31f, .62f } : g == 2 ? new[] { -.6f, -.2f, .2f, .6f } : new[] { -.55f, 0, .55f };
            int mid = spots.Length / 2; float shrink = Mathf.Pow(3f / spots.Length, .35f);
            Clip geyserHost = null;
            for (int i = 0; i < spots.Length; i++)
            {
                bool centre = i == mid;
                Vector2 offset = new Vector2(spots[i] * r, .75f + (centre ? .45f : (i % 2) * .2f));
                float size = (centre ? 3.1f : 2.5f) * kk * shrink, turn = (i % 2 == 0 ? 1 : -1) * 12;
                var under = Attach(root, "Miasma cloud", FrostSpriteMiasma[i % FrostSpriteMiasma.Length], size * 1.15f, new Color(.82f, .82f, .82f, 1), 28, false, offset + new Vector2(-.15f, -.1f)); under.spin = turn; under.pulse = .03f;
                var inner = Attach(root, "Miasma inner glow", glow, size * .9f, Alpha(FrostAcid, .2f), 29, true, offset + new Vector2(.05f, .05f)); inner.pulse = .12f;
                var top = Attach(root, "Miasma crest", FrostSpriteMiasma[(i + 2) % FrostSpriteMiasma.Length], size * .62f, Color.white, 113, false, offset + new Vector2(.15f, size * .3f)); top.spin = -turn * 1.3f; top.pulse = .04f;
                layers.Add(under); layers.Add(inner); layers.Add(top);
                if (centre) geyserHost = top;
            }
            if (g >= 3)
            {
                // A squashed child on the floor, so the plague seal turns in the ground plane.
                var plane = new GameObject("Venom ground plane").transform; plane.SetParent(root, false);
                plane.localPosition = new Vector3(0, floor.y + .01f, 0); plane.localScale = new Vector3(1, .26f, 1);
                // A hexagram curse seal (not the ice nova's snowflake) in plague violet over a soft bed of its glow.
                var plagueGlow = Attach(plane, "Venom plague seal glow", glow, r * 2 / .93f, Alpha(FrostViolet, .22f), 25);
                var plagueSeal = Attach(plane, "Venom plague seal", sigil, r * 2 / .93f, Alpha(Color.Lerp(FrostViolet, FrostAcid, .2f), .7f), 27); plagueSeal.spin = -30;
                layers.Add(plagueGlow); layers.Add(plagueSeal);
            }
            foreach (var layer in layers) FrostZoneLayer(layer, life);
            FrostZoneLayer(pool, life);
            Bundle(root, pool, layers.ToArray());

            // The cloud is full at once, then toxic billows keep boiling inside the radius (mostly behind the actors).
            for (int i = 0; i < Budget(Mathf.RoundToInt(10 * kk)); i++)
            {
                bool behind = i % 3 != 0;
                float x = Range(-.9f, .9f) * r;
                var fill = FrostFume(center + new Vector2(x, Range(-.35f, 1.5f) * (1 - .4f * Mathf.Abs(x / r))), Range(1.3f, 2f) * kk * (behind ? 1 : .7f), behind ? .82f : 1, behind ? .95f : .72f,
                    new Vector2(Range(-.4f, .4f), Range(.1f, .4f)), Range(1.3f, 1.9f), behind ? 28 : 113);
                fill.drag = .8f; fill.startScale = .4f; fill.endScale = 1.4f; fill.hold = .35f;
                FrostDelay(fill, Range(0, .12f));
            }
            // When the cloud creeps (level three), new fumes carry its drift so the body moves as one mass.
            Vector2 lastCenter = center, drift = Vector2.zero; float lastAge = 0;
            Emitter(body, 16 * rate, 0, (owner, p) =>
            {
                if (FrostZoneEnding(owner)) return;
                Vector2 o = FrostZoneCenter(owner);
                if (owner.age > lastAge + .05f) { drift = Vector2.Lerp(drift, (o - lastCenter) / (owner.age - lastAge), .5f); lastCenter = o; lastAge = owner.age; }
                bool behind = Chance(.72f);
                float x = Range(-.92f, .92f) * r, y = Range(-.4f, 1.7f) * (1 - .4f * Mathf.Abs(x / r));
                var puff = FrostFume(o + new Vector2(x, y + (behind ? 0 : .5f)), Range(1.2f, 2f) * kk * (behind ? 1 : .7f), behind ? .82f : 1, behind ? .95f : .72f,
                    new Vector2(-Mathf.Sign(x) * Range(.1f, .5f) + Range(-.2f, .2f), Range(.15f, .5f)) + drift, Range(1.3f, 1.8f), behind ? 28 : 113);
                puff.drag = .8f; puff.startScale = .5f; puff.endScale = 1.5f; puff.hold = .35f;
            });
            // Toxic sparks.
            Emitter(haze, 6 * rate, 0, (owner, p) =>
            {
                if (FrostZoneEnding(owner)) return;
                Vector2 at = FrostZoneCenter(owner) + new Vector2(Range(-.85f, .85f) * r, Range(-.5f, .8f));
                var spark = Spark(at, new Vector2(Range(-.6f, .6f), Range(1.2f, 2.6f)), Chance(.3f) ? FrostAcidHot : FrostAcid, Range(.2f, .32f), Range(.4f, .65f), -1, 1.2f);
                spark.flicker = .3f;
            });
            // Glossy bubbles of light rising out of the acid.
            Emitter(pool, 12 * rate, 0, (owner, p) =>
            {
                if (FrostZoneEnding(owner)) return;
                Vector2 at = FrostZoneCenter(owner) + new Vector2(Range(-.8f, .8f) * r, -lift + Range(0, .12f));
                var bubble = Spawn("Venom bubble", FrostSpriteBubble, at, Range(.24f, .42f), Alpha(FrostAcid, .95f), Range(.4f, .65f), true, 119);
                bubble.velocity = new Vector2(Range(-.15f, .15f), Range(.4f, .9f)); bubble.startScale = .4f; bubble.endScale = 1.25f; bubble.ease = 3; bubble.hold = .75f; bubble.fadePower = 2;
            });
            // Now and then the acid erupts in a small geyser: a jet of acid flame under a drawn spark, flinging drops.
            Emitter(geyserHost, 1.3f * rate, 0, (owner, p) =>
            {
                if (FrostZoneEnding(owner)) return;
                Vector2 o = FrostZoneCenter(owner), at = new Vector2(o.x + Range(-.7f, .7f) * r, o.y - lift + .03f);
                SparkFlip(at + Vector2.up * .35f, 1.1f, Alpha(FrostAcid, 1), .16f, 125);
                var jet = Sheet("Venom geyser", flame, at + Vector2.up * .42f, .62f, Alpha(FrostAcid, .95f), .34f, 121, 22, true);
                jet.velocity = Vector2.up * 1.2f; jet.drag = 2; jet.angle = Range(-10, 10); jet.startScale = .6f; jet.endScale = 1.25f; jet.ease = 4; jet.hold = .25f; jet.fadePower = 1.3f;
                for (int i = 0; i < 4; i++)
                {
                    var drop = Spawn("Venom geyser drop", FrostSpriteDrop, at + Vector2.up * Range(.3f, .9f), Range(.16f, .26f), Color.white, Range(.45f, .6f), false, 122);
                    drop.velocity = new Vector2(Range(-1.5f, 1.5f), Range(3, 5.5f)); drop.gravity = 14; drop.stretch = .04f; drop.alignVelocity = true; drop.hold = .5f; drop.fadePower = 1.3f;
                }
            });
            // Skulls drift up out of the cloud.
            Emitter(sigilMark, 1.4f * rate, 0, (owner, p) =>
            {
                if (FrostZoneEnding(owner)) return;
                FrostSkullWisp(FrostZoneCenter(owner) + new Vector2(Range(-.6f, .6f) * r, Range(-.1f, .7f)), Range(1.2f, 1.6f), 0);
            });
            FrostSkullWisp(center + new Vector2(Range(-.3f, .3f) * r, .3f), 1.5f, .25f);
            // Green arcs crackle inside.
            Emitter(poolGlow, 2.2f * rate, 0, (owner, p) =>
            {
                if (FrostZoneEnding(owner)) return;
                Vector2 a = FrostZoneCenter(owner) + new Vector2(Range(-.75f, .75f) * r, Range(-.4f, .9f));
                FrostArc(a, a + Direction(Range(0, 360)) * Range(.7f, 1.4f), Chance(.3f) ? FrostViolet : FrostAcid, .06f, .13f);
            });

            // Landing: the vial splashes down.
            FrostVenomBurst(center + Vector2.down * lift, r, k);
        }

        /// <summary>An acid splash on the floor: an acid blast with a violet lobe bursting off it, a drawn spark, a shock front,
        /// pale acid impact wedges, a ground ring, a painted crown of venom tendrils, droplets and billowing toxic smoke.</summary>
        private void FrostVenomBurst(Vector2 ground, float r, float k)
        {
            float kk = Mathf.Clamp(k, .6f, 1.3f);
            Vector2 mid = ground + Vector2.up * (.55f * kk);
            var shade = Spawn("Venom burst shade", glow, mid, r * 2 + 1.2f, new Color(.03f, .09f, .01f, .38f), .6f, false, 29); shade.startScale = .6f; shade.endScale = 1.2f; shade.ease = 3; shade.fadePower = 1.4f;
            float blastWidth = Mathf.Min(r * 1.3f + 1.4f, 4.8f);
            FrostBurst(mid, blastWidth, FrostAcid, new Color(.3f, .62f, .1f), .6f, 128);
            int first = clips.Count;
            FrostBurst(mid + new Vector2(Range(-.4f, .4f) * kk, .4f * kk), blastWidth * .62f, FrostViolet, new Color(.42f, .16f, .7f), .5f, 126);
            FrostDelayFrom(first, .06f);
            Flash(mid, 1.5f * kk, Alpha(FrostAcidHot, 1), .12f);
            var shock = ShockFlip(mid, 2.4f * kk + 1, Alpha(FrostAcid, .9f), .26f, 131); shock.startScale = .45f; shock.endScale = 1.2f;
            float lineWidth = r * 1.7f + 1.6f;
            ImpactLines(mid, lineWidth, Alpha(FrostAcid, .85f), .22f);
            Glint(mid, 2.2f * kk, Alpha(FrostAcidHot, 1), .22f, Range(0, 45));
            FrostLevel(GroundWave(ground + Vector2.up * .05f, r * 2.56f, Alpha(FrostAcid, .95f), .42f));

            // The venom erupts in a splash crown (behind the actors, lit for a moment) that flings droplets from its tips.
            float crownWidth = Mathf.Clamp(r * 1.3f, 1.6f, 4.8f), crownHeight = crownWidth * .7f;
            Vector2 crownFoot = ground + Vector2.down * .04f;
            var crown = Spawn("Venom splash crown", FrostSpriteCrown, crownFoot, crownWidth, Color.white, .6f, false, 29);
            crown.axisStart = new Vector2(.6f, .25f); crown.axisEnd = Vector2.one; crown.ease = 10; crown.hold = .4f; crown.fadePower = 1.4f;
            var crownLight = Spawn("Venom splash light", FrostSpriteCrown, crownFoot, crownWidth, Alpha(FrostAcid, .6f), .5f, true, 121);
            crownLight.axisStart = crown.axisStart; crownLight.axisEnd = Vector2.one; crownLight.ease = 10; crownLight.fadePower = 5;
            int drops = Budget(Mathf.RoundToInt(8 * kk));
            for (int i = 0; i < drops; i++)
            {
                float t = drops == 1 ? 0 : i / (float)(drops - 1) * 2 - 1;
                Vector2 tip = ground + new Vector2(t * crownWidth * .36f, crownHeight * Range(.55f, .95f) * (1 - .4f * Mathf.Abs(t)));
                var drop = Spawn("Venom droplet", FrostSpriteDrop, tip, Range(.2f, .32f), Color.white, Range(.5f, .75f), false, 123);
                drop.velocity = new Vector2(t * Range(2.5f, 4.5f), Range(2.5f, 5.5f)); drop.gravity = 16; drop.stretch = .05f; drop.alignVelocity = true; drop.hold = .5f; drop.fadePower = 1.3f;
                FrostDelay(drop, .06f);
            }
            // A toxic mushroom: billows shoot up the middle while others roll out along the floor.
            int plumes = Budget(3);
            for (int i = 0; i < plumes; i++)
            {
                var plume = FrostFume(ground + new Vector2(Range(-.25f, .25f) * kk, .5f + i * .25f), Range(1.2f, 1.6f) * kk, i % 2 == 0 ? .82f : 1, 1,
                    new Vector2(Range(-.4f, .4f), Range(3, 5) * kk), Range(.8f, 1.1f), i < 2 ? 29 : 113);
                plume.drag = 2.6f; plume.startScale = .6f; plume.endScale = 1.7f;
            }
            int billow = Budget(Mathf.RoundToInt(7 * kk));
            for (int i = 0; i < billow; i++)
            {
                int side = i % 2 == 0 ? 1 : -1;
                var fume = FrostFume(ground + new Vector2(side * Range(0, .4f) * r, Range(.3f, 1)), Range(1.1f, 1.6f) * kk, i % 3 == 0 ? 1 : .84f, 1, new Vector2(side * Range(2, 4.5f) * kk, Range(.2f, 1.2f)), Range(.8f, 1.2f), i % 3 == 0 ? 113 : 29);
                fume.drag = 2.2f; fume.startScale = .6f; fume.endScale = 1.8f;
            }
            Needles(mid, 9, 5.5f * kk, FrostAcid, .45f, .48f);
        }

        /// <summary>A painted skull rising out of the toxic cloud, its eyes burning as light, over a soft acid glow.</summary>
        private void FrostSkullWisp(Vector2 at, float size, float delay)
        {
            float life = Range(1.2f, 1.6f), angle = Range(-12, 12), spin = Range(-10, 10);
            Vector2 velocity = new Vector2(Range(-.25f, .25f), Range(.45f, .75f));
            var aura = Spawn("Miasma skull glow", glow, at, size * 1.6f, Alpha(FrostAcid, .32f), life, true, 119);
            var ghost = Spawn("Miasma skull", FrostSpriteSkull, at, size, Color.white, life, false, 120);
            var eyes = Spawn("Miasma skull light", FrostSpriteSkullGlow, at, size, Alpha(FrostAcid, 1), life, true, 121); eyes.flicker = .2f;
            foreach (var clip in new[] { aura, ghost, eyes })
            {
                clip.velocity = velocity; clip.drag = .3f; clip.hold = .4f; clip.fadePower = 1.2f;
                clip.startScale = .6f; clip.endScale = 1.15f; clip.ease = 5; clip.angle = angle; clip.spin = spin;
                FrostDelay(clip, delay);
            }
        }

        /// <summary>A short jagged arc of venom lightning (three segments).</summary>
        private void FrostArc(Vector2 a, Vector2 b, Color color, float width, float life)
        {
            Vector2 delta = b - a; float length = delta.magnitude; if (length < .05f) return;
            Vector2 side = new Vector2(-delta.y, delta.x) / length, previous = a;
            for (int i = 1; i <= 3; i++)
            {
                Vector2 point = i == 3 ? b : Vector2.Lerp(a, b, i / 3f) + side * Range(-.25f, .25f) * length;
                var halo = Line(previous, point, width * 2.6f, Alpha(color, .9f), life, 128, 1.15f); halo.fadePower = 1.3f;
                var inner = Line(previous, point, width, new Color(.95f, 1, .85f, 1), life * .9f, 129, 1.1f); inner.fadePower = 1.2f;
                previous = point;
            }
        }

        // ---------------------------------------------------------------- shared helpers

        /// <summary>Instant echoes (wind-up under 0.2 s): a quick flare in the hand that does not compete with the release.</summary>
        private void FrostQuickCast(CastInfo c, Vector2 hand, Color light, Color hot, bool ice)
        {
            var flare = Anchored(c.anchor, hand, "Quick cast glow", glow, 1.5f, Alpha(light, .5f), .22f, 126); flare.startScale = .4f; flare.endScale = 1.2f; flare.ease = 2; flare.fadePower = 1.4f;
            var heart = Anchored(c.anchor, hand, "Quick cast core", ice ? FrostSpriteGem : core, ice ? .6f : .42f, Alpha(ice ? FrostPale : Color.white, 1), .14f, 132); heart.endScale = .5f; heart.ease = 3;
            Converge(hand, 6, 1.2f, hot, c.windup + .02f, .3f);
            Glint(hand, 1.4f, Alpha(hot, 1), .18f, 10);
        }

        private static float FrostAreaRadius(float castRadius, float min, float max)
        {
            // Casts receive min(1.3, radius * 0.3); undo that to size the wind-up to the real area.
            return castRadius >= 1.29f ? max : Mathf.Clamp(castRadius / .3f, min, max);
        }

        private static void FrostDelay(Clip clip, float delay)
        {
            if (delay <= 0) return;
            clip.age = -delay; if (clip.renderer) clip.renderer.enabled = false;
        }

        /// <summary>Delays every clip spawned since index <paramref name="first"/>, so a built-up burst starts late as one piece.</summary>
        private void FrostDelayFrom(int first, float delay)
        {
            if (delay <= 0) return;
            for (int i = first; i < clips.Count; i++) FrostDelay(clips[i], delay);
        }

        private static void FrostSquash(Clip clip, float factor)
        {
            clip.scale.y *= factor; if (clip.root) clip.root.transform.localScale = clip.scale;
        }

        /// <summary>A zone layer that lives as long as the zone: it spreads out in about 0.3 s and fades over the last 0.9 s.</summary>
        private static void FrostZoneLayer(Clip clip, float life)
        {
            clip.life = life; clip.loop = false;
            clip.startScale = .25f; clip.endScale = 1;
            clip.ease = Mathf.Log(.06f) / Mathf.Log(Mathf.Clamp(1 - .3f / life, .01f, .99f));
            clip.fadeIn = .12f; clip.hold = Mathf.Clamp01(1 - .9f / life); clip.fadePower = 1.3f;
        }

        private bool FrostZoneEnding(Clip owner)
        {
            float start = owner.life - 1.1f;
            return owner.age > start && Chance(Mathf.InverseLerp(start, owner.life - .2f, owner.age));
        }

        private static Vector2 FrostZoneCenter(Clip owner)
        {
            return owner.external ? (Vector2)owner.external.position : (Vector2)owner.root.transform.position;
        }

        // ---------------------------------------------------------------- textures

        private static void BuildFrostTextures()
        {
            // A level-three ice nova freezes its foes: build the frozen shell now, at load, so the first freeze does not hitch.
            StatusTextures(StatusKind.Freeze);
            float unused;
            // A cluster of three painted ice crystals (bottom pivot), and the light of their spines, streaks and rims.
            FrostSpriteCrystal = Build("Frost crystal", 96, 192, (u, v) => FrostCrystalSample(u, v, out unused), new Vector2(.5f, 0));
            FrostSpriteCrystalGlow = Build("Frost crystal light", 96, 192, (u, v) => { float light; var c = FrostCrystalSample(u, v, out light); return White(light * c.a); }, new Vector2(.5f, 0));
            // A fan of nine great painted crystals radiating up from one point (bottom pivot): the ice nova's centrepiece.
            FrostSpriteFan = Build("Frost crystal fan", 256, 160, (u, v) => FrostFanSample(u, v, out unused), new Vector2(.5f, 0));
            FrostSpriteFanGlow = Build("Frost crystal fan light", 256, 160, (u, v) => { float light; var c = FrostFanSample(u, v, out light); return White(light * c.a); }, new Vector2(.5f, 0));

            // Seven painted ice fragments around an empty centre, pointing outward: scaled up, it reads as a crystal bursting apart.
            FrostSpriteShatter = Build("Frost shatter", 128, 128, (u, v) =>
            {
                Color c = Color.clear; float light = 0;
                for (int i = 0; i < 7; i++)
                {
                    uint h = Hash(i * 29 + 401);
                    float angle = (i * 51.4f + ((h & 255) / 255f - .5f) * 30) * Mathf.Deg2Rad, s = Mathf.Sin(angle), co = Mathf.Cos(angle);
                    float start = .2f + (h >> 8 & 255) / 255f * .2f, length = .3f + (h >> 16 & 255) / 255f * .24f, half = .08f + (h >> 24 & 255) / 255f * .05f;
                    c = FrostFacet(c, -u * s + v * co, u * co + v * s - start, 0, 0, length, half, ref light);
                }
                return c;
            });

            // A splash crown of venom (bottom pivot): a low mound and seven curling tendrils tipped with droplets, painted smoothly
            // (deep green at the foot and on the shadowed sides, bright acid up the lit sides and tips, a wet highlight streak).
            FrostSpriteCrown = Build("Venom crown", 256, 192, (u, v) =>
            {
                float px = u * 1.333f, py = v + 1;
                // Each part reports a normalised distance (0 on its spine, 1 on its edge); the deepest part shades the pixel, so
                // overlaps merge without inner rim lines.
                float nd = 9, side = 0;
                FrostCrownPart(ref nd, ref side, Mathf.Sqrt(Sq(px / 1.05f) + Sq(py / .3f)), px / 1.05f);
                for (int i = 0; i < 7; i++)
                {
                    float x0 = (i - 3) * .3f, height = 1.62f - Mathf.Abs(i - 3) * .27f, lean = (i - 3) * .18f;
                    float t = Mathf.Clamp01(py / height), cx = x0 + lean * Mathf.Pow(t, 1.5f), width = Mathf.Lerp(.15f, .045f, t);
                    if (py <= height) FrostCrownPart(ref nd, ref side, Mathf.Abs(px - cx) / width + Smooth(height - .06f, height, py), (px - cx) / width);
                    float dx = px - (x0 + lean * 1.25f), dy = py - (height + .14f), radius = .085f + (3 - Mathf.Abs(i - 3)) * .008f;
                    FrostCrownPart(ref nd, ref side, Mathf.Sqrt(dx * dx + dy * dy) / radius, dx / radius);
                }
                float cover = 1 - Smooth(.93f, 1, nd);
                if (cover <= .002f) return Color.clear;
                float rise = Smooth(.05f, 1.4f, py), litSide = Mathf.Clamp01(.5f - side * .45f);
                float level = .2f + .32f * rise + .3f * litSide * (1 - nd * .6f) + Gauss(side + .45f, .16f) * (1 - Smooth(.5f, .8f, nd)) * .35f;
                Color body = Color.Lerp(FrostAcidTone(Mathf.Clamp01(level)), new Color(.12f, .38f, .07f), Smooth(.72f, .97f, nd) * .55f);
                body.a = cover;
                return body;
            }, new Vector2(.5f, 0));

            // A painted ice chip (the upper face lit, the lower in shadow, blended across its spine; a frosty upper rim and a bright
            // point); points along +x. Tinted white it is ice, tinted green it is vial glass.
            FrostSpriteShard = Build("Frost shard", 96, 48, (u, v) =>
            {
                float along = u > 0 ? u / .98f : -u / .7f, dist = along + Mathf.Abs(v) / .9f;
                float inside = 1 - Smooth(.9f, .97f, dist);
                if (inside <= 0) return Color.clear;
                float lower = Smooth(-.1f, .14f, -v);
                float level = Mathf.Lerp(.68f + .14f * u, .32f + .1f * u, lower) + Gauss(dist - .8f, .07f) * (1 - lower) * .35f + Gauss(u - .78f, .1f) * .25f
                    + Gauss(Mathf.Repeat(u * 1.6f - v * .8f, .5f) - .25f, .03f) * .15f;
                Color c = FrostIceTone(Mathf.Clamp01(level)); c.a = inside; return c;
            });

            // A glossy painted venom drop: lit from the upper left round a sphere, light bouncing back on its lower right rim, a
            // sharp highlight, its edge a deep saturated green.
            FrostSpriteDrop = Build("Frost droplet", 64, 64, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), inside = 1 - Smooth(.76f, .84f, r);
                if (inside <= 0) return Color.clear;
                float x = u / .8f, y = v / .8f, z = Mathf.Sqrt(Mathf.Max(0, 1 - x * x - y * y));
                float diffuse = Mathf.Clamp01(-x * .5f + y * .55f + z * .65f);
                float bounce = Gauss(r - .66f, .07f) * Mathf.Clamp01(x * .7f - y * .7f) * .35f;
                Color c = FrostAcidTone(Mathf.Clamp01(.16f + .64f * diffuse + bounce));
                c = Color.Lerp(c, new Color(.95f, 1, .86f), Gauss(Mathf.Sqrt(Sq(u + .25f) + Sq(v - .28f)), .1f));
                c.a = inside; return c;
            });

            // A glossy bubble of light: a faint body, a bright rim (strongest on the lower right, where light refracts through)
            // and a sharp highlight with a small second one.
            FrostSpriteBubble = Build("Venom bubble", 64, 64, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .96f) return Color.clear;
                float cover = 1 - Smooth(.8f, .87f, r);
                float rim = Gauss(r - .76f, .07f) * (.6f + .4f * Mathf.Clamp01(u * .5f - v * .5f + .5f));
                float spec = Gauss(Mathf.Sqrt(Sq(u + .3f) + Sq(v - .32f)), .11f) * 1.1f, spec2 = Gauss(Mathf.Sqrt(Sq(u - .3f) + Sq(v + .34f)), .07f) * .4f;
                return White(Mathf.Clamp01((.1f + .12f * r / .8f + rim * .75f) * cover + spec + spec2));
            });

            // A frost seal of crisp strokes: branched snowflake arms, two hexagons, a double rim with fine ticks.
            FrostSpriteSigil = Build("Frost seal", 512, 512, (u, v) => White(FrostSigilLevel(u, v)));

            // A crisp six-armed snowflake (white core on each arm), painted light.
            FrostSpriteFlake = Build("Frost flake", 64, 64, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .97f) return Color.clear;
                float sector = Mathf.Repeat(Mathf.Atan2(v, u) + Mathf.PI / 6, Mathf.PI / 3) - Mathf.PI / 6;
                float x = r * Mathf.Cos(sector), y = Mathf.Abs(r * Mathf.Sin(sector)), level = 0;
                if (x < .86f) level = FrostStroke(y, .075f * (1 - x * .4f), .9f);
                for (int b = 1; b <= 2; b++) { float t = x - b * .28f; if (t > 0 && t < .22f) level = Mathf.Max(level, FrostStroke(Mathf.Abs(y - t * 1.05f) * .69f, .058f, .8f)); }
                level = Mathf.Max(level, FrostStroke(r, .17f, 1));
                return White(level);
            });

            // A luminous ice gem (an elongated hexagon) as painted light: soft facets lit from the upper left blended across a
            // faint ridge, an inner glow, refraction streaks and a bright rim.
            FrostSpriteGem = Build("Frost gem", 96, 128, (u, v) =>
            {
                float ax = Mathf.Abs(u), ay = Mathf.Abs(v);
                float edgeX = ay < .3f ? .62f : .62f * (.95f - ay) / .65f, d = edgeX - ax;
                if (d < -.02f) return Color.clear;
                float cover = Smooth(-.02f, .004f, d);
                float lit = Mathf.Clamp01(.5f - u * .45f + v * .3f), inner = Gauss(Mathf.Sqrt(u * u + Sq(v * .8f)), .35f) * .32f;
                float ridge = Gauss(u, .03f) * .12f * (1 - ay), streak = Gauss(Mathf.Repeat(u * 1.8f + v * 1.2f, .5f) - .25f, .025f) * .2f;
                float rim = Gauss(d - .02f, .05f) * (.55f + .4f * lit);
                return White(Mathf.Clamp01(.18f + lit * .35f + inner + ridge + streak + rim) * cover);
            });

            // Still toxic cloud masses for the lasting cloud: lumpy unions of round puffs painted with soft light from the upper
            // left (violet shadow, green body, acid highlight) and a brushed grain; alpha rises toward each puff's centre, so the
            // ink thins it puff by puff as it fades.
            FrostSpriteMiasma = new Sprite[4];
            for (int i = 0; i < FrostSpriteMiasma.Length; i++)
            {
                var puffs = FrostPuffs(i * 17 + 333, .44f, .18f, .18f, .22f, .16f);
                int seed = i;
                FrostSpriteMiasma[i] = Build("Toxic cloud " + i, 160, 160, (u, v) =>
                {
                    float field, shade = FrostCloudShade(puffs, u, v, out field);
                    if (field <= 0) return Color.clear;
                    Color c = FrostToxicTone(FrostCloudLight(shade, Fbm(u * 5 + seed * 3, v * 5 + 7, 3)));
                    c.a = Mathf.Clamp01(field * 1.5f) * Window(Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)));
                    return c;
                });
            }
            FrostSpriteFumes = new[] { FrostBillow("Toxic billow 0", 401, true), FrostBillow("Toxic billow 1", 419, true) };
            FrostSpriteMists = new[] { FrostBillow("Frost mist 0", 433, false), FrostBillow("Frost mist 1", 457, false) };

            // Frozen ground, painted: a mid blue body brightening into paler frost patches and a frosted white rim, streaked, a
            // sheen across it and cracks of deeper blue. The plain ice is a little thinner than the rim and cracks, so it erodes
            // first and leaves them as it thaws.
            FrostSpriteIce = Build("Frost ice sheet", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float edge = .8f + (Fbm(u * 2.2f + 31, v * 2.2f + 17, 3) - .5f) * .35f;
                float inside = 1 - Smooth(edge - .03f, edge, r);
                if (inside <= 0) return Color.clear;
                float rim = Smooth(edge - .24f, edge - .05f, r), crack = Gauss(FrostCellEdge(u * 4.2f + 10, v * 4.2f + 10), .02f) * (1 - rim);
                float frost = Smooth(.42f, .66f, Fbm(u * 6 + 3, v * 6 + 8, 3)), grain = Fbm(u * 15 + 1, v * 4 + 5, 2) - .5f;
                float sheen = Gauss(u * .6f - v * .8f + .25f, .18f) * .15f;
                float level = Mathf.Clamp01(.4f + .24f * frost + .14f * grain + sheen + .5f * rim);
                Color c = Color.Lerp(FrostIceTone(level), new Color(.16f, .42f, .86f), crack * .75f);
                c.a = inside * Mathf.Lerp(.68f + .1f * frost, 1, Mathf.Max(rim, crack));
                return c;
            });

            // A glossy acid pool, painted: acid green brightening in glossy patches with a sheen, a violet taint blended in, soft
            // bubble rings and satellite splashes, its edge deepening to a saturated green.
            FrostSpritePool = Build("Venom pool", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float edge = .78f + (Fbm(u * 2.4f + 5, v * 2.4f + 41, 3) - .5f) * .4f;
                float inside = 1 - Smooth(edge - .02f, edge, r), splash = 0;
                for (int k = 0; k < 8; k++)
                {
                    uint h = Hash(k * 13 + 211);
                    float angle = k * .785f + ((h & 255) / 255f - .5f) * .6f, dist = .8f + (h >> 8 & 255) / 255f * .12f, size = .03f + (h >> 16 & 255) / 255f * .035f;
                    float d = Mathf.Sqrt(Sq(u - Mathf.Cos(angle) * dist) + Sq(v - Mathf.Sin(angle) * dist));
                    splash = Mathf.Max(splash, 1 - Smooth(size * .85f, size, d));
                }
                float cover = Mathf.Max(inside, splash);
                if (cover <= 0) return Color.clear;
                float rimDepth = Mathf.Max(Smooth(edge - .14f, edge - .02f, r), inside < splash ? 1 : 0);
                float gloss = Smooth(.42f, .64f, Fbm(u * 5 + 2, v * 5 + 9, 3)), sheen = Gauss(v * .9f - u * .3f - .2f, .12f) * (1 - rimDepth) * .3f;
                Color body = FrostAcidTone(Mathf.Clamp01(.32f + .3f * gloss + sheen));
                body = Color.Lerp(body, new Color(.42f, .18f, .5f), Smooth(.56f, .7f, Fbm(u * 2 + 21, v * 2 + 7, 2)) * .65f * (1 - rimDepth));
                float bubbles = 0;
                if (r < edge - .1f)
                    for (int k = 0; k < 10; k++)
                    {
                        uint h = Hash(k * 7 + 300);
                        float bx = ((h & 255) / 255f - .5f) * 1.1f, by = ((h >> 8 & 255) / 255f - .5f) * 1.1f, br = .035f + (h >> 16 & 255) / 255f * .06f;
                        bubbles = Mathf.Max(bubbles, Gauss(Mathf.Sqrt(Sq(u - bx) + Sq(v - by)) - br, .014f));
                    }
                body = Color.Lerp(body, new Color(.9f, 1, .7f), bubbles * .85f);
                body = Color.Lerp(body, new Color(.12f, .38f, .06f), rimDepth * .7f);
                body.a = cover * Mathf.Lerp(.82f, 1, Mathf.Max(rimDepth, bubbles));
                return body;
            });

            // A painted toxic skull with a short tapering smoke tail, and the light of its burning eyes.
            FrostSpriteSkull = Build("Venom skull", 128, 128, (u, v) => FrostSkullSample(u, v, out unused));
            FrostSpriteSkullGlow = Build("Venom skull eyes", 128, 128, (u, v) => { float light; var c = FrostSkullSample(u, v, out light); return White(light * c.a); });
        }

        /// <summary>The ice ramp for a light level: a deep saturated blue, a clear mid blue, pale frost, and white.</summary>
        private static Color FrostIceTone(float level)
        {
            Color c = Color.Lerp(new Color(.1f, .27f, .7f), new Color(.3f, .62f, .97f), Smooth(.12f, .45f, level));
            c = Color.Lerp(c, new Color(.74f, .93f, 1), Smooth(.45f, .78f, level));
            return Color.Lerp(c, new Color(.97f, 1, 1), Smooth(.8f, .98f, level));
        }

        /// <summary>The acid ramp for a light level: a deep saturated green, acid green, a bright acid, and a pale highlight.</summary>
        private static Color FrostAcidTone(float level)
        {
            Color c = Color.Lerp(new Color(.1f, .34f, .06f), new Color(.32f, .66f, .1f), Smooth(.1f, .45f, level));
            c = Color.Lerp(c, new Color(.6f, .94f, .26f), Smooth(.45f, .78f, level));
            return Color.Lerp(c, new Color(.9f, 1, .68f), Smooth(.8f, .98f, level));
        }

        /// <summary>Toxic smoke for a light level: plague violet in shadow, green body, a bright acid highlight.</summary>
        private static Color FrostToxicTone(float light)
        {
            Color c = Color.Lerp(new Color(.3f, .11f, .42f), new Color(.46f, .2f, .5f), Smooth(0, .25f, light));
            c = Color.Lerp(c, new Color(.22f, .48f, .14f), Smooth(.25f, .5f, light));
            c = Color.Lerp(c, new Color(.48f, .84f, .2f), Smooth(.5f, .78f, light));
            return Color.Lerp(c, new Color(.84f, 1, .52f), Smooth(.84f, 1, light));
        }

        /// <summary>Frost mist for a light level: soft cold blue in shadow, pale frost, white where it is lit.</summary>
        private static Color FrostMistTone(float light)
        {
            Color c = Color.Lerp(new Color(.5f, .7f, .96f), new Color(.76f, .91f, 1), Smooth(.15f, .6f, light));
            return Color.Lerp(c, new Color(.97f, 1, 1), Smooth(.65f, 1, light));
        }

        /// <summary>Painted light on a cloud of round puffs (x, y, radius) lit from the upper left: each puff is shaded as a soft
        /// sphere and the shading of overlapping puffs blends by how deep inside each one the point lies, so every lobe keeps its
        /// own lit face and shadowed underside with no hard crease. <paramref name="cover"/> is the lumpy silhouette (1 at a
        /// puff's centre, 0 a quarter beyond its radius).</summary>
        private static float FrostCloudShade(Vector3[] puffs, float u, float v, out float cover)
        {
            float weight = 1e-5f, light = 0; cover = 0;
            foreach (var p in puffs)
            {
                float reach = p.z * 1.25f, dx = (u - p.x) / reach, dy = (v - p.y) / reach, x = dx * dx + dy * dy;
                if (x >= 1) continue;
                cover = Mathf.Max(cover, 1 - x);
                float w = Mathf.Pow(1 - x, 6);
                light += w * Mathf.Clamp01(-dx * .55f + dy * .55f + Mathf.Sqrt(1 - x) * .63f); weight += w;
            }
            return light / weight;
        }

        /// <summary>The cloud's painted light level: the puffs' shading over a little ambient light, with a brushed grain.</summary>
        private static float FrostCloudLight(float shade, float grain) { return Mathf.Clamp01(.16f + .8f * shade + (grain - .5f) * .3f); }

        /// <summary>Six round puffs (x, y, radius) heaped round a main one, from a seed.</summary>
        private static Vector3[] FrostPuffs(int seed, float main, float near, float spread, float small, float smallSpread)
        {
            var puffs = new Vector3[6]; uint h0 = Hash(seed);
            for (int k = 0; k < puffs.Length; k++)
            {
                uint h = Hash((int)h0 + k * 7919);
                float angle = k * 1.047f + (h & 255) / 255f * .8f, distance = k == 0 ? 0 : near + (h >> 8 & 255) / 255f * spread;
                puffs[k] = new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance * .82f, k == 0 ? main : small + (h >> 16 & 255) / 255f * smallSpread);
            }
            return puffs;
        }

        /// <summary>A billow flipbook painted in the toxic or the frost palette: the puffs swell and drift apart while holes open
        /// and the edges tear away, lit softly from the upper left throughout.</summary>
        private static Sprite[] FrostBillow(string name, int seed, bool toxic)
        {
            var puffs = FrostPuffs(seed, .4f, .14f, .16f, .2f, .14f);
            return Sheet(name, 6, 128, 128, (u, v, f) =>
            {
                float p = f / 5f, grow = 1 + .3f * p, x = u / grow, y = v / grow - p * .06f;
                float cloud, shade = FrostCloudShade(puffs, x, y, out cloud);
                float field = cloud - Smooth(.15f, 1, p) * .8f * Fbm(u * 3.4f + f * .3f + seed * .37f, v * 3.4f - f * .2f, 3);
                if (field <= 0) return Color.clear;
                float light = FrostCloudLight(shade, Fbm(u * 5 + seed * .1f, v * 5 - seed * .1f, 2));
                Color c = toxic ? FrostToxicTone(light) : FrostMistTone(light);
                c.a = Mathf.Clamp01(field * 1.6f) * Window(Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)));
                return c;
            });
        }

        private static Color FrostCrystalSample(float u, float v, out float light)
        {
            float h = (v + 1) * .5f; light = 0;
            Color c = FrostFacet(Color.clear, u, h, -.4f, -.82f, .56f, .3f, ref light);
            c = FrostFacet(c, u, h, .42f, .84f, .46f, .26f, ref light);
            return FrostFacet(c, u, h, 0, .05f, .98f, .5f, ref light);
        }

        private static Color FrostFanSample(float u, float v, out float light)
        {
            float px = u * 1.6f, py = v + 1; light = 0;
            Color c = Color.clear;
            for (int i = 0; i < FrostFanBlades.Length; i += 3)
            {
                float theta = FrostFanBlades[i] * Mathf.Deg2Rad, s = Mathf.Sin(theta), co = Mathf.Cos(theta), bx = s * .12f;
                float along = (px - bx) * s + py * co, across = (px - bx) * co - py * s;
                c = FrostFacet(c, across, along, 0, 0, FrostFanBlades[i + 1], FrostFanBlades[i + 2], ref light);
            }
            return c;
        }

        /// <summary>A painted skull: bone lit softly from the upper left round the dome, a deep violet-green in the hollows and
        /// glowing acid pupils, with a short tapering smoke tail. <paramref name="light"/> is the eyes' glow.</summary>
        private static Color FrostSkullSample(float u, float v, out float light)
        {
            light = 0;
            float cr = Mathf.Sqrt(Sq(u / .56f) + Sq((v - .2f) / .5f));
            float jx = Mathf.Abs(u) - .28f, jy = Mathf.Abs(v + .28f) - .18f;
            float jd = Mathf.Sqrt(Sq(Mathf.Max(jx, 0)) + Sq(Mathf.Max(jy, 0))) + Mathf.Min(Mathf.Max(jx, jy), 0) - .08f;
            float cranium = 1 - Smooth(.94f, 1, cr), jaw = 1 - Smooth(-.01f, .012f, jd), shape = Mathf.Max(cranium, jaw);
            float tailT = Mathf.Clamp01((-.42f - v) / .5f), tailX = u - Mathf.Sin(v * 9 + 1) * .12f * tailT, tailW = .24f * (1 - tailT);
            float tail = v < -.36f ? (1 - Smooth(tailW - .04f, tailW, Mathf.Abs(tailX))) * (1 - tailT * .5f) : 0;
            float cover = Mathf.Max(shape, tail);
            if (cover <= .002f) return Color.clear;
            float level;
            if (shape > .5f)
            {
                float x = u / .56f, y = (v - .2f) / .5f, z = Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Min(1, x * x + y * y)));
                float diffuse = cranium > .5f ? Mathf.Clamp01(-x * .45f + y * .5f + z * .7f) : Mathf.Clamp01(.45f - u * .45f + (v + .28f) * .5f) * .8f;
                level = .25f + .65f * diffuse;
            }
            else level = .3f + .25f * (1 - tailT);
            Color c = Color.Lerp(new Color(.22f, .42f, .14f), new Color(.66f, .9f, .44f), Smooth(.2f, .7f, level));
            c = Color.Lerp(c, new Color(.92f, 1, .78f), Smooth(.75f, .95f, level));
            if (shape > .5f)
            {
                float eye = Mathf.Min(Mathf.Sqrt(Sq((u + .23f) / .17f) + Sq((v - .02f) / .15f)), Mathf.Sqrt(Sq((u - .23f) / .17f) + Sq((v - .02f) / .15f)));
                float nose = Mathf.Sqrt(Sq(u / .07f) + Sq((v + .19f) / .09f));
                float teeth = v < -.33f && v > -.46f && Mathf.Abs(u) < .24f ? Gauss(Mathf.Repeat(u + .06f, .12f) - .06f, .012f) : 0;
                float mouth = Mathf.Abs(u) < .27f ? Gauss(v + .33f, .016f) : 0;
                float hollow = Mathf.Max(Mathf.Max(1 - Smooth(.8f, 1.05f, eye), 1 - Smooth(.75f, 1.05f, nose)), Mathf.Max(teeth, mouth));
                c = Color.Lerp(c, new Color(.2f, .1f, .26f), hollow);
                float pupil = Mathf.Min(Mathf.Sqrt(Sq(u + .23f) + Sq(v)), Mathf.Sqrt(Sq(u - .23f) + Sq(v))), burning = Gauss(pupil, .06f);
                c = Color.Lerp(c, new Color(.88f, 1, .5f), burning);
                light = Mathf.Clamp01(burning * 1.1f + Gauss(pupil, .14f) * .35f * hollow);
            }
            c.a = cover;
            return c;
        }

        /// <summary>One crisp painted stroke: a hard edge, a thin deep rim, then rising to <paramref name="peak"/> on its spine.</summary>
        private static float FrostStroke(float distance, float half, float peak)
        {
            if (distance >= half + .003f) return 0;
            float s = Mathf.Clamp01(1 - distance / Mathf.Max(.0001f, half));
            return (.13f + (peak - .13f) * Mathf.Pow(s, .55f)) * (1 - Smooth(half - .005f, half + .003f, distance));
        }

        /// <summary>The frost seal's light level: branched snowflake arms, two hexagons, a double rim with fine ticks.</summary>
        private static float FrostSigilLevel(float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v); if (r > .99f) return 0;
            float theta = Mathf.Atan2(v, u);
            float sector = Mathf.Repeat(theta + Mathf.PI / 6, Mathf.PI / 3) - Mathf.PI / 6;
            float x = r * Mathf.Cos(sector), y = Mathf.Abs(r * Mathf.Sin(sector)), level = 0;
            if (x > .06f && x < .84f) level = FrostStroke(y, .012f, .85f);
            float[] starts = { .26f, .46f, .63f }, lengths = { .17f, .14f, .09f };
            for (int b = 0; b < 3; b++)
            {
                float t = x - starts[b];
                if (t > 0 && t < lengths[b]) level = Mathf.Max(level, FrostStroke(Mathf.Abs(y - t * 1.05f) * .69f, .011f, .8f));
            }
            float diamond = Mathf.Abs(x - .87f) / .05f + y / .03f;
            if (diamond < 1) level = Mathf.Max(level, .35f + .6f * (1 - diamond));
            level = Mathf.Max(level, FrostStroke(Mathf.Abs(x - .57f), .008f, .75f));
            float sector2 = Mathf.Repeat(theta, Mathf.PI / 3) - Mathf.PI / 6;
            level = Mathf.Max(level, FrostStroke(Mathf.Abs(r * Mathf.Cos(sector2) - .4f), .007f, .7f));
            level = Mathf.Max(level, FrostStroke(Mathf.Abs(r - .955f), .012f, .95f));
            level = Mathf.Max(level, FrostStroke(Mathf.Abs(r - .905f), .006f, .7f));
            level = Mathf.Max(level, FrostStroke(Mathf.Abs(r - .2f), .008f, .8f));
            if (r > .912f && r < .95f) level = Mathf.Max(level, FrostStroke(Mathf.Abs(Mathf.Repeat(theta * Mathf.Rad2Deg + 2.5f, 5) - 2.5f) * Mathf.Deg2Rad * r, .004f, .6f));
            level = Mathf.Max(level, FrostStroke(Mathf.Sqrt(Sq(x - .65f) + Sq(y - .375f)), .025f, .9f));
            return level;
        }

        /// <summary>One painted ice blade: smooth light from the upper left across a soft ridge (a pale lit face brightening to
        /// its rim and tip, a deeper blue shadow face), cold light glowing up its spine from the base, refraction streaks, a
        /// frosty rim on the lit edge, a thin reflected rim on the shadow edge and a frost grain; its edge is a deep saturated
        /// blue, never dark. <paramref name="light"/> takes the blade's inner light (spine glow, streaks, rims) wherever it is
        /// the front-most blade, for the light copy drawn over the paint.</summary>
        private static Color FrostFacet(Color under, float u, float h, float baseX, float tipX, float tipH, float halfWidth, ref float light)
        {
            float t = h / tipH; if (t <= 0 || t >= 1) return under;
            float half = halfWidth * Mathf.Pow(1 - t, .8f) + .002f, d = (u - Mathf.Lerp(baseX, tipX, t)) / half;
            float inside = (1 - Smooth(.9f, 1, Mathf.Abs(d))) * Smooth(0, .03f, h);
            if (inside <= 0) return under;
            float ridge = -.2f + .15f * t, shadow = Smooth(ridge - .14f, ridge + .1f, d);
            float n = Fbm(u * 6.5f + tipH * 7, h * 3.2f + tipX * 5, 3);
            float lit = .64f + .2f * t + .08f * Smooth(ridge, -.9f, d), dark = .3f + .14f * t - .12f * Smooth(.35f, .95f, d);
            float spine = Gauss(d - ridge * .6f, .42f) * Mathf.Pow(1 - t, .7f) * .32f;
            float streaks = Gauss(Mathf.Repeat(t * 2.6f - d * .55f + tipH * 1.7f, 1) - .5f, .05f) * (1 - Smooth(.6f, .9f, Mathf.Abs(d))) * .28f;
            float rimLit = Gauss(d + .84f, .1f) * (1 - shadow) * .5f, rimBack = Gauss(d - .82f, .07f) * .2f, tip = Gauss(1 - t, .08f) * .35f;
            float level = Mathf.Clamp01(Mathf.Lerp(lit, dark, shadow) + spine + streaks + rimLit + rimBack + tip + (n - .5f) * .14f);
            Color body = Color.Lerp(FrostIceTone(level), new Color(.12f, .32f, .8f), Smooth(.8f, .98f, Mathf.Abs(d)) * shadow * .55f);
            body.a = inside;
            light = Mathf.Lerp(light, Mathf.Clamp01(spine * 1.7f + streaks * 1.6f + rimLit * 1.3f + tip), inside);
            return Over(under, body);
        }

        private static void FrostCrownPart(ref float nd, ref float side, float distance, float across)
        {
            if (distance < nd) { nd = distance; side = across; }
        }

        /// <summary>Distance between the nearest and second-nearest jittered cell points: small along cell borders.</summary>
        private static float FrostCellEdge(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float first = 9, second = 9;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    uint h = Hash((ix + i) * 73856093 ^ (iy + j) * 19349663);
                    float px = ix + i + (h & 1023) / 1023f, py = iy + j + (h >> 10 & 1023) / 1023f;
                    float d = Mathf.Sqrt(Sq(x - px) + Sq(y - py));
                    if (d < first) { second = first; first = d; } else if (d < second) second = d;
                }
            return second - first;
        }
    }
}
