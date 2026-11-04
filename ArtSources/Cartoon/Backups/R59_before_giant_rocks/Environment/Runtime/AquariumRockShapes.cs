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
    // All arithmetic is plain float math, identical on every machine.
    public static class AquariumRockShapes
    {
        public const int Pillar = 0, Table = 1, Arch = 2, Split = 3, Pebbles = 4, Ledge = 5, Worn = 6, Spire = 7, Count = 8, Variants = 2;
        public static readonly string[] Names = { "Pillar stack", "Table rock", "Arch", "Split boulder", "Pebble heap", "Overhang ledge", "Worn boulder", "Jagged spire" };
        // Local bounds (scale 1) of every kind, generous enough for the surface noise.
        public static readonly Point[] LocalMin =
        {
            new Point(-1.45f, -.45f, -1.35f), new Point(-2.05f, -.45f, -1.65f), new Point(-3.35f, -.45f, -1.45f), new Point(-2.25f, -.45f, -1.65f),
            new Point(-2.0f, -.45f, -2.0f), new Point(-2.1f, -.45f, -1.6f), new Point(-2.25f, -.45f, -2.05f), new Point(-1.55f, -.45f, -1.35f)
        };
        public static readonly Point[] LocalMax =
        {
            new Point(1.45f, 3.35f, 1.35f), new Point(2.25f, 2.25f, 1.65f), new Point(3.15f, 3.75f, 1.45f), new Point(2.25f, 2.15f, 1.65f),
            new Point(2.0f, 1.55f, 2.0f), new Point(2.4f, 3.25f, 1.6f), new Point(2.45f, 1.85f, 2.05f), new Point(2.25f, 4.75f, 1.75f)
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
                default: // Spire
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
            }
            return d;
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
        private const float Cell = 4f, MinX = -60, MinZ = -32, Pad = 2.5f;
        private const int NX = 30, NZ = 16;
        private readonly int[][] cells;
        public AquariumRockSet(AquariumRock[] rocks)
        {
            Rocks = rocks; int n = rocks.Length;
            cos = new float[n]; sin = new float[n]; cx = new float[n]; cy = new float[n]; cz = new float[n]; reach = new float[n];
            var lists = new List<int>[NX * NZ];
            for (int i = 0; i < n; i++)
            {
                var r = rocks[i]; double a = r.yaw * Math.PI / 180; cos[i] = (float)Math.Cos(a); sin[i] = (float)Math.Sin(a);
                var lo = AquariumRockShapes.LocalMin[r.kind]; var hi = AquariumRockShapes.LocalMax[r.kind];
                var mid = AquariumRockShapes.ToWorld(r, (lo + hi) * .5f);
                cx[i] = mid.x; cy[i] = mid.y; cz[i] = mid.z; reach[i] = (hi - lo).Length * .5f * r.scale + .05f;
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
            return AquariumRockShapes.Local(r.kind, r.variant, new Point((c * dx - s * dz) * k, (p.y - r.position.y) * k, (s * dx + c * dz) * k)) * r.scale;
        }
        private int[] CellOf(Point p)
        {
            int gx = (int)Math.Floor((p.x - MinX) / Cell), gz = (int)Math.Floor((p.z - MinZ) / Cell);
            if (gx < 0 || gz < 0 || gx >= NX || gz >= NZ) return null;
            return cells[gz * NX + gx];
        }
        private float Bound(int i, Point p)
        { float x = p.x - cx[i], y = p.y - cy[i], z = p.z - cz[i]; return (float)Math.Sqrt(x * x + y * y + z * z) - reach[i]; }

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
                if (t1 < 0 || t0 > 1 || t0 >= best) continue;
                float t = Math.Max(0, t0), stop = Math.Min(1, Math.Min(t1, best));
                for (int step = 0; step < 300 && t <= stop; step++)
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
