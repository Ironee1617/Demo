namespace BackdropPainter;

/// <summary>Sky palace of the storms: a colossal aqueduct and slender towers in indigo mist, floating
/// islands carrying gold-inlaid marble ruins, a pale moon and layered luminous cloud banks.</summary>
public sealed class CloudTheme : ThemePainter
{
    public override string Id => "cloud";
    public override bool OpenBelow => true;

    static readonly Ramp CloudRamp = new("4c5084", "5e6296", "7476a8", "8e8eb8", "aaa8c8", "c6c0d8", "ddd6e4", "efe9ee", "fdf8f6");
    static readonly Ramp FarCloud = new("4a5084", "5c6294", "7276a4", "8c8eb6", "a8a8c8", "c4c2d8");
    static readonly Ramp Marble = new("3a3a5a", "4e4e6e", "66668a", "8484a2", "a4a2ba", "c4c0d0", "e2dee6", "f6f2f2");
    static readonly Ramp IsleRock = new("23243e", "30324e", "3e4060", "4e5274", "626888", "7a809c");
    static readonly Col Gold = Col.Hex("dcbc78"), GoldDark = Col.Hex("8a7048"), Moon = Col.Hex("f2ecdc");
    static readonly (float y, Col c)[] SkyStops = Kit.Stops((-11, "9a9cc4"), (-5, "8486b2"), (-1, "6a6e9e"), (3, "4e5486"), (7, "363c6c"), (10, "262c58"));

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
        Kit.Stars(c, 4, 10, .002f, Col.Hex("e8e4ff"), 7, (x, y) => M.Smooth(4, 8, y) * .8f);
        // A pale moon with a soft halo.
        Kit.Glow(c, 20, 6.4f, 4.5f, Col.Hex("c8c4e8"), .4f);
        c.Paint(18.5f, 21.5f, 4.9f, 7.9f, (x, y, i, j) =>
        {
            float dx = x - 20, dy = y - 6.4f, d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > 1.05f) return Col.Clear;
            float crater = f.Fbm(x, y, .35f, 3, 9);
            return Kit.Mix(Col.Hex("cfc8d8"), Moon, .6f - dx * .4f + dy * .2f - (crater > .3f ? .25f : 0), i, j);
        });
        // High cirrus streaks.
        c.Paint(0, c.Period, 2, 10, (x, y, i, j) =>
        {
            float n = f.Fbm2(x, y, 6, .5f, 4, 11);
            float a = M.Smooth(.25f, .6f, n) * .35f;
            float q = MathF.Floor(a * 5 + Dither.Bayer(i, j)) / 5;
            return q <= 0 ? Col.Clear : Col.Hex("9a98c8").WithA(q);
        });
        // Distant cloud strata below the horizon.
        Kit.CloudBank(c, 0, 30, -11, 4.5f, .9f, 2.2f, 30, FarCloud.Fogged(SkyStops[1].c, .35f), 13, .2f);
        Kit.CloudBank(c, 0, 30, -11, 2.4f, .7f, 1.6f, 34, FarCloud.Fogged(SkyStops[0].c, .2f), 15, .25f);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Far()
    {
        var s = new LayerSpec("far", .85f, 64, 32, -11, 21, -96) { Under = FarCloud[3] };
        var c = s.NewCanvas(); var f = c.F;
        // A colossal two-tier aqueduct with slender domed towers.
        var style = new ArcadeStyle
        {
            Span = 3.2f, PierHalf = .42f, Base = -8, Spring = .6f, Pointed = 0, Rings = 2, RingW = .13f, PlinthH = .5f, CapH = .3f,
            Deck = 3, CorniceH = .25f, Corbel = .28f, GalleryTop = 4.7f, GallerySpan = 1.6f, GalleryHalf = .44f, GallerySpring = 4f, Oculus = false,
            Missing = (x, y) =>
            {
                float bx = MathF.Floor(x / .4f) * .4f, by = MathF.Floor(y / .2f) * .2f;
                float gap = MathF.Abs(f.Dx(bx, 27.5f));
                return gap < 3.2f && by > -6 + gap * .9f + Field.Hash01((int)(bx * 7), (int)(by * 7), 31) * .6f;
            }
        };
        var arch = c.Blank();
        arch.Paint(0, c.Period, -8, 9, (x, y, i, j) =>
        {
            foreach (var (tx, top) in new[] { (5.4f, 7.6f), (17f, 6.2f) })
            {
                float dx = MathF.Abs(x - tx);
                if (y < top && dx < .55f) return Col.White;
                if (y >= top && dx < .6f && (dx * dx) / .36f + (y - top) * (y - top) / .7f < 1) return Col.White;
                if (y > top + .7f && y < top + 1.6f && dx < .06f) return Col.White;
            }
            return ArchKit.Hit(style, f, x, y).Part != Part.None ? Col.White : Col.Clear;
        });
        arch.Post((i, j, col) =>
        {
            float x = arch.X(i), y = arch.Y(j);
            var h = ArchKit.Hit(style, f, x, y);
            V3 n = h.Part == Part.None ? new V3(0, 0, 1) : h.N;
            foreach (var tx in new[] { 5.4f, 17f }) if (MathF.Abs(x - tx) < .6f && (h.Part == Part.None)) { float u = (x - tx) / .6f; n = new V3(u, 0, MathF.Sqrt(Math.Max(0, 1 - u * u))); }
            float v = (h.Part == Part.None ? .5f : ArchKit.BaseTone(h)) * .4f + Kit.Lambert(n.X, n.Y, n.Z) * .55f;
            var col0 = Marble.At(v, i, j, .4f);
            if (h.Part is Part.Cornice or Part.Capital) col0 = Kit.Mix(col0, Gold, .5f, i, j, 3);
            return col0;
        });
        Kit.Fog(arch, y => Kit.Smooth(SkyStops, y), y => .55f + M.Smooth(1, -6, y) * .3f);
        c.Over(arch);
        // Floating rocks drift beneath and around the aqueduct.
        var isles = c.Blank();
        foreach (var (ix, iy, w) in new[] { (11.5f, 5.2f, 1.6f), (24.5f, 1.4f, 2.2f), (30.5f, 6.6f, 1f) })
            isles.Paint(ix - w, ix + w, iy - w * 1.6f, iy + .3f, (x, y, i, j) =>
            {
                float dx = MathF.Abs(x - ix) / w, top = iy + f.N(x, 0, .4f, 33) * .05f;
                float bottom = iy - w * 1.5f * (1 - dx) * (1 - dx) - f.N(x, y, .3f, 35) * .15f;
                return dx < 1 && y < top && y > bottom ? Col.White : Col.Clear;
            });
        Kit.Inflate(isles, 14, (i, j, src, h, n) =>
        {
            int up = isles.Depth(i, j, 0, 1, 3);
            if (up < 3) return Col.Hex("8aa0a8");
            var col = IsleRock.At(.2f + Kit.Lambert(n.X, n.Y, n.Z) * .6f, i, j, .4f);
            return isles.Depth(i, j, -1, 0, 2) < 2 ? Col.Lerp(col, Col.Hex("b8b8dc"), .4f) : col;
        });
        Kit.Fog(isles, y => Kit.Smooth(SkyStops, y), y => .5f);
        c.Over(isles);
        // Cloud banks swallow the aqueduct's foundations.
        Kit.CloudBank(c, 0, 32, -11, 5.5f, 1.1f, 2.4f, 34, FarCloud, 37, .25f);
        Kit.CloudBank(c, 0, 32, -11, 2.6f, .8f, 1.8f, 40, FarCloud.Shift(.08f), 39, .25f);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Mid()
    {
        var s = new LayerSpec("mid", .65f, 96, 36, -9.5f, 22, -95) { Under = CloudRamp[3] };
        var c = s.NewCanvas(); var f = c.F;
        var isle = c.Blank();
        // Two floating islands with paved tops and tapering rocky roots.
        var islands = new[] { (cx: 7.5f, top: 1.2f, w: 4.2f), (cx: 25.5f, top: 4.6f, w: 3.4f) };
        foreach (var (cx, top, w) in islands)
            isle.Paint(cx - w - .2f, cx + w + .2f, top - w * 1.9f, top + .1f, (x, y, i, j) =>
            {
                float dx = MathF.Abs(x - cx) / w;
                float bottom = top - w * 1.8f * MathF.Pow(1 - dx, 1.6f) - f.Fbm(x, y, .4f, 3, 201) * .25f - .25f;
                float edge = top - MathF.Max(0, dx - .9f) * 2;
                return dx < 1 && y < edge && y > bottom ? Col.White : Col.Clear;
            });
        Kit.Inflate(isle, 40, (i, j, src, h, n) =>
        {
            float x = isle.X(i), y = isle.Y(j);
            int up = isle.Depth(i, j, 0, 1, 14);
            if (up < 3) return Marble.At(up == 0 ? .95f : .7f, i, j, .3f);
            if (up < 14) return Kit.Mix(Col.Hex("4a5a60"), Col.Hex("6a8070"), 1 - up / 14f, i, j);
            float strata = M.Fract((y + f.Fbm(x, y, 2, 3, 203) * .4f) / .5f) < .1f ? -.12f : 0;
            return Kit.Grain(IsleRock.At(.15f + Kit.Lambert(n.X, n.Y, n.Z) * .62f + strata + f.Fbm(x, y, .5f, 3, 205) * .1f, i, j, .4f), i, j, 207, .05f, .15f);
        }, (i, j) => f.Fbm(isle.X(i), isle.Y(j), 1, 3, 209) * 14);
        // Hanging roots under the islands.
        var rng = new Rng(211);
        foreach (var (cx, top, w) in islands)
            for (int k = 0; k < 7; k++)
            {
                float rx = cx + rng.R(-w * .7f, w * .7f), dx = MathF.Abs(rx - cx) / w;
                Kit.Vine(isle, rx, top - w * 1.8f * MathF.Pow(1 - dx, 1.6f) - .1f, rng.R(.6f, 2.2f), Col.Hex("3a4a52"), new Ramp("2a3a40", "3a5048", "4e6a52", "6a8a62", "8aa874"), 300 + k, .1f, 1.5f);
            }
        // Marble ruins: gold-banded columns, a pediment fragment and an obelisk.
        var ruins = c.Blank();
        void Column(float cx, float baseY, float h, bool broken, int seed)
        {
            ruins.Paint(cx - .5f, cx + .5f, baseY, baseY + h + .4f, (x, y, i, j) =>
            {
                float dx = x - cx, r = .27f, t = y - baseY;
                float top = broken ? h - (dx + r) * .7f + f.N(x, y, .15f, seed) * .15f : h;
                if (t > top + (broken ? 0 : .3f)) return Col.Clear;
                if (t < .2f) return MathF.Abs(dx) < .4f ? Marble.At(.6f + (t > .14f ? .2f : 0) - dx, i, j, .3f) : Col.Clear;
                if (!broken && t > h) { if (MathF.Abs(dx) > .42f) return Col.Clear; return t > h + .2f ? Marble.At(.75f - dx * .5f, i, j, .3f) : Kit.Mix(GoldDark, Gold, .6f - dx, i, j); }
                float u = dx / r; if (MathF.Abs(u) > 1) return Col.Clear;
                float flute = MathF.Cos(u * MathF.PI * 3) * .06f;
                var col = Marble.At(Kit.Cylinder(u, .2f) * .82f + flute, i, j, .35f);
                if (MathF.Abs(t - h * .25f) < .05f || MathF.Abs(t - h * .75f) < .05f) col = Kit.Mix(GoldDark, Gold, Kit.Cylinder(u), i, j);
                return col;
            });
        }
        Column(5.2f, 1.2f, 3.6f, false, 213); Column(6.6f, 1.2f, 3.6f, false, 215); Column(8.6f, 1.2f, 2.2f, true, 217); Column(10.1f, 1.2f, 1.2f, true, 219);
        // Entablature across the first pair, broken at the right.
        ruins.Paint(4.6f, 7.8f, 5.1f, 6.4f, (x, y, i, j) =>
        {
            float right = 7.2f + f.N(0, y, .2f, 221) * .2f;
            if (x > right) return Col.Clear;
            if (y < 5.35f) return Marble.At(.55f, i, j, .3f);
            if (y < 5.5f) return Kit.Mix(GoldDark, Gold, .7f, i, j);
            float peak = 6.3f - MathF.Abs(x - 5.9f) * .45f;
            return y < peak ? Marble.At(y > peak - .07f ? .9f : .62f - (x - 5.9f) * .1f, i, j, .3f) : Col.Clear;
        });
        // An obelisk on the high island.
        ruins.Paint(24.6f, 26.4f, 4.6f, 9.2f, (x, y, i, j) =>
        {
            float dx = x - 25.5f, t = (y - 4.6f) / 4.3f;
            float hw = .38f * (1 - t * .45f);
            if (t > 1) { float tip = (y - 8.9f) / .45f; if (tip > 1 || MathF.Abs(dx) > hw * (1 - tip)) return Col.Clear; return Kit.Mix(GoldDark, Gold, .5f - dx * 2, i, j); }
            if (MathF.Abs(dx) > hw) return Col.Clear;
            var col = Marble.At(dx < 0 ? .72f : .45f, i, j, .3f);
            if (MathF.Abs(dx) < hw * .45f && M.Mod(y, .38f) < .08f && t < .8f) col = Kit.Mix(col, Gold, .5f, i, j, 2);
            return col;
        });
        Kit.Edge(ruins, -1, 0, 1, (col, d, i, j) => Col.Lerp(col, Col.White, .2f));
        Kit.Fog(isle, y => Col.Hex("5a6094"), y => .1f);
        Kit.Fog(ruins, y => Col.Hex("5a6094"), y => .08f);
        // Cloud layers: a luminous bank behind the islands and the cloud sea below.
        Kit.CloudBank(c, 0, 36, -9.5f, 6, 1.2f, 2.6f, 32, CloudRamp.Fogged(Col.Hex("6a6e9e"), .25f), 223, .22f);
        c.Over(isle);
        c.Over(ruins);
        Kit.CloudBank(c, 0, 36, -9.5f, 3.4f, 1, 2.2f, 36, CloudRamp, 225, .25f);
        Kit.Glow(c, 7.5f, 3, 4, Col.Hex("e8d8b8"), .12f);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Near()
    {
        var s = new LayerSpec("near", .40f, 96, 40, -8.5f, 23, -94) { Under = CloudRamp[2] };
        var c = s.NewCanvas();
        // Rolling cloud sea in the foreground below the walkway, and wisps drifting overhead.
        Kit.CloudBank(c, 0, 40, -8.5f, 1.8f, 1.1f, 2.2f, 34, CloudRamp.Shift(-.12f), 401, .28f);
        Kit.CloudBank(c, 0, 40, -8.5f, 1f, .8f, 1.6f, 38, CloudRamp.Shift(-.2f), 403, .2f);
        foreach (var (wx, wy, w) in new[] { (6f, 11.2f, 5f), (24f, 12.2f, 7f), (35f, 10.8f, 3.5f) })
            Kit.CloudBank(c, wx - w, wx + w, wy, 1.2f, .5f, 1.2f, 14, CloudRamp.Shift(-.06f), (int)(wx * 17), .25f, .9f);
        return new PaintedLayer(s, c);
    }
}
