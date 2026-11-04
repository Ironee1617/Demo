using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Ground and contraptions: the travelling quake, the guarding orbs, the fighting phantom and the sealed mine, painted as
    // light with soft blooms (the core's painted ramp) and as softly lit stone, never outlined.
    // Quake: the caster stamps and a glowing rift runs along the floor stride by stride, painted rock fangs (soft upper-left
    // light, grain, strata, cracks glowing with molten earth light) bursting up through it, shock fronts racing along the
    // floor and billows of warm dust thrown off; at level three its end erupts in a basalt pillar ringed with fangs.
    // Orbit: plasma orbs (swirling filaments, a lit rim, flares, a white-hot heart, a glow) ride a spinner in the tilted orbit
    // plane, so every orb sits exactly where gameplay strikes (OrbitOffset); each drags a painted light ribbon over a soft glow
    // lying on the orbit itself, and orbs (and trails) on the far half of the ellipse draw behind the actors. Awakened orbs
    // wear a painted gold crown with four flares.
    // Decoy: a frozen copy of the caster's own art, flooded to a luminous pale ghost over a translucent cyan body, glowing
    // softly with a lagging afterimage; it slides out of the caster, bobs, lunges with a spectral crescent on each attack and
    // bursts in a pale cyan blast, spark and shock front when it expires.
    // Mine: glowing painted glyphs are tossed from the hand, slam onto the floor, lock in when armed and blink under an upright
    // mark until the blast: a pink-magenta explosion, a fan of painted fire petals, seal shards, warm grey smoke and shock
    // fronts on the true radius.
    // Contrast on the pale sky comes from soft underlays and deep saturated hues of each skill's own colour, never dark ink.
    public sealed partial class TowerVfx
    {
        private static readonly Color EarthAmber = new Color(1, .62f, .24f), EarthMagma = new Color(1, .36f, .06f), EarthHot = new Color(1, .95f, .78f),
            EarthDustTone = new Color(.7f, .58f, .45f, .55f), EarthDustDeep = new Color(.5f, .38f, .28f, .62f), EarthSoil = new Color(.54f, .41f, .3f, .55f),
            EarthGold = new Color(1, .86f, .46f), EarthGoldHot = new Color(1, .97f, .82f),
            EarthAzure = new Color(.42f, .76f, 1), EarthAzureHot = new Color(.85f, .97f, 1), EarthOrbPlasma = new Color(.32f, .66f, 1), EarthAzureDeep = new Color(.16f, .42f, 1),
            EarthPale = new Color(.68f, .95f, 1), EarthMist = new Color(.82f, .95f, 1, .34f), EarthCyanDeep = new Color(.12f, .66f, 1),
            EarthPink = new Color(1, .38f, .76f), EarthPinkHot = new Color(1, .88f, .96f), EarthMagenta = new Color(.95f, .16f, .6f),
            EarthSmoke = new Color(.58f, .49f, .47f, .7f);
        // Painted stone: deep shadow, mid and lit tones (warm brown-grey), a crack's throat and the molten glow inside it.
        private static readonly Color EarthStoneDeep = new Color(.12f, .07f, .05f), EarthStoneMid = new Color(.4f, .28f, .2f), EarthStoneLit = new Color(.8f, .64f, .47f),
            EarthCrackThroat = new Color(.3f, .08f, .02f), EarthMoltenTone = new Color(1, .52f, .14f);

        // Textures are baked a few rows a frame once the effects exist (see EarthBakeStep), or at once on a skill's first use.
        private sealed class EarthBakeJob { public string name; public int width, height, row; public Vector2 pivot; public bool pair; public EarthPairShade shade; public Color32[] first, second; public Action<Sprite, Sprite> store; }
        private sealed class EarthBakeLoop { }
        private static readonly List<EarthBakeJob> EarthBakes = new List<EarthBakeJob>();
        private static bool EarthQueued, EarthBaked;
        private static Sprite[] EarthFangRock, EarthFangVein, EarthRiftInk, EarthRiftLight, EarthShardSet, EarthChipSet;
        private static Sprite EarthColumnRock, EarthColumnVein, EarthTrailLight, EarthTrailGlow, EarthOrbCrown, EarthWraith, EarthSeal, EarthSlab, EarthPetal, EarthGlyph;
        private static readonly Dictionary<Material, Material> EarthInkMaterials = new Dictionary<Material, Material>();
        // The phantom's light copies: additive at a high gain, so the copied art floods to a pale silhouette and only its
        // darkest lines stay faintly, like a painted afterimage.
        private static readonly Dictionary<Material, Material> EarthFlatMaterials = new Dictionary<Material, Material>();
        private static Material EarthFlatSprite;
        private static readonly Color EarthCyan = new Color(.2f, .7f, 1);
        private const float EarthFlatGain = 2.6f;

        // One orb: its layers, their sorting orders in front of and behind the actors, where on the orbit it started, the
        // orbit radius and its halo's full strength (dimmed while the orb crosses in front of the body); and its painted trail
        // (a light ribbon over a soft glow lying on the orbit behind it, which turns with the orbit instead of counter-turning).
        private sealed class EarthOrbRig { public Clip[] layers; public int[] front, back; public float phase, radius, haloAlpha, trailAlpha, glowAlpha; public bool behind; public Clip trail, trailGlow; }
        // A per-frame job (orb depth, phantom pose, mine arming). Emitters stop while the effects are over their hard cap, so
        // the player loop runs these itself then.
        private sealed class EarthDriver { public TowerVfx host; public Clip clip; public Action<Clip> run; }
        private static readonly List<EarthDriver> EarthDrivers = new List<EarthDriver>();
        // The last stride of a running rift (a clip as its clock and where it landed): a front pinned against the world's
        // edge lands on the same spot again and only crumbles there.
        private Clip EarthStrideClock; private Vector2 EarthStrideAt;
        // The last orb cast (who cast it, and a clip that lives through its release): a companion's orbs fly to the hero they guard.
        private Transform EarthOrbitFrom; private Clip EarthOrbitClock;
        // A phantom standing at its zone root: its body copies with their rest offsets and how far behind in time each follows
        // (the afterimage lags), which way it faces and when it lunged.
        private sealed class EarthPhantom
        {
            public Transform root; public readonly List<Clip> body = new List<Clip>(); public readonly List<Vector2> rest = new List<Vector2>(); public readonly List<float> lag = new List<float>();
            public Clip light, primary; public float clock, lungeAt = -9, slideFrom; public int facing = 1, copyFacing = 1;
        }
        private readonly List<EarthPhantom> EarthPhantoms = new List<EarthPhantom>();
        // The hand the last mine cast threw from (on its caster, so it follows the caster through the wind-up), so the mines
        // it lays can fly from there.
        private Vector2 EarthMineHand; private Transform EarthMineAnchor; private Vector3 EarthMineLocal; private int EarthMineFrame = -9999;
        // Strides drawn so far (alternate strides carry the optional accents), and mines already bursting this frame.
        private int EarthQuakeBeat, EarthBlastFrame = -1, EarthBlastCount;

        // ---------------------------------------------------------------- Quake

        private void CastQuake(CastInfo c)
        {
            EarthTextures();
            int g = Grade, side = c.facing; float k = EarthGradeScale(g), w = c.windup, release = Mathf.Max(0, w - .02f);
            Color amber = EarthBlend(c.tint, EarthAmber, .35f), hot = Color.Lerp(amber, g >= 3 ? EarthGoldHot : EarthHot, .55f);
            Vector2 ground = c.pos + Vector2.down, foot = ground + new Vector2(side * .3f, 0);
            if (w < .2f)
            {
                // An instant echo: a stamp flash, a short shock front and dust; the rift itself follows at once.
                var stamp = Flash(foot + Vector2.up * .2f, 1.4f, Alpha(hot, .8f), .14f); stamp.ramp = Ramp(Color.white, amber);
                EarthShock(foot + Vector2.up * .04f, 1.2f, Alpha(amber, .85f), .28f, 116, .32f);
                Dust(foot, 0, .7f);
                return;
            }
            float total = w + .3f, hold = w / total;

            // The ground answers: a soft dark halo for the light, a glow pooling on the floor, an earth seal turning under the
            // feet and molten cracks spreading.
            var shade = Underlay(ground + Vector2.up * .7f, 3.6f * k, .28f, total); shade.hold = hold;
            var pool = GroundGlow(ground, 3.4f * k, Alpha(amber, .5f), total); pool.hold = hold; pool.fadeIn = .1f; pool.startScale = .5f; pool.ease = 2;
            var seal = Spawn("Earth quake seal", runeRing, ground, 3 * k, Alpha(hot, .85f), total, true, 27); LayFlat(seal, .3f);
            seal.spin = 70 * side; seal.fadeIn = .08f; seal.startScale = .4f; seal.endScale = 1; seal.ease = 3; seal.hold = hold; seal.fadePower = 1.5f;
            // The floor scar tears open in the last moments before the stamp, marking where the rift will start.
            float open = Mathf.Max(0, release - .15f), riftAngle = Range(-8, 8);
            var scar = Decal(ground, crack, 2.8f * k, new Color(.2f, .09f, .04f, .8f), total + .8f - open, false, 25);
            var lava = Decal(ground, crack, 2.8f * k, Alpha(hot, .95f), total + .3f - open, true, 26);
            foreach (var x in new[] { scar, lava }) { x.angle = riftAngle; x.startScale = .2f; x.ease = 5; x.hold = .55f; EarthLater(x, open); }
            lava.ramp = Ramp(EarthHot, amber, EarthMagma);

            // Slabs of the floor tear loose and float slowly up round the caster as the charge builds, rocking, glowing on
            // their undersides and shedding crumbs of soil.
            int stones = Budget(4);
            for (int i = 0; i < stones; i++)
            {
                float delay = Range(0, w * .3f), x = (i % 2 == 0 ? 1 : -1) * Range(.55f, 1.6f) * k;
                int order = i % 3 == 0 ? 119 : 28;
                var slab = Spawn("Earth lifted slab", EarthSlab, ground + new Vector2(x, .02f), Range(.42f, .62f) * k, Color.white, w - delay + .04f, false, order);
                slab.angle = Range(-12, 12); slab.spin = Range(-20, 20);
                slab.velocity = new Vector2(Range(-.08f, .08f), Range(2.8f, 3.4f) * Mathf.Sqrt(k)); slab.drag = 4;
                slab.fadeIn = .05f; slab.startScale = .7f; slab.endScale = 1; slab.ease = 3; slab.hold = .9f; slab.fadePower = 2;
                EarthLater(slab, delay);
                if (i >= 2) continue;
                var crumb = Spawn("Earth slab crumb", EarthChipSet[i % EarthChipSet.Length], ground + new Vector2(x, .4f), Range(.1f, .15f) * k, Color.white, .4f, false, order);
                crumb.velocity = new Vector2(Range(-.3f, .3f), -.2f); crumb.gravity = 9; crumb.spin = Range(-300, 300); crumb.hold = .6f; crumb.fadePower = 1;
                EarthLater(crumb, delay + Range(.12f, .2f));
            }
            for (int i = 0; i < 3; i++)
            {
                var rise = EarthLater(Puff(ground + new Vector2(Range(-.8f, .8f) * k, .15f), Range(.8f, 1.1f) * k, EarthSoil, new Vector2(Range(-.3f, .3f), Range(.8f, 1.3f)), w * .9f, 115), Range(0, w * .3f));
                rise.drag = 1.5f; rise.startScale = .6f; rise.endScale = 1.8f; rise.fadeIn = .12f; rise.hold = .4f;
            }
            // Dust is drawn in along the floor and amber light gathers into the stamping foot.
            for (int i = 0; i < 4; i++)
            {
                float s = i % 2 == 0 ? 1 : -1;
                var drift = EarthLater(Puff(ground + new Vector2(s * Range(1.3f, 1.8f) * k, .2f), Range(.7f, 1) * k, EarthDustTone, new Vector2(-s * Range(2.2f, 3), .2f), w * .8f, 116), i * w * .1f);
                drift.drag = 1.6f; drift.startScale = .8f; drift.endScale = 1.4f; drift.fadeIn = .1f; drift.hold = .5f;
            }
            Converge(foot + Vector2.up * .3f, 8, 2 * k, amber, w, .3f);

            // Release: the stamp. A burst of molten earth light just ahead of the foot, a hit spark, shock fronts racing out along
            // the floor both ways, a painted front skimming toward the target, chips and needles flying, billows of dust thrown
            // both ways and the first stretch of the glowing rift opening toward the target.
            var stampFlash = EarthLater(Flash(foot + Vector2.up * .3f, 2.2f * k, Alpha(hot, .9f), .16f), release); stampFlash.ramp = Ramp(Color.white, amber);
            EarthLater(Blast(foot + new Vector2(side * .5f, .35f), 1.55f * k, amber, .42f, 120), release);
            EarthShock(foot + Vector2.up * .04f, 2.3f * k, Alpha(amber, .95f), .42f, 116, .32f, release);
            EarthShock(foot + Vector2.up * .04f, 1.5f * k, Alpha(hot, .8f), .32f, 117, .32f, release + .04f);
            EarthArc(foot + new Vector2(side * .35f, .14f), 1.8f * k, side > 0 ? 0 : 180, Alpha(amber, .95f), .2f, 117, .42f, new Vector2(side * 2.5f, 0), release);
            EarthLater(ImpactLines(foot + Vector2.up * .45f, 3.1f * k, Alpha(hot, .75f), .22f), release);
            EarthRiftSegment(foot + new Vector2(side * .3f, 0), side, amber, release);
            int chips = Budget(4);
            for (int i = 0; i < chips; i++)
            {
                var chip = EarthLater(Spawn("Earth stamp chip", EarthChipSet[i % EarthChipSet.Length], foot + new Vector2(Range(-.4f, .4f), .1f), Range(.16f, .28f) * k, Color.white, Range(.5f, .8f), false, 128), release);
                chip.velocity = new Vector2(Range(-3.5f, 3.5f) + side * 1.2f, Range(3, 6)); chip.gravity = 16; chip.spin = Range(-600, 600); chip.hold = .5f; chip.fadePower = .6f;
            }
            EarthNeedles(foot + Vector2.up * .25f, 5, 90 - side * 25, 120, 6.5f * Mathf.Sqrt(k), amber, .3f, .4f, release);
            for (int i = 0; i < 4; i++)
            {
                float s = i % 2 == 0 ? 1 : -1;
                bool deep = i >= 2;
                var dust = EarthLater(Puff(foot + new Vector2(s * .3f, .15f), Range(.8f, 1.1f) * k, deep ? EarthDustDeep : EarthDustTone, new Vector2(s * Range(2.5f, 4), Range(.4f, 1.1f)), Range(.6f, .85f), deep ? 28 : 118), release);
                dust.endScale = 2.2f;
            }
        }

        // One stride of the rift, called every 0.06 s as the front runs 0.6 further from the caster; facing 0 is the
        // level-three eruption at its end.
        private void ImpactQuake(Vector2 pos, float radius, Color tint, int facing)
        {
            EarthTextures();
            if (facing == 0) { EarthEruption(pos, radius, tint); return; }
            int g = Grade, side = facing < 0 ? -1 : 1; float k = EarthGradeScale(g);
            Color amber = EarthBlend(tint, EarthAmber, .35f);
            Vector2 ground = pos;
            if (EarthStrideClock != null && EarthStrideClock.age < .1f && (EarthStrideAt - ground).sqrMagnitude < .01f && clips.Contains(EarthStrideClock))
            {
                // The front is pinned against the world's edge: the rift has met the wall and only crumbles against it.
                var crumble = Puff(ground + new Vector2(Range(-.15f, .15f), .2f), Range(.6f, .8f) * k, EarthDustTone, new Vector2(-side * Range(.3f, .8f), Range(.5f, 1.1f)), Range(.5f, .7f), 118);
                crumble.endScale = 1.8f;
                var bit = Spawn("Earth rift chip", EarthChipSet[random.Next(EarthChipSet.Length)], ground + new Vector2(0, .15f), Range(.12f, .2f) * k, Color.white, .55f, false, 128);
                bit.velocity = new Vector2(-side * Range(.8f, 2), Range(2.5f, 4)); bit.gravity = 16; bit.spin = Range(-600, 600); bit.hold = .5f; bit.fadePower = .6f;
                EarthStrideClock = crumble;
                return;
            }
            var gash = EarthRiftSegment(ground, side, amber, 0);
            EarthStrideClock = gash; EarthStrideAt = ground;

            // Slabs of rock burst up through the rift leaning with the run, two tall ones behind the actors and often a short
            // one in front; they stand a moment and sink back into the floor as solid stone.
            int beat = EarthQuakeBeat++;
            float stand = Range(.36f, .44f);
            var fangs = new List<EarthFang>(3);
            for (int i = 0; i < 2; i++)
            {
                float height = Range(1.3f, 1.75f) * k * (i == 0 ? 1 : .82f);
                Vector2 at = ground + new Vector2(-side * (.12f + i * .3f) + Range(-.06f, .06f), -.03f);
                fangs.Add(EarthFangRise(at, height, side * Range(6, 20) * (i % 2 == 0 ? 1 : -.4f), 27, stand + i * .04f, i * .015f, true));
            }
            if (Chance(.45f)) fangs.Add(EarthFangRise(ground + new Vector2(-side * Range(.25f, .4f), -.1f), Range(.65f, .9f) * k, side * Range(10, 24), 120, stand - .06f, .02f, false));
            // The sinking is scheduled when it comes (the gash keeps the time), so a long run never holds every stride's sinking
            // stones at once; a puff of dust rises at their roots and every other stride a shard breaks off.
            float sinkFrom = stand - .065f; bool sunk = false, shedding = beat % 2 == 1;
            EarthEachFrame(gash, owner =>
            {
                if (sunk || owner.age < sinkFrom) return;
                sunk = true; owner.emit = Emit.None;
                foreach (var fang in fangs) EarthFangSink(fang, fang.sinkAt - owner.age);
                var settle = EarthLater(Puff(ground + new Vector2(-side * .25f, .12f), Range(.6f, .8f) * k, EarthDustTone, new Vector2(Range(-.5f, .5f), Range(.2f, .5f)), Range(.5f, .65f), 118), stand - owner.age);
                settle.endScale = 1.9f;
                if (!shedding) return;
                var shed = EarthLater(Spawn("Earth fang shard", EarthChipSet[random.Next(EarthChipSet.Length)], ground + new Vector2(-side * .2f, Range(.5f, .9f) * k), Range(.12f, .18f) * k, Color.white, .45f, false, 128), stand - owner.age);
                shed.velocity = new Vector2(Range(-1.2f, 1.2f), Range(.8f, 1.8f)); shed.gravity = 15; shed.spin = Range(-500, 500); shed.hold = .5f; shed.fadePower = .7f;
            });

            // A low spout of magma light behind the fangs and a billow of warm soil; a chip flies (and a needle, every other
            // stride).
            var spout = Pillar(ground + Vector2.down * .05f, .7f * k, 1.1f * k, Alpha(Color.Lerp(amber, EarthMagma, .5f), .55f), .22f, 26);
            spout.axisStart = new Vector2(.6f, .15f); spout.hold = .2f; spout.fadePower = 1.4f;
            var plume = Puff(ground + new Vector2(-side * .2f, .25f), Range(.95f, 1.25f) * k, beat % 2 == 0 ? EarthDustDeep : EarthDustTone, new Vector2(side * Range(.3f, .8f), Range(1.6f, 2.4f)), Range(.6f, .8f), 116);
            plume.drag = 2.4f; plume.endScale = 2.1f;
            var chip = Spawn("Earth rift chip", EarthChipSet[random.Next(EarthChipSet.Length)], ground + new Vector2(Range(-.3f, .2f), .1f), Range(.11f, .22f) * k, Color.white, Range(.5f, .8f), false, 128);
            chip.velocity = new Vector2(side * Range(.5f, 2.5f) + Range(-1.2f, 1.2f), Range(3.5f, 6.5f) * Mathf.Sqrt(k)); chip.gravity = 17; chip.spin = Range(-700, 700); chip.hold = .5f; chip.fadePower = .6f;
            if (beat % 2 == 1) Spark(ground + new Vector2(Range(-.3f, .2f), .1f), new Vector2(side * Range(.4f, 2) + Range(-.8f, .8f), Range(3, 6) * Mathf.Sqrt(k)), amber, Range(.24f, .36f) * k, Range(.24f, .36f), 7, 2);
            // Embers keep rising off the fresh seam for a while, so the live rift moves where the lava floor below only glows.
            int embers = g >= 2 ? 1 + beat % 2 : 1;
            for (int i = 0; i < embers; i++)
            {
                var ember = Spark(ground + new Vector2(-side * Range(.05f, .6f), .04f), new Vector2(Range(-.25f, .25f), Range(1.1f, 2)), i % 2 == 0 ? EarthGoldHot : amber, Range(.12f, .18f), Range(.35f, .5f), -1.5f, 1.2f);
                ember.flicker = .35f; EarthLater(ember, Range(.04f, .34f));
            }
            // The front: a hit spark and a shock front racing out along the floor on one stride, a painted front skimming the
            // floor ahead of the run on the next.
            if (beat % 2 == 0)
            {
                Flash(ground + Vector2.up * .2f, 1.1f * k, Alpha(amber, .75f), .12f);
                EarthShock(ground + Vector2.up * .03f, .9f * k, Alpha(amber, .85f), .3f, 116, .32f);
            }
            else
            {
                EarthArc(ground + new Vector2(side * .15f, .14f), 1.25f * k, side > 0 ? 0 : 180, Alpha(amber, .9f), .15f, 117, .42f, new Vector2(side * 4, 0));
            }
            // Awakened, gold-white sparks crown every other stride.
            if (g >= 3 && beat % 2 == 1) { var star = Glint(ground + new Vector2(-side * .3f, Range(.8f, 1.5f)), 1.1f, Alpha(EarthGoldHot, .95f), .22f, Range(0, 45)); star.spin = 0; }
        }

        // The awakened end of the run: a pillar of dark basalt with molten seams erupts and stands, fangs burst round it out to
        // the edge, boulders are flung and dust rolls out along the floor; then the pillar sinks back, shedding rock and dust.
        // (No beam of light: the stone is the event.)
        private void EarthEruption(Vector2 pos, float radius, Color tint)
        {
            float R = Mathf.Clamp(radius, 1, 3.5f), s = Mathf.Clamp(R / 1.7f, .7f, 1.6f);
            Color amber = EarthBlend(tint, EarthAmber, .35f), hot = Color.Lerp(amber, EarthGoldHot, .55f);
            Vector2 ground = pos + Vector2.down * .8f;
            var shade = Underlay(ground + Vector2.up * 1.3f, 4.6f * s, .34f, .8f); shade.hold = .3f;
            // Molten earth light bursts out behind the pillar as it breaks the floor (the stone stays the event in front of it).
            Blast(ground + Vector2.up * 1.1f * s, 3.4f * s, amber, .55f, 26);
            var bloom = Flash(ground + Vector2.up * .8f, 2.6f * s, Alpha(hot, .85f), .22f); bloom.ramp = Ramp(Color.white, amber);
            ImpactLines(ground + Vector2.up * .8f, 5.2f * s, Alpha(hot, .7f), .28f);
            // Painted fronts skim out along the floor both ways, and billows of warm dust burst behind the pillar.
            for (int i = 0; i < 2; i++)
                EarthArc(ground + new Vector2((i == 0 ? 1 : -1) * .5f, .14f), R * .95f, i == 0 ? 0 : 180, Alpha(amber, .95f), .26f, 117, .42f, new Vector2((i == 0 ? 1 : -1) * 3, 0));
            InkBurst(ground + Vector2.up * .7f * s, 1.2f * s, EarthDustDeep, 3, .85f, 27);

            // The pillar shoots up and stands a second (painted basalt lit from the upper left), its seams cooling from white
            // through gold to amber; then it sinks back into the floor as solid stone.
            const float Stand = 1, Sink = .35f;
            float width = 1.25f * s, height = EarthColumnRock.bounds.size.y / EarthColumnRock.bounds.size.x * width;
            Vector2 foot = ground + Vector2.down * .12f;
            var stone = Spawn("Earth eruption pillar", EarthColumnRock, foot, width, Color.white, Stand + .02f, false, 28);
            var seams = Spawn("Earth eruption pillar seams", EarthColumnVein, foot, width, new Color(1, 1, 1, .95f), Stand * .85f, true, 29);
            foreach (var x in new[] { stone, seams }) { x.axisStart = new Vector2(.75f, 0); x.axisEnd = Vector2.one; x.ease = EarthPop(x.life, .1f); x.fadePower = 0; }
            seams.ramp = Ramp(Color.white, EarthGoldHot, amber); seams.hold = .35f; seams.fadePower = 1.4f;
            var sink = EarthLater(Spawn("Earth eruption pillar sinking", EarthColumnRock, foot, width, Color.white, Sink, false, 28), Stand);
            sink.axisStart = Vector2.one; sink.axisEnd = new Vector2(.92f, 0); sink.ease = 1.3f; sink.hold = .7f; sink.fadePower = 1;
            // A brief gold flash on its crest.
            var crest = EarthLater(Glint(foot + Vector2.up * height * .9f, 1.4f * s, Alpha(EarthGoldHot, .95f), .2f), .07f); crest.spin = 0;

            // Fangs burst out round the pillar to the burst's edge, the outer ones a moment later; the short front ones stand
            // before the actors.
            for (int i = 0; i < 4; i++)
            {
                float t = i < 2 ? -1 + i * .45f : .55f + (i - 2) * .45f, x = t * R * .9f;
                bool front = i == 1 || i == 2;
                float fh = (1.8f - Mathf.Abs(t) * .7f) * s * Range(.85f, 1.05f) * (front ? .65f : 1);
                var fang = EarthFangRise(ground + new Vector2(x, front ? -.08f : 0), fh, Mathf.Sign(x) * Range(14, 28), front ? 128 : 27, Range(.75f, .9f), Mathf.Abs(t) * .06f, !front);
                EarthFangSink(fang, fang.sinkAt);
            }

            // The floor: a molten crack across the area, shock fronts racing out to its true edge and dust rolling with them.
            Crack(ground, R * 2.1f, Alpha(amber, .9f), 1.6f);
            GroundGlow(ground, R * 2.2f, Alpha(amber, .55f), 1.1f);
            EarthShock(ground + Vector2.up * .03f, R, Alpha(hot, .95f), .5f, 116, .32f);
            EarthShock(ground + Vector2.up * .03f, R * .7f, Alpha(amber, .85f), .42f, 117, .32f, .06f);
            for (int i = 0; i < 2; i++)
            {
                float side = i % 2 == 0 ? 1 : -1;
                var dust = Puff(ground + new Vector2(side * Range(.2f, .6f), .2f), Range(.9f, 1.3f) * s, i == 0 ? EarthDustDeep : EarthDustTone, new Vector2(side * Range(2.5f, 4.5f) * s, Range(.3f, 1.2f)), Range(.7f, 1), i == 0 ? 28 : 118);
                dust.drag = 2.2f; dust.endScale = 2.3f;
            }
            // Boulders are flung high and rain back; needles of molten rock fly up the pillar; dust climbs it.
            int boulders = Budget(2);
            for (int i = 0; i < boulders; i++) EarthBoulder(ground + new Vector2(Range(-.5f, .5f), Range(.5f, 2.5f) * s), s, new Vector2(Range(-4, 4) * s, Range(5, 9) * s), i % 2 == 0 ? 128 : 119, 0);
            EarthNeedles(ground + Vector2.up * 1.2f * s, 5, 90, 110, 8 * s, amber, .5f, .5f * s, 0);
            for (int i = 0; i < 2; i++)
            {
                var fume = EarthLater(Puff(ground + new Vector2(Range(-.4f, .4f), Range(1, 2.6f) * s), Range(1, 1.4f) * s, i == 0 ? EarthDustDeep : EarthSmoke, new Vector2(Range(-.3f, .3f), Range(.5f, 1)), Range(.9f, 1.1f), 27), .1f + i * .1f);
                fume.drag = 1; fume.endScale = 2.2f; fume.fadeIn = .1f;
            }
            // As it sinks: rock breaks off its flanks and a curtain of dust rolls out from its foot.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1 : 1;
                EarthBoulder(foot + new Vector2(side * width * .4f, Range(1.2f, 2.2f) * s), s * .8f, new Vector2(side * Range(1, 2.2f), Range(1, 2.5f)), 119, Stand);
                var curtain = EarthLater(Puff(ground + new Vector2(side * Range(.3f, .5f), .2f), Range(1, 1.3f) * s, EarthDustTone, new Vector2(side * Range(1.4f, 2.4f), Range(.4f, .9f)), Range(.7f, .9f), 118), Stand + .05f);
                curtain.drag = 2; curtain.endScale = 2.4f;
            }
        }

        private void EarthBoulder(Vector2 at, float s, Vector2 velocity, int order, float delay)
        {
            var chunk = Spawn("Earth boulder", EarthChipSet[random.Next(EarthChipSet.Length)], at, Range(.26f, .46f) * s, Color.white, Range(.9f, 1.3f), false, order);
            chunk.velocity = velocity; chunk.gravity = 18; chunk.spin = Range(-400, 400); chunk.hold = .6f; chunk.fadePower = .6f;
            EarthLater(chunk, delay);
        }

        /// <summary>One stride of the rift ending at <paramref name="front"/>: a scorched gash with a white-gold molten seam laid
        /// behind the front over a soft glow, so strides join end to end (both ends of the strip sit at mid-height). Returns the gash.</summary>
        private Clip EarthRiftSegment(Vector2 front, int side, Color amber, float delay)
        {
            int variant = random.Next(EarthRiftInk.Length); float flipY = Chance(.5f) ? -1 : 1, flipX = Chance(.5f) ? -1 : 1;
            Vector2 mid = front + new Vector2(-side * .3f, -.05f);
            var heat = Spawn("Earth rift glow", glow, mid + Vector2.up * .06f, 1.5f, Alpha(Color.Lerp(amber, EarthMagma, .3f), .8f), 1.1f, true, 25);
            heat.scale.y *= .36f; heat.root.transform.localScale = heat.scale; heat.fadeIn = .03f; heat.hold = .25f; heat.fadePower = 1.4f;
            EarthLater(heat, delay);
            var gash = Spawn("Earth rift", EarthRiftInk[variant], mid, .8f, Color.white, 2.4f, false, 24);
            var seam = Spawn("Earth rift magma", EarthRiftLight[variant], mid, .8f, Color.white, 1.6f, true, 26);
            foreach (var x in new[] { gash, seam })
            {
                x.scale.y *= 1.3f * flipY; x.scale.x *= flipX; x.root.transform.localScale = x.scale;
                x.axisStart = new Vector2(.4f, .25f); x.axisEnd = Vector2.one; x.ease = EarthPop(x.life, .06f); x.fadeIn = .02f;
                EarthLater(x, delay);
            }
            gash.hold = .6f; gash.fadePower = 1.2f;
            seam.ramp = Ramp(Color.white, EarthGoldHot, amber); seam.hold = .3f; seam.fadePower = 1.5f;
            return gash;
        }

        // A fang standing in the floor, remembered until it sinks: its look, its pose and when (from its spawn) it starts to sink.
        private struct EarthFang { public Sprite sprite; public Vector2 ground; public float width, tall, lean, sinkAt; public int order; }

        /// <summary>A fang of rock erupting from the floor: painted stone, with its glowing cracks cooling over it as light
        /// when <paramref name="veined"/>. <paramref name="lean"/> tilts its tip toward +x. It stands <paramref name="stand"/>
        /// seconds; <see cref="EarthFangSink"/> then lowers it back into the floor as solid rock.</summary>
        private EarthFang EarthFangRise(Vector2 ground, float height, float lean, int order, float stand, float delay, bool veined)
        {
            int variant = random.Next(EarthFangRock.Length);
            var sprite = EarthFangRock[variant];
            float width = height * Range(.42f, .56f), tall = height / sprite.bounds.size.y;
            var stone = Spawn("Earth fang", sprite, ground, width, Color.white, stand + .02f, false, order);
            var veins = veined ? Spawn("Earth fang veins", EarthFangVein[variant], ground, width, new Color(1, 1, 1, .95f), stand, true, order + 1) : null;
            foreach (var x in new[] { stone, veins })
            {
                if (x == null) continue;
                x.scale.y = tall; x.root.transform.localScale = x.scale;
                x.angle = -lean; x.axisStart = new Vector2(.7f, 0); x.axisEnd = Vector2.one; x.ease = EarthPop(x.life, .07f); x.fadePower = 0;
                EarthLater(x, delay);
            }
            if (veins != null) { veins.ramp = Ramp(EarthHot, EarthAmber, EarthMagma); veins.hold = .45f; veins.fadePower = 1.3f; }
            return new EarthFang { sprite = sprite, ground = ground, width = width, tall = tall, lean = lean, order = order, sinkAt = delay + stand };
        }

        /// <summary>The fang sinking back into the floor as solid rock after <paramref name="delay"/> seconds, fading only at the very end.</summary>
        private void EarthFangSink(EarthFang fang, float delay)
        {
            var sink = Spawn("Earth fang sinking", fang.sprite, fang.ground, fang.width, Color.white, .22f, false, fang.order);
            sink.scale.y = fang.tall; sink.root.transform.localScale = sink.scale; sink.angle = -fang.lean;
            sink.axisStart = Vector2.one; sink.axisEnd = new Vector2(.9f, 0); sink.ease = 1.3f; sink.hold = .7f; sink.fadePower = 1;
            EarthLater(sink, Mathf.Max(0, delay));
        }

        // ---------------------------------------------------------------- Orbit

        private void CastOrbit(CastInfo c)
        {
            EarthTextures();
            int g = Grade, n = g + 1; float w = c.windup, release = Mathf.Max(0, w - .02f), R = Mathf.Clamp(c.radius / .3f, 1, 2.6f), size = EarthOrbSize(g);
            Vector2 hub = c.pos + Vector2.down * .4f, ground = c.pos + Vector2.down;
            EarthOrbitFrom = c.anchor;
            if (w < .2f)
            {
                // An instant echo: a spark at the hub; the orbs themselves arrive with the guard at once.
                Converge(hub, 5, 1.6f, EarthAzure, w, .4f);
                var pip = EarthLater(Glint(hub, 1.6f, Alpha(EarthAzureHot, .9f), .2f), release); pip.spin = 0;
                EarthOrbitClock = pip;
                return;
            }
            float total = w + .25f, hold = w / total;
            // A glow pooling on the floor, the guard's seal turning in it and a deeper inner ring counter-turning.
            var pool = GroundGlow(ground, 3, Alpha(EarthAzure, .5f), total); pool.hold = hold; pool.fadeIn = .1f; pool.startScale = .5f; pool.ease = 2;
            EarthOrbitClock = pool;
            var seal = Spawn("Earth orbit cast seal", runeRing, ground, 2.7f, Alpha(EarthAzure, .85f), total, true, 27); LayFlat(seal, .3f);
            var inner = Spawn("Earth orbit cast seal inner", sigil, ground, 1.7f, Alpha(EarthAzureDeep, .75f), total, true, 27); LayFlat(inner, .3f);
            foreach (var x in new[] { seal, inner }) { x.spin = 90; x.fadeIn = .08f; x.startScale = .4f; x.endScale = 1; x.ease = 3; x.hold = hold; x.fadePower = 1.5f; }
            inner.spin = -140;

            // The orbs condense out of the caster: a spinner in the orbit plane widens from the body to the true orbit radius
            // and turns so that at the release each orb sits exactly where the guard's orb takes over; each orb drags its
            // painted crescent along the path.
            var spinner = Spawn("Earth orbit cast spinner", core, hub, core.bounds.size.x, Color.clear, w + .02f, true, 0);
            // A clear pivot: its renderer keeps its sprite (fully transparent, so it draws nothing) but is never animated again.
            spinner.renderer = null;
            LayFlat(spinner, OrbitTilt);
            spinner.spin = OrbitDegreesPerSecond; spinner.angle = -OrbitDegreesPerSecond * w;
            spinner.startScale = .15f; spinner.endScale = 1; spinner.ease = 1.6f;
            var rigs = new EarthOrbRig[n]; var layers = new List<Clip>();
            for (int i = 0; i < n; i++) rigs[i] = EarthOrb(spinner.root.transform, spinner.angle, i, n, R, size, g, layers);
            foreach (var clip in layers) { clip.life = w + .02f; clip.loop = false; clip.fadeIn = .08f; clip.fadePower = 0; }
            // The plasma keeps churning through its loop while the orbs live (and any other plasma layer with it).
            foreach (var rig in rigs) rig.layers[1].loop = true;
            EarthLoopPlasma(rigs);
            EarthEachFrame(seal, owner => { foreach (var rig in rigs) EarthOrbDepth(rig, spinner.angle); });
            Converge(hub, 12, 2.6f, EarthAzure, w, .45f);

            // Release: a hit spark at the hub and a shock front racing out along the orbit itself.
            var flash = EarthLater(Flash(hub, 2.4f, Alpha(EarthAzureHot, .75f), .2f), release); flash.ramp = Ramp(Color.white, EarthAzure);
            EarthShock(hub, R, Alpha(EarthAzure, .85f), .36f, 121, OrbitTilt, release);
            if (g >= 3) EarthShock(hub, R * .8f, Alpha(EarthGold, .8f), .32f, 122, OrbitTilt, release + .05f);
        }

        /// <summary>Every plasma layer of the orbs keeps looping its frames.</summary>
        private static void EarthLoopPlasma(EarthOrbRig[] rigs)
        {
            foreach (var rig in rigs) foreach (var clip in rig.layers) if (clip.frames != null) clip.loop = true;
        }

        private void ZoneOrbit(Transform root, float radius, float life, int count)
        {
            EarthTextures();
            int g = Grade, n = Mathf.Max(1, count); float R = Mathf.Max(.3f, radius), size = EarthOrbSize(g);
            float span = life + .1f, hold = Mathf.Clamp01(1 - .4f / span);
            var layers = new List<Clip>();
            // A clear pivot carries the frame clock (its sprite stays, fully transparent, so it draws nothing).
            var clock = Attach(root, "Earth orbit clock", core, .05f, Color.clear, 0);
            // The guard's seal turning in the floor plane under the hub over a soft pool of light; a deeper inner seal counter-turns
            // from level two and a gold rune ring rides outside it at level three.
            var floor = EarthPlane(root, new Vector2(0, -.58f), .3f);
            var pool = Attach(floor, "Earth orbit floor glow", glow, 2.9f, Alpha(EarthAzure, .42f), 26);
            var seal = Attach(floor, "Earth orbit seal", runeRing, 2.2f, Alpha(EarthAzure, .7f), 27); seal.spin = 50;
            var seals = new List<Clip> { pool, seal };
            if (g >= 2) { var inner = Attach(floor, "Earth orbit seal inner", sigil, 1.45f, Alpha(EarthAzureDeep, .7f), 27); inner.spin = -80; seals.Add(inner); }
            if (g >= 3) { var crown = Attach(floor, "Earth orbit seal crown", runeRing, 2.85f, Alpha(EarthGold, .6f), 27); crown.spin = -35; seals.Add(crown); }
            layers.AddRange(seals);
            // The orbs ride a spinner turning in the tilted orbit plane, so each sits exactly on OrbitOffset; they counter-turn
            // to stay upright and undo the plane's squash, so they stay round. Their trails turn with the spinner.
            var plane = EarthPlane(root, Vector2.zero, OrbitTilt);
            var spinner = Attach(plane, "Earth orbit spinner", core, core.bounds.size.x, Color.clear, 0);
            spinner.spin = OrbitDegreesPerSecond; layers.Add(spinner);
            var rigs = new EarthOrbRig[n];
            for (int i = 0; i < n; i++) rigs[i] = EarthOrb(spinner.root.transform, 0, i, n, R, size, g, layers);
            foreach (var clip in layers) { clip.life = span; clip.loop = false; clip.hold = hold; clip.fadePower = 1.2f; }
            foreach (var rig in rigs) rig.layers[1].loop = true;
            EarthLoopPlasma(rigs);
            foreach (var x in seals) { x.fadeIn = .15f; x.startScale = .5f; x.endScale = 1; x.ease = EarthPop(span, .2f); }
            clock.life = span; clock.loop = false;
            bool ended = false;
            EarthEachFrame(clock, owner =>
            {
                foreach (var rig in rigs) EarthOrbDepth(rig, spinner.angle);
                if (ended || owner.age < life - .1f) return;
                // The guard runs out: each orb bursts in a hit spark, a small shock front, needles and glass.
                ended = true;
                foreach (var rig in rigs)
                {
                    var body = rig.layers[1]; if (!body.root) continue;
                    Vector2 at = body.root.transform.position;
                    var pop = Flash(at, 1.5f, Alpha(EarthAzure, .9f), .16f); pop.ramp = Ramp(Color.white, EarthAzure);
                    EarthShock(at, .6f, Alpha(EarthAzureHot, .85f), .26f, 129);
                    EarthNeedles(at, 4, Range(0, 360), 300, 4.5f, EarthAzure, .26f, .32f);
                    for (int i = 0; i < 2; i++)
                    {
                        var piece = Spawn("Earth orb glass", shard, at, Range(.16f, .24f), Alpha(EarthAzure, .9f), Range(.3f, .45f), true, 129);
                        piece.velocity = Direction(Range(0, 360)) * Range(2, 4); piece.gravity = 6; piece.drag = 1.5f; piece.spin = Range(-600, 600); piece.angle = Range(0, 360); piece.fadePower = 1.3f;
                    }
                }
            });
            Bundle(root, clock, layers.ToArray());
            // Arrival: a shock front along the orbit and a star on each orb.
            Vector2 hub = root.position;
            EarthShock(hub, R, Alpha(EarthAzure, .8f), .32f, 121, OrbitTilt).startScale = .7f;
            for (int i = 0; i < n; i++) Glint(hub + OrbitOffset(i, n, 0, R), 1.1f, Alpha(EarthAzureHot, .9f), .18f, 0).spin = 0;

            // Cast by a companion away from the hero it guards: the orbs it condensed streak over and lock into the orbit.
            var from = EarthOrbitClock != null && clips.Contains(EarthOrbitClock) ? EarthOrbitFrom : null;
            EarthOrbitClock = null;
            if (!from) return;
            Vector2 start = (Vector2)from.position + Vector2.up * .6f;
            if ((start - hub).sqrMagnitude < .8f * .8f) return;
            const float Flight = .14f;
            var beamGlow = Line(start, hub, .9f, Alpha(EarthAzure, .35f), .24f, 120); beamGlow.renderer.sprite = glow; beamGlow.scale.y = .9f / glow.bounds.size.y; beamGlow.fadePower = 1.5f;
            var beam = Line(start, hub, .3f, Alpha(EarthAzure, .9f), .22f, 121); beam.fadePower = 1.5f;
            for (int i = 0; i < n; i++)
            {
                Vector2 to = hub + OrbitOffset(i, n, Flight, R), velocity = (to - start) / Flight;
                var flier = Spawn("Earth orb flight", plasmaFrames[0], start, size * 1.2f, Alpha(EarthOrbPlasma, 1), Flight, true, 123);
                var light = Spawn("Earth orb flight glow", glow, start, size * 2.2f, Alpha(EarthAzure, .5f), Flight, true, 122);
                flier.frames = plasmaFrames; flier.fps = 16; flier.loop = true;
                foreach (var x in new[] { flier, light }) { x.velocity = velocity; x.fadePower = 0; }
            }
            foreach (var rig in rigs) { foreach (var clip in rig.layers) clip.fadeIn = Flight; rig.trail.fadeIn = rig.trailGlow.fadeIn = Flight; }
        }

        // An orb striking an enemy as it passes: a hit spark, a shock front, light wedge lines and needles flung on.
        private void ImpactOrbit(Vector2 pos, float radius, Color tint, int facing)
        {
            EarthTextures();
            int g = Grade; float k = Mathf.Clamp(radius / .5f, .7f, 1.6f) * (1 + .1f * (g - 1));
            Color azure = EarthBlend(tint, EarthAzure, .3f);
            float heading = Range(0, 360);
            var flash = Flash(pos, 1.6f * k, Alpha(azure, .95f), .14f); flash.ramp = Ramp(Color.white, azure);
            EarthShock(pos, .6f * k, Alpha(EarthAzureHot, .85f), .26f, 129);
            ImpactLines(pos, 1.8f * k, Alpha(EarthAzureHot, .7f), .18f);
            EarthNeedles(pos, 5, heading, 150, 6 * k, azure, .26f, .36f);
            if (g >= 3) { var gold = Glint(pos, 1.1f * k, Alpha(EarthGold, .9f), .22f, 45); gold.spin = 90; }
        }

        private static float EarthOrbSize(int grade) { return .76f + .06f * (Mathf.Clamp(grade, 1, 3) - 1); }

        /// <summary>One orb riding <paramref name="spinner"/> (whose angle is <paramref name="spinStart"/> now): a glow, a plasma
        /// orb (the looped flipbook: filaments swirling inside, a lit rim, flares licking off it), a white-hot heart and, awakened,
        /// a painted gold crown, each counter-turning and unsquashed so it stays upright and round; behind it a
        /// painted light ribbon over a soft glow lies on the orbit itself and turns with the spinner.</summary>
        private EarthOrbRig EarthOrb(Transform spinner, float spinStart, int index, int count, float R, float size, int g, List<Clip> into)
        {
            float phase = index * 360f / count;
            Vector2 offset = Direction(phase) * R;
            // The halo stays faint (and dims further while the orb passes in front of the body) so the guarded hero stays clear.
            var rig = new EarthOrbRig { phase = phase, radius = R, haloAlpha = g >= 3 ? .42f : g == 2 ? .46f : .5f, trailAlpha = 1, glowAlpha = .5f };
            var halo = Attach(spinner, "Earth orb halo", glow, size * 2.9f, Alpha(EarthAzure, rig.haloAlpha), 121, true, offset);
            var shell = PlasmaOrb(spinner, "Earth orb", size * 1.75f, Alpha(EarthOrbPlasma, 1), 123, offset); shell.frames = plasmaFrames; shell.fps = 14 + index * 2;
            var light = Attach(spinner, "Earth orb heart", core, size * .42f, Alpha(EarthAzureHot, .95f), 124, true, offset + new Vector2(-.08f, .1f) * size); light.pulse = .08f;
            var list = new List<Clip> { halo, shell, light };
            // Awakened: a painted gold crown rings the ball, four short flares standing off it like a star.
            if (g >= 3) { var crown = Attach(spinner, "Earth orb crown", EarthOrbCrown, size * 1.62f, Alpha(EarthGold, .9f), 125, true, offset); crown.pulse = .05f; list.Add(crown); }
            rig.layers = list.ToArray(); rig.front = new int[list.Count]; rig.back = new int[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var clip = list[i];
                clip.spin = -OrbitDegreesPerSecond; clip.angle = -spinStart; clip.scale.y /= OrbitTilt; clip.root.transform.localScale = clip.scale;
                rig.front[i] = clip.renderer.sortingOrder; rig.back[i] = clip.renderer.sortingOrder - 100;
                into.Add(clip);
            }
            // The trail's circle (radius .86 of its sprite) is the orbit itself, its head under the orb: a soft glow ribbon under
            // the painted one, in a deeper blue so it glows instead of washing out on a pale sky.
            float trailWidth = 2 * R / .86f;
            rig.trailGlow = Attach(spinner, "Earth orb trail glow", EarthTrailGlow, trailWidth, Alpha(EarthAzureDeep, rig.glowAlpha), 120);
            rig.trail = Attach(spinner, "Earth orb trail", EarthTrailLight, trailWidth, Alpha(EarthAzure, rig.trailAlpha), 121);
            foreach (var x in new[] { rig.trailGlow, rig.trail }) { x.angle = phase; into.Add(x); }
            // From level two a few sparkles shed off the ball.
            if (g >= 2) Emitter(shell, 5 + 2 * g, .12f, (owner, p) =>
            {
                var mote = Spawn("Earth orb sparkle", fleck, p, Range(.14f, .22f), Alpha(g >= 3 ? EarthGoldHot : Color.white, .95f), Range(.25f, .4f), true, rig.behind ? 22 : 126);
                mote.velocity = Inside(1) + Vector2.down * .3f; mote.drag = 2; mote.flicker = .4f; mote.endScale = .2f; mote.ease = 1;
            });
            return rig;
        }

        /// <summary>Orbs on the far half of the ellipse draw behind the actors and a little smaller; an orb crossing in front of
        /// the body dims its halo so it never washes the hero out. A trail draws behind the actors while its middle is on the
        /// far half, and thins while it crosses in front of the body.</summary>
        private static void EarthOrbDepth(EarthOrbRig rig, float spin)
        {
            float angle = (spin + rig.phase) * Mathf.Deg2Rad, depth = Mathf.Sin(angle);
            rig.behind = depth > 0;
            float scale = 1 - .12f * depth;
            for (int i = 0; i < rig.layers.Length; i++)
            {
                var clip = rig.layers[i]; if (!clip.renderer) continue;
                clip.renderer.sortingOrder = rig.behind ? rig.back[i] : rig.front[i];
                clip.startScale = clip.endScale = scale;
            }
            var halo = rig.layers[0];
            float across = Mathf.Abs(Mathf.Cos(angle) * rig.radius);
            halo.color = Alpha(halo.color, rig.behind ? rig.haloAlpha : Mathf.Lerp(.12f, rig.haloAlpha, Smooth(.3f, .6f, across)));
            float mid = angle - EarthTrailSpan * .48f * Mathf.Deg2Rad;
            bool back = Mathf.Sin(mid) > 0;
            float clear = back ? 1 : Mathf.Lerp(.4f, 1, Smooth(.3f, .75f, Mathf.Abs(Mathf.Cos(mid) * rig.radius)));
            if (rig.trail.renderer) { rig.trail.renderer.sortingOrder = back ? 21 : 121; rig.trail.color = Alpha(rig.trail.color, rig.trailAlpha * clear); }
            if (rig.trailGlow.renderer) { rig.trailGlow.renderer.sortingOrder = back ? 20 : 120; rig.trailGlow.color = Alpha(rig.trailGlow.color, rig.glowAlpha * clear); }
        }

        // ---------------------------------------------------------------- Decoy

        private void CastDecoy(CastInfo c)
        {
            EarthTextures();
            int g = Grade, side = c.facing; float w = c.windup, release = Mathf.Max(0, w - .02f);
            Color pale = EarthBlend(c.tint, EarthPale, .3f);
            Vector2 ground = c.pos + Vector2.down;
            if (w < .2f)
            {
                // An instant echo: a flicker of mirror light; the phantom itself appears at once.
                var pip = EarthLater(Glint(c.pos, 1.6f, Alpha(pale, .9f), .2f), release); pip.spin = 0;
                Sparkles(c.pos, 4, .6f, pale);
                return;
            }
            float total = w + .25f, hold = w / total;
            // A mirror seal under the caster, turning in a pool of pale light.
            var pool = GroundGlow(ground, 2.9f, Alpha(pale, .5f), total); pool.hold = hold; pool.fadeIn = .1f; pool.startScale = .5f; pool.ease = 2;
            var seal = Spawn("Earth decoy seal", runeRing, ground, 2.6f, Alpha(pale, .85f), total, true, 27); LayFlat(seal, .3f);
            seal.spin = -110 * side; seal.fadeIn = .08f; seal.startScale = .4f; seal.endScale = 1; seal.ease = 3; seal.hold = hold; seal.fadePower = 1.5f;

            // The caster's image splits: two flat pale afterimages peel away to either side, flickering, while mirror light gathers.
            if (c.anchor)
            {
                Vector2 feet = c.anchor.position;
                var snapshots = new Dictionary<MeshFilter, Mesh>();
                for (int i = 0; i < 2; i++)
                {
                    // Both copies share one snapshot of the pose; they die together, so the first owns it.
                    int copyFacing;
                    var echo = EarthGhost(c.anchor, transform, feet, 1, true, out copyFacing, snapshots, i == 0);
                    if (echo == null) break;
                    float dir = i == 0 ? side : -side, travel = i == 0 ? .5f : .3f;
                    echo.color = Alpha(pale, i == 0 ? .36f : .24f); echo.velocity = new Vector2(dir * travel / w, 0);
                    echo.life = w + .06f; echo.fadeIn = w * .35f; echo.hold = .75f; echo.fadePower = 2; echo.flicker = .35f;
                }
            }
            Converge(c.pos, 10, 2, pale, w, .5f);
            for (int i = 0; i < 3; i++)
            {
                var star = EarthLater(Glint(c.pos + new Vector2(Range(-.8f, .8f), Range(-.8f, .9f)), Range(.6f, .9f), Alpha(i % 2 == 0 ? Color.white : pale, .9f), .2f, Range(0, 45)), Range(0, Mathf.Max(.01f, w - .15f)));
                star.spin = 0;
            }
            for (int i = 0; i < 2; i++)
            {
                var mist = EarthLater(Puff(ground + new Vector2(Range(-.7f, .7f), .15f), Range(.7f, 1), EarthMist, new Vector2(Range(-.4f, .4f), Range(.2f, .5f)), w * .9f, 116), Range(0, w * .3f));
                mist.drag = 1; mist.fadeIn = .1f; mist.endScale = 2;
            }

            // Release: the split snaps with a pale hit spark, a cross glint, a shock front and light wedge lines, and the phantom
            // slides out (the zone takes over).
            var flash = EarthLater(Flash(c.pos, 2.4f, Alpha(pale, .9f), .18f), release); flash.ramp = Ramp(Color.white, pale);
            var cross = EarthLater(Glint(c.pos, 2.4f, Color.white, .22f), release); cross.spin = 0;
            EarthLater(Wave(c.pos, 3, Alpha(pale, .8f), .3f, 3), release);
            EarthLater(ImpactLines(c.pos, 2.9f, Alpha(pale, .7f), .22f), release);
        }

        private void ZoneDecoy(Transform root, Transform caster, float radius, float life, Color tint)
        {
            EarthTextures();
            int g = Grade; Color pale = EarthBlend(tint, EarthPale, .3f);
            Vector2 at = root.position, feet = new Vector2(0, -.6f);
            float span = life + .6f, hold = Mathf.Clamp01((life - .45f) / span), rise = EarthPop(span, .2f);
            var rec = new EarthPhantom { root = root };
            if (caster)
            {
                float dx = at.x - caster.position.x;
                rec.facing = Mathf.Abs(dx) > .05f ? (dx > 0 ? 1 : -1) : 1;
                rec.slideFrom = Mathf.Clamp(-dx, -2.5f, 2.5f);
                if (Mathf.Abs(dx) > 2.5f)
                {
                    // Cast by a companion far from where it stands: a pale streak carries it the rest of the way.
                    Vector2 chest = (Vector2)caster.position + Vector2.up * .8f;
                    var streakGlow = Line(chest, at + Vector2.up * .2f, .8f, Alpha(pale, .35f), .24f, 120); streakGlow.renderer.sprite = glow; streakGlow.scale.y = .8f / glow.bounds.size.y; streakGlow.fadePower = 1.5f;
                    var streak = Line(chest, at + Vector2.up * .2f, .26f, Alpha(pale, .8f), .22f, 121); streak.fadePower = 1.5f;
                }
                int copyFacing;
                // A translucent cyan body under the pale light flooding it, and a fainter light copy lagging a beat behind as an
                // afterimage, all frozen copies of the caster's art sharing one snapshot of its pose: a luminous ghost of the
                // caster. The light copy may outlive the root (it lifts away when the phantom shatters), so it owns the snapshot.
                var snapshots = new Dictionary<MeshFilter, Mesh>();
                var bodyGhost = EarthGhost(caster, root, feet, 0, false, out copyFacing, snapshots, false);
                if (bodyGhost != null)
                {
                    rec.copyFacing = copyFacing;
                    var echoGhost = EarthGhost(caster, root, feet, 1, false, out copyFacing, snapshots, false);
                    var lightGhost = EarthGhost(caster, root, feet, 1, false, out copyFacing, snapshots, true);
                    bodyGhost.color = Alpha(Color.Lerp(pale, EarthCyan, .55f), .6f);
                    echoGhost.color = Alpha(Color.Lerp(pale, EarthCyanDeep, .4f), .2f); echoGhost.flicker = .3f;
                    lightGhost.color = Alpha(Color.Lerp(pale, EarthCyan, .15f), .32f); lightGhost.flicker = .06f;
                    rec.light = lightGhost;
                    EarthPhantomBody(rec, echoGhost, feet, g >= 3 ? .12f : .09f);
                    EarthPhantomBody(rec, bodyGhost, feet);
                    EarthPhantomBody(rec, lightGhost, feet);
                }
            }
            if (rec.body.Count == 0)
            {
                // No caster art to copy: a cloaked spectral warrior stands in.
                var rimShape = Attach(root, "Earth wraith rim", EarthWraith, 1.04f, Alpha(pale, .45f), 27, true, feet + new Vector2(0, -.04f)); rimShape.flicker = .3f;
                var echoShape = Attach(root, "Earth wraith echo", EarthWraith, .95f, Alpha(EarthCyanDeep, .35f), 28, true, feet); echoShape.flicker = .3f;
                var lightShape = Attach(root, "Earth wraith", EarthWraith, .95f, Alpha(pale, .7f), 125, true, feet); lightShape.flicker = .12f;
                rec.light = lightShape; rec.copyFacing = 1;
                EarthPhantomBody(rec, rimShape, feet + new Vector2(0, -.04f)); EarthPhantomBody(rec, echoShape, feet, .09f); EarthPhantomBody(rec, lightShape, feet);
            }
            foreach (var part in rec.body)
            {
                part.life = span; part.loop = false; part.fadeIn = .15f; part.hold = hold; part.fadePower = 1;
                part.axisStart = new Vector2(1.15f, .55f); part.axisEnd = Vector2.one; part.ease = rise;
            }
            EarthPhantomPose(rec, 0);

            // It glows: a tall soft aura behind the ghost (behind the actors), and a pool of pale light on the floor under it;
            // a turning rune ring at level two, a gold one at three.
            var halo = Attach(root, "Earth phantom aura", glow, 1.9f, Alpha(Color.Lerp(pale, EarthCyan, .3f), .4f), 29, true, feet + new Vector2(0, .85f));
            halo.scale.y *= 1.45f; halo.root.transform.localScale = halo.scale; halo.pulse = .05f;
            var plane = EarthPlane(root, new Vector2(0, -.58f), .3f);
            var pool = Attach(plane, "Earth phantom floor glow", glow, 2.6f, Alpha(pale, .45f), 26);
            var extras = new List<Clip> { pool, halo };
            if (g >= 2) { var runes = Attach(plane, "Earth phantom runes", runeRing, 2.2f, Alpha(pale, .7f), 27); runes.spin = 70; extras.Add(runes); }
            if (g >= 3) { var crown = Attach(plane, "Earth phantom crown", runeRing, 2.8f, Alpha(EarthGold, .65f), 27); crown.spin = -45; extras.Add(crown); }
            foreach (var x in extras) { x.life = span; x.loop = false; x.fadeIn = .2f; x.hold = hold; x.fadePower = 1; x.startScale = .4f; x.endScale = 1; x.ease = rise; }
            // A clear pivot carries the frame clock (its sprite stays, fully transparent, so it draws nothing).
            var aura = Attach(root, "Earth phantom clock", core, .05f, Color.clear, 0);
            aura.life = span; aura.loop = false;
            rec.primary = aura;

            // Mist pools at its feet and pale sparkles rise off the body.
            int beat = 0;
            Emitter(pool, 5 + 2 * g, 0, (owner, p) =>
            {
                if (!rec.root) return;
                Vector2 base0 = (Vector2)rec.root.position + feet;
                if (beat++ % 3 == 0)
                {
                    var mist = Puff(base0 + new Vector2(Range(-.5f, .5f), .12f), Range(.6f, .9f), EarthMist, new Vector2(Range(-.4f, .4f), Range(.15f, .45f)), Range(.8f, 1.1f), 116);
                    mist.endScale = 2; mist.fadeIn = .15f; mist.drag = 1.2f;
                    return;
                }
                var soul = Spawn("Earth phantom mote", fleck, base0 + new Vector2(Range(-.32f, .32f), Range(.3f, 1.6f)), Range(.16f, .26f), Alpha(Chance(.3f) ? Color.white : pale, .9f), Range(.45f, .65f), true, 126);
                soul.velocity = new Vector2(Range(-.2f, .2f), Range(.7f, 1.3f)); soul.drag = .5f; soul.flicker = .3f; soul.endScale = .3f; soul.ease = 1;
            });
            EarthEachFrame(aura, owner => EarthPhantomPose(rec, owner.age));
            EarthPhantoms.RemoveAll(x => !x.root); EarthPhantoms.Add(rec);
            var layers = new List<Clip>(rec.body); layers.AddRange(extras);
            Bundle(root, aura, layers.ToArray());

            // Arrival: a pale hit spark where it stands and a small shock front on the floor.
            var arrive = Flash(at + Vector2.up * .3f, 1.8f, Alpha(pale, .85f), .18f); arrive.ramp = Ramp(Color.white, pale);
            Glint(at + Vector2.up * .35f, 1.5f, Alpha(Color.white, .9f), .2f, 0).spin = 0;
            EarthShock(at + new Vector2(0, -.57f), 1, Alpha(pale, .85f), .32f, 116, .32f);
        }

        // Each attack: the phantom turns and lunges with a spectral crescent; facing 0 is its shattering when it expires.
        private void ImpactDecoy(Vector2 pos, float radius, Color tint, int facing)
        {
            EarthTextures();
            int g = Grade; Color pale = EarthBlend(tint, EarthPale, .3f);
            var rec = EarthFindPhantom(pos + Vector2.down * .2f);
            if (facing == 0) { EarthShatter(pos, radius, pale, rec, g); return; }
            int side = facing < 0 ? -1 : 1;
            if (rec != null) { rec.facing = side; rec.lungeAt = rec.clock; }

            // A flattened crescent reaching the attack's radius sweeps down and forward on a diagonal: a pale cyan painted stroke
            // under a white razor edge over a soft glow, with a lagging echo; a faint short arc on the far side shows the cut also
            // catches foes behind. Speed lines trail the lunge like an afterimage's smear.
            float W = Mathf.Clamp(radius * 1.75f, 1.6f, 3.4f), start = Range(-13, 3), angle = side > 0 ? start : 180 - start, swing = -side * 320;
            Vector2 c = pos + new Vector2(side * .35f, .05f);
            Color blade = Color.Lerp(pale, EarthCyan, .65f);
            var bloom = Spawn("Earth phantom cut glow", glow, c + new Vector2(side * W * .22f, -.05f), W * 1.05f, Alpha(blade, .28f), .26f, true, 128);
            bloom.scale.y *= .55f; bloom.root.transform.localScale = bloom.scale; bloom.startScale = .7f; bloom.ease = 3; bloom.fadePower = 1.5f;
            var arc = Crescent(c, W, Alpha(blade, .95f), .24f, angle, 130); arc.startScale = .8f; arc.ease = 6; arc.hold = .3f; arc.fadePower = 1.1f;
            var edge = Crescent(c + new Vector2(side * .05f, 0), W * .95f, new Color(1, 1, 1, .9f), .16f, angle, 131); edge.startScale = .82f; edge.ease = 6; edge.hold = .2f;
            var echo = EarthLater(Crescent(c - new Vector2(side * .12f, 0), W * .92f, Alpha(blade, .5f), .24f, angle + side * 20, 129), .03f);
            var rear = Crescent(pos + new Vector2(-side * .3f, .05f), W * .7f, Alpha(pale, .35f), .2f, side > 0 ? 180 - start : start, 129);
            foreach (var x in new[] { arc, edge, echo, rear }) { EarthSquash(x, .62f); x.spin = swing; }
            rear.spin = -swing;
            Flash(c + new Vector2(side * W * .38f, -.15f), 1.3f, Alpha(pale, .9f), .12f);
            EarthNeedles(c + new Vector2(side * W * .35f, 0), 4, side > 0 ? 5 : 175, 70, 5.5f, pale, .28f, .38f);
            for (int i = 0; i < 2; i++)
            {
                float y = pos.y + (i == 0 ? .25f : -.3f) + Range(-.08f, .08f), from = pos.x - side * Range(.15f, .35f);
                var line = Line(new Vector2(from, y), new Vector2(from - side * Range(1, 1.5f), y), Range(.07f, .11f), i == 0 ? new Color(1, 1, 1, .85f) : Alpha(pale, .9f), .16f, 128);
                line.velocity = new Vector2(-side * 3, 0); line.fadePower = 1.5f;
            }
            int pieces = Budget(2);
            for (int i = 0; i < pieces; i++)
            {
                var piece = Spawn("Earth phantom glass", shard, c + new Vector2(side * W * .3f, Range(-.4f, .4f)), Range(.16f, .26f), Alpha(pale, .9f), Range(.35f, .5f), true, 129);
                piece.velocity = new Vector2(side * Range(3, 5), Range(-.5f, 2)); piece.gravity = 6; piece.drag = 1.5f; piece.spin = Range(-600, 600); piece.angle = Range(0, 360); piece.fadePower = 1.3f;
            }
            if (g >= 2)
            {
                // A second cut, a beat later and steeper, crosses the first in an X.
                float crossAngle = angle + side * 70;
                Vector2 cc = c + new Vector2(side * .2f, .25f);
                var cross = EarthLater(Crescent(cc, W * .93f, Alpha(Color.Lerp(pale, EarthCyan, .65f), .9f), .22f, crossAngle, 130), .1f);
                var crossEdge = EarthLater(Crescent(cc + new Vector2(side * .04f, 0), W * .88f, new Color(1, 1, 1, .8f), .15f, crossAngle, 131), .1f);
                foreach (var x in new[] { cross, crossEdge }) { EarthSquash(x, .62f); x.spin = swing; }
            }
            if (g >= 3)
            {
                var gold = EarthLater(Crescent(c + new Vector2(side * .08f, 0), W * 1.02f, Alpha(EarthGold, .7f), .2f, angle, 131), .01f);
                EarthSquash(gold, .62f); gold.spin = swing;
                Glint(c + new Vector2(side * W * .38f, -.15f), 1.3f, Alpha(EarthGoldHot, .95f), .2f, 0).spin = 0;
            }
        }

        // The phantom expires: its light flares white and lifts away while it bursts in a pale cyan blast, a hit spark, shock
        // fronts, spectral glass, needles and pale mist.
        private void EarthShatter(Vector2 pos, float radius, Color pale, EarthPhantom rec, int g)
        {
            float R = Mathf.Clamp(radius, .8f, 3.5f), s = Mathf.Clamp(R / 1.8f, .7f, 1.5f);
            Vector2 ground = pos + Vector2.down * .8f;
            if (rec != null) EarthReleaseGhost(rec);
            var shade = Underlay(pos, 3.6f * s, .3f, .5f); shade.hold = .2f;
            Blast(pos, 2.7f * s, Color.Lerp(pale, EarthCyan, .35f), .48f, 128);
            var spark = SparkFlip(pos, 2.6f * s, Alpha(pale, .95f), .26f, 132); spark.ramp = Ramp(Color.white, pale);
            var star = Glint(pos, 2.8f * s, Color.white, .28f); star.spin = 0;
            ImpactLines(pos, 4 * s, Alpha(pale, .75f), .26f);
            Wave(pos, R * 1.6f / .74f, Alpha(pale, .85f), .36f, 3);
            EarthNeedles(pos, 6, 90, 320, 6.5f * s, pale, .4f, .45f * s);
            EarthShock(ground + Vector2.up * .03f, R, Alpha(pale, .9f), .46f, 116, .32f);
            int pieces = Budget(8);
            for (int i = 0; i < pieces; i++)
            {
                bool hot = i % 2 == 0;
                Vector2 dir = Direction(i * 360f / pieces + Range(-15, 15));
                var piece = Spawn("Earth phantom shard", shard, pos + dir * .2f + new Vector2(0, Range(-.4f, .5f)), Range(.22f, .4f) * s, hot ? Alpha(pale, .95f) : Alpha(EarthCyanDeep, .85f), Range(.55f, .85f), true, 129);
                piece.velocity = dir * Range(3, 6.5f) * s + Vector2.up * 1.2f; piece.gravity = 7; piece.drag = 1.3f; piece.spin = Range(-600, 600); piece.angle = Range(0, 360); piece.hold = .3f; piece.fadePower = 1.3f;
            }
            for (int i = 0; i < 3; i++)
            {
                var mist = Puff(i == 2 ? pos + Inside(.3f) : ground + new Vector2(Range(-.6f, .6f), .2f), Range(.8f, 1.1f) * s, EarthMist, new Vector2(Range(-1.5f, 1.5f), Range(.3f, .8f)), Range(.7f, .95f), 116);
                mist.endScale = 2.2f;
            }
            if (g >= 3)
            {
                Glint(pos + Vector2.up * .2f, 2 * s, Alpha(EarthGoldHot, .95f), .3f, 45).spin = 60;
                Wave(pos, R * 1.2f / .74f, Alpha(EarthGold, .8f), .32f, 3);
            }
        }

        private EarthPhantom EarthFindPhantom(Vector2 at)
        {
            EarthPhantoms.RemoveAll(x => !x.root);
            EarthPhantom best = null; float bestDistance = .8f;
            foreach (var x in EarthPhantoms)
            {
                float d = Vector2.Distance(x.root.position, at);
                if (d < bestDistance) { bestDistance = d; best = x; }
            }
            return best;
        }

        // Frees the phantom's light copy from its dying root: it flares white, swells and lifts away.
        private void EarthReleaseGhost(EarthPhantom rec)
        {
            var clip = rec.light; rec.light = null;
            if (clip == null || !clip.root || ReferenceEquals(clip.external, null) || !clips.Contains(clip)) return;
            if (rec.primary != null && rec.primary.layers != null) rec.primary.layers.Remove(clip);
            var t = clip.root.transform; t.SetParent(transform, true);
            clip.external = null; clip.position = t.position; clip.velocity = Vector2.up * .9f; clip.drag = 1;
            clip.age = 0; clip.life = .42f; clip.hold = 0; clip.fadeIn = 0; clip.fadePower = 1.6f; clip.flicker = 0; clip.pulse = 0;
            clip.color = Color.white; clip.ramp = Ramp(Color.white, EarthPale);
            clip.startScale = 1; clip.endScale = 1.16f; clip.ease = 2; clip.axisStart = clip.axisEnd = Vector2.one;
        }

        private static void EarthPhantomBody(EarthPhantom rec, Clip clip, Vector2 rest, float lag = 0) { clip.offset = rest; rec.body.Add(clip); rec.rest.Add(rest); rec.lag.Add(lag); }

        // The phantom's pose each frame: it slides out of the caster, bobs, lunges toward its target and faces it; the
        // afterimage copy follows the same motion a beat late, so it peels away on the slide and every lunge.
        private static void EarthPhantomPose(EarthPhantom rec, float t)
        {
            rec.clock = t;
            int mirror = rec.facing * rec.copyFacing;
            for (int i = 0; i < rec.body.Count; i++)
            {
                var part = rec.body[i]; if (ReferenceEquals(part.external, null) || !part.root) continue;
                part.offset = rec.rest[i] + EarthPhantomShift(rec, t - rec.lag[i]); part.scale.x = Mathf.Abs(part.scale.x) * mirror;
                part.root.transform.localPosition = part.offset;
            }
        }

        private static Vector2 EarthPhantomShift(EarthPhantom rec, float t)
        {
            float slide = rec.slideFrom * Mathf.Pow(1 - Mathf.Clamp01(Mathf.Max(0, t) / .24f), 3);
            float since = t - rec.lungeAt, lunge = since < 0 ? 0 : since < .07f ? since / .07f : Mathf.Exp(-(since - .07f) * 9);
            return new Vector2(slide + rec.facing * .36f * lunge, Mathf.Sin(t * 2.6f) * .04f);
        }

        /// <summary>A frozen copy of an actor's art (sprites and weighted meshes) with the actor's feet at <paramref name="at"/>.
        /// Mode 0 is a tinted alpha copy drawn just below the actor's orders (the ghost's translucent body), 1 flat light
        /// (additive at a high gain, so the art floods to a pale silhouette that keeps only its darkest lines). Rides
        /// <paramref name="parent"/> (at a local offset), or moves freely in the world when <paramref name="free"/>. Null when
        /// there is nothing to copy.</summary>
        private Clip EarthGhost(Transform actor, Transform parent, Vector2 at, int mode, bool free, out int facing, Dictionary<MeshFilter, Mesh> snapshots, bool own)
        {
            facing = 1;
            var sprites = new List<SpriteRenderer>(); var meshes = new List<MeshRenderer>();
            int low = int.MaxValue, high = int.MinValue;
            foreach (var source in actor.GetComponentsInChildren<SpriteRenderer>())
            {
                if (source.sprite == null || !EarthCopyable(source)) continue;
                sprites.Add(source); low = Mathf.Min(low, source.sortingOrder); high = Mathf.Max(high, source.sortingOrder);
            }
            foreach (var source in actor.GetComponentsInChildren<MeshRenderer>())
            {
                var filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || !EarthCopyable(source)) continue;
                meshes.Add(source); low = Mathf.Min(low, source.sortingOrder); high = Mathf.Max(high, source.sortingOrder);
            }
            if (sprites.Count + meshes.Count == 0) return null;
            // Light keeps the actor's own orders; the body sits just below all of them.
            int spanOrders = high - low, shift = mode == 1 ? 0 : -spanOrders - 1;
            var root = new GameObject(mode == 1 ? "Earth phantom light" : "Earth phantom body");
            root.transform.SetParent(parent, false);
            if (free) root.transform.position = new Vector3(at.x, at.y, 0); else root.transform.localPosition = at;
            Vector3 origin = actor.position;
            var copies = new List<SpriteRenderer>(); var meshCopies = new List<MeshRenderer>(); var owned = new List<Mesh>();
            foreach (var source in sprites)
            {
                var part = new GameObject("Phantom " + source.name); part.transform.SetParent(root.transform, false);
                part.transform.localPosition = source.transform.position - origin; part.transform.localRotation = source.transform.rotation; part.transform.localScale = source.transform.lossyScale;
                var copy = part.AddComponent<SpriteRenderer>(); copy.sprite = source.sprite; copy.flipX = source.flipX; copy.flipY = source.flipY;
                copy.sharedMaterial = mode == 1 ? EarthFlatSpriteMaterial : AlphaBlend; copy.sortingLayerID = source.sortingLayerID; copy.sortingOrder = source.sortingOrder + shift;
                copies.Add(copy);
            }
            var block = new MaterialPropertyBlock();
            foreach (var source in meshes)
            {
                var filter = source.GetComponent<MeshFilter>();
                var part = new GameObject("Phantom " + source.name); part.transform.SetParent(root.transform, false);
                part.transform.localPosition = source.transform.position - origin; part.transform.localRotation = source.transform.rotation; part.transform.localScale = source.transform.lossyScale;
                // Weighted vertices keep moving in the source mesh, so the copy draws a snapshot of this pose, shared by the copies
                // made together (and destroyed by the one that owns them).
                Mesh mesh;
                if (snapshots == null || !snapshots.TryGetValue(filter, out mesh) || !mesh)
                {
                    mesh = Instantiate(filter.sharedMesh); mesh.name = "Phantom " + filter.sharedMesh.name;
                    if (snapshots != null) snapshots[filter] = mesh;
                }
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                if (snapshots == null || own) owned.Add(mesh);
                var copy = part.AddComponent<MeshRenderer>();
                var materials = source.sharedMaterials; var tinted = new Material[materials.Length];
                for (int i = 0; i < materials.Length; i++) tinted[i] = mode == 1 ? EarthFlatMaterial(materials[i]) : EarthInkMaterial(materials[i]);
                copy.sharedMaterials = tinted; copy.sortingLayerID = source.sortingLayerID; copy.sortingOrder = source.sortingOrder + shift;
                copy.SetPropertyBlock(block); meshCopies.Add(copy);
            }
            float sign = sprites.Count > 0 ? sprites[0].transform.lossyScale.x * (sprites[0].flipX ? -1 : 1) : meshes[0].transform.lossyScale.x;
            facing = sign < 0 ? -1 : 1;
            var clip = new Clip { root = root, external = free ? null : parent, offset = free ? Vector2.zero : at, position = at, copies = copies.ToArray(), meshCopies = meshCopies.ToArray(),
                ownedMeshes = owned.ToArray(), meshTint = block, scale = Vector3.one, color = Color.white, life = Infinite, fadePower = 0, loop = true, seed = sequence++, fresh = true };
            clips.Add(clip); spawned.Add(clip);
            return clip;
        }

        // Only the actor's own art is copied: no shadows, no effect sprites riding on it, nothing outside the actor orders.
        private static bool EarthCopyable(Renderer source)
        {
            if (!source.enabled || !source.gameObject.activeInHierarchy || source.sortingOrder < 30 || source.sortingOrder > 70) return false;
            var material = source.sharedMaterial;
            if (material != null && (material == additiveMaterial || material == alphaMaterial || ghostMaterials.ContainsValue(material) || EarthInkMaterials.ContainsValue(material)
                || material == EarthFlatSprite || EarthFlatMaterials.ContainsValue(material))) return false;
            return !source.gameObject.name.Contains("Shadow");
        }

        /// <summary>The phantom's flat light for sprite copies (their texture comes from the sprite).</summary>
        private static Material EarthFlatSpriteMaterial
        {
            get
            {
                if (EarthFlatSprite) return EarthFlatSprite;
                EarthFlatSprite = new Material(Additive) { name = "Phantom flat light" };
                if (EarthFlatSprite.HasProperty("_Gain")) EarthFlatSprite.SetFloat("_Gain", EarthFlatGain);
                return EarthFlatSprite;
            }
        }

        /// <summary>The phantom's flat light for a mesh copy drawn with <paramref name="source"/>'s texture.</summary>
        private static Material EarthFlatMaterial(Material source)
        {
            Material flat;
            if (source != null && EarthFlatMaterials.TryGetValue(source, out flat) && flat) return flat;
            flat = new Material(Additive) { name = "Phantom flat light " + (source != null ? source.name : "") };
            if (flat.HasProperty("_Gain")) flat.SetFloat("_Gain", EarthFlatGain);
            if (source != null && source.HasProperty("_MainTex")) flat.mainTexture = source.mainTexture;
            if (source != null) EarthFlatMaterials[source] = flat;
            return flat;
        }

        private static Material EarthInkMaterial(Material source)
        {
            Material ink;
            if (source != null && EarthInkMaterials.TryGetValue(source, out ink) && ink) return ink;
            ink = new Material(AlphaBlend) { name = "Phantom ink " + (source != null ? source.name : "") };
            if (source != null && source.HasProperty("_MainTex")) ink.mainTexture = source.mainTexture;
            if (source != null) EarthInkMaterials[source] = ink;
            return ink;
        }

        // ---------------------------------------------------------------- Mine

        private void CastMine(CastInfo c)
        {
            EarthTextures();
            int g = Grade, side = c.facing, n = g + 1; float w = c.windup, release = Mathf.Max(0, w - .02f);
            Color pink = EarthBlend(c.tint, EarthPink, .3f), hot = Color.Lerp(pink, EarthPinkHot, .6f);
            Vector2 hand = c.pos + new Vector2(side * .95f, -.15f), ground = c.pos + Vector2.down;
            EarthMineHand = hand; EarthMineFrame = Time.frameCount; EarthMineAnchor = c.anchor;
            if (c.anchor) EarthMineLocal = c.anchor.InverseTransformPoint(new Vector3(hand.x, hand.y, 0));
            if (w < .2f)
            {
                // An instant echo: a flick of the hand; the seals fly with the mines that follow at once.
                var pip = EarthLater(Glint(hand, 1.4f, Alpha(hot, .9f), .18f), release); pip.spin = 0;
                return;
            }
            float total = w + .2f, hold = w / total;
            // One faint bloom in the palm.
            var palm = Anchored(c.anchor, hand, "Earth mine palm", glow, 1.2f, Alpha(pink, .55f), w + .06f, 124);
            palm.fadeIn = .05f; palm.startScale = .35f; palm.endScale = 1.1f; palm.ease = 1.5f; palm.hold = .8f; palm.fadePower = 2; palm.pulse = .08f;
            // The glowing glyphs form whirling round the hand, one for each mine, each in its own small bloom.
            for (int i = 0; i < n; i++)
            {
                for (int layer = 0; layer < 2; layer++)
                {
                    bool lit = layer == 1;
                    var token = Spawn(lit ? "Earth mine token" : "Earth mine token glow", lit ? EarthSeal : glow, hand, lit ? .6f : 1, lit ? Alpha(pink, .95f) : Alpha(pink, .45f), w, true, lit ? 129 : 128);
                    token.orbitCenter = hand; token.orbitAngle = i * 360f / n; token.orbit = 400 * side; token.orbitRadius = .7f; token.orbitRadiusEnd = .3f; token.orbitPower = .7f;
                    token.spin = 300; token.startScale = .2f; token.endScale = 1; token.ease = 2; token.fadeIn = .08f; token.hold = .85f; token.fadePower = 2;
                }
            }
            Converge(hand, 8, 1.6f, pink, w, .5f);
            var sigilFloor = GroundSigil(ground, 2.4f, Alpha(pink, .7f), total, 90); sigilFloor.hold = hold;
            // Release: a jagged flick of light at the hand as the seals are thrown.
            var flash = EarthLater(Flash(hand, 1.5f, Alpha(pink, .9f), .13f), release); flash.ramp = Ramp(Color.white, pink);
            EarthNeedles(hand, 3, side > 0 ? 20 : 160, 50, 5, pink, .22f, .32f, release);
        }

        private void ZoneMine(Transform root, float radius, float life)
        {
            EarthTextures();
            int g = Grade; float R = Mathf.Max(.5f, radius), size = 1.15f + .12f * (g - 1), span = life + 1;
            Vector2 at = root.position, ground = at + Vector2.down * .6f;
            // The toss: a mine laid by a cast just now flies there from the caster's hand (where the hand is now, so a caster
            // who moved through the wind-up, or a companion laying mines by the hero, still throws from its own hand).
            Vector2 hand = EarthMineAnchor ? (Vector2)EarthMineAnchor.TransformPoint(EarthMineLocal) : EarthMineHand;
            float land = 0, dx = ground.x - hand.x;
            if (Time.frameCount - EarthMineFrame < 240 && Mathf.Abs(dx) < 7)
            {
                land = Mathf.Clamp(.14f + Mathf.Abs(dx) * .05f, .14f, .32f);
                EarthToss(hand, ground + Vector2.up * .05f, land, size, dx >= 0 ? 1 : -1);
            }
            // A clear pivot carries the frame clock (its sprite stays, fully transparent, so it draws nothing).
            var clock = Attach(root, "Earth mine clock", core, .05f, Color.clear, 0);
            // The glowing glyph on the floor over a soft pool of its light, and the mine's upright mark hovering over it in a bloom.
            var plane = EarthPlane(root, new Vector2(0, -.58f), .32f);
            var pool = Attach(plane, "Earth mine floor glow", glow, size * 1.7f, Alpha(EarthPink, .4f), 26);
            var seal = Attach(plane, "Earth mine seal", EarthSeal, size, Alpha(EarthPink, .5f), 27);
            Vector2 markAt = new Vector2(0, -.18f);
            var markGlow = Attach(root, "Earth mine mark glow", glow, .95f, Alpha(EarthPink, .4f), 121, true, markAt);
            var mark = Attach(root, "Earth mine mark", EarthGlyph, .46f, Alpha(EarthPink, .5f), 122, true, markAt);
            var layers = new List<Clip> { pool, seal, markGlow, mark };
            if (g >= 3) { var crown = Attach(plane, "Earth mine crown", runeRing, size * 1.5f, Alpha(EarthGold, .5f), 27); crown.spin = -60; layers.Add(crown); }
            seal.spin = 35;
            float pop = EarthPop(span, .1f);
            foreach (var x in layers) { x.life = span; x.loop = false; x.fadePower = 0; x.startScale = 1.6f; x.endScale = 1; x.ease = pop; EarthLater(x, land); }
            clock.life = span; clock.loop = false;

            // The landing: a hit spark, billows of dust and a small shock front where the glyph slams down.
            var thud = EarthLater(Flash(ground + Vector2.up * .12f, 1.4f, Alpha(EarthPink, .85f), .12f), land); thud.ramp = Ramp(Color.white, EarthPink);
            EarthShock(ground + Vector2.up * .03f, .75f, Alpha(EarthPinkHot, .85f), .28f, 116, .32f, land);
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? 1 : -1;
                var dust = EarthLater(Puff(ground + new Vector2(s * .2f, .12f), Range(.45f, .6f), EarthDustTone, new Vector2(s * Range(1, 1.8f), Range(.2f, .5f)), Range(.4f, .55f), 118), land);
                dust.endScale = 1.8f;
            }

            // Armed at 0.35 s: the seal locks in and brightens; then it blinks slowly, its mark snapping bright on each beat and
            // a ring pulsing out over the floor.
            bool armed = false; int beat = -1;
            EarthEachFrame(clock, owner =>
            {
                float t = owner.age;
                if (t < .35f)
                {
                    float nervous = .5f + .5f * Mathf.Sin(t * 60);
                    seal.color = Alpha(EarthPink, .3f + .2f * nervous); mark.color = Alpha(EarthPink, .3f + .3f * nervous);
                    return;
                }
                if (!armed) { armed = true; if (root) EarthMineArm((Vector2)root.position + Vector2.down * .6f, size, R, g); }
                float phase = (t - .35f) * 1.25f, b = Mathf.Pow(.5f + .5f * Mathf.Cos(phase * 2 * Mathf.PI), 2);
                seal.color = Alpha(EarthPink, .55f + .4f * b);
                mark.color = Alpha(b > .5f ? EarthPinkHot : EarthPink, .55f + .45f * b);
                markGlow.color = Alpha(EarthPink, .25f + .35f * b); pool.color = Alpha(EarthPink, .3f + .3f * b);
                int cycle = Mathf.FloorToInt(phase);
                if (cycle != beat && root)
                {
                    beat = cycle;
                    Vector2 floorAt = (Vector2)root.position + Vector2.down * .6f;
                    EarthShock(floorAt + Vector2.up * .03f, size * .75f, Alpha(EarthPink, .75f), .5f, 27, .32f).startScale = .35f;
                    var mote = Spawn("Earth mine mote", fleck, floorAt + new Vector2(Range(-.25f, .25f), .5f), Range(.18f, .26f), Alpha(EarthPinkHot, .95f), Range(.5f, .7f), true, 126);
                    mote.velocity = new Vector2(Range(-.15f, .15f), Range(.8f, 1.4f)); mote.drag = .4f; mote.flicker = .3f; mote.endScale = .3f; mote.ease = 1;
                }
            });
            Bundle(root, clock, layers.ToArray());
        }

        // The seal arming: a ring snaps down onto it, a flash and a star, and a thin ring runs out to the blast radius once.
        private void EarthMineArm(Vector2 ground, float size, float R, int g)
        {
            var snap = Spawn("Earth mine lock", ring, ground + Vector2.up * .03f, size * 2, Alpha(EarthPinkHot, .9f), .14f, true, 27);
            EarthSquash(snap, .32f); snap.startScale = 1; snap.endScale = .45f; snap.ease = 2; snap.fadePower = .8f;
            var flash = Flash(ground + Vector2.up * .3f, 1.2f, Alpha(EarthPink, .85f), .13f); flash.ramp = Ramp(Color.white, EarthPink);
            EarthLater(Glint(ground + Vector2.up * .42f, 1.1f, Alpha(g >= 3 ? EarthGoldHot : Color.white, .9f), .2f, 0), .1f).spin = 0;
            // The blast reach runs out once (from level two the grade's own edge ring already shows it the whole time).
            if (g >= 2) return;
            EarthShock(ground + Vector2.up * .03f, R, Alpha(EarthPink, .6f), .55f, 116, .32f).startScale = .3f;
        }

        // A glyph thrown from the hand in an arc, spinning in its own bloom, shedding needle sparks; it lands after
        // <paramref name="time"/> seconds.
        private void EarthToss(Vector2 from, Vector2 to, float time, float size, int side)
        {
            const float Gravity = 22;
            Vector2 v = new Vector2((to.x - from.x) / time, (to.y - from.y) / time + .5f * Gravity * time);
            var lit = Spawn("Earth mine thrown", EarthSeal, from, size * .6f, Alpha(EarthPink, .95f), time, true, 129);
            var bloom = Spawn("Earth mine thrown glow", glow, from, size * 1.1f, Alpha(EarthPink, .45f), time, true, 128);
            foreach (var x in new[] { lit, bloom }) { x.velocity = v; x.gravity = Gravity; x.spin = -side * 720; x.fadePower = 0; x.startScale = .7f; x.endScale = 1; x.ease = 1; }
            Emitter(lit, 30, .08f, (owner, p) =>
            {
                var trace = Spawn("Earth mine thrown trace", spark, p, Range(.22f, .32f), Alpha(EarthPink, .9f), Range(.14f, .2f), true, 128);
                trace.velocity = -owner.velocity * .15f; trace.alignVelocity = true; trace.endScale = .2f; trace.ease = 1; trace.fadePower = 1.2f; trace.scale.y *= .7f;
            });
        }

        private void ImpactMine(Vector2 pos, float radius, Color tint, int facing)
        {
            EarthTextures();
            int g = Grade; float R = Mathf.Clamp(radius, .6f, 3.5f), s = Mathf.Clamp(R / 1.3f, .7f, 1.6f);
            Color pink = EarthBlend(tint, EarthPink, .3f), hot = Color.Lerp(pink, EarthPinkHot, .6f);
            // Mines laid together often burst in the same frame: the first draws the full blast, the others follow as a quick
            // chain 0.07 s apart with lighter blasts, so a row of mines reads as a rippling chain instead of one white-out.
            if (EarthBlastFrame != Time.frameCount) { EarthBlastFrame = Time.frameCount; EarthBlastCount = 0; }
            int place = EarthBlastCount++;
            if (place == 0) { EarthMineBlast(pos, R, s, pink, hot, g, true); return; }
            float fuse = place * .07f; bool fired = false;
            var clock = Spawn("Earth mine fuse", core, pos, .05f, Color.clear, fuse + .1f, true, 0);
            EarthEachFrame(clock, owner => { if (fired || owner.age < fuse) return; fired = true; EarthMineBlast(pos, R, s, pink, hot, g, false); });
        }

        /// <summary>One mine's blast: a pink-magenta explosion billowing from a white heart, a fan of painted fire petals, light
        /// wedge lines, seal shards, needles, warm grey smoke and shock fronts on the true radius. The lead of a chain draws it
        /// in full; the followers keep a smaller blast, a few petals, shards and the floor front.</summary>
        private void EarthMineBlast(Vector2 pos, float R, float s, Color pink, Color hot, int g, bool lead)
        {
            Vector2 ground = pos + Vector2.down * .3f, body = ground + Vector2.up * .6f;
            var blast = Blast(body + Vector2.up * .1f, (lead ? 2.9f : 1.9f) * s, Color.Lerp(pink, EarthMagenta, .45f), lead ? .55f : .42f, 128);
            blast.ramp = Ramp(Color.white, new Color(1, .8f, .93f), new Color(1, .6f, .86f));
            if (lead)
            {
                var shade = Underlay(body, 3.4f * s, .34f, .5f); shade.hold = .2f;
                var spark = SparkFlip(body, 2.2f * s, Alpha(hot, .95f), .2f, 132); spark.ramp = Ramp(Color.white, pink);
                ImpactLines(body, 3.8f * s, Alpha(hot, .75f), .26f);
                // The shock: a painted front on the true radius.
                Wave(ground + Vector2.up * .1f, R * 2 / .74f, Alpha(pink, .8f), .34f, 3);
            }
            // A shock front racing along the floor to the true radius, over a flash of light pooling there.
            GroundGlow(ground, R * 2.1f, Alpha(pink, lead ? .55f : .4f), .5f);
            EarthShock(ground + Vector2.up * .03f, R, Alpha(hot, lead ? .9f : .7f), .46f, 116, .32f).startScale = .35f;
            // Fire petals fan up out of the seal, uneven in length and lean, each a painted tongue with a hot spine; deep magenta
            // ones a little shorter stand among them for body.
            int petals = lead ? 6 + (g >= 2 ? 1 : 0) : 3;
            float tilt0 = Range(-12, 12);
            for (int i = 0; i < petals; i++)
            {
                float t = petals == 1 ? 0 : i / (float)(petals - 1) * 2 - 1;
                float tilt = tilt0 + t * 72 + Range(-9, 9), length = (lead ? 2.3f : 1.5f) * s * (1 - .4f * Mathf.Abs(t)) * Range(.75f, 1.1f);
                bool deep = lead && i % 3 == 2;
                EarthPetalClip(ground + new Vector2(t * .2f * s, .05f), length * (deep ? .85f : 1), tilt, Alpha(deep ? EarthMagenta : i % 3 == 1 ? hot : pink, .95f), Range(.4f, .5f), true, deep ? 125 : 126, Mathf.Abs(t) * .03f);
            }
            // The seal breaks: broken arcs of its ring fly, hot and deep magenta.
            int pieces = lead ? 5 + (g >= 3 ? 2 : 0) : 3;
            for (int i = 0; i < pieces; i++)
            {
                bool lit = i % 2 == 0;
                var piece = Spawn("Earth seal shard", EarthShardSet[random.Next(EarthShardSet.Length)], ground + new Vector2(Range(-.3f, .3f), .15f), Range(.3f, .44f) * s, lit ? Alpha(hot, .95f) : Alpha(EarthMagenta, .9f), Range(.55f, .8f), true, 129);
                piece.velocity = Direction(Range(20, 160)) * Range(4, 7.5f) * s; piece.gravity = 10; piece.drag = .8f; piece.spin = Range(-500, 500); piece.angle = Range(0, 360); piece.hold = .35f; piece.fadePower = 1.2f;
            }
            EarthNeedles(ground + Vector2.up * .3f, lead ? 4 + g : 2, 90 + Range(-15, 15), 150, 7.5f * s, pink, .4f, .42f * s);
            // A scorch of deep plum-brown (not black) where the glyph burned.
            Decal(ground, scorch, 1.6f * s, new Color(.3f, .13f, .17f, .5f), 1.4f, false, 24);
            if (!lead) return;
            // Warm grey smoke billows out of the blast and a plume of it climbs.
            InkBurst(body + Vector2.up * .2f, 1.2f * s, EarthSmoke, 4, 1, 117);
            var fume = Puff(body + new Vector2(Range(-.3f, .3f), Range(.5f, .8f)), Range(1.2f, 1.5f) * s, EarthSmoke, new Vector2(Range(-.4f, .4f), Range(1, 1.6f)), Range(1.05f, 1.25f), 115);
            fume.drag = 1.4f; fume.endScale = 2.1f; fume.fadeIn = .08f;
            var burn = Decal(ground, crack, 1.5f * s, Alpha(pink, .85f), .9f, true, 26); burn.angle = Range(-10, 10); burn.fadePower = 1.6f;
            if (g >= 3) Glint(body, 2.2f * s, Alpha(EarthGoldHot, .95f), .28f, 0).spin = 0;
        }

        /// <summary>A fire petal standing on <paramref name="root"/>, <paramref name="length"/> long and leaning <paramref name="tilt"/>
        /// degrees from upright: it shoots out in a drawn frame or two, holds and frays.</summary>
        private Clip EarthPetalClip(Vector2 root, float length, float tilt, Color color, float life, bool additive, int order, float delay)
        {
            var petal = Spawn("Earth mine petal", EarthPetal, root, length * .46f, color, life, additive, order);
            petal.scale.y = length / EarthPetal.bounds.size.y; petal.root.transform.localScale = petal.scale;
            petal.angle = -tilt; petal.axisStart = new Vector2(.7f, .35f); petal.axisEnd = Vector2.one; petal.ease = 7; petal.hold = .4f; petal.fadePower = 1.2f;
            return EarthLater(petal, delay);
        }

        // ---------------------------------------------------------------- shared helpers

        private static Clip EarthLater(Clip clip, float delay)
        {
            if (delay > 0) { clip.age = -delay; if (clip.renderer) clip.renderer.enabled = false; }
            return clip;
        }
        private static Clip EarthSquash(Clip clip, float y) { clip.scale.y *= y; clip.root.transform.localScale = clip.scale; return clip; }
        /// <summary>The ease exponent that completes a start-to-end change in about <paramref name="seconds"/> of a long life.</summary>
        private static float EarthPop(float life, float seconds) { return Mathf.Log(.08f) / Mathf.Log(1 - Mathf.Clamp(seconds / Mathf.Max(.01f, life), .001f, .9f)); }
        private static float EarthGradeScale(int grade) { return grade >= 3 ? 1.4f : grade == 2 ? 1.2f : 1; }
        /// <summary>The family colour nudged toward the gameplay tint when that tint is a real colour.</summary>
        private static Color EarthBlend(Color tint, Color family, float amount)
        {
            float h, s, v; Color.RGBToHSV(tint, out h, out s, out v);
            if (s < .15f || v < .2f) return family;
            var mixed = Color.Lerp(family, new Color(tint.r, tint.g, tint.b, 1), amount); mixed.a = 1; return mixed;
        }

        private Transform EarthPlane(Transform root, Vector2 offset, float squash)
        {
            var plane = new GameObject("Earth plane").transform; plane.SetParent(root, false);
            plane.localPosition = offset; plane.localScale = new Vector3(1, squash, 1); return plane;
        }

        /// <summary>Calls <paramref name="act"/> once a frame while <paramref name="owner"/> lives: an emitter that ignores its count,
        /// backed by the player loop for the frames when the effects are over their hard cap and emitters stop.</summary>
        private void EarthEachFrame(Clip owner, Action<Clip> act)
        {
            float last = -1;
            Action<Clip> once = o => { if (o.age == last) return; last = o.age; act(o); };
            Emitter(owner, 900, 0, (o, p) => once(o));
            EarthDrivers.Add(new EarthDriver { host = this, clip = owner, run = once });
        }

        // Runs the frame jobs whose emitters were skipped this frame (the effects are over their hard cap); drops finished ones.
        private static void EarthDrive()
        {
            for (int i = EarthDrivers.Count - 1; i >= 0; i--)
            {
                var job = EarthDrivers[i];
                if (!job.host || !job.clip.root || !job.clip.root.activeInHierarchy) { EarthDrivers.RemoveAt(i); continue; }
                if (job.host.clips.Count <= HardCap || job.clip.age < 0) continue;
                if (!job.host.clips.Contains(job.clip)) { EarthDrivers.RemoveAt(i); continue; }
                try { job.run(job.clip); }
                catch (Exception error) { EarthDrivers.RemoveAt(i); Debug.LogException(error); }
            }
        }

        /// <summary>A painted crescent (bulging toward <paramref name="angle"/>) with a white leading edge just ahead of it, snapping
        /// open, holding and fraying; <paramref name="squash"/> below 1 lays it along the floor as a shock front, and both strokes
        /// travel at <paramref name="velocity"/>, starting after <paramref name="delay"/> seconds. Returns the coloured stroke.</summary>
        private Clip EarthArc(Vector2 pos, float width, float angle, Color color, float life, int order, float squash, Vector2 velocity = default(Vector2), float delay = 0)
        {
            var arc = Crescent(pos, width, color, life, angle, order); arc.startScale = .8f; arc.endScale = 1.08f; arc.ease = 6; arc.hold = .3f; arc.fadePower = 1.1f;
            var edge = Crescent(pos + Direction(angle) * width * .025f, width * .97f, new Color(1, 1, 1, Mathf.Min(1, color.a)), life * .75f, angle, order + 1);
            edge.scale.y *= .94f; edge.startScale = .82f; edge.endScale = 1.07f; edge.ease = 6; edge.hold = .2f; edge.fadePower = 1.4f;
            foreach (var x in new[] { arc, edge })
            {
                if (squash != 1) EarthSquash(x, squash);
                if (velocity != Vector2.zero) { x.velocity = velocity; x.drag = 6; }
                EarthLater(x, delay);
            }
            return arc;
        }

        /// <summary>Needle sparks fanned across <paramref name="spread"/> degrees round <paramref name="angle"/> (white and
        /// <paramref name="color"/>), falling a little, after <paramref name="delay"/> seconds.</summary>
        private void EarthNeedles(Vector2 pos, int count, float angle, float spread, float speed, Color color, float life, float size, float delay = 0)
        {
            count = Budget(count);
            for (int i = 0; i < count; i++)
            {
                float a = angle + (count == 1 ? 0 : (i / (float)(count - 1) - .5f) * spread) + Range(-12, 12);
                Vector2 d = Direction(a);
                var needle = Spark(pos + d * .1f, d * speed * Range(.55f, 1.1f), i % 3 == 0 ? Color.white : color, size * Range(.7f, 1.25f), life * Range(.6f, 1), 4, 3);
                EarthLater(needle, delay);
            }
        }

        /// <summary>A shock front (the hand-animated ring that races out, thins and breaks into arcs) whose leading edge ends at
        /// <paramref name="radius"/>; below 1, <paramref name="squash"/> lays it in that plane (it turns inside the plane, so the
        /// breaks fall differently each time but it never tilts). Starts after <paramref name="delay"/> seconds.</summary>
        private Clip EarthShock(Vector2 pos, float radius, Color color, float life, int order, float squash = 1, float delay = 0)
        {
            // The flipbook's front reaches .88 of the sprite's half width on its last frame.
            var clip = ShockFlip(pos, radius * 2 / .88f, color, life, order);
            clip.startScale = .5f; clip.endScale = 1; clip.ease = 2.5f; clip.hold = .5f; clip.fadePower = .9f;
            if (squash != 1) LayFlat(clip, squash);
            return EarthLater(clip, delay);
        }

        // ---------------------------------------------------------------- textures

        // The family's textures take a few hundred milliseconds to shade. Once the effects exist (the core's textures are built)
        // a player-loop step bakes them a couple of milliseconds per frame, so no first cast hitches; a skill used before they
        // are done finishes the rest at once.
        private delegate void EarthPairShade(float u, float v, out Color first, out Color second);

        private static bool EarthBakeFailed;

        /// <summary>Adds the family's step to the end of the player loop's Update phase once: it bakes the textures, then keeps
        /// the frame jobs running while emitters are stopped.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EarthHookBakes()
        {
            var loop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
            var phases = loop.subSystemList; if (phases == null) return;
            for (int i = 0; i < phases.Length; i++)
            {
                if (phases[i].type != typeof(UnityEngine.PlayerLoop.Update)) continue;
                var list = new List<UnityEngine.LowLevel.PlayerLoopSystem>(phases[i].subSystemList ?? new UnityEngine.LowLevel.PlayerLoopSystem[0]);
                if (list.Exists(s => s.type == typeof(EarthBakeLoop))) return;
                list.Add(new UnityEngine.LowLevel.PlayerLoopSystem { type = typeof(EarthBakeLoop), updateDelegate = EarthBakeStep });
                phases[i].subSystemList = list.ToArray(); loop.subSystemList = phases;
                UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(loop);
                return;
            }
        }

        // Waits until the core textures exist (the effects are in use), then bakes about 3 ms a frame; runs the frame jobs.
        private static void EarthBakeStep()
        {
            if (EarthDrivers.Count > 0) EarthDrive();
            if (EarthBaked || EarthBakeFailed || !built) return;
            try { EarthBake(3); }
            catch (Exception error) { EarthBakeFailed = true; Debug.LogException(error); }
        }

        /// <summary>Every texture is ready before any of the family's effects draws.</summary>
        private static void EarthTextures() { if (!EarthBaked) EarthBake(double.MaxValue); }

        /// <summary>Shades queued textures row by row for up to <paramref name="budgetMs"/>; true once all are done.</summary>
        private static bool EarthBake(double budgetMs)
        {
            if (EarthBaked) return true;
            EarthQueue();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (EarthBakes.Count > 0)
            {
                var job = EarthBakes[0];
                if (job.first == null) { job.first = new Color32[job.width * job.height]; if (job.pair) job.second = new Color32[job.width * job.height]; }
                while (job.row < job.height)
                {
                    if (watch.Elapsed.TotalMilliseconds > budgetMs) return false;
                    float v = (job.row + .5f) / job.height * 2 - 1; int at = job.row * job.width;
                    for (int x = 0; x < job.width; x++)
                    {
                        Color c1, c2; job.shade((x + .5f) / job.width * 2 - 1, v, out c1, out c2);
                        job.first[at + x] = EarthClamp(c1); if (job.pair) job.second[at + x] = EarthClamp(c2);
                    }
                    job.row++;
                }
                job.store(EarthSprite(job.name, job.width, job.height, job.first, job.pivot), job.pair ? EarthSprite(job.name + " light", job.width, job.height, job.second, job.pivot) : null);
                EarthBakes.RemoveAt(0);
            }
            EarthBaked = true;
            return true;
        }

        private static void EarthJob(string name, int width, int height, Vector2 pivot, bool pair, EarthPairShade shade, Action<Sprite, Sprite> store)
        {
            EarthBakes.Add(new EarthBakeJob { name = name, width = width, height = height, pivot = pivot, pair = pair, shade = shade, store = store });
        }

        private static void EarthQueue()
        {
            if (EarthQueued) return; EarthQueued = true;
            EarthQueueQuake(); EarthQueueOrbit(); EarthQueueDecoy(); EarthQueueMine();
        }

        private static Color32 EarthClamp(Color c) { return new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), Mathf.Clamp01(c.a)); }
        /// <summary>A painted stroke's light level at distance <paramref name="d"/> from its spine: a crisp edge at
        /// <paramref name="half"/>, a thin deep rim just inside it, then rising smoothly to <paramref name="peak"/> on the spine.</summary>
        private static float EarthStroke(float d, float half, float peak = 1)
        {
            d = Mathf.Abs(d); if (d >= half + .004f) return 0;
            float s = Mathf.Clamp01(1 - d / half);
            return Mathf.Lerp(.14f, peak, Smooth(0, 1, Mathf.Pow(s, .7f))) * (1 - Smooth(half - .006f, half + .004f, d));
        }

        private static Sprite EarthSprite(string name, int width, int height, Color32[] pixels, Vector2 pivot)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "Tower effect " + name, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 2 };
            texture.SetPixels32(pixels); texture.Apply(true, false);
            return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private static void EarthQueueQuake()
        {
            // Rock fangs (bottom pivot): painted stone spikes lit softly from the upper left, with a light copy of their glowing
            // cracks and molten root.
            EarthFangRock = new Sprite[3]; EarthFangVein = new Sprite[3];
            for (int i = 0; i < 3; i++)
            {
                int variant = i;
                EarthJob("Earth fang " + i, 96, 256, new Vector2(.5f, 0), true, (float u, float v, out Color stone, out Color vein) =>
                {
                    float heat; stone = EarthFangSample(u, v, variant, out heat);
                    vein = White(heat);
                }, (rock0, light0) => { EarthFangRock[variant] = rock0; EarthFangVein[variant] = light0; });
            }
            // The rift strip: a crack entering and leaving at mid-height of both ends, with side cracks: a scorched gash, and as
            // light a molten seam swelling smoothly to a white-hot spine, its heat flickering along it.
            EarthRiftInk = new Sprite[4]; EarthRiftLight = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                int variant = i;
                EarthJob("Earth rift " + i, 128, 64, new Vector2(.5f, .5f), true, (float u, float v, out Color ink, out Color lit) =>
                {
                    float heat, lip; float a = EarthRiftShape(u * 2, v, variant, out heat, out lip);
                    float fade = 1 - Smooth(.86f, 1, Mathf.Abs(v));
                    ink = Over(new Color(.42f, .24f, .13f, lip * .5f), new Color(.17f, .075f, .035f, a * .9f)); ink.a *= fade;
                    lit = White(heat * fade);
                }, (ink0, light0) => { EarthRiftInk[variant] = ink0; EarthRiftLight[variant] = light0; });
            }
            // The eruption pillar (bottom pivot): a broken basalt column painted like the fangs, and its glowing seams.
            EarthJob("Earth column", 96, 288, new Vector2(.5f, 0), true, (float u, float v, out Color stone, out Color vein) =>
            {
                float heat; stone = EarthColumnSample(u, (v + 1) * 3, out heat);
                vein = White(heat);
            }, (rock0, light0) => { EarthColumnRock = rock0; EarthColumnVein = light0; });
            // A slab torn from the floor (centre pivot): a pale lit top, shaded flanks and molten light along its broken underside.
            EarthSingle("Earth slab", 96, 48, new Vector2(.5f, .5f), (u, v) => EarthSlabSample(u, v * .5f), sprite => EarthSlab = sprite);
            // Chips of rock for every flung piece (centre pivot): painted lumps, the last with glowing veins.
            EarthChipSet = new Sprite[3];
            for (int i = 0; i < 3; i++)
            {
                int variant = i;
                EarthSingle("Earth chip " + i, 64, 64, new Vector2(.5f, .5f), (u, v) =>
                {
                    float unused; return EarthStoneSample(u, v, 37 + variant * 23, variant == 2 ? 1 : 0, out unused);
                }, sprite => EarthChipSet[variant] = sprite);
            }
        }

        /// <summary>A light value (0 shadow, 1 lit, above 1 a sunlit highlight) as painted stone.</summary>
        private static Color EarthStoneTone(float light)
        {
            var c = Color.Lerp(EarthStoneDeep, EarthStoneMid, Smooth(0, .55f, light));
            c = Color.Lerp(c, EarthStoneLit, Smooth(.42f, 1, light));
            return Color.Lerp(c, new Color(.97f, .9f, .76f), Smooth(1, 1.3f, light));
        }

        /// <summary>Paints standing rock at <paramref name="x"/> across from its axis and <paramref name="h"/> (0 root, 1 top): a
        /// rounded mass lit softly from the upper left over broad soft planes, grain and pits, slanted strata creases each with a
        /// lit ledge, a lit rim along its left edge, warm lava light bouncing up its foot and right edge, and cracks that glow as
        /// they climb from a molten root (<paramref name="rootSpan"/> of its height) to <paramref name="reach"/>. <paramref name="left"/>, <paramref name="right"/> and
        /// <paramref name="top"/> are distances inside the silhouette in pixels; <paramref name="unit"/> and <paramref name="tall"/>
        /// are pixels per x and per h. <paramref name="heat"/> is the glow's light level.</summary>
        private static Color EarthPaintSpire(float x, float h, float halfL, float halfR, float left, float right, float top, float unit, float tall, float seed, float reach, float rootSpan, out float heat)
        {
            float half = Mathf.Max(.03f, (halfL + halfR) * .5f), mid = (halfR - halfL) * .5f;
            float nx = Mathf.Clamp((x - mid) / half + .45f * (Fbm(x * 1.3f + seed, h * 2.6f + seed * .7f, 2) - .5f), -1, 1);
            float nz = Mathf.Sqrt(Mathf.Max(0, 1 - nx * nx));
            float diffuse = Mathf.Clamp01(-.62f * nx + .16f + .64f * nz);
            float grain = Fbm(x * 2.6f + seed * 2, h * 17 + seed, 3), pits = Fbm(x * 7 + seed, h * 30 - seed, 2);
            float light = (.06f + .94f * Mathf.Pow(diffuse, 1.25f)) * (.8f + .42f * (grain - .5f));
            light *= 1 - .16f * Smooth(.62f, .7f, pits);
            float phase = h * (4.2f + Mathf.Repeat(seed, 1.7f)) - x * .55f + seed + .3f * Mathf.PerlinNoise(x * 2.2f + seed, h * 3.1f);
            float s = Mathf.Repeat(phase, 1), broken = Smooth(.32f, .62f, Mathf.PerlinNoise(h * 4.4f + seed, x * 3 + 2));
            light *= 1 - .4f * Gauss(s - .5f, .028f) * broken;
            light += .16f * Gauss(s - .54f, .026f) * broken * diffuse;
            light += .34f * Gauss(left, 3.2f) + .16f * Gauss(top, 5) * diffuse;
            light *= .5f + .5f * Smooth(0, .22f, h);
            var c = EarthStoneTone(light);
            float bounce = Gauss(right, 3.5f) * (1 - Smooth(.05f, .55f, h)) * .55f + (1 - Smooth(0, .2f, h)) * .3f;
            c = Color.Lerp(c, new Color(.95f, .45f, .14f), Mathf.Clamp01(bounce));
            // The main crack wanders up from the root and thins out; two side cracks branch off it and taper away.
            float path = mid + (Mathf.PerlinNoise(h * 3.6f + seed, 2.5f) - .5f) * half * 1.3f;
            float wpx = 3.4f * Mathf.Max(0, 1 - h / reach) * (.7f + .6f * Mathf.PerlinNoise(h * 9 + seed, 7.7f));
            float d = Mathf.Abs(x - path) * unit, crack = wpx > .25f ? Smooth(wpx, 0, d) : 0;
            float throat = wpx > .25f ? Smooth(wpx * 2.2f + 1, wpx, d) : 0;
            for (int b = 0; b < 2; b++)
            {
                float h0 = (.16f + .22f * b) * reach, h1 = h0 + .16f * reach;
                float x0 = mid + (Mathf.PerlinNoise(h0 * 3.6f + seed, 2.5f) - .5f) * half * 1.3f, x1 = x0 + (b == 0 ? -1 : 1) * half * .8f;
                float sd = SegmentDistance(x * unit, h * tall, x0 * unit, h0 * tall, x1 * unit, h1 * tall);
                float along = Mathf.Clamp01((h - h0) / (h1 - h0)), bw = 2.4f * (1 - along) + .3f;
                crack = Mathf.Max(crack, Smooth(bw, 0, sd) * (1 - along * .5f));
                throat = Mathf.Max(throat, Smooth(bw * 2 + 1, bw, sd) * (1 - along));
            }
            float root = Smooth(rootSpan, rootSpan * .13f, h + rootSpan * .66f * (Mathf.PerlinNoise(x * 6 + seed, 3) - .5f));
            c = Color.Lerp(c, EarthCrackThroat, throat * .7f);
            c = Color.Lerp(c, Color.Lerp(EarthMoltenTone, new Color(1, .92f, .66f), crack), Smooth(.15f, .7f, crack));
            c = Color.Lerp(c, EarthMoltenTone, root * .9f);
            heat = Mathf.Max(crack * .95f, root);
            return c;
        }

        /// <summary>A painted fang of rock standing on its base (v = -1), leaning a little and breaking to an off-centre point,
        /// its edges lumpy as a brush leaves them. The texture is 96 x 256, so a u unit is 48 pixels and an h unit 256.</summary>
        private static Color EarthFangSample(float u, float v, int variant, out float heat)
        {
            heat = 0;
            float h = (v + 1) * .5f, seed = variant * 3.7f + 1.3f, tipH = .965f - .045f * variant;
            float axis = (variant - 1) * .24f * h * h + (Mathf.PerlinNoise(h * 2.1f + seed, 4.1f) - .5f) * .14f * h;
            float taper = Mathf.Pow(Mathf.Max(0, 1 - h / tipH), .82f);
            float notch = .24f * Gauss(h - (.4f + .13f * variant), .045f);
            float halfL = .8f * taper * (1 + .34f * (Fbm(h * 5.5f + seed, 1.7f, 3) - .5f)) * (1 - (variant != 1 ? notch : 0));
            float halfR = .76f * taper * (1 + .34f * (Fbm(h * 5.5f + seed, 8.3f, 3) - .5f)) * (1 - (variant == 1 ? notch : 0));
            float x = u - axis, left = (x + halfL) * 48, right = (halfR - x) * 48, top = (tipH - h) * 256;
            float cover = Smooth(-1, 1, Mathf.Min(Mathf.Min(left, right), top + 2)) * Smooth(0, .012f, h);
            if (cover <= 0) return Color.clear;
            var c = EarthPaintSpire(x, h, halfL, halfR, left, right, top, 48, 256, seed, .66f, .075f, out heat);
            c.a = cover; heat *= cover; return c;
        }

        /// <summary>The eruption column at (X, H): X in -1..1, H in 0..6 (48 pixels a unit both ways), its top broken unevenly.</summary>
        private static Color EarthColumnSample(float X, float H, out float heat)
        {
            heat = 0;
            float halfL = .72f - .035f * H + (Fbm(H * 1.1f + 2, 1.3f, 3) - .5f) * .24f, halfR = .7f - .035f * H + (Fbm(H * 1.1f + 2, 6.1f, 3) - .5f) * .24f;
            float crest = 5.15f + (Fbm(X * 1.8f + 4, 2.2f, 3) - .5f) * 1.3f;
            float left = (X + halfL) * 48, right = (halfR - X) * 48, top = (crest - H) * 48;
            float cover = Smooth(-1, 1, Mathf.Min(Mathf.Min(left, right), top)) * Smooth(0, .05f, H);
            if (cover <= 0) return Color.clear;
            var c = EarthPaintSpire(X, H / 6, halfL, halfR, left, right, top, 48, 288, 5.3f, .75f, .028f, out heat);
            c.a = cover; heat *= cover; return c;
        }

        /// <summary>A slab of floor torn loose at (X, Y), 48 pixels a unit: a pale lit top face, a lit left and a shaded right
        /// flank, grain, a bright lip and molten light along its broken underside.</summary>
        private static Color EarthSlabSample(float X, float Y)
        {
            float top = .26f + (Fbm(X * 1.6f + 1, .7f, 2) - .5f) * .1f, bottom = -.36f + (Fbm(X * 2.6f + 4, 3.1f, 3) - .5f) * .26f;
            float half = .84f + (Fbm(Y * 3 + 2, 5.5f, 2) - .5f) * .16f;
            float below = (Y - bottom) * 48;
            float cover = Smooth(-1, 1, Mathf.Min(Mathf.Min((half - Mathf.Abs(X)) * 48, (top - Y) * 48), below)); if (cover <= 0) return Color.clear;
            float grain = Fbm(X * 5 + 3, Y * 9 + 1, 3);
            float face = Smooth(.13f, .05f, top - Y), flank = Mathf.Clamp01(.62f - .32f * X / half) * (.6f + .4f * Smooth(bottom, top, Y));
            float light = Mathf.Lerp(flank, 1.02f, face) * (.82f + .4f * (grain - .5f)) + .22f * Gauss(top - Y, .025f);
            var c = Color.Lerp(EarthStoneTone(light), EarthMoltenTone, Smooth(7, 1, below) * .85f);
            c.a = cover; return c;
        }

        /// <summary>A painted lump of rock (64 pixels): a broken rounded silhouette, soft upper-left light over broad planes,
        /// grain and pits, a lit rim and, when <paramref name="molten"/>, glowing veins. <paramref name="heat"/> is how hot.</summary>
        private static Color EarthStoneSample(float u, float v, int seed, float molten, out float heat)
        {
            heat = 0;
            float r = Mathf.Sqrt(u * u + v * v), th = Mathf.Atan2(v, u);
            float rr = .72f + .34f * (RingNoise(th, seed * .29f, 1.2f, 0) - .5f) + .08f * (RingNoise(th, seed, 3.8f, 0) - .5f);
            float cover = 1 - Smooth(rr - .04f, rr + .01f, r); if (cover <= 0) return Color.clear;
            float x = u / rr, y = v / rr, z = Mathf.Sqrt(Mathf.Max(0, 1 - x * x - y * y));
            float px = x + .4f * (Fbm(u * 1.4f + seed, v * 1.4f, 2) - .5f), py = y + .4f * (Fbm(u * 1.4f, v * 1.4f + seed, 2) - .5f);
            float diffuse = Mathf.Clamp01(-px * .6f + py * .6f + z * .55f);
            float grain = Fbm(u * 3.2f + seed, v * 6 - seed, 3), pits = Fbm(u * 9 + seed * 2, v * 9 + seed, 2);
            float light = (.08f + .92f * Mathf.Pow(diffuse, 1.2f)) * (.82f + .4f * (grain - .5f));
            light *= 1 - .14f * Smooth(.62f, .7f, pits);
            light += .3f * Gauss((rr - r) * 32, 2.6f) * Smooth(-.1f, .6f, -x * .6f + y * .8f);
            light *= 1 - .3f * Smooth(rr - .22f, rr, r) * (1 - diffuse);
            var c = EarthStoneTone(light);
            if (molten > 0)
            {
                float vein = Gauss(Fbm(u * 2.4f + seed * 3, v * 2.4f + 11, 3) - .5f, .035f) * Smooth(.1f, .5f, r / rr);
                heat = Mathf.Clamp01(vein * molten * (.6f + .5f * (1 - diffuse)));
                c = Color.Lerp(c, EarthCrackThroat, Smooth(0, .3f, heat) * .6f);
                c = Color.Lerp(c, Color.Lerp(EarthMoltenTone, new Color(1, .9f, .6f), heat), Smooth(.25f, .8f, heat));
            }
            c.a = cover; return c;
        }

        // Where each rift variant's three side cracks start and end: (bx, by, ex, ey) per crack.
        private static readonly float[][] EarthRiftBranches = { EarthRiftBranchEnds(0), EarthRiftBranchEnds(1), EarthRiftBranchEnds(2), EarthRiftBranchEnds(3) };

        private static float[] EarthRiftBranchEnds(int variant)
        {
            var ends = new float[12];
            for (int b = 0; b < 3; b++)
            {
                float bx = -1.35f + b * 1.3f + variant * .12f, by = EarthRiftPath(bx, variant), up = (b + variant) % 2 == 0 ? 1 : -1;
                ends[b * 4] = bx; ends[b * 4 + 1] = by; ends[b * 4 + 2] = bx + (b % 2 == 0 ? .5f : -.4f); ends[b * 4 + 3] = by + up * (.4f + .06f * b);
            }
            return ends;
        }

        // The crack's line: low and irregular (two slow triangle waves and noise), pinned to mid-height at both ends.
        private static float EarthRiftPath(float X, int variant)
        {
            float t = (X + 2) * .25f, env = Mathf.Sin(t * Mathf.PI);
            float a = Mathf.Abs(Mathf.Repeat(t * (1.1f + variant * .4f) + .17f + variant * .29f, 1) - .5f) * 2 - .5f;
            float b = Mathf.Abs(Mathf.Repeat(t * (3 + variant) + .41f + variant * .13f, 1) - .5f) * 2 - .5f;
            float n = Mathf.PerlinNoise(X * 1.7f + variant * 5, .5f) - .5f;
            return (a * .3f + b * .15f + n * .35f) * env;
        }

        // The rift at (X, Y) with X in -2..2 and Y in -1..1 (square pixels): the main crack and three side cracks.
        // Heat is the light level: a seam swelling smoothly to its spine, flickering along it, inside a faint fringe of glow,
        // narrower than the gash so a scorched edge shows round it; lip is the scorched band round the gash.
        private static float EarthRiftShape(float X, float Y, int variant, out float heat, out float lip)
        {
            float t = (X + 2) * .25f, env = Mathf.Sin(t * Mathf.PI);
            float width = .15f + .16f * Mathf.Sqrt(Mathf.Max(0, env)) * (.7f + .6f * Mathf.PerlinNoise(t * 6 + variant * 4, 2.5f));
            float d = Mathf.Abs(Y - EarthRiftPath(X, variant)), flicker = .8f + .4f * Mathf.PerlinNoise(X * 3.3f + variant * 2, 1.7f);
            float cover = 1 - Smooth(width * .85f, width, d);
            heat = Mathf.Max(Mathf.Pow(Mathf.Clamp01(1 - d / (width * .72f)), 1.25f) * flicker, (1 - Smooth(width * .5f, width * .9f, d)) * .12f);
            lip = 1 - Smooth(width * 1.5f, width * 2.1f, d);
            var ends = EarthRiftBranches[variant];
            for (int b = 0; b < 3; b++)
            {
                float bx = ends[b * 4], by = ends[b * 4 + 1], ex = ends[b * 4 + 2], ey = ends[b * 4 + 3];
                float dx = ex - bx, dy = ey - by;
                float along = Mathf.Clamp01(((X - bx) * dx + (Y - by) * dy) / (dx * dx + dy * dy));
                float sd = SegmentDistance(X, Y, bx, by, ex, ey), bw = Mathf.Lerp(width * .6f, .03f, along);
                cover = Mathf.Max(cover, 1 - Smooth(bw * .8f, bw, sd));
                heat = Mathf.Max(heat, Mathf.Pow(Mathf.Clamp01(1 - sd / (bw * .7f)), 1.25f) * (1 - along * .6f) * flicker);
                lip = Mathf.Max(lip, (1 - Smooth(bw * 1.4f, bw * 2, sd)) * (1 - Smooth(.7f, 1, along)));
            }
            return cover;
        }

        /// <summary>One texture shaded by <paramref name="shade"/>, queued for baking.</summary>
        private static void EarthSingle(string name, int width, int height, Vector2 pivot, Shade shade, Action<Sprite> store)
        {
            EarthJob(name, width, height, pivot, false, (float u, float v, out Color first, out Color second) => { first = shade(u, v); second = Color.clear; }, (sprite, unused) => store(sprite));
        }

        // How far round the orbit (degrees) an orb's trail reaches behind it.
        private const float EarthTrailSpan = 110;

        private static void EarthQueueOrbit()
        {
            // The orb's trail: a painted light ribbon riding the orbit circle itself (radius .86 of the sprite), thickest just
            // behind the orb's head at angle 0 with a hot spine, tapering clockwise and breaking into streaks toward its tail;
            // and a wider soft glow of it, kept smooth light (the soft set) so the ribbon glows instead of sitting flat.
            EarthJob("Earth orbit trail", 256, 256, new Vector2(.5f, .5f), true, (float u, float v, out Color lit, out Color haze) =>
            {
                lit = haze = Color.clear;
                float r = Mathf.Sqrt(u * u + v * v); if (r < .45f || r > 1) return;
                float theta = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
                float t = -theta / EarthTrailSpan; if (t < -.07f || t > 1) return;
                float k = Mathf.Clamp01(t);
                float half = .12f * Mathf.Pow(1 - k, .7f) * Smooth(-.06f, .05f, t) + .003f, center = .86f - .025f * k;
                float across = (r - center) / half;
                float streaks = RingNoise(theta * Mathf.Deg2Rad, r, 3, 46);
                float body = Mathf.Pow(Mathf.Clamp01(1 - Mathf.Abs(across)), .8f) * (.84f + .5f * (streaks - .5f)) * (.9f + .2f * Mathf.Clamp(across, -1, 1));
                lit = White(body * (1.08f - .75f * k) - Smooth(.45f, 1, k) * (.2f + .55f * streaks));
                float wide = Gauss(r - center, half * 1.6f + .035f) * Mathf.Pow(1 - k, 1.3f) * Smooth(-.07f, .06f, t);
                haze = White(wide * .9f * Smooth(.45f, .62f, r) * (1 - Smooth(.92f, 1, r)));
            }, (light0, glow0) => { EarthTrailLight = light0; EarthTrailGlow = glow0; soft.Add(glow0); });
            // The awakened orb's gold crown: a painted ring round the ball swelling to a hot spine with brush streaks, and four
            // short flares standing off it on the diagonals.
            EarthSingle("Earth orb crown", 128, 128, new Vector2(.5f, .5f), (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), th = Mathf.Atan2(v, u);
                float level = EarthStroke(r - .8f, .055f) * (.85f + .3f * (RingNoise(th, r, 2, 20) - .5f));
                float along = (r - .8f) / .17f;
                if (along > 0 && along < 1)
                    for (int i = 0; i < 4; i++)
                    {
                        float d = Mathf.Abs(Mathf.Repeat(th - (i * 90 + 45) * Mathf.Deg2Rad + Mathf.PI, 2 * Mathf.PI) - Mathf.PI) * r;
                        level = Mathf.Max(level, EarthStroke(d, .04f * (1 - along) + .004f, .9f));
                    }
                return White(level * (1 - Smooth(.97f, 1, r)));
            }, sprite => EarthOrbCrown = sprite);
        }

        private static void EarthQueueDecoy()
        {
            // A cloaked spectral warrior (bottom pivot, facing +x) for a phantom with no art to copy: bright rim, soft body.
            EarthSingle("Earth wraith", 128, 256, new Vector2(.5f, 0), (u, v) =>
            {
                float rim; float a = EarthWraithShape(u * .5f, (v + 1) * .5f, out rim);
                return White(a * (.3f + .7f * rim) * Smooth(0, .14f, (v + 1) * .5f));
            }, sprite => EarthWraith = sprite);
        }

        private static float EarthWraithShape(float X, float Y, out float rim)
        {
            float hem = .03f + .07f * Mathf.PerlinNoise(X * 14 + 2, 1.3f), hw;
            if (Y > .74f) hw = Mathf.Lerp(.15f, .06f, Smooth(.74f, .8f, Y));
            else if (Y > .45f) hw = Mathf.Lerp(.11f, .15f, (Y - .45f) / .29f);
            else hw = Mathf.Lerp(.23f, .11f, Y / .45f);
            float cx = -.02f + (.45f - Y) * .06f, bodyEdge = hw - Mathf.Abs(X - cx);
            float body = (1 - Smooth(-.012f, 0, -bodyEdge)) * Smooth(hem, hem + .03f, Y) * (1 - Smooth(.79f, .81f, Y));
            float hd = Mathf.Sqrt(Sq(X - .015f) + Sq((Y - .865f) * .9f));
            float head = 1 - Smooth(.068f, .078f, hd);
            float arm = 1 - Smooth(.026f, .034f, SegmentDistance(X, Y, .06f, .7f, .2f, .56f));
            float blade = 1 - Smooth(.009f, .015f, SegmentDistance(X, Y, .19f, .56f, .42f, .95f));
            float guard = 1 - Smooth(.01f, .016f, SegmentDistance(X, Y, .15f, .6f, .24f, .52f));
            float a = Mathf.Max(Mathf.Max(body, head), Mathf.Max(Mathf.Max(arm, blade), guard));
            rim = Mathf.Max(body * (1 - Smooth(0, .035f, bodyEdge)), head * Smooth(.04f, .075f, hd));
            rim = Mathf.Max(rim, Mathf.Max(arm, Mathf.Max(blade, guard)) * .8f);
            return a;
        }

        private static void EarthQueueMine()
        {
            // The glyph: painted strokes swelling smoothly to hot spines with brush streaks along them (a heavy outer ring, a thin
            // inner ring, an eight-point star between them, rune bars in the band and a diamond eye at its heart) over a faint
            // fill of light inside the inner ring, so it glows from within.
            EarthSingle("Earth seal", 192, 192, new Vector2(.5f, .5f), (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float th = Mathf.Atan2(v, u), deg = Mathf.Repeat(th * Mathf.Rad2Deg, 360), brush = .82f + .36f * (RingNoise(th, r, 2.4f, 30) - .5f);
                float a = EarthStroke(r - .855f, .07f) * brush;
                a = Mathf.Max(a, EarthStroke(r - .66f, .028f, .8f));
                float star = 1e9f;
                for (int i = 0; i < 8; i++)
                {
                    float a0 = i * 45 * Mathf.Deg2Rad, a1 = (i + 3) * 45 * Mathf.Deg2Rad;
                    star = Mathf.Min(star, SegmentDistance(u, v, Mathf.Cos(a0) * .64f, Mathf.Sin(a0) * .64f, Mathf.Cos(a1) * .64f, Mathf.Sin(a1) * .64f));
                }
                a = Mathf.Max(a, EarthStroke(star, .034f, .88f) * (.85f + .3f * (Fbm(u * 6 + 2, v * 6 + 5, 2) - .5f)));
                float cell = deg / 30, local = (cell - Mathf.Floor(cell) - .5f) * 30 * Mathf.Deg2Rad * r;
                if (((int)cell & 1) == 0) a = Mathf.Max(a, EarthStroke(local, .032f, .85f) * Band(.69f, .8f, r));
                float diamond = (Mathf.Abs(u) + Mathf.Abs(v)) / .24f;
                if (diamond < 1.02f) a = Mathf.Max(a, Mathf.Lerp(.2f, 1, Smooth(0, .8f, 1 - diamond)) * (1 - Smooth(.97f, 1.02f, diamond)));
                a = Mathf.Max(a, .11f * (1 - Smooth(.6f, .64f, r)) * (.8f + .4f * Fbm(u * 3 + 1, v * 3 + 7, 2)));
                return White(Mathf.Clamp01(a) * (1 - Smooth(.95f, .99f, r)));
            }, sprite => EarthSeal = sprite);
            // A fire petal (bottom pivot, pointing up): a tongue swelling from its root to a curved, sharp tip, level falling
            // across it from a hot spine and along it toward the tip, with streaks running along its length.
            EarthSingle("Earth petal", 64, 128, new Vector2(.5f, 0), (u, v) =>
            {
                float h = (v + 1) * .5f;
                float w = h < .34f ? .92f * Mathf.Pow(Mathf.Sin(Mathf.Clamp01(h / .34f) * Mathf.PI * .5f), .55f) : .92f * Mathf.Pow(Mathf.Max(0, 1 - (h - .34f) / .64f), 1.2f);
                float c = .22f * h * h, across = 1 - Mathf.Abs(u - c) / Mathf.Max(.02f, w);
                if (across <= 0 || h > .985f) return Color.clear;
                float streaks = Fbm(h * 2.4f + 3, (u - c) * 9 + 5, 2);
                float level = Mathf.Pow(across, .7f) * (1.12f - .72f * h) * (.82f + .42f * (streaks - .5f));
                return White(Mathf.Clamp01(level) * Smooth(0, .04f, h));
            }, sprite => EarthPetal = sprite);
            // The armed mine's upright mark: a painted diamond frame, a solid diamond glowing hot at its heart and two side ticks.
            EarthSingle("Earth mine glyph", 128, 128, new Vector2(.5f, .5f), (u, v) =>
            {
                float frame = (Mathf.Abs(u) / .6f + Mathf.Abs(v) / .88f - 1) * .5f;
                float a = EarthStroke(frame, .08f);
                float inner = Mathf.Abs(u) / .2f + Mathf.Abs(v) / .3f;
                if (inner < 1.02f) a = Mathf.Max(a, Mathf.Lerp(.25f, 1, Smooth(0, .7f, 1 - inner)) * (1 - Smooth(.97f, 1.02f, inner)));
                a = Mathf.Max(a, EarthStroke(Mathf.Abs(v), .042f, .85f) * Band(.7f, .94f, Mathf.Abs(u)));
                return White(Mathf.Clamp01(a) * Window(Mathf.Max(Mathf.Abs(u), Mathf.Abs(v))));
            }, sprite => EarthGlyph = sprite);
            // Shards of a broken seal: pieces of its ring band (a curved arc with broken, slanted ends), a bright rim, the ring's
            // fine inner line and one rune tick across the band.
            EarthShardSet = new Sprite[3];
            for (int i = 0; i < 3; i++)
            {
                int variant = i;
                EarthSingle("Earth seal shard " + i, 64, 64, new Vector2(.5f, .5f), (u, v) =>
                {
                    const float Inner = 1.45f, Outer = 2.15f, CenterY = -1.8f;
                    float r = Mathf.Sqrt(u * u + Sq(v - CenterY)), theta = Mathf.Atan2(u, v - CenterY) * Mathf.Rad2Deg;
                    float left = -(13 + 4 * variant) + (r - 1.8f) * (variant == 1 ? -14 : 14), right = 11 + 3 * variant + (r - 1.8f) * (variant == 2 ? 14 : -10);
                    float d = Mathf.Min(Mathf.Min(r - Inner, Outer - r), Mathf.Min((theta - left) * Mathf.Deg2Rad * r, (right - theta) * Mathf.Deg2Rad * r));
                    float cover = 1 - Smooth(0, .07f, -d);
                    float mid = (left + right) * .5f + (variant - 1) * 3;
                    float tick = Gauss((theta - mid) * Mathf.Deg2Rad * r, .05f) * Band(1.55f, 2.05f, r);
                    float line = Gauss(r - 1.95f, .03f) * .7f;
                    float lit = cover * (.42f + .58f * Gauss(Mathf.Max(0, d), .09f)) + (tick * .9f + line) * Smooth(0, .03f, d);
                    return White(Mathf.Clamp01(lit) * Window(Mathf.Max(Mathf.Abs(u), Mathf.Abs(v))));
                }, sprite => EarthShardSet[variant] = sprite);
            }
        }
    }
}
