using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Explicit validation only: production inputs, projectile collision and floor transitions drive these checks.
    public sealed class TowerPolishSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class PolishReport
        {
            public string mode = "towerPolishSmoke", finishedUtc;
            public bool success, earlyDodgePrimes, oncePerDodge, ordinaryInvulnerabilityDoesNotPrime, lateDodgeDoesNotPrime;
            public bool missedSlashPreserves, counterDamageMultiplier, counterConsumedOnce, skillDamageUnchanged, companionDamageUnchanged;
            public bool pausedTimerFrozen, activeTimerExpires, damageCancelsCounter, floorTransitionResets, newRunResets;
            public bool cleanFloorsCounted, streakRecorded, shieldContactBreaksFlawless, hurtFloorBreaksFlawless, clearRecordedOnce;
            public bool clearRemovesProjectiles, rewardExitSafe;
            public float counterDamage, ordinaryDamage, shieldAbsorbed, actualDamageTaken;
            public List<string> checks = new List<string>(), screenshots = new List<string>(), errors = new List<string>();
        }
        private readonly PolishReport report = new PolishReport();
        private TowerGame game; private TowerSmokeDriver capture;
        private string prefix, reportPath; private bool finishing, quitOnComplete;
        private float deadline;
        private static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        public bool IsFinished { get { return finishing; } }
        public bool Succeeded { get { return finishing && report.success; } }
        private IList Shots { get { return (IList)typeof(TowerGame).GetField("shots", Hidden).GetValue(game); } }
        private object Call(string method, params object[] args)
        { return typeof(TowerGame).GetMethod(method, Hidden).Invoke(game, args); }
        private void SetField(string name, object value)
        { typeof(TowerGame).GetField(name, Hidden).SetValue(game, value); }
        private static void SetProperty(object target, string name, object value)
        { target.GetType().GetField("<" + name + ">k__BackingField", Hidden).SetValue(target, value); }

        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver, string capturePrefix, string outputReportPath, bool quitOnComplete = false)
        {
            if (game != null) throw new InvalidOperationException("Create a new polish driver for each suite run.");
            if (tower == null) throw new ArgumentNullException(nameof(tower));
            if (captureDriver == null) throw new ArgumentNullException(nameof(captureDriver));
            if (!Path.IsPathRooted(capturePrefix) || !Path.IsPathRooted(outputReportPath)) throw new ArgumentException("Suite capture and report paths must be absolute.");
            game = tower; capture = captureDriver; prefix = capturePrefix; reportPath = outputReportPath; this.quitOnComplete = quitOnComplete;
            capture.PrepareForSuite(tower); game.SuppressProgressPersistence = true; game.SmokeControlled = true; enabled = true;
            Application.logMessageReceived += RecordLog; deadline = Time.realtimeSinceStartup + 120;
            StartCoroutine(Guard(Suite()));
        }
        private void RecordLog(string condition, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(condition + "\n" + trace); }
        private void OnDestroy() { Application.logMessageReceived -= RecordLog; }
        private void Update()
        { if (!finishing && Time.realtimeSinceStartup > deadline) { StopAllCoroutines(); Finish(new TimeoutException("Polish suite exceeded 120 seconds.")); } }
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
        private IEnumerator Suite()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            yield return null;
            Setup(); VerifyDodgeWindow();
            yield return Capture("counter-ready", "회피 반격 검증 · 적 탄막을 회피해 다음 검 공격 강화");
            VerifyCounterDamage(); yield return VerifyCounterLifetime();
            yield return VerifyFloorRecords(); VerifyNewRun(); Finish(null);
        }
        private void Setup()
        {
            game.enabled = false;
            if (game.hero != null && game.hero.root) game.hero.root.SetActive(false);
            foreach (var actor in game.allies) if (actor.root) actor.root.SetActive(false);
            foreach (var actor in game.enemies) if (actor.root) actor.root.SetActive(false);
            game.NewRun(74); game.StartClimb();
            game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = game.DropOverride = game.FocusOverride = false;
            ClearEnemies(); ClearShots(); Place(game.hero, new Vector2(0, TowerWorld.Ground));
            SetField("previousHeroPosition", game.hero.pos); Spawn(25);
        }
        private void ClearEnemies()
        {
            foreach (var enemy in game.enemies) if (enemy.root) { enemy.root.SetActive(false); Destroy(enemy.root); }
            game.enemies.Clear(); Call("ResetEnemyCombat");
        }
        private void ClearShots()
        {
            foreach (object shot in Shots) { var root = (GameObject)shot.GetType().GetField("root").GetValue(shot); if (root) Destroy(root); }
            Shots.Clear();
        }
        private TowerGame.Fighter Spawn(float x)
        {
            var enemy = (TowerGame.Fighter)Call("Spawn", "Polish fixture", 0, true, new Vector2(x, TowerWorld.Ground));
            game.enemies.Add(enemy); Call("ConfigureEnemy", enemy, 0, 0);
            enemy.maxHp = enemy.hp = 1000; enemy.stunTime = enemy.cooldown = 999; return enemy;
        }
        private static void Place(TowerGame.Fighter actor, Vector2 position)
        { actor.pos = position; actor.velocityY = 0; actor.grounded = true; actor.root.transform.position = position; }
        private void BeginInputDodge()
        {
            game.dashCooldown = 0; game.DashOverride = true;
            try { Call("Update"); } finally { game.DashOverride = false; }
            Require(game.dashTime > 0 && game.invulnerable > 0, "Actual dash input did not start an invulnerable dodge.");
            SetField("previousHeroPosition", game.hero.pos);
        }
        private void CrossHostileShot()
        {
            Vector2 center = game.hero.pos + Vector2.up * .9f;
            Call("Fire", center + Vector2.left * 2, center + Vector2.right * 2, 200f, 12f, true, Color.red, 0f, null, 1, 0f);
            Call("TickShots", .02f);
            Require(Shots.Count == 0, "Hostile crossing fixture did not contact the hero's collision circle.");
        }
        private void VerifyDodgeWindow()
        {
            float hp = game.hero.hp;
            game.invulnerable = .5f; CrossHostileShot();
            Require(game.PerfectDodges == 0 && game.CounterRemaining == 0 && game.hero.hp == hp, "Ordinary hit invulnerability granted a dodge counter.");
            report.ordinaryInvulnerabilityDoesNotPrime = true;
            BeginInputDodge(); game.elapsed += TowerGame.DodgeCounterWindow + .001f; CrossHostileShot();
            Require(game.PerfectDodges == 0 && game.CounterRemaining == 0 && game.hero.hp == hp, "A late invulnerable dodge granted an early-window counter.");
            report.lateDodgeDoesNotPrime = true;
            BeginInputDodge(); CrossHostileShot();
            Require(game.PerfectDodges == 1 && Near(game.CounterRemaining, TowerGame.DodgeCounterDuration) && game.hero.hp == hp && game.FloorHits == 0,
                "A hostile crossing during the first dodge window failed to prime without damaging the hero.");
            report.earlyDodgePrimes = true;
            CrossHostileShot();
            Require(game.PerfectDodges == 1 && Near(game.CounterRemaining, TowerGame.DodgeCounterDuration), "More than one hostile crossing granted counters in the same dash.");
            report.oncePerDodge = true;
            report.checks.Add("Actual DashOverride starts the window: early hostile crossings grant one counter per dash without damage; ordinary invulnerability and crossings after 0.12 seconds grant none.");
        }
        private void VerifyCounterDamage()
        {
            float remaining = game.CounterRemaining; Call("PlayerSlash");
            Require(game.CounterRemaining == remaining && game.CounterHits == 0, "A missed slash consumed the prepared counter."); report.missedSlashPreserves = true;
            Require(game.run.Player.CritChance == 0, "Counter damage fixture must not contain random critical hits.");
            var first = Spawn(game.hero.pos.x + 1.1f); var second = Spawn(game.hero.pos.x + 1.4f);
            float baseDamage = game.PlayerDamage; Call("PlayerSlash");
            report.counterDamage = first.maxHp - first.hp;
            Require(Near(report.counterDamage, baseDamage * TowerGame.DodgeCounterMultiplier) && Near(second.maxHp - second.hp, report.counterDamage), "Connected counter slash did not apply its multiplier to the hit group.");
            Require(game.CounterRemaining == 0 && game.CounterHits == 1, "One slash hitting two targets consumed or counted more than one counter.");
            report.counterDamageMultiplier = true;
            float before = first.hp; Call("PlayerSlash"); report.ordinaryDamage = before - first.hp;
            Require(Near(report.ordinaryDamage, baseDamage) && game.CounterHits == 1, "A subsequent ordinary slash retained the counter bonus."); report.counterConsumedOnce = true;

            BeginInputDodge(); CrossHostileShot();
            Place(first, game.hero.pos + Vector2.right); Place(second, game.hero.pos + Vector2.right * 1.4f);
            AugmentDefinition definition = null;
            foreach (var augment in TowerRun.Augments) if (augment.OwnerId == "player" && augment.Kind == RewardKind.Skill && augment.Effect == SkillEffect.IceNova) { definition = augment; break; }
            Require(definition != null, "Missing player IceNova damage fixture.");
            var skill = new SkillState(definition, game.run.Player); before = first.hp;
            Call("CastSkill", game.run.Player, skill, game.hero, first);
            Require(Near(before - first.hp, game.PlayerDamage * skill.Power) && game.CounterRemaining > 0 && game.CounterHits == 1, "An automatic skill gained or consumed the basic-slash counter."); report.skillDamageUnchanged = true;
            var ally = new CompanionState(TowerRun.GetDefinition("michael")); Require(ally.CritChance == 0, "Companion fixture has random critical hits.");
            before = first.hp; Call("StrikeEnemy", first, 11f, ally, 0f);
            Require(Near(before - first.hp, 11) && game.CounterRemaining > 0 && game.CounterHits == 1, "Companion damage gained or consumed the player's counter."); report.companionDamageUnchanged = true;
            report.checks.Add("A miss preserves the charge; the next connected slash deals exactly 1.5x once, including multiple victims. The following slash, direct IceNova and companion StrikeEnemy keep their normal damage.");
        }
        private IEnumerator VerifyCounterLifetime()
        {
            Setup(); BeginInputDodge(); CrossHostileShot();
            float remaining = game.CounterRemaining, elapsed = game.elapsed;
            game.menu = true; game.enabled = true;
            float until = Time.realtimeSinceStartup + .18f; while (Time.realtimeSinceStartup < until) yield return null;
            game.enabled = false; game.menu = false;
            Require(game.CounterRemaining == remaining && game.elapsed == elapsed, "The paused menu advanced the counter timer or run clock.");
            game.party = true; Call("Update"); game.party = false;
            Require(game.CounterRemaining == remaining && game.elapsed == elapsed, "The party screen advanced the counter timer."); report.pausedTimerFrozen = true;
            // Let Unity advance frame time: repeated calls inside one frame can keep a tiny startup delta.
            int samples = 0; float expiryDeadline = Time.realtimeSinceStartup + 15;
            game.enabled = true;
            while (game.CounterRemaining > 0 && Time.realtimeSinceStartup < expiryDeadline) { samples++; yield return null; }
            game.enabled = false;
            Require(game.CounterRemaining == 0 && game.elapsed - elapsed >= TowerGame.DodgeCounterDuration - .05f && game.CounterHits == 0,
                "Prepared counter failed to expire after three seconds of active simulation: remaining=" + game.CounterRemaining.ToString("F6") +
                ", activeSeconds=" + (game.elapsed - elapsed).ToString("F6") + ", frames=" + samples + ", counterHits=" + game.CounterHits);
            report.activeTimerExpires = true;
            BeginInputDodge(); CrossHostileShot(); game.dashTime = game.invulnerable = 0;
            float hp = game.hero.hp; Call("HurtHero", 12f);
            Require(game.CounterRemaining == 0 && game.FloorHits == 1 && game.hero.hp < hp, "A real hit did not cancel the prepared counter and record floor contact."); report.damageCancelsCounter = true;
            report.checks.Add("Menu/party pause freeze the charge and run clock. Active production Update expires it after three seconds; an actual hit cancels it immediately.");
        }
        private IEnumerator VerifyFloorRecords()
        {
            Setup(); SetProperty(game.run, "MaxFloor", 4); game.elapsed = 12.5f;
            ClearViaUpdate();
            Require(game.LastFloorFlawless && game.FlawlessFloors == 1 && game.FlawlessStreak == 1 && game.LastFloorSeconds >= 12.5f && game.LastFloorSeconds < 12.6f, "Clean first floor result or duration was wrong.");
            yield return Capture("flawless-reward", "무피격 층 기록 · 실제 보상 화면");
            ChooseAndAdvance();
            game.elapsed += 9.25f; ClearViaUpdate();
            Require(game.LastFloorFlawless && game.FlawlessFloors == 2 && game.FlawlessStreak == 2 && game.BestFlawlessStreak == 2, "Two clean floors did not form a two-floor streak.");
            report.cleanFloorsCounted = report.streakRecorded = true;
            ChooseAndAdvance(); SetField("playerShield", 100f); SetField("shieldTime", 30f); game.invulnerable = 0;
            float hp = game.hero.hp; Call("HurtHero", 12f); report.shieldAbsorbed = 100 - game.PlayerShield;
            Require(game.hero.hp == hp && game.FloorHits == 1 && game.DamageTaken == 0 && report.shieldAbsorbed > 0, "Shield contact must count as a hit without adding health damage.");
            ClearViaUpdate();
            Require(!game.LastFloorFlawless && game.FlawlessFloors == 2 && game.FlawlessStreak == 0 && game.BestFlawlessStreak == 2, "Shield contact incorrectly counted as flawless or lost the best streak."); report.shieldContactBreaksFlawless = true;
            ChooseAndAdvance(); SetField("playerShield", 0f); game.invulnerable = 0;
            hp = game.hero.hp; Call("HurtHero", 20f); report.actualDamageTaken = hp - game.hero.hp;
            Require(report.actualDamageTaken > 0 && Near(game.DamageTaken, report.actualDamageTaken) && game.FloorHits == 1, "Health damage accounting was not the actual mitigated damage.");
            ClearViaUpdate();
            Require(!game.LastFloorFlawless && game.FlawlessFloors == 2 && game.FlawlessStreak == 0, "A hurt floor was recorded as flawless."); report.hurtFloorBreaksFlawless = true;
            game.SelectOffer(game.run.Offers[0].Id); Require(game.run.IsComplete, "The four-floor fixture did not complete through its final reward.");
            yield return Capture("ending-record", "등반 기록 검증 · 무피격 층과 최고 연속 기록");
            report.clearRecordedOnce = true;
            report.checks.Add("Four real Update-driven clears: two clean floors build streak 2; shield contact and health damage each break flawless status. Rewards/NextFloor preserve totals and reset floor-local state; repeated updates never recount a clear.");
        }
        private void ClearViaUpdate()
        {
            game.dashTime = 0; ClearShots(); ClearEnemies();
            Vector2 center = game.hero.pos + Vector2.up * .9f;
            Call("Fire", center + Vector2.left * 2, center, .1f, 12f, true, Color.red, 0f, null, 1, 0f);
            Require(Shots.Count == 1, "Floor-clear fixture did not retain its in-flight hostile projectile.");
            Call("Update");
            Require(game.run.IsRewardPending && game.run.IsFloorCleared && game.CounterRemaining == 0, "Floor clear did not open a reward and clear the counter.");
            Require(Shots.Count == 0, "Floor clear left an in-flight hostile projectile frozen beneath the reward screen."); report.clearRemovesProjectiles = true;
            int floors = game.FlawlessFloors, streak = game.FlawlessStreak, cleared = game.run.ClearedFloors; float seconds = game.LastFloorSeconds;
            for (int i = 0; i < 4; i++) Call("Update");
            Require(game.FlawlessFloors == floors && game.FlawlessStreak == streak && game.run.ClearedFloors == cleared && game.LastFloorSeconds == seconds,
                "Repeated reward-screen updates recorded the cleared floor again.");
        }
        private void ChooseAndAdvance()
        {
            int floor = game.run.Floor, total = game.FlawlessFloors;
            game.SelectOffer(game.run.Offers[0].Id); Require(!game.run.IsRewardPending, "Fixture reward was not selectable.");
            float hp = game.hero.hp; int hits = game.FloorHits;
            game.invulnerable = 0; for (int i = 0; i < 4; i++) Call("Update");
            Require(game.hero.hp >= hp && game.FloorHits == hits && Shots.Count == 0, "A cleared floor's old projectiles resumed damage after reward selection."); report.rewardExitSafe = true;
            // Inject a stale charge to verify transition cleanup independently of clear-time cleanup.
            SetProperty(game, "CounterRemaining", 1f); game.NextFloor();
            Require(game.run.Floor == floor + 1 && game.FloorHits == 0 && game.CounterRemaining == 0 && game.FlawlessFloors == total, "Floor transition leaked floor-local state or erased run totals.");
            foreach (var enemy in game.enemies) enemy.stunTime = enemy.cooldown = 999;
            report.floorTransitionResets = true;
        }
        private void VerifyNewRun()
        {
            Require(game.FlawlessFloors > 0 && game.DamageTaken > 0, "New-run reset fixture lacks previous records.");
            game.NewRun(75);
            Require(game.CounterRemaining == 0 && game.PerfectDodges == 0 && game.CounterHits == 0 && game.FlawlessFloors == 0 && game.FlawlessStreak == 0 && game.BestFlawlessStreak == 0 && game.FloorHits == 0 && game.DamageTaken == 0 && game.LastFloorSeconds == 0 && !game.LastFloorFlawless,
                "A new run inherited prior combat records."); report.newRunResets = true;
        }
        private IEnumerator Capture(string suffix, string message)
        {
            yield return null; yield return null;
            game.Notice(message, 5); yield return capture.CaptureForSuite(prefix, suffix);
            report.screenshots.Add(prefix + "-" + suffix + ".png");
        }
        private static bool Near(float first, float second) { return Mathf.Abs(first - second) < .002f; }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void Finish(Exception error)
        {
            if (finishing) return; finishing = true;
            if (error != null) report.errors.Add(error is TargetInvocationException && error.InnerException != null ? error.InnerException.ToString() : error.ToString());
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.success = error == null && report.errors.Count == 0;
            game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = game.DropOverride = false;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= RecordLog;
            if (quitOnComplete) Application.Quit(report.success ? 0 : 1);
        }
    }
}
