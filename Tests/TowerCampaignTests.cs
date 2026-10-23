using System;
using System.Collections.Generic;
using PrototypeLab.Roguelike;

// Story campaign rules: chapters, village companion choices, expedition growth and saves.
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
    private static List<string> Party(TowerCampaignSave save, string chosen)
    {
        var party = new List<string>();
        if (chosen != null) party.Add(chosen);
        for (int i = save.Roster.Count - 1; i >= 0 && party.Count < TowerRun.MaxCompanions; i--) party.Add(save.Roster[i]);
        return party;
    }
    private static int Levels(TowerRun run)
    {
        int total = run.Player.Level;
        foreach (var companion in run.Companions) total += companion.Level;
        return total;
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
                if (chapter.IsFinal) { Require(chapter.Candidates.Count == 0 && chapter.MythId == null && chapter.Theme == TowerThemeId.Abyss, "the final ascent brings restored gods only"); continue; }
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
        test("a new campaign opens only the first chapter and checks every expedition party", () => {
            var save = new TowerCampaignSave(); var first = TowerCampaign.Chapters[0];
            Require(save.ClearedChapters == 0 && Object.ReferenceEquals(save.NextChapter, first) && save.UnlockedFloor == 10 && save.Roster.Count == 0 && !save.IsComplete, "fresh state");
            Require(save.CanDepart(first, "zeus", new[] { "zeus" }) && save.CanDepart(first, "poseidon", new[] { "poseidon" }), "either Olympian may join");
            Require(!save.CanDepart(first, null, new string[0]), "a chapter god must be chosen");
            Require(!save.CanDepart(first, "athena", new[] { "athena" }), "only the two offered gods may be chosen");
            Require(!save.CanDepart(first, "zeus", new[] { "zeus", "poseidon" }), "only one of the two gods may join");
            Require(!save.CanDepart(first, "zeus", new string[0]), "the chosen god must come along");
            Require(!save.CanDepart(TowerCampaign.Chapters[1], "odin", new[] { "odin" }), "later chapters stay locked");
            bool rejected = false; try { save.BeginChapter(1, "athena", new[] { "athena" }); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "invalid parties cannot depart");
        });
        test("each restored myth returns the hero to the village, opens ten more floors and keeps the party's growth", () => {
            var save = new TowerCampaignSave();
            for (int c = 0; c < TowerCampaign.Chapters.Count; c++)
            {
                var chapter = save.NextChapter; Require(chapter.Index == c && save.UnlockedFloor == chapter.LastFloor, "unlocked through chapter " + c);
                string chosen = chapter.Candidates.Count > 0 ? chapter.Candidates[c % 2] : null;
                var party = Party(save, chosen);
                Require(party.Count == Math.Min(TowerRun.MaxCompanions, c + (chosen == null ? 0 : 1)), "party size before chapter " + c);
                var run = save.BeginChapter(1000 + c, chosen, party);
                Require(run.IsCampaign && Object.ReferenceEquals(run.Chapter, chapter) && run.Floor == chapter.FirstFloor && run.MaxFloor == chapter.LastFloor, "expedition floors " + c);
                Require(run.Theme.Id == chapter.Theme && !run.IsRewardPending && run.Companions.Count == party.Count, "expedition opens on the chapter theme with its party");
                Require(!save.RecordChapterClear(run, chosen), "an unfinished expedition is not recorded");
                int replayed = Levels(run);
                Climb(run);
                Require(run.IsComplete && run.Floor == chapter.LastFloor && run.ClearedFloors == 10, "chapter climbed " + c);
                int grown = Levels(run);
                Require(grown >= replayed + 2, "two fifth-floor rewards per chapter");
                Require(save.RecordChapterClear(run, chosen) && save.ClearedChapters == c + 1, "chapter recorded " + c);
                Require(!save.RecordChapterClear(run, chosen), "a chapter is recorded once");
                if (chosen != null) Require(new List<string>(save.Roster).Contains(chosen) && save.Roster.Count == c + 1, "the chosen god joins for good");
                Require(save.IsRestored(chapter), "chapter restored");
                if (!save.IsComplete)
                {
                    // Coming home and leaving again rebuilds the same heroes from the save.
                    var preview = save.Preview(7, run.Chapter.Candidates.Count > 0 ? new List<string>(party) : party);
                    Require(Levels(preview) == grown, "village preview keeps every upgrade (" + Levels(preview) + " vs " + grown + ")");
                }
            }
            Require(save.IsComplete && save.NextChapter == null && save.UnlockedFloor == 90 && save.Roster.Count == 8, "the evil god falls after nine chapters");
        });
        test("a lost expedition keeps the saved state and allows the other god to be chosen", () => {
            var save = new TowerCampaignSave();
            var failed = save.BeginChapter(5, "zeus", new[] { "zeus" });
            Require(failed.ClearFloor() && failed.AdvanceFloor() && failed.ClearFloor(), "climb a little");
            Require(save.ClearedChapters == 0 && save.Roster.Count == 0, "nothing recorded on defeat");
            var retry = save.BeginChapter(6, "poseidon", new[] { "poseidon" });
            Require(retry.Floor == 1 && retry.GetCompanion("poseidon") != null && retry.GetCompanion("zeus") == null, "retry with the other Olympian");
            Climb(retry);
            Require(!save.RecordChapterClear(retry, "zeus"), "only the god who came can be recorded");
            Require(save.RecordChapterClear(retry, "poseidon") && save.Roster.Count == 1 && save.Roster[0] == "poseidon", "poseidon joins");
            Require(!save.CanDepart(save.NextChapter, "odin", new[] { "odin", "zeus" }), "the unchosen Olympian never joins");
            Require(save.CanDepart(save.NextChapter, "thor", new[] { "thor", "poseidon" }), "restored gods may follow");
        });
        test("saves round-trip and ignore damaged or impossible entries", () => {
            var save = new TowerCampaignSave { PrologueSeen = true, TutorialDone = true };
            for (int c = 0; c < 3; c++)
            {
                var chapter = save.NextChapter; string chosen = chapter.Candidates[1];
                var run = save.BeginChapter(40 + c, chosen, Party(save, chosen)); Climb(run);
                Require(save.RecordChapterClear(run, chosen), "record " + c);
            }
            string text = save.Save(); var loaded = TowerCampaignSave.Load(text);
            Require(loaded.Save() == text, "round trip " + text);
            Require(loaded.ClearedChapters == 3 && loaded.PrologueSeen && loaded.TutorialDone && !loaded.EndingSeen && loaded.Roster.Count == 3 && loaded.LastParty.Count == 3, "fields restored");
            var a = save.Preview(1, save.LastParty); var b = loaded.Preview(1, loaded.LastParty);
            Require(Levels(a) == Levels(b) && Math.Abs(a.Player.MaxHealth - b.Player.MaxHealth) < .001f && Math.Abs(a.Player.Damage - b.Player.Damage) < .001f, "growth restored");
            foreach (string bad in new[] { null, "", "x", "c1;9", "c2;1;0;zeus;;0;", "c1;one;0;;;0;", "c1;1;0;;;NaN;" })
            {
                var broken = TowerCampaignSave.Load(bad);
                Require(broken.ClearedChapters == 0 && broken.Roster.Count == 0 && broken.Vitality == 0, "damaged save ignored: " + bad);
            }
            var forged = TowerCampaignSave.Load("c1;1;0;zeus,thor,athena;thor,zeus;-5;zeus.skill1=2,unknown=3,player.fireball=0");
            Require(forged.ClearedChapters == 1 && forged.Roster.Count == 1 && forged.Roster[0] == "zeus", "only restored chapter gods are kept");
            Require(forged.LastParty.Count == 1 && forged.LastParty[0] == "zeus" && forged.Vitality == 0, "party limited to the roster, vitality clamped");
            Require(forged.GrowthLevel("zeus.skill1") == 2 && forged.GrowthLevel("unknown") == 0 && forged.GrowthLevel("player.fireball") == 0, "unknown and empty upgrades dropped");
            var overflow = TowerCampaignSave.Load("c1;99;7;;;0;");
            Require(overflow.IsComplete && overflow.ClearedChapters == 9 && overflow.EndingSeen, "chapter count clamps");
        });
    }
}
