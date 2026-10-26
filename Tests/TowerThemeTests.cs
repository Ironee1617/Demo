using System;
using System.Collections.Generic;
using PrototypeLab.Roguelike;

public static class TowerThemeTests
{
    private static int passed, failed;
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Test(string name, Action body)
    { try { body(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); } }
    private static void ClearAndChoose(TowerRun run)
    {
        Require(run.ClearFloor() && run.IsRewardPending && (run.IsRecruitOffer || run.Draft == (run.Floor % TowerRun.RewardInterval == 0 ? RewardDraft.Skills : RewardDraft.Stats)), "clear floor and show its stat, fifth-floor skill or recruit cards");
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
        Test("nine chapter themes contain fifty-four regular species, nine bosses and one final evil god", () => {
            Require(TowerThemeCatalog.All.Count == 9, "nine chapter themes");
            var themeIds = new HashSet<TowerThemeId>(); var themeNames = new HashSet<string>();
            var ids = new HashSet<string>(); var names = new HashSet<string>(); int regular = 0, finals = 0;
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
                Require(theme.AirborneVariant >= 0 && theme.AirborneVariant < theme.Monsters.Count, "flyer art slot in " + theme.Name);
                ValidMonster(theme.Boss, ids, names);
                Require(theme.Boss.Kind == 4, "theme boss uses the grounded boss combat kind");
                if (theme.FinalBoss != null)
                {
                    ValidMonster(theme.FinalBoss, ids, names); finals++;
                    Require(theme.FinalBoss.Kind == 4 && theme.FinalBoss.Variant == TowerThemeCatalog.FinalVariant, "final boss art and combat kind");
                    Require(theme.FinalBoss.HealthMultiplier > theme.Boss.HealthMultiplier, "the evil god outlasts its herald");
                }
            }
            foreach (TowerThemeId id in Enum.GetValues(typeof(TowerThemeId)))
                Require(themeIds.Contains(id) == (id != TowerThemeId.Village), "chapter catalog covers every tower theme but not the village: " + id);
            Require(regular == 54 && finals == 1 && ids.Count == 64 && names.Count == 64, "complete regular, boss and final pools");
            Require(TowerThemeCatalog.Get(TowerThemeId.Abyss).FinalBoss != null, "the evil god waits in the abyss");
        });
        Test("ten-floor chapters follow the requested order up to the ninetieth floor", () => {
            var order = new[] { TowerThemeId.Olympus, TowerThemeId.Asgard, TowerThemeId.Dangun, TowerThemeId.Eden, TowerThemeId.Veda,
                TowerThemeId.Papyrus, TowerThemeId.Chinese, TowerThemeId.Hell, TowerThemeId.Abyss };
            Require(TowerThemeCatalog.FloorsPerTheme == 10 && TowerRun.DefaultMaxFloor == 90, "ten floors per theme, ninety floors");
            for (int floor = 1; floor <= 90; floor++)
            {
                Require(TowerThemeCatalog.BlockForFloor(floor) == (floor - 1) / 10, "block index on floor " + floor);
                Require(TowerThemeCatalog.ForFloor(floor).Id == order[(floor - 1) / 10], "theme on floor " + floor);
            }
            Require(TowerThemeCatalog.ForFloor(71).Id == TowerThemeId.Hell && TowerThemeCatalog.ForFloor(80).Id == TowerThemeId.Hell, "hell spans 71-80");
            Require(TowerThemeCatalog.ForFloor(81).Id == TowerThemeId.Abyss && TowerThemeCatalog.ForFloor(90).Id == TowerThemeId.Abyss, "the abyss spans 81-90");
            Require(TowerThemeCatalog.ForFloor(500).Id == TowerThemeId.Abyss, "custom climbs past ninety stay in the abyss");
        });
        Test("a run's theme follows its floor through combat, rewards and stairs", () => {
            var run = new TowerRun(91025, 90, false);
            for (int floor = 1; floor <= run.MaxFloor; floor++)
            {
                var expected = TowerThemeCatalog.ForFloor(floor);
                Require(run.Floor == floor && Object.ReferenceEquals(run.Theme, expected), "theme held on active floor " + floor);
                ClearAndChoose(run);
                Require(Object.ReferenceEquals(run.Theme, expected), "clear and reward preserve theme on " + floor);
                if (floor == run.MaxFloor) { Require(run.IsComplete && !run.AdvanceFloor(), "complete run stops"); break; }
                Require(run.AdvanceFloor(), "advance after chosen reward");
            }
        });
        Test("chapter-ending and mid-chapter floors keep a boss every fifth floor", () => {
            int bosses = 0;
            for (int floor = 1; floor <= 90; floor++)
            {
                Require(TowerDifficulty.IsBossFloor(floor) == (floor % 5 == 0), "boss cadence on floor " + floor);
                if (TowerDifficulty.IsBossFloor(floor)) bosses++;
            }
            Require(bosses == 18 && TowerDifficulty.IsBossFloor(90), "the final floor holds a boss");
        });
        Test("invalid catalog inputs fail", () => {
            bool invalidFloor = false;
            try { TowerThemeCatalog.BlockForFloor(0); } catch (ArgumentOutOfRangeException) { invalidFloor = true; }
            Require(invalidFloor, "invalid inputs are rejected");
        });
        TowerCampaignTests.Run(Test);
        Console.WriteLine("TOWER_THEME_TESTS_" + (failed == 0 ? "OK " : "FAILED ") + passed + "/" + (passed + failed));
        return failed == 0 ? 0 : 1;
    }
}
