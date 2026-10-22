namespace BackdropPainter;

/// <summary>Crystal cavern: a slate cathedral of natural columns, tiered strata, violet and cyan
/// geodes and a still subterranean pool.</summary>
public sealed class RockTheme : ThemePainter
{
    public override string Id => "rock";

    static readonly Ramp Slate = new("0c101c", "131a2b", "1b2439", "252f48", "323d59", "434e6d", "5a6686", "7a84a2");
    static readonly Ramp Violet = new("1c1438", "35246a", "53399a", "7458c4", "9a7ee2", "c4acf6", "f1e8ff");
    static readonly Ramp Cyan = new("0f2a36", "1a4c5c", "277684", "3ea2ac", "6ccccc", "aef0e8", "effffb");
    static readonly Col Mist = Col.Hex("3b4f78"), Glint = Col.Hex("8ee6e0"), Lilac = Col.Hex("a68ee0");
    static readonly (float y, Col c)[] SkyStops = Kit.Stops((-11, "2e5a66"), (-7.5f, "24405a"), (-4, "1d2c48"), (0, "17203a"), (4, "11182e"), (10, "090d1d"));

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return Sky();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    /// <summary>Slate with warped strata bands, fine cracks and an inflated relief normal.</summary>
    static Col Rock(Field f, float x, float y, int i, int j, V3 n, float bias, float contrast, int seed)
    {
        float warp = f.Fbm(x, y, 3, 3, seed) * .6f + x * .04f;
        float band = (y + warp) / .55f + f.N(x, y, 2.5f, seed + 1) * .6f;
        float tier = Field.Hash01((int)MathF.Floor(band), 11, seed) - .5f;
        float edge = M.Fract(band);
        bool broken = f.N(x, y * 3, .9f, seed + 2) > .15f;
        float v = bias + Kit.Lambert(n.X, n.Y, n.Z) * contrast + tier * .14f;
        if (!broken) { if (edge < .05f) v -= .16f; else if (edge < .1f) v += .08f; }
        v += f.Fbm2(x, y, .5f, 1.6f, 3, seed + 3) * .1f;
        if (f.Ridge(x, y, 1.6f, 3, seed + 5) > .92f) v -= .18f;
        return Kit.Grain(Slate.At(v, i, j, .3f), i, j, seed + 7, .04f, .15f);
    }

    PaintedLayer Sky()
    {
        var s = new LayerSpec("sky", .95f, 48, 30, -11, 21, -97) { Under = SkyStops[0].c, Over = SkyStops[^1].c };
        var c = s.NewCanvas(); var f = c.F;
        c.PaintAll((x, y, i, j) => Kit.Gradient(SkyStops, y, 4, 1, i, j));
        Kit.Glow(c, 9, 3.5f, 9, Lilac, .22f, .8f);
        Kit.Glow(c, 23, -5, 10, Glint, .2f, .5f);
        // Distant natural columns dissolving into the mist.
        c.Paint(0, c.Period, -11, 10, (x, y, i, j) =>
        {
            float sp = f.Snap(5f), u = M.Mod(x, sp) - sp * .5f;
            float t = M.InvLerp(-7, 8, y), hw = .45f + (MathF.Pow(1 - t, 3) + MathF.Pow(t, 3)) * 2.2f + f.N(x, y, 1.2f, 13) * .25f;
            bool roof = y > 6.2f + f.Fbm(x, 0, 6, 3, 15) * 1.2f;
            if (!roof && MathF.Abs(u) > hw) return Col.Clear;
            var bg = Kit.Gradient(SkyStops, y, 4, 1, i, j);
            return Col.Lerp(bg, Col.Hex("2b3858"), .3f);
        });
        // Drifting mist banks.
        c.Paint(0, c.Period, -11, 6, (x, y, i, j) =>
        {
            float n = f.Fbm(x * .5f, y * 2.2f, 5, 4, 21);
            float a = M.Smooth(-.1f, .5f, n) * M.Smooth(6, -2, y) * .45f;
            float q = MathF.Floor(a * 5 + Dither.Bayer(i, j)) / 5;
            return q <= 0 ? Col.Clear : Mist.WithA(q);
        });
        return new PaintedLayer(s, c);
    }

    PaintedLayer Far()
    {
        var s = new LayerSpec("far", .85f, 64, 32, -11, 21, -96) { Over = Col.Hex("0d1120") };
        var c = s.NewCanvas(); var f = c.F;
        float[] cols = { 3, 12.5f, 23.5f };
        float Nearest(float x) { float d = 99; foreach (var cx in cols) d = Math.Min(d, MathF.Abs(f.Dx(x, cx))); return d; }
        float Roof(float x) => 2.8f + 5.4f * MathF.Pow(M.Clamp01(Nearest(x) / 5.2f), .55f) + f.Fbm(x, 1, 2.5f, 4, 31) * .5f;
        float Pool = -6.6f;

        var rock = c.Blank();
        rock.Paint(0, c.Period, Pool - .2f, c.Top, (x, y, i, j) =>
        {
            if (y > Roof(x)) return Col.White;
            foreach (var cx in cols)
            {
                float dx = f.Dx(x, cx), t = M.InvLerp(Pool, Roof(cx) + 1, y);
                float hw = .6f + (MathF.Pow(1 - t, 4) * 1.6f + MathF.Pow(t, 2.5f) * 2.4f) + f.N(x, y, .8f, 33) * .18f;
                if (MathF.Abs(dx) < hw) return Col.White;
            }
            // Rubble mounds and stalagmites along the shore.
            float mound = Pool + .5f + MathF.Max(0, f.Fbm(x, 4, 3, 4, 35)) * 2.2f;
            if (y < mound) return Col.White;
            float sx = f.Snap(.9f), u = M.Mod(x, sx) - sx * .5f; int k = (int)MathF.Floor(x / sx);
            float hh = Field.Hash01(M.Mod(k, (int)MathF.Round(c.Period / sx)), 2, 37);
            if (hh > .55f) { float len = (hh - .55f) * 6, tt = (y - Pool) / len; if (tt < 1 && MathF.Abs(u) < .22f * (1 - tt)) return Col.White; }
            return Col.Clear;
        });
        Kit.Inflate(rock, 40, (i, j, src, h, n) => Rock(f, rock.X(i), rock.Y(j), i, j, n, .1f, .62f, 41),
            (i, j) => f.Fbm(rock.X(i), rock.Y(j), 2, 3, 43) * 18 + f.Fbm(rock.X(i), rock.Y(j), .5f, 2, 44) * 3);
        // Geodes glitter in the far walls.
        var gems = c.Blank(); var rng = new Rng(45);
        var glows = new List<(float x, float y, Col c)>();
        for (int k = 0; k < 16; k++)
        {
            float gx = rng.R(0, 32), gy = rng.R(-5, 8);
            if (!rock.Solid(rock.I(gx), rock.J(gy)) || !rock.Solid(rock.I(gx), rock.J(gy - .5f))) continue;
            bool violet = k % 2 == 0;
            var at = Kit.CrystalCluster(gems, gx, gy - .2f, rng.R(.8f, 1.4f), rng.R(-.4f, .4f), .6f, 6, violet ? Violet : Cyan, 47 + k);
            glows.Add((at.x, at.y, violet ? Lilac : Glint));
        }
        Kit.Edge(gems, 1, 0, 1, (col, d, i, j) => Col.Lerp(col, Col.Black, .35f));
        rock.Over(gems);
        foreach (var g in glows) Kit.Light(rock, g.x, g.y, 2.4f, g.c, .4f);
        Kit.Edge(rock, 0, 1, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("6c76a0"), d == 0 ? .35f : .15f));
        Kit.Fog(rock, y => Kit.Smooth(SkyStops, y), y => .5f + M.Smooth(-1, -6.5f, y) * .25f);

        // The still pool mirrors the cavern in turquoise.
        c.Paint(0, c.Period, -11, Pool, (x, y, i, j) => Kit.Mix(Col.Hex("173040"), Col.Hex("0d1a28"), M.InvLerp(Pool, -11, y), i, j));
        c.Over(rock);
        Kit.Reflect(c, Pool, 4.4f, Col.Hex("12283a"), .55f, 49);
        c.Paint(0, c.Period, -11, Pool, (x, y, i, j) =>
        {
            float n = f.Fbm2(x, y, 1.6f, .07f, 2, 51);
            return n > .55f && Pool - y < 3 ? Glint.WithA(.55f) : Col.Clear;
        });
        foreach (var g in glows) Kit.Glow(c, g.x, g.y, 1.4f, g.c, .32f);
        Kit.Glow(c, 18, Pool, 5, Glint, .22f, .25f);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Mid()
    {
        var s = new LayerSpec("mid", .65f, 96, 36, -9.5f, 22, -95);
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(201);
        float Roof(float x) => 10.6f + f.Fbm(x, 2, 5, 4, 211) * 1.1f;
        float ColX = 5.5f;
        var rock = c.Blank();
        var spikes = new List<(float x, float y, float len, float w)>();
        foreach (var (cx, len, w) in new[] { (14f, 3.4f, 2.4f), (24.5f, 2.6f, 1.8f), (32f, 3.8f, 2.6f) })
            for (int k = 0; k < 8; k++)
            {
                float ox = k == 0 ? 0 : rng.R(-w * .8f, w * .8f);
                spikes.Add((cx + ox, Roof(cx + ox) + .3f, len * (k == 0 ? 1 : rng.R(.3f, .7f)), w * (k == 0 ? .6f : rng.R(.18f, .35f))));
            }
        rock.Paint(0, c.Period, -9.6f, c.Top, (x, y, i, j) =>
        {
            if (y > Roof(x)) return Col.White;
            // A great hourglass column fused from stalactite and stalagmite.
            float t = M.InvLerp(-9.5f, Roof(ColX), y), dx = f.Dx(x, ColX);
            float hw = .55f + MathF.Pow(1 - t, 3) * 1.5f + MathF.Pow(t, 2.4f) * 2f + f.N(x, y, .7f, 213) * .14f + MathF.Sin(y * 2.1f) * .05f;
            if (MathF.Abs(dx) < hw) return Col.White;
            // A stepped strata outcrop on the right.
            float shelf = -9.5f + 8.2f * MathF.Pow(MathF.Max(0, MathF.Sin(MathF.PI * M.InvLerp(17.5f, 30.5f, x))), .55f);
            shelf = MathF.Floor(shelf / .85f) * .85f + f.N(x, 0, .6f, 215) * .12f;
            if (x > 17.5f && x < 30.5f && y < shelf) return Col.White;
            foreach (var sp in spikes)
            {
                float d = f.Dx(x, sp.x);
                if (MathF.Abs(d) < sp.w && Kit.Spike(f, sp.x + d, y, sp.x, sp.y, sp.len, sp.w, -1, 217, out _, out _, .22f, 1.2f)) return Col.White;
            }
            foreach (var (bx, h, w) in new[] { (12.5f, 4.2f, 1.3f), (14.2f, 2.4f, .8f), (33.5f, 3.4f, 1.1f) })
                if (Kit.Spike(f, x, y, bx, -9.6f, h, w, 1, 219, out _, out _, .2f, 1)) return Col.White;
            return Col.Clear;
        });
        Kit.Inflate(rock, 72, (i, j, src, h, n) => Rock(f, rock.X(i), rock.Y(j), i, j, n, .06f, .78f, 221),
            (i, j) => f.Fbm(rock.X(i), rock.Y(j), 1.8f, 4, 223) * 30 + f.Fbm(rock.X(i), rock.Y(j), .4f, 2, 224) * 5);
        Kit.Edge(rock, -1, 1, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("8a90b8"), d == 0 ? .45f : .2f));
        // Crystal growths: violet on the outcrop, cyan from the column and hanging from the roof.
        var gems = c.Blank();
        var glows = new List<(float x, float y, Col c, float r)>();
        void Grow(float x, float y, float size, float tilt, int n, bool violet, int seed, int dir = 1)
        {
            var at = Kit.CrystalCluster(gems, x, y, size, tilt, .55f, n, violet ? Violet : Cyan, seed, dir);
            glows.Add((at.x, at.y, violet ? Lilac : Glint, size * 1.9f));
        }
        float Shelf(float x) { for (float y = 2; y > -9.4f; y -= .02f) if (rock.Solid(rock.I(x), rock.J(y))) return y; return -9.5f; }
        Grow(21.4f, Shelf(21.4f) - .15f, 2.3f, -.15f, 9, true, 231);
        Grow(19.4f, Shelf(19.4f) - .1f, 1.1f, -.5f, 5, false, 232);
        Grow(26.6f, Shelf(26.6f) - .15f, 1.7f, .25f, 7, true, 233);
        Grow(24f, Shelf(24f) - .1f, 1.1f, 0, 5, false, 235);
        Grow(29.3f, Shelf(29.3f) - .1f, .9f, .4f, 4, false, 236);
        Grow(ColX + 1.15f, -2.6f, 1.3f, 1.05f, 5, false, 237);
        Grow(ColX - 1.4f, 4.4f, 1f, -1.1f, 4, true, 238);
        Grow(14, Roof(14) - 2.9f, 1.6f, 0, 7, false, 239, -1);
        Grow(32.2f, Roof(32.2f) - 3.3f, 1.4f, .1f, 6, true, 241, -1);
        Kit.Edge(gems, 1, -1, 1, (col, d, i, j) => Col.Lerp(col, Col.Hex("0a0c18"), .45f));
        rock.Over(gems);
        Kit.Fog(rock, y => Col.Hex("2a3a5c"), y => .1f + M.Smooth(-3, -9.5f, y) * .45f);
        foreach (var g in glows) { Kit.Light(rock, g.x, g.y, g.r * 1.6f, g.c, .55f); }
        c.Over(rock);
        foreach (var g in glows) Kit.Glow(c, g.x, g.y, g.r * 1.2f, g.c, .4f);
        Kit.Stars(c, -9, 9, .00015f, Col.Hex("bfe8ff"), 243, (x, y) => .7f);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Near()
    {
        var s = new LayerSpec("near", .40f, 96, 40, 2, 12.5f, -94) { Over = Col.Hex("080b14") };
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(401);
        float Roof(float x) => 12.1f + f.Fbm(x, 2, 3.5f, 4, 81) * .7f;
        var spikes = new List<(float x, float y, float len, float w)>();
        foreach (var (cx, len, w) in new[] { (8f, 7.2f, 4.6f), (29f, 8f, 5.6f), (19f, 3.6f, 2.2f) })
        {
            spikes.Add((cx, Roof(cx) + .5f, len, w * .8f));
            for (int k = 0; k < 8; k++)
            {
                float ox = rng.R(-w * .6f, w * .6f);
                spikes.Add((cx + ox, Roof(cx + ox) + .3f, len * rng.R(.3f, .8f) * (1 - MathF.Abs(ox) / w * .8f), w * rng.R(.22f, .42f)));
            }
        }
        for (int k = 0; k < 14; k++) { float x = rng.R(0, 40); spikes.Add((x, Roof(x) + .3f, rng.R(.8f, 2.4f), rng.R(.35f, .8f))); }
        var rock = c.Blank();
        rock.Paint(0, c.Period, 2, c.Top, (x, y, i, j) =>
        {
            if (y > Roof(x)) return Col.White;
            foreach (var sp in spikes)
            {
                float dx = f.Dx(x, sp.x);
                if (MathF.Abs(dx) < sp.w && Kit.Spike(f, sp.x + dx, y, sp.x, sp.y, sp.len, sp.w, -1, 91, out _, out _, .26f, 1.15f)) return Col.White;
            }
            return Col.Clear;
        });
        var dark = new Ramp("070a12", "0c111c", "121928", "1a2336", "243048", "34425e");
        Kit.Inflate(rock, 26, (i, j, src, h, n) =>
        {
            float x = rock.X(i), y = rock.Y(j);
            float v = .12f + Kit.Lambert(n.X, n.Y, n.Z) * .55f + f.Fbm(x, y, .5f, 3, 85) * .14f;
            return Kit.Grain(dark.At(v, i, j, .5f), i, j, 87, .05f, .2f);
        }, (i, j) => f.Fbm(rock.X(i), rock.Y(j), .9f, 4, 86) * 9);
        Kit.Edge(rock, 0, -1, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("4f8a9a"), d == 0 ? .55f : .25f));
        var gems = c.Blank();
        var glows = new List<(float x, float y, Col c)>();
        foreach (var (gx, len, violet, seed) in new[] { (8.4f, 6.4f, true, 411), (29.4f, 7.2f, false, 413), (19.2f, 3.3f, true, 415) })
        {
            var at = Kit.CrystalCluster(gems, gx, Roof(gx) - len + .5f, 1.6f, 0, .5f, 7, violet ? Violet : Cyan, seed, -1);
            glows.Add((at.x, at.y, violet ? Lilac : Glint));
        }
        Kit.Edge(gems, 1, -1, 1, (col, d, i, j) => Col.Lerp(col, Col.Black, .5f));
        rock.Over(gems);
        foreach (var g in glows) Kit.Light(rock, g.x, g.y, 3.2f, g.c, .5f);
        c.Over(rock);
        foreach (var g in glows) Kit.Glow(c, g.x, g.y, 2.6f, g.c, .38f);
        return new PaintedLayer(s, c);
    }
}
