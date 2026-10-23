namespace BackdropPainter;

/// <summary>Garden helpers for the Eden planes: blossoms, fruit, butterflies, limbs, streams, falls and lilies.</summary>
public static class EdenKit
{
    /// <summary>Soft diagonal light rays like <see cref="Kit.Shafts"/> but composited (not additive), so they
    /// stay pale and translucent over empty air instead of leaving dark premultiplied specks.</summary>
    public static void Rays(Canvas c, float angleDeg, float spacing, float width, Col col, float strength, float y0, float y1, int seed, Func<float, float, float> mask = null)
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
        });
    }

    /// <summary>A flat flower seen face-on: petal lobes around a centre disc. rot turns the lobes.</summary>
    public static void Blossom(Canvas p, float cx, float cy, float r, Col petal, Col shade, Col centre, int petals = 5, float rot = 0, float alpha = 1)
    {
        p.Paint(cx - r, cx + r, cy - r, cy + r, (x, y, i, j) =>
        {
            float dx = x - cx, dy = y - cy, d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > r) return Col.Clear;
            float ang = MathF.Atan2(dy, dx) + rot;
            float lobe = .6f + .4f * MathF.Abs(MathF.Cos(ang * petals * .5f));
            if (d > r * lobe) return Col.Clear;
            if (d < r * .3f) return centre.WithA(alpha);
            float v = (d / r - .3f) / .7f;
            bool dark = v < .3f || (dx > 0 && dy < 0 && Dither.Hit(.5f, i, j)) || d > r * lobe - 1.1f / p.Ppu;
            return (dark ? shade : petal).WithA(alpha);
        });
    }

    /// <summary>A shaded sphere with a specular glint and an optional stem: apple, pear or golden fruit.</summary>
    public static void Fruit(Canvas p, float cx, float cy, float r, Ramp ramp, Col glint, float stem = 0)
    {
        p.Paint(cx - r, cx + r, cy - r, cy + r + stem, (x, y, i, j) =>
        {
            float dx = (x - cx) / r, dy = (y - cy) / r, d2 = dx * dx + dy * dy;
            if (d2 >= 1)
            {
                if (stem > 0 && MathF.Abs(x - cx) < .6f / p.Ppu && y > cy && y < cy + r + stem) return ramp[0];
                return Col.Clear;
            }
            float nz = MathF.Sqrt(1 - d2);
            float v = .12f + Kit.Lambert(dx, dy, nz) * .8f;
            if (d2 > .7f && dx * Kit.Key.X + dy * Kit.Key.Y < 0) v -= .15f;
            if ((dx + .36f) * (dx + .36f) + (dy - .4f) * (dy - .4f) < .045f) return glint;
            return ramp.At(v, i, j, .35f);
        });
    }

    /// <summary>A tiny butterfly: fore- and hindwing lobes either side of a dark body. fold narrows the wings.</summary>
    public static void Butterfly(Canvas p, float cx, float cy, float size, Col wing, Col edge, float fold = 1)
    {
        float w = Math.Max(.2f, fold);
        p.Paint(cx - size * 1.1f, cx + size * 1.1f, cy - size * .8f, cy + size * .8f, (x, y, i, j) =>
        {
            float dx = x - cx, dy = y - cy;
            if (MathF.Abs(dx) < .6f / p.Ppu && MathF.Abs(dy) < size * .5f) return edge;
            float ax = MathF.Abs(dx) / (size * w);
            float fx = (ax - .55f) / .5f, fy = (dy - size * .18f) / (size * .46f);
            float hx = (ax - .4f) / .4f, hy = (dy + size * .3f) / (size * .34f);
            float fd = fx * fx + fy * fy, hd = hx * hx + hy * hy;
            if (fd >= 1 && hd >= 1) return Col.Clear;
            bool rim = fd > .5f && hd > .5f;
            return rim ? edge : wing;
        });
    }

    /// <summary>Signed distance helpers along a polyline: nearest squared distance, parameter and side.</summary>
    static void Nearest((float x, float y)[] pts, float x, float y, out float d2, out float t, out float side)
    {
        d2 = float.MaxValue; t = 0; side = 1; int n = pts.Length - 1;
        for (int s = 0; s < n; s++)
        {
            float ax = pts[s].x, ay = pts[s].y, vx = pts[s + 1].x - ax, vy = pts[s + 1].y - ay, len2 = vx * vx + vy * vy;
            float u = len2 > 0 ? Math.Clamp(((x - ax) * vx + (y - ay) * vy) / len2, 0, 1) : 0;
            float px = ax + vx * u - x, py = ay + vy * u - y, d = px * px + py * py;
            if (d < d2) { d2 = d; t = (s + u) / n; side = vx * (y - ay) - vy * (x - ax) >= 0 ? 1 : -1; }
        }
    }

    public static (float x, float y)[] Curve(float x0, float y0, float mx, float my, float x1, float y1, int steps = 28)
    {
        var pts = new (float x, float y)[steps + 1];
        for (int s = 0; s <= steps; s++)
        {
            float t = s / (float)steps, u = 1 - t;
            pts[s] = (u * u * x0 + 2 * u * t * mx + t * t * x1, u * u * y0 + 2 * u * t * my + t * t * y1);
        }
        return pts;
    }

    /// <summary>A tapering limb along a polyline; the shader receives t along the limb and u across in [-1,1].</summary>
    public static void Limb(Canvas p, (float x, float y)[] pts, float w0, float w1, Func<float, float, int, int, float, float, Col> shade)
    {
        float w = Math.Max(w0, w1);
        float lo = pts.Min(q => q.x) - w, hi = pts.Max(q => q.x) + w, y0 = pts.Min(q => q.y) - w, y1 = pts.Max(q => q.y) + w;
        p.Paint(lo, hi, y0, y1, (x, y, i, j) =>
        {
            Nearest(pts, x, y, out float d2, out float t, out float side);
            float hw = M.Lerp(w0, w1, t), dist = MathF.Sqrt(d2);
            if (dist > hw) return Col.Clear;
            return shade(x, y, i, j, t, side * dist / hw);
        });
    }

    /// <summary>A winding stream along a polyline; width tapers from w0 to w1. Bright centre, darker banks, glints.</summary>
    public static void Stream(Canvas p, (float x, float y)[] pts, float w0, float w1, Col deep, Col shallow, Col glint, int seed)
    {
        var f = p.F; float w = Math.Max(w0, w1);
        float lo = pts.Min(q => q.x) - w, hi = pts.Max(q => q.x) + w, y0 = pts.Min(q => q.y) - w, y1 = pts.Max(q => q.y) + w;
        p.Paint(lo, hi, y0, y1, (x, y, i, j) =>
        {
            Nearest(pts, x, y, out float d2, out float t, out _);
            float hw = M.Lerp(w0, w1, t) * .5f * (1 + f.N(x, y, .7f, seed) * .3f), dist = MathF.Sqrt(d2);
            if (dist > hw) return Col.Clear;
            float k = dist / hw;
            float rip = f.Fbm2(x, y, .45f, .05f, 2, seed + 1);
            if (rip > .42f && k < .65f) return glint;
            return Kit.Mix(shallow, deep, M.Smooth(.45f, 1, k) + (rip < -.3f ? .3f : 0), i, j, 3);
        });
    }

    /// <summary>A falling ribbon of water with streaks; slightly wider and whiter toward the bottom.</summary>
    public static void Waterfall(Canvas p, float x, float top, float bottom, float width, Col deep, Col bright, int seed, float alpha = .92f)
    {
        var f = p.F;
        p.Paint(x - width, x + width, bottom, top, (px, py, i, j) =>
        {
            float t = (top - py) / Math.Max(.01f, top - bottom);
            float cx = x + f.N(0, py, 2, seed) * width * .15f, half = width * .5f * (1 + t * .3f), d = MathF.Abs(px - cx) / half;
            if (d > 1) return Col.Clear;
            float n = f.Fbm2(px, py, width * .22f, 1.2f, 2, seed + 1) * .5f + .5f;
            float v = M.Smooth(.3f, .75f, n) * (1 - d * .45f) + t * .25f;
            return Kit.Mix(deep, bright, v, i, j, 3).WithA(alpha);
        });
    }

    /// <summary>A floating lily pad with a notch, optionally carrying a pointed-petal lily.</summary>
    public static void LilyPad(Canvas p, float cx, float cy, float rx, float ry, Ramp leaf, float notch, Col? petal, Col petalShade, Col centre)
    {
        p.Paint(cx - rx, cx + rx, cy - ry, cy + ry, (x, y, i, j) =>
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry, d2 = dx * dx + dy * dy;
            if (d2 >= 1) return Col.Clear;
            float ang = MathF.Atan2(dy, dx);
            if (MathF.Abs(ang - notch) < .32f) return Col.Clear;
            float v = .55f - dx * .15f + dy * .2f + (d2 > .72f ? -.2f : 0);
            return leaf.At(v, i, j, .3f);
        });
        if (petal.HasValue) Blossom(p, cx + rx * .15f, cy + ry * .9f, rx * .42f, petal.Value, petalShade, centre, 7, .3f);
    }

    /// <summary>A slender serpent along a polyline: shaded body, a lighter belly line, a head and a bright eye.</summary>
    public static void Serpent(Canvas p, (float x, float y)[] pts, float thick, Ramp body, Col eye)
    {
        float lo = pts.Min(q => q.x) - thick * 2, hi = pts.Max(q => q.x) + thick * 2, y0 = pts.Min(q => q.y) - thick * 2, y1 = pts.Max(q => q.y) + thick * 2;
        var head = pts[^1];
        p.Paint(lo, hi, y0, y1, (x, y, i, j) =>
        {
            Nearest(pts, x, y, out float d2, out float t, out float side);
            float hw = thick * (t < .08f ? t / .08f * .6f + .3f : 1), dist = MathF.Sqrt(d2);
            float hx = (x - head.x) / (thick * 1.9f), hy = (y - head.y) / (thick * 1.3f);
            bool inHead = hx * hx + hy * hy < 1;
            if (dist > hw && !inHead) return Col.Clear;
            if (inHead && (x - head.x - thick * .5f) * (x - head.x - thick * .5f) + (y - head.y - thick * .35f) * (y - head.y - thick * .35f) < (thick * .3f) * (thick * .3f)) return eye;
            float u = inHead ? hy : side * dist / hw;
            float v = Kit.Cylinder(u, .25f) * .75f + (M.Fract(t * 26) < .5f ? .06f : -.04f);
            if (!inHead && u < -.35f) v += .18f;
            return body.At(v, i, j, .35f);
        });
    }

    /// <summary>A pale-barked flowering fruit tree: tapered trunk with root flare, three limbs, a leaf-clump
    /// canopy, then blossoms and fruit scattered through the crown.</summary>
    public static void FloweringTree(Canvas p, float x, float baseY, float height, float rx, float ry, Ramp bark, Ramp leaf,
        Col petal, Col petalShade, Col petalCentre, int blossoms, Ramp fruit, Col glint, int fruits, int seed, float lean = 0, float bright = 0, int clumps = 7, int leavesPer = 220)
    {
        var f = p.F; var rng = new Rng(seed);
        float trunkTop = baseY + height * .58f, cx = x + lean * height * .35f, cy = baseY + height * .72f;
        float w = height * .075f;
        p.Paint(x - w * 3, x + w * 3 + lean * height, baseY - .05f, trunkTop + .2f, (px, py, i, j) =>
        {
            float t = (py - baseY) / (trunkTop - baseY); if (t < 0 || t > 1.1f) return Col.Clear;
            float axis = x + lean * height * .35f * t * t + MathF.Sin(t * 4 + seed) * w * .25f;
            float flare = MathF.Pow(M.Smooth(.3f, 0, t), 2) * w * 1.6f;
            float hw = w * (1 - t * .4f) + flare;
            float u = (px - axis) / hw; if (MathF.Abs(u) > 1) return Col.Clear;
            float v = Kit.Cylinder(u, .22f) * .78f + f.Fbm(px * 2, py * .6f, .35f, 2, seed + 1) * .14f;
            if (f.Ridge(px * 2.2f, py * .5f, .5f, 2, seed + 2) > .92f) v -= .18f;
            return bark.At(v, i, j, .4f);
        });
        foreach (var (ox, oy) in new[] { (-.7f, .35f), (.65f, .4f), (0f, .7f) })
        {
            var pts = Curve(cx - lean * height * .05f, trunkTop - .25f, cx + ox * rx * .5f, trunkTop + ry * .3f, cx + ox * rx, cy + oy * ry);
            Limb(p, pts, w * .55f, w * .18f, (px, py, i, j, t, u) => bark.At(Kit.Cylinder(u, .22f) * .72f - t * .1f, i, j, .4f));
        }
        var cl = new List<(float x, float y, float rx, float ry, float z)>();
        for (int k = 0; k < clumps; k++)
        {
            float ang = rng.R(0, MathF.PI * 2), rad = MathF.Sqrt(rng.F()) * .72f;
            float lx = cx + MathF.Cos(ang) * rad * rx, ly = cy + MathF.Sin(ang) * rad * ry;
            cl.Add((lx, ly, rx * rng.R(.38f, .55f), ry * rng.R(.42f, .6f), ly));
        }
        cl.Add((cx, cy + ry * .1f, rx * .6f, ry * .6f, cy + ry * .1f));
        foreach (var (lx, ly, crx, cry, _) in cl.OrderByDescending(c => c.z))
            Kit.LeafClump(p, lx, ly, crx, cry, leavesPer, leaf, seed * 7 + (int)(lx * 13), .24f, .35f, bright + M.Clamp01((ly - cy) / ry) * .12f);
        for (int k = 0; k < blossoms; k++)
        {
            float ang = rng.R(0, MathF.PI * 2), rad = MathF.Sqrt(rng.F());
            float bx = cx + MathF.Cos(ang) * rad * rx * .95f, by = cy + MathF.Sin(ang) * rad * ry * .95f;
            if (!p.Solid(p.I(bx), p.J(by))) continue;
            Blossom(p, bx, by, rng.R(.05f, .085f), petal, petalShade, petalCentre, 5, rng.R(0, 3));
        }
        for (int k = 0; k < fruits; k++)
        {
            float ang = rng.R(0, MathF.PI * 2), rad = MathF.Sqrt(rng.F());
            float bx = cx + MathF.Cos(ang) * rad * rx * .9f, by = cy + MathF.Sin(ang) * rad * ry * .9f - ry * .1f;
            if (!p.Solid(p.I(bx), p.J(by))) continue;
            Fruit(p, bx, by, rng.R(.06f, .09f), fruit, glint, .04f);
        }
    }

    /// <summary>A tall garden flower on a stem: daisy, bell spike or cup, in the given pastel.</summary>
    public static void TallFlower(Canvas p, float x, float baseY, float height, int kind, Col petal, Col shade, Col centre, Ramp stem, int seed)
    {
        var rng = new Rng(seed);
        float sway = rng.R(-.12f, .12f), topX = x + sway * height, topY = baseY + height;
        p.Paint(x - .3f, x + .3f, baseY, topY, (px, py, i, j) =>
        {
            float t = (py - baseY) / height, axis = x + sway * height * t * t;
            if (MathF.Abs(px - axis) < .7f / p.Ppu) return stem[1];
            // A pair of slim leaves low on the stem.
            float ly = baseY + height * .3f, dx = px - axis, dy = py - ly;
            if (dy > -.02f && dy < .1f && MathF.Abs(dx) < .22f && MathF.Abs(dy - MathF.Abs(dx) * .35f) < .03f * (1 - MathF.Abs(dx) / .22f) + .006f) return stem[dx < 0 ? 3 : 2];
            return Col.Clear;
        });
        switch (kind)
        {
            case 0: Blossom(p, topX, topY, rng.R(.1f, .15f), petal, shade, centre, 8, rng.R(0, 1)); break;
            case 1:
                for (int k = 0; k < 7; k++)
                {
                    float t = .55f + k * .07f, by = baseY + height * t, bx = x + sway * height * t * t + (k % 2 == 0 ? .06f : -.06f) * (1 - k / 9f);
                    Blossom(p, bx, by, .045f * (1 - k / 14f), petal, shade, centre, 4, k * .7f);
                }
                break;
            default:
                p.Paint(topX - .16f, topX + .16f, topY - .05f, topY + .3f, (px, py, i, j) =>
                {
                    float dx = px - topX, dy = py - topY;
                    if (dy < 0 || dy > .28f) return Col.Clear;
                    float hw = .06f + dy * .35f;
                    if (MathF.Abs(dx) > hw) return Col.Clear;
                    // Three pointed petals: the middle one in front.
                    float tip = .28f - MathF.Abs(MathF.Sin(dx / hw * MathF.PI * 1.5f)) * .06f;
                    if (dy > tip) return Col.Clear;
                    bool mid = MathF.Abs(dx) < hw * .35f;
                    return (mid ? petal : shade).WithA(1);
                });
                p.Paint(topX - .05f, topX + .05f, topY + .12f, topY + .2f, (px, py, i, j) => MathF.Abs(px - topX) < .012f ? centre : Col.Clear);
                break;
        }
    }
}
