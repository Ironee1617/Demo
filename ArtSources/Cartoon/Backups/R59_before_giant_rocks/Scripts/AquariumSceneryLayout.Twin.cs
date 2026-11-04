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
        // 0 pebble, 1 shell, 2 sea star.
        public int kind;
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
    public sealed partial class AquariumSceneryLayout
    {
        public AquariumRockSet TwinRocks { get; private set; } = AquariumRockSet.Empty;
        public AquariumSceneCoral[] Corals { get; private set; }
        public AquariumSceneDetail[] Details { get; private set; }

        // Grass tufts (form 5) are a twin-only seaweed form.
        public const int GrassForm = 5;

        private static AquariumSceneryLayout BuildTwin(int seed)
        {
            var layout = new AquariumSceneryLayout(seed, new float[AquariumMonolithLayout.Groups.Length], new float[0]);
            layout.Map = AquariumMaps.Twin;
            int baseSeed = seed == 0 ? 5701 : seed;
            Random random = null;
            for (int attempt = 0; attempt < 4 && layout.DrainSites == null; attempt++)
            {
                random = new Random(attempt == 0 ? baseSeed : unchecked(baseSeed * 31 + attempt));
                layout.TwinRocks = new AquariumRockSet(PlaceTwinRocks(random));
                using (Use(layout)) layout.DrainSites = AquariumTwinMap.PickDrains(random);
            }
            if (layout.DrainSites == null)
            {
                // Never expected: no room for the drains among the rocks, so this game has open tanks.
                layout.TwinRocks = AquariumRockSet.Empty;
                using (Use(layout)) layout.DrainSites = AquariumTwinMap.PickDrains(new Random(baseSeed));
            }
            using (Use(layout)) layout.PlaceTwinLife(random);
            return layout;
        }

        // ---------------------------------------------------------------- rocks
        private static Point[] twinSpawns, twinVents;
        private static Point[] TwinSpawns()
        {
            if (twinSpawns != null) return twinSpawns;
            var list = new List<Point>();
            for (int i = 0; i < 5; i++) list.Add(AquariumTwinMap.Layout(-9 + i * 2.4f, 2, 0));
            for (int i = 0; i < 8; i++) list.Add(AquariumTwinMap.Layout((i % 2 == 0 ? -7.5f : 7.5f) + (i / 2) * 2.2f - 1, 2, i % 3 - 1.5f));
            for (int i = 0; i < 2; i++) list.Add(AquariumTwinMap.Layout(10, 2, -4 + i * 2));
            return twinSpawns = list.ToArray();
        }
        // AquariumEnvironment.TwinAtmosphere's aeration vents.
        private static Point[] TwinVents()
        {
            return twinVents ?? (twinVents = new[] { new Point(-35, 6), new Point(-22, -7), new Point(-30, 10), new Point(22, 7), new Point(35, -5), new Point(31, 12) });
        }
        private static readonly Point MouthA = new Point(AquariumTwinMap.PlaneA, 0, 0), MouthB = new Point(AquariumTwinMap.PlaneB, 0, 0);

        // Room for a rock with this footprint at (x, z): inside a tank with a swim lane to the glass, clear of the
        // tunnel mouths, the dive water, the spawn points and the vents.
        private static bool RockRoom(float x, float z, float footprint, float glassGap)
        {
            if (Math.Abs(x) < AquariumTwinMap.PlaneB + 1) return false;
            if (AquariumTwinMap.Water(x, z) < footprint + glassGap) return false;
            var p = new Point(x, 0, z);
            if (Flat(p, MouthA) < footprint + 6.5f || Flat(p, MouthB) < footprint + 6.5f) return false;
            if (AquariumEntryLayout.KeepWaterClear(p, footprint + 2.2f)) return false;
            foreach (var s in TwinSpawns()) if (Flat(p, s) < footprint + 1.6f) return false;
            foreach (var v in TwinVents()) if (Flat(p, v) < footprint + 1.0f) return false;
            return true;
        }
        private struct Placed { public Point p; public float footprint; public int cluster; }
        private static bool Spaced(List<Placed> placed, Point p, float footprint, float gap, int cluster)
        {
            foreach (var q in placed)
            {
                float d = Flat(p, q.p);
                if (cluster >= 0 && q.cluster == cluster) { if (d < (footprint + q.footprint) * .55f) return false; }
                else if (d < footprint + q.footprint + gap) return false;
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
            return tank == 0 ? new Point(-50 + (float)random.NextDouble() * 38, 0, -23 + (float)random.NextDouble() * 46)
                : new Point(12 + (float)random.NextDouble() * 39, 0, -23 + (float)random.NextDouble() * 46);
        }
        private static float Range(Random random, float a, float b) { return a + (float)random.NextDouble() * (b - a); }

        private static AquariumRock[] PlaceTwinRocks(Random random)
        {
            var rocks = new List<AquariumRock>(); var placed = new List<Placed>();
            // Formations: every big shape appears once per game, spread over the two tanks (the larger B gets four).
            var big = new List<int> { AquariumRockShapes.Arch, AquariumRockShapes.Table, AquariumRockShapes.Ledge, AquariumRockShapes.Spire, AquariumRockShapes.Pillar, AquariumRockShapes.Split };
            for (int i = big.Count - 1; i > 0; i--) { int j = random.Next(i + 1); int t = big[i]; big[i] = big[j]; big[j] = t; }
            int[] extra = { AquariumRockShapes.Arch, AquariumRockShapes.Table, AquariumRockShapes.Ledge, AquariumRockShapes.Pillar, AquariumRockShapes.Split, AquariumRockShapes.Worn };
            for (int i = 0; i < 3; i++) big.Insert(i == 0 ? 3 : big.Count, extra[random.Next(extra.Length)]);
            for (int n = 0; n < big.Count; n++)
            {
                int kind = big[n], tank = n < 4 ? 0 : 1;
                float scale = kind == AquariumRockShapes.Arch ? Range(random, 1.5f, 1.75f) : kind == AquariumRockShapes.Spire ? Range(random, 1.3f, 1.6f) : Range(random, 1.3f, 1.65f);
                float footprint = AquariumRockShapes.Footprint(kind) * scale;
                for (int tries = 0; tries < 300; tries++)
                {
                    var p = TankPoint(random, tank);
                    if (!RockRoom(p.x, p.z, footprint, 2.6f) || !Spaced(placed, p, footprint, 2.8f, -1)) continue;
                    rocks.Add(MakeRock(random, kind, p.x, p.z, scale)); placed.Add(new Placed { p = p, footprint = footprint, cluster = -1 }); break;
                }
            }
            // Clusters: a massif of two to four rocks that read as one outcrop.
            int[] clusterKinds = { AquariumRockShapes.Worn, AquariumRockShapes.Pebbles, AquariumRockShapes.Split, AquariumRockShapes.Pillar, AquariumRockShapes.Worn };
            for (int c = 0; c < 8; c++)
            {
                int tank = c % 2;
                for (int tries = 0; tries < 220; tries++)
                {
                    var centre = TankPoint(random, tank);
                    if (!RockRoom(centre.x, centre.z, 3.6f, 2.4f) || !Spaced(placed, centre, 3.6f, 2.6f, -1)) continue;
                    int members = 2 + random.Next(3), made = 0;
                    for (int m = 0; m < members * 10 && made < members; m++)
                    {
                        int kind = clusterKinds[random.Next(clusterKinds.Length)];
                        float scale = kind == AquariumRockShapes.Pillar ? Range(random, .8f, 1.1f) : Range(random, .8f, 1.25f);
                        float footprint = AquariumRockShapes.Footprint(kind) * scale;
                        double a = random.NextDouble() * Math.PI * 2; float reach = made == 0 ? 0 : Range(random, 1.6f, 3.2f);
                        var p = new Point(centre.x + (float)Math.Cos(a) * reach, 0, centre.z + (float)Math.Sin(a) * reach);
                        if (!RockRoom(p.x, p.z, footprint, 2.2f) || !Spaced(placed, p, footprint, 2.4f, 100 + c)) continue;
                        rocks.Add(MakeRock(random, kind, p.x, p.z, scale)); placed.Add(new Placed { p = p, footprint = footprint, cluster = 100 + c }); made++;
                    }
                    break;
                }
            }
            // Single boulders and pebble heaps between them.
            int[] singleKinds = { AquariumRockShapes.Worn, AquariumRockShapes.Pebbles, AquariumRockShapes.Worn, AquariumRockShapes.Pebbles, AquariumRockShapes.Split };
            for (int s = 0; s < 14; s++)
            {
                int tank = s % 2 == 0 ? 1 : (s < 11 ? 0 : 1);
                int kind = singleKinds[random.Next(singleKinds.Length)];
                float scale = Range(random, .6f, 1.0f), footprint = AquariumRockShapes.Footprint(kind) * scale;
                for (int tries = 0; tries < 160; tries++)
                {
                    var p = TankPoint(random, tank);
                    if (!RockRoom(p.x, p.z, footprint, 1.9f) || !Spaced(placed, p, footprint, 2.1f, -1)) continue;
                    rocks.Add(MakeRock(random, kind, p.x, p.z, scale)); placed.Add(new Placed { p = p, footprint = footprint, cluster = -1 }); break;
                }
            }
            return rocks.ToArray();
        }

        // ---------------------------------------------------------------- life
        private bool TwinSandClear(Point p, float radius, float height, bool tunnelOk)
        {
            if (AquariumTwinMap.Water(p.x, p.z) < radius + .35f) return false;
            if (!tunnelOk && Math.Abs(p.x) < AquariumTwinMap.PlaneB + .8f) return false;
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
            for (; y > bottom; y -= .05f) if (TwinRocks.RockDistance(rock, new Point(top.x, y, top.z)) < 0) break;
            if (y <= bottom || y > maxHeight) return false;
            var p = new Point(top.x, y + .01f, top.z);
            if (TwinRocks.Normal(p).y < minUp) return false;
            // Open water above it (not under another bed of rock).
            for (float h = .3f; h < 1.1f; h += .3f) if (TwinRocks.Distance(new Point(p.x, p.y + h, p.z)) < .1f) return false;
            if (AquariumTwinMap.Water(p.x, p.z) < .6f) return false;
            foot = p; return true;
        }

        private void PlaceTwinLife(Random random)
        {
            var spacing = new Spacing(); var anemones = new List<AquariumSceneAnemone>(); var plants = new List<AquariumScenePlant>();
            var corals = new List<AquariumSceneCoral>(); var details = new List<AquariumSceneDetail>();
            int rockCount = TwinRocks.Count;
            // Anemones first (the clownfish homes): flat rock tops, then sand round the rock feet, both tanks alike.
            for (int tries = 0; tries < 900 && anemones.Count < 7 && rockCount > 0; tries++)
            {
                int rock = random.Next(rockCount); int kind = TwinRocks.Rocks[rock].kind;
                if (kind == AquariumRockShapes.Pebbles || kind == AquariumRockShapes.Spire) continue;
                Point foot; if (!RockTop(random, rock, .82f, 4.6f, out foot)) continue;
                float size = .27f + (float)random.NextDouble() * .13f, r = AnemoneSpread(size);
                if (!spacing.Free(foot, r, 1.15f)) continue;
                spacing.Add(foot, r);
                anemones.Add(new AquariumSceneAnemone { position = foot, size = size, kind = random.Next(2), yaw = (float)(random.NextDouble() * 360), ledge = true });
            }
            for (int tries = 0; tries < 3000 && anemones.Count < 20; tries++)
            {
                var p = TwinSandCandidate(random, tries % 2);
                float size = .36f + (float)random.NextDouble() * .12f, r = AnemoneSpread(size);
                if (!TwinSandClear(p, r, size * 1.2f, false)) continue;
                p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .02f;
                if (!spacing.Free(p, r, 1.15f)) continue;
                spacing.Add(p, r);
                anemones.Add(new AquariumSceneAnemone { position = p, size = size, kind = random.Next(2), yaw = (float)(random.NextDouble() * 360) });
            }
            // Coral colonies on the rocks (more on the big formations) and coral beds on the sand.
            for (int rock = 0; rock < rockCount; rock++)
            {
                var r = TwinRocks.Rocks[rock];
                bool formation = r.scale >= 1.28f && r.kind != AquariumRockShapes.Pebbles;
                int want = formation ? 9 + random.Next(5) : r.kind == AquariumRockShapes.Pebbles ? 2 + random.Next(2) : 3 + random.Next(3), made = 0;
                for (int tries = 0; tries < want * 8 && made < want; tries++)
                {
                    Point foot; if (!RockTop(random, rock, .55f, 8.6f, out foot)) continue;
                    float size = Range(random, .28f, .55f) * (formation ? 1 : .85f);
                    bool near = false; foreach (var c in corals) if (Flat(c.position, foot) < (c.size + size) * .75f && Math.Abs(c.position.y - foot.y) < .5f) { near = true; break; }
                    if (near || !spacing.Free(foot, size * .6f, .8f)) continue;
                    foot.y -= .03f;
                    corals.Add(new AquariumSceneCoral { position = foot, size = size, kind = random.Next(6), yaw = (float)(random.NextDouble() * 360) }); made++;
                }
            }
            for (int tries = 0, bed = 0; tries < 1400 && bed < 45; tries++)
            {
                var p = TwinSandCandidate(random, tries % 2);
                float size = Range(random, .2f, .42f);
                if (!TwinSandClear(p, size * .8f, size, false)) continue;
                p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .01f;
                if (!spacing.Free(p, size * .8f, .9f)) continue;
                spacing.Add(p, size * .8f);
                corals.Add(new AquariumSceneCoral { position = p, size = size, kind = random.Next(6), yaw = (float)(random.NextDouble() * 360) }); bed++;
            }
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
            int sand = 0;
            for (int tries = 0; tries < 9000 && sand < 210; tries++)
            {
                int tank = tries % 2; double kindRoll = random.NextDouble();
                var p = TwinSandCandidate(random, tank);
                int form = kindRoll < .3 ? 2 : random.Next(5);
                float height = form == 2 ? 1.4f + (float)random.NextDouble() * 3.0f : 1.0f + (float)random.NextDouble() * 2.6f;
                float r = PlantSpread(form, height);
                if (!TwinSandClear(p, r, height, false)) continue;
                p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .01f;
                if (!spacing.Free(p, r, .9f)) continue;
                spacing.Add(p, r); sand++;
                plants.Add(new AquariumScenePlant { position = p, form = form, height = height, radius = r, phase = (float)(random.NextDouble() * Math.PI * 2), yaw = (float)(random.NextDouble() * 360) });
            }
            // Grass tufts: low meadows round the rocks and along the glass, a few along the tunnel walls.
            int grass = 0;
            for (int tries = 0; tries < 6000 && grass < 170; tries++)
            {
                Point p; bool tunnel = tries % 9 == 0;
                if (tunnel) p = new Point(Range(random, AquariumTwinMap.PlaneA + 1, AquariumTwinMap.PlaneB - 1), 0, (random.NextDouble() < .5 ? -1 : 1) * Range(random, 2.7f, 3.5f));
                else p = TwinSandCandidate(random, tries % 2);
                float height = Range(random, .35f, .7f), r = PlantSpread(GrassForm, height);
                if (!TwinSandClear(p, r, height, tunnel)) continue;
                p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .005f;
                if (!spacing.Free(p, r, .75f)) continue;
                spacing.Add(p, r); grass++;
                plants.Add(new AquariumScenePlant { position = p, form = GrassForm, height = height, radius = r, phase = (float)(random.NextDouble() * Math.PI * 2), yaw = (float)(random.NextDouble() * 360) });
            }
            // Pebbles, shells and sea stars (looks only; nothing collides with them).
            int[] wantDetails = { 260, 50, 30 };
            for (int kind = 0; kind < 3; kind++)
                for (int tries = 0, made = 0; tries < wantDetails[kind] * 4 && made < wantDetails[kind]; tries++)
                {
                    var p = kind == 0 && tries % 3 == 0 ? new Point(Range(random, AquariumTwinMap.PlaneA, AquariumTwinMap.PlaneB), 0, Range(random, -3.4f, 3.4f)) : TwinSandCandidate(random, tries % 2);
                    float size = kind == 0 ? Range(random, .03f, .09f) : kind == 1 ? Range(random, .07f, .12f) : Range(random, .14f, .22f);
                    if (AquariumTwinMap.Water(p.x, p.z) < .25f || AquariumEntryLayout.KeepWaterClear(p, 0)) continue;
                    float ground = AquariumSeabed.TwinHeight(p.x, p.z);
                    if (TwinRocks.Overlaps(new Point(p.x, ground + .05f, p.z), size + .02f)) continue;
                    bool drain = false; if (DrainSites != null) foreach (var d in DrainSites) if (Flat(p, d) < AquariumDrainLayout.ClearanceRadius) { drain = true; break; }
                    if (drain) continue;
                    p.y = ground + .01f;
                    details.Add(new AquariumSceneDetail { position = p, size = size, kind = kind, yaw = (float)(random.NextDouble() * 360) }); made++;
                }
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
