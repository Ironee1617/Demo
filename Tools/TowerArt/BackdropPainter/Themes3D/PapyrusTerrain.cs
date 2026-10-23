using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>Papyrus walkway: a limestone coping with a cavetto cornice over a painted hieroglyph frieze,
/// sandstone ashlar below, slabs carried on papyrus-bundle brackets, and a canopic jar, a papyrus clump and an
/// ankh stele as props.</summary>
public sealed class PapyrusTerrain : TerrainKit
{
    public override string Id => "papyrus";
    public override Lighting Light() => PapyrusScene.TerrainLight();

    static readonly TerrainBake.Masonry Ashlar = new()
    {
        Course = .75f, MinLen = 1.4f, MaxLen = 2.6f, Light = "e2c896", Dark = "b89464", Mortar = "c8aa7a", Dirt = "7a5e3e",
        Joint = .01f, Bevel = .035f, Chips = .9f, Grime = .28f, Streaks = .3f, Rough = .85f, Stains = .3f, Stain = "9a7a52", Seed = 71,
    };

    public override void Top(Scene sc, Mats m)
    {
        int lime = m.Sandstone("c8a46e", "eed6a4", .25f, 701);
        int frieze = PapyrusScene.Painted(sc, m, m.Sandstone("d8b880", "f2dcaa", .3f, 702), .26f, p => (p.X, p.Y + .62f, p.Z < -.5f && p.Y < -.3f && p.Y > -.64f), 703);
        int blue = m.Paint("2a5a9a", .6f, .3f, 704);
        sc.Add(new Vector3(-1, -.95f, -.95f), new Vector3(13, .08f, .8f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start;
            a.Add(Sdf.Box(p - new Vector3(6, -.06f, 0), new Vector3(7, .06f, .6f)) - .006f, lime);
            // Cavetto: a hollow curve flaring forward under the coping, painted with vertical leaf bands.
            float cav = MathF.Max(Sdf.Box(p - new Vector3(6, -.2f, -.5f), new Vector3(7, .09f, .2f)), -(Sdf.CylinderX(p - new Vector3(6, -.3f, -.74f), .16f, 7.5f)));
            bool leaf = R.Mod(p.X, .16f) < .07f;
            a.Add(cav, leaf ? blue : lime);
            a.Add(Sdf.Box(p - new Vector3(6, -.3f, -.56f), new Vector3(7, .012f, .03f)) - .01f, lime);
            a.Add(Sdf.Box(p - new Vector3(6, -.47f, -.38f), new Vector3(7, .17f, .18f)), frieze);
            a.Add(Sdf.Box(p - new Vector3(6, -.68f, -.58f), new Vector3(7, .03f, .03f)) - .008f, lime);
            mt = a.M; return a.D;
        });
    }

    void SlabShape(Scene sc, Mats m, float x0, float x1, bool end)
    {
        int lime = m.Sandstone("c8a46e", "eed6a4", .25f, 711);
        int green = m.Paint("3a7a5a", .6f, .3f, 712);
        int ochre = m.Paint("c89a3a", .6f, .3f, 713);
        float cx = (x0 + x1) * .5f, hw = (x1 - x0) * .5f;
        sc.Add(new Vector3(x0 - .3f, -1, -.8f), new Vector3(x1 + .3f, .05f, .8f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start; var q = p - new Vector3(cx, 0, 0);
            a.Add(Sdf.Box(q - new Vector3(0, -.14f, 0), new Vector3(hw - (end ? .02f : -.1f), .14f, .58f)) - .008f, lime);
            a.Add(Sdf.Box(q - new Vector3(0, -.31f, -.06f), new Vector3(hw - (end ? .06f : -.1f), .03f, .5f)), ochre);
            // Papyrus-bundle brackets every 1.5 units: lobed stalks flaring into an umbel under the slab.
            float bx = R.Mod(p.X + .75f, 1.5f) - .75f;
            if (!end || p.X < x1 - .2f)
            {
                var bq = new Vector3(bx, p.Y + .95f, p.Z + .1f);
                float br = PapyrusScene.PapyrusColumn(bq, .62f, .09f);
                a.Add(br, bq.Y > .45f ? green : lime);
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
            int alabaster = m.Marble("efe4cc", "c8b088", .9f, .4f, 721);
            int paint = m.Paint("2a5a9a", .5f, .2f, 722);
            sc.Add(new Vector3(cx - .3f, -.02f, -.3f), new Vector3(cx + .3f, 1.05f, .3f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(cx, 0, 0); var a = Acc.Start;
                float r = q.Y < .6f ? .14f + MathF.Sin(q.Y / .6f * MathF.PI * .8f + .3f) * .07f : .12f;
                float jar = MathF.Max((MathF.Sqrt(q.X * q.X + q.Z * q.Z) - r) * .85f, MathF.Max(-q.Y, q.Y - .66f));
                a.Add(jar, alabaster);
                // A jackal-headed stopper.
                a.Add(MathF.Min(Sdf.Ellipsoid(q - new Vector3(0, .76f, 0), new Vector3(.12f, .1f, .11f)), Sdf.RoundCone(q, new Vector3(-.04f, .76f, 0), new Vector3(-.2f, .72f, 0), .05f, .02f)), paint);
                a.Add(MathF.Min(Sdf.RoundCone(q, new Vector3(0, .82f, .05f), new Vector3(.02f, .98f, .05f), .03f, .008f), Sdf.RoundCone(q, new Vector3(0, .82f, -.05f), new Vector3(.02f, .98f, -.05f), .03f, .008f)), paint);
                mt = a.M; return a.D;
            });
        }
        else if (index == 1)
        {
            int reed = m.Foliage("3a5a22", "8aa04a", .4f, 731, "c8b46a", .25f);
            var rnd = new Rand(732); var stems = new List<(Vector3, Vector3)>();
            for (int k = 0; k < 9; k++) { var b = new Vector3(cx + rnd.R(-.18f, .18f), 0, rnd.R(-.15f, .15f)); stems.Add((b, b + new Vector3(rnd.R(-.25f, .25f), rnd.R(.8f, 1.25f), 0))); }
            sc.Add(new Vector3(cx - .7f, -.02f, -.5f), new Vector3(cx + .7f, 1.45f, .5f), (Vector3 p, out int mt) =>
            {
                mt = reed; float d = 1e9f;
                foreach (var (b, t) in stems)
                {
                    d = MathF.Min(d, Sdf.Capsule(p, b, t, .012f));
                    d = MathF.Min(d, Sdf.ConeY(p - t - new Vector3(0, .07f, 0), .07f, .015f, .14f) + n.Fbm(p, .02f, 2, 733) * .015f);
                }
                return d;
            });
        }
        else
        {
            int stone = m.Sandstone("b08452", "e0bc86", .2f, 741);
            int gold = m.Metal("e6b44a", .25f, .1f, "6b5a2e", 742);
            sc.Add(new Vector3(cx - .4f, -.02f, -.25f), new Vector3(cx + .4f, 1.2f, .25f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(cx, 0, 0); var a = Acc.Start;
                // A round-topped stele with a raised gold ankh.
                float stele = MathF.Min(Sdf.Box(q - new Vector3(0, .42f, 0), new Vector3(.3f, .42f, .09f)), Sdf.CylinderZ(q - new Vector3(0, .84f, 0), .3f, .09f));
                a.Add(stele, stone);
                var g = q - new Vector3(0, .62f, -.1f);
                float loop = MathF.Max(Sdf.TorusZ(g - new Vector3(0, .2f, 0), .08f, .025f), -.0f);
                float stem = Sdf.Box(g - new Vector3(0, -.06f, 0), new Vector3(.025f, .2f, .02f));
                float bar = Sdf.Box(g - new Vector3(0, .08f, 0), new Vector3(.12f, .025f, .02f));
                a.Add(MathF.Min(loop, MathF.Min(stem, bar)), gold);
                mt = a.M; return a.D;
            });
        }
    }
}
