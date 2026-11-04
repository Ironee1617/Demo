using System;
using System.Collections.Generic;

namespace PrototypeLab.Aquarium
{
    // R69: the twin tanks' tunnel gets its own dressing every game, from the same scenery seed (so every machine
    // builds the same): rock ribs pressed to the acrylic in pairs, a low arch alcove, reef shelves and overhangs,
    // outcrops and rubble hugging both walls, then corals, anemones, seaweed, grass, sea stars, shells and pebbles
    // on and round them. The rocks join the layout's rock set (AquariumRockSet), so they are drawn, collide, join
    // (R64 seams) and enter the bots' grid exactly like the tanks' rocks.
    // R69c: four or five rock structures in all (see PlaceTunnelRocks), no wall outcrops, fillers or rubble.
    // R69b: three to five obstacles also stand in the middle of the tunnel (a central pillar group, an arch spanning
    // it, a rib reaching out from one wall past the centre, a table rock, a coral mound), staggered from side to side
    // so the way through snakes. The wall dressing keeps to the sides of a gently winding lane, but the lane is no
    // longer kept open: instead every rock is accepted only while a diver-sized way through remains (TunnelSpace: a
    // ball of TunnelClearance radius, more than the diver's body and its turning reach, can still travel from one
    // mouth to the other between the rocks, the sand, the acrylic and the roof), and while the bots' 2 m grid still
    // links the two tanks through the tunnel. No rock touches the acrylic and none stands in the bell mouths.
    public sealed partial class AquariumSceneryLayout
    {
        // Half width of the open swim lane (3.7 m across); it runs the whole height of the tunnel.
        public const float TunnelLaneHalfWidth = 1.85f;
        // Rocks stay between -TunnelReach and TunnelReach (clear of the bell mouths).
        public const float TunnelReach = 9.3f;
        // R69b: radius of the open way kept through the tunnel (the diver's body is .6 m round and 2.44 m long, it
        // keeps .95 m off walls and 1.38 m over the sand; a 1.3 m ball holds it at any heading).
        public const float TunnelClearance = 1.3f;
        // R69b: the middle obstacles placed this game, and the way through found after the last rock (QA).
        public int TunnelMiddleCount { get; private set; }
        public Point[] TunnelRoute { get; private set; }
        // QA: what each middle station got (type, lean, filler rocks, whether its lean side closed).
        public string TunnelLog { get; private set; }
        private System.Text.StringBuilder tunnelLog;
        // The side (+1 north, -1 south) the lane leans to this game; 0 when the layout has no tunnel dressing.
        public float TunnelLaneSide { get; private set; }
        // QA: lay the twin tanks out without the tunnel's dressing (before and after renders and timings).
        public static bool TunnelDressingOff;
        private float tunnelPhase, tunnelWave;
        // First tunnel rock in TwinRocks (the tanks' rocks come first).
        public int TunnelRockStart { get; private set; }
        public int TunnelRockCount { get { return TwinRocks.Count - TunnelRockStart; } }
        public float TunnelLaneCentre(float x) { return TunnelLaneSide * .5f + .32f * (float)Math.Sin(x * tunnelWave + tunnelPhase); }
        public bool InTunnelLane(Point p, float radius)
        {
            return TunnelLaneSide != 0 && Math.Abs(p.x) < AquariumTwinMap.PlaneB + .5f && Math.Abs(p.z - TunnelLaneCentre(p.x)) < TunnelLaneHalfWidth + radius;
        }

        // ---------------------------------------------------------------- rocks
        private TunnelSpace space;
        private AquariumRock[] PlaceTunnelRocks(Random random, AquariumRock[] tankRocks)
        {
            var rocks = new List<AquariumRock>(tankRocks); TunnelRockStart = tankRocks.Length; TunnelMiddleCount = 0; TunnelRoute = null;
            if (TunnelDressingOff) { TunnelLaneSide = 0; return tankRocks; }
            TunnelLaneSide = random.NextDouble() < .5 ? -1 : 1;
            tunnelWave = Range(random, .32f, .5f); tunnelPhase = (float)(random.NextDouble() * Math.PI * 2);
            var placed = new List<Placed>();
            space = new TunnelSpace(); tunnelLog = new System.Text.StringBuilder();
            // R69c: four or five rock structures in all, spread evenly along the tunnel: one stands in the middle (an
            // arch spanning the tunnel, a pillar or a coral mound) and the others lean to alternate sides (a pillar or
            // spire, a rib reaching out from the wall, a table rock, a coral mound), so the way through weaves a little
            // without being a maze.
            int count = 4 + random.Next(2), central = random.Next(count);
            float step = 16.4f / count, lean = random.NextDouble() < .5 ? -1 : 1;
            var sideTypes = new List<int> { 0, 2, 3, 0, 2, 4 };
            for (int i = sideTypes.Count - 1; i > 0; i--) { int j = random.Next(i + 1); int t = sideTypes[i]; sideTypes[i] = sideTypes[j]; sideTypes[j] = t; }
            int[] centralTypes = { 1, 0, 4, 1 };
            for (int n = 0, sideAt = 0; n < count; n++)
            {
                float x = -8.2f + step * (n + .5f) + Range(random, -.4f, .4f);
                bool middle = n == central;
                int type = middle ? centralTypes[random.Next(centralTypes.Length)] : sideTypes[sideAt++ % sideTypes.Count];
                float side = middle ? 0 : lean;
                bool made = TryMiddle(random, rocks, placed, type, x, side) || TryMiddle(random, rocks, placed, 4, x, side) || TryMiddle(random, rocks, placed, 0, x, side);
                tunnelLog.Append(middle ? "c" : "s").Append(x.ToString("0.0")).Append(" t").Append(type).Append(made ? "+ " : "- ");
                if (made) TunnelMiddleCount++;
                if (!middle) lean = -lean;
            }
            TunnelRoute = space.Route(); space = null; TunnelLog = tunnelLog.ToString();
            return rocks.ToArray();
        }

        // R69b: one middle obstacle at station x, leaning to one side (lean = -1 or 1; R69c: 0 in the middle):
        //  0 a pillar or spire, 1 an arch spanning the tunnel (under or over it),
        //  2 a rib: an overhang ledge or split boulder reaching out from one wall past the centre, 3 a table rock,
        //  4 a coral mound (a broad pebble heap the life dresses with corals).
        // Tried a few times, smaller and nearer its side each time, until the way through stays open.
        private bool TryMiddle(Random random, List<AquariumRock> rocks, List<Placed> placed, int type, float x, float lean)
        {
            int variant = random.Next(AquariumRockShapes.Variants); float tint = (float)random.NextDouble();
            for (int attempt = 0; attempt < 7; attempt++)
            {
                float shrink = 1 - attempt * .07f, push = attempt * .25f;
                int kind; float scale, yaw = (float)(random.NextDouble() * 360), z;
                switch (type)
                {
                    case 0: kind = random.NextDouble() < .65 ? AquariumRockShapes.Pillar : AquariumRockShapes.Spire; scale = (kind == AquariumRockShapes.Pillar ? Range(random, 1.15f, 1.35f) : Range(random, .95f, 1.1f)) * shrink; z = lean * (Range(random, .2f, .8f) + push); break;
                    case 1: kind = AquariumRockShapes.Arch; scale = Range(random, 1.05f, 1.18f) * shrink; yaw = 90 + Range(random, -6, 6); z = lean * Range(random, 0, .35f); break;
                    case 2:
                        kind = random.NextDouble() < .6 ? AquariumRockShapes.Ledge : AquariumRockShapes.Split;
                        scale = (kind == AquariumRockShapes.Ledge ? Range(random, 1.4f, 1.65f) : Range(random, 1.4f, 1.6f)) * shrink;
                        if (kind == AquariumRockShapes.Ledge) yaw = (lean > 0 ? 90 : 270) + Range(random, -12, 12); // its back on the wall at lean, the shelf toward the middle
                        z = float.NaN; break;
                    case 3: kind = AquariumRockShapes.Table; scale = Range(random, 1.35f, 1.55f) * shrink; z = lean * (Range(random, .2f, .8f) + push); break;
                    default: kind = AquariumRockShapes.Pebbles; scale = Range(random, 1.4f, 1.7f) * shrink; z = lean * (Range(random, .2f, .7f) + push); break;
                }
                AquariumRock rock;
                if (float.IsNaN(z))
                {
                    // The rib: slid in from the wall at lean until it clears the acrylic.
                    bool fits = false; rock = new AquariumRock();
                    for (float d = .15f; d < 4.2f && !fits; d += .15f)
                    {
                        z = lean * (AquariumTwinMap.Radius(x) - d);
                        rock = new AquariumRock { kind = kind, variant = variant, position = new Point(x, TunnelFoot(kind, x, z, scale), z), yaw = yaw, scale = scale, tint = tint };
                        fits = TunnelBoxInside(rock) && TunnelGlassClear(rock);
                    }
                    if (!fits || !space.TryAdd(rock)) continue;
                }
                else
                {
                    rock = new AquariumRock { kind = kind, variant = variant, position = new Point(x, TunnelFoot(kind, x, z, scale), z), yaw = yaw, scale = scale, tint = tint };
                    if (!TunnelBoxInside(rock) || !TunnelGlassClear(rock) || !space.TryAdd(rock)) continue;
                }
                rocks.Add(rock); placed.Add(new Placed { p = new Point(x, 0, z), footprint = AquariumRockShapes.Footprint(kind) * scale, cluster = 700 });
                return true;
            }
            return false;
        }

        // The foot a little under the lowest sand round it (the tunnel's bed is nearly level).
        private static float TunnelFoot(int kind, float x, float z, float scale)
        {
            float f = AquariumRockShapes.Footprint(kind) * scale * .55f, low = AquariumSeabed.TwinHeight(x, z);
            for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; low = Math.Min(low, AquariumSeabed.TwinHeight(x + (float)Math.Cos(a) * f, z + (float)Math.Sin(a) * f)); }
            return low - .14f * scale;
        }
        // The rock's plan box stays clear of the bell mouths and inside the bore's outline grown by a little.
        private static bool TunnelBoxInside(AquariumRock r)
        {
            var lo = AquariumRockShapes.LocalMin[r.kind]; var hi = AquariumRockShapes.LocalMax[r.kind];
            for (int c = 0; c < 4; c++)
            {
                var w = AquariumRockShapes.ToWorld(r, new Point((c & 1) == 0 ? lo.x : hi.x, 0, (c & 2) == 0 ? lo.z : hi.z));
                if (Math.Abs(w.x) > TunnelReach + .4f || AquariumTwinMap.Water(w.x, w.z) < -.35f) return false;
            }
            return true;
        }
        // The stone itself keeps 12 cm off the acrylic: the side walls from under the sand to the springline, and the arch.
        private static bool TunnelGlassClear(AquariumRock r)
        {
            var lo = AquariumRockShapes.LocalMin[r.kind]; var hi = AquariumRockShapes.LocalMax[r.kind];
            float x0 = float.MaxValue, x1 = float.MinValue, top = r.position.y + hi.y * r.scale;
            for (int c = 0; c < 4; c++) { var w = AquariumRockShapes.ToWorld(r, new Point((c & 1) == 0 ? lo.x : hi.x, 0, (c & 2) == 0 ? lo.z : hi.z)); x0 = Math.Min(x0, w.x); x1 = Math.Max(x1, w.x); }
            // R69b: both walls (the middle obstacles reach across).
            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                float side = sideIndex == 0 ? -1 : 1;
                for (float x = x0; x <= x1 + .001f; x += .25f)
                {
                    float radius = AquariumTwinMap.Radius(x) - .12f;
                    for (float y = .05f; y <= Math.Min(top, AquariumTwinMap.Springline) + .001f; y += .25f)
                        if (RockField(r, new Point(x, y, side * radius)) < .02f) return false;
                    if (top <= AquariumTwinMap.Springline) continue;
                    for (float a = 0; a <= 1.571f; a += .12f)
                    {
                        var p = new Point(x, AquariumTwinMap.Springline + radius * (float)Math.Sin(a), side * radius * (float)Math.Cos(a));
                        if (p.y > top + .05f) break;
                        if (RockField(r, p) < .02f) return false;
                    }
                }
            }
            return true;
        }
        private static float RockField(AquariumRock r, Point world)
        {
            return AquariumRockShapes.Local(r.kind, r.variant, AquariumRockShapes.ToLocal(r, world)) * r.scale;
        }


        // ---------------------------------------------------------------- R69b: the way through
        // The tunnel's water on a 25 cm lattice with the joined rock field (the same smooth union, R64 Blend, as
        // AquariumRockSet.Distance, capped). A cell is open for the diver's ball when the rocks are TunnelClearance
        // away, the acrylic .95 m and more, the roof .95 m, and the sand 1.38 m below. A rock is kept only while open
        // cells still join the two mouths, and while the bots' grid (2 m cells as AquariumBotNav) still links the tanks.
        private sealed class TunnelSpace
        {
            private const float Step = .3f, X0 = -12.5f, Z0 = -5f, Y0 = 0f, Cap = TunnelClearance + 1.0f;
            private const int NX = 84, NZ = 34, NY = 28;
            private const float RoofPad = AquariumBodyCollision.DiverFloor + .22f;
            private readonly float[] field = new float[NX * NY * NZ];
            private readonly bool[] water = new bool[NX * NY * NZ];
            private readonly float[] scratch = new float[NX * NY * NZ];
            private readonly int[] queue = new int[NX * NY * NZ], from = new int[NX * NY * NZ];
            private readonly List<int> touched = new List<int>(8192);
            public TunnelSpace()
            {
                for (int iz = 0; iz < NZ; iz++) for (int ix = 0; ix < NX; ix++)
                {
                    // Where the diver's centre may be: AquariumBodyCollision keeps its silhouette DiverFloor over the sand
                    // and (its pitch added) at least as far under the acrylic roof, which in the tunnel also bounds the
                    // centre's side reach (AquariumTwinMap.RoofHit).
                    float x = X0 + ix * Step, z = Z0 + iz * Step, floor = AquariumSeabed.TwinHeight(x, z) + AquariumBodyCollision.DiverFloor + .1f;
                    bool tunnel = Math.Abs(x) < AquariumTwinMap.PlaneB + RoofPad;
                    bool wet = AquariumTwinMap.Water(x, z) >= AquariumBodyCollision.DiverWall && (!tunnel || Math.Abs(z) <= AquariumTwinMap.Radius(x) - RoofPad);
                    float roof = tunnel ? AquariumTwinMap.RoofCeiling(x, z, RoofPad) : AquariumModel.WaterHeight - RoofPad;
                    for (int iy = 0; iy < NY; iy++)
                    {
                        float y = Y0 + iy * Step; int i = Index(ix, iy, iz);
                        field[i] = Cap; water[i] = wet && y >= floor && y <= roof;
                    }
                }
            }
            private static int Index(int ix, int iy, int iz) { return (iz * NY + iy) * NX + ix; }
            // Adds the rock when the way stays open; otherwise leaves the field as it was.
            public bool TryAdd(AquariumRock r)
            {
                var lo = AquariumRockShapes.LocalMin[r.kind]; var hi = AquariumRockShapes.LocalMax[r.kind];
                var mid = AquariumRockShapes.ToWorld(r, (lo + hi) * .5f); float reach = (hi - lo).Length * .5f * r.scale + Cap + AquariumRockSet.Blend;
                int x0 = Math.Max(0, (int)Math.Floor((mid.x - reach - X0) / Step)), x1 = Math.Min(NX - 1, (int)Math.Ceiling((mid.x + reach - X0) / Step));
                int y0 = Math.Max(0, (int)Math.Floor((mid.y - reach - Y0) / Step)), y1 = Math.Min(NY - 1, (int)Math.Ceiling((mid.y + reach - Y0) / Step));
                int z0 = Math.Max(0, (int)Math.Floor((mid.z - reach - Z0) / Step)), z1 = Math.Min(NZ - 1, (int)Math.Ceiling((mid.z + reach - Z0) / Step));
                double a = r.yaw * Math.PI / 180; float c = (float)Math.Cos(a), s = (float)Math.Sin(a), k = 1f / r.scale;
                touched.Clear();
                for (int iz = z0; iz <= z1; iz++) for (int iy = y0; iy <= y1; iy++) for (int ix = x0; ix <= x1; ix++)
                {
                    int i = Index(ix, iy, iz); float old = field[i];
                    float x = X0 + ix * Step, y = Y0 + iy * Step, z = Z0 + iz * Step;
                    float dx = x - r.position.x, dz = z - r.position.z;
                    float lx = (c * dx - s * dz) * k, ly = (y - r.position.y) * k, lz = (s * dx + c * dz) * k;
                    // The box is a lower bound of the stone: where it is a blend's width beyond, nothing changes.
                    float ox = Math.Max(Math.Max(lo.x - lx, lx - hi.x), 0), oy = Math.Max(Math.Max(lo.y - ly, ly - hi.y), 0), oz = Math.Max(Math.Max(lo.z - lz, lz - hi.z), 0);
                    if ((float)Math.Sqrt(ox * ox + oy * oy + oz * oz) * r.scale >= old + AquariumRockSet.Blend) continue;
                    float d = AquariumRockShapes.Local(r.kind, r.variant, new Point(lx, ly, lz)) * r.scale;
                    float next = Math.Min(Cap, AquariumRockSet.SMin(old, d, AquariumRockSet.Blend));
                    if (next >= old) continue;
                    scratch[i] = old; field[i] = next; touched.Add(i);
                }
                if (Open() && BotsCross()) return true;
                foreach (int i in touched) field[i] = scratch[i];
                return false;
            }
            private bool Free(int i) { return water[i] && field[i] >= TunnelClearance; }
            // Open cells join the west end (in tank A) to the east end (in tank B).
            private bool Open() { return Search(false) >= 0; }
            private int Search(bool keepPath)
            {
                int head = 0, tail = 0;
                for (int i = 0; i < from.Length; i++) from[i] = -2;
                for (int iz = 0; iz < NZ; iz++) for (int iy = 0; iy < NY; iy++) { int i = Index(0, iy, iz); if (Free(i)) { from[i] = -1; queue[tail++] = i; } }
                while (head < tail)
                {
                    int i = queue[head++]; int ix = i % NX, iy = (i / NX) % NY, iz = i / (NX * NY);
                    if (ix == NX - 1) return i;
                    for (int n = 0; n < 6; n++)
                    {
                        int jx = ix + (n == 0 ? 1 : n == 1 ? -1 : 0), jy = iy + (n == 2 ? 1 : n == 3 ? -1 : 0), jz = iz + (n == 4 ? 1 : n == 5 ? -1 : 0);
                        if (jx < 0 || jy < 0 || jz < 0 || jx >= NX || jy >= NY || jz >= NZ) continue;
                        int j = Index(jx, jy, jz); if (from[j] != -2 || !Free(j)) continue;
                        from[j] = i; queue[tail++] = j;
                    }
                }
                return -1;
            }
            // The way through as world points (every metre or so), or null when closed.
            public Point[] Route()
            {
                int end = Search(true); if (end < 0) return null;
                var path = new List<Point>();
                for (int i = end, n = 0; i >= 0; i = from[i], n++)
                    if (n % 4 == 0 || from[i] < 0) path.Add(new Point(X0 + (i % NX) * Step, Y0 + ((i / NX) % NY) * Step, Z0 + (i / (NX * NY)) * Step));
                path.Reverse(); return path.ToArray();
            }
            // The bots' grid through the tunnel: cells 2 m apart (x even, z odd, the four water layers) whose centre
            // is .6 m (+ margin) off rock, linked when the straight line between them keeps .5 m (+ margin) off it.
            private static readonly float[] Layers = { 1.9f, 4.2f, 6.6f };
            private float Sample(Point p)
            {
                int ix = (int)Math.Round((p.x - X0) / Step), iy = (int)Math.Round((p.y - Y0) / Step), iz = (int)Math.Round((p.z - Z0) / Step);
                if (ix < 0 || iy < 0 || iz < 0 || ix >= NX || iy >= NY || iz >= NZ) return Cap;
                // The lattice point is within .26 m of p (the field changes by at most that much).
                return field[Index(ix, iy, iz)] - .26f;
            }
            private static bool NavWater(Point c, float pad)
            {
                if (AquariumTwinMap.Water(c.x, c.z) < pad || c.y < AquariumSeabed.TwinHeight(c.x, c.z) + 1f) return false;
                return !(Math.Abs(c.x) < AquariumTwinMap.PlaneB + pad && c.y > AquariumTwinMap.RoofCeiling(c.x, c.z, pad));
            }
            private bool BotsCross()
            {
                const int NXc = 13, NZc = 4; int NL = Layers.Length;
                var ok = new bool[NXc * NZc * NL]; var seen = new bool[ok.Length]; var list = new int[ok.Length];
                Func<int, Point> centre = i => new Point(-12 + (i % NXc) * 2, Layers[i / (NXc * NZc)], -3 + ((i / NXc) % NZc) * 2);
                for (int i = 0; i < ok.Length; i++) { var c = centre(i); ok[i] = NavWater(c, 1f) && Sample(c) >= .75f; }
                int head = 0, tail = 0;
                for (int i = 0; i < ok.Length; i++) if (ok[i] && i % NXc == 0) { seen[i] = true; list[tail++] = i; }
                while (head < tail)
                {
                    int i = list[head++]; if (i % NXc == NXc - 1) return true;
                    int x = i % NXc, z = (i / NXc) % NZc, l = i / (NXc * NZc); var a = centre(i);
                    for (int dl = -1; dl <= 1; dl++) for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int X = x + dx, Z = z + dz, L = l + dl;
                        if (X < 0 || Z < 0 || L < 0 || X >= NXc || Z >= NZc || L >= NL) continue;
                        int j = (L * NZc + Z) * NXc + X; if (!ok[j] || seen[j]) continue;
                        var b = centre(j); bool clear = true;
                        for (float t = 0; t <= 1.001f && clear; t += .1f) { var p = a + (b - a) * t; clear = Sample(p) >= .65f && NavWater(p, .5f); }
                        if (!clear) continue;
                        seen[j] = true; list[tail++] = j;
                    }
                }
                return false;
            }
        }

        // ---------------------------------------------------------------- life
        // Sand in the tunnel (between the reach marks), off the lane, clear of the rocks and under the roof.
        private bool TunnelSandClear(Point p, float radius, float height, bool laneOk)
        {
            if (Math.Abs(p.x) > TunnelReach + .6f || AquariumTwinMap.Water(p.x, p.z) < radius * .7f + .3f) return false;
            if (!laneOk && InTunnelLane(p, radius * .6f)) return false;
            float ground = AquariumSeabed.TwinHeight(p.x, p.z);
            if (ground + height > AquariumTwinMap.RoofCeiling(p.x, p.z, .3f)) return false;
            for (float h = .15f; h < height + .3f; h += .5f)
                if (TwinRocks.Overlaps(new Point(p.x, ground + h, p.z), radius * .75f + .12f)) return false;
            return true;
        }
        // A sand point round the foot of one of the tunnel rocks.
        private Point TunnelRockFoot(Random random, out int rock)
        {
            rock = TunnelRockStart + random.Next(TunnelRockCount); var r = TwinRocks.Rocks[rock];
            double a = random.NextDouble() * Math.PI * 2;
            float reach = AquariumRockShapes.Footprint(r.kind) * r.scale * Range(random, .45f, .95f) + Range(random, .05f, 1.0f);
            return new Point(r.position.x + (float)Math.Cos(a) * reach, 0, r.position.z + (float)Math.Sin(a) * reach);
        }
        private bool TunnelTop(Random random, int rock, float minUp, float clearance, out Point foot)
        {
            if (!RockTop(random, rock, minUp, AquariumModel.WaterHeight, out foot)) return false;
            return foot.y + clearance < AquariumTwinMap.RoofCeiling(foot.x, foot.z, .2f);
        }
        private void PlaceTunnelLife(Random random, Spacing spacing, List<AquariumSceneAnemone> anemones, List<AquariumScenePlant> plants, List<AquariumSceneCoral> corals, List<AquariumSceneDetail> details)
        {
            if (random == null || TunnelRockCount <= 0) return;
            int start = TunnelRockStart, end = TwinRocks.Count, coralsFrom = corals.Count;
            Func<Point, float, bool> nearCoral = (foot, size) =>
            {
                for (int i = coralsFrom; i < corals.Count; i++) { var c = corals[i]; if (Flat(c.position, foot) < (c.size + size) * .75f && Math.Abs(c.position.y - foot.y) < .5f) return true; }
                return false;
            };
            // One to three anemones on the flat tops (clownfish homes in the tunnel too).
            for (int tries = 0, made = 0, want = 1 + random.Next(2); tries < 200 && made < want; tries++)
            {
                int rock = start + random.Next(end - start); int kind = TwinRocks.Rocks[rock].kind;
                if (kind == AquariumRockShapes.Pebbles || kind == AquariumRockShapes.Spire) continue;
                Point foot; if (!TunnelTop(random, rock, .82f, .8f, out foot)) continue;
                float size = Range(random, .26f, .36f), r = AnemoneSpread(size);
                if (!spacing.Free(foot, r, 1.15f)) continue;
                spacing.Add(foot, r); made++;
                anemones.Add(new AquariumSceneAnemone { position = foot, size = size, kind = random.Next(2), yaw = (float)(random.NextDouble() * 360), ledge = true });
            }
            // Coral colonies on every rock (more on the shelves, the arch and the ribs).
            for (int rock = start; rock < end; rock++)
            {
                var r = TwinRocks.Rocks[rock]; bool small = r.kind == AquariumRockShapes.Pebbles || r.scale < .55f;
                bool feature = r.kind == AquariumRockShapes.Arch || r.kind == AquariumRockShapes.Table || r.kind == AquariumRockShapes.Ledge || r.kind == AquariumRockShapes.Pillar || r.kind == AquariumRockShapes.Spire;
                int want = small ? 1 + random.Next(2) : feature ? 4 + random.Next(3) : 3 + random.Next(2);
                for (int tries = 0, made = 0; tries < want * 8 && made < want; tries++)
                {
                    float size = small ? Range(random, .2f, .32f) : Range(random, .24f, .48f);
                    Point foot; if (!TunnelTop(random, rock, .55f, size, out foot)) continue;
                    if (nearCoral(foot, size) || !spacing.Free(foot, size * .6f, .8f)) continue;
                    foot.y -= .03f; made++;
                    corals.Add(new AquariumSceneCoral { position = foot, size = size, kind = random.Next(6), yaw = (float)(random.NextDouble() * 360) });
                }
            }
            // Coral heads, seaweed and grass round the rock feet.
            for (int tries = 0, made = 0, want = 4 + random.Next(4); tries < 160 && made < want; tries++)
            {
                int rock; var p = TunnelRockFoot(random, out rock); float size = Range(random, .24f, .46f);
                if (!TunnelSandClear(p, size * .8f, size, true)) continue;
                p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .01f;
                if (!spacing.Free(p, size * .8f, .7f)) continue;
                spacing.Add(p, size * .8f); made++;
                corals.Add(new AquariumSceneCoral { position = p, size = size, kind = random.Next(6), yaw = (float)(random.NextDouble() * 360) });
            }
            for (int tries = 0, made = 0, want = 3 + random.Next(4); tries < 160 && made < want; tries++)
            {
                int rock; var p = TunnelRockFoot(random, out rock);
                int form = random.Next(5); float height = form == 2 ? Range(random, 1.4f, 2.6f) : Range(random, .8f, 1.9f), r = PlantSpread(form, height);
                if (!TunnelSandClear(p, r, height, true)) continue;
                p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .01f;
                if (!spacing.Free(p, r, .75f)) continue;
                spacing.Add(p, r); made++;
                plants.Add(new AquariumScenePlant { position = p, form = form, height = height, radius = r, phase = (float)(random.NextDouble() * Math.PI * 2), yaw = (float)(random.NextDouble() * 360) });
            }
            // A few on the flat tops of the shelves too.
            for (int tries = 0, made = 0; tries < 80 && made < 2; tries++)
            {
                int rock = start + random.Next(end - start);
                int form = random.Next(5); float height = Range(random, .6f, 1.2f), r = PlantSpread(form, height);
                Point foot; if (!TunnelTop(random, rock, .72f, height + .2f, out foot)) continue;
                if (!spacing.Free(foot, r, .9f)) continue;
                spacing.Add(foot, r); made++;
                plants.Add(new AquariumScenePlant { position = foot, form = form, height = height, radius = r, phase = (float)(random.NextDouble() * Math.PI * 2), yaw = (float)(random.NextDouble() * 360), ledge = true });
            }
            for (int tries = 0, made = 0, want = 6 + random.Next(5); tries < 200 && made < want; tries++)
            {
                int rock; var p = TunnelRockFoot(random, out rock);
                float height = Range(random, .35f, .65f), r = PlantSpread(GrassForm, height);
                if (!TunnelSandClear(p, r, height, true)) continue;
                p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .005f;
                if (!spacing.Free(p, r, .75f)) continue;
                spacing.Add(p, r); made++;
                plants.Add(new AquariumScenePlant { position = p, form = GrassForm, height = height, radius = r, phase = (float)(random.NextDouble() * Math.PI * 2), yaw = (float)(random.NextDouble() * 360) });
            }
            // Sea stars on the rock faces and the sand beside them, shells and pebbles strewn round the feet
            // (looks only, nothing collides with them; the lane's sand may have them).
            for (int tries = 0, made = 0, want = 3 + random.Next(3); tries < 160 && made < want; tries++)
            {
                int rock = start + random.Next(end - start); if (TwinRocks.Rocks[rock].kind == AquariumRockShapes.Pebbles) continue;
                Point foot; if (!TunnelTop(random, rock, .35f, .1f, out foot)) continue;
                float size = Range(random, .13f, .22f);
                if (nearCoral(foot, size) || !spacing.Free(foot, size, .5f)) continue;
                var n = TwinRocks.Normal(foot); foot.y -= .012f; made++;
                details.Add(new AquariumSceneDetail { position = foot, size = size, kind = 3, normal = n, yaw = (float)(random.NextDouble() * 360) });
            }
            int[] wantDetails = { 15 + random.Next(11), 4 + random.Next(4), 2 + random.Next(2) };
            for (int kind = 0; kind < 3; kind++)
                for (int tries = 0, made = 0; tries < wantDetails[kind] * 5 && made < wantDetails[kind]; tries++)
                {
                    int rock; var p = TunnelRockFoot(random, out rock);
                    float size = kind == 0 ? Range(random, .03f, .1f) : kind == 1 ? Range(random, .07f, .12f) : Range(random, .15f, .26f);
                    if (!TunnelSandClear(p, size, .1f, true)) continue;
                    if (kind == 2 && !spacing.Free(p, size, .5f)) continue;
                    p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .01f; made++;
                    details.Add(new AquariumSceneDetail { position = p, size = size, kind = kind, yaw = (float)(random.NextDouble() * 360) });
                }
        }
    }
}
