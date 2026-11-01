using System.IO.Compression;
using System.Numerics;

namespace UIPainter;

/// <summary>Straight-alpha float RGBA image with row 0 at the top.</summary>
public sealed class Img
{
    public readonly int W, H;
    public readonly Vector4[] P;
    public delegate Vector4 Shader(float x, float y);

    public Img(int w, int h) { W = w; H = h; P = new Vector4[w * h]; }

    /// <summary>Composites <paramref name="s"/> over the image at pixel centres, with ss×ss supersamples when asked.</summary>
    public Img Over(Shader s, int ss = 1)
    {
        Parallel.For(0, H, j =>
        {
            for (int i = 0; i < W; i++)
            {
                Vector4 c;
                if (ss <= 1) c = s(i + .5f, j + .5f);
                else
                {
                    var sum = Vector4.Zero;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            var k = s(i + (sx + .5f) / ss, j + (sy + .5f) / ss);
                            sum += new Vector4(k.X * k.W, k.Y * k.W, k.Z * k.W, k.W);
                        }
                    sum /= ss * ss;
                    c = sum.W > 1e-5f ? new Vector4(sum.X / sum.W, sum.Y / sum.W, sum.Z / sum.W, sum.W) : Vector4.Zero;
                }
                if (c.W <= 0) continue;
                P[j * W + i] = Blend(P[j * W + i], c);
            }
        });
        return this;
    }

    static Vector4 Blend(Vector4 d, Vector4 s)
    {
        float a = s.W + d.W * (1 - s.W);
        if (a <= 1e-6f) return Vector4.Zero;
        var rgb = (new Vector3(s.X, s.Y, s.Z) * s.W + new Vector3(d.X, d.Y, d.Z) * d.W * (1 - s.W)) / a;
        return new Vector4(rgb, MathF.Min(a, 1));
    }

    /// <summary>Copies <paramref name="src"/> over this image with its top-left corner at (x, y).</summary>
    public void Blit(Img src, int x, int y)
    {
        for (int j = 0; j < src.H; j++)
            for (int i = 0; i < src.W; i++)
            {
                int tx = x + i, ty = y + j;
                if (tx < 0 || ty < 0 || tx >= W || ty >= H) continue;
                var s = src.P[j * src.W + i]; if (s.W <= 0) continue;
                P[ty * W + tx] = Blend(P[ty * W + tx], s);
            }
    }

    public void Save(string path)
    {
        var bytes = new byte[W * H * 4];
        for (int k = 0; k < P.Length; k++)
        {
            var p = P[k];
            // Fully transparent texels take their neighbours' colour so filtering never pulls in black fringes.
            bytes[k * 4] = B(p.X); bytes[k * 4 + 1] = B(p.Y); bytes[k * 4 + 2] = B(p.Z); bytes[k * 4 + 3] = B(p.W);
        }
        Png.Write(path, W, H, bytes);
    }

    /// <summary>Spreads the colour of covered texels into fully transparent ones (alpha untouched).</summary>
    public Img Bleed(int passes = 4)
    {
        for (int pass = 0; pass < passes; pass++)
        {
            var copy = (Vector4[])P.Clone();
            for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                {
                    if (copy[j * W + i].W > 0) continue;
                    Vector3 sum = Vector3.Zero; float n = 0;
                    for (int dj = -1; dj <= 1; dj++)
                        for (int di = -1; di <= 1; di++)
                        {
                            int x = i + di, y = j + dj; if (x < 0 || y < 0 || x >= W || y >= H) continue;
                            var o = copy[y * W + x]; if (o.W <= 0 && o.X + o.Y + o.Z <= 0) continue;
                            sum += new Vector3(o.X, o.Y, o.Z); n++;
                        }
                    if (n > 0) P[j * W + i] = new Vector4(sum / n, 0);
                }
        }
        return this;
    }

    static byte B(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255), 0, 255);
}

/// <summary>Minimal RGBA8 PNG encoder; rows are stored top to bottom.</summary>
public static class Png
{
    static readonly uint[] CrcTable = BuildCrc();

    public static void Write(string path, int width, int height, byte[] rgba)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        int stride = width * 4;
        var raw = new byte[(stride + 1) * height];
        var prev = new byte[stride]; var cur = new byte[stride]; var trial = new byte[stride]; var best = new byte[stride];
        for (int row = 0; row < height; row++)
        {
            Buffer.BlockCopy(rgba, row * stride, cur, 0, stride);
            long bestScore = long.MaxValue; byte bestFilter = 0;
            for (byte f = 0; f < 5; f++)
            {
                long score = 0;
                for (int i = 0; i < stride; i++)
                {
                    int a = i >= 4 ? cur[i - 4] : 0, b = prev[i], c = i >= 4 ? prev[i - 4] : 0;
                    int pred = f switch { 0 => 0, 1 => a, 2 => b, 3 => (a + b) >> 1, _ => Paeth(a, b, c) };
                    byte v = (byte)(cur[i] - pred); trial[i] = v; score += v < 128 ? v : 256 - v;
                }
                if (score < bestScore) { bestScore = score; bestFilter = f; Buffer.BlockCopy(trial, 0, best, 0, stride); }
            }
            raw[row * (stride + 1)] = bestFilter;
            Buffer.BlockCopy(best, 0, raw, row * (stride + 1) + 1, stride);
            (prev, cur) = (cur, prev);
        }
        byte[] compressed;
        using (var ms = new MemoryStream())
        {
            using (var z = new ZLibStream(ms, CompressionLevel.SmallestSize, true)) z.Write(raw, 0, raw.Length);
            compressed = ms.ToArray();
        }
        using var fs = File.Create(path);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        BE(ihdr, 0, (uint)width); BE(ihdr, 4, (uint)height);
        ihdr[8] = 8; ihdr[9] = 6;
        Chunk(fs, "IHDR", ihdr); Chunk(fs, "IDAT", compressed); Chunk(fs, "IEND", Array.Empty<byte>());
    }

    static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; BE(len, 0, (uint)data.Length); s.Write(len);
        var t = System.Text.Encoding.ASCII.GetBytes(type); s.Write(t); s.Write(data);
        uint crc = 0xFFFFFFFF;
        foreach (var b in t) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        var c = new byte[4]; BE(c, 0, crc ^ 0xFFFFFFFF); s.Write(c);
    }

    static void BE(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

    static uint[] BuildCrc()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++) { uint c = n; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; t[n] = c; }
        return t;
    }
}
