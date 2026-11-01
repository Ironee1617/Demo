using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Frost and venom family.
    // IceNova: a frost seal and spiralling snow charge the caster, then a white-blue burst freezes the ground across the
    // whole radius, a double row of ice crystals erupts outward from the caster and shatters, and cold mist rolls away.
    // PoisonCloud: a toxic vortex gathers in the hand and bursts like a vial; the cloud lands with an acid splash and churns
    // as layered dark/glowing miasma over a bubbling acid pool, with toxic sparks, green arcs and drifting skull wisps.
    public sealed partial class TowerVfx
    {
        private static Sprite FrostSpriteCrystal, FrostSpriteShard, FrostSpriteDrop, FrostSpriteSigil, FrostSpriteIce, FrostSpritePool, FrostSpriteSkull;
        private static Sprite FrostSpriteFan, FrostSpriteShatter, FrostSpriteCrown;
        // Crystal fan blades, back to front: angle from vertical (degrees), length and base half-width (texture units, height 2).
        private static readonly float[] FrostFanBlades = { -66, 1.1f, .18f, 64, 1.15f, .18f, -47, 1.45f, .23f, 45, 1.5f, .23f, -27, 1.72f, .26f, 28, 1.75f, .26f, -11, 1.88f, .28f, 13, 1.84f, .27f, 2, 1.96f, .32f };

        private static readonly Color FrostPale = new Color(.84f, .97f, 1), FrostBlue = new Color(.55f, .86f, 1), FrostInk = new Color(.03f, .2f, .52f),
            FrostAcid = new Color(.6f, 1, .28f), FrostAcidHot = new Color(.9f, 1, .66f), FrostMurk = new Color(.08f, .22f, .03f),
            FrostPlague = new Color(.26f, .07f, .32f), FrostViolet = new Color(.72f, .38f, 1);

        // ---------------------------------------------------------------- ice nova

        private void CastIceNova(CastInfo c)
        {
            float w = c.windup, R = FrostAreaRadius(c.radius, 2.2f, 4.5f);
            Vector2 pos = c.pos, ground = new Vector2(pos.x, pos.y - 1), hand = pos + new Vector2(c.facing * .32f, .2f);
            Color light = Color.Lerp(IceLight, c.tint, .2f);
            if (w < .2f) { FrostQuickCast(c, hand, light, FrostPale, true); return; }
            float life = w + .35f, hold = w / life;
            // Higher levels swell the charge and the circle and grow a fuller crown of crystals; level three turns a second,
            // white-gold circle the other way behind the first and raises a column of cold light.
            int g = Grade; float gs = 1 + (g - 1) * .12f;
            if (g >= 3)
            {
                var crownInk = Spawn("Frost crown circle ink", FrostSpriteSigil, pos + Vector2.up * .1f, 4.6f, new Color(.03f, .16f, .48f, .5f), w + .2f, false, 26);
                var crownCircle = Spawn("Frost crown circle", FrostSpriteSigil, pos + Vector2.up * .1f, 4.5f, new Color(1, .95f, .8f, .75f), w + .2f, true, 27);
                foreach (var s in new[] { crownInk, crownCircle }) { s.spin = -45 * c.facing; s.fadeIn = .1f; s.startScale = .3f; s.endScale = 1.1f; s.ease = 2; s.hold = w / (w + .2f); s.fadePower = 1.6f; }
                var column = Pillar(ground, 1.8f, 6.5f, Alpha(light, .55f), w + .25f, 27); column.fadeIn = w * .5f; column.hold = .75f; column.fadePower = 1.8f;
                var columnInk = Spawn("Frost column ink", pillar, ground, 2.5f, new Color(.03f, .16f, .48f, .32f), w + .25f, false, 26);
                columnInk.scale = new Vector3(2.5f / pillar.bounds.size.x, 6.5f / pillar.bounds.size.y, 1); columnInk.root.transform.localScale = columnInk.scale;
                columnInk.axisStart = new Vector2(.35f, .12f); columnInk.axisEnd = Vector2.one; columnInk.ease = 4; columnInk.fadeIn = w * .5f; columnInk.hold = .75f; columnInk.fadePower = 1.8f;
            }

            // Deep blue shade behind the caster so the cold light reads on pale skies.
            var shade = Spawn("Frost cast shade", glow, pos + Vector2.up * .15f, 3.8f, new Color(.01f, .06f, .2f, .45f), life, false, 111);
            shade.fadeIn = w * .4f; shade.hold = hold; shade.fadePower = 1.6f; shade.startScale = .6f; shade.endScale = 1.1f;

            // A frost seal spreads across the floor toward the nova radius.
            float sealWidth = R * 1.75f;
            var sealInk = Spawn("Frost seal ink", FrostSpriteSigil, ground + Vector2.up * .03f, sealWidth, new Color(.03f, .18f, .5f, .62f), life, false, 25);
            var seal = Spawn("Frost seal", FrostSpriteSigil, ground + Vector2.up * .03f, sealWidth, Alpha(FrostPale, .95f), life, true, 27);
            foreach (var s in new[] { sealInk, seal }) { FrostSquash(s, .24f); s.fadeIn = .06f; s.startScale = .2f; s.endScale = 1; s.ease = 3; s.hold = hold; s.fadePower = 1.6f; }
            var floor = GroundGlow(ground, sealWidth * 1.2f, Alpha(light, .65f), life); floor.hold = hold; floor.fadeIn = .06f; floor.startScale = .3f; floor.endScale = 1; floor.ease = 3;
            Emitter(floor, 28, 0, (owner, p) =>
            {
                float t = Mathf.Clamp01(owner.age / Mathf.Max(.01f, owner.life * .6f));
                Vector2 at = new Vector2(ground.x + Range(-1f, 1f) * R * (.25f + .6f * t), ground.y + .05f);
                if (Chance(.5f))
                {
                    var breath = Puff(at, Range(.35f, .65f), Chance(.4f) ? new Color(.3f, .58f, .92f, .45f) : new Color(.68f, .88f, 1, .5f), new Vector2(Range(-.2f, .2f), Range(.9f, 1.8f)), Range(.4f, .6f), 114);
                    breath.drag = 1.5f; breath.endScale = 2.1f;
                }
                else
                {
                    var mote = Spawn("Rising frost", glow, at + Vector2.up * Range(0, .4f), Range(.08f, .17f), Alpha(FrostPale, .95f), Range(.3f, .55f), true, 131);
                    mote.velocity = new Vector2(Range(-.3f, .3f), Range(1.4f, 2.8f)); mote.flicker = .5f; mote.endScale = .2f; mote.ease = 1;
                }
            });

            // An upright frost circle turns behind the caster (drawn under the actors) and flares at the release.
            var circleInk = Spawn("Frost circle ink", FrostSpriteSigil, pos + Vector2.up * .1f, 3.4f * gs, new Color(.03f, .16f, .48f, .62f), w + .18f, false, 28);
            var circle = Spawn("Frost circle", FrostSpriteSigil, pos + Vector2.up * .1f, 3.4f * gs, Alpha(FrostPale, .9f), w + .18f, true, 29);
            foreach (var s in new[] { circleInk, circle }) { s.spin = 75 * c.facing; s.fadeIn = .07f; s.startScale = .35f; s.endScale = 1.2f; s.ease = 2; s.hold = w / (w + .18f); s.fadePower = 1.6f; }

            // The charge: an ice orb grows in the hand inside a glow that swells over the whole body.
            var aura = Anchored(c.anchor, pos, "Frost aura", glow, 3f * gs, Alpha(light, .6f), w + .08f, 124); aura.fadeIn = w * .6f; aura.hold = .8f; aura.startScale = .5f; aura.endScale = 1.05f; aura.pulse = .05f;
            var ink = Anchored(c.anchor, hand, "Frost charge ink", glow, 1.6f * gs, new Color(.02f, .12f, .38f, .55f), w + .03f, 118, false); ink.startScale = .3f; ink.endScale = 1; ink.ease = 1.5f; ink.hold = .9f; ink.fadeIn = .04f;
            var halo = Anchored(c.anchor, hand, "Frost charge halo", glow, 2.1f * gs, Alpha(light, .9f), w + .03f, 125); halo.startScale = .2f; halo.endScale = 1; halo.ease = 1.5f; halo.hold = .9f; halo.fadeIn = .04f;
            var shell = Anchored(c.anchor, hand, "Frost charge shell", orb, 1.05f * gs, Alpha(FrostPale, .95f), w + .03f, 131); shell.startScale = .1f; shell.endScale = 1; shell.ease = 1.5f; shell.hold = .9f; shell.spin = 260; shell.fadeIn = .04f;
            var heart = Anchored(c.anchor, hand, "Frost charge core", core, .5f * gs, Color.white, w + .03f, 132); heart.startScale = .25f; heart.endScale = 1; heart.ease = 1.5f; heart.hold = .9f; heart.pulse = .12f;

            // Light, snowflakes and cold breath spiral into the hand.
            Converge(hand, 12, 2.9f, light, w, .45f);
            int flakes = Budget(12);
            for (int i = 0; i < flakes; i++)
            {
                bool inked = i % 2 == 0;
                var f = Spawn("Gathering snowflake", flake, hand, Range(.32f, .56f), inked ? new Color(.12f, .4f, .88f, .95f) : Alpha(FrostPale, .95f), w * Range(.8f, 1), !inked, 130);
                f.orbitCenter = hand; f.orbitAngle = Range(0, 360); f.orbit = (i % 2 == 0 ? 1 : -1) * Range(200, 320); f.orbitRadius = Range(1.8f, 3.2f); f.orbitRadiusEnd = .15f; f.orbitPower = 1.3f;
                f.spin = Range(-300, 300); f.fadeIn = .07f; f.hold = .7f; f.endScale = .5f; f.ease = 1;
            }
            for (int i = 0; i < 6; i++)
            {
                var breath = Spawn("Frost breath", smoke[i % smoke.Length], pos, Range(1.1f, 1.6f), i % 2 == 0 ? new Color(.62f, .85f, 1, .5f) : new Color(.3f, .56f, .9f, .4f), w * Range(.85f, 1.05f), false, 114);
                breath.orbitCenter = pos; breath.orbitAngle = i * 60 + Range(-20, 20); breath.orbit = c.facing * 160; breath.orbitRadius = Range(2.2f, 2.8f); breath.orbitRadiusEnd = .4f; breath.orbitPower = 1.2f;
                breath.spin = Range(-120, 120); breath.fadeIn = w * .4f; breath.startScale = 1.2f; breath.endScale = .5f; breath.ease = 1; breath.fadePower = 1.2f; breath.angle = Range(0, 360);
            }

            // A crown of ice crystals forms around the caster, pointing outward.
            int crystals = 6 + 2 * (g - 1);
            for (int i = 0; i < crystals; i++)
            {
                float a0 = i * 360f / crystals + 30, delay = i * w * .3f / crystals;
                var crystal = Spawn("Forming crystal", FrostSpriteShard, pos, 1.05f, new Color(.62f, .9f, 1, 1), w + .02f - delay, false, 129);
                crystal.orbitCenter = pos + Vector2.up * .1f; crystal.orbitAngle = a0; crystal.orbit = 70 * c.facing; crystal.orbitRadius = 1.75f; crystal.orbitRadiusEnd = 1.15f;
                crystal.angle = a0; crystal.spin = 70 * c.facing;
                crystal.startScale = .05f; crystal.endScale = 1; crystal.ease = 2.5f; crystal.hold = .85f; crystal.fadePower = 2;
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
                        new Color(.86f, .96f, 1, 1), w + .07f - appear, false, 28);
                    tooth.angle = -side * Range(8, 20); tooth.axisStart = new Vector2(.5f, 0); tooth.axisEnd = Vector2.one; tooth.ease = 5; tooth.hold = .82f; tooth.fadePower = 2;
                    FrostDelay(tooth, appear);
                }

            // Release: the charge snaps into a white glint.
            float at0 = Mathf.Max(0, w - .02f);
            var release = Spawn("Frost release", glow, hand, 2.6f, Color.white, .16f, true, 133); release.startScale = .5f; release.endScale = 1.5f; release.ease = 3; release.fadePower = 1.5f; FrostDelay(release, at0);
            FrostDelay(Glint(hand, 2.5f, Color.white, .22f, 20), at0);
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
            if (g == 2) FrostDelay(GroundWave(ground + Vector2.up * .06f, R * 2 / .74f, new Color(.85f, .97f, 1, .85f), .5f), .16f);
            if (g >= 3)
            {
                for (int side = -1; side <= 1; side += 2) FrostCrystalFan(ground + new Vector2(side * R * .5f, 0), Mathf.Clamp(R * .7f, 1.6f, 3.4f), light);
                var column = Pillar(ground, 2, 8, Alpha(light, .7f), .9f, 27); column.hold = .3f;
                var columnCore = Pillar(ground, .6f, 8.4f, new Color(.92f, .99f, 1, .9f), .6f, 28); columnCore.hold = .25f;
                var columnInk = Spawn("Nova column ink", pillar, ground, 2.8f, new Color(.03f, .16f, .48f, .4f), 1, false, 26);
                columnInk.scale = new Vector3(2.8f / pillar.bounds.size.x, 8 / pillar.bounds.size.y, 1); columnInk.root.transform.localScale = columnInk.scale;
                columnInk.axisStart = new Vector2(.35f, .12f); columnInk.axisEnd = Vector2.one; columnInk.ease = 4; columnInk.hold = .3f; columnInk.fadePower = 1.3f;
            }

            // Peak frame: a white-hot heart inside a deep blue shade, a star glint and anime impact lines.
            var shade = Spawn("Nova shade", glow, pos, R * 1.9f + 1, new Color(.01f, .06f, .2f, .55f), .55f, false, 111);
            shade.startScale = .5f; shade.endScale = 1.15f; shade.ease = 4; shade.fadePower = 1.6f;
            var heart = Spawn("Nova heart", core, pos, k + .4f, Color.white, .08f, true, 134); heart.startScale = 1.2f; heart.endScale = .3f; heart.ease = 1;
            // The white peak keeps one size at every level (a bigger area grows the rings, ice and mist, not the glare).
            var flash = Spawn("Nova flash", glow, pos, Mathf.Min(R * 1.05f + .6f, 4f), new Color(1, 1, 1, .9f), .15f, true, 133); flash.startScale = .45f; flash.endScale = 1.5f; flash.ease = 4; flash.fadePower = 2.2f;
            flash.ramp = Ramp(Color.white, light);
            // A tight deep-blue ring frames the white peak so the very first frame already reads as ice, not a blank flash.
            // The ring starts outside the flash's bright core (rim radius about 1.6 units) so the flash cannot wash it out.
            var frame = Spawn("Nova peak ring ink", ring, pos, 3.4f * kk + 1.4f, new Color(.03f, .2f, .58f, .92f), .22f, false, 119);
            frame.startScale = .85f; frame.endScale = 1.35f; frame.ease = 3; frame.fadePower = 1.5f;
            var frameLight = Spawn("Nova peak ring", ring, pos, 3.3f * kk + 1.4f, Alpha(light, .9f), .18f, true, 132);
            frameLight.startScale = .85f; frameLight.endScale = 1.35f; frameLight.ease = 3; frameLight.fadePower = 1.5f;
            var bloom = Spawn("Nova bloom", glow, pos, R * 2.8f, Alpha(light, .45f / (1 + (g - 1) * .25f)), .34f, true, 124); bloom.startScale = .3f; bloom.endScale = 1.1f; bloom.ease = 4; bloom.fadePower = 2.2f;
            Glint(pos, Mathf.Min(R * 1.6f + .6f, 6.4f), new Color(.9f, .98f, 1, 1), .32f, Range(0, 45));
            // Higher levels' wider lines are drawn a little thinner in light and deeper in ink, so the peak stays ice, not white.
            var lines = ImpactLines(pos, R * 2.9f, new Color(.85f, .97f, 1, .95f - (g - 1) * .1f), .28f);
            var inked = Spawn("Nova ink lines", burst, pos, R * 2.95f, new Color(.04f, .2f, .5f, .55f + (g - 1) * .07f), .28f, false, 119);
            inked.angle = lines.angle + 4; inked.startScale = .55f; inked.endScale = 1.15f; inked.ease = 3; inked.fadePower = 1.4f;

            // Shock rings: the ring texture peaks at 74% of its half-width, so 2.7R puts the rim on the gameplay radius.
            float ringWidth = R * 2.7f;
            FrostSquash(Wave(pos, ringWidth, Alpha(light, .75f), .36f, 4.5f), .62f);
            var ringInk = Spawn("Nova ring ink", ring, pos, ringWidth * 1.02f, new Color(.04f, .26f, .68f, .85f), .4f, false, 119);
            FrostSquash(ringInk, .62f); ringInk.startScale = .12f; ringInk.endScale = 1; ringInk.ease = 4; ringInk.fadePower = 1.3f;
            GroundWave(ground + Vector2.up * .06f, ringWidth * 1.02f, Alpha(light, .95f), .5f);
            var groundInk = Spawn("Nova ground ink", ring, ground + Vector2.up * .04f, ringWidth * 1.02f, new Color(.04f, .26f, .62f, .75f), .55f, false, 26);
            FrostSquash(groundInk, .3f); groundInk.startScale = .12f; groundInk.endScale = 1; groundInk.ease = 3; groundInk.fadePower = 1.2f;
            FrostDelay(GroundWave(ground + Vector2.up * .06f, ringWidth * 1.1f, new Color(1, 1, 1, .7f), .45f), .09f);

            // Frozen ground across the whole radius, with a frost seal and a cold glow that linger.
            var sheet = Spawn("Frozen ground", FrostSpriteIce, ground + Vector2.up * .02f, R * 2.45f, Color.white, 2.1f, false, 24);
            FrostSquash(sheet, .2f); sheet.startScale = .2f; sheet.endScale = 1; sheet.ease = 6; sheet.hold = .55f; sheet.fadePower = 1.4f; sheet.fadeIn = .02f;
            var seal = Spawn("Frost seal", FrostSpriteSigil, ground + Vector2.up * .03f, R * 2.05f, Alpha(FrostPale, .9f), 1.3f, true, 27);
            FrostSquash(seal, .22f); seal.startScale = .3f; seal.endScale = 1; seal.ease = 5; seal.hold = .4f; seal.fadePower = 1.5f;
            var sealInk = Spawn("Frost seal ink", FrostSpriteSigil, ground + Vector2.up * .03f, R * 2.05f, new Color(.03f, .18f, .5f, .5f), 1.3f, false, 25);
            FrostSquash(sealInk, .22f); sealInk.startScale = .3f; sealInk.endScale = 1; sealInk.ease = 5; sealInk.hold = .4f; sealInk.fadePower = 1.5f;
            GroundGlow(ground, R * 2.6f, Alpha(light, .75f), 1.3f);

            // Ice crystals erupt outward from the caster in a front and a back row, then shatter.
            int front = Mathf.Clamp(Mathf.RoundToInt(R * 1.9f), 3, 7), back = Mathf.Clamp(Mathf.RoundToInt(R * 1.3f), 2, 6);
            const float travel = .2f;
            // The centrepiece: a fan of great crystals bursts up behind the caster at the peak, flash-lit, then shatters.
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
                // A wall of cold light races out along the floor with the ice.
                var wall = Pillar(ground + Vector2.down * .05f, 1.1f * k + .3f, 2.4f * k + .4f, Alpha(light, .75f), .3f, 126);
                wall.velocity = new Vector2(side * R * 5.5f, 0); wall.drag = 5;
            }

            // Shards, ice sparks and snowflakes fly; cold mist rolls away along the floor.
            int shards = Budget(Mathf.RoundToInt(9 + 3 * Mathf.Min(1, k)));
            for (int i = 0; i < shards; i++)
            {
                Vector2 direction = Direction(Range(8, 172));
                var chip = Spawn("Ice shard", FrostSpriteShard, pos + direction * .3f, Range(.3f, .6f) * kk, new Color(.6f, .9f, 1, 1), Range(.6f, .9f), false, 129);
                chip.velocity = direction * (R * Range(1.8f, 3.6f) + 1.5f); chip.gravity = 12; chip.drag = .8f; chip.alignVelocity = true; chip.fadePower = 1.2f; chip.hold = .4f;
            }
            int sparks = Budget(7);
            for (int i = 0; i < sparks; i++)
                Spark(pos + Inside(.3f), Direction(Range(0, 360)) * (R * Range(2, 4) + 1), i % 3 == 0 ? Color.white : FrostPale, Range(.25f, .4f), Range(.3f, .45f), 4, 2);
            int snow = Budget(Mathf.RoundToInt(10 * Mathf.Min(1, kk)));
            for (int i = 0; i < snow; i++)
            {
                bool inkedFlake = i % 2 == 0;
                var f = Spawn("Snowflake", flake, pos + Inside(R * .35f), Range(.22f, .45f), inkedFlake ? new Color(.2f, .5f, .92f, .9f) : Alpha(FrostPale, .95f), Range(1, 1.6f), !inkedFlake, 130);
                f.velocity = new Vector2(Range(-1, 1) * R * 1.4f, Range(.5f, 2.5f)); f.drag = 1.8f; f.gravity = .6f; f.spin = Range(-220, 220);
                f.fadeIn = .04f; f.hold = .35f; f.fadePower = 1.3f; f.angle = Range(0, 60);
            }
            // Cold mist starts at the ice front (not over the caster) so the peak frame stays crisp, then rolls outward.
            int mist = Budget(Mathf.RoundToInt(10 * Mathf.Min(1, kk)));
            for (int i = 0; i < mist; i++)
            {
                int side = i % 2 == 0 ? 1 : -1;
                var m = Puff(ground + new Vector2(side * Range(.3f, .55f) * R, Range(.15f, .5f)), Range(1.3f, 2) * kk, i % 3 != 2 ? new Color(.34f, .6f, .95f, .5f) : new Color(.74f, .9f, 1, .55f),
                    new Vector2(side * Range(2.5f, 5) * k, Range(.1f, .5f)), Range(1.1f, 1.6f), 113);
                m.drag = 2.4f; m.startScale = .4f; m.endScale = 2.4f; m.fadeIn = .08f;
                FrostDelay(m, Range(.04f, .1f));
            }
            // Lingering cold mist hugging the frozen floor, and snow drifting down over the area.
            int lowCount = Budget(Mathf.RoundToInt(5 * Mathf.Min(1, kk)));
            for (int i = 0; i < lowCount; i++)
            {
                var low = Puff(ground + new Vector2(Range(-.85f, .85f) * R, Range(.1f, .35f)), Range(1.3f, 1.9f) * kk, i % 2 == 0 ? new Color(.4f, .66f, .96f, .42f) : new Color(.78f, .93f, 1, .5f),
                    new Vector2(Range(-.6f, .6f), Range(0, .15f)), Range(1.6f, 2.1f), 113);
                low.drag = 1; low.fadeIn = .35f; low.startScale = .7f; low.endScale = 1.9f; low.fadePower = 1.6f;
                FrostDelay(low, Range(.1f, .3f));
            }
            int fallCount = Budget(Mathf.RoundToInt(8 * Mathf.Min(1, kk)));
            for (int i = 0; i < fallCount; i++)
            {
                bool inkedSnow = i % 2 == 0;
                var fall = Spawn("Falling snow", flake, ground + new Vector2(Range(-.95f, .95f) * R, Range(1.4f, 3.4f) * kk), Range(.2f, .4f), inkedSnow ? new Color(.18f, .48f, .92f, .9f) : Alpha(FrostPale, .95f), Range(1.3f, 1.9f), !inkedSnow, 130);
                fall.velocity = new Vector2(Range(-.4f, .4f), -Range(.4f, .9f)); fall.spin = Range(-160, 160); fall.angle = Range(0, 60); fall.fadeIn = .25f; fall.hold = .5f; fall.fadePower = 1.4f;
                FrostDelay(fall, Range(.12f, .6f));
            }
            Sparkles(pos, 4, R * .8f, FrostPale);
            for (int i = 0; i < 4; i++)
                FrostDelay(Glint(ground + new Vector2(Range(-.85f, .85f) * R, Range(.4f, 1.5f) * kk), Range(.5f, .9f), Color.white, .3f, Range(0, 45)), Range(.25f, .9f));
        }

        /// <summary>One ice crystal bursting from the floor (bottom pivot), shining, then breaking into falling chips.</summary>
        private void FrostSpike(Vector2 at, float height, float lean, float delay, float life, bool front, Color light)
        {
            var body = Spawn("Ice spike", FrostSpriteCrystal, at, height * .5f, front ? Color.white : new Color(.78f, .86f, 1, 1), life, false, front ? 127 : 28);
            body.angle = -lean; body.axisStart = new Vector2(.45f, 0); body.axisEnd = Vector2.one; body.ease = 8; body.hold = .72f; body.fadePower = 1.8f;
            FrostDelay(body, delay);
            if (!front) return;
            var sheen = Spawn("Ice spike light", spike, at, height / 3 * 1.05f, Alpha(light, .55f), life * .55f, true, 128);
            sheen.angle = body.angle; sheen.axisStart = new Vector2(.45f, 0); sheen.axisEnd = Vector2.one; sheen.ease = 8; sheen.fadePower = 2.2f;
            FrostDelay(sheen, delay);
            Vector2 up = Direction(90 - lean);
            float breakAt = delay + life * .74f;
            // The crystal bursts into a cluster of faceted fragments that fly apart and tumble down (one clip).
            var chips = Spawn("Ice spike shatter", FrostSpriteShatter, at + up * height * Range(.4f, .6f), Mathf.Clamp(height * .62f, .5f, 1.5f), new Color(.72f, .92f, 1, 1), Range(.5f, .62f), false, 129);
            chips.startScale = .35f; chips.endScale = 1.5f; chips.ease = 2.5f; chips.velocity = new Vector2(Range(-.7f, .7f), Range(.3f, 1.1f)); chips.gravity = 7;
            chips.spin = Range(-220, 220); chips.angle = Range(0, 360); chips.hold = .3f; chips.fadePower = 1.5f;
            FrostDelay(chips, breakAt);
            var crack = Spawn("Ice shatter", glow, at + up * height * .45f, height * .75f, Alpha(FrostPale, .75f), .2f, true, 130);
            crack.startScale = .4f; crack.endScale = 1.2f; crack.ease = 3; crack.fadePower = 1.6f; crack.scale.x *= .6f;
            FrostDelay(crack, breakAt);
        }

        /// <summary>A fan of great ice crystals bursting up from one point behind the caster (bottom pivot): flash-lit as it
        /// erupts, standing through the nova, then shattering into fragments.</summary>
        private void FrostCrystalFan(Vector2 ground, float width, Color light)
        {
            const float life = 1.15f;
            float height = width * .625f, breakAt = life * .7f;
            Vector2 foot = ground + Vector2.up * .04f;
            var fan = Spawn("Ice crown", FrostSpriteFan, foot, width, Color.white, life, false, 28);
            fan.axisStart = new Vector2(.55f, .15f); fan.axisEnd = Vector2.one; fan.ease = 12; fan.hold = .66f; fan.fadePower = 1.8f;
            // Same life and growth as the crystals so the light matches them; it burns off quickly.
            var lit = Spawn("Ice crown light", FrostSpriteFan, foot, width, Alpha(light, .85f), life, true, 29);
            lit.axisStart = fan.axisStart; lit.axisEnd = Vector2.one; lit.ease = 12; lit.fadePower = 6;
            for (int i = 0; i < 3; i++)
            {
                Vector2 at = ground + new Vector2((i - 1) * width * .27f, height * (i == 1 ? .6f : .38f));
                var chips = Spawn("Ice crown shatter", FrostSpriteShatter, at, height * (i == 1 ? .7f : .55f), new Color(.75f, .93f, 1, 1), .6f, false, 129);
                chips.startScale = .35f; chips.endScale = 1.55f; chips.ease = 2.5f; chips.velocity = new Vector2((i - 1) * 1.3f + Range(-.3f, .3f), Range(.4f, 1.2f));
                chips.gravity = 8; chips.spin = Range(-160, 160); chips.angle = Range(0, 360); chips.hold = .35f; chips.fadePower = 1.5f;
                FrostDelay(chips, breakAt + i * .03f);
            }
            var flash = Spawn("Ice crown shatter flash", glow, ground + Vector2.up * height * .5f, width * .85f, Alpha(FrostPale, .75f), .22f, true, 130);
            flash.startScale = .5f; flash.endScale = 1.2f; flash.ease = 3; flash.fadePower = 1.6f; FrostSquash(flash, .75f);
            FrostDelay(flash, breakAt);
            for (int side = -1; side <= 1; side += 2)
                FrostDelay(Glint(ground + new Vector2(side * width * Range(.12f, .22f), height * Range(.6f, .85f)), Range(.9f, 1.2f), Color.white, .26f, Range(0, 45)), Range(.2f, .45f));
        }

        // ---------------------------------------------------------------- poison cloud

        private void CastPoisonCloud(CastInfo c)
        {
            float w = c.windup, R = FrostAreaRadius(c.radius, 1.5f, 4);
            int f = c.facing;
            Vector2 pos = c.pos, hand = pos + new Vector2(f * .42f, .12f);
            if (w < .2f) { FrostQuickCast(c, hand, FrostAcid, FrostAcidHot, false); return; }
            // The vortex charges until it is thrown; the flask then arcs onto the mark and lands at the release.
            float flight = Mathf.Clamp(w * .45f, .1f, .2f), throwAt = w - flight, life = throwAt + .06f;

            var shade = Spawn("Venom shade", glow, hand, 3.2f, new Color(.04f, .08f, .01f, .5f), throwAt + .2f, false, 28);
            shade.fadeIn = throwAt * .4f; shade.hold = throwAt / (throwAt + .2f); shade.fadePower = 1.5f; shade.startScale = .5f; shade.endScale = 1.1f;

            // A toxic vortex spins up in the hand: dark green and plague-purple arms under glowing acid.
            // Higher levels swell the vortex; level three adds a plague rune turning behind it.
            int g = Grade; float gs = 1 + (g - 1) * .12f;
            var murk = Anchored(c.anchor, hand, "Venom vortex murk", swirl, 3.9f * gs, new Color(.06f, .18f, .02f, .9f), life, 28, false); murk.spin = -480 * f;
            var plague = Anchored(c.anchor, hand, "Venom vortex plague", swirl, 3.1f * gs, new Color(.32f, .08f, .42f, .8f), life, 29, false); plague.spin = 640 * f;
            var acid = Anchored(c.anchor, hand, "Venom vortex", swirl, 2.6f * gs, Alpha(FrostAcid, .75f), life, 126); acid.spin = -560 * f;
            var halo = Anchored(c.anchor, hand, "Venom halo", glow, 3.2f * gs, Alpha(FrostAcid, .7f), life, 125);
            var heart = Anchored(c.anchor, hand, "Venom core", core, .7f * gs, new Color(.92f, 1, .75f, 1), life, 131); heart.pulse = .15f;
            if (g >= 3)
            {
                var runeInk = Anchored(c.anchor, hand, "Venom plague rune ink", runeRing, 4.2f, new Color(.16f, .03f, .22f, .8f), life, 27, false); runeInk.spin = 90 * f;
                var rune = Anchored(c.anchor, hand, "Venom plague rune", runeRing, 4.1f, Alpha(FrostViolet, .8f), life, 28); rune.spin = 90 * f;
                foreach (var layer in new[] { runeInk, rune }) { layer.startScale = .3f; layer.endScale = 1; layer.ease = 2; layer.hold = .8f; layer.fadeIn = .08f; }
            }
            // Toxic fumes rise from the caster's feet while the vortex charges.
            Vector2 feet = pos + Vector2.down * .9f;
            Emitter(shade, 24, 0, (owner, p) =>
            {
                var fume = Puff(feet + new Vector2(Range(-.7f, .7f), Range(0, .35f)), Range(.9f, 1.3f), FrostMiasmaTone(random.Next(3)), new Vector2(Range(-.3f, .3f), Range(.8f, 1.5f)), Range(.5f, .7f), 29);
                fume.drag = 1.2f; fume.startScale = .7f; fume.endScale = 1.9f;
            });
            foreach (var layer in new[] { murk, plague, acid, halo, heart }) { layer.startScale = .25f; layer.endScale = 1; layer.ease = 1.8f; layer.hold = .82f; layer.fadeIn = .05f; }
            Converge(hand, 16, 2.8f, FrostAcid, throwAt, .5f);
            for (int i = 0; i < 6; i++)
            {
                var fume = Spawn("Gathering fume", smoke[i % smoke.Length], hand, Range(1.2f, 1.7f), i % 2 == 0 ? new Color(.1f, .26f, .04f, .6f) : new Color(.28f, .08f, .36f, .55f), throwAt * Range(.85f, 1.05f), false, 29);
                fume.orbitCenter = hand; fume.orbitAngle = i * 60 + Range(-15, 15); fume.orbit = -f * 220; fume.orbitRadius = Range(1.7f, 2.3f); fume.orbitRadiusEnd = .25f; fume.orbitPower = 1.2f;
                fume.spin = Range(-150, 150); fume.fadeIn = w * .35f; fume.startScale = 1.2f; fume.endScale = .4f; fume.ease = 1; fume.fadePower = 1.2f;
            }
            Emitter(halo, 14, .35f, (owner, p) =>
            {
                var bubble = Spawn("Venom bubble", FrostSpriteDrop, p, Range(.12f, .22f), new Color(.42f, .82f, .18f, .95f), Range(.35f, .55f), false, 128);
                bubble.velocity = new Vector2(Range(-.5f, .5f), Range(.8f, 1.7f)); bubble.startScale = .4f; bubble.endScale = 1.2f; bubble.ease = 1; bubble.hold = .7f; bubble.fadePower = 2;
            });

            // The landing spot is marked on the floor while the vortex charges.
            Vector2 mark = new Vector2(c.target.x, c.target.y - .7f + .03f);
            float markLife = w + .3f, markHold = w / markLife;
            var markFill = Spawn("Venom mark fill", glow, mark, R * 2.4f, new Color(.12f, .3f, .03f, .75f), markLife, false, 24);
            var markInk = Spawn("Venom mark ink", runeRing, mark, R * 2.03f, new Color(.16f, .03f, .22f, .95f), markLife, false, 25);
            var markLight = Spawn("Venom mark", runeRing, mark, R * 2, Alpha(FrostViolet, .9f), markLife, true, 27);
            foreach (var m in new[] { markFill, markInk, markLight }) { FrostSquash(m, .24f); m.fadeIn = w * .5f; m.startScale = .5f; m.endScale = 1; m.ease = 3; m.hold = markHold; m.fadePower = 1.5f; }
            GroundGlow(mark, R * 2.2f, Alpha(FrostAcid, .45f), markLife).hold = markHold;

            // Throw: the vortex bursts like a vial at the hand, glass and venom spray out.
            Vector2 aim = c.target - hand; float heading = aim.sqrMagnitude > .0001f ? Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg : (f > 0 ? 0 : 180);
            var pop = Spawn("Vial burst", glow, hand, 2.6f, Alpha(FrostAcidHot, .95f), .18f, true, 133); pop.startScale = .4f; pop.endScale = 1.5f; pop.ease = 3; pop.fadePower = 1.5f; FrostDelay(pop, throwAt);
            var popInk = Spawn("Vial burst ink", smoke[0], hand, 1.8f, new Color(.1f, .26f, .04f, .7f), .4f, false, 117); popInk.startScale = .5f; popInk.endScale = 1.8f; popInk.ease = 2; popInk.spin = 90; FrostDelay(popInk, throwAt);
            FrostDelay(Glint(hand, 1.8f, Alpha(FrostAcidHot, 1), .2f, 15), throwAt);
            for (int i = 0; i < Budget(7); i++)
            {
                var glass = Spawn("Vial glass", FrostSpriteShard, hand, Range(.22f, .36f), new Color(.82f, 1, .72f, .95f), Range(.4f, .55f), false, 129);
                glass.velocity = Direction(heading + Range(-80, 80)) * Range(3, 6) + Vector2.up * 1.5f; glass.gravity = 12; glass.spin = Range(-700, 700); glass.angle = Range(0, 360); glass.hold = .4f;
                FrostDelay(glass, throwAt);
            }
            for (int i = 0; i < Budget(6); i++)
            {
                var drop = Spawn("Venom spray", FrostSpriteDrop, hand, Range(.16f, .26f), new Color(.38f, .78f, .14f, 1), Range(.3f, .45f), false, 128);
                drop.velocity = Direction(heading + Range(-35, 35)) * Range(4, 7) + Vector2.up * Range(.5f, 2.5f); drop.gravity = 15; drop.stretch = .05f; drop.alignVelocity = true; drop.hold = .5f;
                FrostDelay(drop, throwAt);
            }

            // The flask of venom arcs onto the mark, trailing glowing drips and fumes, and lands exactly at the release.
            Vector2 landing = mark + Vector2.up * .3f;
            const float gravity = 34;
            Vector2 launch = (landing - hand) / flight + Vector2.up * (.5f * gravity * flight);
            // A dark venom body under the glowing shell keeps the flask readable against pale skies.
            var flaskInk = Spawn("Venom flask shade", glow, hand, 1.5f, new Color(.05f, .14f, .02f, .7f), flight, false, 118);
            var flaskBody = Spawn("Venom flask body", FrostSpriteDrop, hand, .72f, new Color(.2f, .5f, .07f, 1), flight, false, 129);
            var flask = Spawn("Venom flask", orb, hand, .95f, Alpha(FrostAcid, 1), flight, true, 130); flask.spin = 900 * f;
            var flaskGlow = Spawn("Venom flask glow", glow, hand, 2.4f, Alpha(FrostAcid, .85f), flight, true, 128);
            var flaskCore = Spawn("Venom flask core", core, hand, .4f, FrostAcidHot, flight, true, 131);
            foreach (var clip in new[] { flaskInk, flaskBody, flask, flaskGlow, flaskCore }) { clip.velocity = launch; clip.gravity = gravity; clip.fadePower = 0; FrostDelay(clip, throwAt); }
            Emitter(flaskGlow, 70, .12f, (owner, p) =>
            {
                var trace = Spawn("Venom trail", glow, p, Range(.4f, .65f), Alpha(FrostAcid, .7f), Range(.15f, .25f), true, 127); trace.endScale = .2f; trace.ease = 1;
                if (Chance(.5f))
                {
                    var drip = Spawn("Venom drip", FrostSpriteDrop, p, Range(.12f, .2f), new Color(.3f, .66f, .1f, 1), Range(.3f, .45f), false, 126);
                    drip.velocity = owner.velocity * .15f + new Vector2(Range(-.5f, .5f), Range(-.5f, .5f)); drip.gravity = 14; drip.fadePower = 1.5f;
                }
                else Puff(p, Range(.4f, .6f), FrostMiasmaTone(random.Next(2)), Vector2.zero, Range(.3f, .45f), 117);
            });
        }

        private void ImpactPoisonCloud(Vector2 pos, float radius, Color tint, int facing)
        {
            float r = Mathf.Clamp(radius, .7f, 4), k = Mathf.Clamp(r / 2, .45f, 1.4f);
            Vector2 ground = new Vector2(pos.x, pos.y - .72f);
            FrostVenomBurst(ground, r, k);
            var pool = Spawn("Venom puddle", FrostSpritePool, ground + Vector2.up * .02f, r * 2.5f, Color.white, 1.7f, false, 24);
            FrostSquash(pool, .24f); pool.startScale = .2f; pool.endScale = 1; pool.ease = 6; pool.hold = .45f; pool.fadePower = 1.4f; pool.fadeIn = .03f;
            GroundGlow(ground, r * 2.7f, Alpha(FrostAcid, .55f), 1.2f);
            FrostSkullWisp(pos + new Vector2(Range(-.2f, .2f), .25f), 1.15f + .4f * k, .12f);
            for (int i = 0; i < Budget(6); i++)
            {
                var linger = Puff(ground + new Vector2(Range(-.8f, .8f) * r, Range(.3f, 1.1f) * k), Range(1, 1.5f) * k, FrostMiasmaTone(i), new Vector2(Range(-.4f, .4f), Range(.2f, .5f)), Range(1.2f, 1.6f), 113);
                linger.drag = 1; linger.fadeIn = .25f; linger.endScale = 2.2f; linger.fadePower = 1.6f;
            }
            FrostArc(pos + new Vector2(-r * .5f, Range(-.2f, .4f)), pos + new Vector2(r * .4f, Range(-.2f, .5f)), FrostAcid, .07f, .14f);
        }

        private void ZonePoisonCloud(Transform root, float radius, float life)
        {
            float r = Mathf.Clamp(radius, .8f, 5), k = Mathf.Clamp(r / 2.8f, .4f, 1.6f);
            Vector2 center = root.position;
            const float lift = .6f;
            Vector2 floor = new Vector2(0, -lift + .03f);

            // Acid pool across the radius, a glow on the floor and a plague sigil.
            var pool = Attach(root, "Venom pool", FrostSpritePool, r * 2.55f, Color.white, 24, false, floor); FrostSquash(pool, .22f);
            var poolGlow = Attach(root, "Venom pool glow", glow, r * 2.8f, Alpha(FrostAcid, .5f), 26, true, floor); FrostSquash(poolGlow, .3f); poolGlow.pulse = .07f;
            var sigilMark = Attach(root, "Venom sigil", runeRing, r * 2.1f, Alpha(FrostViolet, .55f), 27, true, floor + Vector2.up * .01f); FrostSquash(sigilMark, .22f);
            // The churning body: a dark shade, three vortex clusters (dark arms behind the actors, glowing arms in front)
            // and a soft acid haze, so the cloud has depth and the enemies inside stay readable.
            // Higher levels boil harder (more vortex clusters, denser fumes, more skulls and arcs) inside the same true radius;
            // level three also turns a plague seal on the floor. Its root creeps after the nearest enemy, and every layer and
            // emitter follows the root.
            int g = Grade;
            float kk = Mathf.Clamp(k, .6f, 1.3f), rate = Mathf.Min(k, 1.25f) * (1 + (g - 1) * .25f);
            var body = Attach(root, "Miasma shadow", glow, r * 2.5f, new Color(.05f, .13f, .02f, .5f), 111, false, new Vector2(0, .75f)); FrostSquash(body, .62f); body.pulse = .05f;
            var haze = Attach(root, "Miasma haze", glow, r * 2.7f, Alpha(FrostAcid, .3f), 116, true, new Vector2(0, .7f)); FrostSquash(haze, .6f); haze.pulse = .06f;
            var layers = new System.Collections.Generic.List<Clip> { poolGlow, sigilMark, body, haze };
            float[] spots = g >= 3 ? new[] { -.62f, -.31f, 0, .31f, .62f } : g == 2 ? new[] { -.6f, -.2f, .2f, .6f } : new[] { -.55f, 0, .55f };
            int mid = spots.Length / 2; float shrink = Mathf.Pow(3f / spots.Length, .35f);
            Clip geyserHost = null;
            for (int i = 0; i < spots.Length; i++)
            {
                bool centre = i == mid;
                Vector2 offset = new Vector2(spots[i] * r, .75f + (centre ? .45f : (i % 2) * .2f));
                float size = (centre ? 3.1f : 2.5f) * kk * shrink, turn = (i % 2 == 0 ? 1 : -1) * 55;
                var under = Attach(root, "Miasma deep vortex", swirl, size * 1.2f, centre ? new Color(.07f, .2f, .03f, .5f) : new Color(.22f, .05f, .28f, .45f), 29, false, offset + new Vector2(-.15f, -.1f)); under.spin = turn * .6f;
                var dark = Attach(root, "Miasma vortex", swirl, size, centre ? new Color(.22f, .05f, .28f, .5f) : new Color(.07f, .2f, .03f, .55f), 112, false, offset); dark.spin = turn;
                var bright = Attach(root, "Miasma glow vortex", swirl, size * .8f, Alpha(FrostAcid, .42f), 126, true, offset + new Vector2(.1f, .05f)); bright.spin = -turn * 1.4f;
                layers.Add(under); layers.Add(dark); layers.Add(bright);
                if (centre) geyserHost = bright;
            }
            if (g >= 3)
            {
                // A squashed child on the floor, so the plague seal turns in the ground plane.
                var plane = new GameObject("Venom ground plane").transform; plane.SetParent(root, false);
                plane.localPosition = new Vector3(0, floor.y + .01f, 0); plane.localScale = new Vector3(1, .26f, 1);
                // A hexagram curse seal (not the ice nova's snowflake) in plague violet over deep murk.
                var plagueInk = Attach(plane, "Venom plague seal ink", sigil, r * 2 / .93f * 1.02f, new Color(.16f, .03f, .22f, .75f), 25, false); plagueInk.spin = -30;
                var plagueSeal = Attach(plane, "Venom plague seal", sigil, r * 2 / .93f, Alpha(Color.Lerp(FrostViolet, FrostAcid, .2f), .7f), 27); plagueSeal.spin = -30; plagueSeal.pulse = .03f;
                layers.Add(plagueInk); layers.Add(plagueSeal);
            }
            foreach (var layer in layers) FrostZoneLayer(layer, life);
            FrostZoneLayer(pool, life);
            Bundle(root, pool, layers.ToArray());

            // The cloud is full at once, then miasma keeps boiling inside the radius (mostly behind the actors).
            for (int i = 0; i < Budget(Mathf.RoundToInt(14 * kk)); i++)
            {
                bool behind = i % 3 != 0;
                float x = Range(-.9f, .9f) * r;
                var fill = Puff(center + new Vector2(x, Range(-.35f, 1.5f) * (1 - .4f * Mathf.Abs(x / r))), Range(1.6f, 2.4f) * kk, FrostMiasmaTone(i, behind ? 1 : .7f),
                    new Vector2(Range(-.4f, .4f), Range(.1f, .4f)), Range(1.6f, 2.4f), behind ? 29 : 113);
                fill.drag = .8f; fill.startScale = .4f; fill.endScale = 1.6f; fill.fadeIn = .15f; fill.fadePower = 1.5f;
                FrostDelay(fill, Range(0, .12f));
            }
            // When the cloud creeps (level three), new fumes carry its drift so the body moves as one mass.
            Vector2 lastCenter = center, drift = Vector2.zero; float lastAge = 0;
            Emitter(body, 22 * rate, 0, (owner, p) =>
            {
                if (FrostZoneEnding(owner)) return;
                Vector2 o = FrostZoneCenter(owner);
                if (owner.age > lastAge + .05f) { drift = Vector2.Lerp(drift, (o - lastCenter) / (owner.age - lastAge), .5f); lastCenter = o; lastAge = owner.age; }
                bool behind = Chance(.6f);
                float x = Range(-.92f, .92f) * r, y = Range(-.4f, 1.7f) * (1 - .4f * Mathf.Abs(x / r));
                var puff = Puff(o + new Vector2(x, y), Range(1.5f, 2.4f) * kk, FrostMiasmaTone(random.Next(3), behind ? 1 : .7f),
                    new Vector2(-Mathf.Sign(x) * Range(.1f, .5f) + Range(-.2f, .2f), Range(.15f, .5f)) + drift, Range(1.6f, 2.2f), behind ? 29 : 113);
                puff.drag = .8f; puff.startScale = .6f; puff.endScale = 2; puff.fadeIn = .35f; puff.fadePower = 1.6f;
            });
            // Glowing fumes and toxic sparks.
            Emitter(haze, 10 * rate, 0, (owner, p) =>
            {
                if (FrostZoneEnding(owner)) return;
                Vector2 at = FrostZoneCenter(owner) + new Vector2(Range(-.85f, .85f) * r, Range(-.5f, .8f));
                if (Chance(.5f))
                {
                    var fume = Spawn("Miasma light", smoke[random.Next(smoke.Length)], at, Range(.8f, 1.3f), Alpha(FrostAcid, .32f), Range(.9f, 1.3f), true, 117);
                    fume.velocity = new Vector2(Range(-.3f, .3f), Range(.2f, .5f)); fume.spin = Range(-60, 60); fume.angle = Range(0, 360); fume.startScale = .6f; fume.endScale = 1.8f; fume.ease = 1; fume.fadeIn = .25f; fume.fadePower = 1.5f;
                }
                else Spark(at, new Vector2(Range(-.6f, .6f), Range(1.2f, 2.6f)), Chance(.3f) ? FrostAcidHot : FrostAcid, Range(.14f, .26f), Range(.45f, .75f), -1, 1.2f).flicker = .4f;
            });
            // Bubbles rising out of the acid.
            Emitter(pool, 12 * rate, 0, (owner, p) =>
            {
                if (FrostZoneEnding(owner)) return;
                Vector2 at = FrostZoneCenter(owner) + new Vector2(Range(-.8f, .8f) * r, -lift + Range(0, .12f));
                var bubble = Spawn("Venom bubble", FrostSpriteDrop, at, Range(.24f, .42f), new Color(.62f, 1, .3f, .95f), Range(.4f, .7f), false, 119);
                bubble.velocity = new Vector2(Range(-.15f, .15f), Range(.4f, .9f)); bubble.startScale = .4f; bubble.endScale = 1.3f; bubble.ease = 1; bubble.hold = .75f; bubble.fadePower = 2;
            });
            // Now and then the acid erupts in a small geyser of glowing venom.
            Emitter(geyserHost, 1.3f * rate, 0, (owner, p) =>
            {
                if (FrostZoneEnding(owner)) return;
                Vector2 o = FrostZoneCenter(owner), at = new Vector2(o.x + Range(-.7f, .7f) * r, o.y - lift + .03f);
                var pop = Spawn("Venom geyser glow", glow, at + Vector2.up * .35f, 1.5f, Alpha(FrostAcid, .8f), .32f, true, 125); pop.startScale = .3f; pop.endScale = 1.3f; pop.ease = 3; pop.fadePower = 1.5f;
                var jet = Spawn("Venom geyser", spike, at, Range(.42f, .55f), new Color(.36f, .76f, .12f, .92f), .32f, false, 121);
                jet.angle = Range(-10, 10); jet.axisStart = new Vector2(.6f, 0); jet.axisEnd = new Vector2(.8f, 1); jet.ease = 6; jet.hold = .2f; jet.fadePower = 1.5f;
                for (int i = 0; i < 4; i++)
                {
                    var drop = Spawn("Venom geyser drop", FrostSpriteDrop, at + Vector2.up * Range(.3f, .9f), Range(.14f, .24f), new Color(.4f, .82f, .14f, 1), Range(.45f, .6f), false, 122);
                    drop.velocity = new Vector2(Range(-1.5f, 1.5f), Range(3, 5.5f)); drop.gravity = 14; drop.stretch = .04f; drop.alignVelocity = true; drop.hold = .5f; drop.fadePower = 1.5f;
                }
            });
            // Skull wisps drift up out of the cloud.
            Emitter(sigilMark, 1.4f * rate, 0, (owner, p) =>
            {
                if (FrostZoneEnding(owner)) return;
                FrostSkullWisp(FrostZoneCenter(owner) + new Vector2(Range(-.6f, .6f) * r, Range(-.1f, .7f)), Range(1.3f, 1.7f), 0);
            });
            FrostSkullWisp(center + new Vector2(Range(-.3f, .3f) * r, .3f), 1.6f, .25f);
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

        /// <summary>An acid splash on the floor: flash, rings, a crown of venom tendrils, droplets and billowing fumes.</summary>
        private void FrostVenomBurst(Vector2 ground, float r, float k)
        {
            float kk = Mathf.Clamp(k, .6f, 1.3f);
            Vector2 mid = ground + Vector2.up * (.55f * kk);
            var shade = Spawn("Venom burst shade", glow, mid, r * 2 + 1.2f, new Color(.03f, .09f, .01f, .58f), .65f, false, 111); shade.startScale = .6f; shade.endScale = 1.2f; shade.ease = 3; shade.fadePower = 1.4f;
            var flash = Spawn("Venom flash", glow, mid, r * 1.8f + 1.2f, Alpha(FrostAcidHot, .95f), .22f, true, 132); flash.startScale = .6f; flash.endScale = 1.4f; flash.ease = 3; flash.fadePower = 1.6f;
            flash.ramp = Ramp(Color.white, FrostAcid);
            // A dark venom ring snaps out under the flash so the first frame reads as poison on pale floors too.
            var frame = Spawn("Venom peak ring ink", ring, mid, 2.2f * kk + .8f, new Color(.08f, .26f, .03f, .9f), .22f, false, 119);
            frame.startScale = .45f; frame.endScale = 1.35f; frame.ease = 3; frame.fadePower = 1.5f;
            var heart = Spawn("Venom flash core", core, mid, 1.1f * kk, new Color(.95f, 1, .85f, 1), .12f, true, 133); heart.endScale = .3f; heart.ease = 1;
            // Peak frame: acid impact lines over a dark ink copy, and a sharp glint.
            float lineWidth = r * 1.7f + 1.6f;
            var lines = ImpactLines(mid, lineWidth, Alpha(FrostAcidHot, .95f), .26f);
            var ink = Spawn("Venom ink lines", burst, mid, lineWidth * 1.02f, new Color(.07f, .2f, .02f, .6f), .26f, false, 119);
            ink.angle = lines.angle + 4; ink.startScale = .55f; ink.endScale = 1.15f; ink.ease = 3; ink.fadePower = 1.4f;
            Glint(mid, 2.4f * kk, Alpha(FrostAcidHot, 1), .24f, Range(0, 45));
            GroundWave(ground + Vector2.up * .05f, r * 2.7f, Alpha(FrostAcid, .9f), .45f);
            var ringInk = Spawn("Venom ring ink", ring, ground + Vector2.up * .04f, r * 2.7f, new Color(.1f, .3f, .04f, .7f), .5f, false, 26);
            FrostSquash(ringInk, .3f); ringInk.startScale = .12f; ringInk.endScale = 1; ringInk.ease = 3; ringInk.fadePower = 1.2f;

            // The venom erupts in a splash crown (behind the actors, lit for a moment) that flings droplets from its tips.
            float crownWidth = Mathf.Clamp(r * 1.3f, 1.6f, 4.8f), crownHeight = crownWidth * .7f;
            Vector2 crownFoot = ground + Vector2.down * .04f;
            var crown = Spawn("Venom splash crown", FrostSpriteCrown, crownFoot, crownWidth, Color.white, .6f, false, 29);
            crown.axisStart = new Vector2(.6f, .25f); crown.axisEnd = Vector2.one; crown.ease = 10; crown.hold = .35f; crown.fadePower = 1.6f;
            var crownLight = Spawn("Venom splash light", FrostSpriteCrown, crownFoot, crownWidth, Alpha(FrostAcid, .55f), .6f, true, 121);
            crownLight.axisStart = crown.axisStart; crownLight.axisEnd = Vector2.one; crownLight.ease = 10; crownLight.fadePower = 5;
            int drops = Budget(Mathf.RoundToInt(9 * kk));
            for (int i = 0; i < drops; i++)
            {
                float t = drops == 1 ? 0 : i / (float)(drops - 1) * 2 - 1;
                Vector2 tip = ground + new Vector2(t * crownWidth * .36f, crownHeight * Range(.55f, .95f) * (1 - .4f * Mathf.Abs(t)));
                var drop = Spawn("Venom droplet", FrostSpriteDrop, tip, Range(.17f, .3f), new Color(.35f, .75f, .12f, 1), Range(.55f, .8f), false, 123);
                drop.velocity = new Vector2(t * Range(2.5f, 4.5f), Range(2.5f, 5.5f)); drop.gravity = 16; drop.stretch = .05f; drop.alignVelocity = true; drop.hold = .5f; drop.fadePower = 1.5f;
                FrostDelay(drop, .06f);
            }
            // A toxic mushroom: dark fumes shoot up the middle while billows roll out along the floor.
            int plumes = Budget(4);
            for (int i = 0; i < plumes; i++)
            {
                var plume = Puff(ground + new Vector2(Range(-.25f, .25f) * kk, .5f + i * .25f), Range(1.3f, 1.7f) * kk, i % 2 == 0 ? new Color(.07f, .2f, .02f, .72f) : new Color(.25f, .06f, .3f, .62f),
                    new Vector2(Range(-.4f, .4f), Range(3, 5) * kk), Range(.9f, 1.2f), i < 2 ? 29 : 113);
                plume.drag = 2.6f; plume.startScale = .7f; plume.endScale = 2.3f; plume.fadeIn = .02f;
            }
            int billow = Budget(Mathf.RoundToInt(10 * kk));
            for (int i = 0; i < billow; i++)
            {
                int side = i % 2 == 0 ? 1 : -1;
                var fume = Puff(ground + new Vector2(side * Range(0, .4f) * r, Range(.3f, 1)), Range(1.2f, 1.8f) * kk, FrostMiasmaTone(i, 1.2f), new Vector2(side * Range(2, 4.5f) * kk, Range(.2f, 1.2f)), Range(1, 1.5f), 113);
                fume.drag = 2.2f; fume.startScale = .7f; fume.endScale = 2.6f; fume.fadeIn = .02f;
            }
            for (int i = 0; i < Budget(10); i++)
                Spark(mid, Direction(Range(10, 170)) * Range(3, 7) * kk, i % 3 == 0 ? FrostAcidHot : FrostAcid, Range(.2f, .35f), Range(.35f, .6f), 6, 1.5f);
        }

        private void FrostSkullWisp(Vector2 at, float size, float delay)
        {
            float life = Range(1.3f, 1.7f);
            Vector2 drift = new Vector2(Range(-.25f, .25f), Range(.45f, .75f));
            var ghost = Spawn("Miasma skull", FrostSpriteSkull, at, size, new Color(1, 1, 1, .92f), life, false, 120);
            var aura = Spawn("Miasma skull glow", glow, at, size * 1.8f, Alpha(FrostAcid, .35f), life, true, 119);
            var shadow = Spawn("Miasma skull shade", glow, at, size * 1.45f, new Color(.03f, .09f, .01f, .5f), life, false, 118);
            foreach (var clip in new[] { ghost, aura, shadow }) { clip.velocity = drift; clip.drag = .3f; clip.fadeIn = .35f; clip.hold = .25f; clip.fadePower = 1.4f; clip.startScale = .75f; clip.endScale = 1.2f; clip.ease = 1; FrostDelay(clip, delay); }
            ghost.angle = Range(-12, 12); ghost.spin = Range(-10, 10);
        }

        /// <summary>A short jagged arc of venom lightning (three segments).</summary>
        private void FrostArc(Vector2 a, Vector2 b, Color color, float width, float life)
        {
            Vector2 delta = b - a; float length = delta.magnitude; if (length < .05f) return;
            Vector2 side = new Vector2(-delta.y, delta.x) / length, previous = a;
            for (int i = 1; i <= 3; i++)
            {
                Vector2 point = i == 3 ? b : Vector2.Lerp(a, b, i / 3f) + side * Range(-.25f, .25f) * length;
                var halo = Line(previous, point, width * 3.2f, Alpha(color, .6f), life, 128, 1.15f); halo.flicker = .4f; halo.fadePower = 1.5f;
                var inner = Line(previous, point, width, new Color(.95f, 1, .85f, 1), life * .9f, 129, 1.1f); inner.flicker = .3f; inner.fadePower = 1.3f;
                previous = point;
            }
        }

        // ---------------------------------------------------------------- shared helpers

        /// <summary>Instant echoes (wind-up under 0.2 s): a quick flare in the hand that does not compete with the release.</summary>
        private void FrostQuickCast(CastInfo c, Vector2 hand, Color light, Color hot, bool ice)
        {
            var flare = Anchored(c.anchor, hand, "Quick cast glow", glow, 1.7f, Alpha(light, .9f), .22f, 126); flare.startScale = .4f; flare.endScale = 1.2f; flare.ease = 2; flare.fadePower = 1.4f;
            Anchored(c.anchor, hand, "Quick cast ink", glow, 1.3f, ice ? new Color(.02f, .12f, .36f, .45f) : new Color(.1f, .22f, .03f, .5f), .24f, 118, false);
            var heart = Anchored(c.anchor, hand, "Quick cast core", core, .42f, Color.white, .14f, 132); heart.endScale = .3f;
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

        private static Color FrostMiasmaTone(int index, float alphaScale = 1)
        {
            switch (index % 3)
            {
                case 0: return new Color(.1f, .26f, .04f, .6f * alphaScale);
                case 1: return new Color(.27f, .07f, .33f, .52f * alphaScale);
                default: return new Color(.33f, .62f, .12f, .5f * alphaScale);
            }
        }

        // ---------------------------------------------------------------- textures

        private static void BuildFrostTextures()
        {
            // A level-three ice nova freezes its foes: build the frozen shell now, at load, so the first freeze does not hitch.
            StatusTextures(StatusKind.Freeze);
            // A cluster of three faceted ice crystals, bottom pivot, with deep blue rims so it reads on pale backgrounds.
            FrostSpriteCrystal = Build("Frost crystal", 96, 192, (u, v) =>
            {
                float h = (v + 1) * .5f;
                Color c = FrostFacet(Color.clear, u, h, -.4f, -.82f, .56f, .3f);
                c = FrostFacet(c, u, h, .42f, .84f, .46f, .26f);
                return FrostFacet(c, u, h, 0, .05f, .98f, .5f);
            }, new Vector2(.5f, 0));

            // A fan of nine great faceted crystals radiating up from one point (bottom pivot): the ice nova's centrepiece.
            FrostSpriteFan = Build("Frost crystal fan", 256, 160, (u, v) =>
            {
                float px = u * 1.6f, py = v + 1;
                Color c = Color.clear;
                for (int i = 0; i < FrostFanBlades.Length; i += 3)
                {
                    float theta = FrostFanBlades[i] * Mathf.Deg2Rad, s = Mathf.Sin(theta), co = Mathf.Cos(theta), bx = s * .12f;
                    float along = (px - bx) * s + py * co, across = (px - bx) * co - py * s;
                    c = FrostFacet(c, across, along, 0, 0, FrostFanBlades[i + 1], FrostFanBlades[i + 2]);
                }
                return c;
            }, new Vector2(.5f, 0));

            // Seven faceted ice fragments around an empty centre, pointing outward: scaled up, it reads as a crystal bursting apart.
            FrostSpriteShatter = Build("Frost shatter", 128, 128, (u, v) =>
            {
                Color c = Color.clear;
                for (int i = 0; i < 7; i++)
                {
                    uint h = Hash(i * 29 + 401);
                    float angle = (i * 51.4f + ((h & 255) / 255f - .5f) * 30) * Mathf.Deg2Rad, s = Mathf.Sin(angle), co = Mathf.Cos(angle);
                    float start = .2f + (h >> 8 & 255) / 255f * .2f, length = .3f + (h >> 16 & 255) / 255f * .24f, half = .08f + (h >> 24 & 255) / 255f * .05f;
                    c = FrostFacet(c, -u * s + v * co, u * co + v * s - start, 0, 0, length, half);
                }
                return c;
            });

            // A splash crown of venom (bottom pivot): a low mound and seven curling tendrils tipped with droplets,
            // acid green with a dark rim and a wet highlight so it reads on pale and dark floors alike.
            FrostSpriteCrown = Build("Venom crown", 256, 192, (u, v) =>
            {
                float px = u * 1.333f, py = v + 1;
                // Each part reports a normalised distance (0 on its spine, 1 on its outline); the deepest part shades the pixel,
                // so overlaps merge without inner rim lines.
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
                float cover = 1 - Smooth(.8f, 1, nd), edge = Smooth(.45f, .95f, nd);
                if (cover <= .002f) return Color.clear;
                Color body = Color.Lerp(new Color(.2f, .45f, .06f), new Color(.62f, 1, .3f), Mathf.Clamp01(py / 1.6f) * .7f + .2f);
                body = Color.Lerp(body, new Color(.92f, 1, .72f), Gauss(side + .4f, .2f) * .65f * (1 - edge));
                body = Color.Lerp(body, new Color(.04f, .13f, .02f), edge * .85f);
                body.a = cover * .96f;
                return body;
            }, new Vector2(.5f, 0));

            // A greyscale faceted shard (lit top face, dark rim) to tint as ice or glass; points along +x.
            FrostSpriteShard = Build("Frost shard", 96, 48, (u, v) =>
            {
                float along = u > 0 ? u / .98f : -u / .7f, dist = along + Mathf.Abs(v) / .9f;
                float inside = 1 - Smooth(.84f, .96f, dist);
                if (inside <= 0) return Color.clear;
                float shade = v > 0 ? .95f - .25f * Mathf.Abs(u) : .5f - .2f * Mathf.Abs(u), rim = Smooth(.55f, .9f, dist);
                float l = Mathf.Clamp01(shade * (1 - rim * .72f) + Gauss(v - .14f, .08f) * (1 - Mathf.Abs(u)) * .45f);
                return new Color(l, l, l, inside * Mathf.Lerp(.9f, 1, rim));
            });

            // A round droplet with a dark rim and a specular spot.
            FrostSpriteDrop = Build("Frost droplet", 64, 64, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), inside = 1 - Smooth(.7f, .84f, r);
                if (inside <= 0) return Color.clear;
                float rim = Smooth(.4f, .8f, r), spec = Gauss(Mathf.Sqrt(Sq(u + .26f) + Sq(v - .3f)), .13f);
                float l = Mathf.Clamp01((.78f + .22f * v) * (1 - rim * .65f) + spec * .9f);
                return new Color(l, l, l, inside);
            });

            // A six-fold snowflake seal: branched arms, two hexagons, double rim with fine ticks.
            FrostSpriteSigil = Build("Frost seal", 512, 512, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float theta = Mathf.Atan2(v, u);
                float sector = Mathf.Repeat(theta + Mathf.PI / 6, Mathf.PI / 3) - Mathf.PI / 6;
                float x = r * Mathf.Cos(sector), y = Mathf.Abs(r * Mathf.Sin(sector));
                float a = Gauss(y, .012f) * Smooth(.06f, .1f, x) * (1 - Smooth(.8f, .84f, x));
                float[] starts = { .26f, .46f, .63f }, lengths = { .17f, .14f, .09f };
                for (int b = 0; b < 3; b++)
                {
                    float t = x - starts[b];
                    if (t > 0 && t < lengths[b] + .03f) a = Mathf.Max(a, Gauss(y - t * 1.05f, .011f) * (1 - Smooth(lengths[b] - .03f, lengths[b], t)));
                }
                float diamond = Mathf.Abs(x - .87f) / .05f + y / .03f;
                a = Mathf.Max(a, (1 - Smooth(.7f, 1, diamond)) * .9f);
                a += Gauss(x - .57f, .008f) * .8f;
                float sector2 = Mathf.Repeat(theta, Mathf.PI / 3) - Mathf.PI / 6;
                a += Gauss(r * Mathf.Cos(sector2) - .4f, .008f) * .7f;
                a += Gauss(r - .96f, .011f) + Gauss(r - .91f, .006f) * .85f + Gauss(r - .2f, .007f) * .7f;
                float tick = Mathf.Abs(Mathf.Repeat(theta * Mathf.Rad2Deg + 2.5f, 5) - 2.5f);
                a += (1 - Smooth(.25f, .55f, tick)) * Smooth(.912f, .918f, r) * (1 - Smooth(.95f, .956f, r)) * .7f;
                a += Gauss(Mathf.Sqrt(Sq(x - .65f) + Sq(y - .375f)), .025f) * .9f;
                a += (1 - Smooth(.15f, .96f, r)) * .07f;
                return White(Mathf.Clamp01(a) * (1 - Smooth(.975f, .998f, r)));
            });

            // Frozen ground: a pale blue ice sheet with Voronoi cracks and a white frosted rim.
            FrostSpriteIce = Build("Frost ice sheet", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float edge = .8f + (Fbm(u * 2.2f + 31, v * 2.2f + 17, 3) - .5f) * .35f;
                float inside = 1 - Smooth(edge - .12f, edge, r);
                if (inside <= 0) return Color.clear;
                float crackLine = Gauss(FrostCellEdge(u * 4.2f + 10, v * 4.2f + 10), .05f);
                float frost = Fbm(u * 6 + 3, v * 6 + 8, 3), rim = Smooth(edge - .3f, edge - .05f, r);
                Color ice = Color.Lerp(new Color(.42f, .74f, .96f), new Color(.7f, .9f, 1), frost);
                ice = Color.Lerp(ice, new Color(.92f, .98f, 1), rim * .8f);
                ice = Color.Lerp(ice, new Color(.08f, .3f, .62f), crackLine * .85f * (1 - rim * .5f));
                ice.a = inside * Mathf.Clamp01(.55f + rim * .35f + crackLine * .3f + (frost - .5f) * .3f);
                return ice;
            });

            // An acid pool: mottled toxic green with purple taint, a dark rim, surface bubbles and satellite splashes.
            FrostSpritePool = Build("Venom pool", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float edge = .78f + (Fbm(u * 2.4f + 5, v * 2.4f + 41, 3) - .5f) * .4f;
                float inside = 1 - Smooth(edge - .06f, edge, r), splash = 0;
                for (int k = 0; k < 8; k++)
                {
                    uint h = Hash(k * 13 + 211);
                    float angle = k * .785f + ((h & 255) / 255f - .5f) * .6f, dist = .8f + (h >> 8 & 255) / 255f * .12f, size = .03f + (h >> 16 & 255) / 255f * .035f;
                    float d = Mathf.Sqrt(Sq(u - Mathf.Cos(angle) * dist) + Sq(v - Mathf.Sin(angle) * dist));
                    splash = Mathf.Max(splash, 1 - Smooth(size * .7f, size, d));
                }
                float cover = Mathf.Max(inside, splash);
                if (cover <= 0) return Color.clear;
                float rim = Mathf.Max(Smooth(edge - .2f, edge - .04f, r) * inside, splash * .7f);
                Color body = Color.Lerp(new Color(.3f, .62f, .08f), new Color(.68f, 1, .3f), Smooth(.35f, .75f, Fbm(u * 5 + 2, v * 5 + 9, 3)));
                body = Color.Lerp(body, new Color(.14f, .32f, .04f), Smooth(.55f, .8f, Fbm(u * 3 + 11, v * 3 + 2, 2)) * .6f);
                body = Color.Lerp(body, new Color(.35f, .12f, .4f), Smooth(.62f, .8f, Fbm(u * 2 + 21, v * 2 + 7, 2)) * .7f);
                body = Color.Lerp(body, new Color(.05f, .12f, .02f), rim * .85f);
                float bubble = 0;
                for (int k = 0; k < 10; k++)
                {
                    uint h = Hash(k * 7 + 300);
                    float bx = ((h & 255) / 255f - .5f) * 1.1f, by = ((h >> 8 & 255) / 255f - .5f) * 1.1f, br = .035f + (h >> 16 & 255) / 255f * .06f;
                    float d = Mathf.Sqrt(Sq(u - bx) + Sq(v - by));
                    bubble = Mathf.Max(bubble, Gauss(d - br, .014f) + (d < br ? .15f : 0));
                }
                body = Color.Lerp(body, new Color(.9f, 1, .7f), Mathf.Clamp01(bubble) * .85f * inside);
                body.a = cover * .92f;
                return body;
            });

            // A toxic skull wisp: pale green cranium and jaw, dark sockets with glowing pupils, a smoky tail.
            FrostSpriteSkull = Build("Venom skull", 128, 128, (u, v) =>
            {
                float cr = Mathf.Sqrt(Sq(u / .56f) + Sq((v - .2f) / .5f)), cranium = 1 - Smooth(.9f, 1, cr);
                float jx = Mathf.Abs(u) - .28f, jy = Mathf.Abs(v + .28f) - .18f;
                float jd = Mathf.Sqrt(Sq(Mathf.Max(jx, 0)) + Sq(Mathf.Max(jy, 0))) + Mathf.Min(Mathf.Max(jx, jy), 0) - .08f;
                float shape = Mathf.Max(cranium, 1 - Smooth(-.03f, .02f, jd));
                float tailT = Mathf.Clamp01((-.42f - v) / .5f);
                float tail = v < -.36f ? Gauss(u - Mathf.Sin(v * 9 + 1) * .12f * tailT, .28f - tailT * .15f) * Mathf.Pow(1 - tailT, 1.5f) : 0;
                float cover = Mathf.Max(shape, tail * .8f) * (1 - Smooth(.9f, 1, Mathf.Abs(u)));
                if (cover <= .002f) return Color.clear;
                float eye = 1 - Smooth(.8f, 1, Mathf.Min(Mathf.Sqrt(Sq((u + .23f) / .17f) + Sq((v - .02f) / .15f)), Mathf.Sqrt(Sq((u - .23f) / .17f) + Sq((v - .02f) / .15f))));
                float nose = 1 - Smooth(.8f, 1, Mathf.Sqrt(Sq(u / .07f) + Sq((v + .19f) / .09f)));
                float teeth = v < -.33f && v > -.46f && Mathf.Abs(u) < .24f ? Gauss(Mathf.Repeat(u + .06f, .12f) - .06f, .014f) : 0;
                float mouth = Mathf.Abs(u) < .27f ? Gauss(v + .33f, .02f) : 0;
                float dark = Mathf.Clamp01(eye + nose + teeth * .8f + mouth * .7f) * shape;
                float light = Mathf.Clamp01(.72f + .3f * (v * .4f - u * .25f) - Smooth(.5f, 1, cr) * .25f);
                Color c = Color.Lerp(new Color(.18f, .42f, .08f), new Color(.78f, 1, .52f), light);
                c = Color.Lerp(c, new Color(.03f, .07f, .02f), dark);
                float pupil = Gauss(Mathf.Min(Mathf.Sqrt(Sq(u + .23f) + Sq(v)), Mathf.Sqrt(Sq(u - .23f) + Sq(v))), .05f);
                c = Color.Lerp(c, new Color(.85f, 1, .45f), pupil * .9f);
                c.a = Mathf.Clamp01(cover);
                return c;
            });
        }

        private static Color FrostFacet(Color under, float u, float h, float baseX, float tipX, float tipH, float halfWidth)
        {
            float t = h / tipH; if (t <= 0 || t >= 1) return under;
            float half = halfWidth * Mathf.Pow(1 - t, .8f) + .002f, d = (u - Mathf.Lerp(baseX, tipX, t)) / half;
            float inside = (1 - Smooth(.8f, 1, Mathf.Abs(d))) * Smooth(0, .04f, h);
            if (inside <= 0) return under;
            float ridge = -.2f + .15f * t;
            float shade = d < ridge ? Mathf.Lerp(1, .68f, (d + 1) / (ridge + 1)) : Mathf.Lerp(.5f, .26f, (d - ridge) / (1 - ridge));
            shade = Mathf.Clamp01(shade * Mathf.Lerp(.8f, 1.12f, t) + Gauss(d - ridge, .05f) * .25f);
            Color mid = new Color(.34f, .68f, .98f);
            Color body = shade < .5f ? Color.Lerp(new Color(.05f, .18f, .46f), mid, shade * 2) : Color.Lerp(mid, new Color(.9f, .98f, 1), (shade - .5f) * 2);
            float rim = Smooth(.55f, .95f, Mathf.Abs(d)) * (1 - t * .4f);
            body = Color.Lerp(body, new Color(.03f, .1f, .3f), rim * .7f);
            body = Color.Lerp(body, Color.white, Mathf.Clamp01(Gauss(d + .6f, .09f) * (1 - t) * .8f + Gauss(1 - t, .06f) * .5f));
            body.a = inside * Mathf.Lerp(.86f, 1, rim);
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
