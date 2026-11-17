using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    // R82: frame-time and motion log for the diver's dive-in and the catch stow (Play mode only, nothing saved).
    // An autopilot plays the local diver in a practice round (swims to a target, spears it, holds still for the
    // reel and the stow); every rendered frame is logged (frame time, main thread, GC, profiler markers, the
    // camera, the diver and the skewered body) to Temp/ClaudeQA/R82_<label>.csv. With profile on, the profiler
    // hierarchy of every frame over spikeMs is written to R82_<label>_spikes.txt.
    public static class AquariumR82QA
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
        public static string Status = "idle";
        public static string Folder { get { return Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp/ClaudeQA"); } }
        static readonly string[] MarkerNames = { "GC.Collect", "Shader.CreateGPUProgram", "Mesh.UploadMeshData", "Instantiate", "GUI.Repaint", "Physics.Simulate", "Gfx.WaitForPresentOnGfxThread", "Aquarium.Draw", "Aquarium.Session", "Aquarium.Camera", "Aquarium.Target", "Loading.ReadObject", "EditorLoop", "PlayerLoop", "Camera.Render" };
        const int Cap = 60000;
        struct Row
        {
            public int frame, gc0, harp, caught; public float rt, dt, st, entry, stowS; public long main, gcRec, gcThread;
            public Vector3 cam, diver, body; public Quaternion camRot, bodyRot; public Vector3 bodyScale; public bool hasBody, dumped; public int fx;
        }
        static Row[] rows; static long[] marks; static int count;
        static ProfilerRecorder[] recorders; static ProfilerRecorder mainRec, gcRec;
        static AquariumGame game; static AquariumSession session; static AquariumView view; static object followCamera;
        static FieldInfo pauseField, actorRoot; static IDictionary actors; static MethodInfo fishKey, playerKey;
        static string label, plan; static int planIndex, team; static float began, maxSeconds, spikeMs, doneAt, fireAt, targetSince;
        static bool hooked, profile, caughtThis, hadShot; static int targetId, lastFx, pendingDump, dumpCooldown, lastHarpoon, lastGc; static long lastThread;
        static Transform diverT, bodyT; static int diverId; static string bodyKey;
        static readonly List<string> fxNames = new List<string>(); static readonly HashSet<int> skipped = new HashSet<int>();
        static readonly StringBuilder spikes = new StringBuilder(); static readonly StringBuilder notes = new StringBuilder();
        static Point lastAim = new Point(0, 0, 1);

        public static string Begin(string name, int map, int asTeam, string targets, float seconds, bool withProfiler, float spikeAtMs)
        {
            if (!EditorApplication.isPlaying) return "not playing";
            game = UnityEngine.Object.FindFirstObjectByType<AquariumGame>(); if (game == null) return "no game";
            session = (AquariumSession)typeof(AquariumGame).GetField("session", Any).GetValue(game);
            view = (AquariumView)typeof(AquariumGame).GetField("view", Any).GetValue(game);
            followCamera = typeof(AquariumGame).GetField("followCamera", Any).GetValue(game);
            pauseField = typeof(AquariumGame).GetField("pause", Any);
            typeof(AquariumGame).GetField("automated", Any).SetValue(game, true);
            actors = (IDictionary)typeof(AquariumView).GetField("actors", Any).GetValue(view);
            actorRoot = typeof(AquariumView).GetNestedType("Actor", Any).GetField("root", Any);
            fishKey = typeof(AquariumView).GetMethod("FishKey", Any); playerKey = typeof(AquariumView).GetMethod("PlayerKey", Any);
            label = name; plan = targets ?? ""; team = asTeam; maxSeconds = seconds; profile = withProfiler; spikeMs = spikeAtMs;
            planIndex = 0; doneAt = 0; fireAt = 0; targetId = 0; caughtThis = hadShot = false; lastFx = 0; pendingDump = 0; dumpCooldown = 0; lastHarpoon = 0;
            diverT = bodyT = null; diverId = 0; bodyKey = null; fxNames.Clear(); skipped.Clear(); spikes.Length = 0; notes.Length = 0;
            if (rows == null) { rows = new Row[Cap]; marks = new long[Cap * MarkerNames.Length]; }
            count = 0;
            DisposeRecorders();
            recorders = new ProfilerRecorder[MarkerNames.Length];
            for (int i = 0; i < MarkerNames.Length; i++) { recorders[i] = new ProfilerRecorder(MarkerNames[i], 1, ProfilerRecorderOptions.Default); recorders[i].Start(); }
            mainRec = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            gcRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            if (profile) { ProfilerDriver.ClearAllFrames(); ProfilerDriver.profileEditor = false; UnityEngine.Profiling.Profiler.enabled = true; ProfilerDriver.enabled = true; }
            session.PreferredMap = map;
            session.StartPractice("QA", asTeam == 0 ? Team.Seeker : Team.Hider);
            began = Time.realtimeSinceStartup; lastGc = GC.CollectionCount(0); lastThread = GC.GetAllocatedBytesForCurrentThread();
            if (!hooked) { EditorApplication.update += Tick; Camera.onPreRender += Sample; hooked = true; }
            Status = "running " + name;
            return Status + " markers:" + Valid();
        }
        static string Valid() { var s = ""; for (int i = 0; i < MarkerNames.Length; i++) s += " " + MarkerNames[i] + "=" + (recorders[i].Valid ? "ok" : "na"); return s + " main=" + mainRec.Valid + " gc=" + gcRec.Valid; }
        static void DisposeRecorders()
        {
            if (recorders != null) { foreach (var r in recorders) r.Dispose(); recorders = null; mainRec.Dispose(); gcRec.Dispose(); }
        }
        public static string End(string why = "ended")
        {
            if (hooked) { EditorApplication.update -= Tick; Camera.onPreRender -= Sample; hooked = false; }
            if (profile) { ProfilerDriver.enabled = false; UnityEngine.Profiling.Profiler.enabled = false; }
            if (count > 0 && label != null) Write();
            DisposeRecorders();
            if (game != null) typeof(AquariumGame).GetField("automated", Any).SetValue(game, false);
            Status = why + " " + label + " frames=" + count + " plan=" + planIndex + "/" + (plan ?? "").Length;
            return Status;
        }

        // ---------------------------------------------------------------- per rendered frame
        static void Sample(Camera cam)
        {
            if (view == null || cam != view.Camera || count >= Cap || session == null) return;
            var state = session.State; if (state == null) return;
            var r = new Row { frame = Time.frameCount, rt = Time.realtimeSinceStartup - began, dt = Time.unscaledDeltaTime * 1000f, st = state.time, entry = float.NaN, stowS = float.NaN };
            r.main = mainRec.Valid ? mainRec.LastValue : 0; r.gcRec = gcRec.Valid ? gcRec.LastValue : 0;
            long bytes = GC.GetAllocatedBytesForCurrentThread(); r.gcThread = bytes - lastThread; lastThread = bytes;
            int gc = GC.CollectionCount(0); r.gc0 = gc - lastGc; lastGc = gc;
            for (int i = 0; i < recorders.Length; i++) marks[count * MarkerNames.Length + i] = recorders[i].Valid ? recorders[i].LastValue : 0;
            r.cam = cam.transform.position; r.camRot = cam.transform.rotation;
            Swimmer diver = null; foreach (var p in state.players) if (p.team == Team.Seeker) { diver = p; break; }
            if (diver != null)
            {
                if (diver.entryAt > 0) r.entry = state.time - (diver.entryAt - AquariumDiveEntry.Seconds);
                if (diverT == null || diverId != diver.id) { diverT = Root((string)playerKey.Invoke(view, new object[] { diver.id })); diverId = diver.id; }
                if (diverT != null) r.diver = diverT.position;
            }
            Harpoon shot = null; if (diver != null) foreach (var h in state.harpoons) if (h.owner == diver.id) { shot = h; break; }
            if (shot != null)
            {
                bool caught = shot.caughtFish != 0 || shot.caughtPlayer != 0;
                r.caught = shot.caughtFish != 0 ? 1 : shot.caughtPlayer != 0 ? 2 : 0;
                r.harp = !shot.returning ? 1 : !caught ? 3 : state.time < shot.retrievedAt ? 2 : 4;
                if (caught) r.stowS = state.time - shot.retrievedAt;
                if (caught && (lastHarpoon != shot.id || bodyT == null))
                {
                    lastHarpoon = shot.id;
                    bodyKey = shot.caughtFish != 0 ? (string)fishKey.Invoke(view, new object[] { shot.caughtFish }) : (string)playerKey.Invoke(view, new object[] { shot.caughtPlayer });
                    bodyT = Root(bodyKey);
                }
                if (caught && bodyT != null) { r.hasBody = true; r.body = bodyT.position; r.bodyRot = bodyT.rotation; r.bodyScale = bodyT.localScale; }
            }
            else bodyT = null;
            if (state.effects != null) foreach (var e in state.effects) if (e.id > lastFx) { lastFx = e.id; fxNames.Add(e.kind); r.fx = fxNames.Count; }
            if (dumpCooldown > 0) { dumpCooldown--; r.dumped = true; }
            else if (profile && r.dt > spikeMs && count > 5) { pendingDump = 2; dumpCooldown = 12; notes.AppendLine("spike frame=" + r.frame + " dt=" + r.dt.ToString("0.0") + " rt=" + r.rt.ToString("0.00") + " entry=" + r.entry.ToString("0.00") + " stowS=" + r.stowS.ToString("0.00") + " harp=" + r.harp); }
            rows[count++] = r;
        }
        static Transform Root(string key)
        {
            if (key == null || !actors.Contains(key)) return null;
            var actor = actors[key]; if (actor == null) return null;
            var go = (GameObject)actorRoot.GetValue(actor); return go != null ? go.transform : null;
        }

        // ---------------------------------------------------------------- autopilot
        static void Tick()
        {
            if (!EditorApplication.isPlaying || game == null) { End("stopped: not playing"); return; }
            try { Drive(); } catch (Exception e) { End("error: " + e); }
        }
        static void Drive()
        {
            pauseField.SetValue(game, false);
            if (pendingDump > 0 && --pendingDump == 0) DumpSpike();
            float now = Time.realtimeSinceStartup;
            var model = session.Model; if (model == null) { End("no model"); return; }
            if (now - began > maxSeconds || count >= Cap - 2) { End("done(time)"); return; }
            if (model.Phase == RoundPhase.Finished) { if (doneAt == 0) doneAt = now + 1f; if (now > doneAt) End("done(round over)"); return; }
            var p = model.Player(session.LocalId); if (p == null) return;
            if (team != 0) return;   // watching as an alien: no input
            var input = new SwimInput { aimX = lastAim.x, aimY = lastAim.y, aimZ = lastAim.z };
            if (model.Time < model.HuntAt + .6f) { lastAim = p.facing; session.Input(new SwimInput { aimX = p.facing.x, aimY = p.facing.y, aimZ = p.facing.z }); return; }
            Harpoon mine = null; foreach (var h in model.Harpoons) if (h.owner == p.id) { mine = h; break; }
            if (mine != null)
            {
                hadShot = true; if (mine.caughtFish != 0 || mine.caughtPlayer != 0) caughtThis = true;
                session.Input(input); return;   // hold still: watch the reel and the stow
            }
            if (hadShot) { hadShot = false; if (caughtThis) { planIndex++; targetId = 0; skipped.Clear(); } caughtThis = false; }
            if (planIndex >= plan.Length) { if (doneAt == 0) doneAt = now + 2f; if (now > doneAt) End("done"); session.Input(input); return; }
            char want = plan[planIndex];
            Point at = p.position; bool found = false; Swimmer alien = null;
            if (want == 'f')
            {
                Prey best = null; float bestD = float.MaxValue;
                foreach (var f in model.Fish)
                {
                    if (f.dead || f.eaten || f.collected || skipped.Contains(f.id)) continue;
                    if (targetId != 0 && f.id != targetId) continue;
                    float d = (f.position - p.position).Length; if (d < bestD) { bestD = d; best = f; }
                }
                if (best == null && targetId != 0) { targetId = 0; return; }
                if (best != null) { if (targetId != best.id) { targetId = best.id; targetSince = now; } at = best.position; found = true; }
            }
            else
            {
                float bestD = float.MaxValue; int alive = 0;
                foreach (var a in model.Players)
                {
                    if (a.team != Team.Hider) continue;
                    if (a.alive) alive++;
                    bool corpse = !a.alive && a.deathReason == "starved" && !a.collected;
                    if (targetId != 0 ? a.id != targetId : (!a.alive || a.swallowedBy != 0 || skipped.Contains(a.id))) continue;
                    if (targetId != 0 && !(a.alive || corpse)) continue;
                    float d = (a.position - p.position).Length; if (d < bestD) { bestD = d; alien = a; }
                }
                if (alien == null && targetId != 0) { targetId = 0; return; }
                if (alien != null)
                {
                    if (targetId != alien.id) { targetId = alien.id; targetSince = now; }
                    if (want == 'c' && alien.alive && alive >= 2 && (alien.position - p.position).Length < 7f)
                    { alien.alive = false; alien.deathReason = "starved"; alien.deathAt = model.Time - 10; alien.diedAt = model.Time - 10; alien.velocity = new Point(); }
                    at = alien.position; found = true;
                }
            }
            if (!found) { session.Input(input); return; }
            if (now - targetSince > 14f) { skipped.Add(targetId); targetId = 0; notes.AppendLine("gave up on target at " + (now - began).ToString("0.0")); return; }
            var d2 = at - p.position; float range = d2.Length; var aim = range > .01f ? d2.Normalized : lastAim;
            lastAim = aim; input.aimX = aim.x; input.aimY = aim.y; input.aimZ = aim.z;
            if (range > 3.6f) { input.x = aim.x; input.y = aim.y; input.z = aim.z; input.sprint = range > 9; }
            else if (range < 2.2f) { input.x = -aim.x * .6f; input.y = -aim.y * .6f; input.z = -aim.z * .6f; }
            Look(aim);
            bool clear = AquariumModel.ObstacleHit(p.position, at) > 1;
            if (range < 4.6f && clear && model.Time >= p.nextAction + .15f && now >= fireAt)
            {
                if (alien != null && alien.alive) alien.harpoonHits = AquariumModel.HitsToKill(alien) - 1;
                input.action = true; fireAt = now + .5f;
                notes.AppendLine("fire " + want + " at rt=" + (now - began).ToString("0.00") + " range=" + range.ToString("0.0"));
            }
            session.Input(input);
        }
        static void Look(Point aim)
        {
            if (followCamera == null) return;
            var type = followCamera.GetType();
            float yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg, pitch = -Mathf.Asin(Mathf.Clamp(aim.y, -1, 1)) * Mathf.Rad2Deg;
            type.GetField("targetYaw", Any).SetValue(followCamera, yaw); type.GetField("targetPitch", Any).SetValue(followCamera, Mathf.Clamp(pitch, -60, 60));
        }

        // ---------------------------------------------------------------- profiler hierarchy of the worst recent frame
        struct Item { public string name; public float self, total, gc; public int calls; }
        static void DumpSpike()
        {
            int last = ProfilerDriver.lastFrameIndex, first = Math.Max(ProfilerDriver.firstFrameIndex, last - 8), worst = -1; float worstMs = 0;
            for (int f = last; f >= first; f--)
                using (var v = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnDontSort, false))
                { if (v != null && v.valid && v.frameTimeMs > worstMs) { worstMs = v.frameTimeMs; worst = f; } }
            if (worst < 0) { spikes.AppendLine("(no profiler frame)"); return; }
            using (var v = ProfilerDriver.GetHierarchyFrameDataView(worst, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnDontSort, false))
            {
                var items = new List<Item>(); var stack = new Stack<int>(); var kids = new List<int>();
                stack.Push(v.GetRootItemID());
                while (stack.Count > 0)
                {
                    int id = stack.Pop();
                    items.Add(new Item { name = v.GetItemName(id), self = v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime), total = v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime), gc = v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnGcMemory), calls = (int)v.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnCalls) });
                    kids.Clear(); v.GetItemChildren(id, kids); foreach (var k in kids) stack.Push(k);
                }
                spikes.AppendLine("=== profiler frame " + worst + " " + worstMs.ToString("0.0") + " ms  (" + (notes.Length > 0 ? notes.ToString().TrimEnd().Substring(Math.Max(0, notes.ToString().TrimEnd().LastIndexOf('\n') + 1)) : "") + ")");
                items.Sort((a, b) => b.self.CompareTo(a.self));
                for (int i = 0; i < Math.Min(22, items.Count); i++) spikes.AppendLine(string.Format("   self {0,7:0.00}  total {1,7:0.00}  gc {2,8:0}  x{3,-4} {4}", items[i].self, items[i].total, items[i].gc, items[i].calls, items[i].name));
                items.Sort((a, b) => b.total.CompareTo(a.total));
                spikes.Append("   by total:"); for (int i = 0; i < Math.Min(14, items.Count); i++) spikes.Append(" " + items[i].name + "=" + items[i].total.ToString("0.0")); spikes.AppendLine();
            }
        }

        // ---------------------------------------------------------------- a client's view of a catch
        // A small stage (own model and view, as AquariumR77QA): the model ticks 30 times a second, but the view only
        // gets a snapshot (through JSON, as over the network) `hz` times a second and draws every frame on the
        // client's presentation clock. The diver spears an alien 5.5 m ahead; the skewered body's place is logged
        // every frame, seen by the diver (first person) or by another alien. frameClock off = the pre-R82 behaviour.
        static GameObject stageParent, hiddenGame; static AquariumView stageView; static Action stageTick;
        public static string StartClient(string name, float hz, bool firstPerson, bool frameClock)
        {
            if (!EditorApplication.isPlaying) return "not playing";
            EndClient();
            AquariumView.FrameClock = frameClock;
            var g = UnityEngine.Object.FindFirstObjectByType<AquariumGame>();
            if (g != null) { hiddenGame = g.gameObject; hiddenGame.SetActive(false); }
            stageParent = new GameObject("R82 QA stage"); var v = stageView = new AquariumView(stageParent.transform);
            var model = new AquariumModel(); model.AddPlayer(1, "Alien", Team.Hider); model.AddPlayer(2, "Diver", Team.Seeker); model.AddPlayer(3, "Alien 2", Team.Hider);
            model.SetRole(2, Team.Seeker, true); model.SetRole(3, Team.Hider, true);
            if (!model.StartRound()) return "round did not start";
            model.Players.RemoveAll(p => p.filled);
            for (int i = 0; i < 2000 && model.Time < model.HuntAt + .1f; i++) model.Tick(1f / 30);
            var diver = model.Player(2); diver.position = new Point(2.3f, 9, 4); diver.facing = new Point(0, 0, 1); diver.entryAt = 0;
            var alien = model.Player(1); alien.position = new Point(2.3f, 9, 9.5f); alien.facing = new Point(-1, 0, 0); alien.lastMealAt = float.MaxValue; alien.starveAt = float.MaxValue;
            alien.harpoonHits = AquariumModel.HitsToKill(alien) - 1;
            model.Player(3).position = new Point(-1.5f, 9.6f, 9); model.Player(3).lastMealAt = float.MaxValue; model.Player(3).starveAt = float.MaxValue;
            foreach (var f in model.Fish) { f.position = new Point(-20, 8, -5); f.facing = new Point(1, 0); f.turnAt = float.MaxValue; f.steering = new Point(); f.swimSpeed = 0; }
            var actorsOf = (IDictionary)typeof(AquariumView).GetField("actors", Any).GetValue(v);
            var rootOf = typeof(AquariumView).GetNestedType("Actor", Any).GetField("root", Any);
            string key = (string)typeof(AquariumView).GetMethod("PlayerKey", Any).Invoke(v, new object[] { 1 });
            AquariumSnapshot state = null; float acc = 0, sinceSnapshot = 1, clock = float.NaN, t0 = 0; int frame = 0; bool fired = false;
            var rel = new List<Vector3>(); var phase = new List<int>(); var times = new List<float>();
            stageTick = () =>
            {
                frame++;
                float dt = Mathf.Clamp(Time.unscaledDeltaTime, .002f, .05f);
                if (frame == 5) { model.SetInput(2, new SwimInput { aimZ = 1, action = true }); fired = true; t0 = model.Time; }
                acc += dt; while (acc >= 1f / 30) { model.Tick(1f / 30); acc -= 1f / 30; if (fired) model.SetInput(2, new SwimInput { aimZ = 1 }); }
                sinceSnapshot += dt;
                if (state == null || sinceSnapshot >= 1f / hz) { sinceSnapshot %= 1f / hz; state = JsonUtility.FromJson<AquariumSnapshot>(JsonUtility.ToJson(model.Snapshot())); }
                // AquariumSession's clock on a client: runs every frame, kept just behind the last snapshot.
                clock = float.IsNaN(clock) ? state.time - .05f : Mathf.Clamp(clock + dt, state.time - .15f, state.time);
                var d = model.Player(2);
                var cam = v.Camera.transform;
                if (firstPerson) cam.SetPositionAndRotation(new Vector3(d.position.x, d.position.y + .15f, d.position.z + .4f), Quaternion.LookRotation(Vector3.forward));
                else { cam.position = new Vector3(-1.5f, 9.9f, 9.2f); cam.LookAt(new Vector3(d.position.x, d.position.y, d.position.z + 1.5f)); }
                v.Draw(state, firstPerson ? 2 : 3, dt, clock);
                v.PresentEquipment(state, firstPerson ? 2 : 3, firstPerson);
                Harpoon shot = state.harpoons.Length > 0 ? state.harpoons[0] : null;
                if (shot != null && shot.caughtPlayer != 0 && actorsOf.Contains(key))
                {
                    var go = (GameObject)rootOf.GetValue(actorsOf[key]);
                    if (go != null) { rel.Add(cam.InverseTransformPoint(go.transform.position)); phase.Add(state.time < shot.retrievedAt - .6f ? 0 : state.time < shot.retrievedAt ? 1 : 2); times.Add(dt); }
                }
                if (fired && (model.Time - t0 > 9f || (shot == null && model.Time - t0 > 1f)))
                {
                    var names = new[] { "reel", "dock (last .6 s)", "stow" }; var text = new StringBuilder(name + " hz=" + hz + " firstPerson=" + firstPerson + " frameClock=" + frameClock + " frames=" + rel.Count + "\n");
                    for (int ph = 0; ph < 3; ph++)
                    {
                        int n = 0, frozen = 0; double sum = 0, rough = 0, last = -1, ms = 0;
                        for (int i = 1; i < rel.Count; i++)
                        {
                            if (phase[i] != ph || phase[i - 1] != ph) continue;
                            double step = (rel[i] - rel[i - 1]).magnitude; n++; ms += times[i] * 1000; if (step <= 1e-6) frozen++; sum += step; if (last >= 0) rough += Math.Abs(step - last); last = step;
                        }
                        if (n > 0) text.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture, "  {0,-17} frames={1,3} avgFrame={2:0.0}ms frozen={3,3} ({4:0}%) roughness={5:0.00}\n", names[ph], n, ms / n, frozen, 100.0 * frozen / n, rough / Math.Max(1e-9, sum)));
                    }
                    Directory.CreateDirectory(Folder); File.AppendAllText(Path.Combine(Folder, "R82_client_stage.txt"), text.ToString());
                    Status = "client done " + name; EndClient();
                }
            };
            EditorApplication.update += ClientTick; Status = "client running " + name;
            return Status;
        }
        static void ClientTick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= ClientTick; stageTick = null; return; }
            try { if (stageTick != null) stageTick(); } catch (Exception e) { Status = "client error: " + e; EndClient(); }
        }
        public static void EndClient()
        {
            EditorApplication.update -= ClientTick; stageTick = null; AquariumView.FrameClock = true;
            if (stageView != null) { try { stageView.Dispose(); } catch (Exception) { } stageView = null; }
            if (stageParent != null) { UnityEngine.Object.Destroy(stageParent); stageParent = null; }
            if (hiddenGame != null) { hiddenGame.SetActive(true); hiddenGame = null; }
        }

        // Rewrites Scripts/AquariumGlyphsR82.cs: every character the runtime scripts contain (their text literals, and
        // harmlessly their comments), for the HUD font warm-up. Run it after adding text with new characters.
        public static string WriteGlyphs()
        {
            if (EditorApplication.isPlaying) return "not in Play mode";
            string root = Application.dataPath + "/Demos/HideAndSeek", file = root + "/Scripts/AquariumGlyphsR82.cs";
            var set = new SortedSet<char>();
            for (char c = ' '; c <= '~'; c++) set.Add(c);
            foreach (var f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string unix = f.Replace('\\', '/');
                if (unix.Contains("/Editor/") || unix.EndsWith("/AquariumGlyphsR82.cs")) continue;
                foreach (var ch in File.ReadAllText(f)) if (ch > 0x7f && ch != 0xB7 && ch != 0xFEFF && !char.IsSurrogate(ch) && !char.IsControl(ch) && !char.IsWhiteSpace(ch)) set.Add(ch);
            }
            // Names kept in assets (text YAML writes them as \uXXXX escapes).
            foreach (var f in Directory.GetFiles(root, "*.asset", SearchOption.AllDirectories))
            {
                if (new FileInfo(f).Length > 2000000) continue;
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f), @"\\u([0-9A-Fa-f]{4})"))
                { char ch = (char)Convert.ToInt32(m.Groups[1].Value, 16); if (ch > 0x7f && ch != 0xB7 && ch != 0xFEFF && !char.IsSurrogate(ch) && !char.IsControl(ch) && !char.IsWhiteSpace(ch)) set.Add(ch); }
            }
            var text = new StringBuilder();
            foreach (var ch in set) { if (ch == '"' || ch == '\\') text.Append('\\'); text.Append(ch); }
            var code = "namespace PrototypeLab.Aquarium\n{\n    // R82: every character the game's own text uses, laid out once at start-up so the dynamic HUD font never has to\n    // add one mid-round (AquariumGame.WarmGlyphs). Generated by AquariumR82QA.WriteGlyphs() from the runtime scripts:\n    // run it again after adding text with new characters (a missing one only costs its old first-use stall).\n    internal static class AquariumGlyphsR82\n    {\n        public const string Text = \"" + text + "\";\n    }\n}\n";
            File.WriteAllText(file, code, new UTF8Encoding(true));
            return "wrote " + set.Count + " characters";
        }

        static void Write()
        {
            Directory.CreateDirectory(Folder);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var csv = new StringBuilder(count * 220);
            csv.Append("frame,rt,dtMs,mainMs,gcRec,gcThread,gc0,st,entry,harp,caught,stowS,camx,camy,camz,crx,cry,crz,crw,dx,dy,dz,hasBody,bx,by,bz,brx,bry,brz,brw,bsx,bsy,bsz,dumped,fx");
            foreach (var m in MarkerNames) csv.Append(',').Append(m);
            csv.AppendLine();
            for (int i = 0; i < count; i++)
            {
                var r = rows[i];
                csv.AppendFormat(inv, "{0},{1:0.0000},{2:0.000},{3:0.000},{4},{5},{6},{7:0.0000},{8:0.0000},{9},{10},{11:0.0000}", r.frame, r.rt, r.dt, r.main / 1e6, r.gcRec, r.gcThread, r.gc0, r.st, r.entry, r.harp, r.caught, r.stowS);
                csv.AppendFormat(inv, ",{0:0.00000},{1:0.00000},{2:0.00000},{3:0.00000},{4:0.00000},{5:0.00000},{6:0.00000},{7:0.00000},{8:0.00000},{9:0.00000}", r.cam.x, r.cam.y, r.cam.z, r.camRot.x, r.camRot.y, r.camRot.z, r.camRot.w, r.diver.x, r.diver.y, r.diver.z);
                csv.AppendFormat(inv, ",{0},{1:0.00000},{2:0.00000},{3:0.00000},{4:0.00000},{5:0.00000},{6:0.00000},{7:0.00000},{8:0.0000},{9:0.0000},{10:0.0000},{11},{12}", r.hasBody ? 1 : 0, r.body.x, r.body.y, r.body.z, r.bodyRot.x, r.bodyRot.y, r.bodyRot.z, r.bodyRot.w, r.bodyScale.x, r.bodyScale.y, r.bodyScale.z, r.dumped ? 1 : 0, r.fx > 0 ? fxNames[r.fx - 1] : "");
                for (int k = 0; k < MarkerNames.Length; k++) csv.Append(',').Append((marks[i * MarkerNames.Length + k] / 1e6).ToString("0.000", inv));
                csv.AppendLine();
            }
            File.WriteAllText(Path.Combine(Folder, "R82_" + label + ".csv"), csv.ToString());
            File.WriteAllText(Path.Combine(Folder, "R82_" + label + "_notes.txt"), "screen=" + Screen.width + "x" + Screen.height + " target=" + Application.targetFrameRate + " vsync=" + QualitySettings.vSyncCount + " profile=" + profile + "\n" + notes + "\n" + spikes);
        }
    }
}
