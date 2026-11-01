using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Grace family: HealPulse, ShieldPulse and LifeDrain.
    // HealPulse: heaven opens over the caster (a narrow descending shaft, a halo, falling light) while a lotus seal unfolds
    // and light and petals spiral into the hands; the release strikes a divine column down from the sky, raises a ring of
    // sanctuary pillars on the edge of the area, and throws life rings and a petal storm.
    // ShieldPulse: bevelled ward plates fly in and lock onto a golden hexagonal dome rising out of a rune seal; the dome
    // snaps shut, a scan of light runs up its cells, a beacon shoots from its apex and light shards break off.
    // LifeDrain: a black-crimson vortex drinks blood smoke into the hand while the victim is branded; the release tears a
    // ghost out of the victim and pulls a thick soul river (dark channel, crimson and life strands, smoke, souls) into the
    // caster, who surges with life.
    // Big shapes are drawn twice: a saturated alpha-blended "pigment" or dark "ink" layer that keeps its colour on pale
    // marble skies, and a smaller additive light layer on top. Large backdrops (halo, column, dome) sit behind the actors.
    // Instant echo casts (wind-up under 0.2 s) are always followed at once by the skill's own burst, so they stay small.
    public sealed partial class TowerVfx
    {
        private static Sprite GraceDome, GraceSoul, GraceFacet, GraceMandala, GraceShaft, GracePlate, GraceRibbon;
        private static Sprite[] GraceGlyphs;
        private static readonly Color GraceCrimson = new Color(1, .16f, .3f), GraceBlood = new Color(.5f, .01f, .1f, .8f), GraceVoid = new Color(.05f, 0, .04f, .9f),
            GraceAwake = new Color(1, .93f, .68f), GraceUmber = new Color(.36f, .2f, .03f, .72f);
        private const float GraceDomeWidth = 4.4f, GraceDomeHeight = 3.6f;
        // The caster's drink of the first soul river of a drain, so a second river landing with it only adds a gulp.
        private Clip GraceDrinkMark;

        // ---------------------------------------------------------------- heal

        private void CastHealPulse(CastInfo c)
        {
            float k = Mathf.Clamp(c.size / 1.4f, .75f, 2), w = c.windup, release = Mathf.Max(0, w - .02f);
            Color main = GraceVivid(c.tint, LifeLight), life = Color.Lerp(main, LifeLight, .15f), hot = Color.Lerp(life, Warm, .55f);
            Color deep = GraceDeep(main, .3f, .85f), pigment = GraceDeep(main, .62f, .9f);
            Vector2 pos = c.pos, ground = pos + Vector2.down, hand = pos + new Vector2(c.facing * .25f, .45f), heart = pos + Vector2.up * .2f;

            // Life gathers in the raised hands and peaks exactly at the release.
            float handLife = w + .1f;
            var aura = Anchored(c.anchor, hand, "Grace heal hand pigment", glow, 1.7f * k, Alpha(pigment, .55f), handLife, 118, false);
            aura.fadeIn = .05f; aura.startScale = .35f; aura.endScale = 1.1f; aura.ease = 1.5f; aura.hold = w / handLife; aura.fadePower = 2;
            var bloom = Anchored(c.anchor, hand, "Grace heal hand bloom", glow, 2 * k, Alpha(life, .75f), handLife, 124);
            bloom.fadeIn = .04f; bloom.startScale = .3f; bloom.endScale = 1.15f; bloom.ease = 1.5f; bloom.hold = w / handLife; bloom.fadePower = 2; bloom.pulse = .06f;
            var handCore = Anchored(c.anchor, hand, "Grace heal hand core", core, .6f * k, Alpha(Warm, .95f), w + .05f, 126);
            handCore.fadeIn = .04f; handCore.startScale = .25f; handCore.endScale = 1; handCore.ease = 1.5f; handCore.hold = .85f; handCore.fadePower = 2;
            if (w < .2f)
            {
                // An instant echo: the heal burst follows at once, so the echo only sparks the hands.
                Converge(hand, 6, 1.8f * k, life, w, .45f);
                var spark0 = GraceLater(Glint(hand, 1.8f * k, Alpha(Warm, .9f), .2f), release); spark0.spin = 0;
                return;
            }
            float total = w + .4f, hold = w / total;
            // Higher levels open a wider shaft of heaven and a bigger halo.
            int grade = Grade; float gl = GraceLevel(1.15f, 1.3f);

            // Heaven opens: a narrow shaft descends on the caster (behind the actors) and drops of light rain down; both
            // brighten toward the release.
            float beamLife = w + .16f;
            for (int layer = 0; layer < 3; layer++)
            {
                var shaft = layer == 0 ? GraceColumn(GraceShaft, ground, 1.15f * k * gl, 12, Alpha(pigment, .55f), beamLife, false, 26)
                    : layer == 1 ? GraceColumn(GraceShaft, ground, .9f * k * gl, 12, Alpha(life, .6f), beamLife, true, 27)
                    : GraceColumn(pillar, ground, .32f * k * gl, 12, Alpha(Warm, .8f), beamLife, true, 27);
                shaft.axisStart = new Vector2(.12f, 1); shaft.axisEnd = Vector2.one; shaft.ease = 1.4f; shaft.fadeIn = w * .45f; shaft.hold = w / beamLife; shaft.fadePower = 2.2f;
            }
            int drops = Budget(6);
            for (int i = 0; i < drops; i++)
            {
                float x = pos.x + Range(-.8f, .8f) * k, top = pos.y + Range(4.5f, 6);
                var drop = GraceLater(Line(new Vector2(x, top), new Vector2(x, top - 1.3f), .11f * k, Alpha(Color.Lerp(life, Warm, .5f), .95f), .3f, 130), Range(0, Mathf.Max(0, w - .3f)));
                drop.velocity = Vector2.down * 15; drop.fadeIn = .05f; drop.fadePower = 1.2f;
            }

            // A lotus seal unfolds under the caster: dark ink beneath the light so it reads on pale floors.
            var ink = Spawn("Grace lotus ink", GraceMandala, ground, 4 * k, deep, total, false, 25);
            GraceFlat(ink, .3f); ink.spin = 30; ink.fadeIn = .06f; ink.startScale = .45f; ink.endScale = 1; ink.ease = 3; ink.hold = hold; ink.fadePower = 1.5f;
            var seal = Spawn("Grace lotus seal", GraceMandala, ground, 3.8f * k, Alpha(life, .95f), total, true, 27);
            GraceFlat(seal, .3f); seal.spin = 30; seal.fadeIn = .05f; seal.startScale = .45f; seal.endScale = 1; seal.ease = 3; seal.hold = hold; seal.fadePower = 1.5f;
            var runes = RuneCircle(ground, 2.6f * k, Alpha(hot, .85f), total, -80, true); runes.hold = hold;
            var floor = GroundGlow(ground, 5 * k, Alpha(life, .5f), total); floor.hold = hold;
            Emitter(floor, 22 * k, 1.7f * k, (owner, p) => GraceHealMote(GraceOnGround(owner, p), Chance(.3f) ? hot : LifeLight, pigment, k));

            // A lotus halo opens behind the caster as the charge builds (behind the actors, so the hero stays readable).
            var haloInk = Spawn("Grace heal halo ink", GraceMandala, heart, 4.2f * k * gl, Alpha(deep, .8f), total, false, 26);
            haloInk.spin = -25; haloInk.startScale = .3f; haloInk.endScale = 1; haloInk.ease = 2; haloInk.fadeIn = .06f; haloInk.hold = hold; haloInk.fadePower = 1.6f;
            var halo = Spawn("Grace heal halo", GraceMandala, heart, 4.1f * k * gl, Alpha(life, .9f), total, true, 27);
            halo.spin = -25; halo.startScale = .3f; halo.endScale = 1; halo.ease = 2; halo.fadeIn = .06f; halo.hold = hold; halo.fadePower = 1.6f;
            if (grade >= 2)
            {
                // Level two (part of the heal becomes a shield): a gold ring of ward light turns inside the halo the other way;
                // awakened, a gold-white rune ring crowns the halo's rim.
                var wardInk = Spawn("Grace heal ward ring ink", runeRing, heart, 2.7f * k, GraceUmber, total, false, 26);
                var ward = Spawn("Grace heal ward ring", runeRing, heart, 2.6f * k, Alpha(Color.Lerp(GoldLight, Warm, .3f), .9f), total, true, 28);
                foreach (var clip in new[] { wardInk, ward }) { clip.spin = 60; clip.startScale = .3f; clip.endScale = 1; clip.ease = 2; clip.fadeIn = .08f; clip.hold = hold; clip.fadePower = 1.6f; }
            }
            if (grade >= 3)
            {
                var crown = Spawn("Grace heal crown", runeRing, heart, 4.6f * k * gl, Alpha(GraceAwake, .8f), total, true, 28);
                crown.spin = 18; crown.startScale = .35f; crown.endScale = 1; crown.ease = 2; crown.fadeIn = .1f; crown.hold = hold; crown.fadePower = 1.6f; crown.pulse = .03f;
            }

            Converge(hand, 14, 2.7f * k, life, w, .45f);
            // Petals whirl in from a wide circle and vanish into the hands.
            int petals = Budget(12);
            for (int i = 0; i < petals; i++)
            {
                bool lit = i % 3 == 0;
                var leaf = Spawn("Grace gathering petal", petal, hand, Range(.4f, .6f) * k, lit ? Alpha(Warm, .9f) : Alpha(Color.Lerp(pigment, main, Range(0, .4f)), .95f), w * Range(.85f, 1), lit, lit ? 130 : 121);
                leaf.orbitCenter = hand; leaf.orbitAngle = i * 360f / petals + Range(-12, 12); leaf.orbit = Range(240, 340);
                leaf.orbitRadius = Range(2.1f, 2.9f) * k; leaf.orbitRadiusEnd = .1f; leaf.orbitPower = 1.4f;
                leaf.spin = Range(-300, 300); leaf.fadeIn = .1f; leaf.hold = .7f; leaf.fadePower = 2; leaf.startScale = 1; leaf.endScale = .4f;
            }

            // Release: a bloom of light, a tall cross flare and rings (heart pickups and rest heals end here).
            var flash = GraceLater(Spawn("Grace heal release", glow, hand, 2.8f * k, Alpha(Warm, .85f), .26f, true, 131), release);
            flash.ramp = Ramp(Color.white, life); flash.startScale = .45f; flash.endScale = 1.35f; flash.ease = 2.5f; flash.fadePower = 1.6f;
            var cross = GraceLater(Glint(hand, 3.2f * k, Color.white, .34f), release); cross.scale.y *= 1.5f; cross.spin = 0;
            GraceLater(Wave(pos, 4.4f * k, Alpha(life, .85f), .38f, 3), release);
            GraceLater(GraceRing(pos, 4 * k, Alpha(pigment, .75f), .36f, 3, false, 119), release);
            GraceLater(GroundWave(ground, 5.6f * k, Alpha(life, .85f), .45f), release);
            int pops = Budget(8);
            for (int i = 0; i < pops; i++)
            {
                bool lit = i % 2 == 0;
                var leaf = GraceLater(Spawn("Grace petal pop", petal, hand, Range(.3f, .45f) * k, lit ? Alpha(Warm, .9f) : Alpha(pigment, .95f), Range(.6f, .9f), lit, lit ? 130 : 121), release);
                leaf.velocity = Direction(i * 360f / pops + Range(-15, 15)) * Range(3.5f, 5.5f) * k + Vector2.up; leaf.drag = 3; leaf.gravity = -1; leaf.spin = Range(-500, 500); leaf.hold = .35f; leaf.fadePower = 1.3f;
            }
        }

        private void ImpactHealPulse(Vector2 pos, float radius, Color tint, int facing)
        {
            float R = Mathf.Clamp(radius, .8f, 4.5f), s = Mathf.Clamp(.35f + R * .19f, .55f, 1.2f);
            Color main = GraceVivid(tint, LifeLight), life = Color.Lerp(main, LifeLight, .15f), hot = Color.Lerp(life, Warm, .55f);
            Color deep = GraceDeep(main, .3f, .85f), pigment = GraceDeep(main, .62f, .9f);
            Vector2 ground = pos + Vector2.down * .95f, heart = pos + Vector2.up * .2f;
            float span = Mathf.Max(2.6f, R * 1.9f);
            // Higher levels: a wider divine column and taller sanctuary pillars (the healed area itself grows with the radius).
            int grade = Grade; float gl = GraceLevel(1.15f, 1.3f);

            // Peak: a white-hot heart, an emerald bloom, a tall holy cross and impact lines, all inside the first 0.05 s.
            var shade = Underlay(heart, 5 * s, .26f, .5f); shade.hold = .2f;
            var core0 = Spawn("Grace heal heart", core, pos, 1.3f * s, Color.white, .14f, true, 133); core0.startScale = .5f; core0.endScale = 1.7f; core0.ease = 2.5f; core0.fadePower = 1.5f;
            var tintBloom = Spawn("Grace heal pigment bloom", glow, heart, 4.4f * s, Alpha(pigment, .55f), .5f, false, 119); tintBloom.startScale = .45f; tintBloom.endScale = 1.2f; tintBloom.ease = 3; tintBloom.fadePower = 1.4f;
            var bloom = Flash(pos, 3.6f * s, Alpha(life, .7f), .28f); bloom.ramp = Ramp(Color.white, life); bloom.startScale = .45f; bloom.endScale = 1.25f; bloom.ease = 3;
            var cross = Glint(heart, 5.2f * s, Color.white, .45f); cross.scale.y *= 1.65f; cross.spin = 0;
            var star = Glint(heart, 3.2f * s, Alpha(hot, .95f), .36f, 45); star.spin = -70;
            GracePigment(ImpactLines(pos, 7.4f * s, Alpha(pigment, .8f), .34f), 119);
            ImpactLines(pos, 6.6f * s, Alpha(Color.Lerp(life, Color.white, .45f), .7f), .3f);
            Rays(heart, 8, 1.6f * s, 3.4f * s, life, .3f);

            // A huge lotus halo flares open behind the caster.
            var haloInk = Spawn("Grace heal halo ink", GraceMandala, heart, 5 * s, Alpha(deep, .62f), .9f, false, 26);
            haloInk.spin = -30; haloInk.startScale = .55f; haloInk.endScale = 1.1f; haloInk.ease = 4; haloInk.hold = .25f; haloInk.fadePower = 1.5f;
            var halo = Spawn("Grace heal halo", GraceMandala, heart, 4.9f * s, Alpha(life, .9f), .85f, true, 27);
            halo.spin = -30; halo.startScale = .55f; halo.endScale = 1.1f; halo.ease = 4; halo.hold = .2f; halo.fadePower = 1.6f;

            // A divine column strikes down from the sky at full height and widens: dark-green body and soft light behind
            // the actors, a beam and a white spine in front; the strike splashes light across the floor.
            float height = 14 * Mathf.Lerp(.7f, 1, Mathf.InverseLerp(.55f, 1, s));
            var shaft = GraceColumn(GraceShaft, ground, 3.2f * s * gl, height, Alpha(deep, .72f), 1.15f, false, 26); shaft.hold = .3f; shaft.fadePower = 1.2f;
            var glowShaft = GraceColumn(GraceShaft, ground, 2.7f * s * gl, height, Alpha(life, .7f), 1, true, 27); glowShaft.hold = .25f;
            var beam = GraceColumn(pillar, ground, 1.9f * s * gl, height, Alpha(life, .8f), .8f, true, 128); beam.hold = .1f;
            var spine = GraceColumn(pillar, ground, .6f * s * gl, height, grade >= 3 ? GraceAwake : Color.white, .5f, true, 129); spine.fadePower = 1.6f;
            foreach (var column in new[] { shaft, glowShaft, beam, spine }) { column.axisStart = new Vector2(.12f, 1); column.ease = 5; }
            var strike = Spawn("Grace heal strike", glow, ground, span * .9f, Alpha(Color.Lerp(life, Warm, .25f), .7f), .3f, true, 128);
            GraceFlat(strike, .3f); strike.startScale = .3f; strike.endScale = 1.1f; strike.ease = 3; strike.fadePower = 1.6f;

            // Life rings on the body and the floor, staggered; pigment rings keep their colour on pale backdrops.
            Wave(pos, R * 2.6f, Alpha(life, .75f), .5f, 3);
            GraceRing(pos, R * 2.1f, Alpha(pigment, .8f), .42f, 3.5f, false, 119);
            GraceLater(Wave(pos, R * 3.1f, Alpha(hot, .35f), .55f, 2.5f), .1f);
            GroundWave(ground, span * 1.55f, Alpha(life, .7f), .55f);
            GraceFlat(GraceLater(GraceRing(ground, span * 1.9f, Alpha(pigment, .6f), .55f, 3, false, 24), .1f), .34f);
            var inkRing = GraceRing(ground, span * 1.5f, Alpha(deep, .75f), .6f, 3, false, 24); GraceFlat(inkRing, .34f);

            // The lotus seal holds on the floor, slowly turning, then fades; light motes and leaves rise off it.
            var sealInk = Spawn("Grace lotus ink", GraceMandala, ground, span * 1.06f, deep, 1.5f, false, 25);
            GraceFlat(sealInk, .3f); sealInk.spin = 16; sealInk.fadeIn = .03f; sealInk.startScale = .55f; sealInk.endScale = 1; sealInk.ease = 6; sealInk.hold = .45f; sealInk.fadePower = 1.3f;
            var seal = Spawn("Grace lotus seal", GraceMandala, ground, span, life, 1.4f, true, 27);
            GraceFlat(seal, .3f); seal.spin = 16; seal.fadeIn = .03f; seal.startScale = .55f; seal.endScale = 1; seal.ease = 6; seal.hold = .4f; seal.fadePower = 1.4f;
            RuneCircle(ground, span * .66f, Alpha(hot, .9f), 1.3f, -45, true);
            var floor = GroundGlow(ground, span * 1.1f, Alpha(life, .32f), 1.3f);
            Emitter(floor, 30 * s, span * .45f, (owner, p) => GraceHealMote(GraceOnGround(owner, p), Chance(.35f) ? hot : LifeLight, pigment, s));

            // Sanctuary: pillars of light erupt one after another around the rim of the healed area.
            int posts = Budget(Mathf.RoundToInt(4 + 3 * s));
            float rimX = span * .47f, rimY = span * .47f * .3f;
            for (int i = 0; i < posts; i++)
            {
                float a = (i * 360f / posts + 90 + 180f / posts) * Mathf.Deg2Rad;
                Vector2 at = ground + new Vector2(Mathf.Cos(a) * rimX, Mathf.Sin(a) * rimY);
                bool back = Mathf.Sin(a) > 0;
                float delay = .05f + i * .035f, h = Range(3.3f, 4.3f) * s * gl * (back ? .85f : 1);
                var stem = GraceLater(GraceColumn(GraceShaft, at, .75f * s, h, Alpha(pigment, .85f), .7f, false, back ? 26 : 120), delay);
                stem.axisStart = new Vector2(.5f, .05f); stem.ease = 5; stem.hold = .3f; stem.fadePower = 1.4f;
                var post = GraceLater(GraceColumn(pillar, at, 1.05f * s, h * 1.15f, Alpha(life, .95f), .6f, true, back ? 27 : 127), delay);
                post.axisStart = new Vector2(.35f, .05f); post.ease = 5; post.hold = .2f; post.fadePower = 1.5f;
                var burstAt = GraceLater(Spawn("Grace sanctuary spring", glow, at, 1.6f * s, Alpha(Color.Lerp(life, Warm, .4f), .9f), .26f, true, back ? 27 : 128), delay);
                GraceFlat(burstAt, .4f); burstAt.startScale = .4f; burstAt.endScale = 1.2f; burstAt.ease = 3; burstAt.fadePower = 1.5f;
            }

            // Petal storm: petals whirl out of the heart and drift up.
            int petals = Budget(Mathf.RoundToInt(30 * s));
            for (int i = 0; i < petals; i++)
            {
                bool lit = i % 4 == 0;
                var leaf = Spawn("Grace petal storm", petal, pos, Range(.34f, .55f) * Mathf.Max(.8f, s), lit ? Alpha(Warm, .95f) : Alpha(Color.Lerp(pigment, main, Range(0, .45f)), .95f), Range(1, 1.6f), lit, lit ? 130 : 121);
                leaf.orbitCenter = pos + Vector2.down * .15f; leaf.velocity = Vector2.up * Range(.5f, 1.5f); leaf.drag = .3f;
                leaf.orbitAngle = i * 360f / petals + Range(-12, 12); leaf.orbit = (i % 4 == 3 ? -1 : 1) * Range(110, 210);
                leaf.orbitRadius = .25f; leaf.orbitRadiusEnd = R * Range(.55f, 1); leaf.orbitPower = .3f;
                leaf.spin = Range(-420, 420); leaf.hold = .35f; leaf.fadePower = 1.3f; leaf.startScale = .5f; leaf.endScale = 1; leaf.ease = 3;
            }
            int motes = Budget(Mathf.RoundToInt(14 * s));
            for (int i = 0; i < motes; i++)
                GraceLater(GraceHealMote(ground + new Vector2(Range(-.5f, .5f) * span, Range(-.12f, .12f) * span), i % 3 == 0 ? hot : LifeLight, pigment, s * 1.2f), Range(0, .25f));
            for (int i = 0; i < 6; i++)
                GraceLater(Glint(pos + new Vector2(Range(-R, R) * .75f, Range(-.6f, 1.8f) * s), Range(.7f, 1.2f) * s, Alpha(Color.Lerp(hot, Color.white, .4f), .95f), .3f, Range(0, 45)), Range(.06f, .6f));
            var after = Spawn("Grace heal afterglow", glow, pos, 2.8f * s, Alpha(life, .45f), 1.3f, true, 122); after.fadeIn = .15f; after.fadePower = 1.6f; after.pulse = .05f;
            // A companion's pulse heals the hero, not the caster: a stream of life runs from the caster to the hero and blooms there.
            var game = ArcheryGame();
            if (game != null && game.hero != null && game.hero.root)
            {
                Vector2 chest = game.hero.pos + Vector2.up * .95f;
                if ((chest - pos).sqrMagnitude > 2.25f) GraceLifeStream(pos, chest, life, hot, pigment);
            }
            if (grade < 2) return;
            // Level two turns part of the heal into a shield: a faint gold ward dome rises over the hero with the light.
            Color ward = Color.Lerp(GoldLight, Warm, .35f);
            var domeInk = GraceDomeLayer(ground, GraceDomeWidth * .88f * 1.02f, GraceDomeHeight * .88f * 1.02f, Alpha(GraceUmber, .45f), .8f, false, 26);
            var dome = GraceDomeLayer(ground, GraceDomeWidth * .88f, GraceDomeHeight * .88f, Alpha(ward, .3f), .75f, true, 121);
            foreach (var clip in new[] { domeInk, dome }) { clip.startScale = .8f; clip.endScale = 1; clip.ease = 5; clip.hold = .35f; clip.fadePower = 1.5f; GraceLater(clip, .06f); }
            for (int i = 0; i < 3; i++)
            {
                Vector2 normal = Direction(40 + i * 50);
                GraceLater(Glint(ground + new Vector2(normal.x * GraceDomeWidth * .44f, normal.y * GraceDomeHeight * .88f), Range(.8f, 1.1f), Alpha(Warm, .95f), .26f, Range(0, 45)), .1f + i * .06f);
            }
            if (grade < 3) return;
            // Awakened (the heal leaves a lasting field): gold-white petals rain down through the column onto the field.
            int falls = Budget(5);
            for (int i = 0; i < falls; i++)
            {
                var leaf = GraceLater(Spawn("Grace heal gold petal", petal, ground + new Vector2(Range(-1.2f, 1.2f), Range(4, 5.5f)), Range(.32f, .45f), Alpha(i % 2 == 0 ? GraceAwake : Warm, .95f), 1.1f, true, 130), Range(.1f, .4f));
                leaf.velocity = new Vector2(Range(-.4f, .4f), -Range(3.5f, 4.5f)); leaf.drag = .6f; leaf.spin = Range(-300, 300); leaf.fadeIn = .1f; leaf.hold = .5f; leaf.fadePower = 1.4f;
            }
        }

        // ---------------------------------------------------------------- shield

        private void CastShieldPulse(CastInfo c)
        {
            float k = Mathf.Clamp(c.size / 1.4f, .8f, 1.6f), w = c.windup, release = Mathf.Max(0, w - .02f);
            Color main = GraceVivid(c.tint, GoldLight), gold = Color.Lerp(main, GoldLight, .15f), bright = Color.Lerp(gold, Warm, .5f);
            Color bronze = GraceDeep(main, .3f, .68f, -.04f), pigment = GraceDeep(main, .78f, .92f, -.035f);
            Vector2 ground = c.pos + Vector2.down, body = c.pos + Vector2.up * .15f, hand = c.pos + new Vector2(c.facing * .3f, .4f);

            var palm = Anchored(c.anchor, hand, "Grace ward palm", glow, 1.6f * k, Alpha(gold, .75f), w + .08f, 124);
            palm.fadeIn = .04f; palm.startScale = .35f; palm.endScale = 1.1f; palm.ease = 1.5f; palm.hold = .8f; palm.fadePower = 2; palm.pulse = .05f;
            if (w < .2f)
            {
                // An instant echo: the dome itself comes with the shield burst that follows at once.
                Converge(body, 5, 1.8f * k, bright, w, .3f);
                var tick = GraceLater(Glint(hand, 1.6f * k, Alpha(Warm, .9f), .2f), release); tick.spin = 0;
                return;
            }
            // Higher levels raise a bigger, brighter dome.
            int grade = Grade; float gl = GraceLevel(1.07f, 1.14f);
            float total = w + .35f, hold = w / total, domeW = GraceDomeWidth * k * gl, domeH = GraceDomeHeight * k * gl;

            // Rune seal on the floor.
            var sealInk = Spawn("Grace ward seal ink", runeRing, ground, 4.4f * k, Alpha(bronze, .8f), total, false, 25);
            GraceFlat(sealInk, .32f); sealInk.spin = 45; sealInk.fadeIn = .06f; sealInk.startScale = .55f; sealInk.endScale = 1; sealInk.ease = 4; sealInk.hold = hold; sealInk.fadePower = 1.5f;
            var seal = RuneCircle(ground, 4.2f * k, Alpha(bright, .95f), total, 45, true); seal.hold = hold;
            var floor = GroundGlow(ground, 5 * k, Alpha(gold, .45f), total); floor.hold = hold;
            Emitter(floor, 18 * k, 1.7f * k, (owner, p) => GraceRise(GraceOnGround(owner, p), Chance(.4f) ? Warm : gold, k * .9f));

            // The dome rises out of the floor during the wind-up: a bronze lattice behind the actors, an amber lattice and
            // gold light in front. It hands over to the shield burst right after the release.
            float domeLife = w + .1f;
            for (int layer = 0; layer < 3; layer++)
            {
                var shell = layer == 0 ? GraceDomeLayer(ground, domeW * 1.03f, domeH * 1.03f, Alpha(bronze, .5f), domeLife, false, 26)
                    : layer == 1 ? GraceDomeLayer(ground, domeW, domeH, Alpha(pigment, .36f), domeLife, false, 119)
                    : GraceDomeLayer(ground, domeW, domeH, Alpha(bright, .34f), domeLife, true, 121);
                shell.axisStart = new Vector2(.8f, .03f); shell.axisEnd = Vector2.one; shell.ease = 2.4f; shell.fadeIn = .06f; shell.hold = w / domeLife; shell.fadePower = 2.2f;
            }

            // Bevelled ward plates sweep in from all around and slam onto the dome surface exactly at the release,
            // where each locks with a white-gold flash.
            // They start close round the dome (not scattered over the sky) and each carries a warm glowing rim, so they read
            // as plates of light homing onto the shell.
            int plates = Budget(8);
            Color plateTone = GraceDeep(main, .95f, .97f, -.03f);
            for (int i = 0; i < plates; i++)
            {
                float a = Mathf.Lerp(16, 164, (i + .5f) / plates) + Range(-5, 5);
                Vector2 normal = Direction(a), at = ground + new Vector2(normal.x * domeW * .5f, normal.y * domeH) * .96f;
                float size = Range(.85f, 1.05f) * k, from = a + Range(-25, 25), sweep = (i % 2 == 0 ? 1 : -1) * Range(50, 90), radius = Range(1.1f, 1.7f) * k;
                float tilt = Range(-40, 40), spin = Range(-260, 260);
                var plate = Spawn("Grace ward plate", GracePlate, at, size, plateTone, w, false, 120);
                var rim = Spawn("Grace ward plate glow", GracePlate, at, size * 1.12f, Alpha(Warm, .45f), w, true, 121);
                foreach (var clip in new[] { plate, rim })
                {
                    clip.orbitCenter = at; clip.orbitAngle = from; clip.orbit = sweep; clip.orbitRadius = radius; clip.orbitRadiusEnd = 0; clip.orbitPower = 1.3f;
                    clip.angle = tilt; clip.spin = spin; clip.fadeIn = .08f; clip.hold = .85f; clip.fadePower = 1.5f; clip.startScale = .6f; clip.endScale = 1; clip.ease = 1;
                }
                var locked = GraceLater(Spawn("Grace ward plate lock", GracePlate, at, size * 1.05f, Alpha(Warm, .75f), .24f, true, 131), release);
                locked.ramp = Ramp(Color.white, bright); locked.startScale = 1.45f; locked.endScale = .9f; locked.ease = 4; locked.fadePower = 1.8f;
                // The locked plate stays a moment in amber under the flash, so the shell reads on pale skies too.
                var seated = GraceLater(Spawn("Grace ward plate seated", GracePlate, at, size * 1.08f, plateTone, .32f, false, 120), release);
                seated.startScale = 1.25f; seated.endScale = 1; seated.ease = 5; seated.hold = .35f; seated.fadePower = 1.4f;
            }

            // Glyph runes orbit the caster, close in and lock into place at the release.
            int glyphs = 4; float turn = 320, lockRadius = 1.8f * k;
            for (int i = 0; i < glyphs; i++)
            {
                float start = 45 + i * 360f / glyphs; Sprite glyph = GraceGlyphs[i % GraceGlyphs.Length];
                Vector2 at = body + Direction(start + w * turn) * lockRadius;
                for (int layer = 0; layer < 2; layer++)
                {
                    bool lit = layer == 1; float size = (lit ? 1 : 1.15f) * k;
                    var rune = Spawn("Grace ward rune", glyph, body, size, lit ? Alpha(bright, .95f) : Alpha(GraceDeep(main, .55f, .9f, -.04f), .9f), w, lit, lit ? 130 : 119);
                    rune.orbitCenter = body; rune.orbitAngle = start; rune.orbit = turn; rune.orbitRadius = 2.6f * k; rune.orbitRadiusEnd = lockRadius; rune.orbitPower = .55f;
                    rune.fadeIn = .08f; rune.hold = .92f; rune.fadePower = 2;
                }
                var locked = GraceLater(Spawn("Grace ward rune lock", glyph, at, 1.05f * k, Alpha(Warm, .8f), .38f, true, 131), release);
                locked.ramp = Ramp(Warm, bright); locked.startScale = 1.6f; locked.endScale = 1; locked.ease = 5; locked.hold = .3f; locked.fadePower = 1.6f;
            }
            Converge(body, 8, 2.7f * k, bright, w, .3f);
            if (grade >= 2)
            {
                // Level two: a second, wider rune ring turns the other way on the floor.
                var outerInk = Spawn("Grace ward outer ring ink", runeRing, ground, 5.6f * k, Alpha(bronze, .7f), total, false, 24); GraceFlat(outerInk, .32f);
                var outer = RuneCircle(ground, 5.4f * k, Alpha(gold, .8f), total, -35, true); outer.hold = hold;
                outerInk.spin = -35; outerInk.fadeIn = .06f; outerInk.startScale = .55f; outerInk.endScale = 1; outerInk.ease = 4; outerInk.hold = hold; outerInk.fadePower = 1.5f;
            }
            if (grade >= 3)
            {
                // Awakened (the ward will burst when it breaks): four more gold-white glyphs circle the other way, wider, and lock
                // between the first four.
                for (int i = 0; i < glyphs; i++)
                {
                    float start = i * 360f / glyphs; Sprite glyph = GraceGlyphs[(i + 2) % GraceGlyphs.Length];
                    float lockAt = start - w * turn;
                    Vector2 at = body + Direction(lockAt) * lockRadius * 1.3f;
                    for (int layer = 0; layer < 2; layer++)
                    {
                        bool lit = layer == 1; float size = (lit ? .9f : 1.05f) * k;
                        var rune = Spawn("Grace ward outer rune", glyph, body, size, lit ? Alpha(GraceAwake, .95f) : Alpha(GraceUmber, .85f), w, lit, lit ? 130 : 119);
                        rune.orbitCenter = body; rune.orbitAngle = start; rune.orbit = -turn; rune.orbitRadius = 3.1f * k; rune.orbitRadiusEnd = lockRadius * 1.3f; rune.orbitPower = .55f;
                        rune.fadeIn = .08f; rune.hold = .92f; rune.fadePower = 2;
                    }
                    var locked = GraceLater(Spawn("Grace ward outer rune lock", glyph, at, .95f * k, Alpha(GraceAwake, .85f), .4f, true, 131), release);
                    locked.startScale = 1.6f; locked.endScale = 1; locked.ease = 5; locked.hold = .3f; locked.fadePower = 1.6f;
                }
            }

            // Release: a crown glint on the apex and rings (reward and passive wards end here; skills add the burst).
            var crown = GraceLater(Glint(ground + Vector2.up * domeH * .97f, 2.8f * k, Color.white, .34f), release); crown.scale.y *= 1.3f;
            GraceLater(Wave(body, 4.8f * k, Alpha(bright, .5f), .35f, 3), release);
            GraceLater(GraceRing(body, 4.4f * k, Alpha(pigment, .8f), .33f, 3, false, 119), release);
            GraceLater(GroundWave(ground, 5.6f * k, Alpha(gold, .85f), .4f), release);
        }

        private void ImpactShieldPulse(Vector2 pos, float radius, Color tint, int facing)
        {
            // A wide burst without a facing is a level-three ward breaking (radius 2.6); casts and passives draw the dome (1.1 to 1.5).
            if (facing == 0 && radius >= 2) { GraceShieldShatter(pos, radius, tint); return; }
            int grade = Grade;
            float k = Mathf.Clamp(radius / 1.2f, .7f, 1.8f) * GraceLevel(1.07f, 1.14f);
            Color main = GraceVivid(tint, GoldLight), gold = Color.Lerp(main, GoldLight, .15f), bright = Color.Lerp(gold, Warm, .5f);
            Color bronze = GraceDeep(main, .3f, .68f, -.04f), pigment = GraceDeep(main, .78f, .92f, -.035f);
            Vector2 ground = pos + Vector2.down, body = pos + Vector2.up * .35f;
            float domeW = GraceDomeWidth * k, domeH = GraceDomeHeight * k;
            Vector2 apex = ground + Vector2.up * domeH * .96f;

            // Peak: a white-hot heart, a short gold bloom, impact lines and a crown glint on the dome apex.
            var shade = Underlay(body, 5 * k, .24f, .45f); shade.hold = .2f;
            var heart = Spawn("Grace ward heart", core, pos, 1.1f * k, Color.white, .1f, true, 133); heart.startScale = .5f; heart.endScale = 1.7f; heart.ease = 2.5f; heart.fadePower = 1.5f;
            var bloom = Flash(body, 3 * k, Alpha(bright, .25f), .14f); bloom.ramp = Ramp(Warm, gold); bloom.startScale = .5f; bloom.endScale = 1.2f; bloom.ease = 3;
            var amber = Spawn("Grace ward pigment bloom", glow, body, 4.8f * k, Alpha(pigment, .35f), .3f, false, 118); amber.startScale = .5f; amber.endScale = 1.15f; amber.ease = 3; amber.fadePower = 1.5f;
            GracePigment(ImpactLines(body, 7.6f * k, Alpha(pigment, .8f), .32f), 119);
            ImpactLines(body, 6.6f * k, Alpha(bright, .45f), .26f);
            var crown = Glint(apex, 3.2f * k, Color.white, .4f); crown.scale.y *= 1.3f;

            // A beacon spear of light shoots up from the apex.
            var beaconTint = GraceColumn(GraceShaft, apex, .9f * k, 4.6f * k, Alpha(pigment, .6f), .5f, false, 26); beaconTint.hold = .15f; beaconTint.fadePower = 1.6f;
            var beacon = GraceColumn(pillar, apex, 1.1f * k, 5.6f * k, Alpha(bright, .85f), .45f, true, 128); beacon.hold = .1f; beacon.fadePower = 1.6f;

            // A golden aegis crest flares open behind the caster.
            var crestInk = Spawn("Grace ward crest ink", runeRing, body + Vector2.up * .2f, 5 * k, Alpha(bronze, .6f), .8f, false, 25);
            crestInk.spin = 40; crestInk.startScale = .5f; crestInk.endScale = 1.12f; crestInk.ease = 4; crestInk.hold = .25f; crestInk.fadePower = 1.5f;
            var crest = Spawn("Grace ward crest", runeRing, body + Vector2.up * .2f, 4.9f * k, Alpha(bright, .6f), .75f, true, 27);
            crest.spin = 40; crest.startScale = .5f; crest.endScale = 1.12f; crest.ease = 4; crest.hold = .2f; crest.fadePower = 1.6f;

            // The dome: bronze lattice behind the actors, an amber pigment lattice and gold light in front, a quick snap
            // flash, then a slow fade.
            var domeInk = GraceDomeLayer(ground, domeW * 1.03f, domeH * 1.03f, Alpha(bronze, .6f), 1.5f, false, 26);
            domeInk.startScale = .9f; domeInk.endScale = 1; domeInk.ease = 6; domeInk.hold = .45f; domeInk.fadePower = 1.4f;
            var domeTint = GraceDomeLayer(ground, domeW, domeH, Alpha(pigment, .44f), 1.4f, false, 119);
            domeTint.startScale = .9f; domeTint.endScale = 1; domeTint.ease = 6; domeTint.hold = .4f; domeTint.fadePower = 1.5f;
            var dome = GraceDomeLayer(ground, domeW, domeH, Alpha(bright, .32f), 1.4f, true, 121);
            dome.startScale = .9f; dome.endScale = 1; dome.ease = 6; dome.hold = .35f; dome.fadePower = 1.6f; dome.pulse = .025f;
            var snap = GraceDomeLayer(ground, domeW, domeH, Alpha(Color.white, .3f), .1f, true, 132);
            snap.startScale = 1.15f; snap.endScale = 1; snap.ease = 5; snap.fadePower = 1.8f;

            // A scan of light runs up the dome: its cells flash from the floor to the apex.
            int cells = Budget(12);
            for (int i = 0; i < cells; i++)
            {
                float a = Range(18, 162) * Mathf.Deg2Rad, rr = Range(.72f, .98f);
                Vector2 at = ground + new Vector2(Mathf.Cos(a) * domeW * .5f * rr, Mathf.Sin(a) * domeH * rr);
                var cell = GraceLater(Spawn("Grace ward cell", GracePlate, at, Range(.45f, .65f) * k, Alpha(bright, .85f), .24f, true, 130), .04f + (at.y - ground.y) / domeH * .32f);
                cell.ramp = Ramp(Color.white, gold); cell.startScale = .5f; cell.endScale = 1.1f; cell.ease = 3; cell.fadePower = 1.5f;
            }

            // Hexagonal ripples and rings run outward from the dome; dust is shoved off its base.
            var ripple = Spawn("Grace ward ripple", hex, body, domeW * .95f, Alpha(bright, .3f), .42f, true, 130); ripple.startScale = .75f; ripple.endScale = 1.6f; ripple.ease = 3; ripple.fadePower = 1.5f; ripple.spin = 20;
            var tinted = Spawn("Grace ward ripple pigment", hex, body, domeW * .95f, Alpha(pigment, .55f), .45f, false, 119); tinted.startScale = .8f; tinted.endScale = 1.75f; tinted.ease = 3; tinted.fadePower = 1.5f; tinted.spin = -20;
            Wave(body, 7 * k, Alpha(bright, .6f), .42f, 3);
            GraceRing(body, 6.4f * k, Alpha(pigment, .8f), .4f, 3, false, 119);
            GroundWave(ground, 8.2f * k, Alpha(gold, .9f), .5f);
            var inkRing = GraceRing(ground, 7.6f * k, Alpha(bronze, .7f), .5f, 3, false, 24); GraceFlat(inkRing, .34f);
            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 0 ? 1 : -1;
                var dust = Puff(ground + new Vector2(side * domeW * Range(.4f, .5f), .1f), Range(.6f, .9f) * k, DustTone, new Vector2(side * Range(2.5f, 4.5f) * k, Range(.3f, .8f)), Range(.45f, .65f), 118);
                dust.gravity = -.3f; dust.endScale = 2.1f;
            }

            // Light shards and sparks burst off the dome surface.
            int shards = Budget(Mathf.RoundToInt(18 * k));
            for (int i = 0; i < shards; i++)
            {
                float angle = Mathf.Lerp(12, 168, (i + Range(.1f, .9f)) / shards);
                Vector2 normal = Direction(angle), at = ground + new Vector2(normal.x * domeW * .5f, normal.y * domeH);
                bool lit = i % 3 == 0;
                var piece = Spawn("Grace ward shard", GraceFacet, at, Range(.28f, .5f) * k, lit ? Alpha(bright, .95f) : Alpha(Color.Lerp(pigment, bright, Range(0, .35f)), .95f), Range(.55f, .9f), lit, lit ? 129 : 120);
                piece.velocity = normal * Range(4, 8) * k + Vector2.up; piece.gravity = 10; piece.drag = 1.2f; piece.spin = Range(-600, 600); piece.angle = Range(0, 360); piece.hold = .35f; piece.fadePower = 1.3f;
            }
            for (int i = 0; i < 10; i++)
            {
                Vector2 normal = Direction(Range(10, 170)), at = ground + new Vector2(normal.x * domeW * .5f, normal.y * domeH);
                Spark(at, normal * Range(5, 9) * k, i % 2 == 0 ? Color.white : bright, Range(.25f, .42f), Range(.25f, .4f), 6, 3);
            }

            // The seal and a warm floor glow linger; motes drift up the dome walls; glints twinkle on its surface.
            var sealInk = Spawn("Grace ward seal ink", runeRing, ground, 5 * k, Alpha(bronze, .8f), 1.2f, false, 25);
            GraceFlat(sealInk, .32f); sealInk.spin = 30; sealInk.fadeIn = .03f; sealInk.startScale = .7f; sealInk.endScale = 1; sealInk.ease = 6; sealInk.hold = .4f; sealInk.fadePower = 1.4f;
            RuneCircle(ground, 4.8f * k, Alpha(bright, .9f), 1.2f, 30, true);
            GroundGlow(ground, 6 * k, Alpha(gold, .3f), 1.2f);
            for (int i = 0; i < 8; i++)
            {
                Vector2 normal = Direction(Range(15, 165));
                GraceLater(GraceRise(ground + new Vector2(normal.x * domeW * .48f, normal.y * domeH * .95f), i % 2 == 0 ? Warm : gold, k), Range(.05f, .4f));
            }
            for (int i = 0; i < 5; i++)
            {
                Vector2 normal = Direction(Range(25, 155));
                GraceLater(Glint(ground + new Vector2(normal.x * domeW * .5f, normal.y * domeH), Range(.8f, 1.2f) * k, Alpha(Warm, .95f), .28f, Range(0, 45)), Range(.12f, .8f));
            }
            if (grade < 2) return;
            // Level two: a thicker shell, a second gold rim glowing on the dome as it holds, over an umber twin so the doubled
            // shell reads on pale skies too.
            var rimInk = GraceDomeLayer(ground, domeW * 1.05f, domeH * 1.05f, Alpha(GraceUmber, .4f), 1.5f, false, 118);
            var rim = GraceDomeLayer(ground, domeW * 1.03f, domeH * 1.03f, Alpha(gold, .35f), 1.5f, true, 122); rim.pulse = .03f;
            foreach (var clip in new[] { rimInk, rim }) { clip.startScale = .92f; clip.endScale = 1; clip.ease = 6; clip.hold = .45f; clip.fadePower = 1.5f; }
            if (grade < 3) return;
            // Awakened (the ward bursts when it breaks): a second gold-white shell stands just outside the first, a white-gold star
            // pulses on the apex, and cracks of light run across the surface and hold there: the ward is primed to burst.
            var outerInk = GraceDomeLayer(ground, domeW * 1.11f, domeH * 1.11f, Alpha(GraceUmber, .35f), 1.6f, false, 117);
            var outer = GraceDomeLayer(ground, domeW * 1.08f, domeH * 1.08f, Alpha(GraceAwake, .25f), 1.6f, true, 123); outer.pulse = .04f;
            foreach (var clip in new[] { outerInk, outer }) { clip.startScale = .85f; clip.endScale = 1; clip.ease = 5; clip.fadeIn = .08f; clip.hold = .5f; clip.fadePower = 1.4f; }
            var primed = GraceLater(Glint(apex + Vector2.up * .35f * k, 2.2f * k, Alpha(GraceAwake, 1), 1.3f, 0), .12f);
            primed.spin = 0; primed.startScale = .3f; primed.endScale = 1; primed.ease = 4; primed.pulse = .12f; primed.hold = .6f; primed.fadePower = 1.4f;
            var beaconGlow = GraceLater(Spawn("Grace ward primed glow", glow, apex + Vector2.up * .35f * k, 1.6f * k, Alpha(GraceAwake, .55f), 1.3f, true, 130), .12f);
            beaconGlow.pulse = .15f; beaconGlow.hold = .6f; beaconGlow.fadePower = 1.4f;
            for (int i = 0; i < 6; i++)
            {
                float a0 = (30 + i * 24 + Range(-8, 8)) * Mathf.Deg2Rad, a1 = a0 + Range(-.3f, .3f);
                Vector2 p0 = ground + new Vector2(Mathf.Cos(a0) * domeW * .5f, Mathf.Sin(a0) * domeH) * .98f, p1 = ground + new Vector2(Mathf.Cos(a1) * domeW * .34f, Mathf.Sin(a1) * domeH * .7f);
                float delay = .12f + i * .06f;
                var veinInk = GraceLater(GraceInkLine(p0, p1, .3f, Alpha(GraceUmber, .75f), 1.2f, 120), delay);
                var vein = GraceLater(Line(p0, p1, .16f, Alpha(GraceAwake, 1), 1.2f, 131), delay);
                foreach (var clip in new[] { veinInk, vein }) { clip.axisStart = new Vector2(.1f, 1); clip.axisEnd = Vector2.one; clip.ease = 6; clip.hold = .6f; clip.fadePower = 1.5f; }
                vein.flicker = .3f;
            }
        }

        /// <summary>A level-three ward bursting as it breaks (<paramref name="pos"/> is 0.8 above the hero's feet): the dome flashes
        /// white and cracks, flies apart in amber plates and light shards, and a shock runs along the floor to the burst's reach.</summary>
        private void GraceShieldShatter(Vector2 pos, float radius, Color tint)
        {
            float R = Mathf.Clamp(radius, 1.5f, 4.5f), k = Mathf.Clamp(R / 2.6f, .7f, 1.5f);
            Color main = GraceVivid(tint, GoldLight), gold = Color.Lerp(main, GoldLight, .15f), bright = Color.Lerp(gold, Warm, .5f);
            Color bronze = GraceDeep(main, .3f, .68f, -.04f), pigment = GraceDeep(main, .78f, .92f, -.035f), plateTone = GraceDeep(main, .95f, .97f, -.03f);
            Vector2 feet = pos + Vector2.down * .8f, body = feet + Vector2.up * 1.1f;
            float domeW = GraceDomeWidth, domeH = GraceDomeHeight;
            var shade = Underlay(body, R * 2.2f, .3f, .4f); shade.hold = .2f;
            // The dome's last moment: a white flash of the whole shell that swells as it gives way, over a bronze copy.
            var crackFlash = GraceDomeLayer(feet, domeW, domeH, Alpha(Color.white, .6f), .16f, true, 132); crackFlash.startScale = 1; crackFlash.endScale = 1.12f; crackFlash.ease = 3; crackFlash.fadePower = 1.4f;
            var shellInk = GraceDomeLayer(feet, domeW * 1.02f, domeH * 1.02f, Alpha(bronze, .6f), .22f, false, 119); shellInk.startScale = 1; shellInk.endScale = 1.2f; shellInk.ease = 3; shellInk.fadePower = 1.5f;
            // Cracks race from the heart to the shell.
            for (int i = 0; i < 7; i++)
            {
                float a = (18 + i * 24 + Range(-8, 8)) * Mathf.Deg2Rad;
                Vector2 rim = feet + new Vector2(Mathf.Cos(a) * domeW * .5f, Mathf.Sin(a) * domeH) * .97f, mid = Vector2.Lerp(body, rim, .5f) + Inside(.3f);
                foreach (var seg in new[] { new[] { body, mid }, new[] { mid, rim } })
                {
                    var crackInk = GraceInkLine(seg[0], seg[1], .2f, Alpha(bronze, .8f), .2f, 120); crackInk.fadePower = 1.4f;
                    var crackLight = Line(seg[0], seg[1], .09f, Alpha(Color.Lerp(Warm, Color.white, .5f), 1), .16f, 132, 1.1f); crackLight.fadePower = 1.4f;
                }
            }
            var heart = Spawn("Grace ward break heart", core, body, 1.3f * k, Color.white, .12f, true, 133); heart.startScale = .5f; heart.endScale = 1.8f; heart.ease = 2.5f; heart.fadePower = 1.5f;
            var bloom = Flash(body, 3.2f * k, Alpha(bright, .8f), .24f); bloom.ramp = Ramp(Color.white, gold);
            GracePigment(ImpactLines(body, R * 2.8f, Alpha(pigment, .8f), .3f), 119);
            ImpactLines(body, R * 2.5f, Alpha(bright, .75f), .26f);
            // The burst's reach: gold rings on the floor whose rims land on the radius, over bronze ink.
            float reach = R * 2 / .74f;
            GroundWave(feet + Vector2.up * .05f, reach, Alpha(gold, .95f), .45f);
            var reachInk = GraceRing(feet, reach * 1.02f, Alpha(bronze, .7f), .5f, 3, false, 24); GraceFlat(reachInk, .34f);
            var floorFlash = Spawn("Grace ward break floor", glow, feet + Vector2.up * .05f, reach * .8f, Alpha(bright, .5f), .35f, true, 26);
            floorFlash.scale.y *= .3f; floorFlash.root.transform.localScale = floorFlash.scale; floorFlash.startScale = .3f; floorFlash.endScale = 1; floorFlash.ease = 3; floorFlash.fadePower = 1.5f;
            // The shell flies apart: amber plates tumble out from where they sat on the dome, with light shards among them.
            int plates = Budget(10);
            for (int i = 0; i < plates; i++)
            {
                float a = Mathf.Lerp(14, 166, (i + .5f) / plates) + Range(-6, 6);
                Vector2 normal = Direction(a), at = feet + new Vector2(normal.x * domeW * .5f, normal.y * domeH) * .95f;
                bool lit = i % 3 == 0;
                var plate = Spawn("Grace ward plate shard", GracePlate, at, Range(.6f, .85f) * k, lit ? Alpha(Warm, .9f) : plateTone, Range(.5f, .7f), lit, lit ? 130 : 120);
                plate.velocity = normal * Range(3.5f, 5.5f) * k + Vector2.up * .8f; plate.gravity = 9; plate.drag = 1.6f; plate.spin = Range(-700, 700); plate.angle = Range(0, 360); plate.hold = .3f; plate.fadePower = 1.3f;
            }
            int facets = Budget(8);
            for (int i = 0; i < facets; i++)
            {
                Vector2 normal = Direction(Range(10, 170));
                var piece = Spawn("Grace ward facet", GraceFacet, body + normal * .4f, Range(.3f, .5f) * k, i % 2 == 0 ? Alpha(bright, .95f) : Alpha(pigment, .95f), Range(.45f, .7f), i % 2 == 0, i % 2 == 0 ? 129 : 120);
                piece.velocity = normal * Range(5, 8) * k; piece.gravity = 8; piece.drag = 1.4f; piece.spin = Range(-600, 600); piece.angle = Range(0, 360); piece.fadePower = 1.3f;
            }
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? 1 : -1;
                var dust = Puff(feet + new Vector2(side * Range(.4f, 1.2f), .12f), Range(.6f, .9f) * k, DustTone, new Vector2(side * Range(3, 5) * k, Range(.3f, .8f)), Range(.5f, .7f), 118);
                dust.gravity = -.3f; dust.endScale = 2.2f;
            }
            for (int i = 0; i < 3; i++) GraceLater(GraceRise(feet + new Vector2(Range(-1, 1) * R * .6f, .1f), i % 2 == 0 ? Warm : gold, k), Range(.1f, .35f));
        }

        /// <summary>A shield soaking up a hit on the hero; frequent, so it stays tiny (9 clips, 0.24 s).</summary>
        public void ShieldBlock(Vector2 pos, Color tint)
        {
            EnsureTextures();
            Color gold = GraceVivid(tint, GoldLight), bright = Color.Lerp(gold, Warm, .5f), pigment = GraceDeep(gold, .85f, .55f, -.03f);
            var lattice = Spawn("Grace block lattice", hex, pos, 2.3f, Alpha(bright, .95f), .22f, true, 130); lattice.startScale = .82f; lattice.endScale = 1.15f; lattice.ease = 3; lattice.fadePower = 1.6f;
            var ink = Spawn("Grace block pigment", hex, pos, 2.4f, pigment, .2f, false, 119); ink.startScale = .82f; ink.endScale = 1.15f; ink.ease = 3; ink.fadePower = 1.6f;
            Vector2 normal = Direction(Range(20, 160)), hit = pos + normal * 1.05f;
            var flash = Spawn("Grace block flash", glow, hit, .95f, new Color(1, 1, 1, .9f), .1f, true, 132); flash.endScale = 1.7f; flash.ease = 1;
            Glint(hit, 1.3f, Color.white, .18f, Range(0, 45));
            Wave(hit, 1.15f, Alpha(bright, .9f), .16f, 3);
            for (int i = 0; i < 4; i++) Spark(hit, normal * Range(3, 6) + Direction(Range(0, 360)) * 2, i % 2 == 0 ? Color.white : bright, Range(.2f, .3f), Range(.16f, .24f), 6, 3);
        }

        // ---------------------------------------------------------------- life drain

        private void CastLifeDrain(CastInfo c)
        {
            float k = Mathf.Clamp(c.size / 1.43f, .8f, 1.6f), w = c.windup, release = Mathf.Max(0, w - .02f);
            Color life = Color.Lerp(GraceVivid(c.tint, LifeLight), LifeLight, .3f);
            Vector2 hand = c.pos + new Vector2(c.facing * .8f, .1f), ground = c.pos + Vector2.down;
            if (w < .2f)
            {
                // An instant echo: the drain itself follows at once, so the echo only flicks a small vortex open.
                var curl = Anchored(c.anchor, hand, "Grace drain echo swirl", swirl, 1.9f * k, Alpha(GraceCrimson, .8f), w + .12f, 125);
                curl.spin = -680; curl.startScale = .3f; curl.endScale = 1; curl.ease = 2; curl.fadeIn = .03f; curl.hold = .6f; curl.fadePower = 1.6f;
                var pit = Anchored(c.anchor, hand, "Grace drain echo core", core, .55f * k, GraceVoid, w + .1f, 127, false);
                pit.startScale = .3f; pit.endScale = 1; pit.ease = 2; pit.fadeIn = .03f; pit.hold = .6f; pit.fadePower = 1.6f;
                Converge(hand, 5, 1.8f * k, life, w, .5f);
                return;
            }
            float total = w + .6f, hold = w / total;
            // Higher levels open a bigger vortex (more souls follow from the particle budget).
            int grade = Grade; k *= GraceLevel(1.1f, 1.2f);

            // A black-crimson vortex opens in the hand: dark smoke arms turning one way, crimson and life light the other,
            // around a black core with a crimson rim.
            var shadow = Underlay(hand, 4.2f * k, .5f, total); shadow.hold = hold; shadow.fadePower = 1.5f;
            var abyss = Anchored(c.anchor, hand, "Grace drain abyss", swirl, 4.2f * k, GraceVoid, total, 28, false);
            abyss.spin = -480; abyss.angle = Range(0, 360); abyss.startScale = .25f; abyss.endScale = 1; abyss.ease = 2.5f; abyss.fadeIn = .05f; abyss.hold = hold; abyss.fadePower = 1.6f;
            var veil = Anchored(c.anchor, hand, "Grace drain blood arms", swirl, 3.5f * k, GraceBlood, total, 29, false);
            veil.spin = -560; veil.angle = Range(0, 360); veil.startScale = .25f; veil.endScale = 1; veil.ease = 2.5f; veil.fadeIn = .05f; veil.hold = hold; veil.fadePower = 1.6f;
            var crimson = Anchored(c.anchor, hand, "Grace drain crimson swirl", swirl, 2.9f * k, Alpha(GraceCrimson, .75f), total, 125);
            crimson.spin = -680; crimson.angle = Range(0, 360); crimson.startScale = .25f; crimson.endScale = 1; crimson.ease = 2.5f; crimson.fadeIn = .05f; crimson.hold = hold; crimson.fadePower = 1.6f;
            var spirit = Anchored(c.anchor, hand, "Grace drain life swirl", swirl, 1.8f * k, Alpha(life, .8f), total, 126);
            spirit.spin = 560; spirit.startScale = .3f; spirit.endScale = 1; spirit.ease = 2.5f; spirit.fadeIn = .05f; spirit.hold = hold; spirit.fadePower = 1.6f;
            var eye = Anchored(c.anchor, hand, "Grace drain core", core, .8f * k, GraceVoid, total, 127, false);
            eye.startScale = .3f; eye.endScale = 1; eye.ease = 2; eye.fadeIn = .05f; eye.hold = hold; eye.fadePower = 1.6f; eye.pulse = .07f;
            var rim = Anchored(c.anchor, hand, "Grace drain rim", ring, 1.3f * k, Alpha(GraceCrimson, .95f), total, 128);
            rim.startScale = .3f; rim.endScale = 1; rim.ease = 2; rim.fadeIn = .05f; rim.hold = hold; rim.fadePower = 1.6f; rim.pulse = .09f;

            // Blood smoke, crimson souls and life motes spiral into the vortex.
            int fumes = Budget(6);
            for (int i = 0; i < fumes; i++)
            {
                var fume = Puff(hand, Range(.9f, 1.3f) * k, Alpha(GraceBlood, .7f), Vector2.zero, w * Range(.8f, 1), 116);
                fume.orbitCenter = hand; fume.orbitAngle = i * 360f / fumes + Range(-15, 15); fume.orbit = -Range(300, 420);
                fume.orbitRadius = Range(2, 2.8f) * k; fume.orbitRadiusEnd = .1f; fume.orbitPower = 1.3f;
                fume.startScale = 1; fume.endScale = .3f; fume.fadeIn = .1f; fume.hold = .6f; fume.fadePower = 1.4f;
            }
            Converge(hand, 10, 3 * k, life, w, .55f);
            int souls = Budget(10);
            for (int i = 0; i < souls; i++)
            {
                bool dark = i % 2 == 0;
                var soul = Spawn("Grace drain soul", GraceSoul, hand, Range(.9f, 1.3f) * k, dark ? GraceBlood : Alpha(GraceCrimson, .95f), w * Range(.75f, 1), !dark, dark ? 118 : 127);
                soul.orbitCenter = hand; soul.orbitAngle = Range(0, 360); soul.orbit = -Range(260, 380); soul.orbitRadius = Range(2.2f, 3.2f) * k; soul.orbitRadiusEnd = 0; soul.orbitPower = 1.5f;
                soul.alignVelocity = true; soul.fadeIn = .08f; soul.hold = .75f; soul.endScale = .5f; soul.ease = 1;
            }

            // A crimson sigil under the caster.
            var sigInk = Spawn("Grace drain sigil ink", sigil, ground, 4.2f * k, GraceBlood, total, false, 25);
            GraceFlat(sigInk, .34f); sigInk.spin = -40; sigInk.fadeIn = .06f; sigInk.startScale = .6f; sigInk.endScale = 1; sigInk.ease = 3; sigInk.hold = hold; sigInk.fadePower = 1.5f;
            var sig = GroundSigil(ground, 4 * k, Alpha(GraceCrimson, .85f), total, -40); sig.hold = hold;

            // A blood sigil turns behind the caster (behind the actors, so the hero stays readable).
            Vector2 back = c.pos + Vector2.up * .25f;
            var ritualInk = Spawn("Grace drain ritual ink", sigil, back, 3.6f * k, Alpha(GraceBlood, .7f), total, false, 26);
            ritualInk.spin = 60; ritualInk.startScale = .3f; ritualInk.endScale = 1; ritualInk.ease = 2; ritualInk.fadeIn = .06f; ritualInk.hold = hold; ritualInk.fadePower = 1.6f;
            var ritual = Spawn("Grace drain ritual", sigil, back, 3.5f * k, Alpha(GraceCrimson, .75f), total, true, 27);
            ritual.spin = 60; ritual.startScale = .3f; ritual.endScale = 1; ritual.ease = 2; ritual.fadeIn = .06f; ritual.hold = hold; ritual.fadePower = 1.6f;

            // The victim is branded with a turning crimson rune and starts to bleed light toward the hand.
            Vector2 mark = c.target + Vector2.down * .25f, toHand = hand - mark;
            if (toHand.magnitude > 1)
            {
                // Two dark tendrils reach out of the vortex and hook the victim.
                Vector2 reach = -toHand, normal = new Vector2(-reach.y, reach.x) / reach.magnitude;
                for (int strand = 0; strand < 2; strand++)
                {
                    Vector2 previous = hand; float sign = strand == 0 ? 1 : -1, phase = Range(0, 6.28f);
                    for (int i = 1; i <= 6; i++)
                    {
                        float t = i / 6f, delay = t * w * .55f;
                        Vector2 point = hand + reach * t + normal * sign * Mathf.Sin(t * Mathf.PI * 2 + phase) * .38f * Mathf.Sin(t * Mathf.PI);
                        var tendril = GraceLater(GraceInkLine(previous, point, .26f, GraceBlood, w - delay + .25f, 117), delay); tendril.hold = .6f; tendril.fadePower = 1.4f;
                        var vein = GraceLater(Line(previous, point, .1f, Alpha(GraceCrimson, .9f), w - delay + .2f, 127, 1.1f), delay); vein.hold = .6f; vein.flicker = .25f;
                        previous = point;
                    }
                }
                float brandLife = w + .3f;
                var brandShade = Underlay(mark, 2.8f * k, .35f, brandLife); brandShade.hold = .6f;
                var brandInk = Spawn("Grace drain brand ink", runeRing, mark, 2.5f * k, GraceBlood, brandLife, false, 117);
                brandInk.spin = 220; brandInk.fadeIn = .08f; brandInk.startScale = .6f; brandInk.endScale = 1; brandInk.ease = 4; brandInk.hold = .5f; brandInk.fadePower = 1.4f;
                var brand = RuneCircle(mark, 2.3f * k, Alpha(GraceCrimson, .95f), brandLife, 220, false, 126); brand.hold = .5f;
                for (int i = 0; i < 6; i++)
                {
                    bool green = i % 2 == 0;
                    var seep = GraceLater(Spawn("Grace drain seep", GraceSoul, mark + Inside(.35f), Range(.7f, 1), green ? Alpha(life, .9f) : GraceBlood, w * .6f, green, green ? 127 : 118), Range(.04f, w * .4f));
                    seep.velocity = toHand / (w * .6f) * Range(.45f, .75f); seep.alignVelocity = true; seep.fadeIn = .06f; seep.endScale = .6f; seep.ease = 1;
                }
            }

            if (grade >= 3)
            {
                // Awakened (the drain heals twice as much): a gold-white ring of life turns round the vortex's rim.
                var crown = Anchored(c.anchor, hand, "Grace drain awakened rim", runeRing, 2.4f * k, Alpha(Color.Lerp(GraceAwake, LifeLight, .3f), .9f), total, 129);
                var crownInk = Anchored(c.anchor, hand, "Grace drain awakened rim ink", runeRing, 2.5f * k, GraceUmber, total, 117, false);
                foreach (var clip in new[] { crown, crownInk }) { clip.spin = 200; clip.startScale = .3f; clip.endScale = 1; clip.ease = 2.5f; clip.fadeIn = .08f; clip.hold = hold; clip.fadePower = 1.6f; }
            }

            // Release: the ring implodes, then the vortex snaps with a crimson flash (the drained life comes back later).
            var implode = GraceLater(Spawn("Grace drain implode", ring, hand, 4.4f * k, Alpha(GraceCrimson, .95f), .18f, true, 131), Mathf.Max(0, w - .16f));
            implode.startScale = 1; implode.endScale = .12f; implode.ease = 1.5f; implode.fadePower = .6f;
            var snap = GraceLater(Spawn("Grace drain snap", core, hand, .8f * k, Color.white, .12f, true, 133), release); snap.startScale = .4f; snap.endScale = 1.6f; snap.ease = 2.5f; snap.fadePower = 1.5f;
            var bloomOut = GraceLater(Flash(hand, 2.4f * k, Alpha(GraceCrimson, .8f), .26f), release); bloomOut.ramp = Ramp(Color.white, GraceCrimson);
            GraceLater(GracePigment(ImpactLines(hand, 4.6f * k, Alpha(GraceBlood, .85f), .26f), 118), release);
            GraceLater(ImpactLines(hand, 4 * k, Alpha(GraceCrimson, .85f), .24f), release);
        }

        private void ImpactLifeDrain(Vector2 pos, float radius, Color tint, int facing)
        {
            float k = Mathf.Clamp(radius * 1.35f, 1, 3);
            Color life = Color.Lerp(GraceVivid(tint, LifeLight), LifeLight, .3f), hot = Color.Lerp(life, Color.white, .45f);
            GraceRip(pos, k);
            // A ghost is torn upward out of the body, followed by wavering soul streaks.
            for (int layer = 0; layer < 2; layer++)
            {
                bool lit = layer == 1;
                var ghost = Spawn(lit ? "Grace torn ghost" : "Grace torn ghost ink", GraceSoul, pos + Vector2.up * .3f * k, (lit ? 1.7f : 2) * k, lit ? Alpha(hot, .9f) : GraceBlood, .5f, lit, lit ? 128 : 117);
                ghost.angle = 90; ghost.velocity = Vector2.up * 4 * k; ghost.drag = 2.5f;
                ghost.axisStart = new Vector2(.5f, 1.2f); ghost.axisEnd = new Vector2(1.5f, .75f); ghost.ease = 2; ghost.fadeIn = .03f; ghost.fadePower = 1.4f;
            }
            int souls = Budget(Mathf.RoundToInt(7 * k));
            for (int i = 0; i < souls; i++)
            {
                bool green = i % 3 != 0;
                var soul = GraceLater(Spawn("Grace torn soul", GraceSoul, pos + Inside(.3f * k), Range(.6f, .9f) * k, green ? Alpha(hot, .95f) : GraceBlood, Range(.5f, .8f), green, green ? 129 : 118), Range(0, .12f));
                soul.orbitCenter = soul.position; soul.velocity = new Vector2(Range(-1, 1), Range(3.5f, 5.5f)) * k; soul.drag = 1.2f;
                soul.orbitAngle = Range(0, 360); soul.orbit = (i % 2 == 0 ? 1 : -1) * Range(500, 700); soul.orbitRadius = .25f * k; soul.orbitRadiusEnd = .05f;
                soul.alignVelocity = true; soul.fadeIn = .05f; soul.hold = .3f; soul.fadePower = 1.4f; soul.endScale = .5f; soul.ease = 1;
            }
            var lift = Spawn("Grace soul glow", glow, pos + Vector2.up * .4f * k, 1.8f * k, Alpha(life, .7f), .6f, true, 124); lift.velocity = Vector2.up * 1.6f * k; lift.drag = 1.5f; lift.fadeIn = .05f; lift.fadePower = 1.4f;
            GraceLater(Wave(pos, 3.2f * k, Alpha(life, .8f), .36f, 3), .05f);
        }

        private void TrailLifeDrain(Vector2 from, Vector2 to, Color tint)
        {
            Vector2 delta = to - from; float length = delta.magnitude; if (length < .05f) return;
            Vector2 dir = delta / length, side = new Vector2(-dir.y, dir.x);
            Color life = Color.Lerp(GraceVivid(tint, LifeLight), LifeLight, .3f), hot = Color.Lerp(life, Color.white, .5f), pigment = GraceDeep(life, .55f, .85f);
            // Higher levels pull a thicker, wider river.
            int grade = Grade; float gl = GraceLevel(1.12f, 1.25f);
            float travel = Mathf.Clamp(length / 13, .15f, .42f), amp = Mathf.Clamp(length * .24f, .45f, 1.1f) * gl, turns = Mathf.Clamp(length / 3, .8f, 1.6f), flow = .7f;
            float strandLife = flow + .25f, angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // The victim: the soul is ripped out in a dark burst and its ghost is torn loose toward the caster; dark smoke
            // keeps bleeding from the wound.
            GraceRip(from, 1.35f);
            for (int layer = 0; layer < 2; layer++)
            {
                bool lit = layer == 1;
                var ghost = Spawn(lit ? "Grace torn spirit" : "Grace torn spirit ink", GraceSoul, from + dir * .4f, lit ? 2.2f : 2.6f, lit ? Alpha(hot, .9f) : GraceBlood, .42f, lit, lit ? 128 : 117);
                ghost.angle = angle; ghost.velocity = dir * Mathf.Min(length, 4) * 1.8f; ghost.drag = 2.2f;
                ghost.axisStart = new Vector2(.5f, 1.2f); ghost.axisEnd = new Vector2(1.7f, .7f); ghost.ease = 2; ghost.fadeIn = .03f; ghost.fadePower = 1.4f;
            }
            for (int i = 0; i < 2; i++)
            {
                var wound = GraceLater(Puff(from + Inside(.25f), Range(.8f, 1.1f), GraceBlood, new Vector2(Range(-.4f, .4f), Range(.6f, 1.2f)), Range(.6f, .8f), 115), .15f + i * .2f);
                wound.endScale = 2.2f; wound.drag = 1.5f;
            }

            // The soul river: a dark channel with a life-white spine, wound by a thick life-green strand and a crimson strand
            // (seamless flat-ended ribbons), revealed from the victim to the caster, holding while the souls flow, then fading.
            int segments = Mathf.Max(5, Budget(Mathf.Clamp(Mathf.CeilToInt(turns * 6), 6, 10)));
            for (int strand = 0; strand < 2; strand++)
            {
                Vector2 previous = GraceHelix(from, delta, side, 0, amp, turns, strand);
                for (int i = 1; i <= segments; i++)
                {
                    float t = i / (float)segments; Vector2 point = GraceHelix(from, delta, side, t, amp, turns, strand);
                    float delay = Mathf.Max(0, t - .5f / segments) * travel;
                    if (strand == 0)
                    {
                        var stain = GraceLater(GraceStrand(previous, point, .95f * gl, Alpha(pigment, .85f), strandLife, false, 119), delay); stain.hold = .45f; stain.fadePower = 1.3f;
                        var vein = GraceLater(GraceStrand(previous, point, .8f * gl, Alpha(life, .95f), strandLife, true, 126), delay); vein.hold = .45f; vein.fadePower = 1.4f; vein.flicker = .12f;
                    }
                    else
                    {
                        var ink = GraceLater(GraceStrand(previous, point, gl, GraceBlood, strandLife, false, 117), delay); ink.hold = .45f; ink.fadePower = 1.3f;
                        var vein = GraceLater(GraceStrand(previous, point, .7f * gl, Alpha(GraceCrimson, .95f), strandLife, true, 127), delay); vein.hold = .45f; vein.fadePower = 1.4f; vein.flicker = .12f;
                    }
                    previous = point;
                }
            }
            for (int i = 0; i < 3; i++)
            {
                Vector2 a = from + delta * (i / 3f), b = from + delta * ((i + 1) / 3f);
                float delay = i / 3f * travel;
                var axis = GraceLater(GraceStrand(a, b, 1.2f * gl, Alpha(GraceVoid, .75f), strandLife + .05f, false, 116), delay); axis.hold = .45f; axis.fadePower = 1.2f;
                var vital = GraceLater(GraceStrand(a, b, .24f * gl, Alpha(grade >= 3 ? Color.Lerp(hot, GraceAwake, .5f) : hot, .95f), strandLife, true, 128), delay); vital.hold = .4f; vital.fadePower = 1.5f; vital.pulse = .1f;
            }
            var channel = GraceInkLine(from, to, 2.2f, Alpha(GraceBlood, .5f), strandLife + .1f, 115); channel.fadeIn = travel * .6f; channel.hold = .45f; channel.fadePower = 1.2f;
            var sheen = Line(from, to, 1.6f, Alpha(life, .2f), strandLife, 125, 1.1f); sheen.fadeIn = travel * .6f; sheen.hold = .4f; sheen.fadePower = 1.3f;

            // A wound glow on the victim pours a continuous river along the channel: life and crimson souls winding round
            // each other, soft light motes and rolling blood smoke, each arriving at the caster.
            float speed = length / travel, winding = 360 * turns * .5f / travel;
            var wellspring = GraceLater(Spawn("Grace drain wellspring", glow, from, 1.8f, Alpha(GraceCrimson, .55f), flow, true, 124), travel * .3f);
            wellspring.pulse = .1f; wellspring.fadeIn = .05f; wellspring.hold = .6f; wellspring.fadePower = 1.5f;
            int pour = 0;
            // About 18 river particles are alive at once whatever the length of the stream.
            Emitter(wellspring, Mathf.Clamp(18 / travel, 40, 64) * GraceLevel(1.1f, 1.2f), 0, (owner, p) => GraceDrainFlow(from, dir, side, speed, travel, amp, winding, pour++, life, hot, pigment));

            // The caster drinks it in: an imploding crimson ring, a life bloom with a pigment halo, a glint, a surge of
            // life rising around the body, a second gulp when the last soul lands, and rising motes. A second river landing
            // with the first (level two and up drain two victims) only adds its own gulp.
            float arrive = travel * .85f;
            Vector2 feet = to + Vector2.down * .95f;
            var gulp = GraceLater(Spawn("Grace drain gulp", ring, to, 3.8f, Alpha(GraceCrimson, .9f), .24f, true, 131), travel * .7f); gulp.startScale = 1; gulp.endScale = .15f; gulp.ease = 1.5f; gulp.fadePower = .8f;
            if (GraceRecent(GraceDrinkMark, .3f, to, 1.5f)) { GraceLater(Glint(to, 1.8f, Alpha(hot, .95f), .24f), travel); return; }
            var drinkTint = GraceLater(Spawn("Grace drain drink pigment", glow, to, 3.4f, Alpha(pigment, .5f), flow + .3f, false, 119), arrive); drinkTint.fadeIn = .06f; drinkTint.hold = .5f; drinkTint.fadePower = 1.5f; drinkTint.pulse = .06f;
            var drink = GraceLater(Spawn("Grace drain drink", glow, to, 3.2f, Alpha(life, .85f), flow + .2f, true, 130), arrive); GraceDrinkMark = drink;
            drink.ramp = Ramp(Color.white, life); drink.startScale = .5f; drink.endScale = 1.2f; drink.ease = 2; drink.hold = .4f; drink.fadePower = 1.5f; drink.pulse = .08f;
            var surgeInk = GraceLater(GraceColumn(GraceShaft, feet, 1.9f, 3.8f, Alpha(pigment, .55f), flow, false, 26), arrive); surgeInk.hold = .35f; surgeInk.fadePower = 1.4f;
            var surge = GraceLater(GraceColumn(pillar, feet, 1.7f, 4.4f, Alpha(life, .75f), flow * .85f, true, 27), arrive); surge.hold = .3f; surge.fadePower = 1.5f;
            GraceLater(Wave(to, 4.4f, Alpha(life, .85f), .4f, 3), travel + .04f);
            GraceLater(GraceRing(to, 4, Alpha(pigment, .8f), .38f, 3, false, 119), travel + .04f);
            GraceLater(Glint(to, 2.6f, Color.white, .3f), travel);
            var last = GraceLater(Spawn("Grace drain sated", core, to, .7f, Alpha(hot, .85f), .14f, true, 133), travel + flow); last.startScale = .4f; last.endScale = 1.6f; last.ease = 2.5f; last.fadePower = 1.5f;
            GraceLater(Wave(to, 3.6f, Alpha(hot, .8f), .36f, 3), travel + flow);
            for (int i = 0; i < 4; i++) GraceLater(GraceRise(to + Inside(.6f), i % 2 == 0 ? hot : life, 1.1f), travel + Range(0, flow));
            if (grade < 3) return;
            // Awakened (twice the healing): life overflows. A gold-white column climbs inside the green surge, a ring of life
            // crowns the hero's head and twin streams of light spiral up round the body.
            var overflow = GraceLater(GraceColumn(pillar, feet, .9f, 5.2f, Alpha(GraceAwake, .8f), flow * .8f, true, 28), arrive + .05f); overflow.hold = .3f; overflow.fadePower = 1.5f;
            // Twice the healing, shown literally: a second gulp blooms a beat after the first, with its own ring of life.
            var second = GraceLater(Spawn("Grace drain second drink", glow, to, 3f, Alpha(Color.Lerp(life, GraceAwake, .45f), .85f), .45f, true, 130), arrive + .18f);
            second.ramp = Ramp(Color.white, GraceAwake); second.startScale = .4f; second.endScale = 1.25f; second.ease = 2; second.fadePower = 1.5f;
            var secondTint = GraceLater(Spawn("Grace drain second drink pigment", glow, to, 3.2f, Alpha(GraceUmber, .45f), .5f, false, 119), arrive + .18f);
            secondTint.startScale = .5f; secondTint.endScale = 1.2f; secondTint.ease = 2; secondTint.fadePower = 1.5f;
            GraceLater(Wave(to, 4.4f, Alpha(GraceAwake, .85f), .4f, 3), arrive + .2f);
            GraceLater(GraceRing(to, 4.2f, Alpha(GraceUmber, .7f), .4f, 3, false, 119), arrive + .2f);
            var crownInk = GraceLater(Spawn("Grace drain crown ink", runeRing, to + Vector2.up * 1.25f, 2.48f, GraceUmber, flow, false, 117), arrive);
            var crown = GraceLater(Spawn("Grace drain crown", runeRing, to + Vector2.up * 1.25f, 2.4f, Alpha(GraceAwake, .95f), flow, true, 131), arrive);
            foreach (var clip in new[] { crownInk, crown }) { GraceFlat(clip, .32f); clip.spin = 160; clip.startScale = .3f; clip.endScale = 1; clip.ease = 3; clip.hold = .5f; clip.fadePower = 1.5f; }
            int spirals = Budget(6);
            for (int i = 0; i < spirals; i++)
            {
                var mote = GraceLater(Spawn("Grace drain overflow", i % 2 == 0 ? wisp : glow, feet + Vector2.up * .3f, Range(.2f, .32f), Alpha(i % 3 == 0 ? GraceAwake : hot, .95f), flow + .2f, true, 131), arrive + i * .05f);
                mote.orbitCenter = feet + Vector2.up * .3f; mote.velocity = Vector2.up * 2.6f; mote.orbitAngle = i * 360f / spirals; mote.orbit = (i % 2 == 0 ? 1 : -1) * 420;
                mote.orbitRadius = .75f; mote.orbitRadiusEnd = .35f; mote.fadeIn = .05f; mote.hold = .5f; mote.fadePower = 1.4f; mote.flicker = .2f;
            }
        }

        /// <summary>A short stream of life from a healer to the hero it heals: two winding strands over a pigment twin, motes
        /// flowing along it, and a bloom, a ring and rising light on the hero when it arrives.</summary>
        private void GraceLifeStream(Vector2 from, Vector2 to, Color life, Color hot, Color pigment)
        {
            Vector2 delta = to - from, side = new Vector2(-delta.y, delta.x).normalized; float length = delta.magnitude;
            float travel = Mathf.Clamp(length / 14, .12f, .3f), amp = Mathf.Clamp(length * .12f, .25f, .6f);
            const int segments = 5;
            for (int strand = 0; strand < 2; strand++)
            {
                Vector2 previous = GraceHelix(from, delta, side, 0, amp, 1, strand);
                for (int i = 1; i <= segments; i++)
                {
                    float t = i / (float)segments; Vector2 point = GraceHelix(from, delta, side, t, amp, 1, strand);
                    float delay = (t - .5f / segments) * travel;
                    if (strand == 0) { var tint = GraceLater(GraceStrand(previous, point, .55f, Alpha(pigment, .75f), .55f, false, 119), delay); tint.hold = .35f; tint.fadePower = 1.4f; }
                    var vein = GraceLater(GraceStrand(previous, point, strand == 0 ? .38f : .26f, Alpha(strand == 0 ? life : hot, .95f), .5f, true, 127), delay); vein.hold = .35f; vein.fadePower = 1.5f;
                    previous = point;
                }
            }
            for (int i = 0; i < 4; i++)
            {
                var mote = GraceLater(Spawn("Grace life stream mote", wisp, from, Range(.22f, .34f), Alpha(i % 2 == 0 ? hot : life, .95f), travel, true, 129), i * .05f);
                mote.velocity = delta / travel; mote.fadeIn = .03f; mote.hold = .7f; mote.fadePower = 1.4f;
            }
            var bloom = GraceLater(Flash(to, 2.2f, Alpha(life, .8f), .3f), travel); bloom.ramp = Ramp(Color.white, life);
            GraceLater(Wave(to, 2.8f, Alpha(hot, .8f), .35f, 3), travel);
            GraceLater(GraceRing(to, 2.6f, Alpha(pigment, .7f), .35f, 3, false, 119), travel);
            for (int i = 0; i < 3; i++) GraceLater(GraceRise(to + Inside(.5f), i % 2 == 0 ? hot : life, 1), travel + Range(0, .25f));
        }

        /// <summary>One particle of the soul river. Kinds alternate so life and crimson souls wind round each other.</summary>
        private void GraceDrainFlow(Vector2 from, Vector2 dir, Vector2 side, float speed, float travel, float amp, float winding, int n, Color life, Color hot, Color pigment)
        {
            int kind = n % 6;
            if (kind == 5)
            {
                var fume = Puff(from + side * Range(-.25f, .25f), Range(.8f, 1.15f), Alpha(GraceBlood, .6f), dir * speed * Range(.55f, .7f), travel * 1.35f, 115);
                fume.drag = .4f; fume.startScale = .6f; fume.endScale = 1.5f; fume.fadeIn = .04f; fume.fadePower = 1.5f;
                return;
            }
            bool green = kind == 0 || kind == 2 || kind == 4, lit = kind == 0 || kind == 1 || kind == 4, mote = kind == 4;
            Color tone = kind == 0 ? Alpha(hot, .95f) : kind == 1 ? Alpha(GraceCrimson, .95f) : kind == 2 ? Alpha(pigment, .9f) : kind == 3 ? GraceBlood : Alpha(life, .7f);
            var soul = Spawn(mote ? "Grace river light" : "Grace river soul", mote ? glow : GraceSoul, from, mote ? Range(.8f, 1.1f) : Range(1.2f, 1.7f), tone, travel, lit, lit ? (mote ? 125 : 129) : 120);
            soul.orbitCenter = from; soul.velocity = dir * speed; soul.orbitAngle = (green ? 0 : 180) + Range(-20, 20); soul.orbit = winding;
            soul.orbitRadius = amp * Range(.7f, .95f); soul.orbitRadiusEnd = .06f; soul.orbitPower = 1.6f;
            soul.alignVelocity = !mote; soul.fadeIn = .03f; soul.hold = .7f; soul.fadePower = 1.4f; soul.startScale = 1; soul.endScale = .55f; soul.ease = 1;
        }

        // A soul ripped out of a body: dark burst, crimson flash, impact lines and an imploding vortex.
        private void GraceRip(Vector2 pos, float k)
        {
            var shade = Underlay(pos, 3.2f * k, .48f, .6f); shade.hold = .3f;
            var flash = Spawn("Grace rip flash", core, pos, .9f * k, Color.white, .12f, true, 133); flash.startScale = .5f; flash.endScale = 1.8f; flash.ease = 2.5f; flash.fadePower = 1.5f;
            var blood = Spawn("Grace rip blood bloom", glow, pos, 3 * k, Alpha(GraceDeep(GraceCrimson, .55f, 1), .6f), .4f, false, 117); blood.startScale = .4f; blood.endScale = 1.1f; blood.ease = 3; blood.fadePower = 1.5f;
            var bloom = Flash(pos, 2.6f * k, Alpha(GraceCrimson, .85f), .3f); bloom.ramp = Ramp(Color.white, GraceCrimson);
            GracePigment(ImpactLines(pos, 4.8f * k, Alpha(GraceBlood, .9f), .3f), 118);
            ImpactLines(pos, 4 * k, Alpha(GraceCrimson, .85f), .26f);
            Glint(pos, 2.2f * k, Alpha(Color.Lerp(GraceCrimson, Color.white, .5f), .95f), .22f, 45);
            var maw = Spawn("Grace rip maw", swirl, pos, 3 * k, GraceVoid, .6f, false, 116); maw.spin = 520; maw.angle = Range(0, 360); maw.startScale = 1.25f; maw.endScale = .35f; maw.ease = 2; maw.fadeIn = .03f; maw.fadePower = 1.2f;
            var whirl = Swirl(pos, 2.4f * k, Alpha(GraceCrimson, .85f), .5f, 640); whirl.startScale = 1.2f; whirl.endScale = .3f; whirl.ease = 2;
            int puffs = Budget(4);
            for (int i = 0; i < puffs; i++) { var puff = Puff(pos + Inside(.3f * k), Range(.6f, 1) * k, GraceBlood, Direction(i * 90 + Range(-20, 20)) * Range(1.5f, 3) * k, Range(.45f, .7f), 115); puff.endScale = 2; }
            for (int i = 0; i < 6; i++) Spark(pos, Direction(i * 60 + Range(-15, 15)) * Range(4, 7.5f) * k, i % 2 == 0 ? Color.white : GraceCrimson, Range(.24f, .4f), Range(.22f, .36f), 4, 3);
        }

        // ---------------------------------------------------------------- shared helpers

        /// <summary>A grace skill's growth with its level: 1 at level one, <paramref name="two"/> and <paramref name="three"/> above.</summary>
        private float GraceLevel(float two, float three) { return Grade >= 3 ? three : Grade == 2 ? two : 1; }

        /// <summary>True while <paramref name="clip"/> is alive, younger than <paramref name="window"/> and near <paramref name="pos"/>.</summary>
        private bool GraceRecent(Clip clip, float window, Vector2 pos, float distance)
        {
            return clip != null && clip.age < window && (clip.position - pos).sqrMagnitude < distance * distance && clips.Contains(clip);
        }

        /// <summary>A squashed child plane of a zone root: rings attached to it and spun turn in the ground plane. It dies with the root.</summary>
        private static Transform GracePlane(Transform root, Vector2 offset, float squash)
        {
            var go = new GameObject("Grace ground plane"); go.transform.SetParent(root, false);
            go.transform.localPosition = offset; go.transform.localScale = new Vector3(1, squash, 1);
            return go.transform;
        }

        private Clip GraceRise(Vector2 at, Color color, float scale)
        {
            bool streakMote = Chance(.35f);
            var mote = Spawn("Grace rising light", streakMote ? spark : wisp, at, (streakMote ? Range(.3f, .5f) : Range(.14f, .28f)) * scale, Alpha(color, .95f), Range(.55f, 1), true, 131);
            mote.velocity = new Vector2(Range(-.25f, .25f), Range(1.6f, 3.4f) * Mathf.Max(.7f, scale)); mote.drag = .3f; mote.fadeIn = .08f; mote.endScale = .35f; mote.ease = 1; mote.flicker = .25f;
            mote.alignVelocity = streakMote;
            return mote;
        }

        /// <summary>A rising healing mote: mostly light, sometimes a saturated leaf that keeps its colour on pale floors.</summary>
        private Clip GraceHealMote(Vector2 at, Color light, Color pigment, float scale)
        {
            if (!Chance(.3f)) return GraceRise(at, light, scale);
            var leaf = Spawn("Grace rising leaf", petal, at, Range(.22f, .34f) * scale, Alpha(pigment, .95f), Range(.6f, 1), false, 121);
            leaf.velocity = new Vector2(Range(-.4f, .4f), Range(1.4f, 2.8f) * Mathf.Max(.7f, scale)); leaf.drag = .4f; leaf.spin = Range(-360, 360); leaf.angle = Range(0, 360);
            leaf.fadeIn = .08f; leaf.hold = .4f; leaf.fadePower = 1.3f; leaf.startScale = .5f; leaf.endScale = 1;
            return leaf;
        }

        private Clip GraceColumn(Sprite sprite, Vector2 ground, float width, float height, Color color, float life, bool additive, int order)
        {
            var clip = Spawn("Grace column", sprite, ground, width, color, life, additive, order);
            clip.scale = new Vector3(width / sprite.bounds.size.x, height / sprite.bounds.size.y, 1); clip.root.transform.localScale = clip.scale;
            clip.axisStart = new Vector2(.35f, .12f); clip.axisEnd = Vector2.one; clip.ease = 4; clip.fadePower = 1.3f;
            return clip;
        }

        private Clip GraceDomeLayer(Vector2 ground, float width, float height, Color color, float life, bool additive, int order)
        {
            var clip = Spawn("Grace dome", GraceDome, ground, width, color, life, additive, order);
            clip.scale.y *= height / width; clip.root.transform.localScale = clip.scale;
            return clip;
        }

        /// <summary>An expanding ring; alpha-blended rings keep their colour on bright backdrops.</summary>
        private Clip GraceRing(Vector2 pos, float width, Color color, float life, float ease, bool additive, int order)
        {
            var clip = Spawn("Grace ring", ring, pos, width, color, life, additive, order);
            clip.startScale = .12f; clip.endScale = 1; clip.ease = ease; clip.fadePower = 1.2f; return clip;
        }

        /// <summary>A strand segment drawn with the flat-ended ribbon, so consecutive segments join without beading.</summary>
        private Clip GraceStrand(Vector2 a, Vector2 b, float thickness, Color color, float life, bool additive, int order)
        {
            Vector2 delta = b - a; float length = Mathf.Max(.02f, delta.magnitude) * 1.14f;
            var clip = Spawn("Grace strand", GraceRibbon, (a + b) * .5f, length, color, life, additive, order);
            clip.scale = new Vector3(length / GraceRibbon.bounds.size.x, thickness / GraceRibbon.bounds.size.y, 1);
            clip.angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg; clip.ease = 1;
            clip.root.transform.localScale = clip.scale; clip.root.transform.localRotation = Quaternion.Euler(0, 0, clip.angle);
            return clip;
        }

        private Clip GraceInkLine(Vector2 a, Vector2 b, float thickness, Color color, float life, int order)
        {
            var clip = Line(a, b, thickness, color, life, order, 1.1f); clip.renderer.sharedMaterial = AlphaBlend; return clip;
        }

        /// <summary>Turns an additive clip into an alpha-blended pigment layer drawn under the light.</summary>
        private static Clip GracePigment(Clip clip, int order)
        {
            clip.renderer.sharedMaterial = AlphaBlend; clip.renderer.sortingOrder = order; return clip;
        }

        private static Clip GraceLater(Clip clip, float delay)
        {
            if (delay > 0) { clip.age = -delay; if (clip.renderer) clip.renderer.enabled = false; }
            return clip;
        }

        private void GraceFlat(Clip clip, float squash) { LayFlat(clip, squash); }

        /// <summary>An emitter point mapped from the emitter's circle onto the squashed floor ellipse.</summary>
        private static Vector2 GraceOnGround(Clip owner, Vector2 p)
        {
            Vector2 o = owner.root.transform.position; return o + new Vector2(p.x - o.x, (p.y - o.y) * .3f);
        }

        private static Vector2 GraceHelix(Vector2 from, Vector2 delta, Vector2 side, float t, float amp, float turns, int strand)
        {
            float envelope = Mathf.Pow(Mathf.Sin(Mathf.PI * (t * .9f + .05f)), .55f);
            return from + delta * t + side * Mathf.Sin(t * turns * 2 * Mathf.PI + strand * Mathf.PI) * amp * envelope;
        }

        /// <summary>The tint when it is a real colour, otherwise the family fallback.</summary>
        private static Color GraceVivid(Color tint, Color fallback)
        {
            float h, s, v; Color.RGBToHSV(tint, out h, out s, out v);
            if (s < .2f || v < .25f) return fallback;
            return Color.HSVToRGB(h, Mathf.Max(s, .45f), Mathf.Max(v, .9f));
        }

        /// <summary>A deep, saturated version of a colour for alpha-blended contrast layers.</summary>
        private static Color GraceDeep(Color color, float value, float alpha, float hueShift = 0)
        {
            float h, s, v; Color.RGBToHSV(color, out h, out s, out v);
            var deep = Color.HSVToRGB(Mathf.Repeat(h + hueShift, 1), Mathf.Clamp01(s * 1.25f + .2f), value); deep.a = alpha; return deep;
        }

        // ---------------------------------------------------------------- textures

        private static void BuildGraceTextures()
        {
            // A hexagonal barrier dome standing on the floor (bottom pivot): bright rim, fresnel edge, lattice that thins
            // toward the middle so the hero inside stays readable, a soft sheen and a line hugging the ground.
            GraceDome = Build("Grace dome", 256, 256, (u, v) =>
            {
                float h = (v + 1) * .5f, r = Mathf.Sqrt(u * u + h * h);
                if (r > 1) return Color.clear;
                float inside = (1 - Smooth(.955f, .99f, r)) * Smooth(0, .025f, h);
                float rim = Gauss(r - .955f, .018f) + Gauss(r - .95f, .07f) * .3f, fresnel = Mathf.Pow(r, 5) * .45f;
                float lattice = Gauss(HexEdge(u, h, .12f), .012f) * (.2f + .8f * Mathf.Pow(r, 2.5f));
                float sheen = Gauss(Mathf.Sqrt(Sq(u + .38f) + Sq(h - .62f)), .18f) * .25f;
                float a = (rim + fresnel + lattice * .75f + sheen + .03f) * inside + Gauss(h, .03f) * (1 - Smooth(.8f, 1, Mathf.Abs(u))) * .5f;
                return White(Mathf.Clamp01(a));
            }, new Vector2(.5f, 0));

            // A soul streak: a round head with a wavering, wispy tail (drawn along +x so it aligns with its velocity).
            GraceSoul = Build("Grace soul", 128, 64, (u, v) =>
            {
                float x = u, y = v * .5f, d = Mathf.Sqrt(Sq(x - .5f) + y * y);
                float head = Gauss(d, .17f), t = Mathf.Clamp01((.5f - x) / 1.4f);
                float width = .17f * Mathf.Pow(1 - t, 1.1f) + .012f, wave = Mathf.Sin(t * 8.5f) * .045f * t;
                float tail = x < .5f ? Gauss(y - wave, width) * Mathf.Pow(1 - t, 1.4f) * (.75f + .5f * Fbm(x * 3 + 5, y * 6 + 2, 2)) : 0;
                float a = Mathf.Max(head, tail * .85f) + Gauss(d, .07f) * .3f;
                return White(Mathf.Clamp01(a) * (1 - Smooth(.86f, .99f, Mathf.Abs(u))) * (1 - Smooth(.8f, .99f, Mathf.Abs(v))));
            });

            // A faceted light shard: a tall diamond with four differently lit faces and a bright ridge.
            GraceFacet = Build("Grace facet", 48, 96, (u, v) =>
            {
                float d = Mathf.Abs(u) / .8f + Mathf.Abs(v) / .95f, inside = 1 - Smooth(.86f, .96f, d);
                float face = u < 0 ? (v > 0 ? 1 : .86f) : (v > 0 ? .74f : .62f);
                float l = Mathf.Clamp01(face + Gauss(u, .06f) * .25f + Gauss(d - .86f, .05f) * .3f);
                return new Color(l, l, l, inside);
            });

            // A light shaft (bottom pivot) with a flat, streaked body so the column keeps a clear silhouette.
            GraceShaft = Build("Grace shaft", 64, 256, (u, v) =>
            {
                float h = (v + 1) * .5f, body = 1 - Smooth(.42f, .95f, Mathf.Abs(u));
                float streaks = .68f + .32f * Mathf.PerlinNoise(u * 5 + 11, h * 1.6f + 3);
                float rise = Smooth(0, .03f, h) * (1 - Smooth(.4f, 1, h));
                return White(Mathf.Clamp01((body * streaks + Gauss(u, .18f) * .35f) * rise));
            }, new Vector2(.5f, 0));

            // A bevelled hexagonal ward plate (flat top and bottom): a shaded face, a bright bevelled rim and a small inner
            // hexagon. Tinted dark amber it reads on pale skies; drawn additively it flashes as a lit cell.
            GracePlate = Build("Grace plate", 96, 96, (u, v) =>
            {
                float x = Mathf.Abs(u), y = Mathf.Abs(v), d = Mathf.Max(x * .866f + y * .5f, y);
                float inside = 1 - Smooth(.8f, .84f, d);
                if (inside <= 0) return Color.clear;
                float rim = Smooth(.62f, .74f, d), inner = Gauss(d - .36f, .03f);
                float l = Mathf.Clamp01(.5f + .16f * (v - u * .4f) + rim * .5f + inner * .4f);
                return new Color(l, l, l, inside * Mathf.Lerp(.85f, 1, rim));
            });

            // A soft energy ribbon (along +x) with a brighter core and ends that fade to half exactly where segments overlap,
            // so a chain of segments reads as one continuous strand.
            GraceRibbon = Build("Grace ribbon", 128, 32, (u, v) =>
            {
                float ends = 1 - Smooth(.76f, .99f, Mathf.Abs(u));
                float body = Gauss(v, .42f) * .75f + Gauss(v, .16f) * .45f;
                float grain = .82f + .18f * Mathf.PerlinNoise(u * 4 + 7, v * 1.5f + 2);
                return White(Mathf.Clamp01(body * grain) * ends * (1 - Smooth(.85f, 1, Mathf.Abs(v))));
            });

            // A lotus seal: two rings of petal outlines with veins, pearls between the petal tips and a ticked outer band.
            GraceMandala = Build("Grace lotus seal", 384, 384, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float th = Mathf.Atan2(v, u);
                float a = Gauss(r - .93f, .012f) + Gauss(r - .875f, .008f) * .85f + Gauss(r - .27f, .01f) * .9f + Gauss(r - .21f, .007f) * .6f;
                for (int layer = 0; layer < 2; layer++)
                {
                    float step = 2 * Mathf.PI / 8, offset = layer == 0 ? 0 : step * .5f, tip = layer == 0 ? .84f : .6f, root = .27f;
                    float local = Mathf.Repeat(th - offset + step * .5f, step) - step * .5f, t = (r - root) / (tip - root);
                    if (t < 0 || t > 1.05f) continue;
                    float half = step * (layer == 0 ? .5f : .42f) * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)), .75f);
                    a += Gauss((Mathf.Abs(local) - half) * r, .011f) * (layer == 0 ? .95f : .7f);
                    if (Mathf.Abs(local) < half) a += .1f + Gauss(local * r, .006f) * .35f * (1 - Mathf.Clamp01(t));
                }
                float pearl = Mathf.Repeat(th, Mathf.PI / 4) - Mathf.PI / 8;
                a += Gauss(Mathf.Sqrt(Sq(pearl * .9f) + Sq(r - .9f)), .022f) * .9f;
                float tick = Mathf.Abs(Mathf.Repeat(th * Mathf.Rad2Deg + 3.75f, 7.5f) - 3.75f);
                a += (1 - Smooth(.5f, 1.2f, tick)) * Smooth(.882f, .888f, r) * (1 - Smooth(.912f, .918f, r)) * .6f;
                a += (1 - Smooth(.05f, .85f, r)) * .06f + Gauss(r, .08f) * .5f;
                return White(Mathf.Clamp01(a) * (1 - Smooth(.95f, .995f, r)));
            });

            // Four runic glyphs (Algiz, Tiwaz, Ingwaz, Sowilo) inside a thin ring.
            GraceGlyphs = Sheet("Grace glyph", 4, 64, 64, (u, v, f) =>
            {
                float d = GraceGlyphDistance(u, v, f), r = Mathf.Sqrt(u * u + v * v);
                float a = Gauss(d, .075f) + Gauss(d, .2f) * .3f + Gauss(r - .76f, .035f) * .55f;
                return White(Mathf.Clamp01(a) * Window(r));
            });
        }

        private static float GraceGlyphDistance(float u, float v, int frame)
        {
            float d;
            switch (frame)
            {
                case 0:
                    d = SegmentDistance(u, v, 0, -.55f, 0, .55f);
                    d = Mathf.Min(d, SegmentDistance(u, v, 0, .05f, -.38f, .5f));
                    d = Mathf.Min(d, SegmentDistance(u, v, 0, .05f, .38f, .5f));
                    break;
                case 1:
                    d = SegmentDistance(u, v, 0, -.55f, 0, .55f);
                    d = Mathf.Min(d, SegmentDistance(u, v, 0, .55f, -.36f, .15f));
                    d = Mathf.Min(d, SegmentDistance(u, v, 0, .55f, .36f, .15f));
                    break;
                case 2:
                    d = SegmentDistance(u, v, 0, .55f, -.38f, 0);
                    d = Mathf.Min(d, SegmentDistance(u, v, -.38f, 0, 0, -.55f));
                    d = Mathf.Min(d, SegmentDistance(u, v, 0, -.55f, .38f, 0));
                    d = Mathf.Min(d, SegmentDistance(u, v, .38f, 0, 0, .55f));
                    d = Mathf.Min(d, SegmentDistance(u, v, -.2f, 0, .2f, 0));
                    break;
                default:
                    d = SegmentDistance(u, v, -.3f, .55f, .28f, .15f);
                    d = Mathf.Min(d, SegmentDistance(u, v, .28f, .15f, -.28f, -.15f));
                    d = Mathf.Min(d, SegmentDistance(u, v, -.28f, -.15f, .3f, -.55f));
                    break;
            }
            return d;
        }
        // ---------------------------------------------------------------- field of life

        /// <summary>A lasting field of life (level-three heal pulse, Raphael's oath; it heals the hero standing in it every half
        /// second). A lotus seal lies on the floor with its outer ring on the field's edge, over a soft green-gold glow and a dark
        /// green stain that keeps it on pale floors; a faint column of light breathes over it, a ring of life runs out to the edge
        /// with every heal, and motes and leaves drift up off it.</summary>
        private void ZoneHealPulse(Transform root, float radius, float life)
        {
            float R = Mathf.Clamp(radius, .8f, 4.5f), k = Mathf.Clamp(R / 2.2f, .5f, 1.6f);
            Color green = LifeLight, gold = Color.Lerp(LifeLight, GoldLight, .45f), hot = Color.Lerp(LifeLight, Warm, .6f);
            Color deep = GraceDeep(LifeLight, .3f, .8f), pigment = GraceDeep(LifeLight, .6f, .9f);
            // The zone root rides 0.6 above the field's centre on the floor.
            var floor = GracePlane(root, new Vector2(0, -.58f), .3f);
            float seal = R * 2 / .93f;
            var stain = Attach(floor, "Grace field stain", glow, R * 2.5f, Alpha(deep, .35f), 24, false);
            var glowPool = Attach(floor, "Grace field glow", glow, R * 2.7f, Alpha(gold, .5f), 26); glowPool.pulse = .05f;
            var ink = Attach(floor, "Grace field seal ink", GraceMandala, seal * 1.02f, Alpha(deep, .75f), 25, false); ink.spin = 12;
            var lotus = Attach(floor, "Grace field seal", GraceMandala, seal, Alpha(green, .85f), 27); lotus.spin = 12;
            var runes = Attach(floor, "Grace field runes", runeRing, R * 1.15f, Alpha(gold, .8f), 27); runes.spin = -28;
            // A faint column of life breathing over the field, behind the actors.
            var column = Attach(root, "Grace field column", GraceShaft, R * .95f, Alpha(green, .22f), 26, true, new Vector2(0, -.6f));
            column.scale.y = 3.4f * k / GraceShaft.bounds.size.y; column.pulse = .04f;
            var columnInk = Attach(root, "Grace field column ink", GraceShaft, R * 1.1f, Alpha(pigment, .16f), 25, false, new Vector2(0, -.6f));
            columnInk.scale.y = 3 * k / GraceShaft.bounds.size.y;
            var layers = new[] { stain, glowPool, ink, lotus, runes, column, columnInk };
            float hold = Mathf.Clamp01(1 - .7f / life), pop = Mathf.Log(.05f) / Mathf.Log(1 - Mathf.Clamp(.35f / life, .02f, .5f));
            foreach (var clip in layers) { clip.life = life; clip.loop = false; clip.fadeIn = .2f; clip.hold = hold; clip.fadePower = 1.4f; clip.startScale = .4f; clip.endScale = 1; clip.ease = pop; }
            foreach (var clip in new[] { column, columnInk }) { clip.startScale = 1; clip.axisStart = new Vector2(.4f, .15f); clip.axisEnd = Vector2.one; }
            // With every heal (twice a second) a ring of life runs out to the field's edge, and now and then a glint sparkles on it.
            int beat = 0; float ringWidth = R * 2 / .74f;
            Emitter(glowPool, 2, 0, (owner, p) =>
            {
                if (owner.age > owner.life - .5f) return;
                Vector2 at = owner.root.transform.position;
                var wave = Spawn("Grace field breath", ring, at + Vector2.up * .02f, ringWidth, Alpha(hot, .7f), .7f, true, 26);
                var waveInk = Spawn("Grace field breath ink", ring, at, ringWidth * 1.02f, Alpha(pigment, .45f), .75f, false, 24);
                foreach (var clip in new[] { wave, waveInk }) { clip.scale.y *= .3f; clip.root.transform.localScale = clip.scale; clip.startScale = .2f; clip.endScale = 1; clip.ease = 2.2f; clip.fadePower = 1.3f; }
                var bloom = Spawn("Grace field bloom", glow, at + Vector2.up * .1f, R * 1.4f, Alpha(green, .3f), .5f, true, 26);
                bloom.scale.y *= .4f; bloom.root.transform.localScale = bloom.scale; bloom.startScale = .5f; bloom.endScale = 1.1f; bloom.ease = 2; bloom.fadePower = 1.5f;
                if (beat++ % 2 == 0) Glint(at + new Vector2(Range(-.7f, .7f) * R, Range(.3f, 1.6f)), Range(.7f, 1), Alpha(Color.Lerp(hot, Color.white, .4f), .95f), .3f, Range(0, 45));
            });
            // Motes and leaves drift up off the whole field.
            float spread = R * .9f;
            Emitter(lotus, 9 + 4 * k, 0, (owner, p) =>
            {
                if (owner.age > owner.life - .4f) return;
                Vector2 at = (Vector2)owner.root.transform.position + new Vector2(Range(-spread, spread), Range(-.15f, .15f));
                GraceHealMote(at, Chance(.35f) ? hot : green, pigment, .9f);
            });
            Bundle(root, lotus, stain, glowPool, ink, runes, column, columnInk);
            // The field opens: a bloom and a ring out to its edge.
            Vector2 centre = (Vector2)root.position + new Vector2(0, -.58f);
            var open = GroundWave(centre, ringWidth, Alpha(gold, .9f), .45f); open.startScale = .15f;
            var flash = Spawn("Grace field open", glow, centre + Vector2.up * .3f, R * 1.6f, Alpha(hot, .6f), .3f, true, 128); flash.scale.y *= .5f; flash.startScale = .4f; flash.endScale = 1.2f; flash.ease = 2; flash.fadePower = 1.5f;
        }
    }
}
