namespace BackdropPainter;

/// <summary>Reusable pixel-art painting primitives shared by the seven theme painters.</summary>
public static class Kit
{
    /// <summary>Warm key light from the upper left, matching the creature art.</summary>
    public static readonly V3 Key = new V3(-.62f, .52f, .58f).N();

    public static (float y, Col c)[] Stops(params (float y, string hex)[] s) => s.Select(v => (v.y, Col.Hex(v.hex))).OrderBy(v => v.y).ToArray();

    /// <summary>Vertical gradient quantised into bands per segment with ordered dither at band edges.</summary>
    public static Col Gradient((float y, Col c)[] s, float y, int bands, float dither, int i, int j)
    {
        if (y <= s[0].y) return s[0].c;
        if (y >= s[^1].y) return s[^1].c;
        int k = 0; while (y > s[k + 1].y) k++;
        float t = (y - s[k].y) / (s[k + 1].y - s[k].y);
        float q = MathF.Floor(t * bands + (Dither.Bayer(i, j) - .5f) * dither + .5f) / bands;
        return Col.Lerp(s[k].c, s[k + 1].c, q);
    }

    /// <summary>Continuous (unquantised) colour of a gradient, used as a fog target.</summary>
    public static Col Smooth((float y, Col c)[] s, float y)
    {
        if (y <= s[0].y) return s[0].c;
        if (y >= s[^1].y) return s[^1].c;
        int k = 0; while (y > s[k + 1].y) k++;
        return Col.Lerp(s[k].c, s[k + 1].c, (y - s[k].y) / (s[k + 1].y - s[k].y));
    }

    public static Col Mix(Col a, Col b, float t, int i, int j, int steps = 4)
    {
        float q = MathF.Floor(M.Clamp01(t) * steps + Dither.Bayer(i, j)) / steps;
        return Col.Lerp(a, b, Math.Min(1, q));
    }

    /// <summary>Per-texel grain: a stable sparse scatter of lighter and darker specks.</summary>
    public static Col Grain(Col c, int i, int j, int seed, float density, float amount)
    {
        float h = Field.Hash01(i, j, seed);
        if (h < density * .5f) return Col.Lerp(c, Col.Black, amount).WithA(c.A);
        if (h > 1 - density * .5f) return Col.Lerp(c, Col.White, amount * .7f).WithA(c.A);
        return c;
    }

    public static float Lambert(float nx, float ny, float nz) => Math.Max(0, nx * Key.X + ny * Key.Y + nz * Key.Z);

    /// <summary>Cylinder lighting across a horizontal offset u in [-1,1].</summary>
    public static float Cylinder(float u, float ambient = .18f)
    {
        u = Math.Clamp(u, -1, 1); float nz = MathF.Sqrt(1 - u * u);
        return ambient + (1 - ambient) * Lambert(u, 0, nz);
    }

    /// <summary>Running-bond masonry; returns mortar flag, per-block value and edge proximity.</summary>
    public static bool Masonry(Field f, float x, float y, float bw, float bh, float mortar, int seed, out float shade, out float lx, out float ly, out uint id)
    {
        bw = f.Snap(bw);
        int row = (int)MathF.Floor(y / bh);
        float offset = (row & 1) * bw * .5f + (Field.Hash01(row, 7, seed) - .5f) * bw * .3f;
        int count = Math.Max(1, (int)MathF.Round(f.Period / bw));
        int col = (int)MathF.Floor((x + offset) / bw);
        lx = x + offset - col * bw; ly = y - row * bh;
        id = Field.Hash(M.Mod(col, count), row, seed);
        shade = (id & 255) / 255f;
        return lx < mortar || ly < mortar;
    }

    /// <summary>Gothic or round arcade opening test. u is the offset from the opening centre.</summary>
    public static bool InOpening(float u, float y, float half, float spring, float bottom, float pointed)
    {
        if (y < bottom || MathF.Abs(u) >= half) return false;
        if (y < spring) return true;
        float e = pointed * half * 2, r = half + e, dy = y - spring;
        return (u - e) * (u - e) + dy * dy < r * r && (u + e) * (u + e) + dy * dy < r * r;
    }
    public static float ApexHeight(float half, float pointed) { float e = pointed * half * 2; return MathF.Sqrt(half * half + 2 * half * e); }

    /// <summary>Tapered spike (stalactite, stalagmite, icicle) with a noisy rim. dir=-1 hangs down.</summary>
    public static bool Spike(Field f, float x, float y, float bx, float by, float length, float width, int dir, int seed, out float u, out float t, float jag = .18f, float power = 1.1f)
    {
        t = (y - by) * dir / length; u = 0;
        if (t < 0 || t > 1) return false;
        float n = f.N2(x, y, .35f, .45f, seed) * jag;
        float hw = width * .5f * MathF.Pow(1 - t, power) * (1 + n);
        float dx = x - bx;
        if (MathF.Abs(dx) >= hw || hw <= 0) return false;
        u = dx / hw; return true;
    }

    /// <summary>Hexagonal crystal prism with a pointed tip. Returns facet 0 lit, 1 face, 2 shade, 3 ridge.</summary>
    public static int Crystal(float x, float y, float bx, float by, float angle, float length, float halfWidth, out float along)
    {
        float s = MathF.Sin(angle), c = MathF.Cos(angle);
        float px = x - bx, py = y - by;
        along = px * s + py * c; float across = px * c - py * s;
        if (along < -halfWidth * .3f || along > length) return -1;
        float tip = halfWidth * 1.6f, limit = along > length - tip ? halfWidth * (length - along) / tip : halfWidth;
        if (MathF.Abs(across) > limit) return -1;
        float k = across / Math.Max(limit, 1e-4f);
        if (MathF.Abs(k + .05f) < .09f && along > length - tip * 1.4f) return 3;
        return k < -.32f ? 0 : k > .42f ? 2 : 1;
    }

    /// <summary>Draws a filled soft disc of additive light with stepped rings.</summary>
    public static void Glow(Canvas c, float x, float y, float r, Col col, float strength, float squash = 1, int steps = 5)
        => c.Glow(x, y, r, r * squash, col, strength, steps);

    /// <summary>Applies colour to texels within <paramref name="width"/> steps of an exposed edge facing (dx,dy).</summary>
    public static void Edge(Canvas plane, int dx, int dy, int width, Func<Col, int, int, int, Col> f)
    {
        plane.Post((i, j, c) =>
        {
            int d = plane.Depth(i, j, dx, dy, width);
            return d < width ? f(c, d, i, j) : c;
        });
    }

    /// <summary>Blends texels toward a height-dependent fog colour.</summary>
    public static void Fog(Canvas plane, Func<float, Col> fog, Func<float, float> amount, float dither = .8f)
    {
        plane.Post((i, j, c) =>
        {
            float y = plane.Y(j), a = M.Clamp01(amount(y));
            if (a <= 0) return c;
            float q = MathF.Floor(a * 16 + (Dither.Bayer(i, j) - .5f) * dither + .5f) / 16;
            return Col.Lerp(c, fog(y), q).WithA(c.A);
        });
    }

    /// <summary>Scatters single-texel stars or motes with occasional four-point glints.</summary>
    public static void Stars(Canvas c, float y0, float y1, float density, Col col, int seed, Func<float, float, float> fade = null)
    {
        int j0 = Math.Max(0, c.J(y0)), j1 = Math.Min(c.H - 1, c.J(y1));
        for (int j = j0; j <= j1; j++)
            for (int i = 0; i < c.W; i++)
            {
                float h = Field.Hash01(i, j, seed);
                if (h > density) continue;
                float y = c.Y(j), a = fade == null ? 1 : fade(c.X(i), y);
                float b = .45f + Field.Hash01(i, j, seed + 1) * .55f;
                if (a * b < .2f) continue;
                var s = col.WithA(a * b);
                c.Set(i, j, c.Get(i, j).Under(s));
                if (h < density * .09f && a > .6f)
                    foreach (var (ox, oy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        c.Set(i + ox, j + oy, c.Get(i + ox, j + oy).Under(col.WithA(a * b * .5f)));
            }
    }

    /// <summary>Soft diagonal shafts of light with dithered steps.</summary>
    public static void Shafts(Canvas c, float angleDeg, float spacing, float width, Col col, float strength, float y0, float y1, int seed, Func<float, float, float> mask = null)
    {
        float a = angleDeg * MathF.PI / 180, ca = MathF.Cos(a), sa = MathF.Sin(a);
        c.Paint(0, c.Period, y0, y1, (x, y, i, j) =>
        {
            float p = x * ca + y * sa;
            float band = c.F.N2(p, 0, spacing, 1, seed) * .5f + .5f;
            float beam = M.Smooth(1 - width, 1, band);
            float fall = M.Smooth(y0, y0 + (y1 - y0) * .6f, y) * (mask == null ? 1 : mask(x, y));
            float v = beam * fall * strength;
            float q = MathF.Floor(v * 6 + Dither.Bayer(i, j)) / 6;
            return q <= 0 ? Col.Clear : col.WithA(Math.Min(1, q));
        }, add: true);
    }

    /// <summary>
    /// A fan of faceted crystal prisms from a common root. Ramp: 0 outline .. last highlight (6+ entries).
    /// dir=1 grows upward, -1 hangs. Returns the cluster's approximate centre for a glow.
    /// </summary>
    public static (float x, float y) CrystalCluster(Canvas p, float bx, float by, float size, float tilt, float spread, int count, Ramp ramp, int seed, int dir = 1)
    {
        var rng = new Rng(seed);
        var prisms = new List<(float x, float y, float a, float len, float hw)>();
        for (int k = 0; k < count; k++)
        {
            float centre = 1 - MathF.Abs(k - (count - 1) * .5f) / Math.Max(1, count * .5f);
            float a = tilt + (k - (count - 1) * .5f) / Math.Max(1, count - 1) * spread * 2 + rng.R(-.12f, .12f);
            float len = size * (.45f + centre * .55f) * rng.R(.75f, 1.05f);
            prisms.Add((bx + rng.R(-size, size) * .16f, by, (dir < 0 ? MathF.PI : 0) + a * dir, len, len * rng.R(.13f, .19f)));
        }
        // Short side prisms first so the tall centre crystals sit in front.
        foreach (var pr in prisms.OrderBy(q => q.len))
        {
            float reach = pr.len + pr.hw * 2;
            int id = (int)(pr.x * 131 + pr.len * 977);
            p.Paint(pr.x - reach, pr.x + reach, pr.y - reach, pr.y + reach, (x, y, i, j) =>
            {
                int facet = Crystal(x, y, pr.x, pr.y, pr.a, pr.len, pr.hw, out float along);
                if (facet < 0) return Col.Clear;
                if (dir < 0 && (facet == 0 || facet == 2)) facet = 2 - facet;
                float t = M.Clamp01(along / pr.len);
                switch (facet)
                {
                    case 0: return ramp.At(.62f + t * .2f, i, j, .5f);
                    case 2: return ramp.At(.2f + t * .12f, i, j, .5f);
                    case 3: return ramp.Last;
                    default:
                        // Inner fractures read as fine bright lines along the prism.
                        float line = Field.Hash01((int)(MathF.Floor((x * MathF.Cos(pr.a) - y * MathF.Sin(pr.a)) * p.Ppu / 3)), id, seed);
                        return ramp.At(.38f + t * .22f + (line > .86f ? .2f : 0), i, j, .5f);
                }
            });
        }
        return (bx, by + size * .4f * dir);
    }

    /// <summary>
    /// A clump of individually painted leaves inside an ellipse. Back leaves are darker; the clump is lit
    /// from the upper left; each leaf carries a midrib. Ramp runs dark to light (6+ entries).
    /// </summary>
    public static void LeafClump(Canvas p, float cx, float cy, float rx, float ry, int count, Ramp ramp, int seed, float leafLen = .26f, float droop = .5f, float bright = 0)
    {
        var rng = new Rng(seed);
        var leaves = new List<(float x, float y, float a, float len, float z)>();
        for (int k = 0; k < count; k++)
        {
            float ang = rng.R(0, MathF.PI * 2), rad = MathF.Sqrt(rng.F());
            float lx = cx + MathF.Cos(ang) * rad * rx, ly = cy + MathF.Sin(ang) * rad * ry;
            float outward = MathF.Atan2((ly - cy) / ry, (lx - cx) / rx);
            float a = outward + rng.R(-.9f, .9f);
            // Droop pulls leaves toward hanging down.
            a = MathF.Atan2(MathF.Sin(a) - droop, MathF.Cos(a));
            leaves.Add((lx, ly, a, leafLen * rng.R(.7f, 1.35f), rng.F() * .6f + (1 - rad) * .1f + (ly - cy) / ry * .2f));
        }
        foreach (var lf in leaves.OrderBy(l => l.z))
        {
            float ca = MathF.Cos(lf.a), sa = MathF.Sin(lf.a), wid = lf.len * .2f;
            float light = M.Clamp01(.5f + (-(lf.x - cx) / rx * .55f + (lf.y - cy) / ry * .75f) * .5f);
            float baseV = .12f + lf.z * .45f + light * .38f + bright;
            p.Paint(lf.x - lf.len, lf.x + lf.len, lf.y - lf.len, lf.y + lf.len, (x, y, i, j) =>
            {
                float dx = x - lf.x, dy = y - lf.y, along = dx * ca + dy * sa, across = -dx * sa + dy * ca;
                if (along < 0 || along > lf.len) return Col.Clear;
                float t = along / lf.len, half = wid * MathF.Pow(MathF.Sin(MathF.PI * MathF.Min(1, t * 1.1f)), .75f);
                if (MathF.Abs(across) > half) return Col.Clear;
                float v = baseV + (across > 0 ? .08f : -.04f) - t * .05f;
                if (MathF.Abs(across) < .6f / p.Ppu && t < .85f) v += .1f;
                if (MathF.Abs(across) > half - .8f / p.Ppu) v -= .12f;
                return ramp.At(v, i, j, .35f);
            });
        }
    }

    /// <summary>A snow-laden conifer: tiered jagged boughs, each capped with snow on its upper face.</summary>
    public static void Pine(Canvas p, float x, float baseY, float height, Ramp needles, Col snow, Col snowShade, int seed)
    {
        var f = p.F; int tiers = Math.Max(3, (int)(height / .55f));
        float half = height * .32f;
        p.Paint(x - half - .2f, x + half + .2f, baseY - .05f, baseY + height + .1f, (px, py, i, j) =>
        {
            float dx = px - x, t = (py - baseY) / height;
            if (t < 0 || t > 1) return Col.Clear;
            if (MathF.Abs(dx) < height * .025f + .03f && t < .2f) return needles[1];
            // Each tier is a drooping triangle; the envelope narrows toward the crown.
            float tierT = t * tiers, local = M.Fract(tierT), env = half * MathF.Pow(1 - t, .9f);
            float w = env * (.45f + (1 - local) * .7f) * (1 + f.N(px, py, .12f, seed) * .18f);
            float sag = (MathF.Abs(dx) / Math.Max(env, .01f)) * .22f / tiers;
            if (MathF.Abs(dx) > w || t < .12f) return Col.Clear;
            float tierTop = (MathF.Floor(tierT) + 1) / tiers - sag;
            float v = .25f + (dx < 0 ? .25f : 0) + local * .2f + f.N(px * 3, py * 3, .2f, seed + 1) * .15f;
            // Snow rests on the upper part of every tier.
            float snowLine = .55f + f.N(px, py, .15f, seed + 2) * .2f;
            if (local > snowLine && t + sag < tierTop + .02f)
                return dx < -w * .2f || Dither.Hit(.5f, i, j) ? snow : snowShade;
            return needles.At(v, i, j, .4f);
        });
    }

    /// <summary>A leaning palm: ringed trunk and a crown of long drooping fronds with leaflets.</summary>
    public static void Palm(Canvas p, float x, float baseY, float height, float lean, Ramp trunk, Ramp fronds, int seed)
    {
        var rng = new Rng(seed);
        float topX = x + lean * height, topY = baseY + height;
        p.Paint(Math.Min(x, topX) - .4f, Math.Max(x, topX) + .4f, baseY - .05f, topY + .1f, (px, py, i, j) =>
        {
            float t = (py - baseY) / height; if (t < 0 || t > 1) return Col.Clear;
            float cx = x + lean * height * (t * t * .6f + t * .4f), hw = .1f * (1 - t * .35f);
            float u = (px - cx) / hw; if (MathF.Abs(u) > 1) return Col.Clear;
            float ring = M.Fract((py - baseY) / .16f);
            float v = Cylinder(u, .2f) * .8f + (ring < .18f ? -.2f : 0);
            return trunk.At(v, i, j, .4f);
        });
        int count = 9;
        for (int k = 0; k < count; k++)
        {
            float a0 = MathF.PI * (k / (float)(count - 1)) + rng.R(-.15f, .15f);
            float len = height * rng.R(.38f, .5f), droop = rng.R(.9f, 1.5f);
            int id = seed * 31 + k;
            float bright = MathF.Cos(a0) < 0 ? .12f : -.05f;
            p.Paint(topX - len - .3f, topX + len + .3f, topY - len, topY + len * .5f, (px, py, i, j) =>
            {
                // March along the frond's arc and test leaflet coverage.
                float dx = px - topX, dy = py - topY;
                for (int s = 1; s <= 24; s++)
                {
                    float t = s / 24f;
                    float fx = MathF.Cos(a0) * len * t, fy = MathF.Sin(a0) * len * t * .55f - droop * len * t * t * .7f;
                    float rx = dx - fx, ry = dy - fy, d2 = rx * rx + ry * ry;
                    float w = .07f * MathF.Sin(MathF.PI * MathF.Min(1, t * 1.15f)) + .012f;
                    if (d2 < .0006f) return fronds.At(.3f + bright, i, j, .3f);
                    // Leaflets hang below the rib.
                    if (MathF.Abs(rx) < .045f && ry < 0 && ry > -w * 2.6f) return fronds.At(.45f + bright + (rx < 0 ? .12f : 0) + ry * 2, i, j, .4f);
                }
                return Col.Clear;
            });
        }
    }

    /// <summary>Faceted ice: Worley cells act as flat crystal planes with bright edges and inner fractures.</summary>
    public static Col Ice(Field f, float x, float y, int i, int j, V3 n, Ramp ramp, float cell, int seed, float bias = .3f)
    {
        var h = f.Cell(x, y, cell, seed, 2.6f);
        float a = (h.Id & 1023) / 1023f * MathF.PI * 2, tilt = ((h.Id >> 10) & 255) / 255f * .55f;
        var fn = new V3(n.X + MathF.Cos(a) * tilt, n.Y + MathF.Sin(a) * tilt, n.Z).N();
        float v = bias + Lambert(fn.X, fn.Y, fn.Z) * .55f;
        // Only some facet boundaries catch the light; the rest are soft value changes.
        bool bright = ((h.Id >> 18) & 7) < 3;
        if (h.F2 - h.F1 < .04f) v += bright ? .26f : -.05f;
        // Depth: translucent ice darkens away from its surface.
        v += (n.Z - .8f) * -.15f;
        if (f.Ridge(x, y, cell * 2.2f, 2, seed + 3) > .95f) v += .22f;
        return ramp.At(v, i, j, .3f);
    }

    /// <summary>A hanging liana: a swaying strand with small paired leaves.</summary>
    public static void Vine(Canvas p, float x, float top, float length, Col stem, Ramp leaves, int seed, float sway = .18f, float thick = 1.5f)
    {
        var f = p.F;
        p.Paint(x - sway - .4f, x + sway + .4f, top - length - .3f, top, (px, py, i, j) =>
        {
            float t = (top - py) / length; if (t < 0 || t > 1) return Col.Clear;
            float cx = x + MathF.Sin(t * 5.2f + seed) * sway * t + f.N(0, py, 1.2f, seed) * .05f;
            float d = MathF.Abs(px - cx) * p.Ppu;
            if (d < thick) return d < thick * .5f ? Col.Lerp(stem, Col.White, .12f) : stem;
            // Leaves every ~0.22 units alternate sides.
            float k = MathF.Floor((top - py) / .22f), ly = top - (k + .5f) * .22f;
            float side = k % 2 == 0 ? 1 : -1, lx = cx + side * .07f;
            float dx = (px - lx) * side, dy = py - ly;
            if (dx > -.02f && dx < .13f && MathF.Abs(dy + dx * .4f) < .035f * (1 - dx / .13f) + .008f && Field.Hash01((int)k, seed, 3) > .25f)
                return leaves.At(.45f + Field.Hash01((int)k, seed, 5) * .4f - (dy < -dx * .4f ? .15f : 0), i, j, .4f);
            return Col.Clear;
        });
    }

    /// <summary>Coloured point light that only tints covered texels (light spilling onto rock).</summary>
    public static void Light(Canvas p, float x, float y, float r, Col col, float strength, int steps = 5)
    {
        p.Paint(x - r, x + r, y - r, y + r, (px, py, i, j) =>
        {
            var dst = p.P[j * p.W + i];
            if (dst.A <= 0) return Col.Clear;
            float dx = (px - x) / r, dy = (py - y) / r, d = MathF.Sqrt(dx * dx + dy * dy);
            if (d >= 1) return Col.Clear;
            float a = (1 - d) * (1 - d) * strength;
            float q = MathF.Floor(a * steps + Dither.Bayer(i, j)) / steps;
            return q <= 0 ? Col.Clear : col.WithA(Math.Min(1, q));
        }, add: true);
    }

    /// <summary>Mirrors everything above <paramref name="waterY"/> into the water below with ripples.</summary>
    public static void Reflect(Canvas c, float waterY, float depth, Col tint, float keep, int seed)
    {
        var src = (Col[])c.P.Clone();
        int jw = c.J(waterY);
        Parallel.For(Math.Max(0, c.J(waterY - depth)), Math.Max(0, jw), j =>
        {
            float y = c.Y(j), d = waterY - y;
            for (int i = 0; i < c.W; i++)
            {
                float x = c.X(i);
                float wob = c.F.N2(x, y, .6f, .08f + d * .05f, seed) * (.04f + d * .05f);
                int si = c.I(x + wob), sj = c.J(waterY + d * 1.05f);
                if (sj >= c.H) continue;
                var r = src[sj * c.W + M.Mod(si, c.W)];
                if (r.A <= 0) continue;
                float fade = keep * (1 - M.Clamp01(d / depth));
                if (Field.Hash01(i, j, seed) > .2f + fade && fade < .5f) continue;
                var mixed = Col.Lerp(tint, r, fade).WithA(1);
                c.P[j * c.W + i] = c.P[j * c.W + i].Under(mixed.WithA(M.Clamp01(.35f + fade)));
            }
        });
    }

    /// <summary>Chamfer distance (in texels) from every covered texel to the nearest uncovered one.</summary>
    public static float[] Distance(Canvas p, float cap = 4096)
    {
        int w = p.W, h = p.H; var d = new float[w * h];
        for (int k = 0; k < d.Length; k++) d[k] = p.P[k].A > .5f ? cap : 0;
        const float a = 1, b = 1.4142f;
        float At(int i, int j) => j < 0 || j >= h ? 0 : d[j * w + M.Mod(i, w)];
        for (int pass = 0; pass < 2; pass++) // two sweeps settle the wrap seam
        {
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    int k = j * w + i; if (d[k] == 0) continue;
                    float v = d[k];
                    v = Math.Min(v, At(i - 1, j) + a); v = Math.Min(v, At(i, j - 1) + a);
                    v = Math.Min(v, At(i - 1, j - 1) + b); v = Math.Min(v, At(i + 1, j - 1) + b);
                    d[k] = v;
                }
            for (int j = h - 1; j >= 0; j--)
                for (int i = w - 1; i >= 0; i--)
                {
                    int k = j * w + i; if (d[k] == 0) continue;
                    float v = d[k];
                    v = Math.Min(v, At(i + 1, j) + a); v = Math.Min(v, At(i, j + 1) + a);
                    v = Math.Min(v, At(i + 1, j + 1) + b); v = Math.Min(v, At(i - 1, j + 1) + b);
                    d[k] = v;
                }
        }
        return d;
    }

    public delegate Col LitShader(int i, int j, Col src, float height, V3 normal);

    /// <summary>
    /// Inflates a silhouette into a rounded relief: height rises over <paramref name="radius"/> texels
    /// from the edge, plus an optional bump field, and the shader receives the resulting normal.
    /// </summary>
    public static void Inflate(Canvas p, float radius, LitShader shade, Func<int, int, float> bump = null, float bumpScale = 1)
    {
        var dist = Distance(p, radius * 2);
        int w = p.W, h = p.H; var height = new float[w * h];
        Parallel.For(0, h, j =>
        {
            for (int i = 0; i < w; i++)
            {
                int k = j * w + i; if (dist[k] <= 0) continue;
                float t = Math.Min(1, dist[k] / radius), prof = MathF.Sqrt(1 - (1 - t) * (1 - t));
                height[k] = prof * radius + (bump == null ? 0 : bump(i, j) * bumpScale);
            }
        });
        float H(int i, int j) { if (j < 0 || j >= h) return 0; return height[j * w + M.Mod(i, w)]; }
        var src = (Col[])p.P.Clone();
        Parallel.For(0, h, j =>
        {
            for (int i = 0; i < w; i++)
            {
                int k = j * w + i; if (src[k].A <= 0) continue;
                float gx = (H(i + 1, j) - H(i - 1, j)) * .5f, gy = (H(i, j + 1) - H(i, j - 1)) * .5f;
                var n = new V3(-gx, -gy, 1).N();
                p.P[k] = shade(i, j, src[k], height[k] / Math.Max(1, radius), n);
            }
        });
    }

    /// <summary>Union-of-spheres cumulus bank lit from the upper left, quantised to a ramp.</summary>
    public sealed class Puff { public float X, Y, R; }
    public static List<Puff> CloudPuffs(Rng rng, float x0, float x1, float baseY, float height, float minR, float maxR, int count)
    {
        var list = new List<Puff>();
        for (int k = 0; k < count; k++)
        {
            float t = (k + rng.R(.1f, .9f)) / count, x = M.Lerp(x0, x1, t);
            float profile = MathF.Sin(MathF.PI * t);
            float r = M.Lerp(minR, maxR, rng.F() * .55f + profile * .45f);
            float y = baseY + r * .55f + rng.F() * height * profile;
            list.Add(new Puff { X = x, Y = y, R = r });
        }
        return list;
    }

    /// <summary>A cumulus bank: union of spheres with a flat base, lit from the upper left with bright rims.</summary>
    public static void CloudBank(Canvas p, float x0, float x1, float baseY, float height, float minR, float maxR, int count, Ramp ramp, int seed, float rim = .25f, float alpha = 1)
    {
        var f = p.F; var puffs = CloudPuffs(new Rng(seed), x0, x1, baseY, height, minR, maxR, count);
        p.Paint(x0 - maxR * 1.2f, x1 + maxR * 1.2f, baseY - .05f, baseY + height + maxR * 2, (x, y, i, j) =>
        {
            float z = Spheres(f, puffs, x, y, baseY, seed, out float nx, out float ny, .14f);
            if (z < 0) return Col.Clear;
            float nz = MathF.Sqrt(Math.Max(0, 1 - nx * nx - ny * ny));
            // Wrapped lighting keeps the shadow side soft and lavender rather than rock-dark.
            float facing = nx * Key.X + ny * Key.Y + nz * Key.Z, wrap = .5f + .5f * facing;
            float v = .16f + wrap * .8f - M.Smooth(baseY + height * .5f, baseY, y) * .2f;
            if (nz < .38f && nx * Key.X + ny * Key.Y > .05f) v += rim;
            return ramp.At(v, i, j, .3f).WithA(alpha);
        });
    }

    /// <summary>Finds the front-most sphere covering (x,y) and returns its lit value; -1 outside.</summary>
    public static float Spheres(Field f, List<Puff> puffs, float x, float y, float baseY, int seed, out float nx, out float ny, float erosion = .12f)
    {
        float best = -1; nx = ny = 0;
        foreach (var p in puffs)
        {
            float dx = f.Dx(x, p.X), dy = y - p.Y;
            float rr = p.R * (1 + f.N(x, y, p.R * .45f, seed) * erosion);
            float d2 = dx * dx + dy * dy;
            if (d2 >= rr * rr) continue;
            float z = MathF.Sqrt(rr * rr - d2) + p.Y * .02f;
            if (z > best) { best = z; nx = dx / rr; ny = dy / rr; }
        }
        if (best < 0 || y < baseY) return -1;
        return best;
    }
}
