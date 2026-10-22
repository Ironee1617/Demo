using System.IO.Compression;

namespace BackdropPainter;

/// <summary>Minimal RGBA8 PNG encoder with per-row adaptive filtering.</summary>
public static class Png
{
    static readonly uint[] CrcTable = BuildCrc();

    /// <summary>Writes rows top to bottom; <paramref name="rgba"/> is stored bottom row first.</summary>
    public static void Write(string path, int width, int height, byte[] rgba)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        int stride = width * 4;
        var raw = new byte[(stride + 1) * height];
        var prev = new byte[stride];
        var cur = new byte[stride];
        var trial = new byte[stride];
        var best = new byte[stride];
        for (int row = 0; row < height; row++)
        {
            Buffer.BlockCopy(rgba, (height - 1 - row) * stride, cur, 0, stride);
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
        ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
        Chunk(fs, "IHDR", ihdr);
        Chunk(fs, "IDAT", compressed);
        Chunk(fs, "IEND", Array.Empty<byte>());
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
