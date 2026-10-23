namespace BackdropPainter;

/// <summary>Kunlun and the celestial palace in ink-wash mist: jade karst peaks rising from a cloud sea,
/// a red-and-gold palace with a pagoda and cloud bridge, a dragon winding through auspicious clouds,
/// and a palace garden of lacquered pillars, guardian lions, peach trees, lanterns and a moon gate.</summary>
public sealed class ChineseTheme : ThemePainter
{
    public override string Id => "chinese";

    static readonly (float y, Col c)[] SkyStops = Kit.Stops((-11, "ebe5d8"), (-5, "e2ddd1"), (0, "d4d4cd"), (4, "bcc5ca"), (7.5f, "a3b2bc"), (10, "8fa0ae"));
    static readonly Col Mist = Col.Hex("d9d7cf"), MistDeep = Col.Hex("c3c6c4");
    static readonly Ramp MistRamp = new("aab2b6", "bcc2c4", "cdd0d0", "dbdcd9", "e8e7e2", "f2f0ea", "faf8f2");
    static readonly Ramp CloudInk = new("66748a", "8a97a8", "b2bcc8", "d0d7de", "e6eaee", "f8f8f6");
    static readonly Ramp Karst = new("36464c", "45585c", "587068", "70867e", "8a9e94", "a6b6aa", "c2cec2");
    static readonly Ramp KarstFar = new("5c6c74", "6c7c82", "7e8e90", "92a09e", "a8b4ae", "bec8c0", "d2d8d0");
    static readonly Ramp Pine = new("1e3028", "2a4034", "365240", "46644c", "5a7a5a");
    static readonly Ramp Gold = new("6a4418", "8a5c1c", "b07c22", "d09a2e", "e8b848", "f6d470", "fff0b0");
    static readonly Ramp Red = new("4a1410", "72201a", "9a2c1e", "c03a24", "d85a3a", "e8846a");
    static readonly Ramp LanternRed = new("5a1410", "8c1e16", "b82c1c", "d84a2a", "ee6e44", "f8a070", "ffd0a0");
    static readonly Ramp Stone = new("4a4e54", "6a6e72", "8a8e90", "a8aaa8", "c4c4c0", "dcdad4", "f0eee8");
    static readonly Ramp Wood = new("3a2416", "5a3820", "7a4e2a", "9a6a3a", "b88a52");
    static readonly Ramp Jade = new("1e4a44", "28625a", "347a6c", "468e7c", "5aa08c", "78b49e", "9ccab4");
    static readonly Ramp Blossom = new("8a3a50", "b05068", "d07088", "e890a0", "f4acb8", "fcc8d0", "fff0f0");
    static readonly Ramp Bark = new("1a1412", "2a2018", "3c2e22", "50402e", "66543c");
    static readonly Ramp Bronze = new("2a2a22", "3e3c2c", "585438", "7a7048", "9a8c58", "b8a868");
    static readonly Ramp Bamboo = new("2e4a28", "3e6434", "558044", "6e9a56", "8ab468", "a8cc84");
    static readonly Ramp GreyTile = new("3a3e44", "4e5258", "646870", "7c8088", "969aa0");
    static readonly Col Ink = Col.Hex("3c4452"), DragonGold = Col.Hex("d8ac4e"), PanelDark = Col.Hex("2a1a16");
    static readonly Col RidgeDark = Col.Hex("4a3a22"), RidgeHi = Col.Hex("e8c860");
    static readonly (float y, Col c)[] WaterStops = Kit.Stops((-9.5f, "1c4842"), (-6, "2a645a"), (-4.2f, "3a7e6e"), (-3.5f, "4c9482"));

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return Sky();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    PaintedLayer Sky()
    {
        var s = new LayerSpec("sky", .95f, 48, 30, -11, 21, -97) { Under = SkyStops[0].c, Over = SkyStops[^1].c };
        var c = s.NewCanvas(); var f = c.F;
        c.PaintAll((x, y, i, j) => Kit.Gradient(SkyStops, y, 4, 1, i, j));
        // Ink-wash ghosts of distant peaks dissolving into the haze below.
        c.Paint(0, c.Period, -11, 6.5f, (x, y, i, j) =>
        {
            float sp = f.Snap(4.2f); int n = (int)MathF.Round(c.Period / sp), k = M.Mod((int)MathF.Floor(x / sp), n);
            float u = M.Mod(x, sp) - sp * .5f - (Field.Hash01(k, 1, 9) - .5f) * 1.2f;
            float top = -2.5f + Field.Hash01(k, 2, 9) * 7.5f, hw = .55f + Field.Hash01(k, 3, 9) * .9f, dome = top - hw;
            float lim = y > dome ? hw * MathF.Sqrt(Math.Max(0, 1 - (y - dome) * (y - dome) / (hw * hw))) : hw * (1 + (dome - y) * .03f);
            lim *= 1 + f.N2(x, y, 1, 1.5f, 11) * .14f;
            if (MathF.Abs(u) > lim || y > top) return Col.Clear;
            float tint = .05f + .22f * M.Smooth(-8, -2, y) + (lim - MathF.Abs(u) < .08f ? .05f : 0);
            return Col.Lerp(Kit.Gradient(SkyStops, y, 4, 1, i, j), Col.Hex("7e8e96"), tint);
        });
        // The great pale disc of the sun seen through mist.
        Kit.Glow(c, 21, 5.6f, 5.5f, Col.Hex("f8f0dc"), .35f, .9f, 6);
        c.Paint(18.5f, 23.5f, 3.2f, 8, (x, y, i, j) =>
        {
            float dx = x - 21, dy = y - 5.6f, d = MathF.Sqrt(dx * dx + dy * dy) / 2.2f;
            if (d > 1) return Col.Clear;
            if (d > .95f) return Col.Hex("dccfb6");
            return Kit.Mix(Col.Hex("f0e6d0"), Col.Hex("faf4e4"), .7f - d * .5f - dx * .08f, i, j, 3);
        });
        // Low mist bands.
        c.Paint(0, c.Period, -11, 1, (x, y, i, j) =>
        {
            float a = M.Smooth(1, -5, y) * (.35f + f.Fbm2(x, y, 5, .5f, 3, 13) * .35f);
            float q = MathF.Floor(a * 5 + Dither.Bayer(i, j)) / 5;
            return q <= 0 ? Col.Clear : Col.Hex("ece8de").WithA(q);
        });
        // A dragon winds through auspicious clouds high in the sky.
        var cloud = CloudInk.Fogged(Kit.Smooth(SkyStops, 7), .18f);
        ChineseKit.XiangCloud(c, 3.5f, 6.2f, 3.2f, 1.1f, cloud, 21, .95f, -1);
        ChineseKit.XiangCloud(c, 26.5f, 2.6f, 3.6f, 1.2f, cloud, 23, .9f, 1);
        ChineseKit.Dragon(c, 2.2f, 7.4f, 11.5f, .62f, .3f, Ink, DragonGold, 5);
        ChineseKit.XiangCloud(c, 6.2f, 7.1f, 2.4f, .9f, cloud, 25, .95f, 1);
        ChineseKit.XiangCloud(c, 11.8f, 6.3f, 3.4f, 1.15f, cloud, 27, .95f, 1);
        ChineseKit.XiangCloud(c, 16.5f, 8.4f, 2.8f, 1f, cloud, 29, .9f, -1);
        ChineseKit.XiangCloud(c, 14.5f, 3.4f, 2.6f, .9f, cloud.Fogged(Mist, .2f), 31, .85f, 1);
        ChineseKit.XiangCloud(c, 21.5f, 8.8f, 2f, .75f, cloud, 33, .85f, 1);
        ChineseKit.XiangCloud(c, 8.5f, .8f, 3f, 1f, cloud.Fogged(Mist, .3f), 35, .8f, -1);
        ChineseKit.XiangCloud(c, 20.5f, -1.2f, 2.6f, .9f, cloud.Fogged(Mist, .35f), 37, .75f, 1);
        return new PaintedLayer(s, c);
    }

    /// <summary>A rank of karst pillars: domed tops, bulging flanks, vertical strata, pale ledges with greenery and pines.</summary>
    static void KarstRank(Canvas p, (float x, float top, float w)[] peaks, Ramp ramp, int seed, float pineScale)
    {
        var f = p.F;
        p.Paint(0, p.Period, -11, p.Top, (x, y, i, j) =>
        {
            foreach (var pk in peaks)
            {
                float dx = f.Dx(x, pk.x), hw = pk.w * .5f, dome = pk.top - hw * .9f, lim;
                if (y > dome) { float t = (y - dome) / (hw * .9f); if (t >= 1) continue; lim = hw * MathF.Sqrt(1 - t * t); }
                else lim = hw * (1 + (dome - y) * .025f);
                lim *= 1 + f.N2(x, y, 1.2f, 1.6f, seed) * .14f + f.N2(x, y, .5f, .35f, seed + 1) * .06f;
                if (MathF.Abs(dx) < lim) return Col.White;
            }
            return Col.Clear;
        });
        Kit.Inflate(p, 22, (i, j, src, h, n) =>
        {
            float x = p.X(i), y = p.Y(j);
            float strata = f.Fbm2(x, y, .18f, 2.2f, 3, seed + 2) * .5f + .5f, ledge = f.Fbm2(x, y, 1.6f, .25f, 2, seed + 3);
            float v = .22f + Kit.Lambert(n.X, n.Y, n.Z) * .52f + (strata - .5f) * .22f;
            if (ledge > .35f) v += .12f;
            if (f.Ridge(x, y, 1.1f, 3, seed + 4) > .93f) v -= .15f;
            if (p.Depth(i, j, 0, 1, 3) < 3) v += .2f;
            var col = ramp.At(v, i, j, .4f);
            if (ledge > .42f && n.Y > .15f) col = Kit.Mix(col, Col.Hex("4e7a5e"), .5f, i, j, 3);
            return col;
        }, (i, j) => f.Fbm(p.X(i), p.Y(j), 1.2f, 3, seed + 5) * 8);
        var rng = new Rng(seed);
        foreach (var pk in peaks)
            for (int k = 0; k < 4; k++)
            {
                float px = pk.x + rng.R(-pk.w * .32f, pk.w * .32f), top = pk.top + .3f;
                while (top > -10 && !p.Solid(p.I(px), p.J(top))) top -= .02f;
                Kit.Pine(p, px, top - .06f, rng.R(.5f, .9f) * pineScale, Pine, Pine[4], Pine[3], seed * 7 + k);
                // A pine clinging to a flank ledge.
                float ly = rng.R(pk.top - 5, pk.top - 1.2f); int dir = rng.Chance(.5f) ? 1 : -1; float lx = pk.x;
                while (MathF.Abs(lx - pk.x) < pk.w && p.Solid(p.I(lx), p.J(ly))) lx += dir * .02f;
                if (MathF.Abs(lx - pk.x) < pk.w) Kit.Pine(p, lx + dir * .12f, ly - .08f, rng.R(.35f, .6f) * pineScale, Pine, Pine[4], Pine[3], seed * 11 + k);
            }
    }

    PaintedLayer Far()
    {
        var s = new LayerSpec("far", .85f, 64, 32, -11, 21, -96) { Under = MistRamp[4] };
        var c = s.NewCanvas(); var f = c.F;
        var back = c.Blank();
        KarstRank(back, new[] { (2.5f, 5.5f, 2.6f), (9f, 7.4f, 2.2f), (20.5f, 6.8f, 3f), (28f, 8.4f, 2.4f) }, KarstFar, 41, .8f);
        Kit.Fog(back, y => Kit.Smooth(SkyStops, y), y => .6f + M.Smooth(0, -7, y) * .3f);
        var rank = c.Blank();
        KarstRank(rank, new[] { (6f, 3.6f, 2f), (13f, 4.8f, 2.6f), (31.5f, 4.2f, 1.9f), (23.5f, 2.4f, 2.2f) }, KarstFar, 43, .9f);
        Kit.Fog(rank, y => Kit.Smooth(SkyStops, y), y => .42f + M.Smooth(-1, -7, y) * .35f);
        var front = c.Blank();
        KarstRank(front, new[] { (16.5f, 1.3f, 5.4f), (25.5f, -.1f, 3.4f), (9.3f, 2.7f, 2.6f) }, Karst, 45, 1);
        // The celestial palace on the summits: main hall, side hall, pagoda and a stair-bridge of cloud-stone.
        ChineseKit.Hall(front, 16.5f, 1.3f, 2.5f, 1.5f, 1.25f, Stone, Red, Gold, Wood, PanelDark, RidgeDark, RidgeHi, 47, true);
        ChineseKit.Hall(front, 9.3f, 2.7f, 1.15f, 1.05f, .85f, Stone, Red, Gold, Wood, PanelDark, RidgeDark, RidgeHi, 49);
        ChineseKit.Pagoda(front, 25.5f, -.1f, 1f, 6, .92f, Stone, Red, Gold, Wood, PanelDark, RidgeDark, RidgeHi, 51);
        ChineseKit.CloudBridge(front, 10.7f, 2.95f, 13.7f, 1.6f, .25f, Stone, 53);
        ChineseKit.CloudBridge(front, 19.3f, 1.6f, 24.3f, .2f, .3f, Stone, 55);
        Kit.Fog(front, y => Kit.Smooth(SkyStops, y), y => .28f + M.Smooth(-1, -7, y) * .4f);
        c.Over(back);
        Kit.CloudBank(c, 0, 32, -8.5f, 4, 1.2f, 2.4f, 30, MistRamp.Fogged(Kit.Smooth(SkyStops, -4), .35f), 57, .15f, .9f);
        c.Over(rank);
        Kit.CloudBank(c, 0, 32, -8, 2.8f, 1, 2, 34, MistRamp.Fogged(Kit.Smooth(SkyStops, -5), .2f), 59, .15f, .9f);
        c.Over(front);
        Kit.CloudBank(c, 0, 32, -11, 5.2f, 1.4f, 2.8f, 34, MistRamp, 61, .18f);
        Kit.CloudBank(c, 0, 32, -11, 2.6f, .9f, 1.8f, 40, MistRamp.Shift(.05f), 63, .15f);
        // Wisps of cloud drift across the palace terraces.
        foreach (var (wx, wy, w) in new[] { (12.5f, .4f, 3f), (22f, -.6f, 2.6f), (3.5f, -1.4f, 2.4f) })
            Kit.CloudBank(c, wx - w, wx + w, wy, .9f, .4f, .9f, 12, MistRamp, (int)(wx * 13), .15f, .85f);
        Kit.Glow(c, 16.5f, 3.5f, 4, Col.Hex("f8e8c8"), .1f);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Mid()
    {
        var s = new LayerSpec("mid", .65f, 96, 36, -9.5f, 22, -95) { Under = WaterStops[0].c };
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(201);
        const float T = -3.3f; // terrace level
        // Jade pond below the terrace: lotus pads and koi under rippling light.
        c.Paint(0, c.Period, -9.5f, T, (x, y, i, j) => Kit.Gradient(WaterStops, y, 4, 1, i, j));
        foreach (var (kx, ky, kl, ka) in new[] { (4.5f, -5.2f, 1.1f, .3f), (12.5f, -6.8f, 1.3f, 2.9f), (21f, -4.6f, 1f, -.6f), (27.5f, -7.4f, 1.2f, 1.1f), (33f, -5f, 1f, 2.6f), (16.5f, -8.4f, .9f, .2f) })
            ChineseKit.Koi(c, kx, ky, kl, ka, Col.Hex("f0e6d8"), Col.Hex("e0582c"), Col.Hex("3a2a28"), (int)(kx * 7));
        for (int k = 0; k < 26; k++) ChineseKit.Lotus(c, rng.R(0, 36), rng.R(-9.2f, -3.7f), rng.R(.28f, .5f), Jade.Shift(.1f), 300 + k);
        c.Paint(0, c.Period, -9.5f, T, (x, y, i, j) =>
        {
            float d = T - y, n = f.Fbm2(x, y, 1.1f + d * .2f, .06f + d * .02f, 2, 205);
            float a = M.Smooth(.4f, .62f, n) * .5f * (1 - M.Clamp01(d / 5));
            return a > .12f ? Col.Hex("c8ecdc").WithA(Math.Min(1, a)) : Col.Clear;
        });
        // Garden props on the terrace.
        var props = c.Blank();
        ChineseKit.MoonGate(props, 15.2f, T, 1.3f, 4.2f, 1.75f, Stone, GreyTile, Stone, 207);
        foreach (var px in new[] { 3.4f, 8.2f, 22.6f, 27.8f }) ChineseKit.DragonPillar(props, px, T, 7.4f, .3f, Red, Gold, (int)(px * 17));
        foreach (var (bx0, bx1) in new[] { (3.4f, 8.2f), (22.6f, 27.8f) })
        {
            float cx = (bx0 + bx1) * .5f, hw = (bx1 - bx0) * .5f;
            props.Paint(bx0 - .5f, bx1 + .5f, 7.4f, 7.95f, (x, y, i, j) => y > 7.85f || y < 7.5f ? Gold.At(.5f, i, j, .3f) : Red.At(.35f + (M.Mod(x, .6f) < .3f ? .08f : 0), i, j, .3f));
            ChineseKit.Dougong(props, bx0 - .5f, bx1 + .5f, 7.95f, 8.45f, Col.Lerp(Red[1], Col.Black, .35f), Wood, Col.Hex("5e8a6a"), 209);
            ChineseKit.Roof(props, cx, 8.45f, hw + 1.1f, hw * .55f, 1.45f, .5f, Gold, RidgeDark, RidgeHi, 211);
        }
        ChineseKit.Lion(props, 12.4f, T, 1.7f, Stone, 1, 213);
        ChineseKit.Lion(props, 18f, T, 1.7f, Stone, -1, 215);
        ChineseKit.PeachTree(props, 1f, T, 5.6f, Bark, Blossom, Col.Hex("f0c060"), Col.Hex("b87828"), Col.Hex("e8907c"), 217);
        ChineseKit.PeachTree(props, 31.6f, T, 5.2f, Bark, Blossom, Col.Hex("f0c060"), Col.Hex("b87828"), Col.Hex("e8907c"), 219);
        ChineseKit.PeachTree(props, 20.6f, T, 4.4f, Bark, Blossom, Col.Hex("f0c060"), Col.Hex("b87828"), Col.Hex("e8907c"), 221);
        ChineseKit.Ding(props, 10.4f, T, 1.5f, Bronze, Col.Hex("ff9a40"), 223);
        ChineseKit.Ding(props, 29.9f, T, 1.3f, Bronze, Col.Hex("ff9a40"), 225);
        foreach (var (bx, bh, bl) in new[] { (33.8f, 6.4f, .06f), (34.3f, 7.2f, -.04f), (34.9f, 5.6f, .1f), (35.4f, 6.8f, -.08f), (6.2f, 4.2f, .08f), (6.6f, 5f, -.06f) })
            ChineseKit.Bamboo(props, bx, T, bh, bl, Bamboo, (int)(bx * 23));
        foreach (var lx in new[] { 4.5f, 5.8f, 7.1f, 23.7f, 25.2f, 26.7f })
            ChineseKit.Lantern(props, lx, 7.4f, .32f, .25f + (M.Mod(lx, 1.3f) > .6f ? .2f : 0), LanternRed, Gold[4], Gold[1], Col.Hex("5a3a20"));
        ChineseKit.Balustrade(props, 0, 36, T, .62f, Stone, 227);
        Kit.Fog(props, y => Mist, y => .14f + .2f * M.Smooth(3, -3.5f, y));
        c.Over(props);
        foreach (var (sx, sy) in new[] { (10.4f, T + 1.45f), (29.9f, T + 1.25f) }) ChineseKit.Smoke(c, sx, sy, 5.5f, .45f, Col.Hex("e8e6de"), .55f, (int)(sx * 3));
        foreach (var lx in new[] { 4.5f, 5.8f, 7.1f, 23.7f, 25.2f, 26.7f })
        {
            float ly = 7.4f - (.25f + (M.Mod(lx, 1.3f) > .6f ? .2f : 0)) - .4f;
            Kit.Light(c, lx, ly, 1.6f, Col.Hex("ff9a50"), .4f);
            Kit.Glow(c, lx, ly, .9f, Col.Hex("ffa860"), .3f);
        }
        Kit.Shafts(c, -20, 4, .25f, Col.Hex("fff4dc"), .08f, -3, 12, 229);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Near()
    {
        var s = new LayerSpec("near", .40f, 96, 40, 2, 12.5f, -94) { Over = Col.Hex("161a20") };
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(401);
        var dark = new Ramp("161a20", "22262c", "30323a", "3e3e44", "504a40", "6a6040", "8a7c4c");
        var darkRed = new Ramp("1a0c0c", "2e1210", "46191a", "5e2420", "78332a", "90483a");
        var darkWood = new Ramp("1c1410", "2a1c14", "3a281a", "4c3624", "60462e");
        // The eave of a great hall overhead: tiled roof mass with two upturned corners.
        float Eave(float x)
        {
            float e = 11.7f;
            foreach (var cx in new[] { 7f, 27f }) e += 1.1f * MathF.Pow(M.Clamp01(1 - MathF.Abs(f.Dx(x, cx)) / 3.2f), 2.6f);
            return e;
        }
        var roof = c.Blank();
        roof.Paint(0, c.Period, 10, c.Top, (x, y, i, j) =>
        {
            float e = Eave(x); if (y < e) return Col.Clear;
            float px1 = 1f / c.Ppu, d = y - e;
            float u = M.Mod(x, .22f) / .22f;
            if (d < 3 * px1) return u < .5f ? dark[2] : dark[1];
            if (d < 5 * px1) return dark[3];
            float v = .35f + (u < .3f ? .15f : u > .85f ? -.08f : 0) - (M.Mod(y, .2f) < .035f ? .12f : 0) + M.Smooth(e + 2.5f, e, y) * .18f + f.N(x, y, .3f, 403) * .05f;
            return dark.At(v, i, j, .4f);
        });
        // Eave board, two tiers of dougong and a lintel beam underneath.
        roof.Paint(0, c.Period, 10, 12.9f, (x, y, i, j) =>
        {
            float e = Eave(x), under = e - y;
            if (under < 0 || under > 1.85f) return Col.Clear;
            if (under < .22f) return (M.Mod(x, .45f) < .22f ^ under < .1f) ? darkRed[2] : darkRed[1];
            return Col.Clear;
        });
        var brackets = c.Blank();
        ChineseKit.Dougong(brackets, 0, 40, 10.35f, 11.45f, Col.Hex("1c1212"), darkWood, Col.Hex("3e5e48"), 405);
        brackets.Post((i, j, col) => brackets.Y(j) > Eave(brackets.X(i)) - .22f ? Col.Clear : col);
        roof.Over(brackets);
        roof.Paint(0, c.Period, 9.9f, 10.4f, (x, y, i, j) =>
        {
            if (y < 9.95f || y > 10.35f) return Col.Clear;
            bool trim = y > 10.3f || y < 10.02f;
            return trim ? Gold.At(.35f, i, j, .3f) : darkRed.At(.45f + (f.N(x, 0, .5f, 407) > .2f && M.Mod(y, .1f) < .05f ? .1f : 0), i, j, .3f);
        });
        // Bells at the corners, lanterns on long cords, and blossom branches drooping from above.
        foreach (var bx in new[] { 7f, 27f })
            roof.Paint(bx - .3f, bx + .3f, Eave(bx) - 1.3f, Eave(bx), (x, y, i, j) =>
            {
                float dx = x - bx, top = Eave(bx) - .05f, t = top - y;
                if (t < .5f) return MathF.Abs(dx) < 1.2f / c.Ppu ? Gold[2] : Col.Clear;
                float hw = .07f + (t - .5f) * .38f;
                if (t < 1.05f && MathF.Abs(dx) < hw) return Gold.At(.55f - dx * 1.5f + (t > .98f ? -.2f : 0), i, j, .3f);
                if (t > 1.05f && t < 1.25f && MathF.Abs(dx) < .04f) return Gold[3];
                return Col.Clear;
            });
        foreach (var (lx, cord) in new[] { (2.5f, 1.2f), (11.5f, 2.2f), (17f, 1f), (22.5f, 2.6f), (31.5f, 1.4f), (37f, 2f) })
            ChineseKit.Lantern(roof, lx, 9.95f, .5f, cord, LanternRed.Shift(-.1f), Gold[4], Gold[1], Col.Hex("4a3020"));
        var branches = c.Blank();
        foreach (var (bx, len, sgn) in new[] { (14.5f, 4.6f, 1), (33.5f, 3.8f, -1), (5f, 2.8f, 1) })
        {
            float ax = bx, ay = 12.4f, w = .11f;
            for (int k = 0; k < 4; k++)
            {
                float nx = ax + sgn * len * .18f * (k % 2 == 0 ? 1 : .4f), ny = ay - len * .24f;
                Capsule(branches, ax, ay, nx, ny, w, w * .8f, Bark, 409 + k);
                Kit.LeafClump(branches, nx + sgn * .2f, ny + .1f, .55f, .4f, 90, Blossom.Shift(-.12f), 420 + k * 3 + (int)bx, .14f, .3f);
                ax = nx; ay = ny; w *= .8f;
            }
        }
        for (int k = 0; k < 10; k++) Kit.Vine(branches, rng.R(0, 40), 11.9f - rng.R(0, .3f), rng.R(1.2f, 3.4f), Col.Hex("2a2418"), Blossom.Shift(-.2f), 440 + k, .18f, 1.6f);
        roof.Over(branches);
        c.Over(roof);
        foreach (var (lx, cord) in new[] { (2.5f, 1.2f), (11.5f, 2.2f), (17f, 1f), (22.5f, 2.6f), (31.5f, 1.4f), (37f, 2f) })
        {
            float ly = 9.95f - cord - .62f;
            Kit.Light(c, lx, ly, 2.4f, Col.Hex("ff9a50"), .45f);
            Kit.Glow(c, lx, ly, 1.3f, Col.Hex("ffa860"), .28f);
        }
        return new PaintedLayer(s, c);
    }

    static void Capsule(Canvas c, float ax, float ay, float bx, float by, float w0, float w1, Ramp bark, int seed)
    {
        float r = Math.Max(w0, w1);
        c.Paint(Math.Min(ax, bx) - r, Math.Max(ax, bx) + r, Math.Min(ay, by) - r, Math.Max(ay, by) + r, (px, py, i, j) =>
        {
            float d = ChineseKit.Seg(px, py, ax, ay, bx, by, out float t), w = M.Lerp(w0, w1, t);
            if (d >= w) return Col.Clear;
            float side = MathF.Sign((px - ax) * (by - ay) - (py - ay) * (bx - ax));
            return bark.At(Kit.Cylinder(d / w * side, .2f) * .8f, i, j, .35f);
        });
    }
}
