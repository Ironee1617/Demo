using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>Hell walkway: a cracked basalt crust glowing with magma over columnar basalt, rubble bound by molten
/// seams below, iron grate platforms with hanging chains and spikes, and a small cage, a spiked post and a shard of
/// Solomon's seal as props.</summary>
public sealed class HellTerrain : TerrainKit
{
    public override string Id => "hell";
    public override Lighting Light() => HellScene.TerrainLight();

    static int Crust(Scene sc, int seed, float heat)
    {
        var n = sc.N; var dark = R.Rgb("161312"); var light = R.Rgb("3e3836"); var hot = R.Rgb("ff4a10"); var core = R.Rgb("ffc060");
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float t = n.Fbm(p, .4f, 4, seed) * .5f + .5f;
            var c = n.Voronoi(p, .35f, seed + 1);
            float vein = R.Smooth(.03f, 0, c.F2 - c.F1) * R.Smooth(.4f, .6f, n.Fbm(p, .9f, 2, seed + 2) * .5f + .5f);
            s.Albedo = R.Mix(dark, light, t) * (1 - vein); s.Rough = .7f; s.Spec = .05f;
            s.Emit = R.Mix(hot, core, vein * vein) * vein * heat;
            s.N = Mats.Bump(q => n.Fbm(q, .15f, 4, seed + 3) * .015f, p, s.N, 1, .006f);
        });
    }

    public override void Top(Scene sc, Mats m)
    {
        var n = sc.N;
        int crust = Crust(sc, 901, 3);
        int column = m.Rock("121010", "2e2a28", .5f, .6f, .3f, 902);
        sc.Add(new Vector3(-1, -.95f, -.95f), new Vector3(13, .08f, .8f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start;
            a.Add(Sdf.Box(p - new Vector3(6, -.08f, 0), new Vector3(7, .08f, .62f)) + n.Fbm(p, .12f, 3, 903) * .015f, crust);
            // Columnar basalt: faceted vertical prisms with ragged tops.
            float cell = R.Mod(p.X, .26f) - .13f; int id = (int)MathF.Floor(p.X / .26f); id = ((id % 97) + 97) % 97;
            float top = -.18f - R.Hash01(id, 0, 0, 904) * .1f;
            float facet = MathF.Abs(cell) * .25f;
            float prism = MathF.Max(MathF.Max(-.55f - p.Z + facet, p.Y - top), MathF.Abs(cell) - .125f);
            a.Add(prism, column);
            a.Add(MathF.Max(-.5f - p.Z, p.Y + .2f), column);
            mt = a.M; return a.D;
        });
    }

    void Grate(Scene sc, Mats m, float x0, float x1, bool end)
    {
        int iron = m.Metal("2e2a28", .45f, .6f, "6a2a12", 911);
        int hot = m.Glow("d8401a", 1.3f, "2a1410", .3f, 912);
        float cx = (x0 + x1) * .5f, hw = (x1 - x0) * .5f;
        sc.Add(new Vector3(x0 - .3f, -1, -.8f), new Vector3(x1 + .3f, .05f, .8f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start; var q = p - new Vector3(cx, 0, 0); float ew = hw - (end ? .02f : -.2f);
            float frame = Sdf.Subtract(Sdf.Box(q - new Vector3(0, -.08f, 0), new Vector3(ew, .08f, .58f)), Sdf.Box(q - new Vector3(0, -.08f, 0), new Vector3(ew - .08f, .1f, .5f)));
            float bars = MathF.Max(Sdf.Box(new Vector3(R.Mod(p.X, .14f) - .07f, q.Y + .06f, q.Z), new Vector3(.018f, .05f, .55f)), MathF.Abs(q.X) - ew);
            a.Add(MathF.Min(frame, bars), iron);
            // Embers glowing through the grate bed.
            a.Add(Sdf.Box(q - new Vector3(0, -.14f, 0), new Vector3(ew - .1f, .02f, .48f)), hot);
            a.Add(Sdf.Box(q - new Vector3(0, -.2f, -.56f), new Vector3(ew, .05f, .03f)) - .01f, iron);
            // Spikes beneath and chains hanging from the rim.
            float sx = R.Mod(p.X, .3f) - .15f;
            if (MathF.Abs(q.X) < ew - .05f) a.Add(Sdf.ConeY(new Vector3(sx, q.Y + .38f, q.Z + .45f), .14f, .002f, .04f), iron);
            float chainX = R.Mod(p.X + .75f, 1.5f) - .75f;
            if (MathF.Abs(q.X) < ew - .1f) a.Add(Kit3D.Chain(new Vector3(chainX, q.Y, q.Z), new Vector3(0, -.22f, -.5f), new Vector3(0, -.85f, -.5f), 0, .08f), iron);
            if (end) a.Add(Sdf.ConeY(q - new Vector3(hw - .02f, .12f, -.5f), .14f, .04f, .002f), iron);
            mt = a.M; return a.D;
        });
    }

    public override void Slab(Scene sc, Mats m) => Grate(sc, m, -1, 7, false);
    public override void Cap(Scene sc, Mats m, float end) => Grate(sc, m, end - 1.9f, end, true);

    public override TileSample Body(TileNoise n, float x, float y)
    {
        // Basalt rubble bound by molten seams.
        var c = n.Voronoi(x, y, .3f, 921);
        float edge = c.F2 - c.F1, face = R.Smooth(.025f, .09f, edge);
        float tone = R.Hash01((int)(c.Id & 0xffff), 0, 0, 921), grain = n.N(x, y, .02f, 922) * .5f + .5f;
        var col = R.Mix(R.Rgb("100e0e"), R.Rgb("36302e"), R.Clamp01(tone * .6f + grain * .3f));
        float seam = R.Smooth(.025f, 0, edge) * R.Smooth(.5f, .72f, n.Fbm(x, y, 1.2f, 3, 923) * .5f + .5f);
        float h = edge < .025f ? -.05f : -(1 - face) * (1 - face) * .05f + n.Fbm(x, y, .12f, 4, 924) * .015f;
        return new TileSample { H = h, Albedo = col * (1 - seam), Rough = .7f, Emit = R.Mix(R.Rgb("e8300a"), R.Rgb("ff9a30"), seam * seam) * seam * 1.1f };
    }

    public override void Prop(int index, Scene sc, Mats m)
    {
        var n = sc.N; float cx = .75f;
        if (index == 0)
        {
            int iron = m.Metal("2e2a28", .45f, .6f, "6a2a12", 931);
            sc.Add(new Vector3(cx - .45f, -.02f, -.45f), new Vector3(cx + .45f, 1.2f, .45f), (Vector3 p, out int mt) =>
            {
                mt = iron; var q = p - new Vector3(cx, 0, 0);
                float frame = MathF.Min(Sdf.CylinderY(q - new Vector3(0, .04f, 0), .38f, .04f), Sdf.TorusY(q - new Vector3(0, .9f, 0), .36f, .025f));
                var r = Sdf.RepeatAngleY(q, 10);
                float bars = Sdf.Capsule(r, new Vector3(.36f, .05f, 0), new Vector3(.36f, .9f, 0), .018f);
                float dome = MathF.Max(MathF.Abs(Sdf.Sphere(q - new Vector3(0, .9f, 0), .36f)) - .02f, -(q.Y - .9f));
                dome = MathF.Max(dome, MathF.Abs(R.Mod(MathF.Atan2(q.Z, q.X) * 1.6f, 1) - .5f) - .08f);
                return MathF.Min(MathF.Min(frame, bars), MathF.Min(dome, Sdf.TorusZ(q - new Vector3(0, 1.32f, 0), .06f, .015f)));
            });
        }
        else if (index == 1)
        {
            int wood = m.Wood("1e1410", "3a2a20", .9f, true, 941);
            int iron = m.Metal("2e2a28", .45f, .6f, "6a2a12", 942);
            sc.Add(new Vector3(cx - .4f, -.02f, -.3f), new Vector3(cx + .4f, 1.35f, .3f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(cx, 0, 0); var a = Acc.Start;
                a.Add(Sdf.Box(q - new Vector3(0, .62f, 0), new Vector3(.08f, .62f, .08f)), wood);
                for (int k = 0; k < 5; k++)
                {
                    float y = .3f + k * .2f; var dir = new Vector3(k % 2 == 0 ? 1 : -1, .3f, -.3f);
                    a.Add(Sdf.RoundCone(q, new Vector3(0, y, 0), new Vector3(0, y, 0) + dir * .3f, .03f, .004f), iron);
                }
                a.Add(Kit3D.Chain(q, new Vector3(.09f, 1.1f, -.09f), new Vector3(.2f, .2f, -.12f), .1f, .07f), iron);
                mt = a.M; return a.D;
            });
        }
        else
        {
            int stone = m.Rock("141212", "34302e", .5f, .7f, .4f, 951);
            int seal = m.Glyphs(stone, "ff8a30", 6, p => p.Z < -.05f ? R.Smooth(.03f, .012f, Kit3D.Hexagram(p.X - .75f, p.Y - .7f, .95f)) : 0);
            sc.Add(new Vector3(cx - .6f, -.02f, -.3f), new Vector3(cx + .6f, 1.1f, .3f), (Vector3 p, out int mt) =>
            {
                mt = seal; var q = p - new Vector3(cx, 0, 0);
                // A wedge-shaped shard of the broken seal leaning in the ash.
                float disc = Sdf.CylinderZ(Sdf.RotZ(q - new Vector3(0, .7f, 0), .2f), .95f, .1f);
                float wedge = MathF.Max(MathF.Max(-(q.X * .6f + q.Y * .2f - .05f), q.X * .3f - q.Y * .9f + .1f), -q.Y);
                return MathF.Max(disc, wedge) + n.Fbm(p, .1f, 3, 952) * .02f;
            });
        }
    }
}
