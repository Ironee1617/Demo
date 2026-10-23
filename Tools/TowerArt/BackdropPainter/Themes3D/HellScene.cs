using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>The hell where Solomon bound the seventy-two demons, now trampled by the dark god: a sky of smoke lit
/// red from below over erupting volcanoes, black basalt prison towers chained together above a river of lava,
/// and in front the broken seal of Solomon with its hexagram still glowing, iron cages, spiked racks and obsidian
/// obelisks among smouldering cracks.</summary>
public sealed class HellScene : ThemePainter
{
    public override string Id => "hell";

    static readonly Vector3 GlowDir = Vector3.Normalize(new Vector3(.55f, .35f, -.45f));

    static Sky MakeSky(float period) => new(period, (-11, "3a0c06"), (-3, "4a1008"), (0, "300a06"), (3, "1a0806"), (6, "0e0505"), (10, "060303"))
    {
        SunX = 10, SunY = -1.5f, SunRadius = .01f, HaloRadius = 9, SunColor = new Vector3(4, 1.2f, .3f), HaloColor = new Vector3(1.4f, .35f, .08f),
    };

    static Lighting Light(Sky sky, float distance, float haze) => new()
    {
        SunDir = GlowDir, Sun = new Vector3(2.2f, .8f, .3f),
        SkyZenith = R.Rgb("3a1410") * .7f, SkyHorizon = R.Rgb("6a2410") * .7f, Bounce = R.Rgb("a83a14") * .6f, Ambient = .9f,
        Haze = Bake.HazeFrom(sky, .1f), Distance = distance, HazeDensity = haze, Exposure = .85f, ShadowK = 10, Saturation = 1.05f,
        BloomThreshold = 1.1f,
    };

    public static Lighting TerrainLight()
    {
        var l = Light(MakeSky(12), 0, 0); l.HazeMax = 0; l.Exposure = 1.3f; l.ShadowReach = 3; l.AoScale = .5f;
        return l;
    }

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return SkyLayer();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    static int Basalt(Mats m, int seed) => m.Rock("141212", "34302e", .7f, .75f, .5f, seed);

    /// <summary>Basalt broken by glowing magma veins.</summary>
    static int Cracked(Scene sc, int seed, float heat, float cover = .35f)
    {
        var n = sc.N; var dark = R.Rgb("161312"); var light = R.Rgb("3a3432"); var hot = R.Rgb("ff4a10"); var core = R.Rgb("ffc060");
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float t = n.Fbm(p, .6f, 4, seed) * .5f + .5f;
            var c = n.Voronoi(p, .5f, seed + 1);
            float vein = R.Smooth(.035f, 0, c.F2 - c.F1) * R.Smooth(cover, cover + .22f, n.Fbm(p, 1.5f, 2, seed + 2) * .5f + .5f);
            s.Albedo = R.Mix(dark, light, t) * (1 - vein); s.Rough = .7f; s.Spec = .05f;
            s.Emit = R.Mix(hot, core, vein * vein) * vein * heat;
            s.N = Mats.Bump(q => n.Fbm(q, .25f, 4, seed + 3) * .02f, p, s.N, 1, .01f);
        });
    }

    PaintedLayer SkyLayer()
    {
        var spec = new LayerSpec("sky", .95f, 48, 30, -11, 21, -97);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(2.5f);
        int rock = Cracked(sc, 701, 2f, .5f);
        int lava = m.Lava("1a0e0a", "ff4a10", 7, 702);
        var peaks = new[] { (5f, 5.5f, 2.6f), (14.5f, 7f, 3f), (1f, 2.6f, 2.2f), (22f, 3.4f, 2.4f) };
        sc.Add(new Vector3(0, -9, 30), new Vector3(30, 8, 50), (Vector3 p, out int mt) =>
        {
            float rise = R.Smooth(30, 38, p.Z);
            float h = R.Mix(-9, Kit3D.Mountain(n, 30, p.X, p.Z, -4.8f, peaks, .5f, 703), rise);
            // Craters at the summits of the two great cones, with lava spilling down the slopes.
            float slope = R.Smooth(.2f, .8f, n.Fbm(new Vector3(p.X * 3, p.Y * .5f, 0), .6f, 3, 704) * .5f + .5f) * R.Smooth(2, -1, MathF.Min(MathF.Abs(R.Wrap(p.X, 5, 30)), MathF.Abs(R.Wrap(p.X, 14.5f, 30))) - .4f);
            mt = slope > .5f ? lava : rock;
            return (p.Y - h) * .3f;
        });
        // Smoke banks lit from below by the fires.
        var smoke = sc.AddVolume(new Vector3(0, -2, 14), new Vector3(30, 10, 28), q =>
        {
            float f = n.Fbm(q * new Vector3(1, 1.6f, 1), 2.2f, 5, 705) * .5f + .5f;
            float band = R.Smooth(-1, 2, q.Y) * R.Smooth(10, 5, q.Y);
            return MathF.Max(0, f - .45f) * 2.2f * band;
        });
        smoke.Albedo = R.Rgb("3a2a28"); smoke.Steps = 36; smoke.Ambient = .6f; smoke.Emit = new Vector3(.06f, .012f, .003f);
        var lt = Light(sky, 70, .008f); lt.Background = sky.At; lt.HazeMax = .6f; lt.BloomStrength = .6f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Far()
    {
        var spec = new LayerSpec("far", .85f, 64, 32, -11, 19, -96);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3);
        int basalt = Cracked(sc, 711, .7f, .6f);
        int lava = m.Lava("1c100c", "ff5010", 8, 712);
        int iron = m.Metal("2e2a28", .5f, .6f, "5a2a14", 713);
        int cage = m.Glow("e8380e", .7f, "2a1a14", .4f, 714);
        sc.Add(new Vector3(0, -11, 0), new Vector3(32, -3, 8), (Vector3 p, out int mt) => { mt = lava; return p.Y + 3.4f + n.Fbm(new Vector3(p.X, 0, p.Z), .8f, 3, 715) * .05f; });
        var towers = new[] { (3.5f, 7.4f, 1.3f), (13f, 9.4f, 1.6f), (22.5f, 6.4f, 1.2f), (29f, 8.2f, 1.4f) };
        foreach (var (x, h, r) in towers)
        {
            int sd = (int)(x * 17);
            sc.Add(new Vector3(x - r * 1.6f, -4, 8.5f), new Vector3(x + r * 1.6f, h + .6f, 12.5f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(x, -3.4f, 10.5f); var a = Acc.Start;
                float rr = r * (1 - q.Y / (h + 3.4f) * .3f);
                float tower = MathF.Max(Sdf.Box(q - new Vector3(0, (h + 3.4f) * .5f, 0), new Vector3(rr, (h + 3.4f) * .5f, rr * .8f)), -Sdf.Box(new Vector3(q.X, R.Mod(q.Y, 1.8f) - .9f, q.Z + rr * .8f), new Vector3(rr * .45f, .45f, .25f)));
                tower += n.Fbm(p, .5f, 4, sd) * .08f;
                // Crenellated spiked crown.
                float spikes = Sdf.ConeY(new Vector3(Sdf.RepeatLimited(q.X, rr * .5f, 2), q.Y - h - 3.4f - .3f, q.Z), .3f, .12f, .01f);
                a.Add(MathF.Min(tower, spikes), basalt);
                // Glowing cell windows with iron bars.
                var wq = new Vector3(q.X, R.Mod(q.Y, 1.8f) - .9f, q.Z + rr * .8f - .2f);
                if (MathF.Abs(q.X) < rr * .45f && q.Y > .5f) a.Add(Sdf.Box(wq, new Vector3(rr * .45f, .45f, .02f)), cage);
                float bars = Sdf.Box(new Vector3(R.Mod(q.X, .18f) - .09f, wq.Y, wq.Z + .08f), new Vector3(.015f, .45f, .015f));
                if (MathF.Abs(q.X) < rr * .45f && q.Y > .5f) a.Add(bars, iron);
                mt = a.M; return a.D;
            });
        }
        // Giant chains slung between the towers.
        sc.Add(new Vector3(-1, 1, 9.6f), new Vector3(33, 7.6f, 11.4f), (Vector3 p, out int mt) =>
        {
            mt = iron;
            float d = Kit3D.Chain(p, new Vector3(4.6f, 5.8f, 10.5f), new Vector3(11.6f, 6.6f, 10.5f), 1.8f, .32f);
            d = MathF.Min(d, Kit3D.Chain(p, new Vector3(14.4f, 5.6f, 10.5f), new Vector3(21.6f, 4.6f, 10.5f), 1.6f, .32f));
            d = MathF.Min(d, Kit3D.Chain(p, new Vector3(23.4f, 4.4f, 10.5f), new Vector3(28f, 5.8f, 10.5f), 1.2f, .3f));
            return d;
        });
        var smoke = sc.AddVolume(new Vector3(0, -3.6f, 2), new Vector3(32, 2, 9), q => MathF.Max(0, n.Fbm(q, 1.4f, 4, 716) * .5f + .5f - .55f) * 1.6f * R.Smooth(2, -2.5f, q.Y));
        smoke.Albedo = R.Rgb("4a302a"); smoke.Steps = 24; smoke.Emit = new Vector3(.08f, .016f, .004f);
        var lt = Light(sky, 24, .014f); lt.HazeMax = .45f; lt.BloomStrength = .7f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Mid()
    {
        var spec = new LayerSpec("mid", .65f, 96, 36, -9.5f, 21, -95);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3.5f);
        int ground = Cracked(sc, 721, 1.8f, .52f);
        int sealStone = Basalt(m, 722);
        int seal = m.Glyphs(sealStone, "ff8a30", 9, p => R.Smooth(.07f, .03f, Kit3D.Hexagram(p.X - 18, p.Y - 2.4f, 3.1f)));
        int iron = m.Metal("2a2624", .5f, .65f, "6a2a12", 723);
        int obsidian = sc.Material((Vector3 p, ref Surface s) => { s.Albedo = R.Rgb("0a0808"); s.Rough = .12f; s.Spec = .06f; });
        int wood = m.Wood("1e1410", "3a2a20", .9f, true, 724);
        int lava = m.Lava("1c100c", "ff5010", 9, 725);
        sc.Add(new Vector3(0, -6, 1.5f), new Vector3(36, -1.8f, 15), (Vector3 p, out int mt) =>
        {
            mt = ground; float top = -2.66f + n.Fbm(new Vector3(p.X, 0, p.Z), 1.2f, 4, 726) * .2f + R.Smooth(5, 14, p.Z) * .8f;
            return MathF.Max(p.Y - top, 2.2f - p.Z) * .8f;
        });
        sc.Add(new Vector3(26, -3, 3), new Vector3(34, -2.4f, 7), (Vector3 p, out int mt) => { mt = lava; return p.Y + 2.68f + MathF.Max(0, MathF.Pow((p.X - 30) / 3.6f, 2) + MathF.Pow((p.Z - 5) / 1.8f, 2) - 1) * .3f; });
        // The broken seal: a great disc split in three, the hexagram still burning in its face.
        sc.Add(new Vector3(13.6f, -3, 5), new Vector3(22.4f, 6, 8), (Vector3 p, out int mt) =>
        {
            var q = p - new Vector3(18, 2.4f, 6.5f);
            float disc = Sdf.CylinderZ(q, 3.4f, .4f);
            // Fracture planes with displaced chunks.
            float crack1 = MathF.Abs(q.X * .8f + q.Y * .6f - .3f + n.Fbm(p, .4f, 3, 727) * .2f) - .05f;
            float crack2 = MathF.Abs(-q.X * .5f + q.Y * .85f + .9f + n.Fbm(p, .4f, 3, 728) * .2f) - .05f;
            float d = MathF.Max(disc, -MathF.Min(crack1, crack2)) + n.Fbm(p, .3f, 3, 729) * .04f;
            // The lower part sinks into the ground.
            mt = seal; return MathF.Max(d, -(p.Y + 2.7f));
        });
        // Iron cages, spiked racks and obsidian obelisks.
        foreach (var (x, z) in new[] { (5.4f, 4.8f), (24.6f, 5.4f) })
            sc.Add(new Vector3(x - 1.2f, -2.8f, z - 1.2f), new Vector3(x + 1.2f, .4f, z + 1.2f), (Vector3 p, out int mt) =>
            {
                mt = iron; var q = p - new Vector3(x, -2.66f, z);
                float frame = Sdf.BoxFrame(q - new Vector3(0, 1.35f, 0), new Vector3(.95f, 1.35f, .95f), .05f);
                float bars = MathF.Min(Sdf.Box(new Vector3(R.Mod(q.X, .24f) - .12f, q.Y - 1.35f, MathF.Abs(q.Z) - .92f), new Vector3(.02f, 1.3f, .02f)), Sdf.Box(new Vector3(MathF.Abs(q.X) - .92f, q.Y - 1.35f, R.Mod(q.Z, .24f) - .12f), new Vector3(.02f, 1.3f, .02f)));
                float spikes = Sdf.ConeY(new Vector3(R.Mod(q.X, .24f) - .12f, q.Y - 2.85f, MathF.Abs(q.Z) - .92f), .15f, .03f, .002f);
                return MathF.Min(MathF.Min(frame, MathF.Max(bars, Sdf.Box(q - new Vector3(0, 1.35f, 0), new Vector3(.95f, 1.35f, .95f)))), spikes);
            });
        foreach (float x in new[] { 9.6f, 31.6f })
            sc.Add(new Vector3(x - 1.4f, -2.8f, 3.6f), new Vector3(x + 1.4f, .8f, 5.6f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(x, -2.66f, 4.6f); var a = Acc.Start;
                float posts = MathF.Min(Sdf.Box(q - new Vector3(-1.1f, 1.4f, 0), new Vector3(.1f, 1.4f, .1f)), Sdf.Box(q - new Vector3(1.1f, 1.4f, 0), new Vector3(.1f, 1.4f, .1f)));
                float beams = MathF.Min(Sdf.Box(q - new Vector3(0, 2.6f, 0), new Vector3(1.25f, .1f, .1f)), Sdf.Box(q - new Vector3(0, .9f, 0), new Vector3(1.25f, .1f, .1f)));
                a.Add(MathF.Min(posts, beams), wood);
                float sp = MathF.Min(Sdf.ConeY(new Vector3(Sdf.RepeatLimited(q.X, .3f, 3), q.Y - 2.85f, q.Z), .15f, .05f, .002f), Sdf.ConeY(new Vector3(Sdf.RepeatLimited(q.X, .3f, 3), q.Y - 1.15f, q.Z), .15f, .05f, .002f));
                a.Add(sp, iron);
                a.Add(Kit3D.Chain(q, new Vector3(-.6f, 2.5f, -.12f), new Vector3(-.6f, 1.2f, -.12f), 0, .12f), iron);
                a.Add(Kit3D.Chain(q, new Vector3(.6f, 2.5f, -.12f), new Vector3(.6f, 1.4f, -.12f), 0, .12f), iron);
                mt = a.M; return a.D;
            });
        foreach (var (x, h) in new[] { (1.8f, 5.2f), (12.4f, 3.6f), (28.8f, 4.4f) })
            sc.Add(new Vector3(x - .8f, -2.8f, 6), new Vector3(x + .8f, -2.6f + h + .8f, 8), (Vector3 p, out int mt) =>
            {
                mt = obsidian; var q = p - new Vector3(x, -2.66f, 7);
                float w = .45f - q.Y / h * .15f;
                return MathF.Min(MathF.Max(Sdf.Box(q - new Vector3(0, h * .5f, 0), new Vector3(w, h * .5f, w)), -q.Y), Sdf.Pyramid(q - new Vector3(0, h, 0), .6f, .6f)) + n.Fbm(p, .2f, 3, 730) * .015f;
            });
        var lt = Light(sky, 6, .012f); lt.HazeMax = .25f; lt.BloomStrength = .7f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Near()
    {
        // Chains and hooks hanging from the top when the camera climbs.
        var spec = new LayerSpec("near", .4f, 96, 40, 8.6f, 5, -94);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(4);
        int iron = m.Metal("2a2624", .45f, .6f, "6a2a12", 741);
        var rnd = new Rand(742); var hangs = new List<(float x, float len, float z)>();
        for (float x = 1; x < 40; x += rnd.R(2.2f, 4.6f)) hangs.Add((x, rnd.R(1.4f, 4), rnd.R(1.2f, 2.8f)));
        sc.Add(new Vector3(0, 8.6f, .5f), new Vector3(40, 13.6f, 3.5f), (Vector3 p, out int mt) =>
        {
            mt = iron; float d = 1e9f;
            foreach (var (x, len, z) in hangs)
            {
                float dx = R.Wrap(p.X, x, 40); if (MathF.Abs(dx) > .4f) continue;
                var q = new Vector3(dx, p.Y, p.Z - z);
                d = MathF.Min(d, Kit3D.Chain(q, new Vector3(0, 13.8f, 0), new Vector3(0, 13.8f - len, 0), 0, .14f));
                var hq = q - new Vector3(0, 13.8f - len - .2f, 0);
                d = MathF.Min(d, MathF.Max(Sdf.TorusZ(hq, .14f, .03f), hq.Y - .02f));
            }
            return d;
        });
        var lt = Light(sky, 2, .01f); lt.HazeMax = .15f;
        return Bake.Layer(spec, sc, lt, cam);
    }
}
