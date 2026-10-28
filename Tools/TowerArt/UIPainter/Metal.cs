using System.Numerics;

namespace UIPainter;

/// <summary>Piecewise-linear colour ramp over a shade value.</summary>
public sealed class Ramp
{
    readonly (float t, Vector3 c)[] stops;
    public Ramp(params (float t, Vector3 c)[] stops) { this.stops = stops; }

    public Vector3 At(float t)
    {
        if (t <= stops[0].t) return stops[0].c;
        for (int i = 1; i < stops.Length; i++)
            if (t <= stops[i].t) return Vector3.Lerp(stops[i - 1].c, stops[i].c, (t - stops[i - 1].t) / (stops[i].t - stops[i - 1].t));
        return stops[^1].c;
    }

    static Vector3 C(float r, float g, float b) => new(r, g, b);

    /// <summary>Gold leaf: bronze shadows, warm mid tones and near-white glints.</summary>
    public static readonly Ramp Gold = new((0, C(.13f, .07f, .03f)), (.32f, C(.42f, .26f, .09f)), (.58f, C(.80f, .59f, .26f)), (.78f, C(.96f, .83f, .50f)), (.98f, C(1, .96f, .82f)), (1.2f, C(1, 1, .97f)));
    /// <summary>Neutral silver, tinted at runtime for tiers and branches.</summary>
    public static readonly Ramp Silver = new((0, C(.09f, .10f, .12f)), (.32f, C(.34f, .36f, .40f)), (.58f, C(.68f, .70f, .74f)), (.78f, C(.88f, .89f, .92f)), (.98f, C(.98f, .98f, 1)), (1.2f, C(1, 1, 1)));
    public static readonly Ramp Sapphire = new((0, C(.01f, .02f, .07f)), (.4f, C(.05f, .12f, .38f)), (.75f, C(.22f, .44f, .86f)), (.95f, C(.70f, .86f, 1)), (1.2f, C(1, 1, 1)));
    public static readonly Ramp Ruby = new((0, C(.06f, .0f, .01f)), (.4f, C(.36f, .03f, .06f)), (.75f, C(.82f, .16f, .2f)), (.95f, C(1, .66f, .62f)), (1.2f, C(1, 1, 1)));
    public static readonly Ramp Glass = new((0, C(.05f, .05f, .06f)), (.4f, C(.22f, .23f, .26f)), (.75f, C(.56f, .58f, .62f)), (.95f, C(.92f, .93f, .96f)), (1.2f, C(1, 1, 1)));
    public static readonly Ramp Bone = new((0, C(.12f, .10f, .08f)), (.35f, C(.45f, .40f, .33f)), (.65f, C(.82f, .77f, .66f)), (.9f, C(.97f, .94f, .86f)), (1.2f, C(1, 1, 1)));
    public static readonly Ramp Crystal = new((0, C(.01f, .06f, .10f)), (.4f, C(.08f, .35f, .55f)), (.75f, C(.42f, .80f, .98f)), (.95f, C(.85f, .97f, 1)), (1.2f, C(1, 1, 1)));
    public static readonly Ramp Ember = new((0, C(.12f, .02f, .0f)), (.4f, C(.62f, .16f, .02f)), (.75f, C(.98f, .55f, .12f)), (.95f, C(1, .90f, .55f)), (1.2f, C(1, 1, .9f)));
    public static readonly Ramp Jade = new((0, C(.0f, .06f, .05f)), (.4f, C(.05f, .32f, .26f)), (.75f, C(.30f, .80f, .64f)), (.95f, C(.80f, 1, .92f)), (1.2f, C(1, 1, 1)));
}

/// <summary>Bevelled metal: a height field rising inside each shape, lit from the upper left.</summary>
public static class Metal
{
    static readonly Vector3 Light = Vector3.Normalize(new Vector3(-.5f, -.75f, .75f));
    static readonly Vector3 Half = Vector3.Normalize(Light + Vector3.UnitZ);

    /// <summary>Shades the interior of <paramref name="sdf"/> as metal bevelled over <paramref name="bevel"/> pixels.</summary>
    public static Img.Shader Shade(Func<Vector2, float> sdf, float bevel, Ramp ramp, float opacity = 1, float relief = 1.1f, float gloss = .55f)
    {
        float Height(Vector2 p) { float t = Math.Clamp(-sdf(p) / bevel, 0, 1); return MathF.Sqrt(1 - (1 - t) * (1 - t)); }
        return (x, y) =>
        {
            var p = new Vector2(x, y);
            float d = sdf(p), cov = Sdf.Cov(d);
            if (cov <= 0) return default;
            float hx = Height(p + Vector2.UnitX * .5f) - Height(p - Vector2.UnitX * .5f);
            float hy = Height(p + Vector2.UnitY * .5f) - Height(p - Vector2.UnitY * .5f);
            var n = Vector3.Normalize(new Vector3(-hx * bevel * relief, -hy * bevel * relief, 1));
            float diffuse = MathF.Max(0, Vector3.Dot(n, Light));
            float spec = MathF.Pow(MathF.Max(0, Vector3.Dot(n, Half)), 26);
            float shade = .26f + .66f * diffuse + gloss * spec;
            // The outer lip falls into shadow so each piece reads against any background.
            shade *= .62f + .38f * Sdf.Smooth(0, .45f, Math.Clamp(-d / bevel, 0, 1));
            return new Vector4(ramp.At(shade), cov * opacity);
        };
    }

    /// <summary>A soft dark halo around a shape that lifts it off the panel below.</summary>
    public static Img.Shader Shadow(Func<Vector2, float> sdf, float reach, float alpha, Vector2 offset = default)
    {
        return (x, y) =>
        {
            float d = sdf(new Vector2(x, y) - offset);
            float a = alpha * (1 - Sdf.Smooth(-1, reach, d));
            return a <= 0 ? default : new Vector4(0, 0, 0, a);
        };
    }

    /// <summary>Flat colour clipped to a shape.</summary>
    public static Img.Shader Fill(Func<Vector2, float> sdf, Vector3 color, float alpha = 1)
    {
        return (x, y) =>
        {
            float cov = Sdf.Cov(sdf(new Vector2(x, y)));
            return cov <= 0 ? default : new Vector4(color, cov * alpha);
        };
    }
}
