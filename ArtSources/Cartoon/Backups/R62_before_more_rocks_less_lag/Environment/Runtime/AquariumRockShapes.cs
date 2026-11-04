using System;
using System.Collections.Generic;

namespace PrototypeLab.Aquarium
{
    // R58: one rock of the twin tanks' scenery (AquariumSceneryLayout.TwinRocks). Its foot stands on the sand at
    // position (y a little under the sand, so the base is buried), turned by yaw (Unity's: +x toward -z) and
    // scaled uniformly. Shape and collision are the same signed field (AquariumRockShapes.Local).
    [Serializable]
    public struct AquariumRock
    {
        public int kind, variant;
        public Point position;
        public float yaw, scale;
        // Per-rock tint (painted variation), 0..1.
        public float tint;
    }

    // R58: eight rock silhouettes for the twin tanks, written as signed distance fields in a local frame (foot at
    // the origin, y up, about 1 m units). The same field draws the mesh (AquariumEnvironment.RockPrototype,
    // marching tetrahedra) and gives the host its collision, so what you see is what blocks you. Shapes are
    // rounded painted boulders with a few flat chipped facets:
    //   0 pillar stack, 1 table rock, 2 arch (swim-through), 3 split boulder pair, 4 pebble heap,
    //   5 overhang ledge, 6 worn round boulder, 7 jagged spire.
    // R59: six giant landmark formations written at true size (local units are metres at scale 1, 6.5 to 9 m
    // tall, 11 to 20 m long), each with swim-throughs a diver fits through:
    //   8 sea stack group (three pillars with gaps), 9 great arch, 10 mesa with a cave under its table,
    //   11 reef wall with a window and a notch, 12 cleft boulder (crevice under a wedged chockstone),
    //   13 terraced outcrop (four flat tiers, an overhang niche).
    // All arithmetic is plain float math, identical on every machine.
    public static class AquariumRockShapes
    {
        public const int Pillar = 0, Table = 1, Arch = 2, Split = 3, Pebbles = 4, Ledge = 5, Worn = 6, Spire = 7, Variants = 2;
        // R59: the giants follow the eight small shapes (SmallCount); Count covers both.
        public const int Stacks = 8, GreatArch = 9, Mesa = 10, Ridge = 11, Cleft = 12, Terrace = 13;
        public const int SmallCount = 8, FirstGiant = 8, Count = 14;
        public static bool IsGiant(int kind) { return kind >= FirstGiant; }
        public static readonly string[] Names = { "Pillar stack", "Table rock", "Arch", "Split boulder", "Pebble heap", "Overhang ledge", "Worn boulder", "Jagged spire",
            "Giant sea stacks", "Giant great arch", "Giant mesa cave", "Giant reef wall", "Giant cleft boulder", "Giant terraces" };
        // Local bounds (scale 1) of every kind, generous enough for the surface noise.
        public static readonly Point[] LocalMin =
        {
            new Point(-1.45f, -.45f, -1.35f), new Point(-2.05f, -.45f, -1.65f), new Point(-3.35f, -.45f, -1.45f), new Point(-2.25f, -.45f, -1.65f),
            new Point(-2.0f, -.45f, -2.0f), new Point(-2.1f, -.45f, -1.6f), new Point(-2.25f, -.45f, -2.05f), new Point(-1.55f, -.45f, -1.35f),
            // R59 giants (measured from the fields, plus the weathering's reach)
            new Point(-7.5f, -.95f, -5.75f), new Point(-9.8f, -.95f, -4.0f), new Point(-7.6f, -.95f, -6.1f), new Point(-10.5f, -.95f, -3.9f), new Point(-6.7f, -.95f, -5.5f), new Point(-8.7f, -.95f, -7.4f)
        };
        public static readonly Point[] LocalMax =
        {
            new Point(1.45f, 3.35f, 1.35f), new Point(2.25f, 2.25f, 1.65f), new Point(3.15f, 3.75f, 1.45f), new Point(2.25f, 2.15f, 1.65f),
            new Point(2.0f, 1.55f, 2.0f), new Point(2.4f, 3.25f, 1.6f), new Point(2.45f, 1.85f, 2.05f), new Point(2.25f, 4.75f, 1.75f),
            new Point(7.8f, 9.3f, 5.95f), new Point(9.2f, 8.8f, 3.85f), new Point(8.15f, 8.6f, 6.2f), new Point(11.55f, 7.3f, 4.45f), new Point(6.8f, 7.35f, 5.6f), new Point(8.7f, 8.1f, 7.5f)
        };
        // Horizontal footprint radius (scale 1): how much floor a rock takes.
        public static float Footprint(int kind)
        {
            var a = LocalMin[kind]; var b = LocalMax[kind];
            float x = Math.Max(-a.x, b.x), z = Math.Max(-a.z, b.z);
            return (float)Math.Sqrt(x * x + z * z) * .86f;
        }
        public static float Height(int kind) { return LocalMax[kind].y; }

        // ---------------------------------------------------------------- primitives
        private static float Ellipsoid(float x, float y, float z, float rx, float ry, float rz)
        {
            float ax = x / rx, ay = y / ry, az = z / rz, k0 = (float)Math.Sqrt(ax * ax + ay * ay + az * az);
            float bx = x / (rx * rx), by = y / (ry * ry), bz = z / (rz * rz), k1 = (float)Math.Sqrt(bx * bx + by * by + bz * bz);
            if (k1 < 1e-6f) return -Math.Min(rx, Math.Min(ry, rz));
            return k0 * (k0 - 1) / k1;
        }
        private static float RoundBox(float x, float y, float z, float bx, float by, float bz, float r)
        {
            float qx = Math.Abs(x) - bx, qy = Math.Abs(y) - by, qz = Math.Abs(z) - bz;
            float ox = Math.Max(qx, 0), oy = Math.Max(qy, 0), oz = Math.Max(qz, 0);
            return (float)Math.Sqrt(ox * ox + oy * oy + oz * oz) + Math.Min(Math.Max(qx, Math.Max(qy, qz)), 0) - r;
        }
        private static float SMin(float a, float b, float k) { float h = Math.Max(k - Math.Abs(a - b), 0) / k; return Math.Min(a, b) - h * h * k * .25f; }
        private static float SMax(float a, float b, float k) { float h = Math.Max(k - Math.Abs(a - b), 0) / k; return Math.Max(a, b) + h * h * k * .25f; }

        // Chipped facets: seven fixed directions (turned per seed) cut a rounded ellipsoid like a worn cut stone.
        private static readonly float[] FacetX = { 1, -.62f, -.55f, .35f, .2f, -.8f, .75f };
        private static readonly float[] FacetY = { .35f, .55f, -.2f, .95f, -.35f, .1f, -.1f };
        private static readonly float[] FacetZ = { .15f, .65f, -.85f, -.3f, .95f, -.4f, -.8f };
        private static readonly float[] FacetN;
        static AquariumRockShapes()
        {
            FacetN = new float[FacetX.Length];
            for (int i = 0; i < FacetX.Length; i++) FacetN[i] = 1f / (float)Math.Sqrt(FacetX[i] * FacetX[i] + FacetY[i] * FacetY[i] + FacetZ[i] * FacetZ[i]);
        }
        private static float Faceted(float x, float y, float z, float rx, float ry, float rz, int seed, float cut)
        {
            float e = Ellipsoid(x, y, z, rx, ry, rz);
            float qx = x / rx, qy = y / ry, qz = z / rz;
            float a = seed * 1.37f, c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            float tx = c * qx + s * qz, tz = -s * qx + c * qz;
            float f = -10;
            for (int i = 0; i < FacetX.Length; i++)
            {
                float level = cut + ((i * 7 + seed * 3) % 5) * .025f;
                f = Math.Max(f, (tx * FacetX[i] + qy * FacetY[i] + tz * FacetZ[i]) * FacetN[i] - level);
            }
            return SMax(e, f * Math.Min(rx, Math.Min(ry, rz)), .1f);
        }

        // ---------------------------------------------------------------- noise (as the cliffs' weathering)
        private static float Hash(int x, int y, int z)
        { unchecked { uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)z * 2246822519u; h = (h ^ (h >> 13)) * 1274126177u; return (h ^ (h >> 16)) * (1f / 4294967295f); } }
        private static float Mix(float a, float b, float t) { return a + (b - a) * t; }
        public static float Noise(float px, float py, float pz)
        {
            int x = (int)Math.Floor(px), y = (int)Math.Floor(py), z = (int)Math.Floor(pz);
            float u = px - x, v = py - y, w = pz - z; u = u * u * (3 - 2 * u); v = v * v * (3 - 2 * v); w = w * w * (3 - 2 * w);
            return Mix(Mix(Mix(Hash(x, y, z), Hash(x + 1, y, z), u), Mix(Hash(x, y + 1, z), Hash(x + 1, y + 1, z), u), v),
                Mix(Mix(Hash(x, y, z + 1), Hash(x + 1, y, z + 1), u), Mix(Hash(x, y + 1, z + 1), Hash(x + 1, y + 1, z + 1), u), v), w);
        }
        private static float Weather(float x, float y, float z, int variant, float amount)
        {
            float o = variant * 17.3f + 3.1f;
            return ((Noise(x * 1.15f + o, y * 1.15f, z * 1.15f - o) - .5f) * .2f + (Noise(x * 2.9f - o, y * 2.9f + o, z * 2.9f) - .5f) * .07f) * amount;
        }

        // ---------------------------------------------------------------- the eight shapes
        // Signed distance (metres at scale 1) of a local point to a rock of this kind and variant.
        public static float Local(int kind, int variant, Point p)
        {
            float x = p.x, y = p.y, z = p.z, d;
            int v = variant;
            switch (kind)
            {
                case Pillar:
                {
                    float a = Faceted(x, y - .45f, z, 1.08f, .78f, .98f, 1 + v, .82f);
                    float bx = x - .12f + v * .08f, by = y - 1.45f, bz = z + .05f;
                    float c1 = (float)Math.Cos(.5f + v), s1 = (float)Math.Sin(.5f + v);
                    float b = Faceted(c1 * bx + s1 * bz, by, -s1 * bx + c1 * bz, .84f, .62f, .76f, 2 + v, .84f);
                    float cx = x + .1f - v * .1f, cy = y - 2.32f, cz = z - .06f;
                    float c = Faceted(cx, cy, cz, .64f, .52f, .6f, 3 + v, .82f);
                    float cap = Ellipsoid(x - .02f, y - 2.86f, z, .34f, .27f, .32f);
                    d = SMin(SMin(SMin(a, b, .2f), c, .18f), cap, .12f);
                    d += Weather(x, y, z, v, 1);
                    break;
                }
                case Table:
                {
                    float pedestal = Faceted(x, y - .58f, z, .78f, .98f, .66f, 4 + v, .85f);
                    float qx = x - .15f, qy = y - 1.68f + qx * .07f - z * .04f;
                    float slab = RoundBox(qx, qy, z, 1.5f - .15f * v, .14f, 1.02f + .1f * v, .22f);
                    float rubble = Faceted(x - .75f, y - .15f, z - .6f, .46f, .38f, .42f, 11 + v, .82f);
                    float rubble2 = Faceted(x + .8f, y - .1f, z + .4f, .36f, .3f, .33f, 12 + v, .82f);
                    d = SMin(SMin(SMin(pedestal, slab, .32f), rubble, .16f), rubble2, .14f);
                    d += Weather(x, y, z, v, 1.1f);
                    break;
                }
                case Arch:
                {
                    float left = Faceted(x + 1.95f, y - 1.0f, z, 1.0f, 1.45f, .95f, 5 + v, .8f);
                    float right = Faceted(x - 1.9f, y - .85f, z - .05f, 1.02f, 1.3f, .98f, 6 + v, .8f);
                    float qx = x, qy = y - 1.3f;
                    float ring = (float)Math.Sqrt(qx * qx + qy * qy) - 1.9f;
                    float lz = z / .9f, lr = ring / .72f;
                    float top = ((float)Math.Sqrt(lr * lr + lz * lz) - 1) * .72f;
                    top = SMax(top, 1.15f - y, .25f);
                    float crown = Faceted(x + .35f, y - 3.05f, z + .05f, 1.0f, .42f, .78f, 8 + v, .8f);
                    float knob = Faceted(x - 1.35f, y - 2.55f, z - .1f, .62f, .55f, .7f, 9 + v, .8f);
                    float rubble = Faceted(x + 2.75f, y - .2f, z - .55f, .5f, .42f, .46f, 2 + v, .8f);
                    d = SMin(SMin(left, right, .2f), top, .45f);
                    d = SMin(SMin(SMin(d, crown, .3f), knob, .25f), rubble, .15f);
                    d += Weather(x, y, z, v, 1.1f);
                    break;
                }
                case Split:
                {
                    // One boulder cleft in two, the halves leaning apart from the foot.
                    float ta = .1f + .03f * v, ca = (float)Math.Cos(ta), sa = (float)Math.Sin(ta);
                    float ax = x + .4f, ay = y;
                    float lx = ca * ax + sa * ay, ly = -sa * ax + ca * ay;
                    float halfA = SMax(Faceted(lx + .02f, ly - .78f, z, 1.42f, 1.12f, 1.25f, 7 + v, .88f), lx - .02f, .1f);
                    float bx2 = x - .4f;
                    float rx = ca * bx2 - sa * ay, ry = sa * bx2 + ca * ay;
                    float halfB = SMax(Faceted(rx - .02f, ry - .78f, z, 1.42f, 1.12f, 1.25f, 7 + v, .88f), -rx - .02f, .1f);
                    d = Math.Min(halfA, halfB);
                    d = SMin(d, Faceted(x - 1.35f, y - .12f, z + .8f, .42f, .34f, .38f, 13 + v, .82f), .12f);
                    d += Weather(x, y, z, v, .9f);
                    break;
                }
                case Pebbles:
                {
                    d = 100;
                    for (int i = 0; i < 8; i++)
                    {
                        float h = Hash(i, v, 41), g = Hash(i, v, 77);
                        float angle = i * 2.399f + v * 1.1f, reach = i == 0 ? 0 : .45f + .95f * h;
                        float size = i == 0 ? .82f : .38f + .34f * g;
                        float sx = (float)Math.Cos(angle) * reach, sz = (float)Math.Sin(angle) * reach;
                        float stone = Faceted(x - sx, y - size * .5f, z - sz, size, size * .78f, size * (.8f + .2f * h), i + v * 3, .84f);
                        d = SMin(d, stone, .1f);
                    }
                    d += Weather(x, y, z, v, .6f);
                    break;
                }
                case Ledge:
                {
                    float back = Faceted(x + .72f, y - 1.38f, z, 1.02f, 1.62f, .96f, 9 + v, .86f);
                    float qx = x - .72f, qy = y - 2.18f + qx * .06f;
                    float shelf = RoundBox(qx, qy, z - .05f, 1.2f, .18f, .86f + .08f * v, .22f);
                    float footing = Faceted(x + .45f, y - .3f, z - .2f, 1.4f, .58f, 1.12f, 10 + v, .88f);
                    float drip = Ellipsoid(x - 1.45f, y - 1.92f, z + .3f, .32f, .3f, .3f);
                    d = SMin(SMin(SMin(back, shelf, .38f), footing, .32f), drip, .12f);
                    d += Weather(x, y, z, v, 1);
                    break;
                }
                case Worn:
                {
                    float main = Ellipsoid(x, y - .55f, z, 1.52f, 1.02f, 1.26f);
                    float lobe = Ellipsoid(x - .98f, y - .36f, z - .58f, .86f, .64f, .76f);
                    float lobe2 = Ellipsoid(x + .8f, y - .44f, z + .62f, .76f, .6f, .72f + .1f * v);
                    d = SMin(SMin(main, lobe, .45f), lobe2, .42f);
                    d += Weather(x, y, z, v, .55f);
                    break;
                }
                case Spire:
                {
                    float lean = .12f + .05f * v;
                    float qx = x - y * lean;
                    float low = Faceted(qx, y - 1.0f, z, 1.02f, 1.28f, .92f, 12 + v, .84f);
                    float mid = Faceted(qx - .04f, y - 2.55f, z, .6f, 1.35f, .55f, 13 + v, .82f);
                    float tip = Faceted(qx - .1f, y - 3.75f, z, .28f, .7f, .27f, 14 + v, .8f);
                    float sx = x - .98f - (y * .26f), sz = z - .5f;
                    float spike = Faceted(sx, y - .9f, sz, .46f, 1.02f, .43f, 15 + v, .82f);
                    d = SMin(SMin(SMin(low, mid, .3f), tip, .22f), spike, .2f);
                    d += Weather(x, y, z, v, 1.15f);
                    break;
                }
                default:
                    d = Giant(kind, v, x, y, z);
                    break;
            }
            return d;
        }

        // ---------------------------------------------------------------- R59: the giant formations
        // Weathering at landmark scale: broad swells and dents over the small grain of the boulders.
        private static float GiantWeather(float x, float y, float z, int variant)
        {
            float o = variant * 23.7f + 9.1f;
            return (Noise(x * .3f + o, y * .3f, z * .3f - o) - .5f) * .5f + (Noise(x * .85f - o, y * .85f + o, z * .85f) - .5f) * .26f + Weather(x, y, z, variant, 1.3f);
        }
        // The giants' boulders are cut a little deeper than the small rocks', so broad flat facets show.
        private static float GF(float x, float y, float z, float rx, float ry, float rz, int seed, float cut)
        {
            return Faceted(x, y, z, rx, ry, rz, seed, cut - .045f);
        }
        // A tier: a chipped boulder cut flat at top (above its centre, the frame of x, y, z).
        private static float Tier(float x, float y, float z, float rx, float ry, float rz, int seed, float top, float cut)
        {
            return SMax(GF(x, y, z, rx, ry, rz, seed, cut), y - top, .35f);
        }
        private static float Giant(int kind, int v, float x, float y, float z)
        {
            float d, weather = 1;
            switch (kind)
            {
                case Stacks:
                {
                    // R61: three rugged sea stacks (8.5, 6.2 and 4.4 m before scale): angular seven-sided columns that
                    // taper and lean, broken tops, strata ledges and vertical cracks, on one rubble apron, swim gaps between.
                    float h1 = v == 0 ? 8.5f : 8.0f;
                    float p1 = Column(x, y, z, -3.9f, 1.8f, 2.45f, 1.65f, h1, 21 + v, .05f + .02f * v);
                    float p2 = Column(x, y, z, 3.8f, 1.1f, 2.15f, 1.45f, 6.2f, 25 + v, -.03f);
                    float p3 = Column(x, y, z, 0, -3.4f - v * .3f, 1.5f, 1.0f, 4.4f, 28 + v, .04f);
                    d = Math.Min(Math.Min(p1, p2), p3) + Strata(x, y, z, v) + Cracks(x, y, z, v);
                    float apron = Ellipsoid(x, y + .35f, z - .2f, 7.0f, 1.05f, 4.9f);
                    float rubble = SMin(GF(x - 2.2f, y - .2f, z + 4.3f, .95f, .6f, .85f, 30 + v, .84f), GF(x + 6.0f, y - .25f, z + 1.6f, .9f, .65f, .8f, 31 + v, .84f), .2f);
                    d = SMin(SMin(d, apron, .9f), rubble, .35f);
                    weather = .5f;
                    break;
                }
                case GreatArch:
                {
                    // Two massive legs and a thick span: an opening about 6 m wide and 5 m high, 8 m to the crown.
                    float s = v == 0 ? 1f : -1f;
                    float lx = x + 5.4f, rx = x - 5.3f;
                    float legL = SMin(GF(lx - .3f, y - 3.0f, z, 2.7f, 3.4f, 2.8f, 31 + v, .82f), GF(lx + .1f, y - .9f, z + .2f, 3.2f, 1.5f, 3.2f, 32 + v, .85f), .7f);
                    float legR = SMin(GF(rx + .3f, y - 2.7f, z - .15f, 2.6f, 3.1f, 2.7f, 33 + v, .82f), GF(rx - .1f, y - .8f, z - .1f, 3.1f, 1.4f, 3.1f, 34 + v, .85f), .7f);
                    float qy = y - 2.3f, ring = (float)Math.Sqrt(x * x + qy * qy) - 4.2f, lr = ring / 1.8f, lz = z / 2.5f;
                    float span = ((float)Math.Sqrt(lr * lr + lz * lz) - 1) * 1.8f;
                    span = SMax(span, 2.3f - y, .6f);
                    float crown = GF(x - .9f * s, y - 7.0f, z + .1f, 2.4f, .95f, 1.9f, 35 + v, .8f);
                    float knob = GF(x + 3.2f * s, y - 6.1f, z - .2f, 1.4f, 1.1f, 1.6f, 36 + v, .8f);
                    float rubble = GF(x + 8.1f * s, y - .3f, z + 1.7f, 1.1f, .8f, 1.0f, 37 + v, .84f);
                    d = SMin(SMin(legL, legR, .3f), span, 1.1f);
                    d = SMin(SMin(SMin(d, crown, .7f), knob, .5f), rubble, .35f);
                    break;
                }
                case Mesa:
                {
                    // A broad table on two supports: a cave runs under it between them, and the slab overhangs.
                    float tilt = .05f + .02f * v;
                    float sy = y - 5.55f - x * tilt * .5f + z * .03f;
                    float slab = SMax(RoundBox(x - .2f, sy, z, 5.9f, .85f, 4.5f, .9f), GF(x - .2f, sy, z, 7.4f, 2.6f, 5.9f, 41 + v, .9f), .5f);
                    float core = GF(x + 3.7f, y - 2.5f, z + .5f, 2.6f, 2.9f, 3.2f, 42 + v, .84f);
                    float support = GF(x - 4.3f, y - 2.4f, z - .6f, 1.9f, 2.9f, 2.3f, 43 + v, .84f);
                    float tier = Tier(x + 1.6f, y - 7.2f, z - .6f, 2.8f, 1.2f, 2.2f, 44 + v, .8f, .84f);
                    float boulders = SMin(GF(x - 6.6f, y - .4f, z + 2.6f, 1.2f, .9f, 1.1f, 45 + v, .84f), GF(x + 6.2f, y - .3f, z - 3.0f, 1.0f, .75f, .9f, 46 + v, .84f), .2f);
                    float aprons = Math.Min(Ellipsoid(x + 3.7f, y + .3f, z + .5f, 3.6f, .9f, 4.0f), Ellipsoid(x - 4.3f, y + .3f, z - .6f, 2.7f, .9f, 3.0f));
                    d = SMin(SMin(core, support, .4f), slab, .9f);
                    d = SMin(SMin(SMin(d, tier, .6f), boulders, .4f), aprons, .9f);
                    break;
                }
                case Ridge:
                {
                    // A curving reef wall, 19 m long: a high block, a window under a lintel, a tall buttress,
                    // an open notch and a pinnacle at the end.
                    float wz = z - .9f * (float)Math.Sin(x * .28f + v * 1.3f);
                    float a = SMin(GF(x + 6.6f, y - 2.6f, wz, 2.6f, 3.1f, 2.1f, 51 + v, .83f), GF(x + 8.0f, y - 1.2f, wz + .3f, 1.6f, 1.6f, 1.7f, 52 + v, .84f), .5f);
                    float b = SMin(GF(x - 1.5f, y - 2.9f, wz, 2.0f, 3.5f, 2.2f, 53 + v, .83f), GF(x - 1.7f, y - 5.6f, wz - .1f, 1.3f, 1.4f, 1.4f, 54 + v, .82f), .5f);
                    float lintel = SMax(RoundBox(x + 2.25f, y - 5.2f, wz, 2.6f, .5f, 1.4f, .55f), GF(x + 2.25f, y - 5.2f, wz, 3.6f, 1.3f, 2.1f, 57 + v, .88f), .3f);
                    float c = SMin(GF(x - 8.3f, y - 2.0f, wz, 1.7f, 2.4f, 1.8f, 55 + v, .83f), GF(x - 8.5f, y - 4.3f, wz + .1f, .9f, 1.2f, .95f, 56 + v, .8f), .4f);
                    float apron = Ellipsoid(x - .3f, y + .3f, wz, 10.4f, 1.0f, 2.9f);
                    d = SMin(SMin(SMin(a, b, .3f), lintel, .7f), c, .3f);
                    d = SMin(d, apron, 1.0f);
                    break;
                }
                case Cleft:
                {
                    // One huge boulder split by a crevice that widens upward, a chockstone wedged across its top.
                    float cx = x - .25f * (float)Math.Sin(z * .35f + v);
                    float body = SMin(GF(x, y - 3.3f, z, 5.6f, 3.7f, 4.6f, 61 + v, .88f), GF(x, y - .7f, z, 6.0f, 1.5f, 5.0f, 62 + v, .88f), .9f);
                    float half = 1.5f + .12f * y;
                    body = SMax(body, half - Math.Abs(cx), .35f);
                    float chock = GF(x - .2f, y - 5.9f, z - .6f, 2.8f, 1.05f, 1.7f, 63 + v, .8f);
                    float side = GF(x + 4.6f, y - 1.0f, z + 3.6f, 1.6f, 1.2f, 1.4f, 64 + v, .84f);
                    d = SMin(SMin(body, chock, .3f), side, .4f);
                    break;
                }
                default: // Terrace
                {
                    // R61: four flat tiers stepping up to 7.6 m before scale, with steep weathered risers and level
                    // ledges (room for corals and anemones), an overhang niche below the second tier.
                    float zz = v == 0 ? z : -z;
                    float t1 = Step(x, y, zz, 0, 0, 6.0f, 1.15f, 2.0f, 71 + v);
                    float t2 = Step(x, y, zz, -1.1f, -.7f, 4.3f, 1.12f, 4.0f, 72 + v);
                    float t3 = Step(x, y, zz, -1.9f, -1.3f, 3.0f, 1.1f, 5.9f, 73 + v);
                    float t4 = Step(x, y, zz, -2.4f, -1.7f, 1.8f, 1.05f, 7.6f, 74 + v);
                    d = SMin(SMin(SMin(t1, t2, .12f), t3, .12f), t4, .12f);
                    d = SMax(d, -Ellipsoid(x - 4.4f, y - 1.5f, zz - .6f, 2.8f, 1.9f, 2.5f), .35f);
                    d = SMin(d, GF(x - 6.8f, y - .4f, zz + 3.6f, 1.1f, .8f, 1.0f, 75 + v, .84f), .3f);
                    weather = 0;
                    break;
                }
            }
            return d + GiantWeather(x, y, z, v) * weather;
        }
        // R61: a seven-sided column, radius r0 at the foot tapering to r1 at its height h,
        // leaning along x, its top broken off at a slant.
        private static readonly float[] ColumnCos = new float[7 * 64], ColumnSin = new float[7 * 64];
        private static float Column(float x, float y, float z, float cx, float cz, float r0, float r1, float h, int seed, float lean)
        {
            float qx = x - cx - lean * y, qz = z - cz;
            float t = Math.Max(0, Math.Min(1, y / h)), r = r0 + (r1 - r0) * t;
            float side = Prism(qx, qz, r, seed);
            float top = y - h + (qx * .14f - qz * .09f) * (1 + (seed % 3) * .35f);
            return SMax(side, top, .3f);
        }
        // Seven planes round the axis, each a little in or out (a chipped prism), as a 2D distance.
        private static float Prism(float qx, float qz, float r, int seed)
        {
            int k = (seed & 63) * 7;
            if (ColumnCos[k] == 0 && ColumnSin[k] == 0)
                lock (ColumnCos)
                    for (int i = 0; i < 7; i++) { double a = seed * 1.7 + i * .8976; ColumnSin[k + i] = (float)Math.Sin(a); ColumnCos[k + i] = (float)Math.Cos(a); }
            float side = -100;
            for (int i = 0; i < 7; i++) side = Math.Max(side, qx * ColumnCos[k + i] + qz * ColumnSin[k + i] - r * (1 + (Hash(i, seed, 5) - .5f) * .45f));
            return side;
        }
        // A terrace tier: a stretched chipped prism with weathered, slightly battered risers and a level top.
        private static float Step(float x, float y, float z, float cx, float cz, float r, float stretch, float top, int seed)
        {
            float qx = (x - cx) / stretch, qz = z - cz;
            float riser = Prism(qx, qz, r + Math.Max(0, top - y) * .1f, seed);
            riser += (Noise(x * .45f + seed, y * .35f, z * .45f) - .5f) * .8f + (Noise(x * 1.4f, y * 1.1f + seed, z * 1.4f) - .5f) * .22f;
            float level = y - top + (x * .035f - z * .025f) + (Noise(x * .6f, seed, z * .6f) - .5f) * .1f;
            return SMax(riser, level, .14f);
        }
        // Horizontal strata: each bed bulges a little at its foot, a lip every ~0.9 m.
        private static float Strata(float x, float y, float z, int v)
        {
            float g = y * .8f + Noise(x * .3f + v * 7, y * .12f, z * .3f) * 2.2f, f = g - (float)Math.Floor(g);
            float lip = f < .8f ? f / .8f : (1 - f) / .2f;
            float amount = .06f + .1f * Noise(x * .5f, y * .3f + 4, z * .5f);
            return amount * (lip - .5f) * 2;
        }
        // Narrow vertical cracks.
        private static float Cracks(float x, float y, float z, int v)
        {
            float c = Noise(x * .7f + 3 + v, y * .1f, z * .7f - 5);
            return Math.Max(0, .08f - Math.Abs(c - .5f)) / .08f * .16f;
        }

        // R59: the swim-throughs of a giant (local centre and direction through it, at scale 1). The layout keeps
        // them free of rubble and plants; the checks swim through them.
        public static void Passages(int kind, int variant, List<Point> centres, List<Point> directions, List<bool> through)
        {
            centres.Clear(); directions.Clear(); through.Clear();
            Action<float, float, float, float, float, bool> add = (x, y, z, dx, dz, t) =>
            { centres.Add(new Point(x, y, z)); directions.Add(new Point(dx, 0, dz).Normalized); through.Add(t); };
            switch (kind)
            {
                case Stacks: // from the middle of the three pillars out through each gap
                    add(0, 2.4f, -.2f, 0, 1, false); add(0, 2.4f, -.2f, -.95f, -.31f, false); add(0, 2.4f, -.2f, .89f, -.45f, false); break;
                case GreatArch: add(0, 2.2f, 0, 0, 1, true); break;
                case Mesa: add(.65f, 1.9f, 0, 0, 1, true); break;
                case Ridge:
                {
                    float w = -2.25f, n = 5.05f;
                    add(w, 2.0f, .9f * (float)Math.Sin(w * .28f + variant * 1.3f), -.25f * (float)Math.Cos(w * .28f + variant * 1.3f), 1, true);
                    add(n, 2.4f, .9f * (float)Math.Sin(n * .28f + variant * 1.3f), -.25f * (float)Math.Cos(n * .28f + variant * 1.3f), 1, true);
                    break;
                }
                case Cleft: add(.25f * (float)Math.Sin(variant), 2.2f, 0, 0, 1, true); break;
                case Terrace: add(4.6f, 1.5f, variant == 0 ? .6f : -.6f, 1, 0, false); break;
            }
        }

        // R59: each giant's field sampled every half metre over its box (exact at the samples), worked out once
        // per run on a worker thread (AquariumEnvironment.PrewarmRocks) or on first use. Trilinear between the
        // samples it is within .65 m of the field (gradient under 1.5); outside the box it is a lower bound.
        private const float CoarseStep = .5f;
        private static readonly float[][] coarse = new float[Count * Variants][];
        private static readonly int[][] coarseSize = new int[Count * Variants][];
        private static readonly object coarseLock = new object();
        public static void PrepareCoarse(int kind, int variant)
        {
            int key = kind * Variants + variant;
            lock (coarseLock) { if (coarse[key] != null) return; }
            var lo = LocalMin[kind]; var hi = LocalMax[kind];
            int nx = (int)Math.Ceiling((hi.x - lo.x) / CoarseStep) + 1, ny = (int)Math.Ceiling((hi.y - lo.y) / CoarseStep) + 1, nz = (int)Math.Ceiling((hi.z - lo.z) / CoarseStep) + 1;
            var field = new float[nx * ny * nz];
            for (int z = 0; z < nz; z++) for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
                field[(z * ny + y) * nx + x] = Local(kind, variant, new Point(lo.x + x * CoarseStep, lo.y + y * CoarseStep, lo.z + z * CoarseStep));
            lock (coarseLock) { if (coarse[key] == null) { coarseSize[key] = new[] { nx, ny, nz }; coarse[key] = field; } }
        }
        public static float CoarseDistance(int kind, int variant, Point p)
        {
            int key = kind * Variants + variant; var field = coarse[key];
            if (field == null) { PrepareCoarse(kind, variant); field = coarse[key]; }
            var size = coarseSize[key]; int nx = size[0], ny = size[1], nz = size[2];
            var lo = LocalMin[kind];
            float fx = (p.x - lo.x) / CoarseStep, fy = (p.y - lo.y) / CoarseStep, fz = (p.z - lo.z) / CoarseStep;
            float cx = Math.Max(0, Math.Min(nx - 1.001f, fx)), cy = Math.Max(0, Math.Min(ny - 1.001f, fy)), cz = Math.Max(0, Math.Min(nz - 1.001f, fz));
            float ox = (fx - cx) * CoarseStep, oy = (fy - cy) * CoarseStep, oz = (fz - cz) * CoarseStep, outside = (float)Math.Sqrt(ox * ox + oy * oy + oz * oz);
            int x = (int)cx, y = (int)cy, z = (int)cz; float u = cx - x, v = cy - y, w = cz - z;
            int i = (z * ny + y) * nx + x, sy = nx, sz = nx * ny;
            float a = field[i] + (field[i + 1] - field[i]) * u, b = field[i + sy] + (field[i + sy + 1] - field[i + sy]) * u;
            float c = field[i + sz] + (field[i + sz + 1] - field[i + sz]) * u, d = field[i + sz + sy] + (field[i + sz + sy + 1] - field[i + sz + sy]) * u;
            float near = a + (b - a) * v, far = c + (d - c) * v;
            float inner = near + (far - near) * w;
            // Out of the box: the stone lies inside it, so at least the way to the box; at most the box face's value less it.
            return outside > 0 ? Math.Max(outside, inner - outside * 1.5f) : inner;
        }

        // R59: local floor samples (1 m apart) a giant stands on, for the layout's room and spacing tests.
        private static readonly Dictionary<int, Point[]> footprints = new Dictionary<int, Point[]>();
        public static Point[] FootprintSamples(int kind, int variant)
        {
            int key = kind * 10 + variant;
            lock (footprints)
            {
                Point[] ready; if (footprints.TryGetValue(key, out ready)) return ready;
                var lo = LocalMin[kind]; var hi = LocalMax[kind]; var list = new List<Point>();
                for (float x = (float)Math.Floor(lo.x) + .5f; x < hi.x; x += 1)
                    for (float z = (float)Math.Floor(lo.z) + .5f; z < hi.z; z += 1)
                    {
                        bool solid = false;
                        for (float y = .4f; y < hi.y && !solid; y += 1.1f) solid = Local(kind, variant, new Point(x, y, z)) < .35f;
                        if (solid) list.Add(new Point(x, 0, z));
                    }
                return footprints[key] = list.ToArray();
            }
        }

        // Gradient of the local field (unnormalised direction is fine for normals).
        public static Point LocalNormal(int kind, int variant, Point p)
        {
            const float e = .012f;
            return new Point(Local(kind, variant, new Point(p.x + e, p.y, p.z)) - Local(kind, variant, new Point(p.x - e, p.y, p.z)),
                Local(kind, variant, new Point(p.x, p.y + e, p.z)) - Local(kind, variant, new Point(p.x, p.y - e, p.z)),
                Local(kind, variant, new Point(p.x, p.y, p.z + e)) - Local(kind, variant, new Point(p.x, p.y, p.z - e))).Normalized;
        }

        // ---------------------------------------------------------------- frames
        public static Point ToLocal(AquariumRock r, Point world)
        {
            double a = r.yaw * Math.PI / 180; float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            float dx = world.x - r.position.x, dz = world.z - r.position.z;
            return new Point((c * dx - s * dz) / r.scale, (world.y - r.position.y) / r.scale, (s * dx + c * dz) / r.scale);
        }
        public static Point ToWorld(AquariumRock r, Point local)
        {
            double a = r.yaw * Math.PI / 180; float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            float x = local.x * r.scale, z = local.z * r.scale;
            return new Point(r.position.x + c * x + s * z, r.position.y + local.y * r.scale, r.position.z - s * x + c * z);
        }
        public static Point DirectionToWorld(AquariumRock r, Point d)
        {
            double a = r.yaw * Math.PI / 180; float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return new Point(c * d.x + s * d.z, d.y, -s * d.x + c * d.z);
        }
    }

    // R58: the placed rocks of one layout with a coarse lookup grid, answering the world queries the cliffs answer
    // in the default tank (AquariumMonolithLayout delegates to it in the twin tanks).
    public sealed class AquariumRockSet
    {
        public static readonly AquariumRockSet Empty = new AquariumRockSet(new AquariumRock[0]);
        public readonly AquariumRock[] Rocks;
        private readonly float[] cos, sin, cx, cy, cz, reach;
        // R59: a giant's local box (scaled), a tighter bound than its sphere for the long shapes.
        private readonly bool[] giant;
        private const float Cell = 4f, MinX = -60, MinZ = -32, Pad = 2.5f;
        private const int NX = 30, NZ = 16;
        private readonly int[][] cells;
        public AquariumRockSet(AquariumRock[] rocks)
        {
            Rocks = rocks; int n = rocks.Length;
            cos = new float[n]; sin = new float[n]; cx = new float[n]; cy = new float[n]; cz = new float[n]; reach = new float[n]; giant = new bool[n];
            var lists = new List<int>[NX * NZ];
            for (int i = 0; i < n; i++)
            {
                var r = rocks[i]; double a = r.yaw * Math.PI / 180; cos[i] = (float)Math.Cos(a); sin[i] = (float)Math.Sin(a);
                var lo = AquariumRockShapes.LocalMin[r.kind]; var hi = AquariumRockShapes.LocalMax[r.kind];
                var mid = AquariumRockShapes.ToWorld(r, (lo + hi) * .5f);
                cx[i] = mid.x; cy[i] = mid.y; cz[i] = mid.z; reach[i] = (hi - lo).Length * .5f * r.scale + .05f; giant[i] = AquariumRockShapes.IsGiant(r.kind);
                int x0 = Math.Max(0, (int)Math.Floor((cx[i] - reach[i] - Pad - MinX) / Cell)), x1 = Math.Min(NX - 1, (int)Math.Floor((cx[i] + reach[i] + Pad - MinX) / Cell));
                int z0 = Math.Max(0, (int)Math.Floor((cz[i] - reach[i] - Pad - MinZ) / Cell)), z1 = Math.Min(NZ - 1, (int)Math.Floor((cz[i] + reach[i] + Pad - MinZ) / Cell));
                for (int gx = x0; gx <= x1; gx++) for (int gz = z0; gz <= z1; gz++)
                {
                    int k = gz * NX + gx; if (lists[k] == null) lists[k] = new List<int>(4); lists[k].Add(i);
                }
            }
            cells = new int[NX * NZ][];
            for (int k = 0; k < cells.Length; k++) cells[k] = lists[k] == null ? null : lists[k].ToArray();
        }
        public int Count { get { return Rocks.Length; } }
        public Point Centre(int i) { return new Point(cx[i], cy[i], cz[i]); }
        public float Reach(int i) { return reach[i]; }

        // The field of one rock at a world point.
        public float RockDistance(int i, Point p)
        {
            var r = Rocks[i]; float c = cos[i], s = sin[i];
            float dx = p.x - r.position.x, dz = p.z - r.position.z, k = 1f / r.scale;
            var local = new Point((c * dx - s * dz) * k, (p.y - r.position.y) * k, (s * dx + c * dz) * k);
            if (giant[i])
            {
                // R59: well away from a giant its sampled field answers (within .65 m, always more than 3 m
                // out); the exact field is worked out only near the stone.
                float far = AquariumRockShapes.CoarseDistance(r.kind, r.variant, local);
                if (far > 3f) return far * r.scale;
            }
            return AquariumRockShapes.Local(r.kind, r.variant, local) * r.scale;
        }
        private int[] CellOf(Point p)
        {
            int gx = (int)Math.Floor((p.x - MinX) / Cell), gz = (int)Math.Floor((p.z - MinZ) / Cell);
            if (gx < 0 || gz < 0 || gx >= NX || gz >= NZ) return null;
            return cells[gz * NX + gx];
        }
        private float Bound(int i, Point p)
        {
            float x = p.x - cx[i], y = p.y - cy[i], z = p.z - cz[i], sphere = (float)Math.Sqrt(x * x + y * y + z * z) - reach[i];
            if (!giant[i] || sphere > Pad) return sphere;
            var r = Rocks[i]; var lo = AquariumRockShapes.LocalMin[r.kind]; var hi = AquariumRockShapes.LocalMax[r.kind];
            float dx = p.x - r.position.x, dz = p.z - r.position.z, k = 1f / r.scale;
            float lx = (cos[i] * dx - sin[i] * dz) * k, ly = (p.y - r.position.y) * k, lz = (sin[i] * dx + cos[i] * dz) * k;
            float ox = Math.Max(Math.Max(lo.x - lx, lx - hi.x), 0), oy = Math.Max(Math.Max(lo.y - ly, ly - hi.y), 0), oz = Math.Max(Math.Max(lo.z - lz, lz - hi.z), 0);
            return Math.Max(sphere, (float)Math.Sqrt(ox * ox + oy * oy + oz * oz) * r.scale);
        }

        // R59: narrows [t0, t1] to where the segment crosses a giant's local box (grown by pad); false if it misses.
        private bool BoxSpan(int i, Point start, Point end, float pad, ref float t0, ref float t1)
        {
            var r = Rocks[i]; float c = cos[i], s = sin[i], k = 1f / r.scale;
            float ax = start.x - r.position.x, az = start.z - r.position.z, bx = end.x - r.position.x, bz = end.z - r.position.z;
            float sx = (c * ax - s * az) * k, sy = (start.y - r.position.y) * k, sz = (s * ax + c * az) * k;
            float ex = (c * bx - s * bz) * k, ey = (end.y - r.position.y) * k, ez = (s * bx + c * bz) * k;
            var lo = AquariumRockShapes.LocalMin[r.kind]; var hi = AquariumRockShapes.LocalMax[r.kind]; float g = pad * k;
            if (!Slab(sx, ex - sx, lo.x - g, hi.x + g, ref t0, ref t1)) return false;
            if (!Slab(sy, ey - sy, lo.y - g, hi.y + g, ref t0, ref t1)) return false;
            return Slab(sz, ez - sz, lo.z - g, hi.z + g, ref t0, ref t1);
        }
        private static bool Slab(float origin, float delta, float lo, float hi, ref float t0, ref float t1)
        {
            if (Math.Abs(delta) < 1e-7f) return origin >= lo && origin <= hi;
            float a = (lo - origin) / delta, b = (hi - origin) / delta;
            if (a > b) { float t = a; a = b; b = t; }
            t0 = Math.Max(t0, a); t1 = Math.Min(t1, b);
            return t0 <= t1;
        }

        // Distance to the nearest rock (100 when none is near), exact within the lookup pad.
        public float Distance(Point p)
        {
            var list = CellOf(p); if (list == null) return 100;
            float best = 100;
            foreach (int i in list)
            {
                float bound = Bound(i, p); if (bound >= best) continue;
                best = bound > Pad ? Math.Min(best, bound) : Math.Min(best, RockDistance(i, p));
            }
            return best;
        }
        public bool Overlaps(Point p, float radius)
        {
            var list = CellOf(p); if (list == null) return false;
            foreach (int i in list) if (Bound(i, p) < radius && RockDistance(i, p) < radius) return true;
            return false;
        }
        public Point Normal(Point p)
        {
            const float e = .014f;
            return new Point(Distance(new Point(p.x + e, p.y, p.z)) - Distance(new Point(p.x - e, p.y, p.z)),
                Distance(new Point(p.x, p.y + e, p.z)) - Distance(new Point(p.x, p.y - e, p.z)),
                Distance(new Point(p.x, p.y, p.z + e)) - Distance(new Point(p.x, p.y, p.z - e))).Normalized;
        }
        // Pushes a body of this radius out of the rocks (as AquariumMonolithLayout.Constrain).
        public Point Constrain(Point p, float radius)
        {
            if (!Overlaps(p, radius)) return p;
            for (int pass = 0; pass < 32; pass++)
            {
                float d = Distance(p); if (d >= radius) break;
                var normal = Normal(p); float step = radius - d + .015f;
                if (p.y + normal.y * step < AquariumSeabed.Height(p) + radius || p.y + normal.y * step > AquariumModel.WaterHeight - radius)
                { normal.y = 0; if (normal.Length < .0001f) normal = new Point(1, 0); normal = normal.Normalized; }
                p += normal * step;
            }
            return p;
        }
        // First blocked fraction of the segment (padded), or 2 when it stays clear.
        public float Hit(Point start, Point end, float padding = 0)
        {
            if (Rocks.Length == 0) return 2;
            var ray = end - start; float length = ray.Length;
            if (length < 1e-5f) return Distance(start) <= padding ? 0 : 2;
            float best = 2;
            for (int i = 0; i < Rocks.Length; i++)
            {
                // Where the segment runs through this rock's padded bounding sphere.
                float R = reach[i] + Math.Max(0, padding) + .01f;
                float ox = start.x - cx[i], oy = start.y - cy[i], oz = start.z - cz[i];
                float a = length * length, b = 2 * (ox * ray.x + oy * ray.y + oz * ray.z), c = ox * ox + oy * oy + oz * oz - R * R;
                float disc = b * b - 4 * a * c; if (disc < 0) continue;
                float root = (float)Math.Sqrt(disc), t0 = (-b - root) / (2 * a), t1 = (-b + root) / (2 * a);
                if (giant[i] && !BoxSpan(i, start, end, Math.Max(0, padding) + .05f, ref t0, ref t1)) continue;
                if (t1 < 0 || t0 > 1 || t0 >= best) continue;
                float t = Math.Max(0, t0), stop = Math.Min(1, Math.Min(t1, best));
                for (int step = 0; step < 600 && t <= stop; step++)
                {
                    float d = RockDistance(i, start + ray * t) - padding;
                    if (d < .0015f) { best = Math.Min(best, t); break; }
                    t += Math.Max(.0005f / length, d * .55f / length);
                }
            }
            return best;
        }
    }
}
