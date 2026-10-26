namespace StoryPainter;

/// <summary>
/// Breakable caches of the nine tower themes and the loot they scatter, at 96 texels per unit, each cache pivoted at
/// its bottom centre (Resources/Tower/Breakables). Every theme has a fragile vessel that shatters at one blow and a
/// sturdy container that takes two; the runtime cuts the painting itself into shards when it breaks.
/// </summary>
public static class Breakables
{
    static readonly Col Outline = Col.Hex("140c0a");
    static readonly Ramp Terracotta = new("4a1e10", "6e2c16", "94401e", "b45a28", "cc7436", "e0944e", "f0b678");
    static readonly Ramp Glaze = new("0c0808", "181010", "261c1a", "3a2e2a", "524440");
    static readonly Ramp Wood = new("2a1a10", "40281a", "583824", "70482e", "8a5c3a", "a6744a", "c08e5e");
    static readonly Ramp Bronze = new("3a2410", "5e3c1a", "8a5a26", "b07a34", "d09a48", "ecc070", "fbe4a0");
    static readonly Ramp Iron = new("16181c", "24282e", "343a42", "4a525c", "646e7a", "8490a0");
    static readonly Ramp Ice = new("163452", "24527a", "3672a0", "5294c2", "78b6dc", "a8d6ee", "dcf2fc");
    static readonly Ramp Snow = new("8aa4bc", "aec4d8", "cedde9", "e8f0f6", "ffffff");
    static readonly Ramp Gold = new("4a3008", "7a5210", "a8761c", "d09c2c", "eec04a", "fbe07c", "fff4c0");
    static readonly Ramp Onggi = new("140a06", "24140c", "361e12", "4c2c1a", "643c24", "7e5034", "9a6a4a");
    static readonly Ramp Straw = new("4a3414", "6c4e1e", "8e6a2a", "b08a3a", "ccaa52", "e4c870", "f4e09a");
    static readonly Ramp Rope = new("3e2c18", "5e4426", "7e5e36", "9e7c4a", "b8965e");
    static readonly Ramp Wicker = new("3a2410", "5a3a1c", "7c5428", "9e7036", "bc8e4a", "d6ac66");
    static readonly Ramp Apple = new("3a0608", "5e0c10", "861418", "ac2020", "cc3a2c", "e8644a", "f89c7a");
    static readonly Ramp Leaf = new("10240e", "1c3a16", "2c5420", "40702a", "5a8c36", "7aa848", "9cc462");
    static readonly Ramp Clay = new("4a1a0e", "6c2814", "903a1c", "b04e26", "c86834", "dc8a50", "eeaa74");
    static readonly Ramp Chalk = new("b8b0a0", "d8d0c0", "f4f0e4");
    static readonly Ramp Brass = new("4a3010", "6e4a18", "946824", "b88a34", "d8ac4c", "f0cc70", "fff0b0");
    static readonly Ramp Husk = new("2a180c", "3e2614", "56361e", "6e4a2c", "8a6040", "a67c56");
    static readonly Ramp Alabaster = new("8a7e66", "a89a80", "c4b89c", "dcd2b8", "eee6d2", "faf6ea");
    static readonly Ramp Lapis = new("0c1a3a", "14285a", "1e3a7e", "2c52a4", "4470c4", "6a92dc");
    static readonly Ramp Jet = new("0a0a0e", "16161c", "24242c", "34343e", "4a4a56");
    static readonly Ramp Porcelain = new("8c96a4", "aab4c0", "c8d0da", "e2e8ee", "f4f7fa", "ffffff");
    static readonly Ramp Cobalt = new("0a1640", "122468", "1c3690", "2a4cb0", "4468c8");
    static readonly Ramp Lacquer = new("2a0606", "48080a", "6a0e0e", "8e1814", "b02a1c", "cc4430", "e46a4a");
    static readonly Ramp Charred = new("0a0606", "160c0a", "22140e", "301c14", "40281c", "523424");
    static readonly Ramp Ember = new("5a1004", "a02a08", "e0560e", "ff8a24", "ffc060", "fff0b0");
    static readonly Ramp Bone = new("4a4034", "6e6252", "92867a", "b4a998", "d4ccbc", "ece6da");
    static readonly Ramp Flesh = new("1e060e", "3a0c18", "5a1424", "7a1e32", "9c2c44", "be4a5e", "dc7a88");
    static readonly Ramp Void = new("08040e", "140a20", "221236", "341c50", "4a2a70", "6a3e98");
    static readonly Ramp VoidGlow = new("6a2aa0", "9a48d8", "c878ff", "e8b0ff", "fbe8ff");
    static readonly Ramp Rock = new("14121a", "221e28", "302a38", "403a4a", "56505e", "6e6878");
    static readonly Ramp Blood = new("3a0410", "64081c", "920e26", "be1a34", "e03248", "f45a6a", "ffa0a8");
    static readonly Ramp Teal = new("0e4a46", "1a7a72", "2eaa9c", "5ad4c4", "a0f2e6", "eafffb");

    public static IEnumerable<(string name, Img img)> Paint()
    {
        yield return ("olympus-amphora", Amphora());
        yield return ("olympus-chest", OlympusChest());
        yield return ("asgard-ice", IceBlock());
        yield return ("asgard-barrel", MeadBarrel());
        yield return ("dangun-onggi", OnggiJar());
        yield return ("dangun-sack", StrawSack());
        yield return ("eden-basket", AppleBasket());
        yield return ("eden-crate", VineCrate());
        yield return ("veda-matka", Matka());
        yield return ("veda-kalasha", Kalasha());
        yield return ("papyrus-canopic", CanopicJar());
        yield return ("papyrus-chest", PaintedChest());
        yield return ("chinese-vase", BlueWhiteVase());
        yield return ("chinese-chest", LacquerBox());
        yield return ("hell-urn", SkullUrn());
        yield return ("hell-crate", EmberCrate());
        yield return ("abyss-sac", EggSac());
        yield return ("abyss-crystal", VoidCrystal());
        yield return ("heart", Heart());
        yield return ("coin", Coin(30, 13, false));
        yield return ("gold-coin", Coin(40, 18, true));
    }

    // ---------------------------------------------------------------- shapes and shading helpers

    static (int, int, int, int) Box(float x0, float y0, float x1, float y1) => ((int)x0, (int)y0, (int)MathF.Ceiling(x1), (int)MathF.Ceiling(y1));
    static Inside Rect(float x0, float y0, float x1, float y1, float bevel = 0) => (float x, float y, out float t, out float s) =>
    {
        t = (x - x0) / (x1 - x0); s = (y - y0) / (y1 - y0);
        if (x < x0 || x > x1 || y < y0 || y > y1) return false;
        return Math.Min(x - x0, x1 - x) + Math.Min(y - y0, y1 - y) >= bevel;
    };
    /// <summary>A turned vessel: half-width r(y) about cx between y0 and y1; t runs -1..1 across it, s 0..1 up.</summary>
    static Inside Lathe(float cx, float y0, float y1, Func<float, float> r) => (float x, float y, out float t, out float s) =>
    {
        s = (y - y0) / (y1 - y0); float w = y < y0 || y > y1 ? 0 : r(y);
        t = w > 0 ? (x - cx) / w : 0;
        return w > 0 && MathF.Abs(x - cx) < w;
    };
    static Inside Poly(params (float x, float y)[] p) => (float x, float y, out float t, out float s) =>
    {
        t = s = 0; bool inside = false;
        for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x) inside = !inside;
        return inside;
    };
    /// <summary>Smoothly interpolated half-widths through (y, r) pairs.</summary>
    static Func<float, float> Profile(params float[] yr) => y =>
    {
        int n = yr.Length / 2;
        if (y <= yr[0]) return yr[1];
        for (int i = 0; i + 1 < n; i++)
        {
            float y0 = yr[i * 2], y1 = yr[i * 2 + 2];
            if (y > y1) continue;
            float k = (y - y0) / Math.Max(.001f, y1 - y0); k = k * k * (3 - 2 * k);
            return yr[i * 2 + 1] + (yr[i * 2 + 3] - yr[i * 2 + 1]) * k;
        }
        return yr[n * 2 - 1];
    };
    /// <summary>Position around a turned vessel, 0..1 over its visible half, so painted bands wrap with the surface.</summary>
    static float Around(float t) => MathF.Asin(Math.Clamp(t, -1, 1)) / MathF.PI + .5f;
    static readonly V3 Half = new V3(Img.Key.X, Img.Key.Y, Img.Key.Z + 1).N();
    /// <summary>A glaze or metal highlight from the key light.</summary>
    static Col Gloss(Col c, Texel tx, float strength, float power = 26)
    { float spec = MathF.Pow(Math.Max(0, tx.Normal.Dot(Half)), power); return c.Add(new Col(1, 1, 1), spec * strength); }
    static float Shade(Texel tx, float bias, float key = .6f) => bias + tx.Key * key + tx.Rim * .06f;
    static float Segment(float px, float py, float ax, float ay, float bx, float by)
    {
        float vx = bx - ax, vy = by - ay, l2 = vx * vx + vy * vy;
        float u = l2 > 0 ? Math.Clamp(((px - ax) * vx + (py - ay) * vy) / l2, 0, 1) : 0;
        float dx = px - ax - vx * u, dy = py - ay - vy * u; return MathF.Sqrt(dx * dx + dy * dy);
    }
    /// <summary>Boards with dark seams every <paramref name="plank"/> texels and grain running along them.</summary>
    static Col Planks(Ramp ramp, Texel tx, float bias, int seed, float plank = 11, bool vertical = false)
    {
        float across = vertical ? tx.X : tx.Y, along = vertical ? tx.Y : tx.X;
        int board = (int)MathF.Floor(across / plank); float seam = across - board * plank;
        float g = Noise.Fbm(vertical ? tx.X * 4 : tx.X * .3f, vertical ? tx.Y * .3f : tx.Y * 4, 14, 2, seed + board * 17);
        float v = Shade(tx, bias, .55f) + g * .12f + (Noise.H01(board, 0, seed) - .5f) * .14f;
        if (seam < 1.2f) v -= .3f; else if (seam < 2.2f) v += .05f;
        if (Noise.Ridge(along * .4f, across * 2.2f, 16, seed + 5) > .88f) v -= .12f;
        return ramp.At(v, tx.X, tx.Y, .35f);
    }
    static Col Metal(Ramp ramp, Texel tx, float bias = .1f, float gloss = .55f) => Gloss(ramp.At(Shade(tx, bias, .7f), tx.X, tx.Y), tx, gloss);
    static void Rivet(Img img, float x, float y, Ramp ramp, float r = 2.2f) =>
        img.Part(Shape.Ellipse(x, y, r, r), r, tx => Metal(ramp, tx, .25f, .8f), Outline, Box(x - r - 1, y - r - 1, x + r + 1, y + r + 1), outlineWidth: .6f);
    static void Leaves(Img img, (float x, float y)[] path, int count, float size, int seed)
    {
        img.Part(Shape.Tube(path, t => 1.6f), 2, tx => Leaf.At(Shade(tx, .05f), tx.X, tx.Y), Outline, outlineWidth: .7f);
        for (int i = 0; i < count; i++)
        {
            var (px, py) = path[(int)((i + .5f) / count * (path.Length - 1))];
            float angle = (i % 2 == 0 ? 35 : -35) + (Noise.H01(i, 1, seed) - .5f) * 40, side = i % 2 == 0 ? 1 : -1;
            float lx = px + side * size * .6f, ly = py + size * .25f;
            img.Part(Shape.Ellipse(lx, ly, size, size * .45f, angle), size * .6f, tx =>
            {
                float vein = MathF.Abs(tx.S) < .12f ? -.18f : 0;
                return Leaf.At(Shade(tx, .12f) + vein + Noise.Value(tx.X, tx.Y, 3, seed + i) * .05f, tx.X, tx.Y);
            }, Outline, Box(lx - size - 2, ly - size - 2, lx + size + 2, ly + size + 2), outlineWidth: .8f);
        }
    }

    // ---------------------------------------------------------------- Olympus

    static Img Amphora()
    {
        const int w = 78, h = 100; const float cx = w / 2f; var img = new Img(w, h);
        var r = Profile(0, 13, 4, 13, 6, 9, 11, 9.5f, 18, 15, 32, 25, 46, 30, 58, 28.5f, 68, 22, 74, 14, 78, 10, 88, 10.5f, 91, 14.5f, 95, 14, 96, 12);
        foreach (int side in new[] { -1, 1 })
            img.Part(Shape.Tube(Shape.Bezier((cx + side * 20, 68), (cx + side * 34, 72), (cx + side * 32, 93), (cx + side * 11, 87)), t => 3.2f), 3,
                tx => Gloss(Glaze.At(Shade(tx, .15f, .7f), tx.X, tx.Y), tx, .6f), Outline, Box(side < 0 ? 0 : cx + 6, 62, side < 0 ? cx - 6 : w - 1, 99));
        img.Part(Lathe(cx, 0, 96, r), 22, tx =>
        {
            float y = tx.Y, u = Around(tx.T), v = Shade(tx, .1f, .66f) + Noise.Value(tx.X, tx.Y, 6, 3) * .03f;
            bool black;
            if (y < 11 || y >= 26 && y < 30 || y >= 58 && y < 62 || y >= 70 && y < 74) black = true;
            else if (y < 14 || y >= 30 && y < 34 || y >= 54 && y < 58) black = false;
            // Rays rising from the foot, a Greek key under the shoulder and a running wave between dotted borders.
            else if (y < 26) { float f = u * 14 % 1; black = MathF.Abs(f - .5f) < (26 - y) / 12 * .5f; }
            else if (y >= 62 && y < 70) black = Meander(u * 11, (y - 62) / 8);
            else if (y >= 74) black = !(y >= 83 && y < 85);
            else black = (y >= 36 && y < 39 || y >= 49 && y < 52) && u * 26 % 1 < .45f || y >= 40 && y < 48 && y < 44 + MathF.Sin(u * MathF.PI * 10) * 3.6f;
            return black ? Gloss(Glaze.At(v, tx.X, tx.Y), tx, .75f) : Gloss(Terracotta.At(v + .1f, tx.X, tx.Y), tx, .22f);
        }, Outline, Box(cx - 32, 0, cx + 32, 97), smooth: 1);
        return img;
    }
    // One cell of a Greek key: a hooked spiral between two border lines.
    static bool Meander(float a, float b)
    {
        float x = a - MathF.Floor(a), y = b;
        if (y < .14f || y > .86f) return true;
        return x < .16f && y < .7f || y > .54f && y < .7f && x < .66f || x > .5f && x < .66f && y > .3f && y < .7f || y > .3f && y < .44f && x > .3f && x < .66f;
    }

    static Img OlympusChest()
    {
        var img = Chest(104, 80, 50, true, tx => Planks(Wood, tx, .18f, 11), tx => Planks(Wood, tx, .26f, 13, 9, true), tx => Metal(Bronze, tx), new[] { 24f, 80f }, Bronze);
        // A bronze laurel wreath around the lock.
        const float cx = 52, cy = 56;
        for (int side = -1; side <= 1; side += 2)
            for (int k = 0; k < 4; k++)
            {
                float a = MathF.PI * (.55f + k * .16f), lx = cx + side * MathF.Cos(a) * -11, ly = cy + MathF.Sin(a) * 9 - 6;
                img.Part(Shape.Ellipse(lx, ly, 3.4f, 1.6f, side * (40 + k * 25)), 2, tx => Metal(Bronze, tx, .2f, .7f), Outline, Box(lx - 5, ly - 5, lx + 5, ly + 5), outlineWidth: .6f);
            }
        return img;
    }

    /// <summary>A box chest: plank body, a domed or flat lid, metal straps, corner caps and a lock plate.</summary>
    static Img Chest(int w, int h, float bodyTop, bool domed, Func<Texel, Col> body, Func<Texel, Col> lid, Func<Texel, Col> metal, float[] straps, Ramp lockRamp)
    {
        var img = new Img(w, h); float cx = w / 2f, x0 = 5, x1 = w - 5;
        float LidTop(float x) { float t = (x - cx) / (cx - x0 + 2); return domed ? bodyTop + (h - 2 - bodyTop) * MathF.Sqrt(Math.Max(0, 1 - t * t * .8f)) : h - 2; }
        img.Part(Rect(x0, 0, x1, bodyTop, 2), 7, body, Outline, Box(x0 - 2, 0, x1 + 2, bodyTop + 2));
        img.Part((float x, float y, out float t, out float s) =>
        {
            t = (x - x0) / (x1 - x0); s = (y - bodyTop) / (h - bodyTop);
            return x >= x0 - 2 && x <= x1 + 2 && y >= bodyTop - 2 && y <= LidTop(x);
        }, 8, lid, Outline, Box(x0 - 3, bodyTop - 3, x1 + 3, h));
        img.Part(Rect(x0 - 2, bodyTop - 3, x1 + 2, bodyTop + 2, 1), 2, metal, Outline, Box(x0 - 4, bodyTop - 5, x1 + 4, bodyTop + 4));
        foreach (float sx in straps)
            img.Part((float x, float y, out float t, out float s) => { t = (x - sx) / 4; s = y / h; return MathF.Abs(x - sx) < 4 && y >= 0 && y <= LidTop(x) - .5f; },
                3, metal, Outline, Box(sx - 6, 0, sx + 6, h));
        foreach (float corner in new[] { x0, x1 })
        {
            float a = corner < cx ? corner - 2 : corner - 9, b = corner < cx ? corner + 9 : corner + 2;
            img.Part(Shape.Union(Rect(a, 0, b, 5, 1), Rect(corner < cx ? a : b - 5, 0, corner < cx ? a + 5 : b, 13, 1)), 2, metal, Outline, Box(a - 2, 0, b + 2, 15));
            foreach (float sx in straps) { Rivet(img, sx, 8, lockRamp, 1.6f); Rivet(img, sx, bodyTop - 9, lockRamp, 1.6f); }
        }
        img.Part(Rect(cx - 8, bodyTop - 14, cx + 8, bodyTop + 5, 3), 3, tx =>
        {
            float dx = tx.X - cx, dy = tx.Y - (bodyTop - 6);
            if (dx * dx + dy * dy < 4.5f || MathF.Abs(dx) < 1 && dy < 0 && dy > -5) return Col.Hex("100804");
            return Metal(lockRamp, tx, .2f, .7f);
        }, Outline, Box(cx - 10, bodyTop - 16, cx + 10, bodyTop + 7));
        return img;
    }

    // ---------------------------------------------------------------- Asgard

    static Img IceBlock()
    {
        const int w = 92, h = 84; var img = new Img(w, h);
        var block = Poly((8, 0), (84, 0), (88, 14), (82, 38), (86, 56), (70, 74), (52, 70), (40, 80), (22, 70), (6, 52), (10, 30), (4, 14));
        (float x, float y)[] facets = { (46, 40), (20, 20), (70, 18), (26, 56), (66, 52), (46, 70), (82, 34), (12, 38) };
        img.Part(block, 14, tx =>
        {
            // Flat facets: each texel takes the tone of its nearest facet seed, with bright cleavage lines between them.
            int best = 0, second = 0; float d1 = 1e9f, d2 = 1e9f;
            for (int k = 0; k < facets.Length; k++)
            {
                float dx = tx.X - facets[k].x, dy = tx.Y - facets[k].y, d = dx * dx + dy * dy;
                if (d < d1) { d2 = d1; second = best; d1 = d; best = k; } else if (d < d2) { d2 = d; second = k; }
            }
            float edge = MathF.Sqrt(d2) - MathF.Sqrt(d1);
            float v = .28f + tx.Key * .3f + Noise.H01(best, 3, 7) * .3f + tx.Rim * .1f;
            var c = Ice.At(v, tx.X, tx.Y, .25f);
            // Coins frozen in the heart of the block glint through the ice.
            float cx = tx.X - 46, cy = tx.Y - 34;
            if (cx * cx / 300 + cy * cy / 160 < 1)
            {
                float coin = Noise.Value(tx.X, tx.Y, 3.5f, 9);
                var gold = Gold.At(.35f + coin * .35f + tx.Key * .3f, tx.X, tx.Y);
                c = Col.Lerp(c, gold, .55f - (cx * cx / 300 + cy * cy / 160) * .3f);
            }
            if (edge < 1.1f) c = Col.Lerp(c, Ice.Last, .55f);
            if (tx.Y > 62 - Noise.Value(tx.X, 0, 6, 11) * 4 && tx.Normal.Y > -.2f) c = Snow.At(.4f + tx.Key * .6f, tx.X, tx.Y);
            return Gloss(c, tx, .9f, 18);
        }, Col.Hex("0e2236"), Box(2, 0, 90, 82), outlineWidth: 1);
        img.Light(46, 34, 26, Col.Hex("ffd27a"), .35f);
        return img;
    }

    static Img MeadBarrel()
    {
        const int w = 80, h = 92; const float cx = w / 2f; var img = new Img(w, h);
        Func<float, float> r = y => 29 + 6 * MathF.Sin(MathF.PI * Math.Clamp(y / 86, 0, 1));
        img.Part(Lathe(cx, 0, 86, r), 20, tx =>
        {
            float u = Around(tx.T), stave = u * 8 % 1;
            float v = Shade(tx, .12f, .6f) + Noise.Fbm(tx.X * 3, tx.Y * .3f, 14, 2, 21 + (int)(u * 8)) * .12f + (Noise.H01((int)(u * 8), 0, 23) - .5f) * .12f;
            if (stave < .06f) v -= .3f;
            // The rune of wealth, fehu, cut into the middle staves.
            float rx = tx.X - cx, ry = tx.Y - 44;
            float rune = Math.Min(Segment(rx, ry, -5, -14, -5, 14), Math.Min(Segment(rx, ry, -5, 2, 6, 10), Segment(rx, ry, -5, 9, 5, 16)));
            if (rune < 1.4f) return Col.Lerp(Wood[0], Col.Hex("6ad8ff"), .45f);
            if (rune < 2.4f) v += .08f;
            return Wood.At(v, tx.X, tx.Y, .35f);
        }, Outline, Box(cx - 37, 0, cx + 37, 88), smooth: 1);
        foreach (float y0 in new[] { 8f, 30f, 56f, 76f })
            img.Part(Lathe(cx, y0, y0 + 5, y => r(y) + 1.2f), 3, tx =>
            {
                var c = Metal(Iron, tx, .12f, .5f);
                return tx.S > .6f && Noise.Value(tx.X, tx.Y, 3, 31) > -.2f ? Snow.At(.3f + tx.Key * .6f, tx.X, tx.Y) : c;
            }, Outline, Box(cx - 38, y0 - 1, cx + 38, y0 + 7));
        img.Part(Shape.Ellipse(cx, 86, 29, 5.5f), 4, tx =>
        {
            float rings = MathF.Sqrt(tx.T * tx.T + tx.S * tx.S) * 5 % 1;
            var c = Wood.At(.45f + tx.Key * .3f + (rings < .15f ? -.2f : 0), tx.X, tx.Y);
            return Noise.Value(tx.X, tx.Y, 4, 33) > -.1f ? Snow.At(.55f + tx.Key * .45f, tx.X, tx.Y) : c;
        }, Outline, Box(cx - 31, 79, cx + 31, 92));
        return img;
    }

    // ---------------------------------------------------------------- Dangun

    static Img OnggiJar()
    {
        const int w = 84, h = 88; const float cx = w / 2f; var img = new Img(w, h);
        var r = Profile(0, 21, 4, 23, 20, 33, 46, 38, 60, 34, 70, 22, 74, 19, 76, 21, 79, 20);
        img.Part(Lathe(cx, 0, 79, r), 24, tx =>
        {
            float u = Around(tx.T), v = Shade(tx, .1f, .62f) + Noise.Fbm(tx.X, tx.Y, 10, 2, 41) * .06f;
            // Throwing rings and the potter's finger-drawn grass on the shoulder.
            if ((tx.Y * .55f) % 1 < .12f && tx.Y < 52) v -= .05f;
            if (tx.Y > 50 && tx.Y < 66)
            {
                float stroke = 58 + MathF.Sin(u * MathF.PI * 9) * 5 + (u * 9 % 1 - .5f) * 6;
                if (MathF.Abs(tx.Y - stroke) < 1.1f) v += .22f;
            }
            if (tx.Y < 6) v -= .1f;
            return Gloss(Onggi.At(v, tx.X, tx.Y), tx, .7f, 20);
        }, Outline, Box(cx - 40, 0, cx + 40, 80), smooth: 2);
        img.Part(Lathe(cx, 77, 87, Profile(77, 20, 80, 18, 84, 12, 87, 4)), 8, tx => Gloss(Onggi.At(Shade(tx, .15f, .6f), tx.X, tx.Y), tx, .6f, 20), Outline, Box(cx - 22, 75, cx + 22, 88));
        img.Part(Shape.Ellipse(cx, 87, 4, 2.4f), 2, tx => Onggi.At(Shade(tx, .3f), tx.X, tx.Y), Outline, Box(cx - 6, 84, cx + 6, 88));
        return img;
    }

    static Img StrawSack()
    {
        const int w = 96, h = 82; var img = new Img(w, h); const float cx = w / 2f;
        Inside sack = (float x, float y, out float t, out float s) =>
        {
            s = y / 70; float half = 40 + MathF.Sin(MathF.PI * Math.Clamp(y / 66, 0, 1)) * 5 - Math.Max(0, y - 52) * 1.2f;
            t = (x - cx) / half;
            return y >= 0 && y <= 70 && MathF.Abs(x - cx) < half;
        };
        img.Part(sack, 18, tx =>
        {
            // Straw mat woven in tight rows, with stray stalks poking out.
            float row = tx.Y % 4, col = (tx.X + ((int)(tx.Y / 4) % 2) * 3) % 6;
            float v = Shade(tx, .08f, .6f) + (row < 1 ? -.14f : 0) + (col < 1 ? -.08f : 0) + Noise.Value(tx.X * 2.5f, tx.Y * .4f, 3, 51) * .1f;
            return Straw.At(v, tx.X, tx.Y);
        }, Outline, Box(cx - 46, 0, cx + 46, 72), smooth: 1);
        foreach (float y0 in new[] { 20f, 48f })
            img.Part(Shape.Tube(new[] { (cx - 42, y0 - 1), (cx, y0 + 1.5f), (cx + 42, y0 - 1) }, t => 2.2f), 2, tx =>
            {
                float twist = (tx.X * .8f + tx.Y) % 5;
                return Rope.At(Shade(tx, .15f) + (twist < 1.2f ? -.2f : 0), tx.X, tx.Y);
            }, Outline, Box(cx - 46, y0 - 5, cx + 46, y0 + 6));
        // The mouth is gathered and tied, leaving a tuft of straw.
        img.Part(Shape.Union(Rect(cx - 8, 66, cx + 8, 72, 2), Rect(cx - 6, 70, cx + 6, 76, 1)), 3, tx => Rope.At(Shade(tx, .2f), tx.X, tx.Y), Outline, Box(cx - 10, 64, cx + 10, 78));
        for (int k = -3; k <= 3; k++)
            img.Part(Shape.Tube(new[] { (cx + k * 1.6f, 75f), (cx + k * 3.2f, 79f), (cx + k * 5.5f, 81f) }, t => 1.4f - t * .8f), 1, tx => Straw.At(.55f + tx.Key * .4f, tx.X, tx.Y), Outline, Box(cx - 22, 73, cx + 22, 82), outlineWidth: .6f);
        return img;
    }

    // ---------------------------------------------------------------- Eden

    static Img AppleBasket()
    {
        const int w = 94, h = 80; const float cx = w / 2f; var img = new Img(w, h);
        (float x, float y, float r, bool golden)[] apples = { (cx - 22, 46, 11, false), (cx + 20, 47, 11, false), (cx - 4, 50, 12, false), (cx - 13, 60, 10.5f, false), (cx + 10, 61, 11, true), (cx + 1, 69, 9.5f, false) };
        foreach (var (ax, ay, ar, golden) in apples)
        {
            img.Part(Shape.Ellipse(ax, ay, ar, ar * .92f, 0, (x, y) => MathF.Abs(x - ax) < 2.5f && y > ay + ar * .7f ? -.12f : 0), ar * .9f, tx =>
            {
                float streak = Noise.Value(tx.X * .6f, tx.Y * 2.2f, 3, 61) * .08f;
                return Gloss((golden ? Gold : Apple).At(Shade(tx, .08f, .7f) + streak, tx.X, tx.Y), tx, .85f, 30);
            }, Outline, Box(ax - ar - 2, ay - ar - 2, ax + ar + 2, ay + ar + 2), outlineWidth: .9f);
            img.Part(Shape.Tube(new[] { (ax, ay + ar * .7f), (ax + 1, ay + ar + 2.5f) }, t => 1), 1, tx => Husk[2], Outline, Box(ax - 3, ay + ar - 3, ax + 4, ay + ar + 5), outlineWidth: .5f);
        }
        img.Part(Shape.Ellipse(cx + 12, 72, 6, 2.6f, 25), 3, tx => Leaf.At(Shade(tx, .15f) + (MathF.Abs(tx.S) < .15f ? -.15f : 0), tx.X, tx.Y), Outline, Box(cx + 4, 66, cx + 20, 78), outlineWidth: .7f);
        Inside basket = (float x, float y, out float t, out float s) =>
        {
            s = y / 44; float half = 33 + y * .22f; t = (x - cx) / half;
            return y >= 0 && y <= 44 && MathF.Abs(x - cx) < half;
        };
        img.Part(basket, 14, tx =>
        {
            // Wicker: upright stakes with weavers passing over and under them.
            float u = Around(tx.T) * 13, stake = u % 1; int course = (int)(tx.Y / 5); bool over = ((int)u + course) % 2 == 0;
            float v = Shade(tx, .1f, .6f) + (over ? .1f : -.08f) + (tx.Y % 5 < 1 ? -.18f : 0) + (stake < .12f ? -.1f : 0);
            return Wicker.At(v, tx.X, tx.Y);
        }, Outline, Box(cx - 45, 0, cx + 45, 46), smooth: 1);
        img.Part(Shape.Tube(new[] { (cx - 43, 43f), (cx, 45.5f), (cx + 43, 43f) }, t => 3), 3, tx =>
            Wicker.At(Shade(tx, .2f) + ((tx.X * .7f + tx.Y) % 4 < 1 ? -.2f : 0), tx.X, tx.Y), Outline, Box(cx - 47, 38, cx + 47, 50));
        return img;
    }

    static Img VineCrate()
    {
        const int w = 98, h = 84; var img = new Img(w, h);
        img.Part(Rect(5, 0, 93, 70, 2), 6, tx =>
        {
            bool frame = tx.X < 13 || tx.X > 85 || tx.Y < 8 || tx.Y > 62;
            float d = Math.Min(Segment(tx.X, tx.Y, 13, 8, 85, 62), Segment(tx.X, tx.Y, 13, 62, 85, 8));
            if (frame || d < 5) return Planks(Wood, tx, d < 5 && !frame ? .3f : .24f, 71, 70, !frame && d >= 5 ? false : tx.X < 13 || tx.X > 85);
            return Planks(Wood, tx, .08f, 73, 9);
        }, Outline, Box(3, 0, 95, 72));
        foreach (var (x, y) in new[] { (9f, 4f), (89f, 4f), (9f, 66f), (89f, 66f) }) Rivet(img, x, y, Iron, 1.8f);
        Leaves(img, Shape.Bezier((2, 6), (30, 20), (18, 58), (52, 74)), 7, 5.5f, 75);
        Leaves(img, Shape.Bezier((96, 30), (78, 48), (90, 70), (64, 80)), 5, 5, 77);
        // A small white blossom on the vine.
        for (int k = 0; k < 5; k++)
        {
            float a = k * MathF.PI * .4f, px = 58 + MathF.Cos(a) * 3, py = 74 + MathF.Sin(a) * 3;
            img.Part(Shape.Ellipse(px, py, 2.6f, 2.6f), 2, tx => Chalk.At(.5f + tx.Key * .5f, tx.X, tx.Y), Outline, Box(px - 4, py - 4, px + 4, py + 4), outlineWidth: .5f);
        }
        img.Part(Shape.Ellipse(58, 74, 1.6f, 1.6f), 1, tx => Gold.At(.7f, tx.X, tx.Y), Outline, Box(55, 71, 61, 77), outlineWidth: .4f);
        return img;
    }

    // ---------------------------------------------------------------- Veda

    static Img Matka()
    {
        const int w = 84, h = 88; const float cx = w / 2f; var img = new Img(w, h);
        var r = Profile(0, 14, 3, 22, 14, 34, 32, 38, 48, 34, 60, 22, 66, 13, 74, 12, 78, 16, 81, 15);
        img.Part(Lathe(cx, 0, 81, r), 24, tx =>
        {
            float u = Around(tx.T), v = Shade(tx, .1f, .62f) + Noise.Fbm(tx.X, tx.Y, 9, 2, 81) * .07f;
            var c = Clay.At(v, tx.X, tx.Y);
            // Chalk-white zigzags and dots painted by hand around the shoulder.
            float zig = 52 + MathF.Abs(u * 12 % 1 - .5f) * 8;
            bool paint = MathF.Abs(tx.Y - zig) < 1.2f || MathF.Abs(tx.Y - 44) < .9f || MathF.Abs(tx.Y - 63) < .9f;
            float du = (u * 16 % 1 - .5f) * 7, dy = tx.Y - 38;
            if (du * du + dy * dy < 2.4f) paint = true;
            return paint ? Chalk.At(.3f + tx.Key * .7f, tx.X, tx.Y) : Gloss(c, tx, .18f);
        }, Outline, Box(cx - 40, 0, cx + 40, 82), smooth: 2);
        return img;
    }

    static Img Kalasha()
    {
        const int w = 84, h = 104; const float cx = w / 2f; var img = new Img(w, h);
        // Five mango leaves fan out under a coconut crowning the pot.
        float[] angles = { -62, -32, 0, 32, 62 };
        foreach (float a in angles)
        {
            float rad = (90 - a) * MathF.PI / 180, tipX = cx + MathF.Cos(rad) * 34, tipY = 66 + MathF.Sin(rad) * 30;
            img.Part(Shape.Tube(Shape.Bezier((cx, 66), (cx + (tipX - cx) * .35f, 66 + (tipY - 66) * .5f), (cx + (tipX - cx) * .8f, 66 + (tipY - 66) * .95f), (tipX, tipY), 30), t => 6.2f * MathF.Sin(MathF.PI * Math.Clamp(t * .92f + .08f, 0, 1))), 4,
                tx => Leaf.At(Shade(tx, .1f) + (MathF.Abs(tx.S) < .14f ? -.2f : 0) + Noise.Value(tx.X, tx.Y, 4, 91) * .05f, tx.X, tx.Y), Outline, Box(cx - 46, 60, cx + 46, 103), outlineWidth: .9f);
        }
        img.Part(Shape.Ellipse(cx, 80, 14, 17, 0, (x, y) => Noise.Value(x, y, 3, 93) * .04f), 12, tx =>
        {
            float fiber = Noise.Value(tx.X * 3, tx.Y * .5f, 3, 95);
            return Husk.At(Shade(tx, .08f, .6f) + fiber * .14f, tx.X, tx.Y);
        }, Outline, Box(cx - 16, 61, cx + 16, 99));
        var r = Profile(0, 15, 3, 19, 14, 30, 30, 34, 42, 30, 52, 18, 58, 12, 62, 13, 66, 18, 69, 17);
        img.Part(Lathe(cx, 0, 69, r), 22, tx =>
        {
            float u = Around(tx.T), v = Shade(tx, .08f, .7f);
            // Red and saffron thread wound around the neck, and three rows of vermilion dots.
            if (tx.Y > 54 && tx.Y < 61) return (int)(tx.Y + u * 30) % 3 == 0 ? Col.Hex("f0b030") : Col.Lerp(Col.Hex("8a1010"), Col.Hex("d02a1a"), tx.Key);
            float du = (u * 10 % 1 - .5f) * 6, dy = (tx.Y - 34) % 7 - 3.5f;
            if (tx.Y > 26 && tx.Y < 42 && du * du + dy * dy < 2.2f) return Col.Lerp(Col.Hex("901010"), Col.Hex("e03020"), tx.Key);
            if (MathF.Abs(tx.Y - 22) < .8f || MathF.Abs(tx.Y - 46) < .8f) v -= .2f;
            return Gloss(Brass.At(v, tx.X, tx.Y), tx, .95f, 20);
        }, Outline, Box(cx - 36, 0, cx + 36, 70), smooth: 1);
        return img;
    }

    // ---------------------------------------------------------------- Papyrus

    static Img CanopicJar()
    {
        const int w = 70, h = 104; const float cx = w / 2f; var img = new Img(w, h);
        var r = Profile(0, 17, 4, 19, 30, 26, 52, 25, 64, 21, 66, 22);
        img.Part(Lathe(cx, 0, 66, r), 18, tx =>
        {
            float v = Shade(tx, .15f, .58f) + Noise.Ridge(tx.X * 1.4f, tx.Y * .6f, 14, 101) * .1f;
            // A column of incised hieroglyphs inlaid with blue.
            float gx = tx.X - cx, gy = tx.Y;
            if (MathF.Abs(gx) < 7 && gy > 10 && gy < 58)
            {
                if (MathF.Abs(MathF.Abs(gx) - 7) < .8f) return Lapis.At(.5f, tx.X, tx.Y);
                int glyph = (int)((gy - 10) / 9); float ly = (gy - 10) % 9;
                bool mark = glyph % 3 == 0 ? MathF.Abs(ly - 4.5f) < 1 && MathF.Abs(gx) < 4.5f || MathF.Abs(gx) < 1 && ly > 2 && ly < 7 :
                    glyph % 3 == 1 ? MathF.Abs(MathF.Sqrt(gx * gx + (ly - 4.5f) * (ly - 4.5f)) - 3) < .9f : MathF.Abs(ly - 2 - MathF.Abs(gx) * .6f) < .9f && MathF.Abs(gx) < 4.5f;
                if (mark) return Lapis.At(.35f + tx.Key * .4f, tx.X, tx.Y);
            }
            return Gloss(Alabaster.At(v, tx.X, tx.Y), tx, .35f);
        }, Outline, Box(cx - 28, 0, cx + 28, 67), smooth: 2);
        // The lid: the jackal head of Duamutef in a striped headdress.
        img.Part(Poly((cx - 19, 64), (cx + 19, 64), (cx + 17, 74), (cx + 13, 84), (cx - 13, 84), (cx - 17, 74)), 7, tx =>
        {
            bool stripe = (int)((tx.Y - 64) / 3.2f) % 2 == 0;
            return Gloss((stripe ? Lapis : Gold).At(Shade(tx, .12f, .65f), tx.X, tx.Y), tx, .6f);
        }, Outline, Box(cx - 21, 62, cx + 21, 86));
        var head = Poly((cx - 11, 80), (cx + 4, 80), (cx + 20, 83), (cx + 21, 87), (cx + 10, 90), (cx + 6, 94), (cx + 7, 103), (cx + 1, 97), (cx - 3, 97), (cx - 7, 103), (cx - 10, 94), (cx - 12, 88));
        img.Part(head, 6, tx =>
        {
            float ex = tx.X - (cx + 2), ey = tx.Y - 90;
            if (ex * ex / 6 + ey * ey / 1.5f < 1) return Gold.At(.8f, tx.X, tx.Y);
            return Gloss(Jet.At(Shade(tx, .1f, .7f), tx.X, tx.Y), tx, .7f);
        }, Outline, Box(cx - 14, 78, cx + 23, 104));
        return img;
    }

    static Img PaintedChest()
    {
        const int w = 104, h = 84; var img = new Img(w, h); const float cx = w / 2f;
        foreach (float lx in new[] { 10f, 94f })
            img.Part(Rect(lx - 5, 0, lx + 5, 10, 1), 3, tx => Planks(Wood, tx, .2f, 111, 30, true), Outline, Box(lx - 7, 0, lx + 7, 12));
        img.Part(Rect(4, 8, 100, 60, 2), 6, tx =>
        {
            float v = Shade(tx, .12f, .55f) + Noise.Value(tx.X, tx.Y, 5, 113) * .05f;
            // Gold borders around a frieze of lotus and a row of painted ankh signs.
            if (tx.X < 9 || tx.X > 95 || tx.Y < 13 || tx.Y > 55) return Gloss(Gold.At(v + .1f, tx.X, tx.Y), tx, .5f);
            if (tx.Y > 40 && tx.Y < 46) return ((int)(tx.X / 6) % 2 == 0 ? Col.Hex("c8402a") : Col.Hex("e8d8a8")).Add(new Col(1, 1, 1), tx.Key * .1f);
            float ax = (tx.X - 9) % 17 - 8.5f, ay = tx.Y - 26;
            bool ankh = MathF.Abs(MathF.Sqrt(ax * ax + (ay - 6) * (ay - 6) * 1.6f) - 3.4f) < 1 || MathF.Abs(ax) < 1 && ay < 3 && ay > -9 || MathF.Abs(ay - 1) < .9f && MathF.Abs(ax) < 5;
            if (ankh) return Gold.At(.55f + tx.Key * .4f, tx.X, tx.Y);
            return Lapis.At(v + .15f, tx.X, tx.Y);
        }, Outline, Box(2, 6, 102, 62));
        // A cavetto cornice lid that flares at the top.
        img.Part((float x, float y, out float t, out float s) =>
        {
            s = (y - 58) / 24; t = (x - cx) / 52; float flare = s * s * 6;
            return y >= 58 && y <= 82 && MathF.Abs(x - cx) < 48 + flare;
        }, 7, tx =>
        {
            float band = (tx.Y - 58) % 6;
            Col c = band < 3 ? Lapis.At(Shade(tx, .2f), tx.X, tx.Y) : Gold.At(Shade(tx, .15f), tx.X, tx.Y);
            return Gloss(c, tx, .4f);
        }, Outline, Box(0, 56, 104, 84));
        img.Part(Shape.Ellipse(cx, 70, 7, 5), 4, tx => Gloss(Col.Lerp(Col.Hex("b03018"), Col.Hex("f06a3a"), tx.Key), tx, .8f), Outline, Box(cx - 9, 63, cx + 9, 77));
        return img;
    }

    // ---------------------------------------------------------------- Chinese realm

    static Img BlueWhiteVase()
    {
        const int w = 76, h = 102; const float cx = w / 2f; var img = new Img(w, h);
        var r = Profile(0, 17, 3, 18, 14, 19, 40, 26, 62, 32, 74, 28, 84, 15, 88, 9, 95, 9, 97, 11, 99, 10);
        img.Part(Lathe(cx, 0, 99, r), 22, tx =>
        {
            float u = Around(tx.T), y = tx.Y, v = Shade(tx, .25f, .55f);
            bool blue;
            if (y < 4 || y > 86 && y < 89 || y > 95) blue = y < 4 || y > 95 || true;
            // Lotus petals round the foot, a scroll of clouds on the belly and a ruyi collar on the shoulder.
            else if (y < 20) { float p = u * 9 % 1 - .5f; blue = MathF.Abs(MathF.Abs(p) * 2 - (20 - y) / 16) < .12f || y < 6; }
            else if (y > 72) { float p = u * 6 % 1 - .5f, arc = 80 - MathF.Sqrt(Math.Max(0, .25f - p * p)) * 14; blue = y > arc && y < 86 || MathF.Abs(y - 73) < .9f; }
            else
            {
                float swirl = MathF.Sin(u * MathF.PI * 8 + y * .35f) * MathF.Cos(y * .28f - u * 6);
                blue = MathF.Abs(swirl) < .18f || MathF.Abs(y - 24) < .8f || MathF.Abs(y - 68) < .8f;
            }
            var c = blue ? Cobalt.At(v - .05f + Noise.Value(tx.X, tx.Y, 3, 121) * .12f, tx.X, tx.Y) : Porcelain.At(v + .1f, tx.X, tx.Y);
            return Gloss(c, tx, .95f, 34);
        }, Outline, Box(cx - 34, 0, cx + 34, 100), smooth: 1);
        return img;
    }

    static Img LacquerBox()
    {
        var img = Chest(100, 78, 50, false, tx =>
        {
            float v = Shade(tx, .1f, .55f);
            // A gold cloud scroll painted on the lacquer.
            float dx = tx.X - 50, dy = tx.Y - 24, a = MathF.Atan2(dy, dx), rr = MathF.Sqrt(dx * dx + dy * dy);
            bool cloud = rr < 16 && MathF.Abs(rr - (6 + a * 1.4f + (a < 0 ? 8.8f : 0))) < 1 || MathF.Abs(rr - 14) < .8f && dy > -6;
            if (cloud) return Gold.At(.5f + tx.Key * .4f, tx.X, tx.Y);
            return Gloss(Lacquer.At(v, tx.X, tx.Y), tx, .8f, 30);
        }, tx => Gloss(Jet.At(Shade(tx, .15f, .6f), tx.X, tx.Y), tx, .8f, 30), tx => Metal(Gold, tx, .15f, .6f), new float[0], Brass);
        foreach (float y0 in new[] { 74f })
            img.Part(Rect(3, y0 - 2, 97, y0 + 2, 1), 2, tx => Metal(Gold, tx, .2f), Outline, Box(1, y0 - 4, 99, y0 + 4));
        return img;
    }

    // ---------------------------------------------------------------- Hell

    static Img SkullUrn()
    {
        const int w = 80, h = 96; const float cx = w / 2f; var img = new Img(w, h);
        // Bones poking out of the mouth.
        img.Part(Shape.Tube(new[] { (cx - 6, 70f), (cx - 12, 88f) }, t => 2.6f), 3, tx => Bone.At(Shade(tx, .2f), tx.X, tx.Y), Outline, Box(cx - 18, 66, cx, 94));
        img.Part(Shape.Union(Shape.Ellipse(cx - 13, 89, 3.2f, 3.2f), Shape.Ellipse(cx - 10, 91, 3, 3)), 3, tx => Bone.At(Shade(tx, .25f), tx.X, tx.Y), Outline, Box(cx - 18, 84, cx - 5, 95));
        img.Part(Shape.Tube(new[] { (cx + 4, 70f), (cx + 13, 84f) }, t => 2.4f), 3, tx => Bone.At(Shade(tx, .2f), tx.X, tx.Y), Outline, Box(cx, 66, cx + 18, 90));
        img.Part(Shape.Union(Shape.Ellipse(cx + 14, 85, 3, 3), Shape.Ellipse(cx + 11, 87, 2.8f, 2.8f)), 3, tx => Bone.At(Shade(tx, .25f), tx.X, tx.Y), Outline, Box(cx + 6, 80, cx + 19, 91));
        var r = Profile(0, 16, 4, 18, 16, 30, 40, 34, 56, 30, 66, 20, 70, 17, 73, 21, 76, 20);
        img.Part(Lathe(cx, 0, 76, r), 22, tx =>
        {
            float v = Shade(tx, .05f, .55f) + Noise.Fbm(tx.X, tx.Y, 8, 2, 131) * .08f;
            var c = Gloss(Iron.At(v, tx.X, tx.Y), tx, .45f);
            // Glowing cracks show the fire inside.
            float crack = Noise.Ridge(tx.X, tx.Y, 18, 133);
            if (crack > .9f && tx.Y < 60) return Ember.At(.5f + (crack - .9f) * 5, tx.X, tx.Y);
            return c;
        }, Outline, Box(cx - 36, 0, cx + 36, 77), smooth: 2);
        // A skull moulded on the belly.
        img.Part(Shape.Union(Shape.Ellipse(cx, 40, 11, 10), Rect(cx - 6, 26, cx + 6, 34, 2)), 6, tx =>
        {
            float dx = tx.X - cx, dy = tx.Y - 37;
            foreach (float ex in new[] { -4.6f, 4.6f }) if ((dx - ex) * (dx - ex) + dy * dy * 1.2f < 9) return Ember.At(.55f + (1 - ((dx - ex) * (dx - ex) + dy * dy) / 9) * .4f, tx.X, tx.Y);
            if (MathF.Abs(dx) < 1.6f && dy < -3 && dy > -7) return Charred[0];
            if (tx.Y < 32 && (int)(tx.X - cx + 20) % 3 == 0) return Charred[1];
            return Bone.At(Shade(tx, .1f, .6f), tx.X, tx.Y);
        }, Outline, Box(cx - 13, 24, cx + 13, 52));
        img.Light(cx, 38, 18, Col.Hex("ff7a2a"), .35f);
        return img;
    }

    static Img EmberCrate()
    {
        const int w = 102, h = 86; var img = new Img(w, h);
        img.Part(Rect(5, 0, 97, 70, 2), 6, tx =>
        {
            var c = Planks(Charred, tx, .15f, 141, 10);
            float crack = Noise.Ridge(tx.X * .5f, tx.Y * 1.6f, 14, 143);
            if (crack > .86f) return Ember.At(.35f + (crack - .86f) * 4.5f, tx.X, tx.Y);
            return c;
        }, Outline, Box(3, 0, 99, 72));
        foreach (float y0 in new[] { 6f, 58f })
            img.Part(Rect(3, y0, 99, y0 + 7, 1), 2, tx => Metal(Iron, tx, .08f, .4f), Outline, Box(1, y0 - 2, 101, y0 + 9));
        foreach (float x in new[] { 12f, 51f, 90f }) { Rivet(img, x, 9.5f, Iron); Rivet(img, x, 61.5f, Iron); }
        // Iron spikes along the lid.
        for (int k = 0; k < 6; k++)
        {
            float x = 14 + k * 14.8f;
            img.Part(Poly((x - 4, 69), (x + 4, 69), (x + .6f, 84), (x - .6f, 84)), 3, tx => Metal(Iron, tx, .15f, .7f), Outline, Box(x - 6, 67, x + 6, 86));
        }
        img.Light(51, 34, 40, Col.Hex("ff5a1a"), .25f);
        return img;
    }

    // ---------------------------------------------------------------- Abyss

    static Img EggSac()
    {
        const int w = 88, h = 88; const float cx = w / 2f; var img = new Img(w, h);
        // Root tendrils clutch the floor.
        for (int k = 0; k < 5; k++)
        {
            float x = cx - 30 + k * 15, bend = k % 2 == 0 ? 6 : -6;
            img.Part(Shape.Tube(Shape.Bezier((x, 0), (x + bend, 6), (cx + (x - cx) * .5f, 14), (cx + (x - cx) * .3f, 22)), t => 3.4f - t * 1.4f), 3,
                tx => Flesh.At(Shade(tx, .1f) + Noise.Value(tx.X, tx.Y, 3, 151 + k) * .06f, tx.X, tx.Y), Outline, Box(x - 12, 0, x + 12, 26));
        }
        img.Part(Shape.Ellipse(cx, 46, 32, 36, 0, (x, y) => Noise.Value(x, y, 9, 153) * .05f), 26, tx =>
        {
            // A translucent membrane over a dark core; coins glint where they were swallowed.
            float dx = tx.X - cx, dy = tx.Y - 42, core = MathF.Sqrt(dx * dx / 324 + dy * dy / 400);
            float veins = Noise.Ridge(tx.X, tx.Y, 16, 155);
            float v = Shade(tx, .15f, .5f) - Math.Max(0, 1 - core) * .3f;
            var c = Flesh.At(v, tx.X, tx.Y);
            if (core < .8f && Noise.Value(tx.X, tx.Y, 3, 157) > .45f) c = Col.Lerp(c, Gold.At(.6f, tx.X, tx.Y), .7f - core * .6f);
            if (veins > .82f) c = Col.Lerp(c, Blood[2], (veins - .82f) * 4);
            return Gloss(c, tx, .9f, 30);
        }, Col.Hex("12040a"), Box(cx - 34, 8, cx + 34, 84), smooth: 2);
        img.Light(cx, 42, 22, Col.Hex("ffc060"), .2f);
        return img;
    }

    static Img VoidCrystal()
    {
        const int w = 98, h = 104; var img = new Img(w, h);
        img.Part(Poly((4, 0), (94, 0), (88, 12), (70, 18), (30, 20), (10, 12)), 6, tx => Rock.At(Shade(tx, .1f) + Noise.Value(tx.X, tx.Y, 4, 161) * .08f, tx.X, tx.Y), Outline, Box(2, 0, 96, 22));
        (float x, float y, float len, float wid, float lean)[] shards = { (24, 10, 52, 9, -24), (72, 10, 46, 9, 22), (40, 12, 84, 12, -6), (58, 12, 64, 10, 10), (30, 14, 34, 7, -40), (80, 12, 28, 7, 42) };
        foreach (var (sx, sy, len, wid, lean) in shards)
        {
            float a = (90 + lean) * MathF.PI / 180, dx = MathF.Cos(a), dy = MathF.Sin(a);
            Inside prism = (float x, float y, out float t, out float s) =>
            {
                float px = x - sx, py = y - sy, along = px * dx + py * dy, across = -px * dy + py * dx;
                t = across / wid; s = along / len;
                float half = along > len - wid * 1.4f ? wid * (len - along) / (wid * 1.4f) : wid;
                return along >= 0 && along <= len && MathF.Abs(across) < half;
            };
            img.Part(prism, 4, tx =>
            {
                // Two facets meet along the crystal's spine; light runs up its edges.
                float v = tx.T < 0 ? .5f : .18f;
                v += tx.S * .2f + tx.Key * .2f;
                var c = Void.At(v, tx.X, tx.Y, .25f);
                if (MathF.Abs(tx.T) < .08f || MathF.Abs(tx.T) > .86f) c = VoidGlow.At(.3f + tx.S * .6f, tx.X, tx.Y);
                return Gloss(c, tx, .6f, 20);
            }, Col.Hex("0a0412"), Box(sx - len, sy - 2, sx + len, sy + len + 2), outlineWidth: 1);
        }
        img.Light(48, 50, 40, Col.Hex("b060ff"), .3f);
        return img;
    }

    // ---------------------------------------------------------------- loot

    static Img Heart()
    {
        const int w = 46, h = 42; var img = new Img(w, h); const float cx = w / 2f, cy = 18.5f, s = 17.5f;
        Inside heart = (float x, float y, out float t, out float ss) =>
        {
            float X = (x - cx) / s, Y = (y - cy) / s; t = X; ss = Y;
            float a = X * X + Y * Y - 1; return a * a * a - X * X * Y * Y * Y < 0;
        };
        img.Part(heart, 12, tx => Gloss(Blood.At(Shade(tx, .1f, .75f), tx.X, tx.Y), tx, 1.1f, 22), Col.Hex("2a0208"), Box(1, 0, w - 1, h - 1), outlineWidth: 1.4f, smooth: 2);
        img.Part(Shape.Ellipse(cx - 7, cy + 11, 4, 2.4f, 35), 2, tx => new Col(1, 1, 1, .75f), Col.Clear.WithA(0), Box(cx - 13, cy + 6, cx - 1, cy + 16), outlineWidth: 0);
        return img;
    }

    /// <summary>A coin with the star of Lumen struck in relief; the large one carries a teal gem and is worth five.</summary>
    static Img Coin(int size, float radius, bool large)
    {
        var img = new Img(size, size); float c = size / 2f;
        float Star(float dx, float dy)
        {
            float r = MathF.Sqrt(dx * dx + dy * dy), a = MathF.Atan2(dy, dx);
            return r - radius * (.32f + .26f * MathF.Pow(MathF.Abs(MathF.Cos(a * 2)), 6));
        }
        img.Part(Shape.Ellipse(c, c, radius, radius), radius * .45f, tx =>
        {
            float dx = tx.X - c, dy = tx.Y - c, r = MathF.Sqrt(dx * dx + dy * dy);
            float v = Shade(tx, .12f, .75f);
            if (r > radius - 2.6f) v += .12f;
            else if (r > radius - 3.6f) v -= .25f;
            if (large && r > radius - 6.5f && r < radius - 4.5f && (MathF.Atan2(dy, dx) * 12 / MathF.PI % 1 + 1) % 1 < .5f) v += .25f;
            return Gloss(Gold.At(v, tx.X, tx.Y), tx, 1, 18);
        }, Col.Hex("3a2006"), Box(0, 0, size - 1, size - 1), (x, y) => Star(x - c, y - c) < 0 ? radius * .18f : 0, outlineWidth: 1);
        if (large)
            img.Part(Shape.Ellipse(c, c, 4.2f, 4.2f), 3, tx => Gloss(Teal.At(Shade(tx, .2f, .7f), tx.X, tx.Y), tx, 1.2f, 16), Col.Hex("06302c"), Box(c - 6, c - 6, c + 6, c + 6), outlineWidth: .8f);
        return img;
    }
}
