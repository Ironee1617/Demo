using System.Numerics;

namespace UIPainter;

/// <summary>Signed distances in texture pixels (negative inside), y pointing down.</summary>
public static class Sdf
{
    const float InvSqrt2 = .70710678f;

    public static float Box(Vector2 p, Vector2 c, Vector2 half, float r = 0)
    {
        var q = Vector2.Abs(p - c) - half + new Vector2(r);
        return Vector2.Max(q, Vector2.Zero).Length() + MathF.Min(MathF.Max(q.X, q.Y), 0) - r;
    }

    /// <summary>A box with its four corners cut at 45 degrees by <paramref name="cut"/> pixels.</summary>
    public static float Chamfer(Vector2 p, Vector2 c, Vector2 half, float cut)
    {
        var q = Vector2.Abs(p - c);
        float box = MathF.Max(q.X - half.X, q.Y - half.Y);
        float diag = (q.X + q.Y - (half.X + half.Y - cut)) * InvSqrt2;
        return MathF.Max(box, diag);
    }

    public static float Circle(Vector2 p, Vector2 c, float r) => (p - c).Length() - r;

    public static float Diamond(Vector2 p, Vector2 c, float r)
    {
        var q = Vector2.Abs(p - c);
        return (q.X + q.Y - r) * InvSqrt2;
    }

    /// <summary>A capsule from a to b whose radius tapers from ra to rb.</summary>
    public static float Segment(Vector2 p, Vector2 a, Vector2 b, float ra, float rb)
    {
        var pa = p - a; var ba = b - a;
        float h = Math.Clamp(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba), 0, 1);
        return (pa - ba * h).Length() - (ra + (rb - ra) * h);
    }
    public static float Segment(Vector2 p, Vector2 a, Vector2 b, float r) => Segment(p, a, b, r, r);

    /// <summary>A ring arc of radius r and half thickness t between angles a0 and a1 (radians, y down).</summary>
    public static float Arc(Vector2 p, Vector2 c, float r, float a0, float a1, float t)
    {
        var d = p - c;
        float rel = MathF.Atan2(d.Y, d.X) - a0;
        rel -= MathF.Floor(rel / MathF.Tau) * MathF.Tau;
        if (rel <= a1 - a0) return MathF.Abs(d.Length() - r) - t;
        var e0 = c + r * new Vector2(MathF.Cos(a0), MathF.Sin(a0));
        var e1 = c + r * new Vector2(MathF.Cos(a1), MathF.Sin(a1));
        return MathF.Min((p - e0).Length(), (p - e1).Length()) - t;
    }

    /// <summary>Exact distance to a closed polygon.</summary>
    public static float Polygon(Vector2 p, params Vector2[] v)
    {
        float d = Vector2.Dot(p - v[0], p - v[0]), s = 1;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
        {
            var e = v[j] - v[i]; var w = p - v[i];
            var b = w - e * Math.Clamp(Vector2.Dot(w, e) / Vector2.Dot(e, e), 0, 1);
            d = MathF.Min(d, Vector2.Dot(b, b));
            bool c1 = p.Y >= v[i].Y, c2 = p.Y < v[j].Y, c3 = e.X * w.Y > e.Y * w.X;
            if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
        }
        return s * MathF.Sqrt(d);
    }

    /// <summary>Five-pointed star (outer radius r, inner ratio rf) pointing up.</summary>
    public static float Star(Vector2 p, Vector2 c, float r, float rf)
    {
        var q = p - c; q.Y = -q.Y;
        var k1 = new Vector2(0.809016994375f, -0.587785252292f);
        var k2 = new Vector2(-k1.X, k1.Y);
        q.X = MathF.Abs(q.X);
        q -= 2 * MathF.Max(Vector2.Dot(k1, q), 0) * k1;
        q -= 2 * MathF.Max(Vector2.Dot(k2, q), 0) * k2;
        q.X = MathF.Abs(q.X);
        q.Y -= r;
        var ba = rf * new Vector2(-k1.Y, k1.X) - new Vector2(0, 1);
        float h = Math.Clamp(Vector2.Dot(q, ba) / Vector2.Dot(ba, ba), 0, r);
        return (q - ba * h).Length() * MathF.Sign(q.Y * ba.X - q.X * ba.Y);
    }

    /// <summary>A heart of roughly <paramref name="size"/> pixels, its point at the bottom.</summary>
    public static float Heart(Vector2 p, Vector2 c, float size)
    {
        var q = (p - c) / size; q.Y = -q.Y + .55f; q.X = MathF.Abs(q.X);
        float d;
        if (q.Y + q.X > 1) d = MathF.Sqrt(Vector2.DistanceSquared(q, new Vector2(.25f, .75f))) - MathF.Sqrt(2) / 4;
        else d = MathF.Sqrt(MathF.Min(Vector2.DistanceSquared(q, new Vector2(0, 1)), Vector2.DistanceSquared(q, .5f * MathF.Max(q.X + q.Y, 0) * Vector2.One))) * MathF.Sign(q.X - q.Y);
        return d * size;
    }

    public static float Outline(float d, float halfWidth) => MathF.Abs(d) - halfWidth;

    /// <summary>Anti-aliased coverage of a one-pixel edge band.</summary>
    public static float Cov(float d) => Math.Clamp(.5f - d, 0, 1);

    public static float Smooth(float a, float b, float x)
    {
        float t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>Distances measured from the nearest corner of a w×h texture, for four-way symmetric ornaments.</summary>
    public static Vector2 Fold(Vector2 p, float w, float h) => new(MathF.Min(p.X, w - p.X), MathF.Min(p.Y, h - p.Y));

    public static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    public static float Noise(float x, float y, int seed)
    {
        int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y);
        float fx = x - ix, fy = y - iy;
        float u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
        float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed), c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
        return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
    }

    public static float Fbm(float x, float y, int seed, int octaves = 5)
    {
        float sum = 0, amp = .5f, norm = 0;
        for (int o = 0; o < octaves; o++) { sum += Noise(x, y, seed + o * 17) * amp; norm += amp; x *= 2.03f; y *= 2.03f; amp *= .5f; }
        return sum / norm;
    }
}
