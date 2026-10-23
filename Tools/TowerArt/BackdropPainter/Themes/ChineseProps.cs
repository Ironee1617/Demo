namespace BackdropPainter;

/// <summary>Garden props for the celestial palace: guardian lions, incense tripods, bamboo, peach trees,
/// the moon gate, koi, lotus leaves, dragon-coiled pillars and a marble balustrade.</summary>
public static partial class ChineseKit
{
    /// <summary>Seated stone guardian lion on a plinth, facing dir (1 right, -1 left).</summary>
    public static void Lion(Canvas c, float x, float baseY, float h, Ramp stone, int dir, int seed)
    {
        var f = c.F; float plinthH = h * .3f, pw = h * .48f, top = baseY + plinthH, headR = h * .2f;
        var parts = new (float x, float y, float r)[]
        {
            (x - dir * h * .14f, top + h * .3f, h * .3f),      // haunches
            (x + dir * h * .08f, top + h * .44f, h * .25f),    // chest
            (x + dir * h * .12f, top + h * .76f, headR),       // head
            (x + dir * h * .24f, top + h * .66f, h * .1f),     // muzzle
        };
        c.Paint(x - pw - .1f, x + pw + .1f, baseY, top + h + .1f, (px, py, i, j) =>
        {
            float dx = f.Dx(px, x);
            if (py < top)
            {
                float t = (py - baseY) / plinthH;
                if (MathF.Abs(dx) > pw * (t > .82f ? 1.06f : 1)) return Col.Clear;
                return stone.At(.5f + (t > .82f ? .25f : t < .14f ? -.1f : 0) + (dx < 0 ? .1f : -.08f) + f.N(px, py, .15f, seed) * .05f, i, j, .3f);
            }
            float lx = dx - dir * h * .2f;
            if (py < top + h * .46f && MathF.Abs(lx) < h * .08f) return stone.At(Kit.Cylinder(lx / (h * .08f), .25f) * .7f, i, j, .3f);
            float bx = dx - dir * h * .38f, by = py - (top + h * .1f);
            if (bx * bx + by * by < h * h * .012f) return stone.At(.5f + (-bx * .6f + by * .8f) / (h * .11f) * .3f, i, j, .3f);
            float best = -1, nx = 0, ny = 0; int which = -1;
            for (int k = 0; k < parts.Length; k++)
            {
                var p = parts[k]; float ox = f.Dx(px, p.x), oy = py - p.y, d2 = ox * ox + oy * oy;
                if (d2 >= p.r * p.r) continue;
                float z = MathF.Sqrt(p.r * p.r - d2) + k * .02f;
                if (z > best) { best = z; nx = ox / p.r; ny = oy / p.r; which = k; }
            }
            var hd = parts[2]; float hx = f.Dx(px, hd.x), hy = py - hd.y, hr = MathF.Sqrt(hx * hx + hy * hy);
            if (which < 0 && hr < headR * 1.32f && hy > -headR * .2f && hx * dir < headR * .5f)
            {
                float ang = MathF.Atan2(hy, hx), curl = MathF.Abs(MathF.Sin(ang * 6.5f));
                return stone.At(.3f + curl * .3f + hy / headR * .1f, i, j, .3f);
            }
            if (which < 0) return Col.Clear;
            float nz = MathF.Sqrt(Math.Max(0, 1 - nx * nx - ny * ny));
            float v = .28f + Kit.Lambert(nx, ny, nz) * .58f;
            if (which == 3 && py < hd.y - headR * .25f && hx * dir > headR * .55f) v -= .45f;
            if (which == 2) { float ex = hx - dir * headR * .4f, ey = hy - headR * .2f; if (ex * ex + ey * ey < headR * headR * .05f) return ex * ex + ey * ey < headR * headR * .012f ? stone[0] : stone[^1]; }
            if (which == 2 && hx * dir < 0) v += (Field.Hash01((int)(px * c.Ppu / 3), (int)(py * c.Ppu / 3), seed) - .5f) * .25f;
            if (which == 0 && f.N2(px, py, .08f, .05f, seed + 1) > .3f) v -= .08f;
            return Kit.Grain(stone.At(v, i, j, .35f), i, j, seed, .04f, .12f);
        });
    }

    /// <summary>Bronze tripod incense burner (ding): three legs, a bellied bowl with a taotie band, two ears.</summary>
    public static void Ding(Canvas c, float x, float baseY, float h, Ramp bronze, Col ember, int seed)
    {
        var f = c.F; float px1 = 1f / c.Ppu, legH = h * .3f, bodyR = h * .36f, bodyRy = h * .27f, cy = baseY + legH + bodyRy * .85f;
        c.Paint(x - bodyR - .1f, x + bodyR + .1f, baseY, baseY + h + .05f, (px, py, i, j) =>
        {
            float dx = f.Dx(px, x), dy = py - cy;
            if (py < cy)
                foreach (var lx in new[] { -bodyR * .62f, bodyR * .62f, 0f })
                {
                    float lw = h * .06f * (lx == 0 ? .8f : 1), curve = MathF.Sign(lx) * MathF.Pow(M.Clamp01((cy - py) / (legH + bodyRy)), 2) * h * .1f;
                    if (MathF.Abs(dx - lx - curve) < lw && py > baseY) return bronze.At(.4f + (lx == 0 ? -.18f : 0) + (dx - lx - curve < 0 ? .15f : -.05f) + (py < baseY + px1 * 3 ? -.15f : 0), i, j, .35f);
                }
            float ex = dx / bodyR, ey = dy / bodyRy;
            if (ex * ex + ey * ey < 1)
            {
                float nz = MathF.Sqrt(Math.Max(0, 1 - ex * ex - ey * ey)), v = .28f + Kit.Lambert(ex, ey, nz) * .58f;
                if (MathF.Abs(ey + .05f) < .3f)
                {
                    float u = MathF.Asin(Math.Clamp(ex, -1, 1)), mu = M.Mod(u * 5, 1), mv = M.Mod((ey + .35f) * 1.7f, 1);
                    bool line = mu < .16f || (mv < .18f && mu < .7f) || (MathF.Abs(mv - .6f) < .09f && mu > .3f);
                    if (line) v -= .2f;
                }
                if (ey < -.75f) v -= .1f;
                if (ey > .78f) v += .18f;
                return Kit.Grain(bronze.At(v, i, j, .35f), i, j, seed, .05f, .12f);
            }
            foreach (var sx in new[] { -bodyR * .55f, bodyR * .55f })
            {
                float ox = dx - sx, oy = py - (cy + bodyRy), ew = h * .08f, eh = h * .18f;
                if (MathF.Abs(ox) < ew && oy > -eh * .3f && oy < eh)
                {
                    if (MathF.Abs(ox) < ew * .45f && oy > eh * .25f && oy < eh * .75f) return Col.Clear;
                    return bronze.At(.5f + (ox < 0 ? .12f : -.1f), i, j, .3f);
                }
            }
            return Col.Clear;
        });
        c.Paint(x - bodyR * .5f, x + bodyR * .5f, cy + bodyRy - px1 * 3, cy + bodyRy + px1 * 2, (px, py, i, j) => Dither.Hit(.6f, i, j) ? ember : Col.Clear);
    }

    /// <summary>A rising plume of incense smoke, swaying and thinning as it climbs.</summary>
    public static void Smoke(Canvas c, float x, float y0, float height, float width, Col col, float strength, int seed)
    {
        var f = c.F;
        c.Paint(x - width * 2 - 1, x + width * 2 + 1, y0, y0 + height, (px, py, i, j) =>
        {
            float rise = (py - y0) / height, sway = MathF.Sin(rise * 5.5f + seed) * width * .7f * rise + rise * width * .4f;
            float w = width * (.25f + rise * 1.3f), d = MathF.Abs(f.Dx(px, x + sway)) / w;
            if (d > 1) return Col.Clear;
            float n = f.Fbm(px, py - rise * .4f, .6f, 3, seed) * .5f + .5f;
            float a = (1 - d * d) * n * 1.6f * (1 - rise) * strength * M.Smooth(0, .1f, rise);
            float q = MathF.Floor(a * 5 + Dither.Bayer(i, j)) / 5;
            return q <= 0 ? Col.Clear : col.WithA(Math.Min(1, q));
        });
    }

    /// <summary>A bamboo stalk with nodes and fans of narrow leaves on the upper half.</summary>
    public static void Bamboo(Canvas c, float x, float baseY, float h, float lean, Ramp green, int seed)
    {
        var rng = new Rng(seed); float px1 = 1f / c.Ppu, r = .05f + h * .005f;
        float Cx(float t) => x + lean * h * t * t;
        c.Paint(Math.Min(x, x + lean * h) - .2f, Math.Max(x, x + lean * h) + .2f, baseY, baseY + h, (px, py, i, j) =>
        {
            float t = (py - baseY) / h, u = (px - Cx(t)) / r;
            if (MathF.Abs(u) > 1) return Col.Clear;
            float seg = M.Mod(py - baseY + seed * .1f, .55f);
            return green.At(Kit.Cylinder(u, .25f) * .72f + (seg < .03f ? -.3f : seg < .06f ? .15f : 0), i, j, .35f);
        });
        int fans = 3 + (int)h;
        for (int k = 0; k < fans; k++)
        {
            float t = rng.R(.35f, 1f), lx = Cx(t), ly = baseY + h * t; int n = rng.I(2, 5);
            for (int m = 0; m < n; m++)
            {
                float a = rng.Chance(.5f) ? rng.R(-.95f, -.1f) : MathF.PI - rng.R(-.95f, -.1f);
                float len = rng.R(.35f, .6f) * (.7f + h * .05f), wid = len * .09f, ca = MathF.Cos(a), sa = MathF.Sin(a), tone = rng.R(.35f, .75f);
                c.Paint(lx - len, lx + len, ly - len, ly + len, (px, py, i, j) =>
                {
                    float dx = px - lx, dy = py - ly, along = dx * ca + dy * sa, across = -dx * sa + dy * ca;
                    if (along < 0 || along > len) return Col.Clear;
                    float half = wid * MathF.Sin(MathF.PI * MathF.Min(1, along / len * 1.05f));
                    if (MathF.Abs(across) > half) return Col.Clear;
                    return green.At(tone + (across > 0 ? .1f : -.05f) + (MathF.Abs(across) < px1 * .6f ? .08f : 0), i, j, .3f);
                });
            }
        }
    }

    static void Capsule(Canvas c, float ax, float ay, float bx, float by, float w0, float w1, Ramp bark, int seed)
    {
        float r = Math.Max(w0, w1);
        c.Paint(Math.Min(ax, bx) - r, Math.Max(ax, bx) + r, Math.Min(ay, by) - r, Math.Max(ay, by) + r, (px, py, i, j) =>
        {
            float d = Seg(px, py, ax, ay, bx, by, out float t), w = M.Lerp(w0, w1, t);
            if (d >= w) return Col.Clear;
            float vx = bx - ax, vy = by - ay, side = MathF.Sign((px - ax) * vy - (py - ay) * vx);
            float u = d / w * side;
            return bark.At(Kit.Cylinder(u, .2f) * .8f + c.F.N2(px, py, .08f, .4f, seed) * .1f, i, j, .35f);
        });
    }

    /// <summary>Peach tree of immortality: a twisting dark trunk, forking branches, pink blossom clumps and golden peaches.</summary>
    public static void PeachTree(Canvas c, float x, float baseY, float h, Ramp bark, Ramp blossom, Col peach, Col peachDark, Col blush, int seed)
    {
        var rng = new Rng(seed);
        var segs = new List<(float ax, float ay, float bx, float by, float w0, float w1)>();
        var tips = new List<(float x, float y, float w)>();
        float tx = x, ty = baseY, w = h * .055f, dirx = rng.Chance(.5f) ? 1 : -1;
        var trunk = new List<(float x, float y)>();
        for (int k = 0; k < 4; k++)
        {
            float len = h * .13f, ang = MathF.PI / 2 + dirx * (k % 2 == 0 ? .4f : -.3f) * rng.R(.6f, 1.4f);
            float nx = tx + MathF.Cos(ang) * len, ny = ty + MathF.Sin(ang) * len;
            segs.Add((tx, ty, nx, ny, w, w * .85f)); trunk.Add((nx, ny)); tx = nx; ty = ny; w *= .85f;
        }
        void Branch(float ax, float ay, float ang, float len, float w0, int depth)
        {
            float bx = ax + MathF.Cos(ang) * len, by = ay + MathF.Sin(ang) * len;
            segs.Add((ax, ay, bx, by, w0, w0 * .6f));
            if (depth == 0) { tips.Add((bx, by, len)); return; }
            int n = rng.I(2, 4);
            for (int k = 0; k < n; k++) Branch(bx, by, ang + rng.R(-.9f, .9f), len * rng.R(.55f, .8f), w0 * .6f, depth - 1);
        }
        Branch(tx, ty, MathF.PI / 2 + dirx * .55f, h * .24f, w, 2);
        Branch(tx, ty, MathF.PI / 2 - dirx * .75f, h * .22f, w * .9f, 2);
        Branch(trunk[1].x, trunk[1].y, MathF.PI / 2 - dirx * 1.25f, h * .2f, w * .8f, 1);
        foreach (var s in segs) Capsule(c, s.ax, s.ay, s.bx, s.by, s.w0, s.w1, bark, seed);
        foreach (var t in tips)
            Kit.LeafClump(c, t.x, t.y + .05f, .3f + t.w * .9f, .22f + t.w * .6f, 50 + (int)(t.w * 150), blossom, seed + (int)(t.x * 97 + t.y * 31), .13f, .1f);
        foreach (var t in tips)
        {
            if (rng.Chance(.45f)) continue;
            float px = t.x + rng.R(-.25f, .25f), py = t.y - rng.R(.05f, .3f), r = .085f + rng.F() * .03f;
            c.Paint(px - r, px + r, py - r, py + r * 1.2f, (qx, qy, i, j) =>
            {
                float dx = qx - px, dy = qy - py, d = MathF.Sqrt(dx * dx + dy * dy) / r;
                if (d > 1) return dy > r * .8f && MathF.Abs(dx) < 1f / c.Ppu && dy < r * 1.15f ? bark[2] : Col.Clear;
                float light = (-dx * .6f + dy * .75f) / r;
                if (light < -.3f && d > .55f) return peachDark;
                return dx > r * .2f && dy < 0 && Dither.Hit(.5f, i, j) ? blush : Kit.Mix(peachDark, peach, .55f + light * .5f, i, j, 3);
            });
        }
    }

    /// <summary>A white garden wall with grey tile coping pierced by a round moon gate (the opening is left transparent).</summary>
    public static void MoonGate(Canvas c, float x, float baseY, float wallTop, float halfW, float r, Ramp plaster, Ramp tile, Ramp frame, int seed)
    {
        var f = c.F; float px1 = 1f / c.Ppu, cy = baseY + r + .1f, copingH = Math.Max(4 * px1, .26f);
        c.Paint(x - halfW - .1f, x + halfW + .1f, baseY, wallTop + copingH, (px, py, i, j) =>
        {
            float dx = f.Dx(px, x);
            if (MathF.Abs(dx) > halfW + (py > wallTop ? .08f : 0)) return Col.Clear;
            float dy = py - cy, d = MathF.Sqrt(dx * dx + dy * dy);
            if (d < r) return Col.Clear;
            if (py > wallTop)
            {
                float t = (py - wallTop) / copingH, u = M.Mod(dx, .18f) / .18f;
                if (t < .22f) return u < .5f ? tile[1] : tile[0];
                if (t > .86f) return tile.Last;
                return tile.At(.45f + (u < .12f ? -.2f : 0) + t * .25f, i, j, .3f);
            }
            if (d < r + .14f) { float t = (d - r) / .14f; return frame.At(.5f + (-dx * .5f + dy * .7f) / r * .25f + (t < .25f ? .2f : 0), i, j, .3f); }
            if (py < baseY + .3f) return plaster.At(.5f + (py > baseY + .25f ? .2f : 0) + f.N(px, py, .2f, seed) * .05f, i, j, .3f);
            return plaster.At(.8f + f.Fbm(px, py, .6f, 3, seed) * .08f - M.Smooth(baseY + .9f, baseY + .3f, py) * .12f, i, j, .3f);
        });
    }

    /// <summary>A koi seen from above: patterned body, tail fan and pectoral fins, at an angle.</summary>
    public static void Koi(Canvas c, float x, float y, float len, float ang, Col body, Col mark, Col dark, int seed)
    {
        var f = c.F; float ca = MathF.Cos(ang), sa = MathF.Sin(ang);
        c.Paint(x - len, x + len, y - len, y + len, (px, py, i, j) =>
        {
            float dx = f.Dx(px, x), dy = py - y, al = dx * ca + dy * sa, ac = -dx * sa + dy * ca, t = al / (len * .5f);
            float half = len * .17f * MathF.Sqrt(Math.Max(0, 1 - t * t)) * (1 + (1 - MathF.Abs(t + .2f)) * .2f);
            if (t > -1 && t < 1 && MathF.Abs(ac) < half)
            {
                var col = f.N(px * 6, py * 6, .2f, seed) > .2f ? mark : body;
                if (MathF.Abs(ac) > half - 1.5f / c.Ppu) col = Col.Lerp(col, dark, .4f);
                if (t > .78f && t < .9f && MathF.Abs(MathF.Abs(ac) - half * .45f) < len * .02f) col = dark;
                return col.WithA(.92f);
            }
            if (t < -.8f && t > -1.45f) { float tt = (-.8f - t) / .65f; if (MathF.Abs(ac) < len * .05f + tt * len * .17f) return Col.Lerp(body, mark, .5f).WithA(.7f); }
            if (t > .1f && t < .45f && MathF.Abs(ac) > half && MathF.Abs(ac) < half + len * .1f * (1 - MathF.Abs(t - .28f) / .17f)) return body.WithA(.6f);
            return Col.Clear;
        });
    }

    /// <summary>A floating lotus pad with a notch, radial veins and a dark centre.</summary>
    public static void Lotus(Canvas c, float x, float y, float r, Ramp leaf, int seed)
    {
        var f = c.F; float notch = .9f + seed % 3 * .7f;
        c.Paint(x - r, x + r, y - r * .45f, y + r * .45f, (px, py, i, j) =>
        {
            float dx = f.Dx(px, x), dy = (py - y) / .42f, d = MathF.Sqrt(dx * dx + dy * dy) / r;
            if (d > 1) return Col.Clear;
            float ang = MathF.Atan2(dy, dx);
            if (MathF.Abs(ang - notch) < .2f && d > .25f) return Col.Clear;
            float v = .55f + dy / r * .2f - d * .1f + (M.Mod(ang * 5 / MathF.PI, 1) < .08f ? -.12f : 0);
            if (d > .9f) v -= .2f;
            if (d < .08f) v -= .2f;
            return leaf.At(v, i, j, .3f);
        });
    }

    /// <summary>Red lacquered pillar on a gilded base with a golden dragon coiling up it, its head emerging at the capital.</summary>
    public static void DragonPillar(Canvas c, float x, float y0, float y1, float r, Ramp red, Ramp gold, int seed)
    {
        var f = c.F; float px1 = 1f / c.Ppu, pitch = r * 7.5f;
        float Coil(float y) => MathF.Sin((y - y0) / pitch * MathF.PI * 2 + seed) * r * .85f;
        float Front(float y) => MathF.Cos((y - y0) / pitch * MathF.PI * 2 + seed);
        float headY = y1 - r * .6f, headX = Coil(headY);
        c.Paint(x - r * 1.5f, x + r * 1.5f, y0, y1 + r * .7f, (px, py, i, j) =>
        {
            float dx = f.Dx(px, x), u = dx / r;
            if (py > y1) return MathF.Abs(dx) < r * 1.3f ? gold.At(.6f - u * .2f + (py > y1 + r * .45f ? .15f : 0), i, j, .35f) : Col.Clear;
            float hx = (dx - headX) / (r * .5f), hy = (py - headY) / (r * .3f);
            if (hx * hx + hy * hy < 1)
            {
                if (hx > .3f && hy > -.1f && hy < .25f) return gold[0];
                return gold.At(.55f + (-hx * .3f + hy * .5f) * .4f, i, j, .3f);
            }
            foreach (var sgn in new[] { -1f, 1f })
                if (Seg(px, py, x + headX - r * .1f * sgn, headY + r * .25f, x + headX - r * .35f * sgn, headY + r * .75f, out float st) < r * .09f * (1 - st)) return gold[3];
            float coilX = Coil(py), front = Front(py), cw = r * .32f;
            bool onCoil = front > -.1f && MathF.Abs(dx - coilX) < cw * (.6f + .4f * M.Clamp01(front + .1f)) && py > y0 + r * .9f && py < headY - r * .1f;
            if (onCoil)
            {
                float cu = (dx - coilX) / cw, v = .5f + Kit.Cylinder(cu, .2f) * .42f - (front < .2f ? .2f : 0);
                if (Field.Hash01((int)((px - coilX) * c.Ppu / 2.5f), (int)(py * c.Ppu / 2.5f), seed) > .72f) v -= .12f;
                return gold.At(v, i, j, .35f);
            }
            if (MathF.Abs(u) > 1) return Col.Clear;
            if (py < y0 + r * .9f) return gold.At(.55f - u * .2f + (py < y0 + r * .3f ? -.1f : 0) + (py > y0 + r * .8f ? .1f : 0), i, j, .35f);
            float vv = Kit.Cylinder(u, .22f) * .85f + f.N(px, py, .4f, seed) * .04f;
            if (MathF.Abs(u) > .92f) vv -= .15f;
            return red.At(vv, i, j, .35f);
        });
    }

    /// <summary>White marble balustrade along a terrace edge: posts with bud finials, a rail and panels.</summary>
    public static void Balustrade(Canvas c, float x0, float x1, float y0, float h, Ramp stone, int seed)
    {
        var f = c.F; float px1 = 1f / c.Ppu, sp = f.Snap(.62f);
        c.Paint(x0, x1, y0, y0 + h * 1.2f, (x, y, i, j) =>
        {
            float t = (y - y0) / h, u = M.Mod(x, sp) / sp;
            bool post = u < .11f || u > .89f;
            if (post)
            {
                if (t > 1.2f) return Col.Clear;
                if (t > 1) { float bud = MathF.Abs(u < .5f ? u : 1 - u) / .11f; return bud < 1 - (t - 1) * 4 ? stone.At(.75f + (u < .5f ? .1f : -.1f), i, j, .3f) : Col.Clear; }
                return stone.At(.6f + (u < .05f || u > .95f ? .15f : -.05f) + (t > .95f ? .2f : 0), i, j, .3f);
            }
            if (t > 1) return Col.Clear;
            if (t > .82f) return stone.At(t > .95f ? .92f : .7f, i, j, .3f);
            if (t < .12f) return stone.At(.55f, i, j, .3f);
            float cx = MathF.Abs(u - .5f) * sp, cy = MathF.Abs(t - .47f) * h;
            bool carve = cx < sp * .22f && cy < h * .22f && !(cx < sp * .14f && cy < h * .14f);
            return stone.At(.72f + (carve ? -.18f : 0) + f.N(x, y, .2f, seed) * .04f, i, j, .3f);
        });
    }
}
