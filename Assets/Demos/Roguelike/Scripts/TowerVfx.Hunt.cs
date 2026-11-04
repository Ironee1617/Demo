using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Hunting tools: the returning war disc, the bouncing hot slug, the hunter's mark and the chain hook.
    // The disc, the chain and the hook are painted metal: smooth light from the upper left over rounded edges, brushed grain,
    // a pale rim on the lit edges, a coloured bounce on the shadowed ones and sharp specular glints, with a thin edge of the
    // metal's own deep hue where an outline would be. A light layer of glints and glowing edges lies over each, a soft bloom
    // under it. Energy (the slug, the brand, the reticle, sight lines, wakes and lashes) is painted light: a level profile
    // that the light material ramps from a deep edge through the hue to a white core, over a deep saturated copy of the same
    // silhouette (never wider, never near-black) that keeps it on the pale sky, over a bloom. Hits use the hand-animated
    // flipbooks: hit sparks flare and scatter, shock fronts race out and break, and the slug and the mark end in small blasts.
    // Skill level grows density, glow and secondary layers; level three adds gold edges and runes. Rings that mark an area
    // keep its gameplay radius.
    // A caster keeps walking while it winds up, so every wind-up rides along with its caster, and the release burst is
    // booked rather than timed: it fires when the shot or the trail actually leaves, at the hand where the caster is then
    // (a cast cancelled by a dash never shows one).
    public sealed partial class TowerVfx
    {
        // ---------------------------------------------------------------- palette, textures and rigs

        private static readonly Color HuntDiscLight = new Color(1, .8f, .42f), HuntDiscHot = new Color(1, .95f, .8f), HuntDiscDeep = new Color(.86f, .38f, .03f, .45f),
            HuntSlugLight = new Color(1, .9f, .58f), HuntSlugHot = new Color(1, .99f, .92f), HuntSlugBolt = new Color(1, .76f, .32f), HuntSlugFire = new Color(1, .58f, .16f), HuntSlugDeep = new Color(.92f, .46f, .03f, .45f),
            HuntMarkLight = new Color(1, .22f, .24f), HuntMarkHot = new Color(1, .55f, .5f), HuntMarkDeep = new Color(.72f, .04f, .1f, .7f),
            HuntSteelLight = new Color(.7f, .85f, 1), HuntSteelHot = new Color(.9f, .97f, 1), HuntSteelDeep = new Color(.16f, .34f, .78f, .45f),
            HuntEmber = new Color(1, .76f, .4f), HuntGold = new Color(1, .9f, .58f),
            // painted smoke kicked out of each tool's hits: grey with a breath of the tool's hue, never black
            HuntDiscSmoke = new Color(.52f, .4f, .3f, .45f), HuntSlugSmoke = new Color(.54f, .42f, .3f, .42f),
            HuntMarkSmoke = new Color(.5f, .22f, .24f, .45f), HuntSteelSmoke = new Color(.4f, .45f, .55f, .45f);

        // Painted objects come as [0] the painted body (ink, baked shading) and [1] its light layer (glints and glowing edges).
        // Light shapes come as [0] the light level and [1] its solid silhouette, which the deep contrast copy is drawn from, so
        // that copy is exactly as wide as the light and never rims it in a darker line.
        private static Sprite[] HuntDiscSet, HuntChainSet, HuntHookSet, HuntReticleSet, HuntChevronSet, HuntSlugSet, HuntBrandSet, HuntDiscBlurSet;
        // The disc's sheen: it stays lit from the upper left while the blades turn under it.
        private static Sprite HuntDiscSheen;

        private sealed class HuntDiscRig { public Transform root; public int grade; }
        // A wake behind a shot whose front stays put while it grows with the distance flown, so it never pokes out behind the shooter.
        private sealed class HuntTrailLayer { public Clip clip; public float baseX, baseY, front, length; }
        private sealed class HuntSlugRig
        {
            public Transform root; public int grade, bounces; public Vector2 dir, last, flownFrom, hitAt, hitDir; public float esc = 1, haloAlpha = .8f;
            public Clip halo, star, crown, hitFlash; public Vector3 haloScale, starScale, crownScale; public List<HuntTrailLayer> trails = new List<HuntTrailLayer>();
        }
        // A booked release burst: where the caster's body and hand sit relative to its anchor, and how to draw the burst.
        private sealed class HuntBooking
        {
            public SkillEffect effect; public Transform anchor; public Vector2 rest, body, hand; public Color light; public float s, windup; public int g; public Clip carrier;
        }
        // A wind-up line from the caster's hand to a fixed point on the prey, re-drawn every frame as the caster moves.
        private sealed class HuntSight { public Clip clip; public Vector2 local, target; public float end; }

        private readonly List<HuntDiscRig> HuntDiscs = new List<HuntDiscRig>();
        private readonly List<HuntSlugRig> HuntSlugs = new List<HuntSlugRig>();
        private readonly List<HuntBooking> HuntBookings = new List<HuntBooking>();
        private Clip HuntDiscHitMark, HuntSlugHitMark;
        // The hooks thrown this frame (gameplay reels the n-th one to .95 + n * .55 units in front of the thrower), the last one's
        // bite and whether that enemy stays put (bosses and flyers are never reeled; bosses are never dazed).
        private int HuntHookFrame = -1, HuntHookIndex, HuntHookSide = 1;
        private bool HuntHookHeld, HuntHookBoss;
        private Vector2 HuntHookTo, HuntHookEnd;
        // The marks loosed this frame all leave from the drawn sigil.
        private int HuntMarkFrame = -1;
        private Vector2 HuntMarkOrigin, HuntMarkFrom;
        private TowerGame HuntGame;

        // ---------------------------------------------------------------- textures (built at load)

        private static Sprite HuntSprite(string name, int width, int height, Color32[] colors)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "Tower effect " + name, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 2 };
            texture.SetPixels32(colors); texture.Apply(true, false);
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private struct HuntTexel
        {
            public Color body; public float light;
            public HuntTexel(Color body, float light) { this.body = body; this.light = light; }
        }

        /// <summary>Paints an object in one pass: <paramref name="shade"/>(x, y) gives the painted body colour (alpha = cover) and
        /// the light layer's level there; x spans -1..1 across the width and y keeps the same scale. Empty texels take the
        /// <paramref name="bleed"/> colour, so filtering never darkens the silhouette's edge into an outline. A strip that tiles
        /// end to end (<paramref name="tile"/>) keeps its ends unfaded.</summary>
        private static Sprite[] HuntPaint(string name, int width, int height, bool tile, Color bleed, Func<float, float, HuntTexel> shade)
        {
            var body = new Color32[width * height]; var light = new Color32[width * height];
            float aspect = height / (float)width;
            for (int y = 0; y < height; y++)
            {
                float v = (y + .5f) / height * 2 - 1;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + .5f) / width * 2 - 1;
                    var s = shade(u, v * aspect);
                    float window = (tile ? 1 : 1 - Smooth(.92f, 1, Mathf.Abs(u))) * (1 - Smooth(.9f, 1, Mathf.Abs(v)));
                    Color b = s.body; b.a *= window;
                    if (b.a < .004f) b = new Color(bleed.r, bleed.g, bleed.b, 0);
                    body[y * width + x] = b;
                    light[y * width + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(s.light * window) * 255));
                }
            }
            return new[] { HuntSprite(name, width, height, body), HuntSprite(name + " light", width, height, light) };
        }

        /// <summary>A shape of light: [0] its level (<paramref name="level"/>(x, y), x across -1..1, y at the same scale) and [1] its
        /// solid silhouette at the same size.</summary>
        private static Sprite[] HuntLight(string name, int width, int height, Func<float, float, float> level)
        {
            var lit = new Color32[width * height]; var solid = new Color32[width * height];
            float aspect = height / (float)width;
            for (int y = 0; y < height; y++)
            {
                float v = (y + .5f) / height * 2 - 1;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + .5f) / width * 2 - 1;
                    float l = Mathf.Clamp01(level(u, v * aspect)) * (1 - Smooth(.94f, 1, Mathf.Abs(u))) * (1 - Smooth(.92f, 1, Mathf.Abs(v)));
                    lit[y * width + x] = new Color32(255, 255, 255, (byte)(l * 255));
                    solid[y * width + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(l * 9) * 255));
                }
            }
            return new[] { HuntSprite(name, width, height, lit), HuntSprite(name + " solid", width, height, solid) };
        }

        /// <summary>Painted metal at a point of a solid (distance <paramref name="d"/>, outward direction gx, gy): smooth light from
        /// the upper left over edges rounded across <paramref name="round"/> units, brushed <paramref name="grain"/>, a pale rim on
        /// the lit edges, a coloured bounce on the shadowed ones and a specular glint where the rounding faces the light. No
        /// outline: a thin edge of the metal's own deep hue lines the shadowed side. <paramref name="glint"/> returns the specular
        /// light and the lit rim for the light layer.</summary>
        private static Color HuntMetal(float d, float gx, float gy, float round, float grain, Color shadow, Color mid, Color lit, Color edge, Color bounce, out float glint)
        {
            float depth = Mathf.Max(0, -d), lean = (1 - Smooth(0, round, depth)) * .9f;
            float nx = gx * lean, ny = gy * lean, nz = Mathf.Sqrt(Mathf.Max(0, 1 - lean * lean));
            float diffuse = Mathf.Clamp01(-.5f * nx + .6f * ny + .62f * nz);
            float spec = Mathf.Pow(Mathf.Clamp01(-.28f * nx + .34f * ny + .9f * nz), 30);
            float l = (.1f + .95f * diffuse) * (.86f + .28f * (grain - .5f));
            Color c = Color.Lerp(shadow, mid, Smooth(.15f, .55f, l));
            c = Color.Lerp(c, lit, Smooth(.55f, .9f, l));
            float rim = Gauss(depth - .012f, .014f), litSide = Mathf.Clamp01(-gx * .64f + gy * .77f), darkSide = Mathf.Clamp01(gx * .64f - gy * .77f);
            c = Color.Lerp(c, bounce, rim * darkSide * .65f);
            c = Color.Lerp(c, edge, Gauss(depth, .012f) * (1 - litSide) * .7f);
            c = Color.Lerp(c, new Color(1, .99f, .95f), Mathf.Clamp01(spec * 1.2f + rim * litSide * .35f));
            glint = Mathf.Clamp01(spec * 1.2f);
            c.a = 1 - Smooth(-.01f, .006f, d);
            return c;
        }

        /// <summary>Painted steel (cool blue-grey, a cyan bounce, a deep blue edge) for a solid given by its distance field.</summary>
        private static HuntTexel HuntSteel(Func<float, float, float> field, float x, float y, float round)
        {
            float d = field(x, y); if (d > .02f) return new HuntTexel(Color.clear, 0);
            const float e = .008f;
            float gx = field(x + e, y) - field(x - e, y), gy = field(x, y + e) - field(x, y - e), gl = Mathf.Sqrt(gx * gx + gy * gy);
            if (gl > 1e-6f) { gx /= gl; gy /= gl; }
            float glint;
            var c = HuntMetal(d, gx, gy, round, Fbm(x * 5 + 3, y * 15 + 5, 2), new Color(.13f, .18f, .32f), new Color(.3f, .39f, .55f), new Color(.64f, .74f, .9f),
                new Color(.12f, .25f, .58f), new Color(.4f, .76f, 1), out glint);
            return new HuntTexel(c, glint * (1 - Smooth(-.004f, .008f, d)));
        }

        /// <summary>The light level of a painted stroke from its distance field: a deep edge rising to <paramref name="peak"/> on its
        /// spine over <paramref name="spine"/> units, with brush grain.</summary>
        private static float HuntLevel(float d, float spine, float peak, float grain)
        {
            float inside = 1 - Smooth(-.008f, .006f, d); if (inside <= 0) return 0;
            return inside * (.16f + (peak - .16f) * Smooth(0, spine, -d)) * (.88f + .26f * (grain - .5f));
        }

        /// <summary>The light level across a painted stroke (0 on its spine, 1 at its edge): a thin white line down the middle,
        /// a narrow pale band, a wide saturated body and a deep rim, so the stroke reads mostly as its colour.</summary>
        private static float HuntStroke(float across) { return Mathf.Max(.98f - 1.5f * across, .46f - .36f * across); }

        private static float HuntTaper(float px, float py, float ax, float ay, float bx, float by, float r0, float r1)
        {
            float dx = bx - ax, dy = by - ay, t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
            return Mathf.Sqrt(Sq(px - (ax + dx * t)) + Sq(py - (ay + dy * t))) - Mathf.Lerp(r0, r1, t);
        }

        // A chakram: a solid disc carrying eight hooked blades whose tips lead a clockwise spin, its face a recessed plate with
        // four raised spokes, a raised hub ring and a gem. outer is the distance to the blades' cutting contour, t where a point
        // sits along its blade (0 at the tip), spoke the distance to the raised spokes.
        private static float HuntDiscField(float x, float y, out float outer, out float t, out float spoke)
        {
            float r = Mathf.Sqrt(x * x + y * y), th = Mathf.Atan2(y, x);
            t = Mathf.Repeat(th / (2 * Mathf.PI) * 8, 1);
            float tooth = .6f + .28f * Mathf.Pow(1 - t, 1.7f), face = .785f * r, outside = r - tooth;
            if (outside > 0) outer = r < .88f ? Mathf.Min(outside * .8f, (1 - t) * face) : outside * .8f;
            else outer = -Mathf.Min(-outside * .8f, t * face);
            spoke = 1e9f;
            for (int k = 0; k < 4; k++)
            {
                float a = (45 + k * 90) * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                spoke = Mathf.Min(spoke, SegmentDistance(x, y, ca * .16f, sa * .16f, ca * .44f, sa * .44f) - .05f);
            }
            return Mathf.Min(outer, r - .6f);
        }

        // A strip of four chain links (face-on rings alternating with edge-on bars) that tiles end to end along +x.
        private static float HuntChainField(float x, float y)
        {
            float best = 1e9f;
            for (int j = -1; j <= 4; j++)
            {
                float c = -.75f + j * .5f, d;
                if ((j & 1) == 0) { float q = Mathf.Sqrt(Sq((x - c) / .3f) + Sq(y / .15f)); d = Mathf.Abs(q - 1) * .17f - .06f; }
                else d = SegmentDistance(x, y, c - .27f, 0, c + .27f, 0) - .07f;
                best = Mathf.Min(best, d);
            }
            return best;
        }

        // A harpoon hook pointing +x: an eye for the chain, a shaft, a spear point and a barbed J curling down and back.
        private static float HuntHookField(float x, float y)
        {
            if (y > .36f || y < -.86f) return 1;
            float eye = Mathf.Abs(Mathf.Sqrt(Sq(x + .74f) + y * y) - .12f) - .07f;
            float shaft = SegmentDistance(x, y, -.62f, 0, .34f, 0) - .1f;
            float halfWidth = .2f * Mathf.Clamp01(1 - (x - .26f) / .64f);
            float point = Mathf.Max(Mathf.Max(Mathf.Abs(y) - halfWidth, .26f - x), x - .9f) * .92f;
            float hx = x - .1f, hy = y + .3f, hr = Mathf.Sqrt(hx * hx + hy * hy);
            float s = Mathf.Repeat(90 - Mathf.Atan2(hy, hx) * Mathf.Rad2Deg, 360);
            const float sweep = 235;
            float endA = (90 - sweep) * Mathf.Deg2Rad;
            float ex = .1f + Mathf.Cos(endA) * .3f, ey = -.3f + Mathf.Sin(endA) * .3f, tx = Mathf.Sin(endA), ty = -Mathf.Cos(endA);
            float curl = s <= sweep ? Mathf.Abs(hr - .3f) - Mathf.Lerp(.105f, .055f, s / sweep) : Mathf.Sqrt(Sq(x - ex) + Sq(y - ey)) - .055f;
            float barb = HuntTaper(x, y, ex, ey, ex + tx * .24f, ey + ty * .24f, .066f, .008f);
            return Mathf.Min(Mathf.Min(Mathf.Min(eye, shaft), Mathf.Min(point, curl)), barb);
        }

        // A reticle: a ring broken at the cardinals where four pointed ticks cross it, an inner ring and a centre dot.
        private static float HuntReticleField(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y), deg = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            float cd = Mathf.Abs(Mathf.Repeat(deg + 45, 90) - 45), rad = cd * Mathf.Deg2Rad;
            float arc = Mathf.Max(Mathf.Abs(r - .72f) - .055f, (16 - cd) * Mathf.Deg2Rad * r);
            float along = r * Mathf.Cos(rad), across = r * Mathf.Sin(rad), half = .12f * Mathf.Clamp01((along - .46f) / .44f);
            float tick = Mathf.Max(Mathf.Max(across - half, .46f - along), along - .92f) * .9f;
            float inner = Mathf.Abs(r - .3f) - .032f, dot = r - .07f;
            return Mathf.Min(Mathf.Min(arc, tick), Mathf.Min(inner, dot));
        }

        // A chevron pointing +x (a jaw of the reticle, the head of the mark's arrow).
        private static float HuntChevronField(float x, float y)
        {
            return Mathf.Min(HuntTaper(x, y, -.55f, .6f, .6f, 0, .12f, .17f), HuntTaper(x, y, -.55f, -.6f, .6f, 0, .12f, .17f));
        }

        private static void HuntDiscTextures()
        {
            if (HuntDiscSet != null) return;
            // The disc, painted: polished bronze-gold whose rounded edges are lit the same on every blade (the light from the upper
            // left is the fixed sheen drawn over the spinning disc), concentric turning marks, blades bright toward their leading
            // tips and deep toward their roots, a mid-gold rim band, a deep recessed plate inside an engraved groove with raised
            // spokes and a raised hub ring catching the light, a thin deep amber edge in place of an outline and a hot amber gem.
            // Its light layer is the glowing cutting edges, the gem's white heat and a glint along the hub ring.
            HuntDiscSet = HuntPaint("Hunt war disc", 160, 160, false, new Color(.86f, .48f, .1f), (x, y) =>
            {
                float outer, t, spoke, d = HuntDiscField(x, y, out outer, out t, out spoke);
                if (d > .03f) return new HuntTexel(Color.clear, 0);
                float r = Mathf.Sqrt(x * x + y * y), depth = Mathf.Max(0, -d), hub = Mathf.Abs(r - .24f) - .055f;
                float lean = 1 - Smooth(0, .08f, depth), face = Mathf.Sqrt(Mathf.Max(0, 1 - lean * lean * .8f));
                float lathe = Fbm(r * 11 + 3, 2.3f, 2), blade = Smooth(.58f, .64f, r), plate = 1 - Smooth(.42f, .45f, r);
                float tone = Mathf.Lerp(.55f, Mathf.Lerp(.36f, .72f, Mathf.Pow(1 - t, 1.4f)), blade);
                tone = Mathf.Lerp(tone, .3f + .06f * r / .44f, plate);
                if (spoke < 0) tone = Mathf.Lerp(tone, .5f, plate);
                if (hub < 0) tone = .6f;
                float l = tone * (.72f + .3f * face) * (.86f + .28f * (lathe - .5f)) + .36f * Gauss(depth - .03f, .018f);
                l += (spoke < .005f ? .3f * Gauss(spoke + .022f, .014f) * plate : 0) + .3f * Gauss(hub + .025f, .014f);
                l += .22f * Gauss(r - .458f, .009f) - .22f * Gauss(r - .44f, .011f);
                Color c = Color.Lerp(new Color(.42f, .17f, .03f), new Color(.86f, .48f, .1f), Smooth(.15f, .5f, l));
                c = Color.Lerp(c, new Color(1, .8f, .4f), Smooth(.5f, .8f, l));
                c = Color.Lerp(c, new Color(1, .96f, .85f), Smooth(.8f, 1, l));
                c = Color.Lerp(c, new Color(.66f, .28f, .03f), Gauss(depth, .016f) * .7f);
                c = Color.Lerp(c, new Color(1, .74f, .3f), 1 - Smooth(.06f, .095f, r));
                c.a = 1 - Smooth(-.012f, .006f, d);
                float lit = Mathf.Max(Gauss(outer + .03f, .022f) * Smooth(.58f, .65f, r) * .85f, (1 - Smooth(.02f, .085f, r)) * 1.05f);
                lit = Mathf.Max(lit, Gauss(hub + .025f, .016f) * .45f);
                return new HuntTexel(c, lit * (1 - Smooth(-.004f, .01f, d)));
            });
            // The sheen over the solid rim band (always metal under it, whatever the blades' turn): a bright crescent of
            // reflected light on the upper left, a dimmer one opposite and a star glint on the lit rim.
            HuntDiscSheen = Build("Hunt disc sheen", 128, 128, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .9f) return Color.clear;
                float deg = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
                float band = Smooth(.46f, .49f, r) * (1 - Smooth(.57f, .6f, r));
                float key = Gauss(Mathf.DeltaAngle(deg, 128), 32), back = Gauss(Mathf.DeltaAngle(deg, -52), 18) * .45f;
                float level = band * (key * .92f + back) * (.85f + .3f * (Fbm(deg * .04f + 3, r * 14, 2) - .5f));
                float gx = u - Mathf.Cos(128 * Mathf.Deg2Rad) * .53f, gy = v - Mathf.Sin(128 * Mathf.Deg2Rad) * .53f;
                float star = Gauss(gx, .024f) * Sq(Mathf.Max(0, 1 - Mathf.Abs(gy) / .3f)) + Gauss(gy, .024f) * Sq(Mathf.Max(0, 1 - Mathf.Abs(gx) / .3f)) + Gauss(Mathf.Sqrt(gx * gx + gy * gy), .045f);
                return White(Mathf.Clamp01(Mathf.Max(level, star)));
            });
            // The spinning rim's motion: two painted sweeps of unequal length whose blunt heads lead a clockwise spin and
            // whose tails taper to dry points counter-clockwise behind them. Each has a razor outer edge (white-hot along the
            // head), a level falling off inward and brush streaks running round the stroke.
            HuntDiscBlurSet = HuntLight("Hunt disc sweep", 128, 128, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .97f || r < .28f) return 0;
                float th = Mathf.Atan2(v, u), deg = th * Mathf.Rad2Deg, best = 0;
                float streaks = RingNoise(th, r, 1.6f, 17);
                for (int k = 0; k < 2; k++)
                {
                    float head = k == 0 ? 0 : 196, span = k == 0 ? 175 : 112;
                    float behind = Mathf.Repeat(deg - head, 360); if (behind > span) continue;
                    float t = behind / span, outer = (k == 0 ? .92f : .86f) - .07f * t;
                    float thick = (k == 0 ? .34f : .24f) * Mathf.Pow(1 - t, 1.2f) + .008f;
                    float depth = (outer - r) / thick; if (depth < -.06f || depth > 1) continue;
                    float edge = 1 - Smooth(-.06f, 0, -depth), cap = Smooth(0, 7, behind);
                    best = Mathf.Max(best, edge * cap * HuntStroke(Mathf.Max(0, depth)) * (.82f + .45f * (streaks - .5f)) * (1 - .45f * t));
                }
                return best;
            });
        }

        private static void HuntChainTextures()
        {
            if (HuntChainSet != null) return;
            HuntDiscTextures();
            // Painted steel: each link a rounded bar of metal lit from the upper left, glinting along its lit flank.
            HuntChainSet = HuntPaint("Hunt chain", 128, 32, true, new Color(.3f, .39f, .55f), (x, y) => HuntSteel(HuntChainField, x, y, .065f));
            HuntHookSet = HuntPaint("Hunt hook", 128, 128, false, new Color(.3f, .39f, .55f), (x, y) =>
            {
                var texel = HuntSteel(HuntHookField, x, y, .09f);
                // the spear point burns white at its tip
                if (texel.body.a > .5f) texel.light = Mathf.Max(texel.light, Gauss(x - .84f, .07f) * .85f);
                return texel;
            });
        }

        private static void HuntMarkTextures()
        {
            if (HuntReticleSet != null) return;
            HuntReticleSet = HuntLight("Hunt reticle", 128, 128, (x, y) =>
                Mathf.Max(HuntLevel(HuntReticleField(x, y), .045f, .74f, Fbm(x * 3 + 7, y * 3 + 2, 2)), (1 - Smooth(.025f, .07f, Mathf.Sqrt(x * x + y * y))) * 1.05f));
            HuntChevronSet = HuntLight("Hunt chevron", 64, 64, (x, y) => HuntLevel(HuntChevronField(x, y), .1f, .95f, Fbm(x * 2 + 5, y * 2 + 1, 2)));
            // The hunter's brand, laid on like one brush glyph: a bold ring that lands at the upper left, swells, and runs on
            // past its start into a dry tail; four fangs of uneven length biting in on the diagonals; a diamond eye. Every
            // stroke is a light level (hard outer edge, a white line down its middle) with streaks running along it.
            HuntBrandSet = HuntLight("Hunt brand", 256, 256, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > .99f) return 0;
                float th = Mathf.Atan2(v, u), deg = Mathf.Repeat(th * Mathf.Rad2Deg - 118, 360), best = 0;
                for (int lap = 0; lap < 2; lap++)
                {
                    float s = deg + lap * 360; if (s > 392) continue;
                    float t = s / 392, half = .125f * Mathf.Sin(Mathf.PI * Mathf.Pow(t, .62f)) + .003f, centre = .7f - .045f * t;
                    float across = Mathf.Abs(r - centre) / half; if (across > 1.06f) continue;
                    float streaks = RingNoise(th, r, 1.3f, 24);
                    best = Mathf.Max(best, (1 - Smooth(.94f, 1.03f, across)) * HuntStroke(across) * (.86f + .4f * (streaks - .5f)));
                }
                for (int k = 0; k < 4; k++)
                {
                    float angle = (45 + 90 * k + (k == 1 ? 7 : k == 3 ? -5 : 0)) * Mathf.Deg2Rad, ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
                    float along = u * ca + v * sa, side = -u * sa + v * ca, tip = k % 2 == 0 ? .4f : .47f;
                    if (along < tip || along > .97f) continue;
                    float t = (along - tip) / (.97f - tip), half = .19f * Mathf.Pow(t, .85f) + .003f, across = Mathf.Abs(side) / half;
                    if (across > 1.06f) continue;
                    float streaks = Fbm(along * 3 + k * 5, side * 26 + 3, 2);
                    best = Mathf.Max(best, (1 - Smooth(.93f, 1.03f, across)) * (1 - Smooth(.9f, .97f, along)) * HuntStroke(across) * Mathf.Lerp(1.08f, .8f, t) * (.86f + .4f * (streaks - .5f)));
                }
                float eye = (Mathf.Abs(u) + Mathf.Abs(v) * 1.25f) / .16f;
                if (eye < 1.05f) best = Mathf.Max(best, (1 - Smooth(.93f, 1.03f, eye)) * (1.15f - eye * .9f));
                return best;
            });
        }

        private static void HuntSlugTextures()
        {
            if (HuntSlugSet != null) return;
            // A hot slug pointing +x, painted as a bolt of light: an ogive head burning white down its spine, a gold body and a
            // tail that thins to a point, with heat streaks running along it.
            HuntSlugSet = HuntLight("Hunt slug", 128, 64, (x, y) =>
            {
                float half = x > .3f ? .25f * Mathf.Sqrt(Mathf.Max(0, 1 - Sq((x - .3f) / .62f))) : .25f * Mathf.Pow(Mathf.Clamp01((x + .96f) / 1.26f), .85f);
                if (half < .004f) return 0;
                float across = Mathf.Abs(y) / half; if (across > 1.05f) return 0;
                float heat = Smooth(-.85f, .45f, x), grain = Fbm(x * 2.6f + 3, y / half * 1.4f + 7, 2);
                float body = Mathf.Pow(Mathf.Max(0, 1 - across), .7f) * (.3f + .78f * heat);
                return (1 - Smooth(.94f, 1.04f, across)) * (.14f + .95f * body) * (.86f + .3f * (grain - .5f));
            });
        }

        // ---------------------------------------------------------------- small helpers

        private static float HuntScale(int grade) { return grade >= 3 ? 1.3f : grade == 2 ? 1.15f : 1; }
        private static Color HuntTone(Color baseLight, Color tint) { return Color.Lerp(baseLight, new Color(tint.r, tint.g, tint.b, 1), .35f); }
        private static float HuntAngle(Vector2 d) { return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; }
        private static Vector2 HuntAim(Vector2 d, Vector2 fallback) { return d.sqrMagnitude > .0001f ? d.normalized : fallback; }
        /// <summary>The ease exponent that finishes a start-to-end scale change in about <paramref name="seconds"/> of a longer life.</summary>
        private static float HuntPop(float life, float seconds) { return Mathf.Log(.08f) / Mathf.Log(1 - Mathf.Clamp(seconds / Mathf.Max(.01f, life), .001f, .9f)); }
        private static Clip HuntTurn(Clip clip, float angle) { clip.angle = angle; clip.root.transform.localRotation = Quaternion.Euler(0, 0, angle + clip.angleOffset); return clip; }
        private static Clip HuntOpaque(Clip clip) { clip.renderer.sharedMaterial = AlphaBlend; return clip; }
        private static Clip HuntMirror(Clip clip) { clip.scale.x = -clip.scale.x; clip.root.transform.localScale = clip.scale; return clip; }
        private static Clip HuntMirrorY(Clip clip) { clip.scale.y = -clip.scale.y; clip.root.transform.localScale = clip.scale; return clip; }
        private static Clip HuntFlat(Clip clip, float y) { clip.scale.y *= y; clip.root.transform.localScale = clip.scale; return clip; }
        /// <summary>Moves a free clip at once (position, angle and scale), for clips a controller poses every frame.</summary>
        private static void HuntPlace(Clip clip, Vector2 pos, float angle)
        {
            clip.position = pos; clip.angle = angle;
            if (!clip.root) return;
            var t = clip.root.transform;
            t.position = new Vector3(pos.x, pos.y, 0); t.localRotation = Quaternion.Euler(0, 0, angle + clip.angleOffset); t.localScale = clip.scale;
        }
        /// <summary>Re-draws a free light streak (made by Line) between <paramref name="a"/> and <paramref name="b"/>.</summary>
        private static void HuntAimLine(Clip line, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a; float old = line.scale.x;
            line.scale.x = Mathf.Max(.02f, d.magnitude) / streak.bounds.size.x; line.position = (a + b) * .5f; line.angle = HuntAngle(d);
            if (!line.root) return;
            var t = line.root.transform;
            t.position = new Vector3(line.position.x, line.position.y, 0); t.localRotation = Quaternion.Euler(0, 0, line.angle + line.angleOffset);
            if (old != 0) { var size = t.localScale; size.x *= line.scale.x / old; t.localScale = size; }
        }
        /// <summary>Lets a clip ride along with a hooked enemy while it is reeled <paramref name="ride"/> over about .16 seconds.
        /// Under the simulation's drag a start speed of v travels v / drag in all, so 18 * ride with drag 18 lands on the spot.</summary>
        private static Clip HuntRide(Clip clip, Vector2 ride)
        {
            if (ride.sqrMagnitude < .0001f) return clip;
            clip.velocity += ride * 18; clip.drag = 18; return clip;
        }

        /// <summary>A painted object riding on <paramref name="anchor"/> (or free): [0] the painted body, [1] its light layer (glints
        /// and glowing edges) and, when <paramref name="bloom"/> has alpha, [2] a soft bloom under it.</summary>
        private Clip[] HuntObject(Transform anchor, Sprite[] set, Vector2 pos, float angle, float width, float life, Color shine, Color bloom, int order)
        {
            var body = HuntTurn(Anchored(anchor, pos, "Hunt painted object", set[0], width, Color.white, life, order - 1, false), angle);
            var lit = HuntTurn(Anchored(anchor, pos, "Hunt painted object light", set[1], width, shine, life, order), angle);
            if (bloom.a <= 0) return new[] { body, lit };
            return new[] { body, lit, Anchored(anchor, pos, "Hunt painted object bloom", glow, width * 1.8f, bloom, life, order - 4) };
        }

        /// <summary>A shape of painted light riding on <paramref name="anchor"/> (or free): [0] the light, then a deep saturated copy
        /// of its silhouette under it when <paramref name="deep"/> has alpha, then a soft bloom when <paramref name="bloom"/> is above 0.</summary>
        private List<Clip> HuntGlyph(Transform anchor, Sprite[] set, Vector2 pos, float angle, float width, float life, Color light, Color deep, float bloom, int order)
        {
            var list = new List<Clip> { HuntTurn(Anchored(anchor, pos, "Hunt light shape", set[0], width, light, life, order), angle) };
            if (deep.a > 0) list.Add(HuntTurn(Anchored(anchor, pos, "Hunt light shape depth", set[1], width, deep, life, order - 1, false), angle));
            if (bloom > 0) list.Add(Anchored(anchor, pos, "Hunt light shape bloom", glow, width * 1.35f, Alpha(light, bloom), life, order - 2));
            return list;
        }

        /// <summary>True while <paramref name="clip"/> is alive, younger than <paramref name="window"/> and near <paramref name="pos"/>.</summary>
        private bool HuntRecent(Clip clip, float window, Vector2 pos, float distance)
        {
            return clip != null && clip.age < window && (clip.position - pos).sqrMagnitude < distance * distance && clips.Contains(clip);
        }

        private HuntDiscRig HuntNearestDisc(Vector2 pos, float range)
        {
            HuntDiscs.RemoveAll(x => !x.root);
            HuntDiscRig best = null; float bestD = range * range;
            foreach (var rig in HuntDiscs) { float d = ((Vector2)rig.root.position - pos).sqrMagnitude; if (d < bestD) { bestD = d; best = rig; } }
            return best;
        }

        private HuntSlugRig HuntNearestSlug(Vector2 pos, float range)
        {
            HuntSlugs.RemoveAll(x => !x.root);
            HuntSlugRig best = null; float bestD = range * range;
            foreach (var rig in HuntSlugs) { float d = ((Vector2)rig.root.position - pos).sqrMagnitude; if (d < bestD) { bestD = d; best = rig; } }
            return best;
        }

        /// <summary>A hit: the hand-animated spark (a flare that splits into shards and scatters) in the tool's hue over a bloom.</summary>
        private Clip HuntSpark(Vector2 pos, float width, Color light, float life, int order = 133)
        {
            var bloom = Spawn("Hunt hit bloom", glow, pos, width * 1.4f, Alpha(light, .55f), Mathf.Max(.2f, life * 1.8f), true, order - 4);
            bloom.endScale = 1.35f; bloom.ease = 1; bloom.fadePower = 1.4f;
            return SparkFlip(pos, width, Alpha(light, 1), life, order);
        }

        /// <summary>A sonic shock front standing across <paramref name="dir"/>: the shock flipbook squashed into a ring seen edge-on,
        /// opening and breaking into arcs as it rides forward.</summary>
        private Clip HuntBoom(Vector2 pos, Vector2 dir, float width, Color color, float life, float speed, bool additive = true, int order = 130)
        {
            var clip = Spawn("Hunt sonic ring", shockFrames[0], pos, width, color, life, additive, order);
            clip.frames = shockFrames; clip.loop = false;
            clip.scale.x *= .32f; clip.root.transform.localScale = clip.scale; HuntTurn(clip, HuntAngle(dir));
            clip.startScale = .45f; clip.endScale = 1; clip.ease = 3; clip.hold = .4f; clip.fadePower = 1; clip.velocity = dir * speed; clip.drag = 5; return clip;
        }

        /// <summary>A soft bloom stretched along a segment: the glow round a lash, a shaft or a muzzle tongue.</summary>
        private Clip HuntGlowLine(Vector2 a, Vector2 b, float thickness, Color color, float life, int order)
        {
            Vector2 delta = b - a; float length = Mathf.Max(.05f, delta.magnitude);
            var clip = Spawn("Hunt glow line", glow, (a + b) * .5f, length, color, life, true, order);
            clip.scale = new Vector3(length * 1.45f / glow.bounds.size.x, thickness / glow.bounds.size.y, 1); clip.root.transform.localScale = clip.scale;
            clip.ease = 1; clip.fadePower = 1.3f;
            return HuntTurn(clip, HuntAngle(delta));
        }

        /// <summary>Needle sparks flung in a cone of ±<paramref name="spread"/> degrees round <paramref name="dir"/> at uneven speeds, white and the hue.</summary>
        private void HuntNeedles(Vector2 pos, Vector2 dir, float spread, int count, float speed, Color light, float life, float size = .34f, float gravity = 5)
        {
            float a = HuntAngle(dir);
            for (int i = 0, n = Budget(count); i < n; i++)
                Spark(pos + Inside(.08f), Direction(a + Range(-spread, spread)) * speed * Range(.45f, 1.1f), i % 3 == 0 ? Color.white : i % 3 == 1 ? light : Color.Lerp(light, Color.white, .45f),
                    size * Range(.6f, 1.3f), life * Range(.6f, 1), gravity, 2.6f);
        }

        /// <summary>Painted smoke knocked out of a hit: a few puffs set round the struck point (most of them along <paramref name="dir"/>,
        /// one thrown back), already open when they appear so they frame the spark instead of hiding under it.</summary>
        private void HuntInk(Vector2 pos, Vector2 dir, float size, Color color, int count, float life)
        {
            float a = HuntAngle(dir);
            for (int i = 0, n = Budget(count); i < n; i++)
            {
                Vector2 d = Direction(a + (i == 2 ? 180 : 0) + Range(-65, 65));
                var ink = Puff(pos + d * size * Range(.4f, .75f), size * Range(.75f, 1.1f), color, d * size * Range(1.4f, 2.8f) + Vector2.up * size * .5f, life * Range(.8f, 1.1f), 117);
                ink.startScale = .8f; ink.endScale = 1.35f; ink.fadeIn = 0; ink.hold = .3f; ink.fadePower = 1.2f;
            }
        }

        /// <summary>A painted crescent sweep (PaintArc: the coloured stroke and its white leading edge) carried along by
        /// <paramref name="velocity"/>; returns both strokes.</summary>
        private Clip[] HuntArc(Vector2 pos, float width, float angle, Color color, float life, int order, Vector2 velocity, float drag)
        {
            var arc = PaintArc(pos, width, angle, color, life, order); var edge = clips[clips.Count - 1];
            foreach (var x in new[] { arc, edge }) { x.velocity = velocity; x.drag = drag; }
            return new[] { arc, edge };
        }

        // ---------------------------------------------------------------- per-frame control, wind-ups that follow, booked releases

        private Clip HuntInvisible(string name, Vector2 pos, float life) { return Spawn(name, glow, pos, .1f, new Color(0, 0, 0, 0), life, true, 110); }

        /// <summary>Runs <paramref name="step"/>(clip, seconds since the last run) once every frame the clip lives. An emitter runs at a
        /// throttled rate (and up to six times in one frame), so a very high rate with a once-per-age guard makes it a per-frame
        /// controller that keeps its pace when the effect count is high or the frame rate is above sixty.</summary>
        private static void HuntControl(Clip clip, Action<Clip, float> step)
        {
            float seen = 0;
            Emitter(clip, 100000, 0, (owner, p) =>
            {
                if (owner.age <= seen) return;
                float dt = owner.age - seen; seen = owner.age; step(owner, dt);
            });
        }

        /// <summary>A free invisible clip that watches a gameplay root and calls <paramref name="lost"/> with its last position once it is gone.</summary>
        private void HuntWatch(Transform root, float maxLife, Action<Vector2> lost)
        {
            Vector2 last = root.position; bool fired = false;
            HuntControl(HuntInvisible("Hunt shot watch", last, maxLife), (owner, dt) =>
            {
                if (fired) return;
                if (root) { last = root.position; return; }
                fired = true; owner.life = owner.age; lost(last);
            });
        }

        private HuntSight HuntSightOf(Transform anchor, Clip line, Vector2 start, Vector2 target)
        {
            return new HuntSight { clip = line, local = anchor ? start - (Vector2)anchor.position : start, target = target, end = line.life - line.age };
        }

        /// <summary>Keeps a wind-up with its caster, who may walk on while casting: every free clip spawned since <paramref name="mark"/>
        /// is carried along with <paramref name="anchor"/> each frame (clips riding the anchor follow by themselves), and sight lines are
        /// re-drawn from the hand to their fixed end. A dash cancels the cast in gameplay and moves the caster faster than any run,
        /// so then the whole wind-up fades out at once.</summary>
        private void HuntFollow(Transform anchor, int mark, params HuntSight[] sights)
        {
            if (!anchor || mark >= clips.Count) return;
            int n = clips.Count - mark; var riders = new Clip[n]; var ends = new float[n]; var aimed = new bool[n]; float life = .05f;
            for (int i = 0; i < n; i++)
            {
                var x = clips[mark + i]; riders[i] = x; ends[i] = x.life - x.age;
                foreach (var sight in sights) if (ReferenceEquals(sight.clip, x)) aimed[i] = true;
                if (!float.IsInfinity(ends[i])) life = Mathf.Max(life, ends[i]);
            }
            Vector2 last = anchor.position; bool cancelled = false;
            HuntControl(HuntInvisible("Hunt wind-up follow", last, life), (owner, dt) =>
            {
                if (!anchor) return;
                Vector2 now = anchor.position, delta = now - last; last = now;
                // a dash runs 19 units a second sideways; a run stays under 7 (jumps, falls and launch pads move it vertically)
                if (!cancelled && Mathf.Abs(delta.x) > 13 * dt + .02f) cancelled = true;
                bool moved = delta.sqrMagnitude > 1e-8f;
                if (!moved && !cancelled) return;
                for (int i = 0; i < riders.Length; i++)
                {
                    // a clip past its life is gone, and its pooled object may already draw something else
                    if (owner.age >= ends[i] - .02f) continue;
                    var x = riders[i];
                    if (cancelled) { x.color.a *= .45f; x.emit = Emit.None; }
                    if (!moved || aimed[i] || !ReferenceEquals(x.external, null) || !x.root) continue;
                    x.position += delta; x.orbitCenter += delta; x.root.transform.position = new Vector3(x.position.x, x.position.y, 0);
                }
                if (moved) foreach (var sight in sights) if (owner.age < sight.end - .02f) HuntAimLine(sight.clip, now + sight.local, sight.target);
            });
        }

        /// <summary>Books a cast's release burst. It fires when the shot or trail actually leaves (so a cancelled cast shows none),
        /// at the hand where the caster is by then.</summary>
        private void HuntExpect(SkillEffect effect, CastInfo c, Vector2 hand, Color light, float s, int g)
        {
            Vector2 rest = c.anchor ? (Vector2)c.anchor.position : c.pos;
            HuntBookings.Add(new HuntBooking { effect = effect, anchor = c.anchor, rest = rest, body = c.pos - rest, hand = hand - rest, light = light, s = s, g = g, windup = c.windup,
                carrier = HuntInvisible("Hunt release booking", hand, c.windup + .3f) });
        }

        /// <summary>Takes the booked release of <paramref name="effect"/> by the caster whose shot or trail leaves from <paramref name="origin"/>
        /// and gives the caster's present hand and body. Every booking of that cast is used up (a wind-up and an instant re-cast of
        /// the same release); the real wind-up's is drawn.</summary>
        private HuntBooking HuntClaim(SkillEffect effect, Vector2 origin, out Vector2 hand, out Vector2 body)
        {
            hand = body = origin; HuntBooking best = null;
            for (int i = HuntBookings.Count - 1; i >= 0; i--)
            {
                var b = HuntBookings[i];
                bool anchored = !ReferenceEquals(b.anchor, null);
                if ((anchored && !b.anchor) || !clips.Contains(b.carrier)) { HuntBookings.RemoveAt(i); continue; }
                if (b.effect != effect) continue;
                Vector2 rest = anchored ? (Vector2)b.anchor.position : b.rest;
                if ((rest + b.body - origin).sqrMagnitude > 1) continue;
                HuntBookings.RemoveAt(i); b.carrier.life = b.carrier.age;
                if (best == null || b.windup > best.windup) { best = b; hand = rest + b.hand; body = rest + b.body; }
            }
            return best;
        }

        /// <summary>Whether the enemy struck at <paramref name="at"/> stays where it is: bosses and flyers are never reeled in, and bosses
        /// are never dazed (<paramref name="boss"/>). Read from the fighters the game keeps; false when none matches.</summary>
        private bool HuntHookHolds(Vector2 at, out bool boss)
        {
            boss = false;
            if (!HuntGame) HuntGame = GetComponentInParent<TowerGame>();
            if (!HuntGame || HuntGame.enemies == null) return false;
            foreach (var e in HuntGame.enemies)
            {
                if (e == null) continue;
                Vector2 chest = e.pos + Vector2.up * (e.isBoss ? 1.2f : .7f);
                if ((chest - at).sqrMagnitude > .0025f) continue;
                boss = e.isBoss; return e.isBoss || e.airborne;
            }
            return false;
        }

        // ---------------------------------------------------------------- Boomerang: the returning war disc

        private void CastBoomerang(CastInfo c)
        {
            HuntDiscTextures();
            float w = c.windup; int f = c.facing, g = Grade; float gs = HuntScale(g);
            Color light = HuntTone(HuntDiscLight, c.tint);
            // The disc forms at the height the thrown disc leaves from (the small hero's eye line) but a step ahead of the face,
            // so the throw barely jumps and the caster's face stays clear.
            Vector2 hand = c.pos + new Vector2(f * .95f, -.05f), feet = c.pos + Vector2.down;
            float width = 1.2f * gs;
            HuntExpect(SkillEffect.Boomerang, c, hand, light, w < .2f ? gs * .75f : gs, g);
            if (w < .2f) return;
            int mark = clips.Count;
            Underlay(hand, 2.6f * gs, .22f, w + .1f).fadeIn = .1f;
            // the war disc forming in the throwing hand, spinning up from a spark: painted gold whose cutting edges glow, under
            // a sheen that stays lit from the upper left while the blades turn
            var disc = new List<Clip>(HuntObject(c.anchor, HuntDiscSet, hand, 0, width, w + .03f, Alpha(HuntDiscHot, .95f), Alpha(light, .45f), 128));
            disc.Add(Anchored(c.anchor, hand, "Disc sheen", HuntDiscSheen, width, Alpha(HuntDiscHot, .9f), w + .03f, 129));
            foreach (var x in disc) { x.fadeIn = w * .3f; x.startScale = .15f; x.endScale = 1; x.ease = 2.4f; x.hold = .9f; x.fadePower = 2; }
            for (int i = 0; i < 2; i++) { disc[i].spin = -f * 720; if (f < 0) HuntMirror(disc[i]); }
            var blur = Anchored(c.anchor, hand, "Disc spin blur", HuntDiscBlurSet[0], width * 1.35f, Alpha(light, g >= 3 ? .6f : .8f), w + .03f, 126);
            var blurDeep = Anchored(c.anchor, hand, "Disc spin blur depth", HuntDiscBlurSet[1], width * 1.35f, HuntDiscDeep, w + .03f, 117, false);
            foreach (var x in new[] { blur, blurDeep }) { x.spin = -f * 1500; x.fadeIn = w * .4f; x.startScale = .3f; x.endScale = 1; x.ease = 2; x.hold = .9f; x.fadePower = 2; if (f < 0) HuntMirror(x); }
            // the disc's glow and the awakened rune sit behind the caster, so they light the air around it without washing it out
            var halo = Anchored(c.anchor, hand, "Disc halo", glow, 2.3f * gs, Alpha(light, .34f), w + .05f, 28);
            halo.startScale = .2f; halo.endScale = 1; halo.ease = 1.5f; halo.pulse = .08f; halo.hold = .85f; halo.fadePower = 2;
            Converge(hand, 10, 2.2f * gs, light, w, .5f);
            // gold sparks flung off the rim as it spins up
            Emitter(disc[1], 26 * gs, 0, (owner, p) =>
            {
                float grow = Mathf.Clamp01(owner.age / Mathf.Max(.05f, w)), a = Range(0, 360);
                Vector2 rim = (Vector2)owner.root.transform.position + Direction(a) * width * .42f * grow;
                Spark(rim, Direction(a - f * 90) * Range(2.5f, 5) + Vector2.up * .6f, Chance(.35f) ? Color.white : Chance(.5f) ? HuntEmber : light, Range(.16f, .26f), Range(.16f, .26f), 3, 2.5f);
            });
            // footing, and the awakened disc's gold edge and rune
            GroundGlow(feet, 3.4f * gs, Alpha(light, .24f), w + .3f);
            if (g >= 2) { var seal = RuneCircle(feet, 2.8f * gs, Alpha(light, .75f), w + .35f, f * 70, true); seal.startScale = .5f; }
            if (g >= 3)
            {
                Vector2 crownAt = hand + new Vector2(f * .3f, 0);
                var crown = Anchored(c.anchor, crownAt, "Disc awakened rune", runeRing, width * 1.6f, Alpha(HuntGold, .55f), w + .05f, 28);
                crown.spin = f * 160; crown.startScale = .4f; crown.endScale = 1; crown.ease = 3; crown.fadeIn = .1f; crown.hold = .85f; crown.fadePower = 2;
                // a gold copy of the glowing edges just outside the blades, turning with the metal
                var gilt = Anchored(c.anchor, hand, "Disc awakened edge", HuntDiscSet[1], width * 1.1f, Alpha(HuntGold, .9f), w + .03f, 126);
                gilt.spin = -f * 720; if (f < 0) HuntMirror(gilt);
                gilt.startScale = .15f; gilt.endScale = 1; gilt.ease = 2.4f; gilt.hold = .9f; gilt.fadeIn = w * .3f; gilt.fadePower = 2;
            }
            HuntFollow(c.anchor, mark);
        }

        /// <summary>The throw: a hit spark, a painted sweep of the arm opening along the throw (tilted with the spin) over its deep
        /// copy, burst lines stretched along the throw, sonic shock fronts, a cone of needles and a kick of dust.</summary>
        private void HuntDiscRelease(Vector2 pos, Vector2 dir, Color light, float s, int g, Vector2 feet)
        {
            float a = HuntAngle(dir); int f = dir.x >= 0 ? 1 : -1;
            HuntSpark(pos, 1.9f * s, Color.Lerp(light, HuntDiscHot, .35f), .14f, 133);
            var lines = ImpactLines(pos + dir * .4f, 2.8f * s, Alpha(light, .85f), .18f); lines.angle = a; lines.scale.y *= .55f;
            float tilt = a - f * 14;
            HuntArc(pos + dir * .55f, 2.7f * s, tilt, Alpha(light, .95f), .26f, 130, dir * 6, 4);
            var sweepDeep = HuntOpaque(Crescent(pos + dir * .55f, 2.65f * s, HuntDiscDeep, .22f, tilt, 128)); sweepDeep.velocity = dir * 6; sweepDeep.drag = 4;
            sweepDeep.startScale = .82f; sweepDeep.endScale = 1.06f; sweepDeep.ease = 6; sweepDeep.hold = .25f;
            HuntBoom(pos + dir * .4f, dir, 2.1f * s, Alpha(HuntDiscHot, .9f), .26f, 9);
            HuntBoom(pos + dir * .4f, dir, 2.1f * s, HuntDiscDeep, .24f, 9, false, 127);
            HuntNeedles(pos + dir * .2f, dir, 34, 9, 10 * s, light, .28f, .38f, 4);
            if (g >= 3) ShockFlip(pos, 1.9f * s, Alpha(HuntGold, .85f), .28f, 131);
            for (int i = 0, n = Budget(3); i < n; i++)
                Puff(feet + new Vector2(-f * Range(.1f, .5f), .1f), Range(.5f, .8f), DustTone, new Vector2(-f * Range(1.5f, 3), Range(.2f, .7f)), Range(.4f, .6f), 118);
        }

        private void ProjectileBoomerang(Transform root, Color tint, bool signature)
        {
            HuntDiscTextures();
            int g = signature ? Grade : 1; float gs = HuntScale(g);
            Color light = HuntTone(HuntDiscLight, tint);
            Vector2 at = root.position, dir = HuntAim(root.right, Vector2.right); int side = dir.x >= 0 ? 1 : -1;
            if (signature)
            {
                Vector2 hand, body; var booked = HuntClaim(SkillEffect.Boomerang, at, out hand, out body);
                if (booked != null) HuntDiscRelease(hand + dir * .3f, dir, booked.light, booked.s, booked.g, body + Vector2.down);
            }
            float width = (signature ? 1.25f : .9f) * gs, spin = -side * 1080, start = Range(0, 360);
            // The root keeps its launch rotation on the way back, so everything that turns spins on its own. The painted metal is
            // drawn over all of the disc's light (and the level's aura, from level two), so the bladed silhouette never whites out;
            // only its glowing edges and the fixed sheen lie on top of it.
            var body0 = Attach(root, "War disc", HuntDiscSet[0], width, Color.white, 129, false);
            var edge = Attach(root, "War disc edges", HuntDiscSet[1], width, Alpha(HuntDiscHot, .95f), 130);
            // the sheen stays lit from the upper left whichever way the root was launched
            var sheen = Attach(root, "War disc sheen", HuntDiscSheen, width, Alpha(HuntDiscHot, .9f), 131); sheen.angle = -root.eulerAngles.z;
            var blur = Attach(root, "War disc blur", HuntDiscBlurSet[0], width * 1.4f, Alpha(light, g >= 3 ? .45f : g == 2 ? .6f : .8f), 124);
            var blurDeep = Attach(root, "War disc blur depth", HuntDiscBlurSet[1], width * 1.4f, HuntDiscDeep, 117, false);
            var halo = Attach(root, "War disc glow", glow, width * 2.3f, Alpha(light, g >= 3 ? .2f : g == 2 ? .3f : .42f), 116); halo.pulse = .08f;
            var layers = new List<Clip> { edge, sheen, blur, blurDeep, halo };
            foreach (var x in new[] { body0, edge }) { x.spin = spin; x.angle = start; }
            foreach (var x in new[] { blur, blurDeep }) { x.spin = spin * 1.6f; x.angle = start; }
            if (side < 0) foreach (var x in new[] { body0, edge, blur, blurDeep }) HuntMirror(x);
            if (g >= 2)
            {
                var outer = Attach(root, "War disc outer blur", HuntDiscBlurSet[0], width * 1.75f, Alpha(light, g >= 3 ? .12f : .2f), 122);
                outer.spin = spin * 1.25f; outer.angle = start + 60; if (side < 0) HuntMirror(outer); layers.Add(outer);
            }
            if (g >= 3)
            {
                // the awakened disc's gold edge: a gold copy of the glowing edges just outside the blades, turning with the metal;
                // a faint rune turns around it
                var gilt = Attach(root, "War disc awakened edge", HuntDiscSet[1], width * 1.1f, Alpha(HuntGold, .9f), 128); gilt.spin = spin; gilt.angle = start;
                if (side < 0) HuntMirror(gilt); layers.Add(gilt);
                var rune = Attach(root, "War disc awakened rune", runeRing, width * 1.65f, Alpha(HuntGold, .3f), 120); rune.spin = -spin * .2f; layers.Add(rune);
            }
            // the disc leaves from inside the thrower's chest: let it fade in over its first .06 s
            body0.fadeIn = .06f; foreach (var x in layers) x.fadeIn = .06f;
            var rig = new HuntDiscRig { root = root, grade = g };
            HuntDiscs.RemoveAll(x => !x.root); HuntDiscs.Add(rig);
            // The trail: two points of the rim trace a double cycloid behind the disc (a gold stroke over its deep copy and a
            // white-hot thread), painted crescents of the trailing rim peel off behind it and erode (tilted back and forth with
            // the spin), a soft glow lingers along the flight, echoes of the glowing edges flicker between the sweeps and needles
            // are thrown off the rim. Drawn at sixty steps a second.
            Vector2 drawn = at; float phase = start, rim = width * .4f, acc = 0; int beat = 0;
            HuntControl(halo, (owner, dt) =>
            {
                if (!root) return;
                acc += dt; if (acc < 1 / 60f) return;
                float span = acc; acc = 0;
                Vector2 now = root.position, step = now - drawn;
                if (owner.age < .05f || step.sqrMagnitude < 1e-6f) { drawn = now; return; }
                Vector2 travel = step.normalized; float before = phase; phase += -side * 1620 * span;
                for (int k = 0; k < 2; k++)
                {
                    Vector2 a0 = drawn + Direction(before + k * 180) * rim, a1 = now + Direction(phase + k * 180) * rim;
                    var trace = Line(a0, a1, (k == 0 ? .15f : .08f) * gs, k == 0 ? Alpha(light, .95f) : Alpha(HuntDiscHot, 1), .22f, k == 0 ? 121 : 122, 1.4f);
                    trace.hold = .15f; trace.fadePower = 1.2f; trace.axisEnd = new Vector2(1, .3f);
                    if (k == 0) { var deep = HuntOpaque(Line(a0, a1, .15f * gs, HuntDiscDeep, .2f, 115, 1.4f)); deep.fadePower = 1.2f; deep.axisEnd = new Vector2(1, .3f); }
                }
                beat++;
                if (beat % 3 == 0)
                {
                    var wake = Spawn("War disc glow wake", glow, now - travel * width * .2f, width * 1.4f, Alpha(light, .24f), .22f, true, 114);
                    wake.endScale = .45f; wake.ease = 1; wake.fadePower = 1.2f;
                }
                // fewer and softer sweeps once the level's aura rides on the disc too, so the stack never whites out
                int every = g >= 2 ? 4 : 3;
                if (beat % every == 0)
                {
                    float tilt = HuntAngle(-travel) + ((beat / every) % 2 == 0 ? 22 : -16) * side;
                    Vector2 c0 = now - travel * width * .12f;
                    var sweep = Spawn("War disc sweep", crescent, c0, width * 1.45f, Alpha(Color.Lerp(light, HuntDiscHot, .2f), g >= 3 ? .55f : g == 2 ? .7f : .9f), .18f, true, 121);
                    var sweepDeep = Spawn("War disc sweep depth", crescent, c0, width * 1.45f, HuntDiscDeep, .16f, false, 115);
                    foreach (var x in new[] { sweep, sweepDeep }) { HuntTurn(x, tilt); x.scale.y *= 1.1f; x.startScale = .8f; x.endScale = 1.08f; x.ease = 6; x.hold = .2f; x.fadePower = 1.15f; x.velocity = -travel * .6f; }
                }
                if (beat % 3 == 1 && beat % every != 0)
                {
                    // an echo of the glowing edges between the sweeps: a stroboscopic afterimage of light along the flight
                    var echo = Spawn("War disc light echo", HuntDiscSet[1], now, width, Alpha(light, .65f), .16f, true, 120);
                    echo.endScale = 1.12f; echo.ease = 1; echo.fadePower = 1.4f; if (side < 0) HuntMirror(echo); HuntTurn(echo, body0.angle);
                    float a = Range(0, 360);
                    Spark(now + Direction(a) * rim, Direction(a - side * 90) * Range(3, 6) + travel * Range(1, 3), Chance(.3f) ? Color.white : light, Range(.2f, .34f), Range(.16f, .26f), 4, 2.5f);
                }
                drawn = now;
            });
            Bundle(root, body0, layers.ToArray());
            HuntWatch(root, 6, end => HuntDiscCatch(end, light, gs));
        }

        // The disc caught (or gone): a small spark and shock front where it vanished, kept small since it is caught at the thrower's chest.
        private void HuntDiscCatch(Vector2 pos, Color light, float s)
        {
            HuntSpark(pos, 1.1f * s, Color.Lerp(light, HuntDiscHot, .5f), .12f, 132);
            ShockFlip(pos, 1.5f * s, Alpha(light, .85f), .22f, 130);
            Needles(pos, 5, 4 * s, light, .22f, .28f);
        }

        // Called for every enemy the disc passes through (it never stops): a sharp metallic cut.
        private void HitBoomerang(Vector2 pos, Color tint, bool signature, Vector2 direction)
        {
            HuntDiscTextures();
            var rig = HuntNearestDisc(pos, 1.6f);
            int keep = Grade; if (rig != null) Grade = rig.grade;
            int g = Grade; float gs = HuntScale(g);
            Color light = HuntTone(HuntDiscLight, tint); float a = HuntAngle(direction);
            Vector2 n = new Vector2(-direction.y, direction.x);
            // two discs crossing one body together: the second is a sharp echo, not another white-out
            bool echo = HuntRecent(HuntDiscHitMark, .07f, pos, .8f);
            if (!echo) Underlay(pos, 2f * gs, .22f, .24f);
            var star = HuntSpark(pos, (echo ? 1.1f : 1.7f) * gs, Color.Lerp(light, HuntDiscHot, .15f), .14f, 133);
            if (!echo) HuntDiscHitMark = star;
            // the rim's cut: a painted crescent bulging along the disc's travel, its white edge leading, over its deep copy
            // (it grows half as fast as the disc with the level: the awakened disc's own light already crowds the hit)
            float cut = a + Range(-20, 20), cs = 1 + (gs - 1) * .5f;
            var slash = PaintArc(pos, 2f * cs, cut, Alpha(light, .95f), .22f, 130); slash.scale.y *= 1.15f;
            var slashDeep = HuntOpaque(Crescent(pos, 2f * cs, HuntDiscDeep, .2f, cut, 128)); slashDeep.scale.y *= 1.15f;
            slashDeep.startScale = .82f; slashDeep.endScale = 1.06f; slashDeep.ease = 6; slashDeep.hold = .25f;
            Vector2 across = Direction(a + 90 + Range(-25, 25));
            var cutLine = Line(pos - across * .8f * gs, pos + across * .8f * gs, .09f, Color.white, .1f, 132); cutLine.startScale = .3f; cutLine.endScale = 1; cutLine.ease = 6;
            if (!echo)
            {
                // one shock front (the awakened disc's is gold), burst lines and a little warm smoke
                ShockFlip(pos, (g >= 3 ? 1.6f : 1.3f) * gs, Alpha(g >= 3 ? HuntGold : light, .9f), g >= 3 ? .26f : .22f, 131);
                ImpactLines(pos, 2f * gs, Alpha(light, .9f), .16f);
                HuntInk(pos, direction, .5f * gs, HuntDiscSmoke, 2, .42f);
            }
            // needles thrown on along the travel, a few flung sideways off the rim
            HuntNeedles(pos, direction, 38, echo ? 3 : 7, 9 * gs, light, .26f, .36f, 7);
            if (!echo) for (int i = 0; i < 2; i++) Spark(pos, (n * (i == 0 ? 1 : -1) * Range(3, 6) + direction * Range(1, 3)) * gs, HuntEmber, Range(.24f, .34f), Range(.16f, .26f), 7, 2.5f);
            Grade = keep;
        }

        // A ringing burst: a gold blast, shock fronts opening one after another to the burst's edge, the disc flung open spinning, embers.
        private void ImpactBoomerang(Vector2 pos, float radius, Color tint, int facing)
        {
            HuntDiscTextures();
            int g = Grade; float gs = HuntScale(g), r = Mathf.Clamp(radius, .5f, 3.5f), s = .7f + r * .35f;
            Color light = HuntTone(HuntDiscLight, tint);
            Underlay(pos, r * 2.8f, .22f, .4f);
            Blast(pos, 1.7f * s, light, .42f, 128);
            HuntSpark(pos, 2.2f * s, HuntDiscHot, .16f, 134);
            HuntInk(pos, Vector2.up, .7f * s, HuntDiscSmoke, 3, .5f);
            for (int i = 0; i < 3; i++)
            {
                var band = Wave(pos, r * 2.7f * (.6f + .2f * i), Alpha(i == 0 ? HuntDiscHot : light, .9f - .2f * i), .3f + .08f * i, 3); band.age = -.05f * i;
            }
            foreach (var x in HuntObject(null, HuntDiscSet, pos, Range(0, 360), 1.2f * gs, .32f, Alpha(HuntDiscHot, .9f), Color.clear, 131))
            { x.startScale = .8f; x.endScale = 1.8f; x.ease = 2; x.spin = -1400; x.fadePower = 1.4f; }
            ImpactLines(pos, r * 2.4f, Alpha(light, .8f), .22f);
            EmberBurst(pos, 8, 6 * s, HuntEmber, .5f);
            for (int i = 0, n = Budget(10); i < n; i++)
                Spark(pos, Direction(Range(0, 360)) * Range(4, 9) * s, i % 3 == 0 ? Color.white : i % 3 == 1 ? HuntEmber : light, Range(.22f, .36f), Range(.2f, .34f), 6, 2.4f);
            if (g >= 3) { var rune = RuneCircle(pos, r * 2.1f, Alpha(HuntGold, .85f), .6f, -120, false, 126); rune.startScale = .5f; }
        }

        // ---------------------------------------------------------------- Ricochet: the bouncing hot slug

        private void CastRicochet(CastInfo c)
        {
            HuntSlugTextures();
            float w = c.windup; int f = c.facing, g = Grade; float gs = HuntScale(g);
            Color light = HuntTone(HuntSlugLight, c.tint);
            // the slug heats at the height the shot leaves from (the small hero's eye line), held out a step ahead of the face
            Vector2 hand = c.pos + new Vector2(f * .95f, -.05f), dir = HuntAim(c.target - hand, new Vector2(f, 0)); float a = HuntAngle(dir);
            HuntExpect(SkillEffect.Ricochet, c, hand, light, w < .2f ? .75f * gs : gs, g);
            if (w < .2f) return;
            // a lock glint on the prey (it stays with the prey, not with the shooter)
            var lockOn = Glint(c.target, 1f * gs, Alpha(HuntSlugHot, .9f), w * .5f, 45); lockOn.age = -w * .5f;
            int mark = clips.Count;
            Underlay(hand, 2.2f * gs, .28f, w + .08f).fadeIn = .1f;
            // the heat glow sits ahead of the hand, so the shooter's body never whites out
            Vector2 glowAt = hand + new Vector2(f * .25f, 0);
            var heat = Anchored(c.anchor, glowAt, "Slug heat", glow, 1.4f * gs, Alpha(light, .42f), w + .04f, 126);
            heat.startScale = .15f; heat.endScale = 1; heat.ease = 1.4f; heat.pulse = .1f; heat.hold = .85f; heat.fadePower = 2;
            // the slug itself: a bolt of painted light heating from gold to a white-hot head, over its deep copy
            foreach (var x in HuntGlyph(c.anchor, HuntSlugSet, hand, a, 1f * gs, w + .03f, Alpha(HuntSlugBolt, 1), HuntSlugDeep, 0, 128))
            { x.fadeIn = w * .4f; x.startScale = .3f; x.endScale = 1; x.ease = 2; x.hold = .9f; x.fadePower = 2; }
            var star = Anchored(c.anchor, hand + new Vector2(f * .2f, 0), "Slug star", glint, .85f * gs, Alpha(HuntSlugHot, .95f), w + .04f, 129);
            star.spin = 260; star.startScale = .1f; star.endScale = 1; star.ease = 1.4f; star.hold = .85f; star.fadePower = 2;
            Converge(hand, 8, 1.6f * gs, light, w, .6f);
            // accelerator rings standing across the aim, snapping open one after another: painted shock rings (the awakened
            // slug's are gold rune rings), each over its deep copy
            int rings = g >= 2 ? 3 : 2;
            for (int i = 0; i < rings; i++)
            {
                Vector2 at = hand + dir * (.55f + i * .45f);
                float width = (1.5f - i * .25f) * gs, delay = i * w * .18f, span = Mathf.Max(.1f, w - delay + .05f);
                Sprite face = g >= 3 ? runeRing : ring;
                var deep = Spawn("Slug accelerator depth", face, at, width, HuntSlugDeep, span, false, 117);
                var lit = Spawn("Slug accelerator", face, at, width, Alpha(g >= 3 ? HuntGold : light, .95f), span, true, 127);
                foreach (var x in new[] { deep, lit }) { x.scale.x *= .36f; x.root.transform.localScale = x.scale; HuntTurn(x, a); x.age = -delay; x.fadeIn = .03f; x.startScale = .45f; x.endScale = 1; x.ease = 7; x.hold = .85f; x.fadePower = 2; }
            }
            // a gold sight line to the prey over its deep copy, re-drawn from the hand as the shooter moves
            var sight = Line(hand, c.target, .06f, Alpha(light, .7f), w, 126); sight.fadeIn = w * .6f; sight.flicker = .35f; sight.hold = .85f; sight.fadePower = 2;
            var sightDeep = HuntOpaque(Line(hand, c.target, .06f, HuntSlugDeep, w, 116)); sightDeep.fadeIn = w * .6f; sightDeep.hold = .85f; sightDeep.fadePower = 2;
            if (g >= 3) { var crown = RuneCircle(hand + new Vector2(f * .3f, 0), 1.7f * gs, Alpha(HuntGold, .7f), w + .05f, -f * 200, false, 28); crown.startScale = .3f; }
            HuntFollow(c.anchor, mark, HuntSightOf(c.anchor, sight, hand, c.target), HuntSightOf(c.anchor, sightDeep, hand, c.target));
        }

        /// <summary>The shot: a hit spark at the muzzle, a long tongue of flame along the aim in its glow, sonic shock fronts, burst
        /// lines stretched along the aim and a tight cone of needles.</summary>
        private void HuntMuzzle(Vector2 pos, Vector2 dir, Color light, float s, int g)
        {
            float a = HuntAngle(dir);
            HuntSpark(pos, 1.7f * s, HuntSlugBolt, .12f, 132);
            var tongue = Line(pos, pos + dir * 1.6f * s, .32f * s, Alpha(light, .95f), .11f, 131); tongue.startScale = .5f; tongue.endScale = 1; tongue.ease = 7; tongue.hold = .2f; tongue.fadePower = 1.2f;
            HuntGlowLine(pos, pos + dir * 1.6f * s, .9f * s, Alpha(light, .5f), .16f, 128);
            HuntBoom(pos + dir * .2f, dir, 1.4f * s, Alpha(HuntSlugHot, .9f), .2f, 8);
            HuntBoom(pos + dir * .6f, dir, 1.05f * s, Alpha(light, .85f), .2f, 13).age = -.03f;
            HuntBoom(pos + dir * .2f, dir, 1.4f * s, HuntSlugDeep, .18f, 8, false, 127);
            var lines = ImpactLines(pos + dir * .3f, 1.9f * s, Alpha(light, .8f), .14f); lines.angle = a; lines.scale.y *= .55f;
            HuntNeedles(pos, dir, 22, 6, 12 * s, light, .18f, .32f, 2);
            if (g >= 3) ShockFlip(pos, 1.7f * s, Alpha(HuntGold, .9f), .24f, 131);
        }

        private void ProjectileRicochet(Transform root, Color tint, bool signature)
        {
            HuntSlugTextures();
            int g = signature ? Grade : 1; float gs = HuntScale(g);
            Color light = HuntTone(HuntSlugLight, tint);
            Vector2 at = root.position, dir = HuntAim(root.right, Vector2.right);
            if (signature)
            {
                Vector2 hand, body; var booked = HuntClaim(SkillEffect.Ricochet, at, out hand, out body);
                if (booked != null) HuntMuzzle(hand + dir * .3f, dir, booked.light, booked.s, booked.g);
            }
            float len = 1.05f * gs;
            // the slug and its wake ride the root, which gameplay turns at every bounce: a bolt of painted light over its deep copy,
            // a glow round its head and a spinning star at its tip
            var bolt = Attach(root, "Hot slug", HuntSlugSet[0], len, Alpha(HuntSlugBolt, 1), 126);
            var depth = Attach(root, "Hot slug depth", HuntSlugSet[1], len, HuntSlugDeep, 119, false);
            float haloAlpha = g >= 2 ? .36f : .48f;
            var halo = Attach(root, "Hot slug glow", glow, 1.5f * gs, Alpha(light, haloAlpha), 118, true, new Vector2(len * .15f, 0)); halo.pulse = .12f;
            var star = Attach(root, "Hot slug star", glint, .95f * gs, Alpha(HuntSlugHot, .85f), 127, true, new Vector2(len * .28f, 0)); star.spin = 700;
            var rig = new HuntSlugRig { root = root, grade = g, dir = dir, last = at, flownFrom = at, halo = halo, star = star, haloAlpha = haloAlpha };
            // the wake: a needle of white-hot light inside a gold spindle over its deep copy, all in a long soft glow
            HuntSlugTrail(root, rig, "Slug streak", streak, 2f * gs, .17f, Alpha(HuntSlugHot, 1), 124, true, .05f * gs);
            HuntSlugTrail(root, rig, "Slug streak sheath", streak, 2.3f * gs, .42f, Alpha(light, .95f), 121, true, 0);
            HuntSlugTrail(root, rig, "Slug streak depth", streak, 2.3f * gs, .42f, HuntSlugDeep, 116, false, 0);
            HuntSlugTrail(root, rig, "Slug streak glow", glow, 2.6f * gs, .22f, Alpha(light, .32f), 115, true, 0);
            var layers = new List<Clip> { depth, halo, star };
            foreach (var t in rig.trails) layers.Add(t.clip);
            if (g >= 3)
            {
                // the awakened slug's gold crown, lit brighter by every bounce
                rig.crown = Attach(root, "Hot slug gold ring", ring, 1.1f * gs, Alpha(HuntGold, .3f), 125); rig.crown.pulse = .08f; layers.Add(rig.crown);
                rig.crownScale = rig.crown.scale;
            }
            // the slug leaves from inside the shooter's chest: let it fade in over its first .05 s
            bolt.fadeIn = .05f; foreach (var x in layers) x.fadeIn = .05f;
            rig.haloScale = halo.scale; rig.starScale = star.scale;
            HuntSlugGrow(rig, 0);
            HuntSlugs.RemoveAll(x => !x.root); HuntSlugs.Add(rig);
            // Every frame the streak is sized and the heading kept (a bounce reads it); at sixty steps a second a tracer is drawn
            // along the path (so every bounce leaves a hard angle) and needle sparks are shed behind.
            Vector2 drawn = at; float acc = 0; int beat = 0;
            HuntControl(halo, (owner, dt) =>
            {
                if (!root) return;
                Vector2 now = root.position, step = now - rig.last;
                HuntSlugGrow(rig, (now - rig.flownFrom).magnitude);
                if (step.sqrMagnitude > 1e-6f) rig.dir = step.normalized;
                rig.last = now;
                acc += dt; if (acc < 1 / 60f) return;
                acc = 0;
                if (owner.age > .04f && (now - drawn).sqrMagnitude > 1e-5f)
                {
                    float esc = Mathf.Min(1.35f, 1 + (g >= 3 ? .2f : .06f) * rig.bounces);
                    var trace = Line(drawn, now, .07f * gs * esc, Alpha(HuntSlugHot, 1), .15f, 123, 1.2f); trace.hold = .2f; trace.fadePower = 1.2f; trace.axisEnd = new Vector2(1, .3f);
                    var traceLit = Line(drawn, now, .17f * gs * esc, Alpha(light, .75f), .17f, 120, 1.2f); traceLit.fadePower = 1.1f; traceLit.axisEnd = new Vector2(1, .3f);
                }
                drawn = now;
                if (++beat % 3 == 0 && owner.age > .04f)
                {
                    Vector2 across = new Vector2(-rig.dir.y, rig.dir.x) * (beat % 2 == 0 ? 1 : -1);
                    Spark(now - rig.dir * Range(.3f, .8f), across * Range(1.5f, 3.5f) - rig.dir * Range(1, 3), beat % 2 == 0 ? Color.white : light, Range(.18f, .28f) * gs, Range(.14f, .22f), 3, 3);
                }
            });
            Bundle(root, bolt, layers.ToArray());
            // The shot ends on its last target (just struck) or runs out of flight. Only now is it known which hit was the last,
            // since a bounce to a target straight ahead looks like a hit that goes on.
            HuntWatch(root, 4, last =>
            {
                if (HuntRecent(rig.hitFlash, .12f, rig.hitAt, .2f)) { HuntSlugFinal(rig, light); return; }
                HuntSpark(last, .9f * gs, HuntSlugBolt, .11f, 131);
                HuntNeedles(last, rig.dir, 60, 4, 4, light, .2f, .24f, 4);
            });
        }

        /// <summary>How far a slug's heat has climbed (0 cold, 1 after about six awakened bounces): the part of the escalation that is
        /// shown as whiter light, more and faster sparks and a longer exit streak instead of a bigger burst, since every hit is single-target.</summary>
        private static float HuntSlugHeat(float esc) { return Mathf.Clamp01((esc - 1) / 1.2f); }

        // The last target takes the slug: a fuller burst on top of the hit's ping (a blast driven on through the body), hotter
        // after every bounce but no wider than a hit.
        private void HuntSlugFinal(HuntSlugRig rig, Color light)
        {
            int keep = Grade; Grade = rig.grade;
            float gs = HuntScale(rig.grade), s = gs * Mathf.Min(rig.esc, 1.35f), heat = HuntSlugHeat(rig.esc); Vector2 pos = rig.hitAt;
            Color hotLight = Color.Lerp(light, HuntSlugHot, heat * .7f);
            Vector2 on = HuntAim(rig.hitDir, Vector2.right); float onA = HuntAngle(on);
            HuntSpark(pos, 2f * s, Color.Lerp(HuntSlugBolt, HuntSlugHot, heat * .6f), .16f, 133);
            Blast(pos + on * .2f * s, 1.25f * s * (1 + .2f * heat), Color.Lerp(HuntSlugFire, HuntSlugBolt, heat * .6f), .34f, 129);
            var wedges = ImpactLines(pos + on * .25f * s, 2.4f * s, Alpha(hotLight, .95f), .2f); wedges.angle = onA; wedges.scale.y *= .55f;
            HuntBoom(pos + on * .3f, on, 1.7f * s, Alpha(hotLight, .9f), .22f, 7);
            HuntBoom(pos + on * .3f, on, 1.7f * s, HuntSlugDeep, .2f, 7, false, 127);
            HuntInk(pos, on, .55f * s, HuntSlugSmoke, 2, .45f);
            float reach = 1.6f * gs * (1 + heat);
            var through = Line(pos, pos + rig.hitDir * reach, .12f * s, Alpha(HuntSlugHot, .9f), .14f + .06f * heat, 130); through.startScale = .3f; through.endScale = 1; through.ease = 4; through.velocity = rig.hitDir * 4;
            var throughDeep = HuntOpaque(Line(pos, pos + rig.hitDir * reach, .12f * s, HuntSlugDeep, .16f + .06f * heat, 117)); throughDeep.startScale = .3f; throughDeep.endScale = 1; throughDeep.ease = 4; throughDeep.velocity = rig.hitDir * 4;
            EmberBurst(pos, 5 + Mathf.Min(rig.bounces, 6), 4 * s * (1 + .3f * heat), hotLight, .6f, 4);
            if (rig.grade >= 3) Glint(pos, 1.5f * s, Alpha(Color.Lerp(HuntGold, Color.white, heat * .5f), .6f + .35f * heat), .22f, 45);
            Grade = keep;
        }

        /// <summary>A wake layer behind the slug (front <paramref name="front"/> units behind its centre), grown by <see cref="HuntSlugGrow"/>.</summary>
        private void HuntSlugTrail(Transform root, HuntSlugRig rig, string name, Sprite sprite, float length, float thickness, Color color, int order, bool additive, float front)
        {
            var clip = Attach(root, name, sprite, length, color, order, additive, new Vector2(-front - length * .5f, 0));
            clip.scale.y *= thickness;
            rig.trails.Add(new HuntTrailLayer { clip = clip, baseX = clip.scale.x, baseY = clip.scale.y, front = -front, length = length });
        }

        /// <summary>Sizes the slug's streaks to the distance flown since launch or the last bounce, thickened (a little) by its heat.</summary>
        private static void HuntSlugGrow(HuntSlugRig rig, float flown)
        {
            float thick = Mathf.Min(rig.esc, 1.35f), reach = 1 + (Mathf.Min(rig.esc, 2.2f) - 1) * .5f;
            foreach (var t in rig.trails)
            {
                float visible = Mathf.Clamp(flown + .3f, .25f, t.length * reach);
                t.clip.scale = new Vector3(t.baseX * visible / t.length, t.baseY * thick, 1); t.clip.offset = new Vector2(t.front - visible * .5f, 0);
                if (t.clip.root) { t.clip.root.transform.localScale = t.clip.scale; t.clip.root.transform.localPosition = t.clip.offset; }
            }
        }

        /// <summary>Each bounce makes a slug hotter (strongly at level three, where damage grows too): its halo and star grow a little,
        /// and the rest of the climb shows as whiter light, a faster star and a brighter gold crown.</summary>
        private void HuntEscalate(HuntSlugRig rig, Color light)
        {
            float step = rig.grade >= 3 ? .22f : rig.grade == 2 ? .1f : .05f, grow = 1 + step * rig.bounces, heat = HuntSlugHeat(grow);
            rig.esc = grow;
            if (rig.halo != null) { rig.halo.scale = rig.haloScale * Mathf.Min(grow, 1.25f); rig.halo.color = Alpha(Color.Lerp(light, HuntSlugHot, Mathf.Min(1, .15f + heat)), rig.haloAlpha + Mathf.Min(.15f, .04f * rig.bounces)); }
            if (rig.star != null) { rig.star.scale = rig.starScale * Mathf.Min(grow, 1.5f); rig.star.spin = 700 + 160 * rig.bounces; }
            if (rig.crown != null) { rig.crown.scale = rig.crownScale * Mathf.Min(1 + .1f * rig.bounces, 1.4f); rig.crown.color = Alpha(Color.Lerp(HuntGold, Color.white, heat * .5f), Mathf.Min(.95f, .35f + .2f * rig.bounces)); }
        }

        // Every bounce point and the final hit: a hit spark, a small gold blast and a spark fan thrown off the struck face.
        private void HitRicochet(Vector2 pos, Color tint, bool signature, Vector2 direction)
        {
            HuntSlugTextures();
            var rig = HuntNearestSlug(pos, 1.4f);
            int keep = Grade; if (rig != null) Grade = rig.grade;
            int g = Grade, k = rig != null ? rig.bounces++ : 0;
            float esc = 1 + (g >= 3 ? .2f : g == 2 ? .1f : .05f) * k, gs = HuntScale(g), s = gs * Mathf.Min(esc, 1.35f), heat = HuntSlugHeat(esc);
            Color light = Color.Lerp(HuntTone(HuntSlugLight, tint), HuntSlugHot, heat * .7f);
            Vector2 incoming = rig != null && rig.dir.sqrMagnitude > .5f ? rig.dir : direction;
            bool bounce = Vector2.Angle(incoming, direction) > 12;
            Vector2 away = bounce ? direction - incoming : -direction;
            away = away.sqrMagnitude > .01f ? away.normalized : -direction;
            float aa = HuntAngle(away);
            Underlay(pos, 1.7f * s, .2f, .22f);
            // the spark and a small blast at the struck point, wedge lines snapping out round it and a little smoke knocked off the face
            var flash = HuntSpark(pos, 1.55f * s, Color.Lerp(HuntSlugBolt, HuntSlugHot, heat * .6f), .12f, 133);
            HuntSlugHitMark = flash;
            Blast(pos, .9f * s, Color.Lerp(HuntSlugFire, HuntSlugBolt, heat * .6f), .26f, 128);
            var wedges = ImpactLines(pos, 1.8f * s, Alpha(light, .9f), .14f); wedges.angle = aa; wedges.scale.y *= .7f;
            HuntInk(pos, away, .42f * s, HuntSlugSmoke, 1, .34f);
            if (bounce)
            {
                // the deflection drawn as a hard chevron: the incoming streak meets the point, a bright scar marks the struck face
                var inLine = Line(pos - incoming * 1.2f * s, pos, .09f * s, Alpha(HuntSlugHot, 1), .12f, 130); inLine.axisEnd = new Vector2(1, .2f); inLine.fadePower = 1.3f;
                var inDeep = HuntOpaque(Line(pos - incoming * 1.2f * s, pos, .09f * s, HuntSlugDeep, .13f, 117)); inDeep.axisEnd = new Vector2(1, .2f);
                Vector2 faceDir = new Vector2(-away.y, away.x);
                var face = Line(pos - faceDir * .5f * s, pos + faceDir * .5f * s, .08f, Alpha(HuntSlugHot, 1), .1f, 131); face.startScale = .3f; face.endScale = 1; face.ease = 6;
            }
            ShockFlip(pos, 1.25f * s, Alpha(light, .85f), .2f, 130);
            // the heat shows in the spark fan: more of them, flying faster, whiter
            for (int i = 0, n = Budget(8 + 2 * Mathf.Min(k, 5)); i < n; i++)
            {
                bool through = i % 5 == 4;
                Vector2 v = Direction((through ? HuntAngle(direction) : aa) + Range(-50, 50)) * Range(5, 11) * gs * (1 + .5f * heat);
                Spark(pos, v, i % 3 == 0 ? Color.white : i % 3 == 1 ? HuntSlugHot : light, Range(.2f, .34f) * gs, Range(.14f, .28f), 8, 2.4f);
            }
            if (rig != null) { rig.hitAt = pos; rig.hitDir = direction; rig.hitFlash = flash; }
            else
            {
                // no slug to follow (it was never drawn): treat the hit as the last
                ImpactLines(pos, 1.9f * s, Alpha(light, .85f), .2f);
                Wave(pos, 1.8f * s, Alpha(light, .9f), .26f, 3);
            }
            if (g >= 3)
            {
                Color gold = Color.Lerp(HuntGold, Color.white, heat * .5f);
                ShockFlip(pos, 1.5f * s, Alpha(gold, .85f), .24f, 131);
                var rune = RuneCircle(pos, 1.3f * s, Alpha(gold, .6f + .3f * heat), .32f, 260 + 120 * k, false, 126); rune.startScale = .4f;
            }
            if (rig != null) { if (bounce) rig.flownFrom = pos; HuntEscalate(rig, HuntTone(HuntSlugLight, tint)); }
            Grade = keep;
        }

        // A burst of hot slugs bouncing off in all directions (rarely used).
        private void ImpactRicochet(Vector2 pos, float radius, Color tint, int facing)
        {
            HuntSlugTextures();
            int g = Grade; float gs = HuntScale(g), r = Mathf.Clamp(radius, .5f, 3.5f);
            Color light = HuntTone(HuntSlugLight, tint);
            Underlay(pos, r * 2.6f, .2f, .3f);
            HuntSpark(pos, 1.9f * gs, HuntSlugBolt, .14f, 133);
            Blast(pos, 1.3f * gs, HuntSlugFire, .36f, 128);
            HuntInk(pos, Vector2.up, .6f * gs, HuntSlugSmoke, 3, .4f);
            Wave(pos, r * 2.7f, Alpha(light, .9f), .3f, 3);
            ImpactLines(pos, r * 2.4f, Alpha(light, .8f), .2f);
            for (int i = 0, n = Budget(5); i < n; i++)
            {
                // a zigzag path: out, a hard turn, out again, inside the burst radius
                Vector2 d1 = Direction(i * 72 + Range(-15, 15)), mid = pos + d1 * r * Range(.4f, .6f), d2 = Direction(HuntAngle(d1) + (i % 2 == 0 ? 55 : -55)), end = mid + d2 * r * .4f;
                var l1 = Line(pos, mid, .07f * gs, Alpha(HuntSlugHot, .95f), .16f, 128); l1.fadePower = 1.5f;
                var l2 = Line(mid, end, .07f * gs, Alpha(HuntSlugHot, .95f), .16f, 128); l2.fadePower = 1.5f; l2.age = -.04f;
                var t1 = HuntOpaque(Line(pos, mid, .1f * gs, HuntSlugDeep, .18f, 116)); t1.fadePower = 1.3f;
                var t2 = HuntOpaque(Line(mid, end, .1f * gs, HuntSlugDeep, .18f, 116)); t2.fadePower = 1.3f; t2.age = -.04f;
                Glint(mid, .8f * gs, Alpha(HuntSlugHot, .9f), .12f, 45).age = -.03f;
            }
            if (g >= 3) { var rune = RuneCircle(pos, r * 2.1f, Alpha(HuntGold, .8f), .5f, 200, false, 126); rune.startScale = .5f; }
        }

        // The slug changing course: a quick zip of light drawn ahead of it to the next target, and a lock tick on that target.
        private void TrailRicochet(Vector2 from, Vector2 to, Color tint)
        {
            HuntSlugTextures();
            Vector2 delta = to - from; float length = delta.magnitude; if (length < .05f) return;
            var rig = HuntNearestSlug(from, 1.4f);
            int g = rig != null ? rig.grade : Grade, k = rig != null ? rig.bounces + 1 : 1;
            float esc = 1 + (g >= 3 ? .2f : g == 2 ? .1f : .05f) * k, gs = HuntScale(g) * Mathf.Min(esc, 1.35f);
            Color light = Color.Lerp(HuntTone(HuntSlugLight, tint), HuntSlugHot, HuntSlugHeat(esc) * .7f);
            Vector2 dir = delta / length; const float zip = .06f;
            int n = Mathf.Clamp(Mathf.CeilToInt(length / .7f), 2, 10);
            for (int i = 0; i < n; i++)
            {
                Vector2 a0 = from + delta * (i / (float)n), a1 = from + delta * ((i + 1) / (float)n);
                float delay = zip * i / n;
                var hot = Line(a0, a1, .06f * gs, Alpha(g >= 3 ? HuntGold : HuntSlugHot, 1), .16f, 127, 1.12f); hot.age = -delay; hot.hold = .2f; hot.fadePower = 1.3f;
                var lit = Line(a0, a1, .15f * gs, Alpha(light, .9f), .18f, 122, 1.12f); lit.age = -delay; lit.hold = .15f; lit.fadePower = 1.3f;
                var deep = HuntOpaque(Line(a0, a1, .15f * gs, HuntSlugDeep, .2f, 116, 1.12f)); deep.age = -delay; deep.fadePower = 1.2f;
            }
            Vector2 across = new Vector2(-dir.y, dir.x);
            for (int i = 0, m = Budget(2); i < m; i++)
            {
                Vector2 s0 = from + across * (i % 2 == 0 ? .18f : -.18f) + dir * Range(0, length * .3f);
                var whoosh = Line(s0, s0 + dir * Range(.7f, 1.2f), .035f, new Color(1, 1, 1, .7f), .12f, 126); whoosh.velocity = dir * 15; whoosh.fadePower = 1.5f;
            }
            Glint(to, .95f * gs, Alpha(HuntSlugHot, .95f), .14f, 45).age = -zip;
            var tick = Spawn("Ricochet lock ring", ring, to, .9f * gs, Alpha(light, .8f), .16f, true, 129); tick.age = -zip; tick.startScale = 1.4f; tick.endScale = .7f; tick.ease = 3; tick.fadePower = 1.3f;
        }

        // ---------------------------------------------------------------- HuntMark: the hunter's mark

        private void CastHuntMark(CastInfo c)
        {
            HuntMarkTextures();
            float w = c.windup; int f = c.facing, g = Grade; float gs = HuntScale(g);
            Color light = HuntTone(HuntMarkLight, c.tint);
            // the sigil is drawn a full step ahead of the hand, clear of the hunter's face and chest
            Vector2 hand = c.pos + new Vector2(f * .5f, .15f), dir = HuntAim(c.target - hand, new Vector2(f, 0)), at = hand + dir * .95f, feet = c.pos + Vector2.down;
            HuntExpect(SkillEffect.HuntMark, c, at, light, w < .2f ? .75f * gs : gs, g);
            if (w < .2f) return;
            // on the prey (it stays with the prey): a faint reticle of crimson light settling at the height the mark will strike
            Vector2 prey = c.target + Vector2.up * .2f;
            foreach (var x in HuntGlyph(null, HuntReticleSet, prey, 45, 1.9f, w + .02f, Alpha(light, .7f), Alpha(HuntMarkDeep, .3f), .18f, 127))
            { x.spin = 90; x.fadeIn = w * .4f; x.startScale = 1.45f; x.endScale = 1; x.ease = 1.5f; x.hold = .75f; x.fadePower = 2; }
            int mark = clips.Count;
            Underlay(at, 2.3f * gs, .22f, w + .1f).fadeIn = .1f;
            // the sigil the hunter draws: the brand laid on in a stroke before the hand, glowing crimson with a white-hot spine over
            // its deep red copy, turning and closing on the aim
            foreach (var x in HuntGlyph(c.anchor, HuntBrandSet, at, 0, 1.75f * gs, w + .05f, Alpha(light, .9f), HuntMarkDeep, .45f, 128))
            { x.spin = -f * 110; x.fadeIn = .05f; x.startScale = 1.35f; x.endScale = .9f; x.ease = HuntPop(w + .05f, .12f); x.hold = .86f; x.fadePower = 2; if (f < 0) HuntMirror(x); }
            // a rune ring turning round the brand from level two (the awakened one in gold); kept to one ring so the brand stays
            // the shape that reads
            if (g >= 2)
            {
                var seal = Anchored(c.anchor, at, g >= 3 ? "Mark awakened rune" : "Mark sigil ring", runeRing, 2.1f * gs, Alpha(g >= 3 ? HuntGold : light, .75f), w + .05f, 125);
                seal.spin = f * 90; seal.fadeIn = .1f; seal.startScale = .55f; seal.endScale = 1; seal.ease = 3; seal.hold = .85f; seal.fadePower = 2;
            }
            Converge(at, 9, 1.8f * gs, light, w, .45f);
            // the sight: a thin crimson line drawn to the prey over its deep copy, re-drawn from the sigil as the hunter moves
            var sight = Line(at, prey, .06f, Alpha(light, .75f), w, 126); sight.fadeIn = w * .5f; sight.flicker = .35f; sight.hold = .85f; sight.fadePower = 2;
            var sightDeep = HuntOpaque(Line(at, prey, .06f, HuntMarkDeep, w, 116)); sightDeep.fadeIn = w * .5f; sightDeep.hold = .85f; sightDeep.fadePower = 2;
            GroundGlow(feet, 3f * gs, Alpha(light, .22f), w + .3f);
            HuntFollow(c.anchor, mark, HuntSightOf(c.anchor, sight, at, prey), HuntSightOf(c.anchor, sightDeep, at, prey));
        }

        /// <summary>The sigil loosing the mark: a crimson hit spark, a shock front, burst lines and needles along the shot.</summary>
        private void HuntMarkFlash(Vector2 pos, Color light, float s, float angle, int g)
        {
            HuntSpark(pos, 1.8f * s, Color.Lerp(light, HuntMarkHot, .4f), .13f, 132);
            Wave(pos, 1.8f * s, Alpha(light, .9f), .22f, 4);
            ImpactLines(pos, 2f * s, Alpha(light, .8f), .16f);
            HuntNeedles(pos, Direction(angle), 28, 5, 8 * s, light, .2f, .32f, 2);
            if (g >= 3) ShockFlip(pos, 2.2f * s, Alpha(HuntGold, .85f), .26f, 131);
        }

        // A thin arrow of red light that lands at once: a bright shaft in its glow over a deep red copy, thinning away, with a chevron
        // head driven in. A cast's arrows leave from the sigil the hunter drew (the first of them looses it); a mark passed on leaves
        // from its old prey.
        private void TrailHuntMark(Vector2 from, Vector2 to, Color tint)
        {
            HuntMarkTextures();
            int g = Grade; float gs = HuntScale(g); Color light = HuntTone(HuntMarkLight, tint);
            Vector2 start = from;
            if (HuntMarkFrame == Time.frameCount && (from - HuntMarkOrigin).sqrMagnitude < 1e-4f) start = HuntMarkFrom;
            else
            {
                Vector2 hand, body; var booked = HuntClaim(SkillEffect.HuntMark, from, out hand, out body);
                if (booked != null)
                {
                    start = hand;
                    HuntMarkFlash(hand, booked.light, booked.s, HuntAngle(HuntAim(to - hand, Vector2.right)), booked.g);
                }
                HuntMarkFrame = Time.frameCount; HuntMarkOrigin = from; HuntMarkFrom = start;
            }
            // prey closer than the sigil: the arrow leaves from the hand instead of flying back
            if (Vector2.Dot(to - start, to - from) <= 0) start = from;
            Vector2 delta = to - start; float length = delta.magnitude; if (length < .05f) return;
            Vector2 dir = delta / length; float a = HuntAngle(dir);
            HuntGlowLine(start, to, .55f * gs, Alpha(light, .4f), .24f, 115);
            var deep = HuntOpaque(Line(start, to, .22f * gs, HuntMarkDeep, .26f, 117)); deep.axisEnd = new Vector2(1, .15f); deep.ease = 2;
            var lit = Line(start, to, .22f * gs, Alpha(light, .95f), .22f, 125, 1.02f); lit.axisEnd = new Vector2(1, .15f); lit.ease = 2; lit.hold = .2f; lit.fadePower = 1.1f;
            var hot = Line(start, to, .06f * gs, Alpha(g >= 3 ? HuntGold : HuntMarkHot, 1), .18f, 128, 1.01f); hot.axisEnd = new Vector2(1, .3f); hot.ease = 2;
            foreach (var x in HuntGlyph(null, HuntChevronSet, to - dir * .12f, a, .62f * gs, .3f, Alpha(light, 1), HuntMarkDeep, .3f, 130))
            { x.velocity = dir * 2.5f; x.drag = 9; x.hold = .4f; x.fadePower = 1.5f; x.startScale = 1.3f; x.endScale = 1; x.ease = 4; }
            for (int i = 0, n = Budget(3); i < n; i++)
            {
                Vector2 s0 = start + delta * Range(.1f, .6f) + new Vector2(-dir.y, dir.x) * Range(-.12f, .12f);
                var race = Line(s0, s0 + dir * Range(.6f, 1.1f), .04f, i % 2 == 0 ? new Color(1, 1, 1, .8f) : Alpha(light, .85f), .12f, 126); race.velocity = dir * 16; race.fadePower = 1.5f;
            }
            HuntSpark(start, .9f * gs, Color.Lerp(light, HuntMarkHot, .4f), .1f, 131);
        }

        // The reticle snapping shut on the prey (the lasting mark over its head is drawn by the status effects).
        private void ImpactHuntMark(Vector2 pos, float radius, Color tint, int facing)
        {
            HuntMarkTextures();
            int g = Grade; float gs = HuntScale(g), r = Mathf.Clamp(radius, .5f, 2f);
            Color light = HuntTone(HuntMarkLight, tint);
            // the reticle only drops and locks; the brand it leaves holds on after it
            float width = r * 2.1f, life = .42f;
            const float snap = .11f;
            Underlay(pos, r * 2.8f, .26f, .55f);
            // the reticle of crimson light drops onto the prey and snaps shut, turning a quarter as it locks
            var lockOn = HuntGlyph(null, HuntReticleSet, pos, 45, width, life, Alpha(light, .85f), HuntMarkDeep, .3f, 131);
            if (g >= 3) lockOn.Add(HuntTurn(Spawn("Mark awakened edge", HuntReticleSet[0], pos, width * 1.05f, Alpha(HuntGold, .5f), life, true, 132), 45));
            float pop = HuntPop(life, snap);
            foreach (var x in lockOn) { x.startScale = 1.6f; x.endScale = 1; x.ease = pop; x.fadeIn = .03f; x.hold = .5f; x.fadePower = 1.3f; }
            HuntControl(lockOn[0], (owner, dt) =>
            {
                float turn = 45 * Sq(Mathf.Max(0, 1 - owner.age / snap));
                foreach (var x in lockOn) HuntTurn(x, turn);
            });
            // four jaws close in on the diagonals and bite at the ring
            for (int k = 0; k < 4; k++)
            {
                Vector2 dk = Direction(k * 90 + 45);
                float outer = r * 1.9f, inner = r * .95f;
                foreach (var x in HuntGlyph(null, HuntChevronSet, pos + dk * outer, k * 90 + 225, .46f * r * Mathf.Sqrt(gs), life * .8f, Alpha(light, 1), HuntMarkDeep, 0, 134))
                { x.velocity = -dk * (outer - inner) * 18; x.drag = 18; x.hold = .4f; x.fadePower = 1.5f; x.fadeIn = .02f; }
            }
            // the lock as the jaws meet: a crimson hit spark and a blast, one long painted cut, the brand burned onto the prey (it
            // pops, holds, then breaks up), wedge lines, needles thrown mostly away from the hunter and smoke kicked out behind
            int lockFrom = clips.Count;
            HuntSpark(pos, 2.4f * r * gs, Color.Lerp(light, HuntMarkHot, .35f), .16f, 136);
            Blast(pos, 1.5f * r * gs, light, .4f, 124);
            // the strike: one long painted cut slanting down across the prey, white down its spine, over its deep red copy (which
            // lies over the reticle, so the cut reads across the glyph instead of melting into it)
            int f = facing < 0 ? -1 : 1; Vector2 cut = Direction(f > 0 ? -36 : 216) * width * .95f;
            var slash = Line(pos - cut, pos + cut, .3f * r * gs, Alpha(light, 1), .26f, 134);
            var slashCore = Line(pos - cut * .9f, pos + cut * .9f, .09f * r * gs, Color.white, .2f, 135);
            var slashDeep = HuntOpaque(Line(pos - cut, pos + cut, .3f * r * gs, HuntMarkDeep, .26f, 133));
            foreach (var x in new[] { slash, slashCore, slashDeep }) { x.axisStart = new Vector2(.15f, 1); x.axisEnd = Vector2.one; x.ease = 9; x.hold = .3f; x.fadePower = 1.2f; }
            foreach (var x in HuntGlyph(null, HuntBrandSet, pos, facing < 0 ? 12 : -12, width * 1.08f, .55f, Alpha(light, .9f), HuntMarkDeep, .5f, 129))
            { x.startScale = .72f; x.endScale = 1; x.ease = 7; x.hold = .4f; x.fadePower = 1.2f; if (facing < 0) HuntMirror(x); }
            ImpactLines(pos, width * 1.35f, Alpha(light, .9f), .2f);
            HuntNeedles(pos, new Vector2(facing, facing == 0 ? 1 : .35f), facing == 0 ? 180 : 75, 8, 6 * r, light, .24f, .34f, 2);
            HuntInk(pos, new Vector2(facing == 0 ? 1 : facing, .3f), .62f * r, HuntMarkSmoke, 3, .45f);
            for (int i = lockFrom; i < clips.Count; i++) clips[i].age = -snap;
            // from the hunter's side, the mark's speed lines streaking in
            if (facing != 0)
                for (int i = 0; i < 3; i++)
                {
                    float y = (i - 1) * .22f * r;
                    var lineIn = Line(pos + new Vector2(-facing * 2.4f * r, y), pos + new Vector2(-facing * .9f * r, y * .6f), .05f, i == 1 ? new Color(1, 1, 1, .85f) : Alpha(light, .8f), .14f, 127);
                    lineIn.velocity = new Vector2(facing * 8, 0); lineIn.fadePower = 1.5f;
                }
            if (g >= 2)
            {
                var inner = Spawn("Mark inner ring", ring, pos, width * .55f, Alpha(HuntMarkHot, .85f), .4f, true, 128); inner.age = -snap; inner.startScale = 1.6f; inner.endScale = 1; inner.ease = 4; inner.hold = .3f; inner.fadePower = 1.4f;
            }
            if (g >= 3)
            {
                var rune = RuneCircle(pos, width * 1.15f, Alpha(HuntGold, .75f), .8f, 90, false, 126); rune.age = -snap;
                Glint(pos, 1.6f * r, Alpha(HuntGold, .9f), .24f, 45).age = -snap;
            }
        }

        // ---------------------------------------------------------------- Hook: the chain hook

        private void CastHook(CastInfo c)
        {
            HuntChainTextures();
            float w = c.windup; int f = c.facing, g = Grade; float gs = HuntScale(g);
            Color light = HuntTone(HuntSteelLight, c.tint);
            // the throwing hand, held out ahead of the face (c.pos is already at the small hero's head)
            Vector2 hand = c.pos + new Vector2(f * .6f, -.1f), feet = c.pos + Vector2.down;
            HuntExpect(SkillEffect.Hook, c, hand, light, w < .2f ? gs * .75f : gs, g);
            if (w < .2f) return;
            int mark = clips.Count;
            // the chain is whirled overhead, ahead of and above the head, so the thrower's face stays clear
            Vector2 hub = c.pos + new Vector2(f * .6f, .8f); float R = .7f * gs, life = w + .02f, rate = -f * 880, start = f > 0 ? 210 : -30;
            Underlay(hub, R * 3.4f, .2f, w + .08f).fadeIn = .1f;
            // a straight run of painted links turning like a clock hand, the painted hook at its end in its own glow
            Color shine = g >= 3 ? Alpha(HuntGold, .8f) : Alpha(HuntSteelHot, .85f);
            var spoke = new[]
            {
                Spawn("Whirled chain", HuntChainSet[0], hub, R, Color.white, life, false, 118),
                Spawn("Whirled chain shine", HuntChainSet[1], hub, R, shine, life, true, 124),
            };
            var head = new[]
            {
                Spawn("Whirled hook", HuntHookSet[0], hub, 1.2f * gs, Color.white, life, false, 120),
                Spawn("Whirled hook shine", HuntHookSet[1], hub, 1.2f * gs, Alpha(HuntSteelHot, .95f), life, true, 126),
                Spawn("Whirled hook glow", glow, hub, 1.5f * gs, Alpha(light, .26f), life, true, 119),
            };
            foreach (var x in spoke) { HuntWhirl(x, hub, start, rate, R * .5f, 90 * f); x.startScale = .35f; x.endScale = 1; x.ease = 2.4f; }
            foreach (var x in head) { HuntWhirl(x, hub, start, rate, R, 0); if (f < 0) HuntMirrorY(x); }
            // the turning chain leaves a fan of fading steel copies: the propeller blur of a whirled chain
            Emitter(spoke[0], 45, 0, (owner, p) =>
            {
                // none in the last moments, so no copy hangs over the thrower once the chain has flown
                if (owner.age > owner.life - .07f) return;
                var ghost = Spawn("Whirled chain afterimage", HuntChainSet[0], p, R, Alpha(light, .3f), .08f, false, 115);
                ghost.scale = owner.root.transform.localScale; ghost.fadePower = 1.4f; HuntTurn(ghost, owner.angle + owner.angleOffset);
            });
            var blur = Spawn("Chain whirl blur", HuntDiscBlurSet[0], hub, R * 2.5f, Alpha(light, .6f), life, true, 123);
            var blurDeep = Spawn("Chain whirl blur depth", HuntDiscBlurSet[1], hub, R * 2.5f, HuntSteelDeep, life, false, 116);
            foreach (var x in new[] { blur, blurDeep }) { x.spin = rate; x.angle = start - 20; x.startScale = .35f; x.endScale = 1; x.ease = 2.2f; x.fadeIn = .1f; x.hold = .88f; x.fadePower = 2; if (f < 0) HuntMirror(x); }
            var disk = Spawn("Chain whirl glow", glow, hub, R * 2.4f, Alpha(light, .22f), life, true, 28); disk.fadeIn = .1f; disk.hold = .85f; disk.pulse = .06f; disk.fadePower = 2;
            // sparks thrown off the whirling hook
            Emitter(head[1], 30 * gs, 0, (owner, p) =>
            {
                Vector2 at = owner.root.transform.position, tangent = Direction(owner.angle);
                Spark(at, tangent * Range(3, 6) + Vector2.up * Range(0, 1.5f), Chance(.4f) ? HuntEmber : Color.white, Range(.16f, .26f), Range(.16f, .26f), 6, 2.5f);
            });
            Converge(hub, 8, 1.8f * gs, light, w, .4f);
            GroundGlow(feet, 3.2f * gs, Alpha(light, .22f), w + .3f);
            if (g >= 2) { var seal = RuneCircle(feet, 2.8f * gs, Alpha(light, .7f), w + .35f, f * 80, true); seal.startScale = .5f; }
            if (g >= 3)
            {
                var crown = Spawn("Whirl gold rune", runeRing, hub, R * 2.4f, Alpha(HuntGold, .6f), life, true, 121);
                crown.spin = -rate * .2f; crown.startScale = .4f; crown.endScale = 1; crown.ease = 3; crown.fadeIn = .1f; crown.hold = .85f; crown.fadePower = 2;
            }
            HuntFollow(c.anchor, mark);
        }

        /// <summary>Sets a free clip circling <paramref name="hub"/> (radius growing from a third), turned along its path plus <paramref name="offset"/>.</summary>
        private static void HuntWhirl(Clip clip, Vector2 hub, float start, float rate, float radius, float offset)
        {
            clip.orbitCenter = hub; clip.orbitAngle = start; clip.orbit = rate; clip.orbitRadius = radius * .35f; clip.orbitRadiusEnd = radius; clip.orbitPower = .45f;
            clip.alignVelocity = true; clip.angleOffset = offset; clip.fadeIn = .08f; clip.hold = .88f; clip.fadePower = 2;
            clip.position = hub + Direction(start) * radius * .35f; clip.root.transform.position = new Vector3(clip.position.x, clip.position.y, 0);
            HuntTurn(clip, start + 90 * Mathf.Sign(rate));
        }

        /// <summary>The throw's whip crack at the hand: a steel-white hit spark, sonic shock fronts, burst lines and needles along the throw.</summary>
        private void HuntHookRelease(Vector2 hand, Vector2 dir, Color light, float s, int g)
        {
            float a = HuntAngle(dir); Vector2 at = hand + dir * .3f;
            HuntSpark(at, 1.5f * s, Color.Lerp(HuntSteelLight, HuntSteelHot, .3f), .12f, 132);
            HuntBoom(hand + dir * .5f, dir, 1.7f * s, Alpha(HuntSteelHot, .85f), .22f, 10);
            HuntBoom(hand + dir * .5f, dir, 1.7f * s, HuntSteelDeep, .2f, 10, false, 127);
            var lines = ImpactLines(at + dir * .3f, 2.1f * s, Alpha(light, .8f), .16f); lines.angle = a; lines.scale.y *= .55f;
            HuntNeedles(at, dir, 30, 6, 10 * s, HuntEmber, .2f, .32f, 4);
            if (g >= 3) ShockFlip(at, 1.9f * s, Alpha(HuntGold, .85f), .24f, 131);
        }

        // The chain shot to the enemy: painted links paid out from the hand to a barbed hook in the body, following the enemy as
        // gameplay reels it in over .16 s with a whip running down the chain, then going slack and dropping away.
        // A boss or a flyer is not moved: the chain snaps taut, ripples once and goes slack.
        private void TrailHook(Vector2 from, Vector2 to, Color tint)
        {
            HuntChainTextures();
            int g = Grade; float gs = HuntScale(g); Color light = HuntTone(HuntSteelLight, tint);
            Vector2 delta0 = to - from; int side = delta0.x >= 0 ? 1 : -1;
            if (Time.frameCount != HuntHookFrame)
            {
                HuntHookFrame = Time.frameCount; HuntHookIndex = 0;
                Vector2 hand, body; var booked = HuntClaim(SkillEffect.Hook, from, out hand, out body);
                if (booked != null) HuntHookRelease(hand, HuntAim(to - hand, new Vector2(side, 0)), booked.light, booked.s, booked.g);
            }
            else HuntHookIndex++;
            // the chain leaves the throwing hand, just in front of the chest
            Vector2 start = from + HuntAim(delta0, new Vector2(side, 0)) * Mathf.Min(.4f, delta0.magnitude * .3f);
            Vector2 delta = to - start; float length = delta.magnitude; if (length < .1f) return;
            Vector2 dir = delta / length; float a = HuntAngle(dir);
            bool boss; bool held = HuntHookHolds(to, out boss);
            float stop = .95f + HuntHookIndex * .55f;
            Vector2 end = !held && Mathf.Abs(delta0.x) > stop ? new Vector2(from.x + side * stop, to.y) : to;
            HuntHookTo = to; HuntHookEnd = end; HuntHookSide = side; HuntHookHeld = held; HuntHookBoss = boss;
            const float reel = .16f, slack = .32f;
            float life = reel + slack, L = 1.1f * (1 + (gs - 1) * .5f), headW = 1.25f * gs;
            int count = Mathf.CeilToInt(length / L) + 1;
            // painted steel links with a cold glint along each (gold once awakened)
            Color shine = g >= 3 ? Alpha(HuntGold, .8f) : Alpha(HuntSteelHot, .85f);
            var tones = new[] { Color.white, shine };
            var chain = new Clip[count][];
            for (int j = 0; j < count; j++)
            {
                chain[j] = new Clip[2];
                for (int q = 0; q < 2; q++)
                {
                    var x = chain[j][q] = Spawn(q == 0 ? "Hook chain" : "Hook chain shine", HuntChainSet[q], start, L, tones[q], life, q == 1, q == 0 ? 118 : 124);
                    x.hold = reel / life; x.fadePower = 1.4f;
                }
            }
            // links thrown to the left are turned over, so their lit flank stays on top
            Vector3 linkScale = chain[0][0].scale; if (side < 0) linkScale.y = -linkScale.y;
            // the painted hook is drawn over its own bite spark, so the barb stays readable as it strikes
            var head = HuntObject(null, HuntHookSet, to, a, headW, life + .12f, Alpha(HuntSteelHot, .95f), Alpha(light, .4f), 137);
            foreach (var x in head) { x.hold = (reel + .1f) / (life + .12f); x.fadePower = 1.5f; if (side < 0) HuntMirrorY(x); }
            Vector2 eyeOffset = dir * (.37f * headW);
            float whip = held ? .22f : .16f;
            Action<float> pose = t =>
            {
                float k = Mathf.Clamp01(t / reel), eased = 1 - (1 - k) * (1 - k);
                Vector2 headAt = Vector2.Lerp(to, end, eased), eye = headAt - eyeOffset, span = eye - start;
                float spanLength = span.magnitude;
                Vector2 along = spanLength > .01f ? span / spanLength : dir, across = new Vector2(-along.y, along.x);
                float baseAngle = HuntAngle(along), amp = whip * gs * (1 - eased), phase = t * 38;
                foreach (var x in head) HuntPlace(x, headAt, a);
                for (int j = 0; j < count; j++)
                {
                    float back = j * L, piece = Mathf.Min(L, spanLength - back);
                    bool visible = piece > .04f;
                    float fromHand = spanLength - (back + piece * .5f);
                    float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(fromHand / Mathf.Max(.01f, spanLength)));
                    float offset = amp * envelope * Mathf.Sin(fromHand * 2.2f - phase), slope = amp * envelope * 2.2f * Mathf.Cos(fromHand * 2.2f - phase);
                    Vector2 center = eye - along * (back + piece * .5f) + across * offset;
                    for (int q = 0; q < 2; q++)
                    {
                        var x = chain[j][q];
                        x.scale = new Vector3(linkScale.x * Mathf.Max(.01f, piece) / L, linkScale.y, 1);
                        x.color = visible ? tones[q] : Alpha(tones[q], 0);
                        HuntPlace(x, center, baseAngle + Mathf.Atan(slope) * Mathf.Rad2Deg);
                    }
                }
            };
            pose(0);
            // posed every frame while it hauls (so the chain keeps up with the reeled enemy at any frame rate), then dropped
            bool dropped = false; float acc = 0;
            HuntControl(head[1], (owner, dt) =>
            {
                float t = owner.age;
                if (t <= reel + .01f)
                {
                    pose(t);
                    // the chain rattles as it hauls: sparks off the links, and speed lines racing back along it toward the thrower
                    acc += dt;
                    if (acc >= 1 / 20f)
                    {
                        acc = 0;
                        float k = Mathf.Clamp01(t / reel), eased = 1 - (1 - k) * (1 - k);
                        Vector2 headAt = Vector2.Lerp(to, end, eased), pt = Vector2.Lerp(start, headAt, Range(.2f, .9f)), along = HuntAim(headAt - start, dir);
                        Vector2 across = new Vector2(-along.y, along.x);
                        Spark(pt, across * Range(-3, 3) - along * Range(2, 5), Chance(.5f) ? HuntEmber : Color.white, Range(.18f, .26f), Range(.14f, .22f), 6, 3);
                        if (!held)
                            for (int i = 0; i < 2; i++)
                            {
                                Vector2 s0 = Vector2.Lerp(start, headAt, Range(.3f, 1)) + across * Range(.16f, .36f) * (i == 0 ? 1 : -1);
                                var pull = Line(s0, s0 - along * Range(.7f, 1.3f), Range(.05f, .08f), i == 0 ? Color.white : Alpha(light, .95f), Range(.1f, .14f), 126);
                                pull.velocity = -along * 14; pull.hold = .2f; pull.fadePower = 1.3f;
                            }
                    }
                    return;
                }
                if (dropped) return;
                dropped = true; pose(reel);
                // the chain goes slack: its links drop and fade, shedding a few glints of steel
                foreach (var links in chain)
                {
                    Vector2 v = new Vector2(Range(-.4f, .4f), Range(.3f, 1.2f)); float turn = Range(-60, 60);
                    foreach (var x in links) { x.velocity = v; x.gravity = 10; x.spin = turn; }
                }
                Vector2 restAt = end - eyeOffset;
                for (int i = 0, n = 3; i < n; i++)
                {
                    var mote = Spawn("Chain glint", fleck, Vector2.Lerp(start, restAt, Range(.15f, 1)), Range(.16f, .26f), Alpha(i % 2 == 0 ? HuntSteelHot : light, .95f), Range(.25f, .4f), true, 124);
                    mote.velocity = new Vector2(Range(-.4f, .4f), Range(.4f, 1.2f)); mote.flicker = .4f; mote.endScale = .2f; mote.ease = 1; mote.angle = Range(-20, 20);
                }
            });
            // the shot itself: a luminous lash down the chain (a soft glow, a steel-blue stroke over its deep copy, all under the
            // links) and a white thread that flashes over them, speed lines racing out with it and a shock front at the hand
            HuntGlowLine(start, to, .6f * gs, Alpha(light, .45f), .14f, 115);
            var lashDeep = HuntOpaque(Line(start, to, .16f * gs, HuntSteelDeep, .1f, 116, 1.02f)); lashDeep.fadePower = 1.4f;
            var lashLit = Line(start, to, .16f * gs, Alpha(light, .95f), .11f, 117, 1.02f); lashLit.hold = .25f; lashLit.fadePower = 1.3f; lashLit.axisEnd = new Vector2(1, .3f);
            var lashCore = Line(start, to, .06f * gs, Color.white, .08f, 127, 1.01f); lashCore.hold = .3f; lashCore.fadePower = 1.3f; lashCore.axisEnd = new Vector2(1, .3f);
            for (int i = 0, n = Budget(4); i < n; i++)
            {
                Vector2 s0 = start + dir * Range(.3f, length * .6f) + new Vector2(-dir.y, dir.x) * Range(-.3f, .3f);
                var speed = Line(s0, s0 + dir * Range(.8f, 1.6f), Range(.05f, .08f), i % 2 == 0 ? Color.white : Alpha(light, .95f), Range(.1f, .14f), 126); speed.velocity = dir * 16; speed.hold = .2f; speed.fadePower = 1.3f;
            }
            HuntBoom(start + dir * .45f, dir, 1.4f * gs, Alpha(HuntSteelHot, .85f), .18f, 8);
        }

        // The hook biting: a steel-white hit spark and shock front where it struck, sparks thrown on, and a short daze crackle that
        // rides in with the enemy. A boss is never dazed (only the bite is drawn) and a boss or a flyer is never moved (it all stays
        // on it). With no facing it is the level-three burst around the thrower: a heavier slam.
        private void ImpactHook(Vector2 pos, float radius, Color tint, int facing)
        {
            HuntChainTextures();
            int g = Grade; float gs = HuntScale(g); Color light = HuntTone(HuntSteelLight, tint);
            if (facing == 0) { HuntHookSlam(pos, radius, light, g, gs); return; }
            bool fresh = HuntHookFrame == Time.frameCount && (pos - HuntHookTo).sqrMagnitude < .05f, boss = fresh && HuntHookBoss;
            Vector2 ride = fresh && !HuntHookHeld ? HuntHookEnd - HuntHookTo : Vector2.zero;
            float r = Mathf.Clamp(radius, .5f, 1.6f), a = facing > 0 ? 0 : 180;
            Underlay(pos, 2f * r * gs, .22f, .25f);
            // the catch: a steel-white spark where the barb bites, a shock front riding in with the prey, burst lines, sparks
            // thrown on and a little cool smoke knocked out
            HuntSpark(pos, 1.9f * r * gs, Color.Lerp(HuntSteelLight, HuntSteelHot, .3f), .15f, 133);
            HuntRide(ShockFlip(pos, 1.7f * r * gs, Alpha(light, .9f), .24f, 131), ride);
            ImpactLines(pos, 2f * r * gs, Alpha(light, .9f), .16f);
            HuntInk(pos, new Vector2(facing, .25f), .5f * r * gs, HuntSteelSmoke, 2, .4f);
            for (int i = 0, n = Budget(7); i < n; i++)
                Spark(pos, Direction(a + Range(-60, 60)) * Range(4, 9) + Vector2.up * Range(0, 2), i % 3 == 0 ? Color.white : i % 3 == 1 ? HuntEmber : light, Range(.24f, .36f), Range(.16f, .28f), 9, 2.4f);
            if (!boss)
            {
                // the daze rides in with the prey: a second, whiter pulse and crackles
                var pulse = HuntRide(ShockFlip(pos, 1.3f * r * gs, Alpha(HuntSteelHot, .8f), .2f, 130), ride); pulse.age = -.05f;
                for (int i = 0, n = 3; i < n; i++)
                {
                    Vector2 p0 = pos + Inside(.25f * r), d = Direction(Range(0, 360)), p1 = p0 + d * Range(.25f, .4f) * r, p2 = p1 + Direction(HuntAngle(d) + Range(-70, 70)) * Range(.2f, .35f) * r;
                    float delay = Range(0, .06f);
                    foreach (var seg in new[] { Line(p0, p1, .05f, Alpha(HuntSteelHot, .95f), .12f, 130), Line(p1, p2, .05f, Alpha(light, .95f), .12f, 130) })
                    { seg.age = -delay; seg.flicker = .5f; seg.fadePower = 1.4f; HuntRide(seg, ride); }
                }
            }
            if (g >= 2) Sparkles(pos, 3, .5f * r, light);
            if (g >= 3)
            {
                HuntRide(ShockFlip(pos, 1.9f * r * gs, Alpha(HuntGold, .85f), .26f, 131), ride);
                Glint(pos, 2.2f * r, Alpha(HuntGold, .9f), .18f, -30);
            }
        }

        // The level-three burst, centred on the thrower (gameplay strikes everything within the radius of its feet): the shock runs
        // out along the ground to the burst radius, while the hook itself comes down on the prey just reeled in at the thrower's side
        // and the chain lashes along the floor. Over the thrower there is only ground light and a shade behind it, so it stays in view.
        // All of it lands at once, on the beat of the burst's damage and shake.
        private void HuntHookSlam(Vector2 pos, float radius, Color light, int g, float gs)
        {
            float R = Mathf.Clamp(radius, .8f, 3f); int side = HuntHookSide < 0 ? -1 : 1;
            Vector2 ground = new Vector2(pos.x, pos.y - .8f), land = ground + Vector2.right * side, top = land + Vector2.up * .45f;
            // around the thrower: the ground shock to the burst's edge, cracks and a shade behind the actors
            Underlay(ground + Vector2.up * .45f, R * 3f, .24f, .5f);
            GroundWave(ground + Vector2.up * .05f, R * 2.7f, Alpha(light, .95f), .4f);
            var dark = Decal(ground, crack, R * 1.9f, new Color(.1f, .12f, .18f, .8f), 1.2f, false, 25); dark.angle = Range(-8, 8);
            var hotCrack = Decal(ground, crack, R * 1.8f, Alpha(light, .8f), .8f, true, 26); hotCrack.angle = dark.angle; hotCrack.fadePower = 1.8f;
            // the hook is already biting the ground by the prey, its barb curled toward the thrower, with the light of its fall above
            float headW = 1.35f * gs; Vector2 headAt = land + Vector2.up * (.42f * headW);
            foreach (var x in HuntObject(null, HuntHookSet, headAt, -90, headW, .5f, Alpha(HuntSteelHot, .95f), Alpha(light, .4f), 137))
            { x.startScale = 1.25f; x.endScale = 1; x.ease = 6; x.hold = .5f; x.fadePower = 1.5f; if (side < 0) HuntMirrorY(x); }
            for (int j = 0; j < 2; j++)
            {
                Vector2 at = headAt + Vector2.up * (.37f * headW + .45f + j * .9f);
                foreach (var x in new[] { Spawn("Slam chain", HuntChainSet[0], at, .9f, Color.white, .24f, false, 133), Spawn("Slam chain shine", HuntChainSet[1], at, .9f, Alpha(HuntSteelHot, .85f), .24f, true, 134) })
                { HuntTurn(x, 90); x.velocity = Vector2.down * 1.5f; x.hold = .3f; x.fadePower = 1.6f; }
            }
            var fall = Line(headAt + Vector2.up * 2.8f, headAt + Vector2.up * .5f, .2f * gs, Alpha(light, .9f), .1f, 121); fall.fadePower = 1.6f; fall.axisEnd = new Vector2(1, .3f);
            HuntGlowLine(headAt + Vector2.up * 2.8f, headAt + Vector2.up * .5f, .6f * gs, Alpha(light, .4f), .14f, 116);
            // the bite on the prey: a hit spark over a steel-blue blast, a slim column of light, burst lines along the floor and smoke
            HuntSpark(top, 1.7f, Color.Lerp(HuntSteelLight, HuntSteelHot, .3f), .15f, 135);
            Blast(top, 1.4f * gs, light, .36f, 129);
            HuntInk(land + Vector2.up * .3f, Vector2.up, .65f, HuntSteelSmoke, 3, .45f);
            Pillar(land, .55f * gs, 2.3f * gs, Alpha(light, .55f), .28f);
            var lit = HuntFlat(ImpactLines(top, R * 1.6f, Alpha(light, .75f), .22f), .5f); lit.angle = 0;
            // the chain lashing along the floor both ways, low and behind the fighters
            for (int i = 0; i < 6; i++)
            {
                int way = i % 2 == 0 ? 1 : -1; float lean = Range(-4, 10);
                Vector2 d = Direction(way > 0 ? lean : 180 - lean); float v = 4.1f * R * Range(.8f, 1.05f), turn = Range(-90, 90);
                Vector2 from = land + Vector2.up * .2f + d * .3f;
                foreach (var x in new[] { Spawn("Lashing chain", HuntChainSet[0], from, .9f * gs, Color.white, .45f, false, 27), Spawn("Lashing chain shine", HuntChainSet[1], from, .9f * gs, Alpha(HuntSteelHot, .8f), .45f, true, 28) })
                { HuntTurn(x, HuntAngle(d)); x.velocity = d * v; x.drag = 5; x.spin = turn; x.hold = .4f; x.fadePower = 1.4f; }
            }
            for (int i = 0, n = Budget(9); i < n; i++)
                Spark(top, Direction(Range(30, 150)) * Range(5, 10), i % 3 == 0 ? Color.white : i % 3 == 1 ? HuntEmber : light, Range(.22f, .36f), Range(.25f, .42f), 14, 2);
            for (int i = 0, n = Budget(4); i < n; i++)
            {
                Vector2 at = i % 2 == 0 ? land : ground;
                var dust = Puff(at + new Vector2(Range(-.4f, .4f), .12f), Range(.6f, .95f), DustTone, new Vector2((i % 4 < 2 ? 1 : -1) * Range(2, 4.5f), Range(.3f, 1f)), Range(.5f, .8f), 118);
                dust.gravity = -.4f; dust.endScale = 2.3f;
            }
            for (int i = 0, n = Budget(4); i < n; i++)
            {
                var chip = Spawn("Slam debris", rock[i % rock.Length], top, Range(.1f, .2f), Color.white, Range(.5f, .8f), false, 128);
                chip.velocity = new Vector2(Range(-4, 4), Range(3, 6.5f)); chip.gravity = 18; chip.spin = Range(-600, 600); chip.fadePower = .6f; chip.hold = .5f;
            }
            if (g >= 3)
            {
                RuneCircle(ground, R * 2.1f, Alpha(HuntGold, .85f), 1f, 80, true);
                Glint(top, 1.6f, Alpha(HuntGold, .9f), .22f, 0);
            }
        }
    }
}
