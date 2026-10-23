namespace StoryPainter;

/// <summary>
/// Nihilos, the evil god who devoured Earth: a horned bone mask split by a vertical maw and crowned with rows of
/// red eyes, a halo of shattered Earth fragments, a ribcage opening onto a burning maw, two clawed arms and a
/// tattered robe that dissolves into tentacles. Painted facing left like the creature boards, feet at the bottom row.
/// </summary>
public static class Nihilos
{
    public const int W = 400, H = 480;
    static readonly Col Outline = Col.Hex("020103");
    static readonly Ramp Skin = new("07040a", "140a16", "221024", "35182f", "4b213b", "66304a", "864258", "a85a68");
    static readonly Ramp Bone = new("1e1512", "3a2b24", "5e4a3c", "8a735e", "b49c80", "d6c4a4", "efe4cc", "fffaee");
    static readonly Ramp Robe = new("040306", "0b0710", "130c1a", "1c1226", "281a33", "362342", "46304f");
    static readonly Ramp Tentacle = new("0a0409", "1a0a14", "2c1020", "441a2e", "602440", "82304e", "a8445e", "c86a78");
    static readonly Ramp Horn = new("030203", "0d0a0e", "191419", "272029", "383040", "4e4558", "6e6478");
    static readonly Ramp Blood = new("2a0206", "4e050c", "7a0a14", "a8141e", "d42228", "f2482e", "ff8a4a", "ffd090");
    static readonly Ramp Ocean = new("0c1c3a", "143060", "1e4a8a", "2e66b0", "4c8ad0", "82b4e8");
    static readonly Ramp Land = new("1e2a12", "2e4218", "48601e", "687a2c", "8c8a42", "a89a62");
    static readonly Ramp Crust = new("1a0e0a", "2e1a12", "4a2a1a", "6a3e24", "8c5634");

    static (int, int, int, int) Box(float x0, float y0, float x1, float y1) => ((int)x0, (int)y0, (int)MathF.Ceiling(x1), (int)MathF.Ceiling(y1));
    static Func<float, float> Taper(float a, float b, float power = 1) => t => a + (b - a) * MathF.Pow(t, power);

    public static Img Paint()
    {
        var img = new Img(W, H);
        Halo(img);
        BackTentacles(img);
        RobeBody(img);
        FrontTentacles(img);
        FarArm(img);
        Torso(img);
        ChestMaw(img);
        Neck(img);
        Head(img);
        NearArm(img);
        // The burning maw and the eyes light the flesh around them.
        img.Light(204, 268, 96, Col.Hex("ff3a2a"), .5f);
        img.Light(176, 378, 42, Col.Hex("ff4a30"), .38f);
        return img;
    }

    /// <summary>The remains of Earth orbit the god's head on a frayed ring of dark fire.</summary>
    static void Halo(Img img)
    {
        const float cx = 214, cy = 396, radius = 126;
        img.Part((float x, float y, out float t, out float s) =>
        {
            t = s = 0; float dx = x - cx, dy = y - cy, r = MathF.Sqrt(dx * dx + dy * dy);
            float band = 3.4f + Noise.Value(MathF.Atan2(dy, dx) * 40, 0, 9, 71) * 1.8f;
            return MathF.Abs(r - radius) < band && Noise.Value(MathF.Atan2(dy, dx) * 60, 3, 14, 73) > -.6f;
        }, 2, tx => Blood.At(.35f + tx.Key * .3f, tx.X, tx.Y), Col.Hex("1a0206"), Box(cx - radius - 6, cy - radius - 6, cx + radius + 6, Math.Min(H - 1, cy + radius + 6)), outlineWidth: .6f);
        var rng = new Random(97);
        for (int k = 0; k < 14; k++)
        {
            float a = (k / 14f) * MathF.PI * 2 + (float)rng.NextDouble() * .25f;
            float fx = cx + MathF.Cos(a) * radius, fy = cy + MathF.Sin(a) * radius * .96f;
            float size = 11 + (float)rng.NextDouble() * 10 + (MathF.Sin(a) > .3f ? 5 : 0);
            if (fy + size > H - 2) fy = H - 2 - size;
            int seed = 200 + k * 13;
            float inward = MathF.Atan2(cy - fy, cx - fx);
            img.Part((float x, float y, out float t, out float s) =>
            {
                float dx = x - fx, dy = y - fy, r = MathF.Sqrt(dx * dx + dy * dy), phi = MathF.Atan2(dy, dx);
                // Broken plates: an irregular outline with a few sharp notches.
                float edge = size * (1 + Noise.Value(MathF.Cos(phi) * 30 + seed, MathF.Sin(phi) * 30, 18, seed) * .32f);
                if (Noise.H01((int)((phi + 4) * 3), seed, 5) > .8f) edge *= .82f;
                t = MathF.Cos(phi - inward); s = r / Math.Max(1, edge);
                return r < edge;
            }, 6, tx =>
            {
                // The side facing the god glows with the planet's torn molten core.
                if (tx.Depth < 3.2f && tx.T > .35f) return Blood.At(.55f + tx.T * .4f - tx.Depth * .06f, tx.X, tx.Y);
                if (tx.Depth < 3.2f) return Crust.At(.25f + tx.Key * .6f, tx.X, tx.Y);
                float land = Noise.Fbm(tx.X + seed * 7, tx.Y, 13, 3, seed);
                float cloud = Noise.Fbm(tx.X * 1.6f + seed, tx.Y * 2.4f, 9, 2, seed + 3);
                float burn = Noise.Fbm(tx.X + seed, tx.Y * 1.3f, 8, 2, seed + 9);
                float light = .12f + tx.Key * .62f;
                // Scorched continents: soot and dying embers spread over the land and sea.
                if (burn > .38f) return burn > .55f && Noise.H01(tx.X, tx.Y, seed) > .7f ? Blood.At(.5f, tx.X, tx.Y) : Crust.At(.1f + light * .5f, tx.X, tx.Y);
                if (cloud > .55f) return Col.Lerp(Ocean[3], Col.Hex("d8dce4"), .4f + light * .4f);
                return land > .08f ? Land.At(light + (land - .3f) * .4f, tx.X, tx.Y) : Ocean.At(light + land * .5f, tx.X, tx.Y);
            }, Outline, Box(fx - size * 1.4f, fy - size * 1.4f, fx + size * 1.4f, fy + size * 1.4f));
        }
    }

    static Col TentacleShade(Texel tx, float length, int seed)
    {
        // Round pale suckers march along the underside; a vein of dull red runs along the top.
        float along = tx.T * length, spacing = 11 - tx.T * 4, cell = along / spacing, local = (cell - MathF.Floor(cell) - .5f) * spacing;
        float radius = 3.4f * (1.05f - tx.T * .7f), across = (tx.S + .62f) * tx.Depth / Math.Max(.05f, 1 - MathF.Abs(tx.S));
        float sucker = MathF.Sqrt(local * local + across * across * 1.8f);
        if (tx.S < -.25f && sucker < radius)
            return sucker < radius * .45f ? Col.Hex("2a0a12") : sucker < radius * .7f ? Col.Hex("8a4a52") : Col.Lerp(Col.Hex("a86468"), Col.Hex("d8a098"), tx.Key);
        float v = .12f + tx.Key * .62f + tx.Rim * .22f + Noise.Value(tx.X, tx.Y, 6, seed) * .06f;
        if (tx.S > .2f && tx.S < .42f && Noise.Ridge(tx.X, tx.Y, 30, seed) > .5f) return Blood.At(.25f + v * .3f, tx.X, tx.Y);
        return Tentacle.At(v, tx.X, tx.Y);
    }

    static void DrawTentacle(Img img, (float x, float y)[] path, float w0, float w1, int seed)
    {
        float length = 0; for (int i = 1; i < path.Length; i++) length += MathF.Sqrt(MathF.Pow(path[i].x - path[i - 1].x, 2) + MathF.Pow(path[i].y - path[i - 1].y, 2));
        img.Part(Shape.Tube(path, Taper(w0, w1, .8f)), w0 * .9f, tx => TentacleShade(tx, length, seed), Outline,
            Box(path.Min(p => p.x) - w0, path.Min(p => p.y) - w0, path.Max(p => p.x) + w0, path.Max(p => p.y) + w0),
            (x, y) => Noise.Value(x, y, 5, seed) * 1.5f);
    }

    static void BackTentacles(Img img)
    {
        DrawTentacle(img, Shape.Bezier((250, 112), (334, 122), (398, 176), (370, 256)), 17, 2.5f, 11);
        DrawTentacle(img, Shape.Bezier((168, 116), (98, 142), (34, 196), (22, 268)), 15, 2.5f, 13);
        DrawTentacle(img, Shape.Bezier((270, 80), (346, 84), (394, 110), (386, 160)), 13, 2, 15);
    }

    static float RobeCenter(float y) => 208 - (200 - y) * .06f;
    static float Hem(float x)
    {
        float h = 4 + 20 * Math.Max(0, Noise.Value(x, 0, 11, 31));
        // Ragged V-cuts tear up into the cloth between the tentacles.
        float cut = x / 23 - MathF.Floor(x / 23);
        float depth = 10 + 34 * Noise.H01((int)MathF.Floor(x / 23), 1, 33);
        h += Math.Max(0, 1 - MathF.Abs(cut - .5f) * 4.5f) * depth;
        return h;
    }

    static void RobeBody(Img img)
    {
        static float Top(float x) => 216 - MathF.Abs(x - 206) * .16f;
        img.Part((float x, float y, out float t, out float s) =>
        {
            t = s = 0;
            if (y > Top(x) || y < Hem(x)) return false;
            float flare = MathF.Pow(1 - Math.Clamp(y / 214, 0, 1), 1.3f);
            float half = 46 + 118 * flare + Noise.Value(y, x * .1f, 16, 35) * 9 + Noise.Value(y, 3, 5, 36) * 3;
            s = (x - RobeCenter(y)) / half;
            return MathF.Abs(x - RobeCenter(y)) < half;
        }, 30, tx =>
        {
            float hemDistance = tx.Y - Hem(tx.X), u = tx.X - RobeCenter(tx.Y);
            // Deep vertical creases fall from the belt and fan out toward the hem.
            float fold = MathF.Sin(u * .12f * (1.25f - tx.Y / 260f) + Noise.Value(tx.X, tx.Y, 38, 39) * 2.4f);
            float v = .08f + tx.Key * .55f + tx.Rim * .16f + fold * .1f - (fold < -.8f ? .12f : 0);
            // A rotted blood-red border of glyph dashes runs above the torn hem.
            if (hemDistance > 4 && hemDistance < 10 && Noise.H01((int)(tx.X / 4), (int)(hemDistance / 3), 37) > .4f)
                return Blood.At(.16f + tx.Key * .35f, tx.X, tx.Y);
            float belt = Top(tx.X) - tx.Y;
            if (belt < 11) return belt < 2 || belt > 9 ? Bone.At(.15f + tx.Key * .4f, tx.X, tx.Y) : Blood.At(.1f + tx.Key * .35f + (MathF.Abs((tx.X % 12) - 6) < 1.5f ? .2f : 0), tx.X, tx.Y);
            return Robe.At(v, tx.X, tx.Y);
        }, Outline, Box(30, 0, 380, 218), (x, y) => 5f * MathF.Sin((x - RobeCenter(y)) * .12f * (1.25f - y / 260f) + Noise.Value(x, y, 38, 39) * 2.4f) * (1.15f - y / 230f));
    }

    static void FrontTentacles(Img img)
    {
        DrawTentacle(img, Shape.Bezier((150, 44), (96, 10), (44, 2), (14, 42)), 19, 2.5f, 41);
        DrawTentacle(img, Shape.Bezier((262, 40), (318, 8), (370, 3), (392, 52)), 19, 2.5f, 43);
        DrawTentacle(img, Shape.Bezier((224, 30), (240, 5), (282, 3), (296, 24)), 11, 2, 45);
        DrawTentacle(img, Shape.Bezier((188, 32), (172, 6), (128, 6), (118, 26)), 10, 2, 47);
    }

    static Col SkinShade(Texel tx, int seed)
    {
        float v = .05f + tx.Key * .72f + tx.Rim * .26f;
        // Swollen veins show faintly under the dead skin.
        float vein = Noise.Ridge(tx.X * .8f, tx.Y, 46, seed);
        if (vein > .9f) return Blood.At(.06f + (vein - .9f) * 2.5f + tx.Key * .25f, tx.X, tx.Y, .3f);
        if (vein > .84f) v -= .12f;
        return Skin.At(v + Noise.Value(tx.X, tx.Y, 7, seed) * .05f, tx.X, tx.Y, .3f);
    }

    static void Claw(Img img, (float x, float y) a, (float x, float y) b, (float x, float y) c, (float x, float y) d, float width, int seed)
    {
        var path = Shape.Bezier(a, b, c, d, 32);
        img.Part(Shape.Tube(path, Taper(width, .6f, .9f)), width, tx =>
        {
            float v = .15f + tx.Key * .7f + tx.T * .2f;
            return tx.T > .78f ? Bone.At(.45f + tx.Key * .4f, tx.X, tx.Y) : Horn.At(v, tx.X, tx.Y);
        }, Outline, Box(path.Min(p => p.x) - width, path.Min(p => p.y) - width, path.Max(p => p.x) + width, path.Max(p => p.y) + width));
    }

    static void Limb(Img img, (float x, float y) a, (float x, float y) b, float w0, float w1, int seed)
    {
        var path = new[] { a, ((a.x + b.x) * .5f, (a.y + b.y) * .5f), b };
        img.Part(Shape.Tube(path, Taper(w0, w1)), w0, tx => SkinShade(tx, seed), Outline,
            Box(Math.Min(a.x, b.x) - w0, Math.Min(a.y, b.y) - w0, Math.Max(a.x, b.x) + w0, Math.Max(a.y, b.y) + w0),
            (x, y) => Noise.Value(x, y, 6, seed) * 1.2f, smooth: 1);
    }

    static void Spike(Img img, (float x, float y) a, (float x, float y) b, float width, int seed)
    {
        img.Part(Shape.Tube(new[] { a, b }, Taper(width, .5f)), width, tx => Bone.At(.2f + tx.Key * .65f, tx.X, tx.Y), Outline,
            Box(Math.Min(a.x, b.x) - width, Math.Min(a.y, b.y) - width, Math.Max(a.x, b.x) + width, Math.Max(a.y, b.y) + width));
    }

    static void FarArm(Img img)
    {
        Limb(img, (258, 318), (312, 368), 15, 12, 51);
        Spike(img, (314, 370), (342, 354), 5, 53);
        Limb(img, (312, 368), (330, 428), 12, 9, 55);
        img.Part(Shape.Ellipse(332, 437, 13, 11, 10), 8, tx => SkinShade(tx, 57), Outline, Box(316, 424, 348, 450));
        float[] angles = { 52, 76, 100, 124 }, lengths = { 30, 36, 34, 27 };
        for (int k = 0; k < 4; k++)
        {
            float a = angles[k] * MathF.PI / 180, l = lengths[k];
            var start = (332 + MathF.Cos(a) * 9, 437 + MathF.Sin(a) * 8);
            var end = (start.Item1 + MathF.Cos(a - .35f) * l, Math.Min(H - 3, start.Item2 + MathF.Sin(a - .35f) * l));
            Claw(img, start, (start.Item1 + MathF.Cos(a) * l * .45f, start.Item2 + MathF.Sin(a) * l * .45f),
                (end.Item1 - MathF.Cos(a - .8f) * l * .2f, end.Item2 - MathF.Sin(a - .8f) * l * .2f), end, 4.5f, 59 + k);
        }
    }

    static void Torso(Img img)
    {
        // A gaunt torso: broad ribbed chest, a pinched waist sinking into the belt, bony shoulders.
        var torso = Shape.Union(
            Shape.Ellipse(206, 284, 62, 52),
            Shape.Ellipse(206, 234, 44, 40),
            Shape.Ellipse(206, 206, 50, 16),
            Shape.Ellipse(205, 320, 42, 15),
            Shape.Ellipse(157, 310, 23, 19, 20),
            Shape.Ellipse(256, 314, 22, 18, -20));
        img.Part(torso, 34, tx => SkinShade(tx, 61), Outline, Box(126, 186, 284, 344), (x, y) => Noise.Value(x, y, 9, 63) * 2.5f, smooth: 3);
    }

    static void ChestMaw(Img img)
    {
        const float cx = 204, cy = 268, rx = 23, ry = 46;
        img.Part(Shape.Ellipse(cx, cy, rx, ry, 0, (x, y) => Noise.Value(x, y, 6, 65) * .08f), 6, tx =>
        {
            float core = 1 - MathF.Sqrt(tx.T * tx.T * 1.8f + tx.S * tx.S);
            return Blood.At(.08f + core * 1.05f + Noise.Value(tx.X, tx.Y, 5, 67) * .08f, tx.X, tx.Y, .6f);
        }, Col.Hex("140004"), Box(cx - rx - 2, cy - ry - 2, cx + rx + 2, cy + ry + 2));
        // Ribs frame the maw, then teeth line its lips.
        for (int k = 0; k < 5; k++)
        {
            float y = 302 - k * 15, w = 4.6f - k * .25f;
            foreach (int side in new[] { -1, 1 })
            {
                var path = Shape.Bezier((cx + side * (rx - 3), y + 2), (cx + side * 30, y + 9), (cx + side * 50, y), (cx + side * 58, y - 14), 28);
                img.Part(Shape.Tube(path, Taper(w, w * .7f)), w, tx => Bone.At(.18f + tx.Key * .72f - tx.T * .1f, tx.X, tx.Y), Outline,
                    Box(path.Min(p => p.x) - w, path.Min(p => p.y) - w, path.Max(p => p.x) + w, path.Max(p => p.y) + w));
            }
        }
        img.Part(Shape.Ellipse(cx, 318, 11, 8), 5, tx => Bone.At(.2f + tx.Key * .7f, tx.X, tx.Y), Outline, Box(cx - 12, 309, cx + 12, 327));
        for (int k = 0; k < 9; k++)
        {
            float y = cy - ry * .82f + k * (ry * 1.64f / 8);
            float span = rx * MathF.Sqrt(Math.Max(0, 1 - MathF.Pow((y - cy) / ry, 2)));
            foreach (int side in new[] { -1, 1 })
            {
                float bx = cx + side * (span - 1), len = 7 + (k % 3) * 2, half = 3.2f;
                img.Part((float x, float yy, out float t, out float s) =>
                {
                    t = s = 0; float along = (bx - x) * side;
                    return along > 0 && along < len && MathF.Abs(yy - y) < half * (1 - along / len);
                }, 2, tx => Bone.At(.45f + tx.Key * .5f, tx.X, tx.Y), Col.Hex("2a0a08"), Box(bx - len - 1, y - half - 1, bx + len + 1, y + half + 1), outlineWidth: .7f);
            }
        }
    }

    static void Neck(Img img)
    {
        img.Part(Shape.Tube(new[] { (201f, 322f), (196f, 340f), (189f, 358f) }, Taper(18, 13)), 12, tx =>
        {
            // Tendons stand out on the gaunt neck.
            if (MathF.Abs(tx.S - .35f) < .1f || MathF.Abs(tx.S + .3f) < .1f) return Skin.At(.25f + tx.Key * .6f, tx.X, tx.Y);
            return SkinShade(tx, 69);
        }, Outline, Box(170, 314, 222, 366));
    }

    static void Head(Img img)
    {
        const float hx = 180, hy = 392, rx = 37, ry = 50, tilt = -10;
        // Horns first: they sweep back from the crown.
        float tiltRad = tilt * MathF.PI / 180;
        float[] crown = { 34, 56, 78, 98, 118, 140, 160 };
        float[] reach = { 46, 66, 86, 90, 80, 62, 44 };
        for (int k = 0; k < crown.Length; k++)
        {
            float a = crown[k] * MathF.PI / 180;
            float bx = hx + (MathF.Cos(a) * rx * MathF.Cos(tiltRad) - MathF.Sin(a) * ry * MathF.Sin(tiltRad)) * .9f;
            float by = hy + (MathF.Cos(a) * rx * MathF.Sin(tiltRad) + MathF.Sin(a) * ry * MathF.Cos(tiltRad)) * .9f;
            float dir = a - .42f, back = dir - .62f, l = reach[k];
            var end = (bx + MathF.Cos(back) * l, by + MathF.Sin(back) * l);
            if (end.Item2 > H - 3) { float over = end.Item2 - (H - 3); end = (end.Item1 + over * .6f, H - 3); }
            if (end.Item1 > W - 3) end = (W - 3, end.Item2);
            var path = Shape.Bezier((bx, by), (bx + MathF.Cos(dir) * l * .5f, by + MathF.Sin(dir) * l * .5f),
                (end.Item1 - MathF.Cos(back - .3f) * l * .25f, end.Item2 - MathF.Sin(back - .3f) * l * .25f), end, 40);
            float w = 7 + (k == 3 ? 2 : 0);
            img.Part(Shape.Tube(path, Taper(w, .7f, .85f)), w, tx =>
            {
                float rings = tx.T * l / 6.5f; rings -= MathF.Floor(rings);
                float v = .1f + tx.Key * .72f + tx.Rim * .12f - (rings < .22f ? .18f : 0);
                return Horn.At(v, tx.X, tx.Y);
            }, Outline, Box(path.Min(p => p.x) - w, path.Min(p => p.y) - w, path.Max(p => p.x) + w, path.Max(p => p.y) + w));
        }
        var skull = Shape.Union(Shape.Ellipse(hx, hy, rx, ry, tilt, (x, y) => Noise.Value(x, y, 8, 75) * .03f),
            Shape.Tube(new[] { (180f, 372f), (172f, 354f), (163f, 336f) }, Taper(24, 9)));
        img.Part(skull, 18, tx =>
        {
            float crack = Noise.Ridge(tx.X, tx.Y, 26, 77);
            float v = .14f + tx.Key * .7f + tx.Rim * .1f - (tx.Depth < 5 ? .12f : 0);
            if (crack > .8f) v -= .35f;
            return Bone.At(v, tx.X, tx.Y);
        }, Outline, Box(130, 330, 230, 446), (x, y) => Noise.Value(x, y, 10, 79) * 2.4f);
        // The face splits along a vertical maw lined with needle teeth.
        var slit = Shape.Bezier((185, 412), (183, 396), (176, 368), (165, 340), 32);
        img.Part(Shape.Tube(slit, t => 1.6f + 6.4f * MathF.Sin(MathF.PI * Math.Clamp(t * 1.05f, 0, 1))), 4, tx =>
        {
            float tooth = tx.T * 18; tooth -= MathF.Floor(tooth);
            if (MathF.Abs(tx.S) > .58f && tooth < .45f) return Bone.At(.55f + tx.Key * .4f, tx.X, tx.Y);
            return Blood.At(.25f + (1 - MathF.Abs(tx.S)) * .75f, tx.X, tx.Y, .6f);
        }, Col.Hex("100003"), Box(152, 332, 196, 418), outlineWidth: .8f);
        // Rows of small burning eyes across the brow.
        (float x, float y, float r)[] eyes = { (158, 413, 4.6f), (168, 424, 5), (182, 429, 5.4f), (196, 425, 5), (206, 414, 4.4f),
            (163, 399, 4f), (200, 401, 4f), (172, 438, 3.4f), (191, 438, 3.4f) };
        foreach (var (ex, ey, er) in eyes)
        {
            img.Part(Shape.Ellipse(ex, ey, er * 1.25f, er * .82f, -8), 2, tx =>
            {
                float d = MathF.Sqrt(tx.T * tx.T + tx.S * tx.S);
                return d < .32f ? Col.Hex("fff0c0") : d < .62f ? Col.Hex("ff5a2a") : Col.Hex("b8101c");
            }, Col.Hex("1c0204"), Box(ex - er * 1.4f, ey - er, ex + er * 1.4f, ey + er));
            img.Light(ex, ey, er * 3.2f, Col.Hex("ff3020"), .45f);
        }
    }

    static void NearArm(Img img)
    {
        Limb(img, (150, 314), (104, 262), 16, 13, 81);
        Spike(img, (106, 264), (126, 296), 5.5f, 83);
        Limb(img, (104, 262), (76, 196), 13, 9, 85);
        img.Part(Shape.Ellipse(70, 183, 14, 11, 25), 9, tx => SkinShade(tx, 87), Outline, Box(52, 168, 88, 198));
        (float sx, float sy, float ex, float ey)[] claws = { (60, 176, 22, 128), (68, 172, 40, 116), (76, 172, 60, 112), (83, 177, 82, 120) };
        for (int k = 0; k < claws.Length; k++)
        {
            var (sx, sy, ex, ey) = claws[k];
            Claw(img, (sx, sy), (sx - 8, sy - 22), (ex - 8, ey + 18), (ex, ey), 4.6f, 89 + k);
        }
    }
}
