using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>The Lantern Village on the planet Lumen at dusk: a violet sky with two moons and a ringed planet over
/// rolling meadows and a lake, the dark god's tower piercing the sky far away, and in front timber cottages with
/// lit windows, the tents of refugee gods from every pantheon, strings of paper lanterns and a great lantern tree.</summary>
public sealed class VillageScene : ThemePainter
{
    public override string Id => "village";

    const float G = -2.66f;
    static readonly Vector3 DuskDir = Vector3.Normalize(new Vector3(.62f, .22f, -.5f));
    static readonly Vector3 SetSun = Vector3.Normalize(new Vector3(.75f, -.3f, -.6f));

    static Sky MakeSky(float period) => new(period, (-11, "e89a70"), (-4, "d88470"), (-1, "b06a7e"), (2, "6a4a8a"), (5, "34306a"), (10, "161a40"))
    {
        SunX = 4, SunY = -7, SunRadius = .01f, HaloRadius = 10, SunColor = new Vector3(.3f, .12f, .06f), HaloColor = new Vector3(1.1f, .45f, .25f),
        Stars = .03f, StarsBelow = 2, CirrusAmount = .3f, CirrusLow = -3, CirrusHigh = 4, CirrusColor = new Vector3(1.3f, .72f, .7f),
    };

    static Lighting Light(Sky sky, float distance, float haze) => new()
    {
        SunDir = DuskDir, Sun = new Vector3(1.5f, .85f, .7f),
        SkyZenith = R.Rgb("4a3c8a") * .8f, SkyHorizon = R.Rgb("c07a7c") * .8f, Bounce = R.Rgb("4a3a34") * .45f, Ambient = .8f,
        Haze = Bake.HazeFrom(sky, .3f), Distance = distance, HazeDensity = haze, Exposure = 1.15f, ShadowK = 8, Saturation = 1.05f,
        BloomThreshold = .9f,
    };

    public static Lighting TerrainLight()
    {
        var l = Light(MakeSky(12), 0, 0); l.HazeMax = 0; l.Exposure = 1.35f; l.ShadowReach = 3; l.AoScale = .5f;
        return l;
    }

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return SkyLayer();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    /// <summary>A lit sphere painted on the sky: moons and the ringed planet. Returns coverage.</summary>
    static float Body(float dx, float dy, float r, out float lit, out Vector3 nrm)
    {
        float d = MathF.Sqrt(dx * dx + dy * dy); lit = 0; nrm = Vector3.Zero;
        if (d > r) return 0;
        float nz = MathF.Sqrt(MathF.Max(0, 1 - d * d / (r * r)));
        nrm = new Vector3(dx / r, dy / r, -nz);
        lit = MathF.Max(0, Vector3.Dot(nrm, SetSun));
        return R.Smooth(r, r - .03f, d);
    }

    static Vector3 Celestial(Noise n, Sky sky, float x, float v, float period)
    {
        var c = sky.At(x, v);
        // Ringed planet low in the east, its rings tilted across the face.
        float px = R.Wrap(x, 14.4f, period), py = v - 3.7f; const float pr = 1.75f, tilt = -.28f;
        float ca = MathF.Cos(tilt), sa = MathF.Sin(tilt), rx = px * ca - py * sa, ry = px * sa + py * ca;
        float re = MathF.Sqrt(rx * rx + ry * ry / .04f) * 2.3f / pr;
        float ringA = R.Smooth(3, 3.2f, re) * R.Smooth(5.4f, 5.1f, re) * (.6f + .4f * MathF.Sin(re * 9) * MathF.Sin(re * 23 + 1)) * R.Smooth(.05f, .14f, MathF.Abs(re - 4.3f)) * .85f;
        var ringCol = R.Rgb("e6cfc0") * (.55f + .45f * R.Smooth(-3, 4, rx));
        if (ry >= 0) c = R.Mix(c, ringCol, ringA);
        float cov = Body(px, py, pr, out float lit, out var nrm);
        if (cov > 0)
        {
            float band = MathF.Sin(ry * 5.5f + n.Fbm(new Vector3(rx * .3f, ry * 2, 0), 1.2f, 3, 1101) * 1.8f) * .5f + .5f;
            var col = R.Mix(R.Rgb("8aa6b6"), R.Rgb("dcc0a0"), band);
            // The ring's shadow falls across the lit side as a thin dark band.
            float shadow = R.Smooth(.0f, .12f, MathF.Abs(ry + .45f) - .08f);
            var lc = col * (lit * 1.5f * (.35f + .65f * shadow) + .04f) + R.Rgb("8ab0ff") * MathF.Pow(1 + nrm.Z, 3) * .25f * lit;
            c = R.Mix(c, lc, cov);
        }
        if (ry < 0) c = R.Mix(c, ringCol, ringA);
        // The great pale moon and its small amber sister.
        foreach (var (mx, my, mr, hex, seed) in new[] { (7.6f, 4.3f, 1.05f, "dfe6f2", 1102), (4.2f, 2.2f, .4f, "f2c890", 1103) })
        {
            float dx = R.Wrap(x, mx, period), dy = v - my;
            float mc = Body(dx, dy, mr, out float ml, out _);
            float d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > mr) { c += R.Rgb(hex) * .08f * MathF.Exp(-(d - mr) * 2.2f); continue; }
            float maria = n.Fbm(new Vector3(dx, dy, 0) / mr * 2, 1.4f, 4, seed) * .5f + .5f;
            var col = R.Rgb(hex) * (.62f + .38f * R.Smooth(.35f, .65f, maria));
            c = R.Mix(c, col * (ml * 1.6f + .05f), mc);
        }
        return c;
    }

    PaintedLayer SkyLayer()
    {
        var spec = new LayerSpec("sky", .95f, 48, 30, -11, 21, -97);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(2.5f);
        int rock = m.Rock("1e1a30", "3a3050", .8f, .8f, .4f, 1111);
        var peaks = new[] { (3f, 3.2f, 3f), (11f, 2.4f, 2.4f), (17f, 3.6f, 3f), (24f, 2f, 2.6f) };
        sc.Add(new Vector3(0, -9, 30), new Vector3(30, 0, 50), (Vector3 p, out int mt) =>
        {
            mt = rock; float h = R.Mix(-9, Kit3D.Mountain(n, 30, p.X, p.Z, -6.4f, peaks, .45f, 1112), R.Smooth(30, 38, p.Z));
            return (p.Y - h) * .3f;
        });
        // The dark god's tower, a twisted black spire running out of the top of the sky with red slits burning in it.
        var tc = new Vector3(.5f, -7, 34);
        int stone = sc.Material((Vector3 p, ref Surface s) => { s.Albedo = R.Rgb("100c14") * (.8f + .2f * n.Fbm(p, .6f, 3, 1113)); s.Rough = .5f; s.Spec = .05f; });
        int tower = m.Glyphs(stone, "ff2a18", 6, p =>
        {
            var q = new Vector3(R.Wrap(p.X, tc.X, 30), p.Y - tc.Y, p.Z - tc.Z); float a = MathF.Atan2(q.Z, q.X) + q.Y * .15f;
            float sector = R.Mod(a / (MathF.PI * 2) * 6 + .5f, 1), band = R.Mod(q.Y, 1.6f);
            // Slits only in the shaft faces, never on the buttress fins.
            float shaft = R.Smooth(.12f, .02f, MathF.Sqrt(q.X * q.X + q.Z * q.Z) - (1.5f - R.Clamp01(q.Y / 18) * 1.05f));
            return R.Smooth(.07f, .04f, MathF.Abs(sector - .5f)) * R.Smooth(.3f, .45f, band) * R.Smooth(1.1f, .95f, band) * shaft;
        });
        sc.Add(new Vector3(-3, -7.5f, 30.5f), new Vector3(4, 11, 37.5f), (Vector3 p, out int mt) =>
        {
            mt = tower; var q = Sdf.RotY(p - tc, (p.Y - tc.Y) * .15f);
            float y = q.Y, rad = 1.5f - R.Clamp01(y / 18) * 1.05f + MathF.Max(0, .16f - MathF.Abs(R.Mod(y + .8f, 1.6f) - .8f)) * .5f;
            var f = Sdf.RepeatAngleY(q, 6);
            float shaft = MathF.Max(MathF.Max(f.X - rad, MathF.Abs(f.Z) - rad * .58f), -y);
            // Buttress fins flaring at the base and spikes along the edges.
            var b = Sdf.RepeatAngleY(q, 3);
            float fin = MathF.Max(MathF.Max(Sdf.Box(new Vector3(b.X - rad - .6f, y - 2.2f, b.Z), new Vector3(1, 2.2f, .14f)), (b.X - rad) * .6f + (y - 1) * .8f - 1.2f), -y);
            float spike = Sdf.ConeY(new Vector3(f.X - rad - .1f, R.Mod(y, 3.2f) - 1.6f, f.Z), .4f, .1f, .005f);
            return MathF.Min(MathF.Min(shaft, fin), MathF.Max(spike, -y + 2)) * .8f;
        });
        // A slow dark vortex of cloud wound round the upper tower.
        var vortex = sc.AddVolume(new Vector3(-8, 3, 26), new Vector3(9, 10.5f, 42), q =>
        {
            float dx = R.Wrap(q.X, tc.X, 30), dz = q.Z - tc.Z, r = MathF.Sqrt(dx * dx + dz * dz), a = MathF.Atan2(dz, dx);
            float swirl = n.Fbm(new Vector3(MathF.Cos(a + r * .5f) * r, q.Y * 1.4f, MathF.Sin(a + r * .5f) * r), 2, 4, 1114) * .5f + .5f;
            return MathF.Max(0, swirl - .45f) * 2.4f * R.Smooth(1.6f, 3, r) * R.Smooth(8, 4, r) * R.Smooth(3, 6, q.Y);
        });
        vortex.Albedo = R.Rgb("3a2a40"); vortex.Steps = 32; vortex.Ambient = .7f; vortex.Emit = new Vector3(.05f, .008f, .01f);
        var lt = Light(sky, 70, .005f); lt.Background = (x, v) => Celestial(n, sky, x, v, spec.Period); lt.HazeMax = .4f; lt.BloomStrength = .5f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    /// <summary>A warm-lit cottage window: pane emission with a cross of mullions.</summary>
    static float WindowMask(float u, float v, float hw, float hh) =>
        MathF.Abs(u) < hw && MathF.Abs(v) < hh && MathF.Abs(u) > .025f && MathF.Abs(v) > .025f ? 1 : 0;

    static float FarHills(Noise n, float x, float z) => -3.6f + n.Fbm(new Vector3(x, 0, z), 3, 4, 1121) * .8f + R.Smooth(5, 13, z) * 2.8f;

    PaintedLayer Far()
    {
        var spec = new LayerSpec("far", .85f, 64, 32, -11, 19, -96);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3);
        int meadow = m.Ground("26324a", "44586a", "3a3036", 1122, "c8a0e0", .03f);
        int lake = m.Water("1a2440", .7f, .15f, 1123);
        int wall = m.Paint("b8a898", .7f, .2f, 1124);
        int roof = m.Tiles("3a3048", .12f, .6f, 1125);
        int lights = m.Glow("ffb060", 5, null, 0, 1126);
        int leaves = m.Foliage("141e26", "2e4048", .45f, 1127, "a890d0", .1f);
        int pod = m.Glow("8af0ff", 4, null, 0, 1128);
        sc.Add(new Vector3(0, -11, 0), new Vector3(32, 0, 16), (Vector3 p, out int mt) => { mt = meadow; return (p.Y - FarHills(n, p.X, p.Z)) * .6f; });
        sc.Add(new Vector3(0, -4, 0), new Vector3(32, -3.2f, 7), (Vector3 p, out int mt) => { mt = lake; return p.Y + 3.4f; });
        // Distant cottages on the slopes, windows aglow.
        var rnd = new Rand(1129);
        foreach (var (x, z) in new[] { (2.4f, 10f), (5.2f, 11.5f), (9.8f, 9.6f), (14.6f, 12f), (21.2f, 10.4f), (25.4f, 11.8f), (29.6f, 9.8f) })
        {
            float y = FarHills(n, x, z) - .1f, w = rnd.R(.35f, .55f), h = rnd.R(.4f, .6f);
            sc.Add(new Vector3(x - w - .2f, y - .2f, z - w - .2f), new Vector3(x + w + .2f, y + h + w * .9f + .2f, z + w + .2f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(x, y, z); var a = Acc.Start;
                a.Add(Sdf.Box(q - new Vector3(0, h * .5f, 0), new Vector3(w, h * .5f, w * .8f)), wall);
                a.Add(Sdf.GableX(q - new Vector3(0, h, 0), w * .9f, w * .8f, w * 1.1f), roof);
                a.Add(MathF.Max(Sdf.Box(new Vector3(R.Mod(q.X + w, w * .66f) - w * .33f, q.Y - h * .45f, q.Z + w * .8f), new Vector3(w * .1f, h * .14f, .02f)), MathF.Abs(q.X) - w * .8f), lights);
                mt = a.M; return a.D;
            });
        }
        // Lantern trees with glowing seed pods.
        foreach (var (x, z, s) in new[] { (7.6f, 8.6f, 1.2f), (12.2f, 7.4f, 1f), (18.4f, 9.2f, 1.4f), (27.6f, 7.8f, 1.1f) })
        {
            float y = FarHills(n, x, z) - .1f;
            var lobes = Kit3D.Crown(new Rand((int)(x * 37)), new Vector3(x, y + 1.6f * s, z), 1.8f * s, .9f * s, 8, .7f);
            var (bmin, bmax) = Kit3D.LobeBounds(lobes, .3f);
            var pods = new List<Vector3>(); var pr = new Rand((int)(x * 41));
            for (int k = 0; k < 7; k++) pods.Add(new Vector3(x + pr.R(-.8f, .8f) * s, y + pr.R(.9f, 1.3f) * s, z - pr.R(.3f, .6f) * s));
            sc.Add(Vector3.Min(bmin, new Vector3(x - .3f, y - .1f, z - .3f)), bmax, (Vector3 p, out int mt) =>
            {
                var a = Acc.Start;
                a.Add(Sdf.RoundCone(p, new Vector3(x, y, z), new Vector3(x, y + 1.4f * s, z), .1f * s, .05f * s), leaves);
                a.Add(Kit3D.Canopy(n, p, lobes, .2f * s, (int)x), leaves);
                foreach (var c in pods) a.Add(Sdf.Sphere(p - c, .06f * s), pod);
                mt = a.M; return a.D;
            });
        }
        var mist = sc.AddVolume(new Vector3(0, -4, 0), new Vector3(32, -1.5f, 12), q => MathF.Max(0, n.Fbm(q, 2.4f, 3, 1130) * .5f + .5f - .5f) * .9f * R.Smooth(-1.5f, -3.4f, q.Y));
        mist.Albedo = R.Rgb("c8a8c8"); mist.Steps = 20;
        var lt = Light(sky, 26, .011f); lt.HazeMax = .45f; lt.BloomStrength = .6f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    /// <summary>Striped tent cloth in two colours with a gold hem.</summary>
    static int Cloth(Scene sc, Vector3 axis, string aHex, string bHex, int stripes, int seed)
    {
        var n = sc.N; var a = R.Rgb(aHex); var b = R.Rgb(bHex); var gold = R.Rgb("d8a840");
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            var q = p - axis; float ang = MathF.Atan2(q.Z, q.X) / (MathF.PI * 2) * stripes;
            float st = R.Smooth(.45f, .55f, MathF.Abs(R.Mod(ang, 1) - .5f) * 2);
            float hem = R.Smooth(.08f, .05f, MathF.Abs(q.Y - 1.15f));
            float weave = n.N(p, .04f, seed) * .05f;
            s.Albedo = R.Mix(R.Mix(a, b, st), gold, hem) * (.95f + weave); s.Rough = .85f; s.Trans = .25f;
            s.N = Mats.Bump(v => n.Fbm(v, .3f, 2, seed + 1) * .02f, p, s.N, 1, .01f);
        });
    }

    /// <summary>A half-timbered cottage facing the viewer with its gable. Foot centre at (x, G, z).</summary>
    static void Cottage(Scene sc, Mats m, float x, float z, float w, float h, float rh, int seed)
    {
        var n = sc.N; float d = 1.3f;
        int plaster = m.Paint("d6c6ae", .8f, .25f, seed);
        int timber = m.Wood("2a1c14", "4a3424", .8f, true, seed + 1);
        int roof = m.Tiles("4a3a52", .14f, .65f, seed + 2);
        int stone = m.Rock("4a4248", "7a7078", .4f, .8f, .4f, seed + 3);
        int glow = m.Glow("ffb468", 5.5f, "3a2414", .05f, seed + 4);
        var o = new Vector3(x, G, z); float chimneyTop = h + rh * 1.2f + .2f;
        sc.Add(new Vector3(x - w - .6f, G - .1f, z - d - .6f), new Vector3(x + w + .6f, G + h + rh + 1.4f, z + d + .6f), (Vector3 p, out int mt) =>
        {
            var q = p - o; var a = Acc.Start;
            float body = Sdf.Box(q - new Vector3(0, h * .5f, 0), new Vector3(w, h * .5f, d));
            float gable = Sdf.GableZ(q - new Vector3(0, h, 0), w, rh * w / (w + .3f), d);
            float house = MathF.Min(body, gable);
            // Windows and door cut into the front face.
            var wq = new Vector3(MathF.Abs(q.X) - w * .55f, q.Y - h * .5f, q.Z + d);
            float win = Sdf.Box(wq, new Vector3(.28f, .32f, .1f));
            var gq = new Vector3(q.X, q.Y - h - rh * .32f, q.Z + d);
            float gwin = Sdf.Box(gq, new Vector3(.2f, .22f, .1f));
            float door = Sdf.Box(new Vector3(q.X, q.Y - .55f, q.Z + d), new Vector3(.26f, .55f, .1f));
            float holes = MathF.Min(MathF.Min(win, gwin), door);
            house = MathF.Max(house, -holes);
            // Timber framing proud of the plaster on the front.
            float fx = MathF.Abs(q.X), fz = q.Z + d + .02f;
            float posts = MathF.Min(MathF.Abs(fx - w + .06f), MathF.Abs(fx - w * .2f)) - .06f;
            float rails = MathF.Min(MathF.Abs(q.Y - h + .06f), MathF.Abs(q.Y - .12f)) - .06f;
            float brace = MathF.Abs((fx - w * .2f) * h * .9f - (q.Y - .12f) * (w * .8f)) / MathF.Sqrt(h * h * .81f + w * w * .64f) - .05f;
            brace = MathF.Max(brace, MathF.Max(-(fx - w * .2f), fx - w + .06f));
            float slope = (fx * rh + (q.Y - h) * (w + .3f) - (w + .3f) * rh * w / (w + .3f)) / MathF.Sqrt(rh * rh + (w + .3f) * (w + .3f));
            float king = MathF.Max(MathF.Abs(q.X) - .06f, MathF.Max(h - q.Y, q.Y - h - rh));
            float frame2d = MathF.Min(MathF.Min(MathF.Max(MathF.Min(posts, MathF.Min(rails, brace)), q.Y - h), MathF.Max(MathF.Abs(slope + .06f) - .06f, h - q.Y)), king);
            float frame = MathF.Max(MathF.Max(frame2d, MathF.Abs(fz) - .04f), MathF.Max(house - .04f, -holes - .04f));
            a.Add(house + n.Fbm(p, .2f, 3, seed + 5) * .01f, plaster);
            a.Add(MathF.Max(frame, -holes), timber);
            // Glowing panes set back with mullions, and the door left ajar with light behind it.
            float pane = MathF.Min(Sdf.Box(wq - new Vector3(0, 0, .06f), new Vector3(.28f, .32f, .02f)), Sdf.Box(gq - new Vector3(0, 0, .06f), new Vector3(.2f, .22f, .02f)));
            a.Add(pane, glow);
            float mull = MathF.Min(Sdf.Box(wq - new Vector3(0, 0, .03f), new Vector3(.025f, .32f, .025f)), Sdf.Box(wq - new Vector3(0, 0, .03f), new Vector3(.28f, .025f, .025f)));
            mull = MathF.Min(mull, MathF.Min(Sdf.Box(gq - new Vector3(0, 0, .03f), new Vector3(.02f, .22f, .02f)), Sdf.Box(gq - new Vector3(0, 0, .03f), new Vector3(.2f, .02f, .02f))));
            a.Add(mull, timber);
            a.Add(Sdf.Box(new Vector3(q.X, q.Y - .55f, q.Z + d - .08f), new Vector3(.26f, .55f, .02f)), glow);
            // Overhanging roof shell, stone footing and a chimney.
            float shell = MathF.Max(Sdf.GableZ(q - new Vector3(0, h - .05f, 0), w + .3f, rh, d + .35f), -Sdf.GableZ(q - new Vector3(0, h - .25f, 0), w + .3f, rh, d + .6f));
            a.Add(shell + n.Fbm(p, .2f, 2, seed + 6) * .015f, roof);
            a.Add(Sdf.Box(q - new Vector3(0, .1f, 0), new Vector3(w + .06f, .14f, d + .06f)) + n.Fbm(p, .12f, 3, seed + 7) * .02f, stone);
            a.Add(Sdf.Box(q - new Vector3(w * .5f, h + rh * .7f, d * .3f), new Vector3(.18f, rh * .5f + .2f, .18f)), stone);
            mt = a.M; return a.D;
        });
        var smoke = sc.AddVolume(new Vector3(x + w * .5f - .8f, G + chimneyTop, z + d * .3f - .8f), new Vector3(x + w * .5f + 1.8f, G + chimneyTop + 5, z + d * .3f + .8f), q =>
        {
            var s = q - new Vector3(x + w * .5f, G + chimneyTop, z + d * .3f);
            float r = .12f + s.Y * .12f, drift = s.Y * s.Y * .06f;
            float plume = R.Smooth(r, 0, MathF.Sqrt((s.X - drift) * (s.X - drift) + s.Z * s.Z));
            return plume * (n.Fbm(q * new Vector3(1, .6f, 1), .5f, 3, seed + 8) * .5f + .6f) * R.Smooth(5, 2, s.Y) * 1.2f;
        });
        smoke.Albedo = R.Rgb("b0a4b4"); smoke.Steps = 20;
    }

    /// <summary>A bell tent of a refugee god. Foot centre at (x, G, z).</summary>
    static void Tent(Scene sc, Mats m, float x, float z, float r, string aHex, string bHex, string flagHex, int seed)
    {
        var n = sc.N; var o = new Vector3(x, G, z);
        int cloth = Cloth(sc, o, aHex, bHex, 12, seed);
        int pole = m.Wood("3a2a1c", "6a5038", .7f, true, seed + 1);
        int glow = m.Glow("ffb468", 4.5f, "3a2418", .05f, seed + 2);
        int flag = m.Paint(flagHex, .7f, .1f, seed + 3);
        float wallH = 1.15f, apex = wallH + r * 1.1f;
        sc.Add(new Vector3(x - r - .3f, G - .1f, z - r - .3f), new Vector3(x + r + .9f, G + apex + .9f, z + r + .3f), (Vector3 p, out int mt) =>
        {
            var q = p - o; var a = Acc.Start;
            float rr = MathF.Sqrt(q.X * q.X + q.Z * q.Z);
            float sag = MathF.Sin(MathF.Atan2(q.Z, q.X) * 12) * .025f;
            float wallD = MathF.Max(MathF.Abs(rr - r + sag) - .03f, MathF.Max(-q.Y, q.Y - wallH));
            float cone = MathF.Max(MathF.Abs(Sdf.ConeY(q - new Vector3(0, wallH + (apex - wallH) * .5f, 0), (apex - wallH) * .5f, r + .08f, .04f) + sag) - .03f, -(q.Y - wallH + .02f));
            // A doorway with its flaps tied back.
            float door = MathF.Max(MathF.Abs(q.X) - .32f + q.Y * .12f, MathF.Max(q.Z, q.Y - .95f));
            float shell = MathF.Max(MathF.Min(wallD, cone), -door);
            a.Add(shell, cloth);
            // Lamplight filling the tent, seen through the doorway.
            a.Add(Sdf.CylinderY(q - new Vector3(0, .45f, 0), r - .12f, .45f), glow);
            a.Add(Sdf.CylinderY(q - new Vector3(0, (apex + .7f) * .5f, 0), .035f, (apex + .7f) * .5f), pole);
            // Pennant flying from the pole top.
            var fq = q - new Vector3(.04f, apex + .5f, 0);
            float pen = MathF.Max(MathF.Max(Sdf.Box(new Vector3(fq.X - .35f, fq.Y + MathF.Sin(fq.X * 6) * .04f, fq.Z), new Vector3(.35f, .12f - fq.X * .14f, .01f)), -fq.X), fq.X - .7f);
            a.Add(pen, flag);
            mt = a.M; return a.D;
        });
    }

    /// <summary>Rope sagging between two posts with paper lanterns hung along it.</summary>
    static void LanternString(Scene sc, int rope, int[] lamps, Vector3 a0, Vector3 b0, float sag, int count)
    {
        var lan = new List<(Vector3 c, int mat)>();
        for (int k = 0; k < count; k++)
        {
            float t = (k + .5f) / count;
            lan.Add((Vector3.Lerp(a0, b0, t) - new Vector3(0, sag * 4 * t * (1 - t) + .22f, 0), lamps[k % lamps.Length]));
        }
        var min = Vector3.Min(a0, b0) - new Vector3(.2f, sag + .5f, .2f); var max = Vector3.Max(a0, b0) + new Vector3(.2f, .2f, .2f);
        sc.Add(min, max, (Vector3 p, out int mt) =>
        {
            var acc = Acc.Start;
            float t = R.Clamp01((p.X - a0.X) / (b0.X - a0.X));
            float cy = R.Mix(a0.Y, b0.Y, t) - sag * 4 * t * (1 - t), cz = R.Mix(a0.Z, b0.Z, t);
            float slope = (b0.Y - a0.Y) / (b0.X - a0.X) - sag * 4 * (1 - 2 * t) / (b0.X - a0.X);
            acc.Add(MathF.Sqrt(MathF.Pow((p.Y - cy) / MathF.Sqrt(1 + slope * slope), 2) + (p.Z - cz) * (p.Z - cz)) - .012f, rope);
            foreach (var (c, mat) in lan)
            {
                var q = p - c; if (MathF.Abs(q.X) > .3f) continue;
                acc.Add(Sdf.Ellipsoid(q, new Vector3(.12f, .15f, .12f)), mat);
                acc.Add(Sdf.CylinderY(q - new Vector3(0, .13f, 0), .006f, .1f), rope);
            }
            mt = acc.M; return acc.D;
        });
    }

    PaintedLayer Mid()
    {
        var spec = new LayerSpec("mid", .65f, 96, 36, -9.5f, 21, -95);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3.5f);
        int grass = m.Ground("26363e", "4a6860", "4a3a34", 1141, "d0b0f0", .05f);
        int meadow = m.Glyphs(grass, "8af0ff", 2.6f, p =>
        {
            var c = n.Voronoi(p, .35f, 1142);
            return R.Hash01((int)(c.Id & 0xffff), 0, 0, 1143) > .86f ? R.Smooth(.05f, .02f, c.F1) : 0;
        });
        int path = m.Rock("5a4c48", "8c7c70", .45f, .85f, .3f, 1144);
        sc.Add(new Vector3(0, -6, 1.5f), new Vector3(36, -.6f, 15), (Vector3 p, out int mt) =>
        {
            float top = G + n.Fbm(new Vector3(p.X, 0, p.Z), 1.4f, 4, 1145) * .1f + R.Smooth(10, 15, p.Z) * 1.4f;
            mt = p.Z < 3.6f + n.Fbm(p, .8f, 2, 1146) * .4f ? path : meadow;
            return MathF.Max(p.Y - top, 2.2f - p.Z) * .8f;
        });
        Cottage(sc, m, 4.4f, 8.6f, 1.7f, 2.3f, 1.7f, 1150);
        Cottage(sc, m, 29.4f, 8.2f, 2f, 2.7f, 2f, 1160);
        Tent(sc, m, 10.6f, 6.8f, 1.5f, "ece6da", "3a6aa8", "e8c040", 1170);   // Olympian white and blue
        Tent(sc, m, 22.8f, 6.4f, 1.3f, "2a4a8a", "e0b040", "f0f0f0", 1175);   // Egyptian lapis and gold
        Tent(sc, m, 34f, 7.6f, 1.2f, "a8201a", "e8b040", "3a8a5a", 1180);     // Chinese vermilion
        // The great lantern tree in the village square.
        int bark = m.Wood("2a201c", "54423a", .9f, true, 1185);
        int leaves = m.Foliage("142226", "34504a", .5f, 1186, "c8a0f0", .14f);
        int lampWarm = m.Glow("ffb060", 6, "5a3414", .05f, 1187);
        int lampCyan = m.Glow("90f0ff", 5, "1a3a44", .05f, 1188);
        int lampRose = m.Glow("ff90b0", 5, "4a1a2a", .05f, 1189);
        var rnd = new Rand(1190); var lobes = Kit3D.Crown(rnd, new Vector3(17, G + 4.8f, 10), 6.4f, 2.6f, 18, .4f);
        var (lmin, lmax) = Kit3D.LobeBounds(lobes, .6f);
        var limbs = new[] { (new Vector3(0, 2.4f, 0), new Vector3(-2.4f, 4.2f, 0), .22f), (new Vector3(0, 2.6f, 0), new Vector3(2.6f, 4.4f, .2f), .22f), (new Vector3(0, 3, 0), new Vector3(0, 5, -.4f), .2f) };
        var hanging = new List<(Vector3 c, int mat)>(); var hr = new Rand(1191);
        for (int k = 0; k < 16; k++) hanging.Add((new Vector3(17 + hr.R(-3, 3), G + hr.R(2.6f, 3.6f), 10 - hr.R(1, 2.2f)), k % 3 == 0 ? lampCyan : lampWarm));
        sc.Add(Vector3.Min(lmin, new Vector3(13.5f, G - .1f, 8.5f)), lmax, (Vector3 p, out int mt) =>
        {
            var q = p - new Vector3(17, G, 10); var a = Acc.Start;
            a.Add(Kit3D.Trunk(q, 3.4f, .55f, limbs) + n.Fbm(p * new Vector3(1, .4f, 1), .3f, 3, 1192) * .05f, bark);
            a.Add(Kit3D.Canopy(n, p, lobes, .35f, 1193), leaves);
            foreach (var (c, mat) in hanging)
            {
                var l = p - c; if (MathF.Abs(l.X) > .3f) continue;
                a.Add(Sdf.Ellipsoid(l, new Vector3(.1f, .13f, .1f)), mat);
                a.Add(Sdf.CylinderY(l - new Vector3(0, .4f, 0), .006f, .3f), bark);
            }
            mt = a.M; return a.D;
        });
        // Lantern posts with strings of paper lanterns between them.
        int post = m.Wood("2a1e16", "4a3626", .8f, true, 1194);
        var postsX = new[] { 1.6f, 7.4f, 13.6f, 20.4f, 26.2f, 32f };
        foreach (float px in postsX)
            sc.Add(new Vector3(px - .3f, G - .1f, 4.7f), new Vector3(px + .3f, G + 3.3f, 5.3f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(px, G, 5); var a = Acc.Start;
                a.Add(Sdf.Box(q - new Vector3(0, 1.5f, 0), new Vector3(.06f, 1.5f, .06f)), post);
                a.Add(Sdf.Box(q - new Vector3(0, 2.9f, 0), new Vector3(.22f, .03f, .05f)), post);
                a.Add(Sdf.Ellipsoid(q - new Vector3(0, 3.1f, 0), new Vector3(.13f, .17f, .13f)), lampWarm);
                mt = a.M; return a.D;
            });
        for (int k = 0; k < postsX.Length; k++)
        {
            float x0 = postsX[k], x1 = k + 1 < postsX.Length ? postsX[k + 1] : postsX[0] + 36;
            LanternString(sc, post, new[] { lampWarm, lampRose, lampWarm, lampCyan }, new Vector3(x0 + .2f, G + 2.86f, 5), new Vector3(x1 - .2f, G + 2.86f, 5), .55f, 6);
        }
        // Fireflies drifting over the meadow.
        var ff = new Rand(1195); int fly = m.Glow("d8ff9a", 7, null, 0, 1196);
        for (int k = 0; k < 40; k++)
        {
            var c = new Vector3(ff.R(0, 36), G + ff.R(.4f, 4.6f), ff.R(3.4f, 9));
            sc.Add(c - new Vector3(.05f), c + new Vector3(.05f), (Vector3 p, out int mt) => { mt = fly; return Sdf.Sphere(p - c, .025f); });
        }
        var lt = Light(sky, 6, .007f); lt.HazeMax = .2f; lt.BloomStrength = .8f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Near()
    {
        // Bunting and lanterns strung across the top when the camera climbs.
        var spec = new LayerSpec("near", .4f, 96, 40, 8.6f, 5, -94);
        var sc = new Scene(spec.Period); var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(4);
        int rope = m.Wood("3a2a1c", "5a4430", .8f, false, 1201);
        int lampWarm = m.Glow("ffb060", 6, "5a3414", .05f, 1202);
        int lampCyan = m.Glow("90f0ff", 5, "1a3a44", .05f, 1203);
        var flags = new[] { m.Paint("c83a3a", .7f, .1f, 1204), m.Paint("e8c040", .7f, .1f, 1205), m.Paint("3a7ac8", .7f, .1f, 1206), m.Paint("5aa86a", .7f, .1f, 1207), m.Paint("f0ece4", .7f, .1f, 1208) };
        foreach (var (x0, x1, y, z, sag) in new[] { (-2f, 12f, 13.4f, 1.6f, 1.8f), (11f, 26f, 13.6f, 2.4f, 2.2f), (25f, 38f, 13.2f, 1.8f, 1.6f) })
        {
            LanternString(sc, rope, new[] { lampWarm, lampCyan }, new Vector3(x0, y, z), new Vector3(x1, y, z), sag * .7f, 5);
            float yb = y - .3f, sb = sag;
            sc.Add(new Vector3(x0 - .2f, yb - sb - .6f, z - .4f), new Vector3(x1 + .2f, yb + .2f, z - .1f), (Vector3 p, out int mt) =>
            {
                float t = R.Clamp01((p.X - x0) / (x1 - x0)), cy = yb - sb * 4 * t * (1 - t);
                float ropeD = MathF.Sqrt((p.Y - cy) * (p.Y - cy) + (p.Z - z + .25f) * (p.Z - z + .25f)) - .01f;
                float cell = R.Mod(p.X - x0, .45f) - .225f; int id = (int)MathF.Floor((p.X - x0) / .45f);
                float flag = MathF.Max(Kit3D.Triangle(cell, p.Y - cy + .18f, .17f, true), MathF.Abs(p.Z - z + .25f) - .008f);
                var acc = Acc.Start; acc.Add(ropeD, rope); acc.Add(flag, flags[((id % 5) + 5) % 5]);
                mt = acc.M; return acc.D;
            });
        }
        var lt = Light(sky, 2, .01f); lt.HazeMax = .15f; lt.BloomStrength = .7f;
        return Bake.Layer(spec, sc, lt, cam);
    }
}
