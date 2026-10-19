using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Opt-in fixtures exercise production simulation; ordinary players never enter this suite.
    public sealed class TowerCombatSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class CombatReport
        {
            public string mode = "towerCombatSmoke", finishedUtc;
            public bool success, formationForward, formationReversed, stableSlots, companionScale, movingFormationMaintained;
            public bool airborneTargetEligibility, airborneMeleeImmune, airborneRangedDamage, rangedOnlyAcquisition;
            public bool noRangedSpawnGuard, dismissedLastRangedLandsFlyer;
            public bool lockedAim, fanPattern, gapPattern, radialPattern, fixedBulletTrajectory;
            public bool stationaryBulletHit, jumpInputEvades, dashInputInvulnerable, sweptBulletHit, hostileBulletDeath, cameraStableOnHit, focusInputAndHitCore, readySkillWaitsForDash;
            public List<string> checks = new List<string>(), screenshots = new List<string>(), errors = new List<string>();
        }
        private readonly CombatReport report = new CombatReport();
        private TowerGame game; private TowerSmokeDriver capture;
        private string prefix, reportPath; private bool finishing, quitOnComplete = true;
        public bool IsFinished { get { return finishing; } }
        public bool Succeeded { get { return finishing && report.success; } }
        private float deadline;
        private static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private IList Shots { get { return (IList)typeof(TowerGame).GetField("shots", Hidden).GetValue(game); } }
        private object Call(string method, params object[] args)
        { return typeof(TowerGame).GetMethod(method, Hidden).Invoke(game, args); }
        private static T Field<T>(object value, string name)
        { return (T)value.GetType().GetField(name).GetValue(value); }
        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver)
        {
            string[] args = Environment.GetCommandLineArgs();
            Initialize(tower, captureDriver,
                Argument(args, "-towerCombatCapture") ?? Path.Combine(Application.persistentDataPath, "tower-combat"),
                Argument(args, "-towerCombatReport") ?? Path.Combine(Application.persistentDataPath, "tower-combat.json"), true);
        }
        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver, string capturePrefix, string outputReportPath, bool quitOnComplete = false)
        {
            if (game != null) throw new InvalidOperationException("Create a new combat driver for each suite run.");
            if (tower == null) throw new ArgumentNullException(nameof(tower));
            if (captureDriver == null) throw new ArgumentNullException(nameof(captureDriver));
            if (!Path.IsPathRooted(capturePrefix) || !Path.IsPathRooted(outputReportPath)) throw new ArgumentException("Suite capture and report paths must be absolute.");
            game = tower; capture = captureDriver; prefix = capturePrefix; reportPath = outputReportPath; this.quitOnComplete = quitOnComplete;
            if (!quitOnComplete) capture.PrepareForSuite(tower);
            game.SmokeControlled = true; enabled = true;
            Application.logMessageReceived += RecordLog; deadline = Time.realtimeSinceStartup + 180;
            StartCoroutine(Guard(Suite()));
        }
        private static string Argument(string[] args, string key)
        { int at = Array.IndexOf(args, key); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
        private void RecordLog(string condition, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(condition + "\n" + trace); }
        private void OnDestroy() { Application.logMessageReceived -= RecordLog; }
        private void Update()
        { if (!finishing && Time.realtimeSinceStartup > deadline) { StopAllCoroutines(); Finish(new TimeoutException("Combat suite exceeded 180 seconds.")); } }
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
            Require(Path.IsPathRooted(prefix) && Path.IsPathRooted(reportPath), "Absolute combat report paths are required.");
            Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            yield return null; yield return Formations(); yield return Airborne();
            yield return Patterns(); yield return ControlledDodge();
            Finish(null);
        }
        private void Setup(params string[] companions)
        {
            game.enabled = true;
            if (game.hero != null && game.hero.root) game.hero.root.SetActive(false);
            foreach (var actor in game.allies) if (actor.root) actor.root.SetActive(false);
            foreach (var actor in game.enemies) if (actor.root) actor.root.SetActive(false);
            game.NewRun(74); game.StartClimb(); game.MoveOverride = 0; game.FocusOverride = false; game.AttackOverride = game.JumpOverride = game.DashOverride = false;
            var list = (IList)typeof(TowerRun).GetField("companions", Hidden).GetValue(game.run);
            foreach (string id in companions) list.Add(new CompanionState(TowerRun.GetDefinition(id)));
            game.ReconcileLoadout(); ClearEnemies(); ClearShots();
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); game.hero.facing = 1;
            var sentinel = Spawn(25, TowerWorld.Ground); sentinel.stunTime = sentinel.cooldown = 999;
        }
        private void ClearEnemies()
        { foreach (var enemy in game.enemies) if (enemy.root) { enemy.root.SetActive(false); Destroy(enemy.root); } game.enemies.Clear(); }
        private void ClearShots()
        { foreach (object shot in Shots) { var root = Field<GameObject>(shot, "root"); if (root) Destroy(root); } Shots.Clear(); }
        private TowerGame.Fighter Spawn(float x, float y, int kind = 0)
        {
            var enemy = (TowerGame.Fighter)Call("Spawn", "Combat fixture", kind, true, new Vector2(x, y));
            game.enemies.Add(enemy); Call("ConfigureEnemy", enemy, kind, 0);
            enemy.maxHp = enemy.hp = 1000000; enemy.cooldown = 999; return enemy;
        }
        private static void Place(TowerGame.Fighter actor, Vector2 position)
        { actor.pos = position; actor.velocityY = 0; actor.grounded = true; actor.root.transform.position = position; }
        private IEnumerator Wait(float seconds)
        { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
        private IEnumerator Capture(string suffix, string message)
        {
            yield return null; yield return null;
            game.Notice(message, 5); yield return capture.CaptureForSuite(prefix, suffix);
            report.screenshots.Add(prefix + "-" + suffix + ".png");
        }
        private IEnumerator Formations()
        {
            Setup("rook", "finn", "sera"); yield return Wait(2.5f);
            var slots = new Dictionary<string, float>();
            foreach (var state in game.run.Companions)
            {
                float offset = game.FormationOffset(state); slots.Add(state.Id, offset);
                var actor = game.allies.Find(a => a.id == state.Id);
                Require(Mathf.Abs(actor.pos.x - game.FormationAnchor(state).x) < .5f, "A companion did not settle at its right-facing formation slot: " + state.Id);
                Require(Mathf.Abs(actor.art.transform.localScale.x - .82f) < .001f && Mathf.Abs(actor.root.transform.localScale.x - 1) < .001f, "Companion art scale must be .82 without shrinking physics.");
            }
            Require(slots["rook"] > 0 && slots["finn"] < 0 && slots["sera"] < slots["finn"], "Front/middle/rear role order was incorrect.");
            report.formationForward = report.companionScale = true;
            yield return Capture("formation-right", "진형 검증 · 전열 / 중열 / 후열");
            game.MoveOverride = -1; yield return Wait(.12f); game.MoveOverride = 0; yield return Wait(2.6f);
            Require(game.hero.facing == -1, "Movement input did not reverse the hero facing.");
            foreach (var state in game.run.Companions)
            {
                Require(slots[state.Id] == game.FormationOffset(state), "A stable formation slot changed after facing reversal.");
                Require(Mathf.Abs(game.allies.Find(a => a.id == state.Id).pos.x - game.FormationAnchor(state).x) < .5f, "A companion did not reverse with the hero: " + state.Id);
            }
            report.formationReversed = report.stableSlots = true;
            yield return Capture("formation-left", "진형 검증 · 이동 방향을 바꿔도 역할별 자리 유지");
            game.MoveOverride = -1; yield return Wait(2); game.MoveOverride = 0;
            foreach (var state in game.run.Companions) Require(Mathf.Abs(game.allies.Find(a => a.id == state.Id).pos.x - game.FormationAnchor(state).x) < .65f, "A companion fell behind the moving hero's formation: " + state.Id);
            report.movingFormationMaintained = true;
            report.checks.Add("Formation offsets and actual settled positions reverse with the hero; role slots stay stable and only ally artwork scales to .82.");
        }
        private IEnumerator Airborne()
        {
            Setup("rook", "finn"); ClearEnemies();
            game.enabled = false;
            var flyer = Spawn(2, TowerWorld.Ground + 1, 3); Place(flyer, new Vector2(2, TowerWorld.Ground + 1)); flyer.airborne = true; flyer.stunTime = 999;
            Require(!game.CanTarget(flyer, game.run.Player) && !game.CanTarget(flyer, game.run.GetCompanion("rook")) && game.CanTarget(flyer, game.run.GetCompanion("finn")), "Airborne target eligibility ignored the owner's combat role.");
            report.airborneTargetEligibility = true;
            // Keep geometry in melee reach so immunity must come from role eligibility, not vertical separation.
            float hp = flyer.hp; game.hero.facing = 1; Call("PlayerSlash");
            Fire(flyer.pos + Vector2.up, flyer.pos + Vector2.up, 0, 25, false, game.run.Player); Call("TickShots", .02f);
            Require(flyer.hp == hp, "Player melee or a player-owned projectile damaged an airborne monster."); report.airborneMeleeImmune = true;
            ClearShots(); Fire(flyer.pos + Vector2.up, flyer.pos + Vector2.up, 0, 25, false, game.run.GetCompanion("finn")); Call("TickShots", .02f);
            Require(flyer.hp < hp, "An eligible ranged companion could not hit an airborne monster."); report.airborneRangedDamage = true;
            ClearShots(); foreach (var ally in game.allies) { Place(ally, game.FormationAnchor(game.run.GetCompanion(ally.id))); ally.cooldown = 0; ally.basicPending = false; }
            Call("TickAllies", .02f);
            Require(!game.allies.Find(a => a.id == "rook").basicPending && game.allies.Find(a => a.id == "finn").basicPending, "A melee companion acquired an airborne-only target, or a ranged companion failed to acquire it.");
            report.rangedOnlyAcquisition = true;
            yield return Capture("airborne", "공중 적 검증 · 원거리 직업만 공격 가능");
            game.Dismiss("finn"); Require(!game.HasRangedCompanion && !flyer.airborne, "Dismissing the last ranged companion left an untouchable airborne target.");
            game.enabled = true; yield return Wait(1.2f);
            Require(flyer.pos.y <= TowerWorld.Ground + .1f && game.CanTarget(flyer, game.run.Player), "An unsupported flyer did not land and become melee-eligible.");
            report.dismissedLastRangedLandsFlyer = true;
            Setup(); ClearEnemies(); typeof(TowerRun).GetField("<Floor>k__BackingField", Hidden).SetValue(game.run, 2); Call("SpawnFloor");
            foreach (var enemy in game.enemies) Require(!enemy.airborne, "A party without ranged support spawned an untouchable airborne monster.");
            report.noRangedSpawnGuard = true;
            report.checks.Add("Airborne role gate checked at targeting and damage; player/Vanguard cannot hit, ranged can, and removing the last ranged member lands existing flyers.");
        }
        private void Fire(Vector2 from, Vector2 to, float speed, float damage, bool hostile, HeroState owner = null)
        { Call("Fire", from, to, speed, damage, hostile, Color.cyan, 0f, owner, 1, 0f); }
        private IEnumerator Patterns()
        {
            // Filled through the actual enemy telegraph/release APIs; input evasion remains a separate fixture below.
            yield return VerifyPatterns();
        }
        private IEnumerator VerifyPatterns()
        {
            string[] names = { "AimedFan", "GapWave", "RadialGap" };
            int[] counts = { 3, 4, 9 };
            for (int pattern = 0; pattern < names.Length; pattern++)
            {
                Setup(); ClearEnemies(); game.enabled = false;
                Place(game.hero, new Vector2(-4, TowerWorld.Ground));
                var shooter = Spawn(3, TowerWorld.Ground, 1);
                Call("ConfigureEnemy", shooter, 1, pattern); shooter.cooldown = 0;
                Call("TickEnemy", shooter, .01f);
                Require((string)Call("EnemyPatternName", shooter) == names[pattern], "The deterministic enemy pattern did not match its ordinal.");
                Vector2 locked = (Vector2)Call("EnemyLockedAim", shooter);
                Require((float)Call("EnemyTelegraphRemaining", shooter) > 0 && Vector2.Distance(locked, game.hero.pos) < .001f, "Enemy aim was not locked when its telegraph began.");
                Place(game.hero, new Vector2(-1, TowerWorld.Ground)); Call("TickEnemy", shooter, .1f);
                Require((Vector2)Call("EnemyLockedAim", shooter) == locked, "An enemy retargeted during a visible telegraph."); report.lockedAim = true;
                if (pattern == 0) yield return Capture("telegraph", "탄막 검증 · 예고 시점의 조준점 고정");
                for (int frame = 0; frame < 150 && (int)Call("EnemyReleaseCount", shooter) == 0; frame++) Call("TickEnemy", shooter, .02f);
                Require((int)Call("EnemyReleaseCount", shooter) == 1 && Shots.Count == counts[pattern], "Incorrect first volley count for " + names[pattern] + ": " + Shots.Count);
                Vector2 direction = (locked - shooter.pos).normalized;
                var angles = new List<float>(); var velocities = new Dictionary<object, Vector2>(); var positions = new Dictionary<object, Vector2>();
                foreach (object shot in Shots)
                { var velocity = Field<Vector2>(shot, "velocity"); angles.Add(Vector2.SignedAngle(direction, velocity)); velocities.Add(shot, velocity); positions.Add(shot, Field<Vector2>(shot, "pos")); }
                angles.Sort();
                if (pattern == 0)
                { Require(Mathf.Abs(angles[0] + 18) < .6f && Mathf.Abs(angles[1]) < .6f && Mathf.Abs(angles[2] - 18) < .6f, "Aimed fan spacing must be -18/0/+18 degrees."); report.fanPattern = true; }
                else
                {
                    float safe = pattern == 1 ? 18 : 34;
                    foreach (float angle in angles) Require(Mathf.Abs(angle) > safe, "The intentional safe sector was filled by a bullet: " + names[pattern]);
                    if (pattern == 1) report.gapPattern = true; else report.radialPattern = true;
                }
                Place(game.hero, new Vector2(-8, TowerWorld.Ground)); typeof(TowerGame).GetField("previousHeroPosition", Hidden).SetValue(game, game.hero.pos); Call("TickShots", .1f);
                foreach (object shot in Shots)
                {
                    Require(Field<Vector2>(shot, "velocity") == velocities[shot], "A hostile bullet homed after release.");
                    Require(Vector2.Distance(Field<Vector2>(shot, "pos"), positions[shot] + velocities[shot] * .1f) < .001f, "A hostile bullet did not follow its fixed linear path.");
                }
                report.fixedBulletTrajectory = true;
                if (pattern > 0)
                {
                    for (int frame = 0; frame < 40 && (int)Call("EnemyReleaseCount", shooter) < 2; frame++) Call("TickEnemy", shooter, .02f);
                    Require((int)Call("EnemyReleaseCount", shooter) == 2 && Shots.Count == (pattern == 1 ? 10 : 19), "Incorrect follow-up wave count for " + names[pattern]);
                    Require((Vector2)Call("EnemyLockedAim", shooter) == locked, "A follow-up wave retargeted the hero.");
                }
                report.checks.Add(names[pattern] + ": locked telegraph / first rays " + counts[pattern] + " / fixed trajectories / safe gap and follow-up verified.");
                if (pattern == 2)
                {
                    typeof(TowerGame).GetField("previousHeroPosition", Hidden).SetValue(game, game.hero.pos);
                    Call("TickShots", .55f); Call("TickEffects", .55f); game.view.transform.position = new Vector3(-2, 0, -10);
                    yield return Capture("bullets", "탄막 검증 · 빈틈이 있는 방사형 2연속 패턴");
                }
            }
            game.enabled = true;
        }
        private IEnumerator ControlledDodge()
        {
            Setup(); game.MoveOverride = 1; float start = game.hero.pos.x, at = Time.realtimeSinceStartup;
            yield return Wait(.25f); float normalSpeed = (game.hero.pos.x - start) / (Time.realtimeSinceStartup - at);
            start = game.hero.pos.x; at = Time.realtimeSinceStartup; game.FocusOverride = true;
            yield return Wait(.25f); float focusSpeed = (game.hero.pos.x - start) / (Time.realtimeSinceStartup - at);
            var hitCore = (GameObject)typeof(TowerGame).GetField("playerHitCore", Hidden).GetValue(game);
            Require(game.FocusActive && hitCore.activeInHierarchy && focusSpeed / normalSpeed > .32f && focusSpeed / normalSpeed < .58f, "Focus input did not slow movement to 45% and reveal the collision core.");
            Require(Mathf.Abs(TowerGame.PlayerBulletHitRadius - .24f) < .001f, "Player bullet collision core unexpectedly changed size."); report.focusInputAndHitCore = true;
            Setup(); yield return Wait(.3f); game.hero.hp = game.PlayerMaxHealth;
            Fire(new Vector2(4, TowerWorld.Ground + 1), new Vector2(-4, TowerWorld.Ground + 1), 8, 12, true);
            yield return Wait(.7f); Require(game.hero.hp < game.PlayerMaxHealth, "A stationary hero was not hit by the dodge control shot."); report.stationaryBulletHit = true;
            ClearShots(); game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0; Place(game.hero, new Vector2(0, TowerWorld.Ground));
            Fire(new Vector2(4, TowerWorld.Ground + 1), new Vector2(-4, TowerWorld.Ground + 1), 8, 12, true);
            game.JumpOverride = true; yield return null; game.JumpOverride = false;
            yield return Wait(.6f); Require(game.hero.hp == game.PlayerMaxHealth && game.hero.pos.y > TowerWorld.Ground + .6f, "Actual jump input failed to evade the same horizontal shot."); report.jumpInputEvades = true;
            yield return Capture("dodge", "탄막 검증 · 실제 점프 입력으로 수평 탄 회피");
            ClearShots(); Place(game.hero, new Vector2(0, TowerWorld.Ground)); game.hero.facing = 1; game.invulnerable = 0; game.dashCooldown = 0;
            game.AttackOverride = true; yield return null; game.AttackOverride = false;
            Require(game.attackTimer > 0, "The dash cancellation fixture did not begin an attack.");
            var skillTarget = Spawn(6, TowerWorld.Ground); skillTarget.stunTime = skillTarget.cooldown = 999;
            game.run.Player.Apply(TowerRun.GetAugment("player.fireball")); game.ReconcileLoadout();
            var clocks = (IDictionary)typeof(TowerGame).GetField("skillClocks", Hidden).GetValue(game);
            foreach (DictionaryEntry entry in clocks) entry.Value.GetType().GetField("remaining").SetValue(entry.Value, 0f);
            Fire(game.hero.pos + Vector2.up, game.hero.pos + Vector2.up + Vector2.right * 5, 19, 12, true);
            game.DashOverride = true; yield return null; game.DashOverride = false; yield return Wait(.08f);
            Require(game.dashTime > 0 && game.invulnerable > 0 && game.hero.hp == game.PlayerMaxHealth && game.attackTimer == 0 && game.hero.art.MotionName == "Dash", "Actual dash input did not interrupt an attack and protect the hero from an overlapping parallel shot."); report.dashInputInvulnerable = true;
            Require(game.SkillLastStartedAt("player", "player.fireball") < 0, "A ready automatic player skill began during manual dash and replaced its pose."); report.readySkillWaitsForDash = true;
            ((IList)typeof(HeroState).GetField("skills", Hidden).GetValue(game.run.Player)).Clear(); Call("ResetSkills");
            ClearShots(); game.dashTime = 0; game.invulnerable = 0; Place(game.hero, new Vector2(0, TowerWorld.Ground));
            Fire(new Vector2(-5, TowerWorld.Ground + 1), new Vector2(5, TowerWorld.Ground + 1), 2000, 12, true);
            yield return Wait(.2f);
            Require(game.hero.hp < game.PlayerMaxHealth, "A fast hostile shot tunneled through the hero between frames: pos=" + game.hero.pos + ", invuln=" + game.invulnerable + ", shots=" + Shots.Count); report.sweptBulletHit = true;
            game.enabled = false; ClearShots(); game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0;
            Place(game.hero, new Vector2(2, TowerWorld.Ground)); typeof(TowerGame).GetField("previousHeroPosition", Hidden).SetValue(game, new Vector2(-2, TowerWorld.Ground));
            Fire(new Vector2(0, TowerWorld.Ground + .9f), new Vector2(0, TowerWorld.Ground + .9f), 0, 12, true); Call("TickShots", .02f);
            Require(game.hero.hp < game.PlayerMaxHealth, "Relative swept collision missed a hero crossing a stationary hostile bullet.");
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); game.enabled = true;
            ClearShots(); game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0;
            yield return Wait(1.8f); VerifyCameraHitAgainstControl();
            ClearShots(); game.hero.hp = 1; game.invulnerable = 0;
            Fire(new Vector2(-5, TowerWorld.Ground + 1), new Vector2(5, TowerWorld.Ground + 1), 2000, 1000, true);
            yield return Wait(.2f);
            Require(game.hero.hp == 0 && !game.run.IsRewardPending && !game.run.IsFloorCleared, "A swept hostile shot failed to kill or death cleared the floor."); report.hostileBulletDeath = true;
            report.checks.Add("Identical production bullets hit a stationary hero and miss after actual JumpOverride input; DashOverride grants invulnerability; fast swept shots hit/kill; hit camera remains fixed.");
        }
        private void VerifyCameraHitAgainstControl()
        {
            bool wasEnabled = game.enabled, attack = game.AttackOverride, jump = game.JumpOverride, dash = game.DashOverride, focus = game.FocusOverride;
            float move = game.MoveOverride; game.enabled = false;
            game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = game.FocusOverride = false;
            try
            {
                Require(game.dashTime <= 0 && game.attackTimer <= 0 && game.hero.castRemaining <= 0, "Camera control fixture still had an active player action.");
                ClearShots(); game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0;
                Vector2 heroPosition = game.hero.pos; int facing = game.hero.facing;
                Vector3 start = game.view.transform.position; var control = new Vector3[15];
                float controlMaxMotion = 0;
                // Both paths run in this same Unity frame, using the real Update and identical frame delta.
                // Comparing paths allows ordinary camera tracking to settle without duplicating its formula.
                for (int i = 0; i < control.Length; i++)
                {
                    Call("Update"); control[i] = game.view.transform.position;
                    controlMaxMotion = Mathf.Max(controlMaxMotion, Vector3.Distance(start, control[i]));
                }
                Require(game.hero.hp == game.PlayerMaxHealth && game.hero.pos == heroPosition && game.hero.facing == facing,
                    "The no-hit camera control did not keep its stationary hero unchanged.");
                game.view.transform.position = start; game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0;
                Fire(game.hero.pos + Vector2.up, game.hero.pos + Vector2.up, 0, 12, true);
                float maxPathError = 0;
                for (int i = 0; i < control.Length; i++)
                {
                    Call("Update"); maxPathError = Mathf.Max(maxPathError, Vector3.Distance(control[i], game.view.transform.position));
                }
                float damage = game.PlayerMaxHealth - game.hero.hp;
                report.checks.Add("Paired camera control: samples=" + control.Length + ", frameDelta=" + Time.unscaledDeltaTime.ToString("F6") +
                    ", noHitMaxMotion=" + controlMaxMotion.ToString("F6") + ", hitPathMaxError=" + maxPathError.ToString("F6") + ", actualDamage=" + damage.ToString("F3"));
                Require(damage > 0 && game.hero.pos == heroPosition && game.hero.facing == facing, "Camera hit path did not damage its stationary hero.");
                Require(maxPathError <= .00001f, "A hit changed the camera path relative to its no-hit control."); report.cameraStableOnHit = true;
            }
            finally
            {
                game.enabled = wasEnabled; game.MoveOverride = move;
                game.AttackOverride = attack; game.JumpOverride = jump; game.DashOverride = dash; game.FocusOverride = focus;
            }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void Finish(Exception error)
        {
            if (finishing) return; finishing = true;
            if (error != null) report.errors.Add(error is TargetInvocationException && error.InnerException != null ? error.InnerException.ToString() : error.ToString());
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.success = error == null && report.errors.Count == 0;
            game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = false;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= RecordLog;
            if (quitOnComplete) Application.Quit(report.success ? 0 : 1);
        }
    }
}
