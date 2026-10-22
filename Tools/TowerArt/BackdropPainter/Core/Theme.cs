namespace BackdropPainter;

/// <summary>One parallax plane. Coordinates are layer-local units: with the camera at the
/// reference point (7, 1) they coincide with world units, and the plane drifts by
/// (camera - reference) * Factor elsewhere.</summary>
public sealed class LayerSpec
{
    public string Name;
    public float Factor, Ppu, Period, Bottom, Height;
    public Col? Under, Over;
    public int Order;
    public LayerSpec(string name, float factor, float ppu, float period, float bottom, float height, int order)
    { Name = name; Factor = factor; Ppu = ppu; Period = period; Bottom = bottom; Height = height; Order = order; }
    public Canvas NewCanvas() => new(Period, Bottom, Height, Ppu);
}

public sealed class PaintedLayer
{
    public LayerSpec Spec; public Canvas Canvas;
    public PaintedLayer(LayerSpec spec, Canvas canvas) { Spec = spec; Canvas = canvas; }
}

public abstract class ThemePainter
{
    public const float ReferenceX = 7, ReferenceY = 1, Ground = -3;
    public abstract string Id { get; }
    public abstract IEnumerable<PaintedLayer> Paint();
    /// <summary>Water and cloud maps have no bedrock, so their planes stay visible below the ground line.</summary>
    public virtual bool OpenBelow => false;
}
