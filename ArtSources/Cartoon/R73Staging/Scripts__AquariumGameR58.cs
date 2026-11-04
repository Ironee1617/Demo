using UnityEngine;

namespace PrototypeLab.Aquarium
{
    // R58: the lobby's map selection. A map chip under the player list shows the next game's map and opens the
    // "맵 선택" tab of the right-hand panel: one card per map with its overview picture (rendered from the real
    // map, Resources/AquariumMaps/Map{id}), what it is like, its size and the selected state. The host (and the
    // practice player) picks; everyone else sees the host's pick, read-only.
    public sealed partial class AquariumGame
    {
        private const int MapTab = 2;
        private Texture2D[] mapThumbs;
        private int lastSeenMap = -1;
        private Texture2D MapThumb(int map)
        {
            if (mapThumbs == null) mapThumbs = new Texture2D[AquariumMaps.Count];
            map = AquariumMaps.Clamp(map);
            if (mapThumbs[map] == null) mapThumbs[map] = Resources.Load<Texture2D>("AquariumMaps/Map" + map);
            return mapThumbs[map];
        }
        private bool CanPickMap { get { return session.IsHost && GUI.enabled; } }
        private void PickMap(int map)
        {
            if (!session.IsHost || map == session.Map) return;
            PlayerPrefs.SetInt("Aquarium.Map", map); PlayerPrefs.Save();
            session.SetMap(map); notice = "다음 게임 맵: " + AquariumMaps.Name(map);
        }
        // A client hears about it when the host switches the map.
        private void WatchHostMap()
        {
            int map = session.Map;
            if (lastSeenMap >= 0 && map != lastSeenMap && !session.IsHost) notice = "방장이 맵을 " + AquariumMaps.Name(map) + "(으)로 바꿨습니다.";
            lastSeenMap = map;
        }

        // R70b: the large map card in the left card (where the practice tutorials were): the picture, the name and
        // what the map is like. The host (and the practice player) flips through the maps with the arrows on the
        // picture; anyone can open the full map tab by clicking the card.
        private void LobbyMapCard(Rect rect)
        {
            WatchHostMap();
            int map = session.Map; bool open = lobbyTab == MapTab, compact = rect.height < 110;
            var e = Event.current; bool hover = GUI.enabled && rect.Contains(e.mousePosition);
            Shape(rect, open ? new Color(0.12f, 0.3f, 0.38f, 0.92f) : hover ? new Color(0.1f, 0.26f, 0.34f, 0.9f) : new Color(0.06f, 0.19f, 0.26f, 0.86f), 12);
            Shape(rect, open || hover ? new Color(cyan.r, cyan.g, cyan.b, .8f) : Rim, 12, 1.4f);
            float pad = compact ? 6 : 10, ph = rect.height - pad * 2, pw = Mathf.Min(ph * 1.6f, rect.width * .56f);
            var picture = new Rect(rect.x + pad, rect.y + pad, pw, ph);
            var thumb = MapThumb(map);
            if (thumb != null) GUI.DrawTexture(picture, thumb, ScaleMode.ScaleAndCrop, false, 0, Color.white, 0, 8);
            else Shape(picture, new Color(0.1f, 0.3f, 0.36f, 1), 8);
            Shape(picture, new Color(cyan.r, cyan.g, cyan.b, .5f), 8, 1.2f);
            float tx = picture.xMax + 12, tw = rect.xMax - tx - 10;
            Label(tx, rect.y + pad, tw, 18, session.IsHost ? "다음 게임 맵" : "방장이 고른 맵", caption);
            Label(tx, rect.y + pad + 16, tw, 30, AquariumMaps.Name(map), Tinted(heading, Color.white));
            string more = open ? "닫기 ›" : session.IsHost ? "자세히 보고 고르기 ›" : "자세히 보기 ›";
            if (compact) { float mw = Measure(small, more).x + 4; Label(rect.xMax - 12 - mw, rect.y + pad, mw, 20, more, Tinted(small, cyan)); }
            else Label(tx, rect.yMax - pad - 20, tw, 20, more, Tinted(small, cyan));
            // the host's arrows on the picture
            bool flipped = false;
            if (CanPickMap && AquariumMaps.Count > 1)
            {
                const float b = 30;
                var left = compact ? new Rect(rect.xMax - 2 * b - 18, rect.yMax - b - 8, b, b) : new Rect(picture.x + 4, picture.center.y - b / 2, b, b);
                var right = compact ? new Rect(rect.xMax - b - 12, rect.yMax - b - 8, b, b) : new Rect(picture.xMax - b - 4, picture.center.y - b / 2, b, b);
                if (Button(left, "‹")) { PickMap((map + AquariumMaps.Count - 1) % AquariumMaps.Count); flipped = true; }
                if (Button(right, "›")) { PickMap((map + 1) % AquariumMaps.Count); flipped = true; }
            }
            if (!flipped && GUI.Button(rect, GUIContent.none, GUIStyle.none)) lobbyTab = open ? 0 : MapTab;
        }

        // The right-hand panel's map tab.
        private void MapSelect(float x, float y, float width, float height)
        {
            Card(new Rect(x, y, width, height), true, 18);
            Label(x + 24, y + 18, 300, 34, "맵 선택", heading);
            bool host = session.IsHost, practice = session.Practice;
            Label(x + 24, y + 54, width - 48, 23, practice ? "연습할 맵을 고르세요. 카드를 누르면 바로 바뀝니다."
                : host ? "다음 게임의 맵을 고르세요. 방의 모든 참가자에게 바로 보입니다."
                : "맵은 방장이 고릅니다. 지금 고른 맵으로 게임이 시작됩니다.", small);
            int current = session.Map; const float gap = 16;
            float cardWidth = (width - 48 - gap * (AquariumMaps.Count - 1)) / AquariumMaps.Count, top = y + 92, cardHeight = height - 92 - 66;   // R73: one column per map
            var e = Event.current;
            for (int map = 0; map < AquariumMaps.Count; map++)
            {
                var card = new Rect(x + 24 + map * (cardWidth + gap), top, cardWidth, cardHeight);
                bool chosen = map == current, hover = CanPickMap && !chosen && card.Contains(e.mousePosition);
                Shape(card, chosen ? new Color(0.1f, 0.28f, 0.35f, 0.9f) : hover ? new Color(0.09f, 0.24f, 0.31f, 0.85f) : new Color(0.05f, 0.15f, 0.21f, 0.75f), 14);
                Shape(card, chosen ? cyan : hover ? new Color(cyan.r, cyan.g, cyan.b, .6f) : Rim, 14, chosen ? 2.4f : 1.2f);
                var picture = new Rect(card.x + 12, card.y + 12, card.width - 24, Mathf.Min(card.height - 130, (card.width - 24) * .9f));   // R73: taller pictures, no text
                var thumb = MapThumb(map);
                if (thumb != null) GUI.DrawTexture(picture, thumb, ScaleMode.ScaleAndCrop, false, 0, chosen ? Color.white : new Color(.82f, .88f, .9f, 1), 0, 10);
                else Shape(picture, new Color(0.08f, 0.25f, 0.3f, 1), 10);
                Shape(picture, chosen ? new Color(cyan.r, cyan.g, cyan.b, .9f) : Rim, 10, 1.2f);
                if (chosen)
                {
                    string badge = host ? "✓ 선택됨" : "✓ 방장 선택";
                    float bw = small.CalcSize(new GUIContent(badge)).x + 26; var pill = new Rect(picture.xMax - bw - 10, picture.y + 10, bw, 28);
                    Shape(pill, new Color(cyan.r, cyan.g, cyan.b, .95f), 14);
                    GUI.Label(pill, badge, Tinted(smallCentred, new Color(0.02f, 0.12f, 0.16f)));
                }
                float ty = picture.yMax + 14;
                Label(card.x + 16, ty, card.width - 32, 32, AquariumMaps.Name(map), chosen ? Tinted(heading, cyan) : heading);
                // R73: the tank's name only (no descriptions).
                var action = new Rect(card.x + 12, card.yMax - 54, card.width - 24, 42);
                if (chosen) { Shape(action, new Color(cyan.r, cyan.g, cyan.b, .16f), 10); GUI.Label(action, "선택 중", Tinted(smallCentred, cyan)); }
                else if (CanPickMap) { if (Button(action, "이 맵으로 하기")) PickMap(map); }
                else { Shape(action, new Color(0.04f, 0.12f, 0.17f, .6f), 10); GUI.Label(action, "방장만 바꿀 수 있습니다", Tinted(smallCentred, muted)); }
                if (CanPickMap && !chosen && GUI.Button(new Rect(card.x, card.y, card.width, action.y - card.y), GUIContent.none, GUIStyle.none)) PickMap(map);
            }
            if (!host) Label(x + 24, y + height - 50, width - 48, 40, "보기 전용: 방장이 맵을 바꾸면 여기에 바로 보입니다.", small);
        }
    }
}
