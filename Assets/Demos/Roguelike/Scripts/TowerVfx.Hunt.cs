using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Hunting tools: the returning war disc, the bouncing hot slug, the hunter's mark and the chain hook.
    // Every solid shape is three layers built from one distance field in a single pass: a dark outline and a shaded,
    // saturated body (alpha, so they hold on the pale Olympus sky) under a white-hot additive core (so they glow in the
    // dark depths). Bursts pair additive light with saturated alpha flare lines and rings for the same reason.
    // Skill level grows density, glow and secondary layers; level three adds a gold-white edge and runes. Rings that mark
    // an area keep its gameplay radius.
    // A caster keeps walking while it winds up, so every wind-up rides along with its caster, and the release burst is
    // booked rather than timed: it fires when the shot or the trail actually leaves, at the hand where the caster is then
    // (a cast cancelled by a dash never shows one).
    public sealed partial class TowerVfx
    {
        // ---------------------------------------------------------------- palette, textures and rigs

        private static readonly Color HuntDiscLight = new Color(1, .8f, .42f), HuntDiscHot = new Color(1, .95f, .8f), HuntDiscBody = new Color(.96f, .6f, .14f, .96f),
            HuntDiscInk = new Color(.24f, .1f, .01f, .86f), HuntDiscFlare = new Color(1, .46f, .05f, .7f),
            HuntSlugLight = new Color(1, .9f, .58f), HuntSlugHot = new Color(1, .99f, .92f), HuntSlugBody = new Color(1, .78f, .22f, .95f),
            HuntSlugInk = new Color(.32f, .14f, 0, .82f), HuntSlugFlare = new Color(1, .58f, .08f, .72f),
            HuntMarkLight = new Color(1, .3f, .3f), HuntMarkHot = new Color(1, .5f, .45f), HuntMarkBody = new Color(.9f, .08f, .14f, .93f),
            HuntMarkInk = new Color(.2f, 0, .03f, .84f), HuntMarkFlare = new Color(.78f, .02f, .1f, .64f),
            HuntSteelLight = new Color(.7f, .85f, 1), HuntSteelHot = new Color(.9f, .97f, 1), HuntSteelBody = new Color(.36f, .45f, .6f, .97f),
            HuntSteelInk = new Color(.03f, .06f, .13f, .88f), HuntSteelFlare = new Color(.14f, .32f, .72f, .62f),
            HuntEmber = new Color(1, .76f, .4f), HuntGold = new Color(1, .9f, .58f), HuntGoldInk = new Color(.42f, .26f, .02f, .7f);

        // Sets are [0] dark outline, [1] shaded body, [2] hot core, all the same size and drawn on top of each other.
        private static Sprite[] HuntDiscSet, HuntChainSet, HuntHookSet, HuntReticleSet, HuntChevronSet, HuntSlugSet;
        private static Sprite HuntDiscBlur;

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

        // ---------------------------------------------------------------- textures (built on a skill's first use)

        private static Sprite HuntSprite(string name, int width, int height, Color32[] colors)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "Tower effect " + name, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 2 };
            texture.SetPixels32(colors); texture.Apply(true, false);
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        /// <summary>One pass over a distance field gives an outline, a shaded body and a hot core. <paramref name="shape"/>(x, y)
        /// returns (signed distance, body light, core light); x spans -1..1 across the width and y keeps the same scale.
        /// A strip that tiles end to end (<paramref name="tile"/>) keeps its ends unfaded. A square shape with four-fold
        /// symmetry (<paramref name="fourfold"/>, shading included) is evaluated on one quadrant and turned into the other three,
        /// so a skill's first cast builds its textures in a few milliseconds.</summary>
        private static Sprite[] HuntLayers(string name, int width, int height, float ink, bool tile, bool fourfold, Func<float, float, Vector3> shape)
        {
            var outline = new Color32[width * height]; var body = new Color32[width * height]; var hot = new Color32[width * height];
            float aspect = height / (float)width;
            fourfold &= width == height && width % 2 == 0;
            int x0 = fourfold ? width / 2 : 0, y0 = fourfold ? height / 2 : 0;
            for (int y = y0; y < height; y++)
            {
                float v = (y + .5f) / height * 2 - 1;
                for (int x = x0; x < width; x++)
                {
                    float u = (x + .5f) / width * 2 - 1;
                    Vector3 s = shape(u, v * aspect);
                    float window = (tile ? 1 : 1 - Smooth(.92f, 1, Mathf.Abs(u))) * (1 - Smooth(.9f, 1, Mathf.Abs(v)));
                    float edge = Mathf.Max(1 - Smooth(ink - .012f, ink + .01f, s.x), (1 - Smooth(ink, ink * 2.6f, s.x)) * .35f) * window;
                    float fill = (1 - Smooth(-.012f, .006f, s.x)) * window;
                    // the core only lights the body and a hair beyond its edge
                    float heat = s.z * window * (1 - Smooth(0, .02f, s.x));
                    byte l = (byte)(Mathf.Clamp01(s.y) * 255);
                    var a = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(edge) * 255));
                    var b = new Color32(l, l, l, (byte)(Mathf.Clamp01(fill) * 255));
                    var c = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(heat) * 255));
                    int w1 = width - 1, i0 = y * width + x;
                    outline[i0] = a; body[i0] = b; hot[i0] = c;
                    if (!fourfold) continue;
                    int i1 = x * width + (w1 - y), i2 = (w1 - y) * width + (w1 - x), i3 = (w1 - x) * width + y;
                    outline[i1] = outline[i2] = outline[i3] = a; body[i1] = body[i2] = body[i3] = b; hot[i1] = hot[i2] = hot[i3] = c;
                }
            }
            return new[] { HuntSprite(name + " outline", width, height, outline), HuntSprite(name + " body", width, height, body), HuntSprite(name + " core", width, height, hot) };
        }

        /// <summary>A single white light texture with four-fold symmetry, evaluated on one quadrant.</summary>
        private static Sprite HuntFourfold(string name, int size, Func<float, float, float> alpha)
        {
            var colors = new Color32[size * size]; int half = size / 2, w1 = size - 1;
            for (int y = half; y < size; y++)
            {
                float v = (y + .5f) / size * 2 - 1;
                for (int x = half; x < size; x++)
                {
                    float u = (x + .5f) / size * 2 - 1;
                    var c = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(u, v)) * 255));
                    colors[y * size + x] = c; colors[x * size + (w1 - y)] = c; colors[(w1 - y) * size + (w1 - x)] = c; colors[(w1 - x) * size + y] = c;
                }
            }
            return HuntSprite(name, size, size, colors);
        }

        private static float HuntTaper(float px, float py, float ax, float ay, float bx, float by, float r0, float r1)
        {
            float dx = bx - ax, dy = by - ay, t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
            return Mathf.Sqrt(Sq(px - (ax + dx * t)) + Sq(py - (ay + dy * t))) - Mathf.Lerp(r0, r1, t);
        }

        // A chakram: eight hooked blades on a rim, four spokes, a hub ring and a gem. The blades' tips lead a clockwise spin.
        private static Vector3 HuntDiscShape(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y), th = Mathf.Atan2(y, x);
            float t = Mathf.Repeat(th / (2 * Mathf.PI) * 8, 1), tooth = .6f + .28f * Mathf.Pow(1 - t, 1.7f), face = .785f * r;
            float outside = r - tooth, outer;
            if (outside > 0) outer = r < .88f ? Mathf.Min(outside * .8f, (1 - t) * face) : outside * .8f;
            else outer = -Mathf.Min(-outside * .8f, t * face);
            float rim = Mathf.Max(outer, .5f - r), spoke = 1e9f;
            for (int k = 0; k < 4; k++)
            {
                float a = (45 + k * 90) * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                spoke = Mathf.Min(spoke, SegmentDistance(x, y, ca * .2f, sa * .2f, ca * .56f, sa * .56f) - .05f);
            }
            float hub = Mathf.Abs(r - .24f) - .055f, gem = r - .085f;
            float d = Mathf.Min(Mathf.Min(rim, spoke), Mathf.Min(hub, gem)), depth = Mathf.Max(0, -d);
            float sheen = .5f + .5f * Mathf.Cos(th * 4 - .8f);
            float light = .5f + .32f * Gauss(depth - .015f, .03f) + .22f * sheen * Smooth(.45f, .7f, r) + (gem < 0 ? .4f : 0);
            float edge = Gauss(outer + .03f, .028f) * Smooth(.52f, .6f, r);
            float core = (d < 0 ? .16f : 0) + edge * .95f + (1 - Smooth(.04f, .1f, r)) + Gauss(hub + .025f, .02f) * .45f;
            return new Vector3(d, light, core);
        }

        // A strip of four chain links (face-on rings alternating with edge-on bars) that tiles end to end along +x.
        private static Vector3 HuntChainShape(float x, float y)
        {
            float X = x * 2, Y = y * 2, best = 1e9f;
            for (int j = -1; j <= 4; j++)
            {
                float c = -1.5f + j, d;
                if ((j & 1) == 0) { float q = Mathf.Sqrt(Sq((X - c) / .58f) + Sq(Y / .28f)); d = Mathf.Abs(q - 1) * .3f - .075f; }
                else d = SegmentDistance(X, Y, c - .56f, 0, c + .56f, 0) - .095f;
                best = Mathf.Min(best, d);
            }
            float depth = Mathf.Max(0, -best);
            float light = .42f + .38f * Mathf.Clamp01(.5f + Y * 1.3f) + .35f * Gauss(depth - .03f, .03f) * Mathf.Clamp01(.4f + Y * 1.5f);
            float core = Gauss(depth - .03f, .025f) * Mathf.Clamp01(.2f + Y * 2.2f) * .95f;
            return new Vector3(best * .5f, light, core);
        }

        // A harpoon hook pointing +x: an eye for the chain, a shaft, a spear point and a barbed J curling down and back.
        private static Vector3 HuntHookShape(float x, float y)
        {
            if (y > .32f || y < -.84f) return new Vector3(1, .5f, 0);
            float eye = Mathf.Abs(Mathf.Sqrt(Sq(x + .74f) + y * y) - .11f) - .045f;
            float shaft = SegmentDistance(x, y, -.63f, 0, .34f, 0) - .07f;
            float halfWidth = .14f * Mathf.Clamp01(1 - (x - .26f) / .64f);
            float point = Mathf.Max(Mathf.Max(Mathf.Abs(y) - halfWidth, .26f - x), x - .9f) * .92f;
            float hx = x - .1f, hy = y + .3f, hr = Mathf.Sqrt(hx * hx + hy * hy);
            float s = Mathf.Repeat(90 - Mathf.Atan2(hy, hx) * Mathf.Rad2Deg, 360);
            const float sweep = 235;
            float endA = (90 - sweep) * Mathf.Deg2Rad;
            float ex = .1f + Mathf.Cos(endA) * .3f, ey = -.3f + Mathf.Sin(endA) * .3f, tx = Mathf.Sin(endA), ty = -Mathf.Cos(endA);
            float curl = s <= sweep ? Mathf.Abs(hr - .3f) - Mathf.Lerp(.075f, .035f, s / sweep) : Mathf.Sqrt(Sq(x - ex) + Sq(y - ey)) - .035f;
            float barb = HuntTaper(x, y, ex, ey, ex + tx * .2f, ey + ty * .2f, .045f, .004f);
            float d = Mathf.Min(Mathf.Min(Mathf.Min(eye, shaft), Mathf.Min(point, curl)), barb), depth = Mathf.Max(0, -d);
            float light = .5f + .32f * Mathf.Clamp01(.5f + y * 1.4f) + .3f * Gauss(depth - .03f, .03f);
            float core = Gauss(depth - .028f, .022f) * .85f + (d < 0 ? Gauss(x - .82f, .08f) * .6f : 0);
            return new Vector3(d, light, core);
        }

        // A reticle: a ring broken at the cardinals where four pointed ticks cross it, an inner ring and a centre dot.
        private static Vector3 HuntReticleShape(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y), deg = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            float cd = Mathf.Abs(Mathf.Repeat(deg + 45, 90) - 45), rad = cd * Mathf.Deg2Rad;
            float arc = Mathf.Max(Mathf.Abs(r - .72f) - .045f, (16 - cd) * Mathf.Deg2Rad * r);
            float along = r * Mathf.Cos(rad), across = r * Mathf.Sin(rad), half = .1f * Mathf.Clamp01((along - .46f) / .44f);
            float tick = Mathf.Max(Mathf.Max(across - half, .46f - along), along - .9f) * .9f;
            float inner = Mathf.Abs(r - .3f) - .024f, dot = r - .055f;
            float d = Mathf.Min(Mathf.Min(arc, tick), Mathf.Min(inner, dot)), depth = Mathf.Max(0, -d);
            return new Vector3(d, .72f + .3f * Gauss(depth - .02f, .02f), Gauss(depth - .018f, .016f) * .9f + (1 - Smooth(.03f, .06f, r)));
        }

        // A chevron pointing +x (a jaw of the reticle, the head of the mark's arrow).
        private static Vector3 HuntChevronShape(float x, float y)
        {
            float d = Mathf.Min(HuntTaper(x, y, -.55f, .6f, .6f, 0, .1f, .15f), HuntTaper(x, y, -.55f, -.6f, .6f, 0, .1f, .15f)), depth = Mathf.Max(0, -d);
            return new Vector3(d, .7f + .3f * Gauss(depth - .03f, .03f), Gauss(depth - .03f, .025f) * .9f);
        }

        // A hot slug pointing +x: a rounded body and an ogive nose.
        private static Vector3 HuntSlugShape(float x, float y)
        {
            float body = SegmentDistance(x, y, -.55f, 0, .2f, 0) - .24f, nose = (Mathf.Sqrt(Sq((x - .2f) / .62f) + Sq(y / .24f)) - 1) * .24f;
            float d = Mathf.Min(body, nose);
            float core = (1 - Smooth(-.1f, 0, d)) * .35f + (d < 0 ? Gauss(y, .07f) * Smooth(-.7f, .5f, x) * .75f : 0);
            return new Vector3(d, .62f + .38f * Mathf.Clamp01(.5f + y * 2.2f), core);
        }

        private static void HuntDiscTextures()
        {
            if (HuntDiscSet != null) return;
            HuntDiscSet = HuntLayers("Hunt war disc", 128, 128, .05f, false, true, HuntDiscShape);
            // Four comet arcs and an inner trace whose bright heads lead a clockwise spin: the disc's motion blur.
            HuntDiscBlur = HuntFourfold("Hunt disc blur", 96, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v); if (r > 1) return 0;
                float th = Mathf.Atan2(v, u) / (2 * Mathf.PI);
                float t = Mathf.Repeat(th * 4, 1), tail = Mathf.Pow(1 - t, 2.2f) * Smooth(0, .04f, t);
                float t2 = Mathf.Repeat(th * 4 + .37f, 1), tail2 = Mathf.Pow(1 - t2, 3) * Smooth(0, .05f, t2);
                float a = Gauss(r - .8f, .055f) * tail + Gauss(r - .8f, .14f) * tail * .3f + Gauss(r - .5f, .04f) * tail2 * .5f;
                return Mathf.Clamp01(a) * Window(r);
            });
        }

        private static void HuntChainTextures()
        {
            if (HuntChainSet != null) return;
            HuntDiscTextures();
            // a heavier outline than the other shapes: the links must read as dark iron against a pale sky
            HuntChainSet = HuntLayers("Hunt chain", 128, 32, .05f, true, false, HuntChainShape);
            HuntHookSet = HuntLayers("Hunt hook", 96, 96, .055f, false, false, HuntHookShape);
        }

        private static void HuntMarkTextures()
        {
            if (HuntReticleSet != null) return;
            HuntReticleSet = HuntLayers("Hunt reticle", 128, 128, .05f, false, true, HuntReticleShape);
            HuntChevronSet = HuntLayers("Hunt chevron", 64, 64, .08f, false, false, HuntChevronShape);
        }

        private static void HuntSlugTextures()
        {
            if (HuntSlugSet != null) return;
            HuntSlugSet = HuntLayers("Hunt slug", 64, 32, .07f, false, false, HuntSlugShape);
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

        /// <summary>A three-layer shape (outline, body, hot core) riding on <paramref name="anchor"/>, or free when there is none.</summary>
        private Clip[] HuntShape(Transform anchor, Sprite[] set, Vector2 pos, float angle, float width, float life, Color ink, Color body, Color hot, int order)
        {
            return HuntShape(anchor, set, pos, angle, width, life, ink, body, hot, order - 10, order - 9, order);
        }

        private Clip[] HuntShape(Transform anchor, Sprite[] set, Vector2 pos, float angle, float width, float life, Color ink, Color body, Color hot, int inkOrder, int bodyOrder, int hotOrder)
        {
            var shape = new[]
            {
                Anchored(anchor, pos, "Hunt shape outline", set[0], width, ink, life, inkOrder, false),
                Anchored(anchor, pos, "Hunt shape body", set[1], width, body, life, bodyOrder, false),
                Anchored(anchor, pos, "Hunt shape core", set[2], width, hot, life, hotOrder, true),
            };
            foreach (var clip in shape) HuntTurn(clip, angle);
            return shape;
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

        /// <summary>A sonic ring standing across <paramref name="dir"/>, opening as it rides forward.</summary>
        private Clip HuntBoom(Vector2 pos, Vector2 dir, float width, Color color, float life, float speed, bool additive = true, int order = 130)
        {
            var clip = Spawn("Hunt sonic ring", ring, pos, width, color, life, additive, order);
            clip.scale.x *= .3f; clip.root.transform.localScale = clip.scale; HuntTurn(clip, HuntAngle(dir));
            clip.startScale = .25f; clip.endScale = 1; clip.ease = 3; clip.fadePower = 1.3f; clip.velocity = dir * speed; clip.drag = 5; return clip;
        }

        /// <summary>Burst lines drawn in a saturated alpha colour, so a burst keeps its shape on pale skies.</summary>
        private Clip HuntFlareLines(Vector2 pos, float width, Color color, float life, int order = 118)
        {
            var clip = Spawn("Hunt flare lines", burst, pos, width, color, life, false, order);
            clip.angle = Range(0, 360); clip.startScale = .5f; clip.endScale = 1.15f; clip.ease = 3; clip.fadePower = 1.3f; return clip;
        }

        private Clip HuntFlareRing(Vector2 pos, float width, Color color, float life, int order = 117)
        {
            var clip = Spawn("Hunt flare ring", ring, pos, width, color, life, false, order);
            clip.startScale = .15f; clip.endScale = 1; clip.ease = 3; clip.fadePower = 1.2f; return clip;
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
            float width = 1.05f * gs;
            HuntExpect(SkillEffect.Boomerang, c, hand, light, w < .2f ? gs * .75f : gs, g);
            if (w < .2f) return;
            int mark = clips.Count;
            Underlay(hand, 2.6f * gs, .22f, w + .1f).fadeIn = .1f;
            // the war disc forming in the throwing hand, spinning up from a spark
            var disc = HuntShape(c.anchor, HuntDiscSet, hand, 0, width, w + .03f, HuntDiscInk, HuntDiscBody, Alpha(HuntDiscHot, .95f), 128);
            foreach (var x in disc) { x.spin = -f * 720; x.fadeIn = w * .3f; x.startScale = .15f; x.endScale = 1; x.ease = 2.4f; x.hold = .9f; x.fadePower = 2; if (f < 0) HuntMirror(x); }
            var blur = Anchored(c.anchor, hand, "Disc spin blur", HuntDiscBlur, width * 1.35f, Alpha(light, g >= 3 ? .55f : .75f), w + .03f, 126);
            var blurTone = Anchored(c.anchor, hand, "Disc spin blur tone", HuntDiscBlur, width * 1.38f, Alpha(HuntDiscFlare, .5f), w + .03f, 117, false);
            foreach (var x in new[] { blur, blurTone }) { x.spin = -f * 1500; x.fadeIn = w * .4f; x.startScale = .3f; x.endScale = 1; x.ease = 2; x.hold = .9f; x.fadePower = 2; if (f < 0) HuntMirror(x); }
            // the disc's glow and the awakened rune sit behind the caster, so they light the air around it without washing it out
            var halo = Anchored(c.anchor, hand, "Disc halo", glow, 2.2f * gs, Alpha(light, .5f), w + .05f, 28);
            halo.startScale = .2f; halo.endScale = 1; halo.ease = 1.5f; halo.pulse = .08f; halo.hold = .85f; halo.fadePower = 2;
            Converge(hand, 10, 2.2f * gs, light, w, .5f);
            // bronze sparks flung off the rim as it spins up
            Emitter(disc[2], 26 * gs, 0, (owner, p) =>
            {
                float grow = Mathf.Clamp01(owner.age / Mathf.Max(.05f, w)), a = Range(0, 360);
                Vector2 rim = (Vector2)owner.root.transform.position + Direction(a) * width * .42f * grow;
                var sp = Spark(rim, Direction(a - f * 90) * Range(2.5f, 5) + Vector2.up * .6f, Chance(.35f) ? Color.white : light, Range(.16f, .26f), Range(.16f, .26f), 3, 2.5f);
                if (Chance(.4f)) { HuntOpaque(sp); sp.color = Alpha(HuntDiscFlare, .85f); }
            });
            // footing, and the awakened disc's gold edge and rune
            GroundGlow(feet, 3.4f * gs, Alpha(light, .32f), w + .3f);
            if (g >= 2) { var seal = RuneCircle(feet, 2.8f * gs, Alpha(light, .75f), w + .35f, f * 70, true); seal.startScale = .5f; }
            if (g >= 3)
            {
                Vector2 crownAt = hand + new Vector2(f * .3f, 0);
                var crown = Anchored(c.anchor, crownAt, "Disc awakened rune", runeRing, width * 1.6f, Alpha(HuntGold, .5f), w + .05f, 28);
                var crownTone = Anchored(c.anchor, crownAt, "Disc awakened rune tone", runeRing, width * 1.62f, Alpha(HuntGoldInk, .4f), w + .05f, 27, false);
                foreach (var x in new[] { crown, crownTone }) { x.spin = f * 160; x.startScale = .4f; x.endScale = 1; x.ease = 3; x.fadeIn = .1f; x.hold = .85f; x.fadePower = 2; }
                var edge = Anchored(c.anchor, hand, "Disc awakened edge", ring, width * 1.22f, Alpha(HuntGold, .85f), w + .03f, 127);
                edge.startScale = .15f; edge.endScale = 1; edge.ease = 2.4f; edge.pulse = .06f; edge.hold = .9f; edge.fadeIn = w * .3f; edge.fadePower = 2;
            }
            HuntFollow(c.anchor, mark);
        }

        /// <summary>The throw: a hot flash, a sweep of the arm opening along the throw, sonic rings, a spark cone and a kick of dust.</summary>
        private void HuntDiscRelease(Vector2 pos, Vector2 dir, Color light, float s, int g, Vector2 feet)
        {
            float a = HuntAngle(dir); int f = dir.x >= 0 ? 1 : -1;
            var flash = Spawn("Disc throw flash", glow, pos, 1.1f * s, Alpha(HuntDiscHot, .8f), .12f, true, 132);
            flash.startScale = .5f; flash.endScale = 1.4f; flash.ease = 2; flash.fadePower = 1.5f;
            Glint(pos, 1.6f * s, Alpha(HuntDiscHot, 1), .18f, a);
            HuntFlareLines(pos, 2.8f * s, HuntDiscFlare, .18f);
            var sweep = Crescent(pos + dir * .5f, 2.3f * s, Alpha(light, .85f), .22f, a, 130); sweep.velocity = dir * 6; sweep.drag = 4;
            var sweepTone = HuntOpaque(Crescent(pos + dir * .5f, 2.4f * s, Alpha(HuntDiscFlare, .6f), .2f, a, 117)); sweepTone.velocity = dir * 6; sweepTone.drag = 4;
            HuntBoom(pos + dir * .4f, dir, 1.9f * s, Alpha(HuntDiscHot, .85f), .24f, 9);
            HuntBoom(pos + dir * .4f, dir, 2f * s, Alpha(HuntDiscFlare, .55f), .22f, 9, false, 117);
            for (int i = 0, n = Budget(7); i < n; i++)
                Spark(pos, Direction(a + Range(-40, 40)) * Range(4, 10) * s, i % 3 == 0 ? Color.white : light, Range(.22f, .38f), Range(.18f, .3f), 4, 2.5f);
            if (g >= 3)
            {
                var gold = Spawn("Disc throw gold ring", ring, pos, 1.6f * s, Alpha(HuntGold, .75f), .26f, true, 131);
                gold.startScale = .3f; gold.endScale = 1; gold.ease = 3; gold.fadePower = 1.4f;
            }
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
            float width = (signature ? 1.05f : .8f) * gs, spin = -side * 1080, start = Range(0, 360);
            // The root keeps its launch rotation on the way back, so everything that turns spins on its own.
            // From level two the level's own aura rides on the disc too, so the metal is drawn over all of its light (the core
            // highlights stay on top): the bladed silhouette must never white out.
            bool over = g >= 2;
            var hot = Attach(root, "War disc core", HuntDiscSet[2], width, Alpha(HuntDiscHot, over ? .8f : .95f), over ? 130 : 125);
            var ink = Attach(root, "War disc outline", HuntDiscSet[0], width, HuntDiscInk, over ? 128 : 118, false);
            var body0 = Attach(root, "War disc body", HuntDiscSet[1], width, HuntDiscBody, over ? 129 : 119, false);
            var blur = Attach(root, "War disc blur", HuntDiscBlur, width * 1.35f, Alpha(light, g >= 3 ? .5f : g == 2 ? .55f : .75f), 123);
            var blurTone = Attach(root, "War disc blur tone", HuntDiscBlur, width * 1.38f, Alpha(HuntDiscFlare, .6f), 117, false);
            var halo = Attach(root, "War disc halo", glow, width * 2.2f, Alpha(light, g >= 3 ? .12f : g == 2 ? .22f : .36f), 116); halo.pulse = .08f;
            var layers = new List<Clip> { ink, body0, blur, blurTone, halo };
            foreach (var x in new[] { hot, ink, body0 }) { x.spin = spin; x.angle = start; }
            foreach (var x in new[] { blur, blurTone }) { x.spin = spin * 1.6f; x.angle = start; }
            if (side < 0) foreach (var x in new[] { hot, ink, body0, blur, blurTone }) HuntMirror(x);
            if (g >= 2)
            {
                var outer = Attach(root, "War disc outer blur", HuntDiscBlur, width * 1.75f, Alpha(light, g >= 3 ? .15f : .26f), 122);
                outer.spin = spin * 1.25f; outer.angle = start + 60; if (side < 0) HuntMirror(outer); layers.Add(outer);
            }
            if (g >= 3)
            {
                // the awakened disc's gold-white edge rings the blades just under the metal; a faint rune turns around it
                var edge = Attach(root, "War disc awakened edge", ring, width * 1.22f, Alpha(HuntGold, .9f), 127); edge.pulse = .05f; layers.Add(edge);
                var rune = Attach(root, "War disc awakened rune", runeRing, width * 1.65f, Alpha(HuntGold, .3f), 120); rune.spin = -spin * .2f; layers.Add(rune);
                var runeTone = Attach(root, "War disc awakened rune tone", runeRing, width * 1.68f, Alpha(HuntGoldInk, .5f), 115, false); runeTone.spin = rune.spin; layers.Add(runeTone);
            }
            // the disc leaves from inside the thrower's chest: let it fade in over its first .06 s
            hot.fadeIn = .06f; foreach (var x in layers) x.fadeIn = .06f;
            var rig = new HuntDiscRig { root = root, grade = g };
            HuntDiscs.RemoveAll(x => !x.root); HuntDiscs.Add(rig);
            // The trail: two points of the rim trace a double cycloid behind the disc (its circular afterimage), over a soft smear,
            // with fading copies of the disc and sparks thrown off the rim. Drawn at about sixty steps a second at any frame rate.
            Vector2 drawn = at; float phase = start, rim = width * .4f, acc = 0; int beat = 0;
            HuntControl(halo, (owner, dt) =>
            {
                if (!root) return;
                acc += dt; if (acc < 1 / 90f) return;
                float span = acc; acc = 0;
                Vector2 now = root.position, step = now - drawn;
                if (owner.age < .05f || step.sqrMagnitude < 1e-6f) { drawn = now; return; }
                Vector2 travel = step.normalized; float before = phase; phase += -side * 1620 * span;
                for (int k = 0; k < 2; k++)
                {
                    Vector2 a0 = drawn + Direction(before + k * 180) * rim, a1 = now + Direction(phase + k * 180) * rim;
                    var trace = Line(a0, a1, (k == 0 ? .12f : .085f) * gs, k == 0 ? Alpha(HuntDiscBody, .9f) : Alpha(Color.Lerp(light, HuntDiscHot, .4f), .95f), .28f, k == 0 ? 116 : 122, 1.4f);
                    if (k == 0) HuntOpaque(trace);
                    trace.fadePower = 1.4f; trace.axisEnd = new Vector2(1, .25f);
                }
                var smear = Line(drawn, now, width * .75f, Alpha(light, g >= 2 ? .12f : .18f), .2f, 120, 1.5f); smear.fadePower = 1.5f;
                beat++;
                if (beat % 2 == 0)
                {
                    // a fading copy of the disc every other step: a stroboscopic afterimage along the flight
                    var ghost = Spawn("War disc afterimage", HuntDiscSet[1], now, width, Alpha(HuntDiscFlare, .5f), .2f, false, 115);
                    ghost.endScale = 1.12f; ghost.ease = 1; ghost.fadePower = 1.4f; if (side < 0) HuntMirror(ghost); HuntTurn(ghost, body0.angle);
                    if (beat % 4 == 0)
                    {
                        var lit = Spawn("War disc light echo", HuntDiscSet[2], now, width, Alpha(light, .45f), .12f, true, 121);
                        lit.endScale = 1.1f; lit.ease = 1; lit.fadePower = 1.5f; if (side < 0) HuntMirror(lit); HuntTurn(lit, body0.angle);
                    }
                }
                if (beat % 3 == 0)
                {
                    float a = Range(0, 360);
                    Spark(now + Direction(a) * rim, Direction(a - side * 90) * Range(3, 6) + travel * Range(1, 3), Chance(.3f) ? Color.white : light, Range(.18f, .3f), Range(.16f, .26f), 4, 2.5f);
                }
                drawn = now;
            });
            Bundle(root, hot, layers.ToArray());
            HuntWatch(root, 6, end => HuntDiscCatch(end, light, gs));
        }

        // The disc caught (or gone): a small ring of light where it vanished, kept small since it is caught at the thrower's chest.
        private void HuntDiscCatch(Vector2 pos, Color light, float s)
        {
            var flash = Spawn("Disc catch flash", glow, pos, 1f * s, Alpha(HuntDiscHot, .7f), .12f, true, 132); flash.endScale = 1.4f; flash.ease = 1; flash.fadePower = 1.5f;
            Glint(pos, 1.5f * s, Alpha(HuntDiscHot, .95f), .16f, 20);
            var band = Spawn("Disc catch ring", ring, pos, 1.4f * s, Alpha(light, .8f), .2f, true, 130); band.startScale = .3f; band.endScale = 1; band.ease = 3; band.fadePower = 1.3f;
            HuntFlareRing(pos, 1.5f * s, Alpha(HuntDiscFlare, .5f), .2f);
            for (int i = 0, n = Budget(5); i < n; i++) Spark(pos, Direction(Range(0, 360)) * Range(2, 5) * s, i % 2 == 0 ? Color.white : light, Range(.16f, .26f), Range(.16f, .24f), 3, 3);
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
            var flash = Spawn("Disc cut flash", glow, pos, (echo ? .8f : 1.2f) * gs, Alpha(HuntDiscHot, .95f), .08f, true, 133); flash.endScale = 1.5f; flash.ease = 1; flash.fadePower = 1.6f;
            if (!echo) HuntDiscHitMark = flash;
            // the rim's cut: a crescent bulging along the disc's travel, over a saturated copy for pale skies
            float cut = a + Range(-20, 20);
            var slash = Crescent(pos, 1.9f * gs, Alpha(Color.Lerp(light, HuntDiscHot, .5f), .95f), .16f, cut, 131); slash.scale.y *= 1.15f;
            var slashTone = HuntOpaque(Crescent(pos, 2f * gs, Alpha(HuntDiscFlare, .75f), .16f, cut, 118)); slashTone.scale.y *= 1.15f;
            Vector2 across = Direction(a + 90 + Range(-25, 25));
            var cutLine = Line(pos - across * .75f * gs, pos + across * .75f * gs, .07f, Color.white, .1f, 132); cutLine.startScale = .3f; cutLine.endScale = 1; cutLine.ease = 4;
            Glint(pos, 1.4f * gs, Alpha(HuntDiscHot, 1), .14f, a + 45);
            if (!echo)
            {
                var band = Spawn("Disc cut ring", ring, pos, 1f * gs, Alpha(light, .85f), .16f, true, 130); band.startScale = .2f; band.endScale = 1; band.ease = 3; band.fadePower = 1.3f;
                HuntFlareLines(pos, 1.8f * gs, Alpha(HuntDiscFlare, .75f), .15f);
            }
            for (int i = 0, count = Budget(echo ? 3 : 7); i < count; i++)
            {
                bool sideways = i % 3 == 2;
                Vector2 v = sideways ? n * (i % 2 == 0 ? 1 : -1) * Range(3, 6) + direction * Range(1, 3) : Direction(a + Range(-40, 40)) * Range(4, 9);
                var sp = Spark(pos, v * gs, i % 3 == 0 ? Color.white : i % 3 == 1 ? HuntEmber : light, Range(.2f, .34f), Range(.16f, .28f), 7, 2.5f);
                if (i % 4 == 3) { HuntOpaque(sp); sp.color = Alpha(HuntDiscFlare, .9f); }
            }
            if (g >= 3 && !echo)
            {
                var gold = Spawn("Disc cut gold ring", ring, pos, 1.5f * gs, Alpha(HuntGold, .85f), .22f, true, 131); gold.startScale = .3f; gold.endScale = 1; gold.ease = 3; gold.fadePower = 1.4f;
                Glint(pos, 1.9f, Alpha(HuntGold, .9f), .18f, a);
            }
            Grade = keep;
        }

        // A ringing burst: thin metallic rings opening one after another to the burst's edge, the disc's ghost flung open, blade shards.
        private void ImpactBoomerang(Vector2 pos, float radius, Color tint, int facing)
        {
            HuntDiscTextures();
            int g = Grade; float gs = HuntScale(g), r = Mathf.Clamp(radius, .5f, 3.5f), s = .7f + r * .35f;
            Color light = HuntTone(HuntDiscLight, tint);
            Underlay(pos, r * 2.8f, .22f, .4f);
            var flash = Spawn("Disc burst flash", glow, pos, 1.8f * s, Alpha(HuntDiscHot, .95f), .12f, true, 133); flash.endScale = 1.5f; flash.ease = 1; flash.fadePower = 1.5f;
            var bloom = Spawn("Disc burst bloom", glow, pos, r * 3f, Alpha(light, .4f), .4f, true, 122); bloom.startScale = .6f; bloom.endScale = 1.2f; bloom.fadePower = 1.4f;
            for (int i = 0; i < 3; i++)
            {
                float width = r * 2.7f * (.6f + .2f * i);
                var band = Wave(pos, width, Alpha(i == 0 ? HuntDiscHot : light, .9f - .2f * i), .3f + .08f * i, 3); band.age = -.05f * i; band.pulse = .05f;
                var bandTone = HuntFlareRing(pos, width * 1.03f, Alpha(HuntDiscFlare, .55f - .12f * i), .3f + .08f * i); bandTone.age = band.age;
            }
            foreach (var x in HuntShape(null, HuntDiscSet, pos, Range(0, 360), 1.1f * gs, .32f, Alpha(HuntDiscInk, .6f), Alpha(HuntDiscBody, .8f), Alpha(HuntDiscHot, .9f), 128))
            { x.startScale = .8f; x.endScale = 1.8f; x.ease = 2; x.spin = -1400; x.fadePower = 1.4f; }
            ImpactLines(pos, r * 2.4f, Alpha(light, .8f), .22f);
            HuntFlareLines(pos, r * 2.6f, HuntDiscFlare, .24f);
            Shards(pos, 8, 6 * s, .3f * s, Alpha(HuntDiscHot, .9f), .45f);
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
            // the heat glow sits ahead of the hand and kept soft, so the shooter's body never whites out
            Vector2 glowAt = hand + new Vector2(f * .25f, 0);
            var heat = Anchored(c.anchor, glowAt, "Slug heat", glow, 1.2f * gs, Alpha(light, .5f), w + .04f, 126);
            var heatTone = Anchored(c.anchor, glowAt, "Slug heat tone", glow, 1.1f * gs, Alpha(HuntSlugFlare, .4f), w + .04f, 116, false);
            foreach (var x in new[] { heat, heatTone }) { x.startScale = .15f; x.endScale = 1; x.ease = 1.4f; x.pulse = .1f; x.hold = .85f; x.fadePower = 2; }
            foreach (var x in HuntShape(c.anchor, HuntSlugSet, hand, a, .75f * gs, w + .03f, HuntSlugInk, HuntSlugBody, Alpha(HuntSlugHot, .95f), 128))
            { x.fadeIn = w * .4f; x.startScale = .3f; x.endScale = 1; x.ease = 2; x.hold = .9f; x.fadePower = 2; }
            var star = Anchored(c.anchor, hand + new Vector2(f * .15f, 0), "Slug star", glint, .8f * gs, Alpha(HuntSlugHot, .95f), w + .04f, 129);
            star.spin = 260; star.startScale = .1f; star.endScale = 1; star.ease = 1.4f; star.hold = .85f; star.fadePower = 2;
            Converge(hand, 8, 1.6f * gs, light, w, .6f);
            // accelerator rings standing across the aim, opening one after another
            int rings = g >= 2 ? 3 : 2;
            for (int i = 0; i < rings; i++)
            {
                Vector2 at = hand + dir * (.55f + i * .45f);
                float width = (1.5f - i * .25f) * gs, delay = i * w * .18f, span = Mathf.Max(.1f, w - delay + .05f);
                var tone = Spawn("Slug accelerator tone", runeRing, at, width * 1.03f, Alpha(HuntSlugFlare, .75f), span, false, 117);
                var lit = Spawn("Slug accelerator", runeRing, at, width, Alpha(g >= 3 ? HuntGold : light, .95f), span, true, 127);
                foreach (var x in new[] { tone, lit }) { x.scale.x *= .36f; x.root.transform.localScale = x.scale; HuntTurn(x, a); x.age = -delay; x.fadeIn = .06f; x.startScale = .25f; x.endScale = 1; x.ease = 4; x.hold = .85f; x.fadePower = 2; }
                lit.flicker = .12f;
            }
            // a gold sight line to the prey, re-drawn from the hand as the shooter moves
            var sight = Line(hand, c.target, .05f, Alpha(light, .65f), w, 126); sight.fadeIn = w * .6f; sight.flicker = .35f; sight.hold = .85f; sight.fadePower = 2;
            var sightTone = HuntOpaque(Line(hand, c.target, .1f, Alpha(HuntSlugFlare, .6f), w, 116)); sightTone.fadeIn = w * .6f; sightTone.hold = .85f; sightTone.fadePower = 2;
            if (g >= 3) { var crown = RuneCircle(hand + new Vector2(f * .3f, 0), 1.7f * gs, Alpha(HuntGold, .7f), w + .05f, -f * 200, false, 28); crown.startScale = .3f; }
            HuntFollow(c.anchor, mark, HuntSightOf(c.anchor, sight, hand, c.target), HuntSightOf(c.anchor, sightTone, hand, c.target));
        }

        /// <summary>The shot: a white-hot muzzle flash, a hard star, sonic rings and a tight spark cone.</summary>
        private void HuntMuzzle(Vector2 pos, Vector2 dir, Color light, float s, int g)
        {
            float a = HuntAngle(dir);
            var flash = Spawn("Slug muzzle flash", glow, pos, 1.2f * s, Alpha(HuntSlugHot, .95f), .1f, true, 132); flash.endScale = 1.5f; flash.ease = 1; flash.fadePower = 1.5f;
            Glint(pos, 1.5f * s, Alpha(HuntSlugHot, 1), .14f, a);
            HuntBoom(pos + dir * .2f, dir, 1.3f * s, Alpha(HuntSlugHot, .9f), .2f, 8);
            HuntBoom(pos + dir * .6f, dir, 1f * s, Alpha(light, .85f), .2f, 13).age = -.03f;
            HuntBoom(pos + dir * .2f, dir, 1.4f * s, Alpha(HuntSlugFlare, .6f), .18f, 8, false, 117);
            HuntFlareLines(pos, 1.7f * s, Alpha(HuntSlugFlare, .7f), .14f);
            for (int i = 0, n = Budget(6); i < n; i++)
                Spark(pos, Direction(a + Range(-24, 24)) * Range(6, 12) * s, i % 2 == 0 ? Color.white : light, Range(.2f, .32f), Range(.12f, .2f), 2, 3);
            if (g >= 3)
            {
                var gold = Spawn("Slug gold ring", ring, pos, 1.6f * s, Alpha(HuntGold, .9f), .22f, true, 131);
                gold.startScale = .3f; gold.endScale = 1; gold.ease = 3; gold.fadePower = 1.4f;
            }
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
            float len = .72f * gs;
            // the slug and its hard streak ride the root, which gameplay turns at every bounce
            var hot = Attach(root, "Hot slug core", HuntSlugSet[2], len, Alpha(HuntSlugHot, 1), 126);
            var body0 = Attach(root, "Hot slug body", HuntSlugSet[1], len, HuntSlugBody, 119, false);
            var ink = Attach(root, "Hot slug outline", HuntSlugSet[0], len, HuntSlugInk, 118, false);
            // from level two the level's aura rides on the slug too, so its own halo stays soft (a pale sky turns stacked light white)
            float haloAlpha = g >= 2 ? .55f : .8f;
            var halo = Attach(root, "Hot slug halo", glow, 1.2f * gs, Alpha(light, haloAlpha), 122); halo.pulse = .12f;
            var haloTone = Attach(root, "Hot slug heat", glow, 1f * gs, Alpha(HuntSlugFlare, .35f), 117, false); haloTone.pulse = .1f;
            var star = Attach(root, "Hot slug star", glint, .95f * gs, Alpha(HuntSlugHot, .9f), 127); star.spin = 700;
            var rig = new HuntSlugRig { root = root, grade = g, dir = dir, last = at, flownFrom = at, halo = halo, star = star, haloAlpha = haloAlpha };
            HuntSlugTrail(root, rig, "Slug streak", 1.7f * gs, .16f, Alpha(HuntSlugHot, .95f), 124, true, .05f * gs);
            HuntSlugTrail(root, rig, "Slug streak glow", 2.1f * gs, .45f, Alpha(light, .7f), 121, true, 0);
            HuntSlugTrail(root, rig, "Slug streak tone", 2f * gs, .36f, Alpha(HuntSlugFlare, .85f), 116, false, 0);
            HuntSlugTrail(root, rig, "Slug streak ink", 1.9f * gs, .55f, Alpha(HuntSlugInk, .3f), 115, false, .1f * gs);
            var layers = new List<Clip> { body0, ink, halo, haloTone, star };
            foreach (var t in rig.trails) layers.Add(t.clip);
            if (g >= 3)
            {
                // the awakened slug's gold crown, lit brighter by every bounce
                rig.crown = Attach(root, "Hot slug gold ring", ring, 1.1f * gs, Alpha(HuntGold, .3f), 125); rig.crown.pulse = .08f; layers.Add(rig.crown);
                rig.crownScale = rig.crown.scale;
            }
            // the slug leaves from inside the shooter's chest: let it fade in over its first .05 s
            hot.fadeIn = .05f; foreach (var x in layers) x.fadeIn = .05f;
            rig.haloScale = halo.scale; rig.starScale = star.scale;
            HuntSlugGrow(rig, 0);
            HuntSlugs.RemoveAll(x => !x.root); HuntSlugs.Add(rig);
            // Every frame the streak is sized and the heading kept (a bounce reads it); at about sixty steps a second a tracer is
            // drawn along the path (so every bounce leaves a hard angle) and motes are shed behind.
            Vector2 drawn = at; float acc = 0; int beat = 0;
            HuntControl(halo, (owner, dt) =>
            {
                if (!root) return;
                Vector2 now = root.position, step = now - rig.last;
                HuntSlugGrow(rig, (now - rig.flownFrom).magnitude);
                if (step.sqrMagnitude > 1e-6f) rig.dir = step.normalized;
                rig.last = now;
                acc += dt; if (acc < 1 / 90f) return;
                acc = 0;
                if (owner.age > .04f && (now - drawn).sqrMagnitude > 1e-5f)
                {
                    float esc = Mathf.Min(1.35f, 1 + (g >= 3 ? .2f : .06f) * rig.bounces);
                    var trace = Line(drawn, now, .06f * gs * esc, Alpha(HuntSlugHot, .9f), .14f, 123, 1.2f); trace.fadePower = 1.4f; trace.axisEnd = new Vector2(1, .3f);
                    var traceTone = HuntOpaque(Line(drawn, now, .13f * gs * esc, Alpha(HuntSlugFlare, .7f), .16f, 116, 1.2f)); traceTone.fadePower = 1.2f; traceTone.axisEnd = new Vector2(1, .3f);
                }
                drawn = now;
                if (++beat % 3 == 0 && owner.age > .04f)
                {
                    var mote = Spawn("Slug mote", glow, now - rig.dir * Range(.3f, .9f), Range(.1f, .2f) * gs, Alpha(beat % 2 == 0 ? Color.white : light, .9f), Range(.2f, .3f), true, 124);
                    mote.velocity = new Vector2(-rig.dir.y, rig.dir.x) * Range(-1.2f, 1.2f); mote.drag = 2.5f; mote.flicker = .5f; mote.endScale = .2f; mote.ease = 1;
                }
            });
            Bundle(root, hot, layers.ToArray());
            // The shot ends on its last target (just struck) or runs out of flight. Only now is it known which hit was the last,
            // since a bounce to a target straight ahead looks like a hit that goes on.
            HuntWatch(root, 4, last =>
            {
                if (HuntRecent(rig.hitFlash, .12f, rig.hitAt, .2f)) { HuntSlugFinal(rig, light); return; }
                var fizzle = Spawn("Slug fizzle", glow, last, .9f * gs, Alpha(HuntSlugHot, .8f), .12f, true, 131); fizzle.endScale = 1.4f; fizzle.ease = 1; fizzle.fadePower = 1.5f;
                for (int i = 0; i < 4; i++) Spark(last, Direction(Range(0, 360)) * Range(2, 4), i % 2 == 0 ? Color.white : light, Range(.14f, .22f), Range(.14f, .2f), 4, 3);
            });
        }

        /// <summary>How far a slug's heat has climbed (0 cold, 1 after about six awakened bounces): the part of the escalation that is
        /// shown as whiter light, more and faster sparks and a longer exit streak instead of a bigger burst, since every hit is single-target.</summary>
        private static float HuntSlugHeat(float esc) { return Mathf.Clamp01((esc - 1) / 1.2f); }

        // The last target takes the slug: a fuller burst on top of the hit's ping, hotter after every bounce but no wider than a hit.
        private void HuntSlugFinal(HuntSlugRig rig, Color light)
        {
            int keep = Grade; Grade = rig.grade;
            float gs = HuntScale(rig.grade), s = gs * Mathf.Min(rig.esc, 1.35f), heat = HuntSlugHeat(rig.esc); Vector2 pos = rig.hitAt;
            Color hotLight = Color.Lerp(light, HuntSlugHot, heat * .7f);
            ImpactLines(pos, 1.9f * s, Alpha(hotLight, .85f), .2f);
            Wave(pos, 1.8f * s, Alpha(hotLight, .9f), .26f, 3);
            HuntFlareRing(pos, 1.85f * s, Alpha(HuntSlugFlare, .6f), .26f);
            var bloom = Spawn("Slug final bloom", glow, pos, 1.8f * gs, Alpha(light, .3f), .4f, true, 122); bloom.startScale = .6f; bloom.endScale = 1.3f; bloom.fadePower = 1.5f;
            float reach = 1.6f * gs * (1 + heat);
            var through = Line(pos, pos + rig.hitDir * reach, .1f * s, Alpha(HuntSlugHot, .9f), .14f + .06f * heat, 130); through.startScale = .3f; through.endScale = 1; through.ease = 4; through.velocity = rig.hitDir * 4;
            var throughTone = HuntOpaque(Line(pos, pos + rig.hitDir * reach, .18f * s, Alpha(HuntSlugFlare, .6f), .16f + .06f * heat, 117)); throughTone.startScale = .3f; throughTone.endScale = 1; throughTone.ease = 4; throughTone.velocity = rig.hitDir * 4;
            EmberBurst(pos, 5 + Mathf.Min(rig.bounces, 6), 4 * s * (1 + .3f * heat), hotLight, .6f, 4);
            if (rig.grade >= 3) Glint(pos, 1.5f * s, Alpha(Color.Lerp(HuntGold, Color.white, heat * .5f), .6f + .35f * heat), .22f, 45);
            Grade = keep;
        }

        /// <summary>A streak layer behind the slug (front <paramref name="front"/> units behind its centre), grown by <see cref="HuntSlugGrow"/>.</summary>
        private void HuntSlugTrail(Transform root, HuntSlugRig rig, string name, float length, float thickness, Color color, int order, bool additive, float front)
        {
            var clip = Attach(root, name, streak, length, color, order, additive, new Vector2(-front - length * .5f, 0));
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

        // Every bounce point and the final hit: a ping of white-hot light and a spark fan thrown off the struck face.
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
            var flash = Spawn("Ricochet flash", glow, pos, 1.1f * s, Alpha(HuntSlugHot, 1), .08f, true, 133); flash.endScale = 1.5f; flash.ease = 1; flash.fadePower = 1.6f;
            HuntSlugHitMark = flash;
            Glint(pos, 1.3f * s, Alpha(HuntSlugHot, 1), .15f, aa + 45);
            var bloom = Spawn("Ricochet bloom", glow, pos, 2f * gs, Alpha(light, .3f), .22f, true, 123); bloom.endScale = 1.3f; bloom.fadePower = 1.5f;
            if (bounce)
            {
                // the deflection drawn as a hard chevron: the incoming streak meets the point, a bright scar marks the struck face
                var inLine = Line(pos - incoming * 1.2f * s, pos, .07f * s, Alpha(HuntSlugHot, .95f), .12f, 130); inLine.axisEnd = new Vector2(1, .2f); inLine.fadePower = 1.5f;
                var inTone = HuntOpaque(Line(pos - incoming * 1.2f * s, pos, .14f * s, Alpha(HuntSlugFlare, .8f), .13f, 117)); inTone.axisEnd = new Vector2(1, .2f);
                Vector2 faceDir = new Vector2(-away.y, away.x);
                var face = Line(pos - faceDir * .45f * s, pos + faceDir * .45f * s, .06f, Alpha(HuntSlugHot, .9f), .1f, 131); face.startScale = .3f; face.endScale = 1; face.ease = 4;
            }
            HuntFlareLines(pos, 1.7f * s, Alpha(HuntSlugFlare, .75f), .15f);
            var band = Spawn("Ricochet ring", ring, pos, 1f * s, Alpha(light, .85f), .16f, true, 130); band.startScale = .2f; band.endScale = 1; band.ease = 3; band.fadePower = 1.3f;
            // the heat shows in the spark fan: more of them, flying faster, whiter
            for (int i = 0, n = Budget(8 + 2 * Mathf.Min(k, 5)); i < n; i++)
            {
                bool through = i % 5 == 4;
                Vector2 v = Direction((through ? HuntAngle(direction) : aa) + Range(-50, 50)) * Range(5, 11) * gs * (1 + .5f * heat);
                var sp = Spark(pos, v, i % 3 == 0 ? Color.white : i % 3 == 1 ? HuntSlugHot : light, Range(.2f, .34f) * gs, Range(.14f, .28f), 8, 2.4f);
                if (i % 4 == 3) { HuntOpaque(sp); sp.color = Alpha(HuntSlugFlare, .9f); }
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
                var goldRing = Spawn("Ricochet gold ring", ring, pos, 1.3f * s, Alpha(gold, .85f), .22f, true, 131); goldRing.startScale = .3f; goldRing.endScale = 1; goldRing.ease = 3; goldRing.fadePower = 1.4f;
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
            var flash = Spawn("Ricochet burst flash", glow, pos, 1.6f * gs, Alpha(HuntSlugHot, .95f), .1f, true, 133); flash.endScale = 1.5f; flash.ease = 1; flash.fadePower = 1.5f;
            Wave(pos, r * 2.7f, Alpha(light, .9f), .3f, 3);
            HuntFlareRing(pos, r * 2.75f, Alpha(HuntSlugFlare, .55f), .3f);
            HuntFlareLines(pos, r * 2.4f, HuntSlugFlare, .2f);
            for (int i = 0, n = Budget(5); i < n; i++)
            {
                // a zigzag path: out, a hard turn, out again, inside the burst radius
                Vector2 d1 = Direction(i * 72 + Range(-15, 15)), mid = pos + d1 * r * Range(.4f, .6f), d2 = Direction(HuntAngle(d1) + (i % 2 == 0 ? 55 : -55)), end = mid + d2 * r * .4f;
                var l1 = Line(pos, mid, .06f * gs, Alpha(HuntSlugHot, .95f), .16f, 128); l1.fadePower = 1.5f;
                var l2 = Line(mid, end, .06f * gs, Alpha(HuntSlugHot, .95f), .16f, 128); l2.fadePower = 1.5f; l2.age = -.04f;
                var t1 = HuntOpaque(Line(pos, mid, .12f * gs, Alpha(HuntSlugFlare, .7f), .18f, 116)); t1.fadePower = 1.3f;
                var t2 = HuntOpaque(Line(mid, end, .12f * gs, Alpha(HuntSlugFlare, .7f), .18f, 116)); t2.fadePower = 1.3f; t2.age = -.04f;
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
                var hot = Line(a0, a1, .055f * gs, Alpha(g >= 3 ? HuntGold : HuntSlugHot, .95f), .16f, 127, 1.12f); hot.age = -delay; hot.fadePower = 1.5f;
                var lit = Line(a0, a1, .24f * gs, Alpha(light, .45f), .2f, 122, 1.12f); lit.age = -delay; lit.fadePower = 1.4f;
                var tone = HuntOpaque(Line(a0, a1, .12f * gs, Alpha(HuntSlugFlare, .7f), .2f, 116, 1.12f)); tone.age = -delay; tone.fadePower = 1.3f;
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
            // on the prey (it stays with the prey): a faint reticle settling at the height the mark will strike
            Vector2 prey = c.target + Vector2.up * .2f;
            foreach (var x in HuntShape(null, HuntReticleSet, prey, 45, 1.9f, w + .02f, Alpha(HuntMarkInk, .45f), Alpha(HuntMarkBody, .55f), Alpha(HuntMarkHot, .45f), 127))
            { x.spin = 90; x.fadeIn = w * .4f; x.startScale = 1.9f; x.endScale = 1.2f; x.ease = 1.5f; x.hold = .75f; x.fadePower = 2; }
            int mark = clips.Count;
            Underlay(at, 2.3f * gs, .22f, w + .1f).fadeIn = .1f;
            // the sigil the hunter draws: a crimson reticle before the hand, turning and closing on the aim
            foreach (var x in HuntShape(c.anchor, HuntReticleSet, at, 0, 1.25f * gs, w + .05f, HuntMarkInk, HuntMarkBody, Alpha(HuntMarkHot, .7f), 128))
            { x.spin = -f * 140; x.fadeIn = .1f; x.startScale = 1.3f; x.endScale = .85f; x.ease = 1.5f; x.hold = .86f; x.fadePower = 2; }
            var seal = Anchored(c.anchor, at, "Mark sigil ring", runeRing, 1.9f * gs, Alpha(light, .8f), w + .05f, 126);
            var sealTone = Anchored(c.anchor, at, "Mark sigil ring tone", runeRing, 1.94f * gs, Alpha(HuntMarkFlare, .5f), w + .05f, 117, false);
            foreach (var x in new[] { seal, sealTone }) { x.spin = f * 90; x.fadeIn = .1f; x.startScale = .55f; x.endScale = 1; x.ease = 3; x.hold = .85f; x.fadePower = 2; }
            if (g >= 2)
            {
                var inner = Anchored(c.anchor, at, "Mark sigil inner ring", ring, 1.1f * gs, Alpha(HuntMarkHot, .8f), w + .05f, 127);
                inner.startScale = 1.8f; inner.endScale = .9f; inner.ease = 2; inner.pulse = .06f; inner.fadeIn = .12f; inner.hold = .85f; inner.fadePower = 2;
            }
            if (g >= 3)
            {
                var crown = Anchored(c.anchor, at, "Mark awakened rune", runeRing, 2.1f * gs, Alpha(HuntGold, .5f), w + .05f, 120);
                crown.spin = -f * 60; crown.startScale = .4f; crown.endScale = 1; crown.ease = 3; crown.fadeIn = .1f; crown.hold = .85f; crown.fadePower = 2;
            }
            Converge(at, 9, 1.8f * gs, light, w, .45f);
            // the sight: a thin crimson line drawn to the prey, re-drawn from the sigil as the hunter moves
            var sight = Line(at, prey, .05f, Alpha(light, .7f), w, 126); sight.fadeIn = w * .5f; sight.flicker = .35f; sight.hold = .85f; sight.fadePower = 2;
            var sightTone = HuntOpaque(Line(at, prey, .09f, Alpha(HuntMarkFlare, .5f), w, 116)); sightTone.fadeIn = w * .5f; sightTone.hold = .85f; sightTone.fadePower = 2;
            GroundGlow(feet, 3f * gs, Alpha(light, .28f), w + .3f);
            HuntFollow(c.anchor, mark, HuntSightOf(c.anchor, sight, at, prey), HuntSightOf(c.anchor, sightTone, at, prey));
        }

        /// <summary>The sigil loosing the mark: a crimson flash, a ring and short burst lines.</summary>
        private void HuntMarkFlash(Vector2 pos, Color light, float s, float angle, int g)
        {
            var flash = Spawn("Mark loose flash", glow, pos, 1.4f * s, Alpha(HuntMarkHot, .9f), .1f, true, 132); flash.endScale = 1.5f; flash.ease = 1; flash.fadePower = 1.5f;
            Glint(pos, 1.8f * s, Alpha(HuntMarkHot, 1), .15f, angle);
            Wave(pos, 1.8f * s, Alpha(light, .9f), .22f, 3);
            HuntFlareRing(pos, 1.9f * s, Alpha(HuntMarkFlare, .6f), .22f);
            HuntFlareLines(pos, 2f * s, HuntMarkFlare, .16f);
            for (int i = 0, n = Budget(5); i < n; i++)
                Spark(pos, Direction(angle + Range(-30, 30)) * Range(4, 8) * s, i % 2 == 0 ? Color.white : light, Range(.18f, .3f), Range(.14f, .22f), 2, 3);
            if (g >= 3) { var gold = Spawn("Mark loose gold ring", ring, pos, 2.2f * s, Alpha(HuntGold, .85f), .24f, true, 131); gold.startScale = .3f; gold.endScale = 1; gold.ease = 3; gold.fadePower = 1.4f; }
        }

        // A thin arrow of red light that lands at once: a bright shaft over a dark line, thinning away, with a chevron head driven in.
        // A cast's arrows leave from the sigil the hunter drew (the first of them looses it); a mark passed on leaves from its old prey.
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
            var ink = HuntOpaque(Line(start, to, .2f * gs, Alpha(HuntMarkInk, .6f), .24f, 116)); ink.axisEnd = new Vector2(1, .2f); ink.ease = 2;
            var body0 = HuntOpaque(Line(start, to, .11f * gs, Alpha(HuntMarkBody, .9f), .26f, 117)); body0.axisEnd = new Vector2(1, .15f); body0.ease = 2;
            var lit = Line(start, to, .34f * gs, Alpha(light, .5f), .22f, 125, 1.02f); lit.axisEnd = new Vector2(1, .1f); lit.ease = 2;
            var hot = Line(start, to, .05f * gs, Alpha(g >= 3 ? HuntGold : HuntMarkHot, 1), .18f, 128, 1.01f); hot.axisEnd = new Vector2(1, .3f); hot.ease = 2;
            foreach (var x in HuntShape(null, HuntChevronSet, to - dir * .12f, a, .55f * gs, .3f, HuntMarkInk, HuntMarkBody, Alpha(HuntMarkHot, .7f), 130))
            { x.velocity = dir * 2.5f; x.drag = 9; x.hold = .4f; x.fadePower = 1.5f; x.startScale = 1.3f; x.endScale = 1; x.ease = 4; }
            for (int i = 0, n = Budget(3); i < n; i++)
            {
                Vector2 s0 = start + delta * Range(.1f, .6f) + new Vector2(-dir.y, dir.x) * Range(-.12f, .12f);
                var race = Line(s0, s0 + dir * Range(.6f, 1.1f), .04f, i % 2 == 0 ? new Color(1, 1, 1, .8f) : Alpha(light, .85f), .12f, 126); race.velocity = dir * 16; race.fadePower = 1.5f;
            }
            var muzzle = Spawn("Mark arrow flash", glow, start, .9f * gs, Alpha(HuntMarkHot, .8f), .1f, true, 131); muzzle.endScale = 1.4f; muzzle.ease = 1; muzzle.fadePower = 1.5f;
        }

        // The reticle snapping shut on the prey (the lasting mark over its head is drawn by the status effects).
        private void ImpactHuntMark(Vector2 pos, float radius, Color tint, int facing)
        {
            HuntMarkTextures();
            int g = Grade; float gs = HuntScale(g), r = Mathf.Clamp(radius, .5f, 2f);
            Color light = HuntTone(HuntMarkLight, tint);
            float width = r * 2.1f, life = .75f;
            const float snap = .11f;
            Underlay(pos, r * 2.8f, .26f, .55f);
            // the reticle drops onto the prey and snaps shut, turning a quarter as it locks
            var lockOn = new List<Clip>(HuntShape(null, HuntReticleSet, pos, 45, width, life, HuntMarkInk, HuntMarkBody, Alpha(HuntMarkHot, .7f), 129));
            if (g >= 3)
            {
                var edge = Spawn("Mark awakened edge", HuntReticleSet[2], pos, width * 1.05f, Alpha(HuntGold, .5f), life, true, 130); HuntTurn(edge, 45); lockOn.Add(edge);
            }
            float pop = HuntPop(life, snap);
            foreach (var x in lockOn) { x.startScale = 1.6f; x.endScale = 1; x.ease = pop; x.fadeIn = .03f; x.hold = .55f; x.fadePower = 1.6f; }
            HuntControl(lockOn[2], (owner, dt) =>
            {
                float turn = 45 * Sq(Mathf.Max(0, 1 - owner.age / snap));
                foreach (var x in lockOn) HuntTurn(x, turn);
            });
            // four jaws close in on the diagonals and bite at the ring
            for (int k = 0; k < 4; k++)
            {
                Vector2 dk = Direction(k * 90 + 45);
                float outer = r * 1.9f, inner = r * .95f;
                foreach (var x in HuntShape(null, HuntChevronSet, pos + dk * outer, k * 90 + 225, .42f * r * Mathf.Sqrt(gs), life * .8f, HuntMarkInk, HuntMarkBody, Alpha(HuntMarkHot, .7f), 131))
                { x.velocity = -dk * (outer - inner) * 18; x.drag = 18; x.hold = .4f; x.fadePower = 1.5f; x.fadeIn = .02f; }
            }
            // the lock: a flash, a crimson ring and burst lines as the jaws meet
            var flash = Spawn("Mark lock flash", glow, pos, 1.6f * r * gs, Alpha(HuntMarkHot, .95f), .1f, true, 133); flash.age = -snap; flash.endScale = 1.5f; flash.ease = 1; flash.fadePower = 1.5f;
            Glint(pos, 2f * r * gs, Alpha(HuntMarkHot, 1), .2f, 0).age = -snap;
            var shock = Wave(pos, width * 1.25f, Alpha(light, .9f), .3f, 3); shock.age = -snap;
            HuntFlareRing(pos, width * 1.3f, Alpha(HuntMarkFlare, .6f), .3f).age = -snap;
            HuntFlareLines(pos, width * 1.4f, HuntMarkFlare, .22f).age = -snap;
            ImpactLines(pos, width * 1.3f, Alpha(light, .8f), .2f).age = -snap;
            for (int i = 0, n = Budget(8); i < n; i++)
                Spark(pos, Direction(i * 45 + Range(-12, 12)) * Range(3, 6) * r, i % 2 == 0 ? Color.white : light, Range(.18f, .28f), Range(.16f, .26f), 2, 3).age = -snap;
            // a crimson glow held on the prey for a moment
            var pool = Spawn("Mark glow", glow, pos, width * 1.3f, Alpha(light, .35f), .7f, true, 122); pool.fadeIn = .05f; pool.hold = .3f; pool.fadePower = 1.4f; pool.pulse = .08f;
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
            Vector2 hub = c.pos + new Vector2(f * .6f, .8f); float R = .65f * gs, life = w + .02f, rate = -f * 880, start = f > 0 ? 210 : -30;
            Underlay(hub, R * 3.4f, .2f, w + .08f).fadeIn = .1f;
            // a straight run of links turning like a clock hand, the hook at its end
            Color shine = g >= 3 ? Alpha(HuntGold, .7f) : Alpha(light, .6f);
            var spoke = new[]
            {
                Spawn("Whirled chain outline", HuntChainSet[0], hub, R, HuntSteelInk, life, false, 117),
                Spawn("Whirled chain", HuntChainSet[1], hub, R, HuntSteelBody, life, false, 118),
                Spawn("Whirled chain shine", HuntChainSet[2], hub, R, shine, life, true, 124),
            };
            var head = new[]
            {
                Spawn("Whirled hook outline", HuntHookSet[0], hub, .78f * gs, HuntSteelInk, life, false, 119),
                Spawn("Whirled hook", HuntHookSet[1], hub, .78f * gs, HuntSteelBody, life, false, 120),
                Spawn("Whirled hook shine", HuntHookSet[2], hub, .78f * gs, Alpha(HuntSteelHot, .9f), life, true, 126),
            };
            foreach (var x in spoke) { HuntWhirl(x, hub, start, rate, R * .5f, 90 * f); x.startScale = .35f; x.endScale = 1; x.ease = 2.4f; }
            foreach (var x in head) { HuntWhirl(x, hub, start, rate, R, 0); if (f < 0) HuntMirrorY(x); }
            // the turning chain leaves a fan of fading copies: the propeller blur of a whirled chain
            Emitter(spoke[1], 60, 0, (owner, p) =>
            {
                var ghost = Spawn("Whirled chain afterimage", HuntChainSet[1], p, R, Alpha(HuntSteelFlare, .5f), .08f, false, 115);
                ghost.scale = owner.root.transform.localScale; ghost.fadePower = 1.4f; HuntTurn(ghost, owner.angle + owner.angleOffset);
            });
            var blur = Spawn("Chain whirl blur", HuntDiscBlur, hub, R * 2.5f, Alpha(light, .55f), life, true, 123);
            var blurTone = Spawn("Chain whirl blur tone", HuntDiscBlur, hub, R * 2.55f, Alpha(HuntSteelFlare, .62f), life, false, 116);
            foreach (var x in new[] { blur, blurTone }) { x.spin = rate; x.angle = start - 20; x.startScale = .35f; x.endScale = 1; x.ease = 2.2f; x.fadeIn = .1f; x.hold = .88f; x.fadePower = 2; if (f < 0) HuntMirror(x); }
            var disk = Spawn("Chain whirl glow", glow, hub, R * 2.4f, Alpha(light, .3f), life, true, 28); disk.fadeIn = .1f; disk.hold = .85f; disk.pulse = .06f; disk.fadePower = 2;
            // sparks thrown off the whirling hook
            Emitter(head[2], 30 * gs, 0, (owner, p) =>
            {
                Vector2 at = owner.root.transform.position, tangent = Direction(owner.angle);
                Spark(at, tangent * Range(3, 6) + Vector2.up * Range(0, 1.5f), Chance(.4f) ? HuntEmber : Color.white, Range(.16f, .26f), Range(.16f, .26f), 6, 2.5f);
            });
            Converge(hub, 8, 1.8f * gs, light, w, .4f);
            GroundGlow(feet, 3.2f * gs, Alpha(light, .3f), w + .3f);
            if (g >= 2) { var seal = RuneCircle(feet, 2.8f * gs, Alpha(light, .7f), w + .35f, f * 80, true); seal.startScale = .5f; }
            if (g >= 3)
            {
                var crown = Spawn("Whirl gold rune", runeRing, hub, R * 2.4f, Alpha(HuntGold, .6f), life, true, 120);
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

        /// <summary>The throw's whip crack at the hand: a flash, a glint, sonic rings and sparks along the throw.</summary>
        private void HuntHookRelease(Vector2 hand, Vector2 dir, Color light, float s, int g)
        {
            float a = HuntAngle(dir); Vector2 at = hand + dir * .3f;
            var flash = Spawn("Hook throw flash", glow, at, 1f * s, Alpha(HuntSteelHot, .75f), .1f, true, 132); flash.endScale = 1.5f; flash.ease = 1; flash.fadePower = 1.5f;
            Glint(at, 1.5f * s, Alpha(HuntSteelHot, 1), .16f, a);
            HuntBoom(hand + dir * .5f, dir, 1.6f * s, Alpha(HuntSteelHot, .85f), .22f, 10);
            HuntBoom(hand + dir * .5f, dir, 1.7f * s, Alpha(HuntSteelFlare, .55f), .2f, 10, false, 117);
            HuntFlareLines(at, 2f * s, Alpha(HuntSteelFlare, .7f), .16f);
            for (int i = 0, n = Budget(6); i < n; i++)
                Spark(at, Direction(a + Range(-30, 30)) * Range(5, 10) * s, i % 2 == 0 ? HuntEmber : Color.white, Range(.18f, .3f), Range(.14f, .24f), 4, 2.5f);
            if (g >= 3) { var gold = Spawn("Hook throw gold ring", ring, at, 1.8f * s, Alpha(HuntGold, .85f), .22f, true, 131); gold.startScale = .3f; gold.endScale = 1; gold.ease = 3; gold.fadePower = 1.4f; }
        }

        // The chain shot to the enemy: links paid out from the hand to a barbed hook in the body, following the enemy
        // as gameplay reels it in over .16 s with a whip running down the chain, then going slack and dropping away.
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
            float life = reel + slack, L = .9f * (1 + (gs - 1) * .5f), headW = .85f * gs;
            int count = Mathf.CeilToInt(length / L) + 1;
            // dark iron with a thin cold shine: steel on a pale sky, not a bolt of lightning
            Color shine = g >= 3 ? Alpha(HuntGold, .7f) : Alpha(light, .6f);
            var tones = new[] { HuntSteelInk, HuntSteelBody, shine };
            var chain = new Clip[count][];
            for (int j = 0; j < count; j++)
            {
                chain[j] = new Clip[3];
                for (int q = 0; q < 3; q++)
                {
                    var x = chain[j][q] = Spawn(q == 0 ? "Hook chain outline" : q == 1 ? "Hook chain" : "Hook chain shine", HuntChainSet[q], start, L, tones[q], life, q == 2, q == 0 ? 117 : q == 1 ? 118 : 124);
                    x.hold = reel / life; x.fadePower = 1.4f;
                }
            }
            Vector3 linkScale = chain[0][0].scale;
            // the hook head is drawn over its own bite flash, so the barb stays readable as it strikes
            var head = HuntShape(null, HuntHookSet, to, a, headW, life + .12f, HuntSteelInk, HuntSteelBody, Alpha(HuntSteelHot, .9f), 134, 135, 136);
            foreach (var x in head) { x.hold = (reel + .1f) / (life + .12f); x.fadePower = 1.5f; if (side < 0) HuntMirrorY(x); }
            var headGlow = Spawn("Hook head glow", glow, to, 1.3f * gs, Alpha(light, .4f), reel + .14f, true, 122); headGlow.hold = .5f; headGlow.fadePower = 1.4f;
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
                HuntPlace(headGlow, headAt, 0);
                for (int j = 0; j < count; j++)
                {
                    float back = j * L, piece = Mathf.Min(L, spanLength - back);
                    bool visible = piece > .04f;
                    float fromHand = spanLength - (back + piece * .5f);
                    float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(fromHand / Mathf.Max(.01f, spanLength)));
                    float offset = amp * envelope * Mathf.Sin(fromHand * 2.2f - phase), slope = amp * envelope * 2.2f * Mathf.Cos(fromHand * 2.2f - phase);
                    Vector2 center = eye - along * (back + piece * .5f) + across * offset;
                    for (int q = 0; q < 3; q++)
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
            HuntControl(head[2], (owner, dt) =>
            {
                float t = owner.age;
                if (t <= reel + .01f)
                {
                    pose(t);
                    // the chain rattles as it hauls: sparks off the links
                    acc += dt;
                    if (acc >= 1 / 20f)
                    {
                        acc = 0;
                        float k = Mathf.Clamp01(t / reel), eased = 1 - (1 - k) * (1 - k);
                        Vector2 headAt = Vector2.Lerp(to, end, eased), pt = Vector2.Lerp(start, headAt, Range(.2f, .9f)), along = HuntAim(headAt - start, dir);
                        Spark(pt, new Vector2(-along.y, along.x) * Range(-3, 3) - along * Range(2, 5), Chance(.5f) ? HuntEmber : Color.white, Range(.16f, .24f), Range(.14f, .22f), 6, 3);
                    }
                    return;
                }
                if (dropped) return;
                dropped = true; pose(reel);
                // the chain goes slack: its links drop and fade, shedding a few motes of steel light
                foreach (var links in chain)
                {
                    Vector2 v = new Vector2(Range(-.4f, .4f), Range(.3f, 1.2f)); float turn = Range(-60, 60);
                    foreach (var x in links) { x.velocity = v; x.gravity = 10; x.spin = turn; }
                }
                Vector2 restAt = end - eyeOffset;
                for (int i = 0, n = 3; i < n; i++)
                {
                    var mote = Spawn("Chain mote", glow, Vector2.Lerp(start, restAt, Range(.15f, 1)), Range(.1f, .2f), Alpha(i % 2 == 0 ? HuntSteelHot : light, .9f), Range(.3f, .5f), true, 124);
                    mote.velocity = new Vector2(Range(-.4f, .4f), Range(.4f, 1.2f)); mote.flicker = .5f; mote.endScale = .2f; mote.ease = 1;
                }
            });
            // the shot itself: a faint lash of light down the chain (under the links' dark iron), speed lines and a sonic ring at the hand
            var lash = Line(start, to, .5f * gs, Alpha(HuntSteelLight, .25f), .09f, 121, 1.02f); lash.fadePower = 1.6f;
            var lashCore = Line(start, to, .04f * gs, new Color(1, 1, 1, .5f), .07f, 127, 1.01f); lashCore.fadePower = 1.6f;
            var lashTone = HuntOpaque(Line(start, to, .2f * gs, Alpha(HuntSteelFlare, .55f), .09f, 116, 1.02f)); lashTone.fadePower = 1.6f;
            for (int i = 0, n = Budget(3); i < n; i++)
            {
                Vector2 s0 = start + dir * Range(.3f, length * .6f) + new Vector2(-dir.y, dir.x) * Range(-.25f, .25f);
                var speed = Line(s0, s0 + dir * Range(.8f, 1.5f), Range(.04f, .07f), Alpha(i % 2 == 0 ? Color.white : light, .8f), Range(.1f, .14f), 126); speed.velocity = dir * 16; speed.fadePower = 1.5f;
            }
            HuntBoom(start + dir * .45f, dir, 1.3f * gs, Alpha(HuntSteelHot, .8f), .18f, 8);
        }

        // The hook biting: sparks thrown on, a hot flash where it struck, and a short daze shock that rides in with the enemy.
        // A boss is never dazed (only the bite is drawn) and a boss or a flyer is never moved (the shock stays on it).
        // With no facing it is the level-three burst around the thrower: a heavier slam.
        private void ImpactHook(Vector2 pos, float radius, Color tint, int facing)
        {
            HuntChainTextures();
            int g = Grade; float gs = HuntScale(g); Color light = HuntTone(HuntSteelLight, tint);
            if (facing == 0) { HuntHookSlam(pos, radius, light, g, gs); return; }
            bool fresh = HuntHookFrame == Time.frameCount && (pos - HuntHookTo).sqrMagnitude < .05f, boss = fresh && HuntHookBoss;
            Vector2 ride = fresh && !HuntHookHeld ? HuntHookEnd - HuntHookTo : Vector2.zero;
            float r = Mathf.Clamp(radius, .5f, 1.6f), a = facing > 0 ? 0 : 180;
            Underlay(pos, 2f * r * gs, .22f, .25f);
            var flash = Spawn("Hook bite flash", glow, pos, .8f * r * gs, Alpha(HuntSteelHot, .95f), .09f, true, 133); flash.endScale = 1.5f; flash.ease = 1; flash.fadePower = 1.6f;
            Glint(pos, 1.8f * r * gs, Alpha(HuntSteelHot, 1), .14f, 30);
            HuntFlareLines(pos, 2.2f * r * gs, HuntSteelFlare, .16f);
            ImpactLines(pos, 2f * r * gs, Alpha(light, .8f), .16f);
            for (int i = 0, n = Budget(7); i < n; i++)
            {
                var sp = Spark(pos, Direction(a + Range(-60, 60)) * Range(4, 9) + Vector2.up * Range(0, 2), i % 3 == 0 ? Color.white : i % 3 == 1 ? HuntEmber : light, Range(.2f, .32f), Range(.16f, .28f), 9, 2.4f);
                if (i % 4 == 3) { HuntOpaque(sp); sp.color = Alpha(HuntSteelFlare, .9f); }
            }
            if (!boss)
            {
                // the daze rides in with the prey: a shock ring, a steel glow and crackles
                HuntRide(Wave(pos, 1.8f * r * gs, Alpha(light, .9f), .22f, 3), ride);
                HuntRide(HuntFlareRing(pos, 1.85f * r * gs, Alpha(HuntSteelFlare, .6f), .22f), ride);
                var held = Spawn("Hook bite glow", glow, pos, 1.6f * r * gs, Alpha(light, .55f), .3f, true, 122); held.fadePower = 1.4f; HuntRide(held, ride);
                for (int i = 0, n = 3; i < n; i++)
                {
                    Vector2 p0 = pos + Inside(.25f * r), d = Direction(Range(0, 360)), p1 = p0 + d * Range(.25f, .4f) * r, p2 = p1 + Direction(HuntAngle(d) + Range(-70, 70)) * Range(.2f, .35f) * r;
                    float delay = Range(0, .06f);
                    foreach (var seg in new[] { Line(p0, p1, .045f, Alpha(HuntSteelHot, .95f), .12f, 130), Line(p1, p2, .045f, Alpha(light, .95f), .12f, 130) })
                    { seg.age = -delay; seg.flicker = .5f; seg.fadePower = 1.4f; HuntRide(seg, ride); }
                }
            }
            if (g >= 2) Sparkles(pos, 3, .5f * r, light);
            if (g >= 3)
            {
                var gold = Spawn("Hook bite gold ring", ring, pos, 1.6f * r * gs, Alpha(HuntGold, .85f), .24f, true, 131); gold.startScale = .3f; gold.endScale = 1; gold.ease = 3; gold.fadePower = 1.4f; HuntRide(gold, ride);
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
            HuntFlat(HuntFlareRing(ground, R * 2.75f, Alpha(HuntSteelFlare, .65f), .4f, 25), .34f);
            var dark = Decal(ground, crack, R * 1.9f, new Color(.05f, .06f, .09f, .85f), 1.2f, false, 25); dark.angle = Range(-8, 8);
            var hotCrack = Decal(ground, crack, R * 1.8f, Alpha(light, .8f), .8f, true, 26); hotCrack.angle = dark.angle; hotCrack.fadePower = 1.8f;
            // the hook is already biting the ground by the prey, its barb curled toward the thrower, with the blur of its fall above
            float headW = 1.1f * gs; Vector2 headAt = land + Vector2.up * (.42f * headW);
            foreach (var x in HuntShape(null, HuntHookSet, headAt, -90, headW, .5f, HuntSteelInk, HuntSteelBody, Alpha(HuntSteelHot, .9f), 134, 135, 136))
            { x.startScale = 1.25f; x.endScale = 1; x.ease = 6; x.hold = .5f; x.fadePower = 1.5f; if (side < 0) HuntMirrorY(x); }
            for (int j = 0; j < 2; j++)
            {
                Vector2 at = headAt + Vector2.up * (.37f * headW + .4f + j * .8f);
                foreach (var x in new[] { Spawn("Slam chain outline", HuntChainSet[0], at, .8f, HuntSteelInk, .24f, false, 132), Spawn("Slam chain", HuntChainSet[1], at, .8f, HuntSteelBody, .24f, false, 133) })
                { HuntTurn(x, 90); x.velocity = Vector2.down * 1.5f; x.hold = .3f; x.fadePower = 1.6f; }
            }
            var fall = Line(headAt + Vector2.up * 2.8f, headAt + Vector2.up * .5f, .38f * gs, Alpha(light, .35f), .1f, 121); fall.fadePower = 2.5f; fall.axisEnd = new Vector2(1, .3f);
            var fallTone = HuntOpaque(Line(headAt + Vector2.up * 2.8f, headAt + Vector2.up * .5f, .18f * gs, Alpha(HuntSteelFlare, .45f), .1f, 116)); fallTone.fadePower = 2.5f; fallTone.axisEnd = new Vector2(1, .3f);
            // the bite on the prey: a small hot flash, a slim column of light, burst lines along the floor
            var flash = Spawn("Hook slam flash", glow, top, 1.2f, Alpha(HuntSteelHot, .5f), .14f, true, 133); flash.startScale = .5f; flash.endScale = 1.4f; flash.ease = 2; flash.fadePower = 1.5f;
            Pillar(land, .55f * gs, 2.3f * gs, Alpha(light, .5f), .28f);
            HuntOpaque(Pillar(land, .8f * gs, 2f * gs, Alpha(HuntSteelFlare, .35f), .28f, 117));
            var lines = HuntFlat(HuntFlareLines(top, R * 1.7f, HuntSteelFlare, .26f), .5f); lines.angle = 0;
            var lit = HuntFlat(ImpactLines(top, R * 1.5f, Alpha(light, .7f), .22f), .5f); lit.angle = 0;
            // the chain lashing along the floor both ways, low and behind the fighters
            for (int i = 0; i < 6; i++)
            {
                int way = i % 2 == 0 ? 1 : -1; float lean = Range(-4, 10);
                Vector2 d = Direction(way > 0 ? lean : 180 - lean); float v = 4.1f * R * Range(.8f, 1.05f), turn = Range(-90, 90);
                Vector2 from = land + Vector2.up * .2f + d * .3f;
                foreach (var x in new[] { Spawn("Lashing chain outline", HuntChainSet[0], from, .8f * gs, HuntSteelInk, .45f, false, 27), Spawn("Lashing chain", HuntChainSet[1], from, .8f * gs, HuntSteelBody, .45f, false, 28) })
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
