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
        private bool initialized, finishing, subscribed, previousSmokeControlled;
        private float deadline;
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver, string prefix, string reportPath)
        {
            if (initialized) throw new InvalidOperationException("This companion suite was already initialized.");
            if (!tower || !captureDriver) throw new ArgumentException("A live tower and capture driver are required.");
            this.prefix = AbsoluteFilePath(prefix, false);
            this.reportPath = AbsoluteFilePath(reportPath, true);
            Directory.CreateDirectory(Path.GetDirectoryName(this.prefix));
            Directory.CreateDirectory(Path.GetDirectoryName(this.reportPath));
            game = tower; capture = captureDriver; initialized = true;
            previousSmokeControlled = game.SmokeControlled;
            report.startedUtc = DateTime.UtcNow.ToString("o");
            report.basicCompanions = TowerRun.Definitions.Count;
            report.expectedCases = TowerRun.Definitions.Count + 10;
            report.fixtureAdjustments.Add("Each case starts a fresh seeded run and inserts the specified companion into the roster; random recruitment cards are not under test.");
            report.fixtureAdjustments.Add("Player movement, jump, dash and attack inputs are disabled. One production-spawned target has 100000 HP and a long stun/cooldown, so enemy attacks cannot affect the observation.");
            report.fixtureAdjustments.Add("Basic attacks run through the normal enabled TowerGame.Update. No fixture calls Fire, StrikeEnemy, DamageEnemy, BeginAllyAttack or ResolveAllyAttack.");
            report.fixtureAdjustments.Add("The first basic attack is isolated by setting its recovery cooldown high after release. Learned-skill cases suppress basic attacks and apply the named learned-skill definition directly.");
            report.fixtureAdjustments.Add("The rear-crossing regression moves the target behind a real pending melee attack. Dismissal cases call the production Dismiss method. The suite leaves the fixture paused and never quits the application.");
            Application.logMessageReceived += RecordLog; subscribed = true;
            deadline = Time.realtimeSinceStartup + 210;
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
                Finish(new InvalidOperationException(!game ? "The tower was destroyed during the suite." : "Companion suite exceeded its lifetime or 210-second deadline."));
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
            foreach (var definition in TowerRun.Definitions)
                yield return Case("basic:" + definition.Id, definition.Id, BasicAttack(definition.Id, false));
            foreach (string id in new[] { "rook", "finn", "sera", "mira" })
                yield return Case("airborne:" + id, id, BasicAttack(id, true));
            yield return Case("learned-fireball", "sera", LearnedFireball());
            yield return Case("dismiss-pending-basic", "rook", DismissPendingBasic());
            yield return Case("dismiss-pending-skill", "sera", DismissPendingSkill());
            yield return Case("dismiss-projectile", "finn", DismissProjectile());
            yield return Case("dismiss-area", "sera", DismissArea());
            yield return Case("melee-target-crosses-behind", "rook", RearCrossing());
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
            game.NewRun(74); game.SmokeControlled = true;
            game.MoveOverride = 0; game.FocusOverride = false;
            game.AttackOverride = game.JumpOverride = game.DashOverride = false;
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
            if (airborne && state.Definition.Role == CompanionRole.Vanguard)
            {
                yield return Simulate(state.AttackInterval * 1.7f);
                Require(!actor.basicPending && target.hp == before && game.OwnerProjectiles(id) == 0, "A Vanguard acquired or damaged an airborne target.");
                current.detail = "Actual Update: melee never acquired the airborne-only target and caused no damage.";
                yield break;
            }
            yield return Until(() => actor.basicPending, 4, "No production basic attack began for " + id);
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
            if ((!airborne && (id == "rook" || id == "finn" || id == "sera" || id == "mira")) || (airborne && id == "finn"))
                yield return Capture((airborne ? "airborne-" : "basic-") + id);
        }
        private TowerGame.Fighter Learn(string augmentId)
        {
            var target = Setup("sera"); var actor = game.allies.Find(ally => ally.id == "sera"); actor.cooldown = 999;
            game.run.GetCompanion("sera").Apply(TowerRun.GetAugment(augmentId)); game.ReconcileLoadout();
            return target;
        }
        private IEnumerator LearnedFireball()
        {
            var target = Setup("sera"); var actor = game.allies.Find(ally => ally.id == "sera"); actor.cooldown = 999;
            yield return Simulate(.4f);
            Require(game.SkillCasts("sera", "sera.skill1") == 0 && game.OwnerProjectiles("sera") == 0, "A locked companion skill activated before selection.");
            var state = game.run.GetCompanion("sera"); state.Apply(TowerRun.GetAugment("sera.skill1")); game.ReconcileLoadout();
            var skill = state.GetSkill("sera.skill1"); float before = target.hp;
            yield return Until(() => game.SkillLastStartedAt("sera", "sera.skill1") >= 0, 4, "The learned fireball did not begin through Update.");
            Require(game.SkillCasts("sera", "sera.skill1") == 0 && game.OwnerProjectiles("sera") == 0 && target.hp == before, "Fireball released before its cast preparation.");
            current.expectedReleaseDelay = actor.art.CastReleaseTime(SkillEffect.Fireball, game.SkillCastDuration("sera", "sera.skill1"));
            yield return Until(() => game.SkillCasts("sera", "sera.skill1") > 0, 3, "Learned fireball did not release.");
            float first = game.SkillLastCastAt("sera", "sera.skill1");
            current.observedReleaseDelay = first - game.SkillLastStartedAt("sera", "sera.skill1");
            current.projectileObserved = game.OwnerProjectiles("sera") > 0;
            Require(current.projectileObserved && Mathf.Abs(current.observedReleaseDelay - current.expectedReleaseDelay) <= .085f, "Learned fireball projectile/timing was incorrect.");
            yield return Until(() => target.hp < before, 3, "Learned fireball did not actually damage its target.");
            current.hpLost = before - target.hp;
            yield return Capture("learned-fireball");
            yield return Until(() => game.SkillCasts("sera", "sera.skill1") >= 2, skill.Cooldown + 5, "Learned fireball did not repeat after cooldown.");
            current.cooldown = skill.Cooldown; current.observedCastInterval = game.SkillLastCastAt("sera", "sera.skill1") - first;
            Require(current.observedCastInterval >= skill.Cooldown - .08f && current.observedCastInterval < skill.Cooldown + .25f, "Learned fireball repeated outside its actual cooldown interval.");
            current.detail = "Locked before selection; learned cast released a real damaging projectile twice at its configured cooldown.";
        }
        private IEnumerator DismissPendingBasic()
        {
            var target = Setup("rook"); var actor = game.allies.Find(ally => ally.id == "rook"); float before = target.hp;
            yield return Until(() => actor.basicPending, 4, "The pending basic-dismiss fixture never began an actual attack.");
            game.Dismiss("rook"); yield return Simulate(.9f);
            Require(target.hp == before && game.allies.Count == 0 && game.OwnerProjectiles("rook") == 0, "Dismissed pending basic attack still dealt damage.");
            current.detail = "Dismissal removed a real pending melee attacker before contact; no delayed damage followed.";
        }
        private IEnumerator DismissPendingSkill()
        {
            var target = Learn("sera.skill1"); float before = target.hp;
            yield return Until(() => game.SkillLastStartedAt("sera", "sera.skill1") >= 0, 4, "The pending cast-dismiss fixture never began an actual cast.");
            Require(game.SkillCasts("sera", "sera.skill1") == 0 && OwnedEntries("pendingSkills", "sera") == 1, "The cast was not pending at dismissal.");
            game.Dismiss("sera"); yield return Simulate(1);
            Require(target.hp == before && game.OwnerProjectiles("sera") == 0 && OwnedEntries("pendingSkills", "sera") == 0, "Dismissal retained a pending learned cast or its damage.");
            current.detail = "Dismissal cancelled an actual learned cast before release.";
        }
        private IEnumerator DismissProjectile()
        {
            var target = Setup("finn"); float before = target.hp;
            yield return Until(() => game.OwnerProjectiles("finn") > 0, 4, "The projectile-dismiss fixture never released a real arrow.");
            Require(target.hp == before, "The arrow hit before its flight could be tested."); current.projectileObserved = true;
            game.Dismiss("finn");
            Require(game.OwnerProjectiles("finn") == 0, "Dismissal did not remove its airborne projectile immediately.");
            yield return Simulate(1); Require(target.hp == before, "A dismissed companion's arrow damaged its former target.");
            current.detail = "Dismissal removed a real travelling basic arrow before impact.";
        }
        private IEnumerator DismissArea()
        {
            var target = Learn("sera.skill2"); float before = target.hp;
            yield return Until(() => OwnedEntries("skillAreas", "sera") > 0 && target.hp < before, 5, "The real learned poison cloud never spawned and damaged its target.");
            current.hpLost = before - target.hp; game.Dismiss("sera"); before = target.hp;
            Require(OwnedEntries("skillAreas", "sera") == 0, "Dismissal left its active damage area behind.");
            yield return Simulate(1); Require(target.hp == before, "A dismissed companion's area kept dealing damage.");
            current.detail = "A real learned damaging cloud was removed; its next periodic hit never occurred.";
        }
        private IEnumerator RearCrossing()
        {
            var target = Setup("rook"); var actor = game.allies.Find(ally => ally.id == "rook"); float before = target.hp;
            yield return Until(() => actor.basicPending, 4, "Rear-crossing fixture never observed a real melee windup.");
            int attackFacing = actor.facing; current.expectedReleaseDelay = actor.basicDelay; float started = game.elapsed;
            Place(target, actor.pos + new Vector2(-attackFacing * 1.1f, 0));
            yield return Until(() => !actor.basicPending, 3, "Rear-crossing melee never reached its real contact frame.");
            actor.cooldown = 999; current.observedReleaseDelay = game.elapsed - started; current.hpLost = before - target.hp;
            yield return Capture("rear-crossing");
            Require(target.hp == before, "A target behind the locked melee direction was damaged: facing=" + attackFacing + ", HP lost=" + current.hpLost + ".");
            current.detail = "A target crossing behind during a real melee windup received no front-facing strike damage.";
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
                game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = false;
                game.FocusOverride = false; game.menu = true; game.SmokeControlled = previousSmokeControlled;
            }
            report.finishedUtc = DateTime.UtcNow.ToString("o");
            report.success = Succeeded = report.errors.Count == 0 && report.checks.Count == report.expectedCases;
            FailureReason = report.errors.Count == 0 ? "" : String.Join("\n", report.errors.ToArray());
            try { File.WriteAllText(reportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false)); }
            catch (Exception writeError) { Succeeded = false; FailureReason += "\nCould not write report: " + writeError.Message; }
            IsFinished = true; enabled = false;
        }
    }
}
