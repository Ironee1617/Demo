using System.Numerics;
using BackdropPainter.Render;

namespace BackdropPainter;

/// <summary>Abyss walkway: a crust of bruised flesh whose front edge is a gum set with teeth, muscle and buried bone
/// below, platforms made of a spine with vertebrae and ribs curling beneath, and an eye stalk, a skull and a bone
/// stake as props.</summary>
public sealed class AbyssTerrain : TerrainKit
{
    public override string Id => "abyss";
    public override Lighting Light() => AbyssScene.TerrainLight();

    public override void Top(Scene sc, Mats m)
    {
        var n = sc.N;
        int crust = AbyssScene.Flesh(sc, 1001, "1a040a", "42121e");
        int gum = AbyssScene.Flesh(sc, 1002, "2e0812", "6a2232");
        int tooth = AbyssScene.Bone(sc, 1003);
        sc.Add(new Vector3(-1, -.95f, -.95f), new Vector3(13, .1f, .8f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start;
            float lumps = n.Fbm(p, .3f, 4, 1004) * .035f + MathF.Sin(p.X * 5 + n.Fbm(p, 1, 2, 1005) * 4) * .006f;
            a.Add(Sdf.Box(p - new Vector3(6, -.12f, 0), new Vector3(7, .12f, .62f)) + lumps, crust);
            // A swollen gum along the front edge with a row of teeth pointing down.
            float gumD = Sdf.CylinderX(new Vector3(p.X - 6, p.Y + .26f, p.Z + .56f) * new Vector3(1, 1, .8f), .17f, 7) + n.Fbm(p, .15f, 3, 1006) * .03f;
            a.Add(gumD, gum);
            float cell = R.Mod(p.X, .17f) - .085f; int id = ((int)MathF.Floor(p.X / .17f) % 71 + 71) % 71;
            float len = .1f + R.Hash01(id, 0, 0, 1007) * .14f, tilt = (R.Hash01(id, 1, 0, 1007) - .5f) * .3f;
            var q = Sdf.RotZ(new Vector3(cell, p.Y + .36f, p.Z + .66f), tilt);
            a.Add(Sdf.ConeY(q + new Vector3(0, len * .5f, 0), len * .5f, .012f, .05f), tooth);
            // Muscle hanging below the gum.
            a.Add(MathF.Max(-.52f - p.Z + n.Fbm(p, .2f, 3, 1008) * .06f, p.Y + .3f), crust);
            mt = a.M; return a.D;
        });
    }

    void Clod(Scene sc, float x0, float x1, bool end)
    {
        var n = sc.N;
        int flesh = AbyssScene.Flesh(sc, 1011, "1a040a", "42121e");
        int bone = AbyssScene.Bone(sc, 1012);
        int sinew = AbyssScene.Flesh(sc, 1013, "2e0610", "6a1e2e");
        float cx = (x0 + x1) * .5f, hw = (x1 - x0) * .5f;
        sc.Add(new Vector3(x0 - .4f, -1, -.85f), new Vector3(x1 + .4f, .1f, .8f), (Vector3 p, out int mt) =>
        {
            var a = Acc.Start; var q = p - new Vector3(cx, 0, 0); float ew = hw - (end ? .05f : -.2f);
            // A lump of bruised flesh tapering downward, its top the walkable line.
            float depth = .45f + n.Fbm(new Vector3(p.X, 0, 0), .5f, 3, 1014) * .12f;
            float clod = MathF.Max(Sdf.RoundBox(q - new Vector3(0, -.3f, 0), new Vector3(ew, .3f, .58f), end ? .2f : .06f), -(p.Y + depth) - MathF.Abs(q.Z) * .25f);
            a.Add(clod + n.Fbm(p, .15f, 4, 1015) * .035f, flesh);
            if (MathF.Abs(q.X) < ew - .08f)
            {
                // A spine of vertebra knobs breaking through the front face.
                float vx = R.Mod(p.X, .26f) - .13f;
                a.Add(Sdf.Ellipsoid(new Vector3(vx, p.Y + .15f, p.Z + .6f), new Vector3(.1f, .085f, .08f)) + n.Fbm(p, .05f, 2, 1016) * .006f, bone);
                // Ribs hooking down and out from the spine.
                float rx = R.Mod(p.X + .13f, .52f) - .26f;
                var rq = new Vector3(rx, p.Y, p.Z);
                float rib = R.SMin(Sdf.RoundCone(rq, new Vector3(-.1f, -.2f, -.64f), new Vector3(.02f, -.44f, -.66f), .028f, .022f),
                    Sdf.RoundCone(rq, new Vector3(.02f, -.44f, -.66f), new Vector3(.2f, -.58f, -.6f), .022f, .008f), .02f);
                a.Add(rib, bone);
                // Torn sinews and drips hanging from the underside.
                float sx = R.Mod(p.X + .37f, .74f) - .37f; int id = ((int)MathF.Floor((p.X + .37f) / .74f) % 37 + 37) % 37;
                float hang = .15f + R.Hash01(id, 0, 0, 1017) * .35f, sz = -.2f - R.Hash01(id, 1, 0, 1017) * .3f;
                a.Add(Sdf.RoundCone(new Vector3(sx, p.Y, p.Z), new Vector3(0, -depth + .06f, sz), new Vector3(.03f, -depth - hang, sz - .03f), .035f, .01f), sinew);
            }
            // The end tapers into a jagged broken bone.
            if (end) a.Add(Sdf.ConeY(Sdf.RotZ(new Vector3(q.X - hw + .1f, p.Y + .22f, p.Z + .3f), -1.2f), .2f, .07f, .005f), bone);
            mt = a.M; return a.D;
        });
    }

    public override void Slab(Scene sc, Mats m) => Clod(sc, -1, 7, false);
    public override void Cap(Scene sc, Mats m, float end) => Clod(sc, end - 1.9f, end, true);

    public override TileSample Body(TileNoise n, float x, float y)
    {
        // Muscle fibres in folds, bruised and veined, pooled with dark blood.
        float fibre = MathF.Sin(x * 34 + n.Fbm(x, y, .9f, 3, 1021) * 14) * .5f + .5f;
        float fold = n.Fbm(x, y, .7f, 4, 1022) * .5f + .5f;
        float bruise = R.Smooth(.5f, .75f, n.Fbm(x, y, 1.6f, 3, 1023) * .5f + .5f);
        float vein = R.Smooth(.9f, .97f, n.Ridged(x, y, .5f, 4, 1025));
        var col = R.Mix(R.Rgb("16040a"), R.Rgb("4a1220"), fold * .7f + fibre * .2f);
        col = R.Mix(col, R.Rgb("24102c"), bruise * .6f);
        col = R.Mix(col, R.Rgb("3a1240"), vein * .6f);
        float blood = R.Smooth(.62f, .7f, n.Fbm(x, y, 1.4f, 3, 1026) * .5f + .5f);
        col = R.Mix(col, R.Rgb("120206"), blood * .8f);
        float h = fold * .05f + fibre * .006f - vein * .01f;
        return new TileSample { H = h, Albedo = col, Rough = R.Mix(.45f, .15f, blood) };
    }

    public override void Prop(int index, Scene sc, Mats m)
    {
        var n = sc.N; float cx = .75f;
        if (index == 0)
        {
            int stalk = AbyssScene.Flesh(sc, 1031, "3a0814", "8a2a3a");
            var c = new Vector3(cx + .12f, 1.02f, -.05f);
            int eye = AbyssScene.Eye(sc, c, .2f, 1032);
            sc.Add(new Vector3(cx - .45f, -.02f, -.4f), new Vector3(cx + .45f, 1.25f, .4f), (Vector3 p, out int mt) =>
            {
                var a = Acc.Start; var q = p - new Vector3(cx, 0, 0);
                float stem = Sdf.RoundCone(q, new Vector3(0, 0, 0), new Vector3(.08f, .55f, 0), .13f, .07f);
                stem = R.SMin(stem, Sdf.RoundCone(q, new Vector3(.08f, .55f, 0), new Vector3(.12f, .88f, .02f), .07f, .1f), .05f);
                a.Add(stem + n.Fbm(p, .08f, 3, 1033) * .015f, stalk);
                a.Add(Sdf.Sphere(p - c, .2f), eye);
                mt = a.M; return a.D;
            });
        }
        else if (index == 1)
        {
            int bone = AbyssScene.Bone(sc, 1041);
            sc.Add(new Vector3(cx - .55f, -.02f, -.4f), new Vector3(cx + .55f, .7f, .4f), (Vector3 p, out int mt) =>
            {
                mt = bone; var q = Sdf.RotY(p - new Vector3(cx, 0, 0), .25f);
                // A skull half sunk in the flesh, with a scattering of long bones.
                var s = q - new Vector3(0, .3f, 0);
                float cranium = Sdf.Ellipsoid(s, new Vector3(.28f, .26f, .32f));
                float face = Sdf.Ellipsoid(s - new Vector3(0, -.12f, -.18f), new Vector3(.2f, .18f, .18f));
                float skull = R.SMin(cranium, face, .08f);
                float sockets = MathF.Min(Sdf.Sphere(s - new Vector3(-.09f, -.06f, -.32f), .075f), Sdf.Sphere(s - new Vector3(.09f, -.06f, -.32f), .075f));
                float nose = Sdf.Ellipsoid(s - new Vector3(0, -.16f, -.34f), new Vector3(.03f, .05f, .05f));
                skull = MathF.Max(skull, -MathF.Min(sockets, nose));
                float teeth = MathF.Max(Sdf.Box(new Vector3(R.Mod(s.X, .045f) - .0225f, s.Y + .26f, s.Z + .27f), new Vector3(.016f, .035f, .02f)), MathF.Abs(s.X) - .12f);
                skull = MathF.Max(MathF.Min(skull, teeth), -(q.Y - .02f));
                float bones = MathF.Min(Sdf.Capsule(q, new Vector3(-.5f, .04f, .1f), new Vector3(-.1f, .06f, -.25f), .035f), Sdf.Capsule(q, new Vector3(.18f, .04f, .25f), new Vector3(.52f, .05f, -.1f), .03f));
                return MathF.Min(skull, bones) + n.Fbm(p, .06f, 3, 1042) * .006f;
            });
        }
        else
        {
            int bone = AbyssScene.Bone(sc, 1051);
            int flesh = AbyssScene.Flesh(sc, 1052, "3a0814", "7a1a2a");
            sc.Add(new Vector3(cx - .45f, -.02f, -.35f), new Vector3(cx + .45f, 1.35f, .35f), (Vector3 p, out int mt) =>
            {
                var a = Acc.Start; var q = p - new Vector3(cx, 0, 0);
                // A sharpened bone stake with torn flesh caught halfway up.
                a.Add(Sdf.RoundCone(q, new Vector3(-.04f, 0, 0), new Vector3(.06f, 1.3f, -.02f), .07f, .006f), bone);
                float rag = Sdf.Ellipsoid(Sdf.RotZ(q - new Vector3(.02f, .62f, 0), .4f), new Vector3(.17f, .1f, .12f)) + n.Fbm(p, .08f, 3, 1053) * .04f;
                a.Add(rag, flesh);
                a.Add(Sdf.RoundCone(q, new Vector3(.06f, .56f, -.04f), new Vector3(.12f, .25f, -.08f), .03f, .01f), flesh);
                mt = a.M; return a.D;
            });
        }
    }
}
