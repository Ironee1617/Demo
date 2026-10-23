namespace BackdropPainter;

/// <summary>Painting helpers for the Dangun theme: tapered tube strokes for trunks and limbs, Korean red pines
/// with umbrella needle pads, ink-wash mountain ranges that dissolve into mist, cloth ribbons, twisted straw
/// rope, prayer cairns, dolmens, carved jangseung posts, a pavilion silhouette and birds.</summary>
public static class DangunKit
{
    public readonly struct Node
    {
        public readonly float X, Y, W;
        public Node(float x, float y, float w) { X = x; Y = y; W = w; }
    }

    /// <summary>Nodes along a quadratic curve a→b bending toward c; the half-width tapers w0→w1 and a sideways wobble adds a crook.</summary>
    public static List<Node> Curve(float ax, float ay, float cx, float cy, float bx, float by, float w0, float w1, int steps = 10, float wobble = 0, int seed = 0)
    {
        var list = new List<Node>(steps + 1);
        float dx = bx - ax, dy = by - ay, len = MathF.Max(1e-4f, MathF.Sqrt(dx * dx + dy * dy));
        float nx = -dy / len, ny = dx / len;
        for (int k = 0; k <= steps; k++)
        {
            float t = k / (float)steps, u = 1 - t;
            float x = u * u * ax + 2 * u * t * cx + t * t * bx, y = u * u * ay + 2 * u * t * cy + t * t * by;
            float w = wobble * MathF.Sin(t * 7.3f + seed * 1.7f) * t * (1 - t) * 4;
            list.Add(new Node(x + nx * w, y + ny * w, w0 + (w1 - w0) * t));
        }
        return list;
    }

    /// <summary>Interpolated node at parameter t along the node list.</summary>
    public static Node At(IList<Node> nodes, float t)
    {
        float s = Math.Clamp(t, 0, 1) * (nodes.Count - 1);
        int k = Math.Min(nodes.Count - 2, (int)MathF.Floor(s)); float u = s - k;
        var a = nodes[k]; var b = nodes[k + 1];
        return new Node(a.X + (b.X - a.X) * u, a.Y + (b.Y - a.Y) * u, a.W + (b.W - a.W) * u);
    }

    /// <summary>Paints a tapered tube along the nodes; jag roughens the outline with noise.</summary>
    public static void Stroke(Canvas p, IList<Node> nodes, Col col, float jag = 0, int seed = 0)
    {
        if (nodes.Count < 2) return;
        float pad = jag * 1.3f + .05f;
        float x0 = nodes.Min(n => n.X - n.W) - pad, x1 = nodes.Max(n => n.X + n.W) + pad;
        float y0 = nodes.Min(n => n.Y - n.W) - pad, y1 = nodes.Max(n => n.Y + n.W) + pad;
        var f = p.F; int count = nodes.Count;
        p.Paint(x0, x1, y0, y1, (x, y, i, j) =>
        {
            float best = 9;
            for (int k = 0; k + 1 < count; k++)
            {
                var a = nodes[k]; var b = nodes[k + 1];
                float dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
                float t = l2 > 1e-8f ? Math.Clamp(((x - a.X) * dx + (y - a.Y) * dy) / l2, 0, 1) : 0;
                float px = a.X + dx * t, py = a.Y + dy * t;
                float d = MathF.Sqrt((x - px) * (x - px) + (y - py) * (y - py)) - (a.W + (b.W - a.W) * t);
                if (d < best) best = d;
            }
            if (jag > 0) best -= (f.N(x, y, .22f, seed) + .3f) * jag;
            return best < 0 ? col : Col.Clear;
        });
    }

    /// <summary>A flat umbrella pad of pine needles: domed top, ragged bristly underside, short diagonal needle dashes.</summary>
    public static void NeedlePad(Canvas p, float cx, float cy, float rx, float ry, Ramp needles, int seed, float bright = 0)
    {
        var f = p.F;
        p.Paint(cx - rx - .15f, cx + rx + .15f, cy - ry - .2f, cy + ry + .15f, (x, y, i, j) =>
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            float r = dx * dx + (dy > 0 ? dy * dy * 1.15f : dy * dy * 1.9f);
            float edge = f.N2(x, y, .13f, .07f, seed) * .55f;
            if (r > 1 + edge) return Col.Clear;
            int ci = (int)MathF.Floor(i / 3f), cj = (int)MathF.Floor((j + (dx < 0 ? -i : i) * .45f) / 5f);
            float dash = Field.Hash01(ci, cj, seed);
            float v = .36f + bright + (dy > .15f ? .22f : dy < -.35f ? -.16f : 0) - dx * .1f + (dash - .5f) * .36f;
            if (r > .72f + edge) v -= .1f;
            return needles.At(v, i, j, .3f);
        });
    }

    /// <summary>A Korean red pine: crooked leaning trunk, sweeping limbs and umbrella pads. Wood is a silhouette
    /// (inflate it later); needle pads are painted straight onto <paramref name="pads"/>.</summary>
    public static void Pine(Canvas wood, Canvas pads, float x, float baseY, float h, float lean, Ramp needles, int seed, float bright = 0, Col? woodCol = null)
    {
        var rng = new Rng(seed);
        var wc = woodCol ?? Col.White;
        float topX = x + lean * h, topY = baseY + h;
        float crook = rng.R(-.1f, .1f) * h;
        var trunk = Curve(x, baseY, x + lean * h * .35f + crook, baseY + h * .5f, topX, topY, h * .05f, h * .014f, 12, h * .03f, seed);
        Stroke(wood, trunk, wc, h * .004f, seed);
        var padList = new List<(float x, float y, float rx, float ry, float z)>();
        int limbs = 3 + rng.I(0, 3);
        for (int k = 0; k < limbs; k++)
        {
            float t = .4f + .5f * k / Math.Max(1, limbs - 1) + rng.R(-.04f, .04f);
            float side = (k % 2 == 0 ? 1 : -1) * (rng.Chance(.2f) ? -1 : 1);
            var n = At(trunk, t);
            float len = h * (.17f + (1 - t) * .24f) * rng.R(.8f, 1.2f);
            float ex = n.X + side * len, ey = n.Y + len * rng.R(.12f, .4f);
            var limb = Curve(n.X, n.Y, n.X + side * len * .55f, n.Y + len * .02f, ex, ey, n.W * .6f, h * .008f, 8, 0, seed + k);
            Stroke(wood, limb, wc);
            padList.Add((ex + side * len * .12f, ey + len * .1f, len * .62f + h * .06f, len * .2f + h * .025f, t));
            if (rng.Chance(.5f))
            {
                // A secondary tuft halfway along the limb.
                var m = At(limb, .5f);
                padList.Add((m.X, m.Y + len * .08f, len * .3f + h * .03f, len * .11f + h * .015f, t - .01f));
            }
        }
        padList.Add((topX, topY + h * .03f, h * .17f, h * .055f, 1));
        foreach (var pd in padList.OrderBy(q => q.z)) NeedlePad(pads, pd.x, pd.y, pd.rx, pd.ry, needles, seed * 7 + (int)(pd.z * 100), bright);
    }

    /// <summary>Per-column crest heights for a range: pointed peaks over a base level plus ridged crags.</summary>
    public static float[] CrestTable(Canvas p, float baseY, (float x, float h, float w)[] peaks, float rough, int seed)
    {
        var f = p.F; var t = new float[p.W];
        for (int i = 0; i < p.W; i++)
        {
            float x = p.X(i), y = baseY;
            foreach (var (px, h, w) in peaks)
            {
                float d = MathF.Abs(f.Dx(x, px)) / w;
                if (d < 1) y += h * MathF.Pow(1 - d, 1.4f) * (1 + .12f * MathF.Sin(d * 11 + px));
            }
            y += (f.Ridge(x, 0, 1.4f, 3, seed) - .5f) * rough + f.Fbm(x, 0, 5, 2, seed + 1) * rough * .6f;
            t[i] = y;
        }
        return t;
    }

    /// <summary>Fills below a crest table: slopes facing the key light are lit, the crest catches a rim of dawn, vertical
    /// brush streaks texture the body and everything dissolves into mist with depth below the crest.</summary>
    public static void Range(Canvas p, float[] crest, Ramp ramp, float bottom, Func<float, Col> fog, float fogBase, float fogDepth, float lit, int seed, Col rim, float rimAmount, float texture = .1f, float ink = 0)
    {
        var f = p.F; int W = p.W;
        p.Paint(0, p.Period, bottom, p.Top, (x, y, i, j) =>
        {
            float c = crest[i];
            if (y > c) return Col.Clear;
            float slope = (crest[M.Mod(i + 2, W)] - crest[M.Mod(i - 2, W)]) * p.Ppu * .25f;
            float facing = Math.Clamp(slope, -1.5f, 1.5f) / 1.5f;
            float depth = c - y;
            float v = .45f + facing * lit + f.Fbm2(x, y, .3f, 2.4f, 3, seed) * texture + f.Fbm(x, y, 1.8f, 2, seed + 1) * texture * .6f;
            if (f.Ridge(x, y, 1.2f, 3, seed + 2) > .9f) v -= .12f;
            // An ink line of darker crags just under the crest, as a brush would leave it.
            if (ink > 0 && depth > 2.5f * p.Px && depth < (6 + f.N(x, 0, .3f, seed + 3) * 4) * p.Px) v -= ink;
            var col = ramp.At(v, i, j, .4f);
            float fa = fogBase + M.Smooth(0, fogDepth, depth) * (1 - fogBase) * .85f;
            float q = MathF.Floor(fa * 12 + (Dither.Bayer(i, j) - .5f) * .8f + .5f) / 12;
            col = Col.Lerp(col, fog(y), q);
            if (depth < 2.5f * p.Px) col = Col.Lerp(col, rim, rimAmount);
            return col;
        });
    }

    /// <summary>Horizontal streaks of mist, densest at the middle of the band.</summary>
    public static void MistBands(Canvas c, float y0, float y1, float strength, Col col, int seed, float sizeX = 7, float sizeY = .35f)
    {
        var f = c.F;
        c.Paint(0, c.Period, y0, y1, (x, y, i, j) =>
        {
            float n = f.Fbm2(x, y, sizeX, sizeY, 3, seed);
            float env = MathF.Sin(MathF.PI * M.InvLerp(y0, y1, y));
            float a = M.Smooth(.02f, .5f, n) * env * strength;
            float q = MathF.Floor(a * 5 + Dither.Bayer(i, j)) / 5;
            return q <= 0 ? Col.Clear : col.WithA(q);
        });
    }

    /// <summary>Five-step cloth ramp around a base colour.</summary>
    public static Ramp Cloth(Col c) => new(Col.Lerp(c, Col.Black, .5f), Col.Lerp(c, Col.Black, .25f), c, Col.Lerp(c, Col.White, .18f), Col.Lerp(c, Col.White, .42f));

    /// <summary>A hanging cloth strip that sways, twists and ends in a notched hem.</summary>
    public static void Ribbon(Canvas p, float x, float top, float len, float w, Ramp cloth, int seed, float sway = .15f)
    {
        p.Paint(x - w - sway - .05f, x + w + sway + .05f, top - len - .05f, top + .02f, (px, py, i, j) =>
        {
            float t = (top - py) / len; if (t < 0 || t > 1) return Col.Clear;
            float ph = seed * .61f;
            float cx = x + (MathF.Sin(t * 4.2f + ph) * .7f + MathF.Sin(t * 9.1f + ph * 2) * .3f) * sway * MathF.Min(1, t * 1.5f);
            float twist = .72f + .28f * MathF.Cos(t * 5.8f + ph);
            float half = w * .5f * twist, u = (px - cx) / half;
            if (MathF.Abs(u) > 1) return Col.Clear;
            if (t > 1 - .05f * Field.Hash01(i, seed, 3)) return Col.Clear;
            float v = .5f + MathF.Cos(t * 5.8f + ph) * .16f + (u < -.35f ? .12f : u > .5f ? -.14f : 0);
            if (MathF.Abs(u) > .8f) v -= .1f;
            return cloth.At(v, i, j, .35f);
        });
    }

    /// <summary>Height of a slack rope between two points at x.</summary>
    public static float RopeY(float x0, float y0, float x1, float y1, float sag, float x)
    {
        float t = M.Clamp01((x - x0) / (x1 - x0));
        return y0 + (y1 - y0) * t - sag * 4 * t * (1 - t);
    }

    /// <summary>A twisted straw rope slung between two points.</summary>
    public static void Rope(Canvas p, float x0, float y0, float x1, float y1, float sag, float thick, Ramp straw, int seed)
    {
        p.Paint(Math.Min(x0, x1), Math.Max(x0, x1), Math.Min(y0, y1) - sag - thick, Math.Max(y0, y1) + thick, (x, y, i, j) =>
        {
            float cy = RopeY(x0, y0, x1, y1, sag, x), d = (y - cy) / thick;
            if (MathF.Abs(d) > 1) return Col.Clear;
            float twist = M.Fract(x * 9 + d * .55f + seed * .1f);
            float v = .3f + MathF.Sqrt(1 - d * d) * .35f + (twist < .3f ? -.22f : twist < .5f ? .12f : 0);
            return straw.At(v, i, j, .4f);
        });
    }

    /// <summary>A stack of flat prayer stones, each separated by a hairline gap so inflation rounds them individually.</summary>
    public static void Cairn(Canvas sil, float x, float baseY, float h, int seed)
    {
        var rng = new Rng(seed);
        int n = 4 + (int)(h * 4);
        float y = baseY, gap = 1.6f * sil.Px, w0 = h * .4f;
        for (int k = 0; k < n; k++)
        {
            float t = k / (float)n;
            float hw = w0 * (1 - t * .62f) * rng.R(.85f, 1.1f), hh = (h / n) * rng.R(.75f, 1) * .5f;
            float cx = x + (k > 0 ? rng.R(-1, 1) * w0 * .12f : 0), cy = y + hh, chh = MathF.Max(sil.Px, hh - gap * .5f);
            float pw = 2.6f + rng.F() * 1.5f;
            sil.Paint(cx - hw, cx + hw, cy - chh, cy + chh, (qx, qy, i, j) =>
            {
                float u = (qx - cx) / hw, v = (qy - cy) / chh;
                return MathF.Pow(MathF.Abs(u), pw) + MathF.Pow(MathF.Abs(v), 2.2f) < 1 ? Col.White : Col.Clear;
            });
            y += hh * 2;
        }
        // A small upright pebble on top.
        float tx = x + rng.R(-.03f, .03f), tr = w0 * .22f, ty = y + gap;
        sil.Paint(tx - tr, tx + tr, ty, ty + tr * 2.2f, (qx, qy, i, j) =>
        {
            float u = (qx - tx) / tr, v = (qy - ty - tr * 1.1f) / (tr * 1.1f);
            return u * u + v * v < 1 ? Col.White : Col.Clear;
        });
    }

    /// <summary>A table dolmen: two uprights leaning slightly inward under a long weathered capstone.</summary>
    public static void Dolmen(Canvas sil, float x, float baseY, float width, float legH, float capH, float tilt, int seed)
    {
        float hw = width * .5f, gap = 2 * sil.Px;
        foreach (var side in new[] { -1f, 1f })
        {
            float lx = x + side * hw * .56f, lw = width * .08f;
            sil.Paint(lx - lw - .2f, lx + lw + .2f, baseY, baseY + legH, (px, py, i, j) =>
            {
                float t = (py - baseY) / legH, cx = lx - side * t * legH * .06f;
                float u = (px - cx) / (lw * (1 + (1 - t) * .18f));
                return MathF.Abs(u) < 1 - MathF.Pow(t, 6) * .25f ? Col.White : Col.Clear;
            });
        }
        float cy = baseY + legH + gap + capH * .5f, chh = capH * .5f;
        sil.Paint(x - hw - .15f, x + hw + .15f, cy - chh - .25f, cy + chh + .35f, (px, py, i, j) =>
        {
            float u = (px - x) / hw, v = (py - (cy + (px - x) * tilt)) / chh;
            float bulge = 1 + sil.F.N(px, py, .5f, seed) * .3f + (v > 0 ? sil.F.N(px, 0, .25f, seed + 1) * .25f : 0);
            return MathF.Pow(MathF.Abs(u), 3.5f) + MathF.Pow(MathF.Abs(v), 2.6f) < bulge ? Col.White : Col.Clear;
        });
    }

    /// <summary>Blocky strokes assembled from hash bits, Hanja-like at a glance. lx,ly in the cell [0,1].</summary>
    public static bool Glyph(float lx, float ly, int row, int seed)
    {
        if (lx < .1f || lx > .9f || ly < .08f || ly > .92f) return false;
        uint h = Field.Hash(row, 3, seed); float sw = .12f;
        bool hit = false;
        if ((h & 1) != 0 && MathF.Abs(ly - .82f) < sw * .5f) hit = true;
        if ((h & 2) != 0 && MathF.Abs(ly - .5f) < sw * .5f && lx > .2f && lx < .8f) hit = true;
        if ((h & 4) != 0 && MathF.Abs(ly - .18f) < sw * .5f) hit = true;
        if ((h & 8) != 0 && MathF.Abs(lx - .5f) < sw * .5f) hit = true;
        if ((h & 16) != 0 && MathF.Abs(lx - .22f) < sw * .5f && ly > .3f) hit = true;
        if ((h & 32) != 0 && MathF.Abs(lx - .78f) < sw * .5f && ly < .7f) hit = true;
        if ((h & 64) != 0 && lx > .28f && lx < .72f && ly > .28f && ly < .66f && (MathF.Abs(lx - .3f) < sw * .5f || MathF.Abs(lx - .7f) < sw * .5f || MathF.Abs(ly - .3f) < sw * .5f || MathF.Abs(ly - .64f) < sw * .5f)) hit = true;
        if ((h & 128) != 0 && MathF.Abs((lx - .5f) + (ly - .5f) * .6f) < sw * .4f && ly < .8f && ly > .2f) hit = true;
        if ((h & 7) == 0 && (h & 56) == 0) hit |= MathF.Abs(ly - .5f) < sw * .5f || MathF.Abs(lx - .5f) < sw * .5f;
        return hit;
    }

    /// <summary>A jangseung village guardian post: hat or top-knot, bulging eyes, bulb nose, grinning teeth and a column of carved characters.</summary>
    public static void Jangseung(Canvas p, float x, float baseY, float h, bool male, Ramp wood, int seed)
    {
        var f = p.F;
        float r = h * .085f, top = baseY + h;
        float eyeY = top - r * 1.15f, noseY = top - r * 1.95f, mouthY = top - r * 2.8f, chinY = top - r * 3.45f;
        Col ink = Col.Hex("1a1510"), sclera = Col.Hex("ece4d0"), lip = Col.Hex("b03028"), teeth = Col.Hex("e6dcc4");
        p.Paint(x - r * 1.7f, x + r * 1.7f, baseY - .02f, top + r * 1.2f, (px, py, i, j) =>
        {
            float dx = px - x;
            if (py > top)
            {
                float ht = py - top;
                if (male)
                {
                    if (ht < r * .22f) return MathF.Abs(dx) < r * 1.55f ? wood.At(.28f - dx / r * .1f, i, j, .3f) : Col.Clear;
                    if (ht < r * .95f && MathF.Abs(dx) < r * 1.05f - ht * .25f) return wood.At(.4f - dx / r * .18f + (ht > r * .85f ? .15f : 0), i, j, .3f);
                    return Col.Clear;
                }
                float kx = dx / (r * .95f), ky = ht / (r * .75f);
                return kx * kx + ky * ky < 1 ? wood.At(.5f - kx * .22f, i, j, .3f) : Col.Clear;
            }
            float u = dx / r; if (MathF.Abs(u) > 1) return Col.Clear;
            float grain = f.Fbm2(px, py, .07f, 1.2f, 2, seed) * .1f;
            float v = .06f + Kit.Cylinder(u, .22f) * .74f + grain;
            foreach (var side in new[] { -1f, 1f })
            {
                float ex = dx - side * r * .45f, ey = py - eyeY, er = r * .31f, d2 = ex * ex + ey * ey;
                if (d2 < er * er)
                {
                    float pxx = ex - side * r * .05f, pyy = ey - r * .02f, pr = r * .15f;
                    if (pxx * pxx + pyy * pyy < pr * pr) return ink;
                    float rimR = er - 1.6f * p.Px;
                    if (d2 > rimR * rimR) return ink;
                    return Col.Lerp(sclera, Col.Black, ex > 0 ? .12f : 0);
                }
                float bx = ex, by = ey - r * .44f + bx * bx / r * 1.6f;
                if (MathF.Abs(bx) < r * .42f && MathF.Abs(by) < r * .1f) return ink;
            }
            float nx = dx / (r * .22f), ny = (py - noseY) / (r * .3f), nd = nx * nx + ny * ny;
            if (nd < 1) return nd > .78f ? wood.At(v - .25f, i, j, .3f) : wood.At(v + .18f - ny * .08f, i, j, .3f);
            float mx = MathF.Abs(dx) / (r * .8f);
            if (mx < 1)
            {
                float my = py - (mouthY + r * .3f * mx * mx), mh = r * .19f * (1 - mx * mx * .5f);
                if (MathF.Abs(my) < mh)
                {
                    if (MathF.Abs(my) > mh - 1.7f * p.Px || mx > .9f) return lip;
                    float tooth = M.Fract(dx / (r * .19f));
                    return tooth < .18f ? ink : teeth;
                }
            }
            if (male && py < mouthY - r * .25f && py > chinY - r * .9f && MathF.Abs(dx) < r * .7f && Field.Hash01(i / 2, seed, 5) > .55f - (py - (chinY - r * .9f)) / (r * .9f) * .2f) return ink;
            if (!male)
            {
                float c1 = dx - r * .62f, c2 = dx + r * .62f, cy = py - noseY;
                if (c1 * c1 + cy * cy < r * r * .012f || c2 * c2 + cy * cy < r * r * .012f) return Col.Lerp(lip, Col.White, .3f);
            }
            float inkTop = chinY - r * (male ? 1.1f : .4f), inkBottom = baseY + h * .12f;
            if (py < inkTop && py > inkBottom && MathF.Abs(u) < .62f)
            {
                float cellH = r * 1.05f, cw = r * 1.1f;
                int row = (int)MathF.Floor((inkTop - py) / cellH);
                float ly = 1 - M.Fract((inkTop - py) / cellH), lx = (dx + cw * .5f) / cw;
                if (Glyph(lx, ly, row, seed)) return ink;
            }
            return wood.At(v, i, j, .35f);
        });
    }

    /// <summary>Silhouette test for a tiered Korean pavilion standing on a platform; (dx, dy) relative to the platform's centre-bottom.</summary>
    public static bool Pavilion(float dx, float dy, float w, float h, int tiers = 2)
    {
        float ax = MathF.Abs(dx);
        if (dy < 0 || dy > h) return false;
        float plat = h * .09f;
        if (dy < plat) return ax < w * .56f;
        float tierH = (h - plat) / tiers;
        for (int t = 0; t < tiers; t++)
        {
            float ty = dy - (plat + t * tierH);
            if (ty < 0 || ty > tierH) continue;
            float shrink = 1 - t * .3f, eave = tierH * .48f;
            if (ty < eave)
            {
                float span = w * .34f * shrink;
                for (int k = -1; k <= 1; k++) if (MathF.Abs(dx - k * span) < w * .028f) return true;
                if (t > 0 && ty < tierH * .14f && ax < w * .42f * shrink) return true;
                if (t == 0 && ty > eave - tierH * .12f && ax < w * .4f) return true; // lintel beam
                return false;
            }
            float rt = (ty - eave) / (tierH - eave);
            float halfTop = w * .2f * shrink, halfEave = w * .6f * shrink;
            float half = halfEave + (halfTop - halfEave) * rt;
            float lift = MathF.Pow(ax / halfEave, 3) * (tierH - eave) * .32f;
            if (ax < half && ty - eave > lift) return true;
            if (rt > .8f && MathF.Abs(ax - halfTop) < w * .035f) return true;
        }
        return false;
    }

    /// <summary>A distant bird: two curved wing strokes.</summary>
    public static void Bird(Canvas p, float x, float y, float size, Col col)
    {
        p.Paint(x - size, x + size, y - size * .1f, y + size * .6f, (px, py, i, j) =>
        {
            float dx = MathF.Abs(px - x) / size, wy = y + size * .45f * MathF.Pow(dx, 1.6f);
            return dx <= 1 && MathF.Abs(py - wy) < p.Px * .9f ? col : Col.Clear;
        });
    }
}
