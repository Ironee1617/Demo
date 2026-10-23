using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>Mount Olympus at golden hour, rendered with physical lighting: snow-capped summits above a sunlit sea
/// of cloud, an acropolis on limestone cliffs, and a marble temple with a ruined colonnade, cypresses, an olive
/// tree and burning bronze braziers on the terrace behind the walkway.</summary>
public sealed class OlympusScene : ThemePainter
{
    public override string Id => "olympus";
    public override bool OpenBelow => true;

    const float SunX = 9.6f, SunY = 2.4f;
    static readonly Vector3 SunDir = Vector3.Normalize(new Vector3(.66f, .4f, -.3f));

    static Sky MakeSky(float period) => new(period, (-11, "f0c090"), (-4, "f6c690"), (-1, "f0b894"), (1.5f, "d6b8b4"), (4, "8fb0dc"), (7, "5a8ed6"), (10, "3a6cc0"))
    { SunX = SunX, SunY = SunY, SunRadius = .5f, HaloRadius = 6, SunColor = new Vector3(40, 30, 18), HaloColor = new Vector3(1.5f, 1f, .55f), CirrusAmount = .8f, CirrusLow = 1.5f, CirrusHigh = 10 };

    static Lighting Light(Sky sky, float distance, float haze) => new()
    {
        SunDir = SunDir, Sun = new Vector3(4.4f, 3.25f, 2f),
        SkyZenith = R.Rgb("6688cc") * .95f, SkyHorizon = R.Rgb("dcae96") * .8f, Bounce = R.Rgb("b48e6c") * .45f, Ambient = .8f,
        Background = null, Haze = Bake.HazeFrom(sky), Distance = distance, HazeDensity = haze, Exposure = .95f, ShadowK = 14, Saturation = 1.08f,
    };

    /// <summary>The walkway's own light: the same golden sun and sky, without aerial haze.</summary>
    public static Lighting TerrainLight()
    {
        // Slightly whiter than the backdrop's golden hour so the walkway reads clearly under the characters.
        var l = Light(MakeSky(12), 0, 0); l.HazeMax = 0; l.Exposure = .8f; l.ShadowReach = 3; l.AoScale = .5f;
        l.Sun = new Vector3(3.5f, 3.1f, 2.5f); l.SkyHorizon = R.Rgb("c9c2c4") * .8f; l.Bounce = R.Rgb("9a8c80") * .4f;
        return l;
    }

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return SkyLayer();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    static int Alpine(Scene sc, float snowLine)
    {
        var n = sc.N; var rockA = R.Rgb("7e7270"); var rockB = R.Rgb("c2b0a4"); var snow = R.Rgb("fbf4ec"); var snowShade = R.Rgb("b9c4dc");
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float t = n.Fbm(p, 1.2f, 5, 41) * .5f + .5f;
            float strata = MathF.Sin(p.Y * 7 + n.Fbm(p, 2, 3, 42) * 3) * .5f + .5f;
            var rock = R.Mix(rockA, rockB, R.Clamp01(t * .7f + strata * .3f));
            float snowAmt = R.Smooth(snowLine - .5f, snowLine + .6f, p.Y + n.Fbm(p, .9f, 4, 43) * .9f) * R.Smooth(.25f, .6f, s.N.Y + n.N(p, .3f, 44) * .2f);
            s.Albedo = R.Mix(rock, R.Mix(snowShade, snow, R.Clamp01(s.N.Y)), snowAmt);
            s.Rough = R.Mix(.9f, .5f, snowAmt); s.Spec = .04f;
            s.N = Mats.Bump(q => n.Ridged(q, .6f, 4, 45) * .08f, p, s.N, 1, .02f);
        });
    }

    /// <summary>A Doric temple facing the viewer: stepped base, colonnade, entablature and pediment. Origin at the base centre front.</summary>
    static Shape Temple(int marble, int shadowMarble, float halfW, float depth, float colR, float colH, int columns, int gold = -1, int roof = -1)
    {
        float baseH = colR * 1.6f, spacing = (halfW * 2 - colR * 3) / (columns - 1);
        float entab = colH + baseH, archH = colR * 1.3f, friezeH = colR * 1.4f, cornH = colR * .6f;
        float roofY = entab + archH + friezeH + cornH;
        return (Vector3 q, out int m) =>
        {
            var a = Acc.Start;
            a.Add(Kit3D.Steps(q - new Vector3(0, 0, depth * .5f), halfW + colR * 1.2f, depth * .5f + colR * 1.2f, baseH, 3, colR * .4f), marble);
            // Front and back rows of columns: offset from the nearest column centre.
            float half = (columns - 1) * .5f;
            float k = MathF.Max(0, MathF.Min(columns - 1, MathF.Round(q.X / spacing + half)));
            float cx = q.X - (k - half) * spacing;
            float colFront = Kit3D.Column(new Vector3(cx, q.Y - baseH, q.Z - colR * 1.4f), colR, colH, 20, true);
            float colBack = Kit3D.Column(new Vector3(cx, q.Y - baseH, q.Z - depth + colR * 1.4f), colR, colH, 20, true);
            a.Add(MathF.Min(colFront, colBack), marble);
            // Cella wall set well back inside the peristyle, so the portico stays in deep shade.
            float cella = Sdf.Box(q - new Vector3(0, baseH + colH * .5f, depth * .62f), new Vector3(halfW - colR * 3.2f, colH * .5f, depth * .26f));
            float door = Sdf.Box(q - new Vector3(0, baseH + colH * .36f, depth * .36f), new Vector3(colR * 1.5f, colH * .36f, colR * 1.2f));
            a.Add(Sdf.Subtract(cella, door), shadowMarble);
            float arch = Sdf.Box(q - new Vector3(0, entab + archH * .5f, depth * .5f), new Vector3(halfW + colR * .3f, archH * .5f, depth * .5f + colR * .3f));
            // Frieze with triglyph grooves.
            float frieze = Sdf.Box(q - new Vector3(0, entab + archH + friezeH * .5f, depth * .5f), new Vector3(halfW + colR * .3f, friezeH * .5f, depth * .5f + colR * .3f));
            float tg = Sdf.Box(new Vector3(Sdf.RepeatLimited(q.X, spacing * .5f, columns), q.Y - (entab + archH + friezeH * .5f), q.Z + colR * .3f), new Vector3(colR * .06f, friezeH * .4f, colR * .08f));
            frieze = Sdf.Subtract(frieze, tg);
            float cornice = Sdf.Box(q - new Vector3(0, entab + archH + friezeH + cornH * .5f, depth * .5f), new Vector3(halfW + colR * .7f, cornH * .5f, depth * .5f + colR * .7f));
            a.Add(MathF.Min(MathF.Min(arch, frieze), cornice), marble);
            // Pediment facing the viewer: a low gable whose ridge runs back along z, with a recessed tympanum.
            float gW = halfW + colR * .7f, gH = colR * 3.1f, gL = depth * .5f + colR * .7f;
            var gq = q - new Vector3(0, roofY, depth * .5f);
            float gable = Sdf.GableZ(gq, gW, gH, gL);
            float recess = Sdf.GableZ(gq - new Vector3(0, colR * .3f, -gL), gW - colR * 1.1f, gH - colR * .85f, colR * .35f);
            float ped = Sdf.Subtract(gable, recess);
            // The sloping tops carry a skin of roof tiles.
            float skin = Sdf.Subtract(ped, Sdf.GableZ(gq + new Vector3(0, colR * .16f, 0), gW, gH, gL + 1));
            a.Add(ped, marble);
            if (roof >= 0) a.Add(skin - .002f, roof);
            a.Add(Sdf.GableZ(gq - new Vector3(0, colR * .3f, -gL + colR * .36f), gW - colR * 1.1f, gH - colR * .85f, colR * .02f), shadowMarble);
            if (gold >= 0) a.Add(Sdf.Sphere(new Vector3(MathF.Abs(gq.X) - gW + colR * .2f, gq.Y - colR * .35f, gq.Z + gL), colR * .26f), gold);
            m = a.M; return a.D;
        };
    }

    PaintedLayer SkyLayer()
    {
        var spec = new LayerSpec("sky", .95f, 48, 30, -11, 21, -97);
        var sc = new Scene(spec.Period); var n = sc.N; var sky = MakeSky(spec.Period); var cam = new Camera(2.5f);
        int alpine = Alpine(sc, 1.6f);
        var peaks = new[] { (24f, 8.2f, 3.4f), (20f, 5.4f, 2.4f), (28.6f, 4.2f, 2.4f), (4.6f, 4.4f, 3.2f), (11.8f, 2.6f, 2.4f), (15.4f, 1.8f, 2f) };
        sc.Add(new Vector3(0, -9, 30), new Vector3(30, 9.5f, 52), (Vector3 p, out int m) =>
        {
            m = alpine;
            float rise = R.Smooth(30, 40, p.Z);
            float h = R.Mix(-9, Kit3D.Mountain(n, 30, p.X, p.Z, -5.5f, peaks, .9f, 3), rise);
            return (p.Y - h) * .3f;
        });
        var sea = sc.AddVolume(new Vector3(0, -11, 4), new Vector3(30, -.6f, 30), Kit3D.CloudSea(n, -3.6f, 2.2f, 3.2f, 5));
        sea.Coarse = Kit3D.CloudSeaCoarse(n, -3.6f, 2.2f, 3.2f, 5); sea.Albedo = R.Rgb("fff4ec"); sea.Steps = 48;
        var tower = Kit3D.Cumulus(n, new[] { (new Vector3(1.8f, -1, 20), 3.2f), (new Vector3(3.2f, 1.8f, 20), 2.6f), (new Vector3(.4f, 1.2f, 21), 2.2f), (new Vector3(2.2f, 4, 20), 1.9f), (new Vector3(-1.4f, -.6f, 20), 2.4f) }, 2.6f, .55f, 9);
        var cu = sc.AddVolume(new Vector3(-4.5f, -4, 16), new Vector3(7.5f, 6.5f, 24), tower); cu.Albedo = R.Rgb("fff6f0"); cu.Steps = 40;
        var lt = Light(sky, 70, .009f); lt.Background = sky.At; lt.HazeMax = .7f; lt.Ambient = 1.1f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Far()
    {
        var spec = new LayerSpec("far", .85f, 64, 32, -11, 18, -96);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3);
        int lime = m.Rock("8c7c74", "dccab6", 1.4f, .9f, .5f, 61, "6d7a4e", .35f);
        int marble = m.Marble("f1e6d4", "a89a88", 1.2f, .5f, 62);
        int shade = m.Marble("cbbfae", "8a7e70", 1.2f, .5f, 63);
        int cyp = m.Foliage("2a4a2c", "7a9c58", .3f, 64);
        int olive = m.Foliage("4a5a40", "a1ad86", .4f, 65);
        // Acropolis mesa rising out of the cloud sea.
        sc.Add(new Vector3(8, -11, 4), new Vector3(25, -.5f, 13), (Vector3 p, out int mt) =>
        {
            mt = lime;
            float top = -1.25f + n.Fbm(new Vector3(p.X, 0, p.Z), 2, 3, 66) * .12f;
            float side = MathF.Abs(p.X - 16.5f) - 7.2f + (p.Y - top) * -.18f + n.Fbm(p, 1.1f, 5, 67) * .55f;
            float front = 7.6f - p.Z + n.Fbm(p, 1.3f, 4, 68) * .5f;
            float d = MathF.Max(MathF.Max(side, p.Y - top), MathF.Max(front, p.Z - 12));
            return d * .7f;
        });
        int ftiles = m.Tiles("c8b294", .08f, .6f, 60);
        var parthenon = Temple(marble, shade, 3.1f, 2.6f, .17f, 1.55f, 8, -1, ftiles);
        sc.Add(new Vector3(12.6f, -1.4f, 8.4f), new Vector3(20.4f, 2.3f, 11.6f), (Vector3 p, out int mt) => parthenon(p - new Vector3(16.5f, -1.28f, 8.8f), out mt));
        foreach (var (x, h) in new[] { (9.9f, 1.9f), (10.5f, 1.5f), (22.6f, 2.1f), (23.3f, 1.6f) })
        {
            int seed = (int)(x * 10);
            sc.Add(new Vector3(x - .3f, -1.4f, 8.2f), new Vector3(x + .3f, -1.25f + h + .1f, 9.4f), (Vector3 p, out int mt) => { mt = cyp; return Kit3D.Cypress(n, p - new Vector3(x, -1.3f, 8.8f), h, .42f, seed); });
        }
        // A rocky spur with a small round tholos on the left.
        sc.Add(new Vector3(0, -11, 6), new Vector3(7, -1.5f, 12), (Vector3 p, out int mt) =>
        {
            mt = lime;
            float top = -2.3f + n.Fbm(new Vector3(p.X, 0, p.Z), 1.5f, 3, 69) * .25f;
            float d = MathF.Max(MathF.Max(MathF.Abs(p.X - 3.5f) - 2.6f + n.Fbm(p, 1, 4, 70) * .5f, p.Y - top), MathF.Max(9 - p.Z + n.Fbm(p, 1.2f, 3, 71) * .4f, p.Z - 11.5f));
            return d * .7f;
        });
        sc.Add(new Vector3(2.4f, -2.4f, 9.2f), new Vector3(4.6f, .2f, 11.4f), (Vector3 p, out int mt) =>
        {
            var q = p - new Vector3(3.5f, -2.32f, 10.3f); var a = Acc.Start;
            a.Add(Kit3D.Steps(q, 1.05f, 1.05f, .18f, 2, .08f), marble);
            var ring = Sdf.RepeatAngleY(q - new Vector3(0, .18f, 0), 10);
            a.Add(Kit3D.Column(ring - new Vector3(.82f, 0, 0), .07f, 1.15f, 12, true), marble);
            a.Add(Sdf.CylinderY(q - new Vector3(0, .78f, 0), .62f, .48f), shade);
            a.Add(Sdf.CylinderY(q - new Vector3(0, 1.42f, 0), .95f, .08f), marble);
            a.Add(MathF.Max(Sdf.Sphere(q - new Vector3(0, 1.3f, 0), .95f), -(q.Y - 1.5f)) , marble);
            mt = a.M; return a.D;
        });
        sc.Add(new Vector3(5.2f, -2.6f, 9.4f), new Vector3(6.8f, -.6f, 11), (Vector3 p, out int mt) =>
        {
            var lobes = new[] { (new Vector3(6f, -1.3f, 10.2f), .55f), (new Vector3(5.6f, -1.5f, 10.1f), .4f), (new Vector3(6.4f, -1.45f, 10.3f), .42f) };
            float c = Kit3D.Canopy(n, p, lobes, .16f, 72);
            float t = Kit3D.Trunk(p - new Vector3(6, -2.35f, 10.2f), .9f, .07f, Array.Empty<(Vector3, Vector3, float)>());
            if (c < t) { mt = olive; return c; }
            mt = lime; return t;
        });
        var sea = sc.AddVolume(new Vector3(0, -11, 0), new Vector3(32, -1.2f, 22), Kit3D.CloudSea(n, -4.4f, 2.2f, 3.4f, 73, 2.6f));
        sea.Coarse = Kit3D.CloudSeaCoarse(n, -4.4f, 2.2f, 3.4f, 73, 2.6f); sea.Albedo = R.Rgb("fff2ea"); sea.Steps = 44;
        var lt = Light(sky, 26, .016f); lt.HazeMax = .5f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Mid()
    {
        var spec = new LayerSpec("mid", .65f, 96, 36, -9.5f, 21, -95);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3.5f);
        int marble = m.Marble("f3eadb", "9c907e", .9f, .5f, 81);
        int shade = m.Marble("d2c6b4", "8a7e70", .9f, .45f, 82);
        int worn = m.Rock("9a8c80", "e2d4c2", 1, .85f, .45f, 83, "7d8a58", .25f);
        int gold = m.Metal("e8b960", .28f, .15f, "6b5a2e", 84);
        int bronze = m.Metal("b07a46", .35f, .55f, "3f6b5a", 85);
        int tiles = m.Tiles("cdb79c", .14f, .6f, 86);
        int cyp = m.Foliage("2a4a2c", "86a860", .3f, 87);
        int olive = m.Foliage("4e5e44", "a8b48e", .42f, 88);
        int bark = m.Wood("3c3028", "7a6a58", .85f, true, 89);
        // Terrace: a marble pavement edge behind the walkway, its face falling into the clouds.
        sc.Add(new Vector3(0, -6, 1.5f), new Vector3(36, -2.3f, 14), (Vector3 p, out int mt) =>
        {
            mt = worn;
            float top = -2.62f;
            float face = 2.2f - p.Z + n.Fbm(p, .9f, 4, 90) * .12f;
            float d = MathF.Max(MathF.Max(p.Y - top, face), MathF.Max(p.Z - 13.5f, -p.Y - 5.6f + n.Fbm(p, 1.4f, 4, 91) * .9f));
            // Ashlar joints on the face.
            float course = R.Fract((p.Y + 6) / .52f), block = R.Fract((p.X + MathF.Floor((p.Y + 6) / .52f) * .45f) / 1.15f);
            float joint = MathF.Min(MathF.Min(course, 1 - course) / .52f * 2, MathF.Min(block, 1 - block) * 1.15f * 2);
            if (MathF.Abs(face) < .1f) d += R.Smooth(.04f, 0, joint) * .025f;
            return d * .8f;
        });
        // Main temple.
        var temple = Temple(marble, shade, 6.2f, 5.4f, .36f, 5.5f, 8, -1, tiles);
        sc.Add(new Vector3(3.2f, -2.8f, 3.4f), new Vector3(17.2f, 6.6f, 9.8f), (Vector3 p, out int mt) => temple(p - new Vector3(10.2f, -2.62f, 3.9f), out mt));
        // Ruined colonnade on the right: broken shafts of varied height and a fallen drum.
        float[] ruinH = { 4.6f, 3.1f, 5.2f, 1.6f, 4.1f };
        sc.Add(new Vector3(20.2f, -2.8f, 4.6f), new Vector3(30.4f, 3.2f, 6.6f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start;
            for (int k = 0; k < ruinH.Length; k++)
            {
                var q = p - new Vector3(21.2f + k * 2.05f, -2.62f, 5.6f);
                float col = Kit3D.Column(q, .34f, 5.6f, 20, true);
                // Broken top: a jagged plane cut.
                float cut = q.Y - ruinH[k] - n.Fbm(q, .25f, 3, 92 + k) * .25f - q.X * .3f;
                a.Add(MathF.Max(col, k == 2 ? col : cut), k == 2 ? marble : worn);
            }
            a.Add(Sdf.CylinderX(p - new Vector3(25.4f, -2.62f + .32f, 6.1f), .32f, .48f), worn);
            a.Add(Sdf.Box(p - new Vector3(26.2f, 2.85f, 5.6f), new Vector3(2.4f, .3f, .4f)), marble);
            mt = a.M; return a.D;
        });
        // Cypresses and an olive tree.
        foreach (var (x, z, h) in new[] { (17.9f, 6.5f, 5.6f), (19f, 7.6f, 4.6f), (33.2f, 6.2f, 6.1f), (34.5f, 7.4f, 4.9f), (1.4f, 7.8f, 4.3f) })
        {
            int seed = (int)(x * 13);
            sc.Add(new Vector3(x - .55f, -2.7f, z - .55f), new Vector3(x + .55f, -2.62f + h + .1f, z + .55f), (Vector3 p, out int mt) => { mt = cyp; return Kit3D.Cypress(n, p - new Vector3(x, -2.62f, z), h, .95f, seed); });
        }
        var olRnd = new Rand(7);
        var olLobes = Kit3D.Crown(olRnd, new Vector3(31.2f, .4f, 4.4f), 2.6f, 1.3f, 9, .7f);
        var (olMin, olMax) = Kit3D.LobeBounds(olLobes, .5f);
        sc.Add(Vector3.Min(olMin, new Vector3(29.2f, -2.7f, 3.2f)), Vector3.Max(olMax, new Vector3(33.2f, 1.6f, 5.6f)), (Vector3 p, out int mt) =>
        {
            float c = Kit3D.Canopy(n, p, olLobes, .28f, 93);
            float t = Kit3D.Trunk(p - new Vector3(31.2f, -2.62f, 4.4f), 2.2f, .17f, new[] { (new Vector3(0, 1.6f, 0), new Vector3(-.8f, 2.6f, 0), .09f), (new Vector3(0, 1.8f, 0), new Vector3(.9f, 2.7f, .1f), .08f) });
            t += MathF.Sin(p.Y * 9 + p.X * 3) * .015f;
            if (c < t) { mt = olive; return c; }
            mt = bark; return t;
        });
        // Bronze tripod braziers with live flames.
        foreach (float x in new[] { 1.8f, 19.6f })
        {
            sc.Add(new Vector3(x - .5f, -2.7f, 2.6f), new Vector3(x + .5f, -.4f, 3.6f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(x, -2.62f, 3.1f); var a = Acc.Start;
                for (int k = 0; k < 3; k++)
                {
                    float ang = k * 2.094f; var foot = new Vector3(MathF.Cos(ang) * .3f, 0, MathF.Sin(ang) * .3f);
                    a.Add(Sdf.Capsule(q, foot, new Vector3(foot.X * .4f, 1.2f, foot.Z * .4f), .025f), bronze);
                }
                float bowl = MathF.Max(Sdf.Sphere(q - new Vector3(0, 1.45f, 0), .34f), q.Y - 1.45f);
                bowl = Sdf.Subtract(bowl, Sdf.Sphere(q - new Vector3(0, 1.47f, 0), .3f));
                a.Add(bowl, bronze);
                mt = a.M; return a.D;
            });
            Kit3D.Fire(sc, new Vector3(x, -1.22f, 3.1f), .24f, .85f, 94 + (int)x, 2);
        }
        var sea = sc.AddVolume(new Vector3(0, -9.5f, 0), new Vector3(36, -2.4f, 16), Kit3D.CloudSea(n, -5.4f, 2.4f, 3.6f, 95, 3));
        sea.Coarse = Kit3D.CloudSeaCoarse(n, -5.4f, 2.4f, 3.6f, 95, 3); sea.Albedo = R.Rgb("fff1e6"); sea.Steps = 44;
        var lt = Light(sky, 6, .012f); lt.HazeMax = .22f; lt.BloomStrength = .5f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Near()
    {
        var spec = new LayerSpec("near", .4f, 96, 40, -9.5f, 5.6f, -94);
        var sc = new Scene(spec.Period); var n = sc.N; var sky = MakeSky(spec.Period); var cam = new Camera(4);
        var sea = sc.AddVolume(new Vector3(0, -9.5f, 0), new Vector3(40, -3.9f, 12), Kit3D.CloudSea(n, -6.4f, 1.9f, 4f, 101, 3.4f));
        sea.Coarse = Kit3D.CloudSeaCoarse(n, -6.4f, 1.9f, 4f, 101, 3.4f); sea.Albedo = R.Rgb("fff0e4"); sea.Steps = 40;
        var lt = Light(sky, 4, .01f); lt.HazeMax = .3f;
        return Bake.Layer(spec, sc, lt, cam);
    }
}
