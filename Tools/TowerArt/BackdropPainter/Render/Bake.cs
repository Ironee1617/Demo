using System.Numerics;

namespace BackdropPainter.Render;

/// <summary>Shared layer setup for the lit themes: preview scaling and edge fills taken from the rendered strip.</summary>
public static class Bake
{
    /// <summary>Resolution multiplier; below 1 only for quick previews, never for exported assets.</summary>
    public static float Scale = 1;
    /// <summary>Supersamples per axis on silhouettes.</summary>
    public static int Samples = 3;

    public static Canvas Canvas(LayerSpec s) => new(s.Period, s.Bottom, s.Height, s.Ppu * Scale);

    /// <summary>Renders a scene into the layer and fills solid colour beyond fully opaque edge rows.</summary>
    public static PaintedLayer Layer(LayerSpec spec, Scene scene, Lighting light, Camera cam)
    {
        var c = Canvas(spec);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        new Renderer(scene, light, cam) { Samples = Samples }.Render(c);
        Console.WriteLine($"  {spec.Name}: {c.W}x{c.H} in {sw.ElapsedMilliseconds} ms");
        if (spec.Under == null && RowOpaque(c, 0)) spec.Under = RowAverage(c, 0);
        if (spec.Over == null && RowOpaque(c, c.H - 1)) spec.Over = RowAverage(c, c.H - 1);
        return new PaintedLayer(spec, c);
    }

    static bool RowOpaque(Canvas c, int j) { for (int i = 0; i < c.W; i++) if (c.P[j * c.W + i].A < .99f) return false; return true; }
    static Col RowAverage(Canvas c, int j)
    {
        float r = 0, g = 0, b = 0;
        for (int i = 0; i < c.W; i++) { var p = c.P[j * c.W + i]; r += p.R; g += p.G; b += p.B; }
        return new Col(r / c.W, g / c.W, b / c.W);
    }

    /// <summary>Haze colour that follows the sky gradient, lifted toward the horizon glow.</summary>
    public static Func<float, Vector3> HazeFrom(Sky sky, float lift = 0) => v => sky.Gradient(v) * (1 + lift);
}
