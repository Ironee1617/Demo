using System;
using System.Collections.Generic;

namespace PrototypeLab.Aquarium
{
    // R58: a coral colony of a game's scenery (foot on a rock top or the sand) and a bit of small sand dressing.
    [Serializable]
    public struct AquariumSceneCoral
    {
        public Point position;
        public float yaw, size;
        public int kind;
    }
    [Serializable]
    public struct AquariumSceneDetail
    {
        public Point position;
        public float yaw, size;
        // 0 pebble, 1 shell, 2 sea star on the sand, 3 sea star on a rock (R63).
        public int kind;
        // R63: the rock face's outward normal under a rock sea star (kind 3); unused otherwise.
        public Point normal;
    }

    // R58: the twin tanks' scenery, laid out anew for every game from the host's scenery seed (the same seed the
    // default tank shuffles with, AquariumSnapshot.scenerySeed), so every machine builds the same layout:
    //  * rock formations (arch, table, ledge, spire, pillar, split boulder) as big hiding spots, a few rock
    //    clusters and single boulders and pebble heaps (AquariumRockShapes, real collision through
    //    AquariumMonolithLayout), kept off the glass, the tunnel mouths, the dive water, the spawn points,
    //    the bubble vents and each other so open lanes stay for the chase;
    //  * two return drains per tank on the open sand that is left;
    //  * corals on the rock tops and on the sand, anemones (clownfish homes) at rock feet and on flat rock tops,
    //    seaweed, eelgrass meadows and grass tufts, and pebbles, shells and sea stars on the sand.
    // R61: bigger giants (scale 1.22 to 1.34, 9 to 11 m tall), three or four per game (still at most two per tank),
    // twice the rocks and clusters (boulder fields along the glass), half the formations tall, kelp forests,
    // coral gardens, more beds, grass, anemones and sand details, gathered in planting patches with open chase lanes
    // (two per tank) kept free of rocks and tall plants.
    // R59: first two or three giant landmarks (AquariumRockShapes.Stacks..Terrace, 6.5 to 9 m tall), at most two
    // per tank, each kept 3.6 m off the glass and well clear of the mouths, the dive water, spawns and vents;
    // the other rocks thin out round them and stay off their swim-throughs, and each giant is dressed as a reef
    // (corals, anemones and seaweed on its ledges, rubble, grass and seaweed round its foot). A few of the
    // smaller formations come larger too.
    public sealed partial class AquariumSceneryLayout
    {
        public AquariumRockSet TwinRocks { get; private set; } = AquariumRockSet.Empty;
        public AquariumSceneCoral[] Corals { get; private set; }
        public AquariumSceneDetail[] Details { get; private set; }

        // Grass tufts (form 5) are a twin-only seaweed form.
        public const int GrassForm = 5;

        private static AquariumSceneryLayout BuildTwin(int seed)
        {
            if (AquariumMaps.IsTutorial) return BuildTutorial(seed);   // R78b: the tutorial tank's fixed dressing (AquariumTutorialMap.cs)
            var layout = new AquariumSceneryLayout(seed, new float[AquariumMonolithLayout.Groups.Length], new float[0]);
            layout.Map = AquariumMaps.IsGrotto ? AquariumMaps.Grotto : AquariumMaps.Twin;   // R73: the grotto shares this
            int baseSeed = seed == 0 ? 5701 : seed;
            Random random = null, tunnelRandom = null;
            for (int attempt = 0; attempt < 4 && layout.DrainSites == null; attempt++)
            {
                random = new Random(attempt == 0 ? baseSeed : unchecked(baseSeed * 31 + attempt));
                var tankRocks = PlaceTwinRocks(random, layout.chaseLanes);
                // R69: then the tunnel's rocks (AquariumSceneryLayout.Tunnel.cs), from their own random so the
                // tanks lay out as before.
                tunnelRandom = new Random(unchecked(baseSeed * 7919 + 69));
                layout.TwinRocks = new AquariumRockSet(layout.PlaceTunnelRocks(tunnelRandom, tankRocks));
                using (Use(layout)) layout.DrainSites = AquariumTwinMap.PickDrains(random);
            }
            if (layout.DrainSites == null)
            {
                // Never expected: no room for the drains among the rocks, so this game has open tanks.
                layout.TwinRocks = AquariumRockSet.Empty; layout.TunnelRockStart = 0; layout.TunnelLaneSide = 0; tunnelRandom = null;
                using (Use(layout)) layout.DrainSites = AquariumTwinMap.PickDrains(new Random(baseSeed));
            }
            using (Use(layout)) layout.PlaceTwinLife(random, tunnelRandom);
            return layout;
        }

        // ---------------------------------------------------------------- rocks
        private static Point[] twinVents; private static readonly Point[][] twinSpawnsByMap = new Point[2][];   // R73: per twin-family map
        private static Point[] TwinSpawns()
        {
            int slot = AquariumMaps.IsGrotto ? 1 : 0; var twinSpawns = twinSpawnsByMap[slot];
            if (twinSpawns != null) return twinSpawns;
            var list = new List<Point>();
            for (int i = 0; i < 5; i++) list.Add(AquariumTwinMap.Layout(-9 + i * 2.4f, 2, 0));
            for (int i = 0; i < 8; i++) list.Add(AquariumTwinMap.Layout((i % 2 == 0 ? -7.5f : 7.5f) + (i / 2) * 2.2f - 1, 2, i % 3 - 1.5f));
            for (int i = 0; i < 2; i++) list.Add(AquariumTwinMap.Layout(10, 2, -4 + i * 2));
            return twinSpawnsByMap[slot] = list.ToArray();
        }
        // AquariumEnvironment.TwinAtmosphere's aeration vents.
        public static readonly Point[] TubeVents = { new Point(-27, 12.5f), new Point(-11, -13f), new Point(-21, 0), new Point(21, 11), new Point(27.5f, -5), new Point(17, -11.5f) };
        private static Point[] TwinVents()
        {
            // R73: the tube tank's vents stand clear of the tube.
            if (AquariumTwinMap.Single) return TubeVents;
            return twinVents ?? (twinVents = new[] { new Point(-35, 6), new Point(-22, -7), new Point(-30, 10), new Point(22, 7), new Point(35, -5), new Point(31, 12) });
        }
        // R73: the tube tank has no tunnel mouths (far away, so nothing keeps off them).
        private static Point MouthA { get { return AquariumTwinMap.Single ? new Point(999, 0, 999) : new Point(AquariumTwinMap.PlaneA, 0, 0); } }
        private static Point MouthB { get { return AquariumTwinMap.Single ? new Point(999, 0, 999) : new Point(AquariumTwinMap.PlaneB, 0, 0); } }

        // Room for a rock with this footprint at (x, z): inside a tank with a swim lane to the glass, clear of the
        // tunnel mouths, the dive water, the spawn points and the vents.
        private static bool RockRoom(float x, float z, float footprint, float glassGap)
        {
            if (AquariumTwinMap.Single ? AquariumTubeMap.Near(x, z, footprint + 1.2f) : Math.Abs(x) < AquariumTwinMap.PlaneB + 1) return false;   // R73: nothing under the tube
            if (AquariumTwinMap.Water(x, z) < footprint * .8f + glassGap) return false;
            var p = new Point(x, 0, z);
            if (Flat(p, MouthA) < footprint + 6.5f || Flat(p, MouthB) < footprint + 6.5f) return false;
            if (AquariumEntryLayout.KeepWaterClear(p, footprint + 2.2f)) return false;
            foreach (var s in TwinSpawns()) if (Flat(p, s) < footprint + SpawnRoom) return false;
            foreach (var v in TwinVents()) if (Flat(p, v) < footprint + 1.0f) return false;
            return true;
        }
        // R62: room kept round each spawn point (it is pushed clear of rock anyway).
        private const float SpawnRoom = 1.0f;
        private struct Placed { public Point p; public float footprint; public int cluster; }
        private const float Snug = .72f;
        private static bool Spaced(List<Placed> placed, Point p, float footprint, float gap, int cluster)
        {
            foreach (var q in placed)
            {
                float d = Flat(p, q.p);
                // R62: the footprint circle reaches the box's corners; rocks are spaced by about their real width.
                if (cluster >= 0 && q.cluster == cluster) { if (d < (footprint + q.footprint) * .55f * Snug) return false; }
                else if (d < (footprint + q.footprint) * Snug + gap) return false;
            }
            return true;
        }
        private static AquariumRock MakeRock(Random random, int kind, float x, float z, float scale)
        {
            // The foot sits a little under the lowest sand round it, so the base is buried on a slope.
            float f = AquariumRockShapes.Footprint(kind) * scale * .55f, low = AquariumSeabed.TwinHeight(x, z);
            for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; low = Math.Min(low, AquariumSeabed.TwinHeight(x + (float)Math.Cos(a) * f, z + (float)Math.Sin(a) * f)); }
            return new AquariumRock { kind = kind, variant = random.Next(AquariumRockShapes.Variants), position = new Point(x, low - .14f * scale, z),
                yaw = (float)(random.NextDouble() * 360), scale = scale, tint = (float)random.NextDouble() };
        }
        private static Point TankPoint(Random random, int tank)
        {
            // R73: the tube tank's halves are its west and east (one box).
            if (AquariumTwinMap.Single) return new Point((tank == 0 ? -AquariumTwinMap.TubeHalfX : 0) + (float)random.NextDouble() * AquariumTwinMap.TubeHalfX, 0, -AquariumTwinMap.TubeHalfZ + (float)random.NextDouble() * 2 * AquariumTwinMap.TubeHalfZ);
            return tank == 0 ? new Point(-50 + (float)random.NextDouble() * 38, 0, -23 + (float)random.NextDouble() * 46)
                : new Point(12 + (float)random.NextDouble() * 39, 0, -23 + (float)random.NextDouble() * 46);
        }
        private static float Range(Random random, float a, float b) { return a + (float)random.NextDouble() * (b - a); }

        // ---------------------------------------------------------------- R59: giants
        // A giant's floor samples in the world.
        private static Point[] GiantFloor(AquariumRock r)
        {
            var local = AquariumRockShapes.FootprintSamples(r.kind, r.variant); var world = new Point[local.Length];
            for (int i = 0; i < local.Length; i++) { var w = AquariumRockShapes.ToWorld(r, local[i]); world[i] = new Point(w.x, 0, w.z); }
            return world;
        }
        // Its swim-throughs as world segments (start, end), 5 m either side of the centre (into the niche only).
        private static void GiantPassages(AquariumRock r, List<Point> segments)
        {
            var centres = new List<Point>(); var directions = new List<Point>(); var through = new List<bool>();
            AquariumRockShapes.Passages(r.kind, r.variant, centres, directions, through);
            for (int i = 0; i < centres.Count; i++)
            {
                var c = AquariumRockShapes.ToWorld(r, centres[i]); var d = AquariumRockShapes.DirectionToWorld(r, directions[i]);
                c.y = 0; d.y = 0;
                segments.Add(through[i] ? c - d * (5 * r.scale) : c); segments.Add(c + d * (5 * r.scale));
            }
        }
        private static float SegmentDistance(Point p, Point a, Point b)
        {
            float dx = b.x - a.x, dz = b.z - a.z, l2 = dx * dx + dz * dz;
            float t = l2 < 1e-6f ? 0 : Math.Max(0, Math.Min(1, ((p.x - a.x) * dx + (p.z - a.z) * dz) / l2));
            float x = p.x - (a.x + dx * t), z = p.z - (a.z + dz * t); return (float)Math.Sqrt(x * x + z * z);
        }
        // Clear of every giant by gap (floor samples) and of their swim-throughs by lane.
        private static bool GiantClear(List<Point[]> floors, List<Point> passages, Point p, float footprint, float gap, float lane)
        {
            foreach (var floor in floors) foreach (var q in floor) if (Flat(p, q) < footprint + gap) return false;
            for (int i = 0; i + 1 < passages.Count; i += 2) if (SegmentDistance(p, passages[i], passages[i + 1]) < footprint + lane) return false;
            return true;
        }
        // R62: a wall rock's box (at its yaw and scale, foot at p) stays inside the water by 0.2 m.
        private static bool WallBoxClear(int kind, float scale, float yaw, Point p)
        {
            var lo = AquariumRockShapes.LocalMin[kind]; var hi = AquariumRockShapes.LocalMax[kind];
            var r = new AquariumRock { kind = kind, position = p, yaw = yaw, scale = scale };
            for (int c = 0; c < 4; c++)
            {
                var w = AquariumRockShapes.ToWorld(r, new Point((c & 1) == 0 ? lo.x : hi.x, 0, (c & 2) == 0 ? lo.z : hi.z));
                if (AquariumTwinMap.Water(w.x, w.z) < .2f) return false;
            }
            return true;
        }
        private static bool GiantFloorRoom(Point[] floor)
        {
            foreach (var p in floor)
            {
                if (AquariumTwinMap.Single ? AquariumTubeMap.Near(p, 2.5f) : Math.Abs(p.x) < AquariumTwinMap.PlaneB + 2.5f) return false;
                if (AquariumTwinMap.Water(p.x, p.z) < 2.9f) return false;
                if (Flat(p, MouthA) < 7.8f || Flat(p, MouthB) < 7.8f) return false;
                if (AquariumEntryLayout.KeepWaterClear(p, 2.6f)) return false;
            }
            return true;
        }
        private static bool GiantOpenings(AquariumRock rock)
        {
            // Spawn points keep a metre of open water round them (under an arch or beside a wall is fine), and
            // the vents' bubble columns stay open up to the surface.
            Func<Point, float> field = q => AquariumRockShapes.Local(rock.kind, rock.variant, AquariumRockShapes.ToLocal(rock, q)) * rock.scale;
            foreach (var s in TwinSpawns()) if (field(s) < .75f) return false; // R61: the spawn is pushed clear of rock anyway; only never inside
            foreach (var v in TwinVents())
            {
                float ground = AquariumSeabed.TwinHeight(v.x, v.z);
                for (float h = .4f; h < AquariumModel.WaterHeight; h += 1f) if (field(new Point(v.x, ground + h, v.z)) < .7f) return false;
            }
            return true;
        }
        private static readonly int[] GiantKinds = { AquariumRockShapes.Stacks, AquariumRockShapes.GreatArch, AquariumRockShapes.Mesa, AquariumRockShapes.Ridge, AquariumRockShapes.Cleft, AquariumRockShapes.Terrace };
        // Highest a giant may reach: R61 a swim gap of 1.15 m stays under the water surface (the diver is 1 m across).
        public const float GiantCeiling = AquariumModel.WaterHeight - 1.15f;
        // The foot goes .35 m under the lowest sand under the whole giant, so no edge of it floats; a giant that
        // would reach above the ceiling is shrunk to it.
        private static void SettleGiant(ref AquariumRock r, Point[] floor)
        {
            float low = AquariumSeabed.TwinHeight(r.position.x, r.position.z);
            foreach (var q in floor) low = Math.Min(low, AquariumSeabed.TwinHeight(q.x, q.z));
            r.position.y = low - .35f;
            float top = r.position.y + AquariumRockShapes.LocalMax[r.kind].y * r.scale;
            if (top > GiantCeiling) r.scale = r.scale * (GiantCeiling - r.position.y) / (top - r.position.y);
        }
        // R61: scale bands tried in turn: the full landmark size first, then a little smaller where it will not fit.
        private static readonly float[] GiantBandLow = { 1.22f, 1.08f, .95f }, GiantBandHigh = { 1.34f, 1.2f, 1.06f };
        private static bool TryGiant(Random random, int kind, int tank, int band, int attempts, List<Point[]> floors, out AquariumRock rock, out Point[] floor)
        {
            for (int tries = 0; tries < attempts; tries++)
            {
                var p = TankPoint(random, tank);
                float yaw = (float)(random.NextDouble() * 360), scale = Range(random, GiantBandLow[band], GiantBandHigh[band]);
                rock = new AquariumRock { kind = kind, variant = random.Next(AquariumRockShapes.Variants), position = new Point(p.x, 0, p.z), yaw = yaw, scale = scale, tint = (float)random.NextDouble() };
                floor = GiantFloor(rock);
                if (!GiantFloorRoom(floor)) continue;
                bool apart = true;
                foreach (var other in floors) { foreach (var a in other) { foreach (var b in floor) if (Flat(a, b) < 4.6f) { apart = false; break; } if (!apart) break; } if (!apart) break; }
                if (!apart) continue;
                SettleGiant(ref rock, floor);
                if (!GiantOpenings(rock)) continue;
                return true;
            }
            rock = new AquariumRock(); floor = null; return false;
        }

        // R61: half width of the open chase lanes.
        public const float LaneHalfWidth = 1.7f;
        private static AquariumRock[] PlaceTwinRocks(Random random, List<Point> lanes)
        {
            var rocks = new List<AquariumRock>(); var placed = new List<Placed>();
            var floors = new List<Point[]>(); var passages = new List<Point>();
            // R59: two or three giants (at most two per tank, one in each tank at least), different kinds.
            int giantWant = random.NextDouble() < .4 ? 4 : 3;
            var giantKinds = new List<int>(GiantKinds);
            for (int i = giantKinds.Count - 1; i > 0; i--) { int j = random.Next(i + 1); int t = giantKinds[i]; giantKinds[i] = giantKinds[j]; giantKinds[j] = t; }
            int first = random.Next(2); var giantTanks = new[] { first, 1 - first, random.Next(2), 1 - first };
            var perTank = new int[2];
            int kindIndex = 0;
            for (int slot = 0; slot < giantWant; slot++)
            {
                bool done = false;
                // The slot's tank first (one giant in each tank before a second anywhere); when no spot is
                // found the next kind is tried (the smaller ones fit where the long ones do not).
                for (int band = 0; band < (slot < 2 ? 2 : 3) && !done; band++)
                for (int k = kindIndex; k < giantKinds.Count && !done; k++)
                    for (int pass = 0; pass < 2 && !done; pass++)
                    {
                        int tank = pass == 0 ? giantTanks[slot] : 1 - giantTanks[slot];
                        if (perTank[tank] >= 2 || (pass == 1 && slot < 2)) continue;
                        AquariumRock rock; Point[] floor;
                        if (!TryGiant(random, giantKinds[k], tank, band, slot < 2 ? 140 : 70, floors, out rock, out floor)) continue;
                        rocks.Add(rock); floors.Add(floor); GiantPassages(rock, passages); perTank[tank]++; done = true;
                        int t = giantKinds[k]; giantKinds[k] = giantKinds[kindIndex]; giantKinds[kindIndex] = t; kindIndex++;
                    }
            }
            int giants = rocks.Count;
            // R61: open chase lanes, two per tank through its middle at seeded angles; rocks and tall plants keep off.
            lanes.Clear();
            for (int tank = 0; tank < 2; tank++)
            {
                var centre = tank == 0 ? AquariumTwinMap.CentreA : AquariumTwinMap.CentreB;
                double a0 = random.NextDouble() * Math.PI;
                // R62: one lane per tank (with the open spawn band across its middle that makes two corridors).
                for (int l = 0; l < 1; l++)
                {
                    double a = a0 + l * (Math.PI * .5 + (random.NextDouble() - .5) * .6);
                    var dir = new Point((float)Math.Cos(a), 0, (float)Math.Sin(a));
                    lanes.Add(centre - dir * 26); lanes.Add(centre + dir * 26);
                }
            }
            Func<Point, float, bool> offLane = (p, footprint) =>
            {
                for (int i = 0; i + 1 < lanes.Count; i += 2) if (SegmentDistance(p, lanes[i], lanes[i + 1]) < footprint * .45f + LaneHalfWidth) return false;
                return true;
            };
            RockStage(0, rocks.Count);
            // R62: a rocky slope along the glass of both tanks: overlapping medium and large boulders, ledges and
            // pillars (a third of them 4 to 7.5 m tall) lining the walls, broken where the chase lanes, the tunnel
            // mouths, the dive water, the spawns, the vents and the giants need the room.
            {
                int[] wallKinds = { AquariumRockShapes.Worn, AquariumRockShapes.Worn, AquariumRockShapes.Split, AquariumRockShapes.Split, AquariumRockShapes.Ledge, AquariumRockShapes.Pillar, AquariumRockShapes.Table, AquariumRockShapes.Spire };
                const int Steps = 150;
                for (int i = 0; i < Steps; i++)
                {
                    double roll = random.NextDouble();
                    int kind = wallKinds[random.Next(wallKinds.Length)];
                    float scale = Range(random, 1.0f, 1.75f);
                    if (random.NextDouble() < .33) scale = Math.Min(Range(random, 1.8f, 2.5f), 7.5f / AquariumRockShapes.Height(kind));
                    float yaw = (float)(random.NextDouble() * 360);
                    if (roll < .15) continue;
                    var edge = AquariumTwinMap.PointOn((i + .5f) / Steps, false);
                    if (AquariumTwinMap.Single ? AquariumTubeMap.Near(edge, 4f) : Math.Abs(edge.x) < AquariumTwinMap.PlaneB + 2) continue;
                    var inward = AquariumTwinMap.Outward(edge) * -1f;
                    float footprint = AquariumRockShapes.Footprint(kind) * scale;
                    // In from the glass until the rock's box keeps clear of it.
                    Point p = new Point(); bool fits = false;
                    for (float d = footprint * .35f; d < footprint * 1.3f + 1 && !fits; d += .25f)
                    {
                        p = edge + inward * d; p.y = 0;
                        fits = WallBoxClear(kind, scale, yaw, p);
                    }
                    if (!fits) { WallFail(0); continue; }
                    if (Flat(p, MouthA) < footprint + 7.5f || Flat(p, MouthB) < footprint + 7.5f) { WallFail(1); continue; }
                    if (AquariumEntryLayout.KeepWaterClear(p, footprint + 2.2f)) { WallFail(2); continue; }
                    bool blocked = false;
                    foreach (var sp in TwinSpawns()) if (Flat(p, sp) < footprint + 1.6f) { blocked = true; break; }
                    foreach (var v in TwinVents()) if (Flat(p, v) < footprint + 1.0f) { blocked = true; break; }
                    if (blocked) { WallFail(3); continue; }
                    if (!Spaced(placed, p, footprint, .8f, 900)) { WallFail(4); continue; }
                    // Wall rocks may lean on a giant standing near the glass (only its swim-throughs stay open).
                    if (!GiantClear(floors, passages, p, footprint, -footprint * .4f, 2.4f)) { WallFail(5); continue; }
                    if (!offLane(p, footprint)) { WallFail(6); continue; }
                    var rock = MakeRock(random, kind, p.x, p.z, scale); rock.yaw = yaw;
                    rocks.Add(rock); placed.Add(new Placed { p = p, footprint = footprint, cluster = 900 });
                    // Often a lower boulder in front of it, so the slope runs two deep.
                    if (random.NextDouble() < .55)
                    {
                        int front = random.NextDouble() < .5 ? AquariumRockShapes.Worn : random.NextDouble() < .5 ? AquariumRockShapes.Split : AquariumRockShapes.Pebbles;
                        float fs = Range(random, .7f, 1.25f), ff = AquariumRockShapes.Footprint(front) * fs;
                        var q = p + inward * (footprint * .8f + ff * .6f); q.y = 0;
                        if (Flat(q, MouthA) < ff + 7.5f || Flat(q, MouthB) < ff + 7.5f || AquariumEntryLayout.KeepWaterClear(q, ff + 2.2f)) continue;
                        bool near = false;
                        foreach (var sp in TwinSpawns()) if (Flat(q, sp) < ff + 1.6f) { near = true; break; }
                        foreach (var v in TwinVents()) if (Flat(q, v) < ff + 1.0f) { near = true; break; }
                        if (near || !Spaced(placed, q, ff, .5f, 900) || !GiantClear(floors, passages, q, ff, -ff * .4f, 2.4f) || !offLane(q, ff)) continue;
                        rocks.Add(MakeRock(random, front, q.x, q.z, fs)); placed.Add(new Placed { p = q, footprint = ff, cluster = 900 });
                    }
                }
            }
            RockStage(1, rocks.Count);
            // R62: rock ridges: short lines of four to six overlapping boulders across the floor, two per tank.
            for (int r = 0; r < 6; r++)
            {
                int tank = r % 2;
                for (int tries = 0; tries < 120; tries++)
                {
                    var start = TankPoint(random, tank);
                    double a = random.NextDouble() * Math.PI; var dir = new Point((float)Math.Cos(a), 0, (float)Math.Sin(a));
                    int count = 4 + random.Next(3), made = 0;
                    if (!RockRoom(start.x, start.z, 2.0f, 2.0f) || !Spaced(placed, start, 2.0f, .8f, -1) || !offLane(start, 2.0f)) continue;
                    for (int m = 0; m < count; m++)
                    {
                        int kind = random.NextDouble() < .5 ? AquariumRockShapes.Worn : random.NextDouble() < .5 ? AquariumRockShapes.Split : AquariumRockShapes.Pillar;
                        float scale = Range(random, .9f, 1.5f), footprint = AquariumRockShapes.Footprint(kind) * scale;
                        var p = start + dir * (m * Range(random, 1.9f, 2.5f)) + new Point(-dir.z, 0, dir.x) * Range(random, -.6f, .6f); p.y = 0;
                        if (!RockRoom(p.x, p.z, footprint, 1.8f) || !Spaced(placed, p, footprint, .8f, 800 + r) || !GiantClear(floors, passages, p, footprint, 1.8f, 2.4f) || !offLane(p, footprint)) break;
                        rocks.Add(MakeRock(random, kind, p.x, p.z, scale)); placed.Add(new Placed { p = p, footprint = footprint, cluster = 800 + r }); made++;
                    }
                    break;
                }
            }
            RockStage(2, rocks.Count);
            // Rubble at each giant's foot: R62 six to ten boulders, ledges and pebble heaps piled against it (off its
            // swim-throughs), some of them big.
            for (int g = 0; g < giants; g++)
            {
                var giant = rocks[g]; int want = 6 + random.Next(5), made = 0;
                for (int tries = 0; tries < 130 && made < want; tries++)
                {
                    int kind = random.NextDouble() < .45 ? AquariumRockShapes.Worn : random.NextDouble() < .4 ? AquariumRockShapes.Pebbles : random.NextDouble() < .6 ? AquariumRockShapes.Split : AquariumRockShapes.Ledge;
                    float scale = Range(random, .6f, 1.4f), footprint = AquariumRockShapes.Footprint(kind) * scale;
                    Point edge; if (!GiantEdge(random, giant, footprint * .45f, footprint * .55f + .6f, out edge)) continue;
                    if (!RockRoom(edge.x, edge.z, footprint, 1.6f) || !Spaced(placed, edge, footprint, .9f, -1)) continue;
                    if (!GiantClear(new List<Point[]>(), passages, edge, footprint, 0, 2.2f)) continue;
                    rocks.Add(MakeRock(random, kind, edge.x, edge.z, scale)); placed.Add(new Placed { p = edge, footprint = footprint, cluster = -1 }); made++;
                }
            }
            RockStage(3, rocks.Count);
            // Formations: the big shapes spread over the two tanks (fewer where giants stand); R61 half of them come
            // tall (up to 7.2 m) for cover at mid height.
            var big = new List<int> { AquariumRockShapes.Arch, AquariumRockShapes.Table, AquariumRockShapes.Ledge, AquariumRockShapes.Spire, AquariumRockShapes.Pillar, AquariumRockShapes.Split };
            for (int i = big.Count - 1; i > 0; i--) { int j = random.Next(i + 1); int t = big[i]; big[i] = big[j]; big[j] = t; }
            int[] extra = { AquariumRockShapes.Arch, AquariumRockShapes.Table, AquariumRockShapes.Ledge, AquariumRockShapes.Pillar, AquariumRockShapes.Split, AquariumRockShapes.Worn, AquariumRockShapes.Spire };
            for (int i = 0; i < 8; i++) big.Add(extra[random.Next(extra.Length)]);
            // R62: up to seven per tank (fewer where giants stand), alternating tanks.
            int inA = Math.Max(4, 7 - perTank[0]), inB = Math.Max(4, 7 - perTank[1]), madeA = 0, madeB = 0;
            for (int n = 0; n < big.Count; n++)
            {
                int kind = big[n], tank = n % 2;
                if (tank == 0 ? madeA >= inA : madeB >= inB) continue;
                if (tank == 0) madeA++; else madeB++;
                float scale = kind == AquariumRockShapes.Arch ? Range(random, 1.5f, 1.75f) : kind == AquariumRockShapes.Spire ? Range(random, 1.3f, 1.6f) : Range(random, 1.3f, 1.65f);
                if (random.NextDouble() < .55) scale = Math.Max(scale, Math.Min(Range(random, 1.85f, 2.5f), 8f / AquariumRockShapes.Height(kind)));
                // R62: tall, but not so wide that nothing fits (the narrow pillars and spires go tallest).
                scale = Math.Min(scale, Math.Max(1.3f, 4.4f / AquariumRockShapes.Footprint(kind)));
                float footprint = AquariumRockShapes.Footprint(kind) * scale;
                for (int tries = 0; tries < 300; tries++)
                {
                    var p = TankPoint(random, tank);
                    if (!RockRoom(p.x, p.z, footprint, 2.4f) || !Spaced(placed, p, footprint, 1.0f, -1) || !GiantClear(floors, passages, p, footprint, 2.4f, 2.6f) || !offLane(p, footprint)) continue;
                    rocks.Add(MakeRock(random, kind, p.x, p.z, scale)); placed.Add(new Placed { p = p, footprint = footprint, cluster = -1 }); break;
                }
            }
            RockStage(4, rocks.Count);
            // Clusters: outcrops of three to five rocks that read as one massif; R61 twice as many, a good part of them
            // as boulder fields along the glass.
            int[] clusterKinds = { AquariumRockShapes.Worn, AquariumRockShapes.Pebbles, AquariumRockShapes.Split, AquariumRockShapes.Pillar, AquariumRockShapes.Worn, AquariumRockShapes.Spire };
            for (int c = 0; c < 28 - giants; c++)
            {
                int tank = c % 2; bool wall = c % 5 < 2;
                for (int tries = 0; tries < 220; tries++)
                {
                    Point centre;
                    if (wall)
                    {
                        var edge = AquariumTwinMap.PointOn((float)random.NextDouble(), false);
                        if ((edge.x < 0 ? 0 : 1) != tank) continue;
                        centre = edge - AquariumTwinMap.Outward(edge) * Range(random, 3.2f, 5.5f); centre.y = 0;
                    }
                    else centre = TankPoint(random, tank);
                    if (!RockRoom(centre.x, centre.z, 2.6f, 1.8f) || !Spaced(placed, centre, 2.4f, .8f, -1) || !GiantClear(floors, passages, centre, 2.4f, 2.0f, 2.4f) || !offLane(centre, 2.5f)) continue;
                    int members = 3 + random.Next(4), made = 0;
                    for (int m = 0; m < members * 10 && made < members; m++)
                    {
                        int kind = clusterKinds[random.Next(clusterKinds.Length)];
                        float scale = kind == AquariumRockShapes.Pillar || kind == AquariumRockShapes.Spire ? Range(random, .8f, 1.5f) : Range(random, .8f, 1.6f);
                        float footprint = AquariumRockShapes.Footprint(kind) * scale;
                        double a = random.NextDouble() * Math.PI * 2; float reach = made == 0 ? 0 : Range(random, 1.4f, 3.4f);
                        var p = new Point(centre.x + (float)Math.Cos(a) * reach, 0, centre.z + (float)Math.Sin(a) * reach);
                        if (!RockRoom(p.x, p.z, footprint, 1.6f) || !Spaced(placed, p, footprint, .8f, 100 + c) || !GiantClear(floors, passages, p, footprint, 1.8f, 2.4f) || !offLane(p, footprint)) continue;
                        rocks.Add(MakeRock(random, kind, p.x, p.z, scale)); placed.Add(new Placed { p = p, footprint = footprint, cluster = 100 + c }); made++;
                    }
                    break;
                }
            }
            RockStage(5, rocks.Count);
            // R61: rubble fields: a scatter of four to eight small stones and pebble heaps (knee high) in a few places.
            for (int f = 0; f < 8; f++)
            {
                int tank = f % 2;
                for (int tries = 0; tries < 80; tries++)
                {
                    var centre = TankPoint(random, tank);
                    if (!RockRoom(centre.x, centre.z, 1.5f, 1.4f) || !GiantClear(floors, passages, centre, 1.5f, 1.5f, 2.2f) || !offLane(centre, 1.5f)) continue;
                    for (int m = 0, made = 0, want = 4 + random.Next(5); m < want * 8 && made < want; m++)
                    {
                        int kind = random.NextDouble() < .55 ? AquariumRockShapes.Pebbles : AquariumRockShapes.Worn;
                        float scale = Range(random, .3f, .6f), footprint = AquariumRockShapes.Footprint(kind) * scale;
                        double a = random.NextDouble() * Math.PI * 2; float reach = Range(random, .3f, 3.4f);
                        var p = new Point(centre.x + (float)Math.Cos(a) * reach, 0, centre.z + (float)Math.Sin(a) * reach);
                        if (!RockRoom(p.x, p.z, footprint, 1.3f) || !Spaced(placed, p, footprint, .5f, -1) || !GiantClear(floors, passages, p, footprint, 1.2f, 2.2f) || !offLane(p, footprint)) continue;
                        rocks.Add(MakeRock(random, kind, p.x, p.z, scale)); placed.Add(new Placed { p = p, footprint = footprint, cluster = -1 }); made++;
                    }
                    break;
                }
            }
            RockStage(6, rocks.Count);
            // Single boulders and pebble heaps between them (R61: twice as many).
            int[] singleKinds = { AquariumRockShapes.Worn, AquariumRockShapes.Split, AquariumRockShapes.Worn, AquariumRockShapes.Pebbles, AquariumRockShapes.Split, AquariumRockShapes.Ledge };
            for (int s = 0; s < 46 - 2 * giants; s++)
            {
                int tank = s % 2;
                int kind = singleKinds[random.Next(singleKinds.Length)];
                float scale = Range(random, .6f, 1.3f), footprint = AquariumRockShapes.Footprint(kind) * scale;
                for (int tries = 0; tries < 220; tries++)
                {
                    var p = TankPoint(random, tank);
                    if (!RockRoom(p.x, p.z, footprint, 1.7f) || !Spaced(placed, p, footprint, .7f, -1) || !GiantClear(floors, passages, p, footprint, 2.0f, 2.2f) || !offLane(p, footprint)) continue;
                    rocks.Add(MakeRock(random, kind, p.x, p.z, scale)); placed.Add(new Placed { p = p, footprint = footprint, cluster = -1 }); break;
                }
            }
            RockStage(9, rocks.Count);
            return rocks.ToArray();
        }
        // QA: rock counts after each placing stage of the last layout (AquariumR59QA reads them).
        [ThreadStatic] private static System.Text.StringBuilder rockStages;
        public static string RockStages { get { return rockStages == null ? "" : rockStages.ToString(); } }
        private static void WallFail(int reason) { if (rockStages == null) rockStages = new System.Text.StringBuilder(); rockStages.Append('w').Append(reason).Append(' '); }
        private static void RockStage(int stage, int count)
        {
            if (rockStages == null) rockStages = new System.Text.StringBuilder();
            if (stage == 0) { var keep = rockStages.ToString(); rockStages.Length = 0; }
            rockStages.Append(stage).Append(':').Append(count).Append(' ');
        }
        // A sand point just off a giant's foot: in from far along a random heading until the rock is met, then
        // out again by out1..out2 metres.
        private static bool GiantEdge(Random random, AquariumRock g, float out1, float out2, out Point edge)
        {
            edge = new Point();
            double a = random.NextDouble() * Math.PI * 2; var dir = new Point((float)Math.Cos(a), 0, (float)Math.Sin(a));
            var lo = AquariumRockShapes.LocalMin[g.kind]; var hi = AquariumRockShapes.LocalMax[g.kind];
            var mid = AquariumRockShapes.ToWorld(g, (lo + hi) * .5f); mid.y = 0;
            float start = (hi - lo).Length * .5f * g.scale + 1, outside = start;
            // Traced in by the field (big steps in open water), then pinned to 5 cm.
            for (float r = start; r > 0; )
            {
                var q = mid + dir * r; float ground = AquariumSeabed.TwinHeight(q.x, q.z);
                float d = AquariumRockShapes.Local(g.kind, g.variant, AquariumRockShapes.ToLocal(g, new Point(q.x, ground + .45f, q.z))) * g.scale;
                if (d < 0)
                {
                    float inside = r;
                    for (int i = 0; i < 5; i++)
                    {
                        float m = (inside + outside) * .5f; var w = mid + dir * m; float h = AquariumSeabed.TwinHeight(w.x, w.z);
                        if (AquariumRockShapes.Local(g.kind, g.variant, AquariumRockShapes.ToLocal(g, new Point(w.x, h + .45f, w.z))) < 0) inside = m; else outside = m;
                    }
                    var p = mid + dir * (outside + Range(random, out1, out2)); edge = new Point(p.x, 0, p.z); return true;
                }
                outside = r; r -= Math.Max(.2f, d * .75f);
            }
            return false;
        }

        // ---------------------------------------------------------------- life
        private bool TwinSandClear(Point p, float radius, float height, bool tunnelOk)
        {
            if (AquariumTwinMap.Water(p.x, p.z) < radius + .35f) return false;
            if (AquariumTwinMap.Single) { if (AquariumTubeMap.Near(p, radius + .3f) && AquariumSeabed.TwinHeight(p.x, p.z) + height > AquariumTubeMap.AxisY - AquariumTubeMap.Radius - .5f) return false; }
            else if (!tunnelOk && Math.Abs(p.x) < AquariumTwinMap.PlaneB + .8f) return false;
            if (height > .9f && (Flat(p, MouthA) < 3.6f + radius || Flat(p, MouthB) < 3.6f + radius)) return false;
            if (AquariumEntryLayout.KeepWaterClear(p, radius + .3f)) return false;
            float ground = AquariumSeabed.TwinHeight(p.x, p.z);
            for (float h = .15f; h < height + .3f; h += .5f)
                if (TwinRocks.Overlaps(new Point(p.x, ground + h, p.z), radius * .75f + .12f)) return false;
            if (DrainSites != null) foreach (var d in DrainSites) if (Flat(p, d) < AquariumDrainLayout.ClearanceRadius + radius * .6f + .2f) return false;
            foreach (var v in TwinVents()) if (Flat(p, v) < .9f) return false;
            return true;
        }
        // A random sand point of one tank: along the glass, round a rock's foot, or anywhere.
        private Point TwinSandCandidate(Random random, int tank)
        {
            double roll = random.NextDouble();
            if (roll < .34)
            {
                for (int tries = 0; tries < 8; tries++)
                {
                    var edge = AquariumTwinMap.PointOn((float)random.NextDouble(), false);
                    if ((edge.x < 0 ? 0 : 1) != tank) continue;
                    var inward = AquariumTwinMap.Outward(edge) * -1f;
                    return edge + inward * Range(random, .6f, 3.4f);
                }
            }
            else if (roll < .66 && TwinRocks.Count > 0)
            {
                for (int tries = 0; tries < 8; tries++)
                {
                    var r = TwinRocks.Rocks[random.Next(TwinRocks.Count)];
                    if ((r.position.x < 0 ? 0 : 1) != tank) continue;
                    double a = random.NextDouble() * Math.PI * 2;
                    float reach = AquariumRockShapes.Footprint(r.kind) * r.scale * Range(random, .55f, 1.05f) + Range(random, .1f, 1.6f);
                    return new Point(r.position.x + (float)Math.Cos(a) * reach, 0, r.position.z + (float)Math.Sin(a) * reach);
                }
            }
            return TankPoint(random, tank);
        }
        // A flat top of a rock (world foot), scanning that rock's own field down a random column.
        private bool RockTop(Random random, int rock, float minUp, float maxHeight, out Point foot)
        {
            foot = new Point();
            var r = TwinRocks.Rocks[rock]; var lo = AquariumRockShapes.LocalMin[r.kind]; var hi = AquariumRockShapes.LocalMax[r.kind];
            var local = new Point(Range(random, lo.x, hi.x) * .9f, hi.y, Range(random, lo.z, hi.z) * .9f);
            var top = AquariumRockShapes.ToWorld(r, local); float bottom = AquariumSeabed.TwinHeight(top.x, top.z) + .25f;
            float y = top.y;
            if (!AquariumRockShapes.IsGiant(r.kind))
                for (; y > bottom; y -= .05f) { if (TwinRocks.RockDistance(rock, new Point(top.x, y, top.z)) < 0) break; }
            else
            {
                // R59: the tall giants are traced down by their own field (big steps in open water), then the
                // surface is pinned to a centimetre.
                float above = y;
                for (; y > bottom; )
                {
                    float d = TwinRocks.RockDistance(rock, new Point(top.x, y, top.z));
                    if (d < 0) break;
                    above = y; y -= Math.Max(.05f, d * .7f);
                }
                if (y > bottom)
                    for (int i = 0; i < 6; i++) { float m = (y + above) * .5f; if (TwinRocks.RockDistance(rock, new Point(top.x, m, top.z)) < 0) y = m; else above = m; }
            }
            if (y <= bottom || y > maxHeight) return false;
            var p = new Point(top.x, y + .01f, top.z);
            if (TwinRocks.Normal(p).y < minUp) return false;
            // Open water above it (not under another bed of rock).
            for (float h = .3f; h < 1.1f; h += .3f) if (TwinRocks.Distance(new Point(p.x, p.y + h, p.z)) < .1f) return false;
            if (AquariumTwinMap.Water(p.x, p.z) < .6f) return false;
            foot = p; return true;
        }

        // R59: off the giants' swim-throughs (horizontal distance to their lanes).
        private List<Point> giantLanes = new List<Point>();
        // R61: the open chase lanes (segment pairs) and the dense planting patches of this layout.
        private List<Point> chaseLanes = new List<Point>(), patches = new List<Point>();
        private bool OffChase(Point p, float radius)
        {
            for (int i = 0; i + 1 < chaseLanes.Count; i += 2) if (SegmentDistance(p, chaseLanes[i], chaseLanes[i + 1]) < LaneHalfWidth - .4f + radius) return false;
            return true;
        }
        // A sand point of one tank, most often inside one of its planting patches (dense beds with open sand between).
        private Point PatchCandidate(Random random, int tank)
        {
            if (patches.Count > 0 && random.NextDouble() < .6)
            {
                var c = patches[random.Next(patches.Count)];
                if ((c.x < 0 ? 0 : 1) == tank)
                {
                    double a = random.NextDouble() * Math.PI * 2; float reach = 4.8f * (float)Math.Sqrt(random.NextDouble());
                    return new Point(c.x + (float)Math.Cos(a) * reach, 0, c.z + (float)Math.Sin(a) * reach);
                }
            }
            return TwinSandCandidate(random, tank);
        }
        private bool OffLanes(Point p, float radius)
        {
            for (int i = 0; i + 1 < giantLanes.Count; i += 2) if (SegmentDistance(p, giantLanes[i], giantLanes[i + 1]) < 1.8f + radius) return false;
            return true;
        }
        // QA: section times of the last life pass (AquariumR59QA reads them).
        private static readonly System.Diagnostics.Stopwatch lapWatch = new System.Diagnostics.Stopwatch();
        private static readonly System.Text.StringBuilder lapLog = new System.Text.StringBuilder();
        public static string LifeTimings { get { return lapLog.ToString(); } }
        private static void Lap(int section) { lapLog.Append(section).Append(':').Append(lapWatch.ElapsedMilliseconds).Append(' '); }
        private void PlaceTwinLife(Random random, Random tunnelRandom)
        {
            var spacing = new Spacing(); var anemones = new List<AquariumSceneAnemone>(); var plants = new List<AquariumScenePlant>();
            var corals = new List<AquariumSceneCoral>(); var details = new List<AquariumSceneDetail>();
            int rockCount = TwinRocks.Count;
            lapWatch.Reset(); lapWatch.Start(); lapLog.Length = 0;
            var giants = new List<int>(); giantLanes = new List<Point>();
            // R61: eight planting patches per tank, off the chase lanes.
            patches = new List<Point>();
            for (int tank = 0; tank < 2; tank++)
                for (int tries = 0, made = 0; tries < 200 && made < 8; tries++)
                {
                    var c = TankPoint(random, tank);
                    if (AquariumTwinMap.Water(c.x, c.z) < 2.5f || !OffChase(c, 2.5f)) continue;
                    bool near = false; foreach (var q in patches) if (Flat(q, c) < 7) { near = true; break; }
                    if (near) continue;
                    patches.Add(c); made++;
                }
            for (int i = 0; i < rockCount; i++) if (AquariumRockShapes.IsGiant(TwinRocks.Rocks[i].kind)) { giants.Add(i); GiantPassages(TwinRocks.Rocks[i], giantLanes); }
            Lap(0);
            // R59: every giant reads as a reef: one or two anemones on its ledges first (clownfish homes up high).
            foreach (int g in giants)
            {
                int made = 0, want = 1 + random.Next(2);
                for (int tries = 0; tries < 160 && made < want; tries++)
                {
                    Point foot; if (!RockTop(random, g, .84f, GiantCeiling, out foot)) continue;
                    float size = .3f + (float)random.NextDouble() * .14f, r = AnemoneSpread(size);
                    if (!spacing.Free(foot, r, 1.15f)) continue;
                    spacing.Add(foot, r); made++;
                    anemones.Add(new AquariumSceneAnemone { position = foot, size = size, kind = random.Next(2), yaw = (float)(random.NextDouble() * 360), ledge = true });
                }
            }
            // Anemones first (the clownfish homes): flat rock tops, then sand round the rock feet, both tanks alike.
            for (int tries = 0; tries < 900 && anemones.Count < 11 && rockCount > 0; tries++)
            {
                int rock = random.Next(rockCount); int kind = TwinRocks.Rocks[rock].kind;
                if (kind == AquariumRockShapes.Pebbles || kind == AquariumRockShapes.Spire) continue;
                Point foot; if (!RockTop(random, rock, .82f, 4.6f, out foot)) continue;
                float size = .27f + (float)random.NextDouble() * .13f, r = AnemoneSpread(size);
                if (!spacing.Free(foot, r, 1.15f)) continue;
                spacing.Add(foot, r);
                anemones.Add(new AquariumSceneAnemone { position = foot, size = size, kind = random.Next(2), yaw = (float)(random.NextDouble() * 360), ledge = true });
            }
            for (int tries = 0; tries < 4000 && anemones.Count < 30; tries++)
            {
                var p = PatchCandidate(random, tries % 2);
                float size = .36f + (float)random.NextDouble() * .12f, r = AnemoneSpread(size);
                if (!TwinSandClear(p, r, size * 1.2f, false) || !OffLanes(p, r)) continue;
                p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .02f;
                if (!spacing.Free(p, r, 1.15f)) continue;
                spacing.Add(p, r);
                anemones.Add(new AquariumSceneAnemone { position = p, size = size, kind = random.Next(2), yaw = (float)(random.NextDouble() * 360) });
            }
            Lap(1);
            // Coral colonies on the rocks (more on the big formations) and coral beds on the sand.
            for (int rock = 0; rock < rockCount; rock++)
            {
                var r = TwinRocks.Rocks[rock]; bool giant = AquariumRockShapes.IsGiant(r.kind);
                bool formation = r.scale >= 1.28f && r.kind != AquariumRockShapes.Pebbles;
                int want = giant ? 26 + random.Next(9) : formation ? 9 + random.Next(5) : r.kind == AquariumRockShapes.Pebbles ? 2 + random.Next(2) : 3 + random.Next(3), made = 0;
                for (int tries = 0; tries < want * (giant ? 9 : 7) && made < want; tries++)
                {
                    Point foot; if (!RockTop(random, rock, .55f, giant ? GiantCeiling : 8.6f, out foot)) continue;
                    if (giant && foot.y < 4.5f && !OffLanes(foot, .4f)) continue;
                    float size = giant ? Range(random, .34f, .78f) : Range(random, .28f, .55f) * (formation ? 1 : .85f);
                    bool near = false; foreach (var c in corals) if (Flat(c.position, foot) < (c.size + size) * .75f && Math.Abs(c.position.y - foot.y) < .5f) { near = true; break; }
                    if (near || !spacing.Free(foot, size * .6f, .8f)) continue;
                    foot.y -= .03f;
                    corals.Add(new AquariumSceneCoral { position = foot, size = size, kind = random.Next(6), yaw = (float)(random.NextDouble() * 360) }); made++;
                }
            }
            Lap(2);
            // R59: coral heads round each giant's foot.
            foreach (int g in giants)
                for (int tries = 0, made = 0; tries < 90 && made < 10; tries++)
                {
                    Point p; if (!GiantEdge(random, TwinRocks.Rocks[g], 0, 1.4f, out p)) continue;
                    float size = Range(random, .3f, .55f);
                    if (!TwinSandClear(p, size * .8f, size, false) || !OffLanes(p, size)) continue;
                    p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .01f;
                    if (!spacing.Free(p, size * .8f, .9f)) continue;
                    spacing.Add(p, size * .8f); made++;
                    corals.Add(new AquariumSceneCoral { position = p, size = size, kind = random.Next(6), yaw = (float)(random.NextDouble() * 360) });
                }
            // R61: coral gardens: tight groups of five to eight heads in the patches.
            foreach (var c in patches)
            {
                if (random.NextDouble() < .35) continue;
                for (int tries = 0, made = 0, want = 5 + random.Next(4); tries < 40 && made < want; tries++)
                {
                    double a = random.NextDouble() * Math.PI * 2; float reach = 1.8f * (float)Math.Sqrt(random.NextDouble());
                    var p = new Point(c.x + (float)Math.Cos(a) * reach, 0, c.z + (float)Math.Sin(a) * reach);
                    float size = Range(random, .28f, .6f);
                    if (!TwinSandClear(p, size * .8f, size, false) || !OffLanes(p, size)) continue;
                    p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .01f;
                    if (!spacing.Free(p, size * .8f, .6f)) continue;
                    spacing.Add(p, size * .8f); made++;
                    corals.Add(new AquariumSceneCoral { position = p, size = size, kind = random.Next(6), yaw = (float)(random.NextDouble() * 360) });
                }
            }
            for (int tries = 0, bed = 0; tries < 3000 && bed < 110; tries++)
            {
                var p = PatchCandidate(random, tries % 2);
                float size = Range(random, .2f, .42f);
                if (!TwinSandClear(p, size * .8f, size, false) || !OffLanes(p, size)) continue;
                p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .01f;
                if (!spacing.Free(p, size * .8f, .9f)) continue;
                spacing.Add(p, size * .8f);
                corals.Add(new AquariumSceneCoral { position = p, size = size, kind = random.Next(6), yaw = (float)(random.NextDouble() * 360) }); bed++;
            }
            Lap(3);
            // R59: seaweed on each giant's ledges and round its foot.
            foreach (int g in giants)
            {
                for (int tries = 0, made = 0; tries < 200 && made < 7; tries++)
                {
                    Point foot; if (!RockTop(random, g, .72f, GiantCeiling, out foot)) continue;
                    if (foot.y < 4.5f && !OffLanes(foot, .5f)) continue;
                    int form = random.Next(5); float height = .8f + (float)random.NextDouble() * 1.0f, r = PlantSpread(form, height);
                    if (foot.y + height > AquariumModel.WaterHeight - .6f || !spacing.Free(foot, r, .9f)) continue;
                    spacing.Add(foot, r); made++;
                    plants.Add(new AquariumScenePlant { position = foot, form = form, height = height, radius = r, phase = (float)(random.NextDouble() * Math.PI * 2), yaw = (float)(random.NextDouble() * 360), ledge = true });
                }
                for (int tries = 0, made = 0; tries < 180 && made < 14; tries++)
                {
                    Point p; if (!GiantEdge(random, TwinRocks.Rocks[g], .1f, 1.8f, out p)) continue;
                    int form = random.NextDouble() < .35 ? 2 : random.Next(5);
                    float height = form == 2 ? 2.0f + (float)random.NextDouble() * 2.6f : 1.2f + (float)random.NextDouble() * 2.0f, r = PlantSpread(form, height);
                    if (!TwinSandClear(p, r, height, false) || !OffLanes(p, r)) continue;
                    p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .01f;
                    if (!spacing.Free(p, r, .9f)) continue;
                    spacing.Add(p, r); made++;
                    plants.Add(new AquariumScenePlant { position = p, form = form, height = height, radius = r, phase = (float)(random.NextDouble() * Math.PI * 2), yaw = (float)(random.NextDouble() * 360) });
                }
                for (int tries = 0, made = 0; tries < 240 && made < 28; tries++)
                {
                    Point p; if (!GiantEdge(random, TwinRocks.Rocks[g], 0, 1.3f, out p)) continue;
                    float height = Range(random, .35f, .7f), r = PlantSpread(GrassForm, height);
                    if (!TwinSandClear(p, r, height, false)) continue;
                    p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .005f;
                    if (!spacing.Free(p, r, .75f)) continue;
                    spacing.Add(p, r); made++;
                    plants.Add(new AquariumScenePlant { position = p, form = GrassForm, height = height, radius = r, phase = (float)(random.NextDouble() * Math.PI * 2), yaw = (float)(random.NextDouble() * 360) });
                }
            }
            Lap(4);
            // Seaweed on the flat rock tops, then on the sand.
            for (int tries = 0; tries < 900 && plants.Count < 18 && rockCount > 0; tries++)
            {
                int rock = random.Next(rockCount);
                Point foot; if (!RockTop(random, rock, .72f, 4.2f, out foot)) continue;
                int form = random.Next(5); float height = .7f + (float)random.NextDouble() * .8f, r = PlantSpread(form, height);
                if (!spacing.Free(foot, r, .9f)) continue;
                spacing.Add(foot, r);
                plants.Add(new AquariumScenePlant { position = foot, form = form, height = height, radius = r, phase = (float)(random.NextDouble() * Math.PI * 2), yaw = (float)(random.NextDouble() * 360), ledge = true });
            }
            // R61: kelp forests: stands of tall kelp reaching mid-water, in the patches away from the lanes.
            foreach (var c in patches)
            {
                if (random.NextDouble() < .5) continue;
                for (int tries = 0, made = 0, want = 7 + random.Next(7); tries < 60 && made < want; tries++)
                {
                    double a = random.NextDouble() * Math.PI * 2; float reach = 2.8f * (float)Math.Sqrt(random.NextDouble());
                    var p = new Point(c.x + (float)Math.Cos(a) * reach, 0, c.z + (float)Math.Sin(a) * reach);
                    float height = Range(random, 4.6f, 7.4f), r = PlantSpread(2, height);
                    if (!TwinSandClear(p, r * .5f, height, false) || !OffLanes(p, .6f) || !OffChase(p, .6f)) continue;
                    p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .01f;
                    if (!spacing.Free(p, r, .35f)) continue;
                    spacing.Add(p, r); made++;
                    plants.Add(new AquariumScenePlant { position = p, form = 2, height = height, radius = r, phase = (float)(random.NextDouble() * Math.PI * 2), yaw = (float)(random.NextDouble() * 360) });
                }
            }
            Lap(5);
            int sand = 0;
            for (int tries = 0; tries < 9000 && sand < 400; tries++)
            {
                int tank = tries % 2; double kindRoll = random.NextDouble();
                var p = PatchCandidate(random, tank);
                int form = kindRoll < .3 ? 2 : random.Next(5);
                float height = form == 2 ? 1.4f + (float)random.NextDouble() * 3.0f : 1.0f + (float)random.NextDouble() * 2.6f;
                float r = PlantSpread(form, height);
                if (!TwinSandClear(p, r, height, false) || !OffLanes(p, r * .5f) || (height > 1.2f && !OffChase(p, r * .4f))) continue;
                p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .01f;
                if (!spacing.Free(p, r, .75f)) continue;
                spacing.Add(p, r); sand++;
                plants.Add(new AquariumScenePlant { position = p, form = form, height = height, radius = r, phase = (float)(random.NextDouble() * Math.PI * 2), yaw = (float)(random.NextDouble() * 360) });
            }
            Lap(6);
            // Grass tufts: low meadows round the rocks and along the glass, a few along the tunnel walls.
            int grass = 0;
            for (int tries = 0; tries < 12000 && grass < 380; tries++)
            {
                Point p; bool tunnel = tries % 9 == 0 && !AquariumTwinMap.Single;
                if (tunnel) p = new Point(Range(random, AquariumTwinMap.PlaneA + 1, AquariumTwinMap.PlaneB - 1), 0, (random.NextDouble() < .5 ? -1 : 1) * Range(random, 2.7f, 3.5f));
                else p = PatchCandidate(random, tries % 2);
                float height = Range(random, .35f, .7f), r = PlantSpread(GrassForm, height);
                if (!TwinSandClear(p, r, height, tunnel)) continue;
                p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .005f;
                if (!spacing.Free(p, r, .75f)) continue;
                spacing.Add(p, r); grass++;
                plants.Add(new AquariumScenePlant { position = p, form = GrassForm, height = height, radius = r, phase = (float)(random.NextDouble() * Math.PI * 2), yaw = (float)(random.NextDouble() * 360) });
            }
            Lap(7);
            // R59: pebbles and shells strewn round each giant's foot.
            foreach (int g in giants)
                for (int tries = 0, made = 0; tries < 300 && made < 80; tries++)
                {
                    Point p; if (!GiantEdge(random, TwinRocks.Rocks[g], -.1f, 1.1f, out p)) continue;
                    int kind = made % 7 == 6 ? 1 : 0;
                    float size = kind == 0 ? Range(random, .04f, .12f) : Range(random, .07f, .12f);
                    float ground = AquariumSeabed.TwinHeight(p.x, p.z);
                    if (AquariumTwinMap.Water(p.x, p.z) < .25f || TwinRocks.Overlaps(new Point(p.x, ground + .05f, p.z), size + .02f)) continue;
                    bool drain = false; if (DrainSites != null) foreach (var d in DrainSites) if (Flat(p, d) < AquariumDrainLayout.ClearanceRadius) { drain = true; break; }
                    if (drain) continue;
                    p.y = ground + .01f; made++;
                    details.Add(new AquariumSceneDetail { position = p, size = size, kind = kind, yaw = (float)(random.NextDouble() * 360) });
                }
            // Pebbles, shells and sea stars (looks only; nothing collides with them).
            // R63: more and bigger sea stars (now modelled), and some on the rocks and along the glass foot below.
            int[] wantDetails = { 560, 110, 96 };
            for (int kind = 0; kind < 3; kind++)
                for (int tries = 0, made = 0; tries < wantDetails[kind] * 4 && made < wantDetails[kind]; tries++)
                {
                    var p = kind == 0 && tries % 3 == 0 && !AquariumTwinMap.Single ? new Point(Range(random, AquariumTwinMap.PlaneA, AquariumTwinMap.PlaneB), 0, Range(random, -3.4f, 3.4f)) : PatchCandidate(random, tries % 2);
                    float size = kind == 0 ? Range(random, .03f, .09f) : kind == 1 ? Range(random, .07f, .12f) : Range(random, .15f, .29f);
                    if (AquariumTwinMap.Water(p.x, p.z) < .25f || AquariumEntryLayout.KeepWaterClear(p, 0)) continue;
                    float ground = AquariumSeabed.TwinHeight(p.x, p.z);
                    if (TwinRocks.Overlaps(new Point(p.x, ground + .05f, p.z), size + .02f)) continue;
                    bool drain = false; if (DrainSites != null) foreach (var d in DrainSites) if (Flat(p, d) < AquariumDrainLayout.ClearanceRadius) { drain = true; break; }
                    if (drain) continue;
                    p.y = ground + .01f;
                    details.Add(new AquariumSceneDetail { position = p, size = size, kind = kind, yaw = (float)(random.NextDouble() * 360) }); made++;
                }
            // R63: sea stars on the rocks (flat tops and gentle faces, tilted to the face) ...
            for (int tries = 0, made = 0; tries < 500 && made < 34 && rockCount > 0; tries++)
            {
                int rock = random.Next(rockCount); if (TwinRocks.Rocks[rock].kind == AquariumRockShapes.Pebbles) continue;
                Point foot; if (!RockTop(random, rock, .35f, 7.5f, out foot)) continue;
                float size = Range(random, .13f, .24f); float yaw = (float)(random.NextDouble() * 360);
                bool near = false; foreach (var c in corals) if (Flat(c.position, foot) < c.size * .6f + size && Math.Abs(c.position.y - foot.y) < .6f) { near = true; break; }
                if (near || !spacing.Free(foot, size, .5f)) continue;
                var n = TwinRocks.Normal(foot); foot.y -= .012f;
                details.Add(new AquariumSceneDetail { position = foot, size = size, kind = 3, normal = n, yaw = yaw }); made++;
            }
            // ... and a few on the sand along the foot of the glass.
            for (int tries = 0, made = 0; tries < 800 && made < 18; tries++)
            {
                var p = TankPoint(random, tries % 2); float size = Range(random, .15f, .26f), yaw = (float)(random.NextDouble() * 360);
                float w = AquariumTwinMap.Water(p.x, p.z);
                if (w < .3f || w > 1.0f || AquariumEntryLayout.KeepWaterClear(p, 0)) continue;
                float ground = AquariumSeabed.TwinHeight(p.x, p.z);
                if (TwinRocks.Overlaps(new Point(p.x, ground + .05f, p.z), size + .02f)) continue;
                bool drain = false; if (DrainSites != null) foreach (var d in DrainSites) if (Flat(p, d) < AquariumDrainLayout.ClearanceRadius) { drain = true; break; }
                if (drain) continue;
                p.y = ground + .01f;
                details.Add(new AquariumSceneDetail { position = p, size = size, kind = 2, yaw = yaw }); made++;
            }
            Lap(8);
            // R69: the tunnel's corals, anemones, seaweed and sand dressing round its rocks.
            PlaceTunnelLife(tunnelRandom, spacing, anemones, plants, corals, details);
            Lap(9);
            Anemones = anemones.ToArray(); Plants = plants.ToArray(); Corals = corals.ToArray(); Details = details.ToArray();
            var spots = new Point[Anemones.Length];
            for (int i = 0; i < spots.Length; i++) spots[i] = Anemones[i].position;
            AnemoneSpots = spots;
            var heads = new Point[Corals.Length];
            for (int i = 0; i < heads.Length; i++) heads[i] = Corals[i].position + new Point(0, Corals[i].size * .7f, 0);
            CoralSpots = heads;
        }
    }
}
