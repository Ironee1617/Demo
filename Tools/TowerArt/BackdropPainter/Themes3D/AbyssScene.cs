using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>The abyss of the dark god: a vast eye watching from a bruised black sky among broken pieces of the
/// world, a canyon set with giant teeth where impaled colossi hang as distant silhouettes, and in front walls of
/// flesh under arching ribs, flesh pillars studded with eyes and pools of blood.</summary>
public sealed class AbyssScene : ThemePainter
{
    public override string Id => "abyss";

    static readonly Vector3 KeyDir = Vector3.Normalize(new Vector3(-.4f, .5f, -.55f));

    static Sky MakeSky(float period) => new(period, (-11, "160510"), (-3, "1a0612"), (0, "140818"), (3, "0c0512"), (6, "07030a"), (10, "030104"))
    {
        SunX = 7, SunY = 3.2f, SunRadius = .01f, HaloRadius = 8, SunColor = new Vector3(.5f, .05f, .05f), HaloColor = new Vector3(.12f, .01f, .03f),
    };

    static Lighting Light(Sky sky, float distance, float haze) => new()
    {
        SunDir = KeyDir, Sun = new Vector3(1.15f, .38f, .5f),
        SkyZenith = R.Rgb("2a1238") * .6f, SkyHorizon = R.Rgb("3a0e22") * .6f, Bounce = R.Rgb("4a0810") * .5f, Ambient = .7f,
        Haze = Bake.HazeFrom(sky, .3f), Distance = distance, HazeDensity = haze, Exposure = 1.1f, ShadowK = 10, Saturation = .92f, BloomThreshold = 1,
    };

    public static Lighting TerrainLight()
    {
        var l = Light(MakeSky(12), 0, 0); l.HazeMax = 0; l.Exposure = 1.2f; l.ShadowReach = 3; l.AoScale = .5f;
        return l;
    }

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return SkyLayer();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    /// <summary>Wet, veined flesh with light scattering beneath the surface.</summary>
    public static int Flesh(Scene sc, int seed, string darkHex = "2a0810", string lightHex = "6a2030")
    {
        var n = sc.N; var d = R.Rgb(darkHex); var l = R.Rgb(lightHex); var vein = R.Rgb("4a1a5a"); var bruise = R.Rgb("2a1430");
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float t = n.Fbm(p, .5f, 4, seed) * .5f + .5f;
            float v = R.Smooth(.95f, .99f, n.Ridged(p, .7f, 4, seed + 1));
            float fold = n.Fbm(p * new Vector3(1, 2.2f, 1), .25f, 3, seed + 2) * .5f + .5f;
            s.Albedo = R.Mix(R.Mix(R.Mix(d, l, t * .8f + fold * .2f), bruise, R.Smooth(.55f, .8f, n.Fbm(p, 1.6f, 3, seed + 3) * .5f + .5f) * .6f), vein, v * .6f);
            s.Rough = .3f + (1 - t) * .2f; s.Spec = .04f; s.Trans = .2f;
            s.N = Mats.Bump(q => n.Fbm(q * new Vector3(1, 2.2f, 1), .25f, 3, seed + 2) * .03f + n.Ridged(q, .7f, 3, seed + 1) * .012f, p, s.N, 1, .008f);
        });
    }

    public static int Bone(Scene sc, int seed)
    {
        var n = sc.N; var a = R.Rgb("7a6a52"); var b = R.Rgb("b2a284"); var dirt = R.Rgb("3a1c1a");
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float t = n.Fbm(p, .3f, 4, seed) * .5f + .5f;
            float stain = R.Smooth(.55f, .8f, n.Fbm(p, .8f, 3, seed + 1) * .5f + .5f);
            s.Albedo = R.Mix(R.Mix(a, b, t), dirt, stain * .75f); s.Rough = .6f; s.Spec = .035f;
            s.N = Mats.Bump(q => -R.Smooth(.6f, .8f, n.N(q, .03f, seed + 2) * .5f + .5f) * .006f, p, s.N, 1, .004f);
        });
    }

    /// <summary>Eyeball: glossy veined white, a glowing red iris and a slit pupil looking toward -z.</summary>
    public static int Eye(Scene sc, Vector3 centre, float radius, int seed)
    {
        var n = sc.N;
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            var q = Vector3.Normalize(p - centre);
            float iris = MathF.Sqrt(q.X * q.X + q.Y * q.Y);
            bool front = q.Z < 0;
            float vein = R.Smooth(.86f, .97f, n.Ridged(p, radius * .8f, 3, seed));
            s.Albedo = R.Mix(R.Rgb("a89080"), R.Rgb("6a0a12"), MathF.Min(1, vein * 1.2f + R.Smooth(-.2f, .6f, q.Z) * .5f)); s.Rough = .1f; s.Spec = .06f;
            if (front && iris < .5f)
            {
                float slit = MathF.Abs(q.X) < .08f * (1 - MathF.Abs(q.Y) * 1.6f) ? 1 : 0;
                float fibre = MathF.Sin(MathF.Atan2(q.Y, q.X) * 40) * .5f + .5f;
                s.Albedo = slit > 0 ? new Vector3(.01f) : R.Mix(R.Rgb("a8100a"), R.Rgb("ffb020"), (1 - iris * 2) * .6f + fibre * .2f);
                s.Emit = slit > 0 ? Vector3.Zero : R.Rgb("ff3a14") * (1.2f - iris * 2) * 1.6f;
            }
        });
    }

    /// <summary>The watching eye drawn into the sky: an almond of glowing veined sclera with a burning iris.</summary>
    static Vector3 SkyEye(Noise n, float x, float v, float period)
    {
        float dx = R.Wrap(x, 7, period), dy = v - 3.2f;
        float lid = MathF.Abs(dy) - 2.2f * MathF.Sqrt(MathF.Max(0, 1 - dx * dx / 30.25f));
        if (lid > .4f) return Vector3.Zero;
        float inside = R.Smooth(.25f, -.25f, lid);
        float r = MathF.Sqrt(dx * dx + dy * dy);
        float vein = R.Smooth(.85f, .97f, n.Ridged(new Vector3(x, v, 0), .9f, 4, 806));
        var sclera = R.Mix(R.Rgb("4a1418") * .3f, new Vector3(.9f, .06f, .05f), vein);
        float iris = R.Smooth(1.7f, 1.5f, r), pupil = R.Smooth(.05f, -.05f,MathF.Abs(dx) - MathF.Sqrt(MathF.Max(0, 1 - dy * dy / 3.4f)) * .35f);
        float fibre = MathF.Sin(MathF.Atan2(dy, dx) * 60) * .5f + .5f;
        var col = R.Mix(sclera, R.Mix(new Vector3(1.6f, .12f, .04f), new Vector3(2.4f, .9f, .1f), (1 - r / 1.7f) * .7f + fibre * .2f), iris);
        col *= 1 - pupil * iris;
        col *= .25f + .75f * R.Smooth(0, -.6f, lid);
        return col * inside;
    }

    PaintedLayer SkyLayer()
    {
        var spec = new LayerSpec("sky", .95f, 128, 30, -11, 21, -97);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(2.5f);
        int rock = m.Rock("1a1418", "3a3036", .8f, .8f, .5f, 801);
        int land = m.Ground("2a3a22", "4a5a32", "3a2a22", 802);
        int sea = m.Water("0a1a2a", .8f, .2f, 803);
        // Broken fragments of the world drifting in the dark: a rocky underside with a crust of land and sea.
        foreach (var (x, y, r) in new[] { (-1.5f, .4f, 1.4f), (14.5f, 1.2f, 1.7f), (16.8f, 4.8f, .8f), (1.2f, 4.9f, .7f), (12.6f, 5.6f, .55f) })
        {
            int sd = (int)(x * 31);
            sc.Add(new Vector3(x - r * 1.6f, y - r * 1.8f, 24 - r * 1.6f), new Vector3(x + r * 1.6f, y + r * .5f, 24 + r * 1.6f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(x, y, 24);
                float under = Sdf.Ellipsoid(q - new Vector3(0, -r * .4f, 0), new Vector3(r * 1.3f, r * 1.1f, r * 1.1f)) + n.Fbm(p, r * .4f, 4, sd) * r * .25f;
                float d = MathF.Max(under, q.Y - r * .1f - n.Fbm(new Vector3(p.X, 0, p.Z), r * .5f, 3, sd + 1) * r * .08f);
                bool top = q.Y > r * .02f;
                mt = top ? (n.Fbm(p, r * .6f, 2, sd + 2) > 0 ? land : sea) : rock;
                return d * .7f;
            });
        }
        var mist = sc.AddVolume(new Vector3(0, -11, 8), new Vector3(30, -2, 30), Kit3D.CloudSea(n, -5.4f, 2.6f, .9f, 805, 3));
        mist.Coarse = Kit3D.CloudSeaCoarse(n, -5.4f, 2.6f, .9f, 805, 3); mist.Albedo = R.Rgb("4a1a2a"); mist.Steps = 32; mist.Emit = new Vector3(.05f, .005f, .015f);
        var lt = Light(sky, 60, .01f); lt.Background = (x, v) => sky.At(x, v) + SkyEye(n, x, v, spec.Period); lt.HazeMax = .6f; lt.BloomStrength = .6f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Far()
    {
        var spec = new LayerSpec("far", .85f, 128, 32, -11, 19, -96);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3);
        int flesh = Flesh(sc, 811, "240610", "5a1824");
        int tooth = Bone(sc, 812);
        int iron = m.Metal("1a1416", .5f, .5f, "4a1414", 813);
        // Canyon walls of flesh, gums studded with teeth along their rims.
        sc.Add(new Vector3(0, -11, 6), new Vector3(32, 2.4f, 16), (Vector3 p, out int mt) =>
        {
            mt = flesh; float h = -2.2f + MathF.Abs(MathF.Sin(p.X / 32 * MathF.PI * 2)) * 2.4f + n.Fbm(new Vector3(p.X, 0, p.Z), 1.6f, 4, 814) * .6f;
            return MathF.Max(p.Y - h, 8 - p.Z + n.Fbm(p, 1, 3, 815) * .6f) * .6f;
        });
        sc.Add(new Vector3(0, -3, 6.6f), new Vector3(32, 4.6f, 9.6f), (Vector3 p, out int mt) =>
        {
            mt = tooth; float cell = R.Mod(p.X, 1.1f) - .55f; int id = ((int)MathF.Floor(p.X / 1.1f) % 29 + 29) % 29;
            float base0 = -2.2f + MathF.Abs(MathF.Sin(p.X / 32 * MathF.PI * 2)) * 2.4f;
            float h = .8f + R.Hash01(id, 0, 0, 816) * 1.6f;
            var q = new Vector3(cell, p.Y - base0, p.Z - 8.2f);
            q = Sdf.RotZ(q, (R.Hash01(id, 1, 0, 816) - .5f) * .5f);
            return Sdf.ConeY(q - new Vector3(0, h * .5f, 0), h * .5f, .38f, .03f) + n.Fbm(p, .2f, 3, 817) * .02f;
        });
        // Distant colossi impaled on great spikes, kept to dim silhouettes in the haze.
        foreach (var (x, s) in new[] { (1.5f, 1f), (14f, 1.2f) })
            sc.Add(new Vector3(x - 3 * s, -4, 11), new Vector3(x + 3 * s, 8 * s, 14), (Vector3 p, out int mt) =>
            {
                var q = (p - new Vector3(x, -2.5f, 12.5f)) / s; var a = Acc.Start;
                float spike = Sdf.ConeY(q - new Vector3(0, 4, 0), 4.5f, .3f, .02f);
                a.Add(spike * s, iron);
                float torso = Sdf.Ellipsoid(Sdf.RotZ(q - new Vector3(.1f, 5.6f, 0), .25f), new Vector3(.7f, 1.1f, .45f));
                float head = Sdf.Sphere(q - new Vector3(-.35f, 6.6f, 0), .4f);
                float arms = MathF.Min(Sdf.Capsule(q, new Vector3(-.4f, 6.1f, 0), new Vector3(-1.6f, 5f, .2f), .18f), Sdf.Capsule(q, new Vector3(.5f, 6, 0), new Vector3(1.7f, 4.9f, -.2f), .18f));
                float legs = MathF.Min(Sdf.Capsule(q, new Vector3(-.2f, 4.7f, 0), new Vector3(-.6f, 2.8f, .1f), .22f), Sdf.Capsule(q, new Vector3(.3f, 4.7f, 0), new Vector3(.9f, 3, -.1f), .22f));
                a.Add((R.SMin(R.SMin(torso, head, .2f), MathF.Min(arms, legs), .2f) + n.Fbm(q, .3f, 3, 818) * .05f) * s, flesh);
                mt = a.M; return a.D;
            });
        var mist = sc.AddVolume(new Vector3(0, -11, 0), new Vector3(32, -1, 16), Kit3D.CloudSea(n, -4.6f, 2.2f, 1, 819, 2.6f));
        mist.Coarse = Kit3D.CloudSeaCoarse(n, -4.6f, 2.2f, 1, 819, 2.6f); mist.Albedo = R.Rgb("4a1a28"); mist.Steps = 32; mist.Emit = new Vector3(.06f, .006f, .016f);
        var lt = Light(sky, 26, .02f); lt.HazeMax = .55f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Mid()
    {
        var spec = new LayerSpec("mid", .65f, 192, 36, -9.5f, 21, -95);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3.5f);
        int flesh = Flesh(sc, 821);
        int dark = Flesh(sc, 822, "1e040c", "4a1020");
        int bone = Bone(sc, 823);
        int blood = m.Water("2a0206", .5f, .1f, 824);
        sc.Add(new Vector3(0, -6, 1.5f), new Vector3(36, -1.8f, 15), (Vector3 p, out int mt) =>
        {
            mt = dark; float top = -2.66f + n.Fbm(new Vector3(p.X, 0, p.Z), .9f, 4, 825) * .2f + R.Smooth(5, 14, p.Z) * 1.2f;
            return MathF.Max(p.Y - top, 2.2f - p.Z) * .7f;
        });
        foreach (var (x, z, rx, rz) in new[] { (8f, 4.6f, 2.6f, 1.2f), (27.5f, 5.2f, 3.2f, 1.4f) })
            sc.Add(new Vector3(x - rx - .2f, -3, z - rz - .2f), new Vector3(x + rx + .2f, -2.5f, z + rz + .2f), (Vector3 p, out int mt) =>
            {
                mt = blood; float e = MathF.Pow((p.X - x) / rx, 2) + MathF.Pow((p.Z - z) / rz, 2);
                return MathF.Max(p.Y + 2.62f, (e - 1) * .5f);
            });
        // A ragged wall of folded flesh at the back, low enough to show the canyon and the watching sky.
        sc.Add(new Vector3(0, -3, 11), new Vector3(36, 6.2f, 16), (Vector3 p, out int mt) =>
        {
            mt = dark; float face = 12.5f - p.Z + n.Fbm(p * new Vector3(1, .6f, 1), 1.2f, 5, 826) * .9f + MathF.Sin(p.Y * 1.6f + n.Fbm(p, 2, 2, 827) * 3) * .25f;
            float top = .6f + n.Fbm(new Vector3(p.X, 0, 0), 3, 4, 829) * 2.6f + MathF.Sin(p.X / 36 * MathF.PI * 4) * .9f;
            return MathF.Max(face, (p.Y - top) * .8f) * .6f;
        });
        // Rib arches springing from the ground and meeting overhead in a point.
        sc.Add(new Vector3(0, -3, 8.6f), new Vector3(36, 6.4f, 10.4f), (Vector3 p, out int mt) =>
        {
            mt = bone; float px = MathF.Abs(R.Mod(p.X, 6) - 3), y = p.Y + 2.66f, z = p.Z - 9.5f;
            const float c = 1.5f, rr = 4.2f, y0 = 4.5f;
            float d2 = y >= y0 ? MathF.Abs(MathF.Sqrt((px + c) * (px + c) + (y - y0) * (y - y0)) - rr) : MathF.Abs(px - 2.7f);
            float th = .3f - R.Clamp01(y / 8.4f) * .12f;
            float rib = MathF.Max(MathF.Sqrt(d2 * d2 + z * z) - th, -y - .2f);
            return rib + n.Fbm(p, .3f, 3, 828) * .03f;
        });
        // Flesh pillars studded with eyes.
        var eyes = new List<(Vector3 c, float r)>();
        var rnd = new Rand(829);
        foreach (var (x, h) in new[] { (3f, 7.4f), (16.5f, 9.6f), (31.4f, 6.8f) })
        {
            for (int k = 0; k < 4; k++) eyes.Add((new Vector3(x + rnd.R(-.5f, .5f), -2.66f + rnd.R(1, h - 1.2f), 7 - .78f), rnd.R(.22f, .42f)));
            int sd = (int)(x * 11); float px = x, ph = h;
            sc.Add(new Vector3(x - 1.6f, -3, 5), new Vector3(x + 1.6f, -2.66f + h + .4f, 9), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(px, -2.66f, 7);
                float r = 1.05f + MathF.Sin(q.Y * 1.3f + sd) * .15f - R.Smooth(ph * .7f, ph, q.Y) * .5f;
                float d = MathF.Max(MathF.Sqrt(q.X * q.X + q.Z * q.Z) - r + n.Fbm(p, .4f, 4, sd) * .18f, q.Y - ph);
                mt = flesh; return d * .7f;
            });
        }
        foreach (var (c, r) in eyes)
        {
            int eye = Eye(sc, c, r, (int)(c.Y * 97));
            sc.Add(c - new Vector3(r + .05f), c + new Vector3(r + .05f), (Vector3 p, out int mt) => { mt = eye; return Sdf.Sphere(p - c, r); });
        }
        var lt = Light(sky, 6, .014f); lt.HazeMax = .3f; lt.BloomStrength = .6f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Near()
    {
        // Sinews and tendons hanging from the top when the camera climbs.
        var spec = new LayerSpec("near", .4f, 192, 40, 8.6f, 5, -94);
        var sc = new Scene(spec.Period); var n = sc.N; var sky = MakeSky(spec.Period); var cam = new Camera(4);
        int sinew = Flesh(sc, 841, "4a0c18", "a03a48");
        var rnd = new Rand(842); var strands = new List<(Vector3 a, Vector3 b, float r)>();
        for (float x = .5f; x < 40; x += rnd.R(.6f, 1.8f)) strands.Add((new Vector3(x, 14, rnd.R(1, 3)), new Vector3(x + rnd.R(-.4f, .4f), 13.6f - rnd.R(.8f, 3.6f), rnd.R(1, 3)), rnd.R(.04f, .12f)));
        sc.Add(new Vector3(-1, 8.6f, .5f), new Vector3(41, 13.6f, 3.5f), (Vector3 p, out int mt) =>
        {
            mt = sinew; float d = 1e9f;
            foreach (var (a, b, r) in strands)
            {
                if (MathF.Abs(R.Wrap(p.X, a.X, 40)) > 1) continue;
                var q = new Vector3(a.X + R.Wrap(p.X, a.X, 40), p.Y, p.Z);
                d = MathF.Min(d, Sdf.RoundCone(q, a, b, r, r * .3f));
            }
            return d + n.Fbm(p, .1f, 2, 843) * .01f;
        });
        var lt = Light(sky, 2, .01f); lt.HazeMax = .15f;
        return Bake.Layer(spec, sc, lt, cam);
    }
}
