using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Explicitly initialized by an Editor/CLI fixture. Never runs for ordinary players.
    public sealed class TowerCompanionSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class CheckResult
        {
            public string name, companion, role, detail;
            public bool passed, projectileObserved;
            public float expectedReleaseDelay, observedReleaseDelay, hpLost, cooldown, observedCastInterval;
        }
        [Serializable] private sealed class CompanionReport
        {
            public string mode = "towerCompanionSmoke", startedUtc, finishedUtc;
            public bool success;
            public int expectedCases, basicCompanions;
            public List<string> fixtureAdjustments = new List<string>();
            public List<CheckResult> checks = new List<CheckResult>();
            public List<string> screenshots = new List<string>(), errors = new List<string>();
        }

        public bool IsFinished { get; private set; }
        public bool Succeeded { get; private set; }
        public string FailureReason { get; private set; }
        private readonly CompanionReport report = new CompanionReport();
        private TowerGame game;
        private TowerSmokeDriver capture;
        private CheckResult current;
        private string prefix, reportPath;
        private bool initialized, finishing, subscribed, previousSmokeControlled, quitOnFinish, movementOnly;
        private float deadline;
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver, string prefix, string reportPath, bool quitOnFinish = false, bool movementOnly = false)
        {
            if (initialized) throw new InvalidOperationException("This companion suite was already initialized.");
            if (!tower || !captureDriver) throw new ArgumentException("A live tower and capture driver are required.");
            this.prefix = AbsoluteFilePath(prefix, false);
            this.reportPath = AbsoluteFilePath(reportPath, true);
            Directory.CreateDirectory(Path.GetDirectoryName(this.prefix));
            Directory.CreateDirectory(Path.GetDirectoryName(this.reportPath));
            game = tower; capture = captureDriver; initialized = true; this.quitOnFinish = quitOnFinish; this.movementOnly = movementOnly;
            previousSmokeControlled = game.SmokeControlled;
            report.startedUtc = DateTime.UtcNow.ToString("o");
            report.basicCompanions = TowerRun.Definitions.Count;
            report.mode = movementOnly ? "towerCompanionMovementSmoke" : "towerCompanionSmoke";
            report.expectedCases = movementOnly ? 10 : TowerRun.Definitions.Count * 5 + 20;
            report.fixtureAdjustments.Add("Each case starts a fresh seeded run using the companion's own mythology and inserts that companion into the roster; recruitment filtering is covered by model tests.");
            report.fixtureAdjustments.Add("Attack-timing cases disable player movement, jump, dash and attack inputs. Production-spawned targets have 100000 HP and a long stun/cooldown, so enemy attacks cannot affect the observation.");
            report.fixtureAdjustments.Add("Basic attacks run through the normal enabled TowerGame.Update. No fixture calls Fire, StrikeEnemy, DamageEnemy, BeginAllyAttack or ResolveAllyAttack.");
            report.fixtureAdjustments.Add("The first basic attack is isolated by setting its recovery cooldown high after release. Learned-skill cases suppress basic attacks and apply the named learned-skill definition directly.");
            report.fixtureAdjustments.Add("The rear-crossing regression moves the target behind a real pending melee attack. Dismissal cases call the production Dismiss method. The suite leaves the fixture paused and never quits the application.");
            report.fixtureAdjustments.Add("Movement regressions drive real player move/reversal/dash/jump inputs. Locked targets stand at the right world boundary so ordinary hit knockback cannot move them; HP remains finite and is never restored during observation. The death/rejoin case weakens its target for a real basic hit, and a second live enemy keeps the floor running.");
            Application.logMessageReceived += RecordLog; subscribed = true;
            deadline = Time.realtimeSinceStartup + 900;
            try
            {
                capture.PrepareForSuite(game);
                game.SmokeControlled = true;
                StartCoroutine(Guard(Suite()));
            }
            catch (Exception error) { Finish(error); }
        }

        private static string AbsoluteFilePath(string path, bool json)
        {
            if (String.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
                throw new ArgumentException("Companion output paths must be absolute file paths.");
            string full = Path.GetFullPath(path), name = Path.GetFileName(full);
            if (String.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Directory.Exists(full))
                throw new ArgumentException("Invalid companion output file path.");
            if (json && !full.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The companion report must have a .json extension.");
            return full;
        }
        private void RecordLog(string message, string trace, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && report.errors.Count < 64)
                report.errors.Add(type + ": " + message);
        }
        private void Update()
        {
            if (!initialized || IsFinished || finishing) return;
            if (!game || game.hero == null || !game.hero.root || Time.realtimeSinceStartup > deadline)
            {
                StopAllCoroutines();
                Finish(new InvalidOperationException(!game ? "The tower was destroyed during the suite." : "Companion suite exceeded its lifetime or 900-second deadline."));
            }
        }
        private void OnDisable()
        {
            if (initialized && !IsFinished && !finishing)
            { StopAllCoroutines(); Finish(new OperationCanceledException("Companion suite was disabled before completion.")); }
        }
        private void OnDestroy()
        {
            if (initialized && !IsFinished && !finishing) Finish(new OperationCanceledException("Companion suite was destroyed before completion."));
            Unsubscribe();
        }
        private void Unsubscribe()
        { if (subscribed) { Application.logMessageReceived -= RecordLog; subscribed = false; } }

        private IEnumerator Guard(IEnumerator body)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(body);
            while (stack.Count > 0)
            {
                bool more = false; object value = null; Exception error = null;
                try { more = stack.Peek().MoveNext(); if (more) value = stack.Peek().Current; }
                catch (Exception caught) { error = caught; }
                if (error != null) { Finish(error); yield break; }
                if (!more) { stack.Pop(); continue; }
                var nested = value as IEnumerator;
                if (nested != null) stack.Push(nested); else yield return value;
            }
        }
        private IEnumerator Case(string name, string companion, IEnumerator body)
        {
            current = new CheckResult { name = name, companion = companion, role = TowerRun.GetDefinition(companion).Role.ToString() };
            report.checks.Add(current);
            var stack = new Stack<IEnumerator>(); stack.Push(body);
            while (stack.Count > 0)
            {
                bool more = false; object value = null; Exception error = null;
                try { more = stack.Peek().MoveNext(); if (more) value = stack.Peek().Current; }
                catch (Exception caught) { error = caught; }
                if (error != null)
                {
                    current.detail = Unwrap(error).Message;
                    report.errors.Add(name + ": " + current.detail);
                    yield break;
                }
                if (!more) { stack.Pop(); continue; }
                var nested = value as IEnumerator;
                if (nested != null) stack.Push(nested); else yield return value;
            }
            current.passed = true;
        }
        private IEnumerator Suite()
        {
            yield return Case("target-lock:michael", "michael", TargetLock("michael"));
            yield return Case("target-lock:gabriel", "gabriel", TargetLock("gabriel"));
            yield return Case("nearer-enemy-keeps-target-lock", "gabriel", NearerEnemyKeepsLock());
            yield return Case("target-death-attacks-during-return", "michael", TargetDeathRejoins());
            yield return Case("follow-jumps-before-leader-lands", "michael", PromptFollowJump());
            yield return Case("moving-jump-keeps-landing-during-attack", "gabriel", MovingJumpAndAttack());
            yield return Case("overlapping-followers-share-platform", "gabriel", SharedPlatform());
            yield return Case("rendered-soles-stay-above-ground", "michael", GroundedSoles());
            yield return Case("smooth-follow-and-one-platform-jump", "michael", SmoothFollowAndJump());
            yield return Case("high-platform-rejoin-via-reachable-ledge", "michael", HighPlatformRejoin());
            if (movementOnly) { Finish(null); yield break; }
            foreach (var definition in TowerRun.Definitions)
                yield return Case("basic:" + definition.Id, definition.Id, BasicAttack(definition.Id, false));
            foreach (var definition in TowerRun.Definitions)
                foreach (var augment in TowerRun.Augments)
                    if (augment.OwnerId == definition.Id && augment.Kind == RewardKind.Skill)
                        yield return Case("learned:" + augment.Id, definition.Id, LearnedSkill(definition.Id, augment));
            foreach (string id in new[] { "michael", "gabriel", "uriel", "raphael" })
                yield return Case("airborne:" + id, id, BasicAttack(id, true));
            yield return Case("learned-fireball", "uriel", LearnedFireball());
            yield return Case("dismiss-pending-basic", "michael", DismissPendingBasic());
            yield return Case("dismiss-pending-skill", "uriel", DismissPendingSkill());
            yield return Case("dismiss-projectile", "gabriel", DismissProjectile());
            yield return Case("dismiss-area", "uriel", DismissArea());
            yield return Case("melee-target-crosses-behind", "michael", RearCrossing());
            Require(report.checks.Count == report.expectedCases, "Not every companion scenario was executed.");
            Finish(null);
        }

        private object Call(string method, params object[] arguments)
        { return typeof(TowerGame).GetMethod(method, Hidden).Invoke(game, arguments); }
        private static object Member(object instance, string name)
        { return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(instance); }
        private int OwnedEntries(string field, string ownerId)
        {
            int count = 0;
            foreach (object entry in (IList)typeof(TowerGame).GetField(field, Hidden).GetValue(game))
            {
                object holder = field == "pendingSkills" ? Member(entry, "clock") : entry;
                var owner = Member(holder, "owner") as HeroState;
                if (owner != null && owner.Id == ownerId) count++;
            }
            return count;
        }
        private TowerGame.Fighter Setup(string id, bool airborne = false)
        {
            if (game.hero != null && game.hero.root) game.hero.root.SetActive(false);
            foreach (var actor in game.allies) if (actor.root) actor.root.SetActive(false);
            foreach (var actor in game.enemies) if (actor.root) actor.root.SetActive(false);
            game.SmokeControlled = true; game.NewRun(74);
            game.run = new TowerRun(74, TowerRun.DefaultMaxFloor, false);
            game.MoveOverride = 0; game.FocusOverride = false;
            game.AttackOverride = game.JumpOverride = game.DashOverride = game.DropOverride = false;
            Place(game.hero, new Vector2(-10, TowerWorld.Ground)); game.hero.facing = 1;
            var roster = (IList)typeof(TowerRun).GetField("companions", Hidden).GetValue(game.run);
            roster.Add(new CompanionState(TowerRun.GetDefinition(id))); game.ReconcileLoadout();
            Call("ResetEnemyCombat");
            foreach (var enemy in game.enemies) if (enemy.root) { enemy.root.SetActive(false); Destroy(enemy.root); }
            game.enemies.Clear();
            var companion = game.run.GetCompanion(id);
            var ally = game.allies.Find(actor => actor.id == id);
            Place(ally, game.FormationAnchor(companion));
            float distance = companion.Definition.Role == CompanionRole.Vanguard ? 1.1f : 4;
            var target = (TowerGame.Fighter)Call("Spawn", "Companion validation target", airborne ? 3 : 0, true,
                new Vector2(ally.pos.x + distance, TowerWorld.Ground));
            Call("ConfigureEnemy", target, airborne ? 3 : 0, 0);
            target.hp = target.maxHp = 100000; target.stunTime = target.cooldown = 999;
            game.enemies.Add(target); game.StartClimb(); game.enabled = true;
            return target;
        }
        private static void Place(TowerGame.Fighter actor, Vector2 position)
        { actor.pos = position; actor.velocityY = 0; actor.grounded = true; actor.root.transform.position = position; }
        private IEnumerator Until(Func<bool> predicate, float seconds, string failure)
        {
            float limit = Time.realtimeSinceStartup + seconds;
            while (!predicate())
            {
                Require(game && game.enabled && !game.Paused, "The production simulation stopped during a companion observation.");
                Require(Time.realtimeSinceStartup < limit, failure);
                yield return null;
            }
        }
        private IEnumerator Simulate(float seconds)
        {
            float until = game.elapsed + seconds, limit = Time.realtimeSinceStartup + seconds * 5 + 3;
            while (game.elapsed < until)
            { Require(Time.realtimeSinceStartup < limit && !game.Paused, "The fixture did not advance through production Update."); yield return null; }
        }
        private IEnumerator Capture(string suffix)
        { yield return capture.CaptureForSuite(prefix, suffix); report.screenshots.Add(prefix + "-" + suffix + ".png"); }

        private IEnumerator BasicAttack(string id, bool airborne)
        {
            var target = Setup(id, airborne); var state = game.run.GetCompanion(id);
            var actor = game.allies.Find(ally => ally.id == id); float before = target.hp;
            Require(state.Skills.Count == 0 && !game.AttackOverride, "A basic attack fixture unexpectedly had learned skills or player attacks.");
            yield return Until(() => actor.basicPending, airborne?12:4, "No production basic attack began for " + id);
            float started = game.elapsed; current.expectedReleaseDelay = actor.basicDelay;
            Require(target.hp == before && game.OwnerProjectiles(id) == 0, "Damage or a projectile appeared before the basic windup.");
            Require(actor.art.MotionName == "Attack", "The production basic attack did not play its attack motion.");
            yield return Until(() => !actor.basicPending, 3, "Basic windup never released for " + id);
            current.observedReleaseDelay = game.elapsed - started; actor.cooldown = 999;
            Require(Mathf.Abs(current.observedReleaseDelay - current.expectedReleaseDelay) <= .085f, "Basic release/contact delay did not match its animation timing.");
            bool ranged = state.Definition.Role != CompanionRole.Vanguard;
            current.projectileObserved = game.OwnerProjectiles(id) > 0;
            Require(current.projectileObserved == ranged, ranged ? "No real basic projectile appeared." : "A melee basic attack unexpectedly spawned a projectile.");
            yield return Until(() => target.hp < before, 3, "The actual basic attack never reduced target HP for " + id);
            current.hpLost = before - target.hp;
            Require(Mathf.Abs(current.hpLost - state.Damage) < .05f, "The first basic hit did not apply exactly the companion's current damage.");
            current.detail = ranged ? "Delayed production projectile appeared, travelled, and dealt one basic hit." : "Delayed production melee contact dealt one basic hit.";
            if ((!airborne && (id == "michael" || id == "gabriel" || id == "uriel" || id == "raphael")) || (airborne && id == "gabriel"))
                yield return Capture((airborne ? "airborne-" : "basic-") + id);
        }
        private IEnumerator LearnedSkill(string id, AugmentDefinition definition)
        {
            var target = Setup(id); var state = game.run.GetCompanion(id);
            var actor = game.allies.Find(ally => ally.id == id); actor.cooldown = 999;
            Place(target, actor.pos + Vector2.right * 1.1f);
            game.hero.hp = game.PlayerMaxHealth * .25f;
            float targetBefore = target.hp, heroBefore = game.hero.hp, shieldBefore = game.PlayerShield;
            Require(state.Skills.Count == 0 && game.SkillCasts(id, definition.Id) == 0, "Skill was active before learning: " + definition.Id);
            Require(state.Apply(definition), "Could not learn owned skill: " + definition.Id); game.ReconcileLoadout();
            yield return Until(() => game.SkillLastStartedAt(id, definition.Id) >= 0, 4, "Actual Update did not begin owned skill: " + definition.Id);
            Require(game.SkillCasts(id, definition.Id) == 0, "Owned skill released without its cast preparation: " + definition.Id);
            current.expectedReleaseDelay = actor.art.CastReleaseTime(definition.Effect, game.SkillCastDuration(id, definition.Id));
            yield return Until(() => game.SkillCasts(id, definition.Id) > 0, 3, "Actual Update did not release owned skill: " + definition.Id);
            current.observedReleaseDelay = game.SkillLastCastAt(id, definition.Id) - game.SkillLastStartedAt(id, definition.Id);
            Require(Mathf.Abs(current.observedReleaseDelay - current.expectedReleaseDelay) <= .085f, "Owned skill cast timing was incorrect: " + definition.Id);
            current.projectileObserved = game.OwnerProjectiles(id) > 0;
            if (definition.Effect == SkillEffect.HealPulse)
                Require(game.hero.hp > heroBefore, "Owned healing skill restored no health: " + definition.Id);
            else if (definition.Effect == SkillEffect.ShieldPulse)
                Require(game.PlayerShield > shieldBefore, "Owned shield skill gave no protection: " + definition.Id);
            else
            {
                yield return Until(() => target.hp < targetBefore, 4, "Owned skill dealt no actual damage: " + definition.Id);
                current.hpLost = targetBefore - target.hp;
                if (definition.Effect == SkillEffect.LifeDrain) Require(game.hero.hp > heroBefore, "Life drain did not heal its player: " + definition.Id);
                if (definition.Effect == SkillEffect.IceNova || definition.Effect == SkillEffect.PoisonCloud)
                    Require(target.slowTime > 0, "Owned status skill did not slow its target: " + definition.Id);
            }
            current.detail = "Actual Update learned and released " + definition.Effect + " for " + state.Definition.MythId + "; observed damage, healing or shield and production cast timing.";
        }
        private TowerGame.Fighter Learn(string augmentId)
        {
            var target = Setup("uriel"); var actor = game.allies.Find(ally => ally.id == "uriel"); actor.cooldown = 999;
            game.run.GetCompanion("uriel").Apply(TowerRun.GetAugment(augmentId)); game.ReconcileLoadout();
            return target;
        }
        private IEnumerator LearnedFireball()
        {
            var target = Setup("uriel"); var actor = game.allies.Find(ally => ally.id == "uriel"); actor.cooldown = 999;
            yield return Simulate(.4f);
            Require(game.SkillCasts("uriel", "uriel.skill1") == 0 && game.OwnerProjectiles("uriel") == 0, "A locked companion skill activated before selection.");
            var state = game.run.GetCompanion("uriel"); state.Apply(TowerRun.GetAugment("uriel.skill1")); game.ReconcileLoadout();
            var skill = state.GetSkill("uriel.skill1"); float before = target.hp;
            yield return Until(() => game.SkillLastStartedAt("uriel", "uriel.skill1") >= 0, 4, "The learned fireball did not begin through Update.");
            Require(game.SkillCasts("uriel", "uriel.skill1") == 0 && game.OwnerProjectiles("uriel") == 0 && target.hp == before, "Fireball released before its cast preparation.");
            current.expectedReleaseDelay = actor.art.CastReleaseTime(SkillEffect.Fireball, game.SkillCastDuration("uriel", "uriel.skill1"));
            yield return Until(() => game.SkillCasts("uriel", "uriel.skill1") > 0, 3, "Learned fireball did not release.");
            float first = game.SkillLastCastAt("uriel", "uriel.skill1");
            current.observedReleaseDelay = first - game.SkillLastStartedAt("uriel", "uriel.skill1");
            current.projectileObserved = game.OwnerProjectiles("uriel") > 0;
            Require(current.projectileObserved && Mathf.Abs(current.observedReleaseDelay - current.expectedReleaseDelay) <= .085f, "Learned fireball projectile/timing was incorrect.");
            yield return Until(() => target.hp < before, 3, "Learned fireball did not actually damage its target.");
            current.hpLost = before - target.hp;
            yield return Capture("learned-fireball");
            yield return Until(() => game.SkillCasts("uriel", "uriel.skill1") >= 2, skill.Cooldown + 5, "Learned fireball did not repeat after cooldown.");
            current.cooldown = skill.Cooldown; current.observedCastInterval = game.SkillLastCastAt("uriel", "uriel.skill1") - first;
            Require(current.observedCastInterval >= skill.Cooldown - .08f && current.observedCastInterval < skill.Cooldown + .25f, "Learned fireball repeated outside its actual cooldown interval.");
            current.detail = "Locked before selection; learned cast released a real damaging projectile twice at its configured cooldown.";
        }
        private IEnumerator DismissPendingBasic()
        {
            var target = Setup("michael"); var actor = game.allies.Find(ally => ally.id == "michael"); float before = target.hp;
            yield return Until(() => actor.basicPending, 4, "The pending basic-dismiss fixture never began an actual attack.");
            game.Dismiss("michael"); yield return Simulate(.9f);
            Require(target.hp == before && game.allies.Count == 0 && game.OwnerProjectiles("michael") == 0, "Dismissed pending basic attack still dealt damage.");
            current.detail = "Dismissal removed a real pending melee attacker before contact; no delayed damage followed.";
        }
        private IEnumerator DismissPendingSkill()
        {
            var target = Learn("uriel.skill1"); float before = target.hp;
            yield return Until(() => game.SkillLastStartedAt("uriel", "uriel.skill1") >= 0, 4, "The pending cast-dismiss fixture never began an actual cast.");
            Require(game.SkillCasts("uriel", "uriel.skill1") == 0 && OwnedEntries("pendingSkills", "uriel") == 1, "The cast was not pending at dismissal.");
            game.Dismiss("uriel"); yield return Simulate(1);
            Require(target.hp == before && game.OwnerProjectiles("uriel") == 0 && OwnedEntries("pendingSkills", "uriel") == 0, "Dismissal retained a pending learned cast or its damage.");
            current.detail = "Dismissal cancelled an actual learned cast before release.";
        }
        private IEnumerator DismissProjectile()
        {
            var target = Setup("gabriel"); float before = target.hp;
            yield return Until(() => game.OwnerProjectiles("gabriel") > 0, 4, "The projectile-dismiss fixture never released a real arrow.");
            Require(target.hp == before, "The arrow hit before its flight could be tested."); current.projectileObserved = true;
            game.Dismiss("gabriel");
            Require(game.OwnerProjectiles("gabriel") == 0, "Dismissal did not remove its airborne projectile immediately.");
            yield return Simulate(1); Require(target.hp == before, "A dismissed companion's arrow damaged its former target.");
            current.detail = "Dismissal removed a real travelling basic arrow before impact.";
        }
        private IEnumerator DismissArea()
        {
            var target = Learn("uriel.skill2"); float before = target.hp;
            yield return Until(() => OwnedEntries("skillAreas", "uriel") > 0 && target.hp < before, 5, "The real learned poison cloud never spawned and damaged its target.");
            current.hpLost = before - target.hp; game.Dismiss("uriel"); before = target.hp;
            Require(OwnedEntries("skillAreas", "uriel") == 0, "Dismissal left its active damage area behind.");
            yield return Simulate(1); Require(target.hp == before, "A dismissed companion's area kept dealing damage.");
            current.detail = "A real learned damaging cloud was removed; its next periodic hit never occurred.";
        }
        private IEnumerator RearCrossing()
        {
            var target = Setup("michael"); var actor = game.allies.Find(ally => ally.id == "michael"); float before = target.hp;
            yield return Until(() => actor.basicPending, 4, "Rear-crossing fixture never observed a real melee windup.");
            int attackFacing = actor.facing; current.expectedReleaseDelay = actor.basicDelay; float started = game.elapsed;
            Place(target, actor.pos + new Vector2(-attackFacing * 1.1f, 0));
            yield return Until(() => !actor.basicPending, 3, "Rear-crossing melee never reached its real contact frame.");
            actor.cooldown = 999; current.observedReleaseDelay = game.elapsed - started; current.hpLost = before - target.hp;
            yield return Capture("rear-crossing");
            Require(target.hp == before, "A target behind the locked melee direction was damaged: facing=" + attackFacing + ", HP lost=" + current.hpLost + ".");
            current.detail = "A target crossing behind during a real melee windup received no front-facing strike damage.";
        }

        private TowerGame.Fighter BoundaryTarget(string id)
        {
            var target = Setup(id); var state = game.run.GetCompanion(id);
            var actor = game.allies.Find(ally => ally.id == id);
            float distance = state.Definition.Role == CompanionRole.Vanguard ? 1.1f : 4;
            Place(target, new Vector2(TowerWorld.Right, TowerWorld.Ground));
            Place(actor, target.pos - Vector2.right * distance);
            Place(game.hero, actor.pos);
            return target;
        }
        private TowerGame.Fighter ExtraTarget(Vector2 position)
        {
            var target = (TowerGame.Fighter)Call("Spawn", "Companion movement validation target", 0, true, position);
            Call("ConfigureEnemy", target, 0, 1);
            target.hp = target.maxHp = 100000; target.stunTime = target.cooldown = 999;
            game.enemies.Add(target); return target;
        }
        private IEnumerator ObserveLock(TowerGame.Fighter actor, TowerGame.Fighter target, float lockedX, float seconds)
        {
            float until = game.elapsed + seconds, limit = Time.realtimeSinceStartup + seconds * 5 + 3;
            while (game.elapsed < until)
            {
                Require(Time.realtimeSinceStartup < limit && !game.Paused, "Target-lock observation stopped advancing through Update.");
                Require(ReferenceEquals(actor.combatTarget, target), "Player movement or a nearer enemy replaced a live combat target.");
                Require(Mathf.Abs(actor.pos.x - lockedX) < .025f, "A companion left a stationary target to follow player movement or teleported to formation.");
                yield return null;
            }
        }
        private IEnumerator TargetLock(string id)
        {
            var target = BoundaryTarget(id); var state = game.run.GetCompanion(id);
            var actor = game.allies.Find(ally => ally.id == id); float lockedX = actor.pos.x;
            yield return Until(() => actor.basicPending, 3, "Target-lock fixture did not start its actual basic attack.");
            float before = target.hp;
            game.MoveOverride = -1; yield return ObserveLock(actor, target, lockedX, .65f);
            game.MoveOverride = 1; yield return ObserveLock(actor, target, lockedX, .3f);
            game.MoveOverride = -1; game.DashOverride = true; yield return null; game.DashOverride = false;
            Require(game.dashCooldown > .5f, "The target-lock fixture did not execute a real player dash.");
            yield return ObserveLock(actor, target, lockedX, 3.3f); game.MoveOverride = 0;
            Require(Mathf.Abs(game.hero.pos.x - actor.pos.x) > 15, "The target-lock fixture never crossed the old follow teleport distance.");
            Require(target.hp <= before - state.Damage * 2, "Locked attacks stopped dealing actual damage while the player moved.");
            current.hpLost = before - target.hp;
            current.detail = "Actual Update: repeated hits continued at a fixed ally position during player movement, reversal, dash and separation above 15 units.";
        }
        private IEnumerator NearerEnemyKeepsLock()
        {
            var target = BoundaryTarget("gabriel"); var actor = game.allies.Find(ally => ally.id == "gabriel");
            yield return Until(() => actor.basicPending, 3, "Nearer-enemy fixture never acquired its first target.");
            var nearer = ExtraTarget(actor.pos - Vector2.right * .8f); float before = target.hp;
            yield return ObserveLock(actor, target, actor.pos.x, 2.8f);
            Require(target.hp < before && nearer.hp == nearer.maxHp, "A newly arrived nearer enemy stole subsequent basic attacks from the living target.");
            current.hpLost = before - target.hp;
            current.detail = "A new enemy on the opposite side stayed unharmed while subsequent real arrows continued damaging the original target.";
        }
        private IEnumerator TargetDeathRejoins()
        {
            var target = BoundaryTarget("michael"); var actor = game.allies.Find(ally => ally.id == "michael");
            var state = game.run.GetCompanion("michael");
            yield return Until(() => actor.basicPending, 3, "Rejoin fixture never locked its first target.");
            game.MoveOverride = -1; yield return ObserveLock(actor, target, actor.pos.x, 3); game.MoveOverride = 0;
            var other = ExtraTarget(new Vector2(16, TowerWorld.Ground));
            target.hp = 1; actor.cooldown = 0;
            yield return Until(() => target.hp <= 0 && !game.enemies.Contains(target), 3, "A real companion hit did not kill and remove the locked target.");
            Require(actor.returningToFormation && actor.combatTarget == null, "Target death did not enter formation return before acquiring another live enemy.");
            float previousX = actor.pos.x, previousTime = game.elapsed;
            float limit = Time.realtimeSinceStartup + 12;
            while (actor.combatTarget == null)
            {
                Require(Time.realtimeSinceStartup < limit && !game.Paused, "A companion never completed its formation return after a kill.");
                float dt = game.elapsed - previousTime;
                Require(Mathf.Abs(actor.pos.x - previousX) <= (state.MoveSpeed + game.PlayerMoveSpeed) * dt + .025f, "Formation return teleported or exceeded its ordinary movement speed.");
                previousX = actor.pos.x; previousTime = game.elapsed; yield return null;
            }
            Require(ReferenceEquals(actor.combatTarget, other) && Vector2.Distance(actor.pos, other.pos) < state.Range &&
                Mathf.Abs(actor.pos.x - game.hero.pos.x) > 5, "The returning companion ignored an enemy in range until it reached the player.");
            float attackX = actor.pos.x;
            yield return Until(() => other.hp < other.maxHp, 3, "The returning companion acquired an enemy but did not damage it.");
            Require(Mathf.Abs(actor.pos.x - attackX) < .025f, "The companion repositioned instead of attacking where it found the enemy.");
            current.detail = "A real kill cleared the lock; the ally returned without teleporting and stopped to damage an enemy in range before reaching the player.";
        }
        private IEnumerator PromptFollowJump()
        {
            var target = Setup("michael"); var actor = game.allies.Find(ally => ally.id == "michael");
            Place(target, new Vector2(TowerWorld.Right, TowerWorld.Ground));
            var world = (TowerWorld)Member(game, "world");
            var low = Array.Find(world.Ledges, ledge => ledge.y > TowerWorld.Ground + .65f && ledge.y < TowerWorld.Ground + 2 && ledge.width >= 2.4f);
            Place(game.hero, new Vector2(low.x, TowerWorld.Ground)); Place(actor, game.hero.pos);
            actor.combatTarget = null; actor.returningToFormation = true;
            game.JumpOverride = true; yield return null; game.JumpOverride = false;
            yield return Until(() => !actor.grounded && actor.velocityY > 0, .5f, "The follower waited for the player to land before jumping.");
            Require(!game.hero.grounded && game.hero.velocityY > 0, "The follower did not jump during the player's ascent.");
            yield return Until(() => game.hero.grounded && actor.grounded && Mathf.Abs(actor.pos.y - low.y) < .08f, 4, "The early follower jump missed the player's platform.");
            game.DropOverride = true; yield return null; game.DropOverride = false;
            yield return Until(() => game.hero.grounded && actor.grounded && Mathf.Abs(actor.pos.y - TowerWorld.Ground) < .08f, 4, "The follower did not descend after the player dropped through a shared platform.");
            current.detail = "The overlapping follower jumped during the player's ascent, landed on the same ledge, then followed a real drop-through input.";
        }
        private IEnumerator SharedPlatform()
        {
            var target = Setup("gabriel");
            Place(target, new Vector2(TowerWorld.Right, TowerWorld.Ground));
            var world = (TowerWorld)Member(game, "world");
            var low = Array.Find(world.Ledges, ledge => ledge.y > TowerWorld.Ground + .65f && ledge.y < TowerWorld.Ground + 2 && ledge.width >= 2.4f);
            var roster = (IList)typeof(TowerRun).GetField("companions", Hidden).GetValue(game.run);
            roster.Add(new CompanionState(TowerRun.GetDefinition("uriel")));
            roster.Add(new CompanionState(TowerRun.GetDefinition("raphael"))); game.ReconcileLoadout();
            Place(game.hero, new Vector2(low.x, low.y));
            foreach (var actor in game.allies) { Place(actor, game.hero.pos); actor.combatTarget = null; actor.returningToFormation = true; }
            yield return Simulate(1.4f);
            var spots = new List<float> { game.hero.pos.x };
            foreach (var actor in game.allies)
            {
                var slot = game.FormationAnchor(game.run.GetCompanion(actor.id));
                Require(actor.grounded && Mathf.Abs(actor.pos.y - low.y) < .08f && Mathf.Abs(actor.pos.x - slot.x) < .2f, "A follower did not settle into its own slot on the shared platform.");
                Require(Mathf.Abs(actor.pos.x - low.x) <= low.width * .5f, "A follower slot left the shared platform.");
                spots.Add(actor.pos.x);
            }
            spots.Sort(); for (int i = 1; i < spots.Count; i++) Require(spots[i] - spots[i - 1] > .25f, "The player and followers still overlap on the shared platform.");
            current.detail = "Three companions spread from the player's position into separate slots on one shared platform.";
        }
        private IEnumerator MovingJumpAndAttack()
        {
            var target = Setup("gabriel"); var actor = game.allies.Find(ally => ally.id == "gabriel");
            Place(target, new Vector2(TowerWorld.Right, TowerWorld.Ground));
            var world = (TowerWorld)Member(game, "world");
            var launch = Array.Find(world.Ledges, ledge => Mathf.Abs(ledge.x) < .1f && Mathf.Abs(ledge.y + 1.8f) < .08f);
            var landing = Array.Find(world.Ledges, ledge => Mathf.Abs(ledge.x - 4) < .1f && Mathf.Abs(ledge.y + .4f) < .08f);
            Require(launch.width > 0 && landing.width > 0, "The central-crown fixture is missing its gap jump supports.");
            Place(game.hero, new Vector2(launch.x, launch.y)); Place(actor, game.hero.pos);
            actor.combatTarget = null; actor.returningToFormation = true;
            game.MoveOverride = 1; game.JumpOverride = true; yield return null; game.JumpOverride = false;
            yield return Until(() => game.hero.pos.x >= landing.x - .5f, 3, "The moving player never reached the next platform.");
            game.MoveOverride = 0;
            yield return Until(() => !actor.grounded && actor.followNext >= 0, 3, "The follower did not launch across the platform gap.");
            var incoming = ExtraTarget(actor.pos + Vector2.right * .5f);
            yield return Until(() => actor.basicPending, 2, "The airborne follower did not attack an enemy entering range.");
            Require(!actor.grounded, "The gap fixture did not exercise attacking during flight.");
            Require(actor.moveVelocity > .1f, "Starting the airborne attack erased the follower's horizontal momentum.");
            yield return Until(() => actor.grounded, 3, "The attacking follower never landed.");
            Require(Mathf.Abs(actor.pos.y - landing.y) < .08f && Mathf.Abs(actor.pos.x - landing.x) < landing.width * .5f,
                "Starting an airborne attack cancelled horizontal steering and lost the landing platform.");
            yield return Until(() => incoming.hp < incoming.maxHp, 3, "The airborne attack did not deal damage.");
            current.detail = "Real rightward movement and jump crossed a platform gap; acquiring and attacking a new enemy in flight preserved horizontal landing steering and dealt damage.";
        }
        private IEnumerator GroundedSoles()
        {
            float worst = 0;
            for (int index = TowerMythArt.FirstArtIndex; index < TowerMythArt.FirstArtIndex + TowerMythArt.CharacterCount; index++)
            {
                var root = new GameObject("Temporary sole regression") { hideFlags = HideFlags.HideAndDontSave };
                root.transform.position = new Vector3(1000, 0, 0);
                try
                {
                    var art = TowerActorArt.Create(root.transform, index);
                    var rig = art.GetComponentInChildren<TowerMythRig>();
                    var eden = art.GetComponentInChildren<TowerEdenArt>();
                    var parts = rig != null ? rig.CapturePartIndices() : null;
                    for (int frame = 0; frame < 150; frame++)
                    {
                        float speed = frame < 20 || frame >= 130 ? 0 : frame < 65 ? 3 : 8;
                        int face = frame < 95 ? 1 : -1;
                        art.SetLocomotion(true, 0, false, speed, speed * face);
                        art.SetPose(speed > 0, false, false, face, 1f / 60);
                        if (eden != null)
                        {
                            float sole = eden.Renderer.bounds.min.y - root.transform.position.y;
                            worst = Mathf.Min(worst, sole);
                            Require(Mathf.Abs(sole) <= .003f, "Eden pixel sole left the floor: art=" + index + ", frame=" + frame + ", y=" + sole);
                            continue;
                        }
                        var vertices = rig.CaptureVertices();
                        for (int i = 0; i < vertices.Length; i++) if (parts[i] == 9 || parts[i] == 12)
                        {
                            worst = Mathf.Min(worst, vertices[i].y);
                            Require(vertices[i].y >= -.003f, "Rendered sole crossed the floor: art=" + index + ", frame=" + frame + ", y=" + vertices[i].y);
                        }
                    }
                }
                finally { Destroy(root); }
                yield return null;
            }
            current.detail = "All 50 companion meshes kept painted soles above the floor through idle, walk, run, reversal and stop; minimum y=" + worst + ".";
        }
        private IEnumerator SmoothFollowAndJump()
        {
            var target = Setup("michael"); var actor = game.allies.Find(ally => ally.id == "michael");
            var state = game.run.GetCompanion("michael"); Place(target, new Vector2(TowerWorld.Right, TowerWorld.Ground));
            game.MoveOverride = 1; float until = game.elapsed + .9f, firstSpeed = 0, peakSpeed = 0;
            while (game.elapsed < until)
            {
                if (firstSpeed == 0 && actor.moveVelocity > .01f) firstSpeed = actor.moveVelocity;
                peakSpeed = Mathf.Max(peakSpeed, actor.moveVelocity); yield return null;
            }
            game.MoveOverride = 0;
            Require(firstSpeed > 0 && firstSpeed < 1.5f && peakSpeed > 3, "Following began at full speed instead of accelerating gradually.");
            bool slowedGradually = false; float limit = Time.realtimeSinceStartup + 5;
            while (Mathf.Abs(actor.moveVelocity) > .02f || Mathf.Abs(actor.pos.x - game.FormationAnchor(state).x) > .15f)
            {
                Require(Time.realtimeSinceStartup < limit && !game.Paused, "The follower did not settle after player movement stopped.");
                if (actor.moveVelocity > .2f && actor.moveVelocity < peakSpeed * .7f) slowedGradually = true;
                yield return null;
            }
            Require(slowedGradually, "Following snapped to a stop without slowing down.");
            float settledX = actor.pos.x; yield return Simulate(.7f);
            Require(Mathf.Abs(actor.pos.x - settledX) < .025f && Mathf.Abs(actor.moveVelocity) < .02f, "A settled follower jittered or kept changing its movement speed.");
            var world = (TowerWorld)Member(game, "world");
            int lowIndex = Array.FindIndex(world.Ledges, ledge => ledge.y > TowerWorld.Ground + .65f && ledge.y < TowerWorld.Ground + 2 && ledge.width >= 2.4f);
            Require(lowIndex >= 0, "The selected layout has no directly reachable low platform for the jump fixture.");
            var low = world.Ledges[lowIndex];
            Place(game.hero, new Vector2(low.x, TowerWorld.Ground));
            Place(actor, game.FormationAnchor(state)); actor.followGroundY = TowerWorld.Ground;
            Require(Mathf.Abs(game.hero.pos.x - low.x) < low.width * .5f - .2f && Mathf.Abs(actor.pos.x - low.x) < low.width * .5f - .2f, "The selected low platform does not contain both jump anchors.");
            int jumps = 0; bool grounded = actor.grounded;
            game.JumpOverride = true; yield return null; game.JumpOverride = false;
            until = game.elapsed + 3;
            while (game.elapsed < until)
            {
                if (grounded && !actor.grounded && actor.velocityY > 0) jumps++;
                grounded = actor.grounded; yield return null;
            }
            Require(jumps == 1 && game.hero.grounded && actor.grounded && Mathf.Abs(game.hero.pos.y - low.y) < .08f && Mathf.Abs(actor.pos.y - low.y) < .08f && Mathf.Abs(actor.pos.x - game.FormationAnchor(state).x) < .15f, "Following failed to jump once and settle on the selected low platform; jumps=" + jumps + ".");
            current.detail = "Real player movement produced gradual acceleration/braking and a stable stop; one real player jump caused exactly one follower jump onto the selected low platform in " + world.CurrentPattern.Name + ".";
        }
        private IEnumerator HighPlatformRejoin()
        {
            var target = Setup("michael"); var actor = game.allies.Find(ally => ally.id == "michael");
            var state = game.run.GetCompanion("michael"); var world = (TowerWorld)Member(game, "world");
            int highIndex = Array.FindIndex(world.Ledges, ledge => Mathf.Abs(ledge.y - world.HighestPlatform) < .08f);
            Require(highIndex >= 0, "The selected layout has no top-tier platform for the rejoin fixture.");
            var high = world.Ledges[highIndex];
            Place(target, new Vector2(high.x > 5 ? TowerWorld.Left + 1 : TowerWorld.Right, TowerWorld.Ground));
            Place(game.hero, new Vector2(high.x, high.y)); Place(actor, new Vector2(high.x, TowerWorld.Ground));
            actor.followGroundY = TowerWorld.Ground;
            actor.returningToFormation = true;
            bool visitedIntermediateLedge = false, grounded = actor.grounded; int jumps = 0;
            float until = game.elapsed + 24, limit = Time.realtimeSinceStartup + 60;
            while (actor.returningToFormation || !actor.grounded || Mathf.Abs(actor.pos.y - high.y) > .08f || Mathf.Abs(actor.pos.x - game.FormationAnchor(state).x) > .6f)
            {
                Require(game.elapsed < until && Time.realtimeSinceStartup < limit && !game.Paused, "The follower could not rejoin the selected top-tier platform through reachable ledges.");
                Require(!actor.returningToFormation || actor.combatTarget == null, "An enemy interrupted the high-platform formation return.");
                if (grounded && !actor.grounded && actor.velocityY > 0) jumps++;
                grounded = actor.grounded;
                if (actor.grounded) foreach (var ledge in world.Ledges)
                    if (ledge.y > TowerWorld.Ground + .08f && ledge.y < high.y - .08f && Mathf.Abs(actor.pos.y - ledge.y) < .08f && Mathf.Abs(actor.pos.x - ledge.x) < ledge.width * .5f + .18f) visitedIntermediateLedge = true;
                Require(jumps <= 9, "The follower repeatedly jumped instead of taking a reachable route to the higher platform.");
                yield return null;
            }
            Require(visitedIntermediateLedge && jumps >= 6, "The follower reached the top-tier platform without jumping through the required intermediate supports.");
            Require(game.hero.grounded && actor.grounded && !actor.returningToFormation, "The high-platform return completed before both actors were grounded.");
            while (!actor.grounded || Mathf.Abs(actor.pos.y - high.y) > .08f || Mathf.Abs(actor.moveVelocity) > .02f || Mathf.Abs(actor.pos.x - game.FormationAnchor(state).x) > .15f)
            {
                Require(game.elapsed < until && Time.realtimeSinceStartup < limit && !game.Paused, "The high-platform follower did not finish braking: position=" + actor.pos.ToString("F3") + ", anchor=" + game.FormationAnchor(state).ToString("F3") + ", velocity=" + actor.moveVelocity.ToString("F3") + ", grounded=" + actor.grounded + ".");
                yield return null;
            }
            float settledX = actor.pos.x; yield return Simulate(.5f);
            Require(actor.grounded && Mathf.Abs(actor.pos.y - high.y) < .08f && Mathf.Abs(actor.pos.x - settledX) < .025f && Mathf.Abs(actor.moveVelocity) < .02f && actor.combatTarget == null, "The follower did not remain settled on the top-tier platform: position=" + actor.pos.ToString("F3") + ", anchor=" + game.FormationAnchor(state).ToString("F3") + ", x movement=" + (actor.pos.x - settledX).ToString("F3") + ", velocity=" + actor.moveVelocity.ToString("F3") + ", grounded=" + actor.grounded + ".");
            var roster = (IList)typeof(TowerRun).GetField("companions", Hidden).GetValue(game.run);
            roster.Clear();
            foreach (string id in new[] { "gabriel", "uriel", "raphael" }) roster.Add(new CompanionState(TowerRun.GetDefinition(id)));
            game.ReconcileLoadout();
            var slots = new List<float> { game.hero.pos.x };
            foreach (var companion in game.run.Companions)
            {
                var slot = game.FormationAnchor(companion); slots.Add(slot.x);
                Require(Mathf.Abs(slot.y - high.y) < .08f && Mathf.Abs(slot.x - high.x) <= high.width * .5f, "A follower slot left the narrow top-tier platform.");
            }
            slots.Sort(); for (int i = 1; i < slots.Count; i++) Require(slots[i] - slots[i - 1] > .2f, "Follower slots overlap on a narrow platform.");
            current.detail = "Actual Update routed a ground-level follower through intermediate ledges to the top-tier platform in " + world.CurrentPattern.Name + " in " + jumps + " jumps, settled after braking, and fitted three separate follower slots on the player's platform.";
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private static Exception Unwrap(Exception error)
        { return error is TargetInvocationException && error.InnerException != null ? error.InnerException : error; }
        private void Finish(Exception error)
        {
            if (finishing || IsFinished) return; finishing = true; Unsubscribe();
            if (error != null) report.errors.Add(Unwrap(error).Message);
            if (game)
            {
                game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = game.DropOverride = false;
                game.FocusOverride = false; game.menu = true; game.SmokeControlled = previousSmokeControlled;
            }
            report.finishedUtc = DateTime.UtcNow.ToString("o");
            report.success = Succeeded = report.errors.Count == 0 && report.checks.Count == report.expectedCases;
            FailureReason = report.errors.Count == 0 ? "" : String.Join("\n", report.errors.ToArray());
            try { File.WriteAllText(reportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false)); }
            catch (Exception writeError) { Succeeded = false; FailureReason += "\nCould not write report: " + writeError.Message; }
            IsFinished = true; enabled = false; if (quitOnFinish) Application.Quit(Succeeded ? 0 : 1);
        }
    }
}
