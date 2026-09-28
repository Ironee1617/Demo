#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // Opt-in development-build harness. Inactive without -aqTest.
    public sealed class AquariumSmokeDriver : MonoBehaviour
    {
        [Serializable] private class Report
        {
            public string mode, code, status, phase;
            public int localId, players, snapshots, fishRemaining;
            public float time, localX, localZ;
            public bool connected, host;
            public string[] errors;
        }
        private AquariumSession session;
        private AquariumView view;
        private string mode, reportPath, capturePath, codePath;
        private float started, duration = 24, nextInput;
        private bool initialized, captured;
        private readonly List<string> errors = new List<string>();
        private string Arg(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
            return "";
        }
        public void Initialize(AquariumSession value, AquariumView sceneView)
        {
            mode = Arg("-aqTest"); if (mode.Length == 0) { enabled = false; return; }
            session = value; view = sceneView; reportPath = Arg("-aqReport"); capturePath = Arg("-aqCapture"); codePath = Arg("-aqCodeFile");
            float requested; if (float.TryParse(Arg("-aqDuration"), out requested)) duration = requested;
            Application.logMessageReceived += Log; started = Time.realtimeSinceStartup;
        }
        private void Log(string condition, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(condition); }
        private void Update()
        {
            if (!initialized)
            {
                initialized = true;
                switch (mode)
                {
                    case "host": session.OpenLocalTest(true, "Test host", Team.Hider); break;
                    case "client": session.OpenLocalTest(false, "Test seeker", Team.Seeker); break;
                    case "relayHost": _ = session.OpenInternet(true, "", "Relay host", Team.Hider); break;
                    case "relayClient": _ = session.OpenInternet(false, Arg("-aqJoin"), "Relay seeker", Team.Seeker); break;
                    case "practice": session.StartPractice("Test swimmer", Team.Hider); break;
                    case "seeker": session.StartPractice("Test diver", Team.Seeker); break;
                    case "menu": break;
                }
            }
            float elapsed = Time.realtimeSinceStartup - started;
            if (session.Code.Length > 0 && codePath.Length > 0) { File.WriteAllText(codePath, session.Code); codePath = ""; }
            if (session.Connected)
            {
                var state = session.State;
                var p = Array.Find(state.players, value => value.id == session.LocalId);
                if (state.phase == RoundPhase.Lobby)
                {
                    if (!session.IsHost) session.Role(Team.Seeker, true);
                    else if (state.players.Length >= 2 && elapsed > 3) session.StartRound();
                }
                if (p != null && elapsed > nextInput)
                {
                    nextInput = elapsed + 0.05f;
                    session.Input(new SwimInput { x = Mathf.Sin(elapsed * 0.7f), z = Mathf.Cos(elapsed * 0.6f), aimX = -1, aimZ = 0, morph = true, action = true });
                }
            }
            if (!captured && capturePath.Length > 0 && elapsed > duration - 3)
            { CaptureWorld(); captured = true; }
            if (elapsed > duration)
            {
                var s = session.State; var p = s == null ? null : Array.Find(s.players, value => value.id == session.LocalId);
                var report = new Report { mode = mode, connected = session.Connected, host = session.IsHost, code = session.Code, status = session.Status,
                    localId = session.LocalId, players = s == null ? 0 : s.players.Length, snapshots = session.ReceivedSnapshots,
                    phase = s == null ? "none" : s.phase.ToString(), time = s == null ? 0 : s.time, fishRemaining = s == null ? 0 : s.remaining,
                    localX = p == null ? 0 : p.position.x, localZ = p == null ? 0 : p.position.z, errors = errors.ToArray() };
                if (reportPath.Length > 0) File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                enabled = false; Application.Quit(errors.Count == 0 ? 0 : 1);
            }
        }
        private void CaptureWorld()
        {
            // Hidden test windows have no backbuffer; explicitly render the actual game camera.
            var target = new RenderTexture(1440, 900, 24);
            var previous = RenderTexture.active; var cameraTarget = view.Camera.targetTexture;
            var pixels = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            try
            {
                view.Camera.targetTexture = target; view.Camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); pixels.Apply();
                File.WriteAllBytes(capturePath, pixels.EncodeToPNG());
            }
            finally { view.Camera.targetTexture = cameraTarget; RenderTexture.active = previous; target.Release(); Destroy(target); Destroy(pixels); }
        }
        private void OnDestroy() { Application.logMessageReceived -= Log; }
    }
}
#endif
