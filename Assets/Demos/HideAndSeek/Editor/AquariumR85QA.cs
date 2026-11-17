using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    // R85: QA for the underwater feel (swaying plants and anemones, suspended specks, bubble streams). Play mode
    // only, nothing saved. A practice round on a fixed scenery seed with an autopilot swimming from plant to plant:
    //  * mode "perf": no pictures; frame time, draw calls, batches and garbage per frame to R85_<label>_perf.txt;
    //  * mode "fixed": the game's camera is placed at fixed vantage points (close and wide on seaweed and anemones)
    //    and rendered every frame at a fixed 20 fps clock to R85_<label>/v<k>_<frame>.jpg;
    //  * mode "play": the player's own view (diver first person or alien third person) every frame, same clock.
    public static class AquariumR85QA
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
        public static string Status = "idle";
        public static string Out { get { return Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "ClaudeQA"); } }
        static AquariumGame game; static AquariumSession session; static AquariumView view; static object followCamera; static FieldInfo pauseField;
        static string label, mode; static int team, seed, frames, maxFrames, waypoint, oldVsync, oldTarget, oldCapture, skip;
        static float began, maxSeconds, startAt; static bool hooked, started;
        static readonly List<Vector3> route = new List<Vector3>();
        static readonly List<Vector3> camAt = new List<Vector3>(), camLook = new List<Vector3>(); static readonly List<float> camFov = new List<float>();
        static RenderTexture target; static Texture2D image;
        static ProfilerRecorder calls, batches, setPass, tris, gcRec, mainRec;
        static readonly List<float> dts = new List<float>(); static double sumCalls, sumBatches, sumSetPass, sumTris, sumGc, sumMain; static long maxGc; static int gcFrames, gen0;
        static Point lastAim = new Point(0, 0, 1); static Vector3 chase;
        public static string Views = "";

        public static string Begin(string name, int map, int asTeam, int scenerySeed, string what, float seconds, int width = 960, int height = 540, int warmupFrames = 0)
        {
            if (!EditorApplication.isPlaying) return "not playing";
            game = UnityEngine.Object.FindFirstObjectByType<AquariumGame>(); if (game == null) return "no game";
            session = (AquariumSession)typeof(AquariumGame).GetField("session", Any).GetValue(game);
            view = (AquariumView)typeof(AquariumGame).GetField("view", Any).GetValue(game);
            followCamera = typeof(AquariumGame).GetField("followCamera", Any).GetValue(game);
            pauseField = typeof(AquariumGame).GetField("pause", Any);
            typeof(AquariumGame).GetField("automated", Any).SetValue(game, true);
            Application.runInBackground = true;
            label = name; mode = what; team = asTeam; seed = scenerySeed; maxSeconds = seconds; frames = 0; waypoint = 0; started = false; skip = warmupFrames;
            chase = Vector3.zero; route.Clear(); camAt.Clear(); camLook.Clear(); camFov.Clear(); dts.Clear();
            sumCalls = sumBatches = sumSetPass = sumTris = sumGc = sumMain = 0; maxGc = 0; gcFrames = 0;
            session.PreferredMap = map;
            session.OpenPracticeLobby("QA", asTeam == 0 ? Team.Seeker : Team.Hider, 0);
            typeof(AquariumModel).GetProperty("ScenerySeed", Any).SetValue(session.Model, scenerySeed, null);
            session.Model.StartRound();
            typeof(AquariumSession).GetProperty("State", Any).SetValue(session, session.Model.Snapshot(), null);
            oldVsync = QualitySettings.vSyncCount; oldTarget = Application.targetFrameRate; oldCapture = Time.captureFramerate;
            if (mode == "perf") { QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; }
            else
            {
                Time.captureFramerate = 20;
                Directory.CreateDirectory(Path.Combine(Out, "R85_" + label));
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
            }
            maxFrames = mode == "perf" ? int.MaxValue : Mathf.RoundToInt(seconds * 20);
            began = Time.realtimeSinceStartup;
            if (!hooked) { EditorApplication.update += Tick; Camera.onPreRender += Sample; hooked = true; }
            Status = "running " + name;
            return Status;
        }

        public static string End(string why = "ended")
        {
            if (hooked) { EditorApplication.update -= Tick; Camera.onPreRender -= Sample; hooked = false; }
            QualitySettings.vSyncCount = oldVsync; Application.targetFrameRate = oldTarget; Time.captureFramerate = oldCapture;
            if (mode == "perf" && started) { DisposeRecorders(); WritePerf(); }
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); target = null; }
            if (image != null) { UnityEngine.Object.DestroyImmediate(image); image = null; }
            if (game != null) typeof(AquariumGame).GetField("automated", Any).SetValue(game, false);
            Status = why + " " + label + " frames=" + frames + (mode == "perf" ? "\n" + perfText : "");
            return Status;
        }
        static void DisposeRecorders() { calls.Dispose(); batches.Dispose(); setPass.Dispose(); tris.Dispose(); gcRec.Dispose(); mainRec.Dispose(); }

        static Vector3 V(Point p) { return new Vector3(p.x, p.y, p.z); }
        // The round's route and the fixed cameras, from the scenery layout of the seed (the same every run).
        static void Plan()
        {
            var layout = AquariumSceneryLayout.Current; var plants = layout.Plants; var anemones = layout.Anemones;
            var sb = new StringBuilder();
            if (plants != null && plants.Length > 0)
            {
                // The tallest plant, a mid one and a grass tuft, each with a few neighbours in view.
                int tall = 0, mid = -1, low = -1;
                for (int i = 0; i < plants.Length; i++)
                {
                    if (plants[i].height > plants[tall].height) tall = i;
                    if (mid < 0 && plants[i].height > 2.2f && plants[i].height < 3.4f) mid = i;
                    if (low < 0 && plants[i].height < .8f) low = i;
                }
                AddCamera(V(plants[tall].position), plants[tall].height * .55f, 5.5f, 1.2f, 40, 60);
                if (mid >= 0) AddCamera(V(plants[mid].position), plants[mid].height * .5f, 3.2f, .6f, 200, 55);
                if (low >= 0) AddCamera(V(plants[low].position), .25f, 1.5f, .45f, 120, 55);
                sb.Append("tall=" + tall + " h=" + plants[tall].height.ToString("0.0") + " mid=" + mid + " low=" + low + " plants=" + plants.Length);
                int n = Mathf.Min(8, plants.Length);
                for (int k = 0; k < n; k++) { var p = V(plants[(k * 37 + 5) % plants.Length].position); p.y += 1.3f; route.Add(p); }
            }
            if (anemones != null && anemones.Length > 0)
            {
                var a = V(anemones[0].position);
                AddCamera(a, anemones[0].size * .6f, 1.25f, .5f, 30, 50);
                if (anemones.Length > 3) AddCamera(V(anemones[3].position), anemones[3].size * .6f, .8f, .35f, 250, 45);
                sb.Append(" anemones=" + anemones.Length);
                route.Insert(Mathf.Min(1, route.Count), a + Vector3.up * 1.2f);
            }
            // A wide view over the sand from mid water.
            var mid0 = AquariumMaps.Layout(-4, 3.2f, 1); var look = AquariumMaps.Layout(6, .8f, -2);
            camAt.Add(V(mid0)); camLook.Add(V(look)); camFov.Add(68);
            for (int k = 0; k < AquariumWaterLife.DebugSpots.Count && k < 2; k++) { var s = AquariumWaterLife.DebugSpots[k * 3 % AquariumWaterLife.DebugSpots.Count]; AddCamera(s, 1.6f, 3.4f, .2f, 70 + k * 120, 60); }
            sb.Append(" spots=" + AquariumWaterLife.DebugSpots.Count);
            Views = sb.ToString() + " cameras=" + camAt.Count + " route=" + route.Count;
        }
        static void AddCamera(Vector3 foot, float lookHeight, float distance, float height, float yawDeg, float fov)
        {
            float a = yawDeg * Mathf.Deg2Rad; var look = foot + Vector3.up * lookHeight;
            var at = foot + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * distance + Vector3.up * (lookHeight + height);
            // Turn round the subject until the line of sight is clear of rock and glass.
            for (int i = 0; i < 12 && AquariumModel.SolidHit(new Point(look.x, look.y, look.z), new Point(at.x, at.y, at.z), .3f) <= 1; i++)
            { a += Mathf.PI / 6; at = foot + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * distance + Vector3.up * (lookHeight + height); }
            camAt.Add(at); camLook.Add(look); camFov.Add(fov);
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || game == null) { End("stopped: not playing"); return; }
            try { Drive(); } catch (Exception e) { End("error: " + e); }
        }
        static void Drive()
        {
            pauseField.SetValue(game, false);
            var model = session.Model; if (model == null) { End("no model"); return; }
            var p = model.Player(session.LocalId); if (p == null) return;
            if (model.Phase == RoundPhase.Finished) { End("done(round over)"); return; }
            if (mode == "botplay") p.bot = true;
            bool ready = (mode == "fixed" ? model.Time >= 4f : model.Time >= model.HuntAt + (mode == "chase" ? 4f : 1.5f)) && view.Camera != null && AquariumSceneryLayout.Current != null && AquariumSceneryLayout.Current.Plants != null;
            if (!ready) { session.Input(new SwimInput { aimX = p.facing.x, aimY = p.facing.y, aimZ = p.facing.z }); return; }
            if (!started)
            {
                started = true; Plan(); startAt = Time.realtimeSinceStartup;
                if (mode == "perf")
                {
                    calls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count"); batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
                    setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count"); tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
                    gcRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame"); mainRec = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
                    gen0 = GC.CollectionCount(0);
                }
            }
            // Autopilot: swim from plant to plant (the fixed-camera mode only idles).
            var input = new SwimInput { aimX = lastAim.x, aimY = lastAim.y, aimZ = lastAim.z };
            if (mode != "fixed" && mode != "chase" && mode != "botplay" && route.Count > 0)
            {
                var to = route[waypoint % route.Count]; var at = V(p.position); var d = to - at; float range = d.magnitude;
                if (range < 2.2f) waypoint++;
                else
                {
                    var aim = d / range; lastAim = new Point(aim.x, aim.y, aim.z);
                    input.aimX = input.x = aim.x; input.aimY = input.y = aim.y; input.aimZ = input.z = aim.z; input.sprint = range > 6 && (waypoint % 2 == 0);
                    Look(lastAim);
                }
            }
            session.Input(input);
            if (mode == "perf") { if (Time.realtimeSinceStartup - startAt > maxSeconds) End("done"); return; }
            if (skip > 0) { skip--; return; }
            if (mode == "fixed")
            {
                var cam = view.Camera; var t = cam.transform; var pos = t.position; var rot = t.rotation; float fov = cam.fieldOfView;
                for (int k = 0; k < camAt.Count; k++)
                {
                    t.position = camAt[k]; t.rotation = Quaternion.LookRotation(camLook[k] - camAt[k], Vector3.up); cam.fieldOfView = camFov[k];
                    view.RenderTo(target); Save("v" + k + "_" + frames.ToString("0000"));
                }
                t.position = pos; t.rotation = rot; cam.fieldOfView = fov;
            }
            else if (mode == "chase")
            {
                // Beside the diver (a bot), a little ahead, looking back at it: its bubbles and the plants it passes.
                Swimmer diver = null; foreach (var s in model.Players) if (s.team == Team.Seeker) { diver = s; break; }
                if (diver != null)
                {
                    var at = V(diver.position); var f = V(diver.facing); f.y = 0; if (f.sqrMagnitude < .01f) f = Vector3.forward; f.Normalize();
                    var side = Vector3.Cross(Vector3.up, f); chase = Vector3.Lerp(chase, at + side * 3.2f + Vector3.up * .5f - f * 1.2f, chase == Vector3.zero ? 1 : .08f);
                    var cam = view.Camera; var t = cam.transform; var pos = t.position; var rot = t.rotation; float fov = cam.fieldOfView;
                    t.position = chase; t.rotation = Quaternion.LookRotation(at - f * .8f - chase, Vector3.up); cam.fieldOfView = 62;
                    view.RenderTo(target); Save("c_" + frames.ToString("0000"));
                    t.position = pos; t.rotation = rot; cam.fieldOfView = fov;
                }
            }
            else { view.RenderTo(target); Save("p_" + frames.ToString("0000")); }
            frames++;
            if (frames >= maxFrames) End("done");
        }
        static void Save(string file)
        {
            var old = RenderTexture.active; RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            RenderTexture.active = old;
            File.WriteAllBytes(Path.Combine(Out, "R85_" + label, file + ".jpg"), image.EncodeToJPG(90));
        }
        static void Look(Point aim)
        {
            if (followCamera == null) return;
            var type = followCamera.GetType();
            float yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg, pitch = -Mathf.Asin(Mathf.Clamp(aim.y, -1, 1)) * Mathf.Rad2Deg;
            type.GetField("targetYaw", Any).SetValue(followCamera, yaw); type.GetField("targetPitch", Any).SetValue(followCamera, Mathf.Clamp(pitch, -60, 60));
        }

        // One sample per frame the game's camera renders.
        static void Sample(Camera cam)
        {
            if (mode != "perf" || !started || view == null || cam != view.Camera) return;
            if (Time.realtimeSinceStartup - startAt < 2) return;   // settle
            dts.Add(Time.unscaledDeltaTime * 1000f); frames++;
            sumCalls += calls.LastValue; sumBatches += batches.LastValue; sumSetPass += setPass.LastValue; sumTris += tris.LastValue; sumMain += mainRec.LastValue * 1e-6;
            long gc = gcRec.LastValue; sumGc += gc; if (gc > 0) gcFrames++; if (gc > maxGc) maxGc = gc;
        }
        static void WritePerf()
        {
            if (dts.Count == 0) return;
            var sorted = dts.ToArray(); Array.Sort(sorted); int n = sorted.Length; double sum = 0; foreach (var d in sorted) sum += d;
            int over = 0; double mean = sum / n; foreach (var d in sorted) if (d > mean * 2 + 2) over++;
            var sb = new StringBuilder();
            sb.AppendLine("label=" + label + " map=" + session.Map + " team=" + team + " seed=" + seed + " frames=" + n + " screen=" + Screen.width + "x" + Screen.height + " aa=" + QualitySettings.antiAliasing + " cascades=" + QualitySettings.shadowCascades);
            sb.AppendLine("frame ms: mean=" + mean.ToString("0.00") + " median=" + sorted[n / 2].ToString("0.00") + " p95=" + sorted[(int)(n * .95f)].ToString("0.00") + " p99=" + sorted[(int)(n * .99f)].ToString("0.00") + " max=" + sorted[n - 1].ToString("0.00") + " fps(mean)=" + (1000 / mean).ToString("0.0") + " spikes(>2x mean+2ms)=" + over);
            sb.AppendLine("main thread ms mean=" + (sumMain / n).ToString("0.00"));
            sb.AppendLine("draw calls=" + (sumCalls / n).ToString("0.0") + " batches=" + (sumBatches / n).ToString("0.0") + " setpass=" + (sumSetPass / n).ToString("0.0") + " tris=" + (sumTris / n).ToString("0"));
            sb.AppendLine("gc bytes/frame mean=" + (sumGc / n).ToString("0") + " max=" + maxGc + " frames with gc alloc=" + gcFrames + " gen0 collections=" + (GC.CollectionCount(0) - gen0));
            Directory.CreateDirectory(Out);
            File.WriteAllText(Path.Combine(Out, "R85_" + label + "_perf.txt"), sb.ToString());
            perfText = sb.ToString();
        }
        static string perfText = "";
    }
}
