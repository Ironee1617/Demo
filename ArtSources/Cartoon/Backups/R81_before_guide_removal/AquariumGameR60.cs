using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R60: chat in the waiting room and after the round (never while playing), the aliens' pings (middle mouse,
    // seen by the aliens only, pointing only), and the divers' changing room: built here (R81: with the guide computer on its
    // wall, the walk input flattened to the floor, a small hint while waiting, and the fade to black over the cut to
    // the dive deck.
    public sealed partial class AquariumGame
    {
        private const string ChatControl = "aquarium.chat";
        private string chatText = "";
        private bool chatTyping, chatBlur, chatFocus;
        private int chatSeen;
        private GUIStyle chatLine, chatName, pingName, chatField;
        private GameObject lockerRoom;
        private AudioClip pingSound;
        private int pingsHeard;
        private float pingAllowedAt;

        private bool ChatOpen { get { return session.CanChat; } }

        // ---- chat ----------------------------------------------------------------------------------------------
        private void ChatStyles()
        {
            if (chatLine != null) return;
            chatLine = new GUIStyle(small) { wordWrap = false, clipping = TextClipping.Clip, fontSize = 13 }; chatLine.normal.textColor = new Color(.9f, .96f, .98f); Steady(chatLine);
            chatName = new GUIStyle(chatLine) { fontStyle = FontStyle.Bold };
            pingName = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13, clipping = TextClipping.Overflow };
            pingName.normal.textColor = Color.white; Steady(pingName);
            chatField = new GUIStyle(field) { fontSize = 14, padding = new RectOffset(12, 12, 7, 6) };
        }

        // Enter opens the line or sends it, Esc closes it (handled before the box draws so the key never types).
        private void ChatKeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                if (GUI.GetNameOfFocusedControl() == ChatControl)
                {
                    if (CleanTyped().Length > 0) session.SendChat(chatText);
                    chatText = ""; chatBlur = true;
                }
                else chatFocus = true;
                e.Use();
            }
            else if (e.keyCode == KeyCode.Escape && GUI.GetNameOfFocusedControl() == ChatControl) { chatBlur = true; e.Use(); }
        }
        private string CleanTyped() { return AquariumSession.CleanChat(chatText); }

        private void ChatBox(Rect r, string title)
        {
            ChatStyles();
            if (!ChatOpen) { chatTyping = false; return; }
            ChatKeys();
            Card(r, false, 12);
            Label(r.x + 14, r.y + 8, 120, 18, title, caption);
            Label(r.x + r.width - 214, r.y + 8, 200, 18, chatTyping ? "Enter 보내기   Esc 닫기" : "Enter 채팅", Tinted(caption, new Color(muted.r, muted.g, muted.b, .9f)));
            const float lineH = 18;
            var lines = new Rect(r.x + 14, r.y + 27, r.width - 28, r.height - 27 - 42);
            int fit = Mathf.Max(1, Mathf.FloorToInt(lines.height / lineH));
            var chat = session.Chat; int start = Mathf.Max(0, chat.Count - fit);
            if (chat.Count == 0) Label(lines.x, lines.y, lines.width, lineH, "아직 대화가 없습니다. 인사를 건네 보세요!", Tinted(chatLine, new Color(muted.r, muted.g, muted.b, .75f)));
            for (int i = start; i < chat.Count; i++)
            {
                var line = chat[i]; float ly = lines.y + (i - start) * lineH;
                var nameColor = line.spectator ? muted : TeamColor(line.team);   // R70b: the waiting room's team colours
                string who = (line.player == session.LocalId ? "나" : line.name) + (line.spectator ? " (관전)" : "");
                float nw = Mathf.Min(150, chatName.CalcSize(new GUIContent(who + ":")).x + 2);
                Label(lines.x, ly, nw, lineH, who + ":", Tinted(chatName, nameColor));
                Label(lines.x + nw + 4, ly, lines.width - nw - 4, lineH, line.text, chatLine);
            }
            var input = new Rect(r.x + 10, r.yMax - 39, r.width - 20, 32);
            GUI.SetNextControlName(ChatControl);
            FieldBack(input);
            chatText = GUI.TextField(input, chatText ?? "", AquariumSession.ChatLimit, chatField);
            if (string.IsNullOrEmpty(chatText) && GUI.GetNameOfFocusedControl() != ChatControl)
                Label(input.x + 13, input.y + 6, input.width - 20, 22, "Enter를 눌러 메시지 입력", Tinted(chatLine, new Color(muted.r, muted.g, muted.b, .7f)));
            if (chatFocus) { GUI.FocusControl(ChatControl); chatFocus = false; }
            if (chatBlur) { GUI.FocusControl(null); chatBlur = false; }
            if (Event.current.type == EventType.Repaint) chatTyping = GUI.GetNameOfFocusedControl() == ChatControl;
            chatSeen = chat.Count;
        }

        // The lobby box: inside the left card between the player list and the buttons.
        private void LobbyChat(float x, float top, float width, float bottom)
        {
            if (bottom - top < 90) return;
            ChatBox(new Rect(x, top, width, bottom - top), "대기실 채팅");
        }

        // After the round: bottom left, clear of the results table.
        private void FinishedChat(float width, float height, bool reviewing)
        {
            if (session.Practice) return;
            float h = 196, w = reviewing ? 380 : 300, y = reviewing ? height - 96 - h : height - 24 - h;
            ChatBox(new Rect(24, y, w, h), "채팅");
        }

        // ---- pings -----------------------------------------------------------------------------------------------
        private void PingInput(Swimmer p, bool control)
        {
            if (!control || p.team != Team.Hider || !Input.GetMouseButtonDown(2) || Time.unscaledTime < pingAllowedAt) return;
            pingAllowedAt = Time.unscaledTime + .5f;
            var cam = view.Camera.transform; var state = session.State;
            var start = new Point(cam.position.x, cam.position.y, cam.position.z);
            var end = start + new Point(cam.forward.x, cam.forward.y, cam.forward.z) * 45;
            float hit = AquariumModel.ObstacleHit(start, end);
            foreach (var fish in state.fish) if (!fish.eaten && !fish.dead) hit = Mathf.Min(hit, AquariumModel.FishHitTime(start, end, fish));
            foreach (var other in state.players) if (other.id != p.id && other.alive && other.swallowedBy == 0) hit = Mathf.Min(hit, AquariumModel.PlayerHitTime(start, end, other, state.time, state.drains));
            var at = start + (end - start) * (hit <= 1 ? hit : 20f / 45f);
            at = AquariumModel.Constrain(at, .25f); at.y = Mathf.Min(at.y, AquariumModel.WaterHeight - .2f);
            session.SendPing(at);
        }

        private void PingMarkers(AquariumSnapshot state, Swimmer local, float width, float height)
        {
            var pings = session.Pings; float now = Time.realtimeSinceStartup;
            for (int i = pings.Count - 1; i >= 0; i--) if (now - pings[i].shownAt > 6) pings.RemoveAt(i);
            // (R73h: the 3D arrows are placed in Update, UpdatePingArrows)   // R66: no closure every frame
            if (pings.Count > pingsHeard && pingSound != null && local != null && local.team == Team.Hider) audioSource.PlayOneShot(pingSound, .55f);
            pingsHeard = pings.Count;
            if (local == null || local.team != Team.Hider || state.phase != RoundPhase.Playing) return;
            ChatStyles();
            var cam = view.Camera;
            foreach (var mark in pings)
            {
                float age = now - mark.shownAt, life = 5.5f, fade = Mathf.Clamp01((life - age) / .8f);
                if (fade <= 0) continue;
                var kind = AquariumAlienKinds.Get(mark.alienKind); var tint = new Color(kind.r, kind.g, kind.b);
                var world = new Vector3(mark.at.x, mark.at.y, mark.at.z);
                // R73h: on screen the 3D arrow (AquariumGamePing3D) marks the spot; the name sits over its top
                float arrowScale = Mathf.Clamp(Vector3.Distance(cam.transform.position, world) / 11f, 1f, 3f);
                var screen = cam.WorldToViewportPoint(world + Vector3.up * 1.9f * arrowScale);
                bool behind = screen.z <= 0;
                if (behind) { screen.x = 1 - screen.x; screen.y = 1 - screen.y; }
                float sx = screen.x * width, sy = (1 - screen.y) * height;
                bool off = behind || sx < 40 || sx > width - 40 || sy < 60 || sy > height - 60;
                if (off)
                {
                    var c = new Vector2(width / 2, height / 2); var d = new Vector2(sx, sy) - c; if (behind) d = -d * 1000;
                    if (d.sqrMagnitude < 1) d = Vector2.down;
                    float k = Mathf.Min((width / 2 - 46) / Mathf.Max(1, Mathf.Abs(d.x)), (height / 2 - 66) / Mathf.Max(1, Mathf.Abs(d.y)));
                    sx = c.x + d.x * k; sy = c.y + d.y * k;
                }
                // off screen: a ripple on arrival, then a steady diamond at the edge pointing the way (with the name)
                if (off && age < .9f) { float g = age / .9f, ring = 22 + 60 * g; Shape(new Rect(sx - ring / 2, sy - ring / 2, ring, ring), new Color(tint.r, tint.g, tint.b, .8f * (1 - g)), ring / 2, 2.5f); }
                float pulse = 1 + .12f * Mathf.Sin(age * 7);
                float s = 18 * pulse;
                if (off)
                {
                    var m = GUI.matrix; GUIUtility.RotateAroundPivot(45, new Vector2(sx, sy));
                    Shape(new Rect(sx - s / 2, sy - s / 2, s, s), new Color(0, .05f, .08f, .65f * fade), 3);
                    Shape(new Rect(sx - s / 2, sy - s / 2, s, s), new Color(tint.r, tint.g, tint.b, fade), 3, 2.5f);
                    Shape(new Rect(sx - 3, sy - 3, 6, 6), new Color(tint.r, tint.g, tint.b, fade), 1);
                    GUI.matrix = m;
                }
                else sy += 26;   // the label just over the arrow
                float metres = local != null ? (mark.at - local.position).Length : 0;
                var old = GUI.color; GUI.color = new Color(1, 1, 1, fade);
                Label(sx - 80, sy - 38, 160, 18, (mark.player == session.LocalId ? "내 핑" : mark.name) + "  " + Mathf.RoundToInt(metres) + "m", Tinted(pingName, Color.Lerp(tint, Color.white, .45f)));
                GUI.color = old;
            }
        }

        private AudioClip PingSound()
        {
            const int rate = 22050; var data = new float[(int)(rate * .32f)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate, env = Mathf.Exp(-t * 11) * Mathf.Clamp01(t * 200);
                data[i] = (Mathf.Sin(2 * Mathf.PI * 1180 * t) * .6f + Mathf.Sin(2 * Mathf.PI * 1770 * t) * .25f) * env * (t < .12f ? 1 : .55f)
                    + Mathf.Sin(2 * Mathf.PI * 1480 * Mathf.Max(0, t - .1f)) * Mathf.Exp(-Mathf.Max(0, t - .1f) * 14) * (t > .1f ? .45f : 0);
            }
            var clip = AudioClip.Create("alien ping", data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }

        // ---- changing room -------------------------------------------------------------------------------------
        private void EnsureLockerRoom()
        {
            if (lockerRoom != null) return;
            // R81: the fish guide is read on the room's computer (AquariumGameR81.cs); the wall posters are gone.
            lockerRoom = AquariumEnvironment.LockerRoom(null, GuidePages());
            guideComputer = lockerRoom.GetComponentInChildren<AquariumGuideComputer>(true);
            if (pingSound == null) pingSound = PingSound();
        }

        // Floor movement in the room: the camera's heading only, so looking down never slows the walk.
        private Point LockerMove()
        {
            float right = DiverAxis(KeyCode.D, KeyCode.RightArrow, KeyCode.A, KeyCode.LeftArrow), forward = DiverAxis(KeyCode.W, KeyCode.UpArrow, KeyCode.S, KeyCode.DownArrow);
            float yaw = followCamera.Yaw * Mathf.Deg2Rad;
            var move = new Point(Mathf.Sin(yaw), 0, Mathf.Cos(yaw)) * forward + new Point(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw)) * right;
            return move.Length > 1 ? move.Normalized : move;
        }

        // While waiting: how long until the dive and what to do meanwhile (R81: at the bottom, above the quick slots).
        private const string LockerHintText = "WASD 걷기   Shift 달리기   마우스 둘러보기   컴퓨터에서 어종 도감을 볼 수 있어요";
        private void LockerHint(AquariumSnapshot state, Swimmer p, float width, float height)
        {
            if (session.IsSpectator || guideOpen || !AquariumLockerRoom.Waiting(p, state.time)) return;   // R81: the open guide shows the countdown itself
            float left = Mathf.Max(0, p.entryAt - AquariumDiveEntry.Seconds - state.time);
            float w = Mathf.Max(460, Measure(smallCentred, LockerHintText).x + 44);   // R81: as wide as its text
            var r = new Rect(width / 2 - w / 2, height - 172, w, 58);   // R81: above the quick slots, clear of the monitor
            Card(r, false, 14);
            Label(r.x, r.y + 6, r.width, 24, Cat("입수까지 ", Int(Mathf.CeilToInt(left)), "초"), Tinted(centred, new Color(1f, .82f, .45f)));
            Label(r.x, r.y + 31, r.width, 20, LockerHintText, smallCentred);
        }

        // Black over the cut from the room to the deck (for whoever's eyes the camera is).
        private void LockerFade(float width, float height)
        {
            if (!session.Connected || session.State.phase != RoundPhase.Playing) return;
            var viewer = PlayerById(session.State.players, followCamera.FollowId);
            float a = AquariumLockerRoom.Fade(viewer, session.State.time);
            if (a > .001f) Panel(new Rect(0, 0, width, height), new Color(0, .01f, .02f, a));
        }
    }
}
