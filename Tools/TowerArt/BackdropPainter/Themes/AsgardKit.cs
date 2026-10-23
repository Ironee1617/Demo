namespace BackdropPainter;

/// <summary>Shape helpers for the Asgard theme: angular runes, round shields, carved dragon heads,
/// raven silhouettes, brazier flames, banner emblems and tapering limb strokes.</summary>
public static class AsgardKit
{
    // Stroke lists per invented rune, as segments (x0,y0,x1,y1) in cell coordinates u,v in [-1,1], v up.
    static readonly float[][] Runes =
    {
        new[] { 0, -1, 0, 1, 0, .3f, .7f, .9f, 0, -.25f, .7f, .35f },
        new[] { -.6f, -1, -.6f, 1, -.6f, 1, .6f, .45f, .6f, .45f, .6f, -1 },
        new[] { 0, -1, 0, 1, 0, .55f, .7f, .05f, .7f, .05f, 0, -.45f },
        new[] { -.7f, -.85f, .7f, .85f, -.7f, .85f, .7f, -.85f },
        new[] { 0, -1, 0, 1, 0, .85f, .7f, .35f, 0, .25f, .7f, -.25f },
        new[] { -.5f, 1, .5f, .4f, .5f, .4f, -.5f, -.3f, -.5f, -.3f, .5f, -1 },
        new[] { 0, -1, 0, 1, 0, 1, -.7f, .4f, 0, 1, .7f, .4f },
        new[] { 0, .85f, .7f, 0, .7f, 0, 0, -.85f, 0, -.85f, -.7f, 0, -.7f, 0, 0, .85f },
        new[] { 0, -1, 0, 1, -.6f, .5f, .6f, -.5f },
        new[] { 0, -1, 0, 1, 0, 1, .7f, .3f },
        new[] { -.6f, -1, -.6f, 1, .6f, -1, .6f, 1, -.6f, .25f, .6f, -.25f },
        new[] { 0, .9f, .7f, .15f, .7f, .15f, .25f, -.3f, 0, .9f, -.7f, .15f, -.7f, .15f, -.25f, -.3f, .25f, -.3f, .7f, -1, -.25f, -.3f, -.7f, -1 },
    };
    public static int RuneKinds => Runes.Length;

    public static float SegDist(float px, float py, float ax, float ay, float bx, float by)
    {
        float dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
        float t = l2 > 0 ? Math.Clamp(((px - ax) * dx + (py - ay) * dy) / l2, 0, 1) : 0;
        float ex = ax + dx * t - px, ey = ay + dy * t - py;
        return MathF.Sqrt(ex * ex + ey * ey);
    }

    /// <summary>Distance in cell units from (u,v) to the strokes of rune <paramref name="kind"/>.</summary>
    public static float Rune(float u, float v, int kind)
    {
        var s = Runes[M.Mod(kind, Runes.Length)]; float best = 9;
        for (int k = 0; k < s.Length; k += 4) best = Math.Min(best, SegDist(u, v, s[k], s[k + 1], s[k + 2], s[k + 3]));
        return best;
    }

    /// <summary>A vertical column of runes: cells <paramref name="cw"/> wide and <paramref name="ch"/> tall starting at y0.
    /// Returns the stroke distance in cell units (9 outside the column) and the cell row.</summary>
    public static float RuneColumn(float x, float y, float cx, float y0, float cw, float ch, int seed, out int row)
    {
        row = (int)MathF.Floor((y - y0) / ch);
        float u = (x - cx) / (cw * .5f), v = M.Fract((y - y0) / ch) * 2 - 1;
        if (MathF.Abs(u) > 1.3f) return 9;
        // Shrink the glyph a little inside the cell so neighbours never touch.
        return Rune(u * 1.25f, v * 1.35f, (int)(Field.Hash01(row, 3, seed) * Runes.Length));
    }

    public static bool Ellipse(float x, float y, float cx, float cy, float rx, float ry)
    { float dx = (x - cx) / rx, dy = (y - cy) / ry; return dx * dx + dy * dy < 1; }

    public static bool Capsule(float x, float y, float ax, float ay, float bx, float by, float r) => SegDist(x, y, ax, ay, bx, by) < r;

    /// <summary>Carved dragon head of size s with its neck root at (ox,oy). (ax,ay) is the world direction of the
    /// head's local +x (snout) axis and (bx,by) of its local +y (crest) axis. 0 miss, 1 head, 2 eye, 4 horn, 5 teeth.</summary>
    public static int Dragon(float px, float py, float ox, float oy, float s, float ax, float ay, float bx, float by, out float lx, out float ly)
    {
        float wx = px - ox, wy = py - oy;
        lx = (wx * ax + wy * ay) / s; ly = (wx * bx + wy * by) / s;
        if (lx < -.25f || lx > 1.1f || ly < -.2f || ly > .95f) return 0;
        if (Ellipse(lx, ly, .64f, .52f, .05f, .045f)) return 2;                                   // eye
        bool teeth = lx > .78f && lx < 1.0f && ly < .41f && ly > .41f - .04f * (1 - M.Fract((lx - .78f) / .07f));
        if (teeth) return 5;
        bool head = Ellipse(lx, ly, .56f, .47f, .33f, .19f) && !(lx > .72f && ly < .41f);          // skull, mouth cut open
        bool upperJaw = lx > .62f && lx < 1.06f && ly >= .41f && ly < .41f + .15f * (1 - (lx - .62f) / .44f) + .04f;
        bool lowerJaw = lx > .7f && lx < 1.0f && ly < .41f - .025f - (lx - .7f) * .18f && ly > .3f - (lx - .7f) * .45f;
        if (head || upperJaw || lowerJaw) return 1;
        bool neck = Capsule(lx, ly, 0, 0, .32f, .34f, .17f) || Capsule(lx, ly, .2f, .2f, .45f, .4f, .15f);
        if (neck) return 1;
        bool horn = Capsule(lx, ly, .44f, .62f, .3f, .8f, .075f) || Capsule(lx, ly, .3f, .8f, .17f, .72f, .055f)
                 || Capsule(lx, ly, .54f, .63f, .44f, .76f, .045f);
        return horn ? 4 : 0;
    }

    /// <summary>Round shield: 0 miss, 1 rim, 2 boss, 3 field colour a, 4 field colour b.</summary>
    public static int Shield(float dx, float dy, float r, int kind)
    {
        float d = MathF.Sqrt(dx * dx + dy * dy);
        if (d >= r) return 0;
        if (d > r * .86f) return 1;
        if (d < r * .22f) return 2;
        switch (kind & 3)
        {
            case 0: return dx < 0 ? 3 : 4;
            case 1: return (dx < 0) ^ (dy < 0) ? 3 : 4;
            case 2: return d < r * .58f ? 3 : 4;
            default: return ((int)MathF.Floor((MathF.Atan2(dy, dx) + MathF.PI) / (MathF.PI * 2) * 8) & 1) == 0 ? 3 : 4;
        }
    }

    /// <summary>Perched raven facing +x with its feet at the origin; s is the body length.</summary>
    public static bool RavenPerched(float px, float py, float ox, float oy, float s, int dir)
    {
        float lx = (px - ox) * dir / s, ly = (py - oy) / s;
        if (lx < -.6f || lx > .6f || ly < -.02f || ly > .6f) return false;
        // Body tilted nose-up.
        float ca = MathF.Cos(.32f), sa = MathF.Sin(.32f), bx = lx - .02f, by = ly - .25f;
        float rx = bx * ca + by * sa, ry = -bx * sa + by * ca;
        if (rx * rx / (.3f * .3f) + ry * ry / (.13f * .13f) < 1) return true;
        if (Ellipse(lx, ly, .3f, .44f, .1f, .09f)) return true;                       // head
        if (lx > .36f && lx < .54f && MathF.Abs(ly - .43f + (lx - .36f) * .15f) < .04f * (1 - (lx - .36f) / .18f)) return true; // beak
        if (lx < -.22f && lx > -.55f && MathF.Abs(ly - .2f - (lx + .22f) * .35f) < .03f + (-.22f - lx) * .16f) return true;  // tail
        if ((MathF.Abs(lx - .06f) < .018f || MathF.Abs(lx - .16f) < .018f) && ly < .14f) return true; // legs
        return false;
    }

    /// <summary>Flying raven centred at (ox,oy); flap in [-1,1] raises or lowers the wing tips.</summary>
    public static bool RavenFlying(float px, float py, float ox, float oy, float s, float flap)
    {
        float lx = (px - ox) / s, ly = (py - oy) / s;
        if (MathF.Abs(lx) > .75f || MathF.Abs(ly) > .5f) return false;
        if (Ellipse(lx, ly, 0, 0, .2f, .065f)) return true;
        if (Ellipse(lx, ly, .22f, .02f, .07f, .05f)) return true;
        if (lx > .27f && lx < .38f && MathF.Abs(ly - .01f) < .025f * (1 - (lx - .27f) / .11f)) return true;
        if (lx < -.15f && lx > -.38f && MathF.Abs(ly + .01f) < .03f + (-.15f - lx) * .25f) return true;
        for (int side = -1; side <= 1; side += 2)
        {
            float ax = lx * side; if (ax < 0) continue;
            float mid = .25f + flap * .12f, tip = .1f + flap * .4f;
            float wy = ax < .32f ? M.Lerp(0, mid, ax / .32f) : M.Lerp(mid, tip, (ax - .32f) / .38f);
            float w = ax < .32f ? .09f - ax * .1f : .06f * (1 - (ax - .32f) / .38f) + .012f;
            if (ax < .7f && MathF.Abs(ly - wy) < w) return true;
        }
        return false;
    }

    /// <summary>Teardrop flame over a bowl: 0 outside, else 1 outer .. 3 core.</summary>
    public static int Flame(Field f, float px, float py, float cx, float by, float h, float w, int seed)
    {
        float t = (py - by) / h;
        if (t < 0 || t > 1) return 0;
        float sway = f.N(px * .5f + seed, py, .5f, seed) * .25f * t + MathF.Sin(t * 7 + seed) * .05f * t;
        float half = w * .5f * MathF.Pow(1 - t, .7f) * (1 + f.N(px, py, .16f, seed + 1) * .45f) * M.Smooth(0, .12f, t + .06f);
        float u = MathF.Abs(px - cx - sway) / Math.Max(half, 1e-3f);
        if (u > 1) return 0;
        if (t > .45f && f.Ridge(px, py * .7f, .22f, 2, seed + 2) > .9f) return 0; // licking tongues
        float v = (1 - u) * (1 - t * .55f);
        return v > .6f ? 3 : v > .33f ? 2 : 1;
    }

    /// <summary>Mjolnir (kind 0) or a triple-triangle knot (kind 1) emblem inside a unit cell u,v in [-1,1].</summary>
    public static bool Emblem(float u, float v, int kind, float stroke = .1f)
    {
        if (kind == 0)
        {
            bool handle = MathF.Abs(u) < .12f && v > -.95f && v < .2f;
            float side = .6f - MathF.Abs(v - .5f) * .25f;
            bool head = MathF.Abs(u) < side && v > .15f && v < .85f;
            return handle || head;
        }
        float best = 9;
        float[][] tris =
        {
            new[] { 0, .7f, -.6f, -.3f, .6f, -.3f },
            new[] { -.33f, .12f, -.93f, -.88f, .27f, -.88f },
            new[] { .33f, .12f, -.27f, -.88f, .93f, -.88f },
        };
        foreach (var t in tris)
            for (int k = 0; k < 3; k++)
            {
                int n = (k + 1) % 3;
                best = Math.Min(best, SegDist(u, v, t[k * 2], t[k * 2 + 1], t[n * 2], t[n * 2 + 1]));
            }
        return best < stroke;
    }

    /// <summary>A tapering stroke along a polyline; thickness runs from t0 at the start to t1 at the end.</summary>
    public sealed class Limb
    {
        public readonly List<(float x, float y)> Pts = new();
        public float T0, T1, Len;
        public float X0 = 1e9f, X1 = -1e9f, Y0 = 1e9f, Y1 = -1e9f;
        public void Add(float x, float y)
        {
            if (Pts.Count > 0) { var p = Pts[^1]; Len += MathF.Sqrt((x - p.x) * (x - p.x) + (y - p.y) * (y - p.y)); }
            Pts.Add((x, y));
            X0 = Math.Min(X0, x); X1 = Math.Max(X1, x); Y0 = Math.Min(Y0, y); Y1 = Math.Max(Y1, y);
        }
        /// <summary>Half-width at the point of the polyline nearest (x,y), or -1 when outside the stroke.</summary>
        public float Hit(float x, float y, out float along)
        {
            float best = 9, run = 0, bestRun = 0; along = 0;
            for (int k = 1; k < Pts.Count; k++)
            {
                var a = Pts[k - 1]; var b = Pts[k];
                float dx = b.x - a.x, dy = b.y - a.y, l2 = dx * dx + dy * dy, seg = MathF.Sqrt(l2);
                float t = l2 > 0 ? Math.Clamp(((x - a.x) * dx + (y - a.y) * dy) / l2, 0, 1) : 0;
                float ex = a.x + dx * t - x, ey = a.y + dy * t - y, d = MathF.Sqrt(ex * ex + ey * ey);
                if (d < best) { best = d; bestRun = run + seg * t; }
                run += seg;
            }
            along = Len > 0 ? bestRun / Len : 0;
            float half = M.Lerp(T0, T1, along) * .5f;
            return best < half ? half : -1;
        }
    }

    /// <summary>Quadratic Bezier polyline from a through control c to b.</summary>
    public static Limb Bezier(float ax, float ay, float cx, float cy, float bx, float by, float t0, float t1, int steps = 24)
    {
        var l = new Limb { T0 = t0, T1 = t1 };
        for (int k = 0; k <= steps; k++)
        {
            float t = k / (float)steps, s = 1 - t;
            l.Add(s * s * ax + 2 * s * t * cx + t * t * bx, s * s * ay + 2 * s * t * cy + t * t * by);
        }
        return l;
    }

    /// <summary>Point on a quadratic Bezier.</summary>
    public static (float x, float y) BezierAt(float ax, float ay, float cx, float cy, float bx, float by, float t)
    {
        float s = 1 - t;
        return (s * s * ax + 2 * s * t * cx + t * t * bx, s * s * ay + 2 * s * t * cy + t * t * by);
    }

    /// <summary>Paints the union of limbs as a white mask onto <paramref name="p"/>.</summary>
    public static void PaintLimbs(Canvas p, IEnumerable<Limb> limbs, Col col)
    {
        foreach (var l in limbs)
        {
            float pad = Math.Max(l.T0, l.T1) * .5f + .1f;
            var lim = l;
            p.Paint(l.X0 - pad, l.X1 + pad, l.Y0 - pad, l.Y1 + pad, (x, y, i, j) => lim.Hit(x, y, out _) >= 0 ? col : Col.Clear);
        }
    }
}
