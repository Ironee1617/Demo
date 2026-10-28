using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>A Vedic river city at saffron sunset: distant temple spires over the Ganges, ghats descending to
/// the water where diya lamps float, curvilinear shikhara towers, and in front a red sandstone temple, a pair of
/// stone elephants, a fire altar and a banyan with hanging aerial roots, all garlanded in marigold.</summary>
public sealed class VedaScene : ThemePainter
{
    public override string Id => "veda";

    static readonly Vector3 SunDir = Vector3.Normalize(new Vector3(.75f, .3f, -.3f));

    static Sky MakeSky(float period) => new(period, (-11, "f2a058"), (-3, "f6b05a"), (0, "f48a48"), (2.5f, "d0606a"), (5, "7a4a84"), (10, "2e2456"))
    {
        SunX = 17, SunY = .6f, SunRadius = .7f, HaloRadius = 7, SunColor = new Vector3(40, 22, 8), HaloColor = new Vector3(1.8f, .8f, .25f),
        CirrusAmount = .7f, CirrusLow = 1, CirrusHigh = 8, CirrusColor = new Vector3(1.4f, .7f, .55f),
    };

    static Lighting Light(Sky sky, float distance, float haze) => new()
    {
        SunDir = SunDir, Sun = new Vector3(4.6f, 2.7f, 1.2f),
        SkyZenith = R.Rgb("6a4a8a") * .8f, SkyHorizon = R.Rgb("e88a5a") * .7f, Bounce = R.Rgb("a8603a") * .45f, Ambient = .85f,
        Haze = Bake.HazeFrom(sky), Distance = distance, HazeDensity = haze, Exposure = .95f, ShadowK = 10, Saturation = 1.08f,
    };

    public static Lighting TerrainLight()
    {
        var l = Light(MakeSky(12), 0, 0); l.HazeMax = 0; l.Exposure = .9f; l.ShadowReach = 3; l.AoScale = .5f;
        l.Sun = new Vector3(4f, 2.9f, 1.8f);
        return l;
    }

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return SkyLayer();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    /// <summary>A Nagara shikhara: beehive profile over a rounded-square plan, vertical ribs, tier grooves, an
    /// amalaka disc and a finial. Origin at the base centre.</summary>
    public static float Shikhara(Vector3 q, float h, float r0)
    {
        float t = R.Clamp01(q.Y / h);
        float r = r0 * MathF.Pow(1 - MathF.Pow(t, 1.6f), .55f) + r0 * .08f;
        float ang = MathF.Atan2(q.Z, q.X);
        float square = MathF.Pow(MathF.Pow(MathF.Abs(q.X), 4) + MathF.Pow(MathF.Abs(q.Z), 4), .25f);
        float round = MathF.Sqrt(q.X * q.X + q.Z * q.Z);
        float rq = R.Mix(round, square, .55f);
        r *= 1 + .05f * MathF.Cos(ang * 12) - .03f * MathF.Pow(MathF.Abs(MathF.Sin(t * MathF.PI * 9)), 6);
        float tower = MathF.Max((rq - r) * .7f, MathF.Max(-q.Y, q.Y - h));
        float amalaka = Sdf.Ellipsoid(q - new Vector3(0, h + r0 * .1f, 0), new Vector3(r0 * .32f, r0 * .12f, r0 * .32f)) + MathF.Cos(ang * 24) * r0 * .008f;
        float finial = MathF.Min(Sdf.Sphere(q - new Vector3(0, h + r0 * .3f, 0), r0 * .09f), Sdf.CylinderY(q - new Vector3(0, h + r0 * .45f, 0), r0 * .025f, r0 * .14f));
        return MathF.Min(tower, MathF.Min(amalaka, finial));
    }

    /// <summary>A stone elephant standing side-on, facing -x. Origin at its feet centre.</summary>
    static float Elephant(Noise n, Vector3 q, float s)
    {
        q /= s;
        float body = Sdf.Ellipsoid(q - new Vector3(0, 1.45f, 0), new Vector3(1.05f, .75f, .62f));
        float legs = 1e9f;
        foreach (var (lx, lz) in new[] { (-.62f, -.32f), (-.6f, .32f), (.6f, -.3f), (.62f, .3f) }) legs = MathF.Min(legs, Sdf.RoundCone(q, new Vector3(lx, 0, lz), new Vector3(lx, 1.1f, lz), .24f, .27f));
        float head = Sdf.Ellipsoid(q - new Vector3(-1.08f, 1.75f, 0), new Vector3(.5f, .55f, .45f));
        float ear = Sdf.Ellipsoid(q - new Vector3(-.82f, 1.7f, 0), new Vector3(.12f, .55f, .72f));
        float trunk = MathF.Min(Sdf.RoundCone(q, new Vector3(-1.45f, 1.6f, 0), new Vector3(-1.62f, .9f, 0), .18f, .12f), Sdf.RoundCone(q, new Vector3(-1.62f, .9f, 0), new Vector3(-1.5f, .25f, 0), .12f, .08f));
        float tusk = Sdf.RoundCone(q, new Vector3(-1.35f, 1.4f, .2f), new Vector3(-1.7f, 1.2f, .25f), .05f, .02f);
        float tail = Sdf.Capsule(q, new Vector3(1.02f, 1.6f, 0), new Vector3(1.12f, .9f, 0), .04f);
        float d = R.SMin(R.SMin(body, legs, .15f), R.SMin(head, MathF.Min(trunk, ear), .12f), .2f);
        d = MathF.Min(MathF.Min(d, tusk), tail);
        return (d + n.Fbm(q, .3f, 3, 401) * .02f) * s;
    }

    static int Marigold(Scene sc, int seed)
    {
        var n = sc.N;
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float t = n.N(p, .03f, seed) * .5f + .5f;
            s.Albedo = R.Mix(R.Rgb("e85a10"), R.Rgb("ffb020"), t); s.Rough = .7f; s.Trans = .3f;
            s.N = Mats.Bump(q => n.Fbm(q, .02f, 2, seed + 1) * .006f, p, s.N, 1, .003f);
        });
    }

    /// <summary>A sagging garland between two points: a chain of flower balls along a catenary.</summary>
    public static float Garland(Vector3 p, Vector3 a, Vector3 b, float sag, float ball)
    {
        float best = 1e9f; int count = Math.Max(4, (int)((b - a).Length() / (ball * 1.7f)));
        // Quick reject from the bounding region.
        var mid = (a + b) * .5f - new Vector3(0, sag * .5f, 0);
        if ((p - mid).Length() > (b - a).Length() * .6f + sag + ball * 2) return (p - mid).Length() - (b - a).Length() * .6f - sag;
        for (int k = 0; k <= count; k++)
        {
            float t = k / (float)count; var c = Vector3.Lerp(a, b, t) - new Vector3(0, sag * 4 * t * (1 - t), 0);
            best = MathF.Min(best, (p - c).Length() - ball);
        }
        return best;
    }

    PaintedLayer SkyLayer()
    {
        var spec = new LayerSpec("sky", .95f, 128, 30, -11, 21, -97);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(2.5f);
        int stone = m.Rock("6a3a2a", "a8644a", 1, .9f, .3f, 402);
        int water = m.Water("4a2a2a", 1.2f, .12f, 403);
        sc.Add(new Vector3(0, -11, 20), new Vector3(30, -2.4f, 40), (Vector3 p, out int mt) => { mt = water; return p.Y + 2.9f; });
        // A far shore of temple spires against the sun.
        var rnd = new Rand(404); var spires = new List<(float x, float h, float r)>();
        for (float x = .6f; x < 30; x += rnd.R(1.6f, 3.2f)) spires.Add((x, rnd.R(1.2f, 3.4f), rnd.R(.35f, .6f)));
        sc.Add(new Vector3(0, -3.2f, 30), new Vector3(30, 1.8f, 34), (Vector3 p, out int mt) =>
        {
            mt = stone; float d = p.Y + 2.4f - n.Fbm(new Vector3(p.X, 0, 0), 1.5f, 3, 405) * .2f;
            d = MathF.Max(d, MathF.Abs(p.Z - 32) - 1.5f);
            foreach (var (x, h, r) in spires) { float dx = R.Wrap(p.X, x, 30); if (MathF.Abs(dx) < r * 1.5f) d = MathF.Min(d, Shikhara(new Vector3(dx, p.Y + 2.5f, p.Z - 32), h, r)); }
            return d;
        });
        var lt = Light(sky, 70, .012f); lt.Background = sky.At; lt.HazeMax = .75f; lt.BloomStrength = .4f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Far()
    {
        var spec = new LayerSpec("far", .85f, 128, 32, -11, 19, -96);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3);
        int sandstone = m.Sandstone("8a4a32", "c88a62", .3f, 411);
        int plaster = m.Paint("e8c8a0", .7f, .3f, 412);
        int water = m.Water("3a2028", .9f, .15f, 413);
        int lamp = m.Glow("ffb040", 14, "3a1a0a", .3f, 414);
        sc.Add(new Vector3(0, -11, 0), new Vector3(32, -3, 7), (Vector3 p, out int mt) => { mt = water; return p.Y + 3.4f; });
        // Ghats: broad steps climbing from the river to the temple terraces.
        sc.Add(new Vector3(0, -4, 6), new Vector3(32, -.6f, 14), (Vector3 p, out int mt) =>
        {
            mt = sandstone; float z = p.Z - 7;
            float stepTop = -3.4f + MathF.Floor(MathF.Max(0, z) / .32f + 1) * .16f;
            float steps = MathF.Max(p.Y - MathF.Min(stepTop, -1.2f), -z);
            return MathF.Max(steps, p.Z - 13.5f) * .8f;
        });
        var rnd = new Rand(415); var temples = new List<(float x, float h, float r)>();
        foreach (float x in new[] { 3f, 7.6f, 13.4f, 18.2f, 24f, 28.6f }) temples.Add((x + rnd.R(-.4f, .4f), rnd.R(2.2f, 4.2f), rnd.R(.6f, .9f)));
        foreach (var (x, h, r) in temples)
        {
            sc.Add(new Vector3(x - r * 2.2f, -1.4f, 9.6f), new Vector3(x + r * 2.2f, -1.2f + h + r * .8f, 13), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(x, -1.2f, 11.2f); var a = Acc.Start;
                a.Add(Sdf.Box(q - new Vector3(0, .25f, 0), new Vector3(r * 1.6f, .25f, r * 1.3f)), sandstone);
                a.Add(Shikhara(q - new Vector3(0, .5f, 0), h, r), sandstone);
                // A domed mandapa porch in front.
                a.Add(MathF.Min(Sdf.Box(q - new Vector3(0, .8f, -r * 1.4f), new Vector3(r * .8f, .3f, r * .5f)), Sdf.Sphere(q - new Vector3(0, 1.1f, -r * 1.4f), r * .55f)), plaster);
                mt = a.M; return a.D;
            });
        }
        // Floating diya lamps scattered over the water.
        var lamps = new List<Vector3>(); for (int k = 0; k < 60; k++) lamps.Add(new Vector3(rnd.R(0, 32), -3.36f, rnd.R(.5f, 6.5f)));
        sc.Add(new Vector3(0, -3.5f, 0), new Vector3(32, -3.2f, 7), (Vector3 p, out int mt) =>
        {
            mt = lamp; float d = 1e9f;
            foreach (var l in lamps) { var q = new Vector3(R.Wrap(p.X, l.X, 32), p.Y - l.Y, p.Z - l.Z); d = MathF.Min(d, Sdf.Ellipsoid(q, new Vector3(.08f, .05f, .08f))); }
            return d;
        });
        var lt = Light(sky, 26, .016f); lt.HazeMax = .5f; lt.BloomStrength = .6f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Mid()
    {
        var spec = new LayerSpec("mid", .65f, 192, 36, -9.5f, 21, -95);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3.5f);
        int sandstone = m.Sandstone("8a4630", "cc8e64", .28f, 421);
        int carved = m.Rock("7a4632", "c0805c", .6f, .85f, .2f, 422);
        int pave = m.Rock("8a5a40", "c49270", .7f, .85f, .3f, 423);
        int fire = m.Glow("ff8a28", 11, "2a1008", .4f, 424);
        int bark = m.Rock("4a3a2e", "7a6450", .5f, .95f, .2f, 425);
        int leaves = m.Foliage("2a4420", "6a8a3a", .4f, 426);
        int marigold = Marigold(sc, 427);
        int brass = m.Metal("d8a048", .3f, .25f, "5a4a2a", 428);
        // Temple court paving behind the walkway.
        sc.Add(new Vector3(0, -6, 1.5f), new Vector3(36, -2.3f, 14), (Vector3 p, out int mt) =>
        {
            mt = pave; return MathF.Max(p.Y + 2.64f + R.Smooth(.02f, 0, MathF.Min(MathF.Abs(R.Mod(p.X, 1.2f) - .6f), MathF.Abs(R.Mod(p.Z, 1.2f) - .6f)) - .57f) * .01f, 2.2f - p.Z) * .9f;
        });
        // The great shikhara on its plinth with a pillared porch.
        sc.Add(new Vector3(18.4f, -2.8f, 5), new Vector3(29.6f, 11.6f, 14), (Vector3 p, out int mt) =>
        {
            var q = p - new Vector3(24, -2.62f, 9.6f); var a = Acc.Start;
            a.Add(Kit3D.Steps(q, 4.6f, 3.6f, 1.1f, 4, .3f), sandstone);
            a.Add(Shikhara(q - new Vector3(0, 1.1f, .8f), 9.2f, 3f), carved);
            float porch = Sdf.Box(q - new Vector3(0, 2.9f, -2.6f), new Vector3(2.4f, .3f, 1.1f));
            float pillars = Sdf.CylinderY(new Vector3(Sdf.RepeatLimited(q.X, 1.1f, 2), q.Y - 1.9f, q.Z + 3.4f), .16f, .8f);
            a.Add(MathF.Min(porch, pillars), sandstone);
            a.Add(Sdf.Sphere(q - new Vector3(0, 3.6f, -2.6f), 1.1f), sandstone);
            a.Add(Garland(q, new Vector3(-2.4f, 2.62f, -3.72f), new Vector3(2.4f, 2.62f, -3.72f), .5f, .07f), marigold);
            mt = a.M; return a.D;
        });
        // Stone elephants guarding the steps.
        foreach (var (x, flip) in new[] { (16.4f, 1f), (31.6f, -1f) })
            sc.Add(new Vector3(x - 1.9f, -2.8f, 3.6f), new Vector3(x + 1.9f, .2f, 5.6f), (Vector3 p, out int mt) =>
            {
                mt = carved; var q = p - new Vector3(x, -2.62f, 4.6f); q.X *= flip;
                return MathF.Min(Elephant(n, q - new Vector3(0, .5f, 0), 1.1f), Sdf.Box(q - new Vector3(0, .25f, 0), new Vector3(1.8f, .25f, .8f)));
            });
        // A fire altar: stepped square pit with flames.
        sc.Add(new Vector3(9.2f, -2.8f, 3.8f), new Vector3(12.8f, -.4f, 6.2f), (Vector3 p, out int mt) =>
        {
            var q = p - new Vector3(11, -2.62f, 5); var a = Acc.Start;
            float pit = Sdf.Subtract(Kit3D.Steps(q, 1.3f, 1, .5f, 3, .15f), Sdf.Box(q - new Vector3(0, .5f, 0), new Vector3(.55f, .3f, .4f)));
            a.Add(pit, sandstone);
            mt = a.M; return a.D;
        });
        Kit3D.Fire(sc, new Vector3(11, -2.3f, 5), .5f, 1.5f, 429);
        // Banyan with aerial roots falling from its limbs.
        var rnd = new Rand(430); var lobes = Kit3D.Crown(rnd, new Vector3(4.6f, 4.4f, 7.4f), 9, 2.6f, 18, .35f);
        var roots = new List<Vector3>(); for (int k = 0; k < 34; k++) roots.Add(new Vector3(4.6f + rnd.R(-4.2f, 4.2f), 0, 7.4f + rnd.R(-1.2f, .4f)));
        var (bmin, bmax) = Kit3D.LobeBounds(lobes, .6f);
        sc.Add(Vector3.Min(bmin, new Vector3(-.5f, -2.8f, 4.5f)), Vector3.Max(bmax, new Vector3(9.8f, 6, 10)), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start; var q = p - new Vector3(4.6f, -2.62f, 7.4f);
            float trunk = Kit3D.Trunk(q, 4.4f, .9f, new[] { (new Vector3(0, 3.6f, 0), new Vector3(-3.8f, 6.4f, 0), .35f), (new Vector3(0, 3.8f, 0), new Vector3(4, 6.6f, .2f), .32f) });
            trunk += n.Fbm(p * new Vector3(1, .3f, 1), .3f, 3, 431) * .1f;
            float hang = 1e9f;
            if (p.Y < 4.5f) foreach (var r in roots) { float dx = p.X - r.X, dz = p.Z - r.Z; hang = MathF.Min(hang, MathF.Sqrt(dx * dx + dz * dz) - .045f - MathF.Max(0, 1.5f - p.Y) * .015f); }
            hang = MathF.Max(hang, p.Y - 4.6f);
            a.Add(R.SMin(trunk, hang, .1f), bark);
            a.Add(Kit3D.Canopy(n, p, lobes, .4f, 432), leaves);
            mt = a.M; return a.D;
        });
        // Brass lamp stands along the court.
        foreach (float x in new[] { 14.4f, 33.6f, .8f })
            sc.Add(new Vector3(x - .3f, -2.8f, 3.2f), new Vector3(x + .3f, -.2f, 3.8f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(x, -2.62f, 3.5f); var a = Acc.Start;
                float stand = MathF.Min(Sdf.CylinderY(q - new Vector3(0, .9f, 0), .04f, .9f), Sdf.CylinderY(q - new Vector3(0, .05f, 0), .2f, .05f));
                for (int k = 0; k < 3; k++) stand = MathF.Min(stand, Sdf.CylinderY(q - new Vector3(0, .7f + k * .5f, 0), .18f - k * .04f, .025f));
                a.Add(stand, brass);
                var fq = q - new Vector3(0, 1.85f, 0); a.Add(Sdf.Ellipsoid(fq - new Vector3(0, .06f, 0), new Vector3(.05f, .11f, .05f)), fire);
                mt = a.M; return a.D;
            });
        var lt = Light(sky, 6, .012f); lt.HazeMax = .25f; lt.BloomStrength = .55f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Near()
    {
        // Carved lintels hung with marigold garlands and bells reach into the top of the view when the camera climbs.
        var spec = new LayerSpec("near", .4f, 192, 40, 8.6f, 5, -94);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(4);
        int stone = m.Sandstone("7a3c28", "b47250", .25f, 441);
        int marigold = Marigold(sc, 442);
        int brass = m.Metal("d8a048", .3f, .3f, "5a4a2a", 443);
        foreach (var (x, w) in new[] { (8f, 5f), (28f, 6f) })
        {
            sc.Add(new Vector3(x - w - .5f, 8.6f, 1), new Vector3(x + w + .5f, 13.6f, 3), (Vector3 p, out int mt) =>
            {
                var a = Acc.Start;
                float lintel = Sdf.Box(p - new Vector3(x, 12.6f, 2), new Vector3(w, .45f, .5f)) + MathF.Sin(p.X * 9) * MathF.Sin(p.Y * 9) * .01f;
                a.Add(lintel, stone);
                for (float gx = x - w + .4f; gx < x + w - 1; gx += 2.2f)
                    a.Add(Garland(p, new Vector3(gx, 12.1f, 1.4f), new Vector3(gx + 2.2f, 12.1f, 1.4f), .55f, .08f), marigold);
                float bx = R.Mod(p.X - x + .1f, 2.2f) - 1.1f;
                if (MathF.Abs(p.X - x) < w - .3f)
                {
                    a.Add(Sdf.Capsule(new Vector3(bx, p.Y, p.Z - 1.6f), new Vector3(0, 12.1f, 0), new Vector3(0, 11.2f, 0), .012f), brass);
                    a.Add(MathF.Max(Sdf.ConeY(new Vector3(bx, p.Y - 11.05f, p.Z - 1.6f), .13f, .13f, .05f), -(p.Y - 10.95f)), brass);
                }
                mt = a.M; return a.D;
            });
        }
        var lt = Light(sky, 2, .01f); lt.HazeMax = .15f; lt.BloomStrength = .3f;
        return Bake.Layer(spec, sc, lt, cam);
    }
}
