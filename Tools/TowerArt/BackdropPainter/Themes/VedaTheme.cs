namespace BackdropPainter;

/// <summary>Vedic sacred realm at saffron dusk: a vast low sun ringed by a faint mandala above golden
/// Meru, a temple skyline of shikharas and gopurams over the Ganga ghats with floating diyas, a courtyard
/// of lotus-capital pillars, elephant statues, a yajna fire, a banyan with aerial roots and marigold
/// garlands, all under carved temple eaves hung with bells and oil lamps.</summary>
public sealed class VedaTheme : ThemePainter
{
    public override string Id => "veda";

    const float W = -1.2f;    // Ganga surface in the far plane
    const float Plaza = .6f;  // top of the ghats, where the temples stand
    const float Floor = -2.5f; // courtyard pavement in the mid plane

    static readonly Ramp Sandstone = new("2e1418", "4a2224", "68342c", "884a36", "a86646", "c4845a", "dca472", "f0c48c");
    static readonly Ramp FarStone = new("3e1c34", "582a44", "743a52", "905060", "ac6a6c", "c88678", "e0a488");
    static readonly Ramp Meru = new("5a2a48", "80404e", "a86050", "cc8a56", "e8b460", "ffd880");
    static readonly Ramp Leaf = new("182820", "223c2a", "2e5232", "3e683a", "548244", "6e9c50", "8cb460");
    static readonly Ramp Bark = new("221212", "382220", "4e3428", "664834", "7e5e40", "987850");
    static readonly Ramp Eave = new("0e0812", "180c1a", "241426", "321c34", "442842", "583852", "705068");
    static readonly Ramp Brass = new("4a3010", "7a5420", "b08430", "e0b858", "fff0a0");
    static readonly Ramp Fire = new("b02c18", "ff6a24", "ffc24a", "fff2bc");
    static readonly Col Gold = Col.Hex("f4c65c"), Saffron = Col.Hex("ff9a2e"), Lamp = Col.Hex("ffcf6a"), Dusk = Col.Hex("7a4068");
    static readonly (float y, Col c)[] SkyStops = Kit.Stops((-11, "ffd882"), (-4.5f, "ffb850"), (-1, "fa9240"), (1.5f, "e2665a"), (3.5f, "bc4878"), (6, "7e3680"), (8.5f, "4a2a6a"), (10, "261c48"));
    static readonly (float y, Col c)[] WaterStops = Kit.Stops((-11, "0c2e3a"), (-6, "14505a"), (-3, "1e7478"), (W - .5f, "2e9290"), (W, "5cb0a4"));

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return Sky();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    /// <summary>Carved relief motifs in a cell grid: rosettes, dancers, vine scrolls, lotus buds and pots.</summary>
    static bool Motif(float x, float y, float cw, float ch, int seed)
    {
        int gx = (int)MathF.Floor(x / cw), gy = (int)MathF.Floor(y / ch);
        float u = M.Fract(x / cw) * 2 - 1, v = M.Fract(y / ch) * 2 - 1;
        int kind = (int)(Field.Hash01(gx, gy, seed) * 5);
        float r2 = u * u + v * v;
        switch (kind)
        {
            case 0: { float pr = .55f + .18f * MathF.Cos(MathF.Atan2(v, u) * 8); return r2 < pr * pr && r2 > .06f; }
            case 1: return (u * u) / .16f + (v - .45f) * (v - .45f) / .12f < 1 || (MathF.Abs(u) < .32f - (v + .6f) * .1f && v > -.65f && v < .2f);
            case 2: return MathF.Abs(v - MathF.Sin(u * 4.5f) * .35f) < .14f && MathF.Abs(u) < .8f;
            case 3: return (u * u) / .22f + (v + .1f) * (v + .1f) / .45f < 1 && v < .75f - MathF.Abs(u) * 1.4f;
            default: return (MathF.Abs(u) < .36f + .18f * MathF.Sin((v + 1) * 2.2f) && v > -.7f && v < .4f) || (MathF.Abs(u) < .2f && v >= .4f && v < .7f);
        }
    }

    PaintedLayer Sky()
    {
        var s = new LayerSpec("sky", .95f, 48, 30, -11, 21, -97) { Under = SkyStops[0].c, Over = SkyStops[^1].c };
        var c = s.NewCanvas(); var f = c.F;
        c.PaintAll((x, y, i, j) => Kit.Gradient(SkyStops, y, 4, 1, i, j));
        // Thin cirrus lit from below, rose against the violet.
        c.Paint(0, c.Period, 1, 9.5f, (x, y, i, j) =>
        {
            float n = f.Fbm2(x, y, 7, .45f, 4, 11);
            float a = M.Smooth(.28f, .62f, n) * .4f * M.Smooth(1, 2.5f, y) * M.Smooth(9.5f, 7.5f, y);
            float q = MathF.Floor(a * 5 + Dither.Bayer(i, j)) / 5;
            return q <= 0 ? Col.Clear : Col.Lerp(Col.Hex("ff9a6a"), Col.Hex("c86a9a"), M.InvLerp(1, 8, y)).WithA(q);
        });
        Kit.Stars(c, 4, 10, .0018f, Col.Hex("fff4dc"), 7, (x, y) => M.Smooth(4, 8.5f, y) * .9f);
        // The sun: a huge low disc with a soft corona and a faint mandala of lotus-petal rings.
        float sx = 17, sy = 1.0f, sr = 2.6f;
        Kit.Glow(c, sx, sy, 7f, Col.Hex("ffb850"), .3f, .8f, 6);
        c.Paint(sx - 6.5f, sx + 6.5f, sy - 6.5f, sy + 6.5f, (x, y, i, j) =>
        {
            float dx = f.Dx(x, sx), dy = y - sy, d = MathF.Sqrt(dx * dx + dy * dy), ang = MathF.Atan2(dy, dx);
            if (d < sr) return Kit.Mix(Col.Hex("ffd264"), Col.Hex("fff8d4"), 1 - d / sr * .85f, i, j, 5);
            float a = 0;
            foreach (var (r, n, k) in new[] { (sr + .3f, 16, .28f), (sr + .85f, 24, .2f), (sr + 1.5f, 32, .14f), (sr + 2.3f, 40, .1f) })
            {
                float petal = r + MathF.Abs(MathF.Cos(ang * n * .5f)) * .2f;
                if (MathF.Abs(d - petal) < .04f) a = Math.Max(a, k);
                if (MathF.Abs(d - r + .1f) < .025f) a = Math.Max(a, k * .55f);
            }
            if (a <= 0) return Col.Clear;
            float q = MathF.Floor(a * 4 + Dither.Bayer(i, j)) / 4;
            return q <= 0 ? Col.Clear : Col.Hex("ffe4a0").WithA(q);
        });
        // A distant temple city along the horizon, barely darker than the haze.
        c.Paint(0, c.Period, -5, 1, (x, y, i, j) =>
        {
            float sp = f.Snap(.9f); int n = (int)MathF.Round(c.Period / sp), k = M.Mod((int)MathF.Floor(x / sp), n);
            float u = MathF.Abs(M.Mod(x, sp) / sp * 2 - 1), h = Field.Hash01(k, 1, 23);
            float top = -2.9f + (h > .6f ? (h - .6f) * 6 : h * .4f);
            float t = M.Clamp01((y + 3.6f) / (top + 3.6f));
            if (y > top || u > .95f * MathF.Pow(1 - t, h > .6f ? .5f : .15f)) return Col.Clear;
            return Col.Lerp(Kit.Gradient(SkyStops, y, 4, 1, i, j), Col.Hex("5a2a48"), .38f);
        });
        // Mount Meru: a golden stepped mountain with lesser peaks at its feet, far off in the haze.
        var meru = c.Blank();
        meru.Paint(-1.5f, 11.5f, -5, 6.5f, (x, y, i, j) =>
        {
            Col col = Col.Clear;
            foreach (var (mx, top, half, tiers) in new[] { (.6f, 1.6f, 1.6f, 3), (8.6f, 2.2f, 1.7f, 4), (2.6f, .4f, 1.3f, 2), (6.8f, .8f, 1.3f, 2), (4.5f, 5.6f, 3.8f, 8) })
            {
                float baseY = -3.6f, dx = x - mx, t = (y - baseY) / (top - baseY);
                if (t < 0 || t > 1) continue;
                float k = MathF.Floor(t * tiers), hw = half * (1 - k / tiers) * (1 + f.N(x, y, .5f, 19) * .07f);
                if (MathF.Abs(dx) > hw) continue;
                float local = M.Fract(t * tiers);
                float v = dx > hw * .12f ? .74f : dx > -hw * .12f ? .56f : .3f;
                v += f.Fbm(x, y, .4f, 2, 21) * .08f;
                if (local > .9f) v += .25f;
                col = Meru.At(v, i, j, .4f);
            }
            return col;
        });
        Kit.Fog(meru, y => Kit.Smooth(SkyStops, y), y => .5f + M.Smooth(2, -3, y) * .22f);
        c.Over(meru);
        return new PaintedLayer(s, c);
    }

    /// <summary>Nagara shikhara: a sanctum wall, a curvilinear tower of relief bands, an amalaka disc and a kalasha.
    /// Returns a tone in [0,1] or -1 outside; gold marks the sunlit finial parts.</summary>
    static float Shikhara(float dx, float y, float baseY, float top, float half, out bool gold)
    {
        gold = false;
        float h = top - baseY, t = (y - baseY) / h, ax = MathF.Abs(dx);
        if (t < 0 || t > 1) return -1;
        if (t > .93f)
        {
            float kt = (t - .93f) / .07f, kr = half * .2f * (kt < .55f ? .55f + .75f * MathF.Sin(kt / .55f * MathF.PI * .5f + .3f) : (1 - kt) * 1.5f + .08f);
            if (ax > kr) return -1;
            gold = true; return .62f + Kit.Cylinder(dx / kr, .3f) * .3f;
        }
        if (t > .86f)
        {
            float ar = half * .4f * (t > .915f ? .8f : 1);
            if (ax > ar) return -1;
            gold = true;
            float rib = M.Fract(dx / (ar * .22f) + .5f);
            return .5f + (rib < .5f ? .18f : -.04f) + Kit.Cylinder(dx / ar, .2f) * .2f;
        }
        if (t < .24f)
        {
            // Sanctum wall with pilasters, a doorway and a cornice.
            float ww = half * 1.06f;
            if (ax > ww) return -1;
            float v = .44f + Kit.Cylinder(dx / ww * .6f, .3f) * .25f;
            if (t > .21f) v += .2f; else if (t > .19f) v -= .1f;
            if (M.Fract(ax / (ww * .34f)) < .18f && t < .19f) v += .08f;
            if (ax < half * .16f && t < .16f) v = .12f;
            return v;
        }
        float bt = (t - .24f) / .62f;
        float hw = half * (MathF.Pow(1 - MathF.Pow(bt, 2.3f), .55f) * .92f + .08f);
        if (ax > hw) return -1;
        float u = dx / hw, v2 = .4f + Kit.Cylinder(u, .25f) * .35f;
        float band = M.Fract(bt * 13);
        if (band > .84f) v2 += .14f; else if (band > .74f) v2 -= .12f;
        if (MathF.Abs(MathF.Sin(u * MathF.PI * 3.5f)) > .85f) v2 += .06f;
        if (ax < hw * .18f) v2 += .07f;
        // Miniature spires clinging to the lower body.
        if (bt < .45f && ax > hw * .5f && ax < hw * .98f && band < .6f) v2 += M.Fract(ax / (hw * .24f)) < .5f ? .06f : -.06f;
        return v2;
    }

    /// <summary>Dravidian gopuram: a tiered trapezoid with projecting cornices and rows of niches, topped by a barrel vault with kalashas.</summary>
    static float Gopuram(float dx, float y, float baseY, float top, float half, int tiers, out bool gold)
    {
        gold = false;
        float h = top - baseY, t = (y - baseY) / h, ax = MathF.Abs(dx);
        if (t < 0 || t > 1) return -1;
        if (t > .86f)
        {
            float ct = (t - .86f) / .14f, cw = half * .36f;
            if (ct > .74f)
            {
                float pw = cw * .95f; if (ax > pw) return -1;
                float pot = M.Fract(dx / (pw * .4f) + .5f), pt = (ct - .74f) / .26f;
                float pr = pt < .6f ? .34f : (1 - pt) * .6f;
                if (MathF.Abs(pot - .5f) > pr) return -1;
                gold = true; return .68f + (pot < .5f ? .12f : -.08f);
            }
            if (ax > cw * MathF.Sqrt(1 - ct * ct * .55f)) return -1;
            return .48f + (dx < -cw * .3f ? .16f : dx > cw * .3f ? -.1f : .02f) + (ct > .55f ? .14f : ct < .12f ? -.1f : 0);
        }
        float taper = 1 - t / .86f * .6f;
        float tierT = t / .86f * tiers, lt = M.Fract(tierT);
        float hw = half * taper * (lt > .8f ? 1.1f : 1);
        if (ax > hw) return -1;
        float v = .42f + (dx < -hw * .3f ? .14f : dx > hw * .3f ? -.09f : .02f);
        if (lt > .8f) v += lt > .92f ? .2f : -.12f;
        else
        {
            float cell = M.Fract(dx / (half * .2f) + .5f), cv = (lt - .14f) / .5f;
            bool niche = cell > .3f && cell < .7f && cv > 0 && cv < 1 && (cv < .68f || (cell - .5f) * (cell - .5f) / .04f + (cv - .68f) * (cv - .68f) / .1f < 1);
            if (niche) v -= .17f; else if (lt < .1f && cell > .2f && cell < .8f) v += .07f;
        }
        return v;
    }

    /// <summary>A domed shrine: an arcaded hall, a drum, a bulbous dome and a gold finial.</summary>
    static float Dome(float dx, float y, float baseY, float wallTop, float r, out bool gold)
    {
        gold = false;
        float ax = MathF.Abs(dx);
        if (y < baseY) return -1;
        if (y < wallTop)
        {
            if (ax > r * 1.18f) return -1;
            float v = .42f + (dx < 0 ? .12f : -.06f);
            if (y > wallTop - .14f) v += .18f; else if (y > wallTop - .2f) v -= .1f;
            float cell = M.Fract(dx / (r * .5f) + .5f), oy = (y - baseY) / (wallTop - baseY);
            if (cell > .32f && cell < .68f && oy < .6f && (oy < .42f || (cell - .5f) * (cell - .5f) / .03f + (oy - .42f) * (oy - .42f) / .03f < 1)) v -= .22f;
            return v;
        }
        float dy = y - wallTop;
        if (dy < .2f) { if (ax > r * .9f) return -1; return .5f + (dx < 0 ? .1f : -.08f) + (M.Fract(dx / (r * .3f)) < .15f ? -.1f : 0); }
        float cy = wallTop + .05f, dd = y - cy, rr = r * 1.05f;
        if (dd < rr)
        {
            float hw = r * MathF.Sqrt(Math.Max(0, 1 - (dd / rr) * (dd / rr)));
            if (ax > hw) return -1;
            float nx = dx / r, ny = dd / rr, nz = MathF.Sqrt(Math.Max(0, 1 - nx * nx - ny * ny));
            float v = .22f + Kit.Lambert(nx, ny, nz) * .62f;
            if (MathF.Abs(MathF.Sin(MathF.Atan2(dx, nz * r + .01f) * 5)) > .9f) v -= .08f;
            return v;
        }
        float ft = dd - rr;
        if (ft < .42f && ax < .05f + (ft < .14f ? .08f : ft > .3f ? 0 : .03f)) { gold = true; return .7f; }
        return -1;
    }

    /// <summary>Ripple highlights on open water, warm with the dusk near the surface line.</summary>
    static void Ripples(Canvas c, float surface, float reach, int seed, float strength)
    {
        var f = c.F;
        c.Paint(0, c.Period, surface - reach, surface, (x, y, i, j) =>
        {
            float d = surface - y;
            float n = f.Fbm2(x, y, .8f + d * .25f, .04f + d * .02f, 2, seed);
            float a = M.Smooth(.4f, .62f, n) * strength * (1 - M.Clamp01(d / reach));
            if (a < .12f) return Col.Clear;
            return Col.Lerp(Col.Hex("ffc878"), Col.Hex("b8ecdc"), M.Clamp01(d / reach * 1.5f)).WithA(Math.Min(1, a));
        });
        c.Paint(0, c.Period, surface - 2 / c.Ppu, surface, (x, y, i, j) => Col.Hex("ffd890").WithA(.55f));
    }

    /// <summary>A floating diya: a tiny leaf boat carrying a flame, with its glow and a shimmering reflection.</summary>
    static void Diya(Canvas c, float x, float y, float scale, int seed)
    {
        float px = 1 / c.Ppu;
        c.Paint(x - .12f * scale, x + .12f * scale, y - .03f, y + .12f * scale, (qx, qy, i, j) =>
        {
            float dx = (qx - x) / scale, dy = (qy - y) / scale;
            if (dy < 0 && dy > -.03f && MathF.Abs(dx) < .1f + dy * 2) return Col.Hex("3a1c18");
            if (dy >= 0 && dy < .1f && MathF.Abs(dx) < .035f * (1 - dy / .1f) + .005f) return dy < .05f ? Col.Hex("fff4c0") : Col.Hex("ffc040");
            return Col.Clear;
        });
        Kit.Glow(c, x, y + .03f * scale, .55f * scale, Col.Hex("ffb040"), .7f, .7f, 3);
        c.Paint(x - .05f * scale, x + .05f * scale, y - .9f * scale, y - .03f, (qx, qy, i, j) =>
        {
            float d = (y - qy) / scale, wob = c.F.N2(qx, qy, .4f, .06f, seed) * .03f;
            if (MathF.Abs(qx - x - wob) > .018f * scale + px * .5f) return Col.Clear;
            float a = (1 - d / .9f) * .75f;
            float q = MathF.Floor(a * 4 + Dither.Bayer(i, j)) / 4;
            return q <= 0 ? Col.Clear : Col.Hex("ffc860").WithA(q);
        });
    }

    PaintedLayer Far()
    {
        var s = new LayerSpec("far", .85f, 64, 32, -11, 21, -96) { Under = WaterStops[0].c, Over = SkyStops[^1].c };
        var c = s.NewCanvas(); var f = c.F;
        c.Paint(0, c.Period, -11, W, (x, y, i, j) => Kit.Gradient(WaterStops, y, 4, 1, i, j));

        var town = c.Blank();
        // Ghats: flights of steps down to the river, broken by terrace walls and small chhatri pavilions.
        float sec = f.Snap(5.33f);
        int secs = (int)MathF.Round(c.Period / sec);
        town.Paint(0, c.Period, W, Plaza + .02f, (x, y, i, j) =>
        {
            int k = M.Mod((int)MathF.Floor(x / sec), secs);
            float hs = Field.Hash01(k, 2, 41), sx = M.Mod(x, sec), v;
            if (hs > .72f)
            {
                bool mortar = Kit.Masonry(f, x, y, .8f, .26f, 1.4f / 64, 43, out float sh, out _, out _, out _);
                v = .46f + (sh - .5f) * .1f - (mortar ? .12f : 0);
                float cell = M.Fract(x / .6f), ny = (y - W) / (Plaza - W);
                if (cell > .3f && cell < .7f && ny > .5f && ny < .85f && (ny < .75f || (cell - .5f) * (cell - .5f) / .04f + (ny - .75f) * (ny - .75f) / .01f < 1)) v -= .2f;
                if (y > Plaza - .1f) v += .2f;
            }
            else
            {
                float off = hs * .1f, ly = M.Fract((y - W + off) / .2f), row = MathF.Floor((y - W + off) / .2f);
                v = .42f + (ly > .7f ? .22f : ly < .14f ? -.12f : 0);
                if (M.Mod(x + row * .41f, .9f) < .03f) v -= .1f;
                if (hs < .3f && sx > sec * .28f && sx < sec * .72f && y > W + .72f && y < W + .9f) v = .6f;
            }
            return FarStone.At(v + f.Fbm(x, y, .6f, 2, 45) * .06f, i, j, .45f);
        });
        // Chhatris on the landings: four posts, a shallow dome and a gold finial.
        for (int k = 0; k < secs; k++)
        {
            if (Field.Hash01(k, 2, 41) >= .3f) continue;
            float cx = (k + .5f) * sec, cy = W + .9f;
            town.Paint(cx - .7f, cx + .7f, cy, cy + 1.4f, (x, y, i, j) =>
            {
                float dx = x - cx, ax = MathF.Abs(dx), t = y - cy;
                if (t < .6f) return ax < .62f && (MathF.Abs(ax - .5f) < .04f || MathF.Abs(ax - .17f) < .035f) ? FarStone.At(.45f, i, j, .4f) : Col.Clear;
                if (t < .72f) return ax < .68f ? FarStone.At(t > .68f ? .7f : .4f, i, j, .4f) : Col.Clear;
                float dd = t - .72f, hw = .62f * MathF.Sqrt(Math.Max(0, 1 - (dd / .42f) * (dd / .42f)));
                if (dd < .42f) return ax < hw ? FarStone.At(.35f + Kit.Lambert(dx / .62f, dd / .42f, .6f) * .5f, i, j, .4f) : Col.Clear;
                return ax < .04f && dd < .58f ? Gold : Col.Clear;
            });
        }
        // The temple skyline along the plaza; later entries stand in front.
        var skyline = new (int kind, float x, float top, float half)[]
        {
            (2, 0.4f, 2.1f, 1.1f), (2, 17.9f, 2.0f, .9f), (2, 25.9f, 1.9f, .8f), (2, 7.6f, 2.3f, 1.2f),
            (0, 3.2f, 7.4f, 1.05f), (1, 11.4f, 6.3f, 1.5f), (3, 15.4f, 3.2f, .95f),
            (0, 20.2f, 8.5f, 1.3f), (0, 23.6f, 5.1f, .8f), (1, 28.2f, 7.1f, 1.7f), (3, 31.2f, 2.8f, .7f),
        };
        town.Paint(0, c.Period, Plaza - .05f, c.Top, (x, y, i, j) =>
        {
            float tone = -1; bool gold = false;
            foreach (var b in skyline)
            {
                float dx = f.Dx(x, b.x), v; bool g = false;
                switch (b.kind)
                {
                    case 0: v = Shikhara(dx, y, Plaza, b.top, b.half, out g); break;
                    case 1: v = Gopuram(dx, y, Plaza, b.top, b.half, 5, out g); break;
                    case 3: v = Dome(dx, y, Plaza, Plaza + (b.top - Plaza) * .42f, b.half, out g); break;
                    default:
                        {
                            // Mandapa: a low pillared hall with a heavy cornice.
                            float ax = MathF.Abs(dx), t = y - Plaza, h = b.top - Plaza;
                            if (ax > b.half || t > h) { v = -1; break; }
                            v = .44f + (dx < 0 ? .1f : -.06f);
                            if (t > h - .22f) v += .2f; else if (t > h - .3f) v -= .1f;
                            else if (t > .15f && t < h - .45f && M.Fract(ax / .36f) > .4f) v -= .22f;
                            break;
                        }
                }
                if (v < 0) continue;
                tone = v; gold = g;
            }
            if (tone < 0) return Col.Clear;
            return gold ? Kit.Mix(Col.Hex("a86a28"), Gold, tone, i, j, 3) : FarStone.At(tone + f.Fbm(x, y, .5f, 2, 47) * .05f, i, j, .45f);
        });
        // Saffron pennants on the tallest spires.
        foreach (var b in skyline)
        {
            if (b.kind == 2 || b.kind == 3) continue;
            float px = b.x, py = b.top;
            town.Paint(px - .05f, px + .6f, py - .02f, py + .75f, (x, y, i, j) =>
            {
                float dx = x - px, dy = y - py;
                if (MathF.Abs(dx) < .02f && dy < .7f) return Col.Hex("5a2a20");
                float t = dx / .5f;
                if (t > 0 && t < 1 && dy > .35f + t * .1f + MathF.Sin(t * 6) * .03f && dy < .68f - t * .2f + MathF.Sin(t * 6 + 1) * .03f) return t < .5f ? Saffron : Col.Hex("ff7a1c");
                return Col.Clear;
            });
        }
        Kit.Fog(town, y => Kit.Smooth(SkyStops, y), y => .4f + M.Smooth(3, -1.2f, y) * .16f - M.Smooth(5, 8, y) * .14f);
        c.Over(town);
        Kit.Reflect(c, W, 3.4f, Col.Hex("1e6a70"), .5f, 49);
        // Floating diyas drift down the river, nearer ones larger.
        var rng = new Rng(51);
        for (int k = 0; k < 44; k++)
        {
            float d = rng.R(.05f, 2.8f), x = rng.R(0, 32);
            Diya(c, x, W - d, .6f + d * .3f, 500 + k);
        }
        Ripples(c, W, 3.5f, 53, .5f);
        return new PaintedLayer(s, c);
    }

    /// <summary>A hanging aerial root: a thin twisting strand; those reaching the ground thicken into prop roots.</summary>
    static void Root(Canvas p, float x, float top, float length, float thick, float ground, int seed)
    {
        var f = p.F; bool reach = top - length <= ground + .05f;
        if (reach) length = top - ground;
        p.Paint(x - .5f, x + .5f, top - length - .05f, top, (px, py, i, j) =>
        {
            float t = (top - py) / length; if (t < 0 || t > 1) return Col.Clear;
            float cx = x + MathF.Sin(t * 4.2f + seed) * .1f * t + f.N(0, py, 1.5f, seed) * .05f;
            float w = thick * (1 - t * .45f) + (reach ? M.Smooth(.8f, 1, t) * 4 : 0);
            float d = MathF.Abs(px - cx) * p.Ppu;
            if (d < w) return (px - cx) * p.Ppu < -w * .35f ? Bark[4] : Bark[2];
            float cx2 = cx + MathF.Sin(t * 9 + seed * .7f) * .04f;
            if (t > .15f && MathF.Abs(px - cx2) * p.Ppu < w * .5f) return Bark[1];
            return Col.Clear;
        });
    }

    /// <summary>A marigold garland: a catenary chain of orange heads with tufts of mango leaves.</summary>
    static void Garland(Canvas p, float x0, float y0, float x1, float y1, float sag, int seed)
    {
        int n = Math.Max(2, (int)((x1 - x0) / .105f));
        for (int k = 0; k <= n; k++)
        {
            float t = k / (float)n, gx = M.Lerp(x0, x1, t), gy = M.Lerp(y0, y1, t) - MathF.Sin(t * MathF.PI) * sag;
            bool leaf = k % 6 == 3;
            p.Paint(gx - .08f, gx + .08f, gy - .14f, gy + .07f, (x, y, i, j) =>
            {
                float dx = x - gx, dy = y - gy;
                if (leaf) return dy < 0 && dy > -.13f && MathF.Abs(dx) < .012f + (-dy) * .35f ? Leaf.At(.5f + dx * 4 - dy, i, j, .3f) : Col.Clear;
                float d2 = dx * dx + dy * dy; if (d2 > .0033f) return Col.Clear;
                return Kit.Mix(Col.Hex("d04c0c"), Col.Hex("ffb020"), .55f + (dy - dx) * 7, i, j, 3);
            });
        }
    }

    /// <summary>A bronze bell on a chain: crown, flaring body, bright lip and a clapper.</summary>
    static void Bell(Canvas p, float x, float top, float chain, float size)
    {
        p.Paint(x - size * 1.1f, x + size * 1.1f, top - chain - size * 1.75f, top, (px, py, i, j) =>
        {
            float dx = px - x, dy = top - py;
            if (dy < chain) return MathF.Abs(dx) * p.Ppu < .9f ? Col.Hex("5a4028") : Col.Clear;
            float t = (dy - chain) / (size * 1.6f);
            if (t > 1.05f) return Col.Clear;
            float hw = t < .12f ? size * .28f : t < .8f ? size * (.45f + .55f * MathF.Pow(t, 1.6f)) : t < .92f ? size * 1.05f : MathF.Abs(dx) < size * .13f ? size * .13f : -1;
            if (hw < 0 || MathF.Abs(dx) > hw) return Col.Clear;
            float v = Kit.Cylinder(dx / hw, .2f) * .8f + (t > .8f && t < .92f ? .18f : 0) + (t < .12f ? -.1f : 0) + (t > .92f ? -.3f : 0);
            return Brass.At(v, i, j, .4f);
        });
    }

    /// <summary>A hanging brass oil lamp: chains, a shallow dish and a small flame with its glow.</summary>
    static void HangingLamp(Canvas p, float x, float top, float chain)
    {
        float ly = top - chain;
        p.Paint(x - .2f, x + .2f, ly - .13f, top, (px, py, i, j) =>
        {
            float dx = px - x, dy = py - ly;
            if (dy > .16f) return MathF.Abs(dx) * p.Ppu < .9f || (dy < .42f && MathF.Abs(MathF.Abs(dx) - (dy - .16f) * .55f) * p.Ppu < .9f) ? Col.Hex("5a4028") : Col.Clear;
            if (dy >= 0) { float hw = .035f * (1 - dy / .16f) + .006f; return MathF.Abs(dx) < hw ? (dy < .09f ? Col.Hex("fff4c0") : Col.Hex("ffb830")) : Col.Clear; }
            if (dy > -.12f) { float hw = .17f * MathF.Sqrt(Math.Max(0, 1 - (dy / .12f) * (dy / .12f))); return MathF.Abs(dx) < hw ? Brass.At(.4f + Kit.Cylinder(dx / .17f, .2f) * .4f + (dy > -.02f ? .2f : 0), i, j, .4f) : Col.Clear; }
            return Col.Clear;
        });
        Kit.Glow(p, x, ly + .08f, .7f, Lamp, .55f, 1, 4);
    }

    /// <summary>A brass dish lamp on a stone bracket at a pillar's side, with its flame.</summary>
    static void WallLamp(Canvas p, float x, float y, float dir)
    {
        p.Paint(x - .3f, x + .3f, y - .13f, y + .21f, (px, py, i, j) =>
        {
            float dx = (px - x) * dir, dy = py - y;
            if (dx < 0) return Col.Clear;
            if (dy < 0 && dy > -.12f && dx < .22f && dy > -.12f + dx * .45f) return Sandstone.At(.35f, i, j, .3f);
            if (dy >= 0 && dy < .05f && dx < .24f) return Brass.At(.5f + (dy > .03f ? .25f : 0), i, j, .4f);
            if (dy >= .05f && dy < .2f && MathF.Abs(dx - .16f) < .03f * (1 - (dy - .05f) / .15f) + .005f) return dy < .12f ? Col.Hex("fff4c0") : Col.Hex("ffb830");
            return Col.Clear;
        });
        Kit.Glow(p, x + .16f * dir, y + .1f, .6f, Lamp, .5f, 1, 4);
    }

    /// <summary>A lotus on a lily pad: a fan of pink petals around a gold heart, or a closed bud on its stem.</summary>
    static void Lotus(Canvas p, float x, float y, float size, bool bud, int seed)
    {
        p.Paint(x - size * 1.4f, x + size * 1.4f, y - size * .3f, y + size * 1.3f, (px, py, i, j) =>
        {
            float dx = (px - x) / size, dy = (py - y) / size;
            if (dy < .1f && dy > -.22f && dx * dx / 1.7f + (dy + .05f) * (dy + .05f) / .025f < 1 && !(dy > -.05f && MathF.Abs(dx - .6f) < .15f - (dy + .05f))) return Leaf.At(.45f + dy * 2 + dx * .1f, i, j, .3f);
            if (bud)
            {
                if (dx * dx / .09f + (dy - .5f) * (dy - .5f) / .25f < 1 && dy < 1.1f - MathF.Abs(dx) * 2) return Kit.Mix(Col.Hex("d8548a"), Col.Hex("ffc0d8"), .4f + dy * .4f - dx, i, j, 3);
                if (MathF.Abs(dx) < .04f && dy < .3f && dy > 0) return Leaf[3];
                return Col.Clear;
            }
            if (dy < 0) return Col.Clear;
            for (int ring = 0; ring < 2; ring++)
            {
                int n = ring == 0 ? 7 : 5; float len = ring == 0 ? 1.0f : .72f, wid = ring == 0 ? .2f : .17f, spread = ring == 0 ? .72f : .5f;
                for (int k = 0; k < n; k++)
                {
                    float a = MathF.PI * (.5f - spread * .5f) + MathF.PI * spread * (k + .5f) / n, ca = MathF.Cos(a), sa = MathF.Sin(a);
                    float along = dx * ca + dy * sa, across = -dx * sa + dy * ca;
                    if (along > 0 && along < len && MathF.Abs(across) < wid * MathF.Sin(MathF.PI * Math.Min(1, along / len * 1.05f)))
                        return Kit.Mix(Col.Hex("e0609a"), Col.Hex("ffd8e8"), along / len * .8f + ring * .2f, i, j, 3);
                }
            }
            if (dx * dx + (dy - .1f) * (dy - .1f) < .04f) return Gold;
            return Col.Clear;
        });
    }

    /// <summary>A carved sandstone pillar: stepped plinth, square relief panels, a banded round shaft, a pot and a lotus capital with brackets.</summary>
    static void Pillar(Canvas p, float cx, float capY, int seed)
    {
        var f = p.F; float r = .27f;
        p.Paint(cx - .8f, cx + .8f, Floor - .05f, capY + 1.15f, (x, y, i, j) =>
        {
            float dx = x - cx, ax = MathF.Abs(dx), t = y - Floor, v;
            if (t < .6f)
            {
                float pw = t < .24f ? .5f : t < .42f ? .43f : .37f;
                if (ax > pw) return Col.Clear;
                v = .5f - dx * .4f + ((t > .2f && t < .24f) || (t > .38f && t < .42f) || t > .56f ? .2f : 0);
                if (t > .44f && t < .56f && MathF.Abs(MathF.Sin(dx * 22)) > .7f) v += .1f;
            }
            else if (t < 3.0f)
            {
                if (ax > .34f) return Col.Clear;
                v = .52f + (dx < -.2f ? .14f : dx > .22f ? -.18f : 0);
                float py = M.Fract((t - .6f) / 1.2f);
                if (!(ax > .25f || py < .07f || py > .93f)) { v -= .08f; if (Motif(x - cx, y, .5f, .6f, seed)) v += .15f; }
                else if (ax > .25f && ax < .28f) v += .06f;
            }
            else if (t < capY - Floor - .55f)
            {
                if (ax > r) return Col.Clear;
                float u = dx / r;
                v = Kit.Cylinder(u, .18f) * .82f;
                float band = M.Mod(t - 3, 1.1f);
                if (band < .06f) v -= .2f; else if (band < .12f) v += .12f; else if (band > .5f && band < .62f) v += .08f * MathF.Cos(u * 6);
                if (MathF.Abs(MathF.Cos(u * MathF.PI * 4)) > .92f) v -= .06f;
            }
            else if (t < capY - Floor)
            {
                float kt = (t - (capY - Floor - .55f)) / .55f, kw = r * (1 + MathF.Sin(kt * MathF.PI) * .5f);
                if (ax > kw) return Col.Clear;
                v = Kit.Cylinder(dx / kw, .2f) * .8f + (kt > .85f ? .1f : 0);
            }
            else
            {
                float ct = t - (capY - Floor);
                if (ct < .7f)
                {
                    float lw = r * 1.05f + ct / .7f * .4f;
                    float petal = MathF.Abs(MathF.Cos(dx / lw * MathF.PI * 3.5f));
                    if (ax > lw || (ct < .1f && petal < .35f)) return Col.Clear;
                    v = Kit.Cylinder(dx / lw, .2f) * .7f + petal * .22f - .05f;
                }
                else if (ct < .86f) { if (ax > .72f) return Col.Clear; v = .62f - dx * .2f + (ct > .82f ? .15f : 0); }
                else
                {
                    float bt = (ct - .86f) / .29f, arm = .55f - bt * .3f;
                    if (!(MathF.Abs(ax - arm) < .14f + bt * .1f || ax < .22f)) return Col.Clear;
                    v = .48f + (dx < 0 ? .1f : -.08f) + (bt > .8f ? .1f : 0);
                }
            }
            return Kit.Grain(Sandstone.At(v + f.Fbm(x, y, .4f, 2, seed + 1) * .05f, i, j, .4f), i, j, seed + 2, .04f, .12f);
        });
    }

    /// <summary>A stone elephant in profile on a carved plinth, dressed in a red and gold caparison.</summary>
    static void Elephant(Canvas p, float cx, float baseY, float dir, int seed)
    {
        var f = p.F;
        p.Paint(cx - 1.8f, cx + 1.8f, baseY, baseY + .5f, (x, y, i, j) =>
        {
            float dx = x - cx, t = y - baseY, hw = t < .26f ? 1.7f : 1.52f;
            if (MathF.Abs(dx) > hw) return Col.Clear;
            float v = .48f - dx * .05f + ((t > .22f && t < .26f) || t > .46f ? .2f : t < .04f ? -.1f : 0);
            if (t > .28f && t < .44f && Motif(x, y, .3f, .16f, seed)) v += .14f;
            return Sandstone.At(v, i, j, .4f);
        });
        float by = baseY + .5f;
        var body = p.Blank();
        body.Paint(cx - 2.1f, cx + 2.1f, by, by + 2.4f, (x, y, i, j) =>
        {
            float dx = (x - cx) * dir, dy = y - by;
            float bx = dx + .2f, byy = dy - 1.4f;
            bool bodyIn = bx * bx / 1.21f + byy * byy / .5f < 1;
            float hx = dx - 1.0f, hy = dy - 1.5f;
            bool headIn = hx * hx / .25f + hy * hy / .27f < 1;
            float ex = dx - .68f, ey = dy - 1.55f;
            bool earIn = ex * ex / .13f + ey * ey / .21f < 1;
            bool legs = dy >= 0 && dy < 1.1f && (MathF.Abs(dx + .8f) < .2f || MathF.Abs(dx + .3f) < .2f || MathF.Abs(dx - .3f) < .2f || MathF.Abs(dx - .8f) < .2f);
            float td = 9;
            for (int k = 0; k <= 18; k++)
            {
                float t = k / 18f, tx = 1.38f + MathF.Sin(t * 2.4f) * .2f - t * t * .3f, ty = 1.3f - t * 1.22f + (t > .8f ? (t - .8f) * 1.2f : 0);
                float ddx = dx - tx, ddy = dy - ty; td = Math.Min(td, MathF.Sqrt(ddx * ddx + ddy * ddy) - (.17f - t * .09f));
            }
            bool trunk = td < 0;
            bool tusk = dx > 1.2f && dx < 1.72f && MathF.Abs(dy - (.95f + (dx - 1.2f) * .3f)) < .055f;
            if (tusk && !headIn) return Col.Hex("808080");
            if (!(bodyIn || headIn || earIn || legs || trunk)) return Col.Clear;
            if (earIn && !headIn) return Col.Hex("c0c0c0");
            return Col.White;
        });
        Kit.Inflate(body, 10, (i, j, src, h, n) =>
        {
            float x = body.X(i), y = body.Y(j), dx = (x - cx) * dir, dy = y - by;
            if (src.R < .6f) return Col.Hex("f0e6c8");
            float v = .22f + Kit.Lambert(n.X, n.Y, n.Z) * .55f + f.Fbm(x, y, .3f, 2, seed + 3) * .05f;
            if (src.R < .9f) { v -= .08f; if (body.Depth(i, j, (int)dir, 0, 2) < 2 || body.Depth(i, j, 0, 1, 2) < 2) v += .15f; }
            if (dx > 1.1f && dy < 1.1f && M.Fract(dy / .11f) < .3f) v -= .08f;
            float bx = dx + .2f, byy = dy - 1.4f;
            if (bx * bx / 1.21f + byy * byy / .5f < 1 && byy > .1f && MathF.Abs(bx) < .78f)
            {
                bool border = MathF.Abs(bx) > .68f || byy < .18f;
                return border ? Kit.Mix(Col.Hex("a07030"), Gold, v, i, j, 3) : Kit.Mix(Col.Hex("6a1420"), Col.Hex("c03038"), v + (Field.Hash01((int)(bx * 12), (int)(byy * 12), seed) > .85f ? .5f : 0), i, j, 3);
            }
            return Kit.Grain(Sandstone.At(v, i, j, .4f), i, j, seed + 4, .04f, .12f);
        });
        body.Paint(cx - 1.5f, cx + 1.5f, by + 1.4f, by + 2.1f, (x, y, i, j) =>
        {
            float dx = (x - cx) * dir, dy = y - by;
            if ((dx - 1.12f) * (dx - 1.12f) + (dy - 1.62f) * (dy - 1.62f) < .0016f) return Col.Hex("1a0c10");
            if (MathF.Abs(dx - 1.02f) < .05f && dy > 1.72f && dy < 2.0f) return Gold;
            return Col.Clear;
        });
        p.Over(body);
    }

    PaintedLayer Mid()
    {
        var s = new LayerSpec("mid", .65f, 96, 36, -9.5f, 22, -95);
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(221);
        float tankX = 11.8f, tankHalf = 1.9f, tankW = Floor - .35f;
        // Pavement: courses of sandstone with a stepped temple tank cut into it.
        var ground = c.Blank();
        ground.Paint(0, c.Period, -9.5f, Floor, (x, y, i, j) =>
        {
            float tdx = MathF.Abs(x - tankX), depth = Floor - y, step = MathF.Floor(depth / .18f);
            if (depth < 1.6f && tdx < tankHalf - step * .16f) return Col.Clear;
            bool mortar = Kit.Masonry(f, x, y, 1.3f, .4f, 1.5f / 96, 201, out float sh, out _, out _, out _);
            float v = .4f + (sh - .5f) * .1f - (mortar ? .15f : 0) + f.Fbm(x, y, .5f, 3, 203) * .06f;
            if (depth < .05f) v += .25f; else if (depth < .12f) v += .1f;
            if (depth < .4f && tdx < tankHalf + .3f && M.Mod(depth, .18f) < .03f) v += .25f;
            if (depth > .55f && depth < .75f && Motif(x, y, .34f, .2f, 205)) v += .12f;
            v -= M.Smooth(-3.6f, -9.5f, y) * .2f;
            return Kit.Grain(Sandstone.At(v, i, j, .4f), i, j, 207, .04f, .14f);
        });
        ground.Paint(tankX - tankHalf - .1f, tankX + tankHalf + .1f, tankW - 1.3f, tankW, (x, y, i, j) =>
        {
            float d = tankW - y;
            var col = Kit.Mix(Col.Hex("2e8a86"), Col.Hex("123e46"), M.Clamp01(d / 1.2f), i, j, 5);
            float n = f.Fbm2(x, y, .5f, .04f, 2, 209);
            if (d < .5f && M.Smooth(.42f, .6f, n) * (1 - d / .5f) > .3f) col = Col.Hex("ffc070");
            if (d < 2 / 96f) col = Col.Hex("ffd890");
            return col;
        });
        foreach (var (lx, sz, bud) in new[] { (10.4f, .3f, false), (11.2f, .22f, true), (12.1f, .34f, false), (13.0f, .26f, false), (12.6f, .2f, true) })
            Lotus(ground, lx, tankW - .02f, sz, bud, (int)(lx * 7));

        // Elephant statues flank the court.
        var statues = c.Blank();
        Elephant(statues, 5.4f, Floor, 1, 231);
        Elephant(statues, 24.6f, Floor, -1, 233);

        // Pillars with lotus capitals carry a carved architrave across the colonnade.
        var stone = c.Blank();
        float[] pillars = { 2.2f, 8.6f, 15.0f, 21.4f };
        float capY = 5.35f, beamY = 6.5f;
        foreach (var cx in pillars) Pillar(stone, cx, capY, (int)(cx * 13));
        stone.Paint(pillars[0] - .9f, pillars[^1] + .9f, beamY, beamY + 1.25f, (x, y, i, j) =>
        {
            float t = y - beamY, v = .5f;
            if (t > 1.1f) v += .22f; else if (t > 1.0f) v -= .12f;
            else if (t > .3f && t < .95f) { v -= .06f; if (Motif(x, y, .36f, .65f, 211)) v += .16f; }
            else if (t < .08f) v -= .18f; else if (t < .16f) v += .08f;
            return Kit.Grain(Sandstone.At(v + f.Fbm(x, y, .4f, 2, 213) * .04f, i, j, .4f), i, j, 215, .04f, .12f);
        });
        // The yajna altar: three stacked stone tiers with a carved band and a dark fire pit.
        float ax0 = 18.2f, fy = Floor + 1.05f;
        stone.Paint(ax0 - 1.5f, ax0 + 1.5f, Floor, Floor + 1.15f, (x, y, i, j) =>
        {
            float dx = x - ax0, t = y - Floor;
            float hw = t < .4f ? 1.4f : t < .76f ? 1.12f : t < 1.12f ? .86f : -1;
            if (hw < 0 || MathF.Abs(dx) > hw) return Col.Clear;
            float lt = t < .4f ? t / .4f : t < .76f ? (t - .4f) / .36f : (t - .76f) / .36f;
            float v = .46f - dx * .07f + (lt > .88f ? .22f : lt < .1f ? -.12f : 0);
            if (lt > .28f && lt < .72f && Motif(x, y, .26f, .16f, 241)) v += .12f;
            if (t > .98f && MathF.Abs(dx) < .66f) return Col.Hex("1e0c10");
            return Kit.Grain(Sandstone.At(v, i, j, .4f), i, j, 243, .04f, .12f);
        });
        Kit.Light(stone, ax0, fy + .4f, 3.5f, Col.Hex("ff8a30"), .45f);
        foreach (var cx in pillars) { WallLamp(stone, cx + .34f, Floor + 3.25f, 1); Kit.Light(stone, cx + .5f, Floor + 3.4f, 1.3f, Col.Hex("ff9a40"), .4f); }

        // Flames and smoke over the altar.
        var fire = c.Blank();
        fire.Paint(ax0 - 1, ax0 + 1, fy, fy + 2.1f, (x, y, i, j) =>
        {
            float dx = x - ax0, h = y - fy;
            float sway = f.N2(x, y, .6f, .35f, 245) * .2f;
            float env = .6f * MathF.Pow(Math.Max(0, 1 - h / 1.9f), .75f) * (1 + f.N2(x, y, .3f, .22f, 247) * .55f);
            float d = MathF.Abs(dx - sway * h) / Math.Max(env, .01f);
            if (d > 1) return Col.Clear;
            float n = f.Fbm2(x, y * .6f, .25f, .3f, 3, 249) * .5f + .5f;
            float heat = (1 - d) * (1 - h / 1.9f) * 1.25f + n * .45f - .3f;
            return heat < .12f ? Col.Clear : Fire.At(heat, i, j, .35f);
        });
        var smoke = c.Blank();
        smoke.Paint(ax0 - 3.5f, ax0 + 4, fy + 1.2f, 12.5f, (x, y, i, j) =>
        {
            float rise = y - fy - 1.2f, sway = MathF.Sin(rise * .5f + 1) * .5f + rise * .12f, w = .3f + rise * .18f;
            float d = MathF.Abs(x - ax0 - sway) / w;
            float n = f.Fbm(x, y - rise * .15f, .8f, 4, 251) * .5f + .5f;
            float a = ((1 - d) * n * 1.5f - .3f) * M.Smooth(12, 4, y) * M.Smooth(0, 1.2f, rise) * .8f;
            float q = MathF.Floor(a * 5 + Dither.Bayer(i, j)) / 5;
            return q <= 0 ? Col.Clear : Col.Lerp(Col.Hex("9a6a78"), Col.Hex("4e3a5a"), M.InvLerp(0, 8, rise)).WithA(q);
        });

        // The banyan: a buttressed trunk, spreading limbs, a leafy crown and hanging aerial roots.
        float bx0 = 30.6f;
        var tree = c.Blank();
        tree.Paint(bx0 - 5, bx0 + 5.5f, Floor - .1f, 7.5f, (x, y, i, j) =>
        {
            float dx = x - bx0, t = y - Floor;
            float hw = .6f + MathF.Pow(M.Smooth(1.8f, 0, t), 2) * 1.0f + f.N(x, y, .7f, 301) * .08f;
            if (t < 7.2f && MathF.Abs(dx + MathF.Sin(t * .6f) * .15f) < hw) return Col.White;
            foreach (var (ang, len, w) in new[] { (2.6f, 4.6f, .34f), (.5f, 4.8f, .36f), (2.0f, 3.2f, .24f), (1.15f, 3.4f, .26f) })
            {
                float oy = y - 4.4f, along = dx * MathF.Cos(ang) + oy * MathF.Sin(ang), across = -dx * MathF.Sin(ang) + oy * MathF.Cos(ang) + along * along * .05f;
                if (along > 0 && along < len && MathF.Abs(across) < w * (1 - along / len * .6f)) return Col.White;
            }
            return Col.Clear;
        });
        Kit.Inflate(tree, 22, (i, j, src, h, n) =>
        {
            float x = tree.X(i), y = tree.Y(j);
            float ridge = f.Ridge(x * 2.2f + y * .3f, y * .4f, .5f, 3, 303);
            float v = .1f + Kit.Lambert(n.X, n.Y, n.Z) * .6f + (ridge - .55f) * .3f;
            return Kit.Grain(Bark.At(v, i, j, .35f), i, j, 305, .05f, .15f);
        }, (i, j) => f.Fbm(tree.X(i), tree.Y(j), 1, 3, 306) * 12);
        var canopy = c.Blank();
        var dusky = Leaf.Fogged(Dusk, .24f).Shift(-.08f);
        foreach (var (lx, ly, rx, ry) in new[] { (bx0 - 3.6f, 6.9f, 2.0f, 1.2f), (bx0 - 1.4f, 8.3f, 2.1f, 1.3f), (bx0 + 1.0f, 8.8f, 2.2f, 1.4f), (bx0 + 3.4f, 7.5f, 1.9f, 1.2f), (bx0 - 2.2f, 5.9f, 1.6f, .9f), (bx0 + 2.4f, 6.0f, 1.6f, .9f), (bx0 + .2f, 10.3f, 2.4f, 1.2f), (bx0 + 4.8f, 9.4f, 1.5f, 1.0f), (bx0 - 4.9f, 8.6f, 1.4f, 1.0f) })
            Kit.LeafClump(canopy, lx, ly, rx, ry, 260, dusky, (int)(lx * 11 + ly * 3), .32f, .35f);
        var roots = c.Blank();
        for (int k = 0; k < 16; k++)
        {
            float rx = bx0 + rng.R(-4.6f, 5.0f), top = 5.4f + rng.R(0, 1.8f);
            float len = rng.Chance(.3f) ? 9 : rng.R(1.2f, 5.5f);
            Root(roots, rx, top, len, rng.R(1.2f, 2.4f), Floor, 320 + k);
        }

        // Garlands between the pillars, bells under the beam.
        var deco = c.Blank();
        for (int k = 0; k + 1 < pillars.Length; k++) Garland(deco, pillars[k] + .5f, capY + .6f, pillars[k + 1] - .5f, capY + .6f, .75f, 340 + k);
        Garland(deco, pillars[^1] + .6f, capY + .55f, bx0 - 2.6f, 6.3f, .8f, 350);
        foreach (var bx in new[] { 4.3f, 6.5f, 10.7f, 12.9f, 17.1f, 19.3f }) Bell(deco, bx, beamY, .22f, .18f);

        Kit.Fog(ground, y => Dusk, y => .14f + M.Smooth(-3.2f, -8, y) * .35f);
        Kit.Fog(statues, y => Dusk, y => .12f);
        Kit.Fog(stone, y => Dusk, y => .12f - M.Smooth(6, 8, y) * .05f);
        Kit.Fog(tree, y => Dusk, y => .12f);
        Kit.Edge(stone, 1, 0, 1, (col, d, i, j) => Col.Lerp(col, Col.Hex("ffb060"), .2f));
        Kit.Edge(tree, 1, 0, 1, (col, d, i, j) => Col.Lerp(col, Col.Hex("ffb060"), .2f));
        c.Over(ground);
        c.Over(statues);
        c.Over(stone);
        c.Over(tree);
        c.Over(roots);
        c.Over(canopy);
        c.Over(deco);
        c.Over(smoke);
        c.Over(fire);
        Kit.Glow(c, ax0, fy + .5f, 2.4f, Col.Hex("ff9a3a"), .4f, .8f);
        Kit.Glow(c, ax0, fy + .3f, 1f, Col.Hex("ffd27a"), .5f, .7f, 3);
        for (int k = 0; k < 14; k++)
        {
            float ex = ax0 + rng.R(-.9f, 1.2f), ey = fy + rng.R(1.2f, 4.5f);
            c.Set(c.I(ex), c.J(ey), Col.Hex("ffd060"));
            Kit.Glow(c, ex, ey, .18f, Col.Hex("ff9a3a"), .8f, 1, 2);
        }
        return new PaintedLayer(s, c);
    }

    PaintedLayer Near()
    {
        var s = new LayerSpec("near", .40f, 96, 40, 2, 12.5f, -94) { Over = Eave[0] };
        var c = s.NewCanvas(); var f = c.F;
        float eaveY = 10.0f, bs = f.Snap(40 / 12f);
        // Carved temple eaves: a petal fringe under a relief frieze and curved kapota, with corbel brackets.
        var eave = c.Blank();
        eave.Paint(0, c.Period, 8.4f, c.Top, (x, y, i, j) =>
        {
            if (y > eaveY) return Col.White;
            // Lotus-petal fringe: pointed petals hanging from the lower string course.
            float sp = f.Snap(.55f), u = MathF.Abs(M.Mod(x, sp) / sp * 2 - 1), d = eaveY - y;
            if (d < .32f * MathF.Pow(1 - u, 1.4f)) return Col.White;
            // Stepped corbel brackets ending in a lotus bud.
            float bu = M.Mod(x + bs * .5f, bs) - bs * .5f;
            float bw = d < .3f ? .36f : d < .55f ? .28f : d < .8f ? .2f : -1;
            if (bw > 0 && MathF.Abs(bu) < bw) return Col.White;
            float ky = y - (eaveY - .96f);
            if (bu * bu / .04f + ky * ky / .032f < 1) return Col.White;
            return Col.Clear;
        });
        Kit.Inflate(eave, 9, (i, j, src, h, n) =>
        {
            float x = eave.X(i), y = eave.Y(j);
            float v = .14f + Kit.Lambert(n.X, n.Y, n.Z) * .5f + f.Fbm(x, y, .5f, 3, 403) * .08f;
            if (y > eaveY + 1.9f) return Eave[0];
            if (y > eaveY + 1.5f) v = .18f + (M.Fract((y - eaveY) / .2f) < .3f ? .12f : 0);
            else if (y > eaveY + 1.05f)
            {
                float kt = (y - eaveY - 1.05f) / .45f;
                v = .2f + MathF.Sin(kt * MathF.PI) * .35f;
                float cell = M.Fract(x / .62f) * 2 - 1, e = cell * cell + (kt - .3f) * (kt - .3f) * 3;
                if (e < .55f && e > .3f) v += .16f;
            }
            else if (y > eaveY + .35f)
            {
                float ft = (y - eaveY - .35f) / .7f;
                if (ft < .08f || ft > .92f) v += .18f;
                else { v -= .06f; if (Motif(x, y, .5f, .7f, 405)) v += .2f; }
            }
            else if (y > eaveY) v = .3f + (M.Fract((y - eaveY) / .12f) < .4f ? .12f : 0);
            else
            {
                float sp = f.Snap(.55f), u = MathF.Abs(M.Mod(x, sp) / sp * 2 - 1), d = eaveY - y;
                if (u < .14f && d > .04f) v += .14f;
                float bu = MathF.Abs(M.Mod(x + bs * .5f, bs) - bs * .5f);
                if (bu < .4f && (MathF.Abs(d - .3f) < .02f || MathF.Abs(d - .55f) < .02f || MathF.Abs(d - .8f) < .02f)) v += .16f;
                if (bu < .04f && d > .85f) v += .1f;
            }
            return Kit.Grain(Eave.At(v, i, j, .4f), i, j, 407, .04f, .14f);
        });
        Kit.Edge(eave, 0, -1, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("d88a4a"), d == 0 ? .5f : .22f));
        // Bells at the bracket tips, garlands between them and oil lamps on long chains.
        var hang = c.Blank();
        int brackets = (int)MathF.Round(c.Period / bs);
        for (int k = 0; k < brackets; k++)
        {
            float bx = k * bs;
            Bell(hang, bx, eaveY - 1.12f, .22f, .2f);
            Garland(hang, bx + .3f, eaveY - .5f, bx + bs - .3f, eaveY - .5f, .95f, 420 + k);
        }
        var lamps = new[] { (1.7f, 1.9f), (9.1f, 1.5f), (15.2f, 2.3f), (22.6f, 1.7f), (30.4f, 2.1f), (36.1f, 1.4f) };
        foreach (var (lx, chain) in lamps) HangingLamp(hang, lx, eaveY - .3f, chain);
        c.Over(eave);
        c.Over(hang);
        foreach (var (lx, chain) in lamps) Kit.Light(c, lx, eaveY - .3f - chain + .1f, 1.6f, Col.Hex("ff9a40"), .4f);
        return new PaintedLayer(s, c);
    }
}
