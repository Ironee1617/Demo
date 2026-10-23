using System.Numerics;

namespace BackdropPainter.Render;

/// <summary>One texel of a periodic body tile: relief toward the viewer, colour and gloss.</summary>
public struct TileSample { public float H; public Vector3 Albedo; public float Rough; public Vector3 Emit; }

/// <summary>
/// A theme's lit terrain. Geometry is authored in walkway units: the walkable line is y = 0, the top surface spans
/// z from -0.6 (front edge) to 0.6, and the camera looks down 10 degrees so the surface reads as a band centred on
/// the collision line. The runtime counterpart is Assets/Demos/Roguelike/Scripts/TowerLitTerrain.cs.
/// </summary>
public abstract class TerrainKit
{
    public abstract string Id { get; }
    public abstract Lighting Light();
    /// <summary>Ground surface band, repeating every 12 units.</summary>
    public abstract void Top(Scene sc, Mats m);
    /// <summary>Platform slab band, repeating every 6 units.</summary>
    public abstract void Slab(Scene sc, Mats m);
    /// <summary>A slab whose right end finishes at x = <paramref name="end"/>.</summary>
    public abstract void Cap(Scene sc, Mats m, float end);
    /// <summary>Body texel at (x, y) on a 6-unit tile; use <see cref="TileNoise"/> so it repeats both ways.</summary>
    public abstract TileSample Body(TileNoise n, float x, float y);
    /// <summary>Three ground props; each is centred at x = 0.75 on a 1.5-unit cell with its foot at y = 0.</summary>
    public abstract void Prop(int index, Scene sc, Mats m);
    /// <summary>The body fades out downward (open sky below the walkway).</summary>
    public virtual bool OpenBelow => false;
}

/// <summary>Noise that repeats in both x and y over a square tile: y is wrapped onto a circle in 3D noise space.</summary>
public sealed class TileNoise
{
    public readonly float Size; readonly Noise n; readonly float r;
    public TileNoise(float size) { Size = size; n = new Noise(size); r = size / (2 * MathF.PI); }
    Vector3 P(float x, float y, float z = 0) { float a = y / Size * MathF.PI * 2; return new Vector3(x, MathF.Cos(a) * r + z, MathF.Sin(a) * r); }
    public float N(float x, float y, float size, int seed) => n.N(P(x, y), size, seed);
    public float Fbm(float x, float y, float size, int octaves, int seed, float gain = .5f) => n.Fbm(P(x, y), size, octaves, seed, gain);
    public float Ridged(float x, float y, float size, int octaves, int seed) => n.Ridged(P(x, y), size, octaves, seed);
    public Noise.Cell Voronoi(float x, float y, float size, int seed) => n.Voronoi(P(x, y), size, seed);
}

public static class TerrainBake
{
    public const float Ppu = 96, Pitch = 10, TopTile = 12, TopAbove = .25f, TopBelow = 1.25f, BodyTile = 6;
    public const float SlabTile = 6, SlabAbove = .25f, SlabBelow = 1f, CapHalf = .375f, PropCell = 1.5f, PropBase = .1f;

    static Camera Cam() => new(Pitch, 20, 60);

    public static void Bake(TerrainKit kit, string dir, string previewDir)
    {
        Directory.CreateDirectory(dir);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var light = kit.Light();
        var top = Band(kit.Top, TopTile, TopAbove, TopBelow, light);
        // The band's lowest rows fade so the body tile behind takes over without a seam.
        for (int j = 0; j < top.H; j++)
        {
            float f = R.Smooth(0, .3f * Ppu, j);
            if (f >= 1) break;
            for (int i = 0; i < top.W; i++) { var c = top.P[j * top.W + i]; top.P[j * top.W + i] = c.WithA(c.A * f); }
        }
        Write(top, Path.Combine(dir, kit.Id + "-top.png"), previewDir);
        Write(Band(kit.Slab, SlabTile, SlabAbove, SlabBelow, light), Path.Combine(dir, kit.Id + "-slab.png"), previewDir);
        // The cap is cut from a wider render around the slab's end.
        var wide = new Canvas(4, -SlabBelow, SlabAbove + SlabBelow, Ppu);
        var capScene = new Scene(4); kit.Cap(capScene, new Mats(capScene), 2);
        new Renderer(capScene, light, Cam()) { Samples = 3 }.Render(wide);
        Write(Crop(wide, 2 - CapHalf, CapHalf * 2), Path.Combine(dir, kit.Id + "-cap.png"), previewDir);
        Write(Tile(kit, light), Path.Combine(dir, kit.Id + "-body.png"), previewDir);
        var props = new Canvas(PropCell * 3, -PropBase, PropCell, Ppu);
        for (int k = 0; k < 3; k++)
        {
            var cell = new Canvas(PropCell, -PropBase, PropCell, Ppu);
            var sc = new Scene(PropCell); kit.Prop(k, sc, new Mats(sc));
            new Renderer(sc, light, Cam()) { Samples = 3 }.Render(cell);
            for (int j = 0; j < cell.H; j++) for (int i = 0; i < cell.W; i++) props.P[j * props.W + k * cell.W + i] = cell.P[j * cell.W + i];
        }
        Write(props, Path.Combine(dir, kit.Id + "-props.png"), previewDir);
        Console.WriteLine($"{kit.Id} terrain: {sw.ElapsedMilliseconds} ms");
    }

    static Canvas Band(Action<Scene, Mats> build, float tile, float above, float below, Lighting light)
    {
        var c = new Canvas(tile, -below, above + below, Ppu);
        var sc = new Scene(tile); build(sc, new Mats(sc));
        new Renderer(sc, light, Cam()) { Samples = 3 }.Render(c);
        return c;
    }

    static Canvas Crop(Canvas src, float x0, float width)
    {
        var c = new Canvas(width, src.Bottom, src.H / src.Ppu, src.Ppu);
        int i0 = (int)MathF.Round(x0 * src.Ppu);
        for (int j = 0; j < c.H; j++) for (int i = 0; i < c.W; i++) c.P[j * c.W + i] = src.Get(i0 + i, j);
        return c;
    }

    /// <summary>Lights a periodic relief map: bumped normals, soft self-shadowing toward the sun and cavity occlusion.</summary>
    static Canvas Tile(TerrainKit kit, Lighting light)
    {
        var c = new Canvas(BodyTile, 0, BodyTile, Ppu);
        int N = c.W; var tn = new TileNoise(BodyTile);
        var s = new TileSample[N * N];
        Parallel.For(0, N, j => { for (int i = 0; i < N; i++) s[j * N + i] = kit.Body(tn, (i + .5f) / Ppu, (j + .5f) / Ppu); });
        float H(int i, int j) => s[((j % N + N) % N) * N + ((i % N + N) % N)].H;
        var L = light.SunDir; float lxy = MathF.Sqrt(L.X * L.X + L.Y * L.Y), rise = -L.Z / MathF.Max(lxy, 1e-3f);
        float stepX = L.X / MathF.Max(lxy, 1e-3f), stepY = L.Y / MathF.Max(lxy, 1e-3f), px = 1 / Ppu;
        Parallel.For(0, N, j =>
        {
            for (int i = 0; i < N; i++)
            {
                var t = s[j * N + i];
                float hx = (H(i + 1, j) - H(i - 1, j)) / (2 * px), hy = (H(i, j + 1) - H(i, j - 1)) / (2 * px);
                var nrm = Vector3.Normalize(new Vector3(-hx, -hy, -1));
                // Shadow: march toward the sun across the relief.
                float shadow = 1;
                for (int k = 1; k <= 48; k++)
                {
                    float d = k * 1.5f, ray = t.H + d * px * rise;
                    float occ = H(i + (int)MathF.Round(stepX * d), j + (int)MathF.Round(stepY * d)) - ray;
                    if (occ > 0) shadow = MathF.Min(shadow, R.Clamp01(1 - occ * 40));
                    if (shadow <= 0) break;
                }
                // Cavities sit below their surroundings.
                float avg = 0; int cnt = 0;
                for (int dj = -6; dj <= 6; dj += 3) for (int di = -6; di <= 6; di += 3) { avg += H(i + di, j + dj); cnt++; }
                float ao = R.Clamp01(1 - MathF.Max(0, avg / cnt - t.H) * 9);
                float ndl = MathF.Max(0, Vector3.Dot(nrm, L));
                var col = t.Albedo * (light.Sun * ndl * shadow + light.SkyLight(nrm) * ao) + t.Emit;
                var hv = Vector3.Normalize(L + new Vector3(0, 0, -1));
                float spec = MathF.Pow(MathF.Max(0, Vector3.Dot(nrm, hv)), 2 / MathF.Max(.02f, t.Rough * t.Rough * t.Rough * t.Rough)) * (1 - t.Rough) * .3f * shadow;
                col += light.Sun * spec * .25f;
                col = R.Aces(R.Saturate(col * light.Exposure * light.Tint, light.Saturation));
                float g = (R.Hash01(i, j, 0, 78) - .5f) / 255f;
                c.P[j * N + i] = new Col(R.Clamp01(R.ToSrgb(col.X) + g), R.Clamp01(R.ToSrgb(col.Y) + g), R.Clamp01(R.ToSrgb(col.Z) + g), 1);
            }
        });
        return c;
    }

    static void Write(Canvas c, string path, string previewDir)
    {
        c.Bleed();
        Png.Write(path, c.W, c.H, c.ToBytes());
        if (previewDir != null) { Directory.CreateDirectory(previewDir); Png.Write(Path.Combine(previewDir, "terrain-" + Path.GetFileName(path)), c.W, c.H, c.ToBytes()); }
    }

    // ---------- Shared tile and geometry helpers ----------

    /// <summary>Dressed-stone coursework for body tiles.</summary>
    public sealed class Masonry
    {
        public float Course = .55f, MinLen = 1f, MaxLen = 1.7f, Joint = .025f, Bevel = .06f, Chips = .6f, Pits = .5f, Grime = .35f, Streaks = .3f, Rough = .75f, Hewn = .012f, Stains = .3f;
        public string Stain = "6a6a52";
        public string Light = "e8e0d2", Dark = "c9bead", Mortar = "a89a86", Dirt = "6e6252";
        public float Veins; public string Vein = "9a8e7a";
        public int Seed = 1;
    }

    /// <summary>
    /// A texel of staggered ashlar that repeats every 6 units both ways: chipped bevels, recessed mortar, per-block
    /// tone, mottling, grain, pits, grime gathered in the joints and faint rain streaks.
    /// </summary>
    public static TileSample Stone(TileNoise n, float x, float y, Masonry s)
    {
        int courses = Math.Max(1, (int)MathF.Round(BodyTile / s.Course)); float course = BodyTile / courses;
        int k = (int)MathF.Floor(y / course); float fy = y / course - k; int kk = ((k % courses) + courses) % courses;
        float len = s.MinLen + R.Hash01(kk, 1, 0, s.Seed) * (s.MaxLen - s.MinLen);
        int count = Math.Max(1, (int)MathF.Round(BodyTile / len)); float L = BodyTile / count;
        float shift = R.Hash01(kk, 0, 0, s.Seed) * L;
        float bx = R.Mod(x + shift, BodyTile) / L; int b = (int)MathF.Floor(bx); float fx = bx - b;
        float edge = MathF.Min(MathF.Min(fx, 1 - fx) * L, MathF.Min(fy, 1 - fy) * course);
        // Chipped arrises: the bevel wanders with noise and bites deeper in places.
        float chip = R.Clamp01(n.Fbm(x, y, .07f, 4, s.Seed + 3) * .7f + .5f) * s.Bevel * s.Chips * 1.6f;
        float e = edge - chip * .6f;
        float face = R.Smooth(0, s.Bevel, e);
        bool mortar = e < s.Joint;
        // Hewn faces bulge slightly and carry tool-pitted relief.
        float bulge = MathF.Sin(MathF.Min(fx, 1 - fx) * MathF.PI) * MathF.Sin(MathF.Min(fy, 1 - fy) * MathF.PI);
        float h = mortar ? -.022f : -(1 - face) * (1 - face) * .04f + bulge * .01f;
        h += n.Fbm(x, y, .18f, 4, s.Seed + 12) * s.Hewn + n.Fbm(x, y, .05f, 3, s.Seed + 4) * .003f - R.Smooth(.62f, .78f, n.N(x, y, .025f, s.Seed + 5) * .5f + .5f) * .006f * s.Pits;
        float tone = R.Hash01(kk, b, 0, s.Seed + 9);
        float mottle = n.Fbm(x, y, 1.2f, 4, s.Seed + 6) * .5f + .5f;
        float grain = n.N(x, y, .02f, s.Seed + 7) * .5f + .5f;
        var col = R.Mix(R.Rgb(s.Dark), R.Rgb(s.Light), R.Clamp01(tone * .75f + mottle * .35f - .05f)) * (.92f + grain * .14f);
        float stain = R.Smooth(.55f, .8f, n.Fbm(x, y, 1.6f, 5, s.Seed + 13) * .5f + .5f) * s.Stains;
        col = R.Mix(col, R.Rgb(s.Stain) * (.8f + grain * .4f), stain);
        if (s.Veins > 0) col = R.Mix(col, R.Rgb(s.Vein), MathF.Pow(1 - MathF.Abs(n.Fbm(x, y, 1.5f, 5, s.Seed + 8)), 30) * s.Veins);
        // Grime gathers near joints and in rain streaks running down from them.
        float streak = R.Smooth(.3f, .9f, n.Fbm(x, 0, .25f, 3, s.Seed + 10) * .5f + .5f) * R.Smooth(.2f, .8f, n.Fbm(x, y, 2f, 2, s.Seed + 11) * .5f + .5f);
        float grime = R.Clamp01((1 - R.Smooth(0, s.Bevel * 2.5f, edge)) * s.Grime + streak * s.Streaks * (1 - fy) * .6f);
        col = R.Mix(col, R.Rgb(s.Dirt), grime * .55f);
        if (mortar) col = R.Rgb(s.Mortar) * (.8f + grain * .3f);
        return new TileSample { H = h, Albedo = col, Rough = mortar ? .95f : s.Rough };
    }

    /// <summary>Fieldstone or rubble walling: irregular rounded stones from Voronoi cells, earth or mortar between them.</summary>
    public sealed class Rubble
    {
        public float Size = .45f, Gap = .03f, Round = .09f, Moss = .35f, Hewn = .02f, Rough = .85f;
        public string Light = "8a8478", Dark = "4e4a44", Fill = "3a3226", MossHex = "4e6a34", MossLight = "7e9a4e";
        public int Seed = 3;
    }

    public static TileSample Fieldstone(TileNoise n, float x, float y, Rubble s)
    {
        var c = n.Voronoi(x, y, s.Size, s.Seed);
        float edge = c.F2 - c.F1;
        float face = R.Smooth(s.Gap, s.Gap + s.Round, edge);
        bool fill = edge < s.Gap;
        float tone = R.Hash01((int)(c.Id & 0xffff), (int)(c.Id >> 16), 0, s.Seed);
        float h = fill ? -.05f : -(1 - face) * (1 - face) * .05f + MathF.Sqrt(face) * .015f;
        h += n.Fbm(x, y, .15f, 4, s.Seed + 1) * s.Hewn + n.Fbm(x, y, .04f, 3, s.Seed + 2) * .004f;
        float grain = n.N(x, y, .02f, s.Seed + 3) * .5f + .5f, mottle = n.Fbm(x, y, .9f, 4, s.Seed + 4) * .5f + .5f;
        var col = R.Mix(R.Rgb(s.Dark), R.Rgb(s.Light), R.Clamp01(tone * .7f + mottle * .4f - .1f)) * (.9f + grain * .16f);
        // Moss creeps over stone tops and out of the joints.
        float moss = R.Smooth(.5f, .75f, n.Fbm(x, y, .7f, 4, s.Seed + 5) * .5f + .5f + (1 - face) * .25f) * s.Moss;
        col = R.Mix(col, R.Mix(R.Rgb(s.MossHex), R.Rgb(s.MossLight), grain), moss);
        if (fill) col = R.Rgb(s.Fill) * (.7f + grain * .4f);
        return new TileSample { H = h + moss * .01f, Albedo = col, Rough = fill ? .95f : s.Rough };
    }

    /// <summary>Rich soil with embedded pebbles and thin roots (for earth banks and garden ground).</summary>
    public sealed class Soil
    {
        public string Dark = "3a2a1c", Light = "6a4e34", Pebble = "8a8070", PebbleDark = "4a443c", Root = "2a1c12";
        public float Pebbles = .5f, Roots = .5f; public int Seed = 5;
    }

    public static TileSample Earth(TileNoise n, float x, float y, Soil s)
    {
        float t = n.Fbm(x, y, .5f, 5, s.Seed) * .5f + .5f, grain = n.N(x, y, .015f, s.Seed + 1) * .5f + .5f;
        var col = R.Mix(R.Rgb(s.Dark), R.Rgb(s.Light), t) * (.85f + grain * .25f);
        float h = n.Fbm(x, y, .12f, 4, s.Seed + 2) * .012f;
        // Pebbles: small Voronoi cells raised above the soil.
        var c = n.Voronoi(x, y, .16f, s.Seed + 3);
        float pr = .05f + R.Hash01((int)(c.Id & 0xffff), 0, 0, s.Seed) * .05f;
        if (R.Hash01((int)(c.Id & 0xffff), 1, 0, s.Seed) < s.Pebbles && c.F1 < pr)
        {
            float k = 1 - c.F1 / pr; h += MathF.Sqrt(k) * .03f;
            col = R.Mix(R.Rgb(s.PebbleDark), R.Rgb(s.Pebble), R.Hash01((int)(c.Id & 0xffff), 2, 0, s.Seed) * .7f + k * .3f) * (.9f + grain * .2f);
        }
        // Roots: thin dark ridged lines wandering through the bank.
        float root = R.Smooth(.94f, .99f, n.Ridged(x, y, 1.1f, 3, s.Seed + 4)) * s.Roots;
        col = R.Mix(col, R.Rgb(s.Root), root); h += root * .01f;
        return new TileSample { H = h, Albedo = col, Rough = .92f };
    }

    /// <summary>Distance to a run of blocks along x whose joints sit at the given cut positions (wrapping every period).</summary>
    public static float JointGroove(float x, float[] cuts, float period, float width)
    {
        float best = 9;
        foreach (float c in cuts) best = MathF.Min(best, MathF.Abs(R.Wrap(x, c, period)));
        return R.Smooth(width, 0, best);
    }
    public static float[] Cuts(Rand rnd, float period, float min, float max)
    {
        var list = new List<float>(); float x = rnd.R(0, max);
        float start = x;
        while (x < start + period - min) { list.Add(x % period); x += rnd.R(min, max); }
        return list.ToArray();
    }
}
