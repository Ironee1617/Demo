using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Mystic arts: the gathering vortex, the creeping curse, the war banner and the blooming seed, drawn as painted light over
    // soft blooms, with painted objects (cloth, pole, crest, petals) lit softly from the upper left.
    // Vortex: a sea-teal whirlpool of painted water strokes turns on the floor in a pool of glow across the area while a funnel
    // of water walls, swirl strokes and droplets spirals inward around the gathered enemies (passing behind and in front of
    // them); gusts and needles stream in from the edge of the pull, and the end collapses into a geyser that bursts in a
    // hand-drawn teal blast with a spark and spray.
    // Curse: a violet brand gathers in the hand in a violet bloom over deep violet smoke, a luminous thorned tendril with a hot
    // thread shoots to the victim behind a plasma head, and a glowing eye sigil is stamped on it with a violet blast and spark.
    // WarBanner: a painted crimson-and-gold standard drops into the ground behind the caster in a burst of gold; its cloth
    // ripples (sliced into strips that follow a travelling wave and catch the light smoothly on each fold), a glowing star
    // turns behind its crest, a bold painted ring and a seal mark the reach of its blessing, and every second a shock front
    // sweeps out to that edge and flares there.
    // Bloom: a seed of light is lobbed onto the enemy; when it blooms a pink blast billows behind the opening flower, a gold
    // spark flares, painted petals unfold, loosen and scatter, crosses of light rise and a flower seal opens across the strike
    // radius.
    // There are no dark ink twins; where a shape needs body on a pale sky it gets a deep saturated hue of its own colour, no
    // wider than its light, or a soft underlay behind the actors.
    // Higher levels grow height, density and secondary layers; ground rings always match the gameplay radius.
    public sealed partial class TowerVfx
    {
        private static Sprite MysticSpiral, MysticArc, MysticVeil, MysticFunnel, MysticStroke, MysticBrand, MysticPole, MysticCrest, MysticBloomSeal, MysticPetal, MysticPetalPink, MysticLeafPaint;
        private static Sprite[] MysticFlagStrips, MysticFlagGlowStrips, MysticTendril, MysticTendrilLight;
        private const int MysticStripCount = 24;
        private const float MysticFlagAspect = 288f / 176f;

        private static readonly Color MysticSea = new Color(.36f, .9f, .95f), MysticFoam = new Color(.86f, 1, .98f),
            MysticDeep = new Color(0, .34f, .44f, .88f), MysticTide = new Color(.2f, .66f, .78f),
            MysticHex = new Color(.74f, .4f, 1), MysticHexHot = new Color(.95f, .84f, 1), MysticHexBody = new Color(.34f, .03f, .56f, .92f), MysticHexSmoke = new Color(.36f, .08f, .54f, .58f),
            MysticGold = new Color(1, .8f, .36f), MysticGoldHot = new Color(1, .96f, .82f),
            MysticPink = new Color(1, .62f, .84f), MysticPinkHot = new Color(1, .93f, .97f),
            MysticSap = new Color(.55f, 1, .62f), MysticSpray = new Color(.55f, .86f, .92f, .62f), MysticPainted = new Color(1, 1, 1, .95f);

        // A droplet of the vortex funnel: it spirals in and up along a flattened circle, passing behind and in front of the actors.
        private sealed class MysticVortexMote { public Clip clip; public Color tone; public float angle, phase, rate, turn, height, lift, unit; public bool deep; }
        // A planted banner: the cloth strips follow a travelling wave every frame and the pulses make the crest flare.
        private sealed class MysticBannerRig
        {
            public Transform root; public Clip pole, poleGlow, crest, crestGlow, star, halo; public Clip[] cloth, shine;
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

            // A small whirlpool turns in the open hand: a soft teal bloom, painted water strokes and foam strokes turning
            // against each other, and a white heart.
            var bloom = Anchored(c.anchor, hand, "Mystic vortex hand bloom", glow, 2.1f * k, Alpha(MysticSea, .55f), handLife, 28); bloom.pulse = .06f;
            var lit = Anchored(c.anchor, hand, "Mystic vortex hand light", MysticSpiral, 1.35f * k, Alpha(MysticSea, 1), handLife, 126); lit.spin = 620; lit.angle = 110;
            var foam = Anchored(c.anchor, hand, "Mystic vortex hand foam", MysticSpiral, .9f * k, Alpha(MysticFoam, .9f), handLife, 127); foam.spin = 780; foam.angle = 20;
            var heart = Anchored(c.anchor, hand, "Mystic vortex hand heart", core, .32f * k, Alpha(MysticFoam, 1), handLife, 128); heart.pulse = .1f;
            foreach (var x in new[] { bloom, lit, foam, heart }) { x.fadeIn = .04f; x.startScale = .3f; x.endScale = 1; x.ease = 1.5f; x.hold = hold; x.fadePower = 2; }
            if (w < .2f)
            {
                // An instant echo: the vortex itself opens at once, so the echo only spins the hand.
                Converge(hand, 6, 1.6f * k, MysticSea, w, .6f);
                return;
            }
            var shade = Underlay(hand, 2.8f * k, .22f, w + .2f); shade.hold = .7f;
            Converge(hand, 12, 2.4f * k, MysticSea, w, .7f);
            // Painted currents of water and foam whirl into the hand.
            int currents = Budget(6);
            for (int i = 0; i < currents; i++)
            {
                Color tone = i % 3 == 0 ? MysticFoam : i % 3 == 1 ? MysticSea : MysticTide;
                var arc = Spawn("Mystic gathering current", MysticArc, hand, Range(1.3f, 2.1f) * k, Alpha(tone, 1), w * Range(.8f, 1), true, i % 3 == 2 ? 125 : 127);
                arc.spin = Range(520, 720); arc.angle = Range(0, 360); arc.startScale = 1.3f; arc.endScale = .3f; arc.ease = 1.3f; arc.fadeIn = .06f; arc.hold = .65f; arc.fadePower = 1.5f;
            }
            // A whirl opens on the floor under the caster in a pool of glow, and a faint one gathers where the vortex will open.
            float total = w + .15f;
            var pool = MysticGroundBloom(ground, 3 * k, Alpha(MysticSea, .42f), total);
            var floorLit = Spawn("Mystic vortex floor", MysticSpiral, ground, 2.4f * k, Alpha(MysticSea, .9f), total, true, 27); LayFlat(floorLit, .32f); floorLit.spin = 360;
            foreach (var x in new[] { pool, floorLit }) { x.fadeIn = .06f; x.startScale = .3f; x.endScale = 1; x.ease = 2; x.hold = w / total; x.fadePower = 1.5f; }
            float reach = Mathf.Clamp(c.radius / .3f, 1, 4);
            Vector2 site = c.target + Vector2.down * .7f;
            var previewGlow = MysticGroundBloom(site, reach * 1.9f, Alpha(MysticSea, .3f), w + .06f);
            var preview = Spawn("Mystic vortex preview", MysticSpiral, site, reach * 1.6f, Alpha(MysticSea, .75f), w + .06f, true, 27); LayFlat(preview, .32f); preview.spin = 400;
            foreach (var x in new[] { previewGlow, preview }) { x.fadeIn = w * .6f; x.startScale = .2f; x.endScale = .9f; x.ease = 1.5f; x.hold = .85f; x.fadePower = 2; }
            // Release: the hand flicks the whirl away with a spark, a shock front and a splash of droplets.
            GraceStar(hand, 2.1f * k, Alpha(MysticFoam, 1), .2f, 132, release);
            GraceWave(hand, 2.8f * k, Alpha(MysticSea, .9f), .28f, 3, 131, release);
            for (int i = 0, n = Budget(5); i < n; i++) MysticLater(MysticDroplet(hand, new Vector2(c.facing * Range(2.5f, 5), Range(-1, 2.5f)), i % 3 == 2, Range(.3f, .45f)), release);
        }

        private void ZoneVortex(Transform root, float radius, float life)
        {
            MysticEnsure();
            int g = Grade; bool awake = g >= 3;
            float R = Mathf.Clamp(radius, .6f, 5), H = 2.2f + .45f * (g - 1), disc = R * 2;
            Vector2 center = root.position, ground = center + Vector2.down * .6f;
            var layers = new List<Clip>(); var planes = new List<Transform>(); var squash = new List<float>();

            // A faint deep-teal well behind everything, so the water reads on a pale sky.
            var shade = Attach(root, "Mystic vortex shade", glow, R * 3.2f, new Color(0, .1f, .14f, .26f), 24, false, new Vector2(0, .5f));
            shade.scale.y *= .85f;

            // The whirlpool on the floor in a pool of teal glow: a deep-teal body (thinner and fainter than the light above it),
            // painted water strokes and foam strokes turning inward and a glowing eye.
            var floorPlane = MysticPlane(root, new Vector2(0, -.6f), .32f); planes.Add(floorPlane); squash.Add(.32f);
            var pitGlow = Attach(floorPlane, "Mystic whirlpool glow", glow, disc * 1.2f, Alpha(MysticSea, .38f), 25);
            var pit = Attach(floorPlane, "Mystic whirlpool body", MysticSpiral, disc * .94f, Alpha(MysticDeep, .55f), 26, false); pit.spin = 210; pit.angle = 40;
            var pitLight = Attach(floorPlane, "Mystic whirlpool light", MysticSpiral, disc * .97f, Alpha(MysticSea, .9f), 27); pitLight.spin = 250; pitLight.angle = 95;
            var froth = Attach(floorPlane, "Mystic whirlpool froth", MysticSpiral, disc * .68f, Alpha(MysticFoam, .75f), 27); froth.spin = 380; froth.angle = 200;
            var eye = Attach(floorPlane, "Mystic whirlpool eye", glow, R * .9f, Alpha(MysticFoam, .5f), 28);
            layers.AddRange(new[] { pitGlow, pit, pitLight, froth, eye });
            // The rim of light on the true edge of the pull area. From level two the grade overlay lays its own lit edge there
            // (and at three its rune seal), so the two never part into a double line.
            if (g == 1) { var rim = Attach(floorPlane, "Mystic whirlpool rim", GraceShock, disc / .74f, Alpha(MysticSea, .9f), 27); layers.Add(rim); }

            // The body of the funnel: a cone of slanting deep-teal water behind the actors (a little narrower than the light on
            // it), narrow at the floor and opening upward, inside a broad soft glow, with a foam sheen on its walls in front of
            // them; its middle stays hollow over whatever stands inside it.
            var conePlane = MysticPlane(root, new Vector2(0, -.6f), 1); planes.Add(conePlane); squash.Add(1);
            var coneGlow = Attach(conePlane, "Mystic funnel glow", GraceBeam, 2 * R * 1.5f, Alpha(MysticSea, .34f), 27); MysticStretch(coneGlow, 2 * R * 1.5f, H + 1.2f);
            var coneBack = Attach(conePlane, "Mystic funnel body", MysticFunnel, 2 * R * .88f, Alpha(MysticDeep, .6f), 28, false); MysticStretch(coneBack, 2 * R * .88f, H + .5f);
            var coneSheen = Attach(conePlane, "Mystic funnel sheen", MysticFunnel, 2 * R * .92f, Alpha(MysticFoam, .4f), 122); MysticStretch(coneSheen, 2 * R * .92f, H + .5f);
            layers.Add(coneGlow); layers.Add(coneBack); layers.Add(coneSheen);

            // The funnel: rings of painted swirl strokes, narrow at the floor and widening upward, turning faster near the
            // bottom. Deep-teal water walls pass behind the actors, sea and foam strokes in front.
            int bands = 4 + g;
            for (int j = 0; j < bands; j++)
            {
                float t = j / (float)(bands - 1), h = .25f + t * H, r = R * Mathf.Lerp(.3f, .92f, Mathf.Pow(t, .8f));
                var plane = MysticPlane(root, new Vector2(0, -.6f + h), .24f); planes.Add(plane); squash.Add(.24f);
                float width = 2 * r / .9f, turn = Mathf.Lerp(600, 320, t), start = j * 67 + Range(0, 40);
                bool top = j == bands - 1;
                var back = Attach(plane, "Mystic funnel water wall", MysticVeil, width * 1.04f, Alpha(MysticTide, .8f), 29); back.angle = start; back.spin = turn;
                var front = Attach(plane, "Mystic funnel stroke", MysticArc, width, Alpha(Color.Lerp(MysticSea, MysticFoam, t * .5f + (top ? .3f : 0)), .95f), 125); front.angle = start + 180; front.spin = turn;
                layers.Add(back); layers.Add(front);
                if (j % 2 == 1 || g >= 2) { var spray = Attach(plane, "Mystic funnel spray", MysticArc, width * .92f, Alpha(MysticFoam, .8f), 126); spray.angle = start + 95; spray.spin = turn * 1.1f; layers.Add(spray); }
                // Awakened: a fine gold edge rides the topmost stroke (the stroke itself stays sea-white).
                if (awake && top) { var gilt = Attach(plane, "Mystic funnel gold edge", MysticArc, width * 1.05f, Alpha(MysticGold, .7f), 124); gilt.angle = start + 170; gilt.spin = turn; layers.Add(gilt); }
            }

            // A thin spout of water rises in the eye inside a soft beam.
            var spoutGlow = Attach(root, "Mystic vortex spout glow", GraceBeam, R * 1.2f, Alpha(MysticSea, .4f), 28, true, new Vector2(0, -.6f)); MysticStretch(spoutGlow, R * 1.2f, H + .9f);
            var spout = Attach(root, "Mystic vortex spout", pillar, R * .5f, Alpha(MysticSea, .5f), 124, true, new Vector2(0, -.6f)); MysticStretch(spout, R * .5f, H + .8f);
            layers.Add(spoutGlow); layers.Add(spout);

            // Needle droplets spiral in and up through the funnel, passing behind and in front of whatever it holds.
            int count = 10 + 5 * g;
            var motes = new MysticVortexMote[count];
            for (int i = 0; i < count; i++)
            {
                bool deep = i % 3 == 2;
                Color tone = Alpha(deep ? MysticTide : awake && i % 4 == 1 ? MysticGoldHot : i % 4 == 0 ? MysticFoam : MysticSea, 1);
                var clip = Attach(root, "Mystic vortex droplet", spark, .4f, tone, 125);
                motes[i] = new MysticVortexMote { clip = clip, tone = tone, deep = deep, angle = Range(0, 360), phase = i / (float)count + Range(-.03f, .03f), rate = Range(.6f, .95f),
                    turn = Range(330, 470), height = Range(.1f, H * .7f), lift = Range(.3f, 1), unit = Range(.5f, .85f) / spark.bounds.size.x };
                layers.Add(clip);
            }

            // The vortex holds until it collapses (the collapse burst takes over); it only grows in.
            foreach (var l in layers) { l.life = life; l.fadeIn = .08f; l.fadePower = 0; }
            shade.life = life; shade.fadeIn = .12f; shade.fadePower = 0;
            float pop = MysticPop(life, .25f);
            foreach (var l in new[] { shade, spoutGlow, spout }) { l.startScale = .3f; l.endScale = 1; l.ease = pop; }
            var planeArray = planes.ToArray(); var squashArray = squash.ToArray();
            // The funnel is posed once per frame: the high rate keeps it running on fast displays and when emitters are thinned
            // under load, and repeated calls in one frame are skipped.
            float posed = -1;
            Emitter(shade, 240, 0, (owner, p) => { if (owner.age == posed) return; posed = owner.age; MysticSpinVortex(owner.age, life, R, motes, planeArray, squashArray); });
            // Gusts and needles stream in from the edge of the pull, spray is flung off the rim.
            Emitter(pit, 5 + 3 * g, 0, (owner, p) => MysticGust(p, R));
            Emitter(eye, 4 + 2 * g, 0, (owner, p) =>
            {
                float a = Range(0, 360) * Mathf.Deg2Rad; Vector2 at = p + new Vector2(Mathf.Cos(a) * R * .85f, Mathf.Sin(a) * R * .85f * .32f + .05f);
                MysticDroplet(at, new Vector2(-Mathf.Sin(a) * Range(1.5f, 3), Range(2.5f, 4.5f)), Chance(.35f), Range(.45f, .65f));
                if (Chance(.3f)) { var mist = Puff(at, Range(.5f, .8f), MysticSpray, new Vector2(Range(-.4f, .4f), Range(.3f, .8f)), Range(.5f, .8f), 118); mist.endScale = 1.8f; }
            });
            MysticSpinVortex(0, life, R, motes, planeArray, squashArray);
            Bundle(root, shade, layers.ToArray());

            // The vortex opens with a splash: a spark, a shock front out to the edge, and droplets.
            GraceStar(center, Mathf.Min(R * 1.5f, 4.2f), Alpha(MysticFoam, 1), .22f);
            GraceGroundWave(ground, disc / .74f, Alpha(MysticFoam, .9f), .35f);
            for (int i = 0, n = Budget(6); i < n; i++) MysticDroplet(ground + new Vector2(Range(-.3f, .3f) * R, .1f), new Vector2(Range(-2.5f, 2.5f), Range(3, 6)), i % 3 == 0, Range(.5f, .7f));
        }

        private static void MysticSpinVortex(float t, float life, float R, MysticVortexMote[] motes, Transform[] planes, float[] squash)
        {
            // Opening and closing: the whole funnel springs open, then draws in just before the collapse.
            float open = 1 - Mathf.Pow(1 - Mathf.Clamp01(t / .25f), 3), left = life - t;
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
                int order = back ? (m.deep ? 28 : 29) : (m.deep ? 121 : 126);
                if (m.clip.renderer.sortingOrder != order) m.clip.renderer.sortingOrder = order;
                // Droplets in front of the actors are bigger and brighter than the deeper ones passing behind them.
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
            var gust = Line(a, b, Range(.1f, .16f), Alpha(Chance(.4f) ? MysticFoam : MysticSea, .95f), Range(.24f, .34f), 124, 1);
            gust.velocity = new Vector2(-side * Range(4.5f, 7), 0); gust.fadeIn = .04f; gust.fadePower = 1.2f;
            // A needle of debris is dragged in with the gust.
            if (Chance(.6f)) Spark(a + new Vector2(0, Range(-.2f, .2f)), new Vector2(-side * Range(5, 8), Range(-.4f, .4f)), Chance(.3f) ? Color.white : MysticSea, Range(.32f, .5f), Range(.25f, .35f), 0, .5f);
        }

        private Clip MysticDroplet(Vector2 pos, Vector2 velocity, bool deep, float life)
        {
            var drop = Spark(pos, velocity, deep ? MysticTide : Alpha(Chance(.4f) ? MysticFoam : MysticSea, 1), Range(.3f, .46f), life, 12, .4f);
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

            // The funnel snaps shut: its rings collapse into the eye.
            for (int j = 0; j < 3; j++)
            {
                var snap = Spawn("Mystic vortex implode", GraceShock, ground + Vector2.up * (.3f + j * .8f * s), span * (1 - j * .22f), Alpha(j == 0 ? Color.Lerp(MysticSea, MysticFoam, .5f) : MysticSea, .9f), .16f, true, 128);
                LayFlat(snap, .3f); snap.startScale = 1; snap.endScale = .1f; snap.ease = 1.5f; snap.fadePower = .6f;
            }
            // A white heart, a hand-drawn blast of sea foam billowing out of the eye, a spark and spray thrown out behind the
            // actors (the gathered foes stay visible).
            var shade = Underlay(heart, Mathf.Min(R * 2.6f, 6), .28f, .6f); shade.hold = .25f;
            var flash = Spawn("Mystic vortex heart", core, ground + Vector2.up * .5f, .9f * s, Alpha(MysticFoam, 1), .12f, true, 133); flash.startScale = .4f; flash.endScale = 1.7f; flash.ease = 4; flash.fadePower = 1.3f;
            Blast(ground + Vector2.up * .8f * s, Mathf.Min(R * 1.7f, 3.8f), Alpha(Color.Lerp(MysticSea, MysticFoam, .35f), 1), .6f, 127);
            GraceStar(ground + Vector2.up * .8f, Mathf.Min(R * 2, 4.4f), Alpha(MysticFoam, 1), .28f, 132);
            // A geyser bursts up out of the eye: a soft beam behind the actors, painted water and a white spine in front.
            var spoutGlow = GraceColumn(GraceBeam, ground, 3.6f * s, tall * 1.1f, Alpha(MysticSea, .5f), .65f, true, 28); spoutGlow.hold = .2f; spoutGlow.ease = 4;
            var spout = Pillar(ground, 1.2f * s, tall * 1.05f, Alpha(MysticSea, 1), .55f, 127); spout.hold = .15f;
            Pillar(ground, .45f * s, tall * 1.1f, Alpha(MysticFoam, 1), .36f, 128).hold = .1f;
            // The geyser has a body: a column of slanting water flaring as it rises, with spray boiling off its sides.
            var body = Spawn("Mystic geyser body", MysticFunnel, ground, 1.55f * s, Alpha(MysticSea, .75f), .5f, true, 126); MysticStretch(body, 1.55f * s, tall);
            body.axisStart = new Vector2(.45f, .1f); body.axisEnd = Vector2.one; body.ease = 6; body.hold = .25f; body.fadePower = 1.3f;
            for (int i = 0, n = Budget(5); i < n; i++)
            {
                float t = (i + .5f) / n, side = i % 2 == 0 ? 1 : -1;
                var spray = MysticLater(Puff(ground + new Vector2(side * Range(.1f, .4f) * s, tall * Mathf.Lerp(.15f, .85f, t)), Range(.7f, 1.05f) * s * (.7f + .5f * t), MysticSpray,
                    new Vector2(side * Range(.6f, 1.4f), Range(.3f, .9f)), Range(.55f, .8f), 118), t * .08f);
                spray.endScale = 1.9f; spray.fadeIn = .03f;
            }
            for (int i = 0, n = Budget(3); i < n; i++)
            {
                var crownPuff = Puff(ground + new Vector2(Range(-.4f, .4f) * s, tall * Range(.55f, .8f)), Range(.7f, 1.1f) * s, MysticSpray, new Vector2(Range(-1.6f, 1.6f), Range(.5f, 1.5f)), Range(.65f, .9f), 118);
                crownPuff.endScale = 2; crownPuff.gravity = 1.5f; crownPuff.fadeIn = .05f;
            }
            // A shock front runs across the floor to the edge of the area (at level three the awakening's gold ring rides that
            // edge instead); impact wedges and droplets fly out.
            if (!awake) GraceGroundWave(ground, span, Alpha(MysticSea, .95f), .5f);
            ImpactLines(heart, Mathf.Min(R * 3, 5.4f), Alpha(MysticFoam, .95f), .28f);
            for (int i = 0, n = Budget(14); i < n; i++)
                MysticDroplet(ground + new Vector2(Range(-.4f, .4f) * s, Range(.2f, 1.2f) * s), new Vector2(Range(-1, 1) * Range(2, 6) * s, Range(4, 9) * s), i % 3 == 2, Range(.6f, 1));
            for (int i = 0, n = Budget(5); i < n; i++)
            {
                float side = i % 2 == 0 ? 1 : -1;
                var foam = Puff(ground + new Vector2(side * Range(.1f, .8f) * R, .15f), Range(.6f, 1) * s, MysticSpray, new Vector2(side * Range(1.5f, 3.5f), Range(.2f, .7f)), Range(1, 1.4f), 118);
                foam.endScale = 2.2f; foam.drag = 2; foam.hold = .3f;
            }
            // Spray rains back down over the area for a moment after the burst.
            for (int i = 0, n = Budget(6); i < n; i++)
            {
                var rain = MysticLater(MysticDroplet(ground + new Vector2(Range(-1, 1) * R * .9f, tall * Range(.5f, .9f)), new Vector2(Range(-.5f, .5f), Range(-1, 1)), i % 3 == 2, Range(.5f, .7f)), Range(.25f, .7f));
                rain.gravity = 14;
            }
            // The whirlpool spins down and drains from the floor in a fading pool of glow, its strokes eroding as it shrinks.
            var basinGlow = MysticGroundBloom(ground, 2.4f * R, Alpha(MysticSea, .4f), 1.1f);
            var basin = Spawn("Mystic vortex drain", MysticSpiral, ground, 2 * R * .96f, Alpha(MysticSea, .9f), 1.1f, true, 27); LayFlat(basin, .32f); basin.spin = 320;
            foreach (var x in new[] { basinGlow, basin }) { x.startScale = 1; x.endScale = .3f; x.ease = 1.5f; x.hold = .25f; x.fadePower = 1.2f; }
            if (awake)
            {
                // Awakened: the awakening and the grade overlay already crown this burst in gold (flash, crown, seal, edge), so
                // the vortex keeps its own water: a second, taller surge of the geyser with a thin gold core and spray.
                var surgeBody = MysticLater(Pillar(ground, 1.1f * s, tall * 1.08f, Alpha(MysticFoam, .95f), .42f, 127), .12f); surgeBody.hold = .1f;
                var surge = MysticLater(Pillar(ground, .32f * s, tall * 1.1f, Alpha(MysticGoldHot, 1), .36f, 128), .12f); surge.hold = .1f;
                for (int i = 0, n = Budget(5); i < n; i++)
                    MysticLater(Spark(ground + new Vector2(Range(-.3f, .3f), tall * Range(.3f, .9f)), Direction(Range(30, 150)) * Range(4, 8), i % 2 == 0 ? MysticFoam : MysticGoldHot, Range(.32f, .48f), Range(.4f, .6f), 8, 1.5f), .12f);
            }
        }

        // ---------------------------------------------------------------- Curse

        private void CastCurse(CastInfo c)
        {
            MysticEnsure();
            int g = Grade; float k = Mathf.Clamp(c.size / 1.4f, .8f, 1.5f) * (1 + .1f * (g - 1)), w = c.windup, release = Mathf.Max(0, w - .02f);
            Vector2 hand = c.pos + new Vector2(c.facing * .62f, .02f), ground = c.pos + Vector2.down;
            float handLife = w + .1f, hold = w / handLife;

            // A violet brand turns in the hand inside a violet bloom over a wisp of deep violet smoke (behind the caster), its slit
            // eye burning white.
            var cloud = Anchored(c.anchor, hand, "Mystic curse hand smoke", smoke[1], 1.6f * k, MysticHexSmoke, handLife, 29, false); cloud.spin = 70;
            var bloom = Anchored(c.anchor, hand, "Mystic curse hand bloom", glow, 2.1f * k, Alpha(MysticHex, .55f), handLife, 28); bloom.pulse = .06f;
            var brand = Anchored(c.anchor, hand, "Mystic curse hand brand", MysticBrand, 1.05f * k, Alpha(MysticHex, 1), handLife, 126); brand.spin = -160;
            var pupil = Anchored(c.anchor, hand, "Mystic curse hand heart", core, .24f * k, Alpha(MysticHexHot, 1), handLife, 127); pupil.pulse = .12f;
            foreach (var x in new[] { cloud, bloom, brand, pupil }) { x.fadeIn = .04f; x.startScale = .3f; x.endScale = 1; x.ease = 1.6f; x.hold = hold; x.fadePower = 2; }
            if (w < .2f)
            {
                // An instant echo: the curse leaves at once, so the echo only kindles the brand.
                Converge(hand, 5, 1.6f * k, MysticHex, w, .5f);
                return;
            }
            var shade = Underlay(hand, 3 * k, .3f, w + .2f); shade.hold = .7f;
            // Deep violet smoke and violet needles spiral into the hand.
            int fumes = Budget(5);
            for (int i = 0; i < fumes; i++)
            {
                var fume = Puff(hand, Range(.7f, 1) * k, MysticHexSmoke, Vector2.zero, w * Range(.8f, 1), 116);
                fume.orbitCenter = hand; fume.orbitAngle = i * 360f / fumes + Range(-15, 15); fume.orbit = -Range(300, 420);
                fume.orbitRadius = Range(1.3f, 1.9f) * k; fume.orbitRadiusEnd = .1f; fume.orbitPower = 1.3f;
                fume.startScale = 1; fume.endScale = .3f; fume.fadeIn = .06f; fume.hold = .6f; fume.fadePower = 1.3f;
            }
            Converge(hand, 10, 2.6f * k, MysticHex, w, .55f);
            // The brand burns on the floor under the caster in a pool of violet light.
            float total = w + .25f;
            var pool = MysticGroundBloom(ground, 3.6f * k, Alpha(MysticHex, .42f), total);
            var seal = Spawn("Mystic curse floor", MysticBrand, ground, 3 * k, Alpha(MysticHex, .95f), total, true, 27); LayFlat(seal, .32f); seal.spin = 45;
            foreach (var x in new[] { pool, seal }) { x.fadeIn = .06f; x.startScale = .5f; x.endScale = 1; x.ease = 3; x.hold = w / total; x.fadePower = 1.4f; }
            // Release: the brand flares in a spark and a shock front and is flung.
            GraceStar(hand, 2.2f * k, Alpha(MysticHex, 1), .22f, 132, release);
            GraceWave(hand, 2.4f * k, Alpha(MysticHex, .85f), .26f, 3, 131, release);
            MysticLater(ImpactLines(hand, 2.8f * k, Alpha(MysticHex, 1), .2f), release);
        }

        private void TrailCurse(Vector2 from, Vector2 to, Color tint)
        {
            MysticEnsure();
            Vector2 delta = to - from; float length = delta.magnitude; if (length < .05f) return;
            int g = Grade; float thick = 1 + .15f * (g - 1);
            // Quick enough that marks drawn at once by other layers (grade, status) follow the bite closely.
            float travel = Mathf.Clamp(length / 30, .08f, .2f);
            MysticCurseFrame = Time.frameCount; MysticCurseEnd = to; MysticCurseTravel = travel;
            float linger = .3f + .1f * g;

            // The tendril: one ragged, thorned ribbon of violet light stretched from the hand to the victim in a soft violet glow,
            // with a white-hot thread inside it; it shoots across in the travel time, holds, then erodes from its thin edges.
            int variant = random.Next(MysticTendril.Length); bool flip = Chance(.5f);
            float width = Mathf.Clamp(length * .34f, 1.25f, 2.1f) * thick, vineLife = travel + linger + .1f;
            var halo = MysticVine(from, to, GraceRibbon, width * 1.5f, Alpha(MysticHex, .42f), vineLife, true, 124);
            var vine = MysticVine(from, to, MysticTendril[variant], width, Alpha(Color.Lerp(MysticHex, MysticHexBody, .3f), .85f), vineLife, true, 125);
            var thread = MysticVine(from, to, MysticTendrilLight[variant], width, Alpha(MysticHexHot, 1), vineLife - .04f, true, 126);
            // A second, fainter strand winds the other way round the first, so the tendril reads as braided energy.
            var braid = MysticVine(from, to, MysticTendril[1 - variant], width * .8f, Alpha(MysticHex, .6f), vineLife - .02f, true, 124); braid.scale.y *= -1;
            foreach (var x in new[] { halo, vine, thread, braid })
            {
                if (flip) x.scale.y *= -1;
                x.axisStart = new Vector2(.03f, 1); x.axisEnd = Vector2.one; x.ease = GracePop(x.life, travel); x.hold = (travel + linger * .5f) / x.life; x.fadePower = 1.3f;
            }
            thread.flicker = .12f;
            // A violet plasma head with a white spark races along it to the bite.
            var head = GracePlasma(from, .85f * thick, Alpha(MysticHex, 1), travel + .1f, 127); head.velocity = delta / travel; head.hold = .7f; head.endScale = 1.2f;
            var spark0 = Spawn("Mystic curse head", core, from, .32f * thick, Alpha(MysticHexHot, 1), travel + .04f, true, 128); spark0.velocity = delta / travel; spark0.hold = .8f;
            // Drops of violet light fall from the tendril and deep violet smoke curls off it.
            Vector2 side = new Vector2(-delta.y, delta.x) / length;
            for (int i = 0, drips = Budget(3); i < drips; i++)
            {
                float t = Range(.15f, .9f); Vector2 at = from + delta * t + side * Range(-.15f, .15f);
                var drop = MysticLater(Spawn("Mystic curse drip", core, at, Range(.12f, .18f), Alpha(MysticHex, .95f), Range(.4f, .6f), true, 127), t * travel + .05f);
                drop.velocity = new Vector2(Range(-.3f, .3f), Range(-.5f, .3f)); drop.gravity = 8; drop.hold = .5f; drop.fadePower = 1.2f;
            }
            for (int i = 0, puffs = Budget(3); i < puffs; i++)
            {
                float t = Range(.1f, .9f);
                var fume = MysticLater(Puff(from + delta * t, Range(.5f, .8f) * thick, MysticHexSmoke, new Vector2(Range(-.3f, .3f), Range(.3f, .7f)), Range(.55f, .75f), 115), t * travel);
                fume.endScale = 1.8f;
            }
            // A flick of light at the source, and a shock front and needles where it bites.
            GraceStar(from, 1.2f * thick, Alpha(MysticHex, 1), .14f);
            GraceWave(to, 1.8f * thick, Alpha(MysticHex, .9f), .24f, 3, 131, travel);
            for (int i = 0; i < 4; i++) MysticLater(Spark(to, Direction(Range(0, 360)) * Range(2.5f, 5), i % 2 == 0 ? MysticHexHot : MysticHex, Range(.26f, .38f), Range(.2f, .3f), 4, 3), travel);
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
            float life = .95f + .15f * g, stamp = MysticPop(life, .08f);

            var shade = MysticLater(Underlay(pos, 2.8f * s, .32f, life), d); shade.hold = .4f;
            // The eye sigil falls in large and slams onto the victim: a violet bloom, a deep violet body (no wider than the light),
            // the glowing sigil, then a white sear.
            var brandGlow = MysticLater(Spawn("Mystic curse brand bloom", glow, pos, 2.6f * s, Alpha(MysticHex, .55f), life, true, 116), d);
            var brandBody = MysticLater(Spawn("Mystic curse brand body", MysticBrand, pos, 1.3f * s, Alpha(MysticHexBody, .6f), life, false, 118), d);
            var brand = MysticLater(Spawn("Mystic curse brand", MysticBrand, pos, 1.31f * s, Alpha(MysticHex, 1), life * .85f, true, 127), d);
            foreach (var x in new[] { brandGlow, brandBody, brand }) { x.startScale = 1.7f; x.endScale = 1; x.ease = stamp; x.spin = 35; x.hold = .45f; x.fadePower = 1.3f; }
            brandGlow.spin = 0; brandGlow.pulse = .05f;
            var sear = MysticLater(Spawn("Mystic curse sear", MysticBrand, pos, 1.33f * s, Alpha(MysticHexHot, 1), .16f, true, 129), hit); sear.startScale = 1.1f; sear.endScale = 1; sear.ease = 4; sear.fadePower = 1.4f; sear.angle = 2.5f;
            var heat = MysticLater(Spawn("Mystic curse sear core", core, pos, .55f * s, Alpha(MysticHexHot, 1), .1f, true, 133), hit); heat.startScale = .5f; heat.endScale = 1.5f; heat.ease = 4; heat.fadePower = 1.3f;
            if (lead)
            {
                // The stamp bursts: a violet blast billows off the sigil, a spark flares, wedges and a shock front run out.
                int mark = clips.Count;
                Blast(pos, 1.9f * s, Alpha(MysticHex, 1), .45f, 125);
                GraceDefer(mark, hit);
                GraceStar(pos, 2.4f * s, Alpha(MysticHexHot, 1), .22f, 132, hit);
                MysticLater(ImpactLines(pos, 2.8f * s, Alpha(MysticHex, 1), .24f), hit);
                GraceWave(pos, 2.4f * s, Alpha(MysticHex, .9f), .3f, 3, 131, hit);
            }
            // Deep violet smoke bursts off the stamp; drops of violet light splatter; smoke and violet needles rise from the burn.
            for (int i = 0, n = Budget(3); i < n; i++)
            {
                Vector2 way = Direction(Range(0, 360));
                var plume = MysticLater(Puff(pos + way * .2f * s, Range(.5f, .75f) * s, MysticHexSmoke, way * Range(1.2f, 2.2f) * s + Vector2.up * .4f, Range(.4f, .55f), 116), hit);
                plume.startScale = .5f; plume.endScale = 1.4f; plume.fadeIn = 0;
            }
            for (int i = 0, n = Budget(6); i < n; i++)
            {
                var drop = MysticLater(Spawn("Mystic curse splatter", core, pos + Inside(.2f * s), Range(.12f, .2f) * s, Alpha(i % 2 == 0 ? MysticHex : MysticHexHot, .95f), Range(.45f, .7f), true, 127), hit);
                drop.velocity = Direction(i * 360f / n + Range(-20, 20)) * Range(3, 6) * s + Vector2.up; drop.gravity = 9; drop.drag = 1; drop.hold = .5f; drop.fadePower = 1.2f;
            }
            for (int i = 0, n = Budget(3); i < n; i++) { var fume = MysticLater(Puff(pos + Inside(.4f * s), Range(.6f, .9f) * s, MysticHexSmoke, new Vector2(Range(-.6f, .6f), Range(.5f, 1.1f)), Range(.65f, .85f), 115), d + .1f); fume.endScale = 1.9f; }
            for (int i = 0, n = Budget(4); i < n; i++)
            {
                var needle = MysticLater(Spawn("Mystic curse needle", spark, pos + Inside(.5f * s), Range(.32f, .48f) * s, Alpha(i % 2 == 0 ? MysticHex : MysticHexHot, 1), Range(.6f, .9f), true, 128), d + Range(.1f, .4f));
                needle.velocity = new Vector2(Range(-.4f, .4f), Range(1.4f, 2.4f)); needle.drag = .4f; needle.alignVelocity = true; needle.hold = .4f; needle.fadePower = 1.2f; needle.endScale = .4f; needle.ease = 1;
            }
            if (g < 2) return;

            // Level two and up: a rune wheel turns behind the brand (bolder at three), violet smoke lingers on the burn, and veins
            // of violet light reach out from it (more of them at three, toward the next host the curse will spread to).
            float wheelAlpha = awake ? .9f : .7f;
            var wheel = MysticLater(RuneCircle(pos, 2.2f * s, Alpha(awake ? Color.Lerp(MysticHex, MysticHexHot, .35f) : MysticHex, wheelAlpha), life, -60, false, 125), d + .05f);
            wheel.hold = .45f;
            for (int i = 0, n = Budget(g); i < n; i++)
            {
                var linger = MysticLater(Puff(pos + Inside(.5f * s) + Vector2.up * .3f * s, Range(.55f, .8f) * s, MysticHexSmoke, new Vector2(Range(-.25f, .25f), Range(.35f, .6f)), Range(1, 1.4f), 115), d + Range(.25f, .45f));
                linger.drag = 1; linger.endScale = 1.6f; linger.fadeIn = .1f; linger.hold = .2f;
            }
            int veins = awake ? 4 : 3; float turn = Range(0, 360);
            for (int i = 0; i < veins; i++)
            {
                float a = turn + i * 360f / veins + Range(-12, 12); Vector2 way = Direction(a), across = Direction(a + 90), previous = pos + way * .5f * s;
                for (int j = 1; j <= 2; j++)
                {
                    Vector2 next = pos + way * (.5f + j * .44f) * s + across * Range(-.12f, .12f);
                    float delay = hit + .01f + j * .04f;
                    var vein = MysticLater(MysticBrush(previous, next, .22f * (1.2f - j * .2f) * s, Alpha(MysticHex, .85f), .55f, true, 126), delay); vein.hold = .4f;
                    var lit = MysticLater(MysticBrush(previous, next, .1f * s, Alpha(MysticHexHot, 1), .45f, true, 127), delay); lit.hold = .3f; lit.flicker = .15f;
                    // Awakened: a fine gold thread runs inside each vein.
                    if (awake) { var thread = MysticLater(Line(previous, next, .04f * s, Alpha(MysticGold, 1), .4f, 128, 1.1f), delay); thread.hold = .3f; }
                    previous = next;
                }
                MysticLater(GraceTwinkle(previous, .7f * s, Alpha(awake ? MysticGoldHot : MysticHexHot, 1), .22f), hit + .14f);
            }
            if (awake)
            {
                // Awakened (the curse spreads on death): a violet rim with a fine gold line just outside it.
                var rim = MysticLater(Spawn("Mystic curse rim", GraceShock, pos, 2 * s, Alpha(MysticHex, .8f), life * .8f, true, 128), hit);
                var gilt = MysticLater(Spawn("Mystic curse rim gold", GraceShock, pos, 2.08f * s, Alpha(MysticGold, .55f), life * .7f, true, 128), hit);
                foreach (var x in new[] { rim, gilt }) { x.pulse = .05f; x.startScale = .8f; x.endScale = 1; x.ease = 5; x.hold = .4f; x.fadePower = 1.3f; }
                rim.flicker = .12f;
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
            var palmGlow = Anchored(c.anchor, hand, "Mystic banner hand bloom", glow, 1.4f, Alpha(MysticGold, .55f), w + .08f, 28);
            var palm = Anchored(c.anchor, hand, "Mystic banner hand", core, .42f, Alpha(MysticGoldHot, 1), w + .08f, 125);
            foreach (var x in new[] { palmGlow, palm }) { x.fadeIn = .04f; x.startScale = .3f; x.endScale = 1; x.ease = 1.5f; x.hold = .8f; x.pulse = .08f; }
            if (w < .2f)
            {
                // An instant echo: the banner drops in at once, so the echo only flashes the hand.
                Converge(hand, 5, 1.6f, MysticGold, w, .4f);
                GraceStar(hand, 1.4f, Alpha(MysticGoldHot, 1), .18f, 132, release);
                return;
            }
            float total = w + .25f, hold = w / total;
            // A small painted ring kindles on the floor where the caster stands (it is not the banner's reach) in a pool of gold
            // light, and motes rise from it.
            var pool = MysticGroundBloom(ground, 3, Alpha(MysticGold, .42f), total);
            var kindle = Spawn("Mystic war kindle", GraceBrushRing, ground, 2.4f, Alpha(MysticGold, .95f), total, true, 26); LayFlat(kindle, .3f); kindle.angle = Range(0, 360); kindle.spin = 90;
            foreach (var x in new[] { pool, kindle }) { x.fadeIn = .06f; x.startScale = .3f; x.endScale = 1; x.ease = 2.5f; x.hold = hold; x.fadePower = 1.4f; }
            Emitter(kindle, 10, .9f, (owner, p) => MysticBannerMote(MysticOnGround(owner, p), g >= 3));
            // The standard is drawn up out of the floor in light behind the caster inside a soft beam, its crest kindling at the top.
            var beam = GraceColumn(GraceBeam, foot, 2, H + 1.2f, Alpha(MysticGold, .45f), w + .1f, true, 26);
            var column = Pillar(foot, .85f, H + 1, Alpha(MysticGold, .75f), w + .1f, 27);
            foreach (var x in new[] { beam, column }) { x.fadeIn = w * .3f; x.hold = w / (w + .1f); x.fadePower = 2; x.axisStart = new Vector2(.3f, .1f); x.axisEnd = Vector2.one; x.ease = 1.6f; }
            var ghost = Spawn("Mystic banner rising", MysticPole, foot, .26f, Alpha(MysticGoldHot, .9f), w + .05f, true, 28); MysticStretch(ghost, .26f, H);
            ghost.axisStart = new Vector2(1, .08f); ghost.axisEnd = Vector2.one; ghost.ease = 1.8f; ghost.fadeIn = .06f; ghost.hold = .8f; ghost.fadePower = 2;
            var crestGhost = MysticLater(Spawn("Mystic banner crest kindling", MysticCrest, crestAt, .95f, Alpha(MysticGoldHot, .95f), w * .55f + .05f, true, 29), w * .45f);
            crestGhost.startScale = .3f; crestGhost.endScale = 1; crestGhost.ease = 3; crestGhost.fadeIn = .04f; crestGhost.hold = .8f; crestGhost.fadePower = 2;
            Converge(hand, 10, 2.2f, MysticGold, w, .35f);
            // Release: sparks at the crest and the hand as the standard is planted.
            GraceStar(crestAt, 2.4f, Alpha(MysticGoldHot, 1), .28f, 132, release);
            GraceStar(hand, 1.6f, Alpha(MysticGold, 1), .2f, 132, release);
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

            // The standard (behind the actors): a painted wooden pole with gold bands and a thin rim of gold light (so it holds
            // against dark towers), a painted golden sun crest in a soft glow over a slowly turning star of light, and the cloth
            // flying behind the caster.
            var poleGlow = Attach(root, "Mystic banner pole rim", MysticPole, .4f, Alpha(MysticGold, .5f), 26, true, foot); MysticStretch(poleGlow, .4f, H);
            var pole = Attach(root, "Mystic banner pole", MysticPole, .24f, Color.white, 27, false, foot); MysticStretch(pole, .24f, H);
            layers.Add(poleGlow);
            float crestW = .95f + .12f * (g - 1), crestLift = crestW * .4f;
            Vector2 crestAt = foot + Vector2.up * (H + crestLift);
            var crestGlow = Attach(root, "Mystic banner crest glow", glow, crestW * 3, Alpha(MysticGold, .6f), 25, true, crestAt); crestGlow.pulse = .06f;
            var star = Attach(root, "Mystic banner sun star", glint, crestW * 2.6f, Alpha(MysticGoldHot, .8f), 26, true, crestAt); star.spin = 24;
            var crest = Attach(root, "Mystic banner crest", MysticCrest, crestW, Color.white, 29, false, crestAt);
            layers.Add(crestGlow); layers.Add(star); layers.Add(crest);
            Clip halo = null;
            if (g >= 2) { halo = Attach(root, "Mystic banner sun halo", runeRing, crestW * (awake ? 2.5f : 2), Alpha(awake ? MysticGoldHot : MysticGold, awake ? .9f : .7f), 26, true, crestAt); halo.spin = 30; layers.Add(halo); }
            // The cloth and, at every level, its gold edge light (faint at one, full when awakened), so it holds on a red sky.
            var cloth = new Clip[MysticStripCount]; var shine = new Clip[MysticStripCount];
            float seg = flagW / MysticStripCount;
            for (int i = 0; i < MysticStripCount; i++)
            {
                cloth[i] = Attach(root, "Mystic banner cloth", MysticFlagStrips[i], seg, Color.white, 28, false, foot); MysticStretch(cloth[i], seg * 1.12f * side, flagH); layers.Add(cloth[i]);
                shine[i] = Attach(root, "Mystic banner cloth shine", MysticFlagGlowStrips[i], seg, Alpha(MysticGoldHot, .6f), 29, true, foot); MysticStretch(shine[i], seg * 1.12f * side, flagH); layers.Add(shine[i]);
            }

            // The reach of the blessing on the floor: a pool of gold light, a bold painted brush ring turning inside it and a gold
            // edge with a rune ring exactly on the radius. From level two the grade overlay lays its own lit edge there (and at
            // three its rune seal).
            var plane = MysticPlane(root, new Vector2(0, -.6f), .3f);
            float edgeW = 2 * R / .74f, span = 2 * R / .955f, bandW = 2 * R * .8f / .95f;
            var pool = Attach(plane, "Mystic war floor bloom", glow, 2 * R * 1.15f, Alpha(MysticGold, .3f), 25);
            var band = Attach(plane, "Mystic war ring", GraceBrushRing, bandW, Alpha(MysticGold, .95f), 26); band.spin = 14;
            var floorLayers = new List<Clip> { pool, band };
            if (g == 1) { var edge = Attach(plane, "Mystic war edge", GraceShock, edgeW, Alpha(MysticGold, .9f), 27); floorLayers.Add(edge); }
            if (!awake) { var aura = Attach(plane, "Mystic war seal", runeRing, span, Alpha(MysticGold, .8f), 27); aura.spin = 8; floorLayers.Add(aura); }
            else { var laurel = Attach(plane, "Mystic war laurel", runeRing, R * 1.3f, Alpha(MysticGoldHot, .85f), 27); laurel.spin = -22; floorLayers.Add(laurel); }
            layers.AddRange(floorLayers);

            float pop = MysticPop(life, .2f);
            pole.life = life; pole.fadeIn = .04f; pole.hold = hold; pole.fadePower = 1.4f;
            foreach (var l in layers) { l.life = life; l.fadeIn = .06f; l.hold = hold; l.fadePower = 1.4f; }
            foreach (var l in floorLayers) { l.startScale = .2f; l.endScale = 1; l.ease = pop; }
            var rig = new MysticBannerRig { root = root, pole = pole, poleGlow = poleGlow, crest = crest, crestGlow = crestGlow, star = star, halo = halo, cloth = cloth, shine = shine,
                height = H, poleX = poleX, flagW = flagW, flagH = flagH, crestLift = crestLift, side = side, phase = Range(0, 6.28f), shineGain = g == 1 ? .42f : g == 2 ? .67f : 1 };
            MysticBanners.Add(rig);
            // The banner is posed once per frame: the high rate keeps it waving on fast displays and when emitters are thinned
            // under load, and repeated calls in one frame are skipped.
            Emitter(pole, 240, 0, (owner, p) => { if (owner.age == rig.clock) return; MysticWaveBanner(rig, owner.age); });
            Emitter(band, 7 + 4 * g, R, (owner, p) => MysticBannerMote(MysticOnGround(owner, p), awake));
            MysticWaveBanner(rig, 0);
            Bundle(root, pole, layers.ToArray());

            // The standard lands: a burst of gold light and dust billows at its foot, a spark flares, a shock front runs out to
            // the edge, wedges and needles fly and a spark lights the crest.
            Vector2 world = root.position, footWorld = world + foot, groundWorld = world + Vector2.down * .6f;
            const float land = .13f;
            int mark = clips.Count;
            Blast(footWorld + Vector2.up * .45f, 2.4f, Alpha(MysticGold, 1), .5f, 28);
            GraceDefer(mark, land);
            GraceStar(footWorld + Vector2.up * .3f, 2.8f, Alpha(MysticGoldHot, 1), .24f, 132, land);
            GraceGroundWave(groundWorld, 2 * R / .74f, Alpha(MysticGold, .95f), .45f, land);
            MysticLater(ImpactLines(footWorld + Vector2.up * .25f, 3.4f, Alpha(MysticGoldHot, 1), .24f), land);
            for (int i = 0, n = Budget(5); i < n; i++)
            {
                float s = i % 2 == 0 ? 1 : -1;
                var dust = MysticLater(Puff(footWorld + new Vector2(s * Range(.1f, .4f), .1f), Range(.5f, .8f), DustTone, new Vector2(s * Range(1.6f, 3.2f), Range(.3f, .8f)), Range(.5f, .7f), 118), land); dust.gravity = -.3f;
            }
            for (int i = 0, n = Budget(8); i < n; i++) MysticLater(Spark(footWorld + Vector2.up * .2f, new Vector2(Range(-4, 4), Range(2, 6)), i % 3 == 0 ? Color.white : MysticGold, Range(.3f, .46f), Range(.3f, .5f), 8, 2), land);
            GraceStar(world + crestAt, 2.2f + .3f * g, Alpha(MysticGoldHot, 1), .3f, 133, land + .05f);
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
            rig.crest.offset = crestAt; rig.crestGlow.offset = crestAt; rig.star.offset = crestAt; if (rig.halo != null) rig.halo.offset = crestAt;
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
                float deg = Mathf.Atan(slope) * Mathf.Rad2Deg * .5f * rig.side, fold = Mathf.Cos(ph + 1.2f);
                // Painted folds: the light rolls smoothly over each fold of the cloth.
                float light = Mathf.Lerp(.66f, 1, Smooth(-.7f, .6f, fold));
                Vector2 at = attach + new Vector2(rig.side * x, y);
                var strip = rig.cloth[i]; strip.offset = at; strip.angle = deg; strip.color = new Color(light, light, light, 1);
                var s = rig.shine[i]; s.offset = at; s.angle = deg; s.color = Alpha(MysticGoldHot, Mathf.Clamp01((.35f + .45f * Smooth(.05f, .75f, fold) + .3f * kick) * rig.shineGain));
            }
        }

        private void MysticBannerMote(Vector2 at, bool awake)
        {
            if (Chance(.22f))
            {
                // A small shred of crimson or gold cloth drifting up.
                var shred = Spawn("Mystic banner shred", petal, at, Range(.16f, .26f), Chance(.5f) ? new Color(.86f, .14f, .16f, .94f) : new Color(1, .76f, .3f, .94f), Range(.9f, 1.4f), false, 121);
                shred.velocity = new Vector2(Range(-.3f, .3f), Range(.8f, 1.6f)); shred.drag = .4f; shred.spin = Range(-400, 400); shred.angle = Range(0, 360); shred.fadeIn = .06f; shred.hold = .4f; shred.fadePower = 1.3f;
                return;
            }
            var mote = Spark(at, new Vector2(Range(-.15f, .15f), Range(1.3f, 2.6f)), Chance(awake ? .5f : .3f) ? MysticGoldHot : MysticGold, Range(.22f, .36f), Range(.6f, 1), -.6f, .5f);
            mote.fadeIn = .06f;
        }

        private void ImpactWarBanner(Vector2 pos, float radius, Color tint, int facing)
        {
            MysticEnsure();
            int g = Grade; bool awake = g >= 3;
            float R = Mathf.Clamp(radius, .8f, 6);
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
                GraceStar(crestAt, 2 + .3f * g, Alpha(MysticGoldHot, 1), .26f, 133);
            }
            // A shock front of gold sweeps across the floor out to the edge of the blessing (its last drawn frame lands on the radius).
            var sweep = ShockFlip(ground, 2 * R / .88f, Alpha(MysticGold, 1), .55f, 27); LayFlat(sweep, .3f);
            sweep.startScale = .08f; sweep.endScale = 1; sweep.ease = 2.4f; sweep.fadePower = 1;
            if (g >= 2)
            {
                var echo = MysticLater(ShockFlip(ground, 2 * R / .88f * .98f, Alpha(MysticGoldHot, .75f), .48f, 27), .08f); LayFlat(echo, .3f);
                echo.startScale = .08f; echo.endScale = 1; echo.ease = 2.4f; echo.fadePower = 1;
            }
            // The edge of the reach flares in pillars of light as the front arrives.
            for (int edge = -1; edge <= 1; edge += 2)
            {
                Vector2 at = ground + new Vector2(edge * R, 0);
                var aura = MysticLater(GraceColumn(GraceBeam, at, 1.7f, 1.9f + .3f * g, Alpha(MysticGold, .45f), .4f, true, 25), .26f); aura.hold = .2f; aura.ease = 6;
                var post = MysticLater(Pillar(at, .75f, 1.4f + .3f * g, Alpha(awake ? MysticGoldHot : MysticGold, .95f), .38f, 27), .26f); post.hold = .2f; post.ease = 6;
                for (int i = 0; i < 2; i++) MysticLater(Spark(at + Vector2.up * .1f, new Vector2(Range(-.6f, .6f), Range(3, 5)), i == 0 ? Color.white : MysticGold, Range(.3f, .42f), Range(.35f, .5f), 2, 1.5f), .26f + i * .04f);
            }
            // Sparks of courage rise inside the reach.
            for (int i = 0, n = Budget(3); i < n; i++)
                Spark(ground + new Vector2(Range(-R, R) * .85f, Range(.1f, .4f)), new Vector2(0, Range(2.5f, 4)), awake ? MysticGoldHot : MysticGold, Range(.34f, .5f), Range(.4f, .6f), -1, .8f);
        }

        // ---------------------------------------------------------------- Bloom

        private void CastBloom(CastInfo c)
        {
            MysticEnsure();
            int g = Grade; float k = Mathf.Clamp(c.size / 1.4f, .8f, 1.5f) * (1 + .1f * (g - 1)), w = c.windup, release = Mathf.Max(0, w - .02f);
            Vector2 hand = c.pos + new Vector2(c.facing * .55f, -.12f), ground = c.pos + Vector2.down;
            float handLife = w + .1f, hold = w / handLife;
            // A seed of light glows in the cupped hands: a white-pink heart in a pink bloom inside a turning green sparkle.
            var bloom = Anchored(c.anchor, hand, "Mystic seed cup bloom", glow, 1.7f * k, Alpha(MysticPink, .55f), handLife, 28); bloom.pulse = .06f;
            var spark0 = Anchored(c.anchor, hand, "Mystic seed cup sparkle", fleck, .8f * k, Alpha(MysticSap, 1), handLife, 126); spark0.spin = 140;
            var seed = Anchored(c.anchor, hand, "Mystic seed cup heart", core, .3f * k, MysticPinkHot, handLife, 127); seed.pulse = .12f;
            foreach (var x in new[] { bloom, spark0, seed }) { x.fadeIn = .04f; x.startScale = .3f; x.endScale = 1; x.ease = 1.5f; x.hold = hold; x.fadePower = 2; }
            if (w < .2f)
            {
                // An instant echo: the seed is thrown at once, so the echo only lights the hands.
                Converge(hand, 5, 1.4f * k, MysticPink, w, .5f);
                return;
            }
            // A small lotus bud cups the seed: painted leaves below, painted petals fanning up around it with a sheen of light.
            Vector2 stem = hand + Vector2.down * .14f * k;
            int folds = 4 + (g - 1);
            for (int i = -1; i <= folds; i++)
            {
                bool leaf = i < 0 || i == folds; float a = leaf ? (i < 0 ? -18 : 198) : Mathf.Lerp(42, 138, i / (float)Mathf.Max(1, folds - 1));
                float size = (leaf ? .64f : .54f) * k;
                Vector2 at = stem + Direction(a) * size * .42f;
                var fold = Anchored(c.anchor, at, leaf ? "Mystic cupping leaf" : "Mystic cupping petal", leaf ? MysticLeafPaint : MysticPetalPink, size, MysticPainted, w + .05f, leaf ? 119 : 120, false);
                fold.angle = a; fold.fadeIn = .06f; fold.startScale = .3f; fold.endScale = 1; fold.ease = 2; fold.hold = .85f; fold.fadePower = 2;
                if (leaf) continue;
                var lit = Anchored(c.anchor, at, "Mystic cupping petal light", MysticPetal, size * .85f, Alpha(MysticPink, .4f), w + .05f, 126);
                lit.angle = a; lit.fadeIn = .06f; lit.startScale = .3f; lit.endScale = 1; lit.ease = 2; lit.hold = .85f; lit.fadePower = 2;
            }
            Converge(hand, 6, 2.2f * k, MysticPink, w, .5f);
            Converge(hand, 5, 2 * k, MysticSap, w, .4f);
            // A flower seal opens on the floor under the caster in a pool of pink light.
            float total = w + .25f;
            var pool = MysticGroundBloom(ground, 3.4f * k, Alpha(MysticPink, .42f), total);
            var seal = Spawn("Mystic seed seal", MysticBloomSeal, ground, 2.8f * k, Alpha(MysticPink, .95f), total, true, 27); LayFlat(seal, .32f); seal.spin = 30;
            foreach (var x in new[] { pool, seal }) { x.fadeIn = .06f; x.startScale = .4f; x.endScale = 1; x.ease = 3; x.hold = w / total; x.fadePower = 1.4f; }
            // Release: sparks and a puff of petals as the seed is flicked.
            GraceStar(hand, 2 * k, Alpha(MysticPinkHot, 1), .2f, 132, release);
            Vector2 toward = (c.target - hand).sqrMagnitude > .01f ? (c.target - hand).normalized : Vector2.right * c.facing;
            for (int i = 0, n = Budget(5); i < n; i++)
            {
                bool lit = i % 2 == 1;
                var puff = MysticLater(Spawn("Mystic seed release petal", lit ? MysticPetal : MysticPetalPink, hand, Range(.3f, .42f) * k, lit ? Alpha(MysticPinkHot, 1) : MysticPainted, Range(.5f, .7f), lit, lit ? 129 : 121), release);
                puff.velocity = (toward + Inside(.8f)) * Range(2, 3.5f); puff.drag = 2.5f; puff.spin = Range(-500, 500); puff.angle = Range(0, 360); puff.hold = .3f; puff.fadePower = 1.3f;
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
            // The seed: a hot heart in a pink bloom, wrapped by a painted petal and a painted leaf spinning round it.
            var halo = Spawn("Mystic seed bloom", glow, from, 1.1f * sz, Alpha(MysticPink, .55f), travel, true, 120);
            var heart = Spawn("Mystic seed heart", core, from, .4f * sz, MysticPinkHot, travel, true, 129);
            var leafA = Spawn("Mystic seed petal", MysticPetalPink, from, .54f * sz, MysticPainted, travel, false, 121); leafA.spin = 900;
            var leafB = Spawn("Mystic seed leaf", MysticLeafPaint, from, .5f * sz, MysticPainted, travel, false, 122); leafB.spin = 900; leafB.angle = 180;
            foreach (var x in new[] { halo, heart, leafA, leafB }) { x.velocity = throwV; x.gravity = fall; x.fadePower = .3f; }
            // A trail of sparkles and tiny painted petals behind it.
            Emitter(heart, 40 + 10 * (g - 1), .05f, (owner, p) =>
            {
                if (Chance(.35f))
                {
                    var bit = Spawn("Mystic seed trail petal", Chance(.6f) ? MysticPetalPink : MysticLeafPaint, p, Range(.18f, .26f) * sz, MysticPainted, Range(.35f, .55f), false, 121);
                    bit.velocity = new Vector2(Range(-.4f, .4f), Range(-.6f, .2f)); bit.spin = Range(-500, 500); bit.angle = Range(0, 360); bit.fadePower = 1.2f;
                    return;
                }
                var mote = Spawn("Mystic seed trail light", fleck, p, Range(.16f, .28f) * sz, Alpha(Chance(.6f) ? MysticPink : MysticSap, 1), Range(.25f, .4f), true, 126);
                mote.velocity = new Vector2(Range(-.3f, .3f), Range(-.4f, .3f)); mote.endScale = .2f; mote.ease = 1; mote.angle = Range(-20, 20);
            });
            GraceStar(from, 1 * sz, Alpha(MysticPinkHot, 1), .14f);
            // The seed lands: a pink spark, a green shock front and a few painted petals.
            GraceStar(to, 1.6f * sz, Alpha(MysticPink, 1), .2f, 132, travel);
            GraceWave(to, 1.9f * sz, Alpha(MysticSap, .9f), .28f, 3, 131, travel);
            MysticLater(GraceTwinkle(to, 1.2f * sz, Alpha(MysticPinkHot, 1), .24f), travel);
            for (int i = 0, n = Budget(3); i < n; i++)
            {
                var bit = MysticLater(Spawn("Mystic seed landing petal", i % 2 == 0 ? MysticPetalPink : MysticLeafPaint, to, Range(.26f, .36f) * sz, MysticPainted, Range(.45f, .65f), false, 121), travel);
                bit.velocity = Direction(i * 360f / n + Range(-20, 20)) * Range(1.8f, 3) + Vector2.up; bit.drag = 2.5f; bit.spin = Range(-500, 500); bit.angle = Range(0, 360); bit.hold = .3f; bit.fadePower = 1.3f;
            }
        }

        private void ImpactBloom(Vector2 pos, float radius, Color tint, int facing)
        {
            MysticEnsure();
            int g = Grade; bool lush = g >= 3;
            float R = Mathf.Clamp(radius, .8f, 3.5f), s = Mathf.Clamp(.55f + R * .28f, .85f, 1.4f) * (1 + .06f * (g - 1));
            Vector2 heart = pos + Vector2.up * .15f, ground = pos + Vector2.down * .8f;
            float life = 1.02f + .04f * (g - 1);
            // Seeds blooming together near each other (one cast on several foes) share the first flower's light: a follower
            // draws only its petals, seal and motes.
            bool lead = !(MysticBloomFrame == Time.frameCount && (MysticBloomAt - pos).sqrMagnitude < 9);
            if (lead) { MysticBloomFrame = Time.frameCount; MysticBloomAt = pos; }
            // Petal tips (about 1.1 lengths from the bud once open) stay inside the strike radius; leaves reach furthest.
            float leafCap = R * .82f, petalCap = R * .74f, backCap = R * .7f;

            // The bloom bursts: a white-pink heart, a pink blast billowing out behind the opening flower (the hand-drawn explosion,
            // under the front petals), a gold spark, pink wedges and needles over a faint shade behind the actors; a mandala opens
            // behind the flower in a soft bloom.
            if (lead)
            {
                var shade = Underlay(heart, 3.6f * s, .22f, .7f); shade.hold = .3f;
                var pop = Spawn("Mystic bloom heart", core, heart, .85f * s, Alpha(MysticPinkHot, 1), .12f, true, 133); pop.startScale = .4f; pop.endScale = 1.6f; pop.ease = 4; pop.fadePower = 1.3f;
                Blast(heart, Mathf.Min(3.4f * s, R * 2.4f), Alpha(MysticPink, 1), .6f, 119);
                GraceStar(heart, Mathf.Min(3 * s, R * 2.2f), Alpha(MysticGoldHot, 1), .26f, 132);
                ImpactLines(heart, Mathf.Min(5 * s, R * 2.5f), Alpha(MysticPink, 1), .28f);
                for (int i = 0, n = Budget(8); i < n; i++)
                {
                    Vector2 way = Direction(i * 360f / n + Range(-18, 18));
                    Spark(heart + way * .2f, way * Range(4.5f, 7.5f) * s, i % 3 == 0 ? Color.white : MysticPink, Range(.32f, .5f) * s, Range(.22f, .34f), 3, 3);
                }
                Vector2 mandalaAt = heart + Vector2.up * .3f * s; float mandalaW = Mathf.Min(3.4f * s, R * 2);
                var mandalaGlow = Spawn("Mystic bloom mandala bloom", glow, mandalaAt, mandalaW * 1.3f, Alpha(MysticPink, .42f), life, true, 25);
                var mandala = Spawn("Mystic bloom mandala", MysticBloomSeal, mandalaAt, mandalaW, Alpha(MysticPink, .85f), life, true, 27); mandala.spin = 25;
                foreach (var x in new[] { mandalaGlow, mandala }) { x.startScale = .4f; x.endScale = 1.05f; x.ease = 6; x.hold = .4f; x.fadePower = 1.3f; }
            }

            // The lotus: (lush) broad painted leaves spread at its foot, a back and a front row of painted petals fan open out of
            // the bud with a sheen of light over them and a crown of pale petals stands in the middle; they hold, then loosen.
            // Leaves and the back row stand behind the actors, so the host and its neighbours stay visible through the flower.
            Vector2 cup = heart + Vector2.down * .3f * s;
            if (lush)
            {
                for (int i = 0; i < 4; i++)
                {
                    float a = i < 2 ? -14 - i * 22 : 194 + (i - 2) * 22, len0 = Mathf.Min((i % 2 == 0 ? 1.9f : 1.5f) * s, leafCap * (i % 2 == 0 ? 1 : .82f));
                    MysticLotus(Spawn("Mystic bloom leaf", MysticLeafPaint, cup, len0, MysticPainted, life, false, 27), cup, a, len0 * .45f, a > 90 ? 6 : -6, .5f);
                    MysticLotus(Spawn("Mystic bloom leaf light", MysticPetal, cup, len0 * .85f, Alpha(MysticSap, .35f), life * .9f, true, 28), cup, a, len0 * .45f, a > 90 ? 6 : -6, .5f);
                }
            }
            int back = lush ? 6 : 5; float lenBack = Mathf.Min(1.2f * s, backCap);
            for (int i = 0; i < back; i++)
            {
                float a = Mathf.Lerp(28, 152, i / (float)(back - 1)) + Range(-3, 3), drift = a > 90 ? 8 : -8;
                MysticLotus(Spawn("Mystic bloom back petal", MysticPetalPink, cup, lenBack, new Color(.92f, .82f, .9f, .94f), life, false, 28), cup, a, lenBack * .45f, drift, .5f);
            }
            // The front row covers the host only while it opens: it starts to erode sooner than the rest.
            int front = lush ? 7 : 6; float len = Mathf.Min(1.4f * s, petalCap);
            for (int i = 0; i < front; i++)
            {
                float a = Mathf.Lerp(-6, 186, i / (float)(front - 1)) + Range(-3, 3), drift = a > 90 ? 12 : -12;
                MysticLotus(Spawn("Mystic bloom petal", MysticPetalPink, cup, len, Alpha(Color.Lerp(Color.white, new Color(1, .86f, .93f), Range(0, 1)), .95f), life, false, 120), cup, a, len * .45f, drift, .32f);
                MysticLotus(Spawn("Mystic bloom petal light", MysticPetal, cup, len * .85f, Alpha(MysticPink, .38f), life * .8f, true, 126), cup, a, len * .45f, drift, .32f);
                if (lush && i % 3 == 0) MysticLotus(Spawn("Mystic bloom petal gold", MysticPetal, cup, len * 1.06f, Alpha(MysticGoldHot, .5f), life * .6f, true, 125), cup, a, len * .47f, drift, .32f);
            }
            int crown = lush ? 4 : 3; float len2 = Mathf.Min(.95f * s, petalCap * .7f);
            for (int i = 0; i < crown; i++)
            {
                float a = Mathf.Lerp(70, 110, i / (float)(crown - 1));
                MysticLotus(Spawn("Mystic bloom crown petal", MysticPetalPink, cup, len2, MysticPainted, life * .95f, false, 127), cup, a, len2 * .45f, (a - 90) * .15f, .4f);
                MysticLotus(Spawn("Mystic bloom crown light", MysticPetal, cup, len2 * .82f, Alpha(MysticPinkHot, .55f), life * .75f, true, 128), cup, a, len2 * .45f, (a - 90) * .15f, .4f);
            }
            // The stamen: a gold heart in a soft glow with a little pollen glitter.
            var stamenGlow = Spawn("Mystic bloom stamen bloom", glow, heart, 1.2f * s, Alpha(MysticGold, .5f), life * .6f, true, 129);
            var stamen = Spawn("Mystic bloom stamen", core, heart, .42f * s, Alpha(MysticGoldHot, 1), life * .6f, true, 130);
            foreach (var x in new[] { stamenGlow, stamen }) { x.pulse = .1f; x.hold = .35f; x.fadePower = 1.3f; x.startScale = .3f; x.endScale = 1; x.ease = 6; }
            if (lead)
            {
                GraceTwinkle(heart, 2 * s, Alpha(MysticGoldHot, 1), .3f).spin = 30;
                Sparkles(heart, 5, .5f * s, MysticGoldHot);
                // Healing light: a pink-green column behind the actors inside a soft pink beam.
                float columnH = (3.2f + .5f * (g - 1)) * Mathf.Min(s, 1.2f);
                var beam = GraceColumn(GraceBeam, ground, 3.4f * s, columnH * 1.1f, Alpha(MysticPink, .45f), .9f, true, 24); beam.hold = .2f; beam.ease = 6;
                var column = Pillar(ground, 1.4f * s, columnH, Alpha(MysticSap, .7f), .85f, 26); column.hold = .2f; column.ease = 6;
            }

            // A flower seal opens across the strike radius in a pool of pink light; a shock front runs out to its edge.
            float span = 2 * R / .955f, sealLife = 1.3f + .2f * (g - 1);
            var pool = MysticGroundBloom(ground, span * 1.25f, Alpha(MysticPink, .4f), sealLife);
            var seal = Spawn("Mystic bloom seal", MysticBloomSeal, ground, span, Alpha(MysticPink, .95f), sealLife, true, 27); LayFlat(seal, .32f); seal.spin = 18;
            foreach (var x in new[] { pool, seal }) { x.fadeIn = .03f; x.startScale = .5f; x.endScale = 1; x.ease = 6; x.hold = .45f; x.fadePower = 1.2f; }
            GraceGroundWave(ground, 2 * R / .74f, Alpha(MysticSap, .9f), .5f);
            if (lead) GraceWave(heart, 3.2f * s, Alpha(MysticPinkHot, .85f), .4f, 3);

            // A storm of painted petals and leaves whirls out once the flower is open; crosses of healing light rise.
            for (int i = 0, n = Budget(lead ? 9 : 5); i < n; i++)
            {
                bool lit = i % 4 == 0, leafy = i % 5 == 2;
                Sprite sprite = leafy ? MysticLeafPaint : lit ? MysticPetal : MysticPetalPink;
                var flake = MysticLater(Spawn("Mystic bloom storm petal", sprite, heart + Inside(.3f * s), Range(.34f, .52f) * s, lit ? Alpha(MysticPinkHot, 1) : MysticPainted, Range(1, 1.5f), lit, lit ? 130 : 121), Range(.15f, .32f));
                flake.velocity = Direction(i * 360f / n + Range(-15, 15)) * Range(2.5f, 4.5f) * s + Vector2.up * 1.2f;
                flake.drag = 1.6f; flake.gravity = -.5f; flake.spin = Range(-500, 500); flake.angle = Range(0, 360); flake.hold = .4f; flake.fadePower = 1.2f;
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
                // planted it (or, when that is unknown, rises in a spiral), with green shock fronts and a gold spark.
                if (lead)
                {
                    Rays(heart, 6, Mathf.Min(1.6f * s, R * .7f), Mathf.Min(2.8f * s, R * 1.1f), MysticSap, .3f);
                    GraceWave(heart, Mathf.Min(3.6f * s, R * 2), Alpha(MysticSap, .85f), .45f, 3, 131, .25f);
                    GraceGroundWave(ground, 2 * R / .74f, Alpha(MysticSap, .85f), .5f, .25f);
                    GraceStar(heart, 2.2f * s, Alpha(MysticGoldHot, 1), .26f, 132, .25f);
                }
                bool aimed = thrown >= 0 && (caster - heart).sqrMagnitude > 1;
                for (int i = 0, n = Budget(aimed ? 6 : 4); i < n; i++)
                {
                    float wait = .3f + i * .05f + Range(0, .08f), flight = Range(.65f, .85f);
                    Vector2 from = heart + Inside(.5f * s);
                    bool needle = i % 2 == 0;
                    var stream = MysticLater(Spawn("Mystic second life", needle ? spark : GraceCross, from, needle ? Range(.42f, .58f) : Range(.26f, .36f), Alpha(i % 3 == 0 ? MysticGoldHot : MysticSap, 1), flight, true, 131), wait);
                    if (aimed)
                    {
                        // An arc that lands on the caster's hand after the flight (gravity bends it), eroding on the way in.
                        const float bend = 4;
                        Vector2 to = caster + Inside(.3f);
                        stream.velocity = (to - from) / flight + Vector2.up * bend * flight * .5f; stream.gravity = bend;
                    }
                    else { stream.velocity = new Vector2(Range(-.6f, .6f), Range(2, 3.2f)); stream.drag = .3f; }
                    stream.alignVelocity = needle; stream.fadeIn = .04f; stream.hold = .55f; stream.fadePower = 1.3f; stream.endScale = needle ? .45f : 1; stream.ease = needle ? 1 : 2;
                }
            }
        }

        /// <summary>A petal of an opening lotus: it grows out of the bud at <paramref name="cupAt"/> along
        /// <paramref name="angle"/> (base at the cup, tip outward), then drifts open and loosens as it erodes after <paramref name="hold"/>.</summary>
        private static void MysticLotus(Clip clip, Vector2 cupAt, float angle, float reach, float drift, float hold)
        {
            clip.orbitCenter = cupAt; clip.orbitAngle = angle; clip.orbit = drift; clip.spin = drift;
            clip.orbitRadius = reach * .3f; clip.orbitRadiusEnd = reach * 1.35f; clip.orbitPower = .3f;
            clip.angle = angle; clip.startScale = .25f; clip.endScale = 1; clip.ease = 8; clip.fadeIn = .02f; clip.hold = hold; clip.fadePower = 2.2f;
        }

        private Clip MysticHealMote(Vector2 at, bool leaf, float scale)
        {
            if (leaf)
            {
                var l = Spawn("Mystic rising leaf", Chance(.5f) ? MysticLeafPaint : MysticPetalPink, at, Range(.26f, .38f) * scale, MysticPainted, Range(.7f, 1.1f), false, 121);
                l.velocity = new Vector2(Range(-.4f, .4f), Range(1.2f, 2.4f)); l.drag = .5f; l.spin = Range(-300, 300); l.angle = Range(0, 360); l.fadeIn = .05f; l.hold = .4f; l.fadePower = 1.3f;
                return l;
            }
            bool cross = Chance(.45f);
            var mote = Spawn("Mystic healing light", cross ? GraceCross : spark, at, (cross ? Range(.24f, .34f) : Range(.3f, .46f)) * scale, Alpha(Chance(.6f) ? MysticSap : MysticPinkHot, 1), Range(.6f, 1), true, 131);
            mote.velocity = new Vector2(Range(-.3f, .3f), Range(1.5f, 3)); mote.drag = .35f; mote.hold = .45f; mote.fadePower = 1.2f;
            if (cross) { mote.startScale = .35f; mote.endScale = 1; mote.ease = 6; } else { mote.alignVelocity = true; mote.endScale = .35f; mote.ease = 1; mote.fadeIn = .04f; }
            return mote;
        }

        // ---------------------------------------------------------------- shared helpers

        /// <summary>Holds a clip back for <paramref name="delay"/> seconds (on top of any delay it already has).</summary>
        private static Clip MysticLater(Clip clip, float delay)
        {
            if (delay > 0) { clip.age -= delay; if (clip.renderer) clip.renderer.enabled = false; }
            return clip;
        }

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

        /// <summary>A pool of soft light lying on the floor under a seal, behind the actors.</summary>
        private Clip MysticGroundBloom(Vector2 ground, float width, Color color, float life)
        {
            var clip = Spawn("Mystic floor bloom", glow, ground, width, color, life, true, 25); clip.scale.y *= .32f; clip.fadeIn = .05f; clip.fadePower = 1.4f;
            return clip;
        }

        /// <summary>A ragged painted stroke segment that tapers at both ends; chained segments overlap where they taper.</summary>
        private Clip MysticBrush(Vector2 a, Vector2 b, float thickness, Color color, float life, bool additive, int order)
        {
            Vector2 delta = b - a; float length = Mathf.Max(.02f, delta.magnitude) * 1.16f;
            var clip = Spawn("Mystic brush", MysticStroke, (a + b) * .5f, length, color, life, additive, order);
            clip.scale = new Vector3(length / MysticStroke.bounds.size.x, thickness / MysticStroke.bounds.size.y, 1);
            clip.angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg; clip.ease = 1;
            clip.root.transform.localScale = clip.scale; clip.root.transform.localRotation = Quaternion.Euler(0, 0, clip.angle);
            return clip;
        }

        /// <summary>A baked vine (light body or thread) stretched from <paramref name="from"/> (its pivot) to <paramref name="to"/>,
        /// <paramref name="thickness"/> across, so one clip draws the whole creeping stroke with no joints.</summary>
        private Clip MysticVine(Vector2 from, Vector2 to, Sprite sprite, float thickness, Color color, float life, bool additive, int order)
        {
            Vector2 delta = to - from; float length = Mathf.Max(.05f, delta.magnitude);
            var clip = Spawn("Mystic vine", sprite, from, length, color, life, additive, order);
            Vector3 size = sprite.bounds.size;
            clip.scale = new Vector3(length / size.x, thickness / size.y, 1); clip.root.transform.localScale = clip.scale;
            clip.angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg; clip.root.transform.localRotation = Quaternion.Euler(0, 0, clip.angle);
            clip.ease = 1;
            return clip;
        }

        /// <summary>A painted line's light level (white core, straight fall-off to 0 at <paramref name="width"/>).</summary>
        private static float MysticLine(float distance, float width) { return Mathf.Clamp01(1 - Mathf.Abs(distance) / width); }

        // ---------------------------------------------------------------- textures

        // Built once after the first scene loads (the core builds its own textures when the effects are created), and
        // again on first use if that has not happened. Light shapes are level profiles for the painted light material (crisp
        // outer edges, smooth fall-off, white-hot centres, streaks along each stroke); painted objects (pole, crest, cloth,
        // petals, leaves) carry soft shading in rgb, lit from the upper left, with a thin deep edge of their own colour where
        // an outline would have been.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void MysticPrebuild() { MysticEnsure(); }

        private static void MysticEnsure()
        {
            if (MysticSpiral) return;

            // A painted swirl stroke along a circle: a razor bright outer edge at its head, colour streaking back along the
            // stroke and falling off inward, tapering away over 230 degrees behind the head (a counter-clockwise spin leads
            // with the head).
            MysticArc = Build("Mystic swirl stroke", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .99f) return Color.clear;
                float th = Mathf.Atan2(v, u), t = Mathf.Repeat(-th * Mathf.Rad2Deg, 360) / 230f; if (t > 1) return Color.clear;
                float thick = .17f * (1 - .75f * t) + .012f, outer = .9f - .05f * t;
                float depth = (outer - r) / thick; if (depth > 1) return Color.clear;
                float edge = 1 - Smooth(-.004f, .006f, r - outer);
                float streaks = .76f + .5f * (RingNoise(th, r, 1.8f, 22) - .5f);
                return White(Mathf.Clamp01((1 - Mathf.Max(0, depth)) * edge * streaks * Smooth(0, .03f, t) * Mathf.Lerp(1.1f, .6f, t)));
            });

            // A painted water wall for the funnel: a wide band along most of a circle with a crisp ragged edge, its light
            // falling smoothly inward from a bright rim with streaks running along the water.
            MysticVeil = Build("Mystic water wall", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .99f) return Color.clear;
                float th = Mathf.Atan2(v, u), t = Mathf.Repeat(-th * Mathf.Rad2Deg, 360) / 290f; if (t > 1) return Color.clear;
                float thick = .2f * (1 - .5f * t) + .03f, outer = .92f - .04f * t;
                float rag = (RingNoise(th, r, 2.5f, 6) - .5f) * .5f, depth = (outer - r) / thick;
                float edge = 1 - Smooth(-.006f, .006f, r - outer);
                float body = Mathf.Pow(Mathf.Clamp01(1 - depth - rag * .35f), 1.3f) * (.78f + .44f * (RingNoise(th, r, 1.6f, 20) - .5f));
                return White(Mathf.Clamp01(edge * body * Smooth(0, .04f, t) * (1 - Smooth(.75f, 1, t + rag * .3f)) * .95f));
            });

            // The body of the vortex funnel (bottom pivot): a cone narrow at the floor and open at the top, brighter along its
            // walls, cut into slanting strokes of water, its middle hollow over whatever stands inside it; lit from the left.
            MysticFunnel = Build("Mystic funnel", 128, 256, (u, v) =>
            {
                float h = (v + 1) * .5f, half = Mathf.Lerp(.32f, .98f, Mathf.Pow(h, .8f)), x = Mathf.Abs(u) / half;
                if (x > 1) return Color.clear;
                float wall = Mathf.Clamp01((x - .3f) / .62f) * (1 - Smooth(.95f, 1, x));
                float slant = Mathf.Sin((u * 2.4f - h * 7.5f) * Mathf.PI + Fbm(u * 3 + 7, h * 6 + 2, 2) * 2);
                float top = .86f + .1f * Fbm(u * 5 + 1, 4, 2);
                float ends = Smooth(0, .05f, h) * Mathf.Clamp01((top - h) / .14f);
                float l = .7f + .28f * Smooth(.6f, -.6f, u);
                return new Color(l, l, l, Mathf.Clamp01(wall * (.4f + .6f * Smooth(-.15f, .15f, slant)) * ends * 1.1f));
            }, new Vector2(.5f, 0));

            // Painted petals (base at -u, pointed tip at +u): a gentle cup lit from the upper left, a pale midrib, fine veins, a
            // lit upper rim and brush grain. The plain one is shading only (for tinted light copies); the pink one fades from a
            // deep rose base through pink to a blush-white tip; the leaf runs from deep green to a sunlit yellow-green.
            MysticPetal = Build("Mystic lotus petal", 96, 48, (u, v) => MysticPetalPaint(u, v, new Color(.8f, .8f, .8f), Color.white, Color.white, Color.white));
            MysticPetalPink = Build("Mystic painted petal", 96, 48, (u, v) => MysticPetalPaint(u, v, new Color(.76f, .18f, .44f), new Color(1, .47f, .7f), new Color(1, .86f, .92f), new Color(1, .95f, .97f)));
            MysticLeafPaint = Build("Mystic painted leaf", 96, 48, (u, v) => MysticPetalPaint(u, v, new Color(.08f, .32f, .15f), new Color(.2f, .6f, .28f), new Color(.5f, .85f, .36f), new Color(.85f, 1, .6f)));

            // A ragged painted stroke (along +x) tapering at both ends.
            MysticStroke = Build("Mystic brush stroke", 128, 32, (u, v) =>
            {
                float taper = Mathf.Pow(Mathf.Max(0, 1 - u * u), .6f), half = .75f * taper + .02f;
                float rag = (Mathf.PerlinNoise(u * 6 + 3, 1.7f) - .5f) * .35f;
                float across = Mathf.Clamp01(1 - Mathf.Abs(v) / (half * (1 + rag)));
                return White(Mathf.Clamp01(across * 1.15f * (.85f + .3f * (Fbm(u * 2 + 3, v * 8 + 1, 2) - .5f))));
            });

            // Thorned vines of light (base at -u, tip at +u, pivot at the base) and their hot threads, two windings each.
            MysticTendril = new Sprite[2]; MysticTendrilLight = new Sprite[2];
            for (int i = 0; i < 2; i++)
            {
                int variant = i;
                MysticTendril[i] = Build("Mystic tendril " + i, 512, 96, (u, v) => MysticVinePaint(u, v, variant, false), new Vector2(0, .5f));
                MysticTendrilLight[i] = Build("Mystic tendril light " + i, 512, 96, (u, v) => MysticVinePaint(u, v, variant, true), new Vector2(0, .5f));
            }

            // The curse brand as white-cored strokes: a painted outer band, a thin ring with ticks, eight thorns stabbing
            // inward, an almond eye and a slit pupil.
            MysticBrand = Build("Mystic curse brand", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .99f) return Color.clear;
                float th = Mathf.Atan2(v, u), deg = Mathf.Repeat(th * Mathf.Rad2Deg, 360);
                float a = Mathf.Clamp01((r - .85f) / .1f) * (1 - Smooth(.95f, .965f, r)) * (.8f + .4f * (RingNoise(th, r, 2, 18) - .5f));
                a = Mathf.Max(a, MysticLine(r - .815f, .013f) * .8f);
                float tick = Mathf.Abs(Mathf.Repeat(deg + 7.5f, 15) - 7.5f) * Mathf.Deg2Rad * r;
                if (r > .74f && r < .815f) a = Mathf.Max(a, MysticLine(tick, .013f) * .7f);
                float sector = Mathf.Repeat(th, Mathf.PI / 4) - Mathf.PI / 8, along = (.8f - r) / .26f;
                if (along > 0 && along < 1) { float half = .085f * (1 - along) + .004f; a = Mathf.Max(a, Mathf.Clamp01(1 - Mathf.Abs(sector) * r / half) * (1 - along * .3f)); }
                float ex = u / .62f, lid = .3f * Mathf.Max(0, 1 - ex * ex);
                if (Mathf.Abs(ex) < 1.02f) a = Mathf.Max(a, MysticLine(Mathf.Abs(v) - lid, .032f) * .95f);
                if (Mathf.Abs(v) < lid) a = Mathf.Max(a, MysticLine(r - .2f, .022f) * .75f);
                float pupil = Mathf.Sqrt(Sq(u / .07f) + Sq(v / .23f));
                a = Mathf.Max(a, Mathf.Clamp01((1 - pupil) * 2.5f));
                return White(a);
            });

            // The banner pole (bottom pivot): a turned wooden shaft lit from the left (cylinder light, a soft specular streak,
            // grain) with gold bands catching a bright highlight; its shaded side turns deep brown, never black, and no outline.
            MysticPole = Build("Mystic banner pole", 32, 256, (u, v) =>
            {
                float h = (v + 1) * .5f, x = Mathf.Abs(u), ends = Smooth(0, .01f, h) * (1 - Smooth(.99f, 1, h));
                float shaft = (1 - Smooth(.46f, .54f, x)) * ends;
                if (shaft <= 0) return Color.clear;
                float n = Mathf.Clamp(u / .5f, -1, 1), z = Mathf.Sqrt(Mathf.Max(0, 1 - n * n));
                float light = Mathf.Clamp01(-.6f * n + .55f * z + .15f), spec = Gauss(n + .45f, .18f);
                float grain = .86f + .14f * Mathf.PerlinNoise(u * 3 + 5, h * 60) + .06f * (Fbm(u * 2, h * 14, 2) - .5f);
                Color wood = Color.Lerp(new Color(.26f, .11f, .04f), new Color(.68f, .4f, .19f), light) * grain + new Color(1, .85f, .6f) * spec * .16f;
                float band = Mathf.Max(Mathf.Max(1 - Smooth(.012f, .02f, Mathf.Abs(h - .06f)), 1 - Smooth(.008f, .014f, Mathf.Abs(h - .52f))), 1 - Smooth(.012f, .02f, Mathf.Abs(h - .95f)));
                Color gold = Color.Lerp(new Color(.55f, .3f, .06f), new Color(1, .82f, .4f), light) + new Color(1, .95f, .8f) * spec * .45f;
                Color c = Color.Lerp(wood, gold, band); c.a = shaft; return c;
            }, new Vector2(.5f, 0));

            // The crest: a gold spear blade over a rayed sun disc with a crimson jewel, on a socket. Painted metal lit from the
            // upper left (a bevelled blade with a bright ridge, a domed disc, rays catching the light), a jewel with a soft
            // highlight, and a thin deep-amber edge where the metal turns away instead of an outline.
            MysticCrest = Build("Mystic banner crest", 128, 128, (u, v) =>
            {
                float body = MysticCrestMask(u, v, 0);
                if (body <= 0) return Color.clear;
                float inner = MysticCrestMask(u, v, -.03f);
                float s = (v - .1f) / .88f, d = Mathf.Sqrt(u * u + Sq(v + .3f)), light;
                if (d < .2f) light = .5f + .45f * Mathf.Clamp01((-u * .5f + (v + .3f) * .6f) / .2f + .35f);
                else if (s > 0 && d > .47f)
                {
                    float half = .2f * Mathf.Pow(Mathf.Max(.05f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(s))), .7f), a = Mathf.Clamp(u / half, -1, 1);
                    light = Mathf.Clamp01(.6f - .38f * a + .12f * s) + Gauss(a + .05f, .14f) * .3f;
                }
                else light = .58f + .32f * Mathf.Cos(Mathf.Atan2(v + .3f, u) - 2.2f);
                light *= .92f + .16f * (Fbm(u * 6 + 2, v * 6 + 5, 2) - .5f);
                Color gold = Color.Lerp(new Color(.5f, .25f, .04f), new Color(1, .8f, .34f), Mathf.Clamp01(light));
                gold = Color.Lerp(gold, new Color(1, .97f, .82f), Smooth(.8f, 1.05f, light));
                float jewel = 1 - Smooth(.085f, .105f, d);
                Color gem = Color.Lerp(new Color(.45f, .02f, .07f), new Color(1, .3f, .3f), Mathf.Clamp01(.5f - (u + .03f) * 3 + (v + .3f) * 3));
                gem = Color.Lerp(gem, Color.white, Gauss(Mathf.Sqrt(Sq(u + .03f) + Sq(v + .27f)), .025f) * .9f);
                Color c = Color.Lerp(gold, gem, jewel);
                c = Color.Lerp(new Color(.52f, .24f, .04f), c, Mathf.Clamp01(.35f + inner * .65f));
                c.a = body; return c;
            });

            // A flower seal on the floor: a painted outer band, ten petal outlines with midribs, pearls between the tips,
            // five seeds and a heart. Lines only, no fill.
            MysticBloomSeal = Build("Mystic bloom seal", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .99f) return Color.clear;
                float th = Mathf.Atan2(v, u);
                float a = Mathf.Clamp01((r - .88f) / .08f) * (1 - Smooth(.95f, .965f, r)) * (.8f + .4f * (RingNoise(th, r, 2, 18) - .5f));
                a = Mathf.Max(a, MysticLine(r - .3f, .016f) * .8f);
                const float step = 2 * Mathf.PI / 10;
                float local = Mathf.Repeat(th + step * .5f, step) - step * .5f, t = (r - .3f) / .52f;
                if (t > 0 && t < 1.02f)
                {
                    float half = step * .46f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)), .7f);
                    a = Mathf.Max(a, MysticLine((Mathf.Abs(local) - half) * r, .016f) * .92f);
                    if (Mathf.Abs(local) < half) a = Mathf.Max(a, MysticLine(local * r, .01f) * .45f * (1 - t));
                }
                float pearl = Mathf.Repeat(th, step) - step * .5f;
                a = Mathf.Max(a, MysticLine(Mathf.Sqrt(Sq(pearl * r) + Sq(r - .845f)), .03f) * .9f);
                for (int i = 0; i < 5; i++) { float pa = i * 72 * Mathf.Deg2Rad; a = Mathf.Max(a, MysticLine(Mathf.Sqrt(Sq(u - Mathf.Cos(pa) * .15f) + Sq(v - Mathf.Sin(pa) * .15f)), .045f) * .85f); }
                a = Mathf.Max(a, MysticLine(r, .06f));
                return White(a);
            });

            // The war flag (pole side at the left) and its gold edge light, each sliced into strips that ripple separately.
            MysticFlagStrips = MysticSlice(Build("Mystic war flag", 288, 176, (u, v) => MysticFlagPaint(u, v, false)).texture, "Mystic war flag strip");
            MysticFlagGlowStrips = MysticSlice(Build("Mystic war flag glow", 288, 176, (u, v) => MysticFlagPaint(u, v, true)).texture, "Mystic war flag glow strip");

            // A whirlpool seen from above: five painted arms wound tight, each a stroke with a white leading edge and colour
            // falling back behind it, streaked along the arm, around an empty eye. Spinning it counter-clockwise makes the arms
            // flow inward. Built last: it marks the set as complete.
            MysticSpiral = Build("Mystic whirlpool", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .99f) return Color.clear;
                float phi = Mathf.Atan2(v, u) * 5 - Mathf.Log(r + .03f) * 6.5f, turns = phi / (2 * Mathf.PI) + .5f;
                float p = Mathf.Repeat(turns, 1); int arm = ((int)Mathf.Floor(turns) % 5 + 5) % 5;
                if (p < .4f || p > .77f) return Color.clear;
                float stroke = Mathf.Clamp01((p - .4f) / .35f) * (1 - Smooth(.745f, .77f, p));
                float streaks = .74f + .52f * (Fbm(p * 7 + arm * 3.1f, Mathf.Log(r + .03f) * 1.6f + arm * 1.7f, 2) - .5f);
                float radial = Smooth(.06f, .28f, r) * Mathf.Clamp01((.97f - r) / .3f);
                return White(Mathf.Clamp01(stroke * streaks * radial * (.62f + .48f * r)));
            });
        }

        /// <summary>A painted petal along +x (base at -u, tip at +u): a gentle cup lit softly from the upper left, coloured from
        /// <paramref name="root"/> at the base through <paramref name="body"/> to <paramref name="tip"/>, with a lit upper rim, a
        /// pale midrib and fine veins washed toward <paramref name="sheen"/>, and brush grain. Alpha rises toward the midrib, so a
        /// petal erodes from its rim as it fades.</summary>
        private static Color MysticPetalPaint(float u, float v, Color root, Color body, Color tip, Color sheen)
        {
            float t = (u + 1) * .5f, half = .86f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.PI * Mathf.Pow(t, 1.3f))), .8f);
            float inside = 1 - Smooth(half - .07f, half + .01f, Mathf.Abs(v));
            if (inside <= 0) return Color.clear;
            float rib = .1f * Mathf.Sin(t * 3), across = Mathf.Clamp((v - rib) / Mathf.Max(.05f, half), -1, 1);
            float light = GraceBladeLight(across * .7f, u * .8f);
            Color c = Color.Lerp(root, body, Smooth(0, .45f, t));
            c = Color.Lerp(c, tip, Smooth(.5f, 1, t) * .85f);
            float shade = .68f + .4f * light;
            c = new Color(c.r * shade, c.g * shade, c.b * shade);
            float rim = Gauss(Mathf.Abs(across) - .9f, .07f) * (across > 0 ? .5f : .15f) * Smooth(.15f, .6f, t);
            float midrib = MysticLine(v - rib, .05f) * .25f * Smooth(.1f, .5f, t) * (1 - t);
            float veins = Mathf.Pow(Mathf.Abs(Mathf.Sin(across * 7 + t * 2)), 14) * .08f * Smooth(.1f, .5f, t);
            c = Color.Lerp(c, sheen, Mathf.Clamp01(rim + midrib + veins + Smooth(.65f, 1, light) * .25f));
            float grain = .92f + .16f * (Fbm(u * 5 + 7, v * 8 + 2, 2) - .5f);
            return new Color(Mathf.Clamp01(c.r * grain), Mathf.Clamp01(c.g * grain), Mathf.Clamp01(c.b * grain), inside * (.34f + .66f * Mathf.Pow(1 - Mathf.Abs(across), .8f)));
        }

        /// <summary>A creeping vine of light along +x (base at -u, tip at +u): a ragged wavy stroke tapering to a point with thorns
        /// hooking off it toward the tip, lit smoothly toward one side; with <paramref name="light"/>, the thin white-cored
        /// thread inside it.</summary>
        private static Color MysticVinePaint(float u, float v, int variant, bool light)
        {
            float x = (u + 1) * .5f, env = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(x * .96f + .02f)), .7f);
            float y = Mathf.Sin(x * 2 * Mathf.PI * (1.6f + .35f * variant) + variant * 1.9f + .4f) * .5f * env;
            float half = .2f * (1 - .85f * x) + .025f, d = v - y;
            float ends = Smooth(0, .02f, x) * (1 - Smooth(.96f, 1, x));
            if (light)
            {
                float thread = Mathf.Clamp01(1 - Mathf.Abs(d) / (half * .55f));
                float streak = .8f + .4f * (Fbm(x * 4 + variant * 3, d * 16 + 1, 2) - .5f);
                return White(Mathf.Clamp01(thread * streak * 1.1f) * ends);
            }
            float rag = (Fbm(x * 9 + variant * 5, v * 2 + 3, 2) - .5f) * .4f;
            float body = 1 - Smooth(half * (1 + rag) - .02f, half * (1 + rag), Mathf.Abs(d));
            float thorn = 0;
            for (int t = 0; t < 6; t++)
            {
                float tx = .12f + t * .14f + variant * .03f; if (tx > .92f) break;
                float side = t % 2 == 0 ? 1 : -1, len = .22f * (1 - tx) + .07f;
                float q = (d * side - half * .5f) / len; if (q < 0 || q > 1) continue;
                float a = (x - tx) * 5.3f, wq = .15f * (1 - q);
                thorn = Mathf.Max(thorn, 1 - Smooth(wq - .02f, wq, Mathf.Abs(a - .35f * q)));
            }
            float l = Mathf.Lerp(.55f, 1, Smooth(-half, half, d));
            // Alpha rises toward the spine of the stroke (thorns are thin), so the vine erodes from its edges as it fades.
            float core = .55f + .45f * Mathf.Clamp01(1 - Mathf.Abs(d) / Mathf.Max(.01f, half));
            return new Color(l, l, l, Mathf.Max(body * core, thorn * .6f) * ends);
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

        /// <summary>The swallow-tailed war flag: painted crimson cloth (a smooth fall of light down the cloth, soft folds hanging
        /// from the pole, weave grain and a thin deep-crimson hem instead of an outline) with a gold border and a gold sun painted
        /// as metal lit from the upper left. With <paramref name="glow"/> it returns only the gold parts as light levels, for the
        /// edge light.</summary>
        private static Color MysticFlagPaint(float u, float v, bool glow)
        {
            const float halfH = .8f;
            float x = (u + 1) * .5f, ay = Mathf.Abs(v), tail = .8f + .2f * Mathf.Clamp01(ay / halfH);
            float d = Mathf.Min(halfH - ay, Mathf.Min((x - .015f) * 2 * MysticFlagAspect, (tail - x) * 2 * MysticFlagAspect * .82f));
            float inside = Smooth(-.012f, .012f, d);
            float ex = (x - .43f) * 2 * MysticFlagAspect, er = Mathf.Sqrt(ex * ex + v * v);
            float ray = Mathf.Abs(Mathf.Repeat(Mathf.Atan2(v, ex) * Mathf.Rad2Deg + 15, 30) - 15) / 15f, along = (er - .3f) / .24f;
            float rays = along > 0 && along < 1 ? 1 - Smooth(.45f * (1 - along) - .08f, .45f * (1 - along), ray) : 0;
            float emblem = Mathf.Clamp01(Mathf.Max(1 - Smooth(.17f, .2f, er), Mathf.Max(MysticLine(er - .26f, .03f), rays)));
            float border = 1 - Smooth(.1f, .125f, d), pinstripe = MysticLine(d - .16f, .014f);
            if (glow) return White(Mathf.Max(Mathf.Max(border * .72f, emblem * .95f), pinstripe * .5f) * inside);
            if (inside <= 0) return Color.clear;
            float fall = Mathf.Lerp(.62f, 1, Smooth(-.8f, .55f, v));
            float folds = .9f + .1f * Mathf.Sin(x * 19 + Mathf.Sin(v * 2.3f) * .8f) * Smooth(.05f, .3f, x);
            float weave = .93f + .07f * Mathf.PerlinNoise(x * 40 + 3, v * 26 + 1) + .05f * (Fbm(x * 4 + 3, v * 3 + 1, 2) - .5f);
            float shade = fall * folds * weave * Mathf.Lerp(.72f, 1, Smooth(0, .06f, d));
            Color cloth = Color.Lerp(new Color(.42f, .02f, .06f), new Color(.92f, .14f, .16f), shade);
            cloth = Color.Lerp(cloth, new Color(1, .5f, .42f), Smooth(.92f, 1.05f, shade) * .5f);
            float metalLight = Mathf.Clamp01((.55f + .45f * (v / halfH) - .2f * x) * fall + .15f) * (.85f + .3f * MysticLine(d - .06f, .05f));
            float emblemLight = er < .3f ? Mathf.Clamp01(.55f + (-ex * .5f + v * .6f) * 1.2f) : metalLight;
            Color gold = Color.Lerp(new Color(.6f, .34f, .07f), new Color(1, .86f, .45f), Mathf.Lerp(metalLight, emblemLight, emblem));
            gold = Color.Lerp(gold, new Color(1, .97f, .8f), Smooth(.85f, 1.05f, Mathf.Lerp(metalLight, emblemLight, emblem)) * .6f);
            float metal = Mathf.Max(border, Mathf.Max(emblem, pinstripe * .8f));
            Color paint = Color.Lerp(cloth, gold, metal); paint.a = inside;
            return paint;
        }
    }
}
