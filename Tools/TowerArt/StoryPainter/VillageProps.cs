namespace StoryPainter;

/// <summary>
/// World props of the lantern village at 96 texels per unit, each pivoted at the bottom centre: tutorial
/// signposts, the story stele, the shrine of myths (with nine cups for the restored-myth flames the runtime
/// lights) and the straw training dummy.
/// </summary>
public static class VillageProps
{
    const float Ppu = 96;
    static readonly Col Outline = Col.Hex("140c0a");
    static readonly Ramp Wood = new("2a1a10", "40281a", "583824", "70482e", "8a5c3a", "a6744a", "c08e5e");
    static readonly Ramp Stone = new("23262e", "343842", "474c57", "5c616c", "737883", "8d929b", "a9adb4", "c8cbd0");
    static readonly Ramp Moss = new("1c2a1a", "2a3e24", "3c5430", "526c3c", "6c864c");
    static readonly Ramp Straw = new("4a3414", "6c4e1e", "8e6a2a", "b08a3a", "ccaa52", "e4c870", "f4e09a");
    static readonly Ramp Burlap = new("3a2e22", "54442f", "6e5b40", "897454", "a38d6a", "bba684");
    static readonly Ramp Rope = new("3e2c18", "5e4426", "7e5e36", "9e7c4a");
    static readonly Ramp Glow = new("1a5a5a", "2a8a86", "46b8ac", "78dccc", "b4f2e6", "eefffa");
    static readonly Ramp Brass = new("4a3010", "6e4a18", "946824", "b88a34", "d8ac4c", "f0cc70");

    public static IEnumerable<(string name, Img img)> Paint()
    {
        yield return ("signpost", Signpost());
        yield return ("stele", Stele());
        yield return ("runestone", Runestone());
        yield return ("shrine", Shrine());
        yield return ("dummy", Dummy());
    }

    static (int, int, int, int) Box(float x0, float y0, float x1, float y1) => ((int)x0, (int)y0, (int)MathF.Ceiling(x1), (int)MathF.Ceiling(y1));
    static Inside Rect(float x0, float y0, float x1, float y1, float bevel = 0) => (float x, float y, out float t, out float s) =>
    {
        t = (x - x0) / (x1 - x0); s = (y - y0) / (y1 - y0);
        if (x < x0 || x > x1 || y < y0 || y > y1) return false;
        float cx = Math.Min(x - x0, x1 - x), cy = Math.Min(y - y0, y1 - y);
        return cx + cy >= bevel;
    };
    static Col Grain(Ramp ramp, Texel tx, float bias, int seed, float along = 1)
    {
        // Wood grain: long stretched noise bands with a few dark knots.
        float g = Noise.Fbm(tx.X * along, tx.Y / along, 22, 2, seed);
        float v = bias + tx.Key * .55f + tx.Rim * .08f + g * .14f;
        if (Noise.Ridge(tx.X * along * 1.6f, tx.Y / along * .3f, 30, seed + 1) > .82f) v -= .16f;
        return ramp.At(v, tx.X, tx.Y, .35f);
    }

    static Img Signpost()
    {
        var img = new Img(88, 132);
        img.Part(Rect(39, 0, 49, 112), 4, tx => Grain(Wood, tx, .15f, 3, .25f), Outline, Box(36, 0, 52, 114));
        img.Part(Rect(6, 74, 82, 116, 3), 6, tx =>
        {
            // Two planks with a dark seam, nailed to the post.
            if (MathF.Abs(tx.Y - 95) < 1) return Wood[1];
            if ((MathF.Abs(tx.X - 44) < 2.2f && (MathF.Abs(tx.Y - 84) < 2.2f || MathF.Abs(tx.Y - 106) < 2.2f))) return Brass.At(.6f + tx.Key * .3f, tx.X, tx.Y);
            return Grain(Wood, tx, .28f, 5, 4);
        }, Outline, Box(4, 72, 84, 118));
        // A lantern hangs from a hook beneath the board.
        img.Part(Rect(64, 60, 74, 72, 2), 3, tx => tx.Y > 69 ? Brass.At(.4f + tx.Key * .4f, tx.X, tx.Y) : Col.Lerp(Col.Hex("ffd27a"), Col.Hex("fff4cc"), tx.Key), Outline, Box(62, 58, 76, 74));
        img.Part(Rect(68, 71, 70, 76), 1, tx => Brass.At(.3f + tx.Key * .5f, tx.X, tx.Y), Outline, Box(66, 69, 72, 78), outlineWidth: .5f);
        img.Light(69, 66, 30, Col.Hex("ffb050"), .45f);
        img.Part(Shape.Tube(new[] { (24f, 0f), (28f, 6f), (32f, 1f) }, t => 3.2f), 2, tx => Moss.At(.3f + tx.Key * .6f, tx.X, tx.Y), Col.Hex("101a0e"), Box(18, 0, 38, 10));
        return img;
    }

    static Img Stele()
    {
        var img = new Img(112, 184);
        img.Part(Rect(6, 0, 106, 22, 4), 6, tx => Stone.At(.2f + tx.Key * .55f + Noise.Value(tx.X, tx.Y, 6, 9) * .08f, tx.X, tx.Y), Outline, Box(4, 0, 108, 24));
        img.Part((float x, float y, out float t, out float s) =>
        {
            t = (x - 56) / 40; s = (y - 20) / 160;
            // A tall slab with a rounded top and a chipped corner.
            float half = 40 - Math.Max(0, y - 160) * .6f;
            if (y > 176 || y < 18 || MathF.Abs(x - 56) > half) return false;
            if (x > 88 && y > 150 && (x - 88) + (y - 150) > 22) return false;
            return true;
        }, 12, tx =>
        {
            float v = .18f + tx.Key * .58f + Noise.Value(tx.X, tx.Y, 8, 11) * .07f;
            // Engraved lines of script and a glowing star sigil of Lumen.
            float line = (tx.Y - 40) % 11;
            if (tx.Y > 40 && tx.Y < 118 && line < 2 && MathF.Abs(tx.X - 56) < 28 - (tx.Y % 33) * .3f && Noise.H01((int)(tx.X / 4), (int)(tx.Y / 11), 13) > .25f) return Stone[1];
            float dx = tx.X - 56, dy = tx.Y - 142, r = MathF.Sqrt(dx * dx + dy * dy), a = MathF.Atan2(dy, dx);
            float star = 9 + 7 * MathF.Pow(MathF.Abs(MathF.Cos(a * 2)), 6);
            if (r < star && r > star - 3) return Glow.At(.7f, tx.X, tx.Y);
            if (r < 3) return Glow.Last;
            if (Noise.Ridge(tx.X, tx.Y, 28, 15) > .92f) v -= .16f;
            if (tx.Y < 34 && Noise.Value(tx.X, tx.Y, 7, 17) > .1f) return Moss.At(.25f + tx.Key * .5f, tx.X, tx.Y);
            return Stone.At(v, tx.X, tx.Y);
        }, Outline, Box(14, 16, 98, 178));
        img.Light(56, 142, 34, Col.Hex("58e0d0"), .5f);
        return img;
    }

    // The skill tree's runestone: a squat tablet whose face carries a small copy of the tree, a root on top and
    // coloured branches below, some runes lit and some still dark. Short enough for its label to sit above it.
    static readonly (float x, float y, int colour, bool lit)[] Runes =
    {
        (60, 126, 0, true), (60, 111, 1, true), (45, 111, 1, true), (75, 111, 1, true), (60, 96, 0, true),
        (45, 96, 2, true), (30, 96, 2, false), (75, 96, 3, true), (90, 96, 3, false), (60, 81, 4, true), (60, 66, 0, true),
        (45, 66, 5, true), (30, 66, 5, false), (75, 66, 6, true), (90, 66, 6, true), (30, 51, 5, false), (90, 51, 6, false),
        (60, 51, 4, false), (45, 38, 5, false), (75, 38, 6, false)
    };
    static readonly (int a, int b)[] RuneLinks =
    {
        (0, 1), (1, 2), (1, 3), (1, 4), (4, 5), (5, 6), (4, 7), (7, 8), (4, 9), (9, 10), (10, 11), (11, 12), (10, 13), (13, 14),
        (12, 15), (14, 16), (10, 17), (15, 18), (16, 19)
    };
    // Root, arcane, swift, bond, fortune, guard, might: the branch colours of the tree screen.
    static readonly Col[] RuneBright = { Col.Hex("f4ead2"), Col.Hex("b088f8"), Col.Hex("84dc70"), Col.Hex("66d4dc"), Col.Hex("f2c454"), Col.Hex("74a2ff"), Col.Hex("f4664e") };
    static readonly Col[] RuneDeep = { Col.Hex("7e6e50"), Col.Hex("4e3290"), Col.Hex("2c6426"), Col.Hex("1c5a60"), Col.Hex("74541a"), Col.Hex("223e7a"), Col.Hex("782216") };

    static Img Runestone()
    {
        var img = new Img(120, 150);
        img.Part(Rect(6, 0, 114, 16, 4), 6, tx => Stone.At(.2f + tx.Key * .55f + Noise.Value(tx.X, tx.Y, 6, 51) * .08f, tx.X, tx.Y), Outline, Box(4, 0, 116, 18));
        img.Part((float x, float y, out float t, out float s) =>
        {
            t = (x - 60) / 48; s = (y - 14) / 130;
            // A broad slab narrowing a little towards its chamfered top.
            float half = 48 - Math.Max(0, y - 100) * .12f;
            if (y < 14 || y > 144 || MathF.Abs(x - 60) > half) return false;
            return y < 136 || MathF.Abs(x - 60) - (half - 8) + (y - 136) < 0;
        }, 10, tx =>
        {
            for (int i = 0; i < Runes.Length; i++)
            {
                var r = Runes[i]; float dx = MathF.Abs(tx.X + .5f - r.x), dy = MathF.Abs(tx.Y + .5f - r.y);
                if (dx > 4.5f || dy > 4.5f) continue;
                bool rim = dx > 3.4f || dy > 3.4f;
                if (!r.lit) return rim ? Stone[1] : Col.Lerp(Stone[2], RuneDeep[r.colour], .35f);
                return rim ? RuneBright[r.colour] : Col.Lerp(RuneDeep[r.colour], RuneBright[r.colour], .45f + tx.Key * .4f);
            }
            foreach (var (a, b) in RuneLinks)
            {
                var p = Runes[a]; var q = Runes[b];
                float vx = q.x - p.x, vy = q.y - p.y, u = Math.Clamp(((tx.X + .5f - p.x) * vx + (tx.Y + .5f - p.y) * vy) / (vx * vx + vy * vy), 0, 1);
                float ex = p.x + vx * u - tx.X - .5f, ey = p.y + vy * u - tx.Y - .5f;
                if (ex * ex + ey * ey > 1.3f) continue;
                return p.lit && q.lit ? Col.Lerp(RuneDeep[q.colour], RuneBright[q.colour], .6f) : Stone[1];
            }
            float v = .2f + tx.Key * .55f + Noise.Value(tx.X, tx.Y, 8, 53) * .07f;
            if (Noise.Ridge(tx.X, tx.Y, 26, 55) > .93f) v -= .16f;
            if (tx.Y < 28 && Noise.Value(tx.X, tx.Y, 7, 57) > .15f) return Moss.At(.25f + tx.Key * .5f, tx.X, tx.Y);
            return Stone.At(v, tx.X, tx.Y);
        }, Outline, Box(10, 12, 110, 146), smooth: 1);
        img.Light(60, 96, 52, Col.Hex("8cc0ff"), .4f);
        return img;
    }

    static Img Shrine()
    {
        const int w = 308, h = 324; var img = new Img(w, h);
        const float cx = w / 2f;
        // Two lantern pillars frame a tall monolith that carries the star of Lumen.
        foreach (float px in new[] { cx - 128, cx + 128 })
        {
            img.Part(Rect(px - 11, 0, px + 11, 210, 2), 8, tx =>
            {
                float drum = tx.Y % 42;
                float v = .2f + tx.Key * .55f + (drum < 2 ? -.2f : drum > 39 ? .12f : 0);
                return Stone.At(v, tx.X, tx.Y);
            }, Outline, Box(px - 13, 0, px + 13, 212));
            img.Part(Rect(px - 17, 210, px + 17, 222, 3), 4, tx => Stone.At(.35f + tx.Key * .5f, tx.X, tx.Y), Outline, Box(px - 19, 208, px + 19, 224));
            img.Part(Rect(px - 10, 222, px + 10, 248, 3), 4, tx => tx.Y > 244 || tx.Y < 226 ? Brass.At(.35f + tx.Key * .5f, tx.X, tx.Y) : Col.Lerp(Col.Hex("ffc86a"), Col.Hex("fff6d8"), tx.Key * .8f), Outline, Box(px - 12, 220, px + 12, 250));
            img.Part(Shape.Tube(new[] { (px - 13, 248f), (px, 262f), (px + 13, 248f) }, t => 3.5f), 3, tx => Brass.At(.3f + tx.Key * .6f, tx.X, tx.Y), Outline, Box(px - 18, 244, px + 18, 268));
            img.Light(px, 236, 60, Col.Hex("ffb050"), .5f);
        }
        img.Part((float x, float y, out float t, out float s) =>
        {
            t = (x - cx) / 46; s = y / 320;
            float half = 46 - Math.Max(0, y - 250) * .42f;
            return y > 100 && y < 322 && MathF.Abs(x - cx) < half && !(y > 300 && MathF.Abs(x - cx) > 40 - (y - 300));
        }, 18, tx =>
        {
            float v = .16f + tx.Key * .6f + Noise.Value(tx.X, tx.Y, 9, 21) * .07f;
            float dx = tx.X - cx, dy = tx.Y - 238, r = MathF.Sqrt(dx * dx + dy * dy), a = MathF.Atan2(dy, dx);
            float star = 14 + 13 * MathF.Pow(MathF.Abs(MathF.Cos(a * 2)), 8);
            if (r < star && r > star - 3.4f) return Glow.At(.75f, tx.X, tx.Y);
            if (r < 4.5f) return Glow.Last;
            // Nine small constellations, one for each myth the gods came to restore.
            for (int k = 0; k < 9; k++)
            {
                float sx = cx + (k % 3 - 1) * 24, sy = 196 - (k / 3) * 26 + (k % 2) * 4;
                float ddx = tx.X - sx, ddy = tx.Y - sy;
                if (ddx * ddx + ddy * ddy < 3.2f) return Glow.At(.55f, tx.X, tx.Y);
            }
            if (Noise.Ridge(tx.X, tx.Y, 30, 23) > .93f) v -= .16f;
            return Stone.At(v, tx.X, tx.Y);
        }, Outline, Box(cx - 50, 98, cx + 50, 323), smooth: 2);
        img.Light(cx, 238, 70, Col.Hex("58e0d0"), .55f);
        // The altar: a broad stepped stone base with a carved frieze and nine cups along its top edge.
        img.Part(Rect(cx - 148, 0, cx + 148, 18, 3), 6, tx => Stone.At(.18f + tx.Key * .5f + Noise.Value(tx.X, tx.Y, 7, 25) * .08f, tx.X, tx.Y), Outline, Box(cx - 150, 0, cx + 150, 20));
        img.Part(Rect(cx - 136, 16, cx + 136, 124, 4), 12, tx =>
        {
            float v = .2f + tx.Key * .55f + Noise.Value(tx.X, tx.Y, 8, 27) * .07f;
            if (tx.Y > 96 && tx.Y < 108) return Stone.At(v + (MathF.Abs((tx.X - cx) % 18) < 2 ? -.25f : .08f), tx.X, tx.Y);
            if (tx.Y > 40 && tx.Y < 82 && MathF.Abs((tx.X - cx + 9) % 34) < 26 && MathF.Abs((tx.X - cx + 9) % 34) > 22) return Stone[1];
            if (tx.Y < 30 && Noise.Value(tx.X, tx.Y, 6, 29) > .2f) return Moss.At(.2f + tx.Key * .55f, tx.X, tx.Y);
            if (Noise.Ridge(tx.X, tx.Y, 26, 31) > .93f) v -= .16f;
            return Stone.At(v, tx.X, tx.Y);
        }, Outline, Box(cx - 138, 14, cx + 138, 126), smooth: 1);
        img.Part(Rect(cx - 142, 122, cx + 142, 132, 3), 4, tx => Stone.At(.4f + tx.Key * .5f, tx.X, tx.Y), Outline, Box(cx - 144, 120, cx + 144, 134));
        for (int k = 0; k < 9; k++)
        {
            // Matches the runtime flames at ShrineX - 1.24 + k * .31 units, 1.42 units up.
            float x = cx + (-1.24f + k * .31f) * Ppu, y = 132;
            img.Part(Shape.Union(Rect(x - 8, y, x + 8, y + 7, 2), Rect(x - 4, y - 1, x + 4, y + 1)), 3, tx => Brass.At(.3f + tx.Key * .6f, tx.X, tx.Y), Outline, Box(x - 10, y - 3, x + 10, y + 9));
        }
        return img;
    }

    static Img Dummy()
    {
        var img = new Img(96, 170);
        img.Part(Rect(44, 0, 52, 120), 3, tx => Grain(Wood, tx, .15f, 41, .25f), Outline, Box(42, 0, 54, 122));
        // Crossbar arms wrapped in straw.
        img.Part(Rect(8, 92, 88, 102, 2), 4, tx =>
        {
            float wrap = (tx.X * .9f + tx.Y * .6f) % 7;
            return wrap < 1.4f ? Rope.At(.3f + tx.Key * .4f, tx.X, tx.Y) : Straw.At(.25f + tx.Key * .6f + Noise.Value(tx.X, tx.Y, 3, 43) * .1f, tx.X, tx.Y);
        }, Outline, Box(6, 90, 90, 104));
        // Straw body bound with rope, a red target painted on the chest.
        img.Part(Shape.Ellipse(48, 76, 23, 34, 0, (x, y) => Noise.Value(x, y, 4, 45) * .06f), 10, tx =>
        {
            if (MathF.Abs(tx.Y - 56) < 2 || MathF.Abs(tx.Y - 98) < 2) return Rope.At(.3f + tx.Key * .5f, tx.X, tx.Y);
            float dx = tx.X - 46, dy = tx.Y - 80, r = MathF.Sqrt(dx * dx + dy * dy);
            if (r < 13) return (int)(r / 4.4f) % 2 == 0 ? Col.Lerp(Col.Hex("a8281e"), Col.Hex("d84a34"), tx.Key) : Col.Lerp(Col.Hex("c8b48a"), Col.Hex("eadcb4"), tx.Key);
            float strand = Noise.Value(tx.X * 2.5f, tx.Y * .35f, 3, 47);
            return Straw.At(.2f + tx.Key * .6f + strand * .18f, tx.X, tx.Y);
        }, Outline, Box(22, 40, 74, 112));
        // Burlap sack head with stitched eyes and a rope collar.
        img.Part(Shape.Ellipse(48, 130, 19, 21, 0, (x, y) => Noise.Value(x, y, 5, 49) * .05f), 8, tx =>
        {
            float dx = tx.X - 48, dy = tx.Y - 132;
            foreach (float ex in new[] { -7f, 7f })
                if (MathF.Abs(MathF.Abs(dx - ex) - MathF.Abs(dy - 2)) < 1.1f && MathF.Abs(dx - ex) < 4 && MathF.Abs(dy - 2) < 4) return Col.Hex("2a1c14");
            if (dy < -8 && dy > -10 && MathF.Abs(dx) < 8 && ((int)tx.X % 3) != 0) return Col.Hex("2a1c14");
            float weave = ((int)tx.X + (int)tx.Y) % 3 == 0 ? -.06f : 0;
            return Burlap.At(.2f + tx.Key * .6f + weave, tx.X, tx.Y);
        }, Outline, Box(28, 108, 68, 152));
        img.Part(Rect(36, 108, 60, 113, 1), 2, tx => Rope.At(.3f + tx.Key * .5f, tx.X, tx.Y), Outline, Box(34, 106, 62, 115));
        // A tuft of straw pokes out of the top of the sack.
        img.Part(Shape.Tube(new[] { (44f, 148f), (40f, 160f), (35f, 166f) }, t => 3 - t * 2), 2, tx => Straw.At(.5f + tx.Key * .4f, tx.X, tx.Y), Outline, Box(30, 146, 50, 170));
        img.Part(Shape.Tube(new[] { (52f, 148f), (57f, 159f), (63f, 164f) }, t => 3 - t * 2), 2, tx => Straw.At(.5f + tx.Key * .4f, tx.X, tx.Y), Outline, Box(48, 146, 68, 168));
        return img;
    }
}
