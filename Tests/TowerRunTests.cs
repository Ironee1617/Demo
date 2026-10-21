using System;
using System.Collections.Generic;
using PrototypeLab.Roguelike;

public static class TowerRunTests
{
    private static int passed, failed;
    private sealed class ControlledRandom : Random
    {
        public double Roll;
        public ControlledRandom(double roll) { Roll = roll; }
        public override double NextDouble() { return Roll; }
        public override int Next(int maxValue) { return 0; }
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Close(float actual, float expected, string message)
    { Check(Math.Abs(actual - expected) < .001f, message + ": " + actual + " != " + expected); }
    private static void Test(string name, Action body)
    { try { body(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); } }
    private static void ToReward(TowerRun run)
    {
        if (run.IsRewardPending) return;
        Check(!run.IsComplete, "not complete before next reward");
        if (run.IsFloorCleared) Check(run.AdvanceFloor(), "advance cleared floor");
        Check(run.ClearFloor(), "clear active floor");
        Check(run.IsRewardPending, "every floor opens a reward");
    }
    private static RewardOffer Match(TowerRun run, Predicate<RewardOffer> predicate, int attempts = 500)
    {
        for (int i = 0; i < attempts; i++)
        {
            ToReward(run);
            foreach (var offer in run.Offers) if (predicate(offer)) return offer;
            for (int slot = 0; slot < 3; slot++)
            {
                if (run.CanReroll(slot)) run.Reroll(slot);
                foreach (var offer in run.Offers) if (predicate(offer)) return offer;
            }
            RewardOffer fallback = run.Offers[0];
            foreach (var offer in run.Offers) if (offer.OwnerId == "player" && offer.Kind == RewardKind.Trait) { fallback = offer; break; }
            Check(run.ChooseOffer(fallback.Id), "choose fallback while searching");
        }
        throw new Exception("requested real offer did not appear");
    }
    private static void Take(TowerRun run, string id)
    { var offer = Match(run, o => o.AugmentId == id); Check(run.ChooseOffer(offer.Id), "choose " + id); }
    private static void FillParty(TowerRun run)
    { while (run.Companions.Count < 3) { var offer = Match(run, o => o.Kind == RewardKind.Recruit); Check(run.ChooseOffer(offer.Id), "fill party"); } }
    private static void Apply(HeroState hero, string id) { Check(hero.Apply(TowerRun.GetAugment(id)), "apply owned augment " + id); }

    public static int Main()
    {
        Test("every cleared floor opens three choices and blocks advancement until one is selected", () => {
            var run = new TowerRun(14);
            Check(run.Floor == 1 && run.ClearedFloors == 0 && !run.AdvanceFloor(), "floor one start");
            for (int floor = 1; floor <= 8; floor++) {
                Check(run.ClearFloor() && !run.ClearFloor(), "one clear");
                Check(run.IsRewardPending && run.Offers.Count == 3 && !run.AdvanceFloor(), "reward blocks stairs");
                Check(run.ClearedFloors == floor && run.Floor == floor, "counts");
                Check(run.ChooseOffer(run.Offers[0].Id), "choose exactly one");
                Check(run.AdvanceFloor() && run.Floor == floor + 1, "next floor");
            }
        });
        Test("independent rarity boundaries preserve forty forty twenty", () => {
            double[] rolls = { 0, .39999999, .4, .79999999, .8, .99999999 };
            for (int i = 0; i < rolls.Length; i++) {
                var run = new TowerRun(new ControlledRandom(rolls[i])); ToReward(run);
                foreach (var offer in run.Offers) {
                    Check((int)offer.Tier == i / 2, "rarity boundary");
                    Check(offer.Kind == RewardKind.Recruit ? TowerRun.GetDefinition(offer.OwnerId).Tier == offer.Tier : TowerRun.GetAugment(offer.AugmentId).Tier == offer.Tier, "catalog tier eligibility");
                }
            }
        });
        Test("thirty thousand offers have configured odds and independently mixed tiers", () => {
            var run = new TowerRun(27611, 10000); int[] counts = new int[3]; int mixed = 0;
            for (int i = 0; i < 10000; i++) {
                ToReward(run); foreach (var offer in run.Offers) counts[(int)offer.Tier]++;
                if (run.Offers[0].Tier != run.Offers[1].Tier || run.Offers[0].Tier != run.Offers[2].Tier) mixed++;
                Check(run.ChooseOffer(run.Offers[0].Id), "random selection");
            }
            Check(Math.Abs(counts[0] / 30000f - .4f) < .015f, "silver odds tolerance");
            Check(Math.Abs(counts[1] / 30000f - .4f) < .015f && Math.Abs(counts[2] / 30000f - .2f) < .015f, "gold diamond odds");
            Check(mixed > 7500 && run.IsComplete, "independent slots and completion");
        });
        Test("catalog contains fifty distinct myth characters and three hundred seventy-three owned augments", () => {
            Check(TowerRun.Definitions.Count == 50 && TowerRun.Augments.Count == 373, "catalog sizes");
            var ids = new HashSet<string>(); var art = new HashSet<int>(); var effects = new HashSet<SkillEffect>(); int[] tiers = new int[3];
            foreach (var c in TowerRun.Definitions) {
                Check(ids.Add(c.Id) && art.Add(c.ArtIndex) && c.ArtIndex >= 100 && c.ArtIndex < 150, "independent supplied character art");
                tiers[(int)c.Tier]++; int skills = 0, traits = 0;
                foreach (var a in TowerRun.Augments) if (a.OwnerId == c.Id) { if (a.Kind == RewardKind.Skill) skills++; else traits++; }
                Check(skills == 4 && traits == 3, "four skills and three traits per companion");
            }
            Check(tiers[0] == 20 && tiers[1] == 20 && tiers[2] == 10, "two silver two gold and one diamond recruit per myth");
            ids.Clear(); int playerSkills = 0, playerTraits = 0;
            foreach (var a in TowerRun.Augments) {
                Check(ids.Add(a.Id) && !String.IsNullOrEmpty(a.Name) && !String.IsNullOrEmpty(a.Description), "unique complete augment");
                Check(a.OwnerId == "player" || TowerRun.GetDefinition(a.OwnerId) != null, "valid owner");
                if (a.Kind == RewardKind.Skill) { effects.Add(a.Effect); Check(a.Cooldown >= 2 && a.Power > 0 && a.Count >= 1, "usable learned skill"); }
                if (a.OwnerId == "player") { if (a.Kind == RewardKind.Skill) playerSkills++; else playerTraits++; }
            }
            Check(playerSkills == 8 && playerTraits == 15 && effects.Count == 16, "player content and implemented effect coverage");
        });
        Test("new players and recruited companions start without learned skills or traits", () => {
            var run = new TowerRun(new ControlledRandom(.1));
            Check(run.Player.Skills.Count == 0 && run.Player.Traits.Count == 0, "no player autoskills before unlock");
            ToReward(run); var recruit = run.Offers[0]; Check(recruit.Kind == RewardKind.Recruit && run.ChooseOffer(recruit.Id), "real recruit");
            var state = run.GetCompanion(recruit.OwnerId);
            Check(state.Skills.Count == 0 && state.Traits.Count == 0 && state.Level == 1, "no unselected companion skills");
            Close(run.Player.Damage, 28, "base damage"); Close(run.Player.MaxHealth, 160, "base health"); Close(run.Player.Armor, 0, "base armor"); Close(run.Player.MoveSpeed, 6, "base movement"); Close(run.Player.AttackInterval, .43f, "base interval");
        });
        Test("open party slots allow recruitment mixed with player skills and stats", () => {
            bool mixed = false, playerSkill = false, playerTrait = false;
            for (int seed = 1; seed <= 100; seed++) {
                var run = new TowerRun(seed); ToReward(run); bool recruit = false, augment = false;
                foreach (var o in run.Offers) {
                    if (o.Kind == RewardKind.Recruit) recruit = true;
                    else { augment = true; Check(o.OwnerId == "player", "unowned companion cannot gain skills"); playerSkill |= o.Kind == RewardKind.Skill; playerTrait |= o.Kind == RewardKind.Trait; }
                }
                mixed |= recruit && augment;
            }
            Check(mixed && playerSkill && playerTrait, "all valid categories can appear together with a vacancy");
        });
        Test("a full party receives both player and companion augments without recruitment", () => {
            var run = new TowerRun(81, 300); FillParty(run); bool player = false, ally = false, skill = false, trait = false;
            for (int i = 0; i < 100; i++) {
                ToReward(run); foreach (var o in run.Offers) {
                    Check(o.Kind != RewardKind.Recruit && run.GetHero(o.OwnerId) != null, "full party eligible owner");
                    player |= o.OwnerId == "player"; ally |= o.OwnerId != "player"; skill |= o.Kind == RewardKind.Skill; trait |= o.Kind == RewardKind.Trait;
                }
                Check(run.ChooseOffer(run.Offers[0].Id), "apply mixed augment"); Check(run.Companions.Count == 3, "party cap");
            }
            Check(player && ally && skill && trait, "all categories remain eligible");
        });
        Test("all three cards may target the same owned companion without category quotas", () => {
            bool observed = false;
            for (int seed = 1; seed <= 200 && !observed; seed++) {
                var run = new TowerRun(seed, 100); FillParty(run);
                for (int step = 0; step < 12 && !observed; step++) {
                    ToReward(run); string owner = run.Offers[0].OwnerId;
                    observed = owner != "player" && run.Offers[1].OwnerId == owner && run.Offers[2].OwnerId == owner;
                    Check(run.ChooseOffer(run.Offers[0].Id), "advance seed scenario");
                }
            }
            Check(observed, "same companion across all three choices is reachable");
        });
        Test("visible cards and rerolls never duplicate augment identities", () => {
            var run = new TowerRun(332, 200);
            for (int i = 0; i < 100; i++) {
                ToReward(run); var identities = new HashSet<string>();
                foreach (var o in run.Offers) Check(identities.Add(o.AugmentId), "initial distinct identities");
                for (int slot = 0; slot < 3; slot++) {
                    long old = run.Offers[slot].Id; string previous = run.Offers[slot].AugmentId;
                    Check(run.Reroll(slot) && !run.Reroll(slot) && !run.ChooseOffer(old), "one reroll and stale rejection");
                    Check(run.Offers[slot].AugmentId != previous, "reroll always changes identity"); identities.Clear();
                    foreach (var o in run.Offers) Check(identities.Add(o.AugmentId), "rerolled distinct identities");
                }
                Check(run.ChooseOffer(run.Offers[0].Id), "select card");
            }
        });
        Test("reroll budgets are independent and reset only at the next floor", () => {
            var run = new TowerRun(19); ToReward(run); long second = run.Offers[1].Id, third = run.Offers[2].Id;
            Check(!run.Reroll(-1) && !run.Reroll(3), "invalid slot"); Check(run.Reroll(0), "first reroll");
            Check(run.Offers[1].Id == second && run.Offers[2].Id == third && run.CanReroll(1) && run.CanReroll(2), "other slots unchanged");
            Check(run.ChooseOffer(run.Offers[0].Id), "consume selection"); ToReward(run);
            for (int slot = 0; slot < 3; slot++) Check(run.CanReroll(slot), "new floor refreshes allowances");
        });
        Test("real skill selection learns then upgrades only its owner with accurate cooldown preview", () => {
            var run = new TowerRun(45, 2000);
            for (int level = 1; level <= 3; level++) {
                var offer = Match(run, o => o.AugmentId == "player.fireball"); float preview = offer.Cooldown;
                Check(offer.NextLevel == level && offer.IsUpgrade == (level > 1), "learn versus level-up text");
                Check(offer.Description.Contains("재사용"), "cooldown in card"); Check(run.ChooseOffer(offer.Id), "apply skill");
                var state = run.Player.GetSkill("player.fireball"); Check(state.Level == level, "actual level"); Close(state.Cooldown, preview, "preview matches effective cooldown");
            }
            Check(run.Player.Skills.Count >= 1 && !run.Player.Apply(TowerRun.GetAugment("player.fireball")), "level three cannot upgrade again");
            for (int i = 0; i < 100; i++) { ToReward(run); foreach (var o in run.Offers) Check(o.AugmentId != "player.fireball", "capped skill removed from offer pool"); run.ChooseOffer(run.Offers[0].Id); }
        });
        Test("five basic player traits alter the actual combat stats", () => {
            var hero = new TowerRun().Player;
            Apply(hero, "player.might"); Apply(hero, "player.vitality"); Apply(hero, "player.armor"); Apply(hero, "player.swiftness"); Apply(hero, "player.frenzy");
            Close(hero.Damage, 28 * 1.16f, "damage"); Close(hero.MaxHealth, 160 * 1.15f, "health"); Close(hero.Armor, 8, "armor"); Close(hero.MoveSpeed, 6 * 1.12f, "speed"); Close(hero.AttackInterval, .43f / 1.16f, "attack rate");
            Check(hero.Traits.Count == 5 && hero.UpgradeCount == 5 && hero.Skills.Count == 0, "traits independent from skills");
        });
        Test("repeatable traits stack on their owner and expose cumulative effective values", () => {
            var hero = new TowerRun().Player; Apply(hero, "player.might"); Apply(hero, "player.might");
            Close(hero.Damage, 28 * 1.32f, "additive base damage stacks"); var trait = hero.GetTrait("player.might");
            Check(hero.Traits.Count == 1 && trait.Level == 2 && trait.Description.Contains("누적") && trait.Description.Contains("현재"), "one leveled trait with actual state");
            Check(!hero.Apply(TowerRun.GetAugment("michael.trait1")), "cannot apply another owner's trait");
        });
        Test("skill power cooldown area and projectile traits update already learned skills", () => {
            var hero = new TowerRun().Player; Apply(hero, "player.fireball"); var skill = hero.GetSkill("player.fireball");
            float power = skill.Power, cooldown = skill.Cooldown, radius = skill.Radius;
            Apply(hero, "player.arcana"); Apply(hero, "player.focus"); Apply(hero, "player.expansion"); Apply(hero, "player.echo");
            Close(skill.Power, power * 1.25f, "live power scaling"); Close(skill.Cooldown, cooldown * .9f, "live cooldown scaling"); Close(skill.Radius, radius * 1.25f, "live area scaling"); Check(skill.Count == 2, "additional projectile");
            Apply(hero, "player.fireball"); Check(skill.Level == 2 && skill.Cooldown < cooldown * .9f && skill.Power > power * 1.25f, "learned skill upgrade changes performance");
        });
        Test("additional projectiles affect projectile skills rather than heals or pulse counts", () => {
            var hero = new TowerRun().Player;
            Apply(hero, "player.fireball"); Apply(hero, "player.ice"); Apply(hero, "player.bladestorm"); Apply(hero, "player.chain"); Apply(hero, "player.echo");
            Check(hero.GetSkill("player.fireball").Count == 2, "fireball projectile grows");
            Check(hero.GetSkill("player.ice").Count == TowerRun.GetAugment("player.ice").Count, "nova unaffected");
            Check(hero.GetSkill("player.bladestorm").Count == TowerRun.GetAugment("player.bladestorm").Count, "blade pulses unaffected");
            Check(hero.GetSkill("player.chain").Count == TowerRun.GetAugment("player.chain").Count, "chain count unaffected");
            foreach (var a in TowerRun.Augments) if (a.Kind == RewardKind.Skill && (a.Effect == SkillEffect.PierceShot || a.Effect == SkillEffect.Shockwave)) Check(a.Count >= 2, "piercing content reaches multiple enemies");
        });
        Test("remaining player passives expose useful live stats with safe cooldown and chance bounds", () => {
            var hero = new TowerRun().Player;
            Apply(hero, "player.reach"); Apply(hero, "player.precision"); Apply(hero, "player.vampire"); Apply(hero, "player.renewal"); Apply(hero, "player.evasion"); Apply(hero, "player.grace");
            Close(hero.Range, 2.6f * 1.2f, "range"); Close(hero.CritChance, .12f, "crit"); Close(hero.LifeSteal, .06f, "vampirism"); Close(hero.Regeneration, 2, "regeneration"); Close(hero.DodgeCooldownMultiplier, .85f, "dodge cooldown"); Close(hero.HealingMultiplier, 1.3f, "healing");
            for (int i = 0; i < 30; i++) { Apply(hero, "player.focus"); Apply(hero, "player.evasion"); Apply(hero, "player.precision"); Apply(hero, "player.vampire"); }
            Check(hero.CooldownMultiplier >= .25f && hero.DodgeCooldownMultiplier >= .25f && hero.CritChance <= .8f && hero.LifeSteal <= .6f, "passive caps");
        });
        Test("trait cards preview effective before and after values including the final partial gain at each cap", () => {
            var hero = new TowerRun().Player;
            foreach (var definition in TowerRun.Augments) if (definition.OwnerId == "player" && definition.Kind == RewardKind.Trait) {
                for (int level = 1; level <= 11; level++) {
                    string before = hero.StatText(definition.Stat);
                    var offer = new RewardOffer(level, 0, definition, hero);
                    Check(offer.NextLevel == level && hero.StatText(definition.Stat) == before, "preview must not apply the reward");
                    Apply(hero, definition.Id);
                    Check(offer.Description.Contains("현재 " + before + " → 선택 후 " + hero.StatText(definition.Stat)), "preview matches actual effective stat: " + definition.Id);
                }
            }
            var nearCap = new TowerRun().Player;
            for (int i = 0; i < 6; i++) Apply(nearCap, "player.precision");
            var finalCrit = new RewardOffer(1, 0, TowerRun.GetAugment("player.precision"), nearCap);
            Check(finalCrit.Description.Contains("현재 72% → 선택 후 80%"), "last critical chance pick shows partial gain instead of an impossible 84%");
        });
        Test("capped traits leave initial choices and rerolls while uncapped repeatable traits stay eligible", () => {
            string[] cappedIds = { "player.precision", "player.vampire", "player.focus", "player.evasion" };
            int[] capLevels = { 7, 10, 8, 5 };
            foreach (double roll in new[] { .6, .9 }) {
                var run = new TowerRun(new ControlledRandom(roll), 4);
                for (int stat = 0; stat < cappedIds.Length; stat++) {
                    var definition = TowerRun.GetAugment(cappedIds[stat]);
                    for (int i = 1; i < capLevels[stat]; i++) Apply(run.Player, definition.Id);
                    Check(run.Player.CanImproveTrait(definition), "last partial gain is still eligible: " + definition.Id);
                    Apply(run.Player, definition.Id);
                    Check(!run.Player.CanImproveTrait(definition), "fully capped trait no longer improves stats: " + definition.Id);
                }
                Apply(run.Player, "player.reach"); Apply(run.Player, "player.echo");
                Check(run.Player.CanImproveTrait(TowerRun.GetAugment("player.reach")) && run.Player.CanImproveTrait(TowerRun.GetAugment("player.echo")), "uncapped repeated growth remains eligible");
                for (int floor = 0; floor < 4; floor++) {
                    ToReward(run);
                    for (int attempt = 0; attempt < 4; attempt++) {
                        foreach (var offer in run.Offers) {
                            Check(Array.IndexOf(cappedIds, offer.AugmentId) < 0, "capped trait cannot waste a choice");
                            Check((int)offer.Tier == (roll < .8 ? 1 : 2), "filter preserves rolled tier");
                        }
                        if (attempt < 3) Check(run.Reroll(attempt), "reroll still finds useful reward");
                    }
                    Check(run.ChooseOffer(run.Offers[0].Id), "filtered choice applies");
                }
            }
        });
        Test("an exhausted custom-length gold pool retains three distinct cards and working rerolls", () => {
            var run = new TowerRun(new ControlledRandom(.6), 5);
            Take(run, "recruit:raphael"); Take(run, "recruit:uriel");
            foreach (var definition in TowerRun.Augments) if (definition.OwnerId == "player" && definition.Kind == RewardKind.Skill)
                for (int i = 0; i < 3; i++) Apply(run.Player, definition.Id);
            foreach (string id in new[] { "player.precision", "player.vampire", "player.focus" })
                for (int i = 0; i < 10; i++) Apply(run.Player, id);
            // Both gold recruits are owned. Capping their skills and Raphael's cooldown leaves only three useful gold identities.
            foreach (var ally in run.Companions)
                foreach (var definition in TowerRun.Augments) if (definition.OwnerId == ally.Id && definition.Kind == RewardKind.Skill)
                    for (int i = 0; i < 3; i++) Apply(ally, definition.Id);
            for (int i = 0; i < 8; i++) Apply(run.GetCompanion("raphael"), "raphael.trait2");
            ToReward(run);
            foreach (var offer in run.Offers) Check(run.GetHero(offer.OwnerId).CanImproveTrait(TowerRun.GetAugment(offer.AugmentId)), "initial cards still prefer every useful identity");
            for (int slot = 0; slot < 3; slot++) {
                string previous = run.Offers[slot].AugmentId;
                Check(run.Reroll(slot) && run.Offers[slot].AugmentId != previous, "exhausted pool reroll changes identity");
                if (slot == 0) Check(!run.GetHero(run.Offers[0].OwnerId).CanImproveTrait(TowerRun.GetAugment(run.Offers[0].AugmentId)), "fallback only when prior and visible identities exhaust useful options");
                var identities = new HashSet<string>();
                foreach (var offer in run.Offers) Check(offer.Tier == RewardTier.Gold && identities.Add(offer.AugmentId), "three distinct gold cards remain valid");
            }
            Check(run.ChooseOffer(run.Offers[0].Id), "fallback card still selects");
        });
        Test("dismissal removes only dismissed skills and traits while preserving player and other allies", () => {
            var run = new TowerRun(91, 1000); FillParty(run);
            var removed = run.Companions[0]; var kept = run.Companions[1];
            string removedId = removed.Id, keptId = kept.Id;
            Apply(removed, removedId + ".skill1"); Apply(removed, removedId + ".trait1"); Apply(kept, keptId + ".skill1"); Apply(run.Player, "player.fireball"); Apply(run.Player, "player.armor");
            int playerSkillCount = run.Player.Skills.Count, playerTraitCount = run.Player.Traits.Count, keptSkillCount = kept.Skills.Count; float playerArmor = run.Player.Armor;
            ToReward(run); Check(run.Reroll(1), "spend reroll before dismissal"); long[] stale = { run.Offers[0].Id, run.Offers[1].Id, run.Offers[2].Id };
            Check(run.DismissCompanion(removedId) && !run.DismissCompanion("player") && run.GetHero(removedId) == null, "dismiss only companions");
            Check(Object.ReferenceEquals(kept, run.GetCompanion(keptId)) && kept.Skills.Count == keptSkillCount, "other ally preserved");
            Check(run.Player.Skills.Count == playerSkillCount && run.Player.Traits.Count == playerTraitCount && run.Player.Armor == playerArmor, "player state preserved");
            Check(run.CanReroll(0) && !run.CanReroll(1) && run.CanReroll(2), "spent reroll remains spent");
            foreach (long id in stale) Check(!run.ChooseOffer(id), "all obsolete cards rejected");
            foreach (var o in run.Offers) Check(o.Kind == RewardKind.Recruit || o.OwnerId != removedId, "dismissed owner's skills excluded");
            var recruitment = Match(run, o => o.AugmentId == "recruit:" + removedId); Check(run.ChooseOffer(recruitment.Id), "fresh recruitment");
            var fresh = run.GetCompanion(removedId); Check(!Object.ReferenceEquals(removed, fresh) && fresh.Skills.Count == 0 && fresh.Traits.Count == 0 && fresh.Level == 1, "recruited copy has no old progression");
        });
        Test("player fallback prevents exhausted tier pools even with every player skill capped", () => {
            foreach (double roll in new[] { .1, .6, .9 }) {
                var run = new TowerRun(new ControlledRandom(roll), 20);
                foreach (var a in TowerRun.Augments) if (a.OwnerId == "player" && a.Kind == RewardKind.Skill) for (int i = 0; i < 3; i++) Apply(run.Player, a.Id);
                for (int floor = 0; floor < 12; floor++) {
                    ToReward(run); for (int slot = 0; slot < 3; slot++) Check(run.Reroll(slot), "fallback reroll");
                    Check(run.Offers.Count == 3 && run.ChooseOffer(run.Offers[0].Id), "fallback selection");
                }
            }
        });
        Test("one selection invalidates the entire reward including retained cards from old floors", () => {
            var run = new TowerRun(88); ToReward(run); long first = run.Offers[0].Id, other = run.Offers[1].Id;
            Check(run.ChooseOffer(first) && !run.ChooseOffer(first) && !run.ChooseOffer(other) && !run.Reroll(1), "single consumption");
            ToReward(run); Check(!run.ChooseOffer(first) && !run.ChooseOffer(other), "ids not reused across floors");
        });
        Test("final floor completes only after its final choice", () => {
            var run = new TowerRun(67);
            for (int floor = 1; floor <= 25; floor++) { ToReward(run); Check(!run.IsComplete && run.Floor == floor, "pending final choice"); Check(run.ChooseOffer(run.Offers[0].Id), "final selection"); }
            Check(run.IsComplete && run.ClearedFloors == 25 && !run.AdvanceFloor() && !run.ClearFloor(), "completed run stops");
        });
        TowerMythTests.Register(Test);
        TowerOathTests.Register(Test);
        Console.WriteLine(passed + " passed, " + failed + " failed"); return failed == 0 ? 0 : 1;
    }
}
