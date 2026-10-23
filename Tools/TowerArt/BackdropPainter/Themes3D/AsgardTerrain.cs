using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>Asgard walkway: snow-capped dark granite with a faintly glowing rune course, frost-touched ashlar,
/// iron-banded timber beams hung with icicles, and a rune stone, a shield and a snowy fir as props.</summary>
public sealed class AsgardTerrain : TerrainKit
{
    public override string Id => "asgard";
    public override Lighting Light() => AsgardScene.TerrainLight();

    static readonly TerrainBake.Masonry Granite = new()
    {
        Course = .75f, MinLen = 1.3f, MaxLen = 2.4f, Light = "727c8c", Dark = "4a5260", Mortar = "4a525c", Dirt = "2a3038",
        Joint = .01f, Bevel = .05f, Chips = 1.1f, Grime = .35f, Streaks = .35f, Rough = .8f, Stains = .45f, Stain = "b8c6d6", Hewn = .016f, Veins = .15f, Vein = "9aa6b6", Seed = 21,
    };

    /// <summary>A lumpy snow cap whose top sits on the walkable line, with drips over the front edge.</summary>
    static float SnowCap(Noise n, Vector3 p, float x0, float x1, float period)
    {
        float lump = n.Fbm(new Vector3(p.X, 0, p.Z), .35f, 3, 301) * .025f;
        float cap = Sdf.Box(p - new Vector3((x0 + x1) * .5f, -.05f, -.02f), new Vector3((x1 - x0) * .5f, .05f + lump, .62f)) - .035f;
        float drip = R.Mod(p.X, .27f) - .135f; int id = (int)MathF.Floor(p.X / .27f);
        float len = .05f + R.Hash01(((id % 97) + 97) % 97, 0, 0, 302) * .09f;
        cap = R.SMin(cap, Sdf.Capsule(new Vector3(drip, p.Y, p.Z + .64f), new Vector3(0, -.06f, 0), new Vector3(0, -.06f - len, 0), .028f), .04f);
        return cap;
    }

    public override void Top(Scene sc, Mats m)
    {
        var n = sc.N;
        int granite = m.Rock("3e4654", "707a8a", .8f, .85f, .5f, 303);
        int runes = m.Glyphs(granite, "56e8ff", 1.4f, p => p.Z < -.5f && p.Y < -.28f && p.Y > -.62f ? Kit3D.Runes(p.X, p.Y + .62f, .2f, .3f, .018f, 304) : 0);
        int snow = m.Snow(305);
        sc.Add(new Vector3(-1, -.95f, -.95f), new Vector3(13, .08f, .8f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start;
            a.Add(SnowCap(n, p, -1, 13, 12), snow);
            // Capstones and a recessed rune course under them.
            float cap = Sdf.Box(p - new Vector3(6, -.17f, 0), new Vector3(7, .08f, .6f)) - .01f + n.Fbm(p, .25f, 3, 306) * .015f;
            a.Add(cap, granite);
            float course = Sdf.Box(p - new Vector3(6, -.46f, -.04f), new Vector3(7, .18f, .52f)) + n.Fbm(p, .3f, 3, 307) * .012f;
            a.Add(course, runes);
            a.Add(Sdf.Box(p - new Vector3(6, -.7f, 0), new Vector3(7, .05f, .58f)) - .012f + n.Fbm(p, .25f, 3, 308) * .012f, granite);
            mt = a.M; return a.D;
        });
    }

    void Beam(Scene sc, Mats m, float x0, float x1, bool end)
    {
        var n = sc.N;
        int wood = m.Wood("2a1d14", "5c4230", .75f, false, 311);
        int iron = m.Metal("40444a", .45f, .5f, "3a2a1a", 312);
        int snow = m.Snow(313);
        int ice = sc.Material((Vector3 p, ref Surface s) => { s.Albedo = R.Rgb("a8d2ee"); s.Rough = .1f; s.Spec = .05f; s.Trans = .6f; s.Emit = R.Rgb("2a5a7a") * .35f; });
        float cx = (x0 + x1) * .5f, hw = (x1 - x0) * .5f;
        sc.Add(new Vector3(x0 - .3f, -1, -.75f), new Vector3(x1 + .3f, .08f, .75f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start; var q = p - new Vector3(cx, 0, 0);
            a.Add(SnowCap(n, p, x0, x1 - (end ? .06f : 0), 6), snow);
            float beam = Sdf.RoundBox(q - new Vector3(0, -.26f, 0), new Vector3(hw - (end ? .04f : 0), .18f, .5f), .05f);
            a.Add(beam, wood);
            // Iron straps every metre and a riveted end plate.
            float strap = Sdf.Box(new Vector3(R.Mod(p.X, 1.5f) - .75f, q.Y + .26f, q.Z), new Vector3(.045f, .2f, .52f)) - .01f;
            a.Add(end && q.X > hw - .2f ? 9 : strap, iron);
            if (end) a.Add(Sdf.Box(q - new Vector3(hw - .03f, -.26f, 0), new Vector3(.04f, .2f, .52f)) - .01f, iron);
            // Icicles beneath.
            float cell = R.Mod(p.X, .22f) - .11f; int id = (int)MathF.Floor(p.X / .22f) % 64;
            float len = .12f + R.Hash01(id < 0 ? id + 64 : id, 3, 0, 314) * .42f;
            var iq = new Vector3(cell, q.Y + .44f + len * .5f, q.Z + .42f - R.Hash01(id < 0 ? id + 64 : id, 4, 0, 314) * .3f);
            if (MathF.Abs(q.X) < hw - .1f) a.Add(Sdf.ConeY(iq, len * .5f, .004f, .03f + len * .03f), ice);
            mt = a.M; return a.D;
        });
    }

    public override void Slab(Scene sc, Mats m) => Beam(sc, m, -1, 7, false);
    public override void Cap(Scene sc, Mats m, float end) => Beam(sc, m, end - 1.9f, end, true);
    public override TileSample Body(TileNoise n, float x, float y) => TerrainBake.Stone(n, x, y, Granite);

    public override void Prop(int index, Scene sc, Mats m)
    {
        var n = sc.N; float cx = .75f;
        if (index == 0)
        {
            int stone = m.Snowy(m.Rock("3a4250", "6a7484", .7f, .85f, .5f, 321), 1);
            int runes = m.Glyphs(stone, "56e8ff", 2, p => Kit3D.Runes(p.X * 1.3f, p.Y, .14f, .22f, .016f, 322));
            sc.Add(new Vector3(cx - .4f, -.02f, -.3f), new Vector3(cx + .4f, 1.3f, .3f), (Vector3 p, out int mt) =>
            {
                mt = runes; var q = p - new Vector3(cx, 0, 0);
                return Sdf.RoundBox(q - new Vector3(0, .6f, 0), new Vector3(.27f - q.Y * .05f, .6f, .1f), .06f) + n.Fbm(p, .2f, 4, 323) * .03f;
            });
        }
        else if (index == 1)
        {
            int paint = m.Paint("8e2c24", .55f, .4f, 331);
            int stripe = m.Paint("d8c8a0", .55f, .4f, 332);
            int iron = m.Metal("4a4c50", .4f, .5f, "3a2a1a", 333);
            int wood = m.Wood("3a2a1c", "6a4e36", .8f, true, 334);
            sc.Add(new Vector3(cx - .6f, -.02f, -.4f), new Vector3(cx + .6f, 1.2f, .4f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(cx, 0, 0); var a = Acc.Start;
                // A round shield leaning back against an axe planted in the ground.
                var sq = Sdf.RotX(q - new Vector3(-.05f, .44f, .05f), -.25f);
                float face = Sdf.CylinderZ(sq, .42f, .035f);
                float ang = MathF.Atan2(sq.Y, sq.X);
                bool band = MathF.Sin(ang * 3) > .2f && new Vector2(sq.X, sq.Y).Length() < .39f;
                a.Add(face, band ? stripe : paint);
                a.Add(Sdf.TorusZ(sq, .42f, .03f), iron);
                a.Add(Sdf.Sphere(sq - new Vector3(0, 0, -.05f), .09f), iron);
                a.Add(Sdf.Capsule(q, new Vector3(.34f, 0, .15f), new Vector3(.3f, 1.1f, .15f), .025f), wood);
                a.Add(Sdf.Box(q - new Vector3(.4f, 1f, .15f), new Vector3(.12f, .08f, .015f)), iron);
                mt = a.M; return a.D;
            });
        }
        else
        {
            int fir = m.Snowy(m.Foliage("10201a", "2e4c38", .25f, 341), .9f);
            sc.Add(new Vector3(cx - .6f, -.02f, -.6f), new Vector3(cx + .6f, 1.4f, .6f), (Vector3 p, out int mt) => { mt = fir; return Kit3D.Pine(n, p - new Vector3(cx, 0, 0), 1.3f, 1f, 5, 342); });
        }
    }
}
