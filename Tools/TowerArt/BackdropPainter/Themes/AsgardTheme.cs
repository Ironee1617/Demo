namespace BackdropPainter;

/// <summary>Asgard at dusk: Yggdrasil's boughs spread under an aurora, the Bifrost arcs down to its gate,
/// Valhalla's golden roof and shield wall stand on a snowy plateau, and the approach runs between rune-lit
/// pillars, braziers and banners beneath the longhall's carved dragon eaves.</summary>
public sealed class AsgardTheme : ThemePainter
{
    public override string Id => "asgard";

    static readonly (float y, Col c)[] SkyStops = Kit.Stops((-11, "6c88a6"), (-7, "52708e"), (-3, "3c5476"), (1, "2a3e60"), (5, "1c2c4c"), (10, "0d1632"));
    static readonly Ramp Bark = new("0c1020", "141a2c", "1d2438", "283048", "384260", "4e5a7c", "6c7a9a", "94a2bc");
    static readonly Ramp Canopy = new("14262e", "1c3640", "24484c", "2e5c58", "3e7466", "5a8e78", "7eaa8c", "a8c8a4");
    static readonly Ramp GoldRoof = new("4a3010", "6a4818", "8c6420", "ae8030", "cc9c3c", "e6b848", "f6d060", "ffe890");
    static readonly Ramp Timber = new("1c1412", "2a1e1a", "3a2a22", "4c382c", "604838", "765a46", "8c6e56", "a48868");
    static readonly Ramp Stone = new("161c2c", "202839", "2a364c", "384862", "4a5c78", "607490", "7e90a8", "a0b0c4");
    static readonly Ramp Iron = new("0e1016", "1a1c24", "282b36", "3a3e4a", "50545e", "6c7078");
    static readonly Ramp IceRamp = new("1a2c4c", "2a4470", "446890", "6c90b4", "9cbcd4", "cce0ec", "eef6fa");
    static readonly Col Snow = Col.Hex("e6eef8"), SnowShade = Col.Hex("b0c0dc"), SnowDeep = Col.Hex("7e90b8");
    static readonly Col RuneGlow = Col.Hex("5ae8ff"), RuneCore = Col.Hex("d8fcff");
    static readonly Col Ember = Col.Hex("ff8a34"), Flame = Col.Hex("ffc050"), FlameCore = Col.Hex("fff2c0");
    static readonly Col Gold = Col.Hex("e0b858"), GoldDark = Col.Hex("7a5a22");
    static readonly Col[] shieldA = { Col.Hex("a83a30"), Col.Hex("2e5e92"), Col.Hex("c8a040"), Col.Hex("d8d4c8") };
    static readonly Col[] shieldB = { Col.Hex("d8d4c8"), Col.Hex("c8a040"), Col.Hex("a83a30"), Col.Hex("262830") };

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return Sky();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    static Col FogCol(float y) => Kit.Smooth(SkyStops, y);

    PaintedLayer Sky()
    {
        var s = new LayerSpec("sky", .95f, 48, 30, -11, 21, -97) { Under = SkyStops[0].c, Over = SkyStops[^1].c };
        var c = s.NewCanvas(); var f = c.F;
        c.PaintAll((x, y, i, j) => Kit.Gradient(SkyStops, y, 4, 1, i, j));
        Kit.Stars(c, .5f, 10, .0045f, Col.Hex("e4ecff"), 7, (x, y) => M.Smooth(.5f, 4, y));
        // Aurora: a broad undulating ribbon, green core with violet fringes, quantised into dithered bands.
        c.Paint(0, c.Period, 1.5f, 10, (x, y, i, j) =>
        {
            float yc = 5.3f + f.Fbm(x, 0, 8, 3, 11) * 1.3f + MathF.Sin(x / c.Period * MathF.PI * 4 + 1) * .4f;
            float d = (y - yc) / 1.5f;
            if (d < -1.4f || d > 2.2f) return Col.Clear;
            float curtain = d < 0 ? MathF.Exp(-d * d * 3.2f) : MathF.Exp(-d * 1.1f);
            float rays = M.Clamp01(f.Fbm2(x, y, .5f, 5, 3, 13) * .8f + .6f);
            float width = .5f + .5f * f.N(x, 0, 7, 15);
            float a = curtain * rays * width * .8f;
            Col col = d < -.5f ? Col.Hex("b49cf4") : d < .35f ? Col.Hex("5ef0b0") : d < 1.1f ? Col.Hex("5ad4d8") : Col.Hex("9a8cf0");
            float q = MathF.Floor(a * 6 + Dither.Bayer(i, j)) / 6;
            return q <= 0 ? Col.Clear : col.WithA(q);
        }, add: true);
        // Snow peaks on the horizon: a far range in the haze and a nearer, darker one.
        float Ridge(float x, int seed, float baseY, float amp, (float x, float h, float w)[] peaks)
        {
            float t = baseY + f.Fbm(x, 0, 4, 3, seed) * amp;
            foreach (var p in peaks)
            {
                float d = MathF.Abs(f.Dx(x, p.x));
                t = MathF.Max(t, baseY + p.h * MathF.Max(0, 1 - d / p.w) + f.N(x, 0, .6f, seed + 1) * .12f);
            }
            return t;
        }
        void Range(int seed, float baseY, float amp, (float x, float h, float w)[] peaks, Col rock, Col lit, Col shade, float fogAmt)
        {
            c.Paint(0, c.Period, -11, baseY + 5, (x, y, i, j) =>
            {
                float top = Ridge(x, seed, baseY, amp, peaks);
                if (y > top) return Col.Clear;
                float slope = (Ridge(x + .12f, seed, baseY, amp, peaks) - Ridge(x - .12f, seed, baseY, amp, peaks)) / .24f;
                float depth = top - y, n = f.Fbm(x, y, 1.2f, 3, seed + 5);
                // Snow holds near the crest and in gullies; the faces below show rock in vertical streaks.
                float streak = f.Fbm2(x, y, .3f, 2.2f, 3, seed + 7);
                bool snowy = depth < .5f + n * .4f || streak < -.25f + depth * .05f;
                Col col = !snowy ? Col.Lerp(rock, shade, M.Clamp01(streak * .8f + .2f)) : slope > .15f + n * .3f ? lit : shade;
                float fogA = fogAmt + M.Smooth(top - .5f, top - 5, y) * .35f;
                return Kit.Mix(col, FogCol(y), fogA, i, j, 8);
            });
        }
        var farPeaks = new[] { (2.5f, 3.4f, 3.2f), (9f, 2.6f, 2.4f), (14.5f, 3.9f, 3.6f), (21f, 2.2f, 2.2f), (26.5f, 3.1f, 2.8f) };
        var nearPeaks = new[] { (5.5f, 1.6f, 2.6f), (17f, 1.3f, 2.2f), (24f, 1.9f, 3f) };
        Range(21, -1.6f, 1.2f, farPeaks, Col.Hex("405878"), Col.Hex("b8c8dc"), Col.Hex("7a90b0"), .5f);
        Range(31, -2.6f, .9f, nearPeaks, Col.Hex("2c3c58"), Col.Hex("96aac4"), Col.Hex("5a6e90"), .35f);
        // Ground haze.
        c.Paint(0, c.Period, -11, -1.5f, (x, y, i, j) =>
        {
            float a = M.Smooth(-1.5f, -6, y) * (.45f + f.Fbm(x, y * 2, 4, 3, 41) * .3f);
            float q = MathF.Floor(a * 5 + Dither.Bayer(i, j)) / 5;
            return q <= 0 ? Col.Clear : Col.Hex("8aa4c4").WithA(q);
        });
        return new PaintedLayer(s, c);
    }

    PaintedLayer Far()
    {
        var s = new LayerSpec("far", .85f, 64, 32, -11, 21, -96) { Over = Col.Lerp(Canopy[0], SkyStops[^1].c, .38f) };
        var c = s.NewCanvas(); var f = c.F;

        // ---- Yggdrasil: a colossal trunk with limbs spreading across the upper sky ----
        float tx = 14.2f;
        float TrunkX(float y) => tx + MathF.Sin(y * .28f + .6f) * .22f;
        float TrunkHalf(float y) => 1.4f - (y + 3) * .04f + (y < -1.5f ? (-1.5f - y) * (-1.5f - y) * .3f : 0);
        var tree = c.Blank();
        tree.Paint(tx - 6, tx + 6, -11, c.Top, (x, y, i, j) =>
        {
            float hw = TrunkHalf(y);
            float lobes = y < 0 ? MathF.Abs(MathF.Sin(x * 2.1f + y * .5f)) * M.Smooth(0, -3, y) * .5f : 0;
            hw += lobes + f.N(x, y, .8f, 51) * .12f;
            return MathF.Abs(x - TrunkX(y)) < hw ? Col.White : Col.Clear;
        });
        var limbs = new List<AsgardKit.Limb>(); var rng = new Rng(53);
        var tips = new List<(float x, float y)>();
        void Grow(float ax, float ay, float dirX, float len, float t0, float t1, int depth, float lift)
        {
            float bx = ax + dirX * len, by = ay + len * lift + rng.R(-.3f, .3f);
            float cx = ax + dirX * len * .5f, cy = ay + len * lift * .15f + rng.R(-.4f, .2f) - len * .05f;
            limbs.Add(AsgardKit.Bezier(ax, ay, cx, cy, bx, by, t0, t1));
            tips.Add((bx, by));
            if (depth >= 3) return;
            int kids = depth == 0 ? 3 : 2;
            for (int k = 0; k < kids; k++)
            {
                float t = rng.R(.35f, .85f);
                var p = AsgardKit.BezierAt(ax, ay, cx, cy, bx, by, t);
                float side = rng.Chance(.65f) ? dirX : -dirX;
                Grow(p.x, p.y, side, len * rng.R(.32f, .5f), M.Lerp(t0, t1, t) * .7f, Math.Max(.05f, t1 * .5f), depth + 1, lift + rng.R(.2f, .6f));
            }
        }
        foreach (var (y0, dir, len) in new[] { (3.6f, -1, 12f), (4.4f, 1, 9f), (5.3f, -1, 8f), (6.1f, 1, 11f), (6.8f, -1, 6f), (7.5f, 1, 6.5f), (8.2f, -1, 7.5f), (8.8f, 1, 4.5f), (9.4f, -1, 4f) })
            Grow(TrunkX(y0) + dir * TrunkHalf(y0) * .7f, y0, dir, len, .75f - y0 * .03f, .12f, 0, .32f);
        AsgardKit.PaintLimbs(tree, limbs, Col.White);
        Kit.Inflate(tree, 64, (i, j, src, h, n) =>
        {
            float x = tree.X(i), y = tree.Y(j);
            float ridge = f.Ridge(x * 2.2f + y * .15f, y * .45f, .5f, 3, 55);
            float v = .1f + Kit.Lambert(n.X, n.Y, n.Z) * .58f + (ridge - .5f) * .4f + f.Fbm(x, y, .9f, 2, 57) * .1f;
            var col = Bark.At(v, i, j, .4f);
            int up = tree.Depth(i, j, 0, 1, 4);
            if (up < 4 && n.Y > -.2f) col = up == 0 ? Snow : up < 2 ? SnowShade : Col.Lerp(col, SnowDeep, .5f);
            return col;
        }, (i, j) => f.Fbm2(tree.X(i), tree.Y(j), .3f, 1.6f, 3, 59) * 12);
        // Faintly glowing canopy at the limb tips and along the top.
        var canopy = c.Blank(); var crng = new Rng(61);
        foreach (var (px, py) in tips.Where(t => t.y > 6.2f))
            if (crng.Chance(.6f)) Kit.LeafClump(canopy, px, py + .1f, crng.R(.7f, 1.3f), crng.R(.45f, .8f), 70, Canopy, (int)(px * 31 + py * 7), .28f, .35f, .05f);
        canopy.Paint(0, c.Period, 9.6f, c.Top, (x, y, i, j) => y > 9.8f + f.N(x, 0, 1.5f, 63) * .3f ? Canopy[0] : Col.Clear);

        // ---- Valhalla: a longhall with a golden shingle roof, dragon finials and a shield wall ----
        float hx0 = 3.2f, hx1 = 10.7f, hc = (hx0 + hx1) * .5f, eave = -.55f, wallBase = -2.75f;
        float RidgeY(float x) { float u = (x - hc) / (hx1 - hx0) * 2; return 2.9f - u * u * .45f; }
        var hall = c.Blank();
        hall.Paint(hx0 - 1.2f, hx1 + 1.2f, -4, 4.5f, (x, y, i, j) =>
        {
            float ridge = RidgeY(Math.Clamp(x, hx0, hx1));
            if (x > hx0 - .45f && x < hx1 + .45f && y > eave && y < ridge)
            {
                float t = (y - eave) / (ridge - eave);
                if (y > ridge - .14f) return Kit.Mix(GoldDark, Gold, .45f + (y > ridge - .05f ? .4f : 0), i, j, 3);
                // Scalloped gilded shingles: small overlapping rows, each with a rounded lower edge.
                float rh = .13f, row = (y - eave) / rh, rowFrac = M.Fract(row); int rowI = (int)MathF.Floor(row);
                float cw = .17f, colOff = (rowI & 1) * cw * .5f, cu = M.Mod(x + colOff, cw) / cw;
                float edge = .1f + .22f * (1 - (cu - .5f) * (cu - .5f) * 4);
                float scale = rowFrac < edge ? -.3f : rowFrac < edge + .12f ? .12f : 0;
                if (MathF.Abs(cu - .5f) > .45f) scale -= .12f;
                float v = .55f + t * .25f + scale + (Field.Hash01(rowI, (int)MathF.Floor((x + colOff) / cw), 71) - .5f) * .12f;
                var col = GoldRoof.At(v, i, j, .35f);
                if (t > .65f) col = Kit.Mix(col, Col.Hex("9ae0b8"), (t - .65f) * .45f, i, j, 3);
                return col;
            }
            if (x > hx0 - .3f && x < hx1 + .3f && y > eave - .22f && y <= eave) return Timber.At(y > eave - .06f ? .55f : .3f, i, j, .3f);
            if (x >= hx0 && x <= hx1 && y >= wallBase && y <= eave - .22f)
            {
                float pw = .2f, pu = M.Mod(x - hx0, pw) / pw;
                float v = .38f + (pu < .12f ? -.2f : pu > .8f ? .1f : 0) + f.Fbm2(x, y, .05f, 1.2f, 2, 73) * .1f;
                if (y < wallBase + .2f) v = .25f;
                return Timber.At(v, i, j, .35f);
            }
            return Col.Clear;
        });
        hall.Paint(hc - .5f, hc + .5f, wallBase, -.95f, (x, y, i, j) =>
        {
            float dx = MathF.Abs(x - hc);
            float top = -1.25f + (dx < .4f ? MathF.Sqrt(Math.Max(0, .16f - dx * dx)) * .7f : 0);
            if (dx > .44f || y > top + .08f) return Col.Clear;
            if (dx > .4f || y > top) return Timber.At(.6f, i, j, .3f);
            return Kit.Mix(Col.Hex("ffd27a"), Col.Hex("c0602c"), M.InvLerp(wallBase, -1.1f, y) * .9f + dx * .5f, i, j, 5);
        });
        for (int k = 0; k < 10; k++)
        {
            float sx = hx0 + .55f + k * .72f, sy = -1.3f, r = .3f;
            if (MathF.Abs(sx - hc) < .75f) continue;
            int kind = (int)(Field.Hash01(k, 1, 75) * 4), pal = (int)(Field.Hash01(k, 2, 75) * 4);
            hall.Paint(sx - r, sx + r, sy - r, sy + r, (x, y, i, j) =>
            {
                int h = AsgardKit.Shield(x - sx, y - sy, r, kind);
                if (h == 0) return Col.Clear;
                float light = .85f + (-(x - sx) + (y - sy)) / r * .18f;
                Col col = h == 1 ? Iron[2] : h == 2 ? Iron[4] : h == 3 ? shieldA[pal] : shieldB[pal];
                if (h == 2 && x - sx < 0 && y - sy > 0) col = Iron[5];
                return (col * light).Clamp().WithA(1);
            });
        }
        foreach (var (fx, dir) in new[] { (hx0 - .35f, -1), (hx1 + .35f, 1) })
        {
            float fy = RidgeY(dir < 0 ? hx0 : hx1) - .2f;
            hall.Paint(fx - 1.2f, fx + 1.2f, fy - .2f, fy + 1.1f, (x, y, i, j) =>
            {
                int h = AsgardKit.Dragon(x, y, fx, fy, 1.05f, dir, 0, 0, 1, out float lx, out float ly);
                if (h == 0) return Col.Clear;
                if (h == 2) return Col.Hex("ffe8a0");
                if (h == 5) return Col.Hex("f4ecd8");
                float v = .5f + ly * .4f - lx * .15f + (h == 4 ? .1f : 0);
                return GoldRoof.At(v, i, j, .35f);
            });
        }

        // ---- Snowy plateau, the Bifrost and its gate ----
        var ground = c.Blank();
        ground.Paint(0, c.Period, -11, -2, (x, y, i, j) =>
        {
            float top = -2.7f + f.Fbm(x, 0, 3, 3, 81) * .18f;
            if (y > top) return Col.Clear;
            float d = top - y;
            if (d < .06f) return Snow;
            return Kit.Mix(SnowShade, SnowDeep, M.InvLerp(.06f, 1.6f, d) + f.Fbm2(x, y, 1.2f, .25f, 2, 83) * .2f, i, j);
        });
        var rainbow = c.Blank();
        float ArcY(float x) { float u = x + 3.8f; return 4.0f - .2535f * u * u; }
        Col[] bands = { Col.Hex("b084ff"), Col.Hex("5aa8ff"), Col.Hex("5cf0b8"), Col.Hex("f4f080"), Col.Hex("ffb460"), Col.Hex("ff7a7a") };
        rainbow.Paint(-10.5f, 1.6f, -7, 5.2f, (x, y, i, j) =>
        {
            float yc = ArcY(x), slope = (ArcY(x + .05f) - ArcY(x - .05f)) / .1f;
            float dist = (y - yc) / MathF.Sqrt(1 + slope * slope), T = .95f;
            if (MathF.Abs(dist) > T * .5f + .45f) return Col.Clear;
            float fade = M.Smooth(-10.5f, -7.5f, x) * M.Smooth(1.8f, .6f, x);
            float shimmer = .75f + .25f * f.N2(x, y, .5f, .3f, 85);
            if (MathF.Abs(dist) > T * .5f)
            {
                float halo = (1 - (MathF.Abs(dist) - T * .5f) / .45f) * .22f * fade * shimmer;
                float hq = MathF.Floor(halo * 5 + Dither.Bayer(i, j)) / 5;
                return hq <= 0 ? Col.Clear : Col.Hex("c8d8ff").WithA(hq);
            }
            int band = Math.Clamp((int)MathF.Floor((dist + T * .5f) / T * 6), 0, 5);
            float a = (.5f - MathF.Abs(band - 2.5f) * .05f) * fade * shimmer;
            float q = MathF.Floor(a * 6 + Dither.Bayer(i, j)) / 6;
            return q <= 0 ? Col.Clear : bands[band].WithA(q);
        });
        var gate = c.Blank();
        float gx = .4f;
        gate.Paint(gx - 1.3f, gx + 1.3f, -3.2f, 2.5f, (x, y, i, j) =>
        {
            float dx = x - gx, ax = MathF.Abs(dx);
            bool pier = MathF.Abs(ax - .72f) < .24f && y > -3 && y < 1.5f;
            bool lintel = ax < 1.1f && y > 1.35f && y < 1.75f;
            bool cap = y >= 1.75f && y < 2.25f && MathF.Abs(ax - .72f) < .3f * (1 - (y - 1.75f) / .5f);
            if (cap) return Kit.Mix(GoldDark, Gold, .5f + (dx < 0 ? .2f : 0), i, j, 3);
            if (!(pier || lintel)) return Col.Clear;
            float u = pier ? (ax - .72f) / .24f * MathF.Sign(dx) : 0;
            float v = .35f + Kit.Cylinder(u, .25f) * .4f + (lintel && y > 1.68f ? .15f : 0);
            bool mortar = Kit.Masonry(f, x, y, .5f, .3f, 1.5f / 64, 87, out float sh, out _, out _, out _);
            v += (sh - .5f) * .1f - (mortar ? .12f : 0);
            if (pier && y > -2.2f && y < 1.1f && AsgardKit.RuneColumn(x, y, gx + MathF.Sign(dx) * .72f, -2.2f, .26f, .33f, 89 + (dx > 0 ? 1 : 0), out _) < .14f) return RuneGlow.WithA(.8f);
            return Stone.At(v, i, j, .4f);
        });
        var pines = c.Blank();
        var needles = new Ramp("101a28", "162436", "1e3046", "283e54", "344c62");
        foreach (var (px, h) in new[] { (2.2f, 1.9f), (11.6f, 2.3f), (12.5f, 1.6f), (17.6f, 2.4f), (18.7f, 1.7f), (27.2f, 2.1f), (28.3f, 1.5f), (30.6f, 2.6f), (31.5f, 1.8f), (26.1f, 1.4f) })
            Kit.Pine(pines, px, -2.85f, h, needles, Snow, SnowShade, (int)(px * 37));

        Kit.Fog(tree, FogCol, y => .3f + M.Smooth(2, -5, y) * .25f);
        Kit.Fog(canopy, FogCol, y => .42f);
        Kit.Fog(hall, FogCol, y => .28f + M.Smooth(0, -4, y) * .14f);
        Kit.Fog(gate, FogCol, y => .4f);
        Kit.Fog(ground, FogCol, y => .45f + M.Smooth(-3, -7, y) * .4f);
        Kit.Fog(pines, FogCol, y => .48f);
        Kit.Glow(c, tx, 9.2f, 6, Col.Hex("7ac8a0"), .14f, .6f);
        c.Over(rainbow);
        c.Over(tree);
        c.Over(canopy);
        c.Over(ground);
        c.Over(hall);
        c.Over(gate);
        c.Over(pines);
        Kit.Glow(c, hc, wallBase + .4f, 1.6f, Col.Hex("ffb060"), .25f, .6f);
        Kit.Glow(c, gx, -.6f, 1.8f, Col.Hex("b0c8ff"), .18f);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Mid()
    {
        var s = new LayerSpec("mid", .65f, 96, 36, -9.5f, 22, -95);
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(201);
        Col fogCol = Col.Hex("2e4466");
        float groundY = -3.5f;
        var pillarsX = new[] { 1.5f, 7.5f, 13.5f, 19.5f, 25.5f, 31.5f };
        var pillarTop = new[] { 7.3f, 8.1f, 7.6f, 8.3f, 7.4f, 7.9f };

        // ---- Rune pillars: stone plinth and drum, iron-banded timber post, carved bracket capital ----
        var post = c.Blank();
        for (int k = 0; k < pillarsX.Length; k++)
        {
            float px = pillarsX[k], top = pillarTop[k]; int seed = 211 + k * 7;
            post.Paint(px - .9f, px + .9f, groundY - .2f, top + .1f, (x, y, i, j) =>
            {
                float dx = x - px, ax = MathF.Abs(dx);
                if (y < -2.1f)
                {
                    float hw = y < -2.3f ? .68f : .68f - (y + 2.3f) * .9f;
                    if (ax > hw) return Col.Clear;
                    float v = .42f + (y > -2.32f ? .22f : 0) - dx * .12f + f.Fbm(x, y, .5f, 3, seed) * .08f;
                    bool mortar = Kit.Masonry(f, x, y, .7f, .4f, 1.6f / 96, seed + 1, out float sh, out _, out _, out _);
                    v += (sh - .5f) * .08f - (mortar ? .14f : 0);
                    if (y > -3.2f && y < -2.75f && ax < .55f) v += MathF.Abs(MathF.Sin(x * 7 + MathF.Sin(y * 14))) < .3f ? -.18f : .06f;
                    return Stone.At(v, i, j, .4f);
                }
                if (y < -1.0f)
                {
                    float r = .5f; if (ax > r) return Col.Clear;
                    float u = dx / r, v = .3f + Kit.Cylinder(u, .2f) * .5f + f.Fbm(x, y, .4f, 2, seed + 2) * .06f;
                    if (y > -1.12f || y < -2.0f) v += .12f;
                    return Stone.At(v, i, j, .4f);
                }
                float capY = top - .55f;
                if (y >= capY)
                {
                    float t = (y - capY) / .55f, hwc = .62f * (t < .25f ? .55f + t * 1.8f : 1) - (y > top - .08f ? .1f : 0);
                    if (ax > hwc || y > top) return Col.Clear;
                    float v = .42f + (t > .85f ? .2f : 0) - dx * .18f;
                    if (t > .3f && t < .8f && M.Mod(ax + t * .6f, .22f) < .07f) v -= .18f;
                    return Timber.At(v, i, j, .35f);
                }
                float rr = .37f - (y + 1) * .012f; if (ax > rr) return Col.Clear;
                float uu = dx / rr;
                float grain = f.Fbm2(x, y, .08f, 1.6f, 2, seed + 3) * .1f;
                float vv = .18f + Kit.Cylinder(uu, .22f) * .6f + grain;
                float band = M.Mod(y + 1, 1.7f);
                if (band < .16f) return Iron.At(.3f + Kit.Cylinder(uu, .3f) * .5f + (band < .03f || band > .13f ? -.15f : 0), i, j, .4f);
                return Timber.At(vv, i, j, .35f);
            });
        }
        // Glowing runes carved between the iron bands.
        var runes = c.Blank();
        for (int k = 0; k < pillarsX.Length; k++)
        {
            float px = pillarsX[k], top = pillarTop[k]; int seed = 231 + k * 3;
            runes.Paint(px - .4f, px + .4f, -.8f, top - .7f, (x, y, i, j) =>
            {
                int seg = (int)MathF.Floor((y + 1) / 1.7f); float local = y + 1 - seg * 1.7f;
                if (local < .24f || local > .24f + 1.38f) return Col.Clear;
                float d = AsgardKit.RuneColumn(x, y, px - .02f, -1 + seg * 1.7f + .24f, .44f, .46f, seed + seg, out int row);
                if (Field.Hash01(row, seg, seed) < .28f) return Col.Clear; // some cells were left uncarved
                if (d < .05f) return RuneCore;
                if (d < .15f) return RuneGlow;
                if (d < .42f) { float a = (1 - (d - .15f) / .27f) * .5f; float q = MathF.Floor(a * 4 + Dither.Bayer(i, j)) / 4; return q <= 0 ? Col.Clear : RuneGlow.WithA(q); }
                return Col.Clear;
            });
        }

        // ---- Crossbeams with hanging banners ----
        var beams = c.Blank(); var banners = c.Blank();
        var bannerCols = new[] { (Col.Hex("7a2630"), Col.Hex("4a1620")), (Col.Hex("263c74"), Col.Hex("16244a")) };
        for (int k = 0; k < pillarsX.Length; k += 2)
        {
            float xa = pillarsX[k], xb = pillarsX[k + 1], by = Math.Min(pillarTop[k], pillarTop[k + 1]) - .25f;
            beams.Paint(xa, xb, by - .34f, by + .02f, (x, y, i, j) =>
            {
                float t = (y - (by - .34f)) / .36f;
                float v = .3f + (t > .85f ? .22f : t < .15f ? -.1f : 0) + f.Fbm2(x, y, 1.2f, .06f, 2, 241 + k) * .1f;
                float strap = M.Mod(x - xa, 1.5f);
                if (strap < .12f) return Iron.At(.35f + (strap < .02f || strap > .1f ? -.15f : 0), i, j, .4f);
                return Timber.At(v, i, j, .35f);
            });
            for (int b = 0; b < 2; b++)
            {
                float bx = M.Lerp(xa, xb, b == 0 ? .3f : .7f), bw = .95f, topY = by - .36f, len = 2.7f;
                var (fieldA, fieldB) = bannerCols[(k / 2 + b) % 2]; int emblem = (k / 2 + b) % 2; int bseed = 251 + k * 5 + b;
                banners.Paint(bx - bw, bx + bw, topY - len - .2f, topY + .05f, (x, y, i, j) =>
                {
                    float t = (topY - y) / len; if (t < 0 || t > 1.08f) return Col.Clear;
                    float wave = MathF.Sin(t * 6.5f + bseed) * .06f * t + f.N(0, y, 1.5f, bseed) * .03f;
                    float dx = x - bx - wave, ax = MathF.Abs(dx);
                    float hw = bw * .5f * (1 - t * .08f);
                    if (ax > hw) return Col.Clear;
                    float tail = 1 - .18f * (1 - ax / hw);
                    if (t > tail + .08f * MathF.Abs(MathF.Sin(ax * 30))) return Col.Clear;
                    if (t < .05f) return Timber.At(.5f, i, j, .3f);
                    float fold = MathF.Sin(dx * 9 + t * 3 + bseed) * .5f + .5f;
                    bool border = ax > hw - .07f || t > tail - .07f;
                    Col col = border ? Kit.Mix(GoldDark, Gold, .4f + fold * .3f, i, j, 3) : Kit.Mix(fieldB, fieldA, .35f + fold * .5f, i, j, 4);
                    float eu = dx / (hw * .62f), ev = -(t - .5f) / .32f;
                    if (!border && MathF.Abs(eu) < 1 && MathF.Abs(ev) < 1 && AsgardKit.Emblem(eu, ev, emblem, .11f)) col = Kit.Mix(GoldDark, Gold, .5f + fold * .3f, i, j, 3);
                    return col;
                });
            }
        }

        // ---- Braziers, rune stones ----
        var props = c.Blank();
        var fires = new List<(float x, float y)>();
        foreach (var bx in new[] { 4.5f, 16.5f, 28.5f })
        {
            float bowlY = -2.15f, rx = .46f, ry = .26f;
            props.Paint(bx - .8f, bx + .8f, groundY - .1f, bowlY + .2f, (x, y, i, j) =>
            {
                float dx = x - bx;
                if (y <= bowlY && y > bowlY - ry)
                {
                    float u = dx / rx, v = (bowlY - y) / ry;
                    if (u * u + v * v < 1) return Iron.At((y > bowlY - .05f ? .65f : .25f + Kit.Cylinder(u, .25f) * .35f) - v * .15f, i, j, .4f);
                }
                if (y > bowlY && y < bowlY + .07f && MathF.Abs(dx) < rx + .04f) return Iron.At(.7f - MathF.Abs(dx) * .3f, i, j, .3f);
                foreach (var lx in new[] { -.3f, .3f, 0f })
                {
                    float tt = (bowlY - ry * .6f - y) / (bowlY - ry * .6f - groundY);
                    if (tt < 0 || tt > 1) continue;
                    float cx = bx + lx * (1 + tt * .8f);
                    if (MathF.Abs(x - cx) < (lx == 0 ? .045f : .05f) + (tt > .9f ? .04f : 0)) return Iron.At(lx == 0 ? .15f : .35f, i, j, .3f);
                }
                return Col.Clear;
            });
            fires.Add((bx, bowlY));
        }
        foreach (var (sx, h, w) in new[] { (10.5f, 1.6f, .62f), (22.5f, 1.3f, .55f), (34.5f, 1.9f, .7f), (9.7f, .9f, .4f) })
        {
            int seed = (int)(sx * 17);
            props.Paint(sx - w, sx + w, groundY - .1f, groundY + h + .1f, (x, y, i, j) =>
            {
                float t = (y - groundY) / h, dx = x - sx;
                if (t < 0 || t > 1) return Col.Clear;
                float env = 1 - t * .12f;
                if (t > .75f) { float q = (t - .75f) / .25f; env *= MathF.Sqrt(Math.Max(0, 1 - q * q)); }
                float hw = w * env + f.N(x, y, .3f, seed) * .04f;
                if (MathF.Abs(dx) > hw) return Col.Clear;
                float u = dx / Math.Max(hw, .01f);
                float v = .25f + Kit.Cylinder(u, .2f) * .45f + f.Fbm(x, y, .4f, 3, seed + 1) * .1f;
                if (f.Ridge(x, y, .8f, 2, seed + 2) > .92f) v -= .15f;
                var col = Stone.At(v, i, j, .4f);
                float d = AsgardKit.RuneColumn(x, y, sx, groundY + .25f, .3f * w / .6f, .34f, seed + 3, out _);
                if (t > .12f && t < .82f && MathF.Abs(u) < .55f)
                {
                    if (d < .1f) col = Col.Lerp(RuneGlow, RuneCore, .5f);
                    else if (d < .22f) col = RuneGlow;
                    else if (d < .45f) col = Col.Lerp(col, RuneGlow, (1 - (d - .22f) / .23f) * .35f);
                }
                if (t > .9f) col = Dither.Hit(.6f, i, j) ? Snow : SnowShade;
                return col;
            });
        }

        // Shields and spears leaning against two of the plinths.
        foreach (var (px, flip) in new[] { (7.5f, 1), (19.5f, -1), (31.5f, 1) })
        {
            float sx = px + flip * 1.0f, sy = groundY + .46f, r = .46f; int kind = (int)(px * 3) % 4, pal = (int)(px) % 4;
            props.Paint(sx - r, sx + r, sy - r, sy + r, (x, y, i, j) =>
            {
                int h = AsgardKit.Shield((x - sx) / .92f, y - sy, r, kind);
                if (h == 0) return Col.Clear;
                float light = .8f + (-(x - sx) + (y - sy)) / r * .2f;
                Col col = h == 1 ? Iron[2] : h == 2 ? Iron[4] : h == 3 ? shieldA[pal] : shieldB[pal];
                if (h == 2 && x - sx < 0 && y - sy > 0) col = Iron[5];
                return (col * light).Clamp().WithA(1);
            });
            float ex = px - flip * .85f, ey0 = groundY, ex1 = px - flip * .5f, ey1 = groundY + 3.1f;
            props.Paint(Math.Min(ex, ex1) - .2f, Math.Max(ex, ex1) + .2f, ey0, ey1 + .55f, (x, y, i, j) =>
            {
                float d = AsgardKit.SegDist(x, y, ex, ey0, ex1, ey1);
                if (d < .028f) return Timber.At(.55f + (x < ex1 ? .1f : -.1f), i, j, .3f);
                float hy = y - ey1, hx = x - ex1 - flip * -.11f * hy;
                if (hy > -.05f && hy < .5f && MathF.Abs(hx) < .09f * MathF.Sin(MathF.PI * Math.Min(1, (hy + .05f) / .55f)) + .01f) return Iron.At(.5f + (hx < 0 ? .25f : 0), i, j, .3f);
                return Col.Clear;
            });
        }
        // ---- Snowy pines behind the colonnade, ravens, drifts ----
        var trees = c.Blank();
        var needles = new Ramp("0c1620", "12202e", "1a2c3c", "24394a", "30485a");
        foreach (var (px, h) in new[] { (3.2f, 3.6f), (4.6f, 2.4f), (9.1f, 4.2f), (10.0f, 2.8f), (15.4f, 3.2f), (17.6f, 4.6f), (22.2f, 3.0f), (23.4f, 4.4f), (27.4f, 3.4f), (29.6f, 2.6f), (33.2f, 4.0f), (34.9f, 2.9f) })
            Kit.Pine(trees, px, groundY - .2f, h, needles, Snow, SnowShade, (int)(px * 41));
        var birds = c.Blank();
        void Raven(float ox, float oy, float size, int dir)
        {
            birds.Paint(ox - size, ox + size, oy - .05f, oy + size * .7f, (x, y, i, j) =>
                AsgardKit.RavenPerched(x, y, ox, oy, size, dir) ? (Dither.Hit(.15f, i, j) ? Col.Hex("1c2434") : Col.Hex("0c1018")) : Col.Clear);
        }
        Raven(7.75f, pillarTop[1], .62f, -1); Raven(19.3f, pillarTop[3], .58f, 1); Raven(4.6f, pillarTop[0] - .23f, .55f, 1); Raven(28.3f, pillarTop[4] - .23f, .55f, -1);
        foreach (var (fx, fy, flap, sz) in new[] { (11f, 10.2f, .4f, .8f), (14.5f, 9.3f, -.5f, .65f), (31f, 10.8f, .1f, .7f) })
            birds.Paint(fx - sz, fx + sz, fy - sz * .5f, fy + sz * .5f, (x, y, i, j) => AsgardKit.RavenFlying(x, y, fx, fy, sz, flap) ? Col.Hex("0c1018") : Col.Clear);
        var drift = c.Blank();
        drift.Paint(0, c.Period, -9.6f, -2.8f, (x, y, i, j) =>
        {
            float top = -3.45f + f.Fbm(x, 0, 2.5f, 3, 271) * .5f;
            if (y > top) return Col.Clear;
            return top - y < .05f ? Snow : Kit.Mix(SnowShade, SnowDeep, M.InvLerp(top - .05f, top - 1.6f, y), i, j);
        });

        Kit.Fog(trees, y => fogCol, y => .3f);
        Kit.Fog(post, y => fogCol, y => .1f + M.Smooth(-3, -9, y) * .4f);
        Kit.Fog(props, y => fogCol, y => .12f + M.Smooth(-3, -9, y) * .4f);
        Kit.Fog(beams, y => fogCol, y => .1f);
        Kit.Fog(banners, y => fogCol, y => .14f);
        c.Over(trees);
        c.Over(post);
        c.Over(runes);
        c.Over(beams);
        c.Over(banners);
        c.Over(props);
        c.Over(birds);
        c.Over(drift);
        // Rune light and firelight on the surroundings, then the flames and their smoke.
        for (int k = 0; k < pillarsX.Length; k++)
            for (float gy = -.3f; gy < pillarTop[k] - 1; gy += 1.7f) Kit.Glow(c, pillarsX[k], gy + .5f, .9f, RuneGlow, .16f, 1.4f);
        foreach (var (fx, fy) in fires)
        {
            Kit.Light(c, fx, fy + .3f, 3.2f, Ember, .5f);
            Kit.Glow(c, fx, fy + .4f, 1.7f, Ember, .4f, .8f);
            Kit.Glow(c, fx, fy + .35f, .5f, Flame, .6f, 1.2f, 3);
            c.Paint(fx - .6f, fx + .6f, fy, fy + 1.2f, (x, y, i, j) =>
            {
                int h = AsgardKit.Flame(f, x, y, fx, fy - .02f, 1.1f, .68f, (int)(fx * 13));
                return h == 0 ? Col.Clear : h == 3 ? FlameCore : h == 2 ? Flame : Ember;
            });
            c.Paint(fx - 1.4f, fx + 1.4f, fy + 1, fy + 5, (x, y, i, j) =>
            {
                float rise = y - fy - 1, sway = MathF.Sin(rise * 1.3f + fx) * .12f * rise;
                float w = .25f + rise * .18f, d = MathF.Abs(x - fx - sway) / w;
                float n = f.Fbm(x, y - rise * .2f, .6f, 3, 261) * .5f + .5f;
                float a = ((1 - d) * n * 1.2f - .3f) * M.Smooth(4.5f, 1, rise) * .35f;
                float q = MathF.Floor(a * 4 + Dither.Bayer(i, j)) / 4;
                return q <= 0 ? Col.Clear : Col.Hex("1a2030").WithA(q);
            });
        }
        Kit.Stars(c, -3.4f, 12.4f, .0008f, Col.Hex("e8f0ff"), 281, (x, y) => .3f + M.Smooth(5.5f, 8, y) * .4f);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Near()
    {
        var s = new LayerSpec("near", .40f, 96, 40, 2, 12.5f, -94) { Over = Col.Hex("0a0c12") };
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(401);
        var dark = new Ramp("0a0c12", "121420", "1a1a28", "242230", "302a34", "3e3638", "4e4440", "62564a");
        float fasciaBottom = 10.2f, fasciaTop = 10.95f;
        // ---- Ceiling planks and the carved fascia of the longhall eaves ----
        var wood = c.Blank();
        wood.Paint(0, c.Period, fasciaTop, c.Top, (x, y, i, j) =>
        {
            float row = (y - fasciaTop) / .5f, fr = M.Fract(row); int r = (int)MathF.Floor(row);
            float grain = f.Fbm2(x, y, 2.5f, .05f, 3, 403 + r) * .12f;
            float v = .2f + grain - M.Smooth(fasciaTop, fasciaTop + 2.5f, y) * .12f;
            if (fr < .07f) v -= .15f; else if (fr > .93f) v += .06f;
            if (M.Mod(x + r * 1.7f, 4.3f) < .03f) v -= .15f;
            return Kit.Grain(dark.At(v, i, j, .4f), i, j, 405, .04f, .15f);
        });
        wood.Paint(0, c.Period, fasciaBottom, fasciaTop, (x, y, i, j) =>
        {
            float yc = (fasciaBottom + fasciaTop) * .5f, amp = .22f, k = MathF.PI * 2 / 1.6f;
            float d = Math.Min(MathF.Abs(y - (yc + amp * MathF.Sin(x * k))), MathF.Abs(y - (yc - amp * MathF.Sin(x * k))));
            float v = .28f + f.Fbm2(x, y, 2, .08f, 2, 407) * .06f;
            if (y > fasciaTop - .05f || y < fasciaBottom + .05f) v -= .12f;
            else if (d < .05f) v += .3f; else if (d < .08f) v += .12f; else if (d < .1f) v -= .1f;
            if (MathF.Abs(M.Mod(x, 1.6f) - .8f) < .06f && MathF.Abs(y - yc) < .06f) return Kit.Mix(GoldDark, Gold, .5f + (y > yc ? .3f : 0), i, j, 3);
            return dark.At(v, i, j, .4f);
        });
        // ---- Rafter ends and carved dragon beams hanging below the fascia ----
        var beams = c.Blank();
        for (int k = 0; k < 16; k++)
        {
            float bx = .5f + k * 2.5f;
            if (M.Mod(bx - 3, 10) < .1f) continue;
            float w = .3f;
            beams.Paint(bx - w, bx + w, fasciaBottom - .4f, fasciaBottom + .02f, (x, y, i, j) =>
            {
                float dx = x - bx; if (MathF.Abs(dx) > w) return Col.Clear;
                float v = .25f + (dx < -w * .5f ? .12f : dx > w * .5f ? -.08f : 0) + (y < fasciaBottom - .34f ? -.12f : 0);
                return dark.At(v, i, j, .4f);
            });
        }
        var carved = Timber.Shift(-.22f);
        foreach (var bx in new[] { 3f, 13f, 23f, 33f })
        {
            int dir = ((int)bx / 10) % 2 == 0 ? -1 : 1;
            float rootY = 9.0f, sz = 1.7f;
            float ax = -.156f * dir, ay = -.988f, bxv = .988f * dir, byv = -.156f;
            beams.Paint(bx - 2.4f, bx + 2.4f, rootY - 2.1f, fasciaBottom + .02f, (x, y, i, j) =>
            {
                float dx = x - bx;
                if (y >= rootY && MathF.Abs(dx) < .32f)
                {
                    float u = dx / .32f, v = .3f + Kit.Cylinder(u, .25f) * .5f + f.Fbm2(x, y, .06f, 1.4f, 2, 421) * .08f;
                    // Carved interlace: crossing diagonal grooves with a gold collar at the foot.
                    float g1 = M.Mod(y - rootY + dx * 1.2f, .42f), g2 = M.Mod(y - rootY - dx * 1.2f, .42f);
                    if (g1 < .05f || g2 < .05f) v -= .2f; else if (g1 < .1f || g2 < .1f) v += .12f;
                    if (y < rootY + .16f) return Kit.Mix(GoldDark, Gold, .4f + Kit.Cylinder(u, .3f) * .3f, i, j, 3);
                    return carved.At(v, i, j, .4f);
                }
                int h = AsgardKit.Dragon(x, y, bx, rootY + .05f, sz, ax, ay, bxv, byv, out float lx, out float ly);
                if (h == 0) return Col.Clear;
                if (h == 2) return Col.Hex("ffd870");
                if (h == 5) return Col.Hex("e8e0d0");
                float vv = .36f + ly * .3f - lx * .12f + (h == 4 ? .1f : 0) + f.Fbm(x, y, .3f, 2, 423) * .08f;
                // Carved scales along the skull.
                if (h == 1 && lx > .25f && lx < .75f && ly > .5f && M.Fract(lx * 9 + MathF.Floor(ly * 9) * .5f) < .12f) vv -= .14f;
                return carved.At(vv, i, j, .4f);
            });
        }
        Kit.Edge(beams, -1, 0, 1, (col, d, i, j) => Col.Lerp(col, Col.Hex("a89478"), .35f));
        Kit.Edge(beams, 1, 0, 1, (col, d, i, j) => Col.Lerp(col, Col.Hex("0a0c12"), .4f));
        Kit.Edge(beams, 0, -1, 1, (col, d, i, j) => Col.Lerp(col, Col.Hex("7a7a90"), .3f));
        // ---- Icicles along the eaves ----
        var ice = c.Blank();
        var spikes = new List<(float x, float len, float w)>();
        for (int k = 0; k < 70; k++) { float x = rng.R(0, 40); if (rng.Chance(.75f)) spikes.Add((x, rng.R(.25f, 1.6f), rng.R(.08f, .2f))); }
        foreach (var (x, len, w) in new[] { (7.2f, 4.4f, .5f), (18.6f, 3.6f, .42f), (36.4f, 4.9f, .55f), (26.5f, 3.2f, .4f), (11.4f, 2.6f, .32f) })
        {
            spikes.Add((x, len, w));
            for (int k = 0; k < 4; k++) { float ox = rng.R(-.6f, .6f); spikes.Add((x + ox, len * rng.R(.3f, .65f), w * rng.R(.35f, .6f))); }
        }
        ice.Paint(0, c.Period, fasciaBottom - 5.2f, fasciaBottom + .05f, (x, y, i, j) =>
        {
            foreach (var sp in spikes)
            {
                float dx = f.Dx(x, sp.x);
                if (MathF.Abs(dx) < sp.w && Kit.Spike(f, sp.x + dx, y, sp.x, fasciaBottom + .04f, sp.len, sp.w, -1, 411, out float u, out float t, .1f, 1.3f))
                    return IceRamp.At(.35f + (1 - MathF.Abs(u + .3f)) * .4f - t * .15f + f.N(x, y, .12f, 413) * .12f, i, j, .4f).WithA(.95f);
            }
            return Col.Clear;
        });
        Kit.Edge(ice, -1, 0, 1, (col, d, i, j) => Col.Lerp(col, Col.Hex("e8f4fa"), .5f));
        // ---- Iron lanterns on chains ----
        var lamps = c.Blank();
        var lampX = new[] { 8.2f, 18.0f, 28.4f, 38.2f };
        foreach (var lx in lampX)
        {
            float ly = 8.1f;
            lamps.Paint(lx - .35f, lx + .35f, ly - .45f, fasciaBottom, (x, y, i, j) =>
            {
                float dx = x - lx, ax = MathF.Abs(dx);
                if (y > ly + .3f) { bool link = M.Mod(y, .12f) < .08f; return link ? (ax < .025f ? Iron[3] : Col.Clear) : (ax < .04f ? Iron[2] : Col.Clear); }
                if (y > ly + .2f) return ax < .08f ? Iron[2] : Col.Clear;
                if (y > ly - .3f)
                {
                    if (ax > .22f) return Col.Clear;
                    bool frame = ax > .18f || MathF.Abs(y - ly) < .02f || ax < .02f || y > ly + .15f || y < ly - .25f;
                    return frame ? Iron.At(.35f + (dx < 0 ? .15f : 0), i, j, .3f) : Kit.Mix(Col.Hex("ffc864"), Col.Hex("fff4d0"), 1 - ax / .18f, i, j, 3);
                }
                return ax < .1f * (1 - (ly - .3f - y) / .15f) + .02f ? Iron[2] : Col.Clear;
            });
        }
        c.Over(wood);
        c.Over(beams);
        c.Over(ice);
        c.Over(lamps);
        foreach (var lx in lampX)
        {
            Kit.Light(c, lx, 8.1f, 2.6f, Ember, .55f);
            Kit.Glow(c, lx, 8.1f, 1.4f, Col.Hex("ffb060"), .4f);
        }
        return new PaintedLayer(s, c);
    }
}
