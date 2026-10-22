namespace BackdropPainter;

/// <summary>Gradient and cellular noise that repeats exactly every <see cref="Period"/> world units in x.</summary>
public sealed class Field
{
    public readonly float Period;
    public Field(float period) { Period = period; }

    public static uint Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ (uint)seed * 0xCB1AB31Fu;
            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
            return h;
        }
    }
    public static float Hash01(int x, int y, int seed) => (Hash(x, y, seed) & 0xFFFFFF) / 16777216f;

    static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
    static float Grad(uint h, float x, float y)
    {
        switch (h & 7)
        {
            case 0: return x + y; case 1: return x - y; case 2: return -x + y; case 3: return -x - y;
            case 4: return x * 1.41f; case 5: return -x * 1.41f; case 6: return y * 1.41f; default: return -y * 1.41f;
        }
    }

    /// <summary>Perlin noise in lattice units with the lattice wrapping every <paramref name="cells"/> columns.</summary>
    public static float Lattice(float x, float y, int cells, int seed)
    {
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float fx = x - x0, fy = y - y0, u = Fade(fx), v = Fade(fy);
        int xa = M.Mod(x0, cells), xb = M.Mod(x0 + 1, cells);
        float n00 = Grad(Hash(xa, y0, seed), fx, fy), n10 = Grad(Hash(xb, y0, seed), fx - 1, fy);
        float n01 = Grad(Hash(xa, y0 + 1, seed), fx, fy - 1), n11 = Grad(Hash(xb, y0 + 1, seed), fx - 1, fy - 1);
        float a = n00 + (n10 - n00) * u, b = n01 + (n11 - n01) * u;
        return (a + (b - a) * v) * .72f;
    }

    int Cells(float size) => Math.Max(1, (int)MathF.Round(Period / Math.Max(size, 1e-4f)));

    /// <summary>Single octave in [-1,1] with features about <paramref name="size"/> units wide.</summary>
    public float N(float x, float y, float size, int seed)
    {
        int c = Cells(size); float s = c / Period;
        return Lattice(x * s, y * s, c, seed);
    }

    /// <summary>Anisotropic single octave; x feature size snaps to the period, y is free.</summary>
    public float N2(float x, float y, float sizeX, float sizeY, int seed)
    {
        int c = Cells(sizeX); float s = c / Period;
        return Lattice(x * s, y / sizeY, c, seed);
    }

    public float Fbm2(float x, float y, float sizeX, float sizeY, int octaves, int seed)
    {
        int c = Cells(sizeX); float sum = 0, amp = 1, norm = 0, sy = 1 / sizeY;
        for (int o = 0; o < octaves; o++)
        {
            float s = c / Period;
            sum += Lattice(x * s, y * sy, c, seed + o * 131) * amp;
            norm += amp; amp *= .5f; c *= 2; sy *= 2;
        }
        return sum / norm * 1.25f;
    }

    /// <summary>Snaps a repeating interval so a whole number fits the period.</summary>
    public float Snap(float interval) => Period / Math.Max(1, MathF.Round(Period / interval));

    /// <summary>Fractal sum normalised to roughly [-1,1].</summary>
    public float Fbm(float x, float y, float size, int octaves, int seed, float gain = .5f)
    {
        int c = Cells(size); float sum = 0, amp = 1, norm = 0;
        for (int o = 0; o < octaves; o++)
        {
            float s = c / Period;
            sum += Lattice(x * s, y * s, c, seed + o * 131) * amp;
            norm += amp; amp *= gain; c *= 2;
        }
        return sum / norm * 1.25f;
    }

    /// <summary>Ridged fractal in [0,1]; sharp crests suit cracks, veins and rock seams.</summary>
    public float Ridge(float x, float y, float size, int octaves, int seed)
    {
        int c = Cells(size); float sum = 0, amp = 1, norm = 0;
        for (int o = 0; o < octaves; o++)
        {
            float s = c / Period;
            float n = 1 - MathF.Abs(Lattice(x * s, y * s, c, seed + o * 71) * 1.3f);
            sum += n * n * amp; norm += amp; amp *= .5f; c *= 2;
        }
        return sum / norm;
    }

    public struct CellHit { public float F1, F2, Px, Py; public uint Id; }

    /// <summary>Periodic Worley cells about <paramref name="size"/> units across, optionally stretched vertically.</summary>
    public CellHit Cell(float x, float y, float size, int seed, float stretchY = 1)
    {
        int c = Cells(size); float s = c / Period;
        float lx = x * s, ly = y * s / stretchY;
        int cx = (int)MathF.Floor(lx), cy = (int)MathF.Floor(ly);
        var hit = new CellHit { F1 = 9, F2 = 9 };
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int gx = cx + ox, gy = cy + oy, wx = M.Mod(gx, c);
                uint h = Hash(wx, gy, seed);
                float px = gx + ((h & 1023) / 1023f) * .8f + .1f, py = gy + (((h >> 10) & 1023) / 1023f) * .8f + .1f;
                float dx = px - lx, dy = py - ly, d = MathF.Sqrt(dx * dx + dy * dy);
                if (d < hit.F1) { hit.F2 = hit.F1; hit.F1 = d; hit.Id = h; hit.Px = px / s; hit.Py = py * stretchY / s; }
                else if (d < hit.F2) hit.F2 = d;
            }
        return hit;
    }

    /// <summary>Shortest signed x offset from a to b on the wrapped strip.</summary>
    public float Dx(float a, float b)
    {
        float d = M.Mod(a - b + Period * .5f, Period) - Period * .5f;
        return d;
    }
}
