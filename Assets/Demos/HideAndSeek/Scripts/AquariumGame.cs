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
        private Team selectedTeam;
        private bool pause, pendingAction, pendingMorph;
        private float inputTimer;
        private GUIStyle title, heading, body, small, button, primary, field, number;
        private Font font;
        private Texture2D buttonTexture, hoverTexture, primaryTexture;
        private readonly Color ink = new Color(0.035f, 0.09f, 0.15f, 0.97f);
        private readonly Color cyan = new Color(0.38f, 0.96f, 0.9f);
        private readonly Color muted = new Color(0.61f, 0.75f, 0.81f);
        private AudioSource audioSource;
        private readonly Dictionary<string, AudioClip> sounds = new Dictionary<string, AudioClip>();
        private int lastEffect;
        private bool automated;

        private void Awake()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60;
            session = new AquariumSession(); view = new AquariumView(transform); backdrop = new AquariumModel();
            followCamera = new AquariumThirdPersonCamera(view.Camera);
            playerName = PlayerPrefs.GetString("Aquarium.Name", "Diver");
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.volume = 0.24f; audioSource.spatialBlend = 0;
            sounds["eat"] = Tone("eat", 700, 1150, 0.13f); sounds["shot"] = Tone("shot", 180, 55, 0.12f);
            sounds["burp"] = Tone("burp", 110, 370, 0.45f); sounds["morph"] = Tone("morph", 340, 820, 0.25f);
            sounds["caught"] = Tone("caught", 450, 90, 0.25f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var smoke = gameObject.AddComponent<AquariumSmokeDriver>();
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

        private Swimmer Local()
        {
            if (session.State == null) return null;
            return Array.Find(session.State.players, p => p.id == session.LocalId);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && session.Connected) pause = !pause;
            var p = Local();
            followCamera.Prepare(session.State, session.LocalId);
            bool playing = session.Connected && session.State.phase == RoundPhase.Playing;
            bool wasLocked = Cursor.lockState == CursorLockMode.Locked;
            bool captureMouse = playing && !pause && Application.isFocused && !automated;
            if (!automated) { Cursor.lockState = captureMouse ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !captureMouse; }
            if (captureMouse && wasLocked)
                followCamera.Look(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"), Input.mouseScrollDelta.y);
            if (playing && p != null && !automated)
            {
                bool control = captureMouse && wasLocked && p.alive && p.swallowedBy == 0;
                pendingAction |= control && (p.team == Team.Hider ? Input.GetKeyDown(KeyCode.E) : Input.GetMouseButtonDown(0));
                pendingMorph |= control && Input.GetKeyDown(KeyCode.Q);
                inputTimer += Time.unscaledDeltaTime;
                if (inputTimer >= 0.05f)
                {
                    inputTimer = 0;
                    var aim = followCamera.Aim(session.State, p);
                    var move = control ? followCamera.Movement(
                        (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                        (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0),
                        (Input.GetKey(KeyCode.Space) ? 1 : 0) - (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ? 1 : 0)) : new Point();
                    session.Input(new SwimInput
                    {
                        x = move.x, y = move.y, z = move.z,
                        aimX = aim.x, aimY = aim.y, aimZ = aim.z, slow = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
                        action = pendingAction && control, morph = pendingMorph && control
                    });
                    pendingAction = pendingMorph = false;
                }
            }
            else { pendingAction = pendingMorph = false; }
            session.Update(Time.unscaledDeltaTime);
            if (!session.Connected)
            {
                backdrop.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
                view.Draw(backdrop.Snapshot(), 0, Time.unscaledDeltaTime);
            }
            else
            {
                view.Draw(session.State, session.LocalId, Time.unscaledDeltaTime);
                foreach (var fx in session.State.effects)
                {
                    if (fx.id <= lastEffect) continue;
                    lastEffect = fx.id; AudioClip sound;
                    if (sounds.TryGetValue(fx.kind, out sound)) audioSource.PlayOneShot(sound);
                }
            }
        }

        private void LateUpdate()
        {
            followCamera.Prepare(session.State, session.LocalId);
            followCamera.UpdatePose(session.State, view, Time.unscaledDeltaTime);
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
            body = Text(16, Color.white); small = Text(13, muted); number = Text(30, cyan, FontStyle.Bold);
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
        private string TeamName(Team team) { return team == Team.Hider ? "숨는 물고기" : "작살 술래"; }

        private void OnGUI()
        {
            Styles(); float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
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
            GUI.matrix = Matrix4x4.identity;
        }
        private void Menu(float width, float height)
        {
            float x = 48, y = (height - 650) / 2;
            Panel(new Rect(x, y, 472, 650), ink);
            Panel(new Rect(x, y, 5, 650), cyan);
            Label(x + 30, y + 22, 410, 24, "MIMIC  /  AQUARIUM     •     MULTIPLAYER DEMO", small);
            Label(x + 30, y + 58, 410, 58, "숨은 물고기", title);
            Label(x + 30, y + 125, 400, 54, "물고기 사이로 스며들고, 수조를 비우세요.\n누군가는 물고기가 아닙니다.", body);
            Separator(x + 30, y + 192, 412);
            Label(x + 30, y + 210, 100, 25, "플레이어 이름", small);
            playerName = GUI.TextField(new Rect(x + 30, y + 240, 412, 42), playerName, 16, field);
            GUI.enabled = !session.Busy;
            if (Button(x + 30, y + 300, 199, (selectedTeam == Team.Hider ? "✓ " : "") + "숨는 물고기", selectedTeam == Team.Hider)) selectedTeam = Team.Hider;
            if (Button(x + 243, y + 300, 199, (selectedTeam == Team.Seeker ? "✓ " : "") + "작살 술래", selectedTeam == Team.Seeker)) selectedTeam = Team.Seeker;
            if (Button(x + 30, y + 358, 412, "인터넷 방 만들기  →", true)) Connect(true);
            joinCode = GUI.TextField(new Rect(x + 30, y + 414, 253, 42), joinCode, 12, field).ToUpperInvariant();
            GUI.enabled = !session.Busy && joinCode.Trim().Length >= 4;
            if (Button(x + 297, y + 414, 145, "코드로 참가")) Connect(false);
            GUI.enabled = !session.Busy;
            if (Button(x + 30, y + 474, 412, "혼자 연습하기  ·  AI 참가"))
            { SaveName(); lastEffect = 0; session.StartPractice(playerName, selectedTeam); }
            GUI.enabled = true;
            string status = session.Status;
            Label(x + 30, y + 536, 412, 80, string.IsNullOrEmpty(status) ? "2–8명 · 방 코드 접속 · 3D / 3인칭\nWASD 이동 · Space 상승 / Ctrl 하강\n마우스 시점 회전 · ESC 메뉴" : status, small);
            if (session.Busy && GUI.Button(new Rect(x + 318, y + 605, 124, 28), "연결 취소", button)) session.Leave();
            else if (!session.Busy && Application.CanStreamedLevelBeLoaded("00_Hub") && GUI.Button(new Rect(x + 30, y + 606, 155, 28), "← 데모 허브", button)) SceneManager.LoadScene("00_Hub");
            Label(width - 352, height - 104, 310, 60, "AQUARIUM 07\n먹을 것인가, 들킬 것인가.", heading);
        }
        private void SaveName() { playerName = AquariumModel.CleanName(playerName); PlayerPrefs.SetString("Aquarium.Name", playerName); PlayerPrefs.Save(); }
        private void Connect(bool host) { SaveName(); lastEffect = 0; notice = ""; _ = session.OpenInternet(host, joinCode, playerName, selectedTeam); }
        private void Leave() { session.Leave(); pause = false; notice = ""; lastEffect = 0; }

        private void Lobby(float width, float height)
        {
            float x = width / 2 - 320, y = height / 2 - 306;
            Panel(new Rect(x, y, 640, 612), ink);
            Label(x + 28, y + 22, 400, 35, "입수 준비", heading);
            Label(x + 28, y + 68, 130, 20, "친구에게 보낼 방 코드", small);
            Label(x + 28, y + 94, 350, 46, session.Code, number);
            if (Button(x + 446, y + 88, 165, "코드 복사")) { GUIUtility.systemCopyBuffer = session.Code; notice = "방 코드를 복사했습니다."; }
            Separator(x + 28, y + 154, 584);
            int row = 0; var local = Local();
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
                if (Button(x + 28, controls, 186, "역할 바꾸기")) session.Role(local.team == Team.Hider ? Team.Seeker : Team.Hider, false);
                if (session.IsHost)
                { if (Button(x + 228, controls, 184, "경기 시작", true)) notice = session.StartRound(); }
                else if (Button(x + 228, controls, 184, local.ready ? "준비 취소" : "준비 완료", !local.ready)) session.Role(local.team, !local.ready);
            }
            if (Button(x + 426, controls, 186, "방 나가기")) Leave();
            Label(x + 28, controls + 61, 584, 60, string.IsNullOrEmpty(notice) ? "물고기와 술래가 각각 1명 이상 필요합니다.\n모두 준비하면 방장이 시작할 수 있습니다." : notice, small);
        }

        private void PlayHUD(float width, float height)
        {
            var state = session.State; var p = Local(); if (p == null) return;
            Panel(new Rect(24, 20, 290, 88), ink);
            Label(42, 31, 260, 23, "수조에 남은 먹이", small);
            Label(42, 57, 120, 42, state.remaining + " / 32", number);
            Label(175, 64, 120, 28, session.Practice ? "AI 연습" : "인터넷 연결됨", small);
            float seconds = Mathf.Max(0, state.endsAt - Mathf.Max(state.time, state.huntAt));
            Panel(new Rect(width / 2 - 72, 20, 144, 88), ink);
            Label(width / 2 - 48, 26, 120, 25, "남은 시간", small);
            Label(width / 2 - 47, 56, 130, 45, string.Format("{0:00}:{1:00}", (int)seconds / 60, (int)seconds % 60), number);
            Panel(new Rect(width - 314, 20, 290, 88), ink);
            Label(width - 294, 31, 250, 30, TeamName(p.team), heading);
            string condition = !p.alive ? "잡혔습니다 · 관전 중" : p.swallowedBy != 0 ? "동료 뱃속! 곧 나옵니다" : p.team == Team.Seeker ? "이상하게 행동하는 물고기를 찾으세요" : p.revealUntil > state.time ? "정체 노출 중!" : p.species < 0 ? "Q로 가까운 물고기로 변신하세요" : "변신 중 · 물고기처럼 움직이세요";
            Label(width - 294, 68, 262, 30, condition, small);
            Panel(new Rect(24, height - 83, width - 48, 59), ink);
            string controls = p.team == Team.Hider ? "WASD 이동 · Space 상승 / Ctrl 하강 · 마우스 시점 · Q 변신 · E 먹기 · Shift 천천히" : "WASD 이동 · Space 상승 / Ctrl 하강 · 마우스 시점 · 왼쪽 클릭 작살 · 휠 카메라 거리";
            Label(43, height - 72, width - 300, 30, controls, body);
            float cooldown = Mathf.Max(0, p.nextAction - state.time);
            Label(43, height - 45, width - 310, 20, p.team == Team.Hider ? "먹이 32마리를 모두 먹으면 승리 · 동료를 삼키면 1초 뒤 뱉고 정체가 드러납니다" : "모든 외계인을 잡거나 시간을 버티면 승리 · 바위는 작살을 막습니다", small);
            if (Button(width - 195, height - 75, 156, cooldown > 0 ? "재사용 " + cooldown.ToString("0.0") + "s" : "ESC  메뉴")) pause = true;
            if (state.phase == RoundPhase.Playing && state.time < state.huntAt)
            {
                float x = width / 2 - 240;
                Panel(new Rect(x, 126, 480, 75), ink);
                Label(x + 24, 137, 440, 30, "숨을 시간  " + Mathf.CeilToInt(state.huntAt - state.time), heading);
                Label(x + 24, 173, 440, 24, p.team == Team.Hider ? "Q로 물고기를 복사하고 무리에 섞이세요." : "잠시 후 움직일 수 있습니다. 수조를 잘 살펴보세요.", small);
            }
            if (state.phase == RoundPhase.Finished)
            {
                float x = width / 2 - 255, y = height / 2 - 135;
                Panel(new Rect(x, y, 510, 270), ink);
                Label(x + 28, y + 26, 460, 58, state.winner == (int)Team.Hider ? "물고기 팀 승리!" : "술래 팀 승리!", title);
                Label(x + 28, y + 102, 454, 56, "먹은 물고기  " + state.eaten + " / 32\n" + (state.winner == (int)p.team ? "이번 수조는 우리 팀의 승리입니다." : "다음 판에 다시 도전해 보세요."), body);
                if (session.IsHost && Button(x + 28, y + 187, 216, session.Practice ? "다시 연습" : "대기실로", true))
                { if (session.Practice) { lastEffect = 0; session.StartPractice(playerName, selectedTeam); } else session.Rematch(); }
                if (!session.IsHost) Label(x + 28, y + 190, 216, 45, "방장의 재시작을 기다리는 중", small);
                if (Button(x + 266, y + 187, 216, "나가기")) Leave();
            }
            else if (!pause && p.alive && p.swallowedBy == 0)
            {
                float mx = width / 2, my = height / 2;
                Panel(new Rect(mx - 6, my - 1, 12, 2), cyan); Panel(new Rect(mx - 1, my - 6, 2, 12), cyan);
            }
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
