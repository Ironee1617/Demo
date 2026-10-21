using System;
using System.Collections.Generic;
using PrototypeLab.Roguelike;

public static class TowerMythTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void ClearedReward(TowerRun run)
    {
        if (run.IsFloorCleared) Check(run.AdvanceFloor(), "advance to earn next floor");
        Check(run.ClearFloor(), "clear floor fixture");
    }
    private static void FinishReward(TowerRun run)
    { Check(run.IsRewardPending && run.ChooseOffer(run.Offers[0].Id), "finish reward fixture"); }
    private static void Earn(TowerProgress progress, TowerRun run, int floors)
    {
        for (int i = 0; i < floors; i++) {
            ClearedReward(run);
            Check(progress.RecordFloorClear(run) == 1, "one point for every newly cleared floor");
            FinishReward(run);
        }
    }
    private static void CheckOffer(TowerRun run, RewardOffer offer)
    {
        if (offer.OwnerId == "player") return;
        var definition = TowerRun.GetDefinition(offer.OwnerId);
        Check(definition != null && definition.MythId == run.SelectedMythId, "foreign mythology excluded from reward pool");
        if (offer.Kind != RewardKind.Recruit) Check(run.GetCompanion(offer.OwnerId) != null, "augment must belong to an owned companion");
    }

    public static void Register(Action<string, Action> test)
    {
        test("ten mythologies own five independent humanoid companions each", () => {
            Check(TowerMythCatalog.Definitions.Count == 10, "ten mythologies");
            string[] expected = { "eden", "olympus", "asgard", "india", "egypt", "china", "korea", "solomon", "ireland", "zodiac" };
            var myths = new HashSet<string>(); var companions = new HashSet<string>();
            foreach (var myth in TowerMythCatalog.Definitions) {
                Check(myths.Add(myth.Id) && TowerMythCatalog.Get(myth.Id) == myth, "unique resolvable myth");
                Check(!String.IsNullOrEmpty(myth.Name) && !String.IsNullOrEmpty(myth.Description), "complete myth presentation");
                Check(myth.CompanionIds.Count == 5, "five companions per myth");
                Check(myth.Price == (myth.Id == TowerMythCatalog.StarterMythId ? 0 : TowerMythCatalog.MythPrice) && myth.Price >= 0, "configured shop prices");
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
            Check(TowerMythCatalog.Get("missing") == null && TowerMythCatalog.Get(null) == null, "unknown lookup is safe");
        });
        test("selected mythology gates recruitment and rerolls for every roster", () => {
            foreach (var myth in TowerMythCatalog.Definitions) {
                var run = new TowerRun(415, 100, myth.Id);
                Check(run.SelectedMythId == myth.Id, "run retains selected mythology");
                bool recruited = false, ownedAugment = false;
                for (int floor = 0; floor < 40; floor++) {
                    ClearedReward(run);
                    foreach (var offer in run.Offers) CheckOffer(run, offer);
                    for (int slot = 0; slot < 3; slot++) {
                        Check(run.Reroll(slot), "every mythology supports slot rerolls");
                        foreach (var offer in run.Offers) CheckOffer(run, offer);
                    }
                    var choice = run.Offers[0];
                    foreach (var offer in run.Offers) if (offer.Kind == RewardKind.Recruit) { choice = offer; break; }
                    recruited |= choice.Kind == RewardKind.Recruit;
                    ownedAugment |= choice.OwnerId != "player" && choice.Kind != RewardKind.Recruit;
                    Check(run.ChooseOffer(choice.Id), "apply mythology reward");
                }
                Check(recruited && ownedAugment && run.Companions.Count == TowerRun.MaxCompanions, "selected roster recruits and progresses");
                foreach (var ally in run.Companions) Check(ally.Definition.MythId == myth.Id, "owned party belongs to selected mythology");
            }
            bool invalid = false, missing = false;
            try { new TowerRun(1, 25, "missing"); } catch (ArgumentException) { invalid = true; }
            try { new TowerRun(1, 25, null); } catch (ArgumentException) { missing = true; }
            Check(invalid && missing, "invalid mythology cannot silently start a different roster");
        });
        test("new account requires an explicit unlocked myth selection and rejects unaffordable purchases", () => {
            var progress = new TowerProgress(); var locked = TowerMythCatalog.Definitions[1];
            Check(progress.Points == 0 && !progress.HasSelectedMyth && String.IsNullOrEmpty(progress.SelectedMythId), "new account waits for selection");
            Check(progress.IsUnlocked(TowerMythCatalog.StarterMythId) && !progress.IsUnlocked(locked.Id), "free starter and locked shop");
            Check(!progress.TrySelect(locked.Id) && !progress.TryPurchase(locked.Id) && !progress.TryPurchase("missing") && !progress.TrySelect(null), "reject invalid or unaffordable changes");
            Check(progress.Points == 0 && !progress.HasSelectedMyth, "failed actions do not mutate account");
            Check(progress.TrySelect(TowerMythCatalog.StarterMythId) && progress.HasSelectedMyth, "explicit starter selection enables run");
            Check(!progress.TryPurchase(TowerMythCatalog.StarterMythId) && progress.Points == 0, "already owned myth cannot be repurchased");
        });
        test("floor points are awarded once before or after reward and survive saving", () => {
            var progress = new TowerProgress(); var run = new TowerRun(37, 20);
            Check(progress.RecordFloorClear(null) == 0 && progress.RecordFloorClear(run) == 0 && progress.Points == 0, "uncleared floors earn nothing");
            ClearedReward(run);
            Check(progress.RecordFloorClear(run) == 1 && progress.Points == 1 && progress.RecordFloorClear(run) == 0, "clear awards exactly once while reward pending");
            FinishReward(run); Check(progress.RecordFloorClear(run) == 0, "selecting augment does not award twice");
            progress = TowerProgress.Load(progress.Save());
            Check(progress.Points == 1 && progress.RecordFloorClear(run) == 0, "persisted receipt prevents duplicate award");
            Earn(progress, run, 3); Check(progress.Points == 4, "later floors still earn points");
            var newRun = new TowerRun(37, 20);
            Check(newRun.RunId != run.RunId && progress.RecordFloorClear(newRun) == 0, "same seed starts independent account receipt");
            Earn(progress, newRun, 2); Check(progress.Points == 6 && progress.RecordFloorClear(run) == 0, "new run earns without replaying old run");
            progress = TowerProgress.Load(progress.Save());
            Check(progress.Points == 6 && progress.RecordFloorClear(newRun) == 0 && progress.RecordFloorClear(run) == 0, "multiple run receipts survive saving");
        });
        test("batched floor reconciliation awards every unrecorded floor exactly once including final floor", () => {
            var progress = new TowerProgress(); var run = new TowerRun(91, 3);
            for (int i = 0; i < 3; i++) { ClearedReward(run); FinishReward(run); }
            Check(run.IsComplete && progress.RecordFloorClear(run) == 3 && progress.Points == 3, "completed run credits all missed clears");
            Check(progress.RecordFloorClear(run) == 0 && !run.ClearFloor() && progress.Points == 3, "completion cannot mint another clear");
        });
        test("all nine purchased mythologies deduct exact prices and retain ownership and selection", () => {
            var progress = new TowerProgress(); var run = new TowerRun(61, 200); int totalPrice = 0;
            foreach (var myth in TowerMythCatalog.Definitions) if (myth.Id != TowerMythCatalog.StarterMythId) totalPrice += myth.Price;
            Earn(progress, run, totalPrice); Check(progress.Points == totalPrice, "earn shop budget through real floor clears");
            foreach (var myth in TowerMythCatalog.Definitions) {
                if (myth.Id == TowerMythCatalog.StarterMythId) continue;
                int before = progress.Points;
                Check(!progress.TrySelect(myth.Id), "cannot select locked myth before purchase");
                Check(progress.TryPurchase(myth.Id) && progress.Points == before - myth.Price && progress.IsUnlocked(myth.Id), "successful purchase deducts one exact price");
                Check(!progress.TryPurchase(myth.Id) && progress.Points == before - myth.Price, "duplicate purchase cannot spend points");
                Check(progress.TrySelect(myth.Id), "owned myth can be selected");
                progress = TowerProgress.Load(progress.Save());
                Check(progress.HasSelectedMyth && progress.SelectedMythId == myth.Id && progress.IsUnlocked(myth.Id), "selection and unlock survive reload");
                Check(progress.RecordFloorClear(run) == 0, "shop reload preserves floor receipt");
            }
            Check(progress.Points == 0, "all purchases spend exactly earned budget");
            foreach (var myth in TowerMythCatalog.Definitions) Check(progress.IsUnlocked(myth.Id), "all purchases persist together");
        });
        test("malformed account saves recover a playable starter and filter unknown myth IDs", () => {
            foreach (string saved in new[] { null, "", "garbage", "2|99|eden|eden|", "1|-5|eden|eden|", "1|oops|eden|eden|" }) {
                var progress = TowerProgress.Load(saved);
                Check(progress.Points == 0 && !progress.HasSelectedMyth && progress.IsUnlocked(TowerMythCatalog.StarterMythId), "invalid saves recover starter selection flow");
            }
            var filtered = TowerProgress.Load("1|7|missing|missing,eden|");
            Check(filtered.Points == 7 && !filtered.IsUnlocked("missing") && !filtered.HasSelectedMyth && filtered.IsUnlocked("eden"), "unknown saved IDs cannot become owned or selected");
            var lockedSelection = TowerProgress.Load("1|7|olympus|eden|");
            Check(!lockedSelection.HasSelectedMyth && !lockedSelection.IsUnlocked("olympus"), "saved selection cannot unlock an unpurchased myth");
        });
    }
}