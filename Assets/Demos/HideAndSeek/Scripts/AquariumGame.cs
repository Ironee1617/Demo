using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PrototypeLab.Aquarium
{
    public sealed class AquariumGame : MonoBehaviour
    {
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
        private bool pause, showRules, pendingAction, pendingMorph, pendingFeed, pendingScan, pendingEgg;
        private float inputTimer;
        private MorphTarget morphTarget;
        private int pendingMorphTargetId;
        private bool pendingMorphTargetPlayer;
        private RoundPhase previousPhase;
        private GUIStyle title, heading, body, small, button, primary, field, number, roomNameText;
        private Font font;
        private Texture2D buttonTexture, hoverTexture, primaryTexture;
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
            Application.runInBackground = true; Application.targetFrameRate = 60;
            session = new AquariumSession(); view = new AquariumView(transform); backdrop = new AquariumModel(speciesCount: AquariumSpecies.Count);
            followCamera = new AquariumThirdPersonCamera(view.Camera);
            playerName = PlayerPrefs.GetString("Aquarium.Name", "Diver");
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.volume = 0.24f; audioSource.spatialBlend = 0;
            feedAudioSource = gameObject.AddComponent<AudioSource>(); feedAudioSource.volume = 0.38f; feedAudioSource.spatialBlend = 0;
            sounds["eat"] = Tone("eat", 700, 1150, 0.13f); sounds["shot"] = Tone("shot", 180, 55, 0.12f);
            sounds["burp"] = Tone("burp", 110, 370, 0.45f); sounds["morph"] = Tone("morph", 340, 820, 0.25f);
            sounds["caught"] = Tone("caught", 450, 90, 0.25f);
            sounds["hatch"] = Tone("hatch", 520, 1050, 0.5f); sounds["scan"] = Tone("scan", 300, 650, 0.35f);
            sounds["egg"] = Tone("egg", 500, 740, 0.2f);
            sounds["feed"] = FeedSound();
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
                return Array.Find(session.State.players, p => p.id == session.SpectatingId)
                    ?? Array.Find(session.State.players, p => p.alive && p.team == Team.Seeker)
                    ?? Array.Find(session.State.players, p => p.alive);
            return Array.Find(session.State.players, p => p.id == session.LocalId);
        }

        private Swimmer SoundListener()
        {
            var listener = Local();
            if (listener != null && !listener.alive && session.State.time - listener.diedAt >= AquariumModel.DeathReviewSeconds)
                listener = Array.Find(session.State.players, s => s.alive && s.team == Team.Seeker) ?? listener;
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
            if (Input.GetKeyDown(KeyCode.Escape))
            { if (roomDialog != RoomDialog.None) CloseRoomDialog(true); else if (showRules) showRules = false; else if (session.Connected) pause = !pause; }
            UpdateRoomMenuState();
            var p = Local();
            followCamera.Prepare(session.State, session.LocalId, session.SpectatingId);
            bool playing = session.Connected && session.State.phase == RoundPhase.Playing;
            bool reviewing = session.Connected && session.State.phase == RoundPhase.Finished && session.State.time < session.State.reviewEndsAt;
            bool wasLocked = Cursor.lockState == CursorLockMode.Locked;
            bool captureMouse = ((playing && !session.IsSpectator) || reviewing) && !pause && !showRules && Application.isFocused && !automated;
            if (!automated) { Cursor.lockState = captureMouse ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !captureMouse; }
            if (captureMouse && wasLocked)
                followCamera.Look(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"), Input.mouseScrollDelta.y);
            if (reviewing && captureMouse && wasLocked)
                followCamera.MoveFree(
                    (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                    (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0), Time.unscaledDeltaTime);
            if (playing && p != null && !automated && !session.IsSpectator)
            {
                bool control = captureMouse && wasLocked && p.alive && p.swallowedBy == 0;
                pendingAction |= control && session.State.time >= session.State.huntAt && (p.team == Team.Hider ? Input.GetKeyDown(KeyCode.E) : Input.GetMouseButtonDown(0));
                if (control && p.team == Team.Hider && Input.GetKeyDown(KeyCode.Q) && morphTarget != null)
                {
                    pendingMorph = true; pendingMorphTargetId = morphTarget.id; pendingMorphTargetPlayer = morphTarget.player;
                }
                pendingFeed |= control && p.team == Team.Seeker && Input.GetKeyDown(KeyCode.F);
                pendingScan |= control && p.team == Team.Seeker && Input.GetKeyDown(KeyCode.R);
                pendingEgg |= control && p.team == Team.Hider && Input.GetKeyDown(KeyCode.R);
                inputTimer += Time.unscaledDeltaTime;
                if (inputTimer >= 1f / 30f)
                {
                    inputTimer = 0;
                    var aim = followCamera.Aim(session.State, p);
                    var move = control ? followCamera.Movement(
                        (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                        (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)) : new Point();
                    session.Input(new SwimInput
                    {
                        x = move.x, y = move.y, z = move.z,
                        aimX = aim.x, aimY = aim.y, aimZ = aim.z, slow = control && Input.GetKey(KeyCode.LeftAlt),
                        sprint = control && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)),
                        action = pendingAction && control, morph = pendingMorph && control, feed = pendingFeed && control,
                        morphTargetId = pendingMorphTargetId, morphTargetPlayer = pendingMorphTargetPlayer,
                        scan = pendingScan && control, layEgg = pendingEgg && control
                    });
                    pendingAction = pendingMorph = pendingFeed = pendingScan = pendingEgg = false;
                    pendingMorphTargetId = 0;
                }
            }
            else { pendingAction = pendingMorph = pendingFeed = pendingScan = pendingEgg = false; }
            session.Update(Time.unscaledDeltaTime);
            if (!session.Connected)
            {
                backdrop.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
                view.Draw(backdrop.Snapshot(), 0, Time.unscaledDeltaTime);
            }
            else
            {
                view.Draw(session.State, session.LocalId, Time.unscaledDeltaTime);
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
                    else audioSource.PlayOneShot(sound);
                }
                previousPhase = session.State.phase;
            }
            UpdateFeedSound();
        }

        private void LateUpdate()
        {
            followCamera.Prepare(session.State, session.LocalId, session.SpectatingId);
            followCamera.UpdatePose(session.State, view, Time.unscaledDeltaTime);
            view.PresentEquipment(session.State, followCamera.FollowId, followCamera.FirstPerson);
            var local = Local();
            morphTarget = !session.IsSpectator && session.State != null && session.State.phase == RoundPhase.Playing && local != null
                && local.alive && local.team == Team.Hider && local.swallowedBy == 0
                ? view.FindMorphTarget(session.State, local, followCamera.Aim(session.State, local)) : null;
            view.SetMorphTarget(morphTarget);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && !automated && session != null && session.Connected) pause = true;
        }

        private Texture2D Texture(Color color)
        { var t = new Texture2D(1, 1); t.SetPixel(0, 0, color); t.Apply(); return t; }
        private GUIStyle Text(int size, Color color, FontStyle weight = FontStyle.Normal)
        { return new GUIStyle(GUI.skin.label) { font = font, fontSize = size, fontStyle = weight, wordWrap = true, richText = false, normal = { textColor = color } }; }
        private void Styles()
        {
            if (title != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 24);
            title = Text(40, Color.white, FontStyle.Bold); heading = Text(22, Color.white, FontStyle.Bold);
            body = Text(16, Color.white); roomNameText = Text(15, Color.white); small = Text(13, muted); number = Text(30, cyan, FontStyle.Bold);
            buttonTexture = Texture(new Color(0.09f, 0.2f, 0.27f)); hoverTexture = Texture(new Color(0.15f, 0.33f, 0.4f)); primaryTexture = Texture(cyan);
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 16, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(12, 12, 5, 5), richText = false };
            button.normal.background = buttonTexture; button.normal.textColor = Color.white;
            button.hover.background = hoverTexture; button.hover.textColor = Color.white;
            button.active.background = hoverTexture; button.active.textColor = cyan;
            primary = new GUIStyle(button); primary.normal.background = primaryTexture; primary.normal.textColor = ink;
            field = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 18, padding = new RectOffset(14, 14, 9, 8) };
            field.normal.background = buttonTexture; field.normal.textColor = Color.white;
            field.focused.background = hoverTexture; field.focused.textColor = Color.white;
        }
        private void Panel(Rect rect, Color color) { var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
        private void Label(float x, float y, float w, float h, string text, GUIStyle style) { GUI.Label(new Rect(x, y, w, h), text, style); }
        private bool Button(float x, float y, float w, string text, bool main = false) { return GUI.Button(new Rect(x, y, w, 42), text, main ? primary : button); }
        private void Separator(float x, float y, float width) { Panel(new Rect(x, y, width, 1), new Color(0.2f, 0.4f, 0.47f, 0.7f)); }
        private string TeamName(Team team) { return team == Team.Hider ? "외계인" : "잠수부 (술래)"; }

        private void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool captureHud = smoke != null && smoke.BeginHudCapture();
#endif
            Styles(); float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            if (showRules || roomDialog != RoomDialog.None) GUI.enabled = false;
            if (!session.Connected) Menu(width, height);
            else if (session.State.phase == RoundPhase.Lobby) Lobby(width, height);
            else PlayHUD(width, height);
            if (pause && session.Connected)
            {
                Panel(new Rect(0, 0, width, height), new Color(0, 0.03f, 0.06f, 0.6f));
                float x = width / 2 - 200, y = height / 2 - 120; Panel(new Rect(x, y, 400, 240), ink);
                Label(x + 24, y + 22, 352, 35, "수조 메뉴", heading);
                Label(x + 24, y + 63, 352, 30, "메뉴를 열어도 경기는 계속 진행됩니다.", small);
                if (Button(x + 24, y + 104, 352, "계속 헤엄치기", true)) pause = false;
                if (Button(x + 24, y + 162, 352, session.IsHost && !session.Practice ? "방 종료하고 나가기" : "나가기")) Leave();
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
            Panel(new Rect(x, y, 472, 650), ink); Panel(new Rect(x, y, 5, 650), cyan);
            Label(x + 30, y + 22, 410, 24, "AQUARIUM  /  HIDE & SEEK     ·     DEMO 0.1", small);
            Label(x + 30, y + 58, 410, 58, "수족관 술래잡기", title);
            Label(x + 30, y + 125, 400, 54, "평범한 물고기 사이, 누군가 숨어 있습니다.\n외계인 4명과 잠수부 1명의 수중 추격전.", body);
            Separator(x + 30, y + 192, 412);
            Label(x + 30, y + 210, 100, 25, "플레이어 이름", small);
            GUI.enabled = usable;
            playerName = GUI.TextField(new Rect(x + 30, y + 240, 412, 42), playerName, 16, field);
            if (Button(x + 30, y + 300, 199, (selectedTeam == Team.Hider ? "✓ " : "") + TeamName(Team.Hider), selectedTeam == Team.Hider)) selectedTeam = Team.Hider;
            if (Button(x + 243, y + 300, 199, (selectedTeam == Team.Seeker ? "✓ " : "") + TeamName(Team.Seeker), selectedTeam == Team.Seeker)) selectedTeam = Team.Seeker;
            if (Button(x + 30, y + 358, 412, "바로 플레이  →  AI와 연습", true))
            { SaveName(); lastEffect = 0; session.StartPractice(playerName, selectedTeam); }
            if (Button(x + 30, y + 412, 412, "온라인 방 만들기")) OpenRoomDialog(RoomDialog.Create);
            joinCode = GUI.TextField(new Rect(x + 30, y + 468, 253, 42), joinCode, 12, field).ToUpperInvariant();
            if (joinCode.Length == 0) Label(x + 44, y + 479, 219, 25, "친구의 방 코드 입력", small);
            GUI.enabled = usable && joinCode.Trim().Length >= 4;
            if (Button(x + 297, y + 468, 145, "코드로 참가")) OpenRoomDialog(RoomDialog.CodeJoin);
            GUI.enabled = !showRules && roomDialog == RoomDialog.None;
            string status = string.IsNullOrEmpty(roomDialogError) ? session.Status : roomDialogError;
            Label(x + 30, y + 532, 412, 68, string.IsNullOrEmpty(status)
                ? "1인 연습 / 2–5인 온라인 · 빈 외계인 자리는 AI\n경기 중인 방은 코드로 관전 입장합니다.\n다음 대기실부터 경기에 참가할 수 있습니다." : status, small);
            if (session.Busy && GUI.Button(new Rect(x + 318, y + 605, 124, 28), "연결 취소", button)) CloseRoomDialog(true);
            else if (!session.Busy && GUI.Button(new Rect(x + 30, y + 605, 412, 28), "게임 규칙과 데모 설정 보기", button)) showRules = true;
            DrawRoomList(548, y, width - 596, 650, usable);
        }

        private void DrawRoomList(float x, float y, float width, float height, bool usable)
        {
            Panel(new Rect(x, y, width, height), ink);
            Label(x + 24, y + 22, width - 175, 35, "온라인 방 목록", heading);
            GUI.enabled = usable && !session.RoomsBusy;
            if (Button(x + width - 144, y + 20, 120, session.RoomsBusy ? "조회 중…" : "새로고침")) RefreshRoomList();
            GUI.enabled = !showRules && roomDialog == RoomDialog.None;
            Label(x + 24, y + 72, width - 48, 42, "대기 중인 방만 표시합니다.\n경기가 시작되면 숨겨지고 대기실로 돌아오면 다시 나타납니다.", small);
            Separator(x + 24, y + 125, width - 48);
            float nameWidth = width - 430;
            Label(x + 24, y + 139, nameWidth, 25, "방 이름", small);
            Label(x + width - 394, y + 139, 54, 25, "인원", small);
            Label(x + width - 322, y + 139, 72, 25, "상태", small);
            Label(x + width - 238, y + 139, 82, 25, "입장 조건", small);
            int count = 0;
            if (session.Rooms != null)
                for (int i = 0; i < session.Rooms.Count; i++)
                    if (session.Rooms[i] != null && session.Rooms[i].phase == "Waiting") count++;
            if (count == 0)
            {
                Label(x + 34, y + 278, width - 68, 80,
                    session.RoomsBusy ? "대기 중인 방을 찾고 있습니다…" : "현재 대기 중인 방이 없습니다.\n새 방을 만들거나 친구의 코드로 참가하세요.", body);
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
                    Panel(new Rect(0, ry, width - 42, 59), visibleRow % 2 == 0 ? new Color(0.07f, 0.17f, 0.23f) : new Color(0.055f, 0.135f, 0.2f));
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
                : string.IsNullOrEmpty(session.RoomsStatus) ? "목록은 5초마다 자동 갱신됩니다. 새로고침으로 바로 확인할 수 있습니다.\n경기 중인 방은 친구의 코드로 관전 참가할 수 있습니다." : session.RoomsStatus, small);
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
                && !roomRequestPending && roomDialog == RoomDialog.None && !showRules
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
            float x = width / 2 - 240, y = height / 2 - 200; Panel(new Rect(x, y, 480, 400), ink);
            bool creating = roomDialog == RoomDialog.Create, codeJoining = roomDialog == RoomDialog.CodeJoin;
            Label(x + 24, y + 20, 432, 36, creating ? "온라인 방 만들기" : codeJoining ? "코드로 참가" : "비밀번호 입력", heading);
            Label(x + 24, y + 64, 432, 44, creating ? "방 목록에 표시할 이름을 정해 주세요.\n비밀번호를 비우면 누구나 참가할 수 있습니다."
                : codeJoining ? "경기 중인 방은 관전으로 입장합니다.\n다음 대기실부터 함께 플레이할 수 있습니다."
                : "대기 중인 방에 참가합니다.\n방장이 정한 비밀번호를 입력해 주세요.", small);
            Label(x + 24, y + 114, 432, 24, creating ? "방 이름 · 1–32자" : codeJoining ? "친구의 방 코드" : "참가할 방", small);
            bool busy = session.Busy || roomSubmitting;
            GUI.enabled = !busy;
            if (creating) roomNameDraft = GUI.TextField(new Rect(x + 24, y + 142, 432, 42), roomNameDraft, 32, field);
            else if (codeJoining) joinCode = GUI.TextField(new Rect(x + 24, y + 142, 432, 42), joinCode, 12, field).ToUpperInvariant();
            else Label(x + 24, y + 142, 432, 42, selectedRoomName, body);
            Label(x + 24, y + 198, 432, 23, creating || codeJoining ? "비밀번호 · 선택" : "방 비밀번호", small);
            roomPassword = GUI.PasswordField(new Rect(x + 24, y + 226, 432, 42), roomPassword, '●', 64, field);
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
            SaveName(); lastEffect = 0; notice = roomDialogError = "";
            int request = ++roomRequestVersion; roomSubmitting = roomRequestPending = true;
            try
            {
                if (kind == RoomDialog.Create) await session.CreateRoom(roomName, password, playerName, selectedTeam);
                else if (kind == RoomDialog.RoomJoin) await session.JoinRoom(lobbyId, password, playerName, selectedTeam);
                else await session.OpenInternet(false, joinCode, playerName, selectedTeam, password);
            }
            catch (Exception)
            { if (request == roomRequestVersion) roomDialogError = "접속하지 못했습니다. 인터넷 연결을 확인하고 다시 시도해 주세요."; }
            finally { if (request == roomRequestVersion) roomSubmitting = false; }
        }
        private void SaveName() { playerName = AquariumModel.CleanName(playerName); PlayerPrefs.SetString("Aquarium.Name", playerName); PlayerPrefs.Save(); }
        private void Leave() { session.Leave(); CloseRoomDialog(false); pause = false; showRules = false; notice = ""; lastEffect = 0; roomsRequested = false; }
        private void Lobby(float width, float height)
        {
            float x = width / 2 - 320, y = height / 2 - 306;
            Panel(new Rect(x, y, 640, 612), ink);
            Label(x + 28, y + 18, 584, 34, string.IsNullOrEmpty(session.RoomName) ? "입수 준비" : session.RoomName, string.IsNullOrEmpty(session.RoomName) ? heading : body);
            Label(x + 28, y + 52, 584, 23, (session.HasPassword ? "비밀번호 방" : "공개 방") + "  ·  플레이어 " + session.HumanCount + "명  ·  관전자 " + session.SpectatorCount + "명", small);
            Label(x + 28, y + 82, 250, 20, "친구에게 보낼 방 코드", small);
            Label(x + 28, y + 106, 350, 42, session.Code, number);
            if (Button(x + 446, y + 99, 165, "코드 복사")) { GUIUtility.systemCopyBuffer = session.Code; notice = "방 코드를 복사했습니다."; }
            Separator(x + 28, y + 154, 584);
            int row = 0; var local = session.IsSpectator ? null : Local();
            foreach (var p in session.State.players)
            {
                float ry = y + 169 + row++ * 35;
                Label(x + 28, ry, 290, 30, p.name + (p.id == session.LocalId ? "  (나)" : "") + (p.id == 1 ? "  방장" : ""), body);
                Label(x + 335, ry, 150, 30, TeamName(p.team), small);
                Label(x + 505, ry, 100, 30, p.ready ? "준비 완료" : "준비 중", small);
            }
            float controls = y + 460;
            if (local != null)
            {
                if (Button(x + 28, controls, 186, "역할 바꾸기")) { notice = ""; session.Role(local.team == Team.Hider ? Team.Seeker : Team.Hider, false); }
                if (session.IsHost)
                { if (Button(x + 228, controls, 184, "경기 시작", true)) notice = session.StartRound(); }
                else if (Button(x + 228, controls, 184, local.ready ? "준비 취소" : "준비 완료", !local.ready)) session.Role(local.team, !local.ready);
            }
            if (Button(x + 426, controls, 186, "방 나가기")) Leave();
            string lobbyNotice = !string.IsNullOrEmpty(notice) ? notice : session.Status;
            Label(x + 28, controls + 61, 584, 60, string.IsNullOrEmpty(lobbyNotice) ? "잠수부 1명 + 외계인 1명 이상이 필요합니다.\n모두 준비하면 시작합니다. 빈 외계인 자리는 AI가 채웁니다." : lobbyNotice, small);
        }

        private void PlayHUD(float width, float height)
        {
            var state = session.State;
            if (state.phase == RoundPhase.Finished) { FinishedHUD(width, height); return; }
            var p = Local(); if (p == null) return;
            ScanMarkers(state, p, width, height);
            Panel(new Rect(24, 20, 296, 116), ink);
            Label(42, 30, 260, 23, "TEAM  /  공동 포식 목표", small);
            Label(42, 57, 145, 42, state.eaten + " / " + state.target, number);
            Label(191, 64, 114, 28, "남은 먹이 " + state.remaining, body);
            Meter(42, 105, 260, state.target == 0 ? 1 : (float)state.eaten / state.target, cyan);
            float seconds = Mathf.Max(0, state.endsAt - Mathf.Max(state.time, state.huntAt));
            Panel(new Rect(width / 2 - 83, 20, 166, 88), ink);
            Label(width / 2 - 60, 26, 130, 25, session.Practice ? "AI 연습 · 남은 시간" : "대결 · 남은 시간", small);
            Label(width / 2 - 47, 56, 130, 45, string.Format("{0:00}:{1:00}", (int)seconds / 60, (int)seconds % 60), number);
            Panel(new Rect(width - 314, 20, 290, 88), ink);
            Label(width - 294, 30, 250, 34, session.IsSpectator ? "관전 중" : TeamName(p.team), heading);
            string condition = session.IsSpectator ? p.name + " · " + TeamName(p.team) : !p.alive ? (followCamera.DeathReview ? "사망 · 잠시 후 관전으로 전환" : "사망 · 잠수부 시점으로 관전")
                : p.swallowedBy != 0 ? "동족에게 먹힘 · " + Mathf.CeilToInt(p.releaseAt - state.time) + "초 후 탈출"
                : p.stunnedUntil > state.time ? "포식 경직 · " + (p.stunnedUntil - state.time).ToString("0.0") + "초"
                : p.morphLockedUntil > state.time ? "변신 제한 · " + (p.morphLockedUntil - state.time).ToString("0.0") + "초 / 이동 가능"
                : p.team == Team.Seeker ? "흔적을 추적하고 남은 알도 찾으세요"
                : p.species < 0 ? "기본형 · 물고기를 조준하고 Q" : "물고기 위장 중 · 정체를 숨기세요";
            Label(width - 294, 68, 265, 34, condition, small);
            Ranking(state, width - 314, 122, 290);
            if (state.eggs != null && state.eggs.Length > 0)
            {
                float nearest = float.MaxValue;
                foreach (var egg in state.eggs) nearest = Mathf.Min(nearest, egg.hatchAt - state.time);
                Panel(new Rect(24, 150, 296, 67), ink);
                Label(42, 158, 260, 25, "부화 대기 중인 알  " + state.eggs.Length, body);
                Label(42, 187, 260, 23, "가장 가까운 부화까지 " + Mathf.CeilToInt(Mathf.Max(0, nearest)) + "초", small);
            }
            if (session.IsSpectator)
            {
                Panel(new Rect(24, height - 115, width - 48, 90), ink);
                Label(44, height - 105, width - 220, 32, "관전 중 · " + p.name, heading);
                Label(44, height - 65, width - 220, 28, "진행 중인 경기로 입장했습니다. 다음 대기실부터 경기에 참가합니다.", body);
                if (GUI.Button(new Rect(width - 162, height - 65, 118, 28), "ESC  메뉴", button)) pause = true;
                return;
            }            Panel(new Rect(24, height - 162, 296, 137), ink);
            Label(42, height - 152, 260, 24, "스태미나  " + Mathf.RoundToInt(p.stamina) + " / 100", body);
            Meter(42, height - 122, 260, p.stamina / AquariumModel.MaxStamina, cyan);
            if (p.team == Team.Hider)
            {
                float hunger = p.alive ? Mathf.Clamp(AquariumModel.HungerSeconds - (state.time - p.lastMealAt), 0, AquariumModel.HungerSeconds) : 0;
                Label(42, height - 102, 260, 24, "굶주림까지  " + Mathf.CeilToInt(hunger) + "초", body);
                Meter(42, height - 72, 260, hunger / AquariumModel.HungerSeconds, hunger <= 15 ? new Color(1, 0.35f, 0.35f) : new Color(1, 0.73f, 0.36f));
                Label(42, height - 53, 260, 22, "직접 포식 " + p.personalEaten + "마리 · 점수 " + p.score, small);
            }
            else
            {
                Label(42, height - 101, 260, 44, "일반 물고기 오사  " + state.killed + "마리\n오사할수록 외계인 목표가 줄어듭니다.", small);
                Label(42, height - 53, 260, 22, "외계인과 남은 알을 모두 제거하세요.", small);
            }
            Panel(new Rect(334, height - 162, width - 358, 137), ink);
            if (!p.alive)
            {
                Label(354, height - 149, width - 400, 30, followCamera.DeathReview ? "사망 · 잠시 후 다른 플레이어를 관전합니다" : "사망한 외계인 · 잠수부의 눈으로 관전 중", heading);
                Label(354, height - 108, width - 400, 50, "동료의 알이 부화하면 다시 플레이합니다.\n외계인이 전멸해도 알이 남아 있으면 경기는 계속됩니다.", body);
            }
            else
            {
                float cardWidth = (width - 422) / 3;
                string eggStatus = p.swallowedBy != 0 ? "동료 뱃속에서는 사용 불가" : p.stunnedUntil > state.time ? "경직 종료 후 사용 가능" : p.eggUsed ? "이번 경기 사용 완료" : p.personalEaten < AquariumModel.EggFoodRequired
                    ? "직접 " + AquariumModel.EggFoodRequired + "마리 포식 필요" : !HasReviveTarget(state) ? "부활 가능한 사망자 필요" : "동료 부활 준비 완료";
                Skill(354, height - 151, cardWidth, p.team == Team.Hider ? "Q  위장" : "클릭  작살",
                    p.team == Team.Hider ? Ready(Mathf.Max(p.stunnedUntil, Mathf.Max(p.nextMorph, p.morphLockedUntil)), state.time) : Ready(Mathf.Max(p.nextAction, state.huntAt), state.time));
                Skill(370 + cardWidth, height - 151, cardWidth, p.team == Team.Hider ? "E  포식" : "F  먹이 뿌리기",
                    p.team == Team.Hider ? (state.time < state.huntAt ? "숨을 시간이 끝나면 사용 가능" : Ready(Mathf.Max(p.nextAction, p.stunnedUntil), state.time)) : Ready(p.nextFeed, state.time));
                Skill(386 + cardWidth * 2, height - 151, cardWidth, p.team == Team.Hider ? "R  산란" : "R  생명체 탐지",
                    p.team == Team.Hider ? eggStatus : p.scanUntil > state.time ? "탐지 중 " + (p.scanUntil - state.time).ToString("0.0") + "초" : Ready(p.nextScan, state.time));
                Label(354, height - 76, width - 400, 23, "W 시선 방향 전진 · S 후진 · A/D 좌우 · 마우스 시점 · Shift 질주", small);
                Label(354, height - 52, width - 580, 22, p.team == Team.Hider ? "일반 포식 경직 2초 / 동족 포식 경직 3초 + 변신 제한 3초" : "탐지 표식은 모든 생명체에 동일합니다. 외계인 정체는 알려주지 않습니다.", small);
            }
            if (GUI.Button(new Rect(width - 162, height - 57, 118, 24), "ESC  메뉴", button)) pause = true;
            if (state.phase == RoundPhase.Playing && state.time < state.huntAt)
            {
                float x = width / 2 - 222;
                Panel(new Rect(x, 126, 444, 84), ink);
                Label(x + 24, 137, 396, 34, "숨을 시간  " + Mathf.CeilToInt(state.huntAt - state.time), heading);
                Label(x + 24, 173, 396, 32, p.team == Team.Hider ? "Q로 위장하세요. 숨을 시간에는 먹을 수 없습니다." : "잠시 후 움직일 수 있습니다. 수조를 살펴보세요.", small);
            }
            if (!pause && p.alive && p.swallowedBy == 0)
            {
                float mx = width / 2, my = height / 2;
                Panel(new Rect(mx - 6, my - 1, 12, 2), cyan); Panel(new Rect(mx - 1, my - 6, 2, 12), cyan);
                if (morphTarget != null && p.team == Team.Hider)
                {
                    Panel(new Rect(mx - 91, my + 23, 182, 30), ink);
                    Label(mx - 79, my + 26, 164, 25, "Q  테두리 물고기로 위장", small);
                }
            }
        }

        private string Ready(float at, float time) { return at > time ? (at - time).ToString("0.0") + "초 후 사용" : "사용 가능"; }
        private void Meter(float x, float y, float width, float ratio, Color color)
        { Panel(new Rect(x, y, width, 8), new Color(0.13f, 0.24f, 0.29f)); Panel(new Rect(x, y, width * Mathf.Clamp01(ratio), 8), color); }
        private void Skill(float x, float y, float width, string name, string status)
        { Label(x, y, width, 36, name, heading); Label(x, y + 38, width, 30, status, small); }
        private bool HasReviveTarget(AquariumSnapshot state)
        {
            foreach (var alien in state.players)
                if (alien.team == Team.Hider && !alien.alive && (state.eggs == null || !Array.Exists(state.eggs, egg => egg.revivePlayer == alien.id))) return true;
            return false;
        }
        private void Ranking(AquariumSnapshot state, float x, float y, float width)
        {
            var aliens = Array.FindAll(state.players, a => a.team == Team.Hider);
            Array.Sort(aliens, (a, b) => a.score == b.score ? a.id.CompareTo(b.id) : b.score.CompareTo(a.score));
            Panel(new Rect(x, y, width, 58 + aliens.Length * 33), ink);
            Label(x + 18, y + 11, width - 36, 27, "외계인 개인 랭킹", body);
            Separator(x + 18, y + 45, width - 36);
            for (int i = 0; i < aliens.Length; i++)
            {
                var alien = aliens[i]; float ry = y + 52 + 33 * i;
                if (alien.id == session.LocalId) Panel(new Rect(x + 8, ry, width - 16, 30), new Color(0.1f, 0.29f, 0.33f));
                string name = alien.name.Length > 9 ? alien.name.Substring(0, 9) : alien.name;
                Label(x + 18, ry + 2, width - 105, 27, (i + 1) + "  " + name + (alien.id == session.LocalId ? " · 나" : ""), body);
                Label(x + width - 80, ry + 2, 66, 25, alien.score.ToString(), body);
            }
        }
        private void ScanMarkers(AquariumSnapshot state, Swimmer local, float width, float height)
        {
            Swimmer diver = local.team == Team.Seeker ? local : !local.alive && !followCamera.DeathReview ? Array.Find(state.players, a => a.team == Team.Seeker && a.alive) : null;
            if (diver == null || diver.scanUntil <= state.time) return;
            foreach (var fish in state.fish) if (!fish.dead && !fish.eaten) ScanMarker(fish.position, diver.position, width, height);
            foreach (var alien in state.players) if (alien.team == Team.Hider && alien.alive && alien.swallowedBy == 0) ScanMarker(alien.position, diver.position, width, height);
            if (state.eggs != null) foreach (var egg in state.eggs) ScanMarker(egg.position, diver.position, width, height);
            Label(width / 2 - 130, 111, 260, 25, "생명체 탐지 중 · 동일한 중립 표식", small);
        }
        private void ScanMarker(Point point, Point origin, float width, float height)
        {
            if ((point - origin).Length > 22) return;
            var s = view.Camera.WorldToViewportPoint(new Vector3(point.x, point.y, point.z));
            if (s.z <= 0 || s.x < 0 || s.x > 1 || s.y < 0 || s.y > 1) return;
            float x = s.x * width, y = (1 - s.y) * height; Color tint = new Color(0.62f, 0.92f, 0.95f, 0.8f);
            Panel(new Rect(x - 9, y - 9, 18, 1), tint); Panel(new Rect(x - 9, y + 9, 18, 1), tint);
            Panel(new Rect(x - 9, y - 9, 1, 18), tint); Panel(new Rect(x + 9, y - 9, 1, 18), tint);
        }

        private void Rules(float width, float height)
        {
            Panel(new Rect(0, 0, width, height), new Color(0, 0.025f, 0.04f, 0.84f));
            float x = width / 2 - 470, y = height / 2 - 300;
            Panel(new Rect(x, y, 940, 600), ink);
            Label(x + 30, y + 23, 820, 42, "플레이 가이드  /  수족관 술래잡기", heading);
            Label(x + 30, y + 70, 880, 30, "기획서 v0.1 · 2026.10.16 조작·관전 개선판", small);
            Separator(x + 30, y + 111, 880);
            Label(x + 30, y + 130, 420, 40, "기획서의 핵심 규칙", heading);
            Label(x + 30, y + 180, 420, 320,
                "외계인 4명 대 잠수부 1명\n\n일반 물고기를 모두 없애면 외계인 승리.\n잠수부 오사는 외계인 팀 목표를 줄입니다.\n\n일반 포식: 2초 경직 + 빨간 흔적 5초.\n동족 포식: 양쪽 3초 경직 + 3초 변신 제한.\n뱉을 때 노란 흔적 5초, 먹은 쪽만 감점.\n\n60초 동안 일반 물고기를 못 먹으면 사망.\n산란은 개인당 1회, 죽은 동료가 있어야 가능.\n알이 남으면 전멸해도 경기 계속.\n부화는 부활이며 승리 조건이 아닙니다.", body);
            Label(x + 488, y + 130, 420, 40, "이번 데모의 임시 설정", heading);
            Label(x + 488, y + 180, 420, 332,
                "경기 3분 · 일반 물고기 32마리\n시간 만료 시 잠수부 승리\n\n본인이 3마리 포식하면 산란 조건 달성.\n알은 12초 뒤 1명 부활, 작살 1회로 파괴.\n탐지 5초 / 쿨타임 18초 / 반경 22m.\n\n포식 +100 / 동족 포식 -150 / 부화 +150.\n잠수부 시야 노출 2초마다 +5 (생명당 +50).\n점수·포식량·산란 사용 여부는 부활 후 유지.\n\nW 시선 방향 전진 / S 후진 / A·D 좌우.\n위·아래를 보고 W로 상승·하강, Shift 질주.\n외계인: Q 위장 / E 포식 / R 산란\n잠수부: 클릭 작살 / F 먹이 / R 탐지", body);
            if (Button(x + 650, y + 530, 260, "확인 · 돌아가기", true)) showRules = false;
            Label(x + 30, y + 539, 590, 40, "팀 승패와 외계인 개인 점수는 별도로 계산합니다.", small);
        }

        private string VictoryReason(AquariumSnapshot state)
        {
            switch (state.winReason)
            {
                case WinReason.AllAliensCaught: return "외계인 전원 사망 · 남은 부활 알도 없습니다.";
                case WinReason.FoodGoalReached: return "외계인이 먹이 목표를 달성했습니다. (일반 물고기 사망 " + state.killed + "마리 반영)";
                case WinReason.TimeExpired: return "제한 시간 3분 종료 · 데모 설정에 따라 잠수부 승리.";
                case WinReason.DiversLeft: return "모든 잠수부가 퇴장했습니다.";
                default: return "경기가 종료되었습니다.";
            }
        }

        private void FinishedHUD(float width, float height)
        {
            var state = session.State;
            // The result scene contains only survivors; the ranking still records every score.
            foreach (var alien in state.players)
            {
                if (alien.team != Team.Hider || !alien.alive || alien.swallowedBy != 0) continue;
                var screen = view.Camera.WorldToViewportPoint(view.PlayerPosition(alien) + Vector3.up * 0.8f);
                bool outside = screen.z <= 0 || screen.x < 0.08f || screen.x > 0.92f || screen.y < 0.14f || screen.y > 0.8f;
                if (screen.z <= 0) { screen.x = 1 - screen.x; screen.y = 1 - screen.y; }
                float x = Mathf.Clamp(screen.x * width, 105, width - 105);
                float y = Mathf.Clamp((1 - screen.y) * height, 145, height - 125);
                Panel(new Rect(x - 96, y - 16, 192, 51), ink);
                Label(x - 86, y - 13, 175, 26, (outside ? "◆ " : "▼ ") + alien.name, body);
                Label(x - 86, y + 12, 175, 22, "외계인 · 생존 위치", small);
            }
            Panel(new Rect(24, 20, width - 48, 104), ink);
            Label(44, 30, width - 88, 36, state.winner == (int)Team.Hider ? "외계인 팀 승리!" : "잠수부 팀 승리!", heading);
            Label(44, 72, width - 88, 36, VictoryReason(state), body);
            Ranking(state, width - 314, 138, 290);
            float remaining = Mathf.Max(0, state.reviewEndsAt - state.time);
            if (remaining > 0)
            {
                Panel(new Rect(24, height - 88, width - 48, 64), ink);
                Label(44, height - 80, width - 88, 28, "자유시점  " + Mathf.CeilToInt(remaining) + "초 · 경기 종료 순간 정지", heading);
                Label(44, height - 48, width - 88, 22, "W 시선 방향 전진 · S 후진 · A/D 좌우 · 마우스 시점 · ESC 메뉴", small);
                return;
            }
            float panelX = width / 2 - 255, panelY = height / 2 - 100;
            Panel(new Rect(panelX, panelY, 510, 200), ink);
            Label(panelX + 28, panelY + 24, 454, 60,
                "먹은 물고기 " + state.eaten + " / 최종 목표 " + state.target + "\n잠수부가 죽인 일반 물고기 " + state.killed + "마리", body);
            if (session.IsHost && Button(panelX + 28, panelY + 125, 216, session.Practice ? "다시 연습" : "대기실로", true))
            { if (session.Practice) { lastEffect = 0; session.StartPractice(playerName, selectedTeam); } else session.Rematch(); }
            if (!session.IsHost) Label(panelX + 28, panelY + 128, 216, 45, "방장의 재시작을 기다리는 중", small);
            if (Button(panelX + 266, panelY + 125, 216, "나가기")) Leave();
        }

        private void OnDestroy()
        {
            if (!automated) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            session?.Dispose(); view?.Dispose();
            foreach (var clip in sounds.Values) Destroy(clip);
            if (font != null) Destroy(font);
            if (buttonTexture != null) Destroy(buttonTexture); if (hoverTexture != null) Destroy(hoverTexture); if (primaryTexture != null) Destroy(primaryTexture);
        }
    }
}
