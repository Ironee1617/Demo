using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>Veda walkway: red sandstone coping over a carved lotus frieze draped with marigold garlands,
/// sandstone ashlar below, carved slabs hung with garlands and little bells, and a brass lamp, a marigold pot
/// and a stone lotus as props.</summary>
public sealed class VedaTerrain : TerrainKit
{
    public override string Id => "veda";
    public override Lighting Light() => VedaScene.TerrainLight();

    static readonly TerrainBake.Masonry Ashlar = new()
    {
        Course = .65f, MinLen = 1.2f, MaxLen = 2.2f, Light = "c48c6e", Dark = "925c44", Mortar = "a87058", Dirt = "4e3024",
        Joint = .01f, Bevel = .04f, Chips = .8f, Grime = .3f, Streaks = .35f, Rough = .85f, Stains = .35f, Stain = "6a4434", Seed = 61,
    };

    static int Marigold(Scene sc, int seed)
    {
        var n = sc.N;
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float t = n.N(p, .03f, seed) * .5f + .5f;
            s.Albedo = R.Mix(R.Rgb("e2500c"), R.Rgb("ffaa1c"), t); s.Rough = .7f; s.Trans = .3f;
            s.N = Mats.Bump(q => n.Fbm(q, .02f, 2, seed + 1) * .008f, p, s.N, 1, .003f);
        });
    }

    /// <summary>Lotus petals in low relief: a repeating fan of pointed petals across a band.</summary>
    static float Lotus(float x, float y, float cell, float bandH)
    {
        float u = R.Mod(x, cell) - cell * .5f, v = y / bandH;
        float best = 9;
        for (int k = -2; k <= 2; k++)
        {
            float ang = k * .38f; float px = u * MathF.Cos(ang) - (v * bandH) * MathF.Sin(ang), py = u * MathF.Sin(ang) + (v * bandH) * MathF.Cos(ang);
            float w = cell * .16f * (1 - R.Clamp01(py / (bandH * .95f))); best = MathF.Min(best, MathF.Abs(px) - w);
        }
        return R.Smooth(.008f, -.008f, best) * R.Smooth(0, .02f, v) * R.Smooth(1, .9f, v);
    }

    public override void Top(Scene sc, Mats m)
    {
        var n = sc.N;
        int stone = m.Sandstone("8a4630", "cc8e64", .22f, 601);
        int carved = m.Rock("8a4a32", "c88864", .5f, .85f, .15f, 602);
        int marigold = Marigold(sc, 603);
        sc.Add(new Vector3(-1, -.95f, -.95f), new Vector3(13, .1f, .8f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start;
            a.Add(Sdf.Box(p - new Vector3(6, -.09f, 0), new Vector3(7, .09f, .62f)) - .01f, stone);
            a.Add(Sdf.Box(p - new Vector3(6, -.2f, -.62f), new Vector3(7, .04f, .05f)) - .015f, stone);
            float frieze = Sdf.Box(p - new Vector3(6, -.44f, -.38f), new Vector3(7, .2f, .18f)) - Lotus(p.X, p.Y + .64f, .32f, .4f) * .02f;
            a.Add(frieze, carved);
            a.Add(Sdf.Box(p - new Vector3(6, -.68f, -.6f), new Vector3(7, .03f, .04f)) - .01f, stone);
            for (float gx = 0; gx < 12; gx += 1.5f)
            {
                float d = VedaScene.Garland(p, new Vector3(gx, -.22f, -.72f), new Vector3(gx + 1.5f, -.22f, -.72f), .2f, .035f);
                if (d < a.D) a.Add(d, marigold);
            }
            mt = a.M; return a.D;
        });
    }

    void SlabShape(Scene sc, Mats m, float x0, float x1, bool end)
    {
        int stone = m.Sandstone("8a4630", "cc8e64", .22f, 611);
        int marigold = Marigold(sc, 612);
        int brass = m.Metal("d8a048", .3f, .3f, "5a4a2a", 613);
        float cx = (x0 + x1) * .5f, hw = (x1 - x0) * .5f;
        sc.Add(new Vector3(x0 - .3f, -1, -.8f), new Vector3(x1 + .3f, .05f, .8f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start; var q = p - new Vector3(cx, 0, 0);
            a.Add(Sdf.Box(q - new Vector3(0, -.12f, 0), new Vector3(hw - (end ? .02f : -.1f), .12f, .58f)) - .01f, stone);
            a.Add(Sdf.Box(q - new Vector3(0, -.3f, -.04f), new Vector3(hw - (end ? .1f : -.1f), .07f, .5f)) - Lotus(p.X, p.Y + .37f, .26f, .14f) * .012f, stone);
            if (end) a.Add(MathF.Min(Sdf.Sphere(q - new Vector3(hw - .1f, -.46f, -.2f), .07f), Sdf.ConeY(q - new Vector3(hw - .1f, -.38f, -.2f), .05f, .07f, .02f)), stone);
            for (float gx = x0 - 1.5f; gx < x1; gx += 1.5f)
            {
                if (end && gx + 1.5f > x1 - .1f) continue;
                a.Add(VedaScene.Garland(p, new Vector3(gx, -.38f, -.62f), new Vector3(gx + 1.5f, -.38f, -.62f), .22f, .035f), marigold);
                float bx = p.X - gx;
                a.Add(Sdf.Capsule(new Vector3(bx, p.Y, p.Z + .6f), new Vector3(0, -.38f, 0), new Vector3(0, -.62f, 0), .007f), brass);
                a.Add(MathF.Max(Sdf.ConeY(new Vector3(bx, p.Y + .68f, p.Z + .6f), .06f, .06f, .02f), -(p.Y + .74f)), brass);
            }
            mt = a.M; return a.D;
        });
    }

    public override void Slab(Scene sc, Mats m) => SlabShape(sc, m, -1, 7, false);
    public override void Cap(Scene sc, Mats m, float end) => SlabShape(sc, m, end - 1.9f, end, true);
    public override TileSample Body(TileNoise n, float x, float y) => TerrainBake.Stone(n, x, y, Ashlar);

    public override void Prop(int index, Scene sc, Mats m)
    {
        var n = sc.N; float cx = .75f;
        if (index == 0)
        {
            int brass = m.Metal("d8a048", .28f, .25f, "5a4a2a", 621);
            int fire = m.Glow("ffa040", 12, "2a1008", .3f, 622);
            sc.Add(new Vector3(cx - .3f, -.02f, -.3f), new Vector3(cx + .3f, 1.35f, .3f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(cx, 0, 0); var a = Acc.Start;
                float stand = MathF.Min(Sdf.CylinderY(q - new Vector3(0, .55f, 0), .03f, .55f), Sdf.ConeY(q - new Vector3(0, .06f, 0), .06f, .18f, .08f));
                for (int k = 0; k < 2; k++) stand = MathF.Min(stand, Sdf.CylinderY(q - new Vector3(0, .5f + k * .45f, 0), .14f - k * .04f, .018f));
                stand = MathF.Min(stand, MathF.Max(Sdf.Sphere(q - new Vector3(0, 1.12f, 0), .12f), q.Y - 1.12f));
                a.Add(stand, brass);
                a.Add(Sdf.Ellipsoid(q - new Vector3(.05f, 1.2f, 0), new Vector3(.035f, .08f, .035f)), fire);
                mt = a.M; return a.D;
            });
        }
        else if (index == 1)
        {
            int clay = m.Paint("a8502c", .7f, .3f, 631, .03f);
            int marigold = Marigold(sc, 632);
            int leaf = m.Foliage("2a4a20", "5a7a34", .3f, 633);
            sc.Add(new Vector3(cx - .45f, -.02f, -.45f), new Vector3(cx + .45f, .95f, .45f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(cx, 0, 0); var a = Acc.Start;
                float r = q.Y < .5f ? .2f + MathF.Sin(q.Y / .5f * MathF.PI) * .08f : .2f;
                float pot = MathF.Max((MathF.Sqrt(q.X * q.X + q.Z * q.Z) - r) * .85f, MathF.Max(-q.Y, q.Y - .52f));
                a.Add(Sdf.Subtract(pot, Sdf.CylinderY(q - new Vector3(0, .52f, 0), .17f, .06f)), clay);
                var c = sc.N.Voronoi(q, .09f, 634);
                float flowers = Sdf.Ellipsoid(q - new Vector3(0, .62f, 0), new Vector3(.3f, .18f, .28f)) + (c.F1 - .035f) * .5f;
                a.Add(flowers, marigold);
                a.Add(Sdf.Ellipsoid(q - new Vector3(0, .56f, 0), new Vector3(.32f, .06f, .3f)) + n.Fbm(p, .04f, 3, 635) * .03f, leaf);
                mt = a.M; return a.D;
            });
        }
        else
        {
            int stone = m.Rock("8a4a32", "c88864", .5f, .85f, .2f, 641);
            sc.Add(new Vector3(cx - .55f, -.02f, -.55f), new Vector3(cx + .55f, .75f, .55f), (Vector3 p, out int mt) =>
            {
                mt = stone; var q = p - new Vector3(cx, 0, 0);
                float baseD = Sdf.CylinderY(q - new Vector3(0, .08f, 0), .38f, .08f);
                var pq = Sdf.RepeatAngleY(q - new Vector3(0, .16f, 0), 10);
                float petals = Sdf.Ellipsoid(Sdf.RotZ(pq - new Vector3(.24f, .14f, 0), -.6f), new Vector3(.17f, .07f, .09f));
                var iq = Sdf.RepeatAngleY(Sdf.RotY(q - new Vector3(0, .28f, 0), .3f), 8);
                float inner = Sdf.Ellipsoid(Sdf.RotZ(iq - new Vector3(.12f, .1f, 0), -1f), new Vector3(.12f, .05f, .07f));
                return MathF.Min(MathF.Min(baseD, petals), MathF.Min(inner, Sdf.Sphere(q - new Vector3(0, .36f, 0), .08f))) + n.Fbm(p, .06f, 3, 642) * .006f;
            });
        }
    }
}
