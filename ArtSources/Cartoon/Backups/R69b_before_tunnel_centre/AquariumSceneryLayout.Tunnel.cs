using System;
using System.Collections.Generic;

namespace PrototypeLab.Aquarium
{
    // R69: the twin tanks' tunnel gets its own dressing every game, from the same scenery seed (so every machine
    // builds the same): rock ribs pressed to the acrylic in pairs, a low arch alcove, reef shelves and overhangs,
    // outcrops and rubble hugging both walls, then corals, anemones, seaweed, grass, sea stars, shells and pebbles
    // on and round them. The rocks join the layout's rock set (AquariumRockSet), so they are drawn, collide, join
    // (R64 seams) and enter the bots' grid exactly like the tanks' rocks.
    // A swim lane stays open the whole length of the tunnel, from the sand to the roof: TunnelLaneHalfWidth either
    // side of a gently winding centre line, kept to one side of the tunnel axis per game so the room left for the
    // rocks alternates (wide on one wall, narrow on the other). No rock comes within a fillet's reach of the lane, none
    // touches the acrylic, and none stands in the bell mouths.
    public sealed partial class AquariumSceneryLayout
    {
        // Half width of the open swim lane (3.7 m across); it runs the whole height of the tunnel.
        public const float TunnelLaneHalfWidth = 1.85f;
        // Rocks stay between -TunnelReach and TunnelReach (clear of the bell mouths).
        public const float TunnelReach = 9.3f;
        // Margin kept from the lane by every rock surface (more than the R64 fillet can bulge, .9 / 4).
        private const float LaneMargin = .35f;
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
        private AquariumRock[] PlaceTunnelRocks(Random random, AquariumRock[] tankRocks)
        {
            var rocks = new List<AquariumRock>(tankRocks); TunnelRockStart = tankRocks.Length;
            if (TunnelDressingOff) { TunnelLaneSide = 0; return tankRocks; }
            TunnelLaneSide = random.NextDouble() < .5 ? -1 : 1;
            tunnelWave = Range(random, .32f, .5f); tunnelPhase = (float)(random.NextDouble() * Math.PI * 2);
            var placed = new List<Placed>();
            float wide = -TunnelLaneSide;
            // Rock ribs: two or three stations where a tall pillar or spire rises against each wall.
            int ribs = 2 + random.Next(2); var stations = new List<float>();
            for (int tries = 0; tries < 60 && stations.Count < ribs; tries++)
            {
                float x = Range(random, -7.4f, 7.4f); bool near = false;
                foreach (var s in stations) if (Math.Abs(s - x) < 4.4f) { near = true; break; }
                if (!near) stations.Add(x);
            }
            foreach (float x in stations)
                for (int side = -1; side <= 1; side += 2)
                {
                    bool spire = random.NextDouble() < .4;
                    int kind = spire ? AquariumRockShapes.Spire : AquariumRockShapes.Pillar;
                    float scale = spire ? Range(random, .66f, .84f) : Range(random, .86f, 1.12f);
                    TryTunnelRock(random, rocks, placed, kind, scale, (float)(random.NextDouble() * 360), x + Range(random, -.4f, .4f), side, .55f);
                }
            // The wide wall's features: a low arch alcove (its window facing the lane), a reef shelf and an overhang.
            var features = new List<int> { AquariumRockShapes.Arch, AquariumRockShapes.Table, AquariumRockShapes.Ledge };
            for (int i = features.Count - 1; i > 0; i--) { int j = random.Next(i + 1); int t = features[i]; features[i] = features[j]; features[j] = t; }
            int want = 2 + random.Next(2);
            for (int f = 0, made = 0; f < features.Count && made < want; f++)
            {
                int kind = features[f];
                for (int tries = 0; tries < 14; tries++)
                {
                    float x = Range(random, -TunnelReach + 2.6f, TunnelReach - 2.6f);
                    float scale, yaw;
                    if (kind == AquariumRockShapes.Arch) { scale = Range(random, .8f, .92f); yaw = (random.NextDouble() < .5 ? 0 : 180) + Range(random, -7, 7); }
                    else if (kind == AquariumRockShapes.Table) { scale = Range(random, .95f, 1.2f); yaw = (float)(random.NextDouble() * 360); }
                    else { scale = Range(random, .85f, 1.05f); yaw = (wide > 0 ? 90 : 270) + Range(random, -25, 25); } // the shelf leans out from the wall (local +x toward the lane)
                    if (TryTunnelRock(random, rocks, placed, kind, scale, yaw, x, (int)wide, .66f)) { made++; break; }
                }
            }
            // Outcrops hugging both walls, broken now and then by open sand.
            int[] wallKinds = { AquariumRockShapes.Worn, AquariumRockShapes.Split, AquariumRockShapes.Pebbles, AquariumRockShapes.Worn, AquariumRockShapes.Ledge, AquariumRockShapes.Split, AquariumRockShapes.Table };
            for (int side = -1; side <= 1; side += 2)
                for (float x = -TunnelReach + 1.0f + Range(random, 0, 1.2f); x < TunnelReach - 1.0f; x += Range(random, 1.3f, 2.3f))
                {
                    if (random.NextDouble() < .2) continue;
                    int kind = wallKinds[random.Next(wallKinds.Length)];
                    float scale = side == wide ? Range(random, .7f, 1.1f) : Range(random, .55f, .85f);
                    TryTunnelRock(random, rocks, placed, kind, scale, (float)(random.NextDouble() * 360), x, side, .38f);
                }
            // Rubble: small stones and pebble heaps between them.
            for (int i = 0, made = 0, rubble = 5 + random.Next(5); i < 40 && made < rubble; i++)
            {
                int kind = random.NextDouble() < .6 ? AquariumRockShapes.Pebbles : AquariumRockShapes.Worn;
                if (TryTunnelRock(random, rocks, placed, kind, Range(random, .28f, .5f), (float)(random.NextDouble() * 360), Range(random, -TunnelReach + .8f, TunnelReach - .8f), random.NextDouble() < .5 ? -1 : 1, .25f, true)) made++;
            }
            return rocks.ToArray();
        }

        // A rock of this kind slid in from the glass at x until it clears the acrylic, shrunk until it clears the lane.
        private bool TryTunnelRock(Random random, List<AquariumRock> rocks, List<Placed> placed, int kind, float scale, float yaw, float x, int side, float minScale, bool loose = false)
        {
            int variant = random.Next(AquariumRockShapes.Variants); float tint = (float)random.NextDouble();
            for (int shrink = 0; shrink < 6 && scale >= minScale; shrink++, scale *= .88f)
            {
                float footprint = AquariumRockShapes.Footprint(kind) * scale; bool tooBig = false;
                for (float d = .15f; d < 3.4f && !tooBig; d += .12f)
                {
                    float z = side * (AquariumTwinMap.Radius(x) - d);
                    var rock = new AquariumRock { kind = kind, variant = variant, position = new Point(x, TunnelFoot(kind, x, z, scale), z), yaw = yaw, scale = scale, tint = tint };
                    if (!TunnelBoxInside(rock)) { if (d > 2.2f) tooBig = true; continue; }
                    if (!TunnelGlassClear(rock)) continue;
                    // From here inward it only comes nearer the lane: when it reaches it, try smaller.
                    if (!TunnelLaneClear(rock)) { tooBig = true; break; }
                    var p = new Point(x, 0, z);
                    foreach (var q in placed)
                    {
                        // Touching and overlapping is fine (the joins melt them into one stone), stacking is not.
                        float need = (footprint + q.footprint) * (loose ? .5f : .55f);
                        if (Flat(p, q.p) < need) return false;
                    }
                    rocks.Add(rock); placed.Add(new Placed { p = p, footprint = footprint, cluster = 700 });
                    return true;
                }
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
            float side = r.position.z < 0 ? -1 : 1;
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
            return true;
        }
        // No stone within LaneMargin of the lane, from the sand to the roof.
        private bool TunnelLaneClear(AquariumRock r)
        {
            var lo = AquariumRockShapes.LocalMin[r.kind]; var hi = AquariumRockShapes.LocalMax[r.kind];
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            for (int c = 0; c < 4; c++)
            {
                var w = AquariumRockShapes.ToWorld(r, new Point((c & 1) == 0 ? lo.x : hi.x, 0, (c & 2) == 0 ? lo.z : hi.z));
                x0 = Math.Min(x0, w.x); x1 = Math.Max(x1, w.x); z0 = Math.Min(z0, w.z); z1 = Math.Max(z1, w.z);
            }
            float top = r.position.y + hi.y * r.scale + LaneMargin;
            var mid = AquariumRockShapes.ToWorld(r, (lo + hi) * .5f); float reach = (hi - lo).Length * .5f * r.scale + LaneMargin + .05f;
            for (float x = x0 - LaneMargin; x <= x1 + LaneMargin + .001f; x += .22f)
            {
                float c = TunnelLaneCentre(x), a = c - TunnelLaneHalfWidth, b = c + TunnelLaneHalfWidth;
                // The box (a bound of the stone) does not reach the lane here.
                if (z1 + LaneMargin < a || z0 - LaneMargin > b) continue;
                float floor = AquariumSeabed.TwinHeight(x, c) + .06f;
                for (float z = Math.Max(a, z0 - LaneMargin); z <= Math.Min(b, z1 + LaneMargin) + .001f; z += .22f)
                    for (float y = floor; y <= top; y += .22f)
                    {
                        var p = new Point(x, y, z); var o = p - mid;
                        if (o.Length > reach) continue;
                        if (RockField(r, p) < LaneMargin) return false;
                    }
            }
            return true;
        }
        private static float RockField(AquariumRock r, Point world)
        {
            return AquariumRockShapes.Local(r.kind, r.variant, AquariumRockShapes.ToLocal(r, world)) * r.scale;
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
            return !InTunnelLane(foot, .1f) && foot.y + clearance < AquariumTwinMap.RoofCeiling(foot.x, foot.z, .2f);
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
            for (int tries = 0, made = 0, want = 1 + random.Next(3); tries < 200 && made < want; tries++)
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
                int want = small ? 1 + random.Next(2) : feature ? 5 + random.Next(4) : 3 + random.Next(3);
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
            for (int tries = 0, made = 0, want = 10 + random.Next(7); tries < 260 && made < want; tries++)
            {
                int rock; var p = TunnelRockFoot(random, out rock); float size = Range(random, .24f, .46f);
                if (!TunnelSandClear(p, size * .8f, size, false)) continue;
                p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .01f;
                if (!spacing.Free(p, size * .8f, .7f)) continue;
                spacing.Add(p, size * .8f); made++;
                corals.Add(new AquariumSceneCoral { position = p, size = size, kind = random.Next(6), yaw = (float)(random.NextDouble() * 360) });
            }
            for (int tries = 0, made = 0, want = 8 + random.Next(7); tries < 300 && made < want; tries++)
            {
                int rock; var p = TunnelRockFoot(random, out rock);
                int form = random.Next(5); float height = form == 2 ? Range(random, 1.4f, 2.6f) : Range(random, .8f, 1.9f), r = PlantSpread(form, height);
                if (!TunnelSandClear(p, r, height, false) || InTunnelLane(p, r * .35f)) continue;
                p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .01f;
                if (!spacing.Free(p, r, .75f)) continue;
                spacing.Add(p, r); made++;
                plants.Add(new AquariumScenePlant { position = p, form = form, height = height, radius = r, phase = (float)(random.NextDouble() * Math.PI * 2), yaw = (float)(random.NextDouble() * 360) });
            }
            // A few on the flat tops of the shelves too.
            for (int tries = 0, made = 0; tries < 120 && made < 4; tries++)
            {
                int rock = start + random.Next(end - start);
                int form = random.Next(5); float height = Range(random, .6f, 1.2f), r = PlantSpread(form, height);
                Point foot; if (!TunnelTop(random, rock, .72f, height + .2f, out foot) || InTunnelLane(foot, r * .4f)) continue;
                if (!spacing.Free(foot, r, .9f)) continue;
                spacing.Add(foot, r); made++;
                plants.Add(new AquariumScenePlant { position = foot, form = form, height = height, radius = r, phase = (float)(random.NextDouble() * Math.PI * 2), yaw = (float)(random.NextDouble() * 360), ledge = true });
            }
            for (int tries = 0, made = 0, want = 16 + random.Next(10); tries < 360 && made < want; tries++)
            {
                int rock; var p = TunnelRockFoot(random, out rock);
                float height = Range(random, .35f, .65f), r = PlantSpread(GrassForm, height);
                if (!TunnelSandClear(p, r, height, false)) continue;
                p.y = AquariumSeabed.TwinHeight(p.x, p.z) + .005f;
                if (!spacing.Free(p, r, .75f)) continue;
                spacing.Add(p, r); made++;
                plants.Add(new AquariumScenePlant { position = p, form = GrassForm, height = height, radius = r, phase = (float)(random.NextDouble() * Math.PI * 2), yaw = (float)(random.NextDouble() * 360) });
            }
            // Sea stars on the rock faces and the sand beside them, shells and pebbles strewn round the feet
            // (looks only, nothing collides with them; the lane's sand may have them).
            for (int tries = 0, made = 0, want = 5 + random.Next(5); tries < 200 && made < want; tries++)
            {
                int rock = start + random.Next(end - start); if (TwinRocks.Rocks[rock].kind == AquariumRockShapes.Pebbles) continue;
                Point foot; if (!TunnelTop(random, rock, .35f, .1f, out foot)) continue;
                float size = Range(random, .13f, .22f);
                if (nearCoral(foot, size) || !spacing.Free(foot, size, .5f)) continue;
                var n = TwinRocks.Normal(foot); foot.y -= .012f; made++;
                details.Add(new AquariumSceneDetail { position = foot, size = size, kind = 3, normal = n, yaw = (float)(random.NextDouble() * 360) });
            }
            int[] wantDetails = { 36 + random.Next(16), 9 + random.Next(7), 3 + random.Next(4) };
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
