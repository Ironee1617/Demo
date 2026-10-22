namespace BackdropPainter;

/// <summary>Lava citadel: monumental eroded arches over a molten river, columnar basalt cliffs and
/// ember-lit stalactites under a smoky vault.</summary>
public sealed class LavaTheme : ThemePainter
{
    public override string Id => "lava";

    static readonly Ramp Basalt = new("110a12", "1a111b", "251825", "33212f", "45303c", "5b4049", "77555a", "9a7068");
    static readonly Col Ember = Col.Hex("ff8a3a"), Molten = Col.Hex("ffd27a"), Heat = Col.Hex("c8401e");
    static readonly (float y, Col c)[] SkyStops = Kit.Stops((-11, "ffa04a"), (-8, "e0662c"), (-5.5f, "a4381f"), (-3, "6e2220"), (0, "48171f"), (3.5f, "2c0f1a"), (10, "160912"));

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return Sky();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    PaintedLayer Sky()
    {
        var s = new LayerSpec("sky", .95f, 48, 30, -11, 21, -97) { Under = SkyStops[0].c, Over = SkyStops[^1].c };
        var c = s.NewCanvas(); var f = c.F;
        c.PaintAll((x, y, i, j) => Kit.Gradient(SkyStops, y, 4, 1, i, j));
        // Billowing smoke under the vault, lit faintly from below.
        c.Paint(0, c.Period, -4, 10, (x, y, i, j) =>
        {
            float n = f.Fbm(x + f.N(x, y, 9, 3) * 2, y * 1.6f, 6, 4, 21);
            float a = M.Smooth(.05f, .6f, n) * M.Smooth(-4, 1, y) * .5f;
            float lit = M.Smooth(.35f, .75f, f.Fbm(x, y * 1.6f - .4f, 6, 3, 22));
            float q = MathF.Floor(a * 5 + Dither.Bayer(i, j)) / 5;
            return q <= 0 ? Col.Clear : Col.Lerp(Col.Hex("3a1520"), Col.Hex("6e2d2a"), lit).WithA(q);
        });
        // Third plane: the cathedral's far wall, barely darker than the haze.
        c.Paint(0, c.Period, -11, 9, (x, y, i, j) =>
        {
            float span = f.Snap(3.4f), u = M.Mod(x, span) - span * .5f;
            float top = 5.2f + f.Fbm(x, 0, 9, 2, 11) * 1.4f + MathF.Max(0, 1 - MathF.Abs(M.Mod(x, f.Snap(7.5f)) - 1.2f) / .35f) * 2.6f;
            if (y > top || Kit.InOpening(u, y, .95f, -.4f, -30, .22f)) return Col.Clear;
            var bg = Kit.Gradient(SkyStops, y, 4, 1, i, j);
            return Col.Lerp(bg, Col.Hex("3a1622"), .32f);
        });
        foreach (var gx in new[] { 3f, 13.5f, 23f }) Kit.Glow(c, gx, -8.5f, 9, Ember, .3f, .5f);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Far()
    {
        var s = new LayerSpec("far", .85f, 64, 32, -11, 21, -96) { Over = Col.Hex("1a0b14") };
        var c = s.NewCanvas(); var f = c.F;
        float LakeTop(float x) => -6.6f + f.Fbm(x, 3, 4, 2, 31) * .1f;

        // Molten river seen edge-on: flowing streaks beneath a glowing lip.
        c.Paint(0, c.Period, -11, -6, (x, y, i, j) =>
        {
            float top = LakeTop(x); if (y > top) return Col.Clear;
            float t = M.InvLerp(top, -11, y);
            float streak = f.Fbm2(x, y, 2.6f, .14f, 3, 33);
            if (top - y < 1.5f / 64) return Col.Hex("ffcf78");
            if (streak > .22f + t * .4f) return Kit.Mix(Col.Hex("4a171a"), Col.Hex("7a261b"), t + streak * .3f, i, j);
            return Kit.Mix(Col.Hex("d4552a"), Molten, t * .75f + streak * .35f, i, j);
        });

        // Monumental arcade: moulded orders, engaged shafts, glowing oculi and a crenellated gallery.
        var arches = c.Blank();
        float span = f.Snap(8f);
        int bays = (int)MathF.Round(c.Period / span);
        var style = new ArcadeStyle
        {
            Span = 8, PierHalf = .95f, Base = -6.9f, Spring = 2.2f, Pointed = .2f, Deck = 6.4f,
            GalleryTop = 7.9f, GallerySpan = 2, GalleryHalf = .46f, GallerySpring = 7.15f, Merlons = true,
            Missing = (x, y) =>
            {
                // The gallery is gap-toothed where its masonry has fallen.
                return y > 6.6f && f.Fbm(x, y, 1.4f, 3, 48) > .38f;
            }
        };
        var stone = new Ramp("190a16", "22101d", "2e1523", "3c1a28", "4c202d", "612a33", "7c3a3a");
        arches.Paint(0, c.Period, -7, c.Top, (x, y, i, j) =>
        {
            var h = ArchKit.Hit(style, f, x, y);
            if (h.Part == Part.None) return Col.Clear;
            if (h.Part == Part.Oculus) return Kit.Mix(Col.Hex("4a1819"), Col.Hex("c85a2c"), 1.1f - h.Detail, i, j);
            float tone = ArchKit.BaseTone(h);
            if (h.Part is Part.Wall or Part.Pier or Part.Gallery or Part.Plinth)
            {
                bool mortar = Kit.Masonry(f, x, y, .9f, .42f, 1.4f / 64, 51, out float sh, out _, out float ly, out _);
                tone += (sh - .5f) * .12f;
                if (mortar) tone -= .14f; else if (ly < 3f / 64) tone += .05f;
            }
            if (h.Part == Part.Ring && M.Mod(MathF.Atan2(y - style.Spring, M.Mod(x, span) - span * .5f) * 180 / MathF.PI, 9) < 1.2f) tone -= .12f;
            float key = Kit.Lambert(h.N.X, h.N.Y, h.N.Z);
            float under = Math.Max(0, -h.N.Y) * .8f + M.Smooth(.5f, -6.5f, y) * .55f;
            tone = tone * .5f + key * .5f + f.Fbm(x, y, .9f, 3, 49) * .08f;
            if (f.Ridge(x, y, 2.2f, 3, 52) > .9f) tone -= .15f;
            var col = stone.At(tone, i, j, .5f);
            return Kit.Mix(col, Col.Hex("b4452a"), under * .55f, i, j, 5);
        });
        Kit.Edge(arches, 0, -1, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("c0502c"), d == 0 ? .6f : .3f));
        Kit.Fog(arches, y => Kit.Smooth(SkyStops, y), y => .3f + M.Smooth(0, -6.5f, y) * .42f);
        c.Over(arches);
        // The oculi glow from the fires beyond.
        for (int k = 0; k < bays; k++)
        {
            float ox = k * span + style.Phase, oy = style.Spring + (Kit.ApexHeight(span * .5f - style.PierHalf, style.Pointed)) * .78f;
            Kit.Glow(c, ox, oy, 1.2f, Ember, .3f);
        }

        // The vault: a ragged rock ceiling with distant stalactites.
        var vault = c.Blank();
        float Roof(float x) => 8.4f + f.Fbm(x, 9, 4, 4, 37) * .7f;
        vault.Paint(0, c.Period, 5, c.Top, (x, y, i, j) =>
        {
            if (y > Roof(x)) return Col.White;
            float sx = f.Snap(.55f); int k = (int)MathF.Floor(x / sx), n = (int)MathF.Round(c.Period / sx);
            for (int o = -2; o <= 2; o++)
            {
                int kk = M.Mod(k + o, n);
                float h = Field.Hash01(kk, 3, 7); if (h < .45f) continue;
                float cx = (k + o + .5f) * sx + (Field.Hash01(kk, 4, 7) - .5f) * sx;
                float len = .3f + MathF.Pow((h - .45f) / .55f, 2) * 3f, half = .1f + len * .09f;
                float t = (Roof(cx) - y) / len;
                if (t >= 0 && t < 1 && MathF.Abs(x - cx) < half * (1 - t) * (1 + f.N(x, y, .3f, 9) * .2f)) return Col.White;
            }
            return Col.Clear;
        });
        Kit.Inflate(vault, 10, (i, j, src, hgt, n) =>
        {
            float under = Math.Max(0, -n.Y);
            var col = new Ramp("170910", "1f0c15", "2a111b").At(.3f + Kit.Lambert(n.X, n.Y, n.Z) * .4f, i, j, .6f);
            return Kit.Mix(col, Col.Hex("6c2422"), under * .7f, i, j, 4);
        });
        Kit.Fog(vault, y => Kit.Smooth(SkyStops, y), y => .25f);
        c.Over(vault);

        foreach (var bx in new[] { 2.5f, 11f, 19.5f, 27f }) Kit.Glow(c, bx, -6.4f, 2.4f, Molten, .45f, .35f);
        return new PaintedLayer(s, c);
    }

    /// <summary>Columnar basalt: hexagonal prisms with lit caps, joints and lava-filled seams.</summary>
    static void Columns(Canvas p, Rng rng, float x0, float x1, float baseY, Func<float, float> top, int dir, int seed)
    {
        var f = p.F; float x = x0; float px1 = 1f / p.Ppu;
        while (x < x1)
        {
            float w = rng.R(.3f, .74f), cx = x + w * .5f, end = top(cx) + rng.R(-.5f, .45f) * dir;
            if (rng.Chance(.14f)) end -= rng.R(.7f, 1.8f) * dir;
            float lit = rng.R(.2f, .3f), shade = rng.R(.66f, .76f), cap = w * rng.R(.2f, .28f), left = x;
            int id = (int)(x * 97) + seed;
            float joint = 1.1f + Field.Hash01(id, 5, seed) * 1.3f, jointPhase = Field.Hash01(id, 3, seed) * 3;
            p.Paint(left, left + w, Math.Min(baseY, end) - .1f, Math.Max(baseY, end) + .1f, (qx, qy, i, j) =>
            {
                float u = (qx - left) / w, h = (qy - baseY) * dir, len = (end - baseY) * dir;
                if (h < 0 || h > len) return Col.Clear;
                float fromTip = len - h;
                // A notched profile: joints step the faces slightly and chip the corners.
                int seg = (int)MathF.Floor((h + jointPhase) / joint);
                float jog = (Field.Hash01(id, seg, seed + 1) - .5f) * .06f;
                float uu = u + jog / w;
                if (u < px1 / w || u > 1 - px1 / w) return Basalt[0];
                float capEdge = cap * (1 - MathF.Abs(u - .5f) * .9f);
                float tone;
                if (fromTip < capEdge) tone = fromTip < 1.5f * px1 ? 7 : 6f;
                else if (uu < lit) tone = 4.8f;
                else if (uu < shade) tone = 3f;
                else tone = 1.3f;
                float jy = M.Mod(h + jointPhase, joint);
                if (fromTip > capEdge + .05f)
                {
                    if (jy < 1.5f * px1) tone = .3f;
                    else if (jy < 3f * px1) tone += 1.2f;
                    else if (jy < 6f * px1) tone += .5f;
                }
                // Weathered vertical streaks and pitting.
                tone += f.Fbm2(qx, qy, .35f, 1.4f, 3, seed + 7) * 1.1f;
                if (f.Ridge(qx, qy, .9f, 2, seed + 9) > .93f) tone -= 1.4f;
                float glow = dir > 0 ? M.Smooth(-1.5f, -8.5f, qy) : 0;
                var col = Basalt.At(tone / 7, i, j, .55f);
                col = Kit.Grain(col, i, j, seed + 11, .06f, .16f);
                return glow > 0 ? Kit.Mix(col, Col.Lerp(Heat, Ember, glow * .5f), glow * .75f, i, j, 5) : col;
            });
            // Seams glow where the columns meet, strongest near the river.
            p.Paint(left - 1.5f * px1, left + 1.5f * px1, Math.Min(baseY, end), Math.Max(baseY, end), (qx, qy, i, j) =>
            {
                float heat = M.Smooth(-2, -8, qy);
                float n = f.N2(qx, qy, .3f, .5f, seed + 13);
                return heat * (n * .5f + .8f) > .5f ? Kit.Mix(Heat, Molten, heat * .7f, i, j) : Col.Clear;
            });
            x += w;
        }
    }

    PaintedLayer Mid()
    {
        var s = new LayerSpec("mid", .65f, 96, 36, -9.5f, 22, -95);
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(201);
        var cliffs = c.Blank();
        float Bump(float t, float peak) => MathF.Pow(MathF.Sin(MathF.PI * M.Clamp01(t)), .65f) * (1 - MathF.Abs(t - peak) * .45f);
        float TopA(float x) => -4.8f + 7.4f * Bump((x - .4f) / 8.8f, .35f);
        float TopB(float x) => -4.6f + 10.2f * Bump((x - 19.5f) / 11.5f, .36f);
        float TopC(float x) => -5.4f + 3.8f * Bump((x - 12.6f) / 3.2f, .5f);
        Columns(cliffs, rng, .4f, 9.2f, -9.6f, TopA, 1, 300);
        Columns(cliffs, rng, 12.6f, 15.8f, -9.6f, TopC, 1, 305);
        Columns(cliffs, rng, 19.5f, 31f, -9.6f, TopB, 1, 310);
        Kit.Edge(cliffs, -1, 0, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("9a7a88"), .4f - d * .15f));

        // A lava cascade breaks out between the columns of the tall cliff and runs down its face.
        float sx = 23.4f, sy = TopB(23.4f) - 1.1f;
        cliffs.Paint(sx - 1, sx + 1.4f, -9.6f, sy + .4f, (x, y, i, j) =>
        {
            float drop = sy - y; if (drop < -.18f) return Col.Clear;
            float centre = sx + MathF.Sin(y * 1.4f) * .05f + f.N(0, y, 1.5f, 75) * .08f;
            float half = .14f + Math.Max(0, drop) * .028f + f.N2(x, y, .2f, .5f, 77) * .04f;
            if (drop < 0) half = .14f * MathF.Sqrt(1 + drop / .18f);
            float d = MathF.Abs(x - centre) / half;
            if (d > 1) return Col.Clear;
            float n = f.Fbm2(x, y + drop * .3f, .1f, .8f, 3, 71) * .5f + .5f;
            if (f.Fbm2(x, y, .08f, .3f, 2, 73) > .48f && d > .3f) return Col.Hex("6a2219");
            if (d > .82f) return Col.Hex("a8361e");
            return d < .3f + n * .2f ? Kit.Mix(Col.Hex("ffb850"), Col.Hex("fff3c4"), n * (1 - d), i, j) : Kit.Mix(Heat, Ember, n * 1.3f - d * .3f, i, j);
        });
        Kit.Fog(cliffs, y => Col.Hex("a83c26"), y => M.Smooth(-4.5f, -9.5f, y) * .55f);
        Kit.Fog(cliffs, y => Col.Hex("2a111b"), y => .08f);

        // Hanging rock masses under the vault, hazier than the near plane.
        var roof = c.Blank();
        float Roof(float x) => 11.4f + f.Fbm(x, 4, 5, 4, 211) * .9f;
        var spikes = new List<(float x, float y, float len, float w)>();
        foreach (var (cx, len, w) in new[] { (15.5f, 4.2f, 3.2f), (34.6f, 3.4f, 2.6f), (5.5f, 2.2f, 1.6f), (26.5f, 2.6f, 1.8f) })
            for (int k = 0; k < 9; k++)
            {
                float ox = k == 0 ? 0 : rng.R(-w * .8f, w * .8f);
                spikes.Add((cx + ox, Roof(cx + ox) + .4f, len * (k == 0 ? 1 : rng.R(.3f, .7f)), w * (k == 0 ? .7f : rng.R(.2f, .4f))));
            }
        roof.Paint(0, c.Period, 4, c.Top, (x, y, i, j) =>
        {
            if (y > Roof(x)) return Col.White;
            foreach (var sp in spikes)
            {
                float dx = f.Dx(x, sp.x);
                if (MathF.Abs(dx) < sp.w && Kit.Spike(f, sp.x + dx, y, sp.x, sp.y, sp.len, sp.w, -1, 213, out _, out _, .25f, 1.2f)) return Col.White;
            }
            return Col.Clear;
        });
        Kit.Inflate(roof, 16, (i, j, src, hgt, n) =>
        {
            float x = roof.X(i), y = roof.Y(j);
            float v = .15f + Kit.Lambert(n.X, n.Y, n.Z) * .35f + f.Fbm(x, y, .6f, 3, 215) * .12f;
            var col = Basalt.At(v * .8f, i, j, .55f);
            return Kit.Mix(col, Col.Hex("c0512c"), Math.Max(0, -n.Y) * .9f - .15f, i, j, 4);
        }, (i, j) => f.Fbm(roof.X(i), roof.Y(j), .7f, 3, 216) * 5);
        Kit.Fog(roof, y => Col.Hex("2a111b"), y => .18f);

        // Smoke plumes rising from the river, warm underneath and fading into the vault.
        var smoke = c.Blank();
        foreach (var px in new[] { 10.6f, 17.4f, 32.6f })
            smoke.Paint(px - 3, px + 3, -9.5f, 9, (x, y, i, j) =>
            {
                float rise = y + 9.5f, sway = MathF.Sin(rise * .45f + px) * .5f + rise * .12f;
                float w = .5f + rise * .17f, d = MathF.Abs(x - px - sway) / w;
                float n = f.Fbm(x, y - rise * .1f, .9f, 4, 219) * .5f + .5f;
                float a = (1 - d) * n * 1.4f - .25f;
                a *= M.Smooth(9, 1, y) * .5f;
                float q = MathF.Floor(a * 5 + Dither.Bayer(i, j)) / 5;
                return q <= 0 ? Col.Clear : Col.Lerp(Col.Hex("8a3a2c"), Col.Hex("3c1d24"), M.InvLerp(-8, 4, y)).WithA(q);
            });
        c.Over(roof);
        c.Over(smoke);
        c.Over(cliffs);
        Kit.Glow(c, sx + .1f, -8.6f, 2.8f, Ember, .7f, .55f);
        Kit.Glow(c, sx, sy, 1.3f, Molten, .45f);
        Kit.Glow(c, sx + .1f, (sy - 9) * .5f, 1.2f, Ember, .3f, 4);
        foreach (var gx in new[] { 10.8f, 17.5f, 32f }) Kit.Glow(c, gx, -8.8f, 2.4f, Ember, .5f, .45f);
        Kit.Stars(c, -9.4f, 3, .00035f, Col.Hex("ffb85a"), 217, (x, y) => M.Smooth(3, -6, y));
        return new PaintedLayer(s, c);
    }

    PaintedLayer Near()
    {
        var s = new LayerSpec("near", .40f, 96, 40, 2, 12.5f, -94) { Over = Col.Hex("0c070c") };
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(401);
        var rock = c.Blank();
        float Roof(float x) => 12.2f + f.Fbm(x, 2, 3.5f, 4, 81) * .7f;
        var spikes = new List<(float x, float y, float len, float w)>();
        foreach (var (cx, len, w) in new[] { (6f, 7.6f, 5.2f), (27.5f, 8.4f, 6f), (17f, 4f, 2.6f), (36.5f, 3.4f, 2.2f) })
        {
            spikes.Add((cx, Roof(cx) + .5f, len, w * .8f));
            for (int k = 0; k < 7; k++)
            {
                float ox = rng.R(-w * .6f, w * .6f);
                spikes.Add((cx + ox, Roof(cx + ox) + .3f, len * rng.R(.3f, .8f) * (1 - MathF.Abs(ox) / w * .8f), w * rng.R(.22f, .42f)));
            }
        }
        for (int k = 0; k < 16; k++) { float x = rng.R(0, 40); spikes.Add((x, Roof(x) + .3f, rng.R(.8f, 2.6f), rng.R(.35f, .9f))); }
        rock.Paint(0, c.Period, 2, c.Top, (x, y, i, j) =>
        {
            if (y > Roof(x)) return Col.White;
            foreach (var sp in spikes)
            {
                float dx = f.Dx(x, sp.x);
                if (MathF.Abs(dx) < sp.w && Kit.Spike(f, sp.x + dx, y, sp.x, sp.y, sp.len, sp.w, -1, 91, out _, out _, .28f, 1.15f)) return Col.White;
            }
            return Col.Clear;
        });
        var dark = new Ramp("0c070c", "130b12", "1b1019", "251622", "321e2b", "45293a");
        Kit.Inflate(rock, 26, (i, j, src, hgt, n) =>
        {
            float x = rock.X(i), y = rock.Y(j);
            float key = Kit.Lambert(n.X, n.Y, n.Z), under = Math.Max(0, -n.Y);
            float v = .12f + key * .55f + f.Fbm(x, y, .5f, 3, 85) * .14f;
            var col = dark.At(v, i, j, .5f);
            if (under > .55f) col = Kit.Mix(col, Col.Hex("d0582e"), (under - .55f) * 2.2f, i, j, 4);
            return Kit.Grain(col, i, j, 87, .05f, .2f);
        }, (i, j) => f.Fbm(rock.X(i), rock.Y(j), .9f, 4, 86) * 9);
        Kit.Edge(rock, 0, -1, 2, (col, d, i, j) => d == 0 ? Col.Hex("f08a44") : Col.Lerp(col, Col.Hex("a83c26"), .6f));
        c.Over(rock);
        return new PaintedLayer(s, c);
    }
}
