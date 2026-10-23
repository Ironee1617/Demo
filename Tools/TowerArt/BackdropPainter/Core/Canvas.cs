namespace BackdropPainter;

public delegate Col Shader(float x, float y, int i, int j);

/// <summary>
/// A horizontally wrapping pixel strip mapped to world units. Pixel (0,0) is the bottom-left texel.
/// Shaders receive unwrapped world coordinates, so an element straddling the seam is painted on both edges.
/// </summary>
public sealed class Canvas
{
    public readonly int W, H;
    public readonly float Ppu, Bottom, Period;
    public readonly Field F;
    public readonly Col[] P;

    public Canvas(float period, float bottom, float height, float ppu)
    {
        Period = period; Bottom = bottom; Ppu = ppu;
        W = (int)MathF.Round(period * ppu); H = (int)MathF.Ceiling(height * ppu / 4) * 4;
        if (W % 4 != 0) throw new ArgumentException("Strip width must be a multiple of four texels for block compression.");
        F = new Field(period); P = new Col[W * H];
    }
    public Canvas Blank() => new(Period, Bottom, H / Ppu, Ppu);

    public float Top => Bottom + H / Ppu;
    public float X(int i) => (i + .5f) / Ppu;
    public float Y(int j) => Bottom + (j + .5f) / Ppu;
    public int I(float x) => (int)MathF.Floor(x * Ppu);
    public int J(float y) => (int)MathF.Floor((y - Bottom) * Ppu);
    public float Px => 1f / Ppu;

    public Col Get(int i, int j) => j < 0 || j >= H ? Col.Clear : P[j * W + M.Mod(i, W)];
    public bool Solid(int i, int j) => Get(i, j).A > .5f;
    public void Set(int i, int j, Col c) { if (j >= 0 && j < H) P[j * W + M.Mod(i, W)] = c; }

    public void Paint(float x0, float x1, float y0, float y1, Shader shader, bool add = false)
    {
        int i0 = I(x0), i1 = I(x1), j0 = Math.Max(0, J(y0)), j1 = Math.Min(H - 1, J(y1));
        if (i1 - i0 >= W) { i0 = 0; i1 = W - 1; }
        if (j1 < j0) return;
        int rows = j1 - j0 + 1;
        void Row(int r)
        {
            int j = j0 + r; float y = Y(j); int baseIdx = j * W;
            for (int i = i0; i <= i1; i++)
            {
                int wi = M.Mod(i, W);
                var c = shader(X(i), y, wi, j);
                if (c.A <= 0) continue;
                ref var dst = ref P[baseIdx + wi];
                dst = add ? new Col(Math.Min(1, dst.R + c.R * c.A), Math.Min(1, dst.G + c.G * c.A), Math.Min(1, dst.B + c.B * c.A), Math.Max(dst.A, c.A)) : dst.Under(c);
            }
        }
        if ((long)rows * (i1 - i0 + 1) > 40000) Parallel.For(0, rows, Row); else for (int r = 0; r < rows; r++) Row(r);
    }

    public void PaintAll(Shader shader) => Paint(0, Period - Px * .5f, Bottom, Top - Px * .5f, shader);

    /// <summary>Rewrites every opaque pixel; the callback can query neighbours of the unmodified source.</summary>
    public void Post(Func<int, int, Col, Col> f)
    {
        var src = (Col[])P.Clone();
        Parallel.For(0, H, j =>
        {
            for (int i = 0; i < W; i++)
            {
                var c = src[j * W + i];
                if (c.A <= 0) continue;
                P[j * W + i] = f(i, j, c);
            }
        });
    }

    /// <summary>Number of consecutive covered texels from (i,j) stepping (dx,dy), capped at max.</summary>
    public int Depth(int i, int j, int dx, int dy, int max)
    {
        for (int k = 1; k <= max; k++) if (!Solid(i + dx * k, j + dy * k)) return k - 1;
        return max;
    }

    public void Over(Canvas top, float opacity = 1)
    {
        Parallel.For(0, H, j =>
        {
            for (int i = 0; i < W; i++)
            {
                var c = top.P[j * W + i];
                if (c.A <= 0) continue;
                P[j * W + i] = P[j * W + i].Under(opacity >= 1 ? c : c.WithA(c.A * opacity));
            }
        });
    }

    /// <summary>Stepped radial light: pixel-art glow with ordered dither between the rings.</summary>
    public void Glow(float x, float y, float rx, float ry, Col c, float strength, int steps = 5, bool add = true)
    {
        Paint(x - rx, x + rx, y - ry, y + ry, (px, py, i, j) =>
        {
            float dx = (px - x) / rx, dy = (py - y) / ry, d = MathF.Sqrt(dx * dx + dy * dy);
            if (d >= 1) return Col.Clear;
            float a = (1 - d); a = a * a * strength;
            float q = MathF.Floor(a * steps + Dither.Bayer(i, j)) / steps;
            return q <= 0 ? Col.Clear : c.WithA(Math.Min(1, q));
        }, add);
    }

    public byte[] ToBytes(int rowStart = 0, int rowCount = -1)
    {
        if (rowCount < 0) rowCount = H - rowStart;
        var b = new byte[W * rowCount * 4];
        for (int j = 0; j < rowCount; j++)
            for (int i = 0; i < W; i++)
            {
                // Transparent texels keep their (bled) colour so bilinear filtering never pulls in black fringes.
                var c = P[(rowStart + j) * W + i]; int o = (j * W + i) * 4;
                b[o] = Q(c.R); b[o + 1] = Q(c.G); b[o + 2] = Q(c.B); b[o + 3] = Q(c.A);
            }
        return b;
    }

    /// <summary>
    /// Spreads the colour of covered texels into neighbouring transparent ones (alpha stays 0), so filtered and
    /// mipmapped sampling at silhouettes blends toward the edge colour instead of black.
    /// </summary>
    public void Bleed(int passes = 8)
    {
        var src = (Col[])P.Clone();
        var known = new bool[P.Length];
        for (int k = 0; k < P.Length; k++) known[k] = P[k].A > .004f;
        for (int pass = 0; pass < passes; pass++)
        {
            var next = (Col[])src.Clone(); var nextKnown = (bool[])known.Clone(); bool changed = false;
            for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                {
                    int k = j * W + i; if (known[k]) continue;
                    float r = 0, g = 0, bl = 0; int n = 0;
                    for (int dj = -1; dj <= 1; dj++)
                        for (int di = -1; di <= 1; di++)
                        {
                            int jj = j + dj; if (jj < 0 || jj >= H) continue;
                            int kk = jj * W + M.Mod(i + di, W); if (!known[kk]) continue;
                            r += src[kk].R; g += src[kk].G; bl += src[kk].B; n++;
                        }
                    if (n == 0) continue;
                    next[k] = new Col(r / n, g / n, bl / n, 0); nextKnown[k] = true; changed = true;
                }
            src = next; known = nextKnown;
            if (!changed) break;
        }
        for (int k = 0; k < P.Length; k++) if (P[k].A <= .004f) P[k] = src[k].WithA(P[k].A);
    }
    static byte Q(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255), 0, 255);

    public bool RowEmpty(int j) { for (int i = 0; i < W; i++) if (P[j * W + i].A > 0) return false; return true; }
}

public readonly struct V3
{
    public readonly float X, Y, Z;
    public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
    public V3 N() { float l = MathF.Sqrt(X * X + Y * Y + Z * Z); return l > 0 ? new V3(X / l, Y / l, Z / l) : this; }
    public float Dot(V3 o) => X * o.X + Y * o.Y + Z * o.Z;
}
