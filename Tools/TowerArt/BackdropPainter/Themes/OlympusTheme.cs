namespace BackdropPainter;

/// <summary>Mount Olympus at golden hour: cream marble temples on sunlit terraces above a bright cloud
/// sea, a low sun with rays, snow-lit peaks, cypresses and olives, bronze braziers, laurel garlands,
/// amphorae and a golden eagle medallion; the cloud sea rolls below the walkway in the near plane.</summary>
public sealed class OlympusTheme : ThemePainter
{
    public override string Id => "olympus";
    public override bool OpenBelow => true;

    static readonly Ramp Marble = new("625a62", "7f746f", "9c8e7e", "b8a992", "cfc2a9", "e2d7c1", "f0e8d5", "faf5e8");
    static readonly Ramp CloudRamp = new("a494c4", "b4a6cc", "c6b9d6", "d7cbdd", "e6dbe2", "f1e8e8", "f8f1ec", "fdf7ef", "fffcf4");
    static readonly Ramp FarCloud = new("b7a8cc", "c8bad4", "d8cddc", "e6dce2", "f2eae8", "faf4ee");
    static readonly Ramp Bronze = new("3a2616", "5c3c20", "86592c", "b0813e", "d4a85c", "f0d08a");
    static readonly Ramp Laurel = new("2a4426", "385a30", "4a7238", "628c46", "7ea656", "9cc06a");
    static readonly Ramp Cypress = new("1e3424", "2a4630", "365838", "466c44", "587e50");
    static readonly Ramp Olive = new("3e4e3c", "56684e", "6e8262", "8a9c78", "a6b490", "c0cca8");
    static readonly Col Gold = Col.Hex("e4bc5c"), GoldDark = Col.Hex("9c7430"), GoldLight = Col.Hex("f8e6a8");
    static readonly Col SunCore = Col.Hex("fff8e0"), SunGlow = Col.Hex("ffd890"), Flame = Col.Hex("ffb248"), FlameCore = Col.Hex("fff4c0");
    static readonly (float y, Col c)[] SkyStops = Kit.Stops((-11, "f9e2bc"), (-7.5f, "f6cfa0"), (-4, "efbfa0"), (-.5f, "d9b9b4"), (3, "a9b8d6"), (6.5f, "78a2d8"), (10, "5a8cd2"));
    const float SunX = 9.6f, SunY = 3.2f;

    /// <summary>A cloud sea built from overlapping banks so the puffs stay even across the wrap seam.</summary>
    static void Sea(Canvas c, float baseY, float height, float minR, float maxR, int perBank, Ramp ramp, int seed, float rim)
    {
        float p = c.Period;
        for (int k = 0; k < 4; k++)
        {
            float x0 = k * p / 4 - p / 8;
            Kit.CloudBank(c, x0, x0 + p / 2, baseY, height, minR, maxR, perBank, ramp, seed + k * 7, rim);
        }
    }

    /// <summary>An olive tree: a twisted trunk under a silvery lobed canopy with a ragged edge.</summary>
    static void OliveTree(Canvas p, float x, float baseY, float h, int seed)
    {
        var f = p.F; float r = h * .42f, cy = baseY + h - r * .9f;
        var trunk = new Ramp("3e3028", "5a4838", "7a6450", "9a8468");
        p.Paint(x - r * 1.4f, x + r * 1.4f, baseY, baseY + h + .1f, (px, py, i, j) =>
        {
            float dx = px - x;
            float best = 9;
            foreach (var (ox, oy, rr) in new[] { (0f, .05f, 1f), (-.6f, -.2f, .7f), (.55f, -.15f, .72f) })
            {
                float ex = (dx - ox * r) / (rr * r * 1.15f), ey = (py - cy - oy * r) / (rr * r * .8f);
                best = Math.Min(best, ex * ex + ey * ey);
            }
            float edge = 1 + f.N(px, py, .3f, seed) * .4f;
            if (best <= edge)
            {
                float v = .5f - dx / r * .22f + (py - cy) / r * .3f + f.Fbm(px, py, .22f, 2, seed + 1) * .4f;
                if (best > edge * .72f) v -= .12f;
                return Olive.At(v, i, j, .45f);
            }
            if (py < cy)
            {
                float tx = MathF.Sin((py - baseY) * 2.6f + seed) * .06f, hw = .05f + MathF.Max(0, baseY + .25f - py) * .25f;
                if (MathF.Abs(dx - tx) < hw) return trunk.At(.5f - (dx - tx) / hw * .3f, i, j, .4f);
            }
            return Col.Clear;
        });
    }

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return Sky();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    static Col Fogged(float y) => Kit.Smooth(SkyStops, y);

    PaintedLayer Sky()
    {
        var s = new LayerSpec("sky", .95f, 48, 30, -11, 21, -97) { Under = SkyStops[0].c, Over = SkyStops[^1].c };
        var c = s.NewCanvas(); var f = c.F;
        c.PaintAll((x, y, i, j) => Kit.Gradient(SkyStops, y, 4, 1, i, j));
        // Sun halo and rays, then the disc itself.
        Kit.Glow(c, SunX, SunY, 7.5f, SunGlow, .5f, 1, 6);
        Kit.Glow(c, SunX, SunY, 2.6f, SunCore, .45f, 1, 4);
        c.Paint(SunX - 9, SunX + 9, SunY - 9, SunY + 9, (x, y, i, j) =>
        {
            float dx = f.Dx(x, SunX), dy = y - SunY, d = MathF.Sqrt(dx * dx + dy * dy);
            if (d < 1.2f || d > 9) return Col.Clear;
            float ang = MathF.Atan2(dy, dx) / (MathF.PI * 2) + .5f;
            float ray = Field.Lattice(ang * 28, 0, 28, 5) * .5f + .5f;
            float a = M.Smooth(.45f, .85f, ray) * MathF.Pow(1 - d / 9, 1.6f) * .38f;
            float q = MathF.Floor(a * 6 + Dither.Bayer(i, j)) / 6;
            return q <= 0 ? Col.Clear : Col.Hex("fff0c8").WithA(q);
        }, add: true);
        c.Paint(SunX - 1.3f, SunX + 1.3f, SunY - 1.3f, SunY + 1.3f, (x, y, i, j) =>
        {
            float dx = f.Dx(x, SunX), dy = y - SunY, d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > 1.0f) return Col.Clear;
            return d > .9f ? Col.Lerp(SunCore, SunGlow, .3f) : Col.Lerp(SunCore, Col.White, .4f);
        });
        // High cirrus streaks, warm-lit.
        c.Paint(0, c.Period, 3, 10, (x, y, i, j) =>
        {
            float n = f.Fbm2(x, y, 6, .45f, 4, 11);
            float a = M.Smooth(.22f, .6f, n) * .4f;
            float q = MathF.Floor(a * 5 + Dither.Bayer(i, j)) / 5;
            return q <= 0 ? Col.Clear : Col.Hex("fbeedc").WithA(q);
        });
        // Distant snow-lit peaks poking through the cloud sea.
        var peaks = new[] { (px: 29f, py: 3f, w: 6.5f), (px: 22.5f, py: 2.2f, w: 4.5f), (px: 7.4f, py: 1.5f, w: 4f), (px: 16.5f, py: .8f, w: 3.5f) };
        float Smooth(float x, float px, float py, float w) => py - MathF.Abs(f.Dx(x, px)) / w * (py + 4) + f.Fbm(x, 0, 1.6f, 2, 13) * .6f;
        float Ridge(float x, float px, float py, float w) => Smooth(x, px, py, w) - MathF.Abs(f.N(x, 0, .45f, 14)) * .3f;
        c.Paint(0, c.Period, -4.5f, 3.6f, (x, y, i, j) =>
        {
            foreach (var (px, py, w) in peaks)
            {
                if (MathF.Abs(f.Dx(x, px)) > w) continue;
                float h = Ridge(x, px, py, w);
                if (y > h) continue;
                bool lit = Smooth(x + .2f, px, py, w) > Smooth(x - .2f, px, py, w) + .02f;
                float snow = M.Smooth(h - 1.8f, h - .3f, y) + f.N(x, y, .5f, 16) * .2f;
                var rock = lit ? Col.Hex("c0aab4") : Col.Hex("9a98c8");
                var cap = lit ? Col.Hex("fbeee0") : Col.Hex("c8c0dc");
                var col = Kit.Mix(rock, cap, snow, i, j, 3);
                if (f.Ridge(x * .6f, y, .5f, 2, 17) > .95f && snow < .9f) col = Col.Lerp(col, Col.Hex("7c789e"), .35f);
                return Col.Lerp(col, Fogged(y), .4f + M.Smooth(1, -4, y) * .2f);
            }
            return Col.Clear;
        });
        // Cloud sea at the horizon.
        Kit.CloudBank(c, 0, 30, -11, 4.6f, .9f, 2.3f, 30, FarCloud.Fogged(SkyStops[1].c, .35f), 19, .2f);
        Kit.CloudBank(c, 0, 30, -11, 2.4f, .7f, 1.6f, 34, FarCloud.Fogged(SkyStops[0].c, .2f), 21, .25f);
        return new PaintedLayer(s, c);
    }

    /// <summary>A Doric temple front: stepped stylobate, fluted columns with echinus capitals, an
    /// architrave and triglyph frieze under a gold-trimmed cornice, and a pediment with relief figures.</summary>
    static void Temple(Canvas p, float cx, float baseY, float half, int cols, float colH, float scale, int seed)
    {
        var f = p.F;
        float stepH = .22f * scale, stylo = baseY + 3 * stepH, r = .19f * scale, margin = r * 1.9f;
        float ent0 = stylo + colH, ent1 = ent0 + 1.05f * scale, pedH = half * .3f, apex = ent1 + pedH;
        float ColX(int k) => cx - half + margin + (2 * half - 2 * margin) * k / (cols - 1);
        p.Paint(cx - half - 3 * .26f * scale - .2f, cx + half + 3 * .26f * scale + .2f, baseY - .05f, apex + .5f * scale, (x, y, i, j) =>
        {
            float dx = x - cx, ax = MathF.Abs(dx);
            // Stylobate steps.
            if (y < stylo)
            {
                int step = (int)MathF.Floor((y - baseY) / stepH);
                float w = half + (2 - step) * .26f * scale;
                if (ax > w) return Col.Clear;
                float t = M.Fract((y - baseY) / stepH);
                return Marble.At(t > .8f ? .9f : .62f - (w - ax < .06f * scale ? .2f : 0), i, j, .35f);
            }
            // Pediment.
            if (y >= ent1)
            {
                float slope = pedH / half, roof = apex - ax * slope;
                if (y > roof + .16f * scale)
                {
                    // Acroteria at the apex and corners.
                    float ak = Math.Min(ax, MathF.Abs(ax - half));
                    float at = y - (ax < .4f ? apex + .16f * scale : ent1 + .16f * scale);
                    if (ak < .22f * scale && at > 0 && at < .34f * scale && ak < .22f * scale * (1 - at / (.34f * scale)) + .04f) return Kit.Mix(GoldDark, Gold, .6f - at, i, j);
                    return Col.Clear;
                }
                if (ax > half + .1f * scale) return Col.Clear;
                if (y > roof - .02f * scale) return Kit.Mix(GoldDark, GoldLight, .55f - dx * .04f, i, j); // raking gold trim
                if (y > roof - .16f * scale) return Marble.At(.78f, i, j, .3f);                          // raking cornice
                // Tympanum with relief figures.
                float fig = f.Fbm2(x, y, .28f * scale, .5f * scale, 2, seed + 1);
                bool relief = fig > .1f && y < roof - .3f * scale && y > ent1 + .06f * scale;
                float v = .42f + (relief ? .2f + fig * .3f : 0) + (dx < 0 ? .04f : -.04f);
                return Marble.At(v, i, j, .35f);
            }
            // Entablature: architrave, taenia, frieze, cornice.
            if (y >= ent0)
            {
                if (ax > half) return Col.Clear;
                float t = (y - ent0) / (ent1 - ent0);
                if (t < .32f) return Marble.At(.6f + (t < .05f ? -.15f : 0), i, j, .3f);
                if (t < .37f) return Kit.Mix(GoldDark, Gold, .6f, i, j);
                if (t < .8f)
                {
                    float tw = f.Snap(.62f * scale), tu = M.Mod(x - cx + tw * .5f, tw) / tw;
                    if (tu < .34f)
                    {
                        float g = M.Fract(tu / .34f * 3);
                        return Marble.At(g < .3f ? .3f : .62f, i, j, .3f); // triglyph grooves
                    }
                    float dot = MathF.Abs(tu - .67f) < .14f && MathF.Abs(t - .58f) < .13f ? .18f : 0; // metope relief
                    return Marble.At(.72f + dot, i, j, .3f);
                }
                if (t < .86f) return Marble.At(.35f, i, j, .3f);
                return Marble.At(t > .96f ? .95f : .8f, i, j, .3f);
            }
            // Columns in front of the shaded cella wall.
            for (int k = 0; k < cols; k++)
            {
                float u = (x - ColX(k)) / r;
                float t = y - stylo;
                if (t > colH - .22f * scale)
                {
                    float kk = (t - (colH - .22f * scale)) / (.22f * scale);
                    float w = kk > .68f ? r * 1.55f : r * (1 + MathF.Pow(kk / .68f, 2) * .45f);
                    if (MathF.Abs(x - ColX(k)) < w) return Marble.At(kk > .68f ? .8f - (x - ColX(k)) * .3f : Kit.Cylinder((x - ColX(k)) / w, .25f) * .8f, i, j, .3f);
                    continue;
                }
                if (MathF.Abs(u) > 1) continue;
                float flute = MathF.Cos(MathF.Asin(u) * 10) * .07f;
                return Marble.At(Kit.Cylinder(u, .25f) * .82f + flute, i, j, .35f);
            }
            if (ax < half - margin * .5f) return Marble.At(.28f + f.Fbm(x, y, .8f, 2, seed + 3) * .06f, i, j, .4f);
            return Col.Clear;
        });
    }

    /// <summary>A tall cypress: a narrow flame-shaped crown of dark green with a lit left edge.</summary>
    static void CypressTree(Canvas p, float x, float baseY, float h, int seed)
    {
        var f = p.F; float w = h * .12f;
        p.Paint(x - w - .1f, x + w + .1f, baseY, baseY + h + .05f, (px, py, i, j) =>
        {
            float t = (py - baseY) / h; if (t < 0 || t > 1) return Col.Clear;
            if (t < .08f) return MathF.Abs(px - x) < .03f ? Cypress[0] : Col.Clear;
            float hw = w * MathF.Pow(MathF.Sin(MathF.PI * MathF.Min(1, (t - .05f) / .95f * .92f + .05f)), .7f) * (1 + f.N(px, py, .25f, seed) * .25f);
            float dx = px - x; if (MathF.Abs(dx) > hw) return Col.Clear;
            float v = .35f - dx / hw * .3f + f.N(px, py, .2f, seed + 1) * .25f - t * .1f;
            return Cypress.At(v, i, j, .4f);
        });
    }

    PaintedLayer Far()
    {
        var s = new LayerSpec("far", .85f, 64, 32, -11, 21, -96) { Under = FarCloud[2] };
        var c = s.NewCanvas(); var f = c.F;
        // Rocky summit shoulder with snow at the wrap seam.
        var rock = c.Blank();
        float ShoulderSmooth(float x) { float dx = f.Dx(x, 31.5f); return 1.1f - dx * dx * .1f + f.Fbm(x, 0, 1.6f, 2, 31) * .5f; }
        float Shoulder(float x) => ShoulderSmooth(x) - MathF.Abs(f.N(x, 0, .5f, 32)) * .3f;
        rock.Paint(0, c.Period, -11, 2.5f, (x, y, i, j) =>
        {
            float dx = f.Dx(x, 31.5f), top = Shoulder(x);
            if (MathF.Abs(dx) > 4.6f || y > top) return Col.Clear;
            bool lit = ShoulderSmooth(x + .2f) > ShoulderSmooth(x - .2f) + .02f;
            float snow = M.Smooth(top - 1.6f, top - .2f, y) + f.N(x, y, .6f, 34) * .2f;
            var col = Kit.Mix(lit ? Col.Hex("ac9ca8") : Col.Hex("948eb4"), lit ? Col.Hex("f8eee4") : Col.Hex("d4cce0"), snow, i, j, 3);
            if (f.Ridge(x * .6f, y, .6f, 2, 35) > .95f && snow < .9f) col = Col.Lerp(col, Col.Hex("6e6a90"), .4f);
            return col;
        });
        // Lower terrace: a round-arched marble stoa with dentils under its cornice.
        var style = new ArcadeStyle
        {
            Span = 3.2f, PierHalf = .4f, Base = -7.5f, Spring = -2.6f, Pointed = 0, Rings = 1, RingW = .12f, PlinthH = .5f, CapH = .3f,
            Deck = -1.05f, CorniceH = .32f, Corbel = .22f, GalleryTop = -.73f, Gallery = false, Oculus = false, Shafts = false, Phase = .6f,
            Missing = (x, y) => MathF.Abs(f.Dx(x, 9.6f)) < 1.3f + f.N(0, y, .8f, 37) * .4f && y > -4.4f + f.N(x, 0, .5f, 38) * .3f
        };
        var stoa = c.Blank();
        stoa.Paint(0, c.Period, -7.5f, -.6f, (x, y, i, j) =>
        {
            var h = ArchKit.Hit(style, f, x, y);
            if (h.Part == Part.None) return Col.Clear;
            float v = ArchKit.BaseTone(h) * .5f + Kit.Lambert(h.N.X, h.N.Y, h.N.Z) * .55f;
            if (h.Part is Part.Wall or Part.Pier)
            {
                bool mortar = Kit.Masonry(f, x, y, 1.1f, .48f, 1.3f / 64, 39, out float sh, out _, out _, out _);
                v += (sh - .5f) * .08f - (mortar ? .12f : 0);
            }
            var col = Marble.At(v + f.Fbm(x, y, .9f, 2, 41) * .05f, i, j, .4f);
            if (h.Part == Part.Cornice && h.Detail > .72f) col = Kit.Mix(GoldDark, Gold, .6f, i, j);
            return col;
        });
        // Retaining wall that carries the right-hand temple on a higher terrace.
        stoa.Paint(22.6f, 31, -1.1f, 1.7f, (x, y, i, j) =>
        {
            if (y > 1.55f) return Marble.At(.92f, i, j, .3f);
            bool mortar = Kit.Masonry(f, x, y, 1.2f, .5f, 1.3f / 64, 43, out float sh, out _, out _, out _);
            float v = .58f + (sh - .5f) * .1f - (mortar ? .14f : 0) + f.Fbm(x, y, .8f, 2, 45) * .05f;
            if (y > 1.25f && y < 1.45f) v += .18f;
            return Marble.At(v, i, j, .4f);
        });
        var temples = c.Blank();
        Temple(temples, 16, -.75f, 4.2f, 6, 3.4f, 1, 51);
        Temple(temples, 5.4f, -.75f, 2.3f, 4, 2.3f, .72f, 53);
        Temple(temples, 26.8f, 1.5f, 2.0f, 4, 2.1f, .66f, 55);
        // Cypresses and olives among the terraces.
        var trees = c.Blank();
        foreach (var (tx, th) in new[] { (1.1f, 3.6f), (1.9f, 4.4f), (10.9f, 4.2f), (11.8f, 3.1f), (21.2f, 3.8f), (23.2f, 2.6f), (30.4f, 3f), (29.7f, 2.2f) })
            CypressTree(trees, tx, tx > 22 && tx < 29 ? 1.55f : -.75f, th, (int)(tx * 29));
        foreach (var (ox, oy, h) in new[] { (9.1f, -.75f, 2.4f), (13.1f, -.75f, 1.9f), (24.3f, 1.55f, 1.9f), (3.0f, -.75f, 2f) })
            OliveTree(trees, ox, oy, h, (int)(ox * 17));
        Kit.Fog(rock, y => Fogged(y), y => .55f + M.Smooth(1, -6, y) * .25f);
        Kit.Fog(stoa, y => Fogged(y), y => .5f + M.Smooth(-1, -7, y) * .3f);
        Kit.Fog(temples, y => Fogged(y), y => .42f + M.Smooth(1, -4, y) * .18f);
        Kit.Fog(trees, y => Fogged(y), y => .45f);
        c.Over(rock);
        c.Over(stoa);
        c.Over(temples);
        c.Over(trees);
        // Sun warmth over the right terrace, then the cloud sea swallowing the foundations.
        Kit.Glow(c, SunX, SunY, 5, SunGlow, .18f, 1, 4);
        Sea(c, -11, 4.8f, 1.1f, 2.4f, 16, FarCloud.Fogged(SkyStops[1].c, .2f), 61, .25f);
        Sea(c, -11, 2.6f, .8f, 1.8f, 20, FarCloud.Shift(.06f), 63, .25f);
        return new PaintedLayer(s, c);
    }

    /// <summary>A close Doric column: twenty flutes with lit arrises, an echinus and abacus capital; broken shafts end in a ragged cut.</summary>
    static void Column(Canvas p, float cx, float baseY, float h, bool broken, int seed, float r = .34f)
    {
        var f = p.F;
        p.Paint(cx - r * 1.7f, cx + r * 1.7f, baseY - .05f, baseY + h + .7f, (x, y, i, j) =>
        {
            float dx = x - cx, t = y - baseY;
            float top = broken ? h - (dx + r) * .55f + f.N(x, y, .2f, seed) * .22f : h;
            if (t > top + (broken ? 0 : .55f)) return Col.Clear;
            if (!broken && t > h)
            {
                float k = (t - h) / .55f;
                float w = k > .7f ? r * 1.52f : r * (1 + MathF.Pow(k / .7f, 2.2f) * .44f);
                if (MathF.Abs(dx) > w) return Col.Clear;
                float u = dx / w;
                float v = k > .7f ? .74f + (k > .92f ? .14f : 0) - u * .14f : Kit.Cylinder(u, .22f) * .82f + (k < .1f ? -.14f : 0);
                return Marble.At(v, i, j, .35f);
            }
            float uu = dx / r; if (MathF.Abs(uu) > 1) return Col.Clear;
            float ang = MathF.Asin(uu), fl = MathF.Cos(ang * 10);
            float v2 = Kit.Cylinder(uu, .2f) * .78f + fl * .075f + f.Fbm(x, y, .5f, 2, seed + 1) * .04f;
            if (fl > .9f) v2 += .07f;
            if (broken && top - t < .08f) v2 -= .18f;
            if (f.Ridge(x, y, 1.2f, 2, seed + 3) > .945f) v2 -= .12f;
            return Kit.Grain(Marble.At(v2, i, j, .35f), i, j, seed + 5, .04f, .1f);
        });
    }

    /// <summary>Architrave, gold taenia, triglyph frieze and cornice between x0 and x1; the right end may be broken.</summary>
    static void Entablature(Canvas p, float x0, float x1, float y0, bool brokenRight, int seed)
    {
        var f = p.F; float h = .95f;
        p.Paint(x0 - .1f, x1 + .3f, y0, y0 + h, (x, y, i, j) =>
        {
            float right = brokenRight ? x1 - MathF.Abs(f.N(0, y, .25f, seed)) * .9f : x1;
            if (x < x0 || x > right) return Col.Clear;
            float t = (y - y0) / h;
            if (t < .3f) return Marble.At(.6f + (t < .04f ? -.18f : 0) + f.Fbm(x, y, .6f, 2, seed + 1) * .04f, i, j, .3f);
            if (t < .35f) return Kit.Mix(GoldDark, Gold, .65f + (t > .33f ? .2f : 0), i, j);
            if (t < .8f)
            {
                float tu = M.Mod(x - x0 + .18f, .72f) / .72f;
                if (tu < .36f)
                {
                    float g = M.Fract(tu / .36f * 3);
                    return Marble.At(g < .28f ? .3f : g < .4f ? .5f : .66f, i, j, .3f);
                }
                // Metope with a shield relief.
                float mx = (tu - .68f) / .22f, my = (t - .575f) / .17f, d = mx * mx + my * my;
                float v = .74f + (d < 1 ? (d < .35f ? .14f : .06f) - mx * .08f + my * .06f : 0);
                return Marble.At(v, i, j, .3f);
            }
            if (t < .86f) return Marble.At(.32f, i, j, .3f);
            return Marble.At(t > .95f ? .96f : .82f, i, j, .3f);
        });
    }

    /// <summary>A laurel garland swag hung between two points: leaf pairs alternate along the strand, with gold berries and ribbons.</summary>
    static void Garland(Canvas p, float x0, float x1, float y0, float sag, int seed)
    {
        float len = x1 - x0;
        p.Paint(x0 - .15f, x1 + .15f, y0 - sag - .25f, y0 + .2f, (x, y, i, j) =>
        {
            float t = M.Clamp01((x - x0) / len), yc = y0 - sag * 4 * t * (1 - t);
            float dy = y - yc;
            int cell = (int)MathF.Floor((x - x0) / .15f);
            float lx = (x - x0) - (cell + .5f) * .15f, side = (cell & 1) == 0 ? 1 : -1;
            float ly = dy - side * .075f;
            float ex = lx / .085f, ey = ly / .052f;
            if (x >= x0 - .05f && x <= x1 + .05f && ex * ex + ey * ey < 1)
            {
                float v = .45f + Field.Hash01(cell, seed, 3) * .35f + (ly > 0 ? .1f : -.1f);
                if (MathF.Abs(ey) < .18f && MathF.Abs(ex) < .8f) v -= .15f;
                return Laurel.At(v, i, j, .4f);
            }
            if (MathF.Abs(dy) < .035f && x > x0 && x < x1) return Laurel[1];
            if ((cell % 3) == 1 && MathF.Abs(lx) < .03f && MathF.Abs(dy + side * .14f) < .03f) return Kit.Mix(GoldDark, Gold, .7f, i, j);
            float hook = MathF.Min(MathF.Abs(x - x0), MathF.Abs(x - x1));
            if (hook < .035f && y < y0 && y > y0 - .5f) return Kit.Mix(Col.Hex("8a3028"), Col.Hex("c84a3a"), .6f, i, j);
            return Col.Clear;
        });
    }

    /// <summary>Bronze tripod brazier with a bowl of fire; the flame is painted on <paramref name="fire"/>.</summary>
    static void Brazier(Canvas p, Canvas fire, float cx, float baseY, int seed)
    {
        var f = p.F; float bowlY = baseY + 1.25f;
        p.Paint(cx - .6f, cx + .6f, baseY, bowlY + .25f, (x, y, i, j) =>
        {
            float dx = x - cx, t = (y - baseY) / 1.25f;
            foreach (var leg in new[] { -.42f, .05f, .42f })
            {
                float lx = leg * (1 - t * .75f);
                if (MathF.Abs(dx - lx) < .035f + (t < .08f ? .03f : 0) && y < bowlY) return Bronze.At(.3f + (leg < 0 ? .3f : 0) + (dx - lx) * -2, i, j, .35f);
            }
            if (MathF.Abs(dx) < .05f && y < bowlY && t > .3f) return Bronze.At(.35f, i, j, .35f);
            float by = (y - bowlY) / .4f, bx = dx / .52f;
            if (y <= bowlY + .22f && y > bowlY - .4f && bx * bx + (by + .5f) * (by + .5f) * 1.2f < 1)
            {
                if (y > bowlY + .16f) return Bronze.At(.85f - dx * .3f, i, j, .3f);
                return Bronze.At(Kit.Cylinder(bx, .2f) * .7f + f.N(x, y, .15f, seed) * .12f, i, j, .35f);
            }
            return Col.Clear;
        });
        fire.Paint(cx - .6f, cx + .6f, bowlY + .05f, bowlY + 1.6f, (x, y, i, j) =>
        {
            float t = (y - bowlY - .05f) / 1.4f, dx = x - cx;
            float sway = f.N(0, y * 2, .6f, seed + 7) * .18f * t;
            float hw = .34f * MathF.Pow(1 - t, .75f) * (1 + f.N(x * 2, y * 3, .3f, seed + 9) * .5f);
            float d = MathF.Abs(dx - sway) / Math.Max(hw, .001f);
            if (d > 1 || t > 1) return Col.Clear;
            float core = (1 - d) * (1 - t * .9f);
            var col = core > .55f ? FlameCore : core > .3f ? Col.Lerp(Flame, FlameCore, .35f) : Flame;
            return col.WithA(core > .12f || Dither.Hit(.5f, i, j) ? 1 : 0);
        });
        Kit.Glow(fire, cx, bowlY + .5f, 1.5f, Flame, .4f, 1, 4);
    }

    /// <summary>A black-figure amphora: terracotta body with painted bands and a meander, two handles and a foot.</summary>
    static void Amphora(Canvas p, float cx, float baseY, float scale, int seed)
    {
        var f = p.F;
        var terra = Col.Hex("c0703c"); var terraDark = Col.Hex("8e4a28"); var ink = Col.Hex("261c18");
        p.Paint(cx - .5f * scale, cx + .5f * scale, baseY, baseY + 1.15f * scale, (x, y, i, j) =>
        {
            float dx = (x - cx) / scale, t = (y - baseY) / scale;
            float hw;
            if (t < .08f) hw = .17f;
            else if (t < .16f) hw = .08f + (t - .08f) * 1.2f;
            else if (t < .72f) hw = .19f + MathF.Sin((t - .16f) / .56f * MathF.PI) * .13f;
            else if (t < .96f) hw = .13f - (t - .72f) * .15f;
            else hw = .17f;
            bool handle = t > .55f && t < .9f && MathF.Abs(MathF.Abs(dx) - (hw + .07f + (t > .72f ? 0 : (t - .55f) * .2f))) < .035f;
            if (handle) return ink;
            if (MathF.Abs(dx) > hw) return Col.Clear;
            float u = dx / hw, shade = Kit.Cylinder(u, .25f);
            bool black = t < .16f || (t > .72f && t < .95f) || (t > .2f && t < .26f) || (t > .64f && t < .7f);
            if (t > .27f && t < .34f) black = (M.Mod(dx * 9 + MathF.Floor(t * 60), 2) < 1) ^ (((int)MathF.Floor(t * 60) & 1) == 0);
            if (t > .38f && t < .6f && f.Fbm(x * 3, y * 2, .12f * scale, 2, seed) > .1f) black = true;
            var col = black ? Col.Lerp(ink, Col.Hex("4a3a30"), shade * .5f) : Col.Lerp(terraDark, terra, shade);
            if (u < -.55f && !black) col = Col.Lerp(col, Col.Hex("e8a070"), .3f);
            return col;
        });
    }

    /// <summary>A gold medallion bearing an eagle with spread wings over a thunderbolt, on an inscribed marble plinth.</summary>
    static void Emblem(Canvas p, float cx, float baseY, int seed)
    {
        var f = p.F; float plinthTop = baseY + 1.6f, my = plinthTop + .62f, R = .62f;
        p.Paint(cx - .8f, cx + .8f, baseY, my + R + .1f, (x, y, i, j) =>
        {
            float dx = x - cx;
            if (y < plinthTop)
            {
                float t = y - baseY, hw = t < .18f ? .72f : t > plinthTop - baseY - .2f ? .66f : .56f;
                if (MathF.Abs(dx) > hw) return Col.Clear;
                float v = .62f + (hw - MathF.Abs(dx) < .05f && dx > 0 ? -.2f : 0) + (t > plinthTop - baseY - .04f || (t > .14f && t < .18f) ? .25f : 0);
                if (t > .45f && t < 1.15f && MathF.Abs(dx) < .4f && Field.Hash01((int)MathF.Floor(x * 14), (int)MathF.Floor(y * 14), seed) > .55f) v -= .14f;
                return Marble.At(v + f.Fbm(x, y, .5f, 2, seed + 1) * .04f, i, j, .35f);
            }
            float ex = dx, ey = y - my, d = MathF.Sqrt(ex * ex + ey * ey);
            if (d > R) return Col.Clear;
            if (d > R - .06f) return Kit.Mix(GoldDark, GoldLight, .4f - ex * .5f + ey * .3f, i, j);
            float ax = MathF.Abs(ex);
            bool body = ax < .09f - MathF.Max(0, ey) * .1f && ey > -.3f && ey < .14f;
            bool head = (ex - .06f) * (ex - .06f) + (ey - .2f) * (ey - .2f) < .0056f || (ex > .1f && ex < .17f && MathF.Abs(ey - .19f) < .02f);
            bool wings = ax > .07f && ax < .5f && ey > -.08f + ax * .25f - MathF.Abs(MathF.Sin(ax * 40)) * .04f && ey < .12f + ax * .55f && ey < .36f;
            bool tail = ax < .12f + (-.3f - ey) * .4f && ey < -.28f && ey > -.42f;
            bool bolt = MathF.Abs(ex - MathF.Sin(ey * 30) * .04f) < .025f && ey < -.2f && ey > -.5f;
            float v2 = .55f + Kit.Lambert(-ex / R, -ey / R, .8f) * .35f;
            if (body || head || wings || tail) return Bronze.At(.25f + (ex < 0 ? .12f : 0) + (wings ? MathF.Abs(MathF.Sin(ax * 40)) * .1f : 0), i, j, .3f);
            if (bolt) return GoldLight;
            return Kit.Mix(Gold, GoldLight, v2, i, j, 5);
        });
    }

    PaintedLayer Mid()
    {
        var s = new LayerSpec("mid", .65f, 96, 36, -9.5f, 22, -95) { Under = CloudRamp[3] };
        var c = s.NewCanvas(); var f = c.F;
        // Marble terrace: two steps and a stylobate whose face dissolves into the clouds below.
        var stone = c.Blank();
        const float TerraceTop = -3.1f;
        stone.Paint(0, c.Period, -5.8f, TerraceTop + .05f, (x, y, i, j) =>
        {
            float t = TerraceTop - y;
            float v = t < .04f ? .95f : t < .34f ? .7f : t < .38f ? .92f : t < .7f ? .62f : .5f;
            if (t > .7f)
            {
                bool mortar = Kit.Masonry(f, x, y, 1.4f, .36f, 1.4f / 96, 71, out float sh, out _, out _, out _);
                v += (sh - .5f) * .08f; if (mortar) v -= .12f;
            }
            v += f.Fbm(x, y, .7f, 2, 73) * .04f;
            return Marble.At(v, i, j, .35f);
        });
        foreach (var (cx, h, broken) in new[] { (3.2f, 8.5f, false), (5.1f, 8.5f, false), (11.3f, 3.6f, true), (12.9f, 2.4f, true), (18.9f, 8.5f, false), (20.8f, 8.5f, false), (22.7f, 8.5f, false), (28.9f, 8.5f, false), (30.8f, 6.3f, true) })
            Column(stone, cx, TerraceTop, h, broken, (int)(cx * 31));
        // A fallen drum lying on the terrace.
        stone.Paint(14.0f, 15.6f, TerraceTop, TerraceTop + .62f, (x, y, i, j) =>
        {
            float dx = x - 14.8f, dy = y - TerraceTop - .31f;
            if (dx * dx / .55f + dy * dy / .09f > 1) return Col.Clear;
            float fl = MathF.Cos(dy / .3f * 8);
            return Marble.At(.45f + Kit.Lambert(0, dy / .3f, .7f) * .35f + fl * .06f, i, j, .35f);
        });
        float entY = TerraceTop + 8.5f + .55f;
        Entablature(stone, 2.75f, 7.4f, entY, true, 77);
        Entablature(stone, 18.45f, 23.25f, entY, false, 79);
        // Pediment fragment over the left pair, broken to the right.
        stone.Paint(2.6f, 7.6f, entY + .95f, entY + 2.6f, (x, y, i, j) =>
        {
            float t = y - entY - .95f, right = 6.4f - t * .5f + f.N(0, y, .22f, 81) * .3f;
            if (x > right) return Col.Clear;
            float roof = 1.62f - MathF.Abs(x - 5.5f) * .36f;
            if (t > roof) return Col.Clear;
            if (t > roof - .04f) return Kit.Mix(GoldDark, GoldLight, .5f, i, j);
            if (t > roof - .22f) return Marble.At(.8f, i, j, .3f);
            float fig = f.Fbm(x * 1.4f, y, .5f, 2, 83);
            return Marble.At(.4f + (fig > .1f && t < roof - .4f && t > .08f ? .22f + fig * .3f : 0), i, j, .35f);
        });
        Kit.Edge(stone, -1, 0, 1, (col, d, i, j) => Col.Lerp(col, Col.Hex("fff4dc"), .22f));
        Kit.Edge(stone, 1, 0, 1, (col, d, i, j) => Col.Lerp(col, Col.Hex("8a7c8c"), .18f));
        Garland(stone, 3.2f, 5.1f, entY - .35f, .5f, 101);
        Garland(stone, 18.9f, 20.8f, entY - .35f, .55f, 103);
        Garland(stone, 20.8f, 22.7f, entY - .35f, .55f, 105);
        Garland(stone, 28.9f, 30.8f, TerraceTop + 5.9f, .5f, 107);

        var props = c.Blank(); var fire = c.Blank();
        Brazier(props, fire, 8.2f, TerraceTop, 91);
        Brazier(props, fire, 26.4f, TerraceTop, 93);
        Amphora(props, 14.1f, TerraceTop, 1.15f, 95);
        Amphora(props, 13.4f, TerraceTop, .8f, 96);
        Amphora(props, 33.0f, TerraceTop, 1.1f, 97);
        Emblem(props, 16.4f, TerraceTop, 99);
        // Fog the play band toward the warm sky so characters read; the spectacle stays above.
        Kit.Fog(stone, y => Col.Lerp(Fogged(y), Col.Hex("f4dcc4"), .4f), y => .16f + M.Smooth(1.5f, -3.5f, y) * .14f + M.Smooth(-3.5f, -7, y) * .5f);
        Kit.Fog(props, y => Col.Lerp(Fogged(y), Col.Hex("f4dcc4"), .4f), y => .14f);
        Sea(c, -9.5f, 5.4f, 1.2f, 2.6f, 16, CloudRamp.Fogged(Fogged(-4), .3f), 111, .22f);
        c.Over(stone);
        c.Over(props);
        c.Over(fire);
        Kit.Light(c, 8.2f, TerraceTop + 1.7f, 1.8f, Flame, .35f);
        Kit.Light(c, 26.4f, TerraceTop + 1.7f, 1.8f, Flame, .35f);
        Sea(c, -9.5f, 3.6f, 1, 2.2f, 18, CloudRamp, 113, .25f);
        Kit.Glow(c, 20, 10, 6, SunGlow, .1f, .5f, 3);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Near()
    {
        var s = new LayerSpec("near", .40f, 96, 40, -8.5f, 23, -94) { Under = CloudRamp[2] };
        var c = s.NewCanvas();
        // Bright cloud sea below the walkway, lit from the upper left with lavender shadows, and overhead wisps.
        Sea(c, -8.5f, 3.3f, 1.2f, 2.5f, 18, CloudRamp.Shift(-.04f), 401, .28f);
        Sea(c, -8.5f, 1.8f, .8f, 1.8f, 20, CloudRamp.Shift(-.1f), 403, .22f);
        Kit.Glow(c, 10, -4.6f, 9, SunGlow, .12f, .4f, 3);
        Kit.Glow(c, 30, -4.6f, 9, SunGlow, .12f, .4f, 3);
        foreach (var (wx, wy, w) in new[] { (6f, 9.6f, 5f), (24f, 10.3f, 7f), (35f, 9.3f, 3.5f) })
            Kit.CloudBank(c, wx - w, wx + w, wy, 1.2f, .5f, 1.2f, 14, CloudRamp.Shift(.02f), (int)(wx * 17), .25f, .9f);
        return new PaintedLayer(s, c);
    }
}
