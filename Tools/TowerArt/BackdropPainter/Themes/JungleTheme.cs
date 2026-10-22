namespace BackdropPainter;

/// <summary>Ancient jungle temple: twisted giant trunks, a mossy stepped shrine with a carved door,
/// hanging lianas, layered canopies and soft shafts of light through teal haze.</summary>
public sealed class JungleTheme : ThemePainter
{
    public override string Id => "jungle";

    static readonly Ramp Leaf = new("0c2224", "123029", "1a3f30", "245236", "33663d", "4a7c44", "68954e", "92b462", "c2d47e");
    static readonly Ramp Bark = new("141a19", "1d2622", "28332c", "354237", "465445", "5c6b55", "778466");
    static readonly Ramp Stone = new("17221f", "203029", "2c3e35", "3b4f43", "4f6454", "687c69", "889a82");
    static readonly Col Moss = Col.Hex("5e8a46"), MossLight = Col.Hex("8eb05a"), Gold = Col.Hex("e8c66a"), Ray = Col.Hex("d6e8b0");
    static readonly (float y, Col c)[] SkyStops = Kit.Stops((-11, "143a39"), (-6, "1d4a46"), (-2, "2a5b51"), (2, "386858"), (5.5f, "29534e"), (10, "0f272b"));

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
        // Faint distant trunks and a stepped shrine lost in the haze.
        c.Paint(0, c.Period, -11, 10, (x, y, i, j) =>
        {
            float sp = f.Snap(3.75f); int k = (int)MathF.Floor(x / sp);
            float u = M.Mod(x, sp) - sp * .5f - (Field.Hash01(M.Mod(k, (int)MathF.Round(c.Period / sp)), 1, 9) - .5f) * 1.6f;
            float w = .22f + Field.Hash01(M.Mod(k, (int)MathF.Round(c.Period / sp)), 2, 9) * .4f + M.Smooth(-3, -9, y) * .5f;
            float tier = MathF.Floor((y + 7) / .9f), half = 6.5f - tier * .95f;
            bool shrine = y < -1 && MathF.Abs(f.Dx(x, 16)) < half;
            if (!shrine && MathF.Abs(u) > w) return Col.Clear;
            return Col.Lerp(Kit.Gradient(SkyStops, y, 4, 1, i, j), Col.Hex("173a38"), .28f);
        });
        Kit.Shafts(c, 22, 2.6f, .35f, Ray, .2f, -6, 10, 13);
        // Canopy fringe silhouette at the top.
        c.Paint(0, c.Period, 4, 10, (x, y, i, j) =>
        {
            float edge = 7.2f + f.Fbm(x, 0, 2.2f, 4, 17) * 1.4f;
            return y > edge ? Col.Lerp(Kit.Gradient(SkyStops, y, 4, 1, i, j), Col.Hex("0e2426"), .45f) : Col.Clear;
        });
        return new PaintedLayer(s, c);
    }

    /// <summary>Stone with block courses, moss on every upward face and fine weathering.</summary>
    static Col MossyStone(Canvas plane, Field f, int i, int j, V3 n, float bias, int seed, float bw = .62f, float bh = .31f)
    {
        float x = plane.X(i), y = plane.Y(j);
        bool mortar = Kit.Masonry(f, x, y, bw, bh, 1.3f / plane.Ppu, seed, out float sh, out float lx, out float ly, out _);
        float v = bias + Kit.Lambert(n.X, n.Y, n.Z) * .45f + (sh - .5f) * .14f + f.Fbm(x, y, .6f, 3, seed + 1) * .08f;
        if (mortar) v -= .18f; else if (ly > bh - 1.6f / plane.Ppu) v += .1f;
        var col = Stone.At(v, i, j, .4f);
        // Moss spills over tops and creeps down the joints.
        int up = plane.Depth(i, j, 0, 1, 7);
        float moss = (up < 7 ? 1 - up / 7f : 0) + (mortar ? .35f : 0) + f.Fbm(x, y, .5f, 3, seed + 3) * .6f - .25f;
        if (moss > .35f) col = Kit.Mix(Col.Lerp(Moss, Col.Black, .2f), MossLight, up < 2 ? .8f : moss - .35f, i, j, 3);
        return col;
    }

    PaintedLayer Far()
    {
        var s = new LayerSpec("far", .85f, 64, 32, -11, 21, -96) { Over = Col.Hex("0e2426") };
        var c = s.NewCanvas(); var f = c.F;
        var temple = c.Blank();
        float Base = -6.6f, tx = 13.5f;
        temple.Paint(0, c.Period, -11, c.Top, (x, y, i, j) =>
        {
            float dx = f.Dx(x, tx);
            // Stepped platform tiers.
            float tier = MathF.Floor((y - Base) / 1.05f), half = 7.2f - tier * .95f;
            if (y < Base + 5 * 1.05f && MathF.Abs(dx) < half) return Col.White;
            // Shrine block with a stepped crown.
            float crown = Base + 5 * 1.05f + 4.6f + (MathF.Abs(dx) < 1.4f ? .9f : MathF.Abs(dx) < 2.2f ? .45f : 0);
            if (MathF.Abs(dx) < 3 && y < crown) return Col.White;
            // A fractured guardian statue on a plinth.
            float sx = f.Dx(x, 25.5f);
            if (y < Base + 1.4f && MathF.Abs(sx) < 1.3f) return Col.White;
            float body = Base + 1.4f;
            bool robe = y < body + 3.4f && MathF.Abs(sx) < .55f + (body + 3.4f - y) * .12f;
            bool head = (sx * sx) / .16f + (y - body - 3.9f) * (y - body - 3.9f) / .3f < 1;
            bool crest = y > body + 4.1f && y < body + 4.9f && MathF.Abs(sx) < .45f - (y - body - 4.1f) * .3f;
            bool brokenOff = y > body + 2.3f - sx * .9f + f.N(x, y, .3f, 21) * .15f && sx > -.1f;
            if ((robe || head || crest) && !brokenOff) return Col.White;
            if (y < body + 2.6f && MathF.Abs(sx + .85f) < .28f && y > body + .8f) return Col.White;
            // A cliff with a waterfall ledge.
            float cliff = 21.6f + f.Fbm(0, y, 1.4f, 3, 23) * .5f;
            if (x > cliff && x < 23.2f && y > 3.6f) return Col.White;
            return Col.Clear;
        });
        Kit.Inflate(temple, 16, (i, j, src, h, n) =>
        {
            float x = temple.X(i), y = temple.Y(j), dx = MathF.Abs(f.Dx(x, tx));
            var col = MossyStone(temple, f, i, j, n, .2f, 31, .42f, .21f);
            float top = Base + 5 * 1.05f;
            // Pilasters at the shrine corners and a carved frieze over the door.
            if (y > top && dx > 2.45f && dx < 2.8f) col = Col.Lerp(col, Col.White, .1f);
            if (y > top + 3.25f && y < top + 4.05f && dx < 2.6f)
            {
                float gx = M.Mod(x - tx + .2f, .4f), gy = y - top - 3.25f;
                bool glyph = gx > .07f && gx < .33f && gy > .12f && gy < .68f && !(gx > .15f && gx < .25f && gy > .3f && gy < .5f);
                col = glyph ? Col.Lerp(col, Col.White, .12f) : Col.Lerp(col, Col.Black, .25f);
            }
            if (f.Ridge(x, y, 1.3f, 3, 33) > .93f) col = Col.Lerp(col, Col.Black, .35f);
            return col;
        });
        // Carved door: dark portal with a glinting lintel band.
        float doorY = Base + 5 * 1.05f;
        temple.Paint(tx - 1.6f, tx + 1.6f, doorY, doorY + 3.8f, (x, y, i, j) =>
        {
            // A stepped corbel arch, Maya style.
            float dx = MathF.Abs(x - tx), top = doorY + 1.9f + MathF.Floor((1.05f - dx) / .18f) * .2f;
            if (dx < 1.05f && y < Math.Min(top, doorY + 3)) return Kit.Mix(Col.Hex("0d1716"), Col.Hex("3a3420"), M.InvLerp(doorY + 2.5f, doorY, y) * .6f, i, j);
            return Col.Clear;
        });
        Kit.Fog(temple, y => Kit.Smooth(SkyStops, y), y => .42f + M.Smooth(-1, -6.5f, y) * .2f);

        // Giant trunks framing the temple.
        var trees = c.Blank();
        foreach (var (bx, w) in new[] { (3f, 1.25f), (29f, 1.5f), (19f, .7f) })
            trees.Paint(bx - 4, bx + 4, -11, c.Top, (x, y, i, j) =>
            {
                float cx = bx + MathF.Sin(y * .35f + bx) * .35f, root = MathF.Pow(M.Smooth(-3, -6.8f, y), 2) * w * 2.2f;
                float hw = w * .5f + root + f.N(x, y, .6f, 25) * .08f;
                return MathF.Abs(x - cx) < hw ? Col.White : Col.Clear;
            });
        Kit.Inflate(trees, 24, (i, j, src, h, n) =>
        {
            float x = trees.X(i), y = trees.Y(j);
            float v = .14f + Kit.Lambert(n.X, n.Y, n.Z) * .5f + (f.Ridge(x * 3, y * .5f, .5f, 2, 27) - .5f) * .2f;
            return Bark.At(v, i, j, .4f);
        });
        Kit.Fog(trees, y => Kit.Smooth(SkyStops, y), y => .45f);

        // Canopy along the top.
        var canopy = c.Blank(); var rng = new Rng(29);
        for (int k = 0; k < 26; k++) Kit.LeafClump(canopy, k * 32 / 26f + rng.R(-.5f, .5f), rng.R(7.4f, 9.4f), rng.R(1.4f, 2.4f), rng.R(.9f, 1.4f), 260, Leaf, 300 + k, .3f, .3f);
        canopy.Paint(0, c.Period, 8.6f, c.Top, (x, y, i, j) => Leaf[1]);
        for (int k = 0; k < 14; k++) Kit.Vine(canopy, rng.R(0, 32), 7.6f, rng.R(1.5f, 6), Leaf[2], Leaf, 400 + k, .15f, 1);
        Kit.Fog(canopy, y => Kit.Smooth(SkyStops, y), y => .38f);

        c.Over(temple);
        c.Over(trees);
        // Waterfall from the cliff ledge.
        c.Paint(22.2f, 23.6f, Base, 3.8f, (x, y, i, j) =>
        {
            float cx = 22.75f + f.N(0, y, 2, 33) * .06f, half = .32f + (3.8f - y) * .015f, d = MathF.Abs(x - cx) / half;
            if (d > 1) return Col.Clear;
            float n = f.Fbm2(x, y, .12f, .9f, 2, 35) * .5f + .5f;
            return Kit.Mix(Col.Hex("5a8e88"), Col.Hex("c4e4d6"), n * (1 - d * .6f), i, j).WithA(.9f);
        });
        Kit.Glow(c, 22.75f, Base + .3f, 2.2f, Col.Hex("b8dcd0"), .45f, .5f);
        c.Over(canopy);
        Kit.Glow(c, tx, doorY + 1.2f, 1.6f, Gold, .2f);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Mid()
    {
        var s = new LayerSpec("mid", .65f, 96, 36, -9.5f, 22, -95);
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(201);
        // Enormous twisted trunks with flaring buttress roots.
        var trees = c.Blank();
        foreach (var (bx, w, twist) in new[] { (4.2f, 1.35f, .5f), (25f, 1.1f, -.4f) })
            trees.Paint(bx - 5, bx + 5, -9.6f, c.Top, (x, y, i, j) =>
            {
                float cx = bx + MathF.Sin(y * .42f + bx) * twist, root = MathF.Pow(M.Smooth(-3.5f, -9.4f, y), 1.6f);
                float hw = w * .5f * (1 + root * 2.6f) + f.N(x, y, .5f, 203) * .07f;
                // Buttress fins spread at the base.
                float fin = root * (MathF.Abs(MathF.Sin((x - cx) * 2.6f)) * .5f + .5f);
                if (MathF.Abs(x - cx) < hw) return Col.White;
                if (MathF.Abs(x - cx) < hw + fin * 1.4f && y < -7.5f + MathF.Cos((x - cx) * 2.6f) * .5f) return Col.White;
                return Col.Clear;
            });
        // A diagonal limb reaching across from the right trunk.
        trees.Paint(25, 34, 5, 12, (x, y, i, j) =>
        {
            float t = (x - 25) / 9, cy = 6.2f + t * 3.6f + MathF.Sin(t * 5) * .2f, hw = .38f * (1 - t * .6f);
            return MathF.Abs(y - cy) < hw ? Col.White : Col.Clear;
        });
        Kit.Inflate(trees, 60, (i, j, src, h, n) =>
        {
            float x = trees.X(i), y = trees.Y(j);
            // Bark ridges spiral with the twist.
            float ridge = f.Ridge(x * 2.4f + y * .35f, y * .45f, .55f, 3, 205);
            float v = .08f + Kit.Lambert(n.X, n.Y, n.Z) * .62f + (ridge - .55f) * .35f;
            var col = Bark.At(v, i, j, .35f);
            float moss = n.Y * .6f - n.X * .4f + f.Fbm(x, y, .7f, 3, 207) * .8f;
            if (moss > .45f) col = Kit.Mix(Col.Lerp(Moss, Col.Black, .35f), Moss, moss - .45f, i, j, 3);
            return Kit.Grain(col, i, j, 209, .05f, .18f);
        }, (i, j) => f.Fbm(trees.X(i), trees.Y(j), 1.2f, 3, 210) * 18);
        Kit.Edge(trees, -1, 0, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("9ab874"), d == 0 ? .35f : .15f));

        // Moss-covered carved doorway half buried among roots.
        var ruin = c.Blank();
        float dx0 = 14.6f;
        ruin.Paint(dx0 - 3.2f, dx0 + 3.2f, -9.6f, 1.2f, (x, y, i, j) =>
        {
            float dx = MathF.Abs(x - dx0);
            float top = .1f - (dx > 1.9f ? (dx - 1.9f) * 1.1f : 0) + f.N(x, 0, .5f, 211) * .12f;
            if (y > top || dx > 3) return Col.Clear;
            if (dx < 1.05f && y < -1.7f) return Col.Clear;
            if (dx < 1.05f && y >= -1.7f && (dx * dx) / 1.1f + (y + 1.7f) * (y + 1.7f) / .55f < 1) return Col.Clear;
            if (x > dx0 + 1.6f && y > -1.2f - (x - dx0 - 1.6f) * .9f) return Col.Clear;
            return Col.White;
        });
        Kit.Inflate(ruin, 20, (i, j, src, h, n) =>
        {
            float x = ruin.X(i), y = ruin.Y(j);
            var col = MossyStone(ruin, f, i, j, n, .16f, 213, .5f, .26f);
            // Carved glyph band across the jambs.
            if (y > -1.4f && y < -.9f && MathF.Abs(x - dx0) > 1.2f && Field.Hash01((int)MathF.Floor(x * 7), (int)MathF.Floor(y * 9), 5) > .55f) col = Col.Lerp(col, Col.Black, .3f);
            return col;
        });
        ruin.Paint(dx0 - 1.1f, dx0 + 1.1f, -9.6f, -.9f, (x, y, i, j) =>
        {
            float dx = MathF.Abs(x - dx0);
            bool inside = (dx < 1.05f && y < -1.7f) || (dx * dx) / 1.1f + (y + 1.7f) * (y + 1.7f) / .55f < 1;
            return inside ? Kit.Mix(Col.Hex("0a1312"), Col.Hex("2e3a2a"), M.InvLerp(-1, -6, y) * .4f, i, j) : Col.Clear;
        });

        // Canopy, lianas and ferns.
        var canopy = c.Blank();
        for (int k = 0; k < 22; k++) Kit.LeafClump(canopy, k * 36 / 22f + rng.R(-.6f, .6f), rng.R(9.6f, 11.4f), rng.R(1.6f, 2.6f), rng.R(1, 1.6f), 420, Leaf, 500 + k, .36f, .35f);
        canopy.Paint(0, c.Period, 11.2f, c.Top, (x, y, i, j) => Leaf[1]);
        foreach (var (lx, ly, len) in new[] { (8.5f, 10f, 9f), (10.2f, 10f, 5f), (17f, 10.4f, 7.5f), (19.4f, 10f, 3.6f), (21.5f, 10.2f, 11f), (29.5f, 9.6f, 6.4f), (32.6f, 10.2f, 8f), (1.2f, 10f, 4.5f) })
            Kit.Vine(canopy, lx, ly, len, Col.Hex("2f4a2c"), Leaf, (int)(lx * 13), .22f, 2.2f);
        var ferns = c.Blank();
        foreach (var (fx, fy, r) in new[] { (12f, -2.6f, 1.2f), (17.4f, -2.4f, 1f), (7f, -3f, 1.3f), (21.5f, -2.9f, 1.1f), (30.5f, -2.8f, 1.4f), (34.4f, -3.2f, 1f) })
            Kit.LeafClump(ferns, fx, fy, r, r * .55f, 130, Leaf, (int)(fx * 31), .5f, -.6f, .05f);
        ferns.Paint(0, c.Period, -9.6f, -3, (x, y, i, j) => y < -3.2f + f.Fbm(x, 0, 1, 3, 215) * .5f ? Leaf[1] : Col.Clear);
        Kit.Fog(ferns, y => Col.Hex("1e4a44"), y => .22f);

        Kit.Fog(trees, y => Col.Hex("1e4a44"), y => .08f + M.Smooth(-3, -9, y) * .4f);
        Kit.Fog(ruin, y => Col.Hex("1e4a44"), y => .12f + M.Smooth(-3, -9, y) * .4f);
        c.Over(ruin);
        c.Over(trees);
        c.Over(ferns);
        c.Over(canopy);
        Kit.Shafts(c, 22, 3.4f, .28f, Ray, .14f, -9, 10, 217, (x, y) => M.Smooth(10.5f, 7, y) * .8f + .2f);
        // Fireflies and warm glints.
        var rng2 = new Rng(219);
        for (int k = 0; k < 22; k++)
        {
            float gx = rng2.R(0, 36), gy = rng2.R(-6, 6);
            Kit.Glow(c, gx, gy, .32f, Gold, .9f, 1, 3);
            c.Set(c.I(gx), c.J(gy), Col.Hex("fff2b0"));
        }
        return new PaintedLayer(s, c);
    }

    PaintedLayer Near()
    {
        var s = new LayerSpec("near", .40f, 96, 40, 2, 12.5f, -94) { Over = Col.Hex("08171a") };
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(401);
        var dark = new Ramp("06131a", "0a1c1e", "102622", "163226", "1f402c", "2d5234", "41683e");
        var leaves = c.Blank();
        for (int k = 0; k < 30; k++)
        {
            float x = k * 40 / 30f + rng.R(-.6f, .6f);
            float hang = Field.Hash01(k, 1, 3) > .7f ? rng.R(2, 3.6f) : 0;
            Kit.LeafClump(leaves, x, rng.R(12, 13) - hang, rng.R(1.6f, 2.6f), rng.R(1.1f, 1.6f) + hang * .4f, 380, dark, 600 + k, .55f, .6f);
        }
        leaves.Paint(0, c.Period, 12.6f, c.Top, (x, y, i, j) => dark[0]);
        foreach (var (lx, len) in new[] { (5.5f, 7.6f), (6.6f, 5f), (17.5f, 4f), (26.4f, 8.4f), (27.6f, 6.2f), (36f, 5.4f) })
            Kit.Vine(leaves, lx, 12.2f, len, Col.Hex("142a22"), dark, (int)(lx * 17), .3f, 3);
        Kit.Edge(leaves, 0, 1, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("6a8c52"), d == 0 ? .35f : .15f));
        c.Over(leaves);
        return new PaintedLayer(s, c);
    }
}
