namespace BackdropPainter;

/// <summary>Forgotten necropolis: cyclopean arches, a colossal seated king in the dusty haze, carved
/// papyrus pillars, the legs of a shattered colossus and drifting spectral lights.</summary>
public sealed class RuinsTheme : ThemePainter
{
    public override string Id => "ruins";

    static readonly Ramp Sand = new("1c1c24", "28282e", "36352f", "4a4538", "625a44", "7c7052", "988a64", "b6a47a", "d2c296");
    static readonly Ramp FarStone = new("1e2a30", "28363a", "334444", "40524e", "50625a", "647468");
    static readonly Col Spirit = Col.Hex("7ef0d8"), Dust = Col.Hex("c8b488"), Sage = Col.Hex("6e8460");
    static readonly (float y, Col c)[] SkyStops = Kit.Stops((-11, "3e4a46"), (-6, "34423f"), (-2, "2a3838"), (2, "222e32"), (6, "1a242a"), (10, "11181e"));

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return Sky();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    /// <summary>Recessed hieroglyphs in a carved band: each cell holds one of a few simple signs.</summary>
    static float Glyph(float x, float y, float cw, float ch, int seed)
    {
        int gx = (int)MathF.Floor(x / cw), gy = (int)MathF.Floor(y / ch);
        float u = M.Fract(x / cw) * 2 - 1, v = M.Fract(y / ch) * 2 - 1;
        int kind = (int)(Field.Hash01(gx, gy, seed) * 6);
        bool cut = kind switch
        {
            0 => u * u + v * v < .45f && u * u + v * v > .15f,                            // ring
            1 => MathF.Abs(u) < .16f || (MathF.Abs(v - .3f) < .14f && MathF.Abs(u) < .6f), // ankh-like cross
            2 => MathF.Abs(v - MathF.Sin(u * 5) * .25f) < .16f && MathF.Abs(u) < .75f,     // water line
            3 => MathF.Abs(u) < .6f && v > -.6f && v < -.6f + (1 - MathF.Abs(u) / .6f) * 1.4f, // pyramid
            4 => (u * u) / .5f + (v * v) / .12f < 1 && !((u * u) < .04f && v * v < .04f),  // eye
            _ => MathF.Abs(u + .1f) < .35f && v > -.7f && v < .5f && !(u > 0 && v > 0),     // bird block
        };
        return cut ? 1 : 0;
    }

    PaintedLayer Sky()
    {
        var s = new LayerSpec("sky", .95f, 48, 30, -11, 21, -97) { Under = SkyStops[0].c, Over = SkyStops[^1].c };
        var c = s.NewCanvas(); var f = c.F;
        c.PaintAll((x, y, i, j) => Kit.Gradient(SkyStops, y, 4, 1, i, j));
        // Receding pillar ranks dissolving into dust.
        foreach (var (sp, w, tone) in new[] { (2.2f, .32f, .12f), (3.6f, .5f, .22f) })
            c.Paint(0, c.Period, -11, 10, (x, y, i, j) =>
            {
                float s2 = f.Snap(sp), u = MathF.Abs(M.Mod(x + sp * .3f, s2) - s2 * .5f);
                if (u > w && !(y > 5.6f + sp * .4f)) return Col.Clear;
                return Col.Lerp(Kit.Gradient(SkyStops, y, 4, 1, i, j), Col.Hex("0e1418"), tone);
            });
        Kit.Shafts(c, -24, 3.2f, .3f, Dust, .2f, -8, 10, 9);
        c.Paint(0, c.Period, -11, -2, (x, y, i, j) =>
        {
            float a = M.Smooth(-2, -7, y) * (.45f + f.Fbm(x, y * 2, 3, 3, 11) * .35f);
            float q = MathF.Floor(a * 5 + Dither.Bayer(i, j)) / 5;
            return q <= 0 ? Col.Clear : Col.Hex("8a8062").WithA(q);
        });
        return new PaintedLayer(s, c);
    }

    PaintedLayer Far()
    {
        var s = new LayerSpec("far", .85f, 64, 32, -11, 21, -96) { Over = Col.Hex("10171c") };
        var c = s.NewCanvas(); var f = c.F;
        // Cyclopean arcade with a great niche holding a seated king.
        var style = new ArcadeStyle
        {
            Span = 5.4f, PierHalf = .9f, Base = -7, Spring = 2.2f, Pointed = .06f, Rings = 2, RingW = .22f, PlinthH = .8f, CapH = .45f,
            Deck = 6.4f, CorniceH = .35f, Corbel = .4f, GalleryTop = 8.4f, GallerySpan = 2.7f, GalleryHalf = .6f, GallerySpring = 7.5f, Oculus = false, Phase = 2.7f,
            Missing = (x, y) =>
            {
                float bx = MathF.Floor(x / .55f) * .55f, by = MathF.Floor(y / .3f) * .3f;
                return by > 7.2f + f.Fbm(bx, 0, 6, 3, 21) * 2.4f + (Field.Hash01((int)(bx * 5), 0, 22) - .5f) * .6f;
            }
        };
        var arch = c.Blank();
        float kx = 18.9f;
        arch.Paint(0, c.Period, -7, c.Top, (x, y, i, j) =>
        {
            var h = ArchKit.Hit(style, f, x, y);
            if (h.Part == Part.None) return Col.Clear;
            float v = ArchKit.BaseTone(h) * .45f + Kit.Lambert(h.N.X, h.N.Y, h.N.Z) * .5f;
            if (h.Part is Part.Wall or Part.Pier or Part.Gallery)
            {
                bool mortar = Kit.Masonry(f, x, y, 1.3f, .65f, 1.5f / 64, 23, out float sh, out _, out _, out _);
                v += (sh - .5f) * .12f - (mortar ? .14f : 0);
            }
            if (h.Part is Part.Pier && Glyph(x, y, .28f, .34f, 25) > 0 && y > -3 && y < 1.5f) v -= .12f;
            return FarStone.At(v + f.Fbm(x, y, 1, 3, 27) * .08f, i, j, .45f);
        });
        // The seated king: throne, legs, torso, arms on knees and a striped headdress, cracked across the face.
        var king = c.Blank();
        king.Paint(kx - 3, kx + 3, -7, 7, (x, y, i, j) =>
        {
            float dx = x - kx, ax = MathF.Abs(dx);
            bool throne = y < -1.4f && ax < 2.2f;
            bool shins = y < 1.6f && y > -1.4f && (MathF.Abs(dx - .62f) < .42f || MathF.Abs(dx + .62f) < .42f);
            bool lap = y > 1.2f && y < 2.1f && ax < 1.25f;
            bool torso = y > 2 && y < 4.4f && ax < 1.05f - (y - 2) * .1f;
            bool arms = y > 1.5f && y < 3.6f && ax > .95f && ax < 1.35f;
            bool head = y > 4.3f && y < 5.4f && ax < .48f;
            bool nemes = y > 4.1f && y < 5.7f && ax < .5f + (5.7f - y) * .42f && !(y < 4.6f && ax < .3f);
            bool crown = y >= 5.7f && y < 6.2f && ax < .42f - (y - 5.7f) * .3f;
            bool brokenArm = dx > .95f && y > 2.6f;
            if (!(throne || shins || lap || torso || (arms && !brokenArm) || head || nemes || crown)) return Col.Clear;
            if (y > 4.9f && dx > .05f && dx < .4f + (y - 4.9f) * .8f && y < 5.6f) return Col.Clear; // a missing chunk of the face
            float v = .45f + (dx < 0 ? .12f : -.06f);
            if (nemes && !head && M.Mod(y, .2f) < .07f) v -= .14f;
            if (throne && Glyph(x, y, .3f, .36f, 29) > 0 && y > -5.6f) v -= .14f;
            return FarStone.At(v + f.Fbm(x, y, .8f, 3, 31) * .1f, i, j, .45f);
        });
        Kit.Edge(king, -1, 0, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("8a9a84"), .25f));
        // Sand drifts banked against everything.
        var sand = c.Blank();
        sand.Paint(0, c.Period, -11, -2, (x, y, i, j) =>
        {
            float top = -5.6f + f.Fbm(x, 0, 4, 3, 33) * 1.6f + MathF.Max(0, 1 - MathF.Abs(f.Dx(x, kx)) / 4) * 1.4f;
            if (y > top) return Col.Clear;
            return Kit.Mix(Col.Hex("8a7c5a"), Col.Hex("5a523e"), M.InvLerp(top, top - 2, y) + f.Fbm2(x, y, 1.5f, .2f, 2, 35) * .2f, i, j);
        });
        Kit.Fog(arch, y => Kit.Smooth(SkyStops, y), y => .4f + M.Smooth(0, -6, y) * .2f);
        Kit.Fog(king, y => Kit.Smooth(SkyStops, y), y => .3f);
        Kit.Fog(sand, y => Kit.Smooth(SkyStops, y), y => .35f);
        c.Over(arch);
        c.Over(king);
        c.Over(sand);
        Kit.Glow(c, kx, 3.4f, 3.2f, Col.Hex("a8c4a0"), .12f);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Mid()
    {
        var s = new LayerSpec("mid", .65f, 96, 36, -9.5f, 22, -95);
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(201);
        var stone = c.Blank();
        // Carved papyrus pillars: banded shafts with hieroglyphs and flared capitals; two are broken.
        void Pillar(float cx, float top, bool broken, int seed)
        {
            float r = .52f;
            stone.Paint(cx - 1.1f, cx + 1.1f, -9.6f, top + 1.2f, (x, y, i, j) =>
            {
                float dx = x - cx, cut = broken ? top - (dx + r) * .55f + f.N(x, y, .18f, seed) * .3f : top;
                if (y > cut + (broken ? 0 : 1.05f)) return Col.Clear;
                float u, v;
                if (!broken && y > top)
                {
                    // Bell capital flaring toward an abacus slab.
                    float t = (y - top) / 1.05f, w = t > .82f ? r * 1.6f : r * (1 + MathF.Pow(t / .82f, 1.8f) * .9f);
                    if (MathF.Abs(dx) > w) return Col.Clear;
                    u = dx / w;
                    v = Kit.Cylinder(u, .18f) * .8f + (t > .82f ? .12f : 0) + (t < .82f && MathF.Abs(MathF.Sin(u * 6)) < .2f ? -.12f : 0);
                    return Sand.At(v, i, j, .4f);
                }
                if (y < -3.6f && MathF.Abs(dx) < r * 1.35f) return Sand.At(.5f - dx * .3f + (y > -3.8f ? .2f : 0), i, j, .4f);
                u = dx / r; if (MathF.Abs(u) > 1) return Col.Clear;
                v = Kit.Cylinder(u, .16f) * .82f + f.Fbm(x, y, .5f, 3, seed + 1) * .08f;
                float band = M.Mod(y + 3.6f, 1.6f);
                if (band < .07f) v -= .2f; else if (band < .12f) v += .1f;
                else if (Glyph(dx / MathF.Max(.3f, MathF.Sqrt(1 - u * u)) + cx, y, .2f, .26f, seed + 3) > 0 && MathF.Abs(u) < .8f) v -= .16f;
                if (f.Ridge(x, y, 1.1f, 3, seed + 5) > .93f) v -= .2f;
                return Kit.Grain(Sand.At(v, i, j, .4f), i, j, seed + 7, .05f, .15f);
            });
        }
        Pillar(2.6f, 6.2f, false, 211); Pillar(7.4f, 3.4f, true, 213); Pillar(24.6f, 6.6f, false, 215); Pillar(29.2f, 6.6f, false, 217);
        // A lintel bridging the right pair, cracked at its middle.
        stone.Paint(23.6f, 30.4f, 7.6f, 8.6f, (x, y, i, j) =>
        {
            float crack = MathF.Abs(x - 27 - f.N(0, y, .2f, 219) * .1f);
            if (crack < .03f) return Col.Clear;
            float v = .5f + (y > 8.45f ? .25f : y < 7.75f ? -.2f : 0) + f.Fbm(x, y, .5f, 3, 221) * .1f;
            if (y > 7.85f && y < 8.35f && Glyph(x, y, .24f, .5f, 223) > 0) v -= .18f;
            return Sand.At(v, i, j, .4f);
        });
        // The legs and feet of a shattered colossus on its carved pedestal.
        float lx = 15.6f;
        stone.Paint(lx - 3, lx + 3, -9.6f, 4, (x, y, i, j) =>
        {
            float dx = x - lx;
            if (y < -3.2f && MathF.Abs(dx) < 2.6f) return Sand.At(.42f + (y > -3.4f ? .25f : 0) - dx * .03f - (Glyph(x, y, .32f, .4f, 225) > 0 && y < -3.6f && y > -6 ? .15f : 0), i, j, .4f);
            foreach (var (ox, top) in new[] { (-.85f, 2.6f), (.95f, 1.2f) })
            {
                float ldx = dx - ox, t = (y + 3.2f) / (top + 3.2f);
                float w = .5f - t * .12f + (y < -2.7f ? .25f * MathF.Max(0, -ldx) / .5f + .05f : 0);
                float cut = top + (ldx) * .6f + f.N(x, y, .2f, 227) * .25f;
                if (y > cut) continue;
                if (y < -2.7f && ldx > -.95f && ldx < .55f) return Sand.At(.55f - ldx * .2f + (y > -2.8f ? .15f : 0), i, j, .4f); // foot
                if (MathF.Abs(ldx) < w) return Sand.At(Kit.Cylinder(ldx / w, .18f) * .78f + f.Fbm(x, y, .5f, 3, 229) * .08f, i, j, .4f);
            }
            return Col.Clear;
        });
        Kit.Edge(stone, -1, 0, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("e0d0a4"), d == 0 ? .3f : .12f));
        // Vines on the pillars and dust at their feet.
        var green = c.Blank();
        var leaves = new Ramp("1a2620", "24342a", "324434", "44583e", "5c704a", "7a8a5a");
        foreach (var (vx, top, len) in new[] { (2.3f, 7.2f, 6f), (3f, 7.1f, 3.6f), (24.3f, 7.6f, 5f), (29.5f, 7.5f, 7.6f), (25.6f, 8.2f, 2.4f) })
            Kit.Vine(green, vx, top, len, Col.Hex("2c3a2c"), leaves, (int)(vx * 23), .2f, 1.6f);
        Kit.LeafClump(green, 7.4f, 3.1f, .7f, .35f, 70, leaves, 231, .26f, .2f);
        Kit.LeafClump(green, 16.6f, 1.2f, .5f, .25f, 40, leaves, 233, .24f, .2f);
        var sand = c.Blank();
        sand.Paint(0, c.Period, -9.6f, -2.4f, (x, y, i, j) =>
        {
            float top = -3.9f + f.Fbm(x, 0, 2.5f, 3, 235) * .7f;
            return y < top ? Kit.Mix(Col.Hex("a89470"), Col.Hex("5a503c"), M.InvLerp(top, top - 1.6f, y), i, j) : Col.Clear;
        });
        Kit.Fog(stone, y => Col.Hex("2a3634"), y => .1f + M.Smooth(-3, -9, y) * .4f);
        c.Over(stone);
        c.Over(green);
        c.Over(sand);
        Kit.Shafts(c, -24, 3.6f, .26f, Dust, .12f, -9, 12, 237);
        // Spectral turquoise lights drifting among the ruins.
        foreach (var (gx, gy) in new[] { (5.2f, 1.6f), (11.4f, 4.2f), (13.6f, -1.2f), (20.2f, 2.6f), (27f, 5.2f), (33.2f, .4f) })
        {
            Kit.Glow(c, gx, gy, .9f, Spirit, .5f);
            Kit.Glow(c, gx, gy, .25f, Col.Hex("e8fff8"), 1.2f, 1, 3);
        }
        Kit.Stars(c, -6, 8, .0003f, Dust, 239, (x, y) => .6f);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Near()
    {
        var s = new LayerSpec("near", .40f, 96, 40, 2, 12.5f, -94) { Over = Col.Hex("0c1014") };
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(401);
        // Colossal ceiling slabs, cracked and stepped, with roots and dust hanging from the joints.
        var slab = c.Blank();
        float Under(float x)
        {
            int k = (int)MathF.Floor(x / (40 / 12f));
            return 11.6f - Field.Hash01(M.Mod(k, 12), 1, 403) * 1.6f - (M.Mod(k, 12) == 2 || M.Mod(k, 12) == 8 ? 1.1f : 0);
        }
        slab.Paint(0, c.Period, 2, c.Top, (x, y, i, j) =>
        {
            float under = Under(x);
            // A broken corner hangs from some slabs.
            float lx = M.Mod(x, (40 / 12f));
            bool chip = lx > 2.6f && y < under + .5f && y > under - .4f * (lx - 2.6f) / .73f + .1f;
            return y > under && !chip ? Col.White : Col.Clear;
        });
        var dark = new Ramp("0b0e12", "121619", "1a1f22", "24292a", "302f2c", "403a32");
        Kit.Inflate(slab, 14, (i, j, src, h, n) =>
        {
            float x = slab.X(i), y = slab.Y(j);
            float v = .14f + Kit.Lambert(n.X, n.Y, n.Z) * .5f + f.Fbm(x, y, .6f, 3, 405) * .12f;
            if (M.Mod(x, (40 / 12f)) < .05f) v -= .2f;
            if (y > Under(x) + .15f && y < Under(x) + .9f && Glyph(x, y, .3f, .38f, 407) > 0) v -= .12f;
            return Kit.Grain(dark.At(v, i, j, .4f), i, j, 409, .05f, .2f);
        });
        Kit.Edge(slab, 0, -1, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("8a7c5a"), d == 0 ? .45f : .2f));
        var roots = new Ramp("10160f", "182016", "222c1e", "2e3a28", "3c4a32");
        for (int k = 0; k < 18; k++) { float x = rng.R(0, 40); Kit.Vine(slab, x, Under(x) + .05f, rng.R(.6f, 3.4f), Col.Hex("1a2018"), roots, 500 + k, .12f, 2); }
        c.Over(slab);
        return new PaintedLayer(s, c);
    }
}
