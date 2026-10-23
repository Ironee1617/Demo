namespace BackdropPainter;

/// <summary>Dangun's sacred mountain at misty dawn, painted like an ink-wash landscape: receding ridges and a cloud
/// sea under a pale sun, the summit city Sinsi beside the crater lake, the great tree Sindansu hung with five-colour
/// ribbons above a stone altar, dolmens, jangseung guardians, prayer cairns, a straw rope with paper strips and the
/// bear's cave glowing warm in the rock.</summary>
public sealed class DangunTheme : ThemePainter
{
    public override string Id => "dangun";

    static readonly (float y, Col c)[] SkyStops = Kit.Stops((-11, "e6d3b4"), (-6, "e9d7bc"), (-2, "e6cfbf"), (1.5f, "d9d2cb"), (4, "c0c9cf"), (7, "a4b5c0"), (10, "8ea3b3"));
    static readonly Ramp RidgeSky = new("9aacb8", "a6b6c0", "b2c0c8", "bdc9cf", "c8d2d6", "d3dadd");
    static readonly Ramp RidgeA = new("6c8290", "788d99", "8599a3", "92a4ac", "9fafb5", "adbabf");
    static readonly Ramp RidgeB = new("4b6468", "587173", "667e7e", "758b89", "859994", "96a69f");
    static readonly Ramp RidgeC = new("2d473c", "395545", "466350", "55725c", "66836a", "799579");
    static readonly Ramp FarPine = new("263c38", "2f4842", "3a544b", "466055");
    static readonly Ramp CloudRamp = new("9aa6b4", "aab4c0", "bcc4cc", "cdd2d6", "dcdedf", "e9e6e2", "f3eee6", "faf5ec");
    static readonly Ramp SeaRamp = new("98a4b0", "a6b0bc", "b4bcc4", "c1c7cc", "cdd0d3", "d8d9d8", "e2e0dc");
    static readonly Ramp Bark = new("1c1814", "2a241d", "3a3228", "4b4234", "5e5443", "726852", "887c62", "a09474");
    static readonly Ramp PineBark = new("221611", "35241a", "4b3426", "634634", "7b5a42", "92704f", "a8885e");
    static readonly Ramp Wood = new("2a1e16", "3e2e22", "54402e", "6a543c", "80684a", "96805a", "ac9870");
    static readonly Ramp Leaf = new("1c2b22", "25392a", "304934", "3e5c3e", "4f704a", "637f56", "7a9566", "98ae7c", "b8c896");
    static readonly Ramp Needle = new("172820", "1f3328", "283f30", "334c38", "405c42", "52704c", "688459");
    static readonly Ramp Stone = new("2b2d2c", "3b3e3b", "4c504c", "5e625d", "72766f", "888b81", "9ea095", "b5b5a9");
    static readonly Ramp Rock = new("1c1b18", "2a2823", "38352e", "48443b", "5a554a", "6d675a", "817a6a");
    static readonly Ramp Straw = new("5a4a28", "7a6636", "9a8448", "b89e5a", "d2b86e");
    static readonly Ramp Mugwort = new("2c4030", "3c5440", "506a50", "6a8664", "8aa480");
    static readonly Ramp Earth = new("2a2219", "3a3024", "4c4030", "5e5040");
    static readonly Ramp Grass = new("2e4a30", "3d5e3c", "4f7448", "6a8c58");
    static readonly Col Mist = Col.Hex("b4c0c8"), Dawn = Col.Hex("f2dfb8"), Ember = Col.Hex("f0a050"), Moss = Col.Hex("5e8a46");
    static readonly Col ObRed = Col.Hex("c8352c"), ObBlue = Col.Hex("2c5aa8"), ObYellow = Col.Hex("e6c232"), ObWhite = Col.Hex("f0ece2"), ObBlack = Col.Hex("22232a");

    public override IEnumerable<PaintedLayer> Paint()
    {
        yield return Sky();
        yield return Far();
        yield return Mid();
        yield return Near();
    }

    static Col FogCol(float y) => Col.Lerp(Kit.Smooth(SkyStops, y), Mist, .25f);
    static Col FogMid(float y) => Col.Lerp(Kit.Smooth(SkyStops, y), Mist, .4f);

    /// <summary>Weathered granite: lichen on upward faces, fine seams, a touch of dawn on the top edges.</summary>
    static Col StoneShade(Canvas p, Field f, int i, int j, V3 n, int seed)
    {
        float x = p.X(i), y = p.Y(j);
        float v = .2f + Kit.Lambert(n.X, n.Y, n.Z) * .58f + f.Fbm(x, y, .4f, 3, seed) * .1f;
        var col = Stone.At(v, i, j, .4f);
        float lichen = f.Fbm(x * 2, y * 2, .3f, 2, seed + 1);
        if (lichen > .4f && n.Y > -.3f) col = Kit.Mix(col, Col.Hex("9aa882"), .45f, i, j, 3);
        if (f.Ridge(x, y, .7f, 2, seed + 2) > .93f) col = Col.Lerp(col, Col.Black, .3f);
        int up = p.Depth(i, j, 0, 1, 3);
        if (up < 3) col = Col.Lerp(col, Col.Hex("cac6b6"), up == 0 ? .35f : .12f);
        return Kit.Grain(col, i, j, seed + 3, .05f, .15f);
    }

    PaintedLayer Sky()
    {
        var s = new LayerSpec("sky", .95f, 48, 30, -11, 21, -97) { Under = SkyStops[0].c, Over = SkyStops[^1].c };
        var c = s.NewCanvas(); var f = c.F;
        c.PaintAll((x, y, i, j) => Kit.Gradient(SkyStops, y, 5, 1, i, j));
        // Dawn glow spreading from behind the sacred mountain, and a pale veiled sun.
        Kit.Glow(c, 2, 1.5f, 11, Col.Hex("f4dcae"), .3f, .55f);
        Kit.Glow(c, 0, 5.6f, 3.4f, Col.Hex("f8e8c8"), .4f);
        c.Paint(-1, 1, 4.6f, 6.6f, (x, y, i, j) =>
        {
            float dx = x, dy = y - 5.6f, d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > .8f) return Col.Clear;
            if (d > .7f) return Dither.Hit(1 - (d - .7f) / .1f, i, j) ? Col.Hex("f6e4bc") : Col.Clear;
            return Col.Hex("fbf2dc");
        });
        // Two faint ranges beyond the far plane's mountains, each dissolving into the valley mist.
        Col SkyFog(float y) => Kit.Smooth(SkyStops, y);
        var farB = DangunKit.CrestTable(c, 4.3f, new[] { (11.5f, 1.5f, 3.6f), (21f, 1.1f, 3f), (27.5f, .7f, 2.2f) }, .45f, 21);
        DangunKit.Range(c, farB, RidgeSky.Shift(.06f), -11, SkyFog, .45f, 3, .06f, 23, Dawn, .15f, .07f, .05f);
        var farA = DangunKit.CrestTable(c, 3.4f, new[] { (6f, 1.4f, 3f), (16.5f, 2f, 3.4f), (24.5f, 1f, 2.6f) }, .5f, 25);
        DangunKit.Range(c, farA, RidgeSky, -11, SkyFog, .36f, 3, .07f, 27, Dawn, .15f, .08f, .06f);
        // Cloud strata in the valleys and streaks of mist.
        Kit.CloudBank(c, 0, 30, -5.5f, 2.6f, .8f, 1.9f, 30, CloudRamp.Fogged(SkyFog(-2), .5f), 29, .12f);
        DangunKit.MistBands(c, -3, 4.5f, .4f, Col.Hex("dde3e6"), 31);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Far()
    {
        var s = new LayerSpec("far", .85f, 64, 32, -11, 21, -96);
        var c = s.NewCanvas(); var f = c.F;

        // Ridge A: the sacred summit, twin peaks around the crater lake, pale with first light.
        var crestA = DangunKit.CrestTable(c, 2.4f, new[] { (3.4f, 4f, 2.4f), (5.4f, 3.6f, 2f), (13.5f, 1.2f, 2.6f), (23f, 2.4f, 3.2f), (29f, 1.3f, 2.4f) }, .5f, 41);
        var a = c.Blank();
        DangunKit.Range(a, crestA, RidgeA, -11, FogCol, .28f, 4.5f, .13f, 43, Dawn, .3f, .11f, .08f);
        a.Post((i, j, col) =>
        {
            float y = a.Y(j), x = a.X(i);
            float t = M.Smooth(4.6f, 6.2f, y) * (.3f + f.N(x, y, .6f, 45) * .3f);
            return t > 0 ? Kit.Mix(col, Col.Hex("e4e0d8"), t, i, j, 4) : col;
        });
        // The crater lake glimmers in the notch; Sinsi, Hwanung's city, stands on the saddle as a pavilion and small roofs.
        int iA = a.I(3.6f), iB = a.I(5.2f), iMin = iA;
        for (int i = iA; i <= iB; i++) if (crestA[i] < crestA[iMin]) iMin = i;
        float notchX = a.X(iMin), notchY = crestA[iMin];
        a.Paint(notchX - .8f, notchX + .8f, notchY - .18f, notchY + .02f, (x, y, i, j) => crestA[i] - y < .14f && crestA[i] - y > .025f ? Col.Hex("c4e0e8") : Col.Clear);
        void Roof(float bx, float w, float h, int tiers)
        {
            float by = crestA[M.Mod(a.I(bx), a.W)] - .04f;
            a.Paint(bx - w * .7f, bx + w * .7f, by, by + h + .1f, (x, y, i, j) => DangunKit.Pavilion(x - bx, y - by, w, h, tiers) ? Col.Lerp(Col.Hex("3a4854"), FogCol(y), .3f) : Col.Clear);
        }
        Roof(notchX - .05f, 1.35f, 1.25f, 2); Roof(notchX - 1.15f, .62f, .5f, 1); Roof(notchX + .95f, .7f, .55f, 1); Roof(notchX + 1.5f, .5f, .4f, 1);

        // Ridge B: grey-green, with far pines along its crest.
        var crestB = DangunKit.CrestTable(c, .9f, new[] { (9.6f, 2.3f, 2.4f), (16.8f, 1.5f, 2f), (26.5f, 2.7f, 2.6f), (.8f, 1.1f, 1.8f) }, .5f, 51);
        var b = c.Blank();
        DangunKit.Range(b, crestB, RidgeB, -11, FogCol, .3f, 4, .11f, 53, Dawn, .18f, .13f, .1f);
        var rngB = new Rng(55);
        for (int k = 0; k < 14; k++)
        {
            float px = k * 32 / 14f + rngB.R(-.8f, .8f), h = rngB.R(.5f, .9f);
            float by = crestB[M.Mod(b.I(px), b.W)] - .05f;
            var ramp = FarPine.Fogged(FogCol(by), .45f);
            DangunKit.Pine(b, b, px, by, h, rngB.R(-.15f, .15f), ramp, 57 + k, 0, ramp[0]);
        }

        // Ridge C: the nearest, pine-green, crowned with red pines.
        var crestC = DangunKit.CrestTable(c, -1.4f, new[] { (14f, 2f, 2.2f), (.5f, 2.4f, 2.6f), (26f, 1.3f, 2.2f), (7f, .9f, 1.6f), (20f, .7f, 1.4f) }, .5f, 61);
        var cc = c.Blank();
        DangunKit.Range(cc, crestC, RidgeC, -11, FogCol, .22f, 3.5f, .12f, 63, Dawn, .15f, .13f, .1f);
        var rngC = new Rng(65);
        for (int k = 0; k < 22; k++)
        {
            float px = k * 32 / 22f + rngC.R(-.7f, .7f), h = rngC.R(.7f, 1.4f);
            float by = crestC[M.Mod(cc.I(px), cc.W)] - .08f;
            var ramp = FarPine.Fogged(FogCol(by), .25f);
            DangunKit.Pine(cc, cc, px, by, h, rngC.R(-.15f, .15f), ramp, 67 + k, 0, Col.Lerp(ramp[0], Col.Black, .2f));
        }

        c.Over(a);
        Kit.Glow(c, notchX, notchY + .3f, 2.1f, Col.Hex("e4f4f8"), .6f, .55f);
        Kit.CloudBank(c, 0, 32, .1f, 1.3f, .45f, 1f, 44, SeaRamp.Fogged(FogCol(1), .5f), 71, .06f);
        c.Over(b);
        Kit.CloudBank(c, 0, 32, -2.8f, 1.6f, .55f, 1.3f, 42, SeaRamp.Fogged(Mist, .3f), 73, .08f);
        c.Over(cc);
        Kit.CloudBank(c, 0, 32, -4.6f, 1.4f, .5f, 1.2f, 44, SeaRamp.Fogged(Mist, .2f), 75, .1f);
        DangunKit.MistBands(c, -3.5f, 2.5f, .35f, Col.Hex("d4dde2"), 77);
        // A loose skein of birds crossing the dawn beside the sun.
        foreach (var (bx, by, sz) in new[] { (.6f, 4.9f, .13f), (1.05f, 4.75f, .12f), (1.5f, 4.65f, .13f), (1.95f, 4.5f, .11f), (1.3f, 5.1f, .1f), (2.4f, 4.35f, .12f), (.1f, 4.8f, .1f) })
            DangunKit.Bird(c, bx, by, sz, Col.Hex("4a5862"));
        Kit.Shafts(c, 22, 3.2f, .3f, Dawn, .14f, -3, 8, 79);
        return new PaintedLayer(s, c);
    }

    PaintedLayer Mid()
    {
        var s = new LayerSpec("mid", .65f, 96, 36, -9.5f, 22, -95);
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(201);
        float BankTop(float x) => -2.98f + f.Fbm(x, 0, 2.2f, 3, 203) * .14f;

        // Earth bank along the bottom, mostly under the ground line, with grass tufts at its lip.
        c.Paint(0, c.Period, -9.6f, -2.5f, (x, y, i, j) =>
        {
            float top = BankTop(x);
            if (y > top) return Col.Clear;
            if (top - y < .06f) return Grass.At(.6f + f.N(x, 0, .1f, 204) * .4f, i, j, .4f);
            return Earth.At(.5f - M.InvLerp(top, top - 2, y) * .4f + f.Fbm(x, y, .4f, 3, 205) * .15f, i, j, .4f);
        });
        c.Paint(0, c.Period, -3.1f, -2.5f, (x, y, i, j) =>
        {
            float top = BankTop(x), sx = f.Snap(.07f); int k = (int)MathF.Floor(x / sx), n = (int)MathF.Round(c.Period / sx);
            float h = Field.Hash01(M.Mod(k, n), 1, 206);
            if (h < .35f) return Col.Clear;
            float bh = .06f + h * .24f, t = (y - top) / bh;
            if (t > 1 || y < top - .05f) return Col.Clear;
            float u = MathF.Abs(M.Mod(x, sx) - sx * .5f) / (sx * .5f * (1 - t * .9f));
            return u < 1 ? Grass.At(.35f + h * .45f, i, j, .4f) : Col.Clear;
        });

        // The bear's cave: a mossy rock mass with an arched mouth, embers glowing deep inside.
        var rock = c.Blank();
        float CX = 31.6f;
        rock.Paint(CX - 4.4f, CX + 4.4f, -9.6f, 1.6f, (x, y, i, j) =>
        {
            float dx = (x - CX) / 3.7f;
            float top = -3.4f + 4.2f * MathF.Pow(MathF.Max(0, 1 - dx * dx), .75f) + f.Fbm(x, y, .9f, 3, 207) * .35f;
            if (y > top || MathF.Abs(dx) > 1.1f) return Col.Clear;
            if (Kit.InOpening(x - CX, y, .95f, -2.2f, -9.6f, .05f)) return Col.Clear;
            return Col.White;
        });
        Kit.Inflate(rock, 36, (i, j, src, h, n) =>
        {
            float x = rock.X(i), y = rock.Y(j);
            float strata = M.Fract((y + f.Fbm(x, y, 2, 3, 209) * .5f) / .6f) < .12f ? -.1f : 0;
            float v = .1f + Kit.Lambert(n.X, n.Y, n.Z) * .6f + strata + f.Fbm(x, y, .5f, 3, 211) * .1f;
            if (f.Ridge(x, y, 1.1f, 3, 213) > .92f) v -= .15f;
            var col = Rock.At(v, i, j, .4f);
            float moss = n.Y * .7f - n.X * .2f + f.Fbm(x, y, .7f, 3, 215) * .6f;
            if (moss > .55f) col = Kit.Mix(Col.Lerp(Moss, Col.Black, .35f), Moss, (moss - .55f) * 1.8f, i, j, 3);
            return Kit.Grain(col, i, j, 217, .05f, .15f);
        }, (i, j) => f.Fbm(rock.X(i), rock.Y(j), 1.2f, 3, 219) * 16);
        rock.Paint(CX - 1.05f, CX + 1.05f, -9.6f, -1.05f, (x, y, i, j) =>
        {
            if (!Kit.InOpening(x - CX, y, .95f, -2.2f, -9.6f, .05f)) return Col.Clear;
            float dx = (x - CX) / .9f, dy = (y + 3.1f) / 1.1f, d = MathF.Sqrt(dx * dx + dy * dy);
            var col = Kit.Mix(Col.Hex("0f0d0b"), Col.Hex("6a3618"), M.Smooth(1.35f, .15f, d), i, j, 5);
            if (!Kit.InOpening(x - CX, y, .87f, -2.2f, -9.6f, .05f)) col = Col.Hex("161411");
            return col;
        });

        // Red pines on the slope.
        var pwood = c.Blank(); var pads = c.Blank();
        foreach (var (px, h, lean, sd) in new[] { (17.9f, 5.4f, .08f, 221), (24.3f, 6.6f, -.1f, 223), (34.6f, 4.6f, .12f, 225), (5.9f, 3.9f, -.06f, 227) })
            DangunKit.Pine(pwood, pads, px, -3.3f, h, lean, Needle, sd);
        Kit.Inflate(pwood, 14, (i, j, src, h, n) =>
        {
            float x = pwood.X(i), y = pwood.Y(j);
            var cell = f.Cell(x, y, .16f, 229, 1.5f);
            float v = .12f + Kit.Lambert(n.X, n.Y, n.Z) * .58f + ((cell.Id & 255) / 255f - .5f) * .2f;
            if (cell.F2 - cell.F1 < .07f) v -= .14f;
            return Kit.Grain(PineBark.At(v, i, j, .35f), i, j, 231, .05f, .15f);
        }, (i, j) => f.Fbm(pwood.X(i), pwood.Y(j), .6f, 2, 233) * 5);

        // Sindansu, the sacred tree: a colossal zelkova with a buttressed trunk and five great limbs.
        var wood = c.Blank();
        float TX = 10.2f, TB = -4.4f;
        var trunk = new List<DangunKit.Node>();
        for (int k = 0; k <= 14; k++)
        {
            float t = k / 14f, y = TB + t * 8.6f;
            float flare = MathF.Pow(1 - t, 3.2f) * 1.1f;
            trunk.Add(new DangunKit.Node(TX + MathF.Sin(t * 2.3f + .5f) * .22f, y, .5f + (1 - t) * .5f + flare));
        }
        DangunKit.Stroke(wood, trunk, Col.White, .07f, 235);
        foreach (var (ox, len) in new[] { (-1.1f, 1.8f), (1.2f, 1.6f), (-.3f, 1.2f), (.6f, 1.4f) })
            DangunKit.Stroke(wood, DangunKit.Curve(TX + ox, -2.6f, TX + ox * 1.6f, -3.4f, TX + ox + MathF.Sign(ox) * len, -4.6f, .32f, .12f, 6), Col.White, .04f, 237);
        var limbs = new List<List<DangunKit.Node>>();
        void Limb(float ax, float ay, float cx, float cy, float bx, float by, float w0, float w1, float wobble, int seed)
        {
            var nodes = DangunKit.Curve(ax, ay, cx, cy, bx, by, w0, w1, 12, wobble, seed);
            DangunKit.Stroke(wood, nodes, Col.White, .05f, seed);
            limbs.Add(nodes);
        }
        Limb(TX - .5f, 2.3f, TX - 3.6f, 2.1f, TX - 6.8f, 4.4f, .44f, .12f, .25f, 239);
        Limb(TX - .2f, 3.7f, TX - 2.9f, 5.1f, TX - 4.8f, 8.6f, .4f, .1f, .2f, 241);
        Limb(TX + .1f, 4.1f, TX - .3f, 7f, TX + 1.3f, 9.9f, .42f, .1f, .25f, 243);
        Limb(TX + .4f, 3.5f, TX + 3f, 4.5f, TX + 5.4f, 8.3f, .4f, .1f, .2f, 245);
        Limb(TX + .6f, 2.5f, TX + 3.8f, 2.3f, TX + 7.2f, 4.1f, .42f, .12f, .25f, 247);
        var rngT = new Rng(249);
        var twigs = new List<List<DangunKit.Node>>();
        foreach (var limb in limbs)
            for (int k = 0; k < 3; k++)
            {
                float t = rngT.R(.45f, .85f);
                var n = DangunKit.At(limb, t); var n2 = DangunKit.At(limb, t + .05f);
                float dirX = n2.X - n.X, dirY = n2.Y - n.Y, L = MathF.Max(1e-4f, MathF.Sqrt(dirX * dirX + dirY * dirY)); dirX /= L; dirY /= L;
                float side = rngT.Chance(.5f) ? 1 : -1, len = rngT.R(1.2f, 2.4f);
                float ex = n.X + (dirX * .6f - dirY * side * .8f) * len, ey = n.Y + (dirY * .6f + dirX * side * .8f) * len + len * .5f;
                var tw = DangunKit.Curve(n.X, n.Y, (n.X + ex) * .5f + dirX * .3f, (n.Y + ey) * .5f - .2f, ex, ey, n.W * .5f, .035f, 8, .1f, 251 + k);
                DangunKit.Stroke(wood, tw, Col.White, .02f, 253);
                twigs.Add(tw);
            }
        Kit.Inflate(wood, 56, (i, j, src, h, n) =>
        {
            float x = wood.X(i), y = wood.Y(j);
            // Zelkova bark: fine flaking plates over long vertical fissures.
            var cell = f.Cell(x, y, .13f, 255, 2.4f);
            float plate = (cell.Id & 255) / 255f - .5f;
            float fissure = f.Ridge(x * 2.6f, y * .4f, .6f, 3, 257);
            float v = .12f + Kit.Lambert(n.X, n.Y, n.Z) * .58f + plate * .07f + (fissure - .5f) * .32f + f.Fbm(x, y, .5f, 2, 258) * .06f;
            if (cell.F2 - cell.F1 < .03f) v -= .08f;
            var col = Bark.At(v, i, j, .35f);
            float moss = n.Y * .5f - n.X * .3f + f.Fbm(x, y, .6f, 3, 259) * .7f;
            if (moss > .5f) col = Kit.Mix(Col.Lerp(Moss, Col.Black, .3f), Moss, (moss - .5f) * 1.5f, i, j, 3);
            return Kit.Grain(col, i, j, 261, .05f, .15f);
        }, (i, j) => f.Fbm(wood.X(i), wood.Y(j), 1.4f, 3, 263) * 16);
        Kit.Edge(wood, -1, 0, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("c0b090"), d == 0 ? .3f : .12f));

        // Leaf canopy: clumps at every limb and twig end, and along the outer reaches of the upper limbs.
        var canopy = c.Blank(); int lc = 0;
        for (int li = 0; li < limbs.Count; li++)
        {
            var limb = limbs[li]; var end = DangunKit.At(limb, 1);
            Kit.LeafClump(canopy, end.X, end.Y + .3f, 1.9f, 1.15f, 330, Leaf, 300 + lc++, .3f, .4f);
            if (li == 0 || li == 4) continue;
            foreach (var t in new[] { .62f, .82f })
            {
                var n = DangunKit.At(limb, t);
                Kit.LeafClump(canopy, n.X + rng.R(-.3f, .3f), n.Y + .55f, 1.3f, .8f, 200, Leaf, 300 + lc++, .28f, .4f);
            }
        }
        foreach (var tw in twigs) { var end = DangunKit.At(tw, 1); Kit.LeafClump(canopy, end.X, end.Y + .15f, 1f, .65f, 140, Leaf, 300 + lc++, .27f, .45f); }

        // Five-colour ribbons tied along the limbs.
        var ribbons = c.Blank();
        var obang = new[] { DangunKit.Cloth(ObRed), DangunKit.Cloth(ObBlue), DangunKit.Cloth(ObYellow), DangunKit.Cloth(ObWhite), DangunKit.Cloth(ObBlack) };
        int rc = 0;
        foreach (var limb in limbs)
            for (int k = 0; k < 4; k++)
            {
                float t = .2f + k * .2f + rng.R(-.05f, .05f);
                var n = DangunKit.At(limb, t);
                DangunKit.Ribbon(ribbons, n.X + rng.R(-.1f, .1f), n.Y - n.W * .6f, rng.R(1.1f, 2.4f), rng.R(.16f, .24f), obang[rc % 5], 400 + rc, .14f);
                rc++;
            }

        // Props: the straw rope around the trunk, altar offerings, jangseung, the geumjul between posts.
        var props = c.Blank();
        {
            var tn = DangunKit.At(trunk, (.5f - TB) / 8.6f);
            float hw = tn.W + .03f;
            props.Paint(tn.X - hw, tn.X + hw, .3f, .75f, (x, y, i, j) =>
            {
                float u = (x - tn.X) / hw; if (MathF.Abs(u) > 1) return Col.Clear;
                float cy = .55f - .07f * (1 - u * u), d = (y - cy) / .06f;
                if (MathF.Abs(d) > 1) return Col.Clear;
                float twist = M.Fract(x * 9 + d * .55f);
                float v = .3f + MathF.Sqrt(1 - d * d) * .35f + (twist < .3f ? -.22f : twist < .5f ? .12f : 0) - MathF.Abs(u) * .25f;
                return Straw.At(v, i, j, .4f);
            });
            foreach (var du in new[] { -.7f, -.25f, .25f, .7f })
                DangunKit.Ribbon(props, tn.X + du * hw, .5f - .07f * (1 - du * du), .55f, .15f, obang[3], 420 + (int)(du * 10), .05f);
            DangunKit.NeedlePad(props, tn.X + .5f * hw, .25f, .24f, .09f, Needle, 425);
        }
        // Altar before the trunk: slab on two squat supports, with rice cakes, a brass bowl, fruit, candles and incense.
        var stone = c.Blank();
        stone.Paint(TX - 1.6f, TX + 1.6f, -3.3f, -1.55f, (x, y, i, j) =>
        {
            float dx = x - TX;
            if (y > -1.95f) return MathF.Abs(dx) < 1.5f - MathF.Max(0, y + 1.75f) * .6f && y < -1.62f + f.N(x, 0, .3f, 265) * .04f ? Col.White : Col.Clear;
            if (y < -1.98f && (MathF.Abs(dx - .85f) < .3f || MathF.Abs(dx + .85f) < .3f)) return Col.White;
            return Col.Clear;
        });
        DangunKit.Dolmen(stone, 19.7f, -3.3f, 3.3f, 1.75f, .62f, .03f, 267);
        DangunKit.Dolmen(stone, 27.6f, -3.25f, 2.1f, 1.15f, .45f, -.04f, 269);
        foreach (var (kx, kh, sd) in new[] { (7.3f, .95f, 271), (13.6f, 1.35f, 273), (21.9f, .7f, 275), (29.6f, 1.15f, 277), (34.1f, .8f, 279), (16.2f, .55f, 281) })
            DangunKit.Cairn(stone, kx, -3.05f, kh, sd);
        Kit.Inflate(stone, 9, (i, j, src, h, n) => StoneShade(stone, f, i, j, n, 283), (i, j) => f.Fbm(stone.X(i), stone.Y(j), .35f, 2, 285) * 3);
        float altarTop = -1.64f;
        Offerings(props, f, TX, altarTop);
        DangunKit.Jangseung(props, 2.35f, -3.2f, 3.3f, true, Wood, 287);
        DangunKit.Jangseung(props, 3.35f, -3.2f, 2.9f, false, Wood.Shift(.06f), 289);
        // Geumjul: a straw rope between two posts, hung with paper strips, a mugwort bundle and a garlic braid.
        foreach (var px in new[] { 4.9f, 8.0f })
            props.Paint(px - .1f, px + .1f, -3.2f, -.52f, (x, y, i, j) =>
            {
                float u = (x - px) / .08f; if (MathF.Abs(u) > 1) return Col.Clear;
                if (y > -.62f && MathF.Abs(u) > 1 - (y + .62f) * 8) return Col.Clear;
                return Wood.At(.1f + Kit.Cylinder(u, .2f) * .7f + f.Fbm2(x, y, .05f, 1, 2, 291) * .08f, i, j, .35f);
            });
        DangunKit.Rope(props, 4.9f, -.72f, 8.0f, -.76f, .42f, .05f, Straw, 293);
        for (int k = 0; k < 6; k++)
        {
            float rx = 5.15f + k * .52f, ry = DangunKit.RopeY(4.9f, -.72f, 8.0f, -.76f, .42f, rx) - .03f;
            if (k == 2) MugwortBundle(props, rx, ry, 295); else if (k == 4) GarlicBraid(props, rx, ry, 297); else DangunKit.Ribbon(props, rx, ry, .5f, .15f, obang[3], 430 + k, .06f);
        }
        // A second geumjul guards the cave mouth.
        DangunKit.Rope(props, CX - 1.15f, -1.0f, CX + 1.15f, -1.02f, .14f, .045f, Straw, 299);
        foreach (var (k, dx) in new[] { (0, -.85f), (1, -.45f), (2, .05f), (3, .45f), (4, .85f) })
        {
            float rx = CX + dx, ry = DangunKit.RopeY(CX - 1.15f, -1.0f, CX + 1.15f, -1.02f, .14f, rx) - .03f;
            if (k == 1) GarlicBraid(props, rx, ry, 301); else if (k == 3) MugwortBundle(props, rx, ry, 303); else DangunKit.Ribbon(props, rx, ry, .42f, .13f, obang[3], 440 + k, .05f);
        }

        Kit.Fog(rock, FogMid, y => .16f + M.Smooth(-3, -9.5f, y) * .4f);
        Kit.Fog(pwood, FogMid, y => .16f); Kit.Fog(pads, FogMid, y => .2f);
        Kit.Fog(wood, FogMid, y => .14f + M.Smooth(-3, -9.5f, y) * .3f);
        Kit.Fog(canopy, FogMid, y => .07f + M.Smooth(6.5f, 2, y) * .1f);
        Kit.Fog(stone, FogMid, y => .1f); Kit.Fog(props, FogMid, y => .08f); Kit.Fog(ribbons, FogMid, y => .06f);
        c.Over(rock);
        Kit.Glow(c, CX, -2.7f, 1.3f, Ember, .5f, .7f);
        c.Over(pwood); c.Over(pads);
        c.Over(wood);
        c.Over(canopy);
        c.Over(ribbons);
        c.Over(stone);
        c.Over(props);
        Kit.Shafts(c, 22, 3.6f, .26f, Dawn, .12f, -9, 12, 305, (x, y) => M.Smooth(-3, 4, y) * .7f + .3f);
        foreach (var cx in new[] { TX - 1.2f, TX + 1.2f }) Kit.Glow(c, cx, altarTop + .36f, .5f, Col.Hex("ffd890"), .7f, 1, 3);
        Kit.Glow(c, TX, altarTop + .2f, 1.6f, Col.Hex("ffd0a0"), .14f, .5f);
        return new PaintedLayer(s, c);
    }

    /// <summary>Rice-cake stack, brass bowl of rice, persimmons and pears, two candles and a smoking incense bowl on the altar slab.</summary>
    static void Offerings(Canvas p, Field f, float ax, float top)
    {
        Col rice = Col.Hex("ece6d8"), riceShade = Col.Hex("c4bcaa"), brass = Col.Hex("c8a040"), brassDark = Col.Hex("7a5c20");
        // Rice cakes.
        p.Paint(ax - 1.1f, ax - .5f, top, top + .3f, (x, y, i, j) =>
        {
            float dx = MathF.Abs(x - (ax - .8f)), k = MathF.Floor((y - top) / .09f);
            if (k > 2 || dx > .26f - k * .02f) return Col.Clear;
            float ly = (y - top) / .09f - k;
            return ly < .2f || dx > .22f - k * .02f ? riceShade : (ly > .82f && Dither.Hit(.5f, i, j) ? Col.White : rice);
        });
        // Brass bowl with a mound of rice.
        p.Paint(ax - .45f, ax + .05f, top, top + .34f, (x, y, i, j) =>
        {
            float dx = (x - (ax - .2f)) / .2f, dy = y - top;
            if (dy < .18f) { float half = .55f + dy / .18f * .45f; if (MathF.Abs(dx) > half) return Col.Clear; return Kit.Mix(brassDark, brass, Kit.Cylinder(dx / half, .2f) + (dy > .15f ? .3f : 0), i, j); }
            float mound = .18f + .14f * MathF.Max(0, 1 - dx * dx * 1.1f);
            if (dy > mound || MathF.Abs(dx) > 1) return Col.Clear;
            return dx < -.2f || Dither.Hit(.4f, i, j) ? rice : riceShade;
        });
        // Fruit: persimmons and pears.
        foreach (var (fx, fy, r, col, hi) in new[] { (ax + .25f, top + .11f, .11f, "c84a2a", "f08a5a"), (ax + .44f, top + .1f, .1f, "d05030", "f09060"), (ax + .34f, top + .27f, .1f, "c8422a", "f08858"), (ax + .64f, top + .12f, .12f, "d8b848", "f8e08a"), (ax + .84f, top + .1f, .1f, "c8a838", "f0d880") })
        {
            var cc = Col.Hex(col); var hc = Col.Hex(hi);
            p.Paint(fx - r, fx + r, fy - r, fy + r * 1.15f, (x, y, i, j) =>
            {
                float dx = (x - fx) / r, dy = (y - fy) / r, d = dx * dx + dy * dy;
                if (d < 1) { float l = -(dx + .3f) * .4f + (dy + .3f) * .5f; return Kit.Mix(Col.Lerp(cc, Col.Black, .35f), hc, .35f + l, i, j, 4); }
                return MathF.Abs(dx) < .12f && dy > .9f && dy < 1.15f ? Col.Hex("3a3020") : Col.Clear;
            });
        }
        // Candles with flames.
        foreach (var cx in new[] { ax - 1.2f, ax + 1.2f })
        {
            p.Paint(cx - .06f, cx + .06f, top, top + .3f, (x, y, i, j) => MathF.Abs(x - cx) < .028f ? (x < cx ? Col.Hex("efe8d8") : Col.Hex("cfc6b0")) : Col.Clear);
            p.Paint(cx - .05f, cx + .05f, top + .3f, top + .42f, (x, y, i, j) =>
            {
                float t = (y - top - .3f) / .12f, hw = .035f * MathF.Sin(MathF.PI * MathF.Min(1, t * 1.2f)) + .004f;
                if (MathF.Abs(x - cx) > hw) return Col.Clear;
                return MathF.Abs(x - cx) < hw * .45f && t < .6f ? Col.Hex("fff4c8") : Col.Hex("f0a040");
            });
        }
        // Incense bowl and a wavering thread of smoke.
        float ix = ax + 1.0f;
        p.Paint(ix - .14f, ix + .14f, top, top + .12f, (x, y, i, j) => MathF.Abs(x - ix) < .12f - (y - top) * .2f ? Kit.Mix(Col.Hex("4a4a44"), Col.Hex("8a8a80"), (ix - x) / .24f + .5f, i, j) : Col.Clear);
        p.Paint(ix - .5f, ix + .5f, top + .1f, top + 3.4f, (x, y, i, j) =>
        {
            float t = (y - top - .1f) / 3.3f, cx = ix + MathF.Sin(t * 9 + 1) * .12f * t + f.N(0, y, .5f, 307) * .2f * t;
            float w = .012f + t * .09f, d = MathF.Abs(x - cx) / w;
            if (d > 1) return Col.Clear;
            float a = (1 - t * .85f) * (1 - d * .7f) * .5f;
            float q = MathF.Floor(a * 4 + Dither.Bayer(i, j)) / 4;
            return q <= 0 ? Col.Clear : Col.Hex("dfe2e4").WithA(q);
        });
    }

    /// <summary>A bundle of mugwort tied at the stem and hanging leaves-down.</summary>
    static void MugwortBundle(Canvas p, float x, float y, int seed)
    {
        Kit.LeafClump(p, x, y - .26f, .17f, .27f, 40, Mugwort, seed, .2f, .95f);
        p.Paint(x - .06f, x + .06f, y - .06f, y, (px, py, i, j) => MathF.Abs(px - x) < .05f ? Straw.At(.6f + (py - y) * 4, i, j, .3f) : Col.Clear);
    }

    /// <summary>A braid of garlic bulbs hanging from a straw plait.</summary>
    static void GarlicBraid(Canvas p, float x, float y, int seed)
    {
        var rng = new Rng(seed);
        p.Paint(x - .04f, x + .04f, y - .14f, y, (px, py, i, j) => MathF.Abs(px - x) < .025f ? Straw.At(.55f + (M.Fract(py * 20) < .5f ? .2f : 0), i, j, .3f) : Col.Clear);
        for (int k = 0; k < 5; k++)
        {
            float bx = x + (k % 2 == 0 ? -.045f : .045f) + rng.R(-.015f, .015f), by = y - .17f - k * .085f, rx = .07f, ry = .058f;
            p.Paint(bx - rx, bx + rx, by - ry, by + ry * 1.2f, (px, py, i, j) =>
            {
                float dx = (px - bx) / rx, dy = (py - by) / ry, d = dx * dx + dy * dy;
                if (d > 1) return MathF.Abs(dx) < .15f && dy > .9f && dy < 1.2f ? Col.Hex("b8a878") : Col.Clear;
                float l = -(dx + .2f) * .35f + (dy + .2f) * .3f + (MathF.Abs(MathF.Sin(dx * 5.5f)) < .25f ? -.12f : 0);
                return Kit.Mix(Col.Hex("b4a4b8"), Col.Hex("f2ece4"), .45f + l, i, j, 4);
            });
        }
    }

    PaintedLayer Near()
    {
        var s = new LayerSpec("near", .40f, 96, 40, 2, 12.5f, -94) { Over = Col.Hex("121917") };
        var c = s.NewCanvas(); var f = c.F; var rng = new Rng(401);
        var darkBark = new Ramp("0f110e", "171a14", "20241b", "2a2f23", "363c2d", "444b38", "565d47");
        var darkLeaf = new Ramp("0e1410", "151c15", "1c261b", "243122", "2e3d2a", "3a4b34", "485a3e", "5a6c4a");
        // Boughs of the sacred tree crossing the top of the screen.
        var wood = c.Blank();
        var boughs = new List<List<DangunKit.Node>>();
        void Bough(float ax, float ay, float cx, float cy, float bx, float by, float w0, float w1, int seed)
        {
            var n = DangunKit.Curve(ax, ay, cx, cy, bx, by, w0, w1, 14, .35f, seed);
            DangunKit.Stroke(wood, n, Col.White, .05f, seed);
            boughs.Add(n);
        }
        Bough(-3, 14.8f, 5, 11.4f, 14, 10.2f, 1.0f, .3f, 403);
        Bough(14, 10.2f, 19, 10.3f, 24.5f, 11.6f, .3f, .1f, 405);
        Bough(27, 14.8f, 31, 11.6f, 39, 10f, .85f, .22f, 407);
        Bough(39, 10f, 42, 9.9f, 45, 11.4f, .22f, .08f, 409);
        Bough(10.5f, 14.8f, 11, 12.8f, 8.2f, 10.6f, .5f, .14f, 411);
        Bough(22, 14.8f, 21, 13, 17.5f, 11.3f, .4f, .12f, 413);
        for (int k = 0; k < 16; k++)
        {
            var b = boughs[k % boughs.Count]; float t = rng.R(.3f, .95f); var n = DangunKit.At(b, t);
            float side = rng.Chance(.5f) ? 1 : -1, len = rng.R(.8f, 2);
            var tw = DangunKit.Curve(n.X, n.Y, n.X + side * len * .5f, n.Y - len * .5f, n.X + side * len, n.Y - len * .45f, n.W * .5f, .04f, 8, .08f, 420 + k);
            DangunKit.Stroke(wood, tw, Col.White, .02f, 440 + k);
        }
        Kit.Inflate(wood, 44, (i, j, src, h, n) =>
        {
            float x = wood.X(i), y = wood.Y(j);
            float fissure = f.Ridge(x * 2.2f, y * .5f, .6f, 3, 461);
            float v = .1f + Kit.Lambert(n.X, n.Y, n.Z) * .5f + (fissure - .5f) * .25f;
            return Kit.Grain(darkBark.At(v, i, j, .35f), i, j, 463, .05f, .15f);
        }, (i, j) => f.Fbm(wood.X(i), wood.Y(j), 1, 3, 465) * 12);
        Kit.Edge(wood, 0, 1, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("8a9484"), d == 0 ? .35f : .15f));

        // Leaf masses along the top and sprays reaching lower from the boughs.
        var leaves = c.Blank();
        for (int k = 0; k < 30; k++)
        {
            float x = k * 40 / 30f + rng.R(-.6f, .6f);
            Kit.LeafClump(leaves, x, rng.R(10.3f, 12.6f), rng.R(1.5f, 2.5f), rng.R(1, 1.5f), 360, darkLeaf, 600 + k, .48f, .55f);
        }
        leaves.Paint(0, c.Period, 12.7f, c.Top, (x, y, i, j) => darkLeaf[0]);
        for (int k = 0; k < 10; k++) { float x = rng.R(0, 40); Kit.LeafClump(leaves, x, rng.R(8.7f, 9.8f), rng.R(.9f, 1.4f), rng.R(.7f, 1.1f), 160, darkLeaf, 700 + k, .45f, .7f); }
        Kit.Edge(leaves, 0, 1, 2, (col, d, i, j) => Col.Lerp(col, Col.Hex("7e8c74"), d == 0 ? .35f : .15f));

        // Ribbons and a rope with paper strips hanging from the boughs.
        var ribbons = c.Blank();
        var obang = new[] { DangunKit.Cloth(Col.Lerp(ObRed, Col.Black, .3f)), DangunKit.Cloth(Col.Lerp(ObBlue, Col.Black, .3f)), DangunKit.Cloth(Col.Lerp(ObYellow, Col.Black, .3f)), DangunKit.Cloth(Col.Lerp(ObWhite, Col.Black, .3f)), DangunKit.Cloth(ObBlack) };
        for (int k = 0; k < 18; k++)
        {
            var b = boughs[k % boughs.Count]; float t = rng.R(.15f, .95f); var n = DangunKit.At(b, t);
            DangunKit.Ribbon(ribbons, n.X + rng.R(-.15f, .15f), n.Y - n.W * .5f, rng.R(1.4f, 3.2f), rng.R(.22f, .32f), obang[k % 5], 800 + k, .2f);
        }
        DangunKit.Rope(ribbons, 14.3f, 10.15f, 19.6f, 10.25f, .55f, .07f, Straw.Shift(-.3f), 831);
        for (int k = 0; k < 6; k++)
        {
            float rx = 14.7f + k * .85f, ry = DangunKit.RopeY(14.3f, 10.15f, 19.6f, 10.25f, .55f, rx) - .04f;
            DangunKit.Ribbon(ribbons, rx, ry, .75f, .22f, obang[3], 840 + k, .08f);
        }
        // A pine bough leaning in from the right.
        var pine = c.Blank();
        var pb = DangunKit.Curve(40.5f, 13.5f, 36, 11.4f, 31.5f, 11.3f, .32f, .08f, 10, .2f, 851);
        DangunKit.Stroke(pine, pb, darkBark[2], .03f, 853);
        foreach (var (t, rx, ry) in new[] { (1f, 1.3f, .4f), (.72f, 1.1f, .34f), (.45f, .9f, .3f) })
        {
            var n = DangunKit.At(pb, t);
            DangunKit.NeedlePad(pine, n.X, n.Y + ry * .3f, rx, ry, darkLeaf, 860 + (int)(t * 10), .05f);
        }
        c.Over(wood);
        c.Over(leaves);
        c.Over(pine);
        c.Over(ribbons);
        return new PaintedLayer(s, c);
    }
}
