using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>Asgard at the blue hour: aurora curtains and stars over moonlit snow peaks, the Bifrost arching to
/// Valhalla's shield-roofed hall on its cliff, and in front the vast trunk of Yggdrasil, glowing rune stones,
/// a carved longhouse with firelit doors and snow-laden pines.</summary>
public sealed class AsgardScene : ThemePainter
{
    public override string Id => "asgard";

    static readonly Vector3 MoonDir = Vector3.Normalize(new Vector3(.5f, .55f, -.45f));

    static Sky MakeSky(float period) => new(period, (-11, "1c304a"), (-3, "2d4868"), (0, "36557e"), (3, "203a62"), (6, "122244"), (10, "070e22"))
    {
        SunX = 21.5f, SunY = 6.4f, SunRadius = .42f, HaloRadius = 3.5f, SunColor = new Vector3(5.5f, 6, 6.8f), HaloColor = new Vector3(.18f, .24f, .4f),
        Stars = .045f, StarsBelow = 1.5f, Aurora = .6f, AuroraLow = 1.6f, AuroraHigh = 7.5f,
        AuroraLowColor = new Vector3(.15f, 1.5f, .75f), AuroraHighColor = new Vector3(.75f, .3f, 1.2f),
    };

    static Lighting Light(Sky sky, float distance, float haze) => new()
    {
        SunDir = MoonDir, Sun = new Vector3(1.25f, 1.5f, 2.1f),
        SkyZenith = R.Rgb("2c5084") * .9f, SkyHorizon = R.Rgb("4a6c90") * .8f, Bounce = R.Rgb("90a6c6") * .4f, Ambient = 1,
        Haze = Bake.HazeFrom(sky, .15f), Distance = distance, HazeDensity = haze, Exposure = 1.55f, ShadowK = 12, Saturation = 1.05f,
    };

    public static Lighting TerrainLight()
    {
        var l = Light(MakeSky(12), 0, 0); l.HazeMax = 0; l.Exposure = 1.45f; l.ShadowReach = 3; l.AoScale = .5f;
        l.Sun = new Vector3(1.6f, 1.75f, 2.1f);
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
        var n = sc.N; var rockA = R.Rgb("2c323c"); var rockB = R.Rgb("5a6270"); var snow = R.Rgb("eef4fb"); var snowShade = R.Rgb("8ea4c8");
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float t = n.Fbm(p, 1.2f, 5, 141) * .5f + .5f;
            var rock = R.Mix(rockA, rockB, t);
            float snowAmt = R.Smooth(snowLine - .8f, snowLine + .4f, p.Y + n.Fbm(p, .9f, 4, 142) * 1.2f) * R.Smooth(.2f, .55f, s.N.Y + n.N(p, .25f, 143) * .25f);
            snowAmt = MathF.Max(snowAmt, R.Smooth(.55f, .8f, s.N.Y) * .9f);
            s.Albedo = R.Mix(rock, R.Mix(snowShade, snow, R.Clamp01(s.N.Y)), snowAmt);
            s.Rough = R.Mix(.9f, .5f, snowAmt);
            s.N = Mats.Bump(q => n.Ridged(q, .6f, 4, 144) * .08f, p, s.N, 1, .02f);
        });
    }

    /// <summary>Overlapping round shields laid like shingles, each with a boss: Valhalla's roof.</summary>
    static int ShieldRoof(Scene sc, float size)
    {
        var gold = R.Rgb("d9a64a"); var dark = R.Rgb("4a3518"); var n = sc.N;
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float row = MathF.Floor(p.Y / (size * .7f)); float u = p.X / size + (row % 2) * .5f, fu = R.Fract(u) - .5f;
            float fv = R.Fract(p.Y / (size * .7f)) - .5f;
            float d = MathF.Sqrt(fu * fu + fv * fv * 1.6f);
            bool shield = d < .5f;
            float boss = R.Smooth(.16f, .08f, d);
            s.Albedo = shield ? gold * (.75f + R.Hash01((int)MathF.Floor(u), (int)row, 0, 5) * .35f) : dark;
            s.Metal = shield ? .9f : 0; s.Rough = shield ? .32f + n.Fbm(p, .2f, 3, 145) * .15f : .8f;
            s.N = Mats.Bump(q =>
            {
                float r2 = MathF.Floor(q.Y / (size * .7f)), uu = R.Fract(q.X / size + (r2 % 2) * .5f) - .5f, vv = R.Fract(q.Y / (size * .7f)) - .5f;
                float dd = MathF.Sqrt(uu * uu + vv * vv * 1.6f);
                return (dd < .5f ? (1 - dd * 2) * .02f : -.01f) + R.Smooth(.16f, .08f, dd) * .015f;
            }, p, s.N, 1, .004f);
        });
    }

    /// <summary>A Norse longhouse seen side-on: timber walls, gable roof with ridge along x, crossed dragon-head
    /// gable boards at both ends, a firelit doorway and hung shields. Origin at the wall foot, centred.</summary>
    static Shape Longhouse(Scene sc, int timber, int roof, int fire, int shieldPaint, float halfW, float wallH, float depth, float roofH)
    {
        // Gable end toward the viewer: plank wall, steep roof sloping down to both sides, crossed carved boards
        // ending in dragon heads above the apex, a firelit doorway and painted shields flanking it.
        var n = sc.N;
        return (Vector3 q, out int m) =>
        {
            var a = Acc.Start;
            float wall = Sdf.Box(q - new Vector3(0, wallH * .5f, depth * .5f), new Vector3(halfW, wallH * .5f, depth * .5f));
            float gableWall = Sdf.GableZ(q - new Vector3(0, wallH, depth * .5f), halfW, roofH * .92f, depth * .5f);
            wall = MathF.Min(wall, gableWall);
            wall += R.Smooth(.03f, 0, MathF.Abs(R.Mod(q.X, .24f) - .12f) - .1f) * .012f;
            float door = Sdf.Box(q - new Vector3(0, wallH * .42f, 0), new Vector3(wallH * .22f, wallH * .42f, .18f));
            a.Add(Sdf.Subtract(wall, door), timber);
            a.Add(Sdf.Box(q - new Vector3(0, wallH * .42f, .14f), new Vector3(wallH * .22f, wallH * .42f, .02f)), fire);
            // Roof slabs overhanging the gable, thick and snow-laden.
            float outer = Sdf.GableZ(q - new Vector3(0, wallH - .1f, depth * .5f - .25f), halfW + .5f, roofH + .25f, depth * .5f + .3f);
            float inner = Sdf.GableZ(q - new Vector3(0, wallH - .42f, depth * .5f - .25f), halfW + .5f, roofH + .25f, depth * .5f + .6f);
            a.Add(Sdf.Subtract(outer, inner), roof);
            // Crossed barge boards with curled dragon heads.
            var apex = new Vector3(0, wallH - .1f + roofH + .25f, -.08f);
            foreach (float sx in new[] { -1f, 1f })
            {
                var dir = Vector3.Normalize(new Vector3(-sx * (halfW + .5f), -(roofH + .25f), 0));
                var start = apex + new Vector3(sx * (halfW + .5f), -(roofH + .25f), 0);
                var tip = apex - dir * .55f;
                float board = Sdf.Capsule(q, start, tip, .07f);
                var head = q - (tip + new Vector3(-sx * .12f, .12f, 0));
                float curl = MathF.Max(Sdf.TorusZ(head, .14f, .05f), -(head.Y + .02f) * 2);
                a.Add(MathF.Min(board, curl), timber);
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                var sq = new Vector3(q.X - sx * wallH * .62f, q.Y - wallH * .55f, q.Z + .05f);
                float ang = MathF.Atan2(sq.Y, sq.X);
                a.Add(Sdf.CylinderZ(sq, .34f, .03f) - .01f, shieldPaint);
            }
            m = a.M; return a.D;
        };
    }

    PaintedLayer SkyLayer()
    {
        var spec = new LayerSpec("sky", .95f, 128, 30, -11, 21, -97);
        var sc = new Scene(spec.Period); var n = sc.N; var sky = MakeSky(spec.Period); var cam = new Camera(2.5f);
        int alpine = Alpine(sc, -.5f);
        var peaks = new[] { (6f, 6.2f, 3f), (12.4f, 3.6f, 2.2f), (18.2f, 7.4f, 3.4f), (25f, 4.4f, 2.6f), (29.2f, 2.8f, 2f) };
        sc.Add(new Vector3(0, -9, 30), new Vector3(30, 9, 52), (Vector3 p, out int m) =>
        {
            m = alpine; float rise = R.Smooth(30, 40, p.Z);
            float h = R.Mix(-9, Kit3D.Mountain(n, 30, p.X, p.Z, -5.2f, peaks, .9f, 13), rise);
            return (p.Y - h) * .3f;
        });
        var mist = sc.AddVolume(new Vector3(0, -11, 6), new Vector3(30, -1.5f, 30), Kit3D.CloudSea(n, -4.8f, 1.6f, 1.2f, 146, 3));
        mist.Coarse = Kit3D.CloudSeaCoarse(n, -4.8f, 1.6f, 1.2f, 146, 3); mist.Albedo = R.Rgb("b8c8e0"); mist.Steps = 40; mist.Ambient = 1.2f;
        var lt = Light(sky, 70, .01f); lt.Background = sky.At; lt.HazeMax = .7f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Far()
    {
        var spec = new LayerSpec("far", .85f, 128, 32, -11, 18, -96);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3);
        int cliff = m.Snowy(m.Rock("232830", "4c5564", 1.4f, .9f, .5f, 151), 1.1f);
        int timber = m.Wood("2a1e16", "5a4232", .8f, true, 152);
        int roof = ShieldRoof(sc, .32f);
        int fire = m.Glow("ffa040", 9, "1a0c04", .3f, 153);
        int paint = m.Paint("8a2a22", .6f, .3f, 154);
        int pine = m.Snowy(m.Foliage("102018", "2c4a36", .25f, 155), .9f);
        // Rainbow bridge: a luminous spectral arc spanning the sky toward the hall.
        int bifrost = sc.Material((Vector3 p, ref Surface s) =>
        {
            var d = new Vector2(R.Wrap(p.X, 8, 32), p.Y + 10); float r = (d.Length() - 13.2f) / .32f;
            float hue = R.Clamp01(r * .5f + .5f);
            var c = hue < .2f ? R.Mix(new Vector3(1, .1f, .1f), new Vector3(1, .6f, .1f), hue / .2f) : hue < .4f ? R.Mix(new Vector3(1, .6f, .1f), new Vector3(.9f, 1, .2f), (hue - .2f) / .2f)
                  : hue < .6f ? R.Mix(new Vector3(.9f, 1, .2f), new Vector3(.2f, 1, .4f), (hue - .4f) / .2f) : hue < .8f ? R.Mix(new Vector3(.2f, 1, .4f), new Vector3(.2f, .5f, 1), (hue - .6f) / .2f)
                  : R.Mix(new Vector3(.2f, .5f, 1), new Vector3(.6f, .2f, 1), (hue - .8f) / .2f);
            s.Albedo = Vector3.Zero; s.Emit = c * (2.2f + n.Fbm(p, .6f, 3, 156) * .6f);
        });
        sc.Add(new Vector3(-6, -3, 17), new Vector3(22, 3.6f, 19), (Vector3 p, out int mt) =>
        {
            mt = bifrost; var q = p - new Vector3(8, -10, 18);
            float d = Sdf.TorusZ(q, 13.2f, .32f);
            return MathF.Max(d, -(p.Y + 2.6f));
        });
        // The cliff and Valhalla upon it.
        sc.Add(new Vector3(14, -11, 6), new Vector3(31, -.3f, 14), (Vector3 p, out int mt) =>
        {
            mt = cliff;
            float top = -1.1f + n.Fbm(new Vector3(p.X, 0, p.Z), 2, 3, 157) * .15f;
            float side = MathF.Abs(p.X - 22.5f) - 7.5f + (p.Y - top) * -.12f + n.Fbm(p, 1, 5, 158) * .6f;
            float front = 8.5f - p.Z + n.Fbm(p, 1.2f, 4, 159) * .5f;
            return MathF.Max(MathF.Max(side, p.Y - top), MathF.Max(front, p.Z - 13.5f)) * .7f;
        });
        var hall = Longhouse(sc, timber, roof, fire, paint, 1.2f, .7f, 2.4f, 1.1f);
        sc.Add(new Vector3(20.6f, -1.4f, 8.8f), new Vector3(24.8f, 1.7f, 12.4f), (Vector3 p, out int mt) => hall(p - new Vector3(22.7f, -1.12f, 9.5f), out mt));
        var hut = Longhouse(sc, timber, roof, fire, paint, .6f, .45f, 1.2f, .6f);
        sc.Add(new Vector3(16.8f, -1.3f, 9.4f), new Vector3(19.2f, .6f, 11.6f), (Vector3 p, out int mt) => hut(p - new Vector3(18f, -1.12f, 10f), out mt));
        foreach (var (x, z, h) in new[] { (15.4f, 9.4f, 1.3f), (27.6f, 9.6f, 1.6f), (28.6f, 10.2f, 1.2f), (2.4f, 9.6f, 1.4f), (3.4f, 10.4f, 1.1f) })
        {
            int seed = (int)(x * 17);
            sc.Add(new Vector3(x - .5f, -2.3f, z - .5f), new Vector3(x + .5f, -1.1f + h + .1f, z + .5f), (Vector3 p, out int mt) => { mt = pine; return Kit3D.Pine(n, p - new Vector3(x, -1.15f, z), h, .7f, 5, seed); });
        }
        sc.Add(new Vector3(0, -11, 7), new Vector3(7, -1.4f, 12), (Vector3 p, out int mt) =>
        {
            mt = cliff;
            float top = -2.1f + n.Fbm(new Vector3(p.X, 0, p.Z), 1.5f, 3, 160) * .2f;
            return MathF.Max(MathF.Max(MathF.Abs(p.X - 3.2f) - 2.8f + n.Fbm(p, 1, 4, 161) * .5f, p.Y - top), MathF.Max(9.2f - p.Z + n.Fbm(p, 1.2f, 3, 162) * .4f, p.Z - 11.5f)) * .7f;
        });
        var mist = sc.AddVolume(new Vector3(0, -11, 0), new Vector3(32, -2, 20), Kit3D.CloudSea(n, -5.2f, 1.8f, 1.4f, 163, 2.6f));
        mist.Coarse = Kit3D.CloudSeaCoarse(n, -5.2f, 1.8f, 1.4f, 163, 2.6f); mist.Albedo = R.Rgb("b4c4dc"); mist.Steps = 36; mist.Ambient = 1.2f;
        var lt = Light(sky, 26, .016f); lt.HazeMax = .45f; lt.BloomStrength = .5f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Mid()
    {
        var spec = new LayerSpec("mid", .65f, 192, 36, -9.5f, 21, -95);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3.5f);
        int bark = m.Snowy(m.Rock("2e241c", "5c4a3a", .6f, .95f, .2f, 171), .7f);
        int runeBark = m.Glyphs(bark, "56e8ff", 2.4f, p => p.Y > 1.2f && p.Y < 2.6f ? Kit3D.Runes(p.X * 1.2f + p.Z * .6f, p.Y, .26f, .42f, .03f, 172) : 0);
        int granite = m.Snowy(m.Rock("343a44", "6a7280", .8f, .85f, .5f, 173), .9f);
        int runeStone = m.Glyphs(granite, "56e8ff", 2.6f, p => Kit3D.Runes(p.X * 1.4f + p.Z * .7f, p.Y, .26f, .4f, .014f, 174));
        int timber = m.Wood("2a1e16", "604634", .8f, true, 175);
        int roof = m.Snowy(m.Tiles("3a2c22", .16f, .85f, 176), .55f);
        int fire = m.Glow("ff9a38", 10, "1a0c04", .3f, 177);
        int shield = m.Paint("94322a", .55f, .35f, 178);
        int pine = m.Snowy(m.Foliage("10201a", "2e4c38", .25f, 179), .85f);
        int snow = m.Snow(180);
        int iron = m.Metal("3a3c40", .5f, .4f, "2a2018", 181);
        // Snowfield behind the walkway.
        sc.Add(new Vector3(0, -6, 1.5f), new Vector3(36, -2.2f, 14), (Vector3 p, out int mt) =>
        {
            mt = snow; float top = -2.68f + n.Fbm(new Vector3(p.X, 0, p.Z), 1.4f, 4, 182) * .1f + R.Smooth(6, 13, p.Z) * .5f;
            return MathF.Max(p.Y - top, 2.2f - p.Z) * .8f;
        });
        // Yggdrasil: a vast ridged trunk with root flares and a glowing rune band, rising out of frame.
        sc.Add(new Vector3(21.5f, -3, 4), new Vector3(32.5f, 11.6f, 13), (Vector3 p, out int mt) =>
        {
            var q = p - new Vector3(27, -2.7f, 8.5f);
            float ang = MathF.Atan2(q.Z, q.X);
            float ridges = MathF.Sin(ang * 14 + n.Fbm(p * new Vector3(1, .3f, 1), .8f, 3, 183) * 4) * .08f;
            float r = 2.1f - R.Clamp01(q.Y / 14) * .5f + ridges + n.Fbm(p, .7f, 4, 184) * .12f;
            float trunk = (MathF.Sqrt(q.X * q.X + q.Z * q.Z) - r) * .7f;
            trunk = MathF.Max(trunk, -q.Y - .2f);
            for (int k = 0; k < 6; k++)
            {
                float a = k / 6f * MathF.PI * 2 + .4f; var dir = new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
                float root = Sdf.RoundCone(q, dir * 1.2f + new Vector3(0, 1.4f, 0), dir * (3.6f + (k % 2) * .9f) + new Vector3(0, -.15f, 0), .9f, .22f);
                trunk = R.SMin(trunk, root + n.Fbm(p, .5f, 3, 185 + k) * .08f, .6f);
            }
            mt = runeBark; return trunk;
        });
        // Rune stones.
        foreach (var (x, h) in new[] { (2.6f, 3.4f), (10.8f, 2.4f), (19.4f, 4.1f), (34.2f, 2.8f) })
        {
            int seed = (int)(x * 23);
            sc.Add(new Vector3(x - .7f, -2.8f, 3.6f), new Vector3(x + .7f, -2.6f + h + .2f, 5), (Vector3 p, out int mt) =>
            {
                mt = runeStone; var q = p - new Vector3(x, -2.65f, 4.3f);
                float d = Sdf.RoundBox(q - new Vector3(0, h * .5f, 0), new Vector3(.45f - q.Y * .03f, h * .5f, .2f), .12f);
                return d + n.Fbm(p, .35f, 4, seed) * .06f;
            });
        }
        // The carved longhouse.
        var house = Longhouse(sc, timber, roof, fire, shield, 2.6f, 1.9f, 4f, 2.6f);
        sc.Add(new Vector3(6.6f, -2.8f, 5.3f), new Vector3(13.4f, 3.4f, 10.4f), (Vector3 p, out int mt) => house(p - new Vector3(10, -2.65f, 6), out mt));
        // Iron fire baskets flanking the door.
        foreach (float x in new[] { 6.4f, 13.6f })
        {
            sc.Add(new Vector3(x - .4f, -2.8f, 4.4f), new Vector3(x + .4f, -.6f, 5.2f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(x, -2.65f, 4.8f); var a = Acc.Start;
                a.Add(Sdf.CylinderY(q - new Vector3(0, .7f, 0), .04f, .7f), iron);
                float basket = MathF.Max(Sdf.ConeY(q - new Vector3(0, 1.55f, 0), .15f, .12f, .26f), -Sdf.ConeY(q - new Vector3(0, 1.6f, 0), .14f, .1f, .23f));
                a.Add(basket, iron);
                mt = a.M; return a.D;
            });
            Kit3D.Fire(sc, new Vector3(x, -1.05f, 4.8f), .19f, .65f, 186 + (int)x, 1.3f);
        }
        foreach (var (x, z, h) in new[] { (17.4f, 6.4f, 4.8f), (18.6f, 7.8f, 3.6f), (33f, 6.6f, 5.4f), (35.2f, 8f, 4f), (1.2f, 8.2f, 3.8f) })
        {
            int seed = (int)(x * 19);
            sc.Add(new Vector3(x - 1.4f, -2.8f, z - 1.4f), new Vector3(x + 1.4f, -2.6f + h + .1f, z + 1.4f), (Vector3 p, out int mt) => { mt = pine; return Kit3D.Pine(n, p - new Vector3(x, -2.65f, z), h, 2.4f, 6, seed); });
        }
        var lt = Light(sky, 6, .012f); lt.HazeMax = .25f; lt.BloomStrength = .55f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Near()
    {
        // Icicle-hung rock overhangs reach into the top of the view when the camera climbs.
        var spec = new LayerSpec("near", .4f, 192, 40, 9, 4.6f, -94);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(4);
        int rock = m.Snowy(m.Rock("1c2028", "3a4250", 1, .9f, .5f, 191), 1.2f);
        int ice = sc.Material((Vector3 p, ref Surface s) => { s.Albedo = R.Rgb("a8d4ee"); s.Rough = .12f; s.Spec = .05f; s.Trans = .6f; s.Emit = R.Rgb("2a5a7a") * .4f; });
        foreach (var (x, w) in new[] { (5f, 5f), (21f, 7f), (34f, 4.5f) })
        {
            sc.Add(new Vector3(x - w, 9, 1), new Vector3(x + w, 13.6f, 5), (Vector3 p, out int mt) =>
            {
                var a = Acc.Start;
                float under = 11.2f - MathF.Pow(MathF.Abs(p.X - x) / w, 2) * 1.6f + n.Fbm(new Vector3(p.X, 0, p.Z), .9f, 4, 192) * .4f;
                a.Add(MathF.Max(under - p.Y, MathF.Abs(p.Z - 3) - 1.8f) * .7f + n.Fbm(p, .5f, 3, 193) * .1f, rock);
                // Icicles hanging from the underside.
                float cell = R.Mod(p.X, .32f) - .16f; int id = (int)MathF.Floor(p.X / .32f);
                float len = .25f + R.Hash01(id, 0, 0, 194) * .75f, top = under + .02f;
                var iq = new Vector3(cell, p.Y - top + len * .5f, p.Z - 1.4f - R.Hash01(id, 1, 0, 194) * .6f);
                float icicle = Sdf.ConeY(iq, len * .5f, .005f, .05f + len * .03f);
                if (MathF.Abs(p.X - x) < w * .8f) a.Add(icicle, ice);
                mt = a.M; return a.D;
            });
        }
        var lt = Light(sky, 2, .01f); lt.HazeMax = .2f;
        return Bake.Layer(spec, sc, lt, cam);
    }
}
