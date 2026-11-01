using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Explicit validation of the skill practice range through the production game loop: the title button and the
    // village training yard open it, every one of the skills lands on the standing targets (or heals and shields
    // the hero), no target falls and the hero takes no harm, a target brought to zero stands up again, the level, target
    // count and backdrop settings rebuild the yard, the automatic repeat and the input queue cast again, the latest cast's
    // total leaves out earlier zones and the sword, the skill bar blocks sword clicks, and leaving returns to the village
    // (through the prologue on a new save). The saved campaign is never written.
    // Runs in a player with -towerVfxSuite Practice -towerVfxSuitePrefix <prefix> -towerVfxSuiteReport <report>.
    public sealed class TowerPracticeSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class PracticeReport
        {
            public string mode = "towerPracticeSmoke", finishedUtc;
            public bool success, titleButton, villageEntrance, everySkillLands, targetsStand, reactions, revive, settings, autoRepeat, inputQueue, totalsIsolated, barBlocksClicks, leaveToVillage, prologueKept, settingsKept, savedDataUntouched;
            public List<string> casts = new List<string>(), checks = new List<string>(), screenshots = new List<string>(), errors = new List<string>();
        }
        private const string CampaignKey = "Tower.Campaign.v1";
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly PracticeReport report = new PracticeReport();
        private TowerGame game; private TowerSmokeDriver capture;
        private TowerCampaignSave originalCampaign;
        private string prefix, reportPath, originalSaved; private bool finishing, quitOnComplete;
        private float deadline;

        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver, string capturePrefix, string outputReportPath, bool quitOnComplete = false)
        {
            if (game != null) throw new InvalidOperationException("Create a new practice driver for each suite run.");
            if (tower == null) throw new ArgumentNullException(nameof(tower));
            if (captureDriver == null) throw new ArgumentNullException(nameof(captureDriver));
            if (!Path.IsPathRooted(capturePrefix) || !Path.IsPathRooted(outputReportPath)) throw new ArgumentException("Suite capture and report paths must be absolute.");
            game = tower; capture = captureDriver; prefix = capturePrefix; reportPath = outputReportPath; this.quitOnComplete = quitOnComplete;
            originalCampaign = game.campaign; originalSaved = PlayerPrefs.GetString(CampaignKey, "");
            capture.PrepareForSuite(tower); game.SuppressProgressPersistence = true; game.SmokeControlled = true; enabled = true;
            Application.logMessageReceived += RecordLog; deadline = Time.realtimeSinceStartup + 300;
            StartCoroutine(Guard(Suite()));
        }
        private void RecordLog(string condition, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(condition + "\n" + trace); }
        private void OnDestroy() { Application.logMessageReceived -= RecordLog; }
        private void Update()
        { if (!finishing && Time.realtimeSinceStartup > deadline) { StopAllCoroutines(); Finish(new TimeoutException("Practice suite exceeded 300 seconds.")); } }
        private object Call(string method, params object[] args)
        { return typeof(TowerGame).GetMethod(method, Hidden).Invoke(game, args); }
        private IEnumerator Guard(IEnumerator body)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(body);
            while (stack.Count > 0)
            {
                bool more = false; object value = null; Exception error = null;
                try { more = stack.Peek().MoveNext(); if (more) value = stack.Peek().Current; } catch (Exception ex) { error = ex; }
                if (error != null) { Finish(error); yield break; }
                if (!more) { stack.Pop(); continue; }
                var nested = value as IEnumerator; if (nested != null) stack.Push(nested); else yield return value;
            }
        }
        private IEnumerator Capture(string suffix)
        { yield return capture.CaptureForSuite(prefix, suffix); report.screenshots.Add(prefix + "-" + suffix + ".png"); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void Check(string text) { report.checks.Add(text + " PASS"); }
        private IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        private IEnumerator Seconds(float seconds) { float until = Time.time + seconds; while (Time.time < until) yield return null; }
        private IEnumerator Until(Func<bool> condition, float seconds, string failure)
        {
            float limit = Time.realtimeSinceStartup + seconds;
            while (!condition()) { Require(Time.realtimeSinceStartup < limit, failure); yield return null; }
        }
        private int Count { get { return game.enemies.Count; } }
        private void RequireYard(int count, string when)
        {
            Require(game.practice && !game.village && !game.Paused, "The practice range is not running " + when + ".");
            Require(Count == count && game.PracticeTargetsStanding == count, "Expected " + count + " standing targets " + when + " but found " + Count + " (" + game.PracticeTargetsStanding + " standing).");
            foreach (var e in game.enemies) Require(e.theme == game.PracticeTheme && e.monster != null, "A target is not a monster of " + game.PracticeTheme + " " + when + ".");
            Require(game.hero.hp > 0 && game.DamageTaken <= 0, "The hero was harmed " + when + ".");
        }
        // One cast through the production loop: wind-up, release and the effect's own flight, zones and hits.
        private IEnumerator Cast(string shot, float settle = 1.4f)
        {
            int before = game.PracticeCasts;
            Require(game.CastPractice(), "The practice range refused to cast " + game.PracticeEffect + ".");
            Require(game.PracticeCasts == before + 1, "The cast was not counted.");
            yield return Until(() => !game.PracticeBusy, 3, game.PracticeEffect + " never released.");
            if (shot != null) { yield return Seconds(.18f); yield return Capture(shot); }
            yield return Seconds(settle);
        }

        private IEnumerator Suite()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            game.campaign = new TowerCampaignSave { PrologueSeen = true, TutorialDone = true };
            game.EnterVillage(); game.intro = true; game.SmokeControlled = true;
            yield return Frames(4); yield return Capture("title");
            report.titleButton = true; Check("title drawn with the practice button");

            // The village training yard names the range and opens it.
            game.intro = false; yield return Frames(2);
            var hero = game.hero; hero.pos = new Vector2(TowerGame.PracticeYardX + .2f, TowerWorld.Ground); hero.velocityY = 0; hero.grounded = true; hero.support = -1; hero.lastX = hero.pos.x;
            hero.root.transform.position = hero.pos; yield return Frames(45);
            string prompt = (string)Call("SpotPrompt") ?? "";
            Require(prompt.StartsWith("스킬 연습장"), "The training yard offers '" + prompt + "' instead of the practice range.");
            yield return Capture("village-yard");
            game.InteractVillage(); yield return Frames(3);
            RequireYard(3, "after entering from the village");
            Require(!game.run.IsRewardPending && game.run.Companions.Count == 0, "The practice range opened with cards or companions.");
            report.villageEntrance = true; Check("village yard opens the range");
            for (float until = Time.realtimeSinceStartup + 10; !TowerVfx.CodeWarm && Time.realtimeSinceStartup < until;) yield return null;
            yield return Seconds(.5f); yield return Capture("yard");

            // Every skill at level one: offensive skills land, heals heal, shields shield, and nothing falls or hurts.
            for (int i = 0; i < TowerGame.PracticeEffects.Length; i++)
            {
                game.ResetPracticeYard(); game.SelectPractice(i); yield return Frames(2);
                var effect = game.PracticeEffect; float shieldBefore = game.PlayerShield;
                int before = game.PracticeCasts;
                Require(game.CastPractice(), "The practice range refused to cast " + effect + ".");
                float hpAtCast = game.hero.hp;
                yield return Until(() => !game.PracticeBusy, 3, effect + " never released.");
                yield return Seconds(.2f); yield return Capture("skill-" + effect);
                bool lasting = effect == SkillEffect.Sentry || effect == SkillEffect.PoisonCloud || effect == SkillEffect.Curse || effect == SkillEffect.Bloom || effect == SkillEffect.Vortex || effect == SkillEffect.Mine;
                yield return Seconds(lasting ? 2.2f : 1.4f);
                bool heal = effect == SkillEffect.HealPulse, shield = effect == SkillEffect.ShieldPulse;
                if (heal) Require(game.hero.hp > hpAtCast, "Heal pulse did not heal the hero (" + hpAtCast + " -> " + game.hero.hp + ").");
                else if (shield) Require(game.PlayerShield > shieldBefore, "Shield pulse gave no shield.");
                else Require(game.PracticeDamage > 0 && game.PracticeHits > 0, effect + " never hit a target.");
                if (effect == SkillEffect.LifeDrain || effect == SkillEffect.Bloom) Require(game.hero.hp > hpAtCast, effect + " did not heal the hero.");
                RequireYard(3, "after " + effect);
                report.casts.Add(effect + ": damage " + Mathf.RoundToInt(game.PracticeDamage) + ", hits " + game.PracticeHits + ", hp " + Mathf.RoundToInt(hpAtCast) + "->" + Mathf.RoundToInt(game.hero.hp) + ", shield " + Mathf.RoundToInt(game.PlayerShield));
                Require(game.PracticeCasts == before + 1, effect + " was cast more than once.");
            }
            report.everySkillLands = report.targetsStand = true; Check("all " + TowerGame.PracticeEffects.Length + " skills land, targets stand");

            // A kept status sets off its reaction: lightning on slowed targets is superconduct; without a status nothing reacts.
            game.ResetPracticeYard(); game.SetPracticeStatus(1); game.SelectPractice(Array.IndexOf(TowerGame.PracticeEffects, SkillEffect.ChainLightning)); yield return Frames(3);
            int reactions = game.ReactionCount;
            yield return Cast("status-superconduct", .8f);
            Require(game.ReactionCount > reactions && game.PracticeDamage > 0, "Chain lightning on slowed targets set off no reaction.");
            game.SetPracticeStatus(0); game.ResetPracticeYard(); yield return Frames(2);
            reactions = game.ReactionCount; yield return Cast(null, .8f);
            Require(game.ReactionCount == reactions, "A reaction fired without any status on the targets.");
            report.reactions = true; Check("kept status sets off reactions");

            // A target brought to zero bursts and stands again at full health.
            game.ResetPracticeYard(); game.SelectPractice(Array.IndexOf(TowerGame.PracticeEffects, SkillEffect.Fireball)); yield return Frames(2);
            var nearest = game.enemies[0]; nearest.hp = 1;
            yield return Cast(null, .6f);
            RequireYard(3, "after a target fell to zero");
            Require(game.kills == 0 && game.enemies.Contains(nearest) && nearest.hp > 1, "A target brought to zero did not stand up again.");
            report.revive = true; Check("targets brought to zero stand up again");

            // Level, target count and backdrop.
            game.SelectPractice(Array.IndexOf(TowerGame.PracticeEffects, SkillEffect.Meteor));
            game.SetPracticeLevel(1); var low = game.PracticeSkill(); float lowPower = low.Power, lowRadius = low.Radius;
            game.SetPracticeLevel(3); var high = game.PracticeSkill();
            Require(high.Level == 3 && high.Power > lowPower && high.Radius > lowRadius, "Level three did not strengthen the skill.");
            game.SetPracticeTargets(5); yield return Frames(3); RequireYard(5, "with five targets");
            game.SelectPractice(Array.IndexOf(TowerGame.PracticeEffects, SkillEffect.ChainLightning));
            yield return Cast("five-chain", 1.2f);
            Require(game.PracticeHits >= 3, "Chain lightning reached only " + game.PracticeHits + " of five targets.");
            int at = 0, hell = 0;
            for (int i = 0; i < TowerThemeCatalog.All.Count; i++) { if (TowerThemeCatalog.All[i].Id == game.PracticeTheme) at = i; if (TowerThemeCatalog.All[i].Id == TowerThemeId.Hell) hell = i; }
            game.ShiftPracticeTheme(hell - at);
            yield return Frames(3);
            Require(game.PracticeTheme == TowerThemeId.Hell, "The backdrop did not move to Hell but to " + game.PracticeTheme + ".");
            RequireYard(5, "in Hell");
            game.SelectPractice(Array.IndexOf(TowerGame.PracticeEffects, SkillEffect.Meteor));
            yield return Cast("hell-meteor-lv3", 1.4f);
            Require(game.PracticeDamage > 0, "The level three meteor never hit in Hell.");
            report.settings = true; Check("level, five targets and Hell backdrop");

            // The automatic repeat casts again once the yard is quiet; a dash strike starts again from its first spot.
            game.SetPracticeLevel(1); game.SetPracticeTargets(3); game.SelectPractice(Array.IndexOf(TowerGame.PracticeEffects, SkillEffect.DashStrike));
            yield return Frames(2);
            float home = game.hero.pos.x; int casts = game.PracticeCasts;
            game.PracticeAuto = true;
            yield return Until(() => game.PracticeCasts >= casts + 1, 4, "The automatic repeat did not start.");
            yield return Until(() => Mathf.Abs(game.hero.pos.x - home) > .5f, 3, "The automatic dash strike did not move the hero.");
            yield return Until(() => game.PracticeCasts >= casts + 2, 8, "The automatic repeat did not cast twice.");
            Require(Mathf.Abs(game.hero.pos.x - home) < .35f, "The repeated dash strike did not start again from its first spot.");
            game.PracticeAuto = false;
            yield return Until(() => !game.PracticeBusy, 3, "The last automatic dash never finished.");
            report.autoRepeat = true; Check("automatic repeat");

            // A request while the hero is busy waits for the cast to end, then fires.
            game.ResetPracticeYard(); game.SelectPractice(Array.IndexOf(TowerGame.PracticeEffects, SkillEffect.IceNova)); yield return Frames(2);
            casts = game.PracticeCasts;
            game.QueuePractice(); Require(game.PracticeCasts == casts + 1 && game.PracticeBusy, "The first request did not cast at once.");
            game.QueuePractice(); Require(game.PracticeCasts == casts + 1, "A request during the wind-up cast at once.");
            yield return Until(() => game.PracticeCasts == casts + 2, 3, "The queued request never cast.");
            yield return Until(() => !game.PracticeBusy, 3, "The queued cast never finished.");
            report.inputQueue = true; Check("queued request");

            // A lone target stands inside level one's smallest radius: Blade Storm reaches it.
            game.SetPracticeTargets(1); game.SetPracticeLevel(1); game.SelectPractice(Array.IndexOf(TowerGame.PracticeEffects, SkillEffect.BladeStorm)); yield return Frames(2);
            RequireYard(1, "with one target");
            yield return Cast(null, 1.4f);
            Require(game.PracticeHits > 0, "Level one Blade Storm never reached the lone target.");
            game.SetPracticeTargets(3); yield return Frames(2);

            // The total counts only the latest cast: an earlier sentry still firing and the sword stay out of it.
            game.SelectPractice(Array.IndexOf(TowerGame.PracticeEffects, SkillEffect.Sentry));
            yield return Cast(null, .3f);
            game.SelectPractice(Array.IndexOf(TowerGame.PracticeEffects, SkillEffect.IceNova));
            yield return Cast(null, .1f);
            int novaHits = game.PracticeHits; float novaDamage = game.PracticeDamage;
            Require(novaHits >= 1, "Ice Nova never hit the nearest target.");
            game.AttackOverride = true; yield return Seconds(.9f); game.AttackOverride = false;
            yield return Seconds(1.6f);
            Require(game.PracticeHits == novaHits && Mathf.Approximately(game.PracticeDamage, novaDamage), "Sentry or sword hits leaked into the latest cast's total (" + novaHits + " -> " + game.PracticeHits + " hits).");
            report.totalsIsolated = true; Check("latest cast total ignores earlier sentry and sword");
            game.ResetPracticeYard(); yield return Frames(2);

            // The skill bar and settings take clicks; the open yard leaves them to the sword.
            Require((bool)Call("HUDHit", new Vector2(120, 836)) && (bool)Call("HUDHit", new Vector2(150, 300)) && !(bool)Call("HUDHit", new Vector2(800, 500)), "The practice HUD click areas are wrong.");
            report.barBlocksClicks = true; Check("HUD click areas");

            game.menu = true; yield return Frames(3); yield return Capture("menu");
            game.LeavePractice(); yield return Frames(3);
            Require(game.village && !game.practice && Count == 0 && !game.Paused && !game.menu && !game.StoryOpen, "Leaving did not return to the village.");
            report.leaveToVillage = true; Check("leave to village");

            // A new save that opened the range from the title still meets its prologue on the way out.
            var seen = game.campaign; game.campaign = new TowerCampaignSave { TutorialDone = true };
            game.intro = true; yield return Frames(2); game.EnterPractice(); yield return Frames(3);
            game.LeavePractice(); yield return Frames(3);
            Require(game.village && game.StoryOpen, "A new save left the range without its prologue.");
            yield return Capture("prologue-after-practice");
            game.SkipStory(); yield return Frames(2);
            Require(game.campaign.PrologueSeen && !game.StoryOpen, "The prologue was not marked as seen.");
            game.campaign = seen; report.prologueKept = true; Check("new save meets the prologue after the range");

            // The title opens it again with the last settings.
            game.intro = true; yield return Frames(2);
            game.EnterPractice(); yield return Frames(3);
            Require(game.PracticeTheme == TowerThemeId.Hell && game.PracticeTargetCount == 3 && game.PracticeIndex == Array.IndexOf(TowerGame.PracticeEffects, SkillEffect.IceNova), "The range forgot its settings.");
            RequireYard(3, "after entering from the title");
            game.SelectPractice(Array.IndexOf(TowerGame.PracticeEffects, SkillEffect.BladeStorm));
            yield return Cast("hell-bladestorm", .8f);
            report.settingsKept = true; Check("title opens the range with its settings");
            game.EnterVillage(); yield return Frames(2);
            Finish(null);
        }

        private void Finish(Exception error)
        {
            if (finishing) return; finishing = true;
            if (error != null) report.errors.Add(error is TargetInvocationException && error.InnerException != null ? error.InnerException.ToString() : error.ToString());
            report.savedDataUntouched = PlayerPrefs.GetString(CampaignKey, "") == originalSaved;
            if (game) { game.campaign = originalCampaign; game.PracticeAuto = false; game.menu = true; }
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.success = error == null && report.errors.Count == 0 && report.savedDataUntouched;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= RecordLog;
            if (quitOnComplete) Application.Quit(report.success ? 0 : 1);
        }
    }
}
