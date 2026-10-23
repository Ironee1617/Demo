namespace BackdropPainter;

/// <summary>Composites the parallax planes the way the runtime does, with a mock floor and ledges,
/// so a bake can be judged without opening Unity.</summary>
public static class Preview
{
    static readonly (float x, float y, float w)[] Ledges =
    {
        (-9.6f,-1.8f,3),(-7,-.4f,2.8f),(-4.2f,1,3.6f),(-2.4f,-1.8f,3.2f),(.7f,-.4f,2.6f),(6,-1.8f,4.8f),
        (11.7f,-1.8f,3.2f),(14.8f,-.4f,2.8f),(17.7f,1,3.6f),(20.3f,-1.8f,3.4f),(23.1f,-.4f,2.8f),
        (-4.85f,2.4f,3.2f),(-3.95f,3.8f,3.2f),(-4.65f,5.2f,3.2f),(17.05f,2.4f,3.2f),(17.95f,3.8f,3.2f),(17.25f,5.2f,3.2f),
    };

    public static void Render(ThemePainter theme, List<PaintedLayer> layers, string dir)
    {
        Directory.CreateDirectory(dir);
        // Each plane on its own, for tracking down which layer an artefact lives in.
        foreach (var l in layers) Png.Write(Path.Combine(dir, theme.Id + "-layer-" + l.Spec.Name + ".png"), l.Canvas.W, l.Canvas.H, l.Canvas.ToBytes());
        Shot(theme, layers, Path.Combine(dir, theme.Id + "-ground.png"), 1920, 1080, 5.625f, -1, 0);
        Shot(theme, layers, Path.Combine(dir, theme.Id + "-smoke.png"), 1600, 900, 7.5f, 1, .7f);
        Shot(theme, layers, Path.Combine(dir, theme.Id + "-high.png"), 1920, 1080, 5.625f, 17, 6.7f);
    }

    public static void Shot(ThemePainter theme, List<PaintedLayer> layers, string path, int w, int h, float ortho, float cx, float cy)
    {
        var px = new byte[w * h * 4];
        float aspect = w / (float)h, unit = h / (2 * ortho);
        var sorted = layers.OrderBy(l => l.Spec.Order).ToList();
        Parallel.For(0, h, row =>
        {
            for (int col = 0; col < w; col++)
            {
                float wx = cx + (col + .5f - w * .5f) / unit, wy = cy + (row + .5f - h * .5f) / unit;
                var c = new Col(.035f, .065f, .105f);
                foreach (var l in sorted)
                {
                    var s = l.Spec; var cv = l.Canvas;
                    float lx = wx - (cx - ThemePainter.ReferenceX) * s.Factor, ly = wy - (cy - ThemePainter.ReferenceY) * s.Factor;
                    Col t;
                    if (ly < cv.Bottom) t = s.Under ?? Col.Clear;
                    else if (ly >= cv.Top) t = s.Over ?? Col.Clear;
                    else t = cv.Get((int)MathF.Floor(lx * cv.Ppu), (int)MathF.Floor((ly - cv.Bottom) * cv.Ppu));
                    c = c.Under(t);
                }
                c = Mock(theme, wx, wy, c);
                int o = (row * w + col) * 4;
                px[o] = (byte)(c.R * 255); px[o + 1] = (byte)(c.G * 255); px[o + 2] = (byte)(c.B * 255); px[o + 3] = 255;
            }
        });
        Png.Write(path, w, h, px);
    }

    static Col Mock(ThemePainter theme, float x, float y, Col c)
    {
        if (y < -3 && (y > -4 || !theme.OpenBelow))
            return y > -3.08f ? Col.Hex("c8c0a8") : y > -4 ? Col.Hex("5a5560") : Col.Hex("2c2a33");
        foreach (var (lx, ly, lw) in Ledges)
            if (MathF.Abs(x - lx) < lw * .5f && y < ly && y > ly - .5f)
                return y > ly - .07f ? Col.Hex("d8d0b8") : Col.Hex("6a6470");
        // A readable stand-in for a character silhouette.
        if (MathF.Abs(x + 10) < .38f && y > -3 && y < -1.4f) return y > -1.7f ? Col.Hex("f2e6d0") : Col.Hex("4a78c8");
        if (MathF.Abs(x - 3) < .55f && y > -3 && y < -1.8f) return Col.Hex("c84a3a");
        return c;
    }
}
