#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
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
            public string mode, code, status, phase, winReason;
            public int localId, players, hiders, divers, snapshots, fishRemaining, target, killed, personalEaten, score, eggs, revivalPlayer;
            public float time, localX, localY, localZ, minHeight, maxHeight, cameraDistance, cameraYaw, reviewRemaining, feedReadyAt;
            public float stamina, hungerRemaining, morphLockedUntil, scanReadyAt, scanRemaining, revivalHatchAt;
            public int cameraFollowId;
            public bool connected, host, perspective, firstPerson, freeView, spectatingDiver, revivalEggCreated, revived, continuedPlaying;
            public string[] errors;
        }
        private AquariumSession session;
        private AquariumView view;
        private AquariumThirdPersonCamera followCamera;
        private string mode, reportPath, capturePath, hudCapturePath, codePath;
        private float started, duration = 24, nextInput, finishAfter;
        private bool initialized, captured, hudCaptureRequested, revivalEggCreated, revived;
        private int revivalPlayer;
        private float revivalHatchAt;
        private RenderTexture hudTarget, previousTarget;
        private bool hudPending;
        private float minHeight = float.MaxValue, maxHeight = float.MinValue;
        private readonly List<string> errors = new List<string>();
        private string Arg(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
            return "";
        }
        public void Initialize(AquariumSession value, AquariumView sceneView, AquariumThirdPersonCamera playerCamera)
        {
            mode = Arg("-aqTest"); if (mode.Length == 0) { enabled = false; return; }
            session = value; view = sceneView; followCamera = playerCamera; reportPath = Arg("-aqReport"); capturePath = Arg("-aqCapture"); codePath = Arg("-aqCodeFile");
            hudCapturePath = Arg("-aqCaptureHud");
            float requested; if (float.TryParse(Arg("-aqDuration"), out requested)) duration = requested;
            float.TryParse(Arg("-aqFinishAfter"), out finishAfter);
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
                    case "revival": StartRevivalScenario(); break;
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
                if (mode == "revival") CheckRevival(state);
                if (p != null && mode != "revival" && elapsed > nextInput)
                {
                    nextInput = elapsed + 0.05f;
                    var move = followCamera.Movement(0.15f, 0.35f, Mathf.Sin(elapsed * 0.8f) * 0.7f);
                    var aim = followCamera.Aim(state, p);
                    session.Input(new SwimInput { x = move.x, y = move.y, z = move.z, aimX = aim.x, aimY = aim.y, aimZ = aim.z,
                        morph = true, action = true, feed = true, sprint = true, scan = true, layEgg = true });
                    if (state.phase == RoundPhase.Playing)
                    { minHeight = Mathf.Min(minHeight, p.position.y); maxHeight = Mathf.Max(maxHeight, p.position.y); }
                }
                if (mode != "revival" && state.phase == RoundPhase.Playing && followCamera.Active) followCamera.Look(0.12f * Time.unscaledDeltaTime * 60, 0, 0);
                if (finishAfter > 0 && session.IsHost && state.phase == RoundPhase.Playing && elapsed > finishAfter)
                    foreach (var fish in session.Model.Fish) fish.eaten = true;
                if (state.phase == RoundPhase.Finished && state.time < state.reviewEndsAt)
                {
                    followCamera.Look(0.08f * Time.unscaledDeltaTime * 60, 0, 0);
                    followCamera.MoveFree(0.1f, 0.05f, 0.03f, Time.unscaledDeltaTime);
                }
            }
            if (!captured && capturePath.Length > 0 && elapsed > duration - 3)
            { CaptureWorld(); captured = true; }
            if (!hudCaptureRequested && hudCapturePath.Length > 0 && elapsed > duration - 3)
            { hudCaptureRequested = true; hudPending = true; }
            if (elapsed > duration)
            {
                var s = session.State; var p = s == null ? null : Array.Find(s.players, value => value.id == session.LocalId);
                bool continuedPlaying = s != null && s.phase == RoundPhase.Playing;
                if (mode == "revival" && (!revivalEggCreated || !revived || !continuedPlaying))
                    errors.Add("Revival scenario failed: egg=" + revivalEggCreated + ", revived=" + revived + ", playing=" + continuedPlaying);
                var report = new Report { mode = mode, connected = session.Connected, host = session.IsHost, code = session.Code, status = session.Status,
                    localId = session.LocalId, players = s == null ? 0 : s.players.Length, snapshots = session.ReceivedSnapshots,
                    hiders = s == null ? 0 : Array.FindAll(s.players, value => value.team == Team.Hider).Length,
                    divers = s == null ? 0 : Array.FindAll(s.players, value => value.team == Team.Seeker).Length,
                    phase = s == null ? "none" : s.phase.ToString(), time = s == null ? 0 : s.time, fishRemaining = s == null ? 0 : s.remaining,
                    winReason = s == null ? "none" : s.winReason.ToString(), reviewRemaining = s == null ? 0 : Mathf.Max(0, s.reviewEndsAt - s.time),
                    target = s == null ? 0 : s.target, killed = s == null ? 0 : s.killed, feedReadyAt = p == null ? 0 : p.nextFeed,
                    stamina = p == null ? 0 : p.stamina, hungerRemaining = p == null || s == null ? 0 : Mathf.Clamp(AquariumModel.HungerSeconds - (s.time - p.lastMealAt), 0, AquariumModel.HungerSeconds),
                    personalEaten = p == null ? 0 : p.personalEaten, score = p == null ? 0 : p.score, morphLockedUntil = p == null ? 0 : p.morphLockedUntil,
                    eggs = s == null || s.eggs == null ? 0 : s.eggs.Length, scanReadyAt = p == null ? 0 : p.nextScan,
                    scanRemaining = p == null || s == null ? 0 : Mathf.Max(0, p.scanUntil - s.time),
                    revivalPlayer = revivalPlayer, revivalHatchAt = revivalHatchAt, revivalEggCreated = revivalEggCreated, revived = revived, continuedPlaying = continuedPlaying,
                    localX = p == null ? 0 : p.position.x, localY = p == null ? 0 : p.position.y, localZ = p == null ? 0 : p.position.z,
                    minHeight = minHeight == float.MaxValue ? 0 : minHeight, maxHeight = maxHeight == float.MinValue ? 0 : maxHeight,
                    perspective = !view.Camera.orthographic, cameraFollowId = followCamera.FollowId, cameraYaw = followCamera.Yaw,
                    firstPerson = followCamera.FirstPerson, freeView = followCamera.FreeView, spectatingDiver = followCamera.SpectatingDiver,
                    cameraDistance = p == null ? 0 : Vector3.Distance(view.Camera.transform.position, view.PlayerPosition(p)), errors = errors.ToArray() };
                if (reportPath.Length > 0) File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                enabled = false; Application.Quit(errors.Count == 0 ? 0 : 1);
            }
        }

        private void StartRevivalScenario()
        {
            session.StartPractice("Revival tester", Team.Hider);
            // Keep the full roster and live fish, but freeze bot decisions so only the tested input acts.
            foreach (var player in session.Model.Players) player.bot = false;
            var local = session.Model.Player(session.LocalId);
            local.personalEaten = AquariumModel.EggFoodRequired;
            var target = session.Model.Players.Find(player => player.id != session.LocalId && player.team == Team.Hider);
            target.alive = false; target.deathReason = "harpoon"; revivalPlayer = target.id;
            session.Input(new SwimInput { layEgg = true });
        }

        private void CheckRevival(AquariumSnapshot state)
        {
            if (state.eggs != null)
                foreach (var egg in state.eggs)
                    if (egg.owner == session.LocalId && egg.revivePlayer == revivalPlayer)
                    { revivalEggCreated = true; revivalHatchAt = egg.hatchAt; }
            var target = Array.Find(state.players, player => player.id == revivalPlayer);
            if (revivalEggCreated && target != null && target.alive && state.time >= revivalHatchAt) revived = true;
        }

        // Render the real IMGUI pass into the camera image even in a hidden test window.
        public bool BeginHudCapture()
        {
            if (!hudPending || Event.current.type != EventType.Repaint) return false;
            hudPending = false;
            hudTarget = new RenderTexture(Screen.width, Screen.height, 24);
            previousTarget = RenderTexture.active;
            var cameraTarget = view.Camera.targetTexture;
            view.Camera.targetTexture = hudTarget; view.Camera.Render(); view.Camera.targetTexture = cameraTarget;
            RenderTexture.active = hudTarget;
            GL.PushMatrix(); GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);
            return true;
        }

        public void EndHudCapture()
        {
            GL.PopMatrix(); GL.Flush();
            var pixels = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); pixels.Apply();
            File.WriteAllBytes(hudCapturePath, pixels.EncodeToPNG());
            RenderTexture.active = previousTarget;
            hudTarget.Release(); Destroy(hudTarget); Destroy(pixels); hudTarget = null;
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
