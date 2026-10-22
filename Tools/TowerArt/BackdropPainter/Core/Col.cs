using System.Globalization;

namespace BackdropPainter;

/// <summary>sRGB colour with straight alpha. Palettes are authored and blended in sRGB like hand pixel art.</summary>
public readonly struct Col
{
    public readonly float R, G, B, A;
    public Col(float r, float g, float b, float a = 1) { R = r; G = g; B = b; A = a; }
    public static readonly Col Clear = new(0, 0, 0, 0);
    public static readonly Col White = new(1, 1, 1);
    public static readonly Col Black = new(0, 0, 0);

    public static Col Hex(string hex)
    {
        hex = hex.TrimStart('#');
        int v = int.Parse(hex.Substring(0, 6), NumberStyles.HexNumber);
        float a = hex.Length >= 8 ? int.Parse(hex.Substring(6, 2), NumberStyles.HexNumber) / 255f : 1;
        return new Col(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, a);
    }

    public string ToHex() => $"{B8(R):x2}{B8(G):x2}{B8(B):x2}";
    static int B8(float v) => (int)MathF.Round(Math.Clamp(v, 0, 1) * 255);

    public Col WithA(float a) => new(R, G, B, a);
    public static Col Lerp(Col a, Col b, float t)
    {
        t = Math.Clamp(t, 0, 1);
        return new Col(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
    }
    public static Col operator *(Col a, float k) => new(a.R * k, a.G * k, a.B * k, a.A);
    public static Col operator *(Col a, Col b) => new(a.R * b.R, a.G * b.G, a.B * b.B, a.A * b.A);
    public static Col operator +(Col a, Col b) => new(a.R + b.R, a.G + b.G, a.B + b.B, Math.Max(a.A, b.A));
    public Col Clamp() => new(Math.Clamp(R, 0, 1), Math.Clamp(G, 0, 1), Math.Clamp(B, 0, 1), Math.Clamp(A, 0, 1));
    public float Luma => R * .299f + G * .587f + B * .114f;

    /// <summary>Straight-alpha "over" compositing of <paramref name="top"/> onto this colour.</summary>
    public Col Under(Col top)
    {
        if (top.A >= 1) return top;
        if (top.A <= 0) return this;
        float a = top.A + A * (1 - top.A);
        if (a <= 0) return Clear;
        return new Col((top.R * top.A + R * A * (1 - top.A)) / a, (top.G * top.A + G * A * (1 - top.A)) / a, (top.B * top.A + B * A * (1 - top.A)) / a, a);
    }
}

/// <summary>A dark-to-light colour ramp sampled with a small ordered dither at band edges.</summary>
public sealed class Ramp
{
    public readonly Col[] C;
    public Ramp(params string[] hex) { C = hex.Select(Col.Hex).ToArray(); }
    public Ramp(params Col[] cols) { C = cols; }
    public int Count => C.Length;
    public Col this[int i] => C[Math.Clamp(i, 0, C.Length - 1)];
    public Col Last => C[^1];

    /// <summary>Picks a ramp entry for value v in [0,1]; dither widens the stippled band between entries.</summary>
    public Col At(float v, int i, int j, float dither = .45f)
    {
        float t = Math.Clamp(v, 0, 1) * (C.Length - 1);
        float b = Dither.Bayer(i, j) - .5f;
        int idx = (int)MathF.Floor(t + b * dither + .5f);
        return C[Math.Clamp(idx, 0, C.Length - 1)];
    }

    public Ramp Fogged(Col fog, float amount) => new(C.Select(c => Col.Lerp(c, fog, amount).WithA(c.A)).ToArray());
    public Ramp Shift(float lighten) => new(C.Select(c => (lighten >= 0 ? Col.Lerp(c, Col.White, lighten) : Col.Lerp(c, Col.Black, -lighten)).WithA(c.A)).ToArray());
}

public static class Dither
{
    static readonly float[] M = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    public static float Bayer(int i, int j) => (M[(j & 3) * 4 + (i & 3)] + .5f) / 16f;
    /// <summary>True on a stable fraction of pixels; used for stippled transitions.</summary>
    public static bool Hit(float fraction, int i, int j) => Bayer(i, j) < fraction;
}

public static class M
{
    public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    public static float Smooth(float a, float b, float v) { float t = Clamp01((v - a) / (b - a)); return t * t * (3 - 2 * t); }
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    public static float InvLerp(float a, float b, float v) => Clamp01((v - a) / (b - a));
    public static float Fract(float v) => v - MathF.Floor(v);
    public static int Mod(int a, int n) { int r = a % n; return r < 0 ? r + n : r; }
    public static float Mod(float a, float n) { float r = a % n; return r < 0 ? r + n : r; }
    public static float Step(float v, float steps) => MathF.Floor(v * steps) / steps;
}

/// <summary>Deterministic xorshift generator so every bake is byte-identical.</summary>
public sealed class Rng
{
    ulong s;
    public Rng(int seed) { s = 0x9E3779B97F4A7C15UL ^ (ulong)(uint)seed * 0xBF58476D1CE4E5B9UL; Next(); Next(); }
    public ulong Next() { s ^= s << 13; s ^= s >> 7; s ^= s << 17; return s; }
    public float F() => (Next() >> 40) / (float)(1UL << 24);
    public float R(float a, float b) => a + (b - a) * F();
    public int I(int a, int bExclusive) => a + (int)(F() * (bExclusive - a));
    public bool Chance(float p) => F() < p;
}
