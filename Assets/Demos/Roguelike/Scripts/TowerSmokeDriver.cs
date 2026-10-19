using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Opt-in player smoke fixture. Normal players never enter this code path.
    public sealed class TowerSmokeDriver : MonoBehaviour
    {
        [Serializable]
        private sealed class SmokeReport
        {
            public string mode = "towerSmoke";
            public string captureMethod = "Camera RenderTexture with actual IMGUI repaint";
            public string finishedUtc;
            public bool success, movementVerified, jumpVerified, dashVerified, playerAttackVerified;
            public bool realCombatFirstFloorCleared, rerollSingleUseVerified, rerollOtherSlotsPreserved;
            public bool firstFloorReward, everyFloorRewards, mixedRecruitSkillTrait, mageRecruited;
            public bool fireballLockedBeforeSelection, playerSkillProjectile, companionSkillProjectile, companionSkillRepeatsAfterCooldown;
            public bool fiveHeroStatsAffectRuntime, dismissedSkillsAndEffectsCleared, rehiredCompanionStartsFresh, allSkillEffectsVerified, deathNoResurrection, completed;
            public bool playerAttackTimingVerified, companionCastReleaseTimingVerified;
            public int seed, actualCombatKills, fixtureClearedFloors, smokeHealthRefills, finalFloor, clearedFloors;
            public float smokeHealthRestored, companionCooldown, observedCompanionCastInterval;
            public float expectedAttackContactDelay, observedAttackContactDelay, expectedCastReleaseDelay, observedCastReleaseDelay;
            public int rewardSelections, allSkillEffectsCount;
            public List<string> heroStatChanges = new List<string>();
            public List<string> selectedAugments = new List<string>();
            public List<string> effectResults = new List<string>();
            public List<string> companions = new List<string>();
            public List<string> screenshots = new List<string>();
            public List<string> errors = new List<string>();
        }

        private readonly SmokeReport report = new SmokeReport();
        private TowerGame game;
        private string capturePrefix, reportPath;
        private bool active, finishing, captureOnly;
        private float deadline;
        private bool capturePending, captureInProgress;
        private string capturePath, lastCapturePath;
        private RenderTexture captureTarget, previousRenderTarget;
        private const int SmokeSeed = 74; // Deterministic scenario seed, verified against the catalog below.
        private static readonly string[] Goals = { "recruit:sera", "sera.skill1", "player.fireball", "player.might", "player.vitality", "player.armor", "player.swiftness", "player.frenzy" };
        private readonly HashSet<string> selectedGoals = new HashSet<string>();
        private bool probeActive, allowHealthRefill = true, skillsProbed, heroCaptured, dismissed, rehired;
        private TowerGame.Fighter probeTarget;

        private static readonly BindingFlags HiddenStatic = BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly MethodInfo BeginGUI = typeof(GUIUtility).GetMethod("BeginGUI", HiddenStatic);
        private static readonly MethodInfo EndGUI = typeof(GUIUtility).GetMethod("EndGUI", HiddenStatic);
        private static readonly MethodInfo BeginContainer = typeof(GUIUtility).GetMethod("BeginContainer", HiddenStatic);
        private static readonly MethodInfo EndContainer = typeof(GUIUtility).GetMethod("EndContainer", HiddenStatic);
        private static readonly MethodInfo DrawGUI = typeof(TowerGame).GetMethod("OnGUI", BindingFlags.NonPublic | BindingFlags.Instance);

        public void Initialize(TowerGame tower)
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-towerBossSmoke") >= 0)
            {
                game = tower; active = captureOnly = true;
                new GameObject("Tower boss validation").AddComponent<TowerBossSmokeDriver>().Initialize(tower, this);
                return;
            }
            if (Array.IndexOf(args, "-towerCombatSmoke") >= 0)
            {
                game = tower; active = captureOnly = true;
                new GameObject("Tower combat validation").AddComponent<TowerCombatSmokeDriver>().Initialize(tower, this);
                return;
            }
            if (Array.IndexOf(args, "-towerArtSmoke") >= 0)
            {
                new GameObject("Tower art validation").AddComponent<TowerArtSmokeDriver>().Initialize(tower);
                enabled = false; return;
            }
            if (Array.IndexOf(args, "-towerSmoke") < 0) { enabled = false; return; }
            game = tower;
            capturePrefix = Argument(args, "-towerCapture");
            reportPath = Argument(args, "-towerReport");
            if (String.IsNullOrEmpty(capturePrefix)) capturePrefix = Path.Combine(Application.persistentDataPath, "tower-smoke");
            if (String.IsNullOrEmpty(reportPath)) reportPath = Path.Combine(Application.persistentDataPath, "tower-smoke.json");
            Application.logMessageReceived += RecordLog;
            active = true;
            deadline = Time.realtimeSinceStartup + 240;
            game.SmokeControlled = true;
            StartCoroutine(Guard(Smoke()));
        }

        private static string Argument(string[] args, string key)
        {
            int at = Array.IndexOf(args, key);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        private void Update()
        {
            if (captureOnly || !active || finishing || game == null || game.hero == null) return;
            // This fixture alone restores health; production combat always uses its normal damage.
            if (allowHealthRefill && game.hero.hp < game.PlayerMaxHealth)
            {
                report.smokeHealthRefills++;
                report.smokeHealthRestored += game.PlayerMaxHealth - game.hero.hp;
                game.hero.hp = game.PlayerMaxHealth;
            }
            if (Time.realtimeSinceStartup > deadline)
            {
                StopAllCoroutines();
                Fail(new TimeoutException("Tower smoke exceeded 240 seconds."));
            }
        }

        private void RecordLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                if (report.errors.Count < 50) report.errors.Add(type + ": " + condition + "\n" + stackTrace);
        }

        private void OnDestroy() { Application.logMessageReceived -= RecordLog; }

        private void LateUpdate()
        {
            MaintainProbe();
            if (!active || finishing || !capturePending) return;
            // Hidden Windows players can omit native IMGUI repaint dispatch. Use the same
            // real OnGUI method inside a temporary GUI container instead of a backbuffer.
            Event previous = Event.current;
            object state = null;
            bool began = false, container = false;
            Exception failure = null;
            try
            {
                Require(BeginGUI != null && EndGUI != null && BeginContainer != null && EndContainer != null && DrawGUI != null,
                    "Unity GUI repaint entry points were unavailable for offscreen capture.");
                state = Activator.CreateInstance(typeof(GUIUtility).Assembly.GetType("UnityEngine.ObjectGUIState"), true);
                BeginContainer.Invoke(null, new[] { state }); container = true;
                BeginGUI.Invoke(null, new object[] { 0, game.GetInstanceID(), 0 }); began = true;
                Event.current = new Event { type = EventType.Repaint };
                DrawGUI.Invoke(game, null);
            }
            catch (Exception ex) { failure = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex; }
            finally
            {
                try
                {
                    if (began) EndGUI.Invoke(null, new object[] { 0 });
                    if (container) EndContainer.Invoke(null, null);
                    if (state != null) ((IDisposable)state).Dispose();
                    if (previous != null) Event.current = previous;
                }
                catch (Exception ex) { if (failure == null) failure = ex; }
            }
            if (failure != null) Fail(failure);
        }

        internal bool BeginHudCapture()
        {
            if (!capturePending || Event.current == null || Event.current.type != EventType.Repaint) return false;
            capturePending = false;
            captureTarget = new RenderTexture(Screen.width, Screen.height, 24);
            previousRenderTarget = RenderTexture.active;
            var previousCameraTarget = game.view.targetTexture;
            try
            {
                game.view.targetTexture = captureTarget;
                game.view.Render();
            }
            finally { game.view.targetTexture = previousCameraTarget; }
            RenderTexture.active = captureTarget;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
            captureInProgress = true;
            return true;
        }

        internal void EndHudCapture()
        {
            if (!captureInProgress) return;
            Texture2D pixels = null;
            try
            {
                GL.Flush();
                pixels = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(capturePath, pixels.EncodeToPNG());
                lastCapturePath = capturePath;
            }
            finally
            {
                GL.PopMatrix();
                RenderTexture.active = previousRenderTarget;
                captureTarget.Release();
                Destroy(captureTarget);
                if (pixels != null) Destroy(pixels);
                captureTarget = null;
                captureInProgress = false;
            }
        }

        // Walk nested iterators ourselves so a failed assertion still produces a JSON report.
        private IEnumerator Guard(IEnumerator body)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(body);
            while (stack.Count > 0)
            {
                bool moved = false;
                object current = null;
                Exception error = null;
                try
                {
                    moved = stack.Peek().MoveNext();
                    if (moved) current = stack.Peek().Current;
                }
                catch (Exception ex) { error = ex; }
                if (error != null) { Fail(error); yield break; }
                if (!moved) { stack.Pop(); continue; }
                var nested = current as IEnumerator;
                if (nested != null) stack.Push(nested);
                else yield return current;
            }
        }

        private IEnumerator Smoke()
        {
            Require(Path.IsPathRooted(capturePrefix) && Path.IsPathRooted(reportPath), "Smoke output paths must be absolute.");
            Directory.CreateDirectory(Path.GetDirectoryName(capturePrefix));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            game.NewRun(SmokeSeed); game.intro = true; report.seed = game.seed;
            yield return null;
            yield return Capture("intro");
            game.StartClimb();
            yield return RealCombat();
            Require(game.run.IsRewardPending && game.run.Floor == 1, "The first cleared floor did not offer a reward.");
            report.firstFloorReward = true;

            while (!game.run.IsComplete)
            {
                if (game.run.IsRewardPending)
                {
                    yield return SelectReward();
                    if (!heroCaptured && FiveStatsSelected())
                    {
                        game.OpenParty("player"); yield return Capture("heroabilities"); game.party = false; heroCaptured = true;
                    }
                    continue;
                }
                if (game.run.IsFloorCleared)
                {
                    int oldFloor = game.run.Floor; game.NextFloor();
                    Require(game.run.Floor == oldFloor + 1 && game.enemies.Count > 0, "Advancing did not spawn a new encounter.");
                    yield return null;
                }
                if (game.run.Floor == 2 && !report.fireballLockedBeforeSelection)
                {
                    PrepareProbe();
                    float until = game.elapsed + .4f;
                    while (game.elapsed < until) yield return null;
                    report.fireballLockedBeforeSelection = game.run.GetHero("sera").GetSkill("sera.skill1") == null && game.SkillCasts("sera", "sera.skill1") == 0;
                    Require(report.fireballLockedBeforeSelection, "Companion fireball was active before its skill card was selected.");
                    probeActive = false;
                }
                if (selectedGoals.Count == Goals.Length && !skillsProbed)
                {
                    Require(game.run.Floor < 25, "The skill fixture needs a live floor before the final reward.");
                    yield return ProbeOwnedSkills();
                    skillsProbed = true;
                    game.OpenParty("sera"); yield return Capture("companionskills"); game.party = false;
                    game.Dismiss("sera"); dismissed = true;
                    report.dismissedSkillsAndEffectsCleared = game.run.GetHero("sera") == null && game.SkillCasts("sera", "sera.skill1") == 0 && game.SkillRemaining("sera", "sera.skill1") == 0 && game.OwnerProjectiles("sera") == 0;
                    Require(report.dismissedSkillsAndEffectsCleared, "Dismissal retained a learned skill, clock, cast counter or projectile.");
                    yield return VerifyAllEffects();
                }
                if (!game.run.IsFloorCleared)
                {
                    probeActive = false;
                    foreach (var enemy in game.enemies) if (enemy.root) Destroy(enemy.root);
                    game.enemies.Clear(); report.fixtureClearedFloors++;
                    float limit = Time.realtimeSinceStartup + 3;
                    while (!game.run.IsFloorCleared) { Require(Time.realtimeSinceStartup < limit, "Empty encounter did not clear."); yield return null; }
                    Require(game.run.IsRewardPending, "A cleared floor did not offer its three cards.");
                }
            }
            report.everyFloorRewards = report.rewardSelections == 25 && game.run.ClearedFloors == 25;
            report.fiveHeroStatsAffectRuntime = report.heroStatChanges.Count == 5;
            report.completed = game.run.IsComplete;
            Require(report.firstFloorReward && report.everyFloorRewards && report.mixedRecruitSkillTrait && report.mageRecruited &&
                report.fireballLockedBeforeSelection && report.playerSkillProjectile && report.companionSkillProjectile && report.companionSkillRepeatsAfterCooldown &&
                report.fiveHeroStatsAffectRuntime && report.dismissedSkillsAndEffectsCleared && report.rehiredCompanionStartsFresh && report.allSkillEffectsVerified && report.deathNoResurrection &&
                report.playerAttackTimingVerified && report.companionCastReleaseTimingVerified && heroCaptured && report.completed,
                "A required mixed reward or skill check did not execute.");
            yield return Capture("complete");
            yield return new WaitForSecondsRealtime(.5f);
            finishing = true; active = false; report.success = report.errors.Count == 0; WriteReport();
            Application.Quit(report.success ? 0 : 1);
        }

        private IEnumerator RealCombat()
        {
            float startX = game.hero.pos.x; game.MoveOverride = 1;
            float until = Time.realtimeSinceStartup + .8f;
            while (Time.realtimeSinceStartup < until) yield return null;
            report.movementVerified = game.hero.pos.x > startX + 2;
            Require(report.movementVerified, "Player movement did not advance the hero.");
            float jumpStart = game.hero.pos.y; game.JumpOverride = true; yield return null; game.JumpOverride = false;
            until = Time.realtimeSinceStartup + .6f;
            while (Time.realtimeSinceStartup < until) { if (game.hero.pos.y > jumpStart + .4f) report.jumpVerified = true; yield return null; }
            Require(report.jumpVerified, "Jump did not lift the hero.");
            game.DashOverride = true; yield return null; game.DashOverride = false;
            report.dashVerified = game.dashCooldown > .5f || game.dashTime > 0;
            Require(report.dashVerified, "Dash did not start.");
            int expectedKills = game.enemies.Count; bool captured = false;
            game.AttackOverride = true; float limit = Time.realtimeSinceStartup + 65;
            while (!game.run.IsFloorCleared)
            {
                Require(Time.realtimeSinceStartup < limit, "Real first-floor combat timed out.");
                TowerGame.Fighter nearest = null; float best = float.MaxValue;
                foreach (var enemy in game.enemies)
                {
                    float distance = Vector2.Distance(enemy.pos, game.hero.pos);
                    if (distance < best) { nearest = enemy; best = distance; }
                    if (enemy.hp < enemy.maxHp && game.attackTimer > 0) { report.playerAttackVerified = true; VerifyAttackContactTiming(); }
                }
                if (game.kills > 0) { report.playerAttackVerified = true; VerifyAttackContactTiming(); }
                if (nearest != null)
                {
                    float dx = nearest.pos.x - game.hero.pos.x;
                    bool turn = Mathf.Abs(dx) > .15f && Mathf.Sign(dx) != game.hero.facing;
                    game.MoveOverride = game.hero.grounded && game.hero.pos.y - nearest.pos.y > 1.75f ? 1 : Mathf.Abs(dx) > 1.25f || turn ? Mathf.Sign(dx) : 0;
                }
                if (!captured && report.playerAttackVerified && game.enemies.Count > 0) { captured = true; yield return Capture("combat"); }
                yield return null;
            }
            game.MoveOverride = 0; game.AttackOverride = false;
            report.actualCombatKills = game.kills;
            report.realCombatFirstFloorCleared = game.run.Floor == 1 && game.kills == expectedKills && game.enemies.Count == 0;
            Require(report.realCombatFirstFloorCleared && report.playerAttackVerified && captured, "The first encounter was not cleared by actual attacks.");
        }

        private bool FiveStatsSelected()
        { for (int i = 3; i < Goals.Length; i++) if (!selectedGoals.Contains(Goals[i])) return false; return true; }

        private void VerifyAttackContactTiming()
        {
            if (report.playerAttackTimingVerified || game.LastPlayerAttackStartedAt < 0 || game.LastPlayerAttackContactAt < game.LastPlayerAttackStartedAt) return;
            report.expectedAttackContactDelay = game.hero.art.AttackHitTime(game.LastPlayerAttackDuration);
            report.observedAttackContactDelay = game.LastPlayerAttackContactAt - game.LastPlayerAttackStartedAt;
            Require(Mathf.Abs(report.expectedAttackContactDelay - report.observedAttackContactDelay) <= .07f,
                "Basic attack damage did not occur at the production animation contact frame.");
            report.playerAttackTimingVerified = true;
        }

        private RewardOffer PreferredOffer()
        {
            if (dismissed && !rehired) foreach (var offer in game.run.Offers) if (offer.AugmentId == "recruit:sera") return offer;
            foreach (string goal in Goals) if (!selectedGoals.Contains(goal)) foreach (var offer in game.run.Offers) if (offer.AugmentId == goal) return offer;
            return null;
        }

        private RewardOffer PickOffer()
        {
            var selected = PreferredOffer(); if (selected != null) return selected;
            for (int slot = 0; slot < 3; slot++) { game.run.Reroll(slot); selected = PreferredOffer(); if (selected != null) return selected; }
            foreach (var offer in game.run.Offers) if (offer.Kind == RewardKind.Trait && offer.OwnerId == "player") return offer;
            foreach (var offer in game.run.Offers) if (offer.Kind == RewardKind.Trait) return offer;
            foreach (var offer in game.run.Offers) if (offer.Kind == RewardKind.Skill) return offer;
            return game.run.Offers[0];
        }

        private IEnumerator SelectReward()
        {
            Require(game.run.Offers.Count == 3, "Every floor must show exactly three cards.");
            if (game.run.Floor == 1)
            {
                RewardOffer sera = null; foreach (var offer in game.run.Offers) if (offer.AugmentId == "recruit:sera") sera = offer;
                Require(sera != null, "The deterministic fixture seed no longer offers Sera on floor one.");
                int slot = (sera.Slot + 1) % 3;
                long[] previous = { game.run.Offers[0].Id, game.run.Offers[1].Id, game.run.Offers[2].Id };
                report.rerollSingleUseVerified = game.run.Reroll(slot) && !game.run.Reroll(slot) && !game.run.CanReroll(slot) && !game.run.ChooseOffer(previous[slot]);
                report.rerollOtherSlotsPreserved = true;
                for (int other = 0; other < 3; other++) if (other != slot) report.rerollOtherSlotsPreserved &= game.run.Offers[other].Id == previous[other] && game.run.CanReroll(other);
                Require(report.rerollSingleUseVerified && report.rerollOtherSlotsPreserved, "Independent single-use rerolls failed.");
            }
            if (game.run.Floor == 2)
            {
                bool recruit = false, skill = false, trait = false;
                foreach (var offer in game.run.Offers) { recruit |= offer.Kind == RewardKind.Recruit; skill |= offer.Kind == RewardKind.Skill; trait |= offer.Kind == RewardKind.Trait; }
                report.mixedRecruitSkillTrait = recruit && skill && trait && game.run.Companions.Count < 3;
                Require(report.mixedRecruitSkillTrait, "The deterministic fixture no longer shows all three mixed card kinds.");
                yield return Capture("mixedreward");
            }
            var selected = PickOffer(); string id = selected.AugmentId;
            int stat = Array.IndexOf(Goals, id) - 3;
            bool firstStat = stat >= 0 && stat < 5 && !selectedGoals.Contains(id);
            float before = firstStat ? HeroStatValue(stat) : 0;
            game.SelectOffer(selected.Id);
            Require(!game.run.IsRewardPending, "The selected card did not apply.");
            if (Array.IndexOf(Goals, id) >= 0) selectedGoals.Add(id);
            report.rewardSelections++; report.selectedAugments.Add(game.run.Floor + ":" + id);
            if (id == "recruit:sera")
            {
                report.mageRecruited = game.run.GetCompanion("sera") != null && game.allies.Exists(a => a.id == "sera");
                if (dismissed)
                {
                    rehired = true;
                    report.rehiredCompanionStartsFresh = game.run.GetHero("sera").Skills.Count == 0 && game.SkillCasts("sera", "sera.skill1") == 0 && game.SkillRemaining("sera", "sera.skill1") == 0;
                    Require(report.rehiredCompanionStartsFresh, "Rehiring retained skills or an old runtime clock.");
                }
            }
            if (firstStat)
            {
                float after = HeroStatValue(stat);
                Require(stat == 4 ? after < before : after > before, "Hero trait did not change its runtime combat value: " + id);
                if (stat == 1) Require(Mathf.Abs(game.hero.maxHp - game.PlayerMaxHealth) < .01f, "Max health did not reach the live fighter.");
                report.heroStatChanges.Add(id + ": " + before + " -> " + after);
            }
        }

        private float HeroStatValue(int index)
        { return index == 0 ? game.PlayerDamage : index == 1 ? game.PlayerMaxHealth : index == 2 ? game.PlayerArmor : index == 3 ? game.PlayerMoveSpeed : game.PlayerAttackInterval; }

        private void PrepareProbe()
        {
            Require(game.enemies.Count > 0, "A combat probe needs a live enemy.");
            probeTarget = game.enemies[0];
            for (int i = game.enemies.Count - 1; i > 0; i--) { Destroy(game.enemies[i].root); game.enemies.RemoveAt(i); }
            probeTarget.hp = probeTarget.maxHp = 1000000; probeTarget.cooldown = 999; probeTarget.windup = 0;
            game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = false;
            probeActive = true; MaintainProbe();
        }

        private void MaintainProbe()
        {
            if (!probeActive || probeTarget == null || game == null) return;
            game.hero.pos = new Vector2(0, TowerWorld.Ground); game.hero.velocityY = 0; game.hero.root.transform.position = game.hero.pos;
            probeTarget.pos = new Vector2(5, TowerWorld.Ground); probeTarget.velocityY = 0; probeTarget.root.transform.position = probeTarget.pos; probeTarget.cooldown = 999; probeTarget.windup = 0;
            foreach (var ally in game.allies) ally.cooldown = 999;
        }

        private IEnumerator ProbeOwnedSkills()
        {
            PrepareProbe();
            var skill = game.run.GetHero("sera").GetSkill("sera.skill1");
            Require(skill != null && game.run.Player.GetSkill("player.fireball") != null, "Required owned skills were not learned through cards.");
            report.companionCooldown = skill.Cooldown;
            int companionBefore = game.SkillCasts("sera", "sera.skill1"), playerBefore = game.SkillCasts("player", "player.fireball");
            int observed = companionBefore; float firstCast = -1; float limit = Time.realtimeSinceStartup + 35;
            while (!report.companionSkillRepeatsAfterCooldown || !report.playerSkillProjectile)
            {
                Require(Time.realtimeSinceStartup < limit, "Learned skills did not repeat in a live encounter.");
                int count = game.SkillCasts("sera", "sera.skill1");
                if (count > observed)
                {
                    float at = game.SkillLastCastAt("sera", "sera.skill1");
                    var ally = game.allies.Find(a => a.id == "sera");
                    report.expectedCastReleaseDelay = ally.art.CastReleaseTime(SkillEffect.Fireball, game.SkillCastDuration("sera", "sera.skill1"));
                    report.observedCastReleaseDelay = at - game.SkillLastStartedAt("sera", "sera.skill1");
                    Require(Mathf.Abs(report.expectedCastReleaseDelay - report.observedCastReleaseDelay) <= .07f,
                        "The companion projectile was not released at the production casting frame.");
                    report.companionCastReleaseTimingVerified = true;
                    if (firstCast < 0) firstCast = at;
                    else { report.observedCompanionCastInterval = at - firstCast; report.companionSkillRepeatsAfterCooldown = report.observedCompanionCastInterval >= skill.Cooldown - .1f; }
                    report.companionSkillProjectile |= game.OwnerProjectiles("sera") > 0;
                    observed = count;
                }
                if (game.SkillCasts("player", "player.fireball") > playerBefore && game.OwnerProjectiles("player") > 0) report.playerSkillProjectile = true;
                yield return null;
            }
            Require(report.companionSkillProjectile && report.companionSkillRepeatsAfterCooldown && report.playerSkillProjectile, "A learned projectile or cooldown interval was not observed.");
            probeActive = false;
        }

        private IEnumerator VerifyAllEffects()
        {
            // Explicit low-level fixtures supplement the normal card-unlock/cooldown checks above.
            game.party = true; allowHealthRefill = false;
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var cast = typeof(TowerGame).GetMethod("CastSkill", flags);
            var tickShots = typeof(TowerGame).GetMethod("TickShots", flags);
            var tickAreas = typeof(TowerGame).GetMethod("TickSkillAreas", flags);
            var reset = typeof(TowerGame).GetMethod("ResetSkills", flags);
            var shots = (IList)typeof(TowerGame).GetField("shots", flags).GetValue(game);
            var covered = new HashSet<SkillEffect>();
            Require(cast != null && tickShots != null && tickAreas != null && reset != null, "Skill fixture entry points are unavailable.");
            foreach (var definition in TowerRun.Augments)
            {
                if (definition.Kind != RewardKind.Skill || !covered.Add(definition.Effect)) continue;
                foreach (object shot in shots) { var root = (GameObject)shot.GetType().GetField("root").GetValue(shot); if (root) Destroy(root); }
                shots.Clear(); reset.Invoke(game, null);
                game.hero.pos = new Vector2(0, TowerWorld.Ground); game.hero.root.transform.position = game.hero.pos;
                probeTarget.pos = new Vector2(1, TowerWorld.Ground); probeTarget.root.transform.position = probeTarget.pos;
                probeTarget.hp = probeTarget.maxHp = 1000000; probeTarget.slowTime = 0;
                game.hero.hp = game.PlayerMaxHealth * .3f;
                float beforeHp = probeTarget.hp, beforeHero = game.hero.hp, beforeShield = game.PlayerShield, beforeX = game.hero.pos.x;
                var fixtureSkill = new SkillState(definition, game.run.Player);
                cast.Invoke(game, new object[] { game.run.Player, fixtureSkill, game.hero, probeTarget });
                for (int frame = 0; frame < 32; frame++) { tickAreas.Invoke(game, new object[] { .05f }); tickShots.Invoke(game, new object[] { .05f }); }
                bool damaged = probeTarget.hp < beforeHp;
                bool verified = definition.Effect == SkillEffect.HealPulse ? game.hero.hp > beforeHero : definition.Effect == SkillEffect.ShieldPulse ? game.PlayerShield > beforeShield : damaged;
                if (definition.Effect == SkillEffect.IceNova || definition.Effect == SkillEffect.PoisonCloud) verified &= probeTarget.slowTime > 0;
                if (definition.Effect == SkillEffect.LifeDrain) verified &= game.hero.hp > beforeHero;
                if (definition.Effect == SkillEffect.DashStrike) verified &= Mathf.Abs(game.hero.pos.x - beforeX) > .01f;
                Require(verified, "Skill effect produced no expected damage, status, healing or shield: " + definition.Effect);
                report.effectResults.Add(definition.Effect + ": PASS");
            }
            report.allSkillEffectsCount = covered.Count;
            report.allSkillEffectsVerified = covered.Count == Enum.GetValues(typeof(SkillEffect)).Length;
            Require(report.allSkillEffectsVerified, "Not every skill effect had a catalog definition and runtime check.");
            VerifyDeathDoesNotClearFloor(shots, reset, flags);
            game.party = false; allowHealthRefill = true;
            yield return null;
        }

        private void ClearFixtureShots(IList shots)
        {
            foreach (object shot in shots) { var root = (GameObject)shot.GetType().GetField("root").GetValue(shot); if (root) Destroy(root); }
            shots.Clear();
        }

        private void VerifyDeathDoesNotClearFloor(IList shots, MethodInfo reset, BindingFlags flags)
        {
            var fire = typeof(TowerGame).GetMethod("Fire", flags);
            var tick = typeof(TowerGame).GetMethod("Update", flags);
            var spawn = typeof(TowerGame).GetMethod("Spawn", flags);
            Require(fire != null && tick != null && spawn != null, "Death regression entry points are unavailable.");
            int savedKills = game.kills;
            ClearFixtureShots(shots); reset.Invoke(game, null);
            game.party = false; game.hero.pos = new Vector2(0, TowerWorld.Ground); game.hero.velocityY = 0;
            game.hero.hp = 1; game.invulnerable = 0; game.attackTimer = 0;
            probeTarget.pos = game.hero.pos + Vector2.right; probeTarget.hp = 1; probeTarget.kind = 0;
            probeTarget.windup = .0001f; probeTarget.cooldown = 999; probeTarget.strikePoint = game.hero.pos;
            Vector2 target = probeTarget.pos + Vector2.up;
            fire.Invoke(game, new object[] { target, target, 0f, 1000f, false, Color.white, 0f, null, 1, 0f });
            tick.Invoke(game, null);
            Require(game.hero.hp <= 0 && !game.run.IsFloorCleared && !game.run.IsRewardPending && game.enemies.Count == 1,
                "A lethal melee hit let a later friendly projectile clear the floor and revive the hero.");

            // The second ordering covers lethal hostile and final friendly shots in one projectile pass.
            ClearFixtureShots(shots); reset.Invoke(game, null);
            game.hero.hp = 1; game.invulnerable = 0; probeTarget.hp = 1; probeTarget.windup = 0; probeTarget.cooldown = 999;
            fire.Invoke(game, new object[] { target, target, 0f, 1000f, false, Color.white, 0f, null, 1, 0f });
            Vector2 player = game.hero.pos + Vector2.up;
            fire.Invoke(game, new object[] { player, player, 0f, 1000f, true, Color.red, 0f, null, 1, 0f });
            tick.Invoke(game, null);
            Require(game.hero.hp <= 0 && !game.run.IsFloorCleared && !game.run.IsRewardPending && game.enemies.Count == 0,
                "A lethal hostile projectile and the last enemy dying in one frame revived the hero through clear healing.");
            ClearFixtureShots(shots); reset.Invoke(game, null);
            probeTarget = (TowerGame.Fighter)spawn.Invoke(game, new object[] { "Death regression target", 0, true, new Vector2(5, TowerWorld.Ground) });
            probeTarget.hp = probeTarget.maxHp = 1000000; probeTarget.cooldown = 999; game.enemies.Add(probeTarget);
            game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0; game.kills = savedKills;
            game.party = true; report.deathNoResurrection = true;
        }
        private IEnumerator Capture(string suffix)
        {
            string path = capturePrefix + "-" + suffix + ".png";
            capturePath = path;
            lastCapturePath = null;
            capturePending = true;
            float limit = Time.realtimeSinceStartup + 8;
            yield return null;
            while (lastCapturePath != path || !File.Exists(path) || new FileInfo(path).Length == 0)
            {
                Require(Time.realtimeSinceStartup < limit, "Screenshot was not saved: " + path);
                yield return null;
            }
            yield return null;
            yield return null;
            report.screenshots.Add(path);
        }

        internal IEnumerator CaptureForSuite(string prefix, string suffix)
        { capturePrefix = prefix; return Capture(suffix); }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private void WriteReport()
        {
            report.finishedUtc = DateTime.UtcNow.ToString("o");
            if (game != null && game.run != null)
            {
                report.finalFloor = game.run.Floor;
                report.clearedFloors = game.run.ClearedFloors;
                report.companions.Clear();
                foreach (var companion in game.run.Companions)
                    report.companions.Add(companion.Definition.Id + ":" + companion.Definition.Name + ":Lv" + companion.Level);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        }

        private void Fail(Exception error)
        {
            if (finishing) return;
            finishing = true;
            active = false;
            report.success = false;
            report.errors.Add(error.ToString());
            if (game != null) { game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = false; }
            try { WriteReport(); }
            catch (Exception writeError) { Debug.LogError("Tower smoke report could not be saved: " + writeError); }
            Application.Quit(1);
        }
    }
}
