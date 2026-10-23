using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Separate opt-in boss scenario. Loadout/placement fixtures are recorded; all fight damage uses normal inputs.
    public sealed class TowerBossSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class BossReport
        {
            public string mode = "towerBossSmoke", finishedUtc;
            public bool success, floorCountsMonotone, regularCountsMatch, bossCadence, bossesGroundTargetable, regularHealthScaling, regularDamageScaling, regularMovementScaling, regularBulletScaling, regularAttackRateScaling, bossHealthScaling;
            public bool patternsDistinct, aimLocked, preparationBeforeDamage, preparationHasNoOverlays, patternDelays, crownGap, spiralGap, slamCollision, attackMotionsDistinct, pauseFreezesBoss, resetCleansBoss;
            public bool lockedAimAfterKnockback;
            public bool actualBossKilled, bossDeathWaitsForAdds, actualAddsKilled, rewardExactlyOnce, cameraNoShake;
            public int sampledFloors, sampledBosses, actualCombatKills, healthRefills;
            public float restoredHealth;
            public List<string> floorSamples = new List<string>(), statSamples = new List<string>(), patternChecks = new List<string>(), screenshots = new List<string>(), errors = new List<string>();
            public List<string> knockbackChecks = new List<string>();
        }
        private readonly BossReport report = new BossReport();
        private TowerGame game; private TowerSmokeDriver capture;
        private string prefix, reportPath; private bool finishing, battleActive, quitOnComplete = true; private float deadline;
        public bool IsFinished { get { return finishing; } }
        public bool Succeeded { get { return finishing && report.success; } }
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private IList Shots { get { return (IList)typeof(TowerGame).GetField("shots", Hidden).GetValue(game); } }
        private object Call(string name, params object[] values) { return typeof(TowerGame).GetMethod(name, Hidden).Invoke(game, values); }
        private static T Field<T>(object value, string name) { return (T)value.GetType().GetField(name).GetValue(value); }
        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver)
        {
            string[] args = Environment.GetCommandLineArgs();
            Initialize(tower, captureDriver,
                Arg(args, "-towerBossCapture") ?? Path.Combine(Application.persistentDataPath, "tower-boss"),
                Arg(args, "-towerBossReport") ?? Path.Combine(Application.persistentDataPath, "tower-boss.json"), true);
        }
        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver, string capturePrefix, string outputReportPath, bool quitOnComplete = false)
        {
            if (game != null) throw new InvalidOperationException("Create a new boss driver for each suite run.");
            if (tower == null) throw new ArgumentNullException(nameof(tower));
            if (captureDriver == null) throw new ArgumentNullException(nameof(captureDriver));
            if (!Path.IsPathRooted(capturePrefix) || !Path.IsPathRooted(outputReportPath)) throw new ArgumentException("Suite capture and report paths must be absolute.");
            game = tower; capture = captureDriver; prefix = capturePrefix; reportPath = outputReportPath; this.quitOnComplete = quitOnComplete;
            if (!quitOnComplete) capture.PrepareForSuite(tower);
            game.SmokeControlled = true; enabled = true;
            Application.logMessageReceived += RecordLog; deadline = Time.realtimeSinceStartup + 240;
            StartCoroutine(Guard(Suite()));
        }
        private static string Arg(string[] args, string key) { int at = Array.IndexOf(args, key); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
        private void RecordLog(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message + "\n" + trace); }
        private void OnDestroy() { Application.logMessageReceived -= RecordLog; }
        private void Update()
        {
            if (finishing) return;
            if (Time.realtimeSinceStartup > deadline) { StopAllCoroutines(); Finish(new TimeoutException("Boss suite exceeded 240 seconds.")); return; }
            if (battleActive && game.hero.hp > 0 && game.hero.hp < game.PlayerMaxHealth)
            { report.healthRefills++; report.restoredHealth += game.PlayerMaxHealth - game.hero.hp; game.hero.hp = game.PlayerMaxHealth; }
        }
        private IEnumerator Guard(IEnumerator body)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(body);
            while (stack.Count > 0)
            {
                bool more = false; object value = null; Exception error = null;
                try { more = stack.Peek().MoveNext(); if (more) value = stack.Peek().Current; } catch (Exception ex) { error = ex; }
                if (error != null) { Finish(error); yield break; }
                if (!more) { stack.Pop(); continue; } var nested = value as IEnumerator;
                if (nested != null) stack.Push(nested); else yield return value;
            }
        }
        private IEnumerator Suite()
        {
            Require(Path.IsPathRooted(prefix) && Path.IsPathRooted(reportPath), "Boss output paths must be absolute."); Directory.CreateDirectory(Path.GetDirectoryName(prefix));
            yield return FloorsAndStats(); yield return PatternsAndMotions(); yield return ActualFight();
            foreach (var field in typeof(BossReport).GetFields()) if (field.FieldType == typeof(bool) && field.Name != "success") Require((bool)field.GetValue(report), "Boss check was not completed: " + field.Name);
            Finish(null);
        }
        private void SetFloor(int floor)
        {
            game.enabled = false; battleActive = false;
            if (game.hero != null && game.hero.root) game.hero.root.SetActive(false);
            foreach (var actor in game.enemies) if (actor.root) actor.root.SetActive(false);
            foreach (var actor in game.allies) if (actor.root) actor.root.SetActive(false);
            game.NewRun(74); game.StartClimb(); ResetControls(); ClearEnemies();
            typeof(TowerRun).GetField("<Floor>k__BackingField", Hidden).SetValue(game.run, floor); Call("SpawnFloor");
            game.view.transform.position = new Vector3(0, 0, -10);
        }
        private void ResetControls() { game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DropOverride = game.DashOverride = game.FocusOverride = false; }
        private void ClearEnemies()
        { foreach (var actor in game.enemies) if (actor.root) { actor.root.SetActive(false); Destroy(actor.root); } game.enemies.Clear(); }
        private void ClearShots()
        { foreach (object shot in Shots) { var root = Field<GameObject>(shot, "root"); if (root) { root.SetActive(false); Destroy(root); } } Shots.Clear(); }
        private void Place(TowerGame.Fighter actor, Vector2 position)
        { actor.pos = position; actor.velocityY = 0; actor.grounded = true; actor.root.transform.position = position; if (actor == game.hero) typeof(TowerGame).GetField("previousHeroPosition", Hidden).SetValue(game, position); }
        private IEnumerator Wait(float seconds) { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
        private IEnumerator Capture(string name, string message)
        { yield return null; yield return null; game.Notice(message, 5); yield return capture.CaptureForSuite(prefix, name); report.screenshots.Add(prefix + "-" + name + ".png"); }
        private TowerGame.Fighter Boss { get { return game.enemies.Find(e => e.isBoss); } }
        private IEnumerator FloorsAndStats()
        {
            int previousRegular = 0;
            for (int floor = 1; floor <= 25; floor++)
            {
                SetFloor(floor); int regular = 0, bosses = 0, flyers=0;
                foreach (var enemy in game.enemies)
                {
                    Require(enemy.difficultyFloor == floor, "An enemy used the wrong difficulty floor.");
                    if (enemy.isBoss)
                    {
                        bosses++; Require(!enemy.airborne && enemy.grounded && Mathf.Abs(enemy.maxHp - TowerDifficulty.BossHealth(floor) * enemy.monster.HealthMultiplier) < .01f, "Boss HP/ground state did not match the floor and theme.");
                        Require(game.CanTarget(enemy, game.run.Player), "A melee-only player cannot target a boss.");
                        foreach (string id in new[] { "michael", "gabriel", "uriel", "raphael" }) Require(game.CanTarget(enemy, new CompanionState(TowerRun.GetDefinition(id))), "A companion class cannot target the grounded boss.");
                    }
                    else
                    {
                        if(enemy.airborne)flyers++;else regular++;
                        var monster = enemy.monster;
                        float hp = (enemy.kind == 0 ? 60 : enemy.kind == 1 ? 48 : enemy.kind == 2 ? 80 : 52) * TowerDifficulty.HealthMultiplier(floor) * monster.HealthMultiplier;
                        Require(Mathf.Abs(enemy.maxHp - hp) < .01f, "A normal enemy's actual HP did not scale.");
                        Require(Mathf.Abs(enemy.damageScale - TowerDifficulty.DamageMultiplier(floor) * monster.DamageMultiplier) < .001f && Mathf.Abs(enemy.moveScale - TowerDifficulty.MoveMultiplier(floor) * monster.MoveMultiplier) < .001f && Mathf.Abs(enemy.bulletSpeedScale - TowerDifficulty.BulletMultiplier(floor) * monster.BulletMultiplier) < .001f && Mathf.Abs(enemy.attackRateScale - TowerDifficulty.AttackRateMultiplier(floor) * monster.AttackRateMultiplier) < .001f, "A normal enemy's runtime multipliers did not match difficulty and species.");
                    }
                }
                Require(regular == TowerDifficulty.RegularCount(floor) && regular >= previousRegular, "Actual regular floor counts decreased or did not match difficulty.");
                Require(flyers==(floor>=2?1:0), "Flying monster count depended on the party or floor cadence.");
                Require(bosses == (floor % 5 == 0 ? 1 : 0), "Boss cadence did not match multiples of five: " + floor);
                report.sampledFloors++; report.sampledBosses += bosses; report.floorSamples.Add(floor + ": regular=" + regular + ", flying="+flyers+", boss=" + bosses + ", total=" + game.enemies.Count);
                previousRegular = regular; yield return null;
            }
            report.floorCountsMonotone = report.regularCountsMatch = report.bossCadence = report.bossesGroundTargetable = report.regularHealthScaling = report.bossHealthScaling = true;
            float[] first = MeasureRegular(1), final = MeasureRegular(25);
            for (int i = 0; i < 4; i++) Require(final[i] > first[i], "A measured normal combat stat did not increase: " + i);
            Require(Mathf.Abs(final[4] - first[4]) < .001f && final[5] < first[5], "Higher-floor normal attacks must keep preparation time and recover faster.");
            report.regularDamageScaling = report.regularMovementScaling = report.regularBulletScaling = report.regularAttackRateScaling = true;
        }
        private float[] MeasureRegular(int floor)
        {
            SetFloor(floor); Place(game.hero, new Vector2(0, TowerWorld.Ground)); var actor = game.enemies.Find(e => !e.isBoss && e.kind == 0);
            actor.monster = game.run.Theme.Monsters[0];
            Place(actor, new Vector2(1, TowerWorld.Ground)); Call("ConfigureEnemy", actor, 0, 0); actor.windup = .001f; actor.strikePoint = game.hero.pos; game.invulnerable = 0;
            float before = game.hero.hp; Call("TickEnemy", actor, .01f); float damage = before - game.hero.hp;
            Place(game.hero, new Vector2(4, TowerWorld.Ground)); Place(actor, new Vector2(-4, TowerWorld.Ground)); actor.cooldown = 999; actor.windup = 0;
            float x = actor.pos.x; Call("TickEnemy", actor, .1f); float movement = Mathf.Abs(actor.pos.x - x) / .1f;
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); Place(actor, new Vector2(4, TowerWorld.Ground)); Call("ConfigureEnemy", actor, 1, 0); actor.cooldown = 0; ClearShots();
            Call("TickEnemy", actor, .01f); float preparation = actor.windup, interval = actor.cooldown;
            for (int step = 0; step < 200 && Shots.Count == 0; step++) Call("TickEnemy", actor, .02f);
            Require(Shots.Count == 3, "Normal scaling sample did not release its actual fan.");
            float bullet = Field<Vector2>(Shots[0], "velocity").magnitude, bulletDamage = Field<float>(Shots[0], "damage");
            report.statSamples.Add(floor + ": meleeDamage=" + damage + ", move=" + movement + ", bulletSpeed=" + bullet + ", bulletDamage=" + bulletDamage + ", preparation=" + preparation + ", interval=" + interval);
            return new[] { damage, movement, bullet, bulletDamage, preparation, interval };
        }
        private IEnumerator PatternsAndMotions()
        {
            yield return VerifyBossPatterns(); yield return VerifyLockedAimAfterKnockback(); yield return PauseResetAndCamera();
        }
        private void RequireNoPreparationOverlay(HashSet<SpriteRenderer> before)
        {
            foreach (var renderer in game.GetComponentsInChildren<SpriteRenderer>(true))
                Require(before.Contains(renderer), "A boss created a path or danger renderer during preparation: " + renderer.name);
        }
        private void RequireNoPreviewObjects()
        {
            foreach (var item in game.GetComponentsInChildren<Transform>(true))
                Require(item.name != "Locked warning dash" && !item.name.EndsWith(" warning", StringComparison.OrdinalIgnoreCase),
                    "A boss retained a path or danger preview: " + item.name);
        }
        private IEnumerator VerifyLockedAimAfterKnockback()
        {
            // Real Update/input and normal ally basic attacks supply the damage and push.
            // These fixtures do not call damage, slash, attack-release or pattern-hit routines.
            for (int pattern = 0; pattern < 2; pattern++) for (int attacker = 0; attacker < 2; attacker++)
            {
                SetFloor(pattern == 0 ? 5 : 10); var boss = Boss;
                Place(game.hero, new Vector2(0, TowerWorld.Ground)); game.hero.facing = 1;
                Place(boss, new Vector2(2, TowerWorld.Ground)); boss.hp = boss.maxHp = 1000000;
                foreach (var add in game.enemies) if (!add.isBoss) { Place(add, new Vector2(25, TowerWorld.Ground)); add.stunTime = add.cooldown = 999; }
                if (attacker == 1)
                {
                    var companions = (IList)typeof(TowerRun).GetField("companions", Hidden).GetValue(game.run);
                    companions.Add(new CompanionState(TowerRun.GetDefinition("michael"))); game.ReconcileLoadout();
                    foreach (var ally in game.allies) { ally.cooldown = 0; ally.basicPending = false; }
                }
                boss.cooldown = 0; Call("TickBoss", boss, .01f);
                string patternName = (string)Call("BossPatternName", boss), attackerName = attacker == 0 ? "player slash" : "michael basic";
                Require((float)Call("BossTelegraphRemaining", boss) > 1, "Knockback fixture did not begin full boss preparation.");
                RequireNoPreviewObjects(); Vector2 aim = (Vector2)Call("BossLockedAim", boss);
                float beforeHp = boss.hp, beforeX = boss.pos.x, limit = Time.realtimeSinceStartup + 2.5f;
                game.AttackOverride = attacker == 0; game.enabled = true;
                while (boss.hp == beforeHp)
                {
                    Require(Time.realtimeSinceStartup < limit && (float)Call("BossTelegraphRemaining", boss) > 0,
                        patternName + " / " + attackerName + " did not produce a normal attack hit during preparation.");
                    RequireNoPreviewObjects();
                    yield return null;
                }
                ResetControls();
                // Ally hits resolve after TickBoss, so allow the next real update to settle the pushed actor.
                yield return null; game.enabled = false;
                Require(boss.pos.x > beforeX + .1f && boss.hp < beforeHp, patternName + " / " + attackerName + " did not cause real knockback.");
                Require((float)Call("BossTelegraphRemaining", boss) > 0 && (Vector2)Call("BossLockedAim", boss) == aim,
                    "Knockback cancelled preparation or changed the locked aim.");
                RequireNoPreviewObjects(); ClearShots();
                for (int frame = 0; frame < 100 && (int)Call("BossReleaseCount", boss) == 0; frame++)
                {
                    RequireNoPreviewObjects(); Call("TickBoss", boss, .02f);
                }
                Require((int)Call("BossReleaseCount", boss) == 1 && Shots.Count == (pattern == 0 ? 5 : 4), "A pushed boss failed to release its normal first volley.");
                Require((Vector2)Call("BossLockedAim", boss) == aim, "A pushed boss changed its aim at release.");
                Vector2 origin = boss.pos + Vector2.up * 1.4f, direction = (aim + Vector2.up - origin).normalized;
                float maxPositionError = 0, maxAngleError = 0; var angles = new List<float>();
                foreach (object shot in Shots)
                {
                    maxPositionError = Mathf.Max(maxPositionError, Vector2.Distance(Field<Vector2>(shot, "pos"), origin));
                    angles.Add(Vector2.SignedAngle(direction, Field<Vector2>(shot, "velocity")));
                }
                angles.Sort(); float[] expectedAngles = pattern == 0 ? new[] { -38f, -22f, -6f, 10f, 26f } : new[] { -95f, -85f, 85f, 95f };
                for (int i = 0; i < angles.Count; i++) maxAngleError = Mathf.Max(maxAngleError, Mathf.Abs(Mathf.DeltaAngle(expectedAngles[i], angles[i])));
                string result = patternName + " / " + attackerName + ": push=" + (boss.pos.x - beforeX).ToString("F3") +
                    ", projectile origin error=" + maxPositionError.ToString("F3") + ", locked-aim angle error=" + maxAngleError.ToString("F3") + ", preview objects=0";
                report.knockbackChecks.Add(result);
                Require(maxPositionError <= .001f && maxAngleError <= .1f, "A pushed boss did not fire from its actual emitter toward the fixed aim: " + result);
            }
            report.lockedAimAfterKnockback = true;
        }
        private IEnumerator VerifyBossPatterns()
        {
            string[] names = { "CrownVolley", "TwinSpiral", "LeapingSlam" }; float[][] poses = new float[3][];
            for (int pattern = 0; pattern < 3; pattern++)
            {
                SetFloor(5 + pattern * 5); var boss = Boss; Place(game.hero, new Vector2(0, TowerWorld.Ground)); Place(boss, new Vector2(4, TowerWorld.Ground));
                boss.cooldown = 0; game.invulnerable = 0; ClearShots();
                var renderers = new HashSet<SpriteRenderer>(game.GetComponentsInChildren<SpriteRenderer>(true)); Call("TickBoss", boss, .01f);
                Require((string)Call("BossPatternName", boss) == names[pattern], "Incorrect initial boss pattern for floor " + game.run.Floor);
                float preparation = (float)Call("BossTelegraphRemaining", boss); Vector2 aim = (Vector2)Call("BossLockedAim", boss);
                Require(preparation >= 1.19f && aim == game.hero.pos && (int)Call("BossReleaseCount", boss) == 0 && Shots.Count == 0, "Boss attacked without its full preparation.");
                RequireNoPreparationOverlay(renderers);
                float hp = game.hero.hp, clock = .01f;
                for (int i = 0; i < 20; i++) { Call("TickBoss", boss, .02f); clock += .02f; RequireNoPreparationOverlay(renderers); }
                Require(Shots.Count == 0 && game.hero.hp == hp, "A boss dealt damage during preparation.");
                Place(game.hero, new Vector2(-6, TowerWorld.Ground));
                Require((Vector2)Call("BossLockedAim", boss) == aim, "A boss tracked the hero after locking aim.");
                if (pattern == 0)
                {
                    float remaining = (float)Call("BossTelegraphRemaining", boss); game.menu = true; game.enabled = true; yield return Wait(.2f); game.enabled = false;
                    Require((float)Call("BossTelegraphRemaining", boss) == remaining && (int)Call("BossReleaseCount", boss) == 0, "Pause advanced boss preparation or projectiles."); report.pauseFreezesBoss = true; game.menu = false;
                }
                int lastRelease = 0; var times = new List<float>(); var bullets = new HashSet<object>(); var velocities = new List<Vector2>(); var origins = new List<Vector2>();
                bool captured = false; float beforeLandingHp = game.hero.hp;
                for (int frame = 0; frame < 350; frame++)
                {
                    if ((float)Call("BossTelegraphRemaining", boss) > .021f) RequireNoPreparationOverlay(renderers);
                    Call("TickBoss", boss, .02f); clock += .02f;
                    string stage = (string)Call("BossStageName", boss);
                    if (pattern == 2 && stage == "Leaping" && boss.pos.y > TowerWorld.Ground + 2.5f && !captured)
                    { captured = true; yield return Capture("slam", "보스 전용 동작 · 고정 지점 도약 강타"); Place(game.hero, aim); beforeLandingHp = game.hero.hp; }
                    int released = (int)Call("BossReleaseCount", boss);
                    if (released != lastRelease)
                    {
                        Require(released == lastRelease + 1, "Boss skipped a release telemetry event.");
                        times.Add((float)Call("BossLastReleaseAt", boss)); lastRelease = released;
                    }
                    foreach (object shot in Shots) if (bullets.Add(shot))
                    { velocities.Add(Field<Vector2>(shot, "velocity")); origins.Add(Field<Vector2>(shot, "pos")); Require(Field<float>(shot, "damage") > 8, "Boss projectile damage was missing."); }
                    if (stage == "Recovery") break;
                }
                Require((Vector2)Call("BossLockedAim", boss) == aim && times.Count > 0 && times[0] >= preparation, "Boss release ignored its locked aim or preparation delay.");
                if (pattern == 0)
                {
                    Require(times.Count == 3 && bullets.Count == 15, "CrownVolley must release three five-ray fans.");
                    Require(Mathf.Abs(times[1] - times[0] - .4f) < .025f && Mathf.Abs(times[2] - times[1] - .4f) < .025f, "Crown volley intervals drifted.");
                    Vector2 direction = (aim + Vector2.up - origins[0]).normalized; var firstAngles = new List<float>();
                    for (int i = 0; i < 5; i++) firstAngles.Add(Vector2.SignedAngle(direction, velocities[i])); firstAngles.Sort();
                    for (int i = 1; i < firstAngles.Count; i++) Require(Mathf.Abs(firstAngles[i] - firstAngles[i - 1] - 16) < .1f, "Crown fan did not leave even dodge gaps.");
                    report.crownGap = true; Call("TickShots", .55f); Call("TickEffects", .55f); yield return Capture("crown", "보스 전용 탄막 · 왕관 3연속 부채꼴");
                }
                else if (pattern == 1)
                {
                    Require(times.Count == 8 && bullets.Count == 22, "TwinSpiral must release eight rotating pairs with its safe corridor.");
                    for (int i = 1; i < times.Count; i++) Require(Mathf.Abs(times[i] - times[i - 1] - .22f) < .025f, "Spiral interval drifted.");
                    Vector2 direction = (aim + Vector2.up - origins[0]).normalized;
                    foreach (var velocity in velocities) Require(Mathf.Abs(Vector2.SignedAngle(direction, velocity)) >= 52.4f, "A spiral bullet filled the persistent safe corridor.");
                    report.spiralGap = true; Call("TickShots", .6f); Call("TickEffects", .6f); yield return Capture("spiral", "보스 전용 탄막 · 안전 통로가 있는 쌍나선");
                }
                else
                {
                    Require(captured && times.Count == 2 && bullets.Count == 4 && times[0] >= preparation + .83f && Mathf.Abs(times[1] - times[0] - .35f) < .025f, "Slam landing and delayed ground waves did not follow their preparation.");
                    Require(Mathf.Abs(beforeLandingHp - game.hero.hp - 14 * boss.damageScale) < .01f && boss.grounded && !boss.airborne, "A landed boss did not apply its scaled slam collision damage.");
                    // Waves leave from the ground under the landed boss, which follows the generated hills.
                    float landedGround = ((TowerWorld)typeof(TowerGame).GetField("world", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game)).GroundAt(boss.pos.x);
                    Require(Mathf.Abs(boss.pos.y - landedGround) < .01f && Mathf.Abs(origins[0].y - (landedGround + .65f)) < .01f && Mathf.Abs(origins[2].y - (landedGround + 1.05f)) < .01f, "Slam wave heights did not change between bursts."); report.slamCollision = true;
                }
                RequireNoPreviewObjects();
                report.patternChecks.Add(names[pattern] + ": preparation=" + preparation + ", releases=" + times.Count + ", projectiles=" + bullets.Count + ", no preparation overlays / locked aim / delay / gap PASS");
                poses[pattern] = BossMotionPose(pattern);
            }
            Require(Different(poses[0], poses[1]) && Different(poses[0], poses[2]) && Different(poses[1], poses[2]), "Boss exclusive attack motions were identical.");
            report.patternsDistinct = report.aimLocked = report.preparationBeforeDamage = report.preparationHasNoOverlays = report.patternDelays = report.attackMotionsDistinct = true;
        }
        private float[] BossMotionPose(int pattern)
        {
            var root = new GameObject("Original boss motion fixture"); root.transform.SetParent(game.transform, false); root.transform.position = Vector3.one * 1000;
            var art = TowerActorArt.Create(root.transform, 4, true); float duration = pattern == 0 ? 2.25f : pattern == 1 ? 3.2f : 2.75f; art.PlayBossAttack(pattern, duration);
            art.SetPose(false, false, false, 1, duration * .4f);
            Require(art.BossMotionName == (pattern == 0 ? "CrownVolley" : pattern == 1 ? "TwinSpiral" : "LeapingSlam"), "The boss motion API selected the wrong profile.");
            var pose = new List<float>(); foreach (var t in art.GetComponentsInChildren<Transform>()) { pose.AddRange(new[] { t.localPosition.x, t.localPosition.y, t.localEulerAngles.z, t.localScale.x, t.localScale.y }); }
            root.SetActive(false); Destroy(root); return pose.ToArray();
        }
        private static bool Different(float[] a, float[] b)
        { if (a.Length != b.Length) return true; for (int i = 0; i < a.Length; i++) if (Mathf.Abs(a[i] - b[i]) > .0001f) return true; return false; }
        private IEnumerator PauseResetAndCamera()
        {
            SetFloor(5); var old = Boss; Place(game.hero, new Vector2(0, TowerWorld.Ground)); Place(old, new Vector2(4, TowerWorld.Ground)); old.cooldown = 0; Call("TickBoss", old, .01f);
            Require((float)Call("BossTelegraphRemaining", old) > 0, "Reset fixture did not start boss preparation.");
            SetFloor(1); yield return null; yield return null;
            var states = (IDictionary)typeof(TowerGame).GetField("bossCombat", Hidden).GetValue(game);
            Require(states.Count == 0 && Shots.Count == 0 && Boss == null && !old.root, "New run retained boss state, actor or projectiles."); RequireNoPreviewObjects(); report.resetCleansBoss = true;
            SetFloor(5); Place(game.hero, new Vector2(0, TowerWorld.Ground)); foreach (var enemy in game.enemies) enemy.stunTime = 999;
            game.view.transform.position = (Vector3)Call("SnapCamera", new Vector3(1.6f, 0, -10)); var position = game.view.transform.position;
            game.enabled = true; game.invulnerable = 0; Call("Fire", game.hero.pos + Vector2.up * .9f, game.hero.pos + Vector2.up * .9f, 0f, 1f, true, Color.red, 0f, null, 1, 0f);
            yield return Wait(.1f); for (int frame = 0; frame < 15; frame++) { Require(game.view.transform.position == position, "Boss-floor damage shook the stationary camera."); yield return null; } report.cameraNoShake = true;
        }
        private IEnumerator ActualFight()
        {
            SetFloor(5); var boss = Boss; Require(boss != null, "The actual fight did not spawn a boss.");
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); Place(boss, new Vector2(4, TowerWorld.Ground));
            foreach (var add in game.enemies) if (!add.isBoss) { Place(add, new Vector2(25, TowerWorld.Ground)); add.stunTime = add.cooldown = 999; }
            yield return Capture("intro", "5층 테마 보스 · 실제 입력 전투 검증");
            int initialKills = game.kills; game.enabled = true; battleActive = true; bool fighting = false;
            float limit = Time.realtimeSinceStartup + 100, nextJump = Time.realtimeSinceStartup + 2, nextDash = Time.realtimeSinceStartup + 4;
            while (boss.hp > 0)
            {
                Require(Time.realtimeSinceStartup < limit && game.hero.hp > 0, "Actual input boss combat timed out or the QA player died.");
                DriveToward(boss);
                game.JumpOverride = Time.realtimeSinceStartup >= nextJump; if (game.JumpOverride) nextJump = Time.realtimeSinceStartup + 3.2f;
                game.DashOverride = Time.realtimeSinceStartup >= nextDash; if (game.DashOverride) nextDash = Time.realtimeSinceStartup + 5;
                if (!fighting && boss.hp < boss.maxHp * .7f) { fighting = true; yield return Capture("fight", "보스 전투 · 실제 이동 / 검 공격 / 점프 / 회피"); }
                yield return null;
            }
            ResetControls(); yield return null; yield return null;
            Require(game.kills > initialKills && !game.enemies.Contains(boss), "Normal player attacks did not retire the boss."); report.actualBossKilled = true;
            Require(game.enemies.Count > 0 && !game.run.IsFloorCleared && !game.run.IsRewardPending && !game.run.AdvanceFloor(), "Killing only the boss prematurely cleared its living adds."); report.bossDeathWaitsForAdds = true;
            foreach (var add in game.enemies) { add.stunTime = 0; add.cooldown = .8f; }
            limit = Time.realtimeSinceStartup + 80;
            while (game.enemies.Count > 0)
            {
                Require(Time.realtimeSinceStartup < limit && game.hero.hp > 0, "Actual input add combat timed out or the QA player died.");
                TowerGame.Fighter closest = null; float distance = float.MaxValue;
                foreach (var enemy in game.enemies) { float d = Vector2.Distance(enemy.pos, game.hero.pos); if (d < distance) { closest = enemy; distance = d; } }
                DriveToward(closest); yield return null;
            }
            ResetControls(); battleActive = false; yield return null; yield return null;
            report.actualCombatKills = game.kills - initialKills;
            Require(game.run.IsFloorCleared && game.run.IsRewardPending && game.run.ClearedFloors == 1 && game.run.Offers.Count == 3, "Clearing the boss and every add did not create exactly one reward."); report.actualAddsKilled = true;
            var ids = new List<long>(); foreach (var offer in game.run.Offers) ids.Add(offer.Id);
            yield return Wait(.3f); Require(!game.run.ClearFloor() && game.run.ClearedFloors == 1, "A cleared boss floor could reward twice.");
            for (int i = 0; i < ids.Count; i++) Require(ids[i] == game.run.Offers[i].Id, "A pending boss reward regenerated without input."); report.rewardExactlyOnce = true;
            yield return Capture("reward", "보스와 모든 수호자 처치 · 층 보상 1회");
        }
        private void DriveToward(TowerGame.Fighter target)
        {
            float dx = target.pos.x - game.hero.pos.x;
            game.JumpOverride = game.DropOverride = game.DashOverride = false;
            if(Mathf.Abs(target.pos.y-game.hero.pos.y)>1.75f)
            {
                if(game.hero.grounded){game.hero.followGroundX=game.hero.pos.x;game.hero.followGroundY=game.hero.pos.y;}
                Vector2 perch=(Vector2)Call("AllyCombatAnchor",game.hero,target);
                object[] route={game.hero,perch,game.PlayerMoveSpeed,false,false};
                Vector2 destination=(Vector2)Call("AllyFollowDestination",route);
                float routeDx=destination.x-game.hero.pos.x;
                game.MoveOverride=Mathf.Clamp(routeDx/(game.PlayerMoveSpeed*Mathf.Max(.001f,Mathf.Min(Time.unscaledDeltaTime,.04f))),-1,1);
                game.JumpOverride=(bool)route[3];
                game.DropOverride=destination.y<game.hero.pos.y-.65f&&Mathf.Abs(routeDx)<4;
                game.AttackOverride=false;
                return;
            }
            game.MoveOverride = Mathf.Abs(dx) > 1.8f ? Mathf.Sign(dx) : Mathf.Abs(dx) > .2f && game.hero.facing != (dx > 0 ? 1 : -1) ? Mathf.Sign(dx) : 0;
            game.AttackOverride = Mathf.Abs(dx) < 3.1f;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void Finish(Exception error)
        {
            if (finishing) return; finishing = true; battleActive = false;
            if (error != null) report.errors.Add(error is TargetInvocationException && error.InnerException != null ? error.InnerException.ToString() : error.ToString());
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.success = error == null && report.errors.Count == 0; ResetControls();
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= RecordLog;
            if (quitOnComplete) Application.Quit(report.success ? 0 : 1);
        }
    }
}
