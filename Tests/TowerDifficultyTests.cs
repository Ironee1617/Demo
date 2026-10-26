using System;
using PrototypeLab.Roguelike;

public static class TowerDifficultyTests
{
    private static int passed;
    public static int Main()
    {
        try
        {
            Test("Normal counts increase gradually and bosses add one enemy", Counts);
            Test("Twenty bosses end the five-floor theme blocks", BossFloors);
            Test("All five regular stat multipliers increase gradually", Multipliers);
            Test("First-floor baseline and hundredth-floor values", Endpoints);
            Test("Boss health increases after the first boss", BossHealth);
            Test("Out-of-range inputs clamp to supported floors", Clamp);
            Test("Elite floors and the ten-floor trial keep their own pace", EliteAndTrial);
            Console.WriteLine("TOWER_DIFFICULTY_TESTS_OK " + passed + "/7"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Test(string name, Action body) { body(); passed++; Console.WriteLine("PASS " + name); }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Near(float actual, float expected) { Require(Math.Abs(actual - expected) < .001f, "Expected " + expected + ", got " + actual); }
    private static void Counts()
    {
        int regular = 0;
        for (int floor = 1; floor <= 100; floor++)
        {
            int next = TowerDifficulty.RegularCount(floor), all = next + (TowerDifficulty.IsBossFloor(floor) ? 1 : 0);
            Require(next >= regular, "Normal enemy count decreased at floor " + floor);
            Require(next == 6 + (floor - 1) / 12, "Unexpected normal count at floor " + floor);
            Require(TowerDifficulty.FlyerCount(floor) >= (floor >= 2 ? 1 : 0) && TowerDifficulty.FlyerCount(floor) <= 3, "Unexpected flyer count at floor " + floor);
            Require(all == next + (floor % 5 == 0 ? 1 : 0), "Unexpected total count at floor " + floor);
            regular = next;
        }
    }
    private static void BossFloors()
    {
        int bosses = 0;
        for (int floor = 1; floor <= 100; floor++)
        { bool expected = floor % 5 == 0; Require(TowerDifficulty.IsBossFloor(floor) == expected, "Wrong boss floor " + floor); if (expected) bosses++; }
        Require(bosses == 20 && TowerDifficulty.IsBossFloor(100), "Each theme ends with a boss, including floor 100.");
    }
    private static float[] Stats(int floor)
    { return new[] { TowerDifficulty.HealthMultiplier(floor), TowerDifficulty.DamageMultiplier(floor), TowerDifficulty.MoveMultiplier(floor), TowerDifficulty.BulletMultiplier(floor), TowerDifficulty.AttackRateMultiplier(floor) }; }
    private static void Multipliers()
    {
        float[] previous = Stats(1);
        for (int floor = 2; floor <= 100; floor++)
        { var next = Stats(floor); for (int i = 0; i < next.Length; i++) Require(next[i] > previous[i] && next[i] - previous[i] < .02f, "Non-gradual stat change at " + floor); previous = next; }
    }
    private static void Endpoints()
    {
        foreach (float value in Stats(1)) Near(value, 1);
        var final = Stats(100); float[] expected = { 2.584f, 1.792f, 1.198f, 1.1188f, 1.198f };
        for (int i = 0; i < final.Length; i++) Near(final[i], expected[i]);
        Require(TowerDifficulty.RegularCount(1) == 6 && TowerDifficulty.RegularCount(100) == 14, "Incorrect count endpoints.");
    }
    private static void BossHealth()
    {
        Near(TowerDifficulty.BossHealth(5), 420); Near(TowerDifficulty.BossHealth(10), 459.9f); Near(TowerDifficulty.BossHealth(100), 1178.1f);
        float previous = 0; for (int floor = 5; floor <= 100; floor += 5) { float hp = TowerDifficulty.BossHealth(floor); Require(hp > previous, "Boss HP did not increase."); previous = hp; }
    }
    private static void Clamp()
    {
        Require(TowerDifficulty.RegularCount(-10) == TowerDifficulty.RegularCount(1) && TowerDifficulty.RegularCount(999) == TowerDifficulty.RegularCount(100), "Counts did not clamp.");
        var low = Stats(-10); var high = Stats(999); var first = Stats(1); var last = Stats(100);
        for (int i = 0; i < low.Length; i++) { Near(low[i], first[i]); Near(high[i], last[i]); }
    }
    private static void EliteAndTrial()
    {
        for (int floor = 1; floor <= 100; floor++) Require(TowerDifficulty.EliteCount(floor) >= 3 && TowerDifficulty.EliteCount(floor) <= TowerDifficulty.RegularCount(floor), "Elite floors trade numbers for strength.");
        Require(TowerDifficulty.EliteHealth > 1 && TowerDifficulty.EliteDamage > 1, "Elites are stronger.");
        Require(TowerDifficulty.TrialFloor(1) == 1 && TowerDifficulty.TrialFloor(10) == 37, "Trial floors sample the long curve.");
        Require(TowerDifficulty.RegularCount(TowerDifficulty.TrialFloor(10)) == 9, "Trial floor ten fields nine enemies along its long floor.");
        Near(TowerDifficulty.BossHealth(TowerDifficulty.TrialBossFloor(5)), 420); Near(TowerDifficulty.BossHealth(TowerDifficulty.TrialBossFloor(10)), 579.6f);
    }
}
