using System;
using System.Collections.Generic;
using PrototypeLab.Roguelike;

public static class TowerPassiveTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Near(float actual, float expected, string message)
    { Check(Math.Abs(actual - expected) < .001f, message + ": " + actual + " != " + expected); }
    private static TowerRun Party(params string[] ids) { return TowerRun.ForChapter(7, TowerCampaign.Chapters[0], ids); }
    private static PassiveTarget Target(float health = 1, bool elite = false, bool boss = false, bool slowed = false, bool marked = false, float distance = 2, float player = 1)
    { return new PassiveTarget { HealthFraction = health, Elite = elite, Boss = boss, Slowed = slowed, Marked = marked, Distance = distance, PlayerHealthFraction = player }; }

    public static void Register(Action<string, Action> test)
    {
        test("every companion carries one passive of its own, named and described from its legend", () => {
            Check(TowerPassiveCatalog.Passives.Count == 50 && TowerRun.Definitions.Count == 50, "fifty passives for fifty companions");
            var kinds = new HashSet<PassiveKind>(); var names = new HashSet<string>();
            foreach (var definition in TowerRun.Definitions)
            {
                var p = definition.Passive;
                Check(p != null && p.CompanionId == definition.Id && TowerPassiveCatalog.For(definition.Id) == p, "passive resolves for " + definition.Id);
                Check(kinds.Add(p.Kind) && names.Add(p.Name), "each passive is unique: " + definition.Id);
                Check(!String.IsNullOrEmpty(p.Name) && !String.IsNullOrEmpty(p.Lore) && !String.IsNullOrEmpty(p.Description), "complete passive text: " + definition.Id);
                Check(p.Description.IndexOf('{') < 0 && p.Description.IndexOf('}') < 0, "every number is filled in: " + p.Description);
                // The recruit card leaves about three short lines for the passive.
                Check(p.Description.Length <= 66, "passive fits the recruit card: " + definition.Id + " " + p.Description.Length);
                Check(definition.InnateTrait == p.Name && definition.Description.Contains(p.Name) && definition.Description.Contains(p.Description), "the companion presents its passive: " + definition.Id);
                if (p.IsPeriodic || p.HasCooldown) Check(p.Interval > 0, "timed passive has an interval: " + definition.Id);
                if (p.Kind == PassiveKind.SplitShot || p.Kind == PassiveKind.ChainStrike || p.Kind == PassiveKind.Mjolnir || p.Kind == PassiveKind.GroundSlam || p.Kind == PassiveKind.Insight)
                    Check(p.Every > 1, "empowered attack has a count: " + definition.Id);
            }
            Check(kinds.Count == Enum.GetValues(typeof(PassiveKind)).Length, "every passive kind belongs to someone");
            Check(TowerPassiveCatalog.For("missing") == null && TowerPassiveCatalog.For(null) == null, "unknown lookup is safe");
            var offer = new RewardOffer(1, 0, TowerRun.GetDefinition("osiris"));
            Check(offer.Description.Contains("녹색 왕권") && offer.Description.Contains("부활"), "the recruit card shows the passive");
        });
        test("a passive acts only while its god walks with the party", () => {
            var run = Party("michael", "osiris");
            var passives = run.Passives;
            Check(passives.Has(PassiveKind.GiantSlayer) && passives.Has(PassiveKind.Rebirth) && !passives.Has(PassiveKind.Omen), "party passives are active");
            Check(passives.Holder(PassiveKind.Rebirth) == run.GetCompanion("osiris"), "the holder is the companion");
            Near(passives.DamageBonus(run.Player, Target(elite: true)), .25f, "giant slayer before parting");
            Check(run.DismissCompanion("michael"), "dismiss michael");
            Check(!passives.Has(PassiveKind.GiantSlayer), "a dismissed god takes the passive along");
            Near(passives.DamageBonus(run.Player, Target(elite: true)), 0, "no giant slayer after parting");
            Check(!passives.OnBasicAttack(null), "no counter without a companion");
        });
        test("damage passives add up against the foes their legends name", () => {
            var run = Party("michael", "poseidon", "morrigan"); var p = run.Passives;
            Near(p.DamageBonus(run.Player, Target()), 0, "a plain healthy foe");
            Near(p.DamageBonus(run.Player, Target(health: .2f, elite: true, slowed: true)), .25f + .2f + .35f, "a slowed, wounded elite");
            Near(p.DamageBonus(run.GetCompanion("poseidon"), Target(boss: true, health: .31f)), .25f, "the omen waits for thirty percent");
            run = Party("dangun", "tyr", "anubis"); p = run.Passives;
            Near(p.DamageBonus(run.Player, Target(player: .51f)), .18f, "hongik grows with each companion");
            Near(p.DamageBonus(run.Player, Target(player: .5f)), .18f + .3f, "tyr's resolve at half health");
            Near(p.DamageBonus(run.Player, Target(elite: true, health: .3f)), .18f + .2f, "anubis weighs a wounded elite");
            Check(p.Judges(Target(health: .15f)) && !p.Judges(Target(health: .16f)) && !p.Judges(Target(health: .1f, elite: true)) && !p.Judges(Target(health: .1f, boss: true)), "only normal foes are judged");
            Check(run.DismissCompanion("tyr"), "dismiss tyr");
            Near(p.DamageBonus(run.Player, Target(player: .2f)), .12f, "hongik counts the party that remains");
            run = Party("lugh", "erlang_shen"); p = run.Passives; var lugh = run.GetCompanion("lugh");
            Near(p.DamageBonus(lugh, Target(distance: 3)), 0, "no long-arm bonus up close");
            Near(p.DamageBonus(lugh, Target(distance: 6)), .3f, "half the bonus at six");
            Near(p.DamageBonus(lugh, Target(distance: 20)), .6f, "full bonus at range");
            Near(p.DamageBonus(run.Player, Target(distance: 20)), 0, "the long arm is lugh's own");
            Near(p.DamageBonus(run.Player, Target(marked: true)), .2f, "the third eye marks for the whole party");
        });
        test("empowered passives strike on exactly their nth basic attack", () => {
            var run = Party("thor", "gabriel"); var p = run.Passives; var thor = run.GetCompanion("thor");
            Check(p.Status(thor) == "강화 공격까지 4회", "countdown before attacking: " + p.Status(thor));
            var pattern = new List<bool>(); for (int i = 0; i < 8; i++) pattern.Add(p.OnBasicAttack(thor));
            Check(!pattern[0] && !pattern[1] && !pattern[2] && pattern[3] && !pattern[4] && !pattern[6] && pattern[7], "every fourth hammer blow");
            var gabriel = run.GetCompanion("gabriel");
            Check(!p.OnBasicAttack(gabriel) && !p.OnBasicAttack(gabriel) && p.OnBasicAttack(gabriel), "every third arrow splits");
            Check(p.Status(gabriel) == "강화 공격까지 3회", "gabriel's countdown restarts");
            Check(run.DismissCompanion("thor") && !p.OnBasicAttack(thor), "a departed companion no longer counts");
        });
        test("fight-long stat passives rise and fade with the fight", () => {
            var run = Party("nezha", "paimon", "horus"); var p = run.Passives; var nezha = run.GetCompanion("nezha");
            float baseInterval = nezha.AttackInterval, playerInterval = run.Player.AttackInterval;
            for (int i = 0; i < 7; i++) p.OnBasicAttack(nezha);
            Check(p.MomentumStacks == 5, "momentum caps at five");
            Near(nezha.AttackInterval, baseInterval / 1.5f, "five wheels of momentum");
            Near(run.GetCompanion("paimon").AttackInterval, run.GetCompanion("paimon").Definition.AttackInterval, "momentum is nezha's own");
            p.Tick(2.4f); Check(p.MomentumStacks == 5, "momentum holds while fighting on");
            p.Tick(.2f); Check(p.MomentumStacks == 0, "momentum resets after a pause");
            Check(!p.Periodic(PassiveKind.Melody, 9.9f, true) && p.Periodic(PassiveKind.Melody, .1f, true), "the melody plays every ten seconds");
            Near(p.MelodyRemaining, 4, "the melody lasts four seconds");
            Near(run.Player.AttackInterval, playerInterval / 1.3f, "the melody quickens the player");
            Near(nezha.AttackInterval, baseInterval / 1.3f, "and every companion");
            p.Tick(4); Near(run.Player.AttackInterval, playerInterval, "the melody ends");
            Check(!p.Periodic(PassiveKind.Melody, 12, false) && p.Periodic(PassiveKind.Melody, 0, true), "a full clock waits for a target, then plays at once");
            var horus = run.GetCompanion("horus");
            Near(horus.CritChance, .25f, "hawk-eye critical chance");
            Near(p.CriticalMultiplier(horus), 2.5f, "hawk-eye critical damage");
            Near(p.CriticalMultiplier(run.Player), TowerPassiveCatalog.BaseCriticalMultiplier, "others keep the base critical damage");
            run = Party("lugh", "zodiac_ox"); p = run.Passives;
            var lugh = run.GetCompanion("lugh");
            Near(lugh.Range, lugh.Definition.Range * 1.3f, "lugh's long arm reaches further");
            float armor = run.Player.Armor;
            for (int i = 0; i < 7; i++) p.OnPlayerHurt();
            Check(p.EndureStacks == 5, "endurance caps at five");
            Near(run.Player.Armor, armor + 30, "the ox lends armor to the player");
            Near(run.GetCompanion("zodiac_ox").Armor, run.GetCompanion("zodiac_ox").Definition.Armor, "but not to itself");
            p.Tick(4); Near(run.Player.Armor, armor, "endurance fades");
        });
        test("guardian passives shield the player on their own terms", () => {
            var run = Party("athena", "tyr", "ganesha"); var p = run.Passives;
            Near(p.DamageTakenScale(1), .88f, "the aegis turns blows");
            Near(p.DamageTakenScale(.5f), .88f * .85f, "tyr's resolve at half health");
            Near(p.TrapDamageScale, .5f, "ganesha clears the traps' path");
            Check(p.BlocksLeft == 0, "no block before the floor starts");
            p.OnFloorStart(); Check(p.BlocksLeft == 1 && p.TryBlock() && !p.TryBlock(), "one block per floor");
            p.OnFloorStart(); Check(p.TryBlock(), "the next floor renews the block");
            Check(p.TryCooldown(PassiveKind.Aegis) && !p.TryCooldown(PassiveKind.Aegis), "the aegis petrifies, then recovers");
            Check(p.Status(run.GetCompanion("athena")) == "재사용까지 6초", "aegis cooldown shows: " + p.Status(run.GetCompanion("athena")));
            p.Tick(6); Check(p.TryCooldown(PassiveKind.Aegis), "the aegis is ready again");
            run = Party("osiris", "isis"); p = run.Passives; var osiris = run.GetCompanion("osiris");
            Check(p.Status(osiris) == "부활 준비됨" && p.TryRebirth() && !p.TryRebirth(), "one rebirth, then a recharge");
            Check(p.Status(osiris) == "재충전까지 3층", "recharge countdown: " + p.Status(osiris));
            p.OnFloorCleared(); p.OnFloorCleared(); Check(!p.TryRebirth(), "two floors are not enough");
            p.OnFloorCleared(); Check(p.TryRebirth(), "the third floor recharges the rebirth");
            Check(p.TryCooldown(PassiveKind.Wings) && !p.TryCooldown(PassiveKind.Wings), "isis spreads her wings once");
            p.Tick(19.9f); Check(!p.TryCooldown(PassiveKind.Wings), "and waits twenty seconds");
            p.Tick(.1f); Check(p.TryCooldown(PassiveKind.Wings), "before spreading them again");
            Check(run.DismissCompanion("isis") && !p.TryCooldown(PassiveKind.Wings), "no wings without isis");
            Near(Party("zodiac_rat").Passives.DodgeWindowBonus, .1f, "the rat widens the perfect dodge");
            Near(Party("osiris").Passives.DodgeWindowBonus, 0, "others keep the base window");
        });
        test("healing and fury passives scale with the wound and the slain", () => {
            var bari = TowerPassiveCatalog.For("princess_bari");
            Near(TowerPassiveState.LifewaterScale(bari, 0), 1, "a full-health heal is plain");
            Near(TowerPassiveState.LifewaterScale(bari, .5f), 2, "half health doubles it");
            Near(TowerPassiveState.LifewaterScale(bari, 1), 3, "the brink triples it");
            Near(TowerPassiveState.LifewaterScale(bari, 4), 3, "and no further");
            var run = Party("asmodeus", "hwanung"); var p = run.Passives;
            for (int i = 0; i < 7; i++) p.OnKill();
            Check(p.WrathStacks == 5, "wrath caps at five");
            Near(p.DamageBonus(run.Player, Target()), .3f, "five kills of wrath");
            p.Tick(5.9f); Check(p.WrathStacks == 5, "wrath lingers");
            p.OnKill(); p.Tick(5.9f); Check(p.WrathStacks == 5, "a kill renews it");
            p.Tick(.2f); Check(p.WrathStacks == 0, "wrath fades without kills");
            Check(p.NextSeal() == 0 && p.NextSeal() == 1 && p.NextSeal() == 2 && p.NextSeal() == 0, "wind, rain and cloud take turns");
            var hwanung = run.GetCompanion("hwanung");
            Check(p.Status(hwanung) == "다음 발동까지 8초", "periodic countdown: " + p.Status(hwanung));
            Check(p.Periodic(PassiveKind.ThreeSeals, 8, true) && p.Status(hwanung) == "다음 발동까지 8초", "the clock restarts after firing");
            p.Periodic(PassiveKind.ThreeSeals, 9, false); Check(p.Status(hwanung) == "발동 준비됨", "a full clock is ready");
            Check(!p.Periodic(PassiveKind.Gungnir, 99, true), "a passive outside the party never fires");
        });
    }
}
