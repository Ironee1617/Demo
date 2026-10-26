using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Opt-in story campaign fixture (-towerCampaignSmoke): walks the title, prologue, village tutorial, the
    // tower gate, the companion drafts of the first and fourth expeditions, hell, the abyss and the evil god,
    // and captures each screen.
    // It never writes the player's saved progress.
    public sealed class TowerCampaignSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class CampaignReport
        {
            public string mode = "towerCampaignSmoke", finishedUtc;
            public bool success, villageTutorial, gateParty, companionDraft, firstChapter, hellChapter, abyssChapter, finalBoss, chapterRecorded, expeditionLost;
            public List<string> samples = new List<string>(), screenshots = new List<string>(), errors = new List<string>();
        }
        private readonly CampaignReport report = new CampaignReport();
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private TowerGame game; private TowerSmokeDriver capture;
        private string prefix, reportPath; private float deadline; private bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Enter()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-towerCampaignSmoke") < 0) return;
            var driver = new GameObject("Tower campaign validation").AddComponent<TowerCampaignSmokeDriver>();
            driver.prefix = Arg(args, "-towerCampaignCapture") ?? Path.Combine(Application.persistentDataPath, "tower-campaign");
            driver.reportPath = Arg(args, "-towerCampaignReport") ?? driver.prefix + "-report.json";
            driver.deadline = Time.realtimeSinceStartup + 300;
            Application.logMessageReceived += driver.RecordLog;
        }
        private static string Arg(string[] args, string key) { int at = Array.IndexOf(args, key); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private void RecordLog(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message + "\n" + trace); }
        private void OnDestroy() { Application.logMessageReceived -= RecordLog; }
        private void Update() { if (!finished && Time.realtimeSinceStartup > deadline) { StopAllCoroutines(); Finish(new TimeoutException("Campaign suite exceeded 300 seconds.")); } }
        private object Call(string name, params object[] values) { return typeof(TowerGame).GetMethod(name, Hidden).Invoke(game, values); }
        private IEnumerator Start()
        {
            var body = Suite(); Exception error = null;
            while (true)
            {
                bool more;
                try { more = body.MoveNext(); } catch (Exception ex) { error = ex; break; }
                if (!more) break;
                var nested = body.Current as IEnumerator;
                if (nested == null) { yield return body.Current; continue; }
                while (true)
                {
                    bool inner;
                    try { inner = nested.MoveNext(); } catch (Exception ex) { error = ex; break; }
                    if (!inner) break;
                    yield return nested.Current;
                }
                if (error != null) break;
            }
            Finish(error);
        }
        private IEnumerator Shot(string name) { report.screenshots.Add(prefix + "-" + name + ".png"); return capture.CaptureForSuite(prefix, name); }
        private IEnumerator Frames(int count) { for (int i = 0; i < count; i++) { if (game.hero != null) { game.invulnerable = 99; game.hero.hp = Mathf.Max(game.hero.hp, 1); } yield return null; } }
        private void Place(float x)
        {
            game.hero.pos = new Vector2(x, TowerWorld.Ground); game.hero.velocityY = 0; game.hero.grounded = true; game.hero.support = -1; game.hero.lastX = x;
            game.hero.root.transform.position = game.hero.pos;
            game.view.transform.position = new Vector3(Mathf.Clamp(x + 1.6f, -7.5f, 21.5f), 0, -10);
        }
        private void Sample(string text) { report.samples.Add(text); }
        // Plays chapters through the rules so later chapters can be captured without minutes of combat.
        private static TowerCampaignSave Progress(int chapters)
        {
            var save = new TowerCampaignSave { PrologueSeen = true, TutorialDone = true };
            for (int c = 0; c < chapters; c++)
            {
                var run = save.BeginChapter(500 + c);
                while (run.IsRecruitOffer) run.ChooseOffer(run.Offers[0].Id);
                while (true)
                {
                    run.ClearFloor(); if (run.IsRewardPending) run.ChooseOffer(run.Offers[0].Id);
                    if (run.IsComplete) break; run.AdvanceFloor();
                }
                save.RecordChapterClear(run);
            }
            return save;
        }
        // Clears the current floor (taking any reward) and walks to the next one.
        private void Advance()
        {
            foreach (var e in game.enemies) if (e.root) Destroy(e.root);
            game.enemies.Clear();
            if (!game.run.IsFloorCleared) game.run.ClearFloor();
            if (game.run.IsRewardPending) game.run.ChooseOffer(game.run.Offers[0].Id);
            game.NextFloor(FloorType.Normal);
        }
        private TowerGame.Fighter Boss() { return game.enemies.Find(e => e.isBoss); }
        // Picks the first card of every companion card set the expedition opens with.
        private int Draft(string preferred = null)
        {
            int sets = 0;
            while (game.run.IsRewardPending && game.run.IsRecruitOffer)
            {
                var offers = game.run.Offers; long pick = offers[0].Id;
                foreach (var offer in offers) if (offer.CompanionId == preferred) pick = offer.Id;
                Require(offers.Count == Math.Min(TowerRun.OfferSlots, game.run.OpenRecruits), "A draft shows up to three of the remaining gods.");
                var pool = new List<string>(TowerCampaign.CompanionPool(game.run.Chapter));
                foreach (var offer in offers) Require(pool.Contains(offer.CompanionId), "Draft card " + offer.CompanionId + " is outside the chapter's pool.");
                game.SelectOffer(pick); sets++;
            }
            return sets;
        }

        private IEnumerator Suite()
        {
            for (int frame = 0; frame < 240 && game == null; frame++) { yield return null; game = FindFirstObjectByType<TowerGame>(); }
            Require(game != null, "The roguelike game did not load.");
            capture = game.GetComponent<TowerSmokeDriver>(); Require(capture != null, "Capture driver missing.");
            // Like the other suites, the shared driver only captures for this one.
            foreach (var field in new[] { "game", "active", "captureOnly" })
                typeof(TowerSmokeDriver).GetField(field, Hidden).SetValue(capture, field == "game" ? (object)game : true);
            game.SuppressProgressPersistence = true; game.campaign = new TowerCampaignSave();
            Directory.CreateDirectory(Path.GetDirectoryName(prefix));

            game.EnterVillage(); game.intro = true;
            yield return Frames(10); yield return Shot("title");
            game.StartClimb();
            Require(game.village && game.StoryOpen && !game.intro, "Starting the campaign opens the prologue in the village.");
            yield return Frames(4); yield return Shot("prologue");
            for (int i = 0; i < TowerCampaign.Prologue.Count; i++) game.AdvanceStory();
            Require(!game.StoryOpen && game.campaign.PrologueSeen, "The prologue closes after its last page.");

            game.SmokeControlled = true;
            Place(TowerGame.VillageSpawnX); yield return Frames(20); yield return Shot("village-start");
            Place(TowerGame.DummyXs[0] - 1.6f); game.hero.facing = 1; game.AttackOverride = true;
            yield return Frames(24); game.AttackOverride = false; yield return Shot("village-dummies");
            Place(-6.6f); game.hero.pos.y = -1.8f; game.hero.support = 0; game.hero.root.transform.position = game.hero.pos;
            yield return Frames(10); yield return Shot("village-ledges");
            Place(TowerGame.ShrineX - .8f); yield return Frames(12); yield return Shot("village-shrine");
            game.villagePanel = TowerGame.VillagePanel.Shrine; yield return Frames(4); yield return Shot("shrine-panel");
            game.villagePanel = TowerGame.VillagePanel.None;
            Place(19.5f); yield return Frames(12);
            Require(game.campaign.TutorialDone, "Reaching the plaza completes the tutorial.");
            yield return Shot("village-gate");
            report.villageTutorial = true;

            game.OpenGate();
            Require(game.villagePanel == TowerGame.VillagePanel.Gate && game.campaign.CompanionSlots == 1 && game.campaign.CompanionPool.Count == 2, "The first gate drafts one of the two Olympians.");
            yield return Frames(4); yield return Shot("gate-first");
            report.gateParty = true;
            Require(game.StartExpedition(), "The first expedition departs.");
            Require(!game.village && game.run.IsCampaign && game.run.Floor == 1 && game.run.Theme.Id == TowerThemeId.Olympus && game.run.Companions.Count == 0, "Chapter one starts on Olympus.");
            Require(game.run.IsRewardPending && game.run.IsRecruitOffer && game.run.Offers.Count == 2 && !game.run.CanReroll(0), "Chapter one opens with both Olympians and nothing to reroll.");
            yield return Frames(4); yield return Shot("chapter1-intro");
            game.SkipStory(); yield return Frames(4); yield return Shot("chapter1-draft");
            Require(Draft("poseidon") == 1 && game.run.GetCompanion("poseidon") != null && game.run.Companions.Count == 1 && !game.run.IsRewardPending, "Poseidon joins from the single draft.");
            yield return Frames(40); yield return Shot("chapter1-floor1");
            report.firstChapter = true;

            // Past floor 30: Olympus, Asgard, Dangun and Eden's gods, drafted twice.
            game.campaign = Progress(3); game.EnterVillage(); game.SmokeControlled = true;
            game.OpenGate();
            Require(game.campaign.CompanionSlots == 2 && game.campaign.CompanionPool.Count == 8, "Chapter four drafts two of eight gods.");
            yield return Frames(4); yield return Shot("gate-two-slots");
            Require(game.StartExpedition(), "The fourth expedition departs."); game.SkipStory();
            Require(game.run.Floor == 31 && game.run.Offers.Count == 3 && game.run.CanReroll(0), "Three of eight gods are dealt.");
            yield return Frames(4); yield return Shot("eden-draft-1");
            game.SelectOffer(game.run.Offers[0].Id);
            Require(game.run.IsRecruitOffer && game.run.Companions.Count == 1 && game.run.OpenRecruits == 7, "The second companion is drafted straight after the first.");
            yield return Frames(4); yield return Shot("eden-draft-2");
            Require(Draft() == 1 && game.run.Companions.Count == 2, "Two companions climb past floor 30.");
            report.companionDraft = true;

            // Hell: seven myths restored, Solomon's demons wait at the gate.
            game.campaign = Progress(7); game.EnterVillage(); game.SmokeControlled = true; Place(18.5f);
            yield return Frames(14); yield return Shot("village-late");
            game.OpenGate(); yield return Frames(4); yield return Shot("gate-hell");
            Require(game.StartExpedition(), "The hell expedition departs.");
            game.SkipStory();
            Require(Draft() == 3, "Past floor 60 three companions are drafted.");
            Require(game.run.Floor == 71 && game.run.Theme.Id == TowerThemeId.Hell && game.run.Companions.Count == 3, "Hell spans floor 71 with a full party.");
            yield return Frames(60); yield return Shot("hell-71");
            foreach (var e in game.enemies) Sample("hell-71 " + e.monster.Name + " / " + e.art.ProfileName);
            Require(game.enemies.Exists(e => e.art.ProfileName.StartsWith("Infernal")), "Hell creatures are regraded.");
            Place(-12); game.hero.hp = 0;
            for (int i = 0; i < 30; i++) yield return null;
            yield return Shot("expedition-lost");
            Require(game.run.IsCampaign && game.hero.hp <= 0 && game.campaign.ClearedChapters == 7, "A defeat records nothing.");
            report.expeditionLost = true;
            var fallen = new List<string>(); foreach (var c in game.run.Companions) fallen.Add(c.Id);
            game.RetryExpedition(); game.SkipStory(); game.SmokeControlled = true;
            Require(!game.run.IsRewardPending && game.run.Companions.Count == 3 && fallen.TrueForAll(id => game.run.GetCompanion(id) != null), "A retry keeps the same companions without a new draft.");
            for (int i = 0; i < 4; i++) Advance();
            Require(game.run.Floor == 75, "Hell's mid-chapter boss floor.");
            yield return Frames(150); yield return Shot("hell-75-boss");
            Sample("hell-75 boss " + Boss().monster.Name + " / " + Boss().art.ProfileName + " / " + game.BossPatternName(Boss()));
            for (int i = 0; i < 5; i++) Advance();
            Require(game.run.Floor == 80 && Boss() != null && Boss().title != null, "Hell's last floor holds the awakened boss.");
            yield return Frames(80); yield return Shot("hell-80-awakened");
            Sample("hell-80 boss " + Boss().title);
            report.hellChapter = true;

            // The abyss and the evil god.
            game.campaign = Progress(8); game.EnterVillage(); game.SmokeControlled = true;
            game.OpenGate(); yield return Frames(4); yield return Shot("gate-abyss");
            Require(game.campaign.CompanionSlots == 3 && game.campaign.CompanionPool.Count == 16, "The final ascent drafts three of all sixteen restored gods.");
            Require(game.StartExpedition(), "The abyss expedition departs."); game.SkipStory();
            Require(Draft() == 3 && game.run.Companions.Count == 3, "Three restored gods join the final ascent.");
            Require(game.run.Floor == 81 && game.run.Theme.Id == TowerThemeId.Abyss, "The abyss starts on floor 81.");
            yield return Frames(60); yield return Shot("abyss-81");
            foreach (var e in game.enemies) Sample("abyss-81 " + e.monster.Name + " / " + e.art.ProfileName);
            Require(game.enemies.Exists(e => e.art.ProfileName.StartsWith("Flayed")), "Abyss creatures are regraded.");
            report.abyssChapter = true;
            for (int i = 0; i < 4; i++) Advance();
            yield return Frames(120); yield return Shot("abyss-85-herald");
            Sample("abyss-85 boss " + Boss().monster.Name + " / " + Boss().art.ProfileName);
            for (int i = 0; i < 5; i++) Advance();
            var god = Boss();
            Require(game.run.Floor == 90 && god != null && TowerGame.IsFinalBoss(god) && god.art.ProfileName == "Nihilos", "The evil god waits on floor 90.");
            Place(8); yield return Frames(30); yield return Shot("abyss-90-nihilos");
            yield return Frames(200); yield return Shot("abyss-90-technique");
            Sample("abyss-90 " + god.monster.Name + " hp " + god.maxHp + " pattern " + game.BossPatternName(god) + " scale " + god.root.transform.localScale.x);
            god.hp = god.maxHp * .45f; yield return Frames(260); yield return Shot("abyss-90-enraged");
            Require(game.BossEnraged(god), "The evil god enrages below half health.");
            report.finalBoss = true;

            foreach (var e in game.enemies) if (e.root) Destroy(e.root);
            game.enemies.Clear(); yield return Frames(6);
            if (game.run.IsRewardPending) game.SelectOffer(game.run.Offers[0].Id);
            yield return Frames(6);
            Require(game.run.IsComplete && game.campaign.IsComplete, "Clearing floor 90 completes the campaign.");
            yield return Shot("campaign-complete");
            report.chapterRecorded = true;
            game.ReturnFromChapter(); Require(game.StoryOpen, "The epilogue follows the evil god's fall.");
            yield return Frames(4); yield return Shot("epilogue");
            for (int i = 0; i < TowerCampaign.Epilogue.Count; i++) game.AdvanceStory();
            Require(game.village && game.campaign.EndingSeen, "The epilogue returns to the village.");
            game.SmokeControlled = true; Place(16); yield return Frames(20); yield return Shot("village-complete");
        }
        private void Finish(Exception error)
        {
            if (finished) return; finished = true;
            if (error != null) report.errors.Add(error is TargetInvocationException && error.InnerException != null ? error.InnerException.ToString() : error.ToString());
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.success = error == null && report.errors.Count == 0;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Application.Quit(report.success ? 0 : 1);
        }
    }
}
