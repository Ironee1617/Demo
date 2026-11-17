using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R84: text that shrinks to fit its box (labels and buttons), separate horizontal / vertical mouse sensitivity,
    // the host's kick button in the waiting room, and the view lock while a diver is shocked.
    public sealed partial class AquariumGame
    {
        // ---- Fit: a one-line text wider than its box is drawn in a smaller size (down to 60%) ------------------
        private struct FitKey { public int style; public string text; public int width; }
        private sealed class FitKeys : IEqualityComparer<FitKey>
        {
            public bool Equals(FitKey a, FitKey b) { return a.style == b.style && a.width == b.width && string.Equals(a.text, b.text); }
            public int GetHashCode(FitKey k) { return (k.style * 397 ^ k.width) * 397 ^ (k.text == null ? 0 : k.text.GetHashCode()); }
        }
        private readonly Dictionary<FitKey, GUIStyle> fitChoice = new Dictionary<FitKey, GUIStyle>(new FitKeys());
        private readonly Dictionary<long, GUIStyle> fitSized = new Dictionary<long, GUIStyle>();
        private readonly Dictionary<GUIStyle, int> fitIds = new Dictionary<GUIStyle, int>();
        private readonly GUIContent fitContent = new GUIContent();

        private GUIStyle Fit(GUIStyle style, string text, float width)
        {
            if (style == null || string.IsNullOrEmpty(text) || style.wordWrap || style.fontSize <= 0 || width <= 8 || text.IndexOf('\n') >= 0) return style;
            int id;
            if (!fitIds.TryGetValue(style, out id)) { id = fitIds.Count + 1; fitIds[style] = id; }
            var key = new FitKey { style = id, text = text, width = (int)width };
            GUIStyle chosen;
            if (fitChoice.TryGetValue(key, out chosen)) return chosen;
            if (fitChoice.Count > 3000) fitChoice.Clear();   // changing texts (timers) come and go
            fitContent.text = text;
            float need = style.CalcSize(fitContent).x;
            chosen = style;
            if (need > width + .5f)
            {
                int size = Mathf.Max(Mathf.CeilToInt(style.fontSize * .6f), Mathf.FloorToInt(style.fontSize * width / need));
                while (size < style.fontSize)
                {
                    long sk = ((long)id << 16) | (uint)size;
                    GUIStyle sized;
                    if (!fitSized.TryGetValue(sk, out sized)) { sized = new GUIStyle(style) { fontSize = size }; fitSized[sk] = sized; }
                    chosen = sized;
                    if (sized.CalcSize(fitContent).x <= width + .5f || size <= Mathf.CeilToInt(style.fontSize * .6f)) break;
                    size--;
                }
            }
            fitChoice[key] = chosen;
            return chosen;
        }

        // ---- Mouse sensitivity, left-right and up-down apart (settings) -----------------------------------------
        private float sensX = -1, sensY = -1;
        private int sensDrag;
        private const float SensMin = .2f, SensMax = 3f;
        private float SensX { get { if (sensX < 0) LoadSens(); return sensX; } }
        private float SensY { get { if (sensY < 0) LoadSens(); return sensY; } }
        private void LoadSens()
        {
            sensX = Mathf.Clamp(PlayerPrefs.GetFloat("Aquarium.SensX", 1), SensMin, SensMax);
            sensY = Mathf.Clamp(PlayerPrefs.GetFloat("Aquarium.SensY", 1), SensMin, SensMax);
        }

        private float SensitivityRows(float sx, float sw, float y)
        {
            float x0 = SensX, y0 = SensY;
            Label(sx, y, 300, 22, "마우스 감도 (좌우)", caption);
            float nx = SensSlider(new Rect(sx, y + 30, sw - 70, 20), x0, 1);
            Label(sx + sw - 56, y + 22, 56, 30, Mathf.RoundToInt(nx * 100) + "%", body);
            y += 70;
            Label(sx, y, 300, 22, "마우스 감도 (상하)", caption);
            float ny = SensSlider(new Rect(sx, y + 30, sw - 70, 20), y0, 2);
            Label(sx + sw - 56, y + 22, 56, 30, Mathf.RoundToInt(ny * 100) + "%", body);
            y += 70;
            if (!Mathf.Approximately(nx, x0)) { sensX = nx; PlayerPrefs.SetFloat("Aquarium.SensX", nx); }
            if (!Mathf.Approximately(ny, y0)) { sensY = ny; PlayerPrefs.SetFloat("Aquarium.SensY", ny); }
            return y;
        }

        private float SensSlider(Rect r, float value, int id)
        {
            var e = Event.current; var hit = new Rect(r.x - 6, r.y - 10, r.width + 12, r.height + 20);
            float t = Mathf.InverseLerp(SensMin, SensMax, value);
            if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && hit.Contains(e.mousePosition)) { sensDrag = id; e.Use(); }
            if (e.rawType == EventType.MouseUp && sensDrag == id) sensDrag = 0;
            if (sensDrag == id && (e.type == EventType.MouseDrag || e.type == EventType.MouseDown || e.type == EventType.Used))
            {
                t = Mathf.Clamp01((e.mousePosition.x - r.x) / r.width);
                value = Mathf.Round(Mathf.Lerp(SensMin, SensMax, t) * 20) / 20f;   // 5% steps
                t = Mathf.InverseLerp(SensMin, SensMax, value);
            }
            float cy = r.y + r.height / 2;
            Shape(new Rect(r.x, cy - 3, r.width, 6), Track, 3);
            Shape(new Rect(r.x, cy - 3, r.width * t, 6), cyan, 3);
            Shape(new Rect(r.x + r.width * t - 10, cy - 10, 20, 20), Color.white, 10);
            Shape(new Rect(r.x + r.width * t - 10, cy - 10, 20, 20), cyan, 10, 2);
            return value;
        }

        // ---- The host's kick button: over the status of the row under the pointer (waiting room) ----------------
        private void KickButton(Swimmer p, Rect row, float x, float y, bool practice)
        {
            if (practice || !session.IsHost || p.bot || p.id == session.LocalId || !row.Contains(Event.current.mousePosition)) return;
            var rect = new Rect(x, y, 90, 26);
            Shape(rect, new Color(0.03f, 0.1f, 0.14f, 1f), 10);
            if (Button(rect, "강퇴") && session.Kick(p.id)) notice = p.name + " 님을 방에서 내보냈습니다.";
        }

        // ---- A shocked diver cannot turn the view ---------------------------------------------------------------
        private bool ShockLocked()
        {
            if (!session.Connected || session.IsSpectator) return false;
            var state = session.State; if (state.phase != RoundPhase.Playing) return false;
            var me = PlayerById(state.players, session.LocalId);
            return me != null && me.alive && me.team == Team.Seeker && state.time < me.stunnedUntil && me.visualAction == "Shocked";
        }
    }
}
