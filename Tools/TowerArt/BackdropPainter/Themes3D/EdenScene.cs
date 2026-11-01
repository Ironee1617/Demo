using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>The garden of Eden on a golden morning: cumulus over green highlands where waterfalls feed the four
/// rivers, the Tree of Life glowing with golden fruit, and in front the tree of knowledge with red fruit and the
/// coiled serpent, a lily pond, flowering shrubs, stepping stones and shafts of sunlight.</summary>
public sealed class EdenScene : ThemePainter
{
    public override string Id => "eden";

    static readonly Vector3 SunDir = Vector3.Normalize(new Vector3(.45f, .7f, -.4f));

    static Sky MakeSky(float period) => new(period, (-11, "f4ecd0"), (-3, "f2e8c8"), (0, "d6e2d8"), (3, "a8cce8"), (6, "78b0e4"), (10, "4c8cd8"))
    {
        SunX = 19.5f, SunY = 6.2f, SunRadius = .5f, HaloRadius = 5, SunColor = new Vector3(30, 27, 20), HaloColor = new Vector3(1.2f, 1, .6f),
        CirrusAmount = .4f, CirrusLow = 4, CirrusHigh = 10,
    };

    static Lighting Light(Sky sky, float distance, float haze) => new()
    {
        SunDir = SunDir, Sun = new Vector3(4.2f, 3.8f, 2.9f),
        SkyZenith = R.Rgb("6aa2dc") * .9f, SkyHorizon = R.Rgb("d8e0cc") * .8f, Bounce = R.Rgb("6a8a40") * .45f, Ambient = .9f,
        Haze = Bake.HazeFrom(sky, .05f), Distance = distance, HazeDensity = haze, Exposure = .9f, ShadowK = 12, Saturation = 1.1f,
    };

    public static Lighting TerrainLight()
    {
        var l = Light(MakeSky(12), 0, 0); l.HazeMax = 0; l.Exposure = .85f; l.ShadowReach = 3; l.AoScale = .5f;
        return l;
    }

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return SkyLayer();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    static int Highland(Scene sc, int seed)
    {
        var n = sc.N; var grassA = R.Rgb("4a7a32"); var grassB = R.Rgb("9ab858"); var rock = R.Rgb("8a8070");
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float t = n.Fbm(p, 1, 5, seed) * .5f + .5f;
            float cliff = R.Smooth(.55f, .3f, s.N.Y + n.Fbm(p, .5f, 3, seed + 1) * .2f);
            s.Albedo = R.Mix(R.Mix(grassA, grassB, t), rock * (.8f + t * .4f), cliff);
            s.Rough = .9f; s.Trans = (1 - cliff) * .15f;
            s.N = Mats.Bump(q => n.Fbm(q, .2f, 4, seed + 2) * .05f, p, s.N, 1, .02f);
        });
    }

    PaintedLayer SkyLayer()
    {
        var spec = new LayerSpec("sky", .95f, 128, 30, -11, 21, -97);
        var sc = new Scene(spec.Period); var n = sc.N; var sky = MakeSky(spec.Period); var cam = new Camera(2.5f);
        int land = Highland(sc, 301);
        var peaks = new[] { (4f, 3.4f, 4f), (12f, 2.2f, 3f), (21f, 4f, 4.4f), (27f, 2.6f, 3f) };
        sc.Add(new Vector3(0, -9, 30), new Vector3(30, 5, 48), (Vector3 p, out int m) =>
        {
            m = land; float rise = R.Smooth(30, 38, p.Z);
            float h = R.Mix(-9, Kit3D.Mountain(n, 30, p.X, p.Z, -4.6f, peaks, .5f, 302) * .85f, rise);
            return (p.Y - h) * .3f;
        });
        foreach (var (x, y, z, s) in new[] { (3.5f, 3.2f, 24f, 1f), (12f, 5f, 26f, .8f), (25.5f, 2.6f, 22f, 1.1f) })
        {
            var blobs = new[] { (new Vector3(x, y, z), 2.4f * s), (new Vector3(x + 1.8f * s, y + .6f, z), 1.9f * s), (new Vector3(x - 1.7f * s, y - .2f, z), 1.8f * s), (new Vector3(x + .4f, y + 1.6f * s, z), 1.6f * s), (new Vector3(x + 3.2f * s, y - .4f, z), 1.3f * s) };
            var cu = sc.AddVolume(new Vector3(x - 5 * s, y - 3 * s, z - 3 * s), new Vector3(x + 5.5f * s, y + 4 * s, z + 3 * s), Kit3D.Cumulus(n, blobs, 2.4f, .5f, (int)(x * 7)));
            cu.Albedo = R.Rgb("ffffff"); cu.Steps = 40;
        }
        var lt = Light(sky, 70, .009f); lt.Background = sky.At; lt.HazeMax = .7f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Far()
    {
        var spec = new LayerSpec("far", .85f, 128, 32, -11, 21, -96);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3);
        int land = Highland(sc, 311);
        int bark = m.Wood("5a4430", "a08a68", .85f, true, 313);
        int leaves = m.Foliage("2e5a26", "8ab450", .45f, 314);
        int gold = sc.Material((Vector3 p, ref Surface s) => { s.Albedo = R.Rgb("f2c24a"); s.Rough = .3f; s.Spec = .08f; s.Emit = R.Rgb("ffc84a") * 3; });
        int water = m.Water("1e5a6a", .8f, .2f, 315);
        // Terraced cliffs with waterfalls pouring into a river at their foot.
        sc.Add(new Vector3(0, -11, 6), new Vector3(32, 2.4f, 16), (Vector3 p, out int mt) =>
        {
            mt = land;
            float terraces = MathF.Floor((n.Fbm(new Vector3(p.X, 0, p.Z), 6, 3, 316) * .5f + .5f) * 4) * .7f;
            float h = -2.4f + terraces + n.Fbm(p, .6f, 4, 317) * .25f + MathF.Exp(-MathF.Pow(R.Wrap(p.X, 10, 32) / 6, 2)) * 1.6f;
            h = R.Mix(-11, h, R.Smooth(6, 8.5f, p.Z));
            return (p.Y - h) * .3f;
        });
        foreach (var (x, top, w) in new[] { (6.5f, .4f, .4f), (13.2f, 1.2f, .55f), (24.8f, -.3f, .35f) })
        {
            // Falling water as a streaked translucent sheet that widens and breaks up as it drops.
            var fall = sc.AddVolume(new Vector3(x - w * 1.6f, -3.4f, 6.6f), new Vector3(x + w * 1.6f, top + .1f, 7.7f), q =>
            {
                float spread = w * (1 + (top - q.Y) * .08f), dx = MathF.Abs(q.X - x);
                if (dx > spread || MathF.Abs(q.Z - 7.15f) > .22f || q.Y > top || q.Y < -3.3f) return 0;
                float streak = n.Fbm(new Vector3(q.X * 5, q.Y * .35f, q.Z), .35f, 4, 318) * .5f + .5f;
                return R.Smooth(1, .4f, dx / spread) * R.Smooth(.25f, .7f, streak) * 9;
            });
            fall.Albedo = R.Rgb("f4fbff"); fall.Steps = 20; fall.Ambient = 1.3f; fall.Emit = new Vector3(.25f, .3f, .35f);
            var spray = sc.AddVolume(new Vector3(x - 1.4f, -3.6f, 5.8f), new Vector3(x + 1.4f, -1.6f, 8.6f), q => { float d = (q - new Vector3(x, -3.2f, 7.1f)).Length(); return d < 1.3f ? (1.3f - d) * (n.Fbm(q, .4f, 3, 319) * .5f + .5f) * 2 : 0; });
            spray.Albedo = R.Rgb("f8fcff"); spray.Steps = 24;
        }
        sc.Add(new Vector3(0, -3.6f, 1), new Vector3(32, -3, 6.4f), (Vector3 p, out int mt) => { mt = water; return p.Y + 3.25f; });
        // The Tree of Life: a towering trunk under a broad crown hung with glowing golden fruit.
        var rnd = new Rand(320); var lobes = Kit3D.Crown(rnd, new Vector3(20, 3.8f, 11.5f), 8, 3, 20, .3f);
        var fruits = new List<Vector3>(); for (int k = 0; k < 28; k++) { var (c, r) = lobes[k % lobes.Length]; var dir = Vector3.Normalize(new Vector3(rnd.R(-1, 1), rnd.R(-1, .2f), -1)); fruits.Add(c + dir * r * .95f); }
        var (bmin, bmax) = Kit3D.LobeBounds(lobes, .6f);
        sc.Add(Vector3.Min(bmin, new Vector3(16, -1.6f, 9)), Vector3.Max(bmax, new Vector3(24, 6, 14)), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start;
            a.Add(Kit3D.Trunk(p - new Vector3(20, -1.2f, 11.5f), 4.2f, .55f, new[] { (new Vector3(0, 3, 0), new Vector3(-2.6f, 5, 0), .2f), (new Vector3(0, 3.2f, 0), new Vector3(2.8f, 5.2f, 0), .2f) }), bark);
            a.Add(Kit3D.Canopy(n, p, lobes, .4f, 321), leaves);
            foreach (var f in fruits) a.Add(Sdf.Sphere(p - f, .13f), gold);
            mt = a.M; return a.D;
        });
        var lt = Light(sky, 24, .016f); lt.HazeMax = .5f; lt.BloomStrength = .4f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Mid()
    {
        var spec = new LayerSpec("mid", .65f, 192, 36, -9.5f, 21, -95);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3.5f);
        int meadow = m.Ground("3e6a2a", "8aae4e", "5a4630", 331, "f8e8f0", .25f);
        int bark = m.Wood("4a3424", "8a6a4a", .85f, true, 332);
        int leaves = m.Foliage("285a24", "7aaa48", .45f, 333);
        int apple = m.Paint("c4201a", .28f, .15f, 334, .07f);
        int serpentSkin = sc.Material((Vector3 p, ref Surface s) =>
        {
            float scale = MathF.Sin(p.X * 60) * MathF.Sin(p.Y * 60 + p.Z * 40);
            s.Albedo = R.Mix(R.Rgb("2a5a22"), R.Rgb("b8a83a"), R.Smooth(.2f, .9f, scale * .5f + .5f) * .5f); s.Rough = .3f; s.Spec = .07f;
        });
        int water = m.Water("1a4a4a", .5f, .15f, 335);
        int lilyPad = m.Foliage("2e6a2a", "6aa046", .3f, 336);
        int lily = m.Paint("fbf6f0", .6f, .1f, 337);
        int stone = m.Rock("6a6458", "aaa290", .8f, .8f, .4f, 338, "4e7034", .4f);
        int[] blossom = { m.Foliage("2a5a24", "7aa848", .4f, 339, "f4a8c0", .55f), m.Foliage("2a5a24", "7aa848", .4f, 340, "fbe8a0", .5f), m.Foliage("2a5a24", "7aa848", .4f, 341, "f6f2f8", .5f) };
        // Meadow rolling back from the walkway with a pond on the left.
        sc.Add(new Vector3(0, -6, 1.5f), new Vector3(36, -1.2f, 15), (Vector3 p, out int mt) =>
        {
            mt = meadow;
            float pond = MathF.Exp(-MathF.Pow(R.Wrap(p.X, 7, 36) / 3.6f, 2) - MathF.Pow((p.Z - 5) / 2.4f, 2));
            float top = -2.66f + n.Fbm(new Vector3(p.X, 0, p.Z), 1.6f, 4, 342) * .14f + R.Smooth(5, 14, p.Z) * .9f - pond * .5f;
            return MathF.Max(p.Y - top, 2.2f - p.Z) * .8f;
        });
        sc.Add(new Vector3(3, -3.2f, 2.6f), new Vector3(11, -2.7f, 7.6f), (Vector3 p, out int mt) => { mt = water; return p.Y + 2.86f; });
        foreach (var (x, z) in new[] { (5f, 3.6f), (6.4f, 4.6f), (8.2f, 3.8f), (9.2f, 5.2f), (4.2f, 5.6f) })
        {
            sc.Add(new Vector3(x - .5f, -2.95f, z - .5f), new Vector3(x + .5f, -2.4f, z + .5f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(x, -2.84f, z); var a = Acc.Start;
                float pad = MathF.Max(Sdf.CylinderY(q, .36f, .015f), -Sdf.Box(Sdf.RotY(q, .6f) - new Vector3(.3f, 0, 0), new Vector3(.3f, .1f, .03f)));
                a.Add(pad, lilyPad);
                var fq = q - new Vector3(.08f, .08f, 0);
                float petals = Sdf.Ellipsoid(Sdf.RepeatAngleY(fq, 8) - new Vector3(.08f, 0, 0), new Vector3(.08f, .05f, .03f));
                a.Add(petals, lily);
                mt = a.M; return a.D;
            });
        }
        foreach (var (x, z) in new[] { (12.6f, 3.4f), (13.8f, 3.9f), (15f, 3.4f) })
            sc.Add(new Vector3(x - .45f, -3, z - .45f), new Vector3(x + .45f, -2.4f, z + .45f), (Vector3 p, out int mt) => { mt = stone; return Sdf.Ellipsoid(p - new Vector3(x, -2.72f, z), new Vector3(.38f, .14f, .32f)) + n.Fbm(p, .1f, 3, 343) * .02f; });
        // The tree of knowledge with its fruit and the serpent coiled on a limb.
        var rnd = new Rand(344); var lobes = Kit3D.Crown(rnd, new Vector3(22, 3.6f, 6.6f), 7.6f, 3.4f, 18, .35f);
        var fruits = new List<Vector3>(); for (int k = 0; k < 26; k++) { var (c, r) = lobes[k % lobes.Length]; var dir = Vector3.Normalize(new Vector3(rnd.R(-1, 1), rnd.R(-1, .1f), -1.2f)); fruits.Add(c + dir * r * .92f); }
        var limbs = new[] { (new Vector3(0, 2.6f, 0), new Vector3(-2.8f, 4.4f, -.2f), .26f), (new Vector3(0, 3, 0), new Vector3(2.8f, 4.8f, 0), .24f), (new Vector3(0, 2.2f, 0), new Vector3(-3.4f, 2.8f, -.6f), .16f) };
        var coil = new List<Vector3>();
        for (int k = 0; k <= 40; k++)
        {
            float t = k / 40f; var along = limbs[2].Item1 + (limbs[2].Item2 - limbs[2].Item1) * (.25f + t * .7f);
            float ang = t * MathF.PI * 6; coil.Add(along + new Vector3(0, MathF.Cos(ang) * .2f, MathF.Sin(ang) * .2f));
        }
        var (lmin, lmax) = Kit3D.LobeBounds(lobes, .7f);
        sc.Add(Vector3.Min(lmin, new Vector3(17, -2.9f, 4)), Vector3.Max(lmax, new Vector3(27, 6, 9.4f)), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start; var q = p - new Vector3(22, -2.66f, 6.6f);
            a.Add(Kit3D.Trunk(q, 3.4f, .5f, limbs) + n.Fbm(p * new Vector3(1, .4f, 1), .3f, 3, 345) * .05f, bark);
            a.Add(Kit3D.Canopy(n, p, lobes, .35f, 346), leaves);
            foreach (var f in fruits) a.Add(Sdf.Sphere(p - f, .14f) + MathF.Max(0, .03f - (p - f).Y * .2f), apple);
            float serpent = 1e9f;
            for (int k = 1; k < coil.Count; k++) serpent = MathF.Min(serpent, Sdf.Capsule(q, coil[k - 1], coil[k], .07f - k * .001f));
            var head = coil[^1] + new Vector3(-.2f, .05f, -.1f);
            serpent = MathF.Min(serpent, Sdf.Ellipsoid(q - head, new Vector3(.14f, .07f, .09f)));
            a.Add(serpent, serpentSkin);
            mt = a.M; return a.D;
        });
        // Flowering shrubs.
        foreach (var (x, z, w, k) in new[] { (1.2f, 4.4f, 2f, 0), (14.6f, 6.4f, 2.4f, 1), (29.4f, 4.6f, 2.6f, 2), (33.6f, 5.4f, 2f, 0), (18f, 4f, 1.6f, 2) })
        {
            var r2 = new Rand((int)(x * 53)); var lb = Kit3D.Crown(r2, new Vector3(x, -2.66f + w * .3f, z), w, w * .45f, 9, .7f);
            var (smin, smax) = Kit3D.LobeBounds(lb, .4f); int mat = blossom[k];
            sc.Add(smin, smax, (Vector3 p, out int mt) => { mt = mat; return Kit3D.Canopy(n, p, lb, .14f, (int)(x * 9)); });
        }
        // Shafts of sunlight slanting through the canopy.
        var rays = sc.AddVolume(new Vector3(16, -3, 3), new Vector3(30, 8, 10), q =>
        {
            float u = q.X - (q.Y - 8) * -.45f; float band = MathF.Abs(R.Mod(u, 3.4f) - 1.7f);
            float shaft = R.Smooth(.9f, 0, band) * (n.Fbm(new Vector3(u, 0, 0), .8f, 2, 347) * .5f + .5f);
            return shaft * shaft * .05f * R.Smooth(-3, 3, q.Y);
        });
        rays.Albedo = R.Rgb("fff2c8"); rays.Anisotropy = .6f; rays.Steps = 16; rays.Ambient = .2f;
        var lt = Light(sky, 5, .01f); lt.HazeMax = .2f; lt.BloomStrength = .35f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Near()
    {
        // Vines and blossoms hanging from the top when the camera climbs.
        var spec = new LayerSpec("near", .4f, 192, 40, 8.6f, 5, -94);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(4);
        int leaves = m.Foliage("1e4a1e", "5a8a3a", .45f, 351, "f2a0c0", .3f);
        int vine = m.Wood("3a3020", "6a5a3a", .9f, true, 352);
        foreach (var (x, w) in new[] { (6f, 6f), (26f, 7f) })
        {
            var rnd = new Rand((int)x * 3); var lobes = new List<(Vector3, float)>();
            for (int k = 0; k < 10; k++) lobes.Add((new Vector3(x + rnd.R(-w, w), 12.4f + rnd.R(-.4f, .6f), 2 + rnd.R(-.5f, .5f)), rnd.R(.6f, 1.1f)));
            var arr = lobes.ToArray(); var strands = new List<(Vector3, Vector3)>();
            for (int k = 0; k < 9; k++) { float sx = x + rnd.R(-w * .9f, w * .9f); strands.Add((new Vector3(sx, 12.4f, 1.6f), new Vector3(sx + rnd.R(-.2f, .2f), 12.4f - rnd.R(1.2f, 3.2f), 1.5f))); }
            sc.Add(new Vector3(x - w - 1.2f, 8.6f, .2f), new Vector3(x + w + 1.2f, 13.6f, 3.8f), (Vector3 p, out int mt) =>
            {
                var a = Acc.Start;
                a.Add(Kit3D.Canopy(n, p, arr, .2f, (int)x), leaves);
                foreach (var (s0, s1) in strands)
                {
                    float d = Sdf.Capsule(p, s0, s1, .03f);
                    float buds = Sdf.Sphere(new Vector3(p.X - s1.X, R.Mod(p.Y, .35f) - .175f, p.Z - s1.Z), .08f);
                    a.Add(d, vine);
                    if (p.Y > s1.Y && p.Y < s0.Y) a.Add(MathF.Max(buds, MathF.Abs(p.X - s1.X) - .12f) + n.Fbm(p, .04f, 2, 353) * .02f, leaves);
                }
                mt = a.M; return a.D;
            });
        }
        var lt = Light(sky, 2, .01f); lt.HazeMax = .15f;
        return Bake.Layer(spec, sc, lt, cam);
    }
}
