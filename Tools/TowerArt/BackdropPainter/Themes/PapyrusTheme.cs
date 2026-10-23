namespace BackdropPainter;

/// <summary>Nile temple in late-afternoon sun: the winged disc of Ra over a lapis sky, pyramids and the
/// Sphinx in warm haze across the river, palm groves and a felucca, massive painted papyrus-bundle columns,
/// obelisks and jackal statues rising from the inundation among papyrus reeds, under a painted lintel.</summary>
public sealed class PapyrusTheme : ThemePainter
{
    public override string Id => "papyrus";

    internal static readonly Ramp Sand = new("3e2a18", "5c4024", "7a5a30", "9a7840", "b89454", "d0ac68", "e4c484", "f2daa4", "faecc4");
    internal static readonly Ramp FarSand = new("7a5e44", "96785a", "b09470", "c8ac84", "dcc498", "ecd8ac", "f6e8c4");
    internal static readonly Ramp Fronds = new("16381e", "1e4a2a", "2a6236", "3a7a42", "4e9450", "6aac5c", "8cc46c");
    internal static readonly Ramp Trunk = new("3a2818", "54402a", "6e5638", "8a7048", "a88c60");
    internal static readonly Ramp Bronze = new("3a2410", "5c3c18", "865a22", "b48032", "dcae52", "f4d88c");
    internal static readonly Ramp Basalt = new("16120f", "221b17", "2e2520", "3c3028", "4e3f32", "66543e");
    internal static readonly Col Lapis = Col.Hex("2a52b0"), LapisDark = Col.Hex("1c3680"), Teal = Col.Hex("2a9c8c"), Terracotta = Col.Hex("b84a26");
    internal static readonly Col Ochre = Col.Hex("dca03a"), Ink = Col.Hex("26201c"), NileGreen = Col.Hex("4e9450"), Cream = Col.Hex("ecdcb4"), CreamShade = Col.Hex("a89068");
    internal static readonly Col Haze = Col.Hex("f0d49a"), Flame = Col.Hex("ffb060");
    internal static readonly (float y, Col c)[] SkyStops = Kit.Stops((-11, "f2c878"), (-1.2f, "f6d48e"), (.8f, "f0b86c"), (2.4f, "ccb48c"), (3.8f, "7cacb8"), (5.6f, "3e80b2"), (7.6f, "28569a"), (10, "1a3878"));

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return Sky();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    // ---------------------------------------------------------------- shared motifs

    /// <summary>Hieroglyph cells: returns true inside a sign and the sign kind (0..9) for colouring.</summary>
    internal static bool Glyph(float x, float y, float cw, float ch, int seed, out int kind)
    {
        int gx = (int)MathF.Floor(x / cw), gy = (int)MathF.Floor(y / ch);
        float u = M.Fract(x / cw) * 2 - 1, v = M.Fract(y / ch) * 2 - 1;
        uint h = Field.Hash(gx, gy, seed);
        kind = (int)(h % 10);
        float r2 = u * u + v * v;
        switch (kind)
        {
            case 0: return r2 < .5f && r2 > .2f;                                                            // shen ring
            case 1: return (MathF.Abs(u) < .14f && v < .25f && v > -.8f) || (MathF.Abs(v - .1f) < .11f && MathF.Abs(u) < .55f)
                        || (u * u + (v - .5f) * (v - .5f) < .11f && u * u + (v - .5f) * (v - .5f) > .03f);        // ankh
            case 2: return MathF.Abs(u) < .78f && (MathF.Abs(v - .3f - MathF.Abs(M.Fract(u * 2.5f) - .5f) * .5f) < .12f
                        || MathF.Abs(v + .35f - MathF.Abs(M.Fract(u * 2.5f) - .5f) * .5f) < .12f);                // water
            case 3: return MathF.Abs(u) < .62f && v > -.65f && v < -.65f + (1 - MathF.Abs(u) / .62f) * 1.4f;    // pyramid
            case 4: return (u * u) / .6f + (v * v) / .14f < 1 && !(u * u + v * v < .05f);                        // eye
            case 5: return MathF.Abs(u + .1f) < .38f && v > -.7f && v < .5f && !(u > .05f && v > .05f);          // bird block
            case 6: return (u * u) / .1f + (v * v) / .62f < 1;                                                   // reed leaf
            case 7: return r2 < .36f;                                                                           // sun disc
            case 8: return MathF.Abs(u) < .24f && v > -.75f && v < .75f - MathF.Max(0, u) * 1.2f;                // feather
            default: return MathF.Abs(u) < .75f && MathF.Abs(v - MathF.Sin(u * 4.2f) * .35f) < .14f;              // serpent
        }
    }

    internal static Col GlyphCol(int kind, uint id)
    {
        Col c = kind switch
        {
            0 => Ochre, 1 => Teal, 2 => Lapis, 3 => Terracotta, 4 => Ink,
            5 => Lapis, 6 => NileGreen, 7 => Terracotta, 8 => Teal, _ => Ochre,
        };
        return (id & 3) == 0 ? Col.Lerp(c, Ink, .25f) : c;
    }

    /// <summary>A painted register: cream plaster with two rows of coloured signs between lapis and terracotta rules.
    /// yy is the offset from the register's bottom; returns false above the register.</summary>
    internal static bool Register(float xu, float yy, float height, float light, int seed, int i, int j, out Col col)
    {
        col = Col.Clear;
        if (yy < 0 || yy >= height) return false;
        float rule = height * .07f;
        Col bg = Col.Lerp(CreamShade, Cream, light);
        if (yy < rule || yy > height - rule) col = Col.Lerp(Col.Lerp(LapisDark, Lapis, light), bg, .1f);
        else if (yy < rule * 1.8f || yy > height - rule * 1.8f) col = Col.Lerp(Col.Lerp(Col.Hex("7a3018"), Terracotta, light), bg, .1f);
        else
        {
            float inner = height - rule * 3.6f, ch = inner / 2, cw = ch * .78f;
            float gy = yy - rule * 1.8f;
            if (Glyph(xu, gy, cw, ch, seed, out int kind))
            {
                uint id = Field.Hash((int)MathF.Floor(xu / cw), (int)MathF.Floor(gy / ch), seed + 1);
                col = GlyphCol(kind, id) * (.55f + .45f * light);
            }
            else col = bg;
        }
        return true;
    }

    /// <summary>The winged disc of Ra: a red-gold sun with two long feathered wings and a pair of uraei.</summary>
    static void WingedSun(Canvas p, float cx, float cy, float r, float span, Col core, Col rim, Col outline, Col[] rows, float tone)
    {
        var f = p.F;
        p.Paint(cx - span - .2f, cx + span + .2f, cy - r * 1.9f, cy + r * .9f, (x, y, i, j) =>
        {
            float dx = f.Dx(x, cx), ax = MathF.Abs(dx), dy = y - cy;
            float d = MathF.Sqrt(dx * dx + dy * dy) / r;
            if (d < 1)
            {
                if (d > .9f) return outline;
                return Kit.Mix(core, rim, d * d * 1.1f, i, j, 4) * tone;
            }
            // Uraei: two cobras hang from the disc, hoods at the top.
            foreach (float side in new[] { -1f, 1f })
            {
                float ux = dx - side * r * 1.05f, uy = cy - r * .15f - y;
                if (uy > 0 && uy < r * 1.3f)
                {
                    float wig = MathF.Sin(uy / r * 7) * r * .08f * side, hood = uy < r * .45f ? r * .13f : r * .06f;
                    if (MathF.Abs(ux - wig) < hood) return uy < r * .45f && MathF.Abs(ux - wig) < hood * .5f ? rows[1] * tone : outline;
                }
            }
            if (ax < r * 1.02f || ax > span) return Col.Clear;
            float t = (ax - r * 1.02f) / (span - r * 1.02f);
            float top = cy + r * .62f - r * .28f * t, bot = cy - r * .78f + r * 1.02f * MathF.Pow(t, 1.5f);
            if (y > top || y < bot) return Col.Clear;
            float v = (y - bot) / (top - bot);
            float edge = 1.3f / p.Ppu / (top - bot);
            if (v < edge || v > 1 - edge) return outline;
            int row = v > .7f ? 0 : v > .38f ? 1 : 2;
            if (MathF.Abs(v - .7f) < edge * .8f || MathF.Abs(v - .38f) < edge * .8f) return outline;
            float pitch = r * (row == 0 ? .2f : row == 1 ? .3f : .44f), off = row * r * .11f;
            float fr = M.Fract((ax + off) / pitch);
            if (fr < 1.2f / p.Ppu / pitch) return outline;
            int k = (int)MathF.Floor((ax + off) / pitch);
            Col c = row == 2 ? (k % 2 == 0 ? rows[2] : rows[3]) : rows[row];
            // Each feather is a rounded quill: lighter toward its leading edge.
            return Col.Lerp(c, Col.White, fr > .55f ? .12f : 0) * tone;
        });
    }

    /// <summary>A flock of tiny ibises in V formation.</summary>
    static void Flock(Canvas c, float x0, float y0, int n, Col col, int seed)
    {
        var rng = new Rng(seed);
        for (int k = 0; k < n; k++)
        {
            float bx = x0 + k * .32f + rng.R(-.05f, .05f), by = y0 - MathF.Abs(k - n * .5f) * .09f + rng.R(-.03f, .03f);
            int i = c.I(bx), j = c.J(by), spread = rng.Chance(.5f) ? 1 : 0;
            c.Set(i, j, col); c.Set(i - 1, j + spread, col); c.Set(i + 1, j + spread, col);
            if (spread == 1) { c.Set(i - 2, j + 1, col.WithA(.6f)); c.Set(i + 2, j + 1, col.WithA(.6f)); }
        }
    }

    internal static bool Tri(float ax, float ay, float bx, float by, float cx, float cy, float x, float y)
    {
        float d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by), d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy), d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    // ---------------------------------------------------------------- planes

    PaintedLayer Sky()
    {
        var s = new LayerSpec("sky", .95f, 48, 30, -11, 21, -97) { Under = SkyStops[0].c, Over = SkyStops[^1].c };
        var c = s.NewCanvas(); var f = c.F;
        c.PaintAll((x, y, i, j) => Kit.Gradient(SkyStops, y, 4, 1, i, j));
        // Glare low on the sun's side of the sky.
        Kit.Glow(c, 14, -.6f, 9.5f, Col.Hex("fff4d4"), .28f, .4f);
        // Heat haze: wavering pale bands above the horizon.
        c.Paint(0, c.Period, -1.5f, 3.6f, (x, y, i, j) =>
        {
            float n = f.N2(x + f.N(x, y, 4, 3) * .7f, y, 5, .13f, 5);
            float a = M.Smooth(.15f, .7f, n) * M.Smooth(3.6f, .4f, y) * .2f;
            float q = MathF.Floor(a * 4 + Dither.Bayer(i, j)) / 4;
            return q <= 0 ? Col.Clear : Col.Hex("fff0c8").WithA(q);
        });
        // Thin cirrus caught by the low sun.
        c.Paint(0, c.Period, 2.4f, 7.2f, (x, y, i, j) =>
        {
            float n = f.Fbm2(x, y, 7, .32f, 3, 11);
            float a = M.Smooth(.28f, .64f, n) * .42f * M.Smooth(2.4f, 3.4f, y) * M.Smooth(7.2f, 5.8f, y);
            float q = MathF.Floor(a * 5 + Dither.Bayer(i, j)) / 5;
            return q <= 0 ? Col.Clear : Col.Lerp(Col.Hex("f4b878"), Col.Hex("f8dcb0"), M.InvLerp(2.4f, 7, y)).WithA(q);
        });
        // The winged disc of Ra, glowing high in the lapis.
        Kit.Glow(c, 14, 5.4f, 3.6f, Col.Hex("f0a050"), .38f);
        Kit.Glow(c, 14, 5.4f, 1.2f, Col.Hex("ffd080"), .5f, 1, 4);
        WingedSun(c, 14, 5.4f, .55f, 4.4f, Col.Hex("f05828"), Col.Hex("f8c860"), Col.Hex("5a2410"),
            new[] { Lapis, Ochre, Terracotta, Teal }, 1);
        // Far dust-haze bank hugging the horizon.
        c.Paint(0, c.Period, -11, .4f, (x, y, i, j) =>
        {
            float a = M.Smooth(.4f, -2.5f, y) * (.4f + f.Fbm(x, y * 2, 3, 3, 13) * .3f);
            float q = MathF.Floor(a * 5 + Dither.Bayer(i, j)) / 5;
            return q <= 0 ? Col.Clear : Col.Hex("f6dca0").WithA(q);
        });
        return new PaintedLayer(s, c);
    }

    PaintedLayer Far()
    {
        const float W = -.9f;
        var s = new LayerSpec("far", .85f, 64, 32, -11, 21, -96) { Over = SkyStops[^1].c };
        var c = s.NewCanvas(); var f = c.F;
        var waterStops = Kit.Stops((-11, "1c5064"), (-5, "24687a"), (-2.4f, "3a8c8c"), (W, "72b4a4"));
        c.Paint(0, c.Period, -11, W, (x, y, i, j) => Kit.Gradient(waterStops, y, 4, 1, i, j));

        // The Giza plateau: three pyramids and the Sphinx, lit from the left, dissolving in haze.
        var back = c.Blank();
        float plateau = -.35f;
        foreach (var (px, half, h) in new[] { (9.6f, 4.5f, 4.15f), (15.9f, 3.4f, 3.1f), (20f, 1.5f, 1.4f) })
            back.Paint(px - half - .1f, px + half + .1f, plateau, plateau + h + .1f, (x, y, i, j) =>
            {
                float dx = x - px, t = (y - plateau) / h, hw = half * (1 - t);
                if (t < 0 || t > 1 || MathF.Abs(dx) > hw) return Col.Clear;
                float v = dx < 0 ? .8f - t * .06f : .4f + t * .05f;
                if (M.Mod(y, .14f) < .022f) v -= .06f;
                if (MathF.Abs(dx) < .03f) v += .1f;
                return FarSand.At(v + f.N(x, y, .5f, 41) * .04f, i, j, .4f);
            });
        float sx = 26.9f;
        back.Paint(sx - 3.8f, sx + 2.4f, plateau, plateau + 2.9f, (x, y, i, j) =>
        {
            float dx = x - sx, h = y - plateau, hx = dx + 2.05f;
            bool body = dx > -2.3f && dx < 2.1f && h < 1.25f - MathF.Max(0, dx - 1.1f) * .22f;
            bool paws = dx > -3.6f && dx <= -2.3f && h < .5f - MathF.Max(0, -dx - 3.3f) * .9f;
            bool chest = dx > -2.65f && dx < -1.55f && h >= 1.25f && h < 1.6f;
            bool head = MathF.Abs(hx) < .4f && h >= 1.6f && h < 2.5f;
            bool nemes = h >= 1.6f && h < 2.62f && hx > -.42f && hx < .42f + (2.62f - h) * .55f && !(h > 2.5f && MathF.Abs(hx) > .3f + (2.62f - h) * 2);
            if (!(body || paws || chest || head || nemes)) return Col.Clear;
            float v = .6f + (dx < -1.2f ? .12f : 0) - (dx > 1 ? .08f : 0);
            if (head) { v = .56f; if (MathF.Abs(h - 2.2f) < .03f && hx > -.3f && hx < -.08f) v -= .3f; if (hx > -.08f && hx < .02f && h > 1.85f && h < 2.15f) v -= .12f; }
            if (nemes && !head) { v = .66f; if (M.Mod(h, .17f) < .06f) v -= .16f; }
            if (paws && M.Mod(dx, .33f) < .04f) v -= .1f;
            return FarSand.At(v + f.N(x, y, .3f, 43) * .05f, i, j, .4f);
        });
        // Dunes roll across the plateau's foot.
        back.Paint(0, c.Period, plateau - .6f, 1.8f, (x, y, i, j) =>
        {
            float Top(float xx) => -.05f + f.Fbm(xx, 0, 5, 3, 45) * .5f + MathF.Max(0, f.N(xx, 0, 9, 47)) * .7f;
            float top = Top(x);
            if (y > top) return Col.Clear;
            float slope = (Top(x + .2f) - Top(x - .2f)) / .4f;
            float v = .62f + slope * .9f - (top - y) * .12f;
            if (top - y < .03f) v += .12f;
            return FarSand.At(v + f.N(x, y, .4f, 49) * .04f, i, j, .4f);
        });
        Kit.Fog(back, y => Kit.Smooth(SkyStops, y), y => .46f + M.Smooth(0, 5, y) * .12f);

        // The far bank: a sand shore, palm groves and a felucca under sail.
        var shore = c.Blank();
        shore.Paint(0, c.Period, W - .2f, 0, (x, y, i, j) =>
        {
            float top = -.42f + f.Fbm(x, 0, 2.4f, 3, 51) * .12f;
            if (y > top) return Col.Clear;
            float t = M.InvLerp(top, W, y);
            if (y < W + .08f) return Kit.Mix(Col.Hex("8a7a50"), Col.Hex("6a6a48"), t, i, j);
            return Kit.Mix(FarSand[5], FarSand[3], t * .8f + f.N(x, y, .3f, 53) * .2f, i, j);
        });
        var rng = new Rng(55);
        foreach (var (gx0, gx1, n) in new[] { (.8f, 6.4f, 7), (12.4f, 14.6f, 3), (27.6f, 31.6f, 5), (22f, 22.1f, 1) })
            for (int k = 0; k < n; k++)
            {
                float x = M.Lerp(gx0, gx1, n == 1 ? .5f : (k + rng.R(.2f, .8f)) / n);
                Kit.Palm(shore, x, -.55f + rng.R(-.1f, .1f), rng.R(1.9f, 3.3f), rng.R(-.22f, .22f), Trunk, Fronds, 100 + k * 7 + (int)gx0);
            }
        // Felucca: dark hull, tall mast and a cream lateen sail.
        float bx = 23.4f;
        shore.Paint(bx - 1.3f, bx + 1.3f, W - .12f, W + 2.4f, (x, y, i, j) =>
        {
            float dx = x - bx;
            float deck = W + .2f + MathF.Max(0, MathF.Abs(dx) - .55f) * .4f, keel = W - .08f + dx * dx * .22f;
            if (MathF.Abs(dx) < 1.02f && y < deck && y > keel) return y > deck - .045f ? Col.Hex("e8d8b0") : y > deck - .09f ? Col.Hex("a04830") : Col.Hex("3a2818");
            if (MathF.Abs(dx + .12f) < .022f && y >= deck - .05f && y < W + 2.25f) return Col.Hex("2a1c10");
            float ax = bx + .02f, ay = W + 2.2f, lx = bx - .98f, ly = W + .5f, rx = bx + .34f, ry = W + .32f;
            if (Tri(ax, ay, lx, ly, rx, ry, x, y))
            {
                // Yard along the leading edge, a soft fold down the belly.
                float ex = lx - ax, ey = ly - ay, t = M.Clamp01(((x - ax) * ex + (y - ay) * ey) / (ex * ex + ey * ey));
                float ddx = x - (ax + ex * t), ddy = y - (ay + ey * t);
                if (ddx * ddx + ddy * ddy < .0007f) return Col.Hex("4a3018");
                return x > bx - .25f + (y - W) * .12f ? Col.Hex("d8c498") : Col.Hex("f6ecd0");
            }
            return Col.Clear;
        });
        Kit.Fog(shore, y => Kit.Smooth(SkyStops, y), y => .26f);
        c.Over(back);
        c.Over(shore);
        Kit.Reflect(c, W, 2.7f, Col.Hex("3c8890"), .55f, 57);
        // Sun glitter on the river, dense near the far bank.
        c.Paint(0, c.Period, c.Bottom, W, (x, y, i, j) =>
        {
            float d = W - y;
            float n = f.Fbm2(x, y, .8f + d * .25f, .04f + d * .02f, 2, 59);
            float a = M.Smooth(.4f, .62f, n) * .6f * (1 - M.Clamp01(d / 3.4f));
            return a > .12f ? Col.Hex("fff0c0").WithA(Math.Min(1, a)) : Col.Clear;
        });
        c.Paint(0, c.Period, W - 2 / c.Ppu, W + .5f / c.Ppu, (x, y, i, j) => Col.Hex("f8e8b8").WithA(.5f));
        Flock(c, 4.5f, 3.6f, 7, Col.Hex("5a4a3c"), 61);
        Flock(c, 24.8f, 4.9f, 5, Col.Hex("5a4a3c"), 63);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Mid()
    {
        const float WL = -2.1f;
        var s = new LayerSpec("mid", .65f, 96, 36, -9.5f, 22, -95);
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(201);
        // The inundation: shallow turquoise water across the foot of the colonnade.
        var waterStops = Kit.Stops((-9.5f, "1c5658"), (-4.5f, "246e68"), (WL, "4e9c8c"));
        c.Paint(0, c.Period, -9.5f, WL, (x, y, i, j) => Kit.Gradient(waterStops, y, 4, 1, i, j));

        // Obelisks stand a step behind the colonnade.
        var back = c.Blank();
        foreach (var ox in new[] { 5.5f, 24.4f }) PapyrusKit.Obelisk(back, ox, -2.35f, 5.5f, (int)(ox * 7));
        Kit.Fog(back, y => Haze, y => .3f);

        // The colonnade: bundle columns under a painted architrave with scarab panels.
        var front = c.Blank();
        float[] cols = { 2.6f, 8.4f, 15.2f, 21f, 27.8f, 33.4f };
        foreach (var cx in cols) PapyrusKit.Column(front, cx, -2.35f, 4.75f, .7f, (int)(cx * 13));
        float[] bays = { 5.5f, 11.8f, 18.1f, 24.4f, 30.6f, 0f };
        front.Paint(0, c.Period, 6.8f, 8.3f, (x, y, i, j) =>
        {
            if (y < 6.9f) return Sand.At(.28f, i, j, .3f);
            if (y < 7.95f)
            {
                foreach (var bx in bays)
                    if (MathF.Abs(f.Dx(x, bx)) < .62f)
                    {
                        float ax = MathF.Abs(f.Dx(x, bx));
                        if (ax > .58f || y < 6.94f || y > 7.91f) return Ink;
                        return Kit.Grain(Kit.Mix(Col.Hex("c89c48"), Col.Hex("e8c070"), .5f + f.N(x, y, .3f, 303) * .5f, i, j), i, j, 305, .04f, .1f);
                    }
                if (Register(x, y - 6.9f, 1.05f, .85f, 301, i, j, out Col col)) return col;
                return Col.Clear;
            }
            // Cavetto cornice painted with upright palm leaves.
            float t = (y - 7.95f) / .35f;
            if (t > .88f) return Sand.At(.9f, i, j, .3f);
            int k = (int)MathF.Floor(x / .22f); float u = M.Fract(x / .22f);
            Col c0 = (M.Mod(k, 4)) switch { 0 => Lapis, 1 => Ochre, 2 => Teal, _ => Terracotta };
            if (u < .12f || t < .08f) return Ink;
            return c0 * (.72f + t * .28f);
        });
        foreach (var bx in bays) PapyrusKit.Scarab(front, bx, 7.3f, .23f);
        // Seated jackals guard the bays between the columns.
        var guard = c.Blank();
        PapyrusKit.Anubis(guard, 11.8f, -2.35f, 311, 1);
        PapyrusKit.Anubis(guard, 30.6f, -2.35f, 313, -1);
        Kit.Edge(guard, -1, 0, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("d8a060"), d == 0 ? .5f : .2f));
        Kit.Edge(guard, 0, 1, 1, (col, d, i, j) => Col.Lerp(col, Col.Hex("e8b870"), .4f));
        front.Over(guard);

        // Papyrus thickets grow from the water in clumps.
        var reeds = c.Blank();
        foreach (var (rx0, rx1, n) in new[] { (.2f, 1.8f, 9), (3.6f, 5f, 7), (9.6f, 11f, 8), (13f, 14.4f, 6), (16.4f, 17.6f, 5), (19.2f, 20.4f, 7), (22.4f, 23.6f, 6), (25.6f, 27f, 9), (29f, 30f, 4), (31.6f, 32.8f, 7), (34.4f, 35.6f, 6) })
            for (int k = 0; k < n; k++) PapyrusKit.Papyrus(reeds, rng.R(rx0, rx1), WL - .15f, rng.R(1.3f, 2.9f), rng.R(-.12f, .12f), 400 + k + (int)(rx0 * 10));

        Kit.Fog(front, y => Haze, y => .1f + M.Smooth(2.5f, -2.5f, y) * .16f);
        Kit.Fog(reeds, y => Haze, y => .18f);
        c.Over(back);
        c.Over(front);
        c.Over(reeds);
        Kit.Reflect(c, WL, 1.6f, Col.Hex("2a6e68"), .5f, 203);
        c.Paint(0, c.Period, -3.8f, WL, (x, y, i, j) =>
        {
            float d = WL - y;
            float n = f.Fbm2(x, y, .7f + d * .3f, .04f + d * .03f, 2, 205);
            float a = M.Smooth(.42f, .62f, n) * .5f * (1 - M.Clamp01(d / 1.6f));
            return a > .12f ? Col.Hex("e8f4d8").WithA(Math.Min(1, a)) : Col.Clear;
        });
        // Bronze lamps hang from the architrave in the bays without obelisks.
        foreach (var lx in new[] { 11.8f, 18.1f, 30.6f, 0f })
        {
            PapyrusKit.Lamp(c, lx, 6.85f, 5.55f);
            Kit.Glow(c, lx, 5.7f, 1.5f, Flame, .4f);
            Kit.Glow(c, lx, 5.72f, .35f, Col.Hex("fff0c0"), .8f, 1, 3);
        }
        Kit.Shafts(c, -22, 3.8f, .24f, Col.Hex("ffe8b0"), .09f, -2, 11, 207, (x, y) => .4f + M.Smooth(2, 7, y) * .6f);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Near()
    {
        var s = new LayerSpec("near", .40f, 96, 40, 2, 12.5f, -94) { Over = Col.Hex("1a1410") };
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(401);
        var dark = new Ramp("1a1410", "2a2016", "3c2e1e", "4e3c26", "62502e", "76643a", "8a7848", "a08c58");
        const float under = 9.9f;
        float[] suns = { 10f, 30f };
        var lintel = c.Blank();
        lintel.Paint(0, c.Period, under, c.Top, (x, y, i, j) =>
        {
            float h = y - under;
            if (h < .16f) return dark.At(.04f + h * 1.6f, i, j, .3f);
            if (h < .42f || (h >= 1.72f && h < 1.96f))
            {
                // Torus mouldings bound with diagonal lashings of lapis and gold.
                float t = (h < .42f ? h - .16f : h - 1.72f) / .25f, light = MathF.Sqrt(Math.Max(0, 1 - (t * 2 - 1) * (t * 2 - 1)));
                bool band = M.Fract((x + t * .3f) / .6f) < .4f;
                return (band ? LapisDark : Col.Hex("b08a40")) * (.3f + .55f * light);
            }
            if (h < 1.72f) return Kit.Grain(dark.At(.46f + f.Fbm(x, y, .5f, 3, 403) * .08f, i, j, .4f), i, j, 404, .04f, .12f);
            if (h < 2.95f)
            {
                // Cavetto cornice: upright painted palm leaves under a pale lip.
                float t = (h - 1.96f) / .99f;
                if (t > .9f) return dark.At(.78f, i, j, .3f);
                int k = (int)MathF.Floor(x / .42f); float u = M.Fract(x / .42f);
                Col c0 = M.Mod(k, 4) switch { 0 => Lapis, 1 => Ochre, 2 => Teal, _ => Terracotta };
                float lw = .46f * (1 - t * .6f);
                if (MathF.Abs(u - .5f) > lw || t < .05f) return dark[1];
                return c0 * (.38f + t * .32f + (MathF.Abs(u - .5f) < .06f ? .12f : 0));
            }
            bool mortar = Kit.Masonry(f, x, y, 1.6f, .55f, 1.5f / 96, 405, out float sh, out _, out _, out _);
            return dark.At(.2f + (sh - .5f) * .1f - (mortar ? .12f : 0) + f.Fbm(x, y, .5f, 3, 407) * .06f, i, j, .4f);
        });
        // Winged suns carved and painted on the frieze, with cartouche rows between them.
        foreach (var wx in suns)
            WingedSun(lintel, wx, under + 1.02f, .4f, 4.7f, Col.Hex("c04a22"), Col.Hex("d8a040"), Col.Hex("1a1410"),
                new[] { Col.Hex("2a4a98"), Col.Hex("c09040"), Col.Hex("a04020"), Col.Hex("22786c") }, .85f);
        lintel.Paint(0, c.Period, under + .5f, under + 1.55f, (x, y, i, j) =>
        {
            foreach (var wx in suns) if (MathF.Abs(f.Dx(x, wx)) < 5) return Col.Clear;
            float cw = 1f, u = M.Fract(x / cw), gy = y - under - .5f;
            if (u < .05f || gy < .05f || gy > 1) return dark[1];
            if (Glyph(x, gy, cw, 1.05f, 409, out int kind)) return GlyphCol(kind, Field.Hash((int)MathF.Floor(x / cw), 0, 411)) * .72f;
            return Col.Clear;
        });
        // Palm fronds hung from the moulding for the festival.
        var leaves = c.Blank();
        var frond = new Ramp("0e1a10", "16281a", "1e3822", "2a4a2c", "3a5e36", "4e7444");
        for (int k = 0; k < 18; k++)
        {
            float x = k * 40 / 18f + rng.R(-.8f, .8f);
            Frond(leaves, x, under + .3f, rng.R(1.6f, 3.4f), rng.Chance(.5f) ? 1 : -1, frond, 500 + k);
        }
        Kit.Edge(leaves, -1, 0, 1, (col, d, i, j) => Col.Lerp(col, Col.Hex("8a8a50"), .3f));
        c.Over(lintel);
        c.Over(leaves);
        return new PaintedLayer(s, c);
    }

    /// <summary>A hanging palm frond: a curving rib with pairs of drooping leaflets.</summary>
    static void Frond(Canvas p, float ax, float ay, float len, float side, Ramp ramp, int seed)
    {
        var rng = new Rng(seed);
        float bend = rng.R(.2f, .45f), lf = .22f + len * .05f, w = .028f;
        float cs = MathF.Cos(.95f), sn = MathF.Sin(.95f);
        p.Paint(ax - len * .6f - lf, ax + len * .6f + lf, ay - len - lf, ay + .05f, (x, y, i, j) =>
        {
            float dx = x - ax, dy = y - ay;
            for (int s2 = 0; s2 <= 30; s2++)
            {
                float t = s2 / 30f, fx = side * len * bend * t * t, fy = -len * t;
                float tx = side * len * bend * 2 * t, ty = -len; float tl = MathF.Sqrt(tx * tx + ty * ty); tx /= tl; ty /= tl;
                float rx = dx - fx, ry = dy - fy;
                if (rx * rx + ry * ry < w * w * 1.6f) return ramp.At(.3f + t * .2f, i, j, .3f);
                if (t < .08f) continue;
                float scale = 1 - t * .45f;
                foreach (float sg in new[] { -1f, 1f })
                {
                    float d1x = tx * cs - sg * ty * sn, d1y = sg * tx * sn + ty * cs;
                    float a = rx * d1x + ry * d1y, b = MathF.Abs(rx * d1y - ry * d1x);
                    if (a > 0 && a < lf * scale && b < w * (1 - a / (lf * scale)) + .008f)
                        return ramp.At(.25f + t * .25f + (sg < 0 ? .18f : 0) + a / lf * .15f, i, j, .3f);
                }
            }
            return Col.Clear;
        });
    }
}
