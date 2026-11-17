using System;
using System.Collections.Generic;

namespace PrototypeLab.Aquarium
{
    // R57: "쌍둥이 수조", the second map. Two big tanks, A in the west and B in the east, stand with flat front walls
    // (x = PlaneA / PlaneB) facing each other, and a straight acrylic tunnel joins them along z = 0. The tunnel has
    // vertical walls up to the springline and a round arch above; the walls flare into each tank wall through a
    // smooth bell mouth. One signed field (Water, positive in the water) traces the plan outline, the tunnel's
    // radius table gives its roof, and the same shapes drive collision, the bots' grid and the baked meshes.
    // All of it is pure arithmetic, identical on every machine.
    // R73: the same machinery lays out the third map (AquariumMaps.Grotto, "보랏빛 터널 수조"): one stone-walled box tank
    // with a U-shaped acrylic visitor tube on legs through its middle (AquariumTubeMap; it is solid to swimmers and
    // takes the place of the twin tunnel's roof in ConstrainRoof / AboveRoof / RoofHit). Every cache is kept per map
    // (Shape), chosen by AquariumMaps.Current.
    public static class AquariumTwinMap
    {
        public const float PlaneA = -11f, PlaneB = 11f;
        public const float TunnelHalfWidth = 4.25f, MouthFlare = 3f, CornerRound = 1.5f;
        public const float Springline = 3.4f;
        // The building: its outer wall and the visitor gallery's rear wall, measured out from the water.
        public const float OuterOffset = 5.2f, RearWallOffset = 4.6f;
        public const float HalfWidth = 51f, HalfDepth = 24f;

        // A tank: a rounded superellipse (exponent 4 or 2) gently reshaped by a few low harmonics, cut flat at its
        // front wall. Harmonics are (cos k theta, sin k theta) weights for k = 2 and 3, written without trigonometry.
        private sealed class Tank
        {
            public float cx, cz, a, b; public bool square;
            public float c2, s2, c3, s3;
        }
        // One map's tanks and everything worked out from them.
        private sealed class Shape
        {
            public Tank A, B;
            public float[] grid; public int gridX, gridZ;
            public readonly object gridLock = new object(), outlineLock = new object();
            public AquariumPolygon polygon; public Point[] outer; public float[] radius;
            public bool entryReady; public Point entryAnchor, entryRight;
            // R73: one rounded box tank (no second tank, no tunnel): half extents, corner radius, the dive deck's x.
            public bool single; public float halfX, halfZ, corner, entryX;
        }
        private static readonly Shape TwinShape = new Shape
        {
            A = new Tank { cx = PlaneA - .86f * 18f, cz = 0, a = 18f, b = 18.5f, square = true, c2 = .03f, s2 = .018f, c3 = .012f, s3 = -.02f },
            B = new Tank { cx = PlaneB + .80f * 20.5f, cz = 0, a = 20.5f, b = 21.5f, square = false, c2 = -.02f, s2 = -.03f, c3 = -.018f, s3 = .012f }
        };
        // R73: the tube tank: 74 x 36 m of water (about the twin tanks' area). A and B only name its west and east halves
        // (layout points, the halves' middles); the water is the box.
        public const float TubeHalfX = 33f, TubeHalfZ = 16f;   // R73e: 20% less water (66 x 32 m)
        private static readonly Shape TubeShape = new Shape
        {
            single = true, halfX = TubeHalfX, halfZ = TubeHalfZ, corner = 3.2f, entryX = 22f,
            A = new Tank { cx = -16.5f, cz = 0, a = 11f, b = 16f, square = true },
            B = new Tank { cx = 16.5f, cz = 0, a = 11f, b = 16f, square = true }
        };
        private static Shape S { get { return AquariumMaps.Current == AquariumMaps.Grotto ? TubeShape : TwinShape; } }
        // True on the tube tank (one box, no tunnel).
        public static bool Single { get { return S.single; } }
        private static Tank A { get { return S.A; } }
        private static Tank B { get { return S.B; } }
        public static Point CentreA { get { return new Point(A.cx, 0, A.cz); } }
        public static Point CentreB { get { return new Point(B.cx, 0, B.cz); } }

        // ------------------------------------------------------------------ the plan field
        private static float Implicit(float x, float z, Tank t)
        {
            float dx = (x - t.cx) / t.a, dz = (z - t.cz) / t.b, r2 = dx * dx + dz * dz;
            float rho = t.square ? (float)Math.Sqrt(Math.Sqrt(dx * dx * dx * dx + dz * dz * dz * dz)) : (float)Math.Sqrt(r2);
            float m = 1;
            if (r2 > 1e-8f)
            {
                float r = (float)Math.Sqrt(r2), c = dx / r, s = dz / r;
                float cos2 = c * c - s * s, sin2 = 2 * c * s, cos3 = c * (c * c - 3 * s * s), sin3 = s * (3 * c * c - s * s);
                m += t.c2 * cos2 + t.s2 * sin2 + t.c3 * cos3 + t.s3 * sin3;
            }
            return rho / m - 1;
        }
        // Approximate signed distance to a tank's rounded outline (positive inside).
        private static float TankDistance(float x, float z, Tank t)
        {
            const float e = .05f;
            float g = Implicit(x, z, t);
            float gx = (Implicit(x + e, z, t) - Implicit(x - e, z, t)) / (2 * e), gz = (Implicit(x, z + e, t) - Implicit(x, z - e, t)) / (2 * e);
            float length = (float)Math.Sqrt(gx * gx + gz * gz);
            return -g / Math.Max(length, .02f);
        }
        private static float SMin(float a, float b, float k) { float h = Math.Max(k - Math.Abs(a - b), 0) / k; return Math.Min(a, b) - h * h * k * .25f; }
        private static float SMax(float a, float b, float k) { float h = Math.Max(k - Math.Abs(a - b), 0) / k; return Math.Max(a, b) + h * h * k * .25f; }
        // A tank with its flat front wall (the tunnel side), its corners eased.
        public static float TankField(float x, float z) { return TankField(S, x, z); }
        private static float TankField(Shape s, float x, float z)
        {
            if (s.single)
            {
                // a rounded rectangle's exact signed distance, positive inside
                float qx = Math.Abs(x) - (s.halfX - s.corner), qz = Math.Abs(z) - (s.halfZ - s.corner);
                float ox = Math.Max(qx, 0), oz = Math.Max(qz, 0);
                return -((float)Math.Sqrt(ox * ox + oz * oz) + Math.Min(Math.Max(qx, qz), 0) - s.corner);
            }
            return x < 0 ? SMin(TankDistance(x, z, s.A), PlaneA - x, CornerRound) : SMin(TankDistance(x, z, s.B), x - PlaneB, CornerRound);
        }
        private static float Bore(float x, float z) { return Math.Min(TunnelHalfWidth - Math.Abs(z), PlaneB + 4 - Math.Abs(x)); }
        // Positive in the water (both tanks and the tunnel), roughly the distance to the glass.
        public static float Water(float x, float z) { return Water(S, x, z); }
        private static float Water(Shape s, float x, float z) { return s.single ? TankField(s, x, z) : SMax(TankField(s, x, z), Bore(x, z), MouthFlare); }
        public static bool InTunnel(float x) { return x > PlaneA && x < PlaneB && !S.single; }
        // Outward (away from the water) unit normal of the field's level lines at p.
        public static Point Outward(Point p)
        {
            const float e = .04f;
            float gx = Water(p.x + e, p.z) - Water(p.x - e, p.z), gz = Water(p.x, p.z + e) - Water(p.x, p.z - e);
            var n = new Point(-gx, 0, -gz); return n.Length > 1e-6f ? n.Normalized : new Point(1, 0, 0);
        }
        // Slides p across the level lines onto Water = level (the building's walls and bands run on these).
        public static Point Project(Point p, float level)
        {
            const float e = .04f;
            for (int i = 0; i < 4; i++)
            {
                float s = Water(p.x, p.z) - level; if (Math.Abs(s) < .0005f) break;
                float gx = (Water(p.x + e, p.z) - Water(p.x - e, p.z)) / (2 * e), gz = (Water(p.x, p.z + e) - Water(p.x, p.z - e)) / (2 * e);
                float g2 = gx * gx + gz * gz; if (g2 < 1e-6f) break;
                p = new Point(p.x - gx * s / g2, p.y, p.z - gz * s / g2);
            }
            return p;
        }
        // 0 = tank A, 1 = tank B, -1 = the tunnel.
        public static int TankOf(Point p) { if (S.single) return p.x < 0 ? 0 : 1; return p.x <= PlaneA ? 0 : p.x >= PlaneB ? 1 : -1; }

        // ------------------------------------------------------------------ outlines
        private const float GridStep = .2f, GridMinX = -58, GridMaxX = 58, GridMinZ = -31, GridMaxZ = 31;
        private static void EnsureGrid(Shape s)
        {
            lock (s.gridLock)
            {
                if (s.grid != null) return;
                int nx = (int)Math.Round((GridMaxX - GridMinX) / GridStep) + 1, nz = (int)Math.Round((GridMaxZ - GridMinZ) / GridStep) + 1;
                var values = new float[nx * nz];
                for (int iz = 0; iz < nz; iz++) for (int ix = 0; ix < nx; ix++) values[iz * nx + ix] = Water(s, GridMinX + ix * GridStep, GridMinZ + iz * GridStep);
                s.gridX = nx; s.gridZ = nz; s.grid = values;
            }
        }
        // The closed contour Water = level, resampled evenly and wound clockwise (as the default tank's trace).
        public static Point[] Outline(float level, float spacing) { return Contour(S, level, spacing); }
        private static Point[] Contour(Shape s, float level, float spacing)
        {
            EnsureGrid(s);
            int nx = s.gridX, nz = s.gridZ; var grid = s.grid;
            Func<int, float> f = i => { float v = grid[i] - level; return Math.Abs(v) < 1e-6f ? 1e-6f : v; };
            var nodes = new Dictionary<long, Point>(); var links = new Dictionary<long, List<long>>();
            Func<int, int, long> edge = (i, j) => { int lo = Math.Min(i, j), hi = Math.Max(i, j); return ((long)lo << 32) | (uint)hi; };
            Func<int, Point> at = i => new Point(GridMinX + (i % nx) * GridStep, GridMinZ + (i / nx) * GridStep);
            Action<long, int, int> node = (key, i, j) =>
            {
                if (nodes.ContainsKey(key)) return;
                float a = f(i), b = f(j); var p = at(i); var q = at(j);
                nodes[key] = p + (q - p) * (a / (a - b)); links[key] = new List<long>(2);
            };
            Action<long, long> link = (k1, k2) => { links[k1].Add(k2); links[k2].Add(k1); };
            for (int iz = 0; iz < nz - 1; iz++) for (int ix = 0; ix < nx - 1; ix++)
            {
                int a = iz * nx + ix, b = a + 1, c = a + nx + 1, d = a + nx;
                bool ia = f(a) > 0, ib = f(b) > 0, ic = f(c) > 0, id = f(d) > 0;
                if (ia == ib && ib == ic && ic == id) continue;
                long bottom = edge(a, b), right = edge(b, c), top = edge(d, c), left = edge(a, d);
                var cross = new List<long>(4);
                if (ia != ib) { node(bottom, a, b); cross.Add(bottom); }
                if (ib != ic) { node(right, b, c); cross.Add(right); }
                if (id != ic) { node(top, d, c); cross.Add(top); }
                if (ia != id) { node(left, a, d); cross.Add(left); }
                if (cross.Count == 2) link(cross[0], cross[1]);
                else
                {
                    bool centre = (f(a) + f(b) + f(c) + f(d)) * .25f > 0;
                    // Isolate the two corners on the other side of the cell centre.
                    if (ia != centre) { link(left, bottom); link(right, top); }
                    else { link(bottom, right); link(top, left); }
                }
            }
            var visited = new HashSet<long>(); List<Point> best = null;
            foreach (var start in nodes.Keys)
            {
                if (visited.Contains(start)) continue;
                var loop = new List<Point>(); long previous = -1, current = start;
                for (int guard = 0; guard < nodes.Count + 2; guard++)
                {
                    visited.Add(current); loop.Add(nodes[current]);
                    var adjacent = links[current]; if (adjacent.Count != 2) break;
                    long next = adjacent[0] == previous ? adjacent[1] : adjacent[0]; previous = current; current = next;
                    if (current == start) break;
                }
                if (best == null || loop.Count > best.Count) best = loop;
            }
            var result = Resample(best, spacing);
            float area = 0; for (int i = 0; i < result.Count; i++) { var p = result[i]; var q = result[(i + 1) % result.Count]; area += p.x * q.z - q.x * p.z; }
            if (area > 0) result.Reverse();
            return result.ToArray();
        }
        private static List<Point> Resample(List<Point> source, float spacing)
        {
            float total = 0; for (int i = 0; i < source.Count; i++) total += (source[(i + 1) % source.Count] - source[i]).Length;
            int count = Math.Max(8, (int)Math.Ceiling(total / spacing)); float step = total / count, accumulated = 0, target = 0;
            var result = new List<Point>(count);
            for (int i = 0; i < source.Count && result.Count < count; i++)
            {
                var a = source[i]; var b = source[(i + 1) % source.Count]; float length = (b - a).Length;
                while (target <= accumulated + length && result.Count < count)
                { result.Add(a + (b - a) * ((target - accumulated) / Math.Max(length, 1e-6f))); target += step; }
                accumulated += length;
            }
            return result;
        }

        public static AquariumPolygon Polygon
        {
            get
            {
                var s = S;
                if (s.polygon != null) return s.polygon;
                lock (s.outlineLock) { if (s.polygon == null) s.polygon = new AquariumPolygon(Contour(s, 0, .35f), p => Water(s, p.x, p.z)); }
                return s.polygon;
            }
        }
        public static Point[] WaterOutline { get { return Polygon.Points; } }
        // The building's outer wall (offset out from the water).
        public static Point[] OuterOutline
        {
            get
            {
                var s = S;
                if (s.outer != null) return s.outer;
                lock (s.outlineLock) { if (s.outer == null) s.outer = Contour(s, -OuterOffset, .35f); }
                return s.outer;
            }
        }
        public static Point PointOn(float parameter, bool outerLoop)
        {
            var loop = outerLoop ? OuterOutline : WaterOutline;
            float s = (parameter - (float)Math.Floor(parameter)) * loop.Length; int i = (int)s; float t = s - i;
            var a = loop[i % loop.Length]; var b = loop[(i + 1) % loop.Length]; return a + (b - a) * t;
        }
        public static Point[] Sample(bool outerLoop, int count)
        {
            var result = new Point[count]; for (int i = 0; i < count; i++) result[i] = PointOn(i / (float)count, outerLoop); return result;
        }

        // ------------------------------------------------------------------ the tunnel
        private const float TableStep = .05f, TableStart = PlaneA + .02f, TableEnd = PlaneB - .02f;
        private static float[] RadiusTable()
        {
            var s = S;
            if (s.radius != null) return s.radius;
            lock (s.outlineLock) { if (s.radius == null) s.radius = BuildRadiusTable(s); }
            return s.radius;
        }
        private static float[] BuildRadiusTable(Shape s)
        {
            int n = (int)Math.Round((TableEnd - TableStart) / TableStep) + 1; var table = new float[n];
            for (int i = 0; i < n; i++)
            {
                float x = TableStart + i * TableStep, lo = 0, hi = 12;
                for (int k = 0; k < 32; k++) { float mid = (lo + hi) * .5f; if (Water(s, x, mid) > 0) lo = mid; else hi = mid; }
                table[i] = lo;
            }
            return table;
        }
        // Half width of the tunnel bore at x (the arch radius); grows through each bell mouth.
        public static float Radius(float x)
        {
            var table = RadiusTable();
            float s = (Math.Max(TableStart, Math.Min(TableEnd, x)) - TableStart) / TableStep; int i = Math.Min(table.Length - 2, (int)s); float t = s - i;
            return table[i] + (table[i + 1] - table[i]) * t;
        }
        // The highest centre a body of this radius may have under the tunnel roof (vertical walls, then the arch).
        public static float RoofCeiling(float x, float z, float pad)
        {
            float r = Radius(x) - pad; if (r <= 0) return Springline;
            return Springline + (float)Math.Sqrt(Math.Max(0, r * r - z * z));
        }
        // Inside the tunnel slab (between the two front walls, widened by the body).
        public static bool UnderRoof(Point p, float pad) { return p.x > PlaneA - pad && p.x < PlaneB + pad && !S.single; }
        public static bool AboveRoof(Point p, float pad)
        {
            if (S.single) return AquariumTubeMap.Overlaps(p, pad);   // R73: the visitor tube and its legs
            return UnderRoof(p, pad) && p.y > RoofCeiling(p.x, p.z, pad) + .001f;
        }
        // Pushes a body out of the tunnel roof or the front wall above the mouth, whichever is nearer.
        public static Point ConstrainRoof(Point p, float pad)
        {
            if (S.single) return AquariumTubeMap.Constrain(p, pad);
            if (!UnderRoof(p, pad)) return p;
            float top = RoofCeiling(p.x, p.z, pad);
            if (p.y <= top) return p;
            float down = p.y - top, toA = p.x - (PlaneA - pad), toB = (PlaneB + pad) - p.x;
            if (Math.Min(toA, toB) < down) p.x = toA < toB ? PlaneA - pad - .002f : PlaneB + pad + .002f;
            else p.y = top;
            return p;
        }
        private static bool Blocked(Point p, float pad)
        {
            if (!UnderRoof(p, pad)) return false;
            if (p.y > RoofCeiling(p.x, p.z, pad)) return true;
            return InTunnel(p.x) && Math.Abs(p.z) > Radius(p.x) - pad;
        }
        // Swept test against the tunnel's acrylic (roof, side walls, the front wall over each mouth): the first
        // blocked fraction of the segment, or 2 when it stays clear (as AquariumModel.ObstacleHit).
        public static float RoofHit(Point start, Point end, float pad = 0)
        {
            if (S.single) return AquariumTubeMap.Hit(start, end, pad);
            float lo = PlaneA - pad, hi = PlaneB + pad;
            if ((start.x <= lo && end.x <= lo) || (start.x >= hi && end.x >= hi)) return 2;
            var d = end - start; float length = d.Length;
            if (length < 1e-5f) return Blocked(start, pad) ? 0 : 2;
            float t0 = 0, t1 = 1;
            if (Math.Abs(d.x) > 1e-6f)
            {
                float ta = (lo - start.x) / d.x, tb = (hi - start.x) / d.x;
                t0 = Math.Max(0, Math.Min(ta, tb)); t1 = Math.Min(1, Math.Max(ta, tb));
                if (t0 > t1) return 2;
            }
            int steps = (int)Math.Ceiling((t1 - t0) * length / .2f) + 1; float previous = t0;
            for (int i = 0; i <= steps; i++)
            {
                float t = t0 + (t1 - t0) * i / steps;
                if (Blocked(start + d * t, pad)) return i == 0 ? t : previous;
                previous = t;
            }
            return 2;
        }

        // ------------------------------------------------------------------ layout helpers
        public static Point Layout(float x, float y, float z)
        {
            float u = Math.Max(-1, Math.Min(1, x / 15f)), v = Math.Max(-1, Math.Min(1, z / 10f));
            float px = u < 0 ? A.cx + (2 * u + 1) * .74f * A.a : B.cx + (2 * u - 1) * .74f * B.a;
            float pz = v * .74f * (u < 0 ? A.b : B.b);
            return new Point(px, y, pz);
        }
        public static Point Middle(Point p)
        {
            if (p.x > PlaneA - 1.5f && p.x < PlaneB + 1.5f) return new Point(p.x, p.y, 0);
            return p.x < 0 ? new Point(A.cx, p.y, A.cz) : new Point(B.cx, p.y, B.cz);
        }
        // R57: wandering fish now and then head for the tunnel and swim through it to the other tank.
        // roll in 0..1 (the host's random). Returns the new steering.
        public static Point Wander(Point position, Point steering, float roll)
        {
            if (S.single) return steering;
            if (InTunnel(position.x))
            {
                float along = Math.Abs(steering.x) > .05f ? Math.Sign(steering.x) : (position.x < 0 ? 1 : -1);
                float lift = Math.Max(-.25f, Math.Min(.25f, (2.4f - position.y) * .08f));
                return new Point(along, lift + steering.y * .3f, -position.z * .12f + steering.z * .25f).Normalized;
            }
            if (roll > .3f) return steering;
            float mouth = position.x < 0 ? PlaneA : PlaneB;
            float away = Math.Abs(position.x - mouth);
            if (away > 22) return steering;
            var target = new Point(mouth + (position.x < 0 ? 1.5f : -1.5f), Math.Max(1.6f, Math.Min(6.2f, position.y)), position.z * .1f);
            var toward = target - position; return toward.Length > .01f ? toward.Normalized : steering;
        }

        // ------------------------------------------------------------------ dive entry
        // The dive deck stands on tank A's north wall, facing south over the water.
        private static Shape EnsureEntry()
        {
            var s = S;
            if (s.entryReady) return s;
            var loop = WaterOutline;
            lock (s.outlineLock) { if (!s.entryReady) BuildEntry(s, loop); }
            return s;
        }
        private static void BuildEntry(Shape s, Point[] loop)
        {
            int best = 0; float top = float.MinValue;
            for (int i = 0; i < loop.Length; i++)
                if (Math.Abs(loop[i].x - (s.single ? s.entryX : s.A.cx + 1.5f)) < 1.2f && loop[i].z > top) { top = loop[i].z; best = i; }
            var prev = loop[(best + loop.Length - 4) % loop.Length]; var next = loop[(best + 4) % loop.Length];
            s.entryRight = (next - prev).Normalized; s.entryRight.y = 0; s.entryAnchor = loop[best];
            s.entryReady = true;
        }
        public static Point EntryAnchor { get { return EnsureEntry().entryAnchor; } }
        public static Point EntryRight { get { return EnsureEntry().entryRight; } }

        // ------------------------------------------------------------------ drains
        // Two return drains per tank on open, level sand away from the tunnel mouths, the dive water and the glass,
        // at least MinimumSpacing apart (the host's scenery seed picks them; every machine gets the same).
        public static Point[] PickDrains(Random random)
        {
            var pools = new[] { new List<Point>(), new List<Point>() };
            for (float x = -50; x <= 50; x += 1f) for (float z = -22; z <= 22; z += 1f) // R61: a metre apart (the denser rocks made the scan slow)
            {
                var p = new Point(x, 0, z);
                if (Single ? AquariumTubeMap.Near(x, z, 3f) : x > PlaneA - 6.5f && x < PlaneB + 6.5f) continue;
                if (!AquariumDrainLayout.IsClear(p)) continue;
                // R59: open water above the lid too (no giant's overhang or arch over a drain).
                float sand = AquariumSeabed.Height(p);
                if (AquariumMonolithLayout.Overlaps(new Point(x, sand + 3.2f, z), AquariumDrainLayout.ClearanceRadius + .6f)
                    || AquariumMonolithLayout.Overlaps(new Point(x, sand + 5.6f, z), AquariumDrainLayout.ClearanceRadius + .6f)) continue;
                pools[x < 0 ? 0 : 1].Add(p);
            }
            var picked = new List<Point>(4);
            foreach (var pool in pools)
            {
                for (int i = pool.Count - 1; i > 0; i--) { int j = random.Next(i + 1); var t = pool[i]; pool[i] = pool[j]; pool[j] = t; }
                bool done = false;
                foreach (float spacing in new[] { AquariumDrainLayout.MinimumSpacing + 2, AquariumDrainLayout.MinimumSpacing, 12f, 8f })
                {
                    for (int i = 0; i < pool.Count && !done; i++)
                        for (int j = i + 1; j < pool.Count && !done; j++)
                        {
                            var a = pool[i]; var b = pool[j]; float dx = a.x - b.x, dz = a.z - b.z;
                            if (dx * dx + dz * dz >= spacing * spacing) { picked.Add(a); picked.Add(b); done = true; }
                            if (j > i + 60) break;
                        }
                    if (done) break;
                }
                if (!done) return null;
            }
            return picked.ToArray();
        }
    }
}
