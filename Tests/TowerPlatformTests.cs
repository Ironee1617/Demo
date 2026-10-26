using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using PrototypeLab.Roguelike;

public static class TowerPlatformTests
{
    private const float Ground = -3;
    private static int passed, failed;
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Test(string name, Action body)
    { try { body(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); } }
    private static bool Finite(float value) { return !Single.IsNaN(value) && !Single.IsInfinity(value); }
    private static string Shape(TowerFloorLayout layout)
    {
        var pieces = new List<string>();
        foreach (var p in layout.Platforms)
        {
            var s = p.Surface; var nodes = new List<string>();
            for (int i = 0; i < s.NodeCount; i++) nodes.Add(s.NodeX(i).ToString("R", CultureInfo.InvariantCulture) + ":" + s.NodeY(i).ToString("R", CultureInfo.InvariantCulture));
            pieces.Add(String.Join(",", nodes.ToArray()));
        }
        var ground = new List<string>();
        for (int i = 0; i < layout.Ground.NodeCount; i += 7) ground.Add(layout.Ground.NodeY(i).ToString("R", CultureInfo.InvariantCulture));
        return String.Join(";", pieces.ToArray()) + "|" + String.Join(",", ground.ToArray());
    }
    private static void Walk(TowerFloorLayout layout, int support, float left, float right, float speed, float dt, string label)
    {
        float x = left, y = layout.SurfaceHeight(support, left), velocity = 0; bool grounded = true; int current = support;
        while (x < right)
        {
            float old = x; x = Math.Min(right, x + speed * dt);
            TowerPhysics.Step(layout, old, x, ref y, ref velocity, ref grounded, ref current, dt, -1);
            Require(grounded && current == support, label + " left its surface at x=" + x + " (" + layout.Name + ")");
            Require(Math.Abs(y - layout.SurfaceHeight(support, x)) < .0001f, label + " drifted off the surface height at x=" + x);
        }
    }

    public static int Main()
    {
        Test("generated floors keep bounds, tiers, slopes and flat zones", () => {
            int crowns = 0, floors = 0, attempts = 0, totalAttempts = 0;
            for (int seed = -8; seed < 24; seed++)
                for (int floor = 1; floor <= 24; floor++)
                {
                    var layout = TowerTerrainGenerator.Generate(seed, floor); floors++;
                    Require(layout != null && !String.IsNullOrWhiteSpace(layout.Name) && layout.Floor == floor, "layout identity");
                    Require(layout.Platforms.Count >= 22 && layout.Platforms.Count <= 48, "platform count " + layout.Platforms.Count + " in " + layout.Name);
                    Require(layout.ExitX == TowerTerrainGenerator.ExitX && layout.Right == TowerTerrainGenerator.ExitX + TowerTerrainGenerator.ExitMargin, "long floor extent in " + layout.Name);
                    var ground = layout.Ground;
                    Require(ground.MinHeight >= Ground - TowerTerrainGenerator.MaxDip - .0001f && ground.MaxHeight <= Ground + TowerTerrainGenerator.MaxHill + .0001f, "ground height range in " + layout.Name);
                    for (int i = 1; i < ground.NodeCount; i++)
                    {
                        float slope = Math.Abs(ground.NodeY(i) - ground.NodeY(i - 1)) / (ground.NodeX(i) - ground.NodeX(i - 1));
                        Require(slope <= TowerTerrainGenerator.MaxGroundSlope + .01f, "ground slope " + slope + " at x=" + ground.NodeX(i) + " in " + layout.Name);
                    }
                    foreach (var zone in TowerTerrainGenerator.FlatZones(floor))
                        for (float x = Math.Max(zone[0], TowerTerrainGenerator.TerrainLeft); x <= Math.Min(zone[1], TowerTerrainGenerator.TerrainRight); x += .5f)
                            Require(Math.Abs(layout.GroundAt(x) - Ground) < .0001f, "flat zone broken at x=" + x + " floor " + floor + " seed " + seed);
                    Require(Math.Abs(layout.GroundAt(TowerTerrainGenerator.SpawnX) - Ground) < .0001f && Math.Abs(layout.GroundAt(TowerTerrainGenerator.ExitX) - Ground) < .0001f, "spawn and exit on base ground");
                    Require(!TowerTerrainGenerator.LastWasFallback, "random generation fell back to a fixed layout for seed " + seed + " floor " + floor + " after " + TowerTerrainGenerator.LastAttempts + " attempts; last rejection: " + TowerTerrainGenerator.LastRejection);
                    attempts = Math.Max(attempts, TowerTerrainGenerator.LastAttempts); totalAttempts += TowerTerrainGenerator.LastAttempts;
                    bool crown = false; int entries = 0;
                    foreach (var plan in TowerJumpModel.Reachable(layout, .04f)) if (plan.source < 0) entries++;
                    foreach (var p in layout.Platforms)
                    {
                        var s = p.Surface;
                        Require(Finite(s.Left) && Finite(s.Right) && Finite(s.MinHeight) && Finite(s.MaxHeight), "finite geometry");
                        Require(s.Width >= 2.4f && s.Width <= 5, "landing width " + s.Width + " in " + layout.Name);
                        Require(s.Left >= TowerTerrainGenerator.PlatformLeft && s.Right <= TowerTerrainGenerator.PlatformRight, "world bounds and exit lane in " + layout.Name);
                        Require(Math.Abs(s.CenterX + 12) > s.Width * .5f + .55f, "spawn headroom in " + layout.Name);
                        Require(s.MaxHeight - s.MinHeight <= .55f, "platform relief in " + layout.Name);
                        for (int i = 1; i < s.NodeCount; i++)
                        {
                            float slope = Math.Abs(s.NodeY(i) - s.NodeY(i - 1)) / (s.NodeX(i) - s.NodeX(i - 1));
                            Require(slope <= TowerTerrainGenerator.MaxPlatformSlope + .01f, "platform slope " + slope + " in " + layout.Name);
                        }
                        if (p.Tier <= 3) Require(s.ClearanceOver(layout.Ground, .125f) >= TowerTerrainGenerator.MinClearance - .0001f, "platform clearance over the hills in " + layout.Name);
                        if (p.Tier == 3) crown = true;
                        Require(s.MaxHeight <= 7.6f, "platform height " + s.MaxHeight + " in " + layout.Name);
                    }
                    Require(crown && entries >= 2, "a crown and several low entries in " + layout.Name);
                    if (crown) crowns++;
                    for (int i = 0; i < layout.Platforms.Count; i++)
                        for (int j = i + 1; j < layout.Platforms.Count; j++)
                        {
                            var a = layout.Platforms[i].Surface; var b = layout.Platforms[j].Surface;
                            Require(!TowerTerrainGenerator.TooClose(a, b), "stacked platforms too close in " + layout.Name + ": " + i + "/" + j);
                        }
                }
            Require(crowns == floors, "every floor has a crown");
            Console.WriteLine("  " + floors + " floors; worst case " + attempts + " attempts, average " + (totalAttempts / (float)floors).ToString("F2", CultureInfo.InvariantCulture));
        });
        Test("every platform is reachable with an ordinary jump at both frame steps", () => {
            var watch = Stopwatch.StartNew(); int floors = 0;
            for (int seed = 0; seed < 12; seed++)
                for (int floor = 1; floor <= 20; floor++)
                {
                    var layout = TowerTerrainGenerator.Generate(seed, floor); floors++;
                    Require(TowerJumpModel.Reachable(layout, .04f).Count == layout.Platforms.Count, "unreachable platform at dt=.04 in " + layout.Name + " seed " + seed + " floor " + floor);
                    Require(TowerJumpModel.Reachable(layout, 1f / 60).Count == layout.Platforms.Count, "unreachable platform at 60fps in " + layout.Name + " seed " + seed + " floor " + floor);
                }
            Console.WriteLine("  " + floors + " floors generated and verified in " + watch.ElapsedMilliseconds + " ms");
        });
        Test("walking follows every surface without leaving it", () => {
            for (int seed = 0; seed < 8; seed++)
                for (int floor = 1; floor <= 12; floor++)
                {
                    var layout = TowerTerrainGenerator.Generate(seed, floor);
                    foreach (float speed in new[] { 6f, 19f })
                        foreach (float dt in new[] { .04f, 1f / 60 })
                        {
                            Walk(layout, -1, TowerJumpModel.HeroMinX, TowerJumpModel.HeroMaxX(layout), speed, dt, "ground walk at " + speed);
                            for (int i = 0; i < layout.Platforms.Count; i++)
                            {
                                var s = layout.Platforms[i].Surface;
                                Walk(layout, i, s.Left + .05f, s.Right - .05f, speed, dt, "platform " + i + " walk at " + speed);
                            }
                        }
                    // Stepping past a platform end drops the actor instead of stretching the surface.
                    var first = layout.Platforms[0].Surface;
                    float x = first.Right + TowerPhysics.FootMargin + .3f, y = first.HeightAt(first.Right), velocity = 0; bool grounded = true; int support = 0;
                    TowerPhysics.Step(layout, first.Right, x, ref y, ref velocity, ref grounded, ref support, .04f, -1);
                    Require(support != 0 && (!grounded || support >= 0 || Math.Abs(y - layout.GroundAt(x)) < .0001f), "walking off a platform end did not leave it");
                }
        });
        Test("layouts depend only on run seed and floor and never repeat on adjacent floors", () => {
            var archetypes = new HashSet<string>();
            for (int seed = -4; seed < 40; seed++)
            {
                string previous = null;
                for (int floor = 1; floor <= 40; floor++)
                {
                    var layout = TowerTerrainGenerator.Generate(seed, floor);
                    for (int unrelated = 1; unrelated <= 3; unrelated++) TowerTerrainGenerator.Generate(seed + 700, unrelated);
                    string shape = Shape(layout);
                    Require(shape == Shape(TowerTerrainGenerator.Generate(seed, floor)), "repeatable layout for seed " + seed + " floor " + floor);
                    Require(shape != previous, "adjacent floor repeat: " + seed + "/" + floor);
                    previous = shape; if (floor <= 16) archetypes.Add(layout.Archetype);
                }
            }
            Require(archetypes.Count >= 6, "terrain archetypes vary across floors: " + archetypes.Count);
        });
        Test("support lookup and surface heights agree with the layout", () => {
            var layout = TowerTerrainGenerator.Generate(7, 3);
            for (int i = 0; i < layout.Platforms.Count; i++)
            {
                var s = layout.Platforms[i].Surface;
                for (float x = s.Left; x <= s.Right; x += .5f)
                {
                    Require(layout.SupportAt(x, s.HeightAt(x)) == i, "support lookup on platform " + i);
                    Require(Math.Abs(layout.SurfaceHeight(i, x) - s.HeightAt(x)) < .0001f, "surface height of platform " + i);
                }
            }
            Require(layout.SupportAt(TowerTerrainGenerator.SpawnX, Ground) == -1 && Math.Abs(layout.SurfaceHeight(-1, TowerTerrainGenerator.SpawnX) - Ground) < .0001f, "ground support");
            Require(layout.HighestPlatform > 4 && layout.HighestGround >= Ground, "summary heights");
        });
        Test("traps keep clear of lanes and each other and sit on fitting ground", () => {
            var kinds = new Dictionary<TowerHazardKind, int>(); int floors = 0, traps = 0, pads = 0;
            for (int seed = -4; seed < 16; seed++)
                for (int floor = 1; floor <= 30; floor++)
                {
                    var layout = TowerTerrainGenerator.Generate(seed, floor); floors++;
                    float right = (TowerTerrainGenerator.IsBranchFloor(floor) ? TowerTerrainGenerator.ExitX - 10.7f : TowerTerrainGenerator.ExitX) - TowerTerrainGenerator.HazardExitClear;
                    int floorTraps = 0; TowerHazard? previous = null;
                    foreach (var h in layout.Hazards)
                    {
                        string where = h.Kind + " at x=" + h.X + " seed " + seed + " floor " + floor;
                        kinds[h.Kind] = kinds.ContainsKey(h.Kind) ? kinds[h.Kind] + 1 : 1;
                        if (h.Kind == TowerHazardKind.JumpPad) pads++; else floorTraps++;
                        Require(h.Left >= TowerTerrainGenerator.HazardLeft - .0001f && h.Right <= right + .0001f, "trap outside the trap lane: " + where);
                        if (TowerTerrainGenerator.IsFeatureFloor(floor)) Require(h.Right <= TowerTerrainGenerator.FeatureX - TowerTerrainGenerator.FeatureClear || h.Left >= TowerTerrainGenerator.FeatureX + TowerTerrainGenerator.FeatureClear, "trap on the feature spot: " + where);
                        if (previous.HasValue) Require(h.Left - previous.Value.Right >= TowerTerrainGenerator.HazardGap - .0001f, "traps crowd each other: " + where);
                        previous = h;
                        float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
                        for (float x = h.Left; x <= h.Right + .0001f; x += .125f) { lo = Math.Min(lo, layout.GroundAt(x)); hi = Math.Max(hi, layout.GroundAt(x)); }
                        switch (h.Kind)
                        {
                            case TowerHazardKind.Spikes:
                                Require(h.Support == -1 && h.Width >= TowerTerrainGenerator.SpikeMinWidth - .0001f && h.Width <= TowerTerrainGenerator.SpikeMaxWidth + .0001f, "spike strip size: " + where);
                                Require(hi - lo <= .3f || layout.GroundAt(h.X) < Ground - .5f, "spikes on a steep stretch: " + where);
                                break;
                            case TowerHazardKind.FlameVent: Require(h.Support == -1 && hi - lo <= .08f + .0001f, "vent off level ground: " + where); break;
                            case TowerHazardKind.Rockfall:
                                Require(h.Support == -1 && floor >= 2, "rockfall placement: " + where);
                                foreach (var p in layout.Platforms) Require(p.Surface.Right <= h.Left - .3f || p.Surface.Left >= h.Right + .3f, "rock would fall through a platform: " + where);
                                break;
                            case TowerHazardKind.BoltTotem:
                                Require(floor >= 3 && h.Support >= 0 && h.Support < layout.Platforms.Count, "totem support: " + where);
                                var s = layout.Platforms[h.Support].Surface; var tier = layout.Platforms[h.Support].Tier;
                                Require((tier == 2 || tier == 3) && h.Left >= s.Left && h.Right <= s.Right, "totem on its platform: " + where);
                                break;
                            case TowerHazardKind.JumpPad:
                                bool shelf = false; foreach (var p in layout.Platforms) if (p.Tier == 4 && Math.Abs(p.Surface.CenterX - h.X) < 1.9f) shelf = true;
                                Require(h.Support == -1 && hi - lo <= .12f + .0001f && shelf, "launch pad under a shelf tower: " + where);
                                break;
                        }
                    }
                    Require(floorTraps >= 2 && floorTraps <= TowerTerrainGenerator.HazardCount(floor), "trap budget " + floorTraps + " on seed " + seed + " floor " + floor);
                    traps += floorTraps;
                    var again = TowerTerrainGenerator.Generate(seed, floor).Hazards;
                    Require(again.Count == layout.Hazards.Count, "repeatable traps");
                    for (int i = 0; i < again.Count; i++) Require(again[i].Kind == layout.Hazards[i].Kind && again[i].X == layout.Hazards[i].X, "repeatable traps");
                }
            foreach (TowerHazardKind kind in Enum.GetValues(typeof(TowerHazardKind))) Require(kinds.ContainsKey(kind), "no " + kind + " anywhere");
            Console.WriteLine("  " + floors + " floors; average " + (traps / (float)floors).ToString("F2", CultureInfo.InvariantCulture) + " traps and " + (pads / (float)floors).ToString("F2", CultureInfo.InvariantCulture) + " launch pads");
            var compact = new TowerFloorLayout(1, "compact", 1, new TowerSurface(new[] { -40f, 100f }, new[] { Ground, Ground }), new List<TowerPlatform>());
            Require(compact.ExitX == TowerTerrainGenerator.CompactExitX && compact.Right == 29 && compact.Hazards.Count == 0, "hand-built chambers keep the compact extent");
        });
        Console.WriteLine("TOWER_PLATFORM_TESTS " + passed + " passed, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}
