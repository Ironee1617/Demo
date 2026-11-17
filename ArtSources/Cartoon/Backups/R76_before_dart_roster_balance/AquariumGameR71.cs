using System;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R71: the main menu without the big boxes. The reef of the character select fills the screen with the aliens and
    // divers chasing each other (AquariumMenuStage); over it, the title and four glass buttons down the left side:
    // 방 만들기 (online room, or AI practice and the tutorials), 방 찾기 (the room list and joining by code), 설정 (name,
    // preferred role, volume, screen) and 게임 종료. Each opens a panel on the right; Esc closes it.
    public sealed partial class AquariumGame
    {
        private enum MenuPanel { None, Create, Find, Tutorial, Settings }
        private MenuPanel menuPanel;
        private AquariumMenuStage menuStage;
        private Texture2D menuShade;
        private GUIStyle menuTitle, menuTitleShadow, menuButton, menuButtonSub, menuButtonHot;
        private float menuVolume = -1; private bool volumeDrag;
        private bool lobbyAlienPick;   // R72: a 술래 hopeful looking at the alien pick tab

        // Called every frame: the stage shows while not in a room (the tank's own camera rests meanwhile).
        private bool UpdateMenuStage()
        {
            bool show = !session.Connected && !automated;
            if (show && menuStage == null)
            {
                menuStage = new AquariumMenuStage();
                if (menuVolume < 0) { menuVolume = PlayerPrefs.GetFloat("Aquarium.Volume", 1); AudioListener.volume = menuVolume; }
            }
            if (menuStage != null) { menuStage.Show(show); if (show) menuStage.Update(Time.unscaledTime); }
            if (view != null && view.Camera != null && view.Camera.enabled == show) view.Camera.enabled = !show;
            if (!show) menuPanel = MenuPanel.None;
            return show;
        }

        private void MenuStyles()
        {
            if (menuTitle != null) return;
            menuTitle = new GUIStyle(title) { fontSize = 54, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
            menuTitle.normal.textColor = Color.white;
            menuTitleShadow = new GUIStyle(menuTitle); menuTitleShadow.normal.textColor = new Color(0, .05f, .09f, .55f);
            menuButton = new GUIStyle(heading) { fontSize = 21, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            menuButton.normal.textColor = Color.white;
            menuButtonHot = new GUIStyle(menuButton); menuButtonHot.normal.textColor = new Color(.02f, .1f, .14f);
            menuButtonSub = new GUIStyle(small) { fontSize = 12, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            const int n = 64; menuShade = new Texture2D(n, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Menu shade" };
            for (int i = 0; i < n; i++) { float a = 1 - i / (n - 1f); menuShade.SetPixel(i, 0, new Color(.0f, .04f, .07f, .78f * a * a * (3 - 2 * a))); }
            menuShade.Apply();
        }

        private void MainMenu(float width, float height)
        {
            MenuStyles();
            bool usable = !session.Busy && !roomSubmitting && !showRules && roomDialog == RoomDialog.None;
            // a soft shade behind the title and buttons so they read over the reef
            GUI.DrawTexture(new Rect(0, 0, 640, height), menuShade, ScaleMode.StretchToFill, true);
            float x = 72, y = Mathf.Max(48, height * .13f);
            Label(x + 2, y, 520, 18, "AQUARIUM  ·  HIDE & SEEK", caption);
            Label(x + 3, y + 25, 620, 70, "수족관 술래잡기", menuTitleShadow);
            Label(x, y + 22, 620, 70, "수족관 술래잡기", menuTitle);
            Shape(new Rect(x + 3, y + 96, 72, 4), cyan, 2);
            Label(x + 2, y + 110, 520, 24, "외계인 4명, 잠수부 1명. 물고기 사이에 숨어 살아남으세요.", Tinted(body, new Color(.86f, .95f, .98f)));

            GUI.enabled = usable;
            float by = Mathf.Max(y + 160, height * .38f), bw = 300, bh = 54, gap = 12;
            // R72: five plain buttons (no descriptions inside); the tutorials have their own button.
            string[] labels = { "방 만들기", "방 찾기", "튜토리얼", "설정", "게임 종료" };
            MenuPanel[] panels = { MenuPanel.Create, MenuPanel.Find, MenuPanel.Tutorial, MenuPanel.Settings, MenuPanel.None };
            for (int i = 0; i < labels.Length; i++)
            {
                if (!MenuButton(new Rect(x, by + i * (bh + gap), bw, bh), labels[i], "", panels[i] != MenuPanel.None && menuPanel == panels[i], i == 0)) continue;
                if (panels[i] == MenuPanel.None) QuitGame();
                else { Toggle(panels[i]); if (menuPanel == MenuPanel.Find) RefreshRoomList(); }
            }
            GUI.enabled = !showRules && roomDialog == RoomDialog.None;

            string status = string.IsNullOrEmpty(roomDialogError) ? session.Status : roomDialogError;
            if (!string.IsNullOrEmpty(status)) Label(x, by + 5 * (bh + gap) + 6, 520, 40, status, Tinted(small, new Color(1f, .82f, .5f)));
            if (session.Busy && Button(new Rect(x, by + 5 * (bh + gap) + 46, 160, 34), "연결 취소")) CloseRoomDialog(true);

            float px = Mathf.Max(x + bw + 56, width * .42f), pw = Mathf.Min(720, width - px - 48);
            if (menuPanel == MenuPanel.Create) CreatePanel(px, pw, height, usable);
            else if (menuPanel == MenuPanel.Find) FindPanel(px, pw, height, usable);
            else if (menuPanel == MenuPanel.Tutorial) TutorialPanel(px, pw, height, usable);
            else if (menuPanel == MenuPanel.Settings) SettingsPanel(px, pw, height, usable);
            GUI.enabled = true;
        }

        private void Toggle(MenuPanel panel) { menuPanel = menuPanel == panel ? MenuPanel.None : panel; roomDialogError = ""; GUI.FocusControl(null); }

        // A tall glass button: the label (and a quiet line under it), a cyan bar on the left and a little slide when
        // hovered; open panels keep their button lit; the main button is filled.
        private bool MenuButton(Rect r, string label, string sub, bool open, bool main)
        {
            bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
            float slide = hover ? 8 : open ? 6 : 0;
            var rr = new Rect(r.x + slide, r.y, r.width, r.height);
            var fill = main ? (hover ? new Color(.62f, 1f, .95f, .96f) : new Color(cyan.r, cyan.g, cyan.b, .9f))
                : open ? new Color(.1f, .34f, .42f, .86f) : hover ? new Color(.09f, .28f, .36f, .82f) : new Color(.03f, .13f, .19f, .66f);
            if (!GUI.enabled) fill.a *= .5f;
            Shape(rr, fill, 14);
            if (!main) Shape(rr, hover || open ? new Color(.6f, .95f, 1f, .8f) : new Color(.55f, .93f, 1f, .35f), 14, 1.4f);
            if (!main && (hover || open)) Shape(new Rect(rr.x + 12, rr.y + 14, 4, rr.height - 28), cyan, 2);
            var text = main ? menuButtonHot : menuButton;
            bool hasSub = !string.IsNullOrEmpty(sub);
            Label(rr.x + 28, rr.y + (hasSub ? 6 : 0), rr.width - 40, hasSub ? 32 : rr.height, label, text);
            if (hasSub) Label(rr.x + 29, rr.y + 34, rr.width - 40, 18, sub, main ? Tinted(menuButtonSub, new Color(.03f, .22f, .26f)) : menuButtonSub);
            Label(rr.xMax - 36, rr.y, 24, rr.height, "›", Tinted(menuButton, main ? new Color(.02f, .1f, .14f, .8f) : new Color(.7f, .95f, 1f, hover ? 1 : .5f)));
            return UiButton(r, GUIContent.none, GUIStyle.none);
        }

        private Rect PanelCard(float px, float pw, float ph, float height, string titleText, string sub)
        {
            var r = new Rect(px, Mathf.Max(24, (height - ph) / 2), pw, ph);
            Card(r, true, 18);
            Label(r.x + 26, r.y + 20, pw - 120, 34, titleText, heading);
            if (!string.IsNullOrEmpty(sub)) Label(r.x + 26, r.y + 56, pw - 60, 22, sub, small);
            if (Button(new Rect(r.xMax - 64, r.y + 18, 42, 36), "✕")) { menuPanel = MenuPanel.None; GUI.FocusControl(null); }
            return r;
        }

        private void CreatePanel(float px, float pw, float height, bool usable)
        {
            var r = PanelCard(px, pw, 380, height, "방 만들기", "친구와 온라인으로, 또는 혼자 AI와 연습하세요.");
            float cx = r.x + 26, cw = (r.width - 52 - 16) / 2, top = r.y + 96;
            GUI.enabled = usable;
            // online room
            var online = new Rect(cx, top, cw, 216); Card(online, false, 14);
            Label(online.x + 18, online.y + 16, cw - 36, 28, "온라인 방", Tinted(heading, cyan));
            Label(online.x + 18, online.y + 52, cw - 36, 80, "2–5인, 빈자리는 AI로 채울지 방에서 정합니다.\n방 이름과 비밀번호(선택)를 정해 친구를 부르세요.", small);
            if (Button(new Rect(online.x + 18, online.yMax - 60, cw - 36, 42), "온라인 방 만들기", true)) OpenRoomDialog(RoomDialog.Create);
            // AI practice
            var practice = new Rect(cx + cw + 16, top, cw, 216); Card(practice, false, 14);
            Label(practice.x + 18, practice.y + 16, cw - 36, 28, "AI 연습", Tinted(heading, new Color(1f, .82f, .45f)));
            Label(practice.x + 18, practice.y + 52, cw - 36, 22, "혼자 연습, 나머지는 AI", small);
            float hw = (cw - 36 - 8) / 2;
            if (Button(new Rect(practice.x + 18, practice.y + 84, hw, 38), (selectedTeam == Team.Hider ? "✓ " : "") + "외계인", selectedTeam == Team.Hider)) selectedTeam = Team.Hider;
            if (Button(new Rect(practice.x + 18 + hw + 8, practice.y + 84, hw, 38), (selectedTeam == Team.Seeker ? "✓ " : "") + "잠수부", selectedTeam == Team.Seeker)) selectedTeam = Team.Seeker;
            if (Button(new Rect(practice.x + 18, practice.yMax - 60, cw - 36, 42), "연습 시작"))
            { SaveName(); lastEffect = 0; session.OpenPracticeLobby(playerName, selectedTeam, selectedAlien); }
            Label(cx, r.yMax - 46, r.width - 52, 24, "이름은 설정에서 바꿀 수 있습니다.  현재 이름: " + AquariumModel.CleanName(playerName), small);
        }

        // R72: the three solo tutorials, each a wide button with its length.
        private void TutorialPanel(float px, float pw, float height, bool usable)
        {
            var r = PanelCard(px, Mathf.Min(pw, 560), 330, height, "튜토리얼", "혼자 1~2분, 조작과 규칙을 차근차근 익혀요.");
            GUI.enabled = usable;
            for (int i = 1; i <= 3; i++)
                if (Button(new Rect(r.x + 26, r.y + 96 + (i - 1) * 64, r.width - 52, 52), TutorialNames[i].Replace("튜토리얼: ", ""), i == 1)) StartTutorial(i);
        }

        private void FindPanel(float px, float pw, float height, bool usable)
        {
            float ph = Mathf.Min(650, height - 48), top = Mathf.Max(24, (height - ph) / 2);
            DrawRoomList(px, top, pw, ph, usable);
            if (Button(new Rect(px + pw - 196, top + 20, 42, 36), "✕")) { menuPanel = MenuPanel.None; GUI.FocusControl(null); }
            // join by code, along the bottom of the list
            float jy = top + ph - 62;
            Shape(new Rect(px + 14, jy - 8, pw - 28, 58), new Color(0.02f, 0.09f, 0.13f, 0.96f), 12);
            GUI.enabled = usable;
            joinCode = TextField(new Rect(px + 24, jy, pw - 48 - 170, 42), joinCode, 12).ToUpperInvariant();
            if (joinCode.Length == 0) Label(px + 38, jy + 11, 260, 25, "친구의 방 코드 입력", small);
            GUI.enabled = usable && joinCode.Trim().Length >= 4;
            if (Button(px + pw - 24 - 160, jy, 160, "코드로 참가", true)) OpenRoomDialog(RoomDialog.CodeJoin);
            GUI.enabled = usable;
        }

        private void SettingsPanel(float px, float pw, float height, bool usable)
        {
            var r = PanelCard(px, Mathf.Min(pw, 560), 390, height, "설정", "");
            float sx = r.x + 26, sw = r.width - 52, y = r.y + 76;
            GUI.enabled = usable;
            Label(sx, y, 200, 22, "플레이어 이름", caption);
            string before = playerName;
            playerName = TextField(new Rect(sx, y + 22, sw, 42), playerName, 16);
            if (playerName != before) { PlayerPrefs.SetString("Aquarium.Name", AquariumModel.CleanName(playerName)); }
            y += 84;
            float hw = (sw - 10) / 2;
            Label(sx, y, 200, 22, "전체 음량", caption);
            if (menuVolume < 0) menuVolume = PlayerPrefs.GetFloat("Aquarium.Volume", 1);
            float v = Slider(new Rect(sx, y + 30, sw - 70, 20), menuVolume);
            Label(sx + sw - 56, y + 22, 56, 30, Mathf.RoundToInt(v * 100) + "%", body);
            if (!Mathf.Approximately(v, menuVolume)) { menuVolume = v; AudioListener.volume = v; PlayerPrefs.SetFloat("Aquarium.Volume", v); }
            y += 70;
            Label(sx, y, 200, 22, "화면", caption);
            bool full = Screen.fullScreen;
            if (Button(new Rect(sx, y + 22, hw, 40), (full ? "✓ " : "") + "전체 화면", full)) Screen.fullScreen = true;
            if (Button(new Rect(sx + hw + 10, y + 22, hw, 40), (!full ? "✓ " : "") + "창 모드", !full)) Screen.fullScreen = false;
            y += 80;
            if (Button(new Rect(sx, y, sw, 40), "조작과 규칙 보기")) showRules = true;
        }

        // A drag slider in the menu's own look.
        private float Slider(Rect r, float value)
        {
            var e = Event.current; var hit = new Rect(r.x - 6, r.y - 10, r.width + 12, r.height + 20);
            if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && hit.Contains(e.mousePosition)) { volumeDrag = true; e.Use(); }
            if (e.rawType == EventType.MouseUp) volumeDrag = false;
            if (volumeDrag && (e.type == EventType.MouseDrag || e.type == EventType.MouseDown || e.type == EventType.Used))
                value = Mathf.Clamp01((e.mousePosition.x - r.x) / r.width);
            float cy = r.y + r.height / 2;
            Shape(new Rect(r.x, cy - 3, r.width, 6), Track, 3);
            Shape(new Rect(r.x, cy - 3, r.width * value, 6), cyan, 3);
            Shape(new Rect(r.x + r.width * value - 10, cy - 10, 20, 20), Color.white, 10);
            Shape(new Rect(r.x + r.width * value - 10, cy - 10, 20, 20), cyan, 10, 2);
            return value;
        }

        private void QuitGame()
        {
            SaveName(); PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }
    }
}
