using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Short stacked notifications for purchases, recruits and other run events.
    // They keep real time, so a toast raised before a menu still fades while it is open.
    public sealed partial class TowerGame
    {
        internal sealed class ToastMessage { public string title, body; public Color accent; public float age, life; }
        internal const int MaxToasts = 4;
        private readonly List<ToastMessage> toasts = new List<ToastMessage>();
        internal int ToastCount { get { return toasts.Count; } }
        internal string LatestToast { get { return toasts.Count == 0 ? "" : toasts[toasts.Count - 1].title; } }

        internal void Toast(string title, string body, Color accent, float seconds = 2.8f)
        {
            toasts.Add(new ToastMessage { title = title, body = body, accent = accent, life = Mathf.Max(.8f, seconds) });
            while (toasts.Count > MaxToasts) toasts.RemoveAt(0);
        }
        private void TickToasts(float dt)
        {
            for (int i = toasts.Count - 1; i >= 0; i--)
            { toasts[i].age += dt; if (toasts[i].age >= toasts[i].life) toasts.RemoveAt(i); }
        }
        private void DrawToasts()
        {
            if (toasts.Count == 0) return;
            // Over full-screen menus the stack sits top centre; in play it hangs under the right HUD.
            bool overlay = intro || menu || party || help || shopOpen || VillageBlocking || run.IsRewardPending || hero.hp <= 0 || run.IsComplete;
            float x = overlay ? 590 : 1150, y = overlay ? 14 : 176, width = overlay ? 420 : 386;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                float enter = Mathf.Clamp01(t.age / .22f), leave = Mathf.Clamp01((t.life - t.age) / .35f), a = Mathf.Min(enter, leave);
                float slide = (1 - enter) * (1 - enter) * 48;
                bool body = !string.IsNullOrEmpty(t.body);
                Rect r = new Rect(x + (overlay ? 0 : slide), y - (overlay ? slide * .5f : 0), width, body ? 66 : 44);
                Slice(new Rect(r.x - 10, r.y - 4, r.width + 20, r.height + 20), Kit("panel-shadow"), 44, new Color(1, 1, 1, .7f * a));
                Slice(r, Kit("button-body"), 24, new Color(.07f, .085f, .13f, .96f * a));
                Light(new Rect(r.x + 4, r.y + 3, r.width - 8, r.height - 6), Alpha(t.accent, .1f * a));
                Slice(r, Metal("button-edge", t.accent), 24, Alpha(MetalTint(t.accent), .9f * a));
                Diamond(new Vector2(r.x + 20, r.y + r.height * .5f), 14, Alpha(t.accent, a));
                Diamond(new Vector2(r.x + 20, r.y + r.height * .5f), 6, Alpha(night, a));
                Title(new Rect(r.x + 38, r.y + 7, r.width - 50, 24), t.title, 16, Alpha(paper, a), TextAnchor.MiddleLeft);
                if (body) Label(new Rect(r.x + 38, r.y + 33, r.width - 50, 26), t.body, 13, Alpha(muted, a), TextAnchor.MiddleLeft);
                y += r.height + 8;
            }
        }
    }
}
