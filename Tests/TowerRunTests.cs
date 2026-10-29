using System;
using System.Collections.Generic;
using System.Reflection;
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
    // Clears and climbs plain floors until the next card set is waiting; every plain floor deals one.
    internal static void ToReward(TowerRun run)
    {
        for (int guard = 0; guard < 20 && !run.IsRewardPending; guard++)
        {
            Check(!run.IsComplete, "not complete before next reward");
            if (run.IsFloorCleared) Check(run.AdvanceFloor(), "advance cleared floor");
            Check(run.ClearFloor(), "clear active floor");
        }
        Check(run.IsRewardPending, "a reward arrives on the next cleared floor");
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
    // Takes the opening, 30th and 60th floor recruits through the real reward flow.
    private static void FillParty(TowerRun run)
    {
        while (run.Companions.Count < TowerRun.MaxCompanions)
        {
            ToReward(run);
            var choice = run.Offers[0];
            if (run.IsRecruitOffer) Check(choice.Kind == RewardKind.Recruit, "recruit floors only offer companions");
            Check(run.ChooseOffer(choice.Id), "fill party");
        }
    }
    // Fixture-only party composition for synergy and pool tests.
    internal static void Recruit(TowerRun run, string id)
    { typeof(TowerRun).GetMethod("AddCompanion", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(run, new object[] { TowerRun.GetDefinition(id) }); }
    private static void Apply(HeroState hero, string id) { Check(hero.Apply(TowerRun.GetAugment(id)), "apply owned augment " + id); }

    public static int Main()
    {
        Test("every floor deals stat cards, every fifth floor skill cards, and each blocks advancement until one is selected", () => {
            var run = new TowerRun(14, 100, false);
            Check(run.Floor == 1 && run.ClearedFloors == 0 && !run.IsRewardPending && !run.AdvanceFloor(), "floor one start");
            for (int floor = 1; floor <= 15; floor++) {
                Check(run.ClearFloor() && !run.ClearFloor(), "one clear");
                Check(run.ClearedFloors == floor && run.Floor == floor, "counts");
                bool skills = floor % 5 == 0;
                Check(run.IsRewardPending && !run.IsRecruitOffer && run.Draft == (skills ? RewardDraft.Skills : RewardDraft.Stats), "card cadence on floor " + floor);
                Check(run.Offers.Count == 3 && !run.AdvanceFloor(), "reward blocks stairs");
                for (int slot = -1; slot < 3; slot++) {
                    if (slot >= 0) Check(run.Reroll(slot), "reroll slot " + slot);
                    foreach (var o in run.Offers) Check(o.Kind == (skills ? RewardKind.Skill : RewardKind.Trait), "card kind follows the floor on " + floor + ", rerolls included");
                }
                Check(run.ChooseOffer(run.Offers[0].Id), "choose exactly one");
                Check(run.AdvanceFloor() && run.Floor == floor + 1, "next floor");
            }
        });
        Test("a climb opens with three companion cards drawn from every mythology", () => {
            var myths = new HashSet<string>();
            for (int seed = 1; seed <= 60; seed++) {
                var run = new TowerRun(seed);
                Check(run.MaxFloor == TowerRun.DefaultMaxFloor && run.IsRewardPending && run.IsRecruitOffer && run.Offers.Count == 3 && !run.IsFloorCleared, "opening recruit");
                var ids = new HashSet<string>();
                foreach (var o in run.Offers) { Check(o.Kind == RewardKind.Recruit && ids.Add(o.CompanionId), "distinct companions"); myths.Add(TowerRun.GetDefinition(o.CompanionId).MythId); }
                Check(!run.ClearFloor() && run.ChooseOffer(run.Offers[1].Id) && run.Companions.Count == 1 && !run.IsRewardPending && !run.IsRecruitOffer, "opening recruit joins before floor one");
            }
            Check(myths.Count == TowerMythCatalog.Definitions.Count, "every mythology appears in opening recruits");
        });
        Test("floors thirty and sixty recruit only companions and no other card recruits", () => {
            for (int seed = 1; seed <= 20; seed++) {
                var run = new TowerRun(seed); Check(run.ChooseOffer(run.Offers[0].Id), "opening recruit");
                while (run.Floor < TowerRun.DefaultMaxFloor || !run.IsComplete) {
                    ToReward(run);
                    bool recruitFloor = run.Floor == 30 || run.Floor == 60;
                    Check(run.IsRecruitOffer == recruitFloor, "recruit cadence on floor " + run.Floor);
                    foreach (var o in run.Offers) Check((o.Kind == RewardKind.Recruit) == recruitFloor, "card kinds follow the floor on " + run.Floor);
                    Check(run.ChooseOffer(run.Offers[0].Id), "take card");
                    if (run.Floor == 60) Check(run.Companions.Count == 3, "three companions by sixty");
                    if (run.IsComplete) break;
                }
                Check(run.IsComplete && run.Companions.Count == 3 && run.ClearedFloors == TowerRun.DefaultMaxFloor, "full climb");
            }
        });
        Test("a full party at a recruit floor receives ordinary cards instead", () => {
            var run = new TowerRun(5, 100, false);
            Recruit(run, "zeus"); Recruit(run, "odin"); Recruit(run, "ra");
            for (ToReward(run); run.Floor < 30; ToReward(run)) { Check(run.ChooseOffer(run.Offers[0].Id), "card"); Check(run.AdvanceFloor(), "advance"); }
            Check(run.IsRewardPending && !run.IsRecruitOffer && run.Draft == RewardDraft.Skills, "no recruit with a full party");
            foreach (var o in run.Offers) Check(o.Kind != RewardKind.Recruit, "no recruit cards");
        });
        Test("the first recruit card keeps a synergy route open for an existing party", () => {
            for (int seed = 1; seed <= 40; seed++) {
                var run = new TowerRun(seed); var first = run.Offers[0]; Check(run.ChooseOffer(first.Id), "opening");
                string myth = TowerRun.GetDefinition(first.CompanionId).MythId;
                while (run.Floor < 30 || !run.IsRecruitOffer) { ToReward(run); if (!run.IsRecruitOffer) { Check(run.ChooseOffer(run.Offers[0].Id), "card"); Check(run.AdvanceFloor(), "advance"); } }
                Check(TowerRun.GetDefinition(run.Offers[0].CompanionId).MythId == myth, "slot zero shares a mythology");
                string previous = run.Offers[0].CompanionId;
                Check(run.Reroll(0) && run.Offers[0].CompanionId != previous && TowerRun.GetDefinition(run.Offers[0].CompanionId).MythId == myth, "reroll keeps the route with a new companion");
            }
        });
        Test("independent rarity boundaries preserve forty forty twenty", () => {
            double[] rolls = { 0, .39999999, .4, .79999999, .8, .99999999 };
            for (int i = 0; i < rolls.Length; i++) {
                var recruit = new TowerRun(new ControlledRandom(rolls[i]));
                foreach (var offer in recruit.Offers) Check((int)offer.Tier == i / 2 && TowerRun.GetDefinition(offer.CompanionId).Tier == offer.Tier, "recruit rarity boundary");
                var run = new TowerRun(new ControlledRandom(rolls[i]), 100, false); ToReward(run);
                foreach (var offer in run.Offers) {
                    Check((int)offer.Tier == i / 2, "rarity boundary");
                    Check(offer.Kind != RewardKind.Recruit && TowerRun.GetAugment(offer.AugmentId).Tier == offer.Tier, "catalog tier eligibility");
                }
            }
        });
        Test("thirty thousand offers have configured odds and independently mixed tiers", () => {
            // The player's eight skills cap early on this long climb; later fifth floors deal fewer cards and then none.
            var run = new TowerRun(27611, 50000, false); int[] counts = new int[3]; int mixed = 0;
            for (int sets = 0; sets < 10000; ) {
                ToReward(run);
                if (run.Offers.Count == 3) {
                    sets++; foreach (var offer in run.Offers) counts[(int)offer.Tier]++;
                    if (run.Offers[0].Tier != run.Offers[1].Tier || run.Offers[0].Tier != run.Offers[2].Tier) mixed++;
                }
                Check(run.ChooseOffer(run.Offers[0].Id), "random selection");
            }
            Check(Math.Abs(counts[0] / 30000f - .4f) < .015f, "silver odds tolerance");
            Check(Math.Abs(counts[1] / 30000f - .4f) < .015f && Math.Abs(counts[2] / 30000f - .2f) < .015f, "gold diamond odds");
            Check(mixed > 7500 && run.OpenSkills == 0, "independent slots and capped skills");
        });
        Test("catalog contains fifty distinct myth characters and four hundred seventy-seven owned augments", () => {
            Check(TowerRun.Definitions.Count == 50 && TowerRun.Augments.Count == 477, "catalog sizes");
            var ids = new HashSet<string>(); var art = new HashSet<int>(); var effects = new HashSet<SkillEffect>(); int[] tiers = new int[3];
            foreach (var c in TowerRun.Definitions) {
                Check(ids.Add(c.Id) && art.Add(c.ArtIndex) && c.ArtIndex >= 100 && c.ArtIndex < 150, "independent supplied character art");
                tiers[(int)c.Tier]++; int skills = 0, traits = 0;
                foreach (var a in TowerRun.Augments) if (a.OwnerId == c.Id) { if (a.Kind == RewardKind.Skill) skills++; else traits++; }
                Check(skills == 6 && traits == 3, "six skills and three traits per companion");
            }
            Check(tiers[0] == 20 && tiers[1] == 20 && tiers[2] == 10, "two silver two gold and one diamond recruit per myth");
            ids.Clear(); int playerSkills = 0, playerTraits = 0;
            foreach (var a in TowerRun.Augments) {
                Check(ids.Add(a.Id) && !String.IsNullOrEmpty(a.Name) && !String.IsNullOrEmpty(a.Description), "unique complete augment");
                Check(a.OwnerId == "player" || TowerRun.GetDefinition(a.OwnerId) != null, "valid owner");
                if (a.Kind == RewardKind.Skill) { effects.Add(a.Effect); Check(a.Cooldown >= 2 && a.Power > 0 && a.Count >= 1, "usable learned skill"); }
                if (a.OwnerId == "player") { if (a.Kind == RewardKind.Skill) playerSkills++; else playerTraits++; }
            }
            Check(playerSkills == 12 && playerTraits == 15 && effects.Count == 28, "player content and implemented effect coverage");
        });
        Test("every skill level is a clear step and level three awakens with its own card line", () => {
            foreach (var a in TowerRun.Augments) {
                if (a.Kind != RewardKind.Skill) continue;
                var hero = new TowerRun().Player; var skill = new SkillState(a, hero, 1);
                float power = skill.Power, cooldown = skill.Cooldown, radius = skill.Radius; int count = skill.Count;
                for (int level = 2; level <= 3; level++) {
                    var next = new SkillState(a, hero, level);
                    Check(next.Power >= power * 1.35f && next.Cooldown < cooldown + .0001f && next.Radius > radius && next.Count >= count, "each level is stronger: " + a.Id);
                    power = next.Power; cooldown = next.Cooldown; radius = next.Radius; count = next.Count;
                }
                Check(new SkillState(a, hero, 3).Awakened && !new SkillState(a, hero, 2).Awakened, "level three awakens");
                Check(skill.Describe(1).Contains("Lv.3 각성") && skill.Describe(2).Contains("Lv.2 · ") && skill.Describe(3).Contains("Lv.3 각성"), "cards name the level's bonus: " + a.Id);
                Check(skill.Describe(1).Contains(TowerSkillBuilds.StyleName(a.Style)), "cards name the build style: " + a.Id);
            }
        });
        Test("party resonance counts learned levels per style across the player and companions", () => {
            var run = new TowerRun(5, 100, false); Recruit(run, "michael"); Recruit(run, "tyr");
            Check(run.StyleLevels(SkillStyle.Melee) == 0 && run.Player.ResonanceTier(SkillStyle.Melee) == 0, "no resonance before skills");
            Apply(run.Player, "player.shockwave"); Apply(run.GetCompanion("michael"), "michael.skill1"); Apply(run.GetCompanion("tyr"), "tyr.skill1");
            Check(run.StyleLevels(SkillStyle.Melee) == 3 && run.Player.ResonanceTier(SkillStyle.Melee) == 1 && run.GetCompanion("tyr").ResonanceTier(SkillStyle.Melee) == 1, "three melee levels across heroes reach tier one");
            var shock = run.Player.GetSkill("player.shockwave"); var alone = new SkillState(shock.Definition, new TowerRun().Player);
            Close(shock.Power, alone.Power * TowerSkillBuilds.ResonancePower(SkillStyle.Melee, 1), "resonance raises the style's power");
            Check(run.Player.ResonanceTier(SkillStyle.Ranged) == 0, "other styles stay quiet");
            Apply(run.Player, "player.shockwave"); Apply(run.Player, "player.shockwave"); Apply(run.GetCompanion("michael"), "michael.skill1");
            Check(run.StyleLevels(SkillStyle.Melee) == 6 && run.Player.ResonanceTier(SkillStyle.Melee) == 2, "upgrades count toward the next tier");
            Check(shock.Radius > alone.Radius * TowerSkillBuilds.AreaScale(SkillEffect.Shockwave, 3) * 1.05f, "tier two widens melee skills");
            foreach (SkillEffect effect in Enum.GetValues(typeof(SkillEffect))) Check(TowerSkillBuilds.LevelText(effect, 2).Length > 6 && TowerSkillBuilds.LevelText(effect, 3).Length > 9, "level text for every effect");
        });
        Test("every companion keeps its four original skills and gains two narrower ones of distinct effects", () => {
            foreach (var c in TowerRun.Definitions) {
                var fifth = TowerRun.GetAugment(c.Id + ".skill5"); var sixth = TowerRun.GetAugment(c.Id + ".skill6");
                Check(fifth != null && sixth != null && fifth.Effect != sixth.Effect && (int)fifth.Effect >= (int)SkillEffect.Boomerang && (int)sixth.Effect >= (int)SkillEffect.Boomerang, "two new tools: " + c.Id);
                Check(fifth.Tier == RewardTier.Silver && sixth.Tier == RewardTier.Gold && fifth.Radius <= 8.5f && sixth.Radius <= 8.5f, "modest new tools: " + c.Id);
            }
        });
        Test("new players and recruited companions start without learned skills or traits", () => {
            var run = new TowerRun(new ControlledRandom(.1));
            Check(run.Player.Skills.Count == 0 && run.Player.Traits.Count == 0, "no player autoskills before unlock");
            var recruit = run.Offers[0]; Check(recruit.Kind == RewardKind.Recruit && run.ChooseOffer(recruit.Id), "real recruit");
            var state = run.GetCompanion(recruit.OwnerId);
            Check(state.Skills.Count == 0 && state.Traits.Count == 0 && state.Level == 1, "no unselected companion skills");
            Close(run.Player.Damage, 28, "base damage"); Close(run.Player.MaxHealth, 160, "base health"); Close(run.Player.Armor, 0, "base armor"); Close(run.Player.MoveSpeed, 6, "base movement"); Close(run.Player.AttackInterval, .43f, "base interval");
        });
        Test("stat and skill floors grow both the player and owned companions without recruitment", () => {
            bool companionTrait = false, companionSkill = false, playerSkill = false, playerTrait = false;
            for (int seed = 1; seed <= 100; seed++) {
                var run = new TowerRun(seed); Check(run.ChooseOffer(run.Offers[0].Id), "opening recruit");
                for (int floor = 1; floor <= 5; floor++) {
                    ToReward(run); bool skills = run.Floor == 5;
                    foreach (var o in run.Offers) {
                        Check(o.Kind == (skills ? RewardKind.Skill : RewardKind.Trait) && run.GetHero(o.OwnerId) != null, "only owned heroes grow with the floor's kind");
                        if (o.OwnerId == "player") { playerSkill |= skills; playerTrait |= !skills; } else { companionSkill |= skills; companionTrait |= !skills; }
                    }
                    Check(run.ChooseOffer(run.Offers[0].Id), "take card");
                }
            }
            Check(companionTrait && companionSkill && playerSkill && playerTrait, "all valid categories appear");
        });
        Test("a full party receives both player and companion augments without recruitment", () => {
            var run = new TowerRun(81, 1000); FillParty(run); bool player = false, ally = false, skill = false, trait = false;
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
                var run = new TowerRun(seed, 200, false); Recruit(run, "zeus"); Recruit(run, "thor"); Recruit(run, "isis");
                for (int step = 0; step < 12 && !observed; step++) {
                    ToReward(run); string owner = run.Offers[0].OwnerId;
                    observed = owner != "player" && run.Offers[1].OwnerId == owner && run.Offers[2].OwnerId == owner;
                    Check(run.ChooseOffer(run.Offers[0].Id), "advance seed scenario");
                }
            }
            Check(observed, "same companion across all three choices is reachable");
        });
        Test("visible cards and rerolls never duplicate augment identities", () => {
            var run = new TowerRun(332, 1000);
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
        Test("reroll budgets are independent and reset only at the next card set", () => {
            var run = new TowerRun(19, 100, false); ToReward(run); long second = run.Offers[1].Id, third = run.Offers[2].Id;
            Check(!run.Reroll(-1) && !run.Reroll(3), "invalid slot"); Check(run.Reroll(0), "first reroll");
            Check(run.Offers[1].Id == second && run.Offers[2].Id == third && run.CanReroll(1) && run.CanReroll(2), "other slots unchanged");
            Check(run.ChooseOffer(run.Offers[0].Id), "consume selection"); ToReward(run);
            for (int slot = 0; slot < 3; slot++) Check(run.CanReroll(slot), "new card set refreshes allowances");
        });
        Test("real skill selection learns then upgrades only its owner with accurate cooldown preview", () => {
            var run = new TowerRun(45, 20000, false);
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
            Close(hero.Damage, 28 * 1.08f, "damage"); Close(hero.MaxHealth, 160 * 1.08f, "health"); Close(hero.Armor, 4, "armor"); Close(hero.MoveSpeed, 6 * 1.06f, "speed"); Close(hero.AttackInterval, .43f / 1.08f, "attack rate");
            Check(hero.Traits.Count == 5 && hero.UpgradeCount == 5 && hero.Skills.Count == 0, "traits independent from skills");
        });
        Test("repeatable traits stack on their owner and expose cumulative effective values", () => {
            var hero = new TowerRun().Player; Apply(hero, "player.might"); Apply(hero, "player.might");
            Close(hero.Damage, 28 * 1.16f, "additive base damage stacks"); var trait = hero.GetTrait("player.might");
            Check(hero.Traits.Count == 1 && trait.Level == 2 && trait.Description.Contains("누적") && trait.Description.Contains("현재"), "one leveled trait with actual state");
            Check(!hero.Apply(TowerRun.GetAugment("michael.trait1")), "cannot apply another owner's trait");
        });
        Test("skill power cooldown area and projectile traits update already learned skills", () => {
            var hero = new TowerRun().Player; Apply(hero, "player.fireball"); var skill = hero.GetSkill("player.fireball");
            float power = skill.Power, cooldown = skill.Cooldown, radius = skill.Radius;
            Apply(hero, "player.arcana"); Apply(hero, "player.focus"); Apply(hero, "player.expansion"); Apply(hero, "player.echo");
            Close(skill.Power, power * 1.12f, "live power scaling"); Close(skill.Cooldown, cooldown * .95f, "live cooldown scaling"); Close(skill.Radius, radius * 1.12f, "live area scaling"); Check(skill.Count == 2, "additional projectile");
            Apply(hero, "player.fireball"); Check(skill.Level == 2 && skill.Cooldown < cooldown * .95f && skill.Power > power * 1.12f, "learned skill upgrade changes performance");
            for (int i = 0; i < 5; i++) Apply(hero, "player.echo");
            Check(skill.Count == 1 + TowerSkillBuilds.ExtraCount(SkillEffect.Fireball, 2) + HeroState.MaxProjectileBonus && hero.ProjectileBonus == HeroState.MaxProjectileBonus && !hero.CanImproveTrait(TowerRun.GetAugment("player.echo")), "extra projectiles stop at their cap");
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
            Close(hero.Range, 2.6f * 1.1f, "range"); Close(hero.CritChance, .06f, "crit"); Close(hero.LifeSteal, .03f, "vampirism"); Close(hero.Regeneration, 1, "regeneration"); Close(hero.DodgeCooldownMultiplier, .92f, "dodge cooldown"); Close(hero.HealingMultiplier, 1.15f, "healing");
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
            for (int i = 0; i < 13; i++) Apply(nearCap, "player.precision");
            var finalCrit = new RewardOffer(1, 0, TowerRun.GetAugment("player.precision"), nearCap);
            Check(finalCrit.Description.Contains("현재 78% → 선택 후 80%"), "last critical chance pick shows partial gain instead of an impossible 84%");
        });
        Test("capped traits leave initial choices and rerolls while uncapped repeatable traits stay eligible", () => {
            string[] cappedIds = { "player.precision", "player.vampire", "player.focus", "player.evasion" };
            int[] capLevels = { 14, 20, 15, 10 };
            foreach (double roll in new[] { .6, .9 }) {
                var run = new TowerRun(new ControlledRandom(roll), 20, false);
                for (int stat = 0; stat < cappedIds.Length; stat++) {
                    var definition = TowerRun.GetAugment(cappedIds[stat]);
                    for (int i = 1; i < capLevels[stat]; i++) Apply(run.Player, definition.Id);
                    Check(run.Player.CanImproveTrait(definition), "last partial gain is still eligible: " + definition.Id);
                    Apply(run.Player, definition.Id);
                    Check(!run.Player.CanImproveTrait(definition), "fully capped trait no longer improves stats: " + definition.Id);
                }
                Apply(run.Player, "player.reach"); Apply(run.Player, "player.echo");
                Check(run.Player.CanImproveTrait(TowerRun.GetAugment("player.reach")) && run.Player.CanImproveTrait(TowerRun.GetAugment("player.echo")), "uncapped repeated growth remains eligible");
                // Gold keeps two useful stats (reach, arcana) and diamond four, so a third gold card borrows another tier.
                var rolled = (RewardTier)(roll < .8 ? 1 : 2); int usefulAtRoll = roll < .8 ? 2 : 4;
                for (int reward = 0; reward < 4; reward++) {
                    ToReward(run); int atRoll = 0; foreach (var offer in run.Offers) if (offer.Tier == rolled) atRoll++;
                    Check(run.Draft == RewardDraft.Stats && atRoll == Math.Min(3, usefulAtRoll), "stats keep the rolled tier while it has useful identities");
                    for (int attempt = 0; attempt < 4; attempt++) {
                        foreach (var offer in run.Offers) {
                            Check(Array.IndexOf(cappedIds, offer.AugmentId) < 0, "capped trait cannot waste a choice");
                            Check(offer.Kind == RewardKind.Trait && run.Player.CanImproveTrait(TowerRun.GetAugment(offer.AugmentId)), "stat floors keep useful stat cards");
                        }
                        if (attempt < 3) Check(run.Reroll(attempt), "reroll still finds useful reward");
                    }
                    Check(run.ChooseOffer(run.Offers[0].Id), "filtered choice applies");
                }
            }
        });
        Test("an exhausted gold stat pool lends useful stats from other tiers with distinct cards and working rerolls", () => {
            var run = new TowerRun(new ControlledRandom(.6), 5, false);
            Recruit(run, "raphael"); Recruit(run, "uriel");
            foreach (var definition in TowerRun.Augments) if (definition.OwnerId == "player" && definition.Kind == RewardKind.Skill)
                for (int i = 0; i < 3; i++) Apply(run.Player, definition.Id);
            foreach (string id in new[] { "player.precision", "player.vampire", "player.focus" })
                for (int i = 0; i < 20; i++) Apply(run.Player, id);
            // Both gold recruits are owned. Capping their skills and Raphael's cooldown leaves only three useful gold identities.
            foreach (var ally in run.Companions)
                foreach (var definition in TowerRun.Augments) if (definition.OwnerId == ally.Id && definition.Kind == RewardKind.Skill)
                    for (int i = 0; i < 3; i++) Apply(ally, definition.Id);
            for (int i = 0; i < 15; i++) Apply(run.GetCompanion("raphael"), "raphael.trait2");
            Check(!run.GetCompanion("raphael").CanImproveTrait(TowerRun.GetAugment("raphael.trait2")), "raphael's cooldown is capped");
            ToReward(run);
            foreach (var offer in run.Offers) Check(offer.Tier == RewardTier.Gold && run.GetHero(offer.OwnerId).CanImproveTrait(TowerRun.GetAugment(offer.AugmentId)), "initial cards deal every useful gold identity");
            for (int slot = 0; slot < 3; slot++) {
                string previous = run.Offers[slot].AugmentId;
                Check(run.Reroll(slot) && run.Offers[slot].AugmentId != previous, "exhausted pool reroll changes identity");
                if (slot == 0) Check(run.Offers[0].Tier != RewardTier.Gold, "a gold reroll with no unseen useful gold stat borrows another tier");
                var identities = new HashSet<string>();
                foreach (var offer in run.Offers)
                    Check(offer.Kind == RewardKind.Trait && identities.Add(offer.AugmentId) && run.GetHero(offer.OwnerId).CanImproveTrait(TowerRun.GetAugment(offer.AugmentId)), "three distinct useful stat cards remain");
            }
            Check(run.ChooseOffer(run.Offers[0].Id), "borrowed card still selects");
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
            foreach (var o in run.Offers) Check(o.Kind != RewardKind.Recruit && o.OwnerId != removedId, "dismissed owner's skills excluded and no recruit outside recruit floors");
            Recruit(run, removedId);
            var fresh = run.GetCompanion(removedId); Check(!Object.ReferenceEquals(removed, fresh) && fresh.Skills.Count == 0 && fresh.Traits.Count == 0 && fresh.Level == 1, "recruited copy has no old progression");
        });
        Test("with every player skill capped stat floors keep three cards and fifth floors deal no stat cards", () => {
            foreach (double roll in new[] { .1, .6, .9 }) {
                var run = new TowerRun(new ControlledRandom(roll), 80, false);
                foreach (var a in TowerRun.Augments) if (a.OwnerId == "player" && a.Kind == RewardKind.Skill) for (int i = 0; i < 3; i++) Apply(run.Player, a.Id);
                Check(run.OpenSkills == 0, "nothing left to learn");
                for (int reward = 0; reward < 12; reward++) {
                    ToReward(run); Check(run.Draft == RewardDraft.Stats && run.Floor % 5 != 0, "a skill floor with nothing to learn deals no cards");
                    for (int slot = 0; slot < 3; slot++) Check(run.Reroll(slot), "fallback reroll");
                    Check(run.Offers.Count == 3 && run.ChooseOffer(run.Offers[0].Id), "fallback selection");
                }
            }
        });
        Test("fifth floors deal only learnable skills, fewer cards when few remain and never stat cards", () => {
            var run = new TowerRun(new ControlledRandom(.9), 20, false);
            var skills = new List<string>();
            foreach (var a in TowerRun.Augments) if (a.OwnerId == "player" && a.Kind == RewardKind.Skill) skills.Add(a.Id);
            for (int s = 2; s < skills.Count; s++) for (int i = 0; i < 3; i++) Apply(run.Player, skills[s]);
            for (int floor = 1; floor < 5; floor++) { ToReward(run); Check(run.ChooseOffer(run.Offers[0].Id), "stat floor"); }
            ToReward(run);
            Check(run.Floor == 5 && run.Draft == RewardDraft.Skills && run.OpenSkills == 2 && run.Offers.Count == 2, "two learnable skills deal two cards");
            foreach (var o in run.Offers) Check(o.Kind == RewardKind.Skill && skills.IndexOf(o.AugmentId) < 2, "only the learnable skills, at any rarity");
            Check(!run.CanReroll(0) && !run.Reroll(1), "no spare skill to reroll into");
            Recruit(run, "zeus");
            Check(run.OpenSkills == 8 && run.DismissCompanion("zeus") && run.Offers.Count == 2 && run.IsRewardPending, "dismissal redeals the remaining skills");
            Check(run.ChooseOffer(run.Offers[0].Id), "take a skill");
            for (int i = 0; i < 2; i++) Apply(run.Player, skills[0]); for (int i = 0; i < 3; i++) Apply(run.Player, skills[1]);
            Check(run.OpenSkills == 0, "every skill capped");
            for (int floor = 6; floor < 10; floor++) { ToReward(run); Check(run.Floor == floor && run.Draft == RewardDraft.Stats && run.ChooseOffer(run.Offers[0].Id), "stat floor " + floor); }
            Check(run.AdvanceFloor() && run.ClearFloor() && run.Floor == 10 && !run.IsRewardPending && run.Offers.Count == 0 && run.AdvanceFloor(), "a capped fifth floor deals no stat cards in their place");
        });
        Test("one selection invalidates the entire reward including retained cards from old floors", () => {
            var run = new TowerRun(88, 100, false); ToReward(run); long first = run.Offers[0].Id, other = run.Offers[1].Id;
            Check(run.ChooseOffer(first) && !run.ChooseOffer(first) && !run.ChooseOffer(other) && !run.Reroll(1), "single consumption");
            ToReward(run); Check(!run.ChooseOffer(first) && !run.ChooseOffer(other), "ids not reused across floors");
        });
        Test("the ninetieth floor completes only after its final choice", () => {
            var run = new TowerRun(67); Check(run.ChooseOffer(run.Offers[0].Id), "opening");
            for (int floor = 1; floor <= 90; floor++) { ToReward(run); Check(!run.IsComplete && run.Floor == floor, "pending choice on floor " + run.Floor); Check(run.ChooseOffer(run.Offers[0].Id), "selection"); if (floor < 90) Check(run.AdvanceFloor(), "advance"); }
            Check(run.IsComplete && run.ClearedFloors == 90 && !run.AdvanceFloor() && !run.ClearFloor(), "completed run stops");
        });
        Test("fifth floors branch to elite, rest and shop floors which replace the next floor", () => {
            var run = new TowerRun(31, 100, false);
            for (int floor = 1; floor < 5; floor++) {
                Check(run.ClearFloor() && !run.IsBranchFloor && run.Draft == RewardDraft.Stats && run.ChooseOffer(run.Offers[0].Id), "plain floor takes its stat card");
                foreach (var type in new[] { FloorType.Elite, FloorType.Rest, FloorType.Shop }) Check(!run.AdvanceFloor(type), "no branch before floor five");
                Check(run.AdvanceFloor(), "plain advance");
            }
            Check(run.ClearFloor() && run.IsBranchFloor && run.IsRewardPending && run.Draft == RewardDraft.Skills && !run.AdvanceFloor(FloorType.Elite), "branch waits for the skill card");
            Check(run.ChooseOffer(run.Offers[0].Id) && run.AdvanceFloor(FloorType.Elite) && run.Floor == 6 && run.FloorType == FloorType.Elite, "elite floor six");
            Check(run.ClearFloor() && run.IsRewardPending && !run.IsRecruitOffer && run.IsEliteReward && run.Draft == RewardDraft.Skills && run.Offers.Count == 3, "elite clear grants skill spoils");
            Check(run.ChooseOffer(run.Offers[0].Id) && !run.IsEliteReward && !run.IsBranchFloor && !run.AdvanceFloor(FloorType.Rest) && run.AdvanceFloor() && run.FloorType == FloorType.Normal, "elite floors do not branch");
            while (run.Floor < 10) { run.ClearFloor(); if (run.IsRewardPending) run.ChooseOffer(run.Offers[0].Id); if (run.Floor < 10) run.AdvanceFloor(); }
            Check(run.ClearFloor() && run.ChooseOffer(run.Offers[0].Id) && run.AdvanceFloor(FloorType.Rest) && run.FloorType == FloorType.Rest, "rest floor eleven");
            Check(run.ClearFloor() && !run.IsRewardPending && run.ShopOffers.Count == 0 && run.ClearedFloors == 11, "rest floors clear without cards or stalls");
            var final = new TowerRun(31, 5, false); for (int floor = 1; floor <= 5; floor++) { final.ClearFloor(); if (final.IsRewardPending) final.ChooseOffer(final.Offers[0].Id); if (floor < 5) final.AdvanceFloor(); }
            Check(final.IsComplete && !final.IsBranchFloor && !final.AdvanceFloor(FloorType.Shop), "the summit never branches");
            var trial = new TowerRun(31, oathCompanionId:"michael"); for (int floor = 1; floor <= 5; floor++) { trial.ClearFloor(); trial.ChooseOffer(trial.Offers[0].Id); if (floor < 5) trial.AdvanceFloor(); }
            Check(!trial.IsBranchFloor && !trial.AdvanceFloor(FloorType.Elite), "oath trial keeps one exit");
        });
        Test("elite floors deal gold-or-better skills, borrow silver last and turn to diamond stats once nothing is left to learn", () => {
            var skills = new List<AugmentDefinition>();
            foreach (var a in TowerRun.Augments) if (a.OwnerId == "player" && a.Kind == RewardKind.Skill) skills.Add(a);
            Func<TowerRun, Action<TowerRun>, TowerRun> toElite = (run, fifth) => {
                for (int floor = 1; floor < 5; floor++) { ToReward(run); Check(run.ChooseOffer(run.Offers[0].Id), "stat floor " + floor); }
                Check(run.AdvanceFloor() && run.ClearFloor() && run.Floor == 5, "fifth floor"); fifth(run);
                Check(run.AdvanceFloor(FloorType.Elite) && run.ClearFloor() && run.IsEliteReward && !run.IsRecruitOffer, "elite spoils"); return run;
            };
            Action<TowerRun> takeSkill = r => Check(r.ChooseOffer(r.Offers[0].Id), "fifth floor skill");
            int gold = 0, diamond = 0;
            for (int seed = 1; seed <= 40; seed++) {
                var run = toElite(new TowerRun(seed, 100, false), takeSkill);
                Check(run.Draft == RewardDraft.Skills && run.Offers.Count == 3, "three skill cards " + seed);
                for (int slot = 0; slot < 3; slot++) Check(run.Reroll(slot), "spoils reroll");
                foreach (var o in run.Offers) { Check(o.Kind == RewardKind.Skill && o.Tier >= RewardTier.Gold, "gold-or-better skill: " + o.AugmentId); if (o.Tier == RewardTier.Gold) gold++; else diamond++; }
                Check(run.ChooseOffer(run.Offers[0].Id) && !run.IsEliteReward && run.AdvanceFloor() && run.ClearFloor() && !run.IsEliteReward && run.Draft == RewardDraft.Stats, "spoils end with the elite floor");
            }
            Check(gold > diamond && diamond * 4 > gold, "gold and diamond keep their two-to-one odds: " + gold + "/" + diamond);
            var silver = new TowerRun(7, 100, false);
            foreach (var a in skills) if (a.Tier >= RewardTier.Gold) for (int i = 0; i < 3; i++) Apply(silver.Player, a.Id);
            toElite(silver, takeSkill);
            Check(silver.Draft == RewardDraft.Skills && silver.Offers.Count == 3, "silver skills fill in once every high skill is capped");
            foreach (var o in silver.Offers) Check(o.Kind == RewardKind.Skill && o.Tier == RewardTier.Silver, "only silver remains: " + o.AugmentId);
            var learned = new TowerRun(9, 100, false);
            foreach (var a in skills) for (int i = 0; i < 3; i++) Apply(learned.Player, a.Id);
            toElite(learned, r => Check(!r.IsRewardPending, "a capped fifth floor deals nothing"));
            Check(learned.Draft == RewardDraft.Stats && learned.Offers.Count == 3, "diamond stats once nothing is left to learn");
            for (int slot = 0; slot < 3; slot++) Check(learned.Reroll(slot), "stat spoils reroll");
            foreach (var o in learned.Offers) Check(o.Kind == RewardKind.Trait && o.Tier == RewardTier.Diamond, "diamond stat: " + o.AugmentId);
            var party = new TowerRun(11, 100, false);
            foreach (var a in skills) for (int i = 0; i < 3; i++) Apply(party.Player, a.Id);
            Recruit(party, "zeus"); toElite(party, takeSkill);
            Check(party.Draft == RewardDraft.Skills && party.Offers.Count == 3, "the companion's skills are the spoils");
            Check(party.DismissCompanion("zeus") && party.IsRewardPending && party.IsEliteReward && party.Draft == RewardDraft.Stats && party.Offers.Count == 3, "dismissal turns the spoils to stats");
            foreach (var o in party.Offers) Check(o.Kind == RewardKind.Trait && o.Tier == RewardTier.Diamond && o.OwnerId == "player", "diamond player stat: " + o.AugmentId);
        });
        Test("shop floors trade once for a card, healing or vitality paid with gold", () => {
            var run = new TowerRun(77, 100, false);
            for (int floor = 1; floor <= 5; floor++) { run.ClearFloor(); if (run.IsRewardPending) run.ChooseOffer(run.Offers[0].Id); if (floor < 5) run.AdvanceFloor(); }
            Check(run.AdvanceFloor(FloorType.Shop) && run.FloorType == FloorType.Shop && run.ShopOffers.Count == 0, "stall opens only after the clear");
            var wallet = TowerProgress.Load("3|200");
            Check(!run.CanBuy(ShopItem.Heal, 999) && !run.Buy(ShopItem.Heal, wallet), "cannot buy before the floor clears");
            Check(run.ClearFloor() && !run.IsRewardPending && run.ShopOffers.Count == 3 && !run.ShopTraded, "shop clears into three wares");
            Check(!run.CanBuy(ShopItem.Augment, TowerRun.AugmentPrice - 1) && !run.Buy(ShopItem.Augment, TowerProgress.Load("3|" + (TowerRun.AugmentPrice - 1))), "gold must cover the price");
            Check(run.GetShopOffer(ShopItem.Heal).Price == TowerRun.HealPrice && TowerRun.HealPrice < TowerRun.VitalityPrice && TowerRun.VitalityPrice < TowerRun.AugmentPrice, "healing is the cheapest ware and a card the dearest");
            Check(run.Buy(ShopItem.Augment, wallet) && wallet.Gold == 200 - TowerRun.AugmentPrice && run.IsRewardPending && !run.IsRecruitOffer && run.Draft == RewardDraft.Mixed && run.Offers.Count == 3, "bought card opens a mixed reward");
            Check(run.ShopTraded && run.ShopPurchases == 1, "one trade recorded");
            Check(run.ChooseOffer(run.Offers[0].Id), "take the bought card");
            foreach (var item in new[] { ShopItem.Augment, ShopItem.Heal, ShopItem.Vitality })
                Check(!run.CanBuy(item, 9999) && !run.Buy(item, wallet), "the merchant trades only once per floor");
            Check(wallet.Gold == 200 - TowerRun.AugmentPrice, "refused trades spend nothing");
            Check(run.AdvanceFloor() && run.ShopOffers.Count == 0 && !run.ShopTraded, "stall closes on leaving");
            var vitality = new TowerRun(78, 100, false);
            for (int floor = 1; floor <= 5; floor++) { vitality.ClearFloor(); if (vitality.IsRewardPending) vitality.ChooseOffer(vitality.Offers[0].Id); if (floor < 5) vitality.AdvanceFloor(); }
            Check(vitality.AdvanceFloor(FloorType.Shop) && vitality.ClearFloor(), "second shop");
            float health = vitality.Player.MaxHealth; int before = wallet.Gold;
            Check(vitality.Buy(ShopItem.Vitality, wallet) && wallet.Gold == before - TowerRun.VitalityPrice && Math.Abs(vitality.Player.MaxHealth - health - TowerRun.VitalityHealth) < .001f, "vitality raises max health");
            Check(!vitality.Buy(ShopItem.Heal, wallet) && wallet.Gold == before - TowerRun.VitalityPrice, "a heal after vitality is refused");
        });
        Test("two or three companions of one mythology unlock its synergy for the right heroes", () => {
            var run = new TowerRun(3, 100, false);
            Recruit(run, "zeus"); Recruit(run, "odin");
            var zeus = run.GetCompanion("zeus"); float basePower = zeus.SkillPowerMultiplier, playerPower = run.Player.SkillPowerMultiplier;
            Check(run.ActiveSynergies.Count == 0 && run.MythCount("olympus") == 1, "one member is not a synergy");
            Recruit(run, "poseidon");
            Check(run.ActiveSynergies.Count == 1 && run.ActiveSynergies[0].Definition.MythId == "olympus" && run.ActiveSynergies[0].Level == 2, "two olympians activate");
            Close(zeus.SkillPowerMultiplier, basePower + .25f, "member skill power"); Close(run.GetCompanion("poseidon").SkillPowerMultiplier, basePower + .25f, "second member");
            Close(run.GetCompanion("odin").SkillPowerMultiplier, 1, "other mythology unaffected"); Close(run.Player.SkillPowerMultiplier, playerPower, "player unaffected");
            Check(run.DismissCompanion("odin"), "make room"); Recruit(run, "hades");
            Check(run.ActiveSynergies[0].Members == 3 && run.ActiveSynergies[0].Level == 3, "three olympians");
            Close(zeus.SkillPowerMultiplier, basePower + .5f, "three-member power replaces two"); Close(zeus.CritChance, .1f, "three-member crit");
            Check(run.DismissCompanion("hades") && run.DismissCompanion("poseidon"), "break the set");
            Close(zeus.SkillPowerMultiplier, basePower, "synergy ends with the party");
            var korea = new TowerRun(4, 100, false); float health = korea.Player.MaxHealth;
            Recruit(korea, "dangun"); Recruit(korea, "jumong");
            Close(korea.Player.MaxHealth, health * 1.15f, "player-target synergy"); Close(korea.Player.Armor, 4, "player armor synergy");
            Close(korea.GetCompanion("dangun").MaxHealth, TowerRun.GetDefinition("dangun").MaxHealth, "player-only bonus skips companions");
            var asgard = new TowerRun(5, 100, false); float damage = asgard.Player.Damage; Recruit(asgard, "thor"); Recruit(asgard, "loki"); Recruit(asgard, "zeus");
            Close(asgard.Player.Damage, damage * 1.12f, "party synergy reaches the player"); Close(asgard.GetCompanion("zeus").Damage, TowerRun.GetDefinition("zeus").Damage * 1.12f, "and every companion");
            foreach (var synergy in TowerMythCatalog.Synergies)
                Check(synergy.Bonuses(2).Count > 0 && synergy.Bonuses(3).Count >= synergy.Bonuses(2).Count && synergy.Bonuses(1).Count == 0 && !String.IsNullOrEmpty(synergy.Describe(3)), "complete synergy: " + synergy.MythId);
        });
        TowerMythTests.Register(Test);
        TowerOathTests.Register(Test);
        TowerLootTests.Register(Test);
        TowerPassiveTests.Register(Test);
        Console.WriteLine(passed + " passed, " + failed + " failed"); return failed == 0 ? 0 : 1;
    }
}
