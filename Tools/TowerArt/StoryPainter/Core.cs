using System.IO.Compression;

namespace StoryPainter;

/// <summary>sRGB colour with straight alpha.</summary>
public readonly struct Col
{
    public readonly float R, G, B, A;
    public Col(float r, float g, float b, float a = 1) { R = r; G = g; B = b; A = a; }
    public static readonly Col Clear = new(0, 0, 0, 0);
    public static Col Hex(string hex)
    {
        int v = Convert.ToInt32(hex.Substring(0, 6), 16);
        return new Col(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
    }
    public Col WithA(float a) => new(R, G, B, a);
    public static Col Lerp(Col a, Col b, float t)
    {
        t = Math.Clamp(t, 0, 1);
        return new Col(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
    }
    /// <summary>Straight-alpha "over" compositing of <paramref name="top"/> onto this colour.</summary>
    public Col Under(Col top)
    {
        if (top.A >= 1) return top;
        if (top.A <= 0) return this;
        float a = top.A + A * (1 - top.A);
        return new Col((top.R * top.A + R * A * (1 - top.A)) / a, (top.G * top.A + G * A * (1 - top.A)) / a, (top.B * top.A + B * A * (1 - top.A)) / a, a);
    }
    public Col Add(Col light, float amount) =>
        new(Math.Min(1, R + light.R * amount), Math.Min(1, G + light.G * amount), Math.Min(1, B + light.B * amount), A);
}

/// <summary>A dark-to-light ramp sampled with ordered dither at band edges, like hand-placed pixel shading.</summary>
public sealed class Ramp
{
    readonly Col[] c;
    public Ramp(params string[] hex) { c = hex.Select(Col.Hex).ToArray(); }
    public Col this[int i] => c[Math.Clamp(i, 0, c.Length - 1)];
    public Col Last => c[^1];
    public Col At(float v, int x, int y, float dither = .45f)
    {
        float t = Math.Clamp(v, 0, 1) * (c.Length - 1) + (Bayer.At(x, y) - .5f) * dither;
        return c[Math.Clamp((int)MathF.Floor(t + .5f), 0, c.Length - 1)];
    }
}

public static class Bayer
{
    static readonly float[] M = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    public static float At(int x, int y) => (M[(y & 3) * 4 + (x & 3)] + .5f) / 16f;
}

public static class Noise
{
    public static uint Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ (uint)seed * 0xCB1AB31Fu;
            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15; return h;
        }
    }
    public static float H01(int x, int y, int seed) => (Hash(x, y, seed) & 0xFFFFFF) / 16777216f;
    /// <summary>Smooth value noise in [-1,1] with features about <paramref name="size"/> pixels wide.</summary>
    public static float Value(float x, float y, float size, int seed)
    {
        x /= size; y /= size;
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float fx = x - x0, fy = y - y0, u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
        float a = H01(x0, y0, seed), b = H01(x0 + 1, y0, seed), c = H01(x0, y0 + 1, seed), d = H01(x0 + 1, y0 + 1, seed);
        return (a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v) * 2 - 1;
    }
    public static float Fbm(float x, float y, float size, int octaves, int seed)
    {
        float sum = 0, amp = 1, norm = 0;
        for (int o = 0; o < octaves; o++) { sum += Value(x, y, size, seed + o * 131) * amp; norm += amp; amp *= .5f; size *= .5f; }
        return sum / norm;
    }
    /// <summary>Ridged noise in [0,1]; thin bright crests suit cracks and veins.</summary>
    public static float Ridge(float x, float y, float size, int seed)
    { float n = 1 - MathF.Abs(Fbm(x, y, size, 3, seed)); return n * n * n; }
}

public readonly struct V3
{
    public readonly float X, Y, Z;
    public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
    public V3 N() { float l = MathF.Sqrt(X * X + Y * Y + Z * Z); return l > 0 ? new V3(X / l, Y / l, Z / l) : this; }
    public float Dot(V3 o) => X * o.X + Y * o.Y + Z * o.Z;
}

/// <summary>Per-texel facts handed to a part's shader.</summary>
public struct Texel
{
    public int X, Y;
    /// <summary>Distance to the part's edge in texels.</summary>
    public float Depth;
    public V3 Normal;
    /// <summary>Key light from the upper left and the abyss rim light from below right, both 0..1.</summary>
    public float Key, Rim;
    /// <summary>Shape parameters supplied by the inside test (e.g. position along a tube and side).</summary>
    public float T, S;
}

public delegate bool Inside(float x, float y, out float t, out float s);

/// <summary>A pixel image with y up; (0,0) is the bottom-left texel.</summary>
public sealed class Img
{
    public readonly int W, H;
    public readonly Col[] P;
    public static readonly V3 Key = new V3(-.55f, .62f, .56f).N(), RimLight = new V3(.62f, -.48f, .38f).N();
    public Img(int w, int h) { W = w; H = h; P = new Col[w * h]; }
    public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
    public Col Get(int x, int y) => In(x, y) ? P[y * W + x] : Col.Clear;
    public void Over(int x, int y, Col c) { if (In(x, y)) P[y * W + x] = P[y * W + x].Under(c); }

    /// <summary>
    /// Rasterises a shape, inflates it into a rounded relief over <paramref name="radius"/> texels from its edge
    /// (plus an optional bump field) and shades every texel; texels on the rim get the outline colour.
    /// </summary>
    public void Part(Inside inside, float radius, Func<Texel, Col> shade, Col outline, (int x0, int y0, int x1, int y1)? box = null,
        Func<float, float, float> bump = null, float outlineWidth = 1.2f, int smooth = 0)
    {
        var (x0, y0, x1, y1) = box ?? (0, 0, W - 1, H - 1);
        x0 = Math.Max(0, x0 - 2); y0 = Math.Max(0, y0 - 2); x1 = Math.Min(W - 1, x1 + 2); y1 = Math.Min(H - 1, y1 + 2);
        int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
        var mask = new bool[bw * bh]; var ts = new float[bw * bh]; var ss = new float[bw * bh];
        for (int y = 0; y < bh; y++)
            for (int x = 0; x < bw; x++)
            {
                int k = y * bw + x;
                mask[k] = inside(x0 + x + .5f, y0 + y + .5f, out ts[k], out ss[k]);
            }
        var d = Distance(mask, bw, bh);
        var h = new float[bw * bh];
        for (int k = 0; k < h.Length; k++)
        {
            if (!mask[k]) continue;
            float t = Math.Min(1, d[k] / Math.Max(1, radius));
            h[k] = MathF.Sqrt(1 - (1 - t) * (1 - t)) * radius;
            if (bump != null) h[k] += bump(x0 + k % bw + .5f, y0 + k / bw + .5f);
        }
        // Blurring the relief inside the mask softens the creases a distance field leaves along medial axes.
        for (int pass = 0; pass < smooth; pass++)
        {
            var src = (float[])h.Clone();
            for (int y = 0; y < bh; y++)
                for (int x = 0; x < bw; x++)
                {
                    int k = y * bw + x; if (!mask[k]) continue;
                    float sum = 0; int n = 0;
                    for (int oy = -2; oy <= 2; oy++)
                        for (int ox = -2; ox <= 2; ox++)
                        {
                            int nx = x + ox, ny = y + oy;
                            sum += nx < 0 || ny < 0 || nx >= bw || ny >= bh ? 0 : src[ny * bw + nx]; n++;
                        }
                    h[k] = sum / n;
                }
        }
        float Hh(int x, int y) => x < 0 || y < 0 || x >= bw || y >= bh ? 0 : h[y * bw + x];
        for (int y = 0; y < bh; y++)
            for (int x = 0; x < bw; x++)
            {
                int k = y * bw + x; if (!mask[k]) continue;
                int px = x0 + x, py = y0 + y;
                if (d[k] < outlineWidth) { Over(px, py, outline); continue; }
                float gx = (Hh(x + 1, y) - Hh(x - 1, y)) * .5f, gy = (Hh(x, y + 1) - Hh(x, y - 1)) * .5f;
                var n = new V3(-gx, -gy, 1).N();
                var texel = new Texel { X = px, Y = py, Depth = d[k], Normal = n, T = ts[k], S = ss[k],
                    Key = Math.Max(0, n.Dot(Key)), Rim = Math.Max(0, n.Dot(RimLight)) };
                Over(px, py, shade(texel));
            }
    }

    /// <summary>Additive coloured light that only tints covered texels, in stepped dithered rings.</summary>
    public void Light(float cx, float cy, float r, Col c, float strength, int steps = 6)
    {
        for (int y = (int)(cy - r); y <= cy + r; y++)
            for (int x = (int)(cx - r); x <= cx + r; x++)
            {
                if (!In(x, y) || P[y * W + x].A <= 0) continue;
                float dx = (x + .5f - cx) / r, dy = (y + .5f - cy) / r, dd = MathF.Sqrt(dx * dx + dy * dy);
                if (dd >= 1) continue;
                float a = (1 - dd) * (1 - dd) * strength, q = MathF.Floor(a * steps + Bayer.At(x, y)) / steps;
                if (q > 0) P[y * W + x] = P[y * W + x].Add(c, q);
            }
    }

    static float[] Distance(bool[] mask, int w, int h)
    {
        var d = new float[w * h];
        for (int k = 0; k < d.Length; k++) d[k] = mask[k] ? 1e6f : 0;
        float At(int x, int y) => x < 0 || y < 0 || x >= w || y >= h ? 0 : d[y * w + x];
        const float a = 1, b = 1.4142f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int k = y * w + x; if (d[k] == 0) continue;
                d[k] = Math.Min(d[k], Math.Min(Math.Min(At(x - 1, y) + a, At(x, y - 1) + a), Math.Min(At(x - 1, y - 1) + b, At(x + 1, y - 1) + b)));
            }
        for (int y = h - 1; y >= 0; y--)
            for (int x = w - 1; x >= 0; x--)
            {
                int k = y * w + x; if (d[k] == 0) continue;
                d[k] = Math.Min(d[k], Math.Min(Math.Min(At(x + 1, y) + a, At(x, y + 1) + a), Math.Min(At(x + 1, y + 1) + b, At(x - 1, y + 1) + b)));
            }
        return d;
    }

    public byte[] Rgba(Col? background = null, int scale = 1)
    {
        int w = W * scale, h = H * scale; var bytes = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = P[(H - 1 - y / scale) * W + x / scale];
                if (background.HasValue) c = background.Value.Under(c);
                int o = (y * w + x) * 4;
                if (c.A <= 0) continue;
                bytes[o] = Q(c.R); bytes[o + 1] = Q(c.G); bytes[o + 2] = Q(c.B); bytes[o + 3] = Q(c.A);
            }
        return bytes;
    }
    static byte Q(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255), 0, 255);
}

/// <summary>Shape tests. Tubes follow sampled curves and report position along them (t) and side (s).</summary>
public static class Shape
{
    public static (float x, float y)[] Bezier((float x, float y) a, (float x, float y) b, (float x, float y) c, (float x, float y) d, int n = 56)
    {
        var p = new (float, float)[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float t = i / (float)n, u = 1 - t;
            p[i] = (u * u * u * a.x + 3 * u * u * t * b.x + 3 * u * t * t * c.x + t * t * t * d.x,
                    u * u * u * a.y + 3 * u * u * t * b.y + 3 * u * t * t * c.y + t * t * t * d.y);
        }
        return p;
    }

    /// <summary>A tapered tube along a polyline; width(t) is the half-width in texels.</summary>
    public static Inside Tube((float x, float y)[] path, Func<float, float> width)
    {
        // Bounding radius check keeps the per-texel search cheap.
        float maxW = 0; for (int i = 0; i <= 20; i++) maxW = Math.Max(maxW, width(i / 20f));
        float minX = path.Min(p => p.x) - maxW, maxX = path.Max(p => p.x) + maxW, minY = path.Min(p => p.y) - maxW, maxY = path.Max(p => p.y) + maxW;
        return (float x, float y, out float t, out float s) =>
        {
            t = s = 0;
            if (x < minX || x > maxX || y < minY || y > maxY) return false;
            float best = float.MaxValue, bestT = 0, bestS = 0;
            for (int i = 0; i + 1 < path.Length; i++)
            {
                var (ax, ay) = path[i]; var (bx, by) = path[i + 1];
                float vx = bx - ax, vy = by - ay, l2 = vx * vx + vy * vy;
                float u = l2 > 0 ? Math.Clamp(((x - ax) * vx + (y - ay) * vy) / l2, 0, 1) : 0;
                float dx = x - (ax + vx * u), dy = y - (ay + vy * u), dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist < best) { best = dist; bestT = (i + u) / (path.Length - 1); bestS = MathF.Sign(vx * dy - vy * dx); }
            }
            float w = width(bestT);
            if (best >= w) return false;
            t = bestT; s = bestS * best / Math.Max(.001f, w);
            return true;
        };
    }

    public static Inside Ellipse(float cx, float cy, float rx, float ry, float angleDeg = 0, Func<float, float, float> wobble = null)
    {
        float a = angleDeg * MathF.PI / 180, ca = MathF.Cos(a), sa = MathF.Sin(a);
        return (float x, float y, out float t, out float s) =>
        {
            float dx = x - cx, dy = y - cy, u = (dx * ca + dy * sa) / rx, v = (-dx * sa + dy * ca) / ry;
            t = u; s = v;
            float lim = 1 + (wobble == null ? 0 : wobble(x, y));
            return u * u + v * v < lim * lim;
        };
    }

    public static Inside Union(params Inside[] parts) => (float x, float y, out float t, out float s) =>
    {
        foreach (var p in parts) if (p(x, y, out t, out s)) return true;
        t = s = 0; return false;
    };

    public static Inside Minus(Inside a, Inside b) => (float x, float y, out float t, out float s) =>
        a(x, y, out t, out s) && !b(x, y, out _, out _);
}

public static class Png
{
    public static void Write(string path, int w, int h, byte[] rgba)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var fs = File.Create(path);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13]; Be(ihdr, 0, w); Be(ihdr, 4, h); ihdr[8] = 8; ihdr[9] = 6;
        Chunk(fs, "IHDR", ihdr);
        var raw = new byte[(w * 4 + 1) * h];
        for (int y = 0; y < h; y++) { raw[y * (w * 4 + 1)] = 0; Buffer.BlockCopy(rgba, y * w * 4, raw, y * (w * 4 + 1) + 1, w * 4); }
        using (var ms = new MemoryStream())
        {
            using (var z = new ZLibStream(ms, CompressionLevel.SmallestSize, true)) z.Write(raw);
            Chunk(fs, "IDAT", ms.ToArray());
        }
        Chunk(fs, "IEND", Array.Empty<byte>());
    }
    static void Be(byte[] b, int o, int v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }
    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; Be(len, 0, data.Length); s.Write(len);
        var td = new byte[4 + data.Length]; for (int i = 0; i < 4; i++) td[i] = (byte)type[i]; Buffer.BlockCopy(data, 0, td, 4, data.Length);
        s.Write(td); var crc = new byte[4]; Be(crc, 0, (int)Crc(td)); s.Write(crc);
    }
    static uint Crc(byte[] b)
    {
        uint c = 0xFFFFFFFF;
        foreach (byte x in b) { c ^= x; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; }
        return c ^ 0xFFFFFFFF;
    }
}
