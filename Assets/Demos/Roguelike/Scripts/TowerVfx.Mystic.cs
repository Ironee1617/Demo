using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Mystic arts: the gathering vortex, the creeping curse, the war banner and the blooming seed.
    // Vortex: a sea-teal whirlpool turns on the floor across the area while a funnel of wind arcs and water droplets spirals
    // inward around the gathered enemies (droplets pass behind and in front of them); gusts stream in from the edge of the
    // pull, and the end collapses into a geyser of spray.
    // Curse: a violet brand gathers in the hand, an ink-dark tendril creeps to the victim and an eye sigil is stamped on it.
    // WarBanner: a crimson-and-gold standard drops into the ground behind the caster; its cloth ripples (sliced into strips
    // that follow a travelling wave and catch the light on each fold), a golden seal marks the reach of its blessing, and
    // every second a pulse ring sweeps out to that edge and flares there.
    // Bloom: a seed of light is lobbed onto the enemy; when it blooms a flower unfolds, loosens and scatters, healing motes
    // rise and a flower seal opens across the strike radius.
    // Shapes pair a saturated alpha body or dark ink layer (reads on pale skies) with additive light (reads in the dark).
    // Higher levels grow height, density and secondary layers; ground rings always match the gameplay radius.
    public sealed partial class TowerVfx
    {
        private static Sprite MysticSpiral, MysticArc, MysticVeil, MysticFunnel, MysticStroke, MysticBrand, MysticPole, MysticCrest, MysticBloomSeal, MysticPetal;
        private static Sprite[] MysticFlagStrips, MysticFlagGlowStrips;
        private const int MysticStripCount = 24;
        private const float MysticFlagAspect = 288f / 176f;

        private static readonly Color MysticSea = new Color(.36f, .9f, .95f), MysticFoam = new Color(.86f, 1, .98f),
            MysticDeep = new Color(0, .34f, .44f, .85f), MysticAbyss = new Color(0, .07f, .13f, .85f),
            MysticHex = new Color(.74f, .4f, 1), MysticHexHot = new Color(.95f, .84f, 1), MysticHexBody = new Color(.34f, .03f, .56f, .9f), MysticHexInk = new Color(.05f, 0, .08f, .9f),
            MysticGold = new Color(1, .8f, .36f), MysticGoldHot = new Color(1, .96f, .82f), MysticBronze = new Color(.4f, .18f, .02f, .85f),
            MysticPink = new Color(1, .62f, .84f), MysticPinkHot = new Color(1, .93f, .97f), MysticRose = new Color(.84f, .16f, .48f, .92f),
            MysticLeaf = new Color(.16f, .55f, .24f, .92f), MysticSap = new Color(.55f, 1, .62f);

        // A droplet of the vortex funnel: it spirals in and up along a flattened circle, passing behind and in front of the actors.
        private sealed class MysticVortexMote { public Clip clip; public Color tone; public float angle, phase, rate, turn, height, lift, unit; public bool ink; }
        // A planted banner: the cloth strips follow a travelling wave every frame and the pulses make the crest flare.
        private sealed class MysticBannerRig
        {
            public Transform root; public Clip pole, poleGlow, crest, crestGlow, halo; public Clip[] cloth, shine;
            public float height, poleX, flagW, flagH, crestLift, phase, shineGain, clock, beatAt = -9; public int side;
        }
        private readonly List<MysticBannerRig> MysticBanners = new List<MysticBannerRig>();
        // A banner being raised. Gameplay plants it a moment later (beside the hero, even when a companion raises it), so each
        // zone takes the facing of the call standing where it lands, or else of the oldest call still waiting.
        private struct MysticBannerCall { public int frame; public float x; public int facing; }
        private readonly List<MysticBannerCall> MysticBannerCalls = new List<MysticBannerCall>();
        // The last curse tendril and seed throw (end and travel time), so marks are stamped when they arrive (MysticTrailArrival).
        private int MysticCurseFrame = -1; private Vector2 MysticCurseEnd; private float MysticCurseTravel;
        private int MysticSeedFrame = -1; private Vector2 MysticSeedEnd; private float MysticSeedTravel;
        // Seeds in flight: where each was thrown from, so a level-three bloom can send its second wave of life back that way.
        private struct MysticSeedThrow { public int frame; public Vector2 from, to; }
        private readonly List<MysticSeedThrow> MysticSeedThrows = new List<MysticSeedThrow>();
        // Brands and blooms landing in one frame (one cast on several foes) share the first one's flash and rings.
        private int MysticCurseStampFrame = -1, MysticBloomFrame = -1; private Vector2 MysticBloomAt;

        /// <summary>Seconds until a curse tendril or seed drawn this frame reaches <paramref name="at"/>, or 0 when none is on
        /// its way there. Marks of the arrival (the brand, a status look, a grade overlay) can start then instead of before it.</summary>
        private float MysticTrailArrival(SkillEffect effect, Vector2 at)
        {
            int frame = Time.frameCount;
            if (effect == SkillEffect.Curse && MysticCurseFrame == frame && Mathf.Abs(MysticCurseEnd.x - at.x) < .6f && Mathf.Abs(MysticCurseEnd.y - at.y) < 1.8f) return MysticCurseTravel;
            if (effect == SkillEffect.Bloom && MysticSeedFrame == frame && Mathf.Abs(MysticSeedEnd.x - at.x) < .6f && Mathf.Abs(MysticSeedEnd.y - at.y) < 1.8f) return MysticSeedTravel;
            return 0;
        }

        // ---------------------------------------------------------------- Vortex

        private void CastVortex(CastInfo c)
        {
            MysticEnsure();
            int g = Grade; float k = Mathf.Clamp(c.size / 1.4f, .8f, 1.6f) * (1 + .1f * (g - 1)), w = c.windup, release = Mathf.Max(0, w - .02f);
            Vector2 hand = c.pos + new Vector2(c.facing * .62f, -.02f), ground = c.pos + Vector2.down;
            float handLife = w + .12f, hold = w / handLife;

            // A small whirlpool turns in the open hand: dark water behind the caster, a teal body, light arms and a white heart.
            var ink = Anchored(c.anchor, hand, "Mystic vortex hand ink", MysticSpiral, 1.6f * k, Alpha(MysticAbyss, .6f), handLife, 29, false); ink.spin = 420;
            var body = Anchored(c.anchor, hand, "Mystic vortex hand body", MysticSpiral, 1.3f * k, MysticDeep, handLife, 119, false); body.spin = 520; body.angle = 50;
            var lit = Anchored(c.anchor, hand, "Mystic vortex hand light", MysticSpiral, 1.25f * k, Alpha(MysticSea, .9f), handLife, 126); lit.spin = 620; lit.angle = 110;
            var heart = Anchored(c.anchor, hand, "Mystic vortex hand heart", core, .36f * k, Alpha(MysticFoam, .95f), handLife, 127); heart.pulse = .1f;
            foreach (var x in new[] { ink, body, lit, heart }) { x.fadeIn = .04f; x.startScale = .3f; x.endScale = 1; x.ease = 1.5f; x.hold = hold; x.fadePower = 2; }
            if (w < .2f)
            {
                // An instant echo: the vortex itself opens at once, so the echo only spins the hand.
                Converge(hand, 6, 1.6f * k, MysticSea, w, .6f);
                return;
            }
            var shade = Underlay(hand, 3 * k, .3f, w + .2f); shade.hold = .7f;
            Converge(hand, 12, 2.4f * k, MysticSea, w, .7f);
            // Currents of water whirl into the hand.
            int currents = Budget(6);
            for (int i = 0; i < currents; i++)
            {
                bool dark = i % 2 == 1;
                var arc = Spawn(dark ? "Mystic gathering current ink" : "Mystic gathering current", MysticArc, hand, Range(1.3f, 2.1f) * k, dark ? Alpha(MysticDeep, .7f) : Alpha(i % 4 == 0 ? MysticFoam : MysticSea, .9f), w * Range(.8f, 1), !dark, dark ? 120 : 127);
                arc.spin = Range(520, 720); arc.angle = Range(0, 360); arc.startScale = 1.3f; arc.endScale = .3f; arc.ease = 1.3f; arc.fadeIn = .08f; arc.hold = .65f; arc.fadePower = 1.6f;
            }
            // A whirl opens on the floor under the caster, and a faint one gathers where the vortex will open.
            float total = w + .15f;
            var floorInk = Spawn("Mystic vortex floor ink", MysticSpiral, ground, 2.6f * k, Alpha(MysticDeep, .6f), total, false, 25); LayFlat(floorInk, .32f); floorInk.spin = 300;
            var floorLit = Spawn("Mystic vortex floor", MysticSpiral, ground, 2.4f * k, Alpha(MysticSea, .7f), total, true, 27); LayFlat(floorLit, .32f); floorLit.spin = 360;
            foreach (var x in new[] { floorInk, floorLit }) { x.fadeIn = .08f; x.startScale = .3f; x.endScale = 1; x.ease = 2; x.hold = w / total; x.fadePower = 1.6f; }
            float reach = Mathf.Clamp(c.radius / .3f, 1, 4);
            Vector2 site = c.target + Vector2.down * .7f;
            var preview = Spawn("Mystic vortex preview", MysticSpiral, site, reach * 1.6f, Alpha(MysticSea, .55f), w + .06f, true, 27); LayFlat(preview, .32f);
            preview.spin = 420; preview.fadeIn = w * .6f; preview.startScale = .2f; preview.endScale = .9f; preview.ease = 1.5f; preview.hold = .85f; preview.fadePower = 2;
            var previewInk = Spawn("Mystic vortex preview ink", MysticSpiral, site, reach * 1.7f, Alpha(MysticDeep, .45f), w + .06f, false, 25); LayFlat(previewInk, .32f);
            previewInk.spin = 380; previewInk.fadeIn = w * .6f; previewInk.startScale = .2f; previewInk.endScale = .9f; previewInk.ease = 1.5f; previewInk.hold = .85f; previewInk.fadePower = 2;
            // Release: the hand flicks the whirl away.
            var flick = MysticLater(Flash(hand, 2 * k, Alpha(MysticFoam, .85f), .2f), release); flick.ramp = Ramp(Color.white, MysticSea);
            MysticLater(Wave(hand, 2.6f * k, Alpha(MysticSea, .85f), .3f, 3), release);
            MysticLater(MysticRing(hand, 2.8f * k, Alpha(MysticDeep, .7f), .3f, false, 119), release);
        }

        private void ZoneVortex(Transform root, float radius, float life)
        {
            MysticEnsure();
            int g = Grade; bool awake = g >= 3;
            float R = Mathf.Clamp(radius, .6f, 5), H = 2.2f + .45f * (g - 1), disc = R * 2;
            Vector2 center = root.position, ground = center + Vector2.down * .6f;
            var layers = new List<Clip>(); var planes = new List<Transform>(); var squash = new List<float>();

            // A dark well behind everything, so the teal water reads on a pale sky.
            var shade = Attach(root, "Mystic vortex shade", glow, R * 3.4f, new Color(.01f, .03f, .05f, .3f), 24, false, new Vector2(0, .5f));
            shade.scale.y *= .85f;

            // The whirlpool on the floor: dark water, a teal body and light arms turning inward around a black eye, and a rim
            // exactly on the edge of the area.
            var floorPlane = MysticPlane(root, new Vector2(0, -.6f), .32f); planes.Add(floorPlane); squash.Add(.32f);
            var pitInk = Attach(floorPlane, "Mystic whirlpool ink", MysticSpiral, disc * 1.1f, Alpha(MysticAbyss, .7f), 25, false); pitInk.spin = 160;
            var pit = Attach(floorPlane, "Mystic whirlpool body", MysticSpiral, disc, MysticDeep, 26, false); pit.spin = 210; pit.angle = 40;
            var pitLight = Attach(floorPlane, "Mystic whirlpool light", MysticSpiral, disc * .97f, Alpha(MysticSea, .85f), 27); pitLight.spin = 250; pitLight.angle = 95;
            var eye = Attach(floorPlane, "Mystic whirlpool eye", glow, R * .85f, Alpha(MysticAbyss, .9f), 28, false);
            layers.AddRange(new[] { pitInk, pit, pitLight, eye });
            // The rim on the true edge of the pull area. From level two the grade overlay lays its own lit edge (and at three its
            // rune seal) there, so only the ink stays, on the overlay's plane, and the two never part into a double line.
            Clip rimInk;
            if (g == 1)
            {
                rimInk = Attach(floorPlane, "Mystic whirlpool rim ink", ring, disc / .74f * 1.02f, Alpha(MysticDeep, .75f), 25, false);
                var rim = Attach(floorPlane, "Mystic whirlpool rim", ring, disc / .74f, Alpha(MysticSea, .85f), 27); rim.pulse = .025f;
                layers.Add(rim);
            }
            else
            {
                var edgePlane = MysticPlane(root, new Vector2(0, -.57f), .3f); planes.Add(edgePlane); squash.Add(.3f);
                rimInk = Attach(edgePlane, "Mystic whirlpool rim ink", ring, disc / .74f * 1.02f, Alpha(MysticDeep, .75f), 25, false);
                var froth = Attach(floorPlane, "Mystic whirlpool froth", MysticSpiral, disc * .68f, Alpha(MysticFoam, .55f), 27); froth.spin = 380; froth.angle = 200; layers.Add(froth);
            }
            layers.Add(rimInk);

            // The body of the funnel: a cone of dark water behind the actors, narrow at the floor and opening upward, with a
            // sea-glass sheen on its walls in front of them, so the arcs read as one whirling mass rather than loose rings.
            var conePlane = MysticPlane(root, new Vector2(0, -.6f), 1); planes.Add(conePlane); squash.Add(1);
            var coneInk = Attach(conePlane, "Mystic funnel body", MysticFunnel, 2 * R * .95f, Alpha(MysticDeep, .38f), 28, false); MysticStretch(coneInk, 2 * R * .95f, H + .55f);
            var coneSheen = Attach(conePlane, "Mystic funnel sheen", MysticFunnel, 2 * R * .92f, Alpha(MysticSea, .2f), 122); MysticStretch(coneSheen, 2 * R * .92f, H + .5f);
            coneSheen.pulse = .03f; layers.Add(coneInk); layers.Add(coneSheen);

            // The funnel: rings of wind arcs, narrow at the floor and widening upward, turning faster near the bottom. Dark
            // arcs pass behind the actors, light and spray arcs in front.
            int bands = 4 + g;
            for (int j = 0; j < bands; j++)
            {
                float t = j / (float)(bands - 1), h = .25f + t * H, r = R * Mathf.Lerp(.3f, .92f, Mathf.Pow(t, .8f));
                var plane = MysticPlane(root, new Vector2(0, -.6f + h), .24f); planes.Add(plane); squash.Add(.24f);
                float width = 2 * r / .86f, turn = Mathf.Lerp(600, 320, t), start = j * 67 + Range(0, 40);
                bool top = j == bands - 1;
                var back = Attach(plane, "Mystic funnel water wall", MysticVeil, width * 1.06f, Alpha(MysticDeep, .72f), 29, false); back.angle = start; back.spin = turn;
                var front = Attach(plane, "Mystic funnel gust", MysticArc, width, Alpha(Color.Lerp(MysticSea, MysticFoam, t * .5f + (top ? .3f : 0)), .88f), 125); front.angle = start + 180; front.spin = turn;
                layers.Add(back); layers.Add(front);
                if (j % 2 == 1 || g >= 2) { var spray = Attach(plane, "Mystic funnel spray", MysticArc, width * .94f, Alpha(MysticFoam, .7f), 126); spray.angle = start + 95; spray.spin = turn * 1.1f; layers.Add(spray); }
                // Awakened: a fine gold edge rides the topmost gust (the gust itself stays sea-white).
                if (awake && top) { var gilt = Attach(plane, "Mystic funnel gold edge", MysticArc, width * 1.05f, Alpha(MysticGold, .5f), 124); gilt.angle = start + 170; gilt.spin = turn; layers.Add(gilt); }
            }

            // A thin spout of water rises in the eye.
            var spoutInk = Attach(root, "Mystic vortex spout ink", pillar, R * .75f, Alpha(MysticDeep, .38f), 28, false, new Vector2(0, -.6f)); MysticStretch(spoutInk, R * .75f, H + .6f);
            var spout = Attach(root, "Mystic vortex spout", pillar, R * .5f, Alpha(MysticSea, .34f), 124, true, new Vector2(0, -.6f)); MysticStretch(spout, R * .5f, H + .8f); spout.flicker = .15f;
            layers.Add(spoutInk); layers.Add(spout);

            // Droplets spiral in and up through the funnel, passing behind and in front of whatever it holds.
            int count = 10 + 5 * g;
            var motes = new MysticVortexMote[count];
            for (int i = 0; i < count; i++)
            {
                bool dark = i % 3 == 2;
                Color tone = dark ? MysticDeep : Alpha(awake && i % 4 == 1 ? MysticGoldHot : i % 4 == 0 ? MysticFoam : MysticSea, .95f);
                var clip = Attach(root, dark ? "Mystic vortex droplet ink" : "Mystic vortex droplet", spark, .4f, tone, 125, !dark);
                motes[i] = new MysticVortexMote { clip = clip, tone = tone, ink = dark, angle = Range(0, 360), phase = i / (float)count + Range(-.03f, .03f), rate = Range(.6f, .95f),
                    turn = Range(330, 470), height = Range(.1f, H * .7f), lift = Range(.3f, 1), unit = Range(.45f, .75f) / spark.bounds.size.x };
                layers.Add(clip);
            }

            // The vortex holds until it collapses (the collapse burst takes over); it only fades in.
            foreach (var l in layers) { l.life = life; l.fadeIn = .1f; l.fadePower = 0; }
            shade.life = life; shade.fadeIn = .12f; shade.fadePower = 0;
            float pop = MysticPop(life, .3f);
            foreach (var l in new[] { shade, spoutInk, spout }) { l.startScale = .3f; l.endScale = 1; l.ease = pop; }
            var planeArray = planes.ToArray(); var squashArray = squash.ToArray();
            // The funnel is posed once per frame: the high rate keeps it running on fast displays and when emitters are thinned
            // under load, and repeated calls in one frame are skipped.
            float posed = -1;
            Emitter(shade, 240, 0, (owner, p) => { if (owner.age == posed) return; posed = owner.age; MysticSpinVortex(owner.age, life, R, motes, planeArray, squashArray); });
            // Gusts stream in from the edge of the pull, spray is flung off the rim.
            Emitter(rimInk, 5 + 3 * g, 0, (owner, p) => MysticGust(p, R));
            Emitter(eye, 4 + 2 * g, 0, (owner, p) =>
            {
                float a = Range(0, 360) * Mathf.Deg2Rad; Vector2 at = p + new Vector2(Mathf.Cos(a) * R * .85f, Mathf.Sin(a) * R * .85f * .32f + .05f);
                MysticDroplet(at, new Vector2(-Mathf.Sin(a) * Range(1.5f, 3), Range(2.5f, 4.5f)), Chance(.35f), Range(.45f, .65f));
                if (Chance(.35f)) { var mist = Puff(at, Range(.5f, .8f), new Color(.55f, .85f, .9f, .32f), new Vector2(Range(-.4f, .4f), Range(.3f, .8f)), Range(.6f, .9f), 118); mist.endScale = 2; }
            });
            MysticSpinVortex(0, life, R, motes, planeArray, squashArray);
            Bundle(root, shade, layers.ToArray());

            // The vortex opens with a splash.
            var splash = Flash(center, R * 1.5f, Alpha(MysticSea, .7f), .24f); splash.ramp = Ramp(Color.white, MysticSea);
            GroundWave(ground, disc / .74f, Alpha(MysticFoam, .8f), .35f);
            var splashInk = MysticRing(ground, disc / .74f * 1.02f, Alpha(MysticDeep, .7f), .4f, false, 25); LayFlat(splashInk, .32f);
            for (int i = 0, n = Budget(6); i < n; i++) MysticDroplet(ground + new Vector2(Range(-.3f, .3f) * R, .1f), new Vector2(Range(-2.5f, 2.5f), Range(3, 6)), i % 3 == 0, Range(.5f, .7f));
        }

        private static void MysticSpinVortex(float t, float life, float R, MysticVortexMote[] motes, Transform[] planes, float[] squash)
        {
            // Opening and closing: the whole funnel springs open, then draws in just before the collapse.
            float open = 1 - Mathf.Pow(1 - Mathf.Clamp01(t / .3f), 3), left = life - t;
            float k = Mathf.Max(.04f, open * (left < .3f ? Mathf.Lerp(.3f, 1, Mathf.Clamp01(left / .3f)) : 1));
            for (int i = 0; i < planes.Length; i++) if (planes[i]) planes[i].localScale = new Vector3(k, squash[i] * k, 1);
            for (int i = 0; i < motes.Length; i++)
            {
                var m = motes[i]; if (!m.clip.root || !m.clip.renderer) continue;
                float cycle = Mathf.Repeat(m.phase + t * m.rate, 1);
                float r = Mathf.Lerp(R * 1.3f, R * .1f, Mathf.Pow(cycle, .8f)) * k;
                float a = (m.angle + t * m.turn + cycle * 200) * Mathf.Deg2Rad, sin = Mathf.Sin(a), cos = Mathf.Cos(a);
                m.clip.offset = new Vector2(cos * r, -.6f + m.height + cycle * m.lift + sin * r * .26f);
                m.clip.angle = Mathf.Atan2(cos * .26f, -sin) * Mathf.Rad2Deg;
                bool back = sin > 0;
                int order = back ? (m.ink ? 28 : 29) : (m.ink ? 121 : 126);
                if (m.clip.renderer.sortingOrder != order) m.clip.renderer.sortingOrder = order;
                // Droplets in front of the actors are bigger and brighter than the darker ones passing behind them.
                float fade = Smooth(0, .12f, cycle) * (1 - Smooth(.72f, .97f, cycle)) * open;
                m.clip.color = Alpha(m.tone, Mathf.Min(1, m.tone.a * fade * (back ? .75f : 1.3f)));
                float s = m.unit * (back ? .8f : 1.4f) * Mathf.Lerp(1.1f, .6f, cycle);
                m.clip.scale = new Vector3(s, s * .7f, 1);
            }
        }

        private void MysticGust(Vector2 ground, float R)
        {
            float side = Chance(.5f) ? 1 : -1, x = side * Range(R * 1.05f, R * 1.65f), y = Range(.15f, 1.7f), length = Range(.7f, 1.3f);
            Vector2 a = ground + new Vector2(x, y), b = a + new Vector2(side * length, Range(-.05f, .05f));
            var gust = Line(a, b, Range(.07f, .13f), Alpha(Chance(.4f) ? MysticFoam : MysticSea, .8f), Range(.24f, .34f), 124, 1);
            gust.velocity = new Vector2(-side * Range(4.5f, 7), 0); gust.fadeIn = .06f; gust.fadePower = 1.3f;
            var shadow = MysticPigment(Line(a + Vector2.down * .03f, b + Vector2.down * .03f, Range(.14f, .2f), Alpha(MysticDeep, .45f), gust.life, 119, 1), 119);
            shadow.velocity = gust.velocity; shadow.fadeIn = .06f; shadow.fadePower = 1.3f;
        }

        private Clip MysticDroplet(Vector2 pos, Vector2 velocity, bool ink, float life)
        {
            var drop = Spark(pos, velocity, ink ? MysticDeep : Alpha(Chance(.4f) ? MysticFoam : MysticSea, .95f), Range(.26f, .42f), life, 12, .4f);
            if (ink) MysticPigment(drop, 121);
            drop.endScale = .5f; return drop;
        }

        private void ImpactVortex(Vector2 pos, float radius, Color tint, int facing)
        {
            MysticEnsure();
            int g = Grade; bool awake = g >= 3;
            float R = Mathf.Clamp(radius, .6f, 5), s = Mathf.Clamp(R / 1.8f, .7f, 1.5f), span = 2 * R / .74f;
            // Gameplay bursts a level-three vortex from its floor point +.8 (SkillBurst), lower levels from the zone root (+.6).
            Vector2 ground = pos + Vector2.down * (awake ? .8f : .6f), heart = ground + Vector2.up * 1.1f * s;
            // The geyser grows with the level but stays inside the view (about nine units of sky above the floor).
            float tall = Mathf.Min((3.2f + .5f * (g - 1)) * s, 4.5f + .5f * (g - 1));

            // The funnel snaps shut: its rings collapse into the eye (the lowest over a dark wake of water, so it reads on a pale floor).
            for (int j = 0; j < 3; j++)
            {
                var snap = Spawn("Mystic vortex implode", ring, ground + Vector2.up * (.3f + j * .8f * s), span * (1 - j * .22f), Alpha(j == 0 ? Color.Lerp(MysticSea, MysticFoam, .5f) : MysticSea, .75f), .16f, true, 128);
                LayFlat(snap, .3f); snap.startScale = 1; snap.endScale = .1f; snap.ease = 1.5f; snap.fadePower = .6f;
            }
            var snapInk = Spawn("Mystic vortex implode ink", ring, ground + Vector2.up * .3f, span * 1.03f, Alpha(MysticDeep, .6f), .2f, false, 25);
            LayFlat(snapInk, .3f); snapInk.startScale = 1; snapInk.endScale = .1f; snapInk.ease = 1.5f; snapInk.fadePower = .8f;
            // A hot heart and a teal bloom over a dark well (behind the actors, so the gathered foes stay visible).
            var shade = Underlay(heart, Mathf.Min(R * 2.8f, 6.5f), .4f, .7f); shade.hold = .25f;
            var flash = Spawn("Mystic vortex heart", core, ground + Vector2.up * .5f, .95f * s, Alpha(MysticFoam, .95f), .14f, true, 133); flash.startScale = .4f; flash.endScale = 1.8f; flash.ease = 2.5f; flash.fadePower = 1.5f;
            var bloom = Flash(ground + Vector2.up * .8f, Mathf.Min(R * 2.2f, 4.6f), Alpha(MysticSea, .7f), .32f); bloom.ramp = Ramp(MysticFoam, MysticSea);
            var deep = Spawn("Mystic vortex deep bloom", glow, ground + Vector2.up * .7f, Mathf.Min(R * 2.6f, 5.6f), Alpha(MysticDeep, .55f), .5f, false, 29); deep.startScale = .4f; deep.endScale = 1.15f; deep.ease = 3; deep.fadePower = 1.4f;
            // A geyser bursts up out of the eye: a dark body behind the actors, light and a white spine in front.
            var spoutInk = MysticPigment(Pillar(ground, 1.5f * s, tall, Alpha(MysticDeep, .75f), .7f, 29), 29); spoutInk.hold = .2f;
            var spout = Pillar(ground, 1.2f * s, tall * 1.05f, Alpha(MysticSea, .85f), .6f, 127); spout.hold = .15f;
            Pillar(ground, .45f * s, tall * 1.1f, Alpha(MysticFoam, .95f), .4f, 128);
            // The geyser has a body: a column of water flaring as it rises, dark behind the actors and sea-lit in front, with
            // spray boiling off its sides.
            var bodyInk = Spawn("Mystic geyser body ink", MysticFunnel, ground, 1.8f * s, Alpha(MysticDeep, .55f), .65f, false, 29); MysticStretch(bodyInk, 1.8f * s, tall * .95f);
            var body = Spawn("Mystic geyser body", MysticFunnel, ground, 1.55f * s, Alpha(MysticSea, .42f), .55f, true, 126); MysticStretch(body, 1.55f * s, tall);
            foreach (var x in new[] { bodyInk, body }) { x.axisStart = new Vector2(.45f, .1f); x.axisEnd = Vector2.one; x.ease = 4; x.hold = .25f; x.fadePower = 1.4f; }
            for (int i = 0, n = Budget(5); i < n; i++)
            {
                float t = (i + .5f) / n, side = i % 2 == 0 ? 1 : -1;
                var spray = MysticLater(Puff(ground + new Vector2(side * Range(.1f, .4f) * s, tall * Mathf.Lerp(.15f, .85f, t)), Range(.7f, 1.05f) * s * (.7f + .5f * t), new Color(.62f, .9f, .94f, .42f),
                    new Vector2(side * Range(.6f, 1.4f), Range(.3f, .9f)), Range(.6f, .85f), 118), t * .08f);
                spray.endScale = 2; spray.fadeIn = .05f;
            }
            for (int i = 0, n = Budget(4); i < n; i++)
            {
                var crownPuff = Puff(ground + new Vector2(Range(-.4f, .4f) * s, tall * Range(.55f, .8f)), Range(.7f, 1.1f) * s, new Color(.6f, .9f, .95f, .45f), new Vector2(Range(-1.6f, 1.6f), Range(.5f, 1.5f)), Range(.7f, 1), 118);
                crownPuff.endScale = 2.2f; crownPuff.gravity = 1.5f; crownPuff.fadeIn = .08f;
            }
            // Water rings run across the floor to the edge of the area (at level three the awakening's gold ring rides that edge,
            // so only the dark water runs under it); impact lines and spray fly out.
            if (!awake) GroundWave(ground, span, Alpha(MysticSea, .9f), .5f);
            var inkWave = MysticRing(ground, span * 1.02f, Alpha(MysticDeep, .75f), .55f, false, 25); LayFlat(inkWave, .32f);
            ImpactLines(heart, Mathf.Min(R * 3, 5.4f), Alpha(MysticFoam, .7f), .3f);
            MysticPigment(ImpactLines(heart, Mathf.Min(R * 3.3f, 5.9f), Alpha(MysticDeep, .75f), .32f), 119);
            for (int i = 0, n = Budget(16); i < n; i++)
                MysticDroplet(ground + new Vector2(Range(-.4f, .4f) * s, Range(.2f, 1.2f) * s), new Vector2(Range(-1, 1) * Range(2, 6) * s, Range(4, 9) * s), i % 3 == 2, Range(.7f, 1.1f));
            for (int i = 0, n = Budget(6); i < n; i++)
            {
                float side = i % 2 == 0 ? 1 : -1;
                var foam = Puff(ground + new Vector2(side * Range(.1f, .8f) * R, .15f), Range(.6f, 1) * s, new Color(.55f, .85f, .9f, .42f), new Vector2(side * Range(1.5f, 3.5f), Range(.2f, .7f)), Range(1.2f, 1.6f), 118);
                foam.endScale = 2.4f; foam.drag = 2; foam.hold = .3f;
            }
            // Spray rains back down over the area for a moment after the burst.
            for (int i = 0, n = Budget(7); i < n; i++)
            {
                var rain = MysticLater(MysticDroplet(ground + new Vector2(Range(-1, 1) * R * .9f, tall * Range(.5f, .9f)), new Vector2(Range(-.5f, .5f), Range(-1, 1)), i % 3 == 2, Range(.5f, .7f)), Range(.25f, .7f));
                rain.gravity = 14;
            }
            // The whirlpool spins down and drains from the floor, leaving a wet sheen that dries off.
            var basinInk = Spawn("Mystic vortex drain ink", MysticSpiral, ground, 2 * R, Alpha(MysticDeep, .7f), 1.3f, false, 25); LayFlat(basinInk, .32f);
            var basin = Spawn("Mystic vortex drain", MysticSpiral, ground, 2 * R * .96f, Alpha(MysticSea, .6f), 1.2f, true, 27); LayFlat(basin, .32f);
            foreach (var x in new[] { basinInk, basin }) { x.spin = 320; x.startScale = 1; x.endScale = .3f; x.ease = 1.5f; x.hold = .25f; x.fadePower = 1.3f; }
            var wet = Spawn("Mystic vortex wet floor", glow, ground, 2 * R * 1.05f, Alpha(MysticDeep, .45f), 1.8f, false, 24); LayFlat(wet, .3f); wet.hold = .45f; wet.fadePower = 1.3f;
            var sheen = Spawn("Mystic vortex wet sheen", glow, ground, 2 * R * .8f, Alpha(MysticSea, .3f), 1.5f, true, 26); LayFlat(sheen, .3f); sheen.hold = .4f; sheen.fadePower = 1.4f; sheen.pulse = .04f;
            var mist = Puff(ground + Vector2.up * .6f * s, 2 * R * .7f, new Color(.6f, .88f, .92f, .3f), Vector2.up * .4f, 1.6f, 117); mist.startScale = .5f; mist.endScale = 1.6f; mist.drag = 1; mist.fadeIn = .15f;
            if (awake)
            {
                // Awakened: the awakening and the grade overlay already crown this burst in gold (flash, crown, seal, edge), so
                // the vortex keeps its own water: a second, taller surge of the geyser with a thin gold core and spray.
                var surgeBody = MysticLater(Pillar(ground, 1.1f * s, tall * 1.08f, Alpha(MysticFoam, .8f), .45f, 127), .12f); surgeBody.hold = .1f;
                var surge = MysticLater(Pillar(ground, .32f * s, tall * 1.1f, Alpha(MysticGoldHot, .75f), .38f, 128), .12f); surge.hold = .1f;
                for (int i = 0, n = Budget(5); i < n; i++)
                    MysticLater(Spark(ground + new Vector2(Range(-.3f, .3f), tall * Range(.3f, .9f)), Direction(Range(30, 150)) * Range(4, 8), i % 2 == 0 ? MysticFoam : MysticGoldHot, Range(.28f, .45f), Range(.4f, .6f), 8, 1.5f), .12f);
            }
        }

        // ---------------------------------------------------------------- Curse

        private void CastCurse(CastInfo c)
        {
            MysticEnsure();
            int g = Grade; float k = Mathf.Clamp(c.size / 1.4f, .8f, 1.5f) * (1 + .1f * (g - 1)), w = c.windup, release = Mathf.Max(0, w - .02f);
            Vector2 hand = c.pos + new Vector2(c.facing * .62f, .02f), ground = c.pos + Vector2.down;
            float handLife = w + .1f, hold = w / handLife;

            // A violet brand turns in the hand over a pool of ink (behind the caster), its slit eye glowing.
            var pool0 = Anchored(c.anchor, hand, "Mystic curse hand ink", glow, 1.8f * k, Alpha(MysticHexInk, .75f), handLife, 29, false);
            var brandInk = Anchored(c.anchor, hand, "Mystic curse hand brand ink", MysticBrand, 1.12f * k, MysticHexInk, handLife, 118, false); brandInk.spin = -160;
            var brand = Anchored(c.anchor, hand, "Mystic curse hand brand", MysticBrand, 1.02f * k, Alpha(MysticHex, .95f), handLife, 126); brand.spin = -160;
            var pupil = Anchored(c.anchor, hand, "Mystic curse hand heart", core, .28f * k, Alpha(MysticHexHot, .95f), handLife, 127); pupil.pulse = .12f;
            foreach (var x in new[] { pool0, brandInk, brand, pupil }) { x.fadeIn = .04f; x.startScale = .3f; x.endScale = 1; x.ease = 1.6f; x.hold = hold; x.fadePower = 2; }
            if (w < .2f)
            {
                // An instant echo: the curse leaves at once, so the echo only kindles the brand.
                Converge(hand, 5, 1.6f * k, MysticHex, w, .5f);
                return;
            }
            var shade = Underlay(hand, 3.2f * k, .42f, w + .2f); shade.hold = .7f;
            // Ink smoke and violet motes spiral into the hand.
            int fumes = Budget(5);
            for (int i = 0; i < fumes; i++)
            {
                var fume = Puff(hand, Range(.7f, 1) * k, new Color(.22f, .02f, .34f, .6f), Vector2.zero, w * Range(.8f, 1), 116);
                fume.orbitCenter = hand; fume.orbitAngle = i * 360f / fumes + Range(-15, 15); fume.orbit = -Range(300, 420);
                fume.orbitRadius = Range(1.3f, 1.9f) * k; fume.orbitRadiusEnd = .1f; fume.orbitPower = 1.3f;
                fume.startScale = 1; fume.endScale = .3f; fume.fadeIn = .1f; fume.hold = .6f; fume.fadePower = 1.4f;
            }
            Converge(hand, 10, 2.6f * k, MysticHex, w, .55f);
            // The brand burns on the floor under the caster.
            float total = w + .25f;
            var sealInk = Spawn("Mystic curse floor ink", MysticBrand, ground, 3.2f * k, Alpha(MysticHexInk, .8f), total, false, 25); LayFlat(sealInk, .32f);
            var seal = Spawn("Mystic curse floor", MysticBrand, ground, 3 * k, Alpha(MysticHex, .85f), total, true, 27); LayFlat(seal, .32f);
            foreach (var x in new[] { sealInk, seal }) { x.spin = 45; x.fadeIn = .08f; x.startScale = .5f; x.endScale = 1; x.ease = 3; x.hold = w / total; x.fadePower = 1.5f; }
            GroundGlow(ground, 3.4f * k, Alpha(MysticHex, .4f), total).hold = w / total;
            // Release: the brand flares and is flung.
            var flare = MysticLater(Flash(hand, 2 * k, Alpha(MysticHex, .85f), .22f), release); flare.ramp = Ramp(Color.white, MysticHex);
            MysticLater(MysticPigment(ImpactLines(hand, 3.2f * k, Alpha(MysticHexInk, .85f), .24f), 117), release);
            MysticLater(ImpactLines(hand, 2.8f * k, Alpha(MysticHex, .8f), .22f), release);
        }

        private void TrailCurse(Vector2 from, Vector2 to, Color tint)
        {
            MysticEnsure();
            Vector2 delta = to - from; float length = delta.magnitude; if (length < .05f) return;
            Vector2 dir = delta / length, side = new Vector2(-dir.y, dir.x);
            int g = Grade; float thick = 1 + .15f * (g - 1);
            // Quick enough that marks drawn at once by other layers (grade, status) follow the bite closely.
            float travel = Mathf.Clamp(length / 30, .08f, .2f), amp = Mathf.Clamp(length * .07f, .18f, .5f), waves = Mathf.Clamp(length / 2.6f, 1, 3), phase = Range(0, 6.28f);
            MysticCurseFrame = Time.frameCount; MysticCurseEnd = to; MysticCurseTravel = travel;
            int n = Mathf.Clamp(Mathf.RoundToInt(length * 2.2f), 8, 18);
            float linger = .3f + .1f * g, step = travel / n;

            // The tendril creeps from the hand to the victim: a ragged ink stroke with violet light and a hot thread inside,
            // led by a dark swelling head.
            Vector2 previous = from;
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n; Vector2 point = MysticCreep(from, delta, side, t, amp, waves, phase);
                float delay = (t - .5f / n) * travel, span = travel - delay + linger;
                var ink = MysticLater(MysticBrush(previous, point, .44f * thick, MysticHexInk, span, false, 117), delay); ink.hold = .55f; ink.fadePower = 1.3f;
                var lit = MysticLater(MysticBrush(previous, point, .26f * thick, Alpha(MysticHex, .9f), span - .04f, true, 126), delay); lit.hold = .5f; lit.flicker = .15f; lit.fadePower = 1.4f;
                if (i % 2 == 0) { var thread = MysticLater(Line(previous, point, .07f * thick, Alpha(MysticHexHot, .95f), span * .8f, 128, 1.15f), delay); thread.hold = .4f; thread.flicker = .3f; }
                if (i % 2 == 1 || i == n)
                {
                    var head = MysticLater(Spawn("Mystic curse head", glow, point, .95f * thick, Alpha(MysticHex, .95f), step * 3.4f + .04f, true, 127), delay); head.fadePower = 1.2f;
                    var headInk = MysticLater(Spawn("Mystic curse head ink", glow, point, 1.2f * thick, Alpha(MysticHexInk, .8f), step * 3.4f + .06f, false, 116), delay); headInk.fadePower = 1.2f;
                }
                previous = point;
            }
            // Ink drips from the tendril and smoke curls off it.
            for (int i = 0, drips = Budget(3); i < drips; i++)
            {
                float t = Range(.15f, .9f); Vector2 at = MysticCreep(from, delta, side, t, amp, waves, phase);
                var drop = MysticLater(Spawn("Mystic curse drip", core, at, Range(.1f, .16f), MysticHexInk, Range(.4f, .6f), false, 118), t * travel + .05f);
                drop.velocity = new Vector2(Range(-.3f, .3f), Range(-.5f, .3f)); drop.gravity = 8; drop.hold = .5f; drop.fadePower = 1.2f;
            }
            for (int i = 0, puffs = Budget(3); i < puffs; i++)
            {
                float t = Range(.1f, .9f);
                var fume = MysticLater(Puff(MysticCreep(from, delta, side, t, amp, waves, phase), Range(.5f, .8f) * thick, Alpha(MysticHexInk, .55f), new Vector2(Range(-.3f, .3f), Range(.3f, .7f)), Range(.6f, .8f), 115), t * travel);
                fume.endScale = 2;
            }
            // A dark flick at the source, and a small ink splash where it bites.
            Glint(from, 1.1f * thick, Alpha(MysticHex, .9f), .16f, 45).spin = 0;
            MysticLater(Wave(to, 1.6f * thick, Alpha(MysticHex, .8f), .26f, 3), travel);
            MysticLater(MysticRing(to, 1.8f * thick, MysticHexInk, .3f, false, 117), travel);
            for (int i = 0; i < 4; i++) MysticLater(Spark(to, Direction(Range(0, 360)) * Range(2.5f, 5), i % 2 == 0 ? MysticHexHot : MysticHex, Range(.2f, .32f), Range(.2f, .3f), 4, 3), travel);
        }

        private static Vector2 MysticCreep(Vector2 from, Vector2 delta, Vector2 side, float t, float amp, float waves, float phase)
        {
            return from + delta * t + side * Mathf.Sin(t * waves * 2 * Mathf.PI + phase) * amp * Mathf.Sin(Mathf.PI * t);
        }

        private void ImpactCurse(Vector2 pos, float radius, Color tint, int facing)
        {
            MysticEnsure();
            int g = Grade; bool awake = g >= 3;
            // The brand marks one foe, so it keeps one size at every level; the level grows the rune wheel, the veins reaching
            // out from it and the smoke that lingers on the burn.
            float s = Mathf.Clamp(radius / .9f, .7f, 1.3f) * (1 + .05f * (g - 1));
            // A curse that crept here is stamped when its tendril arrives.
            float d = MysticTrailArrival(SkillEffect.Curse, pos), hit = d + .04f;
            // Brands stamped in one frame (one cast on several foes) share the first one's flash, lines and wave.
            bool lead = MysticCurseStampFrame != Time.frameCount; MysticCurseStampFrame = Time.frameCount;
            float life = .95f + .15f * g, stamp = MysticPop(life, .1f);

            var shade = MysticLater(Underlay(pos, 3 * s, .42f, life), d); shade.hold = .4f;
            // The eye brand falls in large and slams onto the victim: an ink outline, a violet body and light, then a violet sear.
            var brandInk = MysticLater(Spawn("Mystic curse brand ink", MysticBrand, pos, 1.45f * s, MysticHexInk, life, false, 117), d);
            var brandBody = MysticLater(Spawn("Mystic curse brand body", MysticBrand, pos, 1.36f * s, MysticHexBody, life, false, 118), d);
            var brand = MysticLater(Spawn("Mystic curse brand", MysticBrand, pos, 1.31f * s, Alpha(MysticHex, .9f), life * .85f, true, 127), d); brand.flicker = .08f;
            foreach (var x in new[] { brandInk, brandBody, brand }) { x.startScale = 1.7f; x.endScale = 1; x.ease = stamp; x.fadeIn = .05f; x.spin = 35; x.hold = .45f; x.fadePower = 1.4f; }
            var sear = MysticLater(Spawn("Mystic curse sear", MysticBrand, pos, 1.33f * s, Alpha(MysticHexHot, .75f), .18f, true, 129), hit); sear.startScale = 1.1f; sear.endScale = 1; sear.ease = 3; sear.fadePower = 1.6f; sear.angle = 2.5f;
            var heat = MysticLater(Spawn("Mystic curse sear core", core, pos, .6f * s, Alpha(MysticHexHot, .8f), .12f, true, 133), hit); heat.startScale = .5f; heat.endScale = 1.5f; heat.ease = 2.5f; heat.fadePower = 1.5f;
            if (lead)
            {
                var bloom = MysticLater(Flash(pos, 2 * s, Alpha(MysticHex, .5f), .24f), hit); bloom.ramp = Ramp(MysticHexHot, MysticHex);
                MysticLater(MysticPigment(ImpactLines(pos, 3.2f * s, Alpha(MysticHexInk, .85f), .3f), 116), hit);
                MysticLater(ImpactLines(pos, 2.8f * s, Alpha(MysticHex, .75f), .26f), hit);
                MysticLater(Wave(pos, 2.2f * s, Alpha(MysticHex, .75f), .32f, 3), hit);
            }
            MysticLater(MysticRing(pos, 2.4f * s, Alpha(MysticHexInk, .8f), .36f, false, 116), hit);
            // Ink splatters off the stamp, smoke and violet wisps rise from the burn.
            for (int i = 0, n = Budget(6); i < n; i++)
            {
                var drop = MysticLater(Spawn("Mystic curse splatter", core, pos + Inside(.2f * s), Range(.1f, .18f) * s, MysticHexInk, Range(.45f, .7f), false, 118), hit);
                drop.velocity = Direction(i * 360f / n + Range(-20, 20)) * Range(3, 6) * s + Vector2.up; drop.gravity = 9; drop.drag = 1; drop.hold = .5f; drop.fadePower = 1.2f;
            }
            for (int i = 0, n = Budget(4); i < n; i++) { var fume = MysticLater(Puff(pos + Inside(.4f * s), Range(.6f, .9f) * s, Alpha(MysticHexInk, .6f), new Vector2(Range(-.6f, .6f), Range(.5f, 1.1f)), Range(.7f, .9f), 115), d + .1f); fume.endScale = 2.1f; }
            for (int i = 0, n = Budget(4); i < n; i++)
            {
                var soul = MysticLater(Spawn("Mystic curse wisp", wisp, pos + Inside(.5f * s), Range(.2f, .34f) * s, Alpha(i % 2 == 0 ? MysticHex : MysticHexHot, .9f), Range(.7f, 1.1f), true, 128), d + Range(.1f, .4f));
                soul.velocity = new Vector2(Range(-.4f, .4f), Range(1, 2)); soul.drag = .4f; soul.fadeIn = .08f; soul.flicker = .3f; soul.endScale = .3f; soul.ease = 1;
            }
            if (g < 2) return;

            // Level two and up: a rune wheel turns behind the brand (bolder at three), ink smoke lingers on the burn, and veins
            // of ink reach out from it (more of them at three, toward the next host the curse will spread to).
            float wheelAlpha = awake ? .8f : .55f;
            var wheelInk = MysticLater(Spawn("Mystic curse wheel ink", runeRing, pos, 2.3f * s, Alpha(MysticHexInk, wheelAlpha * .8f), life, false, 115), d + .05f);
            var wheel = MysticLater(RuneCircle(pos, 2.2f * s, Alpha(awake ? Color.Lerp(MysticHex, MysticHexHot, .35f) : MysticHex, wheelAlpha), life, -60, false, 125), d + .05f);
            wheelInk.spin = -60; wheelInk.fadeIn = .08f; wheelInk.startScale = .6f; wheelInk.endScale = 1; wheelInk.ease = 4; wheelInk.hold = .4f; wheelInk.fadePower = 1.4f;
            wheel.hold = .45f;
            for (int i = 0, n = Budget(g); i < n; i++)
            {
                var linger = MysticLater(Puff(pos + Inside(.5f * s), Range(.7f, 1) * s, Alpha(MysticHexInk, .45f), new Vector2(Range(-.25f, .25f), Range(.25f, .5f)), Range(1.3f, 1.7f), 115), d + Range(.25f, .45f));
                linger.drag = 1; linger.endScale = 2.3f; linger.fadeIn = .2f; linger.hold = .3f;
            }
            int veins = awake ? 4 : 3; float turn = Range(0, 360);
            for (int i = 0; i < veins; i++)
            {
                float a = turn + i * 360f / veins + Range(-12, 12); Vector2 way = Direction(a), across = Direction(a + 90), previous = pos + way * .5f * s;
                for (int j = 1; j <= 2; j++)
                {
                    Vector2 next = pos + way * (.5f + j * .44f) * s + across * Range(-.12f, .12f);
                    float delay = hit + .01f + j * .04f;
                    var vein = MysticLater(MysticBrush(previous, next, .22f * (1.2f - j * .2f) * s, MysticHexInk, .55f, false, 117), delay); vein.hold = .4f;
                    var lit = MysticLater(MysticBrush(previous, next, .1f * s, Alpha(MysticHexHot, .8f), .45f, true, 127), delay); lit.hold = .3f; lit.flicker = .2f;
                    // Awakened: a fine gold thread runs inside each vein.
                    if (awake) { var thread = MysticLater(Line(previous, next, .035f * s, Alpha(MysticGold, .75f), .4f, 128, 1.1f), delay); thread.hold = .3f; }
                    previous = next;
                }
                MysticLater(Glint(previous, .7f * s, Alpha(awake ? MysticGoldHot : MysticHexHot, .85f), .22f, 45), hit + .14f);
            }
            if (awake)
            {
                // Awakened (the curse spreads on death): a violet rim with a fine gold line just outside it.
                var rim = MysticLater(Spawn("Mystic curse rim", ring, pos, 2 * s, Alpha(MysticHex, .6f), life * .8f, true, 128), hit);
                var gilt = MysticLater(Spawn("Mystic curse rim gold", ring, pos, 2.08f * s, Alpha(MysticGold, .3f), life * .7f, true, 128), hit);
                foreach (var x in new[] { rim, gilt }) { x.pulse = .05f; x.startScale = .8f; x.endScale = 1; x.ease = 4; x.hold = .4f; x.fadePower = 1.4f; }
                rim.flicker = .15f;
            }
        }

        // ---------------------------------------------------------------- WarBanner

        private static float MysticBannerHeight(int g) { return 3.4f + .35f * (g - 1); }

        private void CastWarBanner(CastInfo c)
        {
            MysticEnsure();
            // Gameplay plants the banner beside the hero, even when a companion raises it, and this call does not know where:
            // remember its facing for the zone, and draw only what happens at the caster (the reach first shows when it lands).
            MysticBannerCalls.RemoveAll(x => Time.frameCount - x.frame > 600);
            if (MysticBannerCalls.Count >= 6) MysticBannerCalls.RemoveAt(0);
            MysticBannerCalls.Add(new MysticBannerCall { frame = Time.frameCount, x = c.pos.x, facing = c.facing });
            int g = Grade; float w = c.windup, release = Mathf.Max(0, w - .02f);
            float H = MysticBannerHeight(g);
            Vector2 ground = c.pos + Vector2.down, foot = new Vector2(c.pos.x - c.facing * .45f, ground.y), hand = c.pos + new Vector2(c.facing * .2f, .75f);
            Vector2 crestAt = foot + Vector2.up * (H + .4f);
            var palm = Anchored(c.anchor, hand, "Mystic banner hand", glow, 1.4f, Alpha(MysticGold, .8f), w + .08f, 125);
            palm.fadeIn = .04f; palm.startScale = .3f; palm.endScale = 1; palm.ease = 1.5f; palm.hold = .8f; palm.pulse = .08f;
            if (w < .2f)
            {
                // An instant echo: the banner drops in at once, so the echo only flashes the hand.
                Converge(hand, 5, 1.6f, MysticGold, w, .4f);
                MysticLater(Glint(hand, 1.4f, Alpha(MysticGoldHot, .9f), .2f), release).spin = 0;
                return;
            }
            float total = w + .25f, hold = w / total;
            // Gold kindles on the floor where the caster stands (small: it is not the banner's reach) and motes rise from it.
            var kindleInk = Spawn("Mystic war kindle ink", glow, ground, 2.6f, new Color(.62f, .36f, .06f, .32f), total, false, 24); LayFlat(kindleInk, .3f);
            kindleInk.fadeIn = .08f; kindleInk.hold = hold; kindleInk.fadePower = 1.5f;
            var floor = GroundGlow(ground, 2.4f, Alpha(MysticGold, .5f), total); floor.hold = hold;
            Emitter(floor, 10, .9f, (owner, p) => MysticBannerMote(MysticOnGround(owner, p), g >= 3));
            // The standard is drawn up out of the floor in light behind the caster, its crest kindling at the top.
            var column = Pillar(foot, .9f, H + 1, Alpha(MysticGold, .55f), w + .1f, 27);
            column.fadeIn = w * .3f; column.hold = w / (w + .1f); column.fadePower = 2; column.axisStart = new Vector2(.3f, .1f); column.ease = 1.6f;
            var ghostInk = Spawn("Mystic banner rising ink", MysticPole, foot, .3f, Alpha(MysticBronze, .5f), w + .05f, false, 26); MysticStretch(ghostInk, .3f, H);
            var ghost = Spawn("Mystic banner rising", MysticPole, foot, .26f, Alpha(MysticGoldHot, .8f), w + .05f, true, 28); MysticStretch(ghost, .26f, H);
            foreach (var x in new[] { ghostInk, ghost }) { x.axisStart = new Vector2(1, .08f); x.axisEnd = Vector2.one; x.ease = 1.8f; x.fadeIn = .08f; x.hold = .8f; x.fadePower = 2; }
            var crestGhost = MysticLater(Spawn("Mystic banner crest kindling", MysticCrest, crestAt, .95f, Alpha(MysticGoldHot, .85f), w * .55f + .05f, true, 29), w * .45f);
            crestGhost.startScale = .3f; crestGhost.endScale = 1; crestGhost.ease = 3; crestGhost.fadeIn = .06f; crestGhost.hold = .8f; crestGhost.fadePower = 2;
            Converge(hand, 10, 2.2f, MysticGold, w, .35f);
            // Release: a flare at the crest as the standard is planted.
            MysticLater(Glint(crestAt, 2.4f, Alpha(MysticGoldHot, .95f), .3f), release).spin = 0;
            MysticLater(Flash(hand, 1.6f, Alpha(MysticGold, .8f), .2f), release);
        }

        private void ZoneWarBanner(Transform root, float radius, float life)
        {
            MysticEnsure();
            int g = Grade; bool awake = g >= 3;
            float R = Mathf.Clamp(radius, .8f, 6), H = MysticBannerHeight(g), hold = Mathf.Clamp01(1 - .4f / life);
            // The cloth flies behind whoever raised it: take the facing of the call made where it lands (the hero's own), or else
            // of the oldest call still waiting (a companion's, planted beside the hero).
            float rootX = root.position.x; int facing = 1, pick = -1;
            MysticBannerCalls.RemoveAll(x => Time.frameCount - x.frame > 600);
            for (int i = 0; i < MysticBannerCalls.Count && pick < 0; i++) if (Mathf.Abs(MysticBannerCalls[i].x - rootX) < .35f) pick = i;
            if (pick < 0 && MysticBannerCalls.Count > 0) pick = 0;
            if (pick >= 0) { facing = MysticBannerCalls[pick].facing; MysticBannerCalls.RemoveAt(pick); }
            int side = facing > 0 ? -1 : 1;
            // A second banner planted on the same spot flies to the other side, so the two standards never merge into one.
            MysticBanners.RemoveAll(x => !x.root);
            foreach (var other in MysticBanners) if (Mathf.Abs(other.root.position.x - rootX) < .5f && other.side == side) { side = -side; break; }
            float poleX = side * .45f, flagW = 2 + .2f * (g - 1), flagH = flagW / MysticFlagAspect;
            Vector2 foot = new Vector2(poleX, -.6f);
            var layers = new List<Clip>();

            // The standard (behind the actors): a bronze-bound pole with a thin gold rim of light (so it holds against dark
            // towers), a golden sun crest and the cloth flying behind the caster.
            var poleGlow = Attach(root, "Mystic banner pole rim", MysticPole, .4f, Alpha(MysticGold, .4f), 26, true, foot); MysticStretch(poleGlow, .4f, H);
            var pole = Attach(root, "Mystic banner pole", MysticPole, .24f, Color.white, 27, false, foot); MysticStretch(pole, .24f, H);
            layers.Add(poleGlow);
            float crestW = .95f + .12f * (g - 1), crestLift = crestW * .4f;
            Vector2 crestAt = foot + Vector2.up * (H + crestLift);
            var crestGlow = Attach(root, "Mystic banner crest glow", glow, crestW * 2.2f, Alpha(MysticGold, .5f), 26, true, crestAt); crestGlow.pulse = .06f;
            var crest = Attach(root, "Mystic banner crest", MysticCrest, crestW, Color.white, 29, false, crestAt);
            layers.Add(crestGlow); layers.Add(crest);
            Clip halo = null;
            if (g >= 2) { halo = Attach(root, "Mystic banner sun halo", runeRing, crestW * (awake ? 2.5f : 2), Alpha(awake ? MysticGoldHot : MysticGold, awake ? .75f : .5f), 26, true, crestAt); halo.spin = 30; layers.Add(halo); }
            // The cloth and, at every level, its gold edge light (faint at one, full when awakened), so it holds on a red sky.
            var cloth = new Clip[MysticStripCount]; var shine = new Clip[MysticStripCount];
            float seg = flagW / MysticStripCount;
            for (int i = 0; i < MysticStripCount; i++)
            {
                cloth[i] = Attach(root, "Mystic banner cloth", MysticFlagStrips[i], seg, Color.white, 28, false, foot); MysticStretch(cloth[i], seg * 1.12f * side, flagH); layers.Add(cloth[i]);
                shine[i] = Attach(root, "Mystic banner cloth shine", MysticFlagGlowStrips[i], seg, Alpha(MysticGoldHot, .6f), 29, true, foot); MysticStretch(shine[i], seg * 1.12f * side, flagH); layers.Add(shine[i]);
            }

            // The reach of the blessing on the floor: an amber pigment that keeps its colour on a pale floor, a soft glow, and a
            // bronze-inked gold edge with a rune ring exactly on the radius. From level two the grade overlay lays its own lit
            // edge there (and at three its rune seal), so this keeps only the ink under it, on the overlay's plane.
            var plane = MysticPlane(root, new Vector2(0, g >= 2 ? -.57f : -.6f), .3f);
            float edgeW = 2 * R / .74f, span = 2 * R / .955f;
            var pigment = Attach(plane, "Mystic war pigment", glow, 2 * R * 1.08f, new Color(.8f, .5f, .1f, .25f), 24, false);
            var floor = Attach(plane, "Mystic war floor", glow, 2 * R * 1.1f, Alpha(MysticGold, .12f), 26); floor.pulse = .04f;
            var edgeInk = Attach(plane, "Mystic war edge ink", ring, edgeW * 1.02f, Alpha(MysticBronze, .8f), 25, false);
            var floorLayers = new List<Clip> { pigment, floor, edgeInk };
            if (g == 1) { var edge = Attach(plane, "Mystic war edge", ring, edgeW, Alpha(MysticGold, .8f), 27); edge.pulse = .03f; floorLayers.Add(edge); }
            if (!awake)
            {
                var auraInk = Attach(plane, "Mystic war seal ink", runeRing, span * 1.01f, Alpha(MysticBronze, .6f), 25, false); auraInk.spin = 8;
                var aura = Attach(plane, "Mystic war seal", runeRing, span, Alpha(MysticGold, .7f), 27); aura.spin = 8;
                floorLayers.Add(auraInk); floorLayers.Add(aura);
            }
            else { var laurel = Attach(plane, "Mystic war laurel", runeRing, R * 1.3f, Alpha(MysticGoldHot, .65f), 27); laurel.spin = -22; floorLayers.Add(laurel); }
            layers.AddRange(floorLayers);

            float pop = MysticPop(life, .25f);
            pole.life = life; pole.fadeIn = .05f; pole.hold = hold; pole.fadePower = 1.5f;
            foreach (var l in layers) { l.life = life; l.fadeIn = .08f; l.hold = hold; l.fadePower = 1.5f; }
            foreach (var l in floorLayers) { l.startScale = .2f; l.endScale = 1; l.ease = pop; }
            var rig = new MysticBannerRig { root = root, pole = pole, poleGlow = poleGlow, crest = crest, crestGlow = crestGlow, halo = halo, cloth = cloth, shine = shine,
                height = H, poleX = poleX, flagW = flagW, flagH = flagH, crestLift = crestLift, side = side, phase = Range(0, 6.28f), shineGain = g == 1 ? .42f : g == 2 ? .67f : 1 };
            MysticBanners.Add(rig);
            // The banner is posed once per frame: the high rate keeps it waving on fast displays and when emitters are thinned
            // under load, and repeated calls in one frame are skipped.
            Emitter(pole, 240, 0, (owner, p) => { if (owner.age == rig.clock) return; MysticWaveBanner(rig, owner.age); });
            Emitter(floor, 7 + 4 * g, R, (owner, p) => MysticBannerMote(MysticOnGround(owner, p), awake));
            MysticWaveBanner(rig, 0);
            Bundle(root, pole, layers.ToArray());

            // The standard lands: dust, a gold ring out to the edge, sparks and a flare on the crest.
            Vector2 world = root.position, footWorld = world + foot, groundWorld = world + Vector2.down * .6f;
            const float land = .13f;
            MysticLater(Flash(footWorld + Vector2.up * .3f, 2.2f, Alpha(MysticGold, .8f), .22f), land);
            MysticLater(GroundWave(groundWorld, 2 * R / .74f, Alpha(MysticGold, .9f), .45f), land);
            var inkWave = MysticLater(MysticRing(groundWorld, 2 * R / .74f * 1.02f, Alpha(MysticBronze, .7f), .5f, false, 25), land); LayFlat(inkWave, .32f);
            MysticLater(ImpactLines(footWorld + Vector2.up * .25f, 3.2f, Alpha(MysticGoldHot, .75f), .26f), land);
            for (int i = 0, n = Budget(6); i < n; i++)
            {
                float s = i % 2 == 0 ? 1 : -1;
                var dust = MysticLater(Puff(footWorld + new Vector2(s * Range(.1f, .4f), .1f), Range(.5f, .8f), DustTone, new Vector2(s * Range(1.6f, 3.2f), Range(.3f, .8f)), Range(.5f, .7f), 118), land); dust.gravity = -.3f;
            }
            for (int i = 0, n = Budget(8); i < n; i++) MysticLater(Spark(footWorld + Vector2.up * .2f, new Vector2(Range(-4, 4), Range(2, 6)), i % 3 == 0 ? Color.white : MysticGold, Range(.25f, .4f), Range(.3f, .5f), 8, 2), land);
            MysticLater(Glint(world + crestAt, 2.4f + .3f * g, Alpha(MysticGoldHot, .95f), .35f), land + .05f).spin = 0;
        }

        /// <summary>Drops the standard into place, then waves the cloth: each strip follows a travelling wave from the pole
        /// to the swallow tail, and its fold catches more or less light.</summary>
        private static void MysticWaveBanner(MysticBannerRig rig, float t)
        {
            rig.clock = t;
            float land = Mathf.Clamp01(t / .13f), drop = (1 - land * land) * 1.8f;
            float top = -.6f + rig.height + drop;
            Vector2 crestAt = new Vector2(rig.poleX, top + rig.crestLift);
            rig.pole.offset = new Vector2(rig.poleX, -.6f + drop); rig.poleGlow.offset = rig.pole.offset;
            rig.crest.offset = crestAt; rig.crestGlow.offset = crestAt; if (rig.halo != null) rig.halo.offset = crestAt;
            // Each vertical strip of cloth rides a travelling wave that grows toward the tail and sags a little; a beat of the
            // banner makes it snap. Strips shift up and down and tilt by only half the slope, so neighbours stay joined.
            float kick = Mathf.Exp(-Mathf.Max(0, t - rig.beatAt) * 4), gain = .13f * (1 + .7f * kick);
            float k = 2 * Mathf.PI / 1.5f, w = 2 * Mathf.PI * 1.3f, seg = rig.flagW / rig.cloth.Length;
            Vector2 attach = new Vector2(rig.poleX, top - .12f - rig.flagH * .5f);
            for (int i = 0; i < rig.cloth.Length; i++)
            {
                float x = i * seg, reach = x / rig.flagW, amp = gain * (.12f + .88f * reach), ph = k * x - w * t + rig.phase;
                float y = amp * Mathf.Sin(ph) - .1f * x * reach;
                float slope = amp * k * Mathf.Cos(ph) + gain * .88f / rig.flagW * Mathf.Sin(ph) - .2f * reach;
                float deg = Mathf.Atan(slope) * Mathf.Rad2Deg * .5f * rig.side, fold = Mathf.Cos(ph + 1.2f), light = .8f + .2f * fold;
                Vector2 at = attach + new Vector2(rig.side * x, y);
                var strip = rig.cloth[i]; strip.offset = at; strip.angle = deg; strip.color = new Color(light, light, light, 1);
                var s = rig.shine[i]; s.offset = at; s.angle = deg; s.color = Alpha(MysticGoldHot, Mathf.Clamp01((.3f + .4f * Mathf.Max(0, fold) + .3f * kick) * rig.shineGain));
            }
        }

        private void MysticBannerMote(Vector2 at, bool awake)
        {
            if (Chance(.22f))
            {
                // A small shred of crimson or gold cloth drifting up.
                var shred = Spawn("Mystic banner shred", petal, at, Range(.16f, .26f), Chance(.5f) ? new Color(.78f, .08f, .12f, .92f) : new Color(.95f, .7f, .25f, .92f), Range(.9f, 1.4f), false, 121);
                shred.velocity = new Vector2(Range(-.3f, .3f), Range(.8f, 1.6f)); shred.drag = .4f; shred.spin = Range(-400, 400); shred.angle = Range(0, 360); shred.fadeIn = .1f; shred.hold = .4f; shred.fadePower = 1.3f;
                return;
            }
            var mote = Spark(at, new Vector2(Range(-.15f, .15f), Range(1.3f, 2.6f)), Chance(awake ? .5f : .3f) ? MysticGoldHot : MysticGold, Range(.18f, .32f), Range(.6f, 1), -.6f, .5f);
            mote.flicker = .3f; mote.fadeIn = .1f;
        }

        private void ImpactWarBanner(Vector2 pos, float radius, Color tint, int facing)
        {
            MysticEnsure();
            int g = Grade; bool awake = g >= 3;
            float R = Mathf.Clamp(radius, .8f, 6), span = 2 * R / .74f;
            Vector2 ground = pos + Vector2.down * .4f;
            // The banner beats: its crest flares and the cloth snaps. Of the banners standing here, the one that has not beaten
            // for the longest answers (two banners on one spot each beat on their own second).
            MysticBanners.RemoveAll(x => !x.root);
            MysticBannerRig beating = null;
            foreach (var rig in MysticBanners)
            {
                if (Mathf.Abs(rig.root.position.x - pos.x) > .6f || rig.beatAt > rig.clock - .5f) continue;
                if (beating == null || rig.clock - rig.beatAt > beating.clock - beating.beatAt) beating = rig;
            }
            if (beating != null)
            {
                beating.beatAt = beating.clock;
                Vector2 crestAt = (Vector2)beating.root.position + beating.crest.offset;
                Glint(crestAt, 1.8f + .3f * g, Alpha(MysticGoldHot, .95f), .3f).spin = 0;
                Flash(crestAt, 1.6f, Alpha(MysticGold, .7f), .25f);
            }
            // A ring of gold sweeps across the floor out to the edge of the blessing.
            var sweepInk = Spawn("Mystic war pulse ink", ring, ground, span * 1.02f, Alpha(MysticBronze, .7f), .55f, false, 25); LayFlat(sweepInk, .3f);
            var sweep = Spawn("Mystic war pulse", ring, ground, span, Alpha(MysticGold, .95f), .5f, true, 27); LayFlat(sweep, .3f);
            foreach (var x in new[] { sweepInk, sweep }) { x.startScale = .08f; x.endScale = 1; x.ease = 2.4f; x.hold = .35f; x.fadePower = 1.5f; }
            if (g >= 2) { var echo = MysticLater(Spawn("Mystic war pulse echo", ring, ground, span * .98f, Alpha(MysticGoldHot, .5f), .45f, true, 27), .08f); LayFlat(echo, .3f); echo.startScale = .08f; echo.endScale = 1; echo.ease = 2.4f; echo.fadePower = 1.5f; }
            var beat = Spawn("Mystic war pulse heart", glow, ground, 2.2f, Alpha(MysticGold, .55f), .3f, true, 26); LayFlat(beat, .32f); beat.startScale = .5f; beat.endScale = 1.2f; beat.ease = 2;
            // The edge of the reach flares as the ring arrives.
            for (int edge = -1; edge <= 1; edge += 2)
            {
                Vector2 at = ground + new Vector2(edge * R, 0);
                var post = MysticLater(Pillar(at, .75f, 1.4f + .3f * g, Alpha(awake ? MysticGoldHot : MysticGold, .75f), .4f, 27), .26f); post.hold = .2f;
                var postInk = MysticLater(MysticPigment(Pillar(at, .9f, 1.3f + .3f * g, Alpha(MysticBronze, .6f), .42f, 25), 25), .26f); postInk.hold = .2f;
                for (int i = 0; i < 2; i++) MysticLater(Spark(at + Vector2.up * .1f, new Vector2(Range(-.6f, .6f), Range(3, 5)), i == 0 ? Color.white : MysticGold, Range(.25f, .38f), Range(.35f, .5f), 2, 1.5f), .26f + i * .04f);
            }
            // Sparks of courage rise inside the reach.
            for (int i = 0, n = Budget(3); i < n; i++)
                Spark(ground + new Vector2(Range(-R, R) * .85f, Range(.1f, .4f)), new Vector2(0, Range(2.5f, 4)), awake ? MysticGoldHot : MysticGold, Range(.3f, .45f), Range(.4f, .6f), -1, .8f).flicker = .2f;
        }

        // ---------------------------------------------------------------- Bloom

        private void CastBloom(CastInfo c)
        {
            MysticEnsure();
            int g = Grade; float k = Mathf.Clamp(c.size / 1.4f, .8f, 1.5f) * (1 + .1f * (g - 1)), w = c.windup, release = Mathf.Max(0, w - .02f);
            Vector2 hand = c.pos + new Vector2(c.facing * .55f, -.12f), ground = c.pos + Vector2.down;
            float handLife = w + .1f, hold = w / handLife;
            // A seed of light glows in the cupped hands: a rose pigment, pink light, a green spark and a white heart.
            var cup = Anchored(c.anchor, hand, "Mystic seed cup pigment", glow, 1.3f * k, Alpha(MysticRose, .45f), handLife, 118, false);
            var bloomLight = Anchored(c.anchor, hand, "Mystic seed cup light", glow, 1.1f * k, Alpha(MysticPink, .85f), handLife, 125);
            var sprout = Anchored(c.anchor, hand, "Mystic seed cup sprout", glow, .6f * k, Alpha(MysticSap, .7f), handLife, 126);
            var seed = Anchored(c.anchor, hand, "Mystic seed cup heart", core, .3f * k, MysticPinkHot, handLife, 127); seed.pulse = .12f;
            foreach (var x in new[] { cup, bloomLight, sprout, seed }) { x.fadeIn = .04f; x.startScale = .3f; x.endScale = 1; x.ease = 1.5f; x.hold = hold; x.fadePower = 2; }
            if (w < .2f)
            {
                // An instant echo: the seed is thrown at once, so the echo only lights the hands.
                Converge(hand, 5, 1.4f * k, MysticPink, w, .5f);
                return;
            }
            // A small lotus bud cups the seed: green leaves below, rose petals fanning up around it.
            Vector2 stem = hand + Vector2.down * .14f * k;
            int folds = 4 + (g - 1);
            for (int i = -1; i <= folds; i++)
            {
                bool leaf = i < 0 || i == folds; float a = leaf ? (i < 0 ? -18 : 198) : Mathf.Lerp(42, 138, i / (float)Mathf.Max(1, folds - 1));
                float size = (leaf ? .62f : .5f) * k;
                Vector2 at = stem + Direction(a) * size * .42f;
                var fold = Anchored(c.anchor, at, leaf ? "Mystic cupping leaf" : "Mystic cupping petal", MysticPetal, size, leaf ? MysticLeaf : Alpha(MysticRose, .95f), w + .05f, leaf ? 119 : 120, false);
                fold.angle = a; fold.fadeIn = .1f; fold.startScale = .3f; fold.endScale = 1; fold.ease = 2; fold.hold = .85f; fold.fadePower = 2;
                if (leaf) continue;
                var lit = Anchored(c.anchor, at, "Mystic cupping petal light", MysticPetal, size * .85f, Alpha(MysticPink, .65f), w + .05f, 126);
                lit.angle = a; lit.fadeIn = .1f; lit.startScale = .3f; lit.endScale = 1; lit.ease = 2; lit.hold = .85f; lit.fadePower = 2;
            }
            Converge(hand, 6, 2.2f * k, MysticPink, w, .5f);
            Converge(hand, 5, 2 * k, MysticSap, w, .4f);
            // A flower seal opens on the floor under the caster.
            float total = w + .25f;
            var sealInk = Spawn("Mystic seed seal ink", MysticBloomSeal, ground, 3 * k, Alpha(MysticRose, .55f), total, false, 25); LayFlat(sealInk, .32f);
            var seal = Spawn("Mystic seed seal", MysticBloomSeal, ground, 2.8f * k, Alpha(MysticPink, .85f), total, true, 27); LayFlat(seal, .32f);
            foreach (var x in new[] { sealInk, seal }) { x.spin = 30; x.fadeIn = .08f; x.startScale = .4f; x.endScale = 1; x.ease = 3; x.hold = w / total; x.fadePower = 1.5f; }
            GroundGlow(ground, 3.2f * k, Alpha(Color.Lerp(MysticPink, MysticSap, .5f), .4f), total).hold = w / total;
            // Release: a glint and a puff of petals as the seed is flicked.
            MysticLater(Glint(hand, 1.8f * k, Alpha(MysticPinkHot, .95f), .22f), release).spin = 0;
            MysticLater(Flash(hand, 1.6f * k, Alpha(MysticPink, .8f), .2f), release);
            Vector2 toward = (c.target - hand).sqrMagnitude > .01f ? (c.target - hand).normalized : Vector2.right * c.facing;
            for (int i = 0, n = Budget(5); i < n; i++)
            {
                var puff = MysticLater(Spawn("Mystic seed release petal", MysticPetal, hand, Range(.26f, .38f) * k, i % 2 == 0 ? Alpha(MysticRose, .95f) : Alpha(MysticPinkHot, .9f), Range(.5f, .7f), i % 2 == 1, i % 2 == 0 ? 121 : 129), release);
                puff.velocity = (toward + Inside(.8f)) * Range(2, 3.5f); puff.drag = 2.5f; puff.spin = Range(-500, 500); puff.angle = Range(0, 360); puff.hold = .3f; puff.fadePower = 1.4f;
            }
        }

        private void TrailBloom(Vector2 from, Vector2 to, Color tint)
        {
            MysticEnsure();
            Vector2 delta = to - from; float length = delta.magnitude; if (length < .05f) return;
            int g = Grade; float sz = 1 + .15f * (g - 1);
            // A quick lob, so the growing seed other layers draw on the host follows its landing closely.
            float travel = Mathf.Clamp(length / 22, .12f, .24f), apex = Mathf.Clamp(length * .14f, .4f, 1);
            MysticSeedFrame = Time.frameCount; MysticSeedEnd = to; MysticSeedTravel = travel;
            MysticSeedThrows.RemoveAll(x => Time.frameCount - x.frame > 1800);
            if (MysticSeedThrows.Count >= 12) MysticSeedThrows.RemoveAt(0);
            MysticSeedThrows.Add(new MysticSeedThrow { frame = Time.frameCount, from = from, to = to });
            float fall = 8 * apex / (travel * travel);
            // Each step adds velocity before moving, so lift the throw by half a frame of fall to land on the target.
            Vector2 throwV = delta / travel + new Vector2(0, fall * travel * .5f + fall / 120f);
            // The seed: a hot heart in a pink bloom and a rose husk, wrapped by two spinning leaves.
            var husk = Spawn("Mystic seed husk", glow, from, .9f * sz, Alpha(MysticRose, .7f), travel, false, 120);
            var halo = Spawn("Mystic seed halo", glow, from, 1.1f * sz, Alpha(MysticPink, .9f), travel, true, 127);
            var heart = Spawn("Mystic seed heart", core, from, .34f * sz, MysticPinkHot, travel, true, 129);
            var leafA = Spawn("Mystic seed leaf", petal, from, .44f * sz, MysticLeaf, travel, false, 121); leafA.spin = 900;
            var leafB = Spawn("Mystic seed leaf light", petal, from, .4f * sz, Alpha(MysticSap, .8f), travel, true, 128); leafB.spin = 900; leafB.angle = 180;
            foreach (var x in new[] { husk, halo, heart, leafA, leafB }) { x.velocity = throwV; x.gravity = fall; x.fadePower = .3f; }
            // A trail of sparkles and tiny petals behind it.
            Emitter(halo, 45 + 10 * (g - 1), .05f, (owner, p) =>
            {
                if (Chance(.3f))
                {
                    var bit = Spawn("Mystic seed trail petal", petal, p, Range(.14f, .22f) * sz, Chance(.5f) ? Alpha(MysticRose, .9f) : MysticLeaf, Range(.35f, .55f), false, 121);
                    bit.velocity = new Vector2(Range(-.4f, .4f), Range(-.6f, .2f)); bit.spin = Range(-500, 500); bit.angle = Range(0, 360); bit.fadePower = 1.3f;
                    return;
                }
                var mote = Spawn("Mystic seed trail light", glow, p, Range(.14f, .26f) * sz, Alpha(Chance(.6f) ? MysticPink : MysticSap, .9f), Range(.25f, .45f), true, 126);
                mote.velocity = new Vector2(Range(-.3f, .3f), Range(-.4f, .3f)); mote.flicker = .4f; mote.endScale = .2f; mote.ease = 1;
            });
            Glint(from, .9f * sz, Alpha(MysticPinkHot, .9f), .16f).spin = 0;
            // The seed lands: a soft pop of pink light, a green ring and a few petals.
            MysticLater(Flash(to, 1.5f * sz, Alpha(MysticPink, .8f), .2f), travel);
            MysticLater(Wave(to, 1.8f * sz, Alpha(MysticSap, .75f), .3f, 3), travel);
            MysticLater(Glint(to, 1.3f * sz, Alpha(MysticPinkHot, .95f), .25f), travel).spin = 0;
            for (int i = 0, n = Budget(3); i < n; i++)
            {
                var bit = MysticLater(Spawn("Mystic seed landing petal", MysticPetal, to, Range(.22f, .32f) * sz, i % 2 == 0 ? Alpha(MysticRose, .95f) : MysticLeaf, Range(.45f, .65f), false, 121), travel);
                bit.velocity = Direction(i * 360f / n + Range(-20, 20)) * Range(1.8f, 3) + Vector2.up; bit.drag = 2.5f; bit.spin = Range(-500, 500); bit.angle = Range(0, 360); bit.hold = .3f; bit.fadePower = 1.4f;
            }
        }

        private void ImpactBloom(Vector2 pos, float radius, Color tint, int facing)
        {
            MysticEnsure();
            int g = Grade; bool lush = g >= 3;
            float R = Mathf.Clamp(radius, .8f, 3.5f), s = Mathf.Clamp(.55f + R * .28f, .85f, 1.4f) * (1 + .06f * (g - 1));
            Vector2 heart = pos + Vector2.up * .15f, ground = pos + Vector2.down * .8f;
            float life = 1.2f + .05f * (g - 1);
            // Seeds blooming together near each other (one cast on several foes) share the first flower's light: a follower
            // draws only its petals, seal and motes.
            bool lead = !(MysticBloomFrame == Time.frameCount && (MysticBloomAt - pos).sqrMagnitude < 9);
            if (lead) { MysticBloomFrame = Time.frameCount; MysticBloomAt = pos; }
            // Petal tips (about 1.1 lengths from the bud once open) stay inside the strike radius; leaves reach furthest.
            float leafCap = R * .82f, petalCap = R * .74f, backCap = R * .7f;

            // A soft shade and a rose pigment bloom behind the actors, and pink light bursting from the seed.
            if (lead)
            {
                var shade = Underlay(heart, 4 * s, .3f, .8f); shade.hold = .3f;
                var tintBloom = Spawn("Mystic bloom pigment", glow, heart, 3.4f * s, Alpha(MysticRose, .5f), .8f, false, 29);
                tintBloom.startScale = .4f; tintBloom.endScale = 1.15f; tintBloom.ease = 3; tintBloom.hold = .25f; tintBloom.fadePower = 1.4f;
                var bloom = Flash(heart, 2.8f * s, Alpha(MysticPink, .6f), .3f); bloom.ramp = Ramp(MysticPinkHot, MysticPink); bloom.startScale = .4f; bloom.endScale = 1.2f; bloom.ease = 3;
                var pop = Spawn("Mystic bloom heart", core, heart, .9f * s, Alpha(MysticPinkHot, .95f), .13f, true, 133); pop.startScale = .4f; pop.endScale = 1.6f; pop.ease = 2.5f; pop.fadePower = 1.5f;
                MysticPigment(ImpactLines(heart, Mathf.Min(5.6f * s, R * 2.8f), Alpha(MysticRose, .75f), .34f), 119);
                ImpactLines(heart, Mathf.Min(5 * s, R * 2.5f), Alpha(MysticPinkHot, .55f), .3f);
                // A mandala of light opens behind the flower.
                Vector2 mandalaAt = heart + Vector2.up * .3f * s; float mandalaW = Mathf.Min(3.4f * s, R * 2);
                var mandalaInk = Spawn("Mystic bloom mandala ink", MysticBloomSeal, mandalaAt, mandalaW * 1.03f, Alpha(MysticRose, .4f), life, false, 26);
                var mandala = Spawn("Mystic bloom mandala", MysticBloomSeal, mandalaAt, mandalaW, Alpha(MysticPink, .45f), life, true, 27);
                foreach (var x in new[] { mandalaInk, mandala }) { x.spin = 25; x.startScale = .4f; x.endScale = 1.05f; x.ease = 5; x.fadeIn = .03f; x.hold = .4f; x.fadePower = 1.5f; }
            }

            // The lotus: (lush) broad leaves spread at its foot, a back and a front row of rose petals fan open out of the bud and
            // a crown of pale petals stands in the middle; they hold, then loosen. Leaves and the back row stand behind the
            // actors, so the host and its neighbours stay visible through the opening flower.
            Vector2 cup = heart + Vector2.down * .3f * s;
            if (lush)
            {
                for (int i = 0; i < 4; i++)
                {
                    float a = i < 2 ? -14 - i * 22 : 194 + (i - 2) * 22, len0 = Mathf.Min((i % 2 == 0 ? 1.9f : 1.5f) * s, leafCap * (i % 2 == 0 ? 1 : .82f));
                    MysticLotus(Spawn("Mystic bloom leaf", MysticPetal, cup, len0, MysticLeaf, life, false, 27), cup, a, len0 * .45f, a > 90 ? 6 : -6, .5f);
                    MysticLotus(Spawn("Mystic bloom leaf light", MysticPetal, cup, len0 * .85f, Alpha(MysticSap, .35f), life * .9f, true, 28), cup, a, len0 * .45f, a > 90 ? 6 : -6, .5f);
                }
            }
            int back = lush ? 6 : 5; float lenBack = Mathf.Min(1.2f * s, backCap);
            for (int i = 0; i < back; i++)
            {
                float a = Mathf.Lerp(28, 152, i / (float)(back - 1)) + Range(-3, 3), drift = a > 90 ? 8 : -8;
                MysticLotus(Spawn("Mystic bloom back petal", MysticPetal, cup, lenBack, Alpha(Color.Lerp(MysticRose, new Color(.7f, .1f, .4f), .35f), .92f), life, false, 28), cup, a, lenBack * .45f, drift, .5f);
                MysticLotus(Spawn("Mystic bloom back light", MysticPetal, cup, lenBack * .85f, Alpha(MysticPink, .16f), life * .85f, true, 29), cup, a, lenBack * .45f, drift, .5f);
            }
            // The front row covers the host only while it opens: it starts to fade sooner than the rest.
            int front = lush ? 7 : 6; float len = Mathf.Min(1.4f * s, petalCap);
            for (int i = 0; i < front; i++)
            {
                float a = Mathf.Lerp(-6, 186, i / (float)(front - 1)) + Range(-3, 3), drift = a > 90 ? 12 : -12;
                MysticLotus(Spawn("Mystic bloom petal", MysticPetal, cup, len, Alpha(Color.Lerp(MysticRose, new Color(1, .45f, .7f), Range(0, .4f)), .95f), life, false, 120), cup, a, len * .45f, drift, .32f);
                MysticLotus(Spawn("Mystic bloom petal light", MysticPetal, cup, len * .85f, Alpha(MysticPink, .2f), life * .8f, true, 126), cup, a, len * .45f, drift, .32f);
                if (lush && i % 3 == 0) MysticLotus(Spawn("Mystic bloom petal gold", MysticPetal, cup, len * 1.06f, Alpha(MysticGoldHot, .26f), life * .6f, true, 125), cup, a, len * .47f, drift, .32f);
            }
            int crown = lush ? 4 : 3; float len2 = Mathf.Min(.95f * s, petalCap * .7f);
            for (int i = 0; i < crown; i++)
            {
                float a = Mathf.Lerp(70, 110, i / (float)(crown - 1));
                MysticLotus(Spawn("Mystic bloom crown petal", MysticPetal, cup, len2, new Color(1, .74f, .87f, .95f), life * .95f, false, 127), cup, a, len2 * .45f, (a - 90) * .15f, .4f);
                MysticLotus(Spawn("Mystic bloom crown light", MysticPetal, cup, len2 * .82f, Alpha(MysticPinkHot, .3f), life * .75f, true, 128), cup, a, len2 * .45f, (a - 90) * .15f, .4f);
            }
            // The stamen: a gold heart with a little pollen light.
            var stamen = Spawn("Mystic bloom stamen", core, heart, .45f * s, Alpha(MysticGoldHot, .85f), life * .6f, true, 130);
            stamen.fadeIn = .05f; stamen.pulse = .1f; stamen.hold = .35f; stamen.fadePower = 1.4f; stamen.startScale = .3f; stamen.endScale = 1; stamen.ease = 5;
            if (lead)
            {
                var pollen = Spawn("Mystic bloom pollen glow", glow, heart, 1.1f * s, Alpha(MysticGold, .35f), life * .6f, true, 129); pollen.fadeIn = .05f; pollen.hold = .3f; pollen.fadePower = 1.5f;
                Glint(heart, 2.2f * s, Alpha(MysticPinkHot, .9f), .32f).spin = 30;
                Sparkles(heart, 5, .5f * s, MysticGoldHot);
                // Healing light: a soft pink-green column behind the actors.
                float columnH = (3.2f + .5f * (g - 1)) * Mathf.Min(s, 1.2f);
                var column = Pillar(ground, 1.5f * s, columnH, Alpha(MysticSap, .4f), .9f, 26); column.hold = .2f;
                var columnTint = MysticPigment(Pillar(ground, 1.8f * s, columnH * .92f, Alpha(MysticRose, .28f), .95f, 24), 24); columnTint.hold = .2f;
            }

            // A flower seal opens across the strike radius; a ring runs out to its edge.
            float span = 2 * R / .95f, sealLife = 1.3f + .2f * (g - 1);
            var sealInk = Spawn("Mystic bloom seal ink", MysticBloomSeal, ground, span * 1.02f, Alpha(MysticRose, .6f), sealLife, false, 25); LayFlat(sealInk, .32f);
            var seal = Spawn("Mystic bloom seal", MysticBloomSeal, ground, span, Alpha(MysticPink, .9f), sealLife, true, 27); LayFlat(seal, .32f);
            foreach (var x in new[] { sealInk, seal }) { x.spin = 18; x.fadeIn = .03f; x.startScale = .5f; x.endScale = 1; x.ease = 6; x.hold = .45f; x.fadePower = 1.3f; }
            GroundWave(ground, 2 * R / .74f, Alpha(MysticSap, .8f), .5f);
            if (lead)
            {
                GroundGlow(ground, span * 1.1f, Alpha(MysticPink, .3f), 1.1f);
                Wave(heart, 3 * s, Alpha(MysticPinkHot, .6f), .42f, 3);
                MysticRing(heart, 3.2f * s, Alpha(MysticRose, .7f), .45f, false, 119);
            }

            // A storm of petals and leaves whirls out once the flower is open; healing motes rise.
            for (int i = 0, n = Budget(lead ? 9 : 5); i < n; i++)
            {
                bool lit = i % 4 == 0, leafy = i % 5 == 2;
                Color tone = leafy ? MysticLeaf : lit ? Alpha(MysticPinkHot, .9f) : Alpha(Color.Lerp(MysticRose, MysticPink, Range(0, .5f)), .95f);
                var flake = MysticLater(Spawn("Mystic bloom storm petal", leafy ? petal : MysticPetal, heart + Inside(.3f * s), Range(.3f, .48f) * s, tone, Range(1, 1.5f), lit, lit ? 130 : 121), Range(.15f, .32f));
                flake.velocity = Direction(i * 360f / n + Range(-15, 15)) * Range(2.5f, 4.5f) * s + Vector2.up * 1.2f;
                flake.drag = 1.6f; flake.gravity = -.5f; flake.spin = Range(-500, 500); flake.angle = Range(0, 360); flake.hold = .4f; flake.fadePower = 1.3f;
            }
            for (int i = 0, n = Budget(7); i < n; i++)
                MysticLater(MysticHealMote(i % 2 == 0 ? heart + Inside(.6f * s) : ground + new Vector2(Range(-R, R) * .8f, Range(-.15f, .15f)), i % 4 == 3, s), Range(.05f, .45f));

            // The seed this flower grew from: where it was thrown from (the caster), so the second wave of life can go back there.
            int thrown = -1; float nearest = 2.5f;
            MysticSeedThrows.RemoveAll(x => Time.frameCount - x.frame > 1800);
            for (int i = 0; i < MysticSeedThrows.Count; i++) { float dx = Mathf.Abs(MysticSeedThrows[i].to.x - pos.x); if (dx < nearest) { nearest = dx; thrown = i; } }
            Vector2 caster = thrown >= 0 ? MysticSeedThrows[thrown].from : Vector2.zero;
            if (thrown >= 0) MysticSeedThrows.RemoveAt(thrown);
            if (lush)
            {
                // Level three heals double: a second wave of life. Green-gold light streams from the flower back toward the one who
                // planted it (or, when that is unknown, rises in a spiral), with a green ring and a gold star.
                if (lead)
                {
                    Rays(heart, 6, Mathf.Min(1.6f * s, R * .7f), Mathf.Min(2.8f * s, R * 1.1f), MysticSap, .34f);
                    MysticLater(Wave(heart, Mathf.Min(3.6f * s, R * 2), Alpha(MysticSap, .65f), .5f, 3), .25f);
                    MysticLater(GroundWave(ground, 2 * R / .74f, Alpha(MysticSap, .7f), .5f), .25f);
                    MysticLater(Glint(heart, 2 * s, Alpha(MysticGoldHot, .8f), .28f), .25f).spin = 0;
                }
                bool aimed = thrown >= 0 && (caster - heart).sqrMagnitude > 1;
                for (int i = 0, n = Budget(aimed ? 6 : 4); i < n; i++)
                {
                    float wait = .3f + i * .05f + Range(0, .08f), flight = Range(.65f, .85f);
                    Vector2 from = heart + Inside(.5f * s);
                    var stream = MysticLater(Spawn("Mystic second life", i % 2 == 0 ? spark : wisp, from, i % 2 == 0 ? Range(.4f, .55f) : Range(.2f, .3f), Alpha(i % 3 == 0 ? MysticGoldHot : MysticSap, .95f), flight, true, 131), wait);
                    if (aimed)
                    {
                        // An arc that lands on the caster's hand after the flight (gravity bends it), fading on the way in.
                        const float bend = 4;
                        Vector2 to = caster + Inside(.3f);
                        stream.velocity = (to - from) / flight + Vector2.up * bend * flight * .5f; stream.gravity = bend;
                    }
                    else { stream.velocity = new Vector2(Range(-.6f, .6f), Range(2, 3.2f)); stream.drag = .3f; }
                    stream.alignVelocity = i % 2 == 0; stream.fadeIn = .06f; stream.hold = .55f; stream.fadePower = 1.4f; stream.endScale = .45f; stream.ease = 1; stream.flicker = .2f;
                }
            }
            if (lead) { var after = Spawn("Mystic bloom afterglow", glow, heart, 2.4f * s, Alpha(Color.Lerp(MysticPink, MysticSap, .4f), .15f), 1.1f, true, 122); after.fadeIn = .15f; after.fadePower = 1.6f; after.pulse = .05f; }
        }

        /// <summary>A petal of an opening lotus: it grows out of the bud at <paramref name="cupAt"/> along
        /// <paramref name="angle"/> (base at the cup, tip outward), then drifts open and loosens as it fades after <paramref name="hold"/>.</summary>
        private static void MysticLotus(Clip clip, Vector2 cupAt, float angle, float reach, float drift, float hold)
        {
            clip.orbitCenter = cupAt; clip.orbitAngle = angle; clip.orbit = drift; clip.spin = drift;
            clip.orbitRadius = reach * .3f; clip.orbitRadiusEnd = reach * 1.35f; clip.orbitPower = .3f;
            clip.angle = angle; clip.startScale = .25f; clip.endScale = 1; clip.ease = 7; clip.fadeIn = .03f; clip.hold = hold; clip.fadePower = 1.4f;
        }

        private Clip MysticHealMote(Vector2 at, bool leaf, float scale)
        {
            if (leaf)
            {
                var l = Spawn("Mystic rising leaf", petal, at, Range(.2f, .32f) * scale, Chance(.5f) ? MysticLeaf : Alpha(MysticRose, .9f), Range(.7f, 1.1f), false, 121);
                l.velocity = new Vector2(Range(-.4f, .4f), Range(1.2f, 2.4f)); l.drag = .5f; l.spin = Range(-300, 300); l.angle = Range(0, 360); l.fadeIn = .08f; l.hold = .4f; l.fadePower = 1.3f;
                return l;
            }
            bool streakMote = Chance(.35f);
            var mote = Spawn("Mystic healing light", streakMote ? spark : wisp, at, (streakMote ? Range(.28f, .45f) : Range(.14f, .26f)) * scale, Alpha(Chance(.6f) ? MysticSap : MysticPinkHot, .95f), Range(.6f, 1), true, 131);
            mote.velocity = new Vector2(Range(-.3f, .3f), Range(1.5f, 3)); mote.drag = .35f; mote.fadeIn = .08f; mote.endScale = .35f; mote.ease = 1; mote.flicker = .25f; mote.alignVelocity = streakMote;
            return mote;
        }

        // ---------------------------------------------------------------- shared helpers

        private static Clip MysticLater(Clip clip, float delay)
        {
            if (delay > 0) { clip.age = -delay; if (clip.renderer) clip.renderer.enabled = false; }
            return clip;
        }

        /// <summary>Turns an additive clip into an alpha-blended pigment or ink layer.</summary>
        private static Clip MysticPigment(Clip clip, int order) { clip.renderer.sharedMaterial = AlphaBlend; clip.renderer.sortingOrder = order; return clip; }

        private static void MysticStretch(Clip clip, float width, float height)
        {
            Vector3 size = clip.renderer.sprite.bounds.size;
            clip.scale = new Vector3(width / size.x, height / size.y, 1); clip.root.transform.localScale = clip.scale;
        }

        /// <summary>A squashed child of a zone root: clips attached to it turn in the ground plane and die with the root.</summary>
        private static Transform MysticPlane(Transform root, Vector2 offset, float squash)
        {
            var plane = new GameObject("Mystic ground plane").transform;
            plane.SetParent(root, false); plane.localPosition = offset; plane.localRotation = Quaternion.identity; plane.localScale = new Vector3(1, squash, 1);
            return plane;
        }

        /// <summary>The ease that makes a scale change finish within <paramref name="seconds"/> of a clip's <paramref name="life"/>.</summary>
        private static float MysticPop(float life, float seconds)
        {
            float f = Mathf.Clamp(seconds / Mathf.Max(.01f, life), .01f, .9f);
            return Mathf.Clamp(Mathf.Log(.04f) / Mathf.Log(1 - f), 1, 80);
        }

        private static Vector2 MysticOnGround(Clip owner, Vector2 p)
        {
            Vector2 o = owner.root.transform.position; return o + new Vector2(p.x - o.x, (p.y - o.y) * .3f);
        }

        private Clip MysticRing(Vector2 pos, float width, Color color, float life, bool additive, int order)
        {
            var clip = Spawn("Mystic ring", ring, pos, width, color, life, additive, order); clip.startScale = .12f; clip.endScale = 1; clip.ease = 3; clip.fadePower = 1.2f; return clip;
        }

        /// <summary>A ragged ink stroke segment; segments overlap where their ends fade, so a chain reads as one stroke.</summary>
        private Clip MysticBrush(Vector2 a, Vector2 b, float thickness, Color color, float life, bool additive, int order)
        {
            Vector2 delta = b - a; float length = Mathf.Max(.02f, delta.magnitude) * 1.16f;
            var clip = Spawn("Mystic brush", MysticStroke, (a + b) * .5f, length, color, life, additive, order);
            clip.scale = new Vector3(length / MysticStroke.bounds.size.x, thickness / MysticStroke.bounds.size.y, 1);
            clip.angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg; clip.ease = 1;
            clip.root.transform.localScale = clip.scale; clip.root.transform.localRotation = Quaternion.Euler(0, 0, clip.angle);
            return clip;
        }

        // ---------------------------------------------------------------- textures

        // Built once after the first scene loads (the core builds its own textures when the effects are created), and
        // again on first use if that has not happened.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void MysticPrebuild() { MysticEnsure(); }

        private static void MysticEnsure()
        {
            if (MysticSpiral) return;

            // A wind arc (a comet stroke along a circle): bright head at angle 0, the tail trailing clockwise, so a clip
            // spinning counter-clockwise leads with its head.
            MysticArc = Build("Mystic wind arc", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float t = Mathf.Repeat(-Mathf.Atan2(v, u) * Mathf.Rad2Deg, 360) / 230f; if (t > 1) return Color.clear;
                float w = .065f * (1 - .6f * t) + .01f, fade = Mathf.Pow(1 - t, 1.5f) * Smooth(0, .025f, t);
                float a = (Gauss(r - .86f, w) + Gauss(r - .86f, w * 3) * .25f) * fade + Gauss(Mathf.Sqrt(Sq(u - .86f) + v * v), .07f) * .5f;
                return White(Mathf.Clamp01(a) * Window(r));
            });

            // A wall of water in the funnel: a wide streaky band along most of a circle, densest at its leading edge.
            MysticVeil = Build("Mystic water wall", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float t = Mathf.Repeat(-Mathf.Atan2(v, u) * Mathf.Rad2Deg, 360) / 290f; if (t > 1) return Color.clear;
                float streaks = .65f + .35f * Mathf.PerlinNoise(r * 22 + 3, t * 4 + 1);
                float a = Gauss(r - .82f, .1f * (1 - .4f * t) + .03f) * Mathf.Pow(1 - t, 1.2f) * Smooth(0, .05f, t) * streaks;
                return White(Mathf.Clamp01(a * 1.1f) * Window(r));
            });

            // The body of the vortex funnel (bottom pivot): a cone narrow at the floor and open at the top, densest along its walls
            // and streaked with slanting bands of water, so its middle stays thin over whatever stands inside it.
            MysticFunnel = Build("Mystic funnel", 128, 256, (u, v) =>
            {
                float h = (v + 1) * .5f, half = Mathf.Lerp(.32f, .98f, Mathf.Pow(h, .8f)), x = Mathf.Abs(u) / half;
                if (x > 1.05f) return Color.clear;
                float body = 1 - Smooth(.82f, 1.02f, x), walls = .3f + .7f * Smooth(.35f, .92f, x);
                float bands = .62f + .38f * Mathf.Sin((u * 2.4f - h * 7.5f) * Mathf.PI) * (.6f + .4f * Mathf.PerlinNoise(u * 3 + 7, h * 9 + 2));
                float ends = Smooth(0, .07f, h) * (1 - Smooth(.84f, 1, h));
                return White(Mathf.Clamp01(body * walls * bands * ends * 1.1f));
            }, new Vector2(.5f, 0));

            // A lotus petal (base at -u, pointed tip at +u): darker toward the base, a bright rim and fine veins.
            MysticPetal = Build("Mystic lotus petal", 96, 48, (u, v) =>
            {
                float t = (u + 1) * .5f, half = .86f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.PI * Mathf.Pow(t, 1.3f))), .8f);
                float inside = 1 - Smooth(half - .12f, half + .02f, Mathf.Abs(v));
                if (inside <= 0) return Color.clear;
                float edge = Mathf.Abs(v) / Mathf.Max(.05f, half);
                float veins = Gauss(v, .05f) * .16f + Gauss(Mathf.Abs(v) - half * .45f, .04f) * .1f * t;
                float l = Mathf.Clamp01(.52f + .42f * Mathf.Pow(t, .8f) + .14f * Smooth(.6f, 1, edge) + veins);
                return new Color(l, l, l, inside);
            });

            // A ragged ink stroke (along +x) whose ends fade where chained segments overlap.
            MysticStroke = Build("Mystic ink stroke", 128, 32, (u, v) =>
            {
                float n = Mathf.PerlinNoise(u * 5 + 3, 1.7f), m = Mathf.PerlinNoise(u * 11 + 9, v * 3 + 4);
                float edge = .55f + .3f * n, body = 1 - Smooth(edge - .25f, edge + .05f, Mathf.Abs(v) + (m - .5f) * .25f);
                float ends = 1 - Smooth(.74f, .99f, Mathf.Abs(u));
                return White(Mathf.Clamp01(body * (.82f + .18f * m)) * ends * (1 - Smooth(.88f, 1, Mathf.Abs(v))));
            });

            // The curse brand: a ticked double ring, eight thorns reaching inward and a slit-pupil eye.
            MysticBrand = Build("Mystic curse brand", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float th = Mathf.Atan2(v, u), deg = Mathf.Repeat(th * Mathf.Rad2Deg, 360);
                float a = Gauss(r - .93f, .016f) + Gauss(r - .85f, .01f) * .85f;
                float tick = Mathf.Abs(Mathf.Repeat(deg + 7.5f, 15) - 7.5f);
                a += (1 - Smooth(1, 2, tick)) * Smooth(.865f, .875f, r) * (1 - Smooth(.905f, .915f, r)) * .85f;
                float sector = Mathf.Repeat(th, Mathf.PI / 4) - Mathf.PI / 8, along = (.85f - r) / .26f;
                if (along > 0 && along < 1) { float half = .075f * (1 - along); a += (1 - Smooth(half - .012f, half + .004f, Mathf.Abs(sector) * r)) * .9f; }
                float ex = u / .62f, lid = .3f * Mathf.Max(0, 1 - ex * ex);
                if (Mathf.Abs(ex) < 1.02f) a += Gauss(Mathf.Abs(v) - lid, .024f) * .95f;
                if (Mathf.Abs(v) < lid) a += Gauss(r - .2f, .016f) * .7f + .1f;
                float pupil = Mathf.Sqrt(Sq(u / .07f) + Sq(v / .23f));
                a += (1 - Smooth(.8f, 1, pupil)) * .95f + (1 - Smooth(.1f, .86f, r)) * .05f;
                return White(Mathf.Clamp01(a) * (1 - Smooth(.96f, .995f, r)));
            });

            // The banner pole (bottom pivot): dark wood with a lit side, gold bands and a dark outline.
            MysticPole = Build("Mystic banner pole", 32, 256, (u, v) =>
            {
                float h = (v + 1) * .5f, x = Mathf.Abs(u), ends = Smooth(0, .01f, h) * (1 - Smooth(.99f, 1, h));
                Color c = new Color(.07f, .035f, .02f, (1 - Smooth(.68f, .84f, x)) * ends);
                float shaft = (1 - Smooth(.42f, .56f, x)) * ends;
                float l = .6f + .55f * Mathf.Sqrt(Mathf.Max(0, 1 - Sq(u / .56f))) - u * .3f, grain = .85f + .15f * Mathf.PerlinNoise(u * 3 + 5, h * 60);
                float band = Mathf.Max(Mathf.Max(1 - Smooth(.012f, .02f, Mathf.Abs(h - .06f)), 1 - Smooth(.008f, .014f, Mathf.Abs(h - .52f))), 1 - Smooth(.012f, .02f, Mathf.Abs(h - .95f)));
                Color body = Color.Lerp(new Color(.38f, .2f, .09f) * (l * grain), new Color(1, .8f, .38f) * l, band); body.a = shaft;
                return Over(c, body);
            }, new Vector2(.5f, 0));

            // The crest: a gold spear blade over a rayed sun disc with a crimson jewel, on a socket, outlined dark.
            MysticCrest = Build("Mystic banner crest", 128, 128, (u, v) =>
            {
                Color c = new Color(.12f, .05f, .01f, MysticCrestMask(u, v, .07f) * .95f);
                float body = MysticCrestMask(u, v, 0);
                if (body <= 0) return c;
                float d = Mathf.Sqrt(u * u + Sq(v + .3f));
                float l = Mathf.Clamp(.78f + .3f * (v * .25f - u * .6f) + Gauss(u, .025f) * .35f * Smooth(.1f, .2f, v), .4f, 1.25f);
                Color paint = Color.Lerp(new Color(1, .78f, .3f) * l, new Color(.85f, .08f, .12f) * (1.1f - d * 3), 1 - Smooth(.09f, .11f, d)); paint.a = body;
                return Over(c, paint);
            });

            // The flower seal on the floor: ten petal outlines with veins, pearls between the tips and five seeds.
            MysticBloomSeal = Build("Mystic bloom seal", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float th = Mathf.Atan2(v, u);
                float a = Gauss(r - .95f, .013f) + Gauss(r - .89f, .008f) * .7f + Gauss(r - .3f, .01f) * .8f;
                const float step = 2 * Mathf.PI / 10;
                float local = Mathf.Repeat(th + step * .5f, step) - step * .5f, t = (r - .3f) / .55f;
                if (t > 0 && t < 1.02f)
                {
                    float half = step * .46f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)), .7f);
                    a += Gauss((Mathf.Abs(local) - half) * r, .012f) * .9f;
                    if (Mathf.Abs(local) < half) a += .08f + Gauss(local * r, .006f) * .4f * (1 - t);
                }
                float pearl = Mathf.Repeat(th, step) - step * .5f;
                a += Gauss(Mathf.Sqrt(Sq(pearl * r) + Sq(r - .92f)), .02f) * .8f;
                for (int i = 0; i < 5; i++) { float pa = i * 72 * Mathf.Deg2Rad; a += Gauss(Mathf.Sqrt(Sq(u - Mathf.Cos(pa) * .15f) + Sq(v - Mathf.Sin(pa) * .15f)), .035f) * .8f; }
                a += Gauss(r, .06f) * .6f + (1 - Smooth(.1f, .9f, r)) * .05f;
                return White(Mathf.Clamp01(a) * (1 - Smooth(.96f, .995f, r)));
            });

            // The war flag (pole side at the left) and its gold edge light, each sliced into strips that ripple separately.
            MysticFlagStrips = MysticSlice(Build("Mystic war flag", 288, 176, (u, v) => MysticFlagPaint(u, v, false)).texture, "Mystic war flag strip");
            MysticFlagGlowStrips = MysticSlice(Build("Mystic war flag glow", 288, 176, (u, v) => MysticFlagPaint(u, v, true)).texture, "Mystic war flag glow strip");

            // A whirlpool seen from above: five tightly wound arms with foam crests, an empty eye and a rippled rim.
            // Spinning it counter-clockwise makes the arms flow inward. Built last: it marks the set as complete.
            MysticSpiral = Build("Mystic whirlpool", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return Color.clear;
                float arm = .5f + .5f * Mathf.Cos(Mathf.Atan2(v, u) * 5 - Mathf.Log(r + .03f) * 6.5f);
                float body = Mathf.Pow(arm, 5), crest = Mathf.Pow(arm, 26), grain = .65f + .35f * Mathf.PerlinNoise(u * 7 + 3, v * 7 + 11);
                float a = (body * .75f * grain + crest * .6f) * Smooth(.06f, .32f, r) * (1 - Smooth(.72f, .98f, r)) + Gauss(r - .9f, .045f) * .22f * grain;
                return White(Mathf.Clamp01(a) * Window(r));
            });
        }

        private static Sprite[] MysticSlice(Texture2D texture, string name)
        {
            var strips = new Sprite[MysticStripCount]; int width = texture.width / MysticStripCount;
            for (int i = 0; i < strips.Length; i++)
            {
                strips[i] = Sprite.Create(texture, new Rect(i * width, 0, width, texture.height), new Vector2(0, .5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
                strips[i].name = name + " " + i;
            }
            return strips;
        }

        private static float MysticCrestMask(float u, float v, float grow)
        {
            float s = (v - .1f) / .88f, blade = 0;
            if (s > -grow * 1.5f && s < 1 + grow * 1.2f)
            {
                float half = .2f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.PI * Mathf.Clamp01(s))), .7f) + grow;
                blade = 1 - Smooth(half - .02f, half + .005f, Mathf.Abs(u));
            }
            float d = Mathf.Sqrt(u * u + Sq(v + .3f));
            float sun = Mathf.Max(1 - Smooth(.065f + grow - .015f, .065f + grow + .005f, Mathf.Abs(d - .38f)), 1 - Smooth(.16f + grow - .015f, .16f + grow + .005f, d));
            float ray = Mathf.Abs(Mathf.Repeat(Mathf.Atan2(v + .3f, u) * Mathf.Rad2Deg + 15, 30) - 15) / 15f, along = (d - .44f + grow) / (.22f + grow * 2);
            float rays = along > 0 && along < 1 ? 1 - Smooth(.5f * (1 - along) + grow - .06f, .5f * (1 - along) + grow, ray) : 0;
            float socket = (1 - Smooth(.1f + grow - .015f, .1f + grow + .005f, Mathf.Abs(u))) * (1 - Smooth(-.64f + grow - .02f, -.64f + grow, v));
            return Mathf.Clamp01(Mathf.Max(Mathf.Max(blade, sun), Mathf.Max(rays, socket)));
        }

        /// <summary>The swallow-tailed war flag: crimson cloth with a gold border and a gold sun, outlined dark. With
        /// <paramref name="glow"/> it returns only the gold parts as white, for the awakened edge light.</summary>
        private static Color MysticFlagPaint(float u, float v, bool glow)
        {
            const float halfH = .8f;
            float x = (u + 1) * .5f, ay = Mathf.Abs(v), tail = .8f + .2f * Mathf.Clamp01(ay / halfH);
            float d = Mathf.Min(halfH - ay, Mathf.Min((x - .015f) * 2 * MysticFlagAspect, (tail - x) * 2 * MysticFlagAspect * .82f));
            float inside = Smooth(-.012f, .012f, d);
            float ex = (x - .43f) * 2 * MysticFlagAspect, er = Mathf.Sqrt(ex * ex + v * v);
            float ray = Mathf.Abs(Mathf.Repeat(Mathf.Atan2(v, ex) * Mathf.Rad2Deg + 15, 30) - 15) / 15f, along = (er - .3f) / .24f;
            float rays = along > 0 && along < 1 ? 1 - Smooth(.45f * (1 - along) - .08f, .45f * (1 - along), ray) : 0;
            float emblem = Mathf.Clamp01(Mathf.Max(1 - Smooth(.17f, .2f, er), Mathf.Max(Gauss(er - .26f, .025f), rays)));
            float border = 1 - Smooth(.1f, .125f, d), pinstripe = Gauss(d - .16f, .012f);
            if (glow) return White(Mathf.Clamp01(border * .9f + emblem + pinstripe * .5f) * inside);
            Color c = new Color(.09f, .01f, .02f, Smooth(-.075f, -.03f, d));
            if (inside <= 0) return c;
            float grain = .86f + .14f * Mathf.PerlinNoise(x * 4 + 3, v * 9 + 1), shade = (1 - .25f * ay / halfH) * grain;
            Color paint = Color.Lerp(new Color(.8f * shade, .08f * shade, .1f * shade), new Color(1, .8f, .36f) * (.85f + .15f * grain), Mathf.Max(border, Mathf.Max(emblem, pinstripe * .8f)));
            paint.a = inside;
            return Over(c, paint);
        }
    }
}
