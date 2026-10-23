using System.Numerics;
using System.Runtime.CompilerServices;

namespace BackdropPainter.Render;

/// <summary>Scalar and colour helpers for the physically lit renderer. Colours are linear RGB in Vector3.</summary>
public static class R
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Mix(float a, float b, float t) => a + (b - a) * t;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Vector3 Mix(Vector3 a, Vector3 b, float t) => a + (b - a) * t;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Smooth(float a, float b, float v) { float t = Clamp01((v - a) / (b - a)); return t * t * (3 - 2 * t); }
    public static float Fract(float v) => v - MathF.Floor(v);
    public static float Mod(float a, float n) { float r = a % n; return r < 0 ? r + n : r; }
    /// <summary>Signed shortest offset of a from b on a strip that repeats every period.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Wrap(float a, float b, float period) { float d = (a - b) % period; if (d > period * .5f) d -= period; else if (d < -period * .5f) d += period; return d; }
    public static float SMin(float a, float b, float k)
    {
        // Outside the blend band return the nearer value exactly: mixing with a far sentinel such as 1e9 would
        // otherwise cancel to zero in single precision and turn empty space solid.
        float h = Clamp01(.5f + .5f * (b - a) / k);
        if (h <= 0) return b;
        if (h >= 1) return a;
        return Mix(b, a, h) - k * h * (1 - h);
    }
    public static float SMax(float a, float b, float k) => -SMin(-a, -b, k);

    /// <summary>sRGB hex to linear RGB.</summary>
    public static Vector3 Rgb(string hex)
    {
        hex = hex.TrimStart('#');
        int v = int.Parse(hex.Substring(0, 6), System.Globalization.NumberStyles.HexNumber);
        return new Vector3(ToLinear(((v >> 16) & 255) / 255f), ToLinear(((v >> 8) & 255) / 255f), ToLinear((v & 255) / 255f));
    }
    public static float ToLinear(float c) => c <= .04045f ? c / 12.92f : MathF.Pow((c + .055f) / 1.055f, 2.4f);
    public static float ToSrgb(float c) { c = Clamp01(c); return c <= .0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1 / 2.4f) - .055f; }

    /// <summary>ACES filmic curve (Narkowicz fit) on linear values.</summary>
    public static Vector3 Aces(Vector3 x)
    {
        const float a = 2.51f, b = .03f, c = 2.43f, d = .59f, e = .14f;
        return new Vector3(Ac(x.X), Ac(x.Y), Ac(x.Z));
        static float Ac(float v) { v = MathF.Max(0, v); return Clamp01(v * (a * v + b) / (v * (c * v + d) + e)); }
    }
    public static float Luma(Vector3 c) => c.X * .2126f + c.Y * .7152f + c.Z * .0722f;
    public static Vector3 Saturate(Vector3 c, float s) { float l = Luma(c); return new Vector3(l) + (c - new Vector3(l)) * s; }

    public static uint Hash(uint x)
    {
        unchecked { x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16; return x; }
    }
    public static uint Hash(int a, int b, int c, int seed)
    {
        unchecked { return Hash((uint)a * 0x8DA6B343u ^ Hash((uint)b * 0xD8163841u ^ Hash((uint)c * 0xCB1AB31Fu ^ (uint)seed * 0x9E3779B1u))); }
    }
    public static float Hash01(int a, int b, int c, int seed) => (Hash(a, b, c, seed) & 0xFFFFFF) / 16777216f;
}

/// <summary>Deterministic generator for scene layout, so every bake is identical.</summary>
public sealed class Rand
{
    ulong s;
    public Rand(int seed) { s = 0x9E3779B97F4A7C15UL ^ (ulong)(uint)seed * 0xBF58476D1CE4E5B9UL; Next(); Next(); }
    public ulong Next() { s ^= s << 13; s ^= s >> 7; s ^= s << 17; return s; }
    public float F() => (Next() >> 40) / (float)(1UL << 24);
    public float R(float a, float b) => a + (b - a) * F();
    public int I(int a, int bExclusive) => a + (int)(F() * (bExclusive - a));
    public bool Chance(float p) => F() < p;
}
