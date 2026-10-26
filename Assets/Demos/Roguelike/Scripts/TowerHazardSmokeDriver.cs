using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Explicit validation only: the long floor, streamed monster packs, every trap kind and the boss's wake range.
    public sealed class TowerHazardSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class HazardReport
        {
            public string mode = "towerHazardSmoke", finishedUtc, layout;
            public bool success, longFloor, streamedSpawns, streamRelease, cameraReachesExit, allTrapKinds;
            public bool spikesHurtAndBounce, ventIdleSafe, ventBurstHurts, rockfallHitsOnce, totemFires, totemBreaks, padLaunches;
            public bool clearQuietsTraps, peacefulKeepsPadsOnly, bossHoldsUntilWake, bossWakes, compactVillageExtent;
            public int seed, floor, traps, spawnedAtStart, pendingAtStart, totalMonsters;
            public float cameraRight, spikeDamage, rockDamage;
            public List<string> checks = new List<string>(), screenshots = new List<string>(), errors = new List<string>();
        }
        private readonly HazardReport report = new HazardReport();
        private TowerGame game; private TowerSmokeDriver capture;
        private string prefix, reportPath; private bool finishing, quitOnComplete;
        private float deadline;
        private static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        public bool IsFinished { get { return finishing; } }
        public bool Succeeded { get { return finishing && report.success; } }
        private TowerWorld World { get { return (TowerWorld)typeof(TowerGame).GetField("world", Hidden).GetValue(game); } }
        private IList Shots { get { return (IList)typeof(TowerGame).GetField("shots", Hidden).GetValue(game); } }
        private object Call(string method, params object[] args) { return typeof(TowerGame).GetMethod(method, Hidden).Invoke(game, args); }
        private static void SetProperty(object target, string name, object value)
        { target.GetType().GetField("<" + name + ">k__BackingField", Hidden).SetValue(target, value); }

        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver, string capturePrefix, string outputReportPath, bool quitOnComplete = false)
        {
            if (game != null) throw new InvalidOperationException("Create a new hazard driver for each suite run.");
            if (tower == null) throw new ArgumentNullException(nameof(tower));
            if (captureDriver == null) throw new ArgumentNullException(nameof(captureDriver));
            if (!Path.IsPathRooted(capturePrefix) || !Path.IsPathRooted(outputReportPath)) throw new ArgumentException("Suite capture and report paths must be absolute.");
            game = tower; capture = captureDriver; prefix = capturePrefix; reportPath = outputReportPath; this.quitOnComplete = quitOnComplete;
            capture.PrepareForSuite(tower); game.SuppressProgressPersistence = true; game.SmokeControlled = true; game.TrapOverride = true; enabled = true;
            Application.logMessageReceived += RecordLog; deadline = Time.realtimeSinceStartup + 240;
            StartCoroutine(Guard(Suite()));
        }
        private void RecordLog(string condition, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(condition + "\n" + trace); }
        private void OnDestroy() { Application.logMessageReceived -= RecordLog; }
        private void Update()
        { if (!finishing && Time.realtimeSinceStartup > deadline) { StopAllCoroutines(); Finish(new TimeoutException("Hazard suite exceeded 240 seconds.")); } }
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
            FindFloor();
            VerifyStreaming();
            yield return VerifyCamera();
            yield return Tour();
            Arm();
            VerifySpikes(); VerifyVent(); VerifyRockfall(); VerifyTotem(); VerifyPad();
            VerifyClearQuiets();
            VerifyPeaceful();
            yield return VerifyBoss();
            VerifyVillage();
            Finish(null);
        }

        /// <summary>A normal floor whose layout holds every trap kind.</summary>
        private void FindFloor()
        {
            foreach (int floor in new[] { 7, 8, 12, 13, 14, 17, 18 })
                for (int seed = 1; seed < 400; seed++)
                {
                    var layout = TowerTerrainGenerator.Generate(seed, floor); var kinds = new HashSet<TowerHazardKind>();
                    foreach (var h in layout.Hazards) kinds.Add(h.Kind);
                    if (kinds.Count < 5) continue;
                    report.seed = seed; report.floor = floor; report.layout = layout.Name; return;
                }
            throw new InvalidOperationException("No floor with every trap kind among the searched seeds.");
        }
        private void Enter(int seed, int floor, bool streamed, FloorType type = FloorType.Normal)
        {
            game.enabled = false;
            if (game.hero != null && game.hero.root) game.hero.root.SetActive(false);
            game.NewRun(seed); game.StartClimb();
            foreach (var enemy in game.enemies) if (enemy.root) { enemy.root.SetActive(false); Destroy(enemy.root); }
            game.enemies.Clear(); Call("ResetEnemyCombat");
            SetProperty(game.run, "Floor", floor); SetProperty(game.run, "FloorType", type);
            // Play streams packs past the next screen; fixtures normally receive the whole floor at once.
            game.SmokeControlled = !streamed;
            try { Call("SpawnFloor"); } finally { game.SmokeControlled = true; }
            game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = game.DropOverride = game.FocusOverride = false;
        }
        private static void Place(TowerGame.Fighter actor, Vector2 position)
        { actor.pos = position; actor.velocityY = 0; actor.grounded = true; actor.support = -1; actor.lastX = position.x; actor.root.transform.position = position; }
        private TowerGame.Trap FindTrap(TowerHazardKind kind)
        {
            foreach (var trap in game.traps) if (trap.spec.Kind == kind) return trap;
            throw new InvalidOperationException("The floor has no " + kind + ".");
        }

        private void VerifyStreaming()
        {
            Enter(report.seed, report.floor, true);
            var layout = World.CurrentLayout;
            report.longFloor = layout.ExitX == TowerTerrainGenerator.ExitX && Mathf.Approximately(TowerWorld.Right, layout.Right) && Mathf.Approximately(TowerWorld.ExitX, layout.ExitX);
            Require(report.longFloor, "The world did not adopt the long floor's extent.");
            int expected = TowerDifficulty.RegularCount(report.floor) + TowerDifficulty.FlyerCount(report.floor);
            report.spawnedAtStart = game.enemies.Count; report.pendingAtStart = game.PendingEnemies; report.totalMonsters = game.RemainingEnemies;
            Require(report.totalMonsters == expected, "Expected " + expected + " monsters on the floor, found " + report.totalMonsters + ".");
            float reach = game.hero.pos.x + TowerGame.SpawnAhead;
            foreach (var enemy in game.enemies) Require(enemy.pos.x < reach + .01f, "A monster beyond the next screen was created at once (x=" + enemy.pos.x + ").");
            report.streamedSpawns = report.pendingAtStart > 0 && report.spawnedAtStart > 0;
            Require(report.streamedSpawns, "Monsters ahead were not queued.");
            float spread = float.NegativeInfinity; foreach (var enemy in game.enemies) spread = Mathf.Max(spread, enemy.pos.x);
            Place(game.hero, new Vector2(TowerWorld.ExitX - 4, World.GroundAt(TowerWorld.ExitX - 4)));
            for (int i = 0; i < 64 && game.PendingEnemies > 0; i++) Call("TickSpawnQueue");
            report.streamRelease = game.PendingEnemies == 0 && game.enemies.Count == expected;
            Require(report.streamRelease, "Queued monsters were not released when the hero reached them.");
            foreach (var enemy in game.enemies) spread = Mathf.Max(spread, enemy.pos.x);
            Require(spread > TowerWorld.ExitX * .6f, "Monster packs do not reach far down the floor (furthest x=" + spread + ").");
            report.checks.Add("streamed " + report.spawnedAtStart + " at once and " + report.pendingAtStart + " later; furthest pack at x=" + spread.ToString("F1"));
        }

        private IEnumerator VerifyCamera()
        {
            game.invulnerable = 999; game.enabled = true;
            Place(game.hero, new Vector2(TowerWorld.Right - 1, World.GroundAt(TowerWorld.Right - 1)));
            for (int i = 0; i < 90; i++) yield return null;
            report.cameraRight = game.view.transform.position.x;
            report.cameraReachesExit = report.cameraRight > TowerWorld.ExitX - 14 && report.cameraRight <= TowerWorld.Right - 7.5f + .5f;
            Require(report.cameraReachesExit, "The camera did not follow to the floor's end (x=" + report.cameraRight + ").");
        }

        /// <summary>Screens along the floor with the traps active, for review.</summary>
        private IEnumerator Tour()
        {
            var vent = FindTrap(TowerHazardKind.FlameVent); var totem = FindTrap(TowerHazardKind.BoltTotem); var rock = FindTrap(TowerHazardKind.Rockfall);
            var stops = new List<KeyValuePair<string, float>> { new KeyValuePair<string, float>("start", -6) };
            stops.Add(new KeyValuePair<string, float>("vent", vent.spec.X - 3)); stops.Add(new KeyValuePair<string, float>("totem", totem.spec.X - 3));
            stops.Add(new KeyValuePair<string, float>("rockfall", rock.spec.X - 3));
            stops.Add(new KeyValuePair<string, float>("pad", FindTrap(TowerHazardKind.JumpPad).spec.X - 3));
            stops.Add(new KeyValuePair<string, float>("spikes", FindTrap(TowerHazardKind.Spikes).spec.X - 3));
            stops.Add(new KeyValuePair<string, float>("exit", TowerWorld.ExitX - 5));
            foreach (var stop in stops)
            {
                float x = Mathf.Clamp(stop.Value, TowerWorld.Left + 1, TowerWorld.Right - 1);
                Place(game.hero, new Vector2(x, World.GroundAt(x))); game.hero.facing = 1;
                for (int i = 0; i < 50; i++) { game.invulnerable = 999; yield return null; }
                // Catch the vent mid-eruption, the totem charging and a boulder halfway down.
                vent.clock = TowerGame.VentPeriod - TowerGame.VentBurst; totem.clock = TowerGame.TotemPeriod - TowerGame.TotemWarning * .6f;
                if (stop.Key == "rockfall") { rock.rockTime = TowerGame.RockWarning + TowerGame.RockFall * .35f; rock.rockX = rock.spec.X; rock.rockHit = true; }
                for (int i = 0; i < 16; i++) yield return null;
                game.Notice("긴 층 · " + stop.Key + "  (x=" + x.ToString("F0") + " / 출구 " + TowerWorld.ExitX.ToString("F0") + ")", 5);
                yield return capture.CaptureForSuite(prefix, "tour-" + stop.Key);
                report.screenshots.Add(prefix + "-tour-" + stop.Key + ".png");
            }
            game.enabled = false;
        }

        /// <summary>Unit checks run on the trap floor with one stunned sentinel keeping it uncleared.</summary>
        private void Arm()
        {
            Enter(report.seed, report.floor, false);
            report.traps = game.traps.Count;
            Require(report.traps == World.CurrentLayout.Hazards.Count, "Built " + report.traps + " traps for " + World.CurrentLayout.Hazards.Count + " planned.");
            var kinds = new HashSet<TowerHazardKind>(); foreach (var trap in game.traps) kinds.Add(trap.spec.Kind);
            report.allTrapKinds = kinds.Count == 5; Require(report.allTrapKinds, "Not every trap kind was built.");
            foreach (var enemy in game.enemies) { enemy.stunTime = enemy.cooldown = 999; Place(enemy, new Vector2(TowerWorld.ExitX - 1, World.GroundAt(TowerWorld.ExitX - 1))); }
            Require(!game.run.IsFloorCleared, "The trap floor started cleared.");
        }
        private void Ready(Vector2 at)
        {
            Place(game.hero, at); game.hero.hp = game.PlayerMaxHealth; game.invulnerable = 0; game.hero.facing = 1;
            foreach (object shot in Shots) { var root = (GameObject)shot.GetType().GetField("root").GetValue(shot); if (root) Destroy(root); }
            Shots.Clear();
        }

        private void VerifySpikes()
        {
            var trap = FindTrap(TowerHazardKind.Spikes);
            Ready(new Vector2(trap.spec.X, World.GroundAt(trap.spec.X))); int hits = game.TrapHits; float hp = game.hero.hp;
            Call("TickTraps", .02f);
            report.spikeDamage = hp - game.hero.hp;
            report.spikesHurtAndBounce = game.TrapHits == hits + 1 && report.spikeDamage > 0 && !game.hero.grounded && Mathf.Approximately(game.hero.velocityY, TowerGame.SpikeBounce);
            Require(report.spikesHurtAndBounce, "Standing on spikes did not hurt and bounce the hero.");
        }
        private void VerifyVent()
        {
            var trap = FindTrap(TowerHazardKind.FlameVent);
            Ready(new Vector2(trap.spec.X, trap.baseY)); trap.clock = 0; float hp = game.hero.hp;
            Call("TickTraps", .02f);
            report.ventIdleSafe = Mathf.Approximately(hp, game.hero.hp); Require(report.ventIdleSafe, "An idle vent hurt the hero.");
            trap.clock = TowerGame.VentPeriod - TowerGame.VentBurst + .05f; Call("TickTraps", .02f);
            report.ventBurstHurts = game.hero.hp < hp; Require(report.ventBurstHurts, "An erupting vent did not hurt the hero standing over it.");
        }
        private void VerifyRockfall()
        {
            var trap = FindTrap(TowerHazardKind.Rockfall);
            Ready(new Vector2(trap.spec.X, World.GroundAt(trap.spec.X))); int hits = game.TrapHits; float hp = game.hero.hp;
            for (int i = 0; i < 90 && (trap.rockTime >= 0 || i == 0); i++) { game.invulnerable = Mathf.Max(0, game.invulnerable - .02f); Call("TickTraps", .02f); }
            report.rockDamage = hp - game.hero.hp;
            report.rockfallHitsOnce = game.TrapHits == hits + 1 && report.rockDamage > 0 && trap.rockTime < 0 && trap.cooldown > 0;
            Require(report.rockfallHitsOnce, "The rockfall did not land exactly one hit on a hero standing in its zone (hits " + (game.TrapHits - hits) + ").");
        }
        private void VerifyTotem()
        {
            var trap = FindTrap(TowerHazardKind.BoltTotem);
            Vector2 near = new Vector2(trap.spec.X - 2.5f, trap.baseY);
            Ready(near); game.hero.support = World.SupportAt(near.x, near.y, .2f);
            game.view.transform.position = new Vector3(trap.spec.X, trap.baseY + 1, -10);
            trap.clock = TowerGame.TotemPeriod - .01f; int shots = Shots.Count;
            Call("TickTraps", .02f);
            report.totemFires = Shots.Count == shots + 1; Require(report.totemFires, "A charged totem in view did not fire at the hero.");
            Place(game.hero, new Vector2(trap.spec.X - .8f, trap.baseY)); game.hero.facing = 1;
            for (int i = 0; i < TowerGame.TotemHits; i++) Call("StrikeTraps", 2f, false);
            report.totemBreaks = trap.broken; Require(report.totemBreaks, "Three slashes did not topple the totem.");
            trap.clock = TowerGame.TotemPeriod; shots = Shots.Count; Call("TickTraps", .02f);
            Require(Shots.Count == shots, "A broken totem still fired.");
        }
        private void VerifyPad()
        {
            var trap = FindTrap(TowerHazardKind.JumpPad);
            Ready(new Vector2(trap.spec.X, trap.baseY)); game.airJumps = 0;
            Call("TickTraps", .02f);
            report.padLaunches = !game.hero.grounded && Mathf.Approximately(game.hero.velocityY, TowerGame.PadVelocity) && game.airJumps == TowerGame.MaxAirJumps;
            Require(report.padLaunches, "Landing on the pad did not launch the hero.");
        }
        private void VerifyClearQuiets()
        {
            foreach (var enemy in game.enemies) if (enemy.root) Destroy(enemy.root);
            game.enemies.Clear(); Call("ResetBossCombat");
            game.run.ClearFloor();
            var vent = FindTrap(TowerHazardKind.FlameVent);
            Ready(new Vector2(vent.spec.X, vent.baseY)); vent.clock = TowerGame.VentPeriod - TowerGame.VentBurst + .05f; float hp = game.hero.hp;
            Call("TickTraps", .02f);
            report.clearQuietsTraps = Mathf.Approximately(hp, game.hero.hp) && vent.glow.color.a < .2f;
            Require(report.clearQuietsTraps, "Vents kept erupting after the floor was cleared.");
        }
        private void VerifyPeaceful()
        {
            Enter(report.seed, report.floor, false, FloorType.Rest);
            bool padsOnly = true; foreach (var trap in game.traps) padsOnly &= trap.spec.Kind == TowerHazardKind.JumpPad;
            int pads = 0; foreach (var h in World.CurrentLayout.Hazards) if (h.Kind == TowerHazardKind.JumpPad) pads++;
            report.peacefulKeepsPadsOnly = padsOnly && game.traps.Count == pads && game.RemainingEnemies == 0;
            Require(report.peacefulKeepsPadsOnly, "A rest floor built traps or monsters.");
        }
        private IEnumerator VerifyBoss()
        {
            Enter(report.seed, 10, false);
            var boss = game.enemies.Find(e => e.isBoss);
            Require(boss != null && Mathf.Abs(boss.pos.x - (TowerWorld.ExitX - TowerGame.BossStandoff)) < .01f, "The boss does not hold the stretch by the exit.");
            foreach (var enemy in game.enemies) if (!enemy.isBoss) { enemy.stunTime = enemy.cooldown = 999; }
            float start = boss.pos.x; game.invulnerable = 999; game.enabled = true;
            for (int i = 0; i < 60; i++) yield return null;
            report.bossHoldsUntilWake = Mathf.Abs(boss.pos.x - start) < .01f;
            Require(report.bossHoldsUntilWake, "The boss walked toward a hero at the far spawn.");
            float x = start - 13; Place(game.hero, new Vector2(x, World.GroundAt(x)));
            for (int i = 0; i < 60; i++) { game.invulnerable = 999; yield return null; }
            report.bossWakes = boss.pos.x < start - .05f;
            Require(report.bossWakes, "The boss did not engage a hero inside its wake range.");
            game.enabled = false;
        }
        private void VerifyVillage()
        {
            var layout = new TowerFloorLayout(1, "village", 1, new TowerSurface(new[] { -40f, 100f }, new[] { TowerWorld.Ground, TowerWorld.Ground }), new List<TowerPlatform>());
            report.compactVillageExtent = Mathf.Approximately(layout.ExitX, TowerTerrainGenerator.CompactExitX) && layout.Hazards.Count == 0;
            Require(report.compactVillageExtent, "Hand-built chambers lost the compact extent.");
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void Finish(Exception error)
        {
            if (finishing) return; finishing = true;
            if (error != null) report.errors.Add(error is TargetInvocationException && error.InnerException != null ? error.InnerException.ToString() : error.ToString());
            report.finishedUtc = DateTime.UtcNow.ToString("o"); report.success = error == null && report.errors.Count == 0;
            game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = game.DropOverride = false; game.TrapOverride = false;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= RecordLog;
            if (quitOnComplete) Application.Quit(report.success ? 0 : 1);
        }
    }
}
