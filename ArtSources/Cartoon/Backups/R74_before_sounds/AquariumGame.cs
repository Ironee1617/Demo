using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.SceneManagement;

namespace PrototypeLab.Aquarium
{
    public sealed partial class AquariumGame : MonoBehaviour
    {
        private static readonly ProfilerMarker drawMarker=new ProfilerMarker("Aquarium.Draw"),sessionMarker=new ProfilerMarker("Aquarium.Session"),cameraMarker=new ProfilerMarker("Aquarium.Camera"),targetMarker=new ProfilerMarker("Aquarium.Target");
        private AquariumSession session;
        private AquariumView view;
        private AquariumThirdPersonCamera followCamera;
        private AquariumModel backdrop;
        private string playerName = "Diver", joinCode = "", notice = "";
        private enum RoomDialog { None, Create, CodeJoin, RoomJoin }
        private RoomDialog roomDialog;
        private string roomNameDraft = "", roomPassword = "", roomDialogError = "", selectedRoomId = "", selectedRoomName = "";
        private bool roomsRequested, wasConnected, roomSubmitting, roomRequestPending;
        private int roomRequestVersion;
        private float nextRoomsRefreshAt;
        private Vector2 roomScroll;
        private Team selectedTeam;
        // 2026-10-28: the alien picked in the lobby (AquariumAlienKinds), remembered between sessions.
        private int selectedAlien;
        private AquariumAlienPreview alienPreview;
        private readonly Dictionary<(GUIStyle, Color), GUIStyle> tintedStyles = new Dictionary<(GUIStyle, Color), GUIStyle>();
        private bool pause, showRules, pendingAction, pendingMorph, pendingFeed, pendingAbility, pendingEgg, pendingUnmorph, pendingInteract;
        // R27: the drain part under the crosshair and the one the queued F/E press was aimed at.
        private DrainTarget drainTarget;
        private int pendingInteractDrain = -1, pendingInteractPart = -1;
        private float inputTimer;
        private MorphTarget morphTarget;
        private int pendingMorphTargetId;
        private bool pendingMorphTargetPlayer;
        private RoundPhase previousPhase;
        private GUIStyle title, heading, body, small, button, primary, field, number, roomNameText, keycap;
        private Font font;
        private readonly Color ink = new Color(0.035f, 0.09f, 0.15f, 0.97f);
        private readonly Color cyan = new Color(0.38f, 0.96f, 0.9f);
        private readonly Color muted = new Color(0.61f, 0.75f, 0.81f);
        private AudioSource audioSource, feedAudioSource;
        private Point feedSoundPosition;
        private readonly Dictionary<string, AudioClip> sounds = new Dictionary<string, AudioClip>();
        private int lastEffect;
        private bool automated;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private AquariumSmokeDriver smoke;
#endif

        private void Awake()
        {
            Application.runInBackground = true;
            // R66: cap at the monitor's refresh rate (144 on a 144 Hz screen) instead of 60, so presets without vSync can pass 100 fps.
            Application.targetFrameRate = Mathf.Max(60, Mathf.CeilToInt((float)Screen.currentResolution.refreshRateRatio.value));
            useGUILayout = false;   // R66: no GUILayout anywhere, so skip the extra layout pass of OnGUI every frame
            WarmShaders();          // R66 (AquariumGameR66.cs)
            LoadGraphicsChoice();   // R66: the pause menu's picture option
            session = new AquariumSession(); view = new AquariumView(transform); backdrop = new AquariumModel(speciesCount: AquariumSpecies.Count);
            followCamera = new AquariumThirdPersonCamera(view.Camera);
            playerName = PlayerPrefs.GetString("Aquarium.Name", "Diver");
            selectedAlien = AquariumAlienKinds.Clamp(PlayerPrefs.GetInt("Aquarium.Alien", 0));
            selectedDiver = AquariumDiverKinds.Clamp(PlayerPrefs.GetInt("Aquarium.Diver", 0)); session.PreferredDiver = selectedDiver;
            session.PreferredMap = AquariumMaps.Clamp(PlayerPrefs.GetInt("Aquarium.Map", 0));
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.volume = 0.24f; audioSource.spatialBlend = 0;
            feedAudioSource = gameObject.AddComponent<AudioSource>(); feedAudioSource.volume = 0.38f; feedAudioSource.spatialBlend = 0;
            sounds["eat"] = Tone("eat", 700, 1150, 0.13f); sounds["shot"] = Tone("shot", 180, 55, 0.12f);
            sounds["burp"] = Tone("burp", 110, 370, 0.45f); sounds["morph"] = Tone("morph", 340, 820, 0.25f);
            sounds["caught"] = Tone("caught", 450, 90, 0.25f);
            sounds["hatch"] = Tone("hatch", 520, 1050, 0.5f); sounds["scan"] = Tone("scan", 300, 650, 0.35f);
            sounds["egg"] = Tone("egg", 500, 740, 0.2f);
            // R27 drains: a metallic tick as a screw comes free, the lid's grinding scrape, the escape gurgle.
            sounds["screwOut"] = Tone("screwOut", 1450, 980, 0.09f); sounds["lidOpen"] = Tone("lidOpen", 95, 70, 0.6f);
            sounds["lidClosed"] = Tone("lidClosed", 140, 60, 0.35f); sounds["squeezePop"] = Tone("squeezePop", 260, 620, 0.18f);
            sounds["escape"] = Tone("escape", 620, 140, 0.8f);
            // R47: the diver's lid-open alarm.
            sounds["alarm"] = Tone("alarm", 980, 600, 0.55f);
            sounds["feed"] = FeedSound(); sounds["splash"] = SplashSound();
            LoadSoundSet();   // R73: the synthesised WAV set replaces the tones above when present
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            smoke = gameObject.AddComponent<AquariumSmokeDriver>();
            smoke.Initialize(session, view, followCamera); automated = smoke.enabled;
#endif
        }

        private AudioClip Tone(string name, float start, float end, float seconds)
        {
            int count = (int)(22050 * seconds); var data = new float[count]; float phase = 0;
            for (int i = 0; i < count; i++)
            { float p = (float)i / count; phase += Mathf.Lerp(start, end, p) * Mathf.PI * 2 / 22050; data[i] = Mathf.Sin(phase) * Mathf.Sin(p * Mathf.PI) * 0.4f; }
            var clip = AudioClip.Create(name, count, 1, 22050, false); clip.SetData(data, 0); return clip;
        }

        // The diver's entry: a bright splash (filtered noise, sharp attack), a low plunge thump and a bubble gurgle.
        private AudioClip SplashSound()
        {
            const int rate = 22050;
            var data = new float[(int)(rate * 1.1f)]; var noise = new System.Random(731); float low = 0, high = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate, attack = Mathf.Clamp01(t / .012f);
                float white = (float)noise.NextDouble() * 2 - 1; low = Mathf.Lerp(low, white, .08f); high = Mathf.Lerp(high, white, .55f);
                float splash = (high - low) * Mathf.Exp(-t * 7.5f) * attack;
                float thump = Mathf.Sin(2 * Mathf.PI * (95 * t - 40 * t * t)) * Mathf.Exp(-t * 11) * attack;
                float gurgle = Mathf.Sin(2 * Mathf.PI * (420 * t + 900 * t * t)) * Mathf.Exp(-(t - .35f) * (t - .35f) * 30) * (.5f + .5f * Mathf.Sin(t * 70));
                data[i] = splash * .55f + thump * .35f + gurgle * .12f;
            }
            var clip = AudioClip.Create("dive splash", data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }

        private AudioClip FeedSound()
        {
            const int rate = 22050;
            var data = new float[(int)(rate * 0.65f)]; var noise = new System.Random(418); float filtered = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate, envelope = Mathf.Sin(Mathf.PI * i / data.Length);
                filtered = Mathf.Lerp(filtered, (float)noise.NextDouble() * 2 - 1, 0.16f);
                float bubbles = Mathf.Sin(2 * Mathf.PI * (280 * t + 620 * t * t)) * Mathf.Exp(-t * 5);
                float ripple = 0.6f + 0.4f * Mathf.Sin(t * 65);
                data[i] = (filtered * 0.65f + bubbles * 0.22f) * envelope * ripple;
            }
            var clip = AudioClip.Create("feed bubbles", data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }

        private Swimmer Local()
        {
            if (session.State == null) return null;
            if (session.IsSpectator)
                return PlayerById(session.State.players, session.SpectatingId)
                    ?? FirstAliveSeeker(session.State.players)
                    ?? FirstAlive(session.State.players);
            return PlayerById(session.State.players, session.LocalId);
        }

        private Swimmer SoundListener()
        {
            var listener = Local();
            if (listener != null && !listener.alive && session.State.time - listener.diedAt >= AquariumModel.DeathReviewSeconds)
                listener = FirstAliveSeeker(session.State.players) ?? listener;
            return listener;
        }

        private void UpdateFeedSound()
        {
            if (!feedAudioSource.isPlaying) return;
            var listener = SoundListener();
            float gain = listener == null ? 0 : AquariumModel.FeedSoundGain(feedSoundPosition, listener.position);
            if (gain <= 0) { feedAudioSource.Stop(); return; }
            feedAudioSource.volume = 0.38f * gain;
            var delta = new Vector3(feedSoundPosition.x - listener.position.x, feedSoundPosition.y - listener.position.y, feedSoundPosition.z - listener.position.z);
            feedAudioSource.panStereo = Vector3.Dot(view.Camera.transform.right, delta.normalized) * 0.65f;
        }

        private void Update()
        {
            ApplyGraphicsChoice();   // R66
            if (Input.GetKeyDown(KeyCode.Escape) && chatTyping) { chatBlur = true; }   // R60: Esc closes the chat line first
            else if (Input.GetKeyDown(KeyCode.Escape))
            { if (roomDialog != RoomDialog.None) CloseRoomDialog(true); else if (showRules) showRules = false; else if (session.Connected) { if (session.State.phase != RoundPhase.Lobby) pause = !pause; } else menuPanel = MenuPanel.None; }   // R73: no pause menu in the lobby
            UpdateRoomMenuState();
            ControlsGuideInput();   // R70b: K shows / hides the controls guide
            var p = Local();
            followCamera.Prepare(session.State, session.LocalId, session.SpectatingId);
            bool playing = session.Connected && session.State.phase == RoundPhase.Playing;
            bool reviewing = session.Connected && session.State.phase == RoundPhase.Finished && session.State.time < session.State.reviewEndsAt;
            bool wasLocked = Cursor.lockState == CursorLockMode.Locked;
            if (session.Connected) EnsureLockerRoom();   // R60
            bool captureMouse = ((playing && !session.IsSpectator) || reviewing) && !chatTyping && !pause && !showRules && !tutorialDone && Application.isFocused && !automated;
            if (!automated) { Cursor.lockState = captureMouse ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !captureMouse; }
            if (captureMouse && wasLocked)
                followCamera.Look(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"), Input.mouseScrollDelta.y);
            if (reviewing && captureMouse && wasLocked && !chatTyping)
                followCamera.MoveFree(DiverAxis(KeyCode.D, KeyCode.RightArrow, KeyCode.A, KeyCode.LeftArrow), DiverAxis(KeyCode.W, KeyCode.UpArrow, KeyCode.S, KeyCode.DownArrow), Time.unscaledDeltaTime,
                    (Input.GetKey(KeyCode.Space) ? 1 : 0) - (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.C) ? 1 : 0),
                    Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            if (playing && p != null && !automated && !session.IsSpectator)
            {
                bool control = captureMouse && wasLocked && p.alive && p.swallowedBy == 0;
                PingInput(p, control);   // R60: middle mouse, aliens only
                // R44: E is the alien's one key for eating and for drain work: a usable drain part under the
                // crosshair, or a drain job in hand (let go / back out), takes the press; otherwise it bites.
                bool drainKey = p.team == Team.Hider && (p.drainTask != DrainTask.None || drainTarget != null && drainTarget.usable);
                pendingAction |= control && session.State.time >= session.State.huntAt && (p.team == Team.Hider ? Input.GetKeyDown(KeyCode.E) && !drainKey : Input.GetMouseButtonDown(0));
                if (control && p.team == Team.Hider && Input.GetKeyDown(KeyCode.Q) && morphTarget != null)
                {
                    pendingMorph = true; pendingMorphTargetId = morphTarget.id; pendingMorphTargetPlayer = morphTarget.player;
                }
                pendingFeed |= control && p.team == Team.Seeker && Input.GetKeyDown(KeyCode.F);
                // R47: the ability key: F for aliens, R for the diver (R43's scan is gone).
                pendingAbility |= control && Input.GetKeyDown(p.team == Team.Seeker ? KeyCode.R : KeyCode.F);
                pendingEgg |= control && p.team == Team.Hider && Input.GetKeyDown(KeyCode.R);
                // R25: C drops the disguise.
                pendingUnmorph |= control && p.team == Team.Hider && Input.GetKeyDown(KeyCode.C);
                // R41: squeezing into a drain backs out. R73f: on C only (not E, not the swim keys).
                if (control && p.team == Team.Hider && p.drainTask == DrainTask.Enter && Input.GetKeyDown(KeyCode.C))
                { pendingInteract = true; pendingInteractDrain = pendingInteractPart = -1; }
                // R27: E at a drain (R44: the alien too, was F): start on the part under the crosshair, or let go.
                if (control && Input.GetKeyDown(KeyCode.E) && !(p.team == Team.Hider && p.drainTask == DrainTask.Enter) && (p.team == Team.Hider ? drainKey : drainTarget != null || p.drainTask != DrainTask.None))
                {
                    pendingInteract = true;
                    pendingInteractDrain = drainTarget != null ? drainTarget.drain : -1; pendingInteractPart = drainTarget != null ? drainTarget.part : -1;
                }
                inputTimer += Time.unscaledDeltaTime;
                if (session.IsHost || inputTimer >= 1f / 30f)
                {
                    inputTimer %= 1f / 30f;
                    var aim = followCamera.Aim(session.State, p);
                    var move = !control ? new Point() : followCamera.Movement(DiverAxis(KeyCode.D, KeyCode.RightArrow, KeyCode.A, KeyCode.LeftArrow), DiverAxis(KeyCode.W, KeyCode.UpArrow, KeyCode.S, KeyCode.DownArrow));
                    bool inLocker = AquariumLockerRoom.Waiting(p, session.State.time);   // R60: walking on the changing-room floor
                    if (control && inLocker) move = LockerMove();
                    if (control)
                    {
                        if (!inLocker) move.y += (Input.GetKey(KeyCode.Space) ? 1 : 0) - (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ? 1 : 0);
                        if (move.Length > 1) move = move.Normalized;
                    }
                    session.Input(new SwimInput
                    {
                        x = move.x, y = move.y, z = move.z,
                        aimX = aim.x, aimY = aim.y, aimZ = aim.z, slow = control && Input.GetKey(KeyCode.LeftAlt),
                        sprint = control && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)),
                        action = pendingAction && control, morph = pendingMorph && control, feed = pendingFeed && control,
                        morphTargetId = pendingMorphTargetId, morphTargetPlayer = pendingMorphTargetPlayer,
                        ability = pendingAbility && control, layEgg = pendingEgg && control, unmorph = pendingUnmorph && control,
                        interact = pendingInteract && control, interactDrain = pendingInteractDrain, interactPart = pendingInteractPart
                    });
                    pendingAction = pendingMorph = pendingFeed = pendingAbility = pendingEgg = pendingUnmorph = pendingInteract = false;
                    pendingMorphTargetId = 0; pendingInteractDrain = pendingInteractPart = -1;
                }
            }
            else { pendingAction = pendingMorph = pendingFeed = pendingAbility = pendingEgg = pendingUnmorph = pendingInteract = false; }
            using(sessionMarker.Auto())session.Update(Time.unscaledDeltaTime);
            UpdateAlienPreview();
            bool menuShown = UpdateMenuStage();   // R71: the chase on the reef behind the main menu
            if (!session.Connected)
            {
                if (!menuShown)
                {
                    backdrop.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
                    using(drawMarker.Auto())view.Draw(backdrop.Snapshot(), 0, Time.unscaledDeltaTime);
                }
                AquariumGalleryCamera.Murk = 0;
            }
            else
            {
                using(drawMarker.Auto())view.Draw(session.State, session.LocalId, Time.unscaledDeltaTime, session.PresentationTime);
                UpdateMurk();
                bool finished = session.State.phase == RoundPhase.Finished;
                if (finished && previousPhase != RoundPhase.Finished) { audioSource.Stop(); feedAudioSource.Stop(); }
                foreach (var fx in session.State.effects)
                {
                    if (fx.id <= lastEffect) continue;
                    lastEffect = fx.id; AudioClip sound;
                    if (finished || !sounds.TryGetValue(fx.kind, out sound)) continue;
                    if (fx.kind == "feed")
                    {
                        var listener = SoundListener();
                        if (listener == null) continue;
                        float gain = AquariumModel.FeedSoundGain(fx.position, listener.position);
                        if (gain > 0)
                        {
                            feedSoundPosition = fx.position;
                            feedAudioSource.volume = 0.38f * gain;
                            feedAudioSource.clip = sound; feedAudioSource.Play();
                        }
                    }
                    else if (DrainSound(fx.kind))
                    {
                        // R27: drain work is heard nearby only (the diver has to be close to notice). R47: up to 25 m.
                        var listener = SoundListener();
                        float gain = listener == null ? 0 : Mathf.Clamp01(1 - (fx.position - listener.position).Length / DrainHearing);
                        if (gain > 0) audioSource.PlayOneShot(sound, gain);
                    }
                    else audioSource.PlayOneShot(sound);
                }
                previousPhase = session.State.phase;
            }
            UpdateFeedSound();
            TrackR47Effects(); UpdateTutorial(); UpdateHungerWarning(); UpdateScrewSound(); UpdatePingArrows();   // R73h
        }

        private static bool DrainSound(string kind)
        { return kind == "screwOut" || kind == "lidOpen" || kind == "lidClosed" || kind == "squeezePop"; }

        private static bool SwimKeyDown()
        {
            foreach (var key in SwimKeys) if (Input.GetKeyDown(key)) return true;
            return false;
        }
        private static readonly KeyCode[] SwimKeys = { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.Space, KeyCode.LeftControl, KeyCode.RightControl };
        private static float DiverAxis(KeyCode positive, KeyCode positiveAlt, KeyCode negative, KeyCode negativeAlt)
        { return (Input.GetKey(positive) || Input.GetKey(positiveAlt) ? 1 : 0) - (Input.GetKey(negative) || Input.GetKey(negativeAlt) ? 1 : 0); }

        private void LateUpdate()
        {
            followCamera.Prepare(session.State, session.LocalId, session.SpectatingId);
            using(cameraMarker.Auto())followCamera.UpdatePose(session.State, view, Time.unscaledDeltaTime);
            view.PresentEquipment(session.State, followCamera.FollowId, followCamera.FirstPerson && !followCamera.Cinematic && !followCamera.DroneView);
            var local = Local();
            using(targetMarker.Auto())
            {
            morphTarget = !session.IsSpectator && session.State != null && session.State.phase == RoundPhase.Playing && local != null
                && local.alive && local.team == Team.Hider && local.swallowedBy == 0
                ? view.FindMorphTarget(session.State, local, followCamera.Aim(session.State, local)) : null;
            view.SetMorphTarget(morphTarget);
            drainTarget = !session.IsSpectator && session.State != null && session.State.phase == RoundPhase.Playing && local != null
                && local.alive && local.swallowedBy == 0 && local.drainTask != DrainTask.Enter && !local.droneControl ? view.FindDrainTarget(session.State, local) : null;
            // R29: another screw can be lit (and its progress read) while one is being turned.
            view.SetDrainTarget(drainTarget != null && drainTarget.usable && !SameDrainJob(local, drainTarget) ? drainTarget : null);
            // R29: the screws round an alien glow through its own body (and anything else) when it is close.
            view.ShowDrainRims(!session.IsSpectator && session.State != null && session.State.phase == RoundPhase.Playing && local != null
                && local.alive && local.team == Team.Hider && local.swallowedBy == 0 && !local.escaped ? local : null);
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && !automated && session != null && session.Connected && session.State.phase != RoundPhase.Lobby) pause = true;
        }

        // R29: a Subnautica-like HUD. Translucent deep-sea glass cards with a thin cyan rim, ring gauges,
        // round vitals in the lower left, a compass strip with the clock, rounded quick slots and keycaps.
        // Rounded shapes use IMGUI's own rounded-rect drawing (crisp at any scale); ring arcs come from
        // small textures generated once per size and 5-degree step.
        private GUIStyle caption, timerText, vital, vitalSmall, vitalLabel, promptText, keyInk, compassMain, compassMinor, slotName, slotDim, slotReady, alert, hint, centred, smallCentred, keyTiny, slotTitle, slotTitleDim, slotState, slotStateReady;
        private static readonly Color Glass = new Color(0.025f, 0.12f, 0.18f, 0.74f), GlassDeep = new Color(0.02f, 0.08f, 0.125f, 0.93f);
        private static readonly Color Rim = new Color(0.55f, 0.93f, 1f, 0.42f), RimGlow = new Color(0.45f, 0.88f, 1f, 0.08f);
        private static readonly Color Track = new Color(0.6f, 0.88f, 0.98f, 0.16f), Warm = new Color(1f, 0.76f, 0.3f), Danger = new Color(1f, 0.38f, 0.34f);
        private static readonly Color Line = new Color(0.55f, 0.93f, 1f, 0.2f), Disc = new Color(0.015f, 0.075f, 0.11f, 0.62f);
        private readonly Dictionary<int, Texture2D> arcs = new Dictionary<int, Texture2D>();
        private const int ArcSteps = 72;

        private GUIStyle Text(int size, Color color, FontStyle weight = FontStyle.Normal)
        { return new GUIStyle(GUI.skin.label) { font = font, fontSize = size, fontStyle = weight, wordWrap = true, richText = false, normal = { textColor = color } }; }
        private GUIStyle Line1(int size, Color color, FontStyle weight, TextAnchor anchor)
        { var s = Text(size, color, weight); s.wordWrap = false; s.alignment = anchor; s.clipping = TextClipping.Overflow; return s; }
        private void Styles()
        {
            if (title != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 24);
            title = Text(40, Color.white, FontStyle.Bold); heading = Text(22, Color.white, FontStyle.Bold);
            body = Text(16, Color.white); roomNameText = Text(15, Color.white); small = Text(13, muted); number = Text(30, cyan, FontStyle.Bold);
            keycap = Text(13, cyan, FontStyle.Bold); keycap.alignment = TextAnchor.MiddleCenter;
            caption = Line1(11, new Color(cyan.r, cyan.g, cyan.b, .85f), FontStyle.Bold, TextAnchor.UpperLeft);
            timerText = Line1(19, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            vital = Line1(18, Color.white, FontStyle.Bold, TextAnchor.UpperCenter);
            vitalSmall = Line1(13, Color.white, FontStyle.Bold, TextAnchor.UpperCenter);
            vitalLabel = Line1(11, muted, FontStyle.Bold, TextAnchor.UpperCenter);
            promptText = Line1(14, Color.white, FontStyle.Normal, TextAnchor.MiddleLeft);
            keyInk = Line1(12, new Color(0.02f, 0.1f, 0.15f), FontStyle.Bold, TextAnchor.MiddleCenter);
            compassMain = Line1(14, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            compassMinor = Line1(11, muted, FontStyle.Bold, TextAnchor.MiddleCenter);
            slotName = Line1(15, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            slotDim = Line1(15, muted, FontStyle.Bold, TextAnchor.MiddleLeft);
            slotReady = Line1(12, cyan, FontStyle.Bold, TextAnchor.MiddleLeft);
            alert = Line1(13, Warm, FontStyle.Bold, TextAnchor.UpperLeft);
            hint = Line1(13, new Color(muted.r, muted.g, muted.b, .9f), FontStyle.Normal, TextAnchor.MiddleCenter);
            centred = Line1(13, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            smallCentred = Line1(12, new Color(0.85f, 0.94f, 0.97f), FontStyle.Normal, TextAnchor.MiddleCenter);
            // R30: the compact square quick slots.
            keyTiny = Line1(10, new Color(0.02f, 0.1f, 0.15f), FontStyle.Bold, TextAnchor.MiddleCenter);
            slotTitle = Line1(13, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            slotTitleDim = Line1(13, muted, FontStyle.Bold, TextAnchor.MiddleCenter);
            slotState = Line1(10, muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            slotStateReady = Line1(10, cyan, FontStyle.Bold, TextAnchor.MiddleCenter);
            // Buttons and fields draw their own rounded glass underneath; the styles only carry the text.
            button = new GUIStyle { font = font, fontSize = 16, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(12, 12, 5, 5), richText = false, clipping = TextClipping.Clip };
            button.normal.textColor = button.hover.textColor = button.focused.textColor = Color.white; button.active.textColor = cyan;
            primary = new GUIStyle(button); primary.fontStyle = FontStyle.Bold;
            primary.normal.textColor = primary.hover.textColor = primary.focused.textColor = primary.active.textColor = ink;
            field = new GUIStyle { font = font, fontSize = 18, padding = new RectOffset(14, 14, 9, 8), alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, wordWrap = false };
            field.normal.textColor = field.hover.textColor = field.focused.textColor = field.active.textColor = Color.white;
        }
        private void Panel(Rect rect, Color color) { var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
        private void Label(float x, float y, float w, float h, string text, GUIStyle style) { GUI.Label(new Rect(x, y, w, h), text, style); }
        private void Separator(float x, float y, float width) { Shape(new Rect(x, y, width, 1), Line, 0); }
        private string TeamName(Team team) { return team == Team.Hider ? "외계인" : "잠수부 (술래)"; }

        // A filled rounded rectangle, or only its outline when border > 0.
        private static void Shape(Rect rect, Color color, float radius, float border = 0)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0 || rect.height <= 0 || color.a <= 0) return;
            radius = Mathf.Max(0, Mathf.Min(radius, Mathf.Min(rect.width, rect.height) / 2));
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, border, radius);
        }
        // The glass card: dark translucent fill, a faint inner glow and a thin bright rim.
        private void Card(Rect rect, bool deep = false, float radius = 12) { Card(rect, Rim, deep, radius); }
        private void Card(Rect rect, Color rim, bool deep = false, float radius = 12)
        {
            Shape(rect, deep ? GlassDeep : Glass, radius);
            Shape(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), RimGlow, radius - 1, 4);
            Shape(rect, rim, radius, 1.2f);
        }
        private void Pill(Rect rect, Color rim) { Shape(rect, Glass, rect.height / 2); Shape(rect, rim, rect.height / 2, 1.2f); }
        // A light keycap with the key in dark ink; returns its width.
        private float Key(float x, float y, string key)
        {
            float w = Mathf.Max(24, 12 + Measure(keyInk, key).x);
            var rect = new Rect(x, y, w, 22);
            Shape(rect, new Color(0.86f, 0.97f, 1f, 0.94f), 5);
            GUI.Label(rect, key, keyInk);
            return w;
        }
        private bool Button(float x, float y, float w, string text, bool main = false) { return Button(new Rect(x, y, w, 42), text, main); }
        private bool Button(Rect rect, string text, bool main = false)
        {
            bool hover = GUI.enabled && rect.Contains(Event.current.mousePosition);
            float r = Mathf.Min(10, rect.height / 2);
            var fill = main ? (hover ? new Color(0.68f, 1f, 0.97f) : new Color(cyan.r, cyan.g, cyan.b, 0.95f))
                : hover ? new Color(0.13f, 0.32f, 0.4f, 0.92f) : new Color(0.06f, 0.19f, 0.26f, 0.86f);
            if (!GUI.enabled) fill.a *= .45f;
            Shape(rect, fill, r);
            if (!main) Shape(rect, hover ? new Color(0.6f, 0.95f, 1f, 0.75f) : Rim, r, 1.2f);
            return GUI.Button(rect, text, main ? primary : button);
        }
        private void FieldBack(Rect rect)
        {
            bool hover = GUI.enabled && rect.Contains(Event.current.mousePosition);
            Shape(rect, new Color(0.01f, 0.06f, 0.1f, 0.85f), 9);
            Shape(rect, hover ? new Color(0.6f, 0.95f, 1f, 0.7f) : Rim, 9, 1.2f);
        }
        private string TextField(Rect rect, string value, int max) { FieldBack(rect); return GUI.TextField(rect, value, max, field); }

        // A ring gauge: a faint full track and the filled arc clockwise from twelve o'clock.
        private void Ring(float cx, float cy, float diameter, float thickness, float progress, Color color) { Ring(cx, cy, diameter, thickness, progress, color, Track); }
        private void Ring(float cx, float cy, float diameter, float thickness, float progress, Color color, Color track)
        {
            var rect = new Rect(cx - diameter / 2, cy - diameter / 2, diameter, diameter);
            Shape(rect, track, diameter / 2, thickness);
            Arc(rect, thickness, 0, progress, color);
        }
        // An arc of the ring from `from` (turns, clockwise from the top) over `length` turns.
        private void Arc(Rect rect, float thickness, float from, float length, Color color)
        {
            int step = Mathf.RoundToInt(Mathf.Clamp01(length) * ArcSteps);
            if (step <= 0 || Event.current.type != EventType.Repaint) return;
            if (step >= ArcSteps) { Shape(rect, color, rect.width / 2, thickness); return; }
            var texture = ArcTexture(rect.width, thickness, step);
            var matrix = GUI.matrix;
            if (Mathf.Abs(from) > .0001f)
            {
                var pivot = new Vector3(rect.center.x, rect.center.y, 0);
                GUI.matrix = matrix * Matrix4x4.TRS(pivot, Quaternion.Euler(0, 0, from * 360), Vector3.one) * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
            }
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true, 0, color, 0, 0);
            GUI.matrix = matrix;
        }
        private Texture2D ArcTexture(float diameter, float thickness, int step)
        {
            int key = (Mathf.RoundToInt(diameter) * 64 + Mathf.Clamp(Mathf.RoundToInt(thickness * 4), 0, 63)) * 128 + step;
            Texture2D texture;
            if (arcs.TryGetValue(key, out texture) && texture != null) return texture;
            int size = Mathf.Clamp(Mathf.CeilToInt(diameter * 2), 8, 256); float k = size / diameter;
            float outer = size / 2f, inner = outer - thickness * k, limit = step / (float)ArcSteps * Mathf.PI * 2;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Texture rows run bottom-up; sy grows downwards like the screen.
                    float sx = x + .5f - outer, sy = size - y - .5f - outer, r = Mathf.Sqrt(sx * sx + sy * sy);
                    float cover = Mathf.Clamp01(outer - r) * Mathf.Clamp01(r - inner);
                    if (cover > 0)
                    {
                        float a = Mathf.Atan2(sx, -sy); if (a < 0) a += Mathf.PI * 2;
                        cover *= Mathf.Clamp01((limit - a) * r + .5f) * Mathf.Clamp01(a * r + .5f);
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(cover * 255));
                }
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels32(pixels); texture.Apply(false, true);
            arcs[key] = texture; return texture;
        }
        private void Meter(float x, float y, float width, float ratio, Color color, float height = 6)
        {
            Shape(new Rect(x, y, width, height), Track, height / 2);
            float w = width * Mathf.Clamp01(ratio);
            if (w > .5f) Shape(new Rect(x, y, Mathf.Max(height, w), height), color, height / 2);
        }
        // An interaction prompt under the reticle: [key] text in a glass pill.
        private void Prompt(float cx, float y, string key, string text, Color rim)
        {
            float textW = Measure(promptText, text).x;
            float keyW = key.Length > 0 ? Mathf.Max(24, 12 + Measure(keyInk, key).x) + 8 : 0;
            float w = 28 + keyW + textW; var rect = new Rect(cx - w / 2, y, w, 30);
            Pill(rect, rim);
            float x = rect.x + 14;
            if (key.Length > 0) { Key(x, y + 4, key); x += keyW; }
            GUI.Label(new Rect(x, y, textW + 4, 30), text, promptText);
        }
        private void Prompt(float cx, float y, string key, string text) { Prompt(cx, y, key, text, Rim); }
        // R49: the one interaction prompt style under the reticle: a keyboard keycap (light top face on a darker
        // base, so it reads as a physical key) followed by a short action text on a soft shadow.
        private const float PromptStep = 38;
        private void KeyPrompt(float cx, float y, string key, string text, Color accent)
        {
            float textW = Measure(promptText, text).x;
            float capW = Mathf.Max(30, 16 + Measure(keyInk, key).x), capH = 30;
            float w = capW + 10 + textW, x = cx - w / 2;
            Shape(new Rect(x + capW + 2, y + 3, textW + 16, capH - 6), new Color(0, 0.03f, 0.06f, .55f), (capH - 6) / 2);
            Shape(new Rect(x, y + 3, capW, capH - 1), new Color(0.32f, 0.42f, 0.48f, .95f), 7);
            var top = new Rect(x, y, capW, capH - 4);
            Shape(top, new Color(0.9f, 0.97f, 1f, 0.97f), 7);
            Shape(top, new Color(accent.r, accent.g, accent.b, .9f), 7, 1.4f);
            GUI.Label(top, key, keyInk);
            GUI.Label(new Rect(x + capW + 10, y, textW + 6, capH - 2), text, promptText);
        }
        private void KeyPrompt(float cx, float y, string key, string text) { KeyPrompt(cx, y, key, text, cyan); }
        private void Reticle(float mx, float my)
        {
            Shape(new Rect(mx - 8, my - 8, 16, 16), new Color(0.85f, 0.98f, 1f, 0.85f), 8, 1.5f);
            Shape(new Rect(mx - 1.5f, my - 1.5f, 3, 3), new Color(0.85f, 0.98f, 1f, 0.95f), 1.5f);
        }

        private void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool captureHud = smoke != null && smoke.BeginHudCapture();
#endif
            Styles(); float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            if (showRules || roomDialog != RoomDialog.None) GUI.enabled = false;
            if (!session.Connected) MainMenu(width, height);   // R71 (the old boxed Menu is no longer drawn)
            else if (session.State.phase == RoundPhase.Lobby) Lobby(width, height);
            else
            {
                PlayHUD(width, height);
                // R60: pings (aliens), the changing-room hint and the cut to the deck; chat after the round
                var hudState = session.State; var hudLocal = Local();
                if (hudState.phase == RoundPhase.Playing && hudLocal != null) { PingMarkers(hudState, hudLocal, width, height); LockerHint(hudState, hudLocal, width); }
                if (hudState.phase == RoundPhase.Finished) FinishedChat(width, height, hudState.time < hudState.reviewEndsAt);
                LockerFade(width, height);
            }
            if (!session.CanChat && chatTyping) { chatTyping = false; GUI.FocusControl(null); }
            if (pause && session.Connected && session.State.phase == RoundPhase.Lobby) pause = false;   // R73: the lobby has no pause menu
            if (pause && session.Connected)
            {
                Panel(new Rect(0, 0, width, height), new Color(0, 0.03f, 0.06f, 0.6f));
                float x = width / 2 - 200, y = height / 2 - 175; Card(new Rect(x, y, 400, 350), true, 16);
                Label(x + 24, y + 22, 352, 35, "수조 메뉴", heading);
                Shape(new Rect(x + 24, y + 58, 40, 3), cyan, 1.5f);
                Label(x + 24, y + 66, 352, 30, "경기는 계속 진행됩니다.", small);
                if (Button(x + 24, y + 104, 352, "계속 헤엄치기", true)) pause = false;
                if (Button(x + 24, y + 162, 352, "조작과 규칙")) showRules = true;
                // R66: the picture option (AquariumGameR66.cs): full quality, or a faster picture for more frames.
                if (Button(x + 24, y + 220, 352, fastGraphics ? "그래픽: 빠르게 (프레임 우선)" : "그래픽: 최고 품질")) SetFastGraphics(!fastGraphics);
                if (Button(x + 24, y + 278, 352, session.IsHost && !session.Practice ? "방 종료하고 나가기" : "나가기")) Leave();
            }
            GUI.enabled = true;
            if (showRules) Rules(width, height);
            if (roomDialog != RoomDialog.None && !session.Connected) DrawRoomDialog(width, height);
            GUI.matrix = Matrix4x4.identity;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (captureHud) smoke.EndHudCapture();
#endif
        }
        private void Menu(float width, float height)
        {
            float x = 48, y = (height - 650) / 2;
            bool usable = !session.Busy && !roomSubmitting && !showRules && roomDialog == RoomDialog.None;
            Card(new Rect(x, y, 472, 650), true, 18);
            Label(x + 30, y + 22, 410, 24, "AQUARIUM  /  HIDE & SEEK", caption);
            Label(x + 30, y + 52, 410, 58, "수족관 술래잡기", title);
            Shape(new Rect(x + 32, y + 112, 56, 3), cyan, 1.5f);
            Label(x + 30, y + 125, 400, 54, "외계인 4명, 잠수부 1명.\n물고기 사이에 숨어 살아남으세요.", body);
            Separator(x + 30, y + 192, 412);
            Label(x + 30, y + 210, 100, 25, "플레이어 이름", small);
            GUI.enabled = usable;
            playerName = TextField(new Rect(x + 30, y + 240, 412, 42), playerName, 16);
            if (Button(x + 30, y + 300, 199, (selectedTeam == Team.Hider ? "✓ " : "") + TeamName(Team.Hider), selectedTeam == Team.Hider)) selectedTeam = Team.Hider;
            if (Button(x + 243, y + 300, 199, (selectedTeam == Team.Seeker ? "✓ " : "") + TeamName(Team.Seeker), selectedTeam == Team.Seeker)) selectedTeam = Team.Seeker;
            if (Button(x + 30, y + 358, 412, "AI 연습하기", true))
            { SaveName(); lastEffect = 0; session.OpenPracticeLobby(playerName, selectedTeam, selectedAlien); }
            if (Button(x + 30, y + 412, 412, "온라인 방 만들기")) OpenRoomDialog(RoomDialog.Create);
            joinCode = TextField(new Rect(x + 30, y + 468, 253, 42), joinCode, 12).ToUpperInvariant();
            if (joinCode.Length == 0) Label(x + 44, y + 479, 219, 25, "친구의 방 코드 입력", small);
            GUI.enabled = usable && joinCode.Trim().Length >= 4;
            if (Button(x + 297, y + 468, 145, "코드로 참가")) OpenRoomDialog(RoomDialog.CodeJoin);
            // R70b: the three tutorials start from the main menu (they were in the practice waiting room).
            GUI.enabled = usable;
            TutorialButtons(x + 30, y + 522, 412);
            GUI.enabled = !showRules && roomDialog == RoomDialog.None;
            string status = string.IsNullOrEmpty(roomDialogError) ? session.Status : roomDialogError;
            Label(x + 30, y + 578, 412, 32, string.IsNullOrEmpty(status)
                ? "온라인 2–5인, 빈자리는 AI로 채우거나 비워 둘 수 있습니다." : status, small);
            if (session.Busy && Button(new Rect(x + 318, y + 612, 124, 28), "연결 취소")) CloseRoomDialog(true);
            else if (!session.Busy && Button(new Rect(x + 30, y + 612, 412, 28), "조작과 규칙")) showRules = true;
            DrawRoomList(548, y, width - 596, 650, usable);
        }

        private void DrawRoomList(float x, float y, float width, float height, bool usable)
        {
            Card(new Rect(x, y, width, height), true, 18);
            Label(x + 24, y + 22, width - 175, 35, "온라인 방 목록", heading);
            GUI.enabled = usable && !session.RoomsBusy;
            if (Button(x + width - 144, y + 20, 120, session.RoomsBusy ? "조회 중…" : "새로고침")) RefreshRoomList();
            GUI.enabled = !showRules && roomDialog == RoomDialog.None;
            Label(x + 24, y + 72, width - 48, 42, "대기 중인 방, 5초마다 갱신", small);
            Separator(x + 24, y + 125, width - 48);
            float nameWidth = width - 430;
            Label(x + 24, y + 139, nameWidth, 25, "방 이름", caption);
            Label(x + width - 394, y + 139, 54, 25, "인원", caption);
            Label(x + width - 322, y + 139, 72, 25, "상태", caption);
            Label(x + width - 238, y + 139, 82, 25, "입장 조건", caption);
            int count = 0;
            if (session.Rooms != null)
                for (int i = 0; i < session.Rooms.Count; i++)
                    if (session.Rooms[i] != null && session.Rooms[i].phase == "Waiting") count++;
            if (count == 0)
            {
                Label(x + 34, y + 278, width - 68, 80,
                    session.RoomsBusy ? "방을 찾는 중…" : "대기 중인 방이 없습니다.", body);
            }
            else
            {
                var viewport = new Rect(x + 12, y + 174, width - 24, 390);
                roomScroll = GUI.BeginScrollView(viewport, roomScroll, new Rect(0, 0, width - 42, Mathf.Max(390, count * 64)), false, false);
                int row = 0;
                for (int i = 0; i < session.Rooms.Count; i++)
                {
                    var room = session.Rooms[i];
                    if (room == null || room.phase != "Waiting") continue;
                    int visibleRow = row++; float ry = visibleRow * 64;
                    Shape(new Rect(0, ry, width - 42, 59), visibleRow % 2 == 0 ? new Color(0.08f, 0.22f, 0.29f, 0.55f) : new Color(0.06f, 0.17f, 0.23f, 0.45f), 10);
                    Label(12, ry + 9, nameWidth - 8, 45, room.name, roomNameText);
                    Label(width - 406, ry + 17, 62, 29, room.players + " / " + room.maxPlayers, body);
                    Label(width - 334, ry + 17, 78, 29, "대기 중", small);
                    Label(width - 250, ry + 17, 82, 29, room.hasPassword ? "비밀번호" : "없음", small);
                    GUI.enabled = usable && room.CanJoin;
                    string joinLabel = !room.CanJoin ? (room.players >= room.maxPlayers ? "정원 마감" : "입장 불가") : "참가";
                    if (Button(width - 156, ry + 8, 110, joinLabel))
                    {
                        selectedRoomId = room.id; selectedRoomName = room.name;
                        if (room.hasPassword) OpenRoomDialog(RoomDialog.RoomJoin);
                        else _ = ConnectRoom(RoomDialog.RoomJoin, room.id, "", "");
                    }
                    GUI.enabled = !showRules && roomDialog == RoomDialog.None;
                }
                GUI.EndScrollView();
            }
            Separator(x + 24, y + 580, width - 48);
            Label(x + 24, y + 594, width - 48, 48, session.RoomsBusy ? "목록을 새로 불러오는 중입니다…"
                : string.IsNullOrEmpty(session.RoomsStatus) ? "진행 중인 방은 코드로 관전할 수 있습니다." : session.RoomsStatus, small);
        }
        private void RefreshRoomList()
        {
            roomsRequested = true; nextRoomsRefreshAt = Time.unscaledTime + 5f;
            _ = session.RefreshRooms();
        }

        private void UpdateRoomMenuState()
        {
            if (!session.Connected && wasConnected) roomsRequested = false;
            wasConnected = session.Connected;
            if (!automated && !session.Connected && !session.Busy && !session.RoomsBusy && !roomSubmitting
                && !roomRequestPending && roomDialog == RoomDialog.None && !showRules && menuPanel == MenuPanel.Find
                && (!roomsRequested || Time.unscaledTime >= nextRoomsRefreshAt)) RefreshRoomList();
            if (!roomRequestPending) return;
            if (session.Connected) { CloseRoomDialog(false); return; }
            if (!roomSubmitting && !session.Busy)
            {
                roomRequestPending = false;
                if (string.IsNullOrEmpty(roomDialogError)) roomDialogError = string.IsNullOrEmpty(session.Status) ? "접속하지 못했습니다. 다시 시도해 주세요." : session.Status;
            }
        }
        private void OpenRoomDialog(RoomDialog kind)
        {
            roomDialog = kind; roomPassword = roomDialogError = ""; roomRequestPending = false;
            if (kind == RoomDialog.Create) roomNameDraft = AquariumModel.CleanName(playerName) + "의 수족관";
            GUI.FocusControl(null);
        }
        private void CloseRoomDialog(bool cancelConnection)
        {
            roomRequestVersion++;
            if (cancelConnection && (session.Busy || roomSubmitting)) session.Leave();
            roomDialog = RoomDialog.None; roomPassword = roomDialogError = ""; roomSubmitting = roomRequestPending = false;
            GUI.FocusControl(null);
        }
        private void DrawRoomDialog(float width, float height)
        {
            GUI.enabled = true;
            Panel(new Rect(0, 0, width, height), new Color(0, 0.025f, 0.04f, 0.78f));
            float x = width / 2 - 240, y = height / 2 - 200; Card(new Rect(x, y, 480, 400), true, 16);
            bool creating = roomDialog == RoomDialog.Create, codeJoining = roomDialog == RoomDialog.CodeJoin;
            Label(x + 24, y + 20, 432, 36, creating ? "온라인 방 만들기" : codeJoining ? "코드로 참가" : "비밀번호 입력", heading);
            Label(x + 24, y + 64, 432, 44, creating ? "비밀번호를 비우면 공개 방으로 만듭니다."
                : codeJoining ? "경기 중에는 관전으로 입장합니다."
                : "방 비밀번호를 입력하세요.", small);
            Label(x + 24, y + 114, 432, 24, creating ? "방 이름 (1–32자)" : codeJoining ? "친구의 방 코드" : "참가할 방", small);
            bool busy = session.Busy || roomSubmitting;
            GUI.enabled = !busy;
            if (creating) roomNameDraft = TextField(new Rect(x + 24, y + 142, 432, 42), roomNameDraft, 32);
            else if (codeJoining) joinCode = TextField(new Rect(x + 24, y + 142, 432, 42), joinCode, 12).ToUpperInvariant();
            else Label(x + 24, y + 142, 432, 42, selectedRoomName, body);
            Label(x + 24, y + 198, 432, 23, creating || codeJoining ? "비밀번호 (선택)" : "방 비밀번호", small);
            var passwordRect = new Rect(x + 24, y + 226, 432, 42); FieldBack(passwordRect);
            roomPassword = GUI.PasswordField(passwordRect, roomPassword, '●', 64, field);
            string hint = creating ? "비밀번호를 설정하려면 8–64자를 입력하세요." : codeJoining ? "비밀번호가 없는 방은 비워 두세요." : "방장이 정한 비밀번호를 입력해 주세요.";
            Label(x + 24, y + 282, 432, 52, busy ? (string.IsNullOrEmpty(session.Status) ? "연결을 준비하고 있습니다…" : session.Status)
                : string.IsNullOrEmpty(roomDialogError) ? hint : roomDialogError, small);
            GUI.enabled = true;
            if (Button(x + 24, y + 342, 208, busy ? "연결 취소" : "취소")) CloseRoomDialog(true);
            bool validPassword = roomPassword.Length == 0 || roomPassword.Length >= 8;
            bool valid = creating ? roomNameDraft.Trim().Length > 0 && validPassword
                : codeJoining ? joinCode.Trim().Length >= 4 && validPassword : roomPassword.Length >= 8;
            GUI.enabled = !busy && valid;
            if (Button(x + 248, y + 342, 208, creating ? "방 만들기" : "참가하기", true))
                _ = ConnectRoom(roomDialog, selectedRoomId, roomNameDraft.Trim(), roomPassword);
            GUI.enabled = true;
        }
        private async System.Threading.Tasks.Task ConnectRoom(RoomDialog kind, string lobbyId, string roomName, string password)
        {
            SaveName(); lastEffect = 0; notice = roomDialogError = ""; session.PreferredAlien = selectedAlien; session.PreferredDiver = selectedDiver;
            int request = ++roomRequestVersion; roomSubmitting = roomRequestPending = true;
            try
            {
                if (kind == RoomDialog.Create) await session.CreateRoom(roomName, password, playerName, Team.Hider);   // R72: online you ask to be 술래 in the room
                else if (kind == RoomDialog.RoomJoin) await session.JoinRoom(lobbyId, password, playerName, Team.Hider);
                else await session.OpenInternet(false, joinCode, playerName, Team.Hider, password);
            }
            catch (Exception)
            { if (request == roomRequestVersion) roomDialogError = "접속하지 못했습니다. 인터넷 연결을 확인하고 다시 시도해 주세요."; }
            finally { if (request == roomRequestVersion) roomSubmitting = false; }
        }
        private void SaveName() { playerName = AquariumModel.CleanName(playerName); PlayerPrefs.SetString("Aquarium.Name", playerName); PlayerPrefs.Save(); }
        private void Leave() { session.Leave(); CloseRoomDialog(false); pause = false; showRules = false; notice = ""; lastEffect = 0; roomsRequested = false; tutorial = 0; tutorialDone = false; }
        // 2026-10-28 lobby: the room and its players on the left, the alien pick on the right (a live 3D row of
        // every alien, cards, and the picked one's ability slot). AI practice opens the same lobby: the AI fills
        // the other seats, and the start button begins the round.
        // R70b: every player row has a portrait in a team-coloured frame (diver orange, alien green); the next
        // game's map is a large card under the list (where the tutorials were; they moved to the main menu);
        // the fish guide button sits where the role switch was (the switch is a tab on the right panel now).
        private void Lobby(float width, float height)
        {
            const float total = 1184, leftWidth = 448, panelHeight = 640;
            float x = width / 2 - total / 2, y = height / 2 - panelHeight / 2;
            bool practice = session.Practice;
            var local = session.IsSpectator ? null : Local();
            Card(new Rect(x, y, leftWidth, panelHeight), true, 18);
            if (practice)
            {
                Label(x + 28, y + 18, leftWidth - 56, 34, "AI 연습 대기실", heading);
                Label(x + 28, y + 54, leftWidth - 56, 23, "AI가 다른 자리를 채웁니다. 역할과 캐릭터, 맵을 고르고 시작하세요.", small);
            }
            else
            {
                Label(x + 28, y + 18, leftWidth - 300, 34, string.IsNullOrEmpty(session.RoomName) ? "입수 준비" : session.RoomName, string.IsNullOrEmpty(session.RoomName) ? heading : body);
                FillAISwitch(new Rect(x + leftWidth - 212, y + 74, 188, 26));   // R73
                Label(x + 28, y + 76, leftWidth - 250, 23, (session.HasPassword ? "비밀번호 방" : "공개 방") + ",  플레이어 " + session.HumanCount + "명,  관전자 " + session.SpectatorCount + "명", small);
                Label(x + leftWidth - 252, y + 12, 160, 18, "친구에게 보낼 방 코드", caption);
                Shape(new Rect(x + leftWidth - 256, y + 30, 166, 40), new Color(0.01f, 0.06f, 0.1f, 0.7f), 10);
                Label(x + leftWidth - 246, y + 30, 156, 40, session.Code, number);
                if (Button(new Rect(x + leftWidth - 82, y + 32, 58, 36), "복사")) { GUIUtility.systemCopyBuffer = session.Code; notice = "방 코드를 복사했습니다."; }
            }
            float listTop = practice ? y + 88 : y + 104;
            Separator(x + 28, listTop, leftWidth - 56);
            Label(x + 28, listTop + 10, 150, 22, "참가자", caption);
            Label(x + 184, listTop + 10, 150, 22, "역할 / 캐릭터", caption);
            Label(x + leftWidth - 100, listTop + 10, 80, 22, "상태", caption);
            int row = 0; float rowH = practice ? 46 : 40, first = listTop + 32;
            foreach (var p in session.State.players)
            {
                float ry = first + row++ * rowH;
                var team = TeamColor(p.team);
                var back = new Rect(x + 20, ry, leftWidth - 40, rowH - 4);
                Shape(back, p.id == session.LocalId ? new Color(0.12f, 0.33f, 0.4f, 0.6f) : row % 2 == 1 ? new Color(0.08f, 0.22f, 0.29f, 0.5f) : new Color(0.06f, 0.17f, 0.23f, 0.4f), 9);
                Shape(new Rect(back.x, back.y + 5, 3, back.height - 10), team, 1.5f);   // team stripe
                string who = p.name + (p.id == session.LocalId ? "  (나)" : "") + (!practice && p.id == 1 ? "  방장" : "") + (p.bot ? "  AI" : "");
                Label(x + 32, ry + (rowH - 4) / 2 - 14, 146, 28, who, roomNameText);
                float size = rowH - 8;
                PortraitFrame(new Rect(x + 184, ry, size, size), p);
                float tx = x + 184 + size + 8;
                string kindName; Color kindColor;
                if (p.team == Team.Hider) { var kind = AquariumAlienKinds.Get(p.alienKind); kindName = kind.name; kindColor = new Color(kind.r, kind.g, kind.b); }
                else { var diver = AquariumDiverKinds.Get(p.diverKind); kindName = diver.name; kindColor = new Color(diver.r, diver.g, diver.b); }
                Label(tx, ry + (rowH - 4) / 2 - 19, 130, 20, kindName, Tinted(small, Color.Lerp(kindColor, Color.white, .25f)));
                Label(tx, ry + (rowH - 4) / 2 + 2, 130, 16, p.team == Team.Seeker ? (practice ? "잠수부" : "술래 희망") : "외계인", Tinted(caption, team));
                Shape(new Rect(x + leftWidth - 100, ry + (rowH - 4) / 2 - 4, 8, 8), p.ready ? cyan : new Color(muted.r, muted.g, muted.b, .5f), 4);
                Label(x + leftWidth - 86, ry + (rowH - 4) / 2 - 13, 72, 26, p.ready ? "준비 완료" : "준비 중", small);
            }
            float controls = y + panelHeight - 100;
            float listEnd = first + row * rowH + 4;
            // R70b: the map card under the list (large in practice; online it shares the space with the chat).
            float mapHeight = practice ? Mathf.Max(96, controls - 10 - listEnd) : 74;
            LobbyMapCard(new Rect(x + 20, listEnd, leftWidth - 40, mapHeight));
            // R60: the room chat fills the space between the map and the buttons (the controls are in the rules).
            if (!practice) LobbyChat(x + 20, listEnd + mapHeight + 8, leftWidth - 40, controls - 8);
            // R70b: the fish guide where the role switch was; the switch is on the right panel (CharacterPanel).
            if (Button(x + 28, controls, 190, lobbyTab == 1 ? "✓ 어종 도감" : "어종 도감", lobbyTab == 1)) lobbyTab = lobbyTab == 1 ? 0 : 1;
            if (local != null)
            {
                if (session.IsHost)
                { if (Button(x + 230, controls, 190, "게임 시작", true)) { lastEffect = 0; notice = session.StartRound(); } }
                else if (Button(x + 230, controls, 190, local.ready ? "준비 취소" : "준비 완료", !local.ready)) session.Role(local.team, !local.ready);
            }
            if (Button(x + 28, controls + 50, 190, "방 나가기")) Leave();
            if (Button(x + 230, controls + 50, 190, "조작과 규칙")) showRules = true;
            CharacterPanel(x + leftWidth + 16, y, total - leftWidth - 16, panelHeight, local);
            // The notice (or the next step) in the pill under the panels; tips while there is nothing to say.
            string lobbyNotice = !string.IsNullOrEmpty(notice) ? notice : session.Status;
            if (string.IsNullOrEmpty(lobbyNotice) && !practice)
                lobbyNotice = session.IsHost ? "2명 이상, 전원 준비 후 시작. 술래는 희망자 중 무작위 (없으면 전원 중)" : "전원 준비 후 시작. 술래는 희망자 중 무작위 (없으면 전원 중)";
            if (string.IsNullOrEmpty(lobbyNotice)) TipPill(width / 2, y + panelHeight + 10, total);
            else NoticePill(width / 2, y + panelHeight + 10, total, lobbyNotice);
        }
        private void NoticePill(float cx, float y, float maxWidth, string text)
        {
            float w = Mathf.Min(maxWidth, Measure(promptText, text).x + 40);
            var rect = new Rect(cx - w / 2, y, w, 30);
            Pill(rect, new Color(cyan.r, cyan.g, cyan.b, .6f));
            GUI.Label(new Rect(rect.x + 20, y, w - 30, 30), text, promptText);
        }

        private void AlienSelect(float x, float y, float width, float height, Swimmer local)
        {
            var picked = AquariumAlienKinds.Get(selectedAlien);
            var pickedColor = new Color(picked.r, picked.g, picked.b);
            Card(new Rect(x, y, width, height), new Color(picked.r, picked.g, picked.b, .55f), true, 18);
            Label(x + 24, y + 18, 300, 34, "외계인 선택", heading);
            Label(x + 24, y + 54, width - 48, 23, local == null ? "관전 중에는 고를 수 없습니다."
                : local.team == Team.Seeker ? "지금은 잠수부로 참가합니다, 외계인으로 바꾸면 고른 캐릭터로 플레이해요."
                : "캐릭터를 눌러 고르세요, 다른 사람과 같은 외계인도 고를 수 있어요.", small);
            float stageWidth = width - 48, stageHeight = stageWidth / AquariumAlienPreview.Aspect;
            var stage = new Rect(x + 24, y + 86, stageWidth, stageHeight);
            if (alienPreview != null && alienPreview.Texture != null) GUI.DrawTexture(stage, alienPreview.Texture, ScaleMode.StretchToFill, false);
            else Shape(stage, new Color(0.06f, 0.15f, 0.21f, .8f), 6);
            Shape(stage, Rim, 6, 1.2f);
            bool canPick = local != null && GUI.enabled;
            var e = Event.current;
            if (canPick && alienPreview != null && e.type == EventType.MouseDown && e.button == 0 && stage.Contains(e.mousePosition))
            {
                int slot = alienPreview.SlotAt((e.mousePosition.x - stage.x) / stage.width);
                if (slot >= 0) { PickAlien(AquariumAlienKinds.LobbyOrder[slot]); e.Use(); }
            }
            // One card per alien under the stage, in the same order.
            int count = AquariumAlienKinds.LobbyOrder.Length; const float gap = 8;
            float cardWidth = (stageWidth - gap * (count - 1)) / count, cardY = stage.yMax + 12;
            for (int slot = 0; slot < count; slot++)
            {
                var kind = AquariumAlienKinds.Get(AquariumAlienKinds.LobbyOrder[slot]);
                var color = new Color(kind.r, kind.g, kind.b);
                var card = new Rect(stage.x + slot * (cardWidth + gap), cardY, cardWidth, 50);
                bool chosen = kind.id == selectedAlien;
                bool hover = canPick && card.Contains(e.mousePosition);
                Shape(card, chosen ? new Color(0.12f, 0.3f, 0.38f, 0.85f) : hover ? new Color(0.1f, 0.25f, 0.32f, 0.8f) : new Color(0.06f, 0.17f, 0.23f, 0.7f), 10);
                Shape(card, chosen ? color : new Color(color.r, color.g, color.b, .45f), 10, chosen ? 2f : 1.2f);
                Label(card.x + 12, card.y + 13, card.width - 18, 28, (chosen ? "✓ " : "") + kind.name, chosen ? Tinted(body, color) : body);
                int others = OthersWithAlien(kind.id);   // R73: duplicates are allowed, the card counts the others
                if (others > 0) { var tag = new Rect(card.xMax - 40, card.y + 4, 36, 18); Shape(tag, new Color(color.r, color.g, color.b, .35f), 9); GUI.Label(tag, "+" + others, Tinted(smallCentred, Color.white)); }
                if (canPick && GUI.Button(card, GUIContent.none, GUIStyle.none)) PickAlien(kind.id);
            }
            // The picked alien's abilities. R70b: no name block (the card above says it); the F ability and the
            // passive get one box each across the full width (the green alien's single passive takes it all).
            float infoY = cardY + 66, boxH = height - (infoY - y) - 24;
            var parts = AlienAbilityParts(picked);
            var rim = new Color(picked.r, picked.g, picked.b, .6f);
            if (parts[0].Length == 0) AbilityBox(new Rect(x + 24, infoY, stageWidth, boxH), rim, parts[2], parts[3], pickedColor);
            else
            {
                float half = (stageWidth - 12) / 2;
                AbilityBox(new Rect(x + 24, infoY, half, boxH), rim, parts[0], parts[1], pickedColor);
                AbilityBox(new Rect(x + 36 + half, infoY, half, boxH), Rim, parts[2], parts[3], Color.white);
            }
        }

        private void PickAlien(int kind)
        {
            kind = AquariumAlienKinds.Clamp(kind);
            if (kind == selectedAlien) return;
            selectedAlien = kind; PlayerPrefs.SetInt("Aquarium.Alien", kind); PlayerPrefs.Save();
            session.SetAlien(kind);
        }

        private GUIStyle Tinted(GUIStyle style, Color color)
        {
            if (tintedStyles.TryGetValue((style, color), out var cached)) return cached;
            var tinted = new GUIStyle(style); tinted.normal.textColor = color; tintedStyles[(style, color)] = tinted; return tinted;
        }

        private float hudWidth = 1280, hudHeight = 720;
        // R43: an alien hit by a harpoon clouds the diver's water with its dark green blood for a moment:
        // full murk within MurkNear of the hit, none past MurkFar (the shooter is always within harpoon range).
        private const float MurkNear = 6f, MurkFar = 13f, MurkRise = .15f, MurkHold = .7f, MurkSeconds = 2.8f;
        private readonly HashSet<int> murkSeen = new HashSet<int>();
        private float murkAt = -100, murkPeak;
        private Texture2D murkTexture;
        private float MurkLeft()
        {
            float t = Time.unscaledTime - murkAt;
            float envelope = t < 0 || t >= MurkSeconds ? 0 : t < MurkRise ? t / MurkRise : t < MurkHold ? 1 : 1 - Mathf.SmoothStep(0, 1, (t - MurkHold) / (MurkSeconds - MurkHold));
            return murkPeak * envelope;
        }
        private void UpdateMurk()
        {
            var state = session.State; var p = Local();
            bool playing = session.Connected && state != null && state.phase == RoundPhase.Playing;
            if (!playing) { murkSeen.Clear(); murkPeak = 0; AquariumGalleryCamera.Murk = 0; return; }
            // R49: harpoon hits no longer cloud the diver's view; only swimming through alien ink does
            // (AquariumGalleryCamera.InkBombMurk, set by the view).
            AquariumGalleryCamera.Murk = 0;
        }
        // Soft, tileable green cloud puffs (value noise on a wrapping lattice) drawn over the view.
        private Texture2D MurkTexture()
        {
            if (murkTexture != null) return murkTexture;
            const int size = 128;
            murkTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float n = 0, amplitude = .55f;
                for (int octave = 0, period = 4; octave < 4; octave++, period *= 2, amplitude *= .5f)
                    n += amplitude * LatticeNoise(x * period / (float)size, y * period / (float)size, period);
                float alpha = Mathf.Clamp01((n - .28f) * 1.9f);
                float light = .75f + .5f * n;
                var c = AquariumGalleryCamera.MurkColor;
                pixels[y * size + x] = new Color(Mathf.Clamp01(c.r * light * 1.6f), Mathf.Clamp01(c.g * light * 1.6f), Mathf.Clamp01(c.b * light * 1.6f), alpha);
            }
            murkTexture.SetPixels32(pixels); murkTexture.Apply();
            return murkTexture;
        }
        private static float LatticeNoise(float x, float y, int period)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(x0, y0, period), b = Hash(x0 + 1, y0, period), c = Hash(x0, y0 + 1, period), d = Hash(x0 + 1, y0 + 1, period);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
        private static float Hash(int x, int y, int period)
        {
            x = ((x % period) + period) % period; y = ((y % period) + period) % period;
            uint h = (uint)(x * 374761393 + y * 668265263 + period * 2246822519u);
            h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
            return (h & 0xffff) / 65535f;
        }
        private void MurkOverlay(float width, float height)
        {
            // R47: the purple alien's ink bomb clouds the view too.
            float murk = Mathf.Max(AquariumGalleryCamera.Murk, AquariumGalleryCamera.InkBombMurk);
            if (murk <= .005f) return;
            var c = AquariumGalleryCamera.MurkColor;
            Panel(new Rect(0, 0, width, height), new Color(c.r, c.g, c.b, .58f * murk));
            var texture = MurkTexture(); var old = GUI.color; float drift = Time.unscaledTime * .025f, aspect = height / Mathf.Max(1, width);
            var screen = new Rect(0, 0, width, height);
            GUI.color = new Color(1, 1, 1, .9f * murk);
            GUI.DrawTextureWithTexCoords(screen, texture, new Rect(drift, drift * .6f, 1.4f, 1.4f * aspect));
            GUI.color = new Color(1, 1, 1, .7f * murk);
            GUI.DrawTextureWithTexCoords(screen, texture, new Rect(.37f - drift * .8f, .21f + drift * .3f, .8f, .8f * aspect));
            GUI.color = old;
        }

        private void PlayHUD(float width, float height)
        {
            hudWidth = width; hudHeight = height;
            var state = session.State;
            if (state.phase == RoundPhase.Finished) { FinishedHUD(width, height); return; }
            var p = Local(); if (p == null) return;
            MurkOverlay(width, height);
            HungerBorder(state, p, width, height);   // R73
            if (!session.IsSpectator && p.alive && p.team == Team.Hider && state.time < p.injuredUntil)
                DamageBorder(width, height, p.injuredUntil - state.time);
            EscapeMarkers(state, p, width, height);
            // Top left: the shared fish goal, then the eggs waiting to hatch.
            // R42: the diver does not get the fish count (it would tell how many fish are left in the tank).
            bool fishGoal = p.team != Team.Seeker;
            if (fishGoal)
            {
                Card(new Rect(24, 20, 236, 72));
                Label(40, 28, 200, 16, "포식 목표", caption);
                Icon(40, 47, HudIcon.Fish, cyan, 20);
                Label(68, 41, 172, 30, Cat(Int(state.eaten), " / ", Int(state.target)), heading);
                Meter(40, 76, 204, state.target == 0 ? 1 : (float)state.eaten / state.target, cyan, 5);
            }
            if (state.eggs != null && state.eggs.Length > 0)
            {
                float nearest = float.MaxValue, eggY = fishGoal ? 100 : 20;
                foreach (var egg in state.eggs) nearest = Mathf.Min(nearest, egg.hatchAt - state.time);
                Card(new Rect(24, eggY, 236, 40));
                Icon(40, eggY + 9, HudIcon.Egg, cyan, 20);
                // R25: a laid egg counts down from laying; afterwards it waits to revive a fallen ally.
                Label(68, eggY + 6, 176, 28, Cat(Int(state.eggs.Length), "개   ", nearest > 0 ? Cat(Int(Mathf.CeilToInt(nearest)), "s") : "부화 가능"), body);
                EggMeter(state, eggY);
            }
            // Top centre: compass strip and the round clock.
            Compass(width / 2, 16, 440, state, p);
            LidAlarm(state, p, width);
            float seconds = Mathf.Max(0, state.endsAt - Mathf.Max(state.time, state.huntAt));
            var clock = new Rect(width / 2 - 58, 52, 116, 32);
            Pill(clock, seconds <= 60 && state.time >= state.huntAt ? new Color(Warm.r, Warm.g, Warm.b, .7f) : Rim);
            Icon(clock.x + 14, clock.y + 8, HudIcon.Clock, seconds <= 60 && state.time >= state.huntAt ? Warm : cyan, 16);
            Label(clock.x + 38, clock.y, 76, 32, Clock((int)seconds), timerText);
            // Top right: role and condition.
            bool trouble = !session.IsSpectator && (p.injuredUntil > state.time || p.stunnedUntil > state.time || p.swallowedBy != 0);
            Card(new Rect(width - 260, 20, 236, 72), trouble ? new Color(Warm.r, Warm.g, Warm.b, .6f) : Rim);
            Label(width - 244, 28, 204, 16, session.IsSpectator ? "관전" : "역할", caption);
            Label(width - 244, 41, 204, 30, session.IsSpectator ? "관전 중" : p.team == Team.Hider ? AquariumAlienKinds.Get(p.alienKind).name : TeamName(p.team), heading);
            string condition = session.IsSpectator ? (p.name.Length > 14 ? p.name.Substring(0, 14) + "…" : p.name) : p.escaped ? "탈출 성공, 관전 중" : !p.alive ? "사망, 관전 중"
                : p.drainTask == DrainTask.Enter ? "배수구 진입 중"
                : p.swallowedBy != 0 ? Cat("탈출 ", Int(Mathf.CeilToInt(Mathf.Max(0, p.releaseAt - state.time))), "s")
                : p.layEndsAt > state.time ? Cat("산란 중 ", Tenths(p.layEndsAt - state.time), "s")
                : p.injuredUntil > state.time ? Cat("부상, 속도 −20% ", Tenths(p.injuredUntil - state.time), "s")
                : p.stunnedUntil > state.time ? Cat("경직 ", Tenths(p.stunnedUntil - state.time), "s")
                : p.morphLockedUntil > state.time ? Cat("위장 제한 ", Tenths(p.morphLockedUntil - state.time), "s")
                : p.team == Team.Seeker ? "추적 중" : p.species < 0 ? "기본형" : "위장 중";
            Label(width - 244, 69, 204, 20, condition, trouble ? alert : small);
            if (Button(new Rect(width - 112, height - 40, 88, 28), "Esc  메뉴")) pause = true;
            if (session.IsSpectator)
            {
                var watch = new Rect(width / 2 - 180, height - 92, 360, 56);
                Card(watch);
                Label(watch.x, watch.y, watch.width, watch.height, "다음 경기부터 참가", centred);
                // R49: spectators watching the diver see its catch count.
                if (p.team == Team.Seeker) CatchPill(24, height - 60, p);
                return;
            }
            // R49: a fallen alien watching through the diver's eyes sees the diver's catch count too.
            if (followCamera.SpectatingDiver)
            {
                var watched = Array.Find(state.players, s => s.id == followCamera.FollowId);
                if (watched != null && watched.team == Team.Seeker) CatchPill(width - 260, 100, watched);
            }
            if (p.team == Team.Hider) AlienVitals(state, p, height); else DiverVitals(state, p, height);
            // R30: small square quick slots.
            int slots = p.team == Team.Hider ? 5 : 3; const float slotW = 56, gap = 8;
            float actionsX = width / 2 - (slots * slotW + (slots - 1) * gap) / 2, actionsY = height - 98;
            if (p.escaped)
            {
                // R27: through the drain: this alien has won.
                Card(new Rect(width / 2 - 222, actionsY - 8, 444, 72), cyan);
                Label(width / 2 - 204, actionsY + 1, 408, 30, "탈출 성공! 승리!", heading);
                Label(width / 2 - 204, actionsY + 34, 408, 24, "배수구로 빠져나왔습니다, +" + AquariumModel.EscapeScore + "점, 관전 중", small);
            }
            else if (!p.alive)
            {
                Card(new Rect(width / 2 - 222, actionsY - 8, 444, 72));
                Label(width / 2 - 204, actionsY + 1, 408, 30, followCamera.DeathReview ? "사망" : "관전 중", heading);
                Label(width / 2 - 204, actionsY + 34, 408, 24, "동료의 알이 부화하면 부활", small);
            }
            else if (p.team == Team.Hider)
            {
                AlienSlots(state, p, actionsX, actionsY, slotW, gap);
            }
            else
            {
                DiverSlots(state, p, actionsX, actionsY, slotW, gap);
            }
            // R70b: the controls guide on the right edge (K shows / hides it) instead of the bar along the bottom.
            if (p.alive && !p.escaped) ControlsGuide(p, width, height);
            if (p.team == Team.Hider && p.alive && p.layEndsAt > state.time)
            {
                float left = p.layEndsAt - state.time;
                Shape(new Rect(width / 2 - 34, height / 2 + 54, 68, 68), Disc, 34);
                Ring(width / 2, height / 2 + 88, 68, 5, 1 - left / AquariumModel.LayEggSeconds, cyan);
                Label(width / 2 - 34, height / 2 + 74, 68, 28, Tenths(left), centred);
                Prompt(width / 2, height / 2 + 130, "", "알을 낳는 중");
            }
            if (state.phase == RoundPhase.Playing && state.time < state.huntAt)
            {
                var count = new Rect(width / 2 - 136, 96, 272, 46);
                Card(count);
                Label(count.x, count.y, count.width, count.height, Cat(p.team == Team.Hider ? "숨을 시간  " : "시작 대기  ", Int(Mathf.CeilToInt(state.huntAt - state.time)), "s"), centred);
            }
            if (!pause && p.alive && p.swallowedBy == 0)
            {
                float mx = width / 2, my = height / 2;
                Reticle(mx, my);
                float promptY = my + 30;
                if (morphTarget != null && p.team == Team.Hider) { KeyPrompt(mx, promptY, "Q", "위장하기"); promptY += PromptStep; }
                promptY = R47Prompts(state, p, mx, promptY);
                // R29: while one screw turns, aiming at another still shows that one's saved progress.
                // R30: only the grey ring round the aimed part (its saved progress); no text under the reticle,
                // except the short reason when F cannot work there at all.
                bool otherPart = drainTarget != null && !SameDrainJob(p, drainTarget);
                if (otherPart) { DrainPreview(state, p); promptY = DrainPrompt(mx, promptY, p); }
                // R41/R49: a drain job in hand: E lets go (backs out of the hole), unless E is aimed at another usable part.
                if (p.drainTask == DrainTask.Enter) KeyPrompt(mx, promptY, "C", "그만두기", Warm);   // R73f: backing out of the hole is C only
                else if (p.drainTask != DrainTask.None && !(otherPart && drainTarget.usable)) KeyPrompt(mx, promptY, "E", "그만두기", Warm);
            }
            if (!pause && p.alive && p.drainTask != DrainTask.None) DrainGauge(state, p, width, height);
            R47Overlay(state, p, width, height);
        }
        // Is the crosshair on the very part this swimmer is already working?
        private static bool SameDrainJob(Swimmer p, DrainTarget target)
        {
            if (p.drainTask == DrainTask.None || target == null || target.drain != p.drainIndex) return false;
            if (p.drainTask == DrainTask.Screw) return target.part == p.drainScrew;
            if (p.drainTask == DrainTask.Enter) return target.part == AquariumModel.DrainHolePart;
            return target.part == AquariumModel.DrainLidPart;
        }

        private void Vital(float cx, float cy, float diameter, float thickness, float ratio, Color color, HudIcon icon, string value, string label)
        {
            Shape(new Rect(cx - diameter / 2, cy - diameter / 2, diameter, diameter), Disc, diameter / 2);
            Ring(cx, cy, diameter, thickness, ratio, color);
            float iconSize = diameter * .26f;
            Icon(cx - iconSize / 2, cy - diameter * .31f, icon, color, iconSize);
            Label(cx - diameter / 2, cy - diameter * .03f, diameter, diameter * .34f, value, diameter >= 80 ? vital : vitalSmall);
        }
        // Two harpoon hits kill: the ring is split into two lives.
        private void Lives(float cx, float cy, float diameter, float thickness, int left)
        {
            var rect = new Rect(cx - diameter / 2, cy - diameter / 2, diameter, diameter);
            Shape(rect, Disc, diameter / 2);
            var alive = new Color(1f, 0.45f, 0.5f);
            for (int i = 0; i < 2; i++) Arc(rect, thickness, i * .5f + .02f, .46f, i < left ? alive : Track);
            Icon(cx - 7, cy - 16, HudIcon.Heart, left > 0 ? alive : muted, 14);
            Label(cx - diameter / 2, cy + 1, diameter, 20, Cat(Int(left), " / 2"), vitalSmall);
        }
        // R30: a small square quick slot: key badge in the corner, name, status, and a dimmed curtain over the part
        // still recharging (when the length is known). Lit rim when ready.
        private void Slot(float x, float y, float size, string key, string name, string status, float recharge, HudIcon icon)
        {
            bool ready = status == "준비" || status.StartsWith("준비 ×");
            SlotLit(x, y, size, key, name, status, recharge, ready, icon);
        }
        // R29: the compass strip (camera heading) with escape beacons on it for the diver.
        private void Compass(float cx, float y, float width, AquariumSnapshot state, Swimmer local)
        {
            var rect = new Rect(cx - width / 2, y, width, 30);
            Shape(rect, Glass, 15); Shape(rect, Rim, 15, 1.2f);
            var forward = view.Camera.transform.forward;
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            const float span = 150;
            float inner = width - 32, mid = inner / 2, perDegree = inner / span;
            GUI.BeginGroup(new Rect(rect.x + 16, rect.y, inner, rect.height));
            var old = GUI.color;
            int first = Mathf.FloorToInt((yaw - span / 2) / 5) * 5;
            for (int degree = first; degree <= yaw + span / 2 + 5; degree += 5)
            {
                float x = mid + Mathf.DeltaAngle(yaw, degree) * perDegree;
                if (x < -10 || x > inner + 10) continue;
                float fade = Mathf.Clamp01((1 - Mathf.Abs(x - mid) / mid) * 2.2f);
                int d = ((degree % 360) + 360) % 360;
                if (d % 45 == 0)
                {
                    GUI.color = new Color(1, 1, 1, fade);
                    GUI.Label(new Rect(x - 20, 0, 40, 30), Cardinal(d), d % 90 == 0 ? compassMain : compassMinor);
                    GUI.color = old;
                }
                else if (d % 15 == 0) Shape(new Rect(x - .75f, 10, 1.5f, 10), new Color(1, 1, 1, .55f * fade), .75f);
                else Shape(new Rect(x - .5f, 13, 1, 4), new Color(1, 1, 1, .3f * fade), .5f);
            }
            bool diverView = local.team == Team.Seeker || session.IsSpectator || followCamera.SpectatingDiver;
            if (diverView && state.effects != null)
                foreach (var e in state.effects)
                {
                    if (e.kind != "escape" || state.time < e.born || state.time > e.born + e.duration) continue;
                    var camera = view.Camera.transform.position;
                    float bearing = Mathf.Atan2(e.position.x - camera.x, e.position.z - camera.z) * Mathf.Rad2Deg;
                    float x = Mathf.Clamp(mid + Mathf.DeltaAngle(yaw, bearing) * perDegree, 6, inner - 6);
                    float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 7);
                    Shape(new Rect(x - 5, 10, 10, 10), new Color(1f, .45f + .2f * pulse, .2f, .95f), 5);
                }
            if (diverView) CompassDrains(state, yaw, mid, perDegree, inner);
            GUI.EndGroup();
            Shape(new Rect(cx - 1, y - 4, 2, 9), cyan, 1);
        }
        private static string Cardinal(int degree)
        {
            switch (degree) { case 0: return "N"; case 45: return "NE"; case 90: return "E"; case 135: return "SE"; case 180: return "S"; case 225: return "SW"; case 270: return "W"; default: return "NW"; }
        }

        // R28: aiming at a screw or the lid shows its saved progress on an inactive (grey) ring round it; the ring
        // lights up once F / E starts the job, and each screw keeps its own progress to be picked up again later.
        // R29: the numbers sit in the prompt stack under the reticle (DrainPrompt), so a second screw can be read
        // while one is turning without its labels landing on the active job's.
        private void DrainPreview(AquariumSnapshot state, Swimmer p)
        {
            if (state.drains == null || drainTarget.drain < 0 || drainTarget.drain >= state.drains.Length) return;
            var d = state.drains[drainTarget.drain];
            float progress;
            if (drainTarget.part >= 0 && drainTarget.part < AquariumDrainLayout.ScrewCount)
                progress = d.screws != null && drainTarget.part < d.screws.Length ? d.screws[drainTarget.part] : 0;
            else if (drainTarget.part == AquariumModel.DrainLidPart) progress = p.team == Team.Seeker ? 1 - d.lid : d.lid;
            else return;
            var screen = view.Camera.WorldToViewportPoint(view.DrainPartPosition(state, drainTarget.drain, drainTarget.part));
            if (screen.z > 0 && screen.x > .03f && screen.x < .97f && screen.y > .05f && screen.y < .95f)
                Ring(screen.x * hudWidth, (1 - screen.y) * hudHeight, 46, 3, progress, new Color(.8f, .88f, .92f, .85f), new Color(.6f, .7f, .74f, .25f));
        }
        // R28: where an alien got out through a drain, shown to the diver for EscapeMarkSeconds wherever it
        // is (HUD marker, so walls and reefs never hide it; pinned to the screen edge when it is off screen).
        private void EscapeMarkers(AquariumSnapshot state, Swimmer local, float width, float height)
        {
            bool diverView = local.team == Team.Seeker || session.IsSpectator || followCamera.SpectatingDiver;
            if (!diverView || state.effects == null) return;
            foreach (var e in state.effects)
            {
                if (e.kind != "escape") continue;
                float left = e.born + e.duration - state.time;
                if (left <= 0 || state.time < e.born) continue;
                var world = new Vector3(e.position.x, e.position.y + .5f, e.position.z);
                var s = view.Camera.WorldToViewportPoint(world);
                bool behind = s.z <= 0;
                var d = new Vector2(s.x - .5f, s.y - .5f);
                if (behind) d = -d;
                bool off = behind || Mathf.Abs(d.x) > .44f || Mathf.Abs(d.y) > .40f;
                if (off) { float k = Mathf.Max(Mathf.Abs(d.x) / .44f, Mathf.Abs(d.y) / .40f); if (k > .0001f) d /= k; }
                float x = (.5f + d.x) * width, y = (1 - (.5f + d.y)) * height;
                float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 7);
                var hot = new Color(1f, .42f + .2f * pulse, .2f, .95f);
                float size = 40 + 8 * pulse;
                Shape(new Rect(x - size / 2, y - size / 2, size, size), new Color(.3f, .08f, .02f, .35f), size / 2);
                Ring(x, y, size, 3, left / e.duration, hot, new Color(1f, .5f, .3f, .2f));
                Stroke(x - 8, y, x, y - 8, hot); Stroke(x, y - 8, x + 8, y, hot); Stroke(x + 8, y, x, y + 8, hot); Stroke(x, y + 8, x - 8, y, hot);
                var who = PlayerById(state.players, e.owner);
                float metres = (e.position - local.position).Length;
                string text = (who != null ? who.name + " " : "외계인 ") + "탈출!";
                string detail = Mathf.RoundToInt(metres) + "m   " + Mathf.CeilToInt(left) + "s" + (off ? "  (화면 밖)" : "");
                float w = Mathf.Max(Measure(promptText, text).x, Measure(small, detail).x) + 28;
                Card(new Rect(x - w / 2, y + 30, w, 46), new Color(1f, .55f, .3f, .7f), false, 10);
                Label(x - w / 2 + 14, y + 33, w - 20, 22, text, slotName);
                Label(x - w / 2 + 14, y + 53, w - 20, 20, detail, small);
            }
        }
        // R27: why F / E does nothing at the part under the crosshair (R30: shown only then; no action prompt).
        // R32: the one action that does get its key: [E] (R44; was F) 탈출하기 on an open drain the alien can squeeze into.
        // R49: every usable part gets its keycap prompt (screw, lid, hole for aliens; the diver's lid cover).
        private float DrainPrompt(float mx, float y, Swimmer p)
        {
            if (drainTarget.usable)
            {
                string text = p.team == Team.Seeker ? "배수구 뚜껑 닫기"
                    : drainTarget.part == AquariumModel.DrainHolePart ? "탈출하기"
                    : drainTarget.part == AquariumModel.DrainLidPart ? "뚜껑 치우기" : "나사 풀기";
                KeyPrompt(mx, y, "E", text);
                return y + PromptStep;
            }
            if (string.IsNullOrEmpty(drainTarget.reason)) return y;
            Prompt(mx, y, "", drainTarget.reason, new Color(Warm.r, Warm.g, Warm.b, .7f));
            return y + 36;
        }
        // R27: the job in hand. R30: just the lit ring round the part being worked (cyan, red while blocked).
        private void DrainGauge(AquariumSnapshot state, Swimmer p, float width, float height)
        {
            if (state.drains == null || p.drainIndex < 0 || p.drainIndex >= state.drains.Length) return;
            var d = state.drains[p.drainIndex];
            float progress; bool blocked = false;
            int part = p.drainTask == DrainTask.Screw ? p.drainScrew : p.drainTask == DrainTask.Enter ? AquariumModel.DrainHolePart : AquariumModel.DrainLidPart;
            if (p.drainTask == DrainTask.Screw) progress = d.screws != null && p.drainScrew >= 0 && p.drainScrew < d.screws.Length ? d.screws[p.drainScrew] : 0;
            else if (p.drainTask == DrainTask.Lid) progress = d.lid;
            else if (p.drainTask == DrainTask.Enter) progress = p.drainProgress;
            else { progress = 1 - d.lid; blocked = p.drainProgress < 0; }
            var screen = view.Camera.WorldToViewportPoint(view.DrainPartPosition(state, p.drainIndex, part));
            if (screen.z > 0 && screen.x > .03f && screen.x < .97f && screen.y > .05f && screen.y < .95f)
                Ring(screen.x * width, (1 - screen.y) * height, p.drainTask == DrainTask.Screw ? 46 : 64, 4, progress, blocked ? Danger : cyan);
        }
        private string Ready(float at, float time) { return at > time ? Cat(Tenths(at - time), "s") : "준비"; }
        private void DamageBorder(float width,float height,float remaining)
        {
            float pulse=.75f+.25f*Mathf.Sin(Time.unscaledTime*7),fade=Mathf.Clamp01(remaining/.4f);
            for(int i=0;i<18;i++)
            {
                float edge=i*3,alpha=.42f*(1-i/18f)*(1-i/18f)*pulse*fade;
                var c=new Color(.85f,.035f,.02f,alpha);
                Panel(new Rect(edge,edge,width-2*edge,3),c); Panel(new Rect(edge,height-edge-3,width-2*edge,3),c);
                Panel(new Rect(edge,edge+3,3,height-2*edge-6),c); Panel(new Rect(width-edge-3,edge+3,3,height-2*edge-6),c);
            }
        }
        // R70b: the order matches IconFiles (AquariumGameR70.cs).
        private enum HudIcon { Stamina, Hunger, Fish, Egg, Score, Clock, Heart, Harpoon, Drain, Disguise, Bite, LayEgg, Unmorph, Back, Ink, Shield, Invisible, Electric, Dash, Feed, Glue, Xray, Drone, Ping }
        private void Stroke(float x, float y, float endX, float endY, Color color)
        {
            var matrix = GUI.matrix;
            GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(x, y, 0),
                Quaternion.Euler(0, 0, Mathf.Atan2(endY - y, endX - x) * Mathf.Rad2Deg), Vector3.one);
            Panel(new Rect(0, -1, Vector2.Distance(new Vector2(x, y), new Vector2(endX, endY)), 2), color);
            GUI.matrix = matrix;
        }
        private void Icon(float x, float y, HudIcon icon, Color color)
        {
            if (icon == HudIcon.Stamina)
            {
                Stroke(x + 14, y + 1, x + 5, y + 13, color); Stroke(x + 5, y + 13, x + 12, y + 13, color);
                Stroke(x + 12, y + 13, x + 8, y + 23, color); Stroke(x + 8, y + 23, x + 21, y + 9, color);
                Stroke(x + 21, y + 9, x + 13, y + 9, color); Stroke(x + 13, y + 9, x + 14, y + 1, color);
            }
            else if (icon == HudIcon.Hunger)
            {
                Stroke(x + 5, y + 1, x + 5, y + 22, color); Stroke(x + 1, y + 1, x + 1, y + 8, color);
                Stroke(x + 9, y + 1, x + 9, y + 8, color); Stroke(x + 1, y + 8, x + 9, y + 8, color);
                Stroke(x + 18, y + 1, x + 18, y + 22, color); Stroke(x + 22, y + 1, x + 22, y + 12, color);
                Stroke(x + 18, y + 12, x + 22, y + 12, color);
            }
            else if (icon == HudIcon.Fish)
            {
                Stroke(x + 1, y + 12, x + 9, y + 5, color); Stroke(x + 9, y + 5, x + 18, y + 12, color);
                Stroke(x + 18, y + 12, x + 9, y + 19, color); Stroke(x + 9, y + 19, x + 1, y + 12, color);
                Stroke(x + 18, y + 12, x + 24, y + 5, color); Stroke(x + 24, y + 5, x + 24, y + 19, color);
                Stroke(x + 24, y + 19, x + 18, y + 12, color); Panel(new Rect(x + 5, y + 10, 2, 2), color);
            }
            else if (icon == HudIcon.Egg)
            {
                Stroke(x + 12, y + 1, x + 5, y + 8, color); Stroke(x + 5, y + 8, x + 3, y + 17, color);
                Stroke(x + 3, y + 17, x + 8, y + 23, color); Stroke(x + 8, y + 23, x + 16, y + 23, color);
                Stroke(x + 16, y + 23, x + 21, y + 17, color); Stroke(x + 21, y + 17, x + 19, y + 8, color);
                Stroke(x + 19, y + 8, x + 12, y + 1, color);
            }
            else if (icon == HudIcon.Score)
            {
                Stroke(x + 5, y + 2, x + 19, y + 2, color); Stroke(x + 5, y + 2, x + 8, y + 14, color);
                Stroke(x + 19, y + 2, x + 16, y + 14, color); Stroke(x + 8, y + 14, x + 16, y + 14, color);
                Stroke(x + 12, y + 14, x + 12, y + 22, color); Stroke(x + 6, y + 22, x + 18, y + 22, color);
                Stroke(x + 5, y + 5, x + 1, y + 5, color); Stroke(x + 1, y + 5, x + 4, y + 12, color);
                Stroke(x + 19, y + 5, x + 23, y + 5, color); Stroke(x + 23, y + 5, x + 20, y + 12, color);
            }
            else
            {
                Stroke(x + 6, y + 2, x + 18, y + 2, color); Stroke(x + 18, y + 2, x + 23, y + 8, color);
                Stroke(x + 23, y + 8, x + 23, y + 18, color); Stroke(x + 23, y + 18, x + 17, y + 23, color);
                Stroke(x + 17, y + 23, x + 6, y + 23, color); Stroke(x + 6, y + 23, x + 1, y + 18, color);
                Stroke(x + 1, y + 18, x + 1, y + 8, color); Stroke(x + 1, y + 8, x + 6, y + 2, color);
                Stroke(x + 12, y + 6, x + 12, y + 13, color); Stroke(x + 12, y + 13, x + 17, y + 13, color);
            }
        }
        private bool HasReviveTarget(AquariumSnapshot state)
        {
            foreach (var alien in state.players)
                if (alien.team == Team.Hider && !alien.alive && !alien.escaped && (state.eggs == null || !Array.Exists(state.eggs, egg => egg.revivePlayer == alien.id))) return true;
            return false;
        }
        private void OnDestroy()
        {
            if (!automated) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            session?.Dispose(); view?.Dispose(); alienPreview?.Dispose(); diverPreview?.Dispose();
            foreach (var clip in sounds.Values) Destroy(clip);
            if (font != null) Destroy(font);
            foreach (var arc in arcs.Values) if (arc != null) Destroy(arc);
            if (murkTexture != null) Destroy(murkTexture);
            AquariumGalleryCamera.Murk = 0;
        }
    }
}
