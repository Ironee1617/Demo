using System;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R63: painted reef life.
    //  * Corals keep their generators (and random streams), then PaintCoral gives each colony a two-tone tropical
    //    colourway (base to tip, or centre to rim for the plates), keeping the generator's own shading, and writes
    //    the pattern code into the vertex alpha. CoralPaint.cginc (Living Reef and Lobby Diorama) draws the pattern:
    //    polyps, ridges and ribs, brain grooves, bands and pores. Three new growth forms for the twin tanks:
    //    table coral, organ pipes and bubble coral.
    //  * SeaStar models a plump cartoon sea star as one closed, smooth surface: a polar mesh round a star outline
    //    with a domed centre, rounded arms that taper and lift at the tips, a flat pale underside; four species
    //    (classic five-arm, cushion, brittle star with thin wavy arms, seven-arm sunflower) and six colourways.
    //    Its uv is (centre 0 to tip 1, across the arm in 1/8 radius steps) so the shader paints tubercle dots, the centre ring,
    //    bands and the cushion's ridge lines. Everything rides in vertex colours and uv: one shared material.
    public static partial class AquariumEnvironment
    {
        // ---------------------------------------------------------------- corals
        private static readonly Color[,] ReefPairs =
        {
            { C(0x7a3fc4), C(0xff7fc0) }, { C(0xff6a2a), C(0xffd34a) }, { C(0x14a89a), C(0xb8ea4a) }, { C(0xd23c8c), C(0xffb0e0) },
            { C(0xd9352e), C(0xff9a45) }, { C(0x2f6fd8), C(0x6fe6f0) }, { C(0xff5f63), C(0xffe08a) }, { C(0x5fb02a), C(0xf2f25a) },
            { C(0x9a5ae0), C(0x7ff0d8) }, { C(0xf09a20), C(0xfff0b0) },
        };
        public const int ReefColourways = 10, CoralKinds = 9;
        // Pattern code per growth form (see CoralPaint.cginc).
        private static readonly int[] CoralCodes = { 1, 2, 3, 4, 5, 6, 12, 6, 10 };
        private static void PaintedReef(Material m)
        {
            if (m == null) return;
            m.SetFloat("_Pattern", 1); m.SetFloat("_Fluoro", .55f);
        }
        private static float Luma(Color c) { return c.r * .3f + c.g * .59f + c.b * .11f; }
        // Vertices [first, g.Count) are one colony grown at 'at' from 'tint'.
        private static void PaintCoral(Geo g, int first, Vector3 at, int kind, int pick, Color tint)
        {
            int end = g.Count; if (end <= first) return;
            int code = CoralCodes[Mathf.Clamp(kind, 0, CoralCodes.Length - 1)];
            int i0 = Math.Abs(pick) % ReefColourways;
            Color low = ReefPairs[i0, 0], high = ReefPairs[i0, 1];
            float top = at.y + .01f, reach = .01f;
            for (int i = first; i < end; i++)
            {
                var p = g.P(i); top = Mathf.Max(top, p.y);
                reach = Mathf.Max(reach, new Vector2(p.x - at.x, p.z - at.z).magnitude);
            }
            bool radial = kind == 1 || kind == 6;
            float lt = Mathf.Max(.05f, Luma(tint)), alpha = (code + .5f) / 32f;
            var pale = Color.Lerp(high, Color.white, .45f);
            for (int i = first; i < end; i++)
            {
                var p = g.P(i); var c = g.Col(i);
                float f = radial ? new Vector2(p.x - at.x, p.z - at.z).magnitude / reach : (p.y - at.y) / (top - at.y);
                f = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.1f, 1, f));
                float k = Luma(c) / lt;
                var n = Color.Lerp(low, high, f) * Mathf.Clamp(k, .35f, 1.05f);
                n = Color.Lerp(n, pale, Mathf.Clamp01((k - 1) * 1.8f));
                n.a = alpha; g.SetCol(i, n);
            }
        }
        private static void CoralForm(Geo g, Vector3 at, float scale, int kind, int seed, Color tint, System.Random r)
        {
            switch (kind)
            {
                case 0: Staghorn(g, at, Vector3.up * scale * .95f, scale * .075f, 3, tint, r); break;
                case 1: ScrollCoral(g, at, scale * 1.18f, tint, seed); break;
                case 2: SeaFan(g, at, scale, tint, seed); break;
                case 3: BrainCoral(g, at, scale, tint, seed); break;
                case 4: FingerCoral(g, at, scale, tint, seed); break;
                case 5: TubeSponge(g, at, scale, tint, seed); break;
                case 6: TableCoral(g, at, scale, tint, seed); break;
                case 7: OrganPipes(g, at, scale, tint, seed); break;
                default: BubbleCoral(g, at, scale, tint, seed); break;
            }
        }
        // A short trunk under a wide, gently cupped plate with a wavy margin (two faces joined at the rim).
        private static void TableCoral(Geo g, Vector3 at, float scale, Color tint, int seed)
        {
            var top = at + new Vector3(Mathf.Sin(seed) * .04f, .52f, Mathf.Cos(seed) * .04f) * scale;
            Tendril(g, Curve(at, at + Vector3.up * scale * .26f, top, 6), scale * .14f, scale * .085f, tint * .62f, tint, 0, 0, 0, 10);
            const int sides = 56, rows = 8; int first = g.Count;
            for (int face = 0; face < 2; face++) for (int ring = 0; ring <= rows; ring++) for (int s = 0; s < sides; s++)
            {
                float t = ring / (float)rows, a = s * Mathf.PI * 2 / sides;
                float rr = scale * (.08f + 1.0f * t) * (1 + .07f * Mathf.Sin(a * 5 + seed) * t + .03f * Mathf.Cos(a * 9 - seed) * t);
                float y = scale * (.12f * t * t + .035f * Mathf.Sin(a * 4 + seed) * t * t) - (face == 1 ? scale * .045f * (1 - t * .7f) : 0);
                var p = top + new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
                g.V(p, Color.Lerp(tint * .7f, Color.Lerp(tint, C(0xeee3cc), .35f), t) * (face == 1 ? .8f : 1), new Vector2(s / (float)sides, t), Vector2.zero);
                if (ring < rows)
                {
                    int v = first + face * (rows + 1) * sides + ring * sides + s, next = first + face * (rows + 1) * sides + ring * sides + (s + 1) % sides;
                    if (face == 0) g.Q(v, v + sides, next + sides, next); else g.Q(v, next, next + sides, v + sides);
                }
            }
            int bottom = first + (rows + 1) * sides, outer = rows * sides;
            for (int s = 0; s < sides; s++)
            {
                int next = (s + 1) % sides;
                g.Q(first + outer + s, bottom + outer + s, bottom + outer + next, first + outer + next);
                g.Q(first + s, first + next, bottom + next, bottom + s);
            }
        }
        // A tight bundle of slim open tubes of many heights (organ-pipe coral).
        private static void OrganPipes(Geo g, Vector3 at, float scale, Color tint, int seed)
        {
            for (int n = 0; n < 14; n++)
            {
                float a = n * 2.399f + seed, f = Mathf.Sqrt(n / 14f), spread = scale * .34f * f;
                float h = scale * (.5f + .65f * (1 - f) + .18f * Mathf.Sin(n * 3.3f + seed));
                float radius = scale * (.055f + .018f * (n % 3));
                Pipe(g, at + new Vector3(Mathf.Cos(a) * spread, 0, Mathf.Sin(a) * spread), h, radius, a, tint);
            }
        }
        private static void Pipe(Geo g, Vector3 root, float h, float r, float a, Color tint)
        {
            const int sides = 12, rings = 6; int first = g.Count;
            for (int wall = 0; wall < 2; wall++) for (int row = 0; row <= rings; row++) for (int s = 0; s < sides; s++)
            {
                float t = row / (float)rings, angle = s * Mathf.PI * 2 / sides;
                var centre = root + new Vector3(Mathf.Cos(a) * t * t * .04f, h * t, Mathf.Sin(a) * t * t * .04f);
                float radius = r * (.85f + t * .3f) * (wall == 0 ? 1 : .62f);
                g.V(centre + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius), tint * (wall == 0 ? (.72f + t * .33f) : (.24f + t * .3f)), new Vector2(s / (float)sides, t));
                if (row < rings)
                {
                    int v = first + wall * (rings + 1) * sides + row * sides + s, next = first + wall * (rings + 1) * sides + row * sides + (s + 1) % sides;
                    if (wall == 0) g.Q(v, next, next + sides, v + sides); else g.Q(v, v + sides, next + sides, next);
                }
            }
            int inner = first + (rings + 1) * sides, outerTop = first + rings * sides, innerTop = inner + rings * sides;
            int baseCentre = g.Count; g.V(root, tint * .35f);
            for (int s = 0; s < sides; s++)
            {
                int next = (s + 1) % sides; g.Q(outerTop + s, outerTop + next, innerTop + next, innerTop + s);
                g.Q(first + s, inner + s, inner + next, first + next); g.T(baseCentre, inner + s, inner + next);
            }
        }
        // A low mound of plump, glowing bubbles.
        private static void BubbleCoral(Geo g, Vector3 at, float scale, Color tint, int seed)
        {
            g.Ellipsoid(at + Vector3.up * scale * .07f, new Vector3(scale * .55f, scale * .15f, scale * .5f), tint * .6f, 16, 8, .05f);
            for (int n = 0; n < 13; n++)
            {
                float a = n * 2.399f + seed, f = Mathf.Sqrt((n + .5f) / 13f), rr = scale * .42f * f;
                float y = scale * (.2f + .24f * (1 - f));
                float s = scale * (.16f + .06f * Mathf.Abs(Mathf.Sin(n * 2.1f + seed)));
                g.Ellipsoid(at + new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr), new Vector3(s, s * .92f, s), Color.Lerp(tint, C(0xf0e6d6), .3f), 14, 10, .03f);
            }
        }

        // ---------------------------------------------------------------- sea stars
        public const int StarSpeciesCount = 4, StarColourways = 6;
        private static readonly Color[,] StarColours =
        {
            { C(0xff7038), C(0xc8401f) }, { C(0xe23a3a), C(0x92202c) }, { C(0x8a4fd8), C(0x55309e) },
            { C(0xff86b4), C(0xd84b86) }, { C(0xf4c24a), C(0xc5782a) }, { C(0x3d7de8), C(0x2449a8) },
        };
        // Classic most often, then cushion, brittle and sunflower stars.
        internal static int StarSpecies(int hash) { int k = Math.Abs(hash) % 10; return k < 4 ? 0 : k < 6 ? 1 : k < 8 ? 2 : 3; }
        // A sea star resting on the ground at 'at' (radius: centre to arm tip). ground (optional) drapes it.
        private static void SeaStar(Geo g, Vector3 at, float radius, int species, int colourway, float angle, int seed, Func<float, float, float> ground)
        {
            int arms = species == 3 ? 7 : 5;
            float k = species == 1 ? 2.0f : species == 2 ? 9f : species == 3 ? 2.6f : 2.1f;
            float core = species == 1 ? .72f : species == 2 ? .22f : species == 3 ? .42f : .38f;
            float thick = species == 1 ? .30f : species == 2 ? .075f : species == 3 ? .15f : .21f;
            float dome = species == 1 ? .07f : species == 2 ? .11f : species == 3 ? .08f : .12f;
            float curl = species == 1 ? .02f : species == 2 ? .13f : .11f;
            float wave = species == 2 ? .32f : species == 3 ? .05f : 0;
            int cw = Math.Abs(colourway) % StarColourways;
            int code = species == 1 ? 11 : species == 2 ? 9 : cw == 4 ? 8 : 7;
            Color body = StarColours[cw, 0], tip = StarColours[cw, 1];
            Color centre = species == 1 ? Color.Lerp(body, Color.white, .18f) : Color.Lerp(body, tip, .35f);
            Color belly = Color.Lerp(body, C(0xfff0dc), .55f) * .92f;
            float alpha = (code + .5f) / 32f;
            int cols = arms * (species == 2 ? 32 : 22), rings = 9, under = 3;
            var lift = new float[arms]; var phase = new float[arms];
            for (int a = 0; a < arms; a++)
            {
                float h = Mathf.Abs(Mathf.Sin(seed * 12.9898f + a * 78.233f) * 43758.5453f) % 1f;
                lift[a] = curl * (.35f + .65f * h); phase[a] = h * 6.283f;
            }
            if (species != 1) lift[Math.Abs(seed) % arms] *= 2.1f; // one arm raised a little more
            float sector = Mathf.PI / arms;
            int first = g.Count;
            // centre vertices (top, bottom), then the top rings 1..rings (rings = the rim), then the underside rings.
            g.V(at + Vector3.up * radius * (thick + dome), centre, new Vector2(0, 0), Vector2.zero);
            g.V(at + Vector3.up * radius * .004f, belly, new Vector2(0, 99), Vector2.zero);
            for (int side = 0; side < 2; side++)
            {
                int count = side == 0 ? rings : under;
                for (int j = 1; j <= count; j++)
                {
                    float rho = side == 0 ? Mathf.Sin(j / (float)rings * Mathf.PI * .5f) : Mathf.Sin(j / (float)(under + 1) * Mathf.PI * .5f);
                    for (int c = 0; c < cols; c++)
                    {
                        float theta = c * Mathf.PI * 2 / cols;
                        float local = theta / (2 * sector); int arm = Mathf.RoundToInt(local) % arms;
                        float su = (local - Mathf.Round(local)) * 2; // -1..1 across the sector
                        float u = Mathf.Abs(su);
                        float f = Mathf.Pow(Mathf.Max(0, Mathf.Cos(u * Mathf.PI * .5f)), k);
                        float outline = core + (1 - core) * f;
                        float q = rho * outline;
                        float bend = wave * Mathf.Sin(q * Mathf.PI * 1.6f + phase[arm]) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(core + .05f, core + .4f, q));
                        float a = angle + theta + bend;
                        float flat = Mathf.Sqrt(Mathf.Max(0, 1 - rho * rho));
                        float bottomY = .015f * rho * rho;
                        float topY = bottomY + (thick * (1 - .5f * q) * flat + dome * Mathf.Exp(-(q / .28f) * (q / .28f)) * (1 - rho * rho));
                        float up = lift[arm] * Mathf.Pow(Mathf.SmoothStep(0, 1, Mathf.InverseLerp(Mathf.Max(.5f, core + .12f), 1, q)), 2);
                        float y = (side == 0 ? topY : bottomY * .6f) + up;
                        var p = at + new Vector3(Mathf.Cos(a) * q, y, Mathf.Sin(a) * q) * radius;
                        Color col;
                        if (side == 0)
                        {
                            col = Color.Lerp(centre, body, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.05f, .3f, q)));
                            col = Color.Lerp(col, tip, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.68f, 1, q)) * (species == 2 ? .4f : .75f));
                            col *= Mathf.Lerp(1, .78f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.82f, 1, rho)));
                        }
                        else col = belly;
                        col.a = alpha;
                        g.V(p, col, new Vector2(Mathf.Clamp01(q), side == 0 ? su * sector * q * 8 : 99), Vector2.zero);
                    }
                }
            }
            int topRow = first + 2, underRow = first + 2 + rings * cols;
            for (int c = 0; c < cols; c++)
            {
                int n = (c + 1) % cols;
                g.T(first, topRow + n, topRow + c);
                for (int j = 0; j < rings - 1; j++) g.Q(topRow + j * cols + c, topRow + j * cols + n, topRow + (j + 1) * cols + n, topRow + (j + 1) * cols + c);
                g.T(first + 1, underRow + c, underRow + n);
                for (int j = 0; j < under - 1; j++) g.Q(underRow + j * cols + c, underRow + (j + 1) * cols + c, underRow + (j + 1) * cols + n, underRow + j * cols + n);
                int lastUnder = underRow + (under - 1) * cols, rim = topRow + (rings - 1) * cols;
                g.Q(lastUnder + c, rim + c, rim + n, lastUnder + n);
            }
            for (int i = first; i < first + 2; i++) { var c0 = g.Col(i); c0.a = alpha; g.SetCol(i, c0); }
            if (ground != null)
            {
                float baseY = ground(at.x, at.z);
                for (int i = first; i < g.Count; i++) { var p = g.P(i); p.y += ground(p.x, p.z) - baseY; g.SetP(i, p); }
            }
        }
    }
}
