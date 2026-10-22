using System;
using System.Collections.Generic;
using PrototypeLab.Roguelike;

public static class TowerMythTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    // Clears exactly one more floor, taking any card set that floor offers.
    private static void ClearNext(TowerRun run)
    {
        if (run.IsRewardPending) Check(run.ChooseOffer(run.Offers[0].Id), "finish pending card");
        if (run.IsFloorCleared) Check(run.AdvanceFloor(), "advance to earn next floor");
        Check(run.ClearFloor(), "clear floor fixture");
    }
    private static void Earn(TowerProgress progress, TowerRun run, int floors)
    {
        for (int i = 0; i < floors; i++) {
            ClearNext(run);
            Check(progress.RecordFloorClear(run) == 1, "one point for every newly cleared floor");
        }
    }

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
        test("floor points are awarded once per cleared floor and survive saving", () => {
            var progress = new TowerProgress(); var run = new TowerRun(37, 20, false);
            Check(progress.RecordFloorClear(null) == 0 && progress.RecordFloorClear(run) == 0 && progress.Points == 0, "uncleared floors earn nothing");
            ClearNext(run);
            Check(progress.RecordFloorClear(run) == 1 && progress.Points == 1 && progress.RecordFloorClear(run) == 0, "clear awards exactly once");
            progress = TowerProgress.Load(progress.Save());
            Check(progress.Points == 1 && progress.RecordFloorClear(run) == 0, "persisted receipt prevents duplicate award");
            Earn(progress, run, 6); Check(progress.Points == 7, "later floors, including card floors, still earn points");
            var newRun = new TowerRun(37, 20, false);
            Check(newRun.RunId != run.RunId && progress.RecordFloorClear(newRun) == 0, "same seed starts independent account receipt");
            Earn(progress, newRun, 2); Check(progress.Points == 9 && progress.RecordFloorClear(run) == 0, "new run earns without replaying old run");
            progress = TowerProgress.Load(progress.Save());
            Check(progress.Points == 9 && progress.RecordFloorClear(newRun) == 0 && progress.RecordFloorClear(run) == 0, "multiple run receipts survive saving");
        });
        test("batched floor reconciliation awards every unrecorded floor exactly once including final floor", () => {
            var progress = new TowerProgress(); var run = new TowerRun(91, 5, false);
            for (int i = 0; i < 5; i++) ClearNext(run);
            Check(run.ChooseOffer(run.Offers[0].Id) && run.IsComplete && progress.RecordFloorClear(run) == 5 && progress.Points == 5, "completed run credits all missed clears");
            Check(progress.RecordFloorClear(run) == 0 && !run.ClearFloor() && progress.Points == 5, "completion cannot mint another clear");
        });
        test("points are spent exactly and never go negative", () => {
            var progress = new TowerProgress(); var run = new TowerRun(61, 100, false); Earn(progress, run, 12);
            Check(progress.Points == 12 && !progress.TrySpend(-1) && !progress.TrySpend(13) && progress.Points == 12, "invalid spending leaves points");
            Check(progress.TrySpend(5) && progress.Points == 7 && progress.TrySpend(7) && progress.Points == 0 && !progress.TrySpend(1), "exact spending");
            progress = TowerProgress.Load(progress.Save());
            Check(progress.Points == 0 && progress.RecordFloorClear(run) == 0, "spent balance and receipts persist");
        });
        test("malformed saves recover an empty account and legacy saves keep points and receipts", () => {
            foreach (string saved in new[] { null, "", "garbage", "3|99|", "2|-5|", "2|oops|", "1|7|eden|eden" })
                Check(TowerProgress.Load(saved).Points == 0, "invalid saves recover an empty account");
            var run = new TowerRun(12, 10, false); var progress = new TowerProgress(); Earn(progress, run, 3);
            string receipt = run.RunId + ":3";
            var legacy = TowerProgress.Load("1|7|olympus|eden,olympus|" + receipt);
            Check(legacy.Points == 7 && legacy.RecordFloorClear(run) == 0, "mythology purchase saves keep their points and receipts");
            Check(TowerProgress.Load(legacy.Save()).Points == 7 && legacy.Save().StartsWith("2|7|"), "legacy saves upgrade to the points format");
        });
    }
}
