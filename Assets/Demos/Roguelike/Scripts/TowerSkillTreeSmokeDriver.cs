using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Explicit validation of the skill tree through the production game loop: floors cleared by Update bank skill
    // points (two on boss floors), a restored myth sends the hero home with every augment reset, the village's
    // runestone opens the tree, learned ranks show in the village and the next expedition, foresight deals its stat
    // cards before the first fight, and a lost expedition keeps its points. The saved campaign is never written.
    public sealed class TowerSkillTreeSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class SkillTreeReport
        {
            public string mode = "towerSkillTreeSmoke", finishedUtc;
            public bool success, runestonePrompt, floorPoints, bossFloorPoints, augmentsResetOnReturn, treeScreen, treeScrolls, learnedInVillage, treeInExpedition, foresightCards, pointsSurviveDefeat, savedDataUntouched;
            public int pointsAfterChapter, levelBeforeReturn, levelAfterReturn, spent;
            public float villageDamageBefore, villageDamageAfter;
            public List<string> checks = new List<string>(), screenshots = new List<string>(), errors = new List<string>();
        }
        private const string CampaignKey = "Tower.Campaign.v1";
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly SkillTreeReport report = new SkillTreeReport();
        private TowerGame game; private TowerSmokeDriver capture;
        private TowerCampaignSave originalCampaign;
        private string prefix, reportPath, originalSaved; private bool finishing, quitOnComplete;
        private float deadline;

        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver, string capturePrefix, string outputReportPath, bool quitOnComplete = false)
        {
            if (game != null) throw new InvalidOperationException("Create a new skill tree driver for each suite run.");
            if (tower == null) throw new ArgumentNullException(nameof(tower));
            if (captureDriver == null) throw new ArgumentNullException(nameof(captureDriver));
            if (!Path.IsPathRooted(capturePrefix) || !Path.IsPathRooted(outputReportPath)) throw new ArgumentException("Suite capture and report paths must be absolute.");
            game = tower; capture = captureDriver; prefix = capturePrefix; reportPath = outputReportPath; this.quitOnComplete = quitOnComplete;
            originalCampaign = game.campaign; originalSaved = PlayerPrefs.GetString(CampaignKey, "");
            capture.PrepareForSuite(tower); game.SuppressProgressPersistence = true; game.SmokeControlled = true; enabled = true;
            Application.logMessageReceived += RecordLog; deadline = Time.realtimeSinceStartup + 240;
            StartCoroutine(Guard(Suite()));
        }
        private void RecordLog(string condition, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(condition + "\n" + trace); }
        private void OnDestroy() { Application.logMessageReceived -= RecordLog; }
        private void Update()
        { if (!finishing && Time.realtimeSinceStartup > deadline) { StopAllCoroutines(); Finish(new TimeoutException("Skill tree suite exceeded 240 seconds.")); } }
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
        private IEnumerator Frames(int count)
        { for (int i = 0; i < count; i++) { if (game.hero != null && game.hero.hp > 0) game.invulnerable = 99; yield return null; } }
        private IEnumerator Until(Func<bool> condition, float seconds, string failure)
        {
            float limit = Time.realtimeSinceStartup + seconds;
            while (!condition()) { Require(Time.realtimeSinceStartup < limit, failure); if (game.hero != null && game.hero.hp > 0) game.invulnerable = 99; yield return null; }
        }
        private void Place(float x)
        {
            var hero = game.hero; hero.pos = new Vector2(x, TowerWorld.Ground); hero.velocityY = 0; hero.grounded = true; hero.support = -1; hero.lastX = x;
            hero.root.transform.position = hero.pos; game.view.transform.position = new Vector3(Mathf.Clamp(x + 1.6f, -7.5f, 21.5f), 0, -10);
        }
        private void Pick() { while (game.run.IsRewardPending && game.run.IsRecruitOffer) game.SelectOffer(game.run.Offers[0].Id); }
        private void Check(string text) { report.checks.Add(text + " PASS"); }
        // Removes the floor's monsters and lets the production Update clear the floor and bank its points.
        private IEnumerator ClearByUpdate()
        {
            foreach (var enemy in game.enemies) if (enemy.root) { enemy.root.SetActive(false); Destroy(enemy.root); }
            game.enemies.Clear(); ((IList)typeof(TowerGame).GetField("pendingSpawns", Hidden).GetValue(game)).Clear();
            yield return Until(() => game.run.IsFloorCleared, 5, "Floor " + game.run.Floor + " did not clear through Update.");
        }
        private void TakeReward() { if (game.run.IsRewardPending) game.SelectOffer(game.run.Offers[0].Id); }

        private IEnumerator Suite()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            game.campaign = new TowerCampaignSave { PrologueSeen = true, TutorialDone = true };
            game.EnterVillage(); game.intro = false; game.SmokeControlled = true;
            yield return Frames(4);
            Place(TowerGame.RunestoneX + .3f); yield return Frames(12);
            report.runestonePrompt = ((string)Call("SpotPrompt") ?? "").StartsWith("스킬트리");
            Require(report.runestonePrompt, "The runestone does not offer the skill tree.");
            yield return Capture("village-runestone"); Check("runestone prompt");

            // Chapter one through the real floor-clear path: one point per floor, two on the boss floors.
            Require(game.StartExpedition(), "The first expedition did not depart."); game.SkipStory(); Pick();
            int earned = 0;
            for (int floor = 1; floor <= 10; floor++)
            {
                Require(game.run.Floor == floor, "Expected floor " + floor + " but stood on " + game.run.Floor);
                int before = game.campaign.SkillPoints;
                yield return ClearByUpdate();
                int paid = game.campaign.SkillPoints - before, expected = floor % 5 == 0 ? 2 : 1; earned += paid;
                Require(paid == expected && game.ExpeditionPoints == earned, "Floor " + floor + " paid " + paid + " points instead of " + expected);
                TakeReward();
                if (floor == 1) { report.floorPoints = true; Check("floor 1 paid 1 point"); yield return Frames(2); yield return Capture("floor-points"); }
                if (floor == 5) { report.bossFloorPoints = true; Check("boss floor 5 paid 2 points"); }
                if (floor < 10) { game.NextFloor(FloorType.Normal); yield return Frames(2); }
            }
            yield return Until(() => game.run.IsComplete && game.campaign.ClearedChapters == 1, 5, "Chapter one was not recorded.");
            report.pointsAfterChapter = game.campaign.SkillPoints;
            report.levelBeforeReturn = game.run.Player.Level; foreach (var companion in game.run.Companions) report.levelBeforeReturn += companion.Level;
            Require(report.pointsAfterChapter == 12 && report.levelBeforeReturn >= 10, "Chapter one ended with " + report.pointsAfterChapter + " points and party levels " + report.levelBeforeReturn);
            yield return Frames(3); yield return Capture("chapter-complete");

            // Home again: every augment is gone, the points stay.
            game.ReturnFromChapter(); game.SmokeControlled = true; yield return Frames(4);
            report.levelAfterReturn = game.run.Player.Level;
            report.augmentsResetOnReturn = game.village && report.levelAfterReturn == 1 && game.run.Player.Traits.Count == 0 && game.run.Player.Skills.Count == 0 && game.campaign.SkillPoints == 12;
            Require(report.augmentsResetOnReturn, "Returning home kept augments (level " + report.levelAfterReturn + ") or lost points (" + game.campaign.SkillPoints + ")");
            Check("augments reset on return, 12 points kept");

            // The tree screen: learn down the arcane spine into the might branch with the chapter's points.
            report.villageDamageBefore = game.run.Player.Damage;
            game.OpenSkillTree(); yield return Frames(3);
            Require(game.villagePanel == TowerGame.VillagePanel.SkillTree, "The skill tree screen did not open.");
            yield return Capture("tree-fresh");
            foreach (string id in new[] { "origin", "arc_seed", "arc_core", "arc_rune", "arc_gate", "cross_oath", "ft_coin", "ft_feast", "cross_fate" })
                Require(game.LearnTreeNode(TowerSkillTree.Get(id)), "Could not learn " + id);
            report.villageDamageAfter = game.run.Player.Damage;
            report.learnedInVillage = report.villageDamageAfter > report.villageDamageBefore && game.campaign.SkillPoints == 0 && game.campaign.SkillTree.Spent == 12;
            Require(report.learnedInVillage, "Learning did not raise the village hero's damage.");
            game.treeSelection = "mt_blade"; yield return Frames(3); yield return Capture("tree-learned"); Check("learned ranks raise the village hero");
            // A well-travelled tree for the screenshot, foresight included.
            game.campaign.AddSkillPoints(160);
            foreach (string id in new[] { "ft_bounty", "ft_foresight", "ft_harvest", "ft_blood", "sw_step", "sw_tempo", "sw_reach", "sw_wind", "sw_hunt", "sw_blink",
                "bd_call", "bd_shield", "bd_pace", "bd_hymn", "bd_oracle", "bd_ward", "bd_mercy", "gd_skin", "gd_heart", "gd_breath", "gd_wall", "gd_vessel", "gd_bulwark",
                "mt_blade", "mt_fury", "mt_eye", "mt_thirst", "mt_power", "mt_cleave", "arc_calm", "arc_echo", "arc_flow", "arc_wide", "arc_amp", "arc_poise" })
                Require(game.LearnTreeNode(TowerSkillTree.Get(id)), "Could not learn " + id);
            for (int i = 0; i < 2; i++) { game.LearnTreeNode(TowerSkillTree.Get("mt_blade")); game.LearnTreeNode(TowerSkillTree.Get("arc_seed")); game.LearnTreeNode(TowerSkillTree.Get("bd_call")); }
            report.spent = game.campaign.SkillTree.Spent;
            game.treeSelection = "ft_foresight"; yield return Frames(3); yield return Capture("tree-grown");
            report.treeScreen = true; Check("tree screen with " + report.spent + " points spent");
            // The lower tier lies below the view: the wheel's scroll eases down to the throne and back up to the root.
            game.campaign.AddSkillPoints(40);
            foreach (string id in new[] { "or_stair", "or_bell", "or_gate", "cross_heaven", "hv_dawn", "hv_halo", "hv_star", "hv_choir", "uw_dusk", "uw_styx", "uw_soul", "ft_well", "ft_omen", "ft_offering" })
                Require(game.LearnTreeNode(TowerSkillTree.Get(id)), "Could not learn " + id);
            SkillNode root = TowerSkillTree.Root, throne = TowerSkillTree.Get("or_throne");
            Require(game.TreeNodeShown(root) && !game.TreeNodeShown(throne), "The tree did not open on its top with the throne out of view.");
            game.treeSelection = throne.Id; game.ScrollTreeBy(10000);
            yield return Until(() => game.TreeNodeShown(throne), 3, "Scrolling down never brought the throne into view.");
            Require(!game.TreeNodeShown(root), "The root stayed in view at the bottom of the tree.");
            yield return Frames(3); yield return Capture("tree-deep");
            game.ScrollTreeBy(-10000);
            yield return Until(() => game.TreeNodeShown(root), 3, "Scrolling up never brought the root back.");
            report.treeScrolls = !game.TreeNodeShown(throne); Require(report.treeScrolls, "The throne stayed in view at the top of the tree.");
            Check("the tree scrolls down to its third crossroads and back");
            game.villagePanel = TowerGame.VillagePanel.None; yield return Frames(3); yield return Capture("village-after-tree");

            // The next expedition starts at level one with the tree's bonuses and deals foresight's stat cards first.
            float villageDamage = game.run.Player.Damage;
            Require(game.StartExpedition(), "The second expedition did not depart."); game.SkipStory(); Pick();
            yield return Until(() => game.run.IsRewardPending && !game.run.IsRecruitOffer, 3, "Foresight dealt no stat cards.");
            report.foresightCards = game.run.Draft == RewardDraft.Stats && !game.run.IsFloorCleared && game.run.Player.Level == 1;
            // Companion passives may add to the hero on top, so the expedition hero is at least as strong as the village one.
            report.treeInExpedition = game.run.Player.Damage >= villageDamage - .01f && game.run.SkillTree == game.campaign.SkillTree;
            Require(report.foresightCards && report.treeInExpedition, "The expedition did not open with the tree's bonuses and foresight cards.");
            yield return Frames(3); yield return Capture("foresight-cards"); Check("foresight stat cards before the first fight");
            TakeReward(); yield return Frames(2);

            // A defeat on floor two keeps the points banked on floor one.
            yield return ClearByUpdate(); TakeReward(); game.NextFloor(FloorType.Normal); yield return Frames(2);
            int banked = game.campaign.SkillPoints;
            game.invulnerable = 0; game.hero.hp = 0; yield return Frames(20);
            report.pointsSurviveDefeat = game.campaign.SkillPoints == banked && game.ExpeditionPoints == 1 && game.campaign.ClearedChapters == 1;
            Require(report.pointsSurviveDefeat, "A defeat changed the banked points.");
            yield return Capture("expedition-lost"); Check("points survive a defeat");
            Finish(null);
        }

        private void Finish(Exception error)
        {
            if (finishing) return; finishing = true;
            if (error != null) report.errors.Add(error is TargetInvocationException && error.InnerException != null ? error.InnerException.ToString() : error.ToString());
            report.savedDataUntouched = PlayerPrefs.GetString(CampaignKey, "") == originalSaved;
            if (game) { game.campaign = originalCampaign; game.villagePanel = TowerGame.VillagePanel.None; game.menu = true; }
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.success = error == null && report.errors.Count == 0 && report.savedDataUntouched;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= RecordLog;
            if (quitOnComplete) Application.Quit(report.success ? 0 : 1);
        }
    }
}
