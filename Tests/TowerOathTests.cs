using System;
using System.Collections.Generic;
using PrototypeLab.Roguelike;

public static class TowerOathTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Register(Action<string, Action> test)
    {
        test("oath charge arms the next sword hit and perfect dodges never activate it", () => {
            var oath = new OathState("michael");
            for (int i = 0; i < 4; i++) Check(!oath.OnSwordHit() && oath.Charge == i + 1, "charging hit cannot trigger");
            Check(oath.Ready && oath.Activations == 0, "four hits arm");
            oath.OnPerfectDodge(); Check(oath.Charge == 4 && oath.Activations == 0, "charge capped");
            Check(oath.OnSwordHit() && oath.Charge == 0 && oath.Activations == 1, "next hit activates once");
            oath.OnPerfectDodge(); Check(oath.Charge == 2, "dodge charges two");
            oath.OnPerfectDodge(); Check(oath.Ready, "two dodges ready");
        });
        test("oath modifiers combine, do not duplicate, and preserve a ready charge", () => {
            var oath = new OathState("gabriel");
            Check(!oath.Acquire(OathState.MichaelCircle) && !oath.Acquire("unknown"), "foreign and unknown rejected");
            oath.OnPerfectDodge(); oath.OnPerfectDodge();
            Check(oath.Acquire(OathState.Echo) && oath.RequiredCharge == 6 && oath.Ready, "echo cost preserves ready");
            Check(oath.Acquire(OathState.Overdrive) && oath.RequiredCharge == 8 && oath.Power == 2 && oath.Ready, "costs stack");
            Check(oath.OnSwordHit() && !oath.Ready, "combined link consumed");
            Check(!oath.Acquire(OathState.Echo), "no duplicate modifier");
            oath.Acquire(OathState.Dodge); oath.OnPerfectDodge(); Check(oath.Charge == 8, "dodge fills changed capacity");
        });
        test("trial offers cover every modifier and end immediately at floor ten across seeds", () => {
            foreach (string id in new[] { "michael", "gabriel", "raphael" })
                for (int seed = 0; seed < 60; seed++) {
                    var run = new TowerRun(seed, oathCompanionId:id);
                    Check(run.IsOathTrial && run.MaxFloor == 10 && run.Companions.Count == 1 && run.GetCompanion(id) != null, "starter oath");
                    Check(!run.DismissCompanion(id), "bound companion retained");
                    for (int floor = 1; floor <= 10; floor++) {
                        Check(run.ClearFloor() && !run.ClearFloor(), "one clear per floor");
                        Check(!TowerLoot.HasCaches(run), "trial never places caches, so it never farms gold");
                        if (floor == 10) {
                            Check(run.IsComplete && !run.IsRewardPending && run.Offers.Count == 0 && !run.Reroll(0) && !run.AdvanceFloor(), "ten finishes without reward");
                            break;
                        }
                        Check(run.Offers.Count == 3 && run.IsRewardPending, "three choices");
                        if (floor <= 5) Check(run.Offers[0].Kind == RewardKind.Oath, "guaranteed unowned modifier");
                        var identities = new HashSet<string>();
                        foreach (var offer in run.Offers) {
                            Check(identities.Add(offer.AugmentId), "distinct offers");
                            if (offer.Kind == RewardKind.Recruit) Check(OathState.Supports(offer.OwnerId), "trial roster");
                        }
                        long stale = run.Offers[0].Id;
                        Check(run.ChooseOffer(stale) && !run.ChooseOffer(stale), "one reward only");
                        Check(run.AdvanceFloor(), "advance");
                    }
                    Check(run.Oath.Acquired.Count == 5 && run.ClearedFloors == 10, "all five applicable variants reachable in a trial");
                }
        });
        test("oath reroll changes identity and falls back safely after the last variant", () => {
            var run = new TowerRun(17, oathCompanionId:"raphael");
            for (int floor = 1; floor <= 9; floor++) {
                run.ClearFloor();
                string previous = run.Offers[0].AugmentId;
                Check(run.Reroll(0) && run.Offers[0].AugmentId != previous && !run.Reroll(0), "one identity-changing reroll");
                Check(run.Reroll(1) && run.Reroll(2), "other slots reroll");
                Check(run.ChooseOffer(run.Offers[0].Id) && run.AdvanceFloor(), "choice works even when oath pool exhausted");
            }
        });
        test("normal runs keep oath variants out of their reward and progress rules", () => {
            var run = new TowerRun(42, 5, false);
            Check(run.Oath == null && run.Companions.Count == 0 && !run.IsOathTrial, "no free ally in normal mode");
            for (int floor = 1; floor <= 5; floor++) { Check(run.ClearFloor(), "clear"); if (floor < 5) Check(!run.IsRewardPending && run.AdvanceFloor(), "plain floor"); }
            Check(run.IsRewardPending && !run.IsComplete, "final fifth floor still offers its card");
            foreach (var offer in run.Offers) Check(offer.Kind != RewardKind.Oath, "oath trial isolated");
            Check(TowerLoot.HasCaches(run), "normal runs place breakable caches");
        });
    }
}
