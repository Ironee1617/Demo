using System.Numerics;

namespace BackdropPainter.Render;

/// <summary>Nearest-surface accumulator for composite shapes with several materials.</summary>
public struct Acc
{
    public float D; public int M;
    public static Acc Start => new() { D = 1e9f, M = -1 };
    public void Add(float d, int m) { if (d < D) { D = d; M = m; } }
}

/// <summary>Art-directed sky: gradient stops by image height, sun disc and halo, optional cirrus.</summary>
public sealed class Sky
{
    public (float v, Vector3 c)[] Stops;
    public float SunX, SunY, SunRadius = .45f, HaloRadius = 5;
    public Vector3 SunColor = new(30, 26, 20), HaloColor = new(1.2f, .9f, .5f);
    public float CirrusAmount, CirrusLow = 2, CirrusHigh = 12; public Vector3 CirrusColor = new(1.2f, 1.1f, 1);
    /// <summary>Night sky extras: star density and aurora curtains (emissive, linear radiance).</summary>
    public float Stars, StarsBelow = 0, Aurora, AuroraLow = 2, AuroraHigh = 9;
    public Vector3 AuroraLowColor = new(.2f, 1.6f, .7f), AuroraHighColor = new(.9f, .3f, 1.1f);
    public float Period; public Noise N;
    public Sky(float period, params (float v, string hex)[] stops)
    { Period = period; N = new Noise(period); Stops = stops.Select(s => (s.v, R.Rgb(s.hex))).ToArray(); }

    public Vector3 Gradient(float v)
    {
        if (v <= Stops[0].v) return Stops[0].c;
        for (int i = 1; i < Stops.Length; i++)
            if (v <= Stops[i].v) return R.Mix(Stops[i - 1].c, Stops[i].c, R.Smooth(0, 1, (v - Stops[i - 1].v) / (Stops[i].v - Stops[i - 1].v)));
        return Stops[^1].c;
    }

    public Vector3 At(float x, float v)
    {
        var c = Gradient(v);
        float dx = R.Wrap(x, SunX, Period), dy = v - SunY, d = MathF.Sqrt(dx * dx + dy * dy);
        c += HaloColor * (MathF.Exp(-d / HaloRadius) * .6f + MathF.Exp(-d * d / (HaloRadius * HaloRadius * .08f)) * .8f);
        if (d < SunRadius * 1.15f) c = R.Mix(c, SunColor, R.Smooth(SunRadius * 1.15f, SunRadius * .95f, d));
        if (Stars > 0 && v > StarsBelow)
        {
            // Hashed point stars on a fine grid, twinkle-free, fading toward the horizon.
            float g = 18; int gx = (int)MathF.Floor(R.Mod(x, Period) * g), gy = (int)MathF.Floor(v * g);
            uint h = R.Hash(gx % (int)(Period * g), gy, 0, 91);
            if ((h & 1023) < Stars * 1023)
            {
                float sx = (gx + .2f + ((h >> 10) & 255) / 255f * .6f) / g, sy = (gy + .2f + ((h >> 18) & 255) / 255f * .6f) / g;
                float sd = MathF.Sqrt(MathF.Pow(R.Mod(x, Period) - sx, 2) + MathF.Pow(v - sy, 2)) * g;
                float bright = MathF.Pow(((h >> 26) & 63) / 63f, 3) * 6 + .4f;
                c += new Vector3(.9f, .95f, 1.1f) * bright * MathF.Exp(-sd * sd * 40) * R.Smooth(StarsBelow, StarsBelow + 3, v);
            }
        }
        if (Aurora > 0 && v > AuroraLow - 1 && v < AuroraHigh + 2)
        {
            // Curtains: a wandering baseline with vertical rays rising from it, green at the foot, violet on top.
            float baseY = AuroraLow + 1.5f + N.Fbm(x, 0, 9, 3, 61) * 2.2f;
            float rise = v - baseY;
            if (rise > -.4f)
            {
                float rays = MathF.Pow(N.Fbm(x * 1, 0, .35f, 4, 62) * .5f + .5f, 2.2f);
                float fold = N.Fbm(x, v * .3f, 3, 3, 63) * .5f + .5f;
                float foot = MathF.Exp(-MathF.Max(0, -rise) * 9) * MathF.Exp(-MathF.Max(0, rise) / (1.4f + fold * 1.4f));
                // Ribbons: the curtain thins out where a slow lateral mask closes it.
                float ribbon = R.Smooth(.35f, .65f, N.Fbm(x, 0, 6, 2, 64) * .5f + .5f);
                float a = foot * (.25f + rays * 1.2f) * ribbon * Aurora * R.Smooth(AuroraHigh + 2, AuroraHigh, v);
                c += R.Mix(AuroraLowColor, AuroraHighColor, R.Smooth(.3f, 3.2f, rise)) * a;
            }
        }
        if (CirrusAmount > 0 && v > CirrusLow && v < CirrusHigh)
        {
            float band = R.Smooth(CirrusLow, CirrusLow + 2, v) * R.Smooth(CirrusHigh, CirrusHigh - 2, v);
            var q = new Vector3(x, v * 3.2f, 0);
            float w = N.Warped(q, 4, 5, 7, 1.2f) * .5f + .5f;
            float streak = R.Smooth(.5f, .85f, w) * band * CirrusAmount;
            var lit = CirrusColor * (1 + MathF.Exp(-d / (HaloRadius * 1.5f)) * 2);
            c = R.Mix(c, lit, streak * .6f);
        }
        return c;
    }
}

/// <summary>Reusable realistic building blocks: architecture, trees, terrain and clouds.</summary>
public static class Kit3D
{
    // ---------- Architecture ----------

    /// <summary>Tapered fluted shaft standing on y=0 at the local origin.</summary>
    public static float FlutedShaft(Vector3 q, float r0, float r1, float height, int flutes, float depth)
    {
        float t = R.Clamp01(q.Y / height), r = R.Mix(r0, r1, t);
        float ang = MathF.Atan2(q.Z, q.X);
        float f = depth * MathF.Pow(MathF.Max(0, MathF.Cos(ang * flutes)), 2);
        float rr = MathF.Sqrt(q.X * q.X + q.Z * q.Z) - (r - f);
        float dy = MathF.Abs(q.Y - height * .5f) - height * .5f;
        return MathF.Max(rr * .85f, dy);
    }

    /// <summary>A classical column with square plinth, torus base, fluted shaft, echinus and abacus. Origin at its foot.</summary>
    public static float Column(Vector3 q, float radius, float height, int flutes = 20, bool doric = false)
    {
        float plinth = Sdf.Box(q - new Vector3(0, radius * .18f, 0), new Vector3(radius * 1.35f, radius * .18f, radius * 1.35f));
        float torus = doric ? 1e9f : Sdf.TorusY(q - new Vector3(0, radius * .48f, 0), radius * 1.02f, radius * .17f);
        float shaftH = height - radius * (doric ? 1.1f : 1.6f);
        float shaft = FlutedShaft(q - new Vector3(0, radius * .36f, 0), radius, radius * .83f, shaftH, flutes, radius * .07f);
        float top = radius * .36f + shaftH;
        float echinus = Sdf.ConeY(q - new Vector3(0, top + radius * .16f, 0), radius * .16f, radius * .86f, radius * 1.18f) - .01f;
        float abacus = Sdf.Box(q - new Vector3(0, top + radius * .46f, 0), new Vector3(radius * 1.3f, radius * .13f, radius * 1.3f));
        float volute = doric ? 1e9f : MathF.Min(Sdf.TorusZ(q - new Vector3(radius * 1.15f, top + radius * .25f, radius * .2f), radius * .22f, radius * .07f),
                                                Sdf.TorusZ(q - new Vector3(-radius * 1.15f, top + radius * .25f, radius * .2f), radius * .22f, radius * .07f));
        return MathF.Min(MathF.Min(MathF.Min(plinth, torus), MathF.Min(shaft, echinus)), MathF.Min(abacus, volute));
    }

    /// <summary>Stepped stylobate: <paramref name="steps"/> courses rising to y=h, each inset by <paramref name="inset"/>.</summary>
    public static float Steps(Vector3 q, float halfW, float halfD, float h, int steps, float inset)
    {
        float d = 1e9f, sh = h / steps;
        for (int k = 0; k < steps; k++)
            d = MathF.Min(d, Sdf.Box(q - new Vector3(0, sh * (k + .5f), 0), new Vector3(halfW - inset * k, sh * .5f, halfD - inset * k)) - .004f);
        return d;
    }

    /// <summary>
    /// Procedural runes on a 2D band: each cell carries a vertical stave with one to three hashed branches, like
    /// Elder Futhark. Returns 1 on a stroke, 0 elsewhere, with a soft edge.
    /// </summary>
    public static float Runes(float u, float v, float cellW, float cellH, float stroke, int seed)
    {
        int cx = (int)MathF.Floor(u / cellW), cy = (int)MathF.Floor(v / cellH);
        float lx = (u - cx * cellW) / cellW, ly = (v - cy * cellH) / cellH;
        uint h = R.Hash(cx, cy, 0, seed);
        if ((h & 7) == 0) return 0; // a word gap
        float px = lx * cellW, py = ly * cellH, best = 9;
        float x0 = cellW * .5f, y0 = cellH * .12f, y1 = cellH * .88f;
        best = MathF.Min(best, Seg(px, py, x0, y0, x0, y1));
        int branches = 1 + (int)((h >> 3) % 3);
        for (int b = 0; b < branches; b++)
        {
            uint k = (h >> (5 + b * 6)) & 63;
            float ya = cellH * (.2f + (k & 7) / 7f * .6f), dir = (k & 8) != 0 ? 1 : -1, slope = (k & 16) != 0 ? .7f : 0;
            float yb = ya + (slope > 0 ? cellH * .25f * ((k & 32) != 0 ? 1 : -1) : 0);
            best = MathF.Min(best, Seg(px, py, x0, ya, x0 + dir * cellW * .38f, yb));
        }
        return R.Smooth(stroke, stroke * .45f, best);
        static float Seg(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay, t = R.Clamp01(((px - ax) * dx + (py - ay) * dy) / MathF.Max(1e-6f, dx * dx + dy * dy));
            float qx = ax + dx * t - px, qy = ay + dy * t - py;
            return MathF.Sqrt(qx * qx + qy * qy);
        }
    }

    /// <summary>
    /// Procedural hieroglyphs in columns of cells: ankh, eye, sun disc, water ripples, reeds, a bird and a seated
    /// figure, picked per cell by hash. Returns the glyph index + 1 where a stroke covers (u, v), 0 elsewhere.
    /// </summary>
    public static int Hieroglyph(float u, float v, float cell, int seed, out float edge)
    {
        int cx = (int)MathF.Floor(u / cell), cy = (int)MathF.Floor(v / cell);
        float x = (u - cx * cell) / cell - .5f, y = (v - cy * cell) / cell - .5f;
        uint h = R.Hash(cx, cy, 0, seed); int kind = (int)(h % 8);
        float d = 9;
        switch (kind)
        {
            case 0: // ankh
                d = MathF.Min(MathF.Abs(MathF.Sqrt(x * x + (y - .22f) * (y - .22f) * 1.6f) - .14f) - .04f, MathF.Max(MathF.Abs(x) - .045f, MathF.Abs(y + .14f) - .26f));
                d = MathF.Min(d, MathF.Max(MathF.Abs(x) - .22f, MathF.Abs(y - .02f) - .04f)); break;
            case 1: // eye
                d = MathF.Abs(MathF.Sqrt(x * x * .7f + y * y * 4) - .2f) - .035f; d = MathF.Min(d, MathF.Sqrt(x * x + y * y) - .07f);
                d = MathF.Min(d, MathF.Max(MathF.Abs(x + .05f) - .03f, MathF.Abs(y + .2f) - .12f)); break;
            case 2: // sun disc
                d = MathF.Sqrt(x * x + y * y) - .2f; break;
            case 3: // water ripples
                d = MathF.Abs(R.Mod(y + .5f + MathF.Sin(x * 25) * .03f, .22f) - .11f) - .03f; d = MathF.Max(d, MathF.Abs(x) - .38f); break;
            case 4: // reed
                d = MathF.Min(MathF.Max(MathF.Abs(x - MathF.Sin(y * 4) * .05f) - .03f, MathF.Abs(y) - .38f), MathF.Sqrt((x - .08f) * (x - .08f) + (y - .3f) * (y - .3f) * 3) - .07f); break;
            case 5: // bird
                d = MathF.Min(MathF.Sqrt(x * x * 1.4f + (y + .02f) * (y + .02f) * 2.6f) - .2f, MathF.Sqrt((x + .16f) * (x + .16f) + (y - .2f) * (y - .2f)) - .08f);
                d = MathF.Min(d, MathF.Max(MathF.Abs(x - .05f) - .025f, MathF.Abs(y + .3f) - .1f)); break;
            case 6: // seated figure
                d = MathF.Min(MathF.Sqrt(x * x + (y - .28f) * (y - .28f)) - .09f, MathF.Max(MathF.Abs(x) - .1f, MathF.Abs(y + .02f) - .2f));
                d = MathF.Min(d, MathF.Max(MathF.Abs(x - .1f) - .14f, MathF.Abs(y + .26f) - .05f)); break;
            default: // horizontal bar and loaf
                d = MathF.Min(MathF.Max(MathF.Abs(x) - .32f, MathF.Abs(y + .1f) - .05f), MathF.Max(MathF.Sqrt(x * x + (y + .05f) * (y + .05f) * 3) - .25f, -(y + .05f))); break;
        }
        edge = d;
        return d < 0 ? kind + 1 : 0;
    }

    /// <summary>Signed distance to an equilateral triangle of circumradius r (pointing up when flip is false).</summary>
    public static float Triangle(float x, float y, float r, bool flip = false)
    {
        if (flip) y = -y;
        const float k = 1.7320508f;
        float px = MathF.Abs(x) - r * k * .5f, py = y + r * .5f;
        if (px + k * py > 0) { float nx = (px - k * py) / 2, ny = (-k * px - py) / 2; px = nx; py = ny; }
        px -= Math.Clamp(px, -r * k, 0);
        return -MathF.Sqrt(px * px + py * py) * MathF.Sign(py);
    }

    /// <summary>Stroke distance of Solomon's seal: a hexagram inside a double ring, radius r.</summary>
    public static float Hexagram(float x, float y, float r)
    {
        float tri = MathF.Min(MathF.Abs(Triangle(x, y, r * .78f)), MathF.Abs(Triangle(x, y, r * .78f, true)));
        float rr = MathF.Sqrt(x * x + y * y);
        return MathF.Min(tri, MathF.Min(MathF.Abs(rr - r * .9f), MathF.Abs(rr - r)));
    }

    /// <summary>A chain of oval links along a sagging curve between a and b.</summary>
    public static float Chain(Vector3 p, Vector3 a, Vector3 b, float sag, float link)
    {
        var mid = (a + b) * .5f - new Vector3(0, sag * .5f, 0); float span = (b - a).Length();
        float bound = (p - mid).Length() - span * .6f - sag - link * 2;
        if (bound > link) return bound;
        int count = Math.Max(2, (int)(span / (link * 1.6f))); float best = 1e9f;
        var u = Vector3.Normalize(b - a);
        var side = MathF.Abs(u.Z) < .9f ? Vector3.UnitZ : Vector3.UnitX;
        var v = Vector3.Normalize(Vector3.Cross(u, side)); var w = Vector3.Cross(u, v);
        for (int k = 0; k <= count; k++)
        {
            float t = k / (float)count; var c = Vector3.Lerp(a, b, t) - new Vector3(0, sag * 4 * t * (1 - t), 0);
            var q = p - c;
            // Each link is an oval stretched along the chain; alternate links turn a quarter.
            float along = Vector3.Dot(q, u), across = (k & 1) == 0 ? Vector3.Dot(q, v) : Vector3.Dot(q, w), depth = (k & 1) == 0 ? Vector3.Dot(q, w) : Vector3.Dot(q, v);
            float ox = MathF.Max(MathF.Abs(along) - link * .45f, 0), ring = MathF.Sqrt(ox * ox + across * across) - link * .35f;
            best = MathF.Min(best, MathF.Sqrt(ring * ring + depth * depth) - link * .1f);
        }
        return best;
    }

    // ---------- Vegetation ----------

    /// <summary>Broadleaf canopy: soft union of displaced spheres. Returns the canopy distance.</summary>
    public static float Canopy(Noise n, Vector3 q, (Vector3 c, float r)[] lobes, float rough, int seed)
    {
        float d = 1e9f;
        foreach (var (c, r) in lobes) d = R.SMin(d, (q - c).Length() - r, r * .45f);
        if (d > .6f) return d;
        float disp = n.Fbm(q, .55f, 4, seed) * rough + n.N(q, .14f, seed + 1) * rough * .45f;
        return (d + disp) * .7f;
    }

    /// <summary>Box enclosing every lobe plus <paramref name="pad"/>; object bounds must never cut a canopy.</summary>
    public static (Vector3 min, Vector3 max) LobeBounds((Vector3 c, float r)[] lobes, float pad)
    {
        var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
        foreach (var (c, r) in lobes) { min = Vector3.Min(min, c - new Vector3(r + pad)); max = Vector3.Max(max, c + new Vector3(r + pad)); }
        return (min, max);
    }

    /// <summary>Lobes for a rounded crown of a given width and height centred at c.</summary>
    public static (Vector3, float)[] Crown(Rand rnd, Vector3 c, float w, float h, int count, float depth = .6f)
    {
        var list = new List<(Vector3, float)>();
        for (int k = 0; k < count; k++)
        {
            float a = rnd.R(0, MathF.PI * 2), rr = MathF.Sqrt(rnd.F());
            var o = new Vector3(MathF.Cos(a) * rr * w * .55f, rnd.R(-.35f, .45f) * h, MathF.Sin(a) * rr * w * depth * .5f);
            list.Add((c + o, rnd.R(.28f, .42f) * MathF.Min(w, h * 1.6f)));
        }
        return list.ToArray();
    }

    /// <summary>A trunk with a few limbs reaching to the crown lobes. Origin at the trunk foot.</summary>
    public static float Trunk(Vector3 q, float height, float r0, (Vector3 a, Vector3 b, float r)[] limbs)
    {
        float d = Sdf.RoundCone(q, Vector3.Zero, new Vector3(0, height, 0), r0, r0 * .55f);
        foreach (var (a, b, r) in limbs) d = R.SMin(d, Sdf.RoundCone(q, a, b, r, r * .45f), r * .6f);
        return d;
    }

    /// <summary>A tall Mediterranean cypress flame. Origin at the foot.</summary>
    public static float Cypress(Noise n, Vector3 q, float height, float width, int seed)
    {
        float t = R.Clamp01(q.Y / height);
        float r = width * .5f * MathF.Pow(MathF.Sin(MathF.PI * MathF.Min(1, t * 1.05f + .02f)), .7f) * (1 - t * .35f);
        float d = MathF.Sqrt(q.X * q.X + q.Z * q.Z) - r;
        d = MathF.Max(d, MathF.Max(-q.Y + height * .04f, q.Y - height));
        if (d > .4f) return d;
        return (d + n.Fbm(q * new Vector3(1, .5f, 1), .22f, 4, seed) * width * .12f) * .7f;
    }

    /// <summary>A conifer of stacked drooping tiers. Origin at the foot.</summary>
    public static float Pine(Noise n, Vector3 q, float height, float width, int tiers, int seed)
    {
        float trunk = Sdf.RoundCone(q, Vector3.Zero, new Vector3(0, height * .9f, 0), width * .06f, width * .02f);
        float d = trunk;
        for (int k = 0; k < tiers; k++)
        {
            float f = k / (float)tiers;
            float y0 = height * (.18f + f * .72f), hh = height * .26f * (1 - f * .4f), w = width * .5f * (1 - f * .75f);
            float cone = Sdf.ConeY(q - new Vector3(0, y0 + hh * .5f, 0), hh * .5f, w, w * .05f);
            d = MathF.Min(d, cone);
        }
        if (d > .4f) return d;
        return (d + n.Fbm(q, .18f, 4, seed) * width * .1f) * .7f;
    }

    /// <summary>A palm: leaning curved trunk and drooping fronds. Origin at the foot.</summary>
    public static float Palm(Noise n, Vector3 q, float height, float lean, int seed, out bool frond)
    {
        frond = false;
        float d = 1e9f; int seg = 6; var prev = Vector3.Zero;
        for (int k = 1; k <= seg; k++)
        {
            float t = k / (float)seg;
            var p = new Vector3(lean * t * t * height, t * height, 0);
            d = MathF.Min(d, Sdf.RoundCone(q, prev, p, .11f - t * .03f, .1f - t * .03f));
            prev = p;
        }
        // Ring texture on the trunk.
        d += MathF.Sin(q.Y * 40) * .006f;
        var top = prev; float best = 1e9f;
        for (int f = 0; f < 9; f++)
        {
            float a = f / 9f * MathF.PI * 2 + seed;
            var dir = new Vector3(MathF.Cos(a), 0, MathF.Sin(a) * .6f);
            var mid = top + dir * height * .22f + new Vector3(0, height * .05f, 0);
            var tip = top + dir * height * .42f - new Vector3(0, height * .14f, 0);
            float fr = MathF.Min(Sdf.RoundCone(q, top, mid, .03f, .09f), Sdf.RoundCone(q, mid, tip, .09f, .015f));
            best = MathF.Min(best, fr);
        }
        if (best < .3f) best = (best + n.N(q, .05f, seed) * .025f) * .8f;
        if (best < d) { frond = true; return best; }
        return d;
    }

    // ---------- Terrain ----------

    /// <summary>
    /// Ridged mountain height with peaks shaped by an envelope of wrapped Gaussian masses, so the skyline has
    /// deliberate summits instead of uniform noise.
    /// </summary>
    public static float Mountain(Noise n, float period, float x, float z, float baseY, (float x, float h, float w)[] peaks, float rough, int seed)
    {
        float env = 0;
        foreach (var (px, ph, pw) in peaks)
        {
            float dx = R.Wrap(x, px, period);
            env = MathF.Max(env, ph * MathF.Exp(-dx * dx / (pw * pw)));
        }
        var p = new Vector3(x, 0, z);
        float ridge = n.Ridged(p, 3.2f, 7, seed);
        float hills = n.Fbm(p, 6, 3, seed + 1) * .5f + .5f;
        return baseY + env * (.55f + ridge * .55f) + hills * rough + ridge * rough * .6f;
    }

    // ---------- Fire ----------

    /// <summary>
    /// An open fire as an emissive medium rooted at <paramref name="root"/>: a teardrop split into licking tongues
    /// by rising noise, white-gold at the base through orange to dull red at the tips, translucent at its edges.
    /// </summary>
    public static Volume Fire(Scene sc, Vector3 root, float radius, float height, int seed, float heat = 1)
    {
        var n = sc.N;
        var fire = sc.AddVolume(root - new Vector3(radius * 1.8f, .05f, radius * 1.8f), root + new Vector3(radius * 1.8f, height, radius * 1.8f), q =>
        {
            var l = q - root; float h = l.Y / height;
            if (h <= 0 || h >= .98f) return 0;
            float sway = n.Fbm(new Vector3(q.X, q.Y * .6f, q.Z), radius * 1.4f, 3, seed) * radius * .6f * h;
            float r = MathF.Sqrt((l.X - sway) * (l.X - sway) + l.Z * l.Z);
            float profile = radius * MathF.Sqrt(MathF.Max(0, 1 - h)) * R.Smooth(-.1f, .18f, h);
            float tongues = n.Fbm(new Vector3(q.X * 2, q.Y * .7f, q.Z * 2), radius, 4, seed + 1) * .5f + .5f;
            return R.Smooth(profile, profile * .35f, r) * R.Smooth(.3f, .6f, tongues + (1 - h) * .5f) * 18 / radius;
        });
        var core = new Vector3(1, .84f, .55f); var flame = new Vector3(1, .42f, .08f); var tip = new Vector3(.75f, .12f, .02f);
        fire.EmitAt = q =>
        {
            float h = R.Clamp01((q.Y - root.Y) / height);
            return R.Mix(core * 14, R.Mix(flame * 7, tip * 2, R.Smooth(.45f, 1, h)), R.Smooth(.05f, .45f, h)) * heat;
        };
        fire.Albedo = Vector3.Zero; fire.Ambient = 0; fire.Anisotropy = 0; fire.Steps = 28; fire.LightSteps = 1;
        return fire;
    }

    // ---------- Clouds ----------

    /// <summary>A sea of cloud with a billowing top near <paramref name="top"/>; dense below, wispy at the crests.</summary>
    public static Func<Vector3, float> CloudSea(Noise n, float top, float amp, float density, int seed, float billow = 2.2f)
    {
        return q =>
        {
            float surface = top + (n.Fbm(new Vector3(q.X, 0, q.Z), billow, 4, seed) * .5f + .5f) * amp;
            float h = surface - q.Y;
            if (h <= -.2f) return 0;
            float e = n.Fbm(q, .7f, 4, seed + 1) * .5f + .5f;
            float v = R.Clamp01(h * 1.4f + .2f) - (1 - e) * .55f;
            return v <= 0 ? 0 : MathF.Min(1, v * 2.2f) * density;
        };
    }
    public static Func<Vector3, float> CloudSeaCoarse(Noise n, float top, float amp, float density, int seed, float billow = 2.2f)
    {
        return q =>
        {
            float surface = top + (n.Fbm(new Vector3(q.X, 0, q.Z), billow, 2, seed) * .5f + .5f) * amp;
            float h = surface - q.Y;
            return h <= 0 ? 0 : MathF.Min(1, h * 1.2f) * density * .8f;
        };
    }

    /// <summary>Cumulus density: a cluster of soft blobs eroded by billowing noise.</summary>
    public static Func<Vector3, float> Cumulus(Noise n, (Vector3 c, float r)[] blobs, float density, float erosion, int seed)
    {
        return q =>
        {
            float f = 0;
            foreach (var (c, r) in blobs)
            {
                var d = (q - c) / r; d.Y *= d.Y < 0 ? 1.8f : 1;
                float l = d.LengthSquared();
                if (l < 1) f = MathF.Max(f, 1 - l);
            }
            if (f <= 0) return 0;
            float noise = n.Fbm(q, .9f, 5, seed) * .5f + .5f;
            float v = f - erosion * (1 - noise) - .05f;
            return v <= 0 ? 0 : MathF.Min(1, v * 3) * density;
        };
    }
}
