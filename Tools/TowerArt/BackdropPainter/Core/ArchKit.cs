namespace BackdropPainter;

/// <summary>Parts of an arcade bay, from structural mass to carved detail.</summary>
public enum Part { None, Wall, Pier, Shaft, Capital, Plinth, Ring, Keystone, Oculus, Tracery, Cornice, Corbel, Gallery, Colonnette, Parapet }

public sealed class ArcadeStyle
{
    public float Span = 8, PierHalf = .95f, Base = -7, Spring = 2.4f, Pointed = .2f;
    public int Rings = 3; public float RingW = .17f;
    public float PlinthH = .9f, CapH = .5f;
    public float Deck = 6.2f, CorniceH = .32f, Corbel = .45f;
    public float GalleryTop = 7.6f, GallerySpan = 2f, GalleryHalf = .5f, GallerySpring = 6.95f;
    public bool Shafts = true, Oculus = true, Gallery = true, Merlons = false;
    public float Phase;
    /// <summary>Optional erosion: return true where masonry has fallen away.</summary>
    public Func<float, float, bool> Missing;
}

public struct ArchHit
{
    public Part Part; public V3 N; public float Detail; public float U;
}

/// <summary>Analytic gothic arcade with moulded orders, engaged shafts, oculi and a corbelled gallery.</summary>
public static class ArchKit
{
    static readonly V3 Flat = new(0, 0, 1);

    public static ArchHit Hit(ArcadeStyle s, Field f, float x, float y)
    {
        var none = new ArchHit { Part = Part.None };
        if (y < s.Base || y > s.GalleryTop + (s.Merlons ? .5f : 0)) return none;
        if (s.Missing != null && s.Missing(x, y)) return none;
        float span = f.Snap(s.Span);
        float u = M.Mod(x - s.Phase, span) - span * .5f;
        float a = span * .5f - s.PierHalf;
        float pu = span * .5f - MathF.Abs(u); // distance from the pier centre
        float apex = s.Spring + Kit.ApexHeight(a, s.Pointed);

        // Gallery and cornice above the deck.
        if (y >= s.Deck)
        {
            if (y < s.Deck + s.CorniceH)
            {
                float t = (y - s.Deck) / s.CorniceH;
                return new ArchHit { Part = Part.Cornice, N = t > .7f ? new V3(0, .8f, .6f).N() : t < .25f ? new V3(0, -.7f, .7f).N() : Flat, Detail = t };
            }
            if (!s.Gallery) return none;
            float gspan = f.Snap(s.GallerySpan), gu = M.Mod(x - s.Phase + gspan * .5f, gspan) - gspan * .5f;
            if (y > s.GalleryTop)
            {
                if (!s.Merlons) return none;
                float mu = M.Mod(x - s.Phase, gspan * .5f) / (gspan * .5f);
                return mu < .55f ? new ArchHit { Part = Part.Parapet, N = Flat } : none;
            }
            if (y > s.GalleryTop - .18f) return new ArchHit { Part = Part.Parapet, N = y > s.GalleryTop - .06f ? new V3(0, .8f, .6f).N() : Flat };
            float gb = s.Deck + s.CorniceH;
            if (Kit.InOpening(gu, y, s.GalleryHalf, s.GallerySpring, gb + .1f, .25f)) return none;
            float cr = MathF.Abs(MathF.Abs(gu) - gspan * .5f);
            if (cr < .09f && y < s.GallerySpring)
            {
                float off = (gu > 0 ? gu - gspan * .5f : gu + gspan * .5f) / .09f;
                return new ArchHit { Part = Part.Colonnette, N = new V3(off, 0, MathF.Sqrt(Math.Max(0, 1 - off * off))).N() };
            }
            bool gring = Kit.InOpening(gu, y, s.GalleryHalf + .08f, s.GallerySpring, gb + .1f, .25f);
            return new ArchHit { Part = gring ? Part.Ring : Part.Gallery, N = Flat, Detail = gring ? 1 : 0 };
        }
        // Corbel table under the cornice.
        if (y > s.Deck - s.Corbel && M.Mod(x - s.Phase, span / 16) < span / 16 * .45f && y > apex + .1f)
        {
            float t = (y - (s.Deck - s.Corbel)) / s.Corbel;
            return new ArchHit { Part = Part.Corbel, N = t < .35f ? new V3(0, -.6f, .8f).N() : Flat, Detail = t };
        }

        // The opening and its moulded orders.
        if (Kit.InOpening(u, y, a, s.Spring, s.Base, s.Pointed)) return none;
        for (int k = 0; k < s.Rings; k++)
        {
            float outer = a + (k + 1) * s.RingW;
            if (!Kit.InOpening(u, y, outer, s.Spring, s.Base, s.Pointed)) continue;
            // Order k: a rounded roll whose normal points into the opening at its inner edge.
            float inner = a + k * s.RingW, t;
            float rx, ry;
            if (y < s.Spring) { t = (MathF.Abs(u) - inner) / s.RingW; rx = MathF.Sign(u); ry = 0; }
            else
            {
                float e = s.Pointed * a * 2, cx = u > 0 ? -e : e, dx = u - cx, dy = y - s.Spring, d = MathF.Sqrt(dx * dx + dy * dy);
                t = (d - (inner + e)) / s.RingW; rx = dx / d; ry = dy / d;
            }
            t = M.Clamp01(t);
            float roll = MathF.Cos(t * MathF.PI);
            var n = new V3(-rx * roll * .8f, -ry * roll * .8f, MathF.Sin(t * MathF.PI) + .25f).N();
            bool key = y > apex - .05f && MathF.Abs(u) < .16f;
            return new ArchHit { Part = key ? Part.Keystone : Part.Ring, N = n, Detail = k, U = t };
        }

        float outerA = a + s.Rings * s.RingW;
        bool belowSpring = y < s.Spring - s.CapH * .5f;
        if (y < s.Base + s.PlinthH)
        {
            float t = (y - s.Base) / s.PlinthH;
            return new ArchHit { Part = Part.Plinth, N = t > .82f ? new V3(0, .75f, .65f).N() : t > .7f ? new V3(0, -.5f, .85f).N() : Flat, Detail = t };
        }
        if (MathF.Abs(y - s.Spring) < s.CapH * .5f && MathF.Abs(u) >= outerA - .05f)
        {
            float t = (y - (s.Spring - s.CapH * .5f)) / s.CapH;
            float carve = f.N(x, y, .16f, 17);
            return new ArchHit { Part = Part.Capital, N = new V3(0, (t - .5f) * 1.2f, 1).N(), Detail = carve, U = t };
        }
        if (belowSpring && s.Shafts)
        {
            float[] centres = { 0, .55f * s.PierHalf * 1.05f };
            float[] radii = { .3f * s.PierHalf, .2f * s.PierHalf };
            for (int k = 0; k < 2; k++)
            {
                float d = MathF.Abs(pu - centres[k]);
                if (d < radii[k])
                {
                    // Signed offset from this shaft's axis; the pier centre sits at u = ±span/2.
                    float nx = (u > 0 ? -1 : 1) * (pu - centres[k]) / radii[k];
                    return new ArchHit { Part = Part.Shaft, N = new V3(nx, 0, MathF.Sqrt(Math.Max(0, 1 - nx * nx))).N(), Detail = k };
                }
            }
            return new ArchHit { Part = Part.Pier, N = Flat, Detail = 0 };
        }
        // Spandrel: masonry with an oculus between neighbouring arches.
        if (s.Oculus)
        {
            float oy = s.Spring + (apex - s.Spring) * .78f, orad = Math.Min(s.PierHalf * .55f + .25f, (s.Deck - s.Corbel - oy) * .9f);
            float dx = pu, dy = y - oy, d = MathF.Sqrt(dx * dx + dy * dy);
            if (orad > .15f && d < orad)
            {
                if (d > orad - .09f) return new ArchHit { Part = Part.Ring, N = new V3(-dx / d * .6f, -dy / d * .6f, .8f).N(), Detail = 0, U = .5f };
                // Quatrefoil tracery.
                float ang = MathF.Atan2(dy, dx), lobe = MathF.Abs(MathF.Cos(ang * 2));
                bool bar = MathF.Abs(dx) < .035f || MathF.Abs(dy) < .035f || MathF.Abs(d - (orad - .09f) * (.55f + .2f * lobe)) < .03f;
                return new ArchHit { Part = bar ? Part.Tracery : Part.Oculus, N = Flat, Detail = d / orad };
            }
        }
        return new ArchHit { Part = Part.Wall, N = Flat };
    }

    /// <summary>Default value per part before lighting, so the orders and mouldings read as carved.</summary>
    public static float BaseTone(ArchHit h)
    {
        switch (h.Part)
        {
            case Part.Pier: return .38f;
            case Part.Shaft: return .5f;
            case Part.Capital: return .55f + h.Detail * .15f;
            case Part.Plinth: return .5f;
            case Part.Ring: return .44f + (h.Detail % 2) * .08f;
            case Part.Keystone: return .62f;
            case Part.Cornice: return .55f;
            case Part.Corbel: return .5f;
            case Part.Gallery: return .4f;
            case Part.Colonnette: return .55f;
            case Part.Parapet: return .5f;
            case Part.Tracery: return .45f;
            default: return .45f;
        }
    }
}
