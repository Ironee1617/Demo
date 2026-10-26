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
            public bool openingRecruitCompanionOnly, mageRecruited, recruitFloorsCompanionOnly, augmentCardsWithoutRecruit, cardsOnlyOnFifthAndEliteFloors, sameMythSynergyActive;
            public bool branchGuardVerified, branchDoorsVerified, eliteBranchVerified, restBranchVerified, shopBranchVerified, merchantNeedsApproach, shopTradesOnce;
            public bool fireballLockedBeforeSelection, playerSkillProjectile, companionSkillProjectile, companionSkillRepeatsAfterCooldown;
            public bool fiveHeroStatsAffectRuntime, dismissedSkillsAndEffectsCleared, rehiredCompanionStartsFresh, allSkillEffectsVerified, deathNoResurrection, completed;
            public bool playerAttackTimingVerified, companionCastReleaseTimingVerified;
            public int seed, actualCombatKills, fixtureClearedFloors, smokeHealthRefills, finalFloor, clearedFloors;
            public float smokeHealthRestored, companionCooldown, observedCompanionCastInterval, restHealed;
            public float expectedAttackContactDelay, observedAttackContactDelay, expectedCastReleaseDelay, observedCastReleaseDelay;
            public int rewardSelections, recruitSelections, augmentSelections, shopAugments, pointsCredited, pointsSpent, allSkillEffectsCount;
            public List<string> recruitFloors = new List<string>();
            public List<string> cardFloors = new List<string>();
            public List<string> branchChoices = new List<string>();
            public List<string> shopPurchases = new List<string>();
            public List<string> synergies = new List<string>();
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
        private bool active, finishing, captureOnly, quitOnCaptureFailure = true;
        private float deadline;
        private bool capturePending, captureInProgress;
        private string capturePath, lastCapturePath;
        private RenderTexture captureTarget, previousRenderTarget;
        // Deterministic scenario seed, verified against the catalog: Uriel is in the opening recruit, every goal card
        // appears by floor 35, another Eden angel joins on floor 30 and Uriel can be rehired on floor 60.
        private const int SmokeSeed = 83;
        private const float SmokeSeconds = 240;
        // Uriel joins from the opening recruit; the rest are augment cards. Goals[FirstStatGoal..] are the five hero stats.
        private static readonly string[] Goals = { "uriel.skill1", "player.fireball", "player.might", "player.vitality", "player.armor", "player.swiftness", "player.frenzy" };
        private const int FirstStatGoal = 2;
        // The door taken on every fifth floor, which offers only rest, shop and elite exits; elite floors add goal cards.
        private static readonly Dictionary<int, FloorType> Branches = new Dictionary<int, FloorType>
        {
            { 5, FloorType.Elite }, { 10, FloorType.Rest }, { 15, FloorType.Elite }, { 20, FloorType.Shop }, { 25, FloorType.Elite },
            { 30, FloorType.Elite }, { 35, FloorType.Elite }, { 40, FloorType.Elite }, { 45, FloorType.Elite }, { 50, FloorType.Elite },
            { 55, FloorType.Elite }, { 60, FloorType.Elite }, { 65, FloorType.Shop }, { 70, FloorType.Shop }, { 75, FloorType.Rest },
            { 80, FloorType.Elite }, { 85, FloorType.Elite }, { 90, FloorType.Elite }, { 95, FloorType.Elite }
        };
        // The merchant trades once per shop floor, so each shop floor buys the next item.
        private static readonly ShopItem[] ShopPlan = { ShopItem.Augment, ShopItem.Vitality, ShopItem.Heal };
        private int shopVisits;
        private readonly HashSet<string> selectedGoals = new HashSet<string>();
        private bool probeActive, allowHealthRefill = true, skillsProbed, heroCaptured, dismissed, rehired, branchCaptured;
        private int cardClears;
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
            if(Array.IndexOf(args,"-towerEdenReview")>=0)
            {
                tower.SuppressProgressPersistence=true;
                string output=Argument(args,"-towerEdenCapture")??Path.Combine(Application.persistentDataPath,"eden-review");
                new GameObject("Eden pixel animation review").AddComponent<TowerEdenReview>().Initialize(tower,output);
                return;
            }
            if(Array.IndexOf(args,"-towerUIReview")>=0)
            {
                tower.SuppressProgressPersistence=true;game=tower;active=captureOnly=true;
                string output=Argument(args,"-towerUICapture")??Path.Combine(Application.persistentDataPath,"tower-ui");
                new GameObject("Tower UI review").AddComponent<TowerUIReview>().Initialize(tower,this,output);
                return;
            }
            foreach(string flag in new[]{"-towerOathSmoke","-towerPolishSmoke","-towerAnimationSmoke","-towerMythSmoke","-towerCompanionSmoke","-towerBossSmoke","-towerCombatSmoke","-towerArtSmoke","-towerSmoke","-towerThemeSmoke","-towerPlatformSmoke"})
                if(Array.IndexOf(args,flag)>=0)tower.SuppressProgressPersistence=true;
            if (Array.IndexOf(args, "-towerOathSmoke") >= 0)
            {
                game = tower; active = captureOnly = true;
                string oathPrefix = Argument(args, "-towerOathCapture") ?? Path.Combine(Application.persistentDataPath, "tower-oath");
                string oathReport = Argument(args, "-towerOathReport") ?? oathPrefix + "-report.json";
                new GameObject("Tower oath validation").AddComponent<TowerOathSmokeDriver>().Initialize(tower, this, oathPrefix, oathReport, true);
                return;
            }
            if (Array.IndexOf(args, "-towerPolishSmoke") >= 0)
            {
                game = tower; active = captureOnly = true;
                string polishPrefix = Argument(args, "-towerPolishCapture") ?? Path.Combine(Application.persistentDataPath, "tower-polish");
                string polishReport = Argument(args, "-towerPolishReport") ?? Path.Combine(Application.persistentDataPath, "tower-polish.json");
                new GameObject("Tower polish validation").AddComponent<TowerPolishSmokeDriver>().Initialize(tower, this, polishPrefix, polishReport, true);
                return;
            }
            if(Array.IndexOf(args,"-towerAnimationSmoke")>=0)
            {
                new GameObject("Tower animation validation").AddComponent<TowerAnimationSmokeDriver>().Initialize(tower);
                return;
            }
            if (Array.IndexOf(args, "-towerMythSmoke") >= 0)
            {
                game = tower; active = captureOnly = true;
                new GameObject("Tower mythology validation").AddComponent<TowerMythSmokeDriver>().Initialize(tower, this);
                return;
            }
            if (Array.IndexOf(args, "-towerCompanionSmoke") >= 0)
            {
                game = tower; active = captureOnly = true;
                string companionPrefix = Argument(args, "-towerCompanionCapture") ?? Path.Combine(Application.persistentDataPath, "tower-companions");
                string companionReport = Argument(args, "-towerCompanionReport") ?? Path.Combine(Application.persistentDataPath, "tower-companions.json");
                new GameObject("Tower companion validation").AddComponent<TowerCompanionSmokeDriver>().Initialize(tower, this, companionPrefix, companionReport, true);
                return;
            }
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
            BeginClimbSmoke();
        }

        // Runs the -towerSmoke full climb inside an editor play session (Tests/Run-TowerEditorSmoke.ps1 -Suite Climb).
        public void StartClimbSmoke(TowerGame tower, string prefix, string report)
        {
            if (tower == null) throw new ArgumentNullException(nameof(tower));
            if (active || finishing || captureInProgress) throw new InvalidOperationException("Start the climb in a fresh play session without other tower scenarios.");
            // Like the player flag, the editor run never saves its fixture wallet over the account.
            tower.SuppressProgressPersistence = true;
            game = tower; capturePrefix = prefix; reportPath = report; enabled = true;
            BeginClimbSmoke();
        }

        private void BeginClimbSmoke()
        {
            Application.logMessageReceived += RecordLog;
            active = true;
            deadline = Time.realtimeSinceStartup + SmokeSeconds;
            game.SmokeControlled = true;
            StartCoroutine(Guard(Smoke()));
        }

        // Enables the shared camera/IMGUI capture host for an explicitly started suite.
        public void PrepareForSuite(TowerGame tower)
        {
            if (tower == null) throw new ArgumentNullException(nameof(tower));
            if (captureInProgress) throw new InvalidOperationException("Wait for the current HUD capture before starting another suite.");
            StopAllCoroutines(); Application.logMessageReceived -= RecordLog;
            game = tower; active = captureOnly = true; finishing = false; quitOnCaptureFailure = false;
            capturePending = false; capturePath = lastCapturePath = null; probeActive = false; probeTarget = null;
            game.SmokeControlled = true; enabled = true;
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
                Fail(new TimeoutException("Tower smoke exceeded " + SmokeSeconds + " seconds."));
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
            // A blank wallet holds this climb's floor points; the saved account is never read or written.
            game.progress = new TowerProgress();
            // The opening recruit only exists outside SmokeControlled, so the run starts with normal rules.
            game.SmokeControlled = false; game.NewRun(SmokeSeed); game.SmokeControlled = true;
            game.intro = true; report.seed = game.seed;
            yield return null;
            yield return Capture("intro");
            game.StartClimb();
            Require(game.run.IsRewardPending && game.run.IsRecruitOffer && game.run.Companions.Count == 0 && game.run.Floor == 1 && !game.run.IsFloorCleared,
                "A new climb did not open with a recruit card set.");
            yield return SelectReward();
            Require(report.mageRecruited, "The opening recruit did not bring Uriel into the party.");
            yield return RealCombat();
            VerifyCardRule();
            report.branchGuardVerified = !game.run.IsBranchFloor && !game.run.AdvanceFloor(FloorType.Elite) && !game.run.AdvanceFloor(FloorType.Shop) && game.run.Floor == 1;
            Require(report.branchGuardVerified, "An ordinary floor accepted a branch exit.");

            while (!game.run.IsComplete)
            {
                if (game.run.IsRewardPending) { yield return SelectReward(); continue; }
                if (game.run.IsFloorCleared) { yield return Advance(); continue; }
                if (game.run.Floor == 2 && !report.fireballLockedBeforeSelection)
                {
                    PrepareProbe();
                    float until = game.elapsed + .4f;
                    while (game.elapsed < until) yield return null;
                    report.fireballLockedBeforeSelection = game.run.GetHero("uriel").GetSkill("uriel.skill1") == null && game.SkillCasts("uriel", "uriel.skill1") == 0;
                    Require(report.fireballLockedBeforeSelection, "Companion fireball was active before its skill card was selected.");
                    probeActive = false;
                }
                // Uriel leaves after the 30th-floor recruit so the 60th-floor card set, which favours the party's mythology, can rehire him.
                if (selectedGoals.Count == Goals.Length && !skillsProbed && game.run.Floor > TowerRun.RecruitFloors[0])
                {
                    yield return ProbeOwnedSkills();
                    skillsProbed = true;
                    game.OpenParty("uriel"); yield return Capture("companionskills"); game.party = false;
                    game.Dismiss("uriel"); dismissed = true;
                    report.dismissedSkillsAndEffectsCleared = game.run.GetHero("uriel") == null && game.SkillCasts("uriel", "uriel.skill1") == 0 && game.SkillRemaining("uriel", "uriel.skill1") == 0 && game.OwnerProjectiles("uriel") == 0;
                    Require(report.dismissedSkillsAndEffectsCleared, "Dismissal retained a learned skill, clock, cast counter or projectile.");
                    yield return VerifyAllEffects();
                }
                yield return ClearByFixture();
            }
            int elites = 0, shops = 0; foreach (var exit in Branches.Values) { if (exit == FloorType.Elite) elites++; if (exit == FloorType.Shop) shops++; }
            report.shopBranchVerified = shopVisits == shops && shops == ShopPlan.Length && report.shopPurchases.Count == ShopPlan.Length;
            // Every clear was checked against the card rule as it happened; the totals confirm none were skipped.
            report.cardsOnlyOnFifthAndEliteFloors = cardClears == game.run.MaxFloor / TowerRun.RewardInterval + elites &&
                report.rewardSelections == 1 + cardClears + report.shopAugments;
            report.augmentCardsWithoutRecruit = report.augmentSelections > 0;
            report.recruitFloorsCompanionOnly = String.Join(",", report.recruitFloors.ToArray()) == "start,30,60";
            report.fiveHeroStatsAffectRuntime = report.heroStatChanges.Count == 5;
            report.completed = game.run.IsComplete && game.run.Floor == game.run.MaxFloor && game.run.ClearedFloors == game.run.MaxFloor;
            Require(report.openingRecruitCompanionOnly && report.mageRecruited && report.recruitFloorsCompanionOnly && report.augmentCardsWithoutRecruit && report.cardsOnlyOnFifthAndEliteFloors &&
                report.branchGuardVerified && report.branchDoorsVerified && report.eliteBranchVerified && report.restBranchVerified &&
                report.shopBranchVerified && report.merchantNeedsApproach && report.shopTradesOnce && report.sameMythSynergyActive &&
                report.fireballLockedBeforeSelection && report.playerSkillProjectile && report.companionSkillProjectile && report.companionSkillRepeatsAfterCooldown &&
                report.fiveHeroStatsAffectRuntime && report.dismissedSkillsAndEffectsCleared && report.rehiredCompanionStartsFresh && report.allSkillEffectsVerified && report.deathNoResurrection &&
                report.playerAttackTimingVerified && report.companionCastReleaseTimingVerified && heroCaptured && branchCaptured && report.completed,
                "A required climb, reward, branch, shop or skill check did not execute.");
            yield return Capture("complete");
            yield return new WaitForSecondsRealtime(.5f);
            finishing = true; active = false; report.success = report.errors.Count == 0; WriteReport();
            Application.Quit(report.success ? 0 : 1);
        }

        private IEnumerator RealCombat()
        {
            // The opening companion holds its basic attacks so only the player's sword clears this floor.
            HoldAllies();
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
            // The long first floor streams its later packs in as the hero advances; they count toward the clear too.
            int expectedKills = game.RemainingEnemies; bool captured = false;
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
                else if (game.PendingEnemies > 0) game.MoveOverride = 1;
                if (!captured && report.playerAttackVerified && game.enemies.Count > 0) { captured = true; yield return Capture("combat"); }
                HoldAllies();
                yield return null;
            }
            game.MoveOverride = 0; game.AttackOverride = false;
            foreach (var ally in game.allies) ally.cooldown = 0;
            report.actualCombatKills = game.kills;
            report.realCombatFirstFloorCleared = game.run.Floor == 1 && game.kills == expectedKills && game.RemainingEnemies == 0;
            Require(report.realCombatFirstFloorCleared && report.playerAttackVerified && captured, "The first encounter was not cleared by actual attacks.");
        }

        private void HoldAllies()
        { foreach (var ally in game.allies) { ally.cooldown = 999; ally.basicPending = false; } }

        private bool FiveStatsSelected()
        { for (int i = FirstStatGoal; i < Goals.Length; i++) if (!selectedGoals.Contains(Goals[i])) return false; return true; }

        private void VerifyAttackContactTiming()
        {
            if (report.playerAttackTimingVerified || game.LastPlayerAttackStartedAt < 0 || game.LastPlayerAttackContactAt < game.LastPlayerAttackStartedAt) return;
            report.expectedAttackContactDelay = game.hero.art.AttackHitTime(game.LastPlayerAttackDuration);
            report.observedAttackContactDelay = game.LastPlayerAttackContactAt - game.LastPlayerAttackStartedAt;
            Require(Mathf.Abs(report.expectedAttackContactDelay - report.observedAttackContactDelay) <= .07f,
                "Basic attack damage did not occur at the production animation contact frame.");
            report.playerAttackTimingVerified = true;
        }

        private RewardOffer Offer(string augmentId)
        { foreach (var offer in game.run.Offers) if (offer.AugmentId == augmentId) return offer; return null; }

        private RewardOffer PreferredOffer()
        {
            foreach (string goal in Goals) if (!selectedGoals.Contains(goal)) { var offer = Offer(goal); if (offer != null) return offer; }
            return null;
        }

        // Spends each slot's single reroll until a wanted card shows; null when none appears.
        private RewardOffer WithRerolls(Func<RewardOffer> wanted)
        {
            var selected = wanted(); if (selected != null) return selected;
            for (int slot = 0; slot < TowerRun.OfferSlots; slot++) { game.run.Reroll(slot); selected = wanted(); if (selected != null) return selected; }
            return null;
        }

        private RewardOffer PickOffer()
        {
            var selected = WithRerolls(PreferredOffer); if (selected != null) return selected;
            foreach (var offer in game.run.Offers) if (offer.Kind == RewardKind.Trait && offer.OwnerId == "player") return offer;
            foreach (var offer in game.run.Offers) if (offer.Kind == RewardKind.Trait) return offer;
            foreach (var offer in game.run.Offers) if (offer.Kind == RewardKind.Skill) return offer;
            return game.run.Offers[0];
        }

        private IEnumerator SelectReward()
        {
            Require(game.run.Offers.Count == TowerRun.OfferSlots, "Every card set must show exactly three cards.");
            bool recruit = game.run.IsRecruitOffer, opening = recruit && !game.run.IsFloorCleared;
            string where = opening ? "start" : game.run.Floor.ToString();
            foreach (var offer in game.run.Offers)
                Require((offer.Kind == RewardKind.Recruit) == recruit, recruit ? "A recruit card set showed a non-companion card." : "An augment card set offered a companion.");
            RewardOffer selected;
            if (opening)
            {
                report.openingRecruitCompanionOnly = game.run.Floor == 1;
                selected = Offer("recruit:uriel");
                Require(selected != null, "The deterministic fixture seed no longer offers Uriel in the opening recruit.");
                int slot = (selected.Slot + 1) % TowerRun.OfferSlots;
                long[] previous = { game.run.Offers[0].Id, game.run.Offers[1].Id, game.run.Offers[2].Id };
                report.rerollSingleUseVerified = game.run.Reroll(slot) && !game.run.Reroll(slot) && !game.run.CanReroll(slot) && !game.run.ChooseOffer(previous[slot]) &&
                    game.run.Offers[slot].Kind == RewardKind.Recruit;
                report.rerollOtherSlotsPreserved = true;
                for (int other = 0; other < TowerRun.OfferSlots; other++) if (other != slot) report.rerollOtherSlotsPreserved &= game.run.Offers[other].Id == previous[other] && game.run.CanReroll(other);
                Require(report.rerollSingleUseVerified && report.rerollOtherSlotsPreserved, "Independent single-use rerolls failed.");
                yield return Capture("recruit");
            }
            // Later recruits take the first card, which favours the party's mythology; a dismissed Uriel is searched for to check a fresh rehire.
            else if (recruit) selected = (dismissed && !rehired ? WithRerolls(() => Offer("recruit:uriel")) : null) ?? game.run.Offers[0];
            else selected = PickOffer();
            string id = selected.AugmentId;
            int stat = Array.IndexOf(Goals, id) - FirstStatGoal;
            bool firstStat = stat >= 0 && !selectedGoals.Contains(id);
            float before = firstStat ? HeroStatValue(stat) : 0;
            var party = new List<HeroState> { game.run.Player }; party.AddRange(game.run.Companions);
            string partyStats = PartyStats(party);
            game.SelectOffer(selected.Id);
            Require(!game.run.IsRewardPending, "The selected card did not apply.");
            if (Array.IndexOf(Goals, id) >= 0) selectedGoals.Add(id);
            report.rewardSelections++;
            if (recruit) { report.recruitSelections++; report.recruitFloors.Add(where); } else report.augmentSelections++;
            report.selectedAugments.Add(where + ":" + id);
            if (recruit && party.Count > 1 && game.run.MythCount(game.run.GetCompanion(selected.CompanionId).Definition.MythId) >= 2)
            {
                // A same-myth pair must activate its synergy and change an existing member's live stats.
                report.sameMythSynergyActive = game.run.ActiveSynergies.Count > 0 && PartyStats(party) != partyStats;
                foreach (var synergy in game.run.ActiveSynergies) report.synergies.Add(where + ":" + synergy.Definition.MythId + " x" + synergy.Members);
                Require(report.sameMythSynergyActive, "A same-myth recruit did not activate a synergy that changes live stats.");
            }
            if (id == "recruit:uriel")
            {
                report.mageRecruited = game.run.GetCompanion("uriel") != null && game.allies.Exists(a => a.id == "uriel");
                if (dismissed)
                {
                    rehired = true;
                    report.rehiredCompanionStartsFresh = game.run.GetHero("uriel").Skills.Count == 0 && game.SkillCasts("uriel", "uriel.skill1") == 0 && game.SkillRemaining("uriel", "uriel.skill1") == 0;
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
            if (!heroCaptured && FiveStatsSelected())
            {
                game.OpenParty("player"); yield return Capture("heroabilities"); game.party = false; heroCaptured = true;
            }
        }

        private static string PartyStats(List<HeroState> party)
        {
            var text = new System.Text.StringBuilder();
            foreach (var hero in party) foreach (HeroStat stat in Enum.GetValues(typeof(HeroStat))) text.Append(hero.StatText(stat)).Append('|');
            return text.ToString();
        }

        // Cards follow only fifth and elite floors; the 30th and 60th offer companions while the party has room.
        private void VerifyCardRule()
        {
            int floor = game.run.Floor; var type = game.run.FloorType;
            bool card = type == FloorType.Elite || type == FloorType.Normal && floor % TowerRun.RewardInterval == 0;
            bool recruit = card && type == FloorType.Normal && TowerRun.IsRecruitFloor(floor) && game.run.Companions.Count < TowerRun.MaxCompanions;
            Require(game.run.IsRewardPending == card && game.run.IsRecruitOffer == recruit, "Floor " + floor + " (" + type + ") broke the card rule.");
            if (card) { cardClears++; report.cardFloors.Add(floor + (type == FloorType.Elite ? "E" : recruit ? "R" : "")); }
        }

        private IEnumerator ClearByFixture()
        {
            probeActive = false;
            foreach (var enemy in game.enemies) if (enemy.root) Destroy(enemy.root);
            game.enemies.Clear(); report.fixtureClearedFloors++;
            float limit = Time.realtimeSinceStartup + 3;
            while (!game.run.IsFloorCleared) { Require(Time.realtimeSinceStartup < limit, "Empty encounter did not clear."); yield return null; }
            VerifyCardRule();
        }

        private IEnumerator Advance()
        {
            int floor = game.run.Floor;
            Require(game.run.IsBranchFloor == (floor % TowerRun.RewardInterval == 0), "Floor " + floor + " showed the wrong exits.");
            FloorType next = FloorType.Normal;
            if (game.run.IsBranchFloor)
            {
                Require(Branches.TryGetValue(floor, out next), "The fixture has no door planned for branch floor " + floor + ".");
                // The plain exit is gone: standing where it was reaches the elite door instead.
                StandAt(TowerWorld.ExitX);
                report.branchDoorsVerified = game.NearestGate() == FloorType.Elite;
                Require(report.branchDoorsVerified, "A branch floor still offered the plain exit.");
                report.branchChoices.Add(floor + ">" + next);
                if (!branchCaptured)
                {
                    // Stand among the three doors so the capture shows the branch gates.
                    StandAt(TowerWorld.GateX(FloorType.Shop));
                    float settle = Time.realtimeSinceStartup + 1;
                    while (Time.realtimeSinceStartup < settle) yield return null;
                    yield return Capture("branch"); branchCaptured = true;
                }
            }
            // Leave through the door the hero stands in, as the E key does.
            StandAt(TowerWorld.GateX(next));
            Require(game.NearestGate() == next, "The hero did not reach the " + next + " exit on floor " + floor + ".");
            if (next == FloorType.Rest) allowHealthRefill = false;
            game.NextFloor(next);
            Require(game.run.Floor == floor + 1 && game.run.FloorType == next && !game.run.IsFloorCleared, "Advancing did not open the chosen " + next + " floor.");
            if (next == FloorType.Rest || next == FloorType.Shop) { yield return PeacefulFloor(next); yield break; }
            Require(game.enemies.Count > 0, "Advancing did not spawn a new encounter.");
            if (next == FloorType.Elite)
            {
                report.eliteBranchVerified = game.enemies.TrueForAll(e => e.isElite);
                Require(report.eliteBranchVerified, "The elite exit spawned ordinary enemies.");
            }
            yield return null;
        }

        // Rest and shop floors hold no monsters and clear on their first update.
        private IEnumerator PeacefulFloor(FloorType type)
        {
            Require(game.enemies.Count == 0, "The " + type + " floor spawned monsters.");
            float wounded = game.hero.hp = type == FloorType.Rest ? game.PlayerMaxHealth * .3f : game.hero.hp;
            float limit = Time.realtimeSinceStartup + 3;
            while (!game.run.IsFloorCleared) { Require(Time.realtimeSinceStartup < limit, "The " + type + " floor did not clear on arrival."); yield return null; }
            VerifyCardRule();
            if (type == FloorType.Shop) { yield return Shop(); yield break; }
            report.restHealed = game.hero.hp - wounded;
            report.restBranchVerified = report.restHealed >= game.PlayerMaxHealth * TowerRun.RestHealFraction - .5f;
            allowHealthRefill = true;
            Require(report.restBranchVerified, "The rest floor did not restore half of max health.");
        }

        private IEnumerator Shop()
        {
            Require(shopVisits < ShopPlan.Length, "The fixture has no purchase planned for another shop floor.");
            ShopItem item = ShopPlan[shopVisits++];
            Require(game.run.ShopOffers.Count == 3 && !game.shopOpen && !game.run.ShopTraded, "The shop floor did not stock its three items.");
            // SmokeControlled clears earn nothing, so credit this run's cleared floors the way a normal clear does.
            int before = game.progress.Points, credited = game.progress.RecordFloorClear(game.run);
            report.pointsCredited += credited;
            Require(credited > 0 && game.progress.Points == before + credited && report.pointsCredited == game.run.ClearedFloors, "The shop wallet did not receive this run's floor points.");
            // The stall stays shut until the hero walks up to the merchant.
            report.merchantNeedsApproach = !game.NearMerchant && !game.InteractMerchant() && !game.shopOpen;
            Require(report.merchantNeedsApproach, "The merchant traded from across the floor.");
            float limit = Time.realtimeSinceStartup + 8;
            while (!game.NearMerchant)
            {
                Require(Time.realtimeSinceStartup < limit, "The hero could not walk to the merchant.");
                game.MoveOverride = Mathf.Sign(TowerWorld.FeaturePosition.x - game.hero.pos.x);
                yield return null;
            }
            game.MoveOverride = 0;
            Require(game.InteractMerchant() && game.shopOpen, "The merchant did not open the stall for a nearby hero.");
            if (shopVisits == 1) yield return Capture("shop");
            float maxBefore = game.PlayerMaxHealth;
            if (item == ShopItem.Heal) { allowHealthRefill = false; game.hero.hp = game.PlayerMaxHealth * .4f; }
            float hpBefore = game.hero.hp;
            Require(Buy(item) && game.run.ShopTraded && !game.shopOpen, "The merchant did not sell " + item + ".");
            if (item == ShopItem.Augment)
            {
                Require(game.run.IsRewardPending && !game.run.IsRecruitOffer, "Buying an augment did not open a card set.");
                report.shopAugments++;
                yield return SelectReward();
            }
            else if (item == ShopItem.Vitality)
                Require(Mathf.Abs(game.PlayerMaxHealth - maxBefore - TowerRun.VitalityHealth) < .01f && Mathf.Abs(game.hero.maxHp - game.PlayerMaxHealth) < .01f,
                    "The vitality essence did not raise the live max health.");
            else
            {
                Require(game.hero.hp >= hpBefore + game.PlayerMaxHealth * TowerRun.ShopHealFraction - .5f, "The heal potion did not restore health.");
                allowHealthRefill = true;
            }
            // One trade per shop floor: the stall stays shut and a second purchase is refused without spending points.
            int points = game.progress.Points;
            ShopItem other = item == ShopItem.Augment ? ShopItem.Vitality : ShopItem.Augment;
            report.shopTradesOnce = !game.InteractMerchant() && !game.shopOpen && !game.BuyShopItem(other) && game.progress.Points == points && !game.run.CanBuy(other, Int32.MaxValue);
            Require(report.shopTradesOnce, "The merchant traded twice on one shop floor.");
        }

        // Buys through the production shop path and confirms the price left the wallet.
        private bool Buy(ShopItem item)
        {
            int points = game.progress.Points, price = game.run.GetShopOffer(item).Price;
            if (!game.BuyShopItem(item)) return false;
            report.pointsSpent += price; report.shopPurchases.Add(game.run.Floor + ":" + item + " " + price + "P");
            return game.progress.Points == points - price;
        }

        private void StandAt(float x)
        {
            game.hero.pos = new Vector2(x, TowerWorld.Ground); game.hero.velocityY = 0; game.hero.root.transform.position = game.hero.pos;
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
            var skill = game.run.GetHero("uriel").GetSkill("uriel.skill1");
            Require(skill != null && game.run.Player.GetSkill("player.fireball") != null, "Required owned skills were not learned through cards.");
            report.companionCooldown = skill.Cooldown;
            int companionBefore = game.SkillCasts("uriel", "uriel.skill1"), playerBefore = game.SkillCasts("player", "player.fireball");
            int observed = companionBefore; float firstCast = -1; float limit = Time.realtimeSinceStartup + 35;
            while (!report.companionSkillRepeatsAfterCooldown || !report.playerSkillProjectile)
            {
                Require(Time.realtimeSinceStartup < limit, "Learned skills did not repeat in a live encounter.");
                int count = game.SkillCasts("uriel", "uriel.skill1");
                if (count > observed)
                {
                    float at = game.SkillLastCastAt("uriel", "uriel.skill1");
                    var ally = game.allies.Find(a => a.id == "uriel");
                    report.expectedCastReleaseDelay = ally.art.CastReleaseTime(SkillEffect.Fireball, game.SkillCastDuration("uriel", "uriel.skill1"));
                    report.observedCastReleaseDelay = at - game.SkillLastStartedAt("uriel", "uriel.skill1");
                    Require(Mathf.Abs(report.expectedCastReleaseDelay - report.observedCastReleaseDelay) <= .07f,
                        "The companion projectile was not released at the production casting frame.");
                    report.companionCastReleaseTimingVerified = true;
                    if (firstCast < 0) firstCast = at;
                    else { report.observedCompanionCastInterval = at - firstCast; report.companionSkillRepeatsAfterCooldown = report.observedCompanionCastInterval >= skill.Cooldown - .1f; }
                    report.companionSkillProjectile |= game.OwnerProjectiles("uriel") > 0;
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
            if (captureOnly)
            {
                Debug.LogError("Tower suite HUD capture failed: " + error);
                if (quitOnCaptureFailure) Application.Quit(1);
                return;
            }
            try { WriteReport(); }
            catch (Exception writeError) { Debug.LogError("Tower smoke report could not be saved: " + writeError); }
            Application.Quit(1);
        }
    }
}
