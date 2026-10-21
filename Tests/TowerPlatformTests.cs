using System;
using System.Collections.Generic;
using System.Globalization;
using PrototypeLab.Roguelike;

public static class TowerPlatformTests
{
    private const float Ground = -3, Speed = 6, Jump = 10.7f, Gravity = 25;
    private static int passed, failed;
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Test(string name, Action body)
    { try { body(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); } }
    private static float Clamp(float value, float low, float high) { return Math.Max(low, Math.Min(high, value)); }
    private static bool Finite(float value) { return !Single.IsNaN(value) && !Single.IsInfinity(value); }

    // Use the player's weakest ordinary movement, no dash, edge catches or upgrades.
    // Evaluate every intervening platform so a theoretical arc cannot pass a ledge
    // that would actually catch the player before the intended destination.
    private static bool CanJump(TowerPlatformPattern pattern, int source, int target, float dt)
    {
        var destination = pattern.Platforms[target];
        float sourceY = source < 0 ? Ground : pattern.Platforms[source].Y;
        if (destination.Y - sourceY > 2.05f || destination.Y < sourceY - .1f) return false;
        float sourceLeft = source < 0 ? destination.X - 3 : pattern.Platforms[source].X - pattern.Platforms[source].Width * .5f + .3f;
        float sourceRight = source < 0 ? destination.X + 3 : pattern.Platforms[source].X + pattern.Platforms[source].Width * .5f - .3f;
        for (float launch = sourceLeft; launch <= sourceRight + .001f; launch += .15f)
            for (int aim = -1; aim <= 1; aim++)
            {
                float aimX = destination.X + aim * (destination.Width * .5f - .4f);
                float x = launch, y = sourceY, velocity = Jump;
                for (int frame = 0; frame < 100; frame++)
                {
                    x += Clamp(aimX - x, -Speed * dt, Speed * dt);
                    velocity -= Gravity * dt;
                    float next = y + velocity * dt, supportY = Ground;
                    int support = -1;
                    if (velocity <= 0)
                        for (int i = 0; i < pattern.Platforms.Count; i++)
                        {
                            var ledge = pattern.Platforms[i];
                            if (Math.Abs(x - ledge.X) < ledge.Width * .5f + .18f && y >= ledge.Y - .02f && next <= ledge.Y && ledge.Y > supportY)
                            { supportY = ledge.Y; support = i; }
                        }
                    if (next <= supportY)
                    {
                        if (support == target && Math.Abs(x - destination.X) <= destination.Width * .5f - .25f) return true;
                        break;
                    }
                    y = next;
                }
            }
        return false;
    }
    private static void CheckReachable(TowerPlatformPattern pattern, float dt)
    {
        var reached = new HashSet<int>(); var queue = new Queue<int>(); queue.Enqueue(-1);
        while (queue.Count > 0)
        {
            int source = queue.Dequeue();
            for (int target = 0; target < pattern.Platforms.Count; target++)
                if (!reached.Contains(target) && CanJump(pattern, source, target, dt))
                { reached.Add(target); queue.Enqueue(target); }
        }
        for (int i = 0; i < pattern.Platforms.Count; i++)
            Require(reached.Contains(i), pattern.Name + " unreachable ledge " + i + " at dt=" + dt);
    }

    public static int Main()
    {
        Test("sixteen distinct authored layouts have readable dodge surfaces", () => {
            Require(TowerPlatformCatalog.All.Count == 16, "exactly sixteen selectable patterns");
            var ids = new HashSet<int>(); var names = new HashSet<string>(); var shapes = new HashSet<string>();
            foreach (var pattern in TowerPlatformCatalog.All)
            {
                Require(ids.Add(pattern.Id) && !String.IsNullOrWhiteSpace(pattern.Name) && names.Add(pattern.Name), "unique pattern identity");
                Require(pattern.Platforms.Count >= 12 && pattern.Platforms.Count <= 27, "extended platform count in " + pattern.Name);
                var pieces = new List<string>(); var heights = new HashSet<float>(); int entries = 0;
                float minX = Single.PositiveInfinity, maxX = Single.NegativeInfinity;
                foreach (var platform in pattern.Platforms)
                {
                    Require(Finite(platform.X) && Finite(platform.Y) && Finite(platform.Width), "finite geometry");
                    Require(platform.Width >= 2.4f && platform.Width <= 5, "comfortable landing width in " + pattern.Name);
                    Require(platform.X - platform.Width * .5f >= -15.4f && platform.X + platform.Width * .5f <= 25, "world bounds and exit lane in " + pattern.Name);
                    Require(platform.Y >= Ground + .9f && platform.Y <= 5.21f, "six-tier height in " + pattern.Name);
                    Require(Math.Abs(platform.X + 12) > platform.Width * .5f + .55f, "spawn headroom in " + pattern.Name);
                    heights.Add(platform.Y); if (platform.Y - Ground <= 1.65f) entries++;
                    minX = Math.Min(minX, platform.X); maxX = Math.Max(maxX, platform.X);
                    pieces.Add(platform.X.ToString("R", CultureInfo.InvariantCulture) + ":" + platform.Y.ToString("R", CultureInfo.InvariantCulture) + ":" + platform.Width.ToString("R", CultureInfo.InvariantCulture));
                }
                Require(heights.Count == 6 && heights.Contains(2.4f) && heights.Contains(3.8f) && heights.Contains(5.2f), "three additional upper tiers in " + pattern.Name);
                Require(entries >= 2 && maxX - minX >= 25, "multiple entry and elevated dodge routes in " + pattern.Name);
                pieces.Sort(StringComparer.Ordinal); Require(shapes.Add(String.Join(";", pieces)), "distinct authored geometry in " + pattern.Name);
                for (int i = 0; i < pattern.Platforms.Count; i++)
                    for (int j = i + 1; j < pattern.Platforms.Count; j++)
                    {
                        var a = pattern.Platforms[i]; var b = pattern.Platforms[j];
                        if (Math.Abs(a.Y - b.Y) < .1f)
                            Require(Math.Abs(a.X - b.X) > (a.Width + b.Width) * .5f + .2f, "separate platforms on the same tier in " + pattern.Name);
                    }
            }
        });
        Test("every ledge is reachable with ordinary jump at the maximum frame step", () => {
            var errors = new List<string>();
            foreach (var pattern in TowerPlatformCatalog.All) try { CheckReachable(pattern, .04f); } catch (Exception ex) { errors.Add(ex.Message); }
            Require(errors.Count == 0, String.Join("; ", errors));
        });
        Test("every ledge is also reachable at sixty frames per second", () => {
            foreach (var pattern in TowerPlatformCatalog.All) CheckReachable(pattern, 1f / 60);
        });
        Test("every seed covers the catalog and adjacent floors never repeat", () => {
            for (int seed = -32; seed < 96; seed++)
            {
                var seen = new HashSet<int>(); int previous = Int32.MinValue;
                for (int floor = 1; floor <= 128; floor++)
                {
                    var pattern = TowerPlatformCatalog.Select(seed, floor);
                    Require(pattern != null && pattern.Id != previous, "adjacent floor repeat: " + seed + "/" + floor);
                    Require(Object.ReferenceEquals(pattern, TowerPlatformCatalog.Select(seed, floor)), "repeatable selection");
                    previous = pattern.Id; if (floor <= 16) seen.Add(pattern.Id);
                }
                Require(seen.Count == 16, "all authored patterns appear in a sixteen-floor bag for seed " + seed);
            }
        });
        Test("selection depends only on run seed and floor", () => {
            var firstChoices = new HashSet<int>();
            for (int seed = 0; seed < 128; seed++)
            {
                var expected = TowerPlatformCatalog.Select(seed, 37);
                for (int unrelated = 1; unrelated <= 20; unrelated++) TowerPlatformCatalog.Select(seed + 700, unrelated);
                Require(Object.ReferenceEquals(expected, TowerPlatformCatalog.Select(seed, 37)), "independent of other selection calls");
                firstChoices.Add(TowerPlatformCatalog.Select(seed, 1).Id);
            }
            Require(firstChoices.Count == 16, "run seeds provide all sixteen first-floor choices");
        });
        Console.WriteLine("TOWER_PLATFORM_TESTS " + passed + " passed, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}
