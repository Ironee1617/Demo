#if UNITY_EDITOR || DEVELOPMENT_BUILD || AQUARIUM_PERF
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R66: opt-in frame-time logger for standalone builds. Inactive unless a file named aq_r66_perf.txt sits in the
    // build folder (next to the exe). It plays AI-only practice rounds (the local seat is an AI too) on the listed
    // maps and roles at the monitor's native resolution, and writes one CSV per round plus a summary.
    [DefaultExecutionOrder(1200)]
    public sealed class AquariumFrameLogR66 : MonoBehaviour
    {
        struct Row
        {
            public float t, dt, cpu, main, render, gpu, calls, batches, setPass, tris, gcRec, totalMem;
            public long gcThread; public int gcCount, variant, harpoons; public bool shot, settling; public string fx;
            public float m0, m1, m2, m3, m4, m5, m6, m7, m8, m9, m10;
        }
        struct Plan { public int map; public Team team; }
        static readonly string[] MarkerNames = { "GC.Collect", "Shader.CreateGPUProgram", "Mesh.UploadMeshData", "Instantiate", "GUI.Repaint", "Physics.Simulate", "Gfx.WaitForPresentOnGfxThread", "Aquarium.Draw", "Aquarium.Session", "Aquarium.Camera", "Aquarium.Target" };
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<Row> rows = new List<Row>(20000);
        readonly List<Plan> plans = new List<Plan>();
        readonly FrameTiming[] timing = new FrameTiming[1];
        readonly ProfilerRecorder[] markers = new ProfilerRecorder[MarkerNames.Length];
        ProfilerRecorder calls, batches, setPass, tris, gcRec;
        readonly StringBuilder summary = new StringBuilder();
        AquariumSession session;
        string output = "", label = "";
        float seconds = 75, runStarted, waitUntil, shotA = 25, shotB = 45;
        int planIndex = -1, oldW, oldH, quitFrames = -1, lastGcCount;
        long lastThreadBytes;
        bool uncap = true, screens, running, takenA, takenB;
        FullScreenMode oldMode;
        FileStream lockFile;
        int lastFx;
        bool fast; AquariumGame game;
        // The pause menu's picture option (kept in PlayerPrefs, so it is put back to full quality when the logger ends).
        void SetFast(bool on) { typeof(AquariumGame).GetMethod("SetFastGraphics", Private).Invoke(game, new object[] { on }); }

        static string ConfigPath() { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "aq_r66_perf.txt")); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Application.isEditor || !File.Exists(ConfigPath())) return;
            if (FindFirstObjectByType<AquariumGame>() == null) return;
            new GameObject("R66 frame logger").AddComponent<AquariumFrameLogR66>();
        }

        void Start()
        {
            foreach (var raw in File.ReadAllLines(ConfigPath()))
            {
                int eq = raw.IndexOf('='); if (eq <= 0) continue;
                string key = raw.Substring(0, eq).Trim(), value = raw.Substring(eq + 1).Trim();
                if (key == "out") output = value;
                else if (key == "label") label = value;
                else if (key == "seconds") seconds = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                else if (key == "uncap") uncap = value == "1";
                else if (key == "screens") screens = value == "1";
                else if (key == "ab") ab = value == "1";
                else if (key == "fast") fast = value == "1";
                else if (key == "runs")
                    foreach (var run in value.Split(','))
                    { var r = run.Trim(); if (r.Length < 2) continue; plans.Add(new Plan { map = r[0] - '0', team = r[1] == 'H' ? Team.Hider : Team.Seeker }); }
            }
            if (output.Length == 0) output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "R66_perf"));
            Directory.CreateDirectory(output);
            // One logger at a time: a second launch of the same build quits at once instead of sharing the GPU.
            try { lockFile = new FileStream(Path.Combine(output, "running.lock"), FileMode.Create, FileAccess.Write, FileShare.None); }
            catch (IOException) { enabled = false; Application.Quit(); return; }
            game = FindFirstObjectByType<AquariumGame>();
            if (fast) SetFast(true);
            session = (AquariumSession)typeof(AquariumGame).GetField("session", Private).GetValue(game);
            typeof(AquariumGame).GetField("automated", Private).SetValue(game, true);
            Application.runInBackground = true;
            oldW = Screen.width; oldH = Screen.height; oldMode = Screen.fullScreenMode;
            var native = Screen.currentResolution;
            Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);
            for (int i = 0; i < MarkerNames.Length; i++) { markers[i] = new ProfilerRecorder(MarkerNames[i], 1, ProfilerRecorderOptions.Default); markers[i].Start(); }
            calls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            gcRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            summary.AppendLine("label=" + label + " build=" + (Debug.isDebugBuild ? "development" : "release") + " unity=" + Application.unityVersion);
            summary.AppendLine("cpu=" + SystemInfo.processorType + " cores=" + SystemInfo.processorCount + " ram=" + SystemInfo.systemMemorySize + " gpu=" + SystemInfo.graphicsDeviceName + " vram=" + SystemInfo.graphicsMemorySize + " api=" + SystemInfo.graphicsDeviceVersion);
            summary.AppendLine("monitor=" + native.width + "x" + native.height + "@" + native.refreshRateRatio.value.ToString("0.###") + " quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()]
                + " gameVsync=" + QualitySettings.vSyncCount + " gameTargetFps=" + Application.targetFrameRate + " uncap=" + uncap + " fastGraphics=" + fast + " incrementalGC=" + UnityEngine.Scripting.GarbageCollector.isIncremental);
            string valid = ""; for (int i = 0; i < MarkerNames.Length; i++) valid += MarkerNames[i] + ":" + (markers[i].Valid ? "ok " : "n/a ");
            summary.AppendLine("markers " + valid + " calls:" + calls.Valid + " gcRec:" + gcRec.Valid + " frameTiming:" + FrameTimingManager.IsFeatureEnabled());
            waitUntil = Time.realtimeSinceStartup + 2;
        }

        void BeginRun()
        {
            planIndex++;
            if (planIndex >= plans.Count) { Finish(); return; }
            var plan = plans[planIndex];
            if (uncap) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; }
            session.PreferredMap = plan.map;
            session.StartPractice("R66", plan.team);
            foreach (var p in session.Model.Players) if (p.id == session.LocalId) p.bot = true;
            rows.Clear(); running = true; lastFx = 0; takenA = takenB = false; runStarted = Time.realtimeSinceStartup;
            lastGcCount = GC.CollectionCount(0); lastThreadBytes = GC.GetAllocatedBytesForCurrentThread();
            if (ab) CaptureBase();
        }

        // ---- A/B rendering experiments (config ab=1): cycle quality knobs during a round and log the GPU time of each.
        static readonly string[] VariantNames = { "base", "msaa0", "msaa2", "cascades2", "shadowResMedium", "shadowsOff", "postOff", "shadowDistHalf", "tinyCastersOff" };
        bool ab; int variant; float variantAt; int baseAa, baseCascades; ShadowResolution baseRes; ShadowQuality baseShadows; float baseDist;
        readonly List<Renderer> tinyCasters = new List<Renderer>();
        void CaptureBase()
        {
            baseAa = QualitySettings.antiAliasing; baseCascades = QualitySettings.shadowCascades; baseRes = QualitySettings.shadowResolution;
            baseShadows = QualitySettings.shadows; baseDist = QualitySettings.shadowDistance; variant = 0; variantAt = Time.realtimeSinceStartup;
        }
        void ApplyVariant(int v)
        {
            QualitySettings.antiAliasing = baseAa; QualitySettings.shadowCascades = baseCascades; QualitySettings.shadowResolution = baseRes;
            QualitySettings.shadows = baseShadows; QualitySettings.shadowDistance = baseDist;
            foreach (var cam in FindObjectsByType<AquariumGalleryCamera>(FindObjectsSortMode.None)) cam.enabled = true;
            foreach (var r in tinyCasters) if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            tinyCasters.Clear();
            switch (v)
            {
                case 1: QualitySettings.antiAliasing = 0; break;
                case 2: QualitySettings.antiAliasing = 2; break;
                case 3: QualitySettings.shadowCascades = 2; break;
                case 4: QualitySettings.shadowResolution = ShadowResolution.Medium; break;
                case 5: QualitySettings.shadows = ShadowQuality.Disable; break;
                case 6: foreach (var cam in FindObjectsByType<AquariumGalleryCamera>(FindObjectsSortMode.None)) cam.enabled = false; break;
                case 7: QualitySettings.shadowDistance = baseDist * .5f; break;
                case 8:
                    foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                        if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On && r.bounds.extents.magnitude < .5f) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; tinyCasters.Add(r); }
                    break;
            }
            variant = v; variantAt = Time.realtimeSinceStartup;
        }


        static float Ms(ProfilerRecorder r) { return r.Valid ? r.LastValue / 1000000f : 0; }
        static float V(ProfilerRecorder r) { return r.Valid ? r.LastValue : 0; }

        void LateUpdate()
        {
            if (quitFrames >= 0) { if (quitFrames-- == 0) Application.Quit(); return; }
            if (!running)
            {
                if (session != null && Time.realtimeSinceStartup >= waitUntil) BeginRun();
                return;
            }
            FrameTimingManager.CaptureFrameTimings();
            var row = new Row { t = Time.realtimeSinceStartup - runStarted, dt = Time.unscaledDeltaTime * 1000 };
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
            { row.cpu = (float)timing[0].cpuFrameTime; row.main = (float)timing[0].cpuMainThreadFrameTime; row.render = (float)timing[0].cpuRenderThreadFrameTime; row.gpu = (float)timing[0].gpuFrameTime; }
            row.calls = V(calls); row.batches = V(batches); row.setPass = V(setPass); row.tris = V(tris); row.gcRec = V(gcRec);
            long bytes = GC.GetAllocatedBytesForCurrentThread(); row.gcThread = bytes - lastThreadBytes; lastThreadBytes = bytes;
            int count = GC.CollectionCount(0); row.gcCount = count - lastGcCount; lastGcCount = count;
            row.totalMem = GC.GetTotalMemory(false) / 1048576f;
            row.m0 = Ms(markers[0]); row.m1 = Ms(markers[1]); row.m2 = Ms(markers[2]); row.m3 = Ms(markers[3]); row.m4 = Ms(markers[4]); row.m5 = Ms(markers[5]);
            row.m6 = Ms(markers[6]); row.m7 = Ms(markers[7]); row.m8 = Ms(markers[8]); row.m9 = Ms(markers[9]); row.m10 = Ms(markers[10]);
            if (screens && !takenA && row.t >= shotA) { takenA = true; row.shot = true; ScreenCapture.CaptureScreenshot(Path.Combine(output, Name() + "_a.png")); }
            if (screens && !takenB && row.t >= shotB) { takenB = true; row.shot = true; ScreenCapture.CaptureScreenshot(Path.Combine(output, Name() + "_b.png")); }
            row.variant = variant; row.settling = Time.realtimeSinceStartup - variantAt < .35f;
            var snap = session.State;
            if (snap != null)
            {
                row.harpoons = snap.harpoons != null ? snap.harpoons.Length : 0;
                if (snap.effects != null) foreach (var e in snap.effects) if (e.id > lastFx) { lastFx = e.id; row.fx = row.fx == null ? e.kind : row.fx + "+" + e.kind; }
            }
            rows.Add(row);
            if (ab && row.t > 20 && Time.realtimeSinceStartup - variantAt > 1.4f) ApplyVariant((variant + 1) % VariantNames.Length);
            if (row.t >= seconds) EndRun();
        }

        string Name() { var p = plans[planIndex]; return label + "_map" + p.map + (p.team == Team.Hider ? "_alien" : "_diver"); }

        void EndRun()
        {
            running = false;
            var csv = new StringBuilder(rows.Count * 160);
            csv.Append("t,dtMs,cpuMs,mainMs,renderMs,gpuMs,drawCalls,batches,setPass,tris,gcAllocRec,gcAllocThread,gcCollections,heapMB,shot,variant,harpoons,fx");
            foreach (var m in MarkerNames) csv.Append(',').Append(m);
            csv.AppendLine();
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var r in rows)
            {
                csv.AppendFormat(inv, "{0:0.000},{1:0.00},{2:0.00},{3:0.00},{4:0.00},{5:0.00},{6},{7},{8},{9},{10},{11},{12},{13:0.0},{14}", r.t, r.dt, r.cpu, r.main, r.render, r.gpu, r.calls, r.batches, r.setPass, r.tris, r.gcRec, r.gcThread, r.gcCount, r.totalMem, r.shot ? 1 : 0);
                csv.Append(',').Append(r.settling ? -1 : r.variant).Append(',').Append(r.harpoons).Append(',').Append(r.fx ?? "");
                csv.AppendFormat(inv, ",{0:0.00},{1:0.00},{2:0.00},{3:0.00},{4:0.00},{5:0.00},{6:0.00},{7:0.00},{8:0.00},{9:0.00},{10:0.00}", r.m0, r.m1, r.m2, r.m3, r.m4, r.m5, r.m6, r.m7, r.m8, r.m9, r.m10);
                csv.AppendLine();
            }
            File.WriteAllText(Path.Combine(output, Name() + ".csv"), csv.ToString());
            if (ab) { ApplyVariant(0); VariantStats(); }
            Stats("all", 1.0f); Stats("steady(>10s)", 10f);
            waitUntil = Time.realtimeSinceStartup + 1.5f;
            File.WriteAllText(Path.Combine(output, label + "_summary.txt"), summary.ToString());
        }

        void Stats(string what, float from)
        {
            var times = new List<float>(); double sum = 0, gpu = 0, main = 0, gc = 0, callsSum = 0; int collections = 0, n = 0, spikes = 0;
            foreach (var r in rows)
            {
                if (r.t < from || r.shot) continue;
                times.Add(r.dt); sum += r.dt; gpu += r.gpu; main += r.main; gc += Math.Max(r.gcRec, r.gcThread); callsSum += r.calls; collections += r.gcCount; n++;
            }
            if (n == 0) return;
            times.Sort(); float median = times[n / 2];
            int worst = Math.Max(1, n / 100); double low = 0; for (int i = n - worst; i < n; i++) low += times[i]; low /= worst;
            foreach (var t in times) if (t > Math.Max(20, median * 2.5f)) spikes++;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            summary.AppendLine(string.Format(inv, "{0} {1}: frames={2} avgFps={3:0.0} medianMs={4:0.00} p99Ms={5:0.00} onePctLowFps={6:0.0} worstMs={7:0.0} spikes(>2.5x median,>20ms)={8} gpuMs={9:0.00} mainMs={10:0.00} gcBytes/frame={11:0} gcCollections={12} drawCalls={13:0}",
                Name(), what, n, 1000.0 * n / sum, median, times[Math.Min(n - 1, (int)(n * .99f))], 1000.0 / low, times[n - 1], spikes, gpu / n, main / n, gc / n, collections, callsSum / n));
        }

        void VariantStats()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            for (int v = 0; v < VariantNames.Length; v++)
            {
                double dt = 0, gpu = 0, main = 0, tri = 0; int n = 0;
                // the frame timings arrive a few frames late: skip the settling frames after each switch
                foreach (var r in rows) if (r.variant == v && !r.settling && !r.shot && r.t > 20) { dt += r.dt; gpu += r.gpu; main += r.main; tri += r.tris; n++; }
                if (n > 0) summary.AppendLine(string.Format(inv, "{0} ab {1}: frames={2} avgFps={3:0.0} gpuMs={4:0.00} mainMs={5:0.00} trisM={6:0.0}", Name(), VariantNames[v], n, 1000.0 * n / dt, gpu / n, main / n, tri / n / 1e6));
            }
        }

        void Finish()
        {
            File.WriteAllText(Path.Combine(output, label + "_summary.txt"), summary.ToString());
            if (fast) SetFast(false);
            Screen.SetResolution(1440, 900, FullScreenMode.Windowed);   // the player's default window (PlayerSettings), so the saved screen prefs are left as a normal launch would
            quitFrames = 3;
        }

        void OnDestroy()
        {
            foreach (var m in markers) m.Dispose();
            if (lockFile != null) lockFile.Dispose();
            calls.Dispose(); batches.Dispose(); setPass.Dispose(); tris.Dispose(); gcRec.Dispose();
        }
    }
}
#endif
