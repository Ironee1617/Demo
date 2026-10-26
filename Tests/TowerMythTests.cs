using System;
using System.Collections.Generic;
using PrototypeLab.Roguelike;

public static class TowerMythTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    public static void Register(Action<string, Action> test)
    {
        test("ten mythologies own five independent humanoid companions each, all open from the start", () => {
            Check(TowerMythCatalog.Definitions.Count == 10, "ten mythologies");
            string[] expected = { "eden", "olympus", "asgard", "india", "egypt", "china", "korea", "solomon", "ireland", "zodiac" };
            var myths = new HashSet<string>(); var companions = new HashSet<string>();
            foreach (var myth in TowerMythCatalog.Definitions) {
                Check(myths.Add(myth.Id) && TowerMythCatalog.Get(myth.Id) == myth, "unique resolvable myth");
                Check(!String.IsNullOrEmpty(myth.Name) && !String.IsNullOrEmpty(myth.Description), "complete myth presentation");
                Check(myth.CompanionIds.Count == 5, "five companions per myth");
                Check(TowerMythCatalog.GetSynergy(myth.Id) != null, "every mythology has a synergy");
                int[] tiers = new int[3];
                foreach (string id in myth.CompanionIds) {
                    var c = TowerRun.GetDefinition(id);
                    Check(companions.Add(id) && c != null && c.MythId == myth.Id, "companion belongs to exactly one mythology");
                    Check(!String.IsNullOrEmpty(c.InnateTrait) && !String.IsNullOrEmpty(c.InnateDescription), "individual innate trait is described");
                    tiers[(int)c.Tier]++;
                }
                Check(tiers[0] == 2 && tiers[1] == 2 && tiers[2] == 1, "all rarity pools available in every mythology");
            }
            foreach (string id in expected) Check(myths.Contains(id), "requested mythology present: " + id);
            Check(companions.Count == TowerRun.Definitions.Count && companions.Count == 50, "every character listed once");
            Check(TowerMythCatalog.Synergies.Count == 10 && TowerMythCatalog.GetSynergy("missing") == null, "one synergy per mythology");
            Check(TowerMythCatalog.Get("missing") == null && TowerMythCatalog.Get(null) == null, "unknown lookup is safe");
        });
        test("gold adds up from pickups, is spent exactly, saturates and survives saving", () => {
            var progress = new TowerProgress();
            Check(progress.Gold == 0 && progress.AddGold(0) == 0 && progress.AddGold(-4) == 0 && progress.Gold == 0, "nothing or a negative amount adds nothing");
            Check(progress.AddGold(7) == 7 && progress.AddGold(5) == 5 && progress.Gold == 12, "pickups add up");
            Check(!progress.TrySpend(-1) && !progress.TrySpend(13) && progress.Gold == 12, "invalid spending leaves the balance");
            Check(progress.TrySpend(5) && progress.Gold == 7, "exact spending");
            progress = TowerProgress.Load(progress.Save());
            Check(progress.Gold == 7 && progress.Save() == "3|7", "the balance persists");
            Check(progress.TrySpend(7) && progress.Gold == 0 && !progress.TrySpend(1), "a wallet never goes negative");
            var full = TowerProgress.Load("3|" + (Int32.MaxValue - 2));
            Check(full.AddGold(10) == 2 && full.Gold == Int32.MaxValue, "the wallet saturates instead of overflowing");
        });
        test("malformed saves recover an empty wallet and floor-point saves convert to gold", () => {
            foreach (string saved in new[] { null, "", "garbage", "3|", "3|-5", "3|oops", "3|5|x", "4|9", "2|-5|", "2|oops|", "1|7|eden|eden" })
                Check(TowerProgress.Load(saved).Gold == 0, "invalid saves recover an empty wallet: " + saved);
            var run = new TowerRun(12, 10, false);
            var points = TowerProgress.Load("2|7|" + run.RunId + ":3");
            Check(points.Gold == 7 * TowerProgress.LegacyPointGold && points.Save() == "3|70", "floor points keep their buying power as gold");
            var mythology = TowerProgress.Load("1|3|olympus|eden,olympus|" + run.RunId + ":3");
            Check(mythology.Gold == 3 * TowerProgress.LegacyPointGold, "mythology purchase saves convert their points too");
            Check(TowerProgress.Load("2|" + Int32.MaxValue + "|").Gold == Int32.MaxValue, "conversion saturates");
        });
    }
}
