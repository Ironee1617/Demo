namespace BackdropPainter;

/// <summary>Painting helpers for the Kunlun celestial palace: auspicious clouds, a dragon, glazed roofs,
/// dougong brackets, lanterns, halls, pagodas and cloud bridges.</summary>
public static partial class ChineseKit
{
    static float Blob(Field f, float x, float y, (float x, float y, float r)[] discs, float baseY)
    {
        float d = 99;
        foreach (var p in discs) { float dx = f.Dx(x, p.x), dy = y - p.y; d = Math.Min(d, MathF.Sqrt(dx * dx + dy * dy) - p.r); }
        return Math.Max(d, baseY - y);
    }

    /// <summary>Distance from (px,py) to the segment a-b, with t the normalised position along it.</summary>
    public static float Seg(float px, float py, float ax, float ay, float bx, float by, out float t)
    {
        float vx = bx - ax, vy = by - ay, l2 = Math.Max(1e-6f, vx * vx + vy * vy);
        t = M.Clamp01(((px - ax) * vx + (py - ay) * vy) / l2);
        float dx = px - (ax + vx * t), dy = py - (ay + vy * t);
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Auspicious cloud (祥雲): a scalloped body with a flat underside, double ink contour,
    /// an Archimedean curl at the leading end and a thinning tail trailing behind.</summary>
    public static void XiangCloud(Canvas c, float cx, float cy, float w, float h, Ramp ramp, int seed, float alpha = 1, int dir = 1)
    {
        var f = c.F; var rng = new Rng(seed);
        int n = Math.Max(3, (int)MathF.Round(w / (h * .45f)));
        var discs = new (float x, float y, float r)[n];
        for (int k = 0; k < n; k++)
        {
            float t = (k + .5f) / n, px = cx - w * .5f + w * t, prof = MathF.Sin(MathF.PI * t);
            float r = h * (.3f + .38f * prof) * rng.R(.85f, 1.1f);
            discs[k] = (px, cy + prof * h * .3f, r);
        }
        float baseY = cy - h * .1f, px1 = 1f / c.Ppu, line = 1.6f * px1;
        float R = h * .5f, sx = cx + dir * (w * .5f + R * .15f), sy = baseY + R * .9f, tailLen = w * .55f;
        c.Paint(cx - w * .5f - R - tailLen - .2f, cx + w * .5f + R + tailLen + .2f, baseY - R * .3f, cy + h * 1.1f, (x, y, i, j) =>
        {
            float d = Blob(f, x, y, discs, baseY);
            float kx = f.Dx(x, sx), ky = y - sy, rho = MathF.Sqrt(kx * kx + ky * ky);
            bool inCurl = rho < R;
            float tx = f.Dx(x, cx - dir * w * .5f) * -dir, ts = tx / tailLen;
            float tailY = baseY + h * .22f + MathF.Sin(ts * 4.5f + seed) * h * .08f, tailHalf = h * .13f * (1 - M.Clamp01(ts));
            float dt = MathF.Abs(y - tailY) - tailHalf;
            bool inTail = ts > -.1f && ts < 1 && dt < 0;
            if (d >= 0 && !inCurl && !inTail) return Col.Clear;
            if (inCurl)
            {
                if (rho > R - line) return ramp[0].WithA(alpha);
                float a = R / (MathF.PI * 2 * 2.3f), th = MathF.Atan2(ky, kx * dir);
                float u = ((R - rho) / a - th) / (MathF.PI * 2);
                float e = MathF.Abs(u - MathF.Round(u)) * MathF.PI * 2 * a;
                if (rho > R * .1f && e < line * .9f) return ramp[0].WithA(alpha);
                if (rho > R * .1f && e < line * 1.8f) return ramp[2].WithA(alpha);
                return ramp.At(.72f + ky / R * .2f, i, j, .3f).WithA(alpha);
            }
            if (inTail)
            {
                if (dt > -line) return ramp[0].WithA(alpha);
                return ramp.At(.7f - (tailY - y) / Math.Max(tailHalf, px1) * .15f, i, j, .3f).WithA(alpha);
            }
            if (d > -line) return ramp[0].WithA(alpha);
            if (d > -line * 2.6f && d < -line * 1.8f) return ramp[1].WithA(alpha);
            float e2 = px1 * 2;
            float gx = (Blob(f, x + e2, y, discs, baseY) - d) / e2, gy = (Blob(f, x, y + e2, discs, baseY) - d) / e2;
            float light = -gx * .55f + gy * .75f, depth = M.Clamp01(-d / (h * .45f));
            return ramp.At(.6f + light * .25f * (1 - depth * .5f) + depth * .12f, i, j, .35f).WithA(alpha);
        });
    }

    /// <summary>A serpentine dragon seen from the side: tapering body, dorsal spines, four clawed legs,
    /// a horned head with whiskers, and a gold line along the belly.</summary>
    public static void Dragon(Canvas c, float x0, float y0, float len, float amp, float thick, Col body, Col gold, int seed)
    {
        var f = c.F; float px1 = 1f / c.Ppu, ph = seed * .7f;
        float Py(float t) => y0 + amp * MathF.Sin(MathF.PI * 2 * 1.25f * t + ph) + amp * .35f * MathF.Sin(MathF.PI * 2 * 2.7f * t + ph * 1.7f);
        float Hw(float t) => thick * (.25f + .75f * MathF.Pow(MathF.Sin(MathF.PI * (.1f + .9f * M.Clamp01(t))), .6f));
        float hl = thick * 2.8f, hx = x0 - hl * .45f, hy = Py(0) + thick * .1f, hry = thick * .95f, hrx = hl * .5f;
        var legs = new[] { .2f, .34f, .62f, .76f };
        Col dark = Col.Lerp(body, Col.Black, .3f);
        c.Paint(x0 - hl * 2.2f, x0 + len + thick, y0 - amp * 1.4f - thick * 2.6f, y0 + amp * 1.4f + thick * 2.6f, (x, y, i, j) =>
        {
            float t = (x - x0) / len;
            // Head and its ornaments.
            float ox = x - hx, oy = y - hy, ex = ox / hrx, ey = oy / hry;
            if (ex * ex + ey * ey < 1)
            {
                float ang = MathF.Atan2(oy, ox);
                bool mouth = ox < -hrx * .3f && MathF.Abs(ang + MathF.PI * .94f) < .22f + (-ox / hrx - .3f) * .3f;
                if (mouth) return Col.Clear;
                float eyeX = ox + hrx * .25f, eyeY = oy - hry * .3f;
                if (eyeX * eyeX + eyeY * eyeY < thick * thick * .025f) return eyeX * eyeX + eyeY * eyeY < thick * thick * .006f ? dark : gold;
                return body;
            }
            // Horns sweep back from the brow; whiskers trail from the snout.
            foreach (var (bx, by, tipx, tipy, w0) in new[] { (hx + hrx * .2f, hy + hry * .7f, hx + hrx * 1.4f, hy + hry * 2.4f, thick * .22f), (hx + hrx * .5f, hy + hry * .6f, hx + hrx * 1.7f, hy + hry * 1.9f, thick * .16f) })
            {
                float d = Seg(x, y, bx, by, tipx, tipy, out float st);
                if (d < w0 * (1 - st * .85f)) return body;
            }
            foreach (var sign in new[] { 1f, -1f })
            {
                float s = (hx - hrx * .85f - x) / (hl * 1.3f);
                if (s > 0 && s < 1)
                {
                    float wy = hy - hry * .15f + sign * (s * s * thick * 1.1f + MathF.Sin(s * 7 + sign) * thick * .15f) - thick * .3f;
                    if (MathF.Abs(y - wy) < px1 * .9f) return gold;
                }
            }
            // Mane spikes behind the head.
            for (int k = 0; k < 3; k++)
            {
                float mx = hx + hrx * (.7f + k * .25f), my = hy + hry * (.5f - k * .35f);
                float d = Seg(x, y, mx, my, mx + thick * .9f, my + thick * (1.1f - k * .3f), out float st);
                if (d < thick * .14f * (1 - st)) return body;
            }
            if (t < 0 || t > 1.02f) return Col.Clear;
            float yc = Py(t), slope = (Py(t + .002f) - Py(t - .002f)) / (.004f * len);
            float dy = (y - yc) / MathF.Sqrt(1 + slope * slope), hw = Hw(t);
            // Dorsal spines along the back.
            float sp = thick * 1.1f, fin = thick * .55f * MathF.Max(0, 1 - 2 * MathF.Abs(M.Fract(x / sp) - .45f)) * (1 - MathF.Abs(t - .5f) * 1.4f);
            if (dy > hw && dy < hw + fin) return dy > hw + fin * .7f ? gold : body;
            // Legs: short bars down and forward from the belly, each with three claws.
            foreach (var lt in legs)
            {
                float rx = x0 + len * lt, ry = Py(lt) - Hw(lt) * .6f, fx = rx - thick * .9f, fy = ry - thick * 1.25f;
                if (Seg(x, y, rx, ry, fx, fy, out _) < thick * .28f) return body;
                for (int k = 0; k < 3; k++)
                    if (Seg(x, y, fx, fy, fx - thick * .45f * MathF.Cos(k * .5f - .2f), fy - thick * .45f * MathF.Sin(k * .5f - .2f), out _) < px1 * 1.2f) return body;
            }
            if (MathF.Abs(dy) > hw) return Col.Clear;
            if (dy < -hw * .5f && dy > -hw * .78f && t > .02f && t < .97f) return gold;
            if (Field.Hash01((int)(x * c.Ppu / 3), (int)(y * c.Ppu / 3), seed) > .82f && dy > -hw * .4f) return Col.Lerp(body, Col.White, .08f);
            return dy < -hw * .78f ? dark : body;
        });
    }

    /// <summary>Golden glazed hip roof in elevation: a concave slope from the ridge down to an eave whose
    /// corners sweep upward, convex tile ridges, tile rows, drip tiles and a ridge beam with curled ends.</summary>
    public static void Roof(Canvas c, float cx, float eaveY, float halfW, float ridgeHalf, float h, float upturn, Ramp gold, Col ridgeCol, Col ridgeHi, int seed, bool ridgeOn = true)
    {
        var f = c.F; float px1 = 1f / c.Ppu;
        float Eave(float ax) => eaveY + upturn * MathF.Pow(M.Clamp01((ax - halfW * .55f) / (halfW * .45f)), 2.4f);
        float Top(float ax) { float a = M.Clamp01((ax - ridgeHalf) / Math.Max(.01f, halfW - ridgeHalf)); return eaveY + h * (1 - MathF.Pow(a, 1.5f)) + upturn * MathF.Pow(a, 3); }
        float ridgeH = ridgeOn ? Math.Max(2.5f * px1, h * .14f) : 0;
        float tileW = Math.Max(3 * px1, h * .13f), tileH = Math.Max(2 * px1, h * .11f);
        c.Paint(cx - halfW - .1f, cx + halfW + .1f, eaveY - .05f, eaveY + h + upturn + ridgeH * 2.6f, (x, y, i, j) =>
        {
            float dx = f.Dx(x, cx), ax = MathF.Abs(dx);
            if (ax > halfW) return Col.Clear;
            float e = Eave(ax), t = Top(ax);
            if (ridgeOn)
            {
                if (ax < ridgeHalf + ridgeH * .3f && y > t - ridgeH * .2f && y < t + ridgeH) return y > t + ridgeH * .7f ? ridgeHi : ridgeCol;
                foreach (var sxo in new[] { -ridgeHalf, ridgeHalf })
                {
                    float ox = dx - sxo, oy = y - (t + ridgeH * 1.3f);
                    if (ox * ox + oy * oy < ridgeH * ridgeH * 1.1f) return oy > ridgeH * .3f ? ridgeHi : ridgeCol;
                }
            }
            if (y < e || y > t) return Col.Clear;
            float depth = t - e <= px1 ? 1 : (y - e) / (t - e);
            float v = .42f + depth * .3f - dx / halfW * .1f;
            float u = M.Mod(dx, tileW) / tileW;
            v += u < .3f ? .12f : u > .85f ? -.08f : 0;
            if (M.Mod(t - y, tileH) / tileH < .18f) v -= .1f;
            v += f.N(x, y, .3f, seed) * .05f;
            if (y - e < 2.5f * px1) return u < .5f ? gold[1] : gold[0];
            if (y - e < 4f * px1) return gold[2];
            return gold.At(v, i, j, .4f);
        });
    }

    /// <summary>A band of dougong bracket clusters: stepped cap blocks and arms widening upward on a dark ground.</summary>
    public static void Dougong(Canvas c, float x0, float x1, float y0, float y1, Col back, Ramp wood, Col tip, int seed)
    {
        var f = c.F; float span = y1 - y0, cw = f.Snap(Math.Max(.3f, span * 1.1f));
        c.Paint(x0, x1, y0, y1, (x, y, i, j) =>
        {
            float u = M.Mod(x + seed * .37f, cw) / cw - .5f, t = M.Clamp01((y - y0) / span);
            int tier = Math.Min(2, (int)(t * 3)); float lt = t * 3 - tier;
            float half = .12f + tier * .13f;
            bool arm = MathF.Abs(u) < half && lt > .3f, block = MathF.Abs(u) < .09f && lt <= .3f;
            if (!arm && !block) return back;
            if (arm && lt > .6f && MathF.Abs(MathF.Abs(u) - half) < .05f) return tip;
            return wood.At(.45f + (lt > .72f ? .2f : 0) + (u < 0 ? .1f : -.05f), i, j, .4f);
        });
    }

    /// <summary>Red paper lantern on a cord: gold caps, ribbed glowing shell and a tassel.</summary>
    public static void Lantern(Canvas c, float x, float topY, float r, float cord, Ramp red, Col gold, Col goldDark, Col cordCol)
    {
        var f = c.F; float px1 = 1f / c.Ppu, ry = r * 1.05f, cy = topY - cord - r * .18f - ry;
        c.Paint(x - r - .05f, x + r + .05f, cy - ry - r * .95f, topY, (px, py, i, j) =>
        {
            float dx = f.Dx(px, x);
            if (py > cy + ry)
            {
                if (py > cy + ry + r * .18f) return MathF.Abs(dx) < px1 * .8f ? cordCol : Col.Clear;
                return MathF.Abs(dx) < r * .42f ? (dx < 0 ? gold : goldDark) : Col.Clear;
            }
            if (py < cy - ry)
            {
                float below = cy - ry - py;
                if (below < r * .16f) return MathF.Abs(dx) < r * .42f ? (dx < 0 ? gold : goldDark) : Col.Clear;
                if (below < r * .45f) return MathF.Abs(dx) < px1 * .9f ? gold : Col.Clear;
                if (below < r * .9f) return MathF.Abs(dx) < r * .13f * (1 - (below - r * .45f) / (r * .45f) * .3f) ? red[2] : Col.Clear;
                return Col.Clear;
            }
            float ny = (py - cy) / ry, hx = r * MathF.Sqrt(Math.Max(0, 1 - ny * ny));
            if (MathF.Abs(dx) > hx) return Col.Clear;
            float u = Math.Clamp(dx / Math.Max(hx, px1), -1, 1), ang = MathF.Asin(u);
            float rib = M.Mod(ang / (MathF.PI / 5) + .5f, 1);
            float v = .5f + (1 - u * u) * .28f + (1 - ny * ny) * .1f - (rib < .14f ? .18f : 0) - MathF.Max(0, -ny) * .1f;
            if (hx - MathF.Abs(dx) < px1 * 1.2f) v -= .3f;
            return red.At(v, i, j, .35f);
        });
    }

    /// <summary>Red lacquered pillars and wall bays with lattice windows and a studded centre door.</summary>
    public static void WallBays(Canvas c, float cx, float halfW, float y0, float y1, float bayW, Ramp red, Ramp gold, Col panelDark, int seed, bool door = true)
    {
        var f = c.F; float px1 = 1f / c.Ppu;
        int bays = Math.Max(1, (int)MathF.Round(halfW * 2 / bayW)); bayW = halfW * 2 / bays;
        float pr = Math.Max(2.5f * px1, bayW * .07f), wallH = y1 - y0;
        c.Paint(cx - halfW - pr, cx + halfW + pr, y0, y1, (x, y, i, j) =>
        {
            float dx = f.Dx(x, cx) + halfW; int b = (int)MathF.Floor(dx / bayW); float u = dx - b * bayW;
            float pd = Math.Min(u, bayW - u); if (dx < 0) pd = -dx; if (dx > halfW * 2) pd = dx - halfW * 2;
            if (dx < -pr || dx > halfW * 2 + pr) return Col.Clear;
            float t = (y - y0) / wallH;
            if (pd < pr || dx < 0 || dx > halfW * 2)
            {
                float uu = (dx < 0 || dx > halfW * 2 ? (dx < 0 ? -dx : dx - halfW * 2) : pd) / pr;
                return red.At(Kit.Cylinder(uu * (u < bayW * .5f ? -1 : 1), .25f) * .85f, i, j, .35f);
            }
            bool centre = door && (b == bays / 2 || (bays % 2 == 0 && b == bays / 2 - 1));
            if (centre && t < .8f)
            {
                if (t > .76f) return gold.At(.6f, i, j, .3f);
                bool stud = M.Mod(u, bayW * .12f) < px1 * 1.6f && M.Mod(y - y0, wallH * .1f) < px1 * 1.6f;
                return stud ? gold[3] : red.At(.3f + f.N(x, y, .3f, seed) * .05f, i, j, .3f);
            }
            if (t > .28f && t < .82f && pd > pr * 1.6f)
            {
                float lx = M.Mod(u, Math.Max(4 * px1, bayW * .11f)), ly = M.Mod(y - y0, Math.Max(4 * px1, wallH * .09f));
                bool lat = lx < px1 * 1.3f || ly < px1 * 1.3f;
                return lat ? gold.At(.42f, i, j, .3f) : panelDark;
            }
            float v = .45f + f.N(x, y, .35f, seed) * .06f + (t > .9f ? .08f : 0);
            if (t > .82f && t < .86f) return gold.At(.45f, i, j, .3f);
            return red.At(v, i, j, .35f);
        });
    }

    /// <summary>A palace hall on a stone terrace: wall bays, dougong, and a golden roof (optionally two storeys).</summary>
    public static float Hall(Canvas c, float cx, float baseY, float halfW, float wallH, float roofH, Ramp stone, Ramp red, Ramp gold, Ramp wood, Col panelDark, Col ridge, Col ridgeHi, int seed, bool twoStorey = false)
    {
        var f = c.F; float px1 = 1f / c.Ppu;
        float terraceH = Math.Max(4 * px1, wallH * .22f), terrTop = baseY + terraceH, wallTop = terrTop + wallH;
        float dg = Math.Max(3 * px1, wallH * .2f);
        c.Paint(cx - halfW - .35f, cx + halfW + .35f, baseY, terrTop, (x, y, i, j) =>
        {
            float t = (y - baseY) / terraceH, dx = MathF.Abs(f.Dx(x, cx));
            if (dx > halfW + .35f - (t < .5f ? 0 : .08f)) return Col.Clear;
            if (t > .8f) return stone.At(.92f, i, j, .3f);
            bool mortar = Kit.Masonry(f, x, y, .5f, Math.Max(terraceH * .4f, 3 * px1), 1.2f * px1, seed, out float sh, out _, out _, out _);
            return stone.At(.55f + (sh - .5f) * .1f - (mortar ? .15f : 0), i, j, .3f);
        });
        WallBays(c, cx, halfW, terrTop, wallTop, .9f, red, gold, panelDark, seed);
        Dougong(c, cx - halfW - .15f, cx + halfW + .15f, wallTop, wallTop + dg, Col.Lerp(red[1], Col.Black, .35f), wood, Col.Hex("5e8a6a"), seed);
        float eave = wallTop + dg, flare = roofH * .35f;
        if (!twoStorey)
        {
            Roof(c, cx, eave, halfW + .4f + flare, halfW * .55f, roofH, roofH * .3f, gold, ridge, ridgeHi, seed);
            return eave + roofH + roofH * .3f;
        }
        Roof(c, cx, eave, halfW + .4f + flare, halfW * .72f, roofH * .62f, roofH * .22f, gold, ridge, ridgeHi, seed, false);
        float w2 = halfW * .72f, y2 = eave + roofH * .5f, wall2 = wallH * .7f;
        WallBays(c, cx, w2, y2, y2 + wall2, .9f, red, gold, panelDark, seed + 1, false);
        Dougong(c, cx - w2 - .15f, cx + w2 + .15f, y2 + wall2, y2 + wall2 + dg, Col.Lerp(red[1], Col.Black, .35f), wood, Col.Hex("5e8a6a"), seed + 1);
        Roof(c, cx, y2 + wall2 + dg, w2 + .4f + flare, w2 * .5f, roofH, roofH * .32f, gold, ridge, ridgeHi, seed + 2);
        return y2 + wall2 + dg + roofH * 1.32f;
    }

    /// <summary>A tiered pagoda: shrinking storeys, each with a dougong band and an upturned roof, capped by a gold spire.</summary>
    public static void Pagoda(Canvas c, float cx, float baseY, float halfW, int tiers, float tierH, Ramp stone, Ramp red, Ramp gold, Ramp wood, Col panelDark, Col ridge, Col ridgeHi, int seed)
    {
        var f = c.F; float px1 = 1f / c.Ppu;
        float y = baseY, hw = halfW;
        c.Paint(cx - halfW - .3f, cx + halfW + .3f, baseY, baseY + .3f, (x, yy, i, j) => stone.At(yy > baseY + .24f ? .9f : .55f, i, j, .3f));
        y += .3f;
        for (int k = 0; k < tiers; k++)
        {
            float wallH = tierH * .55f, roofH = tierH * .42f, dg = Math.Max(3 * px1, wallH * .22f);
            WallBays(c, cx, hw, y, y + wallH, Math.Max(.5f, hw * .9f), red, gold, panelDark, seed + k, k == 0);
            Dougong(c, cx - hw - .1f, cx + hw + .1f, y + wallH, y + wallH + dg, Col.Lerp(red[1], Col.Black, .35f), wood, Col.Hex("5e8a6a"), seed + k);
            bool last = k == tiers - 1;
            Roof(c, cx, y + wallH + dg, hw + .3f + roofH * .3f, hw * .35f, roofH, roofH * .4f, gold, ridge, ridgeHi, seed + k, last);
            y += wallH + dg + roofH * (last ? 1 : .72f);
            hw *= .88f;
        }
        float top = y + Math.Max(.2f, tierH * .25f);
        c.Paint(cx - .2f, cx + .2f, y, top + tierH * .9f, (x, yy, i, j) =>
        {
            float dx = f.Dx(x, cx), t = (yy - y) / (tierH * .9f + top - y);
            float hwid = Math.Max(px1, .09f * (1 - t) * (1 + (M.Mod(yy - y, .12f) < .04f ? .6f : 0)));
            return MathF.Abs(dx) < hwid ? gold.At(.6f - dx * 2 + (M.Mod(yy - y, .12f) < .04f ? .15f : 0), i, j, .3f) : Col.Clear;
        });
    }

    /// <summary>An arched stone stair-bridge between two terraces, with posts and a rail.</summary>
    public static void CloudBridge(Canvas c, float x0, float y0, float x1, float y1, float arch, Ramp stone, int seed)
    {
        var f = c.F; float px1 = 1f / c.Ppu, stepH = Math.Max(3 * px1, .11f);
        float Deck(float x) { float t = M.Clamp01((x - x0) / (x1 - x0)); return M.Lerp(y0, y1, t) + arch * MathF.Sin(MathF.PI * t); }
        c.Paint(x0, x1, Math.Min(y0, y1) - .7f, Math.Max(y0, y1) + arch + .5f, (x, y, i, j) =>
        {
            float ds = MathF.Floor(Deck(x) / stepH) * stepH;
            if (y > ds + .42f || y < ds - .6f) return Col.Clear;
            if (y > ds)
            {
                float u = M.Mod(x - x0, .42f);
                bool post = u < .06f && y < ds + .42f, rail = y > ds + .28f && y < ds + .36f;
                if (!post && !rail) return Col.Clear;
                return stone.At(post ? (y > ds + .36f ? .95f : .6f) : .82f, i, j, .3f);
            }
            float t = ds - y;
            if (t < 1.5f * px1) return stone.Last;
            if (t < .08f) return stone.At(.75f, i, j, .3f);
            bool mortar = Kit.Masonry(f, x, y, .5f, .15f, 1.2f * px1, seed, out float sh, out _, out _, out _);
            return stone.At(.5f - t * .45f + (sh - .5f) * .1f - (mortar ? .15f : 0) + f.N(x, y, .2f, seed) * .05f, i, j, .35f);
        });
    }
}
