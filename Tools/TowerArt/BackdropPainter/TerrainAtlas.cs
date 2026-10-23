namespace BackdropPainter;

/// <summary>
/// 96 texel-per-unit terrain for the seven mythology maps in one atlas, columns in TowerThemeId order.
/// Each theme owns a 384x384 column: rows from the bottom are props+caps, the raised platform strip
/// (slab over underside detail), buried strata and the walkable ground strip. Strips repeat every four units.
/// </summary>
public static class TerrainAtlas
{
    public const int Ppu = 96, Column = 384;
    static readonly string[] Themes = { "olympus", "asgard", "dangun", "eden", "veda", "papyrus", "chinese" };

    sealed class Mat
    {
        public Ramp Stone, Cap; public Col Mortar, Accent, Trim;
        public float BlockW = .62f, BlockH = .3f;
    }

    static Mat Material(string id) => id switch
    {
        // Olympus: cream marble with gold inlay.
        "olympus" => new Mat { Stone = new Ramp("5c5868", "7a7684", "9a96a2", "b8b4bc", "d2cfd4", "e6e3e4", "f6f3f0"), Cap = new Ramp("b8b0a8", "d4ccc4", "e8e2dc", "f6f2ec", "fffdf8"), Mortar = Col.Hex("4a4654"), Accent = Col.Hex("e2b64e"), Trim = Col.Hex("8a7048"), BlockW = .9f, BlockH = .26f },
        // Asgard: steel-blue runestone under snow, cyan rune glow.
        "asgard" => new Mat { Stone = new Ramp("121a2c", "1b2640", "26344f", "344664", "485c7c", "627696", "8296b2"), Cap = new Ramp("8ea2c0", "b4c6dc", "d2e2ee", "e8f2f8", "ffffff"), Mortar = Col.Hex("0c1222"), Accent = Col.Hex("7ef0ff"), Trim = Col.Hex("2a8aa0"), BlockW = .78f, BlockH = .34f },
        // Dangun: grey granite under moss and earth, obangsaek red and blue.
        "dangun" => new Mat { Stone = new Ramp("23241f", "34352e", "47483f", "5c5d52", "727365", "8b8b7a", "a6a593"), Cap = new Ramp("2e3c22", "445430", "5c7040", "7a8e50", "9aa862"), Mortar = Col.Hex("1a1b16"), Accent = Col.Hex("d9462e"), Trim = Col.Hex("2a4aa0"), BlockW = .7f, BlockH = .32f },
        // Eden: rich earth and river stones under vivid grass.
        "eden" => new Mat { Stone = new Ramp("2a2418", "3c3220", "51432b", "665636", "7b6a44", "958458", "b09f6c"), Cap = new Ramp("2c6a2e", "3c8a38", "56a844", "7cc455", "a6dc6a"), Mortar = Col.Hex("1c180e"), Accent = Col.Hex("f6d868"), Trim = Col.Hex("e06a8a"), BlockW = .6f, BlockH = .3f },
        // Veda: red sandstone ghats, marigold and gold.
        "veda" => new Mat { Stone = new Ramp("3a1e14", "54301e", "6e4428", "8a5a34", "a67242", "be8a52", "d4a468"), Cap = new Ramp("8a5a34", "b07a46", "cf9a58", "e4b86c", "f4d488"), Mortar = Col.Hex("2a140c"), Accent = Col.Hex("ff9a2e"), Trim = Col.Hex("ffd040"), BlockW = .74f, BlockH = .3f },
        // Papyrus: golden sandstone with lapis and turquoise paint.
        "papyrus" => new Mat { Stone = new Ramp("4a3a20", "6a5430", "8a6e3e", "a8884c", "c4a45e", "dcbe74", "f0d88e"), Cap = new Ramp("a8884c", "c8a860", "e0c478", "f0dc98", "fcf0c0"), Mortar = Col.Hex("3a2c16"), Accent = Col.Hex("2a5ac8"), Trim = Col.Hex("2aa89a"), BlockW = .9f, BlockH = .3f },
        // Chinese realm: grey-green jade stone with vermilion lacquer and gold.
        _ => new Mat { Stone = new Ramp("1e2a28", "2a3a36", "3a4c46", "4c6058", "627668", "7a8e7c", "98aa94"), Cap = new Ramp("2e5a48", "3e7458", "549068", "72aa7c", "98c494"), Mortar = Col.Hex("141c1a"), Accent = Col.Hex("d8302a"), Trim = Col.Hex("e6c250"), BlockW = .8f, BlockH = .3f },
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

    static float SegDist(float px, float py, float ax, float ay, float bx, float by)
    {
        float vx = bx - ax, vy = by - ay, wx = px - ax, wy = py - ay, l2 = vx * vx + vy * vy;
        float t = l2 > 0 ? Math.Clamp((wx * vx + wy * vy) / l2, 0, 1) : 0;
        float dx = wx - vx * t, dy = wy - vy * t;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    // Elder-futhark style strokes in block-normalised coordinates (u right, v up), four floats per segment.
    static readonly float[][] Runes =
    {
        new[] { .5f, .2f, .5f, .8f },
        new[] { .38f, .2f, .62f, .5f, .62f, .5f, .38f, .8f },
        new[] { .5f, .2f, .5f, .8f, .5f, .8f, .34f, .64f, .5f, .8f, .66f, .64f },
        new[] { .5f, .2f, .5f, .8f, .34f, .62f, .66f, .38f },
        new[] { .5f, .2f, .5f, .8f, .5f, .52f, .34f, .76f, .5f, .52f, .66f, .76f },
        new[] { .4f, .2f, .4f, .8f, .4f, .8f, .64f, .66f, .4f, .62f, .64f, .48f },
    };

    /// <summary>Texel distance from (u, v) to the nearest stroke of a rune drawn in a sx by sy texel cell.</summary>
    static float RuneDist(int kind, float u, float v, float sx, float sy)
    {
        var g = Runes[M.Mod(kind, Runes.Length)]; float best = 1e9f;
        for (int k = 0; k + 3 < g.Length; k += 4) best = Math.Min(best, SegDist(u * sx, v * sy, g[k] * sx, g[k + 1] * sy, g[k + 2] * sx, g[k + 3] * sy));
        return best;
    }

    /// <summary>Masonry where some blocks carry a glowing carved rune.</summary>
    static Col RuneStone(Mat m, Canvas c, float x, float y, int i, int j, int seed, float bias, float bw, float bh, float chance)
    {
        var stone = Blocks(c, m, x, y, i, j, seed, bias, bw, bh);
        if (stone.Equals(m.Mortar)) return stone;
        var f = c.F; float px = 1f / Ppu;
        Kit.Masonry(f, x, y, bw, bh, 1.5f * px, seed, out _, out float lx, out float ly, out uint id);
        if (Field.Hash01((int)(id & 0x7fffffff), 9, seed) > chance) return stone;
        float bws = f.Snap(bw);
        float d = RuneDist((int)(id % 6), lx / bws, ly / bh, bws * Ppu, bh * Ppu);
        if (d < 1.3f) return Kit.Mix(m.Accent, Col.White, .25f, i, j, 3);
        if (d < 3.2f) return Kit.Mix(stone, m.Accent, .55f - d * .1f, i, j, 3);
        return stone;
    }

    /// <summary>Polished marble walkway: slab with gold inlay, meander frieze and a moulded base.</summary>
    static Col Marble(Mat m, Canvas c, float x, float y, int i, int j, float top, int seed)
    {
        float px = 1f / Ppu;
        if (top < .2f)
        {
            if (top < 2 * px) return m.Cap.Last;
            if (MathF.Abs(top - .1f) < 1.6f * px) return Kit.Mix(m.Trim, m.Accent, .7f, i, j);
            return Blocks(c, m, x, y, i, j, seed, .25f, 1.0f, .2f);
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

    /// <summary>Packed earth with embedded river stones.</summary>
    static Col Earth(Mat m, Canvas c, float x, float y, int i, int j, int seed)
    {
        var f = c.F;
        var cell = f.Cell(x, y, .2f, seed, .7f);
        if (cell.F2 - cell.F1 < .035f) return Kit.Grain(m.Mortar, i, j, seed + 2, .1f, .12f);
        float v = .3f + Field.Hash01((int)(cell.Id & 0x7fffffff), 1, seed) * .35f - cell.F1 * .9f + f.Fbm(x, y, .3f, 2, seed + 1) * .08f;
        return Kit.Grain(m.Stone.At(v, i, j, .4f), i, j, seed + 5, .08f, .14f);
    }

    /// <summary>Small meadow flowers scattered through the upper grass; clear elsewhere.</summary>
    static Col Flower(float x, float top, float grass, int seed)
    {
        int cell = M.Mod((int)MathF.Floor(x / .125f), 32);
        if (Field.Hash01(cell, 0, seed) > .4f) return Col.Clear;
        float fx = (MathF.Floor(x / .125f) + .5f) * .125f + (Field.Hash01(cell, 1, seed) - .5f) * .06f;
        float fy = .015f + Field.Hash01(cell, 2, seed) * grass * .45f, dx = x - fx, dy = top - fy, d = dx * dx + dy * dy;
        if (d > .0008f) return Col.Clear;
        if (d < .00015f) return Col.Hex("fff4b0");
        float pick = Field.Hash01(cell, 3, seed);
        return pick < .3f ? Col.Hex("f07aa0") : pick < .55f ? Col.Hex("f8f4ff") : pick < .8f ? Col.Hex("f8d848") : Col.Hex("6a8af0");
    }

    /// <summary>Marigold petals strewn on a ghat step; clear elsewhere.</summary>
    static Col Petal(float x, float top, int seed)
    {
        int cell = M.Mod((int)MathF.Floor(x / .125f), 32);
        if (Field.Hash01(cell, 0, seed) > .3f) return Col.Clear;
        float fx = (MathF.Floor(x / .125f) + .5f) * .125f + (Field.Hash01(cell, 1, seed) - .5f) * .06f;
        float fy = .012f + Field.Hash01(cell, 2, seed) * .02f, dx = x - fx, dy = top - fy;
        return dx * dx + dy * dy * 2 < .0004f ? (Field.Hash01(cell, 3, seed) > .5f ? Col.Hex("ff9a2e") : Col.Hex("ffc83c")) : Col.Clear;
    }

    /// <summary>A carved lotus-petal frieze; v runs 0 at the top of the band to 1 at its foot.</summary>
    static Col LotusFrieze(Mat m, float x, float v, int i, int j)
    {
        // Pointed tip, bellied sides: the petal widens quickly then rounds off toward its base.
        float u = M.Mod(x, .5f) / .5f - .5f, pw = .03f + .4f * MathF.Sin(MathF.Min(1, v * 1.15f) * MathF.PI * .5f), a = MathF.Abs(u);
        if (a > pw) return m.Stone.At(.28f + v * .08f, i, j, .3f);
        float t = .6f - v * .2f + (u < 0 ? .1f : -.06f);
        if (a > pw - .05f) t -= .22f; else if (a < .025f && v > .15f) t += .12f;
        return m.Cap.At(t, i, j, .35f);
    }

    /// <summary>A painted hieroglyph band: one sign per quarter unit in lapis, turquoise, terracotta or black.</summary>
    static Col Hieroglyphs(Mat m, float x, float v, int i, int j, int seed)
    {
        var bg = m.Cap.At(.62f + v * .12f, i, j, .3f);
        int cell = (int)MathF.Floor(x / .25f); float u = (x - cell * .25f) / .25f;
        cell = M.Mod(cell, 16);
        uint h = Field.Hash(cell, 0, seed);
        Col ink = (int)((h >> 8) % 4) switch { 0 => m.Accent, 1 => m.Trim, 2 => Col.Hex("c85a2c"), _ => Col.Hex("1e1812") };
        float du = u - .5f, dv = v - .5f; bool hit;
        switch ((int)(h % 6))
        {
            case 0: { float r = MathF.Sqrt(du * du + (v - .3f) * (v - .3f)); hit = (r > .09f && r < .17f) || (MathF.Abs(du) < .05f && v > .4f && v < .88f) || (MathF.Abs(v - .48f) < .045f && MathF.Abs(du) < .22f); break; }
            case 1: { float e = du * du / .09f + dv * dv / .025f; hit = (e > .55f && e < 1) || du * du + dv * dv < .008f; break; }
            case 2: { float tri = MathF.Abs(M.Fract(u * 3) * 2 - 1); hit = MathF.Abs(dv - (tri - .5f) * .3f) < .07f && u > .1f && u < .9f; break; }
            case 3: hit = (MathF.Abs(du) < .08f && v > .18f && v < .82f) || (v > .72f && v < .86f && MathF.Abs(du) < .16f); break;
            case 4: hit = v > .25f && v < .85f && MathF.Abs(du) < (v - .25f) * .55f; break;
            default: { float r2 = du * du + dv * dv; hit = r2 < .05f && (r2 > .03f || r2 < .015f); break; }
        }
        return hit ? Kit.Mix(ink, bg, .1f, i, j, 2) : bg;
    }

    /// <summary>A vermilion lacquer band with a gold centre line between top = a and top = b.</summary>
    static Col Lacquer(Mat m, float top, float a, float b, int i, int j)
    {
        float t = (top - a) / (b - a);
        if (MathF.Abs(t - .5f) < .14f) return Kit.Mix(Col.Hex("a07828"), m.Trim, .75f + (t < .5f ? .2f : 0), i, j, 3);
        var red = t < .14f ? Col.Lerp(m.Accent, Col.White, .18f) : t > .86f ? Col.Lerp(m.Accent, Col.Black, .3f) : m.Accent;
        return Kit.Grain(red, i, j, 59, .05f, .08f);
    }

    static Col Buried(string id, Mat m, Canvas c, float x, float y, int i, int j)
    {
        switch (id)
        {
            case "olympus": return Blocks(c, m, x, y, i, j, 31, -.06f, 1.1f, .3f);
            case "eden":
                {
                    var earth = Earth(m, c, x, y, i, j, 31);
                    float r = c.F.Ridge(x, y * 2.5f, .3f, 2, 35);
                    return r > .9f ? Col.Lerp(Col.Hex("3a2a18"), Col.Hex("5a4428"), (r - .9f) * 10) : earth;
                }
            case "asgard": return RuneStone(m, c, x, y, i, j, 31, -.08f, m.BlockW, m.BlockH, .2f);
            default: return Blocks(c, m, x, y, i, j, 31, -.08f);
        }
    }

    /// <summary>The walkable ground strip (or the buried strata beneath it), 4x1 units.</summary>
    static Canvas Ground(string id, Mat m, bool buried)
    {
        var c = new Canvas(4, 0, 1, Ppu); var f = c.F; float px = 1f / Ppu;
        c.PaintAll((x, y, i, j) =>
        {
            float top = 1 - y; // distance below the walking surface
            if (buried) return Buried(id, m, c, x, y, i, j);
            float n = f.N(x, 0, .3f, 41) * .5f + .5f;
            switch (id)
            {
                case "olympus": return Marble(m, c, x, y, i, j, top, 21);
                case "asgard":
                    {
                        float snow = .07f + n * .06f + MathF.Max(0, f.N(x, 0, .2f, 49)) * .05f;
                        if (top < snow) return top < 2 * px ? m.Cap.Last : m.Cap.At(.9f - top / snow * .6f, i, j, .4f);
                        if (top < snow + 2 * px) return m.Cap[0];
                        return RuneStone(m, c, x, y, i, j, 33, 0, m.BlockW, m.BlockH, .55f);
                    }
                case "dangun":
                    {
                        float moss = .07f + n * .06f + MathF.Max(0, f.N(x, 0, .12f, 45)) * .1f;
                        if (top < moss)
                        {
                            if (top < 2 * px) return m.Cap.Last;
                            float blade = Field.Hash01(i, 0, 47);
                            return m.Cap.At(.7f - top / moss * .5f + (blade > .82f ? .25f : 0), i, j, .45f);
                        }
                        if (top < moss + 2 * px) return m.Cap[0];
                        return Blocks(c, m, x, y, i, j, 33);
                    }
                case "eden":
                    {
                        float grass = .11f + n * .05f + MathF.Max(0, f.N(x, 0, .15f, 45)) * .06f;
                        if (top < grass)
                        {
                            if (top < 2 * px) return m.Cap.Last;
                            var flower = Flower(x, top, grass, 51);
                            if (flower.A > 0) return flower;
                            float blade = Field.Hash01(i, 0, 47);
                            return m.Cap.At(.8f - top / grass * .55f + (blade > .8f ? .2f : 0), i, j, .45f);
                        }
                        if (top < grass + 2 * px) return m.Cap[0];
                        return Earth(m, c, x, y, i, j, 33);
                    }
                case "veda":
                    {
                        if (top < 2 * px) return m.Cap.Last;
                        if (top < .06f) { var petal = Petal(x, top, 53); return petal.A > 0 ? petal : m.Cap.At(.75f - top * 5, i, j, .4f); }
                        if (top < .08f) return m.Mortar;
                        if (top < .28f) return LotusFrieze(m, x, (top - .08f) / .2f, i, j);
                        if (top < .3f) return m.Mortar;
                        return Blocks(c, m, x, y, i, j, 33);
                    }
                case "papyrus":
                    {
                        if (top < 2 * px) return m.Cap.Last;
                        float sand = f.N(x, 0, .5f, 53);
                        if (top < .05f + MathF.Max(0, sand) * .04f) return m.Cap.At(.85f - top * 4, i, j, .5f);
                        if (top < .075f) return m.Mortar;
                        if (top < .095f) return m.Accent;
                        if (top < .31f) return Hieroglyphs(m, x, (top - .095f) / .215f, i, j, 55);
                        if (top < .33f) return m.Accent;
                        if (top < .35f) return m.Mortar;
                        return Blocks(c, m, x, y, i, j, 33);
                    }
                default: // chinese
                    {
                        if (top < 2 * px) return m.Cap.Last;
                        if (top < .05f + n * .02f) return m.Cap.At(.75f - top * 6, i, j, .4f);
                        if (top < .07f) return m.Mortar;
                        if (top < .13f) return Lacquer(m, top, .07f, .13f, i, j);
                        if (top < .15f) return m.Mortar;
                        var stone = Blocks(c, m, x, y, i, j, 33);
                        // Faint cloud-scroll carving in the jade stone.
                        if (!stone.Equals(m.Mortar) && f.Ridge(x, y, .35f, 2, 57) > .9f) return Col.Lerp(stone, m.Cap[2], .35f);
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
            float top = 1 - y, n = f.N(x, 0, .2f, 71) * .5f + .5f;
            switch (id)
            {
                case "olympus":
                    if (top < 2 * px) return m.Cap.Last;
                    if (MathF.Abs(top - .1f) < 1.6f * px) return Kit.Mix(m.Trim, m.Accent, .7f, i, j);
                    if (top > .46f) return m.Stone[1];
                    return Blocks(c, m, x, y, i, j, 65, .2f, .9f, .24f);
                case "asgard":
                    {
                        if (top > .47f) return m.Mortar;
                        if (top < 2 * px) return m.Cap.Last;
                        float snow = .07f + n * .04f;
                        if (top < snow) return m.Cap.At(.85f - top * 4, i, j, .4f);
                        if (top < snow + 2 * px) return m.Cap[0];
                        return RuneStone(m, c, x, y, i, j, 67, 0, m.BlockW * .8f, .22f, .5f);
                    }
                case "dangun":
                    {
                        if (top > .47f) return m.Mortar;
                        if (top < 2 * px) return m.Cap.Last;
                        float moss = .05f + n * .04f;
                        if (top < moss) return m.Cap.At(.7f - top * 4, i, j, .4f);
                        if (top < moss + 2 * px) return m.Cap[0];
                        return Blocks(c, m, x, y, i, j, 67, 0, m.BlockW * .8f, .22f);
                    }
                case "eden":
                    {
                        if (top > .47f) return Col.Hex("2a2418");
                        if (top < 2 * px) return m.Cap.Last;
                        float grass = .1f + n * .04f;
                        if (top < grass)
                        {
                            var flower = Flower(x, top, grass, 73);
                            if (flower.A > 0) return flower;
                            return m.Cap.At(.8f - top / grass * .5f + (Field.Hash01(i, 1, 75) > .8f ? .2f : 0), i, j, .45f);
                        }
                        if (top < grass + 2 * px) return m.Cap[0];
                        return Earth(m, c, x, y, i, j, 67);
                    }
                case "veda":
                    {
                        if (top > .47f) return m.Mortar;
                        if (top < 2 * px) return m.Cap.Last;
                        if (top < .05f) { var petal = Petal(x, top, 77); return petal.A > 0 ? petal : m.Cap.At(.75f - top * 5, i, j, .4f); }
                        if (top < .065f) return m.Mortar;
                        if (top < .22f) return LotusFrieze(m, x, (top - .065f) / .155f, i, j);
                        if (top < .235f) return m.Mortar;
                        return Blocks(c, m, x, y, i, j, 67, 0, m.BlockW * .8f, .22f);
                    }
                case "papyrus":
                    {
                        if (top > .47f) return m.Mortar;
                        if (top < 2 * px) return m.Cap.Last;
                        if (top < .045f) return m.Cap.At(.85f - top * 5, i, j, .5f);
                        // Painted cornice: lapis, turquoise, terracotta, black, lapis.
                        if (top < .065f) return m.Accent;
                        if (top < .085f) return m.Trim;
                        if (top < .105f) return Col.Hex("c85a2c");
                        if (top < .125f) return Col.Hex("1e1812");
                        if (top < .145f) return m.Accent;
                        return Blocks(c, m, x, y, i, j, 67, 0, m.BlockW * .8f, .22f);
                    }
                default: // chinese
                    {
                        if (top > .47f) return m.Mortar;
                        if (top < 2 * px) return m.Cap.Last;
                        if (top < .04f) return m.Cap.At(.75f - top * 6, i, j, .4f);
                        if (top < .055f) return m.Mortar;
                        if (top < .115f) return Lacquer(m, top, .055f, .115f, i, j);
                        if (top < .13f) return m.Mortar;
                        return Blocks(c, m, x, y, i, j, 67, 0, m.BlockW * .8f, .22f);
                    }
            }
        });
        // Underside detail in the lower half.
        var under = c.Blank();
        switch (id)
        {
            case "olympus":
                // Sunlit cloud puffs sit just under the marble slab.
                Kit.CloudBank(under, 0, 4, .1f, .16f, .07f, .25f, 26, new Ramp("8e8aa4", "a8a2b8", "c0bac8", "d6d0d8", "e6e0e4", "f2eeee", "fbf8f4"), 95, .12f);
                break;
            case "asgard":
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
            case "dangun":
                {
                    // Timber brackets under the slab and roots creeping down.
                    var wood = new Ramp("2e1e12", "4a3220", "664a2c", "84623a", "a07c48");
                    under.Paint(0, 4, .3f, .5f, (x, y, i, j) =>
                    {
                        float u = M.Mod(x, 1) - .5f, h = .5f - y;
                        if (MathF.Abs(u) < .15f - h * .3f && h < .17f)
                            return wood.At(.5f - u * .9f + (h > .14f ? -.2f : 0) + f.Fbm2(x, y, .3f, .02f, 2, 83) * .12f, i, j, .35f);
                        return Col.Clear;
                    });
                    var rng = new Rng(97);
                    for (int k = 0; k < 5; k++) Kit.Vine(under, rng.R(0, 4), .5f, rng.R(.12f, .38f), Col.Hex("2c3a2c"), new Ramp("1a2620", "24342a", "324434", "44583e", "5c704a"), 99 + k, .03f, 1.2f);
                    break;
                }
            case "eden":
                {
                    var rng = new Rng(81);
                    var leaves = new Ramp("1e5a28", "2c7a34", "3e9a40", "5cb84c", "84d060", "a8e078");
                    for (int k = 0; k < 9; k++) Kit.Vine(under, rng.R(0, 4), .5f, rng.R(.15f, .46f), Col.Hex("4a5a2c"), leaves, 83 + k, .04f, 1.4f);
                    // Root tangles hugging the slab underside.
                    under.Paint(0, 4, .36f, .5f, (x, y, i, j) =>
                    {
                        float r = f.Ridge(x, y * 3, .3f, 2, 85);
                        return r > .86f && y > .5f - (f.N(x, 0, .3f, 87) * .5f + .5f) * .13f ? Col.Lerp(Col.Hex("4a3824"), Col.Hex("6e5436"), r - .86f) : Col.Clear;
                    });
                    // Golden fruit hanging among the vines.
                    var fr = new Rng(89);
                    for (int k = 0; k < 6; k++) { float fx = fr.R(0, 4), fy = fr.R(.2f, .4f); Disc(under, fx, fy, .03f, Col.Hex("e8b83a"), Col.Hex("fff0a0")); }
                    break;
                }
            case "veda":
                {
                    // Lotus-petal corbels and a sagging marigold garland.
                    under.Paint(0, 4, .3f, .5f, (x, y, i, j) =>
                    {
                        float u = M.Mod(x, .5f) - .25f, h = .5f - y, w = .2f * (1 - h / .19f);
                        if (h >= .19f || MathF.Abs(u) >= w) return Col.Clear;
                        float v = .55f - h * 1.2f + (u < 0 ? .08f : -.05f) + (MathF.Abs(u) > w - .035f ? -.22f : 0);
                        return m.Stone.At(v, i, j, .35f);
                    });
                    float sp = 4f / 56;
                    under.Paint(0, 4, .25f, .5f, (x, y, i, j) =>
                    {
                        float t = M.Mod(x, 1), gy = .47f - .15f * MathF.Sin(MathF.PI * t), dy = y - gy, a = M.Mod(x, sp) - sp * .5f, d = a * a + dy * dy;
                        if (d < .0007f) return Kit.Mix(Col.Hex("c04a14"), m.Trim, .7f - d / .0007f * .5f, i, j, 3);
                        if (MathF.Abs(dy) < .006f) return Col.Hex("3a6a3a");
                        return Col.Clear;
                    });
                    break;
                }
            case "papyrus":
                // Papyrus-bundle corbels with painted ties.
                under.Paint(0, 4, .28f, .5f, (x, y, i, j) =>
                {
                    float u = M.Mod(x, .5f) - .25f, h = .5f - y, r = .085f;
                    if (h > .2f || MathF.Abs(u) > r) return Col.Clear;
                    float v = Kit.Cylinder(u / r, .2f) * .8f + MathF.Cos(u / r * 14) * .06f;
                    if (h > .17f) v -= .3f;
                    if (h > .11f && h < .145f) return Kit.Mix(m.Accent, m.Trim, Kit.Cylinder(u / r, .3f), i, j, 3);
                    return m.Stone.At(v, i, j, .35f);
                });
                break;
            default:
                // Auspicious curled clouds carry the celestial slabs.
                Kit.CloudBank(under, 0, 4, .08f, .14f, .06f, .2f, 30, new Ramp("8a9aa0", "a8b4b8", "c4ccce", "dde2e2", "eef0ee", "f8f8f4", "ffffff"), 95, .15f);
                break;
        }
        // The underside sits behind the slab.
        under.Over(c); c = under;
        return c;
    }

    /// <summary>Darkened ends that close each platform's cut strip.</summary>
    static (Canvas left, Canvas right) Caps(string id, Mat m)
    {
        var left = new Canvas(16f / Ppu, 0, 1, Ppu); var right = new Canvas(16f / Ppu, 0, 1, Ppu);
        float px = 1f / Ppu;
        foreach (var (c, dir) in new[] { (left, -1), (right, 1) })
            c.PaintAll((x, y, i, j) =>
            {
                float top = 1 - y; if (top > .48f || y < .5f) return Col.Clear;
                int edge = dir < 0 ? i : 15 - i;
                if (edge < 2) return id == "olympus" ? m.Stone[2] : m.Mortar;
                if (edge < 4) return Col.Black.WithA(.35f);
                if (top < 2 * px && edge < 6) return m.Cap.Last.WithA(.6f);
                return Col.Clear;
            });
        return (left, right);
    }

    /// <summary>A shaded disc with a highlight, for fruit, berries and lamps.</summary>
    static void Disc(Canvas c, float cx, float cy, float r, Col fill, Col hi)
    {
        c.Paint(cx - r, cx + r, cy - r, cy + r, (x, y, i, j) =>
        {
            float dx = x - cx, dy = y - cy, d = dx * dx + dy * dy;
            if (d > r * r) return Col.Clear;
            float hx = dx + r * .35f, hy = dy - r * .35f;
            return hx * hx + hy * hy < r * r * .12f ? hi : d > r * r * .7f ? Col.Lerp(fill, Col.Black, .3f) : fill;
        });
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
        void Urn(float cx, float h, float width, Ramp body, Col band, float neck)
        {
            c.Paint(cx - width - .04f, cx + width + .04f, 0, h, (x, y, i, j) =>
            {
                float dx = x - cx, t = y / h;
                float hw = neck + MathF.Sin(MathF.PI * MathF.Min(1, t * 1.15f)) * (width - neck) + (t > .88f ? .03f : 0);
                if (t > 1 || MathF.Abs(dx) > hw) return Col.Clear;
                float v = Kit.Cylinder(dx / hw, .15f);
                if (MathF.Abs(t - .52f) < .035f || MathF.Abs(t - .3f) < .02f || t > .93f) return Kit.Mix(Col.Lerp(band, Col.Black, .45f), band, v, i, j, 4);
                return body.At(v * .8f + .1f, i, j, .3f);
            });
        }
        var wood = new Ramp("2e1e12", "4a3220", "664a2c", "84623a", "a07c48", "c09a5c");
        var green = new Ramp("1e5a28", "2c7a34", "3e9a40", "5cb84c", "84d060", "a8e078");
        var olive = new Ramp("2e4a2c", "3e5e38", "56764a", "6e8e5c", "8aa670", "a8be84");
        var bronze = new Ramp("1e2a20", "2e3e2c", "445840", "5a7050", "7a9068", "a0b088");
        var brass = new Ramp("5a3c14", "8a6020", "b8862c", "d8a83c", "f0c858", "fff0a0");
        var pink = new Ramp("8a3058", "b4467a", "d86a9c", "f090b8", "fab8d0", "ffe0ea");
        switch (id + k)
        {
            case "olympus0":
                // A broken fluted column stub with a gold band.
                c.Paint(.3f, .7f, 0, .5f, (x, y, i, j) =>
                {
                    float dx = x - .5f, top = .42f - dx * .5f + f.N(x, 0, .05f, 23) * .04f;
                    if (y > top) return Col.Clear;
                    if (y < .06f) return MathF.Abs(dx) < .17f ? m.Stone.At(.7f - dx, i, j, .3f) : Col.Clear;
                    if (MathF.Abs(dx) > .12f) return Col.Clear;
                    if (MathF.Abs(y - .2f) < .02f) return Kit.Mix(m.Trim, m.Accent, .7f - dx * 3, i, j);
                    return m.Stone.At(Kit.Cylinder(dx / .12f, .2f) * .85f + MathF.Cos(dx / .12f * 9) * .05f, i, j, .3f);
                });
                break;
            case "olympus1": Urn(.5f, .46f, .15f, new Ramp("6a5434", "8a6c3c", "b08e48", "d4b060", "f0d080", "fff4c0"), m.Accent, .05f); break;
            case "olympus2":
                // A laurel bush with gold berries.
                c.Paint(.47f, .53f, 0, .22f, (x, y, i, j) => MathF.Abs(x - .5f) < .018f ? wood.At(.4f + (x < .5f ? .15f : 0), i, j, .3f) : Col.Clear);
                Kit.LeafClump(c, .5f, .3f, .3f, .2f, 80, olive, 17, .2f, -.3f);
                foreach (var (bx, by) in new[] { (.36f, .3f), (.5f, .42f), (.63f, .27f), (.57f, .18f), (.42f, .17f) }) Disc(c, bx, by, .02f, m.Accent, Col.Hex("fff4c0"));
                break;
            case "asgard0":
                // A rune-carved standing stone under a cap of snow.
                c.Paint(.3f, .7f, 0, .78f, (x, y, i, j) =>
                {
                    float dx = x - .5f, hw = .13f - y * .04f - MathF.Max(0, y - .62f) * .5f;
                    if (MathF.Abs(dx) > hw || y > .74f) return Col.Clear;
                    if (y > .66f - MathF.Abs(dx) * .3f) return m.Cap.At(.9f - (dx + .13f) * 1.2f, i, j, .3f);
                    float v = Kit.Cylinder(dx / .13f, .25f) * .7f + f.Fbm(x, y, .2f, 2, 19) * .1f;
                    int row = (int)MathF.Floor((y - .08f) / .17f); float ly = (y - .08f) - row * .17f;
                    if (row >= 0 && row < 3)
                    {
                        float d = RuneDist(row + 1, (dx + .13f) / .26f, ly / .17f, .26f * Ppu, .17f * Ppu);
                        if (d < 1.3f) return Kit.Mix(m.Accent, Col.White, .3f, i, j, 3);
                        if (d < 3f) return Kit.Mix(m.Stone.At(v, i, j, .3f), m.Accent, .55f - d * .12f, i, j, 3);
                    }
                    return m.Stone.At(v, i, j, .3f);
                });
                c.Glow(.5f, .36f, .3f, .4f, m.Accent, .18f, 4, true);
                break;
            case "asgard1":
                Rubble(4, new Ramp("1e2a3a", "2c3c50", "40546c", "5a708a", "7a90a8"), .2f);
                c.Post((i, j, col) => c.Depth(i, j, 0, 1, 4) < 4 ? Col.Hex(c.Depth(i, j, 0, 1, 4) < 1 ? "ffffff" : "d4e0ee") : col);
                break;
            case "asgard2": Kit.Pine(c, .5f, 0, .9f, new Ramp("0e1c26", "15283a", "1d3648", "284658", "355868"), Col.Hex("eaf2f8"), Col.Hex("b4c4e0"), 21); break;
            case "dangun0":
                {
                    // A prayer cairn of stacked flat stones.
                    float yy = .02f;
                    for (int s = 0; s < 6; s++)
                    {
                        float sw = .19f - s * .024f, sh = .052f - s * .003f, sx = .5f + rng.R(-.025f, .025f), sy = yy + sh;
                        c.Paint(sx - sw, sx + sw, sy - sh, sy + sh, (x, y, i, j) =>
                        {
                            float dx = (x - sx) / sw, dy = (y - sy) / sh;
                            if (dx * dx + dy * dy > 1) return Col.Clear;
                            return m.Stone.At(.42f + dy * .3f + (dx < 0 ? .1f : -.06f) + f.Fbm(x, y, .1f, 2, 23) * .08f, i, j, .35f);
                        });
                        yy += sh * 1.85f;
                    }
                    break;
                }
            case "dangun1":
                // A jangseung guardian post: carved face, black hat, obangsaek bands.
                c.Paint(.3f, .7f, 0, .95f, (x, y, i, j) =>
                {
                    float dx = x - .5f;
                    if (y > .78f) return MathF.Abs(dx) < .13f && y < .9f - MathF.Abs(dx) * .4f ? Kit.Grain(Col.Hex("1c1612"), i, j, 25, .08f, .15f) : Col.Clear;
                    if (MathF.Abs(dx) > .09f) return Col.Clear;
                    float v = Kit.Cylinder(dx / .09f, .2f) * .75f + .1f;
                    if (y > .5f)
                    {
                        bool eye = MathF.Abs(y - .69f) < .025f && (MathF.Abs(dx - .04f) < .022f || MathF.Abs(dx + .04f) < .022f);
                        bool brow = MathF.Abs(y - .735f) < .008f && MathF.Abs(dx) < .07f;
                        bool nose = MathF.Abs(dx) < .012f && y > .6f && y < .66f;
                        bool mouth = MathF.Abs(y - .56f) < .022f && MathF.Abs(dx) < .06f;
                        if (eye || brow || nose) return Col.Hex("1c1612");
                        if (mouth) return (i & 3) < 2 ? Col.Hex("1c1612") : Col.Hex("f0e6d0");
                        return wood.At(v, i, j, .3f);
                    }
                    float band = M.Mod(y, .16f);
                    if (band < .03f) return Kit.Mix(Col.Lerp(m.Accent, Col.Black, .4f), m.Accent, v, i, j, 3);
                    if (band > .06f && band < .085f) return Kit.Mix(Col.Lerp(m.Trim, Col.Black, .4f), m.Trim, v, i, j, 3);
                    return wood.At(v, i, j, .3f);
                });
                break;
            case "dangun2": Rubble(2, m.Stone, .12f); Tuft(new Ramp("2a3a20", "3e5230", "566c40", "728852", "90a462"), .34f, 11, Col.Hex("f0d048")); break;
            case "eden0":
                Tuft(green, .34f, 14, Col.Hex("f07aa0"));
                foreach (var (bx, by, col) in new[] { (.3f, .22f, "f8f4ff"), (.52f, .3f, "f8d848"), (.7f, .2f, "6a8af0") }) Disc(c, bx, by, .035f, Col.Hex(col), Col.Hex("fff4b0"));
                break;
            case "eden1": Kit.LeafClump(c, .5f, .2f, .34f, .2f, 70, green, 27, .3f, -.5f); break;
            case "eden2":
                // A fruit bush heavy with golden fruit.
                Kit.LeafClump(c, .5f, .3f, .28f, .24f, 90, green.Shift(-.1f), 29, .18f, -.4f);
                foreach (var (bx, by) in new[] { (.38f, .3f), (.52f, .44f), (.64f, .3f), (.46f, .2f), (.6f, .18f) }) Disc(c, bx, by, .035f, m.Accent, Col.Hex("fff8d0"));
                break;
            case "veda0":
                // A clay diya with a living flame.
                c.Glow(.5f, .17f, .26f, .22f, m.Accent, .45f, 4, true);
                c.Paint(.3f, .7f, 0, .3f, (x, y, i, j) =>
                {
                    float dx = x - .5f;
                    if (y < .1f)
                    {
                        float hw = .06f + y * .9f;
                        if (MathF.Abs(dx) > hw) return Col.Clear;
                        return new Ramp("4a2416", "70381e", "985028", "c07038", "dc9050").At(.55f + (dx < 0 ? .15f : -.1f) - (y < .02f ? .25f : 0), i, j, .35f);
                    }
                    float t = (y - .1f) / .15f; if (t > 1) return Col.Clear;
                    float fw = .04f * (1 - t) * MathF.Sqrt(MathF.Min(1, t * 3 + .15f)) + .004f;
                    if (MathF.Abs(dx) > fw) return Col.Clear;
                    float core = MathF.Abs(dx) / fw;
                    return t < .35f && core < .6f ? Col.Hex("ffe690") : core < .7f ? Col.Hex("ffb030") : Col.Hex("e85a20");
                });
                break;
            case "veda1":
                // A lotus on its pad.
                c.Paint(.47f, .53f, 0, .3f, (x, y, i, j) => MathF.Abs(x - .5f) < .012f ? Col.Hex("3a7a46") : Col.Clear);
                c.Paint(.2f, .6f, 0, .1f, (x, y, i, j) => { float dx = (x - .38f) / .16f, dy = (y - .05f) / .035f; return dx * dx + dy * dy < 1 ? Col.Lerp(Col.Hex("2a6a3a"), Col.Hex("4a9a56"), M.Clamp01(.6f - dy * .4f)) : Col.Clear; });
                foreach (int p in new[] { 2, -2, 1, -1, 0 })
                {
                    float cx = .5f + p * .055f, lean = p * .35f; int pp = p;
                    c.Paint(cx - .1f, cx + .1f, .28f, .5f, (x, y, i, j) =>
                    {
                        float v = (y - .3f) / .18f; if (v < 0 || v > 1) return Col.Clear;
                        float u = x - cx - lean * v * .12f, w = .04f * MathF.Sin(MathF.PI * MathF.Min(1, v * 1.05f)) + .003f;
                        if (MathF.Abs(u) > w) return Col.Clear;
                        return pink.At(.55f + v * .35f - MathF.Abs(u) / w * .25f + (pp == 0 ? .1f : 0), i, j, .3f);
                    });
                }
                Disc(c, .5f, .33f, .02f, Col.Hex("f8d848"), Col.Hex("fff8c0"));
                break;
            case "veda2":
                // A brass kalash wreathed in marigolds.
                Urn(.5f, .36f, .14f, brass, m.Accent, .07f);
                foreach (var (bx, by) in new[] { (.37f, .3f), (.44f, .33f), (.5f, .34f), (.56f, .33f), (.63f, .3f) }) Disc(c, bx, by, .025f, m.Accent, m.Trim);
                break;
            case "papyrus0":
                // A lapis scarab.
                c.Paint(.25f, .75f, 0, .3f, (x, y, i, j) =>
                {
                    float dx = x - .5f, dy = y - .1f, d = dx * dx / .0289f + dy * dy / .0081f;
                    if (d < 1)
                    {
                        float v = 1 - d;
                        if (MathF.Abs(dx) < .006f && dy > -.03f) return Col.Hex("101828");
                        if (MathF.Abs(dy - .035f) < .005f && MathF.Abs(dx) < .1f) return Col.Hex("101828");
                        return new Ramp("102848", "1a4078", "2460a8", "2a8ab0", "40b8b0", "90e0d0").At(.3f + v * .55f + (dx < 0 ? .1f : 0), i, j, .3f);
                    }
                    for (int s = -1; s <= 1; s += 2) for (int l = 0; l < 3; l++)
                    {
                        float ax = s * (.07f + l * .045f), ay = .06f - l * .035f;
                        if (SegDist(dx, dy, ax, ay, ax + s * .09f, ay - .07f) < .009f) return Col.Hex("101828");
                    }
                    return Col.Clear;
                });
                break;
            case "papyrus1":
                // A canopic jar with a jackal lid.
                Urn(.5f, .4f, .12f, new Ramp("8a6e3e", "a8884c", "c4a45e", "dcbe74", "f0d88e", "fcf0c0"), m.Accent, .08f);
                c.Paint(.36f, .64f, .36f, .56f, (x, y, i, j) =>
                {
                    float dx = x - .5f, dy = y - .42f;
                    bool head = dx * dx / .0121f + dy * dy / .0064f < 1 && y > .38f;
                    bool ear = MathF.Abs(MathF.Abs(dx) - .06f) < .018f && y > .44f && y < .55f - MathF.Abs(MathF.Abs(dx) - .06f) * 2;
                    if (!head && !ear) return Col.Clear;
                    if (MathF.Abs(dy + .01f) < .006f && dx > -.06f && dx < .06f) return Kit.Mix(m.Trim, m.Accent, .5f, i, j, 2);
                    return Kit.Grain(Col.Lerp(Col.Hex("141210"), Col.Hex("3a3028"), M.Clamp01(.5f - dx * 3)), i, j, 31, .1f, .1f);
                });
                break;
            case "papyrus2":
                // Papyrus reeds with fanned umbels.
                for (int s = 0; s < 6; s++)
                {
                    float bx = .5f + rng.R(-.22f, .22f), lean = rng.R(-.12f, .12f), h = rng.R(.5f, .85f), tx = bx + lean, sr = rng.R(.05f, .08f);
                    c.Paint(bx - .3f, bx + .3f, 0, h + .1f, (x, y, i, j) =>
                    {
                        float t = y / h;
                        if (t <= 1 && MathF.Abs(x - (bx + lean * t * t)) < .01f) return green.At(.3f + t * .3f, i, j, .3f);
                        float dx = x - tx, dy = y - h, r = MathF.Sqrt(dx * dx + dy * dy);
                        if (r > sr || dy < -sr * .3f) return Col.Clear;
                        float ang = MathF.Atan2(dy, dx), spoke = M.Fract(ang / MathF.PI * 7);
                        return spoke < .22f || r < .012f ? green.At(.55f + r / sr * .35f, i, j, .3f) : Col.Clear;
                    });
                }
                break;
            case "chinese0":
                // A bronze tripod censer breathing incense.
                c.Paint(.25f, .75f, 0, .9f, (x, y, i, j) =>
                {
                    float dx = x - .5f;
                    if (y > .46f)
                    {
                        float t = (y - .46f) / .44f, sx = dx - MathF.Sin(t * 5 + f.N(0, y, .3f, 33)) * .05f * t;
                        float a = (1 - t) * .5f * M.Clamp01(1 - MathF.Abs(sx) / (.03f + t * .09f)) * (f.N(x * 3, y * 3, .25f, 35) * .5f + .7f);
                        return a > .12f ? Col.Hex("d8d8d0").WithA(M.Step(a, 4)) : Col.Clear;
                    }
                    bool ear = y > .36f && y < .46f && MathF.Abs(MathF.Abs(dx) - .13f) < .025f;
                    bool body = y > .12f && y < .38f && MathF.Abs(dx) < .17f - MathF.Max(0, .18f - y) * .5f;
                    bool leg = y <= .13f && (MathF.Abs(MathF.Abs(dx) - .11f) < .022f || (MathF.Abs(dx) < .022f && y < .11f));
                    if (!ear && !body && !leg) return Col.Clear;
                    float v = Kit.Cylinder(dx / .17f, .22f) * .8f;
                    if (body && MathF.Abs(y - .25f) < .012f) v += .2f;
                    if (body && y > .35f) v += .15f;
                    if (body && MathF.Abs(y - .2f) < .04f && M.Fract(x * 24) < .3f) v -= .15f; // taotie band
                    return Kit.Grain(bronze.At(v, i, j, .3f), i, j, 37, .06f, .1f);
                });
                break;
            case "chinese1":
                // A red lantern on a lacquered post.
                c.Glow(.56f, .5f, .24f, .24f, Col.Hex("ff6a30"), .35f, 4, true);
                c.Paint(.3f, .8f, 0, .78f, (x, y, i, j) =>
                {
                    float dx = x - .4f;
                    if (MathF.Abs(dx) < .016f && y < .72f) return Kit.Mix(Col.Hex("6a1810"), m.Accent, .5f + (dx < 0 ? .3f : 0), i, j, 3);
                    if (MathF.Abs(y - .7f) < .012f && x > .38f && x < .6f) return Col.Hex("6a1810");
                    if (y > .66f && y < .76f && MathF.Abs(x - .4f) < .06f - (y - .66f) * .4f) return Kit.Mix(Col.Hex("3a1008"), m.Trim, .7f, i, j, 3);
                    float lx = (x - .57f) / .09f, ly = (y - .5f) / .12f, d = lx * lx + ly * ly;
                    if (y > .63f && y < .67f && MathF.Abs(x - .57f) < .035f) return m.Trim;
                    if (y > .33f && y < .37f && MathF.Abs(x - .57f) < .035f) return m.Trim;
                    if (d < 1)
                    {
                        float rib = M.Fract(MathF.Asin(M.Clamp01(MathF.Abs(lx))) / MathF.PI * 2 * 3);
                        float v = .55f + (1 - d) * .4f - (rib < .18f ? .2f : 0);
                        return new Ramp("5a1210", "8c1c16", "c02a22", "e84a34", "ff8a5a", "ffd0a0").At(v, i, j, .3f);
                    }
                    if (y < .33f && y > .22f && MathF.Abs(x - .57f) < .012f + (.33f - y) * .1f) return (j & 2) == 0 ? m.Trim : Col.Hex("c88a20");
                    return Col.Clear;
                });
                break;
            default:
                // chinese2: a peach tree of immortality in blossom.
                c.Paint(.42f, .58f, 0, .3f, (x, y, i, j) => MathF.Abs(x - .5f - (y - .15f) * .2f) < .03f - y * .03f ? wood.At(.35f + (x < .5f ? .15f : 0), i, j, .3f) : Col.Clear);
                Kit.LeafClump(c, .5f, .42f, .32f, .22f, 110, pink, 39, .14f, -.2f);
                foreach (var (bx, by) in new[] { (.36f, .4f), (.5f, .5f), (.64f, .38f), (.55f, .3f) }) Disc(c, bx, by, .03f, Col.Hex("f0b04a"), Col.Hex("ffe0a0"));
                break;
        }
        return c;
    }
}
