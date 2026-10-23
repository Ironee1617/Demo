using System.Numerics;
using System.Runtime.CompilerServices;

namespace BackdropPainter.Render;

/// <summary>
/// 3D gradient noise whose lattice repeats exactly every <see cref="Period"/> units in x, so anything shaded with
/// it tiles seamlessly across the horizontally repeating backdrop strip. Feature sizes snap to whole cells per period.
/// </summary>
public sealed class Noise
{
    public readonly float Period;
    public Noise(float period) { Period = period; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static float Grad(uint h, float x, float y, float z)
    {
        switch (h & 15)
        {
            case 0: return x + y; case 1: return -x + y; case 2: return x - y; case 3: return -x - y;
            case 4: return x + z; case 5: return -x + z; case 6: return x - z; case 7: return -x - z;
            case 8: return y + z; case 9: return -y + z; case 10: return y - z; case 11: return -y - z;
            case 12: return x + y; case 13: return -x + y; case 14: return -y + z; default: return -y - z;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int WrapCell(int x, int cells) { int r = x % cells; return r < 0 ? r + cells : r; }

    /// <summary>Perlin noise in lattice units, roughly in [-1,1], wrapping every <paramref name="cellsX"/> cells in x.</summary>
    public static float Perlin(float x, float y, float z, int cellsX, int seed)
    {
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y), z0 = (int)MathF.Floor(z);
        float fx = x - x0, fy = y - y0, fz = z - z0;
        float u = Fade(fx), v = Fade(fy), w = Fade(fz);
        int xa = WrapCell(x0, cellsX), xb = WrapCell(x0 + 1, cellsX);
        float n000 = Grad(R.Hash(xa, y0, z0, seed), fx, fy, fz);
        float n100 = Grad(R.Hash(xb, y0, z0, seed), fx - 1, fy, fz);
        float n010 = Grad(R.Hash(xa, y0 + 1, z0, seed), fx, fy - 1, fz);
        float n110 = Grad(R.Hash(xb, y0 + 1, z0, seed), fx - 1, fy - 1, fz);
        float n001 = Grad(R.Hash(xa, y0, z0 + 1, seed), fx, fy, fz - 1);
        float n101 = Grad(R.Hash(xb, y0, z0 + 1, seed), fx - 1, fy, fz - 1);
        float n011 = Grad(R.Hash(xa, y0 + 1, z0 + 1, seed), fx, fy - 1, fz - 1);
        float n111 = Grad(R.Hash(xb, y0 + 1, z0 + 1, seed), fx - 1, fy - 1, fz - 1);
        float x00 = n000 + (n100 - n000) * u, x10 = n010 + (n110 - n010) * u;
        float x01 = n001 + (n101 - n001) * u, x11 = n011 + (n111 - n011) * u;
        float y0v = x00 + (x10 - x00) * v, y1v = x01 + (x11 - x01) * v;
        return (y0v + (y1v - y0v) * w) * .95f;
    }

    int Cells(float size) => Math.Max(1, (int)MathF.Round(Period / Math.Max(size, 1e-4f)));

    /// <summary>One octave with features about <paramref name="size"/> units across.</summary>
    public float N(Vector3 p, float size, int seed)
    {
        int c = Cells(size); float s = c / Period;
        return Perlin(p.X * s, p.Y * s, p.Z * s, c, seed);
    }
    public float N(float x, float y, float size, int seed) => N(new Vector3(x, y, 0), size, seed);

    /// <summary>Fractal sum normalised to roughly [-1,1].</summary>
    public float Fbm(Vector3 p, float size, int octaves, int seed, float gain = .5f)
    {
        int c = Cells(size); float sum = 0, amp = 1, norm = 0;
        for (int o = 0; o < octaves; o++)
        {
            float s = c / Period;
            sum += Perlin(p.X * s, p.Y * s, p.Z * s, c, seed + o * 131) * amp;
            norm += amp; amp *= gain; c *= 2;
        }
        return sum / norm * 1.35f;
    }
    public float Fbm(float x, float y, float size, int octaves, int seed, float gain = .5f) => Fbm(new Vector3(x, y, 0), size, octaves, seed, gain);

    /// <summary>Ridged multifractal in [0,1]: sharp crests for mountain ridges, cracks and veins.</summary>
    public float Ridged(Vector3 p, float size, int octaves, int seed, float gain = .5f)
    {
        int c = Cells(size); float sum = 0, amp = 1, norm = 0, weight = 1;
        for (int o = 0; o < octaves; o++)
        {
            float s = c / Period;
            float n = 1 - MathF.Abs(Perlin(p.X * s, p.Y * s, p.Z * s, c, seed + o * 71));
            n *= n; n *= weight; weight = R.Clamp01(n * 1.6f);
            sum += n * amp; norm += amp; amp *= gain; c *= 2;
        }
        return sum / norm;
    }

    /// <summary>Domain-warped fbm: organic marble swirls, cloud billows and eroded rock.</summary>
    public float Warped(Vector3 p, float size, int octaves, int seed, float warp)
    {
        var q = new Vector3(Fbm(p, size * 1.7f, 3, seed + 911), Fbm(p + new Vector3(0, 5.2f, 1.3f), size * 1.7f, 3, seed + 912), 0);
        return Fbm(p + q * warp, size, octaves, seed);
    }

    public struct Cell { public float F1, F2; public Vector3 Center; public uint Id; }

    /// <summary>Periodic 3D Worley cells about <paramref name="size"/> units across.</summary>
    public Cell Voronoi(Vector3 p, float size, int seed, float jitter = .85f)
    {
        int c = Cells(size); float s = c / Period;
        float lx = p.X * s, ly = p.Y * s, lz = p.Z * s;
        int cx = (int)MathF.Floor(lx), cy = (int)MathF.Floor(ly), cz = (int)MathF.Floor(lz);
        var hit = new Cell { F1 = 9, F2 = 9 };
        for (int oz = -1; oz <= 1; oz++)
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = cx + ox, gy = cy + oy, gz = cz + oz;
                    uint h = R.Hash(WrapCell(gx, c), gy, gz, seed);
                    float px = gx + .5f + (((h & 1023) / 1023f) - .5f) * jitter;
                    float py = gy + .5f + ((((h >> 10) & 1023) / 1023f) - .5f) * jitter;
                    float pz = gz + .5f + ((((h >> 20) & 1023) / 1023f) - .5f) * jitter;
                    float dx = px - lx, dy = py - ly, dz = pz - lz, d = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (d < hit.F1) { hit.F2 = hit.F1; hit.F1 = d; hit.Id = h; hit.Center = new Vector3(px, py, pz) / s; }
                    else if (d < hit.F2) hit.F2 = d;
                }
        hit.F1 /= s; hit.F2 /= s;
        return hit;
    }
}
