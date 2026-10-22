namespace BackdropPainter;

/// <summary>Glacier sanctum: an ice fortress carved into a glacier under an aurora, faceted ice cliffs,
/// a frozen waterfall, broken ribbed arches, snowbound pines and hanging icicles.</summary>
public sealed class FrozenTheme : ThemePainter
{
    public override string Id => "frozen";

    static readonly Ramp IceRamp = new("142238", "1c3150", "254466", "305a7e", "3f7194", "5a8eac", "7eaac2", "acccdc", "e2f2f6");
    static readonly Ramp Needles = new("0e1c26", "15283a", "1d3648", "284658", "355868");
    static readonly Col Snow = Col.Hex("eaf2f8"), SnowShade = Col.Hex("b4c4e0"), SnowDeep = Col.Hex("8494c0");
    static readonly (float y, Col c)[] SkyStops = Kit.Stops((-11, "3e5e7c"), (-6, "2e4a6a"), (-2, "23395a"), (3, "182a4a"), (10, "0a1230"));

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return Sky();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    /// <summary>Snow settles on every upward-facing edge, shaded lavender away from the light.</summary>
    static Col SnowCap(Canvas p, int i, int j, Col under, int depth, float lit)
    {
        int up = p.Depth(i, j, 0, 1, depth);
        if (up >= depth) return under;
        if (up == 0) return Snow;
        return lit > .5f ? (Dither.Hit(.7f, i, j) ? Snow : SnowShade) : (up < depth / 2 ? SnowShade : SnowDeep);
    }

    PaintedLayer Sky()
    {
        var s = new LayerSpec("sky", .95f, 48, 30, -11, 21, -97) { Under = SkyStops[0].c, Over = SkyStops[^1].c };
        var c = s.NewCanvas(); var f = c.F;
        c.PaintAll((x, y, i, j) => Kit.Gradient(SkyStops, y, 4, 1, i, j));
        Kit.Stars(c, 1, 10, .004f, Col.Hex("dfe8ff"), 7, (x, y) => M.Smooth(1, 6, y));
        // Aurora curtains with vertical rays.
        c.Paint(0, c.Period, 2.5f, 10, (x, y, i, j) =>
        {
            float yc = 6.6f + f.Fbm(x, 0, 9, 3, 11) * 1.8f, d = (y - yc) / 1.6f;
            if (d < -1.2f || d > 2.4f) return Col.Clear;
            float curtain = d < 0 ? MathF.Exp(-d * d * 6) : MathF.Exp(-d * .9f);
            float rays = M.Clamp01(f.Fbm2(x, y, .35f, 4, 3, 13) * .9f + .55f);
            float a = curtain * rays * (.55f + .45f * f.N(x, 0, 6, 15)) * .6f;
            var col = d < .3f ? Col.Hex("62f0b4") : d < 1.1f ? Col.Hex("68d8f2") : Col.Hex("b29cf2");
            float q = MathF.Floor(a * 6 + Dither.Bayer(i, j)) / 6;
            return q <= 0 ? Col.Clear : col.WithA(q);
        }, add: true);
        // The fortress's far spires, faint behind the snow haze.
        c.Paint(0, c.Period, -11, 9, (x, y, i, j) =>
        {
            float wall = -1.2f + f.Fbm(x, 0, 6, 2, 17) * .8f;
            float sp = f.Snap(2.5f); int k = (int)MathF.Floor(x / sp);
            float hgt = 1.4f + Field.Hash01(M.Mod(k, (int)MathF.Round(c.Period / sp)), 1, 19) * 5.5f, u = MathF.Abs(M.Mod(x, sp) - sp * .5f);
            bool spire = y < wall + hgt && u < .32f * (1 - (y - wall) / hgt) + .12f;
            if (y > wall && !spire) return Col.Clear;
            return Col.Lerp(Kit.Gradient(SkyStops, y, 4, 1, i, j), Col.Hex("5a7898"), .22f);
        });
        c.Paint(0, c.Period, -11, -1, (x, y, i, j) =>
        {
            float a = M.Smooth(-1, -6, y) * (.4f + f.Fbm(x, y * 2, 4, 3, 21) * .3f);
            float q = MathF.Floor(a * 5 + Dither.Bayer(i, j)) / 5;
            return q <= 0 ? Col.Clear : Col.Hex("8aa8c8").WithA(q);
        });
        return new PaintedLayer(s, c);
    }

    PaintedLayer Far()
    {
        var s = new LayerSpec("far", .85f, 64, 32, -11, 21, -96) { Over = Col.Hex("0c1630") };
        var c = s.NewCanvas(); var f = c.F;
        // Glacier cliffs on either side of the sanctum.
        var ice = c.Blank();
        ice.Paint(0, c.Period, -11, c.Top, (x, y, i, j) =>
        {
            float d = MathF.Abs(f.Dx(x, 1.5f));
            float top = 6.4f - MathF.Pow(M.Clamp01((d - 3.2f) / 3.4f), 1.4f) * 12 + f.Fbm(x, 0, 1.2f, 4, 31) * .6f;
            float tier = MathF.Floor(y / 1.6f);
            float shelfIn = Field.Hash01((int)tier, 1, 33) * .5f;
            if (y < top && d < 6.6f - shelfIn) return Col.White;
            return Col.Clear;
        });
        // The sanctum: an ice arcade with snow-capped towers.
        var style = new ArcadeStyle { Span = 5.3f, PierHalf = .62f, Base = -6.8f, Spring = .4f, Pointed = .28f, Deck = 4.2f, GalleryTop = 5.6f, GallerySpan = 1.75f, GalleryHalf = .36f, GallerySpring = 5f, Merlons = true, Rings = 2, RingW = .16f, Phase = 1.5f };
        var fort = c.Blank();
        fort.Paint(6.4f, 28.6f, -7, c.Top, (x, y, i, j) =>
        {
            foreach (var tx in new[] { 9f, 26f })
            {
                float dx = MathF.Abs(x - tx);
                if (y < 6.6f && dx < 1.1f) return Col.White;
                if (y >= 6.6f && y < 9.4f && dx < 1.25f * (1 - (y - 6.6f) / 2.8f)) return Col.White;
                if (y > 6.2f && y < 6.6f && dx < 1.3f) return Col.White;
            }
            return ArchKit.Hit(style, f, x, y).Part != Part.None && x > 8 && x < 27 ? Col.White : Col.Clear;
        });
        var fortRamp = new Ramp("2a4466", "345678", "40688a", "507a9a", "6a90ac", "88a8c0", "acc4d4");
        fort.Post((i, j, col) =>
        {
            float x = fort.X(i), y = fort.Y(j);
            var h = ArchKit.Hit(style, f, x, y);
            V3 n = h.Part == Part.None ? new V3(0, 0, 1) : h.N;
            foreach (var tx in new[] { 9f, 26f })
                if (MathF.Abs(x - tx) < 1.3f && (y >= 6.2f || h.Part == Part.None)) { float u = (x - tx) / 1.3f; n = new V3(u, 0, MathF.Sqrt(Math.Max(0, 1 - u * u))); }
            float v = (h.Part == Part.None ? .45f : ArchKit.BaseTone(h)) * .45f + Kit.Lambert(n.X, n.Y, n.Z) * .5f;
            bool mortar = Kit.Masonry(f, x, y, .7f, .35f, 1.3f / 64, 35, out float sh, out _, out _, out _);
            if (mortar && h.Part is Part.Wall or Part.Pier or Part.Gallery) v -= .12f;
            var c0 = fortRamp.At(v + (sh - .5f) * .08f, i, j, .4f);
            if (h.Part == Part.Oculus) c0 = Kit.Mix(Col.Hex("2a4a70"), Col.Hex("9adcf0"), 1 - h.Detail, i, j);
            return SnowCap(fort, i, j, c0, 4, -n.X + .5f);
        });
        // Narrow lancet windows glow on the towers.
        foreach (var tx in new[] { 9f, 26f })
            for (int k = 0; k < 3; k++)
            {
                float wy = -1.5f + k * 2.4f;
                fort.Paint(tx - .2f, tx + .2f, wy, wy + 1, (x, y, i, j) => Kit.InOpening(x - tx, y, .14f, wy + .75f, wy, .3f) ? Col.Hex("a8e4f0") : Col.Clear);
            }
        Kit.Inflate(ice, 30, (i, j, src, h, n) => SnowCap(ice, i, j, Kit.Ice(f, ice.X(i), ice.Y(j), i, j, n, IceRamp.Shift(.05f), 1.2f, 37, .24f), 5, -n.X + .5f),
            (i, j) => f.Fbm(ice.X(i), ice.Y(j), 1.5f, 3, 39) * 14);
        // A frozen waterfall pours over the left glacier.
        ice.Paint(4.2f, 6.4f, -7, 6, (x, y, i, j) =>
        {
            float top = 5.4f + f.N(x, 0, .6f, 41) * .3f, cx = 5.3f, half = .7f + (top - y) * .03f;
            if (y > top || MathF.Abs(x - cx) > half) return Col.Clear;
            float streak = f.Fbm2(x, y, .09f, 1.6f, 3, 43) * .5f + .5f, edge = MathF.Abs(x - cx) / half;
            return IceRamp.At(.55f + streak * .35f - edge * .2f, i, j, .5f);
        });
        Kit.Fog(ice, y => Kit.Smooth(SkyStops, y), y => .56f + M.Smooth(0, -6, y) * .2f);
        Kit.Fog(fort, y => Kit.Smooth(SkyStops, y), y => .52f + M.Smooth(0, -6, y) * .2f);
        c.Over(fort);
        c.Over(ice);
        foreach (var tx in new[] { 9f, 26f }) Kit.Glow(c, tx, 1.5f, 1.8f, Col.Hex("8ad0e8"), .25f, 2);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Mid()
    {
        var s = new LayerSpec("mid", .65f, 96, 36, -9.5f, 22, -95);
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(201);
        var ice = c.Blank();
        // Layered ice cliffs and a hanging glacier lip.
        float Cliff(float x) => -9.5f + 10.4f * MathF.Pow(MathF.Max(0, MathF.Sin(MathF.PI * M.InvLerp(18.5f, 31.5f, x))), .5f);
        float Lip(float x) => 10.6f + f.Fbm(x, 0, 3, 4, 203) * .9f;
        ice.Paint(0, c.Period, -9.6f, c.Top, (x, y, i, j) =>
        {
            if (y > Lip(x)) return Col.White;
            float top = MathF.Floor(Cliff(x) / .95f) * .95f + f.N(x, 0, .5f, 205) * .1f;
            if (x > 18.5f && x < 31.5f && y < top) return Col.White;
            // Big icicles hanging from the glacier lip.
            float sx = f.Snap(.8f); int k = (int)MathF.Floor(x / sx);
            float h = Field.Hash01(M.Mod(k, (int)MathF.Round(c.Period / sx)), 3, 207);
            if (h > .4f) { float len = .5f + (h - .4f) * 5.5f, t = (Lip(x) - y) / len, u = MathF.Abs(M.Mod(x, sx) - sx * .5f); if (t < 1 && u < .26f * (1 - t)) return Col.White; }
            return Col.Clear;
        });
        // A broken ribbed arch of ice.
        ice.Paint(2, 14, -9.6f, 9, (x, y, i, j) =>
        {
            float cx = 8, half = 4.2f, spring = 1.2f;
            bool outer = Kit.InOpening(x - cx, y, half + .55f, spring, -20, .3f), inner = Kit.InOpening(x - cx, y, half, spring, -20, .3f);
            bool broken = y > 5.6f + (x - cx) * .7f + f.N(x, y, .35f, 209) * .3f && x > cx - 1.6f;
            if (outer && !inner && !broken) return Col.White;
            if (MathF.Abs(MathF.Abs(x - cx) - half - .28f) < .62f && y < spring + .3f) return Col.White;
            return Col.Clear;
        });
        Kit.Inflate(ice, 44, (i, j, src, h, n) => SnowCap(ice, i, j, Kit.Ice(f, ice.X(i), ice.Y(j), i, j, n, IceRamp, .9f, 211, .14f), 7, -n.X + .5f),
            (i, j) => f.Fbm(ice.X(i), ice.Y(j), 1.3f, 3, 213) * 18);
        Kit.Edge(ice, -1, 0, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("dff2f8"), d == 0 ? .45f : .2f));

        // Snowbound pines and crystals on the ledges.
        var trees = c.Blank();
        float Shelf(float x) { for (float y = 3; y > -9.4f; y -= .02f) if (ice.Solid(ice.I(x), ice.J(y)) && !ice.Solid(ice.I(x), ice.J(y + .5f))) return y; return -3.2f; }
        foreach (var (px, h) in new[] { (14.6f, 4.2f), (15.8f, 3.1f), (16.9f, 4.8f), (33.5f, 3.6f), (34.6f, 2.6f) })
            Kit.Pine(trees, px, -3.6f, h, Needles, Snow, SnowShade, (int)(px * 37));
        foreach (var (px, h) in new[] { (21.5f, 2.4f), (23f, 1.8f), (28.6f, 2.2f) })
            Kit.Pine(trees, px, Shelf(px) - .1f, h, Needles, Snow, SnowShade, (int)(px * 41));
        var crystals = new Ramp("1a3050", "2a5478", "3e7ca0", "64a8c6", "9ad2e2", "d0eef4", "ffffff");
        var gems = c.Blank();
        foreach (var (gx, sz) in new[] { (19.4f, 1.1f), (26.2f, .9f), (11.6f, .8f) })
            Kit.CrystalCluster(gems, gx, Shelf(gx) - .1f, sz, 0, .55f, 6, crystals, (int)(gx * 13));
        Kit.Edge(gems, 1, -1, 1, (col, d, i, j) => Col.Lerp(col, Col.Hex("142238"), .5f));
        // Snow drifts along the bottom.
        var drift = c.Blank();
        drift.Paint(0, c.Period, -9.6f, -2.6f, (x, y, i, j) =>
        {
            float top = -3.4f + f.Fbm(x, 0, 2.5f, 3, 215) * .6f;
            if (y > top) return Col.Clear;
            return top - y < .05f ? Snow : Kit.Mix(SnowShade, SnowDeep, M.InvLerp(top - .05f, top - 1.5f, y), i, j);
        });
        Kit.Fog(ice, y => Col.Hex("2c4a6c"), y => .1f + M.Smooth(-3, -9, y) * .4f);
        Kit.Fog(trees, y => Col.Hex("2c4a6c"), y => .12f);
        c.Over(ice);
        c.Over(gems);
        c.Over(trees);
        c.Over(drift);
        foreach (var (gx, sz) in new[] { (19.4f, 1.1f), (26.2f, .9f), (11.6f, .8f) }) Kit.Glow(c, gx, Shelf(gx) + .4f, 1.6f, Col.Hex("9ae4f0"), .35f);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Near()
    {
        var s = new LayerSpec("near", .40f, 96, 40, 2, 12.5f, -94) { Over = Col.Hex("0a1428") };
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(401);
        float Roof(float x) => 12.2f + f.Fbm(x, 2, 3.2f, 4, 81) * .7f;
        var spikes = new List<(float x, float y, float len, float w)>();
        foreach (var (cx, len, w) in new[] { (7f, 6.6f, 3.6f), (28f, 7.4f, 4.2f), (18f, 3.4f, 2f) })
        {
            spikes.Add((cx, Roof(cx) + .4f, len, w * .6f));
            for (int k = 0; k < 9; k++) { float ox = rng.R(-w * .7f, w * .7f); spikes.Add((cx + ox, Roof(cx + ox) + .2f, len * rng.R(.25f, .75f) * (1 - MathF.Abs(ox) / w * .7f), w * rng.R(.12f, .28f))); }
        }
        for (int k = 0; k < 40; k++) { float x = rng.R(0, 40); spikes.Add((x, Roof(x) + .2f, rng.R(.5f, 2.2f), rng.R(.12f, .4f))); }
        var ice = c.Blank();
        ice.Paint(0, c.Period, 2, c.Top, (x, y, i, j) =>
        {
            if (y > Roof(x)) return Col.White;
            foreach (var sp in spikes)
            {
                float dx = f.Dx(x, sp.x);
                if (MathF.Abs(dx) < sp.w && Kit.Spike(f, sp.x + dx, y, sp.x, sp.y, sp.len, sp.w, -1, 91, out _, out _, .08f, 1.35f)) return Col.White;
            }
            return Col.Clear;
        });
        var dark = new Ramp("08101e", "0e1a2e", "142640", "1c3454", "28466a", "3a5e84", "5a80a4", "8eb0c8");
        Kit.Inflate(ice, 22, (i, j, src, h, n) => Kit.Ice(f, ice.X(i), ice.Y(j), i, j, n, dark, .45f, 85, .05f),
            (i, j) => f.Fbm(ice.X(i), ice.Y(j), .8f, 3, 86) * 8);
        Kit.Edge(ice, 0, -1, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("a8d8ea"), d == 0 ? .6f : .25f));
        Kit.Edge(ice, -1, 0, 1, (col, d, i, j) => Col.Lerp(col, Col.Hex("6a94b4"), .35f));
        c.Over(ice);
        return new PaintedLayer(s, c);
    }
}
