using System.Numerics;

namespace UIPainter;

/// <summary>
/// Nine-slice frames, buttons, meters and ornaments. Every texture is drawn at half size in the game
/// (two texture pixels per UI unit), so the borders listed in TowerUIStyle.cs are half these pixel counts.
/// </summary>
public static class Kit
{
    const float Quarter = MathF.PI / 2;
    static Vector2 V(float x, float y) => new(x, y);
    static Vector3 C(float r, float g, float b) => new(r, g, b);

    /// <summary>Night-blue panel body with a lit top lip and darker edges (border 28 px).</summary>
    public static Img PanelFill()
    {
        const int S = 128; var img = new Img(S, S); var c = V(64, 64);
        img.Over((x, y) =>
        {
            float d = Sdf.Box(V(x, y), c, V(62, 62), 10), cov = Sdf.Cov(d);
            if (cov <= 0) return default;
            var col = Vector3.Lerp(C(.088f, .108f, .176f), C(.030f, .040f, .078f), Sdf.Smooth(0, S, y));
            float depth = -d;
            col *= .74f + .26f * Sdf.Smooth(0, 16, depth);
            col += C(.07f, .08f, .11f) * (1 - Sdf.Smooth(1.5f, 4.5f, depth)) * (1 - Sdf.Smooth(4, 14, y));
            return new Vector4(col, cov * .965f);
        }, 2);
        return img;
    }

    /// <summary>Soft drop shadow under panels (border 44 px).</summary>
    public static Img PanelShadow()
    {
        const int S = 96; var img = new Img(S, S);
        img.Over((x, y) =>
        {
            float d = Sdf.Box(V(x, y), V(48, 48), V(22, 22), 10);
            return new Vector4(0, 0, 0, .62f * (1 - Sdf.Smooth(-8, 26, d)));
        });
        return img;
    }

    /// <summary>Double gilded line with art-deco corners and a gem at each corner (border 64 px).</summary>
    public static Img PanelFrame(Ramp metal, Ramp gem)
    {
        const int S = 192; var img = new Img(S, S); var c = V(96, 96);
        float Lines(Vector2 p) => MathF.Min(Sdf.Outline(Sdf.Box(p, c, V(91.5f, 91.5f), 7), 1.7f), Sdf.Outline(Sdf.Box(p, c, V(84, 84), 3), .8f));
        float Corner(Vector2 p)
        {
            var q = Sdf.Fold(p, S, S);
            float d = Sdf.Diamond(q, V(11, 11), 10);
            d = MathF.Min(d, Sdf.Arc(q, V(12, 12), 21, 0, Quarter, .85f));
            d = MathF.Min(d, Sdf.Diamond(q, V(33, 12), 3.4f));
            d = MathF.Min(d, Sdf.Diamond(q, V(12, 33), 3.4f));
            d = MathF.Min(d, Sdf.Circle(q, V(24.5f, 24.5f), 1.7f));
            return d;
        }
        float All(Vector2 p) => MathF.Min(Lines(p), Corner(p));
        img.Over(Metal.Shadow(All, 4, .5f, V(0, 1.5f)));
        img.Over(Metal.Shade(All, 2.2f, metal), 2);
        img.Over(Metal.Shade(p => Sdf.Diamond(Sdf.Fold(p, S, S), V(11, 11), 4.6f), 3.5f, gem, 1, 1.3f, .9f), 2);
        return img;
    }

    /// <summary>Heavier engraved frame for cards and large screens (border 88 px).</summary>
    public static Img CardFrame(Ramp metal, Ramp gem)
    {
        const int S = 256; var img = new Img(S, S); var c = V(128, 128);
        float Outer(Vector2 p) => Sdf.Outline(Sdf.Box(p, c, V(122.5f, 122.5f), 9), 2.7f);
        float Inner(Vector2 p) => Sdf.Outline(Sdf.Box(p, c, V(110, 110), 4), 1.1f);
        float Hair(Vector2 p) => Sdf.Outline(Sdf.Box(p, c, V(104.5f, 104.5f), 2), .5f);
        float Corner(Vector2 p)
        {
            var q = Sdf.Fold(p, S, S);
            float d = Sdf.Diamond(q, V(16, 16), 14);
            d = MathF.Min(d, Sdf.Arc(q, V(18, 18), 34, 0, Quarter, 1.25f));
            d = MathF.Min(d, Sdf.Arc(q, V(18, 18), 46, .1f * MathF.PI, .4f * MathF.PI, .8f));
            d = MathF.Min(d, Sdf.Diamond(q, V(52, 18), 4.5f));
            d = MathF.Min(d, Sdf.Diamond(q, V(18, 52), 4.5f));
            d = MathF.Min(d, Sdf.Segment(q, V(56, 18), V(84, 18), 1.6f, .3f));
            d = MathF.Min(d, Sdf.Segment(q, V(18, 56), V(18, 84), 1.6f, .3f));
            d = MathF.Min(d, Sdf.Circle(q, V(35, 35), 2.3f));
            return d;
        }
        float All(Vector2 p) => MathF.Min(MathF.Min(Outer(p), Inner(p)), Corner(p));
        // An engraved centre line runs along the outer band, stopping short of the corner gems.
        float Groove(Vector2 p) => MathF.Max(Sdf.Outline(Sdf.Box(p, c, V(122.5f, 122.5f), 9), .45f), -Sdf.Diamond(Sdf.Fold(p, S, S), V(16, 16), 19));
        img.Over(Metal.Shadow(All, 5, .55f, V(0, 2)));
        img.Over(Metal.Shade(All, 2.6f, metal), 2);
        img.Over(Metal.Fill(Groove, C(.02f, .015f, .01f), .5f), 2);
        img.Over(Metal.Shade(Hair, 1f, metal, .6f), 2);
        img.Over(Metal.Shade(p => Sdf.Diamond(Sdf.Fold(p, S, S), V(16, 16), 6.5f), 4f, gem, 1, 1.3f, .9f), 2);
        return img;
    }

    /// <summary>Winged crest set over the top edge of cards and title banners.</summary>
    public static Img Crest(Ramp metal, Ramp gem)
    {
        var img = new Img(240, 96); var c = V(120, 46);
        float Body(Vector2 p)
        {
            float d = Sdf.Outline(Sdf.Circle(p, c, 19), 2.4f);
            d = MathF.Min(d, Sdf.Diamond(p, V(120, 16), 6.5f));
            d = MathF.Min(d, Sdf.Diamond(p, V(120, 76), 6.5f));
            for (int s = -1; s <= 1; s += 2)
            {
                d = MathF.Min(d, Sdf.Segment(p, V(120 + s * 22, 46), V(120 + s * 112, 46), 2.6f, .3f));
                d = MathF.Min(d, Sdf.Segment(p, V(120 + s * 20, 37), V(120 + s * 80, 22), 1.7f, .3f));
                d = MathF.Min(d, Sdf.Segment(p, V(120 + s * 20, 55), V(120 + s * 80, 70), 1.7f, .3f));
                d = MathF.Min(d, Sdf.Diamond(p, V(120 + s * 62, 46), 4.5f));
                d = MathF.Min(d, Sdf.Circle(p, V(120 + s * 90, 46), 2.4f));
            }
            return d;
        }
        img.Over(Metal.Shadow(Body, 5, .6f, V(0, 2)));
        img.Over(Metal.Fill(p => Sdf.Circle(p, c, 17.5f), C(.02f, .025f, .05f)), 2);
        img.Over(Metal.Shade(Body, 2.4f, metal), 2);
        img.Over(Metal.Shade(p => Sdf.Circle(p, c, 13.5f), 9f, gem, 1, 1f, .9f), 2);
        return img;
    }

    const float ButtonCut = 11;
    /// <summary>Grey chamfered button body, tinted at runtime (border 24 px).</summary>
    public static Img ButtonBody()
    {
        var img = new Img(96, 64);
        img.Over((x, y) =>
        {
            float d = Sdf.Chamfer(V(x, y), V(48, 32), V(46, 30), ButtonCut), cov = Sdf.Cov(d);
            if (cov <= 0) return default;
            float g = .62f + (.27f - .62f) * Sdf.Smooth(2, 62, y), depth = -d;
            g *= .72f + .28f * Sdf.Smooth(0, 5, depth);
            g += .22f * (1 - Sdf.Smooth(1.5f, 4.5f, depth)) * (1 - Sdf.Smooth(3, 14, y));
            return new Vector4(g, g, g, cov);
        }, 2);
        return img;
    }

    /// <summary>Metal rim and inner hairline of the chamfered button (border 24 px).</summary>
    public static Img ButtonEdge(Ramp metal)
    {
        var img = new Img(96, 64);
        img.Over(Metal.Shade(p => Sdf.Outline(Sdf.Chamfer(p, V(48, 32), V(44.6f, 28.6f), ButtonCut - .6f), 1.5f), 1.8f, metal), 2);
        img.Over(Metal.Shade(p => Sdf.Outline(Sdf.Chamfer(p, V(48, 32), V(40.5f, 24.5f), ButtonCut - 2.4f), .5f), .8f, metal, .5f), 2);
        return img;
    }

    /// <summary>Recessed slot for list rows, stat boxes and empty places (border 20 px).</summary>
    public static Img Inset()
    {
        var img = new Img(64, 64); var c = V(32, 32);
        float D(Vector2 p) => Sdf.Box(p, c, V(31, 31), 7);
        img.Over((x, y) =>
        {
            float d = D(V(x, y)), cov = Sdf.Cov(d);
            if (cov <= 0) return default;
            var col = C(.016f, .022f, .046f) * (.55f + .45f * Sdf.Smooth(0, 9, y - 1));
            float lip = (1 - Sdf.Smooth(.6f, 2.2f, -d)) * Sdf.Smooth(40, 60, y);
            col += C(.16f, .18f, .25f) * lip;
            return new Vector4(col, cov * .86f);
        }, 2);
        img.Over(Metal.Fill(p => Sdf.Outline(D(p), .55f), C(.36f, .41f, .53f), .45f), 2);
        return img;
    }

    /// <summary>Chamfered portrait frame with a gem stud on each cut corner (border 36 px).</summary>
    public static Img PortraitFrame(Ramp metal, Ramp gem)
    {
        const int S = 128; var img = new Img(S, S); var c = V(64, 64);
        float Rim(Vector2 p) => Sdf.Outline(Sdf.Chamfer(p, c, V(59.5f, 59.5f), 15), 2.4f);
        float Stud(Vector2 p) => Sdf.Diamond(Sdf.Fold(p, S, S), V(12, 12), 6.5f);
        float All(Vector2 p) => MathF.Min(Rim(p), Stud(p));
        img.Over(Metal.Shadow(All, 4, .5f, V(0, 1.5f)));
        img.Over(Metal.Shade(All, 2.4f, metal), 2);
        img.Over(Metal.Shade(p => Sdf.Outline(Sdf.Chamfer(p, c, V(53, 53), 12), .6f), .8f, metal, .55f), 2);
        img.Over(Metal.Shade(p => Sdf.Diamond(Sdf.Fold(p, S, S), V(12, 12), 3), 2.5f, gem, 1, 1.3f, .9f), 2);
        return img;
    }

    /// <summary>Round gilded medallion for the hero's portrait, a ruby set at its foot.</summary>
    public static Img Medallion(Ramp metal, Ramp gem)
    {
        const int S = 208; var img = new Img(S, S); var c = V(104, 104);
        float Ring(Vector2 p)
        {
            float d = Sdf.Outline(Sdf.Circle(p, c, 94), 3.6f);
            d = MathF.Min(d, Sdf.Outline(Sdf.Circle(p, c, 85), 1.2f));
            d = MathF.Min(d, Sdf.Diamond(p, c + V(0, -94), 7.5f));
            d = MathF.Min(d, Sdf.Diamond(p, c + V(94, 0), 6.5f));
            d = MathF.Min(d, Sdf.Diamond(p, c + V(-94, 0), 6.5f));
            d = MathF.Min(d, Sdf.Diamond(p, c + V(0, 94), 9));
            return d;
        }
        img.Over(Metal.Shadow(p => Sdf.Circle(p, c, 97), 6, .55f, V(0, 2)));
        img.Over((x, y) =>
        {
            var p = V(x, y); float cov = Sdf.Cov(Sdf.Circle(p, c, 92));
            if (cov <= 0) return default;
            float r = (p - (c + V(-26, -30))).Length() / 120f;
            return new Vector4(Vector3.Lerp(C(.13f, .16f, .27f), C(.025f, .032f, .065f), Sdf.Smooth(0, 1, r)), cov * .97f);
        }, 2);
        img.Over(Metal.Shade(Ring, 2.6f, metal), 2);
        img.Over(Metal.Shade(p => MathF.Min(Sdf.Diamond(p, c + V(0, 94), 4.6f), Sdf.Diamond(p, c + V(0, -94), 3.2f)), 3.5f, gem, 1, 1.3f, .9f), 2);
        return img;
    }

    /// <summary>Dark capsule behind meter fills (border 16 px).</summary>
    public static Img MeterTrough()
    {
        var img = new Img(64, 32);
        img.Over((x, y) =>
        {
            float cov = Sdf.Cov(Sdf.Box(V(x, y), V(32, 16), V(31, 15), 15));
            return cov <= 0 ? default : new Vector4(C(.010f, .013f, .026f) * (.6f + .4f * Sdf.Smooth(1, 10, y)), cov * .93f);
        }, 2);
        return img;
    }

    /// <summary>Metal capsule rim drawn over meter fills (border 16 px).</summary>
    public static Img MeterRim(Ramp metal)
    {
        var img = new Img(64, 32);
        img.Over(Metal.Shade(p => Sdf.Outline(Sdf.Box(p, V(32, 16), V(30, 14), 14), 1.4f), 1.6f, metal), 2);
        return img;
    }

    /// <summary>Grey glossy gradient stretched across meter fills and tinted at runtime.</summary>
    public static Img MeterFill()
    {
        var img = new Img(4, 32);
        img.Over((x, y) =>
        {
            float t = y / 32f, g = .92f + (.48f - .92f) * Sdf.Smooth(.12f, 1, t);
            g = MathF.Min(1, g + .18f * (1 - Sdf.Smooth(0, .12f, MathF.Abs(t - .2f))));
            return new Vector4(g, g, g, 1);
        });
        return img;
    }

    /// <summary>Gilded hairline that fades out at both ends.</summary>
    public static Img Divider()
    {
        var img = new Img(512, 12);
        img.Over((x, y) =>
        {
            float u = x / 512f, fade = Sdf.Smooth(0, .32f, u) * Sdf.Smooth(0, .32f, 1 - u), dy = MathF.Abs(y - 6);
            float a = MathF.Max(Sdf.Cov(dy - .75f), .22f * (1 - Sdf.Smooth(0, 5, dy))) * fade;
            return new Vector4(C(.93f, .80f, .52f), a);
        });
        return img;
    }

    /// <summary>Centre ornament for dividers: a gem between two small diamonds.</summary>
    public static Img Knot(Ramp metal, Ramp gem)
    {
        var img = new Img(112, 32); var c = V(56, 16);
        float Body(Vector2 p)
        {
            float d = Sdf.Diamond(p, c, 10);
            for (int s = -1; s <= 1; s += 2)
            {
                d = MathF.Min(d, Sdf.Diamond(p, c + V(s * 22, 0), 4));
                d = MathF.Min(d, Sdf.Segment(p, c + V(s * 9, 0), c + V(s * 50, 0), 1.1f, .2f));
                d = MathF.Min(d, Sdf.Circle(p, c + V(s * 33, 0), 1.8f));
            }
            return d;
        }
        img.Over(Metal.Shadow(Body, 3, .55f, V(0, 1)));
        img.Over(Metal.Shade(Body, 2.2f, metal), 2);
        img.Over(Metal.Shade(p => Sdf.Diamond(p, c, 4.5f), 3f, gem, 1, 1.3f, .9f), 2);
        return img;
    }

    /// <summary>Raised key with a lit face and a dark lower lip (border 16 px).</summary>
    public static Img Keycap()
    {
        var img = new Img(48, 48); var c = V(24, 24);
        float D(Vector2 p) => Sdf.Box(p, c, V(22, 22), 7);
        img.Over(Metal.Shadow(D, 3, .5f, V(0, 1.5f)));
        img.Over((x, y) =>
        {
            float d = D(V(x, y)), cov = Sdf.Cov(d);
            if (cov <= 0) return default;
            var face = Vector3.Lerp(C(.25f, .29f, .40f), C(.13f, .16f, .25f), Sdf.Smooth(4, 36, y));
            var col = Vector3.Lerp(face, C(.055f, .065f, .11f), Sdf.Smooth(35, 39, y));
            col += C(.12f, .13f, .16f) * (1 - Sdf.Smooth(.8f, 2.4f, -d)) * (1 - Sdf.Smooth(3, 12, y));
            return new Vector4(col, cov * .96f);
        }, 2);
        img.Over(Metal.Fill(p => Sdf.Outline(D(p), .5f), C(.60f, .65f, .78f), .5f), 2);
        return img;
    }

    /// <summary>Tapered flourish that points at a title on its right; mirrored at runtime for the other side.</summary>
    public static Img Flourish(Ramp metal, Ramp gem)
    {
        var img = new Img(256, 48); const float cy = 24;
        float Body(Vector2 p)
        {
            float d = Sdf.Segment(p, V(6, cy), V(216, cy), .35f, 2.2f);
            d = MathF.Min(d, Sdf.Diamond(p, V(236, cy), 8.5f));
            d = MathF.Min(d, Sdf.Arc(p, V(206, cy), 13, -MathF.PI * .92f, -MathF.PI * .2f, 1.1f));
            d = MathF.Min(d, Sdf.Arc(p, V(206, cy), 13, MathF.PI * .2f, MathF.PI * .92f, 1.1f));
            d = MathF.Min(d, Sdf.Diamond(p, V(170, cy), 3.8f));
            d = MathF.Min(d, Sdf.Circle(p, V(150, cy), 2f));
            d = MathF.Min(d, Sdf.Circle(p, V(134, cy), 1.5f));
            return d;
        }
        img.Over(Metal.Shadow(Body, 3, .55f, V(0, 1)));
        img.Over(Metal.Shade(Body, 2f, metal), 2);
        img.Over(Metal.Shade(p => Sdf.Diamond(p, V(236, cy), 4), 3f, gem, 1, 1.3f, .9f), 2);
        return img;
    }

    /// <summary>Grey domed disc for skill tree nodes, tinted by branch.</summary>
    public static Img NodeDisc()
    {
        const int S = 80; var img = new Img(S, S); var c = V(40, 40);
        img.Over((x, y) =>
        {
            var p = V(x, y); float cov = Sdf.Cov(Sdf.Circle(p, c, 31));
            if (cov <= 0) return default;
            float g = .95f - .45f * Sdf.Smooth(0, 1, (p - (c + V(-9, -11))).Length() / 42f);
            return new Vector4(g, g, g, cov);
        }, 2);
        return img;
    }

    public static Img NodeRim(Ramp metal)
    {
        const int S = 80; var img = new Img(S, S); var c = V(40, 40);
        img.Over(Metal.Shadow(p => Sdf.Circle(p, c, 35), 4, .55f, V(0, 1.5f)));
        img.Over(Metal.Shade(p => Sdf.Outline(Sdf.Circle(p, c, 33), 3f), 2.4f, metal), 2);
        img.Over(Metal.Shade(p => Sdf.Outline(Sdf.Circle(p, c, 27.5f), .6f), .8f, metal, .5f), 2);
        return img;
    }

    /// <summary>Grey diamond body of keystone nodes.</summary>
    public static Img KeyFill()
    {
        const int S = 96; var img = new Img(S, S); var c = V(48, 48);
        img.Over((x, y) =>
        {
            var p = V(x, y); float cov = Sdf.Cov(Sdf.Diamond(p, c, 40));
            if (cov <= 0) return default;
            float g = .95f - .45f * Sdf.Smooth(0, 1, (p - (c + V(-10, -12))).Length() / 46f);
            return new Vector4(g, g, g, cov);
        }, 2);
        return img;
    }

    public static Img KeyRim(Ramp metal)
    {
        const int S = 96; var img = new Img(S, S); var c = V(48, 48);
        img.Over(Metal.Shadow(p => Sdf.Diamond(p, c, 45), 4, .55f, V(0, 1.5f)));
        img.Over(Metal.Shade(p => Sdf.Outline(Sdf.Diamond(p, c, 42.5f), 2.8f), 2.4f, metal), 2);
        img.Over(Metal.Shade(p => Sdf.Outline(Sdf.Diamond(p, c, 35.5f), .6f), .8f, metal, .5f), 2);
        return img;
    }
}
