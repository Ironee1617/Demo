using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>Lantern Village walkway: a cobbled lane edged with a stone curb and a grass verge where glowing flowers
/// grow, an earth bank below, plank boardwalks lashed with rope that carry small lanterns underneath, and a lantern
/// post, barrels by a crate and a bush of glowing flowers as props.</summary>
public sealed class VillageTerrain : TerrainKit
{
    public override string Id => "village";
    public override Lighting Light() => VillageScene.TerrainLight();

    static readonly TerrainBake.Soil Bank = new() { Dark = "22201e", Light = "443a34", Pebble = "5e5a5c", PebbleDark = "34302e", Root = "1e1614", Pebbles = .18f, Roots = .4f, Seed = 71 };

    static int Flowers(Scene sc, Mats m, int seed)
    {
        var n = sc.N;
        int grass = m.Ground("24343a", "4a6a5e", "3a2e2c", seed, "d0b0f0", .08f);
        return m.Glyphs(grass, "8af0ff", 2.4f, p =>
        {
            var c = n.Voronoi(p, .12f, seed + 1);
            return R.Hash01((int)(c.Id & 0xffff), 0, 0, seed + 2) > .8f ? R.Smooth(.018f, .008f, c.F1) : 0;
        });
    }

    public override void Top(Scene sc, Mats m)
    {
        var n = sc.N;
        int cobble = m.Rock("4a4248", "8a8088", .25f, .8f, .2f, 1301);
        int curb = m.Rock("3a3c44", "6c6e78", .4f, .75f, .4f, 1302);
        int verge = Flowers(sc, m, 1303);
        int earth = m.Ground("1e2420", "34403a", "2a2224", 1304);
        sc.Add(new Vector3(-1, -.95f, -.95f), new Vector3(13, .2f, .8f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start;
            // Rounded cobbles set in the lane; their crowns form the walkable line.
            var c = n.Voronoi(new Vector3(p.X, 0, p.Z), .11f, 1305);
            float crown = R.Smooth(.0f, .06f, c.F2 - c.F1) * .025f;
            a.Add(MathF.Max(p.Y + .02f - crown, MathF.Max(-p.Y - .3f, MathF.Abs(p.Z - .02f) - .5f)), cobble);
            // A curb of dressed stones along the front edge.
            float bx = R.Mod(p.X, .46f) - .23f;
            float stone = Sdf.RoundBox(new Vector3(bx, p.Y + .07f, p.Z + .54f), new Vector3(.215f, .085f, .08f), .025f) + n.Fbm(p, .1f, 3, 1306) * .012f;
            a.Add(stone, curb);
            // A grass verge with glowing flowers behind the lane.
            float tuft = (n.Fbm(new Vector3(p.X * 3, 0, p.Z * 2), .1f, 3, 1307) * .5f + .5f) * .1f;
            a.Add(MathF.Max(MathF.Max(p.Y - .01f - tuft, .5f - p.Z), -p.Y - .3f) * .7f, verge);
            a.Add(MathF.Max(-.5f - p.Z + n.Fbm(p, .2f, 4, 1308) * .04f, p.Y + .14f), earth);
            mt = a.M; return a.D;
        });
    }

    void Deck(Scene sc, Mats m, float x0, float x1, bool end)
    {
        var n = sc.N;
        int plank = m.Wood("3a2a20", "7a5e44", .75f, false, 1311);
        int beam = m.Wood("2a1e16", "5a4432", .8f, false, 1312);
        int rope = m.Paint("a89070", .9f, .2f, 1313);
        int iron = m.Metal("2a2628", .45f, .5f, "3a3030", 1314);
        int lamp = m.Glow("ffb060", 6, "5a3414", .05f, 1315);
        float cx = (x0 + x1) * .5f, hw = (x1 - x0) * .5f;
        sc.Add(new Vector3(x0 - .3f, -1, -.8f), new Vector3(x1 + .3f, .05f, .8f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start; float qx = p.X - cx, ew = end ? hw - .04f : hw + .2f, limit = MathF.Abs(qx) - ew;
            // Planks laid across with small gaps and a little unevenness.
            float px = R.Mod(p.X, .2f) - .1f; int id = ((int)MathF.Floor(p.X / .2f) % 53 + 53) % 53;
            float lift = (R.Hash01(id, 0, 0, 1316) - .5f) * .012f;
            a.Add(MathF.Max(Sdf.RoundBox(new Vector3(px, p.Y + .05f - lift, p.Z), new Vector3(.092f, .05f, .6f), .012f), limit), plank);
            // Two stringers beneath and rope lashings at every crossbeam.
            float stringer = Sdf.Box(new Vector3(0, p.Y + .2f, MathF.Abs(p.Z) - .42f), new Vector3(1e3f, .1f, .07f));
            float cross = Sdf.Box(new Vector3(R.Mod(p.X, 1.5f) - .75f, p.Y + .2f, p.Z), new Vector3(.06f, .08f, .5f));
            a.Add(MathF.Max(MathF.Min(stringer, cross), limit - .06f), beam);
            float lash = Sdf.TorusX(new Vector3(R.Mod(p.X, 1.5f) - .75f, p.Y + .17f, MathF.Abs(p.Z) - .42f), .13f, .015f);
            if (MathF.Abs(qx) < ew - .1f) a.Add(MathF.Max(lash, MathF.Abs(R.Mod(p.X, 1.5f) - .75f) - .05f), rope);
            // A small lantern hangs under the front stringer every few planks.
            float lx = R.Mod(p.X - 1.5f, 3) - 1.5f;
            if (MathF.Abs(qx) < ew - .4f)
            {
                a.Add(Sdf.CylinderY(new Vector3(lx, p.Y + .42f, p.Z + .46f), .006f, .12f), iron);
                a.Add(Sdf.Ellipsoid(new Vector3(lx, p.Y + .66f, p.Z + .46f), new Vector3(.09f, .12f, .09f)), lamp);
                a.Add(Sdf.CylinderY(new Vector3(lx, p.Y + .54f, p.Z + .46f), .05f, .012f), iron);
            }
            // The end is finished with a thick capping beam.
            if (end) a.Add(Sdf.RoundBox(new Vector3(qx - hw + .08f, p.Y + .1f, p.Z), new Vector3(.08f, .14f, .62f), .02f) + n.Fbm(p, .1f, 3, 1317) * .01f, beam);
            mt = a.M; return a.D;
        });
    }

    public override void Slab(Scene sc, Mats m) => Deck(sc, m, -1, 7, false);
    public override void Cap(Scene sc, Mats m, float end) => Deck(sc, m, end - 1.9f, end, true);

    public override TileSample Body(TileNoise n, float x, float y)
    {
        var s = TerrainBake.Earth(n, x, y, Bank);
        // A few glowing mushrooms and spores seeded in the bank.
        var c = n.Voronoi(x, y, .45f, 1321);
        if (R.Hash01((int)(c.Id & 0xffff), 0, 0, 1322) > .92f) s.Emit += R.Rgb("6ae0ff") * R.Smooth(.035f, .015f, c.F1) * 1.4f;
        return s;
    }

    public override void Prop(int index, Scene sc, Mats m)
    {
        var n = sc.N; float cx = .75f;
        if (index == 0)
        {
            int post = m.Wood("2a1e16", "4a3626", .8f, true, 1331);
            int iron = m.Metal("2a2628", .45f, .5f, "3a3030", 1332);
            int lamp = m.Glow("ffb060", 7, "5a3414", .05f, 1333);
            sc.Add(new Vector3(cx - .4f, -.02f, -.3f), new Vector3(cx + .4f, 1.4f, .3f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(cx, 0, 0); var a = Acc.Start;
                a.Add(Sdf.Box(q - new Vector3(-.1f, .6f, 0), new Vector3(.05f, .6f, .05f)) + n.Fbm(p, .05f, 3, 1334) * .004f, post);
                a.Add(Sdf.Box(q - new Vector3(.05f, 1.16f, 0), new Vector3(.2f, .03f, .035f)), post);
                a.Add(Sdf.CylinderY(q - new Vector3(.2f, 1.06f, 0), .005f, .1f), iron);
                a.Add(Sdf.Ellipsoid(q - new Vector3(.2f, .84f, 0), new Vector3(.1f, .13f, .1f)), lamp);
                a.Add(Sdf.ConeY(q - new Vector3(.2f, .99f, 0), .03f, .1f, .03f), iron);
                mt = a.M; return a.D;
            });
        }
        else if (index == 1)
        {
            int wood = m.Wood("4a3424", "8a6a4a", .75f, true, 1341);
            int hoop = m.Metal("2a2628", .4f, .6f, "3a3030", 1342);
            int crate = m.Wood("5a4430", "9a7a58", .8f, false, 1343);
            sc.Add(new Vector3(cx - .65f, -.02f, -.4f), new Vector3(cx + .65f, .75f, .4f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(cx, 0, 0); var a = Acc.Start;
                var b = q - new Vector3(-.28f, .3f, 0);
                float bulge = .2f + .03f * (1 - b.Y * b.Y / .09f);
                float barrel = MathF.Max(MathF.Sqrt(b.X * b.X + b.Z * b.Z) - bulge, MathF.Abs(b.Y) - .3f);
                float staves = MathF.Sin(MathF.Atan2(b.Z, b.X) * 14);
                a.Add(barrel + MathF.Max(0, staves) * .003f, wood);
                a.Add(MathF.Max(MathF.Abs(MathF.Sqrt(b.X * b.X + b.Z * b.Z) - bulge - .006f) - .008f, MathF.Abs(MathF.Abs(b.Y) - .2f) - .025f), hoop);
                var c = Sdf.RotY(q - new Vector3(.3f, .22f, .02f), .3f);
                a.Add(Sdf.RoundBox(c, new Vector3(.24f, .22f, .22f), .01f), crate);
                a.Add(MathF.Max(Sdf.Box(c, new Vector3(.25f, .23f, .23f)), -Sdf.Box(c, new Vector3(.2f, .18f, .3f))), crate);
                mt = a.M; return a.D;
            });
        }
        else
        {
            int leaves = m.Foliage("1e3a34", "4a7a64", .5f, 1351);
            int bulb = m.Glow("6ad8f0", 2.6f, "2a4a5a", .05f, 1352);
            var rnd = new Rand(1353); var lobes = Kit3D.Crown(rnd, new Vector3(cx, .28f, 0), .9f, .45f, 9, .7f);
            var bulbs = new List<Vector3>(); var br = new Rand(1354);
            for (int k = 0; k < 9; k++) bulbs.Add(new Vector3(cx + br.R(-.4f, .4f), br.R(.35f, .72f), br.R(-.55f, -.35f)));
            var (bmin, bmax) = Kit3D.LobeBounds(lobes, .2f);
            sc.Add(Vector3.Min(bmin, new Vector3(cx - .6f, -.02f, -.4f)), Vector3.Max(bmax, new Vector3(cx + .6f, .9f, .4f)), (Vector3 p, out int mt) =>
            {
                var a = Acc.Start;
                a.Add(MathF.Max(Kit3D.Canopy(n, p, lobes, .08f, 1355), -p.Y), leaves);
                foreach (var c in bulbs)
                {
                    a.Add(Sdf.Sphere(p - c, .045f), bulb);
                    a.Add(Sdf.Capsule(p, c, new Vector3(c.X, c.Y - .2f, c.Z + .08f), .006f), leaves);
                }
                mt = a.M; return a.D;
            });
        }
    }
}
