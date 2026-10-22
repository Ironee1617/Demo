namespace BackdropPainter;

/// <summary>Tidal sunken sanctuary: a colonnade half drowned in a luminous grotto lagoon, coral-crusted
/// columns, a palm islet, lilies, waterfalls and rippling reflections.</summary>
public sealed class WaterTheme : ThemePainter
{
    public override string Id => "water";
    public override bool OpenBelow => true;

    const float W = -3.3f; // lagoon surface in every plane at the reference camera
    static readonly Ramp Stone = new("142a32", "1d363e", "27444a", "345556", "466862", "5e7e74", "7c9888", "a2b8a4");
    static readonly Ramp Coral = new("4a2034", "7a3450", "b0506a", "d86e82", "f0989e", "ffc8c0");
    static readonly Ramp Fronds = new("0e2a26", "163c30", "22523a", "326a44", "4a844e", "6aa05a", "94bc6c");
    static readonly Ramp Trunk = new("231e1c", "3a3028", "524234", "6c5842", "8a7454");
    static readonly Col Foam = Col.Hex("bfeee2"), Deep = Col.Hex("0c2a3c");
    static readonly (float y, Col c)[] SkyStops = Kit.Stops((W, "4f9ea2"), (0, "2e6e7e"), (4, "1a4258"), (10, "0a1a2c"));
    static readonly (float y, Col c)[] WaterStops = Kit.Stops((-11, "0c2a3c"), (-7, "12384c"), (-4.6f, "1d5466"), (W, "3f8a92"));

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return Sky();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    static Col Water(float y, int i, int j) => Kit.Gradient(WaterStops, y, 4, 1, i, j);

    /// <summary>Rippling highlights on the open water, denser toward the horizon line.</summary>
    static void Ripples(Canvas c, int seed, float strength)
    {
        var f = c.F;
        c.Paint(0, c.Period, c.Bottom, W, (x, y, i, j) =>
        {
            float d = W - y;
            float n = f.Fbm2(x, y, .9f + d * .2f, .05f + d * .02f, 2, seed);
            float a = M.Smooth(.42f, .62f, n) * strength * (1 - M.Clamp01(d / 5));
            return a > .12f ? Foam.WithA(Math.Min(1, a)) : Col.Clear;
        });
        c.Paint(0, c.Period, W - 2 / c.Ppu, W + .5f / c.Ppu, (x, y, i, j) => Foam.WithA(.55f));
    }

    PaintedLayer Sky()
    {
        var s = new LayerSpec("sky", .95f, 48, 30, -11, 21, -97) { Under = Deep, Over = SkyStops[^1].c };
        var c = s.NewCanvas(); var f = c.F;
        c.PaintAll((x, y, i, j) => y >= W ? Kit.Gradient(SkyStops, y, 4, 1, i, j) : Water(y, i, j));
        // A luminous mouth in the grotto roof.
        Kit.Glow(c, 11, 4, 9, Col.Hex("a8eee4"), .4f, .8f);
        c.Paint(0, c.Period, 3, 10, (x, y, i, j) =>
        {
            float dx = f.Dx(x, 11), mouth = 4.6f + MathF.Abs(dx) * .55f + f.Fbm(x, 0, 2, 3, 9) * .6f;
            return y > mouth && MathF.Abs(dx) > 2 ? Col.Lerp(Kit.Gradient(SkyStops, y, 4, 1, i, j), Col.Hex("0a1824"), .55f) : Col.Clear;
        });
        Kit.Shafts(c, 28, 2.2f, .3f, Col.Hex("bff0e6"), .22f, W, 10, 11, (x, y) => M.Smooth(12, 4, MathF.Abs(f.Dx(x, 11 + (10 - y) * .45f))));
        // Faint far columns rising from the water.
        c.Paint(0, c.Period, W - .2f, 7, (x, y, i, j) =>
        {
            float sp = f.Snap(2.1f), u = MathF.Abs(M.Mod(x, sp) - sp * .5f);
            float top = 1.5f + Field.Hash01(M.Mod((int)MathF.Floor(x / sp), (int)MathF.Round(c.Period / sp)), 1, 13) * 3.5f;
            float hw = y > top - .25f ? .3f : .19f;
            return u < hw && y < top ? Col.Lerp(Kit.Gradient(SkyStops, y, 4, 1, i, j), Col.Hex("2a5e6a"), .45f) : Col.Clear;
        });
        Ripples(c, 15, .5f);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Far()
    {
        var s = new LayerSpec("far", .85f, 64, 32, -11, 21, -96) { Under = Deep, Over = Col.Hex("0a1a2a") };
        var c = s.NewCanvas(); var f = c.F;
        c.Paint(0, c.Period, -11, W, (x, y, i, j) => Water(y, i, j));
        var style = new ArcadeStyle
        {
            Span = 3.6f, PierHalf = .44f, Base = W - 1.5f, Spring = .6f, Pointed = 0, Rings = 2, RingW = .14f, PlinthH = .4f, CapH = .32f,
            Deck = 3.1f, CorniceH = .26f, Corbel = .3f, GalleryTop = 4.5f, GallerySpan = 1.8f, GalleryHalf = .44f, GallerySpring = 3.9f, Oculus = false,
            Missing = (x, y) =>
            {
                // Broken masonry steps block by block rather than melting smoothly.
                float bx = MathF.Floor(x / .45f) * .45f, by = MathF.Floor(y / .22f) * .22f;
                float edge = 1.6f + f.Fbm(bx, 0, 5, 3, 31) * 4.8f + (Field.Hash01((int)(bx * 9), (int)(by * 9), 32) - .5f) * .8f;
                return by > edge;
            }
        };
        var arcade = c.Blank();
        arcade.Paint(0, c.Period, W - 1.6f, 6, (x, y, i, j) =>
        {
            var h = ArchKit.Hit(style, f, x, y);
            if (h.Part == Part.None) return Col.Clear;
            float v = ArchKit.BaseTone(h) * .45f + Kit.Lambert(h.N.X, h.N.Y, h.N.Z) * .5f + f.Fbm(x, y, .7f, 3, 33) * .08f;
            var col = Stone.At(v, i, j, .45f);
            // Algae at the waterline and coral on the piers.
            if (y < W + .9f + f.N(x, 0, .4f, 35) * .3f) col = Kit.Mix(Col.Hex("1e3e34"), Col.Hex("33584a"), v, i, j);
            if (h.Part is Part.Pier or Part.Shaft && f.Fbm(x, y, .7f, 2, 37) * .8f + M.Smooth(-.5f, W + .3f, y) * .6f > .62f) col = Coral.At(.35f + v * .6f, i, j, .4f);
            return col;
        });
        // Seaweed hangs from the broken arches.
        var weed = c.Blank(); var rng = new Rng(39);
        for (int k = 0; k < 18; k++) Kit.Vine(weed, rng.R(0, 32), rng.R(1.4f, 2.8f), rng.R(.8f, 2.4f), Col.Hex("27503e"), Fronds, 400 + k, .1f, 1);
        arcade.Over(weed);
        Kit.Fog(arcade, y => Kit.Smooth(SkyStops, y), y => .4f);
        c.Over(arcade);
        Kit.Reflect(c, W, 3.2f, Col.Hex("2c707e"), .55f, 41);
        // Waterfalls from the grotto roof.
        foreach (var fx in new[] { 5.5f, 23.8f })
        {
            c.Paint(fx - .5f, fx + .5f, W, c.Top, (x, y, i, j) =>
            {
                float cx = fx + f.N(0, y, 2, 43) * .08f, half = .16f + (10 - y) * .015f, d = MathF.Abs(x - cx) / half;
                if (d > 1) return Col.Clear;
                float n = f.Fbm2(x, y, .08f, .9f, 2, 45) * .5f + .5f;
                return Kit.Mix(Col.Hex("4e9a9e"), Col.Hex("d4f4ec"), n * (1 - d * .5f), i, j).WithA(.85f);
            });
            Kit.Glow(c, fx, W + .1f, 1.4f, Foam, .5f, .45f);
        }
        Ripples(c, 47, .6f);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Mid()
    {
        var s = new LayerSpec("mid", .65f, 96, 36, -9.5f, 22, -95) { Under = Deep };
        var c = s.NewCanvas(); var f = c.F;
        c.Paint(0, c.Period, -9.5f, W, (x, y, i, j) => Water(y, i, j));
        var cols = c.Blank();
        // Drowned classical columns crusted with coral.
        foreach (var (cx, top, broken) in new[] { (3.2f, 6.6f, false), (13.6f, 2.6f, true), (29.4f, 7.8f, false), (32.8f, 4.4f, true) })
            cols.Paint(cx - 1, cx + 1, W - 2, top + 1, (x, y, i, j) =>
            {
                float dx = x - cx, r = .42f;
                float cut = broken ? top - (dx + r) * .5f + f.N(x, y, .2f, 201) * .25f : top;
                if (y > cut + (broken ? 0 : .5f)) return Col.Clear;
                float u = dx / r;
                if (!broken && y > top) { u = dx / .62f; if (MathF.Abs(u) > 1) return Col.Clear; return Stone.At(.5f + (y > top + .38f ? .2f : 0) - u * .2f, i, j, .4f); }
                if (MathF.Abs(u) > 1) return Col.Clear;
                float flute = MathF.Cos(u * MathF.PI * 3.5f) * .08f;
                return Stone.At(Kit.Cylinder(u, .15f) * .78f + flute + f.Fbm(x, y, .4f, 3, 203) * .1f, i, j, .4f);
            });
        // An architrave fragment rests between the two right columns.
        cols.Paint(28.6f, 33.6f, 7.4f, 8.4f, (x, y, i, j) =>
        {
            float top = 8.3f - MathF.Max(0, x - 31.6f) * .55f;
            if (y > top || y < 7.8f) return Col.Clear;
            return Stone.At(.45f + (y > top - .08f ? .25f : 0) + (M.Mod(x, .3f) < .04f && y < 8.1f ? -.15f : 0), i, j, .4f);
        });
        // Coral, barnacles and algae on the stone.
        cols.Post((i, j, col) =>
        {
            float x = cols.X(i), y = cols.Y(j);
            // Coral grows in clumps near the waterline; it fades out higher up the shafts.
            float n = f.Fbm(x, y, .6f, 2, 205) * .8f + M.Smooth(-.5f, W + .3f, y) * .7f;
            if (n > .6f) return Coral.At(.35f + (n - .6f) * 1.4f + f.N(x, y, .08f, 207) * .15f, i, j, .4f);
            if (y < W + .7f) return Kit.Mix(col, Col.Hex("2a4a3a"), .6f, i, j);
            return col;
        });
        // A palm islet with lilies at its foot.
        var isle = c.Blank();
        isle.Paint(17, 27, W - 1, W + 1.4f, (x, y, i, j) =>
        {
            float dx = (x - 21.8f) / 4.2f, top = W + .9f * MathF.Sqrt(Math.Max(0, 1 - dx * dx)) + f.N(x, 0, .5f, 209) * .12f;
            if (y > top || MathF.Abs(dx) > 1) return Col.Clear;
            if (top - y < .14f) return Kit.Mix(Col.Hex("c8b884"), Col.Hex("8e8a5c"), (top - y) / .14f, i, j);
            return Kit.Mix(Col.Hex("3a4a40"), Col.Hex("22302c"), M.InvLerp(top, W - .4f, y), i, j);
        });
        Kit.Palm(isle, 20.4f, W + .7f, 4.6f, -.18f, Trunk, Fronds, 211);
        Kit.Palm(isle, 22.6f, W + .75f, 5.6f, .12f, Trunk, Fronds, 213);
        Kit.Palm(isle, 24.1f, W + .5f, 3.4f, .26f, Trunk, Fronds, 215);
        Kit.LeafClump(isle, 21.4f, W + .95f, 1.2f, .35f, 120, Fronds, 217, .3f, -.4f);
        foreach (var (lx, ly) in new[] { (9.2f, W - .25f), (10.4f, W - .45f), (16.2f, W - .3f), (27.8f, W - .2f), (34.6f, W - .4f) })
            isle.Paint(lx - .4f, lx + .4f, ly - .12f, ly + .2f, (x, y, i, j) =>
            {
                float dx = (x - lx) / .34f, dy = (y - ly) / .08f;
                if (dx * dx + dy * dy < 1) return MathF.Abs(MathF.Atan2(dy, dx) - .4f) < .25f ? Col.Clear : Fronds.At(.55f + dy * .2f, i, j, .4f);
                float fx = (x - lx - .08f) / .09f, fy = (y - ly - .1f) / .08f;
                return fx * fx + fy * fy < 1 ? Coral.At(.7f + fy * .2f, i, j, .3f) : Col.Clear;
            });
        Kit.Fog(cols, y => Col.Hex("1e5260"), y => .14f);
        c.Over(cols);
        c.Over(isle);
        Kit.Reflect(c, W, 4, Col.Hex("1d5262"), .6f, 219);
        Ripples(c, 221, .8f);
        Kit.Shafts(c, 28, 3, .26f, Col.Hex("c4f2e6"), .12f, W, 12, 223);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Near()
    {
        var s = new LayerSpec("near", .40f, 96, 40, 2, 12.5f, -94) { Over = Col.Hex("07141c") };
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(401);
        float Roof(float x) => 12.2f + f.Fbm(x, 2, 3.4f, 4, 81) * .8f - MathF.Max(0, 1 - MathF.Abs(f.Dx(x, 9)) / 4) * 2.2f - MathF.Max(0, 1 - MathF.Abs(f.Dx(x, 30)) / 5) * 3;
        var rock = c.Blank();
        rock.Paint(0, c.Period, 2, c.Top, (x, y, i, j) => y > Roof(x) ? Col.White : Col.Clear);
        var dark = new Ramp("06121a", "0b1c24", "10262e", "17323a", "214248", "305656");
        Kit.Inflate(rock, 30, (i, j, src, h, n) =>
        {
            float v = .1f + Kit.Lambert(n.X, n.Y, n.Z) * .55f + f.Fbm(rock.X(i), rock.Y(j), .5f, 3, 85) * .14f;
            return Kit.Grain(dark.At(v, i, j, .45f), i, j, 87, .05f, .2f);
        }, (i, j) => f.Fbm(rock.X(i), rock.Y(j), .9f, 4, 86) * 10);
        // Moss beards and seaweed strands hang from the lip; drips glint.
        var weed = c.Blank();
        for (int k = 0; k < 34; k++)
        {
            float x = rng.R(0, 40);
            Kit.Vine(weed, x, Roof(x) + .1f, rng.R(.8f, 4.2f), Col.Hex("163a30"), new Ramp("0a2420", "123226", "1a422e", "245438", "346a44"), 600 + k, .14f, 2);
        }
        rock.Over(weed);
        Kit.Edge(rock, 0, -1, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("5aa8a0"), d == 0 ? .5f : .2f));
        c.Over(rock);
        for (int k = 0; k < 14; k++) { float x = rng.R(0, 40), y = Roof(x) - rng.R(.1f, .6f); c.Set(c.I(x), c.J(y), Foam); c.Set(c.I(x), c.J(y) - 1, Foam.WithA(.6f)); }
        return new PaintedLayer(s, c);
    }
}
