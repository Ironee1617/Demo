using System;
using System.Collections.Generic;
using PrototypeLab.Roguelike;

// Story campaign rules: chapters, the companion pick that opens each expedition, expedition growth and saves.
public static class TowerCampaignTests
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }

    // Climbs an expedition to its last floor through the real reward flow.
    private static void Climb(TowerRun run)
    {
        while (true)
        {
            Require(run.ClearFloor(), "clear floor " + run.Floor);
            Require(!run.IsRecruitOffer, "chapters never recruit inside the tower (floor " + run.Floor + ")");
            if (run.IsRewardPending) Require(run.ChooseOffer(run.Offers[0].Id), "choose reward on " + run.Floor);
            if (run.IsComplete) return;
            Require(run.AdvanceFloor(), "advance from " + run.Floor);
        }
    }
    // Takes every companion pick the expedition opens with, checking each against the chapter's pool.
    private static int Draft(TowerRun run, Func<IReadOnlyList<RewardOffer>, RewardOffer> pick = null)
    {
        var pool = new List<string>(TowerCampaign.CompanionPool(run.Chapter)); int sets = 0;
        while (run.IsRewardPending)
        {
            Require(run.IsRecruitOffer && run.IsCompanionPick && !run.IsFloorCleared, "an expedition opens with the companion pick only");
            Require(run.Offers.Count == run.OpenRecruits, "every free god of the pool is offered");
            var dealt = new HashSet<string>();
            foreach (var offer in run.Offers)
            {
                Require(offer.Kind == RewardKind.Recruit && pool.Contains(offer.CompanionId), "card " + offer.CompanionId + " comes from the chapter's pool");
                Require(run.GetCompanion(offer.CompanionId) == null && dealt.Add(offer.CompanionId), "no god is dealt twice or after joining");
            }
            var chosen = pick == null ? run.Offers[0] : pick(run.Offers);
            Require(run.ChooseOffer(chosen.Id), "draft pick " + chosen.CompanionId);
            sets++;
        }
        return sets;
    }
    private static List<string> Ids(TowerRun run)
    { var ids = new List<string>(); foreach (var companion in run.Companions) ids.Add(companion.Id); return ids; }
    private static int Levels(TowerRun run)
    {
        int total = run.Player.Level;
        foreach (var companion in run.Companions) total += companion.Level;
        return total;
    }
    // Plays chapters with first-card picks so later chapters can be tested.
    private static TowerCampaignSave Progress(int chapters)
    {
        var save = new TowerCampaignSave();
        for (int c = 0; c < chapters; c++)
        {
            var run = save.BeginChapter(300 + c); Draft(run); Climb(run);
            Require(save.RecordChapterClear(run), "record chapter " + c);
        }
        return save;
    }

    public static void Run(Action<string, Action> test)
    {
        test("nine story chapters cover ninety floors with two gods of one myth each before the final ascent", () => {
            Require(TowerCampaign.Chapters.Count == 9 && TowerCampaign.TotalFloors == 90, "nine chapters, ninety floors");
            var seen = new HashSet<string>();
            for (int i = 0; i < TowerCampaign.Chapters.Count; i++)
            {
                var chapter = TowerCampaign.Chapters[i];
                Require(chapter.Index == i && chapter.FirstFloor == i * 10 + 1 && chapter.LastFloor == i * 10 + 10, "chapter floors " + i);
                Require(TowerThemeCatalog.ForFloor(chapter.FirstFloor).Id == chapter.Theme && TowerThemeCatalog.ForFloor(chapter.LastFloor).Id == chapter.Theme, "chapter theme " + i);
                Require(Object.ReferenceEquals(TowerCampaign.ForFloor(chapter.FirstFloor), chapter) && Object.ReferenceEquals(TowerCampaign.ForFloor(chapter.LastFloor), chapter), "floor lookup " + i);
                Require(!String.IsNullOrEmpty(chapter.Intro) && !String.IsNullOrEmpty(chapter.RestoredTitle) && !String.IsNullOrEmpty(chapter.RestoredBody), "chapter story text " + i);
                if (chapter.IsFinal) { Require(chapter.Candidates.Count == 0 && chapter.MythId == null && chapter.Theme == TowerThemeId.Abyss, "the final ascent adds no new gods"); continue; }
                Require(chapter.Candidates.Count == 2 && TowerMythCatalog.Get(chapter.MythId) != null, "two candidates of a real myth in chapter " + i);
                foreach (string id in chapter.Candidates)
                {
                    var god = TowerRun.GetDefinition(id);
                    Require(god != null && god.MythId == chapter.MythId && seen.Add(id), "candidate " + id + " belongs to its chapter myth");
                    Require(!String.IsNullOrEmpty(chapter.Line(id)), "candidate line for " + id);
                }
            }
            Require(TowerCampaign.Chapters[0].Theme == TowerThemeId.Olympus && TowerCampaign.Chapters[0].Candidates.Count == 2 && TowerCampaign.Chapters[1].MythId == "asgard", "Olympus first, then Asgard");
            Require(TowerCampaign.Chapters[7].Theme == TowerThemeId.Hell && TowerCampaign.Chapters[7].MythId == "solomon", "Solomon's demons in hell");
            Require(TowerCampaign.Prologue.Count >= 3 && TowerCampaign.Epilogue.Count >= 2, "prologue and epilogue pages");
        });
        test("each chapter drafts from every myth so far and opens a companion slot past floors 30 and 60", () => {
            int[] slots = { 1, 1, 1, 2, 2, 2, 3, 3, 3 };
            for (int i = 0; i < TowerCampaign.Chapters.Count; i++)
            {
                var chapter = TowerCampaign.Chapters[i]; var pool = TowerCampaign.CompanionPool(chapter);
                Require(pool.Count == 2 * Math.Min(i + 1, 8), "pool size before chapter " + i + ": " + pool.Count);
                for (int c = 0; c <= i; c++) foreach (string id in TowerCampaign.Chapters[c].Candidates) Require(new List<string>(pool).Contains(id), id + " drafted before chapter " + i);
                Require(TowerCampaign.CompanionSlots(chapter) == slots[i], "companion slots before chapter " + i);
            }
            var olympus = TowerCampaign.CompanionPool(TowerCampaign.Chapters[0]);
            Require(olympus[0] == "zeus" && olympus[1] == "poseidon", "the first draft is the two Olympians");
            var asgard = TowerCampaign.CompanionPool(TowerCampaign.Chapters[1]);
            Require(asgard.Count == 4 && asgard[2] == "odin" && asgard[3] == "thor", "restoring Olympus adds Asgard's two gods");
        });
        test("the first expedition opens with both Olympians and one pick", () => {
            var save = new TowerCampaignSave(); var first = TowerCampaign.Chapters[0];
            Require(save.ClearedChapters == 0 && Object.ReferenceEquals(save.NextChapter, first) && save.UnlockedFloor == 10 && !save.IsComplete, "fresh state");
            Require(save.CompanionSlots == 1 && save.CompanionPool.Count == 2, "one of the two Olympians");
            Require(save.CanDepart(null) && save.CanDepart(new string[0]) && save.CanDepart(new[] { "zeus" }), "departures draft or keep pool gods");
            Require(!save.CanDepart(new[] { "athena" }) && !save.CanDepart(new[] { "odin" }), "only pool gods may come");
            Require(!save.CanDepart(new[] { "zeus", "poseidon" }) && !save.CanDepart(new[] { "zeus", "zeus" }) && !save.CanDepart(new string[] { null }), "no more than the open slots, each god once");
            bool rejected = false; try { save.BeginChapter(1, new[] { "athena" }); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "invalid parties cannot depart");
            var run = save.BeginChapter(2);
            Require(run.IsCampaign && run.Floor == 1 && run.Companions.Count == 0 && run.CompanionLimit == 1, "the expedition starts without companions");
            Require(run.IsRewardPending && run.IsRecruitOffer && run.Offers.Count == 2 && !run.CanReroll(0) && !run.Reroll(1), "two cards and no spare god to reroll into");
            Require(!run.ClearFloor() && !run.AdvanceFloor(), "the draft comes before the first fight");
            var ids = new HashSet<string> { run.Offers[0].CompanionId, run.Offers[1].CompanionId };
            Require(ids.Contains("zeus") && ids.Contains("poseidon"), "both Olympians are dealt");
            Require(Draft(run, offers => offers[0].CompanionId == "poseidon" ? offers[0] : offers[1]) == 1, "a single draft");
            Require(run.Companions.Count == 1 && run.GetCompanion("poseidon") != null && !run.IsRewardPending && run.ClearFloor(), "Poseidon joins and the fight begins");
        });
        test("the opening pick lays out every free god of the pool in chapter order, whatever the seed, with nothing to reroll", () => {
            var save = Progress(1);
            Require(save.CompanionSlots == 1 && save.CompanionPool.Count == 4, "Olympus and Asgard after the first chapter");
            for (int seed = 0; seed < 20; seed++)
            {
                var run = save.BeginChapter(seed);
                Require(run.IsCompanionPick && run.Offers.Count == 4 && run.OpenRecruits == 4, "all four gods are offered");
                for (int i = 0; i < run.Offers.Count; i++)
                {
                    Require(run.Offers[i].CompanionId == save.CompanionPool[i], "pool order at " + i + ": " + run.Offers[i].CompanionId);
                    Require(!run.CanReroll(i) && !run.Reroll(i), "nothing to reroll at " + i);
                }
                Require(run.ChooseOffer(run.Offers[3].Id) && run.GetCompanion("thor") != null && !run.IsRewardPending && !run.IsCompanionPick, "any god may be chosen, here the last one");
                Require(run.ClearFloor() && run.IsRewardPending && !run.IsCompanionPick && run.CanReroll(0), "floor cards after the pick still reroll");
            }
        });
        test("past floor 30 the pick repeats for a second companion and past floor 60 for a third", () => {
            var save = Progress(3);
            Require(save.CompanionSlots == 2 && save.CompanionPool.Count == 8, "chapter four picks two of eight");
            var run = save.BeginChapter(77);
            Require(run.Floor == 1 && run.MaxFloor == 40 && run.CompanionLimit == 2 && run.Offers.Count == 8, "first pick");
            string first = run.Offers[2].CompanionId;
            Require(run.ChooseOffer(run.Offers[2].Id) && run.IsCompanionPick && run.OpenRecruits == 7 && run.Offers.Count == 7, "the second pick follows at once");
            foreach (var offer in run.Offers) Require(offer.CompanionId != first, "the first pick is not dealt again");
            Require(Draft(run) == 1 && run.Companions.Count == 2 && !run.IsRewardPending, "two companions");
            Require(run.ClearFloor() && !run.IsRecruitOffer, "the fight begins once both companions joined");
            save = Progress(6);
            Require(save.CompanionSlots == 3 && save.CompanionPool.Count == 14, "chapter seven drafts three of fourteen");
            run = save.BeginChapter(78);
            Require(Draft(run) == 3 && run.Companions.Count == 3 && run.Floor == 1 && run.MaxFloor == 70, "three companions past floor 60");
            save = Progress(8);
            Require(save.NextChapter.IsFinal && save.CompanionSlots == 3 && save.CompanionPool.Count == 16, "the final ascent drafts from all sixteen gods");
            run = save.BeginChapter(79);
            Require(Draft(run) == 3 && run.Companions.Count == 3 && run.Floor == 1 && run.MaxFloor == 90, "three companions face the evil god");
        });
        test("each restored myth returns the hero to the village, opens ten more floors and resets every augment; every expedition starts again on floor 1", () => {
            var save = new TowerCampaignSave(); var fresh = new TowerCampaignSave().Preview(7, new string[0]).Player;
            for (int c = 0; c < TowerCampaign.Chapters.Count; c++)
            {
                var chapter = save.NextChapter; Require(chapter.Index == c && save.UnlockedFloor == chapter.LastFloor, "unlocked through chapter " + c);
                var run = save.BeginChapter(1000 + c);
                Require(run.IsCampaign && Object.ReferenceEquals(run.Chapter, chapter) && run.Floor == 1 && run.MaxFloor == chapter.LastFloor, "expedition floors " + c);
                Require(run.Theme.Id == TowerThemeId.Olympus && run.Player.Level == 1 && run.Player.Skills.Count == 0 && run.Player.Traits.Count == 0, "expedition opens on Olympus with a fresh hero");
                Require(Draft(run) == save.CompanionSlots, "one draft per slot in chapter " + c);
                foreach (var companion in run.Companions) Require(companion.Level == 1 && companion.Skills.Count == 0, companion.Id + " joins without earlier augments");
                Require(run.Companions.Count == save.CompanionSlots, "party size in chapter " + c);
                Require(!save.RecordChapterClear(run), "an unfinished expedition is not recorded");
                int joined = Levels(run);
                Climb(run);
                Require(run.IsComplete && run.Floor == chapter.LastFloor && run.ClearedFloors == chapter.LastFloor && run.Theme.Id == chapter.Theme, "climbed from floor 1 through chapter " + c);
                int grown = Levels(run);
                Require(grown >= joined + 2, "two fifth-floor rewards per chapter");
                Require(save.RecordChapterClear(run) && save.ClearedChapters == c + 1, "chapter recorded " + c);
                Require(!save.RecordChapterClear(run), "a chapter is recorded once");
                Require(save.IsRestored(chapter), "chapter restored");
                if (!save.IsComplete)
                {
                    // Coming home resets every augment: the same gods start over at level one.
                    var preview = save.Preview(7, Ids(run));
                    Require(Levels(preview) == 1 + run.Companions.Count, "village preview starts over (" + Levels(preview) + " vs " + grown + ")");
                    Require(Math.Abs(preview.Player.Damage - fresh.Damage) < .001f && Math.Abs(preview.Player.MaxHealth - fresh.MaxHealth) < .001f, "no stat bonus survives the return");
                }
            }
            Require(save.IsComplete && save.NextChapter == null && save.UnlockedFloor == 90 && save.CompanionSlots == 0 && save.CompanionPool.Count == 0, "the evil god falls after nine chapters");
            Require(!save.CanDepart(null), "nothing left to climb");
        });
        test("a lost expedition keeps the saved state and a retry keeps its companions", () => {
            var save = new TowerCampaignSave();
            var failed = save.BeginChapter(5); Draft(failed);
            Require(failed.ClearFloor() && failed.ChooseOffer(failed.Offers[0].Id) && failed.AdvanceFloor() && failed.ClearFloor(), "climb a little");
            Require(save.ClearedChapters == 0 && save.Save() == new TowerCampaignSave().Save(), "nothing recorded on defeat");
            var retry = save.BeginChapter(6, Ids(failed));
            Require(retry.Floor == 1 && !retry.IsRewardPending && Ids(retry)[0] == Ids(failed)[0], "the same companion, no new draft");
            Climb(retry);
            Require(save.RecordChapterClear(retry) && save.ClearedChapters == 1, "the retry restores Olympus");
            save = Progress(3);
            var partial = save.BeginChapter(9, new[] { "zeus" });
            Require(partial.GetCompanion("zeus") != null && partial.IsRewardPending && partial.IsRecruitOffer, "a kept party short of the slots drafts the rest");
            foreach (var offer in partial.Offers) Require(offer.CompanionId != "zeus", "kept gods are not dealt");
            Require(Draft(partial) == 1 && partial.Companions.Count == 2, "the open slot fills");
            Require(!save.CanDepart(new[] { "zeus", "odin", "thor" }) && !save.CanDepart(new[] { "ra" }), "kept parties respect the slots and the pool");
        });
        test("saves round-trip with skill points and the tree, carry old saves forward without their growth and ignore damaged entries", () => {
            var save = Progress(3); save.PrologueSeen = true; save.TutorialDone = true;
            save.AddSkillPoints(9);
            Require(save.LearnSkill("origin") && save.LearnSkill("arc_seed") && save.LearnSkill("arc_seed"), "learn a few ranks");
            string text = save.Save(); var loaded = TowerCampaignSave.Load(text);
            Require(text == "c3;3;3;6;arc_seed=2,origin=1" && loaded.Save() == text, "round trip " + text);
            Require(loaded.ClearedChapters == 3 && loaded.PrologueSeen && loaded.TutorialDone && !loaded.EndingSeen && loaded.CompanionSlots == 2, "fields restored");
            Require(loaded.SkillPoints == 6 && loaded.EarnedPoints == 9 && loaded.SkillTree.Rank("arc_seed") == 2, "points and ranks restored");
            var everyone = TowerCampaign.CompanionPool(TowerCampaign.Chapters[2]);
            var a = save.Preview(1, everyone); var b = loaded.Preview(1, everyone);
            Require(Math.Abs(a.Player.Damage - b.Player.Damage) < .001f && Math.Abs(a.Player.SkillPowerMultiplier - b.Player.SkillPowerMultiplier) < .001f, "the tree's bonuses restored");
            foreach (string bad in new[] { null, "", "x", "c3;9", "c3;1;0;0", "c3;one;0;0;", "c3;1;0;-4;", "c3;1;0;many;", "c2;1;0;NaN;", "c4;1;0;0;", "c1;1;0;;;0" })
            {
                var broken = TowerCampaignSave.Load(bad);
                Require(broken.ClearedChapters == 0 && broken.SkillPoints == 0 && broken.SkillTree.Spent == 0 && !broken.PrologueSeen, "damaged save ignored: " + bad);
            }
            var legacy = TowerCampaignSave.Load("c1;1;3;zeus,thor,athena;thor,zeus;-5;zeus.skill1=2,unknown=3,player.fireball=0");
            Require(legacy.ClearedChapters == 1 && legacy.PrologueSeen && legacy.TutorialDone && legacy.SkillPoints == 0, "a c1 save keeps its chapters and flags");
            Require(legacy.CompanionPool.Count == 4 && legacy.Save() == "c3;1;3;0;", "old gods, parties and growth give way");
            var grown = TowerCampaignSave.Load("c2;2;1;15;player.might=3,zeus.skill1=2");
            Require(grown.ClearedChapters == 2 && grown.Save() == "c3;2;1;0;" && Levels(grown.Preview(1, new[] { "zeus" })) == 2, "a c2 save drops the growth and health it kept between chapters");
            var overflow = TowerCampaignSave.Load("c3;99;7;0;");
            Require(overflow.IsComplete && overflow.ClearedChapters == 9 && overflow.EndingSeen, "chapter count clamps");
        });
    }
}
