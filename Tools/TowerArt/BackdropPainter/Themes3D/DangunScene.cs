using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>Taebaeksan in morning mist: ridge after ridge fading like ink wash, a hermitage with sweeping tiled
/// eaves and a stone pagoda on the far crest, and in front the sacred sindan tree hung with five-colour cloths and
/// a straw geumjul, a stone altar, a pair of jangseung, a dolmen, stone cairns and the bear's cave.</summary>
public sealed class DangunScene : ThemePainter
{
    public override string Id => "dangun";

    static readonly Vector3 SunDir = Vector3.Normalize(new Vector3(-.55f, .55f, -.45f));

    static Sky MakeSky(float period) => new(period, (-11, "dcd6c8"), (-3, "e2dccc"), (0, "d8d6cc"), (3, "c2cad0"), (6, "a2b2c0"), (10, "8a9cb0"))
    {
        SunX = 6.5f, SunY = 4.6f, SunRadius = .55f, HaloRadius = 4, SunColor = new Vector3(14, 12.5f, 10), HaloColor = new Vector3(.5f, .45f, .35f),
        CirrusAmount = .35f, CirrusLow = 3, CirrusHigh = 10, CirrusColor = new Vector3(1.1f, 1.08f, 1.02f),
    };

    static Lighting Light(Sky sky, float distance, float haze) => new()
    {
        SunDir = SunDir, Sun = new Vector3(2.9f, 2.7f, 2.3f),
        SkyZenith = R.Rgb("9aaec4") * .95f, SkyHorizon = R.Rgb("d4d0c4") * .9f, Bounce = R.Rgb("8a8a6c") * .45f, Ambient = 1,
        Haze = Bake.HazeFrom(sky, .05f), Distance = distance, HazeDensity = haze, Exposure = 1.05f, ShadowK = 7, Saturation = .95f,
    };

    public static Lighting TerrainLight()
    {
        var l = Light(MakeSky(12), 0, 0); l.HazeMax = 0; l.Exposure = 1f; l.ShadowReach = 3; l.AoScale = .5f;
        return l;
    }

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return SkyLayer();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    /// <summary>Granite ridges whose crests are furred with pine forest.</summary>
    static int Ridge(Scene sc, string darkHex, string lightHex, int seed)
    {
        var n = sc.N; var d = R.Rgb(darkHex); var l = R.Rgb(lightHex); var forest = R.Rgb("3a4a38");
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float t = n.Fbm(p, 1, 5, seed) * .5f + .5f;
            float trees = R.Smooth(.4f, .7f, s.N.Y + n.Fbm(p, .5f, 4, seed + 1) * .3f);
            s.Albedo = R.Mix(R.Mix(d, l, t), forest * (.8f + t * .4f), trees * .7f);
            s.Rough = .9f; s.Trans = trees * .2f;
            s.N = Mats.Bump(q => n.Fbm(q, .25f, 4, seed + 2) * .05f, p, s.N, 1, .02f);
        });
    }

    /// <summary>A Korean red pine: a twisting reddish trunk and flat, layered needle clouds.</summary>
    static Shape RedPine(Noise n, int bark, int needles, Vector3 foot, float h, float lean, int seed)
    {
        var rnd = new Rand(seed); var pts = new List<Vector3> { foot };
        for (int k = 1; k <= 5; k++) pts.Add(foot + new Vector3(lean * h * (k / 5f) * (k / 5f) + rnd.R(-.15f, .15f) * h * .1f, h * k / 5f * .82f, rnd.R(-.1f, .1f)));
        var pads = new List<(Vector3, float)>();
        for (int k = 0; k < 5; k++)
        {
            var c = pts[3 + (k % 3)] + new Vector3(rnd.R(-.5f, .5f) * h * .35f, rnd.R(-.05f, .15f) * h, rnd.R(-.2f, .2f) * h * .2f);
            pads.Add((c, h * rnd.R(.12f, .2f)));
        }
        var padArr = pads.ToArray();
        return (Vector3 p, out int m) =>
        {
            float t = 1e9f;
            for (int k = 1; k < pts.Count; k++) t = R.SMin(t, Sdf.RoundCone(p, pts[k - 1], pts[k], h * (.05f - k * .006f), h * (.045f - k * .006f)), .05f);
            float c = 1e9f;
            foreach (var (pc, r) in padArr)
            {
                var q = p - pc; q.Y *= 2.4f;
                c = R.SMin(c, q.Length() / 2.4f - r * .5f, r * .25f);
            }
            if (c < .3f) c = (c + n.Fbm(p, .12f, 4, seed) * h * .03f) * .7f;
            if (c < t) { m = needles; return c; }
            m = bark; return t;
        };
    }

    /// <summary>Front view of a hanok roof: ridge, concave slopes and lifted eave corners, extruded in depth.</summary>
    static float Giwa(Vector3 q, float halfW, float ridgeY, float drop, float lift, float thick, float halfD)
    {
        float u = MathF.Min(1, MathF.Abs(q.X) / halfW);
        float top = ridgeY - drop * MathF.Pow(u, .85f) + lift * MathF.Pow(u, 6);
        float d = MathF.Max(MathF.Max(q.Y - top, top - thick - q.Y), MathF.Max(MathF.Abs(q.X) - halfW, MathF.Abs(q.Z) - halfD));
        return d * .6f;
    }

    PaintedLayer SkyLayer()
    {
        var spec = new LayerSpec("sky", .95f, 128, 30, -11, 21, -97);
        var sc = new Scene(spec.Period); var n = sc.N; var sky = MakeSky(spec.Period); var cam = new Camera(2.5f);
        // Three ranges, each fainter, separated by banks of mist.
        var ranges = new[] { (z: 58f, y: -3.6f, amp: 6.2f, seed: 201, dark: "8e98a2", light: "b8c0c6"), (z: 44f, y: -4.6f, amp: 4.6f, seed: 202, dark: "6e7a80", light: "a0a8aa"), (z: 32f, y: -5.6f, amp: 3.4f, seed: 203, dark: "4e5a58", light: "86908a") };
        foreach (var r in ranges)
        {
            int mat = Ridge(sc, r.dark, r.light, r.seed);
            var peaks = new[] { (3f + r.seed % 5, r.amp, 3.6f), (11f + r.seed % 3, r.amp * .7f, 3), (18.5f, r.amp * 1.1f, 4), (25f + r.seed % 4, r.amp * .8f, 3.2f) };
            float z0 = r.z, y0 = r.y; int sd = r.seed;
            sc.Add(new Vector3(0, -11, z0), new Vector3(30, y0 + r.amp * 1.8f + 2, z0 + 8), (Vector3 p, out int m) =>
            {
                m = mat; float rise = R.Smooth(z0, z0 + 4, p.Z);
                float h = R.Mix(-11, Kit3D.Mountain(n, 30, p.X, p.Z, y0, peaks, .7f, sd) + n.Fbm(p, .12f, 3, sd + 5) * .12f, rise);
                return (p.Y - h) * .3f;
            });
            var mist = sc.AddVolume(new Vector3(0, -11, z0 - 7), new Vector3(30, y0 + 1.2f, z0 - 1), Kit3D.CloudSea(n, y0 - 1.6f, 1.8f, .9f, sd + 9, 3.4f));
            mist.Coarse = Kit3D.CloudSeaCoarse(n, y0 - 1.6f, 1.8f, .9f, sd + 9, 3.4f); mist.Albedo = R.Rgb("f2efe8"); mist.Steps = 32; mist.Ambient = 1.3f;
        }
        var lt = Light(sky, 60, .012f); lt.Background = sky.At; lt.HazeMax = .8f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Far()
    {
        var spec = new LayerSpec("far", .85f, 128, 32, -11, 18, -96);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3);
        int ridge = Ridge(sc, "4a524c", "7c827a", 211);
        int bark = m.Wood("5a2e1e", "a0583a", .85f, true, 212);
        int needles = m.Foliage("1e3020", "4e6a40", .3f, 213);
        int wood = m.Wood("4a2e1e", "7a5236", .7f, true, 214);
        int plaster = m.Paint("e8e2d4", .8f, .2f, 215);
        int tiles = m.Tiles("3a3c40", .07f, .7f, 216);
        int granite = m.Rock("6a6a64", "a8a49a", .8f, .85f, .5f, 217);
        sc.Add(new Vector3(0, -11, 6), new Vector3(32, 1.6f, 16), (Vector3 p, out int mt) =>
        {
            mt = ridge;
            float h = -2.6f + MathF.Exp(-MathF.Pow(R.Wrap(p.X, 20, 32) / 7, 2)) * 2.4f + (n.Fbm(new Vector3(p.X, 0, p.Z), 4, 4, 218) * .5f + .5f) * 1.2f + n.Fbm(p, .1f, 3, 219) * .08f;
            h = R.Mix(-11, h, R.Smooth(6, 10, p.Z));
            return (p.Y - h) * .35f;
        });
        // Hermitage on the crest: stone base, plastered timber walls, sweeping giwa roof.
        sc.Add(new Vector3(17.6f, -.6f, 10), new Vector3(22.4f, 2.4f, 12.6f), (Vector3 p, out int mt) =>
        {
            var q = p - new Vector3(20, -.25f, 11.3f); var a = Acc.Start;
            a.Add(Sdf.Box(q - new Vector3(0, .1f, 0), new Vector3(1.5f, .12f, .9f)), granite);
            float wall = Sdf.Box(q - new Vector3(0, .62f, 0), new Vector3(1.25f, .42f, .7f));
            float posts = Sdf.Box(new Vector3(Sdf.RepeatLimited(q.X, .62f, 2), q.Y - .62f, q.Z + .71f), new Vector3(.05f, .42f, .03f));
            a.Add(wall, plaster); a.Add(posts, wood);
            a.Add(Giwa(q - new Vector3(0, 1.05f, 0), 1.95f, .9f, .62f, .2f, .14f, 1.05f), tiles);
            mt = a.M; return a.D;
        });
        // Stone pagoda: stacked tiers with upturned corners.
        sc.Add(new Vector3(23.6f, -.6f, 9.6f), new Vector3(25.2f, 3.4f, 11), (Vector3 p, out int mt) =>
        {
            mt = granite; var q = p - new Vector3(24.4f, -.2f, 10.3f); float d = 1e9f;
            for (int k = 0; k < 5; k++)
            {
                float y = .35f + k * .52f, w = .42f - k * .05f;
                d = MathF.Min(d, Sdf.Box(q - new Vector3(0, y, 0), new Vector3(w * .55f, .2f, w * .55f)));
                d = MathF.Min(d, Giwa(q - new Vector3(0, y + .22f, 0), w, .1f, .05f, .05f, .07f, w));
            }
            return MathF.Min(d, Sdf.Box(q - new Vector3(0, .12f, 0), new Vector3(.5f, .12f, .5f)));
        });
        foreach (var (x, z, h, lean) in new[] { (14.6f, 9.4f, 2.2f, .25f), (16.2f, 10.2f, 1.7f, -.2f), (26.6f, 9.6f, 2f, .3f), (28.2f, 10.4f, 2.4f, -.25f), (5.4f, 11, 1.8f, .2f), (7f, 10.6f, 1.5f, -.3f) })
        {
            float groundY = -2.6f + MathF.Exp(-MathF.Pow(R.Wrap(x, 20, 32) / 7, 2)) * 2.4f + .45f;
            var pine = RedPine(n, bark, needles, new Vector3(x, groundY - .1f, z), h, lean, (int)(x * 31));
            sc.Add(new Vector3(x - h * .8f, groundY - .3f, z - 1), new Vector3(x + h * .8f, groundY + h * 1.1f, z + 1), (Vector3 p, out int mt) => pine(p, out mt));
        }
        var mist = sc.AddVolume(new Vector3(0, -11, 0), new Vector3(32, -.8f, 14), Kit3D.CloudSea(n, -4.2f, 2.4f, 1.1f, 220, 3));
        mist.Coarse = Kit3D.CloudSeaCoarse(n, -4.2f, 2.4f, 1.1f, 220, 3); mist.Albedo = R.Rgb("f4f2ea"); mist.Steps = 36; mist.Ambient = 1.3f;
        var lt = Light(sky, 24, .022f); lt.HazeMax = .55f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Mid()
    {
        var spec = new LayerSpec("mid", .65f, 192, 36, -9.5f, 21, -95);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3.5f);
        int grass = m.Ground("4a5a30", "86945a", "6a5a42", 231);
        int bark = m.Rock("3a2e24", "6a5442", .5f, .95f, .25f, 232, "4e5e34", .3f);
        int leaves = m.Foliage("2a3c22", "6e8248", .4f, 233);
        int pineBark = m.Wood("5a2e1e", "a0583a", .85f, true, 234);
        int needles = m.Foliage("1e3020", "52703e", .3f, 235);
        int granite = m.Rock("5e5c56", "a29e94", .9f, .85f, .55f, 236, "56683a", .35f);
        int straw = m.Wood("8a7444", "c8b07a", .9f, false, 237);
        int paper = m.Paint("f2eee2", .8f, .1f, 238);
        int wood = m.Wood("4a3624", "8a6a48", .8f, true, 239);
        int[] cloth = { m.Paint("2a58a8", .7f, .2f, 240), m.Paint("c22a2a", .7f, .2f, 241), m.Paint("e8c22a", .7f, .2f, 242), m.Paint("f0eee8", .7f, .2f, 243), m.Paint("242424", .7f, .2f, 244) };
        int paintRed = m.Paint("b0302a", .6f, .3f, 245), paintBlack = m.Paint("1e1a16", .6f, .3f, 246), paintWhite = m.Paint("ece6d8", .6f, .3f, 247);
        // Meadow behind the walkway, rising to a rocky hill with the bear's cave on the right.
        sc.Add(new Vector3(0, -6, 1.5f), new Vector3(36, 2.6f, 15), (Vector3 p, out int mt) =>
        {
            mt = grass;
            float hill = MathF.Exp(-MathF.Pow(R.Wrap(p.X, 31, 36) / 4.2f, 2)) * 3.8f * R.Smooth(3, 9, p.Z);
            float top = -2.66f + n.Fbm(new Vector3(p.X, 0, p.Z), 1.4f, 4, 248) * .12f + R.Smooth(5, 14, p.Z) * .7f + hill * .3f;
            return MathF.Max(p.Y - top, 2.2f - p.Z) * .8f;
        });
        sc.Add(new Vector3(27, -3, 5), new Vector3(36, 2.6f, 12), (Vector3 p, out int mt) =>
        {
            mt = granite; var q = p - new Vector3(31.4f, -2.7f, 8.6f);
            float hill = Sdf.Ellipsoid(q, new Vector3(4.2f, 4.6f, 3.4f)) + n.Fbm(p, .8f, 5, 249) * .5f;
            float cave = Sdf.Ellipsoid(q - new Vector3(-.4f, .6f, -3), new Vector3(1.1f, 1.4f, 2.6f)) + n.Fbm(p, .4f, 3, 250) * .15f;
            return Sdf.Subtract(hill, cave) * .7f;
        });
        // The sacred tree.
        var rnd = new Rand(251);
        var lobes = Kit3D.Crown(rnd, new Vector3(14.6f, 5.4f, 7.6f), 11, 3.6f, 22, .35f);
        var limbs = new[] { (new Vector3(0, 3.4f, 0), new Vector3(-3.6f, 6.6f, .3f), .32f), (new Vector3(0, 3.8f, 0), new Vector3(3.8f, 7, -.2f), .3f), (new Vector3(0, 5, 0), new Vector3(-1.2f, 8, .2f), .26f), (new Vector3(0, 4.4f, 0), new Vector3(5.6f, 5.6f, .4f), .2f), (new Vector3(0, 4, 0), new Vector3(-5.4f, 5f, .2f), .2f) };
        var (cmin, cmax) = Kit3D.LobeBounds(lobes, .8f);
        sc.Add(Vector3.Min(cmin, new Vector3(6.6f, -2.9f, 4.6f)), Vector3.Max(cmax, new Vector3(22.6f, 9, 10.8f)), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start; var q = p - new Vector3(14.6f, -2.66f, 7.4f);
            float trunk = Kit3D.Trunk(q, 4.6f, .95f, limbs) + n.Fbm(p * new Vector3(1, .35f, 1), .4f, 4, 252) * .12f;
            for (int k = 0; k < 4; k++) { float an = k * 1.57f + .3f; var dr = new Vector3(MathF.Cos(an), 0, MathF.Sin(an)); trunk = R.SMin(trunk, Sdf.RoundCone(q, dr * .5f + new Vector3(0, .6f, 0), dr * 2 + new Vector3(0, -.1f, 0), .5f, .14f), .4f); }
            a.Add(trunk * .8f, bark);
            a.Add(Kit3D.Canopy(n, p, lobes, .55f, 253), leaves);
            // Geumjul: a straw rope round the trunk with white paper strips.
            a.Add(Sdf.TorusY(q - new Vector3(0, 1.5f, 0), 1.02f, .07f), straw);
            float strip = Sdf.Box(new Vector3(R.Mod(q.X + .2f, .4f) - .2f, q.Y - 1.28f, q.Z + 1.02f), new Vector3(.06f, .18f, .015f));
            if (MathF.Abs(q.X) < .9f) a.Add(strip, paper);
            // Five-colour cloths hanging from the limbs.
            foreach (var (s0, s1, r0) in limbs)
            {
                var dir = s1 - s0;
                for (int k = 2; k < 9; k++)
                {
                    var at = s0 + dir * (k / 9f); var cq = q - at - new Vector3(0, -.55f, -.05f);
                    float rib = Sdf.Box(cq, new Vector3(.05f, .55f + (k % 3) * .12f, .012f)) - .004f;
                    a.Add(rib, cloth[(k + (int)MathF.Abs(s1.X * 3)) % 5]);
                }
            }
            mt = a.M; return a.D;
        });
        // Stone altar.
        sc.Add(new Vector3(13.2f, -2.9f, 4.6f), new Vector3(16, -2, 5.8f), (Vector3 p, out int mt) =>
        {
            mt = granite; var q = p - new Vector3(14.6f, -2.66f, 5.2f);
            return MathF.Min(Sdf.RoundBox(q - new Vector3(0, .5f, 0), new Vector3(1.1f, .08f, .45f), .03f), Sdf.RoundBox(q - new Vector3(0, .22f, 0), new Vector3(.8f, .22f, .32f), .04f)) + n.Fbm(p, .2f, 3, 254) * .02f;
        });
        // Jangseung pair: carved posts with tall hats, painted brows, bulging eyes and toothy grins.
        foreach (var (x, gen) in new[] { (3.2f, 0), (4.6f, 1) })
        {
            sc.Add(new Vector3(x - .4f, -2.9f, 3.2f), new Vector3(x + .4f, .6f, 4.2f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(x, -2.66f, 3.7f); var a = Acc.Start;
                a.Add(Sdf.CylinderY(q - new Vector3(0, 1.2f, 0), .22f, 1.2f) + n.Fbm(p, .1f, 3, 255) * .01f, wood);
                if (gen == 0) a.Add(MathF.Min(Sdf.CylinderY(q - new Vector3(0, 2.62f, 0), .2f, .22f), Sdf.CylinderY(q - new Vector3(0, 2.42f, 0), .34f, .03f)), paintBlack);
                else a.Add(Sdf.ConeY(q - new Vector3(0, 2.6f, 0), .22f, .26f, .1f), paintBlack);
                // Facial features on the front face.
                if (q.Z < -.1f && q.Y > 1.4f && q.Y < 2.4f)
                {
                    float fy = q.Y - 1.9f; int feature = -1;
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        float ex = q.X - sx * .09f;
                        if (ex * ex + (fy - .12f) * (fy - .12f) < .004f) feature = 2;
                        if (MathF.Abs(fy - .25f + MathF.Abs(ex) * .5f) < .02f && MathF.Abs(ex) < .08f) feature = 1;
                    }
                    if (MathF.Abs(fy + .22f) < .045f && MathF.Abs(q.X) < .13f) feature = R.Fract(q.X * 22) < .5f ? 2 : 1;
                    if (feature >= 0) { float body = Sdf.CylinderY(q - new Vector3(0, 1.2f, 0), .222f, 1.2f); a.Add(body - .001f, feature == 1 ? paintBlack : feature == 2 ? paintWhite : paintRed); }
                }
                if (q.Z < -.1f && q.Y > .5f && q.Y < 1.35f && MathF.Abs(q.X) < .09f) a.Add(Sdf.CylinderY(q - new Vector3(0, 1.2f, 0), .222f, 1.2f) - .001f, paintBlack);
                mt = a.M; return a.D;
            });
        }
        // Dolmen and cairns.
        sc.Add(new Vector3(6, -2.9f, 4), new Vector3(9.6f, -.6f, 6.4f), (Vector3 p, out int mt) =>
        {
            mt = granite; var q = p - new Vector3(7.8f, -2.66f, 5.2f);
            float cap = Sdf.RoundBox(q - new Vector3(0, 1.55f, 0), new Vector3(1.5f, .2f, .95f), .15f);
            float legs = MathF.Min(Sdf.RoundBox(q - new Vector3(-.85f, .7f, 0), new Vector3(.22f, .7f, .55f), .1f), Sdf.RoundBox(q - new Vector3(.9f, .7f, 0), new Vector3(.2f, .7f, .5f), .1f));
            return MathF.Min(cap, legs) + n.Fbm(p, .35f, 4, 256) * .06f;
        });
        foreach (var (x, z, h) in new[] { (22.2f, 4.4f, 1.3f), (23.4f, 5.2f, .9f), (26.2f, 4.2f, 1.1f), (0.8f, 4.6f, 1f) })
        {
            int seed = (int)(x * 41);
            sc.Add(new Vector3(x - .6f, -2.9f, z - .6f), new Vector3(x + .6f, -2.6f + h + .2f, z + .6f), (Vector3 p, out int mt) =>
            {
                mt = granite; var q = p - new Vector3(x, -2.66f, z); float d = 1e9f; float y = 0; var rr = new Rand(seed);
                for (int k = 0; k < 7 && y < h; k++)
                {
                    float r = .38f * (1 - y / h * .7f) * rr.R(.85f, 1.1f);
                    d = MathF.Min(d, Sdf.Ellipsoid(q - new Vector3(rr.R(-.05f, .05f), y + r * .45f, 0), new Vector3(r, r * .5f, r * .85f)));
                    y += r * .8f;
                }
                return d + n.Fbm(p, .1f, 3, seed) * .015f;
            });
        }
        foreach (var (x, z, h, lean) in new[] { (19.6f, 6.4f, 5.4f, .3f), (0.6f, 7, 4.6f, -.3f), (25.2f, 7.6f, 4.2f, -.2f) })
        {
            var pine = RedPine(n, pineBark, needles, new Vector3(x, -2.7f, z), h, lean, (int)(x * 37));
            sc.Add(new Vector3(x - h * .7f, -2.9f, z - 1.4f), new Vector3(x + h * .7f, -2.6f + h * 1.05f, z + 1.4f), (Vector3 p, out int mt) => pine(p, out mt));
        }
        var mist = sc.AddVolume(new Vector3(0, -3.2f, 5), new Vector3(36, -1, 15), Kit3D.CloudSea(n, -3.1f, .6f, .3f, 257, 4));
        mist.Coarse = Kit3D.CloudSeaCoarse(n, -3.1f, .6f, .3f, 257, 4); mist.Albedo = R.Rgb("f6f4ec"); mist.Steps = 24; mist.Ambient = 1.3f;
        var lt = Light(sky, 5, .012f); lt.HazeMax = .25f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Near()
    {
        // Pine boughs reaching in from the top corners when the camera climbs.
        var spec = new LayerSpec("near", .4f, 192, 40, 8.6f, 5, -94);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(4);
        int bark = m.Wood("4a261a", "8a4a30", .85f, true, 261);
        int needles = m.Foliage("18281a", "425e36", .3f, 262);
        foreach (var (x, dir) in new[] { (2f, 1f), (24f, -1f) })
        {
            var rnd = new Rand((int)x); var pads = new List<(Vector3, float)>();
            for (int k = 0; k < 6; k++) pads.Add((new Vector3(x + dir * (1 + k * 1.1f), 10.4f + rnd.R(-.5f, .3f), 2 + rnd.R(-.3f, .3f)), rnd.R(.6f, 1f)));
            var arr = pads.ToArray();
            float x0 = MathF.Min(x - dir * 1.5f, x + dir * 8.5f), x1 = MathF.Max(x - dir * 1.5f, x + dir * 8.5f);
            sc.Add(new Vector3(x0, 8.6f, .5f), new Vector3(x1, 13.6f, 3.5f), (Vector3 p, out int mt) => Bough(p, out mt));
            float Bough(Vector3 p, out int mt)
            {
                float limb = Sdf.RoundCone(p, new Vector3(x - dir, 12.8f, 2), new Vector3(x + dir * 7, 10.6f, 2), .22f, .06f);
                float c = 1e9f;
                foreach (var (pc, r) in arr) { var q = p - pc; q.Y *= 2.2f; c = R.SMin(c, q.Length() / 2.2f - r * .5f, r * .3f); }
                if (c < .3f) c = (c + n.Fbm(p, .1f, 4, 263) * .06f) * .7f;
                if (c < limb) { mt = needles; return c; }
                mt = bark; return limb;
            }
        }
        var lt = Light(sky, 2, .01f); lt.HazeMax = .15f;
        return Bake.Layer(spec, sc, lt, cam);
    }
}
