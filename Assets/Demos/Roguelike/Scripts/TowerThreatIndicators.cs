using UnityEngine;

namespace PrototypeLab.Roguelike
{
    // Roused monsters closing in from just past the screen show as an arrow on that edge, at their height, larger and
    // bolder the nearer they are. Monsters in view, still asleep, bosses and anything beyond ThreatRange show none.
    public sealed partial class TowerGame
    {
        internal const float ThreatRange = 6, ThreatArrowMin = 20, ThreatArrowMax = 34;
        // Layout margins that keep the arrows clear of the top panels, the companion strip and the screen's very edge.
        private const float ThreatTop = 140, ThreatBottom = 150, ThreatSide = 8;
        private Texture2D uiThreatArrow;

        /// <summary>
        /// How close a roused monster past the screen is to coming into view: 1 at the edge, falling to 0 at
        /// <see cref="ThreatRange"/> past it (its body to the view's rectangle). -1 when it shows no arrow.
        /// </summary>
        internal float ThreatCloseness(Fighter actor)
        {
            EnemyState state;
            if (actor == null || actor.isBoss || actor.hp <= 0 || !actor.root || !view || !enemyCombat.TryGetValue(actor, out state) || !state.alerted) return -1;
            Vector3 at = view.WorldToViewportPoint(actor.pos + Vector2.up * .8f);
            float high = view.orthographicSize * 2, wide = high * view.aspect;
            float outX = Mathf.Max(0, Mathf.Max(-at.x, at.x - 1)) * wide, outY = Mathf.Max(0, Mathf.Max(-at.y, at.y - 1)) * high;
            float gap = Mathf.Sqrt(outX * outX + outY * outY);
            return gap <= 0 || gap > ThreatRange ? -1 : 1 - gap / ThreatRange;
        }

        /// <summary>Draws the off-screen threat arrows beneath the play HUD while the floor runs; never over cards, menus or a pause.</summary>
        private void DrawThreatIndicators()
        {
            if (Event.current.type != EventType.Repaint || intro || Paused || village || practice || !view) return;
            float left = -uiOffset.x / uiScale + ThreatSide, right = (Screen.width - uiOffset.x) / uiScale - ThreatSide;
            float top = -uiOffset.y / uiScale + ThreatTop, bottom = (Screen.height - uiOffset.y) / uiScale - ThreatBottom;
            foreach (var actor in enemies)
            {
                float near = ThreatCloseness(actor); if (near < 0) continue;
                Vector2 body = WorldGui(actor.pos + Vector2.up * .8f);
                float size = Mathf.Lerp(ThreatArrowMin, ThreatArrowMax, near), half = size * .5f + 3;
                var center = new Vector2(Mathf.Clamp(body.x, left + half, right - half), Mathf.Clamp(body.y, top + half, bottom - half));
                Vector2 toward = body - center; if (toward.sqrMagnitude < .01f) continue;
                // The world labels' colours: crimson for walkers, silver for flyers, gold leaf for elites.
                Color tone = actor.isElite ? brass : actor.airborne ? silver : crimson;
                ThreatArrow(center, Mathf.Atan2(toward.y, toward.x) * Mathf.Rad2Deg, size, tone, actor.isElite ? paper : brass, Mathf.Lerp(.5f, 1, near));
            }
        }

        /// <summary>An arrowhead of <paramref name="size"/> centred on <paramref name="center"/>, turned to <paramref name="angle"/> degrees and rimmed.</summary>
        private void ThreatArrow(Vector2 center, float angle, float size, Color tone, Color rim, float alpha)
        {
            EnsureThreatArrow();
            var saved = GUI.matrix;
            GUI.matrix = saved * Matrix4x4.TRS(center, Quaternion.Euler(0, 0, angle), Vector3.one);
            float edge = size + 6, shade = size + 10, glow = size * 2.2f;
            Halo(new Rect(-glow * .5f, -glow * .5f, glow, glow), Alpha(tone, .2f * alpha));
            GUI.color = Alpha(night, .6f * alpha); GUI.DrawTexture(new Rect(-shade * .5f, -shade * .5f, shade, shade), uiThreatArrow);
            GUI.color = Alpha(rim, .9f * alpha); GUI.DrawTexture(new Rect(-edge * .5f, -edge * .5f, edge, edge), uiThreatArrow);
            GUI.color = Alpha(tone, alpha); GUI.DrawTexture(new Rect(-size * .5f, -size * .5f, size, size), uiThreatArrow);
            GUI.color = Color.white; GUI.matrix = saved;
        }

        /// <summary>A soft-edged arrowhead pointing right, its back notched; four by four samples a pixel.</summary>
        private void EnsureThreatArrow()
        {
            if (uiThreatArrow) return;
            const int n = 64;
            uiThreatArrow = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "UI threat arrow", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            Vector2 tip = new Vector2(60, 32), upper = new Vector2(10, 6), lower = new Vector2(10, 58), notch = new Vector2(24, 32);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int inside = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                        {
                            var p = new Vector2(x + (sx + .5f) / 4, y + (sy + .5f) / 4);
                            if (ArrowCovers(p, tip, upper, notch) || ArrowCovers(p, tip, notch, lower)) inside++;
                        }
                    uiThreatArrow.SetPixel(x, y, new Color(1, 1, 1, inside / 16f));
                }
            uiThreatArrow.Apply();
        }

        /// <summary>Whether <paramref name="p"/> lies inside the triangle a, b, c.</summary>
        private static bool ArrowCovers(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float ab = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float bc = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float ca = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            return !((ab < 0 || bc < 0 || ca < 0) && (ab > 0 || bc > 0 || ca > 0));
        }
    }
}
