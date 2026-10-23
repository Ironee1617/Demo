using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>Olympus walkway: a marble pavement with a moulded cornice, dentils and a triglyph frieze over weathered
/// ashlar, marble slabs carried on billowing cloud, and an amphora, a broken column and a laurel bush as props.</summary>
public sealed class OlympusTerrain : TerrainKit
{
    public override string Id => "olympus";
    public override bool OpenBelow => true;
    public override Lighting Light() => OlympusScene.TerrainLight();

    static readonly TerrainBake.Masonry Ashlar = new()
    {
        Course = .5f, MinLen = 1f, MaxLen = 1.8f, Light = "ebe6dc", Dark = "a99e8c", Mortar = "b2a998", Dirt = "5e5648",
        Joint = .012f, Bevel = .05f, Chips = 1, Grime = .45f, Streaks = .5f, Veins = .3f, Vein = "8f8676", Rough = .6f, Stains = .25f, Stain = "8a8468", Seed = 11,
    };

    public override void Top(Scene sc, Mats m)
    {
        var rnd = new Rand(201);
        int marble = m.Marble("e6ddcd", "9a8e7c", .6f, .5f, 201);
        int paving = m.Marble("ddd2c0", "958a78", .8f, .55f, 202);
        int shade = m.Marble("cfc3b0", "8c8070", .6f, .5f, 203);
        int laurel = m.Foliage("223a20", "5e7c42", .35f, 204);
        float[] pave = TerrainBake.Cuts(rnd, 12, 1.1f, 1.7f);
        sc.Add(new Vector3(-1, -.9f, -.9f), new Vector3(13, .05f, .8f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start;
            float slab = Sdf.Box(p - new Vector3(6, -.06f, 0), new Vector3(7, .06f, .6f)) - .004f;
            slab += TerrainBake.JointGroove(p.X, pave, 12, .02f) * .014f + R.Smooth(.02f, 0, MathF.Abs(p.Z - .05f)) * .01f;
            a.Add(slab, paving);
            // Cornice: a rounded lip under the pavement edge.
            a.Add(Sdf.Box(p - new Vector3(6, -.17f, -.6f), new Vector3(7, .07f, .08f)) - .025f, marble);
            // Dentils.
            float dx = R.Mod(p.X, .16f) - .08f;
            a.Add(Sdf.Box(new Vector3(dx, p.Y + .3f, p.Z + .57f), new Vector3(.045f, .045f, .05f)) - .004f, marble);
            a.Add(Sdf.Box(p - new Vector3(6, -.3f, -.42f), new Vector3(7, .06f, .14f)), shade);
            // Frieze of triglyphs (three grooves) and plain metopes.
            float frieze = Sdf.Box(p - new Vector3(6, -.52f, -.4f), new Vector3(7, .16f, .16f));
            float cell = R.Mod(p.X, 1.2f);
            bool glyph = cell < .32f;
            if (glyph) frieze -= .02f;
            float groove = glyph ? R.Smooth(.025f, 0, MathF.Abs(R.Mod(cell - .04f, .08f) - .04f)) * R.Smooth(-.38f, -.4f, p.Y) * R.Smooth(-.66f, -.64f, p.Y) : 0;
            a.Add(frieze + groove * .02f, glyph ? marble : shade);
            // A plain fillet closes the frieze; the ashlar body tile continues below it.
            a.Add(Sdf.Box(p - new Vector3(6, -.71f, -.58f), new Vector3(7, .03f, .04f)) - .008f, marble);
            // Laurel sprigs in a few pavement joints at the back.
            foreach (float c in new[] { pave[0], pave[pave.Length / 2] })
            {
                var q = new Vector3(R.Wrap(p.X, c, 12), p.Y, p.Z - .42f);
                if (MathF.Abs(q.X) < .4f)
                {
                    float bush = Sdf.Ellipsoid(q - new Vector3(0, .02f, 0), new Vector3(.22f, .1f, .12f)) + sc.N.Fbm(p, .05f, 3, 205) * .03f;
                    a.Add(bush * .7f, laurel);
                }
            }
            mt = a.M; return a.D;
        });
    }

    /// <summary>Cloud puffs along x (duplicated across the wrap) under a slab.</summary>
    static (Vector3, float)[] Puffs(float period, float y, float spacing, float r, int seed, float from = 0, float to = float.MaxValue)
    {
        var rnd = new Rand(seed); var list = new List<(Vector3, float)>();
        for (float x = 0; x < period; x += spacing)
        {
            var c = new Vector3(x + rnd.R(-.12f, .12f), y + rnd.R(-.07f, .05f), rnd.R(-.15f, .15f)); float rr = r * rnd.R(.75f, 1.2f);
            foreach (float o in new[] { 0f, period, -period }) if (c.X + o > from && c.X + o < to) list.Add((c + new Vector3(o, 0, 0), rr));
        }
        return list.ToArray();
    }

    void SlabShape(Scene sc, Mats m, float x0, float x1, bool end, (Vector3, float)[] puffs)
    {
        int marble = m.Marble("e6ddcd", "9a8e7c", .6f, .5f, 211);
        int gold = m.Metal("d8a850", .3f, .15f, "6b5a2e", 212);
        int cloud = m.Cloud("fff8f0", "c4bfd4", 213);
        var n = sc.N; float cx = (x0 + x1) * .5f, hw = (x1 - x0) * .5f;
        sc.Add(new Vector3(x0 - .4f, -.98f, -.7f), new Vector3(x1 + .4f, .05f, .7f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start; var q = p - new Vector3(cx, 0, 0);
            float top = Sdf.Box(q - new Vector3(0, -.08f, 0), new Vector3(hw - (end ? .08f : 0), .08f, .58f)) - .006f;
            if (end) top = MathF.Min(top, Sdf.CylinderZ(q - new Vector3(hw - .08f, -.08f, 0), .08f, .58f));
            a.Add(top, marble);
            a.Add(Sdf.Box(q - new Vector3(0, -.2f, -.04f), new Vector3(hw - (end ? .16f : 0), .045f, .5f)) - .01f, marble);
            a.Add(Sdf.Box(q - new Vector3(0, -.255f, -.5f), new Vector3(hw - (end ? .16f : 0), .012f, .03f)), gold);
            // Billowing cloud that carries the slab.
            float c = Kit3D.Canopy(n, p, puffs, .07f, 214);
            a.Add(c, cloud);
            mt = a.M; return a.D;
        });
    }

    public override void Slab(Scene sc, Mats m) => SlabShape(sc, m, -1, 7, false, Puffs(6, -.48f, .5f, .3f, 215));

    public override void Cap(Scene sc, Mats m, float end)
        => SlabShape(sc, m, end - 1.9f, end, true, Puffs(4, -.48f, .5f, .3f, 215, end - 2.2f, end - .1f).Append((new Vector3(end - .1f, -.42f, 0), .26f)).ToArray());

    public override TileSample Body(TileNoise n, float x, float y) => TerrainBake.Stone(n, x, y, Ashlar);

    public override void Prop(int index, Scene sc, Mats m)
    {
        var n = sc.N; float cx = .75f;
        if (index == 0)
        {
            int clay = m.Paint("a85a34", .6f, .25f, 221, .04f);
            int black = m.Paint("1e1714", .35f, .1f, 222, .06f);
            sc.Add(new Vector3(cx - .4f, -.02f, -.4f), new Vector3(cx + .4f, 1.2f, .4f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(cx, 0, 0); float y = q.Y;
                // Lathe profile: foot, swelling belly, shoulder, neck and lip.
                float r = y < .06f ? .13f : y < .2f ? R.Mix(.1f, .2f, (y - .06f) / .14f) : y < .7f ? .2f + MathF.Sin((y - .2f) / .5f * MathF.PI) * .09f - (y - .2f) * .05f
                        : y < .82f ? R.Mix(.17f, .08f, (y - .7f) / .12f) : y < .98f ? .075f : .11f;
                float body = (MathF.Sqrt(q.X * q.X + q.Z * q.Z) - r) * .8f;
                body = MathF.Max(body, MathF.Max(-y, y - 1.03f));
                float handles = Sdf.TorusZ(new Vector3(MathF.Abs(q.X) - .17f, y - .82f, q.Z), .09f, .018f);
                handles = MathF.Max(handles, -(MathF.Abs(q.X) - .17f));
                float d = MathF.Min(body, handles);
                // Black-figure bands: a broad belly band with clay pinstripes, a black neck and foot.
                bool band = (y > .36f && y < .6f && MathF.Abs(y - .48f) > .012f) || (y > .74f && y < .98f) || y < .05f;
                mt = band && body < .02f ? black : clay;
                return d;
            });
        }
        else if (index == 1)
        {
            int marble = m.Marble("dfd6c6", "958a78", .6f, .55f, 231);
            int moss = m.Foliage("2e4a26", "6e8a4a", .3f, 232);
            sc.Add(new Vector3(cx - .5f, -.02f, -.5f), new Vector3(cx + .5f, 1.05f, .5f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(cx, 0, 0);
                // A broken column stump on its plinth, its top snapped at an angle.
                float col = Kit3D.Column(q, .26f, 2.4f, 20, true);
                float cut = q.Y - .78f - q.X * .35f - n.Fbm(q, .12f, 3, 233) * .12f;
                var a = Acc.Start; a.Add(MathF.Max(col, cut), marble);
                a.Add(Sdf.Ellipsoid(q - new Vector3(-.08f, .72f, 0), new Vector3(.2f, .05f, .2f)) + n.Fbm(p, .04f, 3, 234) * .03f, moss);
                mt = a.M; return a.D;
            });
        }
        else
        {
            int marble = m.Marble("e2d9ca", "958a78", .6f, .5f, 241);
            int bronze = m.Metal("a8743e", .32f, .5f, "3f6b5a", 242);
            sc.Add(new Vector3(cx - .45f, -.02f, -.45f), new Vector3(cx + .45f, 1.05f, .45f), (Vector3 p, out int mt) =>
            {
                var q = p - new Vector3(cx, 0, 0); var a = Acc.Start;
                // Moulded marble pedestal carrying a bronze offering bowl.
                a.Add(Sdf.Box(q - new Vector3(0, .05f, 0), new Vector3(.3f, .05f, .3f)) - .01f, marble);
                a.Add(Sdf.Box(q - new Vector3(0, .38f, 0), new Vector3(.22f, .3f, .22f)) - .01f, marble);
                a.Add(Sdf.Box(q - new Vector3(0, .71f, 0), new Vector3(.28f, .04f, .28f)) - .01f, marble);
                float bowl = MathF.Max(Sdf.Sphere(q - new Vector3(0, .98f, 0), .24f), q.Y - .98f);
                bowl = Sdf.Subtract(bowl, Sdf.Sphere(q - new Vector3(0, 1f, 0), .21f));
                a.Add(MathF.Min(bowl, Sdf.CylinderY(q - new Vector3(0, .79f, 0), .07f, .04f)), bronze);
                mt = a.M; return a.D;
            });
        }
    }
}
