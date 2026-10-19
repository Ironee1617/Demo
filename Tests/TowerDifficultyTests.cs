using System;
using PrototypeLab.Roguelike;

public static class TowerDifficultyTests
{
    private static int passed;
    public static int Main()
    {
        try
        {
            Test("Normal and total enemy counts never decrease", Counts);
            Test("Exactly eight boss floors at multiples of three", BossFloors);
            Test("All five regular stat multipliers increase gradually", Multipliers);
            Test("First-floor baseline and final-floor values", Endpoints);
            Test("Boss health increases after the first boss", BossHealth);
            Test("Out-of-range inputs clamp to supported floors", Clamp);
            Console.WriteLine("TOWER_DIFFICULTY_TESTS_OK " + passed + "/6"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Test(string name, Action body) { body(); passed++; Console.WriteLine("PASS " + name); }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Near(float actual, float expected) { Require(Math.Abs(actual - expected) < .001f, "Expected " + expected + ", got " + actual); }
    private static void Counts()
    {
        int regular = 0, total = 0;
        for (int floor = 1; floor <= 25; floor++)
        {
            int next = TowerDifficulty.RegularCount(floor), all = next + (TowerDifficulty.IsBossFloor(floor) ? 1 : 0);
            Require(next >= regular && all >= total, "Enemy count decreased at floor " + floor);
            Require(next == 4 + (floor - 1) / 3, "Unexpected normal count at floor " + floor);
            regular = next; total = all;
        }
    }
    private static void BossFloors()
    {
        int bosses = 0;
        for (int floor = 1; floor <= 25; floor++)
        { bool expected = floor % 3 == 0; Require(TowerDifficulty.IsBossFloor(floor) == expected, "Wrong boss floor " + floor); if (expected) bosses++; }
        Require(bosses == 8 && !TowerDifficulty.IsBossFloor(25), "Boss cadence must end at 24.");
    }
    private static float[] Stats(int floor)
    { return new[] { TowerDifficulty.HealthMultiplier(floor), TowerDifficulty.DamageMultiplier(floor), TowerDifficulty.MoveMultiplier(floor), TowerDifficulty.BulletMultiplier(floor), TowerDifficulty.AttackRateMultiplier(floor) }; }
    private static void Multipliers()
    {
        float[] previous = Stats(1);
        for (int floor = 2; floor <= 25; floor++)
        { var next = Stats(floor); for (int i = 0; i < next.Length; i++) Require(next[i] > previous[i] && next[i] - previous[i] < .061f, "Non-gradual stat change at " + floor); previous = next; }
    }
    private static void Endpoints()
    {
        foreach (float value in Stats(1)) Near(value, 1);
        var final = Stats(25); float[] expected = { 2.44f, 1.72f, 1.192f, 1.12f, 1.192f };
        for (int i = 0; i < final.Length; i++) Near(final[i], expected[i]);
        Require(TowerDifficulty.RegularCount(1) == 4 && TowerDifficulty.RegularCount(25) == 12, "Incorrect count endpoints.");
    }
    private static void BossHealth()
    {
        Near(TowerDifficulty.BossHealth(3), 420); Near(TowerDifficulty.BossHealth(6), 514.5f); Near(TowerDifficulty.BossHealth(24), 1081.5f);
        float previous = 0; for (int floor = 3; floor <= 24; floor += 3) { float hp = TowerDifficulty.BossHealth(floor); Require(hp > previous, "Boss HP did not increase."); previous = hp; }
    }
    private static void Clamp()
    {
        Require(TowerDifficulty.RegularCount(-10) == TowerDifficulty.RegularCount(1) && TowerDifficulty.RegularCount(99) == TowerDifficulty.RegularCount(25), "Counts did not clamp.");
        var low = Stats(-10); var high = Stats(99); var first = Stats(1); var last = Stats(25);
        for (int i = 0; i < low.Length; i++) { Near(low[i], first[i]); Near(high[i], last[i]); }
    }
}
