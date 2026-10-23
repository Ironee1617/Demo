using System.Numerics;

namespace BackdropPainter.Render;

/// <summary>Signed distance of one object at a point; <paramref name="mat"/> names the material of the nearest part.</summary>
public delegate float Shape(Vector3 p, out int mat);

/// <summary>What a material reports at a surface point. Colours are linear; <see cref="N"/> arrives as the
/// geometric normal and may be tilted for bump detail.</summary>
public struct Surface
{
    public Vector3 Albedo;
    public float Rough, Spec, Metal, Trans, Ao;
    public Vector3 Emit, N;
    public bool Water;
}

public delegate void Material(Vector3 p, ref Surface s);

/// <summary>A solid with a box bound. x may extend past the strip; evaluation wraps it around the period.</summary>
public sealed class Obj
{
    public Vector3 Min, Max;
    public Shape F;
    public float Cx;
    public Obj(Vector3 min, Vector3 max, Shape f) { Min = min; Max = max; F = f; Cx = (min.X + max.X) * .5f; }
}

/// <summary>A participating medium (clouds, mist, smoke) with self-shadowing single scattering.</summary>
public sealed class Volume
{
    public Vector3 Min, Max;
    public Func<Vector3, float> Density;
    /// <summary>Cheaper density for the shadow march toward the sun; falls back to <see cref="Density"/>.</summary>
    public Func<Vector3, float> Coarse;
    public Vector3 Albedo = Vector3.One;
    public float Anisotropy = .35f, Ambient = 1, Steps = 56, LightSteps = 6, LightReach = 2.5f;
    public Vector3 Emit = Vector3.Zero;
    /// <summary>Emission varying through the medium (fire); replaces <see cref="Emit"/> when set.</summary>
    public Func<Vector3, Vector3> EmitAt;
    public float Cx;
    public Volume(Vector3 min, Vector3 max, Func<Vector3, float> density) { Min = min; Max = max; Density = density; Cx = (min.X + max.X) * .5f; }
}

public sealed class Scene
{
    public readonly float Period;
    public readonly Noise N;
    public readonly List<Obj> Objects = new();
    public readonly List<Volume> Volumes = new();
    readonly List<Material> materials = new();

    public Scene(float period) { Period = period; N = new Noise(period); }

    public int Material(Material m) { materials.Add(m); return materials.Count - 1; }
    public Material GetMaterial(int id) => id >= 0 && id < materials.Count ? materials[id] : null;

    public Obj Add(Vector3 min, Vector3 max, Shape f) { var o = new Obj(min, max, f); Objects.Add(o); return o; }
    /// <summary>Adds a single-material solid bounded by a box centred at <paramref name="c"/> with half extents <paramref name="h"/>.</summary>
    public Obj Add(Vector3 c, Vector3 h, int mat, Func<Vector3, float> f)
        => Add(c - h, c + h, (Vector3 p, out int m) => { m = mat; return f(p); });
    public Volume AddVolume(Vector3 min, Vector3 max, Func<Vector3, float> density) { var v = new Volume(min, max, density); Volumes.Add(v); return v; }

    /// <summary>Indices of objects whose wrapped x span (plus <paramref name="pad"/>) covers x.</summary>
    public int[] Candidates(float x, float pad)
    {
        var list = new List<int>();
        for (int i = 0; i < Objects.Count; i++)
        {
            var o = Objects[i];
            float half = (o.Max.X - o.Min.X) * .5f + pad;
            if (half * 2 >= Period || MathF.Abs(R.Wrap(x, o.Cx, Period)) <= half) list.Add(i);
        }
        return list.ToArray();
    }
    public int[] VolumeCandidates(float x)
    {
        var list = new List<int>();
        for (int i = 0; i < Volumes.Count; i++)
        {
            var v = Volumes[i];
            float half = (v.Max.X - v.Min.X) * .5f;
            if (half * 2 >= Period || MathF.Abs(R.Wrap(x, v.Cx, Period)) <= half) list.Add(i);
        }
        return list.ToArray();
    }

    /// <summary>Unwraps x near an object's centre so its shape function sees continuous coordinates.</summary>
    public Vector3 Local(Vector3 p, float cx) => new(cx + R.Wrap(p.X, cx, Period), p.Y, p.Z);

    static float BoxDistance(Vector3 p, Vector3 min, Vector3 max)
    {
        var c = (min + max) * .5f; var h = (max - min) * .5f;
        var q = Vector3.Abs(p - c) - h;
        return Vector3.Max(q, Vector3.Zero).Length() + MathF.Min(MathF.Max(q.X, MathF.Max(q.Y, q.Z)), 0);
    }

    /// <summary>Scene distance over the candidate objects; far boxes return their box distance as a safe step.</summary>
    public float Map(Vector3 p, int[] cand, out int mat)
    {
        float best = 1e9f; mat = -1;
        for (int k = 0; k < cand.Length; k++)
        {
            var o = Objects[cand[k]];
            var q = Local(p, o.Cx);
            float db = BoxDistance(q, o.Min, o.Max);
            if (db > .25f) { if (db < best) { best = db; mat = -1; } continue; }
            float d = o.F(q, out int m);
            if (d < best) { best = d; mat = m; }
        }
        return best;
    }
    public float Map(Vector3 p, int[] cand) => Map(p, cand, out _);
}
