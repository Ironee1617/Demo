using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>Egypt at hot midday: a deep blue sky over the three pyramids shimmering on the desert, the Sphinx and
/// obelisks by the Nile with palms and felucca sails, and in front a hypostyle hall of painted papyrus columns,
/// jackal-headed Anubis on his plinths and papyrus reeds along the water.</summary>
public sealed class PapyrusScene : ThemePainter
{
    public override string Id => "papyrus";

    static readonly Vector3 SunDir = Vector3.Normalize(new Vector3(-.45f, .8f, -.4f));

    static Sky MakeSky(float period) => new(period, (-11, "f4dcae"), (-3, "f2d8a8"), (0, "e8dcc0"), (3, "a8c6e2"), (6, "5a90d4"), (10, "2a5cb4"))
    {
        SunX = 5.5f, SunY = 8.6f, SunRadius = .5f, HaloRadius = 5, SunColor = new Vector3(40, 36, 28), HaloColor = new Vector3(1.4f, 1.2f, .8f),
    };

    static Lighting Light(Sky sky, float distance, float haze) => new()
    {
        SunDir = SunDir, Sun = new Vector3(5f, 4.5f, 3.6f),
        SkyZenith = R.Rgb("4a7ac4") * .9f, SkyHorizon = R.Rgb("e6d4b0") * .8f, Bounce = R.Rgb("d8b07a") * .5f, Ambient = .85f,
        Haze = Bake.HazeFrom(sky, .05f), Distance = distance, HazeDensity = haze, Exposure = .82f, ShadowK = 16, Saturation = 1.05f,
    };

    public static Lighting TerrainLight()
    {
        var l = Light(MakeSky(12), 0, 0); l.HazeMax = 0; l.Exposure = .8f; l.ShadowReach = 3; l.AoScale = .5f;
        return l;
    }

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return SkyLayer();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    static readonly string[] GlyphPaint = { "2a5a9a", "a83a28", "c89a3a", "3a7a5a", "1e1a16", "2a5a9a", "a83a28", "c89a3a" };

    /// <summary>Paints hieroglyph columns onto a base material over the band where <paramref name="band"/> holds.</summary>
    public static int Painted(Scene sc, Mats m, int baseMat, float cell, Func<Vector3, (float u, float v, bool on)> project, int seed)
    {
        var mb = sc.GetMaterial(baseMat); var colors = GlyphPaint.Select(R.Rgb).ToArray();
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            mb(p, ref s);
            var (u, v, on) = project(p);
            if (!on) return;
            int g = Kit3D.Hieroglyph(u, v, cell, seed, out float edge);
            if (g == 0) return;
            float worn = sc.N.Fbm(p, .2f, 3, seed + 1) * .5f + .5f;
            s.Albedo = R.Mix(s.Albedo, colors[g - 1], R.Smooth(.75f, .4f, worn) * .9f);
            s.N = Mats.Bump(q => -R.Smooth(.02f, -.02f, edge) * .004f, p, s.N, 1, .003f);
        });
    }

    /// <summary>A papyrus-bundle column: lobed shaft, banded neck and an open umbel capital. Origin at its foot.</summary>
    public static float PapyrusColumn(Vector3 q, float h, float r)
    {
        float ang = MathF.Atan2(q.Z, q.X);
        float t = R.Clamp01(q.Y / h);
        float shaftR = r * (1 + .08f * MathF.Pow(MathF.Abs(MathF.Cos(ang * 4)), 2) - t * .12f);
        float shaftTop = h * .78f;
        float shaft = MathF.Max(MathF.Sqrt(q.X * q.X + q.Z * q.Z) - shaftR, MathF.Max(-q.Y, q.Y - shaftTop));
        float bands = Sdf.CylinderY(q - new Vector3(0, shaftTop - r * .3f, 0), r * .95f, r * .25f);
        float capital = Sdf.ConeY(q - new Vector3(0, shaftTop + (h - shaftTop) * .5f, 0), (h - shaftTop) * .5f, r * .9f, r * 1.7f) * .9f;
        float abacus = Sdf.Box(q - new Vector3(0, h + r * .12f, 0), new Vector3(r * 1.05f, r * .12f, r * 1.05f));
        float foot = Sdf.CylinderY(q - new Vector3(0, r * .12f, 0), r * 1.3f, r * .12f);
        return MathF.Min(MathF.Min(shaft, bands), MathF.Min(MathF.Min(capital, abacus), foot));
    }

    /// <summary>A seated jackal (Anubis) on a plinth, facing -x. Origin at the plinth foot.</summary>
    public static float Anubis(Vector3 q, float s, out bool gold)
    {
        q /= s; gold = false;
        float plinth = Sdf.Box(q - new Vector3(0, .3f, 0), new Vector3(.9f, .3f, .4f));
        float body = Sdf.Ellipsoid(q - new Vector3(.15f, .95f, 0), new Vector3(.55f, .38f, .28f));
        float chest = Sdf.Ellipsoid(q - new Vector3(-.3f, 1.2f, 0), new Vector3(.22f, .45f, .22f));
        float legs = Sdf.RoundBox(q - new Vector3(-.4f, .8f, 0), new Vector3(.08f, .2f, .18f), .05f);
        float head = Sdf.Ellipsoid(q - new Vector3(-.38f, 1.72f, 0), new Vector3(.17f, .15f, .14f));
        float snout = Sdf.RoundCone(q, new Vector3(-.45f, 1.7f, 0), new Vector3(-.75f, 1.62f, 0), .08f, .035f);
        float ears = MathF.Min(Sdf.RoundCone(q, new Vector3(-.32f, 1.82f, .08f), new Vector3(-.28f, 2.1f, .07f), .05f, .01f), Sdf.RoundCone(q, new Vector3(-.32f, 1.82f, -.08f), new Vector3(-.28f, 2.1f, -.07f), .05f, .01f));
        float tail = Sdf.Capsule(q, new Vector3(.65f, .75f, 0), new Vector3(.9f, .62f, .1f), .05f);
        float collar = Sdf.TorusX(q - new Vector3(-.3f, 1.5f, 0), .2f, .04f);
        float statue = R.SMin(R.SMin(body, chest, .12f), R.SMin(legs, MathF.Min(head, snout), .06f), .08f);
        statue = MathF.Min(MathF.Min(statue, ears), tail);
        if (collar < statue && collar < plinth) { gold = true; return collar * s; }
        return MathF.Min(statue, plinth) * s;
    }

    PaintedLayer SkyLayer()
    {
        var spec = new LayerSpec("sky", .95f, 128, 30, -11, 21, -97);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(2.5f);
        int sand = m.Sandstone("c89a5e", "eed2a0", .6f, 501);
        int stone = m.Sandstone("b48a58", "e2c08a", .25f, 502);
        sc.Add(new Vector3(0, -11, 24), new Vector3(30, -1.6f, 44), (Vector3 p, out int mt) =>
        {
            mt = sand; float dunes = -2.9f + (n.Fbm(new Vector3(p.X, 0, p.Z), 3.5f, 4, 503) * .5f + .5f) * .6f;
            return (p.Y - dunes) * .5f;
        });
        foreach (var (x, h, z) in new[] { (15f, 3.4f, 36f), (19.6f, 3.9f, 38f), (23.2f, 2.4f, 34f) })
            sc.Add(new Vector3(x - h * 1.4f, -3, z - h * 1.4f), new Vector3(x + h * 1.4f, -2.6f + h, z + h * 1.4f), (Vector3 p, out int mt) =>
            {
                mt = stone; var q = p - new Vector3(x, -2.7f, z);
                return Sdf.Pyramid(q, h, h * 1.57f) + MathF.Abs(R.Mod(q.Y, .09f) - .045f) * .02f;
            });
        var lt = Light(sky, 60, .014f); lt.Background = sky.At; lt.HazeMax = .7f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Far()
    {
        var spec = new LayerSpec("far", .85f, 128, 32, -11, 19, -96);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3);
        int sand = m.Sandstone("c89a5e", "eed2a0", .5f, 511);
        int stone = m.Sandstone("b08452", "dcb47e", .22f, 512);
        int gold = m.Metal("e6b44a", .25f, .1f, "6b5a2e", 513);
        int water = m.Water("1e4a5a", .9f, .15f, 514);
        int palmLeaf = m.Foliage("2a4a1e", "6a8a3a", .4f, 515);
        int palmBark = m.Wood("6a5034", "9a7a52", .9f, true, 516);
        int sail = m.Paint("efe6d2", .8f, .2f, 517);
        int hull = m.Wood("4a3220", "7a5634", .8f, false, 518);
        sc.Add(new Vector3(0, -11, 0), new Vector3(32, -3, 6), (Vector3 p, out int mt) => { mt = water; return p.Y + 3.3f; });
        sc.Add(new Vector3(0, -4, 5.5f), new Vector3(32, -1.2f, 14), (Vector3 p, out int mt) =>
        {
            mt = sand; float top = -2.6f + R.Smooth(6, 9, p.Z) * .5f + (n.Fbm(new Vector3(p.X, 0, p.Z), 2.5f, 4, 519) * .5f + .5f) * .4f;
            return MathF.Max(p.Y - top, 5.8f - p.Z) * .6f;
        });
        // The Sphinx: couchant lion body, outstretched paws and a nemes-headed face.
        sc.Add(new Vector3(18, -2.4f, 8), new Vector3(26.5f, 1.6f, 12), (Vector3 p, out int mt) =>
        {
            mt = stone; var q = p - new Vector3(22, -2.2f, 10);
            float body = Sdf.RoundBox(q - new Vector3(1.3f, .65f, 0), new Vector3(2.3f, .6f, .85f), .45f);
            float haunch = Sdf.Ellipsoid(q - new Vector3(3.1f, .85f, 0), new Vector3(.9f, .8f, .9f));
            float paws = Sdf.RoundBox(q - new Vector3(-2, .22f, 0), new Vector3(1.4f, .22f, .55f), .15f);
            float chest = Sdf.Ellipsoid(q - new Vector3(-.4f, 1.1f, 0), new Vector3(.8f, 1, .75f));
            float head = Sdf.RoundBox(q - new Vector3(-.75f, 2.35f, 0), new Vector3(.38f, .45f, .4f), .2f);
            float nemes = Sdf.Ellipsoid(q - new Vector3(-.6f, 2.1f, 0), new Vector3(.55f, .8f, .75f));
            float d = R.SMin(R.SMin(body, haunch, .3f), R.SMin(paws, chest, .3f), .3f);
            d = MathF.Min(d, R.SMin(head, nemes, .1f));
            return d + n.Fbm(p, .3f, 4, 520) * .06f;
        });
        foreach (float x in new[] { 9.2f, 12.4f })
            sc.Add(new Vector3(x - .5f, -2.6f, 9), new Vector3(x + .5f, 3.6f, 10.4f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(x, -2.3f, 9.7f); var a = Acc.Start;
                float t = R.Clamp01(q.Y / 5); float w = .3f - t * .1f;
                a.Add(MathF.Max(Sdf.Box(q - new Vector3(0, 2.5f, 0), new Vector3(w, 2.5f, w)), -(q.Y)), stone);
                a.Add(Sdf.Pyramid(q - new Vector3(0, 5, 0), .45f, .42f), gold);
                mt = a.M; return a.D;
            });
        foreach (var (x, z, h, lean) in new[] { (2.4f, 7, 2.6f, .25f), (3.6f, 8, 2.2f, -.2f), (6, 7.4f, 2.8f, .3f), (28.4f, 7.2f, 2.4f, -.25f), (30.2f, 8.2f, 2.9f, .2f), (15.6f, 7, 2.3f, .2f) })
        {
            int seed = (int)(x * 29);
            sc.Add(new Vector3(x - 1.6f, -2.6f, z - 1.4f), new Vector3(x + 1.6f + lean * h, -2.2f + h * 1.1f, z + 1.4f), (Vector3 p, out int mt) =>
            {
                float d = Kit3D.Palm(n, p - new Vector3(x, -2.3f, z), h, lean, seed, out bool frond);
                mt = frond ? palmLeaf : palmBark; return d;
            });
        }
        foreach (var (x, z) in new[] { (5f, 2.4f), (17f, 3.8f), (26f, 2.2f) })
            sc.Add(new Vector3(x - 1.4f, -3.6f, z - .6f), new Vector3(x + 1.4f, -.4f, z + .6f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(x, -3.3f, z); var a = Acc.Start;
                float boat = MathF.Max(Sdf.Ellipsoid(q, new Vector3(1.2f, .3f, .35f)), q.Y - .08f);
                a.Add(Sdf.Subtract(boat, Sdf.Ellipsoid(q - new Vector3(0, .1f, 0), new Vector3(1.1f, .28f, .3f))), hull);
                a.Add(Sdf.Capsule(q, new Vector3(.1f, 0, 0), new Vector3(.1f, 2.4f, 0), .025f), hull);
                // A lateen sail: a thin triangle slung from a long yard.
                float tri = MathF.Max(MathF.Max(-(q.X + .9f) * .8f + (q.Y - .3f) * .45f, (q.X - .15f)), MathF.Max(.3f - q.Y, (q.Y - .3f) - (q.X + 1) * 1.9f * .5f));
                a.Add(MathF.Max(tri, MathF.Abs(q.Z) - .01f), sail);
                mt = a.M; return a.D;
            });
        var lt = Light(sky, 26, .016f); lt.HazeMax = .5f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Mid()
    {
        var spec = new LayerSpec("mid", .65f, 192, 36, -9.5f, 21, -95);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3.5f);
        int sandstone = m.Sandstone("b08452", "e0bc86", .26f, 521);
        // Glyph columns wrap around each shaft; on the architrave they run along its front face.
        int painted = Painted(sc, m, sandstone, .34f, p =>
        {
            if (p.Y > 4.9f) return (p.X, p.Y, p.Y < 5.85f && p.Z < 5f);
            float lx = R.Mod(p.X, 3.2f) - 1.6f, lz = p.Z - (p.Z < 7.2f ? 5.6f : 8.8f);
            return (MathF.Atan2(lz, lx) * .62f, p.Y, p.Y > -1.4f && p.Y < 2.8f);
        }, 522);
        int paving = m.Rock("b08a5c", "e2c28e", .9f, .85f, .3f, 523);
        int basalt = m.Rock("16161a", "34343a", .5f, .35f, .1f, 524);
        int gold = m.Metal("e6b44a", .25f, .1f, "6b5a2e", 525);
        int reed = m.Foliage("3a5a22", "8aa04a", .4f, 526, "c8b46a", .2f);
        int water = m.Water("1a4248", .6f, .15f, 527);
        sc.Add(new Vector3(0, -6, 1.5f), new Vector3(36, -2.3f, 14), (Vector3 p, out int mt) =>
        {
            mt = paving; return MathF.Max(p.Y + 2.64f + R.Smooth(.02f, 0, MathF.Min(MathF.Abs(R.Mod(p.X, 1.4f) - .7f), MathF.Abs(R.Mod(p.Z, 1.4f) - .7f)) - .67f) * .01f, 2.2f - p.Z) * .9f;
        });
        // Hypostyle hall: two rows of painted papyrus columns under heavy architraves.
        sc.Add(new Vector3(4, -2.8f, 4.6f), new Vector3(24.4f, 7.6f, 10.2f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start;
            float cx = R.Mod(p.X, 3.2f) - 1.6f; bool inside = p.X > 5 && p.X < 23.4f;
            if (inside)
            {
                float col = MathF.Min(PapyrusColumn(new Vector3(cx, p.Y + 2.64f, p.Z - 5.6f), 7.6f, .62f), PapyrusColumn(new Vector3(cx, p.Y + 2.64f, p.Z - 8.8f), 7.6f, .62f));
                a.Add(col, painted);
            }
            float beam = Sdf.Box(p - new Vector3(14.2f, 5.4f, 7.2f), new Vector3(9.4f, .45f, 2.4f));
            a.Add(beam, painted);
            // The cavetto cornice of the roof edge.
            float cornice = Sdf.Box(p - new Vector3(14.2f, 6.1f, 7.2f), new Vector3(9.7f, .28f, 2.7f)) - .02f;
            a.Add(cornice, sandstone);
            mt = a.M; return a.D;
        });
        foreach (var (x, flip) in new[] { (2.4f, 1f), (26.6f, -1f) })
            sc.Add(new Vector3(x - 1.4f, -2.8f, 3.4f), new Vector3(x + 1.4f, 1.2f, 5.2f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(x, -2.64f, 4.3f); q.X *= flip;
                float d = Anubis(q, 1.25f, out bool isGold); mt = isGold ? gold : basalt; return d;
            });
        // Papyrus reeds by a pool on the right.
        sc.Add(new Vector3(28.6f, -3, 2.6f), new Vector3(35.4f, -2.6f, 6.4f), (Vector3 p, out int mt) => { mt = water; return p.Y + 2.8f; });
        var rnd = new Rand(528); var stems = new List<(Vector3, float)>();
        for (int k = 0; k < 26; k++) stems.Add((new Vector3(rnd.R(29, 35), -2.8f, rnd.R(3, 6.2f)), rnd.R(1.2f, 2.4f)));
        sc.Add(new Vector3(28.4f, -2.9f, 2.6f), new Vector3(35.6f, .2f, 6.6f), (Vector3 p, out int mt) =>
        {
            mt = reed; float d = 1e9f;
            foreach (var (b, h) in stems)
            {
                var tip = b + new Vector3(MathF.Sin(b.X) * .2f, h, 0);
                d = MathF.Min(d, Sdf.Capsule(p, b, tip, .02f));
                var hq = p - tip; float tuft = Sdf.ConeY(hq - new Vector3(0, .12f, 0), .12f, .02f, .22f);
                d = MathF.Min(d, tuft + n.Fbm(p, .03f, 2, 529) * .02f);
            }
            return d;
        });
        var lt = Light(sky, 6, .012f); lt.HazeMax = .22f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Near()
    {
        // A lintel with the winged sun disc reaches into the top of the view when the camera climbs.
        var spec = new LayerSpec("near", .4f, 192, 40, 8.6f, 5, -94);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(4);
        int sandstone = m.Sandstone("a87a4a", "d8b480", .24f, 531);
        int gold = m.Metal("e6b44a", .25f, .1f, "6b5a2e", 532);
        int blue = m.Paint("2a5a9a", .6f, .3f, 533);
        foreach (var (x, w) in new[] { (10f, 6f), (30f, 5f) })
            sc.Add(new Vector3(x - w - .5f, 8.6f, 1), new Vector3(x + w + .5f, 13.6f, 3), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(x, 12.3f, 2); var a = Acc.Start;
                a.Add(Sdf.Box(q, new Vector3(w, .6f, .5f)), sandstone);
                a.Add(Sdf.Box(q - new Vector3(0, -.7f, -.1f), new Vector3(w + .2f, .12f, .55f)) - .02f, sandstone);
                // Winged sun disc: a gold disc between two feathered blue wings.
                float disc = Sdf.CylinderZ(q - new Vector3(0, 0, -.52f), .28f, .04f);
                a.Add(disc, gold);
                float wingY = q.Y + MathF.Abs(q.X) * .12f;
                float wing = MathF.Max(Sdf.Box(new Vector3(q.X, wingY, q.Z + .52f), new Vector3(2.4f, .16f, .03f)), .3f - MathF.Abs(q.X));
                wing = MathF.Max(wing, -Sdf.Box(new Vector3(R.Mod(q.X, .18f) - .09f, wingY + .1f, q.Z + .55f), new Vector3(.01f, .08f, .05f)));
                a.Add(wing, blue);
                mt = a.M; return a.D;
            });
        var lt = Light(sky, 2, .01f); lt.HazeMax = .15f;
        return Bake.Layer(spec, sc, lt, cam);
    }
}
