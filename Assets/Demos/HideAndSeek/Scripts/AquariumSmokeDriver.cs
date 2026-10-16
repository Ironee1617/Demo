#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // Opt-in development-build harness. Inactive without -aqTest.
    [DefaultExecutionOrder(1000)]
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
            public bool realRuleDeath, deathReviewObserved, corpseVisible, corpseMotionless, diverSpectateObserved;
            public bool terminalImmediate, terminalSkippedIntro, terminalDeadActorsHidden, worldTransformsFrozen, cameraMovedWhileFrozen;
            public bool eggProtectedFromFinish, eggDeathIntroAllowed, targetSelected, targetOutlineSingle;
            public bool feedSourceFound, feedNearPlayed, feedMovedWhilePlaying, feedStoppedOnExit, feedFarEventObserved, feedFarSilent;
            public float feedNearGain, feedNearVolume, feedNearDistance, feedFarDistance, feedRemainingAtExit;
            public int feedFarSamples;
            public float deathReviewDuration;
            public int outlinedActors, outlineMeshes, targetFishId, frozenTransformCount, frozenSamples;
            public string roomName, roomId, roomsStatus, initialCode, rejectionStatus, dialog;
            public int roomCount, humans, spectators, firstLocalId, rejoinedLocalId, observerInputSamples;
            public bool connectedEver, passwordRejected, roomFound, listedPassword, codePreserved, leftAndRejoined, cleanupCompleted;
            public bool spectatorObserved, spectatorExcluded, spectatorFirstPerson, spectatorInputsIgnored, spectatorStayedUntilLobby, spectatorConverted, hostRematched;
            public bool waitingListed, playingHidden, finishedHidden, waitingRestored, visibilityCodeAvailable;
            public int visibilitySamples;
            public string visibilityPhases;
            public bool swimLookUpRises, swimLookDownFalls, swimLevelKeepsHeight, swimIdleStops;
            public float swimUpHeight, swimDownHeight, swimLevelHeight, swimLevelDistance, swimIdleDrift, swimSettledDrift, swimStopSpeed;
            public int swimInputSamples;
            public string[] errors;
        }
        private AquariumSession session;
        private AquariumView view;
        private AquariumThirdPersonCamera followCamera;
        private string mode, reportPath, capturePath, hudCapturePath, codePath;
        private string password, roomName, roomIdPath, initialCode = "", rememberedRoomId = "", rejectionStatus = "";
        private float rejoinAfter, firstConnectedAt;
        private int firstLocalId, rejoinedLocalId, observerInputSamples, roomCount;
        private bool connectedEver, passwordRejected, roomFound, listedPassword, codePreserved = true, leftAndRejoined;
        private bool spectatorObserved, spectatorExcluded = true, spectatorFirstPerson, spectatorInputsIgnored = true, spectatorStayedUntilLobby = true, spectatorConverted;
        private bool roomRoundStarted, hostRematched, rejoinStarted, autoRematch, completing;
        private bool waitingListed, playingHidden, finishedHidden, waitingRestored, visibilityCodeAvailable = true;
        private int visibilitySamples;
        private string visibilityPhases = "";
        private readonly Dictionary<int, Point> observerPositions = new Dictionary<int, Point>();
        private float started, duration = 24, nextInput, finishAfter;
        private bool initialized, captured, hudCaptureRequested, revivalEggCreated, revived;
        private int revivalPlayer;
        private float revivalHatchAt;
        private RenderTexture hudTarget, previousTarget;
        private bool hudPending;
        private float minHeight = float.MaxValue, maxHeight = float.MinValue;
        private readonly List<string> errors = new List<string>();
        private readonly Dictionary<int, Matrix4x4> frozenTransforms = new Dictionary<int, Matrix4x4>();
        private readonly Dictionary<int, Matrix4x4> corpseTransforms = new Dictionary<int, Matrix4x4>();
        private bool scenarioAdvanced, realRuleDeath, deathReviewObserved, corpseVisible, corpseMotionless = true, diverSpectateObserved;
        private bool terminalImmediate, terminalSkippedIntro = true, terminalDeadActorsHidden = true, worldTransformsFrozen = true, cameraMovedWhileFrozen;
        private bool eggProtectedFromFinish, eggDeathIntroAllowed, targetSelected, targetOutlineSingle;
        private float deathReviewDuration, starvationAt;
        private int targetFishId, outlinedActors, outlineMeshes, frozenSamples;
        private Vector3 frozenCameraPosition;
        private Quaternion frozenCameraRotation;
        private AudioSource feedSource;
        private int feedStage, feedDiverId, feedEffectId, feedFarSamples;
        private float feedStageAt, feedNearGain, feedNearVolume, feedNearDistance, feedFarDistance, feedRemainingAtExit;
        private bool feedSourceFound, feedNearPlayed, feedMovedWhilePlaying, feedStoppedOnExit, feedFarEventObserved, feedFarSilent, feedFarPlaybackObserved;
        private int swimLookStage, swimInputSamples;
        private float swimStageAt, swimUpHeight, swimDownHeight, swimLevelHeight, swimLevelDistance, swimIdleDrift, swimSettledDrift, swimStopSpeed;
        private Point swimStart, swimSettledPosition;
        private bool swimLookUpRises, swimLookDownFalls, swimLevelKeepsHeight, swimIdleStops, swimSettledSampled;
        private bool SwimLookMode { get { return mode == "swimLookHider" || mode == "swimLookSeeker"; } }
        private bool ScenarioMode { get { return mode == "death" || mode == "terminal" || mode == "eggdeath" || mode == "target" || mode == "feed"; } }
        private bool RoomMode { get { return mode == "lobbyHost" || mode == "passwordClient" || mode == "wrongPassword" || mode == "roomHost" || mode == "observerClient" || mode == "rejoinClient"
            || mode == "roomCloudHost" || mode == "roomListClient" || mode == "roomCodeClient" || mode == "roomWrongPassword" || mode == "roomBrowser" || mode == "roomVisibility"; } }
        private bool LocalRoomMode { get { return mode == "host" || mode == "client" || mode == "lobbyHost" || mode == "passwordClient" || mode == "wrongPassword" || mode == "roomHost" || mode == "observerClient" || mode == "rejoinClient"; } }
        private bool ExpectedPasswordRejection { get { return mode == "wrongPassword" || mode == "roomWrongPassword"; } }
        private bool ControlledMode { get { return ScenarioMode || SwimLookMode || mode == "revival" || RoomMode || autoRematch; } }
        private static readonly BindingFlags PrivateFields = BindingFlags.Instance | BindingFlags.NonPublic;
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
            password = Arg("-aqPassword"); roomName = Arg("-aqRoomName"); roomIdPath = Arg("-aqRoomIdFile");
            if (roomName.Length == 0 && mode == "roomCloudHost") roomName = "Aquarium regression room";
            autoRematch = mode == "roomHost" || Array.IndexOf(Environment.GetCommandLineArgs(), "-aqAutoRematch") >= 0;
            float.TryParse(Arg("-aqRejoinAfter"), out rejoinAfter); if (mode == "rejoinClient" && rejoinAfter <= 0) rejoinAfter = 3;
            float requested; if (float.TryParse(Arg("-aqDuration"), out requested)) duration = requested;
            else if (mode == "feed") duration = 14;
            else if (SwimLookMode) duration = 14;
            else if (mode == "roomHost") duration = 46;
            else if (mode == "observerClient") duration = 37;
            else if (mode == "roomVisibility") duration = 55;
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
                    case "host": case "lobbyHost": case "roomHost": session.OpenLocalTest(true, "Test host", Team.Hider, password: password); break;
                    case "client": case "passwordClient": case "wrongPassword": case "rejoinClient": session.OpenLocalTest(false, "Test seeker", Team.Seeker, password: password); break;
                    case "observerClient": session.OpenLocalTest(false, "Late observer", Team.Hider, password: password); break;
                    case "relayHost": case "relayClient": case "roomCloudHost": case "roomListClient": case "roomCodeClient": case "roomWrongPassword": case "roomBrowser": _ = ConnectCloudScenario(); break;
                    case "roomVisibility": _ = TrackRoomVisibility(); break;
                    case "practice": session.StartPractice("Test swimmer", Team.Hider); break;
                    case "seeker": session.StartPractice("Test diver", Team.Seeker); break;
                    case "revival": StartRevivalScenario(); break;
                    case "swimLookHider": case "swimLookSeeker": StartSwimLookScenario(); break;
                    case "death": case "terminal": case "eggdeath": case "target": case "feed": StartScenario(); break;
                    case "menu": break;
                }
            }
            float elapsed = Time.realtimeSinceStartup - started;
            TrackRoomConnection(elapsed);
            if (ScenarioMode && session.Model != null) UpdateScenario(elapsed);
            if (SwimLookMode && session.Model != null) UpdateSwimLookScenario();
            if (session.Code.Length > 0 && codePath.Length > 0) { File.WriteAllText(codePath, session.Code); codePath = ""; }
            if (session.Connected)
            {
                var state = session.State;
                var p = Array.Find(state.players, value => value.id == session.LocalId);
                if (state.phase == RoundPhase.Lobby)
                {
                    if (!session.IsHost && p != null) session.Role(p.team, true);
                    else if ((mode == "host" || mode == "relayHost" || mode == "roomHost" || autoRematch) && !hostRematched && state.players.Length >= 2
                        && elapsed > (mode == "roomCloudHost" && autoRematch ? 10 : 3))
                    {
                        if (session.StartRound().Length == 0 && autoRematch && !roomRoundStarted)
                        {
                            roomRoundStarted = true;
                            foreach (var player in session.Model.Players) { player.bot = false; player.velocity = new Point(); }
                        }
                    }
                }
                if (autoRematch && session.IsHost)
                {
                    if (state.phase == RoundPhase.Playing && elapsed > (finishAfter > 0 ? finishAfter : 12))
                        foreach (var fish in session.Model.Fish) fish.eaten = true;
                    if (state.phase == RoundPhase.Finished && elapsed > 34 && state.time >= state.reviewEndsAt)
                    { session.Rematch(); hostRematched = session.Model.Phase == RoundPhase.Lobby; }
                }
                if (mode == "revival") CheckRevival(state);
                if (p != null && !ControlledMode && elapsed > nextInput)
                {
                    nextInput = elapsed + 0.05f;
                    var move = followCamera.Movement(0.15f, 0.35f);
                    var aim = followCamera.Aim(state, p);
                    session.Input(new SwimInput { x = move.x, y = move.y, z = move.z, aimX = aim.x, aimY = aim.y, aimZ = aim.z,
                        morph = true, action = true, feed = true, sprint = true, scan = true, layEgg = true });
                    if (state.phase == RoundPhase.Playing)
                    { minHeight = Mathf.Min(minHeight, p.position.y); maxHeight = Mathf.Max(maxHeight, p.position.y); }
                }
                if (!ControlledMode && state.phase == RoundPhase.Playing && followCamera.Active) followCamera.Look(0.12f * Time.unscaledDeltaTime * 60, 0, 0);
                if (!ControlledMode && finishAfter > 0 && session.IsHost && state.phase == RoundPhase.Playing && elapsed > finishAfter)
                    foreach (var fish in session.Model.Fish) fish.eaten = true;
                if (state.phase == RoundPhase.Finished && state.time < state.reviewEndsAt)
                {
                    followCamera.Look(0.08f * Time.unscaledDeltaTime * 60, 0, 0);
                    followCamera.MoveFree(0.1f, 0.05f, Time.unscaledDeltaTime);
                }
            }

            if (elapsed > duration && !completing)
            {
                completing = true;
                var s = session.State; var p = s == null ? null : Array.Find(s.players, value => value.id == session.LocalId);
                bool continuedPlaying = s != null && s.phase == RoundPhase.Playing;
                if (mode == "revival" && (!revivalEggCreated || !revived || !continuedPlaying))
                    errors.Add("Revival scenario failed: egg=" + revivalEggCreated + ", revived=" + revived + ", playing=" + continuedPlaying);
                ValidateScenario();
                ValidateRoomScenario();
                if (SwimLookMode && (!swimLookUpRises || !swimLookDownFalls || !swimLevelKeepsHeight || !swimIdleStops))
                    errors.Add("Look swimming failed: up=" + swimLookUpRises + ", down=" + swimLookDownFalls + ", level=" + swimLevelKeepsHeight
                        + ", idle=" + swimIdleStops + ", stage=" + swimLookStage);
                var report = new Report { mode = mode, connected = session.Connected, host = session.IsHost, code = mode == "roomVisibility" ? initialCode : session.Code, status = session.Status,
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
                    realRuleDeath = realRuleDeath, deathReviewObserved = deathReviewObserved, corpseVisible = corpseVisible,
                    corpseMotionless = corpseMotionless && corpseTransforms.Count > 0, diverSpectateObserved = diverSpectateObserved, deathReviewDuration = deathReviewDuration,
                    terminalImmediate = terminalImmediate, terminalSkippedIntro = terminalSkippedIntro,
                    terminalDeadActorsHidden = terminalDeadActorsHidden && terminalImmediate,
                    worldTransformsFrozen = worldTransformsFrozen && frozenSamples > 10, cameraMovedWhileFrozen = cameraMovedWhileFrozen,
                    eggProtectedFromFinish = eggProtectedFromFinish, eggDeathIntroAllowed = eggDeathIntroAllowed,
                    feedSourceFound = feedSourceFound, feedNearPlayed = feedNearPlayed, feedMovedWhilePlaying = feedMovedWhilePlaying,
                    feedStoppedOnExit = feedStoppedOnExit, feedFarEventObserved = feedFarEventObserved, feedFarSilent = feedFarSilent,
                    feedNearGain = feedNearGain, feedNearVolume = feedNearVolume, feedNearDistance = feedNearDistance,
                    feedFarDistance = feedFarDistance, feedRemainingAtExit = feedRemainingAtExit, feedFarSamples = feedFarSamples,
                    targetSelected = targetSelected, targetOutlineSingle = targetOutlineSingle, targetFishId = targetFishId,
                    outlinedActors = outlinedActors, outlineMeshes = outlineMeshes, frozenTransformCount = frozenTransforms.Count, frozenSamples = frozenSamples,
                    roomName = session.RoomName.Length > 0 ? session.RoomName : roomName, roomId = rememberedRoomId, roomsStatus = session.RoomsStatus, initialCode = initialCode, rejectionStatus = rejectionStatus, dialog = Arg("-aqDialog"),
                    roomCount = roomCount, humans = session.HumanCount, spectators = session.SpectatorCount, firstLocalId = firstLocalId, rejoinedLocalId = rejoinedLocalId,
                    observerInputSamples = observerInputSamples, connectedEver = connectedEver, passwordRejected = passwordRejected, roomFound = roomFound, listedPassword = listedPassword,
                    codePreserved = (connectedEver || mode == "roomVisibility") && codePreserved, leftAndRejoined = leftAndRejoined, spectatorObserved = spectatorObserved,
                    spectatorExcluded = spectatorObserved && spectatorExcluded, spectatorFirstPerson = spectatorFirstPerson,
                    spectatorInputsIgnored = observerInputSamples > 5 && spectatorInputsIgnored, spectatorStayedUntilLobby = spectatorObserved && spectatorStayedUntilLobby,
                    spectatorConverted = spectatorConverted, hostRematched = hostRematched,
                    waitingListed = waitingListed, playingHidden = playingHidden, finishedHidden = finishedHidden, waitingRestored = waitingRestored,
                    visibilityCodeAvailable = mode == "roomVisibility" && visibilityCodeAvailable && visibilitySamples > 0,
                    visibilitySamples = visibilitySamples, visibilityPhases = visibilityPhases,
                    swimLookUpRises = swimLookUpRises, swimLookDownFalls = swimLookDownFalls, swimLevelKeepsHeight = swimLevelKeepsHeight, swimIdleStops = swimIdleStops,
                    swimUpHeight = swimUpHeight, swimDownHeight = swimDownHeight, swimLevelHeight = swimLevelHeight, swimLevelDistance = swimLevelDistance,
                    swimIdleDrift = swimIdleDrift, swimSettledDrift = swimSettledDrift, swimStopSpeed = swimStopSpeed, swimInputSamples = swimInputSamples,
                    cameraDistance = p == null ? 0 : Vector3.Distance(view.Camera.transform.position, view.PlayerPosition(p)), errors = errors.ToArray() };
                enabled = false; _ = CompleteReport(report);
            }
        }

        private void StartSwimLookScenario()
        {
            session.StartPractice("Look swimming tester", mode == "swimLookSeeker" ? Team.Seeker : Team.Hider);
            foreach (var player in session.Model.Players)
            {
                player.bot = false; player.velocity = new Point();
                if (player.id != session.LocalId) player.position = new Point(10, 3, player.id - 4);
            }
            var local = session.Model.Player(session.LocalId);
            local.position = new Point(0, 3.5f, -3); local.facing = new Point(0, 0, 1);
            followCamera.Prepare(session.Model.Snapshot(), session.LocalId);
        }

        private void UpdateSwimLookScenario()
        {
            var model = session.Model; var local = model.Player(session.LocalId);
            if (local == null || model.Time < model.HuntAt) return;
            if (swimLookStage == 0)
            {
                swimLookStage = 1; swimStageAt = model.Time; swimStart = local.position;
                followCamera.Look(0, (followCamera.Pitch + 30) / 2.3f, 0);
            }
            else if (swimLookStage <= 3 && model.Time - swimStageAt >= 1)
            {
                var delta = local.position - swimStart;
                if (swimLookStage == 1) { swimUpHeight = delta.y; swimLookUpRises = delta.y > 1 && delta.z > 1; }
                else if (swimLookStage == 2) { swimDownHeight = delta.y; swimLookDownFalls = delta.y < -1 && delta.z > 1; }
                else
                {
                    swimLevelHeight = delta.y; swimLevelDistance = delta.Length;
                    swimLevelKeepsHeight = Mathf.Abs(delta.y) < 0.03f && delta.z > 2;
                }
                swimLookStage++; swimStageAt = model.Time;
                if (swimLookStage <= 3)
                {
                    // Reset only between legs so walls cannot manufacture a passing height check.
                    local.position = new Point(0, 3.5f, -3); local.velocity = new Point();
                    float wantedPitch = swimLookStage == 2 ? 30 : 0;
                    followCamera.Look(0, (followCamera.Pitch - wantedPitch) / 2.3f, 0);
                }
                swimStart = local.position;
            }
            else if (swimLookStage == 4)
            {
                if (!swimSettledSampled && model.Time - swimStageAt >= 0.3f)
                { swimSettledSampled = true; swimSettledPosition = local.position; }
                if (model.Time - swimStageAt >= 1)
                {
                    swimIdleDrift = (local.position - swimStart).Length; swimSettledDrift = (local.position - swimSettledPosition).Length;
                    swimStopSpeed = local.velocity.Length;
                    swimIdleStops = swimSettledSampled && swimIdleDrift > 0.01f && swimIdleDrift < 0.45f && swimSettledDrift < 0.01f && swimStopSpeed < 0.01f;
                    swimLookStage = 5;
                }
            }
            var move = swimLookStage <= 3 ? followCamera.Movement(0, 1) : new Point();
            var aim = followCamera.Aim(session.State, local);
            session.Input(new SwimInput { x = move.x, y = move.y, z = move.z, aimX = aim.x, aimY = aim.y, aimZ = aim.z });
            swimInputSamples++;
            minHeight = Mathf.Min(minHeight, local.position.y); maxHeight = Mathf.Max(maxHeight, local.position.y);
        }

        private string JoinCode()
        {
            string code = Arg("-aqJoin");
            string path = Arg("-aqCodeFile");
            return code.Length > 0 ? code : path.Length > 0 && File.Exists(path) ? File.ReadAllText(path).Trim() : "";
        }

        private async Task ConnectCloudScenario()
        {
            try
            {
                if (mode == "relayHost") { await session.OpenInternet(true, "", "Relay host", Team.Hider, password); return; }
                if (mode == "roomCloudHost") { await session.CreateRoom(roomName, password, "Room host", Team.Hider); return; }
                if (mode == "relayClient" || mode == "roomCodeClient")
                {
                    if (mode == "roomCodeClient") { await session.RefreshRooms(); roomCount = session.Rooms.Count; }
                    await session.OpenInternet(false, JoinCode(), "Code seeker", Team.Seeker, password); return;
                }
                AquariumRoom selected = null;
                string wantedId = roomIdPath.Length > 0 && File.Exists(roomIdPath) ? File.ReadAllText(roomIdPath).Trim() : "";
                string wantedCode = JoinCode();
                while (!completing && Time.realtimeSinceStartup - started < duration - 4)
                {
                    await session.RefreshRooms(); roomCount = session.Rooms.Count;
                    foreach (var room in session.Rooms)
                        if ((wantedId.Length > 0 && room.id == wantedId) || (wantedId.Length == 0 && roomName.Length > 0 && room.name == roomName)
                            || (wantedId.Length == 0 && roomName.Length == 0 && wantedCode.Length > 0 && room.code == wantedCode))
                        { selected = room; break; }
                    if (selected != null) break;
                    await Task.Delay(1300);
                }
                if (completing) return;
                if (selected == null) { errors.Add("Requested cloud room was not found in the real room query: " + session.RoomsStatus); return; }
                roomFound = true; listedPassword = selected.hasPassword; rememberedRoomId = selected.id; roomName = selected.name;
                if (mode == "roomBrowser") { OpenSmokeDialog(selected); return; }
                await session.JoinRoom(selected.id, password, "Listed seeker", Team.Seeker);
            }
            catch (Exception ex) { errors.Add("Cloud smoke connection failed: " + ex.Message); }
        }

        private void OpenSmokeDialog(AquariumRoom selected)
        {
            string requested = Arg("-aqDialog"); if (requested.Length == 0) return;
            var game = GetComponent<AquariumGame>();
            var method = typeof(AquariumGame).GetMethod("OpenRoomDialog", PrivateFields);
            if (method == null) { errors.Add("Room dialog entry point is unavailable."); return; }
            typeof(AquariumGame).GetField("selectedRoomId", PrivateFields).SetValue(game, selected.id);
            typeof(AquariumGame).GetField("selectedRoomName", PrivateFields).SetValue(game, selected.name);
            method.Invoke(game, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, requested, true) });
        }

        private async Task TrackRoomVisibility()
        {
            try
            {
                initialCode = JoinCode().Trim().ToUpperInvariant();
                if (initialCode.Length == 0) throw new InvalidOperationException("Visibility probe needs -aqJoin or a populated -aqCodeFile.");
                rememberedRoomId = roomIdPath.Length > 0 && File.Exists(roomIdPath) ? File.ReadAllText(roomIdPath).Trim() : "";
                string previousPhase = "";
                while (!completing && Time.realtimeSinceStartup - started < duration - 3)
                {
                    await session.RefreshRooms(); roomCount = session.Rooms.Count;
                    if (completing) return;
                    if (session.RoomsStatus.IndexOf("못했습니다", StringComparison.Ordinal) >= 0)
                        throw new InvalidOperationException(session.RoomsStatus);
                    var lobby = await AquariumRooms.FindByCodeAsync(initialCode);
                    if (completing) return;
                    visibilitySamples++;
                    if (lobby == null)
                    { visibilityCodeAvailable = false; errors.Add("The room disappeared from code search during the visibility probe."); return; }
                    var room = AquariumRooms.Describe(lobby);
                    if (rememberedRoomId.Length == 0) rememberedRoomId = room.id;
                    visibilityCodeAvailable &= room.id == rememberedRoomId;
                    codePreserved &= room.code == initialCode;
                    roomName = room.name;
                    bool listed = false;
                    foreach (var listedRoom in session.Rooms)
                        if (listedRoom.id == rememberedRoomId) { listed = true; listedPassword = listedRoom.hasPassword; roomFound = true; }
                    if (room.phase != previousPhase)
                        visibilityPhases += (visibilityPhases.Length == 0 ? "" : " -> ") + room.phase;
                    // List and code queries are separate service requests. Skip transition samples
                    // so a phase change between the two responses cannot produce a false mismatch.
                    if (room.phase == previousPhase)
                    {
                        if (room.phase == AquariumRooms.WaitingPhase)
                        {
                            if (!playingHidden && !finishedHidden) waitingListed |= listed;
                            else if (playingHidden && finishedHidden) waitingRestored |= listed;
                        }
                        else if (room.phase == "Playing" && waitingListed)
                        { playingHidden |= !listed; if (listed) errors.Add("A stable Playing room remained visible in the waiting-room list."); }
                        else if (room.phase == "Finished" && playingHidden)
                        { finishedHidden |= !listed; if (listed) errors.Add("A stable Finished room remained visible in the waiting-room list."); }
                    }
                    previousPhase = room.phase;
                    if (waitingListed && playingHidden && finishedHidden && waitingRestored) return;
                    // Together with the shared 1.1-second query gate, this samples about every two seconds.
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex) { errors.Add("Room visibility probe failed: " + ex.Message); }
        }

        private void TrackRoomConnection(float elapsed)
        {
            if (ExpectedPasswordRejection && !session.Connected && !session.Busy && session.Status.IndexOf("비밀번호", StringComparison.Ordinal) >= 0)
            { passwordRejected = true; rejectionStatus = session.Status; }
            if (!session.Connected) return;
            if (!connectedEver)
            {
                connectedEver = true; firstConnectedAt = elapsed; firstLocalId = session.LocalId; initialCode = session.Code;
            }
            else codePreserved &= session.Code == initialCode;
            if (session.RoomId.Length > 0) rememberedRoomId = session.RoomId;
            if (session.IsHost && roomIdPath.Length > 0 && rememberedRoomId.Length > 0)
            { File.WriteAllText(roomIdPath, rememberedRoomId); roomIdPath = ""; }
            if (rejoinStarted)
            { leftAndRejoined = true; rejoinedLocalId = session.LocalId; }
            if (rejoinAfter > 0 && !rejoinStarted && !session.IsHost && elapsed - firstConnectedAt >= rejoinAfter)
            { rejoinStarted = true; _ = RejoinRoom(); }
        }

        private async Task RejoinRoom()
        {
            try
            {
                string code = session.Code;
                await session.ShutdownAsync();
                // Let the host observe the transport disconnect before reusing the same room.
                await Task.Delay(700);
                if (completing) return;
                if (LocalRoomMode) session.OpenLocalTest(false, "Rejoined seeker", Team.Seeker, password: password);
                else await session.OpenInternet(false, code, "Rejoined seeker", Team.Seeker, password);
            }
            catch (Exception ex) { errors.Add("Same-code rejoin failed: " + ex.Message); }
        }

        private void ObserveRoomScenario(AquariumSnapshot state)
        {
            if (mode != "observerClient" || !session.Connected) return;
            var local = Array.Find(state.players, player => player.id == session.LocalId);
            if (session.IsSpectator)
            {
                spectatorObserved = true; spectatorExcluded &= local == null;
                if (state.phase != RoundPhase.Playing) return;
                var watched = Array.Find(state.players, player => player.id == session.SpectatingId);
                spectatorFirstPerson |= watched != null && followCamera.FirstPerson && followCamera.FollowId == watched.id && !followCamera.DeathReview;
                foreach (var player in state.players)
                {
                    Point position;
                    if (observerPositions.TryGetValue(player.id, out position)) spectatorInputsIgnored &= (player.position - position).Length < 0.01f;
                    else observerPositions[player.id] = player.position;
                }
                spectatorInputsIgnored &= local == null && state.players.Length == AquariumModel.MaxPlayers;
                session.Input(new SwimInput { x = 1, y = 1, z = 1, aimZ = 1, action = true, morph = true, feed = true, sprint = true, scan = true, layEgg = true });
                followCamera.Look(30, 20, 2); observerInputSamples++;
            }
            else if (spectatorObserved)
            {
                if (state.phase == RoundPhase.Lobby && local != null) spectatorConverted = true;
                else if (!spectatorConverted) spectatorStayedUntilLobby = false;
            }
        }

        private void ValidateRoomScenario()
        {
            if (!RoomMode && !autoRematch && rejoinAfter <= 0) return;
            if (mode == "roomVisibility")
            {
                if (!waitingListed || !playingHidden || !finishedHidden || !waitingRestored || !visibilityCodeAvailable || !codePreserved)
                    errors.Add("Room visibility sequence failed: waiting=" + waitingListed + ", playingHidden=" + playingHidden + ", finishedHidden=" + finishedHidden
                        + ", restored=" + waitingRestored + ", codeAvailable=" + visibilityCodeAvailable + ", sameCode=" + codePreserved + ", phases=" + visibilityPhases);
                return;
            }
            if (ExpectedPasswordRejection)
            {
                if (!passwordRejected || connectedEver) errors.Add("Incorrect password was not rejected with a password-specific message: " + session.Status);
                return;
            }
            if (mode == "roomBrowser")
            {
                if (!roomFound || session.Connected) errors.Add("Room browser did not display the requested real room without joining it.");
                if (capturePath.Length > 0 && !captured) errors.Add("Real room browser screenshot was not captured.");
                if (hudCapturePath.Length > 0 && !File.Exists(hudCapturePath)) errors.Add("Real room browser HUD screenshot was not captured.");
                return;
            }
            if (!connectedEver) errors.Add("Expected room connection was never established: " + session.Status);
            if (connectedEver && !codePreserved) errors.Add("Room code changed across the tested session transitions.");
            if (mode == "roomCloudHost" && rememberedRoomId.Length == 0) errors.Add("Cloud room creation returned no room ID.");
            if (mode == "roomListClient" && !roomFound) errors.Add("List join did not find the requested room.");
            if (rejoinAfter > 0 && !leftAndRejoined) errors.Add("Client did not reconnect after leaving the same room code.");
            if (autoRematch && (!roomRoundStarted || !hostRematched)) errors.Add("Host did not complete the initial round and return to the lobby.");
            if (mode == "observerClient" && (!spectatorObserved || !spectatorExcluded || !spectatorFirstPerson || !spectatorInputsIgnored
                || observerInputSamples <= 5 || !spectatorStayedUntilLobby || !spectatorConverted))
                errors.Add("Observer flow failed: observed=" + spectatorObserved + ", excluded=" + spectatorExcluded + ", firstPerson=" + spectatorFirstPerson
                    + ", ignoredInput=" + spectatorInputsIgnored + ", samples=" + observerInputSamples + ", waitedForLobby=" + spectatorStayedUntilLobby + ", converted=" + spectatorConverted);
        }

        private async Task CompleteReport(Report report)
        {
            try { await session.ShutdownAsync(); report.cleanupCompleted = true; }
            catch (Exception ex) { errors.Add("Room cleanup failed: " + ex.Message); }
            report.errors = errors.ToArray();
            if (reportPath.Length > 0) File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Application.Quit(errors.Count == 0 ? 0 : 1);
        }

        private void StartScenario()
        {
            session.StartPractice("Scenario swimmer", Team.Hider);
            foreach (var player in session.Model.Players)
            {
                player.bot = false; player.velocity = new Point();
                if (player.id != session.LocalId) player.position = new Point(9, 3, player.id - 4);
            }
            var local = session.Model.Player(session.LocalId);
            local.position = new Point(-9, 3, -2); local.facing = new Point(0, 0, 1); local.species = 1; local.revealUntil = 0;
            if (mode == "feed")
            {
                var diver = session.Model.Players.Find(player => player.team == Team.Seeker);
                diver.position = new Point(0, 3, 0); diver.facing = new Point(0, 0, 1); feedDiverId = diver.id;
                local.position = new Point(0, 3, 8);
                var field = typeof(AquariumGame).GetField("feedAudioSource", PrivateFields);
                feedSource = field == null ? null : field.GetValue(GetComponent<AquariumGame>()) as AudioSource;
                feedSourceFound = feedSource != null;
                return;
            }
            if (mode == "target")
            {
                targetFishId = session.Model.Fish[0].id;
                PinTargetScenario();
                return;
            }
            starvationAt = session.Model.HuntAt + AquariumModel.HungerSeconds;
            foreach (var player in session.Model.Players)
            {
                if (player.id == session.LocalId || player.team != Team.Hider) continue;
                player.lastMealAt = starvationAt;
                if (mode == "terminal" || mode == "eggdeath")
                { player.alive = false; player.diedAt = session.Model.Time; player.deathSpecies = player.species; player.deathReason = "setup"; }
            }
            if (mode == "terminal") session.Model.Fish[1].dead = true;
            if (mode == "eggdeath") local.personalEaten = AquariumModel.EggFoodRequired;
        }

        private void UpdateScenario(float elapsed)
        {
            if (mode == "feed") { UpdateFeedScenario(elapsed); return; }
            if (mode == "target") { if (elapsed > 0.25f) PinTargetScenario(); return; }
            if (scenarioAdvanced || elapsed < 1.2f) return;
            scenarioAdvanced = true;
            // Advance through the real rules to just before a full sixty-second hunger deadline.
            // The actual death is then produced by the ordinary session tick, never by setting alive=false.
            while (session.Model.Time < starvationAt - 0.5f && session.Model.Phase == RoundPhase.Playing)
                session.Model.Tick(Mathf.Min(0.05f, starvationAt - 0.5f - session.Model.Time));
            if (mode == "eggdeath") session.Input(new SwimInput { layEgg = true });
        }

        private void RequestScenarioFeed()
        {
            var diver = session.Model.Player(feedDiverId);
            diver.nextFeed = 0;
            session.Model.SetInput(diver.id, new SwimInput { feed = true, aimZ = 1 });
        }

        private WaterEffect NewFeedEffect()
        {
            WaterEffect latest = null;
            foreach (var effect in session.Model.Effects)
                if (effect.kind == "feed" && effect.id > feedEffectId && (latest == null || effect.id > latest.id)) latest = effect;
            return latest;
        }

        private void UpdateFeedScenario(float elapsed)
        {
            if (feedSource == null || feedStage < 0 || feedStage >= 6) return;
            var local = session.Model.Player(session.LocalId);
            if (feedStage == 0)
            {
                if (session.Model.Time < session.Model.HuntAt + 0.1f) return;
                RequestScenarioFeed(); feedStage = 1; feedStageAt = elapsed; return;
            }
            if (feedStage == 1)
            {
                var effect = NewFeedEffect();
                if (effect != null && feedSource.isPlaying && feedSource.volume > 0)
                {
                    feedEffectId = effect.id; feedNearDistance = (effect.position - local.position).Length;
                    feedNearGain = AquariumModel.FeedSoundGain(effect.position, local.position); feedNearVolume = feedSource.volume;
                    feedNearPlayed = feedNearGain > 0 && feedNearDistance < 9;
                    feedStage = 2; feedStageAt = elapsed;
                }
                else if (elapsed - feedStageAt > 1.2f)
                { errors.Add("Feed source did not play after an authoritative nearby feed action."); feedStage = -1; }
                return;
            }
            if (feedStage == 2)
            {
                if (elapsed - feedStageAt < 0.1f) return;
                feedMovedWhilePlaying = feedSource.isPlaying && feedSource.clip != null;
                feedRemainingAtExit = feedSource.clip == null ? 0 : feedSource.clip.length - feedSource.time;
                local.position = new Point(12, 3, 7); local.velocity = new Point();
                feedStage = 3; feedStageAt = elapsed; return;
            }
            if (feedStage == 3)
            {
                // The source must stop before the original clip could finish naturally.
                if (feedMovedWhilePlaying && !feedSource.isPlaying && elapsed - feedStageAt < feedRemainingAtExit - 0.03f)
                    feedStoppedOnExit = true;
                if (elapsed - feedStageAt < 1) return;
                RequestScenarioFeed(); feedStage = 4; feedStageAt = elapsed; return;
            }
            if (feedStage == 4)
            {
                var effect = NewFeedEffect();
                if (effect != null)
                {
                    feedEffectId = effect.id; feedFarEventObserved = true; feedFarDistance = (effect.position - local.position).Length;
                    feedFarPlaybackObserved = feedSource.isPlaying; feedFarSamples = 1; feedStage = 5; feedStageAt = elapsed;
                }
                else if (elapsed - feedStageAt > 1.2f)
                { errors.Add("The second authoritative feed action produced no event."); feedStage = -1; }
                return;
            }
            feedFarSamples++; feedFarPlaybackObserved |= feedSource.isPlaying;
            if (elapsed - feedStageAt > 0.85f)
            {
                feedFarSilent = feedFarDistance > AquariumModel.FeedSoundRadius && !feedFarPlaybackObserved && feedFarSamples > 5;
                feedStage = 6;
            }
        }

        private void PinTargetScenario()
        {
            var local = session.Model.Player(session.LocalId);
            for (int i = 0; i < session.Model.Fish.Count; i++)
            {
                var fish = session.Model.Fish[i]; fish.turnAt = float.MaxValue;
                if (fish.id == targetFishId)
                {
                    var position = followCamera.Active
                        ? view.Camera.transform.position + view.Camera.transform.forward * (Vector3.Distance(view.Camera.transform.position, new Vector3(local.position.x, local.position.y, local.position.z)) + 2.15f)
                        : new Vector3(local.position.x, local.position.y + 0.1f, local.position.z + 2.15f);
                    fish.position = AquariumThirdPersonCamera.ToPoint(position); fish.facing = new Point(1, 0, 0);
                }
                else fish.position = new Point(3 + i % 7 * 1.3f, 1.2f + i % 4, -6 + i / 7 * 2.5f);
            }
        }

        private void LateUpdate()
        {
            if (!initialized || !enabled) return;
            var state = session.State; var local = state == null ? null : Array.Find(state.players, p => p.id == session.LocalId);
            float elapsed = Time.realtimeSinceStartup - started;
            if (state != null) ObserveRoomScenario(state);
            if (ScenarioMode && state != null && local != null) ObserveScenario(state, local, elapsed);
            bool captureNow = elapsed > duration - 3;
            if (mode == "roomBrowser") captureNow &= roomFound;
            if (mode == "roomVisibility") captureNow = playingHidden && !finishedHidden;
            if (ScenarioMode)
            {
                if (mode == "feed") captureNow = feedStage == 6;
                else if (mode == "target") captureNow = elapsed > 2 && targetOutlineSingle;
                else if (mode == "terminal") captureNow = frozenSamples > 10 && cameraMovedWhileFrozen;
                else captureNow = !local.alive && followCamera.DeathReview && state.time - local.diedAt >= 0.7f;
            }
            if (captureNow)
            {
                if (!captured && capturePath.Length > 0) { CaptureWorld(); captured = true; }
                if (!hudCaptureRequested && hudCapturePath.Length > 0) { hudCaptureRequested = true; hudPending = true; }
            }
        }

        private void ObserveScenario(AquariumSnapshot state, Swimmer local, float elapsed)
        {
            if (mode == "feed") return;
            if (mode == "target")
            {
                if (elapsed < 1) return;
                var selected = view.FindMorphTarget(state, local, followCamera.Aim(state, local));
                targetSelected = selected != null && !selected.player && selected.id == targetFishId;
                outlinedActors = outlineMeshes = 0;
                var actors = (IDictionary)typeof(AquariumView).GetField("actors", PrivateFields).GetValue(view);
                foreach (DictionaryEntry entry in actors)
                {
                    var root = ActorRoot(entry.Value); int parts = 0;
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                        if (renderer.gameObject.name == "Candidate silhouette" && renderer.enabled && renderer.gameObject.activeInHierarchy) parts++;
                    if (parts > 0) { outlinedActors++; outlineMeshes += parts; }
                }
                var outlined = typeof(AquariumView).GetField("outlinedActor", PrivateFields).GetValue(view);
                var expected = FindActor("f" + targetFishId);
                targetOutlineSingle = targetSelected && outlinedActors == 1 && outlineMeshes > 0 && outlined != null && ActorRoot(outlined) == expected;
                return;
            }
            if (local.alive) return;
            realRuleDeath |= local.deathReason == "starved" && Mathf.Abs(local.diedAt - starvationAt) < 0.12f;
            float sinceDeath = state.time - local.diedAt;
            if (mode == "terminal")
            {
                if (followCamera.DeathReview) terminalSkippedIntro = false;
                if (sinceDeath < 0.2f) terminalImmediate |= state.phase == RoundPhase.Finished && followCamera.FreeView && !followCamera.DeathReview;
                if (state.phase != RoundPhase.Finished) return;
                foreach (var player in state.players) if (!player.alive && FindActor("p" + player.id) != null) terminalDeadActorsHidden = false;
                foreach (var fish in state.fish) if (fish.dead && FindActor("f" + fish.id) != null) terminalDeadActorsHidden = false;
                if (sinceDeath < 0.3f) return;
                var world = (Transform)typeof(AquariumView).GetField("root", PrivateFields).GetValue(view);
                if (frozenTransforms.Count == 0)
                {
                    RememberTransforms(world, frozenTransforms); frozenCameraPosition = view.Camera.transform.position; frozenCameraRotation = view.Camera.transform.rotation;
                }
                else
                {
                    frozenSamples++; worldTransformsFrozen &= TransformsMatch(world, frozenTransforms);
                    cameraMovedWhileFrozen |= Vector3.Distance(frozenCameraPosition, view.Camera.transform.position) > 0.1f || Quaternion.Angle(frozenCameraRotation, view.Camera.transform.rotation) > 1;
                }
                return;
            }
            if (followCamera.DeathReview)
            {
                deathReviewObserved = true;
                if (mode == "eggdeath") eggDeathIntroAllowed |= state.phase == RoundPhase.Playing;
            }
            if (followCamera.SpectatingDiver && !diverSpectateObserved)
            { diverSpectateObserved = true; deathReviewDuration = sinceDeath; }
            var corpse = FindActor("p" + local.id);
            if (corpse != null)
            {
                foreach (var renderer in corpse.GetComponentsInChildren<Renderer>()) if (renderer.enabled && renderer.gameObject.activeInHierarchy) corpseVisible = true;
                if (corpseTransforms.Count == 0) RememberTransforms(corpse.transform, corpseTransforms);
                else corpseMotionless &= TransformsMatch(corpse.transform, corpseTransforms);
            }
            if (mode == "eggdeath" && state.eggs != null && state.eggs.Length > 0 && !Array.Exists(state.players, p => p.team == Team.Hider && p.alive))
                eggProtectedFromFinish |= state.phase == RoundPhase.Playing;
        }

        private GameObject FindActor(string key)
        {
            var actors = (IDictionary)typeof(AquariumView).GetField("actors", PrivateFields).GetValue(view);
            return actors.Contains(key) ? ActorRoot(actors[key]) : null;
        }
        private static GameObject ActorRoot(object actor)
        { return (GameObject)actor.GetType().GetField("root", BindingFlags.Instance | BindingFlags.Public).GetValue(actor); }
        private static void RememberTransforms(Transform root, Dictionary<int, Matrix4x4> poses)
        {
            foreach (var part in root.GetComponentsInChildren<Transform>(true)) poses[part.GetInstanceID()] = part.localToWorldMatrix;
        }
        private static bool TransformsMatch(Transform root, Dictionary<int, Matrix4x4> poses)
        {
            var parts = root.GetComponentsInChildren<Transform>(true); if (parts.Length != poses.Count) return false;
            foreach (var part in parts)
            {
                Matrix4x4 before; if (!poses.TryGetValue(part.GetInstanceID(), out before)) return false;
                var now = part.localToWorldMatrix;
                for (int i = 0; i < 16; i++) if (Mathf.Abs(before[i] - now[i]) > 0.00001f) return false;
            }
            return true;
        }
        private void ValidateScenario()
        {
            if (!ScenarioMode) return;
            if (capturePath.Length > 0 && !captured) errors.Add("Scenario world screenshot was not captured at its assertion point.");
            if (hudCapturePath.Length > 0 && (!hudCaptureRequested || !File.Exists(hudCapturePath))) errors.Add("Scenario HUD screenshot was not captured.");
            if (mode == "feed")
            {
                if (!feedSourceFound || !feedNearPlayed || !feedMovedWhilePlaying || !feedStoppedOnExit || !feedFarEventObserved || !feedFarSilent)
                    errors.Add("Feed audio assertions failed: source=" + feedSourceFound + ", near=" + feedNearPlayed + ", movedDuringClip=" + feedMovedWhilePlaying
                        + ", stoppedOnExit=" + feedStoppedOnExit + ", farEvent=" + feedFarEventObserved + ", farSilent=" + feedFarSilent);
                return;
            }
            if (mode == "target")
            {
                if (!targetSelected || !targetOutlineSingle) errors.Add("Target outline assertion failed: selected=" + targetSelected + ", actors=" + outlinedActors + ", meshes=" + outlineMeshes);
                return;
            }
            if (!realRuleDeath) errors.Add("Local death was not produced at the real hunger deadline.");
            if (mode == "terminal")
            {
                if (!terminalImmediate || !terminalSkippedIntro || !terminalDeadActorsHidden) errors.Add("Terminal death did not immediately enter free view with dead actors hidden.");
                if (!worldTransformsFrozen || frozenSamples <= 10 || !cameraMovedWhileFrozen) errors.Add("Terminal freeze assertion failed: frozen=" + worldTransformsFrozen + ", samples=" + frozenSamples + ", cameraMoved=" + cameraMovedWhileFrozen);
                return;
            }
            if (!deathReviewObserved || !corpseVisible || !corpseMotionless || !diverSpectateObserved || deathReviewDuration < 2.4f || deathReviewDuration > 2.85f)
                errors.Add("Death sequence assertion failed: intro=" + deathReviewObserved + ", corpse=" + corpseVisible + ", stationary=" + corpseMotionless + ", spectating=" + diverSpectateObserved + ", introSeconds=" + deathReviewDuration);
            if (mode == "eggdeath" && (!eggProtectedFromFinish || !eggDeathIntroAllowed)) errors.Add("Active revival egg did not preserve the playing phase and death intro.");
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
