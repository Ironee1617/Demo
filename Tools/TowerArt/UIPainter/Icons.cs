using System.Numerics;

namespace UIPainter;

/// <summary>A row of 64-pixel metal icons; the order matches TowerGame.UIIcon.</summary>
public static class Icons
{
    public const int Cell = 64;
    static Vector2 V(float x, float y) => new(x, y);
    static readonly Vector2 Mid = new(32, 32);

    public static Img Atlas()
    {
        var parts = new Func<Img>[] { Sword, Hourglass, Target, Speed, Star, Heart, Shield, Skull, Crystal, Tower, Flame, Plus, Coin };
        var atlas = new Img(Cell * parts.Length, Cell);
        for (int i = 0; i < parts.Length; i++) atlas.Blit(parts[i](), i * Cell, 0);
        return atlas;
    }

    /// <summary>Draws each layer as bevelled metal over one shared dark outline.</summary>
    static Img Icon(params (Func<Vector2, float> sdf, Ramp ramp, float bevel)[] layers)
    {
        var img = new Img(Cell, Cell);
        float All(Vector2 p) { float d = float.MaxValue; foreach (var l in layers) d = MathF.Min(d, l.sdf(p)); return d; }
        img.Over(Metal.Fill(p => All(p) - 2.6f, new Vector3(.02f, .02f, .035f), .88f), 2);
        foreach (var l in layers) img.Over(Metal.Shade(l.sdf, l.bevel, l.ramp), 2);
        return img;
    }

    static Img Sword() => Icon(
        (p => Sdf.Segment(p, V(23, 41), V(50, 13), 3.6f, .7f), Ramp.Silver, 2.2f),
        (p => Sdf.Segment(p, V(15, 33), V(31, 49), 2.9f), Ramp.Gold, 2f),
        (p => MathF.Min(Sdf.Segment(p, V(21, 43), V(12, 52), 2.4f), Sdf.Circle(p, V(10, 54), 3.8f)), Ramp.Gold, 2f));

    static Img Hourglass()
    {
        float Glass(Vector2 p) => Sdf.Polygon(p, V(21, 15), V(43, 15), V(34.5f, 32), V(43, 49), V(21, 49), V(29.5f, 32));
        float Frame(Vector2 p) => MathF.Min(MathF.Min(Sdf.Box(p, V(32, 11), V(16, 3), 1.5f), Sdf.Box(p, V(32, 53), V(16, 3), 1.5f)),
            MathF.Min(Sdf.Segment(p, V(18, 13), V(18, 51), 1.4f), Sdf.Segment(p, V(46, 13), V(46, 51), 1.4f)));
        return Icon((Glass, Ramp.Glass, 3f), (p => Sdf.Polygon(p, V(24, 49), V(40, 49), V(32, 38)), Ramp.Gold, 2f), (Frame, Ramp.Gold, 1.8f));
    }

    static Img Target() => Icon(
        (p => MathF.Min(Sdf.Outline(Sdf.Circle(p, Mid, 15), 2.5f), MathF.Min(MathF.Min(Sdf.Segment(p, V(32, 5), V(32, 17), 2.2f), Sdf.Segment(p, V(32, 47), V(32, 59), 2.2f)),
            MathF.Min(Sdf.Segment(p, V(5, 32), V(17, 32), 2.2f), Sdf.Segment(p, V(47, 32), V(59, 32), 2.2f)))), Ramp.Silver, 2f),
        (p => Sdf.Circle(p, Mid, 4), Ramp.Ruby, 3f));

    static Img Speed()
    {
        float Chevrons(Vector2 p)
        {
            float d = float.MaxValue;
            for (int k = 0; k < 2; k++) { float x = 13 + k * 17; d = MathF.Min(d, MathF.Min(Sdf.Segment(p, V(x, 14), V(x + 14, 32), 3.4f), Sdf.Segment(p, V(x + 14, 32), V(x, 50), 3.4f))); }
            return d;
        }
        return Icon((Chevrons, Ramp.Jade, 2.4f));
    }

    static Img Star() => Icon((p => Sdf.Star(p, V(32, 34), 26, .46f), Ramp.Gold, 3.5f));

    static Img Heart() => Icon((p => Sdf.Heart(p, V(32, 31), 42), Ramp.Ruby, 5f));

    static Img Shield() => Icon(
        (p => Sdf.Polygon(p, V(14, 11), V(50, 11), V(50, 31), V(32, 56), V(14, 31)), Ramp.Silver, 3.5f),
        (p => Sdf.Diamond(p, V(32, 30), 9), Ramp.Gold, 2.5f));

    static Img Skull()
    {
        float Head(Vector2 p)
        {
            float d = MathF.Min(Sdf.Circle(p, V(32, 27), 17), Sdf.Box(p, V(32, 44), V(10, 8), 3));
            d = MathF.Max(d, -Sdf.Circle(p, V(25, 29), 5));
            d = MathF.Max(d, -Sdf.Circle(p, V(39, 29), 5));
            d = MathF.Max(d, -Sdf.Diamond(p, V(32, 39), 3.2f));
            return d;
        }
        return Icon((Head, Ramp.Bone, 3f));
    }

    static Img Crystal() => Icon(
        (p => Sdf.Polygon(p, V(32, 5), V(47, 25), V(32, 59), V(17, 25)), Ramp.Crystal, 4f),
        (p => MathF.Min(Sdf.Segment(p, V(19, 25), V(45, 25), .6f), Sdf.Segment(p, V(32, 7), V(32, 57), .5f)), Ramp.Glass, .6f));

    static Img Tower() => Icon(
        (p => Sdf.Polygon(p, V(21, 58), V(43, 58), V(38.5f, 22), V(43, 19), V(32, 4), V(21, 19), V(25.5f, 22)), Ramp.Silver, 2.6f),
        (p => MathF.Min(Sdf.Box(p, V(32, 32), V(2.2f, 3.2f), 1), Sdf.Box(p, V(32, 46), V(2.4f, 3.6f), 1)), Ramp.Ember, 1.5f));

    static Img Flame()
    {
        float Body(Vector2 p) => MathF.Min(Sdf.Circle(p, V(32, 42), 13.5f), Sdf.Polygon(p, V(19.5f, 39), V(44.5f, 39), V(37, 22), V(33, 5), V(26, 20)));
        return Icon((Body, Ramp.Ember, 4.5f), (p => Sdf.Circle(p, V(32, 45), 6), Ramp.Gold, 4f));
    }

    static Img Plus() => Icon((p => MathF.Min(Sdf.Box(p, Mid, V(6, 18), 2), Sdf.Box(p, Mid, V(18, 6), 2)), Ramp.Jade, 3f));

    static Img Coin() => Icon(
        (p => Sdf.Circle(p, Mid, 21), Ramp.Gold, 4f),
        (p => MathF.Min(Sdf.Outline(Sdf.Circle(p, Mid, 15), 1.1f), Sdf.Diamond(p, Mid, 6)), Ramp.Gold, 1.4f));
}
