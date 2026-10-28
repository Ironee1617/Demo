using System.Numerics;

namespace UIPainter;

/// <summary>
/// The full-screen backdrop of the title and village screens: a night over planet Lumen with nebulae and
/// stars, a faint golden astrolabe and the evil god's tower rising through low mist. 2560×1440 maps to the
/// 1600×900 UI layout at 1.6 pixels per unit; the tower stands at UI x ≈ 975, between the title column and
/// the chapter list.
/// </summary>
public static class MenuBackground
{
    public const int W = 2560, H = 1440;
    const float TowerX = 1560, CrownY = 330, TipY = 118;
    static Vector3 C(float r, float g, float b) => new(r, g, b);

    public static Img Paint()
    {
        var img = new Img(W, H);
        img.Over((x, y) =>
        {
            float u = x / W, v = y / H;
            var col = Vector3.Lerp(C(.016f, .022f, .058f), C(.058f, .068f, .148f), Sdf.Smooth(0, .64f, v));
            col = Vector3.Lerp(col, C(.018f, .022f, .046f), Sdf.Smooth(.64f, 1f, v));
            float n = Sdf.Fbm(x / 540f, y / 540f, 11), m = Sdf.Fbm(x / 270f + 7, y / 270f, 23);
            float mask = Sdf.Smooth(.32f, .82f, n) * (.3f + .7f * Sdf.Smooth(.1f, .85f, u)) * (1 - Sdf.Smooth(.42f, .86f, v));
            col += Vector3.Lerp(C(.17f, .07f, .25f), C(.03f, .17f, .24f), m) * mask * .95f;
            // A cool glow along the horizon behind the mist.
            col += C(.05f, .07f, .14f) * MathF.Exp(-(v - .66f) * (v - .66f) / (2 * .07f * .07f));
            float dx = x - TowerX, dy = y - CrownY;
            col += C(.24f, .18f, .10f) * MathF.Exp(-(dx * dx + dy * dy) / (2 * 420f * 420f)) * .55f;
            return new Vector4(col, 1);
        });
        Stars(img);
        Planet(img, new Vector2(2230, 230), 128);
        Astrolabe(img, new Vector2(TowerX, CrownY - 30));
        Tower(img);
        Mist(img);
        return img;
    }

    static void Add(Img img, int x, int y, Vector3 c)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        ref var p = ref img.P[y * W + x];
        p = new Vector4(MathF.Min(p.X + c.X, 1), MathF.Min(p.Y + c.Y, 1), MathF.Min(p.Z + c.Z, 1), p.W);
    }

    static void Stars(Img img)
    {
        var random = new Random(7);
        for (int k = 0; k < 2600; k++)
        {
            float x = (float)random.NextDouble() * W, y = (float)random.NextDouble() * H;
            // Fewer stars sink into the mist near the ground.
            if (random.NextDouble() < Sdf.Smooth(.45f, .8f, y / H)) continue;
            float bright = MathF.Pow((float)random.NextDouble(), 4f) + .05f, radius = .5f + bright * 1.5f;
            var tint = Vector3.Lerp(C(.75f, .85f, 1), C(1, .9f, .72f), (float)random.NextDouble());
            int reach = (int)MathF.Ceiling(radius * 4);
            for (int j = -reach; j <= reach; j++)
                for (int i = -reach; i <= reach; i++)
                {
                    float px = MathF.Floor(x) + i + .5f - x, py = MathF.Floor(y) + j + .5f - y, d2 = px * px + py * py;
                    float glow = bright * MathF.Exp(-d2 / (2 * radius * radius * .36f)) + bright * .18f * MathF.Exp(-d2 / (2 * radius * radius * 4));
                    if (glow > .003f) Add(img, (int)x + i, (int)y + j, tint * glow);
                }
            if (bright < .9f) continue;
            // The brightest few carry a thin four-point glint.
            int arm = (int)(6 + bright * 9);
            for (int t = 1; t <= arm; t++)
            {
                float f = bright * .22f * (1 - t / (float)arm);
                Add(img, (int)x + t, (int)y, tint * f); Add(img, (int)x - t, (int)y, tint * f);
                Add(img, (int)x, (int)y + t, tint * f); Add(img, (int)x, (int)y - t, tint * f);
            }
        }
    }

    static void Planet(Img img, Vector2 c, float r)
    {
        var light = Vector2.Normalize(new Vector2(-.8f, -.35f));
        img.Over((x, y) =>
        {
            var p = new Vector2(x, y) - c; float d = p.Length() - r;
            if (d > 40) return default;
            if (d > 0) return new Vector4(C(.45f, .55f, .8f), .10f * (1 - Sdf.Smooth(0, 40, d)));
            var n = p / r; float z = MathF.Sqrt(MathF.Max(0, 1 - n.LengthSquared()));
            float lit = MathF.Max(0, Vector2.Dot(n, light) * .9f + z * .1f);
            float bands = Sdf.Fbm(x / 60f, y / 14f, 41);
            var col = Vector3.Lerp(C(.05f, .06f, .11f), C(.55f, .62f, .78f), MathF.Pow(lit, 1.6f)) * (.85f + .3f * bands);
            return new Vector4(col, Sdf.Cov(d) * .92f);
        });
    }

    static void Astrolabe(Img img, Vector2 c)
    {
        var gold = C(.95f, .80f, .50f);
        img.Over((x, y) =>
        {
            var p = new Vector2(x, y) - c; float r = p.Length();
            if (r < 380 || r > 520) return default;
            float angle = MathF.Atan2(p.Y, p.X) * 180 / MathF.PI + 180;
            float a = 0;
            a = MathF.Max(a, .16f * Sdf.Cov(MathF.Abs(r - 470) - 1.1f));
            a = MathF.Max(a, .11f * Sdf.Cov(MathF.Abs(r - 430) - .7f));
            a = MathF.Max(a, .07f * Sdf.Cov(MathF.Abs(r - 500) - .6f));
            float step = angle % 6, major = angle % 30;
            float tickDist = MathF.Min(step, 6 - step) * r * MathF.PI / 180;
            if (r > 430 && r < 446) a = MathF.Max(a, .12f * Sdf.Cov(tickDist - .6f));
            float majorDist = MathF.Min(major, 30 - major) * r * MathF.PI / 180;
            if (r > 430 && r < 466) a = MathF.Max(a, .16f * Sdf.Cov(majorDist - .9f));
            // A small diamond marks each of the twelve houses on the outer ring.
            float house = MathF.Min(major, 30 - major) * 486 * MathF.PI / 180;
            a = MathF.Max(a, .2f * Sdf.Cov((MathF.Abs(r - 486) + house - 6) * .7071f));
            return a <= 0 ? default : new Vector4(gold, a);
        });
    }

    /// <summary>Half width of the tower at height y: a tapering shaft with ledges, a crown and a spire.</summary>
    static float HalfWidth(float y)
    {
        if (y < TipY) return -1;
        if (y < CrownY - 26) return 34 * Sdf.Smooth(TipY, CrownY - 26, y) + 2;
        if (y < CrownY + 10) return 92;
        float t = (y - CrownY) / (H - CrownY), w = 64 + 160 * t * t + 30 * t;
        foreach (float ledge in new[] { 520f, 720f, 930f, 1160f })
            if (y > ledge && y < ledge + 20) w += 16;
        return w;
    }

    static void Tower(Img img)
    {
        img.Over((x, y) =>
        {
            float w = HalfWidth(y); if (w <= 0) return default;
            float d = MathF.Abs(x - TowerX) - w, cov = Sdf.Cov(d);
            if (cov <= 0) return default;
            float v = y / H, stone = Sdf.Fbm(x / 18f, y / 9f, 71);
            var col = C(.012f, .016f, .034f) * (.85f + .3f * stone) + C(.03f, .035f, .06f) * Sdf.Smooth(.55f, 1, v);
            // Moonlight grazes the left flank; the crown's rim catches the glow above.
            if (x < TowerX) col += C(.09f, .12f, .22f) * (1 - Sdf.Smooth(0, 9, -d));
            if (y < CrownY + 10 && y > CrownY - 26) col += C(.16f, .12f, .06f) * (1 - Sdf.Smooth(0, 5, y - (CrownY - 26)));
            // Rows of lit windows, sparse and deterministic.
            int row = (int)((y - CrownY) / 46), col4 = (int)MathF.Floor((x - TowerX) / 22);
            if (y > CrownY + 40 && Sdf.Hash(row, col4, 5) > .82f)
            {
                float wx = (x - TowerX) - (col4 * 22 + 11), wy = (y - CrownY) - (row * 46 + 23);
                float win = Sdf.Cov(Sdf.Box(new Vector2(wx, wy), Vector2.Zero, new Vector2(2.6f, 6), 1.5f));
                if (MathF.Abs(x - TowerX) < w - 10) col = Vector3.Lerp(col, C(1, .74f, .36f) * (.6f + .4f * Sdf.Hash(row, col4, 9)), win);
            }
            return new Vector4(col, cov);
        });
        // The spire's tip burns gold and sends a faint beam into the sky.
        img.Over((x, y) =>
        {
            float dx = x - TowerX, dy = y - TipY, r2 = dx * dx + dy * dy;
            float a = .9f * MathF.Exp(-r2 / (2 * 9f * 9f)) + .35f * MathF.Exp(-r2 / (2 * 46f * 46f));
            if (y < TipY) a = MathF.Max(a, .22f * MathF.Exp(-dx * dx / (2 * 5f * 5f)) * Sdf.Smooth(0, TipY, y));
            return a < .004f ? default : new Vector4(C(1, .88f, .62f), MathF.Min(a, 1));
        });
    }

    static void Mist(Img img)
    {
        img.Over((x, y) =>
        {
            float v = y / H;
            if (v < .5f) return default;
            float n = Sdf.Fbm(x / 640f, y / 150f, 91), band = Sdf.Smooth(.5f, 1f, v);
            float a = band * (.18f + .62f * Sdf.Smooth(.3f, .75f, n)) * .85f;
            return new Vector4(Vector3.Lerp(C(.10f, .13f, .24f), C(.05f, .06f, .12f), Sdf.Smooth(.75f, 1, v)), MathF.Min(a, .9f));
        });
    }
}
