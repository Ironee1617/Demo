namespace BackdropPainter;

/// <summary>Garden of Eden in soft holy light: the luminous Tree of Life on its hill with waterfalls feeding
/// four rivers, flowering meadows and groves, a lily pool with reflections, light motes and butterflies,
/// and an overhead canopy of blossoms with hanging vines and fruit.</summary>
public sealed class EdenTheme : ThemePainter
{
    public override string Id => "eden";

    static readonly (float y, Col c)[] SkyStops = Kit.Stops((-11, "d6dcbc"), (-5, "ece8c8"), (-2, "fdf3d6"), (.5f, "fff6dc"), (2.5f, "eef0dc"), (4.5f, "c0dcee"), (7.5f, "8ec4ea"), (10, "74b4e4"));
    static readonly Ramp CloudRamp = new("b8cce4", "cad8ea", "dce4ee", "ecece8", "f6f2e6", "fdf9ee", "fffdf6");
    static readonly Ramp FarHill = new("9cc4b8", "a8ccb8", "b4d4bc", "c2dcc4", "d0e4cc");
    static readonly Ramp MidHill = new("5c9c58", "6aaa5e", "7ab868", "8cc674", "a0d282", "b6dc92");
    static readonly Ramp Grass = new("3e7c3a", "4c8c42", "5c9e4c", "6eb058", "84c066", "9ad076", "b2de8a", "cce89e");
    static readonly Ramp LifeLeaf = new("6c9a42", "82b04c", "9ac45a", "b4d66a", "ccE67c", "e0f094", "f0f8b2", "fffbd4");
    static readonly Ramp PaleBark = new("8a7c68", "a69880", "c2b69a", "d8ceb4", "e8e0cc", "f6f0e0", "fffaf0");
    static readonly Ramp Leaf = new("2e5a2c", "3a7034", "4a8a3e", "60a04a", "78b858", "94cc68", "b2de80", "d2ee9c");
    static readonly Ramp NearLeaf = new("1e4028", "285434", "336840", "407c4a", "509256", "64a864", "7ec076", "9cd48c", "bce6a4");
    static readonly Ramp Rock = new("8a8678", "a09c8c", "b6b2a2", "ccc8b6", "dedac8", "ece8d8");
    static readonly Col WaterDeep = Col.Hex("2e9c9c"), WaterShallow = Col.Hex("8ee2da"), WaterGlint = Col.Hex("f0fffa"), Gold = Col.Hex("ffd870"), GoldPale = Col.Hex("fff2c0");
    static readonly Col[] Pastels = { Col.Hex("f6b8cc"), Col.Hex("fff4ee"), Col.Hex("d8c0f0"), Col.Hex("ffe08a"), Col.Hex("b8d8f8") };

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return Sky();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    static Col SkyAt(float y) => Kit.Smooth(SkyStops, y);

    PaintedLayer Sky()
    {
        var s = new LayerSpec("sky", .95f, 48, 30, -11, 21, -97) { Under = SkyStops[0].c, Over = SkyStops[^1].c };
        var c = s.NewCanvas(); var f = c.F;
        c.PaintAll((x, y, i, j) => Kit.Gradient(SkyStops, y, 4, 1, i, j));
        // Two ridges of distant hills dissolving into the golden horizon.
        foreach (var (lift, size, tone, seed) in new[] { (-.9f, 9f, .16f, 9), (-1.5f, 5.5f, .24f, 11) })
            c.Paint(0, c.Period, -11, 1.5f, (x, y, i, j) =>
            {
                float ridge = lift + f.Fbm(x, 0, size, 3, seed) * .9f;
                if (y > ridge) return Col.Clear;
                float t = tone * (.6f + .4f * M.Smooth(ridge, ridge - 1.5f, y));
                return Col.Lerp(Kit.Gradient(SkyStops, y, 4, 1, i, j), Col.Hex("b4d4c8"), t);
            });
        // Celestial glow at the source of the light, upper left.
        Kit.Glow(c, 3, 6.4f, 9.5f, Col.Hex("fff8e2"), .5f, .6f);
        Kit.Glow(c, 3, 6.8f, 3.8f, Col.Hex("fffdf2"), .55f, .75f);
        // Soft cumulus and cirrus, few and luminous.
        foreach (var (x0, x1, baseY, h, seed) in new[] { (-3.5f, 3.5f, 4.9f, .8f, 15), (8.5f, 14.5f, 3.3f, .7f, 17), (14f, 19.5f, 5.5f, .6f, 19), (22f, 27f, 4.4f, .6f, 21) })
            Kit.CloudBank(c, x0, x1, baseY, h, .35f, .9f, 10, CloudRamp, seed, .22f, .92f);
        c.Paint(0, c.Period, 2.5f, 7.5f, (x, y, i, j) =>
        {
            float n = f.Fbm2(x, y, 7, .45f, 4, 23);
            float a = M.Smooth(.28f, .62f, n) * .3f * M.Smooth(2.5f, 4, y);
            float q = MathF.Floor(a * 5 + Dither.Bayer(i, j)) / 5;
            return q <= 0 ? Col.Clear : Col.Hex("fffaf0").WithA(q);
        });
        // God rays leaning from the upper left.
        Kit.Shafts(c, 22, 2.7f, .32f, Col.Hex("fff3cc"), .38f, -3, 10, 13, (x, y) => .35f + .65f * M.Smooth(15, 2, MathF.Abs(f.Dx(x, 4))));
        // Far right: the gate of the garden with the flaming sword's small warm flare.
        float gx = 16.6f;
        c.Paint(gx - 1, gx + 1, -2.4f, 1.2f, (x, y, i, j) =>
        {
            float dx = MathF.Abs(x - gx) - .62f;
            if (MathF.Abs(dx) < .13f && y < .7f + (dx < 0 ? .25f : 0)) return Col.Lerp(Kit.Gradient(SkyStops, y, 4, 1, i, j), Col.Hex("c8b89c"), .35f);
            if (MathF.Abs(x - gx) < .9f && y > .7f && y < .95f) return Col.Lerp(Kit.Gradient(SkyStops, y, 4, 1, i, j), Col.Hex("c8b89c"), .3f);
            return Col.Clear;
        });
        Kit.Glow(c, gx, -.3f, 2.4f, Col.Hex("ffb860"), .32f, 1.3f);
        c.Paint(gx - .3f, gx + .3f, -1.3f, .6f, (x, y, i, j) =>
        {
            float t = M.InvLerp(-1.3f, .6f, y), hw = .16f * MathF.Sin(MathF.PI * MathF.Min(1, t * 1.05f)) * (1 - t * .5f) + .01f;
            float dx = MathF.Abs(x - gx - f.N(0, y, .5f, 25) * .05f * t);
            if (dx > hw) return Col.Clear;
            return dx < hw * .45f ? Col.Hex("fff6d0") : Col.Hex("ffb850");
        });
        Kit.Glow(c, gx, -.4f, .8f, Col.Hex("fff0c0"), .9f, 1.5f);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Far()
    {
        var s = new LayerSpec("far", .85f, 64, 32, -11, 21, -96);
        var c = s.NewCanvas(); var f = c.F;
        const float TX = 9f;
        float Dome(float x) { float d = f.Dx(x, TX) / 7.6f; return MathF.Abs(d) < 1 ? -1.6f + 2.75f * MathF.Pow(1 - d * d, 1.35f) : -1.6f; }
        float HillA(float x) => .15f + f.Fbm(x, 0, 8, 3, 31) * 1.1f;
        float HillB(float x) => -.8f + f.Fbm(x, 0, 5.5f, 3, 33) * .7f;
        float Meadow(float x) => -1.3f + f.Fbm(x, 0, 3, 3, 35) * .26f;

        // Farthest hills: pale, with a lit rim and faint field patterns.
        var hillsA = c.Blank();
        hillsA.Paint(0, c.Period, -11, 2.5f, (x, y, i, j) =>
        {
            float top = HillA(x); if (y > top) return Col.Clear;
            float v = .55f + (top - y < .08f ? .4f : 0) - M.Smooth(top, top - 2.5f, y) * .3f + f.Fbm(x, y, 1.5f, 2, 37) * .12f;
            return FarHill.At(v, i, j, .4f);
        });
        Kit.Fog(hillsA, SkyAt, y => .5f);
        c.Over(hillsA);
        // Middle hills and the Tree of Life's dome.
        var hillsB = c.Blank();
        hillsB.Paint(0, c.Period, -11, 2, (x, y, i, j) =>
        {
            float top = Math.Max(HillB(x), Dome(x)); if (y > top) return Col.Clear;
            bool dome = Dome(x) > HillB(x);
            float v = .5f + (top - y < .07f ? .35f : 0) - M.Smooth(top, top - 2.2f, y) * .3f + f.Fbm(x, y, .9f, 3, 39) * .14f + (dome ? .12f : 0);
            // Terraces of grass with occasional sunlit patches.
            if (f.N2(x, y, 2.5f, .35f, 41) > .45f) v += .1f;
            return MidHill.At(v, i, j, .4f);
        });
        // Rock face on the front of the dome where the falls spring.
        hillsB.Paint(TX - 2f, TX + 2f, -1.7f, 1.2f, (x, y, i, j) =>
        {
            float dx = x - TX, top = Dome(x) - .32f - MathF.Abs(dx) * .12f + f.N(x, 0, .4f, 43) * .14f;
            float hw = 1.45f - MathF.Max(0, y + 1.2f) * .2f + f.N(0, y, .45f, 44) * .28f;
            if (y > top || MathF.Abs(dx) > hw) return Col.Clear;
            float strata = M.Fract((y + f.N(x, 0, .5f, 43) * .2f) / .3f) < .16f ? -.12f : 0;
            float v = .45f + strata + (top - y < .08f ? .3f : 0) + (hw - MathF.Abs(dx) < .08f ? -.12f : 0) + f.Fbm(x, y, .5f, 3, 45) * .16f - M.Smooth(top, top - 2.4f, y) * .25f;
            return Rock.At(v, i, j, .4f);
        });
        Kit.Fog(hillsB, SkyAt, y => .36f);
        c.Over(hillsB);
        // Flowering groves dotted over the slopes.
        var groves = c.Blank(); var rng = new Rng(47);
        for (int k = 0; k < 44; k++)
        {
            float gx = rng.R(0, 32), size = rng.R(.22f, .5f);
            if (MathF.Abs(f.Dx(gx, TX)) < 2.4f) continue;
            float gy = Math.Max(HillB(gx), Dome(gx)) - .04f;
            var tint = Pastels[rng.I(0, 3)]; float ry = size * .85f;
            groves.Paint(gx - size, gx + size, gy, gy + ry * 2.1f, (x, y, i, j) =>
            {
                float dx = (x - gx) / size, dy = (y - gy - ry * 1.1f) / ry;
                if (MathF.Abs(x - gx) < .6f / 64 && y < gy + ry * 1.1f) return Col.Hex("8a7c68");
                float d2 = dx * dx + dy * dy; if (d2 >= 1) return Col.Clear;
                float lit = .5f - dx * .3f + dy * .35f + f.N(x, y, .15f, 49) * .15f;
                return Kit.Mix(Col.Lerp(tint, Col.Hex("6a8a5a"), .45f), Col.Lerp(tint, Col.White, .15f), lit, i, j, 3);
            });
        }
        Kit.Fog(groves, SkyAt, y => .4f);
        c.Over(groves);
        // Mist settling in the valley between the hills and the meadow.
        c.Paint(0, c.Period, -1.7f, .4f, (x, y, i, j) =>
        {
            float a = M.Smooth(.4f, -1.1f, y) * (.3f + f.Fbm(x, y * 3, 4, 3, 51) * .3f);
            float q = MathF.Floor(a * 5 + Dither.Bayer(i, j)) / 5;
            return q <= 0 ? Col.Clear : Col.Hex("fff6e4").WithA(q);
        });

        // The front meadow with a scatter of tiny flowers and four rivers fanning from the pool.
        var meadow = c.Blank();
        meadow.Paint(0, c.Period, -11, -.6f, (x, y, i, j) =>
        {
            float top = Meadow(x); if (y > top) return Col.Clear;
            float v = .55f + (top - y < .06f ? .3f : 0) - M.Smooth(top, top - 2, y) * .25f + f.N2(x, y, .08f, .5f, 53) * .12f + f.Fbm(x, y, 1.2f, 2, 55) * .1f;
            var col = Grass.At(v, i, j, .4f);
            if (top - y < .7f && Field.Hash01(i >> 1, j >> 1, 57) < .05f) col = Col.Lerp(Pastels[(int)(Field.Hash01(i >> 1, j >> 1, 59) * 5)], col, .25f);
            return col;
        });
        // Falls pour from the rock face into a bright pool.
        foreach (var (fx, top) in new[] { (TX - .95f, Dome(TX - .95f) - .35f), (TX + .85f, Dome(TX + .85f) - .3f) })
        {
            EdenKit.Waterfall(c, fx, top, Meadow(fx) + .05f, .34f, Col.Hex("8ed6d2"), Col.Hex("ffffff"), (int)(fx * 17));
            Kit.Glow(c, fx, Meadow(fx) + .15f, 1.3f, Col.Hex("f0fffa"), .6f, .45f);
        }
        float py = Meadow(TX) - .02f;
        meadow.Paint(TX - 2.2f, TX + 2.2f, py - .5f, py + .05f, (x, y, i, j) =>
        {
            float dx = (x - TX) / 2.1f, dy = (y - py) / .34f;
            if (dx * dx + dy * dy >= 1 || y > py) return Col.Clear;
            float rip = f.Fbm2(x, y, .4f, .05f, 2, 61);
            return rip > .4f ? WaterGlint : Kit.Mix(WaterShallow, WaterDeep, dx * dx + dy * dy * .5f, i, j, 3);
        });
        EdenKit.Stream(meadow, new[] { (TX - 1.9f, py - .08f), (TX - 3.6f, py - .22f), (TX - 6f, py - .42f), (TX - 9f, py - .55f), (TX - 12f, py - .7f) }, .3f, .1f, WaterDeep, WaterShallow, WaterGlint, 63);
        EdenKit.Stream(meadow, new[] { (TX - 1.6f, py - .3f), (TX - 2.8f, py - .7f), (TX - 4.4f, py - 1.1f), (TX - 5.6f, py - 1.7f) }, .34f, .18f, WaterDeep, WaterShallow, WaterGlint, 65);
        EdenKit.Stream(meadow, new[] { (TX + 1.9f, py - .08f), (TX + 3.8f, py - .18f), (TX + 6.4f, py - .4f), (TX + 9.5f, py - .5f), (TX + 12.5f, py - .68f) }, .3f, .1f, WaterDeep, WaterShallow, WaterGlint, 67);
        EdenKit.Stream(meadow, new[] { (TX + 1.6f, py - .3f), (TX + 2.9f, py - .75f), (TX + 4.6f, py - 1.2f), (TX + 5.9f, py - 1.8f) }, .34f, .18f, WaterDeep, WaterShallow, WaterGlint, 69);
        Kit.Fog(meadow, SkyAt, y => .22f + M.Smooth(-1.5f, -4, y) * .2f);
        c.Over(meadow);

        // The Tree of Life: luminous halo, pale trunk and limbs, a wide golden-green crown with fruit glints.
        c.Glow(TX, 6.2f, 8.5f, 5.1f, Col.Hex("fff8e4"), .5f, 5, false);
        var tree = c.Blank();
        float baseY = Dome(TX) - .25f;
        tree.Paint(TX - 2.6f, TX + 2.6f, baseY - .4f, 4.8f, (x, y, i, j) =>
        {
            float t = (y - baseY) / 4.2f; if (t < -.08f || t > 1) return Col.Clear;
            float axis = TX + MathF.Sin(t * 2.4f) * .14f;
            float flare = MathF.Pow(M.Smooth(.24f, -.08f, t), 2.3f) * .9f * (1 + f.N(x, 0, .3f, 71) * .55f);
            float hw = .64f * (1 - t * .42f) + flare + f.N(x, y, .4f, 73) * .04f;
            return MathF.Abs(x - axis) < hw ? Col.White : Col.Clear;
        });
        foreach (var (ex, ey, mx, my) in new[] { (-4.1f, 6.3f, -2f, 5.3f), (3.9f, 6.5f, 1.9f, 5.4f), (-2.4f, 7.6f, -1f, 6.2f), (2.2f, 7.7f, 1.1f, 6.3f), (0f, 8.1f, .3f, 6.6f), (-5f, 5.1f, -2.8f, 4.9f), (4.9f, 5.3f, 2.8f, 5f) })
            EdenKit.Limb(tree, EdenKit.Curve(TX, 4.3f, TX + mx, my, TX + ex, ey), .32f, .07f, (x, y, i, j, t, u) => Col.White);
        Kit.Inflate(tree, 14, (i, j, src, h, n) =>
        {
            float x = tree.X(i), y = tree.Y(j);
            float ridge = f.Ridge(x * 2.5f, y * .5f, .6f, 2, 75);
            float v = .3f + Kit.Lambert(n.X, n.Y, n.Z) * .6f + (ridge - .5f) * .28f;
            var col = PaleBark.At(v, i, j, .4f);
            // Faint golden veins of light run up the living bark.
            if (ridge > .9f && y > baseY + .3f) col = Col.Lerp(col, GoldPale, .55f);
            return col;
        });
        Kit.Fog(tree, SkyAt, y => .18f);
        var crown = c.Blank(); var crng = new Rng(77);
        var clumps = new List<(float x, float y, float rx, float ry)>();
        for (int k = 0; k < 22; k++)
        {
            float ang = crng.R(0, MathF.PI * 2), rad = MathF.Sqrt(crng.F()) * .8f;
            clumps.Add((TX + MathF.Cos(ang) * rad * 6.1f, 6.5f + MathF.Sin(ang) * rad * 2.1f, crng.R(1.3f, 2.1f), crng.R(.85f, 1.3f)));
        }
        clumps.Add((TX, 6.9f, 2.6f, 1.5f));
        foreach (var (lx, ly, rx, ry) in clumps.OrderByDescending(q => q.y))
            Kit.LeafClump(crown, lx, ly, rx, ry, 240, LifeLeaf, 79 + (int)(lx * 11 + ly * 7), .3f, .35f, M.Clamp01((ly - 5) / 4) * .2f);
        Kit.Light(crown, TX, 6.6f, 5.5f, GoldPale, .4f, 3);
        for (int k = 0; k < 90; k++)
        {
            float ang = crng.R(0, MathF.PI * 2), rad = MathF.Sqrt(crng.F());
            float gx = TX + MathF.Cos(ang) * rad * 5.8f, gy = 6.5f + MathF.Sin(ang) * rad * 2.1f;
            int gi = crown.I(gx), gj = crown.J(gy);
            if (!crown.Solid(gi, gj)) continue;
            Kit.Light(crown, gx, gy, .3f, Gold, .8f, 3);
            crown.Set(gi, gj, Gold); crown.Set(gi + 1, gj, Gold); crown.Set(gi, gj + 1, Col.Hex("fff8d8")); crown.Set(gi + 1, gj + 1, Col.Lerp(Gold, Col.Hex("c89a30"), .5f));
        }
        Kit.Fog(crown, SkyAt, y => .14f);
        c.Over(tree);
        c.Over(crown);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Mid()
    {
        var s = new LayerSpec("mid", .65f, 96, 36, -9.5f, 22, -95);
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(201);
        const float PX0 = 8.3f, PX1 = 14.2f, PY = -1.25f;
        float Meadow(float x) => -1.3f + f.Fbm(x, 0, 2.6f, 3, 203) * .3f;
        float PoolTop(float x) { float e = MathF.Max(0, MathF.Abs(x - (PX0 + PX1) * .5f) - (PX1 - PX0) * .5f + .6f) / .6f; return PY - e * e * .5f; }
        float PoolBottom(float x) { float e = MathF.Max(0, MathF.Abs(x - (PX0 + PX1) * .5f) - (PX1 - PX0) * .5f + 1.2f) / 1.2f; return PY - 2.1f + e * e * 1.2f + f.N(x, 0, .8f, 204) * .1f; }
        bool InPool(float x, float y) => x > PX0 && x < PX1 && y < PoolTop(x) && y > PoolBottom(x);

        // Meadow bank with grass streaks, tufted rim and a carpet of tiny flowers; a raised bank behind the pool.
        c.Paint(0, c.Period, -9.5f, -.4f, (x, y, i, j) =>
        {
            float top = Meadow(x);
            if (x > PX0 - .3f && x < PX1 + .3f) top = Math.Max(top, -.98f + f.N(x, 0, .6f, 205) * .08f);
            float tuft = f.N2(x, 0, .05f, 1, 207) * .06f;
            if (y > top + tuft) return Col.Clear;
            float v = .5f + (top + tuft - y < .05f ? .3f : 0) - M.Smooth(top, top - 2.2f, y) * .3f + f.N2(x, y, .06f, .6f, 209) * .14f + f.Fbm(x, y, 1.4f, 2, 211) * .1f;
            var col = Grass.At(v, i, j, .4f);
            if (top - y < .9f && top - y > .04f && Field.Hash01(i >> 1, j >> 1, 213) < .035f) col = Col.Lerp(Pastels[(int)(Field.Hash01(i >> 1, j >> 1, 215) * 5)], col, .2f);
            return col;
        });
        c.Paint(PX0, PX1, PY - 2.3f, PY, (x, y, i, j) =>
        {
            if (!InPool(x, y)) return Col.Clear;
            bool bank = y < PoolBottom(x) + .1f;
            return bank ? Kit.Mix(Col.Hex("c8c09a"), Col.Hex("8a9a6a"), M.InvLerp(PoolBottom(x) + .1f, PoolBottom(x), y), i, j) : Kit.Mix(Col.Hex("a8e6e0"), Col.Hex("48b0b4"), M.InvLerp(PY, PY - 2.2f, y), i, j);
        });

        // Flowering fruit trees; the Tree of Knowledge carries red fruit and a serpent on an outstretched limb.
        var trees = c.Blank();
        var redFruit = new Ramp("5a1418", "8c1c20", "c0282c", "e04040", "f06a58");
        var goldFruit = new Ramp("8a5a14", "c08a20", "e8b43a", "f8d464", "fff0a0");
        var noFruit = new Ramp("000000");
        EdenKit.FloweringTree(trees, 12.4f, -1f, 4, 1.7f, 1.15f, PaleBark, Leaf, Col.Hex("fff0f4"), Col.Hex("f0c8d8"), Col.Hex("ffe070"), 40, goldFruit, Col.Hex("fffbe0"), 0, 221, .1f, .05f, 5, 180);
        EdenKit.FloweringTree(trees, 5f, -1.45f, 6.2f, 2.8f, 1.85f, PaleBark, Leaf, Col.Hex("fff6f0"), Col.Hex("f4d4d8"), Col.Hex("ffe070"), 12, redFruit, Col.Hex("ffd8d0"), 16, 223, -.05f, 0, 8, 240);
        EdenKit.FloweringTree(trees, 17.3f, -1.4f, 5.4f, 2.4f, 1.55f, PaleBark, Leaf, Col.Hex("f8b8cc"), Col.Hex("e08ca8"), Col.Hex("fff0a0"), 80, noFruit, Col.White, 0, 225, .08f, .05f, 7, 220);
        EdenKit.FloweringTree(trees, 24.7f, -1.35f, 4.6f, 2f, 1.35f, PaleBark, Leaf, Col.Hex("fff8f2"), Col.Hex("e8d8c8"), Col.Hex("ffd860"), 60, goldFruit, Col.Hex("fffbe0"), 7, 227, -.1f, .05f, 6, 200);
        EdenKit.FloweringTree(trees, 31.7f, -1.4f, 5.8f, 2.5f, 1.65f, PaleBark, Leaf, Col.Hex("d8c0f4"), Col.Hex("b090d8"), Col.Hex("fff0a0"), 70, noFruit, Col.White, 0, 229, .06f, .03f, 7, 230);
        // The serpent's limb reaches right out of the crown.
        var limb = EdenKit.Curve(5.6f, 1.9f, 7.2f, 2.3f, 8.6f, 2.2f);
        EdenKit.Limb(trees, limb, .11f, .035f, (x, y, i, j, t, u) => PaleBark.At(Kit.Cylinder(u, .22f) * .7f - t * .1f, i, j, .4f));
        EdenKit.Fruit(trees, 8.1f, 1.75f, .085f, redFruit, Col.Hex("ffd8d0"), .12f);
        var serpent = new Ramp("1a4a2a", "22663a", "2e8448", "3ea45a", "6cc46a", "a8e08c");
        var coil = new List<(float x, float y)>();
        for (int k = 0; k <= 22; k++)
        {
            float t = k / 22f, u = 1 - t;
            float bx = u * u * 6.4f + 2 * u * t * 7.4f + t * t * 8.5f, by = u * u * 2.1f + 2 * u * t * 2.36f + t * t * 2.22f;
            coil.Add((bx, by + MathF.Sin(t * MathF.PI * 2.5f) * .12f + .03f));
        }
        coil.Add((8.68f, 2.05f)); coil.Add((8.7f, 1.85f)); coil.Add((8.58f, 1.68f));
        EdenKit.Serpent(trees, coil.ToArray(), .055f, serpent, Col.Hex("ffe040"));
        Kit.Fog(trees, SkyAt, y => .2f + M.Smooth(-1, -4, y) * .2f);
        c.Over(trees);

        // Ferns, grasses and tall pastel flowers along the bank.
        var flowers = c.Blank();
        foreach (var (fx, r) in new[] { (2.1f, .9f), (7.2f, .75f), (15.4f, 1f), (20.6f, .8f), (27.6f, .95f), (34.3f, .8f), (22.9f, .6f) })
            Kit.LeafClump(flowers, fx, Meadow(fx) - .15f, r, r * .5f, 120, Leaf, (int)(fx * 31), .46f, -.6f, .08f);
        var stem = new Ramp("2e6a30", "3c8040", "52a052", "70b864");
        for (int k = 0; k < 34; k++)
        {
            float fx = rng.R(0, 36);
            if (fx > PX0 - .2f && fx < PX1 + .2f) { fx = fx < 11.2f ? PX0 - .3f - rng.R(0, 1.6f) : PX1 + .3f + rng.R(0, 1.6f); }
            int kind = rng.I(0, 3); var tint = Pastels[rng.I(0, 5)];
            EdenKit.TallFlower(flowers, fx, Meadow(fx) - .12f, rng.R(.7f, 2.1f), kind, tint, Col.Lerp(tint, Col.Hex("806080"), .35f), Col.Hex("ffd84a"), stem, 300 + k);
        }
        Kit.Fog(flowers, SkyAt, y => .1f);
        c.Over(flowers);

        // Reflections in the pool: mirror the composed scene, keep only the water's area, then lilies and ripples.
        var refl = c.Blank(); refl.Over(c);
        Kit.Reflect(refl, PY, 1.7f, Col.Hex("8cd8d4"), .85f, 231);
        c.Paint(PX0, PX1, PY - 1.8f, PY, (x, y, i, j) => InPool(x, y) ? refl.Get(i, j) : Col.Clear);
        c.Paint(PX0, PX1, PY - 1.8f, PY, (x, y, i, j) =>
        {
            if (!InPool(x, y)) return Col.Clear;
            float d = PY - y, n = f.Fbm2(x, y, .8f, .05f + d * .03f, 2, 233);
            float a = M.Smooth(.36f, .6f, n) * .8f * (1 - M.Clamp01(d / 1.6f));
            if (d < 2 / c.Ppu) a = .6f;
            return a > .1f ? WaterGlint.WithA(a) : Col.Clear;
        });
        var pad = new Ramp("2a6a38", "3a8a48", "4ea65a", "6cbe6c", "90d484");
        foreach (var (lx, ly, rx, lily, k) in new[] { (9.2f, PY - .08f, .3f, true, 0), (10.4f, PY - .22f, .26f, false, 1), (11.6f, PY - .06f, .34f, true, 2), (12.9f, PY - .18f, .28f, false, 3), (13.6f, PY - .07f, .24f, true, 4), (10.1f, PY - .04f, .2f, false, 5) })
            EdenKit.LilyPad(c, lx, ly, rx, rx * .3f, pad, 1.1f + k * .5f, lily ? (k == 2 ? Col.Hex("fff8f4") : Col.Hex("f8b8d0")) : null, Col.Hex("e090b0"), Col.Hex("ffe060"));

        // Light: shafts through the air, drifting gold motes and butterflies.
        EdenKit.Rays(c, 22, 3.2f, .26f, Col.Hex("fff4d0"), .2f, -4, 12.5f, 235, (x, y) => .4f + .6f * M.Smooth(-2, 6, y));
        Kit.Stars(c, -2, 9, .0004f, Col.Hex("fff0b0"), 237, (x, y) => M.Smooth(.15f, .7f, f.N(x, y, 3, 239) * .5f + .5f) * .9f);
        var mrng = new Rng(243);
        for (int k = 0; k < 26; k++)
        {
            float mx = mrng.R(0, 36), my = mrng.R(-1.5f, 8);
            float mr = mrng.R(.12f, .24f);
            c.Glow(mx, my, mr, mr, GoldPale, .8f, 3, false);
            c.Set(c.I(mx), c.J(my), Col.Hex("fffbe0"));
        }
        var brng = new Rng(241);
        for (int k = 0; k < 16; k++)
        {
            float bx = brng.R(0, 36), by = brng.R(-.6f, 6.4f);
            var tint = Pastels[brng.I(0, 5)];
            EdenKit.Butterfly(c, bx, by, brng.R(.07f, .1f), tint, Col.Lerp(tint, Col.Hex("4a3a50"), .55f), brng.R(.45f, 1));
        }
        return new PaintedLayer(s, c);
    }

    PaintedLayer Near()
    {
        var s = new LayerSpec("near", .40f, 96, 40, 2, 12.5f, -94) { Over = NearLeaf[0] };
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(401);
        // Overhead canopy: backlit leaf clumps, some hanging low, sealed solid above.
        var leaves = c.Blank();
        var clumps = new List<(float x, float y, float rx, float ry)>();
        for (int k = 0; k < 32; k++)
        {
            float x = k * 40 / 32f + rng.R(-.6f, .6f);
            float hang = Field.Hash01(k, 1, 403) > .72f ? rng.R(1.4f, 2.8f) : 0;
            clumps.Add((x, rng.R(11.2f, 12.3f) - hang, rng.R(1.5f, 2.5f), rng.R(1f, 1.5f) + hang * .3f));
        }
        foreach (var (x, y, rx, ry) in clumps.OrderByDescending(q => q.y))
            Kit.LeafClump(leaves, x, y, rx, ry, 360, NearLeaf, 600 + (int)(x * 7), .5f, .6f);
        leaves.Paint(0, c.Period, 12.4f, c.Top, (x, y, i, j) => NearLeaf[0]);
        // Vines with fruit hanging from the canopy.
        var goldFruit = new Ramp("6a4410", "a87818", "d8a830", "f4cc58", "fff0a8");
        var redFruit = new Ramp("4a1014", "801a20", "b8262c", "d84040", "f07060");
        foreach (var (vx, len, fruit) in new[] { (3.2f, 6.2f, 1), (4.4f, 3.8f, 0), (11.6f, 5.2f, 2), (17.8f, 7.4f, 1), (19.2f, 4.2f, 0), (24.6f, 3.4f, 2), (30.5f, 6.6f, 1), (31.8f, 4.8f, 0), (36.6f, 5.6f, 2) })
        {
            float top = 11.9f;
            Kit.Vine(leaves, vx, top, len, Col.Hex("2a5230"), NearLeaf, (int)(vx * 17), .28f, 2.6f);
            if (fruit > 0)
            {
                float ex = vx + MathF.Sin(1 * 5.2f + (int)(vx * 17)) * .28f + f.N(0, top - len, 1.2f, (int)(vx * 17)) * .05f;
                EdenKit.Fruit(leaves, ex, top - len - .1f, fruit == 1 ? .13f : .11f, fruit == 1 ? goldFruit : redFruit, Col.Hex("fff8e0"), .14f);
            }
        }
        // Blossoms and fruit through the crown; sunlit rims on every underside.
        var brng = new Rng(405);
        foreach (var (x, y, rx, ry) in clumps)
        {
            int n = brng.I(7, 16); var tint = Pastels[brng.I(0, 3)];
            for (int k = 0; k < n; k++)
            {
                float ang = brng.R(0, MathF.PI * 2), rad = MathF.Sqrt(brng.F());
                float bx = x + MathF.Cos(ang) * rad * rx, by = y + MathF.Sin(ang) * rad * ry * 1.05f;
                if (!leaves.Solid(leaves.I(bx), leaves.J(by)) || by > 12.3f) continue;
                EdenKit.Blossom(leaves, bx, by, brng.R(.08f, .15f), tint, Col.Lerp(tint, Col.Hex("5a3a60"), .4f), Col.Hex("ffd860"), 5, brng.R(0, 3));
            }
            if (brng.Chance(.45f))
            {
                float fx = x + brng.R(-rx * .5f, rx * .5f), fy = y - ry * brng.R(.5f, .85f);
                if (leaves.Solid(leaves.I(fx), leaves.J(fy + .2f))) EdenKit.Fruit(leaves, fx, fy, brng.R(.1f, .14f), brng.Chance(.6f) ? goldFruit : redFruit, Col.Hex("fff8e0"), .1f);
            }
        }
        Kit.Edge(leaves, 0, -1, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("c4e49a"), d == 0 ? .4f : .16f));
        Kit.Edge(leaves, -1, 0, 1, (col, d, i, j) => Col.Lerp(col, Col.Hex("a8d088"), .18f));
        c.Over(leaves);
        // Light breaking through the canopy.
        EdenKit.Rays(c, 22, 2.6f, .3f, Col.Hex("fff6d8"), .5f, 5, 14.5f, 407, (x, y) => .5f + .5f * f.N(x, 0, 5, 409));
        return new PaintedLayer(s, c);
    }
}
