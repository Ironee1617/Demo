namespace BackdropPainter;

/// <summary>
/// 96 texel-per-unit terrain for every theme in one atlas. Each theme owns a 384x384 column:
/// rows from the bottom are props+caps, raised platform strip (slab over underside detail),
/// buried strata and the walkable ground strip. Strips repeat every four units.
/// </summary>
public static class TerrainAtlas
{
    public const int Ppu = 96, Column = 384;
    static readonly string[] Themes = { "lava", "rock", "jungle", "frozen", "water", "cloud", "ruins" };

    sealed class Mat
    {
        public Ramp Stone, Cap; public Col Mortar, Accent;
        public float BlockW = .62f, BlockH = .3f;
    }

    static Mat Material(string id) => id switch
    {
        "lava" => new Mat { Stone = new Ramp("17111a", "221822", "30222c", "40303a", "54404a", "6c525a", "8a6a6c"), Cap = new Ramp("2a1e26", "4a3640", "6e5258", "9a7470", "c49a86"), Mortar = Col.Hex("120c12"), Accent = Col.Hex("f08a3e"), BlockW = .7f, BlockH = .34f },
        "rock" => new Mat { Stone = new Ramp("121a28", "1a2436", "243048", "313e5a", "424f6e", "586688", "7682a2"), Cap = new Ramp("2a344c", "46526e", "66728e", "8e98b2", "b6bed2"), Mortar = Col.Hex("0c1220"), Accent = Col.Hex("a68ee0"), BlockW = .74f, BlockH = .36f },
        "jungle" => new Mat { Stone = new Ramp("18201e", "222c28", "2e3a33", "3c4a40", "4e5e50", "647462", "7e8c74"), Cap = new Ramp("1c3a26", "2c5232", "42703e", "62904c", "8eb45e"), Mortar = Col.Hex("121816"), Accent = Col.Hex("d8c870"), BlockW = .6f, BlockH = .3f },
        "frozen" => new Mat { Stone = new Ramp("1a2c44", "22385a", "2c4a70", "3a5e86", "4e769c", "6a92b2", "92b2c8"), Cap = new Ramp("8a9cc4", "b4c4e0", "d4e0ee", "eaf2f8", "ffffff"), Mortar = Col.Hex("14223a"), Accent = Col.Hex("c8f0f8"), BlockW = .8f, BlockH = .4f },
        "water" => new Mat { Stone = new Ramp("231c1c", "3a2e28", "54402f", "6c5438", "886a44", "a68654", "c4a46c"), Cap = new Ramp("4a3a2c", "7a6040", "a68654", "c8aa70", "e2cc94"), Mortar = Col.Hex("1a1414"), Accent = Col.Hex("d8c48c") },
        "cloud" => new Mat { Stone = new Ramp("44445e", "585874", "70708c", "8c8aa4", "aaa6bc", "c8c2d2", "e6e0e8"), Cap = new Ramp("8c8aa4", "b0acc2", "d0cad8", "ece6ec", "fbf8f6"), Mortar = Col.Hex("3a3a52"), Accent = Col.Hex("dcbc78"), BlockW = .8f, BlockH = .32f },
        _ => new Mat { Stone = new Ramp("1e2024", "2a2c2e", "3a3a38", "4e4b44", "665f52", "807662", "9c8f74"), Cap = new Ramp("3a3832", "5e5848", "847a5e", "ab9c76", "cdbd92"), Mortar = Col.Hex("16181a"), Accent = Col.Hex("7ef0d8"), BlockW = .66f, BlockH = .33f },
    };

    public static void Paint(string path, string previewDir)
    {
        var atlas = new Canvas(Column * Themes.Length / (float)Ppu, 0, Column / (float)Ppu, Ppu);
        for (int t = 0; t < Themes.Length; t++)
        {
            string id = Themes[t]; var mat = Material(id);
            Blit(atlas, Ground(id, mat, false), t * Column, 288);
            Blit(atlas, Ground(id, mat, true), t * Column, 192);
            Blit(atlas, Raised(id, mat), t * Column, 96);
            for (int k = 0; k < 3; k++) Blit(atlas, Prop(id, mat, k), t * Column + k * 96, 0);
            var (left, right) = Caps(id, mat);
            Blit(atlas, left, t * Column + 288, 0);
            Blit(atlas, right, t * Column + 304, 0);
        }
        Png.Write(path, atlas.W, atlas.H, atlas.ToBytes());
        if (previewDir != null) PreviewSheet(atlas, Path.Combine(previewDir, "terrain-atlas.png"));
        if (previewDir != null)
            for (int t = 0; t < Themes.Length; t++)
            {
                var one = new Canvas(Column / (float)Ppu, 0, Column / (float)Ppu, Ppu);
                for (int j = 0; j < Column; j++) for (int i = 0; i < Column; i++) one.Set(i, j, atlas.Get(t * Column + i, j));
                PreviewSheet(one, Path.Combine(previewDir, "terrain-" + Themes[t] + ".png"));
            }
    }

    static void Blit(Canvas dst, Canvas src, int ox, int oy)
    {
        for (int j = 0; j < src.H; j++) for (int i = 0; i < src.W; i++) dst.Set(ox + i, oy + j, src.P[j * src.W + i]);
    }

    static void PreviewSheet(Canvas c, string path)
    {
        // 2x nearest-neighbour enlargement over a mid grey for inspection.
        int w = c.W * 2, h = c.H * 2; var px = new byte[w * h * 4];
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++)
        {
            var s = new Col(.25f, .27f, .3f).Under(c.P[(j / 2) * c.W + i / 2]); int o = (j * w + i) * 4;
            px[o] = (byte)(s.R * 255); px[o + 1] = (byte)(s.G * 255); px[o + 2] = (byte)(s.B * 255); px[o + 3] = 255;
        }
        Png.Write(path, w, h, px);
    }

    /// <summary>Bevelled masonry: lit top-left edges, shadowed bottom-right, chipped corners and cracks.</summary>
    static Col Blocks(Canvas c, Mat m, float x, float y, int i, int j, int seed, float bias = 0, float bw = 0, float bh = 0)
    {
        var f = c.F; float px = 1f / Ppu;
        bw = bw > 0 ? bw : m.BlockW; bh = bh > 0 ? bh : m.BlockH;
        bool mortar = Kit.Masonry(f, x, y, bw, bh, 1.5f * px, seed, out float sh, out float lx, out float ly, out uint id);
        if (mortar) return m.Mortar;
        float bwx = f.Snap(bw);
        float v = .38f + (sh - .5f) * .22f + bias + f.Fbm(x, y, .35f, 3, seed + 1) * .1f;
        if (ly > bh - 3.5f * px) v += .22f;           // lit top edge
        else if (lx < 3 * px) v += .12f;              // lit left edge
        else if (ly < 4.5f * px || lx > bwx - 4 * px) v -= .16f; // shadowed underside and right edge
        // Chipped corners.
        float cx = Math.Min(lx, bwx - lx), cy = Math.Min(ly, bh - ly);
        if (cx + cy < 5 * px && Field.Hash01((int)id, 3, seed) > .4f) return m.Mortar;
        if (f.Ridge(x, y, .5f, 3, seed + 3) > .94f) v -= .2f;
        return Kit.Grain(m.Stone.At(v, i, j, .4f), i, j, seed + 5, .07f, .14f);
    }

    /// <summary>The walkable ground strip (or the buried strata beneath it), 4x1 units.</summary>
    static Canvas Ground(string id, Mat m, bool buried)
    {
        var c = new Canvas(4, 0, 1, Ppu); var f = c.F; float px = 1f / Ppu;
        c.PaintAll((x, y, i, j) =>
        {
            float top = 1 - y; // distance below the walking surface
            if (id == "water")
            {
                if (buried) return Col.Clear;
                // Boardwalk: deck boards over a stringer beam, open water beneath.
                if (top > .42f) return Col.Clear;
                if (top > .3f) return Kit.Mix(m.Stone[1], m.Stone[2], M.Fract(x * 1.5f) < .1f ? 0 : .6f, i, j);
                float board = M.Mod(x, .25f);
                if (board < 1.5f * px) return m.Mortar;
                float grain = f.Fbm2(x, y, .6f, .02f, 2, 11);
                float v = .55f + grain * .18f + (Field.Hash01((int)MathF.Floor(x / .25f), 1, 13) - .5f) * .16f;
                if (top < 2 * px) v = .95f; else if (top < 4 * px) v += .12f; else if (top > .26f) v -= .25f;
                if (MathF.Abs(board - .04f) < 1.2f * px && MathF.Abs(top - .14f) < 1.6f * px) return m.Stone[0];
                return m.Stone.At(v, i, j, .35f);
            }
            if (id == "cloud" && !buried)
            {
                // Marble walkway: polished slab, gold inlay, carved meander frieze and a moulded base.
                if (top < .2f)
                {
                    if (top < 2 * px) return m.Cap.Last;
                    if (MathF.Abs(top - .1f) < 1.6f * px) return Kit.Mix(Col.Hex("8a7048"), m.Accent, .7f, i, j);
                    return Blocks(c, m, x, y, i, j, 21, .25f, 1.0f, .2f);
                }
                if (top < .58f)
                {
                    float u = M.Mod(x, .32f) / .32f, v2 = (top - .2f) / .38f;
                    bool key = (v2 < .14f || v2 > .86f) || (u < .12f && v2 < .7f) || (v2 > .3f && v2 < .44f && u > .12f && u < .75f) || (u > .62f && u < .75f && v2 > .3f);
                    var col = m.Stone.At(.62f - v2 * .1f, i, j, .3f);
                    return key ? Kit.Mix(col, m.Accent, .55f, i, j, 2) : Col.Lerp(col, Col.Black, .12f);
                }
                if (top < .64f) return m.Stone[2];
                return m.Stone.At(.5f - (top - .64f) * .6f, i, j, .3f);
            }
            var stone = Blocks(c, m, x, y, i, j, buried ? 31 : 33, buried ? -.08f : 0);
            if (buried) return stone;
            // Theme-specific walking surface.
            float n = f.N(x, 0, .3f, 41) * .5f + .5f;
            switch (id)
            {
                case "lava":
                    if (top < 2 * px) return m.Cap.Last;
                    if (top < .07f) return m.Cap.At(.75f - top * 6, i, j, .4f);
                    // Ember seams glow in the mortar near the surface.
                    if (stone.Equals(m.Mortar) && top < .5f && f.N(x, y, .4f, 43) > .1f) return Kit.Mix(Col.Hex("8a2a1c"), m.Accent, 1 - top * 2, i, j);
                    return stone;
                case "rock":
                    if (top < 2 * px) return m.Cap.Last;
                    if (top < .06f + n * .03f) return m.Cap.At(.7f - top * 5, i, j, .4f);
                    return stone;
                case "jungle":
                    {
                        float moss = .08f + n * .07f + MathF.Max(0, f.N(x, 0, .12f, 45)) * .12f;
                        if (top < moss)
                        {
                            if (top < 2 * px) return m.Cap.Last;
                            float blade = Field.Hash01(i, 0, 47);
                            return m.Cap.At(.75f - top / moss * .5f + (blade > .8f ? .2f : 0), i, j, .45f);
                        }
                        if (top < moss + 2 * px) return m.Cap[0];
                        return stone;
                    }
                case "frozen":
                    {
                        float snow = .07f + n * .06f + MathF.Max(0, f.N(x, 0, .2f, 49)) * .05f;
                        if (top < snow) return top < 2 * px ? m.Cap.Last : m.Cap.At(.9f - top / snow * .6f, i, j, .4f);
                        if (top < snow + 2 * px) return m.Cap[0];
                        // Blue ice blocks under the snow with an inner sheen.
                        var ice = Kit.Ice(f, x, y, i, j, new V3(0, 0, 1), m.Stone, .35f, 51, .32f);
                        return stone.Equals(m.Mortar) ? m.Mortar : ice;
                    }
                default: // ruins
                    {
                        if (top < 2 * px) return m.Cap.Last;
                        if (top < .06f) return m.Cap.At(.7f - top * 5, i, j, .4f);
                        float sand = f.N(x, 0, .5f, 53);
                        if (sand > .25f && top < .06f + (sand - .25f) * .3f) return m.Cap.At(.85f, i, j, .5f);
                        return stone;
                    }
            }
        });
        return c;
    }

    /// <summary>A platform slab (upper half) and its theme underside (lower half), 4x1 units.</summary>
    static Canvas Raised(string id, Mat m)
    {
        var c = new Canvas(4, 0, 1, Ppu); var f = c.F; float px = 1f / Ppu;
        // Slab body: the top half of the strip.
        c.Paint(0, 4, .5f, 1, (x, y, i, j) =>
        {
            float top = 1 - y;
            if (id == "water")
            {
                if (top > .3f) return Col.Clear;
                float board = M.Mod(x, .25f);
                if (board < 1.5f * px) return m.Mortar;
                float v = .55f + f.Fbm2(x, y, .6f, .02f, 2, 61) * .18f + (Field.Hash01((int)MathF.Floor(x / .25f), 2, 63) - .5f) * .16f;
                if (top < 2 * px) v = .95f; else if (top > .24f) v -= .28f;
                return m.Stone.At(v, i, j, .35f);
            }
            if (id == "cloud")
            {
                if (top < 2 * px) return m.Cap.Last;
                if (MathF.Abs(top - .1f) < 1.6f * px) return Kit.Mix(Col.Hex("8a7048"), m.Accent, .7f, i, j);
                if (top > .46f) return m.Stone[1];
                return Blocks(c, m, x, y, i, j, 65, .2f, .9f, .24f);
            }
            if (top > .47f) return m.Mortar;
            var stone = Blocks(c, m, x, y, i, j, 67, 0, m.BlockW * .8f, .22f);
            if (top < 2 * px) return m.Cap.Last;
            if (id == "jungle" && top < .07f + f.N(x, 0, .2f, 69) * .03f) return m.Cap.At(.7f - top * 4, i, j, .4f);
            if (id == "frozen" && top < .08f + f.N(x, 0, .2f, 71) * .03f) return m.Cap.At(.85f - top * 4, i, j, .4f);
            if (top < .05f) return m.Cap.At(.7f - top * 6, i, j, .4f);
            if (id == "lava" && stone.Equals(m.Mortar) && f.N(x, y, .35f, 73) > .15f) return Kit.Mix(Col.Hex("8a2a1c"), m.Accent, .6f, i, j);
            return stone;
        });
        // Underside detail in the lower half.
        var under = c.Blank();
        switch (id)
        {
            case "lava":
            case "rock":
                {
                    var rng = new Rng(id == "lava" ? 75 : 77);
                    for (float x = 0; x < 4; x += rng.R(.25f, .6f))
                    {
                        float bx = x, len = rng.R(.12f, .42f), w = rng.R(.16f, .34f);
                        under.Paint(bx - w, bx + w, .5f - len - .02f, .51f, (px2, py, i, j) =>
                            Kit.Spike(f, px2, py, bx, .5f, len, w, -1, 79, out float u, out _, .25f, 1.1f) ? Col.White : Col.Clear);
                    }
                    Kit.Inflate(under, 8, (i, j, src, h, n) =>
                    {
                        float v = .15f + Kit.Lambert(n.X, n.Y, n.Z) * .5f;
                        var col = m.Stone.At(v, i, j, .4f);
                        return id == "lava" && n.Y < -.4f ? Kit.Mix(col, Col.Hex("c0502c"), -n.Y - .3f, i, j, 3) : col;
                    });
                    if (id == "rock")
                        foreach (var gx in new[] { .7f, 2.4f, 3.5f })
                            Kit.CrystalCluster(under, gx, .48f, .22f, 0, .5f, 3, new Ramp("1c1438", "35246a", "53399a", "7458c4", "9a7ee2", "c4acf6", "f1e8ff"), (int)(gx * 91), -1);
                    break;
                }
            case "jungle":
                {
                    var rng = new Rng(81);
                    var leaves = new Ramp("12261e", "1c3826", "2a4e30", "3e663a", "568048", "74a058");
                    for (int k = 0; k < 9; k++) Kit.Vine(under, rng.R(0, 4), .5f, rng.R(.15f, .48f), Col.Hex("2c3a26"), leaves, 83 + k, .04f, 1.4f);
                    // Root tangles hugging the slab underside.
                    under.Paint(0, 4, .38f, .5f, (x, y, i, j) =>
                    {
                        float r = f.Ridge(x, y * 3, .3f, 2, 85);
                        return r > .86f && y > .5f - (f.N(x, 0, .3f, 87) * .5f + .5f) * .12f ? Col.Lerp(Col.Hex("3a3024"), Col.Hex("5a4a34"), r - .86f) : Col.Clear;
                    });
                    break;
                }
            case "frozen":
                {
                    var rng = new Rng(89);
                    for (float x = .05f; x < 4; x += rng.R(.12f, .3f))
                    {
                        float bx = x, len = rng.R(.08f, .46f), w = rng.R(.05f, .12f);
                        under.Paint(bx - w, bx + w, .5f - len - .02f, .51f, (px2, py, i, j) =>
                        {
                            if (!Kit.Spike(f, px2, py, bx, .5f, len, w, -1, 91, out float u, out float t, .05f, 1.3f)) return Col.Clear;
                            return new Ramp("2c4a70", "4e769c", "8cb4cc", "c8e4ee", "f4fcff").At(.5f - u * .4f + (MathF.Abs(u + .3f) < .2f ? .3f : 0) - t * .1f, i, j, .3f);
                        });
                    }
                    break;
                }
            case "water":
                {
                    // A cross beam with rope lashings under the deck.
                    under.Paint(0, 4, .58f, .72f, (x, y, i, j) =>
                    {
                        float v = .35f + f.Fbm2(x, y, .5f, .02f, 2, 93) * .15f + (y > .7f ? .15f : y < .6f ? -.15f : 0);
                        bool rope = M.Mod(x, 1) > .02f && M.Mod(x, 1) < .1f;
                        if (rope) return (int)(y * Ppu + x * Ppu) % 4 < 2 ? Col.Hex("d8c48c") : Col.Hex("9a8458");
                        return m.Stone.At(v, i, j, .35f);
                    });
                    break;
                }
            case "cloud":
                // Cloud puffs sit just under the slab rather than hanging free.
                Kit.CloudBank(under, 0, 4, .1f, .16f, .07f, .25f, 26, new Ramp("5e6296", "7476a8", "8e8eb8", "aaa8c8", "c6c0d8", "ddd6e4", "efe9ee"), 95, .12f);
                break;
            default:
                {
                    // Corbels under the slab and dangling roots.
                    under.Paint(0, 4, .3f, .5f, (x, y, i, j) =>
                    {
                        float u = M.Mod(x, 1) - .5f, h = .5f - y;
                        if (MathF.Abs(u) < .16f - h * .3f && h < .18f) return m.Stone.At(.45f - u * .8f + (h > .15f ? -.15f : 0), i, j, .35f);
                        return Col.Clear;
                    });
                    var rng = new Rng(97);
                    for (int k = 0; k < 5; k++) Kit.Vine(under, rng.R(0, 4), .5f, rng.R(.12f, .4f), Col.Hex("2c3a2c"), new Ramp("1a2620", "24342a", "324434", "44583e", "5c704a"), 99 + k, .03f, 1.2f);
                    break;
                }
        }
        // The underside sits behind the slab.
        under.Over(c); c = under;
        return c;
    }

    /// <summary>Darkened ends that close each platform's cut strip.</summary>
    static (Canvas left, Canvas right) Caps(string id, Mat m)
    {
        var left = new Canvas(16f / Ppu * 1, 0, 1, Ppu); var right = new Canvas(16f / Ppu, 0, 1, Ppu);
        float px = 1f / Ppu;
        foreach (var (c, dir) in new[] { (left, -1), (right, 1) })
            c.PaintAll((x, y, i, j) =>
            {
                float top = 1 - y; if (top > (id == "water" ? .3f : .48f) || y < .5f) return Col.Clear;
                int edge = dir < 0 ? i : 15 - i;
                if (edge < 2) return id == "cloud" ? m.Stone[2] : m.Mortar;
                if (edge < 4) return Col.Black.WithA(.35f);
                if (top < 2 * px && edge < 6) return m.Cap.Last.WithA(.6f);
                return Col.Clear;
            });
        return (left, right);
    }

    /// <summary>Small ground props, 1x1 unit with a bottom-centre pivot.</summary>
    static Canvas Prop(string id, Mat m, int k)
    {
        var c = new Canvas(1, 0, 1, Ppu); var f = c.F; var rng = new Rng(Array.IndexOf(Themes, id) * 7919 + k * 977);
        void Rubble(int count, Ramp ramp, float maxH)
        {
            var stones = c.Blank();
            for (int s = 0; s < count; s++)
            {
                float sx = .5f + rng.R(-.3f, .3f), w = rng.R(.08f, .2f), h = rng.R(.05f, maxH);
                stones.Paint(sx - w, sx + w, 0, h, (x, y, i, j) =>
                {
                    float dx = (x - sx) / w, dy = y / h;
                    return dx * dx + dy * dy * .8f < 1 && y < h * (1 - MathF.Abs(dx) * .3f) ? Col.White : Col.Clear;
                });
            }
            Kit.Inflate(stones, 6, (i, j, src, hh, n) => ramp.At(.15f + Kit.Lambert(n.X, n.Y, n.Z) * .7f, i, j, .35f), (i, j) => f.N(c.X(i), c.Y(j), .1f, 7) * 2);
            c.Over(stones);
        }
        void Tuft(Ramp ramp, float h, int blades, Col? flower = null)
        {
            for (int b = 0; b < blades; b++)
            {
                float bx = .5f + rng.R(-.25f, .25f), lean = rng.R(-.5f, .5f), len = h * rng.R(.5f, 1);
                c.Paint(bx - .3f, bx + .3f, 0, len, (x, y, i, j) =>
                {
                    float t = y / len, cx = bx + lean * t * t * len, w = .022f * (1 - t) + .004f;
                    return MathF.Abs(x - cx) < w ? ramp.At(.3f + t * .6f + (x < cx ? .1f : 0), i, j, .35f) : Col.Clear;
                });
            }
            if (flower.HasValue)
                foreach (var (fx, fy) in new[] { (.4f, h * .7f), (.62f, h * .85f) })
                    c.Paint(fx - .05f, fx + .05f, fy - .05f, fy + .05f, (x, y, i, j) => (x - fx) * (x - fx) + (y - fy) * (y - fy) < .0018f ? ((x - fx) * (x - fx) + (y - fy) * (y - fy) < .0003f ? Col.Hex("fff0b0") : flower.Value) : Col.Clear);
        }
        var crystalV = new Ramp("1c1438", "35246a", "53399a", "7458c4", "9a7ee2", "c4acf6", "f1e8ff");
        var crystalC = new Ramp("0f2a36", "1a4c5c", "277684", "3ea2ac", "6ccccc", "aef0e8", "effffb");
        var ice = new Ramp("1a3050", "2a5478", "3e7ca0", "64a8c6", "9ad2e2", "d0eef4", "ffffff");
        var green = new Ramp("12261e", "1c3826", "2a4e30", "3e663a", "568048", "74a058", "9ac070");
        switch (id + k)
        {
            case "lava0": Rubble(6, m.Stone, .2f); break;
            case "lava1":
                Rubble(3, m.Stone, .3f);
                c.Post((i, j, col) => f.Ridge(c.X(i), c.Y(j), .2f, 2, 9) > .88f ? Kit.Mix(Col.Hex("c0441e"), m.Accent, .6f, i, j) : col);
                break;
            case "lava2": Kit.CrystalCluster(c, .5f, .02f, .34f, 0, .55f, 5, new Ramp("0c0a10", "1a1420", "2a2232", "3e3448", "584a62", "7a6a86", "b0a0c0"), 11); break;
            case "rock0": Rubble(5, m.Stone, .22f); break;
            case "rock1": Kit.CrystalCluster(c, .5f, .02f, .5f, 0, .6f, 6, crystalV, 13); break;
            case "rock2": Kit.CrystalCluster(c, .5f, .02f, .42f, .1f, .55f, 5, crystalC, 15); break;
            case "jungle0": Kit.LeafClump(c, .5f, .18f, .36f, .16f, 90, green, 17, .16f, -.6f); break;
            case "jungle1": Tuft(green, .38f, 14, Col.Hex("d87a92")); break;
            case "jungle2":
                foreach (var (mx, mh, r) in new[] { (.38f, .16f, .09f), (.55f, .24f, .12f), (.68f, .12f, .07f) })
                    c.Paint(mx - r, mx + r, 0, mh + r, (x, y, i, j) =>
                    {
                        float dx = x - mx;
                        if (y < mh && MathF.Abs(dx) < .025f) return Col.Hex("d8d0b0");
                        float cy = mh, d = (dx * dx) / (r * r) + (y - cy) * (y - cy) / (r * r * .4f);
                        if (y >= cy - .01f && d < 1) return Kit.Mix(Col.Hex("8a3a2a"), Col.Hex("e0704a"), .7f - dx / r * .5f, i, j, 3);
                        return Col.Clear;
                    });
                break;
            case "frozen0": Kit.CrystalCluster(c, .5f, .02f, .44f, 0, .55f, 5, ice, 19); break;
            case "frozen1":
                Rubble(4, new Ramp("1e2a3a", "2c3c50", "40546c", "5a708a", "7a90a8"), .2f);
                c.Post((i, j, col) => c.Depth(i, j, 0, 1, 4) < 4 ? Col.Hex(c.Depth(i, j, 0, 1, 4) < 1 ? "ffffff" : "d4e0ee") : col);
                break;
            case "frozen2": Kit.Pine(c, .5f, 0, .9f, new Ramp("0e1c26", "15283a", "1d3648", "284658", "355868"), Col.Hex("eaf2f8"), Col.Hex("b4c4e0"), 21); break;
            case "water0": Tuft(new Ramp("1c3a30", "2a5240", "3e6e4c", "5a8c5a", "86ac6a"), .45f, 12); break;
            case "water1":
                Tuft(new Ramp("1c3a30", "2a5240", "3e6e4c", "5a8c5a", "86ac6a"), .3f, 8, Col.Hex("f0a0b4"));
                break;
            case "water2":
                // A coiled rope on the deck.
                c.Paint(.2f, .8f, 0, .2f, (x, y, i, j) =>
                {
                    float dx = (x - .5f) / .26f, dy = (y - .07f) / .07f, d = dx * dx + dy * dy;
                    if (d > 1) return Col.Clear;
                    float ring = M.Fract(MathF.Sqrt(d) * 3.5f);
                    return ring < .25f ? Col.Hex("7a6440") : (i + j) % 4 < 2 ? Col.Hex("d8c48c") : Col.Hex("b49a64");
                });
                break;
            case "cloud0": Rubble(4, m.Stone, .2f); break;
            case "cloud1":
                // A broken column stub with a gold band.
                c.Paint(.3f, .7f, 0, .5f, (x, y, i, j) =>
                {
                    float dx = x - .5f, top = .42f - dx * .5f + f.N(x, 0, .05f, 23) * .04f;
                    if (y > top) return Col.Clear;
                    if (y < .06f) return MathF.Abs(dx) < .17f ? m.Stone.At(.7f - dx, i, j, .3f) : Col.Clear;
                    if (MathF.Abs(dx) > .12f) return Col.Clear;
                    if (MathF.Abs(y - .2f) < .02f) return Kit.Mix(Col.Hex("8a7048"), m.Accent, .7f - dx * 3, i, j);
                    return m.Stone.At(Kit.Cylinder(dx / .12f, .2f) * .85f + MathF.Cos(dx / .12f * 9) * .05f, i, j, .3f);
                });
                break;
            case "cloud2":
                // A gilded urn.
                c.Paint(.32f, .68f, 0, .5f, (x, y, i, j) =>
                {
                    float dx = x - .5f, t = y / .44f, hw = .06f + MathF.Sin(MathF.PI * MathF.Min(1, t * 1.1f)) * .1f + (t > .85f ? .03f : 0);
                    if (t > 1 || MathF.Abs(dx) > hw) return Col.Clear;
                    return Kit.Mix(Col.Hex("6a5434"), m.Accent, Kit.Cylinder(dx / hw, .1f), i, j, 4);
                });
                break;
            case "ruins0": Rubble(5, m.Stone, .24f); break;
            case "ruins1":
                // A fallen carved block.
                c.Paint(.18f, .82f, 0, .3f, (x, y, i, j) =>
                {
                    if (y > .26f) return Col.Clear;
                    float v = .45f + (y > .22f ? .25f : 0) + (x < .22f ? .1f : 0);
                    float gx = M.Mod(x, .12f), gy = y - .06f;
                    if (gx > .03f && gx < .09f && gy > 0 && gy < .12f && Field.Hash01((int)(x / .12f), 1, 25) > .4f) v -= .2f;
                    return m.Stone.At(v, i, j, .35f);
                });
                break;
            default: Tuft(new Ramp("2a2c1e", "3e4228", "56583a", "72744a", "908e5e"), .32f, 10); break;
        }
        return c;
    }
}
