namespace BackdropPainter;

/// <summary>Temple furniture for the papyrus theme: painted papyrus-bundle columns, obelisks, seated
/// jackal statues, papyrus reeds, hanging bronze lamps and scarab emblems.</summary>
internal static class PapyrusKit
{
    static Ramp Sand => PapyrusTheme.Sand;
    static Ramp Fronds => PapyrusTheme.Fronds;
    static Ramp Bronze => PapyrusTheme.Bronze;
    static Ramp Basalt => PapyrusTheme.Basalt;

    /// <summary>A papyrus-bundle column: base drum, ribbed shaft carrying painted registers, lashed neck,
    /// a striped closed-bud capital and an abacus. <paramref name="neck"/> is the top of the shaft.</summary>
    public static void Column(Canvas p, float cx, float baseY, float neck, float r, int seed)
    {
        var f = p.F;
        float budTop = neck + 1.7f, abacusTop = budTop + .35f, shaft0 = baseY + .36f, shaft1 = neck - .6f;
        p.Paint(cx - r * 1.5f, cx + r * 1.5f, baseY, abacusTop + .05f, (x, y, i, j) =>
        {
            float dx = x - cx;
            if (y < shaft0)
            {
                float hw = r * 1.3f; if (MathF.Abs(dx) > hw) return Col.Clear;
                float v = Kit.Cylinder(dx / hw, .2f) * .72f + (y > shaft0 - .05f ? .18f : 0);
                return Sand.At(v, i, j, .4f);
            }
            if (y >= budTop)
            {
                float hw = r * .92f; if (MathF.Abs(dx) > hw || y > abacusTop) return Col.Clear;
                float v = .58f + (dx < -hw * .55f ? .16f : dx > hw * .45f ? -.16f : 0) + (y > abacusTop - .05f ? .14f : 0);
                return Sand.At(v + f.N(x, y, .3f, seed + 9) * .04f, i, j, .4f);
            }
            if (y >= neck)
            {
                // Closed papyrus bud: swells then tapers, painted in sepal stripes of green and gold.
                float t = (y - neck) / 1.7f;
                float prof = t < .45f ? M.Lerp(.86f, 1.38f, MathF.Sin(t / .45f * MathF.PI * .5f)) : M.Lerp(1.38f, .8f, M.Smooth(.45f, 1, t));
                float hw = r * prof; if (MathF.Abs(dx) > hw) return Col.Clear;
                float u = Math.Clamp(dx / hw, -1, 1), light = Kit.Cylinder(u, .22f);
                float ang = MathF.Asin(u) + MathF.PI * .5f, sep = M.Fract(ang / (MathF.PI / 9));
                int stripe = (int)MathF.Floor(ang / (MathF.PI / 9));
                Col c = stripe % 3 == 0 ? PapyrusTheme.Ochre : stripe % 3 == 1 ? Fronds[2] : Fronds[4];
                if (t > .92f) c = PapyrusTheme.Lapis;
                else if (t < .07f) c = PapyrusTheme.Terracotta;
                else if (sep < .11f) c = Col.Lerp(c, PapyrusTheme.Ink, .55f);
                else if (sep > .75f) c = Col.Lerp(c, Col.White, .1f);
                return Kit.Grain(c * ((.42f + .58f * light) * (1 - M.Smooth(.7f, 1, t) * .22f)), i, j, seed + 3, .03f, .1f);
            }
            if (y >= shaft1)
            {
                float hw = r * .86f; if (MathF.Abs(dx) > hw) return Col.Clear;
                float light = Kit.Cylinder(dx / hw, .22f);
                int ring = (int)MathF.Floor((neck - y) / .12f);
                Col c = ring % 3 == 0 ? PapyrusTheme.Lapis : ring % 3 == 1 ? PapyrusTheme.Ochre : PapyrusTheme.Teal;
                if (M.Fract((neck - y) / .12f) < .18f) c = Col.Lerp(c, PapyrusTheme.Ink, .4f);
                return c * (.42f + .58f * light);
            }
            // Shaft: a bundle of stems, slightly waisted at the foot.
            float tt = (y - shaft0) / (shaft1 - shaft0);
            float hwS = r * (tt < .12f ? M.Lerp(.9f, 1, tt / .12f) : M.Lerp(1, .9f, (tt - .12f) / .88f));
            if (MathF.Abs(dx) > hwS) return Col.Clear;
            float us = Math.Clamp(dx / hwS, -1, 1), lightS = Kit.Cylinder(us, .18f);
            float su = M.Fract((us + 1) * .5f * 4) * 2 - 1, stem = Kit.Cylinder(su, .3f);
            float xu = cx + MathF.Asin(us) * r;
            float k = M.Mod(y - baseY - .9f, 1.7f);
            if (y > baseY + .9f && PapyrusTheme.Register(xu, k, 1, lightS * (.72f + stem * .28f), seed, i, j, out Col col)) return col;
            float vs = lightS * .62f + stem * .24f + f.Fbm(x, y, .4f, 3, seed + 1) * .06f;
            return Kit.Grain(Sand.At(vs, i, j, .4f), i, j, seed + 2, .04f, .12f);
        });
    }

    /// <summary>A tapering obelisk on a stepped pedestal with incised glyph columns and a gilded pyramidion.</summary>
    public static void Obelisk(Canvas p, float ox, float baseY, float top, int seed)
    {
        var f = p.F;
        p.Paint(ox - .75f, ox + .75f, baseY, top + .9f, (x, y, i, j) =>
        {
            float dx = x - ox;
            if (y < baseY + .9f)
            {
                float hw = y < baseY + .45f ? .64f : .5f; if (MathF.Abs(dx) > hw) return Col.Clear;
                float v = .5f + (dx < -hw + .1f ? .16f : 0) + (M.Mod(y - baseY, .45f) > .4f ? .16f : 0);
                if (y > baseY + .5f && y < baseY + .85f && MathF.Abs(dx) < hw - .08f && PapyrusTheme.Glyph(x, y, .18f, .3f, seed, out _)) v -= .22f;
                return Sand.At(v, i, j, .4f);
            }
            float t = (y - baseY - .9f) / (top - baseY - .9f), hwT = M.Lerp(.36f, .25f, M.Clamp01(t));
            if (t > 1)
            {
                float pt = (y - top) / .85f;
                if (pt > 1 || MathF.Abs(dx) > hwT * (1 - pt)) return Col.Clear;
                return Kit.Mix(Bronze[3], Bronze[5], .55f - dx * 1.6f - pt * .3f, i, j);
            }
            if (MathF.Abs(dx) > hwT) return Col.Clear;
            float split = -hwT * .3f; bool lit = dx < split;
            float v2 = lit ? .74f : .42f;
            if (MathF.Abs(dx - split) < 1.2f / p.Ppu) v2 += .14f;
            if (MathF.Abs(MathF.Abs(dx) - hwT) < 1.2f / p.Ppu) v2 -= .12f;
            float faceC = lit ? (split - hwT) * .5f : (split + hwT) * .5f, faceW = lit ? hwT + split : hwT - split;
            if (MathF.Abs(dx - faceC) < faceW * .32f && PapyrusTheme.Glyph(dx - faceC + ox, y, faceW * .64f, faceW * .9f, seed + 3, out _)) v2 -= .24f;
            return Sand.At(v2 + f.Fbm(x, y, .3f, 2, seed + 5) * .05f, i, j, .4f);
        });
    }

    /// <summary>A seated jackal (Anubis) in black stone with a gold collar on a carved plinth; facing=1 looks left.</summary>
    public static void Anubis(Canvas p, float cx, float baseY, int seed, int facing = 1)
    {
        var f = p.F;
        p.Paint(cx - 1.4f, cx + 1.4f, baseY, baseY + 3.9f, (x, y, i, j) =>
        {
            float dx = (x - cx) * facing, h = y - baseY;
            if (h < .7f)
            {
                if (MathF.Abs(x - cx) > 1.05f) return Col.Clear;
                float v = .5f + (h > .64f ? .18f : 0) + (x - cx < -.92f ? .14f : 0) + (x - cx > .95f ? -.12f : 0);
                if (h > .12f && h < .55f && MathF.Abs(x - cx) < .9f && PapyrusTheme.Glyph(x, y, .2f, .4f, seed, out _)) v -= .22f;
                return Sand.At(v, i, j, .4f);
            }
            float b = h - .7f;
            bool haunch = (dx - .32f) * (dx - .32f) / .3f + (b - .48f) * (b - .48f) / .23f < 1 && b > 0;
            bool body = dx > -.62f && dx < .6f && b > .55f && b < 1.62f - MathF.Max(0, dx + .3f) * .8f;
            bool legs = (dx > -.64f && dx < -.42f && b < .75f) || (dx > -.36f && dx < -.14f && b < .75f) || (dx > -.64f && dx < -.14f && b >= .5f && b < .75f);
            bool neck = b > 1.5f && b < 2.3f && MathF.Abs(dx + .35f + (b - 1.5f) * .3f) < .17f;
            bool head = (dx + .62f) * (dx + .62f) / .09f + (b - 2.32f) * (b - 2.32f) / .04f < 1;
            bool snout = dx < -.74f && dx > -1.22f && MathF.Abs(b - 2.28f - (dx + .74f) * .06f) < .1f * (1 - (-.74f - dx) / .5f) + .03f;
            bool ears = PapyrusTheme.Tri(-.74f, 2.4f, -.56f, 2.4f, -.67f, 3.05f, dx, b) || PapyrusTheme.Tri(-.52f, 2.4f, -.33f, 2.4f, -.42f, 3.08f, dx, b);
            if (!(haunch || body || legs || neck || head || snout || ears)) return Col.Clear;
            if (neck && b > 1.78f && b < 1.98f) return Kit.Mix(Bronze[3], Bronze[5], .35f - dx * .6f + (M.Mod(b, .06f) < .02f ? .25f : 0), i, j);
            if (head && dx > -.6f && dx < -.5f && b > 2.33f && b < 2.4f) return Col.Hex("f0c050");
            float v2 = .32f + (dx < -.2f ? .22f : 0) - (haunch && dx > .5f ? .12f : 0) + (ears ? -.06f : 0);
            if (legs && dx > -.36f && b < .5f) v2 -= .12f;
            if (body && b > 1.45f - MathF.Max(0, dx + .3f) * .8f) v2 += .12f;
            return Basalt.At(v2 + f.N(x, y, .25f, seed + 1) * .06f, i, j, .4f);
        });
    }

    /// <summary>One papyrus plant: a leaning triangular stem topped by a spray of thin drooping rays.</summary>
    public static void Papyrus(Canvas p, float x, float baseY, float h, float lean, int seed)
    {
        var rng = new Rng(seed);
        float tx = x + lean * h, ty = baseY + h, R = .2f + h * .1f;
        int n = 15 + rng.I(0, 6);
        var rays = new (float ca, float sa, float len, float droop)[n];
        for (int k = 0; k < n; k++)
        {
            float a = -MathF.PI * .12f + MathF.PI * 1.24f * (k + rng.R(.2f, .8f)) / n;
            rays[k] = (MathF.Cos(a), MathF.Sin(a), R * rng.R(.55f, 1), rng.R(.2f, .5f));
        }
        p.Paint(Math.Min(x, tx) - .05f, Math.Max(x, tx) + .05f, baseY, ty, (px, py, i, j) =>
        {
            float t = (py - baseY) / h, cx = x + lean * h * t * t, d = MathF.Abs(px - cx) * p.Ppu;
            if (d < 1.3f) return d < .65f ? Fronds[4] : Fronds[2];
            return Col.Clear;
        });
        float w2 = (1.1f / p.Ppu) * (1.1f / p.Ppu);
        p.Paint(tx - R * 1.3f, tx + R * 1.3f, ty - R * 1.1f, ty + R * 1.1f, (px, py, i, j) =>
        {
            float dx = px - tx, dy = py - ty;
            foreach (var r in rays)
            {
                for (int s2 = 0; s2 <= 10; s2++)
                {
                    float t = s2 / 10f, rx = r.ca * r.len * t, ry = r.sa * r.len * t - r.droop * r.len * t * t;
                    float ddx = dx - rx, ddy = dy - ry;
                    if (ddx * ddx + ddy * ddy < w2 * (t < .3f ? 1.4f : 1)) return Fronds.At(.45f + t * .5f + (r.ca < 0 ? .08f : 0), i, j, .3f);
                }
            }
            return Col.Clear;
        });
    }

    /// <summary>A hanging bronze oil lamp on a chain, flame alight. <paramref name="y"/> is the bowl's rim.</summary>
    public static void Lamp(Canvas p, float x, float chainTop, float y)
    {
        p.Paint(x - .3f, x + .3f, y - .36f, chainTop, (px, py, i, j) =>
        {
            float dx = px - x;
            if (py > y + .24f)
            {
                float d = MathF.Abs(dx) * p.Ppu; int link = (int)MathF.Floor((py - y) * p.Ppu / 3);
                if (d < 1.1f) return link % 2 == 0 ? Bronze[2] : Bronze[4];
                return d < 2.1f && link % 2 == 0 ? Bronze[1] : Col.Clear;
            }
            if (py > y + .05f)
            {
                float ft = (py - (y + .04f)) / .2f, fw = .05f * MathF.Sin(MathF.PI * Math.Min(1, ft * 1.1f)) * (1 - ft * .4f) + .004f;
                if (MathF.Abs(dx) < fw) return Kit.Mix(Col.Hex("fff6c8"), PapyrusTheme.Flame, ft * .8f + MathF.Abs(dx) / fw * .5f, i, j);
                float s = py - y - .05f;
                if (MathF.Abs(MathF.Abs(dx) - .15f + s * .75f) < .012f) return Bronze[2];
                return Col.Clear;
            }
            float t = (y + .05f - py) / .4f; if (t > 1) return Col.Clear;
            float hw = .17f * MathF.Sqrt(1 - t * t); if (MathF.Abs(dx) > hw) return Col.Clear;
            float v = Kit.Cylinder(dx / hw, .25f) * .68f + (py > y - .02f ? .28f : 0) - t * .2f;
            if (M.Mod(py - y, .1f) < .022f) v += .16f;
            return Bronze.At(v, i, j, .4f);
        });
    }

    /// <summary>A scarab emblem pushing the sun disc: lapis wing-cases, teal thorax, black head and legs.</summary>
    public static void Scarab(Canvas p, float x, float y, float sc)
    {
        var ink = PapyrusTheme.Ink;
        p.Paint(x - sc * 2.3f, x + sc * 2.3f, y - sc * 1.7f, y + sc * 2.7f, (px, py, i, j) =>
        {
            float dx = (px - x) / sc, dy = (py - y) / sc;
            float sd = MathF.Sqrt(dx * dx + (dy - 1.95f) * (dy - 1.95f));
            if (sd < .62f) return sd > .5f ? ink : Kit.Mix(PapyrusTheme.Terracotta, PapyrusTheme.Ochre, .5f - dx * .4f + (dy - 1.95f) * .4f, i, j);
            float ax = MathF.Abs(dx);
            foreach (var (x0, y0, x1, y1) in new[] { (.45f, .65f, 1.05f, 1.55f), (.78f, .15f, 1.55f, .3f), (.62f, -.5f, 1.35f, -1.15f) })
            {
                float ex = x1 - x0, ey = y1 - y0, t = M.Clamp01(((ax - x0) * ex + (dy - y0) * ey) / (ex * ex + ey * ey));
                float ddx = ax - (x0 + ex * t), ddy = dy - (y0 + ey * t);
                if (ddx * ddx + ddy * ddy < .011f) return ink;
            }
            float e = dx * dx + (dy + .1f) * (dy + .1f) / .6f;
            if (e < 1)
            {
                if (e > .82f || ax < .05f) return ink;
                return Kit.Mix(PapyrusTheme.LapisDark, Col.Hex("4a7ad0"), .3f - dx * .45f + dy * .25f, i, j, 3);
            }
            float th = dx * dx / .52f + (dy - .55f) * (dy - .55f) / .16f;
            if (th < 1) return th > .78f ? ink : Kit.Mix(Col.Hex("1a7068"), Col.Hex("58c4b0"), .4f - dx * .4f, i, j, 3);
            if (dx * dx / .2f + (dy - .95f) * (dy - .95f) / .1f < 1) return ink;
            return Col.Clear;
        });
    }
}
