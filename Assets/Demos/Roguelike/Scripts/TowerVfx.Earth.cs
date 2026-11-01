using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Ground and contraptions: the travelling quake, the guarding orbs, the fighting phantom and the sealed mine.
    // Quake: the caster stamps and a molten rift runs along the floor stride by stride, rock fangs bursting up through it;
    // at level three its end erupts in a pillar of stone ringed with fangs.
    // Orbit: glassy azure spheres ride a spinner turning in the tilted orbit plane, so every orb sits exactly where gameplay
    // strikes (OrbitOffset); orbs on the far half of the ellipse draw behind the actors and each trails a comet tail.
    // Decoy: a frozen copy of the caster's own art (dark teal ink, pale light, a rim glow behind) slides out of the caster,
    // bobs and shimmers, lunges with a spectral crescent on each attack and shatters into shards when it expires.
    // Mine: seals are tossed from the hand, slam onto the floor, lock in when armed and blink slowly until the blast of rune
    // shards, magenta fire and shock rings on the true radius.
    // Every bright shape is paired with a saturated or dark alpha layer so it keeps its silhouette on pale skies.
    public sealed partial class TowerVfx
    {
        private static readonly Color EarthAmber = new Color(1, .62f, .24f), EarthMagma = new Color(1, .36f, .06f), EarthHot = new Color(1, .95f, .78f),
            EarthUmber = new Color(.22f, .1f, .03f, .78f), EarthDustTone = new Color(.62f, .5f, .38f, .55f), EarthSoil = new Color(.3f, .21f, .15f, .6f),
            EarthGold = new Color(1, .86f, .46f), EarthGoldHot = new Color(1, .97f, .82f),
            EarthAzure = new Color(.42f, .76f, 1), EarthAzureHot = new Color(.85f, .97f, 1), EarthNavy = new Color(.02f, .08f, .3f, .78f),
            EarthPale = new Color(.68f, .95f, 1), EarthTeal = new Color(.03f, .24f, .32f, .62f),
            EarthPink = new Color(1, .38f, .76f), EarthPinkHot = new Color(1, .88f, .96f), EarthPlum = new Color(.3f, .02f, .17f, .82f);

        // Textures are baked a few rows a frame once the effects exist (see EarthBakeStep), or at once on a skill's first use.
        private sealed class EarthBakeJob { public string name; public int width, height, row; public Vector2 pivot; public bool pair; public EarthPairShade shade; public Color32[] first, second; public Action<Sprite, Sprite> store; }
        private sealed class EarthBakeLoop { }
        private static readonly List<EarthBakeJob> EarthBakes = new List<EarthBakeJob>();
        private static bool EarthQueued, EarthBaked;
        private static Sprite[] EarthFangRock, EarthFangVein, EarthRiftInk, EarthRiftLight, EarthShardSet;
        private static Sprite EarthColumnRock, EarthColumnVein, EarthSphere, EarthWraith, EarthSeal, EarthSlab;
        private static readonly Dictionary<Material, Material> EarthInkMaterials = new Dictionary<Material, Material>();

        // One orb: its layers, their sorting orders in front of and behind the actors, where on the orbit it started, the
        // orbit radius and its halo's full strength (dimmed while the orb crosses in front of the body).
        private sealed class EarthOrbRig { public Clip[] layers; public int[] front, back; public float phase, radius, haloAlpha; public bool behind; public int beat; }
        // A per-frame job (orb depth, phantom pose, mine arming). Emitters stop while the effects are over their hard cap, so
        // the player loop runs these itself then.
        private sealed class EarthDriver { public TowerVfx host; public Clip clip; public Action<Clip> run; }
        private static readonly List<EarthDriver> EarthDrivers = new List<EarthDriver>();
        // The last stride of a running rift (a clip as its clock and where it landed): a front pinned against the world's
        // edge lands on the same spot again and only crumbles there.
        private Clip EarthStrideClock; private Vector2 EarthStrideAt;
        // The last orb cast (who cast it, and a clip that lives through its release): a companion's orbs fly to the hero they guard.
        private Transform EarthOrbitFrom; private Clip EarthOrbitClock;
        // A phantom standing at its zone root: its body copies with their rest offsets, which way it faces and when it lunged.
        private sealed class EarthPhantom
        {
            public Transform root; public readonly List<Clip> body = new List<Clip>(); public readonly List<Vector2> rest = new List<Vector2>();
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
                // An instant echo: a quick stamp of light and dust; the rift itself follows at once.
                var stamp = Spawn("Earth echo stamp", glow, foot + Vector2.up * .08f, 1.8f, Alpha(amber, .75f), .24f, true, 26);
                EarthSquash(stamp, .4f); stamp.startScale = .4f; stamp.endScale = 1.2f; stamp.ease = 3; stamp.fadePower = 1.4f;
                Dust(foot, 0, .7f);
                return;
            }
            float total = w + .3f, hold = w / total;

            // The ground answers: a dark halo for the light, an earth seal turning under the feet and molten cracks spreading.
            var shade = Underlay(ground + Vector2.up * .7f, 3.6f * k, .28f, total); shade.hold = hold;
            var sealInk = Spawn("Earth quake seal ink", runeRing, ground, 3.25f * k, Alpha(EarthUmber, .9f), total, false, 25); LayFlat(sealInk, .3f);
            var seal = Spawn("Earth quake seal", runeRing, ground, 3 * k, Alpha(hot, .9f), total, true, 27); LayFlat(seal, .3f);
            foreach (var x in new[] { sealInk, seal }) { x.spin = 70 * side; x.fadeIn = .08f; x.startScale = .4f; x.endScale = 1; x.ease = 3; x.hold = hold; x.fadePower = 1.5f; }
            var pool = GroundGlow(ground, 3.8f * k, Alpha(amber, .55f), total); pool.hold = hold;
            // The floor scar tears open in the last moments before the stamp, marking where the rift will start.
            float open = Mathf.Max(0, release - .15f), riftAngle = Range(-8, 8);
            var scar = Decal(ground, crack, 2.8f * k, new Color(.07f, .04f, .02f, .92f), total + .8f - open, false, 25);
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
                var crumb = Spawn("Earth slab crumb", rock[i % rock.Length], ground + new Vector2(x, .4f), Range(.08f, .13f) * k, new Color(1, .9f, .8f), .4f, false, order);
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
            var stance = Anchored(c.anchor, c.pos + new Vector2(side * .15f, -.45f), "Earth quake stance glow", glow, 1.7f * k, Alpha(amber, .6f), w + .05f, 124);
            stance.fadeIn = w * .5f; stance.startScale = .5f; stance.endScale = 1.1f; stance.ease = 1.5f; stance.hold = .8f; stance.pulse = .08f;

            // Release: the stamp. A flash on the floor, rings out along it, dust thrown both ways, chips flying and the first
            // stretch of molten rift opening toward the target.
            var stampFlash = EarthLater(Flash(foot + Vector2.up * .3f, 2.8f * k, Alpha(hot, .85f), .18f), release); stampFlash.ramp = Ramp(Color.white, amber);
            EarthLater(EarthRing(foot + Vector2.up * .04f, 2.3f * k, Alpha(amber, .9f), .4f, true, 116, .32f), release);
            EarthLater(EarthRing(foot, 2.4f * k, EarthUmber, .45f, false, 24, .34f), release);
            EarthLater(EarthInk(ImpactLines(foot + Vector2.up * .45f, 3.6f * k, EarthUmber, .26f), 118), release);
            EarthLater(ImpactLines(foot + Vector2.up * .45f, 3.1f * k, Alpha(hot, .8f), .22f), release);
            EarthRiftSegment(foot + new Vector2(side * .3f, 0), side, amber, release);
            int chips = Budget(4);
            for (int i = 0; i < chips; i++)
            {
                var chip = EarthLater(Spawn("Earth stamp chip", rock[i % rock.Length], foot + new Vector2(Range(-.4f, .4f), .1f), Range(.12f, .24f) * k, new Color(1, .9f, .8f), Range(.5f, .8f), false, 128), release);
                chip.velocity = new Vector2(Range(-3.5f, 3.5f), Range(3, 6)); chip.gravity = 16; chip.spin = Range(-600, 600); chip.hold = .5f; chip.fadePower = .6f;
            }
            for (int i = 0; i < 4; i++)
            {
                float s = i % 2 == 0 ? 1 : -1;
                var dust = EarthLater(Puff(foot + new Vector2(s * .3f, .15f), Range(.7f, 1) * k, i < 2 ? EarthDustTone : EarthSoil, new Vector2(s * Range(2.5f, 4), Range(.4f, 1.1f)), Range(.55f, .8f), i < 2 ? 118 : 116), release);
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
                var bit = Spawn("Earth rift chip", rock[random.Next(rock.Length)], ground + new Vector2(0, .15f), Range(.1f, .18f) * k, new Color(1, .9f, .82f), .55f, false, 128);
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
                float height = Range(1.05f, 1.45f) * k * (i == 0 ? 1 : .85f);
                Vector2 at = ground + new Vector2(-side * (.12f + i * .28f) + Range(-.06f, .06f), -.03f);
                fangs.Add(EarthFangRise(at, height, side * Range(6, 20) * (i % 2 == 0 ? 1 : -.4f), 27, stand + i * .04f, i * .015f, i == 0));
            }
            if (Chance(.45f)) fangs.Add(EarthFangRise(ground + new Vector2(-side * Range(.25f, .4f), -.1f), Range(.55f, .8f) * k, side * Range(10, 24), 120, stand - .06f, .02f, false));
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
                var shed = EarthLater(Spawn("Earth fang shard", rock[random.Next(rock.Length)], ground + new Vector2(-side * .2f, Range(.5f, .9f) * k), Range(.1f, .16f) * k, new Color(1, .9f, .82f), .45f, false, 128), stand - owner.age);
                shed.velocity = new Vector2(Range(-1.2f, 1.2f), Range(.8f, 1.8f)); shed.gravity = 15; shed.spin = Range(-500, 500); shed.hold = .5f; shed.fadePower = .7f;
            });

            // A low spout of magma light behind the fangs and a plume of soil; a chip flies (and a spark, every other stride).
            var spout = Pillar(ground + Vector2.down * .05f, .8f * k, 1.1f * k, Alpha(Color.Lerp(amber, EarthMagma, .5f), .5f), .26f, 26);
            spout.axisStart = new Vector2(.6f, .15f); spout.fadePower = 1.6f;
            var plume = Puff(ground + new Vector2(-side * .2f, .25f), Range(.9f, 1.2f) * k, Alpha(EarthSoil, .7f), new Vector2(side * Range(.3f, .8f), Range(1.6f, 2.4f)), Range(.7f, .85f), 116);
            plume.drag = 2.4f; plume.endScale = 2.2f;
            var chip = Spawn("Earth rift chip", rock[random.Next(rock.Length)], ground + new Vector2(Range(-.3f, .2f), .1f), Range(.09f, .2f) * k, new Color(1, .9f, .82f), Range(.5f, .8f), false, 128);
            chip.velocity = new Vector2(side * Range(.5f, 2.5f) + Range(-1.2f, 1.2f), Range(3.5f, 6.5f) * Mathf.Sqrt(k)); chip.gravity = 17; chip.spin = Range(-700, 700); chip.hold = .5f; chip.fadePower = .6f;
            if (beat % 2 == 1) Spark(ground + new Vector2(Range(-.3f, .2f), .1f), new Vector2(Range(-1.6f, 1.6f), Range(3, 6) * Mathf.Sqrt(k)), amber, Range(.16f, .26f) * k, Range(.3f, .5f), 7, 1.5f).flicker = .3f;
            // Embers keep rising off the fresh seam for a while, so the live rift moves where the lava floor below only glows.
            int embers = g >= 2 ? 1 + beat % 2 : 1;
            for (int i = 0; i < embers; i++)
            {
                var ember = Spark(ground + new Vector2(-side * Range(.05f, .6f), .04f), new Vector2(Range(-.25f, .25f), Range(1.1f, 2)), i % 2 == 0 ? EarthGoldHot : amber, Range(.1f, .16f), Range(.35f, .5f), -1.5f, 1.2f);
                ember.flicker = .35f; EarthLater(ember, Range(.04f, .34f));
            }
            var heat = Spawn("Earth rift heat", glow, ground + new Vector2(-side * .3f, .05f), 1.9f * k, Alpha(amber, .45f), .65f, true, 26);
            EarthSquash(heat, .35f); heat.startScale = .5f; heat.endScale = 1.1f; heat.ease = 3; heat.hold = .3f; heat.fadePower = 1.4f;
            if (beat % 2 == 0) Flash(ground + Vector2.up * .2f, 1.1f * k, Alpha(amber, .6f), .12f);
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
            var heart = Spawn("Earth eruption heart", core, ground + Vector2.up * .4f, 1.1f * s, Color.white, .12f, true, 133); heart.startScale = .5f; heart.endScale = 1.8f; heart.ease = 2.5f; heart.fadePower = 1.5f;
            var bloom = Flash(ground + Vector2.up * .8f, 2.8f * s, Alpha(amber, .65f), .28f); bloom.ramp = Ramp(Color.white, amber);
            EarthInk(ImpactLines(ground + Vector2.up * .8f, 6 * s, EarthUmber, .32f), 118);
            ImpactLines(ground + Vector2.up * .8f, 5.2f * s, Alpha(hot, .7f), .28f);

            // The pillar shoots up and stands a second (its outline is baked in, so it keeps its shape on pale stone), its seams
            // cooling from white through gold to amber; then it sinks back into the floor as solid stone.
            const float Stand = 1, Sink = .35f;
            float width = 1.25f * s, height = EarthColumnRock.bounds.size.y / EarthColumnRock.bounds.size.x * width;
            Vector2 foot = ground + Vector2.down * .12f;
            var stone = Spawn("Earth eruption pillar", EarthColumnRock, foot, width, Color.white, Stand + .02f, false, 28);
            var seams = Spawn("Earth eruption pillar seams", EarthColumnVein, foot, width, new Color(1, 1, 1, .95f), Stand * .85f, true, 29);
            foreach (var x in new[] { stone, seams }) { x.axisStart = new Vector2(.75f, 0); x.axisEnd = Vector2.one; x.ease = EarthPop(x.life, .1f); x.fadePower = 0; }
            seams.ramp = Ramp(Color.white, EarthGoldHot, amber); seams.hold = .35f; seams.fadePower = 1.4f;
            var sink = EarthLater(Spawn("Earth eruption pillar sinking", EarthColumnRock, foot, width, Color.white, Sink, false, 28), Stand);
            sink.axisStart = Vector2.one; sink.axisEnd = new Vector2(.92f, 0); sink.ease = 1.3f; sink.hold = .7f; sink.fadePower = 1;
            // A short squat swell of heat round its foot, and a brief gold flash on its crest.
            var heat = Spawn("Earth eruption heat", glow, ground + Vector2.up * 1.1f * s, 2.5f * s, Alpha(amber, .4f), .5f, true, 26);
            heat.scale.y *= 1.6f; heat.root.transform.localScale = heat.scale; heat.startScale = .5f; heat.endScale = 1.1f; heat.ease = 3; heat.hold = .2f; heat.fadePower = 1.5f;
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

            // The floor: a molten crack across the area, shock rings out to its true edge and dust rolling with them.
            Crack(ground, R * 2.1f, Alpha(amber, .9f), 1.6f);
            EarthRing(ground + Vector2.up * .03f, R, Alpha(hot, .9f), .45f, true, 116, .32f);
            EarthRing(ground, R * 1.02f, EarthUmber, .5f, false, 24, .34f);
            for (int i = 0; i < 2; i++)
            {
                float side = i % 2 == 0 ? 1 : -1;
                var dust = Puff(ground + new Vector2(side * Range(.2f, .6f), .2f), Range(.8f, 1.2f) * s, i == 0 ? EarthSoil : EarthDustTone, new Vector2(side * Range(2.5f, 4.5f) * s, Range(.3f, 1.2f)), Range(.7f, 1), 118);
                dust.drag = 2.2f; dust.endScale = 2.3f;
            }
            // Boulders are flung high and rain back; embers stream up the pillar; smoke climbs it.
            int boulders = Budget(2);
            for (int i = 0; i < boulders; i++) EarthBoulder(ground + new Vector2(Range(-.5f, .5f), Range(.5f, 2.5f) * s), s, new Vector2(Range(-4, 4) * s, Range(5, 9) * s), i % 2 == 0 ? 128 : 119, 0);
            EmberBurst(ground + Vector2.up * 1.2f * s, 3, 7 * s, amber, .7f, 4);
            for (int i = 0; i < 2; i++)
            {
                var fume = EarthLater(Puff(ground + new Vector2(Range(-.4f, .4f), Range(1, 2.6f) * s), Range(1, 1.4f) * s, EarthSoil, new Vector2(Range(-.3f, .3f), Range(.5f, 1)), Range(1, 1.3f), 115), .1f + i * .1f);
                fume.drag = 1; fume.endScale = 2.4f; fume.fadeIn = .1f;
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
            var chunk = Spawn("Earth boulder", rock[random.Next(rock.Length)], at, Range(.22f, .42f) * s, new Color(1, .9f, .8f), Range(.9f, 1.3f), false, order);
            chunk.velocity = velocity; chunk.gravity = 18; chunk.spin = Range(-400, 400); chunk.hold = .6f; chunk.fadePower = .6f;
            EarthLater(chunk, delay);
        }

        /// <summary>One stride of the rift ending at <paramref name="front"/>: a dark gash with a white-gold molten seam laid behind
        /// the front, so strides join end to end (both ends of the strip sit at mid-height). Returns the gash.</summary>
        private Clip EarthRiftSegment(Vector2 front, int side, Color amber, float delay)
        {
            int variant = random.Next(EarthRiftInk.Length); float flipY = Chance(.5f) ? -1 : 1, flipX = Chance(.5f) ? -1 : 1;
            Vector2 mid = front + new Vector2(-side * .3f, -.05f);
            var gash = Spawn("Earth rift", EarthRiftInk[variant], mid, .8f, Color.white, 2.4f, false, 24);
            var seam = Spawn("Earth rift magma", EarthRiftLight[variant], mid, .8f, Color.white, 1.6f, true, 26);
            foreach (var x in new[] { gash, seam })
            {
                x.scale.y *= 1.1f * flipY; x.scale.x *= flipX; x.root.transform.localScale = x.scale;
                x.axisStart = new Vector2(.4f, .25f); x.axisEnd = Vector2.one; x.ease = EarthPop(x.life, .06f); x.fadeIn = .02f;
                EarthLater(x, delay);
            }
            gash.hold = .6f; gash.fadePower = 1.2f;
            seam.ramp = Ramp(Color.white, EarthGoldHot, amber); seam.hold = .3f; seam.fadePower = 1.5f;
            return gash;
        }

        // A fang standing in the floor, remembered until it sinks: its look, its pose and when (from its spawn) it starts to sink.
        private struct EarthFang { public Sprite sprite; public Vector2 ground; public float width, tall, lean, sinkAt; public int order; }

        /// <summary>A fang of rock erupting from the floor: stone (its dark outline baked in), with molten veins cooling over it
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
            if (veins != null) { veins.ramp = Ramp(EarthHot, EarthMagma); veins.hold = .25f; veins.fadePower = 1.4f; }
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
            var sealInk = Spawn("Earth orbit cast seal ink", runeRing, ground, 2.8f, EarthNavy, total, false, 25); LayFlat(sealInk, .3f);
            EarthOrbitClock = sealInk;
            var seal = Spawn("Earth orbit cast seal", runeRing, ground, 2.7f, Alpha(EarthAzureHot, .9f), total, true, 27); LayFlat(seal, .3f);
            foreach (var x in new[] { sealInk, seal }) { x.spin = 90; x.fadeIn = .08f; x.startScale = .4f; x.endScale = 1; x.ease = 3; x.hold = hold; x.fadePower = 1.5f; }
            var pool = GroundGlow(ground, 3.4f, Alpha(EarthAzure, .45f), total); pool.hold = hold;
            var aura = Anchored(c.anchor, c.pos, "Earth orbit cast aura", glow, 2.5f, Alpha(EarthAzure, .4f), w + .1f, 27);
            aura.fadeIn = w * .4f; aura.hold = .8f; aura.pulse = .06f;

            // The orbs condense out of the caster: a spinner in the orbit plane widens from the body to the true orbit radius
            // and turns so that at the release each orb sits exactly where the guard's orb takes over.
            var spinner = Spawn("Earth orbit cast spinner", core, hub, core.bounds.size.x, Color.clear, w + .02f, true, 0);
            // A clear pivot: its renderer keeps its sprite (fully transparent, so it draws nothing) but is never animated again.
            spinner.renderer = null;
            LayFlat(spinner, OrbitTilt);
            spinner.spin = OrbitDegreesPerSecond; spinner.angle = -OrbitDegreesPerSecond * w;
            spinner.startScale = .15f; spinner.endScale = 1; spinner.ease = 1.6f;
            var rigs = new EarthOrbRig[n]; var layers = new List<Clip>();
            for (int i = 0; i < n; i++) rigs[i] = EarthOrb(spinner.root.transform, spinner.angle, i, n, R, size, g, layers);
            foreach (var clip in layers) { clip.life = w + .02f; clip.loop = false; clip.fadeIn = .08f; clip.fadePower = 0; }
            EarthEachFrame(seal, owner => { foreach (var rig in rigs) EarthOrbDepth(rig, spinner.angle); });
            Converge(hub, 12, 2.6f, EarthAzure, w, .45f);

            // Release: a flash at the hub and a ring running along the orbit itself.
            var flash = EarthLater(Flash(hub, 2.4f, Alpha(EarthAzureHot, .75f), .2f), release); flash.ramp = Ramp(Color.white, EarthAzure);
            var path = EarthLater(EarthRing(hub, R, Alpha(EarthAzure, .85f), .35f, true, 121, OrbitTilt), release); path.startScale = .5f;
            var pathInk = EarthLater(EarthRing(hub, R * 1.01f, EarthNavy, .38f, false, 117, OrbitTilt), release); pathInk.startScale = .5f;
        }

        private void ZoneOrbit(Transform root, float radius, float life, int count)
        {
            EarthTextures();
            int g = Grade, n = Mathf.Max(1, count); float R = Mathf.Max(.3f, radius), size = EarthOrbSize(g);
            float span = life + .1f, hold = Mathf.Clamp01(1 - .4f / span);
            var layers = new List<Clip>();
            // A faint glow round the guarded body (behind the actors) carries the frame clock.
            var aura = Attach(root, "Earth orbit aura", glow, 2.6f, Alpha(EarthAzure, .16f), 27); aura.pulse = .05f;
            // The guard's seal turning in the floor plane under the hub.
            var floor = EarthPlane(root, new Vector2(0, -.58f), .3f);
            var sealInk = Attach(floor, "Earth orbit seal ink", runeRing, 2.3f, EarthNavy, 25, false); sealInk.spin = 50;
            var seal = Attach(floor, "Earth orbit seal", runeRing, 2.2f, Alpha(EarthAzure, .7f), 27); seal.spin = 50;
            var pool = Attach(floor, "Earth orbit floor light", glow, 3, Alpha(EarthAzure, .35f), 26); pool.pulse = .05f;
            layers.Add(sealInk); layers.Add(seal); layers.Add(pool);
            // The orbs ride a spinner turning in the tilted orbit plane, so each sits exactly on OrbitOffset; they counter-turn
            // to stay upright and undo the plane's squash, so they stay round.
            var plane = EarthPlane(root, Vector2.zero, OrbitTilt);
            var spinner = Attach(plane, "Earth orbit spinner", core, core.bounds.size.x, Color.clear, 0);
            spinner.spin = OrbitDegreesPerSecond; layers.Add(spinner);
            var rigs = new EarthOrbRig[n];
            for (int i = 0; i < n; i++) rigs[i] = EarthOrb(spinner.root.transform, 0, i, n, R, size, g, layers);
            foreach (var clip in layers) { clip.life = span; clip.loop = false; clip.hold = hold; clip.fadePower = 1.2f; }
            foreach (var x in new[] { sealInk, seal, pool }) { x.fadeIn = .15f; x.startScale = .5f; x.endScale = 1; x.ease = EarthPop(span, .2f); }
            aura.life = span; aura.loop = false; aura.hold = hold; aura.fadePower = 1.2f; aura.fadeIn = .2f;
            bool ended = false;
            EarthEachFrame(aura, owner =>
            {
                foreach (var rig in rigs) EarthOrbDepth(rig, spinner.angle);
                if (ended || owner.age < life - .1f) return;
                // The guard runs out: each orb breaks up in a burst of light.
                ended = true;
                foreach (var rig in rigs)
                {
                    var body = rig.layers[1]; if (!body.root) continue;
                    Vector2 at = body.root.transform.position;
                    var pop = Flash(at, 1.3f, Alpha(EarthAzure, .8f), .16f); pop.ramp = Ramp(Color.white, EarthAzure);
                    Glint(at, 1.2f, Color.white, .2f, Range(0, 45)).spin = 0;
                    Sparkles(at, 4, .2f, EarthAzureHot);
                }
            });
            Bundle(root, aura, layers.ToArray());
            // Arrival: a ring along the orbit and a star on each orb.
            Vector2 hub = root.position;
            EarthRing(hub, R, Alpha(EarthAzure, .7f), .3f, true, 121, OrbitTilt).startScale = .8f;
            for (int i = 0; i < n; i++) Glint(hub + OrbitOffset(i, n, 0, R), 1.1f, Alpha(EarthAzureHot, .9f), .18f, 0).spin = 0;

            // Cast by a companion away from the hero it guards: the orbs it condensed streak over and lock into the orbit.
            var from = EarthOrbitClock != null && clips.Contains(EarthOrbitClock) ? EarthOrbitFrom : null;
            EarthOrbitClock = null;
            if (!from) return;
            Vector2 start = (Vector2)from.position + Vector2.up * .6f;
            if ((start - hub).sqrMagnitude < .8f * .8f) return;
            const float Flight = .14f;
            EarthInk(Line(start, hub, .5f, Alpha(EarthNavy, .4f), .24f, 117), 117);
            var beam = Line(start, hub, .28f, Alpha(EarthAzure, .85f), .22f, 121); beam.fadePower = 1.5f;
            for (int i = 0; i < n; i++)
            {
                Vector2 to = hub + OrbitOffset(i, n, Flight, R), velocity = (to - start) / Flight;
                var flier = Spawn("Earth orb flight", EarthSphere, start, size * .85f, Color.white, Flight, false, 123);
                var light = Spawn("Earth orb flight light", glow, start, size * 1.6f, Alpha(EarthAzure, .7f), Flight, true, 122);
                foreach (var x in new[] { flier, light }) { x.velocity = velocity; x.fadePower = 0; }
                Emitter(light, Emit.Trace, 70, .05f, Alpha(EarthAzure, .6f));
            }
            foreach (var rig in rigs) foreach (var clip in rig.layers) clip.fadeIn = Flight;
        }

        // An orb striking an enemy as it passes: a small contact flash.
        private void ImpactOrbit(Vector2 pos, float radius, Color tint, int facing)
        {
            EarthTextures();
            int g = Grade; float k = Mathf.Clamp(radius / .5f, .7f, 1.6f) * (1 + .1f * (g - 1));
            Color azure = EarthBlend(tint, EarthAzure, .3f);
            var flash = Flash(pos, 1.5f * k, Alpha(azure, .85f), .14f); flash.ramp = Ramp(Color.white, azure);
            var star = Glint(pos, 1.5f * k, Color.white, .18f, Range(0, 45)); star.spin = 0;
            EarthInk(ImpactLines(pos, 2.1f * k, EarthNavy, .2f), 118);
            ImpactLines(pos, 1.8f * k, Alpha(EarthAzureHot, .75f), .18f);
            Wave(pos, 1.5f * k, Alpha(azure, .85f), .22f, 3);
            int sparks = Budget(4);
            for (int i = 0; i < sparks; i++) Spark(pos, Direction(Range(0, 360)) * Range(3, 6) * k, i % 2 == 0 ? Color.white : azure, Range(.18f, .28f), Range(.18f, .28f), 4, 3);
            if (g >= 3) { var gold = Glint(pos, 1.1f * k, Alpha(EarthGold, .9f), .22f, 45); gold.spin = 90; }
        }

        private static float EarthOrbSize(int grade) { return .72f + .08f * (Mathf.Clamp(grade, 1, 3) - 1); }

        /// <summary>One orb riding <paramref name="spinner"/> (whose angle is <paramref name="spinStart"/> now): a glow, the glassy
        /// sphere, a rim of light and a white heart (and a gold crown at level three), each counter-turning and unsquashed so it
        /// stays upright and round; the heart trails a comet tail.</summary>
        private EarthOrbRig EarthOrb(Transform spinner, float spinStart, int index, int count, float R, float size, int g, List<Clip> into)
        {
            float phase = index * 360f / count;
            Vector2 offset = Direction(phase) * R;
            // The halo stays modest (and dims further while the orb passes in front of the body) so the guarded hero stays clear.
            var rig = new EarthOrbRig { phase = phase, radius = R, haloAlpha = g >= 3 ? .26f : g == 2 ? .33f : .4f };
            var halo = Attach(spinner, "Earth orb halo", glow, size * 1.9f, Alpha(EarthAzure, rig.haloAlpha), 122, true, offset); halo.pulse = .07f;
            var shell = Attach(spinner, "Earth orb", EarthSphere, size, Color.white, 123, false, offset);
            var rim = Attach(spinner, "Earth orb rim", orb, size * 1.04f, Alpha(EarthAzureHot, .7f), 124, true, offset);
            var heart = Attach(spinner, "Earth orb heart", core, size * .38f, Color.white, 125, true, offset); heart.pulse = .1f;
            var list = new List<Clip> { halo, shell, rim, heart };
            // Awakened: a gold rim of light just outside the glass (a larger gold sphere behind it).
            if (g >= 3) list.Add(Attach(spinner, "Earth orb crown", orb, size * 1.12f, Alpha(EarthGold, .8f), 122, true, offset));
            rig.layers = list.ToArray(); rig.front = new int[list.Count]; rig.back = new int[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var clip = list[i];
                clip.spin = -OrbitDegreesPerSecond; clip.angle = -spinStart; clip.scale.y /= OrbitTilt; clip.root.transform.localScale = clip.scale;
                rig.front[i] = clip.renderer.sortingOrder; rig.back[i] = clip.renderer.sortingOrder - 100;
                into.Add(clip);
            }
            Emitter(heart, 44, 0, (owner, p) => EarthOrbWake(p, rig, size, g));
            return rig;
        }

        /// <summary>Orbs on the far half of the ellipse draw behind the actors and a little smaller; an orb crossing in front of
        /// the body dims its halo so it never washes the hero out.</summary>
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
            halo.color = Alpha(halo.color, rig.behind ? rig.haloAlpha : Mathf.Lerp(.15f, rig.haloAlpha, Smooth(.3f, .6f, across)));
        }

        // The comet tail: a short trail of soft azure light with an occasional navy smudge (so the tail reads on pale skies)
        // and sparkles. In front of the body only the bright beads draw over the actors; the rest of the tail stays behind
        // them, so it never lays a band across the hero.
        private void EarthOrbWake(Vector2 at, EarthOrbRig rig, float size, int g)
        {
            int n = rig.beat++;
            if (n % 3 == 2)
            {
                var smudge = Spawn("Earth orb wake ink", glow, at, size * 1.25f, Alpha(EarthNavy, .42f), .2f, false, rig.behind ? 20 : 28);
                smudge.endScale = .25f; smudge.ease = 1; smudge.fadePower = 1.2f;
                return;
            }
            bool bead = n % 3 == 0;
            var dot = Spawn("Earth orb wake", glow, at, size * Range(1.1f, 1.3f), Alpha(bead ? EarthAzureHot : EarthAzure, .85f), Range(.16f, .2f), true, rig.behind ? 21 : bead ? 121 : 29);
            dot.endScale = .12f; dot.ease = 1; dot.fadePower = 1;
            if (g >= 2 && n % 8 == 0)
            {
                var mote = Spawn("Earth orb sparkle", glow, at + Inside(.15f), Range(.1f, .16f), Alpha(g >= 3 ? EarthGoldHot : Color.white, .95f), Range(.3f, .45f), true, rig.behind ? 22 : 126);
                mote.velocity = Inside(1) + Vector2.down * .3f; mote.drag = 2; mote.flicker = .5f; mote.endScale = .2f; mote.ease = 1;
            }
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
            // A mirror seal under the caster.
            var sealInk = Spawn("Earth decoy seal ink", runeRing, ground, 2.7f, EarthTeal, total, false, 25); LayFlat(sealInk, .3f);
            var seal = Spawn("Earth decoy seal", runeRing, ground, 2.6f, Alpha(pale, .9f), total, true, 27); LayFlat(seal, .3f);
            foreach (var x in new[] { sealInk, seal }) { x.spin = -110 * side; x.fadeIn = .08f; x.startScale = .4f; x.endScale = 1; x.ease = 3; x.hold = hold; x.fadePower = 1.5f; }
            var pool = GroundGlow(ground, 3.2f, Alpha(pale, .45f), total); pool.hold = hold;

            // The caster's image splits: two pale copies peel away to either side, flickering, while mirror light gathers.
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
                    echo.color = Alpha(pale, i == 0 ? .6f : .4f); echo.velocity = new Vector2(dir * travel / w, 0);
                    echo.life = w + .06f; echo.fadeIn = w * .35f; echo.hold = .75f; echo.fadePower = 2; echo.flicker = .35f;
                }
            }
            Converge(c.pos, 10, 2, pale, w, .5f);
            for (int i = 0; i < 5; i++)
            {
                var star = EarthLater(Glint(c.pos + new Vector2(Range(-.8f, .8f), Range(-.8f, .9f)), Range(.6f, .9f), Alpha(i % 2 == 0 ? Color.white : pale, .9f), .2f, Range(0, 45)), Range(0, Mathf.Max(.01f, w - .15f)));
                star.spin = 0;
            }
            for (int i = 0; i < 3; i++)
            {
                var mist = EarthLater(Puff(ground + new Vector2(Range(-.7f, .7f), .15f), Range(.7f, 1), Alpha(EarthTeal, .45f), new Vector2(Range(-.4f, .4f), Range(.2f, .5f)), w * .9f, 116), Range(0, w * .3f));
                mist.drag = 1; mist.fadeIn = .1f; mist.endScale = 2;
            }

            // Release: the split snaps with a pale flash and the phantom slides out (the zone takes over).
            var flash = EarthLater(Flash(c.pos, 2.4f, Alpha(pale, .8f), .2f), release); flash.ramp = Ramp(Color.white, pale);
            var cross = EarthLater(Glint(c.pos, 2.4f, Color.white, .25f), release); cross.spin = 0;
            EarthLater(Wave(c.pos, 3, Alpha(pale, .75f), .3f, 3), release);
            EarthLater(EarthInk(ImpactLines(c.pos, 3.4f, EarthTeal, .26f), 118), release);
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
                    EarthInk(Line(chest, at + Vector2.up * .2f, .45f, Alpha(EarthTeal, .4f), .24f, 117), 117);
                    var streak = Line(chest, at + Vector2.up * .2f, .26f, Alpha(pale, .8f), .22f, 121); streak.fadePower = 1.5f;
                }
                int copyFacing;
                // A rim glow behind, the dark teal body and the pale light over it, all frozen copies of the caster's art sharing
                // one snapshot of its pose. The light copy may outlive the root (it lifts away when the phantom shatters), so it
                // owns the snapshot.
                var snapshots = new Dictionary<MeshFilter, Mesh>();
                var rimGhost = EarthGhost(caster, root, feet, 2, false, out copyFacing, snapshots, false);
                if (rimGhost != null)
                {
                    rec.copyFacing = copyFacing;
                    var inkGhost = EarthGhost(caster, root, feet, 0, false, out copyFacing, snapshots, false);
                    var lightGhost = EarthGhost(caster, root, feet, 1, false, out copyFacing, snapshots, true);
                    rimGhost.color = Alpha(pale, .55f); rimGhost.scale = new Vector3(1.08f, 1.05f, 1); rimGhost.flicker = .3f;
                    inkGhost.color = Alpha(EarthTeal, .55f);
                    lightGhost.color = Alpha(pale, .6f); lightGhost.flicker = .12f;
                    rec.light = lightGhost;
                    EarthPhantomBody(rec, rimGhost, feet + new Vector2(0, -.04f));
                    EarthPhantomBody(rec, inkGhost, feet);
                    EarthPhantomBody(rec, lightGhost, feet);
                }
            }
            if (rec.body.Count == 0)
            {
                // No caster art to copy: a cloaked spectral warrior stands in.
                var rimShape = Attach(root, "Earth wraith rim", EarthWraith, 1.04f, Alpha(pale, .45f), 27, true, feet + new Vector2(0, -.04f)); rimShape.flicker = .3f;
                var inkShape = Attach(root, "Earth wraith ink", EarthWraith, .95f, Alpha(EarthTeal, .6f), 28, false, feet);
                var lightShape = Attach(root, "Earth wraith", EarthWraith, .95f, Alpha(pale, .7f), 125, true, feet); lightShape.flicker = .12f;
                rec.light = lightShape; rec.copyFacing = 1;
                EarthPhantomBody(rec, rimShape, feet + new Vector2(0, -.04f)); EarthPhantomBody(rec, inkShape, feet); EarthPhantomBody(rec, lightShape, feet);
            }
            foreach (var part in rec.body)
            {
                part.life = span; part.loop = false; part.fadeIn = .15f; part.hold = hold; part.fadePower = 1;
                part.axisStart = new Vector2(1.15f, .55f); part.axisEnd = Vector2.one; part.ease = rise;
            }
            EarthPhantomPose(rec, 0);

            // Under it a pale pool and a dark ring on the floor; a turning rune ring at level two, a gold one at three.
            var plane = EarthPlane(root, new Vector2(0, -.58f), .3f);
            var pool = Attach(plane, "Earth phantom floor", glow, 2.6f, Alpha(pale, .38f), 26); pool.pulse = .05f;
            var poolInk = Attach(plane, "Earth phantom floor ink", ring, 2.4f, Alpha(EarthTeal, .36f), 25, false);
            var extras = new List<Clip> { pool, poolInk };
            if (g >= 2) { var runes = Attach(plane, "Earth phantom runes", runeRing, 2.2f, Alpha(pale, .7f), 27); runes.spin = 70; extras.Add(runes); }
            if (g >= 3) { var crown = Attach(plane, "Earth phantom crown", runeRing, 2.8f, Alpha(EarthGold, .65f), 27); crown.spin = -45; extras.Add(crown); }
            foreach (var x in extras) { x.life = span; x.loop = false; x.fadeIn = .2f; x.hold = hold; x.fadePower = 1; x.startScale = .4f; x.endScale = 1; x.ease = rise; }
            var aura = Attach(root, "Earth phantom aura", glow, 2.4f, Alpha(pale, .2f), 26, true, new Vector2(0, .35f)); aura.pulse = .06f;
            aura.life = span; aura.loop = false; aura.fadeIn = .2f; aura.hold = hold; aura.fadePower = 1;
            rec.primary = aura;

            // Mist pools at its feet and pale wisps rise off the body.
            int beat = 0;
            Emitter(pool, 6 + 3 * g, 0, (owner, p) =>
            {
                if (!rec.root) return;
                Vector2 base0 = (Vector2)rec.root.position + feet;
                if (beat++ % 3 == 0)
                {
                    var mist = Puff(base0 + new Vector2(Range(-.5f, .5f), .12f), Range(.6f, .9f), Alpha(EarthTeal, .38f), new Vector2(Range(-.4f, .4f), Range(.15f, .45f)), Range(.8f, 1.1f), 116);
                    mist.endScale = 2; mist.fadeIn = .15f; mist.drag = 1.2f;
                    return;
                }
                var soul = Spawn("Earth phantom wisp", wisp, base0 + new Vector2(Range(-.32f, .32f), Range(.3f, 1.6f)), Range(.16f, .28f), Alpha(Chance(.3f) ? Color.white : pale, .85f), Range(.55f, .85f), true, 126);
                soul.velocity = new Vector2(Range(-.2f, .2f), Range(.7f, 1.3f)); soul.drag = .5f; soul.fadeIn = .1f; soul.flicker = .3f; soul.endScale = .3f; soul.ease = 1;
            });
            EarthEachFrame(aura, owner => EarthPhantomPose(rec, owner.age));
            EarthPhantoms.RemoveAll(x => !x.root); EarthPhantoms.Add(rec);
            var layers = new List<Clip>(rec.body); layers.AddRange(extras);
            Bundle(root, aura, layers.ToArray());

            // Arrival: a pale flash where it stands.
            var arrive = Flash(at + Vector2.up * .3f, 1.8f, Alpha(pale, .7f), .2f); arrive.ramp = Ramp(Color.white, pale);
            Glint(at + Vector2.up * .35f, 1.5f, Alpha(Color.white, .9f), .2f, 0).spin = 0;
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

            // A flattened crescent reaching the attack's radius sweeps down and forward on a diagonal: dark teal under pale light
            // under a white edge, with a lagging echo; a faint short arc on the far side shows the cut also catches foes behind.
            float W = Mathf.Clamp(radius * 1.75f, 1.6f, 3.4f), start = Range(-13, 3), angle = side > 0 ? start : 180 - start, swing = -side * 320;
            Vector2 c = pos + new Vector2(side * .35f, .05f);
            var ink = EarthInk(Crescent(c, W * 1.05f, Alpha(EarthTeal, .7f), .28f, angle, 118), 118);
            var arc = Crescent(c, W, Alpha(pale, .95f), .26f, angle, 130);
            var edge = Crescent(c + new Vector2(side * .05f, 0), W * .95f, new Color(1, 1, 1, .65f), .17f, angle, 131);
            var echo = EarthLater(Crescent(c - new Vector2(side * .12f, 0), W * .92f, Alpha(pale, .45f), .26f, angle + side * 20, 129), .03f);
            var rear = Crescent(pos + new Vector2(-side * .3f, .05f), W * .7f, Alpha(pale, .35f), .2f, side > 0 ? 180 - start : start, 129);
            foreach (var x in new[] { ink, arc, edge, echo, rear }) { EarthSquash(x, .62f); x.spin = swing; }
            rear.spin = -swing;
            Flash(c + new Vector2(side * W * .38f, -.15f), 1, Alpha(pale, .8f), .12f);
            int sparks = Budget(4);
            for (int i = 0; i < sparks; i++)
                Spark(c + new Vector2(side * W * .35f, 0) + Inside(.3f), new Vector2(side * Range(3, 6), Range(-2, 2.5f)), i % 2 == 0 ? Color.white : pale, Range(.2f, .32f), Range(.2f, .32f), 3, 2.5f);
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
                var crossInk = EarthLater(EarthInk(Crescent(cc, W * .98f, Alpha(EarthTeal, .6f), .24f, crossAngle, 118), 118), .1f);
                var cross = EarthLater(Crescent(cc, W * .93f, Alpha(pale, .9f), .22f, crossAngle, 130), .1f);
                foreach (var x in new[] { crossInk, cross }) { EarthSquash(x, .62f); x.spin = swing; }
            }
            if (g >= 3)
            {
                var gold = EarthLater(Crescent(c + new Vector2(side * .08f, 0), W * 1.02f, Alpha(EarthGold, .7f), .2f, angle, 131), .01f);
                EarthSquash(gold, .62f); gold.spin = swing;
                Glint(c + new Vector2(side * W * .38f, -.15f), 1.3f, Alpha(EarthGoldHot, .95f), .2f, 0).spin = 0;
            }
        }

        // The phantom expires: its light flares white and lifts away while it bursts into spectral glass and souls.
        private void EarthShatter(Vector2 pos, float radius, Color pale, EarthPhantom rec, int g)
        {
            float R = Mathf.Clamp(radius, .8f, 3.5f), s = Mathf.Clamp(R / 1.8f, .7f, 1.5f);
            Vector2 ground = pos + Vector2.down * .8f;
            if (rec != null) EarthReleaseGhost(rec);
            var shade = Underlay(pos, 3.6f * s, .3f, .5f); shade.hold = .2f;
            var heart = Spawn("Earth shatter heart", core, pos, 1.1f * s, Color.white, .12f, true, 133); heart.startScale = .5f; heart.endScale = 1.8f; heart.ease = 2.5f; heart.fadePower = 1.5f;
            var bloom = Flash(pos, 3 * s, Alpha(pale, .8f), .3f); bloom.ramp = Ramp(Color.white, pale);
            var star = Glint(pos, 2.8f * s, Color.white, .3f); star.spin = 0;
            EarthInk(ImpactLines(pos, 4.6f * s, Alpha(EarthTeal, .75f), .3f), 118);
            ImpactLines(pos, 4 * s, Alpha(pale, .75f), .26f);
            Wave(pos, R * 1.6f / .74f, Alpha(pale, .8f), .36f, 3);
            EarthRing(ground + Vector2.up * .03f, R, Alpha(pale, .9f), .45f, true, 116, .32f);
            EarthRing(ground, R * 1.02f, EarthTeal, .5f, false, 24, .34f);
            int pieces = Budget(12);
            for (int i = 0; i < pieces; i++)
            {
                bool lit = i % 2 == 0;
                Vector2 dir = Direction(i * 360f / pieces + Range(-15, 15));
                var piece = Spawn("Earth phantom shard", shard, pos + dir * .2f + new Vector2(0, Range(-.4f, .5f)), Range(.22f, .4f) * s, lit ? Alpha(pale, .95f) : Alpha(EarthTeal, .85f), Range(.55f, .85f), lit, lit ? 129 : 119);
                piece.velocity = dir * Range(3, 6.5f) * s + Vector2.up * 1.2f; piece.gravity = 7; piece.drag = 1.3f; piece.spin = Range(-600, 600); piece.angle = Range(0, 360); piece.hold = .3f; piece.fadePower = 1.3f;
            }
            int souls = Budget(5);
            for (int i = 0; i < souls; i++)
            {
                var soul = Spawn("Earth phantom soul", wisp, pos + Inside(.5f), Range(.25f, .4f), Alpha(i % 2 == 0 ? Color.white : pale, .9f), Range(.8f, 1.1f), true, 127);
                soul.velocity = new Vector2(Range(-.6f, .6f), Range(1.5f, 3)); soul.drag = .6f; soul.fadeIn = .05f; soul.flicker = .3f; soul.endScale = .3f; soul.ease = 1;
            }
            for (int i = 0; i < 3; i++)
            {
                var mist = Puff(ground + new Vector2(Range(-.6f, .6f), .2f), Range(.8f, 1.1f) * s, Alpha(EarthTeal, .45f), new Vector2(Range(-1.5f, 1.5f), Range(.3f, .8f)), Range(.7f, .95f), 116);
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

        private static void EarthPhantomBody(EarthPhantom rec, Clip clip, Vector2 rest) { clip.offset = rest; rec.body.Add(clip); rec.rest.Add(rest); }

        // The phantom's pose each frame: it slides out of the caster, bobs, lunges toward its target and faces it.
        private static void EarthPhantomPose(EarthPhantom rec, float t)
        {
            rec.clock = t;
            float slide = rec.slideFrom * Mathf.Pow(1 - Mathf.Clamp01(t / .24f), 3);
            float since = t - rec.lungeAt, lunge = since < 0 ? 0 : since < .07f ? since / .07f : Mathf.Exp(-(since - .07f) * 9);
            Vector2 shift = new Vector2(slide + rec.facing * .3f * lunge, Mathf.Sin(t * 2.6f) * .04f);
            int mirror = rec.facing * rec.copyFacing;
            for (int i = 0; i < rec.body.Count; i++)
            {
                var part = rec.body[i]; if (ReferenceEquals(part.external, null) || !part.root) continue;
                part.offset = rec.rest[i] + shift; part.scale.x = Mathf.Abs(part.scale.x) * mirror;
                part.root.transform.localPosition = part.offset;
            }
        }

        /// <summary>A frozen copy of an actor's art (sprites and weighted meshes) with the actor's feet at <paramref name="at"/>.
        /// Mode 0 is a dark alpha ink, 1 additive light, 2 an additive rim drawn behind the ink. Rides <paramref name="parent"/>
        /// (at a local offset), or moves freely in the world when <paramref name="free"/>. Null when there is nothing to copy.</summary>
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
            // Light keeps the actor's own orders; the ink sits just below all of them and the rim below the ink.
            int spanOrders = high - low, shift = mode == 1 ? 0 : mode == 0 ? -spanOrders - 1 : -2 * spanOrders - 2;
            var root = new GameObject(mode == 0 ? "Earth phantom ink" : mode == 1 ? "Earth phantom light" : "Earth phantom rim");
            root.transform.SetParent(parent, false);
            if (free) root.transform.position = new Vector3(at.x, at.y, 0); else root.transform.localPosition = at;
            Vector3 origin = actor.position;
            var copies = new List<SpriteRenderer>(); var meshCopies = new List<MeshRenderer>(); var owned = new List<Mesh>();
            foreach (var source in sprites)
            {
                var part = new GameObject("Phantom " + source.name); part.transform.SetParent(root.transform, false);
                part.transform.localPosition = source.transform.position - origin; part.transform.localRotation = source.transform.rotation; part.transform.localScale = source.transform.lossyScale;
                var copy = part.AddComponent<SpriteRenderer>(); copy.sprite = source.sprite; copy.flipX = source.flipX; copy.flipY = source.flipY;
                copy.sharedMaterial = mode == 0 ? AlphaBlend : Additive; copy.sortingLayerID = source.sortingLayerID; copy.sortingOrder = source.sortingOrder + shift;
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
                for (int i = 0; i < materials.Length; i++) tinted[i] = mode == 0 ? EarthInkMaterial(materials[i]) : GhostMaterial(materials[i]);
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
            if (material != null && (material == additiveMaterial || material == alphaMaterial || ghostMaterials.ContainsValue(material) || EarthInkMaterials.ContainsValue(material))) return false;
            return !source.gameObject.name.Contains("Shadow");
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
            // The plum ink sits behind the actors so it never stains the caster.
            var palmInk = Anchored(c.anchor, hand, "Earth mine palm ink", glow, 1.8f, Alpha(EarthPlum, .5f), w + .06f, 28, false);
            var palm = Anchored(c.anchor, hand, "Earth mine palm", glow, 1.4f, Alpha(pink, .7f), w + .06f, 124);
            foreach (var x in new[] { palmInk, palm }) { x.fadeIn = .05f; x.startScale = .35f; x.endScale = 1.1f; x.ease = 1.5f; x.hold = .8f; x.fadePower = 2; }
            palm.pulse = .08f;
            // The seals form whirling round the hand, one for each mine.
            for (int i = 0; i < n; i++)
            {
                for (int layer = 0; layer < 2; layer++)
                {
                    bool lit = layer == 1;
                    var token = Spawn(lit ? "Earth mine token" : "Earth mine token ink", EarthSeal, hand, lit ? .55f : .6f, lit ? Alpha(pink, .95f) : EarthPlum, w, lit, lit ? 129 : 119);
                    token.orbitCenter = hand; token.orbitAngle = i * 360f / n; token.orbit = 400 * side; token.orbitRadius = .7f; token.orbitRadiusEnd = .3f; token.orbitPower = .7f;
                    token.spin = 300; token.startScale = .2f; token.endScale = 1; token.ease = 2; token.fadeIn = .08f; token.hold = .85f; token.fadePower = 2;
                }
            }
            Converge(hand, 8, 1.6f, pink, w, .5f);
            var sigilFloor = GroundSigil(ground, 2.4f, Alpha(pink, .7f), total, 90); sigilFloor.hold = hold;
            var floorInk = Spawn("Earth mine floor ink", sigil, ground, 2.5f, EarthPlum, total, false, 25); LayFlat(floorInk, .36f);
            floorInk.spin = 90; floorInk.fadeIn = .12f; floorInk.startScale = .85f; floorInk.endScale = 1.05f; floorInk.ease = 1; floorInk.hold = hold; floorInk.fadePower = 1.5f;
            // Release: a flick of light at the hand as the seals are thrown.
            var flash = EarthLater(Flash(hand, 1.6f, Alpha(pink, .85f), .14f), release); flash.ramp = Ramp(Color.white, pink);
            var star = EarthLater(Glint(hand, 1.6f, Color.white, .18f), release); star.spin = 0;
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
            var clock = Attach(root, "Earth mine clock", core, .05f, Color.clear, 0);
            var plane = EarthPlane(root, new Vector2(0, -.58f), .32f);
            var sealInk = Attach(plane, "Earth mine seal ink", EarthSeal, size * 1.12f, EarthPlum, 25, false);
            var seal = Attach(plane, "Earth mine seal", EarthSeal, size, Alpha(EarthPink, .45f), 27);
            var pool = Attach(plane, "Earth mine glow", glow, size * 1.8f, Alpha(EarthPink, .25f), 26);
            var beacon = Attach(root, "Earth mine beacon", pillar, .5f, Alpha(EarthPink, .15f), 121, true, new Vector2(0, -.6f));
            beacon.scale.y *= .55f; beacon.root.transform.localScale = beacon.scale;
            var heart = Attach(root, "Earth mine heart", core, .24f, Alpha(EarthPinkHot, .6f), 122, true, new Vector2(0, -.52f));
            var layers = new List<Clip> { sealInk, seal, pool, beacon, heart };
            if (g >= 3) { var crown = Attach(plane, "Earth mine crown", runeRing, size * 1.5f, Alpha(EarthGold, .55f), 27); crown.spin = -60; layers.Add(crown); }
            sealInk.spin = seal.spin = 35;
            float pop = EarthPop(span, .1f);
            foreach (var x in layers) { x.life = span; x.loop = false; x.fadePower = 0; x.startScale = 1.6f; x.endScale = 1; x.ease = pop; EarthLater(x, land); }
            clock.life = span; clock.loop = false;

            // The landing: a flash, a puff of dust and a small ring where the seal slams down.
            var thud = EarthLater(Flash(ground + Vector2.up * .12f, 1.4f, Alpha(EarthPink, .8f), .12f), land); thud.ramp = Ramp(Color.white, EarthPink);
            EarthLater(EarthRing(ground + Vector2.up * .03f, .75f, Alpha(EarthPinkHot, .85f), .25f, true, 116, .32f), land);
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? 1 : -1;
                var dust = EarthLater(Puff(ground + new Vector2(s * .2f, .12f), Range(.45f, .6f), EarthDustTone, new Vector2(s * Range(1, 1.8f), Range(.2f, .5f)), Range(.4f, .55f), 118), land);
                dust.endScale = 1.8f;
            }

            // Armed at 0.35 s: the seal locks in and brightens; then it blinks slowly, sending up a mote with each beat.
            bool armed = false; int beat = -1;
            EarthEachFrame(clock, owner =>
            {
                float t = owner.age;
                if (t < .35f)
                {
                    float nervous = .5f + .5f * Mathf.Sin(t * 60);
                    seal.color = Alpha(EarthPink, .3f + .2f * nervous); heart.color = Alpha(EarthPinkHot, .3f + .3f * nervous);
                    return;
                }
                if (!armed) { armed = true; if (root) EarthMineArm((Vector2)root.position + Vector2.down * .6f, size, R, g); }
                float phase = (t - .35f) * 1.25f, b = Mathf.Pow(.5f + .5f * Mathf.Cos(phase * 2 * Mathf.PI), 2);
                seal.color = Alpha(EarthPink, .55f + .45f * b);
                pool.color = Alpha(EarthPink, .18f + .32f * b);
                heart.color = Alpha(EarthPinkHot, .45f + .55f * b);
                beacon.color = Alpha(EarthPink, .1f + .3f * b);
                int cycle = Mathf.FloorToInt(phase);
                if (cycle != beat && root)
                {
                    beat = cycle;
                    Vector2 floorAt = (Vector2)root.position + Vector2.down * .6f;
                    var pulse = EarthRing(floorAt + Vector2.up * .03f, size * .7f, Alpha(EarthPink, .7f), .5f, true, 27, .32f); pulse.startScale = .4f;
                    var mote = Spawn("Earth mine mote", glow, floorAt + new Vector2(Range(-.25f, .25f), .1f), Range(.12f, .2f), Alpha(EarthPinkHot, .95f), Range(.7f, 1), true, 126);
                    mote.velocity = new Vector2(Range(-.15f, .15f), Range(.8f, 1.4f)); mote.drag = .4f; mote.fadeIn = .1f; mote.flicker = .3f; mote.endScale = .3f; mote.ease = 1;
                }
            });
            Bundle(root, clock, layers.ToArray());
        }

        // The seal arming: a ring snaps down onto it, a flash and a star, and a thin ring runs out to the blast radius once.
        private void EarthMineArm(Vector2 ground, float size, float R, int g)
        {
            var snap = Spawn("Earth mine lock", ring, ground + Vector2.up * .03f, size * 2, Alpha(EarthPinkHot, .9f), .14f, true, 27);
            EarthSquash(snap, .32f); snap.startScale = 1; snap.endScale = .45f; snap.ease = 2; snap.fadePower = .8f;
            var flash = Flash(ground + Vector2.up * .15f, 1.2f, Alpha(EarthPink, .75f), .14f); flash.ramp = Ramp(Color.white, EarthPink);
            EarthLater(Glint(ground + Vector2.up * .2f, 1.1f, Alpha(g >= 3 ? EarthGoldHot : Color.white, .9f), .2f, 0), .1f).spin = 0;
            // The blast reach runs out once (from level two the grade's own edge ring already shows it the whole time).
            if (g >= 2) return;
            var reach = EarthRing(ground + Vector2.up * .03f, R, Alpha(EarthPink, .55f), .5f, true, 116, .32f); reach.startScale = .3f;
            var reachInk = EarthRing(ground, R * 1.01f, Alpha(EarthPlum, .45f), .5f, false, 24, .34f); reachInk.startScale = .3f;
        }

        // A seal thrown from the hand in an arc, spinning, landing after <paramref name="time"/> seconds.
        private void EarthToss(Vector2 from, Vector2 to, float time, float size, int side)
        {
            const float Gravity = 22;
            Vector2 v = new Vector2((to.x - from.x) / time, (to.y - from.y) / time + .5f * Gravity * time);
            var lit = Spawn("Earth mine thrown", EarthSeal, from, size * .55f, Alpha(EarthPink, .95f), time, true, 129);
            var ink = Spawn("Earth mine thrown ink", EarthSeal, from, size * .6f, EarthPlum, time, false, 119);
            foreach (var x in new[] { lit, ink }) { x.velocity = v; x.gravity = Gravity; x.spin = -side * 720; x.fadePower = 0; x.startScale = .7f; x.endScale = 1; x.ease = 1; }
            Emitter(lit, Emit.Trace, 50, .05f, Alpha(EarthPink, .5f));
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

        /// <summary>One mine's blast: the lead of a chain draws it in full, the followers keep the flash, the floor rings on the
        /// true radius, a fire column and a few shards.</summary>
        private void EarthMineBlast(Vector2 pos, float R, float s, Color pink, Color hot, int g, bool lead)
        {
            Vector2 ground = pos + Vector2.down * .3f, body = ground + Vector2.up * .6f;
            var heart = Spawn("Earth mine blast heart", core, ground + Vector2.up * .35f, .9f * s, lead ? Color.white : Alpha(EarthPinkHot, .5f), .1f, true, 133); heart.startScale = .5f; heart.endScale = 1.6f; heart.ease = 2.5f; heart.fadePower = 1.5f;
            var bloom = Flash(body, 2.6f * s, Alpha(pink, lead ? .7f : .45f), .26f); bloom.ramp = Ramp(new Color(1, .9f, .97f), pink);
            if (lead)
            {
                var shade = Underlay(body, 3.2f * s, .32f, .45f); shade.hold = .2f;
                EarthInk(ImpactLines(body, 4.2f * s, EarthPlum, .3f), 118);
                ImpactLines(body, 3.6f * s, Alpha(hot, .75f), .26f);
                // The shock: a sphere ring on the true radius.
                Wave(ground + Vector2.up * .1f, R * 2 / .74f, Alpha(pink, .8f), .34f, 3);
            }
            // Rings along the floor on the true radius.
            EarthRing(ground + Vector2.up * .03f, R, Alpha(hot, lead ? .95f : .7f), .42f, true, 116, .32f);
            EarthRing(ground, R * 1.02f, EarthPlum, .5f, false, 24, .34f);
            // Rune fire: a broad column of magenta flame over a plum shadow, tongues of fire licking up through it.
            var column = Spawn("Earth mine fire column", glow, ground + Vector2.up * 1.1f * s, 1.2f * s, Alpha(pink, lead ? .55f : .4f), .4f, true, 127);
            column.scale.y *= 2.2f; column.root.transform.localScale = column.scale;
            column.velocity = Vector2.up * .8f; column.startScale = .45f; column.endScale = 1; column.ease = 3; column.hold = .15f; column.fadePower = 1.5f;
            if (lead)
            {
                var columnInk = Spawn("Earth mine fire shadow", glow, ground + Vector2.up * 1.1f * s, 1.7f * s, Alpha(EarthPlum, .45f), .45f, false, 117);
                columnInk.scale.y *= 1.8f; columnInk.root.transform.localScale = columnInk.scale;
                columnInk.startScale = .5f; columnInk.endScale = 1; columnInk.ease = 3; columnInk.hold = .15f; columnInk.fadePower = 1.4f;
            }
            int tongues = lead ? 3 + (g >= 2 ? 1 : 0) : 1;
            for (int i = 0; i < tongues; i++)
            {
                var tongue = Flame(ground + new Vector2(Range(-.35f, .35f) * s, .1f), Range(.8f, 1.1f) * s, new Vector2(Range(-.5f, .5f), Range(2.6f, 4)), Range(.36f, .5f), 126);
                tongue.color = new Color(1, .5f, .92f, .95f);
            }
            // The seal breaks: broken arcs of its ring fly, half lit and half plum.
            int pieces = lead ? 6 + (g >= 3 ? 2 : 0) : 3;
            for (int i = 0; i < pieces; i++)
            {
                bool lit = i % 2 == 0;
                var piece = Spawn("Earth seal shard", EarthShardSet[random.Next(EarthShardSet.Length)], ground + new Vector2(Range(-.3f, .3f), .15f), Range(.3f, .44f) * s, lit ? Alpha(hot, .95f) : EarthPlum, Range(.55f, .8f), lit, lit ? 129 : 119);
                piece.velocity = Direction(Range(20, 160)) * Range(4, 7.5f) * s; piece.gravity = 10; piece.drag = .8f; piece.spin = Range(-500, 500); piece.angle = Range(0, 360); piece.hold = .35f; piece.fadePower = 1.2f;
            }
            int sparks = lead ? 4 + g : 2;
            for (int i = 0; i < sparks; i++) Spark(ground + Vector2.up * .3f, Direction(Range(15, 165)) * Range(4, 8) * s, i % 2 == 0 ? Color.white : pink, Range(.22f, .36f), Range(.3f, .45f), 5, 2);
            Scorch(ground, 1.6f * s, 1.4f);
            if (!lead) return;
            var fume = Puff(body + new Vector2(Range(-.3f, .3f), Range(.2f, .6f)), Range(.9f, 1.2f) * s, new Color(.2f, .05f, .14f, .55f), new Vector2(Range(-.4f, .4f), Range(.8f, 1.4f)), Range(1, 1.2f), 115);
            fume.drag = 1.4f; fume.endScale = 2.3f; fume.fadeIn = .05f;
            var burn = Decal(ground, crack, 1.5f * s, Alpha(pink, .85f), .9f, true, 26); burn.angle = Range(-10, 10); burn.fadePower = 1.6f;
            if (g >= 3) Glint(body, 2.2f * s, Alpha(EarthGoldHot, .95f), .28f, 0).spin = 0;
        }

        // ---------------------------------------------------------------- shared helpers

        private static Clip EarthLater(Clip clip, float delay)
        {
            if (delay > 0) { clip.age = -delay; if (clip.renderer) clip.renderer.enabled = false; }
            return clip;
        }
        private static Clip EarthInk(Clip clip, int order) { clip.renderer.sharedMaterial = AlphaBlend; clip.renderer.sortingOrder = order; return clip; }
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

        /// <summary>A shock ring whose bright line ends exactly at <paramref name="radius"/> (squashed to lie on the floor below 1).</summary>
        private Clip EarthRing(Vector2 pos, float radius, Color color, float life, bool additive, int order, float squash = 1)
        {
            var clip = Spawn("Earth ring", ring, pos, radius * 2 / .74f, color, life, additive, order);
            if (squash != 1) EarthSquash(clip, squash);
            clip.startScale = .15f; clip.endScale = 1; clip.ease = 3; clip.fadePower = 1.3f; return clip;
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

        private static Sprite EarthSprite(string name, int width, int height, Color32[] pixels, Vector2 pivot)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "Tower effect " + name, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 2 };
            texture.SetPixels32(pixels); texture.Apply(true, false);
            return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private static void EarthQueueQuake()
        {
            // Rock fangs (bottom pivot): broken slabs of dark faceted stone with strata, a dark rim, a dark outline outside it
            // (so they keep their silhouette on lava and pale marble alike) and the glow of the rift on their roots, and a mask
            // of the fine molten veins in them.
            EarthFangRock = new Sprite[3]; EarthFangVein = new Sprite[3];
            for (int i = 0; i < 3; i++)
            {
                int variant = i;
                EarthJob("Earth fang " + i, 64, 192, new Vector2(.5f, 0), true, (float u, float v, out Color stone, out Color vein) =>
                {
                    float light, rim, heat, outline; float a = EarthFangShape(u, v, variant, out light, out rim, out heat, out outline);
                    var body = Color.Lerp(new Color(.11f, .065f, .04f), new Color(.6f, .43f, .28f), light);
                    body = Color.Lerp(body, new Color(.95f, .46f, .14f), Gauss((v + 1) * .5f, .1f) * .5f);
                    body = Color.Lerp(body, new Color(.05f, .03f, .02f), rim * .9f); body.a = a;
                    stone = Over(new Color(.07f, .035f, .015f, outline * .85f), body);
                    vein = White(heat);
                }, (rock0, light0) => { EarthFangRock[variant] = rock0; EarthFangVein[variant] = light0; });
            }
            // The rift strip: a low, irregular crack entering and leaving at mid-height of both ends, with side cracks, a wide
            // dark scorched lip, and its molten core as a light mask.
            EarthRiftInk = new Sprite[4]; EarthRiftLight = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                int variant = i;
                EarthJob("Earth rift " + i, 128, 64, new Vector2(.5f, .5f), true, (float u, float v, out Color ink, out Color lit) =>
                {
                    float heat, halo; float a = EarthRiftShape(u * 2, v, variant, out heat, out halo);
                    float fade = 1 - Smooth(.86f, 1, Mathf.Abs(v));
                    ink = Over(new Color(.2f, .09f, .03f, halo * .85f), new Color(.04f, .02f, .01f, a)); ink.a *= fade;
                    lit = White(Mathf.Clamp01(heat + halo * .12f) * fade);
                }, (ink0, light0) => { EarthRiftInk[variant] = ink0; EarthRiftLight[variant] = light0; });
            }
            // The eruption pillar (bottom pivot): a broken column of layered dark basalt with a dark outline, and its glowing seams.
            EarthJob("Earth column", 96, 288, new Vector2(.5f, 0), true, (float u, float v, out Color stone, out Color vein) =>
            {
                float light, rim, heat, outline; float a = EarthColumnShape(u, (v + 1) * 3, out light, out rim, out heat, out outline);
                var body = Color.Lerp(Color.Lerp(new Color(.1f, .07f, .05f), new Color(.45f, .34f, .25f), light), new Color(.05f, .03f, .02f), rim * .95f); body.a = a;
                stone = Over(new Color(.07f, .035f, .015f, outline * .85f), body);
                vein = White(heat);
            }, (rock0, light0) => { EarthColumnRock = rock0; EarthColumnVein = light0; });
            // A slab torn from the floor (centre pivot): a flat top face of paving, a body narrowing to a broken underside that
            // glows with the heat below, and a dark outline.
            EarthSingle("Earth slab", 96, 48, new Vector2(.5f, .5f), (u, v) =>
            {
                float top = .5f + (Mathf.PerlinNoise(u * 3 + 1, .7f) - .5f) * .12f, bottom = -.55f + (Mathf.PerlinNoise(u * 5 + 3, 2.1f) - .5f) * .6f;
                float across = Mathf.Clamp01((v - bottom) / Mathf.Max(.01f, top - bottom)), half = Mathf.Lerp(.6f, .9f, across) + (Mathf.PerlinNoise(v * 4 + 5, 1.1f) - .5f) * .1f;
                float edge = Mathf.Min(Mathf.Min((half - Mathf.Abs(u)) * 48, (top - v) * 24), (v - bottom) * 24);
                float inside = Smooth(-.5f, .8f, edge), outline = 1 - Smooth(1, 3.5f, -edge);
                float grain = Fbm(u * 3 + 2, v * 2.5f, 2);
                var body = Color.Lerp(new Color(.13f, .09f, .065f), new Color(.5f, .39f, .29f), grain);
                body = Color.Lerp(body, new Color(.74f, .64f, .52f), Smooth(4, 1, top - v < 0 ? 0 : (top - v) * 24) * .9f);
                body = Color.Lerp(body, new Color(1, .5f, .15f), Gauss((v - bottom) * 24, 4) * .85f);
                body = Color.Lerp(body, new Color(.05f, .03f, .02f), (1 - Smooth(.5f, 2, edge)) * .8f); body.a = inside;
                return Over(new Color(.07f, .035f, .015f, outline * .8f), body);
            }, sprite => EarthSlab = sprite);
        }

        // A broken slab of rock standing on its base (v = -1): wide at the root, tapering to a jagged break rather than a point.
        // The texture is 64 x 192, so a u unit is 32 pixels and an h unit 192.
        private static float EarthFangShape(float u, float v, int variant, out float light, out float rim, out float heat, out float outline)
        {
            float h = (v + 1) * .5f, seed = variant * 3.7f + 1.3f;
            float axis = (variant - 1) * .14f * h * h + (Mathf.PerlinNoise(h * 2.3f + seed, 1.3f) - .5f) * .16f * h;
            float jag = Mathf.PerlinNoise(h * 9 + seed, 7.1f) - .5f;
            float notch = Smooth(.75f, 1, Mathf.Repeat(h * (3.2f + variant * 1.1f) + seed, 1)) * .12f;
            float half = (.94f - .64f * Mathf.Pow(h, 1.15f)) * (1 + jag * .26f - notch);
            float x = u - axis, ax = Mathf.Abs(x);
            float crown = .9f + .07f * (Mathf.Abs(Mathf.Repeat(x * 1.7f + seed, 1) - .5f) * 2 - .5f) - .05f * x * (variant == 1 ? -1 : 1);
            float inside = (1 - Smooth(half - .07f, half, ax)) * Smooth(0, .015f, h) * (1 - Smooth(crown - .012f, crown, h));
            float edgePixels = Mathf.Min((half - ax) * 32, (crown - h) * 192);
            rim = 1 - Smooth(1.5f, 7, edgePixels);
            outline = (1 - Smooth(1, 4.5f, -edgePixels)) * Smooth(0, .03f, h);
            float ridge = half * (.1f + .45f * (Mathf.PerlinNoise(h * 2.6f + seed, 2.2f) - .5f));
            float face = x < ridge ? .95f : .5f;
            if (crown - h < .07f) face = Mathf.Max(face, .8f);
            float grain = Fbm(u * 2.4f + seed, v * 4.2f + seed, 2);
            float strata = Gauss(Mathf.Repeat(h * 4.6f + grain * .8f + seed, 1) - .5f, .06f);
            light = Mathf.Clamp01(face * (.68f + grain * .6f) - strata * .3f);
            float path = axis + half * .45f * (Mathf.Abs(Mathf.Repeat(h * 2.4f + seed, 1) - .5f) * 2 - .5f);
            heat = Mathf.Clamp01((Gauss(u - path, .07f) * Mathf.Pow(Mathf.Max(0, 1 - h), .9f) + Gauss(h, .15f) * .4f + strata * .2f * (1 - h)) * inside);
            return inside;
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
        private static float EarthRiftShape(float X, float Y, int variant, out float heat, out float halo)
        {
            float t = (X + 2) * .25f, env = Mathf.Sin(t * Mathf.PI);
            float width = .17f + .15f * Mathf.Sqrt(Mathf.Max(0, env)) * (.7f + .6f * Mathf.PerlinNoise(t * 6 + variant * 4, 2.5f));
            float d = Mathf.Abs(Y - EarthRiftPath(X, variant));
            float cover = 1 - Smooth(width * .55f, width, d);
            heat = 1 - Smooth(0, width * .7f, d);
            halo = Gauss(d, width * 3.2f);
            var ends = EarthRiftBranches[variant];
            for (int b = 0; b < 3; b++)
            {
                float bx = ends[b * 4], by = ends[b * 4 + 1], ex = ends[b * 4 + 2], ey = ends[b * 4 + 3];
                float dx = ex - bx, dy = ey - by;
                float along = Mathf.Clamp01(((X - bx) * dx + (Y - by) * dy) / (dx * dx + dy * dy));
                float sd = SegmentDistance(X, Y, bx, by, ex, ey), bw = Mathf.Lerp(width * .55f, .02f, along);
                cover = Mathf.Max(cover, 1 - Smooth(bw * .5f, bw, sd));
                heat = Mathf.Max(heat, (1 - Smooth(0, bw * .7f, sd)) * (1 - along) * .75f);
                halo = Mathf.Max(halo, Gauss(sd, bw * 2.2f) * (1 - along * .6f) * .7f);
            }
            return cover;
        }

        // The eruption column at (X, H): X in -1..1, H in 0..6 (square pixels).
        private static float EarthColumnShape(float X, float H, out float light, out float rim, out float heat, out float outline)
        {
            float jag = Mathf.PerlinNoise(H * 1.7f + 3, 8.3f) - .5f;
            float half = .74f - .04f * H + jag * .14f;
            float top = 5.2f + (Mathf.Abs(Mathf.Repeat(X * 1.9f + .3f, 1) - .5f) * 2 - .5f) * .9f + (Mathf.PerlinNoise(X * 3 + 1, 4) - .5f) * .5f;
            float ax = Mathf.Abs(X);
            float inside = (1 - Smooth(half - .05f, half, ax)) * (1 - Smooth(top - .06f, top, H)) * Smooth(0, .05f, H);
            float edge = Mathf.Min(half - ax, top - H);
            rim = 1 - Smooth(.02f, .14f, edge);
            outline = (1 - Smooth(.8f, 4, -edge * 48)) * Smooth(0, .05f, H);
            float ridge = half * .22f + (Mathf.PerlinNoise(H * .9f, 5) - .5f) * .2f;
            float face = X < ridge ? .9f : .5f;
            float grain = Fbm(X * 2.2f + 7, H * 1.1f, 2);
            float strata = Gauss(Mathf.Repeat(H * 1.35f + X * .12f + grain * .5f, 1) - .5f, .05f);
            light = Mathf.Clamp01(face * (.7f + grain * .55f) - strata * .3f + Gauss(top - H, .25f) * .15f);
            float fault = Mathf.Abs(X - (Mathf.Abs(Mathf.Repeat(H * .8f + .2f, 1) - .5f) * 2 - .5f) * .45f);
            heat = Mathf.Clamp01((Gauss(fault, .07f) * (1 - Smooth(1, 5, H)) + strata * .55f * (1 - Smooth(.5f, 4, H)) + Gauss(H, .35f) * .45f) * inside);
            return inside;
        }

        /// <summary>One texture shaded by <paramref name="shade"/>, queued for baking.</summary>
        private static void EarthSingle(string name, int width, int height, Vector2 pivot, Shade shade, Action<Sprite> store)
        {
            EarthJob(name, width, height, pivot, false, (float u, float v, out Color first, out Color second) => { first = shade(u, v); second = Color.clear; }, (sprite, unused) => store(sprite));
        }

        private static void EarthQueueOrbit()
        {
            // A glassy sphere lit from the upper left: deep navy shadow, azure body, a white highlight, light bouncing up the
            // lower rim and a dark outline, so it reads as a solid ball on any sky.
            EarthSingle("Earth sphere", 96, 96, new Vector2(.5f, .5f), (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float inside = 1 - Smooth(.86f, .92f, r), n = Mathf.Clamp01(r / .86f), z = Mathf.Sqrt(Mathf.Max(0, 1 - n * n));
                float lambert = Mathf.Clamp01((-u * .5f + v * .6f) * .75f + z * .55f);
                Color c = Color.Lerp(new Color(.04f, .14f, .48f), new Color(.45f, .8f, 1), lambert);
                c = Color.Lerp(c, new Color(.62f, .92f, 1), Mathf.Pow(n, 5) * (v < 0 ? .55f : .2f));
                c = Color.Lerp(c, Color.white, Gauss(Mathf.Sqrt(Sq(u + .3f) + Sq(v - .34f)), .13f) * .9f);
                c = Color.Lerp(c, new Color(.02f, .05f, .2f), Smooth(.74f, .88f, r) * .75f);
                c.a = inside; return c;
            }, sprite => EarthSphere = sprite);
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
            // The seal: a double ring with rune ticks, an eight-point star and an eye at its heart.
            EarthSingle("Earth seal", 192, 192, new Vector2(.5f, .5f), (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float deg = Mathf.Repeat(Mathf.Atan2(v, u) * Mathf.Rad2Deg, 360);
                float a = Gauss(r - .9f, .028f) + Gauss(r - .79f, .014f) * .8f + Gauss(r - .27f, .016f) * .7f;
                float star = 1e9f;
                for (int i = 0; i < 8; i++)
                {
                    float a0 = i * 45 * Mathf.Deg2Rad, a1 = (i + 3) * 45 * Mathf.Deg2Rad;
                    star = Mathf.Min(star, SegmentDistance(u, v, Mathf.Cos(a0) * .79f, Mathf.Sin(a0) * .79f, Mathf.Cos(a1) * .79f, Mathf.Sin(a1) * .79f));
                }
                a += Gauss(star, .016f) * .8f;
                float cell = deg / 22.5f, local = cell - Mathf.Floor(cell); uint h = Hash((int)cell * 13 + 5);
                if (r > .81f && r < .88f) a += ((h & 1u) != 0 ? Gauss(local - .5f, .08f) : Gauss(local - .3f, .06f) + Gauss(local - .7f, .06f)) * .8f;
                float d1 = Mathf.Sqrt(u * u + Sq(v + .195f)) - .325f, d2 = Mathf.Sqrt(u * u + Sq(v - .195f)) - .325f;
                a += Gauss(Mathf.Max(d1, d2), .018f) * .95f + (1 - Smooth(.06f, .08f, r));
                a += (1 - Smooth(.1f, .9f, r)) * .08f;
                return White(Mathf.Clamp01(a) * (1 - Smooth(.95f, .995f, r)));
            }, sprite => EarthSeal = sprite);
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
