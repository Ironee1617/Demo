using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>Kunlun at dawn: pillar peaks of the jade mountains rising out of a sea of cloud with pines and a
/// pagoda on their crowns, and in front a heavenly palace of red-lacquered columns under double eaves of golden
/// glazed tile on white marble terraces, peach trees in blossom, red lanterns and a bronze ding breathing incense.</summary>
public sealed class ChineseScene : ThemePainter
{
    public override string Id => "chinese";

    static readonly Vector3 SunDir = Vector3.Normalize(new Vector3(-.6f, .45f, -.4f));

    static Sky MakeSky(float period) => new(period, (-11, "eee2d2"), (-3, "f2e2cc"), (0, "f0d6c0"), (3, "d6ccd6"), (6, "a8b8d0"), (10, "7890b8"))
    {
        SunX = 4, SunY = 2.8f, SunRadius = .55f, HaloRadius = 6, SunColor = new Vector3(26, 20, 13), HaloColor = new Vector3(1.4f, 1, .6f),
        CirrusAmount = .45f, CirrusLow = 2, CirrusHigh = 9, CirrusColor = new Vector3(1.3f, 1.05f, .95f),
    };

    static Lighting Light(Sky sky, float distance, float haze) => new()
    {
        SunDir = SunDir, Sun = new Vector3(4f, 3.2f, 2.3f),
        SkyZenith = R.Rgb("8aa0c8") * .9f, SkyHorizon = R.Rgb("ead6c4") * .85f, Bounce = R.Rgb("b49a82") * .45f, Ambient = .9f,
        Haze = Bake.HazeFrom(sky, .05f), Distance = distance, HazeDensity = haze, Exposure = .92f, ShadowK = 12, Saturation = 1.05f,
    };

    public static Lighting TerrainLight()
    {
        var l = Light(MakeSky(12), 0, 0); l.HazeMax = 0; l.Exposure = .88f; l.ShadowReach = 3; l.AoScale = .5f;
        return l;
    }

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return SkyLayer();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    /// <summary>A Chinese roof seen from the front: steep concave slopes sweeping out to strongly upturned eave
    /// corners, a ridge with end ornaments. Extruded in depth. Origin at the ridge centre.</summary>
    public static float Roof(Vector3 q, float halfW, float drop, float lift, float thick, float halfD)
    {
        float u = MathF.Min(1, MathF.Abs(q.X) / halfW);
        float top = -drop * MathF.Pow(u, .7f) + lift * MathF.Pow(u, 7);
        float slab = MathF.Max(MathF.Max(q.Y - top, top - thick - q.Y), MathF.Max(MathF.Abs(q.X) - halfW, MathF.Abs(q.Z) - halfD * (1 - u * .15f)));
        float ridge = Sdf.Box(q - new Vector3(0, thick * .6f, 0), new Vector3(halfW * .35f, thick * .6f, halfD * .25f));
        float ornament = Sdf.RoundCone(new Vector3(MathF.Abs(q.X), q.Y, q.Z), new Vector3(halfW * .35f, thick, 0), new Vector3(halfW * .38f, thick * 3, 0), thick * .5f, thick * .15f);
        return MathF.Min(slab * .6f, MathF.Min(ridge, ornament));
    }

    /// <summary>Peach-blossom crown material: pink petals over a little green.</summary>
    static int Blossom(Mats m, int seed) => m.Foliage("b8708a", "f6c2d2", .55f, seed, "fff2f6", .45f);

    /// <summary>Sheer pillar mountains: tall eroded columns with flat crowns, furred with pines on top.</summary>
    static int Karst(Scene sc, string darkHex, string lightHex, int seed)
    {
        var n = sc.N; var d = R.Rgb(darkHex); var l = R.Rgb(lightHex); var pine = R.Rgb("2e4630");
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float t = n.Fbm(p * new Vector3(1, .35f, 1), .6f, 5, seed) * .5f + .5f;
            float green = R.Smooth(.55f, .8f, s.N.Y + n.Fbm(p, .3f, 3, seed + 1) * .3f);
            s.Albedo = R.Mix(R.Mix(d, l, t), pine * (.8f + t * .3f), green); s.Rough = .9f; s.Trans = green * .2f;
            s.N = Mats.Bump(q => n.Fbm(q * new Vector3(1, .3f, 1), .2f, 4, seed + 2) * .05f, p, s.N, 1, .02f);
        });
    }

    static float Pillar(Noise n, Vector3 q, float h, float r, int seed)
    {
        float wobble = n.Fbm(new Vector3(q.X, q.Y * .25f, q.Z), .8f, 3, seed) * r * .35f;
        float rr = r * (1 + .1f * MathF.Sin(q.Y * 1.3f + seed)) - MathF.Max(0, q.Y - h + r) * .5f;
        float d = MathF.Sqrt(q.X * q.X + q.Z * q.Z) - rr + wobble;
        return MathF.Max(d * .6f, q.Y - h - n.Fbm(q, .4f, 3, seed + 1) * .3f);
    }

    PaintedLayer SkyLayer()
    {
        var spec = new LayerSpec("sky", .95f, 48, 30, -11, 21, -97);
        var sc = new Scene(spec.Period); var n = sc.N; var sky = MakeSky(spec.Period); var cam = new Camera(2.5f);
        int far = Karst(sc, "8a96a2", "b8c0c8", 601), mid = Karst(sc, "6a7680", "9aa4aa", 602);
        var rnd = new Rand(603);
        foreach (var (z, mat, count, hmin, hmax) in new[] { (50f, far, 9, 3f, 7f), (36f, mid, 7, 2f, 5.5f) })
        {
            for (int k = 0; k < count; k++)
            {
                float x = k * 30f / count + rnd.R(-1, 1), h = rnd.R(hmin, hmax), r = rnd.R(.7f, 1.4f); int sd = 610 + k + (int)z;
                float zz = z + rnd.R(-3, 3);
                sc.Add(new Vector3(x - r * 2, -10, zz - r * 2), new Vector3(x + r * 2, h + 1, zz + r * 2), (Vector3 p, out int mt) => { mt = mat; return Pillar(n, p - new Vector3(x, -9, zz), h + 9, r, sd); });
            }
            var sea = sc.AddVolume(new Vector3(0, -11, z - 10), new Vector3(30, -1, z - 2), Kit3D.CloudSea(n, -4.6f + (50 - z) * .02f, 2.2f, 1.3f, (int)z, 3));
            sea.Coarse = Kit3D.CloudSeaCoarse(n, -4.6f, 2.2f, 1.3f, (int)z, 3); sea.Albedo = R.Rgb("fbf4ee"); sea.Steps = 36; sea.Ambient = 1.3f;
        }
        var lt = Light(sky, 60, .012f); lt.Background = sky.At; lt.HazeMax = .8f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Far()
    {
        var spec = new LayerSpec("far", .85f, 64, 32, -11, 19, -96);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3);
        int rock = Karst(sc, "4e5a5e", "86908c", 621);
        int lacquer = m.Paint("a8281e", .45f, .2f, 622);
        int tile = m.Tiles("d8a43a", .08f, .3f, 623, true);
        int plaster = m.Paint("ece4d4", .7f, .2f, 624);
        foreach (var (x, h, r, z) in new[] { (4f, 3.2f, 1.6f, 10f), (11.5f, 5f, 1.3f, 12f), (19f, 2.4f, 1.9f, 9f), (26.5f, 4.2f, 1.5f, 11f) })
        {
            int sd = (int)(x * 13);
            sc.Add(new Vector3(x - r * 2, -11, z - r * 2), new Vector3(x + r * 2, h + .8f, z + r * 2), (Vector3 p, out int mt) => { mt = rock; return Pillar(n, p - new Vector3(x, -11, z), h + 11, r, sd); });
        }
        // A pagoda on the tallest pillar.
        sc.Add(new Vector3(10.4f, 4.6f, 11), new Vector3(12.6f, 8.6f, 13), (Vector3 p, out int mt) =>
        {
            var q = p - new Vector3(11.5f, 4.8f, 12); var a = Acc.Start;
            for (int k = 0; k < 5; k++)
            {
                float y = k * .65f, w = .5f - k * .06f;
                a.Add(Sdf.Box(q - new Vector3(0, y + .25f, 0), new Vector3(w * .6f, .25f, w * .6f)), k % 2 == 0 ? lacquer : plaster);
                a.Add(Roof(q - new Vector3(0, y + .62f, 0), w * 1.1f, .2f, .18f, .07f, w * .9f), tile);
            }
            a.Add(Sdf.CylinderY(q - new Vector3(0, 3.6f, 0), .03f, .35f), tile);
            mt = a.M; return a.D;
        });
        var sea = sc.AddVolume(new Vector3(0, -11, 0), new Vector3(32, -.5f, 18), Kit3D.CloudSea(n, -3.8f, 2.6f, 2.2f, 625, 2.6f));
        sea.Coarse = Kit3D.CloudSeaCoarse(n, -3.8f, 2.6f, 2.2f, 625, 2.6f); sea.Albedo = R.Rgb("fff6f0"); sea.Steps = 40;
        var lt = Light(sky, 26, .018f); lt.HazeMax = .5f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Mid()
    {
        var spec = new LayerSpec("mid", .65f, 96, 36, -9.5f, 21, -95);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(3.5f);
        int marble = m.Marble("f2efe8", "a8a49c", .8f, .45f, 631);
        int lacquer = m.Paint("a8261c", .4f, .25f, 632);
        int tile = m.Tiles("d8a43a", .13f, .28f, 633, true);
        int greenTile = m.Paint("2e6a5a", .4f, .2f, 634);
        int gold = m.Metal("e6b44a", .25f, .1f, "6b5a2e", 635);
        int bark = m.Wood("3a2a24", "6a524a", .9f, true, 636);
        int blossom = Blossom(m, 637);
        int lantern = sc.Material((Vector3 p, ref Surface s) => { s.Albedo = R.Rgb("c0281c"); s.Rough = .6f; s.Trans = .6f; s.Emit = R.Rgb("ff5a2a") * (2.2f + MathF.Sin(p.Y * 40) * .4f); });
        int bronze = m.Metal("6a5a3a", .45f, .7f, "3a6a5a", 638);
        int paving = m.Rock("8a8a84", "c4c2b8", .9f, .85f, .3f, 639);
        sc.Add(new Vector3(0, -6, 1.5f), new Vector3(36, -2.3f, 14), (Vector3 p, out int mt) =>
        {
            mt = paving; return MathF.Max(p.Y + 2.64f + R.Smooth(.02f, 0, MathF.Min(MathF.Abs(R.Mod(p.X, 1.1f) - .55f), MathF.Abs(R.Mod(p.Z, .8f) - .4f)) - .52f) * .01f, 2.2f - p.Z) * .9f;
        });
        // The palace hall on its marble terrace with a balustrade.
        sc.Add(new Vector3(6.6f, -2.8f, 4), new Vector3(23.4f, 8.4f, 12), (Vector3 p, out int mt) =>
        {
            var q = p - new Vector3(15, -2.64f, 8); var a = Acc.Start;
            a.Add(Kit3D.Steps(q - new Vector3(0, 0, 0), 8, 3.6f, 1, 3, .3f), marble);
            float rail = Sdf.Box(q - new Vector3(0, 1.35f, -3.2f), new Vector3(7.4f, .06f, .06f));
            float posts = Sdf.Box(new Vector3(Sdf.RepeatLimited(q.X, .6f, 12), q.Y - 1.2f, q.Z + 3.2f), new Vector3(.05f, .2f, .05f));
            a.Add(MathF.Min(rail, posts), marble);
            float cols = Sdf.CylinderY(new Vector3(Sdf.RepeatLimited(q.X, 1.7f, 3), q.Y - 3.1f, q.Z + 1.8f), .22f, 2.1f);
            a.Add(cols, lacquer);
            a.Add(Sdf.Box(q - new Vector3(0, 3.1f, -.6f), new Vector3(5.2f, 2.1f, 1)), lacquer);
            // Painted bracket band under the eaves.
            a.Add(Sdf.Box(q - new Vector3(0, 5.35f, -1.6f), new Vector3(5.8f, .2f, .5f)), greenTile);
            a.Add(Roof(q - new Vector3(0, 6.2f, -.4f), 7.4f, .9f, .85f, .24f, 2.8f), tile);
            a.Add(Roof(q - new Vector3(0, 7.9f, 0), 5, .8f, .7f, .2f, 2.1f), tile);
            a.Add(Sdf.Box(q - new Vector3(0, 7.05f, -.4f), new Vector3(4.4f, .55f, .8f)), lacquer);
            mt = a.M; return a.D;
        });
        // Peach trees in blossom.
        foreach (var (x, z, w, h) in new[] { (3.6f, 6.2f, 4.2f, 3.2f), (27, 6, 4.6f, 3.4f), (33.4f, 7.4f, 3.6f, 2.8f) })
        {
            var rnd = new Rand((int)(x * 61)); var lobes = Kit3D.Crown(rnd, new Vector3(x, -2.64f + h, z), w, h * .5f, 14, .4f);
            var (bmin, bmax) = Kit3D.LobeBounds(lobes, .5f);
            sc.Add(Vector3.Min(bmin, new Vector3(x - .6f, -2.8f, z - .6f)), Vector3.Max(bmax, new Vector3(x + .6f, -2.64f + h, z + .6f)), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(x, -2.64f, z);
                float t = Kit3D.Trunk(q, h * .8f, .22f, new[] { (new Vector3(0, h * .5f, 0), new Vector3(-w * .35f, h * .95f, 0), .1f), (new Vector3(0, h * .55f, 0), new Vector3(w * .38f, h, .1f), .09f) });
                float c = Kit3D.Canopy(n, p, lobes, .2f, (int)(x * 7));
                if (c < t) { mt = blossom; return c; }
                mt = bark; return t;
            });
        }
        // Red lanterns hanging along the eaves.
        sc.Add(new Vector3(7.6f, 2, 5), new Vector3(22.4f, 3.8f, 6.6f), (Vector3 p, out int mt) =>
        {
            var q = p - new Vector3(15, -2.64f, 8); var a = Acc.Start;
            float lx = Sdf.RepeatLimited(q.X, 2.4f, 3);
            var lq = new Vector3(lx, q.Y - 5.6f, q.Z + 2.4f);
            a.Add(Sdf.Capsule(lq, new Vector3(0, .9f, 0), new Vector3(0, .35f, 0), .01f), gold);
            a.Add(Sdf.Ellipsoid(lq, new Vector3(.22f, .3f, .22f)) + MathF.Abs(MathF.Sin(MathF.Atan2(lq.Z, lq.X) * 8)) * .006f, lantern);
            a.Add(MathF.Min(Sdf.CylinderY(lq - new Vector3(0, .3f, 0), .1f, .03f), Sdf.CylinderY(lq + new Vector3(0, .3f, 0), .1f, .03f)), gold);
            mt = a.M; return a.D;
        });
        // A bronze ding with incense smoke rising.
        sc.Add(new Vector3(4.4f, -2.8f, 3), new Vector3(6.6f, -1, 5), (Vector3 p, out int mt) =>
        {
            var q = p - new Vector3(5.5f, -2.64f, 4); var a = Acc.Start;
            float body = MathF.Max(Sdf.Ellipsoid(q - new Vector3(0, .75f, 0), new Vector3(.6f, .45f, .5f)), q.Y - 1.05f);
            float legs = Sdf.CylinderY(Sdf.RepeatAngleY(q, 3) - new Vector3(.38f, .25f, 0), .07f, .25f);
            float handles = Sdf.TorusX(new Vector3(MathF.Abs(q.X) - .45f, q.Y - 1.2f, q.Z), .13f, .035f);
            a.Add(MathF.Min(MathF.Min(body, legs), handles), bronze);
            mt = a.M; return a.D;
        });
        var smoke = sc.AddVolume(new Vector3(4.2f, -1.6f, 3), new Vector3(7.2f, 3.6f, 5), q =>
        {
            float sway = MathF.Sin((q.Y + 1.6f) * 1.4f) * .25f * (q.Y + 1.6f) * .3f;
            float dx = q.X - 5.5f - sway, dz = q.Z - 4, w = .12f + (q.Y + 1.6f) * .09f;
            float r = MathF.Sqrt(dx * dx + dz * dz) / w;
            return r < 1 ? (1 - r) * .6f * (n.Fbm(q, .25f, 3, 640) * .5f + .5f) * R.Smooth(3.6f, 1, q.Y) : 0;
        });
        smoke.Albedo = R.Rgb("e8e4e0"); smoke.Steps = 20; smoke.Ambient = 1.2f;
        var lt = Light(sky, 6, .012f); lt.HazeMax = .25f; lt.BloomStrength = .45f;
        return Bake.Layer(spec, sc, lt, cam);
    }

    PaintedLayer Near()
    {
        // An upturned eave corner hung with a lantern reaches into the top of the view when the camera climbs.
        var spec = new LayerSpec("near", .4f, 96, 40, 8.6f, 5, -94);
        var sc = new Scene(spec.Period); var n = sc.N; var m = new Mats(sc); var sky = MakeSky(spec.Period); var cam = new Camera(4);
        int tile = m.Tiles("d8a43a", .13f, .28f, 651, true);
        int lacquer = m.Paint("a8261c", .4f, .25f, 652);
        int lantern = sc.Material((Vector3 p, ref Surface s) => { s.Albedo = R.Rgb("c0281c"); s.Rough = .6f; s.Trans = .6f; s.Emit = R.Rgb("ff5a2a") * 2.4f; });
        int gold = m.Metal("e6b44a", .25f, .1f, "6b5a2e", 653);
        foreach (var (x, flip) in new[] { (4f, 1f), (30f, -1f) })
            sc.Add(new Vector3(x - 7, 8.6f, 0), new Vector3(x + 7, 13.6f, 4), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(x, 13.2f, 2); q.X *= flip; var a = Acc.Start;
                a.Add(Roof(q - new Vector3(-4, 0, 0), 6, 1.2f, 1.1f, .3f, 1.6f), tile);
                a.Add(Sdf.Box(q - new Vector3(-4.5f, -.2f, .3f), new Vector3(5, .25f, .4f)), lacquer);
                var lq = q - new Vector3(1.5f, -2.3f, -.6f);
                a.Add(Sdf.Capsule(lq, new Vector3(0, 1.6f, 0), new Vector3(0, .35f, 0), .012f), gold);
                a.Add(Sdf.Ellipsoid(lq, new Vector3(.26f, .34f, .26f)), lantern);
                mt = a.M; return a.D;
            });
        var lt = Light(sky, 2, .01f); lt.HazeMax = .15f; lt.BloomStrength = .4f;
        return Bake.Layer(spec, sc, lt, cam);
    }
}
