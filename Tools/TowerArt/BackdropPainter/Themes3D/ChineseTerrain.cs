using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>Kunlun walkway: white jade coping carved with cloud scrolls over a red-lacquered band with gilt lines,
/// grey-green ashlar below, jade slabs riding auspicious ruyi clouds, and a stone lantern, a bonsai pine and an
/// incense burner as props.</summary>
public sealed class ChineseTerrain : TerrainKit
{
    public override string Id => "chinese";
    public override Lighting Light() => ChineseScene.TerrainLight();

    static readonly TerrainBake.Masonry Ashlar = new()
    {
        Course = .7f, MinLen = 1.3f, MaxLen = 2.4f, Light = "aab0a6", Dark = "767c74", Mortar = "868a82", Dirt = "444840",
        Joint = .01f, Bevel = .04f, Chips = .8f, Grime = .3f, Streaks = .3f, Rough = .8f, Stains = .4f, Stain = "5a6a4a", Seed = 81,
    };

    /// <summary>Raised cloud scrolls: paired spirals repeating along x on a vertical face.</summary>
    static float Scroll(float x, float y, float cell)
    {
        float u = R.Mod(x, cell) - cell * .5f; float best = 9;
        foreach (float s in new[] { -1f, 1f })
        {
            float cx = s * cell * .22f, dx = u - cx, r = MathF.Sqrt(dx * dx + y * y), a = MathF.Atan2(y, dx * s);
            float spiral = MathF.Abs(R.Mod(r - a / (MathF.PI * 2) * cell * .09f, cell * .09f) - cell * .045f);
            if (r < cell * .2f) best = MathF.Min(best, spiral);
        }
        return R.Smooth(cell * .02f, 0, best);
    }

    public override void Top(Scene sc, Mats m)
    {
        int jade = m.Marble("eeece4", "b8c4b8", .7f, .4f, 801);
        int lacquer = m.Paint("a8261c", .4f, .25f, 802);
        int gold = m.Metal("e6b44a", .25f, .1f, "6b5a2e", 803);
        int stone = m.Rock("6a706a", "a8aea4", .7f, .85f, .4f, 804);
        sc.Add(new Vector3(-1, -.95f, -.95f), new Vector3(13, .06f, .8f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start;
            a.Add(Sdf.Box(p - new Vector3(6, -.08f, 0), new Vector3(7, .08f, .62f)) - .012f, jade);
            float band = Sdf.Box(p - new Vector3(6, -.26f, -.5f), new Vector3(7, .1f, .14f)) - Scroll(p.X, p.Y + .26f, .5f) * .015f;
            a.Add(band, jade);
            a.Add(Sdf.Box(p - new Vector3(6, -.47f, -.44f), new Vector3(7, .11f, .14f)), lacquer);
            a.Add(MathF.Min(Sdf.Box(p - new Vector3(6, -.39f, -.58f), new Vector3(7, .008f, .01f)), Sdf.Box(p - new Vector3(6, -.55f, -.58f), new Vector3(7, .008f, .01f))) - .004f, gold);
            a.Add(Sdf.Box(p - new Vector3(6, -.66f, -.4f), new Vector3(7, .08f, .18f)) + sc.N.Fbm(p, .2f, 3, 805) * .01f, stone);
            mt = a.M; return a.D;
        });
    }

    void SlabShape(Scene sc, Mats m, float x0, float x1, bool end)
    {
        var n = sc.N;
        int jade = m.Marble("cfdccc", "8aa294", .7f, .35f, 811);
        int gold = m.Metal("e6b44a", .25f, .1f, "6b5a2e", 812);
        int cloud = sc.Material((Vector3 p, ref Surface s) =>
        {
            // Ruyi cloud: white body with blue-grey spiral grooves.
            float groove = Scroll(p.X, p.Y + .5f, .6f) + Scroll(p.X + .3f, p.Y + .62f, .45f) * .7f;
            s.Albedo = R.Mix(R.Rgb("fbfaf6"), R.Rgb("8ea0b8"), R.Clamp01(groove)); s.Rough = .6f; s.Trans = .3f; s.Emit = s.Albedo * .25f;
        });
        float cx = (x0 + x1) * .5f, hw = (x1 - x0) * .5f;
        var rnd = new Rand(813); var puffs = new List<(Vector3, float)>();
        for (float x = x0 - 1; x < x1 + 1; x += .5f) puffs.Add((new Vector3(x + rnd.R(-.1f, .1f), -.42f + rnd.R(-.05f, .05f), rnd.R(-.1f, .1f)), rnd.R(.22f, .3f)));
        var arr = puffs.Where(b => !end || b.Item1.X < x1 - .1f).ToArray();
        sc.Add(new Vector3(x0 - .4f, -1, -.8f), new Vector3(x1 + .4f, .05f, .8f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start; var q = p - new Vector3(cx, 0, 0);
            a.Add(Sdf.RoundBox(q - new Vector3(0, -.12f, 0), new Vector3(hw - (end ? .02f : -.2f), .12f, .58f), end ? .08f : .02f), jade);
            a.Add(Sdf.Box(q - new Vector3(0, -.2f, -.58f), new Vector3(hw - (end ? .08f : -.2f), .01f, .012f)), gold);
            float c = 1e9f; foreach (var (pc, r) in arr) { var d = p - pc; d.Y *= 1.5f; c = R.SMin(c, d.Length() / 1.5f - r, .1f); }
            a.Add(c + n.Fbm(p, .08f, 2, 814) * .01f, cloud);
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
            int stone = m.Rock("6a706a", "aab0a6", .4f, .85f, .3f, 821, "4e6a3a", .3f);
            int glow = m.Glow("ffb060", 4, "3a2410", .2f, 822);
            sc.Add(new Vector3(cx - .4f, -.02f, -.4f), new Vector3(cx + .4f, 1.3f, .4f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(cx, 0, 0); var a = Acc.Start;
                float d = MathF.Min(Sdf.Box(q - new Vector3(0, .06f, 0), new Vector3(.28f, .06f, .28f)), Sdf.CylinderY(q - new Vector3(0, .4f, 0), .08f, .3f));
                float box = Sdf.Subtract(Sdf.Box(q - new Vector3(0, .85f, 0), new Vector3(.2f, .16f, .2f)), Sdf.Box(q - new Vector3(0, .85f, 0), new Vector3(.12f, .1f, .3f)));
                float cap = MathF.Min(ChineseScene.Roof(q - new Vector3(0, 1.12f, 0), .32f, .1f, .1f, .05f, .3f), Sdf.Sphere(q - new Vector3(0, 1.22f, 0), .06f));
                a.Add(MathF.Min(MathF.Min(d, box), cap), stone);
                a.Add(Sdf.Box(q - new Vector3(0, .85f, 0), new Vector3(.1f, .09f, .1f)), glow);
                mt = a.M; return a.D;
            });
        }
        else if (index == 1)
        {
            int pot = m.Paint("2e5a7a", .3f, .1f, 831, .07f);
            int bark = m.Wood("3a2e26", "6a564a", .9f, true, 832);
            int needles = m.Foliage("1e3a24", "4e7040", .3f, 833);
            var pads = new[] { (new Vector3(cx - .25f, .62f, 0), .2f), (new Vector3(cx + .2f, .78f, .05f), .17f), (new Vector3(cx - .05f, .92f, -.05f), .14f) };
            sc.Add(new Vector3(cx - .55f, -.02f, -.4f), new Vector3(cx + .55f, 1.15f, .4f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(cx, 0, 0); var a = Acc.Start;
                a.Add(Sdf.RoundBox(q - new Vector3(0, .1f, 0), new Vector3(.36f, .1f, .24f), .03f), pot);
                float trunk = MathF.Min(Sdf.RoundCone(q, new Vector3(0, .18f, 0), new Vector3(-.15f, .5f, 0), .06f, .04f), Sdf.RoundCone(q, new Vector3(-.15f, .5f, 0), new Vector3(.15f, .75f, 0), .04f, .025f));
                a.Add(trunk, bark);
                float c = 1e9f; foreach (var (pc, r) in pads) { var d = p - pc; d.Y *= 2.2f; c = R.SMin(c, d.Length() / 2.2f - r, .05f); }
                a.Add(c + n.Fbm(p, .03f, 3, 834) * .015f, needles);
                mt = a.M; return a.D;
            });
        }
        else
        {
            int bronze = m.Metal("6a5a3a", .45f, .7f, "3a6a5a", 841);
            sc.Add(new Vector3(cx - .4f, -.02f, -.35f), new Vector3(cx + .4f, .7f, .35f), (Vector3 p, out int mt) =>
            {
                mt = bronze; var q = p - new Vector3(cx, 0, 0);
                float body = MathF.Max(Sdf.Ellipsoid(q - new Vector3(0, .38f, 0), new Vector3(.3f, .22f, .26f)), q.Y - .52f);
                float legs = Sdf.CylinderY(Sdf.RepeatAngleY(q, 3) - new Vector3(.2f, .1f, 0), .035f, .12f);
                float handles = Sdf.TorusX(new Vector3(MathF.Abs(q.X) - .22f, q.Y - .6f, q.Z), .07f, .018f);
                return MathF.Min(MathF.Min(body, legs), handles);
            });
        }
    }
}
