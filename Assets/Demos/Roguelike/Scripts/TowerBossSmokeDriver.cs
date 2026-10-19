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
            public bool patternsDistinct, telegraphsLocked, warningsBeforeDamage, patternDelays, crownGap, spiralGap, slamCollision, attackMotionsDistinct, pauseFreezesBoss, resetCleansBoss;
            public bool actualBossKilled, bossDeathWaitsForAdds, actualAddsKilled, rewardExactlyOnce, cameraNoShake;
            public int sampledFloors, sampledBosses, actualCombatKills, healthRefills;
            public float restoredHealth;
            public List<string> floorSamples = new List<string>(), statSamples = new List<string>(), patternChecks = new List<string>(), screenshots = new List<string>(), errors = new List<string>();
        }
        private readonly BossReport report = new BossReport();
        private TowerGame game; private TowerSmokeDriver capture;
        private string prefix, reportPath; private bool finishing, battleActive; private float deadline;
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private IList Shots { get { return (IList)typeof(TowerGame).GetField("shots", Hidden).GetValue(game); } }
        private object Call(string name, params object[] values) { return typeof(TowerGame).GetMethod(name, Hidden).Invoke(game, values); }
        private static T Field<T>(object value, string name) { return (T)value.GetType().GetField(name).GetValue(value); }
        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver)
        {
            game = tower; capture = captureDriver; game.SmokeControlled = true;
            string[] args = Environment.GetCommandLineArgs();
            prefix = Arg(args, "-towerBossCapture") ?? Path.Combine(Application.persistentDataPath, "tower-boss");
            reportPath = Arg(args, "-towerBossReport") ?? Path.Combine(Application.persistentDataPath, "tower-boss.json");
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
        private void ResetControls() { game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = game.FocusOverride = false; }
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
            int previousRegular = 0, previousTotal = 0;
            for (int floor = 1; floor <= 25; floor++)
            {
                SetFloor(floor); int regular = 0, bosses = 0;
                foreach (var enemy in game.enemies)
                {
                    Require(enemy.difficultyFloor == floor, "An enemy used the wrong difficulty floor.");
                    if (enemy.isBoss)
                    {
                        bosses++; Require(!enemy.airborne && enemy.grounded && Mathf.Abs(enemy.maxHp - TowerDifficulty.BossHealth(floor)) < .01f, "Boss HP/ground state did not match the floor.");
                        Require(game.CanTarget(enemy, game.run.Player), "A melee-only player cannot target a boss.");
                        foreach (string id in new[] { "rook", "finn", "sera", "mira" }) Require(game.CanTarget(enemy, new CompanionState(TowerRun.GetDefinition(id))), "A companion class cannot target the grounded boss.");
                    }
                    else
                    {
                        regular++; float hp = (enemy.kind == 0 ? 60 : enemy.kind == 1 ? 48 : enemy.kind == 2 ? 80 : 52) * TowerDifficulty.HealthMultiplier(floor);
                        Require(Mathf.Abs(enemy.maxHp - hp) < .01f, "A normal enemy's actual HP did not scale.");
                        Require(Mathf.Abs(enemy.damageScale - TowerDifficulty.DamageMultiplier(floor)) < .001f && Mathf.Abs(enemy.moveScale - TowerDifficulty.MoveMultiplier(floor)) < .001f && Mathf.Abs(enemy.bulletSpeedScale - TowerDifficulty.BulletMultiplier(floor)) < .001f && Mathf.Abs(enemy.attackRateScale - TowerDifficulty.AttackRateMultiplier(floor)) < .001f, "A normal enemy's runtime multipliers did not match difficulty.");
                    }
                }
                Require(regular == TowerDifficulty.RegularCount(floor) && regular >= previousRegular && game.enemies.Count >= previousTotal, "Actual floor counts decreased or did not match difficulty.");
                Require(bosses == (floor % 3 == 0 ? 1 : 0), "Boss cadence did not match multiples of three: " + floor);
                report.sampledFloors++; report.sampledBosses += bosses; report.floorSamples.Add(floor + ": regular=" + regular + ", boss=" + bosses + ", total=" + game.enemies.Count);
                previousRegular = regular; previousTotal = game.enemies.Count; yield return null;
            }
            report.floorCountsMonotone = report.regularCountsMatch = report.bossCadence = report.bossesGroundTargetable = report.regularHealthScaling = report.bossHealthScaling = true;
            float[] first = MeasureRegular(1), final = MeasureRegular(25);
            for (int i = 0; i < 4; i++) Require(final[i] > first[i], "A measured normal combat stat did not increase: " + i);
            Require(Mathf.Abs(final[4] - first[4]) < .001f && final[5] < first[5], "Higher-floor normal attacks must keep warning time and recover faster.");
            report.regularDamageScaling = report.regularMovementScaling = report.regularBulletScaling = report.regularAttackRateScaling = true;
        }
        private float[] MeasureRegular(int floor)
        {
            SetFloor(floor); Place(game.hero, new Vector2(0, TowerWorld.Ground)); var actor = game.enemies.Find(e => !e.isBoss && e.kind == 0);
            Place(actor, new Vector2(1, TowerWorld.Ground)); Call("ConfigureEnemy", actor, 0, 0); actor.windup = .001f; actor.strikePoint = game.hero.pos; game.invulnerable = 0;
            float before = game.hero.hp; Call("TickEnemy", actor, .01f); float damage = before - game.hero.hp;
            Place(game.hero, new Vector2(4, TowerWorld.Ground)); Place(actor, new Vector2(-4, TowerWorld.Ground)); actor.cooldown = 999; actor.windup = 0;
            float x = actor.pos.x; Call("TickEnemy", actor, .1f); float movement = Mathf.Abs(actor.pos.x - x) / .1f;
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); Place(actor, new Vector2(4, TowerWorld.Ground)); Call("ConfigureEnemy", actor, 1, 0); actor.cooldown = 0; ClearShots();
            Call("TickEnemy", actor, .01f); float warning = actor.windup, interval = actor.cooldown;
            for (int step = 0; step < 200 && Shots.Count == 0; step++) Call("TickEnemy", actor, .02f);
            Require(Shots.Count == 3, "Normal scaling sample did not release its actual fan.");
            float bullet = Field<Vector2>(Shots[0], "velocity").magnitude, bulletDamage = Field<float>(Shots[0], "damage");
            report.statSamples.Add(floor + ": meleeDamage=" + damage + ", move=" + movement + ", bulletSpeed=" + bullet + ", bulletDamage=" + bulletDamage + ", warning=" + warning + ", interval=" + interval);
            return new[] { damage, movement, bullet, bulletDamage, warning, interval };
        }
        private IEnumerator PatternsAndMotions()
        {
            yield return VerifyBossPatterns(); yield return PauseResetAndCamera();
        }
        private IEnumerator VerifyBossPatterns()
        {
            string[] names = { "CrownVolley", "TwinSpiral", "LeapingSlam" }; float[][] poses = new float[3][];
            for (int pattern = 0; pattern < 3; pattern++)
            {
                SetFloor(3 + pattern * 3); var boss = Boss; Place(game.hero, new Vector2(0, TowerWorld.Ground)); Place(boss, new Vector2(4, TowerWorld.Ground));
                boss.cooldown = 0; game.invulnerable = 0; ClearShots(); Call("TickBoss", boss, .01f);
                Require((string)Call("BossPatternName", boss) == names[pattern], "Incorrect initial boss pattern for floor " + game.run.Floor);
                float warning = (float)Call("BossTelegraphRemaining", boss); Vector2 aim = (Vector2)Call("BossLockedAim", boss);
                Require(warning >= 1.19f && aim == game.hero.pos && (int)Call("BossReleaseCount", boss) == 0 && Shots.Count == 0, "Boss attacked without its full visible preparation.");
                float hp = game.hero.hp, clock = .01f;
                for (int i = 0; i < 20; i++) { Call("TickBoss", boss, .02f); clock += .02f; }
                Require(Shots.Count == 0 && game.hero.hp == hp, "A boss dealt damage during its warning.");
                Place(game.hero, new Vector2(-6, TowerWorld.Ground));
                Require((Vector2)Call("BossLockedAim", boss) == aim, "A boss tracked the hero after telegraph locking.");
                if (pattern == 0)
                {
                    float remaining = (float)Call("BossTelegraphRemaining", boss); game.menu = true; game.enabled = true; yield return Wait(.2f); game.enabled = false;
                    Require((float)Call("BossTelegraphRemaining", boss) == remaining && (int)Call("BossReleaseCount", boss) == 0, "Pause advanced boss warning or projectiles."); report.pauseFreezesBoss = true; game.menu = false;
                }
                int lastRelease = 0; var times = new List<float>(); var bullets = new HashSet<object>(); var velocities = new List<Vector2>(); var origins = new List<Vector2>();
                bool captured = false; float beforeLandingHp = game.hero.hp;
                for (int frame = 0; frame < 350; frame++)
                {
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
                Require((Vector2)Call("BossLockedAim", boss) == aim && times.Count > 0 && times[0] >= warning, "Boss release ignored its locked aim or warning delay.");
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
                    Require(captured && times.Count == 2 && bullets.Count == 4 && times[0] >= warning + .83f && Mathf.Abs(times[1] - times[0] - .35f) < .025f, "Slam landing and delayed ground waves did not follow their preparation.");
                    Require(Mathf.Abs(beforeLandingHp - game.hero.hp - 14 * boss.damageScale) < .01f && boss.grounded && !boss.airborne, "A landed boss did not apply its scaled slam collision damage.");
                    Require(Mathf.Abs(origins[0].y - (TowerWorld.Ground + .65f)) < .01f && Mathf.Abs(origins[2].y - (TowerWorld.Ground + 1.05f)) < .01f, "Slam wave heights did not change between bursts."); report.slamCollision = true;
                }
                report.patternChecks.Add(names[pattern] + ": warning=" + warning + ", releases=" + times.Count + ", projectiles=" + bullets.Count + ", locked aim / delay / gap PASS");
                poses[pattern] = BossMotionPose(pattern);
            }
            Require(Different(poses[0], poses[1]) && Different(poses[0], poses[2]) && Different(poses[1], poses[2]), "Boss exclusive attack motions were identical.");
            report.patternsDistinct = report.telegraphsLocked = report.warningsBeforeDamage = report.patternDelays = report.attackMotionsDistinct = true;
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
            SetFloor(3); var old = Boss; Place(game.hero, new Vector2(0, TowerWorld.Ground)); Place(old, new Vector2(4, TowerWorld.Ground)); old.cooldown = 0; Call("TickBoss", old, .01f);
            Require((float)Call("BossTelegraphRemaining", old) > 0, "Reset fixture did not start a boss warning.");
            SetFloor(1); yield return null; yield return null;
            var states = (IDictionary)typeof(TowerGame).GetField("bossCombat", Hidden).GetValue(game);
            Require(states.Count == 0 && Shots.Count == 0 && Boss == null && !old.root, "New run retained boss state, warning actor or projectiles."); report.resetCleansBoss = true;
            SetFloor(3); Place(game.hero, new Vector2(0, TowerWorld.Ground)); foreach (var enemy in game.enemies) enemy.stunTime = 999;
            game.view.transform.position = (Vector3)Call("SnapCamera", new Vector3(1.6f, 0, -10)); var position = game.view.transform.position;
            game.enabled = true; game.invulnerable = 0; Call("Fire", game.hero.pos + Vector2.up * .9f, game.hero.pos + Vector2.up * .9f, 0f, 1f, true, Color.red, 0f, null, 1, 0f);
            yield return Wait(.1f); for (int frame = 0; frame < 15; frame++) { Require(game.view.transform.position == position, "Boss-floor damage shook the stationary camera."); yield return null; } report.cameraNoShake = true;
        }
        private IEnumerator ActualFight()
        {
            SetFloor(3); var boss = Boss; Require(boss != null, "The actual fight did not spawn a boss.");
            Place(game.hero, new Vector2(0, TowerWorld.Ground)); Place(boss, new Vector2(4, TowerWorld.Ground));
            foreach (var add in game.enemies) if (!add.isBoss) { Place(add, new Vector2(25, TowerWorld.Ground)); add.stunTime = add.cooldown = 999; }
            yield return Capture("intro", "3층 보스 · 실제 입력 전투 검증");
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
            game.MoveOverride = Mathf.Abs(dx) > 1.8f ? Mathf.Sign(dx) : Mathf.Abs(dx) > .2f && game.hero.facing != (dx > 0 ? 1 : -1) ? Mathf.Sign(dx) : 0;
            game.AttackOverride = Mathf.Abs(dx) < 3.1f; game.JumpOverride = game.DashOverride = false;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void Finish(Exception error)
        {
            if (finishing) return; finishing = true; battleActive = false;
            if (error != null) report.errors.Add(error is TargetInvocationException && error.InnerException != null ? error.InnerException.ToString() : error.ToString());
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.success = error == null && report.errors.Count == 0; ResetControls();
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true)); Application.Quit(report.success ? 0 : 1);
        }
    }
}
