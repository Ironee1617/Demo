using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Grace family: HealPulse, ShieldPulse and LifeDrain, drawn as painted light.
    // HealPulse: heaven opens over the caster (a column of light with a white spine inside a broad soft beam) while a lotus
    // seal and a painted brush ring unfold on the floor in a pool of light and painted leaves whirl into the hands; the release
    // billows a holy blast behind the caster, strikes a divine column down from the sky with a hand-drawn spark and a tall
    // cross of spindle strokes, raises sanctuary pillars of light on the rim of the area and sends crosses, sparkles and
    // leaves rising.
    // ShieldPulse: luminous gold plates fly in and lock onto a glowing painted dome (a bright rim inside a soft halo, a faint
    // lattice that never hides the hero); the dome snaps shut with a spark, a scan runs up its cells, a beacon shoots from its
    // apex and crystal slivers of light break off. Blocks flash the lattice; a level-three ward bursts as it breaks.
    // LifeDrain: a crimson whirl round a plasma heart drinks soul motes into the hand while luminous tendrils hook the victim;
    // the release tears the soul out (a crimson blast, a spark, needles) and a luminous crimson ribbon with a strand of life
    // winding through it pours plasma soul motes into the caster, who surges with a column of life.
    // Every shape is painted light (a crisp silhouette, a smooth ramp inside) over a soft bloom; painted objects (leaves,
    // plates, slivers) are lit softly from the upper left. There are no dark ink twins: large backdrops sit behind the actors
    // and a soft underlay keeps the light readable on pale skies. Instant echo casts (wind-up under 0.2 s) are always followed
    // at once by the skill's own burst, so they stay small.
    public sealed partial class TowerVfx
    {
        private static Sprite GraceDome, GraceDomeRim, GraceDomeGlow, GraceSoul, GraceFacet, GraceMandala, GraceBeam, GraceRibbon, GracePlate, GracePillar, GraceCross, GraceLeaf, GraceBrushRing, GraceHex, GraceShock;
        private static Sprite[] GraceGlyphs, GraceStream, GraceStreamShift, GraceRibbonFrames;
        private static readonly Color GraceCrimson = new Color(1, .16f, .3f), GraceSoulHot = new Color(1, .7f, .74f), GraceSmoke = new Color(.62f, .08f, .18f, .5f),
            GraceAwake = new Color(1, .93f, .68f);
        private const float GraceDomeWidth = 4.4f, GraceDomeHeight = 3.6f;
        // The caster's drink of the first soul river of a drain, so a second river landing with it only adds a gulp.
        private Clip GraceDrinkMark;

        // ---------------------------------------------------------------- heal

        private void CastHealPulse(CastInfo c)
        {
            float k = Mathf.Clamp(c.size / 1.4f, .75f, 2), w = c.windup, release = Mathf.Max(0, w - .02f);
            Color main = GraceVivid(c.tint, LifeLight), life = Color.Lerp(main, LifeLight, .15f), hot = Color.Lerp(life, Warm, .55f);
            Color leafTone = GraceLeafTone(main);
            Vector2 pos = c.pos, ground = pos + Vector2.down, hand = pos + new Vector2(c.facing * .25f, .45f), heart = pos + Vector2.up * .2f;

            // Life gathers in the raised hands: a white-hot seed in a soft bloom inside a turning cross, peaking at the release.
            float handLife = w + .1f;
            var bloom = Anchored(c.anchor, hand, "Grace heal hand bloom", glow, 1.8f * k, Alpha(life, .6f), w + .06f, 28);
            bloom.fadeIn = .05f; bloom.startScale = .3f; bloom.endScale = 1; bloom.ease = 1.5f; bloom.hold = .85f; bloom.fadePower = 2; bloom.pulse = .06f;
            var seed = Anchored(c.anchor, hand, "Grace heal hand core", core, .5f * k, Alpha(hot, 1), w + .05f, 126);
            seed.fadeIn = .04f; seed.startScale = .25f; seed.endScale = 1; seed.ease = 1.5f; seed.hold = .85f; seed.fadePower = 2; seed.pulse = .08f;
            var handCross = Anchored(c.anchor, hand, "Grace heal hand cross", GraceCross, 1.25f * k, Alpha(life, .95f), handLife, 125);
            handCross.spin = 150; handCross.startScale = .2f; handCross.endScale = 1; handCross.ease = 1.6f; handCross.hold = w / handLife; handCross.fadePower = 2;
            if (w < .2f)
            {
                // An instant echo: the heal burst follows at once, so the echo only sparks the hands.
                Converge(hand, 6, 1.8f * k, life, w, .45f);
                GraceStar(hand, 1.6f * k, Alpha(hot, 1), .18f, 132, release);
                return;
            }
            float total = w + .4f, hold = w / total;
            // Higher levels open a wider shaft of heaven and a bigger halo.
            int grade = Grade; float gl = GraceLevel(1.15f, 1.3f);

            // Heaven opens: a column of light with a white spine descends on the caster inside a broad soft beam (both behind
            // the actors), and drops of light rain down; they brighten toward the release.
            float beamLife = w + .16f;
            var beamGlow = GraceColumn(GraceBeam, ground, 2.6f * k * gl, 12, Alpha(life, .5f), beamLife, true, 26);
            var shaft = GraceColumn(GracePillar, ground, .95f * k * gl, 12, Alpha(life, .95f), beamLife, true, 27);
            foreach (var clip in new[] { beamGlow, shaft }) { clip.axisStart = new Vector2(.12f, 1); clip.axisEnd = Vector2.one; clip.ease = 1.4f; clip.fadeIn = w * .45f; clip.hold = w / beamLife; clip.fadePower = 2; }
            int drops = Budget(6);
            for (int i = 0; i < drops; i++)
            {
                float x = pos.x + Range(-.8f, .8f) * k, top = pos.y + Range(4.5f, 6);
                var drop = GraceLater(Line(new Vector2(x, top), new Vector2(x, top - 1.3f), .11f * k, Alpha(hot, .95f), .3f, 130), Range(0, Mathf.Max(0, w - .3f)));
                drop.velocity = Vector2.down * 15; drop.fadeIn = .05f; drop.fadePower = 1.2f;
            }

            // A lotus seal unfolds under the caster in a pool of soft light inside one painted brush ring; light motes,
            // crosses and painted leaves rise off it.
            var pool = GraceGroundBloom(ground, 5.2f * k, Alpha(life, .5f), total);
            var seal = Spawn("Grace lotus seal", GraceMandala, ground, 3.8f * k, Alpha(life, .95f), total, true, 27);
            GraceFlat(seal, .3f); seal.spin = 30;
            foreach (var clip in new[] { pool, seal }) { clip.fadeIn = .05f; clip.startScale = .45f; clip.endScale = 1; clip.ease = 3; clip.hold = hold; clip.fadePower = 1.5f; }
            var runes = RuneCircle(ground, 2.6f * k, Alpha(hot, .85f), total, -80, true); runes.hold = hold;
            var band = Spawn("Grace heal ring", GraceBrushRing, ground, 4.6f * k, Alpha(life, 1), total, true, 26);
            GraceFlat(band, .3f); band.angle = Range(0, 360); band.spin = 70; band.fadeIn = .06f; band.startScale = .4f; band.endScale = 1; band.ease = 2.5f; band.hold = hold; band.fadePower = 1.4f;
            Emitter(band, 18 * k, 1.7f * k, (owner, p) => GraceHealMote(GraceOnGround(owner, p), Chance(.3f) ? hot : LifeLight, leafTone, k));

            // A lotus halo opens behind the caster in a soft bloom as the charge builds (behind the actors, so the hero stays
            // readable).
            var haloGlow = Spawn("Grace heal halo bloom", glow, heart, 4.8f * k * gl, Alpha(life, .42f), total, true, 25);
            var halo = Spawn("Grace heal halo", GraceMandala, heart, 4.1f * k * gl, Alpha(life, .85f), total, true, 27); halo.spin = -25;
            foreach (var clip in new[] { haloGlow, halo }) { clip.startScale = .3f; clip.endScale = 1; clip.ease = 2; clip.fadeIn = .06f; clip.hold = hold; clip.fadePower = 1.6f; }
            if (grade >= 2)
            {
                // Level two (part of the heal becomes a shield): a gold ring of ward light turns inside the halo the other way;
                // awakened, a gold-white rune ring crowns the halo's rim.
                var ward = Spawn("Grace heal ward ring", runeRing, heart, 2.6f * k, Alpha(Color.Lerp(GoldLight, Warm, .3f), .9f), total, true, 28);
                ward.spin = 60; ward.startScale = .3f; ward.endScale = 1; ward.ease = 2; ward.fadeIn = .08f; ward.hold = hold; ward.fadePower = 1.6f;
            }
            if (grade >= 3)
            {
                var crown = Spawn("Grace heal crown", runeRing, heart, 4.6f * k * gl, Alpha(GraceAwake, .85f), total, true, 28);
                crown.spin = 18; crown.startScale = .35f; crown.endScale = 1; crown.ease = 2; crown.fadeIn = .1f; crown.hold = hold; crown.fadePower = 1.6f;
            }

            Converge(hand, 12, 2.7f * k, life, w, .45f);
            // Painted leaves whirl in from a wide circle and vanish into the hands; every third one is a leaf of pure light.
            int leaves = Budget(10);
            for (int i = 0; i < leaves; i++)
            {
                bool lit = i % 3 == 0;
                var leaf = Spawn("Grace gathering leaf", GraceLeaf, hand, Range(.46f, .64f) * k, lit ? Alpha(hot, .95f) : Alpha(Color.Lerp(leafTone, main, Range(0, .35f)), .95f), w * Range(.85f, 1), lit, lit ? 130 : 121);
                leaf.orbitCenter = hand; leaf.orbitAngle = i * 360f / leaves + Range(-12, 12); leaf.orbit = Range(240, 340);
                leaf.orbitRadius = Range(2.1f, 2.9f) * k; leaf.orbitRadiusEnd = .1f; leaf.orbitPower = 1.4f;
                leaf.spin = Range(-300, 300); leaf.fadeIn = .06f; leaf.hold = .7f; leaf.fadePower = 2; leaf.startScale = 1; leaf.endScale = .4f;
            }

            // Release: a hand-drawn spark, a tall cross of spindle strokes, needles and shock fronts (heart pickups and rest heals
            // end here).
            GraceStar(hand, 2 * k, Alpha(hot, 1), .24f, 132, release);
            GraceCrossFlare(hand, 3.4f * k, Alpha(life, 1), .3f, release);
            GraceWave(pos, 4.4f * k, Alpha(life, .9f), .36f, 3, 131, release);
            GraceGroundWave(ground, 5.6f * k, Alpha(life, .9f), .42f, release);
            int needles = Budget(8);
            for (int i = 0; i < needles; i++)
            {
                Vector2 way = Direction(i * 360f / needles + Range(-18, 18));
                GraceLater(Spark(hand + way * .15f, way * Range(4, 7) * k, i % 3 == 0 ? Color.white : life, Range(.3f, .45f) * k, Range(.22f, .34f), 3, 3), release);
            }
            int pops = Budget(6);
            for (int i = 0; i < pops; i++)
            {
                bool lit = i % 2 == 0;
                var leaf = GraceLater(Spawn("Grace leaf pop", GraceLeaf, hand, Range(.34f, .5f) * k, lit ? Alpha(hot, .95f) : Alpha(leafTone, .95f), Range(.6f, .9f), lit, lit ? 130 : 121), release);
                leaf.velocity = Direction(i * 360f / pops + Range(-15, 15)) * Range(3.5f, 5.5f) * k + Vector2.up; leaf.drag = 3; leaf.gravity = -1; leaf.spin = Range(-500, 500); leaf.hold = .35f; leaf.fadePower = 1.3f;
            }
        }

        private void ImpactHealPulse(Vector2 pos, float radius, Color tint, int facing)
        {
            float R = Mathf.Clamp(radius, .8f, 4.5f), s = Mathf.Clamp(.35f + R * .19f, .55f, 1.2f);
            Color main = GraceVivid(tint, LifeLight), life = Color.Lerp(main, LifeLight, .15f), hot = Color.Lerp(life, Warm, .55f);
            Color leafTone = GraceLeafTone(main);
            Vector2 ground = pos + Vector2.down * .95f, heart = pos + Vector2.up * .2f;
            float span = Mathf.Max(2.6f, R * 1.9f);
            // Higher levels: a wider divine column and taller sanctuary pillars (the healed area itself grows with the radius).
            int grade = Grade; float gl = GraceLevel(1.15f, 1.3f);

            // Peak, inside the first drawn frames: a white-hot heart, a holy blast billowing behind the caster (the hand-drawn
            // explosion in life's colours, wholly behind the actors so the hero stays clear), a spark flaring over the head, a
            // tall cross of spindle strokes, light wedges, needles and sparkles flung out.
            var shade = Underlay(heart, 4.6f * s, .2f, .45f); shade.hold = .2f;
            var core0 = Spawn("Grace heal heart", core, pos, .8f * s, Color.white, .1f, true, 133); core0.startScale = .5f; core0.endScale = 1.5f; core0.ease = 4; core0.fadePower = 1.3f;
            Blast(heart, 4 * s, Alpha(hot, 1), .6f, 26);
            GraceStar(heart + Vector2.up * .9f * s, 2.6f * s, Alpha(hot, 1), .3f, 132);
            GraceCrossFlare(heart, 6.6f * s, Alpha(life, 1), .42f, 0);
            ImpactLines(pos, 5.4f * s, Alpha(life, .85f), .26f);
            int needles = Budget(10);
            for (int i = 0; i < needles; i++)
            {
                Vector2 way = Direction(i * 360f / needles + Range(-16, 16));
                Spark(heart + way * .2f, way * Range(6, 10) * s + Vector2.up * 1.5f, i % 3 == 0 ? Color.white : life, Range(.4f, .6f) * s, Range(.25f, .4f), 4, 3);
            }
            Sparkles(heart, 8, 1.1f * s, hot);

            // A huge lotus halo flares open behind the caster in a soft bloom.
            var haloGlow = Spawn("Grace heal halo bloom", glow, heart, 6 * s, Alpha(life, .45f), .8f, true, 25);
            var halo = Spawn("Grace heal halo", GraceMandala, heart, 4.9f * s, Alpha(life, .9f), .85f, true, 27); halo.spin = -30;
            foreach (var clip in new[] { haloGlow, halo }) { clip.startScale = .55f; clip.endScale = 1.1f; clip.ease = 6; clip.hold = .25f; clip.fadePower = 1.4f; }

            // A divine column strikes down from the sky at full height and widens: a broad soft beam, a column of light and its
            // white-hot spine, all behind the actors so the hero stays clear; the strike splashes painted wedges across the floor.
            float height = 14 * Mathf.Lerp(.7f, 1, Mathf.InverseLerp(.55f, 1, s));
            var beamGlow = GraceColumn(GraceBeam, ground, 6.4f * s * gl, height, Alpha(life, .55f), .9f, true, 26); beamGlow.hold = .3f; beamGlow.fadePower = 1.2f;
            var beam = GraceColumn(GracePillar, ground, 2.6f * s * gl, height, Alpha(life, 1), .9f, true, 27); beam.hold = .25f; beam.fadePower = 1.3f;
            var spine = GraceColumn(GracePillar, ground, .9f * s * gl, height, Alpha(grade >= 3 ? GraceAwake : hot, 1), .45f, true, 29); spine.hold = .15f; spine.fadePower = 1.4f;
            foreach (var column in new[] { beamGlow, beam, spine }) { column.axisStart = new Vector2(.12f, 1); column.ease = 6; }
            var splash = Spawn("Grace heal splash", burst, ground, span * 1.15f, Alpha(life, 1), .3f, true, 27);
            GraceFlat(splash, .3f); splash.angle = Range(0, 360); splash.startScale = .5f; splash.endScale = 1.1f; splash.ease = 5; splash.hold = .2f; splash.fadePower = 1.2f;

            // Shock fronts on the body and the floor; a painted brush ring then holds on the floor, slowly turning, and erodes.
            GraceWave(pos, R * 2.6f, Alpha(life, .9f), .45f, 3);
            GraceGroundWave(ground, span * 1.55f, Alpha(life, .9f), .5f);
            var band = Spawn("Grace heal ring", GraceBrushRing, ground, span * 1.2f, Alpha(life, 1), 1.25f, true, 26);
            GraceFlat(band, .3f); band.angle = Range(0, 360); band.spin = 36; band.startScale = .6f; band.endScale = 1; band.ease = 6; band.hold = .45f; band.fadePower = 1.2f;

            // The lotus seal holds on the floor in a pool of soft light, slowly turning, then erodes; crosses and leaves rise off it.
            var pool = GraceGroundBloom(ground, span * 1.3f, Alpha(life, .5f), 1.4f);
            var seal = Spawn("Grace lotus seal", GraceMandala, ground, span, Alpha(life, 1), 1.4f, true, 27);
            GraceFlat(seal, .3f); seal.spin = 16;
            foreach (var clip in new[] { pool, seal }) { clip.fadeIn = .03f; clip.startScale = .55f; clip.endScale = 1; clip.ease = 6; clip.hold = .45f; clip.fadePower = 1.3f; }
            RuneCircle(ground, span * .66f, Alpha(hot, .9f), 1.3f, -45, true);
            Emitter(seal, 24 * s, span * .45f, (owner, p) => GraceHealMote(GraceOnGround(owner, p), Chance(.35f) ? hot : LifeLight, leafTone, s));

            // Sanctuary: pillars of soft-edged light erupt one after another around the rim of the healed area, each inside a
            // soft beam and kicking up a spark at its foot.
            int posts = Budget(Mathf.RoundToInt(4 + 3 * s));
            float rimX = span * .47f, rimY = span * .47f * .3f;
            for (int i = 0; i < posts; i++)
            {
                float a = (i * 360f / posts + 90 + 180f / posts) * Mathf.Deg2Rad;
                Vector2 at = ground + new Vector2(Mathf.Cos(a) * rimX, Mathf.Sin(a) * rimY);
                bool back = Mathf.Sin(a) > 0;
                float delay = .05f + i * .035f, h = Range(3.3f, 4.3f) * s * gl * (back ? .85f : 1);
                var aura = GraceLater(GraceColumn(GraceBeam, at, 2 * s, h * 1.15f, Alpha(life, .5f), .65f, true, back ? 26 : 119), delay);
                aura.axisStart = new Vector2(.5f, .05f); aura.ease = 6; aura.hold = .3f; aura.fadePower = 1.3f;
                var post = GraceLater(GraceColumn(GracePillar, at, .95f * s, h * 1.1f, Alpha(life, 1), .55f, true, back ? 27 : 127), delay);
                post.axisStart = new Vector2(.35f, .05f); post.ease = 6; post.hold = .25f; post.fadePower = 1.3f;
                var spring = GraceLater(SparkFlip(at, 1.5f * s, Alpha(hot, 1), .2f, back ? 27 : 128), delay);
                GraceFlat(spring, .4f);
            }

            // Leaf storm: painted leaves (and a few of pure light) whirl out of the heart and drift up.
            int leaves = Budget(Mathf.RoundToInt(24 * s));
            for (int i = 0; i < leaves; i++)
            {
                bool lit = i % 4 == 0;
                var leaf = Spawn("Grace leaf storm", GraceLeaf, pos, Range(.4f, .6f) * Mathf.Max(.8f, s), lit ? Alpha(hot, .95f) : Alpha(Color.Lerp(leafTone, main, Range(0, .4f)), .95f), Range(1, 1.6f), lit, lit ? 130 : 121);
                leaf.orbitCenter = pos + Vector2.down * .15f; leaf.velocity = Vector2.up * Range(.5f, 1.5f); leaf.drag = .3f;
                leaf.orbitAngle = i * 360f / leaves + Range(-12, 12); leaf.orbit = (i % 4 == 3 ? -1 : 1) * Range(110, 210);
                leaf.orbitRadius = .25f; leaf.orbitRadiusEnd = R * Range(.55f, 1); leaf.orbitPower = .3f;
                leaf.spin = Range(-420, 420); leaf.hold = .35f; leaf.fadePower = 1.3f; leaf.startScale = .5f; leaf.endScale = 1; leaf.ease = 4;
            }
            int motes = Budget(Mathf.RoundToInt(12 * s));
            for (int i = 0; i < motes; i++)
                GraceLater(GraceHealMote(ground + new Vector2(Range(-.5f, .5f) * span, Range(-.12f, .12f) * span), i % 3 == 0 ? hot : LifeLight, leafTone, s * 1.2f), Range(0, .25f));
            for (int i = 0; i < 6; i++)
                GraceLater(GraceCrossPop(pos + new Vector2(Range(-R, R) * .75f, Range(-.6f, 1.8f) * s), Range(.6f, .95f) * s, Alpha(hot, 1), .34f), Range(.06f, .6f));
            // A companion's pulse heals the hero, not the caster: a stream of life runs from the caster to the hero and blooms there.
            var game = ArcheryGame();
            if (game != null && game.hero != null && game.hero.root)
            {
                Vector2 chest = game.hero.pos + Vector2.up * .95f;
                if ((chest - pos).sqrMagnitude > 2.25f) GraceLifeStream(pos, chest, life, hot);
            }
            if (grade < 2) return;
            // Level two turns part of the heal into a shield: a faint gold ward dome rises over the hero inside its soft halo.
            Color ward = Color.Lerp(GoldLight, Warm, .35f);
            var domeGlow = GraceDomeLayer(ground, GraceDomeWidth * 1.02f, GraceDomeHeight * 1.02f, Alpha(ward, .45f), .8f, true, 26, GraceDomeGlow);
            var dome = GraceDomeLayer(ground, GraceDomeWidth * .88f, GraceDomeHeight * .88f, Alpha(ward, .55f), .75f, true, 121);
            foreach (var clip in new[] { domeGlow, dome }) { clip.startScale = .8f; clip.endScale = 1; clip.ease = 6; clip.hold = .35f; clip.fadePower = 1.4f; GraceLater(clip, .06f); }
            for (int i = 0; i < 3; i++)
            {
                Vector2 normal = Direction(40 + i * 50);
                GraceLater(GraceTwinkle(ground + new Vector2(normal.x * GraceDomeWidth * .44f, normal.y * GraceDomeHeight * .88f), Range(.8f, 1.1f), Alpha(ward, 1), .26f), .1f + i * .06f);
            }
            if (grade < 3) return;
            // Awakened (the heal leaves a lasting field): gold-white leaves rain down through the column onto the field.
            int falls = Budget(5);
            for (int i = 0; i < falls; i++)
            {
                var leaf = GraceLater(Spawn("Grace heal gold leaf", GraceLeaf, ground + new Vector2(Range(-1.2f, 1.2f), Range(4, 5.5f)), Range(.36f, .5f), Alpha(i % 2 == 0 ? GraceAwake : ward, .95f), 1.1f, true, 130), Range(.1f, .4f));
                leaf.velocity = new Vector2(Range(-.4f, .4f), -Range(3.5f, 4.5f)); leaf.drag = .6f; leaf.spin = Range(-300, 300); leaf.fadeIn = .06f; leaf.hold = .5f; leaf.fadePower = 1.4f;
            }
        }

        // ---------------------------------------------------------------- shield

        private void CastShieldPulse(CastInfo c)
        {
            float k = Mathf.Clamp(c.size / 1.4f, .8f, 1.6f), w = c.windup, release = Mathf.Max(0, w - .02f);
            Color main = GraceVivid(c.tint, GoldLight), gold = Color.Lerp(main, GoldLight, .15f), bright = Color.Lerp(gold, Warm, .5f), amber = Color.Lerp(gold, new Color(1, .6f, .12f), .35f);
            Vector2 ground = c.pos + Vector2.down, body = c.pos + Vector2.up * .15f, hand = c.pos + new Vector2(c.facing * .3f, .4f);

            var palmGlow = Anchored(c.anchor, hand, "Grace ward palm bloom", glow, 1.6f * k, Alpha(gold, .45f), w + .08f, 28);
            var palm = Anchored(c.anchor, hand, "Grace ward palm", core, .45f * k, Alpha(bright, 1), w + .08f, 126);
            var palmRune = Anchored(c.anchor, hand, "Grace ward palm rune", GraceGlyphs[0], 1.15f * k, Alpha(gold, .95f), w + .08f, 125);
            palmRune.spin = -120; palm.pulse = .06f; palmGlow.pulse = .05f;
            foreach (var clip in new[] { palmGlow, palm, palmRune }) { clip.fadeIn = .04f; clip.startScale = .3f; clip.endScale = 1; clip.ease = 1.5f; clip.hold = .8f; clip.fadePower = 2; }
            if (w < .2f)
            {
                // An instant echo: the dome itself comes with the shield burst that follows at once.
                Converge(body, 5, 1.8f * k, bright, w, .3f);
                GraceStar(hand, 1.5f * k, Alpha(bright, 1), .18f, 132, release);
                return;
            }
            // Higher levels raise a bigger, brighter dome.
            int grade = Grade; float gl = GraceLevel(1.07f, 1.14f);
            float total = w + .35f, hold = w / total, domeW = GraceDomeWidth * k * gl, domeH = GraceDomeHeight * k * gl;

            // A faint shade behind the caster keeps the gold readable on pale skies while the ward gathers.
            var shade = Underlay(body, 4.6f * k, .18f, total); shade.hold = hold;
            // A rune seal on the floor in a pool of gold light; motes rise off it.
            var pool = GraceGroundBloom(ground, 5 * k, Alpha(gold, .42f), total);
            pool.startScale = .55f; pool.endScale = 1; pool.ease = 4; pool.hold = hold; pool.fadePower = 1.5f;
            var seal = RuneCircle(ground, 4.2f * k, Alpha(bright, .95f), total, 45, true); seal.hold = hold;
            Emitter(seal, 14 * k, 1.7f * k, (owner, p) => GraceRise(GraceOnGround(owner, p), Chance(.4f) ? Warm : gold, k * .9f));

            // The dome rises out of the floor during the wind-up: its soft halo behind the actors, the painted light shell in
            // front. It hands over to the shield burst right after the release.
            float domeLife = w + .04f;
            var riseGlow = GraceDomeLayer(ground, domeW * 1.12f, domeH * 1.12f, Alpha(gold, .4f), domeLife, true, 26, GraceDomeGlow);
            var riseShell = GraceDomeLayer(ground, domeW, domeH, Alpha(bright, .38f), domeLife, true, 121);
            foreach (var shell in new[] { riseGlow, riseShell }) { shell.axisStart = new Vector2(.8f, .03f); shell.axisEnd = Vector2.one; shell.ease = 2.4f; shell.fadeIn = .06f; shell.hold = w / domeLife; shell.fadePower = 2.2f; }

            // Plates of gold light, each in its own glow, sweep in close round the dome and slam onto its surface exactly at the
            // release, where each locks with a white-gold flash.
            int plates = Budget(8);
            for (int i = 0; i < plates; i++)
            {
                float a = Mathf.Lerp(16, 164, (i + .5f) / plates) + Range(-5, 5);
                Vector2 normal = Direction(a), at = ground + new Vector2(normal.x * domeW * .5f, normal.y * domeH) * .96f;
                float size = Range(.9f, 1.1f) * k, from = a + Range(-25, 25), sweep = (i % 2 == 0 ? 1 : -1) * Range(50, 90), radius = Range(1.1f, 1.7f) * k;
                float tilt = Range(-40, 40), spin = Range(-260, 260);
                var plate = Spawn("Grace ward plate", GracePlate, at, size, Alpha(amber, .95f), w, true, 121);
                var aura = Spawn("Grace ward plate bloom", glow, at, size * 2, Alpha(gold, .34f), w, true, 28);
                foreach (var clip in new[] { plate, aura })
                {
                    clip.orbitCenter = at; clip.orbitAngle = from; clip.orbit = sweep; clip.orbitRadius = radius; clip.orbitRadiusEnd = 0; clip.orbitPower = 1.3f;
                    clip.angle = tilt; clip.fadeIn = .06f; clip.hold = .85f; clip.fadePower = 1.5f; clip.startScale = .6f; clip.endScale = 1; clip.ease = 1;
                }
                plate.spin = spin;
                var seated = GraceLater(Spawn("Grace ward plate seated", GracePlate, at, size * 1.08f, Alpha(bright, 1), .32f, true, 122), release);
                seated.ramp = Ramp(Color.white, gold); seated.angle = tilt; seated.startScale = 1.25f; seated.endScale = 1; seated.ease = 5; seated.hold = .35f; seated.fadePower = 1.4f;
            }

            // Glyph runes, each in a soft glow, orbit the caster, close in and lock into place at the release.
            int glyphs = 4; float turn = 320, lockRadius = 1.8f * k;
            for (int i = 0; i < glyphs; i++)
            {
                float start = 45 + i * 360f / glyphs; Sprite glyph = GraceGlyphs[i % GraceGlyphs.Length];
                Vector2 at = body + Direction(start + w * turn) * lockRadius;
                var rune = Spawn("Grace ward rune", glyph, body, k, Alpha(bright, .95f), w, true, 130);
                var aura = Spawn("Grace ward rune bloom", glow, body, 1.7f * k, Alpha(gold, .36f), w, true, 28);
                foreach (var clip in new[] { rune, aura })
                {
                    clip.orbitCenter = body; clip.orbitAngle = start; clip.orbit = turn; clip.orbitRadius = 2.6f * k; clip.orbitRadiusEnd = lockRadius; clip.orbitPower = .55f;
                    clip.fadeIn = .06f; clip.hold = .92f; clip.fadePower = 2;
                }
                var locked = GraceLater(Spawn("Grace ward rune lock", glyph, at, 1.05f * k, Alpha(bright, 1), .36f, true, 131), release);
                locked.ramp = Ramp(Warm, bright); locked.startScale = 1.6f; locked.endScale = 1; locked.ease = 6; locked.hold = .3f; locked.fadePower = 1.4f;
            }
            Converge(body, 8, 2.7f * k, bright, w, .3f);
            if (grade >= 2)
            {
                // Level two: a second, wider rune ring turns the other way on the floor.
                var outer = RuneCircle(ground, 5.4f * k, Alpha(gold, .85f), total, -35, true); outer.hold = hold;
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
                    var rune = Spawn("Grace ward outer rune", glyph, body, .9f * k, Alpha(GraceAwake, .95f), w, true, 130);
                    var aura = Spawn("Grace ward outer rune bloom", glow, body, 1.5f * k, Alpha(GraceAwake, .32f), w, true, 28);
                    foreach (var clip in new[] { rune, aura })
                    {
                        clip.orbitCenter = body; clip.orbitAngle = start; clip.orbit = -turn; clip.orbitRadius = 3.1f * k; clip.orbitRadiusEnd = lockRadius * 1.3f; clip.orbitPower = .55f;
                        clip.fadeIn = .06f; clip.hold = .92f; clip.fadePower = 2;
                    }
                    var locked = GraceLater(Spawn("Grace ward outer rune lock", glyph, at, .95f * k, Alpha(GraceAwake, 1), .38f, true, 131), release);
                    locked.startScale = 1.6f; locked.endScale = 1; locked.ease = 6; locked.hold = .3f; locked.fadePower = 1.4f;
                }
            }

            // Release: a spark on the apex and shock fronts (reward and passive wards end here; skills add the burst).
            GraceStar(ground + Vector2.up * domeH * .97f, 2.6f * k, Alpha(bright, 1), .3f, 132, release);
            GraceWave(body, 4.8f * k, Alpha(bright, .85f), .34f, 3, 131, release);
            GraceGroundWave(ground, 5.6f * k, Alpha(gold, .9f), .4f, release);
        }

        private void ImpactShieldPulse(Vector2 pos, float radius, Color tint, int facing)
        {
            // A wide burst without a facing is a level-three ward breaking (radius 2.6); casts and passives draw the dome (1.1 to 1.5).
            if (facing == 0 && radius >= 2) { GraceShieldShatter(pos, radius, tint); return; }
            int grade = Grade;
            float k = Mathf.Clamp(radius / 1.2f, .7f, 1.8f) * GraceLevel(1.07f, 1.14f);
            Color main = GraceVivid(tint, GoldLight), gold = Color.Lerp(main, GoldLight, .15f), bright = Color.Lerp(gold, Warm, .5f), amber = Color.Lerp(gold, new Color(1, .6f, .12f), .35f);
            Vector2 ground = pos + Vector2.down, body = pos + Vector2.up * .35f;
            float domeW = GraceDomeWidth * k, domeH = GraceDomeHeight * k;
            Vector2 apex = ground + Vector2.up * domeH * .96f;

            // Peak: a white-hot heart, a small spark over the body, light wedges, and a big spark and a cross flare on the apex.
            var shade = Underlay(body, 4.6f * k, .2f, .42f); shade.hold = .2f;
            // The caster stays readable inside the ward: the heart and spark are kept small and short over the body.
            var heart = Spawn("Grace ward heart", core, pos, .7f * k, Color.white, .08f, true, 133); heart.startScale = .5f; heart.endScale = 1.4f; heart.ease = 4; heart.fadePower = 1.3f;
            GraceStar(body, 1.6f * k, Alpha(bright, 1), .16f);
            ImpactLines(body, 5.2f * k, Alpha(gold, .55f), .24f).startScale = .85f;
            GraceStar(apex, 2.8f * k, Alpha(Warm, 1), .28f, 133);
            GraceCrossFlare(apex, 3.6f * k, Alpha(bright, 1), .36f, 0);

            // A beacon spear of light shoots up from the apex inside a soft beam.
            var beaconGlow = GraceColumn(GraceBeam, apex, 2.4f * k, 5.4f * k, Alpha(gold, .5f), .5f, true, 26); beaconGlow.hold = .15f; beaconGlow.fadePower = 1.4f;
            var beacon = GraceColumn(GracePillar, apex, 1.05f * k, 5.6f * k, Alpha(bright, 1), .45f, true, 128); beacon.hold = .12f; beacon.fadePower = 1.4f;
            foreach (var clip in new[] { beaconGlow, beacon }) clip.ease = 6;

            // A golden aegis crest flares open behind the caster in a soft bloom.
            var crestGlow = Spawn("Grace ward crest bloom", glow, body + Vector2.up * .2f, 5.6f * k, Alpha(gold, .26f), .75f, true, 25);
            var crest = Spawn("Grace ward crest", runeRing, body + Vector2.up * .2f, 4.9f * k, Alpha(bright, .8f), .75f, true, 27); crest.spin = 40;
            foreach (var clip in new[] { crestGlow, crest }) { clip.startScale = .5f; clip.endScale = 1.12f; clip.ease = 6; clip.hold = .25f; clip.fadePower = 1.4f; }

            // The dome: a soft halo hugging its rim behind the actors and the painted light shell in front (a bright rim, a faint
            // body thickening toward it, a faint lattice), a white snap of the rim, then it holds and erodes from its thin lines.
            var domeGlow = GraceDomeLayer(ground, domeW * 1.12f, domeH * 1.12f, Alpha(gold, .3f), 1.45f, true, 26, GraceDomeGlow);
            domeGlow.startScale = .96f; domeGlow.endScale = 1; domeGlow.ease = 6; domeGlow.hold = .4f; domeGlow.fadePower = 1.4f;
            var dome = GraceDomeLayer(ground, domeW, domeH, Alpha(bright, .42f), 1.4f, true, 121);
            dome.startScale = .96f; dome.endScale = 1; dome.ease = 6; dome.hold = .35f; dome.fadePower = 1.4f;
            var snap = GraceDomeLayer(ground, domeW, domeH, Alpha(Warm, .7f), .14f, true, 132, GraceDomeRim);
            snap.startScale = 1.1f; snap.endScale = 1; snap.ease = 6; snap.fadePower = 1.3f;

            // A scan of light runs up the dome: its cells flash from the floor to the apex.
            int cells = Budget(12);
            for (int i = 0; i < cells; i++)
            {
                float a = Range(18, 162) * Mathf.Deg2Rad, rr = Range(.72f, .98f);
                Vector2 at = ground + new Vector2(Mathf.Cos(a) * domeW * .5f * rr, Mathf.Sin(a) * domeH * rr);
                var cell = GraceLater(Spawn("Grace ward cell", GracePlate, at, Range(.42f, .58f) * k, Alpha(bright, .6f), .22f, true, 130), .04f + (at.y - ground.y) / domeH * .32f);
                cell.ramp = Ramp(Color.white, gold); cell.startScale = .5f; cell.endScale = 1.1f; cell.ease = 5; cell.fadePower = 1.4f;
            }

            // A hexagonal ripple and shock fronts run outward from the dome; dust is shoved off its base.
            var ripple = Spawn("Grace ward ripple", GraceHex, body, domeW * .95f, Alpha(bright, .45f), .3f, true, 130); ripple.startScale = 1; ripple.endScale = 1.6f; ripple.ease = 4; ripple.fadePower = 1.3f; ripple.spin = 20;
            GraceWave(body, 7 * k, Alpha(gold, .6f), .4f, 3);
            GraceGroundWave(ground, 8.2f * k, Alpha(gold, .9f), .5f);
            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 0 ? 1 : -1;
                var dust = Puff(ground + new Vector2(side * domeW * Range(.4f, .5f), .1f), Range(.6f, .9f) * k, DustTone, new Vector2(side * Range(2.5f, 4.5f) * k, Range(.3f, .8f)), Range(.45f, .65f), 118);
                dust.gravity = -.3f; dust.endScale = 2.1f;
            }

            // Painted crystal slivers of light and needles burst off the dome surface.
            int shards = Budget(Mathf.RoundToInt(16 * k));
            for (int i = 0; i < shards; i++)
            {
                float angle = Mathf.Lerp(12, 168, (i + Range(.1f, .9f)) / shards);
                Vector2 normal = Direction(angle), at = ground + new Vector2(normal.x * domeW * .5f, normal.y * domeH);
                bool lit = i % 3 == 0;
                var piece = Spawn("Grace ward shard", GraceFacet, at, Range(.34f, .56f) * k, lit ? Alpha(bright, 1) : Alpha(Color.Lerp(amber, gold, Range(0, .5f)), .9f), Range(.55f, .9f), true, lit ? 129 : 121);
                piece.velocity = normal * Range(4, 8) * k + Vector2.up; piece.gravity = 10; piece.drag = 1.2f; piece.spin = Range(-600, 600); piece.angle = Range(0, 360); piece.hold = .35f; piece.fadePower = 1.2f;
            }
            for (int i = 0; i < 10; i++)
            {
                Vector2 normal = Direction(Range(10, 170)), at = ground + new Vector2(normal.x * domeW * .5f, normal.y * domeH);
                Spark(at, normal * Range(5, 9) * k, i % 2 == 0 ? Color.white : bright, Range(.3f, .48f), Range(.25f, .4f), 6, 3);
            }

            // The seal lingers in a pool of light; motes drift up the dome walls; sparkles twinkle on its surface.
            var pool = GraceGroundBloom(ground, 5.6f * k, Alpha(gold, .45f), 1.2f);
            pool.startScale = .7f; pool.endScale = 1; pool.ease = 6; pool.hold = .4f; pool.fadePower = 1.4f;
            RuneCircle(ground, 4.8f * k, Alpha(bright, .9f), 1.2f, 30, true);
            for (int i = 0; i < 8; i++)
            {
                Vector2 normal = Direction(Range(15, 165));
                GraceLater(GraceRise(ground + new Vector2(normal.x * domeW * .48f, normal.y * domeH * .95f), i % 2 == 0 ? Warm : gold, k), Range(.05f, .4f));
            }
            for (int i = 0; i < 5; i++)
            {
                Vector2 normal = Direction(Range(25, 155));
                GraceLater(GraceTwinkle(ground + new Vector2(normal.x * domeW * .5f, normal.y * domeH), Range(.8f, 1.2f) * k, Alpha(bright, 1), .28f), Range(.12f, .8f));
            }
            if (grade < 2) return;
            // Level two: a thicker shell, a second gold rim of light standing on the dome as it holds.
            var rim = GraceDomeLayer(ground, domeW * 1.05f, domeH * 1.05f, Alpha(gold, .85f), 1.5f, true, 122, GraceDomeRim);
            rim.startScale = .94f; rim.endScale = 1; rim.ease = 6; rim.hold = .45f; rim.fadePower = 1.3f;
            if (grade < 3) return;
            // Awakened (the ward bursts when it breaks): a second gold-white shell stands just outside the first, a white-gold star
            // pulses on the apex, and cracks of light run across the surface and hold there: the ward is primed to burst.
            var outer = GraceDomeLayer(ground, domeW * 1.12f, domeH * 1.12f, Alpha(GraceAwake, .7f), 1.6f, true, 123, GraceDomeRim);
            outer.startScale = .88f; outer.endScale = 1; outer.ease = 6; outer.fadeIn = .04f; outer.hold = .5f; outer.fadePower = 1.3f;
            var primed = GraceLater(GraceTwinkle(apex + Vector2.up * .35f * k, 2 * k, Alpha(GraceAwake, 1), 1.3f), .12f);
            primed.spin = 0; primed.pulse = .12f; primed.hold = .6f; primed.fadePower = 1.3f;
            for (int i = 0; i < 6; i++)
            {
                float a0 = (30 + i * 24 + Range(-8, 8)) * Mathf.Deg2Rad, a1 = a0 + Range(-.3f, .3f);
                Vector2 p0 = ground + new Vector2(Mathf.Cos(a0) * domeW * .5f, Mathf.Sin(a0) * domeH) * .98f, p1 = ground + new Vector2(Mathf.Cos(a1) * domeW * .34f, Mathf.Sin(a1) * domeH * .7f);
                var vein = GraceLater(Line(p0, p1, .18f, Alpha(GraceAwake, 1), 1.2f, 131), .12f + i * .06f);
                vein.axisStart = new Vector2(.1f, 1); vein.axisEnd = Vector2.one; vein.ease = 6; vein.hold = .6f; vein.fadePower = 1.4f; vein.flicker = .2f;
            }
        }

        /// <summary>A level-three ward bursting as it breaks (<paramref name="pos"/> is 0.8 above the hero's feet): the dome flashes
        /// white and cracks, a gold blast billows behind the hero, a spark bursts, the shell flies apart in plates and slivers of
        /// light, and a shock front runs along the floor to the burst's reach.</summary>
        private void GraceShieldShatter(Vector2 pos, float radius, Color tint)
        {
            float R = Mathf.Clamp(radius, 1.5f, 4.5f), k = Mathf.Clamp(R / 2.6f, .7f, 1.5f);
            Color main = GraceVivid(tint, GoldLight), gold = Color.Lerp(main, GoldLight, .15f), bright = Color.Lerp(gold, Warm, .5f), amber = Color.Lerp(gold, new Color(1, .6f, .12f), .35f);
            Vector2 feet = pos + Vector2.down * .8f, body = feet + Vector2.up * 1.1f;
            float domeW = GraceDomeWidth, domeH = GraceDomeHeight;
            var shade = Underlay(body, R * 2, .24f, .4f); shade.hold = .2f;
            // The dome's last moment: a white flash of the whole shell that swells as it gives way, inside its gold halo.
            var crackFlash = GraceDomeLayer(feet, domeW, domeH, Color.white, .14f, true, 132); crackFlash.startScale = 1; crackFlash.endScale = 1.12f; crackFlash.ease = 4; crackFlash.fadePower = 1.3f;
            var shellGlow = GraceDomeLayer(feet, domeW * 1.12f, domeH * 1.12f, Alpha(gold, .6f), .25f, true, 26, GraceDomeGlow); shellGlow.startScale = 1; shellGlow.endScale = 1.25f; shellGlow.ease = 4; shellGlow.fadePower = 1.4f;
            // Cracks of light race from the heart to the shell.
            for (int i = 0; i < 7; i++)
            {
                float a = (18 + i * 24 + Range(-8, 8)) * Mathf.Deg2Rad;
                Vector2 rim = feet + new Vector2(Mathf.Cos(a) * domeW * .5f, Mathf.Sin(a) * domeH) * .97f, mid = Vector2.Lerp(body, rim, .5f) + Inside(.3f);
                foreach (var seg in new[] { new[] { body, mid }, new[] { mid, rim } })
                {
                    var crackLight = Line(seg[0], seg[1], .13f, Alpha(bright, 1), .18f, 132, 1.1f); crackLight.fadePower = 1.3f;
                }
            }
            var heart = Spawn("Grace ward break heart", core, body, 1.2f * k, Color.white, .12f, true, 133); heart.startScale = .5f; heart.endScale = 1.8f; heart.ease = 4; heart.fadePower = 1.3f;
            Blast(body, 3.8f * k, Alpha(bright, 1), .6f, 28);
            GraceStar(body, 4 * k, Alpha(Warm, 1), .26f, 133);
            ImpactLines(body, R * 2.5f, Alpha(bright, .95f), .26f);
            // The burst's reach: a gold shock front on the floor whose rim lands on the radius, and painted wedges.
            float reach = R * 2 / .74f;
            GraceGroundWave(feet + Vector2.up * .05f, reach, Alpha(gold, .95f), .45f);
            var floorBurst = Spawn("Grace ward break floor", burst, feet + Vector2.up * .05f, reach * .85f, Alpha(bright, 1), .32f, true, 26);
            GraceFlat(floorBurst, .3f); floorBurst.angle = Range(0, 360); floorBurst.startScale = .4f; floorBurst.endScale = 1; floorBurst.ease = 5; floorBurst.fadePower = 1.3f;
            // The shell flies apart: plates of gold light tumble out from where they sat on the dome, with crystal slivers.
            int plates = Budget(10);
            for (int i = 0; i < plates; i++)
            {
                float a = Mathf.Lerp(14, 166, (i + .5f) / plates) + Range(-6, 6);
                Vector2 normal = Direction(a), at = feet + new Vector2(normal.x * domeW * .5f, normal.y * domeH) * .95f;
                bool lit = i % 3 == 0;
                var plate = Spawn("Grace ward plate shard", GracePlate, at, Range(.65f, .9f) * k, lit ? Alpha(bright, 1) : Alpha(amber, .9f), Range(.5f, .7f), true, lit ? 130 : 121);
                plate.velocity = normal * Range(3.5f, 5.5f) * k + Vector2.up * .8f; plate.gravity = 9; plate.drag = 1.6f; plate.spin = Range(-700, 700); plate.angle = Range(0, 360); plate.hold = .3f; plate.fadePower = 1.2f;
            }
            int facets = Budget(8);
            for (int i = 0; i < facets; i++)
            {
                Vector2 normal = Direction(Range(10, 170));
                var piece = Spawn("Grace ward facet", GraceFacet, body + normal * .4f, Range(.36f, .56f) * k, i % 2 == 0 ? Alpha(bright, 1) : Alpha(amber, .9f), Range(.45f, .7f), true, i % 2 == 0 ? 129 : 121);
                piece.velocity = normal * Range(5, 8) * k; piece.gravity = 8; piece.drag = 1.4f; piece.spin = Range(-600, 600); piece.angle = Range(0, 360); piece.fadePower = 1.2f;
            }
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? 1 : -1;
                var dust = Puff(feet + new Vector2(side * Range(.4f, 1.2f), .12f), Range(.6f, .9f) * k, DustTone, new Vector2(side * Range(3, 5) * k, Range(.3f, .8f)), Range(.5f, .7f), 118);
                dust.gravity = -.3f; dust.endScale = 2.2f;
            }
            for (int i = 0; i < 3; i++) GraceLater(GraceRise(feet + new Vector2(Range(-1, 1) * R * .6f, .1f), i % 2 == 0 ? Warm : gold, k), Range(.1f, .35f));
        }

        /// <summary>A shield soaking up a hit on the hero; frequent, so it stays tiny (9 clips, 0.24 s): a hex ward flashes
        /// where it is struck and a spark flares on the impact.</summary>
        public void ShieldBlock(Vector2 pos, Color tint)
        {
            EnsureTextures();
            Color gold = GraceVivid(tint, GoldLight), bright = Color.Lerp(gold, Warm, .5f);
            var lattice = Spawn("Grace block lattice", GraceHex, pos, 2.3f, Alpha(bright, 1), .2f, true, 130); lattice.startScale = .82f; lattice.endScale = 1.12f; lattice.ease = 5; lattice.fadePower = 1.4f;
            Vector2 normal = Direction(Range(20, 160)), hit = pos + normal * 1.05f;
            GraceStar(hit, 1.4f, Alpha(Warm, 1), .14f, 132);
            GraceTwinkle(hit, 1.2f, Alpha(bright, 1), .18f);
            GraceWave(hit, 1.15f, Alpha(bright, .9f), .16f, 3);
            for (int i = 0; i < 4; i++) Spark(hit, normal * Range(3, 6) + Direction(Range(0, 360)) * 2, i % 2 == 0 ? Color.white : bright, Range(.24f, .34f), Range(.16f, .24f), 6, 3);
        }

        // ---------------------------------------------------------------- life drain

        private void CastLifeDrain(CastInfo c)
        {
            MysticEnsure();
            float k = Mathf.Clamp(c.size / 1.43f, .8f, 1.6f), w = c.windup, release = Mathf.Max(0, w - .02f);
            Color life = Color.Lerp(GraceVivid(c.tint, LifeLight), LifeLight, .3f);
            Vector2 hand = c.pos + new Vector2(c.facing * .8f, .1f), ground = c.pos + Vector2.down;
            if (w < .2f)
            {
                // An instant echo: the drain itself follows at once, so the echo only flicks a small whirl open round a plasma heart.
                var curl = Anchored(c.anchor, hand, "Grace drain echo swirl", MysticSpiral, 1.9f * k, Alpha(GraceCrimson, .9f), w + .12f, 125);
                curl.spin = 680;
                var pit = GraceAnchoredPlasma(c.anchor, hand, "Grace drain echo heart", .75f * k, Alpha(GraceCrimson, 1), w + .1f, 127);
                foreach (var clip in new[] { curl, pit }) { clip.startScale = .3f; clip.endScale = 1; clip.ease = 2; clip.fadeIn = .03f; clip.hold = .6f; clip.fadePower = 1.6f; }
                Converge(hand, 5, 1.8f * k, life, w, .5f);
                return;
            }
            float total = w + .6f, hold = w / total;
            // Higher levels open a bigger whirl (more souls follow from the particle budget).
            int grade = Grade; k *= GraceLevel(1.1f, 1.2f);

            // A painted blood whirl opens in the hand: crimson light strokes and pale life strokes turning against each other in a
            // crimson bloom, round a plasma heart with a white-hot core and a crimson rim.
            var shadow = Underlay(hand, 3.4f * k, .26f, total); shadow.hold = hold; shadow.fadePower = 1.5f;
            var bloom = Anchored(c.anchor, hand, "Grace drain bloom", glow, 3.2f * k, Alpha(GraceCrimson, .55f), total, 28); bloom.pulse = .05f;
            var crimson = Anchored(c.anchor, hand, "Grace drain crimson whirl", MysticSpiral, 2.7f * k, Alpha(GraceCrimson, .95f), total, 125);
            crimson.spin = 640; crimson.angle = Range(0, 360);
            var spirit = Anchored(c.anchor, hand, "Grace drain life whirl", MysticSpiral, 1.7f * k, Alpha(life, .9f), total, 126);
            spirit.spin = -520; spirit.angle = Range(0, 360);
            var heart = GraceAnchoredPlasma(c.anchor, hand, "Grace drain heart", 1.2f * k, Alpha(GraceCrimson, 1), total, 127);
            var hotCore = Anchored(c.anchor, hand, "Grace drain core", core, .34f * k, Alpha(GraceSoulHot, 1), total, 128); hotCore.pulse = .1f;
            var rim = Anchored(c.anchor, hand, "Grace drain rim", GraceShock, 1.4f * k, Alpha(GraceCrimson, 1), total, 129); rim.pulse = .09f;
            foreach (var clip in new[] { bloom, crimson, spirit, heart, hotCore, rim }) { clip.startScale = .25f; clip.endScale = 1; clip.ease = 2.5f; clip.fadeIn = .05f; clip.hold = hold; clip.fadePower = 1.5f; }

            // Crimson smoke, plasma soul motes, soul comets and life motes spiral into the whirl.
            int fumes = Budget(3);
            for (int i = 0; i < fumes; i++)
            {
                var fume = Puff(hand, Range(.9f, 1.3f) * k, GraceSmoke, Vector2.zero, w * Range(.8f, 1), 116);
                fume.orbitCenter = hand; fume.orbitAngle = i * 360f / fumes + Range(-15, 15); fume.orbit = -Range(300, 420);
                fume.orbitRadius = Range(2, 2.8f) * k; fume.orbitRadiusEnd = .1f; fume.orbitPower = 1.3f;
                fume.startScale = 1; fume.endScale = .3f; fume.fadeIn = .08f; fume.hold = .6f; fume.fadePower = 1.3f;
            }
            Converge(hand, 10, 3 * k, life, w, .55f);
            int souls = Budget(10);
            for (int i = 0; i < souls; i++)
            {
                bool mote = i % 2 == 0;
                var soul = mote ? GracePlasma(hand, Range(.5f, .7f) * k, Alpha(GraceCrimson, 1), w * Range(.75f, 1), 127)
                    : Spawn("Grace drain soul", GraceSoul, hand, Range(.9f, 1.3f) * k, Alpha(i % 4 == 1 ? GraceSoulHot : GraceCrimson, 1), w * Range(.75f, 1), true, 128);
                soul.orbitCenter = hand; soul.orbitAngle = Range(0, 360); soul.orbit = -Range(260, 380); soul.orbitRadius = Range(2.2f, 3.2f) * k; soul.orbitRadiusEnd = 0; soul.orbitPower = 1.5f;
                soul.alignVelocity = !mote; soul.fadeIn = .06f; soul.hold = .75f; soul.endScale = .5f; soul.ease = 1;
            }

            // A crimson sigil burns under the caster in a pool of crimson light.
            var pool = GraceGroundBloom(ground, 4.6f * k, Alpha(GraceCrimson, .4f), total);
            var sig = Spawn("Grace drain sigil", sigil, ground, 4 * k, Alpha(GraceCrimson, .9f), total, true, 27); GraceFlat(sig, .34f); sig.spin = -40;
            foreach (var clip in new[] { pool, sig }) { clip.fadeIn = .06f; clip.startScale = .6f; clip.endScale = 1; clip.ease = 3; clip.hold = hold; clip.fadePower = 1.5f; }

            // A blood sigil turns behind the caster in a soft crimson bloom (behind the actors, so the hero stays readable).
            Vector2 back = c.pos + Vector2.up * .25f;
            var ritualGlow = Spawn("Grace drain ritual bloom", glow, back, 4.2f * k, Alpha(GraceCrimson, .32f), total, true, 25);
            var ritual = Spawn("Grace drain ritual", sigil, back, 3.5f * k, Alpha(GraceCrimson, .8f), total, true, 27); ritual.spin = 60;
            foreach (var clip in new[] { ritualGlow, ritual }) { clip.startScale = .3f; clip.endScale = 1; clip.ease = 2; clip.fadeIn = .06f; clip.hold = hold; clip.fadePower = 1.6f; }

            // The victim is branded with a turning crimson rune and starts to bleed light toward the hand.
            Vector2 mark = c.target + Vector2.down * .25f, toHand = hand - mark;
            if (toHand.magnitude > 1)
            {
                // Two luminous crimson tendrils (a thorned ribbon of light with a hot thread inside) reach out of the whirl and
                // hook the victim.
                float reach = w * .55f, tendrilLife = w + .25f;
                for (int strand = 0; strand < 2; strand++)
                {
                    int variant = strand;
                    float thick = (strand == 0 ? .95f : .75f) * k;
                    var vine = MysticVine(hand, mark, MysticTendril[variant], thick, Alpha(GraceCrimson, .75f), tendrilLife, true, 126);
                    var thread = MysticVine(hand, mark, MysticTendrilLight[variant], thick, Alpha(GraceSoulHot, 1), tendrilLife - .05f, true, 127);
                    foreach (var clip in new[] { vine, thread })
                    {
                        if (strand == 1) clip.scale.y *= -1;
                        clip.axisStart = new Vector2(.03f, 1); clip.axisEnd = Vector2.one; clip.ease = GracePop(clip.life, reach); clip.hold = .6f; clip.fadePower = 1.3f;
                    }
                    thread.flicker = .15f;
                }
                float brandLife = w + .3f;
                var brandShade = Underlay(mark, 2.6f * k, .22f, brandLife); brandShade.hold = .6f;
                var brandGlow = Spawn("Grace drain brand bloom", glow, mark, 3 * k, Alpha(GraceCrimson, .45f), brandLife, true, 124);
                brandGlow.fadeIn = .06f; brandGlow.startScale = .6f; brandGlow.endScale = 1; brandGlow.ease = 5; brandGlow.hold = .5f; brandGlow.fadePower = 1.4f;
                var brand = RuneCircle(mark, 2.3f * k, Alpha(GraceCrimson, 1), brandLife, 220, false, 126); brand.hold = .5f;
                for (int i = 0; i < 6; i++)
                {
                    bool green = i % 2 == 0;
                    var seep = green ? Spawn("Grace drain seep", GraceSoul, mark + Inside(.35f), Range(.7f, 1), Alpha(life, 1), w * .6f, true, 127)
                        : GracePlasma(mark + Inside(.35f), Range(.4f, .55f), Alpha(GraceCrimson, 1), w * .6f, 127);
                    GraceLater(seep, Range(.04f, w * .4f));
                    seep.velocity = toHand / (w * .6f) * Range(.45f, .75f); seep.alignVelocity = green; seep.fadeIn = .05f; seep.endScale = .6f; seep.ease = 1;
                }
            }

            if (grade >= 3)
            {
                // Awakened (the drain heals twice as much): a gold-white ring of life turns round the whirl's rim.
                var crown = Anchored(c.anchor, hand, "Grace drain awakened rim", runeRing, 2.4f * k, Alpha(Color.Lerp(GraceAwake, LifeLight, .3f), .9f), total, 129);
                crown.spin = 200; crown.startScale = .3f; crown.endScale = 1; crown.ease = 2.5f; crown.fadeIn = .08f; crown.hold = hold; crown.fadePower = 1.6f;
            }

            // Release: the ring implodes, then the whirl snaps with a crimson spark and a shock front (the drained life comes back
            // later).
            var implode = GraceLater(Spawn("Grace drain implode", GraceShock, hand, 4.2f * k, Alpha(GraceCrimson, 1), .16f, true, 131), Mathf.Max(0, w - .15f));
            implode.startScale = 1; implode.endScale = .12f; implode.ease = 1.5f; implode.fadePower = .6f;
            var snap = GraceLater(Spawn("Grace drain snap", core, hand, .75f * k, Color.white, .1f, true, 133), release); snap.startScale = .4f; snap.endScale = 1.5f; snap.ease = 4; snap.fadePower = 1.3f;
            GraceStar(hand, 2.6f * k, Alpha(GraceCrimson, 1), .24f, 132, release);
            GraceWave(hand, 3.2f * k, Alpha(GraceCrimson, .9f), .3f, 3, 131, release);
            GraceLater(ImpactLines(hand, 4 * k, Alpha(GraceCrimson, 1), .22f), release);
        }

        private void ImpactLifeDrain(Vector2 pos, float radius, Color tint, int facing)
        {
            float k = Mathf.Clamp(radius * 1.35f, 1, 3);
            Color life = Color.Lerp(GraceVivid(tint, LifeLight), LifeLight, .3f), hot = Color.Lerp(life, Color.white, .45f);
            GraceRip(pos, k);
            // A ghost of light is torn upward out of the body, followed by wavering life comets and plasma soul motes.
            var ghost = Spawn("Grace torn ghost", GraceSoul, pos + Vector2.up * .3f * k, 1.8f * k, Alpha(hot, 1), .5f, true, 128);
            ghost.angle = 90; ghost.velocity = Vector2.up * 4 * k; ghost.drag = 2.5f;
            ghost.axisStart = new Vector2(.5f, 1.2f); ghost.axisEnd = new Vector2(1.5f, .75f); ghost.ease = 3; ghost.hold = .2f; ghost.fadePower = 1.3f;
            int souls = Budget(Mathf.RoundToInt(6 * k));
            for (int i = 0; i < souls; i++)
            {
                bool green = i % 3 != 0;
                var soul = green ? Spawn("Grace torn soul", GraceSoul, pos + Inside(.3f * k), Range(.6f, .9f) * k, Alpha(hot, 1), Range(.5f, .8f), true, 129)
                    : GracePlasma(pos + Inside(.3f * k), Range(.4f, .55f) * k, Alpha(GraceCrimson, 1), Range(.5f, .8f), 128);
                GraceLater(soul, Range(0, .12f));
                soul.orbitCenter = soul.position; soul.velocity = new Vector2(Range(-1, 1), Range(3.5f, 5.5f)) * k; soul.drag = 1.2f;
                soul.orbitAngle = Range(0, 360); soul.orbit = (i % 2 == 0 ? 1 : -1) * Range(500, 700); soul.orbitRadius = .25f * k; soul.orbitRadiusEnd = .05f;
                soul.alignVelocity = green; soul.fadeIn = .04f; soul.hold = .3f; soul.fadePower = 1.3f; soul.endScale = .5f; soul.ease = 1;
            }
            GraceWave(pos, 3.2f * k, Alpha(life, .9f), .34f, 3, 131, .05f);
        }

        private void TrailLifeDrain(Vector2 from, Vector2 to, Color tint)
        {
            Vector2 delta = to - from; float length = delta.magnitude; if (length < .05f) return;
            Vector2 dir = delta / length, side = new Vector2(-dir.y, dir.x);
            Color life = Color.Lerp(GraceVivid(tint, LifeLight), LifeLight, .3f), hot = Color.Lerp(life, Color.white, .5f);
            // Higher levels pull a thicker, wider river.
            int grade = Grade; float gl = GraceLevel(1.12f, 1.25f);
            float travel = Mathf.Clamp(length / 13, .15f, .42f), flow = .7f, thick = Mathf.Clamp(length * .26f, .9f, 1.7f) * gl;
            float amp = thick * .28f, winding = 360 * 1.25f / travel, angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // The victim: the soul is ripped out in a crimson blast and its ghost is torn loose toward the caster; crimson smoke
            // keeps curling from the wound.
            GraceRip(from, 1.35f);
            var ghost = Spawn("Grace torn spirit", GraceSoul, from + dir * .4f, 2.3f, Alpha(hot, 1), .42f, true, 128);
            ghost.angle = angle; ghost.velocity = dir * Mathf.Min(length, 4) * 1.8f; ghost.drag = 2.2f;
            ghost.axisStart = new Vector2(.5f, 1.2f); ghost.axisEnd = new Vector2(1.7f, .7f); ghost.ease = 3; ghost.hold = .15f; ghost.fadePower = 1.3f;
            for (int i = 0; i < 2; i++)
            {
                var wound = GraceLater(Puff(from + Inside(.25f), Range(.8f, 1.1f), GraceSmoke, new Vector2(Range(-.4f, .4f), Range(.6f, 1.2f)), Range(.6f, .8f), 115), .15f + i * .2f);
                wound.endScale = 2.2f; wound.drag = 1.5f;
            }

            // The soul river: a luminous crimson ribbon stretched from the victim to the caster (its strands wind and flow by
            // themselves, white-hot at their hearts) in a soft crimson glow, with a thinner strand of life winding a quarter turn
            // apart. It shoots across in the travel time, holds while the souls flow, then erodes from its thin edges.
            float riverLife = travel + flow + .2f, riverHold = (travel + flow * .7f) / riverLife, grow = GracePop(riverLife, travel);
            var glowRiver = GraceRiver(from, to, GraceRibbonFrames, thick * 2.6f, Alpha(GraceCrimson, .5f), riverLife, true, 124);
            var crimsonRiver = GraceRiver(from, to, GraceStream, thick, Alpha(GraceCrimson, 1), riverLife, true, 126);
            var vital = GraceRiver(from, to, GraceStreamShift, thick * .62f, Alpha(grade >= 3 ? Color.Lerp(life, GraceAwake, .3f) : life, 1), riverLife, true, 127);
            foreach (var clip in new[] { glowRiver, crimsonRiver, vital }) { clip.axisStart = new Vector2(.03f, 1); clip.axisEnd = Vector2.one; clip.ease = grow; clip.hold = riverHold; clip.fadePower = 1.3f; }

            // Plasma soul motes, soul comets and needles pour along the river from the wound, winding round it, each arriving at
            // the caster.
            float speed = length / travel;
            int pour = 0;
            // About 14 river particles are alive at once whatever the length of the stream.
            Emitter(vital, Mathf.Clamp(14 / travel, 30, 48) * GraceLevel(1.1f, 1.2f), 0, (owner, p) =>
            {
                if (owner.age > travel + flow) return;
                GraceDrainFlow(from, dir, speed, travel, amp, winding, pour++, life, hot);
            });

            // The caster drinks it in: an imploding crimson ring, a spark of life, a column of life in a soft beam, shock fronts,
            // a second gulp when the last soul lands and rising crosses. A second river landing with the first (level two and up
            // drain two victims) only adds its own gulp.
            float arrive = travel * .85f;
            Vector2 feet = to + Vector2.down * .95f;
            var gulp = GraceLater(Spawn("Grace drain gulp", GraceShock, to, 3.6f, Alpha(GraceCrimson, 1), .22f, true, 131), travel * .7f); gulp.startScale = 1; gulp.endScale = .15f; gulp.ease = 1.5f; gulp.fadePower = .8f;
            if (GraceRecent(GraceDrinkMark, .3f, to, 1.5f)) { GraceStar(to, 1.7f, Alpha(hot, 1), .2f, 132, travel); return; }
            var drink = GraceStar(to, 3.2f, Alpha(life, 1), .3f, 132, arrive); GraceDrinkMark = drink;
            GraceStar(to, 2, Alpha(hot, 1), .2f, 133, arrive);
            var surgeGlow = GraceLater(GraceColumn(GraceBeam, feet, 3.4f, 4.6f, Alpha(life, .5f), flow, true, 26), arrive); surgeGlow.hold = .35f; surgeGlow.fadePower = 1.3f; surgeGlow.ease = 6;
            var surge = GraceLater(GraceColumn(GracePillar, feet, 1.6f, 4.4f, Alpha(life, 1), flow * .85f, true, 27), arrive); surge.hold = .3f; surge.fadePower = 1.3f; surge.ease = 6;
            GraceWave(to, 4.2f, Alpha(life, .9f), .38f, 3, 131, travel + .04f);
            GraceCrossFlare(to, 2.6f, Alpha(hot, 1), .3f, travel);
            var last = GraceLater(Spawn("Grace drain sated", core, to, .65f, Alpha(hot, 1), .12f, true, 133), travel + flow); last.startScale = .4f; last.endScale = 1.5f; last.ease = 4; last.fadePower = 1.3f;
            GraceWave(to, 3.4f, Alpha(hot, .85f), .34f, 3, 131, travel + flow);
            for (int i = 0; i < 4; i++) GraceLater(GraceRise(to + Inside(.6f), i % 2 == 0 ? hot : life, 1.1f), travel + Range(0, flow));
            if (grade < 3) return;
            // Awakened (twice the healing): life overflows. A gold-white column climbs inside the green surge, a ring of life
            // crowns the hero's head and twin streams of light spiral up round the body.
            var overflow = GraceLater(GraceColumn(GracePillar, feet, .8f, 5.2f, Alpha(GraceAwake, 1), flow * .8f, true, 28), arrive + .05f); overflow.hold = .3f; overflow.fadePower = 1.3f; overflow.ease = 6;
            // Twice the healing, shown literally: a second gulp blooms a beat after the first, with its own shock front of life.
            GraceStar(to, 3, Alpha(Color.Lerp(life, GraceAwake, .5f), 1), .32f, 132, arrive + .18f);
            GraceWave(to, 4.4f, Alpha(GraceAwake, .9f), .38f, 3, 131, arrive + .2f);
            var crown = GraceLater(Spawn("Grace drain crown", runeRing, to + Vector2.up * 1.25f, 2.4f, Alpha(GraceAwake, .95f), flow, true, 131), arrive);
            GraceFlat(crown, .32f); crown.spin = 160; crown.startScale = .3f; crown.endScale = 1; crown.ease = 4; crown.hold = .5f; crown.fadePower = 1.4f;
            int spirals = Budget(6);
            for (int i = 0; i < spirals; i++)
            {
                bool cross = i % 2 == 0;
                var mote = GraceLater(Spawn("Grace drain overflow", cross ? GraceCross : fleck, feet + Vector2.up * .3f, Range(.24f, .36f), Alpha(i % 3 == 0 ? GraceAwake : hot, 1), flow + .2f, true, 131), arrive + i * .05f);
                mote.orbitCenter = feet + Vector2.up * .3f; mote.velocity = Vector2.up * 2.6f; mote.orbitAngle = i * 360f / spirals; mote.orbit = (i % 2 == 0 ? 1 : -1) * 420;
                mote.orbitRadius = .75f; mote.orbitRadiusEnd = .35f; mote.fadeIn = .04f; mote.hold = .5f; mote.fadePower = 1.3f;
            }
        }

        /// <summary>A short stream of life from a healer to the hero it heals: a painted river in a soft glow, motes flowing
        /// along it, and a spark, a shock front and rising crosses on the hero when it arrives.</summary>
        private void GraceLifeStream(Vector2 from, Vector2 to, Color life, Color hot)
        {
            Vector2 delta = to - from; float length = delta.magnitude;
            float travel = Mathf.Clamp(length / 14, .12f, .3f), thick = Mathf.Clamp(length * .14f, .5f, .9f), streamLife = travel + .4f;
            var halo = GraceRiver(from, to, GraceRibbonFrames, thick * 2.4f, Alpha(life, .45f), streamLife, true, 126);
            var stream = GraceRiver(from, to, GraceStreamShift, thick, Alpha(life, 1), streamLife, true, 127);
            foreach (var clip in new[] { halo, stream }) { clip.axisStart = new Vector2(.03f, 1); clip.axisEnd = Vector2.one; clip.ease = GracePop(streamLife, travel); clip.hold = .55f; clip.fadePower = 1.3f; }
            for (int i = 0; i < 4; i++)
            {
                var mote = GraceLater(Spawn("Grace life stream mote", i % 2 == 0 ? GraceCross : fleck, from, Range(.26f, .36f), Alpha(i % 2 == 0 ? hot : life, 1), travel, true, 129), i * .05f);
                mote.velocity = delta / travel; mote.spin = 300; mote.hold = .7f; mote.fadePower = 1.3f;
            }
            GraceStar(to, 2.2f, Alpha(life, 1), .26f, 132, travel);
            GraceWave(to, 2.8f, Alpha(hot, .85f), .34f, 3, 131, travel);
            for (int i = 0; i < 3; i++) GraceLater(GraceRise(to + Inside(.5f), i % 2 == 0 ? hot : life, 1), travel + Range(0, .25f));
        }

        /// <summary>One particle of the soul river. Kinds alternate, so plasma soul motes, crimson comets and life comets wind
        /// round each other.</summary>
        private void GraceDrainFlow(Vector2 from, Vector2 dir, float speed, float travel, float amp, float winding, int n, Color life, Color hot)
        {
            int kind = n % 6;
            bool plasma = kind == 2 || kind == 5, needle = kind == 4, green = kind == 0 || kind == 4;
            Color tone = kind == 0 ? Alpha(hot, 1) : kind == 4 ? Alpha(life, 1) : kind == 1 ? Alpha(GraceSoulHot, 1) : Alpha(GraceCrimson, 1);
            var soul = plasma ? GracePlasma(from, Range(.55f, .75f), tone, travel, 128)
                : Spawn(needle ? "Grace river needle" : "Grace river soul", needle ? spark : GraceSoul, from, needle ? Range(.45f, .65f) : Range(1.1f, 1.5f), tone, travel, true, needle ? 128 : 129);
            soul.orbitCenter = from; soul.velocity = dir * speed; soul.orbitAngle = (green ? 0 : 180) + Range(-20, 20); soul.orbit = winding;
            soul.orbitRadius = amp * Range(.7f, .95f); soul.orbitRadiusEnd = .06f; soul.orbitPower = 1.6f;
            soul.alignVelocity = !plasma; soul.fadeIn = .02f; soul.hold = .7f; soul.fadePower = 1.3f; soul.startScale = 1; soul.endScale = plasma ? .7f : .55f; soul.ease = 1;
        }

        // A soul ripped out of a body: a white heart, a crimson blast, a spark, wedges, a collapsing whirl and needles.
        private void GraceRip(Vector2 pos, float k)
        {
            MysticEnsure();
            var shade = Underlay(pos, 3 * k, .28f, .55f); shade.hold = .3f;
            var flash = Spawn("Grace rip flash", core, pos, .85f * k, Color.white, .1f, true, 133); flash.startScale = .5f; flash.endScale = 1.7f; flash.ease = 4; flash.fadePower = 1.3f;
            Blast(pos, 2.1f * k, Alpha(GraceCrimson, 1), .45f, 127);
            GraceStar(pos, 2.4f * k, Alpha(GraceSoulHot, 1), .26f, 132);
            ImpactLines(pos, 4 * k, Alpha(GraceCrimson, 1), .24f);
            var whirl = Spawn("Grace rip whirl", MysticSpiral, pos, 1.7f * k, Alpha(GraceCrimson, .95f), .32f, true, 129); whirl.spin = 640; whirl.angle = Range(0, 360); whirl.startScale = 1.15f; whirl.endScale = .3f; whirl.ease = 2; whirl.fadePower = 1.2f;
            for (int i = 0; i < 7; i++) Spark(pos, Direction(i * 360f / 7 + Range(-15, 15)) * Range(4, 8) * k, i % 2 == 0 ? Color.white : GraceCrimson, Range(.3f, .46f), Range(.22f, .36f), 4, 3);
        }

        // ---------------------------------------------------------------- shared helpers

        /// <summary>A grace skill's growth with its level: 1 at level one, <paramref name="two"/> and <paramref name="three"/> above.</summary>
        private float GraceLevel(float two, float three) { return Grade >= 3 ? three : Grade == 2 ? two : 1; }

        /// <summary>The ease that makes a scale change finish within <paramref name="seconds"/> of a clip's <paramref name="life"/>.</summary>
        private static float GracePop(float life, float seconds)
        {
            float f = Mathf.Clamp(seconds / Mathf.Max(.01f, life), .01f, .9f);
            return Mathf.Clamp(Mathf.Log(.04f) / Mathf.Log(1 - f), 1, 80);
        }

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

        /// <summary>A hit flash for releases, locks, blocks and drinks: the hand-drawn spark (it flares, splits into shards and
        /// scatters) over a soft bloom, starting after <paramref name="delay"/>. Returns the spark.</summary>
        private Clip GraceStar(Vector2 pos, float width, Color color, float life, int order = 132, float delay = 0)
        {
            var star = SparkFlip(pos, width, color, life, order);
            var bloom = Spawn("Grace spark bloom", glow, pos, width * 1.15f, Alpha(color, color.a * .4f), star.life, true, order - 3);
            bloom.startScale = .6f; bloom.endScale = 1.2f; bloom.ease = 3; bloom.fadePower = 1.6f;
            if (delay > 0) { GraceLater(star, delay); GraceLater(bloom, delay); }
            return star;
        }

        /// <summary>A tall holy cross of two spindle strokes (white-cored), snapping open after <paramref name="delay"/>.</summary>
        private void GraceCrossFlare(Vector2 pos, float height, Color color, float life, float delay)
        {
            for (int i = 0; i < 2; i++)
            {
                bool upright = i == 0;
                var ray = GraceLater(Spawn("Grace cross flare", streak, pos + (upright ? Vector2.up * height * .08f : Vector2.zero), upright ? height : height * .55f, color, life * (upright ? 1 : .8f), true, 132), delay);
                ray.scale.y *= upright ? .2f : .26f; ray.angle = upright ? 90 : 0; ray.startScale = .3f; ray.endScale = 1; ray.ease = 7; ray.hold = .3f; ray.fadePower = 1.2f;
            }
        }

        /// <summary>A holy cross mote that pops open, holds and erodes.</summary>
        private Clip GraceCrossPop(Vector2 pos, float width, Color color, float life)
        {
            var cross = Spawn("Grace cross", GraceCross, pos, width, color, life, true, 133);
            cross.startScale = .3f; cross.endScale = 1; cross.ease = 6; cross.hold = .35f; cross.fadePower = 1.2f; cross.angle = Range(-12, 12);
            return cross;
        }

        /// <summary>A four-point sparkle that snaps open on a surface.</summary>
        private Clip GraceTwinkle(Vector2 pos, float width, Color color, float life)
        {
            var mote = Spawn("Grace twinkle", fleck, pos, width, color, life, true, 133);
            mote.startScale = .2f; mote.endScale = 1; mote.ease = 6; mote.hold = .3f; mote.fadePower = 1.3f; mote.angle = Range(-15, 15);
            return mote;
        }

        /// <summary>A rising mote of grace: a holy cross that pops open and drifts up, or a needle of light.</summary>
        private Clip GraceRise(Vector2 at, Color color, float scale)
        {
            bool cross = Chance(.45f);
            var mote = Spawn(cross ? "Grace rising cross" : "Grace rising light", cross ? GraceCross : spark, at, (cross ? Range(.26f, .4f) : Range(.32f, .5f)) * scale, Alpha(color, 1), Range(.55f, 1), true, 131);
            mote.velocity = new Vector2(Range(-.25f, .25f), Range(1.6f, 3.4f) * Mathf.Max(.7f, scale)); mote.drag = .3f; mote.hold = .45f; mote.fadePower = 1.2f;
            if (cross) { mote.startScale = .35f; mote.endScale = 1; mote.ease = 6; }
            else { mote.alignVelocity = true; mote.endScale = .35f; mote.ease = 1; mote.fadeIn = .04f; }
            return mote;
        }

        /// <summary>A rising healing mote: mostly light, sometimes a painted leaf.</summary>
        private Clip GraceHealMote(Vector2 at, Color light, Color leafTone, float scale)
        {
            if (!Chance(.3f)) return GraceRise(at, light, scale);
            var leaf = Spawn("Grace rising leaf", GraceLeaf, at, Range(.3f, .42f) * scale, Alpha(leafTone, .95f), Range(.6f, 1), false, 121);
            leaf.velocity = new Vector2(Range(-.4f, .4f), Range(1.4f, 2.8f) * Mathf.Max(.7f, scale)); leaf.drag = .4f; leaf.spin = Range(-360, 360); leaf.angle = Range(0, 360);
            leaf.fadeIn = .05f; leaf.hold = .4f; leaf.fadePower = 1.3f; leaf.startScale = .5f; leaf.endScale = 1; leaf.ease = 4;
            return leaf;
        }

        /// <summary>The tone painted leaves take for a heal colour: the same hue, deep and saturated, which their soft shading
        /// lightens toward the upper left.</summary>
        private static Color GraceLeafTone(Color main) { return GraceDeep(main, .88f, .95f); }

        private Clip GraceColumn(Sprite sprite, Vector2 ground, float width, float height, Color color, float life, bool additive, int order)
        {
            var clip = Spawn("Grace column", sprite, ground, width, color, life, additive, order);
            clip.scale = new Vector3(width / sprite.bounds.size.x, height / sprite.bounds.size.y, 1); clip.root.transform.localScale = clip.scale;
            clip.axisStart = new Vector2(.35f, .12f); clip.axisEnd = Vector2.one; clip.ease = 4; clip.fadePower = 1.3f;
            return clip;
        }

        private Clip GraceDomeLayer(Vector2 ground, float width, float height, Color color, float life, bool additive, int order, Sprite sprite = null)
        {
            var clip = Spawn("Grace dome", sprite ?? GraceDome, ground, width, color, life, additive, order);
            clip.scale.y *= height / width; clip.root.transform.localScale = clip.scale;
            return clip;
        }

        /// <summary>A pool of soft light lying on the floor under a seal, behind the actors.</summary>
        private Clip GraceGroundBloom(Vector2 ground, float width, Color color, float life)
        {
            var clip = Spawn("Grace floor bloom", glow, ground, width, color, life, true, 25); clip.scale.y *= .32f; clip.fadeIn = .05f; clip.fadePower = 1.4f;
            return clip;
        }

        /// <summary>A free plasma orb (the shared looped flipbook: swirling filaments, a lit rim, flares): a soul mote.</summary>
        private Clip GracePlasma(Vector2 pos, float width, Color color, float life, int order)
        {
            var clip = Spawn("Grace plasma mote", plasmaFrames[0], pos, width, color, life, true, order);
            clip.frames = plasmaFrames; clip.fps = 16; clip.loop = true; clip.angle = Range(0, 360);
            return clip;
        }

        /// <summary>A timed plasma orb riding on a caster's hand (or a free one when there is no anchor).</summary>
        private Clip GraceAnchoredPlasma(Transform anchor, Vector2 at, string name, float width, Color color, float life, int order)
        {
            var clip = Anchored(anchor, at, name, plasmaFrames[0], width, color, life, order);
            clip.frames = plasmaFrames; clip.fps = 16; clip.loop = true;
            return clip;
        }

        /// <summary>A painted river stretched from <paramref name="from"/> to <paramref name="to"/>: the frames wind and flow on
        /// their own, so one clip draws the whole stream with no joints. It grows from <paramref name="from"/> (its pivot).</summary>
        private Clip GraceRiver(Vector2 from, Vector2 to, Sprite[] frames, float thickness, Color color, float life, bool additive, int order)
        {
            Vector2 delta = to - from; float length = Mathf.Max(.05f, delta.magnitude);
            var clip = Spawn("Grace river", frames[0], from, length, color, life, additive, order);
            clip.frames = frames; clip.fps = 16; clip.loop = true;
            Vector3 size = frames[0].bounds.size;
            clip.scale = new Vector3(length / size.x, thickness / size.y, 1); clip.root.transform.localScale = clip.scale;
            clip.angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg; clip.root.transform.localRotation = Quaternion.Euler(0, 0, clip.angle);
            clip.ease = 1;
            return clip;
        }

        /// <summary>An expanding shock front (the shared flipbook: a thick ring racing out, thinning and breaking into arcs), sized
        /// so its rim ends where a painted ring of <paramref name="width"/> would (0.78 of the half-width), after <paramref name="delay"/>.</summary>
        private Clip GraceWave(Vector2 pos, float width, Color color, float life, float ease, int order = 131, float delay = 0)
        {
            var wave = ShockFlip(pos, width * .89f, color, life, order); wave.startScale = .3f; wave.endScale = 1; wave.ease = Mathf.Max(ease, 3); wave.fadePower = .9f;
            if (delay > 0) GraceLater(wave, delay);
            return wave;
        }

        /// <summary>A shock front running out along the floor (laid flat, so its arcs turn in the ground plane).</summary>
        private Clip GraceGroundWave(Vector2 pos, float width, Color color, float life, float delay = 0)
        {
            var wave = GraceWave(pos, width, color, life, 3.5f, 116, delay); LayFlat(wave, .34f);
            return wave;
        }

        /// <summary>Holds a clip back for <paramref name="delay"/> seconds (on top of any delay it already has).</summary>
        private static Clip GraceLater(Clip clip, float delay)
        {
            if (delay > 0) { clip.age -= delay; if (clip.renderer) clip.renderer.enabled = false; }
            return clip;
        }

        /// <summary>Holds back every clip spawned since <paramref name="mark"/> (a count of clips taken before), so a
        /// multi-layer builder such as Blast can start later as one piece.</summary>
        private void GraceDefer(int mark, float delay)
        {
            if (delay <= 0) return;
            for (int i = mark; i < clips.Count; i++) GraceLater(clips[i], delay);
        }

        private void GraceFlat(Clip clip, float squash) { LayFlat(clip, squash); }

        /// <summary>An emitter point mapped from the emitter's circle onto the squashed floor ellipse.</summary>
        private static Vector2 GraceOnGround(Clip owner, Vector2 p)
        {
            Vector2 o = owner.root.transform.position; return o + new Vector2(p.x - o.x, (p.y - o.y) * .3f);
        }

        /// <summary>The tint when it is a real colour, otherwise the family fallback.</summary>
        private static Color GraceVivid(Color tint, Color fallback)
        {
            float h, s, v; Color.RGBToHSV(tint, out h, out s, out v);
            if (s < .2f || v < .25f) return fallback;
            return Color.HSVToRGB(h, Mathf.Max(s, .45f), Mathf.Max(v, .9f));
        }

        /// <summary>A deep, saturated version of a colour.</summary>
        private static Color GraceDeep(Color color, float value, float alpha, float hueShift = 0)
        {
            float h, s, v; Color.RGBToHSV(color, out h, out s, out v);
            var deep = Color.HSVToRGB(Mathf.Repeat(h + hueShift, 1), Mathf.Clamp01(s * 1.25f + .2f), value); deep.a = alpha; return deep;
        }

        // ---------------------------------------------------------------- textures

        /// <summary>A painted line's light level: 1 on the line falling straight to 0 at <paramref name="width"/>, so the painted
        /// ramp gives it a white core, a pale band and a coloured edge.</summary>
        private static float GraceLine(float distance, float width) { return Mathf.Clamp01(1 - Mathf.Abs(distance) / width); }

        /// <summary>The soft light on a painted blade (a leaf or a petal), lit from the upper left: each half bulges a little
        /// away from the midrib and the blade curls away toward its ends. <paramref name="across"/> runs from -1 (lower edge)
        /// through the midrib (0) to 1 (upper edge); <paramref name="along"/> from -1 to 1 end to end.</summary>
        private static float GraceBladeLight(float across, float along)
        {
            float s = Mathf.Abs(across) * 2 - 1, side = across >= 0 ? 1 : -1;
            float ny = side * s * .55f, nx = along * .4f, nz = Mathf.Sqrt(Mathf.Max(0, 1 - nx * nx - ny * ny));
            return Mathf.Clamp01(-.45f * nx + .55f * ny + .7f * nz);
        }

        private static Sprite[] GraceSheet(string name, int frames, int width, int height, Vector2 pivot, FrameShade shade)
        {
            var sprites = new Sprite[frames];
            for (int f = 0; f < frames; f++) { int frame = f; sprites[f] = Build(name + " " + f, width, height, (u, v) => shade(u, v, frame), pivot); }
            return sprites;
        }

        // Light shapes here are level profiles for the painted light material (a crisp outer edge, a smooth fall-off, a white-hot
        // centre, brush streaks along the stroke). Painted objects (the leaf) carry soft shading in rgb, lit from the upper left.
        // The beam, ribbon and dome halos are soft blooms (added to the soft set) that glow under the strokes.
        private static void BuildGraceTextures()
        {
            // A glowing barrier dome standing on the floor (bottom pivot): a rim band whose light rises smoothly to a white outer
            // edge (brightest on the upper left), a faint body that thickens toward the rim, a faint hexagonal lattice that fades
            // out toward the middle (the hero inside stays readable), a soft sheen and a band hugging the floor.
            GraceDome = Build("Grace dome", 256, 256, (u, v) =>
            {
                float h = (v + 1) * .5f, r = Mathf.Sqrt(u * u + h * h);
                if (r > 1) return Color.clear;
                float th = Mathf.Atan2(h, u), streaks = .8f + .4f * (RingNoise(th, r, 2.2f, 16) - .5f);
                float lit = .75f + .25f * Mathf.Clamp01(Mathf.Cos(th - 2.2f));
                float rim = Mathf.Pow(Smooth(.78f, .975f, r), 1.6f) * (1 - Smooth(.972f, .99f, r)) * streaks * lit;
                float shell = Mathf.Pow(Smooth(.62f, .97f, r), 2.5f) * .24f * (.85f + .3f * (Fbm(u * 3 + 2, h * 3 + 5, 2) - .5f));
                float lattice = GraceLine(HexEdge(u, h, .13f), .02f) * (.08f + .26f * r * r * r) * (1 - Smooth(.9f, .95f, r));
                float sheen = Gauss(r - .86f, .05f) * Gauss(th - 2.1f, .35f) * .35f;
                float floor = GraceLine(h, .04f) * (1 - Smooth(.8f, .97f, Mathf.Abs(u))) * .7f;
                return White(Mathf.Max(Mathf.Max(rim, shell + lattice + sheen), floor));
            }, new Vector2(.5f, 0));

            // The dome's rim band alone (bottom pivot), for the snap and the extra shells of higher levels: a second lattice at
            // another size would only tangle with the first.
            GraceDomeRim = Build("Grace dome rim", 256, 256, (u, v) =>
            {
                float h = (v + 1) * .5f, r = Mathf.Sqrt(u * u + h * h);
                if (r > 1) return Color.clear;
                float streaks = .8f + .4f * (RingNoise(Mathf.Atan2(h, u), r, 2.6f, 18) - .5f);
                return White(Mathf.Clamp01((r - .84f) / .14f) * (1 - Smooth(.972f, .988f, r)) * streaks * Smooth(0, .02f, h));
            }, new Vector2(.5f, 0));

            // The dome's soft halo (bottom pivot, a bloom): light hugging the rim, spilling a little inside and out.
            GraceDomeGlow = Build("Grace dome glow", 192, 192, (u, v) =>
            {
                float h = (v + 1) * .5f, r = Mathf.Sqrt(u * u + h * h);
                float halo = Gauss(r - .88f, .1f) * .8f + Gauss(r - .8f, .3f) * .25f;
                return White(Mathf.Clamp01(halo) * Smooth(0, .06f, h) * (1 - Smooth(.92f, 1, Mathf.Max(Mathf.Abs(u), h))));
            }, new Vector2(.5f, 0));

            // A broad soft beam of light standing on the floor (bottom pivot, a bloom) for columns and pillars.
            GraceBeam = Build("Grace beam glow", 64, 256, (u, v) =>
            {
                float h = (v + 1) * .5f;
                return White(Gauss(u, .42f) * Smooth(0, .05f, h) * (1 - Smooth(.25f, 1, h)) * (1 - Smooth(.9f, 1, Mathf.Abs(u))));
            }, new Vector2(.5f, 0));

            // A soft ribbon of glow along +x (pivot at its start, a bloom) under rivers and tendrils.
            GraceRibbon = Build("Grace ribbon glow", 256, 64, (u, v) =>
            {
                float x = (u + 1) * .5f;
                return White(Gauss(v, .42f) * Smooth(0, .1f, x) * (1 - Smooth(.88f, 1, x)) * (1 - Smooth(.9f, 1, Mathf.Abs(v))));
            }, new Vector2(0, .5f));
            GraceRibbonFrames = new[] { GraceRibbon };
            soft.Add(GraceDomeGlow); soft.Add(GraceBeam); soft.Add(GraceRibbon);

            // A flat hexagonal ward disc: lattice lines brightening toward a painted rim band with a white outer edge.
            GraceHex = Build("Grace hex ward", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .99f) return Color.clear;
                float rim = Mathf.Clamp01((r - .76f) / .18f) * (1 - Smooth(.94f, .958f, r)) * (.8f + .4f * (RingNoise(Mathf.Atan2(v, u), r, 2, 16) - .5f));
                float lattice = GraceLine(HexEdge(u, v, .2f), .032f) * (.3f + .7f * r * r) * (1 - Smooth(.88f, .93f, r));
                return White(Mathf.Max(rim, lattice));
            });

            // A soul comet along +x: a round head with a white heart and a tapering, wavering tail streaked along its length.
            GraceSoul = Build("Grace soul", 128, 64, (u, v) =>
            {
                float y = v * .5f, d = Mathf.Sqrt(Sq(u - .5f) + y * y);
                float head = Mathf.Clamp01(1 - d / .2f) * 1.1f;
                float t = Mathf.Clamp01((.5f - u) / 1.4f), half = .19f * Mathf.Pow(1 - t, 1.2f) + .006f, wave = Mathf.Sin(t * 8.5f) * .05f * t;
                float tail = u < .5f ? Mathf.Clamp01(1 - Mathf.Abs(y - wave) / half) * (1 - t * .5f) * (.8f + .45f * (Fbm(u * 2 + 5, (y - wave) * 14 + 2, 2) - .5f)) : 0;
                return White(Mathf.Clamp01(Mathf.Max(head, tail)) * (1 - Smooth(.9f, .99f, Mathf.Abs(u))));
            });

            // A painted crystal sliver lit from within: soft faces (the left one in the light), a white ridge, a refraction
            // streak, a lit rim on the upper left and a glow in its heart; no flat facet tones.
            GraceFacet = Build("Grace facet", 48, 96, (u, v) =>
            {
                float d = Mathf.Abs(u) / .8f + Mathf.Abs(v) / .95f, inside = 1 - Smooth(.9f, .96f, d);
                if (inside <= 0) return Color.clear;
                float faces = .38f + .3f * Smooth(.5f, -.6f, u) + .12f * v;
                float ridge = Gauss(u + .06f * v, .1f) * .45f * (1 - d * .5f);
                float refraction = Gauss(u * .9f + v * .45f - .12f, .07f) * .3f;
                float rim = Gauss(d - .86f, .06f) * (u < 0 ? .4f : .15f);
                float heart = Gauss(Mathf.Sqrt(u * u + Sq(v + .1f)), .35f) * .2f;
                return White(inside * Mathf.Clamp01(faces + ridge + refraction + rim + heart));
            });

            // A painted light pillar (bottom pivot): a white spine, a smooth fall-off to ragged streaky sides, vertical brush streaks,
            // and a top that breaks into thinning fingers instead of fading.
            GracePillar = Build("Grace pillar", 96, 384, (u, v) =>
            {
                float h = (v + 1) * .5f;
                float half = .8f + .1f * (Fbm(h * 2.5f + 4, 3, 2) - .5f) * 2;
                float across = Mathf.Clamp01(1 - Mathf.Abs(u) / half);
                float streaks = .78f + .44f * (Fbm(u * 10 + 3, h * 1.3f + 1, 2) - .5f);
                float top = .55f + .4f * Fbm(u * 7 + 9, 2, 2);
                float rise = Smooth(0, .015f, h) * Mathf.Clamp01((top - h) / .22f);
                return White(Mathf.Clamp01(Mathf.Pow(across, .9f) * streaks * rise * 1.12f));
            }, new Vector2(.5f, 0));

            // A painted shock ring: a hard white outer edge with colour streaking back a short, uneven way inward; the inside
            // stays empty, so a ring never fills in as a flat disc while it is small.
            // Its outer edge sits where the shared ring's does (.78 of the half-width), so it is sized the same way.
            GraceShock = Build("Grace shock ring", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .82f) return Color.clear;
                float th = Mathf.Atan2(v, u), depth = Mathf.Max(0, .78f - r);
                float streaks = RingNoise(th, r, 1.6f, 14), reach = .06f + .14f * RingNoise(th, 0, 3.2f, 0) * (.6f + .8f * streaks);
                float outside = 1 - Smooth(-.004f, .006f, r - .78f);
                return White(outside * Mathf.Clamp01(1 - depth / reach) * (.8f + .45f * (streaks - .5f)));
            });

            // A ward plate of light: a hexagonal tile with a soft pillow of light, a bevel that brightens to white on the upper
            // left, a faint inner hexagon, a sheen and a little brush grain; no flat faces.
            GracePlate = Build("Grace plate", 96, 96, (u, v) =>
            {
                float x = Mathf.Abs(u), y = Mathf.Abs(v), d = Mathf.Max(x * .866f + y * .5f, y);
                float inside = 1 - Smooth(.8f, .83f, d);
                if (inside <= 0) return Color.clear;
                float lit = Mathf.Clamp01(.55f - u * .35f + v * .4f);
                float pillow = Mathf.Sqrt(Mathf.Max(0, 1 - Sq(d / .83f)));
                float bevel = Smooth(.6f, .8f, d) * (.4f + .6f * lit);
                float body = (.24f + .2f * pillow * lit) * (.88f + .24f * (Fbm(u * 4 + 3, v * 4 + 1, 2) - .5f));
                float inner = GraceLine(d - .4f, .03f) * .2f;
                float shine = Gauss(Mathf.Sqrt(Sq(u + .28f) + Sq(v - .3f)), .14f) * .4f;
                return White(inside * Mathf.Clamp01(Mathf.Max(bevel * .85f, body + inner + shine)));
            });

            // A holy cross mote: a plus with tapering arms and a white heart.
            GraceCross = Build("Grace cross", 64, 64, (u, v) =>
            {
                float au = Mathf.Abs(u), av = Mathf.Abs(v);
                float armH = Mathf.Clamp01(1 - av / (.28f * (1 - .45f * au))) * Mathf.Clamp01((.95f - au) / .3f);
                float armV = Mathf.Clamp01(1 - au / (.28f * (1 - .45f * av))) * Mathf.Clamp01((.95f - av) / .3f);
                return White(Mathf.Clamp01(Mathf.Max(armH, armV) * 1.15f));
            });

            // A painted leaf along +x (pointed at both ends): a blade cupped a little on each side of a pale midrib, lit softly
            // from the upper left, with faint side veins, a lit upper rim and brush grain. Shading only (it takes any tint); alpha
            // rises toward the midrib, so a leaf erodes from its rim as it fades.
            GraceLeaf = Build("Grace leaf", 96, 48, (u, v) =>
            {
                float half = .9f * Mathf.Pow(Mathf.Max(0, 1 - u * u), .75f) * (1 - .2f * u);
                float inside = 1 - Smooth(half - .07f, half + .01f, Mathf.Abs(v));
                if (inside <= 0) return Color.clear;
                float rib = .1f * Mathf.Sin(u * 2.4f), across = Mathf.Clamp((v - rib) / Mathf.Max(.05f, half), -1, 1);
                float light = .42f + .55f * GraceBladeLight(across, u);
                float veins = Mathf.Pow(Mathf.Abs(Mathf.Sin((u - Mathf.Abs(across) * .35f) * 9)), 12) * .07f * (1 - Mathf.Abs(across));
                light += GraceLine(v - rib, .045f) * .22f * (1 - Mathf.Abs(u)) + veins;
                light += Gauss(Mathf.Abs(across) - .93f, .06f) * (across > 0 ? .18f : .05f);
                light *= .9f + .2f * (Fbm(u * 5 + 3, v * 9 + 1, 2) - .5f);
                float l = Mathf.Clamp01(light);
                return new Color(l, l, l, inside * (.62f + .38f * (1 - Mathf.Abs(across))));
            });

            // One painted brush ring: the stroke starts with a thick press, runs round and tapers past its start; a hard bright
            // outer edge, colour falling off inward, streaks along the stroke.
            GraceBrushRing = Build("Grace brush ring", 384, 384, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .99f) return Color.clear;
                float th = Mathf.Atan2(v, u), t = Mathf.Repeat(th / (2 * Mathf.PI), 1), best = 0;
                for (int lap = 0; lap < 2; lap++)
                {
                    float s = t + lap; if (s > 1.14f) continue;
                    float thick = .12f * (1 - .8f * Mathf.Pow(s / 1.14f, 1.4f)) + .01f, outer = .95f - .025f * s;
                    float depth = (outer - r) / thick; if (depth > 1) continue;
                    float edge = 1 - Smooth(-.004f, .006f, r - outer);
                    float ends = Smooth(0, .015f, s) * (1 - Smooth(1.06f, 1.14f, s));
                    float streaks = .78f + .46f * (RingNoise(th, r, 1.8f, 24) - .5f);
                    best = Mathf.Max(best, Mathf.Clamp01(1 - depth) * edge * ends * streaks);
                }
                return White(best);
            });

            // A painted soul river along +x (from the victim at -u to the caster at +u, pivot at the victim): two strands wind
            // round each other with a white heart and streaks running along them, swelling where they come toward the viewer; a
            // pale spine runs down the middle. The frames shift the winding so the river flows.
            GraceStream = GraceSheet("Grace river", 8, 512, 96, new Vector2(0, .5f), (u, v, f) =>
            {
                float x = (u + 1) * .5f, phase = f / 8f * 2 * Mathf.PI;
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(x * .92f + .04f)), .6f), level = 0;
                for (int s = 0; s < 2; s++)
                {
                    float psi = x * 2 * Mathf.PI * 2.5f - phase + s * Mathf.PI;
                    float y = Mathf.Sin(psi) * .55f * env;
                    float half = (.21f + .1f * Mathf.Cos(psi)) * (.3f + .7f * env) + .015f;
                    float across = Mathf.Clamp01(1 - Mathf.Abs(v - y) / half);
                    float streak = .76f + .5f * (Fbm(Mathf.Cos(psi) * 1.3f + 5 + s * 3, Mathf.Sin(psi) * 1.3f + (v - y) * 11 + 2, 2) - .5f);
                    level = Mathf.Max(level, across * streak * (s == 0 ? 1.1f : .84f));
                }
                float spine = Mathf.Clamp01(1 - Mathf.Abs(v) / .05f) * .62f * env;
                return White(Mathf.Clamp01(Mathf.Max(level, spine)) * Smooth(0, .04f, x) * (1 - Smooth(.95f, 1, x)));
            });
            // The same river a quarter turn on, so a second stream winds between the first one's strands.
            GraceStreamShift = new Sprite[GraceStream.Length];
            for (int i = 0; i < GraceStream.Length; i++) GraceStreamShift[i] = GraceStream[(i + 2) % GraceStream.Length];

            // A crisp lotus seal: a painted outer band with a white edge, thin rings, two rows of petal outlines with a midrib,
            // pearls between the petal tips and a dot at the heart. Lines only, no fill.
            GraceMandala = Build("Grace lotus seal", 384, 384, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .99f) return Color.clear;
                float th = Mathf.Atan2(v, u);
                float a = Mathf.Clamp01((r - .87f) / .09f) * (1 - Smooth(.955f, .97f, r)) * (.78f + .44f * (RingNoise(th, r, 2, 20) - .5f));
                a = Mathf.Max(a, GraceLine(r - .835f, .011f) * .8f);
                a = Mathf.Max(a, GraceLine(r - .27f, .015f) * .9f);
                for (int layer = 0; layer < 2; layer++)
                {
                    float step = 2 * Mathf.PI / 8, offset = layer == 0 ? 0 : step * .5f, tip = layer == 0 ? .8f : .6f, root = .27f;
                    float local = Mathf.Repeat(th - offset + step * .5f, step) - step * .5f, t = (r - root) / (tip - root);
                    if (t < 0 || t > 1.05f) continue;
                    float half = step * (layer == 0 ? .5f : .42f) * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)), .75f);
                    a = Mathf.Max(a, GraceLine((Mathf.Abs(local) - half) * r, .014f) * (layer == 0 ? .95f : .72f));
                    if (Mathf.Abs(local) < half) a = Mathf.Max(a, GraceLine(local * r, .008f) * .5f * (1 - Mathf.Clamp01(t)));
                }
                float pearl = Mathf.Repeat(th, Mathf.PI / 4) - Mathf.PI / 8;
                a = Mathf.Max(a, GraceLine(Mathf.Sqrt(Sq(pearl * .82f) + Sq(r - .835f)), .03f) * .95f);
                a = Mathf.Max(a, GraceLine(r, .07f));
                return White(a);
            });

            // Four runic glyphs (Algiz, Tiwaz, Ingwaz, Sowilo) inside a thin ring, as white-cored strokes.
            GraceGlyphs = Sheet("Grace glyph", 4, 64, 64, (u, v, f) =>
            {
                float d = GraceGlyphDistance(u, v, f), r = Mathf.Sqrt(u * u + v * v);
                return White(Mathf.Max(GraceLine(d, .11f), GraceLine(r - .8f, .05f) * .65f) * (1 - Smooth(.9f, .98f, r)));
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
        /// second). A lotus seal lies on the floor in a pool of soft light with its outer band on the field's edge, a painted
        /// brush ring and a rune ring turn inside it, a faint column of light breathes over it in a soft beam, a shock front of
        /// life runs out to the edge with every heal, and crosses and leaves drift up off it.</summary>
        private void ZoneHealPulse(Transform root, float radius, float life)
        {
            float R = Mathf.Clamp(radius, .8f, 4.5f), k = Mathf.Clamp(R / 2.2f, .5f, 1.6f);
            Color green = LifeLight, gold = Color.Lerp(LifeLight, GoldLight, .45f), hot = Color.Lerp(LifeLight, Warm, .6f);
            Color leafTone = GraceLeafTone(LifeLight);
            // The zone root rides 0.6 above the field's centre on the floor.
            var floor = GracePlane(root, new Vector2(0, -.58f), .3f);
            float seal = R * 2 / .96f;
            var pool = Attach(floor, "Grace field bloom", glow, seal * 1.25f, Alpha(green, .42f), 25);
            var lotus = Attach(floor, "Grace field seal", GraceMandala, seal, Alpha(green, .85f), 27); lotus.spin = 12;
            var runes = Attach(floor, "Grace field runes", runeRing, R * 1.15f, Alpha(gold, .8f), 27); runes.spin = -28;
            var band = Attach(floor, "Grace field ring", GraceBrushRing, R * 1.55f, Alpha(hot, .8f), 26); band.spin = 40;
            // A faint column of light breathing over the field inside a soft beam, behind the actors.
            var beam = Attach(root, "Grace field beam", GraceBeam, R * 1.6f, Alpha(green, .3f), 25, true, new Vector2(0, -.6f));
            beam.scale.y = 3.8f * k / GraceBeam.bounds.size.y; beam.pulse = .05f;
            var column = Attach(root, "Grace field column", GracePillar, R * .75f, Alpha(green, .32f), 26, true, new Vector2(0, -.6f));
            column.scale.y = 3.4f * k / GracePillar.bounds.size.y; column.pulse = .04f;
            var layers = new[] { pool, lotus, runes, band, beam, column };
            float hold = Mathf.Clamp01(1 - .7f / life), pop = Mathf.Log(.05f) / Mathf.Log(1 - Mathf.Clamp(.3f / life, .02f, .5f));
            foreach (var clip in layers) { clip.life = life; clip.loop = false; clip.fadeIn = .12f; clip.hold = hold; clip.fadePower = 1.3f; clip.startScale = .4f; clip.endScale = 1; clip.ease = pop; }
            foreach (var clip in new[] { beam, column }) { clip.startScale = 1; clip.axisStart = new Vector2(.4f, .15f); clip.axisEnd = Vector2.one; }
            // With every heal (twice a second) a shock front of life runs out to the field's edge, and now and then a cross pops.
            int beat = 0; float ringWidth = R * 2 / .74f;
            Emitter(runes, 2, 0, (owner, p) =>
            {
                if (owner.age > owner.life - .5f) return;
                Vector2 at = owner.root.transform.position;
                var wave = GraceGroundWave(at + Vector2.up * .02f, ringWidth, Alpha(hot, .85f), .6f); wave.renderer.sortingOrder = 26; wave.startScale = .2f; wave.ease = 2.4f;
                if (beat++ % 2 == 0) GraceCrossPop(at + new Vector2(Range(-.7f, .7f) * R, Range(.3f, 1.6f)), Range(.55f, .8f), Alpha(hot, 1), .34f);
            });
            // Crosses, needles and leaves drift up off the whole field.
            float spread = R * .9f;
            Emitter(lotus, 8 + 4 * k, 0, (owner, p) =>
            {
                if (owner.age > owner.life - .4f) return;
                Vector2 at = (Vector2)owner.root.transform.position + new Vector2(Range(-spread, spread), Range(-.15f, .15f));
                GraceHealMote(at, Chance(.35f) ? hot : green, leafTone, .9f);
            });
            Bundle(root, lotus, pool, runes, band, beam, column);
            // The field opens: a shock front out to its edge and a spark laid on the floor.
            Vector2 centre = (Vector2)root.position + new Vector2(0, -.58f);
            var open = GraceGroundWave(centre, ringWidth, Alpha(gold, .9f), .45f); open.startScale = .15f;
            var flash = SparkFlip(centre + Vector2.up * .05f, R * 1.8f, Alpha(hot, 1), .26f, 27);
            GraceFlat(flash, .35f);
        }
    }
}
