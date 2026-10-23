using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>Dangun walkway: a turf edge over a fieldstone wall, mossy granite slabs trailing roots, and a stone
/// cairn, a small jangseung and a mossy boulder as props.</summary>
public sealed class DangunTerrain : TerrainKit
{
    public override string Id => "dangun";
    public override Lighting Light() => DangunScene.TerrainLight();

    static readonly TerrainBake.Rubble Fieldstone = new()
    {
        Size = .42f, Gap = .028f, Round = .1f, Moss = .4f, Light = "9a968a", Dark = "5a564e", Fill = "3e3428", MossHex = "4a6232", MossLight = "86985a", Seed = 31,
    };

    /// <summary>Rounded stones laid in two rough courses along x, repeating every period.</summary>
    static float Stones(Noise n, Vector3 p, float top, float bottom, float period, int seed)
    {
        float d = 1e9f; float rowH = (top - bottom) / 2;
        for (int row = 0; row < 2; row++)
        {
            float cy = top - rowH * (row + .5f), shift = row * .27f;
            float cell = R.Mod(p.X + shift, .55f) - .275f; int id = (int)MathF.Floor((p.X + shift) / .55f);
            id = ((id % 97) + 97) % 97;
            float w = .22f + R.Hash01(id, row, 0, seed) * .06f, h = rowH * .45f + R.Hash01(id, row, 1, seed) * rowH * .08f;
            d = MathF.Min(d, Sdf.RoundBox(new Vector3(cell, p.Y - cy, p.Z + .5f), new Vector3(w, h, .14f), .07f));
        }
        return d + n.Fbm(p, .12f, 3, seed + 1) * .02f;
    }

    public override void Top(Scene sc, Mats m)
    {
        var n = sc.N;
        int turf = m.Ground("3e5028", "7e8e4e", "5e4c36", 401, "f0e6a8", .15f);
        int stone = m.Rock("5a564e", "9e9a8e", .6f, .85f, .5f, 402, "4e6634", .45f);
        int earth = m.Ground("3a3020", "5a4a32", "4a3a28", 403);
        sc.Add(new Vector3(-1, -.95f, -.95f), new Vector3(13, .2f, .8f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start;
            // Turf: its upper face is the walkable line; grass tufts break the front edge.
            float lump = n.Fbm(new Vector3(p.X, 0, p.Z), .3f, 3, 404) * .02f;
            float turfD = Sdf.Box(p - new Vector3(6, -.07f, -.02f), new Vector3(7, .07f + lump, .6f)) - .02f;
            float tuft = MathF.Max(p.Y - .02f - (n.Fbm(new Vector3(p.X * 3, 0, p.Z), .12f, 3, 405) * .5f + .5f) * .1f, MathF.Max(MathF.Abs(p.Z + .5f) - .1f, -p.Y - .02f));
            a.Add(MathF.Min(turfD, tuft * .7f), turf);
            a.Add(Sdf.Box(p - new Vector3(6, -.22f, 0), new Vector3(7, .1f, .56f)), earth);
            a.Add(Stones(n, p, -.16f, -.86f, 12, 406), stone);
            mt = a.M; return a.D;
        });
    }

    void SlabShape(Scene sc, Mats m, float x0, float x1, bool end)
    {
        var n = sc.N;
        int granite = m.Rock("5e5c56", "a29e94", .7f, .85f, .5f, 411, "4e6634", .55f);
        int root = m.Wood("3a2a1c", "6a4e34", .9f, true, 412);
        int moss = m.Foliage("34482a", "6e8448", .3f, 413);
        float cx = (x0 + x1) * .5f, hw = (x1 - x0) * .5f;
        sc.Add(new Vector3(x0 - .4f, -1, -.75f), new Vector3(x1 + .4f, .1f, .75f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start; var q = p - new Vector3(cx, 0, 0);
            float slab = Sdf.RoundBox(q - new Vector3(0, -.2f, 0), new Vector3(hw - (end ? .02f : -.2f), .2f, .56f), end ? .12f : .04f);
            slab += n.Fbm(p, .25f, 4, 414) * .03f;
            a.Add(slab, granite);
            // Moss cushions on the top edge.
            a.Add(Sdf.Box(q - new Vector3(0, .0f, -.3f), new Vector3(hw, .03f, .25f)) + (n.Fbm(p, .2f, 3, 415) * .5f + .5f) * .06f - .02f, moss);
            // Roots trailing from the underside.
            for (int k = 0; k < 6; k++)
            {
                float rx = x0 + (k + .5f) / 6 * (x1 - x0) + MathF.Sin(k * 2.3f) * .2f;
                if (end && rx > x1 - .25f) continue;
                float len = .25f + R.Hash01(k, (int)x0, 0, 416) * .45f;
                var top = new Vector3(rx, -.36f, -.3f + (k % 3) * .2f);
                var mid = top + new Vector3(MathF.Sin(k) * .08f, -len * .5f, -.05f);
                var tip = mid + new Vector3(MathF.Cos(k) * .1f, -len * .5f, 0);
                a.Add(MathF.Min(Sdf.RoundCone(p, top, mid, .04f, .025f), Sdf.RoundCone(p, mid, tip, .025f, .008f)), root);
            }
            mt = a.M; return a.D;
        });
    }

    public override void Slab(Scene sc, Mats m) => SlabShape(sc, m, -1, 7, false);
    public override void Cap(Scene sc, Mats m, float end) => SlabShape(sc, m, end - 1.9f, end, true);
    public override TileSample Body(TileNoise n, float x, float y) => TerrainBake.Fieldstone(n, x, y, Fieldstone);

    public override void Prop(int index, Scene sc, Mats m)
    {
        var n = sc.N; float cx = .75f;
        if (index == 0)
        {
            int granite = m.Rock("5e5c56", "a29e94", .5f, .85f, .5f, 421, "4e6634", .25f);
            sc.Add(new Vector3(cx - .5f, -.02f, -.5f), new Vector3(cx + .5f, 1.3f, .5f), (Vector3 p, out int mt) =>
            {
                mt = granite; var q = p - new Vector3(cx, 0, 0); float d = 1e9f, y = 0; var rr = new Rand(422);
                for (int k = 0; k < 8 && y < 1.15f; k++)
                {
                    float r = .3f * (1 - y / 1.3f * .7f) * rr.R(.85f, 1.1f);
                    d = MathF.Min(d, Sdf.Ellipsoid(q - new Vector3(rr.R(-.04f, .04f), y + r * .42f, 0), new Vector3(r, r * .45f, r * .8f)));
                    y += r * .78f;
                }
                return d + n.Fbm(p, .08f, 3, 423) * .012f;
            });
        }
        else if (index == 1)
        {
            int wood = m.Wood("4a3624", "8a6a48", .8f, true, 431);
            int black = m.Paint("1e1a16", .6f, .3f, 432), white = m.Paint("ece6d8", .6f, .3f, 433), red = m.Paint("b0302a", .6f, .3f, 434);
            sc.Add(new Vector3(cx - .3f, -.02f, -.3f), new Vector3(cx + .3f, 1.35f, .3f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(cx, 0, 0); var a = Acc.Start;
                float post = Sdf.CylinderY(q - new Vector3(0, .58f, 0), .13f, .58f) + n.Fbm(p, .06f, 3, 435) * .006f;
                a.Add(post, wood);
                a.Add(MathF.Min(Sdf.CylinderY(q - new Vector3(0, 1.26f, 0), .12f, .1f), Sdf.CylinderY(q - new Vector3(0, 1.16f, 0), .2f, .015f)), black);
                if (q.Z < -.06f && q.Y > .7f && q.Y < 1.14f)
                {
                    float fy = q.Y - .92f; int f = -1;
                    foreach (float sx in new[] { -1f, 1f }) { float ex = q.X - sx * .05f; if (ex * ex + (fy - .07f) * (fy - .07f) < .0012f) f = 1; }
                    if (MathF.Abs(fy + .12f) < .025f && MathF.Abs(q.X) < .08f) f = R.Fract(q.X * 30) < .5f ? 1 : 0;
                    if (MathF.Abs(fy - .15f + MathF.Abs(q.X) * .4f) < .012f && MathF.Abs(q.X) < .1f) f = 0;
                    if (f >= 0) a.Add(post - .001f, f == 1 ? white : black);
                }
                if (q.Z < -.06f && q.Y > .1f && q.Y < .66f && MathF.Abs(q.X) < .05f) a.Add(post - .001f, red);
                mt = a.M; return a.D;
            });
        }
        else
        {
            int rock = m.Rock("5a564e", "9a968a", .5f, .85f, .5f, 441, "4e6634", .6f);
            int grass = m.Foliage("344a24", "7a9048", .35f, 442, "f0e0a0", .2f);
            sc.Add(new Vector3(cx - .7f, -.02f, -.5f), new Vector3(cx + .7f, .9f, .5f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(cx, 0, 0); var a = Acc.Start;
                a.Add(Sdf.Ellipsoid(q - new Vector3(0, .28f, 0), new Vector3(.5f, .4f, .38f)) + n.Fbm(p, .2f, 4, 443) * .06f, rock);
                a.Add(Sdf.Ellipsoid(q - new Vector3(-.45f, .08f, -.1f), new Vector3(.28f, .14f, .2f)) + n.Fbm(p, .04f, 3, 444) * .03f, grass);
                a.Add(Sdf.Ellipsoid(q - new Vector3(.5f, .06f, -.05f), new Vector3(.22f, .1f, .18f)) + n.Fbm(p, .04f, 3, 445) * .03f, grass);
                mt = a.M; return a.D;
            });
        }
    }
}
