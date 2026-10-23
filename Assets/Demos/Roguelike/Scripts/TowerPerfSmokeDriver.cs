using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Explicit opt-in profiling fixture. A full party fights real Update combat with player inputs for a few
    // seconds per floor, then the floor is cleared and the next one is spawned, crossing two theme changes.
    // Every frame records wall time, the tower's profiler markers and Unity's GC/render counters.
    public sealed class TowerPerfSmokeDriver : MonoBehaviour
    {
        [Serializable] private sealed class FloorBuild { public int floor; public string theme; public float frameMs, spawnFloorMs, setThemeMs, generateMs, terrainMs, backdropMs, gateMs, actorsMs, musicMs; }
        [Serializable] private sealed class MarkerStat { public string name; public float meanMs, p95Ms, maxMs; }
        [Serializable] private sealed class PerfReport
        {
            public string mode = "towerPerf", finishedUtc, framesCsv;
            public bool success;
            public int frames, combatFrames, framesOver20, framesOver33, framesOver50, gcCollections;
            public float meanMs, p50Ms, p95Ms, p99Ms, maxMs, meanGcAllocKb, maxGcAllocKb;
            public List<string> recorded = new List<string>(), missing = new List<string>(), spikes = new List<string>(), errors = new List<string>();
            public List<FloorBuild> floorBuilds = new List<FloorBuild>();
            public List<MarkerStat> markers = new List<MarkerStat>();
        }
        private sealed class Sample { public float realMs; public int floor; public string theme; public bool spawned, combat; public double[] values; }

        private static readonly string[] Builtin = { "Main Thread", "PlayerLoop", "GC.Collect", "GC Allocated In Frame", "Camera.Render", "BehaviourUpdate", "Gfx.WaitForPresentOnGfxThread", "GUI.Repaint", "GUI.Layout" };
        private const int Seed = 97, Floors = 11;
        private const float CombatSeconds = 6;
        private readonly PerfReport report = new PerfReport();
        private readonly List<Sample> samples = new List<Sample>();
        private readonly List<string> names = new List<string>();
        private readonly List<ProfilerRecorder> recorders = new List<ProfilerRecorder>();
        private readonly List<bool> timeUnits = new List<bool>();
        private readonly Stopwatch clock = new Stopwatch();
        private TowerGame game;
        private string prefix, reportPath;
        private float deadline;
        private bool finished, initialized, quitOnFinish, recording, combat;
        private int lastFloor, gcAtStart;

        public void Initialize(TowerGame tower, TowerSmokeDriver captureDriver, string capturePrefix, string outputPath, bool quit = false)
        {
            if (!tower) throw new ArgumentException("A live game is required.");
            game = tower; Configure(capturePrefix, outputPath, quit);
        }
        private void Configure(string capturePrefix, string outputPath, bool quit)
        {
            if (initialized) throw new InvalidOperationException("The perf suite was already initialized.");
            if (!Path.IsPathRooted(capturePrefix) || !Path.IsPathRooted(outputPath)) throw new ArgumentException("Perf output paths must be absolute.");
            prefix = Path.GetFullPath(capturePrefix); reportPath = Path.GetFullPath(outputPath); quitOnFinish = quit;
            initialized = true; deadline = Time.realtimeSinceStartup + 600;
            Application.logMessageReceived += RecordLog;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Enter()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-towerPerf") < 0) return;
            var driver = new GameObject("Tower perf validation").AddComponent<TowerPerfSmokeDriver>();
            driver.Configure(Arg(args, "-towerPerfPrefix") ?? Path.Combine(Application.persistentDataPath, "tower-perf"),
                Arg(args, "-towerPerfReport") ?? Path.Combine(Application.persistentDataPath, "tower-perf.json"), true);
        }
        private static string Arg(string[] args, string key) { int at = Array.IndexOf(args, key); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }
        private void RecordLog(string message, string trace, LogType type)
        { if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && report.errors.Count < 32) report.errors.Add(message + "\n" + trace); }
        private void OnDestroy() { Application.logMessageReceived -= RecordLog; foreach (var r in recorders) r.Dispose(); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        private IEnumerator Start()
        {
            if (!initialized) yield break;
            var body = Suite(); Exception error = null;
            while (true)
            {
                bool more;
                try { more = body.MoveNext(); } catch (Exception caught) { error = caught; break; }
                if (!more) break;
                yield return body.Current;
            }
            Finish(error);
        }

        private void StartRecorders()
        {
            var wanted = new List<string>(TowerProfiling.Names); wanted.AddRange(Builtin);
            var available = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(available);
            foreach (string name in wanted)
            {
                bool found = false;
                foreach (var handle in available)
                {
                    var description = ProfilerRecorderHandle.GetDescription(handle);
                    if (description.Name != name) continue;
                    recorders.Add(new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately));
                    timeUnits.Add(description.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds);
                    names.Add(name); found = true; break;
                }
                if (found) report.recorded.Add(name); else report.missing.Add(name);
            }
        }

        private void Update()
        {
            if (initialized && !finished && Time.realtimeSinceStartup > deadline) { StopAllCoroutines(); Finish(new TimeoutException("Perf suite exceeded 600 seconds.")); return; }
            if (!recording) return;
            // Recorders hold the previous frame, matching the wall time measured between two Updates.
            var sample = new Sample { realMs = (float)clock.Elapsed.TotalMilliseconds, floor = game.run.Floor, theme = game.run.Theme.Id.ToString(), combat = combat, values = new double[recorders.Count] };
            clock.Restart();
            sample.spawned = sample.floor != lastFloor; lastFloor = sample.floor;
            for (int i = 0; i < recorders.Count; i++) sample.values[i] = timeUnits[i] ? recorders[i].LastValue * 1e-6 : recorders[i].LastValue / 1024.0;
            samples.Add(sample);
        }

        private IEnumerator Suite()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            for (int frame = 0; frame < 180 && game == null; frame++) { game = FindFirstObjectByType<TowerGame>(); if (game == null) yield return null; }
            Require(game != null, "The roguelike game did not load.");
            game.progress = new TowerProgress();
            game.SmokeControlled = false; game.NewRun(Seed); game.SmokeControlled = true; game.SuppressProgressPersistence = true;
            game.StartClimb();
            if (game.run.IsRewardPending) game.SelectOffer(game.run.Offers[0].Id);
            // A full party is the heaviest per-frame load a real climb reaches.
            var roster = (IList)typeof(TowerRun).GetField("companions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.run);
            foreach (var definition in TowerRun.Definitions)
            {
                if (game.run.Companions.Count >= TowerRun.MaxCompanions) break;
                if (game.run.GetCompanion(definition.Id) == null) roster.Add(new CompanionState(definition));
            }
            game.ReconcileLoadout();
            StartRecorders();
            gcAtStart = GC.CollectionCount(0); lastFloor = game.run.Floor;
            // Let the first floor settle before measuring.
            for (int i = 0; i < 30; i++) yield return null;
            clock.Restart(); recording = true;
            for (int floor = 1; floor <= Floors; floor++)
            {
                yield return Fight();
                foreach (var enemy in game.enemies) if (enemy.root) Destroy(enemy.root);
                game.enemies.Clear();
                float limit = Time.realtimeSinceStartup + 3;
                while (!game.run.IsFloorCleared) { Require(Time.realtimeSinceStartup < limit, "The emptied floor did not clear."); yield return null; }
                while (game.run.IsRewardPending) { game.SelectOffer(game.run.Offers[0].Id); yield return null; }
                if (floor == Floors) break;
                // Branch floors offer no plain exit; the elite door keeps the next floor a combat floor.
                game.NextFloor(game.run.IsBranchFloor ? FloorType.Elite : FloorType.Normal);
                yield return null;
            }
            for (int i = 0; i < 10; i++) yield return null;
            recording = false;
        }

        private IEnumerator Fight()
        {
            combat = true; float until = game.elapsed + CombatSeconds, nextJump = game.elapsed + .6f, nextDash = game.elapsed + 1.7f;
            float limit = Time.realtimeSinceStartup + 30;
            game.AttackOverride = true;
            while (game.elapsed < until && Time.realtimeSinceStartup < limit && game.enemies.Count > 0)
            {
                game.invulnerable = 1000; game.hero.hp = game.PlayerMaxHealth;
                TowerGame.Fighter nearest = null; float best = float.MaxValue;
                foreach (var enemy in game.enemies) { float d = Vector2.Distance(enemy.pos, game.hero.pos); if (d < best) { best = d; nearest = enemy; } }
                float dx = nearest != null ? nearest.pos.x - game.hero.pos.x : 0;
                game.MoveOverride = Mathf.Abs(dx) > 1.2f ? Mathf.Sign(dx) : 0;
                game.JumpOverride = game.elapsed >= nextJump; if (game.JumpOverride) nextJump = game.elapsed + 1.1f;
                game.DashOverride = game.elapsed >= nextDash; if (game.DashOverride) nextDash = game.elapsed + 2.6f;
                yield return null;
            }
            game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = false; combat = false;
        }

        private static float Percentile(List<float> sorted, float p) { return sorted.Count == 0 ? 0 : sorted[Mathf.Clamp(Mathf.CeilToInt(p * sorted.Count) - 1, 0, sorted.Count - 1)]; }
        private double Value(Sample s, string name) { int i = names.IndexOf(name); return i >= 0 ? s.values[i] : 0; }

        private void Finish(Exception error)
        {
            if (finished) return; finished = true; recording = false;
            if (error != null) report.errors.Add(error.ToString());
            if (game != null) { game.MoveOverride = 0; game.AttackOverride = game.JumpOverride = game.DashOverride = false; game.enabled = false; }
            // The first sample spans the recording start; skip it.
            if (samples.Count > 0) samples.RemoveAt(0);
            var times = new List<float>(); float gcSum = 0;
            foreach (var s in samples) { times.Add(s.realMs); float kb = (float)Value(s, "GC Allocated In Frame"); gcSum += kb; report.maxGcAllocKb = Mathf.Max(report.maxGcAllocKb, kb); if (s.combat) report.combatFrames++; }
            report.frames = samples.Count;
            if (samples.Count > 0)
            {
                var sorted = new List<float>(times); sorted.Sort();
                float sum = 0; foreach (float t in times) sum += t;
                report.meanMs = sum / times.Count; report.p50Ms = Percentile(sorted, .5f); report.p95Ms = Percentile(sorted, .95f); report.p99Ms = Percentile(sorted, .99f); report.maxMs = sorted[sorted.Count - 1];
                foreach (float t in times) { if (t > 20) report.framesOver20++; if (t > 33.3f) report.framesOver33++; if (t > 50) report.framesOver50++; }
                report.meanGcAllocKb = gcSum / samples.Count;
            }
            report.gcCollections = GC.CollectionCount(0) - gcAtStart;
            for (int i = 0; i < names.Count; i++)
            {
                if (!timeUnits[i]) continue;
                var values = new List<float>(); foreach (var s in samples) values.Add((float)s.values[i]);
                values.Sort(); float sum = 0; foreach (float v in values) sum += v;
                report.markers.Add(new MarkerStat { name = names[i], meanMs = values.Count > 0 ? sum / values.Count : 0, p95Ms = Percentile(values, .95f), maxMs = values.Count > 0 ? values[values.Count - 1] : 0 });
            }
            foreach (var s in samples)
                if (s.spawned)
                    report.floorBuilds.Add(new FloorBuild { floor = s.floor, theme = s.theme, frameMs = s.realMs, spawnFloorMs = (float)Value(s, "Tower.SpawnFloor"), generateMs = (float)Value(s, "Tower.Generate"),
                        terrainMs = (float)Value(s, "Tower.Terrain"), backdropMs = (float)Value(s, "Tower.Backdrop"), gateMs = (float)Value(s, "Tower.Gate"),
                        setThemeMs = (float)Value(s, "Tower.SetTheme"), actorsMs = (float)Value(s, "Tower.Actors"), musicMs = (float)Value(s, "Tower.Music") });
            var order = new List<int>(); for (int i = 0; i < samples.Count; i++) order.Add(i);
            order.Sort((a, b) => samples[b].realMs.CompareTo(samples[a].realMs));
            for (int k = 0; k < Mathf.Min(25, order.Count); k++)
            {
                var s = samples[order[k]]; var text = new StringBuilder();
                text.Append("frame ").Append(order[k]).Append(" floor ").Append(s.floor).Append(s.spawned ? " [spawn]" : "").Append(s.combat ? " [combat]" : "").Append(": ").Append(s.realMs.ToString("F1")).Append(" ms");
                for (int i = 0; i < names.Count; i++) if (s.values[i] >= (timeUnits[i] ? .5 : 64)) text.Append(" | ").Append(names[i]).Append('=').Append(s.values[i].ToString("F1")).Append(timeUnits[i] ? "ms" : "KB");
                report.spikes.Add(text.ToString());
            }
            try
            {
                report.framesCsv = prefix + "-frames.csv";
                var csv = new StringBuilder("frame,floor,theme,spawned,combat,realMs");
                foreach (string name in names) csv.Append(',').Append(name);
                csv.Append('\n');
                for (int k = 0; k < samples.Count; k++)
                {
                    var s = samples[k];
                    csv.Append(k).Append(',').Append(s.floor).Append(',').Append(s.theme).Append(',').Append(s.spawned ? 1 : 0).Append(',').Append(s.combat ? 1 : 0).Append(',').Append(s.realMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                    foreach (double v in s.values) csv.Append(',').Append(v.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                    csv.Append('\n');
                }
                Directory.CreateDirectory(Path.GetDirectoryName(report.framesCsv)); File.WriteAllText(report.framesCsv, csv.ToString());
            }
            catch (Exception e) { report.errors.Add("CSV: " + e.Message); }
            report.finishedUtc = DateTime.UtcNow.ToString("o");
            report.success = error == null && report.errors.Count == 0 && report.frames > 0;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Application.logMessageReceived -= RecordLog;
            if (quitOnFinish) Application.Quit(report.success ? 0 : 1);
        }
    }
}
