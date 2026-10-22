using System;
using System.Collections.Generic;
using PrototypeLab.Roguelike;

public static class TowerThemeTests
{
    private static int passed, failed;
    private sealed class CountingRandom : Random
    {
        public int Seeds, Integers, Doubles;
        public CountingRandom(int seed) : base(seed) { }
        public override int Next() { Seeds++; return base.Next(); }
        public override int Next(int maxValue) { Integers++; return base.Next(maxValue); }
        public override double NextDouble() { Doubles++; return base.NextDouble(); }
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Test(string name, Action body)
    { try { body(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); } }
    private static void ClearAndChoose(TowerRun run)
    {
        Require(run.ClearFloor() && run.IsRewardPending == (run.Floor % TowerRun.RewardInterval == 0), "clear floor and show rewards on fifth floors");
        Require(!run.IsRewardPending || run.ChooseOffer(run.Offers[0].Id), "choose a real eligible reward");
    }
    private static void ValidMonster(TowerMonsterDefinition monster, HashSet<string> ids, HashSet<string> names)
    {
        Require(monster != null && !String.IsNullOrEmpty(monster.Id) && ids.Add(monster.Id), "unique monster identity");
        Require(!String.IsNullOrEmpty(monster.Name) && names.Add(monster.Name), "unique named monster");
        Require(monster.Kind >= 0 && monster.Kind <= 4, "supported combat kind for " + monster.Id);
        float[] stats = { monster.HealthMultiplier, monster.DamageMultiplier, monster.MoveMultiplier, monster.AttackRateMultiplier, monster.BulletMultiplier };
        foreach (float stat in stats) Require(stat > 0 && !Single.IsNaN(stat) && !Single.IsInfinity(stat), "finite positive combat stat for " + monster.Id);
        Require(monster.Variant >= 0, "valid art variant for " + monster.Id);
    }
    public static int Main()
    {
        Test("seven isolated themes contain forty-two regular species and seven bosses", () => {
            Require(TowerThemeCatalog.All.Count == 7, "seven requested themes");
            var themeIds = new HashSet<TowerThemeId>(); var themeNames = new HashSet<string>();
            var ids = new HashSet<string>(); var names = new HashSet<string>(); int regular = 0;
            foreach (var theme in TowerThemeCatalog.All)
            {
                Require(themeIds.Add(theme.Id) && themeNames.Add(theme.Name), "unique theme identity and name");
                Require(Object.ReferenceEquals(theme, TowerThemeCatalog.Get(theme.Id)), "catalog lookup returns its own pool");
                Require(theme.Monsters.Count == 6, "six regular species in " + theme.Name);
                var kinds = new HashSet<int>(); var variants = new HashSet<int>();
                foreach (var monster in theme.Monsters)
                {
                    ValidMonster(monster, ids, names); kinds.Add(monster.Kind);
                    Require(monster.Kind <= 2, "every species stays reachable by a melee-only party");
                    Require(variants.Add(monster.Variant), "distinct regular monster art in " + theme.Name); regular++;
                }
                Require(kinds.Count == 3, "melee, ranged and heavy enemies in " + theme.Name);
                ValidMonster(theme.Boss, ids, names);
                Require(theme.Boss.Kind == 4, "theme boss uses the grounded boss combat kind");
            }
            foreach (TowerThemeId id in Enum.GetValues(typeof(TowerThemeId))) Require(themeIds.Contains(id), "missing requested theme " + id);
            Require(regular == 42 && ids.Count == 49 && names.Count == 49, "complete isolated regular and boss pools");
        });
        Test("first theme is random and repeatable for a seed", () => {
            var observed = new HashSet<TowerThemeId>();
            for (int seed = 0; seed < 700; seed++)
            {
                var first = new TowerRun(seed); var second = new TowerRun(seed);
                Require(first.Theme != null && first.Theme.Id == second.Theme.Id, "repeatable initial theme for seed " + seed);
                observed.Add(first.Theme.Id);
            }
            Require(observed.Count == 7, "all seven themes are reachable as the first block");
        });
        Test("each roll excludes its predecessor while all six alternatives remain reachable", () => {
            var random = new Random(8034);
            foreach (var previous in TowerThemeCatalog.All)
            {
                var observed = new HashSet<TowerThemeId>();
                for (int attempt = 0; attempt < 700; attempt++)
                { var next = TowerThemeCatalog.Roll(random, previous.Id); Require(next.Id != previous.Id, "previous theme excluded"); observed.Add(next.Id); }
                Require(observed.Count == 6, "all six remaining choices stay eligible after " + previous.Name);
            }
        });
        Test("themes stay fixed through combat and rewards and change only on five-floor boundaries", () => {
            var run = new TowerRun(91025, 1000); var blockTheme = run.Theme; int changes = 0;
            Require(run.Reroll(0) && run.ChooseOffer(run.Offers[0].Id) && Object.ReferenceEquals(run.Theme, blockTheme), "opening recruit preserves theme");
            for (int floor = 1; floor <= run.MaxFloor; floor++)
            {
                Require(run.Floor == floor && Object.ReferenceEquals(run.Theme, blockTheme), "theme held on active floor " + floor);
                Require(!run.AdvanceFloor() && Object.ReferenceEquals(run.Theme, blockTheme), "uncleared stairs preserve theme");
                Require(run.ClearFloor() && Object.ReferenceEquals(run.Theme, blockTheme), "clear preserves theme");
                if (run.IsRewardPending)
                {
                    Require(!run.AdvanceFloor() && run.Reroll(0) && Object.ReferenceEquals(run.Theme, blockTheme), "pending reward and reroll preserve theme");
                    Require(run.ChooseOffer(run.Offers[0].Id) && Object.ReferenceEquals(run.Theme, blockTheme), "selection preserves theme");
                }
                if (floor == run.MaxFloor) { Require(run.IsComplete && !run.AdvanceFloor() && Object.ReferenceEquals(run.Theme, blockTheme), "complete run preserves final theme"); break; }
                Require(run.AdvanceFloor(), "advance after chosen reward");
                if (floor % TowerThemeCatalog.FloorsPerTheme == 0)
                { Require(run.Theme.Id != blockTheme.Id, "adjacent blocks never repeat at " + floor); blockTheme = run.Theme; changes++; }
                else Require(Object.ReferenceEquals(run.Theme, blockTheme), "no mid-block reroll at " + floor);
            }
            Require(changes == 199, "one transition for every completed five-floor block");
        });
        Test("theme transitions do not consume the reward random stream", () => {
            var random = new CountingRandom(773); var run = new TowerRun(random, 16, false);
            Require(random.Seeds == 1 && random.Integers == 0 && random.Doubles == 0, "only one independent theme seed is drawn");
            for (int floor = 1; floor <= 15; floor++)
            {
                ClearAndChoose(run); int seeds = random.Seeds, integers = random.Integers, doubles = random.Doubles;
                Require(run.AdvanceFloor(), "advance random-stream scenario");
                Require(random.Seeds == seeds && random.Integers == integers && random.Doubles == doubles, "theme selection does not disturb future rewards at " + floor);
            }
        });
        Test("reward rerolls and choices cannot change a seeded theme sequence", () => {
            var first = new TowerRun(20918, 101); var second = new TowerRun(20918, 101);
            Require(first.ChooseOffer(first.Offers[0].Id) && second.Reroll(1) && second.ChooseOffer(second.Offers[1].Id), "different opening recruits");
            for (int floor = 1; floor <= 100; floor++)
            {
                Require(first.Theme.Id == second.Theme.Id, "same seeded theme despite different rewards on floor " + floor);
                ClearAndChoose(first);
                Require(second.ClearFloor(), "clear second sequence");
                if (second.IsRewardPending)
                {
                    Require(second.Reroll(0) && second.Reroll(1) && second.Reroll(2), "vary reward random consumption");
                    Require(second.ChooseOffer(second.Offers[2].Id), "choose a different reward slot");
                }
                Require(first.AdvanceFloor() && second.AdvanceFloor(), "advance both theme sequences");
            }
            Require(first.Theme.Id == second.Theme.Id, "same seeded theme after twenty block changes");
        });
        Test("five-floor blocks and theme-ending boss floors agree", () => {
            Require(TowerThemeCatalog.FloorsPerTheme == 5, "five floors per theme"); int bosses = 0;
            for (int floor = 1; floor <= 100; floor++)
            {
                Require(TowerThemeCatalog.BlockForFloor(floor) == (floor - 1) / 5, "block index on floor " + floor);
                Require(TowerDifficulty.IsBossFloor(floor) == (floor % 5 == 0), "one boss at each theme ending");
                if (TowerDifficulty.IsBossFloor(floor)) bosses++;
            }
            Require(bosses == 20 && TowerDifficulty.IsBossFloor(100), "final floor includes a theme boss");
        });
        Test("invalid catalog inputs fail without a random selection", () => {
            bool invalidFloor = false, nullRandom = false;
            try { TowerThemeCatalog.BlockForFloor(0); } catch (ArgumentOutOfRangeException) { invalidFloor = true; }
            try { TowerThemeCatalog.Roll(null); } catch (ArgumentNullException) { nullRandom = true; }
            Require(invalidFloor && nullRandom, "invalid inputs are rejected");
        });
        Console.WriteLine("TOWER_THEME_TESTS_" + (failed == 0 ? "OK " : "FAILED ") + passed + "/" + (passed + failed));
        return failed == 0 ? 0 : 1;
    }
}
