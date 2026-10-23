using System.Numerics;

namespace BackdropPainter.Render;

/// <summary>Procedural physically based materials. Detail comes from 3D noise in world space, so no UVs are needed
/// and every texture tiles with the strip.</summary>
public sealed class Mats
{
    readonly Scene sc; readonly Noise n;
    public Mats(Scene scene) { sc = scene; n = scene.N; }

    /// <summary>Tilts the normal by the gradient of a height field sampled around p.</summary>
    public static Vector3 Bump(Func<Vector3, float> h, Vector3 p, Vector3 normal, float strength, float e = .01f)
    {
        float h0 = h(p);
        var g = new Vector3(h(p + new Vector3(e, 0, 0)) - h0, h(p + new Vector3(0, e, 0)) - h0, h(p + new Vector3(0, 0, e)) - h0) / e;
        g -= normal * Vector3.Dot(g, normal);
        return Vector3.Normalize(normal - g * strength);
    }

    /// <summary>Mixes two materials by a weight that may depend on position and normal (snow on tops, moss in shade).</summary>
    public int Blend(int a, int b, Func<Vector3, Vector3, float> weight)
    {
        var ma = sc.GetMaterial(a); var mb = sc.GetMaterial(b);
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float t = R.Clamp01(weight(p, s.N));
            if (t <= 0) { ma(p, ref s); return; }
            if (t >= 1) { mb(p, ref s); return; }
            var s1 = s; var s2 = s; ma(p, ref s1); mb(p, ref s2);
            s.Albedo = R.Mix(s1.Albedo, s2.Albedo, t); s.Rough = R.Mix(s1.Rough, s2.Rough, t); s.Spec = R.Mix(s1.Spec, s2.Spec, t);
            s.Metal = R.Mix(s1.Metal, s2.Metal, t); s.Trans = R.Mix(s1.Trans, s2.Trans, t); s.Ao = R.Mix(s1.Ao, s2.Ao, t);
            s.Emit = R.Mix(s1.Emit, s2.Emit, t); s.N = Vector3.Normalize(R.Mix(s1.N, s2.N, t));
        });
    }

    /// <summary>Snow settles on upward faces of <paramref name="baseMat"/>; <paramref name="amount"/> shifts the threshold.</summary>
    public int Snowy(int baseMat, float amount = 1, int seed = 83)
    {
        int snow = Snow(seed);
        return Blend(baseMat, snow, (p, nrm) => R.Smooth(.42f, .62f, nrm.Y + n.Fbm(p, .3f, 3, seed) * .22f + (amount - 1) * .3f));
    }

    /// <summary>Adds glowing carved glyphs to a material where <paramref name="mask"/> is positive.</summary>
    public int Glyphs(int baseMat, string glowHex, float intensity, Func<Vector3, float> mask)
    {
        var mb = sc.GetMaterial(baseMat); var g = R.Rgb(glowHex);
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            mb(p, ref s);
            float k = R.Clamp01(mask(p));
            if (k <= 0) return;
            s.Albedo *= 1 - k * .6f; s.Emit += g * intensity * k;
        });
    }

    public int Marble(string baseHex = "efe8dc", string veinHex = "8c8476", float veinScale = .9f, float rough = .32f, int seed = 1)
    {
        var a = R.Rgb(baseHex); var vein = R.Rgb(veinHex); var warm = R.Rgb("e8d8bc");
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            // Fine veins only where the warped field crosses zero; a soft cloudy patina underneath.
            float w = n.Warped(p * new Vector3(1, 1.6f, 1), veinScale * 1.6f, 5, seed, 2.2f);
            float v = MathF.Pow(1 - MathF.Abs(w), 28) * R.Smooth(.2f, .7f, n.Fbm(p, veinScale * 2, 2, seed + 7) * .5f + .5f);
            float patch = n.Fbm(p, 1.6f, 4, seed + 3) * .5f + .5f;
            s.Albedo = R.Mix(R.Mix(a, warm, patch * .4f), vein, v * .35f) * (.95f + n.N(p, .05f, seed + 9) * .04f);
            s.Rough = rough + v * .1f; s.Spec = .05f;
            s.N = Bump(q => n.Fbm(q, .25f, 3, seed + 5) * .0015f, p, s.N, 1);
        });
    }

    /// <summary>Weathered dressed stone or natural rock: mottled colour, cracks and pitted relief.</summary>
    public int Rock(string darkHex, string lightHex, float scale = 1, float rough = .9f, float cracks = .6f, int seed = 11, string mossHex = null, float moss = 0)
    {
        var d = R.Rgb(darkHex); var l = R.Rgb(lightHex); var m = mossHex != null ? R.Rgb(mossHex) : Vector3.Zero;
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float t = n.Fbm(p, .9f * scale, 5, seed) * .5f + .5f;
            var cell = n.Voronoi(p, .55f * scale, seed + 2);
            float crack = R.Smooth(.035f * scale, 0, cell.F2 - cell.F1) * cracks;
            var c = R.Mix(d, l, R.Clamp01(t * 1.2f - .1f)) * (.85f + R.Hash01((int)cell.Id, 0, 0, seed) * .3f);
            c *= 1 - crack * .55f;
            if (moss > 0)
            {
                float up = R.Clamp01(s.N.Y);
                float mm = R.Smooth(.45f, .75f, up * .7f + n.Fbm(p, .4f * scale, 4, seed + 7) * .5f + .3f) * moss;
                c = R.Mix(c, m * (.8f + t * .4f), mm);
            }
            s.Albedo = c; s.Rough = rough; s.Spec = .035f;
            s.Ao = 1 - crack * .5f;
            s.N = Bump(q => n.Fbm(q, .35f * scale, 4, seed + 4) * .02f * scale + n.Ridged(q, .8f * scale, 3, seed + 5) * .015f * scale, p, s.N, 1, .006f);
        });
    }

    /// <summary>Sedimentary sandstone with horizontal strata and wind-worn grain.</summary>
    public int Sandstone(string darkHex = "a8763e", string lightHex = "e3be82", float bands = .35f, int seed = 21)
    {
        var d = R.Rgb(darkHex); var l = R.Rgb(lightHex);
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float strata = MathF.Sin((p.Y + n.Fbm(p, 1.2f, 3, seed) * .25f) / bands * MathF.PI * 2) * .5f + .5f;
            float t = n.Fbm(p, .6f, 4, seed + 1) * .5f + .5f;
            s.Albedo = R.Mix(d, l, R.Clamp01(strata * .45f + t * .55f)) * (.93f + n.N(p, .04f, seed + 2) * .07f);
            s.Rough = .92f; s.Spec = .03f;
            s.N = Bump(q => n.Fbm(q, .2f, 3, seed + 3) * .01f + MathF.Sin(q.Y / bands * 6.28f) * .002f, p, s.N, 1, .005f);
        });
    }

    public int Wood(string darkHex = "4a3020", string lightHex = "8a6040", float rough = .7f, bool vertical = true, int seed = 31)
    {
        var d = R.Rgb(darkHex); var l = R.Rgb(lightHex);
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            var q = vertical ? new Vector3(p.X * 9, p.Y * .6f, p.Z * 9) : new Vector3(p.X * .6f, p.Y * 9, p.Z * 9);
            float grain = MathF.Sin(q.X + q.Z + n.Fbm(p, .5f, 3, seed) * 6) * .5f + .5f;
            s.Albedo = R.Mix(d, l, grain * .6f + n.Fbm(p, .3f, 3, seed + 1) * .2f + .2f);
            s.Rough = rough; s.Spec = .04f;
            s.N = Bump(r => grain * .002f + n.N(r, .03f, seed + 2) * .001f, p, s.N, 1);
        });
    }

    /// <summary>Painted surface (lacquer, plaster, fabric) with subtle wear.</summary>
    public int Paint(string hex, float rough = .45f, float wear = .15f, int seed = 41, float spec = .05f)
    {
        var c = R.Rgb(hex);
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float w = n.Fbm(p, .5f, 4, seed) * .5f + .5f;
            s.Albedo = c * (1 - wear * .5f + w * wear);
            s.Rough = rough + (1 - w) * .2f; s.Spec = spec;
            s.N = Bump(q => n.Fbm(q, .08f, 2, seed + 1) * .0015f, p, s.N, 1, .004f);
        });
    }

    public int Metal(string hex, float rough = .3f, float tarnish = .2f, string tarnishHex = "3a4a3a", int seed = 51)
    {
        var c = R.Rgb(hex); var t = R.Rgb(tarnishHex);
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float w = R.Smooth(.2f, .8f, n.Fbm(p, .4f, 4, seed) * .5f + .5f) * tarnish;
            s.Albedo = R.Mix(c, t, w); s.Metal = 1 - w; s.Rough = rough + w * .5f;
            s.N = Bump(q => n.Fbm(q, .1f, 2, seed + 1) * .001f, p, s.N, 1, .004f);
        });
    }

    /// <summary>Leaves: colour varies by clump, light shines through when backlit.</summary>
    public int Foliage(string darkHex, string lightHex, float trans = .45f, int seed = 61, string flowerHex = null, float flowers = 0)
    {
        var d = R.Rgb(darkHex); var l = R.Rgb(lightHex); var fl = flowerHex != null ? R.Rgb(flowerHex) : Vector3.Zero;
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            // Clump tone at canopy scale, single leaves at a few centimetres, dark gaps between them.
            float t = n.Fbm(p, .35f, 4, seed) * .5f + .5f;
            var cell = n.Voronoi(p, .045f, seed + 1);
            float leaf = R.Hash01((int)(cell.Id & 0xffff), (int)(cell.Id >> 16), 0, seed);
            float gap = R.Smooth(0, .012f, cell.F2 - cell.F1);
            var c = R.Mix(d, l, R.Clamp01(t * .6f + leaf * .45f - .1f));
            if (flowers > 0)
            {
                float f = R.Smooth(.62f, .78f, n.N(p, .06f, seed + 2) * .5f + .5f + (t - .5f) * .3f) * flowers;
                c = R.Mix(c, fl, f);
            }
            s.Albedo = c * (.6f + gap * .4f); s.Rough = .55f; s.Spec = .03f; s.Trans = trans;
            s.Ao = .65f + gap * .35f;
            s.N = Bump(q => { var v = n.Voronoi(q, .045f, seed + 1); return (v.F2 - v.F1) * .25f + n.Fbm(q, .1f, 2, seed + 3) * .02f; }, p, s.N, 1, .006f);
        });
    }

    /// <summary>Grass and earth: green where the ground faces up, soil and stones on slopes.</summary>
    public int Ground(string grassDark, string grassLight, string soilHex, int seed = 71, string flowerHex = null, float flowers = 0)
    {
        var gd = R.Rgb(grassDark); var gl = R.Rgb(grassLight); var so = R.Rgb(soilHex); var fl = flowerHex != null ? R.Rgb(flowerHex) : Vector3.Zero;
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float t = n.Fbm(p, .5f, 5, seed) * .5f + .5f;
            float up = R.Smooth(.55f, .85f, s.N.Y + (t - .5f) * .3f);
            var grass = R.Mix(gd, gl, t);
            grass *= .85f + (n.N(p, .02f, seed + 4) * .5f + .5f) * .3f;
            var c = R.Mix(so * (.8f + t * .4f), grass, up);
            if (flowers > 0) c = R.Mix(c, fl, R.Smooth(.7f, .8f, n.N(p, .05f, seed + 5) * .5f + .5f) * up * flowers);
            s.Albedo = c; s.Rough = .85f; s.Spec = .03f; s.Trans = up * .15f;
            s.N = Bump(q => n.Fbm(q, .06f, 3, seed + 6) * .015f, p, s.N, 1, .004f);
        });
    }

    /// <summary>Billowing cloud solid: bright, soft, light bleeding through the thin edges.</summary>
    public int Cloud(string litHex = "fffaf4", string shadeHex = "c8c4d8", int seed = 131)
    {
        var lit = R.Rgb(litHex); var shade = R.Rgb(shadeHex);
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float t = n.Fbm(p, .25f, 4, seed) * .5f + .5f;
            s.Albedo = R.Mix(shade, lit, R.Clamp01(.55f + s.N.Y * .35f + t * .2f));
            s.Rough = 1; s.Spec = .01f; s.Trans = .55f; s.Ao = .85f + t * .15f;
            // Light scattered through the body keeps shadowed cloud bright rather than grey.
            s.Emit = s.Albedo * (.45f + t * .2f);
            s.N = Bump(q => n.Fbm(q, .08f, 3, seed + 1) * .03f, p, s.N, 1, .01f);
        });
    }

    public int Snow(int seed = 81)
    {
        var w = R.Rgb("f4f7fb"); var b = R.Rgb("c9d6e8");
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            s.Albedo = R.Mix(b, w, R.Clamp01(s.N.Y * .8f + .2f + n.Fbm(p, .4f, 3, seed) * .2f));
            s.Rough = .55f; s.Spec = .04f; s.Trans = .1f;
            s.N = Bump(q => n.Fbm(q, .3f, 4, seed + 1) * .01f, p, s.N, 1);
        });
    }

    public int Water(string deepHex, float waveSize = .6f, float waveStrength = .25f, int seed = 91)
    {
        var d = R.Rgb(deepHex);
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            s.Albedo = d; s.Water = true; s.Rough = .05f;
            s.N = Bump(q => n.Fbm(new Vector3(q.X, 0, q.Z * 2.5f), waveSize, 4, seed) * .04f, p, Vector3.UnitY, waveStrength * 10, .02f);
        });
    }

    /// <summary>Self-lit surface: lamps, embers, molten rock; intensity is linear radiance.</summary>
    public int Glow(string hex, float intensity, string baseHex = null, float flicker = 0, int seed = 101)
    {
        var c = R.Rgb(hex); var b = baseHex != null ? R.Rgb(baseHex) : c * .2f;
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float f = 1 + (flicker > 0 ? n.Fbm(p, .3f, 3, seed) * flicker : 0);
            s.Albedo = b; s.Rough = .6f; s.Emit = c * intensity * MathF.Max(0, f);
        });
    }

    /// <summary>Molten lava: dark crust plates over glowing cracks.</summary>
    public int Lava(string crustHex = "1c1412", string hotHex = "ff5a14", float heat = 8, int seed = 111)
    {
        var crust = R.Rgb(crustHex); var hot = R.Rgb(hotHex); var core = R.Rgb("ffd27a");
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            var cell = n.Voronoi(p, .5f, seed);
            float edge = R.Smooth(.09f, 0, cell.F2 - cell.F1);
            float flow = n.Fbm(p, .9f, 4, seed + 1) * .5f + .5f;
            float glow = R.Clamp01(edge * .9f + R.Smooth(.62f, .9f, flow) * .8f);
            s.Albedo = crust * (.7f + flow * .4f); s.Rough = .85f;
            s.Emit = R.Mix(hot, core, glow * glow) * heat * glow * glow;
            s.N = Bump(q => -R.Smooth(.09f, 0, n.Voronoi(q, .5f, seed).F2 - n.Voronoi(q, .5f, seed).F1) * .03f, p, s.N, 1, .01f);
        });
    }

    /// <summary>Clay roof tiles laid in courses.</summary>
    public int Tiles(string hex, float course = .16f, float rough = .55f, int seed = 121, bool glazed = false)
    {
        var c = R.Rgb(hex);
        return sc.Material((Vector3 p, ref Surface s) =>
        {
            float row = p.Y / course, col = (p.X + (MathF.Floor(row) % 2) * course * .5f) / course;
            float fr = R.Fract(row), fc = R.Fract(col);
            float ridge = MathF.Sin(fc * MathF.PI);
            float shade = .75f + ridge * .25f - (1 - fr) * .1f;
            s.Albedo = c * shade * (.9f + R.Hash01((int)MathF.Floor(col), (int)MathF.Floor(row), 0, seed) * .2f);
            s.Rough = glazed ? .25f : rough; s.Spec = glazed ? .08f : .04f;
            s.N = Bump(q => MathF.Sin(R.Fract((q.X + (MathF.Floor(q.Y / course) % 2) * course * .5f) / course) * MathF.PI) * .012f - R.Fract(q.Y / course) * .008f, p, s.N, 1, .004f);
        });
    }
}
