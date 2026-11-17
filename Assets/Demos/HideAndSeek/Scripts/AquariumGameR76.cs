using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R76: the aliens' roster on the left, the diver's "aliens still in the tank" count, the drone dart (slot, sound,
    // tracer), the diver's skill unlock flash and sound, and the warning the diver hears when an alien starts on a
    // screw or a lid.
    public sealed partial class AquariumGame
    {
        private readonly HashSet<int> r76Seen = new HashSet<int>();
        private int r76Round = -1;
        // (R78a: the start-of-work warning is gone, see TrackR47Effects.)
        private bool unlockPlayed;

        // ---- HUD: the alien roster (top left, under the fish goal and the eggs) ---------------------------------
        private void AlienRoster(AquariumSnapshot state, Swimmer me, float y)
        {
            if (state.players == null) return;
            int n = 0; foreach (var s in state.players) if (s.team == Team.Hider) n++;
            if (n == 0) return;
            const float rowH = 30, w = 236;
            Card(new Rect(24, y, w, 12 + n * rowH));
            float ry = y + 6;
            foreach (var s in state.players)
            {
                if (s.team != Team.Hider) continue;
                var kind = AquariumAlienKinds.Get(s.alienKind); var col = new Color(kind.r, kind.g, kind.b);
                bool gone = !s.alive && !s.escaped;
                if (s.id == me.id) Shape(new Rect(28, ry, w - 8, rowH - 2), new Color(1, 1, 1, .07f), 8);
                // the alien's silhouette in its colour (dim when down)
                Icon(34, ry + 3, HudIcon.Unmorph, gone ? new Color(col.r * .4f, col.g * .4f, col.b * .4f, .85f) : col, 22);
                Label(62, ry + 4, 86, 22, s.name.Length > 7 ? s.name.Substring(0, 7) + "…" : s.name, small);
                // lives: one dot per harpoon hit it can take
                int total = AquariumModel.HitsToKill(s), left = s.alive ? Mathf.Max(0, total - s.harpoonHits) : 0;
                for (int i = 0; i < total; i++) Shape(new Rect(150 + i * 9, ry + 11, 7, 7), i < left ? new Color(1f, .45f, .5f) : Track, 3.5f);
                string status; Color tint;
                if (s.escaped) { status = "탈출"; tint = cyan; }
                else if (s.alive) { status = "생존"; tint = new Color(.55f, 1f, .55f); }
                else
                {
                    float hatch = HatchFor(state, s.id);
                    if (hatch > 0) { status = Cat("부화 대기 ", Int(Mathf.CeilToInt(Mathf.Max(0, hatch - state.time))), "s"); tint = Warm; }
                    else { status = "사망"; tint = Danger; }
                }
                Label(180, ry + 4, 80, 22, status, Tinted(small, tint));
                ry += rowH;
            }
        }

        private static float HatchFor(AquariumSnapshot state, int id)
        {
            if (state.eggs == null) return -1;
            foreach (var e in state.eggs) if (e.revivePlayer == id) return e.hatchAt;
            return -1;
        }

        // ---- HUD: the diver's count of aliens still in the tank, right of the catch count -----------------------
        private void AliveAliensPill(float x, float y)
        {
            var state = session.State; if (state == null || state.players == null) return;
            int alive = 0; foreach (var s in state.players) if (s.team == Team.Hider && s.alive && !s.escaped) alive++;
            string text = Cat("생존  ", Int(alive));
            float w = 48 + Measure(body, text).x;
            var rect = new Rect(x, y, w, 32);
            var green = new Color(.55f, 1f, .55f);
            Pill(rect, new Color(green.r, green.g, green.b, .6f));
            Icon(rect.x + 11, rect.y + 6, HudIcon.Unmorph, green, 20);
            Label(rect.x + 38, rect.y + 2, w - 40, 28, text, body);
        }

        // ---- HUD: the drone's dart slot (LMB while flying the drone) --------------------------------------------
        private void DartSlot(AquariumSnapshot state, Swimmer p, float x, float y, float size)
        {
            float ready = Mathf.Max(p.nextDart, state.huntAt);
            Slot(x, y, size, "LMB", "마취침", Ready(ready, state.time),
                p.nextDart > state.time && state.time >= state.huntAt ? 1 - Mathf.Clamp01((p.nextDart - state.time) / AquariumModel.DartCooldown) : -1, HudIcon.Electric);
        }

        // ---- HUD: the R slot's unlock flash (3.5 s from the unlock) ---------------------------------------------
        private void UnlockFlash(float x, float y, float size, float elapsed)
        {
            if (elapsed < 0 || elapsed > 3.5f || Event.current.type != EventType.Repaint) return;
            float fade = elapsed < 3f ? 1 : (3.5f - elapsed) / .5f;
            var gold = new Color(1f, .84f, .3f);
            Shape(new Rect(x, y, size, size), new Color(gold.r, gold.g, gold.b, .28f * fade * (.6f + .4f * Mathf.Sin(elapsed * 12))), 12);
            for (int k = 0; k < 2; k++)
            {
                float u = Mathf.Repeat(elapsed * 1.1f + k * .5f, 1f), grow = 4 + 24 * u;
                Shape(new Rect(x - grow, y - grow, size + 2 * grow, size + 2 * grow), new Color(gold.r, gold.g, gold.b, .8f * (1 - u) * fade), 12 + grow, 3);
            }
            float bob = Mathf.Sin(elapsed * 5) * 2;
            Label(x - 40, y - 34 + bob, size + 80, 24, "사용 가능!", Tinted(smallCentred, new Color(gold.r, gold.g, gold.b, fade)));
        }

        // ---- Sounds and the dart tracer (from UpdateR74Sounds while a round is played) -------------------------
        private void TrackR76(AquariumSnapshot state)
        {
            if (state.round != r76Round) { r76Round = state.round; r76Seen.Clear(); unlockPlayed = false; darts.Clear(); }
            var me = session.IsSpectator ? null : PlayerById(state.players, session.LocalId);
            // The diver's skill unlocks (R slot): the unlock sound once.
            float unlock = AquariumModel.DiverAbilityAt(state.huntAt);
            if (!unlockPlayed && me != null && me.team == Team.Seeker && state.time >= unlock)
            { unlockPlayed = true; if (state.time - unlock < 2f) PlayCue("unlock", .9f); }
            var local = Local();
            bool diverView = local != null && (local.team == Team.Seeker || followCamera.SpectatingDiver);
            if (state.effects != null)
                foreach (var e in state.effects)
                {
                    if (!r76Seen.Add(e.id) || state.time - e.born > 1f) continue;
                    switch (e.kind)
                    {
                        // R78a: no warning when an alien only starts on a screw or a lid; the alert comes when the screws
                        // are all out and when the lid is off (TrackR47Effects).
                        case "dart":
                            PlayClipAt(Real("shot"), e.position, e.owner, 20f, .55f, 1.8f);
                            darts.Add(new DartLine { a = V3(e.position), b = V3(e.position + e.direction), born = Time.unscaledTime });
                            break;
                        case "dartHit": PlayClipAt(Real("shock"), e.position, e.owner, 20f, .75f, 1.35f); break;
                    }
                }
            UpdateDartLines();
        }

        private static Vector3 V3(Point p) { return new Vector3(p.x, p.y, p.z); }

        private struct DartLine { public Vector3 a, b; public float born; }
        private readonly List<DartLine> darts = new List<DartLine>();
        private LineRenderer[] dartLines;
        private const float DartLineSeconds = .4f;

        private void UpdateDartLines()
        {
            for (int i = darts.Count - 1; i >= 0; i--) if (Time.unscaledTime - darts[i].born > DartLineSeconds) darts.RemoveAt(i);
            if (dartLines == null)
            {
                if (darts.Count == 0) return;
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                dartLines = new LineRenderer[4];
                var mat = shader != null ? new Material(shader) : null;
                for (int i = 0; i < dartLines.Length; i++)
                {
                    var go = new GameObject("dart line"); go.transform.SetParent(transform, false);
                    var lr = go.AddComponent<LineRenderer>();
                    if (mat != null) lr.sharedMaterial = mat;
                    lr.positionCount = 2; lr.widthMultiplier = .035f; lr.numCapVertices = 2; lr.useWorldSpace = true;
                    lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false; lr.enabled = false;
                    dartLines[i] = lr;
                }
            }
            for (int i = 0; i < dartLines.Length; i++)
            {
                var lr = dartLines[i];
                if (i >= darts.Count) { if (lr.enabled) lr.enabled = false; continue; }
                var d = darts[i]; float a = 1 - (Time.unscaledTime - d.born) / DartLineSeconds;
                lr.enabled = true; lr.SetPosition(0, d.a); lr.SetPosition(1, d.b);
                lr.startColor = new Color(.8f, 1f, .9f, a * .9f); lr.endColor = new Color(.8f, 1f, .9f, a * .35f);
            }
        }

        private void HideDartLines()
        {
            darts.Clear();
            if (dartLines != null) foreach (var lr in dartLines) if (lr != null) lr.enabled = false;
        }
    }
}
