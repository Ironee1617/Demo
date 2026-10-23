using System.Numerics;
using System.Runtime.CompilerServices;

namespace BackdropPainter.Render;

/// <summary>Signed distance primitives (after Inigo Quilez). Shapes are centred at the origin unless noted.</summary>
public static class Sdf
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sphere(Vector3 p, float r) => p.Length() - r;

    public static float Box(Vector3 p, Vector3 b)
    {
        var q = Vector3.Abs(p) - b;
        return Vector3.Max(q, Vector3.Zero).Length() + MathF.Min(MathF.Max(q.X, MathF.Max(q.Y, q.Z)), 0);
    }
    public static float RoundBox(Vector3 p, Vector3 b, float r) => Box(p, b - new Vector3(r)) - r;

    /// <summary>The twelve edges of a box with half extents b, bars of half-thickness e.</summary>
    public static float BoxFrame(Vector3 p, Vector3 b, float e)
    {
        p = Vector3.Abs(p) - b;
        var q = Vector3.Abs(p + new Vector3(e)) - new Vector3(e);
        float a = Vector3.Max(new Vector3(p.X, q.Y, q.Z), Vector3.Zero).Length() + MathF.Min(MathF.Max(p.X, MathF.Max(q.Y, q.Z)), 0);
        float c = Vector3.Max(new Vector3(q.X, p.Y, q.Z), Vector3.Zero).Length() + MathF.Min(MathF.Max(q.X, MathF.Max(p.Y, q.Z)), 0);
        float d = Vector3.Max(new Vector3(q.X, q.Y, p.Z), Vector3.Zero).Length() + MathF.Min(MathF.Max(q.X, MathF.Max(q.Y, p.Z)), 0);
        return MathF.Min(a, MathF.Min(c, d));
    }

    /// <summary>Vertical capped cylinder of radius r and half-height h.</summary>
    public static float CylinderY(Vector3 p, float r, float h)
    {
        float dx = MathF.Sqrt(p.X * p.X + p.Z * p.Z) - r, dy = MathF.Abs(p.Y) - h;
        float ox = MathF.Max(dx, 0), oy = MathF.Max(dy, 0);
        return MathF.Min(MathF.Max(dx, dy), 0) + MathF.Sqrt(ox * ox + oy * oy);
    }
    /// <summary>Cylinder along x with radius r and half-length h.</summary>
    public static float CylinderX(Vector3 p, float r, float h) => CylinderY(new Vector3(p.Y, p.X, p.Z), r, h);
    /// <summary>Cylinder along z with radius r and half-length h.</summary>
    public static float CylinderZ(Vector3 p, float r, float h) => CylinderY(new Vector3(p.X, p.Z, p.Y), r, h);

    public static float Capsule(Vector3 p, Vector3 a, Vector3 b, float r)
    {
        var pa = p - a; var ba = b - a;
        float h = R.Clamp01(Vector3.Dot(pa, ba) / Vector3.Dot(ba, ba));
        return (pa - ba * h).Length() - r;
    }

    /// <summary>Cone between a (radius ra) and b (radius rb) with rounded joints: trunks, branches, limbs, horns.</summary>
    public static float RoundCone(Vector3 p, Vector3 a, Vector3 b, float r1, float r2)
    {
        var ba = b - a; float l2 = Vector3.Dot(ba, ba), rr = r1 - r2, a2 = l2 - rr * rr, il2 = 1 / l2;
        var pa = p - a; float y = Vector3.Dot(pa, ba), z = y - l2;
        var xv = pa * l2 - ba * y; float x2 = Vector3.Dot(xv, xv), y2 = y * y * l2, z2 = z * z * l2;
        float k = MathF.Sign(rr) * rr * rr * x2;
        if (MathF.Sign(z) * a2 * z2 > k) return MathF.Sqrt(x2 + z2) * il2 - r2;
        if (MathF.Sign(y) * a2 * y2 < k) return MathF.Sqrt(x2 + y2) * il2 - r1;
        return (MathF.Sqrt(x2 * a2 * il2) + y * rr) * il2 - r1;
    }

    /// <summary>Vertical capped cone: bottom radius r1 at y=-h, top radius r2 at y=+h.</summary>
    public static float ConeY(Vector3 p, float h, float r1, float r2)
    {
        float qx = MathF.Sqrt(p.X * p.X + p.Z * p.Z), qy = p.Y;
        float k1x = r2, k1y = h, k2x = r2 - r1, k2y = 2 * h;
        float cax = qx - MathF.Min(qx, qy < 0 ? r1 : r2), cay = MathF.Abs(qy) - h;
        float t = R.Clamp01(((k1x - qx) * k2x + (k1y - qy) * k2y) / (k2x * k2x + k2y * k2y));
        float cbx = qx - k1x + k2x * t, cby = qy - k1y + k2y * t;
        float s = cbx < 0 && cay < 0 ? -1 : 1;
        return s * MathF.Sqrt(MathF.Min(cax * cax + cay * cay, cbx * cbx + cby * cby));
    }

    /// <summary>Approximate ellipsoid; good away from extreme axis ratios.</summary>
    public static float Ellipsoid(Vector3 p, Vector3 r)
    {
        float k0 = (p / r).Length(), k1 = (p / (r * r)).Length();
        return k1 <= 0 ? -MathF.Min(r.X, MathF.Min(r.Y, r.Z)) : k0 * (k0 - 1) / k1;
    }

    /// <summary>Torus around the y axis.</summary>
    public static float TorusY(Vector3 p, float major, float minor)
    {
        float qx = MathF.Sqrt(p.X * p.X + p.Z * p.Z) - major;
        return MathF.Sqrt(qx * qx + p.Y * p.Y) - minor;
    }
    /// <summary>Torus around the x axis (a ring seen edge-on, lying in the y-z plane).</summary>
    public static float TorusX(Vector3 p, float major, float minor) => TorusY(new Vector3(p.Y, p.X, p.Z), major, minor);
    /// <summary>Torus around the z axis (a ring facing the viewer): arches, halos, wheels.</summary>
    public static float TorusZ(Vector3 p, float major, float minor) => TorusY(new Vector3(p.X, p.Z, p.Y), major, minor);

    /// <summary>Square pyramid with base half-width s/2 at y=0 and apex at y=h.</summary>
    public static float Pyramid(Vector3 p, float h, float s = 1)
    {
        p /= s; h /= s;
        float m2 = h * h + .25f;
        float px = MathF.Abs(p.X), pz = MathF.Abs(p.Z);
        if (pz > px) (px, pz) = (pz, px);
        px -= .5f; pz -= .5f;
        float qx = pz, qy = h * p.Y - .5f * px, qz = h * px + .5f * p.Y;
        float sv = MathF.Max(-qx, 0), t = R.Clamp01((qy - .5f * pz) / (m2 + .25f));
        float a = m2 * (qx + sv) * (qx + sv) + qy * qy, b = m2 * (qx + .5f * t) * (qx + .5f * t) + (qy - m2 * t) * (qy - m2 * t);
        float d2 = MathF.Min(qy, -qx * m2 - qy * .5f) > 0 ? 0 : MathF.Min(a, b);
        return MathF.Sqrt((d2 + qz * qz) / m2) * MathF.Sign(MathF.Max(qz, -p.Y)) * s;
    }

    /// <summary>Triangular prism along z: a gable whose ridge runs along z. w = half-width of base, h = height above y=0.</summary>
    public static float GableZ(Vector3 p, float w, float h, float halfLength)
    {
        float px = MathF.Abs(p.X);
        // Distance to the sloped roof planes and the base, intersected with the length slab.
        float len = MathF.Sqrt(w * w + h * h);
        float slope = (px * h + p.Y * w - w * h) / len;
        float d = MathF.Max(slope, -p.Y);
        return MathF.Max(d, MathF.Abs(p.Z) - halfLength);
    }
    /// <summary>Triangular prism along x (ridge runs along x): a pediment seen face-on.</summary>
    public static float GableX(Vector3 p, float w, float h, float halfLength) => GableZ(new Vector3(p.Z, p.Y, p.X), w, h, halfLength);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Union(float a, float b) => MathF.Min(a, b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Subtract(float a, float b) => MathF.Max(a, -b);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Intersect(float a, float b) => MathF.Max(a, b);

    /// <summary>Repeats space every <paramref name="step"/> along one coordinate, limited to cells [-n, n].</summary>
    public static float RepeatLimited(float v, float step, int n)
    {
        float k = MathF.Round(v / step);
        k = MathF.Max(-n, MathF.Min(n, k));
        return v - step * k;
    }
    /// <summary>Angular repetition around the y axis into <paramref name="count"/> sectors (returns x/z folded).</summary>
    public static Vector3 RepeatAngleY(Vector3 p, int count)
    {
        float sector = MathF.PI * 2 / count;
        float a = MathF.Atan2(p.Z, p.X);
        float k = MathF.Round(a / sector);
        float r = MathF.Sqrt(p.X * p.X + p.Z * p.Z);
        float ang = a - k * sector;
        return new Vector3(MathF.Cos(ang) * r, p.Y, MathF.Sin(ang) * r);
    }
    public static Vector3 RotY(Vector3 p, float a) { float c = MathF.Cos(a), s = MathF.Sin(a); return new Vector3(c * p.X - s * p.Z, p.Y, s * p.X + c * p.Z); }
    public static Vector3 RotZ(Vector3 p, float a) { float c = MathF.Cos(a), s = MathF.Sin(a); return new Vector3(c * p.X - s * p.Y, s * p.X + c * p.Y, p.Z); }
    public static Vector3 RotX(Vector3 p, float a) { float c = MathF.Cos(a), s = MathF.Sin(a); return new Vector3(p.X, c * p.Y - s * p.Z, s * p.Y + c * p.Z); }
}
