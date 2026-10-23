using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>Eden walkway: a flowered turf over a rich earth bank with pebbles and roots, floating clods of meadow
/// trailing roots and blossoming vines, and a flowering bush, lilies and a mossy log as props.</summary>
public sealed class EdenTerrain : TerrainKit
{
    public override string Id => "eden";
    public override Lighting Light() => EdenScene.TerrainLight();

    static readonly TerrainBake.Soil Bank = new() { Dark = "3e2c1c", Light = "6e5236", Pebble = "9a9080", PebbleDark = "5a5246", Root = "2a1a10", Pebbles = .45f, Roots = .6f, Seed = 51 };

    /// <summary>Turf whose top face is the walkable line, with grass blades and flowers breaking the edges.</summary>
    static float Turf(Noise n, Vector3 p, float x0, float x1)
    {
        float lump = n.Fbm(new Vector3(p.X, 0, p.Z), .3f, 3, 501) * .02f;
        float turf = Sdf.Box(p - new Vector3((x0 + x1) * .5f, -.08f, -.02f), new Vector3((x1 - x0) * .5f, .08f + lump, .62f)) - .025f;
        float blades = MathF.Max(p.Y - .02f - (n.Fbm(new Vector3(p.X * 3.5f, 0, p.Z * 2), .1f, 3, 502) * .5f + .5f) * .12f, MathF.Max(-p.Y - .03f, MathF.Abs(p.Z + .52f) - .12f));
        blades = MathF.Max(blades, MathF.Max(x0 - p.X, p.X - x1));
        return MathF.Min(turf, blades * .7f);
    }

    public override void Top(Scene sc, Mats m)
    {
        var n = sc.N;
        int turf = m.Ground("3a6a26", "8cb24c", "5a4430", 503, "f8d0e0", .3f);
        int earth = m.Ground("3e2c1c", "6e5236", "4a3626", 504);
        int pebble = m.Rock("5a5246", "a09684", .3f, .8f, .2f, 505);
        sc.Add(new Vector3(-1, -.95f, -.95f), new Vector3(13, .2f, .8f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start;
            a.Add(Turf(n, p, -1, 13), turf);
            float bank = MathF.Max(-.56f - p.Z + n.Fbm(p, .2f, 4, 506) * .04f, p.Y + .12f);
            a.Add(bank, earth);
            var c = n.Voronoi(p, .2f, 507);
            if (p.Z < -.4f && p.Y < -.2f) a.Add(c.F1 - .055f - R.Hash01((int)(c.Id & 0xffff), 0, 0, 508) * .03f + MathF.Max(0, -.5f - p.Z) * .5f, pebble);
            mt = a.M; return a.D;
        });
    }

    void Clod(Scene sc, Mats m, float x0, float x1, bool end)
    {
        var n = sc.N;
        int turf = m.Ground("3a6a26", "8cb24c", "5a4430", 511, "f8d0e0", .3f);
        int earth = m.Ground("3e2c1c", "6e5236", "4a3626", 512);
        int root = m.Wood("3a2a1c", "6a4e34", .9f, true, 513);
        int vine = m.Foliage("2a5a22", "6a9a40", .4f, 514, "f4a8c4", .45f);
        float cx = (x0 + x1) * .5f, hw = (x1 - x0) * .5f;
        sc.Add(new Vector3(x0 - .4f, -1, -.8f), new Vector3(x1 + .4f, .2f, .8f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start; var q = p - new Vector3(cx, 0, 0);
            a.Add(Turf(n, p, x0 + (end ? 0 : -.1f), x1 - (end ? .05f : -.1f)), turf);
            // An earth clod tapering downward with a lumpy underside.
            float depth = .42f + n.Fbm(new Vector3(p.X, 0, 0), .5f, 3, 515) * .1f;
            float clod = MathF.Max(Sdf.RoundBox(q - new Vector3(0, -.3f, 0), new Vector3(hw - (end ? .05f : -.2f), .3f, .56f), end ? .18f : .05f), -(p.Y + depth) - (MathF.Abs(q.Z) * .2f));
            clod += n.Fbm(p, .15f, 4, 516) * .04f;
            a.Add(clod, earth);
            for (int k = 0; k < 7; k++)
            {
                float rx = x0 + (k + .5f) / 7 * (x1 - x0) + MathF.Sin(k * 2.1f) * .15f;
                if (end && rx > x1 - .25f) continue;
                float len = .2f + R.Hash01(k, (int)x0, 0, 517) * .4f;
                var top = new Vector3(rx, -depth + .05f, -.25f + (k % 3) * .2f); var tip = top + new Vector3(MathF.Sin(k) * .12f, -len, 0);
                if (k % 2 == 0) a.Add(Sdf.RoundCone(p, top, tip, .03f, .006f), root);
                else
                {
                    float strand = Sdf.Capsule(p, top, tip, .02f);
                    float leaves = Sdf.Sphere(new Vector3(p.X - tip.X, R.Mod(p.Y, .12f) - .06f, p.Z - tip.Z), .045f);
                    a.Add(MathF.Min(strand, p.Y < top.Y && p.Y > tip.Y ? leaves : 9), vine);
                }
            }
            mt = a.M; return a.D;
        });
    }

    public override void Slab(Scene sc, Mats m) => Clod(sc, m, -1, 7, false);
    public override void Cap(Scene sc, Mats m, float end) => Clod(sc, m, end - 1.9f, end, true);
    public override TileSample Body(TileNoise n, float x, float y) => TerrainBake.Earth(n, x, y, Bank);

    public override void Prop(int index, Scene sc, Mats m)
    {
        var n = sc.N; float cx = .75f;
        if (index == 0)
        {
            int bush = m.Foliage("285a24", "7aaa48", .42f, 521, "f4a0bc", .6f);
            var rnd = new Rand(522); var lobes = Kit3D.Crown(rnd, new Vector3(cx, .38f, 0), 1.05f, .5f, 12, .7f);
            sc.Add(new Vector3(cx - .72f, -.02f, -.6f), new Vector3(cx + .72f, 1.1f, .6f), (Vector3 p, out int mt) => { mt = bush; return Kit3D.Canopy(n, p, lobes, .07f, 523); });
        }
        else if (index == 1)
        {
            int leaf = m.Foliage("2e6a2a", "72a84a", .35f, 531);
            int petal = m.Paint("fbf4ee", .55f, .1f, 532);
            int pollen = m.Paint("f0c040", .5f, .1f, 533);
            sc.Add(new Vector3(cx - .6f, -.02f, -.4f), new Vector3(cx + .6f, 1.2f, .4f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(cx, 0, 0); var a = Acc.Start;
                for (int k = 0; k < 3; k++)
                {
                    float sx = (k - 1) * .28f, h = .7f + k % 2 * .25f;
                    a.Add(Sdf.Capsule(q, new Vector3(sx, 0, 0), new Vector3(sx * 1.2f, h, 0), .018f), leaf);
                    var fq = q - new Vector3(sx * 1.2f, h + .05f, 0);
                    float trumpet = MathF.Max(Sdf.ConeY(fq, .1f, .02f, .12f), -Sdf.ConeY(fq - new Vector3(0, .02f, 0), .1f, .0f, .1f));
                    a.Add(trumpet, petal); a.Add(Sdf.Sphere(fq - new Vector3(0, .05f, 0), .02f), pollen);
                }
                for (int k = 0; k < 5; k++)
                {
                    float an = k * 1.25f; var dir = new Vector3(MathF.Cos(an), 0, MathF.Sin(an) * .5f);
                    a.Add(Sdf.Capsule(q, Vector3.Zero, dir * .32f + new Vector3(0, .38f, 0), .025f), leaf);
                }
                mt = a.M; return a.D;
            });
        }
        else
        {
            int bark = m.Wood("4a3424", "7a5e44", .9f, false, 541);
            int moss = m.Foliage("3a6a2a", "7aa048", .3f, 542);
            int cap = m.Paint("c86a3a", .5f, .2f, 543);
            sc.Add(new Vector3(cx - .72f, -.02f, -.5f), new Vector3(cx + .72f, .7f, .5f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(cx, 0, 0); var a = Acc.Start;
                a.Add(Sdf.CylinderX(q - new Vector3(0, .24f, 0), .24f, .62f) + n.Fbm(p, .08f, 3, 544) * .02f, bark);
                a.Add(Sdf.Ellipsoid(q - new Vector3(-.1f, .45f, -.05f), new Vector3(.4f, .07f, .22f)) + n.Fbm(p, .04f, 3, 545) * .03f, moss);
                foreach (float mx in new[] { .25f, .4f }) a.Add(MathF.Max(Sdf.Ellipsoid(q - new Vector3(mx, .5f, -.16f), new Vector3(.07f, .04f, .07f)), -(q.Y - .48f)), cap);
                mt = a.M; return a.D;
            });
        }
    }
}
