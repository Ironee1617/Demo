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
            public bool spikesHurtAndBounce, ventIdleSafe, ventBurstHurts, rockfallHitsOnce, totemFires, totemBreaks, padLaunches, trapDodgeCounterOnly;
            public bool clearQuietsTraps, peacefulKeepsPadsOnly, bossHoldsUntilWake, bossWakes, compactVillageExtent;
            public bool spikesQuietAfterClear, rocksQuietAfterClear, clearRevealsLive, clearRecordedOnce, autoExitWalksThrough, branchWaitsForE;
            public bool peacefulRoomCompact, restOffersChoice, restTempers, restLeavesHealed;
            public bool bossBarWaits, bossBarShows, bossStreamed, bossStreamsIn;
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
            VerifySpikes(); VerifyVent(); VerifyRockfall(); VerifyTrapDodge(); VerifyTotem(); VerifyPad();
            VerifyClearQuiets();
            yield return VerifyClearFlow();
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
        /// <summary>
        /// A dash just begun through an erupting vent, a falling boulder or a totem's bolt readies the counter and nothing
        /// more: no hit and no dash charge back. The rat scout rides along, so a trap that paid a perfect dodge's rewards would
        /// show in its full refill. (This climb has no oath; the oath suite checks a trap's bolt leaves the oath alone.) The
        /// fixture holds each dash through the danger (Update does not run).
        /// </summary>
        private void VerifyTrapDodge()
        {
            // The real join path wires the scout's passive; the next Enter starts a new run without it.
            typeof(TowerRun).GetMethod("AddCompanion", Hidden).Invoke(game.run, new object[] { TowerRun.GetDefinition("zodiac_rat") });
            game.ReconcileLoadout();
            Require(game.run.Companions.Count == 1 && game.run.Passives.Has(PassiveKind.Nimble), "The trap dodge fixture could not bring the rat scout along.");
            var refundAt = typeof(TowerGame).GetField("dodgeRefundAt", Hidden);
            var vent = FindTrap(TowerHazardKind.FlameVent); var rock = FindTrap(TowerHazardKind.Rockfall);
            for (int pass = 0; pass < 2; pass++)
            {
                bool flame = pass == 0; var trap = flame ? vent : rock;
                Ready(new Vector2(trap.spec.X, flame ? trap.baseY : World.GroundAt(trap.spec.X))); game.dashTime = 0; SetProperty(game, "CounterRemaining", 0f);
                if (flame) vent.clock = TowerGame.VentPeriod - TowerGame.VentBurst + .05f; else { rock.cooldown = 0; rock.rockTime = -1; }
                // No refund waits on DodgeRefundInterval, so a charge kept proves the trap gave none, the scout's refill included.
                refundAt.SetValue(game, -10f); game.ReadyDash(); Call("StartDash", 0f, false);
                int charges = game.DashCharges, dodges = game.PerfectDodges, hits = game.TrapHits; float hp = game.hero.hp;
                Require(game.dashTime > 0 && game.invulnerable > 0 && charges < game.MaxDashCharges, "The trap dodge fixture's dash did not spend a charge.");
                if (flame) Call("TickTraps", .02f);
                else for (int i = 0; i < 90 && (rock.rockTime >= 0 || i == 0); i++) Call("TickTraps", .02f);
                Require(Mathf.Approximately(hp, game.hero.hp) && game.TrapHits == hits && game.PerfectDodges == dodges + 1 && Mathf.Approximately(game.CounterRemaining, TowerGame.DodgeCounterDuration),
                    "A dash through the " + (flame ? "vent's flame" : "boulder") + " did not ready the counter without a hit.");
                Require(game.DashCharges == charges,
                    "A dash through the " + (flame ? "vent's flame" : "boulder") + " gave a dash charge back (the refund or the scout's refill): charges " + charges + " -> " + game.DashCharges);
            }
            // A totem's bolt is a trap's too: dashing through it readies the counter alone.
            var totem = FindTrap(TowerHazardKind.BoltTotem); Vector2 near = new Vector2(totem.spec.X - 2.5f, totem.baseY);
            Ready(near); game.hero.support = World.SupportAt(near.x, near.y, .2f); game.dashTime = 0; SetProperty(game, "CounterRemaining", 0f);
            game.view.transform.position = new Vector3(totem.spec.X, totem.baseY + 1, -10);
            totem.clock = TowerGame.TotemPeriod - .01f; Call("TickTraps", .02f);
            bool marked = Shots.Count > 0; foreach (object shot in Shots) marked &= (bool)shot.GetType().GetField("trap").GetValue(shot);
            Require(marked, "A charged totem did not fire a bolt marked as a trap's.");
            typeof(TowerGame).GetField("previousHeroPosition", Hidden).SetValue(game, game.hero.pos);
            refundAt.SetValue(game, -10f); game.ReadyDash(); Call("StartDash", 0f, false);
            int kept = game.DashCharges, before = game.PerfectDodges, bolts = Shots.Count; float health = game.hero.hp;
            Require(kept < game.MaxDashCharges, "The totem dodge fixture's dash did not spend a charge.");
            for (int i = 0; i < 90 && Shots.Count == bolts; i++) Call("TickShots", .02f);
            Require(Shots.Count < bolts && Mathf.Approximately(health, game.hero.hp) && game.PerfectDodges == before + 1 && Mathf.Approximately(game.CounterRemaining, TowerGame.DodgeCounterDuration)
                && game.DashCharges == kept,
                "A dash through a totem's bolt did more than ready the counter: charges " + kept + " -> " + game.DashCharges + ", dodges +" + (game.PerfectDodges - before));
            Ready(near);
            game.dashTime = game.invulnerable = 0; SetProperty(game, "CounterRemaining", 0f);
            report.trapDodgeCounterOnly = true;
            report.checks.Add("With the rat scout along, a dash held through an erupting vent, a falling boulder and a totem's bolt readies the counter without a hit and gives no dash charge back, neither the refund nor the scout's full refill.");
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
            // The spikes go quiet too, so the clear playing out and the walk to the exit are safe.
            var spikes = FindTrap(TowerHazardKind.Spikes);
            Ready(new Vector2(spikes.spec.X, World.GroundAt(spikes.spec.X))); int hits = game.TrapHits; hp = game.hero.hp;
            Call("TickTraps", .02f);
            report.spikesQuietAfterClear = game.TrapHits == hits && Mathf.Approximately(hp, game.hero.hp) && game.hero.grounded;
            Require(report.spikesQuietAfterClear, "Spikes still hurt the hero after the floor was cleared.");
            // A boulder already falling as the floor clears lands harmlessly, so the live clear cannot fell the hero under its cards.
            var rock = FindTrap(TowerHazardKind.Rockfall);
            Ready(new Vector2(rock.spec.X, World.GroundAt(rock.spec.X))); hits = game.TrapHits; hp = game.hero.hp;
            rock.rockTime = TowerGame.RockWarning; rock.rockX = rock.spec.X; rock.rockHit = false;
            for (int i = 0; i < 60 && rock.rockTime >= 0; i++) Call("TickTraps", .02f);
            report.rocksQuietAfterClear = rock.rockTime < 0 && game.TrapHits == hits && Mathf.Approximately(hp, game.hero.hp);
            Require(report.rocksQuietAfterClear, "A boulder falling as the floor cleared still hurt the hero.");
        }

        /// <summary>
        /// Real play, without fixture control: a clear keeps the floor running before its cards and is recorded once, a lone
        /// exit is walked through, and a branch floor's doors still wait for E.
        /// </summary>
        private IEnumerator VerifyClearFlow()
        {
            Enter(report.seed, report.floor, false);
            foreach (var enemy in game.enemies) if (enemy.root) Destroy(enemy.root);
            game.enemies.Clear(); game.DropPendingSpawns(); Call("ResetBossCombat");
            float x = TowerWorld.ExitX - 12; Ready(new Vector2(x, World.GroundAt(x))); game.invulnerable = 999;
            game.SmokeControlled = false;
            try
            {
                Call("Update");
                report.clearRevealsLive = game.run.IsFloorCleared && game.run.IsRewardPending && game.ClearRevealing && !game.Paused;
                int cleared = game.run.ClearedFloors, flawless = game.FlawlessFloors; float seconds = game.LastFloorSeconds;
                for (int i = 0; i < 4; i++) Call("Update");
                report.clearRecordedOnce = game.run.ClearedFloors == cleared && game.FlawlessFloors == flawless && game.LastFloorSeconds == seconds;
            }
            finally { game.SmokeControlled = true; }
            Require(report.clearRevealsLive, "A real clear paused the floor at once instead of playing out before its cards.");
            Require(report.clearRecordedOnce, "Updates while the clear's cards waited recorded the floor again.");

            game.SelectOffer(game.run.Offers[0].Id);
            int floor = game.run.Floor; Ready(new Vector2(TowerWorld.ExitX, World.GroundAt(TowerWorld.ExitX))); game.invulnerable = 999;
            game.SmokeControlled = false; game.enabled = true;
            float until = Time.realtimeSinceStartup + 3;
            try { while (game.run.Floor == floor && Time.realtimeSinceStartup < until) yield return null; }
            finally { game.enabled = false; game.SmokeControlled = true; }
            report.autoExitWalksThrough = game.run.Floor == floor + 1 && game.run.FloorType == FloorType.Normal && !game.run.IsFloorCleared;
            Require(report.autoExitWalksThrough, "Standing in a cleared floor's lone exit did not climb on.");

            Enter(report.seed, TowerRun.RewardInterval, false);
            Require(game.run.IsBranchFloor, "The branch fixture floor offered no doors.");
            foreach (var enemy in game.enemies) if (enemy.root) Destroy(enemy.root);
            game.enemies.Clear(); game.DropPendingSpawns(); Call("ResetBossCombat");
            game.run.ClearFloor(); if (game.run.IsRewardPending) game.SelectOffer(game.run.Offers[0].Id);
            Ready(new Vector2(TowerWorld.ExitX, World.GroundAt(TowerWorld.ExitX))); game.invulnerable = 999;
            game.SmokeControlled = false; game.enabled = true;
            until = Time.realtimeSinceStartup + .5f;
            try { while (Time.realtimeSinceStartup < until) yield return null; }
            finally { game.enabled = false; game.SmokeControlled = true; }
            report.branchWaitsForE = game.run.Floor == TowerRun.RewardInterval && game.run.IsFloorCleared && game.NearestGate() == FloorType.Elite;
            Require(report.branchWaitsForE, "A branch floor left through the elite door without E.");
            report.checks.Add("real play: a clear runs on before its cards and is recorded once; a lone exit climbs on, a branch door waits for E");
        }
        private void VerifyPeaceful()
        {
            Enter(report.seed, report.floor, false, FloorType.Rest);
            bool padsOnly = true; foreach (var trap in game.traps) padsOnly &= trap.spec.Kind == TowerHazardKind.JumpPad;
            int pads = 0; foreach (var h in World.CurrentLayout.Hazards) if (h.Kind == TowerHazardKind.JumpPad) pads++;
            report.peacefulKeepsPadsOnly = padsOnly && game.traps.Count == pads && game.RemainingEnemies == 0;
            Require(report.peacefulKeepsPadsOnly, "A rest floor built traps or monsters.");
            // Rest and shop floors are a short quiet room: flat, trap-free, the campfire at its spot and the exit 28 units past the spawn.
            var room = World.CurrentLayout;
            report.peacefulRoomCompact = Mathf.Approximately(room.ExitX, TowerTerrainGenerator.PeacefulExitX) && Mathf.Approximately(TowerWorld.ExitX, room.ExitX) &&
                Mathf.Approximately(TowerWorld.ExitX - game.hero.pos.x, 28) && room.Hazards.Count == 0 && room.Platforms.Count <= 2 && TowerWorld.FeaturePosition.x < TowerWorld.ExitX - 2;
            Require(report.peacefulRoomCompact, "A rest floor kept the long generated climb.");
            VerifyRestChoice();
        }
        // Fixtures take a rest floor's heal at once; real play offers the heal or a tempering of one level-one skill to level two.
        private void VerifyRestChoice()
        {
            AugmentDefinition first = null, second = null;
            foreach (var a in TowerRun.Augments)
                if (a.OwnerId == "player" && a.Kind == RewardKind.Skill) { if (first == null) first = a; else if (second == null) second = a; }
            var player = game.run.Player;
            Require(player.Apply(first) && player.Apply(second) && player.Apply(second), "The fixture could not teach the player a level-one and a level-two skill.");
            float wounded = game.hero.hp = game.PlayerMaxHealth * .3f;
            game.SmokeControlled = false;
            try { Call("Update"); } finally { game.SmokeControlled = true; }
            report.restOffersChoice = game.run.IsFloorCleared && game.RestChoiceOpen && !game.run.IsRewardPending && game.hero.hp < wounded + game.PlayerMaxHealth * TowerRun.RestHealFraction * .5f;
            Require(report.restOffersChoice, "A real rest floor healed at once instead of offering the campfire's choice.");
            Require(game.ChooseRestTemper() && game.run.IsTempering && game.run.Offers.Count == 1 && game.run.Offers[0].AugmentId == first.Id && game.run.Offers[0].NextLevel == TowerRun.TemperedLevel,
                "Tempering did not offer the lone level-one skill (the level-two one must wait for its awakening card).");
            float before = game.hero.hp; game.SelectOffer(game.run.Offers[0].Id);
            report.restTempers = player.GetSkill(first.Id).Level == TowerRun.TemperedLevel && player.GetSkill(second.Id).Level == TowerRun.TemperedLevel && !game.run.IsRewardPending &&
                !game.RestChoiceOpen && !game.run.BeginTempering() && Mathf.Abs(game.hero.hp - before) < .01f;
            Require(report.restTempers, "Tempering did not raise the level-one skill to level two once, in place of the heal.");
            // A hero who walks out without choosing takes the heal on the way.
            Enter(report.seed, report.floor, false, FloorType.Rest);
            wounded = game.hero.hp = game.PlayerMaxHealth * .3f;
            game.SmokeControlled = false;
            try { Call("Update"); } finally { game.SmokeControlled = true; }
            Require(game.RestChoiceOpen, "The second rest floor did not offer its choice.");
            game.NextFloor();
            report.restLeavesHealed = !game.RestChoiceOpen && game.run.Floor == report.floor + 1 && game.hero.hp >= wounded + game.PlayerMaxHealth * TowerRun.RestHealFraction - .5f;
            Require(report.restLeavesHealed, "Leaving a rest floor undecided did not take the campfire's heal.");
            report.checks.Add("rest and shop floors: a short quiet room; real play offers the campfire's heal or a level-one-to-two tempering, and leaving undecided heals");
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
            // Far off and asleep, the boss shows only on the floor track; its bar heads the screen once it is near or awake.
            report.bossBarWaits = game.ShownBoss() == null;
            Require(report.bossBarWaits, "A sleeping boss across the floor already showed its bar.");
            float x = start - 13; Place(game.hero, new Vector2(x, World.GroundAt(x)));
            for (int i = 0; i < 60; i++) { game.invulnerable = 999; yield return null; }
            report.bossWakes = boss.pos.x < start - .05f;
            Require(report.bossWakes, "The boss did not engage a hero inside its wake range.");
            game.enabled = false;
            report.bossBarShows = game.ShownBoss() == boss;
            Require(report.bossBarShows, "An awake boss beside the hero showed no bar.");
            VerifyStreamedBoss();
        }
        /// <summary>Play queues the boss behind the packs: it holds the floor while queued and appears, fully built, near the hero.</summary>
        private void VerifyStreamedBoss()
        {
            Enter(report.seed, 10, true);
            float bossX = TowerWorld.ExitX - TowerGame.BossStandoff;
            bool queued = game.BossPending && game.enemies.Find(e => e.isBoss) == null && game.ShownBoss() == null;
            // Everything short of the boss's stretch streams in and is removed; the queued boss alone keeps the floor uncleared.
            float x = bossX - TowerGame.SpawnAhead - 1; Place(game.hero, new Vector2(x, World.GroundAt(x))); game.invulnerable = 999;
            for (int i = 0; i < 64; i++) Call("TickSpawnQueue");
            foreach (var enemy in game.enemies) if (enemy.root) Destroy(enemy.root);
            game.enemies.Clear(); Call("Update");
            report.bossStreamed = queued && game.BossPending && game.RemainingEnemies == game.PendingEnemies && !game.run.IsFloorCleared;
            Require(report.bossStreamed, "A played boss floor built its boss on entry, or cleared while the boss was still queued.");
            x = bossX - TowerGame.SpawnAhead + 1; Place(game.hero, new Vector2(x, World.GroundAt(x))); game.invulnerable = 999;
            for (int i = 0; i < 64 && game.BossPending; i++) Call("TickSpawnQueue");
            var boss = game.enemies.Find(e => e.isBoss);
            report.bossStreamsIn = boss != null && Mathf.Abs(boss.pos.x - bossX) < .01f && boss.monster == game.run.Theme.Boss && boss.hp == boss.maxHp
                && Mathf.Abs(boss.maxHp - TowerDifficulty.BossHealth(10) * boss.monster.HealthMultiplier) < .01f && game.ShownBoss() == boss;
            Require(report.bossStreamsIn, "The queued boss did not appear, fully built and with its bar, as the hero reached its stretch.");
            report.checks.Add("boss: holds its stretch until woken, bar hidden while far and asleep; play queues it and builds it " + TowerGame.SpawnAhead + " ahead of the hero");
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
